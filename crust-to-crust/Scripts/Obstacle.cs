using Godot;

public partial class Obstacle : Area2D
{
	[Export] public float speed = 500f;
	
	private Vector2 _targetScale = Vector2.One;
	private float _appearTimer = 0f;
	private const float AppearDuration = 0.35f;

	public void TriggerAppear(Vector2 targetScale)
	{
		_targetScale = targetScale;
		Scale = Vector2.Zero;
		Modulate = new Color(1f, 1f, 1f, 0f);
		_appearTimer = 0f;
	}

	public override void _Process(double delta)
	{
		Position += new Vector2(0, -speed * (float)delta);

		if (_appearTimer < AppearDuration)
		{
			_appearTimer += (float)delta;
			float t = Mathf.Clamp(_appearTimer / AppearDuration, 0f, 1f);
			// Плавна функція появи з легким відскоком (overshoot)
			float easeOutBack = 1f + 1.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
			Scale = _targetScale * Mathf.Clamp(easeOutBack, 0f, 1.15f);
			Modulate = new Color(1f, 1f, 1f, t);
		}
		
		if (Position.Y < -500f)
		{
			Visible = false;
			ProcessMode = ProcessModeEnum.Disabled;
		}
	}
}
