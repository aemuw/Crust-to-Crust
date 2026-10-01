using Godot;
using System;

public partial class CaveSmoke : Node
{
    private async System.Threading.Tasks.Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private void Check(bool ok, string text) { if (!ok) throw new Exception(text); GD.Print("PASS: " + text); }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/" + name + ".png");
    }
    public override async void _Ready()
    {
        try
        {
            // Render without taking desktop focus; injected input still works.
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
            await Wait(0.15f);
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts"));
            var level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>();
            AddChild(level);
            var hud = level.GetNode<Hud>("HUD");
            hud.GetNode<Button>("Control/StartMenuPanel/StartButton").EmitSignal(Button.SignalName.Pressed);
            await Wait(2.8f);
            level.StopFalling();
            var player = level.GetNode<Player>("Player");
            var hitbox = player.GetNode<Area2D>("Hitbox");
            hitbox.Monitoring = false;
            var variants = new Obstacle[3];
            var starts = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                variants[i] = GD.Load<PackedScene>("res://Scenes/Obstacles/Obstacle.tscn").Instantiate<Obstacle>();
                level.AddChild(variants[i]);
                starts[i] = new Vector2(-120 + i * 120, 120);
                variants[i].Position = starts[i];
                variants[i].ConfigureVariant(i);
                variants[i].TriggerAppear(Vector2.One);
                variants[i].speed = 120f;
            }
            float startingRotation = variants[0].Rotation;
            await Wait(1f);
            Check(Mathf.Abs(variants[0].Rotation - startingRotation) > 0.15f, "boulder rolls while scrolling");
            Check(Mathf.Abs(variants[1].Rotation) > 0.2f, "obsidian shard sways");
            Check(Mathf.IsEqualApprox(variants[2].Position.X, starts[2].X), "crystal stays fixed horizontally");
            Check(variants[2].ZIndex < 0, "crystal is rendered behind the gameplay objects");
            Check(Mathf.Abs(variants[0].Position.X - starts[0].X) > 60f, "boulder sweeps across the tunnel");
            Check(Mathf.Abs(variants[1].Position.X - starts[1].X) > 90f, "shard has a wide lateral sweep");
            Check(variants[0].Position.Y > variants[1].Position.Y + 15f, "hazards have different relative speeds");
            foreach (var obstacle in variants) obstacle.speed = 0;
            await Capture("cave-layers-and-hazards");
            foreach (var child in level.GetChildren())
                if (child is Obstacle obstacle) { obstacle.Visible = false; obstacle.Monitorable = false; obstacle.ProcessMode = ProcessModeEnum.Disabled; }
            var water = GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>();
            level.AddChild(water);
            water.speed = 0f;
            water.Position = new Vector2(0, player.FixedY + 20);
            var segments = level.GetNode<Node2D>("TunnelSegments");
            foreach (bool left in new[] { true, false })
            {
                hitbox.Monitoring = false;
                for (int attempts = 0; water.FromLeft != left && attempts < 100; attempts++) water.GenerateRandomCaveGeometry();
                Check(water.FromLeft == left, "waterfall selects the requested side for the test");
                int segmentIndex = 0;
                foreach (Node child in segments.GetChildren())
                    if (child is Node2D segment) segment.Position = new Vector2(0, player.FixedY - (left ? 278f : 505f) + (segmentIndex++ - 2) * 720);
                player.Position = new Vector2(0, player.FixedY);
                level.CurrentDepth = level.CoreDepthPoint - 1000;
                await Wait(0.2f);
                hitbox.Monitoring = true;
                Input.ParseInputEvent(new InputEventKey { Keycode = left ? Key.A : Key.D, Pressed = true });
                await Wait(0.8f);
                Input.ParseInputEvent(new InputEventKey { Keycode = left ? Key.A : Key.D, Pressed = false });
                await Wait(0.2f);
                Check(player.GetNode<Sprite2D>("CharacterSprite").Visible && !GetTree().Paused,
                    (left ? "left" : "right") + $" waterfall can be entered with a lethal wall directly behind it (position={player.Position}, alive={player.GetNode<Sprite2D>("CharacterSprite").Visible}, paused={GetTree().Paused})");
                Check(Mathf.Abs(player.Position.X) <= 151f && Mathf.Abs(player.Position.X) >= 100f, "water approach keeps the player clear of wall protrusions");
                Check(!player.GetNode<CpuParticles2D>("FireParticles").Emitting, "entering the waterfall extinguishes the player");
                await Wait(7.3f);
                Check(!player.GetNode<CpuParticles2D>("FireParticles").Emitting, "the player stays cool while remaining in water");
                level.GetNode<Camera2D>("Camera2D").Position = new Vector2(0, water.Position.Y);
                await Capture(left ? "waterfall-left-safe-entry" : "waterfall-right-safe-entry");
            }
            water.Visible = false;
            water.Monitorable = false;
            water.ProcessMode = ProcessModeEnum.Disabled;
            player.Position = new Vector2(220, player.FixedY);
            await Wait(1.2f);
            Check(GetTree().Paused && hud.GetNode<Control>("Control/GameOverPanel").Visible,
                $"ordinary wall collisions remain lethal outside the waterfall approach (position={player.Position}, scale={player.Scale}, alive={player.GetNode<Sprite2D>("CharacterSprite").Visible}, paused={GetTree().Paused}, overlaps={hitbox.GetOverlappingAreas().Count})");
            GD.Print("CAVE SMOKE PASSED");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }
}
