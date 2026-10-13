using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen duel</c>: the fire-control chain's bench (docs/battle/fighting.md 4.2b, "The instruments"): one
/// ship's battery engages another, headless, run after run with other seeds, as fire_control_ref.py's <c>run()</c>
/// does. The shooter steams north; the target starts at a bearing and range from it, on a course relative to north,
/// and both hold course and speed. Hits do nothing yet (4.3a). It reports the hit and straddle rates, how soon the
/// first straddle and hit came after the track started, and what the gunnery cost the sim thread.</summary>
static class Duel
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public record Options(double RangeM, int Level, int Runs, double Minutes, double OwnKn, double TargetKn, double BearingDeg, double TargetCourseDeg,
        CrewQuality Crew, int Seed, int Battery, bool Trace = false);

    public readonly record struct Result(int Shells, int Hits, int Salvos, int Straddles, double FirstHitS, double FirstStraddleS, double GunneryMs,
        double ShellsMs);

    /// <summary>Runs a duel once on a fresh world: the battery's tally at the end.</summary>
    public static Result Once(ShipClass shooter, ShipClass target, SimSettings settings, Options o, int seed)
    {
        var start = WorldTime.FromUtc(1916, 5, 31, 14);
        var w = new World(start, (ulong)seed, settings);
        int a = w.Ships.AddClass(shooter), b = w.Ships.AddClass(target);
        const double lat = 56.0, lon = 4.0;
        var p = Earth.Position(lat, lon);
        var f = Earth.Forward(p, o.BearingDeg);
        Earth.Advance(ref p, ref f, o.RangeM);
        var (tl, to) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(a, lat, lon, 0, o.OwnKn, side: 0));
        w.Apply(ShipSystem.Spawn(b, tl, to, o.TargetCourseDeg, o.TargetKn, side: 1));
        if (o.Crew != CrewQuality.Trained)
            w.Apply(GunnerySystem.SetCrew(0, o.Crew));
        w.Apply(GunnerySystem.Engage(1, o.Battery, 2, o.Level));
        if (o.Trace)
            w.Gunnery.Reported = r => Console.WriteLine(string.Format(Inv,
                "    {0,5:0} s  R {1,6:0}  n {2,2}  fall {3,6:+0;-0} m {4,5:+0;-0} m  aim {5,6:+0;-0} {6,5:+0;-0}  corr {7,6:+0;-0}  hits {8}{9}",
                (r.FiredMs - start.Ms) / 1000.0, r.RangeM, r.Shells, r.MeanX, r.MeanY, r.AimErrM, r.AimErrAcrossM, r.CorrR, r.Hits, r.Straddle ? "  STRADDLE" : ""));
        w.AdvanceTo(start + (long)(o.Minutes * 60_000));
        var t = w.Gunnery.TallyOf(1, o.Battery);
        if (t is not { } x)
            return default;
        double ProbeMs(string name) => Enumerable.Range(0, w.Probes.Count).Where(i => w.Probes.Names[i] == name).Sum(i => Probes.Ms(w.Probes.Ticks[i]));
        return new Result(x.Shells, x.Hits, x.Salvos, x.Straddles, x.FirstHitMs < 0 ? double.NaN : (x.FirstHitMs - x.TrackMs) / 1000.0,
            x.FirstStraddleMs < 0 ? double.NaN : (x.FirstStraddleMs - x.TrackMs) / 1000.0, ProbeMs(nameof(GunnerySystem)), ProbeMs(nameof(ShellSystem)));
    }

    public static int Run(string shooterName, string targetName, Func<string, Design> load, string settingsPath, Options o)
    {
        var settings = SimSettings.Load(settingsPath);
        var shooter = ShipClassCompiler.Compile(ShipDesign.Build(load(shooterName)));
        var target = targetName == shooterName ? shooter : ShipClassCompiler.Compile(ShipDesign.Build(load(targetName)));
        var row = shooter.Batteries[o.Battery];
        int level = o.Level >= 0 ? o.Level : row.Local ? 0 : (int)row.Level;
        Console.WriteLine(string.Format(Inv, "{0} battery {1} ({2:0.#}/{3:0.#}, {4} guns, level {5}{6}) at {7} ({8:0} m, {9:0.0} kyd), bearing {10:0}°, " +
            "target on {11:0}° at {12:0} kn, own 0° at {13:0} kn; {14} crew; {15} runs of {16:0} min",
            shooter, o.Battery, row.CalibreMm, row.CalibreLength, row.Guns, level, row.Local ? ", local control" : "", target, o.RangeM, o.RangeM / 914.4,
            o.BearingDeg, o.TargetCourseDeg, o.TargetKn, o.OwnKn, o.Crew.ToString().ToLowerInvariant(), o.Runs, o.Minutes));
        int shells = 0, hits = 0, salvos = 0, straddles = 0, anyHit = 0;
        double gunneryMs = 0, shellsMs = 0;
        var firstHit = new List<double>();
        var firstStraddle = new List<double>();
        var sw = Stopwatch.StartNew();
        for (int k = 0; k < o.Runs; k++)
        {
            var r = Once(shooter, target, settings, o, o.Seed + k);
            (shells, hits, salvos, straddles) = (shells + r.Shells, hits + r.Hits, salvos + r.Salvos, straddles + r.Straddles);
            (gunneryMs, shellsMs) = (gunneryMs + r.GunneryMs, shellsMs + r.ShellsMs);
            if (!double.IsNaN(r.FirstHitS))
            {
                anyHit++;
                firstHit.Add(r.FirstHitS);
            }
            if (!double.IsNaN(r.FirstStraddleS))
                firstStraddle.Add(r.FirstStraddleS);
        }
        double secs = sw.Elapsed.TotalSeconds;
        static string Median(List<double> v) => v.Count == 0 ? "-" : v.Order().ElementAt(v.Count / 2).ToString("0", Inv);
        Console.WriteLine(string.Format(Inv, "  hits {0:0.0}% ({1} of {2} shells), straddles {3:0}% of {4} salvos; first straddle {5} s, first hit {6} s " +
            "after the track (medians); a hit in {7} of {8} runs", 100.0 * hits / Math.Max(shells, 1), hits, shells, 100.0 * straddles / Math.Max(salvos, 1),
            salvos, Median(firstStraddle), Median(firstHit), anyHit, o.Runs));
        double batterySeconds = o.Runs * o.Minutes * 60;
        Console.WriteLine(string.Format(Inv, "  {0:0.00} s for the runs, {1:0} ms a run; the battery's fire control {2:0.0} µs a game second, its shells " +
            "{3:0.0} µs a shell", secs, secs * 1000 / Math.Max(o.Runs, 1), gunneryMs * 1000 / batterySeconds, shellsMs * 1000 / Math.Max(shells, 1)));
        return 0;
    }
}
