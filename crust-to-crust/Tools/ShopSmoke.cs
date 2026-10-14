using Godot;
using System;
using System.Reflection;

// Run as a standalone scene. Restores the original save even after a failed assertion.
public partial class ShopSmoke : Node
{
    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        GD.Print("PASS: " + message);
    }
    public override void _Ready()
    {
        const string path = "user://crust_save.cfg";
        bool existed = FileAccess.FileExists(path);
        byte[] original = existed ? FileAccess.GetFileAsBytes(path) : null;
        int code = 0;
        try
        {
            var cfg = new ConfigFile(); cfg.SetValue("shop", "wallet", 72); cfg.Save(path);
            typeof(SaveData).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
            SaveData.Load();
            Check(SaveData.CameraShake == 1f && SaveData.FlashIntensity == 1f,"legacy save defaults to full shake and flashes");
            SaveData.CameraShake = 0f; SaveData.FlashIntensity = 0f; SaveData.Save();
            typeof(SaveData).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
            SaveData.Load();
            Check(SaveData.CameraShake == 0f && SaveData.FlashIntensity == 0f,"independent comfort settings survive reload");
            Check(!SaveData.Buy(-1), "invalid item rejected");
            Check(SaveData.Buy(0) && SaveData.Wallet == 60, "first level costs 12");
            Check(SaveData.Buy(0) && SaveData.Wallet == 36, "second level costs 24");
            Check(SaveData.Buy(0) && SaveData.Wallet == 0 && SaveData.Insulation == 3, "third level costs 36");
            Check(!SaveData.Buy(0), "maximum level cannot be purchased again");
            Check(!SaveData.Buy(1) && SaveData.Cooling == 0, "insufficient funds leave state intact");
            SaveData.CreditCoin(); SaveData.Save();
            typeof(SaveData).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
            SaveData.Load();
            Check(SaveData.Wallet == 1 && SaveData.Insulation == 3, "wallet and upgrades persist after reload");
            for (int i = 0; i < 60; i++) SaveData.CreditCoin();
            Check(!SaveData.SelectOrBuySkin(CharacterCatalog.Count), "invalid skin rejected");
            Check(SaveData.SelectOrBuySkin(1) && SaveData.Wallet == 37, "ice skin bought once for 24");
            Check(SaveData.SelectOrBuySkin(0) && SaveData.SelectOrBuySkin(1) && SaveData.Wallet == 37, "owned skin selection is free");
            Check(SaveData.SelectOrBuySkin(2) && SaveData.Wallet == 1, "ember skin costs 36");
            typeof(SaveData).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
            SaveData.Load();
            Check(SaveData.SelectedSkin == 2 && SaveData.OwnsSkin(1) && SaveData.OwnsSkin(2), "skin ownership and selection survive reload");
            SaveData.CreditCoin(300);
            for(int id=3;id<6;id++)
            {
                Check(SaveData.SelectOrBuySkin(id), "new character can be purchased: " + id);
                typeof(SaveData).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null,false);
                SaveData.Load();
                Check(SaveData.SelectedSkin==id && SaveData.OwnsSkin(id), "new character survives reload: " + id);
            }
            GD.Print("SHOP SMOKE PASSED");
        }
        catch (Exception error) { GD.PushError(error.ToString()); code = 1; }
        finally
        {
            if (existed) { using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write); file.StoreBuffer(original); }
            else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        }
        GetTree().Quit(code);
    }
}
