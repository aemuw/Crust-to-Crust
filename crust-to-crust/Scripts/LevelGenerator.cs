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

        var hud = GetNodeOrNull<Hud>("HUD");
        if (hud != null)
        {
            hud.StartRequested += StartGame;
        }

        InitializePool(obstacleScene, ObstaclePoolSize, obstaclePool);
        InitializePool(waterScene, WaterPoolSize, waterPool);
        InitializePool(coinScene, CoinPoolSize, coinPool);
    }

    public void StartGame()
    {
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
