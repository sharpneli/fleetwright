using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>The lights at sea this moment (docs/battle/night.md Q2): a short list of sources that light what's near them,
/// each a place, an intensity (cd) and an owner, gathered from the ships off rails at each look: the ships' fires
/// (<see cref="ShipCondition.FireShowMW"/>, as lamps of <see cref="SimSettings.FireGlowCdPerMW"/>) and the searchlights'
/// beams exposed (Q4, <see cref="SearchlightSystem"/>: each with its axis's bearing, its blurred spread and its mirror);
/// star shell (Q5) joins it. Lights are few beside ships, so a ship lit asks every light (lights × ships, never ships ×
/// ships), and an eye asks only the beams. Pooled: allocates only to grow. Not world state: remade from it at each look.</summary>
public sealed class Lights
{
    Vec3d[] at = new Vec3d[16];
    double[] cd = new double[16];
    int[] owner = new int[16];
    // a beam's (Q4; NaN axis: a lamp all round): its axis's true bearing, its mean light's spread (degrees) and the share of
    // its peak that keeps (SearchlightModel.Held), its own beam's spread (full, degrees) and its mirror (m)
    double[] axis = new double[16], sigma = new double[16], keep = new double[16], spread = new double[16], mirror = new double[16];

    /// <summary>How many lights there are, and how many of them are beams.</summary>
    public int Count { get; private set; }
    public int Beams { get; private set; }

    /// <summary>Light i: where (its height in the place), how bright (a beam's peak), whose (slot).</summary>
    public Vec3d At(int i) => at[i];
    public double Candela(int i) => cd[i];
    public int Owner(int i) => owner[i];

    /// <summary>Light i's beam: its axis's true bearing (NaN: none, a lamp all round) and its full spread, degrees.</summary>
    public double AxisDeg(int i) => axis[i];
    public double SpreadDeg(int i) => spread[i];

    /// <summary>The lights gathered afresh from the ships off rails: each burning ship's fires, halfway up her silhouette
    /// (<see cref="SimSettings.FireGlowHeightShare"/>); then the beams, added by the searchlights.</summary>
    public void Gather(ShipStore store, SimSettings s)
    {
        (Count, Beams) = (0, 0);
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

    /// <summary>A beam exposed (Q4): its lamp's place, its peak (cd), whose (slot), its axis's true bearing and full spread
    /// (degrees), its pointing's error (degrees 1-sigma: none sweeping) and its mirror (m).</summary>
    public void AddBeam(Vec3d lamp, double candela, int slot, double axisDeg, double spreadDeg, double pointingDeg, double mirrorM)
    {
        int i = Add(lamp, candela, slot);
        var (sg, kp) = SearchlightModel.Held(spreadDeg, pointingDeg);
        (axis[i], sigma[i], keep[i], spread[i], mirror[i]) = (axisDeg, sg, kp, spreadDeg, mirrorM);
        Beams++;
    }

    int Add(Vec3d where, double candela, int slot)
    {
        if (Count == at.Length)
        {
            int n = 2 * Count;
            Array.Resize(ref at, n);
            Array.Resize(ref cd, n);
            Array.Resize(ref owner, n);
            Array.Resize(ref axis, n);
            Array.Resize(ref sigma, n);
            Array.Resize(ref keep, n);
            Array.Resize(ref spread, n);
            Array.Resize(ref mirror, n);
        }
        (at[Count], cd[Count], owner[Count], axis[Count]) = (where, candela, slot, double.NaN);
        return Count++;
    }

    /// <summary>The light light i sends toward a point, cd: all of it from a lamp all round, a beam's by its profile off
    /// its axis (<see cref="SearchlightModel.Toward"/>).</summary>
    double Sends(int i, Vec3d to) =>
        double.IsNaN(axis[i]) ? cd[i] : SearchlightModel.Toward(cd[i], Earth.Bearing(at[i], to) - axis[i], sigma[i], keep[i]);

    /// <summary>The light on a ship (slot) from every light but her own, through the haze (Allard's extinction, 3.912 /
    /// the visibility): the illuminance, lux, and the bearing it comes from (the lights' bearings weighed by their light);
    /// 0 lux when none reaches her.</summary>
    public (double Lux, double FromDeg) On(int slot, Vec3d position, double visibilityM)
    {
        double sigmaAir = double.IsPositiveInfinity(visibilityM) || visibilityM <= 0 ? 0 : 3.912 / visibilityM;
        double lux = 0, e = 0, n = 0;
        var east = Earth.East(position);
        var north = Earth.North(position);
        for (int i = 0; i < Count; i++)
        {
            if (owner[i] == slot)
                continue;
            double c = Sends(i, position);
            if (!(c > 0))
                continue;
            var r = at[i] - position;
            double d2 = Math.Max(r.LengthSquared, 1), d = Math.Sqrt(d2), l = c * Math.Exp(-sigmaAir * d) / d2;
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

    /// <summary>The veil over an eye's view of a target (Q4), cd/m²: the light of every beam she stands in scattered back
    /// down the eye's line where it runs inside the beam (<see cref="SearchlightModel.BackscatterCdM2"/>, the eye nearer
    /// the lamp than she is: its own ship's down the whole beam, a consort's from where its line meets the beam near her),
    /// and every other ship's beam on the eye as glare (<see cref="SearchlightModel.GlareCdM2"/>: the lux at the eye, by
    /// the angle between the lamp and the target). 0 with no beam out.</summary>
    public double VeilCdM2(int observerSlot, Vec3d eye, Vec3d target, double visibilityM, SimSettings s)
    {
        if (Beams == 0)
            return 0;
        double veil = 0, look = Earth.Bearing(eye, target);
        for (int i = 0; i < Count; i++)
        {
            if (double.IsNaN(axis[i]))
                continue;
            double offset = (at[i] - eye).Length, d = (target - at[i]).Length;
            if (offset < d)
                veil += SearchlightModel.BackscatterCdM2(cd[i], spread[i], mirror[i], Earth.Bearing(at[i], target) - axis[i], sigma[i], keep[i], offset, d,
                    visibilityM, s);
            if (owner[i] != observerSlot)
            {
                double lux = SearchlightModel.LuxAt(SearchlightModel.LampCd(cd[i], Earth.Bearing(at[i], eye) - axis[i], sigma[i], keep[i], s), offset, visibilityM);
                veil += SearchlightModel.GlareCdM2(lux, Earth.Bearing(eye, at[i]) - look, s);
            }
        }
        return veil;
    }

    /// <summary>Whether a ship (slot) has a beam out.</summary>
    public bool HasBeam(int slot)
    {
        for (int i = 0; i < Count && Beams > 0; i++)
            if (owner[i] == slot && !double.IsNaN(axis[i]))
                return true;
        return false;
    }

    /// <summary>A ship's own lamps (slot) as an eye at a point sees them (Q4): the light toward it of those it's in the beam
    /// of or forward of (<see cref="SearchlightModel.LampCd"/>: a point to aim at), and of the beams seen from beside
    /// (<see cref="SearchlightModel.ColumnCd"/>: a streak, her bearing), cd, and the highest lamp's height over the sea
    /// (m, for the horizon); all 0 with none out.</summary>
    public (double LampCd, double ColumnCd, double HeightM) LampsOf(int slot, Vec3d eye, double visibilityM, SimSettings s)
    {
        if (Beams == 0)
            return (0, 0, 0);
        double lamp = 0, column = 0, h = 0;
        for (int i = 0; i < Count; i++)
        {
            if (owner[i] != slot || double.IsNaN(axis[i]))
                continue;
            lamp += SearchlightModel.LampCd(cd[i], Earth.Bearing(at[i], eye) - axis[i], sigma[i], keep[i], s);
            column += SearchlightModel.ColumnCd(cd[i], spread[i], visibilityM, s);
            h = Math.Max(h, at[i].Length - Earth.Radius);
        }
        return (lamp, column, h);
    }
}
