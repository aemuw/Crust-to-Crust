using Godot;

// Pixel-aligned instrument casing; all lettering remains live UI text.
public partial class TelemetryPanel : Control
{
    public override void _Ready() { MouseFilter=MouseFilterEnum.Ignore; Resized+=QueueRedraw; }
    public override void _Draw()
    {
        var copper=new Color("a27e52");
        DrawRect(new Rect2(3,3,Size.X,Size.Y),new Color(0,0,0,0.45f));
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("101d22f5"));
        DrawRect(new Rect2(0,0,Size.X,Size.Y),copper,false,1);
        DrawRect(new Rect2(4,4,Size.X-8,Size.Y-8),new Color("2a3b3e"),false,1);
        for(int x=12;x<Size.X-10;x+=12) for(int y=40;y<Size.Y-10;y+=12)
            if((x/12+y/12)%3==0) DrawRect(new Rect2(x,y,1,1),new Color("19292d"));
        DrawRect(new Rect2(0,42,3,66),new Color("71d8cd"));
        foreach(float y in new[] {36f,174f})
            DrawRect(new Rect2(16,y,Size.X-32,1),new Color("35474a"));
        foreach(float x in new[] {7f,Size.X-10}) foreach(float y in new[] {7f,Size.Y-10})
        {
            DrawRect(new Rect2(x,y,3,3),copper);
            DrawRect(new Rect2(x+1,y+1,1,1),new Color("192329"));
        }
        for(int x=16;x<=Size.X-16;x+=24) DrawRect(new Rect2(x,197,1,3),new Color("49615e"));
    }
}
