using Godot;
using System;
using System.Reflection;

public partial class ShooterSmoke : Node
{
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    public override async void _Ready()
    {
        try
        {
            Check(!HazardEncounterRules.HasEscape(0,380,48,500,
                new[] { new ProjectilePath(new Vector2(0,300),new Vector2(0,-400)) },
                new[] { new Rect2(-1000,-10000,2000,20000) }),"fully blocked combination is rejected");
            Check(HazardEncounterRules.HasEscape(0,380,48,500,
                new[] { new ProjectilePath(new Vector2(0,300),new Vector2(0,-400)) },Array.Empty<Rect2>()),"telegraphed single shot leaves reachable dodge");
            Check(HazardProjectile.SweepHits(new Vector2(0,200),new Vector2(0,-400),Vector2.Zero,48),"fast projectile sweep catches tunneling");
            Check(!HazardProjectile.SweepHits(new Vector2(150,200),new Vector2(150,-400),Vector2.Zero,48),"nearby dodge is not hit");
            SaveData.Load(); SaveData.TutorialComplete=true;
            var level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var shooter=new WallShooter(); level.AddChild(shooter);
            shooter.Activate(level,0,-1,600);
            Check(shooter.Visible && shooter.GlobalPosition.X < -180,"sentry is anchored to cave wall");
            Check(shooter.ChargeRemaining>=0.9f,"sentry has a full warning before firing");
            float charge=shooter.ChargeRemaining;
            GetTree().Paused=true;
            await ToSignal(GetTree().CreateTimer(0.1,true),SceneTreeTimer.SignalName.Timeout);
            Check(shooter.ChargeRemaining==charge,"pause freezes charge"); GetTree().Paused=false;
            shooter.Deactivate(); Check(!shooter.Visible,"deactivation removes sentry");
            level.SetProcess(false); level.SetPhysicsProcess(false); level.IsGameActive=true; level.FallSpeed=2200;
            var high=new WallShooter(); level.AddChild(high); high.Activate(level,0,-1,3000); high.SetPhysicsProcess(false);
            high._PhysicsProcess(1);
            bool waiting=!(bool)typeof(WallShooter).GetField("_fired",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(high);
            GD.Print("REGRESSION charged offscreen waits: "+waiting);
            var dash=GD.Load<PackedScene>("res://Scenes/Obstacles/Obstacle.tscn").Instantiate<Obstacle>(); level.AddChild(dash);
            dash.ConfigureVariant(4); dash.MotionDifficulty=1; dash.Position=new Vector2(-100,-200+1000*2.6f); dash.speed=1000; dash.TriggerAppear(Vector2.One); dash.SetProcess(false);
            for(int i=0;i<255;i++) dash._Process(0.01);
            bool repeated=(int)typeof(Obstacle).GetField("_dashCount",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dash)>0;
            GD.Print("REGRESSION second dash warned: "+repeated);
            var player=level.Runner;
            var state=typeof(Player).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic);
            state.SetValue(player,Enum.Parse(state.FieldType,"Playing")); player.SetProcess(false); player.SetPhysicsProcess(false);
            player.Position=new Vector2(-100,-200); player._PhysicsProcess(0);
            player.Position=new Vector2(100,-200);
            bool relative=player.ProjectileSweepHits(new Vector2(0,-200),new Vector2(0,-200),8);
            GD.Print("REGRESSION player crosses stationary projectile: "+relative);
            Check(waiting && repeated && relative,"high-speed and moving-player regression checks");
            level.Free();
            await ToSignal(GetTree().CreateTimer(0.15),SceneTreeTimer.SignalName.Timeout);
            GD.Print("SHOOTER SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { GD.PushError(ex.ToString()); GetTree().Paused=false; GetTree().Quit(1); }
    }
}
