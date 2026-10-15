using Godot;
using System;
using System.Reflection;

public partial class HudPanelSmoke : Node
{
    private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static T Field<T>(Hud hud,string name)=>(T)typeof(Hud).GetField(name,Hidden).GetValue(hud);
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    private async System.Threading.Tasks.Task Wait(float seconds=0.15f)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async System.Threading.Tasks.Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage(); image.SavePng("res://artifacts/hud-panel-"+name+".png");
    }
    public override async void _Ready()
    {
        LevelGenerator level=null; int code=0;
        try
        {
            if(DisplayServer.GetName()!="headless") DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
            SaveData.Load(); SaveData.TutorialComplete=true;
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            var hud=level.GetNode<Hud>("HUD"); var panel=Field<Control>(hud,"_runPanel");
            Check(!panel.Visible,"telemetry casing is hidden in the menu");
            hud.StartRun(); level.Runner.GetNode<Area2D>("Hitbox").Monitoring=false;
            for(int i=0;!level.IsGameActive && i<100;i++) await Wait(0.1f);
            Check(level.IsGameActive,"real run reaches playable state");
            level.SetProcess(false); level.SetPhysicsProcess(false); level.SetTunnelHazardsVisible(false);
            level.Runner.SetProcess(false); level.Runner.SetPhysicsProcess(false); hud.SetProcess(false);
            level.CurrentDepth=426; level.FallSpeed=352; hud.UpdateDepth(426); hud.UpdateScore(0); hud._Process(0.11);
            await Wait(); await Capture("normal");
            var depth=Field<Label>(hud,"_depthLabel"); var score=Field<Label>(hud,"_scoreLabel");
            var speed=Field<Label>(hud,"_speedLabel"); var destination=Field<Label>(hud,"_destinationLabel");
            var bar=Field<ProgressBar>(hud,"_journeyBar");
            var font=depth.GetThemeFont("font");
            foreach(char ch in "ІіЇїЄєҐґ") Check(font.HasChar(ch),"pixel font contains Ukrainian glyph: "+ch);
            Check(panel.Visible && depth.GetThemeFontSize("font_size")>score.GetThemeFontSize("font_size"),"depth leads the instrument hierarchy");
            Check(bar.Size.Y<=10 && !bar.GetGlobalRect().Intersects(destination.GetGlobalRect()),"thin route bar does not overlap its caption");
            hud.UpdateDepth(39573); level.CurrentDepth=39573; level.FallSpeed=1250; hud.UpdateScore(123456); hud.UpdateRunStats(12,39573);
            hud.SetBonuses(12,9); hud.SetShield(true); hud.SetThermalStatus(0.7f,true,8,0); hud._Process(0.11); await Wait();
            foreach(string field in new[] {"_depthLabel","_scoreLabel","_speedLabel","_lapLabel","_destinationLabel","_zoneLabel"})
                Check(panel.GetGlobalRect().Grow(1).Encloses(Field<Label>(hud,field).GetGlobalRect()),"long run readout stays inside casing: "+field);
            Check(!score.GetGlobalRect().Intersects(speed.GetGlobalRect()),"coin and speed rows stay separate");
            Check(destination.Text.StartsWith("ДО ВИХОДУ"),"route caption updates beyond the core");
            await Capture("bonuses");
            hud.ShowGameOver(); Check(!panel.Visible && !depth.Visible && !score.Visible,"end screen clears the telemetry panel");
        }
        catch(Exception error) { code=1; GD.PushError(error.ToString()); }
        finally { GetTree().Paused=false; level?.Free(); await Wait(); }
        GD.Print("HUD PANEL SMOKE: "+(code==0 ? "PASSED" : "FAILED")); GetTree().Quit(code);
    }
}
