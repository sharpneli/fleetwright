using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen nearmiss`: near misses alone (docs/battle/shells-armour-damage.md E7; damage/15), through the world's
/// damage system (<see cref="GivenHits.NearBurst"/>, <see cref="GivenHits.NearSalvos"/>). With --at, a burst placed short,
/// over or beside the target at each distance (15 §4's table: splinters through her side and into her upper works, the
/// waterline holes, the men hit); without, salvos with the fire control's error (15 §5's table, per salvo).</summary>
static class NearMissBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public record Options(string? Shooter, int Battery, string? Kind, string? Loadout, double RangeM, double AspectDeg, string? At, int N, int Salvos, int Guns,
        double SigmaRangeM, double SigmaDeflM, double FcRangeM, double FcDeflM, double WaterMs, int Seed, double SettleMin = 0);

    public static int Run(string target, Func<string, Design> load, string settingsPath, Options o)
    {
        var settings = SimSettings.Load(settingsPath);
        var tc = ShipClassCompiler.Compile(ShipDesign.Build(load(target)));
        var sc = o.Shooter == null || o.Shooter == target ? tc : ShipClassCompiler.Compile(ShipDesign.Build(load(o.Shooter)));
        var (shell, name) = HitsBench.ShellOf(sc, o.Battery, o.Kind, o.Loadout);
        if (!double.IsNaN(o.WaterMs))
            shell = shell with { FuzeWaterMs = o.WaterMs };
        var b = sc.Batteries[o.Battery];
        double maxElev = sc.Mounts[sc.Links(b.Mounts)[0]].MaxElevationDeg;
        double w = shell.FillerKg * shell.FillerRe;
        Console.WriteLine(string.Format(Inv, "{0} on {1}: {2} ({3:0.#} kg, filler {4:0.##} kg, {5:0.#} kg TNT; {6} fuze{7}, {8:0.##} ms on the water) at {9:0} m; " +
            "reach {10:0} m", sc.Name, tc.Name, name, shell.MassKg, shell.FillerKg, w, shell.Fuze, shell.Fuze == Fuze.Base ? $" {shell.FuzeDelayS:0.###} s" : "",
            shell.FuzeWaterMs, o.RangeM, NearMiss.ReachM(shell, settings)));
        var wl = tc.Solids.Waterline;
        Console.WriteLine(string.Format(Inv, "  {0}: waterline {1:0.0} to {2:0.0} m, beam {3:0.0} m, freeboard {4:0.0} m", tc.Name, wl[0].X, wl[^1].X, tc.Beam,
            -tc.Vertical.Waterline));
        var sw = Stopwatch.StartNew();
        if (o.At != null)
        {
            foreach (string part in o.At.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = part.Split(':');
                string where = bits[0];
                foreach (string d in bits[1].Split(','))
                {
                    double off = double.Parse(d, Inv);
                    var into = new double[tc.Components.Length];
                    var r = GivenHits.NearBurst(tc, shell, maxElev, o.RangeM, o.AspectDeg, where, off, o.N, (ulong)o.Seed, settings, into, o.SettleMin * 60);
                    double n = Math.Max(r.Near, 1);
                    Console.WriteLine(string.Format(Inv, "  {0,-6} {1,5:0.#} m (falling {2:0.0}°, {3:0} m/s, {4:0.0} cm down): through the side {5,6:0.0}, " +
                        "the parts above {6,5:0.0}; waterline holes {7,7:0.0} cm²; killed {8:0.00}, wounded {9:0.00}; parts harmed {10:0.00}", where, off,
                        r.FallDeg, r.StrikeMs, 100 * r.DepthM, r.SideThrough / n, r.UpperThrough / n, 1e4 * r.HoleM2 / n, r.Killed / n, r.Wounded / n,
                        r.Parts / n));
                    Console.WriteLine(string.Format(Inv, "           shock {0:0.000} (bands {1}); gear out {2:0.00}, blackouts {3:0.00}; to the sea {4:0.0} cm², " +
                        "between spaces {5:0.0} cm²", r.Nsf / n, string.Join("/", r.Bands.Select(x => (x / n).ToString("0.00", Inv))), r.Shocks / n,
                        r.Blackouts / n, 1e4 * r.ShockM2 / n, 1e4 * r.SprungM2 / n));
                    if (o.SettleMin > 0)
                        Console.WriteLine(string.Format(Inv, "           after {0:0} min: water {1:0} t, lost {2:0.0%}", o.SettleMin, r.WaterT / n, r.Lost / n));
                    // the parts above by kind (plated or not: an open one's are its hits)
                    var kinds = new SortedDictionary<string, double>(StringComparer.Ordinal);
                    for (int i = 0; i < into.Length; i++)
                        if (into[i] > 0)
                        {
                            var cp = tc.Components[i];
                            string key = (cp.Role == ComponentRole.Director ? "director" : cp.Kind.ToString().ToLowerInvariant()) +
                                (Effects.PartMild(tc, i, Effects.Armour(tc, i)) > 0 ? "" : " (open)");
                            kinds[key] = kinds.GetValueOrDefault(key) + into[i] / n;
                        }
                    if (kinds.Count > 0)
                        Console.WriteLine("           " + string.Join(", ", kinds.Where(kv => kv.Value >= 0.05).Select(kv => string.Format(Inv, "{0} {1:0.0}", kv.Key, kv.Value))));
                }
            }
        }
        else
        {
            int guns = o.Guns > 0 ? o.Guns : sc.Links(b.Mounts).Length * sc.TurretTypes[sc.Mounts[sc.Links(b.Mounts)[0]].Type].Barrels;
            double sr = !double.IsNaN(o.SigmaRangeM) ? o.SigmaRangeM : settings.DevFirePatternPct / 100 * o.RangeM;
            double sd = !double.IsNaN(o.SigmaDeflM) ? o.SigmaDeflM : sr * settings.DevFireDeflectionShare;
            var r = GivenHits.NearSalvos(tc, shell, maxElev, o.RangeM, o.AspectDeg, guns, o.Salvos, sr, sd, o.FcRangeM, o.FcDeflM, (ulong)o.Seed, settings);
            double n = Math.Max(r.Salvos, 1);
            Console.WriteLine(string.Format(Inv, "  {0} salvos of {1} (σ {2:0} m along, {3:0} m across; fire control σ {4:0} m, {5:0} m), falling {6:0.0}°, {7:0} m/s:",
                r.Salvos, guns, sr, sd, o.FcRangeM, o.FcDeflM, r.FallDeg, r.StrikeMs));
            Console.WriteLine(string.Format(Inv, "  a salvo: hits {0:0.00} (under water {1:0.00}); bursts in the water {2:0.00}, near misses {3:0.00} (within 10 m {4:0.00}, " +
                "30 m {5:0.00}; {6:0.0} cm down on average)", r.Hits / n, r.Underwater / n, r.Bursts / n, r.Near / n, r.Within10 / n, r.Within30 / n,
                100 * r.DepthM / Math.Max(r.Near, 1)));
            Console.WriteLine(string.Format(Inv, "  their splinters: through the side {0:0.0}, the parts above {1:0.0}; waterline holes {2:0} cm²; killed {3:0.00}, " +
                "wounded {4:0.00}; parts harmed {5:0.00}", r.SideThrough / n, r.UpperThrough / n, 1e4 * r.HoleM2 / n, r.Killed / n, r.Wounded / n, r.Parts / n));
            Console.WriteLine(string.Format(Inv, "  their shock: a salvo's near misses by band from 0.03/0.08/0.2/0.45/1.0 {0}; gear out {1:0.00}, blackouts {2:0.00}; " +
                "to the sea {3:0} cm², between spaces {4:0} cm²", string.Join("/", r.Bands.Select(x => (x / n).ToString("0.00", Inv))), r.Shocks / n,
                r.Blackouts / n, 1e4 * r.ShockM2 / n, 1e4 * r.SprungM2 / n));
            if (r.Hits > 0)
                Console.WriteLine(string.Format(Inv, "  a hit: through the side {0:0.0}, men hit {1:0.00}, waterline holes {2:0} cm²", r.SideThrough / r.Hits,
                    (r.Killed + r.Wounded) / r.Hits, 1e4 * r.HoleM2 / r.Hits));
        }
        Console.WriteLine(string.Format(Inv, "  {0:0.00} s", sw.Elapsed.TotalSeconds));
        return 0;
    }
}
