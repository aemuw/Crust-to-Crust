using Godot;

public enum DeathCause { Collision, Overheat }

public partial class Player : CharacterBody2D
{
	private enum State { Menu, Intro, Playing, Emerging, Dead }

	[Export] public float HorizontalSpeed = 380f;
	[Export] public float HorizontalDamping = 12f;
	[Export] public float FixedY = -200f;
	[Export] public float MinX = -310f;
	[Export] public float MaxX = 310f;
	[Export] public float MinBurnTime = 22f;
	[Export] public float MaxBurnTime = 26f;

	// Фізичні шари (мають відповідати project.godot -> [layer_names] 2d_physics).
	private const uint LayerObstacle = 1 << 1; // layer_2
	private const uint LayerWater = 1 << 2;    // layer_3
	private const uint LayerCoin = 1 << 3;     // layer_4

	// Зона нагріву (одиниці відстані від ядра).
	private const float HeatStartDistance = 4500f;
	
	private const float WaterCoolingGraceSeconds = 7f;

	private const float SurfaceY = -450f - 16f;   // поверхня землі зліва над шахтою
	private const float WallPadding = 34f;        // відступ від стін тунелю
	private const float NormalSpriteScale = 0.048f;
	private const float ShieldInvulnerableSeconds = 1.2f;

	private State _state = State.Menu;
	private float _currentVelocityX;

	private bool _isOnFire;
	private float _burnTimer;
	private float _coolingGrace;
	private float _currentBurnDuration;
    private float _temperature;
    private ColorRect _flameEffect;
    private ShaderMaterial _flameMaterial;
    private PointLight2D _fireLight;
    private WindTrails _wind;
    private float _fxClock, _angularVelocity, _spriteAngle;
    private float _skinSpriteScale = NormalSpriteScale;
    private AudioStreamPlayer _windSfx;
    private PointLight2D _lantern;
    public float Temperature => _temperature;

	private bool _hasShield;
	private float _invulnerableTimer;
	private Node2D _shieldVisual;
    private Sprite2D _shieldSprite;
    private float _doubleCoinsTimer, _magnetTimer, _visualKick, _thermalUiTimer, _shakeStrength;
    private bool _lampWanted = true;
    private bool _aimWithPad;
    private Vector2 _padAim = Vector2.Down;
    private int _waterExtinguishes;
    public bool MagnetActive => _magnetTimer > 0f;
    public int CoinMultiplier => _doubleCoinsTimer > 0f ? 2 : 1;

	private int _score;

	private Camera2D _camera;
	private Sprite2D _characterSprite;
	private ColorRect _fireGlow;
	private CpuParticles2D _fireParticles;
	private CpuParticles2D _smokeParticles;
	private CpuParticles2D _debrisParticles;
	private Area2D _hitbox;
	private Hud _hud;
	private LevelGenerator _levelGenerator;
	private AudioStreamPlayer _coinSfx;
	private AudioStreamPlayer _deathSfx;
	private AudioStreamPlayer _waterSfx;

	public bool HasShield => _hasShield;

	// ───────────────────────────── Ініціалізація ─────────────────────────────

	public override void _Ready()
	{
		GameAssets.Load();
        InputSetup.Ensure();
		AudioManager.EnsureBuses();

		_characterSprite = GetNodeOrNull<Sprite2D>("CharacterSprite");
		_fireGlow = GetNodeOrNull<ColorRect>("FireGlow");
		_fireParticles = GetNodeOrNull<CpuParticles2D>("FireParticles");
		_smokeParticles = GetNodeOrNull<CpuParticles2D>("SmokeParticles");
		_debrisParticles = GetNodeOrNull<CpuParticles2D>("DebrisParticles");

		_coinSfx = EnsureSfx("CoinSfx", "res://Audio/coin.wav");
		_deathSfx = EnsureSfx("DeathSfx", "res://Audio/death.wav");
		_waterSfx = EnsureSfx("WaterSfx", "res://Audio/steam.wav");

		_hitbox = GetNodeOrNull<Area2D>("Hitbox");
		if (_hitbox != null)
			_hitbox.AreaEntered += OnAreaEntered;

		_hud = GetNodeOrNull<Hud>("../HUD");
		_levelGenerator = GetNodeOrNull<LevelGenerator>("..");
		_camera = GetNodeOrNull<Camera2D>("../Camera2D");

		SaveData.Load();
        ApplySkin();
        BuildThermalVisuals();
        BuildShieldVisual();
		ResetToSurface();

		if (_hud != null)
			_hud.StartRequested += OnStartGameRequested;
	}

	private AudioStreamPlayer EnsureSfx(string nodeName, string path)
	{
		var player = GetNodeOrNull<AudioStreamPlayer>(nodeName);
		if (player == null)
		{
			player = new AudioStreamPlayer { Name = nodeName };
			AddChild(player);
		}
		player.Bus = AudioManager.SfxBus;
		player.Stream = GD.Load<AudioStream>(path);
		return player;
	}

    private void BuildShieldVisual()
    {
        _shieldVisual = new Node2D { Name = "ShieldVisual",Visible = false,ZIndex = 4 };
        _shieldSprite = new Sprite2D { Texture = GameAssets.Shield,Scale = Vector2.One*132f/GameAssets.Shield.GetWidth(),Material = GameAssets.Unshaded };
        _shieldVisual.AddChild(_shieldSprite); AddChild(_shieldVisual);
    }
    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouseMotion mouse && mouse.Relative.LengthSquared() > 0.01f)
        { _aimWithPad = false; if (_state == State.Playing) _hud?.NotifyTutorialLight(); }
        if (input is InputEventJoypadMotion motion && (motion.Axis == JoyAxis.RightX || motion.Axis == JoyAxis.RightY) && Mathf.Abs(motion.AxisValue) > 0.2f)
        { _aimWithPad = true; if (_state == State.Playing) _hud?.NotifyTutorialLight(); }
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (_state != State.Playing || GetTree().Paused) return;
        if (input.IsActionPressed(InputSetup.ToggleLantern) && !input.IsEcho())
        {
            _lampWanted = !_lampWanted; _lantern.Enabled = _lampWanted;
            _hud?.NotifyTutorialLight();
            GetViewport().SetInputAsHandled();
        }
    }

	private void ResetToSurface()
	{
		Position = new Vector2(-600f, SurfaceY);
		Rotation = 0f;
		Scale = Vector2.One;
		if (_camera != null)
			_camera.Position = new Vector2(0f, SurfaceY);
	}

	// ───────────────────────────── Вступ / вильот ─────────────────────────────

	private void OnStartGameRequested()
	{
		if (_state != State.Menu) return;
		ApplySkin();
        _lantern.TextureScale = 0.80f * (1f + SaveData.Lantern * 0.15f);
        _state = State.Intro;
		PlayIntroAnimation();
	}

	private void PlayIntroAnimation()
	{
		ResetToSurface();

		var tween = CreateTween();

		// 1. Ковзання по поверхні до краю розлому з легкою віддачею
		tween.TweenProperty(this, "position:x", -310f, 0.65f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

		// 2. Високий стрибок у розлом з повним переворотом
		const float targetStartX = 0f;
		tween.TweenProperty(this, "position:y", SurfaceY - 140f, 0.35f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(this, "position:x", targetStartX, 0.75f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
		tween.Parallel().TweenProperty(this, "rotation", Mathf.DegToRad(360f), 0.75f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);

		// 3. Падіння в шахту; камера плавно слідує до центру екрана
		tween.TweenProperty(this, "position:y", FixedY, 0.45f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		if (_camera != null)
		{
			tween.Parallel().TweenProperty(_camera, "position:y", 0f, 0.65f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		}

		// 4. Пружне приземлення (squash & stretch)
		tween.TweenProperty(this, "scale", new Vector2(1.2f, 0.85f), 0.1f);
		tween.TweenProperty(this, "scale", Vector2.One, 0.15f);

		tween.TweenCallback(Callable.From(() =>
		{
			Rotation = 0f;
			Position = new Vector2(targetStartX, FixedY);
			_currentVelocityX = 0f;
			if (_camera != null) _camera.Position = Vector2.Zero;
			_state = State.Playing;
			_levelGenerator?.BeginFalling();
		}));
	}

	/// <summary>Виліт з іншого кінця планети, розворот у невагомості та нове падіння.</summary>
	public void PlayEmergenceAnimation(System.Action onComplete)
	{
		_state = State.Emerging;
		_currentVelocityX = 0f;

		Extinguish(false);
		_coolingGrace = 5f;
		Scale = Vector2.One;
		if (_camera != null) { _camera.IgnoreRotation = false; _camera.Offset = Vector2.Zero; }

		var tween = CreateTween();
		// Вихід через нижню поверхню, повільно в апогеї, потім розворот камери.
		tween.TweenProperty(this, "position", new Vector2(0f, 850f), 1.8f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		if (_camera != null)
		{
			tween.Parallel().TweenProperty(_camera, "position", new Vector2(0f, 700f), 1.8f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			tween.Parallel().TweenProperty(_camera, "zoom", new Vector2(0.85f, 0.85f), 1.8f);
		}
		tween.TweenCallback(Callable.From(() => _levelGenerator?.HideTunnelForEmergence()));
		tween.TweenProperty(this, "position:y", 890f, 0.45f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenInterval(_levelGenerator?.SurfaceHangDuration ?? 0.65f);
		if (_camera != null)
			tween.TweenProperty(_camera, "rotation", Mathf.Pi, 1.7f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(this, "rotation", Mathf.Pi, 0.45f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenCallback(Callable.From(() =>
		{
			// Перебазування координат: видима картинка лишається тією самою.
			Position = new Vector2(0f, -890f);
			Rotation = 0f;
			if (_camera != null) { _camera.Position = new Vector2(0f, -700f); _camera.Rotation = 0f; }
			_levelGenerator?.RebaseEmergence();
		}));
		tween.TweenInterval(0.25f);
		tween.TweenProperty(this, "position:y", FixedY, 1.65f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		if (_camera != null)
		{
			tween.Parallel().TweenProperty(_camera, "position", Vector2.Zero, 1.65f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			tween.Parallel().TweenProperty(_camera, "zoom", Vector2.One, 1.65f);
		}
		tween.TweenCallback(Callable.From(() =>
		{
			_currentVelocityX = 0f;
			_state = State.Playing;
			onComplete?.Invoke();
		}));
	}

	// ───────────────────────────── Кадр гри ─────────────────────────────

	public override void _Process(double delta)
	{
		float dt = (float)delta;
        UpdateLantern(dt);
        if (_wind != null) { _wind.GlobalPosition = Vector2.Zero; _wind.GlobalRotation = 0f; }
        _wind?.SetMotion(_levelGenerator?.FallSpeed ?? 0f, _currentVelocityX, _state == State.Playing);
        if (_windSfx != null)
        {
            float ratio = _state == State.Playing ? Mathf.Clamp(((_levelGenerator?.FallSpeed ?? 0f)-180f)/800f,0f,1f) : 0f;
            _windSfx.VolumeDb = Mathf.Lerp(-55f,-16f,ratio);
            _windSfx.PitchScale = Mathf.Lerp(0.7f,1.25f,ratio);
        }
        if (_state != State.Playing) return;
        _fxClock += dt;
        _doubleCoinsTimer = Mathf.Max(0,_doubleCoinsTimer-dt);
        _magnetTimer = Mathf.Max(0,_magnetTimer-dt);
        _visualKick = Mathf.MoveToward(_visualKick,0f,dt*0.8f);
        _hud?.SetBonuses(_doubleCoinsTimer,_magnetTimer);

		_hud?.UpdateDepth(_levelGenerator?.CurrentDepth ?? 0f);
		if (_levelGenerator != null)
			_hud?.UpdateRunStats(_levelGenerator.Lap, _levelGenerator.TotalDistance);

		UpdateMovement(dt);
		UpdateCamera(dt);
		UpdateInvulnerability(dt);
		UpdateHeating(dt);
        UpdateCubeVisuals();
	}

	private void UpdateMovement(float dt)
	{
		// Аналоговий ввід: клавіші A/D, стрілки, D-pad або лівий стік.
		float moveInput = InputSetup.ReadMovement();

		float targetVelocityX = moveInput * HorizontalSpeed;
		_currentVelocityX = Mathf.Lerp(_currentVelocityX, targetVelocityX, 1f - Mathf.Exp(-dt * HorizontalDamping));

		float minX = MinX;
		float maxX = MaxX;
		if (_levelGenerator != null)
		{
			_levelGenerator.GetTunnelBounds(FixedY, out float leftEdge, out float rightEdge);
			minX = leftEdge + WallPadding;
			maxX = rightEdge - WallPadding;
		}
		float newX = Mathf.Clamp(Position.X + _currentVelocityX * dt, minX, maxX);
        if (Mathf.Abs(newX-Position.X) > 0.05f) _hud?.NotifyTutorialMove();
		Position = new Vector2(newX, FixedY);

		// Нахил кубика залежно від швидкості
		float targetTilt = (_currentVelocityX / HorizontalSpeed) * 0.27f;
        Rotation = Mathf.Lerp(Rotation,targetTilt,1f-Mathf.Exp(-dt*6f));
        float fallRatio = Mathf.Clamp((_levelGenerator?.FallSpeed ?? 0f)/760f,0f,1f);
        float targetSpin = (0.34f+(_currentVelocityX / HorizontalSpeed)*1.65f+Mathf.Sin(_fxClock*2.3f)*0.22f)*fallRatio;
        _angularVelocity = Mathf.Lerp(_angularVelocity,targetSpin,1f-Mathf.Exp(-dt*2.8f));
        _spriteAngle += _angularVelocity*dt;
        if (_characterSprite != null) _characterSprite.Rotation = _spriteAngle + Mathf.Sin(_fxClock*3.1f)*0.025f*fallRatio;
	}

	private void UpdateCamera(float dt)
	{
		if (_camera == null) return;

		// Камера випереджає рух, трохи нахиляється й віддаляється на швидкості. Значення стримані.
		float horizontalRatio = HorizontalSpeed > 0f ? _currentVelocityX / HorizontalSpeed : 0f;
		float fallRatio = _levelGenerator != null ? Mathf.Clamp(_levelGenerator.FallSpeed / 980f, 0f, 1f) : 0f;
		float turbulence = Mathf.Clamp(((_levelGenerator?.FallSpeed ?? 0f)-380f)/420f,0f,1f);
        Vector2 vibration = new Vector2(Mathf.Sin(_fxClock*29f),Mathf.Sin(_fxClock*37f))*turbulence*1.4f;
        _shakeStrength = Mathf.MoveToward(_shakeStrength,0f,dt*55f);
        Vector2 impact = new Vector2(Mathf.Sin(_fxClock*73f),Mathf.Cos(_fxClock*89f))*_shakeStrength;
        Vector2 targetOffset = new Vector2(horizontalRatio * 34f, fallRatio * 32f) + (vibration + impact)*SaveData.CameraShake;
		_camera.Offset = _camera.Offset.Lerp(targetOffset, Mathf.Clamp(dt * 4.5f, 0f, 1f));
		_camera.Rotation = Mathf.Lerp(_camera.Rotation, -horizontalRatio * 0.022f, Mathf.Clamp(dt * 3.8f, 0f, 1f));
		float zoomAmount = Mathf.Lerp(1.0f, 0.94f, fallRatio);
		_camera.Zoom = _camera.Zoom.Lerp(new Vector2(zoomAmount, zoomAmount), Mathf.Clamp(dt * 2.2f, 0f, 1f));
	}

	private void UpdateInvulnerability(float dt)
	{
		if (_invulnerableTimer <= 0f) return;
		_invulnerableTimer = Mathf.Max(0f, _invulnerableTimer - dt);
		if (_characterSprite != null)
            _characterSprite.Visible = _invulnerableTimer <= 0f || SaveData.FlashIntensity < 0.5f || ((int)(_invulnerableTimer * 14f) % 2 == 0);
	}

	// ───────────────────────────── Нагрів і вогонь ─────────────────────────────

    private void UpdateCubeVisuals()
    {
        if (_state != State.Playing || _characterSprite == null) return;
        float ratio = Mathf.Clamp((_levelGenerator?.FallSpeed ?? 0f)/980f,0f,1f);
        _characterSprite.Position = new Vector2(Mathf.Sin(_fxClock*2.7f)*ratio*1.6f,
            Mathf.Sin(_fxClock*(2f+ratio*2f))*ratio*4f);
        float burn = _isOnFire ? Mathf.Clamp(_burnTimer/Mathf.Max(1,_currentBurnDuration),0,1) : 0f;
        float scale = _skinSpriteScale*Mathf.Lerp(1f,0.83f,burn)*(1f+_visualKick);
        float stretch = ratio*0.055f+Mathf.Sin(_fxClock*4.2f)*ratio*0.012f;
        _characterSprite.Scale = new Vector2(scale*(1f-stretch),scale*(1f+stretch));
        if (_shieldVisual != null && _hasShield)
        {
            _shieldVisual.Rotation = Mathf.Sin(_fxClock*0.9f)*0.10f;
            _shieldVisual.Scale = Vector2.One*(1f+Mathf.Sin(_fxClock*2.6f)*0.025f);
        }
    }
    public void ApplySkin()
    {
        if (_characterSprite == null) return;
        _characterSprite.Texture = GD.Load<Texture2D>(SaveData.SkinPath(SaveData.SelectedSkin));
        _skinSpriteScale = NormalSpriteScale * GD.Load<Texture2D>(SaveData.SkinPath(0)).GetWidth() / Mathf.Max(1,_characterSprite.Texture.GetWidth());
        _characterSprite.Scale = Vector2.One * _skinSpriteScale;
        _characterSprite.Material = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
    }
    private void UpdateLantern(float dt)
    {
        if (_lantern == null) return;
        Vector2 direction = GetGlobalMousePosition() - GlobalPosition;
        if (_aimWithPad)
        {
            Vector2 stick = Input.GetVector(InputSetup.AimLeft,InputSetup.AimRight,InputSetup.AimUp,InputSetup.AimDown,0.2f);
            if (stick.LengthSquared() > 0.01f) _padAim = stick.Normalized();
            direction = _padAim.Rotated(_camera != null && !_camera.IgnoreRotation ? _camera.GlobalRotation : 0f);
        }
        if (!_aimWithPad && direction.LengthSquared() < 16f) return;
        float angle = direction.Rotated(-GlobalRotation).Angle() - Mathf.Pi * 0.5f; // Authored cone points down.
        _lantern.Rotation = Mathf.LerpAngle(_lantern.Rotation,angle,1f-Mathf.Exp(-dt*20f));
    }
    private void BuildThermalVisuals()
    {
        _lantern = CaveLighting.Light(new Color("c9ecff"),0.80f,1.75f,true);
        _lantern.Name = "MouseLantern";
        _lantern.Texture = GameAssets.Beam;
        _lantern.ShadowItemCullMask = 3;
        AddChild(_lantern);
        _fireLight = CaveLighting.Light(new Color("ff7429"),0.60f,0f,false);
        _fireLight.Name = "FireLight"; AddChild(_fireLight);
        _flameMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/Burning.gdshader") };
        _flameEffect = new ColorRect { Name = "ProceduralFire", Material = _flameMaterial,
            Position = new Vector2(-58,-112), Size = new Vector2(116,156),
            MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 6, Visible = false };
        AddChild(_flameEffect);
        _wind = new WindTrails { Name = "WindTrails" }; AddChild(_wind);
        _windSfx = EnsureSfx("WindSfx","res://Audio/wind.wav");
        if (_windSfx.Stream is AudioStreamWav windAudio)
        {
            windAudio.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            windAudio.LoopBegin = 0;
            windAudio.LoopEnd = (int)(windAudio.GetLength() * windAudio.MixRate);
        }
        _windSfx.VolumeDb = -55f; _windSfx.Play();
        if (_fireGlow != null) _fireGlow.Hide();
        if (_fireParticles != null)
        {
            _fireParticles.Texture = GD.Load<Texture2D>("res://Textures/ArtRefresh/flame.png");
            _fireParticles.ScaleAmountMin = 0.005f; _fireParticles.ScaleAmountMax = 0.012f;
            _fireParticles.Amount = 18;
        }
        if (_smokeParticles != null)
        {
            _smokeParticles.Texture = GD.Load<Texture2D>("res://Textures/ArtRefresh/light_halo.png");
            _smokeParticles.ScaleAmountMin = 0.035f; _smokeParticles.ScaleAmountMax = 0.065f;
        }
    }
    private void UpdateHeating(float dt)
    {
        if (_levelGenerator == null) return;
        _coolingGrace = Mathf.Max(0f, _coolingGrace - dt);
        bool inWater = false;
        if (_hitbox != null && _hitbox.Monitoring)
            foreach (Area2D area in _hitbox.GetOverlappingAreas())
                if (area is Water && area.IsVisibleInTree() && area.Monitorable) { inWater = true; break; }
        if (inWater)
        {
            _hud?.NotifyTutorialWater();
            _coolingGrace = WaterCoolingGraceSeconds + SaveData.Cooling * 2f;
            _temperature = Mathf.MoveToward(_temperature, 0f, dt * 1.5f);
            if (_isOnFire) Extinguish();
        }
        float distance = Mathf.Abs(_levelGenerator.CurrentDepth - _levelGenerator.CoreDepthPoint);
        float ambient = Mathf.Clamp(1f - distance / HeatStartDistance, 0f, 1f);
        // Thermal inertia, never an instantaneous ignition just from crossing a depth.
        float rate = _coolingGrace > 0f ? -0.32f : ambient > 0.12f
            ? (0.045f + 0.12f * ambient) * (1f - SaveData.Insulation * 0.12f) : -0.09f;
        _temperature = Mathf.Clamp(_temperature + rate * dt, 0f, 1f);
        if (!_isOnFire && _temperature >= 0.999f && _coolingGrace <= 0f) CatchFire();
        if (_isOnFire)
        {
            // Leaving the hot region cools the player and stops further burning.
            if (_temperature < 0.55f) Extinguish(false);
            else
            {
                _burnTimer += dt;
                float burn = Mathf.Clamp(_burnTimer / _currentBurnDuration, 0f, 1f);
                // Only the artwork contracts: the collision hitbox stays consistent.
                if (_characterSprite != null) _characterSprite.Scale = Vector2.One * _skinSpriteScale * Mathf.Lerp(1f, 0.8f, burn);
                if (_burnTimer >= _currentBurnDuration) { Die(DeathCause.Overheat); return; }
            }
        }
        if (_characterSprite != null)
            _characterSprite.Modulate = Colors.White.Lerp(new Color(1f, 0.38f, 0.16f), _temperature * 0.8f);
        _flameEffect.Visible = _isOnFire;
        _flameMaterial.SetShaderParameter("clock",_fxClock);
        _flameMaterial.SetShaderParameter("strength",_isOnFire ? 0.72f + 0.28f*Mathf.Clamp(_burnTimer/_currentBurnDuration,0f,1f) : 0f);
        _flameMaterial.SetShaderParameter("wind",Mathf.Clamp(-_currentVelocityX/HorizontalSpeed,-1f,1f));
        _flameMaterial.SetShaderParameter("speed",Mathf.Clamp(_levelGenerator.FallSpeed/760f,0f,1f));
        _fireLight.Energy = _isOnFire ? 1.3f + SaveData.FlashIntensity*0.16f*Mathf.Sin(_fxClock*17f) : _temperature*0.12f;
        _flameEffect.Rotation = -GlobalRotation;
        _flameEffect.GlobalPosition = GlobalPosition + new Vector2(-58,-112);
        _thermalUiTimer += dt;
        if (_thermalUiTimer >= 0.10f)
        {
            _thermalUiTimer = 0f;
            _hud?.SetThermalStatus(_temperature,_isOnFire,Mathf.Max(0,_currentBurnDuration-_burnTimer),_coolingGrace);
        }
    }

	private void CatchFire()
	{
		if (_isOnFire || _state == State.Dead) return;

		_isOnFire = true;
		_burnTimer = 0f;
		_currentBurnDuration = (MinBurnTime + MaxBurnTime) * 0.5f;

		if (_fireGlow != null) _fireGlow.Visible = false;
		if (_fireParticles != null) _fireParticles.Emitting = true;
		if (_smokeParticles != null) _smokeParticles.Emitting = true;
		if (_characterSprite != null)
		{
			var ignitionTween = CreateTween();
			ignitionTween.TweenProperty(_characterSprite, "scale", Vector2.One * _skinSpriteScale * 1.12f, 0.10f);
			ignitionTween.TweenProperty(_characterSprite, "scale", Vector2.One * _skinSpriteScale, 0.18f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
	}

	private void Extinguish(bool water = true)
	{
        if (water && _isOnFire && ++_waterExtinguishes >= 3) SaveData.UnlockMastery(2);
		_coolingGrace = water ? WaterCoolingGraceSeconds + SaveData.Cooling * 2f : 0f;
        _temperature = Mathf.Min(_temperature, 0.25f);
        if (_flameEffect != null) _flameEffect.Visible = false;
        if (_fireLight != null) _fireLight.Energy = 0f;
		if (!_isOnFire && Scale == Vector2.One)
			return;

		_waterSfx?.Play();

		_isOnFire = false;
		_burnTimer = 0f;
		Scale = Vector2.One;

		if (_characterSprite != null)
		{
			_characterSprite.Modulate = Colors.White;
			_characterSprite.Rotation = _spriteAngle;
			_characterSprite.Scale = Vector2.One * _skinSpriteScale;
		}
		if (_fireGlow != null) _fireGlow.Visible = false;
		if (_fireParticles != null) _fireParticles.Emitting = false;
		if (_smokeParticles != null) _smokeParticles.Emitting = false;

		// Ефект охолодження
		var coolTween = CreateTween();
		coolTween.TweenProperty(this, "modulate", new Color(0.2f, 0.85f, 1f), 0.15f);
		coolTween.TweenProperty(this, "modulate", Colors.White, 0.3f);
	}

	// ───────────────────────────── Зіткнення ─────────────────────────────

	private void OnAreaEntered(Area2D area)
	{
		if (_state != State.Playing || !area.IsVisibleInTree()) return;

		uint layer = area.CollisionLayer;

		if ((layer & LayerObstacle) != 0 || area.IsInGroup("Obstacle"))
		{
			HitObstacle();
			return;
		}

		if ((layer & LayerCoin) != 0 || area.IsInGroup("Coin"))
		{
			CollectPickup(area);
			return;
		}

		if ((layer & LayerWater) != 0 || area.IsInGroup("Water"))
        { Extinguish(); _hud?.NotifyTutorialWater(); }
	}

	private void HitObstacle()
	{
		if (_invulnerableTimer > 0f) return;
		if (_hasShield)
		{
			BreakShield();
			return;
		}
		Die();
	}

	private void CollectPickup(Area2D area)
	{
        if (area is Coin pickup && pickup.Kind != PickupKind.Coin)
        {
            if (pickup.Kind == PickupKind.Shield) GrantShield();
            else if (pickup.Kind == PickupKind.DoubleCoins) _doubleCoinsTimer = 18f;
            else if (pickup.Kind == PickupKind.Magnet) _magnetTimer = 14f;
            _visualKick = 0.12f;
            PlaySfx(_coinSfx,1.35f,1.55f);
        }
        else
        {
            int amount = CoinMultiplier;
            _score += amount; SaveData.CreditCoin(amount);
            _hud?.UpdateScore(_score); _visualKick = Mathf.Max(_visualKick,0.06f);
            PlaySfx(_coinSfx,0.95f,1.2f);
        }

		area.SetDeferred("monitorable", false);
		area.Visible = false;
		area.SetDeferred("process_mode", (int)Node.ProcessModeEnum.Disabled);
	}

	private static void PlaySfx(AudioStreamPlayer sfx, float minPitch, float maxPitch)
	{
		if (sfx == null) return;
		sfx.PitchScale = (float)GD.RandRange(minPitch, maxPitch);
		sfx.Play();
	}

	// ───────────────────────────── Щит ─────────────────────────────

	private void GrantShield()
	{
		_hasShield = true;
		if (_shieldVisual != null) _shieldVisual.Visible = true;
		_hud?.SetShield(true);
		PlaySfx(_coinSfx, 1.45f, 1.5f);
	}

	private void BreakShield()
	{
		_hasShield = false;
        _visualKick = 0.16f;
        var burst = new Sprite2D { Texture = GameAssets.Shield,Material = GameAssets.Unshaded,
            Position = GlobalPosition,Scale = Vector2.One*132f/GameAssets.Shield.GetWidth(),ZIndex = 10 };
        GetParent().AddChild(burst);
        var effect = burst.CreateTween().SetParallel(true);
        effect.TweenProperty(burst,"scale",burst.Scale*1.65f,0.3f);
        effect.TweenProperty(burst,"modulate:a",0f,0.3f);
        effect.Chain().TweenCallback(Callable.From(burst.QueueFree));
		_invulnerableTimer = ShieldInvulnerableSeconds;
		if (_shieldVisual != null) _shieldVisual.Visible = false;
		_hud?.SetShield(false);
		PlaySfx(_deathSfx, 1.6f, 1.8f);
		ShakeCamera(10f);
	}

    private void ShakeCamera(float strength) { _shakeStrength = Mathf.Max(_shakeStrength,strength); }

	// ───────────────────────────── Смерть ─────────────────────────────

	private void Die(DeathCause cause = DeathCause.Collision)
	{
		if (_state == State.Dead) return;

		_state = State.Dead;
		_levelGenerator?.StopFalling();
        _wind?.SetMotion(0f,0f,false);
        _windSfx?.Stop();
        if (_lantern != null) _lantern.Enabled = false;
        if (_flameEffect != null) _flameEffect.Visible = false;
        if (_fireLight != null) _fireLight.Energy = 0f;
		_isOnFire = false;
		if (_levelGenerator != null)
			_hud?.UpdateRunStats(_levelGenerator.Lap, _levelGenerator.TotalDistance);

		if (_deathSfx != null)
		{
			_deathSfx.PitchScale = 1f;
			_deathSfx.Play();
		}
		if (_shieldVisual != null) _shieldVisual.Visible = false;

		if (_fireGlow != null) _fireGlow.Visible = false;
		if (_fireParticles != null) _fireParticles.Emitting = false;
		if (_smokeParticles != null) _smokeParticles.Emitting = false;
		CreateDeathFragments();
		if (_characterSprite != null) _characterSprite.Visible = false;
		if (_debrisParticles != null) _debrisParticles.Emitting = true;

		// Шейк камери від удару
		if (_camera != null && SaveData.CameraShake > 0f)
		{
			var shakeTween = CreateTween();
			shakeTween.TweenProperty(_camera, "offset", new Vector2(GD.RandRange(-18, 18), GD.RandRange(-18, 18))*SaveData.CameraShake, 0.04f);
			shakeTween.TweenProperty(_camera, "offset", new Vector2(GD.RandRange(-10, 10), GD.RandRange(-10, 10))*SaveData.CameraShake, 0.05f);
			shakeTween.TweenProperty(_camera, "offset", new Vector2(GD.RandRange(-4, 4), GD.RandRange(-4, 4))*SaveData.CameraShake, 0.06f);
			shakeTween.TweenProperty(_camera, "offset", Vector2.Zero, 0.08f);
		}

		// Анімація руйнування кубика
		Tween deathTween = CreateTween();
		deathTween.SetParallel(true);
		deathTween.TweenProperty(this, "scale", new Vector2(2.0f, 0.2f), 0.07f)
			.SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);

		deathTween.Chain().SetParallel(true);
		deathTween.TweenProperty(this, "scale", Vector2.Zero, 0.35f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		deathTween.TweenProperty(this, "rotation", Mathf.DegToRad(720f), 0.35f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

		deathTween.Chain().TweenInterval(0.35f);
		deathTween.Chain().TweenCallback(Callable.From(() =>
		{
			GetTree().Paused = true;
			_hud?.ShowGameOver(cause);
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
