using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using Fleetwright.Shipgen.Tools;

namespace Fleetwright.Shipgen.Tests;

/// <summary>fuzz and verify (PORTING.md Step 8). `shipgen fuzz` runs thousands of mutants in child processes with
/// time and memory caps; this is a quick in-process sample of the same contract.</summary>
[Collection("Gpu")]   // one GPU device at a time
public class ToolTests
{
    static List<PyDict> Designs() =>
        Directory.GetFiles(Paths.Shipgen("designs"), "*.json").Order(StringComparer.Ordinal)
            .Select(p => (PyDict)PyJson.Load(p)!).ToList();

    [Fact]
    public void MutantsBuildWithoutCrashing()
    {
        var bases = Designs();
        var choices = Fuzz.CorpusChoices(bases);
        var rng = new Random(7);
        var muts = Enumerable.Range(0, 48).Select(i => Fuzz.Mutate(bases[i % bases.Count], rng, i % 2 == 0, choices)).ToList();
        var bad = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, muts.Count, i =>
        {
            var (outcome, detail) = Fuzz.Check(muts[i].Design, i % 2 == 0);
            if (outcome == "crash")
                bad.Add($"{muts[i].Design.S("id")}_{i} ({string.Join("; ", muts[i].Changes)}): {detail.Split('\n')[0]}");
        });
        Assert.True(bad.IsEmpty, string.Join("\n", bad));
    }

    [Fact]
    public void MutationsAreReproducible()
    {
        var bases = Designs();
        var choices = Fuzz.CorpusChoices(bases);
        string Run() => string.Join("|", Enumerable.Range(0, 20).Select(i =>
        {
            var rng = new Random(i);
            return PyJson.Dumps(Fuzz.Mutate(bases[i % bases.Count], rng, true, choices).Design, null);
        }));
        Assert.Equal(Run(), Run());
    }

    [Fact]
    [Trait("Category", "Gpu")]
    public void BakedDesignVerifies()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fw_verify_{Environment.ProcessId}");
        try
        {
            using var gpu = new GpuBaker();
            foreach (var name in new[] { "bismarck", "fleet_carrier" })
            {
                var design = (PyDict)PyJson.Load(Paths.Shipgen("designs", name + ".json"))!;
                var ship = ShipDesign.Build(design);
                var d = Path.Combine(dir, name);
                PyJson.Save(Path.Combine(d, "hitboxes.json"), ship["hitboxes"], 1);
                var sp = ShipSprites.Build(ship, 10.0, 5);
                ShipBake.Save(sp, ShipBake.Bake(sp, gpu), d);
                var r = Verify.Check(d);
                Assert.True(r.Worst > 0.85, r.Text);
            }
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
