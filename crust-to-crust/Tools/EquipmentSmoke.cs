using Godot;
using System;
using System.Reflection;

public partial class EquipmentSmoke : Node
{
    private int _failures;
    private void Check(bool ok,string text) { if(ok) GD.Print("PASS: "+text); else { _failures++; GD.PushError("FAIL: "+text); } }
    private static void Reload() { typeof(SaveData).GetField("_loaded",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,false); SaveData.Load(); }
    public override void _Ready()
    {
        const string path="user://crust_save.cfg";
        bool existed=FileAccess.FileExists(path); byte[] original=existed ? FileAccess.GetFileAsBytes(path) : null;
        try
        {
            var cfg=new ConfigFile(); cfg.SetValue("shop","wallet",10000); cfg.SetValue("shop","cooling",3); cfg.SetValue("skins","owned",63); cfg.SetValue("skins","selected",4); cfg.SetValue("best","distance",1234f); cfg.Save(path); Reload();
            Check(CharacterCatalog.Count>=12,"original geometric skins remain available");
            Check(CharacterCatalog.Get(3).Name!="Шахтар" && CharacterCatalog.Get(4).Name!="Буровий робот" && CharacterCatalog.Get(5).Name!="Голем","old character ownership maps to replacement cubes");
            Check(CharacterCatalog.Get(3).CreateBodyShape() is RectangleShape2D,"replacement cube has square collision");
            Check(!SaveData.SelectOrBuySkin(7),"circle skin is locked until its form is unlocked");
            int wallet=SaveData.Wallet;
            Check(SaveData.SelectOrBuySkin(6) && SaveData.Wallet==wallet-1500,"circle form unlock costs fifteen hundred");
            Check(SaveData.SelectOrBuySkin(7),"unlocked circle accepts another skin");
            Check(CharacterCatalog.Get(6).CreateBodyShape() is CircleShape2D,"circle has round collision");
            wallet=SaveData.Wallet;
            Check(SaveData.SelectOrBuySkin(9) && SaveData.Wallet==wallet-2500 && CharacterCatalog.Get(9).CreateBodyShape() is ConvexPolygonShape2D,"triangle unlock costs twenty-five hundred and has polygon collision");
            for(int id=3;id<7;id++) Check(SaveData.Buy(id),"new equipment can be bought: "+id);
            Reload();
            Check(SaveData.UpgradeLevel(3)==1 && SaveData.UpgradeLevel(6)==1,"new equipment persists after reload");
            for(int id=3;id<7;id++)
            {
                Check(SaveData.Buy(id) && SaveData.Buy(id) && SaveData.UpgradeLevel(id)==3,"all three levels can be bought: "+id);
                wallet=SaveData.Wallet;
                Check(!SaveData.Buy(id) && SaveData.Wallet==wallet,"maximum equipment cannot charge coins again: "+id);
            }
            Check(!SaveData.Buy(UpgradeCatalog.Count),"invalid equipment is rejected");
            SaveData.CreditCoin(10000);
            foreach(int baseSkin in new[] {12,14,16})
            {
                Check(!SaveData.SelectOrBuySkin(baseSkin+1),"new family skin requires its base form: "+baseSkin);
                wallet=SaveData.Wallet;
                Check(SaveData.SelectOrBuySkin(baseSkin) && SaveData.Wallet==wallet-SaveData.SkinPrice(baseSkin),"new form purchase deducts its correct price: "+baseSkin);
                Check(SaveData.SelectOrBuySkin(baseSkin+1),"new family alternate skin can be bought: "+baseSkin);
                Reload(); Check(SaveData.OwnsSkin(baseSkin+1) && SaveData.SelectedSkin==baseSkin+1,"new form and alternate survive reload: "+baseSkin);
            }
            var reset=typeof(SaveData).GetMethod("ResetEquipment",BindingFlags.Public|BindingFlags.Static);
            Check(reset!=null,"one-time equipment reset is available without resetting progress");
            if(reset!=null)
            {
                wallet=SaveData.Wallet; int owned=SaveData.SkinOwnedMask;
                reset.Invoke(null,null); Reload();
                bool zero=true; for(int i=0;i<7;i++) zero&=SaveData.UpgradeLevel(i)==0;
                Check(zero && SaveData.Wallet==wallet && SaveData.SkinOwnedMask==owned && SaveData.BestDistance==1234,"reset clears only equipment, preserving wallet, skins and records");
                Check(SaveData.Buy(4),"equipment can be bought again after reset"); Reload();
                Check(SaveData.UpgradeLevel(4)==1,"reset does not repeat on later loads");
            }
        }
        catch(Exception error) { _failures++; GD.PushError(error.ToString()); }
        finally { if(existed) { using var file=FileAccess.Open(path,FileAccess.ModeFlags.Write); file.StoreBuffer(original); } else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path)); }
        GD.Print("EQUIPMENT SMOKE: "+_failures+" failures"); GetTree().Quit(_failures==0 ? 0 : 1);
    }
}
