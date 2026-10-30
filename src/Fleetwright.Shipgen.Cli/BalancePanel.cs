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
    /// hypothetical). The scenarios' forces are history's thinned, so a record is counted over the ships the file has.</summary>
    sealed record Case(string File, int[]? Record = null, string Note = "");

    static readonly Case[] Panel =
    [
        new("dover-strait", [0, 2], "G42 and G85"),
        new("coronel", [2, 0], "Good Hope and Monmouth"),
        new("cape-sarych", [0, 0]),
        new("prizes-war"),
        new("aland"),
        new("heligoland-bight", [0, 4], "Mainz, Cöln, Ariadne, V187"),
        new("dogger-bank", [0, 1], "Blücher"),
        new("tsushima", [7, 0], "of the file's 13: six battleships sunk and Buyny scuttled; Oryol and Bedovy taken, which the sim doesn't do"),
        new("jutland", [14, 11]),
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
        int sidesBanded = 0, sidesIn = 0, ordered = 0, orderedRight = 0;
        var kinds = new int[Enum.GetValues<LossKind>().Length];
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
            if (by.Sum() > 0)
                Console.WriteLine($"  how they went: {Kinds(by)}");
        }

        if (cases.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"the scenarios: {sidesIn} of {sidesBanded} sides within about twice the record; the record's loser lost more in {orderedRight} of {ordered} seeds; " +
                              $"how ships went, all together: {Kinds(kinds)}");
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

    static string Kinds(int[] by)
    {
        int all = by.Sum();
        if (all == 0)
            return "none lost";
        return string.Join(", ", Enumerable.Range(0, by.Length).Where(k => by[k] > 0).OrderByDescending(k => by[k])
            .Select(k => $"{(LossKind)k switch { LossKind.Magazine => "magazine", LossKind.Capsized => "capsized", LossKind.BrokeInTwo => "broke in two", LossKind.Foundered => "foundered", var z => z.ToString().ToLowerInvariant() }} {100.0 * by[k] / all:0}%"));
    }
}
