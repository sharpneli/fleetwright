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
            v.CalibreMm[i] = 305;
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

    [Fact]
    public void A_shell_through_her_lands_beyond_and_a_bursts_pieces_never_on_her()
    {
        var v = Two(1_000_000);
        v.Count = 1;
        double wl = Dreadnought.Vertical.Waterline, half = Dreadnought.Beam / 2;
        // through her from starboard, leaving to port 6 m up, falling at 10 degrees
        (v.Outcome[0], v.Burst[0], v.Shows[0], v.ExitMs[0]) = (Outcome.PassedThrough, BurstKind.None, HitShows.Exit, 450);
        (v.ExitAt[0], v.ExitDirection[0]) = (new P3(0, -half, wl + 6), new P3(0, -Math.Cos(0.1745), -Math.Sin(0.1745)));
        var book = new HitBook();
        book.Update(v, 1_000_000, 1, 0.016, ClassOf);
        var splashes = new Fleetwright.Map.Splashes.SplashBook(256);
        HitEffects.Hand(book, 1_000_000, 1, splashes, ClassOf);
        HitEffects.Hand(book, 1_000_016, 1, splashes, ClassOf);
        Assert.Equal(1, splashes.Count);   // once
        var e = book[0];
        double across = Vec3d.Dot(splashes[0].Point - e.At, e.Starboard);
        Assert.InRange(-across, half + 20, half + 60);   // 6 m / tan 10 degrees = 34 m out to port

        // a 15" HE bursting on her deck: its pieces' marks round her, none on her
        v = Two(2_000_000);
        v.Count = 1;
        (v.Outcome[0], v.Burst[0], v.Shows[0], v.Pieces[0], v.FillerKg[0]) = (Outcome.BurstInPlate, BurstKind.InPlate, HitShows.None, 2000, 100);
        book.Update(v, 2_000_000, 1, 0.016, ClassOf);
        splashes = new Fleetwright.Map.Splashes.SplashBook(256);
        HitEffects.Hand(book, 2_000_000, 1, splashes, ClassOf);
        Assert.InRange(splashes.Count, 10, (int)HitLook.PieceMarks);
        for (int i = 0; i < splashes.Count; i++)
        {
            var d = splashes[i].Point - e.At;
            double x = Vec3d.Dot(d, e.Forward), y = Vec3d.Dot(d, e.Starboard);
            Assert.False(x > Dreadnought.Hull.X0 && x < Dreadnought.Hull.X1 && Math.Abs(y) < half - 0.5, $"a mark on her at {x:0}, {y:0}");
        }
    }

    [Fact]
    public void A_hit_marks_her_once_with_a_hole_in_a_scorch_and_a_ship_keeps_the_newest()
    {
        var book = new HitBook();
        var marks = new HitMarks();
        var splashes = new Fleetwright.Map.Splashes.SplashBook(64);
        long ms = 1_000_000;
        for (int k = 0; k < HitMarks.PerShip + 5; k++, ms += 1000)
        {
            book.Update(Two(ms), ms, 1, 0.016, ClassOf);
            HitEffects.Hand(book, ms, 1, splashes, ClassOf, marks);
            HitEffects.Hand(book, ms, 1, splashes, ClassOf, marks);
        }
        var m = marks.Of(1);
        Assert.Equal(HitMarks.PerShip, m.Length);
        // a 15 kg burst inside: a hole 1.2 calibres across... in a scorch an eighth of its 14 m fireball and the hole wider
        Assert.All(m.ToArray(), x => Assert.True(x.Hole > 0 && x.Hole < 1 && x.R > 1.5 && x.R < 4, $"{x}"));
        Assert.True(marks.Of(2).IsEmpty);
    }
}
