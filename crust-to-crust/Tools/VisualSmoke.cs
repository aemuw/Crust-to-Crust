using Godot;
using System;

public partial class VisualSmoke : Node
{
    private async System.Threading.Tasks.Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Check(bool condition,string message) { if (!condition) throw new Exception(message); GD.Print("PASS: "+message); }
    public override async void _Ready()
    {
        try
        {
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
            SaveData.Load(); SaveData.TutorialComplete = true;
            var level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>();
            AddChild(level);
            var hud = level.GetNode<Hud>("HUD"); hud.StartRun();
            await Wait(2.8f);
            for (int attempt = 0; !level.IsGameActive && attempt < 100; attempt++) await Wait(0.1f);
            Check(level.IsGameActive,"intro finishes before lantern test");
            var player = level.GetNode<Player>("Player");
            player.GetNode<Area2D>("Hitbox").Monitoring = false;
            level.StopFalling();
            Input.ActionPress(InputSetup.MoveLeft);
            float startX = player.Position.X;
            await Wait(0.4f);
            Check(Mathf.Abs(player.Position.X-startX) < 0.1f,"stale movement action cannot drive the cube left without a key or connected controller");
            Input.ActionRelease(InputSetup.MoveLeft);
            var lamp = player.GetNode<PointLight2D>("MouseLantern");
            Check(lamp.ShadowEnabled && lamp.Texture.ResourcePath.EndsWith("flashlight_cone.png"),"mouse lantern uses a directed cone with shadows");
            var mouse = new InputEventMouseMotion { Position = new Vector2(1000,440) };
            Input.ParseInputEvent(mouse);
            await Wait(0.3f);
            Vector2 direction = player.GetGlobalMousePosition()-player.GlobalPosition;
            float desired = direction.Angle()-Mathf.Pi*0.5f;
            Check(Mathf.Abs(Mathf.AngleDifference(lamp.GlobalRotation,desired)) < 0.08f,"lantern tracks the mouse in world coordinates");
            mouse.Dispose();
            player.SetProcess(false); lamp.GlobalRotation = 0f;
            Vector2 sample = GetViewport().GetCanvasTransform() * (player.GlobalPosition + new Vector2(0,180));
            async System.Threading.Tasks.Task<float> Brightness(bool enabled,string name)
            {
                lamp.Enabled = enabled; await Wait(0.1f);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var texture = GetViewport().GetTexture(); using var image = texture.GetImage();
                image.SavePng("res://artifacts/lantern-"+name+".png");
                float total = 0f;
                for (int x=-10;x<=10;x++) for (int y=-10;y<=10;y++)
                { Color color = image.GetPixel((int)sample.X+x,(int)sample.Y+y); total += color.R+color.G+color.B; }
                return total/441f;
            }
            if (DisplayServer.GetName() != "headless")
            {
                float off = await Brightness(false,"off"), on = await Brightness(true,"on");
                Check(on > off + 0.01f,$"lantern visibly illuminates the actual cave backdrop ({off:0.000} -> {on:0.000})");
            }
            player.SetProcess(true); lamp.Enabled = true;
            level.CurrentDepth = level.CoreDepthPoint;
            await Wait(0.04f);
            Check(level.VisualWarmth > 0f && level.VisualWarmth < 0.2f,"core transition begins gradually");
            await Wait(3f);
            Check(level.VisualWarmth > 0.9f,"cave warms towards the core over several seconds");
            var crystal = (Sprite2D)GetTree().GetNodesInGroup("TunnelCrystal")[0];
            var shader = (ShaderMaterial)crystal.Material;
            float clock = shader.GetShaderParameter("clock").AsSingle();
            float heat = level.VisualWarmth;
            GetTree().Paused = true;
            await Wait(0.2f);
            Check(Mathf.IsEqualApprox(heat,level.VisualWarmth) && Mathf.IsEqualApprox(clock,shader.GetShaderParameter("clock").AsSingle()),"pause freezes heat and crystal animation");
            GetTree().Paused = false;
            level.CurrentDepth = 0f;
            await Wait(0.04f);
            Check(level.VisualWarmth > 0.7f,"cool biome also fades instead of popping");
            level.Free();
            await Wait(0.15f);
            GD.Print("VISUAL SMOKE PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }
}
