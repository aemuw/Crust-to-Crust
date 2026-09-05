using Godot;

public partial class Water : Area2D
{
	[Export] public float speed = 500f;
	
	public override void _Process(double delta)
	{
		Position += new Vector2(0, -speed * (float)delta);
		if (Position.Y < -500f)
		{
			Visible = false;
			ProcessMode = ProcessModeEnum.Disabled;	
		}	
	}
}
