using Godot;
using System.Collections.Generic;

/// <summary>
/// Керує світом: прокрутка тунелю, пули перешкод/води/монет, біоми за глибиною та прогресія складності.
/// </summary>
public partial class LevelGenerator : Node2D
{
    [Signal] public delegate void CoreCrossedEventHandler();
    public CaveZone Zone => RunDirector.ZoneAt(CurrentDepth,CoreDepthPoint);
    private int _coreLap;
    private float _coreRespite;
    private float _zoneBlend;
	[Export] private PackedScene obstacleScene;
	[Export] private PackedScene waterScene;
	[Export] private PackedScene coinScene;

	[Export] public int ObstaclePoolSize = 16;
	[Export] public int WaterPoolSize = 5;
	[Export] public int CoinPoolSize = 24;

	[Export] private float obstacleInterval = 2.1f;

	[Export] public float FallSpeed = 340f;
	[Export] public float CoreDepthPoint = 20000f;   // відстань до ядра
	[Export] public float TunnelLength = 40000f;     // повний шлях крізь планету
	[Export] public float MinFallSpeed = 200f;
    [Export] public float GravityStrength = 19f;
    [Export] public float AirDrag = 0.000026f;
    [Export] public float TerminalSpeed = 980f;
    [Export(PropertyHint.Range,"1.1,4,0.1")] public float ReturnSpeedDivisor = 2.4f;
    private RunMomentum _momentum;
    private float _routeDistance, _routeScale = 1f;
    public float TraversalStartSpeed => _momentum?.StartSpeed ?? FallSpeed;
    public float TraversalPeakSpeed => _momentum?.PeakSpeed ?? FallSpeed;
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
    private float[] _crystalZoneAlpha;
    private float[] _lightDistances = System.Array.Empty<float>();
    private bool[] _selectedLights = System.Array.Empty<bool>();
    private readonly List<Area2D> _hazardAreas = new();
    private readonly List<(Area2D area, CanvasItem art, Node2D segment, bool left)> _wallBlocks = new();
    private readonly Dictionary<Area2D,bool> _monitorStates = new();
    private Camera2D _camera;
    public Player Runner => _player;
    public static int? RequestedSeed;
    public RunDirector Director { get; private set; } = new RunDirector(System.Random.Shared.Next(1,int.MaxValue));
    public int RunSeed => Director.Seed;
    public EncounterKind Encounter { get; private set; }
    private int _encounterIndex = -1, _encounterSide;
    private float _encounterEndDistance;
    public float SafeLaneAt(float y) => Director.LaneAt(_routeDistance+y/_routeScale);
    public bool CanHazardTraverse(int side,float startY,float radius,float scroll)
    {
        float travel=Mathf.Max(0,startY-CaveViewport.WorldTop(this)+radius+96);
        int steps=Mathf.Clamp(Mathf.CeilToInt(travel/48),2,160);
        for(int i=0;i<=steps;i++)
        {
            float y=startY+travel*i/steps*(1-scroll)/scroll;
            GetRockBounds(y,out float left,out float right);
            if(!SafePassage.TryBounds(side,SafeLaneAt(y),radius+6,left,right,out _,out _,_player?.CollisionEnvelopeRadius??SafePassage.PlayerRadius)) return false;
        }
        return true;
    }
    public void SetRunSeed(int seed)
    {
        if (IsGameActive) return;
        Director = new RunDirector(seed); _encounterIndex = -1; _encounterEndDistance = 0f;
        nextCoinInterval = Director.NextFloat(1.5f,3f);
    }
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
    private readonly List<WallShooter> _shooters=new();
    private readonly List<HazardProjectile> _projectiles=new();
    private float _shooterCooldown=18f;
    private BasaltCollapse _basaltCollapse;
    private CrystalResonance _crystalResonance;
    private MagmaVent _magmaVent;
    private float _magmaTimer, _magmaDelay=10f;
    private bool LocalWallEventActive=>(_basaltCollapse?.Active??false)||(_crystalResonance?.Active??false)||(_magmaVent?.Active??false);
    private float _crystalTimer, _crystalDelay=9f;
    private float _basaltTimer, _basaltDelay=8f;
    private float _batteryTimer, _nextBatteryDelay=10f, _cascadeTimer;
    private int _cascadeRemaining;
    public int BatteriesSpawned { get; private set; }
    public bool ProjectilesAllowed=>IsGameActive && _coreRespite<=0f && !(_hud?.TutorialActive??false) && Encounter!=EncounterKind.WaterApproach && !LocalWallEventActive;

	private float obstacleTimer;
    private float _nextObstacleDelay;
	private float waterTimer;
	private float nextWaterInterval;
    private bool _wasHotZone;
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
    private Hud _hud;
    private bool _tutorialWaterSpawned;
    public bool TutorialWaterPending => _hud?.TutorialWaterPending ?? false;
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
	public float CurrentObstacleInterval => Mathf.Lerp(obstacleInterval, 0.85f, Difficulty);

    public override void _ExitTree() { SaveData.Save(); }

	// ───────────────────────────── Ініціалізація ─────────────────────────────

	public override void _Ready()
	{
		GameAssets.Load();
        if (RequestedSeed.HasValue) { SetRunSeed(RequestedSeed.Value); RequestedSeed = null; }
        SaveData.Load();
        if (SaveData.EconomyLighting) CrystalLightBudget = 3;
        _camera = GetNodeOrNull<Camera2D>("Camera2D");
        _initialFallSpeed = FallSpeed;
        _momentum = new RunMomentum(FallSpeed,TerminalSpeed,ReturnSpeedDivisor);
		nextWaterInterval = FirstWaterDelay; // на старті вода рідкісна: кубик ще холодний
		nextCoinInterval = Director.NextFloat(1.5f,3f);

		_player = GetNodeOrNull<Player>("Player");
		_tunnelSegmentsRoot = GetNodeOrNull<Node2D>("TunnelSegments");
		_surfaceRoot = GetNodeOrNull<Node2D>("TunnelWalls/Surface");
		CacheTunnelNodes();
        CacheCollisionNodes();
        CaveLighting.Setup(this);
        _surfaceRoot?.AddChild(new SurfacePresentation { Name="SurfacePresentation" });
        _lightDistances = new float[CrystalLights.Count];
        _selectedLights = new bool[CrystalLights.Count];
        _crystalZoneAlpha = new float[_tunnelCrystals.Count]; System.Array.Fill(_crystalZoneAlpha,1f);

		_audio = new AudioManager { Name = "AudioManager" };
		AddChild(_audio);

		var hud = GetNodeOrNull<Hud>("HUD");
        _hud = hud;
		if (hud != null)
			hud.StartRequested += StartGame;

		InitializePool(obstacleScene, ObstaclePoolSize, obstaclePool);
		InitializePool(waterScene, WaterPoolSize, waterPool);
		InitializePool(coinScene, CoinPoolSize, coinPool);
        for(int i=0;i<6;i++) { var shooter=new WallShooter(); AddChild(shooter); _shooters.Add(shooter); }
        for(int i=0;i<24;i++) { var shot=new HazardProjectile(); AddChild(shot); _projectiles.Add(shot); }
        _basaltCollapse=new BasaltCollapse { Name="BasaltCollapse" }; AddChild(_basaltCollapse);
        _crystalResonance=new CrystalResonance { Name="CrystalResonance" }; AddChild(_crystalResonance);
        _magmaVent=new MagmaVent { Name="MagmaVent" }; AddChild(_magmaVent);

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
        ShaderMaterial[] materials = { new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/CryoGas.gdshader") },
            new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/CryoRock.gdshader") },GameAssets.RuneStyles[0],CrystalMaterials[0],
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
        _audio?.StartGameplayMusic();
		// Під час стрибка з поверхні секції ще не рухаються.
		IsGameActive = false;
		SetTunnelHazardsVisible(false);
	}

	public void BeginFalling()
	{
        SetTunnelFade(1);
		// Після входу кубика в шахту поверхневі обриви не повинні накладатися на секції тунелю.
		if (_surfaceRoot != null)
			_surfaceRoot.Visible = false;
		IsGameActive = true;
		SetTunnelHazardsVisible(true);
	}

	public void StopFalling()
	{
        _basaltCollapse?.Deactivate();
        _crystalResonance?.Deactivate();
        _magmaVent?.Deactivate();
        _audio?.SetCorePresence(0,false);
        ClearShooters();
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
    public void SetTunnelFade(float alpha)
    {
        if(_tunnelSegmentsRoot!=null) _tunnelSegmentsRoot.Modulate=new Color(1,1,1,Mathf.Clamp(alpha,0,1));
    }

	public void RebaseEmergence()
	{
		ResetForNextTunnelCycle();
		if (_surfaceRoot != null) _surfaceRoot.Rotation = 0f;
		if (_tunnelSegmentsRoot != null) _tunnelSegmentsRoot.Visible = true;
	}

	private void ResetForNextTunnelCycle()
	{
		Lap++;
		CurrentDepth = 0f;
		_tunnelVisualDepth = 0f;
		FallSpeed = _momentum.NextTraversal();
		_isHanging = false;
		obstacleTimer = 0f;
		waterTimer = 0f;
		coinTimer = 0f;
		nextWaterInterval = FirstWaterDelay;
        _wasHotZone = false;
		nextCoinInterval = 2f;

		foreach (KeyValuePair<Node2D, Vector2> entry in _initialTunnelSegmentPositions)
		{
			if (GodotObject.IsInstanceValid(entry.Key))
				entry.Key.Position = entry.Value;
		}
        UpdateTextureCoordinates();
		UpdateWallColors();
	}

	// ───────────────────────────── Основний цикл ─────────────────────────────

    public void RegisterWater(Water water) { if (!_waters.Contains(water)) _waters.Add(water); }
    private bool CrossesEscapeRoute(Area2D area)
    {
        foreach (Node child in area.GetChildren())
        {
            if (child is not CollisionPolygon2D shape) continue;
            float left=float.PositiveInfinity,right=float.NegativeInfinity,top=float.PositiveInfinity,bottom=float.NegativeInfinity;
            foreach (Vector2 point in shape.Polygon)
            {
                Vector2 world=shape.GlobalTransform*point;
                left=Mathf.Min(left,world.X); right=Mathf.Max(right,world.X);
                top=Mathf.Min(top,world.Y); bottom=Mathf.Max(bottom,world.Y);
            }
            float playerRadius=_player?.CollisionEnvelopeRadius??SafePassage.PlayerRadius;
            for (float y=top-playerRadius;y<=bottom+playerRadius;y+=24f)
            {
                float lane=SafeLaneAt(y),margin=playerRadius+SafePassage.HalfWidth+SafePassage.Clearance;
                if (left<=lane+margin && right>=lane-margin) return true;
            }
        }
        return false;
    }
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
        if (_tunnelHazardsVisible) UpdateCrystalWaterClearance((float)delta);
    }

	public override void _Process(double delta)
	{
		SaveData.Tick((float)delta);
        UpdateCaveAtmosphere((float)delta);
        if (!IsGameActive) return;
		if (_isHanging) return; // під час польоту через інший бік планети активні об'єкти відключені

		float dt = (float)delta;
		ActivePlaySeconds += dt;
        float previousDepth = CurrentDepth;
		CurrentDepth += FallSpeed * dt;
        // Integrate route phase: changing wavelength cannot teleport the passage.
        _routeScale = Mathf.Max(1f,FallSpeed/1100f);
        _routeDistance += FallSpeed*dt/_routeScale;
        _momentum.Observe(FallSpeed);
        _coreRespite = Mathf.Max(0,_coreRespite-dt);
        if (previousDepth < CoreDepthPoint && CurrentDepth >= CoreDepthPoint && _coreLap != Lap)
        {
            _coreLap = Lap; _coreRespite = 2f;
            ClearShooters();
            foreach(Node2D item in obstaclePool)
                if(item.Visible && item is Obstacle obstacle) obstacle.RetireForClearance();
            _hud?.AnnounceCore(); _audio?.PlayCoreAccent();
            SaveData.UnlockMastery(0); EmitSignal(SignalName.CoreCrossed);
        }
        _audio?.SetZone(Zone);
        _audio?.SetCorePresence(1f-Mathf.Abs(CurrentDepth-CoreDepthPoint)/6000f,IsGameActive);
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
        float limit = Mathf.Max(FallSpeed,_momentum.SpeedLimit*Mathf.Lerp(1f,1.10f,Difficulty));
        FallSpeed = Mathf.Min(limit,FallSpeed+FallAcceleration*dt);
        _momentum.Observe(FallSpeed);

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
        _momentum.Observe(FallSpeed);
        ClearShooters();
        SaveData.UnlockMastery(1);
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
        _magmaTimer+=dt;
        if(_magmaTimer>=_magmaDelay && Zone==CaveZone.Magma && !(_hud?.TutorialActive??false) && Encounter!=EncounterKind.Cascade && Encounter!=EncounterKind.Sentry)
        {
            int side=Director.NextInt(0,2)==0 ? -1 : 1;
            float sourceY=(_player?.FixedY??-200)+FallSpeed*2.5f;
            if(!TryStartMagmaVent(side,sourceY)) TryStartMagmaVent(-side,sourceY);
            _magmaTimer=0; _magmaDelay=Director.NextFloat(10,15);
        }
        _crystalTimer+=dt;
        if(_crystalTimer>=_crystalDelay && Zone==CaveZone.Crystal && !(_hud?.TutorialActive??false) && Encounter!=EncounterKind.Cascade && Encounter!=EncounterKind.Sentry)
        {
            int side=Director.NextInt(0,2)==0 ? -1 : 1;
            float sourceY=(_player?.FixedY??-200)+FallSpeed*2.7f;
            if(!TryStartCrystalResonance(side,sourceY)) TryStartCrystalResonance(-side,sourceY);
            _crystalTimer=0; _crystalDelay=Director.NextFloat(11,16);
        }
        _basaltTimer+=dt;
        if(_basaltTimer>=_basaltDelay && Zone==CaveZone.Basalt && !(_hud?.TutorialActive??false) && Encounter!=EncounterKind.Cascade && Encounter!=EncounterKind.Sentry)
        {
            int side=Director.NextInt(0,2)==0 ? -1 : 1;
            float sourceY=(_player?.FixedY??-200)+FallSpeed*2.7f;
            if(!TryStartBasaltCollapse(side,sourceY)) TryStartBasaltCollapse(-side,sourceY);
            _basaltTimer=0; _basaltDelay=Director.NextFloat(12,17);
        }

        _shooterCooldown-=dt;
        if(!(_hud?.TutorialActive??false))
        {
            _batteryTimer+=dt;
            if(_batteryTimer>=_nextBatteryDelay)
            {
                _batteryTimer=0; _nextBatteryDelay=Director.NextFloat(12,16);
                float side=(_player?.Position.X??0)>=0 ? -1 : 1;
                float x=Mathf.Clamp(SafeLaneAt(1100)+side*Director.NextFloat(72,105),-175,175);
                if(Mathf.Abs(x-(_player?.Position.X??0))<60) x=Mathf.Clamp((_player?.Position.X??0)+side*90,-175,175);
                float travel=Mathf.Abs(x-(_player?.Position.X??0))/Mathf.Max(1,_player?.HorizontalSpeed??380);
                float y=Mathf.Max(1100,(_player?.FixedY??-200)+FallSpeed*(travel+0.9f));
                SpawnFromPool(coinScene,coinPool,PickupKind.Battery,new Vector2(x,y));
            }
        }
        if (TutorialWaterPending && !_tutorialWaterSpawned)
        { SpawnFromPool(waterScene,waterPool); _tutorialWaterSpawned = true; waterTimer = 0f; }
        while (TotalDistance >= _encounterEndDistance)
        {
            _encounterIndex++; Encounter = Director.EncounterForZone(_encounterIndex,Zone);
            _encounterEndDistance += Director.DurationAt(_encounterIndex)*Mathf.Max(340f,FallSpeed);
            _encounterSide = _encounterIndex % 2 == 0 ? -1 : 1;
            if (Encounter == EncounterKind.WaterApproach) SpawnFromPool(waterScene,waterPool);
        }
		_shieldCooldownTimer = Mathf.Max(0f, _shieldCooldownTimer - dt);
        _bonusCooldownTimer = Mathf.Max(0,_bonusCooldownTimer-dt);

		obstacleTimer += dt;
        if(_cascadeRemaining>0)
        {
            _cascadeTimer-=dt;
            if(_cascadeTimer<=0)
            {
                if(_coreRespite<=0 && !LocalWallEventActive && !(_hud?.TutorialActive??false) && Encounter==EncounterKind.Cascade) SpawnFromPool(obstacleScene,obstaclePool);
                _cascadeRemaining--; _cascadeTimer=0.55f;
                if(_cascadeRemaining==0) obstacleTimer=0;
            }
        }
		if (obstacleTimer >= (_nextObstacleDelay > 0f ? _nextObstacleDelay : CurrentObstacleInterval))
		{
			obstacleTimer = 0f;
            _nextObstacleDelay = CurrentObstacleInterval*Director.NextFloat(0.78f,1.24f);
			if (_cascadeRemaining==0 && !LocalWallEventActive && Encounter != EncounterKind.Rest && Encounter != EncounterKind.WaterApproach && Encounter != EncounterKind.Sentry && !(_hud?.TutorialActive ?? false) && _coreRespite <= 0f)
            {
                SpawnFromPool(obstacleScene, obstaclePool);
                if(Encounter==EncounterKind.Cascade) { _cascadeRemaining=2; _cascadeTimer=0.55f; }
            }
		}
        if(_shooterCooldown<=0 && ActivePlaySeconds>=18 && ProjectilesAllowed && Encounter!=EncounterKind.Rest)
        {
            // Sample intent before pool lookup or water suppression so capacity cannot shift RNG.
            int side=Director.NextInt(0,2)==0 ? -1 : 1;
            float variantRoll=Director.NextFloat(0,1);
            int variant=Zone is CaveZone.Magma or CaveZone.Core ? 1 : Zone==CaveZone.Crystal && variantRoll<0.6f ? 2 : 0;
            _shooterCooldown=Director.NextFloat(5.5f,8.5f);
            float y=(_player?.FixedY??-200)+WallShooter.PreferredFireGap+FallSpeed*WallShooter.WarningSeconds;
            if(!IsWaterInZone(y,FallSpeed*1.8f) && !IsWaterInZone(-200,Water.HalfHeight+120))
                foreach(var shooter in _shooters)
                    if(!shooter.Visible) { shooter.Activate(this,variant,side,y); break; }
        }

		// Вода: у холодних шарах рідко, біля ядра (зона нагріву) значно частіше.
		float distToCore = Mathf.Abs(CurrentDepth - CoreDepthPoint);
		bool hotZone = distToCore < HotZoneRadius;
		if (hotZone && !_wasHotZone) nextWaterInterval = Mathf.Min(nextWaterInterval, HotWaterMin);
        _wasHotZone = hotZone;

		waterTimer += dt;
		if (waterTimer >= nextWaterInterval)
		{
			waterTimer = 0f;
			nextWaterInterval = hotZone
				? Director.NextFloat(HotWaterMin,HotWaterMax)
				: Director.NextFloat(ColdWaterMin,ColdWaterMax);
			SpawnFromPool(waterScene, waterPool);
		}

		coinTimer += dt;
		if (coinTimer >= nextCoinInterval)
		{
			coinTimer = 0f;
			nextCoinInterval = Director.NextFloat(1.5f,3.5f);

			bool shield = _shieldCooldownTimer <= 0f
				&& !(_player?.HasShield ?? false)
				&& Director.NextFloat(0,1) < ShieldChance;
			if (shield) _shieldCooldownTimer = ShieldCooldown;
			PickupKind kind = shield ? PickupKind.Shield : PickupKind.Coin;
            if (!shield && _bonusCooldownTimer <= 0f && Director.NextFloat(0,1) < 0.26f)
            {
                kind = Director.NextFloat(0,1)<0.55f ? PickupKind.DoubleCoins : PickupKind.Magnet;
                _bonusCooldownTimer = 16f;
            }
            // Some coin chains mark the protected route; side coins remain optional risk.
            float x = Encounter == EncounterKind.CoinTrail ? (_encounterIndex % 4 == 0 ? -150f : 150f)
                : kind != PickupKind.Coin || Director.NextFloat(0,1)<0.50f ? SafeLaneAt(1000f) : Director.NextFloat(-165,165);
            SpawnFromPool(coinScene,coinPool,kind,new Vector2(x,1000));
            if (kind == PickupKind.Coin && (Encounter == EncounterKind.CoinTrail || Director.NextFloat(0,1) < 0.38f))
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
        UpdateTextureCoordinates();
	}
    private void UpdateTextureCoordinates()
    {
        for(int i=0;i<RockMaterials.Count;i++)
            RockMaterials[i].SetShaderParameter("texture_origin",new Vector2(0,TotalDistance+RockMaterialOwners[i].Position.Y));
    }

    public void SetTunnelHazardsVisible(bool visible)
    {
        _tunnelHazardsVisible = visible;
        foreach (Area2D area in _hazardAreas) Monitor(area,visible);
        UpdateCrystalWaterClearance();
    }
    private void UpdateCrystalWaterClearance(float dt = 0.016f)
    {
        bool collisionsAllowed = _coreRespite <= 0f && !(_hud?.TutorialActive ?? false);
        _visibleWaters.Clear();
        if (_tunnelHazardsVisible)
            foreach (Water water in _waters)
                if (GodotObject.IsInstanceValid(water) && water.Visible && water.Monitorable) _visibleWaters.Add(water);
        bool Approaching(Water water,float y)
        {
            if (_player == null || FallSpeed <= 0f) return false;
            float eta = (water.GlobalPosition.Y-Water.HalfHeight-_player.GlobalPosition.Y)/FallSpeed;
            float travel = Mathf.Abs(water.StreamCenter.X-_player.GlobalPosition.X)/Mathf.Max(1,_player.HorizontalSpeed)+0.3f;
            return eta >= -0.3f && eta <= travel+0.3f && y >= _player.GlobalPosition.Y-SafePassage.PlayerRadius
                && y <= water.GlobalPosition.Y+Water.HalfHeight;
        }
        foreach (Water water in _visibleWaters)
            foreach (Node2D item in obstaclePool)
                if (item.Visible && Approaching(water,item.GlobalPosition.Y))
                { if(item is Obstacle obstacle) obstacle.RetireForClearance(); }
        foreach (var block in _wallBlocks)
        {
            bool covered = false;
            float y = block.segment.GlobalPosition.Y+(block.left ? 285f : 525f);
            foreach (Water water in _visibleWaters)
                if (water.FromLeft == block.left && (Mathf.Abs(y-water.GlobalPosition.Y) < Water.HalfHeight+150f || Approaching(water,y)))
                { covered = true; break; }
            bool clear = _tunnelHazardsVisible && !covered && !CrossesEscapeRoute(block.area);
            float wallAlpha=_tunnelHazardsVisible ? Mathf.MoveToward(block.art.Modulate.A,clear ? 1 : 0,dt*2.5f) : 0;
            block.art.Modulate=new Color(1,1,1,wallAlpha);
            block.art.Visible=wallAlpha>0.01f;
            Monitor(block.area,clear && collisionsAllowed && wallAlpha>0.95f);
        }
        for (int i = 0; i < _tunnelCrystals.Count; i++)
        {
            Sprite2D crystal = _tunnelCrystals[i];
            bool clear = _tunnelHazardsVisible;
            foreach (Area2D area in _crystalColliders[i]) if (CrossesEscapeRoute(area)) { clear = false; break; }
            float density = Zone == CaveZone.Basalt ? (i%3 == 0 ? 1f : 0f)
                : Zone == CaveZone.Crystal ? 1f : Zone == CaveZone.Magma ? (i%3 != 1 ? 1f : 0f) : (i%2 == 0 ? 1f : 0f);
            foreach (Water water in _visibleWaters)
                if ((Mathf.Abs(crystal.GlobalPosition.Y-water.GlobalPosition.Y) < Water.HalfHeight+170f &&
                    Mathf.Abs(crystal.GlobalPosition.X-water.StreamCenter.X) < 140f) ||
                    ((crystal.GlobalPosition.X < 0) == water.FromLeft && Approaching(water,crystal.GlobalPosition.Y)))
                { clear = false; break; }
            float alpha=_tunnelHazardsVisible ? Mathf.MoveToward(_crystalZoneAlpha[i],clear ? density : 0,dt*1.5f) : 0;
            _crystalZoneAlpha[i]=alpha;
            crystal.Modulate=new Color(1,1,1,alpha);
            crystal.Visible=alpha>0.01f;
            foreach (Area2D area in _crystalColliders[i]) Monitor(area,clear && collisionsAllowed && alpha > 0.95f);
        }
    }

	// ───────────────────────────── Біоми ─────────────────────────────

    private void UpdateCaveAtmosphere(float dt)
    {
        if (GetTree().Paused) return;
        if (IsGameActive) _biomeClock += dt;
        float approach = Mathf.Clamp(1f-Mathf.Abs(CurrentDepth-CoreDepthPoint)/9000f,0f,1f);
        float target = approach*approach*(3f-2f*approach);
        VisualWarmth = Mathf.Lerp(VisualWarmth,target,1f-Mathf.Exp(-dt*1.2f));
        // One shared crystal shader, instead of 25 identical per-crystal updates.
        foreach (ShaderMaterial material in CrystalMaterials)
        {
            material.SetShaderParameter("warmth",VisualWarmth); material.SetShaderParameter("clock",_biomeClock);
            material.SetShaderParameter("flash_intensity",SaveData.FlashIntensity);
        }
        float crystalTarget = Zone == CaveZone.Crystal ? 1f : Zone == CaveZone.Basalt ? 0.15f : 0.55f;
        _zoneBlend = Mathf.Lerp(_zoneBlend,crystalTarget,1f-Mathf.Exp(-dt*1.2f));
        foreach (ShaderMaterial material in CrystalMaterials) material.SetShaderParameter("zone_glow",_zoneBlend);
        _renderTicker += dt;
        if (_renderTicker < 0.05f) return;
        _renderTicker = 0f;
        float centerY = _camera?.GlobalPosition.Y ?? 0f;
        for (int i = 0; i < RockMaterials.Count; i++)
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
            if (_selectedLights[i]) { light.Color = lightColor; light.Energy = Mathf.Lerp(0.18f,0.30f,_zoneBlend)+VisualWarmth*0.03f; }
        }
        if (CaveAmbient != null) CaveAmbient.Color = new Color(0.03f,0.045f,0.065f).Lerp(new Color(0.085f,0.027f,0.014f),VisualWarmth);
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
    private void ClearShooters()
    {
        _basaltCollapse?.Deactivate();
        _crystalResonance?.Deactivate();
        _magmaVent?.Deactivate();
        foreach(var shooter in _shooters) shooter.Deactivate();
        foreach(var shot in _projectiles) shot.Deactivate();
    }
    public bool TryFireVolley(WallShooter shooter,int variant,Vector2 target)
    {
        if(!ProjectilesAllowed || _player==null || IsWaterInZone(_player.FixedY,Water.HalfHeight+140)) return false;
        float gap=shooter.GlobalPosition.Y-_player.GlobalPosition.Y;
        if(gap<WallShooter.MinimumFireGap || gap>WallShooter.MaximumFireGap) return false;
        int count=variant==0 ? 1 : 3, free=0;
        foreach(var shot in _projectiles) if(!shot.Visible) free++;
        if(free<count) return false;
        var paths=new List<ProjectilePath>();
        foreach(var shot in _projectiles) if(shot.Visible) paths.Add(shot.Path);
        int existing=paths.Count;
        Vector2 velocity=shooter.AimVelocity(target);
        for(int i=0;i<count;i++)
            paths.Add(new ProjectilePath(shooter.Muzzle,velocity.Rotated(count==1 ? 0 : (i-1)*Mathf.DegToRad(variant==2 ? 12 : 10))));
        var solids=new List<Rect2>();
        foreach(var area in _hazardAreas)
        {
            if(!area.Monitorable || !area.IsVisibleInTree()) continue;
            foreach(Node child in area.GetChildren())
                if(child is CollisionPolygon2D polygon && !polygon.Disabled && polygon.Polygon.Length>0)
                {
                    Vector2 point=polygon.ToGlobal(polygon.Polygon[0]); var rect=new Rect2(point,Vector2.Zero);
                    foreach(var vertex in polygon.Polygon) rect=rect.Expand(polygon.ToGlobal(vertex));
                    solids.Add(rect);
                }
        }
        float radius=_player.CollisionEnvelopeRadius;
        bool Blocked(float x,float time)
        {
            GetRockBounds(_player.FixedY+FallSpeed*time,out float left,out float right);
            if(x<left+radius+8 || x>right-radius-8) return true;
            foreach(var obstacle in obstaclePool)
                if(obstacle.Visible && obstacle is Obstacle hazard && hazard.BlocksEscapeAt(x,time,radius)) return true;
            return false;
        }
        if(!HazardEncounterRules.HasEscape(_player.Position.X,_player.HorizontalSpeed,radius,FallSpeed,paths,solids,Blocked)) return false;
        for(int i=existing;i<paths.Count;i++)
            foreach(var shot in _projectiles)
                if(!shot.Visible) { shot.Activate(paths[i].Position,paths[i].Velocity,variant,this); break; }
        return true;
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

    public bool CanDischargeMagma(float y)=>IsGameActive && Zone==CaveZone.Magma && !IsWaterInZone(y,Mathf.Max(500,FallSpeed*1.8f));
    public bool TryStartMagmaVent(int side,float y)
    {
        if(!CanDischargeMagma(y) || _magmaVent==null || LocalWallEventActive || (_hud?.TutorialActive??false) || _coreRespite>0 || _cascadeRemaining>0 || !float.IsFinite(y) || FallSpeed<=0) return false;
        float sourceDepth=CurrentDepth+y-(_player?.FixedY??-200);
        if(RunDirector.ZoneAt(sourceDepth,CoreDepthPoint)!=CaveZone.Magma || y-(_player?.FixedY??-200)<FallSpeed*1.5f) return false;
        foreach(var shooter in _shooters) if(shooter.Visible) return false;
        foreach(var shot in _projectiles) if(shot.Visible) return false;
        _magmaVent.Activate(this,side,y); return true;
    }

    public bool TryStartCrystalResonance(int side,float y)
    {
        if(!IsGameActive || Zone!=CaveZone.Crystal || (_crystalResonance?.Active??true) || (_basaltCollapse?.Active??false) || (_magmaVent?.Active??false) || (_hud?.TutorialActive??false) || _coreRespite>0 || _cascadeRemaining>0 || !float.IsFinite(y)) return false;
        float sourceDepth=CurrentDepth+y-(_player?.FixedY??-200);
        if(sourceDepth>TunnelLength-500 || RunDirector.ZoneAt(sourceDepth,CoreDepthPoint)!=CaveZone.Crystal || IsWaterInZone(y,Mathf.Max(400,FallSpeed*2.1f))) return false;
        foreach(var shooter in _shooters) if(shooter.Visible) return false;
        foreach(var shot in _projectiles) if(shot.Visible) return false;
        float radius=SafePassage.ColliderRadius(2)*.6f*SafePassage.AppearOvershoot;
        if(!CanHazardTraverse(side,y,radius,1)) return false;
        _crystalResonance.Activate(this,side,y); return true;
    }
    public bool TryReleaseCrystalFragment(int side,float y)
    {
        if(!IsGameActive || Zone!=CaveZone.Crystal || IsWaterInZone(y,Mathf.Max(400,FallSpeed*2.1f))) return false;
        foreach(Node2D node in obstaclePool)
        {
            if(node.Visible || node is not Obstacle crystal) continue;
            GetRockBounds(y,out float left,out float right);
            crystal.Position=new Vector2(side<0 ? left+70 : right-70,y);
            crystal.ConfigureCrystalFragment();
            crystal.Scale=Vector2.One*.6f;
            if(!crystal.ProtectPassage(side,.6f)) return false;
            crystal.speed=FallSpeed; crystal.MotionDifficulty=Difficulty;
            crystal.TriggerAppear(Vector2.One*.6f); crystal.Show(); crystal.SetDeferred("monitorable",true); crystal.ProcessMode=ProcessModeEnum.Inherit; return true;
        }
        return false;
    }

    public bool TryStartBasaltCollapse(int side,float y)
    {
        if(!IsGameActive || Zone!=CaveZone.Basalt || (_basaltCollapse?.Active??true) || (_crystalResonance?.Active??false) || (_magmaVent?.Active??false) || (_hud?.TutorialActive??false) || _coreRespite>0 || !float.IsFinite(y)) return false;
        float sourceDepth=CurrentDepth+y-(_player?.FixedY??-200);
        if(sourceDepth>TunnelLength-500 || RunDirector.ZoneAt(sourceDepth,CoreDepthPoint)!=CaveZone.Basalt || IsWaterInZone(y,Mathf.Max(400,FallSpeed*1.9f))) return false;
        foreach(var shooter in _shooters) if(shooter.Visible) return false;
        foreach(var shot in _projectiles) if(shot.Visible) return false;
        float radius=29f*.9f*SafePassage.AppearOvershoot;
        if(!CanHazardTraverse(side,y,radius,.88f) && !CanHazardTraverse(side,y,radius,1)) return false;
        _basaltCollapse.Activate(this,side,y); return true;
    }
    public bool TryReleaseBasaltFragment(int side,float y)
    {
        if(!IsGameActive || Zone!=CaveZone.Basalt || IsWaterInZone(y,Mathf.Max(400,FallSpeed*1.9f))) return false;
        foreach(Node2D node in obstaclePool)
        {
            if(node.Visible || node is not Obstacle rock) continue;
            GetRockBounds(y,out float left,out float right);
            rock.Position=new Vector2(side<0 ? left+70 : right-70,y);
            rock.ConfigureVariant(0,0,new HazardMotion(0,0,1.2f,0,0));
            rock.Scale=Vector2.One*.9f;
            if(!rock.ProtectPassage(side,.9f)) return false;
            rock.speed=FallSpeed; rock.MotionDifficulty=Difficulty;
            rock.TriggerAppear(Vector2.One*.9f); rock.Show(); rock.SetDeferred("monitorable",true); rock.ProcessMode=ProcessModeEnum.Inherit; return true;
        }
        return false;
    }

	private void SpawnFromPool(PackedScene scene, List<Node2D> pool, PickupKind pickupKind = PickupKind.Coin, Vector2? pickupPosition = null)
	{
		if (scene == null) return;
        // Sample the whole intent BEFORE suppression or capacity lookup. A skipped appearance
        // consumes exactly the same gameplay decisions as a successful one, without allocations.
        float x = 0f, scaleVariation = 1f, rotation = 0f;
        int variant = 0, side = 1; bool waterFromLeft = false; HazardMotion motion = default;
        if (scene == obstacleScene)
        {
            x = Director.NextFloat(-165,165); scaleVariation = Director.NextFloat(0.75f,SafePassage.MaxSpawnScale);
            rotation = Director.NextFloat(0,Mathf.Pi*2);
            int roll = Director.NextInt(0,100);
            variant = roll < 32 ? 0 : roll < 57 ? 1 : roll < 77 ? 3 : roll < 92 ? 4 : 5;
            if (Encounter == EncounterKind.Debris) variant = 1;
            if (ActivePlaySeconds < 8f && variant >= 4) variant = 0;
            side = Encounter == EncounterKind.Slalom ? (_encounterSide = -_encounterSide) : Director.NextInt(0,2)==0 ? -1 : 1;
            // Occupy the center-facing side of the winding escape route. A stationary
            // player must dodge; obstacles still leave enough space for the real hitbox.
            float lane = SafeLaneAt(1000f);
            if (Mathf.Abs(lane) > 35f) side = lane > 0f ? -1 : 1;
            x = 0f;
            motion = Obstacle.SampleMotion(Director);
        }
        else if (scene == waterScene) waterFromLeft = Director.NextInt(0,2) == 0;
        else if (!pickupPosition.HasValue) x = Director.NextFloat(-175,175);

		// Не спавнимо камінці поверх короткого бокового водоспаду.
        float approachSeconds = 640f / Mathf.Max(1f,_player?.HorizontalSpeed ?? 380f) + 0.25f;
        if (scene == obstacleScene && IsWaterInZone(1000f, Mathf.Max(400f,FallSpeed*approachSeconds)))
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
				waterNode.Position = spawnPosition;
				waterNode.GenerateRandomCaveGeometry(waterFromLeft);
			}
		}
		else if (scene == obstacleScene)
		{
			// Камені спавняться по всій ширині тунелю
			spawnPosition = new Vector2(x,variant==4 && Difficulty>0.4f
                ? (_player?.FixedY??-200)+FallSpeed*(Obstacle.WarningLeadSeconds*2+0.8f) : 1000f);
			Vector2 targetScale = new Vector2(scaleVariation, scaleVariation);
			obj.Scale = targetScale;
			obj.Rotation = rotation;

			if (obj is Obstacle obs)
			{
				obj.Position = spawnPosition;
                obs.ConfigureVariant(variant,_player?.Position.X ?? 0f,motion);
                if (!obs.ProtectPassage(side,scaleVariation) && !obs.ProtectPassage(-side,scaleVariation)) return;
                spawnPosition = obs.Position;
				obs.MotionDifficulty = Difficulty;
				obs.TriggerAppear(targetScale);
			}
		}
		else
		{
			// Монети та щити використовують повну ігрову ширину
			spawnPosition = pickupPosition ?? new Vector2(x,1000f);
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
        if(scene==coinScene && pickupKind==PickupKind.Battery) BatteriesSpawned++;
	}
}
