using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen fire-history</c>: fire's yardstick (docs/battle/fire.md B5, "The yardstick"; damage/17 §9 T6).
/// Each case is a ship of the record's kind, her outfit, her orders before action and her side's doctrine, hit by the
/// record's weight of fire at random times over its minutes, through the world's damage system: the parties, the main,
/// smoke and the magazines all as in battle, the hits landing while her parties work. Run on for hours, each run says
/// when she was ablaze (developed fires in half her sections, 17 §9's measure), whether her fires were ever all out, the
/// spaces that burned, the water she took and how she ended. The first game's cases (1890-1925) lead; the later ones
/// check the mechanisms for later cuts.</summary>
static class FireHistory
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A share of a case's hits: a shooter's battery's shell, by kind (or its first).</summary>
    sealed record Fall(string Shooter, int Battery, string? Kind, double Share);

    /// <summary>A case of the record: the target (and the outfit she's given, by template; null: her own), the hits (n of
    /// them uniformly over the minutes, at a range and from a bearing), her readiness and doctrine, the hours run, what was
    /// recorded and what the plan wants.</summary>
    sealed record FireCase(string Era, string Name, string Target, string? Outfit, Fall[] Falls, int Hits, double Minutes, double RangeM,
        double AspectDeg, string Readiness, string Doctrine, double Hours, string Record, string Wanted);

    static readonly FireCase[] Cases =
    [
        // the US fleet's hits at Santiago were mostly its smaller guns' (Concas; 17 §7): mostly common, some AP
        new("1890-1925", "Santiago 1898: armoured cruiser, unstripped, unorganised, 30 hits in 45 min", "armoured_cruiser", "Victorian (1890)",
            [new("connecticut", 4, "common", 0.5), new("connecticut", 2, "common", 0.3), new("connecticut", 1, "ap", 0.1), new("connecticut", 0, "common", 0.1)],
            30, 45, 2_600, 60, "unprepared", "unorganised-1890", 4,
            "Teresa beached burning at 40 min, Oquendo 55, Vizcaya 100: burned out", "beyond help in most runs, in about 30-100 min"),
        new("1890-1925", "Same hits, stripped and prepared", "armoured_cruiser", "Victorian (1890)",
            [new("connecticut", 4, "common", 0.5), new("connecticut", 2, "common", 0.3), new("connecticut", 1, "ap", 0.1), new("connecticut", 0, "common", 0.1)],
            30, 45, 2_600, 60, "prepared", "prepared-1894", 4, "(Zhenyuan at the Yalu: fires kept in hand under heavier fire)", "later than unprepared"),
        new("1890-1925", "Yalu 1894: 12 hits in 45 min, unstripped, unorganised", "armoured_cruiser", "Victorian (1890)",
            [new("mikasa", 1, "common", 0.6), new("mikasa", 3, "common", 0.3), new("mikasa", 0, "common", 0.1)],
            12, 45, 3_000, 60, "unprepared", "unorganised-1890", 4, "Chaoyong, Yangwei lost to fire", "beyond help in most runs"),
        new("1890-1925", "Yalu 1894: 12 hits in 45 min, stripped and prepared", "armoured_cruiser", "Victorian (1890)",
            [new("mikasa", 1, "common", 0.6), new("mikasa", 3, "common", 0.3), new("mikasa", 0, "common", 0.1)],
            12, 45, 3_000, 60, "prepared", "prepared-1894", 4, "Matsushima, Hiei, Zhenyuan: fires kept in hand", "all fires out in most runs"),
        // Tsushima: Shimose bursting on contact (the 12" "AP"), the 6" powder common
        new("1890-1925", "Tsushima 1905: Suvorov, 60 hits in 40 min, prepared, russian-1905", "borodino", null,
            [new("mikasa", 0, "ap", 0.3), new("mikasa", 1, "common", 0.45), new("mikasa", 1, "ap", 0.25)],
            60, 40, 4_600, 80, "prepared", "russian-1905", 4, "ablaze fore and aft ~30 min after; burned ~5 h, sunk", "ablaze in about 20-40 min"),
        new("1890-1925", "Tsushima 1905: Orel, ~50 hits over the day (4 h), prepared, russian-1905", "borodino", null,
            [new("mikasa", 0, "ap", 0.2), new("mikasa", 1, "common", 0.5), new("mikasa", 1, "ap", 0.3)],
            50, 240, 5_500, 80, "prepared", "russian-1905", 5, "34 fires, each fought, survived; hose water heeled her",
            "many fires, mostly out; tens to hundreds of tonnes of water"),
        new("1890-1925", "Jutland 1916: RN battleship, 15 heavy hits in 30 min, closed up, rn-1916", "iron_duke", null,
            [new("seydlitz", 0, null, 0.8), new("seydlitz", 3, null, 0.2)],
            15, 30, 11_000, 80, "closed up", "rn-1916", 4, "fires rare and short (D.K. Brown); propellant the danger",
            "fires out in minutes; the danger the magazine"),
        new("later cuts", "Savo 1942: heavy cruiser, 1942 fit, 50 hits in 20 min", "heavy_cruiser", "Interwar (1939)",
            [new("heavy_cruiser", 0, "ap", 0.5), new("heavy_cruiser", 1, "he", 0.5)],
            50, 20, 5_000, 90, "unprepared", "usn-1942", 4, "Astoria, Quincy, Vincennes: never out", "never out in most runs"),
        new("later cuts", "Same hits, post-Savo fit", "heavy_cruiser", "Post-Savo (1943)",
            [new("heavy_cruiser", 0, "ap", 0.5), new("heavy_cruiser", 1, "he", 0.5)],
            50, 20, 5_000, 90, "closed up", "usn-1944", 4, "later USN cruisers controlled their fires", "out within ~30 min in nearly all"),
    ];

    /// <summary>One run's ship: when she was ablaze, her fires all out after the last hit (NaN: never), and her state at the
    /// end.</summary>
    sealed class Ship
    {
        public int Entity, HitFires, Hits;
        public double AblazeMin = double.NaN, OutMin = double.NaN, LostMin = double.NaN, LastHitMin;
        public int Burning, Developed, Smoked, Flooded;
        public double WaterT, Power = 1, Men;
        public LossKind Loss;
        public bool Afloat = true, ByFire;
    }

    public static int Run(string settingsPath, int runs, string? only, double? hours, Func<string, Design> load, bool trace = false)
    {
        var settings = SimSettings.Load(settingsPath);
        var classes = new Dictionary<string, ShipClass>();
        ShipClass Cls(string name, string? outfit)
        {
            string key = name + "|" + outfit;
            if (classes.TryGetValue(key, out var c))
                return c;
            var d = load(name);
            if (outfit != null)
                d = d with { Outfit = (Outfits.Get(outfit) ?? throw new ArgumentException($"no outfit template \"{outfit}\"")).Value };
            return classes[key] = ShipClassCompiler.Compile(ShipDesign.Build(d));
        }
        Console.WriteLine($"fire's yardstick (docs/battle/fire.md B5): {runs} runs a case, the hits at random over the case's minutes, " +
                          "through the world's damage system; ablaze: developed fires in half her sections");
        string era = "";
        foreach (var fc in Cases)
        {
            if (only != null && !fc.Name.Contains(only, StringComparison.OrdinalIgnoreCase))
                continue;
            if (fc.Era != era)
                Console.WriteLine($"\n{(era = fc.Era)}");
            var sw = Stopwatch.StartNew();
            var target = Cls(fc.Target, fc.Outfit);
            var res = One(fc, target, n => Cls(n, null), settings, runs, hours ?? fc.Hours, trace);
            Print(fc, target, res, settings, hours ?? fc.Hours, sw.Elapsed.TotalSeconds);
        }
        return 0;
    }

    /// <summary>A case's runs: every ship in one world, far apart, her hits at their times, sampled a minute at a time.</summary>
    static List<Ship> One(FireCase fc, ShipClass target, Func<string, ShipClass> cls, SimSettings settings, int runs, double hours, bool trace)
    {
        var s = settings with { FireReadinessDefault = fc.Readiness, FirefightingDefault = fc.Doctrine };
        if (!s.FireReadiness.Any(x => x.Name == fc.Readiness) || !s.Firefighting.Any(x => x.Name == fc.Doctrine))
            throw new ArgumentException($"{fc.Name}: no readiness \"{fc.Readiness}\" or doctrine \"{fc.Doctrine}\"");
        var aims = fc.Falls.Select(f =>
        {
            var sc = cls(f.Shooter);
            var (shell, _) = HitsBench.ShellOf(sc, f.Battery, f.Kind, null);
            double maxElev = sc.Mounts[sc.Links(sc.Batteries[f.Battery].Mounts)[0]].MaxElevationDeg;
            return new GivenHits.Aim(target, shell, maxElev, fc.RangeM, fc.AspectDeg, s);
        }).ToArray();
        double total = fc.Falls.Sum(f => f.Share);
        var start = WorldTime.FromUtc(1916, 5, 31, 14);
        var w = new World(start, 1, s);
        int ci = w.Ships.AddClass(target);
        var rng = RandomStream.Named(1, "fire history");
        var list = new List<Ship>();
        var due = new List<(long Ms, int Run, int Aim)>();
        for (int i = 0; i < runs; i++)
        {
            w.Apply(ShipSystem.Spawn(ci, 50 + 0.05 * (i % 20), -20 - 0.1 * (i / 20), 0, 8));   // the open Atlantic, 5 km apart
            list.Add(new Ship { Entity = w.Ships.NewestEntity });
            for (int h = 0; h < fc.Hits; h++)
            {
                double u = rng.NextDouble() * total, acc = 0;
                int a = 0;
                for (; a < fc.Falls.Length - 1; a++)
                    if ((acc += fc.Falls[a].Share) > u)
                        break;
                due.Add(((long)(rng.NextDouble() * fc.Minutes * 60_000), i, a));
            }
        }
        due.Sort();
        foreach (var r in list)
            r.LastHitMin = due.Where(d => list[d.Run] == r).Select(d => d.Ms / 60_000.0).DefaultIfEmpty(0).Max();
        if (trace)
        {
            // her fire spaces by kind and use: how many, their area and load as she's ordered
            var k0 = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(list[0].Entity));
            foreach (var g in target.FireSpaces.ToArray().GroupBy(x => (x.Kind, x.Use)).OrderBy(g => g.Key))
                Console.WriteLine(string.Format(Inv, "      {0} {1}: {2}, area {3:0} m² (median), load {4:0} MJ/m², room {5:0} m²", g.Key.Kind, g.Key.Use, g.Count(),
                    g.Select(x => x.AreaM2).Order().ElementAt(g.Count() / 2), g.Average(x => Fires.Load(x, k0)), g.Average(x => Fires.Room(x, s))));
        }
        var path = new HitPath();
        int next = 0;
        var store = w.Ships.Store;
        int sections = target.Sections.Length;
        var lit = new bool[sections];
        for (int m = 1; m <= hours * 60; m++)
        {
            long end = m * 60_000L;
            for (; next < due.Count && due[next].Ms < end; next++)
            {
                var (ms, ri, ai) = due[next];
                var r = list[ri];
                w.AdvanceTo(start + ms);
                int slot = store.SlotOf(r.Entity);
                if (slot < 0 || store.Combat.Condition(slot).Status != ShipStatus.Afloat)
                    continue;
                var hit = aims[ai].Next(w, r.Entity, slot, path, ref rng, out _);
                w.Damage.Strike(w, hit);
                r.Hits++;
                r.HitFires += w.Damage.LastEffects.Fire ? 1 : 0;
                if (trace && ri == 0)
                    Console.WriteLine(string.Format(Inv, "      {0,5:0.0} min  hit {1}: {2}, burst {3}{4}", ms / 60_000.0, r.Hits, w.Damage.LastTerminal.Outcome,
                        w.Damage.LastTerminal.Burst, w.Damage.LastEffects.Fire ? ", FIRE" : ""));
            }
            w.AdvanceTo(start + end);
            foreach (var r in list)
            {
                if (!r.Afloat)
                    continue;
                int slot = store.SlotOf(r.Entity);
                var k = slot >= 0 ? store.Combat.Condition(slot) : null;
                if (k == null || k.Status != ShipStatus.Afloat)
                {
                    (r.Afloat, r.LostMin) = (false, m);
                    if (k != null)
                        Read(r, k);
                    continue;
                }
                // ablaze: a developed fire (or a big one in the open) in half her sections
                Array.Clear(lit);
                var spaces = target.FireSpaces;
                for (int i = 0; i < spaces.Length; i++)
                {
                    ref readonly var x = ref k.Fire[i];
                    if (x.Phase is BurnPhase.Developed or BurnPhase.Decay || spaces[i].OpenAir && x.Burning && x.Q > 1)
                        lit[spaces[i].Section] = true;
                }
                if (double.IsNaN(r.AblazeMin) && lit.Count(b => b) * 2 >= sections)
                    r.AblazeMin = m;
                if (m >= r.LastHitMin)
                {
                    if (k.Fires == 0 && double.IsNaN(r.OutMin))
                        r.OutMin = m;
                    else if (k.Fires > 0)
                        r.OutMin = double.NaN;   // lit again
                }
                Read(r, k);
                if (trace && r == list[0] && (m <= 60 || m % 10 == 0))
                    Trace(target, k, m);
            }
        }
        return list;
    }

    /// <summary>A run's fires at a minute: each burning space, its phase, heat and water, and the parties and the main.</summary>
    static void Trace(ShipClass c, ShipCondition k, int m)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < c.FireSpaces.Length; i++)
        {
            ref readonly var x = ref k.Fire[i];
            if (!x.Burning)
                continue;
            ref readonly var row = ref c.FireSpaces[i];
            sb.Append(string.Format(Inv, " [{0}{1} s{2} {3} {4:0.0}MW {5:0}L/{6:0}m²]", row.Kind.ToString()[0], row.Use, row.Section, x.Phase, x.Q,
                k.FireFlow[i].FogLpm, x.Involved));
        }
        int cut = k.MainCutMs.Count(t => t > 0), sent = k.Parties.Take(k.PartyCount).Count(p => p.Target >= 0);
        Console.WriteLine(string.Format(Inv, "      {0,5} min  fires {1}, main cut in {2} sections, parties {3} of {4} at work, power {5:0%}, men {6:0}:{7}",
            m, k.Fires, cut, sent, k.PartyCount, k.PowerCap, k.Parties.Take(k.PartyCount).Sum(p => p.Men), sb));
    }

    static void Read(Ship r, ShipCondition k)
    {
        (r.Burning, r.WaterT, r.Power, r.Men, r.Loss) = (k.Fires, k.WaterT, k.PowerCap, k.Killed + k.Wounded, k.Loss);
        (r.Developed, r.Smoked, r.Flooded) = (0, 0, 0);
        foreach (ref readonly var x in k.Fire.AsSpan())
            r.Developed += x.DevS > 0 ? 1 : 0;
        foreach (bool b in k.Smoked)
            r.Smoked += b ? 1 : 0;
        foreach (bool b in k.MagazineFlood)
            r.Flooded += b ? 1 : 0;
        for (int m = 0; m < k.MagazineHeat.Length; m++)
            r.ByFire |= k.MagazineLimit[m] > 0 && k.MagazineHeat[m] >= k.MagazineLimit[m];
    }

    static void Print(FireCase fc, ShipClass target, List<Ship> rs, SimSettings s, double hours, double secs)
    {
        int n = rs.Count;
        double Share(Func<Ship, bool> f) => 100.0 * rs.Count(f) / n;
        string Med(IEnumerable<double> xs, string fmt = "0")
        {
            var v = xs.Where(x => !double.IsNaN(x)).Order().ToList();
            return v.Count == 0 ? "-" : v[v.Count / 2].ToString(fmt, Inv);
        }
        Console.WriteLine($"  {fc.Name}");
        Console.WriteLine($"    {target.Name}{(fc.Outfit != null ? $", outfit {fc.Outfit}" : "")}; {fc.Readiness}, {fc.Doctrine}; " +
                          string.Join(" + ", fc.Falls.Select(f => string.Format(Inv, "{0:0}% {1} battery {2}{3}", 100 * f.Share, f.Shooter, f.Battery,
                              f.Kind != null ? " " + f.Kind : ""))) + string.Format(Inv, " at {0:0} m", fc.RangeM));
        Console.WriteLine(string.Format(Inv, "    fires lit by hits {0} a ship (median), spaces burned developed {1}; ablaze {2:0}% of runs, median {3} min; " +
            "all out after the last hit {4:0}% (median {5} min); still burning at {6:0.#} h {7:0}%, {8} spaces",
            Med(rs.Select(r => (double)r.HitFires)), Med(rs.Select(r => (double)r.Developed)), Share(r => !double.IsNaN(r.AblazeMin)),
            Med(rs.Select(r => r.AblazeMin)), Share(r => !double.IsNaN(r.OutMin) && r.Afloat), Med(rs.Select(r => r.Afloat ? r.OutMin : double.NaN)),
            hours, Share(r => r.Afloat && r.Burning > 0), Med(rs.Where(r => r.Afloat).Select(r => (double)r.Burning))));
        var lost = rs.Where(r => !r.Afloat).ToList();
        Console.WriteLine(string.Format(Inv, "    water {0} t (median), power {1:0}%, killed and wounded {2}, machinery abandoned to smoke {3}, magazines flooded {4}; " +
            "lost {5:0}%{6}", Med(rs.Select(r => r.WaterT)), 100 * rs.Average(r => r.Power), Med(rs.Select(r => r.Men)),
            Med(rs.Select(r => (double)r.Smoked), "0.#"), rs.Sum(r => r.Flooded) / (double)n, 100.0 * lost.Count / n,
            lost.Count == 0 ? "" : " (" + string.Join(", ", lost.GroupBy(r => r.Loss == LossKind.Magazine ? r.ByFire ? "magazine by a fire" : "magazine by a hit" : r.Loss.ToString())
                .Select(g => string.Format(Inv, "{0} {1}, median {2} min", g.Key, g.Count(),
                Med(g.Select(r => r.LostMin))))) + ")"));
        Console.WriteLine($"    record: {fc.Record}");
        Console.WriteLine($"    wanted: {fc.Wanted}");
        Console.WriteLine(string.Format(Inv, "    ({0:0.0} s)", secs));
    }
}
