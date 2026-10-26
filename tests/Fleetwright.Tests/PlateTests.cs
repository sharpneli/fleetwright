using Fleetwright.Map;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Tests;

/// <summary>The ship card's damage plate (docs/map-ui/card.md): what it draws of a class is made from her hull field and
/// hitboxes once a class, for every shipped design, and holds together: stations in order with the bottom under the deck,
/// the decks and bulkheads inside her, the upper works' outlines closed over her length, a midship section to heel.</summary>
public class PlateTests
{
    [Fact]
    public void Every_design_makes_a_plate_that_holds_together()
    {
        var paths = Directory.GetFiles(Paths.Shipgen("designs"), "*.json");
        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var cls = ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(path)));
            var p = MapScene.BuildPlate(cls);
            Assert.True(p.Xs.Length > 10, $"{name}: {p.Xs.Length} stations");
            for (int i = 0; i < p.Xs.Length; i++)
            {
                Assert.True(i == 0 || p.Xs[i] > p.Xs[i - 1], $"{name}: stations out of order at {i}");
                Assert.True(p.Bottom[i] <= p.Top[i], $"{name}: the bottom over the deck at x {p.Xs[i]:0.0}");
                Assert.True(p.Half[i] > 0, $"{name}: no breadth at x {p.Xs[i]:0.0}");
            }
            foreach (var (x0, x1, z, _) in p.Decks)
                Assert.True(x1 > x0 && z >= cls.Vertical.Keel, $"{name}: a deck at {z:0.0} m from {x0:0.0} to {x1:0.0}");
            foreach (var (x, lo, hi) in p.Bulkheads)
                Assert.True(x >= p.Xs[0] - 1 && x <= p.Xs[^1] + 1, $"{name}: a bulkhead at {x:0.0} outside her");
            foreach (var run in p.Sky)
            {
                Assert.True(run.Length >= 4, $"{name}: an upper works' run of {run.Length} points");
                Assert.Equal(run[0].Y, run[^1].Y);   // from the deck round to the deck
                Assert.True(run[0].X >= p.X0 - 5 && run[^1].X <= p.X1 + 5, $"{name}: upper works past her ends");
            }
            Assert.True(p.Section.Length >= 6, $"{name}: a section of {p.Section.Length} points");
            Assert.True(p.ZHigh >= cls.Vertical.Keel + cls.Vertical.Depth, $"{name}: the plate's top under her deck");
        }
    }
}
