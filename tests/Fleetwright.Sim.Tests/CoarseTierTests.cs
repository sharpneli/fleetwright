using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The coarse clock tier in a world (docs/battle/plan.md step 2.8c): the asked speed picks it, ships cross
/// between the tiers where they are with nothing jumping (the player switches while zoomed in), a manoeuvre in it
/// ends on rails, a group's ships ride their guide once in station and steer back to it when not, coarse ships meet
/// and see each other, and the same orders and speeds replay to the same world without garbage. The motion itself is
/// CoarseMotionTests'.</summary>
public class CoarseTierTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.5, Lon = 4.0, Kn = 15;
    const byte AiId = 3;
    static readonly UnitOrders O = new(AiId);

    static (World W, int Class) Make(SimSettings? s = null)
    {
        var w = new World(Start, 11, s ?? Settings);
        Assert.Equal(AiId, new AiSystem(w).Id);
        return (w, w.Ships.AddClass(Fleet.Get("dreadnought").Class!));
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    /// <summary>Where a ship is drawn at the world's time: its pose carried on along its arc from its pose's time, as the
    /// snapshot's renderer does.</summary>
    static Vec3d Drawn(World w, int e)
    {
        var s = Ship(w, e);
        var p = s.Position;
        var f = s.Forward;
        ShipMotion.Extrapolate(ref p, ref f, s.Speed, s.YawRate, (w.Time.Ms - s.PoseMs) / 1000.0);
        return p;
    }

    [Fact]
    public void The_asked_speed_picks_the_tier_and_crossing_it_moves_nothing()
    {
        // a ship turning on the fine tick; the clock up to 1,000x mid-turn, and down again to 10x mid-turn
        var (w, c) = Make();
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, Kn));
        w.Apply(ShipSystem.Course(1, 150));
        Run(w, 20.025);
        Assert.Equal(ClockTier.Fine, Ship(w, 1).Tier);
        var before = Drawn(w, 1);
        double rate = Ship(w, 1).YawRate;
        w.Apply(Command.SetSpeed(1000));
        Assert.Equal(ClockTier.Coarse, Ship(w, 1).Tier);
        double up = Earth.Distance(before, Drawn(w, 1));
        output.WriteLine($"fine -> coarse mid-turn: moved {up * 1000:F2} mm; rate of turn {rate * 180 / Math.PI:F3} -> {Ship(w, 1).YawRate * 180 / Math.PI:F3} deg/s");
        Assert.True(up < 0.001);

        Run(w, 20);
        Assert.Equal(ClockTier.Coarse, Ship(w, 1).Tier);
        Assert.InRange(Ship(w, 1).HeadingDeg, 10, 140);   // still turning: it plays out, it doesn't snap
        before = Drawn(w, 1);
        rate = Ship(w, 1).YawRate;
        w.Apply(Command.SetSpeed(10));
        Assert.Equal(ClockTier.Fine, Ship(w, 1).Tier);
        Assert.True(Earth.Distance(before, Drawn(w, 1)) < 0.001);
        Assert.Equal(rate, Ship(w, 1).YawRate, 1e-12);   // the swing carried on
        Run(w, 2);
        output.WriteLine($"coarse -> fine mid-turn: rate {rate * 180 / Math.PI:F3} deg/s, 2 s later {Ship(w, 1).YawRate * 180 / Math.PI:F3}");
        Assert.Equal(rate, Ship(w, 1).YawRate, Math.Abs(rate) * 0.15);
    }

    [Fact]
    public void A_manoeuvre_in_coarse_plays_out_and_ends_on_rails_exactly()
    {
        var (w, c) = Make();
        w.Apply(Command.SetSpeed(1000));
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, Kn));
        Run(w, 120);
        Assert.True(Ship(w, 1).OnRails, "steady, it went on rails");
        w.Apply(ShipSystem.Course(1, 120));
        w.Apply(ShipSystem.Speed(1, 10));
        Assert.Equal(ClockTier.Coarse, Ship(w, 1).Tier);
        Run(w, 30);
        Assert.InRange(Ship(w, 1).HeadingDeg, 5, 115);
        for (int m = 0; m < 30 && !Ship(w, 1).OnRails; m++)
            Run(w, 60);
        Assert.True(Ship(w, 1).OnRails, "its manoeuvre done, on rails at the next coarse step");
        Assert.Equal(120, Ship(w, 1).HeadingDeg, 1e-6);
        Assert.Equal(10, Ship(w, 1).Speed / Handling.MsPerKnot, 1e-6);
    }

    [Fact]
    public void A_route_steamed_coarse_keeps_near_the_fine_ticks_track()
    {
        // the same route at 1x and 1,000x, side by side: the coarse turns are the steady circle at once, with no rudder to
        // move and no swing to build first (the user's: nothing to settle), so it turns inside the fine track at each
        // waypoint, the fine ship running on some 25 s at 18 kn before its swing builds, on a wider circle; both steer
        // for the next waypoint, and part again at its wheel-over: under one and a half of the coarse turning circles
        World Route(double speed)
        {
            var (w, c) = Make(Settings with { RailsEnabled = false });
            w.Apply(Command.SetSpeed(speed));
            w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 18));
            var p = Ship(w, 1).Position;
            foreach (double brg in new[] { 60.0, 150, 250, 330 })
            {
                var f = Earth.Forward(p, brg);
                Earth.Advance(ref p, ref f, 12_000);
                var (la, lo) = Earth.LatLon(p);
                w.Apply(ShipSystem.AddWaypoint(1, la, lo));
            }
            return w;
        }
        var (fine, coarse) = (Route(1), Route(1000));
        var track = new List<Vec3d>();
        double apart = 0, off = 0;
        for (int k = 0; k < 1200; k++)
        {
            Run(fine, 5);
            Run(coarse, 5);
            track.Add(Ship(fine, 1).Position);
            if (k % 12 != 11)
                continue;
            var p = Ship(coarse, 1).Position;
            apart = Math.Max(apart, Earth.Distance(Ship(fine, 1).Position, p));
            off = Math.Max(off, track.Min(q => Earth.Distance(q, p)));   // from the fine track so far, wherever along it
        }
        double heading = Math.Abs(Ship(fine, 1).HeadingDeg - Ship(coarse, 1).HeadingDeg);
        output.WriteLine($"a route of four 12 km legs at 18 kn: coarse at most {off:F0} m off the fine track and {apart:F0} m from the " +
                         $"fine ship at the same time (it keeps its speed in the turns); the last course {heading:F2} deg apart");
        Assert.Equal(Helm.Course, Ship(coarse, 1).Helm);
        double circle = 2 * Ship(coarse, 1).Handling.RadiusFor(Ship(coarse, 1).CourseRudder);
        Assert.True(off < 1.5 * circle, $"{off:F0} m off the track, the circle {circle:F0} m");
        Assert.True(heading < 2, $"{heading:F2} deg");
    }

    [Fact]
    public void A_group_out_of_station_steers_back_by_its_captains_and_then_rides_its_guide()
    {
        // a column of four spawned 400 m out of its stations to port, the clock at 1,000x: nobody jumps to a station
        var (w, c) = Make();
        w.Apply(Command.SetSpeed(1000));
        var at = Earth.Position(Lat, Lon);
        var fwd = Earth.Forward(at, 0);
        var port = -Vec3d.Cross(fwd, at.Normalized());
        for (int i = 0; i < 4; i++)
        {
            var (la, lo) = Earth.LatLon(at - fwd * (i * 600) + port * (i == 0 ? 0 : 400));
            w.Apply(ShipSystem.Spawn(c, la, lo, 0, Kn));
        }
        w.Apply(GroupSystem.CreateOfNewest(4, Formation.Column, 600));
        var last = new Vec3d[5];
        for (int e = 1; e <= 4; e++)
            last[e] = Ship(w, e).Position;
        double worst = 0;
        bool riding = false;
        for (int t = 0; t < 3600 && !riding; t += 10)
        {
            Run(w, 10);
            for (int e = 1; e <= 4; e++)
            {
                var p = Ship(w, e).Position;
                worst = Math.Max(worst, Earth.Distance(p, last[e]) / 10);
                last[e] = p;
            }
            riding = true;
            for (int e = 2; e <= 4; e++)
                riding &= w.Ships.Store.RidesAnother(w.Ships.Store.SlotOf(e)) || Ship(w, e).OnRails;
        }
        output.WriteLine($"in station and riding at {w.Time.Ms - Start.Ms} ms; the fastest any ship went {worst / Handling.MsPerKnot:F1} kn");
        Assert.True(riding, "back in station, riding its guide");
        Assert.True(worst < (Kn + Settings.FollowMaxSpeedDiffKn + 1) * Handling.MsPerKnot, "nothing jumped");
    }

    [Fact]
    public void Coarse_ships_meet_open_an_encounter_and_see_each_other()
    {
        var (w, c) = Make();
        w.Apply(Command.SetSpeed(1000));
        w.Apply(ShipSystem.Spawn(c, 56.0, Lon, 0, Kn, 1));
        w.Apply(ShipSystem.Spawn(c, 57.0, Lon, 180, Kn, 2));
        for (int m = 0; m < 240 && w.Sightings.Of(1)?.IndexOf(2) is null or < 0; m++)
            Run(w, 60);
        Assert.Equal(1, w.Encounters.Count);
        Assert.Equal(ClockTier.Coarse, Ship(w, 1).Tier);
        Assert.True(w.Sightings.Of(1)?.IndexOf(2) >= 0, "side 1 has seen her");
        output.WriteLine($"seen at {Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position):F0} m apart");
    }

    /// <summary>Two squadrons of opposing sides, ordered about with the clock changing across the coarse threshold.</summary>
    static World Exercise(long[] cuts)
    {
        var (w, c) = Make();
        long t = Start.Ms;
        void At(long ms)
        {
            t += ms;
            w.AdvanceTo(new WorldTime(t));
        }
        for (int i = 0; i < 3; i++)
            w.Apply(ShipSystem.Spawn(c, Lat, Lon + i * 0.01, 0, Kn, 1));
        w.Apply(GroupSystem.CreateOfNewest(3, Formation.LineAbreast, 600));
        w.Apply(ShipSystem.Spawn(c, Lat + 0.7, Lon, 180, Kn, 2));
        At(30_000);
        w.Apply(Command.SetSpeed(1000));
        At(600_000);
        w.Apply(O.Course(Unit.Group(w.Groups[0].GroupId), 45));
        At(90_001);
        w.Apply(Command.SetSpeed(30));
        At(45_007);
        w.Apply(Command.SetSpeed(5000));
        foreach (long cut in cuts)
            At(cut);
        w.AdvanceTo(Start + 3 * 3_600_000L);
        return w;
    }

    [Fact]
    public void The_same_orders_and_speeds_give_the_same_world_however_cut_and_on_replay()
    {
        var live = Exercise([]);
        ulong h = live.ComputeHash();
        Assert.Equal(h, Exercise([1, 59_999, 600_001, 77, 1_000_000]).ComputeHash());
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
    public void Coarse_ships_in_an_encounter_allocate_nothing()
    {
        var (w, c) = Make();
        w.Apply(Command.SetSpeed(1000));
        w.Apply(Command.SetWeather(1_000));   // nobody sees anybody: the pictures' growth is PictureTests'
        for (int i = 0; i < 20; i++)
        {
            w.Apply(ShipSystem.Spawn(c, Lat, Lon + i * 0.02, 0, Kn, 1));
            w.Apply(ShipSystem.Spawn(c, Lat + 0.3, Lon + i * 0.02, 180, Kn, 2));
        }
        // zigzagging together, so the encounter stays one and its grid's buffers keep the size they grew to (its
        // splitting and merging is EncounterTests'), the same manoeuvres run first to warm the paths up
        void Zigzag(int legs)
        {
            for (int k = 0; k < legs; k++)
            {
                for (int e = 1; e <= 40; e++)
                    w.Apply(ShipSystem.Course(e, k % 2 == 0 ? 70 : 110));
                Run(w, 300);
            }
        }
        Zigzag(4);
        Assert.Equal(1, w.Encounters.Count);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Zigzag(10);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"50 minutes of 40 coarse ships zigzagging in an encounter: {allocated} bytes allocated, {w.Ships.Store.CoarseCount} coarse");
        Assert.Equal(0, allocated);
        Assert.Equal(1, w.Encounters.Count);
    }
}
