using Godot;

/// <summary>Load once before play, never decode/recreate textures on hazard spawning.</summary>
public static class GameAssets
{
    private static bool _loaded;
    public static SpriteFrames WaterFrames;
    public static ShaderMaterial WaterMaterial;
    public static readonly ShaderMaterial[] RuneStyles = new ShaderMaterial[3];
    public static Texture2D Crystal, Waterfall, Shield, Wheel, Halo, Beam, HotRock, Core, Coolant;
    public static SpriteFrames CoolantFrames;
    public static SpriteFrames CoinFrames;
    public static ShaderMaterial CoolantMaterial;
    public static readonly Texture2D[] HazardTextures = new Texture2D[6];
    public static readonly CanvasItemMaterial Unshaded = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        Crystal = GD.Load<Texture2D>("res://Textures/ArtRefresh/crystal_cyan.png");
        Waterfall = GD.Load<Texture2D>("res://Textures/ArtRefresh/waterfall.png");
        Shield = GD.Load<Texture2D>("res://Textures/ArtRefresh/shield_obsidian.png");
        Wheel = GD.Load<Texture2D>("res://Textures/ArtRefresh/magma_wheel.png");
        Halo = GD.Load<Texture2D>("res://Textures/ArtRefresh/light_halo.png");
        Beam = GD.Load<Texture2D>("res://Textures/ArtRefresh/flashlight_cone.png");
        HotRock = GD.Load<Texture2D>("res://Textures/ArtRefresh/cave_magma.png");
        Core = GD.Load<Texture2D>("res://Textures/ArtRefresh/core_chasm.png");
        HazardTextures[0] = GD.Load<Texture2D>("res://Textures/ArtRefresh/basalt_rock.png");
        HazardTextures[1] = GD.Load<Texture2D>("res://Textures/ArtRefresh/obsidian_shard.png");
        HazardTextures[2] = Crystal; HazardTextures[3] = Wheel;
        HazardTextures[4] = HazardTextures[1]; HazardTextures[5] = Wheel;
        WaterFrames = new SpriteFrames(); WaterFrames.RemoveAnimation("default");
        WaterFrames.AddAnimation("flow"); WaterFrames.AddFrame("flow",Waterfall);
        Coolant = GD.Load<Texture2D>("res://Textures/ArtRefresh/thermal_capsule.png");
        CoolantFrames = new SpriteFrames(); CoolantFrames.RemoveAnimation("default");
        CoolantFrames.AddAnimation("flow"); CoolantFrames.AddFrame("flow",Coolant);
        WaterMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/WaterfallV4.gdshader") };
        CoolantMaterial = new ShaderMaterial { Shader = WaterMaterial.Shader };
        CoolantMaterial.SetShaderParameter("powder_mode",true);
        CoinFrames = new SpriteFrames(); CoinFrames.RemoveAnimation("default"); CoinFrames.AddAnimation("Spin");
        foreach (int frame in new[] {1,2,3,2}) CoinFrames.AddFrame("Spin",GD.Load<Texture2D>($"res://Textures/ArtRefresh/Gold_{frame}.png"));
        CoinFrames.SetAnimationSpeed("Spin",9);
        Shader runeShader = GD.Load<Shader>("res://Shaders/PickupRune.gdshader");
        Color[] colors = { new Color("69e5ec"),new Color("ffc45b"),new Color("ce80ff") };
        for (int i = 0; i < 3; i++)
        { RuneStyles[i] = new ShaderMaterial { Shader = runeShader }; RuneStyles[i].SetShaderParameter("tint",colors[i]); }
    }
}
