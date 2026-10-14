using System;

/// <summary>Run-local momentum; cinematic zeroes never erase a completed traversal's peak.</summary>
public sealed class RunMomentum
{
    private readonly float _initialSpeed, _baseLimit, _divisor;
    public float StartSpeed { get; private set; }
    public float PeakSpeed { get; private set; }
    public float SpeedLimit => MathF.Max(_baseLimit, StartSpeed * _divisor + 120f);
    public RunMomentum(float initialSpeed, float baseLimit, float divisor)
    {
        _initialSpeed = float.IsFinite(initialSpeed) && initialSpeed > 0 ? initialSpeed : 340f;
        _baseLimit = float.IsFinite(baseLimit) && baseLimit > 0 ? baseLimit : 980f;
        _divisor = float.IsFinite(divisor) && divisor >= 1.1f ? divisor : 2.4f;
        Reset();
    }
    public void Observe(float speed)
    { if (float.IsFinite(speed)) PeakSpeed = MathF.Max(PeakSpeed, speed); }
    public float NextTraversal()
    {
        StartSpeed = MathF.Max(PeakSpeed / _divisor, StartSpeed + 60f);
        PeakSpeed = StartSpeed;
        return StartSpeed;
    }
    public void Reset() { StartSpeed = _initialSpeed; PeakSpeed = _initialSpeed; }
}
