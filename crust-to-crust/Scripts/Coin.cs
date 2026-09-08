using Godot;

public partial class Coin : Area2D
{
	[Export] public float speed = 500f;
	
	private AnimatedSprite2D _animSprite;
	private float _appearTimer = 0f;
	private const float AppearDuration = 0.3f;

	public void TriggerAppear()
	{
		Scale = Vector2.Zero;
		Modulate = new Color(1f, 1f, 1f, 0f);
		_appearTimer = 0f;
	}

	public override void _Ready()
	{
		_animSprite = GetNodeOrNull<AnimatedSprite2D>("ColorRect") ?? GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_animSprite?.Play("Spin");
	}

	public override void _Process(double delta)
	{
		Position += new Vector2(0, -speed * (float)delta);

		if (_appearTimer < AppearDuration)
		{
			_appearTimer += (float)delta;
			float t = Mathf.Clamp(_appearTimer / AppearDuration, 0f, 1f);
			// Плавне масштабування та виринання монетки
			float easeOutBack = 1f + 1.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
			Scale = Vector2.One * Mathf.Clamp(easeOutBack, 0f, 1.2f);
			Modulate = new Color(1f, 1f, 1f, t);
		}
		
		if (Position.Y < -500f)
		{
			Visible = false;
			ProcessMode = ProcessModeEnum.Disabled;
		}
	}
}
