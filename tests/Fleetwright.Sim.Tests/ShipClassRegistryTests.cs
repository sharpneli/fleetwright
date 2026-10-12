using System.Collections.Concurrent;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>The class registry (docs/battle/plan.md step 2.3): one build per design, on several threads with the same
/// result as one, no request dropped, the hook seeing every built ship, references counted, failures kept as errors;
/// and the world spawning by the registry's ids, waiting for a build, with routes in the snapshot.</summary>
public class ShipClassRegistryTests
{
    static readonly string[] Ten = ["bismarck", "destroyer", "dreadnought", "fubuki", "kgv", "light_cruiser", "mikasa", "pt_boat", "yamato", "zara"];

    static Design Load(string name) => Design.Load(Path.Combine(Fleet.Shipgen, "designs", name + ".json"));

    static (ContentHash Design, ContentHash Layout)[] BuildAll(int threads, out int hookCalls)
    {
        int calls = 0;
        using var r = new ShipClassRegistry(threads);
        r.Built = (_, _, _) => Interlocked.Increment(ref calls);
        var ids = Ten.Select(n => r.Request(Load(n))).ToArray();
        r.WaitAll();
        hookCalls = calls;
        return [.. ids.Select(id => (r.HashOf(id), r.Get(id)!.LayoutHash))];
    }

    [Fact]
    public void Four_threads_build_what_one_does_and_drop_nothing()
    {
        var one = BuildAll(1, out int calls1);
        var four = BuildAll(4, out int calls4);
        Assert.Equal(one, four);
        Assert.Equal(Ten.Length, calls1);
        Assert.Equal(Ten.Length, calls4);
    }

    [Fact]
    public void A_design_asked_for_twice_is_built_once_with_one_id()
    {
        var built = new ConcurrentBag<int>();
        using var r = new ShipClassRegistry(2);
        r.Built = (id, ship, cls) => built.Add(id);
        int a = r.Request(Load("kgv"));
        int b = r.Request(Load("kgv"));
        int c = r.Request(Load("destroyer"));
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEmpty(r.Get(a)!.Name);
        r.WaitAll();
        Assert.Equal(2, built.Count);
        Assert.Equal(r.Get(a)!.DesignHash, r.HashOf(a));
        Assert.Equal(r.Add(r.Get(a)!), a);   // a compiled class of the same design is the same entry
    }

    [Fact]
    public void References_are_counted_and_a_failed_design_is_an_error()
    {
        using var r = new ShipClassRegistry(1);
        int id = r.Request(Load("destroyer"));
        Assert.NotNull(r.Get(id));   // built: only a finished class can be unused
        r.Acquire(id);
        r.Acquire(id);
        Assert.DoesNotContain(id, r.Unused());
        r.Release(id);
        r.Release(id);
        Assert.Contains(id, r.Unused());
        Assert.Throws<InvalidOperationException>(() => r.Release(id));

        var bad = Load("destroyer");
        bad = Design.Parse(System.Text.RegularExpressions.Regex.Replace(bad.ToJson(), @"""speed_kn"":\s*36", @"""speed_kn"": -5"));
        int b = r.Request(bad);
        Assert.Null(r.Get(b));
        Assert.NotNull(r.ErrorOf(b));
    }

    [Fact]
    public void The_world_spawns_by_registry_id_and_waits_for_the_build()
    {
        using var r = new ShipClassRegistry(2);
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, SimSettings.Default, r);
        int id = r.Request(Load("light_cruiser"));
        w.Apply(ShipSystem.Spawn(id, 56.5, 4.0, 90, 20));   // waits for the build, if it's still running
        Assert.Equal(1, w.Ships.Store.Count);
        Assert.Equal(id, w.Ships.Store[0].ClassIndex);
        Assert.Equal(1, r.RefsOf(id));   // the world holds the class it made a ship of

        // routes reach the snapshot, the next waypoint first
        w.Apply(ShipSystem.AddWaypoint(1, 56.6, 4.3));
        w.Apply(ShipSystem.AddWaypoint(1, 56.8, 4.3));
        w.AdvanceTo(w.Time + 1000);
        var s = new Snapshot();
        w.Fill(s);
        Assert.Equal(1, s.Ships.Count);
        Assert.Equal(id, s.Ships.Class[0]);
        Assert.Equal(Helm.Route, s.Ships.Helm[0]);
        Assert.Equal(2, s.Ships.RouteCount[0]);
        Assert.Equal(56.6, Earth.LatLon(s.Ships.Waypoints[s.Ships.RouteStart[0]]).Lat, 5);
        Assert.Equal(0, s.Ships.IndexOf(1));
        Assert.Equal(-1, s.Ships.IndexOf(2));
    }
}
