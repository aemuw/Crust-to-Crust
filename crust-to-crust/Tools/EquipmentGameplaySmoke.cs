using Godot;
using System;
using System.Reflection;

public partial class EquipmentGameplaySmoke : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private int _failures;
    private void Check(bool ok,string text) { if(ok) GD.Print("PASS: "+text); else { _failures++; GD.PushError("FAIL: "+text); } }
    private async System.Threading.Tasks.Task Wait(float seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private static object Field(object node,string key)=>node.GetType().GetField(key,Hidden).GetValue(node);
    public override async void _Ready()
    {
        const string save="user://crust_save.cfg";
        bool existed=FileAccess.FileExists(save); byte[] original=existed ? FileAccess.GetFileAsBytes(save) : null;
        LevelGenerator level=null;
        try
        {
            if(DisplayServer.GetName()!="headless") DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
            var cfg=new ConfigFile(); cfg.SetValue("shop","wallet",10000);
            foreach(string key in new[] {"insulation","cooling","lantern","accumulator","battery_efficiency","magnet","shield_recovery"}) cfg.SetValue("shop",key,3);
            cfg.SetValue("progress","tutorial_complete",true); cfg.Save("user://crust_save.cfg");
            typeof(SaveData).GetField("_loaded",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
            SaveData.Load(); level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            level.GetNode<Hud>("HUD").StartRun();
            for(int i=0;!level.IsGameActive && i<100;i++) await Wait(0.1f);
            Check(level.IsGameActive,"upgraded run finishes intro");
            var player=level.Runner; player.SetProcess(false); player.SetPhysicsProcess(false); player.GetNode<Area2D>("Hitbox").Monitoring=false;
            level.SetProcess(false); level.SetPhysicsProcess(false); level.SetTunnelHazardsVisible(false); level.FallSpeed=0;
            var charge=(LanternCharge)Field(player,"_charge");
            Check(charge.Capacity==24 && charge.Seconds>19,"bought accumulator configures the actual run charge");
            Check(Mathf.IsEqualApprox(player.GetNode<PointLight2D>("MouseLantern").TextureScale,1.16f),"bought lens extends the real light cone");
            level.CurrentDepth=level.CoreDepthPoint;
            typeof(Player).GetField("_coolingGrace",Hidden).SetValue(player,0f);
            typeof(Player).GetField("_temperature",Hidden).SetValue(player,0f);
            typeof(SaveData).GetProperty("Insulation").SetValue(null,0);
            typeof(Player).GetMethod("UpdateHeating",Hidden).Invoke(player,new object[] {1f});
            float uninsulated=player.Temperature;
            typeof(SaveData).GetProperty("Insulation").SetValue(null,3);
            typeof(Player).GetField("_temperature",Hidden).SetValue(player,0f);
            typeof(Player).GetMethod("UpdateHeating",Hidden).Invoke(player,new object[] {1f});
            Check(uninsulated>0 && Mathf.IsEqualApprox(player.Temperature,uninsulated*0.64f),"bought insulation reduces actual heating by thirty-six percent");
            typeof(Player).GetMethod("Extinguish",Hidden).Invoke(player,new object[] {true});
            Check(Mathf.IsEqualApprox((float)Field(player,"_coolingGrace"),13),"bought cooling gives thirteen seconds after cryogas");
            Coin Pickup(PickupKind kind,Vector2 at)
            {
                var p=GD.Load<PackedScene>("res://Scenes/Obstacles/Coin.tscn").Instantiate<Coin>(); level.AddChild(p); p.Position=at; p.speed=0; p.SetKind(kind); p.Show(); p.SetProcess(false); return p;
            }
            var collect=typeof(Player).GetMethod("OnAreaEntered",Hidden);
            charge.Tick(100,true); var cell=Pickup(PickupKind.Battery,player.Position); collect.Invoke(player,new object[] {cell});
            Check(charge.Seconds==6.5f,"bought battery contacts refill six and a half seconds");
            var magnet=Pickup(PickupKind.Magnet,player.Position); collect.Invoke(player,new object[] {magnet});
            Check((float)Field(player,"_magnetTimer")==20 && player.MagnetRadius==280,"bought coil extends actual magnet duration and radius");
            var coin=Pickup(PickupKind.Coin,player.Position+new Vector2(250,0)); float coinX=coin.Position.X; coin._Process(0.1);
            Check(coin.Position.X<coinX-40,"upgraded magnet attracts a coin beyond the base radius");
            cell=Pickup(PickupKind.Battery,player.Position+new Vector2(250,0)); float cellX=cell.Position.X; cell._Process(0.1);
            Check(Mathf.IsEqualApprox(cell.Position.X,cellX),"upgraded magnet still cannot collect batteries");
            typeof(Player).GetMethod("GrantShield",Hidden).Invoke(player,null); player.ReceiveProjectileHit();
            Check(!player.HasShield && Mathf.IsEqualApprox((float)Field(player,"_invulnerableTimer"),1.8f),"bought stabilizer extends real shield break protection");
            level.Free(); level=null; await Wait(0.2f);
        }
        catch(Exception error) { _failures++; GD.PushError(error.ToString()); GetTree().Paused=false; level?.Free(); await Wait(0.2f); }
        finally
        {
            if(existed) { using var file=FileAccess.Open(save,FileAccess.ModeFlags.Write); file.StoreBuffer(original); }
            else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save));
        }
        GD.Print("EQUIPMENT GAMEPLAY SMOKE: "+_failures+" failures"); GetTree().Quit(_failures==0 ? 0 : 1);
    }
}
