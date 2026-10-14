using System;

/// <summary>Run-local charge. Only active illumination consumes it; there is no regeneration.</summary>
public sealed class LanternCharge
{
    public float Capacity { get; }
    public float Seconds { get; private set; }
    private readonly float _refill;
    public LanternCharge(int accumulator=0,int efficiency=0)
    {
        float extra=2*Math.Clamp(accumulator,0,3);
        Capacity=18+extra; Seconds=14+extra; _refill=5+0.5f*Math.Clamp(efficiency,0,3);
    }
    public void Tick(float delta,bool enabled)
    { if(enabled && float.IsFinite(delta) && delta>0) Seconds=MathF.Max(0,Seconds-delta); }
    public void Refill() => Seconds=MathF.Min(Capacity,Seconds+_refill);
}
