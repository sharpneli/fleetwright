using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Forces: groups of groups (docs/battle/plan.md step 2.6b). An order to a ship is the same given two levels
/// up; a force of squadrons keeps its stations through a turn, on the fine tick and snapped on rails whole; squadrons in
/// line ahead make one line; a squadron in a force is nudged, gathered, pinned and handed back as a ship in a squadron
/// is, with one notice when its superior's orders change; limits and the clearance from the coast come down the tree;
/// it replays and allocates nothing. Every ship starts in the open North Sea.</summary>
public class ForceTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    // clear air: these test their own rules; the smoke field's part in sight is SmokeSightTests', its allocation SmokeSystemTests'
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };
    const double Lat = 56.5, Lon = 4.0;
    readonly List<(int Ship, double AsternM, double AbeamM)> offsets = [];

    static (World W, AiSystem Ai) Make()
    {
        var w = new World(Start, 13, Settings);
        return (w, new AiSystem(w));
    }

    static int Class(World w, string design) => w.Ships.AddClass(Fleet.Get(design).Class!);

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    /// <summary>A force of squadrons (one class each, n ships in line ahead 600 m apart) spawned on their stations,
    /// heading north at 12 kn; the force's id. Line abreast puts each squadron's column a spacing to starboard; line
    /// ahead puts each squadron astern of the last ship of the one before.</summary>
    static int Force(World w, int[] classes, int n, Formation f, double spacing)
    {
        var at = Earth.Position(Lat, Lon);
        var fwd = Earth.Forward(at, 0);
        var starboard = Vec3d.Cross(fwd, at.Normalized());
        var squadrons = new List<int>();
        for (int k = 0; k < classes.Length; k++)
        {
            double astern0 = f == Formation.Column ? k * ((n - 1) * 600 + spacing) : 0, abeam0 = f == Formation.Column ? 0 : k * spacing;
            for (int i = 0; i < n; i++)
            {
                var (la, lo) = Earth.LatLon(at - fwd * (astern0 + i * 600) + starboard * abeam0);
                w.Apply(ShipSystem.Spawn(classes[k], la, lo, 0, 12));
            }
            w.Apply(GroupSystem.CreateOfNewest(n, Formation.Column, 600));
            squadrons.Add(w.Groups[w.Groups.Count - 1].GroupId);
        }
        w.Apply(GroupSystem.Form(squadrons[0], f, spacing));
        int force = w.Groups.Get(squadrons[0])!.Parent;
        foreach (int s in squadrons.Skip(1))
            w.Apply(GroupSystem.Enter(force, s));
        return force;
    }

    /// <summary>The worst ship's distance from its station about the force's guide ship, and its heading off the guide's.</summary>
    (double Off, double Dh) Worst(World w, int force)
    {
        var g = w.Groups.Get(force)!;
        w.Groups.LineOffsets(g, offsets);
        var guide = Ship(w, offsets[0].Ship);
        var gp = guide.Position;
        var starboard = Vec3d.Cross(guide.Forward, gp.Normalized());
        double off = 0, dh = 0;
        for (int i = 1; i < offsets.Count; i++)
        {
            var (e, astern, abeam) = offsets[i];
            var station = (gp - guide.Forward * astern + starboard * abeam).Normalized() * Earth.Radius;
            off = Math.Max(off, Earth.Distance(Ship(w, e).Position, station));
            double d = Ship(w, e).HeadingDeg - guide.HeadingDeg;
            dh = Math.Max(dh, Math.Abs(d - 360 * Math.Round(d / 360)));
        }
        return (off, dh);
    }

    [Fact]
    public void The_same_orders_two_levels_up_give_the_same_helm_commands_at_the_same_times()
    {
        (List<(long, ushort, long, double, double)> Helm, ShipState Ship) One(int levels)
        {
            var (w, ai) = Make();
            int c = Class(w, "dreadnought");
            var log = new HelmLog(1);
            w.Register(log);
            w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 12));
            w.Apply(ShipSystem.Spawn(c, Lat + 0.1, Lon + 0.1, 90, 10));
            Unit u = Unit.Ship(1);
            if (levels > 0)
            {
                w.Apply(GroupSystem.Create(1));   // a squadron of one
                u = Unit.Group(w.Groups[0].GroupId);
            }
            if (levels > 1)
            {
                w.Apply(GroupSystem.Form(w.Groups[0].GroupId));   // in a force of one: the order goes down two admirals
                u = Unit.Group(w.Groups[1].GroupId);
            }
            var o = ai.Orders;
            Run(w, 30);
            w.Apply(o.Course(u, 120));
            Run(w, 300);
            w.Apply(o.Speed(u, 18));
            Run(w, 300);
            w.Apply(o.ClearRoute(u));
            w.Apply(o.AddWaypoint(u, Lat - 0.05, Lon + 0.3));
            w.Apply(o.AddWaypoint(u, Lat - 0.15, Lon + 0.2));
            Run(w, 1800);
            w.Apply(o.Follow(u, Unit.Ship(2), 1000));
            Run(w, 1800);
            w.Apply(o.Run(u, Unit.Ship(2)));
            Run(w, 600);
            w.Apply(o.Hold(u));
            w.Apply(o.Speed(u, 9));
            Run(w, 600);
            return (log.Commands, Ship(w, 1));
        }
        var (direct, a) = One(0);
        var (force, b) = One(2);
        output.WriteLine($"{direct.Count} helm and engine commands either way");
        Assert.True(direct.Count > 10);
        Assert.Equal(direct, force);
        Assert.Equal(a.Position, b.Position);
        Assert.Equal(a.Forward, b.Forward);
    }

    [Fact]
    public void A_force_of_columns_in_line_abreast_keeps_its_stations_through_a_turn()
    {
        var (w, ai) = Make();
        int c = Class(w, "dreadnought");
        int f = Force(w, [c, c, c], 3, Formation.LineAbreast, 3000);
        Run(w, 10 * 60);
        var (off, dh) = Worst(w, f);
        output.WriteLine($"steaming on: worst {off:F0} m off station, {dh:F1}° off the guide's heading");
        Assert.True(off < 100, $"{off:F0} m");

        w.Apply(ai.Orders.Course(Unit.Group(f), 270));   // a turn to port, away from the columns: they wheel round the guide
        Run(w, 60 * 60);
        (off, dh) = Worst(w, f);
        output.WriteLine($"an hour after turning to port: worst {off:F0} m off station, {dh:F1}°");
        Assert.True(off < 300, $"{off:F0} m");
        Assert.True(dh < 3, $"{dh:F1}°");
    }

    [Fact]
    public void Squadrons_in_line_ahead_make_one_line()
    {
        // two squadrons of three in line ahead steam exactly as one squadron of six: the same stations, the same ships
        // followed, the same tracks to the last bit
        World Six(bool asForce)
        {
            var (w, ai) = Make();
            int c = Class(w, "dreadnought");
            int unit;
            if (asForce)
                unit = Force(w, [c, c], 3, Formation.Column, 600);
            else
            {
                var at = Earth.Position(Lat, Lon);
                var fwd = Earth.Forward(at, 0);
                for (int i = 0; i < 6; i++)
                {
                    var (la, lo) = Earth.LatLon(at - fwd * (i * 600));
                    w.Apply(ShipSystem.Spawn(c, la, lo, 0, 12));
                }
                w.Apply(GroupSystem.CreateOfNewest(6, Formation.Column, 600));
                unit = w.Groups[0].GroupId;
            }
            Run(w, 2);
            Assert.Equal((CaptainOrder.Station, Unit.Ship(3).Id), (ai.Captains.Of(4).Order, ai.Captains.Of(4).Target));   // ship 4 follows ship 3 either way
            w.Apply(ai.Orders.Course(Unit.Group(unit), 90));
            Run(w, 60 * 60);
            return w;
        }
        var force = Six(true);
        var g = force.Groups.Get(force.Groups[2].GroupId)!;
        force.Groups.LineOffsets(g, offsets);
        Assert.Equal(new[] { 0.0, 600, 1200, 1800, 2400, 3000 }, offsets.Select(o => o.AsternM));
        var one = Six(false);
        for (int e = 1; e <= 6; e++)
            Assert.Equal(Ship(one, e).Position, Ship(force, e).Position);
        var (off, dh) = Worst(force, g.GroupId);
        output.WriteLine($"an hour after the line turned east: worst {off:F0} m off its station on the guide's heading, {dh:F1}°");
        Assert.True(off < 150, $"{off:F0} m");
    }

    [Fact]
    public void A_battle_line_turns_in_succession_on_its_guides_track()
    {
        // two squadrons of three in line ahead, 3 km between them (what the map's force forms with): every ship keeps to
        // the track the guide steamed through a 90° turn, and the second squadron's guide keeps a steady speed. Before
        // the line followed tracks, the second squadron cut the corner by 2.5 km and rang 4 to 18 kn.
        var (w, ai) = Make();
        int c = Class(w, "dreadnought");
        int f = Force(w, [c, c], 3, Formation.Column, 3000);
        var log = new HelmLog(4);
        w.Register(log);
        Run(w, 60);
        w.Apply(ai.Orders.Course(Unit.Group(f), 90));
        var track = new List<Vec3d>();
        var worst = new double[7];
        for (int sec = 0; sec < 50 * 60; sec++)
        {
            Run(w, 1);
            track.Add(Ship(w, 1).Position);
            if (sec % 5 != 0)
                continue;
            for (int e = 2; e <= 6; e++)
            {
                var p = Ship(w, e).Position;
                double best = double.MaxValue;
                int at = 0;
                for (int i = 0; i < track.Count; i++)
                    if (Earth.Distance(p, track[i]) is double d && d < best)
                        (best, at) = (d, i);
                if (at > 10 && at < track.Count - 10)   // alongside the track, not short of its start
                    worst[e] = Math.Max(worst[e], best);
            }
        }
        var speeds = log.Commands.Where(x => (ShipCommand)x.Code == ShipCommand.Speed).Select(x => x.X).ToList();
        output.WriteLine($"off the guide's track at most {string.Join(" ", Enumerable.Range(2, 5).Select(e => $"{e}:{worst[e]:F0}"))} m; " +
                         $"ship 4 rang {speeds.Count}x, {speeds.Min():F1}-{speeds.Max():F1} kn");
        for (int e = 2; e <= 6; e++)
            Assert.True(worst[e] < 200, $"ship {e} {worst[e]:F0} m off the guide's track");
        Assert.InRange(speeds.Min(), 8, 12);
        Assert.InRange(speeds.Max(), 12, 14);
    }

    [Fact]
    public void A_long_line_settles_after_a_turn_and_goes_on_rails()
    {
        // twelve in line ahead through a 90° turn: steering on the offset from the track alone, each ship swung across
        // it a little more than the one ahead and the rear never settled (33 rudder reversals, 18 m off the track an
        // hour on, never on rails); damped by the rate he closes it, the whole line is on rails in under half an hour
        var (w, ai) = Make();
        int c = Class(w, "dreadnought");
        const int n = 12;
        var at = Earth.Position(Lat, Lon);
        var fwd = Earth.Forward(at, 0);
        for (int i = 0; i < n; i++)
        {
            var (la, lo) = Earth.LatLon(at - fwd * (i * 600));
            w.Apply(ShipSystem.Spawn(c, la, lo, 0, 12));
        }
        w.Apply(GroupSystem.CreateOfNewest(n, Formation.Column, 600));
        Run(w, 60);
        w.Apply(ai.Orders.Course(Unit.Group(w.Groups[0].GroupId), 90));
        var last = new int[n + 1];
        int flips = 0, sec = 0;
        for (; sec < 60 * 60 && !Ship(w, n).OnRails; sec++)
        {
            Run(w, 1);
            for (int e = 2; e <= n; e++)
                if (Ship(w, e).Rudder * 180 / Math.PI is double rd && Math.Abs(rd) > 2)
                {
                    if (last[e] != 0 && Math.Sign(rd) != last[e])
                        flips++;
                    last[e] = Math.Sign(rd);
                }
        }
        output.WriteLine($"the line on rails {sec / 60.0:F1} min after the order to turn; {flips} rudder reversals past 2°");
        for (int e = 1; e <= n; e++)
            Assert.True(Ship(w, e).OnRails, $"ship {e} still off rails at {sec / 60.0:F1} min");
        Assert.True(sec < 35 * 60, $"{sec / 60.0:F1} min");
        Assert.True(flips < 15, $"{flips} reversals");
    }

    [Fact]
    public void On_rails_at_speed_a_force_snaps_onto_its_stations_whole()
    {
        var (w, ai) = Make();
        int c = Class(w, "dreadnought");
        w.Apply(Command.SetSpeed(30_000));   // its guide squadron's column turning in succession within a frame
        int f = Force(w, [c, c, c], 3, Formation.LineAbreast, 3000);
        for (int m = 0; m < 60 && !AllOnRails(w, 9); m++)
            Run(w, 60);
        Assert.True(AllOnRails(w, 9));

        w.Apply(ai.Orders.Course(Unit.Group(f), 90));
        Assert.True(AllOnRails(w, 9), "the order kept them on rails");
        int guide = w.Ships.Store.SlotOf(1);
        for (int e = 2; e <= 9; e++)
            Assert.Equal(guide, w.Ships.Store.RidingOn(w.Ships.Store.SlotOf(e)));   // every ship rides the force's guide
        Assert.True(Worst(w, f).Off < 0.01, "on station at once");
        Run(w, 6 * 3600);
        Assert.True(AllOnRails(w, 9), "six hours on, still on rails");
        var (off, dh) = Worst(w, f);
        output.WriteLine($"six hours after the snap: {off:F3} m off station, {dh:F4}°");
        Assert.True(off < 1, $"{off:F2} m");
    }

    static bool AllOnRails(World w, int n)
    {
        for (int e = 1; e <= n; e++)
            if (!Ship(w, e).OnRails)
                return false;
        return true;
    }

    [Fact]
    public void A_squadron_in_a_force_is_held_gathered_pinned_and_asked_back_as_a_ship_in_a_squadron_is()
    {
        var (w, ai) = Make();
        int c = Class(w, "dreadnought");
        int f = Force(w, [c, c, c], 3, Formation.LineAbreast, 3000);
        var force = w.Groups.Get(f)!;
        int sq2 = force.Members[1];
        Run(w, 5);

        // the player's order to the second squadron takes it out of the force's line; its own ships keep its formation
        w.Apply(ai.Orders.Course(Unit.Group(sq2), 45));
        Assert.Equal(Detachment.Nudged, force.DetachmentOf(sq2));
        Run(w, 10 * 60);
        Assert.Equal(45, Ship(w, 4).HeadingDeg, 0);
        Assert.Equal((CaptainOrder.Station, OrderSource.Superior), (ai.Captains.Of(5).Order, ai.Captains.Of(5).Source));
        Assert.Equal(OrderSource.Player, ai.Admirals.Of(sq2).Source);

        // an order to the force gathers it back to its station
        w.Apply(ai.Orders.Course(Unit.Group(f), 10));
        Assert.Equal(Detachment.InLine, force.DetachmentOf(sq2));
        Run(w, 2);
        Assert.Equal((CaptainOrder.Station, OrderSource.Superior), (ai.Admirals.Of(sq2).Order, ai.Admirals.Of(sq2).Source));

        // pinned, it stays out through the force's orders, and is asked back once
        w.Apply(ai.Orders.Detach(Unit.Group(sq2)));
        Assert.Equal(Detachment.Pinned, force.DetachmentOf(sq2));
        int notices = ai.Admirals.Notices;
        w.Apply(ai.Orders.Course(Unit.Group(f), 20));
        Assert.True(ai.Admirals.IsAsked(sq2));
        Assert.Equal(notices + 1, ai.Admirals.Notices);
        Run(w, 60);   // its admiral thinking on: no new notices
        w.Apply(ai.Orders.Course(Unit.Group(f), 30));   // nor while the one raised is up
        Assert.Equal(notices + 1, ai.Admirals.Notices);
        Assert.Equal(Detachment.Pinned, force.DetachmentOf(sq2));
        w.Apply(ai.Orders.Resume(Unit.Group(sq2)));
        Run(w, 2);
        Assert.Equal(Detachment.InLine, force.DetachmentOf(sq2));
        Assert.False(ai.Admirals.IsAsked(sq2));
        Assert.Equal(OrderSource.Superior, ai.Admirals.Of(sq2).Source);

        // a ship pinned out of its squadron is asked back when the squadron's orders change
        w.Apply(ai.Orders.Detach(Unit.Ship(9)));
        w.Apply(ai.Orders.Course(Unit.Group(force.Members[2]), 50));   // the player's, to the squadron: it leaves the force's line too
        Assert.True(ai.Captains.IsAsked(9));
        Assert.Equal(Detachment.Nudged, force.DetachmentOf(force.Members[2]));
    }

    [Fact]
    public void A_unit_its_task_takes_out_of_the_line_is_the_AIs_not_the_players()
    {
        var (w, ai) = Make();
        int c = Class(w, "dreadnought");
        int f = Force(w, [c, c, c], 3, Formation.LineAbreast, 3000);
        var force = w.Groups.Get(f)!;
        int sq2 = force.Members[1];
        Run(w, 5);

        // the commander's task takes the second squadron out of the force's line: on its task, not held by the player,
        // and the force's orders raise no notice for it
        w.Apply(ai.Orders.GoTo(Unit.Group(sq2), Lat + 0.5, Lon, 15, OrderSource.Commander));
        Run(w, 2);
        Assert.Equal(Detachment.Tasked, force.DetachmentOf(sq2));
        int notices = ai.Admirals.Notices;
        w.Apply(ai.Orders.Course(Unit.Group(f), 20));
        Run(w, 60);
        Assert.False(ai.Admirals.IsAsked(sq2));
        Assert.Equal(notices, ai.Admirals.Notices);
        Assert.Equal(Detachment.Tasked, force.DetachmentOf(sq2));   // an order to the force doesn't gather in a tasked one

        // the player's own order takes it from its task: theirs now
        w.Apply(ai.Orders.Course(Unit.Group(sq2), 90));
        Assert.Equal(Detachment.Nudged, force.DetachmentOf(sq2));

        // a ship tasked out of its squadron likewise: no notice when the squadron's orders change
        int sq3 = force.Members[2];
        w.Apply(ai.Orders.GoTo(Unit.Ship(9), Lat - 0.5, Lon, 15, OrderSource.Commander));
        Run(w, 2);
        var squadron = w.Groups.Get(sq3)!;
        Assert.Equal(Detachment.Tasked, squadron.DetachmentOf(9));
        w.Apply(ai.Orders.Course(Unit.Group(sq3), 50, 0, OrderSource.Commander));
        Assert.False(ai.Captains.IsAsked(9));
        Assert.Equal(Detachment.Tasked, squadron.DetachmentOf(9));
        Assert.Equal(Detachment.Tasked, force.DetachmentOf(sq3));   // the commander's order to it is the AI's too

        // the player's Rejoin ends its task and hands it back to its line
        w.Apply(ai.Orders.Resume(Unit.Ship(9)));
        Run(w, 2);
        Assert.Equal(Detachment.InLine, squadron.DetachmentOf(9));
    }

    [Fact]
    public void Limits_and_the_clearance_from_the_coast_come_down_the_tree()
    {
        var (w, ai) = Make();
        int fast = Class(w, "light_cruiser"), slow = Class(w, "dreadnought");
        int f = Force(w, [fast, slow], 2, Formation.LineAbreast, 3000);
        Run(w, 2);
        double cap = Fleet.Get("dreadnought").Class!.Motion.MaxSpeedKn - Settings.GroupSpeedMarginKn;
        w.Apply(ai.Orders.Speed(Unit.Group(f), 30));
        Assert.Equal(cap, Ship(w, 1).SpeedOrderKn, 3);   // the force's guide ship, a light cruiser, held to the battleships
        Assert.Equal(30, ai.Admirals.SpeedOf(f).AskedKn);
        Assert.Equal(3000, Ship(w, 1).ClearanceM);   // the force's half-width, handed to the ship that leads it
        Assert.Equal(0, Ship(w, 3).ClearanceM);

        w.Apply(ai.Orders.Detach(Unit.Group(w.Groups.Get(f)!.Members[1])));   // the battleships go their own way
        Run(w, 2);
        Assert.Equal(0, Ship(w, 1).ClearanceM);
        Assert.True(Ship(w, 1).SpeedOrderKn > cap + 1, $"{Ship(w, 1).SpeedOrderKn:F1} kn: the cruisers alone may run");
    }

    [Fact]
    public void A_ui_forms_a_force_of_squadrons_before_it_knows_the_forces_id()
    {
        var (w, _) = Make();
        int c = Class(w, "dreadnought");
        for (int i = 0; i < 4; i++)
            w.Apply(ShipSystem.Spawn(c, Lat + 0.02 * i, Lon, 0, 12));
        w.Apply(GroupSystem.Create(1));
        w.Apply(GroupSystem.Join(2, 1));
        w.Apply(GroupSystem.Create(3));
        w.Apply(GroupSystem.Create(4));
        w.Apply(GroupSystem.Form(1, Formation.Column, 1000));   // as the map does: form on the selected squadron,
        w.Apply(GroupSystem.EnterAlongside(2, 1));              // then the picked ships' squadrons alongside it
        w.Apply(GroupSystem.EnterAlongside(3, 1));
        w.Apply(GroupSystem.EnterAlongside(3, 1));              // twice: nothing changes
        var force = w.Groups.Get(w.Groups.Get(1)!.Parent)!;
        Assert.True(force.IsForce);
        Assert.Equal(new[] { 1, 2, 3 }, force.Members);
        w.Apply(GroupSystem.EnterAlongside(force.GroupId, 2));   // a force into itself: refused
        Assert.Equal(0, force.Parent);
    }

    [Fact]
    public void A_force_with_held_and_pinned_squadrons_replays_from_the_log()
    {
        World Script()
        {
            var (w, ai) = Make();
            int c = Class(w, "dreadnought");
            var o = ai.Orders;
            int f = Force(w, [c, c, c], 3, Formation.LineAbreast, 2500);
            var force = w.Groups.Get(f)!;
            Run(w, 60.25);
            w.Apply(o.Course(Unit.Group(f), 30));
            Run(w, 300);
            w.Apply(o.Course(Unit.Group(force.Members[1]), 120));
            w.Apply(o.Detach(Unit.Group(force.Members[2])));
            w.Apply(o.Speed(Unit.Ship(2), 8));
            Run(w, 600);
            w.Apply(GroupSystem.SetFormation(f, Formation.Column, 1000));
            w.Apply(o.Speed(Unit.Group(f), 14));
            Run(w, 400);
            w.Apply(o.Resume(Unit.Group(force.Members[2])));
            Run(w, 500);
            return w;
        }
        var live = Script();
        var replay = new World(Start, 13, Settings);
        _ = new AiSystem(replay);
        Class(replay, "dreadnought");
        foreach (var sc in live.CommandLog)
        {
            replay.AdvanceTo(sc.At);
            replay.Apply(sc.Command);
        }
        replay.AdvanceTo(live.Time);
        Assert.Equal(live.ComputeHash(), replay.ComputeHash());
    }

    [Fact]
    public void A_force_of_four_squadrons_of_eight_thinks_without_allocating()
    {
        var (w, ai) = Make();
        int c = Class(w, "destroyer");
        int f = Force(w, [c, c, c, c], 8, Formation.LineAbreast, 2000);
        w.Apply(ai.Orders.Course(Unit.Group(f), 60));
        var snap = new Snapshot();
        Run(w, 600);   // the same work once untimed: first runs of a path may allocate, play mustn't
        w.Fill(snap);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(w, 600);
        w.Fill(snap);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(5, snap.Groups.Count);
        Assert.True(snap.Groups.Force[snap.Groups.IndexOfGroup(f)]);
        Assert.Equal(1, snap.Groups.GuideShip[snap.Groups.IndexOfGroup(f)]);
    }
}
