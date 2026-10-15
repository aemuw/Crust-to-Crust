using Godot;
using System.Collections.Generic;

public enum CollapsePhase { Dormant, Warning, Passages, Recovery }

/// <summary>Three protected rubble gates, with preparation and a guaranteed recovery.</summary>
public partial class CaveCollapseEvent : Node2D
{
    public const float FirstWarning=1.35f, WaveInterval=1.15f, LeadSeconds=2f, RecoverySeconds=1.6f;
    private LevelGenerator _level;
    private HazardWarning _leftCue, _rightCue;
    private AudioStreamPlayer2D _rumble, _crack;
    private readonly List<Obstacle> _debris=new(6);
    private float _sourceY, _nextRelease, _lastRelease, _recovery;
    public CollapsePhase Phase { get; private set; }
    public bool Active=>Phase!=CollapsePhase.Dormant;
    public float Elapsed { get; private set; }
    public int WavesReleased { get; private set; }
    public int DebrisReleased { get; private set; }
    public override void _Ready()
    {
        Material=GameAssets.Unshaded; ZIndex=2;
        _leftCue=new HazardWarning { Name="LeftWarning" }; AddChild(_leftCue);
        _rightCue=new HazardWarning { Name="RightWarning" }; AddChild(_rightCue);
        _rumble=new AudioStreamPlayer2D { Name="CollapseRumble",Stream=GD.Load<AudioStream>("res://Audio/SFX/collapse_build.wav"),Bus=AudioManager.SfxBus,VolumeDb=-10,Attenuation=.3f };
        _crack=new AudioStreamPlayer2D { Name="CollapseCrack",Stream=GD.Load<AudioStream>("res://Audio/SFX/basalt_crack.wav"),Bus=AudioManager.SfxBus,VolumeDb=-8,Attenuation=.3f };
        AddChild(_rumble); AddChild(_crack); Deactivate();
    }
    public void Activate(LevelGenerator level)
    {
        _level=level; _debris.Clear(); Elapsed=_recovery=0; WavesReleased=DebrisReleased=0;
        _nextRelease=FirstWarning; _lastRelease=0;
        _sourceY=level.Runner.FixedY+level.FallSpeed*(FirstWarning+LeadSeconds);
        Phase=CollapsePhase.Warning; Show(); ProcessMode=ProcessModeEnum.Inherit;
        _rumble.Position=new Vector2(0,level.Runner.FixedY+130); _rumble.MaxDistance=1800; _rumble.Play();
        UpdateCues();
    }
    public void Deactivate()
    {
        foreach(var rock in _debris) if(GodotObject.IsInstanceValid(rock) && rock.Visible) rock.RetireForClearance();
        Finish();
    }
    private void Finish()
    {
        Phase=CollapsePhase.Dormant; Hide(); _leftCue?.Hide(); _rightCue?.Hide();
        _rumble?.Stop(); _crack?.Stop(); _debris.Clear(); ProcessMode=ProcessModeEnum.Disabled;
    }
    private void BeginRecovery(bool retire)
    {
        if(Phase==CollapsePhase.Recovery) return;
        Phase=CollapsePhase.Recovery; _recovery=0; _leftCue.Hide(); _rightCue.Hide();
        _rumble.Stop(); _crack.Stop();
        if(retire) foreach(var rock in _debris) if(rock.Visible) rock.RetireForClearance();
    }
    public override void _Process(double delta)
    {
        float remaining=Mathf.Max(0,(float)delta);
        while(remaining>0 && Active)
        {
            float dt=Mathf.Min(.01f,remaining); remaining-=dt; Elapsed+=dt;
            if(!_level.IsGameActive) { Deactivate(); return; }
            _sourceY-=_level.FallSpeed*dt;
            if(Phase==CollapsePhase.Recovery)
            {
                _recovery+=dt; if(_recovery>=RecoverySeconds) Finish();
                continue;
            }
            if(!_level.CanContinueCollapseEvent(_sourceY)) { BeginRecovery(true); continue; }
            if(WavesReleased<3 && Elapsed>=_nextRelease)
            {
                int count=_level.TryReleaseCollapseGate(_sourceY,_debris);
                if(count==0) { BeginRecovery(true); continue; }
                WavesReleased++; DebrisReleased+=count; Phase=CollapsePhase.Passages; _lastRelease=Elapsed;
                _level.GetRockBounds(_sourceY,out float left,out float right);
                _crack.Position=new Vector2(WavesReleased%2==0 ? right : left,_sourceY);
                Vector2 listener=GetViewport().GetCanvasTransform().AffineInverse()*GetViewport().GetVisibleRect().GetCenter();
                _crack.MaxDistance=Mathf.Max(1800,ToGlobal(_crack.Position).DistanceTo(listener)*1.7f); _crack.Play();
                _nextRelease+=WaveInterval;
                if(WavesReleased<3) _sourceY=_level.Runner.FixedY+_level.FallSpeed*(WaveInterval+LeadSeconds);
            }
            if(WavesReleased==3 && Elapsed-_lastRelease>=LeadSeconds+.5f) BeginRecovery(false);
        }
        UpdateCues(); QueueRedraw();
    }
    private void UpdateCues()
    {
        if(!Active || Phase==CollapsePhase.Recovery || WavesReleased==3) { _leftCue.Hide(); _rightCue.Hide(); return; }
        _level.GetRockBounds(_sourceY,out float left,out float right);
        float duration=WavesReleased==0 ? FirstWarning : WaveInterval;
        float progress=(Elapsed-_nextRelease+duration)/duration;
        Vector2 a=ToGlobal(new Vector2(left,_sourceY)),b=ToGlobal(new Vector2(right,_sourceY));
        _leftCue.SetCue(true,a,a,progress,new Color("cbb08b"),collapse:true);
        _rightCue.SetCue(true,b,b,progress,new Color("cbb08b"),collapse:true);
    }
    public override void _Draw()
    {
        if(!Active) return;
        float fade=Phase==CollapsePhase.Recovery ? Mathf.Clamp(1-_recovery/.7f,0,1) : 1;
        _level.GetRockBounds(_sourceY,out float left,out float right);
        foreach(int side in new[] {-1,1})
        {
            Vector2 root=new(side<0 ? left : right,_sourceY);
            for(int i=0;i<4;i++)
            {
                Vector2 p=root+new Vector2(side*(8+i%2*5),-70+i*38);
                DrawPolyline(new[] {p,p+new Vector2(side*15,13),p+new Vector2(side*6,28)},new Color(.22f,.20f,.17f,.7f*fade),2,true);
            }
            for(int i=0;i<32;i++)
            {
                float age=Mathf.PosMod(Elapsed+i*.037f,1.1f);
                Vector2 p=root+new Vector2(-side*(5+age*(15+i%6*6)),-100+i%7*27+age*age*95);
                DrawCircle(p,1+i%3*.6f,new Color(.55f,.48f,.37f,(1-age/1.1f)*.28f*fade));
            }
        }
    }
}
