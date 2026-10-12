using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>shell-test on ShipClass. Before the first version (on the hitboxes) was deleted, the two agreed shot by shot
/// on all 71 designs, with and without gratings (commit ab66ee3, battle plan step 2.1).</summary>
public class ShellTestTests
{
    [Fact]
    public void A_run_is_its_shots_whatever_the_threads()
    {
        var c = Fleet.Get("nassau").Class!;
        var r = ShellTest.Run(c, 4000, 5);
        // the same shots one by one on this thread
        var st = new ShellTest(c);
        var stream = RandomStream.FromSeed(5);
        int armour = 0, water = 0, leak = 0;
        for (int k = 0; k < 4000; k++)
            switch (st.Trace(st.Draw(stream.Derive((ulong)k)), out _))
            {
                case ShellTest.Outcome.Armour: armour++; break;
                case ShellTest.Outcome.Water: water++; break;
                default: leak++; break;
            }
        Assert.Equal(armour, r.Bands.Sum(b => b.Armour));
        Assert.Equal(water, r.Bands.Sum(b => b.Water));
        Assert.Equal(leak, r.Leaks.Count);
        Assert.True(leak > 0, "nassau's uptakes leak without gratings");
        Assert.Equal(ShellTest.Text("nassau", r, 10), ShellTest.Text("nassau", ShellTest.Run(c, 4000, 5), 10));
    }

    /// <summary>Every design's citadel is tight: shells from every bearing, falling 0-50°, reach it only through armour
    /// or water (uptake openings grated). Off by default until it joins the suite: remove Skip to run it.</summary>
    [Fact(Skip = "off by default for now: run on demand (or `shipgen shell-test shipgen/designs/*.json --gratings`)")]
    public void No_design_leaks()
    {
        var failures = new List<string>();
        foreach (var b in Fleet.Designs)
        {
            var r = ShellTest.Run(b.Class!, 20000, 1, gratings: true);
            if (r.Leaks.Count > 0)
                failures.Add(ShellTest.Text(b.Case.Name, r, 3));
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(5)));
    }
}
