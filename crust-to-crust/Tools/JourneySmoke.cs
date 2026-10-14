using Godot;
using System;
using System.Reflection;

// Runs against real scenes; restores the save after freeing the scene that flushes it.
public partial class JourneySmoke : Node
{
    private LevelGenerator _level;
    private int _failures;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private void Check(bool ok, string message)
    {
        if (ok) GD.Print("PASS: " + message);
        else { _failures++; GD.PushError("FAIL: " + message); }
    }
    private async System.Threading.Tasks.Task Wait(float seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Hidden).SetValue(obj, value);
    private static object Get(object obj, string name) => obj.GetType().GetField(name, Hidden).GetValue(obj);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Hidden).Invoke(obj, args);
    private async System.Threading.Tasks.Task NewRun()
    {
        GetTree().Paused = false;
        if (_level != null) { _level.Free(); _level = null; }
        SaveData.Load(); SaveData.TutorialComplete = true;
        _level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>();
        AddChild(_level);
        _level.GetNode<Hud>("HUD").StartRun();
        _level.GetNode<Player>("Player").GetNode<Area2D>("Hitbox").Monitoring = false;
        await Wait(2.8f);
        for (int attempt = 0; !_level.IsGameActive && attempt < 100; attempt++) await Wait(0.1f);
        if (!_level.IsGameActive) throw new Exception("Intro did not finish");
        _level.GetNode<Player>("Player").GetNode<Area2D>("Hitbox").Monitoring = false;
    }
    private async System.Threading.Tasks.Task Fairness()
    {
        await NewRun();
        var player = _level.GetNode<Player>("Player");
        Set(player, "_hasShield", true);
        Call(player, "HitObstacle");
        Check(!player.HasShield && _level.IsGameActive, "shield absorbs a collision without ending the run");
        Set(player, "_invulnerableTimer", 0f);
        Call(player, "HitObstacle");
        await Wait(1f);
        var stats = (Label)Get(_level.GetNode<Hud>("HUD"), "_gameOverStats");
        Check(stats.Text.Contains("ЗІТКНЕННЯ"), "collision defeat explains its cause");

        await NewRun();
        player = _level.GetNode<Player>("Player");
        Set(player, "_isOnFire", true); Set(player, "_temperature", 1f);
        Set(player, "_currentBurnDuration", 24f); Set(player, "_burnTimer", 24f);
        _level.CurrentDepth = _level.CoreDepthPoint;
        Call(player, "UpdateHeating", 0.01f);
        await Wait(1f);
        stats = (Label)Get(_level.GetNode<Hud>("HUD"), "_gameOverStats");
        Check(stats.Text.Contains("ПЕРЕГРІВ") && stats.Text.Contains("Вод"), "overheat defeat explains water recovery");

        await NewRun();
        _level.SetProcess(false); player = _level.GetNode<Player>("Player"); player.SetProcess(false);
        var obstacle = GD.Load<PackedScene>("res://Scenes/Obstacles/Obstacle.tscn").Instantiate<Obstacle>();
        _level.AddChild(obstacle); obstacle.SetProcess(false);
        obstacle.Position = new Vector2(150, 1000); obstacle.ConfigureVariant(4); obstacle.TriggerAppear(Vector2.One);
        obstacle.speed = 1078f;
        float warned = 0f; bool dashed = false;
        bool visibleWarning = true;
        for (int step = 0; step < 140; step++)
        {
            float before = obstacle.Position.X;
            obstacle._Process(0.01);
            if ((bool)Get(obstacle, "_dashStarted"))
            {
                warned += 0.01f;
                var warningArt = (Label)Get(obstacle,"_warning");
                Vector2 screen = warningArt.GetGlobalTransformWithCanvas().Origin;
                visibleWarning &= screen.Y >= 0 && screen.Y <= GetViewport().GetVisibleRect().Size.Y-34;
            }
            if (Mathf.Abs(obstacle.Position.X - before) > 0.1f) { dashed = true; break; }
        }
        Check(dashed && warned >= 0.89f && obstacle.Position.Y > player.FixedY,
            "maximum-speed dash warns for 0.9 seconds and starts before reaching the player");
        Check(visibleWarning,"dash warning stays inside the viewport for the entire warning interval");
        var warning = (Label)Get(obstacle, "_warning");
        obstacle.ConfigureVariant(4);
        Check(warning.Visible && Mathf.IsEqualApprox(warning.Modulate.A, 1f), "pooled dash resets warning opacity");
        obstacle.Free();

        _level.CurrentDepth = _level.CoreDepthPoint; Call(_level, "SpawnTimers", 0f);
        Set(_level, "nextWaterInterval", 6f); Set(_level, "waterTimer", 0f);
        Call(_level, "SpawnTimers", 0.1f);
        Check((float)Get(_level, "nextWaterInterval") > 5f, "hot water retains its sampled interval after zone entry");
        var pool = (System.Collections.Generic.List<Node2D>)Get(_level,"obstaclePool");
        var scene = (PackedScene)Get(_level,"obstacleScene");
        foreach (Node child in _level.GetChildren()) if (child is Water water) water.Hide();
        _level.IsGameActive = false; _level.SetRunSeed(12345);
        foreach (Node2D item in pool) item.Hide();
        Call(_level,"SpawnFromPool",scene,pool,PickupKind.Coin,null);
        float normal = _level.Director.NextFloat(0,1);
        _level.SetRunSeed(12345);
        foreach (Node2D item in pool) item.Show();
        Call(_level,"SpawnFromPool",scene,pool,PickupKind.Coin,null);
        float saturated = _level.Director.NextFloat(0,1);
        Check(normal == saturated,"pool exhaustion preserves subsequent gameplay RNG decisions");
    }
    public override async void _Ready()
    {
        if (DisplayServer.GetName() != "headless") DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
        const string save = "user://crust_save.cfg";
        bool existed = FileAccess.FileExists(save);
        byte[] original = existed ? FileAccess.GetFileAsBytes(save) : null;
        try { await Fairness(); DirectorTests(); await ReplaySeedTest(); await ControllerTests(); await OnboardingTests(); await ZoneTests(); await CaptureZones(); }
        catch (Exception e) { _failures++; GD.PushError(e.ToString()); }
        finally
        {
            GetTree().Paused = false;
            if (_level != null) { _level.Free(); _level = null; }
            if (existed) { using var file = FileAccess.Open(save, FileAccess.ModeFlags.Write); file.StoreBuffer(original); }
            else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save));
        }
        GD.Print($"JOURNEY SMOKE: {_failures} failures"); GetTree().Quit(_failures == 0 ? 0 : 1);
    }
    private void DirectorTests()
    {
        Type type = typeof(LevelGenerator).Assembly.GetType("RunDirector");
        Check(type != null, "run decisions have a dedicated seeded director");
        if (type == null) return;
        object a = Activator.CreateInstance(type, 12345), b = Activator.CreateInstance(type, 12345), c = Activator.CreateInstance(type, 67890);
        var encounter = type.GetMethod("EncounterAt"); var lane = type.GetMethod("LaneAt");
        bool same = true, different = false, safe = true;
        for (int i = 0; i < 64; i++)
        {
            GD.Randf(); GD.RandRange(0, 999);
            object first = encounter.Invoke(a, new object[] { i });
            same &= first.Equals(encounter.Invoke(b, new object[] { i }));
            different |= !first.Equals(encounter.Invoke(c, new object[] { i }));
        }
        for (int distance = 0; distance < 80000; distance += 19)
        {
            float x = (float)lane.Invoke(a, new object[] { (float)distance });
            float next = (float)lane.Invoke(a, new object[] { distance + 10.78f });
            safe &= Mathf.Abs(x) <= 145.001f && Mathf.Abs(next - x) <= 1.7601f;
        }
        Check(same && different, "same seed survives cosmetic randomness and different seeds vary encounters");
        Check(safe, "sweeping passage stays within 145px and below 176 px/s at maximum speed");
        bool zoneVaries = false;
        for (int i = 0; i < 64; i++)
            zoneVaries |= _level.Director.EncounterForZone(i,CaveZone.Crystal) != _level.Director.EncounterForZone(i,CaveZone.Magma);
        Check(zoneVaries,"zone weights produce different encounter compositions for the same seed");
    }
    private async System.Threading.Tasks.Task ControllerTests()
    {
        await NewRun();
        var player = _level.GetNode<Player>("Player");
        var lamp = player.GetNode<PointLight2D>("MouseLantern");
        bool before = lamp.Enabled;
        GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.Y, Pressed = true });
        await Wait(0.1f);
        Check(lamp.Enabled != before, "controller Y toggles the lantern");
        GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.Y, Pressed = false });
        player.SetProcess(false);
        var camera = _level.GetNode<Camera2D>("Camera2D"); camera.IgnoreRotation = false; camera.Rotation = 1.2f;
        Input.ParseInputEvent(new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = 1f });
        Input.ParseInputEvent(new InputEventJoypadMotion { Axis = JoyAxis.RightY, AxisValue = 1f });
        await Wait(0.05f);
        Call(player,"UpdateLantern",0.3f);
        float desired = camera.Rotation + Mathf.Pi*0.25f - Mathf.Pi*0.5f;
        Check(Mathf.Abs(Mathf.AngleDifference(lamp.GlobalRotation,desired)) < 0.1f, $"right stick aims lantern in camera-relative world direction (pad={Get(player,"_aimWithPad")}, stick={Input.GetVector("aim_left","aim_right","aim_up","aim_down")}, angle={lamp.GlobalRotation}, desired={desired}, cameraGlobal={camera.GlobalRotation}, playerRotation={player.GlobalRotation}, scale={player.Scale})");
        Input.ParseInputEvent(new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = 0f });
        Input.ParseInputEvent(new InputEventJoypadMotion { Axis = JoyAxis.RightY, AxisValue = 0f });
        float held = lamp.GlobalRotation; Call(player,"UpdateLantern",0.1f);
        Check(Mathf.Abs(Mathf.AngleDifference(held,lamp.GlobalRotation)) < 0.05f, "neutral stick retains lantern direction");
        GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.Start, Pressed = true });
        await Wait(0.1f); Check(GetTree().Paused, "controller Start pauses the game");
        GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.Start, Pressed = false });
        var hud = _level.GetNode<Hud>("HUD"); Call(hud,"OpenShop");
        GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = true });
        await Wait(0.1f); Check(!((Control)Get(hud,"_shop")).Visible,"controller B returns from the shop");
        GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = false });
        int bindings = InputMap.ActionGetEvents("move_left").Count; InputSetup.Ensure(); InputSetup.Ensure();
        Check(InputMap.ActionGetEvents("move_left").Count == bindings,"input setup is idempotent");
        GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.Start, Pressed = true });
        await Wait(0.1f); Check(!GetTree().Paused, "controller Start resumes the game");
        GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.Start, Pressed = false });
        var shake = typeof(SaveData).GetField("CameraShake"); var flash = typeof(SaveData).GetField("FlashIntensity");
        Check(shake != null && flash != null, "independent shake and flash preferences are available");
        if (shake != null)
        {
            shake.SetValue(null,0f); Set(player,"_shakeStrength",18f); Set(player,"_currentVelocityX",0f);
            player.SetProcess(false); camera.Offset = Vector2.Zero;
            Call(player,"UpdateCamera",1f);
            Check(Mathf.Abs(camera.Offset.X) < 0.001f, "zero shake removes camera vibration and impact shake");
            shake.SetValue(null,1f);
        }
    }
    private async System.Threading.Tasks.Task ReplaySeedTest()
    {
        _level.IsGameActive = false; _level.SetRunSeed(42);
        float interval = (float)Get(_level,"nextCoinInterval");
        float next = _level.Director.NextFloat(0,1);
        _level.Free();
        typeof(Hud).GetField("_restartImmediately",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        LevelGenerator.RequestedSeed = 42;
        _level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(_level);
        await Wait(0.2f);
        Check(interval == (float)Get(_level,"nextCoinInterval") && next == _level.Director.NextFloat(0,1),
            "same-seed replay matches manual seed after all pools initialize");
    }
    private async System.Threading.Tasks.Task OnboardingTests()
    {
        await NewRun();
        var hud = _level.GetNode<Hud>("HUD");
        var begin = typeof(Hud).GetMethod("BeginTutorial");
        Check(begin != null,"onboarding responds to player actions rather than a fixed hint timer");
        if (begin == null) return;
        begin.Invoke(hud,null); await Wait(0.2f);
        Check((int)Get(hud,"_tutorialStep") == 0,"elapsed time alone does not complete the movement lesson");
        typeof(Hud).GetMethod("NotifyTutorialWater").Invoke(hud,null);
        Check((int)Get(hud,"_tutorialStep") == 0,"water contact cannot bypass the movement lesson");
        typeof(Hud).GetMethod("NotifyTutorialMove").Invoke(hud,null);
        typeof(Hud).GetMethod("NotifyTutorialLight").Invoke(hud,null);
        Check((int)Get(hud,"_tutorialStep") == 2,"movement and light actions unlock the water lesson");
        typeof(Hud).GetMethod("NotifyTutorialWater").Invoke(hud,null);
        Check(!(bool)Get(hud,"_tutorialActive"),"water contact completes onboarding");
        var progress = (ProgressBar)Get(hud,"_journeyBar");
        foreach (float depth in new[] {0f,20000f,40000f})
        {
            _level.CurrentDepth = depth; hud.UpdateDepth(depth); hud._Process(0.2);
            Check(Math.Abs(progress.Value-depth/40000f) < 0.001,"journey progress tracks depth including the core and exit");
        }
    }
    private async System.Threading.Tasks.Task ZoneTests()
    {
        Type director = typeof(LevelGenerator).Assembly.GetType("RunDirector");
        var zoneAt = director.GetMethod("ZoneAt");
        Check(zoneAt != null,"journey has distinct depth zones");
        if (zoneAt == null) return;
        foreach (var fixture in new[] {(0f,"Basalt"),(8000f,"Crystal"),(12000f,"Magma"),(17000f,"Core"),(23000f,"Core"),(28000f,"Magma"),(32000f,"Crystal"),(40000f,"Basalt")})
            Check(zoneAt.Invoke(null,new object[] {fixture.Item1,20000f}).ToString() == fixture.Item2,"depth zones are symmetric around the core");
        await NewRun(); _level.SetProcess(false); _level.GetNode<Player>("Player").SetProcess(false);
        int events = 0; _level.Connect("CoreCrossed",Callable.From(() => events++));
        _level.CurrentDepth = 19999f; _level.FallSpeed = 800f; _level._Process(0.01);
        float speed = _level.FallSpeed; _level._Process(0.01);
        Check(events == 1 && _level.FallSpeed >= speed,"core event occurs once without slowing the descent");
        _level.SetTunnelHazardsVisible(true); _level._PhysicsProcess(0.016);
        bool clear = true;
        foreach (Node area in _level.GetNode<Node2D>("TunnelSegments").FindChildren("*","Area2D",true,false))
            if (area is Area2D hazard && hazard.IsInGroup("Obstacle"))
                clear &= (bool)((System.Collections.Generic.Dictionary<Area2D,bool>)Get(_level,"_monitorStates"))[hazard] == false;
        Check(clear,"core respite protects against static crystal and wall hazards too");
        _level.RebaseEmergence(); _level.CurrentDepth = 19999f; _level._Process(0.01);
        Check(events == 2,"a new lap can announce its core again");
        var unlock = typeof(SaveData).GetMethod("UnlockMastery");
        int wallet = SaveData.Wallet;
        typeof(SaveData).GetField("MasteryMask").SetValue(null,0);
        bool first = (bool)unlock.Invoke(null,new object[] {2}); bool second = (bool)unlock.Invoke(null,new object[] {2});
        Check(first && !second && SaveData.Wallet == wallet,"mastery reward unlocks once without crediting coins");
    }
    private async System.Threading.Tasks.Task CaptureZones()
    {
        if (DisplayServer.GetName() == "headless") return;
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts"));
        var hud = _level.GetNode<Hud>("HUD");
        SaveData.SelectOrBuySkin(0); _level.GetNode<Player>("Player").ApplySkin();
        foreach (var sample in new[] {(0f,"basalt"),(10000f,"crystal"),(14000f,"magma"),(20000f,"core")})
        {
            _level.CurrentDepth = sample.Item1;
            Set(_level,"_coreRespite",0f);
            for (int i = 0; i < 40; i++) { Call(_level,"UpdateCaveAtmosphere",0.1f); _level._PhysicsProcess(0.1); }
            hud.UpdateDepth(sample.Item1); hud._Process(0.2);
            if (sample.Item2 == "core") hud.AnnounceCore();
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var shot = GetViewport().GetTexture().GetImage();
            shot.SavePng("res://artifacts/zone-"+sample.Item2+".png");
        }
        // Capture a real maximum-speed edge warning before its hazard becomes visible.
        var rock = _level.GetNode<Obstacle>("Obstacle");
        rock.Show(); rock.SetProcess(false); rock.Position = new Vector2(140,1000);
        rock.ConfigureVariant(4); rock.TriggerAppear(Vector2.One); rock.speed = 1078; rock._Process(0.4);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using (var warning = GetViewport().GetTexture().GetImage()) warning.SavePng("res://artifacts/edge-warning.png");
        hud.GetNode<Button>("Control/GameOverPanel/RestartButton").Show();
        Call(hud,"OpenShop");
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using (var shop = GetViewport().GetTexture().GetImage()) shop.SavePng("res://artifacts/shop-trails.png");
        Call(hud,"CloseShop");
        _level.Free();
        _level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(_level);
        DisplayServer.WindowSetSize(new Vector2I(700,720));
        await Wait(0.2f);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using (var narrow = GetViewport().GetTexture().GetImage()) narrow.SavePng("res://artifacts/narrow-menu.png");
    }
}
