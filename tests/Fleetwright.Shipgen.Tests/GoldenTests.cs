using Fleetwright.Shipgen.Golden;

namespace Fleetwright.Shipgen.Tests;

/// <summary>The port against Python's goldens (shipgen/golden, PORTING.md Step 1): every design and fuzz mutant,
/// validation strings exactly, the whole ship dict by the golden rules (GoldenDiff), and the hint check.</summary>
public class GoldenTests
{
    static readonly string Root = Paths.Shipgen();

    [Fact]
    public void EveryCaseMatches()
    {
        var cases = GoldenCases.Load(Root);
        Assert.Equal(371, cases.Count);
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.ForEach(cases, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, c =>
        {
            try
            {
                var d = GoldenCases.Check(Root, c, 3);
                if (d.Count > 0)
                    failures.Add($"{c.Name}:\n  " + string.Join("\n  ", d));
            }
            catch (Exception e)
            {
                failures.Add($"{c.Name}: {e.GetType().Name}: {e.Message}");
            }
        });
        Assert.True(failures.IsEmpty, $"{failures.Count} of {cases.Count} cases differ:\n" +
                                      string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal).Take(10)));
    }

    /// <summary>Builds share nothing mutable: the same designs built at once on many threads come out identical to
    /// the same designs built one at a time.</summary>
    [Fact]
    public void ConcurrentBuildsAreIdentical()
    {
        string[] names = ["bismarck", "fleet_carrier", "tanker", "pt_boat", "destroyer", "fuzz_free_057"];
        var cases = GoldenCases.Load(Root).Where(c => names.Contains(c.Name)).ToList();
        var designs = cases.Select(c => Design.Load(c.DesignPath)).ToList();
        var serial = designs.Select(d => PyJson.Dumps(ShipDesign.Build(d), null)).ToList();
        var jobs = Enumerable.Range(0, 4).SelectMany(_ => Enumerable.Range(0, designs.Count)).ToList();
        var parallel = new string[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            parallel[i] = PyJson.Dumps(ShipDesign.Build(designs[jobs[i]]), null));
        for (int i = 0; i < jobs.Count; i++)
            Assert.True(serial[jobs[i]] == parallel[i], $"{cases[jobs[i]].Name} differs when built concurrently");
    }
}
