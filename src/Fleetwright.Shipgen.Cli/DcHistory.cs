using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen dc-history</c>: damage control's yardstick (docs/battle/damage-control.md DC0, "The yardstick").
/// Each case is a ship of the record's kind, her orders before action and her side's doctrine, given the record's damage
/// (shell hits at random over their minutes, torpedoes, holes in her side) and the record's speeds, through the world's
/// damage system; run on for hours, each run says the water she took by the hour, her worst list and her list and trim
/// at the end, her speed and power, and how and when she was lost. The first game's cases (1890-1925) lead, the
/// research's mechanism checks (09 §8, 17 §6) next, the later cuts' last. A case marked (?) is from general knowledge
/// and isn't tuned to until the research request (2026-10-20) is back (the plan's decision 7).</summary>
static class DcHistory
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A share of a case's shell hits: a shooter's battery's shell, by kind (or its first).</summary>
    sealed record Fall(string Shooter, int Battery, string? Kind, double Share);

    enum BlowKind : byte { Shells, Torpedo, Hole, Splinters, Speed }

    /// <summary>What a case does to her, at a minute: shell hits (n at random between the minutes, at a range from a
    /// bearing), a torpedo (a share of her length from the stern, a depth, a side, whose torpedo), a hole in her side (a
    /// share of her length from the stern, a height over the waterline, its area), splinter holes (n holes of a diameter
    /// in a band along her side and of heights, at random), or an order of speed.</summary>
    sealed record Blow(BlowKind Kind, double AtMin)
    {
        public double ToMin { get; init; }
        public int N { get; init; } = 1;
        public double Share { get; init; }
        public double ShareTo { get; init; }
        public double Z { get; init; }
        public double ZTo { get; init; }
        public double Area { get; init; }
        public double DMin { get; init; }
        public double DMax { get; init; }
        public bool Port { get; init; }
        public double Kn { get; init; }
        public double D { get; init; }
        public double RangeM { get; init; }
        public double AspectDeg { get; init; }
        public string? From { get; init; }
        public Fall[] Falls { get; init; } = [];

        public static Blow Shells(double fromMin, double toMin, int n, double rangeM, double aspectDeg, params Fall[] falls) =>
            new(BlowKind.Shells, fromMin) { ToMin = toMin, N = n, RangeM = rangeM, AspectDeg = aspectDeg, Falls = falls };
        public static Blow Torpedo(double atMin, double share, double depthM, bool port = false, string from = "v25") =>
            new(BlowKind.Torpedo, atMin) { Share = share, Z = depthM, Port = port, From = from };
        /// <summary>A hole; d over 0 springs the holed space's boundaries at that load, as its shell's burst would.</summary>
        public static Blow Hole(double atMin, double share, double zOverWl, double areaM2, bool port = false, double d = 0) =>
            new(BlowKind.Hole, atMin) { Share = share, Z = zOverWl, Area = areaM2, Port = port, D = d };
        public static Blow Splinters(double atMin, int n, double dMinM, double dMaxM, double share, double shareTo, double z, double zTo) =>
            new(BlowKind.Splinters, atMin) { N = n, DMin = dMinM, DMax = dMaxM, Share = share, ShareTo = shareTo, Z = z, ZTo = zTo };
        public static Blow Speed(double atMin, double kn) => new(BlowKind.Speed, atMin) { Kn = kn };

        public override string ToString() => Kind switch
        {
            BlowKind.Shells => string.Format(Inv, "{0} hits {1:0}-{2:0} min at {3:0} m ({4})", N, AtMin, ToMin, RangeM,
                string.Join(" + ", Falls.Select(f => string.Format(Inv, "{0:0}% {1} {2}{3}", 100 * f.Share, f.Shooter, f.Battery, f.Kind != null ? " " + f.Kind : "")))),
            BlowKind.Torpedo => string.Format(Inv, "{0}'s torpedo at {1:0} min, {2:0.00} from the stern, {3:0.#} m down{4}", From, AtMin, Share, Z, Port ? ", port" : ""),
            BlowKind.Hole => string.Format(Inv, "a {0:0.##} m² hole at {1:0} min, {2:0.00} from the stern, {3:+0.#;-0.#} m{4}{5}", Area, AtMin, Share, Z, Port ? ", port" : "",
                D > 0 ? string.Format(Inv, ", its boundaries sprung at D {0:0.#}", D) : ""),
            BlowKind.Splinters => string.Format(Inv, "{0} holes of {1:0}-{2:0} cm at {3:0} min, {4:0.00}-{5:0.00} from the stern, {6:+0.#;-0.#} to {7:+0.#;-0.#} m",
                N, 100 * DMin, 100 * DMax, AtMin, Share, ShareTo, Z, ZTo),
            _ => string.Format(Inv, "{0:0} kn at {1:0} min", Kn, AtMin),
        };
    }

    /// <summary>A case: the target, her speed at the start, what's done to her, her readiness and doctrine, the hours run,
    /// what was recorded, what the plan wants, and where the research has it.</summary>
    sealed record DcCase(string Era, string Name, string Target, double Kn, Blow[] Blows, string Readiness, string Doctrine, double Hours,
        string Record, string Wanted, string Basis);

    static readonly DcCase[] Cases =
    [
        new("1890-1925", "Seydlitz, Jutland 1916: a torpedo forward and heavy shells in the bow", "seydlitz", 24,
            [Blow.Torpedo(10, 0.78, 4), Blow.Hole(30, 0.9, 0.2, 1.2, d: 2), Blow.Hole(60, 0.88, -0.5, 0.6, port: true, d: 2),
             Blow.Shells(0, 120, 12, 13_000, 60, new Fall("lion", 0, null, 1)), Blow.Speed(240, 7)],
            "closed up", "german-1916", 10, "5,308 t; slowed, steamed stern first to ease the forward bulkheads, reached harbour",
            "survives in most runs when she slows; loses her forward bulkheads if she holds speed", "17 §1.3, 09 §2.2"),
        new("1890-1925", "Seydlitz's damage, holding 24 kn throughout (against the record's slowing)", "seydlitz", 24,
            [Blow.Torpedo(10, 0.78, 4), Blow.Hole(30, 0.9, 0.2, 1.2, d: 2), Blow.Hole(60, 0.88, -0.5, 0.6, port: true, d: 2),
             Blow.Shells(0, 120, 12, 13_000, 60, new Fall("lion", 0, null, 1))],
            "closed up", "german-1916", 10, "(Bismarck kept her speed and gave up her repair)",
            "more water than slowing; her forward bulkheads may give way", "09 §2.2"),
        new("1890-1925", "Lützow, Jutland 1916: the torpedo flat holed by two heavy shells", "derfflinger", 22,
            [Blow.Hole(0, 0.86, -2, 1.0, d: 2.5), Blow.Hole(1, 0.84, -2.5, 1.0, d: 2.5), Blow.Shells(0, 60, 16, 9_000, 70, new Fall("lion", 0, null, 1)),
             Blow.Speed(60, 15), Blow.Speed(180, 7), Blow.Speed(300, 3)],
            "closed up", "german-1916", 8, "1,038 t at 3 h, 2,395 t at 3¾ h, 8,351 t by ~7½ h (4,209 below the armour deck); scuttled",
            "thousands of tonnes walking aft over hours; lost or close to it in many runs", "17 §1.3, §6.2"),
        new("1890-1925", "Audacious 1914: a mine under the port engine room, not at action stations", "king_george_v", 15,
            [Blow.Torpedo(0, 0.38, 7.5, port: true), Blow.Speed(30, 0)],
            "unprepared", "rn-1916", 13, "engine rooms abandoned ~2 h, power lost, capsized ~12 h after the mine",
            "unprepared closures: lost over hours; closed up and drilled: survives", "17 §1.3"),
        new("1890-1925", "Oslyabya, Tsushima 1905: waterline holes forward, kept her place (?)", "oslyabya", 9,
            [Blow.Hole(0, 0.88, 0.1, 2.0, port: true, d: 2.5), Blow.Hole(5, 0.85, -0.3, 1.0, port: true, d: 2.5),
             Blow.Shells(0, 40, 20, 4_600, 100, new Fall("mikasa", 0, "ap", 0.4), new Fall("mikasa", 1, "common", 0.6))],
            "prepared, cluttered", "russian-1905", 3, "listed to port and capsized within about an hour of the first hits (?)",
            "at speed with holes forward at the waterline: lost within about an hour in most runs", "general knowledge (?)"),
        new("1890-1925", "Marlborough, Jutland 1916: one torpedo abreast the forward boiler room (?)", "iron_duke", 17,
            [Blow.Torpedo(0, 0.62, 4), Blow.Speed(360, 12)],
            "closed up", "rn-1916", 10, "listed ~7°, kept her place for hours, then slowed and went home (?)",
            "survives; a few degrees of list after counterflooding", "general knowledge (?)"),
        new("1890-1925", "A pre-dreadnought at anchor, 1915: one torpedo amidships (?)", "lord_nelson", 0,
            [Blow.Torpedo(0, 0.5, 4)],
            "closed up", "rn-1916", 3, "Majestic and Triumph capsized minutes after one torpedo (?)",
            "capsizes or founders after one or two torpedoes, within minutes to hours", "general knowledge (?)"),
        new("1890-1925", "A pre-dreadnought under way at night, 1915: two torpedoes (?)", "lord_nelson", 10,
            [Blow.Torpedo(0, 0.55, 4), Blow.Torpedo(20, 0.35, 4)],
            "unprepared", "rn-1916", 4, "Formidable sank ~2¾ h after the first torpedo (?)",
            "capsizes or founders after one or two torpedoes, within minutes to hours", "general knowledge (?)"),
        new("1890-1925", "RN battleship, Jutland 1916: 15 heavy hits in 30 min", "iron_duke", 18,
            [Blow.Shells(0, 30, 15, 11_000, 80, new Fall("seydlitz", 0, null, 0.8), new Fall("seydlitz", 3, null, 0.2))],
            "closed up", "rn-1916", 4, "the battleships hit at Jutland all came home",
            "survives; tens to a few hundred tonnes", "08, fighting.md 4.5"),
        new("1890-1925", "Destroyer under shellfire: 6 light-cruiser HE hits in 10 min", "destroyer", 25,
            [Blow.Shells(0, 10, 6, 5_000, 90, new Fall("arethusa", 0, "he", 1))],
            "closed up", "rn-1916", 3, "shell hits rarely flooded beyond the hit space where the crew lived and the pumps had power (WDR 51)",
            "most survive unless the boilers or a magazine go", "17 §6.5"),

        new("mechanism checks", "30 splinter holes along one side, at 18 kn (09 §8)", "super_dreadnought", 18,
            [Blow.Splinters(0, 30, 0.03, 0.12, 0.15, 0.85, -1, 2.5)],
            "closed up", "rn-1916", 8, "(the research's model) drilled ~120 t; no damage control ~2,950 t and 18° at 8 h",
            "drilled: ~120 t; nobody: thousands of tonnes and a heavy list", "09 §8"),
        new("mechanism checks", "A heavy shell through the bow near the waterline, holds 28 kn (09 §8)", "lion", 28,
            [Blow.Hole(0, 0.88, 0.8, 1.2, d: 2), Blow.Hole(0, 0.88, -0.4, 0.4)],
            "closed up", "rn-1916", 4, "(the research's model) forward bulkheads collapse at 8 min, ~10,400 t",
            "forward bulkheads give way; thousands of tonnes", "09 §8, §2.2"),
        new("mechanism checks", "The same, slowed to 12 kn at 4 min (09 §8)", "lion", 28,
            [Blow.Hole(0, 0.88, 0.8, 1.2, d: 2), Blow.Hole(0, 0.88, -0.4, 0.4), Blow.Speed(4, 12)],
            "closed up", "rn-1916", 4, "(the research's model) ~1,900 t", "a fifth of holding 28 kn", "09 §8, §2.2"),

        new("later cuts", "North Carolina 1942: one torpedo abreast turret A, at 19 kn", "super_dreadnought", 19,
            [Blow.Torpedo(0, 0.78, 5, port: true)],
            "closed up", "usn-1942", 4, "970 t; 5.5° list off in 6 min with ~480 t counterflooded; slow flooding negligible",
            "~1,000 t; the list off within ~10 min", "09 §8, WDR 61"),
        new("later cuts", "Shinano-like: 4 torpedoes on one side at 18 kn, unprepared", "super_dreadnought", 18,
            [Blow.Torpedo(0, 0.6, 5), Blow.Torpedo(0, 0.45, 5), Blow.Torpedo(0, 0.35, 5), Blow.Torpedo(0, 0.25, 5)],
            "unprepared", "usn-1942", 8, "Shinano: list 10-13°, 20° at 2¾ h, lost at 7¾ h, run at speed throughout",
            "unsealed and weak crew: lost after 4-8 h; tight and drilled: lives", "09 §8, 17 §1.3"),
    ];

    /// <summary>One run's ship: the water by the hour, her worst list, how she ended.</summary>
    sealed class Ship
    {
        public int Entity;
        public double[] WaterAt = [];
        public double MaxList, List, Trim, WaterT, Power = 1, Kn, LostMin = double.NaN;
        public int Sprung, Collapsed, DoorsOpen;
        /// <summary>Her holes by class (DC2): how many and their area still open, m².</summary>
        public readonly int[] HolesN = new int[ShipCondition.HoleClasses];
        public readonly double[] HolesM2 = new double[ShipCondition.HoleClasses];
        public LossKind Loss;
        public bool Afloat = true;
    }

    static readonly double[] Hours = [0.5, 1, 2, 4, 8, 12];

    public static int Run(string settingsPath, int runs, string? only, double? hours, Func<string, Design> load, bool trace = false)
    {
        var settings = SimSettings.Load(settingsPath);
        var classes = new Dictionary<string, ShipClass>();
        ShipClass Cls(string name) => classes.TryGetValue(name, out var c) ? c : classes[name] = ShipClassCompiler.Compile(ShipDesign.Build(load(name)));
        Console.WriteLine($"damage control's yardstick (docs/battle/damage-control.md DC0): {runs} runs a case, the case's damage and speeds " +
                          "through the world's damage system; (?) marks a case from general knowledge, read but not tuned to");
        string era = "";
        foreach (var dc in Cases)
        {
            if (only != null && !dc.Name.Contains(only, StringComparison.OrdinalIgnoreCase))
                continue;
            if (dc.Era != era)
                Console.WriteLine($"\n{(era = dc.Era)}");
            var sw = Stopwatch.StartNew();
            var target = Cls(dc.Target);
            double h = hours ?? dc.Hours;
            var res = One(dc, target, Cls, settings, runs, h, trace);
            Print(dc, target, res, h, sw.Elapsed.TotalSeconds);
        }
        return 0;
    }

    /// <summary>A case's runs: every ship in one world, far apart, her blows at their times, sampled a minute at a time.</summary>
    static List<Ship> One(DcCase dc, ShipClass target, Func<string, ShipClass> cls, SimSettings settings, int runs, double hours, bool trace)
    {
        var s = settings with { FireReadinessDefault = dc.Readiness, FirefightingDefault = dc.Doctrine };
        if (!s.FireReadiness.Any(x => x.Name == dc.Readiness) || !s.Firefighting.Any(x => x.Name == dc.Doctrine))
            throw new ArgumentException($"{dc.Name}: no readiness \"{dc.Readiness}\" or doctrine \"{dc.Doctrine}\"");
        // each shell fall's aim, by blow and fall
        var aims = dc.Blows.Select(b => b.Falls.Select(f =>
        {
            var sc = cls(f.Shooter);
            var (shell, _) = HitsBench.ShellOf(sc, f.Battery, f.Kind, null);
            double maxElev = sc.Mounts[sc.Links(sc.Batteries[f.Battery].Mounts)[0]].MaxElevationDeg;
            return new GivenHits.Aim(target, shell, maxElev, b.RangeM, b.AspectDeg, s);
        }).ToArray()).ToArray();
        var torpedoes = dc.Blows.Select(b =>
        {
            if (b.Kind != BlowKind.Torpedo)
                return default;
            var sc = cls(b.From!);
            if (sc.Launchers.Length == 0)
                throw new ArgumentException($"{dc.Name}: {sc.Name} has no torpedoes");
            return sc.Torpedoes[sc.Launchers[0].Torpedo];
        }).ToArray();
        double x0 = double.MaxValue, x1 = double.MinValue;
        foreach (var p in target.Solids.Waterline)
            (x0, x1) = (Math.Min(x0, p.X), Math.Max(x1, p.X));

        var start = WorldTime.FromUtc(1916, 5, 31, 14);
        var w = new World(start, 1, s);
        int ci = w.Ships.AddClass(target);
        var rng = RandomStream.Named(1, "dc history");
        var list = new List<Ship>();
        var due = new List<(long Ms, int Run, int Blow, int Sub)>();
        for (int i = 0; i < runs; i++)
        {
            w.Apply(ShipSystem.Spawn(ci, 50 + 0.05 * (i % 20), -20 - 0.1 * (i / 20), 0, dc.Kn));   // the open Atlantic, 5 km apart
            list.Add(new Ship { Entity = w.Ships.NewestEntity, WaterAt = new double[Hours.Length] });
            for (int b = 0; b < dc.Blows.Length; b++)
            {
                var bl = dc.Blows[b];
                if (bl.Kind == BlowKind.Shells)
                    for (int h = 0; h < bl.N; h++)
                        due.Add(((long)((bl.AtMin + rng.NextDouble() * (bl.ToMin - bl.AtMin)) * 60_000), i, b, h));
                else
                    due.Add(((long)(bl.AtMin * 60_000), i, b, 0));
            }
        }
        due.Sort();
        var path = new HitPath();
        int next = 0;
        var store = w.Ships.Store;
        for (int m = 1; m <= hours * 60; m++)
        {
            long end = m * 60_000L;
            for (; next < due.Count && due[next].Ms < end; next++)
            {
                var (ms, ri, bi, _) = due[next];
                var r = list[ri];
                w.AdvanceTo(start + ms);
                int slot = store.SlotOf(r.Entity);
                if (slot < 0 || store.Combat.Condition(slot).Status != ShipStatus.Afloat)
                    continue;
                var bl = dc.Blows[bi];
                switch (bl.Kind)
                {
                    case BlowKind.Shells:
                    {
                        double u = rng.NextDouble() * bl.Falls.Sum(f => f.Share), acc = 0;
                        int a = 0;
                        for (; a < bl.Falls.Length - 1; a++)
                            if ((acc += bl.Falls[a].Share) > u)
                                break;
                        w.Damage.Strike(w, aims[bi][a].Next(w, r.Entity, slot, path, ref rng, out _));
                        break;
                    }
                    case BlowKind.Torpedo:
                    {
                        double x = x0 + bl.Share * (x1 - x0), z = target.Vertical.Waterline - bl.Z;
                        var (cell, y) = FloodModel.SideCell(target, x, z, !bl.Port);
                        if (cell < 0)
                            throw new ArgumentException($"{dc.Name}: no hull at {bl.Share:0.00} of {target.Name}, {bl.Z:0.0} m down");
                        w.Damage.Strike(w, new Hit
                        {
                            Ship = r.Entity, Ms = w.Time.Ms, Point = new P3(x, y, z), Direction = new P3(0, bl.Port ? 1 : -1, 0), SpeedMs = 20,
                            Source = HitSource.Torpedo, Torpedo = torpedoes[bi], ShellType = -1,
                        });
                        break;
                    }
                    case BlowKind.Hole:
                    {
                        double x = x0 + bl.Share * (x1 - x0);
                        w.Apply(DamageSystem.SideHole(r.Entity, HoleAt.X, x, bl.Z, bl.Area, !bl.Port));
                        var (cell, _) = FloodModel.SideCell(target, x, target.Vertical.Waterline + bl.Z, !bl.Port);
                        if (bl.D > 0 && cell >= 0)
                            Leaks.SpringCell(target, store.Combat.Condition(slot), cell, bl.D, s, ref rng);
                        break;
                    }
                    case BlowKind.Splinters:
                        for (int h = 0; h < bl.N; h++)
                        {
                            double d = bl.DMin + rng.NextDouble() * (bl.DMax - bl.DMin);
                            double x = x0 + (bl.Share + rng.NextDouble() * (bl.ShareTo - bl.Share)) * (x1 - x0);
                            w.Apply(DamageSystem.SideHole(r.Entity, HoleAt.X, x, bl.Z + rng.NextDouble() * (bl.ZTo - bl.Z), Math.PI * d * d / 4));
                        }
                        break;
                    case BlowKind.Speed:
                        w.Apply(ShipSystem.Speed(r.Entity, bl.Kn));
                        break;
                }
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
                    (r.Afloat, r.LostMin, r.Loss) = (false, m, k?.Loss ?? LossKind.None);
                    continue;
                }
                (r.WaterT, r.List, r.Trim, r.Power, r.Kn) = (k.WaterT, Math.Abs(k.Attitude.HeelDeg), k.Attitude.TrimDeg, k.PowerCap,
                    store[slot].Speed / 0.514444);
                (r.Sprung, r.Collapsed, r.DoorsOpen) = (0, 0, 0);
                Array.Clear(r.HolesN);
                Array.Clear(r.HolesM2);
                for (int h = 0; h < k.HoleOpenN.Length; h++)
                    (r.HolesN[h % ShipCondition.HoleClasses], r.HolesM2[h % ShipCondition.HoleClasses]) =
                        (r.HolesN[h % ShipCondition.HoleClasses] + k.HoleOpenN[h], r.HolesM2[h % ShipCondition.HoleClasses] + k.HoleOpenM2[h]);
                for (int j = 0; j < k.JoinMarks.Length; j++)
                {
                    r.Sprung += k.JoinSeamM2[j] > 0 || k.JoinClosureM2[j] > 0 ? 1 : 0;
                    r.Collapsed += (k.JoinMarks[j] & JoinMark.Collapsed) != 0 ? 1 : 0;
                    r.DoorsOpen += (k.JoinMarks[j] & JoinMark.DoorOpen) != 0 ? 1 : 0;
                }
                r.MaxList = Math.Max(r.MaxList, r.List);
                for (int h = 0; h < Hours.Length; h++)
                    if (Math.Abs(m - Hours[h] * 60) < 0.5)
                        r.WaterAt[h] = r.WaterT;
                if (trace && r == list[0] && (m <= 30 || m % 10 == 0))
                    Console.WriteLine(string.Format(Inv, "      {0,5} min  water {1,6:0} t, list {2,5:0.0}°, trim {3,5:+0.00;-0.00}°, GM {4,5:0.00} m, " +
                        "speed {5,4:0.0} kn, power {6:0%}, holes {7:0.00} m²", m, k.WaterT, k.Attitude.HeelDeg, k.Attitude.TrimDeg,
                        k.GmM, r.Kn, k.PowerCap, k.BreachM2.Sum()));
            }
        }
        if (trace)
        {
            // the first run's sprung boundaries at the end: each one's cells, plate, height, rating and the water either side
            int slot = store.SlotOf(list[0].Entity);
            var k = slot >= 0 ? store.Combat.Condition(slot) : null;
            var f = w.Damage.Flood(target);
            for (int ei = 0; k != null && ei < f.Edges.Length; ei++)
            {
                var e = f.Edges[ei];
                if (k.JoinSeamM2[e.Pool] <= 0 && k.JoinClosureM2[e.Pool] <= 0 && k.JoinMarks[e.Pool] == JoinMark.None)
                    continue;
                Console.WriteLine(string.Format(Inv, "      edge {0}-{1} {2} at {3:0.0} m: built {4:0.0} m, rating {5:0.0} m, D {6:0.0}, seam {7:0} cm² ×{8:0.0}, closures {9:0} cm², {10}; " +
                    "water {11:0}/{12:0} and {13:0}/{14:0} m³", e.A, e.B, e.Join, e.Z, f.BuiltM[ei], Flooding.Rating(f, k, ei, e.Pool, s), k.JoinD[e.Pool],
                    k.JoinSeamM2[e.Pool] * 1e4, k.JoinWork[e.Pool], k.JoinClosureM2[e.Pool] * 1e4, k.JoinMarks[e.Pool], k.WaterM3[e.A], f.Capacity[e.A],
                    k.WaterM3[e.B], f.Capacity[e.B]));
            }
        }
        // a lost ship's water by the hour after she went is her last
        foreach (var r in list)
            for (int h = 0; h < Hours.Length; h++)
                if (!r.Afloat && Hours[h] * 60 >= r.LostMin)
                    r.WaterAt[h] = double.NaN;
        return list;
    }

    static void Print(DcCase dc, ShipClass target, List<Ship> rs, double hours, double secs)
    {
        int n = rs.Count;
        string Med(IEnumerable<double> xs, string fmt = "0")
        {
            var v = xs.Where(x => !double.IsNaN(x)).Order().ToList();
            return v.Count == 0 ? "-" : v[v.Count / 2].ToString(fmt, Inv);
        }
        var afloat = rs.Where(r => r.Afloat).ToList();
        Console.WriteLine($"  {dc.Name}");
        Console.WriteLine(string.Format(Inv, "    {0}, {1:0} kn; {2}, {3}; {4}", target.Name, dc.Kn, dc.Readiness, dc.Doctrine, string.Join("; ", dc.Blows)));
        var hs = Enumerable.Range(0, Hours.Length).Where(h => Hours[h] <= hours).ToList();
        Console.WriteLine("    water afloat (median): " + string.Join(", ", hs.Select(h => $"{Hours[h].ToString("0.#", Inv)} h {Med(rs.Select(r => r.WaterAt[h]))} t")) +
                          string.Format(Inv, "; worst list {0}° (median, all runs)", Med(rs.Select(r => r.MaxList), "0.0")));
        Console.WriteLine(string.Format(Inv, "    at {0:0.#} h afloat: list {1}°, trim {2}° (+ by the head), speed {3} kn, power {4}",
            hours, Med(afloat.Select(r => r.List), "0.0"), Med(afloat.Select(r => r.Trim), "+0.0;-0.0"), Med(afloat.Select(r => r.Kn), "0.0"),
            afloat.Count == 0 ? "-" : (100 * afloat.Average(r => r.Power)).ToString("0", Inv) + "%"));
        Console.WriteLine(string.Format(Inv, "    boundaries sprung {0}, gave way {1} (in {2:0}% of runs), watertight doors open {3} (medians)",
            Med(rs.Select(r => (double)r.Sprung)), Med(rs.Select(r => (double)r.Collapsed)), 100.0 * rs.Count(r => r.Collapsed > 0) / n,
            Med(rs.Select(r => (double)r.DoorsOpen))));
        Console.WriteLine("    holes open (medians, count and m²): " + string.Join(", ", Enum.GetValues<HoleClass>().Select(h =>
            string.Format(Inv, "{0} {1} ({2})", h.ToString().ToLowerInvariant(), Med(rs.Select(r => (double)r.HolesN[(int)h])),
                Med(rs.Select(r => r.HolesM2[(int)h]), "0.00")))));
        var lost = rs.Where(r => !r.Afloat).ToList();
        Console.WriteLine(string.Format(Inv, "    lost {0:0}%{1}", 100.0 * lost.Count / n, lost.Count == 0 ? "" : " (" + string.Join(", ",
            lost.GroupBy(r => r.Loss).Select(g => string.Format(Inv, "{0} {1}, median {2} min", g.Key, g.Count(), Med(g.Select(r => r.LostMin))))) + ")"));
        Console.WriteLine($"    record: {dc.Record}");
        Console.WriteLine($"    wanted: {dc.Wanted}");
        Console.WriteLine(string.Format(Inv, "    basis: {0}  ({1:0.0} s)", dc.Basis, secs));
    }
}
