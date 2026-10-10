using Fleetwright.Shipgen.Tools;

namespace Fleetwright.Shipgen.Tests;

/// <summary>Every design's citadel is tight: shells from every bearing, falling 0-50°, reach it only through armour or
/// water (shell-test, uptake openings grated). Off by default until it joins the suite: remove Skip to run it.</summary>
public class ShellTests
{
    [Fact(Skip = "off by default for now: run on demand (or `shipgen shell-test shipgen/designs/*.json --gratings`)")]
    public void NoDesignLeaks()
    {
        var failures = new List<string>();
        foreach (var path in Directory.GetFiles(Paths.Shipgen("designs"), "*.json").Order(StringComparer.Ordinal))
        {
            var design = Design.Load(path);
            if (ShipDesign.Validate(design, true).Count > 0)
                continue;
            var r = ShellTest.Run(ShipDesign.Build(design).Hitboxes, 20000, 1, gratings: true);
            if (r.Leaks.Count > 0)
                failures.Add(ShellTest.Text(design.Id ?? path, r, 3));
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(5)));
    }
}
