using Godot;

/// <summary>Wall-mounted telescopic drill arms. Visible geometry and cached physics shapes agree.</summary>
public partial class DrillGate : Area2D
{
    private LevelGenerator _level;
    private readonly CollisionShape2D[] _beams=new CollisionShape2D[2], _heads=new CollisionShape2D[2];
    private readonly CapsuleShape2D[] _capsules=new CapsuleShape2D[2];
    private readonly float[] _wall=new float[2], _end=new float[2];
    private readonly float[] _extension={.18f,.18f};
    private readonly bool[] _fits=new bool[2];
    private float _clock, _offset;
    public bool CollisionActive { get; private set; }
    public Vector2 LeftHeadPosition=>ToGlobal(new Vector2(_end[0],0));
    public override void _Ready()
    {
        CollisionLayer=2; CollisionMask=0; Monitoring=false; Monitorable=false;
        Material=GameAssets.Unshaded;
        for(int i=0;i<2;i++)
        {
            _capsules[i]=new CapsuleShape2D { Radius=8,Height=16 };
            _beams[i]=new CollisionShape2D { Shape=_capsules[i],Rotation=Mathf.Pi/2 }; AddChild(_beams[i]);
            _heads[i]=new CollisionShape2D { Shape=new CircleShape2D { Radius=22 } }; AddChild(_heads[i]);
        }
        Deactivate();
    }
    public void Activate(LevelGenerator level,int index,float y)
    {
        _level=level; Position=new Vector2(0,y); _clock=0; _offset=index*1.3f; _extension[0]=_extension[1]=.18f;
        CollisionActive=false; SetDeferred("monitorable",false); Modulate=Colors.White; Show(); Align();
    }
    public void Arm() { CollisionActive=true; SetDeferred("monitorable",true); }
    public void Disarm() { CollisionActive=false; SetDeferred("monitorable",false); }
    public void Deactivate() { Disarm(); Hide(); }
    private void Align(float dt=0)
    {
        _level.GetRockBounds(Position.Y,out _wall[0],out _wall[1]);
        float lane=_level.SafeLaneAt(Position.Y);
        float margin=(_level.Runner?.CollisionEnvelopeRadius??SafePassage.PlayerRadius)+SafePassage.HalfWidth+SafePassage.Clearance+22+8;
        for(int i=0;i<2;i++)
        {
            float direction=i==0 ? 1 : -1;
            float limit=lane-direction*margin;
            float available=direction*(limit-_wall[i])-36;
            _fits[i]=available>=8;
            float target=CollisionActive ? .6f+.4f*Mathf.Sin(_clock*2.6f+_offset+i*.8f) : .18f;
            _extension[i]=Mathf.MoveToward(_extension[i],target,dt*.8f);
            _end[i]=_wall[i]+direction*(36+Mathf.Max(0,available)*_extension[i]);
            float length=Mathf.Abs(_end[i]-_wall[i]);
            _capsules[i].Height=length+16;
            _beams[i].Position=new Vector2((_end[i]+_wall[i])*.5f,0);
            _heads[i].Position=new Vector2(_end[i],0);
            _beams[i].SetDeferred("disabled",!_fits[i]); _heads[i].SetDeferred("disabled",!_fits[i]);
        }
    }
    public void Step(float dt)
    {
        if(!Visible || _level==null) return;
        float previousY=GlobalPosition.Y;
        float oldLeft=_wall[0], oldRight=_wall[1], oldEndLeft=_end[0], oldEndRight=_end[1];
        Position-=new Vector2(0,_level.FallSpeed*dt); _clock+=dt; Align(dt); QueueRedraw();
        if(!CollisionActive || !_level.IsGameActive) return;
        var player=_level.Runner;
        if(Mathf.Max(previousY,GlobalPosition.Y)+70<player.GlobalPosition.Y || Mathf.Min(previousY,GlobalPosition.Y)-70>player.GlobalPosition.Y) return;
        int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(GlobalPosition.Y-previousY)/12));
        for(int i=0;i<2;i++)
        {
            if(!_fits[i]) continue;
            float oldWall=i==0 ? oldLeft : oldRight,oldEnd=i==0 ? oldEndLeft : oldEndRight;
            if(player.ProjectileSweepHits(new Vector2(oldEnd,previousY),ToGlobal(new Vector2(_end[i],0)),22)) { player.ReceiveProjectileHit(); return; }
            for(int k=0;k<=steps;k++)
            {
                float t=(float)k/steps, y=Mathf.Lerp(previousY,GlobalPosition.Y,t);
                if(Mathf.Abs(y-player.GlobalPosition.Y)>player.CollisionEnvelopeRadius+10) continue;
                if(player.ProjectileSweepHits(new Vector2(Mathf.Lerp(oldWall,_wall[i],t),y),new Vector2(Mathf.Lerp(oldEnd,_end[i],t),y),8))
                { player.ReceiveProjectileHit(); return; }
            }
        }
    }
    public override void _Draw()
    {
        if(_level==null) return;
        for(int i=0;i<2;i++)
        {
            float direction=i==0 ? 1 : -1,wall=_wall[i];
            CaveArt.Wall(this,CaveArt.Mount,new Vector2(wall,0),i==0 ? -1 : 1,new Rect2(-36,-82,60,164),new Color(.6f,.6f,.6f));
            if(!_fits[i]) continue;
            float length=Mathf.Abs(_end[i]-wall);
            DrawSetTransform(new Vector2(wall,0),0,new Vector2(direction,1));
            DrawTextureRect(CaveArt.Rod,new Rect2(0,-8,length,16),false,new Color(.7f,.7f,.7f));
            DrawSetTransform(new Vector2(_end[i],0),_clock*(CollisionActive ? 3 : .3f)*direction);
            DrawTextureRect(CaveArt.Cutter,new Rect2(-22,-22,44,44),false,new Color(.7f,.7f,.7f));
            DrawSetTransform(Vector2.Zero);
        }
    }
}
