using Godot;

/// <summary>A wall-rooted sentry with a locked aim, clear charge and one bounded volley.</summary>
public partial class WallShooter : Node2D
{
    public const float WarningSeconds=1f;
    private LevelGenerator _level;
    private Sprite2D _sprite;
    private int _variant,_side;
    private bool _fired;
    private Vector2 _target;
    public float ChargeRemaining { get; private set; }
    public override void _Ready()
    {
        _sprite=new Sprite2D { Material=GameAssets.Unshaded }; AddChild(_sprite);
        ZIndex=3; Material=GameAssets.Unshaded; Deactivate();
    }
    public void Activate(LevelGenerator level,int variant,int side,float y)
    {
        _level=level; _variant=variant; _side=side<0 ? -1 : 1;
        Position=new Vector2(0,y); AttachToWall();
        _sprite.Texture=GameAssets.ShooterTextures[variant];
        _sprite.Scale=new Vector2(-_side,1)*136f/_sprite.Texture.GetWidth();
        _target=level.Runner?.Position??new Vector2(0,-200);
        ChargeRemaining=WarningSeconds; _fired=false;
        Visible=true; ProcessMode=ProcessModeEnum.Inherit; QueueRedraw();
    }
    private void AttachToWall()
    {
        _level.GetRockBounds(Position.Y,out float left,out float right);
        Position=new Vector2((_side<0 ? left : right)-_side*36,Position.Y);
    }
    public void Deactivate() { Visible=false; ProcessMode=ProcessModeEnum.Disabled; }
    public override void _PhysicsProcess(double delta)
    {
        if(_level?.IsGameActive!=true) return;
        Position-=new Vector2(0,_level.FallSpeed*(float)delta); AttachToWall();
        if(!_fired)
        {
            ChargeRemaining=Mathf.Max(0,ChargeRemaining-(float)delta);
            _sprite.Modulate=Colors.White.Lerp(new Color(_variant==0 ? "a0ffff" : "ffb367"),1f-ChargeRemaining);
            if(ChargeRemaining<=0 && Position.Y<520)
            {
                _fired=true;
                // Never fire after being scrolled past the player or outside the visible cave.
                if(Position.Y>-80) _level.TryFireVolley(this,_variant,_target);
                _sprite.Modulate=new Color(0.65f,0.65f,0.65f);
            }
        }
        QueueRedraw();
        if(Position.Y < -650) Deactivate();
    }
    public Vector2 Muzzle=>GlobalPosition+new Vector2(-_side*54,0);
    public override void _Draw()
    {
        if(_fired || _level==null) return;
        Color color=_variant==0 ? new Color("8ce5e8") : new Color("ffb05b");
        color.A=0.22f;
        Vector2 origin=ToLocal(Muzzle), end=ToLocal(_target);
        DrawDashedLine(origin,end,color,2,14,true);
        color.A=0.7f;
        DrawArc(end,18,0,Mathf.Tau,24,color,2,true);
        // A charge pip stays visible even while the mount approaches from below the screen.
        Transform2D canvas=GetViewport().GetCanvasTransform();
        Vector2 screen=canvas*GlobalPosition, size=GetViewport().GetVisibleRect().Size;
        screen.X=Mathf.Clamp(screen.X,24,size.X-24); screen.Y=Mathf.Clamp(screen.Y,24,size.Y-40);
        Vector2 pip=ToLocal(canvas.AffineInverse()*screen);
        DrawCircle(pip,8+5*(1-ChargeRemaining),color);
        DrawArc(pip,19,-Mathf.Pi/2,-Mathf.Pi/2+Mathf.Tau*(1-ChargeRemaining),24,color,3,true);
    }
}
