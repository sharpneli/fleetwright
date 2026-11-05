using Fleetwright.Shipgen;
using Fleetwright.Sim.Ships;
using Xunit;

namespace Fleetwright.Sim.Tests;

/// <summary>A magazine's room is the size of what it holds (docs/battle/explosions.md "Magazine volumes"): the wing wall stands
/// where the room behind it holds the load, in the hull as it is at that height, and the wing takes what's left.</summary>
public class MagazineRoomTests
{
    static ShipClass Class(string name)
    {
        string shipped = Path.Combine(Fleet.Shipgen, "designs", name + ".json");
        return ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(File.Exists(shipped) ? shipped : Path.Combine(Fleet.Repo, "Content", "Scenarios", "Designs", name + ".json"))));
    }

    /// <summary>A magazine's room over what its load, and any that share its cells, need.</summary>
    static IEnumerable<(string Room, double Ratio)> Ratios(ShipClass c)
    {
        for (int r = 0; r < c.Rooms.Length; r++)
        {
            ref readonly var rr = ref c.Rooms[r];
            if (rr.Kind != RoomKind.Magazine || !(rr.Tonnes > 0) || rr.Shared)
                continue;
            double need = rr.Tonnes / Ordnance.TPerM3;
            for (int o = 0; o < c.Rooms.Length; o++)
                if (o != r && c.Rooms[o].Kind == RoomKind.Magazine && c.Rooms[o].Shared && c.Rooms[o].Tonnes > 0)
                    foreach (int oc in c.Links(c.Rooms[o].Cells))
                        if (c.Cells[oc].Room == r)
                        {
                            need += c.Rooms[o].Tonnes / Ordnance.TPerM3;
                            break;
                        }
            yield return (c.RoomId(r), rr.VolumeM3 / need);
        }
    }

    [Theory]
    [InlineData("iron_duke")]
    [InlineData("seydlitz")]
    [InlineData("dreadnought")]
    public void A_big_ships_main_magazines_hold_their_load(string name)
    {
        var c = Class(name);
        foreach (var (room, ratio) in Ratios(c))
            if (room is not ("Magazine S fore" or "Magazine S aft" or "Magazine SB fore" or "Magazine SB aft"))
                Assert.InRange(ratio, 0.5, 1.5);
    }

    [Fact]
    public void Lions_magazines_are_the_size_of_their_load_at_her_narrow_ends_and_between_her_bunkers()
    {
        var ratios = Ratios(Class("lion")).ToDictionary(v => v.Room, v => v.Ratio);
        Assert.InRange(ratios["Magazine A"], 0.9, 1.1);      // was 1 m wide
        Assert.InRange(ratios["Magazine Q"], 0.9, 1.1);      // was the whole 23 m between the bunkers
        Assert.InRange(ratios["Magazine Y"], 0.9, 1.1);      // was 1 m wide
    }

    [Fact]
    public void A_small_magazine_shares_a_magazines_cell_never_the_wings_stores()
    {
        var c = Class("lion");
        for (int r = 0; r < c.Rooms.Length; r++)
        {
            ref readonly var rr = ref c.Rooms[r];
            if (rr.Kind != RoomKind.Magazine || !rr.Shared)
                continue;
            foreach (int cell in c.Links(rr.Cells))
                Assert.Equal(RoomKind.Magazine, c.Rooms[c.Cells[cell].Room].Kind);
        }
    }
}
