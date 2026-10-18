using System.Globalization;
using System.Text.Json.Nodes;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen fc-sensors</c>: the sensors' anchors (docs/battle/fire-control.md F5d), the designer note's
/// cruiser study run on the sim (research/simulation/fire-control-designer.md §4): a 9 x 8"/55 cruiser (the heavy
/// cruiser's design, one director, no other rangefinder unless asked) against a cruiser at 30 kn on a parallel course,
/// steady or zigzagging ±25° every 150 s, in clear air (35 km) or haze (25 km), practice's errors, 15 minutes. D6, hits
/// against the director's base by tech (plot, table, keeper) and range, relative to 4.57 m; D8, the same budget arranged
/// otherwise at 18 kyd. The research's figures stand beside them; they check the shape (the knee, what an instrument
/// buys), not a number the sim must hit.</summary>
static class FcSensors
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A tech set (designer §4): its blocks.</summary>
    sealed record Tech(string Name, string Optics, string Computer, string Mount);

    static readonly Tech[] Techs =
    [
        new("plot", "Barr & Stroud FQ/FT (O2)", "Dreyer table Mk III-V", "Director, follow the pointer"),
        new("table", "Stereo, long base (O3)", "Admiralty Fire Control Table", "Director, power drive"),
        new("keeper", "Coated, stabilised (O4)", "Ford Mk 8 rangekeeper", "Stable vertical, remote power control"),
    ];

    /// <summary>An arrangement: the director's base (0: by eye), its magnification and kind (null: the tech's), after
    /// control's and each main turret's rangefinder (0: none).</summary>
    sealed record Arrangement(string Name, double DirectorM, double Magnification = 25, string? Kind = null, double AftM = 0, double TurretM = 0);

    static readonly double[] Bases = [0, 2.74, 3.66, 4.57, 6.1, 8.1, 10, 15];
    static readonly double[] RangesKyd = [10, 15, 20, 25];

    /// <summary>D6's figures (keeper, table, plot; relative to 4.57 m; NaN where the note gives none).</summary>
    static readonly Dictionary<(string, double), double[]> D6 = new()
    {
        [("keeper", 0)] = [14, 10, 10, 11], [("keeper", 2.74)] = [91, 86, 80, 79], [("keeper", 3.66)] = [97, 95, 91, 91],
        [("keeper", 6.1)] = [103, 108, 108, 116], [("keeper", 8.1)] = [106, 112, 116, 118], [("keeper", 10)] = [107, 115, 121, 126],
        [("keeper", 15)] = [110, 119, 128, 140], [("table", 2.74)] = [91, 88, 83, 82], [("table", 8.1)] = [105, 109, 111, 120],
        [("table", 15)] = [107, 118, 122, 137], [("plot", 2.74)] = [95, 90, 87, 86], [("plot", 15)] = [107, 114, 110, 119],
    };

    public static int Run(SimSettings settings, int runs, string root, Func<string, ShipClass> cls)
    {
        string path = Path.Combine(root, "designs", "heavy_cruiser.json");
        var target = cls("heavy_cruiser");
        var built = new Dictionary<string, ShipClass?>();
        ShipClass? Variant(Tech t, Arrangement a)
        {
            string key = $"{t.Name}|{a}";
            if (!built.TryGetValue(key, out var c))
                built[key] = c = Build(path, t, a);
            return c;
        }

        Console.WriteLine($"D6: hits in 15 min against the director's base, relative to 4.57 m = 100, the mean of clear and haze, steady and zigzag ({runs} runs a cell)");
        Console.WriteLine("9 x 8\"/55 (the heavy cruiser, one director at ×25, no other rangefinder) against a cruiser at 30 kn, parallel; the research's figure after the slash");
        foreach (var t in Techs)
        {
            var hits = new double[Bases.Length, RangesKyd.Length];
            for (int bi = 0; bi < Bases.Length; bi++)
            {
                var c = Variant(t, new Arrangement("", Bases[bi]));
                for (int ri = 0; ri < RangesKyd.Length; ri++)
                    hits[bi, ri] = c is null ? double.NaN : Hits(c, target, RangesKyd[ri], settings, runs, mixed: true);
            }
            int at457 = Array.IndexOf(Bases, 4.57);
            Console.WriteLine($"\n  {t.Name} ({t.Optics}, {t.Computer}): hits at 4.57 m " +
                string.Join(" / ", Enumerable.Range(0, RangesKyd.Length).Select(r => FcBench.F(hits[at457, r], "0.0"))) + " at " +
                string.Join(" / ", RangesKyd.Select(r => $"{r:0}")) + " kyd");
            Console.WriteLine($"  {"base",-8}" + string.Concat(RangesKyd.Select(r => $"{r + " kyd",14}")));
            for (int bi = 0; bi < Bases.Length; bi++)
            {
                var line = $"  {(Bases[bi] == 0 ? "eye" : Bases[bi].ToString("0.##", Inv) + " m"),-8}";
                for (int ri = 0; ri < RangesKyd.Length; ri++)
                {
                    double rel = 100 * hits[bi, ri] / hits[at457, ri];
                    string refd = D6.TryGetValue((t.Name, Bases[bi]), out var d) ? "/" + d[ri].ToString("0", Inv) : Bases[bi] == 4.57 ? "/100" : "";
                    line += $"{(double.IsNaN(rel) ? (built[$"{t.Name}|{new Arrangement("", Bases[bi])}"] is null ? "no fit" : "-") : rel.ToString("0", Inv)) + refd,14}";
                }
                Console.WriteLine(line);
            }
        }

        Console.WriteLine($"\nD8: the same budget arranged otherwise, table tech, 18 kyd, zigzagging; hits in 15 min, clear / haze ({runs} runs a cell)");
        var table = Techs[1];
        (Arrangement A, string Research)[] d8 =
        [
            (new("4.57 m director, ×25 (the reference)", 4.57), "11.4 / 9.5"),
            (new("+ a 4.57 m in after control", 4.57, AftM: 4.57), "12.2 / 10.5"),
            (new("+ a 2.74 m in after control", 4.57, AftM: 2.74), "(12.2 / 10.5 for a 4.57 m)"),
            (new("+ a 4.57 m in each turret (3)", 4.57, TurretM: 4.57), "12.4 / 11.0 (one turret)"),
            (new("8.1 m in the director", 8.1), "12.8 / 11.6"),
            (new("4.57 m director + 8.1 m turrets", 4.57, TurretM: 8.1), "12.7 / 11.2 (one turret)"),
            (new("2.74 m director + 8.1 m turrets", 2.74, TurretM: 8.1), "13.0 / 10.5 (one turret)"),
            (new("×15", 4.57, Magnification: 15), "9.7 / 8.3"),
            (new("×35", 4.57, Magnification: 35), "11.4 / 9.7"),
            (new("×50", 4.57, Magnification: 50), "10.0 / 9.2"),
            (new("coincidence instead of stereo", 4.57, Kind: "coincidence"), "9.6 / 8.5"),
        ];
        Console.WriteLine($"  {"arrangement",-40}{"clear",8}{"haze",8}{"",4}research");
        double c0 = double.NaN, h0 = double.NaN;
        foreach (var (a, research) in d8)
        {
            var c = Variant(table, a);
            double clear = c is null ? double.NaN : Hits(c, target, 18, settings, runs, mixed: false, haze: false);
            double haze = c is null ? double.NaN : Hits(c, target, 18, settings, runs, mixed: false, haze: true);
            if (double.IsNaN(c0))
                (c0, h0) = (clear, haze);
            Console.WriteLine($"  {a.Name,-40}{FcBench.F(clear, "0.0"),8}{FcBench.F(haze, "0.0"),8}    {research}" +
                (a.DirectorM == 4.57 && a.AftM == 0 && a.TurretM == 0 && a.Magnification == 25 && a.Kind is null ? "" :
                    string.Format(Inv, "   ({0:+0;-0}% / {1:+0;-0}%)", 100 * (clear / c0 - 1), 100 * (haze / h0 - 1))));
        }
        return 0;
    }

    /// <summary>Hits a run in 15 minutes at a range: the mean of clear and haze, steady and zigzag, or zigzag in one
    /// weather.</summary>
    static double Hits(ShipClass shooter, ShipClass target, double kyd, SimSettings settings, int runs, bool mixed, bool haze = false)
    {
        var cases = new List<FcCase>();
        foreach (bool hz in mixed ? new[] { false, true } : [haze])
            foreach (bool zig in mixed ? new[] { false, true } : [true])
                cases.Add(new FcCase
                {
                    Name = $"{kyd} kyd", Shooter = shooter, Target = target, RangeM = kyd * FcBench.Kyd, BearingDeg = 90, CourseDeg = 0, OwnKn = 30,
                    TargetKn = 30, Behaviour = zig ? Policy.Zigzag(25, 150) : Policy.Steady, VisibilityM = hz ? 25_000 : 35_000, Combat = 1, Minutes = 15,
                });
        var res = FcBench.Runs(cases, settings, runs, 1);
        return res.Average(r => r.Sum(x => (double)x.Tally.Hits) / Math.Max(r.Length, 1));
    }

    /// <summary>The heavy cruiser with one main director of the arrangement's base, its optics the tech's at the
    /// arrangement's magnification and kind (a block of the design's own), after control's and the turrets' rangefinders
    /// as asked, no radar; null when it doesn't build (a director too wide for its deck, §7.1).</summary>
    static ShipClass? Build(string path, Tech t, Arrangement a)
    {
        var o = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var fc = o["fire_control"]!.AsObject();
        var main = fc["main"]!.AsObject();
        main["directors"] = 1;
        main["rangefinder_m"] = a.DirectorM;
        main["radar_t"] = 0;
        var cat = FireControlTemplates.Get(t.Optics)!.Value.Optics!;
        string name = $"{t.Optics}, ×{a.Magnification:0}{(a.Kind is null ? "" : ", " + a.Kind)} (bench)";
        var tech = o["fire_control_tech"] as JsonObject ?? new JsonObject();
        o["fire_control_tech"] = tech;
        tech[name] = new JsonObject
        {
            ["optics"] = new JsonObject
            {
                ["kind"] = a.Kind ?? cat.Kind, ["delta_arcsec"] = cat.DeltaArcsec, ["rigid_base_m"] = cat.RigidBaseM, ["flex_exp"] = cat.FlexExp,
                ["magnification"] = a.Magnification, ["m_max"] = cat.MMax,
                // a coincidence set reads 3 a minute where a stereo one reads 4 (designer §3.1)
                ["readings_per_min"] = a.Kind == "coincidence" && cat.Kind == "stereo" ? 0.75 * cat.ReadingsPerMin : cat.ReadingsPerMin, ["coated"] = cat.Coated,
                ["gyro_stabilised"] = cat.GyroStabilised, ["mass_kg_m2"] = cat.MassKgM2,
            },
        };
        main["fit"] = new JsonArray(name, t.Computer, t.Mount);
        fc["aft_rangefinder_m"] = a.AftM;
        fc["conning_tower_rangefinder_m"] = 0;
        foreach (var b in o["main"]!.AsArray())
            b!["rangefinder_m"] = a.TurretM;
        try
        {
            var ship = ShipDesign.Build(Design.Parse(o.ToJsonString()));
            foreach (var w in ship.Report.Warnings.Where(w => w.Contains("rangefinder", StringComparison.OrdinalIgnoreCase) || w.Contains("director", StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine($"  ({t.Name}, {a.Name} {a.DirectorM} m: {w})");
            return ShipClassCompiler.Compile(ship);
        }
        catch (Exception e)
        {
            Console.WriteLine($"  ({t.Name}, {a.Name} {a.DirectorM} m: doesn't build: {e.Message})");
            return null;
        }
    }
}
