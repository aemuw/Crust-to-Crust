using Godot;

public readonly record struct HazardMotion(float Frequency,float Amplitude,float Spin,float Drift,float Rotation);

/// <summary>Six cached variants; irregular motion, telegraphed dash, bounded lateral travel.</summary>
public partial class Obstacle : Area2D
{
    [Export] public float speed = 500f;
    private Sprite2D _sprite;
    private CollisionShape2D _collision;
    private Shape2D[] _shapes;
    private LightOccluder2D _shadow;
    private PointLight2D _glow;
    private Label _warning;
    private Node2D _warningAnchor;
    private Vector2 _targetScale = Vector2.One;
    private float _appearTimer, _phase, _anchorX, _frequency, _amplitude, _spin, _driftVelocity, _dashTarget;
    private int _variant;
    private bool _dashStarted;
    private LevelGenerator _level;
    private Camera2D _camera;
    private int _safeSide;
    private float _protectedRadius;
    private const float AppearDuration = 0.35f;
    public const float WarningLeadSeconds = 0.9f;
    public bool WarningActive => _variant == 4 && _dashStarted && _phase <= WarningLeadSeconds;
    public float MotionDifficulty { get; set; }
    public override void _Ready()
    {
        GameAssets.Load(); _level = GetParent() as LevelGenerator; _sprite = GetNode<Sprite2D>("RockSprite"); _collision = GetNode<CollisionShape2D>("CollisionShape2D");
        _camera = _level?.GetNodeOrNull<Camera2D>("Camera2D");
        _shapes = new Shape2D[] { new CircleShape2D { Radius = 29 },new RectangleShape2D { Size = new Vector2(26,72) },
            new CircleShape2D { Radius = 49 },new CircleShape2D { Radius = 37 },
            new RectangleShape2D { Size = new Vector2(24,66) },new CircleShape2D { Radius = 24 } };
        _shadow = new LightOccluder2D { Occluder = new OccluderPolygon2D {
            Polygon = new[] { new Vector2(-26,-26),new Vector2(26,-26),new Vector2(26,26),new Vector2(-26,26) } } };
        AddChild(_shadow);
        _glow = CaveLighting.Light(new Color("ff7028"),0.32f,0.40f,false); AddChild(_glow); _glow.Visible = false;
        _warning = new Label { Text = "!",Position = new Vector2(-10,0),MouseFilter = Control.MouseFilterEnum.Ignore };
        _warning.AddThemeFontSizeOverride("font_size",34); _warning.AddThemeColorOverride("font_color",new Color("ffb556"));
        _warning.Material = GameAssets.Unshaded;
        _warningAnchor = new Node2D { TopLevel = true,ZIndex = 20 }; AddChild(_warningAnchor);
        _warningAnchor.AddChild(_warning);
    }
    public static HazardMotion SampleMotion(RunDirector director)
    {
        float Random(float min,float max) => director?.NextFloat(min,max) ?? (float)GD.RandRange(min,max);
        return new HazardMotion(Random(0.7f,1.7f),Random(65,135),(Random(0,1)<0.5f ? -1f : 1f)*Random(0.8f,2.6f),
            (Random(0,1)<0.5f ? -1f : 1f)*Random(120,240),Random(-0.4f,0.4f));
    }
    public void ConfigureVariant(int variant,float targetX = 0f,HazardMotion? motion = null)
    {
        _safeSide = 0;
        _variant = Mathf.Clamp(variant,0,5); _phase = 0f; _dashStarted = false; _dashTarget = targetX;
        _anchorX = Position.X;
        HazardMotion profile = motion ?? SampleMotion(_level?.Director);
        _frequency = profile.Frequency; _amplitude = profile.Amplitude;
        _spin = profile.Spin; _driftVelocity = profile.Drift;
        _sprite.Texture = GameAssets.HazardTextures[_variant];
        float canvas = _variant == 1 || _variant == 4 ? 103f : _variant == 2 ? 145f : _variant == 3 ? 103f : _variant == 5 ? 70f : 87f;
        _sprite.Scale = Vector2.One*canvas/_sprite.Texture.GetWidth();
        _sprite.Position = Vector2.Zero; _sprite.Rotation = 0f;
        _sprite.Material = _variant >= 2 && _variant != 4 ? GameAssets.Unshaded : null;
        _collision.Shape = _shapes[_variant];
        _glow.Visible = _variant == 2 || _variant == 3 || _variant == 5;
        _glow.Color = _variant == 2 ? new Color("59dfe8") : new Color("ff7028");
        _warning.Visible = _variant == 4;
        _warning.Modulate = Colors.White;
        _shadow.Visible = _variant != 2;
        ZIndex = _variant == 2 ? -7 : 0;
        Rotation = _variant == 1 ? 0.15f : profile.Rotation;
    }
    public bool ProtectPassage(int side,float scale)
    {
        _safeSide = side < 0 ? -1 : 1;
        _protectedRadius = SafePassage.ColliderRadius(_variant)*scale*SafePassage.AppearOvershoot;
        return KeepPassageOpen();
    }
    private bool KeepPassageOpen()
    {
        if (_safeSide == 0 || _level == null) return true;
        _level.GetRockBounds(Position.Y,out float left,out float right);
        float lane = _level.SafeLaneAt(Position.Y);
        if (!SafePassage.TryBounds(_safeSide,lane,_protectedRadius,left,right,out float min,out float max))
        {
            SetDeferred("monitorable",false); Hide(); ProcessMode = ProcessModeEnum.Disabled;
            return false;
        }
        float x = SafePassage.Clamp(Position.X,min,max);
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
        float dt = (float)delta;
        if (_appearTimer == 0f) _anchorX = Position.X;
        _phase += dt;
        float intensity = Mathf.Lerp(1f,1.5f,MotionDifficulty);
        float scroll = _variant == 0 ? 0.88f : _variant == 1 ? 1.08f : _variant == 5 ? 1.28f : 1f;
        Position += new Vector2(0,-speed*scroll*dt);
        if (speed > 0f)
        {
            if (_variant == 0 || _variant == 1 || _variant == 5)
            {
                float center = _safeSide != 0 && _level != null ? _level.SafeLaneAt(Position.Y) : _anchorX;
                float x = _safeSide != 0 ? center + _safeSide * (92f + 22f*Mathf.Sin(_phase*_frequency*intensity))
                    : _anchorX + Mathf.Sin(_phase*_frequency*intensity)*_amplitude;
                if (_variant == 5) x += Mathf.Sin(_phase*3.7f+1.3f)*35f;
                Position = new Vector2(_safeSide == 0 ? Mathf.Clamp(x,-180,180) : x,Position.Y);
                Rotation = _variant == 1 ? Mathf.Sin(_phase*1.65f)*0.70f : Rotation+(_variant == 0 ? _spin : _spin+3.5f)*dt;
            }
            else if (_variant == 3)
            {
                float x = Position.X + _driftVelocity*dt*intensity;
                if (_safeSide == 0 && (x < -180f || x > 180f)) { _driftVelocity = -_driftVelocity; x = Mathf.Clamp(x,-180,180); }
                Position = new Vector2(x,Position.Y); Rotation += (_driftVelocity>0 ? 1 : -1)*5.5f*dt;
            }
            else if (_variant == 4)
            {
                // Dash is triggered on approach, not at off-screen spawn time.
                float playerY = _level?.Runner?.FixedY ?? -200f;
                if (!_dashStarted && Position.Y - playerY <= speed * (WarningLeadSeconds + 0.25f))
                {
                    _dashStarted = true; _phase = 0f;
                    if (_level != null) _dashTarget = _level.Runner?.Position.X ?? _dashTarget;
                }
                if (_dashStarted && _phase > WarningLeadSeconds)
                {
                    Position = new Vector2(Mathf.MoveToward(Position.X,_dashTarget,360f*intensity*dt),Position.Y);
                    _warning.Hide(); Rotation += _spin*dt;
                }
                else if (_dashStarted) _warning.Modulate = new Color(1,1,1,1f-SaveData.FlashIntensity*0.225f*(1f-Mathf.Sin(_phase*22f)));
            }
        }
        var camera = _camera;
        if (camera != null)
        {
            Transform2D canvas = GetViewport().GetCanvasTransform();
            Vector2 screen = canvas * GlobalPosition + new Vector2(0,-76);
            Vector2 size = GetViewport().GetVisibleRect().Size;
            screen.X = Mathf.Clamp(screen.X,24,size.X-44); screen.Y = Mathf.Clamp(screen.Y,24,size.Y-100);
            _warningAnchor.GlobalPosition = canvas.AffineInverse()*screen;
            _warningAnchor.GlobalRotation = canvas.AffineInverse().X.Angle();
        }
        else _warningAnchor.GlobalPosition = GlobalPosition+new Vector2(0,-76);
        // Apply after sway, bounce and dash. Includes maximum appearance scale;
        // no variant can cross the continuously protected corridor.
        if (!KeepPassageOpen()) return;
        if (_appearTimer < AppearDuration)
        {
            _appearTimer += dt; float t = Mathf.Clamp(_appearTimer/AppearDuration,0,1);
            float ease = 1+2.70158f*Mathf.Pow(t-1,3)+1.70158f*Mathf.Pow(t-1,2);
            Scale = _targetScale*Mathf.Clamp(ease,0,1.15f); Modulate = new Color(1,1,1,t);
        }
        if (Position.Y < -560f) { SetDeferred("monitorable",false); Hide(); ProcessMode = ProcessModeEnum.Disabled; }
    }
}
