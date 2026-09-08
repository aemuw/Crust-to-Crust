using Godot;
using System;

public partial class Water : Area2D
{
	[Export] public float speed = 500f;

	private Polygon2D _waterPolygon;
	private Polygon2D _leftWallPolygon;
	private Polygon2D _rightWallPolygon;
	private Line2D _leftWallBorder;
	private Line2D _rightWallBorder;
	private CollisionPolygon2D _waterShape;
	private CollisionPolygon2D _leftWallCol;
	private CollisionPolygon2D _rightWallCol;

	// Довжина печери 2800px — великий підземний річковий каньйон
	public const float CaveHeight = 2800f;
	public const float HalfHeight = 1400f;
	private const int SegmentCount = 56;

	public Color CurrentWallColor = new Color(0.18f, 0.16f, 0.15f);
	public Color CurrentBorderColor = new Color(0.45f, 0.40f, 0.35f);

	public override void _Ready()
	{
		_waterPolygon = GetNodeOrNull<Polygon2D>("WaterPolygon");
		_leftWallPolygon = GetNodeOrNull<Polygon2D>("LeftWallArea/LeftWallPolygon");
		_rightWallPolygon = GetNodeOrNull<Polygon2D>("RightWallArea/RightWallPolygon");
		_leftWallBorder = GetNodeOrNull<Line2D>("LeftWallBorder");
		_rightWallBorder = GetNodeOrNull<Line2D>("RightWallBorder");
		_waterShape = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");
		_leftWallCol = GetNodeOrNull<CollisionPolygon2D>("LeftWallArea/LeftCollision");
		_rightWallCol = GetNodeOrNull<CollisionPolygon2D>("RightWallArea/RightCollision");

		GenerateRandomCaveGeometry();
	}

	/// <summary>
	/// Generates an organic, twisting river cave with natural S-curves,
	/// changing widths (wide grottoes and narrow technical passes), and rocky relief.
	/// </summary>
	public void GenerateRandomCaveGeometry()
	{
		float stepY = CaveHeight / SegmentCount;
		
		Vector2[] leftInnerPoints = new Vector2[SegmentCount + 1];
		Vector2[] rightInnerPoints = new Vector2[SegmentCount + 1];

		float outerLeftX = -360f;
		float outerRightX = 360f;
		float normalTunnelHalfWidth = 350f;

		// Випадкові коефіцієнти для русла річки
		float turnDir = GD.RandRange(0, 1) == 0 ? 1f : -1f;
		float turnIntensity = (float)GD.RandRange(110.0, 150.0);
		float freq1 = (float)GD.RandRange(1.6, 2.4);
		float freq2 = (float)GD.RandRange(3.0, 4.2);
		float phase1 = (float)GD.RandRange(0.0, Mathf.Pi * 2.0);
		float phase2 = (float)GD.RandRange(0.0, Mathf.Pi * 2.0);

		// Випадкові позиції гротів (розширень) та звужень (chokepoints)
		float wideGrottoProg = (float)GD.RandRange(0.35, 0.65); // десь у центрі великий грот
		float narrowPassProg = wideGrottoProg < 0.5f ? (float)GD.RandRange(0.68, 0.82) : (float)GD.RandRange(0.20, 0.35);

		// Шуми скелястого берега
		float leftRoughA = (float)GD.RandRange(0.0, 100.0);
		float leftRoughB = (float)GD.RandRange(0.0, 100.0);
		float rightRoughA = (float)GD.RandRange(0.0, 100.0);
		float rightRoughB = (float)GD.RandRange(0.0, 100.0);

		for (int i = 0; i <= SegmentCount; i++)
		{
			float y = -HalfHeight + i * stepY;
			float progress = (float)i / SegmentCount; // 0.0 зверху -> 1.0 знизу

			// Огинаюча входу та виходу (швидкий плавний вхід замість лійки: 0.15 довжини на перехід)
			float entryWeight = Mathf.Clamp(progress / 0.12f, 0f, 1f);
			float exitWeight = Mathf.Clamp((1f - progress) / 0.12f, 0f, 1f);
			// Плавні криві входу/виходу
			entryWeight = entryWeight * entryWeight * (3f - 2f * entryWeight);
			exitWeight = exitWeight * exitWeight * (3f - 2f * exitWeight);
			float caveInfluence = entryWeight * exitWeight;

			// 1. Русло печери звивається по центру тунелю
			float riverCenter = (Mathf.Sin(progress * Mathf.Pi * freq1 + phase1) * 0.7f 
			                  + Mathf.Sin(progress * Mathf.Pi * freq2 + phase2) * 0.3f) 
			                  * turnIntensity * turnDir * caveInfluence;

			// 2. Змінна ширина печери (не монотонна лійка! чергування просторих гротів і звужень):
			// Базова напівширина ~170px
			float halfWidth = 175f;
			// Грот розширює печеру до 240px
			float grottoEffect = Mathf.Exp(-Mathf.Pow((progress - wideGrottoProg) / 0.16f, 2f)) * 75f;
			// Вузький прохід стискає до 120px
			float chokeEffect = Mathf.Exp(-Mathf.Pow((progress - narrowPassProg) / 0.12f, 2f)) * 60f;
			
			halfWidth += grottoEffect - chokeEffect;

			// 3. Скелястий рельєф берегів (різний для кожного боку)
			float leftNoise = Mathf.Sin(progress * 14f + leftRoughA) * 16f 
			                + Mathf.Cos(progress * 26f + leftRoughB) * 9f;
			float rightNoise = Mathf.Sin(progress * 15f + rightRoughA) * 16f 
			                 + Mathf.Cos(progress * 25f + rightRoughB) * 9f;

			float leftDistance = halfWidth + leftNoise;
			float rightDistance = halfWidth + rightNoise;

			// Стіни плавно переходять у звичайний тунель на вході та виході
			float leftX = Mathf.Lerp(-normalTunnelHalfWidth, riverCenter - leftDistance, caveInfluence);
			float rightX = Mathf.Lerp(normalTunnelHalfWidth, riverCenter + rightDistance, caveInfluence);

			// Захист від самоперетину та виходу за межі шахти
			leftX = Mathf.Clamp(leftX, -normalTunnelHalfWidth, riverCenter - 50f);
			rightX = Mathf.Clamp(rightX, riverCenter + 50f, normalTunnelHalfWidth);

			leftInnerPoints[i] = new Vector2(leftX, y);
			rightInnerPoints[i] = new Vector2(rightX, y);
		}

		// 1. Полігон води та колізія
		int pointCount = (SegmentCount + 1) * 2;
		Vector2[] waterPoints = new Vector2[pointCount];
		Vector2[] waterUVs = new Vector2[pointCount];

		for (int i = 0; i <= SegmentCount; i++)
		{
			waterPoints[i] = leftInnerPoints[i];
			waterUVs[i] = new Vector2(0f, (float)i / SegmentCount);
		}
		for (int i = 0; i <= SegmentCount; i++)
		{
			int srcIdx = SegmentCount - i;
			int dstIdx = SegmentCount + 1 + i;
			waterPoints[dstIdx] = rightInnerPoints[srcIdx];
			waterUVs[dstIdx] = new Vector2(1f, (float)srcIdx / SegmentCount);
		}

		if (_waterPolygon != null)
		{
			_waterPolygon.Polygons = new Godot.Collections.Array(); // Clear any custom triangles
			_waterPolygon.Polygon = waterPoints;
		}
		if (_waterShape != null) _waterShape.Polygon = waterPoints;

		// 2. Ліва кам'яна стіна, що звужує тунель
		Vector2[] leftWallPoints = new Vector2[(SegmentCount + 1) * 2];
		for (int i = 0; i <= SegmentCount; i++)
		{
			leftWallPoints[i] = new Vector2(outerLeftX, -HalfHeight + i * stepY);
		}
		for (int i = 0; i <= SegmentCount; i++)
		{
			int srcIdx = SegmentCount - i;
			leftWallPoints[SegmentCount + 1 + i] = leftInnerPoints[srcIdx];
		}

		if (_leftWallPolygon != null)
		{
			_leftWallPolygon.Polygons = new Godot.Collections.Array();
			_leftWallPolygon.Polygon = leftWallPoints;
			_leftWallPolygon.Color = CurrentWallColor;
		}
		if (_leftWallCol != null) _leftWallCol.Polygon = leftWallPoints;
		if (_leftWallBorder != null)
		{
			_leftWallBorder.Points = leftInnerPoints;
			_leftWallBorder.DefaultColor = CurrentBorderColor;
		}

		// 3. Права кам'яна стіна, що звужує тунель
		Vector2[] rightWallPoints = new Vector2[(SegmentCount + 1) * 2];
		for (int i = 0; i <= SegmentCount; i++)
		{
			rightWallPoints[i] = rightInnerPoints[i];
		}
		for (int i = 0; i <= SegmentCount; i++)
		{
			int srcIdx = SegmentCount - i;
			rightWallPoints[SegmentCount + 1 + i] = new Vector2(outerRightX, -HalfHeight + srcIdx * stepY);
		}

		if (_rightWallPolygon != null)
		{
			_rightWallPolygon.Polygons = new Godot.Collections.Array();
			_rightWallPolygon.Polygon = rightWallPoints;
			_rightWallPolygon.Color = CurrentWallColor;
		}
		if (_rightWallCol != null) _rightWallCol.Polygon = rightWallPoints;
		if (_rightWallBorder != null)
		{
			_rightWallBorder.Points = rightInnerPoints;
			_rightWallBorder.DefaultColor = CurrentBorderColor;
		}
	}

	public void UpdateBiomeColors(Color wallColor, Color borderColor)
	{
		CurrentWallColor = wallColor;
		CurrentBorderColor = borderColor;

		if (_leftWallPolygon != null) _leftWallPolygon.Color = wallColor;
		if (_rightWallPolygon != null) _rightWallPolygon.Color = wallColor;
		if (_leftWallBorder != null) _leftWallBorder.DefaultColor = borderColor;
		if (_rightWallBorder != null) _rightWallBorder.DefaultColor = borderColor;
	}

	public override void _Process(double delta)
	{
		Position += new Vector2(0, -speed * (float)delta);
		if (Position.Y < -1800f)
		{
			Visible = false;
			ProcessMode = ProcessModeEnum.Disabled;	
		}	
	}
}
