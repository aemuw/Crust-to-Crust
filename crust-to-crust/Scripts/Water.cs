using Godot;

public partial class Water : Area2D
{
	[Export] public float speed = 500f;
	public const float CaveHeight = 480f;
	public const float HalfHeight = CaveHeight * 0.5f;

	private AnimatedSprite2D _waterfallSprite;
	private CollisionShape2D _waterShape;
	private bool _fromLeft;
    private LevelGenerator _level;
    private PointLight2D _waterLight;
    public bool FromLeft => _fromLeft;
    public bool IsThermalCapsule { get; private set; }
    public Vector2 StreamCenter => GlobalPosition + (_waterShape?.Position ?? Vector2.Zero);

	public override void _Ready()
	{
		_waterfallSprite = GetNodeOrNull<AnimatedSprite2D>("WaterfallSprite");
		_waterShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        GameAssets.Load();
        _level = GetParent() as LevelGenerator;
        _level?.RegisterWater(this);
        if (_waterfallSprite != null)
        {
            _waterfallSprite.SpriteFrames = GameAssets.WaterFrames;
            _waterfallSprite.Material = GameAssets.WaterMaterial;
            _waterfallSprite.Scale = Vector2.One*(225f/GameAssets.Waterfall.GetWidth());
        }
        _waterLight = CaveLighting.Light(new Color("83d6e7"),0.55f,0.28f,false);
        AddChild(_waterLight);
        
        if (_waterShape != null) _waterShape.Shape = new RectangleShape2D { Size = new Vector2(70,330) };
		GenerateRandomCaveGeometry();

		if (_waterfallSprite != null)
		{
			_waterfallSprite.Play("flow");
			_waterfallSprite.Frame = 0;
			_waterfallSprite.SpeedScale = (float)GD.RandRange(0.92, 1.12);
		}
	}

	// Назву методу залишено для сумісності з пулом LevelGenerator.
	// Тепер він лише ставить готовий анімований водоспад біля випадкової стіни.
	public void GenerateRandomCaveGeometry(bool? fromLeft = null)
	{
        IsThermalCapsule = _level != null && Mathf.Abs(_level.CurrentDepth+1450f-_level.CoreDepthPoint) <= 8000f;
        if (_waterfallSprite != null)
        {
            _waterfallSprite.SpriteFrames = IsThermalCapsule ? GameAssets.CoolantFrames : GameAssets.WaterFrames;
            _waterfallSprite.Material = IsThermalCapsule ? GameAssets.CoolantMaterial : GameAssets.WaterMaterial;
            Texture2D texture = IsThermalCapsule ? GameAssets.Coolant : GameAssets.Waterfall;
            _waterfallSprite.Scale = new Vector2(225f/texture.GetWidth(),338f/texture.GetHeight());
            _waterfallSprite.Play("flow");
        }
        if (_waterLight != null) _waterLight.Color = IsThermalCapsule ? new Color("ffe0b0") : new Color("83d6e7");
		_fromLeft = fromLeft ?? ((_level?.Director.NextInt(0,2) ?? GD.RandRange(0,1)) == 0);
        AlignToWall();
        if (_waterfallSprite != null)
        {
            _waterfallSprite.FlipH = !_fromLeft;
            _waterfallSprite.Frame = 0;
        }
    }
    private void AlignToWall()
    {
        float left = -280f, right = 280f;
        _level?.GetRockBounds(Position.Y - 190f,out left,out right);
        float anchor = _fromLeft ? left + 78f : right - 78f;
        if (_waterfallSprite != null) _waterfallSprite.Position = new Vector2(anchor,0f);
        Vector2 stream = new Vector2(anchor + (_fromLeft ? 11f : -11f),10f);
        if (_waterShape != null) _waterShape.Position = stream;
        if (_waterLight != null) _waterLight.Position = stream;
    }

	public void UpdateBiomeColors(Color wallColor, Color borderColor)
	{
		if (_waterfallSprite == null) return;

		float heat = Mathf.Clamp(wallColor.R * 1.35f, 0f, 0.35f);
		_waterfallSprite.Modulate = new Color(
			1f,
			1f - heat * 0.16f,
			1f - heat * 0.08f,
			1f);
	}

	public override void _Process(double delta)
	{
		Position += new Vector2(0f, -speed * (float)delta);
        AlignToWall();
		if (CaveViewport.PassedTop(this,HalfHeight+100))
		{
			SetDeferred("monitorable", false);
			Visible = false;
			ProcessMode = ProcessModeEnum.Disabled;
		}
	}
}
