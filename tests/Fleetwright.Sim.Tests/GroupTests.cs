using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Groups and formations (docs/battle/plan.md step 2.4): ships scattered at sea form up in column, line abreast
/// and echelon and keep it through a turn of the guide, which one order makes; the guide is held to what the slowest
/// can make; ships that leave or sink are let go; groups replay from the log; admirals allocate nothing. All in the
/// open North Sea.</summary>
public class GroupTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.5, Lon = 4.0;

    static Unit G(World w) => Unit.Group(w.Groups[0].GroupId);

    static (World W, AiSystem Ai) Make(int n, string design = "dreadnought", string? guideDesign = null)
    {
        var w = new World(Start, 5, Settings);
        var ai = new AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Get(design).Class!);
        int g = guideDesign != null ? w.Ships.AddClass(Fleet.Get(guideDesign).Class!) : c;
        // scattered: a rough box south-west of the guide, all heading somewhere else
        w.Apply(ShipSystem.Spawn(g, Lat, Lon, 90, 12));
        for (int i = 1; i < n; i++)
            w.Apply(ShipSystem.Spawn(c, Lat - 0.006 * i, Lon - 0.01 * (i % 3), 20 * i, 10));
        return (w, ai);
    }

    static void Group(World w, int n, Formation f, double spacing = 600)
    {
        w.Apply(GroupSystem.Create(1, f, spacing));
        for (int e = 2; e <= n; e++)
            w.Apply(GroupSystem.Add(1, e));
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)(s * 1000));

    /// <summary>The worst member's distance from its station and heading off the guide's.</summary>
    static (double Off, double Dh) Worst(World w)
    {
        var g = w.Groups[0];
        double off = 0, dh = 0;
        var guide = Ship(w, g.Guide);
        for (int i = 1; i < g.Members.Count; i++)
        {
            var (t, astern, abeam) = GroupSystem.StationOf(g.Formation, g.SpacingM, g.Line, i);
            var ts = Ship(w, t);
            var up = ts.Position.Normalized();
            var station = (ts.Position - ts.Forward * astern + Vec3d.Cross(ts.Forward, up) * abeam).Normalized() * Earth.Radius;
            var m = Ship(w, g.Members[i]);
            off = Math.Max(off, Earth.Distance(m.Position, station));
            double d = m.HeadingDeg - guide.HeadingDeg;
            dh = Math.Max(dh, Math.Abs(d - 360 * Math.Round(d / 360)));
        }
        return (off, dh);
    }

    [Theory]
    [InlineData(Formation.Column)]
    [InlineData(Formation.LineAbreast)]
    [InlineData(Formation.EchelonStarboard)]
    [InlineData(Formation.EchelonPort)]
    public void A_scattered_group_forms_up_and_keeps_station_through_a_turn(Formation f)
    {
        var (w, ai) = Make(4);
        Group(w, 4, f);
        Run(w, 45 * 60);
        var (off, dh) = Worst(w);
        output.WriteLine($"{f}: after 45 min the worst is {off:F0} m off station, heading {dh:F1}° off the guide's");
        Assert.True(off < 150, $"{off:F0} m off station");
        Assert.True(dh < 3, $"{dh:F1}°");

        w.Apply(ai.Orders.Course(G(w), 180));   // one order to the group: its admiral hands it to the guide
        Run(w, 30 * 60);
        (off, dh) = Worst(w);
        output.WriteLine($"{f}: 30 min after the guide turned south, {off:F0} m off, {dh:F1}°");
        Assert.True(off < 150, $"{off:F0} m off station");
        Assert.True(dh < 3, $"{dh:F1}°");
    }

    [Fact]
    public void The_guide_is_held_to_what_the_slowest_can_make()
    {
        var (w, ai) = Make(3, design: "dreadnought", guideDesign: "light_cruiser");
        Group(w, 3, Formation.Column);
        w.Apply(ai.Orders.Speed(G(w), 28));
        double cap = Fleet.Get("dreadnought").Class!.Motion.MaxSpeedKn - Settings.GroupSpeedMarginKn;
        Assert.Equal(cap, Ship(w, 1).SpeedOrderKn, 3);   // at once: the limit is applied as the order is handed down
        Assert.Equal(28, ai.Admirals.SpeedOf(w.Groups[0].GroupId).AskedKn);   // and what was asked is kept
        Run(w, 5);
        Assert.Equal(cap, Ship(w, 1).SpeedOrderKn, 3);
    }

    [Fact]
    public void A_group_of_the_newest_ships_takes_exactly_them_in_spawn_order()
    {
        var (w, _) = Make(2);   // ships 1 and 2, not in it
        int c = w.Ships.ClassOf(w.Ships.Store.SlotOf(1));
        for (int i = 0; i < 4; i++)
            w.Apply(ShipSystem.Spawn(c, Lat + 0.01 * i, Lon, 0, 10));
        w.Apply(ShipSystem.Drop(5));
        w.Apply(GroupSystem.CreateOfNewest(4, Formation.LineAbreast, 800));
        Assert.Equal(1, w.Groups.Count);
        Assert.Equal(new[] { 3, 4, 6 }, w.Groups[0].Members);
        Assert.Equal(Formation.LineAbreast, w.Groups[0].Formation);
        Assert.Equal(800, w.Groups[0].SpacingM);
    }

    [Fact]
    public void Ships_that_leave_or_sink_are_let_go_and_a_disbanded_group_frees_its_captains()
    {
        var (w, ai) = Make(4);
        Group(w, 4, Formation.Column);
        Run(w, 5);
        for (int e = 2; e <= 4; e++)
            Assert.Equal((CaptainOrder.Station, Whose.Superior), (ai.Captains.Of(e).Order, ai.Captains.Of(e).Whose));
        w.Apply(GroupSystem.Remove(3));
        w.Apply(ShipSystem.Drop(4));
        Run(w, 5);
        Assert.Equal(new[] { 1, 2 }, w.Groups[0].Members);
        Assert.Equal((CaptainOrder.Course, Whose.Own), (ai.Captains.Of(3).Order, ai.Captains.Of(3).Whose));   // his own again, holding
        Assert.Equal(CaptainOrder.None, ai.Captains.Of(4).Order);   // gone with his ship
        Assert.Equal(CaptainOrder.Station, ai.Captains.Of(2).Order);

        w.Apply(GroupSystem.Remove(1));   // the guide leaves: ship 2 guides, holding what it has; ship 1 is its own
        Run(w, 5);
        Assert.Equal(2, w.Groups[0].Guide);
        Assert.Equal((CaptainOrder.Course, Whose.Superior), (ai.Captains.Of(2).Order, ai.Captains.Of(2).Whose));
        Assert.Equal(Whose.Own, ai.Captains.Of(1).Whose);
        Assert.Equal(0, ai.Captains.SpeedOf(1).LimitKn);

        w.Apply(GroupSystem.Disband(w.Groups[0].GroupId));
        Run(w, 5);
        Assert.Equal(0, w.Groups.Count);
        Assert.Equal(Whose.Own, ai.Captains.Of(2).Whose);
    }

    [Fact]
    public void A_ship_joins_a_squadron_at_a_place_and_moves_within_it()
    {
        // the rail's drag and drop (docs/battle/admirals-orders.md O8c)
        var (w, _) = Make(5);
        Group(w, 3, Formation.Column);
        w.Apply(GroupSystem.Create(4));
        int sq = w.Groups.Of(1)!.GroupId;
        w.Apply(GroupSystem.Add(sq, 5, 1));   // ahead of the second
        Assert.Equal(new[] { 1, 5, 2, 3 }, w.Groups.Get(sq)!.Members);
        w.Apply(GroupSystem.Add(sq, 3, 0));   // within it: to the head, guiding
        Assert.Equal(new[] { 3, 1, 5, 2 }, w.Groups.Get(sq)!.Members);
        Assert.Equal(3, w.Groups.Get(sq)!.Guide);
        w.Apply(GroupSystem.Add(sq, 4, 2));   // out of hers, which goes, empty
        Assert.Equal(new[] { 3, 1, 4, 5, 2 }, w.Groups.Get(sq)!.Members);
        Assert.Single(w.Groups.Groups);
    }

    [Fact]
    public void A_group_goes_under_another_s_admiral()
    {
        var (w, _) = Make(4);
        w.Apply(GroupSystem.Create(1));
        w.Apply(GroupSystem.Add(w.Groups.Of(1)!.GroupId, 2));
        w.Apply(GroupSystem.Create(3));
        w.Apply(GroupSystem.Create(4));
        int a = w.Groups.Of(1)!.GroupId, b = w.Groups.Of(3)!.GroupId, c = w.Groups.Of(4)!.GroupId;
        w.Groups.SetName(a, "1st BCS");

        w.Apply(GroupSystem.Subordinate(b, a));   // onto a squadron: a force of the two, it guiding
        var f = w.Groups.Get(w.Groups.Get(a)!.Parent)!;
        Assert.True(f.IsForce);
        Assert.Equal("1st BCS force", f.Name);
        Assert.Equal(new[] { a, b }, f.Members);
        Assert.Equal(Formation.Column, f.Formation);

        w.Apply(GroupSystem.Subordinate(c, f.GroupId));   // onto a force: into it
        Assert.Equal(new[] { a, b, c }, f.Members);

        w.Apply(GroupSystem.Subordinate(c, b));   // onto a squadron in a force: wrapped where it was
        var f2 = w.Groups.Get(w.Groups.Get(b)!.Parent)!;
        Assert.Equal(new[] { a, f2.GroupId }, f.Members);
        Assert.Equal(new[] { b, c }, f2.Members);
        Assert.Equal(f.GroupId, f2.Parent);

        w.Apply(GroupSystem.Subordinate(f.GroupId, a));   // a force under its own member: refused
        Assert.Equal(0, f.Parent);
        Assert.Equal(f.GroupId, w.Groups.Get(a)!.Parent);
    }

    [Fact]
    public void Groups_replay_from_the_log()
    {
        World Script()
        {
            var (w, _) = Make(5);
            Group(w, 5, Formation.Column, 500);
            Run(w, 600.5);
            w.Apply(GroupSystem.SetFormation(1, Formation.LineAbreast, 700));
            w.Apply(new UnitOrders(3).Course(Unit.Group(1), 30));
            Run(w, 400);
            w.Apply(GroupSystem.Remove(2));
            Run(w, 300);
            return w;
        }
        var live = Script();
        var replay = new World(Start, 5, Settings);
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
    public void Four_groups_of_eight_think_without_allocating()
    {
        var w = new World(Start, 5, Settings);
        _ = new AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        for (int i = 0; i < 32; i++)
            w.Apply(ShipSystem.Spawn(c, Lat + 0.02 * (i / 8), Lon + 0.01 * (i % 8), 90, 15));
        for (int g = 0; g < 4; g++)
        {
            w.Apply(GroupSystem.Create(g * 8 + 1, (Formation)(g % 4), 400));
            for (int i = 2; i <= 8; i++)
                w.Apply(GroupSystem.Add(g + 1, g * 8 + i));
        }
        var snap = new Snapshot();
        Run(w, 600);   // the same work once untimed: first runs of a path (the JIT, tiering) may allocate, play mustn't
        w.Fill(snap);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(w, 600);
        w.Fill(snap);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(4, snap.Groups.Count);
        Assert.Equal(8, snap.Groups.MemberCount[2]);
        Assert.Equal(2, snap.Groups.IndexOfShip(20));
    }
}
