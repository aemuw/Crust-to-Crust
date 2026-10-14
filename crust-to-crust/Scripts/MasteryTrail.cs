using Godot;

public static class TrailStyle
{
    public const int Count = 7;
    public static Color Color(int id) => new(id switch { 1=>"ffb556",2=>"bda5ff",3=>"79e3d5",4=>"f3a5c8",5=>"65dfb2",6=>"ffb369",_=>"a6d4e8" });
    public static string Name(int id) => id switch { 1=>"Бурштин",2=>"Кварц",3=>"Іній",4=>"Райдуга",5=>"Північне сяйво",6=>"Іскри",_=>"Повітря" };
    public static string Requirement(int id) => id==1 ? "Пройди ядро" : id==2 ? "Пройди планету наскрізь" : id==3 ? "Погаси полум’я кріогазом 3 рази за забіг" : "Доступний від початку";
    public static void Segment(CanvasItem canvas, int id, Vector2 a, Vector2 b, float life, int index, float clock)
    {
        Color color = id==4 ? Godot.Color.FromHsv(Mathf.PosMod(index*0.028f-clock*0.07f,1),0.7f,1)
            : id==5 ? Color(5).Lerp(new Color("809cf7"),0.5f+0.5f*Mathf.Sin(index*0.22f+clock)) : Color(id);
        color.A=life*0.6f;
        if (id==6)
        {
            if(index%3==0) canvas.DrawLine(a,b,color,1.2f,true);
            if(index%4==0)
            {
                Vector2 p=b+new Vector2(Mathf.Sin(index*2.3f)*9,Mathf.Cos(index)*3);
                canvas.DrawLine(p-Vector2.Up*3*life,p+Vector2.Up*3*life,color,1,true);
                canvas.DrawLine(p-Vector2.Right*3*life,p+Vector2.Right*3*life,color,1,true);
            }
            return;
        }
        canvas.DrawLine(a,b,color,1+4*life,true);
        if(id==5)
        {
            Vector2 offset=new Vector2(Mathf.Sin(index*0.4f+clock*1.5f)*7,0);
            color.A*=0.45f; canvas.DrawLine(a+offset,b+offset,color,2+5*life,true);
        }
        else if(id>0 && id!=4 && index%5==0)
        {
            Vector2 p=b+new Vector2(Mathf.Sin(index*1.7f)*5,0);
            if(id==2) canvas.DrawColoredPolygon(new[] {p+Vector2.Up*4,p+Vector2.Right*2,p+Vector2.Down*4,p+Vector2.Left*2},color);
            else canvas.DrawCircle(p,id==3 ? 2.5f*life : 1.8f*life,color);
        }
    }
}

/// <summary>Actual runner trail, distinct from the ambient speed streaks.</summary>
public partial class MasteryTrail : Node2D
{
    private const int Count=36;
    private readonly Vector2[] _points=new Vector2[Count];
    private int _count;
    private float _speed,_sampleClock,_clock;
    private bool _active;
    private Vector2 _runner;
    public int StyleId=>SaveData.SelectedTrail;
    public int SampleCount=>_count;
    public override void _Ready()
    {
        TopLevel=true; Position=Vector2.Zero; ZIndex=9; Material=GameAssets.Unshaded;
    }
    public void SetMotion(Vector2 runner,float speed,bool active)
    { _runner=runner; _speed=Mathf.Max(0,speed); _active=active; Visible=active; if(!active) { _count=0; _sampleClock=0; } }
    public override void _Process(double delta)
    {
        if(!_active) return;
        float dt=(float)delta; _clock+=dt;
        for(int i=0;i<_count;i++) _points[i]-=new Vector2(0,_speed*dt);
        _sampleClock+=dt;
        if(_sampleClock>=1f/60)
        {
            _sampleClock=Mathf.PosMod(_sampleClock,1f/60);
            for(int i=Mathf.Min(_count,Count-1);i>0;i--) _points[i]=_points[i-1];
            _points[0]=_runner+new Vector2(0,-22); _count=Mathf.Min(_count+1,Count);
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        for(int i=1;i<_count;i++)
            TrailStyle.Segment(this,StyleId,_points[i-1],_points[i],1-(float)i/Count,i,_clock);
    }
}

public partial class TrailPreview : Control
{
    public int StyleId;
    private float _clock;
    public override void _Process(double delta) { if(IsVisibleInTree()) { _clock+=(float)delta; QueueRedraw(); } }
    public override void _Draw()
    {
        Vector2 previous=new Vector2(22,Size.Y/2);
        for(int i=1;i<=35;i++)
        {
            float t=(float)i/35;
            Vector2 p=new Vector2(22+t*(Size.X-62),Size.Y/2+Mathf.Sin(t*4-_clock*2)*10);
            TrailStyle.Segment(this,StyleId,previous,p,t,35-i,_clock);
            previous=p;
        }
        DrawRect(new Rect2(previous-new Vector2(11,11),new Vector2(22,22)),new Color("d4dcd4"));
    }
}
