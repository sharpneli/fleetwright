using Fleetwright.Map;
using Fleetwright.Map.Splashes;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Tests;

/// <summary>The map's book of splashes (docs/visuals/splashes.md P0, P5): each splash taken once however many snapshots
/// carry it; the column's clock slower than the world's above the knee, the foam's capped; a salvo keeping or losing
/// its column whole; splashes of one salvo landing apart animating apart (ready for the ripple); and nothing allocated
/// once it's made.</summary>
public class SplashBookTests
{
    static readonly Vec3d At = Earth.Position(56.5, 4.0);

    /// <summary>A snapshot's view of splashes, newest first, from (ms, salvo, ship hit) oldest first.</summary>
    static SplashesView View(params (long Ms, int Salvo, int Ship)[] oldestFirst)
    {
        var v = new SplashesView();
        v.EnsureCapacity(oldestFirst.Length);
        for (int i = 0; i < oldestFirst.Length; i++)
        {
            var (ms, salvo, ship) = oldestFirst[oldestFirst.Length - 1 - i];
            (v.Point[i], v.Ms[i], v.Salvo[i], v.Ship[i], v.Shooter[i], v.CalibreMm[i], v.FallDeg[i], v.Dye[i]) =
                (At, ms, salvo, ship, 1, 305, 20, Dye.None);
        }
        v.Count = oldestFirst.Length;
        return v;
    }

    [Fact]
    public void Each_splash_is_taken_once_and_hits_are_not_splashes()
    {
        var book = new SplashBook();
        book.Update(View((1000, 1, -1), (1000, 1, -1), (2000, 2, -1)), 2000, 1, 0.016, -1);
        Assert.Equal(3, book.Count);
        // the next snapshot carries them again, and two more at the newest moment, and a hit
        book.Update(View((1000, 1, -1), (1000, 1, -1), (2000, 2, -1), (2000, 2, -1), (2000, 2, 5), (3000, 3, -1)), 3000, 1, 0.016, -1);
        Assert.Equal(5, book.Count);
        book.Update(View((2000, 2, -1), (2000, 2, -1), (2000, 2, 5), (3000, 3, -1)), 3000, 1, 0.016, -1);
        Assert.Equal(5, book.Count);
        book.Clear();
        Assert.Equal(0, book.Count);
    }

    [Fact]
    public void The_columns_clock_is_slower_than_the_worlds_above_the_knee_and_the_foams_is_capped()
    {
        Assert.Equal(1, Effects.Rate(1));
        Assert.Equal(2, Effects.Rate(2));
        Assert.InRange(Effects.Rate(60), 10.9, 11.0);   // the plan's 11x at 60x
        Assert.Equal(Map.SeaView.SpeedCap, Effects.SeaRate(60));
        Assert.Equal(1, Effects.SeaStrength(8));
        Assert.InRange(Effects.SeaStrength(60), 0.36, 0.37);
        Assert.InRange(Effects.KeepShare(60), 0.36, 0.37);

        var book = new SplashBook();
        for (int f = 0; f < 60; f++)
            book.Update(new SplashesView(), 0, 60, 1 / 60.0, -1);
        Assert.InRange(book.ColumnClock, 10.9, 11.0);
        Assert.InRange(book.FoamClock, 7.99, 8.01);
    }

    [Fact]
    public void A_salvo_keeps_its_column_whole_or_not_at_all_and_the_selected_ships_always()
    {
        var book = new SplashBook();
        var shots = new List<(long, int, int)>();
        for (int salvo = 0; salvo < 200; salvo++)
            for (int g = 0; g < 8; g++)
                shots.Add((1000 + salvo * 10 + g, salvo, -1));   // a ripple: each gun a moment apart
        book.Update(View([.. shots]), 4000, 60, 0.016, -1);
        int kept = 0;
        for (int salvo = 0; salvo < 200; salvo++)
        {
            bool first = book[salvo * 8].Column;
            for (int g = 1; g < 8; g++)
                Assert.Equal(first, book[salvo * 8 + g].Column);
            kept += first ? 1 : 0;
        }
        Assert.InRange(kept / 200.0, Effects.KeepShare(60) - 0.12, Effects.KeepShare(60) + 0.12);

        var mine = new SplashBook();
        mine.Update(View([.. shots]), 4000, 60, 0.016, selected: 1);
        for (int i = 0; i < mine.Count; i++)
            Assert.True(mine[i].Column);
    }

    [Fact]
    public void Splashes_of_one_salvo_landing_apart_animate_apart()
    {
        var book = new SplashBook();
        book.Update(View((1000, 7, -1), (1060, 7, -1), (1120, 7, -1)), 1200, 1, 0.016, -1);
        Assert.Equal(0.06, book[1].ColumnT0 - book[0].ColumnT0, 6);
        Assert.Equal(0.06, book[2].ColumnT0 - book[1].ColumnT0, 6);
        Assert.Equal(0.06, book[2].FoamT0 - book[1].FoamT0, 6);
    }

    [Fact]
    public void A_frame_allocates_nothing()
    {
        var book = new SplashBook();
        var v = View((1000, 1, -1), (1000, 1, -1), (2000, 2, -1));
        book.Update(v, 2000, 1, 0.016, -1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int f = 0; f < 1000; f++)
            book.Update(v, 2000 + f * 16, 30, 0.016, -1);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
