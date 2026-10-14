using Godot;
using System;
using System.Linq;
using System.Reflection;

public partial class PresentationSmoke : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private int _failures;
    private void Check(bool ok,string text) { if(ok) GD.Print("PASS: "+text); else { _failures++; GD.PushError("FAIL: "+text); } }
    private async System.Threading.Tasks.Task Wait(float seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private static object Field(object node,string key)=>node.GetType().GetField(key,Hidden).GetValue(node);
    private static void Call(object node,string key,params object[] args)=>node.GetType().GetMethod(key,Hidden).Invoke(node,args);
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage(); image.SavePng("res://artifacts/presentation-"+name+".png");
    }
    public override async void _Ready()
    {
        const string save="user://crust_save.cfg";
        bool existed=FileAccess.FileExists(save); byte[] original=existed ? FileAccess.GetFileAsBytes(save) : null;
        LevelGenerator level=null;
        try
        {
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
            var cfg=new ConfigFile(); cfg.SetValue("shop","wallet",10000); cfg.SetValue("progress","tutorial_complete",true); cfg.Save(save);
            typeof(SaveData).GetField("_loaded",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false); SaveData.Load();
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level); await Wait(0.1f);
            var player=level.Runner; var hud=level.GetNode<Hud>("HUD"); Call(hud,"OpenShop");
            var tabs=hud.GetNode<Control>("Control/CharacterShop").FindChildren("*","TabContainer",true,false).OfType<TabContainer>().Single();
            var tiles=(Button[])Field(hud,"_skinItems");
            foreach(BodyForm form in Enum.GetValues<BodyForm>())
            {
                typeof(Hud).GetField("_previewSkin",Hidden).SetValue(hud,CharacterCatalog.BaseSkin(form)); Call(hud,"RefreshShop"); await Wait(0.06f);
                Check(tiles.Where((tile,i)=>tile.Visible).All(tile=>CharacterCatalog.Get(Array.IndexOf(tiles,tile)).Form==form),"shop filter shows only selected form: "+form);
                await Capture("shop-"+form.ToString().ToLowerInvariant());
            }
            tabs.CurrentTab=1; await Wait(0.1f); await Capture("equipment");
            var equipment=(Button[])Field(hud,"_shopItems"); equipment[^1].GrabFocus(); await Wait(0.1f);
            Check(GetViewport().GetVisibleRect().Grow(1).Encloses(equipment[^1].GetGlobalRect()),"last equipment card scrolls into view");
            tabs.CurrentTab=2; SaveData.MasteryMask=7;
            var trailButtons=(Button[])Field(hud,"_trailItems");
            for(int i=0;i<4;i++)
            {
                trailButtons[i].EmitSignal(Button.SignalName.Pressed); await Wait(0.04f);
                Check(SaveData.SelectedTrail==i && ((TrailPreview)Field(hud,"_trailPreview")).StyleId==i,"trail choice and preview agree: "+i);
                await Capture("trail-preview-"+i);
            }
            tabs.CurrentTab=0; DisplayServer.WindowSetSize(new Vector2I(700,720)); await Wait(0.25f);
            Check(GetViewport().GetVisibleRect().Grow(2).Encloses(((Button)Field(hud,"_buySkin")).GetGlobalRect()),"shop purchase remains reachable in a narrow window");
            await Capture("narrow-shop"); DisplayServer.WindowSetSize(new Vector2I(1280,720)); await Wait(0.15f); Call(hud,"CloseShop");
            hud.StartRun(); await Wait(0.95f);
            Check(!level.IsGameActive && player.Position.Y<-470 && player.Position.X>-310,"intro follows a continuous airborne arc before tunnel entry"); await Capture("intro-arc");
            for(int i=0;!level.IsGameActive && i<100;i++) await Wait(0.05f);
            player.GetNode<Area2D>("Hitbox").Monitoring=false;
            level.SetProcess(false); level.SetPhysicsProcess(false); level.SetTunnelHazardsVisible(false); level.FallSpeed=600; player.SetPhysicsProcess(false);
            await Wait(0.7f);
            var trail=player.GetNode<MasteryTrail>("MasteryTrail");
            Check(trail.Visible && trail.StyleId==3 && trail.SampleCount>=20,"selected water mastery produces an actual trail behind the moving runner"); await Capture("actual-trail");
            GetTree().Paused=true; int samples=trail.SampleCount; var points=(Vector2[])Field(trail,"_points"); Vector2 saved=points[0]; await Wait(0.15f);
            Check(samples==trail.SampleCount && saved==points[0],"pause freezes actual trail samples"); GetTree().Paused=false;
            level.CurrentDepth=level.TunnelLength; level.SetProcess(true);
            var camera=level.GetNode<Camera2D>("Camera2D"); bool exit=false,turn=false;
            Vector2 previous=GetViewport().GetCanvasTransform()*player.GlobalPosition; ulong last=Time.GetTicksMsec(),deadline=last+10000;
            float worst=0; bool smooth=true;
            do
            {
                await Wait(0.025f); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                Vector2 now=GetViewport().GetCanvasTransform()*player.GlobalPosition; ulong tick=Time.GetTicksMsec(); float elapsed=(tick-last)/1000f;
                float jump=previous.DistanceTo(now); worst=Mathf.Max(worst,jump); if(jump>1800*elapsed+25) smooth=false;
                previous=now; last=tick;
                if(!exit && player.Position.Y>820 && camera.Rotation<0.1f) { exit=true; await Capture("exit"); }
                if(!turn && camera.Rotation>0.8f && camera.Rotation<2.2f) { turn=true; await Capture("turn"); }
            } while((level.Lap<2 || !level.IsGameActive) && Time.GetTicksMsec()<deadline);
            Check(exit && turn && level.Lap==2 && level.IsGameActive,"cinematic reaches apex, rotates and re-enters the next traversal");
            Check(smooth,$"camera rebase stays continuous in rendered screen coordinates (largest sample movement={worst:0.0}px)"); await Capture("returned");
            level.Free(); level=null; await Wait(0.2f);
        }
        catch(Exception error) { _failures++; GD.PushError(error.ToString()); GetTree().Paused=false; level?.Free(); await Wait(0.2f); }
        finally
        {
            if(existed) { using var file=FileAccess.Open(save,FileAccess.ModeFlags.Write); file.StoreBuffer(original); }
            else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save));
        }
        GD.Print("PRESENTATION SMOKE: "+_failures+" failures"); GetTree().Quit(_failures==0 ? 0 : 1);
    }
}
