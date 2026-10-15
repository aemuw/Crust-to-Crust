using Godot;

/// <summary>A short, warned environmental gust; the level advances it before player physics.</summary>
public partial class CorePressureWave : Node2D
{
    public const float WarningSeconds=1.1f, PulseSeconds=.9f;
    private LevelGenerator _level;
    private HazardWarning _warning;
    private AudioStreamPlayer2D _cue;
    private float _elapsed, _strength;
    private int _direction;
    private Vector2 _source;
    public bool Active=>Visible;
    public float DriftVelocity { get; private set; }
    public int PulsesReleased { get; private set; }
    public override void _Ready()
    {
        Material=GameAssets.Unshaded; ZIndex=2;
        _warning=new HazardWarning { Name="HazardWarning" }; AddChild(_warning);
        _cue=new AudioStreamPlayer2D { Name="PressureCue",Stream=GD.Load<AudioStream>("res://Audio/SFX/core_pressure.wav"),Bus=AudioManager.SfxBus,VolumeDb=-12,Attenuation=.35f };
        AddChild(_cue); Deactivate();
    }
    public void Activate(LevelGenerator level,int direction)
    {
        _level=level; _direction=direction<0 ? -1 : 1; _elapsed=0;
        DriftVelocity=0; PulsesReleased=0;
        _strength=Mathf.Min(75,Mathf.Max(0,level.Runner.HorizontalSpeed*.2f));
        Show(); Align(); _warning.Hide();
        Vector2 listener=GetViewport().GetCanvasTransform().AffineInverse()*GetViewport().GetVisibleRect().GetCenter();
        _cue.MaxDistance=Mathf.Max(1400,ToGlobal(_source).DistanceTo(listener)*1.7f);
        _cue.Play(); UpdateCue();
    }
    public void Deactivate() { Hide(); DriftVelocity=0; _warning?.Hide(); _cue?.Stop(); }
    private void Align()
    {
        float y=_level.Runner.FixedY+150;
        _level.GetRockBounds(y,out float left,out float right);
        _source=new Vector2(_direction>0 ? left : right,y); _cue.Position=_source;
    }
    private float Integral(float time)
    {
        float t=Mathf.Clamp(time-WarningSeconds,0,PulseSeconds);
        return _strength*PulseSeconds/Mathf.Pi*(1-Mathf.Cos(Mathf.Pi*t/PulseSeconds));
    }
    public void Advance(float dt)
    {
        DriftVelocity=0;
        if(!Active || dt<=0 || !float.IsFinite(dt)) return;
        if(!_level.CanContinueCorePressure()) { Deactivate(); return; }
        float previous=_elapsed; _elapsed+=dt; Align();
        if(previous<WarningSeconds && _elapsed>=WarningSeconds) PulsesReleased=1;
        // Exact impulse integral: the same displacement at different physics tick rates.
        DriftVelocity=_direction*(Integral(_elapsed)-Integral(previous))/dt;
        if(_elapsed>=WarningSeconds+PulseSeconds+.45f) { Deactivate(); return; }
        UpdateCue();
    }
    private void UpdateCue()
    {
        _warning.SetCue(Active && _elapsed<WarningSeconds,ToGlobal(_source),ToGlobal(_source+Vector2.Right*_direction*100),_elapsed/WarningSeconds,new Color("d1b698"),pressure:true);
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(_level==null) return;
        float t=Mathf.Clamp((_elapsed-WarningSeconds)/PulseSeconds,0,1);
        float pulse=Mathf.Sin(Mathf.Pi*t);
        if(pulse<=0) return;
        Color dust=new(.70f,.56f,.42f,pulse*.24f);
        for(int i=0;i<17;i++)
        {
            float y=_level.Runner.FixedY-190+(i%7)*63;
            _level.GetRockBounds(y,out float left,out float right);
            float u=Mathf.PosMod(i*.173f+t*.8f,1);
            float x=Mathf.Lerp(left+18,right-18,_direction>0 ? u : 1-u);
            float length=Mathf.Min(12+i%4*5,_direction>0 ? right-x-8 : x-left-8);
            DrawLine(new Vector2(x,y),new Vector2(x+_direction*length,y-3),dust,1.4f,true);
        }
        for(int i=0;i<3;i++)
        {
            Vector2[] arc=new Vector2[9];
            for(int k=0;k<arc.Length;k++)
            {
                float y=_level.Runner.FixedY-220+k*58;
                _level.GetRockBounds(y,out float left,out float right);
                float u=Mathf.Clamp(t+(i-1)*.12f+Mathf.Sin(k*Mathf.Pi/8)*.09f,.02f,.98f);
                arc[k]=new Vector2(Mathf.Lerp(left+12,right-12,_direction>0 ? u : 1-u),y);
            }
            DrawPolyline(arc,new Color(.56f,.49f,.40f,pulse*.12f),2,true);
        }
    }
}
