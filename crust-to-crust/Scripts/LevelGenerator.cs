using Godot;
using System.Collections.Generic;

/// <summary>
/// Керує світом: прокрутка тунелю, пули перешкод/води/монет, біоми за глибиною та прогресія складності.
/// </summary>
public partial class LevelGenerator : Node2D
{
	[Export] private PackedScene obstacleScene;
	[Export] private PackedScene waterScene;
	[Export] private PackedScene coinScene;

	[Export] public int ObstaclePoolSize = 10;
	[Export] public int WaterPoolSize = 5;
	[Export] public int CoinPoolSize = 24;

	[Export] private float obstacleInterval = 2.6f;

	[Export] public float FallSpeed = 340f;
	[Export] public float CoreDepthPoint = 20000f;   // відстань до ядра
	[Export] public float TunnelLength = 40000f;     // повний шлях крізь планету
	[Export] public float MinFallSpeed = 200f;
    [Export] public float GravityStrength = 19f;
    [Export] public float AirDrag = 0.000026f;
    [Export] public float TerminalSpeed = 980f;
    [Export] public int CrystalLightBudget = 6;
    internal CanvasModulate CaveAmbient;
    internal readonly List<ShaderMaterial> RockMaterials = new();
    internal readonly List<ShaderMaterial> CrystalMaterials = new();
    internal readonly List<PointLight2D> CrystalLights = new();
    private float _biomeClock, _renderTicker;
    internal readonly List<Node2D> RockMaterialOwners = new();
    private readonly List<Water> _waters = new();
    private readonly List<Water> _visibleWaters = new();
    private readonly List<List<Area2D>> _crystalColliders = new();
    private float[] _lightDistances = System.Array.Empty<float>();
    private bool[] _selectedLights = System.Array.Empty<bool>();
    private readonly List<Area2D> _hazardAreas = new();
    private readonly List<(Area2D area, CanvasItem art, Node2D segment, bool left)> _wallBlocks = new();
    private readonly Dictionary<Area2D,bool> _monitorStates = new();
    private Camera2D _camera;
    public Player Runner => _player;
    public float SafeLaneAt(float y) => SafePassage.Center(CurrentDepth,y);
    public float VisualWarmth { get; private set; }
    public float FallAcceleration { get; private set; }
	[Export] public float SurfaceHangDuration = 0.65f;

	// ── Баланс (можна крутити в інспекторі) ──
	[Export] public float DifficultyRampSeconds = 120f; // за скільки секунд складність сягає максимуму
	[Export] public float FirstWaterDelay = 10f;        // перша вода (кубик ще холодний)
	[Export] public float ColdWaterMin = 9f;            // інтервал води у верхніх шарах
	[Export] public float ColdWaterMax = 14f;
	[Export] public float HotWaterMin = 4f;             // інтервал води біля ядра
	[Export] public float HotWaterMax = 7f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ShieldChance = 0.14f; // шанс, що «монета» виявиться щитом
	[Export] public float ShieldCooldown = 25f;         // мінімум секунд між щитами

	private const float HotZoneRadius = 5000f;
	private const float TunnelTemplateHeight = 720f;
	private const float WallColorUpdateInterval = 0.1f;

	// Точні внутрішні краї шести видимих секцій стіни з TunnelSegment.tscn (крок 120 px).
	private static readonly float[] LeftTunnelEdges = { -320f, -290f, -265f, -280f, -275f, -295f, -320f };
	private static readonly float[] RightTunnelEdges = { 320f, 292f, 285f, 275f, 270f, 285f, 320f };

	private readonly List<Node2D> obstaclePool = new();
	private readonly List<Node2D> waterPool = new();
	private readonly List<Node2D> coinPool = new();

	private float obstacleTimer;
    private float _nextObstacleDelay;
	private float waterTimer;
	private float nextWaterInterval;
	private float coinTimer;
	private float nextCoinInterval;
	private float _shieldCooldownTimer;
    private float _bonusCooldownTimer;
	private float _wallColorTimer;

	private float _initialFallSpeed;
	private bool _isHanging;
	private bool _tunnelHazardsVisible;
	private float _tunnelVisualDepth;

	private Player _player;
	private Node2D _tunnelSegmentsRoot;
	private Node2D _surfaceRoot;
	private AudioManager _audio;
	private readonly List<Node2D> _segments = new();
	private readonly Dictionary<Node2D, Vector2> _initialTunnelSegmentPositions = new();
	private readonly List<CanvasItem> _rockForeground = new();
	private readonly List<CanvasItem> _rockBackground = new();
	private readonly List<CanvasItem> _crystals = new();
	private readonly List<Sprite2D> _tunnelCrystals = new();

	private Color _currentWallColor = new Color(0.18f, 0.16f, 0.15f);
	private Color _currentBorderColor = new Color(0.42f, 0.38f, 0.33f);

	public float CurrentDepth;
	public bool IsGameActive;
	public float ActivePlaySeconds { get; private set; }
	/// <summary>Номер поточного кола (після вильоту з іншого боку планети починається нове).</summary>
	public int Lap { get; private set; } = 1;
	/// <summary>Сумарна пройдена відстань з усіх кіл.</summary>
	public float TotalDistance => (Lap - 1) * TunnelLength + CurrentDepth;
	public float Difficulty => Mathf.Clamp(ActivePlaySeconds / DifficultyRampSeconds, 0f, 1f);
	public float CurrentObstacleInterval => Mathf.Lerp(obstacleInterval, 1.25f, Difficulty);

    public override void _ExitTree() { SaveData.Save(); }

	// ───────────────────────────── Ініціалізація ─────────────────────────────

	public override void _Ready()
	{
		GameAssets.Load();
        SaveData.Load();
        if (SaveData.EconomyLighting) CrystalLightBudget = 3;
        _camera = GetNodeOrNull<Camera2D>("Camera2D");
        _initialFallSpeed = FallSpeed;
		nextWaterInterval = FirstWaterDelay; // на старті вода рідкісна: кубик ще холодний
		nextCoinInterval = (float)GD.RandRange(1.5, 3.0);

		_player = GetNodeOrNull<Player>("Player");
		_tunnelSegmentsRoot = GetNodeOrNull<Node2D>("TunnelSegments");
		_surfaceRoot = GetNodeOrNull<Node2D>("TunnelWalls/Surface");
		CacheTunnelNodes();
        CacheCollisionNodes();
        CaveLighting.Setup(this);
        _lightDistances = new float[CrystalLights.Count];
        _selectedLights = new bool[CrystalLights.Count];

		_audio = new AudioManager { Name = "AudioManager" };
		AddChild(_audio);

		var hud = GetNodeOrNull<Hud>("HUD");
		if (hud != null)
			hud.StartRequested += StartGame;

		InitializePool(obstacleScene, ObstaclePoolSize, obstaclePool);
		InitializePool(waterScene, WaterPoolSize, waterPool);
		InitializePool(coinScene, CoinPoolSize, coinPool);

		// Стартовий біом виставляємо до першого кадру, щоб колір не стрибав після Start.
		UpdateWallColors();
		SetTunnelHazardsVisible(false);
        WarmShaders();
	}

    private async void WarmShaders()
    {
        // Actual tiny draws behind the opaque menu prepare pipelines before a run.
        // Invisible/off-screen nodes would not warm a first-use shader.
        var warmup = new Node2D { Name = "ShaderWarmup",Position = _camera?.Position ?? Vector2.Zero };
        AddChild(warmup);
        ShaderMaterial[] materials = { GameAssets.WaterMaterial,GameAssets.RuneStyles[0],CrystalMaterials[0],
            new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/Burning.gdshader") } };
        for (int i = 0; i < materials.Length; i++)
            warmup.AddChild(new Sprite2D { Texture = GameAssets.Crystal,Material = materials[i],
                Position = new Vector2(i*2f,0),Scale = Vector2.One/GameAssets.Crystal.GetWidth() });
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if (GodotObject.IsInstanceValid(warmup)) warmup.QueueFree();
    }

	/// <summary>Кешуємо вузли тунелю один раз, щоб не шукати групи щокадру.</summary>
	private void CacheTunnelNodes()
	{
		_segments.Clear();
		_initialTunnelSegmentPositions.Clear();
		if (_tunnelSegmentsRoot != null)
		{
			foreach (Node child in _tunnelSegmentsRoot.GetChildren())
			{
				if (child is not Node2D segment) continue;
				_segments.Add(segment);
				_initialTunnelSegmentPositions[segment] = segment.Position;
			}
		}

		CacheGroup("RockForeground", _rockForeground);
		CacheGroup("RockBackground", _rockBackground);
		CacheGroup("Crystal", _crystals);
		_tunnelCrystals.Clear();
		foreach (Node node in GetTree().GetNodesInGroup("TunnelCrystal"))
			if (node is Sprite2D crystal) _tunnelCrystals.Add(crystal);
	}

	private void CacheGroup(string group, List<CanvasItem> target)
	{
		target.Clear();
		foreach (Node node in GetTree().GetNodesInGroup(group))
			if (node is CanvasItem item) target.Add(item);
	}

	private void InitializePool(PackedScene scene, int size, List<Node2D> pool)
	{
		if (scene == null) return;

		for (int i = 0; i < size; i++)
		{
			Node2D obj = (Node2D)scene.Instantiate();
			obj.Visible = false;
			if (obj is Area2D pooledArea) pooledArea.Monitorable = false;
			obj.ProcessMode = ProcessModeEnum.Disabled;
			AddChild(obj);
			pool.Add(obj);
            if (obj is Water water) RegisterWater(water);
		}
	}

	// ───────────────────────────── Керування станом гри ─────────────────────────────

	public void StartGame()
	{
		// Під час стрибка з поверхні секції ще не рухаються.
		IsGameActive = false;
		SetTunnelHazardsVisible(false);
	}

	public void BeginFalling()
	{
		// Після входу кубика в шахту поверхневі обриви не повинні накладатися на секції тунелю.
		if (_surfaceRoot != null)
			_surfaceRoot.Visible = false;
		IsGameActive = true;
		SetTunnelHazardsVisible(true);
	}

	public void StopFalling()
	{
		IsGameActive = false;
		FallSpeed = 0f;
		UpdateActiveObjectsSpeed(obstaclePool);
		UpdateActiveObjectsSpeed(waterPool);
		UpdateActiveObjectsSpeed(coinPool);
	}

	public void HideTunnelForEmergence()
	{
		if (_tunnelSegmentsRoot != null) _tunnelSegmentsRoot.Visible = false;
	}

	public void RebaseEmergence()
	{
		ResetForNextTunnelCycle();
		FallSpeed = MinFallSpeed;
		if (_surfaceRoot != null) _surfaceRoot.Rotation = 0f;
		if (_tunnelSegmentsRoot != null) _tunnelSegmentsRoot.Visible = true;
	}

	private void ResetForNextTunnelCycle()
	{
		Lap++;
		CurrentDepth = 0f;
		_tunnelVisualDepth = 0f;
		FallSpeed = _initialFallSpeed;
		_isHanging = false;
		obstacleTimer = 0f;
		waterTimer = 0f;
		coinTimer = 0f;
		nextWaterInterval = FirstWaterDelay;
		nextCoinInterval = 2f;

		foreach (KeyValuePair<Node2D, Vector2> entry in _initialTunnelSegmentPositions)
		{
			if (GodotObject.IsInstanceValid(entry.Key))
				entry.Key.Position = entry.Value;
		}

		UpdateWallColors();
	}

	// ───────────────────────────── Основний цикл ─────────────────────────────

    public void RegisterWater(Water water) { if (!_waters.Contains(water)) _waters.Add(water); }
    private void CacheCollisionNodes()
    {
        foreach (Node2D segment in _segments)
        {
            foreach (Node node in segment.FindChildren("*","Area2D",true,false))
                if (node is Area2D area && area.IsInGroup("Obstacle")) _hazardAreas.Add(area);
            for (int side = 0; side < 2; side++)
            {
                string prefix = side == 0 ? "Left" : "Right";
                var area = segment.GetNode<Area2D>(prefix+"BlockCollision");
                var art = segment.GetNode<CanvasItem>(prefix+"BlockLarge");
                _wallBlocks.Add((area,art,segment,side == 0));
            }
        }
        for (int i = 0; i < _tunnelCrystals.Count; i++)
        {
            _crystalColliders.Add(new List<Area2D>());
            foreach (Node child in _tunnelCrystals[i].GetChildren())
                if (child is Area2D area) _crystalColliders[i].Add(area);
        }
    }
    private void Monitor(Area2D area, bool value)
    {
        if (_monitorStates.TryGetValue(area,out bool previous) && previous == value) return;
        _monitorStates[area] = value;
        area.SetDeferred("monitorable",value);
    }
    public override void _PhysicsProcess(double delta)
    {
        if (_tunnelHazardsVisible) UpdateCrystalWaterClearance();
    }

	public override void _Process(double delta)
	{
		SaveData.Tick((float)delta);
        UpdateCaveAtmosphere((float)delta);
        if (!IsGameActive) return;
		if (_isHanging) return; // під час польоту через інший бік планети активні об'єкти відключені

		float dt = (float)delta;
		ActivePlaySeconds += dt;
		CurrentDepth += FallSpeed * dt;
		_tunnelVisualDepth = CurrentDepth;
		ScrollEditableTunnelSegments(dt);

        if (CurrentDepth >= TunnelLength)
        {
            BeginEmergence();
            return;
        }
        // Arcade descent: positive acceleration through the core and the entire tunnel.
        float proximity = Mathf.Clamp(1f-Mathf.Abs(CurrentDepth-CoreDepthPoint)/9000f,0f,1f);
        float drive = Mathf.Max(0f,GravityStrength)*(0.70f+1.10f*proximity);
        float resistance = 1f+Mathf.Max(0f,AirDrag)*FallSpeed*FallSpeed*0.12f;
        FallAcceleration = drive/resistance;
        float limit = Mathf.Max(FallSpeed,TerminalSpeed*Mathf.Lerp(1f,1.10f,Difficulty));
        FallSpeed = Mathf.Min(limit,FallSpeed+FallAcceleration*dt);

		UpdateActiveObjectsSpeed(obstaclePool);
		UpdateActiveObjectsSpeed(waterPool);
		UpdateActiveObjectsSpeed(coinPool);

		_wallColorTimer -= dt;
		if (_wallColorTimer <= 0f)
		{
			_wallColorTimer = WallColorUpdateInterval;
			UpdateWallColors();
		}

		SpawnTimers(dt);
	}

	private void BeginEmergence()
	{
		// Повний виліт з іншого кінця планети
		SetTunnelHazardsVisible(false);
		_isHanging = true;
		IsGameActive = false;
		FallSpeed = 0f;

		DeactivateAllPoolObjects(obstaclePool);
		DeactivateAllPoolObjects(waterPool);
		DeactivateAllPoolObjects(coinPool);

		// На час вильоту та повернення знову показуємо поверхню планети.
		if (_surfaceRoot != null)
		{
			_surfaceRoot.Visible = true;
			_surfaceRoot.Rotation = Mathf.Pi;
		}

		if (_player != null)
		{
			_player.PlayEmergenceAnimation(BeginFalling);
		}
		else
		{
			ResetForNextTunnelCycle();
			BeginFalling();
		}
	}

	private void SpawnTimers(float dt)
	{
		_shieldCooldownTimer = Mathf.Max(0f, _shieldCooldownTimer - dt);
        _bonusCooldownTimer = Mathf.Max(0,_bonusCooldownTimer-dt);

		obstacleTimer += dt;
		if (obstacleTimer >= (_nextObstacleDelay > 0f ? _nextObstacleDelay : CurrentObstacleInterval))
		{
			obstacleTimer = 0f;
            _nextObstacleDelay = CurrentObstacleInterval*(float)GD.RandRange(0.78,1.24);
			SpawnFromPool(obstacleScene, obstaclePool);
		}

		// Вода: у холодних шарах рідко, біля ядра (зона нагріву) значно частіше.
		float distToCore = Mathf.Abs(CurrentDepth - CoreDepthPoint);
		bool hotZone = distToCore < HotZoneRadius;
		if (hotZone) nextWaterInterval = Mathf.Min(nextWaterInterval, HotWaterMin);

		waterTimer += dt;
		if (waterTimer >= nextWaterInterval)
		{
			waterTimer = 0f;
			nextWaterInterval = hotZone
				? (float)GD.RandRange(HotWaterMin, HotWaterMax)
				: (float)GD.RandRange(ColdWaterMin, ColdWaterMax);
			SpawnFromPool(waterScene, waterPool);
		}

		coinTimer += dt;
		if (coinTimer >= nextCoinInterval)
		{
			coinTimer = 0f;
			nextCoinInterval = (float)GD.RandRange(1.5, 3.5);

			bool shield = _shieldCooldownTimer <= 0f
				&& !(_player?.HasShield ?? false)
				&& GD.Randf() < ShieldChance;
			if (shield) _shieldCooldownTimer = ShieldCooldown;
			PickupKind kind = shield ? PickupKind.Shield : PickupKind.Coin;
            if (!shield && _bonusCooldownTimer <= 0f && GD.Randf() < 0.26f)
            {
                kind = GD.Randf()<0.55f ? PickupKind.DoubleCoins : PickupKind.Magnet;
                _bonusCooldownTimer = 16f;
            }
            // Some coin chains mark the protected route; side coins remain optional risk.
            float x = kind != PickupKind.Coin || GD.Randf()<0.50f
                ? SafeLaneAt(1000f) : (float)GD.RandRange(-165,165);
            SpawnFromPool(coinScene,coinPool,kind,new Vector2(x,1000));
            if (kind == PickupKind.Coin && GD.Randf() < 0.38f)
            {
                SpawnFromPool(coinScene,coinPool,PickupKind.Coin,new Vector2(x+18,1100));
                SpawnFromPool(coinScene,coinPool,PickupKind.Coin,new Vector2(x-18,1200));
            }
		}
	}

	// ───────────────────────────── Тунель ─────────────────────────────

	private float SampleTunnelEdge(float worldY, float[] edgePoints)
	{
		float localY = Mathf.PosMod(worldY, TunnelTemplateHeight);
		float scaled = localY / TunnelTemplateHeight * (edgePoints.Length - 1);
		int index = Mathf.FloorToInt(scaled);
		int next = Mathf.Min(index + 1, edgePoints.Length - 1);
		return Mathf.Lerp(edgePoints[index], edgePoints[next], scaled - index);
	}

    public void GetRockBounds(float screenY, out float left, out float right)
    {
        float worldY = _tunnelVisualDepth + screenY;
        left = SampleTunnelEdge(worldY, LeftTunnelEdges);
        right = SampleTunnelEdge(worldY, RightTunnelEdges);
    }

	public void GetTunnelBounds(float screenY, out float left, out float right)
	{
		// Візуальне зміщення змінюється лише під час гри, тож у меню рельєф стоїть на місці.
		GetRockBounds(screenY, out left, out right);

		// Поки кубик у водоспаді, тримаємо його в відкритому струмені, а не в смертельній стіні позаду.
		foreach (Water water in _waters)
		{
			if (!GodotObject.IsInstanceValid(water) || !water.IsVisibleInTree() || !water.Monitorable) continue;
			if (Mathf.Abs(water.Position.Y - screenY) > Water.HalfHeight + 120f) continue;
			if (water.FromLeft) left = Mathf.Max(left, water.StreamCenter.X - 58f);
            else right = Mathf.Min(right, water.StreamCenter.X + 58f);
		}
	}

	private void ScrollEditableTunnelSegments(float delta)
	{
		const float loopHeight = TunnelTemplateHeight * 5f;
		for (int i = 0; i < _segments.Count; i++)
		{
			Node2D segment = _segments[i];
			segment.Position += new Vector2(0f, -FallSpeed * delta);
			if (segment.Position.Y < -TunnelTemplateHeight * 2f)
				segment.Position += new Vector2(0f, loopHeight);
		}
	}

    public void SetTunnelHazardsVisible(bool visible)
    {
        _tunnelHazardsVisible = visible;
        foreach (Area2D area in _hazardAreas) Monitor(area,visible);
        UpdateCrystalWaterClearance();
    }
    private void UpdateCrystalWaterClearance()
    {
        _visibleWaters.Clear();
        if (_tunnelHazardsVisible)
            foreach (Water water in _waters)
                if (GodotObject.IsInstanceValid(water) && water.Visible && water.Monitorable) _visibleWaters.Add(water);
        foreach (var block in _wallBlocks)
        {
            bool covered = false;
            float y = block.segment.GlobalPosition.Y+(block.left ? 285f : 525f);
            foreach (Water water in _visibleWaters)
                if (water.FromLeft == block.left && Mathf.Abs(y-water.GlobalPosition.Y) < Water.HalfHeight+150f)
                { covered = true; break; }
            bool clear = _tunnelHazardsVisible && !covered;
            Monitor(block.area,clear);
            if (block.art.Visible != clear) block.art.Visible = clear;
        }
        for (int i = 0; i < _tunnelCrystals.Count; i++)
        {
            Sprite2D crystal = _tunnelCrystals[i];
            bool clear = _tunnelHazardsVisible;
            foreach (Water water in _visibleWaters)
                if (Mathf.Abs(crystal.GlobalPosition.Y-water.GlobalPosition.Y) < Water.HalfHeight+170f &&
                    Mathf.Abs(crystal.GlobalPosition.X-water.StreamCenter.X) < 140f)
                { clear = false; break; }
            if (crystal.Visible != clear) crystal.Visible = clear;
            foreach (Area2D area in _crystalColliders[i]) Monitor(area,clear);
        }
    }

	// ───────────────────────────── Біоми ─────────────────────────────

    private void UpdateCaveAtmosphere(float dt)
    {
        if (GetTree().Paused) return;
        if (IsGameActive) _biomeClock += dt;
        GameAssets.WaterMaterial.SetShaderParameter("clock",_biomeClock);
        float approach = Mathf.Clamp(1f-Mathf.Abs(CurrentDepth-CoreDepthPoint)/9000f,0f,1f);
        float target = approach*approach*(3f-2f*approach);
        VisualWarmth = Mathf.Lerp(VisualWarmth,target,1f-Mathf.Exp(-dt*1.2f));
        // One shared crystal shader, instead of 25 identical per-crystal updates.
        foreach (ShaderMaterial material in CrystalMaterials)
        { material.SetShaderParameter("warmth",VisualWarmth); material.SetShaderParameter("clock",_biomeClock); }
        _renderTicker += dt;
        if (_renderTicker < 0.05f) return;
        _renderTicker = 0f;
        float centerY = _camera?.GlobalPosition.Y ?? 0f;
        for (int i = 0; i < RockMaterials.Count; i++)
            if (Mathf.Abs(RockMaterialOwners[i].GlobalPosition.Y+360f-centerY)<1200f)
                RockMaterials[i].SetShaderParameter("warmth",VisualWarmth);
        Color lightColor = new Color("59dfe8").Lerp(new Color("ff8c39"),VisualWarmth);
        for (int i = 0; i < CrystalLights.Count; i++)
        {
            var light = CrystalLights[i]; _selectedLights[i] = false;
            float distance = Mathf.Abs(light.GlobalPosition.Y-centerY);
            _lightDistances[i] = _tunnelHazardsVisible && light.IsVisibleInTree() && distance < 650f ? distance : float.PositiveInfinity;
        }
        for (int slot = 0; slot < Mathf.Clamp(CrystalLightBudget,0,8); slot++)
        {
            int nearest = -1; float best = float.PositiveInfinity;
            for (int i = 0; i < CrystalLights.Count; i++)
                if (_lightDistances[i] < best) { best = _lightDistances[i]; nearest = i; }
            if (nearest < 0) break;
            _selectedLights[nearest] = true;
            _lightDistances[nearest] = float.PositiveInfinity;
        }
        for (int i = 0; i < CrystalLights.Count; i++)
        {
            var light = CrystalLights[i];
            if (light.Enabled != _selectedLights[i]) light.Enabled = _selectedLights[i];
            if (_selectedLights[i]) { light.Color = lightColor; light.Energy = Mathf.Lerp(0.92f,1.10f,VisualWarmth); }
        }
        if (CaveAmbient != null) CaveAmbient.Color = new Color(0.075f,0.095f,0.13f).Lerp(new Color(0.20f,0.055f,0.025f),VisualWarmth);
    }
    private void UpdateWallColors()
    {
        _currentWallColor = new Color(0.18f,0.16f,0.15f).Lerp(new Color(0.36f,0.07f,0.025f),VisualWarmth);
        _currentBorderColor = new Color("526875").Lerp(new Color("ef6930"),VisualWarmth);
        foreach (Node2D node in waterPool)
            if (node.Visible && node is Water water) water.UpdateBiomeColors(_currentWallColor,_currentBorderColor);
    }

	// ───────────────────────────── Пули ─────────────────────────────

	private void DeactivateAllPoolObjects(List<Node2D> pool)
	{
		foreach (Node2D obj in pool)
		{
			obj.Visible = false;
			if (obj is Area2D pooledArea) pooledArea.SetDeferred("monitorable", false);
			obj.ProcessMode = ProcessModeEnum.Disabled;
		}
	}

	private void UpdateActiveObjectsSpeed(List<Node2D> pool)
	{
		for (int i = 0; i < pool.Count; i++)
		{
			if (!pool[i].Visible) continue;
			if (pool[i] is Obstacle movingRock) movingRock.speed = FallSpeed;
            else if (pool[i] is Water movingWater) movingWater.speed = FallSpeed;
            else if (pool[i] is Coin movingCoin) movingCoin.speed = FallSpeed;
			if (pool[i] is Obstacle obstacle) obstacle.MotionDifficulty = Difficulty;
		}
	}

	private bool IsWaterInZone(float targetY, float halfHeight = 1500f)
	{
		foreach (Node2D water in waterPool)
		{
			if (water.Visible && Mathf.Abs(water.Position.Y - targetY) < (Water.HalfHeight + halfHeight))
				return true;
		}
		return false;
	}

	private void SpawnFromPool(PackedScene scene, List<Node2D> pool, PickupKind pickupKind = PickupKind.Coin, Vector2? pickupPosition = null)
	{
		if (scene == null) return;

		// Не спавнимо камінці поверх короткого бокового водоспаду.
		if (scene == obstacleScene && IsWaterInZone(1000f, 400f))
			return;

		Node2D obj = null;
		foreach (Node2D candidate in pool)
		{
			if (!candidate.Visible) { obj = candidate; break; }
		}
		if (obj == null) return; // Bounded pools: no mid-run node/resource allocation spikes.

		Vector2 spawnPosition;
		if (scene == waterScene)
		{
			// Водоспад сам обирає ліву або праву стіну; кореневий вузол лишається по центру.
			spawnPosition = new Vector2(0f, 1450f);
			obj.Scale = Vector2.One;
			obj.Rotation = 0f;
			if (obj is Water waterNode)
			{
				waterNode.UpdateBiomeColors(_currentWallColor, _currentBorderColor);
				waterNode.GenerateRandomCaveGeometry();
			}
		}
		else if (scene == obstacleScene)
		{
			// Камені спавняться по всій ширині тунелю
			spawnPosition = new Vector2((float)GD.RandRange(-165.0, 165.0), 1000f);
			float scaleVariation = (float)GD.RandRange(0.75,SafePassage.MaxSpawnScale);
			Vector2 targetScale = new Vector2(scaleVariation, scaleVariation);
			obj.Scale = targetScale;
			obj.Rotation = (float)GD.RandRange(0, Mathf.Pi * 2);

			if (obj is Obstacle obs)
			{
				obj.Position = spawnPosition;
                int roll = GD.RandRange(0,99);
                int variant = roll < 32 ? 0 : roll < 57 ? 1 : roll < 77 ? 3 : roll < 92 ? 4 : 5;
                if (Difficulty < 0.12f && variant >= 4) variant = 0;
                obs.ConfigureVariant(variant,_player?.Position.X ?? 0f);
                int side = GD.Randf() < 0.5f ? -1 : 1;
                if (!obs.ProtectPassage(side,scaleVariation)) return;
                spawnPosition = obs.Position;
				obs.MotionDifficulty = Difficulty;
				obs.TriggerAppear(targetScale);
			}
		}
		else
		{
			// Монети та щити використовують повну ігрову ширину
			spawnPosition = pickupPosition ?? new Vector2((float)GD.RandRange(-175.0,175.0),1000f);
			obj.Scale = Vector2.One;
			obj.Rotation = 0f;
			if (obj is Coin coin)
				coin.TriggerAppear(pickupKind);
		}

		obj.Position = spawnPosition;
		obj.Visible = true;
		if (obj is Area2D pooledArea) pooledArea.SetDeferred("monitorable", true);
		obj.ProcessMode = ProcessModeEnum.Inherit;
		if (obj is Obstacle movingRock) movingRock.speed = FallSpeed;
        else if (obj is Water movingWater) movingWater.speed = FallSpeed;
        else if (obj is Coin movingCoin) movingCoin.speed = FallSpeed;
	}
}
