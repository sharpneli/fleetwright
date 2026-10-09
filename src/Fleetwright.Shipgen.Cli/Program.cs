using System.Diagnostics;
using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Golden;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using Fleetwright.Shipgen.Render.Golden;

namespace Fleetwright.Shipgen.Cli;

/// <summary>The shipgen command: the test harness of the port (design.py's data output, validation strings and the
/// golden comparison). Arguments are parsed by hand: a few flags, and AOT-safe.</summary>
public static class Program
{
    const string Usage = """
        shipgen validate DESIGN.json... [--no-limits]
            print the validation strings (shipdesign.validate + looks.validate)
        shipgen design DESIGN.json... [--no-limits] [--out DIR]
            build each design: DIR/<id>/report.json, hitboxes.json and ship.json, and design.py's summary line
        shipgen capture [--root DIR] [--out DIR] [--jobs N] [CASE...]
            golden.py's design capture from our side: DIR/<case>.json.gz for each golden case (default all)
        shipgen golden-diff GOLDEN OUT [--show N]
            compare two captures (directories of <case>.json.gz, or two files) by the golden rules
        shipgen golden-check [--root DIR] [--jobs N] [--show N] [CASE|PREFIX*...]
            capture and compare against the goldens in one go, printing each failing case's first differences
        shipgen bench [--root DIR] [--repeat N] [CASE|PREFIX*...]
            single-threaded build time per case (best of N), next to Python's from the golden capture, and the
            designer's per-knob rebuild (the same design with the length hint)
        shipgen draw DESIGN.json... [--out DIR] [--scale S] [--mips N]
            draw each design (default 10 px/m, 5 mips): DIR/<id>/sprite.json and the display lists as SVG (hull.svg,
            height.svg, turrets/<type>.svg)
        shipgen bake DESIGN.json... [--out DIR] [--scale S] [--mips N]
            draw and bake each design on the GPU: DIR/<id>/sprite.json, hull.png, height.png, turrets/<type>.png
            and each layer's _mips.png
        shipgen png-check [--root DIR] [--out DIR] [CASE|PREFIX*...]
            bake every design with a sprite golden and compare its PNGs' coverage (IoU) with Python's; --out keeps
            the PNGs
        shipgen svg-check [--root DIR] [--jobs N] [--show N] [CASE|PREFIX*...]
            every case's SVGs against golden/svg (Python's drawing with the port's RNG)
        shipgen sprite-check [--root DIR] [--jobs N] [--show N] [CASE|PREFIX*...]
            every design's sprite.json against golden/sprite
        --root defaults to the repo's shipgen folder (found from the current directory up).
        """;

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }
        var a = new Args(args.Skip(1));
        try
        {
            return args[0] switch
            {
                "validate" => Validate(a),
                "design" => Design(a),
                "capture" => Capture(a),
                "golden-diff" => GoldenDiffCmd(a),
                "golden-check" => GoldenCheck(a),
                "bench" => Bench(a),
                "draw" => Draw(a),
                "bake" => BakeCmd(a),
                "png-check" => PngCheck(a),
                "svg-check" => Check(a, RenderGolden.CheckSvgs),
                "sprite-check" => Check(a, RenderGolden.CheckSprite),
                _ => Fail($"unknown command {args[0]}\n\n{Usage}"),
            };
        }
        catch (ArgumentException e)
        {
            return Fail(e.Message);
        }
    }

    static int Fail(string msg)
    {
        Console.Error.WriteLine(msg);
        return 2;
    }

    sealed class Args
    {
        public readonly List<string> Positional = [];
        readonly Dictionary<string, string?> flags = new(StringComparer.Ordinal);

        public Args(IEnumerable<string> args)
        {
            var list = args.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                string s = list[i];
                if (!s.StartsWith("--", StringComparison.Ordinal))
                {
                    Positional.Add(s);
                    continue;
                }
                int eq = s.IndexOf('=');
                if (eq > 0)
                    flags[s[2..eq]] = s[(eq + 1)..];
                else if (s is "--no-limits")
                    flags[s[2..]] = null;
                else if (i + 1 < list.Count)
                    flags[s[2..]] = list[++i];
                else
                    throw new ArgumentException($"{s} needs a value");
            }
        }

        public bool Has(string k) => flags.ContainsKey(k);
        public string? Get(string k, string? def = null) => flags.TryGetValue(k, out var v) ? v : def;
        public int Int(string k, int def) => flags.TryGetValue(k, out var v) && v != null ? int.Parse(v) : def;
    }

    static string Root(Args a)
    {
        if (a.Get("root") is string r)
            return r;
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "shipgen", "golden")))
            dir = dir.Parent;
        return dir != null ? Path.Combine(dir.FullName, "shipgen")
            : throw new ArgumentException("no shipgen/golden folder above the current directory: give --root");
    }

    static PyDict LoadDesign(string path) => (PyDict)PyJson.Load(path)!;

    static int Validate(Args a)
    {
        bool limits = !a.Has("no-limits");
        int bad = 0;
        foreach (var p in a.Positional)
        {
            var d = LoadDesign(p);
            var errs = ShipDesign.Validate(d, limits).Concat(Looks.Validate(d)).ToList();
            Console.WriteLine(errs.Count == 0 ? $"{p}: ok" : $"{p}: invalid input:\n  " + string.Join("\n  ", errs));
            bad += errs.Count > 0 ? 1 : 0;
        }
        return bad > 0 ? 1 : 0;
    }

    static int Design(Args a)
    {
        bool limits = !a.Has("no-limits");
        string outDir = a.Get("out", "out_designs")!;
        int crashed = 0;
        foreach (var p in a.Positional)
        {
            var design = LoadDesign(p);
            var errs = ShipDesign.Validate(design, limits).Concat(Looks.Validate(design)).ToList();
            if (errs.Count > 0)
            {
                Console.WriteLine($"{p}: invalid input:\n  " + string.Join("\n  ", errs));
                continue;
            }
            PyDict ship;
            try
            {
                ship = ShipDesign.Build(design);
            }
            catch (NotImplementedException e)
            {
                Console.WriteLine($"{p}: not ported yet ({e.Message})");
                return 1;
            }
            catch (Exception e)
            {
                Console.WriteLine($"{p}: the generator broke on this design: {GoldenCases.PyName(e)}: {e.Message}");
                crashed++;
                continue;
            }
            string dir = Path.Combine(outDir, design.S("id"));
            var rep = ship.D("report");
            PyJson.Save(Path.Combine(dir, "report.json"), rep, 2);
            PyJson.Save(Path.Combine(dir, "hitboxes.json"), ship["hitboxes"], 1);
            PyJson.Save(Path.Combine(dir, "ship.json"), ship, 1);
            Console.WriteLine(Summary(design, rep));
            foreach (var e in rep.L("errors"))
                Console.WriteLine("      ERROR: " + e);
            foreach (var w in rep.L("warnings"))
                Console.WriteLine("      warn:  " + w);
        }
        return crashed > 0 ? 1 : 0;
    }

    /// <summary>design.py's one-line summary.</summary>
    static string Summary(PyDict design, PyDict rep)
    {
        var res = rep.D("results");
        string R(string k, string s, int w) => s.PadLeft(w);
        return $"{design.S("id"),14}: {(Py.Truthy(rep["valid"]) ? "OK " : "BAD")} " +
               $"{R("", Py.F(res["length_m"], 1), 5)} x {R("", Py.F(res["beam_m"], 1), 4)} m  " +
               $"std {Py.Comma(Py.ToLong(res["standard_displacement_t"])),6} t " +
               $"full {Py.Comma(Py.ToLong(res["full_displacement_t"])),6} t  T {Py.Str(res["draught_m"]),5} m  " +
               $"{Py.F(res["power_shp"], 0, comma: true),9} shp  " +
               $"GM {Py.Str(res["gm_full_m"]),5}  trim {Py.F(res["trim_m"], 2, plus: true)}  " +
               $"shift {Py.F(res["layout_shift_m"], 1, plus: true)}";
    }

    static List<GoldenCase> SelectCases(string root, IReadOnlyList<string> pats)
    {
        var all = GoldenCases.Load(root);
        if (pats.Count == 0)
            return all;
        return all.Where(c => pats.Any(p => p.EndsWith('*') ? c.Name.StartsWith(p[..^1], StringComparison.Ordinal)
                                                             : c.Name == p)).ToList();
    }

    static int Capture(Args a)
    {
        string root = Root(a);
        string outDir = a.Get("out", "capture")!;
        var cases = SelectCases(root, a.Positional);
        var sw = Stopwatch.StartNew();
        int n = 0;
        Parallel.ForEach(cases, new ParallelOptions { MaxDegreeOfParallelism = a.Int("jobs", Environment.ProcessorCount / 2) },
            c =>
            {
                var rec = GoldenCases.Capture(LoadDesign(c.DesignPath));
                PyJson.Save(Path.Combine(outDir, c.Name + ".json.gz"), rec, 1);
                Console.WriteLine($"[{Interlocked.Increment(ref n),3}/{cases.Count}] {c.Name}");
            });
        Console.WriteLine($"{cases.Count} cases in {sw.Elapsed.TotalSeconds:F1} s -> {outDir}");
        return 0;
    }

    static int GoldenDiffCmd(Args a)
    {
        if (a.Positional.Count != 2)
            throw new ArgumentException("golden-diff GOLDEN OUT");
        string g = a.Positional[0], o = a.Positional[1];
        int show = a.Int("show", 5);
        var pairs = new List<(string Name, string G, string O)>();
        if (File.Exists(g))
            pairs.Add((Path.GetFileName(g), g, o));
        else
            foreach (var f in Directory.GetFiles(g, "*.json.gz").Order(StringComparer.Ordinal))
                pairs.Add((Path.GetFileName(f)[..^8], f, Path.Combine(o, Path.GetFileName(f))));
        int bad = 0;
        foreach (var (name, gf, of) in pairs)
        {
            if (!File.Exists(of))
            {
                Console.WriteLine($"{name}: missing in {o}");
                bad++;
                continue;
            }
            var gr = (PyDict)PyJson.Load(gf)!;
            var orr = (PyDict)PyJson.Load(of)!;
            gr.Remove("build_s");
            orr.Remove("build_s");
            var diffs = Shipgen.Golden.GoldenDiff.Compare(gr, orr, show);
            if (diffs.Count > 0)
            {
                bad++;
                Console.WriteLine($"{name}: differs");
                foreach (var d in diffs)
                    Console.WriteLine("  " + d.ToString().Replace("\n", "\n  "));
            }
        }
        Console.WriteLine($"{bad} of {pairs.Count} cases differ");
        return bad > 0 ? 1 : 0;
    }

    static int Bench(Args a)
    {
        string root = Root(a);
        var cases = SelectCases(root, a.Positional);
        int repeat = a.Int("repeat", 3);
        var py = (PyDict)PyJson.Load(Path.Combine(root, "golden", "design", "capture.json"))!;
        var pyTimes = py.D("build_s");
        double totC = 0, totP = 0;
        foreach (var c in cases)
        {
            var design = LoadDesign(c.DesignPath);
            if (ShipDesign.Validate((PyDict)PyJson.Plain(design)!, false).Count > 0)
                continue;
            double best = double.MaxValue, bestHinted = double.MaxValue;
            long alloc = 0;
            for (int i = 0; i < repeat; i++)
            {
                var copy = (PyDict)PyJson.Plain(design)!;
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                var ship = ShipDesign.Build(copy);
                best = Math.Min(best, sw.Elapsed.TotalSeconds);
                alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
                // the designer's per-knob rebuild: the same design again, starting from the length it had
                double length = ship.D("report").D("results").F("length_m");
                sw.Restart();
                ShipDesign.Build((PyDict)PyJson.Plain(design)!, length);
                bestHinted = Math.Min(bestHinted, sw.Elapsed.TotalSeconds);
            }
            double p = pyTimes.Get(c.Name) is object o && o is not null ? Py.ToDouble(o) : double.NaN;
            totC += best;
            if (!double.IsNaN(p))
                totP += p;
            Console.WriteLine($"{c.Name,-24} {best,8:F3} s   python {p,8:F3} s   x{p / best,6:F1}   rebuild {bestHinted,7:F3} s" +
                              $"   {alloc / 1e6,6:F0} MB allocated");
        }
        Console.WriteLine($"total {totC:F2} s, python {totP:F2} s (x{totP / totC:F1})");
        return 0;
    }

    static int GoldenCheck(Args a)
    {
        string root = Root(a);
        var cases = SelectCases(root, a.Positional);
        int show = a.Int("show", 3);
        var sw = Stopwatch.StartNew();
        var results = new (GoldenCase C, List<Difference>? D, string? Err)[cases.Count];
        Parallel.For(0, cases.Count, new ParallelOptions { MaxDegreeOfParallelism = a.Int("jobs", Environment.ProcessorCount / 2) },
            i =>
            {
                try
                {
                    results[i] = (cases[i], GoldenCases.Check(root, cases[i], show), null);
                }
                catch (Exception e)
                {
                    results[i] = (cases[i], null, $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }
            });
        int ok = 0;
        foreach (var (c, d, err) in results)
        {
            if (err != null)
            {
                Console.WriteLine($"FAIL {c.Name}: {err.Split('\n').Take(6).Aggregate((x, y) => x + "\n     " + y)}");
                continue;
            }
            if (d!.Count == 0)
            {
                ok++;
                continue;
            }
            Console.WriteLine($"DIFF {c.Name}");
            foreach (var x in d)
                Console.WriteLine("     " + x.ToString().Replace("\n", "\n     "));
        }
        Console.WriteLine($"{ok} of {cases.Count} cases match ({sw.Elapsed.TotalSeconds:F1} s)");
        return ok == cases.Count ? 0 : 1;
    }

    static int Draw(Args a)
    {
        string outDir = a.Get("out", "out_sprites")!;
        double scale = double.Parse(a.Get("scale", "10")!, System.Globalization.CultureInfo.InvariantCulture);
        int mips = a.Int("mips", 5);
        foreach (var path in a.Positional)
        {
            var design = LoadDesign(path);
            var sp = ShipSprites.Build(ShipDesign.Build(design), scale, mips);
            string dir = Path.Combine(outDir, design.S("id"));
            Directory.CreateDirectory(Path.Combine(dir, "turrets"));
            PyJson.Save(Path.Combine(dir, "sprite.json"), sp.Meta, 2);
            File.WriteAllText(Path.Combine(dir, "hull.svg"), SvgWriter.Write(sp.Hull));
            File.WriteAllText(Path.Combine(dir, "height.svg"), SvgWriter.Write(sp.Height));
            foreach (var (tid, sc) in sp.Turrets)
                File.WriteAllText(Path.Combine(dir, "turrets", tid + ".svg"), SvgWriter.Write(sc));
            var size = sp.Meta.L("size_px");
            Console.WriteLine($"drew {design.S("id")}: {size[0]}x{size[1]} px, {sp.Turrets.Count} turret types, " +
                              $"{sp.Clutter.Count} clutter items -> {dir}");
        }
        return 0;
    }

    /// <summary>Run a per-case check over the selected cases in parallel and print each failing case.</summary>
    static int Check(Args a, Func<string, GoldenCase, int, List<string>> check)
    {
        string root = Root(a);
        var cases = SelectCases(root, a.Positional);
        int show = a.Int("show", 3);
        var sw = Stopwatch.StartNew();
        var results = new (GoldenCase C, List<string>? D, string? Err)[cases.Count];
        Parallel.For(0, cases.Count, new ParallelOptions { MaxDegreeOfParallelism = a.Int("jobs", Environment.ProcessorCount / 2) },
            i =>
            {
                try
                {
                    results[i] = (cases[i], check(root, cases[i], show), null);
                }
                catch (Exception e)
                {
                    results[i] = (cases[i], null, $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }
            });
        int ok = 0;
        foreach (var (c, d, err) in results)
        {
            if (err != null)
            {
                Console.WriteLine($"FAIL {c.Name}: {err.Split('\n').Take(8).Aggregate((x, y) => x + "\n     " + y)}");
                continue;
            }
            if (d!.Count == 0)
            {
                ok++;
                continue;
            }
            Console.WriteLine($"DIFF {c.Name}");
            foreach (var x in d)
                Console.WriteLine("     " + x.Replace("\n", "\n     "));
        }
        Console.WriteLine($"{ok} of {cases.Count} cases match ({sw.Elapsed.TotalSeconds:F1} s)");
        return ok == cases.Count ? 0 : 1;
    }

    static int BakeCmd(Args a)
    {
        string outDir = a.Get("out", "out_sprites")!;
        double scale = double.Parse(a.Get("scale", "10")!, System.Globalization.CultureInfo.InvariantCulture);
        int mips = a.Int("mips", 5);
        using var gpu = new GpuBaker();
        Console.WriteLine($"GPU: {gpu.Driver}, {gpu.Samples}");
        foreach (var path in a.Positional)
        {
            var design = LoadDesign(path);
            var sw = Stopwatch.StartNew();
            var sp = ShipSprites.Build(ShipDesign.Build(design), scale, mips);
            double tBuild = sw.Elapsed.TotalSeconds;
            var baked = ShipBake.Bake(sp, gpu);
            double tBake = sw.Elapsed.TotalSeconds - tBuild;
            string dir = Path.Combine(outDir, design.S("id"));
            ShipBake.Save(sp, baked, dir);
            Console.WriteLine($"baked {design.S("id")}: {baked.Hull.Width}x{baked.Hull.Height} px, {sp.Turrets.Count} turret types " +
                              $"(build+draw {tBuild:F2} s, bake {tBake:F2} s, save {sw.Elapsed.TotalSeconds - tBuild - tBake:F2} s) -> {dir}");
        }
        return 0;
    }

    static int PngCheck(Args a)
    {
        string root = Root(a);
        string? keep = a.Get("out");
        var cases = SelectCases(root, a.Positional)
            .Where(c => RenderGolden.State(root, "sprite", c.Name) as string == "ok").ToList();
        using var gpu = new GpuBaker();
        Console.WriteLine($"GPU: {gpu.Driver}, {gpu.Samples}; {cases.Count} designs");
        // build and draw in parallel, bake one at a time on the one device
        var sprites = new ShipSprites[cases.Count];
        Parallel.For(0, cases.Count, i => sprites[i] = RenderGolden.Draw(cases[i]));
        double worst = 1;
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"{"case",-22} {"hull",7} {"height",7} {"turret",7} {"colour",7}");
        for (int i = 0; i < cases.Count; i++)
        {
            var c = cases[i];
            var b = ShipBake.Bake(sprites[i], gpu);
            if (keep != null)
                ShipBake.Save(sprites[i], b, Path.Combine(keep, c.Name));
            string gdir = Path.Combine(root, "golden", "sprite", c.Name);
            double hull = ShipBake.CoverageIoU(Png.Load(Path.Combine(gdir, "hull.png")), b.Hull);
            double height = ShipBake.CoverageIoU(Png.Load(Path.Combine(gdir, "height.png")), b.Height);
            double tur = b.Turrets.Count == 0 ? 1 : b.Turrets.Min(t => ShipBake.CoverageIoU(Png.Load(Path.Combine(gdir, "turrets", t.Key + ".png")), t.Value));
            double col = ShipBake.ColourDiff(Png.Load(Path.Combine(gdir, "hull.png")), b.Hull);
            double m = Math.Min(hull, Math.Min(height, tur));
            worst = Math.Min(worst, m);
            Console.WriteLine($"{c.Name,-22} {hull,7:F4} {height,7:F4} {tur,7:F4} {col,7:F2}{(m < 0.98 ? "  LOW" : "")}");
        }
        Console.WriteLine($"worst IoU {worst:F4} over {cases.Count} designs (bake {sw.Elapsed.TotalSeconds:F1} s)");
        return worst >= 0.98 ? 0 : 1;
    }
}
