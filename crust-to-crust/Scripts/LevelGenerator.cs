using Godot;
using System.Collections.Generic;

public partial class LevelGenerator : Node2D
{
    [Export] private PackedScene obstacleScene;
    [Export] private PackedScene waterScene;
    [Export] private PackedScene coinScene; 

    [Export] public int ObstaclePoolSize = 10;
    [Export] public int WaterPoolSize = 5;
    [Export] public int CoinPoolSize = 15;

    private List<Node2D> obstaclePool = new List<Node2D>();
    private List<Node2D> waterPool = new List<Node2D>();
    private List<Node2D> coinPool = new List<Node2D>();

    private float obstacleTimer = 0f;
    [Export] private float obstacleInterval = 1.5f;

    private float waterTimer = 0f;
    private float nextWaterInterval = 5f;
    
    private float coinTimer = 0f;
    private float nextCoinInterval = 2f;

    public float CurrentDepth = 0f;
    [Export] public float FallSpeed = 550f; // Пришвидшено для динамічного відчуття польоту
    
    [Export] public float CoreDepthPoint = 20000f; // Відстань до ядра 20 000 метрів
    [Export] public float TunnelLength = 40000f;
    [Export] public float MinFallSpeed = 250f;
    [Export] public float SurfaceHangDuration = 1.5f;
    
    private float _initialFallSpeed;
    private bool _isHanging = false;
    private float _hangTimer = 0f;

    // Стіни тунелю для динамічної зміни кольору породи від глибини
    private ColorRect _leftWall;
    private ColorRect _rightWall;
    private ColorRect _leftWallBorder;
    private ColorRect _rightWallBorder;
    private Player _player;
    private Color _currentWallColor = new Color(0.18f, 0.16f, 0.15f);
    private Color _currentBorderColor = new Color(0.42f, 0.38f, 0.33f);
    private const float TunnelHalfWidth = 350f;
    private float _tunnelVisualDepth = 0f;
    private Texture2D _rockWallTexture;
    private Node2D _tunnelSegmentsRoot;
    private Node2D _surfaceRoot;
    private bool _useEditableTunnelScenes = true;
    private const float TunnelTemplateHeight = 720f;
    private static readonly float[] LeftTemplate = { 20f, 12f, 35f, 18f, 62f, 24f, 10f, 46f, 22f, 54f, 16f, 30f, 20f };
    private static readonly float[] RightTemplate = { 18f, 42f, 14f, 28f, 12f, 55f, 20f, 34f, 64f, 18f, 38f, 10f, 18f };

    public bool IsGameActive = false;

    public override void _Ready()
    {
        _initialFallSpeed = FallSpeed;
        nextWaterInterval = 14f; // На старті вода рідкісна, оскільки кубик ще холодний
        nextCoinInterval = (float)GD.RandRange(1.5, 3.0); 
        
        _leftWall = GetNodeOrNull<ColorRect>("TunnelWalls/LeftWall");
        _rightWall = GetNodeOrNull<ColorRect>("TunnelWalls/RightWall");
        _leftWallBorder = GetNodeOrNull<ColorRect>("TunnelWalls/LeftWallBorder");
        _rightWallBorder = GetNodeOrNull<ColorRect>("TunnelWalls/RightWallBorder");
        _player = GetNodeOrNull<Player>("Player");
        _rockWallTexture = GD.Load<Texture2D>("res://Textures/Generated/rock_wall_large.png");
        _tunnelSegmentsRoot = GetNodeOrNull<Node2D>("TunnelSegments");
        _surfaceRoot = GetNodeOrNull<Node2D>("TunnelWalls/Surface");
        TextureRepeat = TextureRepeatEnum.Enabled;

        // Старі прямі прямокутники замінюються процедурно намальованими стінами.
        if (_leftWall != null) _leftWall.Visible = false;
        if (_rightWall != null) _rightWall.Visible = false;
        if (_leftWallBorder != null) _leftWallBorder.Visible = false;
        if (_rightWallBorder != null) _rightWallBorder.Visible = false;
        QueueRedraw();

        var hud = GetNodeOrNull<Hud>("HUD");
        if (hud != null)
        {
            hud.StartRequested += StartGame;
        }

        InitializePool(obstacleScene, ObstaclePoolSize, obstaclePool);
        InitializePool(waterScene, WaterPoolSize, waterPool);
        InitializePool(coinScene, CoinPoolSize, coinPool);
        // Виставляємо стартовий біом до першого кадру гри, щоб колір не стрибав на Start.
        UpdateWallColors();
    }

    public void StartGame()
    {
        // Під час стрибка з поверхні секції ще не рухаються.
        IsGameActive = false;
    }

    public void BeginFalling()
    {
        // Після входу кубика в шахту поверхневі обриви більше не повинні
        // накладатися на готові секції підземного тунелю.
        if (_surfaceRoot != null)
            _surfaceRoot.Visible = false;
        IsGameActive = true;
    }

    private void InitializePool(PackedScene scene, int size, List<Node2D> pool)
    {
        if (scene == null) 
            return;

        for (int i = 0; i < size; i++)
        {
            Node2D obj = (Node2D)scene.Instantiate();
            obj.Visible = false;
            obj.ProcessMode = ProcessModeEnum.Disabled;
            AddChild(obj);
            pool.Add(obj);
        }
    }

    public override void _Process(double delta)
    {
        if (!IsGameActive) return;

        if (_isHanging)
        {
            // Під час польоту через інший бік планети активні об'єкти відключені
            return;
        }

        CurrentDepth += FallSpeed * (float)delta;
		_tunnelVisualDepth = CurrentDepth;
		ScrollEditableTunnelSegments((float)delta);

        if (CurrentDepth < CoreDepthPoint)
        {
            // Динамічне прискорення при падінні у бік ядра
            FallSpeed += 16f * (float)delta;
        }
        else if (CurrentDepth < TunnelLength)
        {
            // Гравітація сповільнює кубик після прольоту ядра до поверхні іншого боку
            FallSpeed -= 16f * (float)delta;
            if (FallSpeed < MinFallSpeed)
                FallSpeed = MinFallSpeed;
        }
        else
        {
            // Повний виліт з іншого кінця планети (40 000м)
            _isHanging = true;
            FallSpeed = 0f;

            // Ховаємо активні перешкоди під час переходу на інший бік планети
            DeactivateAllPoolObjects(obstaclePool);
            DeactivateAllPoolObjects(waterPool);
            DeactivateAllPoolObjects(coinPool);

            if (_player != null)
            {
                // Запуск кінематографічної анімації вильоту в небо, розвороту у невагомості і початку нового падіння
                _player.PlayEmergenceAnimation(() =>
                {
                    CurrentDepth = 0f;
                    FallSpeed = _initialFallSpeed;
                    _isHanging = false;
                });
            }
            else
            {
                CurrentDepth = 0f;
                FallSpeed = _initialFallSpeed;
                _isHanging = false;
            }
            return;
        }
        
        // Оновлюємо швидкість усіх активних об'єктів під поточну швидкість падіння
        UpdateActiveObjectsSpeed(obstaclePool);
        UpdateActiveObjectsSpeed(waterPool);
        UpdateActiveObjectsSpeed(coinPool);

        UpdateWallColors();

        obstacleTimer += (float)delta;
        if (obstacleTimer >= obstacleInterval)
        {
            obstacleTimer = 0f;
            SpawnFromPool(obstacleScene, obstaclePool);		
        }

        // Спавн води: на початку рідко (12-16с), а ближче до ядра (зона нагріву < 5000м) значно частіше (4-7с)
        waterTimer += (float)delta;
        if (waterTimer >= nextWaterInterval)
        {
            waterTimer = 0f;
            float distToCore = Mathf.Abs(CurrentDepth - CoreDepthPoint);
            if (distToCore < 5000f)
            {
                // Гаряча зона біля ядра: рятівна вода з'являється набагато частіше
                nextWaterInterval = (float)GD.RandRange(4.0, 7.0);
            }
            else
            {
                // Верхні холодні пласти: вода спавниться рідко
                nextWaterInterval = (float)GD.RandRange(12.0, 18.0);
            }
            SpawnFromPool(waterScene, waterPool);
        }
        
        coinTimer += (float)delta;
        if (coinTimer >= nextCoinInterval)
        {
            coinTimer = 0f;
            nextCoinInterval = (float)GD.RandRange(1.5, 3.5);
            SpawnFromPool(coinScene, coinPool);
        }
    }

	private float SampleTemplate(float worldY, float[] template)
	{
		float localY = Mathf.PosMod(worldY, TunnelTemplateHeight);
		float scaled = localY / TunnelTemplateHeight * (template.Length - 1);
		int index = Mathf.FloorToInt(scaled);
		int next = Mathf.Min(index + 1, template.Length - 1);
		float t = scaled - index;
		t = t * t * (3f - 2f * t);
		return Mathf.Lerp(template[index], template[next], t);
	}

	public void GetTunnelBounds(float screenY, out float left, out float right)
	{
		// Окреме візуальне зміщення змінюється лише під час активної гри.
		// Тому в головному меню рельєф гарантовано стоїть на місці.
		float worldY = _tunnelVisualDepth + screenY;
		left = -TunnelHalfWidth + SampleTemplate(worldY, LeftTemplate);
		right = TunnelHalfWidth - SampleTemplate(worldY, RightTemplate);
	}

	public override void _Draw()
	{
		// Основний тунель тепер зібраний вручну в TunnelSegment.tscn.
		// Старий код залишено лише як резерв, але він не використовується.
		if (_useEditableTunnelScenes) return;
		const float top = -650f;
		const float bottom = 1050f;
		Color wallTint = GetRockTextureTint();

		// Темна порода всередині тунелю рухається повільніше за передні стіни (parallax).
		Vector2[] backdrop =
		{
			new Vector2(-1200f, top), new Vector2(1200f, top),
			new Vector2(1200f, bottom), new Vector2(-1200f, bottom)
		};
		float parallaxY = _tunnelVisualDepth * 0.18f;
		Vector2[] backdropUv =
		{
			new Vector2(0f, (top + parallaxY) * 0.12f), new Vector2(288f, (top + parallaxY) * 0.12f),
			new Vector2(288f, (bottom + parallaxY) * 0.12f), new Vector2(0f, (bottom + parallaxY) * 0.12f)
		};
		Color backdropTint = new Color(wallTint.R * 0.30f, wallTint.G * 0.30f, wallTint.B * 0.30f);
		DrawColoredPolygon(backdrop, backdropTint, backdropUv, _rockWallTexture);

		// Малюємо готові секції цілком і лише зсуваємо їх по Y.
		// Геометрія не перераховується по екранних зрізах, тому краї більше не дригаються.
		float firstSectionY = -Mathf.PosMod(_tunnelVisualDepth, TunnelTemplateHeight) - TunnelTemplateHeight;
		for (float sectionY = firstSectionY; sectionY < bottom + TunnelTemplateHeight; sectionY += TunnelTemplateHeight)
		{
			DrawTunnelTemplateSection(sectionY, wallTint);
		}
	}

	private void ScrollEditableTunnelSegments(float delta)
	{
		if (_tunnelSegmentsRoot == null) return;
		const float loopHeight = TunnelTemplateHeight * 5f;
		foreach (Node child in _tunnelSegmentsRoot.GetChildren())
		{
			if (child is not Node2D segment) continue;
			segment.Position += new Vector2(0f, -FallSpeed * delta);
			if (segment.Position.Y < -TunnelTemplateHeight * 2f)
				segment.Position += new Vector2(0f, loopHeight);
		}
	}

	private Color GetRockTextureTint()
	{
		return new Color(
			Mathf.Clamp(0.66f + _currentWallColor.R * 1.45f, 0.76f, 1f),
			Mathf.Clamp(0.52f + _currentWallColor.G * 1.25f, 0.54f, 0.82f),
			Mathf.Clamp(0.48f + _currentWallColor.B * 1.15f, 0.50f, 0.76f));
	}

	private void DrawTunnelTemplateSection(float sectionY, Color textureTint)
	{
		int count = LeftTemplate.Length;
		Vector2[] leftInner = new Vector2[count];
		Vector2[] rightInner = new Vector2[count];
		for (int i = 0; i < count; i++)
		{
			float y = sectionY + TunnelTemplateHeight * i / (count - 1);
			leftInner[i] = new Vector2(-TunnelHalfWidth + LeftTemplate[i], y);
			rightInner[i] = new Vector2(TunnelHalfWidth - RightTemplate[i], y);
		}

		Vector2[] leftPolygon = new Vector2[count + 2];
		leftPolygon[0] = new Vector2(-1200f, sectionY);
		for (int i = 0; i < count; i++) leftPolygon[i + 1] = leftInner[i];
		leftPolygon[count + 1] = new Vector2(-1200f, sectionY + TunnelTemplateHeight);

		Vector2[] rightPolygon = new Vector2[count + 2];
		for (int i = 0; i < count; i++) rightPolygon[i] = rightInner[i];
		rightPolygon[count] = new Vector2(1200f, sectionY + TunnelTemplateHeight);
		rightPolygon[count + 1] = new Vector2(1200f, sectionY);

		Vector2[] leftUv = new Vector2[leftPolygon.Length];
		Vector2[] rightUv = new Vector2[rightPolygon.Length];
		for (int i = 0; i < leftPolygon.Length; i++) leftUv[i] = (leftPolygon[i] + new Vector2(1200f, _tunnelVisualDepth)) * 0.12f;
		for (int i = 0; i < rightPolygon.Length; i++) rightUv[i] = (rightPolygon[i] + new Vector2(1200f, _tunnelVisualDepth)) * 0.12f;

		DrawColoredPolygon(leftPolygon, textureTint, leftUv, _rockWallTexture);
		DrawColoredPolygon(rightPolygon, textureTint, rightUv, _rockWallTexture);
		DrawPolyline(leftInner, _currentBorderColor, 5f, true);
		DrawPolyline(rightInner, _currentBorderColor, 5f, true);
		DrawTemplateDecorations(sectionY, textureTint);
	}

	private void DrawTemplateDecorations(float sectionY, Color textureTint)
	{
        // Великі кам'яні блоки є частиною самої заготовки й рухаються разом із нею.
        Vector2[] leftBlock =
        {
            new Vector2(-318f, sectionY + 205f), new Vector2(-255f, sectionY + 218f),
            new Vector2(-205f, sectionY + 278f), new Vector2(-231f, sectionY + 346f),
            new Vector2(-305f, sectionY + 366f)
        };
        Vector2[] rightBlock =
        {
            new Vector2(315f, sectionY + 474f), new Vector2(248f, sectionY + 452f),
            new Vector2(194f, sectionY + 505f), new Vector2(216f, sectionY + 573f),
            new Vector2(304f, sectionY + 598f)
        };
        DrawFormation(leftBlock, textureTint);
        DrawFormation(rightBlock, textureTint);

        // Дві контрастні кристалічні групи, також у фіксованих місцях шаблону.
        Color crystal = _currentWallColor.R > 0.22f
            ? new Color(1f, 0.28f, 0.08f, 0.92f)
            : new Color(0.38f, 0.70f, 0.82f, 0.88f);
        DrawCrystalCluster(new Vector2(-300f, sectionY + 540f), 1f, crystal);
        DrawCrystalCluster(new Vector2(300f, sectionY + 150f), -1f, crystal);
    }

    private void DrawCrystalCluster(Vector2 origin, float direction, Color color)
    {
        DrawColoredPolygon(new[]
        {
            origin, origin + new Vector2(55f * direction, -18f), origin + new Vector2(18f * direction, 10f)
        }, color);
        DrawColoredPolygon(new[]
        {
            origin + new Vector2(4f * direction, 7f), origin + new Vector2(38f * direction, -58f), origin + new Vector2(26f * direction, 12f)
        }, color.Lightened(0.12f));
        DrawColoredPolygon(new[]
        {
            origin + new Vector2(20f * direction, 10f), origin + new Vector2(72f * direction, -30f), origin + new Vector2(43f * direction, 20f)
        }, color.Darkened(0.12f));
    }

    private void DrawFormation(Vector2[] points, Color tint)
    {
        Vector2[] uv = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) uv[i] = (points[i] + new Vector2(1200f, _tunnelVisualDepth)) * 0.12f;
        DrawColoredPolygon(points, tint, uv, _rockWallTexture);
        DrawPolyline(points, _currentBorderColor, 4f, true);
    }

    private void DeactivateAllPoolObjects(List<Node2D> pool)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            pool[i].Visible = false;
            pool[i].ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    private void UpdateActiveObjectsSpeed(List<Node2D> pool)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Visible)
            {
                pool[i].Set("speed", FallSpeed);
            }
        }
    }

    // Текстура/колір шарів землі змінюється з глибиною (поверхня -> камінь -> розпечена магма біля ядра)
    private void UpdateWallColors()
    {
        if (_leftWall == null || _rightWall == null) return;

        float progressToCore = Mathf.Clamp(CurrentDepth / CoreDepthPoint, 0f, 1f);

        Color wallColor;
        Color borderColor;

        if (progressToCore < 0.35f)
        {
            // 0 - 7000м: кам'янисто-земельні сіро-коричневі породи
            float t = progressToCore / 0.35f;
            wallColor = new Color(0.18f, 0.16f, 0.15f).Lerp(new Color(0.15f, 0.13f, 0.16f), t);
            borderColor = new Color(0.42f, 0.38f, 0.33f).Lerp(new Color(0.35f, 0.30f, 0.35f), t);
        }
        else if (progressToCore < 0.75f)
        {
            // 7000 - 15000м: глибокий базальт, з'являються червонувато-темні відтінки
            float t = (progressToCore - 0.35f) / 0.40f;
            wallColor = new Color(0.15f, 0.13f, 0.16f).Lerp(new Color(0.24f, 0.08f, 0.06f), t);
            borderColor = new Color(0.35f, 0.30f, 0.35f).Lerp(new Color(0.55f, 0.20f, 0.12f), t);
        }
        else
        {
            // 15000 - 20000м: ядро планети - розпечена магма, вулканічні стіни
            float t = (progressToCore - 0.75f) / 0.25f;
            wallColor = new Color(0.24f, 0.08f, 0.06f).Lerp(new Color(0.35f, 0.06f, 0.04f), t);
            borderColor = new Color(0.55f, 0.20f, 0.12f).Lerp(new Color(0.95f, 0.45f, 0.10f), t);
        }

        _leftWall.Color = wallColor;
        _rightWall.Color = wallColor;
        if (_leftWallBorder != null) _leftWallBorder.Color = borderColor;
        if (_rightWallBorder != null) _rightWallBorder.Color = borderColor;
        _currentWallColor = wallColor;
        _currentBorderColor = borderColor;
        Color editableTint = GetRockTextureTint();
        foreach (Node node in GetTree().GetNodesInGroup("RockForeground"))
            if (node is CanvasItem item) item.Modulate = editableTint;
        foreach (Node node in GetTree().GetNodesInGroup("RockBackground"))
            if (node is CanvasItem item) item.Modulate = new Color(editableTint.R * 0.30f, editableTint.G * 0.30f, editableTint.B * 0.30f);
        Color crystalTint = wallColor.R > 0.22f ? new Color(1f, 0.24f, 0.06f) : new Color(0.34f, 0.76f, 0.92f);
        foreach (Node node in GetTree().GetNodesInGroup("Crystal"))
            if (node is CanvasItem item) item.Modulate = crystalTint;
        QueueRedraw();

        // Синхронізуємо кольори активних водяних печер з шаром землі
        for (int i = 0; i < waterPool.Count; i++)
        {
            if (waterPool[i].Visible && waterPool[i] is Water activeWater)
            {
                activeWater.UpdateBiomeColors(wallColor, borderColor);
            }
        }
    }

    private bool IsWaterInZone(float targetY, float halfHeight = 1500f)
    {
        for (int i = 0; i < waterPool.Count; i++)
        {
            if (waterPool[i].Visible)
            {
                float waterY = waterPool[i].Position.Y;
                // Водяна печера має довжину 2800 (HalfHeight=1400). Якщо ціль потрапляє в зону печери:
                if (Mathf.Abs(waterY - targetY) < (1400f + halfHeight))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void SpawnFromPool(PackedScene scene, List<Node2D> pool)
    {
        if (scene == null)
            return;

        // Не спавнимо камінці, якщо на лінії спавну або поруч знаходиться водяна печера
        if (scene == obstacleScene && IsWaterInZone(1000f, 400f))
        {
            return;
        }

        Node2D obj = null;
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].Visible)
            {
                obj = pool[i];
                break;
            }
        }

        if (obj == null)
        {
            obj = (Node2D)scene.Instantiate();
            AddChild(obj);
            pool.Add(obj);
        }

        Vector2 spawnPosition;

        // Водяна печера звужує основний тунель по центру (X = 0)
        if (scene == waterScene)
        {
            // Спавнимо по центру тунелю. Довжина печери 2800px, тому починаємо з Y = 2200px
            spawnPosition = new Vector2(0f, 2200f);
            obj.Scale = Vector2.One;
            obj.Rotation = 0f;
            if (obj is Water waterNode)
            {
                if (_leftWall != null && _leftWallBorder != null)
                {
                    waterNode.UpdateBiomeColors(_leftWall.Color, _leftWallBorder.Color);
                }
                waterNode.GenerateRandomCaveGeometry();
            }
        }
        else if (scene == obstacleScene)
        {
            // Камені плавно і вільно спавняться по всій ширині тунелю (-220f .. +220f)
            float randomX = (float)GD.RandRange(-220.0, 220.0);
            spawnPosition = new Vector2(randomX, 1000f);

            // Камінці різних розмірів та випадкових поворотів
            float scaleVariation = (float)GD.RandRange(0.75, 1.35);
            Vector2 targetScale = new Vector2(scaleVariation, scaleVariation);
            obj.Scale = targetScale;
            obj.Rotation = (float)GD.RandRange(0, Mathf.Pi * 2);

            if (obj is Obstacle obs)
            {
                obs.TriggerAppear(targetScale);
            }
        }
        else
        {
            // Монетки також плавно розподіляються по ширині тунелю
            // Якщо є водяна печера, спавнимо вужче по центру тунелю
            float spawnWidth = IsWaterInZone(1000f, 750f) ? 80f : 240f;
            float randomX = (float)GD.RandRange(-spawnWidth, spawnWidth);
            spawnPosition = new Vector2(randomX, 1000f);
            obj.Scale = Vector2.One;
            obj.Rotation = 0f;

            if (obj is Coin coin)
            {
                coin.TriggerAppear();
            }
        }

        obj.Position = spawnPosition;
        obj.Visible = true;
        obj.ProcessMode = ProcessModeEnum.Inherit;
        obj.Set("speed", FallSpeed);
    }
}
