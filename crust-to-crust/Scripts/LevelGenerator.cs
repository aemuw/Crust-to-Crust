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

    private float[] lanes = { -225f, -75f, 75f, 225f };

    private float obstacleTimer = 0f;
    [Export] private float obstacleInterval = 1.5f;

    private float waterTimer = 0f;
    private float nextWaterInterval = 5f;
    
    private float coinTimer = 0f;
    private float nextCoinInterval = 2f;

    public float CurrentDepth = 0f;
    [Export] public float FallSpeed = 500f;
    
    [Export] public float CoreDepthPoint = 5000f;
    [Export] public float TunnelLength = 10000f;
    [Export] public float MinFallSpeed = 150f;
    [Export] public float SurfaceHangDuration = 1.5f;
    
    private float _initialFallSpeed;
    private bool _isHanging = false;
    private float _hangTimer = 0f;

    public override void _Ready()
    {
        _initialFallSpeed = FallSpeed;
        nextWaterInterval = (float)GD.RandRange(5.0, 10.0);
        nextCoinInterval = (float)GD.RandRange(1.0, 3.0); 
        
        InitializePool(obstacleScene, ObstaclePoolSize, obstaclePool);
        InitializePool(waterScene, WaterPoolSize, waterPool);
        InitializePool(coinScene, CoinPoolSize, coinPool);
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
        if (_isHanging)
        {
            _hangTimer += (float)delta;
            if (_hangTimer >= SurfaceHangDuration)
            {
                _isHanging = false;
                _hangTimer = 0f;
                CurrentDepth = 0f;
                FallSpeed = _initialFallSpeed;
            }
            return; // поки дивиться що навколо на поверхні - пауза спавну
        }

        CurrentDepth += FallSpeed * (float)delta;

        if (CurrentDepth < CoreDepthPoint)
        {
            // Прискорення при наближенні до ядра
            FallSpeed += 15f * (float)delta;
        }
        else if (CurrentDepth < TunnelLength)
        {
            // Сповільнення після прольоту ядра у бік іншого кінця планети
            FallSpeed -= 15f * (float)delta;
            if (FallSpeed < MinFallSpeed)
                FallSpeed = MinFallSpeed;
        }
        else
        {
            // Виліт з іншого кінця планети: зупиняється на секунду подивитися навколо
            _isHanging = true;
            FallSpeed = 0f;
        }
        
        obstacleTimer += (float)delta;
        if (obstacleTimer >= obstacleInterval)
        {
            obstacleTimer = 0f;
            SpawnFromPool(obstacleScene, obstaclePool);		
        }

        waterTimer += (float)delta;
        if (waterTimer >= nextWaterInterval)
        {
            waterTimer = 0f;
            nextWaterInterval = (float)GD.RandRange(5.0, 10.0);
            SpawnFromPool(waterScene, waterPool);
        }
        
        coinTimer += (float)delta;
        if (coinTimer >= nextCoinInterval)
        {
            coinTimer = 0f;
            nextCoinInterval = (float)GD.RandRange(1.0, 3.0);
            SpawnFromPool(coinScene, coinPool);
        }
    }

    private void SpawnFromPool(PackedScene scene, List<Node2D> pool)
    {
        if (scene == null)
            return;

        int randomLaneIndex = GD.RandRange(0, lanes.Length - 1);
        Vector2 spawnPosition = new Vector2(lanes[randomLaneIndex], 1000f);

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

        obj.Position = spawnPosition;
        obj.Visible = true;
        obj.ProcessMode = ProcessModeEnum.Inherit;
        obj.Set("speed", FallSpeed);
    }
}
