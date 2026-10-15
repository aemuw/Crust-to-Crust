using Godot;

/// <summary>Textured world props. Atlas regions keep moving parts separate without changing collision geometry.</summary>
public static class CaveArt
{
    private static Texture2D Crop(string file, Rect2 region) => new AtlasTexture { Atlas=GD.Load<Texture2D>("res://Textures/ArtRefresh/"+file+".png"), Region=region, FilterClip=true };
    public static readonly Texture2D Basalt=Crop("basalt_fracture",new Rect2(158,55,537,1856));
    public static readonly Texture2D Crystal=Crop("crystal_fracture",new Rect2(297,15,537,1506));
    public static readonly Texture2D Magma=Crop("magma_fracture",new Rect2(151,65,633,1644));
    public static readonly Texture2D Mount=Crop("drill_parts",new Rect2(97,9,463,516));
    public static readonly Texture2D Rod=Crop("drill_parts",new Rect2(686,230,813,112));
    public static readonly Texture2D Cutter=Crop("drill_parts",new Rect2(146,527,480,479));
    public static readonly Texture2D Cable=Crop("drill_parts",new Rect2(1070,525,137,479));
    // All source art grows rightwards from its embedded rock backing.
    public static void Wall(CanvasItem canvas, Texture2D texture, Vector2 root, int side, Rect2 rect, Color tint)
    {
        canvas.DrawSetTransform(root,0,new Vector2(side<0 ? 1 : -1,1));
        canvas.DrawTextureRect(texture,rect,false,tint);
        canvas.DrawSetTransform(Vector2.Zero);
    }
}
