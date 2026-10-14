using Godot;

/// <summary>One positional charge cue and one successful-shot cue per pooled appearance.</summary>
public partial class ShooterAudioCue : Node2D
{
    private AudioStreamPlayer2D _charge, _fire;
    private bool _active;
    public int WarningCount { get; private set; }
    public int FireCount { get; private set; }
    public override void _Ready()
    {
        _charge=Player("Charge","res://Audio/SFX/sentry_charge.wav",-5f);
        _fire=Player("Fire","res://Audio/SFX/sentry_fire.wav",-7f);
    }
    private AudioStreamPlayer2D Player(string name,string path,float volume)
    {
        var player=new AudioStreamPlayer2D { Name=name,Stream=GD.Load<AudioStream>(path),Bus=AudioManager.SfxBus,
            VolumeDb=volume,MaxDistance=2400,Attenuation=0.35f,PanningStrength=1.4f };
        AddChild(player); return player;
    }
    public void Reset(int variant)
    {
        Stop(); _active=true; WarningCount=FireCount=0;
        float pitch=variant==1 ? 0.85f : variant==2 ? 1.15f : 1f;
        _charge.PitchScale=pitch; _fire.PitchScale=pitch;
    }
    public void Warn(Vector2 worldPosition)
    {
        if(!_active || WarningCount!=0) return;
        GlobalPosition=worldPosition;
        Vector2 listener=GetViewport().GetCanvasTransform().AffineInverse()*GetViewport().GetVisibleRect().GetCenter();
        // Later traversals can exceed the initial speed limit. Keep the early cue
        // within audible range even while the mount is still below the viewport.
        _charge.MaxDistance=Mathf.Max(2400,listener.DistanceTo(worldPosition)*1.7f);
        WarningCount++; _charge.Play();
    }
    public void Fire(Vector2 worldPosition)
    {
        if(!_active || FireCount!=0) return;
        GlobalPosition=worldPosition; FireCount++; _charge.Stop(); _fire.Play();
    }
    public void Stop()
    { _active=false; _charge?.Stop(); _fire?.Stop(); }
    public override void _ExitTree()
    { Stop(); if(_charge!=null) _charge.Stream=null; if(_fire!=null) _fire.Stream=null; }
}
