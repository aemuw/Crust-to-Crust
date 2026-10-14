using Godot;
using System;

public partial class DarknessSmoke : Node
{
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); GD.Print("PASS: "+text); }
    public override void _Ready()
    {
        try
        {
            var charge=new LanternCharge();
            Check(charge.Seconds==14 && charge.Capacity==18,"lantern starts with14 of18 seconds");
            charge.Tick(4,true); Check(charge.Seconds==10,"enabled lamp consumes one unit per second");
            charge.Tick(60,false); Check(charge.Seconds==10,"off/paused/cinematic lamp neither drains nor regenerates");
            charge.Refill(); Check(charge.Seconds==15,"battery restores exactly5 seconds");
            charge.Refill(); Check(charge.Seconds==18,"battery cannot overfill capacity");
            charge.Tick(100,true); Check(charge.Seconds==0,"charge stops at zero");
            charge.Tick(-10,true); charge.Tick(float.NaN,true); Check(charge.Seconds==0,"invalid time cannot create charge");
            charge.Refill(); Check(charge.Seconds==5,"empty lamp can recover from a battery");
            for(int i=0;i<60;i++) charge.Tick(1f/60,true);
            Check(Mathf.Abs(charge.Seconds-4)<0.001f,"charge is frame-rate independent");
            var upgraded=new LanternCharge(3,3);
            Check(upgraded.Seconds==20 && upgraded.Capacity==24,"accumulator improves starting charge and capacity");
            upgraded.Tick(100,true); upgraded.Refill();
            Check(upgraded.Seconds==6.5f && upgraded.Seconds/12<0.55f,"maximum battery upgrade cannot sustain permanent illumination");
            GD.Print("DARKNESS SMOKE PASSED"); GetTree().Quit();
        }
        catch(Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
