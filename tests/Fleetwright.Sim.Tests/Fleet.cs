using System.Collections.Concurrent;
using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Golden;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>The golden cases built and compiled once for the tests that need them: every case's class, and the 71
/// designs' (not the fuzz mutants') built ships too.</summary>
static class Fleet
{
    public static readonly string Repo = FindRepo();

    static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Fleetwright.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("no Fleetwright.slnx above the tests");
    }

    public static string Shipgen => Path.Combine(Repo, "shipgen");

    public sealed record Built(GoldenCase Case, Ship? Ship, ShipClass? Class, string? Error);

    static readonly Lazy<List<Built>> all = new(() =>
    {
        var cases = GoldenCases.Load(Shipgen);
        var built = new ConcurrentBag<Built>();
        Parallel.ForEach(cases, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, c =>
        {
            var design = Design.Load(c.DesignPath);
            if (ShipDesign.Validate(design, limits: false).Count > 0)
                return;
            Ship ship;
            try
            {
                ship = ShipDesign.Build(design);
            }
            catch (Exception)
            {
                return;   // a case whose golden build is an exception: nothing to compile
            }
            bool keep = !c.DesignPath.Contains($"{Path.DirectorySeparatorChar}fuzz{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
            try
            {
                built.Add(new Built(c, keep ? ship : null, ShipClassCompiler.Compile(ship), null));
            }
            catch (ShipCompileException e)
            {
                built.Add(new Built(c, keep ? ship : null, null, e.Message));
            }
        });
        return [.. built.OrderBy(b => b.Case.Name, StringComparer.Ordinal)];
    });

    /// <summary>Every golden case that builds.</summary>
    public static List<Built> All => all.Value;

    /// <summary>The 71 designs that build, with their ships.</summary>
    public static IEnumerable<Built> Designs => All.Where(b => b.Ship != null);

    public static Built Get(string name) => All.Single(b => b.Case.Name == name);

    static readonly ConcurrentDictionary<string, ShipClass> scenarioClasses = new(StringComparer.Ordinal);

    /// <summary>A class from the scenarios' own designs, else a shipped one (as the CLI resolves a name), compiled once.</summary>
    public static ShipClass Scenario(string name) => scenarioClasses.GetOrAdd(name, n =>
    {
        string path = Path.Combine(Repo, "Content", "Scenarios", "Designs", n + ".json");
        return ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(File.Exists(path) ? path : Path.Combine(Shipgen, "designs", n + ".json"))));
    });
}
