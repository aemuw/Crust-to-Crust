using Godot;

public partial class Water : Area2D
{
	[Export] public float speed = 500f;
	public const float CaveHeight = 640f;
	public const float HalfHeight = CaveHeight * 0.5f;

	private AnimatedSprite2D _waterfallSprite;
	private CollisionShape2D _waterShape;
	private bool _fromLeft;

	public override void _Ready()
	{
		_waterfallSprite = GetNodeOrNull<AnimatedSprite2D>("WaterfallSprite");
		_waterShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		GenerateRandomCaveGeometry();

		if (_waterfallSprite != null)
		{
			_waterfallSprite.Play("flow");
			_waterfallSprite.Frame = GD.RandRange(0, 7);
			_waterfallSprite.SpeedScale = (float)GD.RandRange(0.92, 1.12);
		}
	}

	// Назву методу залишено для сумісності з пулом LevelGenerator.
	// Тепер він лише ставить готовий анімований водоспад біля випадкової стіни.
	public void GenerateRandomCaveGeometry()
	{
		_fromLeft = GD.RandRange(0, 1) == 0;
		float side = _fromLeft ? -1f : 1f;

		if (_waterfallSprite != null)
		{
			// Скеля-джерело заходить у стіну, а сама вода виступає в прохід.
			_waterfallSprite.Position = new Vector2(side * 244f, 0f);
			_waterfallSprite.FlipH = !_fromLeft;
			_waterfallSprite.Frame = GD.RandRange(0, 7);
		}

		if (_waterShape != null)
		{
			// Колізія охоплює лише струмінь, без прозорих країв спрайта.
			_waterShape.Position = new Vector2(side * 224f, 18f);
		}
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
		if (Position.Y < -1050f)
		{
			Visible = false;
			ProcessMode = ProcessModeEnum.Disabled;
		}
	}
}
