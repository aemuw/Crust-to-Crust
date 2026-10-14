using Godot;
using System;

public partial class CoreAudioSmoke : Node
{
    private async System.Threading.Tasks.Task Wait(float seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            level.SetProcess(false); level.SetPhysicsProcess(false);
            level.Runner.SetProcess(false); level.Runner.SetPhysicsProcess(false);
            level.Runner.GetNode<Area2D>("Hitbox").Monitoring=false;
            var audio=level.GetNode<AudioManager>("AudioManager");
            var hum=audio.GetNodeOrNull<AudioStreamPlayer>("CoreRumble");
            Check(hum!=null,"core has a dedicated rumble layer");
            var accent=audio.GetNode<AudioStreamPlayer>("CoreAccent");
            Check(accent.Stream.ResourcePath.EndsWith("SFX/core_crossing.wav") && accent.PitchScale==1,"crossing uses its own sound rather than the coin");
            Check(!hum.Playing,"core rumble is silent in menu");
            Check(hum.Stream is AudioStreamWav wav && wav.LoopMode==AudioStreamWav.LoopModeEnum.Forward,"core rumble loops");
            level.StartGame(); level.IsGameActive=true; level.FallSpeed=500;
            level.CurrentDepth=level.CoreDepthPoint-8000; level._Process(.01); await Wait(.1f);
            Check(!hum.Playing,"cold layers do not play core rumble");
            level.CurrentDepth=level.CoreDepthPoint-1000; level._Process(.01); await Wait(.5f);
            Check(hum.Playing && hum.VolumeDb>-35,"approaching the core fades in its rumble");
            Check(audio.GetNode<AudioStreamPlayer>("GameScore").PitchScale==1,"rumble does not change the score tempo");
            level.CurrentDepth=level.CoreDepthPoint-.1f; level._Process(.001); await Wait(.22f);
            Check(accent.Playing,"real core crossing plays the accent");
            float cursor=accent.GetPlaybackPosition(); level._Process(.01); await Wait(.15f);
            Check(accent.GetPlaybackPosition()>cursor+.03f,"remaining near the core does not restart the accent");
            GetTree().Paused=true; float humCursor=hum.GetPlaybackPosition(), accentCursor=accent.GetPlaybackPosition(); await Wait(.15f);
            Check(Mathf.Abs(hum.GetPlaybackPosition()-humCursor)<.03f && Mathf.Abs(accent.GetPlaybackPosition()-accentCursor)<.03f,"pause freezes core effects");
            GetTree().Paused=false;
            float saved=SaveData.SfxVolume; SaveData.SfxVolume=0; AudioManager.ApplyVolumes();
            bool muted=AudioServer.IsBusMute(AudioServer.GetBusIndex(AudioManager.SfxBus)); SaveData.SfxVolume=saved; AudioManager.ApplyVolumes();
            Check(muted && hum.Bus==AudioManager.SfxBus && accent.Bus==AudioManager.SfxBus,"effects setting controls both core sounds");
            level.CurrentDepth=level.CoreDepthPoint+8000; level._Process(.01); await Wait(1.4f);
            Check(!hum.Playing,"rumble fades away after leaving the core region");
            level.CurrentDepth=level.CoreDepthPoint+1000; level._Process(.01); await Wait(.1f); level.StopFalling();
            Check(!hum.Playing && !accent.Playing,"stopping the world stops core effects");
            level.Free(); level=null; await Wait(.2f); GD.Print("CORE AUDIO SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { GetTree().Paused=false; GD.PushError(ex.ToString()); level?.Free(); GetTree().Quit(1); }
    }
}
