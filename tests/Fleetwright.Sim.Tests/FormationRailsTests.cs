using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Formations on rails and the contacts that end them (docs/battle/plan.md step 2.5, "Fast turns for
/// formations on rails"): with the clock fast enough an order to a group on rails is carried out at once on rails, its
/// ships on their stations on the new heading; search patterns (and a patrol that goes round) run on rails start to
/// end; opposing ships come off rails at the moment their rails bring them within the encounter range, and nothing goes
/// on rails within it; the same orders give the same world, without garbage.</summary>
public class FormationRailsTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.5, Lon = 4.0, Kn = 15;

    static (World W, int Class) Make(SimSettings? s = null)
    {
        var w = new World(Start, 11, s ?? Settings);
        _ = new AiSystem(w);
        return (w, w.Ships.AddClass(Fleet.Get("dreadnought").Class!));
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    /// <summary>n ships on their stations about a guide at a place, already in formation (as the map's spawner puts
    /// them); the first's entity.</summary>
    static int Group(World w, int c, double lat, double lon, double heading, int n, Formation f, double spacing, byte side = 0)
    {
        int first = w.Ships.NewestEntity + 1;
        var at = Earth.Position(lat, lon);
        var fwd = Earth.Forward(at, heading);
        var starboard = Vec3d.Cross(fwd, at.Normalized());
        for (int i = 0; i < n; i++)
        {
            var (astern, abeam) = GroupSystem.OffsetFromGuide(f, spacing, i);
            var (la, lo) = Earth.LatLon(at - fwd * astern + starboard * abeam);
            w.Apply(ShipSystem.Spawn(c, la, lo, heading, Kn, side));
        }
        if (n > 1)
            w.Apply(GroupSystem.CreateOfNewest(n, f, spacing));
        return first;
    }

    static bool AllOnRails(World w, int first, int n)
    {
        for (int e = first; e < first + n; e++)
            if (!Ship(w, e).OnRails)
                return false;
        return true;
    }

    static void UntilOnRails(World w, int first, int n)
    {
        for (int m = 0; m < 120 && !AllOnRails(w, first, n); m++)
            Run(w, 60);
        Assert.True(AllOnRails(w, first, n), "the group went on rails");
    }

    /// <summary>How far each of a group's ships is from its station about the guide, at most, m.</summary>
    static double OffStation(World w, int first, int n, Formation f, double spacing)
    {
        var g = Ship(w, first);
        var gp = g.Position;
        var starboard = Vec3d.Cross(g.Forward, gp.Normalized());
        double worst = 0;
        for (int i = 1; i < n; i++)
        {
            var (astern, abeam) = GroupSystem.OffsetFromGuide(f, spacing, i);
            worst = Math.Max(worst, Earth.Distance(Ship(w, first + i).Position, gp - g.Forward * astern + starboard * abeam));
        }
        return worst;
    }

    [Fact]
    public void An_order_to_a_group_on_rails_at_speed_snaps_its_ships_onto_their_stations_and_keeps_them_on_rails()
    {
        var (w, c) = Make();
        w.Apply(Command.SetSpeed(1000));
        int g = Group(w, c, Lat, Lon, 0, 4, Formation.Column, 600);
        UntilOnRails(w, g, 4);

        w.Apply(ShipSystem.Course(g, 90));
        Assert.True(AllOnRails(w, g, 4));
        for (int e = g; e < g + 4; e++)
            Assert.True(Math.Abs(Ship(w, e).HeadingDeg - 90) < 0.05, $"{Ship(w, e).HeadingDeg}°");   // in the guide's frame: the meridians converge over the column
        Assert.True(OffStation(w, g, 4, Formation.Column, 600) < 0.01, "on station at once");

        w.Apply(ShipSystem.Speed(g, 10));
        Assert.True(AllOnRails(w, g, 4));
        for (int e = g; e < g + 4; e++)
            Assert.Equal(10, Ship(w, e).Speed / Handling.MsPerKnot, 1);

        Run(w, 6 * 3600);
        Assert.True(AllOnRails(w, g, 4), "six hours on, still on rails");
        double off = OffStation(w, g, 4, Formation.Column, 600);
        output.WriteLine($"six hours after the snap: {off:F3} m off station at most");
        Assert.True(off < 1, $"{off:F2} m");

        // off rails later (an order below the speed), the ships steam on as the rails had them
        w.Apply(Command.SetSpeed(1));
        w.Apply(ShipSystem.Speed(g, 10));
        Assert.False(Ship(w, g).OnRails);
        Run(w, 120);
        for (int e = g + 1; e < g + 4; e++)
            Assert.Equal(10, Ship(w, e).Speed / Handling.MsPerKnot, 0);
        Assert.True(OffStation(w, g, 4, Formation.Column, 600) < 30, "still in station two minutes off rails");
    }

    [Fact]
    public void Below_the_snap_speed_an_order_takes_the_group_off_rails_to_turn_properly()
    {
        var (w, c) = Make();
        w.Apply(Command.SetSpeed(Settings.RailsSnapMinSpeed * 0.99));
        int g = Group(w, c, Lat, Lon, 0, 3, Formation.LineAbreast, 800);
        UntilOnRails(w, g, 3);
        w.Apply(ShipSystem.Course(g, 90));
        for (int e = g; e < g + 3; e++)
            Assert.False(Ship(w, e).OnRails);
        Run(w, 10);
        Assert.True(Ship(w, g).HeadingDeg < 30, "turning, not turned");
    }

    [Theory]
    [InlineData(SearchPattern.Ladder)]
    [InlineData(SearchPattern.ExpandingSquare)]
    [InlineData(SearchPattern.Sector)]
    [InlineData(SearchPattern.PatrolLine)]
    public void A_group_searching_stays_on_rails_from_start_to_end(SearchPattern pattern)
    {
        var (w, c) = Make();
        w.Apply(Command.SetSpeed(10_000));
        int g = Group(w, c, Lat, Lon, 30, 4, Formation.Column, 500);
        UntilOnRails(w, g, 4);

        Span<Vec3d> wps = stackalloc Vec3d[Settings.RouteCapacity];
        int n = SearchPatterns.Waypoints(pattern, Ship(w, g).Position, 30, 30_000, 10_000, pattern == SearchPattern.Sector ? 3 : 7, true, wps);
        double along = 0;
        var from = Ship(w, g).Position;
        for (int i = 0; i < n; i++)
            (along, from) = (along + Earth.Distance(from, wps[i]), wps[i]);
        SearchPatterns.Orders(g, wps[..n], SearchPatterns.Loops(pattern), cmd => w.Apply(cmd));
        Assert.True(AllOnRails(w, g, 4), "the order kept them on rails");

        double hours = along / (Kn * Handling.MsPerKnot) / 3600;
        int samples = 0;
        for (double t = 0; t < hours * 1.2 + 0.5; t += 0.25)
        {
            Run(w, 900);
            samples++;
            Assert.True(AllOnRails(w, g, 4), $"{pattern}: off rails {t + 0.25:F2} h in");
            Assert.True(OffStation(w, g, 4, Formation.Column, 500) < 1, $"{pattern}: out of station {t + 0.25:F2} h in");
        }
        var s = Ship(w, g);
        output.WriteLine($"{pattern}: {n} waypoints, {along / 1852:F0} nm, {hours:F1} h, on rails at every one of {samples} looks");
        if (SearchPatterns.Loops(pattern))
            Assert.True(s.Helm == Helm.Route && s.RouteLoop && s.RouteCount == 2, "the patrol goes round");
        else
            Assert.Equal(Helm.Course, s.Helm);   // the search done, the course it ended on held
    }

    [Fact]
    public void A_ladder_searched_on_rails_ends_near_where_the_fine_tick_does()
    {
        Vec3d One(bool rails)
        {
            var (w, c) = Make(Settings with { RailsEnabled = rails });
            w.Apply(Command.SetSpeed(10_000));
            w.Apply(ShipSystem.Spawn(c, Lat, Lon, 30, Kn));
            Run(w, 60);
            Span<Vec3d> wps = stackalloc Vec3d[16];
            int n = SearchPatterns.Waypoints(SearchPattern.Ladder, Ship(w, 1).Position, 30, 30_000, 10_000, 5, true, wps);
            SearchPatterns.Orders(1, wps[..n], false, cmd => w.Apply(cmd));
            Run(w, 12 * 3600);
            return Ship(w, 1).Position;
        }
        double d = Earth.Distance(One(true), One(false));
        output.WriteLine($"a ladder of 5 legs, 8 turns, then 7 h on: rails end {d:F0} m from the fine tick's");
        Assert.True(d < 5000, $"{d:F0} m");   // the turns at the waypoints at once, where the helmsman cuts each corner
    }

    [Fact]
    public void A_patrol_on_the_fine_tick_goes_round_too()
    {
        var (w, c) = Make(Settings with { RailsEnabled = false });
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 90, Kn));
        var start = Ship(w, 1).Position;
        Span<Vec3d> wps = stackalloc Vec3d[2];
        int n = SearchPatterns.Waypoints(SearchPattern.PatrolLine, start, 90, 20_000, 0, 0, true, wps);
        SearchPatterns.Orders(1, wps[..n], true, cmd => w.Apply(cmd));
        double far = 0;
        for (int m = 0; m < 6 * 60; m++)
        {
            Run(w, 60);
            far = Math.Max(far, Earth.Distance(start, Ship(w, 1).Position));
        }
        output.WriteLine($"6 h of a 20 km patrol line on the fine tick: at most {far:F0} m from its start");
        Assert.True(Ship(w, 1).Helm == Helm.Route && Ship(w, 1).RouteLoop);
        Assert.True(far < 21_500 && far > 19_000, $"{far:F0} m");
    }

    /// <summary>Two ships on one meridian, steaming at each other: a of side 1 at 55°N heading north, b of side
    /// <paramref name="sideB"/> at 58°N heading south. Both go on rails; the time their rails close to the encounter
    /// range, from where they are.</summary>
    static (World W, long ContactMs) HeadOn(byte sideB)
    {
        var (w, c) = Make();
        w.Apply(ShipSystem.Spawn(c, 55, Lon, 0, Kn, 1));
        w.Apply(ShipSystem.Spawn(c, 58, Lon, 180, Kn, sideB));
        Run(w, 60);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails);
        double gap = Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position) - Settings.EncounterRangeM;
        double v = Ship(w, 1).Speed + Ship(w, 2).Speed;
        return (w, w.Time.Ms + (long)Math.Round(gap / v * 1000));
    }

    [Fact]
    public void Opposing_ships_on_rails_come_off_them_at_the_moment_they_close_to_the_encounter_range()
    {
        var (w, at) = HeadOn(2);
        w.AdvanceTo(new WorldTime(at - 1000));
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails, "a second before: both on rails");
        while (Ship(w, 1).OnRails)
            w.AdvanceTo(w.Time + 1);
        double d = Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position);
        output.WriteLine($"off rails {w.Time.Ms - at:+0;-0} ms from the closed-form time, {d - Settings.EncounterRangeM:F2} m from the range");
        Assert.False(Ship(w, 2).OnRails);
        Assert.True(Math.Abs(d - Settings.EncounterRangeM) < 1.1, $"{d:F2} m");
        Assert.True(Math.Abs(w.Time.Ms - at) <= 100, $"{w.Time.Ms - at} ms");

        // inside the range they stay on the fine tick, steady as they are, until they're past and apart again
        Run(w, 3600);
        Assert.False(Ship(w, 1).OnRails || Ship(w, 2).OnRails);
        Run(w, 3 * 3600);
        double apart = Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position);
        Assert.True(apart > Settings.EncounterRangeM + Settings.EncounterMarginM);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails, $"apart again ({apart / 1000:F0} km): back on rails");
    }

    [Fact]
    public void Ships_of_one_side_pass_on_rails()
    {
        var (w, at) = HeadOn(1);
        w.AdvanceTo(new WorldTime(at + 3_600_000));
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails);
    }

    [Fact]
    public void A_ship_on_the_fine_tick_coming_into_range_takes_an_opposing_group_off_rails()
    {
        var (w, c) = Make();
        int g = Group(w, c, Lat, Lon, 0, 4, Formation.LineAbreast, 2000, side: 1);
        UntilOnRails(w, g, 4);
        // from 90 km north of the group's middle ship, steering south by a rudder held amidships: never steady, so on
        // the fine tick throughout
        var mid = Ship(w, g + 1).Position;
        var north = Earth.Forward(mid, 0);
        Earth.Advance(ref mid, ref north, 90_000);
        var (la, lo) = Earth.LatLon(mid);
        w.Apply(ShipSystem.Spawn(c, la, lo, 180, 20, 2));
        int b = w.Ships.NewestEntity;
        w.Apply(ShipSystem.Rudder(b, 0));
        double radius = 3 * 2000;
        while (Ship(w, g).OnRails)
        {
            Assert.False(Ship(w, b).OnRails);
            Run(w, 1);
        }
        double d = Earth.Distance(Ship(w, g).Position, Ship(w, b).Position);
        output.WriteLine($"the group came off rails with the ship {d / 1000:F3} km from its guide (the range and the group's spread: {(Settings.EncounterRangeM + radius) / 1000:F0} km)");
        Assert.True(AllOnRails(w, g, 4) == false && !Ship(w, g + 3).OnRails);
        double slack = (Settings.RailsCheckMs / 1000.0 + 1) * 40 * Handling.MsPerKnot;   // looked at every few seconds, closing at 35 kn; and the stations to 50 m
        Assert.True(d <= Settings.EncounterRangeM + radius + 60 && d > Settings.EncounterRangeM + radius - 60 - slack, $"{d:F0} m");
    }

    [Fact]
    public void Nothing_goes_on_rails_within_range_of_an_opposing_ship()
    {
        var (w, c) = Make();
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, Kn, 1));
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 0);
        Earth.Advance(ref p, ref f, 40_000);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(c, la, lo, 0, Kn, 2));   // 40 km ahead, the same course and speed
        Run(w, 3600);
        Assert.False(Ship(w, 1).OnRails || Ship(w, 2).OnRails);
        w.Apply(ShipSystem.Course(2, 30));   // now opening the range
        Run(w, 6 * 3600);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails);
    }

    /// <summary>Two opposing groups searching at speed, a patrol, snapped orders and a contact between them, run and cut
    /// at given times.</summary>
    static World War(long[] cuts)
    {
        var (w, c) = Make();
        w.Apply(Command.SetSpeed(30_000));
        int a = Group(w, c, Lat, Lon, 90, 3, Formation.Column, 600, 1);
        int b = Group(w, c, Lat + 0.9, Lon + 3, 270, 3, Formation.LineAbreast, 900, 2);
        long t = Start.Ms;
        void At(long ms)
        {
            t += ms;
            w.AdvanceTo(new WorldTime(t));
        }
        At(1_800_000);
        Span<Vec3d> wps = stackalloc Vec3d[16];
        int n = SearchPatterns.Waypoints(SearchPattern.Ladder, Ship(w, a).Position, 90, 40_000, 15_000, 6, false, wps);
        SearchPatterns.Orders(a, wps[..n], false, cmd => w.Apply(cmd));
        n = SearchPatterns.Waypoints(SearchPattern.PatrolLine, Ship(w, b).Position, 270, 60_000, 0, 0, true, wps);
        SearchPatterns.Orders(b, wps[..n], true, cmd => w.Apply(cmd));
        foreach (long cut in cuts)
            At(cut);
        w.AdvanceTo(Start + 12 * 3_600_000L);
        return w;
    }

    [Fact]
    public void The_same_orders_give_the_same_world_however_the_clock_is_cut_and_on_replay()
    {
        var live = War([]);
        ulong h = live.ComputeHash();
        Assert.Equal(h, War([1, 4_999, 3_600_001, 77, 5_000_000]).ComputeHash());

        var (replay, _) = Make();
        foreach (var sc in live.CommandLog)
        {
            replay.AdvanceTo(sc.At);
            replay.Apply(sc.Command);
        }
        replay.AdvanceTo(live.Time);
        Assert.Equal(h, replay.ComputeHash());
    }

    [Fact]
    public void Contacts_and_snapped_searches_allocate_nothing()
    {
        var (w, c) = Make();
        w.Apply(Command.SetSpeed(30_000));
        int a = Group(w, c, Lat, Lon, 90, 4, Formation.Column, 600, 1);
        int b = Group(w, c, Lat + 0.85, Lon, 90, 4, Formation.Column, 600, 2);   // ~95 km north: near enough to be solved
        UntilOnRails(w, a, 4);
        UntilOnRails(w, b, 4);
        Span<Vec3d> wps = stackalloc Vec3d[4];
        foreach (int g in new[] { a, b })
        {
            int n = SearchPatterns.Waypoints(SearchPattern.PatrolLine, Ship(w, g).Position, 90, 30_000, 0, 0, true, wps);
            SearchPatterns.Orders(g, wps[..n], true, cmd => w.Apply(cmd));
        }
        var snap = new Snapshot();
        Run(w, 2 * 86_400);   // the same work once untimed (first runs of a path may allocate; play mustn't)
        w.Fill(snap);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(w, 2 * 86_400);
        w.Fill(snap);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(AllOnRails(w, a, 4) && AllOnRails(w, b, 4), "patrolling ~95 km apart: on rails throughout");
    }

    [Fact]
    public void The_patterns_have_their_shapes()
    {
        var o = Earth.Position(Lat, Lon);
        Span<Vec3d> p = stackalloc Vec3d[32];

        int n = SearchPatterns.Waypoints(SearchPattern.Ladder, o, 0, 20_000, 5_000, 4, true, p);
        Assert.Equal(7, n);   // 4 legs and the 3 steps across between them
        Assert.Equal(20_000, Earth.Distance(o, p[0]), 0);
        Assert.Equal(90, Earth.Bearing(p[0], p[1]), 1);   // to starboard of the axis
        Assert.Equal(180, Earth.Bearing(p[1], p[2]), 1);
        Assert.True(Math.Abs(Earth.Distance(o, p[6]) - 15_000) < 100);   // the fourth leg ends abeam of the start, three steps across (two of them further north, so wider)

        n = SearchPatterns.Waypoints(SearchPattern.ExpandingSquare, o, 0, 0, 2_000, 6, false, p);
        double[] legs = [2_000, 2_000, 4_000, 4_000, 6_000, 6_000];
        var from = o;
        for (int i = 0; i < n; i++)
        {
            Assert.Equal(legs[i], Earth.Distance(from, p[i]), 0);
            from = p[i];
        }
        Assert.Equal(270, Earth.Bearing(p[0], p[1]), 1);   // to port

        n = SearchPatterns.Waypoints(SearchPattern.Sector, o, 0, 10_000, 0, 3, true, p);
        Assert.Equal(9, n);
        for (int k = 0; k < 3; k++)
        {
            Assert.Equal(10_000, Earth.Distance(o, p[3 * k]), 0);
            Assert.True(Math.Abs(Earth.Distance(o, p[3 * k + 1]) - 10_000) < 20);   // equilateral, near enough on the chart
            Assert.Equal(0, Earth.Distance(o, p[3 * k + 2]), 6);   // back through the datum
            Assert.True(Angle(k * 120.0, Earth.Bearing(o, p[3 * k])) < 0.2);   // laid as rhumb lines: the great circle's first bearing differs by ~0.06°
            Assert.True(Angle(k * 120.0 + 60, Earth.Bearing(o, p[3 * k + 1])) < 0.5);   // the radials every 60°
        }
    }

    /// <summary>The angle between two bearings, degrees, [0, 180].</summary>
    static double Angle(double a, double b)
    {
        double d = b - a;
        return Math.Abs(d - 360 * Math.Round(d / 360));
    }
}
