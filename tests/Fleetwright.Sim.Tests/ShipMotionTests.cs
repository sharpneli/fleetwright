using System.Reflection;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Ships on the sphere (docs/battle/plan.md step 2.2): around the globe and over a pole, turning circles
/// against the class and the research, the track against the tick, the helmsman, routes, many ships on one class,
/// determinism, ships made and dropped mid-run, and no allocation per tick. Every ship starts at sea.</summary>
public class ShipMotionTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    const double Kn = Handling.MsPerKnot;

    /// <summary>The shipped settings: what the game runs with.</summary>
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>The North Sea, between the Dogger Bank and the Skagerrak: open water all round.</summary>
    const double SeaLat = 56.5, SeaLon = 4.0;

    static (World World, int Class) WorldWith(string design, SimSettings? settings = null)
    {
        var w = new World(Start, 7, settings ?? Settings);
        return (w, w.Ships.AddClass(Fleet.Get(design).Class!));
    }

    static ShipState Ship(World w, int entity) => w.Ships.Store[w.Ships.Store.SlotOf(entity)];

    static void Run(World w, double seconds) => w.AdvanceTo(w.Time + (long)Math.Round(seconds * 1000));

    [Fact]
    public void Settings_file_names_every_key_and_is_strict()
    {
        string json = File.ReadAllText(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
        foreach (var p in typeof(SimSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.SetMethod != null)
                Assert.True(json.Contains($"\"{p.Name}\":", StringComparison.Ordinal), $"settings.json has no {p.Name}");
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => SimSettings.Parse("{ \"ShipTickMS\": 50 }"));
        Assert.Equal(40, SimSettings.Parse("{ \"ShipTickMs\": 40 }").ShipTickMs);
        Assert.Equal(SimSettings.Default.MaxSpeed, SimSettings.Parse("{ \"ShipTickMs\": 40 }").MaxSpeed);
    }

    [Fact]
    public void Steaming_round_the_globe_over_both_poles_comes_back_where_it_started()
    {
        // a meridian: the one great circle test that can't miss land (none can); a 1 s tick keeps it quick
        var (w, c) = WorldWith("bismarck", Settings with { ShipTickMs = 1000 });
        w.Apply(ShipSystem.Spawn(c, 0, -25, 0, 20));
        w.Apply(ShipSystem.Rudder(1, 0));
        var p0 = Ship(w, 1).Position;
        double lap = 2 * Math.PI * Earth.Radius / (20 * Kn), maxLat = 0, minLat = 0;
        for (int h = 0; h < 1080; h++)
        {
            Run(w, lap / 1080);
            var (lat, _) = Earth.LatLon(Ship(w, 1).Position);
            (maxLat, minLat) = (Math.Max(maxLat, lat), Math.Min(minLat, lat));
        }
        var s = Ship(w, 1);
        double miss = Earth.Distance(p0, s.Position);
        output.WriteLine($"after {lap / 3600:F0} h: {miss:F2} m from the start, speed {s.Speed / Kn:F4} kn, lat {minLat:F3}..{maxLat:F3}");
        Assert.True(maxLat > 89.9 && minLat < -89.9, "it went over both poles");
        Assert.True(miss < 20, $"{miss:F1} m from the start");
        Assert.Equal(0, s.YawRate);
        Assert.Equal(0, s.HeadingDeg, 6);
    }

    [Fact]
    public void A_course_held_over_the_pole_stays_finite()
    {
        var (w, c) = WorldWith("destroyer");
        w.Apply(ShipSystem.Spawn(c, 89.7, 10, 0, 30));   // the Arctic Ocean, steering 000 straight over the pole
        for (int k = 0; k < 120; k++)
        {
            Run(w, 10);
            var s = Ship(w, 1);
            Assert.True(double.IsFinite(s.Position.X + s.Position.Y + s.Position.Z + s.Forward.X + s.Forward.Y + s.Forward.Z + s.YawRate + s.Rudder));
            Assert.InRange(Earth.LatLon(s.Position).Lat, 88, 90);
        }
    }

    /// <summary>A hard-over turn from full speed: the transfer at 180° (the tactical diameter), the advance at 90°,
    /// the times to 90° and 180°, and the speed held in the turn.</summary>
    static (double Td, double Advance, double T90, double T180, double SpeedLoss) TurningTrial(string design, double tickMs = 50)
    {
        var (w, c) = WorldWith(design, Settings with { ShipTickMs = (int)tickMs });
        double full = w.Ships.Class(c).Motion.MaxSpeedKn;
        w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon, 0, full));
        Run(w, 2);
        var s = Ship(w, 1);
        var frame = s.Frame;
        w.Apply(ShipSystem.Rudder(1, w.Ships.Class(c).Motion.HardOverDeg));
        double turned = 0, last = 0, adv = 0, td = 0, t90 = 0, t180 = 0, t = 0;
        double u = 0;
        while (turned < 360 && t < 3600)
        {
            Run(w, 0.5);
            t += 0.5;
            double hd = s.HeadingDeg, d = hd - last;
            turned += d - 360 * Math.Round(d / 360);
            last = hd;
            var (x, y, _) = frame.ToLocal(s.Position);
            if (t90 == 0 && turned >= 90)
                (t90, adv) = (t, x);
            if (t180 == 0 && turned >= 180)
                (t180, td) = (t, y);
            u = s.Speed;
        }
        return (td, adv, t90, t180, 1 - u / (full * Kn));
    }

    [Fact]
    public void Turning_circles_match_the_class_and_land_in_the_research_bands()
    {
        // (design, the research's band for TD/L where a sourced ship is near it (manoeuvring.md §1-2))
        var cases = new (string Design, double Lo, double Hi)[]
        {
            ("yamato", 2.3, 3.4),        // Yamato 640 m at 26 kn: TD/L 2.5 on 256 m
            ("bismarck", 2.3, 4.0),      // a twin-rudder battleship
            ("colorado", 3.0, 4.5),      // US Standard, single rudder: ~640 m on 180-190 m
            ("fubuki", 5.0, 8.0),        // destroyers: twin 5-6, single 6-8
            ("destroyer", 5.0, 8.0),
            ("tb_destroyer", 5.0, 8.0),
        };
        var fails = new List<string>();
        foreach (var (design, lo, hi) in cases)
        {
            var m = Fleet.Get(design).Class!.Motion;
            var (td, adv, t90, t180, loss) = TurningTrial(design);
            output.WriteLine($"{design,-14} L {m.LengthM,5:F0} m  class TD {m.TacticalDiameterM,5:F0} m  measured {td,5:F0} m ({td / m.LengthM:F2} L)  " +
                             $"advance {adv,4:F0} m  90° {t90,4:F0} s  180° {t180,4:F0} s  speed lost {loss:P0}");
            if (Math.Abs(td / m.TacticalDiameterM - 1) > 0.12)
                fails.Add($"{design}: measured TD {td:F0} m against the class's {m.TacticalDiameterM:F0} m");
            if (td / m.LengthM < lo || td / m.LengthM > hi)
                fails.Add($"{design}: TD/L {td / m.LengthM:F2} outside {lo}-{hi}");
            if (loss < 0.15 || loss > 0.5)
                fails.Add($"{design}: speed lost in the turn {loss:P0}");
        }
        Assert.True(fails.Count == 0, string.Join("\n", fails));
    }

    [Fact]
    public void A_track_barely_depends_on_the_tick()
    {
        Vec3d Track(int tickMs)
        {
            var (w, c) = WorldWith("bismarck", Settings with { ShipTickMs = tickMs });
            w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon, 30, 28));
            Run(w, 1);
            w.Apply(ShipSystem.Course(1, 120, 35));
            w.Apply(ShipSystem.Speed(1, 12));
            Run(w, 240);
            return Ship(w, 1).Position;
        }
        var fine = Track(10);
        foreach (var (tick, tol) in new[] { (50, 2.0), (250, 10.0), (1000, 40.0) })
        {
            double d = Earth.Distance(fine, Track(tick));
            output.WriteLine($"{tick,5} ms tick: {d:F2} m from the 10 ms track after 4 min");
            Assert.True(d < tol, $"{tick} ms: {d:F1} m");
        }
    }

    [Theory]
    [InlineData("bismarck")]
    [InlineData("destroyer")]
    [InlineData("pt_boat")]
    [InlineData("dreadnought")]
    public void The_helmsman_comes_to_a_new_course_without_overshooting(string design)
    {
        var (w, c) = WorldWith(design);
        double full = w.Ships.Class(c).Motion.MaxSpeedKn;
        w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon, 10, full * 0.8));
        Run(w, 1);
        w.Apply(ShipSystem.Course(1, 100));
        double over = 0, t = 0, settled = double.NaN;
        while (t < 1200)
        {
            Run(w, 1);
            t++;
            double e = Ship(w, 1).HeadingDeg - 100;
            over = Math.Max(over, e);
            if (double.IsNaN(settled) && Math.Abs(e) < 0.5)
                settled = t;
        }
        output.WriteLine($"{design}: within 0.5° after {settled:F0} s, overshoot {over:F2}°, at the end {Ship(w, 1).HeadingDeg - 100:F3}°");
        Assert.True(over < 1.5, $"overshot by {over:F2}°");
        Assert.True(settled < 600, $"settled after {settled} s");
        Assert.Equal(100, Ship(w, 1).HeadingDeg, 1);
    }

    [Fact]
    public void Speed_up_and_slow_down_like_ships_of_their_kind()
    {
        // the research's figures, manoeuvring.md §2, as bands: they check the forces, they aren't fitted into them
        foreach (var (design, accLo, accHi, coastLo, coastHi) in new[]
                 {
                     ("bismarck", 120.0, 900.0, 90.0, 900.0),    // BB: τ 150-250 s up; ~3 min to lose a third by stopping
                     ("destroyer", 60.0, 400.0, 30.0, 400.0),    // steam DD: 0 -> 30 kn in 2-3 min
                 })
        {
            var (w, c) = WorldWith(design);
            double full = w.Ships.Class(c).Motion.MaxSpeedKn;
            w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon, 0, 0));
            w.Apply(ShipSystem.Speed(1, full));
            double t = 0;
            while (Ship(w, 1).Speed < 0.9 * full * Kn && t < 3600)
            {
                Run(w, 1);
                t++;
            }
            double acc = t;
            w.Apply(ShipSystem.Speed(1, 0));
            double coast = 0;
            while (Ship(w, 1).Speed > 0.6 * full * Kn && coast < 3600)
            {
                Run(w, 1);
                coast++;
            }
            output.WriteLine($"{design}: 0 -> 90% of {full} kn in {acc:F0} s; engines stopped, down to 60% in {coast:F0} s");
            Assert.InRange(acc, accLo, accHi);
            Assert.InRange(coast, coastLo, coastHi);
        }
    }

    [Fact]
    public void A_route_is_steamed_leg_by_leg_and_its_end_held()
    {
        var (w, c) = WorldWith("light_cruiser");
        w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon, 90, 20));
        var legs = new[] { (56.6, 4.6), (56.9, 4.7), (57.0, 4.2) };
        foreach (var (la, lo) in legs)
            w.Apply(ShipSystem.AddWaypoint(1, la, lo));
        var closest = new double[legs.Length];
        Array.Fill(closest, double.MaxValue);
        for (int k = 0; k < 4 * 3600 && Ship(w, 1).Helm == Helm.Route; k++)
        {
            Run(w, 1);
            for (int i = 0; i < legs.Length; i++)
                closest[i] = Math.Min(closest[i], Earth.Distance(Ship(w, 1).Position, Earth.Position(legs[i].Item1, legs[i].Item2)));
        }
        var s = Ship(w, 1);
        output.WriteLine($"closest to each waypoint: {string.Join(", ", closest.Select(d => $"{d:F0} m"))}; holds {s.CourseOrderDeg:F1}°");
        Assert.Equal(Helm.Course, s.Helm);
        Assert.True(closest[^1] < Settings.ArrivalRadiusM + 1, "reached the last waypoint");
        for (int i = 0; i < legs.Length - 1; i++)
            Assert.True(closest[i] < 1500, $"waypoint {i}: {closest[i]:F0} m");
        double heading = s.HeadingDeg;
        Run(w, 300);
        Assert.Equal(heading, Ship(w, 1).HeadingDeg, 0);
    }

    [Fact]
    public void Fifty_ships_share_one_class_and_none_writes_to_it()
    {
        var cls = Fleet.Get("dreadnought").Class!;
        var layout = cls.LayoutHash;
        var motion = cls.Motion;
        var w = new World(Start, 7, Settings);
        int c = w.Ships.AddClass(cls);
        Assert.Equal(c, w.Ships.AddClass(cls));
        for (int i = 0; i < 50; i++)
            w.Apply(ShipSystem.Spawn(c, SeaLat + 0.02 * (i / 10), SeaLon + 0.03 * (i % 10), 45, 15));
        for (int i = 1; i <= 50; i++)
            w.Apply(i % 2 == 0 ? ShipSystem.Course(i, 200) : ShipSystem.Rudder(i, -20));
        Run(w, 600);
        var store = w.Ships.Store;
        Assert.Equal(50, store.Count);
        for (int i = 0; i < store.Slots; i++)
            Assert.Same(w.Ships.HandlingOf(c), store[i].Handling);
        Assert.Same(cls, w.Ships.Class(c));
        Assert.Equal(layout, cls.LayoutHash);
        Assert.Equal(motion, cls.Motion);
    }

    static ulong Scripted(int[] pieces)
    {
        var (w, c) = WorldWith("kgv");
        w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon, 0, 18));
        w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon + 0.05, 0, 18));
        long t = 0;
        foreach (int ms in pieces)
        {
            t += ms;
            w.AdvanceTo(Start + t);
        }
        w.Apply(ShipSystem.Course(1, 270, 35));
        w.Apply(ShipSystem.Speed(2, 25));
        w.AdvanceTo(Start + 600_000);
        return w.ComputeHash();
    }

    [Fact]
    public void The_same_orders_give_the_same_world_however_the_clock_is_cut()
    {
        ulong once = Scripted([90_000]);
        Assert.Equal(once, Scripted([90_000]));
        Assert.Equal(once, Scripted([1, 49, 25, 37_000, 3, 52_922]));
    }

    [Fact]
    public void Ships_are_made_and_dropped_at_any_moment()
    {
        var (w, c) = WorldWith("destroyer", Settings with { Smoke = false });   // the ships' own events (the smoke's: SmokeSystemTests)
        w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon, 0, 20));
        w.AdvanceTo(Start + 1234);
        w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon + 0.1, 0, 20));   // between ticks
        int slot2 = w.Ships.Store.SlotOf(2);
        Assert.Equal(1234, w.Ships.Store[slot2].PoseMs - Start.Ms);
        w.AdvanceTo(Start + 1251);
        Assert.Equal(1250, w.Ships.Store[slot2].PoseMs - Start.Ms);   // moved on the next tick, only the 16 ms it was there
        w.Apply(ShipSystem.Drop(1));
        w.Apply(ShipSystem.Course(1, 90));   // reaches no ship
        Assert.Equal(1, w.Ships.Store.Count);
        int gen = w.Ships.Store[0].Generation;
        w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon, 0, 20));
        Assert.Equal(0, w.Ships.Store.SlotOf(3));   // the freed slot, a new entity, a later generation
        Assert.Equal(gen, w.Ships.Store[0].Generation);
        w.Apply(ShipSystem.Drop(2));
        w.Apply(ShipSystem.Drop(3));
        w.AdvanceTo(Start + 10_000);
        Assert.Equal(0, w.PendingEvents);   // no ships, no ticks
    }

    [Fact]
    public void A_tick_allocates_nothing()
    {
        var (w, c) = WorldWith("bismarck");
        for (int i = 0; i < 60; i++)
            w.Apply(ShipSystem.Spawn(c, SeaLat + 0.01 * i, SeaLon, 3 * i, 10 + i % 15));
        for (int i = 1; i <= 60; i += 3)
        {
            w.Apply(ShipSystem.AddWaypoint(i, SeaLat + 0.3, SeaLon + 0.4));
            w.Apply(ShipSystem.AddWaypoint(i, SeaLat - 0.3, SeaLon + 0.2));
        }
        var snap = new Snapshot();
        Run(w, 10);
        w.Fill(snap);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(w, 600);
        w.Fill(snap);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(60, snap.Ships.Count);
    }

    [Fact]
    public void The_snapshot_carries_poses_on_along_their_arc()
    {
        var (w, c) = WorldWith("light_cruiser");
        w.Apply(ShipSystem.Spawn(c, SeaLat, SeaLon, 0, 25));
        w.Apply(ShipSystem.Rudder(1, 20));
        Run(w, 30);
        var snap = new Snapshot();
        w.Fill(snap);
        var (p, _) = snap.Ships.PoseAt(0, snap.Ships.PoseMs[0] + Settings.ShipTickMs);
        Run(w, Settings.ShipTickMs / 1000.0);
        double d = Earth.Distance(p, Ship(w, 1).Position);
        output.WriteLine($"one tick on, the extrapolation is {d * 1000:F2} mm off");
        Assert.True(d < 0.01, $"{d:F4} m");
    }
}
