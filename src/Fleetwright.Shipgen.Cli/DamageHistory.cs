using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen damage-history</c>: the battle anchors (docs/battle/shells-armour-damage.md E8). Each case is a
/// ship of the record's kind hit by the record's shells at random times over its minutes, through the world's damage
/// system, each hit from a range and a target angle drawn from the case's bands (the battle's, not one beam-on shot), at
/// her speed. The anchors are the damage done: what the shells did on the plates (Jutland's
/// count on plate over 9″ and on 6-9″, the belt holes), killed and wounded a hit (16's groups, flashes aside), the
/// turret-minutes lost (13), directors, steering, speed, water and list; how she ended is reported beside them, not
/// tuned to (damage control's own yardstick is <c>dc-history</c>). The first game's cases (1890-1925) lead; the later
/// ones check the mechanisms.</summary>
static class DamageHistory
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A share of a case's hits: a shooter's battery's shell, by kind (or its first), or a loadout's; at the
    /// case's ranges, or its own (a secondary that can't reach the main's).</summary>
    sealed record Fall(string Shooter, int Battery, string? Kind, double Share, string? Loadout = null, double RangeHi = 0);

    /// <summary>A case of the record: the target, her speed, the hits (n over the minutes, ranges and target angles drawn
    /// from the bands), her readiness and doctrine, the hours run, what was recorded and what's wanted.</summary>
    sealed record DamageCase(string Era, string Name, string Target, double Kn, Fall[] Falls, int Hits, double Minutes, (double Lo, double Hi) RangeM,
        (double Lo, double Hi) AspectDeg, string Readiness, string Doctrine, double Hours, string Record, string Wanted);

    const string Jut = "closed up";

    static readonly DamageCase[] Cases =
    [
        // Jutland's count of the British shells on German plate (armor/04 §6, Naval Gazing after Campbell): the shell, not
        // the ship; Seydlitz's own plates and the battle's ranges, many hits
        new("1890-1925", "Jutland 1916: British APC on German armour (Lion's 13.5″, Revenge's 15″ on Seydlitz), the count on the plates", "seydlitz", 24,
            [new("lion", 0, "ap", 0.6), new("revenge", 0, "ap", 0.4)], 20, 120, (10_000, 17_000), (45, 135), Jut, "1916", 1,
            "on plate over 9″ 17 hits: 3 ricochets, 1 HE; of 13, 1 through and burst, 5 holed with little effect, 7 not holed (1 in 13). " +
            "On 6-9″ 17: 3 clean, 6 burst while penetrating, 8 failed",
            "through over 9″ and burst near 1 in 13 (1 in 5 to 1 in 40 within the record's count); on 6-9″ clean and burst-in-plate both common"),
        new("1890-1925", "Seydlitz, Jutland 1916: 21 heavy hits (13.5″, 15″) over 4 h, no torpedo", "seydlitz", 24,
            [new("lion", 0, "ap", 0.45), new("revenge", 0, "ap", 0.35), new("lion", 0, "he", 0.2)], 21, 240, (9_000, 16_000), (45, 135), Jut, "1916", 5,
            "5 of 16-22 heavy hits pierced armour; 98 killed, 55 wounded; 5,308 t aboard with a torpedo hit, slowed, reached harbour",
            "a few pierced; killed about 1-3 a hit; water in the thousands of tonnes only with the torpedo (here without)"),
        new("1890-1925", "Derfflinger, Jutland 1916: 17 heavy and 9 secondary hits over 3 h", "derfflinger", 24,
            [new("revenge", 0, "ap", 0.4), new("lion", 0, "ap", 0.25), new("iron_duke", 1, null, 0.35, RangeHi: 11_000)], 26, 180, (8_000, 15_000), (45, 135),
            Jut, "1916", 5, "C and D turrets burned out by flash (73 of 78, 80 of 80 dead); 157 killed, 26 wounded; ~3,000 t; came home",
            "a turret or two out for good; killed a few a hit, flash aside; tonnes in the hundreds to low thousands"),
        new("1890-1925", "Lion, Jutland 1916: 13 × 12″ (Lützow) over 2 h", "lion", 26,
            [new("derfflinger", 0, null, 1)], 13, 120, (12_000, 16_000), (45, 135), Jut, "1916", 4,
            "Q turret's face/roof joint holed, the flash reached the magazine trunk, magazine flooded in time; 99 killed, 51 wounded; came home",
            "a turret out; killed 1-3 a hit flash aside; a magazine loss a few percent"),
        new("1890-1925", "Tiger, Jutland 1916: 18 × 11″ (Moltke) over 2 h", "tiger_1914", 27,
            [new("moltke", 0, null, 1)], 18, 120, (12_000, 16_000), (45, 135), Jut, "1916", 4,
            "Q turret's roof burst without piercing (out for a while), X barbette hit; 24 killed, 46 wounded; little water; fought on",
            "killed about 1-1.5 a hit; turrets in and out; little water"),
        new("1890-1925", "British battlecruisers, Jutland 1916: Indefatigable, 5 × 11″ (Von der Tann) in 15 min", "indefatigable", 25,
            [new("von_der_tann", 0, null, 1)], 5, 15, (13_000, 16_000), (45, 135), Jut, "1916", 1,
            "blew up after 5 hits (Queen Mary after ~7, Invincible after ~5; Lion, Princess Royal, Tiger, New Zealand survived): ~3 in ~58 BC hits",
            "a magazine loss a few percent a hit (record ~5%, 1-14% within the count)"),
        new("1890-1925", "Warspite, Jutland 1916: 15 × 12″ (König class) over 1 h", "revenge", 21,
            [new("konig", 0, null, 1)], 15, 60, (9_000, 14_000), (45, 135), Jut, "1916", 4,
            "14 killed, 32 wounded; steering jammed (debated); flooding aft, came home",
            "killed about 1 a hit; tens to a few hundred tonnes"),
        new("1890-1925", "Lion, Dogger Bank 1915: 16 heavy hits (11″, 12″) over 2 h at 25 kn", "lion", 25,
            [new("derfflinger", 0, null, 0.5), new("moltke", 0, null, 0.5)], 16, 120, (14_000, 17_000), (30, 110), Jut, "1916", 3,
            "a feed tank holed: port engine out, 15 kn; ~3,000 t, 10° list; 1 killed, 20 wounded",
            "slowed in some runs; hundreds to thousands of tonnes; killed few"),
        new("1890-1925", "Chester, Jutland 1916: 17 × 15 cm (Wiesbaden class) at 6-8 km, open-backed shields", "town_chatham", 26,
            [new("wiesbaden", 0, null, 1)], 17, 20, (6_000, 8_000), (45, 135), "closed up", "1916", 2,
            "29 killed, 49 wounded (16's G6b 2.0 / 2.4 a hit); 3 of 10 guns out; came home",
            "killed about 2 a hit among the open crews"),
        new("1890-1925", "Kent at the Falklands 1914: 38 × 10.5 cm (Nürnberg), crews behind armour (Devonshire class)", "devonshire", 22,
            [new("stettin", 0, null, 1)], 38, 90, (5_000, 10_000), (45, 135), "closed up", "1916", 2,
            "'did not cause significant damage'; 6 killed (Kent, 16's G6a 0.17 a hit; Cornwall 18 hits, none)",
            "killed well under 1 a hit; little water"),
        new("1890-1925", "Medium HE on a battleship: 30 × 6″ HE (König's secondaries) over 1 h", "iron_duke", 18,
            [new("konig", 1, "he", 1)], 30, 60, (6_000, 10_000), (45, 135), "closed up", "1916", 2,
            "(Malaya's 6″ battery fire came close)", "a magazine lost rarely, not never: about 1.5% of ships (fighting.md 4.5, the user's)"),
        new("1890-1925", "Destroyer under shellfire: 10 × 6″ lyddite HE (a light cruiser's) in 10 min", "tb_destroyer", 25,
            [new("arethusa", 0, "he", 1)], 10, 10, (3_000, 7_000), (30, 150), "closed up", "1916", 2,
            "(08 R3: one machinery hit cripples; hit count a poor predictor. Jutland's destroyers stopped by medium gunfire mostly went down (?))",
            "crippled nearly always; lost in many runs"),

        new("1890-1925", "Tsushima 1905: Orel, 5 × 12″, 9 × 8″, 39 × 6″ (Japanese) over 4 h", "borodino", 9,
            [new("mikasa", 0, "ap", 0.06), new("mikasa", 0, "common", 0.03), new("izumo", 0, null, 0.17), new("mikasa", 1, null, 0.74)], 53, 240, (4_500, 7_500),
            (45, 135), "prepared", "1905", 5,
            "0 of 4 belt hits pierced; two 6″ turrets jammed by 8″ hits, one burnt out by its ready charges; 43 killed (16's G2a 0.58 a hit); 'only moderately damaged'",
            "no belt holed; killed about 0.6 a hit; a turret or two jammed"),
        new("1890-1925", "Tsushima 1905: Mikasa, ~40 Russian hits (12″, 6″) over 2 h", "mikasa", 15,
            [new("borodino", 0, "ap", 0.25), new("borodino", 1, null, 0.75)], 40, 120, (4_500, 7_000), (45, 135), "prepared", "1916", 3,
            "'none seriously damaged her'; 113 killed and wounded (16's G2b 1.7 / 4.7 a hit)", "no belt holed; killed 1-2 a hit"),
        new("1890-1925", "Yalu 1894: a central-citadel ironclad (Inflexible for Dingyuan), 200 × 6″ common at 2-4 km", "inflexible", 8,
            [new("mikasa", 1, "common", 1, "secondary:JP 1904 common (powder)")], 200, 240, (2_000, 4_000), (45, 135), "unprepared", "1890", 1,
            "belts never pierced ('none deeper than 4 inches'); repeated fires; Dingyuan 14 killed in ~200 hits", "no belt holed; killed well under 1 a hit; fires"),

        new("later cuts", "Bismarck, 27 May 1941: ~80 heavy (16″, 14″) and ~300 medium (8″, 6″, 5.25″) in 90 min, 3-15 km", "bismarck", 8,
            [new("nelson", 0, null, 0.13), new("kgv", 0, null, 0.08), new("kent", 0, null, 0.45), new("nelson", 1, null, 0.17), new("kgv", 1, null, 0.17)],
            380, 90, (3_000, 15_000), (30, 150), "closed up", "1916", 1.5,
            "fore director out at 09:02, all 4 turrets silent by ~44 min; belt ~3 holes, all above water; afloat until torpedoed and scuttled at ~110 min",
            "turrets silent in 30-60 min; belt holes a handful; afloat after an hour"),
        new("later cuts", "South Dakota, 1942: 18 × 8″ AP, 6 × 6″, 1 × 14″, 1 × 5″ at 5-8 km (Colorado for her)", "colorado", 26,
            [new("heavy_cruiser", 0, "ap", 0.69, "main:JP 1931 Type 91 AP (TNA)"), new("kongo", 3, null, 0.23, "secondary:JP 1931 light common (TNA)"),
             new("kongo", 0, "he", 0.04, "main:JP 1931 Type 0 HE (TNA)"), new("yamato", 3, "he", 0.04)], 26, 15, (5_000, 8_000), (45, 135), "closed up", "1942", 2,
            "15-18 of 26 passed through (the 8″ AP's 0.4 s fuze [UNCERTAIN]); 6 belt hits, none pierced; 38-40 killed; flooding small, list 0.75°", "more than half through unburst; no belt holed; under 100 t"),
        new("later cuts", "Kirishima, 1942: 20 × 16″ at 8 km in 7 min (Kongo; Washington's Mk 8)", "kongo", 20,
            [new("colorado", 0, "ap", 1, "main:US 1941 AP Mk 8 (Explosive D)")], 20, 7, (7_800, 8_400), (45, 135), "closed up", "1916", 3,
            "turrets 3-4 silenced (pump rooms), turret 1's magazine flooded; rudder jammed; capsized ~2¼ h later after counterflooding",
            "≥ 2 turrets out; steering lost likely; ≥ 2,000 t"),
        new("later cuts", "Samar 1944: a destroyer, 3 × 46 cm AP at 15-18 km", "destroyer", 30,
            [new("yamato", 0, "ap", 1)], 3, 10, (15_000, 18_000), (30, 150), "closed up", "1944", 2,
            "Johnston: hits in the machinery burst there, 17 kn; elsewhere heavy AP through unburst", "most through unburst; slowed if the machinery is hit"),
        new("later cuts", "WWII destroyer: 10 × 5″ AAC in 10 min", "destroyer", 30,
            [new("heavy_cruiser", 1, "he", 1)], 10, 10, (4_000, 8_000), (30, 150), "closed up", "1944", 2,
            "(08 7.4 #10)", "mostly crippled, rarely lost"),
        new("later cuts", "WWII destroyer: 40 × 5″ AAC in 20 min", "destroyer", 30,
            [new("heavy_cruiser", 1, "he", 1)], 40, 20, (4_000, 8_000), (30, 150), "closed up", "1944", 2,
            "(08 7.4 #10)", "usually lost"),
    ];

    /// <summary>One run's ship: the per-hit tallies, and her state sampled a minute at a time.</summary>
    sealed class Ship
    {
        public int Entity, Hits, Killed, Wounded, FlashKilled, FlashWounded, Flashes, Fires, BeltThrough, BeltHoled, MainBelt, Magazine;
        public double TurretMin, LostMin = double.NaN, SilentMin = double.NaN, DirMin = double.NaN, SteerMin = double.NaN;
        public double WaterT, PeakT, MaxList, Speed = 1, Power = 1;
        public MagazineVia Via;
        public int MainOk, Main, DirOk, Dirs;
        public bool Afloat = true, SteerLost;
        public LossKind Loss;
    }

    /// <summary>The case's tallies over all its hits: the outcome classes, the shells on heavy (over 9″) and medium (6-9″)
    /// plate, turret hits that held and what they did.</summary>
    sealed class Tally
    {
        public int Hits;
        public readonly int[] Classes = new int[4];
        public readonly int[] Heavy = new int[5], Medium = new int[5];   // glanced, not holed, holed (pieces or in-plate), through, through and burst
        public int TurretHeld, Jams, NonPenOut;
        public double LargeStruck;
        public readonly Dictionary<string, int> TurretVia = [];
    }

    public static int Run(string settingsPath, int runs, string? only, Func<string, Design> load, int trace = -1)
    {
        var settings = SimSettings.Load(settingsPath);
        var classes = new Dictionary<string, ShipClass>();
        ShipClass Cls(string name) => classes.TryGetValue(name, out var c) ? c : classes[name] = ShipClassCompiler.Compile(ShipDesign.Build(load(name)));
        Console.WriteLine($"the battle anchors (docs/battle/shells-armour-damage.md E8): {runs} runs a case, each hit from a range and target angle " +
                          "in the case's bands, at random over its minutes, through the world's damage system");
        string era = "";
        foreach (var dc in Cases)
        {
            if (only != null && !dc.Name.Contains(only, StringComparison.OrdinalIgnoreCase))
                continue;
            if (dc.Era != era)
                Console.WriteLine($"\n{(era = dc.Era)}");
            var sw = Stopwatch.StartNew();
            var target = Cls(dc.Target);
            var tally = new Tally();
            var res = One(dc, target, Cls, settings, runs, tally, trace);
            Print(dc, target, res, tally, sw.Elapsed.TotalSeconds);
        }
        return 0;
    }

    static readonly double[] RangeSteps = [0, 0.25, 0.5, 0.75, 1];
    static readonly double[] AspectSteps = [0, 0.25, 0.5, 0.75, 1];

    static List<Ship> One(DamageCase dc, ShipClass target, Func<string, ShipClass> cls, SimSettings settings, int runs, Tally tally, int trace)
    {
        var s = settings with { ReadinessDefault = dc.Readiness, DamageControlDefault = dc.Doctrine };
        if (!s.Readiness.Any(x => x.Name == dc.Readiness) || !s.DamageControl.Any(x => x.Name == dc.Doctrine))
            throw new ArgumentException($"{dc.Name}: no readiness \"{dc.Readiness}\" or doctrine \"{dc.Doctrine}\"");
        // each fall's aims over the case's ranges and target angles
        var aims = dc.Falls.Select(f =>
        {
            var sc = cls(f.Shooter);
            var (shell, _) = HitsBench.ShellOf(sc, f.Battery, f.Kind, f.Loadout);
            double maxElev = sc.Mounts[sc.Links(sc.Batteries[f.Battery].Mounts)[0]].MaxElevationDeg;
            double hi = f.RangeHi > 0 ? f.RangeHi : dc.RangeM.Hi, lo = Math.Min(dc.RangeM.Lo, hi);
            return (from r in RangeSteps
                    from a in AspectSteps
                    select new GivenHits.Aim(target, shell, maxElev, lo + r * (hi - lo),
                        dc.AspectDeg.Lo + a * (dc.AspectDeg.Hi - dc.AspectDeg.Lo), s)).ToArray();
        }).ToArray();
        double total = dc.Falls.Sum(f => f.Share);
        var start = WorldTime.FromUtc(1916, 5, 31, 14);
        var w = new World(start, 1, s);
        int ci = w.Ships.AddClass(target);
        var rng = RandomStream.Named(1, "damage history");
        var list = new List<Ship>();
        var due = new List<(long Ms, int Run, int Fall)>();
        for (int i = 0; i < runs; i++)
        {
            w.Apply(ShipSystem.Spawn(ci, 50 + 0.05 * (i % 20), -20 - 0.1 * (i / 20), 0, dc.Kn));   // the open Atlantic, 5 km apart
            list.Add(new Ship { Entity = w.Ships.NewestEntity });
            for (int h = 0; h < dc.Hits; h++)
            {
                double u = rng.NextDouble() * total, acc = 0;
                int a = 0;
                for (; a < dc.Falls.Length - 1; a++)
                    if ((acc += dc.Falls[a].Share) > u)
                        break;
                due.Add(((long)(rng.NextDouble() * dc.Minutes * 60_000), i, a));
            }
        }
        due.Sort();
        var path = new HitPath();
        int next = 0;
        var store = w.Ships.Store;
        for (int m = 1; m <= Math.Max(dc.Hours * 60, dc.Minutes); m++)
        {
            long end = m * 60_000L;
            for (; next < due.Count && due[next].Ms < end; next++)
            {
                var (ms, ri, fi) = due[next];
                var r = list[ri];
                w.AdvanceTo(start + ms);
                int slot = store.SlotOf(r.Entity);
                if (slot < 0 || store.Combat.Condition(slot).Status != ShipStatus.Afloat)
                    continue;
                var k = store.Combat.Condition(slot);
                var mounts = (bool[])k.MountWorks.Clone();
                var aim = aims[fi][rng.NextInt(aims[fi].Length)];
                w.Damage.Strike(w, aim.Next(w, r.Entity, slot, path, ref rng, out string where));
                var t = w.Damage.LastTerminal;
                var fx = w.Damage.LastEffects;
                r.Hits++;
                tally.Hits++;
                tally.LargeStruck += where is "superstructure" or "director" or "mast" or "top" or "funnel" ? 1 : 0;
                tally.Classes[GivenHits.Report.ClassOf(t.Outcome)]++;
                (r.Killed, r.Wounded, r.Fires, r.Magazine) = (r.Killed + fx.Killed, r.Wounded + fx.Wounded, r.Fires + (fx.Fire ? 1 : 0), r.Magazine + (fx.Magazine ? 1 : 0));
                if (fx.Magazine)
                    r.Via = fx.MagazineVia;
                if (fx.Magazine && fx.MagazineVia == MagazineVia.Turret)
                    tally.TurretVia[fx.Part >= 0 && fx.Part < target.Components.Length ? $"{target.Components[fx.Part].Kind} {t.Outcome} {(int)(w.Damage.LastPath.Crossings.ToArray().Where(c => c.T < t.StopT && c.Plate is >= PlateKind.SideArmour and <= PlateKind.PartArmour).Select(c => c.Mm).DefaultIfEmpty(0).Max())}mm" : "?"] =
                        tally.TurretVia.GetValueOrDefault(fx.Part >= 0 && fx.Part < target.Components.Length ? $"{target.Components[fx.Part].Kind} {t.Outcome} {(int)(w.Damage.LastPath.Crossings.ToArray().Where(c => c.T < t.StopT && c.Plate is >= PlateKind.SideArmour and <= PlateKind.PartArmour).Select(c => c.Mm).DefaultIfEmpty(0).Max())}mm" : "?") + 1;
                if (ri == trace)
                {
                    var hp = w.Damage.LastPath;
                    var x0 = hp.Crossings[Math.Max(0, hp.Crossings.ToArray().ToList().FindIndex(c => c.Kind is CrossingKind.HullIn or CrossingKind.PartIn))];
                    Console.WriteLine(string.Format(Inv, "      {0,5:0.0} min  {1} at x {2:0.0} z {3:+0.0;-0.0} (wl {4:0.0}): {5}, burst {6} at {7:0.0} m, stopped {8:0.0} m; holes {9:0.000} m², " +
                        "out {10}, killed {11}, wounded {12}{13}{14}; water now {15:0} t", ms / 60_000.0, where, x0.Point.X, x0.Point.Z, target.Vertical.Waterline, t.Outcome, t.Burst,
                        t.BurstT, t.StopT, fx.BreachM2, fx.Destroyed + fx.Disabled, fx.Killed, fx.Wounded, fx.Fire ? ", fire" : "", fx.Magazine ? ", MAGAZINE" : "", k.WaterT));
                }
                if (fx.Flash)
                    (r.Flashes, r.FlashKilled, r.FlashWounded) = (r.Flashes + 1, r.FlashKilled + fx.Killed, r.FlashWounded + fx.Wounded);
                Plates(w.Damage.LastPath, t, tally, r);
                int held = t.Glance >= 0 ? t.Glance : t.Outcome is Outcome.Defeated or Outcome.BurstOutside ? t.Plate : -1;
                if (held >= 0 && w.Damage.LastPath.Crossings[held].Plate is PlateKind.Barbette or >= PlateKind.TurretFace and <= PlateKind.TurretRoof &&
                    target.MountOf(w.Damage.LastPath.Crossings[held].Index) >= 0)
                {
                    tally.TurretHeld++;
                    tally.Jams += fx.Turret.Blow == TurretBlow.Jam ? 1 : 0;
                    int mt = target.MountOf(w.Damage.LastPath.Crossings[held].Index);
                    tally.NonPenOut += mounts[mt] && !k.MountWorks[mt] && target.Mounts[mt].Kind == ComponentKind.Main ? 1 : 0;
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
                (r.Main, r.MainOk, r.Dirs, r.DirOk) = (0, 0, k.DirectorWorks.Length, k.DirectorWorks.Count(d => d));
                for (int i = 0; i < target.Mounts.Length; i++)
                    if (target.Mounts[i].Kind == ComponentKind.Main)
                        (r.Main, r.MainOk) = (r.Main + 1, r.MainOk + (k.MountWorks[i] ? 1 : 0));
                if (m <= dc.Minutes)
                    r.TurretMin += r.Main - r.MainOk;
                if (r.Main > 0 && r.MainOk == 0 && double.IsNaN(r.SilentMin))
                    r.SilentMin = m;
                if (r.Dirs > 0 && r.DirOk == 0 && double.IsNaN(r.DirMin))
                    r.DirMin = m;
                bool steer = k.Steering is Steering.Jammed or Steering.Lost or Steering.Engines;
                if (steer && double.IsNaN(r.SteerMin))
                    r.SteerMin = m;
                double top = target.Motion.MaxSpeedKn;
                (r.SteerLost, r.WaterT, r.Power, r.Speed) = (steer, k.WaterT, k.PowerCap, target.SpeedAt(target.PowerAt(top) * k.PowerCap) / top);
                r.MaxList = Math.Max(r.MaxList, Math.Abs(k.Attitude.HeelDeg));
                r.PeakT = Math.Max(r.PeakT, k.WaterT);
                if (list.IndexOf(r) == trace && m % 10 == 0)
                    Console.WriteLine(string.Format(Inv, "      {0,5} min  water {1:0} t, list {2:0.0}°, holes {3:0.00} m², speed {4:0.0} kn, power {5:0%}, main {6} of {7}",
                        m, k.WaterT, k.Attitude.HeelDeg, k.BreachM2.Sum(), store[slot].Speed / 0.514444, k.PowerCap, r.MainOk, r.Main));
            }
        }
        return list;
    }

    /// <summary>The shell on the first plate over 9″ (229 mm) and on the first of 6-9″ (152-229 mm) it met from outside
    /// (Jutland's count): glanced, not holed, holed in pieces or burst in it, through whole, through and burst behind it;
    /// and the belt got through, whole or in pieces.</summary>
    static void Plates(HitPath path, in TerminalResult t, Tally tally, Ship r)
    {
        var xs = path.Crossings;
        int heavy = -1, medium = -1;
        for (int i = 0; i < xs.Length && xs[i].T <= t.StopT; i++)
        {
            if (xs[i].Plate is not (>= PlateKind.SideArmour and <= PlateKind.PartArmour) || xs[i].Plate == PlateKind.Grating ||
                xs[i].Kind is CrossingKind.PartOut or CrossingKind.HullOut ||
                xs[i].Plate == PlateKind.SideArmour && Terminal.FromBehind(path.LegOf(path.Leg(xs[i].T)).Direction, xs[i].Normal))
                continue;   // only plates met from outside: a part entered through its skin, a belt from outboard
            if (xs[i].Mm > 228.6 && heavy < 0)
                heavy = i;
            else if (xs[i].Mm >= 152 && xs[i].Mm <= 228.6 && medium < 0 && heavy < 0)
                medium = i;
            if (xs[i].Plate == PlateKind.SideArmour && xs[i].Mm >= 50)
            {
                if (t.Glance == i || Held(xs, i, t))
                    break;
                if (t.Broken && t.Plate == i)
                    r.BeltHoled++;
                else if (!(t.Outcome == Outcome.BurstInPlate && t.Plate == i))
                    r.BeltThrough++;
                r.MainBelt += xs[i].Mm > 228.6 ? 1 : 0;   // a main belt (over 9″) holed, whole or in pieces
                break;
            }
        }
        if (heavy >= 0)
            tally.Heavy[Class(xs, heavy, t)]++;
        if (medium >= 0)
            tally.Medium[Class(xs, medium, t)]++;
    }

    /// <summary>Whether a plate stopped the shell whole (defeated, burst on its face, or a dud or blind that stayed there).</summary>
    static bool Held(ReadOnlySpan<Crossing> xs, int i, in TerminalResult t) =>
        t.Outcome is Outcome.Defeated or Outcome.BurstOutside && t.Plate == i || !t.Broken && t.StopT <= xs[i].T + 1e-6;

    static int Class(ReadOnlySpan<Crossing> xs, int i, in TerminalResult t)
    {
        if (t.Glance == i)
            return 0;
        if (Held(xs, i, t))
            return 1;
        if (t.Plate == i && (t.Broken || t.Outcome == Outcome.BurstInPlate))
            return 2;
        return t.Burst == BurstKind.Inside && t.BurstT > xs[i].T ? 4 : 3;
    }

    static void Print(DamageCase dc, ShipClass target, List<Ship> rs, Tally ty, double secs)
    {
        int n = rs.Count;
        string Med(IEnumerable<double> xs, string fmt = "0")
        {
            var v = xs.Where(x => !double.IsNaN(x)).Order().ToList();
            return v.Count == 0 ? "-" : v[v.Count / 2].ToString(fmt, Inv);
        }
        string Pct(int a, int b) => b == 0 ? "-" : (100.0 * a / b).ToString("0", Inv) + "%";
        string Plate(int[] p)
        {
            int all = p.Sum();
            return all == 0 ? "none" : string.Format(Inv, "{0} ({1:0.#}% of hits): glanced {2}, not holed {3}, holed in pieces or burst in it {4}, through whole {5}, " +
                "through and burst behind {6} (1 in {7:0.#})", all, 100.0 * all / ty.Hits, Pct(p[0], all), Pct(p[1], all), Pct(p[2], all), Pct(p[3], all), Pct(p[4], all),
                p[4] > 0 ? (double)all / p[4] : double.PositiveInfinity);
        }
        int hits = rs.Sum(r => r.Hits), nf = Math.Max(hits - rs.Sum(r => r.Flashes), 1);
        Console.WriteLine($"  {dc.Name}");
        Console.WriteLine(string.Format(Inv, "    {0}, {1:0} kn; {2}, {3}; {4} hits in {5:0} min at {6:0.#}-{7:0.#} km, {8:0}-{9:0}° off the bow: {10}",
            target.Name, dc.Kn, dc.Readiness, dc.Doctrine, dc.Hits, dc.Minutes, dc.RangeM.Lo / 1000, dc.RangeM.Hi / 1000, dc.AspectDeg.Lo, dc.AspectDeg.Hi,
            string.Join(" + ", dc.Falls.Where(f => f.Share > 0).Select(f => string.Format(Inv, "{0:0}% {1} {2}{3}{4}", 100 * f.Share, f.Shooter, f.Battery,
                f.Kind != null ? " " + f.Kind : "", f.Loadout != null ? $" ({f.Loadout})" : "")))));
        Console.WriteLine("    the hits (16 §2.1): " + string.Join(", ", Enumerable.Range(0, 4).Select(i => string.Format(Inv, "{0} {1}", GivenHits.Report.HitClasses[i],
            Pct(ty.Classes[i], ty.Hits)))) + string.Format(Inv, "; on the upper works {0:0}%", 100 * ty.LargeStruck / Math.Max(ty.Hits, 1)));
        Console.WriteLine("    on plate over 9″: " + Plate(ty.Heavy));
        Console.WriteLine("    on plate 6-9″: " + Plate(ty.Medium));
        Console.WriteLine(string.Format(Inv, "    the belt got through {0:0.00} a ship whole, {1:0.00} in pieces ({2:0.#} and {3:0.#} per 100 hits), over 9″ {7:0.00} a ship; " +
            "turret hits that held {4:0.#} per 100 hits, of those jammed {5}, a main mount out {6}", rs.Average(r => r.BeltThrough), rs.Average(r => r.BeltHoled),
            100.0 * rs.Sum(r => r.BeltThrough) / hits, 100.0 * rs.Sum(r => r.BeltHoled) / hits, 100.0 * ty.TurretHeld / Math.Max(ty.Hits, 1),
            Pct(ty.Jams, ty.TurretHeld), Pct(ty.NonPenOut, ty.TurretHeld), rs.Average(r => r.MainBelt)));
        Console.WriteLine(string.Format(Inv, "    killed {0:0.00} and wounded {1:0.00} a hit, flashes aside; {2} flash(es), killing {3:0} each; killed a ship {4} (median), " +
            "fires lit {5} a ship", (double)(rs.Sum(r => r.Killed) - rs.Sum(r => r.FlashKilled)) / nf, (double)(rs.Sum(r => r.Wounded) - rs.Sum(r => r.FlashWounded)) / nf,
            rs.Sum(r => r.Flashes), (double)rs.Sum(r => r.FlashKilled) / Math.Max(rs.Sum(r => r.Flashes), 1), Med(rs.Select(r => (double)r.Killed)),
            Med(rs.Select(r => (double)r.Fires))));
        var afloat = rs.Where(r => r.Afloat).ToList();
        Console.WriteLine(string.Format(Inv, "    over the case's {0:0} min: turret-minutes lost {1:0} per 100 hits; main battery silent in {2:0}% of runs (median {3} min), " +
            "every director out {4:0}% ({5} min), steering lost {6:0}% ({7} min)", dc.Minutes, 100 * rs.Sum(r => r.TurretMin) / hits,
            100.0 * rs.Count(r => !double.IsNaN(r.SilentMin)) / n, Med(rs.Select(r => r.SilentMin)), 100.0 * rs.Count(r => !double.IsNaN(r.DirMin)) / n,
            Med(rs.Select(r => r.DirMin)), 100.0 * rs.Count(r => !double.IsNaN(r.SteerMin)) / n, Med(rs.Select(r => r.SteerMin))));
        Console.WriteLine(string.Format(Inv, "    the most water aboard {0} t (median; 90th {1} t, all runs)", Med(rs.Select(r => r.PeakT)),
            rs.Select(r => r.PeakT).Order().ElementAt(Math.Min(n - 1, n * 9 / 10)).ToString("0", Inv)));
        Console.WriteLine(string.Format(Inv, "    at {0:0.#} h afloat: water {1} t (median; 90th {2} t), worst list {3}°, speed {4}, power {5}, main mounts {6} of {7}, " +
            "directors {8} of {9}", Math.Max(dc.Hours, dc.Minutes / 60.0), Med(afloat.Select(r => r.WaterT)),
            afloat.Count == 0 ? "-" : afloat.Select(r => r.WaterT).Order().ElementAt(Math.Min(afloat.Count - 1, afloat.Count * 9 / 10)).ToString("0", Inv),
            Med(afloat.Select(r => r.MaxList), "0.0"), afloat.Count == 0 ? "-" : (100 * afloat.Average(r => r.Speed)).ToString("0", Inv) + "%",
            afloat.Count == 0 ? "-" : (100 * afloat.Average(r => r.Power)).ToString("0", Inv) + "%", Med(afloat.Select(r => (double)r.MainOk), "0.#"),
            afloat.Count == 0 ? 0 : afloat[0].Main, Med(afloat.Select(r => (double)r.DirOk), "0.#"), afloat.Count == 0 ? 0 : afloat[0].Dirs));
        var lost = rs.Where(r => !r.Afloat).ToList();
        Console.WriteLine(string.Format(Inv, "    lost {0:0}%{1}; a magazine set off by a hit in {2:0}% of runs{3}", 100.0 * lost.Count / n, lost.Count == 0 ? "" : " (" +
            string.Join(", ", lost.GroupBy(r => r.Loss).Select(g => string.Format(Inv, "{0} {1}, median {2} min", g.Key, g.Count(), Med(g.Select(r => r.LostMin))))) + ")",
            100.0 * rs.Count(r => r.Magazine > 0) / n, rs.Any(r => r.Magazine > 0) ? " (" + string.Join(", ", rs.Where(r => r.Magazine > 0).GroupBy(r => r.Via)
                .Select(g => $"{g.Key} {g.Count()}")) + ")" : "") + (lost.Count == 0 ? "" : "; runs " +
            string.Join(" ", lost.Select(r => string.Format(Inv, "{0}:{1}@{2:0}", rs.IndexOf(r), r.Loss.ToString()[0], r.LostMin)))));
        if (ty.TurretVia.Count > 0)
            Console.WriteLine("    turret flashes by the part and the plate got through: " + string.Join(", ", ty.TurretVia.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
        Console.WriteLine($"    record: {dc.Record}");
        Console.WriteLine($"    wanted: {dc.Wanted}");
        Console.WriteLine(string.Format(Inv, "    ({0:0.0} s)", secs));
    }
}
