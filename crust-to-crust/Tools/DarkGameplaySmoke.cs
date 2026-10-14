using Godot;
using System;
using System.Linq;
using System.Reflection;

public partial class DarkGameplaySmoke : Node
{
    private int _failures;
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private void Check(bool ok,string text) { if(ok) GD.Print("PASS: "+text); else { _failures++; GD.PushError("FAIL: "+text); } }
    private async System.Threading.Tasks.Task Wait(float seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            if(DisplayServer.GetName()!="headless") DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
            SaveData.Load(); SaveData.TutorialComplete=true;
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            level.SetRunSeed(17); await Wait(0.1f);
            Check(level.CaveAmbient.Color.R<0.05f && level.CaveAmbient.Color.B<0.08f,"tunnel ambient is dark rather than broadly illuminated");
            Check(level.CurrentObstacleInterval<2.3f,"early encounter density increased");
            var hud=level.GetNode<Hud>("HUD"); hud.StartRun();
            for(int i=0;!level.IsGameActive && i<100;i++) await Wait(0.1f);
            var player=level.Runner; player.GetNode<Area2D>("Hitbox").Monitoring=false;
            float initial=player.LanternSeconds;
            await Wait(0.3f);
            Check(player.LanternSeconds<initial-0.2f,"active lantern consumes charge in real gameplay");
            float paused=player.LanternSeconds; GetTree().Paused=true; await Wait(0.2f); GetTree().Paused=false;
            Check(Mathf.IsEqualApprox(player.LanternSeconds,paused),"pause preserves actual player charge");
            level.SetProcess(false); level.SetPhysicsProcess(false); player.SetProcess(false); player.SetPhysicsProcess(false);
            level.SetTunnelHazardsVisible(false); await Wait(0.05f);
            level.FallSpeed=500; typeof(LevelGenerator).GetField("_coreRespite",Hidden).SetValue(level,0f);
            typeof(LevelGenerator).GetProperty("Encounter").SetValue(level,EncounterKind.Slalom);
            var charge=(LanternCharge)typeof(Player).GetField("_charge",Hidden).GetValue(player);
            float frozen=charge.Seconds; level.IsGameActive=false; player._Process(3);
            Check(Mathf.IsEqualApprox(frozen,charge.Seconds),"cinematic or stopped tunnel does not drain charge");
            level.IsGameActive=true;
            charge.Tick(100,true); player._Process(0);
            Check(!player.GetNode<PointLight2D>("MouseLantern").Enabled,"empty charge switches the actual light off");
            charge.Refill(); player._Process(0);
            Check(player.GetNode<PointLight2D>("MouseLantern").Enabled,"battery restores a wanted empty lantern");
            typeof(Player).GetField("_lampWanted",Hidden).SetValue(player,false);
            frozen=charge.Seconds; player._Process(3);
            Check(Mathf.IsEqualApprox(frozen,charge.Seconds),"switched-off light does not drain charge");
            typeof(Player).GetField("_lampWanted",Hidden).SetValue(player,true);
            charge.Tick(12,true); float before=charge.Seconds; int wallet=SaveData.Wallet;
            var battery=GD.Load<PackedScene>("res://Scenes/Obstacles/Coin.tscn").Instantiate<Coin>(); level.AddChild(battery);
            battery.SetKind(PickupKind.Battery); battery.Show(); battery.SetProcess(false);
            var collect=typeof(Player).GetMethod("OnAreaEntered",Hidden);
            collect.Invoke(player,new object[] {battery}); collect.Invoke(player,new object[] {battery});
            Check(Mathf.IsEqualApprox(charge.Seconds,Mathf.Min(18,before+5)) && SaveData.Wallet==wallet,"battery adds five seconds once, without coins or multiplier");
            var mount=new WallShooter(); level.AddChild(mount); mount.Activate(level,0,-1,160); mount.SetPhysicsProcess(false);
            Check(level.TryFireVolley(mount,0,new Vector2(220,-200)),"early upward shot can fire with a reachable dodge");
            var shots=level.GetChildren().OfType<HazardProjectile>().Where(p=>p.Visible).ToArray();
            Check(shots.Length==1 && shots.All(p=>p.Path.Velocity.Y<0 && -p.Path.Velocity.Y>=Mathf.Abs(p.Path.Velocity.X)),"turret shots are steep upward diagonals");
            level.StopFalling(); level.IsGameActive=true; level.FallSpeed=500;
            mount.Activate(level,0,-1,player.FixedY+100);
            Check(!level.TryFireVolley(mount,0,player.Position),"turret cannot fire near player's height");
            mount.Activate(level,2,1,player.FixedY+WallShooter.PreferredFireGap);
            Check(level.TryFireVolley(mount,2,new Vector2(-120,player.FixedY)),"wall geode releases a reachable shard fan");
            shots=level.GetChildren().OfType<HazardProjectile>().Where(p=>p.Visible).ToArray();
            Check(shots.Length==3 && shots.All(p=>p.Path.Velocity.Y<0 && -p.Path.Velocity.Y>Mathf.Abs(p.Path.Velocity.X)),"geode emits three steep upward shards");
            level.StopFalling(); level.IsGameActive=true; level.FallSpeed=500;
            typeof(LevelGenerator).GetField("_batteryTimer",Hidden).SetValue(level,0f);
            typeof(LevelGenerator).GetField("_nextBatteryDelay",Hidden).SetValue(level,10f);
            var timers=typeof(LevelGenerator).GetMethod("SpawnTimers",Hidden);
            int spawned=level.BatteriesSpawned;
            timers.Invoke(level,new object[] {9.9f});
            Check(level.BatteriesSpawned==spawned,"battery does not arrive early");
            timers.Invoke(level,new object[] {0.11f});
            float delay=(float)typeof(LevelGenerator).GetField("_nextBatteryDelay",Hidden).GetValue(level);
            Check(level.BatteriesSpawned==spawned+1 && delay>=12 && delay<=16,"first battery spawns at ten seconds then uses a twelve-to-sixteen-second gap");
            var spawnedBattery=level.GetChildren().OfType<Coin>().First(p=>p.Visible && p.Kind==PickupKind.Battery);
            Check(Mathf.Abs(spawnedBattery.Position.X-player.Position.X)>=60,"battery requires a deliberate side movement");
            frozen=charge.Seconds; level.RebaseEmergence();
            Check(Mathf.IsEqualApprox(frozen,charge.Seconds),"next traversal preserves charge rather than refilling");
            level.Free(); level=null; await Wait(0.15f);
        }
        catch(Exception ex) { _failures++; GD.PushError(ex.ToString()); GetTree().Paused=false; level?.Free(); await Wait(0.15f); }
        GD.Print("DARK GAMEPLAY SMOKE: "+_failures+" failures"); GetTree().Quit(_failures==0 ? 0 : 1);
    }
}
