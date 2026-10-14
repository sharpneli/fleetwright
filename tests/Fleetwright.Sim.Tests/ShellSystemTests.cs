using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Shells in flight (docs/battle/fighting.md 4.2a): a salvo lands round its aim and hits a ship in its way, the
/// splashes and hits don't depend on the tick's length (the ships under fire on rails), and a shell allocates
/// nothing.</summary>
public class ShellSystemTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    // splashes kept for the whole test (the screens' 20 s would drop the first salvos')
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { SplashLifeMs = 3_600_000 };
    const double Lat = 56.5, Lon = 4.0;

    /// <summary>A shooter and a target 8 km east of it, both stopped, and a third ship far off turning circles (so the
    /// fine tick runs all along); run until the two are on rails.</summary>
    static (World W, int Shooter, int Target) Make(SimSettings s)
    {
        var w = new World(Start, 7, s);
        _ = new AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        var east = Destination(Earth.Position(Lat, Lon), 90, 8000);
        var (tl, to) = Earth.LatLon(east);
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 0));
        w.Apply(ShipSystem.Spawn(c, tl, to, 0, 0));
        w.Apply(ShipSystem.Spawn(c, Lat + 1, Lon, 0, 12));
        w.Apply(ShipSystem.Rudder(3, 15));
        w.AdvanceTo(w.Time + 20 * 60_000);
        return (w, 1, 2);
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    /// <summary>The point a distance along a great circle from p on a bearing.</summary>
    static Vec3d Destination(Vec3d p, double bearingDeg, double metres)
    {
        double a = metres / Earth.Radius;
        return (p.Normalized() * Math.Cos(a) + Earth.Forward(p, bearingDeg) * Math.Sin(a)) * Earth.Radius;
    }

    /// <summary>Fires the main battery at a point, salvo after salvo, and runs until they've all landed: every splash,
    /// newest first.</summary>
    static List<(Vec3d Point, long Ms, int Ship)> Fire(World w, int shooter, Vec3d at, int salvos)
    {
        var (lat, lon) = Earth.LatLon(at);
        for (int k = 0; k < salvos; k++)
        {
            w.Apply(ShellSystem.FireAt(shooter, 0, lat, lon));
            w.AdvanceTo(w.Time + 30_000);
        }
        w.AdvanceTo(w.Time + 60_000);
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        var s = snap.Splashes;
        return Enumerable.Range(0, s.Count).Select(i => (s.Point[i], s.Ms[i], s.Ship[i])).ToList();
    }

    [Fact]
    public void A_salvo_lands_round_its_aim_and_hits_what_is_in_its_way()
    {
        var (w, shooter, target) = Make(Settings);
        Assert.True(Ship(w, target).OnRails);
        // at open water a kilometre short of the target: splashes round the aim, none on the ship
        var aim = Destination(Ship(w, shooter).Position, 90, 7000);
        var splashes = Fire(w, shooter, aim, 3);
        int guns = w.Ships.Class(Ship(w, shooter).ClassIndex).Batteries[0].Guns;
        Assert.Equal(3 * guns, splashes.Count);
        Assert.All(splashes, s => Assert.Equal(-1, s.Ship));
        double mean = splashes.Average(s => Earth.Distance(s.Point, aim));
        output.WriteLine($"{splashes.Count} splashes, {mean:F0} m from the aim on average (the pattern's sigma {Settings.DevFirePatternPct / 100 * 7000:F0} m)");
        Assert.InRange(mean, 10, 3 * Settings.DevFirePatternPct / 100 * 7000);

        // at the target itself, no spread: every shell meets it
        var (w2, s2, t2) = Make(Settings with { DevFirePatternPct = 0 });
        var hits = Fire(w2, s2, Ship(w2, t2).Position, 1);
        Assert.All(hits, h => Assert.Equal(t2, h.Ship));
        Assert.Equal(guns, w2.Shells.Hits);
        output.WriteLine($"at the target: {w2.Shells.Hits} hits of {guns}");
    }

    [Fact]
    public void Splashes_and_hits_do_not_depend_on_the_tick()
    {
        List<(Vec3d Point, long Ms, int Ship)> Run(int tickMs)
        {
            var (w, shooter, target) = Make(Settings with { ShipTickMs = tickMs });
            Assert.True(Ship(w, target).OnRails && Ship(w, shooter).OnRails);
            var at = Destination(Ship(w, target).Position, 270, 40);   // 40 m short: some hit, some don't
            return Fire(w, shooter, at, 4);
        }
        var a = Run(100);
        var b = Run(5000);
        output.WriteLine($"{a.Count} splashes, {a.Count(s => s.Ship >= 0)} hits");
        Assert.Equal(a.Count, b.Count);
        Assert.True(a.Count(s => s.Ship >= 0) > 0 && a.Count(s => s.Ship < 0) > 0, "some hit and some miss");
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Ship, b[i].Ship);
            Assert.Equal(a[i].Ms, b[i].Ms);
            Assert.True((a[i].Point - b[i].Point).Length < 1e-6, $"splash {i}: {(a[i].Point - b[i].Point).Length} m apart");
        }
    }

    /// <summary>A shell whose top is about the window's (a 6" casemate gun 2.7 m over the sea at about 3°, found at
    /// Tsushima by docs/battle/command.md 5.0's bench): its climb there is near nothing, so finding when it comes down
    /// through the window's top stepped back to before it was fired, and its window's event was scheduled in the past
    /// (the schedule threw). Its whole flight is in the window instead. The shell as it was fired there.</summary>
    [Fact]
    public void A_shell_topping_out_at_the_window_lands()
    {
        var (w, shooter, _) = Make(Settings);
        int table = w.Shells.Ballistics.Ensure(new BallisticKey(152, 46.413459251199995, 720, 1.8, 13));
        var muzzle = new Vec3d(-3391908.27189693, 4032822.5949942796, 3580638.9393342827);
        var forward = new Vec3d(0.8453810760196333, 0.4316345319769659, 0.31467835501176833);
        int landed = w.Shells.Hits + w.Shells.Misses;
        Assert.True(w.Shells.Launch(w, table, muzzle, forward, 2.948362581062245, 0.9944378117770383, 0, shooter, 0));
        w.AdvanceTo(w.Time + 60_000);
        Assert.Equal(landed + 1, w.Shells.Hits + w.Shells.Misses);
    }

    [Fact]
    public void A_shell_allocates_nothing()
    {
        var (w, shooter, target) = Make(Settings);
        var (lat, lon) = Earth.LatLon(Ship(w, target).Position);
        void Salvo()
        {
            w.Apply(ShellSystem.FireAt(shooter, 0, lat, lon));
            w.AdvanceTo(w.Time + 30_000);
        }
        for (int k = 0; k < 3; k++)
            Salvo();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 5; k++)
            Salvo();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(w.Shells.Hits + w.Shells.Misses >= 8 * 10);
    }
}
