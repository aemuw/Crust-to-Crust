using Godot;
using System.Collections.Generic;

public enum CrystalChamberPhase { Dormant, Warning, Passages, Recovery }

/// <summary>Three wall-rooted resonant strata; released shards use the protected obstacle pool.</summary>
public partial class CrystalChamberEvent : Node2D
{
    public const float FirstWarning=1.4f, WaveInterval=1.25f, LeadSeconds=1.8f, RecoverySeconds=1.6f;
    private LevelGenerator _level;
    private HazardWarning _leftCue, _rightCue;
    private AudioStreamPlayer2D _resonance, _fracture;
    private readonly List<Obstacle> _debris=new(8);
    private readonly float[] _strata=new float[3];
    private float _nextRelease, _recovery;
    public CrystalChamberPhase Phase { get; private set; }
    public bool Active=>Phase!=CrystalChamberPhase.Dormant;
    public float Elapsed { get; private set; }
    public int WavesReleased { get; private set; }
    public int DebrisReleased { get; private set; }
    public override void _Ready()
    {
        Material=GameAssets.Unshaded; ZIndex=2;
        _leftCue=new HazardWarning { Name="LeftWarning" };AddChild(_leftCue);
        _rightCue=new HazardWarning { Name="RightWarning" };AddChild(_rightCue);
        _resonance=new AudioStreamPlayer2D { Name="ChamberResonance",Stream=GD.Load<AudioStream>("res://Audio/SFX/crystal_chamber_resonance.wav"),Bus=AudioManager.SfxBus,VolumeDb=-10,Attenuation=.3f };
        _fracture=new AudioStreamPlayer2D { Name="ChamberFracture",Stream=GD.Load<AudioStream>("res://Audio/SFX/crystal_chamber_fracture.wav"),Bus=AudioManager.SfxBus,VolumeDb=-11,Attenuation=.3f };
        AddChild(_resonance);AddChild(_fracture);Deactivate();
    }
    public void Activate(LevelGenerator level)
    {
        _level=level;_debris.Clear();Elapsed=_recovery=0;WavesReleased=DebrisReleased=0;_nextRelease=FirstWarning;
        for(int i=0;i<3;i++) _strata[i]=level.Runner.FixedY+level.FallSpeed*(FirstWarning+LeadSeconds+i*WaveInterval);
        Phase=CrystalChamberPhase.Warning;Show();ProcessMode=ProcessModeEnum.Inherit;Warn();UpdateCues();
    }
    private void PositionAudio(AudioStreamPlayer2D audio,float y,int side)
    {
        _level.GetRockBounds(y,out float left,out float right);
        audio.Position=new Vector2(side<0 ? left : side>0 ? right : (left+right)*.5f,y);
        Vector2 listener=GetViewport().GetCanvasTransform().AffineInverse()*GetViewport().GetVisibleRect().GetCenter();
        audio.MaxDistance=Mathf.Max(1800,ToGlobal(audio.Position).DistanceTo(listener)*1.7f);
    }
    private void Warn()
    {
        if(WavesReleased>=3) return;
        PositionAudio(_resonance,_strata[WavesReleased],WavesReleased==0 ? -1 : WavesReleased==1 ? 1 : 0);_resonance.Play();
    }
    public void Deactivate()
    {
        foreach(var rock in _debris) if(GodotObject.IsInstanceValid(rock) && rock.Visible) rock.RetireForClearance();
        Finish();
    }
    private void Finish()
    {
        Phase=CrystalChamberPhase.Dormant;Hide();_leftCue?.Hide();_rightCue?.Hide();_resonance?.Stop();_fracture?.Stop();
        _debris.Clear();ProcessMode=ProcessModeEnum.Disabled;
    }
    private void Recover(bool retire)
    {
        if(Phase==CrystalChamberPhase.Recovery) return;
        Phase=CrystalChamberPhase.Recovery;_recovery=0;_leftCue.Hide();_rightCue.Hide();_resonance.Stop();_fracture.Stop();
        if(retire) foreach(var rock in _debris) if(rock.Visible) rock.RetireForClearance();
    }
    public override void _Process(double delta)
    {
        float remaining=Mathf.Max(0,(float)delta);
        while(remaining>0 && Active)
        {
            float dt=Mathf.Min(remaining,Mathf.Min(.01f,12/Mathf.Max(1,_level.FallSpeed)));remaining-=dt;Elapsed+=dt;
            if(!_level.IsGameActive) { Deactivate();return; }
            for(int i=0;i<3;i++) _strata[i]-=_level.FallSpeed*dt;
            if(Phase==CrystalChamberPhase.Recovery)
            {
                _recovery+=dt;if(_recovery>=RecoverySeconds) Finish();continue;
            }
            int next=Mathf.Min(WavesReleased,2);
            if(!_level.CanContinueCollapseEvent(_strata[next])) { Recover(true);continue; }
            if(WavesReleased<3 && Elapsed>=_nextRelease)
            {
                // A sudden acceleration cannot turn a completed warning into an unavoidable hit.
                if(_strata[next]-_level.Runner.FixedY<_level.FallSpeed*.95f) { Recover(true);continue; }
                int count=_level.TryReleaseCrystalChamberWave(_strata[next],WavesReleased,_debris);
                if(count==0) { Recover(true);continue; }
                PositionAudio(_fracture,_strata[next],next==0 ? -1 : next==1 ? 1 : 0);_fracture.Play();
                WavesReleased++;DebrisReleased+=count;Phase=CrystalChamberPhase.Passages;_nextRelease+=WaveInterval;Warn();
            }
            // Recovery starts after the tail of the last wave actually passes the runner.
            if(WavesReleased==3 && _strata[2]<_level.Runner.FixedY-_level.FallSpeed*.45f) Recover(false);
        }
        UpdateCues();QueueRedraw();
    }
    private void UpdateCues()
    {
        if(!Active || Phase==CrystalChamberPhase.Recovery || WavesReleased==3) { _leftCue.Hide();_rightCue.Hide();return; }
        float y=_strata[WavesReleased];_level.GetRockBounds(y,out float left,out float right);
        float duration=WavesReleased==0 ? FirstWarning : WaveInterval;
        float progress=(Elapsed-_nextRelease+duration)/duration;
        Vector2 a=ToGlobal(new Vector2(left,y)),b=ToGlobal(new Vector2(right,y));
        _leftCue.SetCue(WavesReleased!=1,a,a,progress,new Color("9dbfc8"),collapse:true);
        _rightCue.SetCue(WavesReleased!=0,b,b,progress,new Color("9dbfc8"),collapse:true);
    }
    public override void _Draw()
    {
        if(!Active) return;
        float recoveryFade=Phase==CrystalChamberPhase.Recovery ? Mathf.Clamp(1-_recovery/1.1f,0,1) : 1;
        for(int row=0;row<3;row++) for(int side=-1;side<=1;side+=2)
        {
            float y=_strata[row];
            float pulse=row==WavesReleased && (row==2 || side==(row==0 ? -1 : 1)) ? Mathf.Pow(.5f+.5f*Mathf.Sin(Elapsed*7),2)*.12f*SaveData.FlashIntensity : 0;
            float brightness=(row<WavesReleased ? .43f : .58f)+pulse;
            // Short slices hug the real wall rather than leaving the middle of a tall prop floating.
            for(int slice=0;slice<CaveArt.CavitySlices.Length;slice++)
            {
                float top=-125+slice*25;
                _level.GetRockBounds(y+top+12.5f,out float left,out float right);
                CaveArt.Wall(this,CaveArt.CavitySlices[slice],new Vector2(side<0 ? left : right,y),side,
                    new Rect2(-63,top,90,25.2f),new Color(brightness,brightness,brightness,recoveryFade));
            }
            if(row>=WavesReleased) continue;
            _level.GetRockBounds(y,out float dustLeft,out float dustRight);
            for(int i=0;i<12;i++)
            {
                float age=Mathf.PosMod(Elapsed+i*.073f,1.1f);
                DrawCircle(new Vector2((side<0 ? dustLeft : dustRight)-side*(10+age*45),y-60+i*10+age*age*80),.8f+i%3*.4f,
                    new Color(.47f,.62f,.67f,(1-age/1.1f)*.25f*recoveryFade));
            }
        }
    }
}
