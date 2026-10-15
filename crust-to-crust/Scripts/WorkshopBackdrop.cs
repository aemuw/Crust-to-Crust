using Godot;

// Code-native workshop decoration stays sharp at any viewport size.
public partial class WorkshopBackdrop : Control
{
    public bool DisplayStand;
    public override void _Ready() { MouseFilter=MouseFilterEnum.Ignore; Resized+=QueueRedraw; }
    public override void _Draw()
    {
        if(DisplayStand)
        {
            DrawRect(new Rect2(Vector2.Zero,Size),new Color("0c191d"));
            for(int x=12;x<Size.X;x+=16) for(int y=12;y<Size.Y;y+=16)
                DrawRect(new Rect2(x,y,1,1),new Color("28403f"));
            float cx=Mathf.Floor(Size.X/2),cy=Mathf.Floor(Size.Y/2);
            DrawRect(new Rect2(cx-62,cy-62,124,124),new Color("1b3738"),false,1);
            DrawRect(new Rect2(cx-70,cy-70,14,3),new Color("63c9bf"));
            DrawRect(new Rect2(cx-70,cy-70,3,14),new Color("63c9bf"));
            DrawRect(new Rect2(cx+56,cy+67,14,3),new Color("b68754"));
            DrawRect(new Rect2(cx+67,cy+56,3,14),new Color("b68754"));
            DrawRect(new Rect2(cx-45,Size.Y-18,90,3),new Color("345853"));
            return;
        }
        DrawRect(new Rect2(Vector2.Zero,Size),new Color("080f13"));
        const int tile=80;
        for(int y=0;y<Size.Y;y+=tile)
            for(int x=-tile;x<Size.X;x+=tile)
            {
                int shift=(y/tile%2)*40,variant=((x+tile)/tile+y/tile*3)%4;
                var rect=new Rect2(x+shift+2,y+2,tile-4,tile-4);
                DrawRect(rect,new Color(variant==0 ? "121e22" : variant==1 ? "101b20" : "0e181d"));
                DrawLine(rect.Position,rect.Position+new Vector2(rect.Size.X,0),new Color("1b2c30"),2);
                if(variant==0)
                {
                    Vector2 a=rect.Position+new Vector2(12,0);
                    DrawLine(a,a+new Vector2(0,20),new Color("20413f"),2);
                    DrawLine(a+new Vector2(0,20),a+new Vector2(16,20),new Color("20413f"),2);
                    DrawLine(a+new Vector2(16,20),a+new Vector2(16,40),new Color("20413f"),2);
                }
            }
        foreach(float x in new[] {24f,Size.X-28})
        {
            DrawRect(new Rect2(x,0,4,Size.Y),new Color("493d31"));
            for(int y=24;y<Size.Y;y+=80) DrawRect(new Rect2(x-2,y,8,8),new Color("a57d50"));
        }
        DrawRect(new Rect2(0,0,Size.X,5),new Color("997348"));
        DrawRect(new Rect2(0,Size.Y-5,Size.X,5),new Color("493d31"));
    }
}
