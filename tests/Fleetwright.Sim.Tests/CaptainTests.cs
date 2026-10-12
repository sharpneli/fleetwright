using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The first captains (docs/battle/plan.md step 2.3b): a follower comes to its station and keeps it through a
/// turn, a runner opens the range, keeping course holds it; a replay re-derives what the captains ordered from the
/// logged orders alone; captains of dropped ships go; thinking allocates nothing. Every ship starts in the open North
/// Sea.</summary>
public class CaptainTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.5, Lon = 4.0, Kn = Handling.MsPerKnot;

    static (World W, AiSystem Ai, int Class) Make(string design = "dreadnought")
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        return (w, ai, w.Ships.AddClass(Fleet.Get(design).Class!));
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)(s * 1000));

    /// <summary>How far the follower is from its station astern of the leader, and its heading and speed against his.</summary>
    static (double Off, double Dh, double Dkn) Station(World w, int follower, int leader, double d)
    {
        var f = Ship(w, follower);
        var l = Ship(w, leader);
        var station = (l.Position - l.Forward * d).Normalized() * Earth.Radius;
        double dh = f.HeadingDeg - l.HeadingDeg;
        dh -= 360 * Math.Round(dh / 360);
        return (Earth.Distance(f.Position, station), dh, (f.Speed - l.Speed) / Kn);
    }

    [Fact]
    public void A_follower_comes_to_its_station_and_keeps_it_through_a_turn()
    {
        var (w, ai, c) = Make();
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 90, 15));               // the leader, east
        w.Apply(ShipSystem.Spawn(c, Lat - 0.02, Lon - 0.03, 0, 10));  // the follower, 2.5 km to the south-west, heading north
        w.Apply(ai.Captain(2, CaptainOrder.Follow, target: 1, distanceM: 800));
        Run(w, 30 * 60);
        var (off, dh, dkn) = Station(w, 2, 1, 800);
        output.WriteLine($"after 30 min: {off:F0} m off station, heading {dh:+0.0;-0.0}°, speed {dkn:+0.00;-0.00} kn");
        Assert.True(off < 150, $"{off:F0} m off station");
        Assert.InRange(dh, -3, 3);
        Assert.InRange(dkn, -0.5, 0.5);

        w.Apply(ShipSystem.Course(1, 180));   // the leader turns south
        Run(w, 20 * 60);
        (off, dh, dkn) = Station(w, 2, 1, 800);
        output.WriteLine($"20 min after the leader's turn: {off:F0} m off station, heading {dh:+0.0;-0.0}°, speed {dkn:+0.00;-0.00} kn");
        Assert.True(off < 150, $"{off:F0} m off station");
        Assert.InRange(dh, -3, 3);
    }

    [Fact]
    public void A_runner_opens_the_range()
    {
        var (w, ai, c) = Make("light_cruiser");
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 90, 12));            // steaming east
        w.Apply(ShipSystem.Spawn(c, Lat + 0.03, Lon, 180, 12));   // 3.3 km to the north, heading at the first
        w.Apply(ai.Captain(2, CaptainOrder.Run, target: 1));
        double r0 = Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position), last = r0;
        Run(w, 4 * 60);   // turning away and working up
        for (int m = 0; m < 10; m++)
        {
            Run(w, 60);
            double r = Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position);
            Assert.True(r > last, $"minute {m + 4}: the range closed to {r:F0} m");
            last = r;
        }
        var runner = Ship(w, 2);
        output.WriteLine($"range {r0:F0} m -> {last:F0} m; the runner heads {runner.HeadingDeg:F0}° at {runner.Speed / Kn:F1} kn");
        Assert.True(last > r0 + 3000);
        var p = runner.Position;
        double away = Earth.Bearing(p, p + (p - Ship(w, 1).Position)), off = runner.HeadingDeg - away;
        off -= 360 * Math.Round(off / 360);
        Assert.True(Math.Abs(off) < 5, $"heading {runner.HeadingDeg:F0}°, straight away is {away:F0}°");
    }

    [Fact]
    public void Keeping_course_holds_what_the_ship_had()
    {
        var (w, ai, c) = Make();
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 60, 14));
        w.Apply(ai.Captain(1, CaptainOrder.KeepCourse));
        w.Apply(ShipSystem.Rudder(1, 20));   // someone puts the helm over; the captain takes it back
        Run(w, 10 * 60);
        Assert.Equal(60, Ship(w, 1).HeadingDeg, 0);
        Assert.Equal(14, Ship(w, 1).SpeedOrderKn, 1);
    }

    [Fact]
    public void A_replay_of_the_logged_orders_gives_the_same_world()
    {
        World Script()
        {
            var (w, ai, c) = Make();
            w.Apply(ShipSystem.Spawn(c, Lat, Lon, 90, 15));
            w.Apply(ShipSystem.Spawn(c, Lat - 0.02, Lon - 0.03, 0, 10));
            w.Apply(ShipSystem.Spawn(c, Lat + 0.03, Lon, 180, 12));
            w.Apply(ai.Captain(2, CaptainOrder.Follow, 1, 800));
            w.Apply(ai.Captain(3, CaptainOrder.Run, 1));
            Run(w, 437.25);
            w.Apply(ShipSystem.Course(1, 140));
            Run(w, 300);
            w.Apply(ai.Captain(3, CaptainOrder.None));
            Run(w, 200);
            return w;
        }
        var live = Script();
        Assert.True(live.CommandLog.Count == 7, $"{live.CommandLog.Count} commands logged: the captains' own orders aren't");

        var replay = new World(Start, 3, Settings);
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

    [Fact]
    public void Captains_of_ships_that_are_gone_go_and_a_lost_target_leaves_the_course_held()
    {
        var (w, ai, c) = Make();
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 90, 15));
        w.Apply(ShipSystem.Spawn(c, Lat - 0.02, Lon, 90, 15));
        w.Apply(ai.Captain(2, CaptainOrder.Follow, 1, 800));
        w.Apply(ai.Captain(1, CaptainOrder.KeepCourse));
        Run(w, 60);
        w.Apply(ShipSystem.Drop(1));
        Run(w, 60);
        Assert.Equal(1, ai.Captains.Count);   // ship 1's captain went with it
        double heading = Ship(w, 2).CourseOrderDeg;
        Run(w, 300);
        Assert.Equal(heading, Ship(w, 2).CourseOrderDeg, 3);
        w.Apply(ai.Captain(2, CaptainOrder.None));
        Run(w, 10);
        Assert.Equal(0, ai.Captains.Count);
        Assert.Equal(0, w.PendingEvents - 1);   // only the ship tick is left: an AI with nothing to do schedules nothing
    }

    [Fact]
    public void Forty_ships_and_twenty_captains_think_without_allocating()
    {
        var (w, ai, c) = Make();
        for (int i = 0; i < 40; i++)
            w.Apply(ShipSystem.Spawn(c, Lat + 0.01 * (i / 8), Lon + 0.015 * (i % 8), 90, 12));
        for (int i = 2; i <= 40; i += 2)
            w.Apply(ai.Captain(i, i % 4 == 0 ? CaptainOrder.Follow : CaptainOrder.KeepCourse, i - 1, 600));
        var snap = new Snapshot();
        Run(w, 5);
        w.Fill(snap);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(w, 600);
        w.Fill(snap);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        int k = snap.Ships.IndexOf(4);
        Assert.Equal((byte)CaptainOrder.Follow, snap.Ships.Captain[k]);
        Assert.Equal(3, snap.Ships.CaptainTarget[k]);
    }
}
