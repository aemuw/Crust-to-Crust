using Godot;

/// <summary>Load once before play, never decode/recreate textures on hazard spawning.</summary>
public static class GameAssets
{
    private static bool _loaded;
    public static SpriteFrames WaterFrames;
    public static ShaderMaterial WaterMaterial;
    public static readonly ShaderMaterial[] RuneStyles = new ShaderMaterial[3];
    public static Texture2D Crystal, Waterfall, Shield, Wheel, Halo, Beam, HotRock, Core;
    public static readonly Texture2D[] HazardTextures = new Texture2D[6];
    public static readonly CanvasItemMaterial Unshaded = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        Crystal = GD.Load<Texture2D>("res://Textures/Generated/crystal_geode_v4.png");
        Waterfall = GD.Load<Texture2D>("res://Textures/Generated/waterfall_v4.png");
        Shield = GD.Load<Texture2D>("res://Textures/Generated/shield_obsidian_v4.png");
        Wheel = GD.Load<Texture2D>("res://Textures/Generated/magma_wheel_v4.png");
        Halo = GD.Load<Texture2D>("res://Textures/Generated/light_halo_painted.png");
        Beam = GD.Load<Texture2D>("res://Textures/Generated/flashlight_cone.png");
        HotRock = GD.Load<Texture2D>("res://Textures/Generated/cave_magma_painted.png");
        Core = GD.Load<Texture2D>("res://Textures/Generated/core_chasm_v4.png");
        var atlas = GD.Load<Texture2D>("res://Textures/Generated/hazards_atlas.png");
        float cell = atlas.GetWidth()/3f;
        for (int i = 0; i < 2; i++) HazardTextures[i] = new AtlasTexture { Atlas = atlas, Region = new Rect2(cell*i,0,cell,atlas.GetHeight()) };
        HazardTextures[2] = Crystal; HazardTextures[3] = Wheel;
        HazardTextures[4] = HazardTextures[1]; HazardTextures[5] = Wheel;
        WaterFrames = new SpriteFrames(); WaterFrames.RemoveAnimation("default");
        WaterFrames.AddAnimation("flow"); WaterFrames.AddFrame("flow",Waterfall);
        WaterMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/WaterfallV4.gdshader") };
        Shader runeShader = GD.Load<Shader>("res://Shaders/PickupRune.gdshader");
        Color[] colors = { new Color("69e5ec"),new Color("ffc45b"),new Color("ce80ff") };
        for (int i = 0; i < 3; i++)
        { RuneStyles[i] = new ShaderMaterial { Shader = runeShader }; RuneStyles[i].SetShaderParameter("tint",colors[i]); }
    }
}
