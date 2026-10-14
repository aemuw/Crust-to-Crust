using Godot;

// Keep the pooled resource path compatible; this is now an embedded cryogenic fissure.
public partial class Water : Area2D
{
    [Export] public float speed = 500f;
    public const float CaveHeight = 220f;
    public const float HalfHeight = CaveHeight * 0.5f;
    private LevelGenerator _level;
    private Sprite2D _rock;
    private CollisionPolygon2D _zone;
    private ColorRect _gas;
    private ShaderMaterial _gasMaterial;
    private PointLight2D _light;
    private AudioStreamPlayer2D _hiss;
    private bool _fromLeft;
    private bool _hissStarted;
    private float _clock, _flow;
    private float _shapeReach;
    public bool FromLeft => _fromLeft;
    public bool IsCoolingActive => _flow > 0.65f && Visible && Monitorable;
    public float StreamReach { get; private set; }
    public Vector2 SourcePosition { get; private set; }
    public Vector2 StreamCenter => ToGlobal(SourcePosition + new Vector2((_fromLeft ? 1 : -1) * StreamReach * 0.55f, -22));

    public override void _Ready()
    {
        _level = GetParent() as LevelGenerator;
        _level?.RegisterWater(this);
        _zone = new CollisionPolygon2D { Name = "CoolingZone" }; AddChild(_zone);
        _rock = new Sprite2D { Name = "FissureRock", Texture = new AtlasTexture {
            Atlas = GD.Load<Texture2D>("res://Textures/ArtRefresh/cryo_fissure.png"), Region = new Rect2(388, 11, 512, 1514) },
            Scale = Vector2.One * 0.14f, ZIndex = 3,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/CryoRock.gdshader") } };
        AddChild(_rock);
        _gasMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/CryoGas.gdshader") };
        _gas = new ColorRect { Name = "ColdGas", MouseFilter = Control.MouseFilterEnum.Ignore, Material = _gasMaterial, ZIndex = 4 };
        AddChild(_gas);
        _light = CaveLighting.Light(new Color("91dbe7"), 0.35f, 0.12f, false); AddChild(_light);
        _hiss = new AudioStreamPlayer2D { Name = "FissureHiss", Stream = GD.Load<AudioStream>("res://Audio/SFX/cryo_vent.wav"),
            Bus = AudioManager.SfxBus, VolumeDb = -22, MaxDistance = 600, PitchScale = 0.85f };
        AddChild(_hiss);
        GenerateRandomCaveGeometry();
    }
    public void GenerateRandomCaveGeometry(bool? fromLeft = null)
    {
        _fromLeft = fromLeft ?? ((_level?.Director.NextInt(0, 2) ?? GD.RandRange(0, 1)) == 0);
        _clock = 0; _flow = 0; _shapeReach = 0; _hissStarted = false;
        _hiss?.Stop();
        AlignToWall();
    }
    private void AlignToWall()
    {
        float left = -280, right = 280;
        _level?.GetRockBounds(Position.Y, out left, out right);
        StreamReach = (right - left) * 0.25f;
        float direction = _fromLeft ? 1 : -1;
        SourcePosition = new Vector2(_fromLeft ? left : right, 0);
        _rock.Position = SourcePosition - new Vector2(direction * 10, 0);
        _rock.FlipH = _fromLeft;
        _gas.Position = SourcePosition + new Vector2(0, -100);
        _gas.Size = new Vector2(StreamReach, 120);
        _gas.Scale = new Vector2(direction, 1);
        _zone.Position = SourcePosition;
        // Replacing polygon shapes each frame invalidates the physics overlap cache.
        if (Mathf.Abs(_shapeReach - StreamReach) > 0.5f)
        {
            Vector2[] points = { new(0,-14), new(0,14), new(StreamReach,-16), new(StreamReach,-64) };
            for (int i = 0; i < points.Length; i++) points[i].X *= direction;
            _zone.Polygon = points; _shapeReach = StreamReach;
        }
        _light.Position = SourcePosition;
        _hiss.Position = SourcePosition;
    }
    public void UpdateBiomeColors(Color wallColor, Color borderColor) { }
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Position += new Vector2(0, -speed * dt);
        AlignToWall(); _clock += dt;
        float gap = GlobalPosition.Y - (_level?.Runner?.GlobalPosition.Y ?? -200);
        float eta = gap / Mathf.Max(speed, 200);
        // Later fissures warn before the approach, then remain open through the crossing.
        bool early = (_level?.ActivePlaySeconds ?? 0) < 45 || (_level?.TutorialWaterPending ?? false);
        bool discharge = early || eta < 1.5f && eta > -0.7f;
        _flow = Mathf.MoveToward(_flow, discharge ? 1 : 0, dt * 5);
        float warning = !early && eta >= 1.5f && eta < 2.3f ? 0.5f + 0.5f * Mathf.Sin(_clock * 17) : 0;
        _gasMaterial.SetShaderParameter("clock", _clock);
        _gasMaterial.SetShaderParameter("flow", _flow);
        _rock.Modulate = new Color(0.6f, 0.76f, 0.82f).Lerp(Colors.White, Mathf.Max(_flow * 0.65f, warning));
        _light.Energy = 0.06f + 0.11f * _flow + warning * 0.04f;
        if (!_hissStarted && gap < 650 && gap > -200 && (_flow > 0.65f || warning > 0.5f))
        { _hissStarted = true; _hiss.Play(); }
        if (CaveViewport.PassedTop(this, HalfHeight + 100))
        {
            _hiss.Stop(); SetDeferred("monitorable", false); Visible = false; ProcessMode = ProcessModeEnum.Disabled;
        }
    }
}
