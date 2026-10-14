using Godot;
using System;
using System.Reflection;

public partial class WarningSmoke : Node
{
    private async System.Threading.Tasks.Task Wait(float seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            var hud=level.GetNode<Hud>("HUD"); hud.StartRun();
            for(int i=0;!level.IsGameActive && i<100;i++) await Wait(.05f);
            level.SetProcess(false); level.SetPhysicsProcess(false); level.Runner.SetProcess(false); level.Runner.SetPhysicsProcess(false);
            level.Runner.GetNode<Area2D>("Hitbox").Monitoring=false;
            var shooter=new WallShooter(); level.AddChild(shooter); level.FallSpeed=500;
            shooter.Activate(level,1,-1,400); shooter.SetPhysicsProcess(false); shooter._PhysicsProcess(.02);
            var warning=shooter.GetNodeOrNull<Node2D>("HazardWarning"); Check(warning!=null,"turret has a contrast-backed warning overlay");
            var camera=level.GetNode<Camera2D>("Camera2D");
            foreach(float zoom in new[] {.4f,1f,1.5f})
            {
                camera.Zoom=Vector2.One*zoom; camera.Rotation=.3f; camera.IgnoreRotation=false; camera.ForceUpdateScroll();
                shooter._PhysicsProcess(0);
                Transform2D composed=GetViewport().GetCanvasTransform()*warning.GlobalTransform;
                Check(composed.X.DistanceTo(Vector2.Right)<.001f && composed.Y.DistanceTo(Vector2.Down)<.001f,"warning keeps pixel size through camera zoom and rotation");
            }
            camera.Zoom=Vector2.One; camera.Rotation=0; camera.ForceUpdateScroll();
            shooter.Deactivate(); Check(!warning.Visible,"retired turret does not leave a floating warning");
            var rock=GD.Load<PackedScene>("res://Scenes/Obstacles/Obstacle.tscn").Instantiate<Obstacle>(); level.AddChild(rock);
            rock.Position=new Vector2(130,900); rock.speed=1000; rock.ConfigureVariant(4,-100); rock.TriggerAppear(Vector2.One); rock.SetProcess(false);
            rock._Process(.25);
            var dash=rock.GetNodeOrNull<Node2D>("HazardWarning"); Check(dash!=null && dash.Visible==rock.WarningActive,"dash badge follows actual warning phase");
            SaveData.FlashIntensity=0; rock._Process(.02);
            Check(dash.Modulate.A>.95f,"reduced flashes keeps a stable visible cue");
            rock.RetireForClearance(); Check(!dash.Visible,"cleared rock cannot leave an active warning");
            if(DisplayServer.GetName()!="headless")
            {
                shooter.Activate(level,1,-1,190); shooter.SetPhysicsProcess(false); shooter._PhysicsProcess(.3);
                var right=new WallShooter(); level.AddChild(right); right.Activate(level,0,1,1500); right.SetPhysicsProcess(false); right._PhysicsProcess(.1);
                rock.Position=new Vector2(90,50); rock.speed=200; rock.ConfigureVariant(4,-140); rock.Show(); rock.TriggerAppear(Vector2.One); rock._Process(.35);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var image=GetViewport().GetTexture().GetImage(); image.SavePng("res://artifacts/warning-preview.png");
            }
            level.Free(); level=null; await Wait(.2f); GD.Print("WARNING SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { GetTree().Paused=false; GD.PushError(ex.ToString()); level?.Free(); GetTree().Quit(1); }
    }
}
