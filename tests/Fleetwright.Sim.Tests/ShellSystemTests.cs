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

    /// <summary>The splash's facts for the screens (docs/visuals/splashes.md P0): the angle it fell at is the range
    /// table's at that range, the bearing the way it flew, the kind its shell's; and the dye its shooter loaded, kept by
    /// shells already flying when she's gone.</summary>
    [Theory]
    [InlineData(4000)]
    [InlineData(9000)]
    [InlineData(16000)]
    public void A_splash_knows_how_it_fell_and_whose_dye_it_carries(double range)
    {
        var (w, shooter, _) = Make(Settings with { DevFirePatternPct = 0 });
        w.Apply(ShipSystem.Dye(shooter, Ships.Dye.Orange));
        var from = Ship(w, shooter).Position;
        var aim = Destination(from, 0, range);   // north, away from the target
        var (lat, lon) = Earth.LatLon(aim);
        w.Apply(ShellSystem.FireAt(shooter, 0, lat, lon));
        w.AdvanceTo(w.Time + 1000);
        w.Apply(ShipSystem.Drop(shooter));   // gone while her shells fly
        w.AdvanceTo(w.Time + 90_000);
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        var s = snap.Splashes;
        Assert.True(s.Count > 0);
        var cls = w.Ships.Class(0);
        var table = w.Shells.Ballistics[w.Shells.TableOf(cls, 0)];
        double e = table.ElevationFor(range, 10), fall = table.Fall(e, 10).FallDeg;
        for (int i = 0; i < s.Count; i++)
        {
            output.WriteLine($"{range} m: fell at {s.FallDeg[i]:F2} deg (the table {fall:F2}), bearing {s.BearingDeg[i]:F1}, {s.Kind[i]}, {s.Dye[i]}");
            Assert.InRange(s.FallDeg[i], fall * 0.9 - 0.2, fall * 1.1 + 0.2);
            Assert.True(Math.Min(s.BearingDeg[i], 360 - s.BearingDeg[i]) < 2, $"bearing {s.BearingDeg[i]}");
            Assert.Equal(cls.ShellTypes[cls.Batteries[0].Fires].Kind, s.Kind[i]);
            Assert.Equal(Ships.Dye.Orange, s.Dye[i]);
        }
    }

    /// <summary>A mount fired, for the screens (docs/visuals/muzzle-flashes.md F0): a salvo makes one record per mount of
    /// the battery, at the moment it's fired, with the mount's barrels, the salvo, and the way and elevation the shells
    /// left; a second salvo makes its own.</summary>
    [Fact]
    public void A_salvo_records_each_mount_that_fired()
    {
        var (w, shooter, _) = Make(Settings with { DevFirePatternPct = 0 });
        var from = Ship(w, shooter).Position;
        var aim = Destination(from, 0, 9000);   // north, away from the target
        var (lat, lon) = Earth.LatLon(aim);
        long first = w.Time.Ms;
        w.Apply(ShellSystem.FireAt(shooter, 0, lat, lon));
        w.AdvanceTo(w.Time + 30_000);
        long second = w.Time.Ms;
        w.Apply(ShellSystem.FireAt(shooter, 0, lat, lon));
        w.AdvanceTo(w.Time + 1000);
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        var s = snap.Shots;

        var cls = w.Ships.Class(Ship(w, shooter).ClassIndex);
        var mounts = cls.Links(cls.Batteries[0].Mounts).ToArray();
        Assert.Equal(2 * mounts.Length, s.Count);
        var table = w.Shells.Ballistics[w.Shells.TableOf(cls, 0)];
        double e = table.ElevationFor(9000, 10);
        for (int i = 0; i < s.Count; i++)
        {
            double bearing = Earth.Heading(s.Muzzle[i], s.Forward[i]);
            output.WriteLine($"mount {s.Mount[i]} at {s.Ms[i]}: {s.Barrels[i]} barrels, salvo {s.Salvo[i]}, bearing {bearing:F1}, elevation {s.ElevationDeg[i]:F2} (the table {e:F2})");
            Assert.Equal(i < mounts.Length ? second : first, s.Ms[i]);   // newest first
            Assert.Equal(shooter, s.Shooter[i]);
            Assert.Equal(Ship(w, shooter).ClassIndex, s.Class[i]);
            Assert.Contains(s.Mount[i], mounts);
            Assert.Equal(cls.TurretTypes[cls.Mounts[s.Mount[i]].Type].Barrels, s.Barrels[i]);
            Assert.True(Math.Min(bearing, 360 - bearing) < 2, $"bearing {bearing}");
            Assert.InRange(s.ElevationDeg[i], e - 0.5, e + 0.5);
        }
        Assert.Equal(mounts.Length, Enumerable.Range(0, mounts.Length).Select(i => s.Mount[i]).Distinct().Count());
        Assert.NotEqual(s.Salvo[0], s.Salvo[s.Count - 1]);
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

    /// <summary>Every shell that meets the water splashes there: a miss, and a hit it ran on under the water to (its
    /// splash where it met the water, at the hit's moment, beside the hit's mark); in flight, the snapshot knows when it
    /// met the water and where, short of its end.</summary>
    [Fact]
    public void A_hit_under_the_water_splashes_where_it_met_it()
    {
        var (w, shooter, target) = Make(Settings with { DevFirePatternPct = 0 });
        var cls = w.Ships.Class(Ship(w, shooter).ClassIndex);
        int guns = cls.Batteries[0].Guns;
        var from = Ship(w, shooter).Position;
        // the dev's salvo doesn't allow for the earth's turning (east, it carries ~0.5% long: Eotvos); measured on a
        // salvo well short, then aimed to meet the water a few metres short of the target's side
        var far = Destination(Ship(w, target).Position, 270, 300);
        double over = Fire(w, shooter, far, 1).Average(x => Earth.Distance(x.Point, from) - Earth.Distance(far, from));
        var water = Destination(Ship(w, target).Position, 270, cls.Beam / 2 + 5);
        var at = Destination(water, 270, over);
        var (lat, lon) = Earth.LatLon(at);
        long fired = w.Time.Ms;
        w.Apply(ShellSystem.FireAt(shooter, 0, lat, lon));
        bool seen = false;
        var snap = new Snapshot();
        var met = new HashSet<long>();   // the moments shells met the water on their way to a hit
        for (int k = 0; k < 600 && (k == 0 || w.Shells.InFlight > 0); k++)
        {
            w.AdvanceTo(w.Time + 50);
            w.Fill(snap, truth: true);
            var v = snap.Shells;
            for (int i = 0; i < v.Count; i++)
                if (v.Hit[i] >= 0 && v.WaterS[i] < v.EndS[i])
                {
                    seen = true;
                    met.Add(v.FiredMs[i] + (long)Math.Floor(v.WaterS[i] * 1000));
                    Assert.True(Earth.Distance(v.Water[i], water) < 15, "met the water short of the side");
                    Assert.True((v.End[i] - v.Water[i]).Length > 1, "ran on to the hull");
                    Assert.True(v.End[i].Length < Earth.Radius, "the hull under the water");
                }
        }
        w.Fill(snap, truth: true);
        var s = snap.Splashes;
        int hits = 0, splashes = 0;
        for (int i = 0; i < s.Count; i++)
            if (s.Ms[i] > fired)
                (hits, splashes) = s.Ship[i] >= 0 ? (hits + 1, splashes) : (hits, splashes + 1);
        output.WriteLine($"{over:0.0} m long east; {guns} shells: {hits} hits, {splashes} splashes; one seen under the water in flight: {seen}");
        Assert.True(hits > 0, "some ran on under the water to the hull");
        Assert.True(seen);
        Assert.Equal(guns, splashes);   // every shell splashed, hit or not
        // each hit's splash comes when its shell met the water, ahead of the hit
        var splashMs = Enumerable.Range(0, s.Count).Where(i => s.Ms[i] > fired && s.Ship[i] < 0).Select(i => s.Ms[i]).ToList();
        var hitMs = Enumerable.Range(0, s.Count).Where(i => s.Ms[i] > fired && s.Ship[i] >= 0).Select(i => s.Ms[i]).ToList();
        output.WriteLine($"splashes at {string.Join(", ", splashMs.Order().Select(m => m - fired))} ms, hits at {string.Join(", ", hitMs.Order().Select(m => m - fired))} ms");
        Assert.All(splashMs, m => Assert.Contains(m, met));
        Assert.True(splashMs.Max() < hitMs.Max() && splashMs.Min() < hitMs.Min(), "the splashes before the hits");
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

    /// <summary>A ship going down sinks in the hit query as on the screen (<see cref="Attitude.At"/>): a salvo with no
    /// spread at a foundering ship hits it as it begins to go, and passes into the sea over it once it's under, before
    /// it's dropped.</summary>
    [Fact]
    public void A_ship_gone_under_is_not_hit()
    {
        var s = Settings with { DevFirePatternPct = 0 };
        int guns = 0;
        int HitsOn(long beforeGoneMs)
        {
            var (w, shooter, target) = Make(s);
            guns = w.Ships.Class(Ship(w, shooter).ClassIndex).Batteries[0].Guns;
            var at = Ship(w, target).Position;
            w.Apply(DamageSystem.Sink(target, LossKind.Foundered));
            w.AdvanceTo(w.Time + s.FounderMs - beforeGoneMs);
            var splashes = Fire(w, shooter, at, 1);
            return splashes.Count(x => x.Ship == target);
        }
        int early = HitsOn(s.FounderMs), late = HitsOn(30_000);
        output.WriteLine($"hits of {guns}: as it begins to go down {early}, 30 s before it's gone {late}");
        Assert.Equal(guns, early);
        Assert.Equal(0, late);
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
