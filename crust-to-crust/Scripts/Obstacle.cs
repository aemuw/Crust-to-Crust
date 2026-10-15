using Godot;

public readonly record struct HazardMotion(float Frequency,float Amplitude,float Spin,float Drift,float Rotation);

/// <summary>Six cached variants; irregular motion, telegraphed dash, bounded lateral travel.</summary>
public partial class Obstacle : Area2D
{
    [Export] public float speed = 500f;
    private Sprite2D _sprite;
    private Sprite2D _lowLightDetail;
    private CollisionShape2D _collision;
    private Shape2D[] _shapes;
    private LightOccluder2D _shadow;
    private PointLight2D _glow;
    private HazardWarning _warning;
    private Line2D _rubbleEdge;
    private Vector2 _targetScale = Vector2.One;
    private float _appearTimer, _phase, _anchorX, _frequency, _amplitude, _spin, _driftVelocity, _dashTarget;
    private int _variant;
    private bool _dashStarted;
    private int _dashCount;
    private LevelGenerator _level;
    private int _safeSide;
    private float _protectedRadius;
    private float _scrollFactor=1f, _retireClock;
    private bool _retiring;
    private float _lateralVelocity;
    private readonly Sprite2D[] _pieces=new Sprite2D[4];
    private readonly Vector2[] _pieceOrigins=new Vector2[4];
    private const float AppearDuration = 0.35f;
    public const float WarningLeadSeconds = 0.9f;
    public bool WarningActive => _variant == 4 && _dashStarted && _phase <= WarningLeadSeconds;
    public float MotionDifficulty { get; set; }
    public override void _Ready()
    {
        GameAssets.Load(); _level = GetParent() as LevelGenerator; _sprite = GetNode<Sprite2D>("RockSprite"); _collision = GetNode<CollisionShape2D>("CollisionShape2D");
        // A faint texture floor keeps stone readable; the lit sprite still carries
        // almost all shading. Wheels retain most of their incandescent appearance.
        _lowLightDetail=new Sprite2D { Name="LowLightDetail",Material=GameAssets.Unshaded };
        _sprite.AddChild(_lowLightDetail);
        _shapes = new Shape2D[] { new CircleShape2D { Radius = 29 },new RectangleShape2D { Size = new Vector2(26,72) },
            new CircleShape2D { Radius = 49 },new CircleShape2D { Radius = 37 },
            new RectangleShape2D { Size = new Vector2(24,66) },new CircleShape2D { Radius = 24 } };
        _shadow = new LightOccluder2D { Occluder = new OccluderPolygon2D { CullMode=OccluderPolygon2D.CullModeEnum.Disabled } };
        AddChild(_shadow);
        for(int i=0;i<4;i++) { _pieces[i]=new Sprite2D { RegionEnabled=true,Visible=false }; AddChild(_pieces[i]); }
        _glow = CaveLighting.Light(new Color("ff7028"),0.32f,0.40f,false); AddChild(_glow); _glow.Visible = false;
        _warning=new HazardWarning { Name="HazardWarning" }; AddChild(_warning);
        _rubbleEdge=new Line2D { Name="RubbleEdge",Closed=true,Width=1.1f,DefaultColor=new Color(.32f,.28f,.22f,.65f),Material=GameAssets.Unshaded,Visible=false,Antialiased=true };
        AddChild(_rubbleEdge);
    }
    public static HazardMotion SampleMotion(RunDirector director)
    {
        float Random(float min,float max) => director?.NextFloat(min,max) ?? (float)GD.RandRange(min,max);
        return new HazardMotion(Random(0.7f,1.7f),Random(65,135),(Random(0,1)<0.5f ? -1f : 1f)*Random(0.8f,2.6f),
            (Random(0,1)<0.5f ? -1f : 1f)*Random(120,240),Random(-0.4f,0.4f));
    }
    private bool _crystalFragment;
    public void ConfigureVariant(int variant,float targetX = 0f,HazardMotion? motion = null)
    {
        _crystalFragment=false;
        _rubbleEdge.Hide();
        _safeSide = 0;
        _variant = Mathf.Clamp(variant,0,5); _phase = 0f; _dashStarted = false; _dashCount=0; _dashTarget = targetX;
        _retiring=false; _retireClock=0;
        _lateralVelocity=0;
        foreach(var piece in _pieces) piece.Hide();
        _sprite.Show(); _collision.SetDeferred("disabled",false); _glow.Energy=0.4f;
        _scrollFactor=DefaultScroll;
        _anchorX = Position.X;
        HazardMotion profile = motion ?? SampleMotion(_level?.Director);
        _frequency = profile.Frequency; _amplitude = profile.Amplitude;
        _spin = profile.Spin; _driftVelocity = profile.Drift;
        _sprite.Texture = GameAssets.HazardTextures[_variant];
        float canvas = HazardSilhouettes.CanvasSize(_variant);
        _shadow.Occluder.Polygon = HazardSilhouettes.Outline(_variant);
        _sprite.Scale = Vector2.One*canvas/_sprite.Texture.GetWidth();
        _sprite.Position = Vector2.Zero; _sprite.Rotation = 0f;
        _sprite.Material = _variant == 2 ? GameAssets.Unshaded : null;
        _lowLightDetail.Texture=_sprite.Texture;
        _lowLightDetail.Visible=_variant!=2;
        _lowLightDetail.Modulate=new Color(1,1,1,_variant is 3 or 5 ? 0.82f : 0.045f);
        _collision.Shape = _shapes[_variant];
        _glow.Visible = _variant == 2 || _variant == 3 || _variant == 5;
        _glow.Color = _variant == 2 ? new Color("59dfe8") : new Color("ff7028");
        _warning.Hide();
        _warning.Modulate = Colors.White;
        _shadow.Visible = _variant != 2;
        ZIndex = _variant == 2 ? -7 : 0;
        Rotation = _variant == 1 ? 0.15f : profile.Rotation;
    }
    public void ConfigureCrystalFragment()
    {
        ConfigureVariant(2,0,new HazardMotion(0,0,.8f,0,0));
        _crystalFragment=true; ZIndex=0; _glow.Energy=.2f;
    }
    public void MarkCollapseDebris()
    {
        _rubbleEdge.Points=HazardSilhouettes.Outline(0); _rubbleEdge.Show();
    }
    public bool ProtectPassage(int side,float scale,float? scrollFactor=null)
    {
        _safeSide = side < 0 ? -1 : 1;
        _protectedRadius = SafePassage.ColliderRadius(_variant)*scale*SafePassage.AppearOvershoot;
        _scrollFactor=scrollFactor??DefaultScroll;
        if(_level!=null && !_level.CanHazardTraverse(_safeSide,Position.Y,_protectedRadius,_scrollFactor))
        {
            // Match wall scrolling when a faster/slower vertical path would pinch later.
            _scrollFactor=1f;
            if(!_level.CanHazardTraverse(_safeSide,Position.Y,_protectedRadius,_scrollFactor)) return false;
        }
        return KeepPassageOpen();
    }
    private float DefaultScroll=>_variant==0 ? 0.88f : _variant==1 ? 1.08f : _variant==5 ? 1.28f : 1f;
    private bool KeepPassageOpen()
    {
        if (_safeSide == 0 || _level == null) return true;
        _level.GetRockBounds(Position.Y,out float left,out float right);
        float lane = _level.SafeLaneAt(Position.Y);
        if (!SafePassage.TryBounds(_safeSide,lane,_protectedRadius,left,right,out float min,out float max,_level.Runner?.CollisionEnvelopeRadius??SafePassage.PlayerRadius))
        {
            RetireForClearance();
            return false;
        }
        float x = SafePassage.Clamp(Position.X,min,max);
        if(x!=Position.X) _lateralVelocity=-_lateralVelocity*0.45f;
        if (_variant == 3 && x != Position.X)
            _driftVelocity = x <= min ? Mathf.Abs(_driftVelocity) : -Mathf.Abs(_driftVelocity);
        Position = new Vector2(x,Position.Y);
        return true;
    }
    public void TriggerAppear(Vector2 targetScale)
    {
        _targetScale = targetScale; _appearTimer = 0f;
        Scale = Vector2.Zero; Modulate = new Color(1,1,1,0);
        _anchorX = Position.X;
    }
    public override void _Process(double delta)
    {
        // Small deterministic steps keep inertia, bounces and warning timing stable at low FPS.
        float remaining=Mathf.Max(0,(float)delta);
        while(remaining>0 && Visible)
        {
            float step=Mathf.Min(remaining,0.01f); StepMotion(step); remaining-=step;
        }
    }
    private float MoveWithInertia(float target,float dt,float maximum,float acceleration)
    {
        float wanted=Mathf.Clamp((target-Position.X)*5,-maximum,maximum);
        _lateralVelocity=Mathf.MoveToward(_lateralVelocity,wanted,acceleration*dt);
        return Position.X+_lateralVelocity*dt;
    }
    private void StepMotion(float dt)
    {
        if(_retiring)
        {
            Position+=new Vector2(0,-speed*_scrollFactor*dt); _retireClock+=dt;
            float fade=Mathf.Clamp(1-_retireClock/0.65f,0,1);
            for(int i=0;i<4;i++)
            {
                Vector2 direction=new Vector2(i%2==0 ? -1 : 1,i<2 ? -0.6f : 0.6f);
                _pieces[i].Position=_pieceOrigins[i]+direction*_retireClock*65;
                _pieces[i].Rotation+=_spin*dt; _pieces[i].Modulate=new Color(1,1,1,fade);
            }
            _glow.Energy=0.4f*fade;
            if(CaveViewport.PassedTop(this,120)) Deactivate();
            return;
        }
        if (_appearTimer == 0f) _anchorX = Position.X;
        _phase += dt;
        float intensity = Mathf.Lerp(1f,1.5f,MotionDifficulty);
        float scroll = _scrollFactor;
        Position += new Vector2(0,-speed*scroll*dt);
        if (speed > 0f)
        {
            if (_variant == 0 || _variant == 1 || _variant == 5)
            {
                float center = _safeSide != 0 && _level != null ? _level.SafeLaneAt(Position.Y) : _anchorX;
                float x = _safeSide != 0 ? center + _safeSide * (92f + 22f*Mathf.Sin(_phase*_frequency*intensity))
                    : _anchorX + Mathf.Sin(_phase*_frequency*intensity)*_amplitude;
                if (_variant == 5) x += Mathf.Sin(_phase*3.7f+1.3f)*35f;
                float moved=MoveWithInertia(x,dt,_variant==5 ? 310*intensity : 230*intensity,_variant==0 ? 520 : 950);
                Position = new Vector2(_safeSide == 0 ? Mathf.Clamp(moved,-180,180) : moved,Position.Y);
                Rotation = _variant == 1 ? Mathf.Sin(_phase*2.1f*intensity)*1.05f : Rotation+(_variant == 0 ? _spin : _spin+3.5f)*dt;
            }
            else if (_variant == 3)
            {
                float x = Position.X + _driftVelocity*dt*intensity;
                if (_safeSide == 0 && (x < -180f || x > 180f)) { _driftVelocity = -_driftVelocity*0.92f; x = Mathf.Clamp(x,-180,180); }
                Position = new Vector2(x,Position.Y); Rotation += (_driftVelocity>0 ? 1 : -1)*5.5f*dt;
            }
            else if (_variant == 4)
            {
                // Dash is triggered on approach, not at off-screen spawn time.
                float playerY = _level?.Runner?.FixedY ?? -200f;
                float lead=MotionDifficulty>0.4f ? WarningLeadSeconds*2+0.8f : WarningLeadSeconds+0.25f;
                if (!_dashStarted && Position.Y - playerY <= speed * lead)
                {
                    _dashStarted = true; _phase = 0f;
                    if (_level != null) _dashTarget = _level.Runner?.Position.X ?? _dashTarget;
                }
                if (_dashStarted && _phase > WarningLeadSeconds)
                {
                    Position = new Vector2(MoveWithInertia(_dashTarget,dt,360f*intensity,1500),Position.Y);
                    _warning.Hide(); Rotation += _spin*dt;
                    if(MotionDifficulty>0.4f && _dashCount==0 && _phase>WarningLeadSeconds+0.25f && Position.Y-playerY>speed*(WarningLeadSeconds+0.12f))
                    {
                        _dashCount++; _phase=0f; _dashTarget=_level?.Runner?.Position.X??_dashTarget;
                        _warning.Show();
                    }
                }

            }
        }
        _warning.SetCue(WarningActive && !_retiring,GlobalPosition,
            new Vector2(_dashTarget,GlobalPosition.Y),_phase/WarningLeadSeconds,new Color("ffbd70"),true);
        // Apply after sway, bounce and dash. Includes maximum appearance scale;
        // no variant can cross the continuously protected corridor.
        if(_crystalFragment) Rotation+=_spin*dt;
        if (!KeepPassageOpen()) return;
        if (_appearTimer < AppearDuration)
        {
            _appearTimer += dt; float t = Mathf.Clamp(_appearTimer/AppearDuration,0,1);
            float ease = 1+2.70158f*Mathf.Pow(t-1,3)+1.70158f*Mathf.Pow(t-1,2);
            Scale = _targetScale*Mathf.Clamp(ease,0,1.15f); Modulate = new Color(1,1,1,t);
        }
        if (CaveViewport.PassedTop(this,Mathf.Max(_protectedRadius,90))) Deactivate();
    }
    private void Deactivate() { _warning.Hide(); SetDeferred("monitorable",false); Hide(); ProcessMode=ProcessModeEnum.Disabled; }
    public void RetireForClearance()
    {
        if(_retiring) return;
        if(!CaveViewport.Intersects(this,Mathf.Max(_protectedRadius,90))) { Deactivate(); return; }
        _retiring=true; _retireClock=0;
        _rubbleEdge.Hide();
        SetDeferred("monitorable",false); _collision.SetDeferred("disabled",true);
        _warning.Hide(); _shadow.Hide();
        Vector2 half=_sprite.Texture.GetSize()/2;
        for(int i=0;i<4;i++)
        {
            var piece=_pieces[i]; Vector2 top=new Vector2(i%2,i/2)*half;
            piece.Texture=_sprite.Texture; piece.RegionRect=new Rect2(top,half);
            piece.Scale=_sprite.Scale; piece.Material=_sprite.Material;
            _pieceOrigins[i]=(top+half/2-_sprite.Texture.GetSize()/2)*_sprite.Scale;
            piece.Position=_pieceOrigins[i]; piece.Rotation=0; piece.Modulate=Colors.White; piece.Show();
        }
        _sprite.Hide();
    }
    public bool BlocksEscapeAt(float x,float seconds,float playerRadius)
    {
        if(_retiring) return false;
        float scroll=_scrollFactor;
        float y=Position.Y-speed*scroll*seconds;
        float radius=SafePassage.ColliderRadius(_variant)*_targetScale.X*SafePassage.AppearOvershoot;
        if(Mathf.Abs(y-(_level?.Runner?.FixedY??-200))>radius+playerRadius+speed*0.04f) return false;
        if(_safeSide!=0 && _level!=null)
            return _safeSide*(x-_level.SafeLaneAt((_level.Runner?.FixedY??-200)+speed*seconds))>SafePassage.HalfWidth;
        return Mathf.Abs(x-Position.X)<radius+playerRadius+_amplitude;
    }
}
