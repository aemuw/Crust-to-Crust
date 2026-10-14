using Godot;
using System;
using System.Linq;
using System.Reflection;

public partial class PolishSmoke : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private int _failures;
    private void Check(bool ok,string text) { if(ok) GD.Print("PASS: "+text); else { _failures++; GD.PushError("FAIL: "+text); } }
    private async System.Threading.Tasks.Task Wait(float seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    public override async void _Ready()
    {
        const string save="user://crust_save.cfg";
        bool existed=FileAccess.FileExists(save); byte[] original=existed ? FileAccess.GetFileAsBytes(save) : null;
        LevelGenerator level=null;
        try
        {
            if(DisplayServer.GetName()!="headless") DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
            InputSetup.Ensure();
            Check(InputMap.ActionGetEvents(InputSetup.ToggleLantern).Any(e=>e is InputEventMouseButton mouse && mouse.ButtonIndex==MouseButton.Left),"left mouse button is bound to lantern toggle");
            Check(CharacterCatalog.Count==18,"six additional mineral skins cover rectangle, diamond and hexagon");
            SaveData.Load(); SaveData.TutorialComplete=true;
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level); await Wait(0.1f);
            var player=level.Runner;
            int selected=SaveData.SelectedSkin;
            for(int id=12;id<CharacterCatalog.Count;id++)
            {
                typeof(SaveData).GetProperty("SelectedSkin").SetValue(null,id); player.ApplySkin();
                var sprite=player.GetNode<Sprite2D>("CharacterSprite");
                Vector2 artSize=sprite.Texture.GetSize()*sprite.Scale;
                Check(artSize.X<=60.1f,"new wide shape fits the collision envelope: "+id);
                var shape=player.GetNode<CollisionShape2D>("collision").Shape;
                Vector2 extent=shape is RectangleShape2D rect ? rect.Size/2 : Vector2.Zero;
                if(shape is ConvexPolygonShape2D polygon) foreach(Vector2 p in polygon.Points) extent=new Vector2(Mathf.Max(extent.X,Mathf.Abs(p.X)),Mathf.Max(extent.Y,Mathf.Abs(p.Y)));
                Check(extent.X<=artSize.X/2 && extent.Y<=artSize.Y/2,"physical outline stays within rendered new skin: "+id);
            }
            typeof(SaveData).GetProperty("SelectedSkin").SetValue(null,selected); player.ApplySkin();
            var rock=GD.Load<PackedScene>("res://Scenes/Obstacles/Obstacle.tscn").Instantiate<Obstacle>(); level.AddChild(rock);
            rock.Position=new Vector2(0,500); rock.speed=500; rock.ConfigureVariant(0,0,new HazardMotion(1,100,1,160,0)); rock.TriggerAppear(Vector2.One); rock.SetProcess(false);
            rock._Process(0.016); float before=rock.Position.X; rock._Process(0.016);
            Check(Mathf.Abs(rock.Position.X-before)<0.8f,"heavy obstacle accelerates smoothly rather than jumping to sway path");
            var second=GD.Load<PackedScene>("res://Scenes/Obstacles/Obstacle.tscn").Instantiate<Obstacle>(); level.AddChild(second);
            second.Position=new Vector2(0,500); second.speed=500; second.ConfigureVariant(0,0,new HazardMotion(1,100,1,160,0)); second.TriggerAppear(Vector2.One); second.SetProcess(false);
            rock._Process(0.968); for(int i=0;i<100;i++) second._Process(0.01);
            Check(rock.Position.DistanceTo(second.Position)<3,"hazard motion stays consistent across frame sizes");
            Check(player.HasNode("MasteryTrail"),"mastery creates an actual trail attached to the runner");
            level.GetNode<Hud>("HUD").StartRun();
            for(int i=0;!level.IsGameActive && i<100;i++) await Wait(0.1f);
            player.GetNode<Area2D>("Hitbox").Monitoring=false;
            var lamp=player.GetNode<PointLight2D>("MouseLantern"); bool previous=lamp.Enabled;
            var click=new InputEventMouseButton { ButtonIndex=MouseButton.Left,Pressed=true,Position=new Vector2(640,500) }; Input.ParseInputEvent(click); await Wait(0.05f);
            Check(lamp.Enabled!=previous,"left click toggles actual lantern while playing");
            click.Pressed=false; Input.ParseInputEvent(click); await Wait(0.05f);
            Check(lamp.Enabled!=previous,"mouse release does not toggle lantern again"); click.Dispose();
            var hud=level.GetNode<Hud>("HUD"); typeof(Hud).GetMethod("TogglePause",Hidden).Invoke(hud,null);
            previous=lamp.Enabled;
            using(var pausedClick=new InputEventMouseButton { ButtonIndex=MouseButton.Left,Pressed=true,Position=new Vector2(20,500) }) Input.ParseInputEvent(pausedClick);
            await Wait(0.05f); Check(lamp.Enabled==previous,"pause clicks cannot toggle the lantern"); GetTree().Paused=false;
            level.Free(); level=null; await Wait(0.2f);
        }
        catch(Exception error) { _failures++; GD.PushError(error.ToString()); GetTree().Paused=false; level?.Free(); await Wait(0.2f); }
        finally
        {
            if(existed) { using var file=FileAccess.Open(save,FileAccess.ModeFlags.Write); file.StoreBuffer(original); }
            else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save));
        }
        GD.Print("POLISH SMOKE: "+_failures+" failures"); GetTree().Quit(_failures==0 ? 0 : 1);
    }
}
