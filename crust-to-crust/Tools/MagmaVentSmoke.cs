using Godot;
using System;
using System.Reflection;

public partial class MagmaVentSmoke : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    private static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Hidden).SetValue(obj,value);
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
            player.GetNode<Area2D>("Hitbox").Monitoring=false; player.Position=new Vector2(0,-200);
            var state=typeof(Player).GetField("_state",Hidden); state.SetValue(player,Enum.Parse(state.FieldType,"Playing"));
            level.IsGameActive=true; level.CurrentDepth=14000; level.FallSpeed=500;
            var start=typeof(LevelGenerator).GetMethod("TryStartMagmaVent"); Check(start!=null,"magma has a natural wall-gas encounter");
            var vent=level.GetNode<Node2D>("MagmaVent"); vent.SetProcess(false);
            var update=vent.GetType().GetMethod("_Process");
            object Property(string name)=>vent.GetType().GetProperty(name).GetValue(vent);
            bool Begin(int side,float y)=>(bool)start.Invoke(level,new object[] {side,y});
            Check(Begin(-1,700),"a magma fissure can start from the left wall");
            update.Invoke(vent,new object[] {.8});
            Check(!(bool)Property("IsEmitting") && (int)Property("BurstsReleased")==0,"warning gives at least 1.1 seconds before heat");
            var hiss=vent.GetNode<AudioStreamPlayer2D>("VentHiss");
            Check(hiss.Stream!=null && hiss.Playing && hiss.Bus==AudioManager.SfxBus,"wall warning has a positional SFX cue");
            float before=player.Temperature;
            update.Invoke(vent,new object[] {.4});
            Check((bool)Property("IsEmitting") && player.Temperature==before,"gas cannot heat the player outside its visible plume");
            Check(!level.ProjectilesAllowed,"turrets cannot overlap the active gas encounter");
            var points=(Vector2[])Property("PlumePolygon");
            float reach=(float)Property("Reach");
            level.GetRockBounds(vent.Position.Y,out float left,out float right);
            Check(reach>40 && reach<=(right-left)*.25f+1,"hot plume stays local to one quarter of the tunnel");
            Vector2 center=vent.ToGlobal(new Vector2(reach*.55f,-35*.55f));
            level.FallSpeed=0; player.Position=center; Set(player,"_temperature",.3f); Set(player,"_coolingGrace",0f);
            update.Invoke(vent,new object[] {.15}); float shortHeat=player.Temperature;
            Check(shortHeat>.3f && shortHeat<.4f,"actual plume contact adds bounded heat to the existing bar");
            update.Invoke(vent,new object[] {.15}); Check(player.Temperature>shortHeat,"contact duration matters rather than a one-shot penalty");
            Set(player,"_coolingGrace",2f); before=player.Temperature; update.Invoke(vent,new object[] {.1});
            Check(player.Temperature==before,"recent cryogenic protection still protects against hot gas");
            Set(player,"_coolingGrace",0f); player.Position=new Vector2(level.SafeLaneAt(vent.Position.Y),vent.Position.Y);
            before=player.Temperature; update.Invoke(vent,new object[] {.1});
            Check(player.Temperature==before,"protected route is outside the gas for the real player envelope");
            vent.SetProcess(true); GetTree().Paused=true; before=vent.Position.Y; float heat=player.Temperature;
            await Wait(.1f); Check(vent.Position.Y==before && player.Temperature==heat,"pause freezes gas motion and heating");
            GetTree().Paused=false; vent.SetProcess(false);
            level.StopFalling(); Check(!vent.Visible && !hiss.Playing,"stopping clears gas and both sound phases");
            level.IsGameActive=true; level.CurrentDepth=9000; level.FallSpeed=500;
            Check(!Begin(1,700),"gas does not appear outside magma");
            level.CurrentDepth=14000;
            var cold=GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>(); level.AddChild(cold);
            ((System.Collections.Generic.List<Node2D>)typeof(LevelGenerator).GetField("waterPool",Hidden).GetValue(level)).Add(cold);
            cold.Position=new Vector2(0,700); cold.speed=0; cold.SetProcess(false);
            Check(!Begin(1,700),"cooling access suppresses hot fissures"); cold.Hide(); cold.Monitorable=false;
            Check(Begin(1,700),"reused fissure can switch to the right wall");
            Check((int)Property("BurstsReleased")==0 && !(bool)Property("IsEmitting"),"reuse clears old burst and exposure");
            Check(vent.Position.X>0 && ((Vector2[])Property("PlumePolygon"))[1].X<0,"right-wall plume and its contact shape point into the tunnel");
            cold.Position=new Vector2(0,400); cold.Show(); update.Invoke(vent,new object[] {1.3});
            Check(!(bool)Property("IsEmitting"),"new cooling access cancels a pending discharge"); cold.Hide();
            level.StopFalling(); level.IsGameActive=true; level.FallSpeed=1200; level.CurrentDepth=24000;
            Check(Begin(1,1900),"return-side magma works at high speed");
            update.Invoke(vent,new object[] {.8}); Check((int)Property("BurstsReleased")==0,"high speed preserves warning duration");
            update.Invoke(vent,new object[] {.5}); Check((int)Property("BurstsReleased")==1,"high speed produces exactly one burst");
            float CrossingHeat(double frameStep)
            {
                level.StopFalling(); level.IsGameActive=true; level.FallSpeed=2200; level.CurrentDepth=23010;
                player.Position=new Vector2(0,-200); Set(player,"_temperature",0f); Set(player,"_coolingGrace",0f);
                Check(Begin(1,3650),"fast crossing starts with sufficient warning in magma");
                vent.SetProcess(false); update.Invoke(vent,new object[] {1.2});
                level.GetRockBounds(-200,out _,out float wall); player.Position=new Vector2(wall-55,-200);
                double leftTime=.9;
                while(leftTime>0) { double dt=Math.Min(frameStep,leftTime); update.Invoke(vent,new object[] {dt}); leftTime-=dt; }
                return player.Temperature;
            }
            float fineHeat=CrossingHeat(.01), coarseHeat=CrossingHeat(.16);
            GD.Print($"CROSSING HEAT: fine={fineHeat}, coarse={coarseHeat}"); Check(fineHeat>.01f && Math.Abs(fineHeat-coarseHeat)<.008f,"swept gas contact remains consistent at 2200 speed and low FPS");
            level.StopFalling(); level.IsGameActive=true; level.FallSpeed=500; level.CurrentDepth=14000;
            typeof(LevelGenerator).GetProperty("Encounter").SetValue(level,EncounterKind.Rest);
            typeof(LevelGenerator).GetMethod("SpawnTimers",Hidden).Invoke(level,new object[] {10.1f});
            Check(vent.Visible,"normal gameplay scheduling starts the vent without manual activation");
            level.StopFalling(); level.IsGameActive=true;
            if(DisplayServer.GetName()!="headless")
            {
                player.Position=new Vector2(0,-200); level.CurrentDepth=14000; level.FallSpeed=200;
                Check(Begin(-1,150),"visual fixture has an embedded wall fissure");
                vent.SetProcess(false); update.Invoke(vent,new object[] {1.3});
                level.GetNode<Hud>("HUD").GetNode<Control>("Control/StartMenuPanel").Hide();
                var camera=level.GetNode<Camera2D>("Camera2D"); camera.Position=Vector2.Zero; camera.ForceUpdateScroll();
                await Wait(.05f); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var im=GetViewport().GetTexture().GetImage(); im.SavePng("res://artifacts/magma-preview.png");
            }
            Set(player,"_temperature",.9999f); Set(player,"_coolingGrace",0f);
            typeof(Player).GetMethod("ApplyHotGas").Invoke(player,new object[] {.02f});
            Check(state.GetValue(player).ToString()=="Dead","gas reaching 100 percent heat is immediately fatal with no second timer");
            level.Free(); level=null; await Wait(.2f); GD.Print("MAGMA VENT SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { GetTree().Paused=false; GD.PushError(ex.ToString()); level?.Free(); GetTree().Quit(1); }
    }
}



