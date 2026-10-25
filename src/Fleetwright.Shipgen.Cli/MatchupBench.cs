using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen matchup A[:n] B[:n]</c> (docs/battle/command.md 5.1g): who can fight whom. Each side is one or
/// more designs with counts (a comma list: <c>arethusa:4,v25:8</c>). It prints each class's profile as the matchup sees
/// it, each battery's hit chance and out chance a hit on the other side's classes at the range, the engagement at each
/// candidate (either side's gun range, either side's torpedo attack) and A's judgement: the range and style the faster
/// side picks, and A's confidence (the share of B's tonnage it puts out of the fight for the share of its own).</summary>
static class MatchupBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(string a, string b, Func<string, Design> load, string settingsPath, double rangeM, double share, double sightM)
    {
        var s = SimSettings.Load(settingsPath);
        var tables = new Dictionary<BallisticKey, BallisticTable>();
        BallisticTable Table(BallisticKey k) => tables.TryGetValue(k, out var t) ? t : tables[k] = new BallisticTable(k);
        var profiles = new Dictionary<string, MatchupProfile>();
        MatchupEntry[] Side(string list) => [.. list.Split(',').Select(item =>
        {
            var parts = item.Split(':');
            if (!profiles.TryGetValue(parts[0], out var p))
                profiles[parts[0]] = p = new MatchupProfile(ShipClassCompiler.Compile(ShipDesign.Build(load(parts[0]))), Table, s);
            // NAME[:COUNT][:t0]: t0 strips its torpedoes
            return new MatchupEntry(p, parts.Length > 1 ? int.Parse(parts[1], Inv) : 1, TorpedoesLeft: parts.Contains("t0") ? 0 : 1);
        })];
        var sa = Side(a);
        var sb = Side(b);
        foreach (var p in profiles.Values)
            Profile(p, s);
        var judged = Matchup.Judge(sa, sb, share, share, s, sightM);
        double r = rangeM > 0 ? rangeM : judged.Terms.RangeM;
        Console.WriteLine(string.Format(Inv, "\nAt {0:0} m:", r));
        Shots(sa, sb, r, s);
        Shots(sb, sa, r, s);
        Console.WriteLine("\nThe engagements (A's view):");
        double[] lines = rangeM > 0 ? [rangeM]
            : [.. new[] { share * sa.Max(e => e.Profile.MainReachM), share * sb.Max(e => e.Profile.MainReachM) }.Where(x => x > 0).Distinct()];
        foreach (double line in lines)
            foreach (var style in new[] { MatchupStyle.Gunfight, MatchupStyle.AttackByA, MatchupStyle.AttackByB, MatchupStyle.AttackByA | MatchupStyle.AttackByB })
                Terms(Matchup.Engage(sa, sb, line, style, s, sightM), s);
        Console.WriteLine(string.Format(Inv, "\nA's judgement: confidence {0:0.###} ({1}, the lines at {2:0} m); A {3:0.#} kn, B {4:0.#} kn{5}", judged.Confidence,
            judged.Terms.Style, judged.Terms.RangeM, judged.SpeedA, judged.SpeedB,
            judged.ACanRefuse(s) ? ": A picks the range, and can refuse" : judged.BCanRefuse(s) ? ": B picks the range, and can refuse" : ": about equal speed, A's own range"));
        string Who(MatchupEntry[] side, ulong mask) => string.Join(", ", side.Where((e, i) => (mask >> i & 1) != 0).Select(e => e.Profile.Class.Name).DefaultIfEmpty("none"));
        Console.WriteLine(string.Format(Inv, "  attackers (torpedoes their better weapon): A {0}; B {1}", Who(sa, judged.Terms.AttackersA), Who(sb, judged.Terms.AttackersB)));
        const int reps = 2000;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int k = 0; k < reps; k++)
            Matchup.Judge(sa, sb, share, share, s, sightM);
        Console.WriteLine(string.Format(Inv, "  a judgement costs {0:0.0} µs ({1} classes against {2})", sw.Elapsed.TotalMicroseconds / reps, sa.Length, sb.Length));
        return 0;
    }

    /// <summary><c>shipgen matchup --kinds A[,C...] B[,D...]</c> (docs/battle/shell-choice.md A0): whether the matchup ranks
    /// a battery's shell kinds as the damage model does. For each of A's batteries, each kind it carries, each of B's
    /// classes and each range in reach: the share of B's ships out of the fight after K hits beam on, as the matchup
    /// judges it (1 - e^(-K x its exponent)) beside the damage model's (<see cref="GivenHits"/>: SHIPS ships of K hits,
    /// SETTLE minutes of flooding and fire after), and each one's best kind; a line where they pick differently is
    /// marked. K is --hits, or by B's size (the square root of her thousands of tonnes, 1 to 8).</summary>
    public static int Kinds(string a, string b, Func<string, Design> load, string settingsPath, double[] ranges, int hits, int ships, double settleMin, int seed)
    {
        var s = SimSettings.Load(settingsPath);
        var fallOut = HitsBench.FallOut(settingsPath);
        var tables = new Dictionary<BallisticKey, BallisticTable>();
        BallisticTable Table(BallisticKey k) => tables.TryGetValue(k, out var t) ? t : tables[k] = new BallisticTable(k);
        var classes = new Dictionary<string, ShipClass>();
        ShipClass Class(string name) => classes.TryGetValue(name, out var c) ? c : classes[name] = ShipClassCompiler.Compile(ShipDesign.Build(load(name)));
        var targets = b.Split(',').Select(n => (Class: Class(n), Profile: new MatchupProfile(Class(n), Table, s))).ToList();
        int disagree = 0, lines = 0;
        foreach (var name in a.Split(','))
        {
            var sc = Class(name);
            for (int bi = 0; bi < sc.Batteries.Length; bi++)
            {
                ref readonly var row = ref sc.Batteries[bi];
                if (row.Kind is not (Battery.Main or Battery.Secondary))
                    continue;
                // a profile whose battery fires each kind it carries: its batteries of the kind loaded with that kind alone
                var kinds = new List<(ShellKind Kind, ShellTypeRow Shell, MatchupGun Gun)>();
                foreach (var kind in sc.Loadout.Loads(bi).ToArray().Select(l => sc.Loadout.ShellTypes[l.ShellType].Kind).Distinct())
                {
                    var errors = new List<string>();
                    string which = row.Kind == Battery.Main ? "main" : "secondary";
                    var lo = sc.Load(LoadoutSpec.Parse($"{which}:{kind.ToString().ToLowerInvariant()}"), errors);
                    var gun = lo == null ? null : new MatchupProfile(sc, Table, s, lo).Guns.FirstOrDefault(g => g.Battery == bi);
                    if (gun == null)
                    {
                        Console.WriteLine($"{sc.Name} b{bi}: no {kind} profile ({string.Join("; ", errors.DefaultIfEmpty("none bearing"))})");
                        continue;
                    }
                    kinds.Add((kind, lo!.ShellTypes[lo.Fires(bi)], gun));
                }
                if (kinds.Count == 0)
                    continue;
                double maxElev = sc.Mounts[sc.Links(row.Mounts)[0]].MaxElevationDeg;
                Console.WriteLine(string.Format(Inv, "\n{0} b{1} ({2}, {3:0} mm): {4}", sc.Name, bi, row.Kind.ToString().ToLowerInvariant(), row.CalibreMm,
                    string.Join(", ", kinds.Select(k => string.Format(Inv, "{0} {1:0.#} kg (filler {2:0.##} kg, {3} fuze)", k.Kind, k.Shell.MassKg, k.Shell.FillerKg, k.Shell.Fuze)))));
                foreach (var (tc, tp) in targets)
                {
                    int k = hits > 0 ? hits : Math.Clamp((int)Math.Round(Math.Sqrt(tp.DisplacementT / 1000)), 1, 8);
                    foreach (double r in ranges)
                    {
                        if (kinds.All(x => r > x.Gun.ReachM))
                            continue;
                        var judged = new double[kinds.Count];
                        var measured = new double[kinds.Count];
                        for (int i = 0; i < kinds.Count; i++)
                        {
                            if (r > kinds[i].Gun.ReachM)
                            {
                                (judged[i], measured[i]) = (double.NaN, double.NaN);
                                continue;
                            }
                            judged[i] = 1 - Math.Exp(-k * Matchup.OutExponent(kinds[i].Gun, tp, r, s));
                            var rep = GivenHits.Run(tc, kinds[i].Shell, maxElev, r, 90, ships * k, k, (ulong)seed, s, null, settleMin * 60, fallOut);
                            measured[i] = (double)rep.Out / Math.Max(rep.Ships, 1);
                        }
                        int bj = Best(judged), bm = Best(measured);
                        // a pick within the bench's noise (two standard errors of the measured shares) isn't a disagreement
                        double noise = 2 * Math.Sqrt(Math.Max(measured[bm] * (1 - measured[bm]), 1.0 / ships) / ships) * Math.Sqrt(2);
                        bool differ = bj != bm && measured[bm] - measured[bj] > noise;
                        lines++;
                        disagree += differ ? 1 : 0;
                        Console.WriteLine(string.Format(Inv, "  on {0,-18} {1,6:0} m, {2} hit(s): {3}  matchup {4}, damage model {5}{6}", tc.Name, r, k,
                            string.Join("  ", kinds.Select((x, i) => string.Format(Inv, "{0} {1,5:0.0}%/{2,5:0.0}%", x.Kind, 100 * judged[i], 100 * measured[i]))),
                            kinds[bj].Kind, kinds[bm].Kind, differ ? "  <- DIFFER" : bj != bm ? "  (within the noise)" : ""));
                    }
                }
            }
        }
        Console.WriteLine($"\n{lines} lines (out of the fight, matchup/damage model), {disagree} where they pick a different kind beyond the noise");
        return 0;

        static int Best(double[] v)
        {
            int best = 0;
            for (int i = 1; i < v.Length; i++)
                if (!double.IsNaN(v[i]) && (double.IsNaN(v[best]) || v[i] > v[best]))
                    best = i;
            return best;
        }
    }

    static void Profile(MatchupProfile p, SimSettings s)
    {
        string Face(MatchupFace[] f) => string.Join(" ", f.Select(x => string.Format(Inv, "{0:0}%@{1:0}{2}", 100 * x.Share, x.Mm, x.HardMm > 0 ? "h" : "")));
        Console.WriteLine(string.Format(Inv, "{0}: {1:0} t, {2:0} x {3:0.0} m, height {4:0.0} m, {5:0.#} kn", p.Class.Name, p.DisplacementT, p.LengthM,
            p.BeamM, p.HeightM, p.SpeedKn));
        Console.WriteLine(string.Format(Inv, "  side {0} (plating {1:0.#} mm); deck {2} (plating {3:0.#} mm)", Face(p.Side), p.SidePlatingMm, Face(p.Deck), p.DeckPlatingMm));
        foreach (var g in p.Guns)
            Console.WriteLine(string.Format(Inv, "  battery {0} {1} {2:0} mm: {3} bearing, {4:0.0} a gun a minute, {5} rounds, level {6}, reach {7:0} m; {8} {9:0.#} kg, filler {10:0.##} kg",
                g.Battery, g.Kind.ToString().ToLowerInvariant(), g.CalibreMm, g.Guns, g.ShellsPerGunMin, g.Rounds == int.MaxValue ? "unlimited" : g.Rounds.ToString(Inv),
                g.Level, g.ReachM, g.Shell.Kind, g.Shell.MassKg, g.Shell.FillerKg));
        if (p.TubesBearing > 0)
            Console.WriteLine(string.Format(Inv, "  torpedoes: {0} bearing, {1} carried; {2:0} kg, {3:0} kn for {4:0} m (attacks from {5:0} m); duds {6:0}%", p.TubesBearing,
                p.Torpedoes, p.Torpedo.ChargeKg, p.Torpedo.FastKn, p.Torpedo.FastRangeM, p.AttackRangeM(s), 100 * p.TorpedoDud));
    }

    static void Shots(MatchupEntry[] from, MatchupEntry[] to, double r, SimSettings s)
    {
        foreach (var e in from)
            foreach (var g in e.Profile.Guns)
                foreach (var t in to)
                {
                    if (r > g.ReachM)
                    {
                        Console.WriteLine(string.Format(Inv, "  {0} b{1} on {2}: out of reach", e.Profile.Class.Name, g.Battery, t.Profile.Class.Name));
                        continue;
                    }
                    // the kind its captain would load (docs/battle/shell-choice.md A4)
                    var c = g.ChosenKind(t.Profile, r, s) ?? g;
                    double hit = Matchup.HitChance(c, t.Profile, r, s), o = Matchup.OutChance(c, t, r, s);
                    var (fall, strike) = c.At(r);
                    Console.WriteLine(string.Format(Inv, "  {0} b{1} on {2} with {3}: hits {4:0.0}% (fall {5:0.0}°, {6:0} m/s, pen {7:0}/{8:0} mm side/deck); a hit puts her out {9:0.00}%; {10:0.0000} out a minute a ship",
                        e.Profile.Class.Name, g.Battery, t.Profile.Class.Name, c.Shell.Kind, 100 * hit, fall, strike, Terminal.PenMm(c.Shell, strike, fall),
                        Terminal.PenMm(c.Shell, strike, 90 - fall), 100 * o, g.Guns * g.ShellsPerGunMin * hit * o));
                }
    }

    static void Terms(MatchupTerms t, SimSettings s) =>
        Console.WriteLine(string.Format(Inv, "  {0,-21} lines at {1,6:0} m{2}{3}, {4,5:0.0} min: on B guns {5:0.###} torpedoes {6:0.###}; on A guns {7:0.###} torpedoes {8:0.###} (attackers lost on the way in {9:0.###}); lost A {10:0.0}% B {11:0.0}%: confidence {12:0.###}",
            t.Style, t.RangeM, t.AttackA > 0 ? string.Format(Inv, ", A attacks from {0:0} m", t.AttackA) : "", t.AttackB > 0 ? string.Format(Inv, ", B attacks from {0:0} m", t.AttackB) : "",
            t.Minutes, t.GunsOnB, t.TorpedoesOnB, t.GunsOnA, t.TorpedoesOnA, t.RunInLost, 100 * t.LostA, 100 * t.LostB, t.Confidence(s.MatchupFloor)));
}
