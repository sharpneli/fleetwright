using Fleetwright.Map.Hits;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Tests;

/// <summary>The world view's shell hits (docs/visuals/hits-and-fires.md I1): the book takes each hit once, back-dated
/// by its age, with its points over the sea where she was and their heights over her waterline; nothing allocates a
/// frame.</summary>
public class HitBookTests
{
    static readonly ShipClass Dreadnought = ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(Paths.Shipgen("designs", "dreadnought.json"))));

    static ShipClass? ClassOf(int id) => id == 0 ? Dreadnought : null;

    static HitsView Two(long ms)
    {
        var v = new HitsView();
        v.EnsureCapacity(2);
        v.Count = 2;
        var at = Earth.Position(56.5, 4);
        double wl = Dreadnought.Vertical.Waterline;
        for (int i = 0; i < 2; i++)
        {
            (v.Ms[i], v.Ship[i], v.Class[i], v.At[i], v.Forward[i]) = (ms, 1, 0, at, Earth.Forward(at, 90));
            (v.Point[i], v.BurstAt[i], v.Outcome[i], v.Burst[i], v.FillerKg[i]) = (new P3(10 * i, 12, wl + 2), new P3(10 * i, 10, wl + 1), Outcome.BurstInside,
                BurstKind.Inside, 15);
        }
        return v;
    }

    [Fact]
    public void Each_hit_is_taken_once_back_dated_and_placed_where_she_was()
    {
        var book = new HitBook();
        long ms = 1_000_000;
        book.Update(Two(ms), ms + 500, 1, 0.016, ClassOf);
        Assert.Equal(2, book.Count);
        Assert.Equal(book.Clock - 0.5, book[0].T0, 1e-9);
        book.Update(Two(ms), ms + 516, 1, 0.016, ClassOf);
        Assert.Equal(2, book.Count);   // the same moment's hits aren't taken again
        var e = book[0];
        Assert.Equal(2, e.Point.Z, 1e-9);   // over her waterline
        // heading east: her starboard is south, x along her east
        var at = Earth.Position(56.5, 4);
        var p = e.Sea(new P3(100, 0, 0));
        Assert.True(Vec3d.Dot(p - at, Earth.Forward(at, 90)) > 99);
        var s = e.Sea(new P3(0, 50, 0));
        Assert.True(Vec3d.Dot(s - at, Earth.Forward(at, 180)) > 49);

        var v = Two(ms);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int f = 0; f < 100; f++)
            book.Update(v, ms + 600 + f * 16, 1, 0.016, ClassOf);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
