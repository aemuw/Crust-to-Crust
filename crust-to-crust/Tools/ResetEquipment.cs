using Godot;
using System;

/// <summary>Manual developer tool: reset equipment once, retaining a complete save backup.</summary>
public partial class ResetEquipment : Node
{
    public override void _Ready()
    {
        try
        {
            const string save="user://crust_save.cfg";
            if(!FileAccess.FileExists(save)) throw new Exception("No existing save found; nothing was reset.");
            byte[] original=FileAccess.GetFileAsBytes(save);
            string backup="res://artifacts/equipment-before-reset-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".cfg";
            using(var copy=FileAccess.Open(backup,FileAccess.ModeFlags.Write)) copy.StoreBuffer(original);
            var before=new ConfigFile(); if(before.Load(save)!=Error.Ok) throw new Exception("Save could not be read.");
            // Preserve every unrelated section and key, including future settings.
            string[] keys={"insulation","cooling","lantern","accumulator","battery_efficiency","magnet","shield_recovery"};
            foreach(string key in keys) before.SetValue("shop",key,0);
            if(before.Save(save)!=Error.Ok) throw new Exception("Save could not be written; backup retained at "+backup);
            var after=new ConfigFile(); if(after.Load(save)!=Error.Ok) throw new Exception("Reset save cannot be reopened.");
            foreach(string key in keys) if(after.GetValue("shop",key,-1).AsInt32()!=0) throw new Exception("Reset verification failed: "+key);
            var old=new ConfigFile(); old.Parse(System.Text.Encoding.UTF8.GetString(original));
            foreach(string section in old.GetSections()) foreach(string key in old.GetSectionKeys(section))
                if(!(section=="shop" && Array.IndexOf(keys,key)>=0) && after.GetValue(section,key).ToString()!=old.GetValue(section,key).ToString())
                    throw new Exception("Unrelated setting changed: "+section+"/"+key);
            GD.Print("EQUIPMENT RESET VERIFIED: all seven levels are zero; every other saved value preserved.");
            GD.Print("BACKUP: "+ProjectSettings.GlobalizePath(backup)); GetTree().Quit();
        }
        catch(Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
