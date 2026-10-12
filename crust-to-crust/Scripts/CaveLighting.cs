using Godot;

public static class CaveLighting
{
    public static PointLight2D Light(Color color,float scale,float energy,bool shadows)
    {
        GameAssets.Load();
        return new PointLight2D { Texture = GameAssets.Halo,Color = color,TextureScale = scale,Energy = energy,
            ShadowEnabled = shadows,ShadowColor = new Color(0.01f,0.015f,0.03f,0.75f),
            ShadowFilter = Light2D.ShadowFilterEnum.Pcf5,RangeZMin = -20,RangeZMax = 20 };
    }
    public static void Occlude(Node2D parent,Vector2[] outline,int mask = 1)
    {
        var occluder = new LightOccluder2D { Occluder = new OccluderPolygon2D { Polygon = outline,CullMode = OccluderPolygon2D.CullModeEnum.Disabled } };
        occluder.Set("occluder_light_mask",mask); parent.AddChild(occluder);
    }
    public static void Setup(LevelGenerator level)
    {
        GameAssets.Load();
        level.CaveAmbient = new CanvasModulate { Name = "CaveAmbient",Color = new Color(0.075f,0.095f,0.13f) };
        level.AddChild(level.CaveAmbient);
        Shader rockShader = GD.Load<Shader>("res://Shaders/CaveRock.gdshader");
        Shader coreShader = GD.Load<Shader>("res://Shaders/CoreBackdrop.gdshader");
        var crystalStyle = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/CaveCrystal.gdshader") };
        level.CrystalMaterials.Add(crystalStyle);
        int section = 0;
        foreach (Node2D segment in level.GetNode<Node2D>("TunnelSegments").GetChildren())
        {
            // All wall slabs on the same side share one material (four per section).
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
                    if (rock.Visible && rock.IsInGroup("RockForeground")) Occlude(rock,rock.Polygon);
                }
                if (child is not Sprite2D crystal || !crystal.IsInGroup("TunnelCrystal")) continue;
                crystal.Texture = GameAssets.Crystal; crystal.Material = crystalStyle; crystal.Modulate = Colors.White;
                float correction = 627f/GameAssets.Crystal.GetWidth();
                crystal.Scale *= correction;
                foreach (Node area in crystal.GetChildren())
                    if (area is Area2D collisionArea) collisionArea.Scale /= correction;
                var light = Light(new Color("59dfe8"),1.05f/crystal.Scale.X,0.92f,false);
                light.Name = "CrystalLight"; light.Enabled = false;
                light.Position = new Vector2(crystal.Position.X < 0 ? 125 : -125,0)/correction;
                crystal.AddChild(light); level.CrystalLights.Add(light);
                foreach (Node area in crystal.GetChildren())
                    if (area is Area2D)
                        foreach (Node shape in area.GetChildren())
                            if (shape is CollisionPolygon2D polygon)
                            {
                                var collisionArea = (Area2D)area;
                                Occlude(collisionArea,polygon.Polygon,2);
                            }
            }
            section++;
        }
    }
}
