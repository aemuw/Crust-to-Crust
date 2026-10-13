using Godot;

public static class CaveLighting
{
    private static readonly Color ShadowTint = new Color(0.01f,0.015f,0.03f,0.75f);
    private static readonly Color CrystalColor = new Color("59dfe8");

    public static PointLight2D Light(Color color,float scale,float energy,bool shadows)
    {
        GameAssets.Load();
        var light = new PointLight2D { Texture = GameAssets.Halo,Color = color,TextureScale = scale,Energy = energy,
            RangeZMin = -20,RangeZMax = 20 };
        // Параметри тіней задаємо лише для світла з тінями: для решти вони не потрібні.
        if (shadows)
        {
            light.ShadowEnabled = true;
            light.ShadowColor = ShadowTint;
            light.ShadowFilter = Light2D.ShadowFilterEnum.Pcf5;
        }
        return light;
    }

    // Спрощення контурів вимкнено (0), щоб тіні фанарика були як в оригіналі.
    public static void Occlude(Node2D parent,Vector2[] outline,int mask = 1,float simplify = 0f)
    {
        var occluder = new LightOccluder2D { Occluder = new OccluderPolygon2D {
            Polygon = Simplify(outline,simplify),CullMode = OccluderPolygon2D.CullModeEnum.Disabled } };
        occluder.Set("occluder_light_mask",mask); parent.AddChild(occluder);
    }

    // Спрощення замкненого контуру (Дуглас-Пекер): менше вершин = дешевші тіні.
    public static Vector2[] Simplify(Vector2[] pts,float eps)
    {
        int n = pts.Length;
        if (n <= 4 || eps <= 0f) return pts;
        int far = 0; float best = 0f;
        for (int i = 1; i < n; i++)
        {
            float d = pts[0].DistanceSquaredTo(pts[i]);
            if (d > best) { best = d; far = i; }
        }
        var keep = new bool[n + 1];
        keep[0] = keep[far] = keep[n] = true;
        var ext = new Vector2[n + 1];
        pts.CopyTo(ext,0); ext[n] = pts[0];
        DouglasPeucker(ext,0,far,eps,keep);
        DouglasPeucker(ext,far,n,eps,keep);
        var result = new System.Collections.Generic.List<Vector2>(n);
        for (int i = 0; i < n; i++) if (keep[i]) result.Add(pts[i]);
        return result.Count >= 3 ? result.ToArray() : pts;
    }

    private static void DouglasPeucker(Vector2[] p,int a,int b,float eps,bool[] keep)
    {
        if (b <= a + 1) return;
        float max = 0f; int idx = -1;
        for (int i = a + 1; i < b; i++)
        {
            float d = p[i].DistanceTo(Geometry2D.GetClosestPointToSegment(p[i],p[a],p[b]));
            if (d > max) { max = d; idx = i; }
        }
        if (idx < 0 || max <= eps) return;
        keep[idx] = true;
        DouglasPeucker(p,a,idx,eps,keep);
        DouglasPeucker(p,idx,b,eps,keep);
    }

    public static void Setup(LevelGenerator level)
    {
        GameAssets.Load();
        level.CaveAmbient = new CanvasModulate { Name = "CaveAmbient",Color = new Color(0.08f,0.10f,0.14f) };
        level.AddChild(level.CaveAmbient);
        Shader rockShader = GD.Load<Shader>("res://Shaders/CaveRock.gdshader");
        Shader coreShader = GD.Load<Shader>("res://Shaders/CoreBackdrop.gdshader");
        var crystalStyle = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/CaveCrystal.gdshader") };
        level.CrystalMaterials.Add(crystalStyle);

        // Один вузол на всі кристали замість окремого скрипта/твіна на кожному.
        var pulse = new CaveLightPulse { Name = "CaveLightPulse" };
        level.AddChild(pulse);

        // Коригувальний множник однаковий для всіх кристалів: рахуємо раз.
        float correction = 627f/GameAssets.Crystal.GetWidth();

        int section = 0;
        foreach (Node2D segment in level.GetNode<Node2D>("TunnelSegments").GetChildren())
        {
            // Усі стінові плити з одного боку ділять один матеріал (по чотири на секцію).
            var styles = new ShaderMaterial[4];
            for (int i = 0; i < 4; i++)
            {
                styles[i] = new ShaderMaterial { Shader = i == 3 ? coreShader : rockShader };
                styles[i].SetShaderParameter("hot_texture",i == 3 ? GameAssets.Core : GameAssets.HotRock);
                styles[i].SetShaderParameter("uv_offset",new Vector2(section*0.137f,section*0.193f));
                if (i < 3) styles[i].SetShaderParameter("edge_mode",i);
                level.RockMaterials.Add(styles[i]); level.RockMaterialOwners.Add(segment);
            }
            foreach (Node child in segment.GetChildren())
            {
                if (child is Polygon2D rock)
                {
                    string name = rock.Name.ToString();
                    rock.Modulate = Colors.White;
                    rock.Material = styles[name == "DeepRock" ? 3 : name.StartsWith("LeftWall") ? 1 : name.StartsWith("RightWall") ? 2 : 0];
                    rock.TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
                    if (rock.Visible && rock.IsInGroup("RockForeground")) Occlude(rock,rock.Polygon,1);
                }
                if (child is not Sprite2D crystal || !crystal.IsInGroup("TunnelCrystal")) continue;
                crystal.Texture = GameAssets.Crystal; crystal.Material = crystalStyle; crystal.Modulate = Colors.White;
                crystal.Scale *= correction;

                var light = Light(CrystalColor,1.05f/crystal.Scale.X,0.92f,false);
                light.Name = "CrystalLight"; light.Enabled = false;
                light.Position = new Vector2(crystal.Position.X < 0 ? 125 : -125,0)/correction;

                // Один прохід по дітях замість трьох: масштаб зон і оклюдери разом.
                foreach (Node node in crystal.GetChildren())
                {
                    if (node is not Area2D area) continue;
                    area.Scale /= correction;
                    foreach (Node shape in area.GetChildren())
                        if (shape is CollisionPolygon2D polygon)
                            Occlude(area,polygon.Polygon,2);
                }
                crystal.AddChild(light); level.CrystalLights.Add(light);
                pulse.Register(light);
            }
            section++;
        }
    }
}
