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
        string fit = c.Fit >= 0 ? $"preset {c.Fit} ({FireControlPresets.Name(c.Fit)})" : row.Local ? "local control" : $"its own fit, level {(int)row.Level}";
        Console.WriteLine(string.Format(Inv, "{0} battery {1} ({2:0.#}/{3:0.#}, {4} guns, {5}) at {6} ({7:0} m, {8:0.0} kyd), bearing {9:0}°, " +
            "target on {10:0}° at {11:0} kn, {12}; own 0° at {13:0} kn, {14}; {15} crew, fire discipline {21}; {16}{17}{18}; {19} runs of {20:0} min",
            c.Shooter, c.Battery, row.CalibreMm, row.CalibreLength, row.Guns, fit, c.Target, c.RangeM, c.RangeM / FcBench.Kyd, c.BearingDeg,
            c.CourseDeg, c.TargetKn, c.Behaviour, c.OwnKn, c.Own, c.Crew.ToString().ToLowerInvariant(),
            c.VisibilityM > 0 ? string.Format(Inv, "visibility {0:0.#} km", c.VisibilityM / 1000) : "clear",
            c.Start.Ms == FcCase.Day.Ms ? "" : $", from {c.Start}", double.IsNaN(c.Combat) ? "" : string.Format(Inv, ", combat factor {0:0.##}", c.Combat),
            runs, c.Minutes, c.Discipline ?? settings.FireDisciplines[0].Name));
        if (trace)
        {
            var one = FcBench.Once(c, settings, seed);
            long t0 = c.Start.Ms;
            foreach (var r in one.Salvos.OrderBy(s => s.FiredMs))
            {
                // its spot, and the bracket after it (fire-control.md F6)
                string spot = one.Spots.FirstOrDefault(p => p.FiredMs == r.FiredMs) is { FiredMs: > 0 } p
                    ? !p.Seen ? "  not seen" : string.Format(Inv, "  {0,-8} [{1}, {2}] next {3:+0;-0}{4}", p.Sign.ToString().ToLowerInvariant(), Bound(p.Lo),
                        Bound(p.Hi), p.Next, p.Phase != r.Phase ? $" -> {Phase(p.Phase)}" : "")
                    : "";
                Console.WriteLine(string.Format(Inv,
                    "    {0,5:0} s  {1}  R {2,6:0}  n {3,2}  fall {4,6:+0;-0} m {5,5:+0;-0} m  aim {6,6:+0;-0} {7,5:+0;-0}  corr {8,6:+0;-0}{12}  hits {9}{10}{11}",
                    (r.FiredMs - t0) / 1000.0, r.Phase == FirePhase.Effect ? "E" : "R", r.RangeM, r.Shells, r.MeanX, r.MeanY, r.AimErrM, r.AimErrAcrossM, r.CorrR,
                    r.Hits, r.Straddle ? "  STRADDLE" : "", spot, r.LadderM != 0 ? string.Format(Inv, " (step {0:+0;-0})", r.LadderM) : ""));
            }
            foreach (long m in one.Manoeuvres)
                Console.WriteLine(string.Format(Inv, "    {0,5:0} s  the target manoeuvres", (m - t0) / 1000.0));
        }
        var sw = Stopwatch.StartNew();
        var all = FcBench.Runs([c], settings, runs, seed)[0];
        double secs = sw.Elapsed.TotalSeconds;
        var mount = c.Shooter.Mounts[c.Shooter.Links(row.Mounts)[0]];
        var st = FcStats.Of(all, c.Minutes, row.Guns, mount.ReloadS);
        foreach (var line in st.Lines())
            Console.WriteLine(line);
        Console.WriteLine(string.Format(Inv, "  {0:0.00} s for the runs, {1:0} ms a run (on threads); the battery's fire control {2:0.0} µs a game second",
            secs, secs * 1000 / Math.Max(runs, 1), st.GunneryMsPerS * 1000));
        return 0;
    }

    static string Bound(double m) => double.IsInfinity(m) ? (m > 0 ? "+inf" : "-inf") : m.ToString("+0;-0", Inv);

    static string Phase(FirePhase p) => p == FirePhase.Effect ? "fire for effect" : p == FirePhase.Ranging ? "ranging" : "opening";

    /// <summary>A fit from the command line: a preset's number or name (director, judgement, argo, ...), or "design" for
    /// the battery's own.</summary>
    public static int ParseFit(string? s)
    {
        if (s is null or "design" or "-1")
            return -1;
        if (int.TryParse(s, out int n))
            return Math.Clamp(n, 0, FireControlPresets.Count - 1);
        int p = FireControlPresets.Find(s);
        if (p < 0)
            throw new ArgumentException($"no preset '{s}': " + string.Join(", ", Enumerable.Range(0, FireControlPresets.Count).Select(FireControlPresets.Name)));
        return p;
    }
}
