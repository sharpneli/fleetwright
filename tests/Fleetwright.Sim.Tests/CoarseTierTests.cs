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
    const byte AiId = 4;   // the world registers ships 1, groups 2 and shells 3; the AI comes after them
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
    public void A_snapshot_carries_a_coarse_turn_on_to_its_end_and_no_further()
    {
        // what the map draws between snapshots: a coarse ship's turn carried on along its circle to the turn's end,
        // then straight, where the world has it then (at 30,000x a snapshot may be minutes of game time old)
        var (w, c) = Make(Settings with { RailsEnabled = false });
        w.Apply(Command.SetSpeed(30_000));
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, Kn));
        Run(w, 60);
        w.Apply(ShipSystem.Course(1, 120));
        Run(w, 10);
        var snap = new Snapshot();
        w.Fill(snap);
        int k = snap.Ships.IndexOf(1);
        long end = snap.Ships.TurnEndMs[k], at = snap.Ships.PoseMs[k];
        Assert.True(end != long.MaxValue && end > at, "mid-turn, its end in the snapshot");
        foreach (long t in new[] { at + 5_000, end, end + 120_000 })
        {
            w.AdvanceTo(new WorldTime(t));
            var (p, f) = snap.Ships.PoseAt(k, t);
            double off = Earth.Distance(p, Ship(w, 1).Position), turn = Math.Abs(Earth.Heading(p, f) - Ship(w, 1).HeadingDeg);
            output.WriteLine($"{(t - at) / 1000.0:F1} s on: drawn {off:F2} m and {turn:F3} deg from the world");
            Assert.True(off < 1 && turn < 0.05, $"{off:F2} m, {turn:F3} deg");
        }
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
    public void A_column_riding_its_guide_coarse_turns_in_succession()
    {
        // a column of four in station at 1,000x (on rails once steady), turned 90 degrees: coarse, its ships ride the
        // guide's track, so each turns where the guide did, and nobody jumps
        var (w, c) = Make();
        w.Apply(Command.SetSpeed(1000));
        var at = Earth.Position(Lat, Lon);
        var fwd = Earth.Forward(at, 0);
        for (int i = 0; i < 4; i++)
        {
            var (la, lo) = Earth.LatLon(at - fwd * (i * 600));
            w.Apply(ShipSystem.Spawn(c, la, lo, 0, Kn));
        }
        w.Apply(GroupSystem.CreateOfNewest(4, Formation.Column, 600));
        int group = w.Groups[0].GroupId;
        for (int m = 0; m < 10 && !Ship(w, 4).OnRails; m++)
            Run(w, 60);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 4).OnRails, "in station: on rails");
        var turnedAt = Ship(w, 1).Position;
        w.Apply(O.Course(Unit.Group(group), 90));
        Assert.Equal(ClockTier.Coarse, Ship(w, 1).Tier);
        Assert.True(w.Ships.Store.RidesAnother(w.Ships.Store.SlotOf(4)), "riding its guide, coarse");
        var last = new Vec3d[5];
        for (int e = 1; e <= 4; e++)
            last[e] = Ship(w, e).Position;
        double fastest = 0, lastsNearest = double.MaxValue;
        bool succession = false;
        for (int t = 0; t < 900; t += 5)
        {
            Run(w, 5);
            for (int e = 1; e <= 4; e++)
            {
                var p = Ship(w, e).Position;
                fastest = Math.Max(fastest, Earth.Distance(p, last[e]) / 5);
                last[e] = p;
            }
            succession |= Math.Abs(Ship(w, 1).HeadingDeg - 90) < 1 && Ship(w, 4).HeadingDeg is < 5 or > 355;
            lastsNearest = Math.Min(lastsNearest, Earth.Distance(Ship(w, 4).Position, turnedAt));
        }
        output.WriteLine($"the last ship came within {lastsNearest:F0} m of where the guide began its turn; the fastest any ship went {fastest / Handling.MsPerKnot:F1} kn");
        Assert.True(succession, "the guide on its new course while the last is still on the old: in succession");
        Assert.True(lastsNearest < 50, "the last ship turned where the guide did");
        Assert.True(fastest < (Kn + 0.5) * Handling.MsPerKnot, "nothing jumped");
        for (int e = 1; e <= 4; e++)
            Assert.Equal(90, Ship(w, e).HeadingDeg, 1);
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

    static double ClearanceOf(Shore shore, Vec3d p) => shore.IsLand(p) ? -1 : shore.Clearance(p, 20_000, out _);

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    public void Coarse_ships_keep_off_the_land(double speed)
    {
        // destroyers every which way in the Skagerrak and the Kattegat, coarse, for six hours: every pose ten seconds apart
        var shore = TestLand.Shore;
        var w = new World(Start, 11, Settings, shore: shore);
        new AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetSpeed(speed));
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3; j++)
                w.Apply(ShipSystem.Spawn(c, 57.4 + 0.35 * j, 8.5 + 1.1 * i, (i * 70 + j * 45) % 360, 20));
        int n = w.Ships.Store.Count;
        double closest = double.PositiveInfinity;
        int inside = 0, looks = 0, backstops = 0, coarse = 0;
        for (int m = 0; m < 6 * 360; m++)
        {
            w.AdvanceTo(w.Time + 10_000);
            for (int e = 1; e <= n; e++)
            {
                var sh = Ship(w, e);
                double d = ClearanceOf(shore, sh.Position) - sh.Handling.MinClearanceM;
                closest = Math.Min(closest, d);
                inside += d < 0 ? 1 : 0;
                coarse += sh.Tier == ClockTier.Coarse ? 1 : 0;
                looks++;
            }
        }
        for (int e = 1; e <= n; e++)
            backstops += Ship(w, e).Backstops;
        output.WriteLine($"{n} destroyers, six hours at {speed:N0}x: {closest:F0} m outside their minimum at the closest, inside it at " +
                         $"{inside} of {looks} looks, {backstops} backstops; coarse at {coarse} looks");
        Assert.True(coarse > 0, "some of it coarse");
        Assert.True(closest > -Ship(w, 1).Handling.MinClearanceM, $"a ship came {-closest:F0} m inside its minimum: on land");
    }

    [Theory]
    [InlineData(59.0, 3.4, 90.0)]
    [InlineData(55.13, 13.6, 90.0)]
    [InlineData(56.3, 7.0, 80.0)]
    public void A_coarse_slide_follows_the_coast_the_fine_tick_does(double lat, double lon, double course)
    {
        // onto Norway, round Bornholm, onto Jutland: coarse turns at once onto the feeler's line, for legs, so it drifts
        // from the fine tick's track a little; it follows the same coast the same way and comes out near it
        var shore = TestLand.Shore;
        (Vec3d End, double Closest, int Backstops) Slide(double speed)
        {
            var w = new World(Start, 11, Settings with { RailsEnabled = false }, shore: shore);
            int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
            w.Apply(Command.SetSpeed(speed));
            w.Apply(ShipSystem.Spawn(c, lat, lon, course, 15));
            double closest = double.PositiveInfinity;
            for (int m = 0; m < 4 * 360; m++)
            {
                w.AdvanceTo(w.Time + 10_000);
                closest = Math.Min(closest, ClearanceOf(shore, Ship(w, 1).Position));
            }
            return (Ship(w, 1).Position, closest, Ship(w, 1).Backstops);
        }
        var fine = Slide(1);
        var coarse = Slide(1000);
        double apart = Earth.Distance(fine.End, coarse.End);
        output.WriteLine($"from {lat}, {lon} on {course}: coarse ends {apart:F0} m from fine; closest coarse {coarse.Closest:F0} m " +
                         $"({coarse.Backstops} backstops), fine {fine.Closest:F0} m ({fine.Backstops})");
        Assert.True(apart < 3000, $"{apart:F0} m apart");
        Assert.True(coarse.Closest > 0, "never on land");
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
