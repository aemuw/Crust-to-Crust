using Godot;
using System;
using System.Linq;
using System.Reflection;

// Exercises the real catalog, purchase path, player collision and workshop layout.
public partial class PixelShopSmoke : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool ok,string message) { if(!ok) throw new Exception(message); GD.Print("PASS: "+message); }
    private async System.Threading.Tasks.Task Wait()=>await ToSignal(GetTree().CreateTimer(0.15),SceneTreeTimer.SignalName.Timeout);
    private static void Call(Hud hud,string method)=>typeof(Hud).GetMethod(method,Hidden).Invoke(hud,null);
    private async System.Threading.Tasks.Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage(); image.SavePng("res://artifacts/pixel-"+name+".png");
    }
    public override async void _Ready()
    {
        const string save="user://crust_save.cfg";
        bool existed=FileAccess.FileExists(save); byte[] original=existed ? FileAccess.GetFileAsBytes(save) : null;
        LevelGenerator level=null; int code=0;
        try
        {
            Check(CharacterCatalog.Count==27,"18 existing skins plus nine new skins");
            Check(Enum.GetValues<BodyForm>().Length==9,"nine selectable body forms");
            var cfg=new ConfigFile(); cfg.SetValue("shop","wallet",20000); cfg.SetValue("skins","owned",(1<<18)-1);
            cfg.SetValue("skins","selected",17); cfg.SetValue("progress","tutorial_complete",true); cfg.Save(save);
            typeof(SaveData).GetField("_loaded",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false); SaveData.Load();
            Check(SaveData.SelectedSkin==17 && Enumerable.Range(0,18).All(SaveData.OwnsSkin),"legacy selection and all old purchases survive");
            foreach(int baseId in new[] {18,21,24})
            {
                int wallet=SaveData.Wallet;
                Check(!SaveData.SelectOrBuySkin(baseId+1) && SaveData.Wallet==wallet,"new variant requires its body form: "+baseId);
                Check(CharacterCatalog.BaseSkin(CharacterCatalog.Get(baseId).Form)==baseId,"new form has its own base skin: "+baseId);
                Check(SaveData.SelectOrBuySkin(baseId),"new form can be purchased: "+baseId);
                Check(SaveData.SelectOrBuySkin(baseId+1) && SaveData.SelectOrBuySkin(baseId+2),"both new variants can be purchased: "+baseId);
                wallet=SaveData.Wallet; Check(SaveData.SelectOrBuySkin(baseId) && SaveData.Wallet==wallet,"owned new form selection is free");
            }
            typeof(SaveData).GetField("_loaded",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false); SaveData.Load();
            Check(Enumerable.Range(0,27).All(SaveData.OwnsSkin),"all 27 purchases persist without bit-mask truncation");
            if(DisplayServer.GetName()!="headless") DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level); await Wait();
            var hud=level.GetNode<Hud>("HUD"); var player=level.Runner;
            await Capture("menu");
            foreach(int id in Enumerable.Range(0,27))
            {
                typeof(SaveData).GetProperty("SelectedSkin").SetValue(null,id); player.ApplySkin();
                var texture=CharacterCatalog.Texture(id);
                Check(texture.GetWidth()<=64 && texture.GetHeight()<=64,"native pixel resolution: "+id);
                var body=player.GetNode<CollisionShape2D>("collision");
                Check(body.Shape==player.GetNode<CollisionShape2D>("Hitbox/hitbox").Shape,"body/contact shape agree: "+id);
                if(id>=18)
                {
                    int vertices=id<21 ? 5 : id<24 ? 8 : 4;
                    Check(body.Shape is ConvexPolygonShape2D polygon && polygon.Points.Length==vertices,"new form collision follows silhouette: "+id);
                    Check(player.ProjectileSweepHits(player.GlobalPosition+new Vector2(0,150),player.GlobalPosition-new Vector2(0,150),4),"projectiles hit new form: "+id);
                }
            }
            // Show real locked states too, after checking persistence above.
            typeof(SaveData).GetProperty("SkinOwnedMask").SetValue(null,(1<<18)-1);
            typeof(SaveData).GetProperty("SelectedSkin").SetValue(null,0); player.ApplySkin();
            Call(hud,"OpenShop");
            var buttons=(Button[])typeof(Hud).GetField("_skinItems",Hidden).GetValue(hud);
            foreach(int id in new[] {0,18,21,24})
            {
                typeof(Hud).GetField("_previewSkin",Hidden).SetValue(hud,id); Call(hud,"RefreshShop"); await Wait();
                Check(buttons.Where(b=>b.Visible).Count()==(id==0 ? 6 : 3),"form filter exposes every expected variant: "+id);
                await Capture("shop-"+id);
            }
            typeof(Hud).GetField("_previewSkin",Hidden).SetValue(hud,19); Call(hud,"RefreshShop"); await Wait();
            var unlock=(Button)typeof(Hud).GetField("_unlockForm",Hidden).GetValue(hud);
            Check(unlock.Visible && !unlock.Disabled,"locked variant offers an affordable form unlock");
            await Capture("shop-locked");
            if(DisplayServer.GetName()!="headless")
            {
                DisplayServer.WindowSetSize(new Vector2I(700,720)); await Wait(); await Wait();
                var purchase=(Button)typeof(Hud).GetField("_buySkin",Hidden).GetValue(hud);
                Check(GetViewport().GetVisibleRect().Grow(2).Encloses(purchase.GetGlobalRect()),"purchase button fits narrow workshop");
                Check(GetViewport().GetVisibleRect().Grow(2).Encloses(unlock.GetGlobalRect()),"form unlock fits narrow workshop");
                await Capture("shop-narrow");
                DisplayServer.WindowSetSize(new Vector2I(1280,720)); await Wait();
            }
        }
        catch(Exception error) { code=1; GD.PushError(error.ToString()); }
        finally
        {
            GetTree().Paused=false; level?.Free();
            if(existed) { using var file=FileAccess.Open(save,FileAccess.ModeFlags.Write); file.StoreBuffer(original); }
            else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save));
        }
        GD.Print("PIXEL SHOP SMOKE: "+(code==0 ? "PASSED" : "FAILED")); GetTree().Quit(code);
    }
}
