using Godot;
using System;

public partial class MomentumSmoke : Node
{
    private static void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); GD.Print("PASS: " + message); }
    public override void _Ready()
    {
        try
        {
            foreach (float divisor in new[] { 2f, 2.4f, 3f, 4f })
            {
                var momentum = new RunMomentum(340, 980, divisor);
                momentum.Observe(800); momentum.Observe(0);
                float first = momentum.NextTraversal();
                Check(Mathf.IsEqualApprox(first, Mathf.Max(400, 800 / divisor)), "peak survives cinematic and first return: " + divisor);
                momentum.Observe(1200);
                float second = momentum.NextTraversal();
                Check(Mathf.IsEqualApprox(second, Mathf.Max(first + 60, 1200 / divisor)), "second return grows: " + divisor);
                momentum.Observe(500);
                Check(momentum.NextTraversal() >= second + 60, "low peak never reduces next start");
                Check(momentum.SpeedLimit > momentum.StartSpeed, "new traversal has acceleration headroom");
                momentum.Reset();
                Check(momentum.StartSpeed == 340 && momentum.PeakSpeed == 340, "new run resets carry");
            }
            var invalid = new RunMomentum(340, 980, float.NaN);
            invalid.Observe(float.PositiveInfinity); invalid.Observe(float.NaN);
            Check(float.IsFinite(invalid.NextTraversal()), "invalid input cannot poison run speed");
            GD.Print("MOMENTUM SMOKE PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
