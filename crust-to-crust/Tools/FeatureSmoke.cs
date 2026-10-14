using Godot;
using System;
using System.Linq;
using System.Reflection;

public partial class FeatureSmoke : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private async System.Threading.Tasks.Task Wait(float seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);
    private static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Hidden).SetValue(obj,value);
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage(); image.SavePng("res://artifacts/"+name+".png");
    }
    public override async void _Ready()
    {
        LevelGenerator level=null;
        try
        {
            if(DisplayServer.GetName()!="headless") DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus,true);
            SaveData.Load(); SaveData.TutorialComplete=true;
            int originalSkin=SaveData.SelectedSkin;
            level=GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            await Wait(0.25f);
            var player=level.GetNode<Player>("Player"); var hud=level.GetNode<Hud>("HUD");
            await Capture("rush-menu");
            Call(hud,"OpenShop"); Set(hud,"_previewSkin",3); Call(hud,"RefreshShop"); await Wait(0.05f);
            await Capture("rush-miner-shop");
            Set(hud,"_previewSkin",4); Call(hud,"RefreshShop"); await Capture("rush-robot-shop");
            Set(hud,"_previewSkin",5); Call(hud,"RefreshShop"); await Capture("rush-golem-shop");
            Call(hud,"CloseShop");
            Call(hud,"OpenSettings",hud.GetNode<Control>("Control/StartMenuPanel")); await Wait(0.05f);
            await Capture("rush-settings"); Call(hud,"CloseSettings");
            foreach(int id in Enumerable.Range(0,CharacterCatalog.Count))
            {
                typeof(SaveData).GetProperty("SelectedSkin").SetValue(null,id); player.ApplySkin();
                var body=player.GetNode<CollisionShape2D>("collision"); var hitbox=player.GetNode<CollisionShape2D>("Hitbox/hitbox");
                Check(body.Shape==hitbox.Shape,"body and contact collision agree: "+id);
                Check(CharacterCatalog.Texture(id).GetHeight()>0,"character art is imported: "+id);
                Check(player.ProjectileSweepHits(player.GlobalPosition+new Vector2(0,200),player.GlobalPosition-new Vector2(0,200),8),"sweep hits actual body: "+id);
                Check(!player.ProjectileSweepHits(player.GlobalPosition+new Vector2(100,200),player.GlobalPosition+new Vector2(100,-200),8),"sweep respects clear space: "+id);
            }
            typeof(SaveData).GetProperty("SelectedSkin").SetValue(null,originalSkin); player.ApplySkin();
            hud.StartRun();
            if(DisplayServer.GetName()!="headless")
            {
                hud._Notification((int)NotificationApplicationFocusOut);
                Check(!GetTree().Paused,"noninteractive rendering window ignores focus-loss pause");
            }
            for(int i=0;!level.IsGameActive && i<100;i++) await Wait(0.1f);
            Check(level.IsGameActive,"intro completes");
            player.GetNode<Area2D>("Hitbox").Monitoring=false;
            Call(hud,"TogglePause"); Call(hud,"OpenSettings",hud.GetNode<Control>("Control").GetChildren().OfType<Control>().First(c=>c.Name=="PauseOverlay"));
            Call(hud,"CloseSettings"); Check(GetTree().Paused,"closing pause settings preserves pause");
            await Capture("rush-pause"); Call(hud,"TogglePause");
            level.SetProcess(false); level.SetPhysicsProcess(false); player.SetProcess(false);
            level.SetTunnelHazardsVisible(false); await Wait(0.05f);
            level.FallSpeed=0;
            var left=new WallShooter(); level.AddChild(left); left.Activate(level,0,-1,100); left.SetPhysicsProcess(false);
            var right=new WallShooter(); level.AddChild(right); right.Activate(level,1,1,330); right.SetPhysicsProcess(false);
            await Capture("rush-wall-shooters");
            level.FallSpeed=500;
            typeof(LevelGenerator).GetProperty("Encounter").SetValue(level,EncounterKind.Sentry);
            Set(level,"_coreRespite",0f);
            Check(level.TryFireVolley(left,0,new Vector2(0,-200)),"wall sentry fires a real pooled projectile");
            Check(level.GetChildren().OfType<HazardProjectile>().Count(p=>p.Visible)==1,"single shot uses one pool entry");
            await Wait(0.1f); await Capture("rush-projectile");
            level.StopFalling();
            Check(level.GetChildren().OfType<HazardProjectile>().All(p=>!p.Visible),"run stop clears every projectile");
            level.IsGameActive=true; level.FallSpeed=500;
            player.SetPhysicsProcess(false); player._PhysicsProcess(0);
            player.GetNode<Area2D>("Hitbox").Monitoring=true;
            Call(player,"GrantShield");
            var incoming=level.GetChildren().OfType<HazardProjectile>().First();
            incoming.Activate(player.GlobalPosition+new Vector2(0,80),new Vector2(0,-480),0,level);
            incoming._PhysicsProcess(0.1);
            Check(!player.HasShield && level.IsGameActive && !incoming.Visible,"real projectile consumes shield once and is removed");
            player.GetNode<Area2D>("Hitbox").Monitoring=false;
            Check(level.TryFireVolley(right,1,new Vector2(0,-200)),"magma shell fires a real three-angle volley");
            var live=level.GetChildren().OfType<HazardProjectile>().Where(p=>p.Visible).ToArray();
            Check(live.Length==3 && live.Select(p=>p.Path.Velocity.Angle()).Distinct().Count()==3,"fan angles are distinct");
            GetTree().Paused=true; Vector2 position=live[0].Position; await Wait(0.1f);
            Check(live[0].Position==position,"pause freezes real projectile motion"); GetTree().Paused=false;
            int children=level.GetChildCount();
            foreach(var shot in level.GetChildren().OfType<HazardProjectile>())
                shot.Activate(new Vector2(-220,900),new Vector2(0,-480),0,level);
            Check(!level.TryFireVolley(left,0,new Vector2(0,-200)) && level.GetChildCount()==children,"full pool skips volley without allocating nodes");
            level.CurrentDepth=level.CoreDepthPoint-1; level._Process(0.01);
            Check(live.All(p=>!p.Visible),"core respite clears every projectile");
            level.Free(); level=null; await Wait(0.15f);
            GD.Print("FEATURE SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex)
        {
            GD.PushError(ex.ToString()); GetTree().Paused=false;
            level?.Free(); await Wait(0.15f); GetTree().Quit(1);
        }
    }
}
