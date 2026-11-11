using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen balance</c> (docs/battle/balance.md BL0): the balance round's panel. The scenarios played to their
/// end in the fine tier with the AI on every side (<see cref="CoarseParity.PlayMany"/>, the same plays the parity bench
/// makes), each side's losses set beside the record's (decision 3: within about twice it), and the fire-control
/// yardstick judged (<see cref="FcHistory.Measure"/>). One table for each step of the round to answer to; the dated row
/// for "The balance log" is written by hand from it, and <c>--out</c> keeps the numbers for a later comparison.</summary>
static class BalancePanel
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A scenario of the panel, and the ships each side lost in the record it's inspired by (none for a
    /// hypothetical). The scenarios' forces are history's thinned, so a record is counted over the ships the file has.
    /// <paramref name="Range"/>: where the record's fire mostly was, km, for a gun class (<see cref="CoarseParity.Played.CalClass"/>;
    /// BL1j, rough [EST] from the standard accounts): the hit rates are read beside it, as the AI closing to where it hits
    /// would hide a weak fire control behind a record-like rate.</summary>
    sealed record Case(string File, int[]? Record = null, string Note = "", (int Cal, double Lo, double Hi)? Range = null);

    static readonly Case[] Panel =
    [
        new("dover-strait", [0, 2], "G42 and G85", (0, 0, 2)),                      // a night melee, Broke ramming G42
        new("coronel", [2, 0], "Good Hope and Monmouth", (2, 5, 11)),             // opened about 12,000 yd, closed to about 5,500
        new("cape-sarych", [0, 0], Range: (2, 6, 8)),                             // in fog, about 38 cables
        new("prizes-war"),
        new("aland"),
        new("heligoland-bight", [0, 4], "Mainz, Cöln, Ariadne, V187", (2, 4, 8)), // the battlecruisers on Cöln and Ariadne in the mist
        new("dogger-bank", [0, 1], "Blücher", (2, 15, 18)),                        // the chase at 17,000-20,000 yd
        new("tsushima", [7, 0], "of the file's 13: six battleships sunk and Buyny scuttled; Oryol and Bedovy taken, which the sim doesn't do", (2, 3, 7)),
        new("jutland", [14, 11], Range: (2, 8, 17)),                               // the run to the south 14-17 km, the fleets 8-13 km
    ];

    /// <param name="Seeds">Seeds a scenario (the Jutland half as many).</param>
    /// <param name="FcRuns">Runs a fire-control case; 0 leaves the yardstick out.</param>
    public sealed record Options(int Seeds, string[]? Only, int FcRuns, int Threads, string LandPath, string? Out);

    public static int Run(SimSettings settings, string root, Func<string, ShipClass> cls, Options o)
    {
        var cases = Panel.Where(c => o.Only is null || o.Only.Contains(c.File)).ToList();
        if (o.Only?.FirstOrDefault(n => Panel.All(c => c.File != n)) is { } bad)
            throw new ArgumentException($"no scenario '{bad}' in the panel ({string.Join(", ", Panel.Select(c => c.File))})");
        int SeedsOf(string file) => file == "jutland" ? Math.Max(1, o.Seeds / 2) : o.Seeds;
        int threads = o.Threads > 0 ? o.Threads : Environment.ProcessorCount;
        Console.WriteLine($"the balance panel (docs/battle/balance.md): {cases.Count} scenarios, {o.Seeds} seeds each (the Jutland {SeedsOf("jutland")}), the fine tier, " +
                          $"the AI on every side" + (o.FcRuns > 0 ? $"; fc-history at {o.FcRuns} runs a case" : ""));
        var sw = Stopwatch.StartNew();

        var (fc, orderings) = o.FcRuns > 0 ? FcHistory.Measure(settings, o.FcRuns, null, cls) : ([], []);
        var (scenarios, plays) = cases.Count > 0
            ? CoarseParity.PlayMany([.. cases.Select(c => c.File)], SeedsOf, [1], settings, root, o.LandPath, threads)
            : ([], []);

        var json = new List<(string File, string Side, double Lost, int Min, int Max, int? Record, string Verdict, double Hits, double HitPct)>();
        var fire = new List<FireRow>();
        int sidesBanded = 0, sidesIn = 0, ordered = 0, orderedRight = 0;
        var kinds = new int[Enum.GetValues<LossKind>().Length];
        var givenAll = new int[4];   // taken, struck, scuttled, abandoned (docs/battle/striking.md SK5)
        foreach (var c in cases)
        {
            var s = scenarios[c.File];
            var p = plays[(c.File, 1)];
            var ends = string.Join(", ", p.GroupBy(x => x.Reason).OrderBy(g => g.Key).Select(g => $"{ScenarioPlay.Why(g.Key)} {g.Count()}"));
            Console.WriteLine();
            Console.WriteLine($"{s.Name} ({p.Length} seeds; ended {ends}; {p.Average(x => x.Hours):F1} h on average)" + (c.Note.Length > 0 ? $"; the record: {c.Note}" : ""));
            Console.WriteLine($"  {"side",-16}{"ships",6}{"lost",8}{"range",9}{"record",8}{"2x",7}{"shell hits",12}{"hit %",8}{"torpedo hits",14}");
            for (int side = 0; side < s.Sides.Length; side++)
            {
                var lost = p.Select(x => x.Result[side].Lost).ToList();
                double mean = lost.Average();
                int? rec = c.Record?[side];
                string verdict = "";
                if (rec is int r)
                {
                    // within about twice the record (decision 3), a ship either way for the small numbers
                    double lo = Math.Min(r / 2.0, r - 1), hi = Math.Max(2.0 * r, r + 1);
                    verdict = mean < lo ? "LOW" : mean > hi ? "HIGH" : "in";
                    sidesBanded++;
                    sidesIn += verdict == "in" ? 1 : 0;
                }
                double hits = p.Average(x => x.ShellHits[side]), shells = p.Sum(x => x.Shells[side]);
                double pct = shells > 0 ? 100 * p.Sum(x => x.ShellHits[side]) / shells : double.NaN;
                Console.WriteLine(string.Format(Inv, "  {0,-16}{1,6}{2,8:0.0}{3,9}{4,8}{5,7}{6,12:0}{7,7:0.0}%{8,14:0.0}", s.Sides[side], p[0].Result[side].Ships, mean,
                    $"{lost.Min()}-{lost.Max()}", rec?.ToString(Inv) ?? "-", verdict, hits, pct, p.Average(x => x.TorpedoHits[side])));
                json.Add((c.File, s.Sides[side], mean, lost.Min(), lost.Max(), rec, verdict, hits, pct));
            }
            // the right side on top: the seeds where the side that lost more in the record loses more here
            if (c.Record is [int a, int b] && a != b)
            {
                int right = p.Count(x => a > b ? x.Result[0].Lost > x.Result[1].Lost : x.Result[1].Lost > x.Result[0].Lost);
                Console.WriteLine($"  the record's loser lost more in {right} of {p.Length} seeds");
                (ordered, orderedRight) = (ordered + p.Length, orderedRight + right);
            }
            var by = new int[kinds.Length];
            foreach (var x in p)
                for (int k = 0; k < by.Length; k++)
                    by[k] += x.LostBy[k];
            for (int k = 0; k < by.Length; k++)
                kinds[k] += by[k];
            var given = new int[4];
            foreach (var x in p)
                foreach (var r in x.Result)
                    (given[0], given[1], given[2], given[3]) = (given[0] + r.Taken, given[1] + r.Struck, given[2] + r.Scuttled, given[3] + r.Abandoned);
            for (int k = 0; k < 4; k++)
                givenAll[k] += given[k];
            if (by.Sum() > 0 || given.Sum() > 0)
                Console.WriteLine($"  how they went: {Kinds(by)}{Given(given)}" +
                                  (p.Sum(x => x.Rearguards) is int rg and > 0 ? $"; rearguards {(double)rg / p.Length:0.0} a seed, {p.Sum(x => x.RearguardsLost)} of {rg} lost" : ""));
            Fire(c, s, p, fire);
        }

        if (cases.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"the scenarios: {sidesIn} of {sidesBanded} sides within about twice the record; the record's loser lost more in {orderedRight} of {ordered} seeds; " +
                              $"how ships went, all together: {Kinds(kinds)}{Given(givenAll)}");
        }
        if (fc.Count > 0)
        {
            var banded = fc.Where(m => m.Verdict.Length > 0).ToList();
            Console.WriteLine();
            Console.WriteLine($"fc-history: {banded.Count(m => m.Verdict == "in")} of {banded.Count} cases in their bands ({banded.Count(m => m.Verdict == "HIGH")} HIGH, " +
                              $"{banded.Count(m => m.Verdict == "LOW")} LOW); the orderings {orderings.Count(x => x.Verdict == "in")} of {orderings.Count} in");
            foreach (var m in banded.Where(m => m.Verdict != "in"))
                Console.WriteLine(string.Format(Inv, "  {0,-44}{1,7:0.0}%  band {2:0.#}-{3:0.#}%  {4}", m.Name, m.St.HitPct, m.Lo, m.Hi, m.Verdict));
            foreach (var x in orderings.Where(x => x.Verdict != "in"))
                Console.WriteLine(string.Format(Inv, "  {0,-62}{1,6:0.00}  want {2:0.#}-{3:0.#}  {4}", x.What, x.Ratio, x.Lo, x.Hi, x.Verdict));
        }
        Console.WriteLine();
        Console.WriteLine($"time: {sw.Elapsed.TotalSeconds:0} s");

        if (o.Out != null)
        {
            using var fs = File.Create(o.Out);
            using var j = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
            j.WriteStartObject();
            j.WriteString("date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", Inv));
            j.WriteNumber("seeds", o.Seeds);
            j.WriteStartArray("sides");
            foreach (var x in json)
            {
                j.WriteStartObject();
                j.WriteString("scenario", x.File);
                j.WriteString("side", x.Side);
                j.WriteNumber("lost", x.Lost);
                j.WriteNumber("min", x.Min);
                j.WriteNumber("max", x.Max);
                if (x.Record is int r)
                    j.WriteNumber("record", r);
                j.WriteString("verdict", x.Verdict);
                j.WriteNumber("hits", x.Hits);
                if (!double.IsNaN(x.HitPct))
                    j.WriteNumber("hit_pct", x.HitPct);
                j.WriteEndObject();
            }
            j.WriteEndArray();
            j.WriteStartArray("fire");
            foreach (var f in fire)
            {
                j.WriteStartObject();
                j.WriteString("scenario", f.File);
                j.WriteString("side", f.Side);
                j.WriteString("guns", ClassNames[f.Cal]);
                j.WriteNumber("shells", f.Shells);
                j.WriteNumber("hits", f.Hits);
                j.WriteNumber("median_km", f.MedianKm);
                j.WriteNumber("q1_km", f.Q1Km);
                j.WriteNumber("q3_km", f.Q3Km);
                if (!double.IsNaN(f.HitsMedianKm))
                    j.WriteNumber("hits_median_km", f.HitsMedianKm);
                j.WriteStartArray("bands");
                foreach (var (sh, hi) in f.Bands)
                {
                    j.WriteStartArray();
                    j.WriteNumberValue(sh);
                    j.WriteNumberValue(hi);
                    j.WriteEndArray();
                }
                j.WriteEndArray();
                j.WriteEndObject();
            }
            j.WriteEndArray();
            j.WriteStartArray("fc_history");
            foreach (var m in fc)
            {
                j.WriteStartObject();
                j.WriteString("case", m.Name);
                j.WriteNumber("hit_pct", m.St.HitPct);
                j.WriteString("verdict", m.Verdict);
                j.WriteEndObject();
            }
            j.WriteEndArray();
            j.WriteEndObject();
            Console.WriteLine($"kept in {o.Out}");
        }
        return 0;
    }

    /// <summary>A side's fire with one class of gun, every seed together: shells and hits a run, the true range when fired
    /// (shells' median and quartiles, hits' median), and the shells and hits a run in each of <see cref="BandsKm"/>.</summary>
    sealed record FireRow(string File, string Side, int Cal, double Shells, double Hits, double MedianKm, double Q1Km, double Q3Km, double HitsMedianKm,
        (double Shells, double Hits)[] Bands);

    static readonly string[] ClassNames = ["light", "medium", "heavy"];
    static readonly string[] ClassLabels = ["light (under 120 mm)", "medium (120-200 mm)", "heavy (200 mm and over)"];
    static readonly double[] BandsKm = [0, 2, 4, 6, 10, 15];

    /// <summary>Where a scenario's fire was (BL1j), each side's by gun class, and the record's class's median beside the
    /// record's ranges: CLOSE under them, FAR over.</summary>
    static void Fire(Case c, Scenario s, CoarseParity.Played[] p, List<FireRow> rows)
    {
        const int bins = CoarseParity.Played.RangeBins, classes = CoarseParity.Played.CalClasses;
        int n = s.Sides.Length * classes * bins;
        var shellsAt = new double[n];
        var hitsAt = new double[n];
        foreach (var x in p)
            for (int k = 0; k < n; k++)
                (shellsAt[k], hitsAt[k]) = (shellsAt[k] + x.FireShells[k], hitsAt[k] + x.FireHits[k]);
        bool any = false;
        var every = new double[classes * bins];
        for (int side = 0; side < s.Sides.Length; side++)
            for (int cal = 0; cal < classes; cal++)
            {
                int at = (side * classes + cal) * bins;
                var sh = shellsAt.AsSpan(at, bins).ToArray();
                var hi = hitsAt.AsSpan(at, bins).ToArray();
                for (int b = 0; b < bins; b++)
                    every[cal * bins + b] += sh[b];
                double shells = sh.Sum(), hits = hi.Sum();
                if (shells == 0)
                    continue;
                if (!any)
                    Console.WriteLine("  where the fire was (shells a run, hit %, the true range when fired: the shells' median (quartiles), the hits' median):");
                any = true;
                var bands = new (double Shells, double Hits)[BandsKm.Length];
                for (int b = 0; b < bins; b++)
                {
                    int band = BandsKm.Count(k => b * CoarseParity.Played.BinM >= k * 1000) - 1;
                    bands[band] = (bands[band].Shells + sh[b] / p.Length, bands[band].Hits + hi[b] / p.Length);
                }
                var row = new FireRow(c.File, s.Sides[side], cal, shells / p.Length, hits / p.Length, Quantile(sh, 0.5), Quantile(sh, 0.25), Quantile(sh, 0.75),
                    hits > 0 ? Quantile(hi, 0.5) : double.NaN, bands);
                rows.Add(row);
                Console.WriteLine(string.Format(Inv, "    {0,-14}{1,-24}{2,8:0} {3,5:0.0}%  fired at {4,4:0.0} km ({5:0.0}-{6:0.0}), hits at {7}", s.Sides[side], ClassLabels[cal],
                    row.Shells, 100 * hits / shells, row.MedianKm, row.Q1Km, row.Q3Km, double.IsNaN(row.HitsMedianKm) ? "-" : row.HitsMedianKm.ToString("0.0", Inv) + " km"));
                Console.WriteLine("      by range: " + string.Join(", ", bands.Select((x, i) => (x, i)).Where(t => t.x.Shells > 0).Select(t =>
                    string.Format(Inv, "{0} {1:0}% of shells at {2:0.0}%", Band(t.i), 100 * t.x.Shells / row.Shells, 100 * t.x.Hits / t.x.Shells))));
            }
        if (c.Range is var (rc, lo, hiKm) && every.AsSpan(rc * bins, bins).ToArray() is var col && col.Sum() > 0)
        {
            double m = Quantile(col, 0.5);
            Console.WriteLine(string.Format(Inv, "  the {0} guns' fire, every side: median {1:0.0} km against the record's {2:0.#}-{3:0.#} km [EST]: {4}", ClassNames[rc], m, lo, hiKm,
                m < lo ? "CLOSE" : m > hiKm ? "FAR" : "in"));
        }
    }

    static string Band(int i) => i == BandsKm.Length - 1 ? $"{BandsKm[i]:0}+ km" : $"{BandsKm[i]:0}-{BandsKm[i + 1]:0} km";

    /// <summary>A quantile of a histogram in <see cref="CoarseParity.Played.BinM"/> bins, km, linear within its bin.</summary>
    static double Quantile(double[] bins, double q)
    {
        double total = bins.Sum(), want = q * total, run = 0;
        for (int b = 0; b < bins.Length; b++)
        {
            if (bins[b] > 0 && run + bins[b] >= want)
                return (b + (want - run) / bins[b]) * CoarseParity.Played.BinM / 1000;
            run += bins[b];
        }
        return bins.Length * CoarseParity.Played.BinM / 1000;
    }

    /// <summary>Of the lost, those given up (striking.md SK5): taken, struck, scuttled, abandoned, as counts over the seeds.</summary>
    static string Given(int[] g) => g.Sum() == 0 ? "" : $"; given up: taken {g[0]}, struck {g[1]}, scuttled {g[2]}, abandoned {g[3]}";

    static string Kinds(int[] by)
    {
        int all = by.Sum();
        if (all == 0)
            return "none lost";
        return string.Join(", ", Enumerable.Range(0, by.Length).Where(k => by[k] > 0).OrderByDescending(k => by[k])
            .Select(k => $"{(LossKind)k switch { LossKind.Magazine => "magazine", LossKind.Capsized => "capsized", LossKind.BrokeInTwo => "broke in two", LossKind.Foundered => "foundered", var z => z.ToString().ToLowerInvariant() }} {100.0 * by[k] / all:0}%"));
    }
}
