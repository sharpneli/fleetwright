using Fleetwright.Map.Sinking;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Tests;

/// <summary>A ship going down in the world view (docs/visuals/hits-and-fires.md I7): her deck's edge posed by the attitude
/// she goes under by, as the hit queries pose her (HitQuery.ToWorld), so the foam crosses where she does.</summary>
public class SinkingTests
{
    static readonly ShipClass Dreadnought = ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(Paths.Shipgen("designs", "dreadnought.json"))));

    [Fact]
    public void Her_deck_edge_goes_under_as_the_attitude_says_and_by_the_end_she_founders_by()
    {
        var c = Dreadnought;
        double bow = c.Hull.X1 - 5, stern = c.Hull.X0 + 5;
        var level = SinkingEffects.Posed(c, Attitude.Upright, 0, 0, c.Hull.DeckTop(0));
        Assert.Equal(c.Hull.DeckTop(0) - c.Vertical.Waterline, level.Height, 6);
        Assert.Equal(0, level.Along, 6);
        // settled down by the bow: foundering goes on down by it
        var settled = new Attitude(1, 2, 0);
        double lossMs = 0, goneMs = 600_000;
        var half = Attitude.At(c, settled, LossKind.Foundered, (long)lossMs, (long)goneMs, 450_000);
        var b = SinkingEffects.Posed(c, half, bow, 0, c.Hull.DeckTop(bow));
        var s = SinkingEffects.Posed(c, half, stern, 0, c.Hull.DeckTop(stern));
        Assert.True(b.Height < s.Height, $"bow {b.Height:0.0} m, stern {s.Height:0.0} m");
        var end = Attitude.At(c, settled, LossKind.Foundered, (long)lossMs, (long)goneMs, (long)goneMs);
        Assert.True(SinkingEffects.Posed(c, end, stern, 0, c.Hull.DeckTop(stern)).Height < 0, "gone: her highest deck under");
    }
}
