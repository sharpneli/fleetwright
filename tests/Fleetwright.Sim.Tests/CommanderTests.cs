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
    // clear air: these test their own rules; the smoke field's part in sight is SmokeSightTests', its allocation SmokeSystemTests'
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };
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

        // the player's order by hand to a unit takes it from him at once, even before he's thought (decision 14: he's on
        // for the player's side from the start); a scenario's opening orders never do; "leave it to the admiral" hands
        // it back
        w.Apply(ai.Orders.Course(mine, 90));
        Assert.True(ai.Commanders.Commands(mine, 0, w.PlayerSide));   // the scenario's orders
        w.Apply(ai.Orders.ByHand.Course(mine, 45));
        Assert.False(ai.Commanders.Commands(mine, 0, w.PlayerSide));
        Run(w, 61);
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
    public void A_side_holding_an_enemy_sends_its_heavy_units_to_close_him()
    {
        var (w, ai) = Make();
        int bb = Class(w, "dreadnought"), lc = Class(w, "light_cruiser");
        var main = Column(w, ai, bb, Earth.Position(Lat, Lon), 0, 3, side: 1);
        var enemy = Column(w, ai, bb, Off(90, 60_000), 0, 2, side: 0, kn: 12);
        var (sla, slo) = LatLon(Off(90, 48_000));
        w.Apply(ShipSystem.Spawn(lc, sla, slo, 0, 12, 1));   // a scout of his, keeping the enemy in sight
        var scout = Unit.Ship(w.Ships.NewestEntity);
        w.Apply(ai.Orders.HoldFire(scout));
        Run(w, 60);   // a look, so the scout holds him
        w.Apply(ai.Orders.Shadow(scout, enemy, 12_000));
        w.Apply(ai.Orders.Commander(1, CommanderMode.On));
        double closed = double.NaN;
        for (double s = 0; s < 2 * 3600 && double.IsNaN(closed); s += 60)
        {
            Run(w, 60);
            var g = w.Ships.Store[w.Ships.Store.SlotOf(main.ShipOf(w.Groups))];
            var e = w.Ships.Store[w.Ships.Store.SlotOf(enemy.ShipOf(w.Groups))];
            if (Earth.Distance(g.Position, e.Position) <= Settings.FightCompareM)
                closed = s;
        }
        var given = ai.Commanders.GivenOf(main);
        output.WriteLine($"closed to {Settings.FightCompareM / 1000:F0} km after {closed / 60:F0} min; given {given}; the scout {ai.Commanders.GivenOf(scout)}; " +
                         $"phase {ai.Commanders.PhaseOf(1)}");
        Assert.Equal((TaskKind.Intercept, CommanderWhy.Contact), (given.Given, given.Why));
        Assert.InRange(closed, 10 * 60, 70 * 60);   // 30 km to close at 15 kn, converging: about 50 minutes
        Assert.Equal(TaskKind.None, ai.Commanders.GivenOf(scout).Given);   // shadowing as told: his scout keeps at it
        Assert.Equal(CommanderPhase.Battle, ai.Commanders.PhaseOf(1).Phase);
    }

    [Fact]
    public void Flotillas_are_sent_in_on_an_enemy_line_their_heavy_units_engage()
    {
        // four destroyers against one dreadnought: an attack worth making (docs/battle/command.md 5.1g)
        var (w, ai) = Make();
        int bb = Class(w, "dreadnought"), dd = Class(w, "destroyer");
        var line = Column(w, ai, bb, Earth.Position(Lat, Lon), 180, 4, side: 0);
        var flotilla = Column(w, ai, dd, Off(270, 3_000), 180, 4, side: 0);
        var enemy = Column(w, ai, bb, Off(90, 16_000), 180, 1, side: 1);
        w.Apply(ai.Orders.FireAtWill(flotilla));
        w.Apply(ai.Orders.Screen(flotilla, line, 3_000));
        w.Apply(ai.Orders.Commander(0, CommanderMode.On));
        long seen = w.CombatLog.Written;
        int torpedoes = 0;
        double sent = double.NaN, fired = double.NaN;
        // alone on its screen, the flotilla's admiral divides it (docs/battle/admirals-orders.md O1c): he sends in its divisions
        var attacking = flotilla;
        for (double s = 0; s < 45 * 60 && double.IsNaN(fired); s += 60)
        {
            Run(w, 60);
            var f = w.Groups.Get(flotilla.GroupId)!;
            var units = f.IsForce ? f.Members.Select(Unit.Group).Prepend(flotilla) : [flotilla];
            if (double.IsNaN(sent) && units.FirstOrDefault(u => ai.Commanders.GivenOf(u).Given == TaskKind.Attack) is { Id: not 0 } d)
                (sent, attacking) = (s, d);
            for (long i = w.CombatLog.Written - seen - 1; i >= 0; i--)
                if (w.CombatLog.Newest((int)i) is { Kind: CombatEvent.Torpedoes } e && w.Ships.Store.SlotOf(e.Shooter) is int slot and >= 0 &&
                    w.Ships.Store[slot].Side == 0)
                    torpedoes += e.Count;
            seen = w.CombatLog.Written;
            if (torpedoes > 0)
                fired = s;
        }
        output.WriteLine($"sent in at {sent / 60:F0} min, {torpedoes} torpedoes by {fired / 60:F0} min; given {ai.Commanders.GivenOf(attacking)}, " +
                         $"on {ai.Tasks.Of(attacking)}; the line given {ai.Commanders.GivenOf(line)}");
        Assert.InRange(sent, 0, 2 * 60);   // the lines are in sight of each other from the start
        Assert.Equal(CommanderWhy.Battle, ai.Commanders.GivenOf(attacking).Why);
        Assert.True(torpedoes > 0);
        Assert.Equal(TaskKind.None, ai.Commanders.GivenOf(line).Given);   // in contact: doctrine's, not his
    }

    [Fact]
    public void Flotillas_are_kept_back_from_a_line_too_strong_for_them()
    {
        // four destroyers against four dreadnoughts by day: the defence's guns beat them off (docs/battle/command.md 5.1g)
        var (w, ai) = Make();
        int bb = Class(w, "dreadnought"), dd = Class(w, "destroyer");
        Column(w, ai, bb, Earth.Position(Lat, Lon), 180, 4, side: 0);
        var flotilla = Column(w, ai, dd, Off(270, 3_000), 180, 4, side: 0);
        Column(w, ai, bb, Off(90, 16_000), 180, 4, side: 1);
        w.Apply(ai.Orders.FireAtWill(flotilla));
        w.Apply(ai.Orders.Commander(0, CommanderMode.On));
        Run(w, 10 * 60);
        output.WriteLine($"the flotilla given {ai.Commanders.GivenOf(flotilla)}");
        Assert.NotEqual(TaskKind.Attack, ai.Commanders.GivenOf(flotilla).Given);
    }

    [Fact]
    public void An_outmatched_side_declines_and_goes_home()
    {
        // two battlecruisers against eight dreadnoughts: outmatched, and faster, so they can get away
        var (w, ai) = Make();
        int bb = Class(w, "dreadnought"), bc = Class(w, "lion");
        var mine = Column(w, ai, bc, Earth.Position(Lat, Lon), 0, 2, side: 1);
        Column(w, ai, bb, Off(90, 22_000), 0, 8, side: 0, kn: 12);
        var (hla, hlo) = LatLon(Off(270, 150_000));
        w.Apply(ai.Orders.Home(1, hla, hlo));
        w.Apply(ai.Orders.Commander(1, CommanderMode.On));
        var home = Earth.Position(hla, hlo);
        double Away() => Earth.Distance(w.Ships.Store[w.Ships.Store.SlotOf(mine.ShipOf(w.Groups))].Position, home);
        double before = Away();
        Run(w, 3 * 60);
        var phase = ai.Commanders.PhaseOf(1);
        var given = ai.Commanders.GivenOf(mine);
        Run(w, 60 * 60);
        output.WriteLine($"phase {phase}, given {given}; {before / 1000:F0} km from home, an hour on {Away() / 1000:F0} km");
        Assert.Equal((CommanderPhase.Retiring, CommanderReason.Outmatched), phase);
        Assert.Equal((TaskKind.Withdraw, CommanderWhy.Outmatched), (given.Given, given.Why));
        Assert.True(Away() < before - 20_000);
    }

    /// <summary>The decline holds (the AI's baseline, 2026-10-20: Japan at Tsushima searched back into the fleet it had
    /// declined every half hour for 15 hours): the fleet he declined out of sight, and then a light cruiser of theirs alone
    /// in sight, he neither searches for it nor closes the cruiser; he goes on home.</summary>
    [Fact]
    public void A_decline_holds_out_of_sight_and_against_a_part_of_the_enemy_seen_later()
    {
        var (w, ai) = Make();
        int bb = Class(w, "dreadnought"), bc = Class(w, "lion"), lc = Class(w, "light_cruiser");
        var mine = Column(w, ai, bc, Earth.Position(Lat, Lon), 0, 2, side: 1);
        var fleet = Column(w, ai, bb, Off(90, 22_000), 0, 8, side: 0, kn: 12);
        var (hla, hlo) = LatLon(Off(270, 150_000));
        w.Apply(ai.Orders.Home(1, hla, hlo));
        w.Apply(ai.Orders.Commander(1, CommanderMode.On));
        Run(w, 3 * 60);
        var declined = ai.Commanders.PhaseOf(1);
        foreach (int e in w.Groups.Get(fleet.GroupId)!.Line.ToArray())
            w.Apply(ShipSystem.Drop(e));   // out of sight
        Run(w, 10 * 60);
        var unseen = ai.Commanders.PhaseOf(1);
        var (la, lo) = LatLon(w.Ships.Store[w.Ships.Store.SlotOf(mine.ShipOf(w.Groups))].Position);
        var (cla, clo) = LatLon(Off(90, 12_000, la, lo));
        w.Apply(ShipSystem.Spawn(lc, cla, clo, 0, 15, 0));   // one of theirs, alone, that he could take on
        w.Apply(ai.Orders.HoldFire(Unit.Ship(w.Ships.NewestEntity)));
        w.Apply(ShipSystem.LookNow());
        var phases = new List<(CommanderPhase, CommanderReason)>();
        var givens = new List<TaskKind>();
        for (int m = 0; m < 30; m++)
        {
            Run(w, 60);
            phases.Add(ai.Commanders.PhaseOf(1));
            givens.Add(ai.Commanders.GivenOf(mine).Given);
        }
        output.WriteLine($"declined {declined}, out of sight {unseen}; with the cruiser: {string.Join(", ", phases.Distinct())}; given {string.Join(", ", givens.Distinct())}");
        Assert.Equal((CommanderPhase.Retiring, CommanderReason.Outmatched), declined);
        Assert.Equal((CommanderPhase.Retiring, CommanderReason.Outmatched), unseen);
        Assert.All(phases, p => Assert.Equal((CommanderPhase.Retiring, CommanderReason.Outmatched), p));
        Assert.All(givens, g => Assert.Equal(TaskKind.Withdraw, g));
    }

    [Fact]
    public void An_outmatched_side_that_cant_get_away_fights()
    {
        // two dreadnoughts against eight of their sisters: as fast as he is, so there's no escape (docs/battle/command.md 5.1g)
        var (w, ai) = Make();
        int bb = Class(w, "dreadnought");
        var mine = Column(w, ai, bb, Earth.Position(Lat, Lon), 0, 2, side: 1);
        Column(w, ai, bb, Off(90, 22_000), 0, 8, side: 0, kn: 12);
        var (hla, hlo) = LatLon(Off(270, 150_000));
        w.Apply(ai.Orders.Home(1, hla, hlo));
        w.Apply(ai.Orders.Commander(1, CommanderMode.On));
        Run(w, 3 * 60);
        var phase = ai.Commanders.PhaseOf(1);
        output.WriteLine($"phase {phase}, given {ai.Commanders.GivenOf(mine)}");
        Assert.NotEqual(CommanderPhase.Retiring, phase.Phase);
        Assert.NotEqual(TaskKind.Withdraw, ai.Commanders.GivenOf(mine).Given);
    }

    [Fact]
    public void A_side_that_loses_the_enemy_searches_where_it_was_then_gives_up_and_goes_home()
    {
        var w = new World(Start, 3, Settings with { SearchLegM = 8_000 });
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(12_000));
        int bb = Class(w, "dreadnought"), lc = Class(w, "light_cruiser");
        var mine = Column(w, ai, bb, Earth.Position(Lat, Lon), 90, 3, side: 1);
        var (ela, elo) = LatLon(Off(90, 10_000));
        w.Apply(ShipSystem.Spawn(lc, ela, elo, 90, 28, 0));   // a fast one, running from him out of sight
        w.Apply(ai.Orders.HoldFire(Unit.Ship(w.Ships.NewestEntity)));
        var (hla, hlo) = LatLon(Off(270, 60_000));
        w.Apply(ai.Orders.Home(1, hla, hlo));
        w.Apply(ShipSystem.LookNow());   // he sees her at once
        w.Apply(ai.Orders.Commander(1, CommanderMode.On));
        var seenPhases = new List<CommanderPhase>();
        var givens = new List<TaskKind>();
        for (double s = 0; s < 8 * 3600 && ai.Commanders.PhaseOf(1).Phase != CommanderPhase.Home; s += 60)
        {
            Run(w, 60);
            if (seenPhases.Count == 0 || seenPhases[^1] != ai.Commanders.PhaseOf(1).Phase)
                seenPhases.Add(ai.Commanders.PhaseOf(1).Phase);
            if (ai.Commanders.GivenOf(mine).Given is var g && g != TaskKind.None && (givens.Count == 0 || givens[^1] != g))
                givens.Add(g);
        }
        var given = ai.Commanders.GivenOf(mine);
        output.WriteLine($"phases {string.Join(" > ", seenPhases)}; given {string.Join(" > ", givens)}; at {w.Time}: {ai.Commanders.PhaseOf(1)}, given {given}");
        Assert.Equal([CommanderPhase.Battle, CommanderPhase.Contact, CommanderPhase.Lost, CommanderPhase.Home],
            seenPhases.Where(p => p is not (CommanderPhase.None or CommanderPhase.NoContact)).Distinct());
        Assert.Equal([TaskKind.Intercept, TaskKind.Search, TaskKind.Withdraw], givens);
        Assert.Equal((TaskKind.Withdraw, CommanderWhy.GaveUp), (given.Given, given.Why));
    }

    [Fact]
    public void Losses_past_doctrines_share_break_a_side_off()
    {
        var (w, ai) = Make();
        int bb = Class(w, "dreadnought");
        var mine = Column(w, ai, bb, Earth.Position(Lat, Lon), 0, 4, side: 1);
        Column(w, ai, bb, Off(90, 22_000), 0, 4, side: 0, kn: 12);
        var (hla, hlo) = LatLon(Off(270, 150_000));
        w.Apply(ai.Orders.Home(1, hla, hlo));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.BreakOffShare, 0.34));
        w.Apply(ai.Orders.Commander(1, CommanderMode.On));
        Run(w, 2 * 60);
        var before = ai.Commanders.PhaseOf(1);
        var line = w.Groups.Get(mine.GroupId)!.Line;
        int a = line[2], b = line[3];
        w.Apply(ShipSystem.Drop(a));   // two of his four gone
        w.Apply(ShipSystem.Drop(b));
        Run(w, 2 * 60);
        output.WriteLine($"before {before}, after {ai.Commanders.PhaseOf(1)}, given {ai.Commanders.GivenOf(mine)}");
        Assert.Equal(CommanderPhase.Battle, before.Phase);
        Assert.Equal((CommanderPhase.Retiring, CommanderReason.BrokenOff), ai.Commanders.PhaseOf(1));
        Assert.Equal((TaskKind.Withdraw, CommanderWhy.BrokenOff), (ai.Commanders.GivenOf(mine).Given, ai.Commanders.GivenOf(mine).Why));
    }

    [Theory]
    [InlineData(NightAction.Avoid)]
    [InlineData(NightAction.Accept)]
    public void By_night_heavy_ships_avoid_action_and_the_flotillas_go_in(NightAction night)
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 23), 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        int bb = Class(w, "dreadnought"), dd = Class(w, "destroyer");
        var mine = Column(w, ai, bb, Earth.Position(Lat, Lon), 180, 3, side: 1);
        var flotilla = Column(w, ai, dd, Off(90, 2_000), 180, 4, side: 1);
        Column(w, ai, bb, Off(90, 7_000), 180, 1, side: 0);   // a battleship the flotilla's attack is worth making on
        w.Apply(ai.Orders.FireAtWill(flotilla));
        var (hla, hlo) = LatLon(Off(270, 150_000));
        w.Apply(ai.Orders.Home(1, hla, hlo));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.NightAction, (double)night));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.NightAttack, 1));
        w.Apply(ShipSystem.LookNow());
        w.Apply(ai.Orders.Commander(1, CommanderMode.On));
        Run(w, 3 * 60);
        var phase = ai.Commanders.PhaseOf(1);
        output.WriteLine($"{night}: phase {phase}; the squadron given {ai.Commanders.GivenOf(mine)}, the flotilla {ai.Commanders.GivenOf(flotilla)}; " +
                         $"the sun {Light.SunElevationDeg(Earth.Position(Lat, Lon), w.Time):F1}°");
        if (night == NightAction.Avoid)
        {
            Assert.Equal(CommanderPhase.Night, phase.Phase);
            Assert.Equal((TaskKind.Withdraw, CommanderWhy.Night), (ai.Commanders.GivenOf(mine).Given, ai.Commanders.GivenOf(mine).Why));
            Assert.Equal((TaskKind.Attack, CommanderWhy.Night), (ai.Commanders.GivenOf(flotilla).Given, ai.Commanders.GivenOf(flotilla).Why));
        }
        else
        {
            Assert.Equal(CommanderPhase.Battle, phase.Phase);
            Assert.Equal(TaskKind.None, ai.Commanders.GivenOf(mine).Given);   // in contact: doctrine fights
        }
    }

    /// <summary>Declining by day doesn't keep his flotillas in by night (the user, 2026-10-20, after the decline was made
    /// to hold): two battlecruisers and a flotilla decline seventeen dreadnoughts at dusk and go home; once it's dark the
    /// flotilla, going home with them, is sent in (on the one apart from the others: against the sixteen the matchup has
    /// no attack worth making, by night as by day), and the battlecruisers go on home. Only a flotilla still in reach when
    /// it gets dark: one long gone home isn't sent back to look for him.</summary>
    [Fact]
    public void Declining_he_still_sends_his_flotillas_in_by_night()
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 21, 8), 3, Settings);   // a few minutes before it's dark (the sun at -6°)
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        int bb = Class(w, "dreadnought"), bc = Class(w, "lion"), dd = Class(w, "destroyer");
        var mine = Column(w, ai, bc, Earth.Position(Lat, Lon), 0, 2, side: 1);
        var flotilla = Column(w, ai, dd, Off(270, 2_000), 0, 4, side: 1);
        Column(w, ai, bb, Off(90, 12_000), 0, 16, side: 0, kn: 12);
        Column(w, ai, bb, Off(60, 7_000), 0, 1, side: 0, kn: 12);   // one apart: worth the flotilla's attack (the sixteen aren't)
        var (hla, hlo) = LatLon(Off(270, 150_000));
        w.Apply(ai.Orders.Home(1, hla, hlo));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.NightAttack, 1));
        w.Apply(ShipSystem.LookNow());
        w.Apply(ai.Orders.Commander(1, CommanderMode.On));
        Run(w, 2 * 60);
        var dusk = (ai.Commanders.PhaseOf(1), ai.Commanders.GivenOf(flotilla).Given, ai.Commanders.GivenOf(flotilla).Why);
        (TaskKind, CommanderWhy) sent = default;
        double sun = 0;
        for (int m = 0; m < 90 && sent.Item1 != TaskKind.Attack; m++)
        {
            Run(w, 60);
            var g = ai.Commanders.GivenOf(flotilla);
            sent = (g.Given, g.Why);
            sun = Light.SunElevationDeg(Earth.Position(Lat, Lon), w.Time);
        }
        output.WriteLine($"at dusk {dusk}; then the flotilla given {sent} at {w.Time} (the sun {sun:F1}°), the battlecruisers {ai.Commanders.GivenOf(mine)}");
        Assert.Equal((CommanderPhase.Retiring, CommanderReason.Outmatched), dusk.Item1);
        Assert.Equal((TaskKind.Withdraw, CommanderWhy.Outmatched), (dusk.Given, dusk.Why));
        Assert.Equal((TaskKind.Attack, CommanderWhy.Night), sent);
        Assert.True(sun < Settings.NightSunElevationDeg);
        Assert.Equal((CommanderPhase.Retiring, CommanderReason.Outmatched), ai.Commanders.PhaseOf(1));
        Assert.Equal(TaskKind.Withdraw, ai.Commanders.GivenOf(mine).Given);
    }

    [Fact]
    public void Battleships_set_as_a_screen_screen_the_main_body()
    {
        // the player's say over what a squadron is for (docs/battle/admirals-orders.md O6): four dreadnoughts set as a
        // screen screen the main body, and aren't the main body though they're as heavy
        var (w, ai) = Make();
        int bb = Class(w, "dreadnought");
        var main = Column(w, ai, bb, Earth.Position(Lat, Lon), 0, 4);
        var screen = Column(w, ai, bb, Off(270, 6_000), 0, 4);
        w.Apply(ai.Orders.Role(screen, UnitRole.Screen));
        w.Apply(ai.Orders.Commander(0, CommanderMode.On));
        foreach (var u in new[] { main, screen })
        {
            var guide = w.Ships.Store[w.Ships.Store.SlotOf(u.ShipOf(w.Groups))];
            var (la, lo) = LatLon(Off(0, 3_000, Earth.LatLon(guide.Position).Lat, Earth.LatLon(guide.Position).Lon));
            w.Apply(ai.Orders.GoTo(u, la, lo));   // a short plan, soon through
        }
        Run(w, 30 * 60);
        output.WriteLine($"main: given {ai.Commanders.GivenOf(main)}; screen: given {ai.Commanders.GivenOf(screen)}, on {ai.Tasks.Of(screen)}");
        Assert.Equal(TaskKind.Search, ai.Commanders.GivenOf(main).Given);
        Assert.Equal((TaskKind.Screen, main.Id), (ai.Commanders.GivenOf(screen).Given, ai.Commanders.GivenOf(screen).Target));
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
    /// <summary>A run (⇧Space, docs/map-ui/input.md T1) ends at the end of a plan the player gave by hand: not at a step
    /// with more to come, and then the clock is back at the run's speed, the fine tier no longer held.</summary>
    [Fact]
    public void A_run_ends_when_a_plan_given_by_hand_ends()
    {
        var (w, ai) = Make();
        w.Apply(Command.SetPlayerSide(0));
        var u = Column(w, ai, Class(w, "dreadnought"), Earth.Position(Lat, Lon), 0, 1);
        var (la, lo) = LatLon(Off(0, 8_000));
        var (lb, lb2) = LatLon(Off(0, 16_000));
        w.Apply(ai.Orders.ByHand.GoTo(u, la, lo, 18));
        w.Apply(UnitOrders.Then(ai.Orders.ByHand.GoTo(u, lb, lb2, 18)));
        w.Apply(Command.SetSpeed(w.Settings.MaxSpeed, holdFine: true, runBack: 10));
        double first = double.NaN, end = double.NaN;
        for (double s = 0; s < 2 * 3600 && double.IsNaN(end); s += 10)
        {
            Run(w, 10);
            if (double.IsNaN(first) && ai.Tasks.QueuedOf(u) == 0)
            {
                first = s;
                Assert.True(w.RunBack > 0, "the plan's first step ended the run");
            }
            if (w.RunBack == 0)
                end = s;
        }
        output.WriteLine($"the first step done at {first / 60:F0} min, the run ended at {end / 60:F0} min");
        Assert.InRange(end, first + 10 * 60, first + 30 * 60);   // the second 8 km at 18 kn
        Assert.Equal((10.0, false, false), (w.Speed, w.FineHeld, w.Paused));
    }

    /// <summary>A run meeting an encounter of the player's drops by its rule, to the run's speed or the rule's if slower,
    /// the run over: slowed to 100x, the run's 60x; paused, at the rule's 1x.</summary>
    [Fact]
    public void A_run_drops_by_the_encounter_rule()
    {
        var (w, ai) = Make();
        w.Apply(Command.SetPlayerSide(0));
        int bb = Class(w, "dreadnought");
        Column(w, ai, bb, Earth.Position(Lat, Lon), 90, 1);
        Column(w, ai, bb, Off(90, 40_000), 270, 1, side: 1);
        w.Apply(Command.SetInterest(InterestKind.Own, InterestAction.Slow, 100));
        w.Apply(Command.SetSpeed(w.Settings.MaxSpeed, holdFine: true, runBack: 60));
        for (int i = 0; i < 360 && w.RunBack > 0; i++)
            Run(w, 10);
        Assert.Equal((0.0, 60.0, false), (w.RunBack, w.Speed, w.Paused));
    }

    [Fact]
    public void A_run_paused_by_the_encounter_rule_is_at_its_speed()
    {
        var (w, ai) = Make();
        w.Apply(Command.SetPlayerSide(0));
        int bb = Class(w, "dreadnought");
        Column(w, ai, bb, Earth.Position(Lat, Lon), 90, 1);
        Column(w, ai, bb, Off(90, 40_000), 270, 1, side: 1);
        w.Apply(Command.SetSpeed(w.Settings.MaxSpeed, holdFine: true, runBack: 60));
        for (int i = 0; i < 360 && w.RunBack > 0; i++)
            Run(w, 10);
        Assert.Equal((0.0, 1.0, true), (w.RunBack, w.Speed, w.Paused));
    }
}
