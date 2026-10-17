using System.Globalization;
using System.Text;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen pen-check`: the plate model alone against firing trials and range tables, no rolls
/// (docs/battle/shells-armour-damage.md P1a). It runs the game's plate step, <see cref="Terminal.LimitMs(double, double,
/// double, double)"/> and its inverse <see cref="Terminal.PenMm(double, double, double, double)"/> (Thompson's F_STD),
/// with the materials' q from the catalogue, over research/simulation/armor/07-*.csv (read from the repo, as a tool may:
/// nothing the release needs). armor_pen_ref.py makes the same tables from the same data, a reference to check
/// against. Sections: A the range tables' belts and decks, B NPG 1946-47's measured limits, C the Italian 1942
/// obliquity ratios, D the early trials (with the shells' caps, bodies and shatter: P2), E Okun's M79 deck columns.</summary>
static class PenCheck
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const double In = 25.4, Lb = 0.45359237, Fps = 0.3048;

    public static int Run(string repo)
    {
        string dir = Path.Combine(repo, "research", "simulation", "armor");
        var tables = Csv(Path.Combine(dir, "07-calibration-tables.csv"));
        var okun = Csv(Path.Combine(dir, "07-okun-facehard-tables.csv"));
        var trials = Csv(Path.Combine(dir, "07-trials.csv"));
        var sources = Csv(Path.Combine(dir, "07-table-sources.csv")).ToDictionary(r => r["src"], r => r["source_note"]);
        UsnTable(tables);
        RangeTables(tables, sources);
        Npg(trials);
        Italian(tables);
        Early(SimSettings.Load(Path.Combine(repo, "Content", "Sim", "settings.json")));
        Decks(okun);
        return 0;
    }

    static void Header(string title) => Console.WriteLine($"\n{new string('=', 100)}\n{title}\n{new string('=', 100)}");

    static double? Num(Dictionary<string, string> r, string key) =>
        r.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Float, Inv, out double d) ? d : null;

    static string S(double v, string f = "0") => v.ToString(f, Inv);

    /// <summary>A: the navweaps / USN 16"/50 Mk 8 table (2,700 lb), the one Thompson's F_STD made.</summary>
    static void UsnTable(List<Dictionary<string, string>> tables)
    {
        Header("A1. The USN 16\"/50 Mk 8 table (S01, 1,225 kg) against the game's PenMm (Thompson), belt at the fall, deck at 90 - fall");
        Console.WriteLine("| range m | V m/s | fall | belt table mm | game | ratio | deck table mm | game | ratio |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var r in tables.Where(r => r["src"] == "S01"))
        {
            double v = Num(r, "strike_v_ms")!.Value, fall = Num(r, "fall_deg")!.Value, cal = Num(r, "cal_mm")!.Value, kg = Num(r, "shell_kg")!.Value;
            double belt = Terminal.PenMm(v, cal, kg, fall), deck = Terminal.PenMm(v, cal, kg, 90 - fall);
            double? tb = Num(r, "belt_pen_mm"), td = Num(r, "deck_pen_mm");
            Console.WriteLine($"| {r["range_m"]} | {S(v)} | {S(fall, "0.0")} | {tb?.ToString("0", Inv) ?? "-"} | {S(belt)} | {(tb is { } b ? S(belt / b, "0.00") : "-")} | " +
                              $"{td?.ToString("0", Inv) ?? "-"} | {(fall > 0 ? S(deck) : "-")} | {(td is { } d && d > 0 && fall > 0 ? S(deck / d, "0.00") : "-")} |");
        }
    }

    /// <summary>A2: every range table (rows with an obliquity of their own are C's) as game / table, belts with falls to
    /// 30° and decks of an inch or more. The tables are on different plates and formulas: a ratio under 1 is a table more
    /// generous than STS at q 1.</summary>
    static void RangeTables(List<Dictionary<string, string>> tables, Dictionary<string, string> sources)
    {
        Header("A2. Range tables: game PenMm / table (belt: fall <= 30 deg; deck: table >= 25 mm), mean (min-max, n)");
        Console.WriteLine("| src | gun | shell | plate | belt | deck |");
        Console.WriteLine("|---|---|---|---|---|---|");
        var all = new List<double>();
        foreach (var g in tables.Where(r => !r["note"].Contains("obliquity")).GroupBy(r => (r["src"], r["shell"])))
        {
            var belt = new List<double>();
            var deck = new List<double>();
            foreach (var r in g)
            {
                if (Num(r, "strike_v_ms") is not { } v || Num(r, "fall_deg") is not { } fall || Num(r, "cal_mm") is not { } cal || Num(r, "shell_kg") is not { } kg)
                    continue;
                if (Num(r, "belt_pen_mm") is { } b && b > 0 && fall <= 30)
                    belt.Add(Terminal.PenMm(v, cal, kg, fall) / b);
                if (Num(r, "deck_pen_mm") is { } d && d >= 25 && fall > 0)
                    deck.Add(Terminal.PenMm(v, cal, kg, 90 - fall) / d);
            }
            if (belt.Count + deck.Count == 0)
                continue;
            all.AddRange(belt);
            var r0 = g.First();
            Console.WriteLine($"| {r0["src"]} | {r0["gun"]} | {Cut(r0["shell"], 30)} | {Cut(r0["plate_type"], 30)} | {Stat(belt)} | {Stat(deck)} |");
        }
        Console.WriteLine($"\nAll belts: {Stat(all)}");
    }

    static string Stat(List<double> x) => x.Count == 0 ? "-" : $"{S(x.Average(), "0.00")} ({S(x.Min(), "0.00")}-{S(x.Max(), "0.00")}, {x.Count})";

    static string Cut(string s, int n) => s.Length <= n ? s : s[..n];

    /// <summary>B: NPG 1946-47, the measured ballistic limits beside the USN NBL the report quotes and the game's V_L
    /// at q 1: the report's NBL is Thompson's own (a check of the port), measured / game the plate's grade.</summary>
    static void Npg(List<Dictionary<string, string>> trials)
    {
        Header("B. NPG 1946-47: measured limit vs the reported USN NBL and the game's V_L (q 1)");
        Console.WriteLine("| shell | plate | mm | Ob | reported NBL m/s | game V_L | game/reported | measured | grade % |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        var grades = new SortedDictionary<string, List<double>>(StringComparer.Ordinal);
        var port = new List<double>();
        foreach (var r in trials.Where(r => r["date"].StartsWith("1946-47", StringComparison.Ordinal)))
        {
            string res = r["result"];
            int nbl = res.IndexOf("USN NBL", StringComparison.Ordinal), open = res.IndexOf('(');
            if (nbl < 0 || open < 0)
                continue;
            if (!double.TryParse(res[(nbl + 7)..].Split("fps")[0].Trim(), NumberStyles.Float, Inv, out double rep)
                || !double.TryParse(res[(open + 1)..].Split("fps")[0].Replace("under", "").Trim(), NumberStyles.Float, Inv, out double meas))
                continue;
            double cal = Num(r, "cal_mm")!.Value, kg = Num(r, "shell_kg")!.Value, mm = Num(r, "plate_mm")!.Value, ob = Num(r, "obliquity_deg")!.Value;
            double vl = Terminal.LimitMs(mm, cal, kg, ob), g = 100 * meas * Fps / vl;
            port.Add(vl / (rep * Fps));
            string key = r["plate_type"].Split('(')[0].Trim();
            if (!grades.TryGetValue(key, out var list))
                grades[key] = list = [];
            list.Add(g);
            Console.WriteLine($"| {Cut(r["gun_or_shell"], 28)} | {Cut(r["plate_type"], 36)} | {S(mm)} | {S(ob)} | {S(rep * Fps)} | {S(vl)} | {S(vl / (rep * Fps), "0.000")} | " +
                              $"{(res.Contains("under") ? "<" : "")}{S(meas * Fps)} | {S(g, "0.0")} |");
        }
        Console.WriteLine($"\ngame V_L / reported NBL: {Stat(port)}");
        Console.WriteLine("Grade by plate (measured / game V_L, %):");
        foreach (var (k, v) in grades)
            Console.WriteLine($"  {k,-44} {S(v.Average(), "0.0")} %  ({v.Count}, {S(v.Min())}-{S(v.Max())})");
    }

    /// <summary>C: the Italian official KC tables (1942): T(Ob)/T(0) at each range, against the game's at the same
    /// striking speed (the one that just beats T(0) square on).</summary>
    static void Italian(List<Dictionary<string, string>> tables)
    {
        Header("C. Italian official tables (1942): T(Ob) / T(0), table vs game");
        Console.WriteLine("| shell | range m | T(0) mm | 10 | 20 | 30 | 40 | game 10/20/30/40 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach (var src in new[] { "S51", "S52" })
            foreach (var g in tables.Where(r => r["src"] == src && r["note"].Contains("obliquity")).GroupBy(r => r["range_m"]))
            {
                var t = new Dictionary<int, double>();
                foreach (var r in g)
                    if (Num(r, "belt_pen_mm") is { } mm && int.TryParse(r["note"].Split("obliquity")[^1].Trim().Split(' ')[0], NumberStyles.Integer, Inv, out int ob))
                        t[ob] = mm;
                if (!t.TryGetValue(0, out double t0))
                    continue;
                var r0 = g.First();
                double cal = Num(r0, "cal_mm")!.Value, kg = Num(r0, "shell_kg")!.Value;
                double v0 = Terminal.LimitMs(t0, cal, kg, 0);
                int[] obs = [10, 20, 30, 40];
                Console.WriteLine($"| {Cut(r0["shell"], 26)} | {g.Key} | {S(t0)} | " + string.Join(" | ", obs.Select(o => t.TryGetValue(o, out double x) ? S(x / t0, "0.000") : "-")) +
                                  " | " + string.Join("/", obs.Select(o => S(Terminal.PenMm(v0, cal, kg, o) / t0, "0.00"))) + " |");
            }
    }

    /// <summary>D: the early trials (armor_pen_ref.py EARLY_TRIALS, sourced in armor/06 and 07): the game's V_L on the
    /// plate times its catalogue material's q, through the game's face-hardened rule with each trial's shell (its cap, and
    /// its body as the shell templates' family has it: <see cref="Terminal.ShatterChance"/>, shattering at even odds or
    /// more, and <see cref="Terminal.FaceHardMm"/>; a capped shell's cap is on at the plate), beside what happened; and the q the trial implies (the STS the shot beats over the plate: '<' the
    /// plate was beaten, so its q is below; '>' it held, above; '=' at the limit).</summary>
    static void Early(SimSettings s)
    {
        Header("D. Early trials through the game's V_L with the catalogue's materials (q by calibre and plate) and the shells' caps and bodies (P2)");
        Console.WriteLine("| trial | material | shell | q | T/D | V m/s | game V_L | game | actual | q implied |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
        // the shells: their caps, and their bodies' proofs (obliquity, T/D) and quality, as ShellTemplates' families
        var shells = new Dictionary<string, (Cap Cap, double Ob, double TD, double Q)>
        {
            ["chilled"] = (Cap.None, 0, 0.3, 0.85), ["uncapped"] = (Cap.None, 15, 0.5, 0.95), ["soft"] = (Cap.Soft, 15, 0.5, 0.95),
            ["krupp1911"] = (Cap.Hard, 20, 0.6, 0.95), ["greenboy"] = (Cap.Hard, 20, 0.67, 0.96), ["ww2"] = (Cap.Hard, 30, 1.0, 1.0),
        };
        (string Name, double D, double W, double V, double T, double Ob, string Mat, string Shell, string Rel)[] rows =
        [
            ("Spezia 1876: 22in WI perforated (100-ton Palliser)", 17.72, 2000, 1446, 22, 0, "wrought iron", "chilled", "<"),
            ("Annapolis 1890: 6in Holtzer 100lb perforated 10.72in compound", 6, 100, 2075, 10.72, 0, "compound", "uncapped", "<"),
            ("Annapolis 1890: 6in stopped by 10.57in all-steel", 6, 100, 2075, 10.57, 0, "steel", "uncapped", ">"),
            ("Annapolis 1890: 6in stopped by 10.42in nickel steel", 6, 100, 2075, 10.42, 0, "nickel steel", "uncapped", ">"),
            ("Annapolis 1890: 8in Firth 210lb perforated compound", 8, 210, 1850, 10.72, 0, "compound", "uncapped", "<"),
            ("Annapolis 1890: 8in stopped (broke) by nickel steel", 8, 210, 1850, 10.42, 0, "nickel steel", "uncapped", ">"),
            ("Johnson 1894: 10in 500lb lodged in 10in NiS at 1490", 10, 500, 1490, 10, 0, "nickel steel", "soft", "="),
            ("Indian Head 1893: 14in Harvey not perf. by uncapped 10in", 10, 500, 2059, 14, 0, "Harvey nickel steel", "uncapped", ">"),
            ("1897: 12in Holtzer 850lb just perf. 12in Harvey (uncapped)", 12, 850, 1811, 12, 0, "Harvey nickel steel", "uncapped", "="),
            ("NO 1937 prob. VII: 10in 500lb, Harvey 13in limit 1620", 10, 500, 1620, 13, 0, "Harvey nickel steel", "ww2", "="),
            ("Meppen 1895: 11.8in KC not perf. by 12in 716lb (uncapped)", 12, 716, 1993, 11.8, 0, "Krupp cemented", "uncapped", ">"),
            ("Beardmore 1901: 6in KC limit 2261, 100lb uncapped", 6, 100, 2261, 6, 0, "Krupp cemented", "uncapped", "="),
            ("Beardmore 1901: 6in KC perforated by capped 105.9lb at 1945", 6, 105.9, 1945, 6, 0, "Krupp cemented", "soft", "<"),
            ("Krupp 250mm KC: capped 150mm limit ~2240", 5.9, 112, 2240, 9.84, 0, "Krupp cemented", "soft", "="),
            ("NO 1937 prob. V: 12in 870lb vs 12in KC limit 1420", 12, 870, 1420, 12, 0, "Krupp cemented", "ww2", "="),
            ("NO 1937 prob. VII: 10in 500lb vs 12in KC limit 1685", 10, 500, 1685, 12, 0, "Krupp cemented", "ww2", "="),
            ("NO 1937 prob. VI: 12in 870lb vs 14in Class A limit 1570", 12, 870, 1570, 14, 0, "Class A", "ww2", "="),
            ("Baden 1921: 15in Greenboy perforated 350mm KC a/A at 18.5", 15, 1910, 1550, 13.78, 18.5, "Krupp cemented", "greenboy", "<"),
            ("Baden 1921: 15in Greenboy stopped by 350mm KC CT at 30", 15, 1910, 1550, 13.78, 30, "Krupp cemented", "greenboy", ">"),
            ("1918-19: Russian 12in 1038lb intact through 8in British KC at 20", 12, 1038, 1615, 8, 20, "cemented armour", "krupp1911", "<"),
            ("1918-19: same shell broke on the 8in plate at 1447", 12, 1038, 1447, 8, 20, "cemented armour", "krupp1911", "~"),
        ];
        int agree = 0, judged = 0;
        foreach (var (name, d, w, vf, t, ob, mat, sh, rel) in rows)
        {
            double cal = d * In, kg = w * Lb, v = vf * Fps, mm = t * In;
            var m = MaterialRow.Of(MaterialTemplates.Get(mat)!);
            var (cap, bob, btd, bq) = shells[sh];
            var shell = new ShellTypeRow { CalibreMm = cal, MassKg = kg, Cap = cap, BreakObDeg = bob, BreakTD = btd, BodyQuality = bq };
            double q = m.Against(mm, cal), te = q * mm;
            bool shatter = false;
            if (m.FaceHardened)
            {
                shatter = Terminal.ShatterChance(shell, m.FaceShatters, t / d, ob, Terminal.BareShare(cap, ob), s) >= 0.5;
                te = Terminal.FaceHardMm(te, shell, ob, shatter, s);
            }
            double vl = Terminal.LimitMs(te, cal, kg, ob), implied = Terminal.PenMm(v, cal, kg, ob) / mm;
            string game = v >= vl ? shatter ? "broken" : "through" : "held";
            string actual = rel switch { "<" => "through", ">" => "held", "=" => "limit", _ => "-" };
            if (rel is "<" or ">")
            {
                judged++;
                agree += game == actual || game == "broken" && actual == "held" ? 1 : 0;
            }
            Console.WriteLine($"| {name} | {mat} | {sh}{(shatter ? ", shatters" : "")} | {S(te / mm, "0.00")} | {S(t / d, "0.00")} | {S(v)} | {S(vl)} | {game} | {actual} | {rel} {S(implied, "0.00")} |");
        }
        Console.WriteLine($"\nThe game agrees with {agree} of the {judged} trials that went one way (q: the plate's worth against this shell, shatter and the body's quality in; broken: holed by a shell in pieces, which counts as held).");
    }

    /// <summary>E: Okun's M79 deck columns (US Class B/STS and German Wotan hart) over the game's deck (Thompson at Ob =
    /// 90 - fall, no deck term: user, 2026-10-17), by deck obliquity. Over 1, M79 has the deck beaten by more than the
    /// game does (the game's deck holds better there); under 1, the game's deck is the easier.</summary>
    static void Decks(List<Dictionary<string, string>> okun)
    {
        Header("E. Okun M79 decks / the game's (Thompson, no deck term), by deck obliquity (fall >= 5 deg)");
        foreach (var (col, name) in new[] { ("us", "US Class B/STS (25% el.)"), ("de", "German Wh (18% el.)") })
        {
            var bins = new SortedDictionary<int, List<double>>();
            foreach (var r in okun)
            {
                if (Num(r, "strike_v_ms") is not { } v || Num(r, "fall_deg") is not { } fall || fall < 5 || Num(r, "shell_kg") is not { } kg
                    || Num(r, "cal_mm") is not { } cal || Num(r, $"{col}_deck_mm") is not { } mm || mm <= 0)
                    continue;
                double ob = 90 - fall;
                int bin = (int)(ob / 10) * 10;
                if (!bins.TryGetValue(bin, out var list))
                    bins[bin] = list = [];
                list.Add(mm / Terminal.PenMm(v, cal, kg, ob));
            }
            Console.WriteLine($"  {name}:");
            foreach (var (b, list) in bins)
                Console.WriteLine($"    Ob {b}-{b + 10}: {Stat(list)}");
        }
    }

    /// <summary>A CSV file as rows of named fields (quoted fields, doubled quotes inside).</summary>
    static List<Dictionary<string, string>> Csv(string path)
    {
        var lines = Fields(File.ReadAllText(path)).ToList();
        var head = lines[0];
        return lines.Skip(1).Where(l => l.Count > 1)
            .Select(l => head.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i < l.Count ? l[x.i] : "", StringComparer.Ordinal)).ToList();
    }

    static IEnumerable<List<string>> Fields(string text)
    {
        var row = new List<string>();
        var f = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    f.Append('"');
                    i++;
                }
                else if (c == '"')
                    quoted = false;
                else
                    f.Append(c);
            }
            else if (c == '"')
                quoted = true;
            else if (c == ',')
            {
                row.Add(f.ToString());
                f.Clear();
            }
            else if (c == '\n')
            {
                row.Add(f.ToString().TrimEnd('\r'));
                f.Clear();
                yield return row;
                row = [];
            }
            else
                f.Append(c);
        }
        if (f.Length > 0 || row.Count > 0)
        {
            row.Add(f.ToString());
            yield return row;
        }
    }
}
