using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class DrillShaftSmoke : Node
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
            level.SetRunSeed(18); level.SetProcess(false); level.SetPhysicsProcess(false);
            var player=level.Runner; player.SetProcess(false); player.SetPhysicsProcess(false); player.GetNode<Area2D>("Hitbox").Monitoring=false;
            var state=typeof(Player).GetField("_state",Hidden);
            var start=typeof(LevelGenerator).GetMethod("TryStartDrillShaft"); Check(start!=null,"drilling shaft has a coordinated entry point");
            var shaft=level.GetNode<Node2D>("DrillShaftEvent"); shaft.SetProcess(false);
            var update=shaft.GetType().GetMethod("_Process");
            object Property(string name)=>shaft.GetType().GetProperty(name).GetValue(shaft);
            bool Begin()=>(bool)start.Invoke(level,null);
            void Reset(float speed=500)
            {
                level.StopFalling(); level.IsGameActive=true; level.CurrentDepth=1000; level.FallSpeed=speed;
                state.SetValue(player,Enum.Parse(state.FieldType,"Playing")); Set(player,"_temperature",0f); Set(level,"_coreRespite",0f);
                Set(level,"waterTimer",0f); Set(level,"nextWaterInterval",1000f);
                Set(level,"_basaltTimer",-100f); Set(level,"_crystalTimer",-100f); Set(level,"_collapseEventTimer",0f);
                Set(level,"_encounterEndDistance",float.MaxValue); typeof(LevelGenerator).GetProperty("Encounter").SetValue(level,EncounterKind.Rest);
                player.Position=new Vector2(level.SafeLaneAt(-200),-200); Set(player,"_hasPreviousContact",false);
            }
            void Tick(float time)
            {
                while(time>0)
                {
                    float dt=Mathf.Min(.01f,time); time-=dt;
                    level._Process(dt); player.Position=new Vector2(level.SafeLaneAt(-200),-200); Set(player,"_hasPreviousContact",false);
                    update.Invoke(shaft,new object[] {(double)dt});
                }
            }
            Reset(); Check(Begin(),"a cold clear stretch can contain the abandoned shaft");
            Check((int)Property("ArmedGates")==0,"preparation shows machinery without early active collisions");
            Tick(.9f); Check((int)Property("ArmedGates")==0,"mechanism warning lasts the full preparation time");
            var motor=shaft.GetNode<AudioStreamPlayer2D>("ShaftMotor");
            Check(motor.Playing && motor.Stream is AudioStreamWav wav && wav.LoopMode!=AudioStreamWav.LoopModeEnum.Disabled,"working machinery has a dedicated looping motor sound");
            Tick(.44f);
            var firstGate=shaft.GetNode<Node2D>("Gate0");
            var heads=(CollisionShape2D[])firstGate.GetType().GetField("_heads",Hidden).GetValue(firstGate);
            float beforeArm=heads[1].GlobalPosition.X; Tick(.02f);
            Check(Math.Abs(heads[1].GlobalPosition.X-beforeArm)<6,"arming does not teleport a drill head sideways");
            Tick(.14f); Check((int)Property("ArmedGates")==1,"first machinery gate arms only after warning");
            Check(!level.ProjectilesAllowed && !level.TryStartCollapseEvent(),"shaft excludes turret volleys and the collapse event");
            Tick(2.14f);
            var lastGate=shaft.GetNode<Node2D>("Gate2");
            var lastHeads=(CollisionShape2D[])lastGate.GetType().GetField("_heads",Hidden).GetValue(lastGate);
            float leftBefore=lastHeads[0].GlobalPosition.X,rightBefore=lastHeads[1].GlobalPosition.X; Tick(.02f);
            Check(Math.Max(Math.Abs(lastHeads[0].GlobalPosition.X-leftBefore),Math.Abs(lastHeads[1].GlobalPosition.X-rightBefore))<6,"later mechanisms also engage without lateral jumps");
            Tick(.14f); Check((int)Property("ArmedGates")==3,"three successive working gates form one shaft section");
            Check(shaft.GetChildCount()==7,"gate count is fixed instead of growing during a run");
            Tick(2.8f); Check(state.GetValue(player).ToString()=="Playing","real mechanism motion preserves the reachable route");
            Check(Property("Phase").ToString()=="Recovery" && !level.ProjectilesAllowed,"shaft provides a protected recovery after passage");
            Tick(1.7f); Check(!shaft.Visible && level.ProjectilesAllowed && !motor.Playing,"normal encounters resume after the machinery fades out");
            Reset(); Check(Begin(),"pause fixture starts"); Tick(.5f); shaft.SetProcess(true); GetTree().Paused=true;
            float elapsed=(float)Property("Elapsed"); await Wait(.1f);
            Check((float)Property("Elapsed")==elapsed,"pause freezes machinery and its sequence"); GetTree().Paused=false; shaft.SetProcess(false);
            level.StopFalling(); Check(!shaft.Visible && !motor.Playing,"stop clears every gate and the motor");

            var shape=(CollisionShape2D)typeof(Player).GetField("_contactShape",Hidden).GetValue(player); var original=shape.Shape;
            foreach(int id in new[] {0,6,9,12,14,16})
            {
                Reset(); shape.Shape=CharacterCatalog.Get(id).CreateBodyShape(); Check(Begin(),"shape fixture starts"); Tick(6);
                Check(state.GetValue(player).ToString()=="Playing","shaft leaves clearance for "+CharacterCatalog.Get(id).Form);
            }
            shape.Shape=original;
            Reset(1200); Check(Begin(),"a long enough stretch supports high-speed machinery"); Tick(6);
            Check((int)Property("ArmedGates")==3 && state.GetValue(player).ToString()=="Playing","high speed preserves warnings and a safe route");
            Reset(); Check(Begin(),"cooling fixture starts"); Tick(1.6f);
            var cold=GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>(); level.AddChild(cold);
            ((List<Node2D>)typeof(LevelGenerator).GetField("waterPool",Hidden).GetValue(level)).Add(cold);
            cold.Position=new Vector2(0,-200); cold.speed=0; cold.SetProcess(false); Tick(.1f);
            Check(Property("Phase").ToString()=="Recovery" && (int)Property("ActiveColliders")==0,"new cooling access immediately disables machinery hazards");
            Reset(); Check(!Begin(),"cooling approach prevents a new shaft section"); cold.Hide(); cold.Monitorable=false;
            level.CurrentDepth=20000; Check(!Begin(),"shaft does not overlap the core");
            Reset(); Set(player,"_temperature",.6f); Check(!Begin(),"hot player is not committed to a long machinery section");
            Reset(5000); Check(!Begin(),"insufficient safe distance suppresses an unfair high-speed section");
            Reset(); Set(level,"_collapseEventTimer",12f);
            typeof(LevelGenerator).GetMethod("SpawnTimers",Hidden).Invoke(level,new object[] {.01f});
            Check(shaft.Visible,"shared major-event scheduler selects the shaft from the run seed");
            if(DisplayServer.GetName()!="headless")
            {
                Reset(200); Check(Begin(),"visual fixture starts"); Tick(2);
                level.GetNode<Hud>("HUD").GetNode<Control>("Control/StartMenuPanel").Hide();
                var camera=level.GetNode<Camera2D>("Camera2D"); camera.Position=Vector2.Zero; camera.ForceUpdateScroll();
                await Wait(.05f); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var im=GetViewport().GetTexture().GetImage(); im.SavePng("res://artifacts/drill-shaft-preview.png");
            }
            Reset(); Check(Begin(),"swept collision fixture starts"); Tick(1.5f);
            var gate=shaft.GetNode<Node2D>("Gate0");
            gate.Position=new Vector2(0,player.FixedY+120);
            gate.GetType().GetMethod("Step").Invoke(gate,new object[] {0f});
            Vector2 head=(Vector2)gate.GetType().GetProperty("LeftHeadPosition").GetValue(gate);
            player.Position=new Vector2(head.X,-200); Set(player,"_hasPreviousContact",false);
            level.FallSpeed=5000;
            gate.GetType().GetMethod("Step").Invoke(gate,new object[] {.05f});
            Check(state.GetValue(player).ToString()=="Dead","swept head collision cannot tunnel through the player at high speed");
            level.Free(); level=null; await Wait(.2f); GD.Print("DRILL SHAFT SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { GetTree().Paused=false; GD.PushError(ex.ToString()); level?.Free(); GetTree().Quit(1); }
    }
}
