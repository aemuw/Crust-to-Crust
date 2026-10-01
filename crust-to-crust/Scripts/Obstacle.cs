using Godot;

public partial class Obstacle : Area2D
{
	[Export] public float speed = 500f;
	
	private Vector2 _targetScale = Vector2.One;
	private float _appearTimer = 0f;
	private const float AppearDuration = 0.35f;

    private Sprite2D _sprite;
    private float _driftPhase;
    private float _anchorX;
    private int _variant;
    public float MotionDifficulty { get; set; }
    public void ConfigureVariant(int variant)
    {
        _variant = variant;
        _sprite ??= GetNode<Sprite2D>("RockSprite");
        var atlas = GD.Load<Texture2D>("res://Textures/Generated/hazards_atlas.png");
        float cell = atlas.GetWidth() / 3f;
        _sprite.Texture = new AtlasTexture { Atlas = atlas, Region = new Rect2(cell * variant, 0, cell, atlas.GetHeight()) };
        _sprite.Scale = Vector2.One * (variant == 1 ? 0.14f : (variant == 2 ? 0.20f : 0.12f));
        ZIndex = variant == 2 ? -7 : 0;
        var collision = GetNode<CollisionShape2D>("CollisionShape2D");
        collision.Shape = variant == 1
            ? new RectangleShape2D { Size = new Vector2(26f, 72f) }
            : new CircleShape2D { Radius = variant == 2 ? 49f : 29f };
        Rotation = variant == 1 ? 0.15f : (float)GD.RandRange(-0.4, 0.4);
        _anchorX = Position.X;
        _driftPhase = 0f;
        _sprite.Rotation = 0f;
        _sprite.Material = variant == 2 ? new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/CaveCrystal.gdshader") } : null;
    }

	public void TriggerAppear(Vector2 targetScale)
	{
		_targetScale = targetScale;
		Scale = Vector2.Zero;
		Modulate = new Color(1f, 1f, 1f, 0f);
		_appearTimer = 0f;
        _anchorX = Position.X;
	}

	public override void _Process(double delta)
	{
		// Boulder: rolls slowly and sinks against the scroll. Shard: sways.
        // Crystal cluster: drifts laterally with a gentle vertical oscillation.
        float scrollRatio = _variant == 0 ? 0.88f : (_variant == 1 ? 1.08f : 1f);
        Position += new Vector2(0, -speed * scrollRatio * (float)delta);
        if (_appearTimer == 0f) _anchorX = Position.X;
        _driftPhase += (float)delta;
        if (speed > 0f && _variant != 2)
        {
            float intensity = Mathf.Lerp(1f, 1.55f, MotionDifficulty);
            float phase = _driftPhase * intensity;
            float amplitude = _variant == 0 ? 95f : 135f;
            float frequency = _variant == 0 ? 0.95f : 1.45f;
            Position = new Vector2(Mathf.Clamp(_anchorX + Mathf.Sin(phase * frequency) * amplitude, -180f, 180f), Position.Y);
            if (_variant == 0) Rotation += 0.75f * intensity * (float)delta;
            else Rotation = Mathf.Sin(phase * 1.65f) * 0.70f;
        }

		if (_appearTimer < AppearDuration)
		{
			_appearTimer += (float)delta;
			float t = Mathf.Clamp(_appearTimer / AppearDuration, 0f, 1f);
			// Плавна функція появи з легким відскоком (overshoot)
			float easeOutBack = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
			Scale = _targetScale * Mathf.Clamp(easeOutBack, 0f, 1.15f);
			Modulate = new Color(1f, 1f, 1f, t);
		}
		
		if (Position.Y < -500f)
		{
			SetDeferred("monitorable", false);
			Visible = false;
			ProcessMode = ProcessModeEnum.Disabled;
		}
	}
}
