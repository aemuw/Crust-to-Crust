using Godot;
using System;
using System.Reflection;

public partial class CorePressureSmoke : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    private static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Hidden).SetValue(obj,value);
    private async System.Threading.Tasks.Task Wait(float t)=>await ToSignal(GetTree().CreateTimer(t),SceneTreeTimer.SignalName.Timeout);
    private static void Key(bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=Godot.Key.A,Keycode=Godot.Key.A,Pressed=pressed });
        Input.FlushBufferedEvents();
    }
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            SaveData.Load(); SaveData.TutorialComplete=true;
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            level.SetRunSeed(17); level.SetProcess(false); level.SetPhysicsProcess(false);
            var player=level.Runner; player.SetProcess(false); player.SetPhysicsProcess(false);
            player.Position=new Vector2(0,-200); player.GetNode<Area2D>("Hitbox").Monitoring=false;
            var state=typeof(Player).GetField("_state",Hidden); state.SetValue(player,Enum.Parse(state.FieldType,"Playing"));
            level.IsGameActive=true; level.CurrentDepth=17500; level.FallSpeed=600;
            var start=typeof(LevelGenerator).GetMethod("TryStartCorePressure");
            Check(start!=null,"core has a bounded pressure-wave encounter");
            var wave=level.GetNode<Node2D>("CorePressureWave");
            var advance=wave.GetType().GetMethod("Advance");
            bool Begin(int direction)=>(bool)start.Invoke(level,new object[] {direction});
            float Drift()=>(float)typeof(LevelGenerator).GetProperty("PressureVelocity").GetValue(level);
            void Tick(double dt) { advance.Invoke(wave,new object[] {(float)dt}); player._PhysicsProcess(dt); }
            void Reset()
            {
                level.StopFalling(); level.IsGameActive=true; level.CurrentDepth=17500; level.FallSpeed=600;
                Set(level,"_coreRespite",0f); player.Position=new Vector2(0,-200); Set(player,"_currentVelocityX",0f);
            }
            Check(Begin(1),"a core pressure wave can warn before pushing right");
            Tick(.8); Check(Drift()==0 && Math.Abs(player.Position.X)<.01f,"full warning precedes all forced movement");
            var sound=wave.GetNode<AudioStreamPlayer2D>("PressureCue");
            Check(sound.Stream!=null && sound.Playing && sound.Bus==AudioManager.SfxBus,"pressure warning plays its own positional sound");
            float max=0;
            for(int i=0;i<100;i++) { Tick(.01); max=Mathf.Max(max,Mathf.Abs(Drift())); }
            Check(player.Position.X>10 && player.Position.X<50 && max<=player.HorizontalSpeed*.2f+.01f,"rightward gust moves less than one figure width and stays weaker than control");
            Check(!level.ProjectilesAllowed,"pressure cannot overlap new turret volleys");
            level.SetPhysicsProcess(true); player.SetPhysicsProcess(true); GetTree().Paused=true;
            float before=player.Position.X; await Wait(.12f);
            Check(player.Position.X==before,"pause freezes both the field and the player's displacement");
            GetTree().Paused=false; level.SetPhysicsProcess(false); player.SetPhysicsProcess(false);
            Reset(); Check(!wave.Visible && !sound.Playing && Drift()==0,"stop clears sound, warning, and drift without residual force");
            Tick(.1); Check(Math.Abs(player.Position.X)<.01f,"restarting does not inherit old pressure velocity");
            Check(Begin(-1),"reused wave can reverse direction");
            for(int i=0;i<220;i++) Tick(.01);
            Check(player.Position.X< -10 && player.Position.X> -50,"leftward pulse is equally bounded");
            Reset(); float lane=level.SafeLaneAt(-200); player.Position=new Vector2(lane,-200);
            Check(Begin(1),"protected-corridor fixture starts"); for(int i=0;i<220;i++) Tick(.01);
            Check(player.Position.X<=lane+SafePassage.HalfWidth+.1f,"pressure cannot drive a safe player out into future moving hazards");
            Reset(); Check(Begin(1),"countersteering fixture starts"); Key(true);
            for(int i=0;i<170;i++) Tick(.01); Key(false);
            Check(player.Position.X< -60,"keyboard countersteering overcomes the pressure wave");

            var contact=(CollisionShape2D)typeof(Player).GetField("_contactShape",Hidden).GetValue(player); var original=contact.Shape;
            foreach(int id in new[] {0,6,9,12,14,16})
            {
                Reset(); contact.Shape=CharacterCatalog.Get(id).CreateBodyShape(); Check(Begin(1),"shape fixture starts");
                for(int i=0;i<220;i++) Tick(.01);
                Check(player.Position.X>10 && player.Position.X<50,"pressure works with "+CharacterCatalog.Get(id).Form+" collision");
            }
            contact.Shape=original;
            Reset(); level.GetRockBounds(-200,out _,out float right); player.Position=new Vector2(right-player.CollisionEnvelopeRadius-8,-200);
            before=player.Position.X; Check(Begin(1),"wall fixture starts"); for(int i=0;i<220;i++) Tick(.01);
            Check(player.Position.X<=before+.1f,"pressure cannot push the collision envelope into the wall");

            Reset(); var blocker=new Area2D { CollisionLayer=2,CollisionMask=0,Position=new Vector2(43,-200) };
            blocker.AddChild(new CollisionShape2D { Shape=new RectangleShape2D { Size=new Vector2(20,110) } }); level.AddChild(blocker);
            await Wait(.08f); Check(Begin(1),"solid-obstacle fixture starts"); for(int i=0;i<220;i++) Tick(.01);
            Check(player.Position.X<10,"forced movement stops before an actual solid obstacle"); blocker.QueueFree(); await Wait(.05f);

            float Displacement(double step)
            {
                Reset(); Check(Begin(1),"frame-rate fixture starts"); double remaining=2.3;
                while(remaining>0) { double dt=Math.Min(step,remaining); Tick(dt); remaining-=dt; }
                return player.Position.X;
            }
            float fine=Displacement(.01), coarse=Displacement(.16);
            Check(Math.Abs(fine-coarse)<1,"gust displacement is stable with low physics tick rates");
            Reset(); level.CurrentDepth=14000; Check(!Begin(1),"pressure stays out of the magma biome");
            level.CurrentDepth=17500;
            var cold=GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>(); level.AddChild(cold);
            ((System.Collections.Generic.List<Node2D>)typeof(LevelGenerator).GetField("waterPool",Hidden).GetValue(level)).Add(cold);
            cold.Position=new Vector2(0,-200); cold.speed=0; cold.SetProcess(false);
            Check(!Begin(1),"cooling approach suppresses pressure"); cold.Hide(); cold.Monitorable=false;
            Check(Begin(1),"pressure can start after cooling clears"); cold.Show(); Tick(1.3);
            Check(!wave.Visible && Drift()==0 && !sound.Playing,"new cooling access cancels warning and force"); cold.Hide();
            Reset(); Check(Begin(1),"core crossing fixture starts"); level.CurrentDepth=19999; level._Process(.01);
            Check(!wave.Visible && Drift()==0,"core crossing respite clears an in-flight wave");
            Reset(); typeof(LevelGenerator).GetProperty("Encounter").SetValue(level,EncounterKind.Rest);
            typeof(LevelGenerator).GetMethod("SpawnTimers",Hidden).Invoke(level,new object[] {8.1f});
            Check(wave.Visible,"normal gameplay scheduling starts a pressure wave");
            // Physics integration is driven by the level before the player, independent of render FPS.
            foreach(Node2D node in (System.Collections.Generic.List<Node2D>)typeof(LevelGenerator).GetField("waterPool",Hidden).GetValue(level)) node.Hide();
            level._PhysicsProcess(1.4); Check(Math.Abs(Drift())>0,"level physics advances the pressure field");
            Reset(); level.FallSpeed=2200; level.CurrentDepth=21500; Check(Begin(-1),"return-side core supports high-speed pressure");
            Tick(.8); Check(Drift()==0,"high falling speed does not shorten the warning");
            if(DisplayServer.GetName()!="headless")
            {
                Reset(); Check(Begin(1),"visual warning fixture starts"); Tick(.65);
                level.GetNode<Hud>("HUD").GetNode<Control>("Control/StartMenuPanel").Hide();
                var camera=level.GetNode<Camera2D>("Camera2D"); camera.Position=Vector2.Zero; camera.ForceUpdateScroll();
                await Wait(.05f); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using(var im=GetViewport().GetTexture().GetImage()) im.SavePng("res://artifacts/pressure-warning-preview.png");
                Tick(.9); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using(var im=GetViewport().GetTexture().GetImage()) im.SavePng("res://artifacts/pressure-preview.png");
            }
            level.Free(); level=null; await Wait(.2f); GD.Print("CORE PRESSURE SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { Key(false); GetTree().Paused=false; GD.PushError(ex.ToString()); level?.Free(); GetTree().Quit(1); }
    }
}




