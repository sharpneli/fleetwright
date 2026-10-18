using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>What a ship does on the fire-control benches (docs/battle/fire-control.md F1), as
/// research/simulation/fire_control_ref.py's Scenario policies have it.</summary>
enum PolicyKind : byte
{
    Steady,
    /// <summary>Its base course ± the amplitude, a side at random, every period (×0.7-1.3).</summary>
    Zigzag,
    /// <summary>A steady circle at a rate, °/s (positive to starboard), from a time after the shooter's track starts.</summary>
    Circle,
    /// <summary>Steering for the last splash within 600 m, no oftener than the period, within ± the amplitude of its base
    /// course (salvo chasing).</summary>
    Chase,
    /// <summary>One turn by the amplitude at a time after the shooter's track starts.</summary>
    Turn,
    /// <summary>Steering erratically: a new course every minute, the last one's ± 25° (1 sigma).</summary>
    Crippled,
}

readonly record struct Policy(PolicyKind Kind, double AmpDeg = 0, double PeriodS = 0, double RateDegS = 0, double FromS = 0)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static readonly Policy Steady = new(PolicyKind.Steady);
    public static readonly Policy Crippled = new(PolicyKind.Crippled);
    public static Policy Zigzag(double ampDeg, double periodS) => new(PolicyKind.Zigzag, ampDeg, periodS);
    public static Policy Circle(double rateDegS, double fromS) => new(PolicyKind.Circle, RateDegS: rateDegS, FromS: fromS);
    public static Policy Chase(double ampDeg, double periodS) => new(PolicyKind.Chase, ampDeg, periodS);
    public static Policy Turn(double deg, double atS) => new(PolicyKind.Turn, deg, FromS: atS);

    /// <summary>steady, zigzag:AMP:PERIOD, circle:RATE:FROM, chase:AMP:PERIOD, turn:DEG:AT, crippled.</summary>
    public static Policy Parse(string s)
    {
        var p = s.Split(':');
        double At(int i, double def) => p.Length > i ? double.Parse(p[i], Inv) : def;
        return p[0].ToLowerInvariant() switch
        {
            "steady" => Steady,
            "zigzag" => Zigzag(At(1, 20), At(2, 240)),
            "circle" => Circle(At(1, 1), At(2, 240)),
            "chase" => Chase(At(1, 30), At(2, 30)),
            "turn" => Turn(At(1, 45), At(2, 300)),
            "crippled" => Crippled,
            _ => throw new ArgumentException($"no policy '{s}' (steady, zigzag:AMP:PERIOD, circle:RATE:FROM, chase:AMP:PERIOD, turn:DEG:AT, crippled)"),
        };
    }

    public override string ToString() => Kind switch
    {
        PolicyKind.Zigzag => string.Format(Inv, "zigzag ±{0:0}° every {1:0} s", AmpDeg, PeriodS),
        PolicyKind.Circle => string.Format(Inv, "circling {0:0.##}°/s from {1:0} s", RateDegS, FromS),
        PolicyKind.Chase => string.Format(Inv, "chasing salvos ±{0:0}°, every {1:0} s at most", AmpDeg, PeriodS),
        PolicyKind.Turn => string.Format(Inv, "turning {0:+0;-0}° at {1:0} s", AmpDeg, FromS),
        PolicyKind.Crippled => "crippled, steering erratically",
        _ => "steady",
    };
}

/// <summary>One bench engagement (fire-control.md F1): a shooter steaming north at its speed, a target at a bearing
/// and range on a course, each with its policy; the weather, the time (night by the clock), battle or practice (the
/// combat factor), how long, the crews. The shooter's battery engages with its own fit or a preset's (0-8).</summary>
sealed record FcCase
{
    public static readonly WorldTime Day = WorldTime.FromUtc(1916, 5, 31, 14);
    /// <summary>A dark night in the North Sea: mid-December, 22:00.</summary>
    public static readonly WorldTime Night = WorldTime.FromUtc(1916, 12, 15, 22);

    public string Name { get; init; } = "";
    public required ShipClass Shooter { get; init; }
    public required ShipClass Target { get; init; }
    public int Battery { get; init; }
    /// <summary>A preset (0-8), or -1 for the battery's own fit.</summary>
    public int Fit { get; init; } = -1;
    public double RangeM { get; init; } = 16 * FcBench.Kyd;
    public double BearingDeg { get; init; } = 90;
    /// <summary>The target's course, degrees true (the shooter's base course is north).</summary>
    public double CourseDeg { get; init; }
    public double OwnKn { get; init; } = 25;
    public double TargetKn { get; init; } = 25;
    public Policy Own { get; init; } = Policy.Steady;
    public Policy Behaviour { get; init; } = Policy.Steady;
    /// <summary>The visibility, m; 0 the settings' default.</summary>
    public double VisibilityM { get; init; }
    public WorldTime Start { get; init; } = Day;
    /// <summary>Battle against practice (<see cref="SimSettings.GunneryCombat"/>); NaN the settings' (battle).</summary>
    public double Combat { get; init; } = double.NaN;
    public double Minutes { get; init; } = 15;
    public CrewQuality Crew { get; init; } = CrewQuality.Trained;
    public bool Damage { get; init; }

    public int Level => Fit >= 0 ? Fit : Shooter.Batteries[Battery].Local ? 0 : (int)Shooter.Batteries[Battery].Level;

    public SimSettings Settings(SimSettings s) => s with
    {
        HitsDoDamage = Damage, GunneryCombat = double.IsNaN(Combat) ? s.GunneryCombat : Combat,
    };
}

/// <summary>One run of a case: the battery's tally, its salvos as they fell, when the target was put on a new course,
/// the speeds the two made good, and the rate the target circled at (°/s, while circling).</summary>
sealed class FcRun
{
    public GunnerySystem.Tally Tally;
    public readonly List<GunnerySystem.SalvoReport> Salvos = [];
    public readonly List<long> Manoeuvres = [];
    /// <summary>When own ship was put on a new course (a turn policy).</summary>
    public readonly List<long> OwnManoeuvres = [];
    public double OwnKn, TargetKn, CircleDegS = double.NaN;
    public double GunneryMs;
}

/// <summary>The fire-control benches' engine (fire-control.md F1): a case run on a fresh world a second at a time, the
/// two ships steered by their policies between the seconds, as fire_control_ref.py's <c>engage()</c> does.</summary>
static class FcBench
{
    public const double Kyd = 914.4, Yd = 0.9144;
    public const int Shooter = 1, TargetShip = 2;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Runs a case once; <paramref name="second"/> is told of each second after it's run (the converge bench's
    /// probe).</summary>
    public static FcRun Once(FcCase c, SimSettings settings, int seed, Action<World, FcRun>? second = null)
    {
        var s = c.Settings(settings);
        var w = new World(c.Start, (ulong)seed, s);
        int a = w.Ships.AddClass(c.Shooter), b = w.Ships.AddClass(c.Target);
        double lat = 56.0, lon = 4.0;
        var p = Earth.Position(lat, lon);
        var f = Earth.Forward(p, c.BearingDeg);
        Earth.Advance(ref p, ref f, c.RangeM);
        var (tl, to) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(a, lat, lon, 0, c.OwnKn, side: 0));
        w.Apply(ShipSystem.Spawn(b, tl, to, c.CourseDeg, c.TargetKn, side: 1));
        if (c.VisibilityM > 0)
            w.Apply(Command.SetWeather(c.VisibilityM));
        if (c.Crew != CrewQuality.Trained)
            w.Apply(GunnerySystem.SetCrew(0, c.Crew));
        w.Apply(GunnerySystem.Engage(Shooter, c.Battery, TargetShip, c.Fit));

        var run = new FcRun();
        var rng = new Random(seed * 7919 + 17);
        GunnerySystem.SalvoReport? fall = null;
        w.Gunnery.Reported = r =>
        {
            run.Salvos.Add(r);
            fall = r;
        };
        double Uniform(double lo, double hi) => lo + (hi - lo) * rng.NextDouble();
        double Gauss() => Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());
        long start = c.Start.Ms, end = start + (long)(c.Minutes * 60_000);
        var tp = c.Behaviour;
        double tNext = tp.PeriodS * Uniform(0.3, 1.0), oNext = c.Own.PeriodS * Uniform(0.5, 1.0), lastChase = -1e9, rudder = 0, prevHeading = double.NaN;
        bool turned = false, circling = false, ownTurned = false;
        double circleSum = 0, ownSum = 0, tgtSum = 0;
        int circleN = 0, n = 0;
        var store = w.Ships.Store;
        for (long t = start + 1000; t <= end; t += 1000)
        {
            double el = (t - start) / 1000.0;
            int ts = store.SlotOf(TargetShip), os = store.SlotOf(Shooter);
            if (ts < 0 || os < 0)
                break;
            var tgt = store[ts];
            long trackMs = w.Gunnery.TallyOf(Shooter, c.Battery) is { TrackMs: >= 0 } tally ? tally.TrackMs : -1;
            double sinceTrack = trackMs >= 0 ? (t - trackMs) / 1000.0 : double.NaN;
            switch (tp.Kind)
            {
                case PolicyKind.Zigzag when el >= tNext:
                    w.Apply(ShipSystem.Course(TargetShip, Mod(c.CourseDeg + tp.AmpDeg * (rng.Next(2) * 2 - 1))));
                    tNext = el + tp.PeriodS * Uniform(0.7, 1.3);
                    run.Manoeuvres.Add(t);
                    break;
                case PolicyKind.Crippled when el >= tNext:
                    w.Apply(ShipSystem.Course(TargetShip, Mod(tgt.HeadingDeg + 25 * Gauss())));
                    tNext = el + 60;
                    run.Manoeuvres.Add(t);
                    break;
                case PolicyKind.Turn when !turned && sinceTrack >= tp.FromS:
                    w.Apply(ShipSystem.Course(TargetShip, Mod(tgt.HeadingDeg + tp.AmpDeg)));
                    turned = true;
                    run.Manoeuvres.Add(t);
                    break;
                case PolicyKind.Circle when sinceTrack >= tp.FromS:
                {
                    // the helm worked to hold the rate: the rudder walked by the rate's error
                    double rate = double.IsNaN(prevHeading) ? 0 : Wrap(tgt.HeadingDeg - prevHeading);
                    if (!circling)
                        run.Manoeuvres.Add(t);
                    else if (sinceTrack >= tp.FromS + 60)
                    {
                        circleSum += Math.Abs(rate);
                        circleN++;
                    }
                    circling = true;
                    rudder = Math.Clamp(rudder + 4 * (tp.RateDegS - rate), -35, 35);
                    w.Apply(ShipSystem.Rudder(TargetShip, rudder));
                    break;
                }
                case PolicyKind.Chase when fall is { } r && el - lastChase > tp.PeriodS && Math.Abs(r.MeanX) < 600:
                {
                    // steer for the splash just seen, within ± the amplitude of the base course
                    double lof = Earth.Bearing(store[os].Position, tgt.Position);
                    double rel = Wrap(lof + double.RadiansToDegrees(Math.Atan2(r.MeanY, r.MeanX)) - c.CourseDeg);
                    if (Math.Abs(rel) > 150)
                        rel = Math.CopySign(tp.AmpDeg, rel);
                    w.Apply(ShipSystem.Course(TargetShip, Mod(c.CourseDeg + Math.Clamp(rel, -tp.AmpDeg, tp.AmpDeg))));
                    lastChase = el;
                    run.Manoeuvres.Add(t);
                    break;
                }
            }
            fall = null;
            prevHeading = tgt.HeadingDeg;
            if (c.Own.Kind == PolicyKind.Zigzag && el >= oNext)
            {
                w.Apply(ShipSystem.Course(Shooter, Mod(c.Own.AmpDeg * (rng.Next(2) * 2 - 1))));
                oNext = el + c.Own.PeriodS * Uniform(0.7, 1.3);
            }
            else if (c.Own.Kind == PolicyKind.Turn && !ownTurned && sinceTrack >= c.Own.FromS)
            {
                w.Apply(ShipSystem.Course(Shooter, Mod(store[os].HeadingDeg + c.Own.AmpDeg)));
                ownTurned = true;
                run.OwnManoeuvres.Add(t);
            }
            w.AdvanceTo(new WorldTime(t));
            second?.Invoke(w, run);
            if ((os = store.SlotOf(Shooter)) >= 0 && (ts = store.SlotOf(TargetShip)) >= 0)
            {
                ownSum += store[os].Speed;
                tgtSum += store[ts].Speed;
                n++;
            }
        }
        run.Tally = w.Gunnery.TallyOf(Shooter, c.Battery) ?? default;
        (run.OwnKn, run.TargetKn) = (ownSum / Math.Max(n, 1) / Handling.MsPerKnot, tgtSum / Math.Max(n, 1) / Handling.MsPerKnot);
        if (circleN > 0)
            run.CircleDegS = circleSum / circleN;
        for (int i = 0; i < w.Probes.Count; i++)
            if (w.Probes.Names[i] == nameof(GunnerySystem))
                run.GunneryMs += Probes.Ms(w.Probes.Ticks[i]);
        return run;
    }

    /// <summary>Several cases' runs at once, on threads: runs[k][j] is case k's j-th (seeds seed0 + j).</summary>
    public static FcRun[][] Runs(IReadOnlyList<FcCase> cases, SimSettings settings, int runs, int seed0)
    {
        var all = new FcRun[cases.Count][];
        for (int k = 0; k < cases.Count; k++)
            all[k] = new FcRun[runs];
        Parallel.For(0, cases.Count * runs, i => all[i / runs][i % runs] = Once(cases[i / runs], settings, seed0 + i % runs));
        return all;
    }

    public static double Mod(double d) => (d % 360 + 360) % 360;

    public static double Wrap(double d) => d - 360 * Math.Round(d / 360);

    public static double Median(IEnumerable<double> v)
    {
        var a = v.Where(x => !double.IsNaN(x)).Order().ToArray();
        return a.Length == 0 ? double.NaN : a[a.Length / 2];
    }

    public static double Rms(IEnumerable<double> v)
    {
        double s = 0;
        int n = 0;
        foreach (double x in v)
        {
            s += x * x;
            n++;
        }
        return n == 0 ? double.NaN : Math.Sqrt(s / n);
    }

    public static string F(double x, string format = "0") => double.IsNaN(x) ? "-" : x.ToString(format, Inv);
}

/// <summary>A case's runs summed up (fire-control.md F1): hits and straddles, how soon the first came (after the
/// track, and by salvo after the first one), the track's aim error as it settled and after the target's manoeuvres,
/// and the salvos' mean fall in range once settled.</summary>
sealed record FcStats
{
    public int Runs, Shells, Hits, Salvos, Straddles, RunsHit;
    public double HitPct, StraddlePct, FirstHitS, FirstStraddleS, FirstHitSalvo, FirstHitAfterOpenS, OwnKn, TargetKn, CircleDegS, GunneryMsPerS;
    /// <summary>The track's aim error (RMS of its miss, m) for salvos fired 2-4, 4-8 and 8+ minutes after the track.</summary>
    public double[] Settle = [];
    /// <summary>The same for salvos fired 0-1, 1-2 and 2-4 minutes after the target's latest manoeuvre.</summary>
    public double[] Recovery = [];
    /// <summary>The salvos' mean fall in range against the target (RMS, m), fired 5 minutes or more after the track.</summary>
    public double MpiRangeM;

    static readonly double[] SettleEdges = [120, 240, 480, double.PositiveInfinity], RecoveryEdges = [0, 60, 120, 240];

    public static FcStats Of(FcRun[] runs, double minutes)
    {
        var st = new FcStats { Runs = runs.Length };
        var settle = Enumerable.Range(0, 3).Select(_ => new List<double>()).ToArray();
        var recovery = Enumerable.Range(0, 3).Select(_ => new List<double>()).ToArray();
        var mpi = new List<double>();
        var firstHit = new List<double>();
        var firstStraddle = new List<double>();
        var firstSalvo = new List<double>();
        var afterOpen = new List<double>();
        var circles = new List<double>();
        foreach (var r in runs)
        {
            var t = r.Tally;
            (st.Shells, st.Hits, st.Salvos, st.Straddles) = (st.Shells + t.Shells, st.Hits + t.Hits, st.Salvos + t.Salvos, st.Straddles + t.Straddles);
            if (t.FirstHitMs >= 0)
            {
                st.RunsHit++;
                firstHit.Add((t.FirstHitMs - t.TrackMs) / 1000.0);
            }
            if (t.FirstStraddleMs >= 0)
                firstStraddle.Add((t.FirstStraddleMs - t.TrackMs) / 1000.0);
            var salvos = r.Salvos.OrderBy(s => s.FiredMs).ToList();
            int hit = salvos.FindIndex(s => s.Hits > 0);
            if (hit >= 0)
            {
                firstSalvo.Add(hit + 1);
                afterOpen.Add((salvos[hit].FiredMs - salvos[0].FiredMs) / 1000.0);
            }
            foreach (var s in salvos)
            {
                double since = (s.FiredMs - t.TrackMs) / 1000.0, err = Math.Sqrt(s.AimErrM * s.AimErrM + s.AimErrAcrossM * s.AimErrAcrossM);
                for (int k = 0; k < 3; k++)
                    if (since >= SettleEdges[k] && since < SettleEdges[k + 1])
                        settle[k].Add(err);
                long last = r.Manoeuvres.LastOrDefault(m => m <= s.FiredMs, -1);
                if (last >= 0)
                    for (int k = 0; k < 3; k++)
                        if ((s.FiredMs - last) / 1000.0 >= RecoveryEdges[k] && (s.FiredMs - last) / 1000.0 < RecoveryEdges[k + 1])
                            recovery[k].Add(err);
                if (since > 300)
                    mpi.Add(s.MeanX);
            }
            if (!double.IsNaN(r.CircleDegS))
                circles.Add(r.CircleDegS);
            st.GunneryMsPerS += r.GunneryMs;
        }
        st.HitPct = 100.0 * st.Hits / Math.Max(st.Shells, 1);
        st.StraddlePct = 100.0 * st.Straddles / Math.Max(st.Salvos, 1);
        st.FirstHitS = FcBench.Median(firstHit);
        st.FirstStraddleS = FcBench.Median(firstStraddle);
        st.FirstHitSalvo = FcBench.Median(firstSalvo);
        st.FirstHitAfterOpenS = FcBench.Median(afterOpen);
        st.Settle = [.. settle.Select(FcBench.Rms)];
        st.Recovery = [.. recovery.Select(FcBench.Rms)];
        st.MpiRangeM = FcBench.Rms(mpi);
        st.OwnKn = runs.Average(r => r.OwnKn);
        st.TargetKn = runs.Average(r => r.TargetKn);
        st.CircleDegS = circles.Count > 0 ? circles.Average() : double.NaN;
        st.GunneryMsPerS /= runs.Length * minutes * 60;
        return st;
    }

    /// <summary>The summary's lines, as <c>duel</c> prints them.</summary>
    public IEnumerable<string> Lines()
    {
        var inv = CultureInfo.InvariantCulture;
        yield return string.Format(inv, "  hits {0:0.0}% ({1} of {2} shells), straddles {3:0}% of {4} salvos; first straddle {5} s, first hit {6} s " +
            "after the track (medians); a hit in {7} of {8} runs", HitPct, Hits, Shells, StraddlePct, Salvos, FcBench.F(FirstStraddleS),
            FcBench.F(FirstHitS), RunsHit, Runs);
        yield return string.Format(inv, "  the first hit on salvo {0}, {1} s after the first salvo (medians)", FcBench.F(FirstHitSalvo),
            FcBench.F(FirstHitAfterOpenS));
        yield return string.Format(inv, "  the track's aim error, RMS m: salvos 2-4 min after the track {0}, 4-8 min {1}, 8+ min {2}; " +
            "after the target's manoeuvres 0-1 min {3}, 1-2 min {4}, 2-4 min {5}", FcBench.F(Settle[0]), FcBench.F(Settle[1]), FcBench.F(Settle[2]),
            FcBench.F(Recovery[0]), FcBench.F(Recovery[1]), FcBench.F(Recovery[2]));
        yield return string.Format(inv, "  the salvos' mean fall in range after 5 min, RMS {0} m; made good: own {1:0.0} kn, target {2:0.0} kn{3}",
            FcBench.F(MpiRangeM), OwnKn, TargetKn, double.IsNaN(CircleDegS) ? "" : string.Format(inv, ", circling {0:0.00}°/s", CircleDegS));
    }
}

/// <summary>Runs research/simulation/fire_control_ref.py's tables beside a bench (<c>--ref</c>), through
/// fc_tables.py; its output goes straight to the console.</summary>
static class FcReference
{
    public static void Run(string root, string table, int n)
    {
        string dir = Path.GetFullPath(Path.Combine(root, "..", "research", "simulation"));
        Console.WriteLine($"\nfire_control_ref.py ({table}, {n} runs a cell):");
        try
        {
            var psi = new ProcessStartInfo("python", $"fc_tables.py {table} {n}") { WorkingDirectory = dir, UseShellExecute = false };
            using var p = Process.Start(psi)!;
            p.WaitForExit();
        }
        catch (Exception x)
        {
            Console.WriteLine($"  couldn't run it ({x.Message}): python with numpy, in {dir}");
        }
    }
}
