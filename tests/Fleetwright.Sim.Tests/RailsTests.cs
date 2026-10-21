using System.Diagnostics;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The rails clock tier (docs/battle/plan.md step 2.5): ships and groups steaming steady go on rails and the fine
/// tick stops; an order brings them back exactly where they are; rails agree with the fine tick (rhumb lines for a course
/// held, great circles for a route's legs, the turns at waypoints instantaneous); groups keep their distances exactly;
/// 2,000 ships steam a year in seconds, in the Pacific with the land in (docs/world/terrain-plan.md S2: where there's
/// room; the tests that must cross land run in a world of water); runs repeat bit for bit.</summary>
public class RailsTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.5, Lon = 4.0;

    static (World W, int Class) Make(SimSettings? s = null, string design = "dreadnought", bool land = false)
    {
        var w = new World(Start, 9, s ?? Settings, shore: land ? TestLand.Shore : null);
        _ = new AiSystem(w);
        return (w, w.Ships.AddClass(Fleet.Get(design).Class!));
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    [Fact]
    public void A_steady_ship_goes_on_rails_the_fine_tick_stops_and_an_order_brings_it_back_where_it_is()
    {
        var (w, c) = Make(Settings with { Smoke = false });   // the ships' own events (the smoke's idle tick: SmokeSystemTests)
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 30, 0));
        w.Apply(ShipSystem.Speed(1, 15));
        Run(w, 60);
        Assert.False(Ship(w, 1).OnRails);   // still working up
        Run(w, 15 * 60);
        Assert.True(Ship(w, 1).OnRails);
        Assert.Equal(1, w.PendingEvents);   // no fine tick: only its course reaching the polar cap, days away

        Run(w, 3 * 3600 + 0.123);
        var before = Ship(w, 1).Position;
        double kn = Ship(w, 1).Speed / Handling.MsPerKnot;
        w.Apply(ShipSystem.Speed(1, 15));   // any order: off rails first
        Assert.False(Ship(w, 1).OnRails);
        Assert.Equal(0, Earth.Distance(before, Ship(w, 1).Position), 6);
        Assert.Equal(kn, Ship(w, 1).Speed / Handling.MsPerKnot, 9);
        Run(w, 10);
        Assert.Equal(30, Ship(w, 1).HeadingDeg, 1);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(18)]
    public void A_ship_placed_slow_and_steered_makes_its_speed_and_goes_on_rails(double kn)
    {
        // as the map does it: placed at a speed, then a right-click (the route cleared, a course). Below the bollard
        // crossover (~11.7 kn) the power for a speed once came from the hull's curve alone, the thrust cap kept the
        // ship short of it, it never counted as steady and the fine tick ran for ever (42k x at most, the user)
        var (w, c) = Make();
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, kn));
        Run(w, 7.5);
        w.Apply(ShipSystem.ClearRoute(1));
        w.Apply(ShipSystem.Course(1, 237.3));
        Run(w, 60 * 60);   // slow ships turn and settle slowly: at 5 kn the turn alone takes ~8 minutes
        Assert.True(Ship(w, 1).OnRails, $"{kn} kn: still on the fine tick at {Ship(w, 1).Speed / Handling.MsPerKnot:F2} kn");
        Assert.Equal(kn, Ship(w, 1).Speed / Handling.MsPerKnot, 2);
    }

    /// <summary>The same orders with rails and without: where the ship ends up.</summary>
    static (Vec3d Rails, Vec3d Fine) Both(Action<World, int> orders, double seconds)
    {
        Vec3d One(bool rails)
        {
            var (w, c) = Make(Settings with { RailsEnabled = rails });
            w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 15));
            orders(w, 1);
            Run(w, seconds);
            return Ship(w, 1).Position;
        }
        return (One(true), One(false));
    }

    [Fact]
    public void A_course_held_on_rails_is_the_rhumb_line_the_helmsman_steers()
    {
        var (rails, fine) = Both((w, e) => w.Apply(ShipSystem.Course(e, 70)), 8 * 3600);
        double d = Earth.Distance(rails, fine);
        output.WriteLine($"8 h at 070°: rails end {d:F1} m from the fine tick's");
        Assert.True(d < 30, $"{d:F0} m");
    }

    [Fact]
    public void A_course_into_a_polar_cap_crosses_it_straight_on_rails_as_on_the_fine_tick()
    {
        // from the Arctic Ocean north of Svalbard, holding 020°: a compass course spirals into the pole; inside the cap
        // the helmsman steers straight on, and leaves holding the heading he leaves with
        Vec3d One(bool rails, out double lat, out double heading, out bool onRails)
        {
            var (w, c) = Make(Settings with { RailsEnabled = rails });
            w.Apply(ShipSystem.Spawn(c, 86.5, 15, 20, 15));
            Run(w, 24 * 3600);
            var s = Ship(w, 1);
            (lat, heading, onRails) = (Earth.LatLon(s.Position).Lat, s.HeadingDeg, s.OnRails);
            return s.Position;
        }
        var r = One(true, out double lat, out double hdg, out bool on);
        var f = One(false, out double latF, out double hdgF, out _);
        output.WriteLine($"a day on: rails at {lat:F2}°N heading {hdg:F1}°, fine at {latF:F2}°N heading {hdgF:F1}°, {Earth.Distance(r, f):F0} m apart");
        Assert.True(on);
        Assert.True(hdg > 90 && hdg < 270 && lat < 88.9, $"over the pole and out of the cap, heading south: {lat:F2}°N, {hdg:F0}°");
        Assert.True(Earth.Distance(r, f) < 2000, $"{Earth.Distance(r, f):F0} m");
    }

    [Fact]
    public void A_route_on_rails_off_and_on_again_ends_where_the_fine_tick_does_within_its_turns()
    {
        double far = 0;
        var (rails, fine) = Both((w, e) =>
        {
            w.Apply(ShipSystem.AddWaypoint(e, 56.9, 4.0));
            w.Apply(ShipSystem.AddWaypoint(e, 57.0, 5.0));
            w.Apply(ShipSystem.AddWaypoint(e, 56.5, 5.4));
            w.Apply(ShipSystem.AddWaypoint(e, 56.2, 4.6));
            Run(w, 2 * 3600 + 17);
            w.Apply(ShipSystem.Speed(e, 15));   // an order on the way: rails -> fine -> rails
            far = Math.Max(far, Earth.Distance(Ship(w, e).Position, Earth.Position(Lat, Lon)));
        }, 8 * 3600);
        double d = Earth.Distance(rails, fine);
        output.WriteLine($"4 legs, 3 turns, a stop off rails: rails end {d:F0} m from the fine tick's");
        Assert.True(d < 1500, $"{d:F0} m");   // each turn on rails is at the waypoint, at once; the helmsman's starts early and takes minutes
    }

    [Fact]
    public void A_group_goes_on_rails_whole_and_keeps_its_distances()
    {
        var (w, c) = Make();
        for (int i = 0; i < 4; i++)
            w.Apply(ShipSystem.Spawn(c, Lat - 0.004 * i, Lon, 0, 15));
        w.Apply(GroupSystem.Create(1, Formation.Column, 450));
        for (int e = 2; e <= 4; e++)
            w.Apply(GroupSystem.Join(e, 1));
        w.Apply(ShipSystem.Course(1, 80));
        for (int m = 0; m < 120 && !Ship(w, 4).OnRails; m++)
            Run(w, 60);
        Assert.All(new[] { 1, 2, 3, 4 }, e => Assert.True(Ship(w, e).OnRails, $"ship {e} on rails"));
        double[] Gaps() => [.. Enumerable.Range(1, 3).Select(e => Earth.Distance(Ship(w, e).Position, Ship(w, e + 1).Position))];
        var g0 = Gaps();
        Run(w, 24 * 3600);   // a day on rails, the North Sea crossed and on
        var g1 = Gaps();
        output.WriteLine($"gaps on railing {string.Join(", ", g0.Select(g => $"{g:F1}"))} m; a day later {string.Join(", ", g1.Select(g => $"{g:F1}"))} m");
        for (int i = 0; i < 3; i++)
            Assert.Equal(g0[i], g1[i], 0);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 4).OnRails);

        w.Apply(ShipSystem.Course(1, 150));   // an order to the guide brings the whole group off rails
        Assert.All(new[] { 1, 2, 3, 4 }, e => Assert.False(Ship(w, e).OnRails));
    }

    /// <summary>n ships patrolling back and forth along their headings, a leg a day, for <paramref name="days"/>: with
    /// the land, in the Pacific, 35°S to 35°N and 170°W to 140°W, a day's leg clear of the continents (its islands slide
    /// them); in a world of water, round the globe from 40°S to 40°N.</summary>
    static World Patrol(int n, int days, int[]? cuts = null, int sides = 1, bool land = true, double speed = 1)
    {
        var s = Settings with { RouteCapacity = days + 2, ShipCapacity = n };
        var (w, c) = Make(s, "destroyer", land);
        if (speed != 1)
            w.Apply(Command.SetSpeed(speed));
        for (int i = 0; i < n; i++)
        {
            // in a world of water, round the globe from 170°W, as 2.5 laid them
            double lat = land ? -35 + 70.0 * (i / 50) / Math.Max(1, (n - 1) / 50) : -40 + 80.0 * (i / 50) / Math.Max(1, (n - 1) / 50);
            double lon = land ? -170 + 30.0 * (i % 50) / 49 : -170 + 6.8 * (i % 50), hdg = (i * 37) % 360;
            w.Apply(ShipSystem.Spawn(c, lat, lon, hdg, 15, (byte)(i % Math.Max(sides, 1))));
            var a = Earth.Position(lat, lon);
            var f = Earth.Forward(a, hdg);
            var b = a;
            Earth.Advance(ref b, ref f, 15 * Handling.MsPerKnot * 86_400);
            var (bl, bo) = Earth.LatLon(b);
            for (int d = 0; d < days; d++)
                w.Apply(ShipSystem.AddWaypoint(i + 1, d % 2 == 0 ? bl : lat, d % 2 == 0 ? bo : lon));
        }
        long t = 0;
        foreach (int cut in cuts ?? [])
        {
            t += cut;
            w.AdvanceTo(Start + t);
        }
        w.AdvanceTo(Start + (long)days * 86_400_000);
        return w;
    }

    /// <summary>Among the Pacific islands at 1M x: ships going round an island slide on rails (terrain plan S3). Below
    /// the snap speed they'd go round on the fine tick, hours of it at Hawaii, and a year would take far too long.</summary>
    [Fact]
    public void Two_thousand_ships_steam_a_year_in_seconds()
    {
        _ = Fleet.Get("destroyer");   // the golden cases build once for the run: outside the timing
        var sw = Stopwatch.StartNew();
        var w = Patrol(2000, 365, speed: 1_000_000);
        sw.Stop();
        var store = w.Ships.Store;
        int onRails = 0;
        for (int i = 0; i < store.Slots; i++)
            onRails += store.OnRails(i) ? 1 : 0;
        output.WriteLine($"2,000 ships, 365 legs each: a year in {sw.Elapsed.TotalSeconds:F1} s, {onRails} on rails at the end");
        Assert.Equal(2000, onRails);
        Assert.True(sw.Elapsed.TotalSeconds < 60, $"{sw.Elapsed.TotalSeconds:F1} s");
        Assert.Equal(Start + 365L * 86_400_000, w.Time);
    }

    /// <summary>At the fine tier's top speed (below the coarse threshold, so off rails round the islands on the fine
    /// tick, S2), the clock keeps that speed with room to spare. A day: about 3 of the 2,000 are off rails at a time, ~55
    /// ship-hours of fine tick in all (ships going round Hawaii and the Tuamotus, up to ~11 h each), and the rest are on
    /// rails, so the cost is the fine tick's per sliding ship, not the fleet's size. Release, 2026-10-14: ~8 s, ~10,000x;
    /// Debug ~7 times slower. The faster tiers among the same islands are the year's (rails, 1M x) and
    /// CoarseTierTests'.</summary>
    [Fact]
    public void Two_thousand_ships_steam_a_day_among_the_Pacific_islands_at_the_fine_tiers_top_speed()
    {
        _ = Fleet.Get("destroyer");
        double top = Settings.CoarseMinSpeed - 1;
        var sw = Stopwatch.StartNew();
        var w = Patrol(2000, 1, speed: top);
        sw.Stop();
        var store = w.Ships.Store;
        int onRails = 0, backstops = 0;
        double closest = double.PositiveInfinity;
        for (int i = 0; i < store.Slots; i++)
            if (store.IsAlive(i))
            {
                var st = store[i];
                (onRails, backstops) = (onRails + (st.OnRails ? 1 : 0), backstops + st.Backstops);
                if (!st.OnRails)
                    closest = Math.Min(closest, TestLand.Shore.IsLand(st.Position) ? -1 : TestLand.Shore.Clearance(st.Position, 1000, out _));
            }
        double kept = 86_400 / sw.Elapsed.TotalSeconds;
        output.WriteLine($"2,000 ships, a day among the islands at {top:#,0}x: {sw.Elapsed.TotalSeconds:F1} s, keeps {kept:#,0}x, " +
                         $"{onRails} on rails at the end, {backstops} backstops");
        Assert.Equal(2000, store.Count);
        Assert.True(closest > 0, "a ship ended on land");
        Assert.True(kept > 2 * top, $"keeps {kept:#,0}x, asked {top:#,0}x");
    }

    [Fact]
    public void Ships_of_two_sides_steam_with_their_contacts_solved()
    {
        // 2,000 ships for 2 days (Release, 2026-10-12): 41 s, of which 34 s the fine tick of the ~100 ships near an
        // enemy at a time (362 contacts); kept small here so the suite stays quick. Round the globe in a world of water:
        // packed into the Pacific box (2026-10-13), its columns 68 km apart, the two sides were always in contact
        const int ships = 600, days = 2, sides = 2;
        // every other ship on the other side: rails are solved pair by pair as they're anchored and every scan, and the
        // pairs that meet come off rails to the fine tick while they're in range
        _ = Fleet.Get("destroyer");   // the golden cases build once for the run: outside the timing
        var sw = Stopwatch.StartNew();
        var w = Patrol(ships, days, sides: sides, land: false);
        sw.Stop();
        var store = w.Ships.Store;
        int onRails = 0;
        for (int i = 0; i < store.Slots; i++)
            onRails += store.OnRails(i) ? 1 : 0;
        output.WriteLine($"{ships} ships of two sides, {days} legs each: {sw.Elapsed.TotalSeconds:F2} s, {onRails} on rails at the end");
        Assert.True(sw.Elapsed.TotalSeconds < 60, $"{sw.Elapsed.TotalSeconds:F1} s");
        Assert.Equal(Start + days * 86_400_000L, w.Time);
    }

    [Fact]
    public void The_same_run_twice_is_the_same_bit_for_bit_however_the_clock_is_cut()
    {
        ulong a = Patrol(60, 20).ComputeHash();
        Assert.Equal(a, Patrol(60, 20).ComputeHash());
        Assert.Equal(a, Patrol(60, 20, [1, 4_999, 5_001, 86_400_000, 3, 7_777_777]).ComputeHash());
    }
}
