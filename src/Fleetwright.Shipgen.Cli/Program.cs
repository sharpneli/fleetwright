using System.Diagnostics;
using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Golden;
using Fleetwright.Shipgen.Tools;
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
        shipgen design DESIGN.json... [--no-limits] [--out DIR] [--scale S] [--mips N] [--no-sprites] [--previews]
            build each design (design.py): DIR/<id>/report.json, hitboxes.json, ship.json and the summary line; then
            draw and bake it on the GPU (default 10 px/m, 5 mips): sprite.json, hull.png, height.png,
            turrets/<type>.png, each layer's _mips.png; --previews adds preview_rest.png and preview_starboard.png
        shipgen capture [--root DIR] [--out DIR] [--jobs N] [CASE...]
            golden.py's design capture from our side: DIR/<case>.json.gz for each golden case (default all)
        shipgen golden-diff GOLDEN OUT [--show N]
            compare two captures (directories of <case>.json.gz, or two files) by the golden rules
        shipgen golden-check [--root DIR] [--jobs N] [--show N] [CASE|PREFIX*...]
            capture and compare against the goldens in one go, printing each failing case's first differences
        shipgen golden-update [--root DIR] [CASE|PREFIX*...]
            after a deliberate change to the output: rewrite the design, SVG and sprite.json goldens of the cases
            that no longer match from ours (the PNGs stay; png-check compares them by coverage)
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
        shipgen fuzz DESIGN.json... [--cases 150] [--seed 1] [--jobs N] [--mem-gb 3] [--timeout 120] [--slow 30]
                     [--mode all|numbers|choices|structure] [--no-limits] [--out fuzz_out]
            mutate the designs and build every mutant in its own process with a time and memory cap: any valid
            design must build (errors are fine), never crash, hang, blow up memory or publish a NaN. Failures
            (and builds slower than --slow s) are saved to --out as <id>_<case>.json and .txt
        shipgen verify DIR...
            check `design` output: turret sprites against their hitboxes at four angles, raised blocks and funnels
            opaque inside their hitboxes, the hull image against the hull outline, the subdivision and traverses
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
                "golden-update" => GoldenUpdate(a),
                "bench" => Bench(a),
                "draw" => Draw(a),
                "fuzz" => FuzzCmd(a),
                "fuzz-one" => FuzzOne(a),
                "verify" => VerifyCmd(a),
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
                else if (s is "--no-limits" or "--no-sprites" or "--previews")   // the switches
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
        double scale = double.Parse(a.Get("scale", "10")!, System.Globalization.CultureInfo.InvariantCulture);
        int mips = a.Int("mips", 5);
        GpuBaker? gpu = null;
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
            if (a.Has("no-sprites"))
                continue;
            gpu ??= new GpuBaker();
            var sp = ShipSprites.Build(ship, scale, mips);
            var baked = ShipBake.Bake(sp, gpu);
            ShipBake.Save(sp, baked, dir);
            if (a.Has("previews"))
            {
                Png.Save(Path.Combine(dir, "preview_rest.png"), Preview.Rest(sp, baked));
                Png.Save(Path.Combine(dir, "preview_starboard.png"), Preview.Starboard(sp, baked));
            }
        }
        gpu?.Dispose();
        return crashed > 0 ? 1 : 0;
    }

    /// <summary>design.py's one-line summary.</summary>
    static string Summary(PyDict design, PyDict rep)
    {
        var res = rep.D("results");
        return $"{design.S("id"),14}: {(Py.Truthy(rep["valid"]) ? "OK " : "BAD")} " +
               $"{res["length_m"],5:F1} x {res["beam_m"],4:F1} m  " +
               $"std {res["standard_displacement_t"],6:N0} t " +
               $"full {res["full_displacement_t"],6:N0} t  T {res["draught_m"],5} m  " +
               $"{res["power_shp"],9:N0} shp  " +
               $"GM {res["gm_full_m"],5}  trim {res["trim_m"]:+0.00;-0.00}  " +
               $"shift {res["layout_shift_m"]:+0.0;-0.0}";
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

    static int GoldenUpdate(Args a)
    {
        string root = Root(a);
        var cases = SelectCases(root, a.Positional);
        var design = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.ForEach(cases, new ParallelOptions { MaxDegreeOfParallelism = a.Int("jobs", Environment.ProcessorCount / 2) }, c =>
        {
            if (GoldenCases.Update(root, c))
                design.Add(c.Name);
        });
        var (svg, sprite) = RenderGolden.Update(root, cases);
        Console.WriteLine($"rewritten: {design.Count} design, {svg.Count} svg, {sprite.Count} sprite.json of {cases.Count} cases");
        foreach (var (kind, names) in new[] { ("design", design.Order(StringComparer.Ordinal).ToList()), ("svg", svg), ("sprite", sprite) })
            if (names.Count > 0)
                Console.WriteLine($"  {kind}: {string.Join(" ", names)}");
        return 0;
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

    /// <summary>The child of `fuzz`: build one mutant (IN.json) and write the outcome to OUT.txt, its first line the
    /// outcome and the rest the detail.</summary>
    static int FuzzOne(Args a)
    {
        var (outcome, detail) = Fuzz.Check(LoadDesign(a.Positional[0]), !a.Has("no-limits"));
        File.WriteAllText(a.Positional[1], outcome + "\n" + detail);
        return 0;
    }

    static int FuzzCmd(Args a)
    {
        int cases = a.Int("cases", 150), jobs = a.Int("jobs", Environment.ProcessorCount);
        int timeout = a.Int("timeout", 120);
        double memGb = double.Parse(a.Get("mem-gb", "3")!, System.Globalization.CultureInfo.InvariantCulture);
        double slowS = double.Parse(a.Get("slow", "30")!, System.Globalization.CultureInfo.InvariantCulture);
        bool limits = !a.Has("no-limits");
        string mode = a.Get("mode", "all")!, outDir = a.Get("out", "fuzz_out")!;
        var rng = new Random(a.Int("seed", 1));
        var bases = a.Positional.Select(p => (Path: p, D: LoadDesign(p))).ToList();
        if (bases.Count == 0)
            throw new ArgumentException("fuzz DESIGN.json...");
        var choices = Fuzz.CorpusChoices(bases.Select(b => b.D));
        var work = Directory.CreateTempSubdirectory("shipgen_fuzz_").FullName;
        var muts = Enumerable.Range(0, cases).Select(i =>
        {
            var (src, b) = bases[i % bases.Count];
            var (d, ch) = Fuzz.Mutate(b, rng, limits, choices, mode);
            return (Case: i, Src: src, D: d, Changes: ch);
        }).ToList();
        var counts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        var slow = new System.Collections.Concurrent.ConcurrentBag<(double S, int Case, string Src)>();
        int bad = 0;
        object print = new();
        string self = Environment.ProcessPath!;
        bool viaDotnet = Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        Parallel.ForEach(muts, new ParallelOptions { MaxDegreeOfParallelism = jobs }, m =>
        {
            string inPath = Path.Combine(work, $"{m.Case}.json"), resPath = Path.Combine(work, $"{m.Case}.txt");
            PyJson.Save(inPath, m.D, 1);
            var psi = new ProcessStartInfo(self) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
            if (viaDotnet)
                psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "shipgen.dll"));
            foreach (var x in new[] { "fuzz-one", inPath, resPath })
                psi.ArgumentList.Add(x);
            if (!limits)
                psi.ArgumentList.Add("--no-limits");
            var sw = Stopwatch.StartNew();
            using var p = Process.Start(psi)!;
            var err = p.StandardError.ReadToEndAsync();
            string outcome, detail;
            long cap = (long)(memGb * (1L << 30)), peak = 0;
            while (true)
            {
                if (p.WaitForExit(100))
                    break;
                try
                {
                    p.Refresh();
                    peak = Math.Max(peak, p.PeakWorkingSet64);
                }
                catch (InvalidOperationException)   // it exited just now
                {
                }
                if (peak > cap || sw.Elapsed.TotalSeconds > timeout)
                {
                    try { p.Kill(true); } catch (InvalidOperationException) { }
                    p.WaitForExit();
                    break;
                }
            }
            double dt = sw.Elapsed.TotalSeconds;
            if (File.Exists(resPath))
            {
                var t = File.ReadAllText(resPath);
                int nl = t.IndexOf('\n');
                (outcome, detail) = (t[..nl], t[(nl + 1)..]);
            }
            else if (dt > timeout)
                (outcome, detail) = ("hang", $"no result after {timeout} s");
            else if (peak > cap)
                (outcome, detail) = ("memory", $"over {memGb} GB");
            else
                (outcome, detail) = ("crash", $"exit {p.ExitCode}: {err.Result}");
            counts.AddOrUpdate(outcome, 1, (_, n) => n + 1);
            bool fail = outcome is "crash" or "memory" or "hang";
            if (!fail && dt > slowS)
            {
                slow.Add((dt, m.Case, m.Src));
                detail = $"{dt:F0} s ({outcome})";
                outcome = "slow";
            }
            if (fail || outcome == "slow")
            {
                Directory.CreateDirectory(outDir);
                string stem = Path.Combine(outDir, $"{m.D.S("id")}_{m.Case}");
                PyJson.Save(stem + ".json", m.D, 1);
                File.WriteAllText(stem + ".txt", $"{m.Src}, changed: {string.Join("; ", m.Changes)}\n\n{outcome}: {detail}\n");
            }
            if (fail)
            {
                Interlocked.Increment(ref bad);
                lock (print)
                    Console.WriteLine($"{outcome.ToUpperInvariant(),7} {m.D.S("id")}_{m.Case}.json  ({string.Join("; ", m.Changes)})\n" +
                                      $"        {detail.Split('\n')[0]}");
            }
        });
        Directory.Delete(work, true);
        Console.WriteLine("outcomes: " + string.Join(", ", counts.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} {kv.Value}")));
        foreach (var (sec, c, src) in slow.OrderByDescending(x => x.S).Take(10))
            Console.WriteLine($"   slow: case {c} ({src}) {sec:F0} s");
        return bad > 0 ? 1 : 0;
    }

    static int VerifyCmd(Args a)
    {
        double worst = 1;
        foreach (var dir in a.Positional)
        {
            var r = Verify.Check(dir);
            Console.WriteLine(r.Text);
            worst = Math.Min(worst, r.Worst);
        }
        Console.WriteLine(worst > 0.85 ? "\nALL OK" : "\nSOME MISMATCH");
        return worst > 0.85 ? 0 : 1;
    }
}
