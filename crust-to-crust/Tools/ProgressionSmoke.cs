using Godot;
using System;

public partial class ProgressionSmoke : Node
{
    private async System.Threading.Tasks.Task Wait(float seconds, bool real = false) =>
        await ToSignal(GetTree().CreateTimer(seconds, true, false, real), SceneTreeTimer.SignalName.Timeout);
    private void Check(bool ok, string text) { if (!ok) throw new Exception(text); GD.Print("PASS: " + text); }
    private bool AnyCrystalVisible()
    {
        foreach (Node node in GetTree().GetNodesInGroup("TunnelCrystal"))
            if (node is CanvasItem item && item.IsVisibleInTree()) return true;
        return false;
    }
    private bool AnyPooledHazardVisible(LevelGenerator level)
    {
        foreach (Node child in level.GetChildren())
            if (child is Obstacle or Water or Coin && ((Node2D)child).Visible) return true;
        return false;
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
            var level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>();
            AddChild(level);
            var player = level.GetNode<Player>("Player");
            var hud = level.GetNode<Hud>("HUD");
            Check(!AnyCrystalVisible() && !AnyPooledHazardVisible(level), "menu has no visible crystals or pooled obstacles");
            hud.GetNode<Button>("Control/StartMenuPanel/StartButton").EmitSignal(Button.SignalName.Pressed);
            await Wait(0.9f);
            Check(!AnyCrystalVisible() && !AnyPooledHazardVisible(level), "initial cinematic has no crystals or spawned hazards");
            Check(Mathf.IsZeroApprox(level.ActivePlaySeconds), "intro does not increase difficulty time");
            await Capture("intro-without-hazards");
            for (int step = 0; !level.IsGameActive && step < 60; step++) await Wait(0.1f);
            Check(AnyCrystalVisible(), "crystals become visible after the intro");
            foreach (Node node in GetTree().GetNodesInGroup("TunnelCrystal"))
            {
                var crystal = (Sprite2D)node;
                Check(crystal.ZIndex < 0 && crystal.Scale.X >= 0.14f, "large crystal is placed on the background layer");
                Check(crystal.GetChildCount() > 0 && crystal.GetChild(0) is Area2D, "background crystal has an obstacle collider");
            }
            float initialInterval = level.CurrentObstacleInterval;
            player.GetNode<Area2D>("Hitbox").Monitoring = false;
            player.ProcessMode = ProcessModeEnum.Disabled;
            level.TunnelLength = 4000000;
            level.CoreDepthPoint = 2000000;
            Engine.TimeScale = 20f;
            for (int step = 0; level.ActivePlaySeconds < 65f && step < 200; step++) await Wait(0.1f, true);
            Check(level.Difficulty > 0.3f && level.CurrentObstacleInterval < initialInterval - 0.3f,
                $"difficulty and obstacle frequency increase after a minute of active play (active={level.ActivePlaySeconds}, difficulty={level.Difficulty}, interval={level.CurrentObstacleInterval}, paused={GetTree().Paused}, playing={level.IsGameActive})");
            float activeSeconds = level.ActivePlaySeconds;
            GetTree().Paused = true;
            await Wait(0.25f, true);
            Check(Mathf.IsEqualApprox(activeSeconds, level.ActivePlaySeconds), "pause does not increase difficulty");
            GetTree().Paused = false;
            for (int step = 0; level.ActivePlaySeconds < 182f && step < 300; step++) await Wait(0.1f, true);
            Engine.TimeScale = 1f;
            Check(Mathf.IsEqualApprox(level.Difficulty, 1f), "difficulty reaches a bounded maximum after three minutes");
            Check(Mathf.IsEqualApprox(level.CurrentObstacleInterval, 1.25f), "spawn interval stays bounded at maximum difficulty");
            await Capture("late-game-background-crystals");
            player.ProcessMode = ProcessModeEnum.Inherit;
            level.CurrentDepth = level.TunnelLength;
            await Wait(0.1f);
            float beforeCinematic = level.ActivePlaySeconds;
            Check(!AnyCrystalVisible() && !AnyPooledHazardVisible(level), "end cinematic hides crystals and every pooled hazard");
            await Wait(3f);
            Check(Mathf.IsEqualApprox(beforeCinematic, level.ActivePlaySeconds), "end cinematic does not increase difficulty");
            Check(!AnyCrystalVisible() && !AnyPooledHazardVisible(level), "no hazards appear on the opposite surface");
            await Capture("emergence-without-hazards");
            await Wait(2.7f);
            Check(!AnyCrystalVisible() && !AnyPooledHazardVisible(level), "return part of cinematic also keeps crystals hidden");
            await Wait(1.7f);
            Check(level.IsGameActive && AnyCrystalVisible(), "crystals return only after falling resumes");
            Check(level.Difficulty > 0.99f, "difficulty is preserved between planet traversals");
            player.GetNode<Area2D>("Hitbox").Monitoring = true;
            var testCrystal = (Sprite2D)GetTree().GetNodesInGroup("TunnelCrystal")[0];
            testCrystal.GlobalPosition = player.GlobalPosition;
            await Wait(1.2f);
            Check(GetTree().Paused && hud.GetNode<Control>("Control/GameOverPanel").Visible,
                "static background crystal still kills the player on contact");
            GD.Print("PROGRESSION SMOKE PASSED");
            GetTree().Quit();
        }
        catch (Exception error) { Engine.TimeScale = 1f; GD.PushError(error.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }
}



