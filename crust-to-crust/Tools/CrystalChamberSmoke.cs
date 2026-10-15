using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class CrystalChamberSmoke : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    private static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
    private async System.Threading.Tasks.Task Wait(float t)=>await ToSignal(GetTree().CreateTimer(t),SceneTreeTimer.SignalName.Timeout);
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            SaveData.Load(); SaveData.TutorialComplete=true;
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            level.SetRunSeed(17); level.SetProcess(false); level.SetPhysicsProcess(false);
            var player=level.Runner; player.SetProcess(false); player.SetPhysicsProcess(false); player.GetNode<Area2D>("Hitbox").Monitoring=false;
            var start=typeof(LevelGenerator).GetMethod("TryStartCrystalChamber");
            Check(start!=null,"crystal chamber has a coordinated entry point");
            var chamber=level.GetNode<Node2D>("CrystalChamberEvent"); chamber.SetProcess(false);
            var update=chamber.GetType().GetMethod("_Process");
            object Property(string name)=>chamber.GetType().GetProperty(name).GetValue(chamber);
            var rocks=(List<Node2D>)typeof(LevelGenerator).GetField("obstaclePool",Hidden).GetValue(level);
            bool Begin()=>(bool)start.Invoke(level,null);
            void Reset(float speed=450,float depth=8001)
            {
                level.StopFalling(); level.IsGameActive=true; level.CurrentDepth=depth; level.FallSpeed=speed;
                var state=typeof(Player).GetField("_state",Hidden);state.SetValue(player,Enum.Parse(state.FieldType,"Playing"));
                player.Position=new Vector2(level.SafeLaneAt(player.FixedY),player.FixedY);
                Set(level,"_majorEventZone",CaveZone.Crystal);
                Set(player,"_temperature",0f); Set(level,"_coreRespite",0f); Set(level,"nextWaterInterval",1000f); Set(level,"waterTimer",0f);
                Set(level,"_forkTimer",-100f);Set(level,"_basaltTimer",-100f); Set(level,"_crystalTimer",-100f); Set(level,"_collapseEventTimer",-100f);
                Set(level,"_encounterEndDistance",float.MaxValue); typeof(LevelGenerator).GetProperty("Encounter").SetValue(level,EncounterKind.Rest);
                foreach(var rock in rocks) { rock.Hide(); rock.SetProcess(false); ((Area2D)rock).Monitorable=false; }
            }
            bool clear=true,reachable=true;
            void Tick(float seconds,float frame=.01f)
            {
                while(seconds>0)
                {
                    float dt=Mathf.Min(seconds,frame);seconds-=dt;
                    float previousLane=level.SafeLaneAt(player.FixedY);
                    level._Process(dt);reachable &= Math.Abs(level.SafeLaneAt(player.FixedY)-previousLane)<=player.HorizontalSpeed*dt+.1f; player.Position=new Vector2(level.SafeLaneAt(player.FixedY),player.FixedY);update.Invoke(chamber,new object[] {(double)dt});
                    foreach(var node in rocks) if(node.Visible)
                    {
                        ((Obstacle)node)._Process(dt);
                        float radius=49*node.Scale.X;
                        if(Math.Abs(node.Position.Y-player.FixedY)<radius+player.CollisionEnvelopeRadius)
                            clear &= Math.Abs(node.Position.X-level.SafeLaneAt(node.Position.Y))>=radius+player.CollisionEnvelopeRadius;
                    }
                }
            }
            Reset(); Check(Begin(),"cold crystal belt permits the event");
            Check(Property("Phase").ToString()=="Warning","preparation precedes the collapsing chamber");
            Tick(1.35f); Check((int)Property("WavesReleased")==0,"resonance gives the full warning before any shard");
            var resonance=chamber.GetNode<AudioStreamPlayer2D>("ChamberResonance");
            Check(resonance.Playing && resonance.Bus==AudioManager.SfxBus,"preparation plays its own localized resonance cue");
            Tick(.1f); Check((int)Property("WavesReleased")==1,"first wave follows the warning");
            Check(!level.ProjectilesAllowed && !level.TryStartCollapseEvent() && !level.TryStartDrillShaft() && !level.TryStartCrystalResonance(-1,900),"event excludes every conflicting encounter");
            var sides=new HashSet<int>(); foreach(var node in rocks) if(node.Visible) sides.Add(Math.Sign(node.Position.X-level.SafeLaneAt(node.Position.Y)));
            Check(sides.Count==1 && sides.Contains(-1),"first shards originate from the left wall");
            Tick(1.3f); sides.Clear(); foreach(var node in rocks) if(node.Visible) sides.Add(Math.Sign(node.Position.X-level.SafeLaneAt(node.Position.Y)));
            Check(sides.Contains(1),"second pulse releases the opposite wall");
            Tick(1.3f); Check((int)Property("WavesReleased")==3,"three waves complete the active sequence");
            Check((int)Property("DebrisReleased")==8 && rocks.Count==16,"eight real shards reuse the existing bounded pool");
            Tick(2.3f); Check(clear && reachable,"actual moving shards leave a corridor within the steering speed");
            Check(Property("Phase").ToString()=="Recovery" && !level.ProjectilesAllowed,"last wave is followed by protected recovery");
            int released=(int)Property("DebrisReleased"); Tick(.4f); Check((int)Property("DebrisReleased")==released,"recovery cannot release late shards");
            Tick(1.4f); Check(!chamber.Visible && level.ProjectilesAllowed && !resonance.Playing,"ordinary encounters resume after recovery");
            Reset(); Check(Begin(),"pause fixture starts"); Tick(.5f); chamber.SetProcess(true); GetTree().Paused=true;
            float elapsed=(float)Property("Elapsed"); await Wait(.1f); Check((float)Property("Elapsed")==elapsed,"pause freezes resonance and release timers"); GetTree().Paused=false; chamber.SetProcess(false);
            level.StopFalling(); Check(!chamber.Visible && !resonance.Playing,"stop clears chamber visuals and sounds");
            var shape=(CollisionShape2D)typeof(Player).GetField("_contactShape",Hidden).GetValue(player); var original=shape.Shape;
            foreach(int id in new[] {0,6,9,12,14,16})
            {
                Reset(); shape.Shape=CharacterCatalog.Get(id).CreateBodyShape(); clear=true; Check(Begin(),"shape fixture starts"); Tick(6.3f,.05f);
                Check(clear,"safe scrolling passage fits "+CharacterCatalog.Get(id).Form);
            }
            shape.Shape=original;
            Reset(1200,28001); clear=true; Check(Begin(),"return crystal belt supports a high-speed chamber"); Tick(6.3f,.1f);
            Check(clear && reachable && (int)Property("WavesReleased")==3,"low frame rate and high speed preserve warnings and clearance");
            Reset(); Check(Begin(),"cooling fixture starts"); Tick(1.6f);
            var cold=GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>(); level.AddChild(cold);
            ((List<Node2D>)typeof(LevelGenerator).GetField("waterPool",Hidden).GetValue(level)).Add(cold); cold.Position=new Vector2(0,-200); cold.speed=0; cold.SetProcess(false);
            Tick(.1f); await Wait(.05f); Check(Property("Phase").ToString()=="Recovery","cooling immediately cancels further waves");
            bool harmless=true; foreach(var rock in rocks) if(rock.Visible) harmless &= !((Area2D)rock).Monitorable;
            Check(harmless,"released shards become harmless when cooling access is needed");
            Reset(); Check(!Begin(),"cooling blocks a new event"); cold.Hide();cold.Monitorable=false;
            Reset(); Set(player,"_temperature",.6f);Check(!Begin(),"hot player cannot start the event");
            Reset(); Check(Begin(),"mid-event heat fixture starts");Tick(1.6f);Set(player,"_temperature",.6f);Tick(.1f);Check(Property("Phase").ToString()=="Recovery","rising heat safely ends an existing event");
            Reset(5000); Check(!Begin(),"insufficient cold distance rejects a high-speed event");
            Reset(); level.CurrentDepth=1000; Check(!Begin(),"basalt does not spawn the crystal chamber");
            Reset(); level.CurrentDepth=20000; Check(!Begin(),"core does not spawn the crystal chamber");
            Reset(); foreach(var rock in rocks) rock.Show(); Check(!Begin(),"exhausted pool rejects an incomplete sequence");
            Reset(); Set(level,"_collapseEventTimer",60f); typeof(LevelGenerator).GetMethod("SpawnTimers",Hidden).Invoke(level,new object[] {.01f});
            Check(chamber.Visible,"normal major-event scheduler selects the chamber in the crystal belt");
            Reset(); Set(level,"_majorEventZone",CaveZone.Basalt);Set(level,"_collapseEventDelay",60f);Set(level,"_collapseEventTimer",0f);
            var schedule=typeof(LevelGenerator).GetMethod("SpawnTimers",Hidden);
            schedule.Invoke(level,new object[] {.5f});Check(!chamber.Visible,"entering the belt retains a short approach before the event");
            schedule.Invoke(level,new object[] {.5f});schedule.Invoke(level,new object[] {.5f});
            Check(chamber.Visible,"earlier basalt cooldown cannot hide the entire crystal event");
            Check(chamber.GetChildCount()==4,"repeat activation reuses the same warning and audio nodes");
            Reset(340,0);Set(level,"_majorEventZone",CaveZone.Basalt);Set(level,"_collapseEventDelay",60f);Set(level,"_collapseEventTimer",-100f);
            while(level.CurrentDepth<13000 && !chamber.Visible) level._Process(.02);
            Check(chamber.Visible,"normal acceleration from the starting speed reaches a playable crystal event");
            Tick(6.3f);Check((int)Property("WavesReleased")==3,"ordinary first-run acceleration supports the complete chamber sequence");
            Reset();Check(Begin(),"sudden acceleration fixture starts");Tick(1);level.FallSpeed=5000;Tick(.5f);
            Check(Property("Phase").ToString()=="Recovery" && (int)Property("DebrisReleased")==0,"sudden acceleration cancels a wave whose safe lead has vanished");
            if(DisplayServer.GetName()!="headless")
            {
                Reset(180); Check(Begin(),"visual fixture starts"); Tick(.8f);
                level.GetNode<Hud>("HUD").GetNode<Control>("Control/StartMenuPanel").Hide();
                var camera=level.GetNode<Camera2D>("Camera2D"); camera.Position=Vector2.Zero;camera.ForceUpdateScroll();
                await Wait(.05f);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using(var im=GetViewport().GetTexture().GetImage()) im.SavePng("res://artifacts/crystal-chamber-warning.png");
                Tick(1.5f);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using(var im=GetViewport().GetTexture().GetImage()) im.SavePng("res://artifacts/crystal-chamber-passage.png");
            }
            level.Free();level=null;await Wait(.2f);GD.Print("CRYSTAL CHAMBER SMOKE PASSED");GetTree().Quit();
        }
        catch(Exception ex) { GetTree().Paused=false;GD.PushError(ex.ToString());level?.Free();GetTree().Quit(1); }
    }
}
