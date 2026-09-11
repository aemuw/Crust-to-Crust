using Godot;

public partial class Water : Area2D
{
	[Export] public float speed = 500f;
	public const float CaveHeight = 1900f;
	public const float HalfHeight = 950f;
	private const int SegmentCount = 30;

	private Polygon2D _waterPolygon;
	private CollisionPolygon2D _waterShape;

	public override void _Ready()
	{
		_waterPolygon = GetNodeOrNull<Polygon2D>("WaterPolygon");
		_waterShape = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");
		GenerateRandomCaveGeometry();
	}

	// Назва методу збережена для пулу LevelGenerator. Замість печери він
	// створює довгий водоспад від випадково обраної стіни тунелю.
	public void GenerateRandomCaveGeometry()
	{
		bool fromLeft = GD.RandRange(0, 1) == 0;
		// Початок завіси знаходиться всередині видимого краю тунелю,
		// а не за кам'яною стіною, тому гравець гарантовано може її торкнутися.
		float wallX = fromLeft ? -305f : 305f;
		Vector2[] wallEdge = new Vector2[SegmentCount + 1];
		Vector2[] curtainEdge = new Vector2[SegmentCount + 1];

		for (int i = 0; i <= SegmentCount; i++)
		{
			float progress = (float)i / SegmentCount;
			float y = -HalfHeight + progress * CaveHeight;
			float ripple = Mathf.Sin(progress * Mathf.Pi * 7f) * 14f
				+ Mathf.Sin(progress * Mathf.Pi * 13f + 0.8f) * 7f;
			float width = 185f + Mathf.Sin(progress * Mathf.Pi * 3f + 0.4f) * 28f + ripple;
			wallEdge[i] = new Vector2(wallX, y);
			curtainEdge[i] = new Vector2(wallX + (fromLeft ? width : -width), y);
		}

		Vector2[] points = new Vector2[(SegmentCount + 1) * 2];
		for (int i = 0; i <= SegmentCount; i++) points[i] = wallEdge[i];
		for (int i = 0; i <= SegmentCount; i++)
			points[SegmentCount + 1 + i] = curtainEdge[SegmentCount - i];

		if (_waterPolygon != null)
		{
			_waterPolygon.Polygons = new Godot.Collections.Array();
			_waterPolygon.Polygon = points;
		}
		if (_waterShape != null) _waterShape.Polygon = points;
	}

	public void UpdateBiomeColors(Color wallColor, Color borderColor)
	{
		if (_waterPolygon == null) return;
		float heat = Mathf.Clamp(wallColor.R * 2.2f, 0f, 0.7f);
		_waterPolygon.Color = new Color(0.08f + heat * 0.10f, 0.62f - heat * 0.12f, 0.82f - heat * 0.08f, 0.82f);
	}

	public override void _Process(double delta)
	{
		Position += new Vector2(0f, -speed * (float)delta);
		if (Position.Y < -1700f)
		{
			Visible = false;
			ProcessMode = ProcessModeEnum.Disabled;
		}
	}
}
