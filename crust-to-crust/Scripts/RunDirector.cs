using System;

public enum EncounterKind { Slalom, Debris, CoinTrail, WaterApproach, Rest, Sentry, Cascade }
public enum CaveZone { Basalt, Crystal, Magma, Core }

/// <summary>Gameplay randomness is separate from rendering, wind, audio and frame count.</summary>
public sealed class RunDirector
{
    public int Seed { get; }
    private readonly Random _random;
    private readonly float _phase, _secondaryPhase;
    public RunDirector(int seed)
    {
        Seed = seed; _random = new Random(seed);
        _phase = Sample(0, 71) * MathF.PI * 2;
        _secondaryPhase = Sample(0, 97) * MathF.PI * 2;
    }
    private float Sample(int index, uint salt)
    {
        uint value = unchecked((uint)Seed ^ (uint)index * 0x9e3779b9u ^ salt);
        value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15;
        value *= 0x846ca68bu; value ^= value >> 16;
        return (value & 0xffffff) / 16777216f;
    }
    public EncounterKind EncounterAt(int index) => (index & 1) != 0 ? EncounterKind.Rest
        : (EncounterKind)(int)(Sample(index, 13) * 4);
    public float DurationAt(int index) => (index & 1) != 0 ? 1.4f + Sample(index, 29) : 5f + Sample(index, 29) * 3f;
    public EncounterKind EncounterForZone(int index,CaveZone zone)
    {
        EncounterKind choice = EncounterAt(index);
        if (choice == EncounterKind.Rest) return choice;
        if(choice==EncounterKind.Debris && Sample(index,83)<0.45f) return EncounterKind.Sentry;
        if(choice==EncounterKind.Debris && Sample(index,83)<0.8f) return EncounterKind.Cascade;
        if (zone == CaveZone.Crystal && choice == EncounterKind.Debris) return EncounterKind.Slalom;
        if ((zone == CaveZone.Magma || zone == CaveZone.Core) && choice == EncounterKind.CoinTrail && Sample(index,43) < 0.65f)
            return EncounterKind.WaterApproach;
        return choice;
    }
    // The escape route sweeps across the tunnel. At 1078 px/s it moves <176 px/s,
    // comfortably below the player's 380 px/s steering, with no discontinuities.
    public float LaneAt(float distance) => 130f * MathF.Sin(distance / 850f + _phase)
        + 15f * MathF.Sin(distance / 1700f + _secondaryPhase);
    public int NextInt(int min, int max) => _random.Next(min, max);
    public float NextFloat(float min, float max) => min + (max - min) * (float)_random.NextDouble();
    public static CaveZone ZoneAt(float depth,float coreDepth)
    {
        float distance = MathF.Abs(depth-coreDepth);
        return distance > 12000 ? CaveZone.Basalt : distance > 8000 ? CaveZone.Crystal : distance > 3000 ? CaveZone.Magma : CaveZone.Core;
    }
}
