using Godot;
using System;
using System.Reflection;

// Integration checks for the core speed regression and effect pickup accounting.
public partial class CoreBonusSmoke : Node
{
    private async System.Threading.Tasks.Task Wait(float t) => await ToSignal(GetTree().CreateTimer(t),SceneTreeTimer.SignalName.Timeout);
    private void Check(bool ok,string text) { if (!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    public override async void _Ready()
    {
        try
        {
            var level = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<LevelGenerator>(); AddChild(level);
            level.GetNode<Hud>("HUD").StartRun(); await Wait(2.8f);
            var player = level.Runner; player.GetNode<Area2D>("Hitbox").Monitoring = false;
            level.CurrentDepth = level.CoreDepthPoint-250f; level.FallSpeed = 600;
            float previous = level.FallSpeed;
            for (int i = 0; i < 15; i++)
            { await Wait(0.1f); Check(level.FallSpeed >= previous,"core passage never reduces speed"); previous = level.FallSpeed; }
            Check(level.CurrentDepth > level.CoreDepthPoint && level.FallSpeed > 600,"speed grows across the core");
            int lights = 0;
            foreach (Node node in level.GetNode<Node2D>("TunnelSegments").FindChildren("CrystalLight","PointLight2D",true,false))
                if (((PointLight2D)node).Enabled) lights++;
            Check(lights <= level.CrystalLightBudget,"active crystal light count respects budget");
            MethodInfo collect = typeof(Player).GetMethod("OnAreaEntered",BindingFlags.NonPublic|BindingFlags.Instance);
            Coin Pickup(PickupKind kind)
            {
                var item = GD.Load<PackedScene>("res://Scenes/Obstacles/Coin.tscn").Instantiate<Coin>();
                level.AddChild(item); item.Position = new Vector2(0,800); item.speed = 0; item.SetKind(kind);
                collect.Invoke(player,new object[] { item }); return item;
            }
            Pickup(PickupKind.DoubleCoins); Check(player.CoinMultiplier == 2,"double coin bonus activates");
            int wallet = SaveData.Wallet; var coin = Pickup(PickupKind.Coin);
            Check(SaveData.Wallet == wallet+2,"one coin awards two wallet coins");
            collect.Invoke(player,new object[] { coin }); Check(SaveData.Wallet == wallet+2,"hidden coin cannot be awarded twice");
            Pickup(PickupKind.Magnet); Check(player.MagnetActive,"magnet bonus activates");
            Pickup(PickupKind.Shield); Check(player.HasShield,"styled shield pickup grants protection");
            GetTree().Paused = true; await Wait(0.3f);
            Check(player.CoinMultiplier == 2 && player.MagnetActive,"pause does not consume effect timers");
            GetTree().Paused = false;
            GD.Print("CORE BONUS SMOKE PASSED"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }
}
