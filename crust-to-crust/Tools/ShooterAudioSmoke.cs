using Godot;
using System;

public partial class ShooterAudioSmoke : Node
{
    private int _failures;
    private void Check(bool ok,string text) { if(ok) GD.Print("PASS: "+text); else { _failures++; GD.PushError(text); } }
    private async System.Threading.Tasks.Task Wait(float seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private static int Count(Node cue,string property)=>(int)cue.GetType().GetProperty(property).GetValue(cue);
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            level.SetProcess(false); level.SetPhysicsProcess(false); level.IsGameActive=true;
            level.Runner.SetProcess(false); level.Runner.SetPhysicsProcess(false); level.Runner.Position=new Vector2(0,-200);
            level.Runner.GetNode<Area2D>("Hitbox").Monitoring=false;
            var shooter=new WallShooter(); level.AddChild(shooter); shooter.SetPhysicsProcess(false);
            var cue=shooter.GetNodeOrNull<Node2D>("ShooterAudio");
            if(cue==null) throw new Exception("Sentry needs positional warning and firing audio");
            foreach(float speed in new[] {340f,980f,2200f,5000f})
            foreach(int side in new[] {-1,1})
            {
                level.FallSpeed=speed;
                shooter.Activate(level,0,side,-200+WallShooter.PreferredFireGap+speed); shooter.SetPhysicsProcess(false);
                float warningAt=-1, shotAt=-1;
                for(int tick=0;tick<300;tick++)
                {
                    shooter._PhysicsProcess(0.01);
                    if(Count(cue,"WarningCount")>0 && warningAt<0) warningAt=tick*.01f;
                    if(Count(cue,"FireCount")>0) { shotAt=tick*.01f; break; }
                }
                Check(warningAt>=0 && shotAt-warningAt>=0.45f,$"audible warning precedes successful shot at speed {speed}, side {side}");
                Check(Count(cue,"WarningCount")==1 && Count(cue,"FireCount")==1,"one warning and one shot for this appearance");
                Check(Mathf.Sign(cue.GlobalPosition.X)==side,"audio source follows the actual wall");
                shooter.Deactivate();
                Check(!cue.GetNode<AudioStreamPlayer2D>("Charge").Playing && !cue.GetNode<AudioStreamPlayer2D>("Fire").Playing,"retirement stops old playback");
            }
            // Fully charged but already passed: no phantom shot.
            level.FallSpeed=0; shooter.Activate(level,0,1,-200); shooter.SetPhysicsProcess(false); shooter._PhysicsProcess(1.1);
            Check(Count(cue,"FireCount")==0,"passed sentry cannot play a firing sound"); shooter.Deactivate();
            // Scene's real volley suppression must also suppress audio.
            var vent=GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>(); level.AddChild(vent);
            ((System.Collections.Generic.List<Node2D>)typeof(LevelGenerator).GetField("waterPool",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(level)).Add(vent);
            vent.Position=new Vector2(0,-200); vent.speed=0; vent.SetProcess(false);
            shooter.Activate(level,0,-1,220); shooter.SetPhysicsProcess(false); shooter._PhysicsProcess(1.1);
            Check(Count(cue,"FireCount")==0,"cooling safety suppression produces no phantom firing sound");
            vent.Hide(); vent.Monitorable=false; shooter.Deactivate();
            shooter.Activate(level,1,1,220); shooter.SetPhysicsProcess(false); shooter._PhysicsProcess(0.01);
            Check(Count(cue,"WarningCount")==1 && Count(cue,"FireCount")==0,"pool reuse resets warning and firing state");
            var second=new WallShooter(); level.AddChild(second); second.Activate(level,2,-1,220); second.SetPhysicsProcess(false); second._PhysicsProcess(.01);
            Check(Count(second.GetNode<Node2D>("ShooterAudio"),"WarningCount")==1 && Count(cue,"WarningCount")==1,"simultaneous sentries keep independent cues");
            second.Deactivate();
            var charge=cue.GetNode<AudioStreamPlayer2D>("Charge"); await Wait(.22f);
            Check(charge.GetPlaybackPosition()>0.01f,"warning playback actually advances before pause");
            GetTree().Paused=true; float position=charge.GetPlaybackPosition(); await Wait(.15f);
            Check(Mathf.Abs(charge.GetPlaybackPosition()-position)<.03f,"pause freezes warning playback"); GetTree().Paused=false;
            float original=SaveData.SfxVolume; SaveData.SfxVolume=0; AudioManager.ApplyVolumes();
            Check(AudioServer.IsBusMute(AudioServer.GetBusIndex(AudioManager.SfxBus)) && charge.Bus==AudioManager.SfxBus,"effects setting mutes positional warning");
            SaveData.SfxVolume=original; AudioManager.ApplyVolumes();
            level.IsGameActive=false; shooter._PhysicsProcess(.01);
            Check(!charge.Playing,"stopping the run stops warning audio");
        }
        catch(Exception ex) { _failures++; GD.PushError(ex.ToString()); }
        finally { GetTree().Paused=false; level?.Free(); await Wait(.15f); }
        GD.Print("SHOOTER AUDIO SMOKE: "+_failures+" failures"); GetTree().Quit(_failures==0 ? 0 : 1);
    }
}
