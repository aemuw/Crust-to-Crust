using Godot;

public enum PickupKind { Coin, Shield, DoubleCoins, Magnet }

public partial class Coin : Area2D
{
    [Export] public float speed = 500f;
    public PickupKind Kind { get; private set; }
    private AnimatedSprite2D _coin;
    private Sprite2D _rune;
    private Label _badge;
    private LevelGenerator _level;
    private Vector2 _baseScale;
    private float _appearTimer, _clock;
    private const float AppearDuration = 0.3f;
    public override void _Ready()
    {
        GameAssets.Load(); _level = GetParent() as LevelGenerator;
        _coin = GetNodeOrNull<AnimatedSprite2D>("ColorRect") ?? GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_coin != null) { _baseScale = _coin.Scale; _coin.Material = GameAssets.Unshaded; _coin.Play("Spin"); }
        _rune = new Sprite2D { Texture = GameAssets.Shield,Scale = Vector2.One*70f/GameAssets.Shield.GetWidth(),Material = GameAssets.Unshaded };
        AddChild(_rune);
        _badge = new Label { Position = new Vector2(-29,-18),Size = new Vector2(58,36),HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,Material = GameAssets.Unshaded };
        _badge.AddThemeFontSizeOverride("font_size",24); _badge.AddThemeColorOverride("font_color",Colors.White); AddChild(_badge);
        SetKind(PickupKind.Coin);
    }
    public void SetKind(PickupKind kind)
    {
        Kind = kind;
        if (_coin == null) return;
        _coin.Visible = kind == PickupKind.Coin;
        _coin.Scale = _baseScale;
        _rune.Visible = kind != PickupKind.Coin; _badge.Visible = kind != PickupKind.Coin;
        _rune.Material = GameAssets.RuneStyles[kind == PickupKind.DoubleCoins ? 1 : kind == PickupKind.Magnet ? 2 : 0];
        _badge.Text = kind == PickupKind.DoubleCoins ? "×2" : kind == PickupKind.Magnet ? "M" : "◆";
        if (kind == PickupKind.Shield) AddToGroup("Shield"); else if (IsInGroup("Shield")) RemoveFromGroup("Shield");
    }
    public void TriggerAppear(PickupKind kind = PickupKind.Coin)
    {
        SetKind(kind); _appearTimer = 0f; _clock = 0f;
        Scale = Vector2.Zero; Modulate = new Color(1,1,1,0);
    }
    public override void _Process(double delta)
    {
        float dt = (float)delta; _clock += dt;
        Position += new Vector2(0,-speed*dt);
        Player runner = _level?.Runner;
        if (Kind == PickupKind.Coin && runner != null && runner.MagnetActive && Position.DistanceSquaredTo(runner.Position)<220f*220f)
            Position = Position.MoveToward(runner.Position,650f*dt);
        if (Kind != PickupKind.Coin)
        {
            _rune.Rotation = Mathf.Sin(_clock*1.6f)*0.18f;
            _rune.Scale = Vector2.One*(70f/GameAssets.Shield.GetWidth())*(1f+Mathf.Sin(_clock*3f)*0.045f);
        }
        if (_appearTimer < AppearDuration)
        {
            _appearTimer += dt; float t = Mathf.Clamp(_appearTimer/AppearDuration,0,1);
            float ease = 1+2.70158f*Mathf.Pow(t-1,3)+1.70158f*Mathf.Pow(t-1,2);
            Scale = Vector2.One*Mathf.Clamp(ease,0,1.2f); Modulate = new Color(1,1,1,t);
        }
        if (Position.Y < -560f) { SetDeferred("monitorable",false); Hide(); ProcessMode = ProcessModeEnum.Disabled; }
    }
}
