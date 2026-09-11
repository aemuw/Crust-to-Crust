using Godot;

public partial class Player : CharacterBody2D
{
    [Export] public float HorizontalSpeed = 520f;
    [Export] public float HorizontalDamping = 12f;
    [Export] public float FixedY = -200f;
    [Export] public float MinX = -310f;
    [Export] public float MaxX = 310f;

    private float _currentVelocityX = 0f;

    [Export] public float MinBurnTime = 14f;
    [Export] public float MaxBurnTime = 16f;

    private bool _isOnFire = false;
    private float _burnTimer = 0f;
    private float _currentBurnDuration = 0f;

    private bool _isDead = false;
    private bool _isGameStarted = false;
    private bool _isIntroPlaying = false;

    private Camera2D _camera;
    private ColorRect _visualRect;
    private Sprite2D _characterSprite;
    private ColorRect _fireGlow;
    private CpuParticles2D _fireParticles;
    private CpuParticles2D _smokeParticles;
    private CpuParticles2D _debrisParticles;
    private Hud _hud;
    private int _score = 0;
    private LevelGenerator _levelGenerator;
    private AudioStreamPlayer _coinSfx;
    private AudioStreamPlayer _deathSfx;
    private AudioStreamPlayer _waterSfx;

    private void InitSoundEffects()
    {
        // Завантажуємо додані якісні звукові файли
        _coinSfx.Stream = GD.Load<AudioStream>("res://Bright_sparkling_ret_#2-1788868577811.wav");
        _deathSfx.Stream = GD.Load<AudioStream>("res://Heavy_rock_smash_col_#4-1788868557120.wav");
        _waterSfx.Stream = GD.Load<AudioStream>("res://Sizzling_steam_hiss__#3-1788868531789.wav");
    }

    public override void _Ready()
    {
        _visualRect = GetNodeOrNull<ColorRect>("ColorRect");
        _characterSprite = GetNodeOrNull<Sprite2D>("CharacterSprite");
        _fireGlow = GetNodeOrNull<ColorRect>("FireGlow");
        _fireParticles = GetNodeOrNull<CpuParticles2D>("FireParticles");
        _smokeParticles = GetNodeOrNull<CpuParticles2D>("SmokeParticles");
        _debrisParticles = GetNodeOrNull<CpuParticles2D>("DebrisParticles");

        _coinSfx = GetNodeOrNull<AudioStreamPlayer>("CoinSfx");
        if (_coinSfx == null)
        {
            _coinSfx = new AudioStreamPlayer();
            _coinSfx.Name = "CoinSfx";
            AddChild(_coinSfx);
        }

        _deathSfx = GetNodeOrNull<AudioStreamPlayer>("DeathSfx");
        if (_deathSfx == null)
        {
            _deathSfx = new AudioStreamPlayer();
            _deathSfx.Name = "DeathSfx";
            AddChild(_deathSfx);
        }

        _waterSfx = GetNodeOrNull<AudioStreamPlayer>("WaterSfx");
        if (_waterSfx == null)
        {
            _waterSfx = new AudioStreamPlayer();
            _waterSfx.Name = "WaterSfx";
            AddChild(_waterSfx);
        }

        InitSoundEffects();

        Area2D hitbox = GetNodeOrNull<Area2D>("Hitbox");
        if (hitbox != null)
        {
            hitbox.AreaEntered += OnAreaEntered;
        }

        _hud = GetNodeOrNull<Hud>("../HUD");
        _levelGenerator = GetNodeOrNull<LevelGenerator>("..");
        _camera = GetNodeOrNull<Camera2D>("../Camera2D");

        float surfaceY = -450f - 16f; // На поверхні землі зліва над шахтою
        Position = new Vector2(-600f, surfaceY);
        Rotation = 0f;
        Scale = Vector2.One;

        if (_camera != null)
        {
            _camera.Position = new Vector2(0f, surfaceY);
        }

        SetProcessUnhandledKeyInput(false);

        if (_hud != null)
        {
            _hud.StartRequested += OnStartGameRequested;
        }
    }

    private void OnStartGameRequested()
    {
        if (_isGameStarted) return;
        _isGameStarted = true;
        PlayIntroAnimation();
    }

    private void PlayIntroAnimation()
    {
        _isIntroPlaying = true;
        SetProcessUnhandledKeyInput(false);

        float surfaceY = -450f - 16f;
        Position = new Vector2(-600f, surfaceY);
        Rotation = 0f;
        Scale = Vector2.One;

        if (_camera != null)
        {
            _camera.Position = new Vector2(0f, surfaceY);
        }

        var tween = CreateTween();
        
        // 1. Ковзання по поверхні до краю розлому (-300) з легкою віддачею
        tween.TweenProperty(this, "position:x", -310f, 0.65f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        
        // 2. Високий динамічний стрибок у розлом з повним переворотом
        float targetStartX = 0f; // плавний стрибок у центр тунелю
        tween.TweenProperty(this, "position:y", surfaceY - 140f, 0.35f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(this, "position:x", targetStartX, 0.75f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
        tween.Parallel().TweenProperty(this, "rotation", Mathf.DegToRad(360f), 0.75f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);

        // 3. Занурення/падіння вниз углиб шахти на позицію польоту (FixedY)
        // Камера плавно слідує за кубиком до центру екрану (0, 0)
        tween.TweenProperty(this, "position:y", FixedY, 0.45f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        if (_camera != null)
        {
            tween.Parallel().TweenProperty(_camera, "position:y", 0f, 0.65f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        }

        // 4. Легке пружне приземлення (squash & stretch ефект)
        tween.TweenProperty(this, "scale", new Vector2(1.2f, 0.85f), 0.1f);
        tween.TweenProperty(this, "scale", Vector2.One, 0.15f);

        tween.TweenCallback(Callable.From(() =>
        {
            Rotation = 0f;
            Position = new Vector2(targetStartX, FixedY);
            _currentVelocityX = 0f;
            if (_camera != null) _camera.Position = Vector2.Zero;
            _isIntroPlaying = false;
            SetProcessUnhandledKeyInput(true);
            _levelGenerator?.BeginFalling();
        }));
    }

    /// <summary>
    /// Анімація вильоту з іншого кінця планети в космос/небо, розвороту у невагомості та плавного нового падіння назад
    /// </summary>
    public void PlayEmergenceAnimation(System.Action onComplete)
    {
        _isIntroPlaying = true;
        _currentVelocityX = 0f;
        SetProcessUnhandledKeyInput(false);

        var tween = CreateTween();
        
        // 1. Потужний виліт вгору крізь поверхню у невагомість
        tween.TweenProperty(this, "position:y", -520f, 0.9f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(this, "position:x", 0f, 0.7f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(this, "rotation", Mathf.DegToRad(360f), 0.9f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        // 2. Зависання в апогеї польоту (невагомість)
        tween.TweenInterval(0.35f);

        // 3. Плавний переворот носом донизу і початок нового вільного падіння назад у шахту
        tween.TweenProperty(this, "rotation", Mathf.DegToRad(720f), 0.7f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(this, "position:y", FixedY, 0.7f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

        // 4. Легкий пружний відскок при вході в стаціонарну позицію
        tween.TweenProperty(this, "scale", new Vector2(1.15f, 0.85f), 0.1f);
        tween.TweenProperty(this, "scale", Vector2.One, 0.15f);

        tween.TweenCallback(Callable.From(() =>
        {
            Rotation = 0f;
            Position = new Vector2(0f, FixedY);
            _currentVelocityX = 0f;
            _isIntroPlaying = false;
            SetProcessUnhandledKeyInput(true);
            onComplete?.Invoke();
        }));
    }

    public override void _Process(double delta)
    {
        if (!_isGameStarted || _isIntroPlaying) return;

        _hud?.UpdateDepth(_levelGenerator?.CurrentDepth ?? 0f);

        // Плавне аналогове керування рухом вліво/вправо (A/D, стрілочки)
        float moveInput = 0f;
        if (!_isDead)
        {
            if (Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.A))
                moveInput -= 1f;
            if (Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D))
                moveInput += 1f;
        }

        // Плавне прискорення та гальмування (damping)
        float targetVelocityX = moveInput * HorizontalSpeed;
        _currentVelocityX = Mathf.Lerp(_currentVelocityX, targetVelocityX, (float)delta * HorizontalDamping);

        // Оновлюємо горизонтальну позицію кубика з м'якими межами тунелю
        float newX = Position.X + _currentVelocityX * (float)delta;
		float dynamicMinX = MinX;
		float dynamicMaxX = MaxX;
		if (_levelGenerator != null)
		{
			_levelGenerator.GetTunnelBounds(FixedY, out float leftEdge, out float rightEdge);
			dynamicMinX = leftEdge + 34f;
			dynamicMaxX = rightEdge - 34f;
		}
        newX = Mathf.Clamp(newX, dynamicMinX, dynamicMaxX);
        Position = new Vector2(newX, FixedY);

		// Камера трохи випереджає горизонтальний рух, нахиляється та віддаляється
		// на великій швидкості падіння. Значення навмисно стримані, щоб не нудило.
		if (_camera != null && !_isDead)
		{
			float horizontalRatio = HorizontalSpeed > 0f ? _currentVelocityX / HorizontalSpeed : 0f;
			float fallRatio = _levelGenerator != null ? Mathf.Clamp(_levelGenerator.FallSpeed / 1200f, 0f, 1f) : 0f;
			Vector2 targetOffset = new Vector2(horizontalRatio * 34f, fallRatio * 16f);
			_camera.Offset = _camera.Offset.Lerp(targetOffset, Mathf.Clamp((float)delta * 4.5f, 0f, 1f));
			_camera.Rotation = Mathf.Lerp(_camera.Rotation, -horizontalRatio * 0.022f, Mathf.Clamp((float)delta * 3.8f, 0f, 1f));
			float zoomAmount = Mathf.Lerp(1.0f, 0.94f, fallRatio);
			_camera.Zoom = _camera.Zoom.Lerp(new Vector2(zoomAmount, zoomAmount), Mathf.Clamp((float)delta * 2.2f, 0f, 1f));
		}

        // Динамічний плавний нахил (tilt) кубика залежно від поточної швидкості
        if (!_isDead)
        {
            float targetTilt = (_currentVelocityX / HorizontalSpeed) * 0.16f;
            Rotation = Mathf.Lerp(Rotation, targetTilt, (float)delta * 14f);
        }

        // Плавний нагрів при наближенні до ядра
        UpdateHeatingProcess((float)delta);
    }

    private void UpdateHeatingProcess(float delta)
    {
        if (_levelGenerator == null || _isDead) return;

        float distToCore = Mathf.Abs(_levelGenerator.CurrentDepth - _levelGenerator.CoreDepthPoint);

        // 1. Попередній нагрів: коли підлітаємо ближче 4500м, кубик поступово червоніє
        if (distToCore < 4500f && !_isOnFire)
        {
            float heatFactor = Mathf.Clamp(1f - (distToCore / 4500f), 0f, 1f);
            if (_visualRect != null)
            {
                _visualRect.Color = new Color(0.65f, 0.65f, 0.65f).Lerp(new Color(1f, 0.25f, 0.1f), heatFactor);
            }
			if (_characterSprite != null)
				_characterSprite.Modulate = Colors.White.Lerp(new Color(1.25f, 0.55f, 0.28f), heatFactor * 0.75f);
            // Ближче 2000м від ядра кубик остаточно займається полум'ям
            if (distToCore < 2000f)
            {
                CatchFire();
            }
        }
        else if (!_isOnFire && _visualRect != null)
        {
            _visualRect.Color = new Color(0.65f, 0.65f, 0.65f);
            if (_characterSprite != null) _characterSprite.Modulate = Colors.White;
        }

        // 2. Фаза горіння: кубик поступово зменшується від плавлення/згорання
        if (_isOnFire)
        {
            _burnTimer += delta;
            _hud?.SetHeatWarning(true);

            float burnProgress = Mathf.Clamp(_burnTimer / _currentBurnDuration, 0f, 1f);
            // Розмір кубика поступово зменшується з 1.0 до 0.45 від горіння
            float currentScale = Mathf.Lerp(1.0f, 0.45f, burnProgress);
            Scale = new Vector2(currentScale, currentScale);

            // Пульсуючий розпечений колір
            if (_visualRect != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(_burnTimer * 14f);
                _visualRect.Color = new Color(1f, 0.15f + pulse * 0.25f, 0.05f);
                if (_characterSprite != null)
                {
                    _characterSprite.Modulate = new Color(1.25f, 0.55f + pulse * 0.35f, 0.25f);
                    _characterSprite.Rotation = Mathf.Sin(_burnTimer * 19f) * 0.035f;
                }
            }

            // Повне згорання -> спалах і смерть
            if (_burnTimer >= _currentBurnDuration)
            {
                Die();
            }
        }
        else
        {
            _hud?.SetHeatWarning(false);
            Scale = Vector2.One;
        }
    }

    private void OnAreaEntered(Area2D area)
    {
        if (_isDead || _isIntroPlaying || !_isGameStarted) return;

        // Obstacle (камінець) - вбиває одразу
        if (area.CollisionLayer == 2 || area.IsInGroup("Obstacle") || area.Name.ToString().Contains("Obstacle"))
        {
            Die();
            return;
        }

        // Coin (монетка) - збір монетки
        if (area.CollisionLayer == 8 || area.CollisionLayer == 16 || area.IsInGroup("Coin") || area.Name.ToString().Contains("Coin"))
        {
            _score++;
            _hud?.UpdateScore(_score);
            _coinSfx?.Play();
            area.Visible = false;
            area.SetDeferred("process_mode", (int)Node.ProcessModeEnum.Disabled);
            return;
        }

        // Water (вода / печера з водою) - гасить вогонь та повністю охолоджує
        if (area.CollisionLayer == 4 || area.IsInGroup("Water") || area.Name.ToString().Contains("Water"))
        {
            Extinguish();
            return;
        }
    }

    private void CatchFire()
    {
        if (_isOnFire || _isDead) 
            return;

        _isOnFire = true;
        _burnTimer = 0f;
        _currentBurnDuration = (float)GD.RandRange(MinBurnTime, MaxBurnTime);

        // Візуальний ефект палаючого кубика: розпечене серце, ореол та дим
        if (_visualRect != null)
            _visualRect.Color = new Color(1f, 0.35f, 0.05f);
        if (_fireGlow != null)
            _fireGlow.Visible = true;
        if (_fireParticles != null)
            _fireParticles.Emitting = true;
        if (_smokeParticles != null)
            _smokeParticles.Emitting = true;
        if (_characterSprite != null)
        {
            var ignitionTween = CreateTween();
            ignitionTween.TweenProperty(_characterSprite, "scale", new Vector2(0.054f, 0.054f), 0.10f);
            ignitionTween.TweenProperty(_characterSprite, "scale", new Vector2(0.048f, 0.048f), 0.18f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
    }

    private void Extinguish()
    {
        if (!_isOnFire && Scale == Vector2.One) 
            return;

        _waterSfx?.Play();

        _isOnFire = false;
        _burnTimer = 0f;

        // Повернення нормального вигляду та відновлення форми
        Scale = Vector2.One;
        if (_visualRect != null)
            _visualRect.Color = new Color(0.65f, 0.65f, 0.65f);
        if (_characterSprite != null)
        {
            _characterSprite.Modulate = Colors.White;
            _characterSprite.Rotation = 0f;
            _characterSprite.Scale = new Vector2(0.048f, 0.048f);
        }
        if (_fireGlow != null)
            _fireGlow.Visible = false;
        if (_fireParticles != null)
            _fireParticles.Emitting = false;
        if (_smokeParticles != null)
            _smokeParticles.Emitting = false;

        // Ефект повного охолодження при вильоті з води
        var coolTween = CreateTween();
        coolTween.TweenProperty(this, "modulate", new Color(0.2f, 0.85f, 1f), 0.15f);
        coolTween.TweenProperty(this, "modulate", Colors.White, 0.3f);
    }

    private void Die()
    {
        if (_isDead) return;

        _isDead = true;
        _isOnFire = false;
        
        SetProcessUnhandledKeyInput(false);

        _deathSfx?.Play();

        if (_fireGlow != null) _fireGlow.Visible = false;
        if (_fireParticles != null) _fireParticles.Emitting = false;
        if (_smokeParticles != null) _smokeParticles.Emitting = false;
        CreateDeathFragments();
        if (_characterSprite != null) _characterSprite.Visible = false;

        // Вибух уламків кубика
        if (_debrisParticles != null)
        {
            _debrisParticles.Emitting = true;
        }

        // Кінематографічний шейк камери від удару
        if (_camera != null)
        {
            var shakeTween = CreateTween();
            shakeTween.TweenProperty(_camera, "offset", new Vector2(GD.RandRange(-18, 18), GD.RandRange(-18, 18)), 0.04f);
            shakeTween.TweenProperty(_camera, "offset", new Vector2(GD.RandRange(-10, 10), GD.RandRange(-10, 10)), 0.05f);
            shakeTween.TweenProperty(_camera, "offset", new Vector2(GD.RandRange(-4, 4), GD.RandRange(-4, 4)), 0.06f);
            shakeTween.TweenProperty(_camera, "offset", Vector2.Zero, 0.08f);
        }

        // Ефектна анімація руйнування кубика
        Tween deathTween = CreateTween();
        deathTween.SetParallel(true);
        
        // 1. Різкий сплюснутий імпульс
        deathTween.TweenProperty(this, "scale", new Vector2(2.0f, 0.2f), 0.07f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        
        // 2. Спалах розлому
        if (_visualRect != null)
        {
            deathTween.TweenProperty(_visualRect, "color", new Color(1f, 0.9f, 0.6f), 0.07f);
        }

        // 3. Розпад та зникнення
        deathTween.Chain().SetParallel(true);
        deathTween.TweenProperty(this, "scale", Vector2.Zero, 0.35f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        deathTween.TweenProperty(this, "rotation", Mathf.DegToRad(720f), 0.35f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        
        if (_visualRect != null)
        {
            deathTween.TweenProperty(_visualRect, "color", new Color(0.9f, 0.1f, 0.05f, 0f), 0.35f);
        }

        deathTween.Chain().TweenInterval(0.35f);
        deathTween.Chain().TweenCallback(Callable.From(() =>
        {
            GetTree().Paused = true;
            _hud?.ShowGameOver();
        }));
    }

    private void CreateDeathFragments()
    {
        if (_characterSprite?.Texture == null || GetParent() == null) return;

        Vector2 textureSize = _characterSprite.Texture.GetSize();
        Vector2 cellSize = textureSize / 3f;
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                var shard = new Sprite2D
                {
                    Texture = _characterSprite.Texture,
                    RegionEnabled = true,
                    RegionRect = new Rect2(new Vector2(column * cellSize.X, row * cellSize.Y), cellSize),
                    Position = Position + new Vector2((column - 1) * 21f, (row - 1) * 21f),
                    Scale = _characterSprite.Scale * Scale,
                    Modulate = new Color(1.2f, 0.55f, 0.22f, 1f),
                    ZIndex = 20
                };
                GetParent().AddChild(shard);

                Vector2 direction = new Vector2(column - 1f, row - 1f).Normalized();
                if (direction == Vector2.Zero) direction = Vector2.Up;
                Vector2 target = shard.Position + direction * (90f + GD.Randf() * 85f) + new Vector2(0f, 70f);
                var shardTween = shard.CreateTween().SetParallel(true);
                shardTween.TweenProperty(shard, "position", target, 0.62f)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                shardTween.TweenProperty(shard, "rotation", (float)GD.RandRange(-5.0, 5.0), 0.62f);
                shardTween.TweenProperty(shard, "modulate:a", 0f, 0.62f).SetDelay(0.20f);
                shardTween.Chain().TweenCallback(Callable.From(shard.QueueFree));
            }
        }
    }
}
