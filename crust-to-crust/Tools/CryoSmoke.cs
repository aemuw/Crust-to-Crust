using Godot;
using System;
using System.Reflection;

public partial class CryoSmoke : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private int _failures;
    private void Check(bool ok, string text) { if (ok) GD.Print("PASS: " + text); else { _failures++; GD.PushError("FAIL: " + text); } }
    private static void Set(object node, string name, object value) => node.GetType().GetField(name, Hidden).SetValue(node, value);
    private static object Get(object node, string name) => node.GetType().GetField(name, Hidden).GetValue(node);
    private static void Heat(Player player, float dt) => typeof(Player).GetMethod("UpdateHeating", Hidden).Invoke(player, new object[] { dt });
    private async System.Threading.Tasks.Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    public override async void _Ready()
    {
        const string save = "user://crust_save.cfg";
        bool existed = FileAccess.FileExists(save); byte[] original = existed ? FileAccess.GetFileAsBytes(save) : null;
        LevelGenerator level = null;
        try
        {
            level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            var player = level.Runner; var hud = level.GetNode<Hud>("HUD");
            hud.StartRun();
            for (int i = 0; !level.IsGameActive && i < 100; i++) await Wait(0.05f);
            level.SetProcess(false); level.SetPhysicsProcess(false); level.SetTunnelHazardsVisible(false); level.FallSpeed = 0;
            player.SetProcess(false); player.SetPhysicsProcess(false);
            var hitbox = player.GetNode<Area2D>("Hitbox"); hitbox.Monitoring = false;
            foreach (Node child in level.GetChildren()) if (child is Water water) { water.Hide(); water.Monitorable = false; }
            level.CurrentDepth = level.CoreDepthPoint; Set(player, "_coolingGrace", 0f); Set(player, "_temperature", 0f);
            Heat(player, 0.1f);
            Check((bool)Get(player, "_isOnFire") && player.Temperature > 0 && player.Temperature < 0.02f, "heating immediately starts visible burning, without a second life bar");
            var vent = GD.Load<PackedScene>("res://Scenes/Obstacles/Water.tscn").Instantiate<Water>(); level.AddChild(vent);
            vent.Position = new Vector2(0, player.FixedY + 25); vent.speed = 0;
            foreach (bool leftSide in new[] { false, true })
            {
                vent.GenerateRandomCaveGeometry(leftSide); vent._Process(0.1);
                level.GetRockBounds(vent.Position.Y, out float left, out float right);
                var polygon = vent.GetNodeOrNull<CollisionPolygon2D>("CoolingZone");
                Check(polygon != null, "cooling uses a shaped jet instead of the old waterfall rectangle");
                if (polygon == null) continue;
                var rect = new Rect2(polygon.ToGlobal(polygon.Polygon[0]), Vector2.Zero);
                foreach (var p in polygon.Polygon) rect = rect.Expand(polygon.ToGlobal(p));
                Check(Mathf.Abs(rect.Size.X - (right - left) * 0.25f) < 2, "gas reaches one quarter of actual tunnel width on either wall");
                Check(!rect.HasPoint(new Vector2((left + right) / 2, rect.GetCenter().Y)), "center route never receives free cooling");
                Check(!vent.HasNode("WaterfallSprite"), "waterfall and capsule art are removed");
            }
            vent.GenerateRandomCaveGeometry(false); vent._Process(0.25);
            // Use the real physics overlap: contact must cool over time, never reset heat on entry.
            player.Position = vent.StreamCenter; hitbox.Monitoring = true;
            await Wait(0.1f);
            Set(player, "_temperature", 0.75f); Set(player, "_coolingGrace", 0f);
            typeof(Player).GetMethod("OnAreaEntered", Hidden).Invoke(player, new object[] { vent });
            Check(player.Temperature == 0.75f, "entering the plume does not instantly erase heat");
            Heat(player, 0.05f); float shortContact = player.Temperature;
            Check(shortContact < 0.75f && shortContact > 0.4f, "brief real plume contact gives partial cooling");
            Heat(player, 0.15f);
            Check(player.Temperature < shortContact, "longer contact removes more heat");
            Check((float)Get(player, "_coolingGrace") > 0, "gas contact grants insulation-compatible temporary protection");
            float runTime = level.ActivePlaySeconds;
            typeof(LevelGenerator).GetProperty("ActivePlaySeconds").SetValue(level, 60f);
            vent.speed = 1000; vent.Position = new Vector2(0, player.Position.Y + 1800);
            vent.GenerateRandomCaveGeometry(false); vent._Process(0.1);
            Check(!vent.IsCoolingActive, "later fissure warns before discharging, without invisible early cooling");
            vent.Position = new Vector2(0, player.Position.Y + 500); vent._Process(0.2);
            Check(vent.IsCoolingActive, "later jet is fully active before the high-speed crossing");
            vent.Position = new Vector2(0, -4000); vent._Process(0.01);
            Check(!vent.Visible && !vent.IsCoolingActive, "retired fissures cannot cool the runner");
            vent.Position = new Vector2(0, player.Position.Y + 25); vent.speed = 0; vent.Show(); vent.Monitorable = true;
            vent.ProcessMode = ProcessModeEnum.Inherit; vent.GenerateRandomCaveGeometry(true);
            Check(!vent.IsCoolingActive && vent.FromLeft, "pool reuse resets flow and changes wall without stale cooling");
            vent._Process(0.25);
            Check(vent.IsCoolingActive, "reused fissure can discharge normally on its new wall");
            typeof(LevelGenerator).GetProperty("ActivePlaySeconds").SetValue(level, runTime);
            hud.SetThermalStatus(0.8f, true, 99f, 0f);
            var label = (Label)Get(hud, "_heatLabel");
            Check(label.Text.Contains("80%") && !label.Text.Contains("99"), "HUD shows one overheating percentage");
            if (DisplayServer.GetName() != "headless")
            {
                vent.Position = new Vector2(0, 0); vent._Process(0.1); vent.Show();
                level.GetNode<Camera2D>("Camera2D").Position = Vector2.Zero;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage(); image.SavePng("res://artifacts/cryo-preview.png");
            }
            hitbox.Monitoring = false; player.Position = new Vector2(0, player.FixedY);
            Set(player, "_coolingGrace", 0f); Set(player, "_temperature", 0.9999f);
            Heat(player, 0.1f);
            Check(Get(player, "_state").ToString() == "Dead", "100 percent heat is fatal immediately, with no extra burn countdown");
        }
        catch (Exception ex) { _failures++; GD.PushError(ex.ToString()); }
        finally
        {
            GetTree().Paused = false; level?.Free(); await Wait(0.2f);
            if (existed) { using var file = FileAccess.Open(save, FileAccess.ModeFlags.Write); file.StoreBuffer(original); }
            else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save));
        }
        GD.Print("CRYO SMOKE: " + _failures + " failures"); GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
