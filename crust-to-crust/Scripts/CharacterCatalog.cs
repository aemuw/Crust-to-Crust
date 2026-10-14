using Godot;

public sealed record CharacterDefinition(int Id,string Name,string TexturePath,int Price,float DisplayHeight,float EnvelopeRadius)
{
    public Shape2D CreateBodyShape() => Id switch
    {
        3 => new CapsuleShape2D { Radius=16,Height=64 },
        4 => new CapsuleShape2D { Radius=21,Height=60 },
        5 => new ConvexPolygonShape2D { Points=new[] { new Vector2(-16,-28),new Vector2(16,-28),new Vector2(25,-9),new Vector2(21,26),new Vector2(-21,26),new Vector2(-25,-9) } },
        _ => new RectangleShape2D { Size=new Vector2(48,48) }
    };
}

/// <summary>Stable save IDs; art, collision and shop all consume the same definition.</summary>
public static class CharacterCatalog
{
    private const string Root="res://Textures/ArtRefresh/";
    private static readonly CharacterDefinition[] Entries =
    {
        new(0,"Базальт",Root+"hero_basalt.png",0,52,46),
        new(1,"Крижаний кварц",Root+"hero_ice.png",24,52,46),
        new(2,"Жар обсидіану",Root+"hero_ember.png",36,52,46),
        new(3,"Шахтар",Root+"hero_miner.png",48,70,48),
        new(4,"Буровий робот",Root+"hero_drillbot.png",60,66,48),
        new(5,"Голем",Root+"hero_golem.png",72,64,48)
    };
    private static readonly Texture2D[] Textures=new Texture2D[Entries.Length];
    public static int Count=>Entries.Length;
    public static CharacterDefinition Get(int id)=>Entries[id>=0 && id<Count ? id : 0];
    public static Texture2D Texture(int id)
    {
        id=Get(id).Id;
        if(Textures[id]!=null) return Textures[id];
        var source=GD.Load<Texture2D>(Get(id).TexturePath);
        using var image=source.GetImage();
        Rect2I used=image.GetUsedRect();
        return Textures[id]=new AtlasTexture { Atlas=source,Region=new Rect2(used.Position,used.Size) };
    }
}
