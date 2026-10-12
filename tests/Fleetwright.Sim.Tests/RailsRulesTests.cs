using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Edge cases of the rails rules found in a review (2026-10-12): the AI wakes for any ship coming off rails,
/// not only for an order; a force whose guide squadron has every ship out of its line still goes on rails; a member
/// gathered back into its line rides its guide's rails steering as they have it; a looping route with a waypoint given
/// twice goes round on rails without an event a millisecond.</summary>
public class RailsRulesTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.5, Lon = 4.0, Kn = 15;

    static (World W, AiSystem Ai, int Class) Make()
    {
        var w = new World(Start, 17, Settings);
        var ai = new AiSystem(w);
        return (w, ai, w.Ships.AddClass(Fleet.Get("dreadnought").Class!));
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    /// <summary>n ships in line ahead 600 m apart about a guide, heading a way, made a group if more than one; the
    /// first's entity.</summary>
    static int Column(World w, int c, double lat, double lon, double heading, int n, byte side = 0)
    {
        int first = w.Ships.NewestEntity + 1;
        var at = Earth.Position(lat, lon);
        var fwd = Earth.Forward(at, heading);
        for (int i = 0; i < n; i++)
        {
            var (la, lo) = Earth.LatLon(at - fwd * (i * 600));
            w.Apply(ShipSystem.Spawn(c, la, lo, heading, Kn, side));
        }
        if (n > 1)
            w.Apply(GroupSystem.CreateOfNewest(n, Formation.Column, 600));
        return first;
    }

    static bool AllOnRails(World w, int first, int n)
    {
        for (int e = first; e < first + n; e++)
            if (!Ship(w, e).OnRails)
                return false;
        return true;
    }

    [Fact]
    public void A_contact_taking_ships_off_rails_wakes_their_captains_and_admirals()
    {
        // two opposing columns on one meridian, steaming at each other on rails: their captains keep station, but
        // while every ship is on rails they think once a minute
        var (w, ai, c) = Make();
        int a = Column(w, c, 55, Lon, 0, 2, side: 1);
        int b = Column(w, c, 58, Lon, 180, 2, side: 2);
        for (int m = 0; m < 120 && !(AllOnRails(w, a, 2) && AllOnRails(w, b, 2)); m++)
            Run(w, 60);
        Assert.True(AllOnRails(w, a, 2) && AllOnRails(w, b, 2), "both columns on rails");
        while (Ship(w, a).OnRails)
            Run(w, 1);
        Assert.False(Ship(w, b).OnRails);
        long now = w.Time.Ms;
        output.WriteLine($"off rails at {now} ms: admirals think at {ai.NextThinkMs(0)}, captains at {ai.NextThinkMs(1)}");
        Assert.InRange(ai.NextThinkMs(0), now, now + Settings.AdmiralPeriodMs);
        Assert.InRange(ai.NextThinkMs(1), now, now + Settings.CaptainPeriodMs);
    }

    [Fact]
    public void A_force_whose_guide_squadron_has_every_ship_out_of_its_line_still_goes_on_rails()
    {
        // a force in line abreast: a squadron of one ship guiding, a column of two to starboard
        var (w, ai, c) = Make();
        w.Apply(Command.SetSpeed(Settings.RailsSnapMinSpeed));
        int lone = Column(w, c, Lat, Lon, 0, 1);
        w.Apply(GroupSystem.Create(lone));
        int first = w.Groups[w.Groups.Count - 1].GroupId;
        var at = Earth.Position(Lat, Lon);
        var (la, lo) = Earth.LatLon(at + Vec3d.Cross(Earth.Forward(at, 0), at.Normalized()) * 3000);
        int col = Column(w, c, la, lo, 0, 2);
        int second = w.Groups.Of(col)!.GroupId;
        w.Apply(GroupSystem.Form(first, Formation.LineAbreast, 3000));
        w.Apply(GroupSystem.Enter(w.Groups.Get(first)!.Parent, second));
        Run(w, 60);
        Assert.True(AllOnRails(w, lone, 1) && AllOnRails(w, col, 2), "the force on rails");

        // the player orders the guide squadron's one ship on its own: out of its line, so its squadron has nobody in line
        w.Apply(ai.Orders.Course(Unit.Ship(lone), 0));
        Assert.Equal(Detachment.Nudged, w.Groups.Of(lone)!.DetachmentOf(lone));
        Run(w, 60);
        output.WriteLine($"a minute on: the lone ship {(Ship(w, lone).OnRails ? "on" : "off")} rails, the column {(AllOnRails(w, col, 2) ? "on" : "off")}");
        Assert.True(AllOnRails(w, col, 2), "the rest of the force went back on rails");
    }

    [Fact]
    public void A_member_gathered_back_into_its_line_rides_its_guides_rails_steering_as_they_have_it()
    {
        var (w, ai, c) = Make();
        w.Apply(Command.SetSpeed(Settings.RailsSnapMinSpeed));
        int g = Column(w, c, Lat, Lon, 0, 3);
        int group = w.Groups.Of(g)!.GroupId;
        Run(w, 10);
        Assert.True(AllOnRails(w, g, 3));

        // the player sends the second ship off on a route of its own, then orders the group: it's gathered back
        var p = Ship(w, g + 1).Position;
        var f = Earth.Forward(p, 45);
        Earth.Advance(ref p, ref f, 40_000);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ai.Orders.AddWaypoint(Unit.Ship(g + 1), la, lo));
        Run(w, 30);
        w.Apply(ai.Orders.Course(Unit.Group(group), 0));
        for (int s = 0; s < 120 && !AllOnRails(w, g, 3); s += 5)
            Run(w, 5);
        Assert.True(AllOnRails(w, g, 3), "the group back on rails");
        output.WriteLine($"the gathered ship's helm on rails: {Ship(w, g + 1).Helm}, {Ship(w, g + 1).RouteCount} waypoints");
        Assert.Equal(Helm.Course, Ship(w, g + 1).Helm);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_looping_route_with_a_waypoint_given_twice_goes_round_on_rails(bool onlyThatOne)
    {
        // a patrol A, A, B (or A, A alone): the leg between the two A's has no length
        var (w, _, c) = Make();
        w.Apply(Command.SetSpeed(Settings.RailsSnapMinSpeed));
        int e = Column(w, c, Lat, Lon, 0, 1);
        Run(w, 10);
        Assert.True(Ship(w, e).OnRails);
        w.Apply(ShipSystem.AddWaypoint(e, Lat + 0.2, Lon));
        w.Apply(ShipSystem.AddWaypoint(e, Lat + 0.2, Lon));
        if (!onlyThatOne)
            w.Apply(ShipSystem.AddWaypoint(e, Lat + 0.4, Lon));
        w.Apply(ShipSystem.LoopRoute(e));
        w.SetTarget(w.Time + 6 * 3_600_000L);
        bool done = w.Advance(maxEvents: 100_000);
        output.WriteLine($"6 h of the patrol: {(done ? "done" : $"stuck at {w.Time.Ms} ms")}, {(Ship(w, e).OnRails ? "on" : "off")} rails");
        Assert.True(done, "six hours within 100,000 events");
        var (lat, _) = Earth.LatLon(Ship(w, e).Position);
        Assert.False(double.IsNaN(lat));
    }
}
