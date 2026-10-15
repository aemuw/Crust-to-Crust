using Godot;

/// <summary>One natural wall fissure: warning, short tapered discharge, then spent rock.</summary>
public partial class MagmaVent : Node2D
{
    public const float WarningSeconds=1.1f;
    private LevelGenerator _level;
    private HazardWarning _warning;
    private AudioStreamPlayer2D _hiss;
    private ShaderMaterial _gasMaterial;
    private ColorRect _gas;
    private PointLight2D _light;
    private int _side;
    private bool _started, _released, _cancelled;
    private float _warningClock, _burstClock, _flow, _clock;
    private readonly Vector2[] _polygon=new Vector2[4];
    public int BurstsReleased { get; private set; }
    public bool Active=>Visible;
    public bool IsEmitting=>Active && _flow>0;
    public float Reach { get; private set; }
    public Vector2[] PlumePolygon=>_polygon;

    public override void _Ready()
    {
        Material=GameAssets.Unshaded; ZIndex=2;
        _warning=new HazardWarning { Name="HazardWarning" }; AddChild(_warning);
        _gasMaterial=new ShaderMaterial { Shader=GD.Load<Shader>("res://Shaders/MagmaGas.gdshader") };
        _gas=new ColorRect { Name="HotGas",MouseFilter=Control.MouseFilterEnum.Ignore,Material=_gasMaterial };
        AddChild(_gas);
        _light=CaveLighting.Light(new Color("ed7b36"),.25f,.1f,false); AddChild(_light);
        _hiss=new AudioStreamPlayer2D { Name="VentHiss",Stream=GD.Load<AudioStream>("res://Audio/SFX/magma_vent.wav"),Bus=AudioManager.SfxBus,VolumeDb=-12,Attenuation=.35f };
        AddChild(_hiss); Deactivate();
    }
    public void Activate(LevelGenerator level,int side,float y)
    {
        _level=level; _side=side<0 ? -1 : 1; Position=new Vector2(0,y);
        _started=_released=_cancelled=false; _clock=_warningClock=_burstClock=_flow=0; BurstsReleased=0;
        _hiss.Stop(); _warning.Hide(); Show(); ProcessMode=ProcessModeEnum.Inherit;
        Align(); UpdateVisuals();
    }
    public void Deactivate()
    {
        Hide(); _flow=0; _warning?.Hide(); _hiss?.Stop();
        ProcessMode=ProcessModeEnum.Disabled;
    }
    private void Align()
    {
        _level.GetRockBounds(Position.Y,out float left,out float right);
        Position=new Vector2(_side<0 ? left : right,Position.Y);
        Reach=(right-left)*.25f;
        float margin=(_level.Runner?.CollisionEnvelopeRadius??SafePassage.PlayerRadius)+SafePassage.HalfWidth+SafePassage.Clearance+8;
        // Include both ends of the tilted plume and the player's full collision envelope.
        for(int i=0;i<9;i++)
        {
            float lane=_level.SafeLaneAt(Position.Y-140+i*30);
            Reach=Mathf.Min(Reach,_side<0 ? lane-margin-left : right-lane-margin);
        }
        Reach=Mathf.Max(0,Reach);
        float direction=-_side;
        _polygon[0]=new Vector2(0,-12); _polygon[1]=new Vector2(direction*Reach,-69);
        _polygon[2]=new Vector2(direction*Reach,-1); _polygon[3]=new Vector2(0,12);
        _gas.Position=new Vector2(0,-80); _gas.Size=new Vector2(Reach,120); _gas.Scale=new Vector2(direction,1);
    }
    public bool TouchesPlume(Vector2 world,float radius)
    {
        if(!IsEmitting || Reach<40) return false;
        Vector2 p=ToLocal(world);
        if(Geometry2D.IsPointInPolygon(p,_polygon)) return true;
        for(int i=0;i<4;i++)
            if(p.DistanceSquaredTo(Geometry2D.GetClosestPointToSegment(p,_polygon[i],_polygon[(i+1)%4]))<=radius*radius) return true;
        return false;
    }
    public override void _Process(double delta)
    {
        float remaining=Mathf.Max(0,(float)delta);
        while(remaining>0 && Active)
        {
            // Swept contact at high scrolling speed: never jump over a narrow plume in one frame.
            float dt=Mathf.Min(remaining,Mathf.Min(.01f,12/Mathf.Max(1,_level.FallSpeed))); remaining-=dt;
            if(!_level.IsGameActive) { Deactivate(); return; }
            Position-=new Vector2(0,_level.FallSpeed*dt); _clock+=dt; Align();
            if(!_level.CanDischargeMagma(Position.Y) || Reach<40) { _cancelled=true; _released=true; _hiss.Stop(); }
            float gap=Position.Y-(_level.Runner?.FixedY??-200);
            if(!_started && !_cancelled && gap<=_level.FallSpeed*1.8f)
            {
                _started=true;
                Vector2 listener=GetViewport().GetCanvasTransform().AffineInverse()*GetViewport().GetVisibleRect().GetCenter();
                _hiss.MaxDistance=Mathf.Max(1400,GlobalPosition.DistanceTo(listener)*1.7f); _hiss.Play();
            }
            else if(_started && !_released)
            {
                _warningClock+=dt;
                if(_warningClock>=WarningSeconds)
                {
                    _released=true;
                    // Acceleration or a late source must never shorten the warning into an ambush.
                    if(gap>_level.FallSpeed*.3f) BurstsReleased=1;
                    else _cancelled=true;
                }
            }
            if(_released)
            {
                _burstClock+=dt;
                _flow=_cancelled ? 0 : Mathf.Min(Mathf.Clamp(_burstClock/.12f,0,1),Mathf.Clamp((1.25f-_burstClock)/.22f,0,1));
            }
            if(TouchesPlume(_level.Runner.GlobalPosition,_level.Runner.CollisionEnvelopeRadius))
                _level.Runner.ApplyHotGas(dt*_flow*.42f);
            _warning.SetCue(_started && !_released,GlobalPosition,GlobalPosition,_warningClock/WarningSeconds,new Color("efa663"),collapse:true);
            if(CaveViewport.PassedTop(this,160) || _burstClock>2.2f) Deactivate();
        }
        UpdateVisuals();
    }
    private void UpdateVisuals()
    {
        _gasMaterial.SetShaderParameter("clock",_clock); _gasMaterial.SetShaderParameter("flow",_flow);
        float spentFade=_released ? Mathf.Clamp((2.2f-_burstClock)/.5f,0,1) : 1;
        _light.Energy=.025f*spentFade+_flow*.11f;
        QueueRedraw();
    }
    public override void _Draw()
    {
        float warmth=_started && !_released ? .25f+.4f*Mathf.Clamp(_warningClock/WarningSeconds,0,1) : .08f+_flow*.7f;
        float fade=_released ? Mathf.Clamp((2.2f-_burstClock)/.5f,0,1) : 1;
        for(int i=0;i<3;i++)
        {
            Vector2[] seam={new(_side*(17+i*4),-57+i*35),new(_side*7,-37+i*24),new(0,-15+i*13),new(_side*12,7+i*17)};
            DrawPolyline(seam,new Color(.047f,.063f,.071f,fade),9,true);
            DrawPolyline(seam,new Color(.38f,.17f,.08f,.7f*fade),4,true);
            DrawPolyline(seam,new Color(.93f,.38f,.12f,warmth*fade),1.5f,true);
        }
    }
}
