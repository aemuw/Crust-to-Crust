using Godot;

public partial class BasaltCollapse : Node2D
{
    private LevelGenerator _level;
    private HazardWarning _warning;
    private AudioStreamPlayer2D _crack;
    private int _side;
    private float _warningTime, _dustTime;
    private bool _started, _released;
    public int FragmentsReleased { get; private set; }
    public bool Active => Visible;
    public override void _Ready()
    {
        Material=GameAssets.Unshaded; ZIndex=2;
        _warning=new HazardWarning { Name="HazardWarning" }; AddChild(_warning);
        _crack=new AudioStreamPlayer2D { Name="RockCrack",Stream=GD.Load<AudioStream>("res://Audio/SFX/basalt_crack.wav"),Bus=AudioManager.SfxBus,VolumeDb=-8,Attenuation=.35f }; AddChild(_crack); Deactivate();
    }
    public void Activate(LevelGenerator level,int side,float y)
    {
        _level=level; _side=side<0 ? -1 : 1; Position=new Vector2(0,y);
        _warningTime=_dustTime=0; _started=_released=false; FragmentsReleased=0;
        _crack.Stop(); _warning.Hide(); Show(); ProcessMode=ProcessModeEnum.Inherit; Align();
    }
    private void Align()
    {
        _level.GetRockBounds(Position.Y,out float left,out float right);
        Position=new Vector2((_side<0 ? left : right)+_side*32,Position.Y);
    }
    public void Deactivate() { Hide(); _warning?.Hide(); _crack?.Stop(); ProcessMode=ProcessModeEnum.Disabled; }
    public override void _Process(double delta)
    {
        float remaining=Mathf.Max(0,(float)delta);
        while(remaining>0 && Active)
        {
            float dt=Mathf.Min(.01f,remaining); remaining-=dt;
            if(_level?.IsGameActive!=true) { Deactivate(); return; }
            Position-=new Vector2(0,_level.FallSpeed*dt); Align();
            float gap=Position.Y-(_level.Runner?.FixedY??-200);
            if(!_started && gap<=_level.FallSpeed*2.1f)
            {
                _started=true;
                Vector2 listener=GetViewport().GetCanvasTransform().AffineInverse()*GetViewport().GetVisibleRect().GetCenter();
                _crack.MaxDistance=Mathf.Max(1400,GlobalPosition.DistanceTo(listener)*1.7f); _crack.Play();
            }
            else if(_started && !_released)
            {
                _warningTime+=dt;
                if(_warningTime>=1.1f)
                {
                    _released=true;
                    if(gap>_level.FallSpeed*.5f && _level.TryReleaseBasaltFragment(_side,Position.Y)) FragmentsReleased=1;
                }
            }
            if(_released) _dustTime+=dt;
            _warning.SetCue(_started && !_released,GlobalPosition,GlobalPosition,_warningTime/1.1f,new Color("e5bd80"),collapse:true);
            if(CaveViewport.PassedTop(this,120) || _dustTime>2.5f) Deactivate();
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(!_started) return;
        float fade=_released ? Mathf.Clamp(1-_dustTime/2.5f,0,1) : 1;
        CaveArt.Wall(this,CaveArt.Basalt,Vector2.Zero,_side,new Rect2(-30,-100,60,200),new Color(.65f,.65f,.65f,fade));
        float clock=_released ? 1.1f+_dustTime : _warningTime;
        for(int i=0;i<18;i++)
        {
            float age=Mathf.PosMod(clock+i*.061f,.85f);
            Vector2 p=new(-_side*(10+age*(22+i%5*5)),-25+i%4*17+age*85);
            DrawCircle(p,1+i%3*.55f,new Color(.55f,.49f,.39f,(1-age/.85f)*.32f*fade));
        }
    }
}
