using Godot;

public partial class Player : CharacterBody2D
{
    private readonly float[] _lanes = { -300f, -100f, 100f, 300f };
    private int _currentLaneIndex = 1;

    [Export] public float MoveSpeed = 16f;
    [Export] public float FixedY = -200f;

    [Export] public float MinBurnTime = 14f;
    [Export] public float MaxBurnTime = 16f;

    private bool _isOnFire = false;
    private float _burnTimer = 0f;
    private float _currentBurnDuration = 0f;

    private ColorRect _visualRect;

    public override void _Ready()
    {
        Position = new Vector2(_lanes[_currentLaneIndex], FixedY);
        _visualRect = GetNodeOrNull<ColorRect>("ColorRect");

        Area2D hitbox = GetNodeOrNull<Area2D>("Hitbox");
        if (hitbox != null)
        {
            hitbox.AreaEntered += OnAreaEntered;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        if (keyEvent.Keycode == Key.Left || keyEvent.Keycode == Key.A)
        {
            if (_currentLaneIndex > 0)
                _currentLaneIndex--;
        }
        else if (keyEvent.Keycode == Key.Right || keyEvent.Keycode == Key.D)
        {
            if (_currentLaneIndex < _lanes.Length - 1)
                _currentLaneIndex++;
        }
    }

    public override void _Process(double delta)
    {
        float targetX = _lanes[_currentLaneIndex];
        float newX = Mathf.Lerp(Position.X, targetX, (float)delta * MoveSpeed);
        Position = new Vector2(newX, FixedY);

        if (_isOnFire)
        {
            _burnTimer += (float)delta;
            if (_burnTimer >= _currentBurnDuration)
            {
                Die();
            }
        }
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area.CollisionLayer == 2 || area.IsInGroup("Obstacle"))
        {
            CatchFire();
            area.QueueFree();
        }
        else if (area.CollisionLayer == 4 || area.IsInGroup("Water"))
        {
            Extinguish();
        }
    }

    private void CatchFire()
    {
        if (_isOnFire) return;

        _isOnFire = true;
        _burnTimer = 0f;
        _currentBurnDuration = (float)GD.RandRange(MinBurnTime, MaxBurnTime);

        if (_visualRect != null)
            _visualRect.Color = new Color(1f, 0.3f, 0.2f);

        GD.Print($"Гравець загорівся! Час до смерті: {_currentBurnDuration:F1} сек.");
    }

    private void Extinguish()
    {
        if (!_isOnFire) return;

        _isOnFire = false;
        _burnTimer = 0f;

        if (_visualRect != null)
            _visualRect.Color = new Color(1f, 1f, 1f);

        GD.Print("Вогонь погашено!");
    }

    private void Die()
{
    _isOnFire = false;
    
    // Блокуємо зчитування клавіш руху
    SetProcessUnhandledKeyInput(false);

    // Зупиняємо весь час у грі: спавнер, перешкоди та світ стають на паузу
    GetTree().Paused = true;

    // Створюємо твін, який ігнорує глобальну паузу
    Tween deathTween = CreateTween();
    deathTween.SetPauseMode(Tween.TweenPauseMode.Process);
    deathTween.SetParallel(true);

    float duration = 0.7f;

    // Закручуємо кубик навколо своєї осі
    deathTween.TweenProperty(this, "rotation", Mathf.DegToRad(180f), duration)
        .SetTrans(Tween.TransitionType.Back)
        .SetEase(Tween.EaseType.In);

    // Зменшуємо до повного зникнення
    deathTween.TweenProperty(this, "scale", Vector2.Zero, duration)
        .SetTrans(Tween.TransitionType.Quad)
        .SetEase(Tween.EaseType.In);

    // Перетворюємо колір на чорно-сірий попіл
    if (_visualRect != null)
    {
        deathTween.TweenProperty(_visualRect, "color", new Color(0.08f, 0.08f, 0.08f, 0f), duration);
    }

    // Коли анімація смерті завершилася: знімаємо паузу і перезапускаємо сцену
    deathTween.Chain().TweenCallback(Callable.From(() =>
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }));
}
}
