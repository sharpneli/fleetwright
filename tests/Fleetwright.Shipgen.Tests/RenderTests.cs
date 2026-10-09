using System.IO.Compression;
using Fleetwright.Shipgen.Golden;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Golden;

namespace Fleetwright.Shipgen.Tests;

/// <summary>The drawing side against Python's goldens (PORTING.md Step 4): every case's SVGs (golden/svg, drawn with
/// the port's RNG on both sides) and every design's sprite.json (golden/sprite).</summary>
public class RenderTests
{
    static readonly string Root = Paths.Shipgen();

    static void Every(Func<string, GoldenCase, int, List<string>> check, string kind, int expectChecked)
    {
        var cases = GoldenCases.Load(Root);
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        int checkedN = 0;
        Parallel.ForEach(cases, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, c =>
        {
            var st = RenderGolden.State(Root, kind, c.Name);
            if (st is System.Text.Json.Nodes.JsonObject || (string?)st == "ok")
                Interlocked.Increment(ref checkedN);
            try
            {
                var d = check(Root, c, 3);
                if (d.Count > 0)
                    failures.Add($"{c.Name}:\n  " + string.Join("\n  ", d));
            }
            catch (Exception e)
            {
                failures.Add($"{c.Name}: {e.GetType().Name}: {e.Message}");
            }
        });
        Assert.Equal(expectChecked, checkedN);
        Assert.True(failures.IsEmpty, $"{failures.Count} cases differ:\n" +
                                      string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal).Take(10)));
    }

    [Fact]
    public void EverySvgMatches() => Every(RenderGolden.CheckSvgs, "svg", 325);

    [Fact]
    public void EverySpriteMatches() => Every(RenderGolden.CheckSprite, "sprite", 70);

    static string Golden(string rel)
    {
        using var gz = new GZipStream(File.OpenRead(Paths.Shipgen("golden", "svg", rel)), CompressionMode.Decompress);
        return new StreamReader(gz).ReadToEnd();
    }

    /// <summary>The comparer isn't blind: a changed colour, a moved point or a missing element each show.</summary>
    [Fact]
    public void SvgDiffCatchesChanges()
    {
        var g = Golden("bismarck/hull.svg.gz");
        Assert.Empty(SvgDiff.Compare(g, g));
        int body = g.IndexOf("</defs>", StringComparison.Ordinal);
        int fill = g.IndexOf("fill=\"#", body, StringComparison.Ordinal) + 7;
        var recoloured = g[..fill] + (g[fill] == '0' ? '1' : '0') + g[(fill + 1)..];
        Assert.NotEmpty(SvgDiff.Compare(g, recoloured));
        int pt = g.IndexOf(" L", body, StringComparison.Ordinal) + 2;
        while (!char.IsDigit(g[pt]))
            pt++;
        var moved = g[..pt] + (g[pt] == '1' ? '2' : '1') + g[(pt + 1)..];
        Assert.NotEmpty(SvgDiff.Compare(g, moved));
        int last = g.LastIndexOf("<circle", StringComparison.Ordinal);
        var dropped = g[..last] + g[(g.IndexOf("/>", last, StringComparison.Ordinal) + 2)..];
        Assert.NotEmpty(SvgDiff.Compare(g, dropped));
        // a clip that clips to something else (deckmargin clips the planking)
        int clip = g.IndexOf("<clipPath id=\"deckmargin\"><path d=\"M", StringComparison.Ordinal) + 36;
        while (!char.IsDigit(g[clip]))
            clip++;
        var reclipped = g[..clip] + (g[clip] == '1' ? '2' : '1') + g[(clip + 1)..];
        Assert.NotEmpty(SvgDiff.Compare(g, reclipped));
    }

    /// <summary>Every look resolves (its "from" chain), and the design side's list of navies and eras is the looks
    /// table's.</summary>
    [Fact]
    public void EveryLookResolves()
    {
        foreach (var (navy, nv) in Render.Looks.NAVIES)
            foreach (var (era, _) in ((PyDict)nv!).D("eras"))
            {
                Assert.Contains(era, Render.Looks.ERAS);
                Assert.NotNull(Render.Looks.Look(navy, era));
            }
        var bad = new Design { Look = new LookInput { Navy = "nowhere", Era = "wwii" } };
        var msg = Assert.Single(Shipgen.Looks.Validate(bad));
        Assert.Contains(string.Join(", ", Render.Looks.NAVIES.Keys), msg);
    }

    /// <summary>Drawing shares nothing mutable between threads: the same ships drawn at once come out identical.</summary>
    [Fact]
    public void ConcurrentDrawingIsIdentical()
    {
        string[] names = ["bismarck", "fleet_carrier", "dante", "pt_boat", "tanker"];
        var ships = names.Select(n => ShipDesign.Build(Design.Load(Paths.Shipgen("designs", n + ".json")))).ToList();
        string Draw(PyDict ship)
        {
            var sp = ShipSprites.Build(ship, 10.0, 5);
            return SvgWriter.Write(sp.Hull) + SvgWriter.Write(sp.Height) + string.Concat(sp.Turrets.Values.Select(SvgWriter.Write))
                   + PyJson.Dumps(sp.Meta, null);
        }
        var serial = ships.Select(Draw).ToList();
        var jobs = Enumerable.Range(0, 4).SelectMany(_ => Enumerable.Range(0, ships.Count)).ToList();
        var parallel = new string[jobs.Count];
        Parallel.For(0, jobs.Count, i => parallel[i] = Draw(ships[jobs[i]]));
        for (int i = 0; i < jobs.Count; i++)
            Assert.True(serial[jobs[i]] == parallel[i], $"{names[jobs[i]]} differs when drawn concurrently");
    }
}
