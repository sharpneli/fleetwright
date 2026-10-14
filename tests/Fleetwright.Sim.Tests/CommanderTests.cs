using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The side's commander (docs/battle/command.md 5.1d): who he commands (none in a world told nothing; a
/// scenario's on unless it's the player's side; the player's order takes a unit from him, "leave it to the admiral"
/// hands it back); a unit's plan, its tasks one after another; and what he gives a unit whose plan's run out, by its
/// role. Clear weather, guns held.</summary>
public class CommanderTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.0, Lon = 4.0;

    static (World W, AiSystem Ai) Make()
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        return (w, ai);
    }

    static int Class(World w, string design) => w.Ships.AddClass(Fleet.Get(design).Class!);

    /// <summary>n ships in line ahead from a point, formed into a squadron if more than one; the unit.</summary>
    static Unit Column(World w, AiSystem ai, int cls, Vec3d at, double heading, int n, byte side = 0, double kn = 15)
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
        if (n == 1)
        {
            w.Apply(ai.Orders.HoldFire(Unit.Ship(first)));
            return Unit.Ship(first);
        }
        w.Apply(GroupSystem.Create(first, Formation.Column, 600));
        int g = w.Groups[w.Groups.Count - 1].GroupId;
        for (int e = first + 1; e < first + n; e++)
            w.Apply(GroupSystem.Add(g, e));
        w.Apply(ai.Orders.HoldFire(Unit.Group(g)));
        w.Apply(ai.Orders.Speed(Unit.Group(g), kn));
        return Unit.Group(g);
    }

    static Vec3d Off(double bearing, double m, double lat = Lat, double lon = Lon)
    {
        var p = Earth.Position(lat, lon);
        var f = Earth.Forward(p, bearing);
        Earth.Advance(ref p, ref f, m);
        return p;
    }

    static (double, double) LatLon(Vec3d p) => Earth.LatLon(p);

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)(s * 1000));

    [Fact]
    public void A_world_told_nothing_has_none_and_a_scenarios_command_every_side_but_the_players()
    {
        var (w, ai) = Make();
        var c = Class(w, "dreadnought");
        var mine = Column(w, ai, c, Earth.Position(Lat, Lon), 0, 2, side: 0);
        var theirs = Column(w, ai, c, Off(0, 100_000), 180, 2, side: 1);
        Assert.False(ai.Commanders.Active);
        Assert.False(ai.Commanders.Commands(theirs, 1, w.PlayerSide));

        // as a scenario has it: every side's commander, the player's side 0
        w.Apply(ai.Orders.Commander(0, CommanderMode.Auto));
        w.Apply(ai.Orders.Commander(1, CommanderMode.Auto));
        w.Apply(Command.SetPlayerSide(0));
        Assert.False(ai.Commanders.Commands(mine, 0, w.PlayerSide));
        Assert.True(ai.Commanders.Commands(theirs, 1, w.PlayerSide));
        w.Apply(Command.SetPlayerSide(-1));   // watching: both his
        Assert.True(ai.Commanders.Commands(mine, 0, w.PlayerSide));
        w.Apply(Command.SetPlayerSide(0));
        w.Apply(ai.Orders.Commander(0, CommanderMode.On));   // the player automates their side
        Assert.True(ai.Commanders.Commands(mine, 0, w.PlayerSide));

        // once he's thought, the player's order to a unit takes it from him; "leave it to the admiral" hands it back
        w.Apply(ai.Orders.Course(mine, 90));
        Assert.True(ai.Commanders.Commands(mine, 0, w.PlayerSide));   // before he's thought: the scenario's orders
        Run(w, 61);
        w.Apply(ai.Orders.Course(mine, 45));
        Assert.False(ai.Commanders.Commands(mine, 0, w.PlayerSide));
        w.Apply(ai.Orders.FireAtWill(mine));
        w.Apply(ai.Orders.Automate(mine, true));
        Assert.True(ai.Commanders.Commands(mine, 0, w.PlayerSide));
        w.Apply(ai.Orders.FireAtWill(mine));   // the guns don't take it
        Assert.True(ai.Commanders.Commands(mine, 0, w.PlayerSide));

        // a unit handed to him on a side he's off for is his; one taken, not
        w.Apply(ai.Orders.Commander(0, CommanderMode.Auto));
        Assert.True(ai.Commanders.Commands(mine, 0, w.PlayerSide));
        w.Apply(ai.Orders.Automate(mine, null));
        Assert.False(ai.Commanders.Commands(mine, 0, w.PlayerSide));
    }

    [Fact]
    public void A_units_plan_runs_task_after_task_and_then_its_commander_gives_it_one()
    {
        var (w, ai) = Make();
        var c = Class(w, "dreadnought");
        var u = Column(w, ai, c, Earth.Position(Lat, Lon), 0, 3);
        w.Apply(ai.Orders.Commander(0, CommanderMode.On));
        var (la, lo) = LatLon(Off(0, 8_000));
        var (lb, lb2) = LatLon(Off(90, 8_000, la, lo));
        w.Apply(ai.Orders.GoTo(u, la, lo, 18));
        w.Apply(UnitOrders.Then(ai.Orders.GoTo(u, lb, lb2, 18)));
        Assert.Equal(1, ai.Tasks.QueuedOf(u));
        double first = double.NaN, second = double.NaN;
        for (double s = 0; s < 2 * 3600 && double.IsNaN(second); s += 10)
        {
            Run(w, 10);
            if (double.IsNaN(first) && ai.Tasks.QueuedOf(u) == 0)
                first = s;
            if (!double.IsNaN(first) && ai.Commanders.GivenOf(u).Given != TaskKind.None)
                second = s;
        }
        var given = ai.Commanders.GivenOf(u);
        output.WriteLine($"the first done at {first / 60:F0} min, the second at {second / 60:F0} min; then {given}, {ai.Tasks.Of(u)}");
        Assert.InRange(first, 10 * 60, 30 * 60);   // 8 km at 18 kn is 14 minutes
        Assert.InRange(second, first + 10 * 60, first + 40 * 60);
        Assert.Equal((TaskKind.Search, CommanderWhy.Idle), (given.Given, given.Why));   // the main body, searching on
        Assert.Equal(TaskKind.Search, ai.Tasks.Of(u).Kind);

        // a new task in place of the plan drops what was to come
        w.Apply(ai.Orders.GoTo(u, la, lo));
        w.Apply(UnitOrders.Then(ai.Orders.GoTo(u, lb, lb2)));
        w.Apply(ai.Orders.GoTo(u, lb, lb2));
        Assert.Equal(0, ai.Tasks.QueuedOf(u));
    }

    [Fact]
    public void Units_through_with_their_tasks_are_given_their_roles()
    {
        var (w, ai) = Make();
        int bb = Class(w, "dreadnought"), lc = Class(w, "light_cruiser"), dd = Class(w, "destroyer");
        var main = Column(w, ai, bb, Earth.Position(Lat, Lon), 0, 4);
        var heavy = Column(w, ai, bb, Off(270, 6_000), 0, 1);
        var scout = Column(w, ai, lc, Off(0, 10_000), 0, 2);
        var flotilla = Column(w, ai, dd, Off(90, 4_000), 0, 4);
        w.Apply(ai.Orders.Commander(0, CommanderMode.On));
        foreach (var u in new[] { main, heavy, scout, flotilla })
        {
            var guide = w.Ships.Store[w.Ships.Store.SlotOf(u.ShipOf(w.Groups))];
            var (la, lo) = LatLon(Off(0, 3_000, Earth.LatLon(guide.Position).Lat, Earth.LatLon(guide.Position).Lon));
            w.Apply(ai.Orders.GoTo(u, la, lo));   // a short plan, soon through
        }
        Run(w, 30 * 60);
        foreach (var (u, name) in new[] { (main, "main"), (heavy, "heavy"), (scout, "scout"), (flotilla, "flotilla") })
            output.WriteLine($"{name}: given {ai.Commanders.GivenOf(u)}, on {ai.Tasks.Of(u)}");
        Assert.Equal(TaskKind.Search, ai.Commanders.GivenOf(main).Given);
        Assert.Equal((TaskKind.Join, main.Id), (ai.Commanders.GivenOf(heavy).Given, ai.Commanders.GivenOf(heavy).Target));
        Assert.Equal(main.GroupId, w.Groups.Of(heavy.Entity)?.GroupId);   // concentrated: in the main body's line
        Assert.Equal((TaskKind.Scout, main.Id), (ai.Commanders.GivenOf(scout).Given, ai.Commanders.GivenOf(scout).Target));
        Assert.Equal((TaskKind.Screen, main.Id), (ai.Commanders.GivenOf(flotilla).Given, ai.Commanders.GivenOf(flotilla).Target));
        foreach (var u in new[] { main, scout, flotilla })
            Assert.Equal(TaskState.OnTask, ai.Tasks.Of(u).Status);
    }
}
