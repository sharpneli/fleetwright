using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A clear line of fire (docs/battle/command.md 5.1a): a battery doesn't take a target with a friend near where
/// its shells would come down, and drops one when a friend comes there; torpedoes aren't fired with a friend on their
/// track, as she'll be by the time they pass. The same friend out of the way changes nothing.</summary>
public class LineOfFireTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    // the line of fire alone: hits do no damage, so a friend shot up by the enemy doesn't fall out of the scene (her guns
    // silent for want of men since docs/battle/crew.md N2)
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { HitsDoDamage = false };
    const double Lat = 56.0, Lon = 4.0;

    record Placed(string Design, double BearingDeg, double RangeM, double CourseDeg, double Kn, byte Side);

    static (World W, AiSystem Ai) Make(params Placed[] ships)
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        foreach (var s in ships)
        {
            int c = w.Ships.AddClass(Fleet.Get(s.Design).Class!);
            var p = Earth.Position(Lat, Lon);
            var f = Earth.Forward(p, s.BearingDeg);
            if (s.RangeM > 0)
                Earth.Advance(ref p, ref f, s.RangeM);
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(c, la, lo, s.CourseDeg, s.Kn, s.Side));
        }
        return (w, ai);
    }

    static int Target(World w, int ship, int battery) => w.Ships.Store.Combat.Target(w.Ships.Store.SlotOf(ship), battery);

    /// <summary>The torpedoes a ship has fired, by the combat log.</summary>
    static int TorpedoesBy(World w, int ship)
    {
        int n = 0;
        for (int i = 0; i < w.CombatLog.Count; i++)
            if (w.CombatLog.Newest(i) is { Kind: CombatEvent.Torpedoes } e && e.Shooter == ship)
                n += e.Count;
        return n;
    }

    [Fact]
    public void A_battery_holds_its_fire_with_a_friend_near_the_fall()
    {
        // the target 13 km east; a friend 12.8 km east, 150 m north of the line, both steaming north at the shooter's speed
        var (w, _) = Make(new Placed("super_dreadnought", 0, 0, 0, 12, 0), new Placed("light_cruiser", 89.3, 12_800, 0, 12, 0),
            new Placed("super_dreadnought", 90, 13_000, 0, 12, 1));
        w.AdvanceTo(w.Time + 3 * 60_000);
        output.WriteLine($"with a friend near the fall: the main battery on #{Target(w, 1, 0)}");
        Assert.NotEqual(3, Target(w, 1, 0));

        var (w2, _) = Make(new Placed("super_dreadnought", 0, 0, 0, 12, 0), new Placed("light_cruiser", 0, 12_800, 0, 12, 0),
            new Placed("super_dreadnought", 90, 13_000, 0, 12, 1));
        w2.AdvanceTo(w2.Time + 3 * 60_000);
        Assert.Equal(3, Target(w2, 1, 0));
    }

    [Fact]
    public void A_battery_drops_its_target_when_a_friend_steams_into_the_fall()
    {
        // the friend 11 km east, 2.5 km south of the line, steaming north at 20 kn across it while the others hold still
        var (w, _) = Make(new Placed("super_dreadnought", 0, 0, 0, 0, 0), new Placed("light_cruiser", 103, 11_300, 0, 20, 0),
            new Placed("super_dreadnought", 90, 13_000, 0, 0, 1));
        w.AdvanceTo(w.Time + 2 * 60_000);
        Assert.Equal(3, Target(w, 1, 0));   // clear: engaged
        bool dropped = false;
        for (int s = 0; s < 6 * 60 && !dropped; s++)
        {
            w.AdvanceTo(w.Time + 1000);
            dropped = Target(w, 1, 0) != 3;
        }
        Assert.True(dropped, "the friend crossed the fall and the battery held");
    }

    [Fact]
    public void Torpedoes_are_not_fired_with_a_friend_on_their_track()
    {
        // a destroyer 4 km west of a stopped battleship (well within its torpedoes' run), a friend 600 m along the track
        // (the aim leads the navy's first estimate of the target, its first fix's prior speed, by as much as ~13°: 1 km on
        // she'd be 220 m off the track, clear by the rule, as a later first sighting had it in fire-control.md F4d)
        var (w, ai) = Make(new Placed("destroyer", 270, 4_000, 0, 0, 0), new Placed("light_cruiser", 270, 3_400, 0, 0, 0),
            new Placed("super_dreadnought", 0, 0, 0, 0, 1));
        w.Apply(ai.Orders.Engage(Unit.Ship(1), 3));
        w.AdvanceTo(w.Time + 3 * 60_000);
        Assert.Equal(0, TorpedoesBy(w, 1));

        var (w2, ai2) = Make(new Placed("destroyer", 270, 4_000, 0, 0, 0), new Placed("light_cruiser", 0, 2_000, 0, 0, 0),
            new Placed("super_dreadnought", 0, 0, 0, 0, 1));
        w2.Apply(ai2.Orders.Engage(Unit.Ship(1), 3));
        w2.AdvanceTo(w2.Time + 3 * 60_000);
        output.WriteLine($"clear: {TorpedoesBy(w2, 1)} torpedoes fired");
        Assert.True(TorpedoesBy(w2, 1) > 0);
    }

    [Fact]
    public void A_torpedo_track_is_judged_as_the_ships_move()
    {
        // a destroyer 4 km west of a stopped battleship, steaming north at 25 kn, fires on her beam, a friend in company
        // (the same course and speed). Seen from the two of them her torpedoes run east and fall astern, about 35° (they
        // make 36 kn): a friend 600 m along the track and 50 m north of it is clear of them (a flotilla turned together
        // fires past the boats ahead of it); one 600 m along and 420 m south of it, clear of the track as it lies now, is
        // where they'll pass
        var (w, ai) = Make(new Placed("destroyer", 270, 4_000, 0, 25, 0), new Placed("destroyer", 270.84, 3_400, 0, 25, 0),
            new Placed("super_dreadnought", 0, 0, 0, 0, 1));
        w.Apply(ai.Orders.Engage(Unit.Ship(1), 3));
        w.AdvanceTo(w.Time + 60_000);
        output.WriteLine($"a friend ahead: {TorpedoesBy(w, 1)} torpedoes fired");
        Assert.True(TorpedoesBy(w, 1) > 0);

        var (w2, ai2) = Make(new Placed("destroyer", 270, 4_000, 0, 25, 0), new Placed("destroyer", 262.96, 3_426, 0, 25, 0),
            new Placed("super_dreadnought", 0, 0, 0, 0, 1));
        w2.Apply(ai2.Orders.Engage(Unit.Ship(1), 3));
        w2.AdvanceTo(w2.Time + 60_000);
        Assert.Equal(0, TorpedoesBy(w2, 1));
    }
}
