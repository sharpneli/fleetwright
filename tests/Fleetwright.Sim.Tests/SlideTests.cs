using System.Diagnostics;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Ships and the land in the fine tier (docs/world/terrain-plan.md, step S1): the feeler slides a ship along
/// a coast at its standoff and lets it go once its order is clear; the backstop never fires for a ship steered by a
/// course or a route; spawns and waypoints on land go to sea. Every scenario runs on the real 1:10M land, with rails
/// off: they learn the land in S2. Each is also a dev command (-ships=design:1:0@lat,lon,heading,kn) to look at.</summary>
public class SlideTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { RailsEnabled = false };
    static Shore Shore => TestLand.Shore;

    static World MakeWorld() => new(Start, 5, Settings, shore: Shore);

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static double ClearanceOf(Vec3d p) => Shore.IsLand(p) ? -1 : Shore.Clearance(p, 5000, out _);

    /// <summary>What a run saw of one ship: its closest approach to the coast, how long it slid, its clearances while
    /// sliding.</summary>
    sealed class Watch
    {
        public double Closest = double.PositiveInfinity;
        public int SlidingS;
        public readonly List<double> SlidingClear = [];
    }

    /// <summary>Runs a world for a time, a second at a time, watching ships against the coast.</summary>
    static Watch[] Run(World w, double seconds, params int[] ships)
    {
        var watch = ships.Select(_ => new Watch()).ToArray();
        for (int t = 0; t < seconds; t++)
        {
            w.AdvanceTo(w.Time + 1000);
            for (int k = 0; k < ships.Length; k++)
            {
                var s = Ship(w, ships[k]);
                double c = ClearanceOf(s.Position);
                watch[k].Closest = Math.Min(watch[k].Closest, c);
                if (s.LandSide != 0)
                {
                    watch[k].SlidingS++;
                    if (!double.IsPositiveInfinity(c))
                        watch[k].SlidingClear.Add(c);
                }
            }
        }
        return watch;
    }

    static double Median(List<double> v) => v.Count == 0 ? double.NaN : v.Order().ElementAt(v.Count / 2);

    void Report(string what, World w, int e, Watch m)
    {
        var s = Ship(w, e);
        var (lat, lon) = Earth.LatLon(s.Position);
        output.WriteLine($"{what}: closest {m.Closest:F0} m (minimum {s.Handling.MinClearanceM:F0}, standoff {s.Handling.StandoffM:F0}), " +
                         $"slid {m.SlidingS} s at a median {Median(m.SlidingClear):F0} m, backstops {s.Backstops}; " +
                         $"now {lat:F3}, {lon:F3} heading {s.HeadingDeg:F0}° ordered {s.CourseOrderDeg:F0}°, side {s.LandSide}");
    }

    static void Safe(World w, int e, Watch m, int backstops = 0)
    {
        var s = Ship(w, e);
        Assert.True(m.Closest >= s.Handling.MinClearanceM, $"ship {e} came {m.Closest:F0} m off the coast, inside its minimum {s.Handling.MinClearanceM:F0}");
        Assert.True(s.Backstops <= backstops, $"the backstop put ship {e} back off the land {s.Backstops} times");
    }

    /// <summary>At 28 kn the battleship follows the coast into an inlet narrower than its turning circle and needs the
    /// backstop at its end: turning round there takes backing and filling, which the helmsman doesn't do (yet).</summary>
    [Theory]
    [InlineData(10, 90, 0)]
    [InlineData(28, 90, 2)]
    [InlineData(15, 70, 0)]
    public void A_course_at_the_Norwegian_coast_slides_along_it_at_the_standoff(double kn, double course, int backstops)
    {
        var w = MakeWorld();
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(ShipSystem.Spawn(c, 59.0, 4.6, course, kn));   // ~40 km off Karmøy
        var m = Run(w, 3.5 * 3600, 1)[0];
        Report($"{kn} kn on {course:000}°", w, 1, m);
        Safe(w, 1, m, backstops);
        var s = Ship(w, 1);
        Assert.True(m.SlidingS > 600, $"slid only {m.SlidingS} s");
        Assert.Equal(course, s.CourseOrderDeg, 6);   // the order is untouched
        Assert.InRange(Median(m.SlidingClear), s.Handling.MinClearanceM, 2.5 * s.Handling.StandoffM);
    }

    [Fact]
    public void A_course_at_Bornholm_goes_round_it_and_resumes()
    {
        var w = MakeWorld();
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(ShipSystem.Spawn(c, 55.13, 14.0, 90, 15));
        var m = Run(w, 4 * 3600, 1)[0];
        Report("Bornholm", w, 1, m);
        Safe(w, 1, m);
        var s = Ship(w, 1);
        var (_, lon) = Earth.LatLon(s.Position);
        Assert.True(m.SlidingS > 60, "never slid");
        Assert.Equal(0, s.LandSide);
        Assert.True(lon > 15.3, $"still at {lon:F2}° E");
        Assert.InRange(s.HeadingDeg, 88, 92);
    }

    [Fact]
    public void A_destroyer_keeps_closer_in_than_a_battleship()
    {
        var w = MakeWorld();
        int dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!), bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(ShipSystem.Spawn(dd, 59.0, 4.6, 80, 15));
        w.Apply(ShipSystem.Spawn(bb, 59.1, 4.6, 80, 15));
        var m = Run(w, 3 * 3600, 1, 2);
        Report("destroyer", w, 1, m[0]);
        Report("battleship", w, 2, m[1]);
        Safe(w, 1, m[0]);
        Safe(w, 2, m[1]);
        Assert.True(Ship(w, 1).Handling.StandoffM < Ship(w, 2).Handling.StandoffM);
        Assert.True(Median(m[0].SlidingClear) < Median(m[1].SlidingClear), "the destroyer kept no closer in");
    }

    [Fact]
    public void A_route_through_the_Bosporus_with_a_follower()
    {
        var w = MakeWorld();
        var ai = new AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(ShipSystem.Spawn(c, 41.32, 29.20, 225, 12));   // in the Black Sea, off the northern mouth
        w.Apply(ShipSystem.Spawn(c, 41.34, 29.23, 225, 12));
        w.Apply(ShipSystem.AddWaypoint(1, 41.23, 29.12));
        w.Apply(ShipSystem.AddWaypoint(1, 41.10, 29.06));
        w.Apply(ShipSystem.AddWaypoint(1, 40.98, 28.98));      // the Sea of Marmara
        w.Apply(ai.Orders.Follow(Unit.Ship(2), Unit.Ship(1), 500));
        var m = Run(w, 2.5 * 3600, 1, 2);
        Report("leader", w, 1, m[0]);
        Report("follower", w, 2, m[1]);
        Safe(w, 1, m[0]);
        Assert.True(m[1].Closest >= Ship(w, 2).Handling.MinClearanceM, $"the follower came {m[1].Closest:F0} m off the coast");
        Assert.True(Earth.LatLon(Ship(w, 1).Position).Lat < 41.0, "the leader didn't come through");
    }

    [Fact]
    public void A_course_up_the_Oslofjord_follows_the_coast_round()
    {
        var w = MakeWorld();
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(ShipSystem.Spawn(c, 59.05, 10.55, 0, 15));
        var m = Run(w, 6 * 3600, 1)[0];
        Report("Oslofjord", w, 1, m);
        Safe(w, 1, m);
    }

    [Fact]
    public void A_line_abreast_along_Jutland_slides_its_landward_ship()
    {
        var w = MakeWorld();
        var ai = new AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        for (int i = 0; i < 3; i++)
            w.Apply(ShipSystem.Spawn(c, 56.0, 8.04 + 0.012 * i, 8, 15));   // ~750 m apart, the last nearest the coast
        w.Apply(GroupSystem.CreateOfNewest(3, Formation.LineAbreast, 750));
        var m = Run(w, 2 * 3600, 1, 2, 3);
        for (int i = 0; i < 3; i++)
            Report($"ship {i + 1}", w, i + 1, m[i]);
        for (int i = 0; i < 3; i++)
            Assert.True(m[i].Closest >= Ship(w, i + 1).Handling.MinClearanceM, $"ship {i + 1} came {m[i].Closest:F0} m off the coast");
        GC.KeepAlive(ai);
    }

    [Fact]
    public void A_runner_with_a_coast_behind_it_slides_along_the_coast()
    {
        var w = MakeWorld();
        var ai = new AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(ShipSystem.Spawn(c, 59.0, 5.0, 270, 12));   // the runner, ~15 km off Karmøy
        w.Apply(ShipSystem.Spawn(c, 59.0, 4.7, 90, 12));    // coming at it from the sea
        w.Apply(ai.Orders.Run(Unit.Ship(1), Unit.Ship(2)));
        var m = Run(w, 1.5 * 3600, 1)[0];
        Report("runner", w, 1, m);
        Safe(w, 1, m);
        Assert.True(m.SlidingS > 60, "never reached the coast");
    }

    [Fact]
    public void Spawns_and_waypoints_on_land_go_to_sea()
    {
        var w = MakeWorld();
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(ShipSystem.Spawn(c, 59.2, 5.6, 0, 10));   // on land near Haugesund
        var s = Ship(w, 1);
        Assert.False(Shore.IsLand(s.Position));
        Assert.True(ClearanceOf(s.Position) >= s.Handling.StandoffM, $"spawned {ClearanceOf(s.Position):F0} m off the coast");
        w.Apply(ShipSystem.Spawn(c, 60, 100, 0, 10));     // deep in Siberia: no ship
        Assert.Equal(1, w.Ships.Store.Count);
        w.Apply(ShipSystem.AddWaypoint(1, 59.4, 6.0));   // inland
        var wp = Ship(w, 1).Waypoint(0);
        Assert.False(Shore.IsLand(wp));
        Assert.True(ClearanceOf(wp) >= s.Handling.StandoffM);
    }

    [Fact]
    public void Sliding_allocates_nothing_and_costs_little()
    {
        var w = MakeWorld();
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(ShipSystem.Spawn(c, 59.0, 4.6, 80, 20));
        w.Apply(ShipSystem.Spawn(c, 56.5, 4.0, 90, 20));   // the open North Sea
        while (Ship(w, 1).LandSide == 0)
            w.AdvanceTo(w.Time + 10_000);
        w.AdvanceTo(w.Time + 60_000);
        long before = GC.GetAllocatedBytesForCurrentThread();
        w.AdvanceTo(w.Time + 600_000);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        var store = w.Ships.Store;
        int coast = store.SlotOf(1), sea = store.SlotOf(2);
        var h = store[coast].Handling;
        const int N = 200_000;
        double Time(int slot)
        {
            var sw = Stopwatch.StartNew();
            double x = 0;
            for (int k = 0; k < N; k++)
                x += Slide.Course(store, slot, h, 80, Shore, Settings, out _);
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds * 1000 / N + 0 * x;
        }
        Time(sea);
        double atSea = Time(sea), along = Time(coast);
        output.WriteLine($"the feeler: {atSea:F2} µs at sea, {along:F2} µs sliding along Norway");
        Assert.True(atSea < 2, $"{atSea:F2} µs at sea");
    }

    [Fact]
    public void The_slide_is_deterministic()
    {
        ulong Once()
        {
            var w = MakeWorld();
            int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
            w.Apply(ShipSystem.Spawn(c, 59.0, 4.6, 80, 20));
            w.AdvanceTo(w.Time + 3_600_000);
            return w.ComputeHash();
        }
        Assert.Equal(Once(), Once());
    }
}
