using Godot;

/// <summary>A fixed, textured ridge with a wide exit and a narrower optional reward lane.</summary>
public partial class RouteFork : Area2D
{
    private LevelGenerator _level;
    private HazardWarning _warning;
    private readonly Coin[] _rewards=new Coin[3];
    private float _fade;
    private bool _retiring;
    public bool Active=>Visible;
    public bool Armed { get; private set; }
    public float Elapsed { get; private set; }
    public float SafeX { get; private set; }
    public float RiskX { get; private set; }
    public float SafeWidth { get; private set; }
    public float RiskWidth { get; private set; }
    public override void _Ready()
    {
        CollisionLayer=2;CollisionMask=0;Monitoring=false;ZIndex=2;Material=GameAssets.Unshaded;
        AddChild(new CollisionShape2D { Shape=new CapsuleShape2D { Radius=36,Height=220 } });
        _warning=new HazardWarning();AddChild(_warning);
        var scene=GD.Load<PackedScene>("res://Scenes/Obstacles/Coin.tscn");
        for(int i=0;i<3;i++){_rewards[i]=scene.Instantiate<Coin>();_rewards[i].Name="Reward"+i;AddChild(_rewards[i]);_rewards[i].SetProcess(false);}
        Deactivate();
    }
    public bool Activate(LevelGenerator level,int side,bool battery)
    {
        _level=level;float y=level.Runner.FixedY+level.FallSpeed*2.5f;
        float left=float.NegativeInfinity,right=float.PositiveInfinity;
        for(int i=0;i<=12;i++){level.GetRockBounds(y-160+i*320f/12,out float l,out float r);left=Mathf.Max(left,l);right=Mathf.Min(right,r);}
        float x=side*80;
        float leftWidth=x-36-left,rightWidth=right-x-36;
        float minimum=level.Runner.CollisionEnvelopeRadius*2+28;
        if(leftWidth<minimum || rightWidth<minimum)return false;
        RiskX=side<0 ? (left+x-36)/2 : (right+x+36)/2;
        SafeX=side<0 ? (right+x+36)/2 : (left+x-36)/2;
        RiskWidth=side<0 ? leftWidth : rightWidth;SafeWidth=side<0 ? rightWidth : leftWidth;
        Position=new Vector2(x,y);Elapsed=_fade=0;Armed=_retiring=false;Modulate=Colors.White;Show();ProcessMode=ProcessModeEnum.Inherit;SetDeferred("monitorable",false);
        for(int i=0;i<3;i++)
        {
            var reward=_rewards[i];reward.Hide();reward.Monitorable=false;
            if(battery && i>0)continue;
            reward.ProcessMode=ProcessModeEnum.Inherit;
            reward.Position=new Vector2(RiskX-x,(i-1)*60);reward.speed=0;reward.TriggerAppear(battery ? PickupKind.Battery : PickupKind.Coin);
            reward.Show();reward.SetDeferred("monitorable",true);
        }
        QueueRedraw();return true;
    }
    public void Deactivate(){Hide();Armed=false;SetDeferred("monitorable",false);_warning?.Hide();foreach(var reward in _rewards)reward?.Hide();ProcessMode=ProcessModeEnum.Disabled;}
    private void Retire(){_retiring=true;Armed=false;SetDeferred("monitorable",false);_warning.Hide();foreach(var reward in _rewards)reward.SetDeferred("monitorable",false);}
    public override void _Process(double delta)
    {
        float remaining=Mathf.Max(0,(float)delta);
        while(remaining>0 && Active)
        {
            float dt=Mathf.Min(remaining,Mathf.Min(.01f,12/Mathf.Max(1,_level.FallSpeed)));remaining-=dt;Elapsed+=dt;
            float previousY=GlobalPosition.Y;Position-=new Vector2(0,_level.FallSpeed*dt);
            if(!_level.IsGameActive){Deactivate();return;}
            if(!_retiring && !_level.CanContinueCollapseEvent(Position.Y))Retire();
            if(_retiring){_fade+=dt;Modulate=new Color(1,1,1,Mathf.Max(0,1-_fade/.6f));if(_fade>=.6f){Deactivate();return;}}
            else
            {
                if(!Armed && Elapsed>=1f)
                {
                    if(Position.Y-_level.Runner.FixedY<_level.FallSpeed*.6f){Retire();continue;}
                    Armed=true;SetDeferred("monitorable",true);
                }
                if(Armed && _level.Runner.ProjectileSweepHits(new Vector2(Position.X,previousY+74),new Vector2(Position.X,GlobalPosition.Y-74),36))
                {_level.Runner.ReceiveProjectileHit();if(!_level.IsGameActive)return;}
            }
            foreach(var reward in _rewards)if(reward.Visible)reward._Process(dt);
            if(CaveViewport.PassedTop(this,130)){Deactivate();return;}
        }
        Vector2 source=ToGlobal(new Vector2(0,-110));
        _warning.SetCue(!_retiring && Position.Y>_level.Runner.FixedY,source,source,Elapsed/1f,new Color("cbb18a"),collapse:true);
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(_level==null)return;
        foreach(float y in new[]{-74f,-25f,25f,74f})
            DrawTextureRect(GameAssets.HazardTextures[0],new Rect2(-36,y-36,72,72),false,new Color(.45f,.45f,.45f));
        // Preview the actual reward at the viewport edge before the ridge enters the view.
        if(_retiring)return;
        Vector2 world=ToGlobal(new Vector2(RiskX-Position.X,-60));var canvas=GetViewport().GetCanvasTransform();var view=GetViewport().GetVisibleRect().Grow(-60);
        Vector2 screen=canvas*world;if(view.HasPoint(screen))return;
        screen=new Vector2(Mathf.Clamp(screen.X,view.Position.X,view.End.X),Mathf.Clamp(screen.Y,view.Position.Y,view.End.Y));
        Vector2 local=ToLocal(canvas.AffineInverse()*screen);
        Texture2D icon=_rewards[0].Kind==PickupKind.Battery ? GameAssets.Battery : GameAssets.CoinFrames.GetFrameTexture("Spin",0);
        DrawTextureRect(icon,new Rect2(local-new Vector2(12,12),new Vector2(24,24)),false,new Color(1,1,1,.85f));
    }
}
