using System.Numerics;
using Fleetwright.Map.Flashes;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Fires;

/// <summary>Funnel smoke (docs/visuals/hits-and-fires.md I6, decision 6: folded in, light), on I5's machinery: each funnel of
/// each ship in the world view a continuous source at its top (<see cref="SmokeTrails"/>), coarse and merged early:
/// <list type="bullet">
/// <item>its output Ȧ ≈ <see cref="FunnelM2s"/> at full power for coal (research/vfx/unified-smoke-system.md "Calibrating
/// funnel smoke"), times the power share (v / v_max)³ (never under <see cref="IdleShare"/>: banked fires still smoke) and
/// the plant's smokiness by its fuel aboard (coal 1, oil <see cref="OilShare"/>, by the bunkers' tonnes: a mixed plant
/// between) [EST, gap 4];</item>
/// <item>grey-brown for coal, light grey for oil (placeholders), rising <see cref="RiseM"/> on its heat;</item>
/// <item>a ship going down makes none (her fires are out or drowned).</item>
/// </list>
/// A shared budget with the fires' trails: the funnels fill the smoke's book only to <see cref="Spare"/> of it, the fires
/// to more, so the fires come first when it's tight. Display only: no smoke blocks sight (decision 2). Allocates only for a
/// class's first sight.</summary>
public static class FunnelSmoke
{
    /// <summary>A funnel's smoke at full power for coal, m²/s; at least this share of it whatever the speed; oil's smokiness
    /// beside coal's; how far it rises on its heat, m, over how long, s; a puff every so often, s; and the share of the book
    /// it may fill to [EST].</summary>
    public static double FunnelM2s = 1500, IdleShare = 0.05, OilShare = 0.3, RiseM = 20, RiseS = 10, EveryS = 4, Spare = 0.6;

    /// <summary>The plume's core as it leaves the funnel's lee, m: it mixes into her wake's turbulence at once; a puff every
    /// so many metres she runs (coarse, the funnels being many: docs/visuals/hits-and-fires.md I8) [EST].</summary>
    public static double CoreM = 10, SpacingM = 20;

    /// <summary>Off: no funnel smoke.</summary>
    public static bool On = true;

    readonly record struct Plant(int[] Funnels, double Coal);

    static readonly Dictionary<ShipClass, Plant> plants = [];

    static Plant PlantOf(ShipClass c)
    {
        if (plants.TryGetValue(c, out var p))
            return p;
        var funnels = new List<int>();
        for (int i = 0; i < c.Components.Length; i++)
            if (c.Components[i].Kind == ComponentKind.Funnel)
                funnels.Add(i);
        double coal = 0, oil = 0;
        foreach (ref readonly var r in c.Rooms)
            if (double.IsFinite(r.Tonnes) && r.Tonnes > 0)
            {
                if (r.Fuel == Fuel.Coal)
                    coal += r.Tonnes;
                else if (r.Fuel == Fuel.Oil)
                    oil += r.Tonnes;
            }
        plants[c] = p = new Plant([.. funnels], coal + oil > 0 ? coal / (coal + oil) : 1);
        return p;
    }

    public static void Build(in MapView view, Snapshot s, long drawMs, double world, double now, Func<int, ShipClass?> classOf, SmokeBook book,
        SmokeTrails trails)
    {
        if (!On || world <= 0)
            return;
        double margin = 3000 / view.MetresPerPixel;
        foreach (var v in (ReadOnlySpan<ShipsView>)[s.Ships, s.Sighted])
            for (int k = 0; k < v.Count; k++)
            {
                if (v.Loss[k] != LossKind.None || classOf(v.Class[k]) is not { } c)
                    continue;
                var plant = PlantOf(c);
                if (plant.Funnels.Length == 0)
                    continue;
                var (p, f) = v.PoseAt(k, drawMs);
                if (!view.Project(p, out double px, out double py) || px < -margin || py < -margin || px > view.Width + margin || py > view.Height + margin)
                    continue;
                double vmax = Math.Max(c.Motion.MaxSpeedKn * 0.514444, 1), share = Math.Max(Math.Pow(Math.Abs(v.Speed[k]) / vmax, 3), IdleShare);
                double smoky = plant.Coal + (1 - plant.Coal) * OilShare;
                double rate = FunnelM2s * Math.Min(share, 1.2) * smoky;
                var tint = Vector3.Lerp(OilSmoke, CoalSmoke, (float)plant.Coal);
                var up = p.Normalized();
                var stbd = Vec3d.Cross(f, up).Normalized();
                double wl = c.Vertical.Waterline;
                foreach (int fn in plant.Funnels)
                {
                    var b = c.Components[fn].Bounds;
                    double z = b.Top - wl, w = Math.Max(Math.Min(b.X1 - b.X0, b.Y1 - b.Y0), 2);
                    var at = p + f * ((b.X0 + b.X1) / 2) + stbd * ((b.Y0 + b.Y1) / 2);
                    long key = 1L << 40 | (long)v.Entity[k] << 16 | (uint)fn;
                    int owner = (int)((uint)(v.Entity[k] * 16 + fn % 16) & 0xffffff) | 1 << 30;
                    trails.Emit(book, key, owner, now, SmokeTrails.Spacing(v.Speed[k], EveryS, SpacingM), at.Normalized() * (Earth.Radius + z), z, rate, 0, 1,
                        tint, Math.Max(w, CoreM), RiseM, RiseS,
                        (fn * 0.618f + v.Entity[k] * 0.137f) % 1, Spare);
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
            case "output": FunnelM2s = Math.Max(v, 0); break;
            case "idle": IdleShare = Math.Clamp(v, 0, 1); break;
            case "oil": OilShare = Math.Max(v, 0); break;
            case "rise": RiseM = Math.Max(v, 0); break;
            case "every": EveryS = Math.Max(v, 0.2); break;
            case "spare": Spare = Math.Clamp(v, 0, 1); break;
            case "on": On = v != 0; break;
            default: return false;
        }
        return true;
    }

    public const string Knobs = "output, idle, oil, rise, every, spare, on";
}
