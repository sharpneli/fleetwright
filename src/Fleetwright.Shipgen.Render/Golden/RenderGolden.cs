using System.IO.Compression;
using System.Text;
using Fleetwright.Shipgen.Golden;

namespace Fleetwright.Shipgen.Render.Golden;

/// <summary>The drawing side against Python's goldens: golden/sprite (sprite.json per design, WSL, Python's own RNG)
/// and golden/svg (every SVG for every case that builds, drawn with PortRandom, the RNG ShipRng also implements).
/// Both at 10 px/m with 5 mips, as the captures.</summary>
public static class RenderGolden
{
    public const double Scale = 10.0;
    public const int Mips = 5;

    static string ReadGz(string path)
    {
        using var fs = File.OpenRead(path);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var sr = new StreamReader(gz, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    /// <summary>The case's state in a capture.json: "ok", "invalid", or (svg capture) a dict for a drawn case.</summary>
    public static object? State(string root, string kind, string name) =>
        ((PyDict)PyJson.Load(Path.Combine(root, "golden", kind, "capture.json"))!).D("cases").Get(name);

    public static ShipSprites Draw(GoldenCase c) =>
        ShipSprites.Build(ShipDesign.Build(Design.Load(c.DesignPath)), Scale, Mips);

    /// <summary>Every SVG of the case against golden/svg: hull, height map and each turret type. Empty when it
    /// matches or the case isn't drawn (it doesn't build).</summary>
    public static List<string> CheckSvgs(string root, GoldenCase c, int max = 10)
    {
        if (State(root, "svg", c.Name) is not PyDict st)
            return [];
        var dir = Path.Combine(root, "golden", "svg", c.Name);
        var sp = Draw(c);
        var out_ = new List<string>();
        void One(string rel, Scene sc)
        {
            if (out_.Count >= max)
                return;
            foreach (var d in SvgDiff.Compare(ReadGz(Path.Combine(dir, rel)), SvgWriter.Write(sc), max - out_.Count))
                out_.Add($"{rel}: {d}");
        }
        One("hull.svg.gz", sp.Hull);
        One("height.svg.gz", sp.Height);
        var golden = Directory.GetFiles(Path.Combine(dir, "turrets"), "*.svg.gz").Select(f => Path.GetFileName(f)[..^".svg.gz".Length])
            .Order(StringComparer.Ordinal).ToList();
        var ours = sp.Turrets.Keys.Order(StringComparer.Ordinal).ToList();
        if (!golden.SequenceEqual(ours))
            out_.Add($"turret types {string.Join(" ", golden)} vs ours {string.Join(" ", ours)}");
        foreach (var t in golden.Intersect(ours))
            One($"turrets/{t}.svg.gz", sp.Turrets[t]);
        double gMax = Math.Round(st.F("max_height_m"), 2), oMax = sp.Meta.D("shadow").F("max_height_m");
        if (gMax != oMax)
            out_.Add($"max_height_m {gMax} vs ours {oMax}");
        return out_;
    }

    static void WriteGz(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        using var gz = new GZipStream(fs, CompressionLevel.SmallestSize);
        var bytes = Encoding.UTF8.GetBytes(text);
        gz.Write(bytes, 0, bytes.Length);
    }

    /// <summary>Rewrite golden/svg and golden/sprite (sprite.json) from ours for the cases that no longer match, after a
    /// deliberate change to the output. The PNGs stay: png-check compares them by coverage. Returns the cases
    /// rewritten.</summary>
    public static (List<string> Svg, List<string> Sprite) Update(string root, IReadOnlyList<GoldenCase> cases)
    {
        var svgCapturePath = Path.Combine(root, "golden", "svg", "capture.json");
        var svgCapture = (PyDict)PyJson.Load(svgCapturePath)!;
        var svgCases = svgCapture.D("cases");
        var svg = new System.Collections.Concurrent.ConcurrentBag<(string Name, double MaxH, long Clutter)>();
        var sprite = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.ForEach(cases, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, c =>
        {
            if (CheckSvgs(root, c, 1).Count > 0)
            {
                var sp = Draw(c);
                var dir = Path.Combine(root, "golden", "svg", c.Name);
                Directory.Delete(dir, true);
                WriteGz(Path.Combine(dir, "hull.svg.gz"), SvgWriter.Write(sp.Hull));
                WriteGz(Path.Combine(dir, "height.svg.gz"), SvgWriter.Write(sp.Height));
                foreach (var (tid, sc) in sp.Turrets)
                    WriteGz(Path.Combine(dir, "turrets", tid + ".svg.gz"), SvgWriter.Write(sc));
                svg.Add((c.Name, sp.Meta.D("shadow").F("max_height_m"), sp.Clutter.Count));
            }
            if (CheckSprite(root, c, 1).Count > 0)
            {
                var meta = ShipSprites.Build(ShipDesign.Build(Design.Load(c.DesignPath)), Scale, Mips).Meta;
                PyJson.Save(Path.Combine(root, "golden", "sprite", c.Name, "sprite.json.gz"), PyJson.Plain(meta), 1);
                sprite.Add(c.Name);
            }
        });
        foreach (var (name, maxH, clutter) in svg)
            svgCases[name] = PyDict.Of(("max_height_m", maxH), ("clutter", clutter));
        if (!svg.IsEmpty)
            PyJson.Save(svgCapturePath, svgCapture, 1);
        return (svg.Select(s => s.Name).Order(StringComparer.Ordinal).ToList(), sprite.Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>sprite.json against golden/sprite by the golden rules, except shadow.max_height_m, which comes from
    /// the clutter (Python's RNG): it must lie between the tallest layout column and that plus the tallest clutter
    /// item, on both sides. Empty when it matches or the case has no sprite golden.</summary>
    public static List<string> CheckSprite(string root, GoldenCase c, int max = 10)
    {
        if (State(root, "sprite", c.Name) as string != "ok")
            return [];
        var golden = (PyDict)PyJson.Load(Path.Combine(root, "golden", "sprite", c.Name, "sprite.json.gz"))!;
        var ship = ShipDesign.Build(Design.Load(c.DesignPath));
        var ours = ShipSprites.Build(ship, Scale, Mips).Meta;
        var g = golden.Copy();
        var o = (PyDict)PyJson.Plain(ours)!;
        var gs = g.D("shadow").Copy();
        var os = o.D("shadow").Copy();
        double gh = gs.F("max_height_m"), oh = os.F("max_height_m");
        gs.Remove("max_height_m");
        os.Remove("max_height_m");
        g["shadow"] = gs;
        o["shadow"] = os;
        var out_ = GoldenDiff.Compare(g, o, max).Select(d => d.ToString()).ToList();
        double lo = Math.Round((ship.D("render").L("columns").Cast<PyDict>().Select(col => col.F("top"))).Max(), 2);
        const double tallest = 3.2;   // the boiler cowl
        foreach (var (who, h) in new[] { ("golden", gh), ("ours", oh) })
            if (h < lo - 0.005 || h > lo + tallest + 0.005)
                out_.Add($"shadow.max_height_m: {who} {h} outside [{lo}, {lo + tallest}]");
        return out_;
    }
}
