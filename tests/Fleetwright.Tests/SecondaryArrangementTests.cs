using Fleetwright.Designer;
using Fleetwright.Shipgen;
using Pos = Fleetwright.Designer.SecondaryArrangement.Pos;

namespace Fleetwright.Tests;

/// <summary>The Secondary tab's edits: a position is mount + tier + stands_on written together, pairs move between
/// batteries, and every edit leaves the design valid.</summary>
public class SecondaryArrangementTests
{
    static Design Load(string name) => Design.Load(Paths.Shipgen("designs", name + ".json"));

    /// <summary>Writing each battery's position back as it is changes nothing the engine reads, in every shipped design.</summary>
    [Fact]
    public void SetPos_round_trips_every_design()
    {
        foreach (var p in Directory.GetFiles(Paths.Shipgen("designs"), "*.json"))
        {
            var d = Design.Load(p);
            var secs = d.Secondary ?? [];
            for (int k = 0; k < secs.Count; k++)
            {
                var back = SecondaryArrangement.SetPos(d, k, SecondaryArrangement.Of(secs[k])).Secondary![k];
                Assert.True((back.Mount ?? "deck", back.Tier ?? "lower", back.StandsOn ?? "deck") ==
                            (secs[k].Mount ?? "deck", secs[k].Tier ?? "lower", secs[k].StandsOn ?? "deck"), $"{Path.GetFileName(p)} {k}");
            }
        }
    }

    [Fact]
    public void Every_position_is_valid()
    {
        var d = Load("babel");
        foreach (var pos in SecondaryArrangement.All)
        {
            var x = SecondaryArrangement.SetPos(d, 0, pos);
            Assert.Equal(pos, SecondaryArrangement.Of(x.Secondary![0]));
            Assert.Empty(ShipDesign.Validate(x, limits: true));
        }
    }

    [Fact]
    public void The_track_plus_adds_a_pair_or_a_battery()
    {
        var d = Load("kongo");   // S: 152 mm lower casemates, 8 a side; SB: 76 mm on the deckhouse
        var (same, k) = SecondaryArrangement.AddPairAt(d, 0, Pos.Lower, takesList: true);
        Assert.Equal((0, 9), (k, same.Secondary![0].PerSide));
        var (other, j) = SecondaryArrangement.AddPairAt(d, 0, Pos.House, takesList: true);
        Assert.Equal(2, j);
        var nb = other.Secondary![2];
        Assert.Equal((152.0, Pos.House, 1), (nb.CalibreMm, SecondaryArrangement.Of(nb), nb.PerSide));
        Assert.Empty(ShipDesign.Validate(other, limits: true));
    }

    [Fact]
    public void Pairs_split_and_move()
    {
        var d = Load("kongo");
        var split = SecondaryArrangement.SplitPair(d, 0);
        Assert.Equal(new int?[] { 7, 1, 4 }, split.Secondary!.Select(b => b.PerSide).ToArray());
        var moved = SecondaryArrangement.MovePair(d, 0, 1);
        Assert.Equal(new int?[] { 7, 5 }, moved.Secondary!.Select(b => b.PerSide).ToArray());
        Assert.Equal(3, SecondaryArrangement.RemovePair(d, 1).Secondary![1].PerSide);
    }

    [Fact]
    public void Mount_ids_parse()
    {
        Assert.Equal((0, 1, 1), SecondaryArrangement.ParseId("S1S"));
        Assert.Equal((1, 12, -1), SecondaryArrangement.ParseId("SB12P"));
        Assert.Equal((0, 3, 0), SecondaryArrangement.ParseId("S3"));
        Assert.Null(SecondaryArrangement.ParseId("SB"));
        Assert.Null(SecondaryArrangement.ParseId("A"));
    }
}
