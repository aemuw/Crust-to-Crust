using System;

/// <summary>Run-local charge. Only active illumination consumes it; there is no regeneration.</summary>
public sealed class LanternCharge
{
    public float Capacity => 18f;
    public float Seconds { get; private set; } = 14f;
    public void Tick(float delta,bool enabled)
    { if(enabled && float.IsFinite(delta) && delta>0) Seconds=MathF.Max(0,Seconds-delta); }
    public void Refill() => Seconds=MathF.Min(Capacity,Seconds+5f);
}
