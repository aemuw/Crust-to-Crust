using Godot;

public partial class LevelGenerator : Node2D
{
	[Export] private PackedScene obstacleScene;
	private float[] lanes = { -300f, -100f, 100f, 300f };
	private float spawnTimer = 0f;
	[Export] private float spawnInterval = 1.5f;
	
	public override void _Process(double delta)
	{
		spawnTimer += (float)delta;
		if (spawnTimer >= spawnInterval)
		{
			spawnTimer = 0f;
			SpawnObstacle();
		}
	}
	
	private void SpawnObstacle()
	{
		if (obstacleScene == null)
			return;
			
		int randomLaneIndex = GD.RandRange(0, lanes.Length - 1);
		float spawnX = lanes[randomLaneIndex];
		Vector2 spawnPosition = new Vector2(spawnX, 1000f);
		
		Node2D obstacle = (Node2D)obstacleScene.Instantiate();		obstacle.Position = spawnPosition;
		GetTree().CurrentScene.AddChild(obstacle);
	}
}
