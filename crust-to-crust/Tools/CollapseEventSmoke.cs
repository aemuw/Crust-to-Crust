using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class CollapseEventSmoke : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    private static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Hidden).SetValue(obj,value);
    private async System.Threading.Tasks.Task Wait(float t)=>await ToSignal(GetTree().CreateTimer(t),SceneTreeTimer.SignalName.Timeout);
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
            var start=typeof(LevelGenerator).GetMethod("TryStartCollapseEvent");
            Check(start!=null,"large collapse has a coordinated event entry point");
            var collapse=level.GetNode<Node2D>("CollapseEvent"); collapse.SetProcess(false);
            var update=collapse.GetType().GetMethod("_Process");
            object Property(string name)=>collapse.GetType().GetProperty(name).GetValue(collapse);
            var rocks=(List<Node2D>)typeof(LevelGenerator).GetField("obstaclePool",Hidden).GetValue(level);
            bool Begin()=>(bool)start.Invoke(level,null);
            void Reset(float speed=500)
            {
                level.StopFalling(); level.IsGameActive=true; level.CurrentDepth=1000; level.FallSpeed=speed;
                Set(player,"_temperature",0f); Set(level,"_coreRespite",0f);
                Set(level,"nextWaterInterval",1000f); Set(level,"waterTimer",0f);
                Set(level,"_basaltTimer",-100f); Set(level,"_crystalTimer",-100f); Set(level,"_collapseEventTimer",0f);
                Set(level,"_encounterEndDistance",float.MaxValue);
                typeof(LevelGenerator).GetProperty("Encounter").SetValue(level,EncounterKind.Rest);
                foreach(var rock in rocks) { rock.Hide(); rock.SetProcess(false); ((Area2D)rock).Monitorable=false; }
            }
            void Tick(float seconds,float frame=.01f)
            {
                while(seconds>0)
                {
                    float dt=Mathf.Min(seconds,frame); seconds-=dt;
                    level._Process(dt); update.Invoke(collapse,new object[] {(double)dt});
                    foreach(var node in rocks) if(node.Visible) ((Obstacle)node)._Process(dt);
                }
            }
            Reset(); Check(Begin(),"a cold, clear stretch can begin a major collapse");
            Check(Property("Phase").ToString()=="Warning","event begins with preparation");
            Tick(1); Check((int)Property("WavesReleased")==0,"dust and cracking precede every dangerous passage");
            var rumble=collapse.GetNode<AudioStreamPlayer2D>("CollapseRumble");
            Check(rumble.Playing && rumble.Bus==AudioManager.SfxBus,"preparation has its own rock-pressure audio");
            Tick(.6f); Check((int)Property("WavesReleased")==1,"the first gate arrives after the full warning");
            bool readable=false; foreach(var node in rocks) if(node.Visible) readable |= node.GetNodeOrNull<Line2D>("RubbleEdge")?.Visible==true;
            Check(readable,"large rubble keeps a restrained visible contour in darkness");
            Check(!level.ProjectilesAllowed && !level.TryStartBasaltCollapse(-1,900),"event excludes turret volleys and local wall collapses");
            Tick(2.2f); Check((int)Property("WavesReleased")==3,"three separated passages are released in sequence");
            Check((int)Property("DebrisReleased")>=3 && (int)Property("DebrisReleased")<=6 && rocks.Count==16,"large fragments reuse the bounded obstacle pool");
            bool clear=true;
            for(int i=0;i<220;i++)
            {
                Tick(.01f); float lane=level.SafeLaneAt(-200);
                foreach(var node in rocks)
                {
                    float radius=29*node.Scale.X;
                    if(node.Visible && Math.Abs(node.Position.Y+200)<radius+player.CollisionEnvelopeRadius)
                        clear &= Math.Abs(node.Position.X-lane)>=radius+player.CollisionEnvelopeRadius;
                }
            }
            Check(clear,"real fragment envelopes leave a reachable passage throughout the scrolling sequence");
            Tick(.5f); Check(Property("Phase").ToString()=="Recovery" && !level.ProjectilesAllowed,"the last passage is followed by a protected recovery");
            int released=(int)Property("WavesReleased"); Tick(.4f);
            Check((int)Property("WavesReleased")==released,"recovery never creates late debris");
            Tick(1.4f); Check(!collapse.Visible && level.ProjectilesAllowed,"normal spawning unlocks after recovery");

            Reset(); Check(Begin(),"repeat fixture starts with clean state"); Tick(.4f);
            collapse.SetProcess(true); GetTree().Paused=true; float elapsed=(float)Property("Elapsed"); await Wait(.1f);
            Check((float)Property("Elapsed")==elapsed,"pause freezes the entire sequence"); GetTree().Paused=false; collapse.SetProcess(false);
            level.StopFalling(); Check(!collapse.Visible && !rumble.Playing,"stop removes all event warnings and sound");

            Reset(); Check(Begin(),"cooling cancellation fixture starts"); Tick(1.6f);
            var cold=GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>(); level.AddChild(cold);
            ((List<Node2D>)typeof(LevelGenerator).GetField("waterPool",Hidden).GetValue(level)).Add(cold);
            cold.Position=new Vector2(0,-200); cold.speed=0; cold.SetProcess(false);
            Tick(.1f); await Wait(.05f);
            Check(Property("Phase").ToString()=="Recovery","new cooling access ends the dangerous sequence early");
            bool harmless=true; foreach(var node in rocks) if(node.Visible) harmless &= !((Area2D)node).Monitorable;
            Check(harmless,"released debris retires smoothly and cannot block cooling");
            Reset(); Check(!Begin(),"major collapse does not start on a cooling approach"); cold.Hide(); cold.Monitorable=false;
            level.CurrentDepth=level.CoreDepthPoint; Check(!Begin(),"major collapse cannot overlap the core");
            Reset(); Set(player,"_temperature",.6f); Check(!Begin(),"a hot player is not trapped in a long event");
            Reset(5000); Check(!Begin(),"insufficient remaining safe biome distance suppresses a late high-speed event");
            Reset(); foreach(var rock in rocks) rock.Show(); Check(!Begin(),"exhausted pool does not begin an incomplete event");
            Reset(1200); Check(Begin(),"a sufficiently long stretch supports a high-speed event"); Tick(4);
            Check((int)Property("WavesReleased")==3,"high speed preserves all three timed warnings");
            Reset(); Set(level,"_collapseEventTimer",12f);
            typeof(LevelGenerator).GetMethod("SpawnTimers",Hidden).Invoke(level,new object[] {.01f});
            Check(collapse.Visible,"normal gameplay scheduling starts the major event");
            if(DisplayServer.GetName()!="headless")
            {
                Reset(200); Check(Begin(),"visual fixture starts"); Tick(.8f);
                level.GetNode<Hud>("HUD").GetNode<Control>("Control/StartMenuPanel").Hide();
                var camera=level.GetNode<Camera2D>("Camera2D"); camera.Position=Vector2.Zero; camera.ForceUpdateScroll();
                await Wait(.05f); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using(var im=GetViewport().GetTexture().GetImage()) im.SavePng("res://artifacts/collapse-warning-preview.png");
                Tick(1.2f); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using(var im=GetViewport().GetTexture().GetImage()) im.SavePng("res://artifacts/collapse-preview.png");
            }
            level.Free(); level=null; await Wait(.2f); GD.Print("COLLAPSE EVENT SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { GetTree().Paused=false; GD.PushError(ex.ToString()); level?.Free(); GetTree().Quit(1); }
    }
}

