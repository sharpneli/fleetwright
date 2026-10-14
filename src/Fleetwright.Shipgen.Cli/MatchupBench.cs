using System.Globalization;
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
        const int reps = 2000;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int k = 0; k < reps; k++)
            Matchup.Judge(sa, sb, share, share, s, sightM);
        Console.WriteLine(string.Format(Inv, "  a judgement costs {0:0.0} µs ({1} classes against {2})", sw.Elapsed.TotalMicroseconds / reps, sa.Length, sb.Length));
        return 0;
    }

    static void Profile(MatchupProfile p, SimSettings s)
    {
        string Face((double Share, double Mm)[] f) => string.Join(" ", f.Select(x => string.Format(Inv, "{0:0}%@{1:0}", 100 * x.Share, x.Mm)));
        Console.WriteLine(string.Format(Inv, "{0}: {1:0} t, {2:0} x {3:0.0} m, height {4:0.0} m, {5:0.#} kn{6}", p.Class.Name, p.DisplacementT, p.LengthM,
            p.BeamM, p.HeightM, p.SpeedKn, p.TorpedoCraft ? ", torpedo craft" : ""));
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
                    double hit = Matchup.HitChance(g, t.Profile, r, s), o = Matchup.OutChance(g, t, r, s);
                    var (fall, strike) = g.At(r);
                    Console.WriteLine(string.Format(Inv, "  {0} b{1} on {2}: hits {3:0.0}% (fall {4:0.0}°, {5:0} m/s, pen {6:0}/{7:0} mm side/deck); a hit puts her out {8:0.00}%; {9:0.0000} out a minute a ship",
                        e.Profile.Class.Name, g.Battery, t.Profile.Class.Name, 100 * hit, fall, strike, Terminal.PenMm(g.Shell, strike, fall, false),
                        Terminal.PenMm(g.Shell, strike, 90 - fall, true), 100 * o, g.Guns * g.ShellsPerGunMin * hit * o));
                }
    }

    static void Terms(MatchupTerms t, SimSettings s) =>
        Console.WriteLine(string.Format(Inv, "  {0,-21} lines at {1,6:0} m{2}{3}, {4,5:0.0} min: on B guns {5:0.###} torpedoes {6:0.###}; on A guns {7:0.###} torpedoes {8:0.###} (attackers lost on the way in {9:0.###}); lost A {10:0.0}% B {11:0.0}%: confidence {12:0.###}",
            t.Style, t.RangeM, t.AttackA > 0 ? string.Format(Inv, ", A attacks from {0:0} m", t.AttackA) : "", t.AttackB > 0 ? string.Format(Inv, ", B attacks from {0:0} m", t.AttackB) : "",
            t.Minutes, t.GunsOnB, t.TorpedoesOnB, t.GunsOnA, t.TorpedoesOnA, t.RunInLost, 100 * t.LostA, 100 * t.LostB, t.Confidence(s.MatchupFloor)));
}
