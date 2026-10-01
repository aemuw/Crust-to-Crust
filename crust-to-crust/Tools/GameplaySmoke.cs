using Godot;
using System;

public partial class GameplaySmoke : Node
{
    private LevelGenerator _level;
    private async System.Threading.Tasks.Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        GD.Print("PASS: " + message);
    }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/" + name + ".png");
    }
    public override async void _Ready()
    {
        try
        {
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
            await Wait(0.15f);
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts"));
            _level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>();
            AddChild(_level);
            await Wait(0.4f);
            await Capture("start-menu");
            var hud = _level.GetNode<Hud>("HUD");
            hud.GetNode<Button>("Control/StartMenuPanel/StartButton").EmitSignal(Button.SignalName.Pressed);
            await Wait(3f);
            Check(_level.IsGameActive, "intro finishes and falling starts");
            var player = _level.GetNode<Player>("Player");
            player.GetNode<Area2D>("Hitbox").Monitoring = false;
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await Wait(0.2f);
            Check(GetTree().Paused, "Escape pauses gameplay");
            float depth = _level.CurrentDepth;
            await Wait(0.3f);
            Check(Mathf.IsEqualApprox(depth, _level.CurrentDepth), "depth does not advance while paused");
            await Capture("pause-menu");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await Wait(0.2f);
            Check(!GetTree().Paused, "Escape resumes gameplay");
            var hitbox = player.GetNode<Area2D>("Hitbox");
            hitbox.Monitoring = true;
            var coin = GD.Load<PackedScene>("res://Scenes/Obstacles/Coin.tscn").Instantiate<Coin>();
            _level.AddChild(coin);
            coin.Position = player.Position;
            coin.speed = 0f;
            await Wait(0.3f);
            Check(!coin.Visible && !coin.Monitorable, "collected coins disable their collision");
            Check(hud.GetNode<Label>("Control/ScoreLabel").Text == "COINS: 1", "a coin scores exactly once");
            _level.CurrentDepth = _level.CoreDepthPoint - 1000f;
            await Wait(0.15f);
            Check(player.GetNode<CpuParticles2D>("FireParticles").Emitting, "the core ignites the player");
            var water = GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>();
            _level.AddChild(water);
            water.Position = player.Position - water.GetNode<CollisionShape2D>("CollisionShape2D").Position;
            water.speed = 0f;
            await Wait(0.3f);
            Check(!player.GetNode<CpuParticles2D>("FireParticles").Emitting, "water extinguishes the fire");
            water.Visible = false;
            water.Monitorable = false;
            water.ProcessMode = ProcessModeEnum.Disabled;
            await Wait(0.8f);
            Check(!player.GetNode<CpuParticles2D>("FireParticles").Emitting, "water gives protection against immediate reignition");
            hitbox.Monitoring = false;
            _level.CurrentDepth = 0f;
            foreach (int variant in new[] { 0, 1, 2 })
            {
                var obstacle = GD.Load<PackedScene>("res://Scenes/Obstacles/Obstacle.tscn").Instantiate<Obstacle>();
                _level.AddChild(obstacle);
                obstacle.Position = new Vector2(-120 + variant * 120, 40);
                obstacle.ConfigureVariant(variant);
                obstacle.TriggerAppear(Vector2.One);
                obstacle.speed = 0;
            }
            await Wait(0.6f);
            await Capture("hazards");
            foreach (var child in _level.GetChildren())
                if (child is Obstacle obstacle) { obstacle.Visible = false; obstacle.ProcessMode = ProcessModeEnum.Disabled; }
            _level.CurrentDepth = _level.TunnelLength;
            await Wait(1.9f);
            Check(player.Position.Y > 800, "player emerges through the opposite surface");
            await Capture("emergence-exit");
            await Wait(0.9f);
            Check(player.Position.Y > 850, "player lingers at the apex");
            await Capture("emergence-hang");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await Wait(0.1f);
            Vector2 apex = player.Position;
            await Wait(0.3f);
            Check(player.Position.IsEqualApprox(apex), "pause freezes the cinematic tween");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await Wait(0.1f);
            await Wait(0.9f);
            float angle = _level.GetNode<Camera2D>("Camera2D").Rotation;
            Check(angle > 0.1f && angle < Mathf.Pi, "camera rotates gradually after the hang");
            await Capture("emergence-turn");
            await Wait(4.2f);
            Check(_level.IsGameActive, "falling resumes after the emergence sequence");
            Check(Mathf.Abs(player.Position.Y - player.FixedY) < 1f, "player returns to the falling position");
            Check(_level.CurrentDepth < 1000f, "next tunnel cycle resets depth");
            await Capture("next-cycle");
            hitbox.Monitoring = true;
            var fatalRock = GD.Load<PackedScene>("res://Scenes/Obstacles/Obstacle.tscn").Instantiate<Obstacle>();
            _level.AddChild(fatalRock);
            fatalRock.Position = player.Position;
            fatalRock.ConfigureVariant(0);
            fatalRock.speed = 0f;
            await Wait(1.2f);
            Check(GetTree().Paused && hud.GetNode<Control>("Control/GameOverPanel").Visible, "obstacle collision ends the game");
            Check(!_level.IsGameActive && Mathf.IsZeroApprox(_level.FallSpeed), "death stops the world");
            await Capture("game-over");
            GD.Print("GAMEPLAY SMOKE PASSED");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }
}
