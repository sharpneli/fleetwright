using Fleetwright.Map;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Tests;

/// <summary>The strip's Make smoke goes to her squadron, never a force's (<see cref="MapScene.Smokers"/>): a squadron
/// divided into a force of two divisions makes smoke by divisions, as two squadrons would.</summary>
public class StripSmokeTests
{
    [Fact]
    public void Smoke_goes_to_her_division_not_the_divided_squadron()
    {
        var settings = SimSettings.Load(Path.Combine(Paths.Root, "Content", "Sim", "settings.json")) with { RailsEnabled = false };
        using var registry = new ShipClassRegistry(0);
        var w = new World(WorldTime.FromUtc(new DateTime(1916, 5, 31, 14, 0, 0, DateTimeKind.Utc)), 3, settings, registry);
        int c = w.Ships.AddClass(ShipClassCompiler.Compile(Shipgen.ShipDesign.Build(Shipgen.Design.Load(Paths.Shipgen("designs", "dreadnought.json")))));
        for (int i = 0; i < 4; i++)
            w.Apply(ShipSystem.Spawn(c, 56.5 - 0.005 * i, 4.0, 90, 16, 0));
        w.Apply(GroupSystem.CreateOfNewest(4));
        var ships = w.Groups[0].Members.ToArray();
        int squadron = w.Groups[0].GroupId;
        w.Apply(GroupSystem.Divide(squadron, 2));
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        var gv = snap.Groups;
        var into = new List<int>();

        int g = MapScene.Smokers(gv, ships[3], toUnit: true, into);
        Assert.False(gv.Force[g]);
        Assert.Equal(squadron, gv.Parent[g]);   // a division of the force the squadron became
        Assert.Equal(ships[2..], into);
        Assert.Equal(-1, MapScene.Smokers(gv, ships[3], toUnit: false, into));
        Assert.Equal([ships[3]], into);
    }
}
