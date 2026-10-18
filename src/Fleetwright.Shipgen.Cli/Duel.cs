using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen duel</c>: the fire-control chain's bench (docs/battle/fighting.md 4.2b, fire-control.md F1): one
/// ship's battery engages another, headless, run after run with other seeds, as fire_control_ref.py's <c>run()</c>
/// does. The shooter steams north; the target starts at a bearing and range from it, on a course relative to north;
/// each keeps to its policy (steady, zigzag, circle, chase the splashes, a turn, crippled: <see cref="Policy"/>), in the
/// weather and light asked for, at battle's or practice's errors. Hits do nothing unless asked (4.3a). It reports the
/// hit and straddle rates, how soon the first straddle and hit came, the track's aim error as it settled and after the
/// target's manoeuvres, and what the gunnery cost the sim thread.</summary>
static class Duel
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(FcCase c, SimSettings settings, int runs, int seed, bool trace)
    {
        var row = c.Shooter.Batteries[c.Battery];
        string fit = c.Fit >= 0 ? $"preset {c.Fit} ({(FireControlLevel)c.Fit})" : row.Local ? "local control" : $"its own fit, level {(int)row.Level}";
        Console.WriteLine(string.Format(Inv, "{0} battery {1} ({2:0.#}/{3:0.#}, {4} guns, {5}) at {6} ({7:0} m, {8:0.0} kyd), bearing {9:0}°, " +
            "target on {10:0}° at {11:0} kn, {12}; own 0° at {13:0} kn, {14}; {15} crew; {16}{17}{18}; {19} runs of {20:0} min",
            c.Shooter, c.Battery, row.CalibreMm, row.CalibreLength, row.Guns, fit, c.Target, c.RangeM, c.RangeM / FcBench.Kyd, c.BearingDeg,
            c.CourseDeg, c.TargetKn, c.Behaviour, c.OwnKn, c.Own, c.Crew.ToString().ToLowerInvariant(),
            c.VisibilityM > 0 ? string.Format(Inv, "visibility {0:0.#} km", c.VisibilityM / 1000) : "clear",
            c.Start.Ms == FcCase.Day.Ms ? "" : $", from {c.Start}", double.IsNaN(c.Combat) ? "" : string.Format(Inv, ", combat factor {0:0.##}", c.Combat),
            runs, c.Minutes));
        if (trace)
        {
            var one = FcBench.Once(c, settings, seed);
            long t0 = c.Start.Ms;
            foreach (var r in one.Salvos.OrderBy(s => s.FiredMs))
                Console.WriteLine(string.Format(Inv,
                    "    {0,5:0} s  R {1,6:0}  n {2,2}  fall {3,6:+0;-0} m {4,5:+0;-0} m  aim {5,6:+0;-0} {6,5:+0;-0}  corr {7,6:+0;-0}  hits {8}{9}",
                    (r.FiredMs - t0) / 1000.0, r.RangeM, r.Shells, r.MeanX, r.MeanY, r.AimErrM, r.AimErrAcrossM, r.CorrR, r.Hits, r.Straddle ? "  STRADDLE" : ""));
            foreach (long m in one.Manoeuvres)
                Console.WriteLine(string.Format(Inv, "    {0,5:0} s  the target manoeuvres", (m - t0) / 1000.0));
        }
        var sw = Stopwatch.StartNew();
        var all = FcBench.Runs([c], settings, runs, seed)[0];
        double secs = sw.Elapsed.TotalSeconds;
        var st = FcStats.Of(all, c.Minutes);
        foreach (var line in st.Lines())
            Console.WriteLine(line);
        Console.WriteLine(string.Format(Inv, "  {0:0.00} s for the runs, {1:0} ms a run (on threads); the battery's fire control {2:0.0} µs a game second",
            secs, secs * 1000 / Math.Max(runs, 1), st.GunneryMsPerS * 1000));
        return 0;
    }

    /// <summary>A fit from the command line: a preset's number (0-8) or name (Director, Table, ...), or "design" for the
    /// battery's own.</summary>
    public static int ParseFit(string? s)
    {
        if (s is null or "design" or "-1")
            return -1;
        if (int.TryParse(s, out int n))
            return Math.Clamp(n, 0, FireControlPresets.Count - 1);
        return (int)Enum.Parse<FireControlLevel>(s, ignoreCase: true);
    }
}
