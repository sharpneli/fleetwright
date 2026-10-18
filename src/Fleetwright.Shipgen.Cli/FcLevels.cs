using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>The fire-control mechanisms alone (docs/battle/fire-control.md F1), the research's tables on the sim's
/// presets (fire-control-research.md; fire_control_ref.py's tables 5, 12, 6 and 11): <c>shipgen fc-converge</c>, how
/// far each computer's prediction is off over a time of flight as it settles and after the target turns or circles,
/// and <c>shipgen fc-levels</c>, each preset's hits in the levels' table's conditions (day, long range, haze, night) and against a manoeuvring target. With
/// <c>--ref</c> the reference's own tables follow. They check that each machine fails where it failed; their hit rates
/// are not targets (the plan's principles).</summary>
static class FcLevels
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const double Tof = 25;
    static readonly int[] Checks = [30, 60, 120, 240, 330, 360, 420, 540];

    static string Name(int level) => $"{level} {(FireControlLevel)level}";

    /// <summary>Table 5 (a crossing target turning 45° at 300 s) and table 12 (circling at 1°/s from 300 s): the
    /// predicted position's error over 25 s, RMS yd, and the error in the target's motion as the computer holds it, kn.</summary>
    public static int Converge(SimSettings settings, int runs, string? refRoot, Func<string, ShipClass> cls)
    {
        var qe = cls("super_dreadnought");
        var target = cls("lion");   // a 25 kn target; Queen Elizabeth makes 24
        FcCase Case(int level, Policy p) => new()
        {
            Name = Name(level), Shooter = qe, Target = target, Fit = level, RangeM = 15 * FcBench.Kyd, BearingDeg = 90, CourseDeg = 180,
            OwnKn = 20, TargetKn = 25, Behaviour = p, Minutes = 11, VisibilityM = 40_000,
        };
        Console.WriteLine(string.Format(Inv, "Table 5: the computer's predicted-position error over a {0:0} s time of flight, RMS yd ({1} runs a row)", Tof, runs));
        Console.WriteLine("Queen Elizabeth at 20 kn, a battlecruiser crossing at 25 kn at 15 kyd (opposite courses, abeam); it turns 45° at 300 s after");
        Console.WriteLine("the track starts (the helm put over then: the sim's ship turns as a ship does, the reference's at once). Rangefinder bias in.");
        var straight = Enumerable.Range(0, FireControlPresets.Count).Select(l => Probe(Case(l, Policy.Turn(45, 300)), settings, runs)).ToArray();
        string hdr = $"{"preset",-24}{"30s",6}{"60s",6}{"120s",6}{"240s",6} |{"+30",6}{"+60",6}{"+120",6}{"+240",6}";
        foreach (var (block, fmt, unit) in new[] { (0, "0", "the miss, yd"), (1, "0.0", "the target's motion as held, error in kn") })
        {
            Console.WriteLine($"  {unit}");
            Console.WriteLine("  " + hdr);
            for (int l = 0; l < straight.Length; l++)
                Console.WriteLine("  " + $"{Name(l),-24}" + string.Concat(Checks.Select((_, k) => (k == 4 ? " |" : "") + $"{FcBench.F(straight[l][block][k], fmt),6}")));
        }
        Console.WriteLine("\nTable 12: the same against a steady circle at 1°/s from 300 s, RMS yd");
        Console.WriteLine($"  {"preset",-24}{"straight 240s",14}{"+30",7}{"+60",7}{"+120",7}{"+240",7}");
        for (int l = 3; l < FireControlPresets.Count; l++)
        {
            var e = Probe(Case(l, Policy.Circle(1, 300)), settings, runs)[0];
            Console.WriteLine($"  {Name(l),-24}{FcBench.F(e[3]),14}" + string.Concat(new[] { 4, 5, 6, 7 }.Select(k => $"{FcBench.F(e[k]),7}")));
        }
        if (refRoot is not null)
        {
            FcReference.Run(refRoot, "converge", runs);
            FcReference.Run(refRoot, "circle", runs);
        }
        return 0;
    }

    /// <summary>A case's runs probed each second: at each check after the track starts, where the track puts the
    /// target a time of flight on (compared with where it went) and the velocity it holds (against the truth's).
    /// [0] the misses, yd; [1] the motion's errors, kn; RMS by check.</summary>
    static double[][] Probe(FcCase c, SimSettings settings, int runs)
    {
        var miss = Checks.Select(_ => new List<double>()).ToArray();
        var rate = Checks.Select(_ => new List<double>()).ToArray();
        object gate = new();
        Parallel.For(0, runs, j =>
        {
            var due = new List<(long Ms, Vec3d Pred, int Check)>();
            var m = Checks.Select(_ => new List<double>()).ToArray();
            var v = Checks.Select(_ => new List<double>()).ToArray();
            FcBench.Once(c, settings, 100 + j, (w, run) =>
            {
                long now = w.Time.Ms;
                int ts = w.Ships.Store.SlotOf(FcBench.TargetShip);
                if (ts < 0 || w.Gunnery.TallyOf(FcBench.Shooter, c.Battery) is not { TrackMs: >= 0 } t)
                    return;
                var tgt = w.Ships.Store[ts];
                for (int i = due.Count - 1; i >= 0; i--)
                    if (due[i].Ms <= now)
                    {
                        m[due[i].Check].Add(Earth.Distance(due[i].Pred, tgt.Position) / FcBench.Yd);
                        due.RemoveAt(i);
                    }
                int k = Array.IndexOf(Checks, (int)((now - t.TrackMs) / 1000));
                if (k < 0 || (now - t.TrackMs) % 1000 != 0)
                    return;
                if (w.Gunnery.TrackAt(FcBench.Shooter, c.Battery, now + (long)(Tof * 1000), out var pred, out _) &&
                    w.Gunnery.TrackAt(FcBench.Shooter, c.Battery, now, out _, out var vel))
                {
                    due.Add((now + (long)(Tof * 1000), pred, k));
                    v[k].Add((vel - tgt.Forward * tgt.Speed).Length / Handling.MsPerKnot);
                }
            });
            lock (gate)
                for (int k = 0; k < Checks.Length; k++)
                {
                    miss[k].AddRange(m[k]);
                    rate[k].AddRange(v[k]);
                }
        });
        return [[.. miss.Select(FcBench.Rms)], [.. rate.Select(FcBench.Rms)]];
    }

    static readonly (string Name, Func<FcCase, FcCase> With)[] LevelColumns =
    [
        ("Day 16 kyd, zigzag", c => c with { RangeM = 16 * FcBench.Kyd, Behaviour = Policy.Zigzag(20, 240), VisibilityM = 25_000 }),
        ("Day 22 kyd, steady", c => c with { RangeM = 22 * FcBench.Kyd, VisibilityM = 35_000 }),   // the reference's 25 kyd is past her 20° mounts
        ("Haze 13 kyd, zigzag", c => c with { RangeM = 13 * FcBench.Kyd, Behaviour = Policy.Zigzag(20, 240), VisibilityM = 13_000 }),
        ("Night 12 kyd, zigzag", c => c with { RangeM = 12 * FcBench.Kyd, Behaviour = Policy.Zigzag(20, 240), Start = FcCase.Night }),
    ];

    static readonly (string Name, Policy Behaviour)[] TurnColumns =
    [
        ("steady", Policy.Steady),
        ("zigzag ±20/4 min", Policy.Zigzag(20, 240)),
        ("circle 0.5°/s", Policy.Circle(0.5, 240)),
        ("circle 1.0°/s", Policy.Circle(1, 240)),
    ];

    /// <summary>Table 6 (day, long range, haze, night) and table 11 (target motion against the computer): 8 × 15" on a
    /// battleship at 25 kn abeam on a parallel course, 15 minutes, battle's errors.</summary>
    public static int Run(SimSettings settings, int runs, string? refRoot, Func<string, ShipClass> cls)
    {
        var qe = cls("super_dreadnought");
        FcCase Base(int level) => new() { Name = Name(level), Shooter = qe, Target = qe, Fit = level, BearingDeg = 90, CourseDeg = 0, Minutes = 15 };
        int levels = FireControlPresets.Count;

        var cases = new List<FcCase>();
        for (int l = 0; l < levels; l++)
            foreach (var (_, with) in LevelColumns)
                cases.Add(with(Base(l)));
        var res = FcBench.Runs(cases, settings, runs, 1);
        Console.WriteLine($"Table 6: the levels: Queen Elizabeth (8 x 15\") on Queen Elizabeth at 24 kn abeam, parallel, 15 min ({runs} runs a cell)");
        Console.WriteLine("cell = hit % / straddle % / median s to the first hit after the track. Haze is the visibility only (the rangefinders'");
        Console.WriteLine("haze and the smoke's obscuring come with F5); night is a December night's dark, as the sighting model sees it.");
        Console.WriteLine($"  {"preset",-24}" + string.Concat(LevelColumns.Select(c => $"{c.Name,24}")));
        for (int l = 0; l < levels; l++)
        {
            string row = "";
            for (int k = 0; k < LevelColumns.Length; k++)
            {
                var st = FcStats.Of(res[l * LevelColumns.Length + k], 15);
                row += st.Shells == 0 ? $"{"no solution",24}" : $"{$"{st.HitPct.ToString("0.0", Inv)} / {st.StraddlePct:0} / {FcBench.F(st.FirstHitS)}",24}";
            }
            Console.WriteLine($"  {Name(l),-24}{row}");
        }

        cases.Clear();
        for (int l = 3; l < levels; l++)
            foreach (var (_, p) in TurnColumns)
                cases.Add(Base(l) with { RangeM = 16 * FcBench.Kyd, Behaviour = p, VisibilityM = 25_000 });
        res = FcBench.Runs(cases, settings, runs, 900);
        Console.WriteLine("\nTable 11: target motion against the computer: as table 6, 16 kyd; circles from 240 s after the track");
        Console.WriteLine("cell = hit % / RMS of the salvos' mean fall in range after 300 s, yd");
        Console.WriteLine($"  {"preset",-24}" + string.Concat(TurnColumns.Select(c => $"{c.Name,20}")));
        var circling = new List<double>[TurnColumns.Length];
        for (int l = 3; l < levels; l++)
        {
            string row = "";
            for (int k = 0; k < TurnColumns.Length; k++)
            {
                var st = FcStats.Of(res[(l - 3) * TurnColumns.Length + k], 15);
                row += $"{$"{st.HitPct.ToString("0.0", Inv)} / {FcBench.F(st.MpiRangeM / FcBench.Yd)}",20}";
                (circling[k] ??= []).Add(st.CircleDegS);
            }
            Console.WriteLine($"  {Name(l),-24}{row}");
        }
        Console.WriteLine("  the circles made good: " + string.Join(", ", TurnColumns.Select((c, k) => (c, k)).Where(x => x.c.Behaviour.Kind == PolicyKind.Circle)
            .Select(x => string.Format(Inv, "{0} at {1:0.00}°/s", x.c.Name, circling[x.k].Average()))));
        if (refRoot is not null)
        {
            FcReference.Run(refRoot, "levels", runs);
            FcReference.Run(refRoot, "turning", runs);
        }
        return 0;
    }
}
