using Godot;
using System;
using System.Reflection;

/// <summary>Real cinematic returns at accelerated time; collision disabled to isolate pacing.</summary>
public partial class LoopSmoke : Node
{
    [Export] public bool CompareDivisors;
    private async System.Threading.Tasks.Task RealWait(float seconds)=>await ToSignal(GetTree().CreateTimer(seconds,true,false,true),SceneTreeTimer.SignalName.Timeout);
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            if(DisplayServer.GetName()!="headless") DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
            SaveData.Load(); SaveData.TutorialComplete=true;
            Engine.TimeScale=12;
            var cases=CompareDivisors ? new[] {(17,2f),(17,2.4f),(17,3f),(17,4f)} : new[] {(17,2.4f),(42,2.4f),(12345,2.4f)};
            foreach(var fixture in cases)
            {
                int seed=fixture.Item1;
                LevelGenerator.RequestedSeed=seed;
                level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>();
                level.ReturnSpeedDivisor=fixture.Item2; AddChild(level);
                var player=level.Runner;
                player.GetNode<Area2D>("Hitbox").Monitoring=false;
                typeof(Player).GetField("_coolingGrace",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,100000f);
                level.GetNode<Hud>("HUD").StartRun();
                int lap=1; float start=level.TraversalStartSpeed, previousSpeed=0, previousPeak=start;
                ulong deadline=Time.GetTicksMsec()+90000;
                while(level.Lap<4 && Time.GetTicksMsec()<deadline)
                {
                    await RealWait(0.035f);
                    // This fixture measures pacing, so isolate all lethal contacts
                    // including projectiles and reapply heat protection after emergence.
                    typeof(Player).GetField("_coolingGrace",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,100000f);
                    typeof(Player).GetField("_invulnerableTimer",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,100000f);
                    if(GetTree().Paused) throw new Exception("unexpected paused/dead run on seed "+seed);
                    if(level.Lap!=lap)
                    {
                        if(level.TraversalStartSpeed<start+59.99f || level.TraversalStartSpeed+0.1f<previousPeak/level.ReturnSpeedDivisor)
                            throw new Exception("lost momentum at return seed "+seed);
                        GD.Print($"LOOP seed={seed} divisor={fixture.Item2:0.0} lap={level.Lap} previousPeak>={previousPeak:0.0} start={level.TraversalStartSpeed:0.0}");
                        start=level.TraversalStartSpeed; previousSpeed=0; lap=level.Lap;
                    }
                    previousPeak=level.TraversalPeakSpeed;
                    if(level.IsGameActive)
                    {
                        if(level.FallSpeed+0.01f<previousSpeed) throw new Exception("speed decreased in active traversal");
                        previousSpeed=level.FallSpeed;
                    }
                }
                if(level.Lap<4) throw new Exception("three returns timed out on seed "+seed);
                level.Free(); level=null; await RealWait(0.15f);
            }
            GD.Print(CompareDivisors ? "PACING SMOKE PASSED: divisors2/2.4/3/4" : "LOOP SMOKE PASSED: 3 returns on seeds 17, 42, 12345");
            Engine.TimeScale=1; GetTree().Quit();
        }
        catch(Exception ex)
        {
            GD.PushError(ex.ToString()); Engine.TimeScale=1; GetTree().Paused=false;
            level?.Free(); await RealWait(0.15f); GetTree().Quit(1);
        }
    }
}
