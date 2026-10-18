using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>The fire-control mechanisms alone (docs/battle/fire-control.md F1, F2), the research's tables on the sim's
/// presets (fire-control-research.md; fire_control_ref.py's tables 5, 12, 6 and 11): <c>shipgen fc-converge</c>, how
/// far each computer's prediction is off over a time of flight as it settles, after the target turns or circles and
/// after own ship turns, with the time it takes to recover; and <c>shipgen fc-levels</c>, each preset's hits by day, at
/// long range, in haze and at night, and against a manoeuvring target. With <c>--ref</c> the reference's own tables
/// follow. They check that each machine fails where it failed; their hit rates are not targets (the plan's
/// principles).</summary>
static class FcLevels
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const double Tof = 25;
    const int Seconds = 600;
    static readonly int[] Checks = [30, 60, 120, 240, 330, 360, 420, 540];

    /// <summary>The presets in the reference's order: the judgement below the eyeball, the extras by their levels
    /// (Argo by the Dreyer, the AFCT's turn setting and the Japanese plot by the AFCT).</summary>
    static readonly int[] All = [FireControlPresets.Judgement, 0, 1, 2, 3, 10, 4, 11, 12, 5, 6, 7, 8];
    static readonly int[] Directors = [3, 10, 4, 11, 12, 5, 6, 7, 8];

    /// <summary>The research's recovery after a target's turn, by preset (fire-control-research.md §5.4, computing.md §8).</summary>
    static string RecoveryBand(int p) => p switch
    {
        0 => "3-5 min", 2 => "3-4 min", 3 or 12 => "2-4 min", 10 => "1.5-2.5 min", 4 or 5 => "1-2 min", 6 => "30-60 s", 7 => "15-30 s", 8 => "5-15 s",
        _ => "",
    };

    static string Name(int preset) => $"{preset} {FireControlPresets.Name(preset)}";

    /// <summary>Table 5 (a crossing target turning 45° at 300 s), the same with own ship turning instead, and table 12
    /// (circling at 1°/s from 300 s): the predicted position's error over 25 s, RMS yd; the target's motion as the
    /// computer holds it, kn; and how long each takes to recover.</summary>
    public static int Converge(SimSettings settings, int runs, string? refRoot, Func<string, ShipClass> cls)
    {
        var qe = cls("super_dreadnought");
        var target = cls("lion");   // a 25 kn target; Queen Elizabeth makes 24
        FcCase Case(int preset, Policy target_, Policy own) => new()
        {
            Name = Name(preset), Shooter = qe, Target = target, Fit = preset, RangeM = 15 * FcBench.Kyd, BearingDeg = 90, CourseDeg = 180,
            OwnKn = 20, TargetKn = 25, Behaviour = target_, Own = own, Minutes = 11, VisibilityM = 40_000, Combat = 1,   // the reference's practice errors
        };
        Console.WriteLine(string.Format(Inv, "Table 5: the computer's predicted-position error over a {0:0} s time of flight, RMS yd ({1} runs a row)", Tof, runs));
        Console.WriteLine("Queen Elizabeth at 20 kn, a battlecruiser crossing at 25 kn at 15 kyd (opposite courses, abeam); it turns 45° at 300 s after");
        Console.WriteLine("the track starts (the helm put over then: the sim's ship turns as a ship does, the reference's at once). Practice's errors");
        Console.WriteLine("(combat 1), as the reference's; rangefinder bias in.");
        Console.WriteLine("Recovery: from the helm order until the error is back within 1.25x (or 50 yd of) its settled level, 240-300 s.");
        var turn = All.Select(p => Probe(Case(p, Policy.Turn(45, 300), Policy.Steady), settings, runs)).ToArray();
        Block(turn, "the miss, yd", "0", true);
        Block(turn, "the target's motion as held, error in kn", "0.0", false);

        Console.WriteLine("\nOwn ship turns 45° at 300 s instead, half the runs toward the target and half away (the target steady): a helm-free");
        Console.WriteLine("machine takes it at once, a plain clock must re-learn its rates, and its stale rates add to or cancel the drift it");
        Console.WriteLine("already has on a crossing target, by the turn's side (the own-turn kick on the salvos is separate and not in these)");
        var own = All.Select(p => Pool(Probe(Case(p, Policy.Steady, Policy.Turn(45, 300)), settings, (runs + 1) / 2),
            Probe(Case(p, Policy.Steady, Policy.Turn(-45, 300)), settings, runs / 2, 100 + runs))).ToArray();
        Block(own, "the miss, yd", "0", false);

        Console.WriteLine("\nTable 12: the same against a steady circle at 1°/s from 300 s, RMS yd");
        Console.WriteLine($"  {"preset",-22}{"straight 240s",14}{"+30",7}{"+60",7}{"+120",7}{"+240",7}");
        foreach (int p in Directors.Where(p => p != 12))
        {
            var e = Probe(Case(p, Policy.Circle(1, 300), Policy.Steady), settings, runs).Miss;
            Console.WriteLine($"  {Name(p),-22}{FcBench.F(e[3]),14}" + string.Concat(new[] { 4, 5, 6, 7 }.Select(k => $"{FcBench.F(e[k]),7}")));
        }
        if (refRoot is not null)
        {
            FcReference.Run(refRoot, "converge", runs);
            FcReference.Run(refRoot, "circle", runs);
        }
        return 0;

        void Block(Probed[] rows, string unit, string fmt, bool recovery)
        {
            Console.WriteLine($"  {unit}");
            Console.WriteLine($"  {"preset",-22}{"30s",6}{"60s",6}{"120s",6}{"240s",6} |{"+30",6}{"+60",6}{"+120",6}{"+240",6}" +
                (recovery ? $"{"recovery",10}{"research",13}" : ""));
            for (int i = 0; i < rows.Length; i++)
            {
                var v = unit.StartsWith("the miss") ? rows[i].Miss : rows[i].Rate;
                Console.WriteLine($"  {Name(All[i]),-22}" + string.Concat(Checks.Select((_, k) => (k == 4 ? " |" : "") + $"{FcBench.F(v[k], fmt),6}")) +
                    (recovery ? $"{Recovery(rows[i].PerSecond),10}{RecoveryBand(All[i]),13}" : ""));
            }
        }
    }

    sealed record Probed(double[] Miss, double[] Rate, double[] PerSecond);

    /// <summary>Two probes' runs as one: their RMS values pooled.</summary>
    static Probed Pool(Probed a, Probed b)
    {
        static double[] P(double[] x, double[] y) => [.. x.Zip(y, (u, v) => double.IsNaN(u) ? v : double.IsNaN(v) ? u : Math.Sqrt((u * u + v * v) / 2))];
        return new Probed(P(a.Miss, b.Miss), P(a.Rate, b.Rate), P(a.PerSecond, b.PerSecond));
    }

    /// <summary>When the miss is back within 1.25x (or 50 yd) of its settled level after the turn at 300 s, and stays
    /// there over 15 s: seconds after the helm order; "none" if it never comes back, "never left" if the turn didn't show
    /// over the error it already had.</summary>
    static string Recovery(double[] rms)
    {
        double pre = FcBench.Rms(rms.Skip(240).Take(60));
        double thr = Math.Max(1.25 * pre, pre + 50);
        bool left = false;
        for (int s = 300; s + 15 <= rms.Length; s++)
        {
            double w = FcBench.Rms(rms.Skip(s).Take(15));
            if (w > thr)
                left = true;
            else if (left)
                return $"{s - 300} s";
        }
        return left ? "none" : "never left";
    }

    /// <summary>A case's runs probed each second: where the track puts the target a time of flight on (compared with
    /// where it went) and the velocity it holds (against the truth's). The misses (yd) and the motion's errors (kn) at the
    /// checks, and the miss each second after the track starts, RMS over the runs.</summary>
    static Probed Probe(FcCase c, SimSettings settings, int runs, int seed0 = 100)
    {
        var miss = new List<double>[Seconds];
        var rate = Checks.Select(_ => new List<double>()).ToArray();
        for (int s = 0; s < Seconds; s++)
            miss[s] = [];
        object gate = new();
        Parallel.For(0, runs, j =>
        {
            var due = new List<(long Ms, Vec3d Pred, int Second)>();
            var m = new List<(int, double)>();
            var v = new List<(int, double)>();
            FcBench.Once(c, settings, seed0 + j, (w, run) =>
            {
                long now = w.Time.Ms;
                int ts = w.Ships.Store.SlotOf(FcBench.TargetShip);
                if (ts < 0 || w.Gunnery.TallyOf(FcBench.Shooter, c.Battery) is not { TrackMs: >= 0 } t)
                    return;
                var tgt = w.Ships.Store[ts];
                for (int i = due.Count - 1; i >= 0; i--)
                    if (due[i].Ms <= now)
                    {
                        m.Add((due[i].Second, Earth.Distance(due[i].Pred, tgt.Position) / FcBench.Yd));
                        due.RemoveAt(i);
                    }
                int s = (int)((now - t.TrackMs) / 1000);
                if (s >= Seconds || !w.Gunnery.TrackAt(FcBench.Shooter, c.Battery, now + (long)(Tof * 1000), out var pred, out _))
                    return;
                due.Add((now + (long)(Tof * 1000), pred, s));
                int k = Array.IndexOf(Checks, s);
                if (k >= 0 && w.Gunnery.TrackAt(FcBench.Shooter, c.Battery, now, out _, out var vel))
                    v.Add((k, (vel - tgt.Forward * tgt.Speed).Length / Handling.MsPerKnot));
            });
            lock (gate)
            {
                foreach (var (s, x) in m)
                    miss[s].Add(x);
                foreach (var (k, x) in v)
                    rate[k].Add(x);
            }
        });
        var perSecond = miss.Select(FcBench.Rms).ToArray();
        return new Probed([.. Checks.Select(k => perSecond[k])], [.. rate.Select(FcBench.Rms)], perSecond);
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
        FcCase Base(int preset) => new() { Name = Name(preset), Shooter = qe, Target = qe, Fit = preset, BearingDeg = 90, CourseDeg = 0, Minutes = 15 };

        var cases = new List<FcCase>();
        foreach (int p in All)
            foreach (var (_, with) in LevelColumns)
                cases.Add(with(Base(p)));
        var res = FcBench.Runs(cases, settings, runs, 1);
        Console.WriteLine($"Table 6: the levels: Queen Elizabeth (8 x 15\") on Queen Elizabeth at 24 kn abeam, parallel, 15 min ({runs} runs a cell)");
        Console.WriteLine("cell = hit % / straddle % / median s to the first hit after the track. Haze is the visibility only (the rangefinders'");
        Console.WriteLine("haze and the smoke's obscuring come with F5); night is a December night's dark, as the sighting model sees it.");
        Console.WriteLine($"  {"preset",-22}" + string.Concat(LevelColumns.Select(c => $"{c.Name,24}")));
        for (int i = 0; i < All.Length; i++)
        {
            string row = "";
            for (int k = 0; k < LevelColumns.Length; k++)
            {
                var st = FcStats.Of(res[i * LevelColumns.Length + k], 15);
                row += st.Shells == 0 ? $"{"no solution",24}" : $"{$"{st.HitPct.ToString("0.0", Inv)} / {st.StraddlePct:0} / {FcBench.F(st.FirstHitS)}",24}";
            }
            Console.WriteLine($"  {Name(All[i]),-22}{row}");
        }

        cases.Clear();
        foreach (int p in Directors)
            foreach (var (_, b) in TurnColumns)
                cases.Add(Base(p) with { RangeM = 16 * FcBench.Kyd, Behaviour = b, VisibilityM = 25_000 });
        res = FcBench.Runs(cases, settings, runs, 900);
        Console.WriteLine("\nTable 11: target motion against the computer: as table 6, 16 kyd; circles from 240 s after the track");
        Console.WriteLine("cell = hit % / RMS of the salvos' mean fall in range after 300 s, yd");
        Console.WriteLine($"  {"preset",-22}" + string.Concat(TurnColumns.Select(c => $"{c.Name,20}")));
        var circling = new List<double>[TurnColumns.Length];
        for (int i = 0; i < Directors.Length; i++)
        {
            string row = "";
            for (int k = 0; k < TurnColumns.Length; k++)
            {
                var st = FcStats.Of(res[i * TurnColumns.Length + k], 15);
                row += $"{$"{st.HitPct.ToString("0.0", Inv)} / {FcBench.F(st.MpiRangeM / FcBench.Yd)}",20}";
                (circling[k] ??= []).Add(st.CircleDegS);
            }
            Console.WriteLine($"  {Name(Directors[i]),-22}{row}");
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
