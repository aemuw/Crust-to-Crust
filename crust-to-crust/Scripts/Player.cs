using Godot;

public partial class Player : CharacterBody2D
{
    // 4 фіксовані смуги руху відносно центру
    // Якщо вікно стандартне (наприклад, 1152 або 1280), ці кроки ідеально лягають у видиму зону
    private readonly float[] _laneOffsets = { -225f, -75f, 75f, 225f };
    
    private int _currentLaneIndex = 1;
    private float _screenCenterX;

    [Export] public float MoveSpeed = 16f;
    [Export] public float FixedY = 0f;

    public override void _Ready()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        _screenCenterX = viewportSize.X / 2f;

        if (Mathf.IsZeroApprox(FixedY))
        {
            FixedY = viewportSize.Y / 3f;
        }

        Position = new Vector2(_screenCenterX + _laneOffsets[_currentLaneIndex], FixedY);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        // Вліво: стрілка вліво або A
        if (keyEvent.Keycode == Key.Left || keyEvent.Keycode == Key.A)
        {
            if (_currentLaneIndex > 0)
            {
                _currentLaneIndex--;
                GD.Print("Поточна лінія: ", _currentLaneIndex);
            }
        }
        // Вправо: стрілка вправо або D
        else if (keyEvent.Keycode == Key.Right || keyEvent.Keycode == Key.D)
        {
            if (_currentLaneIndex < _laneOffsets.Length - 1)
            {
                _currentLaneIndex++;
                GD.Print("Поточна лінія: ", _currentLaneIndex);
            }
        }
    }

    public override void _Process(double delta)
    {
        float targetX = _screenCenterX + _laneOffsets[_currentLaneIndex];
        float newX = Mathf.Lerp(Position.X, targetX, (float)delta * MoveSpeed);

        Position = new Vector2(newX, FixedY);
    }
}
