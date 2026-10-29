using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>The lights at sea this moment (docs/battle/night.md Q2): a short list of sources that light what's near them,
/// each a place, an intensity (cd) and an owner, gathered from the ships off rails at each look. Today the ships' fires
/// (<see cref="ShipCondition.FireShowMW"/>, as lamps of <see cref="SimSettings.FireGlowCdPerMW"/>); the searchlights
/// (Q4) and star shell (Q5) join it, with their beams. Lights are few beside ships, so a ship lit asks every light (lights
/// × ships, never ships × ships). Pooled: allocates only to grow. Not world state: remade from it at each look.</summary>
public sealed class Lights
{
    Vec3d[] at = new Vec3d[16];
    double[] cd = new double[16];
    int[] owner = new int[16];

    /// <summary>How many lights there are.</summary>
    public int Count { get; private set; }

    /// <summary>Light i: where (its height in the place), how bright, whose (slot).</summary>
    public Vec3d At(int i) => at[i];
    public double Candela(int i) => cd[i];
    public int Owner(int i) => owner[i];

    /// <summary>The lights gathered afresh from the ships off rails: each burning ship's fires, halfway up her silhouette
    /// (<see cref="SimSettings.FireGlowHeightShare"/>).</summary>
    public void Gather(ShipStore store, SimSettings s)
    {
        Count = 0;
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int slot = store.OffRailsSlot(k);
            double mw = store.Combat.Condition(slot).FireShowMW;
            if (mw <= 0)
                continue;
            var ship = store[slot];
            var p = ship.Position;
            Add(p + p.Normalized() * (s.FireGlowHeightShare * ship.Handling.Class.Silhouette.TopM), s.FireGlowCdPerMW * mw, slot);
        }
    }

    void Add(Vec3d where, double candela, int slot)
    {
        if (Count == at.Length)
        {
            Array.Resize(ref at, 2 * Count);
            Array.Resize(ref cd, 2 * Count);
            Array.Resize(ref owner, 2 * Count);
        }
        (at[Count], cd[Count], owner[Count]) = (where, candela, slot);
        Count++;
    }

    /// <summary>The light on a ship (slot) from every light but her own, through the haze (Allard's extinction, 3.912 /
    /// the visibility): the illuminance, lux, and the bearing it comes from (the lights' bearings weighed by their light);
    /// 0 lux when none reaches her.</summary>
    public (double Lux, double FromDeg) On(int slot, Vec3d position, double visibilityM)
    {
        double sigma = double.IsPositiveInfinity(visibilityM) || visibilityM <= 0 ? 0 : 3.912 / visibilityM;
        double lux = 0, e = 0, n = 0;
        var east = Earth.East(position);
        var north = Earth.North(position);
        for (int i = 0; i < Count; i++)
        {
            if (owner[i] == slot)
                continue;
            var r = at[i] - position;
            double d2 = Math.Max(r.LengthSquared, 1), d = Math.Sqrt(d2), l = cd[i] * Math.Exp(-sigma * d) / d2;
            lux += l;
            e += l * Vec3d.Dot(r, east) / d;
            n += l * Vec3d.Dot(r, north) / d;
        }
        return (lux, lux > 0 ? Math.Atan2(e, n) * 180 / Math.PI : 0);
    }

    /// <summary>The share of the light from a bearing on a ship that falls on her side toward an eye on another bearing
    /// (both from her): all of it with the light behind the eye, half side on, none from beyond her (a hull's many faces
    /// lit as a diffuse body's: (1 + cos Δ) / 2).</summary>
    public static double Facing(double lightFromDeg, double eyeFromDeg) =>
        (1 + Math.Cos((eyeFromDeg - lightFromDeg) * Math.PI / 180)) / 2;
}
