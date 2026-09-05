using Godot;

public partial class LevelGenerator : Node2D
{
	[Export] private PackedScene obstacleScene;
	[Export] private PackedScene waterScene;

	private float[] lanes = { -225f, -75f, 75f, 225f };

	private float obstacleTimer = 0f;
	[Export] private float obstacleInterval = 1.5f;

	private float waterTimer = 0f;
	private float nextWaterInterval = 5f;

	public float CurrentDepth = 0f;
	[Export] public float FallSpeed = 500f;

	public override void _Ready()
	{
		nextWaterInterval = (float)GD.RandRange(5.0, 10.0);
	}

	public override void _Process(double delta)
	{
		CurrentDepth += FallSpeed * (float)delta;
		FallSpeed += 10f * (float)delta;
		
		obstacleTimer += (float)delta;
		if (obstacleTimer >= obstacleInterval)
		{
			obstacleTimer = 0f;
			SpawnObstacle();
		}

		waterTimer += (float)delta;
		if (waterTimer >= nextWaterInterval)
		{
			waterTimer = 0f;
			nextWaterInterval = (float)GD.RandRange(5.0, 10.0); //наступна вода знову через 5-10 сек
			SpawnWater();
		}
	}

	private void SpawnObstacle()
	{
		if (obstacleScene == null) 
			return;
		
		int randomLaneIndex = GD.RandRange(0, lanes.Length - 1);
		Vector2 spawnPosition = new Vector2(lanes[randomLaneIndex], 1000f);
		
		Node2D obstacle = (Node2D)obstacleScene.Instantiate();
		obstacle.Position = spawnPosition;
		obstacle.Set("speed", FallSpeed);
		GetTree().CurrentScene.AddChild(obstacle);
	}

	private void SpawnWater()
	{
		if (waterScene == null)
			return;
		
		int randomLaneIndex = GD.RandRange(0, lanes.Length - 1);
		Vector2 spawnPosition = new Vector2(lanes[randomLaneIndex], 1000f);
		
		Node2D water = (Node2D)waterScene.Instantiate();
		water.Position = spawnPosition;
		water.Set("speed", FallSpeed);
		GetTree().CurrentScene.AddChild(water);
	}
}
