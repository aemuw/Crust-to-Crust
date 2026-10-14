using Godot;
using System;
using System.Reflection;
using System.Linq;

public partial class VisibilitySmoke : Node
{
    private int _failures;
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private void Check(bool ok,string text) { if(ok) GD.Print("PASS: "+text); else { _failures++; GD.PushError("FAIL: "+text); } }
    private async System.Threading.Tasks.Task Wait(float seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private Obstacle Rock(LevelGenerator level,Vector2 position,int variant)
    {
        var rock=GD.Load<PackedScene>("res://Scenes/Obstacles/Obstacle.tscn").Instantiate<Obstacle>(); level.AddChild(rock);
        rock.Position=position; rock.ConfigureVariant(variant); rock.TriggerAppear(Vector2.One); rock.speed=0; rock.Show(); rock.SetProcess(false); return rock;
    }
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            if(DisplayServer.GetName()!="headless") DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level); await Wait(0.1f);
            level.SetProcess(false); level.SetPhysicsProcess(false); level.Runner.SetProcess(false); level.Runner.SetPhysicsProcess(false);
            level.Runner.Position=new Vector2(0,-200);
            var camera=level.GetNode<Camera2D>("Camera2D"); camera.Position=Vector2.Zero; camera.Zoom=Vector2.One; camera.ForceUpdateScroll(); await Wait(0.05f);
            level.SetRunSeed(17);
            var phase=typeof(LevelGenerator).GetField("_routeDistance",Hidden);
            for(float d=0;d<40000;d+=5) if(Mathf.Abs(level.Director.LaneAt(d+240))<3) { phase.SetValue(level,d); break; }
            var constrained=Rock(level,new Vector2(160,240),3);
            Check(constrained.ProtectPassage(1,1),"fixture begins in a valid passage"); constrained._Process(0.4);
            float best=-1000,bestPhase=0;
            for(float d=0;d<40000;d+=5) if(level.Director.LaneAt(d+240)>best) { best=level.Director.LaneAt(d+240); bestPhase=d; }
            phase.SetValue(level,bestPhase);
            constrained._Process(0.016);
            Check(constrained.Visible,"passage changes do not instantly hide a visible rock");
            camera.Position=Vector2.Zero; camera.Zoom=Vector2.One*0.4f; camera.ForceUpdateScroll(); await Wait(0.05f);
            var high=Rock(level,new Vector2(0,-600),0); high._Process(0.4);
            Check(high.Visible,"rock inside zoomed-out viewport is not culled at fixed world Y");
            var near=Rock(level,new Vector2(100,-100),0); near._Process(0.4);
            var water=GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>(); level.AddChild(water);
            water.Position=new Vector2(0,200); water.Show(); water.Monitorable=true; water.SetProcess(false);
            // Use the real pool branch which formerly hid hazards immediately.
            var pool=(System.Collections.Generic.List<Node2D>)typeof(LevelGenerator).GetField("obstaclePool",Hidden).GetValue(level); pool.Add(near);
            level.FallSpeed=500; level.SetTunnelHazardsVisible(true);
            typeof(LevelGenerator).GetMethod("UpdateCrystalWaterClearance",Hidden).Invoke(level,new object[] {0.016f});
            Check(near.Visible,"water approach does not instantly hide a visible rock");
            var crystal=(Sprite2D)GetTree().GetNodesInGroup("TunnelCrystal")[0];
            crystal.Show(); crystal.Modulate=Colors.White;
            var alphas=(float[])typeof(LevelGenerator).GetField("_crystalZoneAlpha",Hidden).GetValue(level); alphas[0]=1;
            water.GenerateRandomCaveGeometry(crystal.GlobalPosition.X<0);
            water.Position=new Vector2(0,crystal.GlobalPosition.Y); water.Monitorable=true;
            typeof(LevelGenerator).GetMethod("UpdateCrystalWaterClearance",Hidden).Invoke(level,new object[] {0.016f});
            Check(crystal.Visible && crystal.Modulate.A>0.9f,"covered crystal fades rather than instantly disappearing");
            var coreRock=Rock(level,new Vector2(100,-100),0); coreRock._Process(0.4); pool.Add(coreRock);
            level.CurrentDepth=level.CoreDepthPoint-1; level.IsGameActive=true; level._Process(0.016);
            Check(coreRock.Visible,"core crossing does not instantly erase visible rocks");
            var pieces=coreRock.GetChildren().OfType<Sprite2D>().Where(p=>p.RegionEnabled && p.Visible).ToArray();
            Check(pieces.Length==4 && pieces.All(p=>p.Texture!=null && p.RegionRect.Size.X>0),"retirement displays four real texture fragments");
            coreRock._Process(0.325);
            Check(pieces.All(p=>p.Modulate.A>0.4f && p.Modulate.A<0.6f),"fragments fade progressively");
            coreRock.Position=new Vector2(0,-4000); coreRock._Process(0.4);
            Check(!coreRock.Visible && coreRock.ProcessMode==ProcessModeEnum.Disabled,"retired obstacle releases its pool slot offscreen");
            level.FallSpeed=0;
            var shot=new HazardProjectile(); level.AddChild(shot); shot.Activate(new Vector2(0,-600),Vector2.Zero,2,level); shot.SetPhysicsProcess(false);
            shot._PhysicsProcess(0.016);
            Check(shot.Visible,"shard inside zoomed-out viewport is not culled at fixed world Y");
            shot._PhysicsProcess(1.95);
            Check(shot.Visible && shot.Modulate.A>0 && shot.Modulate.A<1,"shard fades before lifetime expiry");
            shot._PhysicsProcess(0.2);
            Check(!shot.Visible,"expired shard returns to its pool");
            level.Free(); level=null; await Wait(0.15f);
        }
        catch(Exception ex) { _failures++; GD.PushError(ex.ToString()); level?.Free(); await Wait(0.15f); }
        GD.Print("VISIBILITY SMOKE: "+_failures+" failures"); GetTree().Quit(_failures==0 ? 0 : 1);
    }
}
