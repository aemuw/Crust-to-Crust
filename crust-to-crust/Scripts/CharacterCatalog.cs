using Godot;

public enum BodyForm { Cube, Circle, Triangle }

public sealed record CharacterDefinition(int Id,string Name,string TexturePath,int Price,float DisplayHeight,float EnvelopeRadius,BodyForm Form=BodyForm.Cube)
{
    public Shape2D CreateBodyShape() => Form switch
    {
        BodyForm.Circle => new CircleShape2D { Radius=24 },
        BodyForm.Triangle => new ConvexPolygonShape2D { Points=new[] { new Vector2(0,-26),new Vector2(30,26),new Vector2(-30,26) } },
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
        new(3,"Мідний механізм",Root+"cube_copper.png",48,52,46),
        new(4,"Аметист",Root+"cube_amethyst.png",60,52,46),
        new(5,"Магмова кірка",Root+"cube_magma.png",72,52,46),
        new(6,"Обсидіанове коло",Root+"circle_obsidian.png",1500,52,46,BodyForm.Circle),
        new(7,"Мідне коло",Root+"circle_copper.png",400,52,46,BodyForm.Circle),
        new(8,"Місячний опал",Root+"circle_opal.png",700,52,46,BodyForm.Circle),
        new(9,"Базальтовий клин",Root+"triangle_basalt.png",2500,56,46,BodyForm.Triangle),
        new(10,"Бурштиновий клин",Root+"triangle_amber.png",400,56,46,BodyForm.Triangle),
        new(11,"Крижаний клин",Root+"triangle_ice.png",700,56,46,BodyForm.Triangle)
    };
    private static readonly Texture2D[] Textures=new Texture2D[Entries.Length];
    public static int Count=>Entries.Length;
    public static int BaseSkin(BodyForm form)=>form==BodyForm.Circle ? 6 : form==BodyForm.Triangle ? 9 : 0;
    public static string FormName(BodyForm form)=>form==BodyForm.Circle ? "КОЛО" : form==BodyForm.Triangle ? "ТРИКУТНИК" : "КУБ";
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
