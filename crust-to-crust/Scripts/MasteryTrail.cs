using Godot;

public static class TrailStyle
{
    public static Color Color(int id)=>new(id==1 ? "ffb556" : id==2 ? "bda5ff" : id==3 ? "79e3d5" : "a6d4e8");
    public static string Name(int id)=>id==1 ? "Бурштин" : id==2 ? "Кварц" : id==3 ? "Вода" : "Повітря";
    public static string Requirement(int id)=>id==1 ? "Пройди ядро" : id==2 ? "Пройди планету наскрізь" : id==3 ? "Погаси полум’я водою 3 рази за забіг" : "Доступний від початку";
}

/// <summary>Actual runner trail, distinct from the ambient speed streaks.</summary>
public partial class MasteryTrail : Node2D
{
    private const int Count=36;
    private readonly Vector2[] _points=new Vector2[Count];
    private int _count;
    private float _speed,_sampleClock;
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
        float dt=(float)delta;
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
        Color color=TrailStyle.Color(StyleId);
        for(int i=1;i<_count;i++)
        {
            float t=1-(float)i/Count; color.A=t*0.6f;
            DrawLine(_points[i-1],_points[i],color,1+4*t,true);
            if(StyleId>0 && i%5==0)
            {
                Vector2 p=_points[i]+new Vector2(Mathf.Sin(i*1.7f)*5,0);
                if(StyleId==2) DrawColoredPolygon(new[] {p+Vector2.Up*4,p+Vector2.Right*2,p+Vector2.Down*4,p+Vector2.Left*2},color);
                else DrawCircle(p,StyleId==3 ? 2.5f*t : 1.8f*t,color);
            }
        }
    }
}

public partial class TrailPreview : Control
{
    public int StyleId;
    private float _clock;
    public override void _Process(double delta) { if(IsVisibleInTree()) { _clock+=(float)delta; QueueRedraw(); } }
    public override void _Draw()
    {
        Color color=TrailStyle.Color(StyleId);
        Vector2 previous=new Vector2(22,Size.Y/2);
        for(int i=1;i<=35;i++)
        {
            float t=(float)i/35;
            Vector2 p=new Vector2(22+t*(Size.X-62),Size.Y/2+Mathf.Sin(t*4-_clock*2)*10);
            color.A=0.1f+0.75f*t; DrawLine(previous,p,color,1+5*t,true);
            if(StyleId>0 && i%5==0) DrawCircle(p+new Vector2(0,Mathf.Sin(_clock*3+i)*7),2*t,color);
            previous=p;
        }
        DrawRect(new Rect2(previous-new Vector2(11,11),new Vector2(22,22)),new Color("d4dcd4"));
    }
}
