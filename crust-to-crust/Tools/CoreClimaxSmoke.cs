using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class CoreClimaxSmoke : Node
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
    static void Check(bool ok,string msg) { if(!ok) throw new Exception(msg); GD.Print("PASS: "+msg); }
    async System.Threading.Tasks.Task Wait(float s)=>await ToSignal(GetTree().CreateTimer(s),SceneTreeTimer.SignalName.Timeout);
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            SaveData.Load();SaveData.TutorialComplete=true;
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>();AddChild(level);
            level.SetRunSeed(17);level.SetProcess(false);level.SetPhysicsProcess(false);
            var player=level.Runner;player.SetProcess(false);player.SetPhysicsProcess(false);player.GetNode<Area2D>("Hitbox").Monitoring=false;
            var method=typeof(LevelGenerator).GetMethod("TryStartCoreClimax");Check(method!=null,"core climax has a coordinated entry point");
            var scene=level.GetNode<Node2D>("CoreClimaxEvent");scene.SetProcess(false);
            var update=scene.GetType().GetMethod("_Process");object P(string n)=>scene.GetType().GetProperty(n).GetValue(scene);
            var rocks=(List<Node2D>)typeof(LevelGenerator).GetField("obstaclePool",Hidden).GetValue(level);
            bool Begin()=>(bool)method.Invoke(level,null);
            void Reset(float speed=600,float depth=17800)
            {
                level.StopFalling();level.IsGameActive=true;level.CurrentDepth=depth;level.FallSpeed=speed;
                var state=typeof(Player).GetField("_state",Hidden);state.SetValue(player,Enum.Parse(state.FieldType,"Playing"));
                player.Position=new Vector2(120,player.FixedY);Set(player,"_temperature",0f);Set(player,"_coolingGrace",0f);Set(player,"_hasPreviousContact",false);
                Set(level,"_coreRespite",0f);Set(level,"_coreLap",0);Set(level,"_climaxLap",0);Set(level,"_collapseEventTimer",-100f);Set(level,"_pressureTimer",-100f);Set(level,"_magmaTimer",-100f);
                Set(level,"nextWaterInterval",1000f);Set(level,"waterTimer",0f);Set(level,"_wasHotZone",true);
                foreach(var cold in (List<Node2D>)typeof(LevelGenerator).GetField("waterPool",Hidden).GetValue(level)){cold.Hide();((Area2D)cold).Monitorable=false;cold.SetProcess(false); }Set(level,"_encounterEndDistance",float.MaxValue);
                typeof(LevelGenerator).GetProperty("Encounter").SetValue(level,EncounterKind.Rest);
                foreach(var r in rocks) { r.Hide();r.SetProcess(false);((Area2D)r).Monitorable=false; }
            }
            bool normalCryo=false;
            void Tick(float s)
            {
                while(s>0)
                {
                    float dt=Mathf.Min(.01f,s);s-=dt;level._Process(dt);update.Invoke(scene,new object[] {(double)dt});
                    foreach(var r in rocks) if(r.Visible) ((Obstacle)r)._Process(dt);
                    if(normalCryo)
                    {
                        foreach(var cold in (List<Node2D>)typeof(LevelGenerator).GetField("waterPool",Hidden).GetValue(level)) if(cold.Visible) ((Water)cold)._Process(dt);
                        typeof(Player).GetMethod("UpdateHeating",Hidden).Invoke(player,new object[] {dt});
                    }
                    level._PhysicsProcess(dt);
                }
            }
            Reset();Check(Begin(),"safe core approach starts the climax");Tick(1.15f);
            Check((int)P("WavesReleased")==0,"opening lasts the full warning before debris");
            Check(scene.GetNode<AudioStreamPlayer2D>("CoreOpening").Playing,"opening has a dedicated audible cue");
            Tick(.1f);Check((int)P("WavesReleased")==1,"central choice appears after warning");
            bool left=true,right=true;foreach(var r in rocks) if(r.Visible)
            { float radius=29*r.Scale.X;left &= Mathf.Abs(r.Position.X+120)>radius+player.CollisionEnvelopeRadius;right &= Mathf.Abs(r.Position.X-120)>radius+player.CollisionEnvelopeRadius; }
            Check(left && right,"central fragment leaves both side passages for the full character envelope");
            Check(!level.ProjectilesAllowed && !level.TryStartCorePressure(1),"ordinary volleys and pressure cannot stack onto the climax");
            Tick(1.6f);Check((int)P("WavesReleased")==2,"second protected rubble wave follows the choice");
            Tick(1.2f);Check(level.CurrentDepth>level.CoreDepthPoint,"fixture crosses the actual core point");
            Check((int)P("WavesReleased")==2 && P("Phase").ToString()!="Dormant","core crossing preserves the active event");
            Check((float)typeof(LevelGenerator).GetField("_coreRespite",Hidden).GetValue(level)==0,"core crossing does not grant invulnerability inside the active sequence");
            Tick(1);Check(P("Phase").ToString()=="Recovery" && !level.ProjectilesAllowed,"recovery begins after the final physical passage");
            Tick(1.8f);Check(!scene.Visible && level.PressureVelocity==0,"climax ends cleanly without residual pressure");
            level.CurrentDepth=17800;Check(!Begin(),"the same lap cannot replay the climax after recovery");
            Reset();Check(!BeginAlready(),"a second attempt cannot replace an active episode");
            bool BeginAlready() { bool first=Begin();return first && Begin(); }
            Tick(.4f);scene.SetProcess(true);GetTree().Paused=true;float time=(float)P("Elapsed");await Wait(.1f);
            Check((float)P("Elapsed")==time,"pause freezes the whole opening");GetTree().Paused=false;scene.SetProcess(false);
            level.StopFalling();Check(!scene.Visible && !scene.GetNode<AudioStreamPlayer2D>("CoreOpening").Playing,"stop clears visuals and opening audio");
            Reset();
            Check(Begin(),"heat fixture starts");Tick(1.4f);Set(player,"_temperature",.85f);Tick(.1f);
            Check(P("Phase").ToString()=="Recovery","dangerous heat cancels further debris instead of blocking cooling");
            Reset();Check(Begin(),"cooling fixture starts");Tick(1.4f);
            var cold=GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>();level.AddChild(cold);
            ((List<Node2D>)typeof(LevelGenerator).GetField("waterPool",Hidden).GetValue(level)).Add(cold);cold.Position=player.Position;cold.speed=0;cold.SetProcess(false);Tick(.1f);await Wait(.05f);
            Check(P("Phase").ToString()=="Recovery","a cryofissure immediately takes priority over the climax");
            bool harmless=true;foreach(var r in rocks) if(r.Visible) harmless &= !((Area2D)r).Monitorable;Check(harmless,"cancelled debris cannot block cooling");cold.Hide();cold.Monitorable=false;
            Reset(1200);Check(Begin(),"high speed preserves a full opening");Tick(3);
            Check((int)P("WavesReleased")==2,"a fast core crossing does not skip or cancel the second wave");
            Reset();level.CurrentDepth=1000;Check(!Begin(),"event cannot start in basalt");
            Reset();Set(player,"_temperature",.9f);Check(!Begin(),"hot player is not committed to the sequence");
            Reset();foreach(var r in rocks)r.Show();Check(!Begin(),"insufficient pool space does not create an incomplete climax");
            var contact=(CollisionShape2D)typeof(Player).GetField("_contactShape",Hidden).GetValue(player);var original=contact.Shape;
            foreach(int id in new[] {0,6,9,12,14,16,18,21,24})
            {
                Reset();contact.Shape=CharacterCatalog.Get(id).CreateBodyShape();Check(Begin(),"current form fixture starts");Tick(1.4f);
                var central=rocks.Find(r=>r.Visible);float radius=29*central.Scale.X;
                player.Position=new Vector2(-120,player.FixedY);Set(player,"_hasPreviousContact",false);
                bool leftSafe=!player.ProjectileSweepHits(new Vector2(central.Position.X,-300),new Vector2(central.Position.X,-100),radius);
                player.Position=new Vector2(120,player.FixedY);Set(player,"_hasPreviousContact",false);
                bool rightSafe=!player.ProjectileSweepHits(new Vector2(central.Position.X,-300),new Vector2(central.Position.X,-100),radius);
                player.Position=new Vector2(0,player.FixedY);Set(player,"_hasPreviousContact",false);
                bool middleHits=player.ProjectileSweepHits(new Vector2(central.Position.X,-300),new Vector2(central.Position.X,-100),radius);
                Check(leftSafe && rightSafe && middleHits,"both branches clear the actual "+CharacterCatalog.Get(id).Form+" shape while the central hazard remains solid");
            }
            contact.Shape=original;
            Reset();Set(player,"_coolingGrace",7f);Set(level,"nextWaterInterval",4f);normalCryo=true;
            Check(Begin(),"just-cooled runner can enter with the normal four-second fissure interval");Tick(4.4f);
            Check((int)P("WavesReleased")==2 && player.Temperature<.01f,"normal cryo cadence and real protection permit both climax waves");
            normalCryo=false;
            Reset();for(float phase=0;phase<12000;phase+=20){Set(level,"_routeDistance",phase);if(level.SafeLaneAt(700)>125)break;}
            Check(Begin(),"narrow-side fixture starts");Tick(3);
            Check(((List<Obstacle>)scene.GetType().GetField("_debris",Hidden).GetValue(scene)).Count==3,"second wave supplies both fragments even on the narrow side");
            Reset();float narrowY=0,minRight=float.PositiveInfinity;
            for(float y=0;y<720;y+=10){level.GetRockBounds(y,out _,out float r);if(r<minRight){minRight=r;narrowY=y;}}
            for(float phase=0;phase<20000;phase+=10){Set(level,"_routeDistance",phase);if(level.SafeLaneAt(narrowY)>130)break;}
            Check(minRight-level.SafeLaneAt(narrowY)<170,"fixture actually selects a narrow side");
            Check(Begin(),"explicit narrow cross-section fixture starts");
            var pair=new List<Obstacle>();Check(level.TryReleaseCoreClimaxWave(narrowY,1,pair)==2,"both sides of a narrow cross-section receive a correctly sized fragment");
            var audio=level.GetNode<AudioManager>("AudioManager");Set(audio,"_gameplayMusic",true);
            audio.SetClimaxPresence(1);audio._Process(.1);
            var music=(AudioStreamPlayer)typeof(AudioManager).GetField("_gameScore",Hidden).GetValue(audio);
            Check(music.PitchScale<1 && music.VolumeDb< -2,"climax changes the existing gameplay music without replacing it");
            level.StopFalling();audio._Process(.1);
            Check(Math.Abs(music.PitchScale-1)<.001 && Math.Abs(music.VolumeDb+2)<.001,"stopping restores normal music pitch and level");
            if(DisplayServer.GetName()!="headless")
            {
                Reset(600);Check(Begin(),"visual fixture starts");Tick(2.5f);
                ((Control)typeof(Hud).GetField("_coreBanner",Hidden).GetValue(level.GetNode<Hud>("HUD"))).Hide();
                level.GetNode<Hud>("HUD").GetNode<Control>("Control/StartMenuPanel").Hide();var cam=level.GetNode<Camera2D>("Camera2D");cam.Position=Vector2.Zero;cam.ForceUpdateScroll();
                await Wait(.05f);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var im=GetViewport().GetTexture().GetImage();im.SavePng("res://artifacts/core-climax-preview.png");
            }
            level.Free();level=null;await Wait(.2f);GD.Print("CORE CLIMAX SMOKE PASSED");GetTree().Quit();
        }
        catch(Exception ex) { GetTree().Paused=false;GD.PushError(ex.ToString());level?.Free();GetTree().Quit(1); }
    }
}
