using Godot;

/// <summary>Bounded pooled shots. World velocity and scrolling velocity are kept separate.</summary>
public partial class HazardProjectile : Area2D
{
    private LevelGenerator _level;
    private Vector2 _velocity;
    private Sprite2D _sprite;
    private float _life;
    public ProjectilePath Path=>new(GlobalPosition,_velocity);
    public const float Radius=8f;
    public override void _Ready()
    {
        CollisionLayer=2; CollisionMask=0; Monitoring=false; Monitorable=false; AddToGroup("Obstacle");
        AddChild(new CollisionShape2D { Shape=new CircleShape2D { Radius=Radius } });
        _sprite=new Sprite2D { Material=GameAssets.Unshaded }; AddChild(_sprite);
        Deactivate();
    }
    public void Activate(Vector2 position,Vector2 velocity,int variant,LevelGenerator level)
    {
        _level=level; Position=position; _velocity=velocity; _life=0;
        _sprite.Texture=GameAssets.HazardTextures[variant==0 ? 1 : 5];
        float size=variant==0 ? 24f : 23f;
        _sprite.Scale=Vector2.One*size/_sprite.Texture.GetWidth();
        _sprite.Modulate=variant==0 ? new Color("b5faff") : new Color("ffb271");
        Visible=true; ProcessMode=ProcessModeEnum.Inherit; SetDeferred("monitorable",true);
    }
    public void Deactivate()
    { Visible=false; SetDeferred("monitorable",false); ProcessMode=ProcessModeEnum.Disabled; }
    public static bool SweepHits(Vector2 from,Vector2 to,Vector2 point,float radius)
        => Geometry2D.GetClosestPointToSegment(point,from,to).DistanceSquaredTo(point)<=radius*radius;
    public override void _PhysicsProcess(double delta)
    {
        if(_level?.IsGameActive!=true) return;
        float dt=(float)delta; _life+=dt;
        Vector2 before=GlobalPosition;
        GlobalPosition+=(_velocity-new Vector2(0,_level.FallSpeed))*dt;
        Rotation=(_velocity-new Vector2(0,_level.FallSpeed)).Angle()+Mathf.Pi/2;
        var player=_level.Runner;
        if(player!=null && player.GetNode<Area2D>("Hitbox").Monitoring &&
            player.ProjectileSweepHits(before,GlobalPosition,Radius) && player.ReceiveProjectileHit())
        { Deactivate(); return; }
        if(_life>2.1f || Position.Y < -650 || Mathf.Abs(Position.X)>370) Deactivate();
    }
}
