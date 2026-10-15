using Godot;
using System.Collections.Generic;

public enum CoreClimaxPhase { Dormant, Opening, Passages, Recovery }

/// <summary>A warned survival section across the core, without replacing falling controls.</summary>
public partial class CoreClimaxEvent : Node2D
{
    public const float FirstWarning=1.2f, Interval=1.6f, Lead=1.5f;
    private LevelGenerator _level;
    private HazardWarning _warning;
    private AudioStreamPlayer2D _opening;
    private Sprite2D _cavity;
    private readonly List<Obstacle> _debris=new(3);
    private float _sourceY,_lastY,_recovery;
    public CoreClimaxPhase Phase { get; private set; }
    public bool Active=>Phase!=CoreClimaxPhase.Dormant;
    public bool ChoiceActive=>Active && Phase!=CoreClimaxPhase.Recovery && Elapsed<FirstWarning+Lead+.4f;
    public float Elapsed { get; private set; }
    public int WavesReleased { get; private set; }
    public override void _Ready()
    {
        ZIndex=2;Material=GameAssets.Unshaded;
        _warning=new HazardWarning { Name="CoreWarning" };AddChild(_warning);
        _cavity=new Sprite2D { Name="CoreCavity",Texture=GameAssets.Core,ZIndex=-8,Material=GameAssets.Unshaded };AddChild(_cavity);
        _opening=new AudioStreamPlayer2D { Name="CoreOpening",Stream=GD.Load<AudioStream>("res://Audio/SFX/core_opening.wav"),Bus=AudioManager.SfxBus,VolumeDb=-12,Attenuation=.25f,MaxDistance=2200 };AddChild(_opening);
        Deactivate();
    }
    public void Activate(LevelGenerator level)
    {
        _level=level;Elapsed=_recovery=0;WavesReleased=0;_debris.Clear();
        _sourceY=level.Runner.FixedY+level.FallSpeed*(FirstWarning+Lead);
        Phase=CoreClimaxPhase.Opening;Show();ProcessMode=ProcessModeEnum.Inherit;
        _opening.Position=new Vector2(0,level.Runner.FixedY+100);_opening.Play();UpdateVisuals();
    }
    public void Deactivate()
    {
        foreach(var rock in _debris) if(GodotObject.IsInstanceValid(rock) && rock.Visible) rock.RetireForClearance();
        Phase=CoreClimaxPhase.Dormant;Hide();_warning?.Hide();_opening?.Stop();_level?.SetClimaxMix(0);_debris.Clear();ProcessMode=ProcessModeEnum.Disabled;
    }
    private void Recover(bool retire)
    {
        if(Phase==CoreClimaxPhase.Recovery) return;
        Phase=CoreClimaxPhase.Recovery;_recovery=0;_warning.Hide();_opening.Stop();_level.EndClimaxPressure();
        if(retire) foreach(var rock in _debris) if(rock.Visible) rock.RetireForClearance();
    }
    public override void _Process(double delta)
    {
        float remaining=Mathf.Max(0,(float)delta);
        while(remaining>0 && Active)
        {
            float dt=Mathf.Min(remaining,Mathf.Min(.01f,12/Mathf.Max(1,_level.FallSpeed)));remaining-=dt;Elapsed+=dt;
            if(!_level.IsGameActive) { Deactivate();return; }
            _sourceY-=_level.FallSpeed*dt;_lastY-=_level.FallSpeed*dt;
            if(Phase==CoreClimaxPhase.Recovery)
            {
                _recovery+=dt;if(_recovery>=1.6f) { Deactivate();return; }continue;
            }
            if(!_level.CanContinueCoreClimax(_sourceY)) { Recover(true);continue; }
            if(WavesReleased<2 && Elapsed>=FirstWarning+WavesReleased*Interval)
            {
                if(_sourceY-_level.Runner.FixedY<_level.FallSpeed*.85f) { Recover(true);continue; }
                int count=_level.TryReleaseCoreClimaxWave(_sourceY,WavesReleased,_debris);
                if(count!=(WavesReleased==0 ? 1 : 2)) { Recover(true);continue; }
                _lastY=_sourceY;WavesReleased++;Phase=CoreClimaxPhase.Passages;
                if(WavesReleased==1) _sourceY=_level.Runner.FixedY+_level.FallSpeed*(Interval+Lead);
                else _level.StartClimaxPressure();
            }
            if(WavesReleased==2 && _lastY<_level.Runner.FixedY-_level.FallSpeed*.45f) Recover(false);
        }
        UpdateVisuals();
    }
    private void UpdateVisuals()
    {
        float open=Mathf.SmoothStep(0,1,Mathf.Clamp(Elapsed/FirstWarning,0,1));
        float fade=Phase==CoreClimaxPhase.Recovery ? Mathf.Clamp(1-_recovery/1.6f,0,1) : 1;
        _level.SetClimaxMix(open*fade);
        _cavity.Position=new Vector2(0,_level.Runner.FixedY+170);
        _cavity.Scale=new Vector2(960f/_cavity.Texture.GetWidth(),1500f/_cavity.Texture.GetHeight());
        _cavity.Modulate=new Color(.34f,.26f,.22f,open*fade*.8f);
        bool warn=Active && Phase!=CoreClimaxPhase.Recovery && WavesReleased<2;
        Vector2 source=ToGlobal(new Vector2(0,_sourceY));
        _warning.SetCue(warn,source,source,(Elapsed-WavesReleased*Interval)/FirstWarning,new Color("d6ab77"),collapse:true);
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(!Active) return;
        float open=Mathf.SmoothStep(0,1,Mathf.Clamp(Elapsed/FirstWarning,0,1));
        float fade=Phase==CoreClimaxPhase.Recovery ? Mathf.Clamp(1-_recovery/1.6f,0,1) : 1;
        // Existing textured basalt lips retreat into the wall as the hot chamber is revealed.
        for(int side=-1;side<=1;side+=2) for(int i=0;i<5;i++)
        {
            float y=_level.Runner.FixedY-300+i*175;
            _level.GetRockBounds(y,out float left,out float right);
            Vector2 root=new((side<0 ? left : right)+side*open*24,y);
            CaveArt.Wall(this,CaveArt.Magma,root,side,new Rect2(-60,-105,85,210),new Color(.6f,.52f,.45f,fade));
        }
    }
}
