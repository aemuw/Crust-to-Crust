using Godot;
using System;

// Diagnostic sample of actual pickups along an assisted safe route, not human earnings.
public partial class EconomySmoke : Node
{
    private LevelGenerator _level;
    public override void _Process(double delta)
    {
        if (_level?.IsGameActive != true) return;
        Player player = _level.Runner;
        player.Position = new Vector2(_level.SafeLaneAt(player.FixedY),player.FixedY);
    }
    private async System.Threading.Tasks.Task Wait(float time) => await ToSignal(GetTree().CreateTimer(time),SceneTreeTimer.SignalName.Timeout);
    public override async void _Ready()
    {
        try
        {
            SaveData.Load(); SaveData.TutorialComplete = true;
            foreach (int seed in new[] {17,42,12345})
            {
                GetTree().Paused = false;
                LevelGenerator.RequestedSeed = seed;
                _level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>();
                AddChild(_level); int wallet = SaveData.Wallet;
                _level.GetNode<Hud>("HUD").StartRun(); Engine.TimeScale = 4f;
                for (int step = 0; _level.ActivePlaySeconds < 60f && !GetTree().Paused && step < 900; step++) await Wait(0.1f);
                float elapsed = _level.ActivePlaySeconds;
                int coins = SaveData.Wallet-wallet;
                GD.Print($"ECONOMY seed={seed} active={elapsed:0.0}s coins={coins} per_minute={(elapsed>0 ? coins*60f/elapsed : 0):0.0} died={GetTree().Paused}");
                if (elapsed <= 0f) throw new Exception("No active gameplay sampled");
                for (int step=0; (_level.Lap < 2 || !_level.IsGameActive) && !GetTree().Paused && step<900; step++) await Wait(0.1f);
                if (_level.Lap < 2 || !_level.IsGameActive || GetTree().Paused) throw new Exception("Assisted route failed full core/exit/next-lap traversal");
                GD.Print($"TRAVERSAL seed={seed} lap={_level.Lap} distance={_level.TotalDistance:0} active={_level.IsGameActive}");
                Engine.TimeScale = 1f; GetTree().Paused = false; _level.Free(); _level = null;
            }
            GD.Print("ECONOMY SMOKE PASSED"); GetTree().Quit();
        }
        catch (Exception error) { Engine.TimeScale = 1f; GetTree().Paused = false; GD.PushError(error.ToString()); _level?.Free(); GetTree().Quit(1); }
    }
}
