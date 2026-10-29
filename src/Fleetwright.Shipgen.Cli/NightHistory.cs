using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen night-history</c>: the night's yardstick (docs/battle/night.md Q0). Each case is a scenario of the
/// record's night fights (the <c>night-*</c> test scenarios and the Dover Strait), played by the AI on every side on the
/// fine tier, a second at a time while an encounter is open, so the ranges at the first sighting, the first salvo and the
/// first torpedoes are read within a second of when they happen. Printed by side beside what was recorded, with the
/// light at the case's place when it starts (the sun, the moon, and the night's reach the sight model takes).</summary>
static class NightHistory
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A case of the record: its scenario, what was recorded and what the sim leaves out.</summary>
    sealed record NightCase(string Era, string File, string Record, string Missing);

    static readonly NightCase[] Cases =
    [
        new("1890-1925", "night-tsushima",
            "Tsushima's night, 27-28 May 1905: the Russian ships that showed searchlights found and torpedoed (Navarin sunk, " +
            "Sissoi Veliky, Admiral Nakhimov, Vladimir Monomakh lost next morning); three Japanese torpedo boats sunk [to check]",
            "the moon; the boats' attack from the dark side"),
        new("1890-1925", "night-southampton",
            "Southampton, Jutland 22:15: searchlights both sides, fire at about 800-1,500 yd for a few minutes; Frauenlob torpedoed " +
            "and sunk; Southampton 89 casualties, Dublin hit [ranges to check]",
            "the challenge; the twilight of a northern June night"),
        new("1890-1925", "night-4th-flotilla",
            "The 4th Flotilla, Jutland 23:30: German searchlights, star shell and secondaries at 1,000-2,000 yd; Tipperary wrecked " +
            "in minutes; Ardent, Fortune lost to gunfire in the night (Sparrowhawk to a collision); probably one torpedo hit (Rostock)",
            "star shell; collisions (4.11)"),
        new("1890-1925", "night-black-prince",
            "Black Prince, Jutland 00:07: blundered into the German line at about 1,000-1,500 yd, lit and sunk in minutes, all hands; " +
            "no hits on the Germans",
            "the unidentified ship (she closed, taking them for friends)"),
        new("1890-1925", "dover-strait",
            "The Dover Strait, 21 April 1917, moonless: sighted at about 600 yd, guns and torpedoes at point blank, Broke rams; " +
            "G42 and G85 sunk, Broke and Swift damaged [to check]",
            "ramming (4.11)"),
    ];

    /// <summary>One side's night in a run: the first sighting (seconds from the start, the range, the level), the first
    /// salvo's and torpedoes' ranges, the salvos' ranges, the shells and torpedoes and what they hit, the ships lost; its
    /// searchlights (Q4): when one of its ships first exposed a beam and her range to what it was on, the ship-seconds of
    /// beams out, and its hits on ships showing a light.</summary>
    sealed class Side
    {
        public double SeenS = double.NaN, SeenM = double.NaN, FireS = double.NaN, FireM = double.NaN, TorpS = double.NaN, TorpM = double.NaN;
        public double LitS = double.NaN, LitM = double.NaN, LitShipS;
        public int HitsOnLights;
        public ContactLevel SeenAs;
        public readonly List<double> SalvoM = [];
        public int Shells, Hits, Torpedoes, TorpedoHits, Lost, Ships;
    }

    sealed record Played(Side[] Sides, double Hours, EndReason Reason, long Missed);

    public static int Run(string settingsPath, string root, string landPath, int runs, string? only, int threads)
    {
        var settings = SimSettings.Load(settingsPath);
        string dir = Path.Combine(root, "..", "Content", "Scenarios");
        var cases = Cases.Where(c => only == null || c.File.Contains(only, StringComparison.OrdinalIgnoreCase) ||
                                     c.Record.Contains(only, StringComparison.OrdinalIgnoreCase)).ToList();
        var scenarios = cases.ToDictionary(c => c.File, c => Scenario.Load(Path.Combine(dir, c.File + ".json"), [dir]));
        var shoreTask = Task.Run(() => Shore.Build(Land.Load(landPath)));
        using var registry = new ShipClassRegistry(0);
        var classes = scenarios.ToDictionary(kv => kv.Key, kv => ScenarioLoader.RequestClasses(kv.Value, registry, [Path.Combine(root, "designs")]));
        registry.WaitAll();
        foreach (var ids in classes.Values)
            foreach (var (name, id) in ids)
                if (registry.ErrorOf(id) is { } err)
                    Console.WriteLine($"  {name}: {err}");
        var shore = shoreTask.Result;
        threads = threads > 0 ? threads : Math.Max(1, Environment.ProcessorCount / 2);
        Console.WriteLine($"the night's yardstick (docs/battle/night.md Q0): {runs} seeds a case, played by the AI on every side, fine tier, " +
                          $"looked at every second while an encounter is open; {threads} threads");

        var jobs = (from c in cases from i in Enumerable.Range(0, runs) select (c.File, Seed: scenarios[c.File].Seed + (ulong)i)).ToList();
        var done = new Played[jobs.Count];
        var sw = Stopwatch.StartNew();
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
            done[i] = Play(scenarios[jobs[i].File], jobs[i].Seed, settings, registry, classes[jobs[i].File], shore));
        Console.WriteLine($"{jobs.Count} plays in {sw.Elapsed.TotalSeconds:F0} s");

        string era = "";
        foreach (var c in cases)
        {
            if (c.Era != era)
                Console.WriteLine($"\n{(era = c.Era)}");
            var s = scenarios[c.File];
            var plays = jobs.Select((j, i) => (j, i)).Where(x => x.j.File == c.File).Select(x => done[x.i]).ToArray();
            Print(c, s, plays, settings);
        }
        return 0;
    }

    static Played Play(Scenario s, ulong seed, SimSettings settings, ShipClassRegistry registry, IReadOnlyDictionary<string, int> classes, Shore shore)
    {
        var (w, ai, _) = ScenarioPlay.Begin(s, seed, settings, registry, classes, shore, Workers.Inline, 1);
        var end = new ScenarioEnd(s, w, ai);
        var store = w.Ships.Store;
        var sides = Enumerable.Range(0, s.Sides.Length).Select(_ => new Side()).ToArray();
        long t0 = s.Start.Ms, read = 0, missed = 0;
        var log = w.CombatLog;
        var launched = new HashSet<int>();

        double Range(int a, int b)
        {
            int sa = store.SlotOf(a), sb = store.SlotOf(b);
            return sa >= 0 && sb >= 0 ? Earth.Distance(store[sa].Position, store[sb].Position) : double.NaN;
        }
        int SideOf(int entity) => end.SideOf(entity) is int x and >= 0 && x < sides.Length ? x : -1;

        while (!end.Check())
        {
            bool open = w.Encounters.Count > 0;
            double want = open ? 1 : 1000;   // the speed only picks the tier: fine while anyone could see anyone
            if (w.Speed != want)
                w.Apply(Command.SetSpeed(want));
            w.AdvanceTo(w.Time + (open ? 1_000 : 60_000));
            double now = (w.Time.Ms - t0) / 1000.0;

            // the first sighting by each side: its earliest contact, at the range between her and the ship that saw her
            for (int sd = 0; sd < sides.Length; sd++)
            {
                var r = sides[sd];
                if (!double.IsNaN(r.SeenS) || w.Sightings.Of(sd) is not { Count: > 0 } pic)
                    continue;
                int first = -1;
                for (int i = 0; i < pic.Count; i++)
                    if (pic[i].Level >= ContactLevel.Ship && (first < 0 || pic[i].FirstMs < pic[first].FirstMs))
                        first = i;
                if (first < 0)
                    continue;
                ref readonly var con = ref pic[first];
                (r.SeenS, r.SeenM, r.SeenAs) = ((con.FirstMs - t0) / 1000.0, Range(con.Observer, con.Entity), con.Level);
            }

            // the searchlights out this second
            for (int k = 0; k < store.OffRailsCount; k++)
            {
                var ship = store[store.OffRailsSlot(k)];
                var (_, on, exposed) = w.Searchlights.OrderOf(ship.Entity, w.Time.Ms);
                if (!exposed || SideOf(ship.Entity) is not (>= 0 and var ls))
                    continue;
                var r = sides[ls];
                r.LitShipS += open ? 1 : 60;
                if (double.IsNaN(r.LitS))
                    (r.LitS, r.LitM) = (now, on != 0 ? Range(ship.Entity, on) : double.NaN);
            }

            long fresh = log.Written - read;
            missed += Math.Max(0, fresh - log.Count);
            for (long i = Math.Min(fresh, log.Count) - 1; i >= 0; i--)
            {
                ref readonly var e = ref log.Newest((int)i);
                int sd = SideOf(e.Shooter);
                switch (e.Kind)
                {
                    case CombatEvent.Fired when sd >= 0:
                    {
                        var r = sides[sd];
                        double m = Range(e.Shooter, e.Target);
                        if (double.IsNaN(r.FireS))
                            (r.FireS, r.FireM) = (now, m);
                        if (!double.IsNaN(m))
                            r.SalvoM.Add(m);
                        r.Shells += e.Count;
                        break;
                    }
                    case CombatEvent.Torpedoes when sd >= 0:
                    {
                        var r = sides[sd];
                        if (double.IsNaN(r.TorpS))
                            (r.TorpS, r.TorpM) = (now, Range(e.Shooter, e.Target));
                        r.Torpedoes += e.Count;
                        launched.Add(e.Shooter);
                        break;
                    }
                    case CombatEvent.Hit when sd >= 0 && e.Source is HitSource.Shell or HitSource.Coarse:
                        sides[sd].Hits++;
                        if (w.Searchlights.OrderOf(e.Target, w.Time.Ms).Exposed)
                            sides[sd].HitsOnLights++;
                        break;
                    case CombatEvent.Hit when sd >= 0 && e.Source == HitSource.Torpedo:
                        sides[sd].TorpedoHits++;
                        break;
                }
            }
            read = log.Written;
        }
        var result = end.Result();   // the ships and losses as the scenario's judge counts them
        for (int sd = 0; sd < sides.Length; sd++)
            (sides[sd].Ships, sides[sd].Lost) = (result[sd].Ships, result[sd].Lost);
        return new Played(sides, (end.EndMs - t0) / 3_600_000.0, end.Reason, missed);
    }

    static void Print(NightCase c, Scenario s, Played[] plays, SimSettings settings)
    {
        Console.WriteLine($"\n  {s.Name} ({c.File}.json)");
        var (lat, lon) = s.Camera is { } cam ? (cam.Lat, cam.Lon) : (0.0, 0.0);
        var at = Earth.Position(lat, lon);
        double sun = Light.SunElevationDeg(at, s.Start), meet = Light.SunElevationDeg(at, new WorldTime(s.Start.Ms + 45 * 60_000));
        var toMoon = Moon.Position(s.Start) - at;
        double moonEl = Math.Asin(Math.Clamp(Vec3d.Dot(toMoon.Normalized(), at.Normalized()), -1, 1)) * 180 / Math.PI;
        var phase = Moon.Phase(s.Start);
        double cloud = s.Weather?.Cloud ?? 0, vis = s.Weather?.VisibilityM ?? settings.DefaultVisibilityM;
        var sky = NightLight.At(at, s.Start, cloud, settings);
        var skyMeet = NightLight.At(at, new WorldTime(s.Start.Ms + 45 * 60_000), cloud, settings);
        Console.WriteLine($"    the light at the start: sun {sun:F1}° (45 min on {meet:F1}°), moon {moonEl:F0}° up, {100 * phase.Lit:F0}% lit" +
                          $" ({(phase.Waxing ? "waxing" : "waning")}), cloud {cloud * 10:0}/10; sky {sky.SkyCdM2:0.0e0} cd/m² ({sky.Lux:0.0e0} lux), 45 min on" +
                          $" {skyMeet.SkyCdM2:0.0e0}; the reference ship seen to {Km(SightModel.SeenToM(vis, sky.SkyCdM2, settings))}, 45 min on" +
                          $" {Km(SightModel.SeenToM(vis, skyMeet.SkyCdM2, settings))} (visibility {Km(vis)})");
        Console.WriteLine($"    record: {c.Record}");
        Console.WriteLine($"    not modelled yet: {c.Missing}");
        Console.WriteLine($"    {"",-10} {"first seen",-24} {"first salvo",-18} {"salvos' range",-15} {"shells",7} {"hits",6} {"hit %",6}" +
                          $" {"torpedoes",-18} {"tp hits",7} {"lost",9}");
        for (int sd = 0; sd < s.Sides.Length; sd++)
        {
            var r = plays.Select(p => p.Sides[sd]).ToArray();
            int seen = r.Count(x => !double.IsNaN(x.SeenS)), fired = r.Count(x => !double.IsNaN(x.FireS)), torp = r.Count(x => !double.IsNaN(x.TorpS));
            string first = seen == 0 ? "never" : $"{Km(Mean(r, x => x.SeenM))} at {Mean(r, x => x.SeenS) / 60:F0} min ({seen}/{r.Length})";
            string salvo = fired == 0 ? "never" : $"{Km(Mean(r, x => x.FireM))} ({fired}/{r.Length})";
            var all = r.SelectMany(x => x.SalvoM).Order().ToArray();
            string spread = all.Length == 0 ? "-" : $"{Km(all[all.Length / 2])} median";
            double shells = r.Average(x => x.Shells), hits = r.Average(x => x.Hits);
            string torps = torp == 0 ? "none" : $"{r.Average(x => x.Torpedoes):F1} from {Km(Mean(r, x => x.TorpM))}";
            Console.WriteLine($"    {s.Sides[sd],-10} {first,-24} {salvo,-18} {spread,-15} {shells,7:F0} {hits,6:F1} {(shells > 0 ? 100 * hits / shells : 0),5:F1}%" +
                              $" {torps,-18} {r.Average(x => x.TorpedoHits),7:F1} {r.Average(x => x.Lost),4:F1} of {r[0].Ships}");
        }
        for (int sd = 0; sd < s.Sides.Length; sd++)
        {
            var r = plays.Select(p => p.Sides[sd]).ToArray();
            int lit = r.Count(x => !double.IsNaN(x.LitS));
            double hits = r.Average(x => x.Hits), onLights = r.Average(x => x.HitsOnLights);
            Console.WriteLine($"    {s.Sides[sd],-10} lights: " + (lit == 0 ? "never" :
                $"first on {Mean(r, x => x.LitS) / 60:F0} min, at {Km(Mean(r, x => x.LitM))} ({lit}/{r.Length}), {r.Average(x => x.LitShipS) / 60:F0} ship-min out") +
                $"; its hits on ships showing a light {onLights:F1} of {hits:F1}");
        }
        Console.WriteLine($"    the end: {plays.Average(p => p.Hours):F1} h; " +
                          string.Join(", ", plays.GroupBy(p => p.Reason).Select(g => $"{g.Key} {g.Count()}")) +
                          (plays.Sum(p => p.Missed) is long m and > 0 ? $"; {m} log lines went by unread" : ""));
    }

    static double Mean(Side[] r, Func<Side, double> v)
    {
        var xs = r.Select(v).Where(x => !double.IsNaN(x)).ToArray();
        return xs.Length == 0 ? double.NaN : xs.Average();
    }

    static string Km(double m) => double.IsNaN(m) ? "-" : double.IsPositiveInfinity(m) ? "unlimited" :
        m < 10_000 ? (m / 1000).ToString("F1", Inv) + " km" : (m / 1000).ToString("F0", Inv) + " km";
}
