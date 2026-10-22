using System.Numerics;
using Fleetwright.Map.Flashes;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Fires;

/// <summary>Funnel smoke (docs/visuals/hits-and-fires.md I6, decision 6: folded in, light), on I5's machinery: each funnel of
/// each ship in the world view a continuous source (<see cref="SmokeTrails"/>), coarse and merged early. What it makes is
/// the smoke field's own source (docs/battle/smoke.md Z0, <see cref="SmokeSources.Funnel"/>: one implementation): the fuel
/// burnt at her speed times its smoke a kg, the funnel's share; rising by Briggs on its heat in the wind she makes, from
/// where the downwash leaves it. Grey-brown for coal, light grey otherwise (placeholders); a ship going down makes none.
/// A shared budget with the fires' trails: the funnels fill the smoke's book only to <see cref="Spare"/> of it, the fires
/// to more, so the fires come first when it's tight. Display only: no smoke blocks sight (decision 2) until the smoke
/// field is taken in. Allocates nothing.</summary>
public static class FunnelSmoke
{
    /// <summary>The look's gain on the field's output (1: as made); a puff every so often, s; and the share of the book it
    /// may fill to.</summary>
    public static double Gain = 1, EveryS = 4, Spare = 0.6;

    /// <summary>The plume's core as it leaves the funnel's lee, m, and a puff every so many metres she runs: close enough
    /// that the short plume reads as one dense ribbon.</summary>
    public static double CoreM = 8, SpacingM = 8;

    /// <summary>A puff's stylised life, s: whole for 0.4 of it, gone by its end (the user, 2026-10-19: "The smoke can look
    /// dense but it should end quick. It is unrealistic but this is just a stylistic thing": no smoke field in the first
    /// game). At 20 kn the plume is ~80 m of dense smoke thinning out over ~120 m more.</summary>
    public static double LifeS = 8;

    /// <summary>Off: no funnel smoke.</summary>
    public static bool On = true;

    public static void Build(in MapView view, Snapshot s, long drawMs, double world, double now, Func<int, ShipClass?> classOf, SmokeBook book,
        SmokeTrails trails, SimSettings settings)
    {
        if (!On || world <= 0)
            return;
        double margin = 3000 / view.MetresPerPixel;
        var air = SmokeAir.Of(s.Weather);
        foreach (var v in (ReadOnlySpan<ShipsView>)[s.Ships, s.Sighted])
            for (int k = 0; k < v.Count; k++)
            {
                if (v.Loss[k] != LossKind.None || classOf(v.Class[k]) is not { } c || c.Funnels.Length == 0)
                    continue;
                var (p, f) = v.PoseAt(k, drawMs);
                if (!view.Project(p, out double px, out double py) || px < -margin || py < -margin || px > view.Width + margin || py > view.Height + margin)
                    continue;
                var tint = c.Smoke.Fuel == Fuel.Coal ? CoalSmoke : OilSmoke;
                var up = p.Normalized();
                var stbd = Vec3d.Cross(f, up).Normalized();
                var overDeck = Earth.Forward(p, air.WindToDeg) * air.WindMs - f * v.Speed[k];
                double relative = overDeck.Length;
                double offBow = relative > 1e-6 ? Math.Acos(Math.Clamp(Vec3d.Dot(-overDeck * (1 / relative), f), -1, 1)) * 180 / Math.PI : 0;
                for (int fn = 0; fn < c.Funnels.Length; fn++)
                {
                    var plume = SmokeSources.Funnel(c, fn, v.Speed[k] / 0.514444, relative, offBow, air, settings);
                    if (!(plume.RateM2s > 0))
                        continue;
                    var b = c.Components[c.Funnels[fn].Component].Bounds;
                    double w = Math.Max(Math.Min(b.X1 - b.X0, b.Y1 - b.Y0), 2);
                    var at = p + f * ((b.X0 + b.X1) / 2) + stbd * ((b.Y0 + b.Y1) / 2);
                    long key = 1L << 40 | (long)v.Entity[k] << 16 | (uint)fn;
                    int owner = (int)((uint)(v.Entity[k] * 16 + fn % 16) & 0xffffff) | 1 << 30;
                    trails.Emit(book, key, owner, now, SmokeTrails.Spacing(v.Speed[k], EveryS, SpacingM), at.Normalized() * (Earth.Radius + plume.BaseM),
                        f * v.Speed[k], plume.BaseM, plume.RateM2s * v.SmokeGain[k] * Gain, 0, 1, tint, Math.Max(w, CoreM), plume.RiseM, plume.RiseS,
                        (fn * 0.618f + v.Entity[k] * 0.137f) % 1, Spare, LifeS);
                }
            }
    }

    /// <summary>Funnel smoke's tints, linear [EST, gap 4]: coal's grey-brown, oil's light grey.</summary>
    static readonly Vector3 CoalSmoke = new(0.27f, 0.25f, 0.23f), OilSmoke = new(0.58f, 0.58f, 0.58f);

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], System.Globalization.CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "gain": Gain = Math.Max(v, 0); break;
            case "every": EveryS = Math.Max(v, 0.2); break;
            case "life": LifeS = Math.Max(v, 0); break;
            case "spare": Spare = Math.Clamp(v, 0, 1); break;
            case "on": On = v != 0; break;
            default: return false;
        }
        return true;
    }

    public const string Knobs = "gain, every, life, spare, on";
}
