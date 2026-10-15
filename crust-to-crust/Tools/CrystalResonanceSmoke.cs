using Godot;
using System;
using System.Reflection;

public partial class CrystalResonanceSmoke : Node
{
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    private async System.Threading.Tasks.Task Wait(float t)=>await ToSignal(GetTree().CreateTimer(t),SceneTreeTimer.SignalName.Timeout);
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            SaveData.Load(); SaveData.TutorialComplete=true;
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            level.SetRunSeed(17);
            level.SetProcess(false); level.SetPhysicsProcess(false); level.Runner.SetProcess(false); level.Runner.SetPhysicsProcess(false);
            level.Runner.Position=new Vector2(0,-200); level.Runner.GetNode<Area2D>("Hitbox").Monitoring=false;
            level.IsGameActive=true; level.CurrentDepth=9000; level.FallSpeed=500;
            var start=typeof(LevelGenerator).GetMethod("TryStartCrystalResonance"); Check(start!=null,"crystal has a local resonant seam");
            var collapse=level.GetNode<Node2D>("CrystalResonance"); collapse.SetProcess(false);
            bool started=false;
            foreach(int side in new[] {-1,1}) if((bool)start.Invoke(level,new object[] {side,1150f})) { started=true; break; }
            Check(started,"a safe crystal wall begins resonating");
            var update=collapse.GetType().GetMethod("_Process");
            update.Invoke(collapse,new object[] {.8});
            var ring=collapse.GetNode<AudioStreamPlayer2D>("CrystalRing");
            Check(ring.Stream!=null && ring.Playing && ring.Bus==AudioManager.SfxBus,"positional crystal warning actually plays on the SFX bus");
            Check((int)collapse.GetType().GetProperty("FragmentsReleased").GetValue(collapse)==0,"ring and shimmer precede the dangerous fragment");
            update.Invoke(collapse,new object[] {1.0});
            Check((int)collapse.GetType().GetProperty("FragmentsReleased").GetValue(collapse)==1,"one bounded protected fragment is released after warning");
            Check(!level.ProjectilesAllowed,"turret volleys cannot overlap the local collapse");
            collapse.SetProcess(true); GetTree().Paused=true; float before=collapse.Position.Y; await Wait(.1f); Check(collapse.Position.Y==before,"pause freezes wall encounter"); GetTree().Paused=false; collapse.SetProcess(false);
            level.StopFalling(); Check(!collapse.Visible && !ring.Playing,"death or surface exit clears resonance and stops sound");
            level.IsGameActive=true; level.CurrentDepth=level.CoreDepthPoint;
            Check(!(bool)start.Invoke(level,new object[] {1,1150f}),"crystal collapse never starts in the core");
            level.CurrentDepth=9000;
            var vent=GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>(); level.AddChild(vent);
            ((System.Collections.Generic.List<Node2D>)typeof(LevelGenerator).GetField("waterPool",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(level)).Add(vent);
            vent.Position=new Vector2(0,1150); vent.speed=0; vent.SetProcess(false);
            Check(!(bool)start.Invoke(level,new object[] {1,1150f}),"cooling approach suppresses the collapse"); vent.Hide(); vent.Monitorable=false;
            typeof(LevelGenerator).GetProperty("Encounter").SetValue(level,EncounterKind.Rest);
            typeof(LevelGenerator).GetMethod("SpawnTimers",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(level,new object[] {9.1f});
            Check(collapse.Visible,"normal gameplay scheduling starts a crystal event without manual activation"); level.StopFalling(); level.IsGameActive=true;
            level.CurrentDepth=28050; level.FallSpeed=1200;
            started=false;
            foreach(int side in new[] {-1,1}) if((bool)start.Invoke(level,new object[] {side,2500f})) { started=true; break; }
            Check(started,"resonance can be reused at high speed on the return side of the planet");
            update.Invoke(collapse,new object[] {.8});
            Check((int)collapse.GetType().GetProperty("FragmentsReleased").GetValue(collapse)==0,"high speed still preserves the full warning");
            update.Invoke(collapse,new object[] {.6});
            Check((int)collapse.GetType().GetProperty("FragmentsReleased").GetValue(collapse)==1,"high speed releases only one safe fragment");
            update.Invoke(collapse,new object[] {.4});
            Check((int)collapse.GetType().GetProperty("FragmentsReleased").GetValue(collapse)==1,"one source never duplicates its fragment");
            level.StopFalling(); level.IsGameActive=true;
            level.CurrentDepth=9000; level.FallSpeed=500;
            foreach(int side in new[] {-1,1}) if((bool)start.Invoke(level,new object[] {side,1150f})) break;
            vent.Position=new Vector2(0,600); vent.Show();
            update.Invoke(collapse,new object[] {1.8});
            Check((int)collapse.GetType().GetProperty("FragmentsReleased").GetValue(collapse)==0,"new cooling access cancels a pending fragment");
            vent.Hide(); level.StopFalling(); level.IsGameActive=true;
            if(DisplayServer.GetName()!="headless")
            {
                level.CurrentDepth=9000; level.FallSpeed=200;
                foreach(int side in new[] {-1,1}) if((bool)start.Invoke(level,new object[] {side,240f})) break;
                collapse.SetProcess(false); update.Invoke(collapse,new object[] {.5});
                level.GetNode<Hud>("HUD").GetNode<Control>("Control/StartMenuPanel").Hide();
                var camera=level.GetNode<Camera2D>("Camera2D"); camera.Position=Vector2.Zero; camera.ForceUpdateScroll();
                await Wait(.05f);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var im=GetViewport().GetTexture().GetImage(); im.SavePng("res://artifacts/crystal-preview.png");
            }
            level.Free(); level=null; await Wait(.2f); GD.Print("CRYSTAL RESONANCE SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { GetTree().Paused=false; GD.PushError(ex.ToString()); level?.Free(); GetTree().Quit(1); }
    }
}

