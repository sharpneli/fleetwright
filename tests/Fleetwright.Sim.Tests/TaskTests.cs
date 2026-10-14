using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Tasks (docs/battle/command.md 5.1c): a squadron goes to a point and reports it's there, and the player's
/// own order ends a task; a ship patrols a line; a squadron intercepts an enemy its scout holds in sight from 50 km
/// off; a detached squadron joins its force again, and one alone joins another into a force; a ship withdraws from an
/// enemy and then home. Clear weather, guns held, doctrine neutral (nobody takes up a fight).</summary>
public class TaskTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.0, Lon = 4.0;

    static (World W, AiSystem Ai, int Class) Make()
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        return (w, ai, w.Ships.AddClass(Fleet.Get("dreadnought").Class!));
    }

    /// <summary>n ships of a class in line ahead from a point, heading somewhere at a speed, formed into a squadron; its
    /// group id.</summary>
    static int Column(World w, AiSystem ai, int cls, Vec3d at, double heading, int n, byte side = 0, double kn = 15)
    {
        int first = w.Ships.NewestEntity + 1;
        var f = Earth.Forward(at, heading + 180);
        var p = at;
        for (int i = 0; i < n; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(cls, la, lo, heading, kn, side));
            Earth.Advance(ref p, ref f, 600);
        }
        w.Apply(GroupSystem.Create(first, Formation.Column, 600));
        int g = w.Groups[w.Groups.Count - 1].GroupId;
        for (int e = first + 1; e < first + n; e++)
            w.Apply(GroupSystem.Add(g, e));
        w.Apply(ai.Orders.HoldFire(Unit.Group(g)));
        w.Apply(ai.Orders.Speed(Unit.Group(g), kn));
        return g;
    }

    static Vec3d Off(double bearing, double m)
    {
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, bearing);
        Earth.Advance(ref p, ref f, m);
        return p;
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static double RunUntil(World w, Func<bool> done, double maxS)
    {
        for (double s = 0; s < maxS; s += 10)
        {
            if (done())
                return s;
            w.AdvanceTo(w.Time + 10_000);
        }
        return double.NaN;
    }

    [Fact]
    public void A_squadron_goes_to_a_point_and_reports_and_the_players_order_ends_a_task()
    {
        var (w, ai, c) = Make();
        int g = Column(w, ai, c, Earth.Position(Lat, Lon), 0, 3);
        var u = Unit.Group(g);
        var (la, lo) = Earth.LatLon(Off(90, 20_000));
        w.Apply(ai.Orders.GoTo(u, la, lo, 18));
        double s = RunUntil(w, () => ai.Tasks.Of(u).Status == TaskState.Done, 3 * 3600);
        output.WriteLine($"there in {s / 60:F0} min: {ai.Tasks.Of(u)}");
        Assert.Equal((TaskKind.GoTo, TaskState.Done, TaskReason.Arrived), ai.Tasks.Of(u));
        Assert.InRange(s, 30 * 60, 50 * 60);   // 20 km at 18 kn is 36 minutes, with the turn

        w.Apply(ai.Orders.GoTo(u, Lat, Lon));
        Assert.Equal(TaskState.OnTask, ai.Tasks.Of(u).Status);
        w.Apply(ai.Orders.Speed(u, 12));   // the task's speed now
        Assert.Equal(TaskState.OnTask, ai.Tasks.Of(u).Status);
        w.Apply(ai.Orders.Course(u, 180));
        Assert.Equal(TaskKind.None, ai.Tasks.Of(u).Kind);
    }

    [Fact]
    public void A_ship_patrols_a_line_back_and_forth()
    {
        var (w, ai, c) = Make();
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 15));
        var (la, lo) = Earth.LatLon(Off(0, 15_000));
        w.Apply(ai.Orders.Patrol(Unit.Ship(1), la, lo));
        bool south = false;
        for (int m = 0; m < 120; m++)
        {
            w.AdvanceTo(w.Time + 60_000);
            double h = Ship(w, 1).HeadingDeg;
            south |= h is > 150 and < 210;
        }
        double back = Earth.Distance(Ship(w, 1).Position, Earth.Position(Lat, Lon));
        output.WriteLine($"after 2 h: turned south {south}, {back:F0} m from the start; {ai.Tasks.Of(Unit.Ship(1))}");
        Assert.True(south);
        Assert.True(back < 16_000);
        Assert.Equal(TaskState.OnTask, ai.Tasks.Of(Unit.Ship(1)).Status);
    }

    [Fact]
    public void A_squadron_intercepts_an_enemy_its_scout_holds_from_50_km()
    {
        var (w, ai, c) = Make();
        int lc = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        int g = Column(w, ai, c, Earth.Position(Lat, Lon), 0, 3);
        // the enemy 50 km to the east, steaming north at 12 kn; a scout of ours 10 km off him, keeping him in sight
        var enemyAt = Off(90, 50_000);
        int enemy = Column(w, ai, c, enemyAt, 0, 2, side: 1, kn: 12);
        var (sla, slo) = Earth.LatLon(Off(90, 40_000));
        w.Apply(ShipSystem.Spawn(lc, sla, slo, 0, 12));
        int scout = w.Ships.NewestEntity;
        w.Apply(ai.Orders.HoldFire(Unit.Ship(scout)));
        w.Apply(ai.Orders.Shadow(Unit.Ship(scout), Unit.Group(enemy), 10_000));
        w.AdvanceTo(w.Time + 60_000);
        int target = w.Groups.Get(enemy)!.Guide;
        var u = Unit.Group(g);
        w.Apply(ai.Orders.Intercept(u, target));
        double s = RunUntil(w, () => ai.Tasks.Of(u).Status != TaskState.OnTask, 3 * 3600);
        double r = Earth.Distance(Ship(w, w.Groups.Get(g)!.Guide).Position, Ship(w, target).Position);
        output.WriteLine($"in contact after {s / 60:F0} min, {r:F0} m off: {ai.Tasks.Of(u)}");
        Assert.Equal((TaskKind.Intercept, TaskState.Done, TaskReason.InContact), ai.Tasks.Of(u));
        Assert.True(r <= Settings.FightCompareM + 1_000, $"{r:F0} m");
        Assert.InRange(s, 10 * 60, 60 * 60);
    }

    [Fact]
    public void A_detached_squadron_joins_its_force_again_and_one_alone_joins_another()
    {
        var (w, ai, c) = Make();
        int a = Column(w, ai, c, Earth.Position(Lat, Lon), 0, 2);
        int b = Column(w, ai, c, Off(90, 3_000), 0, 2);
        w.Apply(GroupSystem.Form(a, Formation.LineAbreast, 3_000));
        int force = w.Groups.Get(a)!.Parent;
        w.Apply(GroupSystem.Enter(force, b));
        w.Apply(ai.Orders.Course(Unit.Group(b), 90));   // the player takes it off east
        w.AdvanceTo(w.Time + 15 * 60_000);
        Assert.NotEqual(Detachment.InLine, w.Groups.Get(force)!.DetachmentOf(b));
        w.Apply(ai.Orders.Join(Unit.Group(b), Unit.Group(force)));
        double s = RunUntil(w, () => ai.Tasks.Of(Unit.Group(b)).Status != TaskState.OnTask, 3600);
        output.WriteLine($"back with its force after {s / 60:F0} min: {ai.Tasks.Of(Unit.Group(b))}");
        Assert.Equal(TaskState.Done, ai.Tasks.Of(Unit.Group(b)).Status);
        Assert.Equal(Detachment.InLine, w.Groups.Get(force)!.DetachmentOf(b));

        var bow = Ship(w, w.Groups.GuideShip(w.Groups.Get(force)!)).Position;   // on the force's bow, where it is now: a stern chase
        var fb = Earth.Forward(bow, 45);                                           // at 4 kn in hand would take hours
        Earth.Advance(ref bow, ref fb, 20_000);
        int lone = Column(w, ai, c, bow, 0, 2);
        w.Apply(ai.Orders.Join(Unit.Group(lone), Unit.Group(force)));
        s = RunUntil(w, () => ai.Tasks.Of(Unit.Group(lone)).Status != TaskState.OnTask, 3600);
        output.WriteLine($"the third squadron joined after {s / 60:F0} min");
        Assert.Equal(force, w.Groups.Get(lone)!.Parent);
    }

    [Fact]
    public void A_ship_withdraws_from_an_enemy_then_home()
    {
        var (w, ai, c) = Make();
        var (hla, hlo) = Earth.LatLon(Off(270, 40_000));
        w.Apply(ai.Orders.Home(0, hla, hlo));
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 90, 15));
        var (ela, elo) = Earth.LatLon(Off(0, 12_000));
        w.Apply(ShipSystem.Spawn(c, ela, elo, 180, 12, 1));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(1)));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(2)));
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(ai.Orders.WithdrawHome(Unit.Ship(1)));
        w.AdvanceTo(w.Time + 2 * 60_000);
        Assert.Equal(Ai.Captains.CaptainOrder.Run, ai.Captains.Of(1).Order);   // away from him first
        double s = RunUntil(w, () => ai.Tasks.Of(Unit.Ship(1)).Status != TaskState.OnTask, 6 * 3600);
        output.WriteLine($"home after {s / 60:F0} min: {ai.Tasks.Of(Unit.Ship(1))}, {Earth.Distance(Ship(w, 1).Position, Off(270, 40_000)):F0} m off it");
        Assert.Equal((TaskKind.Withdraw, TaskState.Done, TaskReason.Home), ai.Tasks.Of(Unit.Ship(1)));
    }
}
