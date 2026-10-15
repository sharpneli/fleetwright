using Fleetwright.Map;
using Fleetwright.Map.Magazines;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Effects = Fleetwright.Map.Effects;
using Xunit.Abstractions;

namespace Fleetwright.Tests;

/// <summary>The world view's magazine explosions (docs/visuals/magazine-explosions.md X2): the body's laws (continuous
/// through lift-off, rising and widening, cooling), the height knee, and the book taking each event once at its own age
/// and allocating nothing a frame.</summary>
public class MagazineTests(ITestOutputHelper output)
{
    static readonly ShipClass Dreadnought = ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(Paths.Shipgen("designs", "dreadnought.json"))));

    static ShipClass? ClassOf(int id) => id == 0 ? Dreadnought : null;

    [Fact]
    public void The_body_lifts_off_rises_and_cools_without_a_jump()
    {
        const double d = 157, life = 12.2, mass = 20_000, deck = 8;
        var before = MagazineBlast.BodyAt(d, life, mass, deck, life - 1e-6);
        var after = MagazineBlast.BodyAt(d, life, mass, deck, life + 1e-6);
        Assert.Equal(before.CentreZ, after.CentreZ, 1e-3);
        Assert.Equal(before.R, after.R, 1e-3);
        Assert.Equal(before.Heat, after.Heat, 1e-3);
        Assert.Equal(deck + d, after.CentreZ, 1e-3);   // lifted off to about its diameter
        var prev = MagazineBlast.BodyAt(d, life, mass, deck, 0);
        Assert.Equal(1, prev.Heat, 6);
        for (double t = 0.5; t <= 120; t += 0.5)
        {
            var b = MagazineBlast.BodyAt(d, life, mass, deck, t);
            Assert.True(b.CentreZ >= prev.CentreZ - 1e-9 && b.R >= prev.R - 1e-9, $"rising and widening at {t} s");
            Assert.True(b.Heat <= prev.Heat + 1e-9, $"cooling at {t} s");
            prev = b;
        }
        var minute = MagazineBlast.BodyAt(d, life, mass, deck, 60);
        output.WriteLine($"at 60 s: centre {minute.CentreZ:0} m, radius {minute.R:0} m, depth {minute.Tau:0.0}");
        Assert.InRange(minute.CentreZ, 800, 1300);   // "thousands of feet" by a minute
    }

    [Fact]
    public void Heights_are_true_to_the_knee_and_bounded_above_it()
    {
        Assert.Equal(80, Effects.Drawn(80));
        Assert.Equal(Effects.KneeM, Effects.Drawn(Effects.KneeM));
        Assert.Equal(Effects.KneeM + 1e-3, Effects.Drawn(Effects.KneeM + 1e-3), 1e-6);   // no kink's jump
        Assert.True(Effects.Drawn(1000) < Effects.KneeM + Effects.KneeHcM);
        Assert.True(Effects.Drawn(1000) > Effects.Drawn(900));
    }

    [Fact]
    public void A_shells_flash_is_where_it_hit_and_a_fires_over_the_magazine()
    {
        var shell = Blasts((1000, 1));
        shell.Point[0] = new P3(12, 3, Dreadnought.Vertical.Waterline + 6);
        var fire = Blasts((1000, 2));
        fire.Cause[0] = BlastCause.Fire;
        var book = new MagazineBook();
        book.Update(shell, 1000, 1, 0.016, ClassOf);
        book.Update(fire, 1000, 1, 0.016, ClassOf);
        Assert.Equal(new P3(12, 3, 6), book[0].Hit);
        var ev = book[1].Event;
        Assert.Equal(ev.X, book[1].Hit.X, 6);
        Assert.Equal(ev.Y, book[1].Hit.Y, 6);
        Assert.True(book[1].Hit.Z >= ev.DeckZ - 1e-6, "on the barbette's top or the deck");
        // the mounts' bearings: the class's rests when she isn't in view
        Assert.Equal(Dreadnought.Mounts[0].RestDeg, book[0].Bearings[0]);
    }

    [Fact]
    public void The_debris_comes_down_round_the_wreck_and_the_gunhouse_goes_off_its_barbette()
    {
        var splashes = new Fleetwright.Map.Splashes.SplashBook();
        var book = new MagazineBook();
        book.Update(Blasts((1000, 1)), 1000, 1, 0.016, ClassOf, null, splashes);
        var e = book[0];
        var d = e.Debris;
        output.WriteLine($"{d.Pieces.Length} pieces, gunhouse {d.Gunhouse} (on deck {d.HouseOnDeck}), {splashes.Count} splashes");
        Assert.Equal(e.Event.Debris, d.Pieces.Length);
        Assert.Equal(d.Pieces.Length + (d.Gunhouse && !d.HouseOnDeck ? 1 : 0), splashes.Count);
        foreach (var p in d.Pieces)
        {
            double t = MagazineDebris.Landing(d.From.Z, p.V.Z);
            var land = MagazineDebris.At(d.From, p.V, t);
            Assert.Equal(0, land.Z, 3);
            Assert.InRange(new System.Numerics.Vector2(land.X - d.From.X, land.Y - d.From.Y).Length(), 0, 600);
        }
        // every splash waits for its piece: none lands before the main event
        for (int i = 0; i < splashes.Count; i++)
            Assert.True(splashes[i].ColumnT0 - splashes.ColumnClock > MagazineBlast.MainAtS - 0.1);
        Assert.True(d.Gunhouse, "the dreadnought's magazine has a turret over it");
        // off its barbette 25-50 m away (Vanguard's ~40 m), turning as it flies
        var land0 = MagazineDebris.At(d.HouseFrom, d.HouseV, MagazineDebris.Landing(d.HouseFrom.Z, d.HouseV.Z));
        Assert.InRange(new System.Numerics.Vector2(land0.X - d.HouseFrom.X, land0.Y - d.HouseFrom.Y).Length(), 20, 55);
        // before the roof lifts it stands; in the air it's off its place; come down in the sea it's gone
        var mount = e.Event.Mount;
        Assert.False(book.Thrown(1, mount, out _));
        Assert.False(book.Thrown(1, mount == 0 ? 1 : 0, out _));
    }

    static BlastsView Blasts(params (long Ms, int Ship)[] oldestFirst)
    {
        var v = new BlastsView();
        v.EnsureCapacity(oldestFirst.Length);
        var at = Earth.Position(56.5, 4.0);
        for (int i = 0; i < oldestFirst.Length; i++)
        {
            var (ms, ship) = oldestFirst[oldestFirst.Length - 1 - i];
            (v.Ms[i], v.Ship[i], v.Class[i], v.Room[i], v.Cause[i]) = (ms, ship, 0, -1, BlastCause.Shell);
            (v.At[i], v.Forward[i]) = (at, Earth.Forward(at, 90));
        }
        v.Count = oldestFirst.Length;
        return v;
    }

    [Fact]
    public void Each_event_is_taken_once_at_its_own_age()
    {
        var book = new MagazineBook();
        book.Update(Blasts((1000, 1)), 1000, 1, 0.016, ClassOf);
        book.Update(Blasts((1000, 1), (1000, 2)), 31_000, 1, 0.016, ClassOf);
        book.Update(Blasts((1000, 1), (1000, 2)), 31_000, 1, 0.016, ClassOf);
        Assert.Equal(2, book.Count);
        // the second was first seen 30 s after it went: drawn at its age, not from its first sight
        Assert.Equal(30, book.Clock - book[1].T0, 1);
        // a magazine of the class's (the record named none: the fullest), its fireball scattered about the event's
        var e = book[0];
        Assert.Equal(RoomKind.Magazine, Dreadnought.Rooms[e.Event.Room].Kind);
        Assert.InRange(e.FireballM / e.Event.FireballM, 0.5, 2);
        Assert.NotEqual(book[0].Seed, book[1].Seed);
    }

    [Fact]
    public void A_frame_allocates_nothing()
    {
        var book = new MagazineBook();
        var v = Blasts((1000, 1), (2000, 2));
        book.Update(v, 2000, 1, 0.016, ClassOf);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int f = 0; f < 600; f++)
            book.Update(v, 2000 + f * 16, 1, 0.016, ClassOf);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(2, book.Count);
    }
}
