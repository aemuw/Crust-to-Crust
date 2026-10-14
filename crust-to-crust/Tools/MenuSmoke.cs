using Godot;
using System;
using System.Reflection;

public partial class MenuSmoke : Node
{
    public override async void _Ready()
    {
        try
        {
            var level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            var hud=level.GetNode<Hud>("HUD");
            await ToSignal(GetTree().CreateTimer(0.15),SceneTreeTimer.SignalName.Timeout);
            var buttons=(Button[])typeof(Hud).GetField("_skinItems",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hud);
            if(buttons.Length!=CharacterCatalog.Count || Array.Exists(buttons,b=>b==null)) throw new Exception("all shapes and skins must have working shop buttons");
            if(!hud.HasNode("Control/Settings")) throw new Exception("settings must be a separate screen");
            var menu=hud.GetNode<Control>("Control/StartMenuPanel");
            foreach(Node child in menu.GetChildren())
                if(child is Label text && (text.Text.Contains("Пірнай") || text.Text.Contains("ГРАВІТАЦІЯ"))) throw new Exception("menu retains decorative slogans");
            level.Free(); await ToSignal(GetTree().CreateTimer(0.15),SceneTreeTimer.SignalName.Timeout);
            GD.Print("MENU SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
