using Godot;
using System;
using System.Reflection;

public partial class InterfaceSmoke : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private async System.Threading.Tasks.Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    public override async void _Ready()
    {
        LevelGenerator level = null;
        try
        {
            SaveData.Load();
            if (!SaveData.OwnsTrail(4) || !SaveData.OwnsTrail(5) || !SaveData.OwnsTrail(6)) throw new Exception("All three new cosmetic trails must be available");
            if (!SaveData.SelectTrail(4)) throw new Exception("Rainbow can be selected");
            typeof(SaveData).GetField("_loaded", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false); SaveData.Load();
            if (SaveData.SelectedTrail != 4) throw new Exception("Rainbow choice must survive saving and reload");
            level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            var hud = level.GetNode<Hud>("HUD");
            var buttons = (Button[])typeof(Hud).GetField("_trailItems", Hidden).GetValue(hud);
            if (buttons.Length != 7) throw new Exception("Shop must contain old and new trails");
            for (int id = 4; id < 7; id++) { buttons[id].EmitSignal(Button.SignalName.Pressed); if (SaveData.SelectedTrail != id) throw new Exception("New trail selector did not select " + id); }
            var sound = level.Runner.GetNode<AudioStreamPlayer>("CryoCoolingSfx");
            if (!sound.Stream.ResourcePath.Contains("SFX/cryo_cooling")) throw new Exception("Cooling must use the new cryogenic sound");
            if (DisplayServer.GetName() != "headless")
            {
                await Wait(0.2f); await Capture("menu");
                typeof(Hud).GetMethod("OpenShop", Hidden).Invoke(hud, null); await Wait(0.2f); await Capture("shop");
                var tabs = hud.GetNode<Control>("Control/CharacterShop").FindChildren("*", "TabContainer", true, false);
                buttons[4].EmitSignal(Button.SignalName.Pressed);
                ((TabContainer)tabs[0]).CurrentTab = 2; await Wait(0.2f); await Capture("trails");
                typeof(Hud).GetMethod("CloseShop", Hidden).Invoke(hud, null);
                var menu = hud.GetNode<Control>("Control/StartMenuPanel");
                typeof(Hud).GetMethod("OpenSettings", Hidden).Invoke(hud, new object[] { menu }); await Wait(0.2f); await Capture("settings");
                DisplayServer.WindowSetSize(new Vector2I(700,720)); await Wait(0.25f); await Capture("settings-narrow");
            }
            level.Free(); level = null; await Wait(0.2f);
            GD.Print("INTERFACE SMOKE PASSED"); GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); level?.Free(); GetTree().Quit(1); }
    }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage(); image.SavePng("res://artifacts/interface-" + name + ".png");
    }
}
