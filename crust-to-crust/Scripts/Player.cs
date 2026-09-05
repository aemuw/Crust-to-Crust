using Godot;

public partial class Player : CharacterBody2D
{
	// 4 фіксовані смуги руху відносно нуля
	private readonly float[] _laneOffsets = { -225f, -75f, 75f, 225f };
	
	private int _currentLaneIndex = 1;

	[Export] public float MoveSpeed = 16f;
	[Export] public float FixedY = -100f;

	public override void _Ready()
	{
		// Ставимо гравця на стартову лінію без жодних додаткових обчислень центру
		Position = new Vector2(_laneOffsets[_currentLaneIndex], FixedY);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
			return;

		if (keyEvent.Keycode == Key.Left || keyEvent.Keycode == Key.A)
		{
			if (_currentLaneIndex > 0)
			{
				_currentLaneIndex--;
			}
		}
		else if (keyEvent.Keycode == Key.Right || keyEvent.Keycode == Key.D)
		{
			if (_currentLaneIndex < _laneOffsets.Length - 1)
			{
				_currentLaneIndex++;
			}
		}
	}

	public override void _Process(double delta)
	{
		float targetX = _laneOffsets[_currentLaneIndex];
		float newX = Mathf.Lerp(Position.X, targetX, (float)delta * MoveSpeed);

		Position = new Vector2(newX, FixedY);
	}
}
