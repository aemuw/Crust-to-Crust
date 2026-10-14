using Godot;

/// <summary>A wall-rooted sentry with a locked aim, clear charge and one bounded volley.</summary>
public partial class WallShooter : Node2D
{
    public const float WarningSeconds=1f;
    public const float MinimumFireGap=250f, MaximumFireGap=580f, PreferredFireGap=420f;
    private LevelGenerator _level;
    private Sprite2D _sprite;
    private Node2D _barrel;
    private Sprite2D _flash;
    private float _recoil,_flashTime;
    private int _variant,_side;
    private bool _fired;
    private Vector2 _target;
    private ShooterAudioCue _audio;
    public float ChargeRemaining { get; private set; }
    public override void _Ready()
    {
        _sprite=new Sprite2D { Material=GameAssets.Unshaded }; AddChild(_sprite);
        _barrel=new Node2D { Name="Barrel" }; AddChild(_barrel);
        _barrel.AddChild(new Polygon2D { Polygon=new[] { new Vector2(0,-7),new Vector2(48,-7),new Vector2(54,-4),new Vector2(54,4),new Vector2(0,7) },Color=new Color("58696b"),Material=GameAssets.Unshaded });
        _barrel.AddChild(new Polygon2D { Polygon=new[] {new Vector2(5,-6),new Vector2(49,-6),new Vector2(49,-3),new Vector2(5,-3)},Color=new Color("aebabc"),Material=GameAssets.Unshaded });
        _flash=new Sprite2D { Texture=GameAssets.Halo,Scale=Vector2.One*70/GameAssets.Halo.GetWidth(),Visible=false,ZIndex=2,Material=GameAssets.Unshaded }; AddChild(_flash);
        _audio=new ShooterAudioCue { Name="ShooterAudio" }; AddChild(_audio);
        ZIndex=3; Material=GameAssets.Unshaded; Deactivate();
    }
    public void Activate(LevelGenerator level,int variant,int side,float y)
    {
        _level=level; _variant=variant; _side=side<0 ? -1 : 1;
        Position=new Vector2(0,y); AttachToWall();
        _sprite.Texture=GameAssets.ShooterTextures[variant];
        _sprite.Scale=new Vector2(-_side,1)*136f/_sprite.Texture.GetWidth();
        _target=level.Runner?.Position??new Vector2(0,-200);
        _barrel.Visible=variant!=2; _barrel.Position=new Vector2(-_side*22,-10); _barrel.Rotation=AimVelocity(_target).Angle();
        _recoil=0; _flashTime=0; _flash.Hide();
        ChargeRemaining=WarningSeconds; _fired=false;
        _audio.Reset(variant);
        Visible=true; ProcessMode=ProcessModeEnum.Inherit; QueueRedraw();
    }
    private void AttachToWall()
    {
        _level.GetRockBounds(Position.Y,out float left,out float right);
        Position=new Vector2((_side<0 ? left : right)-_side*(_variant==2 ? 10 : 36),Position.Y);
    }
    public void Deactivate() { _audio?.Stop(); Visible=false; ProcessMode=ProcessModeEnum.Disabled; }
    public override void _PhysicsProcess(double delta)
    {
        if(_level?.IsGameActive!=true) { _audio?.Stop(); return; }
        Position-=new Vector2(0,_level.FallSpeed*(float)delta); AttachToWall();
        float dt=(float)delta;
        _recoil=Mathf.MoveToward(_recoil,0,dt*50);
        if(!_fired) _barrel.Rotation=Mathf.LerpAngle(_barrel.Rotation,AimVelocity(_target).Angle(),1-Mathf.Exp(-dt*8));
        _barrel.Position=new Vector2(-_side*22,-10)-Vector2.Right.Rotated(_barrel.Rotation)*_recoil;
        _flashTime=Mathf.Max(0,_flashTime-dt); _flash.Visible=_flashTime>0;
        if(_flash.Visible) { _flash.GlobalPosition=Muzzle; _flash.Modulate=new Color(_variant==1 ? "ffb367" : "a0ffff") { A=_flashTime/0.16f*0.8f }; }
        if(!_fired)
        {
            ChargeRemaining=Mathf.Max(0,ChargeRemaining-(float)delta);
            _sprite.Modulate=Colors.White.Lerp(new Color(_variant==1 ? "ffb367" : "a0ffff"),1f-ChargeRemaining);
            float gap=Position.Y-(_level.Runner?.Position.Y??-200);
            // Start where the moving source is audible, with lead time before the
            // existing volley. This does not change charging or firing decisions.
            if(gap>MinimumFireGap && gap<=MaximumFireGap+_level.FallSpeed*0.6f)
                _audio.Warn(Muzzle);
            if(ChargeRemaining<=0 && gap<=MaximumFireGap)
            {
                _fired=true;
                // Never fire after being scrolled past the player or outside the visible cave.
                if(gap>=MinimumFireGap && _level.TryFireVolley(this,_variant,_target))
                { _recoil=9; _flashTime=0.16f; _audio.Fire(Muzzle); }
                _sprite.Modulate=new Color(0.65f,0.65f,0.65f);
            }
        }
        QueueRedraw();
        if(CaveViewport.PassedTop(this,100)) Deactivate();
    }
    public Vector2 Muzzle=>_variant==2 ? GlobalPosition+new Vector2(-_side*54,0) : _barrel.ToGlobal(new Vector2(54,0));
    public Vector2 AimVelocity(Vector2 target)
    {
        Vector2 delta=target-Muzzle;
        float angle=Mathf.Clamp(Mathf.Atan2(Mathf.Max(0,-delta.Y),Mathf.Abs(delta.X)),Mathf.DegToRad(60),Mathf.DegToRad(70));
        return new Vector2(-_side*Mathf.Cos(angle),-Mathf.Sin(angle))*520;
    }
    public override void _Draw()
    {
        if(_fired || _level==null) return;
        Color color=_variant==1 ? new Color("ffb05b") : new Color("8ce5e8");
        color.A=0.22f;
        Vector2 velocity=AimVelocity(_target)-new Vector2(0,_level.FallSpeed);
        float impact=Mathf.Max(0,(_target.Y-Muzzle.Y)/Mathf.Min(-1,velocity.Y));
        Vector2 origin=ToLocal(Muzzle), end=ToLocal(Muzzle+velocity*impact);
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
