using Godot;
using System;
using System.Reflection;
public partial class RouteForkSmoke : Node
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);GD.Print("PASS: "+message);}
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            SaveData.Load();SaveData.TutorialComplete=true;level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>();AddChild(level);level.SetRunSeed(17);level.SetProcess(false);level.SetPhysicsProcess(false);
            var player=level.Runner;player.SetProcess(false);player.SetPhysicsProcess(false);player.GetNode<Area2D>("Hitbox").Monitoring=false;
            var start=typeof(LevelGenerator).GetMethod("TryStartRouteFork");Check(start!=null,"fork has a coordinated entry point");
            var fork=level.GetNode<Node2D>("RouteFork");fork.SetProcess(false);var process=fork.GetType().GetMethod("_Process");object P(string n)=>fork.GetType().GetProperty(n).GetValue(fork);
            void Reset(float speed=600)
            {
                level.GetNode<Camera2D>("Camera2D").Position=Vector2.Zero;level.GetNode<Camera2D>("Camera2D").ForceUpdateScroll();
                level.StopFalling();level.IsGameActive=true;level.CurrentDepth=1000;level.FallSpeed=speed;Set(level,"_coreRespite",0f);Set(level,"waterTimer",0f);Set(level,"nextWaterInterval",1000f);
                Set(player,"_temperature",0f);Set(player,"_hasPreviousContact",false);var state=typeof(Player).GetField("_state",Hidden);state.SetValue(player,Enum.Parse(state.FieldType,"Playing"));
                player.Position=new Vector2(0,player.FixedY);
            }
            bool Begin()=>(bool)start.Invoke(level,null);
            void Tick(float t){while(t>0){float dt=Mathf.Min(.01f,t);t-=dt;process.Invoke(fork,new object[]{(double)dt});}}
            Reset();Check(Begin(),"cold clear tunnel admits a route choice");
            Check((float)P("SafeWidth")>(float)P("RiskWidth") && (float)P("RiskWidth")>=player.CollisionEnvelopeRadius*2+28,"reward branch is tighter while both routes fit the full envelope");
            Check(!level.ProjectilesAllowed && !level.TryStartCollapseEvent(),"fork excludes conflicting encounters");
            Tick(.9f);Check(!(bool)P("Armed"),"approach warning precedes collision activation");Tick(.2f);Check((bool)P("Armed"),"ridge arms after its warning");
            var contact=(CollisionShape2D)typeof(Player).GetField("_contactShape",Hidden).GetValue(player);var original=contact.Shape;
            float x=fork.Position.X;
            foreach(int id in new[]{0,6,9,12,14,16,18,21,24})
            {
                contact.Shape=CharacterCatalog.Get(id).CreateBodyShape();
                foreach(string lane in new[]{"SafeX","RiskX"}){player.Position=new Vector2((float)P(lane),player.FixedY);Set(player,"_hasPreviousContact",false);Check(!player.ProjectileSweepHits(new Vector2(x,-400),new Vector2(x,0),36),lane+" fits "+CharacterCatalog.Get(id).Form);}
            }
            contact.Shape=original;
            var reward=fork.GetNode<Coin>("Reward0");Check(reward.Visible,"reward is visible on the tight branch");
            int wallet=SaveData.Wallet;typeof(Player).GetMethod("OnAreaEntered",Hidden).Invoke(player,new object[]{reward});
            Check(SaveData.Wallet> wallet,"fork reward uses real coin collection");int after=SaveData.Wallet;typeof(Player).GetMethod("OnAreaEntered",Hidden).Invoke(player,new object[]{reward});Check(SaveData.Wallet==after,"a collected fork reward cannot pay twice");await ToSignal(GetTree().CreateTimer(.03),SceneTreeTimer.SignalName.Timeout);
            Tick(4);Check(!fork.Visible,"passed ridge retires after leaving the actual view");
            Reset();Check(Begin(),"repeat fixture starts");Check(reward.ProcessMode!=ProcessModeEnum.Disabled,"reused rewards rejoin physics after collection");Tick(1.2f);Set(player,"_temperature",.6f);Tick(.1f);Check(!(bool)P("Armed"),"heat immediately disarms the ridge");Tick(.8f);Check(!fork.Visible,"cancelled ridge fades away");
            Reset();Check(Begin(),"pause fixture starts");fork.SetProcess(true);GetTree().Paused=true;float elapsed=(float)P("Elapsed");await ToSignal(GetTree().CreateTimer(.1),SceneTreeTimer.SignalName.Timeout);Check((float)P("Elapsed")==elapsed,"pause freezes the fork");GetTree().Paused=false;fork.SetProcess(false);
            level.StopFalling();Check(!fork.Visible,"stop clears the fork and its owned rewards");
            Reset(1200);Check(Begin(),"high-speed approach retains advance warning");Reset();Set(player,"_temperature",.8f);Check(!Begin(),"hot player is not sent into a fork");
            Reset();Set(level,"_forkTimer",100f);Set(level,"_collapseEventTimer",11f);Set(level,"_collapseEventDelay",12f);
            typeof(LevelGenerator).GetMethod("SpawnTimers",Hidden).Invoke(level,new object[]{.01f});Check(!fork.Visible,"an upcoming major event keeps scheduling priority over a fork");
            level.StopFalling();level.SetRunSeed(18);Reset();Check(Begin(),"opposite-side battery fixture starts");
            Check(fork.GetNode<Coin>("Reward0").Kind==PickupKind.Battery && !fork.GetNode<Coin>("Reward1").Visible && (float)P("RiskX")>fork.Position.X,"battery replaces the coins on the opposite risk branch");
            if(DisplayServer.GetName()!="headless")
            {
                Reset(250);Check(Begin(),"visual fixture starts");Tick(1.5f);player.Position=new Vector2((float)P("SafeX"),player.FixedY);
                level.GetNode<Hud>("HUD").GetNode<Control>("Control/StartMenuPanel").Hide();var camera=level.GetNode<Camera2D>("Camera2D");camera.Position=Vector2.Zero;camera.ForceUpdateScroll();
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var im=GetViewport().GetTexture().GetImage();im.SavePng("res://artifacts/route-fork-preview.png");
            }
            level.Free();level=null;await ToSignal(GetTree().CreateTimer(.1),SceneTreeTimer.SignalName.Timeout);GD.Print("ROUTE FORK SMOKE PASSED");GetTree().Quit();
        }
        catch(Exception ex){GetTree().Paused=false;GD.PushError(ex.ToString());level?.Free();GetTree().Quit(1);}
    }
}
