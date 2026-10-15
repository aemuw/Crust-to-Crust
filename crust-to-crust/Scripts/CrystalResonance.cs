using Godot;

/// <summary>A single wall-grown crystal warns, splits, then leaves a quiet spent seam.</summary>
public partial class CrystalResonance : Node2D
{
    private LevelGenerator _level;
    private HazardWarning _warning;
    private AudioStreamPlayer2D _ring;
    private int _side;
    private float _clock, _after;
    private bool _started, _released;
    public int FragmentsReleased { get; private set; }
    public bool Active => Visible;
    public override void _Ready()
    {
        Material=GameAssets.Unshaded; ZIndex=2;
        _warning=new HazardWarning { Name="HazardWarning" }; AddChild(_warning);
        _ring=new AudioStreamPlayer2D { Name="CrystalRing",Stream=GD.Load<AudioStream>("res://Audio/SFX/crystal_resonance.wav"),Bus=AudioManager.SfxBus,VolumeDb=-9,Attenuation=.35f };
        AddChild(_ring); Deactivate();
    }
    public void Activate(LevelGenerator level,int side,float y)
    {
        _level=level; _side=side<0 ? -1 : 1; Position=new Vector2(0,y);
        _clock=_after=0; _started=_released=false; FragmentsReleased=0;
        _ring.Stop(); _warning.Hide(); Show(); ProcessMode=ProcessModeEnum.Inherit; Align(); QueueRedraw();
    }
    private void Align()
    {
        _level.GetRockBounds(Position.Y,out float left,out float right);
        Position=new Vector2((_side<0 ? left : right)+_side*22,Position.Y);
    }
    public void Deactivate() { Hide(); _warning?.Hide(); _ring?.Stop(); ProcessMode=ProcessModeEnum.Disabled; }
    public override void _Process(double delta)
    {
        float remaining=Mathf.Max(0,(float)delta);
        while(remaining>0 && Active)
        {
            float dt=Mathf.Min(.01f,remaining); remaining-=dt;
            if(_level?.IsGameActive!=true) { Deactivate(); return; }
            Position-=new Vector2(0,_level.FallSpeed*dt); Align();
            float gap=Position.Y-(_level.Runner?.FixedY??-200);
            if(!_started && gap<=_level.FallSpeed*2.3f)
            {
                _started=true;
                Vector2 listener=GetViewport().GetCanvasTransform().AffineInverse()*GetViewport().GetVisibleRect().GetCenter();
                _ring.MaxDistance=Mathf.Max(1400,GlobalPosition.DistanceTo(listener)*1.7f); _ring.Play();
            }
            else if(_started && !_released)
            {
                _clock+=dt;
                if(_clock>=1.25f)
                {
                    _released=true;
                    if(gap>_level.FallSpeed*.5f && _level.TryReleaseCrystalFragment(_side,Position.Y)) FragmentsReleased=1;
                }
            }
            if(_released) _after+=dt;
            _warning.SetCue(_started && !_released,GlobalPosition,GlobalPosition,_clock/1.25f,new Color("91d8d7"),collapse:true);
            if(CaveViewport.PassedTop(this,140) || _after>2.5f) Deactivate();
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        float brightness=_started && !_released ? .65f+.18f*Mathf.Clamp(_clock/1.25f,0,1)*SaveData.FlashIntensity : .55f;
        CaveArt.Wall(this,CaveArt.Crystal,Vector2.Zero,_side,new Rect2(-35,-105,75,210),new Color(brightness,brightness,brightness,1));
        if(!_released) return;
        for(int i=0;i<10;i++)
        {
            float t=Mathf.Min(_after,1.5f);
            Vector2 p=new(-_side*(30+t*(20+i*3)),i*7-35+t*t*38);
            DrawLine(p,p+new Vector2(-_side*3,5),new Color(.55f,.82f,.84f,Mathf.Max(0,.6f-t*.4f)),1.5f,true);
        }
    }
}
