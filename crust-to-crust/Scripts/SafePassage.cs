using System;

/// <summary>Shared geometric constraints for every pooled moving hazard.</summary>
public static class SafePassage
{
    // A 64px square fits inside this circle at any rotation (sqrt(32²+32²)<46).
    public const float PlayerRadius = 46f;
    public const float HalfWidth = 24f;
    public const float Amplitude = 60f;
    public const float Wavelength = 1400f;
    public const float Clearance = 14f;
    public const float WallMargin = 8f;
    public const float MaxSpawnScale = 1.12f;
    public const float AppearOvershoot = 1.15f;

    public static float Center(float depth,float screenY) => Amplitude*MathF.Sin((depth+screenY)/Wavelength);
    public static float ColliderRadius(int variant) => variant == 1 ? MathF.Sqrt(13f*13f+36f*36f)
        : variant == 4 ? MathF.Sqrt(12f*12f+33f*33f)
        : variant == 2 ? 49f : variant == 3 ? 37f : variant == 5 ? 24f : 29f;

    public static bool TryBounds(int side,float lane,float radius,float left,float right,out float min,out float max)
    {
        float gap = HalfWidth+PlayerRadius+radius+Clearance;
        min = side < 0 ? left+radius+WallMargin : lane+gap;
        max = side < 0 ? lane-gap : right-radius-WallMargin;
        return min <= max;
    }
    public static float Clamp(float value,float min,float max) => MathF.Max(min,MathF.Min(max,value));
}
