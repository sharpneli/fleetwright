using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen coarse-parity</c> (docs/battle/coarse.md "The parity bench", J0): the coarse tier measured against
/// the fine one, its gold standard. A fixed panel in four layers, cheapest first, each case run both ways:
/// <list type="bullet">
/// <item><b>Duels</b> (<see cref="FcBench"/>, no damage): one battery on a scripted target. Fire alone, no AI.</item>
/// <item><b>Dodging</b> (decision 1): the AI's own ships under fire, played to the end (a flotilla's attack run, light
/// ships running from heavy ones); the hit rate on each target class, and the boats' torpedoes.</item>
/// <item><b>Fights to the end</b>: duels with damage (the target down, out of the fight, and when), and the squadrons.</item>
/// <item><b>Scenarios</b> played to their end: each side's losses, hits and torpedoes, the end's time, the exchange.</item>
/// </list>
/// Each measure's two means are set beside its band (decision 5: ±15% of fine's, an absolute floor for small numbers; the
/// exchange ±10 points) and judged: ok, FAIL (outside the band by more than twice the noise), or noisy (a standard error
/// over a third of the band, with the seeds it would need). The asked speed only picks the tier; the runs go at full tilt,
/// many at once on threads. Every world runs its systems inline (<see cref="Workers.Inline"/>): the same world on any
/// count of workers.</summary>
static class CoarseParity
{
    /// <summary>The asked speed of the coarse runs: the coarse tier, under the rails' snap speed, so nothing is placed or
    /// snapped by the frame rule and the fight is the coarse tier's own.</summary>
    public const double CoarseSpeed = 1000;

    /// <summary>A measure's band, relative to fine's mean (decision 5).</summary>
    const double Rel = 0.15;

    /// <summary>The asked speed while no encounter is open, the same both ways: ships come together as they do in play.</summary>
    const double Cruise = 1000;

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <param name="Threads">How many plays at once (0: a thread a core); a full panel's Jutlands want fewer.</param>
    /// <param name="Only">The scenarios to play, by file name (none: the panel's), for working on one.</param>
    public sealed record Options(string Panel, string[]? Layers, string Tier, int Runs, int Seeds, string? Out, string? FineFrom, string LandPath, int Threads,
        string[]? Only = null);

    /// <summary>A measure's mean over its runs or seeds, its standard error and how many there were.</summary>
    public readonly record struct Stat(double Mean, double Se, int N)
    {
        public static readonly Stat None = new(double.NaN, double.NaN, 0);

        /// <summary>Over the values given, NaN ones left out (a run with no first hit has no time to it).</summary>
        public static Stat Of(IEnumerable<double> values)
        {
            var v = values.Where(x => !double.IsNaN(x)).ToArray();
            if (v.Length == 0)
                return None;
            double mean = v.Average();
            double var = v.Length > 1 ? v.Sum(x => (x - mean) * (x - mean)) / (v.Length - 1) : double.NaN;
            return new Stat(mean, Math.Sqrt(var / v.Length), v.Length);
        }

        /// <summary>A ratio of sums over runs (hits to shells), its error from the runs' spread about it (the ratio
        /// estimator: a run that fired much counts for more).</summary>
        public static Stat Ratio(IReadOnlyList<(double Num, double Den)> runs, double scale = 1)
        {
            double num = runs.Sum(r => r.Num), den = runs.Sum(r => r.Den);
            if (!(den > 0))
                return None;
            double r = num / den;
            int n = runs.Count;
            double ss = runs.Sum(x => (x.Num - r * x.Den) * (x.Num - r * x.Den));
            double se = n > 1 ? Math.Sqrt(ss * n / (n - 1)) / den : double.NaN;
            return new Stat(r * scale, se * scale, n);
        }
    }

    /// <summary>A measure of a case, both ways. <see cref="Floor"/> is the band's least width, or with
    /// <see cref="Absolute"/> the band itself.</summary>
    public sealed record Row(string Layer, string Case, string Name, double Floor, bool Absolute, Stat Fine, Stat Coarse)
    {
        public string Key => $"{Layer}|{Case}|{Name}";
        public double Band => Absolute ? Floor : Math.Max(Rel * Math.Abs(Fine.Mean), Floor);

        /// <summary>ok, FAIL, noisy, or "-" with nothing to compare; and how many times the runs a noisy one would need.</summary>
        public (string Verdict, double More) Judge()
        {
            if (Fine.N == 0 && Coarse.N == 0)
                return ("-", double.NaN);
            if (Fine.N == 0 || Coarse.N == 0)
                return ("FAIL", double.NaN);   // one way has it and the other never does (a first hit, say)
            double band = Band, d = Math.Abs(Coarse.Mean - Fine.Mean);
            double sf = double.IsNaN(Fine.Se) ? 0 : Fine.Se, sc = double.IsNaN(Coarse.Se) ? 0 : Coarse.Se, sd = Math.Sqrt(sf * sf + sc * sc);
            double more = Math.Max(1, Math.Pow(Math.Max(sf, sc) / (band / 3), 2));
            if (d > band + 2 * sd)
                return ("FAIL", double.NaN);
            return more > 1 ? ("noisy", more) : d <= band ? ("ok", double.NaN) : ("noisy", double.NaN);
        }
    }

    // ---------------------------------------------------------------- the panel

    /// <summary>A duel of the panel: its case (fine, at 1x), and whether it's a fight to the end (damage on).</summary>
    sealed record DuelCase(string Name, FcCase Case);

    static List<DuelCase> Duels(Func<string, ShipClass> cls, bool damage, FallOutRule fallOut)
    {
        var bb = cls("super_dreadnought");
        if (damage)
            return
            [
                new("battleship v light cruiser, 12 km", new FcCase { Shooter = bb, Target = cls("light_cruiser"), RangeM = 12_000, TargetKn = 25, Minutes = 30, Damage = true, FallOut = fallOut }),
                new("light cruiser v destroyer, 7 km, zigzag", new FcCase { Shooter = cls("light_cruiser"), Target = cls("destroyer"), RangeM = 7_000, TargetKn = 30,
                    Behaviour = Policy.Zigzag(30, 75), Minutes = 30, Damage = true, FallOut = fallOut }),
            ];
        var dd = cls("destroyer");
        return
        [
            new("battleship v battleship, 16 kyd", new FcCase { Shooter = bb, Target = bb }),
            new("battleship v battleship, 16 kyd, she zigzags", new FcCase { Shooter = bb, Target = bb, Behaviour = Policy.Zigzag(20, 240) }),
            new("battleship v battleship, 16 kyd, she turns", new FcCase { Shooter = bb, Target = bb, Behaviour = Policy.Turn(45, 300) }),
            new("battleship v battleship, 16 kyd, own turn", new FcCase { Shooter = bb, Target = bb, Own = Policy.Turn(45, 300) }),
            new("battleship v battleship, 7 km in 8 km haze", new FcCase { Shooter = bb, Target = bb, RangeM = 7_000, VisibilityM = 8_000 }),
            new("secondaries v destroyer, 6 km", new FcCase { Shooter = bb, Battery = 1, Target = dd, RangeM = 6_000, TargetKn = 30 }),
            new("secondaries v destroyer, 6 km, zigzag", new FcCase { Shooter = bb, Battery = 1, Target = dd, RangeM = 6_000, TargetKn = 30, Behaviour = Policy.Zigzag(30, 75) }),
            new("light cruiser v destroyer, 8 km, zigzag", new FcCase { Shooter = cls("light_cruiser"), Target = dd, RangeM = 8_000, TargetKn = 30, Behaviour = Policy.Zigzag(30, 75) }),
            new("main battery v torpedo boat, 5 km, zigzag", new FcCase { Shooter = bb, Target = cls("v25"), RangeM = 5_000, TargetKn = 28, Behaviour = Policy.Zigzag(30, 75) }),
        ];
    }

    static readonly string[] DodgingScenarios = ["flotilla", "running-fight"];
    static readonly string[] FightScenarios = ["squadrons"];
    static readonly string[] StandardScenarios = ["dover-strait", "cape-sarych", "coronel", "prizes-war", "aland", "tsushima"];
    static readonly string[] FullScenarios = ["heligoland-bight", "dogger-bank", "jutland"];

    public static int Run(SimSettings settings, string root, Func<string, ShipClass> cls, Options o)
    {
        var all = new[] { "duels", "dodging", "fights", "scenarios" };
        var layers = o.Layers ?? (o.Panel == "quick" ? ["duels"] : all);
        if (layers.FirstOrDefault(l => !all.Contains(l)) is { } bad)
            throw new ArgumentException($"no layer '{bad}' ({string.Join(", ", all)})");
        if (o.Panel is not ("quick" or "standard" or "full"))
            throw new ArgumentException($"no panel '{o.Panel}' (quick, standard, full)");
        bool fine = o.Tier is "both" or "fine" && o.FineFrom == null, coarse = o.Tier is "both" or "coarse";
        if (o.Tier is not ("both" or "fine" or "coarse"))
            throw new ArgumentException($"no tier '{o.Tier}' (both, fine, coarse)");
        int runs = o.Runs > 0 ? o.Runs : o.Panel == "quick" ? 100 : 300;
        int seeds = o.Seeds > 0 ? o.Seeds : 48;
        var kept = o.FineFrom != null ? Load(o.FineFrom) : null;
        string contentDir = Path.Combine(root, "..", "Content");
        var fallOut = DefaultFallOut(Path.Combine(contentDir, "Sim", "doctrine.json"));
        Console.WriteLine($"the coarse tier against the fine one: the {o.Panel} panel ({string.Join(", ", layers)}); {runs} runs a duel, {seeds} seeds a scenario; " +
                          $"coarse at {CoarseSpeed:G}x asked" + (kept != null ? $"; the fine side from {o.FineFrom}" : ""));

        var rows = new List<Row>();
        var wall = new double[2];
        var sw = Stopwatch.StartNew();

        // duels, and the fights' duels
        foreach (var (layer, damage) in new[] { ("duels", false), ("fights", true) })
        {
            if (!layers.Contains(layer))
                continue;
            var cases = Duels(cls, damage, fallOut);
            FcRun[][]? f = null, c = null;
            if (fine)
                f = Timed(ref wall[0], () => FcBench.Runs([.. cases.Select(d => d.Case)], settings, runs, 1));
            if (coarse)
                c = Timed(ref wall[1], () => FcBench.Runs([.. cases.Select(d => d.Case with { Speed = CoarseSpeed })], settings, runs, 1));
            for (int k = 0; k < cases.Count; k++)
                rows.AddRange(damage ? FightRows(cases[k], f?[k], c?[k]) : DuelRows(cases[k], f?[k], c?[k]));
        }

        // the scenarios: dodging, the fights' squadrons, the scenarios layer
        var plays = new List<(string Layer, string Name)>();
        if (layers.Contains("dodging"))
            plays.AddRange(DodgingScenarios.Select(n => ("dodging", n)));
        if (layers.Contains("fights"))
            plays.AddRange(FightScenarios.Select(n => ("fights", n)));
        if (layers.Contains("scenarios"))
            plays.AddRange((o.Panel == "full" ? [.. StandardScenarios, .. FullScenarios] : StandardScenarios).Select(n => ("scenarios", n)));
        if (o.Only != null)
            plays = [.. plays.Where(p => o.Only.Contains(p.Name))];
        if (plays.Count > 0)
            rows.AddRange(PlayRows(plays, settings, root, o.LandPath, seeds, fine, coarse, wall, o.Threads > 0 ? o.Threads : Environment.ProcessorCount));

        if (kept != null)
            rows = [.. rows.Select(r => kept.TryGetValue(r.Key, out var k) ? r with { Fine = k } : r)];
        Print(rows, wall, sw.Elapsed.TotalSeconds);
        if (o.Out != null)
        {
            Save(o.Out, o, rows, wall, runs, seeds);
            Console.WriteLine($"kept in {o.Out}");
        }
        return 0;
    }

    static T Timed<T>(ref double total, Func<T> f)
    {
        var sw = Stopwatch.StartNew();
        var r = f();
        total += sw.Elapsed.TotalSeconds;
        return r;
    }

    /// <summary>The default doctrine's fall-out rule (Content/Sim/doctrine.json's "default"): a ship out of the fight as
    /// her captain would judge her.</summary>
    static FallOutRule DefaultFallOut(string path)
    {
        var p = DoctrinePresets.Load(path)[DoctrinePresets.DefaultName];
        double Of(DoctrineField f) => p.Any(x => x.Field == f) ? p.First(x => x.Field == f).Value : Doctrine.Default(f);
        return new FallOutRule(Of(DoctrineField.FallOutSpeedShare), Of(DoctrineField.FallOutGunsShare), Of(DoctrineField.FallOutListDeg),
            Of(DoctrineField.FallOutWaterShare));
    }

    // ---------------------------------------------------------------- the duels' measures

    static IEnumerable<Row> DuelRows(DuelCase d, FcRun[]? f, FcRun[]? c)
    {
        var c0 = d.Case;
        int guns = c0.Shooter.Batteries[c0.Battery].Guns;
        double gm = guns * c0.Minutes;
        Stat S(FcRun[]? runs, Func<FcRun, double> v) => runs == null ? Stat.None : Stat.Of(runs.Select(v));
        Row R(string name, double floor, Func<FcRun, double> v) => new("duels", d.Name, name, floor, false, S(f, v), S(c, v));
        yield return R("hits a gun a minute", 0.005, r => r.Tally.Hits / gm);
        yield return R("rounds a gun a minute", 0.03, r => r.Tally.Shells / gm);
        yield return R("first straddle, s after the track", 20, r => r.Tally.FirstStraddleMs >= 0 ? (r.Tally.FirstStraddleMs - r.Tally.TrackMs) / 1000.0 : double.NaN);
        yield return R("first hit, s after the track", 30, r => r.Tally.FirstHitMs >= 0 ? (r.Tally.FirstHitMs - r.Tally.TrackMs) / 1000.0 : double.NaN);
    }

    static IEnumerable<Row> FightRows(DuelCase d, FcRun[]? f, FcRun[]? c)
    {
        Stat S(FcRun[]? runs, Func<FcRun, double> v) => runs == null ? Stat.None : Stat.Of(runs.Select(v));
        Row R(string name, double floor, Func<FcRun, double> v) => new("fights", d.Name, name, floor, false, S(f, v), S(c, v));
        long t0 = d.Case.Start.Ms;
        yield return R("out of the fight, share", 0.1, r => r.OutMs >= 0 ? 1 : 0);
        yield return R("going down, share", 0.1, r => r.DownMs >= 0 ? 1 : 0);
        yield return R("minutes to out of the fight", 2, r => r.OutMs >= 0 ? (r.OutMs - t0) / 60_000.0 : double.NaN);
        yield return R("hits", 1, r => r.Tally.Hits);
    }

    // ---------------------------------------------------------------- the scenarios

    /// <summary>A scenario played once: the end, each side's result, and what the combat log told as it went.</summary>
    sealed class Played
    {
        public required SideResult[] Result;
        public EndReason Reason;
        public double Hours;
        /// <summary>Shells fired at, and shell hits on, each target class.</summary>
        public readonly Dictionary<string, (double Shells, double Hits)> At = [];
        public required double[] Shells, ShellHits, TorpedoHits, Launchers;
        public long Missed;
    }

    static IEnumerable<Row> PlayRows(List<(string Layer, string Name)> plays, SimSettings settings, string root, string landPath, int seeds, bool fine, bool coarse,
        double[] wall, int threads)
    {
        string dir = Path.Combine(root, "..", "Content", "Scenarios");
        var scenarios = plays.ToDictionary(p => p.Name, p => Scenario.Load(Path.Combine(dir, p.Name + ".json"), [dir]));
        var shoreTask = Task.Run(() => Shore.Build(Land.Load(landPath)));
        using var registry = new ShipClassRegistry(0);
        var classes = scenarios.ToDictionary(kv => kv.Key, kv => ScenarioLoader.RequestClasses(kv.Value, registry, [Path.Combine(root, "designs")]));
        registry.WaitAll();
        var shore = shoreTask.Result;

        var tiers = new List<double>();
        if (fine)
            tiers.Add(1);
        if (coarse)
            tiers.Add(CoarseSpeed);
        var jobs = (from p in plays from t in tiers from i in Enumerable.Range(0, seeds) select (p.Name, Speed: t, Seed: scenarios[p.Name].Seed + (ulong)i)).ToList();
        var done = new Played[jobs.Count];
        var took = new double[jobs.Count];
        Console.WriteLine($"  playing {plays.Count} scenarios {seeds} times each way ({jobs.Count} plays) on {threads} threads...");
        // the biggest first, so the long ones don't start last
        var order = Enumerable.Range(0, jobs.Count).OrderByDescending(i => scenarios[jobs[i].Name].ShipCount).ToArray();
        Parallel.ForEach(order, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            var j = jobs[i];
            var sw = Stopwatch.StartNew();
            done[i] = Play(scenarios[j.Name], j.Seed, j.Speed, settings, registry, classes[j.Name], shore);
            took[i] = sw.Elapsed.TotalSeconds;
        });
        for (int i = 0; i < jobs.Count; i++)
            wall[jobs[i].Speed == 1 ? 0 : 1] += took[i];
        long missed = done.Sum(p => p.Missed);
        if (missed > 0)
            Console.WriteLine($"  ({missed} combat log lines went by unread: the ring too small for a minute of a fight)");

        foreach (var (layer, name) in plays)
        {
            var s = scenarios[name];
            Played[] Of(double speed) => [.. jobs.Select((j, i) => (j, i)).Where(x => x.j.Name == name && x.j.Speed == speed).Select(x => done[x.i])];
            var f = Of(1);
            var c = Of(CoarseSpeed);
            Row R(string m, double floor, Func<Played[], Stat> st, bool absolute = false) => new(layer, s.Name, m, floor, absolute, st(f), st(c));
            Stat Mean(Played[] p, Func<Played, double> v) => p.Length == 0 ? Stat.None : Stat.Of(p.Select(v));

            if (layer == "dodging")
            {
                // the hit rate on each target class (decision 1: a band each, so a destroyer's drift can't hide)
                foreach (var cls in f.Concat(c).SelectMany(p => p.At.Keys).Distinct().Order(StringComparer.Ordinal))
                    yield return R($"hit % on {cls}", 0.5, p => p.Length == 0 ? Stat.None
                        : Stat.Ratio([.. p.Select(x => x.At.TryGetValue(cls, out var a) ? (a.Hits, a.Shells) : (0.0, 0.0))], 100));
            }
            for (int side = 0; side < s.Sides.Length; side++)
            {
                int sd = side;
                string who = s.Sides[side];
                yield return R($"{who}: ships lost", 0.5, p => Mean(p, x => x.Result[sd].Lost));
                yield return R($"{who}: tonnage lost, share", 0.05, p => Mean(p, x => x.Result[sd].Tonnes > 0 ? x.Result[sd].LostTonnes / x.Result[sd].Tonnes : 0));
                yield return R($"{who}: shell hits made", 3, p => Mean(p, x => x.ShellHits[sd]));
                yield return R($"{who}: hit %", 0.5, p => p.Length == 0 ? Stat.None : Stat.Ratio([.. p.Select(x => (x.ShellHits[sd], x.Shells[sd]))], 100));
                yield return R($"{who}: ships firing torpedoes", 0.5, p => Mean(p, x => x.Launchers[sd]));
                yield return R($"{who}: torpedo hits", 0.5, p => Mean(p, x => x.TorpedoHits[sd]));
            }
            if (s.Sides.Length == 2)
                yield return R($"the exchange: {s.Sides[0]}'s share of the tonnage lost", 0.10, p => Mean(p, x =>
                {
                    double a = x.Result[0].LostTonnes, b = x.Result[1].LostTonnes;
                    return a + b > 0 ? a / (a + b) : double.NaN;
                }), absolute: true);
            yield return R("decided, share", 0.1, p => Mean(p, x => x.Reason == EndReason.Decided ? 1 : 0));
            yield return R("the end, hours", 0.25, p => Mean(p, x => x.Hours));
        }
    }

    static Played Play(Scenario s, ulong seed, double speed, SimSettings settings, ShipClassRegistry registry, Dictionary<string, int> classes, Shore shore)
    {
        var (w, ai, _) = ScenarioPlay.Begin(s, seed, settings, registry, classes, shore, Workers.Inline, speed);
        var end = new ScenarioEnd(s, w, ai);
        int sides = s.Sides.Length;
        var p = new Played
        {
            Result = [], Shells = new double[sides], ShellHits = new double[sides], TorpedoHits = new double[sides], Launchers = new double[sides],
        };
        var names = new Dictionary<int, string>();
        var launched = new HashSet<int>();
        var store = w.Ships.Store;
        string Class(int entity)
        {
            if (names.TryGetValue(entity, out var n))
                return n;
            int slot = store.SlotOf(entity);
            return slot >= 0 ? names[entity] = store[slot].Handling.Class.Name : "?";
        }
        int SideOf(int entity) => end.SideOf(entity) is int x and >= 0 && x < sides ? x : -1;
        void Note()
        {
            for (int slot = 0; slot < store.Slots; slot++)
                if (store.IsAlive(slot))
                    Class(store[slot].Entity);
        }
        Note();
        long read = 0;
        var log = w.CombatLog;
        ScenarioPlay.PlayToEnd(w, end, Cruise, speed, () =>
        {
            long fresh = log.Written - read;
            if (fresh > log.Count)
                p.Missed += fresh - log.Count;
            for (long i = Math.Min(fresh, log.Count) - 1; i >= 0; i--)
            {
                ref readonly var e = ref log.Newest((int)i);
                int side = SideOf(e.Shooter);
                switch (e.Kind)
                {
                    case CombatEvent.Fired:
                    {
                        var at = Class(e.Target);
                        p.At[at] = p.At.TryGetValue(at, out var a) ? (a.Shells + e.Count, a.Hits) : (e.Count, 0);
                        if (side >= 0)
                            p.Shells[side] += e.Count;
                        break;
                    }
                    case CombatEvent.Hit when e.Source is HitSource.Shell or HitSource.Coarse:
                    {
                        var at = Class(e.Target);
                        p.At[at] = p.At.TryGetValue(at, out var a) ? (a.Shells, a.Hits + 1) : (0, 1);
                        if (side >= 0)
                            p.ShellHits[side]++;
                        break;
                    }
                    case CombatEvent.Hit when e.Source == HitSource.Torpedo:
                        if (side >= 0)
                            p.TorpedoHits[side]++;
                        break;
                    case CombatEvent.Torpedoes:
                        if (side >= 0 && launched.Add(e.Shooter))
                            p.Launchers[side]++;
                        break;
                }
            }
            read = log.Written;
            Note();
        });
        p.Result = end.Result();
        p.Reason = end.Reason;
        p.Hours = (end.EndMs - s.Start.Ms) / 3_600_000.0;
        return p;
    }

    // ---------------------------------------------------------------- the report

    static void Print(List<Row> rows, double[] wall, double total)
    {
        foreach (var layer in rows.GroupBy(r => r.Layer))
        {
            Console.WriteLine();
            Console.WriteLine($"{layer.Key}:");
            foreach (var c in layer.GroupBy(r => r.Case))
            {
                Console.WriteLine($"  {c.Key}");
                foreach (var r in c)
                {
                    var (v, more) = r.Judge();
                    double d = r.Coarse.Mean - r.Fine.Mean;
                    string pct = r.Absolute || !(Math.Abs(r.Fine.Mean) > 1e-9) ? "" : string.Format(Inv, " ({0:+0;-0;0}%)", Math.Round(100 * d / Math.Abs(r.Fine.Mean)));
                    Console.WriteLine(string.Format(Inv, "    {0,-44} fine {1,-16} coarse {2,-16} {3,-18} band ±{4,-7} {5}{6}", r.Name, Num(r.Fine), Num(r.Coarse),
                        double.IsNaN(d) ? "" : G(d, true) + pct, G(r.Band), v, double.IsNaN(more) ? "" : string.Format(Inv, " (×{0:0.#} the runs)", more)));
                }
            }
        }
        var judged = rows.Select(r => r.Judge().Verdict).ToList();
        Console.WriteLine();
        Console.WriteLine($"{rows.Count} measures: {judged.Count(v => v == "ok")} ok, {judged.Count(v => v == "FAIL")} FAIL, {judged.Count(v => v == "noisy")} noisy, " +
                          $"{judged.Count(v => v == "-")} with nothing either way");
        Console.WriteLine(string.Format(Inv, "time: {0:0} s in all; the runs' own, summed over threads: fine {1:0} s, coarse {2:0} s", total, wall[0], wall[1]));
    }

    static string Num(Stat s) => s.N == 0 ? "-" : G(s.Mean) + (double.IsNaN(s.Se) ? "" : " ±" + G(s.Se));

    static string G(double x, bool sign = false)
    {
        if (double.IsNaN(x))
            return "-";
        double a = Math.Abs(x);
        string f = a >= 100 ? "0" : a >= 10 ? "0.0" : a >= 1 ? "0.00" : "0.000";
        x = double.Parse(x.ToString(f, Inv), Inv);   // rounded first, so a small negative shows as 0, not -0
        return x.ToString(sign ? "+" + f + ";-" + f + ";" + f : f, Inv);
    }

    // ---------------------------------------------------------------- kept results

    static void Save(string path, Options o, List<Row> rows, double[] wall, int runs, int seeds)
    {
        using var fs = File.Create(path);
        using var j = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
        j.WriteStartObject();
        j.WriteString("date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", Inv));
        j.WriteString("commit", Commit());
        j.WriteString("panel", o.Panel);
        j.WriteNumber("runs", runs);
        j.WriteNumber("seeds", seeds);
        j.WriteNumber("coarse_speed", CoarseSpeed);
        j.WriteNumber("fine_s", Math.Round(wall[0], 1));
        j.WriteNumber("coarse_s", Math.Round(wall[1], 1));
        j.WriteStartArray("rows");
        foreach (var r in rows)
        {
            j.WriteStartObject();
            j.WriteString("layer", r.Layer);
            j.WriteString("case", r.Case);
            j.WriteString("name", r.Name);
            j.WriteNumber("floor", r.Floor);
            j.WriteBoolean("absolute", r.Absolute);
            Write(j, "fine", r.Fine);
            Write(j, "coarse", r.Coarse);
            j.WriteString("verdict", r.Judge().Verdict);
            j.WriteEndObject();
        }
        j.WriteEndArray();
        j.WriteEndObject();
    }

    static void Write(Utf8JsonWriter j, string name, Stat s)
    {
        j.WriteStartObject(name);
        j.WriteNumber("n", s.N);
        if (s.N > 0)
        {
            j.WriteNumber("mean", s.Mean);
            if (!double.IsNaN(s.Se))
                j.WriteNumber("se", s.Se);
        }
        j.WriteEndObject();
    }

    /// <summary>A kept file's fine side, by the rows' keys.</summary>
    static Dictionary<string, Stat> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var kept = new Dictionary<string, Stat>();
        Console.WriteLine($"  kept {doc.RootElement.GetProperty("date").GetString()} at {doc.RootElement.GetProperty("commit").GetString()}");
        foreach (var r in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            var f = r.GetProperty("fine");
            int n = f.GetProperty("n").GetInt32();
            kept[$"{r.GetProperty("layer").GetString()}|{r.GetProperty("case").GetString()}|{r.GetProperty("name").GetString()}"] = n == 0 ? Stat.None
                : new Stat(f.GetProperty("mean").GetDouble(), f.TryGetProperty("se", out var se) ? se.GetDouble() : double.NaN, n);
        }
        return kept;
    }

    static string Commit()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("git", "rev-parse --short HEAD") { RedirectStandardOutput = true, UseShellExecute = false })!;
            string s = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return s;
        }
        catch (Exception x) when (x is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return "?";
        }
    }
}
