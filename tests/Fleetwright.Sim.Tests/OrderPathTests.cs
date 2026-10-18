using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>One chain of command (docs/battle/plan.md step 2.6a): an order to a ship is the same whether the player
/// gives it to the ship or to its group's admiral, who hands it down; the player's order to a ship of a line takes it
/// out of the line and its admiral lets it be; an order to the group gathers back what was only nudged out, not what
/// was detached on purpose; a route given to a ship out of its line hands it back at its end; a new guide takes the
/// group's orders; limits are applied as orders are handed down; plain orders cost the AI nothing; all of it replays.
/// Every ship starts in the open North Sea.</summary>
public class OrderPathTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.5, Lon = 4.0;

    static (World W, AiSystem Ai, int Class) Make(string design = "dreadnought")
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        return (w, ai, w.Ships.AddClass(Fleet.Get(design).Class!));
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    static Detachment Detached(World w, int e) => w.Groups.Of(e)!.DetachmentOf(e);

    /// <summary>A column of n ships on their stations, steaming north at 12 kn; its group's id.</summary>
    static int Column(World w, int c, int n, double spacing = 600)
    {
        var at = Earth.Position(Lat, Lon);
        var fwd = Earth.Forward(at, 0);
        for (int i = 0; i < n; i++)
        {
            var (la, lo) = Earth.LatLon(at - fwd * (i * spacing));
            w.Apply(ShipSystem.Spawn(c, la, lo, 0, 12));
        }
        w.Apply(GroupSystem.CreateOfNewest(n, Formation.Column, spacing));
        return w.Groups[0].GroupId;
    }

    [Fact]
    public void The_same_orders_to_a_ship_or_to_its_admiral_give_the_same_helm_commands_at_the_same_times()
    {
        (List<(long, ushort, long, double, double)> Helm, ShipState Ship) One(bool throughAdmiral)
        {
            var (w, ai, c) = Make();
            var log = new HelmLog(1);
            w.Register(log);
            w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 12));
            w.Apply(ShipSystem.Spawn(c, Lat + 0.1, Lon + 0.1, 90, 10));   // another ship, to follow and to run from
            Unit u = Unit.Ship(1);
            if (throughAdmiral)
            {
                w.Apply(GroupSystem.Create(1));   // a squadron of one: the order goes to its admiral, who hands it down
                u = Unit.Group(w.Groups[0].GroupId);
            }
            var o = ai.Orders;
            Run(w, 30);
            w.Apply(o.Course(u, 120));
            Run(w, 300);
            w.Apply(o.Speed(u, 18));
            Run(w, 300);
            w.Apply(o.ClearRoute(u));
            w.Apply(o.AddWaypoint(u, Lat - 0.05, Lon + 0.3));
            w.Apply(o.AddWaypoint(u, Lat - 0.15, Lon + 0.2));
            Run(w, 1800);
            w.Apply(o.Follow(u, Unit.Ship(2), 1000));
            Run(w, 1800);
            w.Apply(o.Run(u, Unit.Ship(2)));
            Run(w, 600);
            w.Apply(o.Hold(u));
            w.Apply(o.Speed(u, 9));
            Run(w, 600);
            return (log.Commands, Ship(w, 1));
        }
        var (direct, a) = One(false);
        var (handed, b) = One(true);
        output.WriteLine($"{direct.Count} helm and engine commands either way; the ship ends {Earth.Distance(a.Position, b.Position):F6} m apart");
        Assert.True(direct.Count > 10, $"{direct.Count} commands");
        Assert.Equal(direct, handed);
        Assert.Equal(a.Position, b.Position);
        Assert.Equal(a.Forward, b.Forward);
        Assert.Equal(a.Speed, b.Speed);
    }

    [Fact]
    public void The_players_order_to_a_ship_of_the_line_takes_it_out_and_its_admiral_lets_it_be()
    {
        var (w, ai, c) = Make();
        int g = Column(w, c, 4);
        Run(w, 60);
        w.Apply(ai.Orders.Course(Unit.Ship(3), 200));
        Assert.Equal(Detachment.Nudged, Detached(w, 3));
        Assert.Equal(new[] { 1, 2, 4 }, w.Groups[0].Line);
        Run(w, 10 * 60);
        Assert.Equal(200, Ship(w, 3).HeadingDeg, 0);   // nobody steered it back
        Assert.Equal((CaptainOrder.Course, OrderSource.Player), (ai.Captains.Of(3).Order, ai.Captains.Of(3).Source));
        Assert.Equal(Unit.Ship(2).Id, ai.Captains.Of(4).Target);   // the line closed up: ship 4 follows ship 2

        // an order to the group is to all of it: the nudged ship comes back to its station
        w.Apply(ai.Orders.Course(Unit.Group(g), 30));
        Assert.Equal(Detachment.InLine, Detached(w, 3));
        Run(w, 2);
        Assert.Equal((CaptainOrder.Station, OrderSource.Superior), (ai.Captains.Of(3).Order, ai.Captains.Of(3).Source));
        Assert.Equal(Unit.Ship(2).Id, ai.Captains.Of(3).Target);

        // detached on purpose, it stays out through the group's orders, until it's handed back
        w.Apply(ai.Orders.Detach(Unit.Ship(3)));
        Assert.Equal(Detachment.Pinned, Detached(w, 3));
        w.Apply(ai.Orders.Course(Unit.Group(g), 60));
        Run(w, 2);
        Assert.Equal(Detachment.Pinned, Detached(w, 3));
        Assert.Equal(OrderSource.Player, ai.Captains.Of(3).Source);
        w.Apply(ai.Orders.Resume(Unit.Ship(3)));
        Run(w, 2);
        Assert.Equal(Detachment.InLine, Detached(w, 3));
        Assert.Equal((CaptainOrder.Station, OrderSource.Superior), (ai.Captains.Of(3).Order, ai.Captains.Of(3).Source));
    }

    [Fact]
    public void A_route_given_to_a_ship_out_of_its_line_hands_it_back_at_its_last_waypoint()
    {
        var (w, ai, c) = Make();
        Column(w, c, 3);
        Run(w, 60);
        var p = Ship(w, 3).Position;
        var east = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref east, 3000);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ai.Orders.AddWaypoint(Unit.Ship(3), la, lo));
        Assert.Equal(Detachment.Nudged, Detached(w, 3));
        int s = 0;
        for (; s < 1800 && Ship(w, 3).Helm == Helm.Route; s++)
            Run(w, 1);
        Assert.True(s < 1800, "the waypoint was reached");
        // the captain sees the route ended at his next look (steaming steady she's on rails, and he looks at their pace,
        // not every second: 22 s for the Dreadnought of 2026-10-18), hands her back, and the admiral gives her her station
        int back = 0;
        for (; back < 60 && ai.Captains.Of(3).Order != CaptainOrder.Station; back++)
            Run(w, 1);
        output.WriteLine($"the waypoint passed {s} s on; back in the line {back} s after");
        Assert.Equal(Detachment.InLine, Detached(w, 3));
        Assert.Equal((CaptainOrder.Station, OrderSource.Superior), (ai.Captains.Of(3).Order, ai.Captains.Of(3).Source));
    }

    [Fact]
    public void When_the_guide_is_taken_the_next_ship_guides_with_the_groups_orders()
    {
        var (w, ai, c) = Make();
        int g = Column(w, c, 3);
        w.Apply(ai.Orders.Course(Unit.Group(g), 90));
        w.Apply(ai.Orders.Speed(Unit.Group(g), 11));
        Run(w, 60);
        double cap = Fleet.Get("dreadnought").Class!.Motion.MaxSpeedKn - Settings.GroupSpeedMarginKn;
        Assert.Equal(cap, ai.Captains.SpeedOf(1).LimitKn);

        w.Apply(ai.Orders.Course(Unit.Ship(1), 0));   // the player takes the guide
        Assert.Equal(2, w.Groups[0].Guide);
        Run(w, 1);   // the admiral sees it at his next think, within the second
        Assert.Equal((CaptainOrder.Course, OrderSource.Superior), (ai.Captains.Of(2).Order, ai.Captains.Of(2).Source));
        Assert.Equal(90, Ship(w, 2).CourseOrderDeg, 6);
        Assert.Equal(11, Ship(w, 2).SpeedOrderKn, 6);
        Assert.Equal(0, ai.Captains.SpeedOf(1).LimitKn);   // its old limit went with its admiral
        Assert.Equal(cap, ai.Captains.SpeedOf(2).LimitKn);
        Run(w, 5 * 60);
        Assert.Equal(0, Ship(w, 1).CourseOrderDeg, 6);
        Assert.Equal(90, Ship(w, 2).CourseOrderDeg, 6);
    }

    [Fact]
    public void A_speed_past_what_the_group_makes_is_never_rung_and_what_was_asked_is_kept()
    {
        var (w, ai, c) = Make();
        int g = Column(w, c, 3);
        var log = new HelmLog(1);
        w.Register(log);
        Run(w, 5);
        double cap = Fleet.Get("dreadnought").Class!.Motion.MaxSpeedKn - Settings.GroupSpeedMarginKn;
        w.Apply(ai.Orders.Speed(Unit.Group(g), 30));
        Run(w, 600);
        var speeds = log.Commands.Where(x => (ShipCommand)x.Code == ShipCommand.Speed).Select(x => x.X).ToList();
        Assert.NotEmpty(speeds);
        Assert.All(speeds, kn => Assert.True(kn <= cap, $"{kn} kn rung"));
        Assert.Equal(30, ai.Admirals.SpeedOf(g).AskedKn);
        Assert.Equal(30, ai.Captains.SpeedOf(1).AskedKn);

        // the guide taken by the player: no limit on it now, and it rings what's asked of it
        w.Apply(ai.Orders.Speed(Unit.Ship(1), 30));
        Assert.Equal(30, Ship(w, 1).SpeedOrderKn);
    }

    [Fact]
    public void Two_thousand_ships_on_plain_orders_cost_the_AI_nothing()
    {
        var (w, ai, c) = Make("destroyer");
        for (int i = 0; i < 2000; i++)
            w.Apply(ShipSystem.Spawn(c, 50 + 0.05 * (i / 50), -20 + 0.08 * (i % 50), 90, 15));
        for (int e = 1; e <= 2000; e++)
            w.Apply(e % 2 == 0 ? ai.Orders.Course(Unit.Ship(e), 80) : ai.Orders.Speed(Unit.Ship(e), 12));
        Assert.False(ai.Captains.Active);
        Assert.False(ai.Admirals.Active);
        Run(w, 120);   // the same work once untimed (first runs of a path may allocate; play mustn't)
        var snap = new Snapshot();
        w.Fill(snap);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(w, 3600);
        w.Fill(snap);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(2000, ai.Captains.Count);
        Assert.False(ai.Captains.Active);
        Assert.Equal((byte)CaptainOrder.Course, snap.Ships.Captain[snap.Ships.IndexOf(2000)]);
    }

    [Fact]
    public void Nudges_detachments_hand_backs_and_new_guides_replay_from_the_log()
    {
        World Script()
        {
            var (w, ai, c) = Make();
            var o = ai.Orders;
            int g = Column(w, c, 5, 500);
            Run(w, 90.5);
            w.Apply(o.Course(Unit.Group(g), 45));
            Run(w, 200);
            w.Apply(o.Course(Unit.Ship(3), 160));
            w.Apply(o.Detach(Unit.Ship(4)));
            Run(w, 300);
            var p = Ship(w, 5).Position;
            var f = Earth.Forward(p, 300);
            Earth.Advance(ref p, ref f, 2500);
            var (la, lo) = Earth.LatLon(p);
            w.Apply(o.AddWaypoint(Unit.Ship(5), la, lo));
            w.Apply(o.Speed(Unit.Ship(1), 9));   // the guide taken: ship 2 guides
            Run(w, 600);
            w.Apply(o.Speed(Unit.Group(g), 14));   // gathers ships 1 and 3 (and 5, if not back already), not 4
            Run(w, 300);
            w.Apply(o.Resume(Unit.Ship(4)));
            Run(w, 400);
            return w;
        }
        var live = Script();
        Assert.Equal(Detachment.InLine, Detached(live, 4));
        Assert.Equal(1, live.Groups[0].Guide);
        var replay = new World(Start, 7, Settings);
        _ = new AiSystem(replay);
        replay.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        foreach (var sc in live.CommandLog)
        {
            replay.AdvanceTo(sc.At);
            replay.Apply(sc.Command);
        }
        replay.AdvanceTo(live.Time);
        Assert.Equal(live.ComputeHash(), replay.ComputeHash());
    }
}
