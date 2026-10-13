using Godot;

/// <summary>Six cached variants; chaotic but bounded motion, telegraphed accelerating dash.</summary>
public partial class Obstacle : Area2D
{
    [Export] public float speed = 500f;

    // Спільні ресурси для всіх перешкод (раніше створювались в кожному екземплярі).
    private static readonly Shape2D[] SharedShapes = {
        new CircleShape2D { Radius = 29 },new RectangleShape2D { Size = new Vector2(26,72) },
        new CircleShape2D { Radius = 49 },new CircleShape2D { Radius = 37 },
        new RectangleShape2D { Size = new Vector2(24,66) },new CircleShape2D { Radius = 24 } };
    private static readonly OccluderPolygon2D SharedOccluder = new OccluderPolygon2D {
        Polygon = new[] { new Vector2(-26,-26),new Vector2(26,-26),new Vector2(26,26),new Vector2(-26,26) } };
    private static readonly Color WarningColor = new Color("ffb556");

    private Sprite2D _sprite;
    private CollisionShape2D _collision;
    private LightOccluder2D _shadow;
    private Label _warning;
    private Vector2 _targetScale = Vector2.One;
    private float _appearTimer, _phase, _anchorX, _anchorGoal, _frequency, _amplitude, _spin, _driftVelocity, _dashTarget;
    private float _kickTimer, _surgePhase, _speedJitter, _dashSpeed;
    private int _variant;
    private bool _dashStarted, _dashFired, _reaimed, _willReaim;
    private LevelGenerator _level;
    private int _safeSide;
    private float _protectedRadius;
    private const float AppearDuration = 0.35f;
    public float MotionDifficulty { get; set; }

    private static float R(float a,float b) => (float)GD.RandRange(a,b);
    private static float RandSign() => GD.Randf() < 0.5f ? -1f : 1f;

    public override void _Ready()
    {
        GameAssets.Load(); _level = GetParent() as LevelGenerator; _sprite = GetNode<Sprite2D>("RockSprite"); _collision = GetNode<CollisionShape2D>("CollisionShape2D");
        _shadow = new LightOccluder2D { Occluder = SharedOccluder };
        AddChild(_shadow);
        _warning = new Label { Text = "!",Position = new Vector2(-10,-76),MouseFilter = Control.MouseFilterEnum.Ignore };
        _warning.AddThemeFontSizeOverride("font_size",34); _warning.AddThemeColorOverride("font_color",WarningColor);
        _warning.Material = GameAssets.Unshaded; AddChild(_warning);
    }

    public void ConfigureVariant(int variant,float targetX = 0f)
    {
        _safeSide = 0;
        _variant = Mathf.Clamp(variant,0,5); _phase = 0f; _dashStarted = _dashFired = _reaimed = false; _dashTarget = targetX;
        _dashSpeed = 0f;
        _willReaim = GD.Randf() < 0.35f; // частина "стрибунів" коригує ривок посеред шляху
        _anchorX = _anchorGoal = Position.X;
        _frequency = R(0.7f,1.7f); _amplitude = R(65f,135f);
        _spin = RandSign()*R(0.8f,2.6f);
        _driftVelocity = RandSign()*R(120f,240f);
        _surgePhase = R(0f,Mathf.Tau);
        _speedJitter = R(0.93f,1.07f);
        _kickTimer = R(0.6f,1.4f);
        _sprite.Texture = GameAssets.HazardTextures[_variant];
        float canvas = _variant == 1 || _variant == 4 ? 103f : _variant == 2 ? 145f : _variant == 3 ? 103f : _variant == 5 ? 70f : 87f;
        _sprite.Scale = Vector2.One*canvas/_sprite.Texture.GetWidth();
        _sprite.Position = Vector2.Zero; _sprite.Rotation = 0f;
        _sprite.Material = null; // перешкоди освітлюються лише світлом (фанарик, кристали)
        _collision.Shape = SharedShapes[_variant];
        _warning.Visible = _variant == 4;
        _shadow.Visible = _variant != 2;
        ZIndex = _variant == 2 ? -7 : 0;
        Rotation = _variant == 1 ? 0.15f : R(-0.4f,0.4f);
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
        _anchorX = _anchorGoal = Position.X;
    }

    // Періодичний "поштовх": раптова зміна траєкторії, що робить рух менш передбачуваним.
    private void Kick(float intensity)
    {
        switch (_variant)
        {
            case 0:
                _kickTimer = R(1.3f,2.5f);
                _anchorGoal = Mathf.Clamp(_anchorX + RandSign()*R(40f,90f),-170f,170f);
                _amplitude = R(65f,135f);
                break;
            case 3:
                _kickTimer = R(0.9f,1.8f);
                _driftVelocity = (GD.Randf() < 0.4f ? -Mathf.Sign(_driftVelocity) : Mathf.Sign(_driftVelocity)) * R(120f,240f);
                break;
            case 5:
                _kickTimer = R(0.7f,1.5f);
                _anchorGoal = Mathf.Clamp(_anchorX + RandSign()*R(60f,110f),-170f,170f);
                _spin = RandSign()*R(0.8f,2.6f);
                break;
            default:
                _kickTimer = 99f;
                break;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_appearTimer == 0f) _anchorX = Position.X;
        _phase += dt;
        float intensity = Mathf.Lerp(1f,1.5f,MotionDifficulty);

        float scroll = _variant == 0 ? 0.88f : _variant == 1 ? 1.08f : _variant == 5 ? 1.28f : 1f;
        scroll *= _speedJitter;
        // Хвилі прискорення/сповільнення: перешкоди то "наповзають", то відстають.
        if (_variant != 2 && _variant != 4)
            scroll *= 1f + Mathf.Lerp(0.06f,0.14f,MotionDifficulty)*Mathf.Sin(_phase*1.3f + _surgePhase);
        Position += new Vector2(0,-speed*scroll*dt);

        if (speed > 0f)
        {
            if (_variant == 0 || _variant == 1 || _variant == 5)
            {
                if (_variant != 1)
                {
                    _kickTimer -= dt;
                    if (_kickTimer <= 0f) Kick(intensity);
                    _anchorX = Mathf.MoveToward(_anchorX,_anchorGoal,150f*intensity*dt);
                }
                float x = _anchorX + Mathf.Sin(_phase*_frequency*intensity)*_amplitude;
                if (_variant == 0) x += Mathf.Sin(_phase*_frequency*2.3f + 1.1f)*_amplitude*0.22f; // друга гармоніка
                if (_variant == 1) x += Mathf.Sin(_phase*2.1f + _surgePhase)*28f;
                if (_variant == 5) x += Mathf.Sin(_phase*3.7f + 1.3f)*35f;
                Position = new Vector2(_safeSide == 0 ? Mathf.Clamp(x,-180,180) : x,Position.Y);
                Rotation = _variant == 1 ? Mathf.Sin(_phase*1.65f)*0.70f : Rotation+(_variant == 0 ? _spin : _spin+3.5f)*dt;
            }
            else if (_variant == 2)
            {
                // Велика кристалічна брила повільно "дихає" з боку в бік.
                float x = _anchorX + Mathf.Sin(_phase*0.55f + _surgePhase)*38f;
                Position = new Vector2(_safeSide == 0 ? Mathf.Clamp(x,-180,180) : x,Position.Y);
            }
            else if (_variant == 3)
            {
                _kickTimer -= dt;
                if (_kickTimer <= 0f) Kick(intensity);
                float x = Position.X + _driftVelocity*dt*intensity;
                if (_safeSide == 0 && (x < -180f || x > 180f)) { _driftVelocity = -_driftVelocity; x = Mathf.Clamp(x,-180,180); }
                Position = new Vector2(x,Position.Y); Rotation += (_driftVelocity>0 ? 1 : -1)*5.5f*dt;
            }
            else if (_variant == 4)
            {
                // Ривок запускається при наближенні, а не при спавні за екраном.
                if (!_dashStarted && Position.Y < 480f)
                {
                    _dashStarted = true; _phase = 0f;
                    if (_level != null) _dashTarget = _level.Runner?.Position.X ?? _dashTarget;
                }
                if (_dashStarted && _phase > 0.65f)
                {
                    if (!_dashFired) { _dashFired = true; _dashSpeed = 120f; _warning.Hide(); }
                    // Одноразове перенацілювання: ривок складніше передбачити.
                    if (_willReaim && !_reaimed && _phase > 0.95f && _level != null)
                    {
                        _reaimed = true; _dashTarget = _level.Runner?.Position.X ?? _dashTarget;
                    }
                    _dashSpeed = Mathf.MoveToward(_dashSpeed,360f*intensity,900f*dt); // розгін замість сталої швидкості
                    Position = new Vector2(Mathf.MoveToward(Position.X,_dashTarget,_dashSpeed*dt),Position.Y);
                    Rotation += _spin*dt;
                }
                else if (_dashStarted)
                {
                    // Чим ближче ривок, тим частіше блимає попередження.
                    float rate = 16f + 14f*Mathf.Clamp(_phase/0.65f,0f,1f);
                    _warning.Modulate = new Color(1,1,1,0.55f+0.45f*Mathf.Sin(_phase*rate));
                }
            }
        }

        // Застосовується після гойдання, відскоків і ривка. Враховує максимальний масштаб появи;
        // жоден варіант не може перетнути постійно захищений коридор.
        if (!KeepPassageOpen()) return;
        if (_appearTimer < AppearDuration)
        {
            _appearTimer += dt; float t = Mathf.Clamp(_appearTimer/AppearDuration,0,1);
            float t1 = t-1f;
            float ease = 1+2.70158f*t1*t1*t1+1.70158f*t1*t1; // без Mathf.Pow
            Scale = _targetScale*Mathf.Clamp(ease,0,1.15f); Modulate = new Color(1,1,1,t);
        }
        if (Position.Y < -560f) { SetDeferred("monitorable",false); Hide(); ProcessMode = ProcessModeEnum.Disabled; }
    }
}
