using Godot;

public enum BodyForm { Cube, Circle, Triangle, Rectangle, Diamond, Hexagon, Pentagon, Octagon, Trapezoid }

public sealed record CharacterDefinition(int Id,string Name,string TexturePath,int Price,float DisplayHeight,float EnvelopeRadius,BodyForm Form=BodyForm.Cube)
{
    public Vector2 RenderSize
    {
        get
        {
            Vector2 size=CharacterCatalog.Texture(Id).GetSize();
            return size*Mathf.Min(DisplayHeight/size.Y,60f/size.X);
        }
    }
    public Shape2D CreateBodyShape()
    {
        if(Form==BodyForm.Cube) return new RectangleShape2D { Size=new Vector2(48,48) };
        Vector2 h=RenderSize/2-Vector2.One*2;
        return Form switch
        {
            BodyForm.Circle => new CircleShape2D { Radius=Mathf.Min(h.X,h.Y) },
            BodyForm.Triangle => new ConvexPolygonShape2D { Points=new[] { new Vector2(0,-h.Y),h,new Vector2(-h.X,h.Y) } },
            BodyForm.Rectangle => new RectangleShape2D { Size=h*2 },
            BodyForm.Diamond => new ConvexPolygonShape2D { Points=new[] { new Vector2(0,-h.Y),new Vector2(h.X,0),new Vector2(0,h.Y),new Vector2(-h.X,0) } },
            BodyForm.Hexagon => new ConvexPolygonShape2D { Points=new[] { new Vector2(-h.X,0),new Vector2(-h.X/2,-h.Y),new Vector2(h.X/2,-h.Y),new Vector2(h.X,0),new Vector2(h.X/2,h.Y),new Vector2(-h.X/2,h.Y) } },
            BodyForm.Pentagon => new ConvexPolygonShape2D { Points=new[] { new Vector2(0,-h.Y),new Vector2(h.X,-h.Y*0.12f),new Vector2(h.X*0.62f,h.Y),new Vector2(-h.X*0.62f,h.Y),new Vector2(-h.X,-h.Y*0.12f) } },
            BodyForm.Octagon => new ConvexPolygonShape2D { Points=new[] { new Vector2(-h.X*0.42f,-h.Y),new Vector2(h.X*0.42f,-h.Y),new Vector2(h.X,-h.Y*0.42f),new Vector2(h.X,h.Y*0.42f),new Vector2(h.X*0.42f,h.Y),new Vector2(-h.X*0.42f,h.Y),new Vector2(-h.X,h.Y*0.42f),new Vector2(-h.X,-h.Y*0.42f) } },
            BodyForm.Trapezoid => new ConvexPolygonShape2D { Points=new[] { new Vector2(-h.X*0.58f,-h.Y),new Vector2(h.X*0.58f,-h.Y),new Vector2(h.X,h.Y),new Vector2(-h.X,h.Y) } },
            _=>new RectangleShape2D { Size=new Vector2(48,48) }
        };
    }
}

/// <summary>Stable save IDs; art, collision and shop all consume the same definition.</summary>
public static class CharacterCatalog
{
    private const string Root="res://Textures/PixelSkins/";
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
        new(11,"Крижаний клин",Root+"triangle_ice.png",700,56,46,BodyForm.Triangle),
        new(12,"Мідний брусок",Root+"bar_copper.png",900,36,46,BodyForm.Rectangle),
        new(13,"Нефритовий брусок",Root+"bar_jade.png",500,36,46,BodyForm.Rectangle),
        new(14,"Кобальтовий ромб",Root+"diamond_cobalt.png",1100,56,46,BodyForm.Diamond),
        new(15,"Гранатовий ромб",Root+"diamond_garnet.png",500,56,46,BodyForm.Diamond),
        new(16,"Бронзовий шестикутник",Root+"hex_bronze.png",1800,52,46,BodyForm.Hexagon),
        new(17,"Малахіт",Root+"hex_malachite.png",500,52,46,BodyForm.Hexagon),
        new(18,"Кварцовий бастіон",Root+"pentagon_basalt.png",1300,52,46,BodyForm.Pentagon),
        new(19,"Аметистовий бастіон",Root+"pentagon_amethyst.png",450,52,46,BodyForm.Pentagon),
        new(20,"Мідний бастіон",Root+"pentagon_copper.png",650,52,46,BodyForm.Pentagon),
        new(21,"Магмовий октагон",Root+"octagon_magma.png",1600,52,46,BodyForm.Octagon),
        new(22,"Крижаний октагон",Root+"octagon_ice.png",450,52,46,BodyForm.Octagon),
        new(23,"Опаловий октагон",Root+"octagon_opal.png",650,52,46,BodyForm.Octagon),
        new(24,"Обсидіанова платформа",Root+"trapezoid_basalt.png",1200,38,46,BodyForm.Trapezoid),
        new(25,"Нефритова платформа",Root+"trapezoid_jade.png",450,38,46,BodyForm.Trapezoid),
        new(26,"Мідна платформа",Root+"trapezoid_copper.png",650,38,46,BodyForm.Trapezoid)
    };
    private static readonly Texture2D[] Textures=new Texture2D[Entries.Length];
    public static int Count=>Entries.Length;
    public static int BaseSkin(BodyForm form)=>form switch { BodyForm.Circle=>6,BodyForm.Triangle=>9,BodyForm.Rectangle=>12,BodyForm.Diamond=>14,BodyForm.Hexagon=>16,BodyForm.Pentagon=>18,BodyForm.Octagon=>21,BodyForm.Trapezoid=>24,_=>0 };
    public static string FormName(BodyForm form)=>form switch { BodyForm.Circle=>"КОЛО",BodyForm.Triangle=>"ТРИКУТНИК",BodyForm.Rectangle=>"БРУСОК",BodyForm.Diamond=>"РОМБ",BodyForm.Hexagon=>"ШЕСТИКУТНИК",BodyForm.Pentagon=>"П’ЯТИКУТНИК",BodyForm.Octagon=>"ВОСЬМИКУТНИК",BodyForm.Trapezoid=>"ТРАПЕЦІЯ",_=>"КУБ" };
    public static CharacterDefinition Get(int id)=>Entries[id>=0 && id<Count ? id : 0];
    public static Texture2D Texture(int id)
    {
        id=Get(id).Id;
        if(Textures[id]!=null) return Textures[id];
        var source=GD.Load<Texture2D>(Get(id).TexturePath);
        using var image=source.GetImage();
        // Ignore almost transparent export noise; it must not enlarge the physical silhouette.
        image.Convert(Image.Format.Rgba8); byte[] pixels=image.GetData();
        int width=image.GetWidth(),height=image.GetHeight(),left=width,top=height,right=-1,bottom=-1;
        for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            if(pixels[(y*width+x)*4+3]>=24) { left=Mathf.Min(left,x); right=Mathf.Max(right,x); top=Mathf.Min(top,y); bottom=Mathf.Max(bottom,y); }
        Rect2I used=right>=left ? new Rect2I(left,top,right-left+1,bottom-top+1) : image.GetUsedRect();
        return Textures[id]=new AtlasTexture { Atlas=source,Region=new Rect2(used.Position,used.Size) };
    }
}
