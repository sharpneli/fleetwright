using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Fall = Fleetwright.Shipgen.Cli.GivenFire.Fall;
using ManGuns = Fleetwright.Sim.Ai.ManGuns;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen crew-history</c>: the crews' yardstick (docs/battle/crew.md "The yardstick", N0). Each case is a
/// ship of the record's kind under the record's fire (<see cref="GivenFire"/>), timed, since a hit count alone can't show
/// reliefs or a lull: the men out of action through the fire and after it, by battle station, the batteries' mounts
/// working, the directors, the conn and the parties, the men shaken and frozen (N3b), beside what was recorded. The men's
/// experience and work-up are the record's (decision 6): their nerve reads the experience.</summary>
static class CrewHistory
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    sealed record CrewCase(string Era, string Name, GivenFire.Barrage Fire, string Record, string Wanted);

    static readonly CrewCase[] Cases =
    [
        new("1890-1925", "Oryol, Tsushima 1905: about 74 hits (Campbell; 12″, 8″, 6″ as Orel's 53) over 5 h",
            new("borodino", 9, [new("mikasa", 0, "ap", 0.06), new("mikasa", 0, "common", 0.03), new("izumo", 0, null, 0.17), new("mikasa", 1, null, 0.74)],
                74, 300, (4_500, 7_500), (45, 135), "prepared", "1905", 5, Experience.Green, Captains: true, Man: (ManGuns.TorpedoCraft, ManGuns.TorpedoCraft)),
            "43 killed, 80 wounded; \"main losses behind armour from splinters entering ports and slits\" (Kostenko)", "about 100-150 out of action"),
        new("1890-1925", "Chester, Jutland 1916: 17 × 15 cm (Wiesbaden class) in about 5 min, open-backed shields, 3-5 weeks in commission",
            new("town_chatham", 26, [new("wiesbaden", 0, null, 1)], 17, 5, (6_000, 8_000), (45, 135), "closed up", "1916", 0.5, Experience.Trained, 0.3, Captains: true, Man: (ManGuns.InRange, ManGuns.TorpedoCraft)),
            "29 killed, 49 wounded; four guns out; no recorded flight", "about 75-80 out; she still steams and fights"),
        new("1890-1925", "Southampton, Jutland night 1916: four light cruisers' 10.5 cm at 1,500 yd for 3.5 min (18 hits [EST])",
            new("town_chatham", 20, [new("stettin", 0, null, 1)], 18, 3.5, (1_200, 1_600), (60, 120), "closed up", "1916", 0.5, Captains: true, Man: (ManGuns.InRange, ManGuns.TorpedoCraft)),
            "89 casualties; 75% of the upper deck killed or wounded in 3.5 min; guns silent \"through lack of flesh and blood\"; stokers off watch re-manned them in a lull",
            "silent from casualties, few or none leaving; reliefs only once the fire stops"),
        new("1890-1925", "Kent, Falklands 1914: 37 × 10.5 cm (Nürnberg) over 90 min, reservists in casemates (Devonshire class)",
            new("devonshire", 22, [new("stettin", 0, null, 1)], 37, 90, (5_000, 10_000), (45, 135), "closed up", "1916", 2, Experience.Average, Captains: true, Man: (ManGuns.InRange, ManGuns.TorpedoCraft)),
            "4 killed, 12 wounded, most at one gun; armour not pierced", "about 16 out; no leaving"),
        new("1890-1925", "Emden, Cocos 1914: Sydney's 6″ from about 9,500 yd for 1 h 40 min (100 hits [EST]; Dresden class as Stettin)",
            new("stettin", 20, [new("town_chatham", 0, null, 1)], 100, 100, (8_000, 9_500), (45, 135), "closed up", "1916", 2, Captains: true, Man: (ManGuns.Always, ManGuns.TorpedoCraft)),
            "115-134 killed and 56-69 wounded of 316; fire slackened as the rangefinders went; one gun at 11:00, then none",
            "accuracy falls with control damage first; one gun late, worked by reliefs"),
        new("1890-1925", "Suvorov, Tsushima 1905: 5 h of 12″, 8″ and 6″ (150 hits [EST]; not counted), green",
            new("borodino", 9, [new("mikasa", 0, "common", 0.1), new("izumo", 0, null, 0.2), new("mikasa", 1, null, 0.7)],
                150, 300, (3_000, 7_500), (30, 150), "prepared", "1905", 5.5, Experience.Green, Captains: true, Man: (ManGuns.TorpedoCraft, ManGuns.TorpedoCraft)),
            "all lost but the staff; short routs after blasts; covered light-gun crews used up as reliefs; a 12-pdr each side firing late",
            "the reserve gone within hours; a last gun late"),
        new("1890-1925", "Sverige under 500 × 152 mm (the user's case) over 2 h at 2-4 km",
            new("sverige", 16, [new("sverige", 1, null, 1)], 500, 120, (2_000, 4_000), (45, 135), "closed up", "1916", 3, Captains: true, Man: (ManGuns.InRange, ManGuns.InRange)),
            "-", "toll rising for as long as her secondaries fight; the last gun outlives the main battery; silent from want of men before the toll flattens"),
        new("later cuts", "Graf Spee, River Plate 1939: 18 × 6″ and 2 × 8″ over 80 min",
            new("deutschland", 24, [new("light_cruiser", 0, null, 0.9, RangeHi: 12_000), new("kent", 0, null, 0.1)], 20, 80, (8_000, 16_000), (30, 150), "closed up", "1916", 2, Captains: true, Man: (ManGuns.InRange, ManGuns.InRange)),
            "36 killed, 60 wounded", "in the band"),
    ];

    /// <summary>A run's ship: her hits' men, and her state at each sample minute (NaN out once she's gone).</summary>
    sealed class Ship
    {
        public int Entity, Hits, Killed, Wounded, Lost = -1, MostShaken, MostFrozen;
        public double[] Out = [], Mounts = [], Conned = [], Parties = [], AtFire = [], OnWater = [], Reliefs = [], Covered = [], Shaken = [], Frozen = [], Officers = [];
        public double[][] Stations = [], Batteries = [];
    }

    public static int Run(string settingsPath, int runs, string? only, Func<string, Design> load)
    {
        var settings = SimSettings.Load(settingsPath);
        var classes = new Dictionary<string, ShipClass>();
        ShipClass Cls(string name) => classes.TryGetValue(name, out var c) ? c : classes[name] = ShipClassCompiler.Compile(ShipDesign.Build(load(name)));
        Console.WriteLine($"the crews' yardstick (docs/battle/crew.md N0, N3b): {runs} runs a case, each a ship under the case's fire, timed, through the world's damage system");
        string era = "";
        foreach (var cc in Cases)
        {
            if (only != null && !cc.Name.Contains(only, StringComparison.OrdinalIgnoreCase))
                continue;
            if (cc.Era != era)
                Console.WriteLine($"\n{(era = cc.Era)}");
            var sw = Stopwatch.StartNew();
            var target = Cls(cc.Fire.Target);
            int[] at = Samples(cc.Fire);
            var ships = new List<Ship>();
            GivenFire.Run(cc.Fire, cc.Name, target, Cls, settings, runs, (_, e) => ships.Add(New(e, at.Length, target)), (w, h) =>
            {
                var fx = w.Damage.LastEffects;
                var r = ships[h.Run];
                (r.Hits, r.Killed, r.Wounded) = (r.Hits + 1, r.Killed + fx.Killed, r.Wounded + fx.Wounded);
                Most(w, r);
            }, (w, m) =>
            {
                foreach (var r in ships)
                    Most(w, r);
                int i = Array.IndexOf(at, m);
                if (i < 0)
                    return;
                foreach (var r in ships)
                    Sample(w, target, r, i);
            });
            Print(cc, target, ships, at, sw.Elapsed.TotalSeconds);
        }
        return 0;
    }

    /// <summary>The minutes sampled: a quarter, a half, three quarters and the end of the fire, then 5, 15 and 30 min after
    /// and the case's end, as far as it runs.</summary>
    static int[] Samples(GivenFire.Barrage b)
    {
        int fire = (int)Math.Ceiling(b.Minutes), end = (int)Math.Max(b.Hours * 60, fire);
        return new[] { 0.25, 0.5, 0.75 }.Select(x => (int)Math.Ceiling(x * fire)).Append(fire)
            .Concat(new[] { 5, 15, 30 }.Select(x => fire + x)).Append(end).Where(m => m >= 1 && m <= end).Distinct().Order().ToArray();
    }

    static Ship New(int entity, int n, ShipClass c)
    {
        double[] Nan() => Enumerable.Repeat(double.NaN, n).ToArray();
        return new Ship
        {
            Entity = entity, Out = Nan(), Mounts = Nan(), Conned = Nan(), Parties = Nan(), AtFire = Nan(), OnWater = Nan(), Reliefs = Nan(), Covered = Nan(),
            Shaken = Nan(), Frozen = Nan(), Officers = Nan(),
            Stations = Enumerable.Range(0, n).Select(_ => new double[BattleStations.Count]).ToArray(),
            Batteries = Enumerable.Range(0, n).Select(_ => new double[c.Batteries.Length]).ToArray(),
        };
    }

    static readonly double[] Scratch = new double[BattleStations.Count];

    /// <summary>The most men shaken and frozen at once so far (after each hit and each minute).</summary>
    static void Most(World w, Ship r)
    {
        int slot = w.Ships.Store.SlotOf(r.Entity);
        if (slot < 0 || w.Ships.Store.Combat.Condition(slot) is not { Status: ShipStatus.Afloat } k)
            return;
        (r.MostShaken, r.MostFrozen) = (Math.Max(r.MostShaken, k.Shaken.Sum()), Math.Max(r.MostFrozen, k.Frozen.Sum()));
    }

    static void Sample(World w, ShipClass c, Ship r, int i)
    {
        var store = w.Ships.Store;
        int slot = store.SlotOf(r.Entity);
        var k = slot >= 0 ? store.Combat.Condition(slot) : null;
        if (k == null || k.Status != ShipStatus.Afloat)
        {
            if (r.Lost < 0)
                r.Lost = i;
            return;
        }
        r.Out[i] = k.CrewLost;
        Array.Clear(Scratch);
        BattleStations.Tally(c, k, Scratch, r.Stations[i]);
        int ok = 0;
        for (int m = 0; m < c.Mounts.Length; m++)
            if (c.Mounts[m].Battery is int b and >= 0 && k.MountWorks[m])
                (ok, r.Batteries[i][b]) = (ok + 1, r.Batteries[i][b] + 1);
        r.Mounts[i] = ok;
        r.Conned[i] = k.Conned ? 1 : 0;
        (r.Reliefs[i], r.Covered[i]) = (k.In.Sum(), k.Covered.Sum());
        (r.Shaken[i], r.Frozen[i], r.Officers[i]) = (k.Shaken.Sum(), k.Frozen.Sum(), k.OfficerAt.Count(t => t != 0));
        double men = 0, fire = 0, water = 0;
        for (int p = 0; p < k.PartyCount; p++)
        {
            ref readonly var party = ref k.Parties[p];
            men += party.Men;
            fire += party.Target >= 0 ? party.Men : 0;
            water += party.Target < 0 && party.Job != RepairJob.None ? party.Men : 0;
        }
        (r.Parties[i], r.AtFire[i], r.OnWater[i]) = (men, fire, water);
    }

    static void Print(CrewCase cc, ShipClass c, List<Ship> rs, int[] at, double secs)
    {
        var b = cc.Fire;
        int n = rs.Count, hits = Math.Max(rs.Sum(r => r.Hits), 1);
        static double Med(IEnumerable<double> xs)
        {
            var v = xs.Where(x => !double.IsNaN(x)).Order().ToList();
            return v.Count == 0 ? double.NaN : v[v.Count / 2];
        }
        static double Q(IEnumerable<double> xs, double q)
        {
            var v = xs.Where(x => !double.IsNaN(x)).Order().ToList();
            return v.Count == 0 ? double.NaN : v[Math.Min(v.Count - 1, (int)(q * v.Count))];
        }
        double crew = c.Components.ToArray().Sum(p => p.BattleCrew) + c.Cells.ToArray().Sum(x => x.BattleCrew);
        var men = new double[BattleStations.Count];
        BattleStations.Tally(c, null, men, new double[BattleStations.Count]);
        Console.WriteLine($"  {cc.Name}");
        Console.WriteLine(string.Format(Inv, "    {0}, {1:0} kn, {2:0} men at battle stations ({3}); {4} crew, work-up {5:0.##}{13}; {6}, {7}; {8} hits in {9:0.#} min at {10:0.#}-{11:0.#} km: {12}",
            c.Name, b.Kn, crew, string.Join(", ", Enumerable.Range(0, BattleStations.Count).Where(s => men[s] > 0).Select(s => $"{BattleStations.Names[s]} {men[s]:0}")),
            Experience.Name(b.Experience), b.WorkUp, b.Readiness, b.Doctrine, b.Hits, b.Minutes, b.RangeM.Lo / 1000, b.RangeM.Hi / 1000,
            string.Join(" + ", b.Falls.Select(f => string.Format(Inv, "{0:0}% {1} {2}{3}", 100 * f.Share, f.Shooter, f.Battery, f.Kind != null ? " " + f.Kind : ""))),
            b.Man is var (cm, og) ? $"; her enemy in sight, casemates closed up {Sim.Ai.DoctrinePresets.NameOf(Sim.Ai.DoctrineField.ManCasemates, (double)cm)}, open guns {Sim.Ai.DoctrinePresets.NameOf(Sim.Ai.DoctrineField.ManOpenGuns, (double)og)}" : ""));
        Console.WriteLine(string.Format(Inv, "    killed {0:0.0} and wounded {1:0.0} a ship ({2:0.00} and {3:0.00} a hit); lost {4:0}% of runs; most shaken at once {5:0.#} (max {6}), frozen {7:0.#} (max {8})",
            (double)rs.Sum(r => r.Killed) / n, (double)rs.Sum(r => r.Wounded) / n, (double)rs.Sum(r => r.Killed) / hits, (double)rs.Sum(r => r.Wounded) / hits,
            100.0 * rs.Count(r => r.Lost >= 0) / n, rs.Average(r => r.MostShaken), rs.Max(r => r.MostShaken), rs.Average(r => r.MostFrozen), rs.Max(r => r.MostFrozen)));
        for (int i = 0; i < at.Length; i++)
        {
            var afloat = rs.Where(r => !double.IsNaN(r.Out[i])).ToList();
            if (afloat.Count == 0)
            {
                Console.WriteLine(string.Format(Inv, "    {0,4} min{1}  all lost", at[i], at[i] <= b.Minutes ? " " : "+"));
                continue;
            }
            double A(Func<Ship, double> f) => afloat.Average(f);
            Console.WriteLine(string.Format(Inv, "    {0,4} min{1}  out of action {2:0} (median; {3:0}-{4:0}): {5}; mounts {6}; conned {7:0}%; parties' men {8:0} (at fires {9:0}, on water {10:0}); reliefs come {12:0.#}, under cover {13:0.#}; shaken {14:0.#}, frozen {15:0.#}, officers out {16:0.#}{11}",
                at[i], at[i] <= b.Minutes ? " " : "+", Med(afloat.Select(r => r.Out[i])), Q(afloat.Select(r => r.Out[i]), 0.1), Q(afloat.Select(r => r.Out[i]), 0.9),
                string.Join(", ", Enumerable.Range(0, BattleStations.Count).Where(s => men[s] > 0 && A(r => r.Stations[i][s]) >= 0.05)
                    .Select(s => string.Format(Inv, "{0} {1:0.#}", BattleStations.Names[s], A(r => r.Stations[i][s])))),
                string.Join(", ", Enumerable.Range(0, c.Batteries.Length).Where(x => Mounts(c, x) > 0)
                    .Select(x => string.Format(Inv, "{0} {1:0.#}/{2}", HitsBench.Battery(c, x), A(r => r.Batteries[i][x]), Mounts(c, x)))),
                100 * A(r => r.Conned[i]), A(r => r.Parties[i]), A(r => r.AtFire[i]), A(r => r.OnWater[i]),
                afloat.Count < n ? string.Format(Inv, "; {0} of {1} afloat", afloat.Count, n) : "", A(r => r.Reliefs[i]), A(r => r.Covered[i]),
                A(r => r.Shaken[i]), A(r => r.Frozen[i]), A(r => r.Officers[i])));
        }
        Console.WriteLine($"    record: {cc.Record}");
        Console.WriteLine($"    wanted: {cc.Wanted}");
        Console.WriteLine(string.Format(Inv, "    ({0:0.0} s)", secs));
    }

    static int Mounts(ShipClass c, int battery)
    {
        int n = 0;
        foreach (ref readonly var m in c.Mounts)
            n += m.Battery == battery ? 1 : 0;
        return n;
    }
}
