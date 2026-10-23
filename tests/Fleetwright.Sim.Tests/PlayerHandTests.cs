using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The player's hand (docs/battle/command.md "The player's hand"): an order given in play holds the unit.
/// Doctrine's standing orders (fight on meeting the enemy, deploy, fall out) leave it alone while a scenario's opening
/// orders don't; combing a torpedo's track takes a held ship for its moment and gives her order back, a route too.</summary>
public class PlayerHandTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    /// <summary>Two light cruisers of side 0 in a column steaming north, an enemy one 12 km east of them steaming north;
    /// both sides' doctrine to fight. The column's orders by hand or as a scenario's.</summary>
    static (World W, AiSystem Ai, int Group) Column(bool hand)
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Engage, (double)Engage.Fight));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Engage, (double)Engage.Fight));
        int c = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        w.Apply(ShipSystem.Spawn(c, 56.0, 4.0, 0, 15, 0));
        w.Apply(ShipSystem.Spawn(c, 55.995, 4.0, 0, 15, 0));
        w.Apply(ShipSystem.Spawn(c, 56.0, 4.195, 0, 15, 1));
        w.Apply(GroupSystem.Create(1));
        w.Apply(GroupSystem.Add(w.Groups.Of(1)!.GroupId, 2));
        int g = w.Groups.Of(1)!.GroupId;
        var o = hand ? ai.Orders.ByHand : ai.Orders;
        w.Apply(o.Course(Unit.Group(g), 0));
        w.Apply(o.Speed(Unit.Group(g), 15));
        return (w, ai, g);
    }

    /// <summary>A lone light cruiser on a course by hand keeps it when she meets the enemy; one on a scenario's course
    /// takes up the fight, as doctrine says.</summary>
    [Fact]
    public void A_ship_held_by_hand_keeps_her_course_on_meeting_the_enemy()
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Engage, (double)Engage.Fight));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Engage, (double)Engage.Fight));
        int c = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        w.Apply(ShipSystem.Spawn(c, 56.0, 4.0, 0, 12, 0));
        w.Apply(ShipSystem.Spawn(c, 56.0, 4.195, 0, 12, 1));
        w.Apply(ai.Orders.ByHand.Course(Unit.Ship(1), 20));
        w.Apply(ai.Orders.Course(Unit.Ship(2), 20));
        w.AdvanceTo(w.Time + 3 * 60_000);
        Assert.Equal(CaptainOrder.Course, ai.Captains.Of(1).Order);
        Assert.Equal(20, Ship(w, 1).CourseOrderDeg, 6);
        Assert.Equal(CaptainOrder.Fight, ai.Captains.Of(2).Order);
    }

    /// <summary>The playtest's case: a ship ordered out of her column to run from the enemy, after they've met, runs;
    /// her captain doesn't turn it into doctrine's fight, which her squadron (on a scenario's orders) takes up.</summary>
    [Fact]
    public void Ordered_out_of_her_column_to_run_she_runs()
    {
        var (w, ai, g) = Column(hand: false);
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(ai.Orders.ByHand.Run(Unit.Ship(2), Unit.Ship(3)));
        w.AdvanceTo(w.Time + 3 * 60_000);
        Assert.Equal(CaptainOrder.Run, ai.Captains.Of(2).Order);
        Assert.Equal(CaptainOrder.Fight, ai.Admirals.Of(g).Order);
        double off = Earth.Bearing(Ship(w, 2).Position, Ship(w, 3).Position) - Ship(w, 2).HeadingDeg;
        off -= 360 * Math.Round(off / 360);
        output.WriteLine($"running: the enemy {off:F0}° off her bow");
        Assert.True(Math.Abs(off) > 120, $"the enemy {off:F0}° off her bow");
    }

    /// <summary>A squadron given a course by hand keeps it on meeting the enemy; on a scenario's, it takes up the fight.</summary>
    [Fact]
    public void A_squadron_held_by_hand_keeps_its_course_on_meeting_the_enemy()
    {
        var (w, ai, g) = Column(hand: true);
        w.AdvanceTo(w.Time + 3 * 60_000);
        Assert.Equal(CaptainOrder.Course, ai.Admirals.Of(g).Order);
        Assert.Equal(0, Ship(w, 1).CourseOrderDeg, 6);

        var (w2, ai2, g2) = Column(hand: false);
        w2.AdvanceTo(w2.Time + 3 * 60_000);
        Assert.Equal(CaptainOrder.Fight, ai2.Admirals.Of(g2).Order);
    }

    /// <summary>Held, a damaged ship past doctrine's fall-out stays on the player's order; let go (back to her squadron),
    /// her captain judges her again and she falls out.</summary>
    [Fact]
    public void Held_she_does_not_fall_out_and_let_go_she_does()
    {
        var (w, ai, g) = Column(hand: false);
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.FallOutGunsShare, 1.01));   // any damage at all
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Engage, (double)Engage.CarryOn));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(3)));
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(ai.Orders.ByHand.Course(Unit.Ship(1), 330));
        w.Apply(DamageSystem.SideHole(1, HoleAt.Mid, 0, 3, 0.01));   // a scratch above the waterline: damaged, nothing lost
        w.AdvanceTo(w.Time + 30_000);
        Assert.Equal(CaptainReason.None, ai.Captains.ReasonOf(1));
        Assert.Equal(CaptainOrder.Course, ai.Captains.Of(1).Order);
        Assert.Equal(g, w.Groups.Of(1)?.GroupId);

        w.Apply(ai.Orders.ByHand.Resume(Unit.Ship(1)));
        w.AdvanceTo(w.Time + 5_000);
        Assert.Equal(CaptainReason.OutGuns, ai.Captains.ReasonOf(1));
        Assert.Equal(CaptainOrder.Home, ai.Captains.Of(1).Order);
    }

    /// <summary>A force in its cruising order given a course by hand doesn't deploy on meeting the enemy's battle line.</summary>
    [Fact]
    public void A_force_held_by_hand_does_not_deploy()
    {
        var (w, ai, force, _) = DeploymentTests.Make(deploy: true);
        w.Apply(ai.Orders.ByHand.Course(Unit.Group(force), 135));
        for (int s = 0; s < 30 * 60; s += 10)
        {
            w.AdvanceTo(w.Time + 10_000);
            Assert.False(ai.Admirals.Deploying(force), $"deploying {s / 60} min in");
        }
        Assert.Equal(Formation.LineAbreast, w.Groups.Get(force)!.Formation);
    }

    /// <summary>A battleship on a route by hand combs a spread seen coming at her, then steers by her route again (not
    /// on the comb's course, and not handed back to anyone).</summary>
    [Fact]
    public void Combing_a_spread_on_her_route_she_takes_up_the_route_again()
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TorpedoEvasion, (double)TorpedoEvasion.Comb));
        int bb = w.Ships.AddClass(Fleet.Get("super_dreadnought").Class!), dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        var p = Earth.Position(56.0, 4.0);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 3_000);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(bb, 56.0, 4.0, 0, 15, 0));
        w.Apply(ShipSystem.Spawn(dd, la, lo, 0, 15, 1));
        w.Apply(ai.Orders.ByHand.AddWaypoint(Unit.Ship(1), 56.5, 4.0));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(2)));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(1)));
        w.AdvanceTo(w.Time + 30_000);
        w.Apply(TorpedoSystem.Launch(2, 1, 4, truth: true));
        double turned = 0;
        for (int s = 0; s < 240; s += 5)
        {
            w.AdvanceTo(w.Time + 5_000);
            double d = Ship(w, 1).HeadingDeg;
            turned = Math.Max(turned, Math.Abs(d - 360 * Math.Round(d / 360)));
        }
        output.WriteLine($"she turned up to {turned:F0}° off north; torpedoes hit {w.Torpedoes.Hits}");
        Assert.True(turned > 45, $"turned {turned:F0}°");
        w.AdvanceTo(w.Time + 5 * 60_000);
        Assert.Equal(Helm.Route, Ship(w, 1).Helm);
        Assert.Equal(CaptainOrder.Route, ai.Captains.Of(1).Order);
        double d2 = Ship(w, 1).HeadingDeg;
        Assert.True(Math.Abs(d2 - 360 * Math.Round(d2 / 360)) < 10, $"heading {d2:F0}°");   // for her waypoint, due north
    }
}
