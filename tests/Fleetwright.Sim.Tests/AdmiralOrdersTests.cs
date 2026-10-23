using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The admiral's orders (docs/battle/admirals-orders.md): screens sharing an arc (O1c: one on the bearing
/// screened on, two on either bow, more spread evenly; a force hands Screen to its squadrons; only the screen nearest an
/// attack counter-attacks), a lone flotilla divided by its own admiral and never the player's, Divide and Merge, where
/// the arc sits (O5), roles (O6), the chain of command (O7), battle decisions down it (O7b) and Deploy as an order (O2). Clear weather, guns held all
/// round (only the steering is measured), and no line turns away.</summary>
public class AdmiralOrdersTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static Vec3d Off(Vec3d from, double bearing, double m)
    {
        var f = Earth.Forward(from, bearing);
        Earth.Advance(ref from, ref f, m);
        return from;
    }

    /// <summary>A column of n ships of a class from a point astern along a heading, 500 m apart, in a squadron; its id.</summary>
    static int Column(World w, AiSystem ai, int cls, Vec3d at, double heading, int n, double kn, byte side = 0)
    {
        int first = w.Ships.NewestEntity + 1;
        var p = at;
        for (int i = 0; i < n; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(cls, la, lo, heading, kn, side));
            p = Off(p, heading + 180, 500);
        }
        w.Apply(GroupSystem.Create(first, Formation.Column, 500));
        int g = w.Groups[w.Groups.Count - 1].GroupId;
        for (int e = first + 1; e < first + n; e++)
            w.Apply(GroupSystem.Add(g, e));
        w.Apply(ai.Orders.HoldFire(Unit.Group(g)));
        return g;
    }

    /// <summary>Four dreadnoughts in line ahead heading south at 15 kn (group 1, ships 1-4); from ship 5 on, a mile astern
    /// of them, <paramref name="squadrons"/> columns of <paramref name="each"/> ships 1 km apart side by side, each told to
    /// screen the line, or (<paramref name="force"/>) a force of them told to; by the player's hand if
    /// <paramref name="hand"/>. The squadrons in the order they were made.</summary>
    static (World W, AiSystem Ai, int Line, int[] Squadrons) Make(int squadrons = 1, int each = 4, bool force = false, bool hand = false,
        string screenDesign = "destroyer")
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TurnAwayDeg, 0));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), dd = w.Ships.AddClass(Fleet.Get(screenDesign).Class!);
        var at = Earth.Position(56.0, 4.0);
        int line = Column(w, ai, bb, at, 180, 4, 15);
        var sq = new int[squadrons];
        for (int k = 0; k < squadrons; k++)
            sq[k] = Column(w, ai, dd, Off(Off(at, 0, 3_400), 270, k * 1_000), 180, each, 15);
        w.Apply(ai.Orders.Speed(Unit.Group(line), 15));
        var o = hand ? ai.Orders.ByHand : ai.Orders;
        if (force)
        {
            w.Apply(GroupSystem.Form(sq[0], Formation.LineAbreast, 1_000));
            int f = w.Groups.Get(sq[0])!.Parent;
            for (int k = 1; k < squadrons; k++)
                w.Apply(GroupSystem.EnterAlongside(sq[k], sq[0]));
            w.Apply(ai.Orders.HoldFire(Unit.Group(f)));
            w.Apply(o.Screen(Unit.Group(f), Unit.Group(line)));
        }
        else
            foreach (int g in sq)
                w.Apply(o.Screen(Unit.Group(g), Unit.Group(line)));
        return (w, ai, line, sq);
    }

    /// <summary>A ship's bearing off the line guide's bow (+ to starboard), degrees, and her distance from it, m.</summary>
    static (double Rel, double R) Where(World w, int guide, int e)
    {
        var g = Ship(w, guide);
        var s = Ship(w, e);
        double rel = Earth.Bearing(g.Position, s.Position) - g.HeadingDeg;
        rel -= 360 * Math.Round(rel / 360);
        return (rel, Earth.Distance(g.Position, s.Position));
    }

    /// <summary>Where each group's guide ship lies off the line's bow, printed.</summary>
    (double Rel, double R)[] Guides(World w, int line, IEnumerable<int> groups)
    {
        int guide = w.Groups.Get(line)!.Guide;
        return groups.Select(g =>
        {
            var at = Where(w, guide, w.Groups.GuideShip(w.Groups.Get(g)!));
            output.WriteLine($"group {g} ({w.Groups.Get(g)!.Name}): {at.Rel:F0}° off the line's bow, {at.R:F0} m");
            return at;
        }).ToArray();
    }

    [Fact]
    public void A_lone_flotilla_is_divided_by_its_admiral_to_cover_both_bows()
    {
        var (w, _, line, sq) = Make();
        w.AdvanceTo(w.Time + 40 * 60_000);
        var f = w.Groups.Get(sq[0])!;
        Assert.True(f.IsForce);   // the same unit, its id kept, a force of two divisions now
        Assert.Equal(new[] { 5, 6 }, w.Groups.Get(f.Members[0])!.Members);
        Assert.Equal(new[] { 7, 8 }, w.Groups.Get(f.Members[1])!.Members);
        Assert.Equal(w.Groups.Get(f.Members[0])!.Name + "I", w.Groups.Get(f.Members[1])!.Name);
        var at = Guides(w, line, f.Members);
        Assert.True(Math.Sign(at[0].Rel) != Math.Sign(at[1].Rel), "both on one bow");
        foreach (var (rel, r) in at)
        {
            Assert.InRange(Math.Abs(rel), 30, 65);
            Assert.InRange(r, 4_000, 6_000);
        }
    }

    [Fact]
    public void The_players_screen_by_hand_is_not_divided()
    {
        // a squadron the player tells to screen keeps one station, on the bearing, its column astern of its guide
        var (w, _, line, sq) = Make(hand: true);
        w.AdvanceTo(w.Time + 40 * 60_000);
        Assert.False(w.Groups.Get(sq[0])!.IsForce);
        int guide = w.Groups.Get(line)!.Guide;
        for (int e = 5; e <= 8; e++)
        {
            var (rel, r) = Where(w, guide, e);
            output.WriteLine($"#{e}: {rel:F0}° off the line's bow, {r:F0} m");
            Assert.InRange(Math.Abs(rel), 0, 15);
        }
    }

    [Fact]
    public void Two_squadrons_screening_one_line_take_either_bow()
    {
        var (w, _, line, sq) = Make(squadrons: 2, each: 2);
        w.AdvanceTo(w.Time + 40 * 60_000);
        var at = Guides(w, line, sq);
        Assert.True(Math.Sign(at[0].Rel) != Math.Sign(at[1].Rel), "both on one bow");
        foreach (var (rel, r) in at)
        {
            Assert.InRange(Math.Abs(rel), 30, 65);
            Assert.InRange(r, 4_000, 6_000);
        }
    }

    [Fact]
    public void Three_squadrons_spread_over_the_arc()
    {
        var (w, _, line, sq) = Make(squadrons: 3, each: 2);
        w.AdvanceTo(w.Time + 45 * 60_000);
        var rel = Guides(w, line, sq).Select(a => a.Rel).Order().ToArray();
        Assert.InRange(rel[0], -60, -30);
        Assert.InRange(rel[1], -12, 12);
        Assert.InRange(rel[2], 30, 60);
    }

    [Fact]
    public void A_force_told_to_screen_hands_it_to_its_squadrons()
    {
        var (w, ai, line, sq) = Make(squadrons: 2, each: 2, force: true);
        w.AdvanceTo(w.Time + 40 * 60_000);
        foreach (int g in sq)
            Assert.Equal((CaptainOrder.Screen, Unit.Group(line).Id), (ai.Admirals.Of(g).Order, ai.Admirals.Of(g).Target));
        var at = Guides(w, line, sq);
        Assert.True(Math.Sign(at[0].Rel) != Math.Sign(at[1].Rel), "both on one bow");
        foreach (var (rel, r) in at)
            Assert.InRange(Math.Abs(rel), 30, 65);
    }

    [Fact]
    public void Divide_and_merge_keep_the_unit()
    {
        var (w, ai, _, sq) = Make(hand: true);
        int groups = w.Groups.Count;
        w.Apply(GroupSystem.Divide(sq[0], 1));   // the player splits off all but the guide
        var f = w.Groups.Get(sq[0])!;
        Assert.True(f.IsForce);
        Assert.Equal(new[] { 5 }, w.Groups.Get(f.Members[0])!.Members);
        Assert.Equal(new[] { 6, 7, 8 }, w.Groups.Get(f.Members[1])!.Members);
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(GroupSystem.Merge(sq[0]));
        Assert.False(f.IsForce);
        Assert.Equal(new[] { 5, 6, 7, 8 }, f.Members);
        Assert.Equal(groups, w.Groups.Count);   // the divisions gone
        Assert.Equal(CaptainOrder.Screen, ai.Admirals.Of(sq[0]).Order);   // its order kept
        w.AdvanceTo(w.Time + 60_000);
        Assert.False(f.IsForce);
    }

    [Fact]
    public void Only_the_screen_nearest_the_attack_counter_attacks()
    {
        var (w, ai, line, sq) = Make(squadrons: 2, each: 2);
        w.AdvanceTo(w.Time + 30 * 60_000);   // on their stations on either bow
        var g = Ship(w, w.Groups.Get(line)!.Guide);
        // four enemy destroyers 13 km east-south-east of the line's guide, steaming west at 28 kn, through it
        int dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        var p = Off(g.Position, 110, 13_000);
        for (int i = 0; i < 4; i++)
        {
            var (la, lo) = Earth.LatLon(Off(p, 180, i * 400));
            w.Apply(ShipSystem.Spawn(dd, la, lo, 270, 28, 1));
            int e = w.Ships.NewestEntity;
            w.Apply(ai.Orders.HoldFire(Unit.Ship(e)));
            w.Apply(ai.Orders.Course(Unit.Ship(e), 270));
        }
        var first = w.Groups.Get(sq[0])!.Members.ToArray();
        var ships = first.Concat(w.Groups.Get(sq[1])!.Members).ToArray();
        var fought = new HashSet<int>();
        bool both = false;
        for (int s = 10; s <= 25 * 60; s += 10)
        {
            w.AdvanceTo(w.Time + 10_000);
            var now = ships.Where(e => ai.Captains.Of(e) is (CaptainOrder.Fight, var target, _) && target > 8).ToArray();
            fought.UnionWith(now);
            both |= now.Any(first.Contains) && now.Any(e => !first.Contains(e));
        }
        output.WriteLine($"squadron {sq[0]}: {string.Join(", ", first)}; counter-attacked: {string.Join(", ", fought)}");
        Assert.NotEmpty(fought);
        Assert.False(both, "both screens counter-attacked at once");
        Assert.True(fought.All(first.Contains) || fought.All(e => !first.Contains(e)), "the counter-attack changed hands");
    }

    /// <summary>An enemy column of a design, n ships, 14 km on the port beam of the line's guide (east: the line steams
    /// south), steaming south with it, guns held.</summary>
    static void EnemyAbeam(World w, AiSystem ai, int line, string design, int n)
    {
        int cls = w.Ships.AddClass(Fleet.Get(design).Class!);
        var g = Ship(w, w.Groups.Get(line)!.Guide);
        var p = Off(g.Position, 90, 14_000);
        for (int i = 0; i < n; i++)
        {
            var (la, lo) = Earth.LatLon(Off(p, 0, i * 600));
            w.Apply(ShipSystem.Spawn(cls, la, lo, 180, 15, 1));
            int e = w.Ships.NewestEntity;
            w.Apply(ai.Orders.HoldFire(Unit.Ship(e)));
            w.Apply(ai.Orders.Course(Unit.Ship(e), 180));
            w.Apply(ai.Orders.Speed(Unit.Ship(e), 15));
        }
    }

    /// <summary>Two screening squadrons' guides' bearings off the line's bow, an hour after an enemy comes abeam.</summary>
    double[] ScreenAgainst(string screenDesign, string enemyDesign, int enemies)
    {
        var (w, ai, line, sq) = Make(squadrons: 2, each: 2, screenDesign: screenDesign);
        w.AdvanceTo(w.Time + 30 * 60_000);   // on the bows
        EnemyAbeam(w, ai, line, enemyDesign, enemies);
        w.AdvanceTo(w.Time + 60 * 60_000);   // dreadnoughts swing round the arc slowly
        return Guides(w, line, sq).Select(a => a.Rel).ToArray();
    }

    [Fact]
    public void Destroyers_screen_a_battle_line_from_its_disengaged_side()
    {
        // two pairs of destroyers no match for four dreadnoughts abeam to port: to starboard, one ahead, one astern
        var rel = ScreenAgainst("destroyer", "dreadnought", 4);
        Assert.All(rel, r => Assert.InRange(r, 20, 170));
        Assert.True(rel.Count(r => r < 90) == 1 && rel.Count(r => r > 90) == 1, "one at the van and one at the rear");
    }

    [Fact]
    public void A_screen_that_is_a_match_for_the_enemy_keeps_toward_him()
    {
        // two pairs of dreadnoughts screening against two light cruisers abeam to port: toward them
        var rel = ScreenAgainst("dreadnought", "light_cruiser", 2);
        Assert.All(rel, r => Assert.InRange(r, -170, -20));
    }

    [Fact]
    public void Roles_are_worked_out_from_the_ships_and_set_by_the_player()
    {
        var (w, ai, line, sq) = Make(hand: true);
        Assert.Equal(UnitRole.Heavy, ai.Roles.Of(Unit.Group(line)));
        Assert.Equal(UnitRole.Flotilla, ai.Roles.Of(Unit.Group(sq[0])));
        w.Apply(ai.Orders.Role(Unit.Group(line), UnitRole.Screen));   // four dreadnoughts told they're a screen
        Assert.Equal((UnitRole.Screen, true), (ai.Roles.Of(Unit.Group(line)), ai.Roles.IsSet(Unit.Group(line))));
        var s = new Snapshot();
        w.Fill(s, truth: true);
        int k = s.Groups.IndexOfGroup(line);
        Assert.Equal(((byte)UnitRole.Screen, true), (s.Groups.Role[k], s.Groups.RoleSet[k]));
        w.Apply(ai.Orders.Role(Unit.Group(line), UnitRole.None));   // worked out again
        Assert.Equal((UnitRole.Heavy, false), (ai.Roles.Of(Unit.Group(line)), ai.Roles.IsSet(Unit.Group(line))));
        Assert.Throws<ArgumentException>(() => w.Apply(ai.Orders.Role(Unit.Group(line), UnitRole.MainBody)));
    }

    /// <summary>A force heading south at 15 kn: a battle squadron of four dreadnoughts (its guide), two light cruisers and
    /// four destroyers, in line abreast; with doctrine's Dispose (or not). In a fleet (<paramref name="fleet"/>), the
    /// force is the fleet's guide and the light cruisers are the fleet's own squadron, not the force's.</summary>
    static (World W, AiSystem Ai, int Force, int Battle, int Cruisers, int Destroyers, int Fleet) Disposed(bool dispose = true, bool fleet = false)
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TurnAwayDeg, 0));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Dispose, dispose ? 1 : 0));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), cl = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!),
            dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        var at = Earth.Position(56.0, 4.0);
        int battle = Column(w, ai, bb, at, 180, 4, 15);
        int cruisers = Column(w, ai, cl, Off(at, 270, 2_000), 180, 2, 15);
        int destroyers = Column(w, ai, dd, Off(at, 270, 4_000), 180, 4, 15);
        w.Apply(GroupSystem.Form(battle, Formation.LineAbreast, 2_000));
        int force = w.Groups.Get(battle)!.Parent;
        w.Apply(GroupSystem.EnterAlongside(destroyers, battle));
        int outer = 0;
        if (fleet)
        {
            w.Apply(GroupSystem.Form(force, Formation.LineAbreast, 4_000));
            outer = w.Groups.Get(force)!.Parent;
            w.Apply(GroupSystem.EnterAlongside(cruisers, force));
        }
        else
            w.Apply(GroupSystem.EnterAlongside(cruisers, battle));
        int top = fleet ? outer : force;
        w.Apply(ai.Orders.HoldFire(Unit.Group(top)));
        w.Apply(ai.Orders.Speed(Unit.Group(top), 15));
        return (w, ai, force, battle, cruisers, destroyers, outer);
    }

    [Fact]
    public void A_force_disposes_its_scouts_ahead_and_its_flotilla_as_a_screen()
    {
        var (w, ai, force, battle, cruisers, destroyers, _) = Disposed();
        for (int min = 5; min <= 45; min += 5)
        {
            w.AdvanceTo(w.Time + 5 * 60_000);
            var c = Ship(w, w.Groups.GuideShip(w.Groups.Get(cruisers)!));
            var (rel0, r0) = Where(w, w.Groups.GuideShip(w.Groups.Get(force)!), c.Entity);
            output.WriteLine($"{min} min: cruisers {rel0:F0}°, {r0:F0} m, {c.Speed / 0.5144:F1} kn ordered {c.SpeedOrderKn:F1}; captain {ai.Captains.Of(c.Entity)}");
        }
        var f = w.Groups.Get(force)!;
        int guide = w.Groups.GuideShip(f);
        var (scoutRel, scoutR) = Where(w, guide, w.Groups.GuideShip(w.Groups.Get(cruisers)!));
        output.WriteLine($"the force's line: {string.Join(", ", f.Line)}; cruisers {ai.Tasks.Of(Unit.Group(cruisers))} at {scoutRel:F0}°, {scoutR:F0} m; " +
                         $"destroyers {ai.Tasks.Of(Unit.Group(destroyers))}, a force {w.Groups.Get(destroyers)!.IsForce}");
        Assert.Equal(new[] { battle }, f.Line);   // the main body alone in the line
        Assert.Equal((TaskKind.Scout, TaskState.OnTask), (ai.Tasks.Of(Unit.Group(cruisers)).Kind, ai.Tasks.Of(Unit.Group(cruisers)).Status));
        Assert.Equal(Unit.Group(force).Id, ai.Tasks.TargetOf(Unit.Group(cruisers)));
        Assert.InRange(Math.Abs(scoutRel), 0, 20);   // ahead of the force
        Assert.InRange(scoutR, 15_000, 25_000);
        Assert.Equal((TaskKind.Screen, Unit.Group(force).Id), (ai.Tasks.Of(Unit.Group(destroyers)).Kind, ai.Tasks.TargetOf(Unit.Group(destroyers))));
        Assert.True(w.Groups.Get(destroyers)!.IsForce);   // divided to screen
        foreach (int sq in w.Groups.Get(destroyers)!.Members)
        {
            var (rel, r) = Where(w, guide, w.Groups.GuideShip(w.Groups.Get(sq)!));
            output.WriteLine($"division {sq}: {rel:F0}°, {r:F0} m");
            Assert.InRange(Math.Abs(rel), 20, 75);
            Assert.InRange(r, 3_500, 6_500);
        }
    }

    [Fact]
    public void Without_doctrine_a_force_keeps_its_squadrons_in_its_line()
    {
        var (w, ai, force, _, cruisers, destroyers, _) = Disposed(dispose: false);
        w.AdvanceTo(w.Time + 20 * 60_000);
        Assert.Equal(3, w.Groups.Get(force)!.Line.Count);
        Assert.Equal(TaskKind.None, ai.Tasks.Of(Unit.Group(cruisers)).Kind);
        Assert.Equal(TaskKind.None, ai.Tasks.Of(Unit.Group(destroyers)).Kind);
    }

    [Fact]
    public void The_players_hand_on_a_member_holds_against_its_admirals_disposition()
    {
        var (w, ai, _, _, cruisers, _, _) = Disposed();
        w.AdvanceTo(w.Time + 5 * 60_000);
        w.Apply(ai.Orders.ByHand.Course(Unit.Group(cruisers), 90));   // the player sends the cruisers east
        w.AdvanceTo(w.Time + 30 * 60_000);
        var g = Ship(w, w.Groups.GuideShip(w.Groups.Get(cruisers)!));
        output.WriteLine($"the cruisers on {ai.Tasks.Of(Unit.Group(cruisers))}, steering {g.HeadingDeg:F0}°");
        Assert.NotEqual(TaskKind.Scout, ai.Tasks.Of(Unit.Group(cruisers)).Kind);
        Assert.InRange(g.HeadingDeg, 80, 100);
    }

    [Fact]
    public void A_fleet_of_forces_is_disposed_level_by_level()
    {
        // the fleet's admiral sends his cruisers to scout; the battle force's own admiral screens it with his destroyers
        var (w, ai, force, _, cruisers, destroyers, fleet) = Disposed(fleet: true);
        w.AdvanceTo(w.Time + 30 * 60_000);
        output.WriteLine($"cruisers {ai.Tasks.Of(Unit.Group(cruisers))} of {ai.Tasks.TargetOf(Unit.Group(cruisers))}; " +
                         $"destroyers {ai.Tasks.Of(Unit.Group(destroyers))} of {ai.Tasks.TargetOf(Unit.Group(destroyers))}");
        Assert.Equal((TaskKind.Scout, Unit.Group(fleet).Id), (ai.Tasks.Of(Unit.Group(cruisers)).Kind, ai.Tasks.TargetOf(Unit.Group(cruisers))));
        Assert.Equal((TaskKind.Screen, Unit.Group(force).Id), (ai.Tasks.Of(Unit.Group(destroyers)).Kind, ai.Tasks.TargetOf(Unit.Group(destroyers))));
        Assert.Equal(new[] { force }, w.Groups.Get(fleet)!.Line);
    }

    /// <summary>Torpedoes a side has fired since the log stood at <paramref name="seen"/>; the log's mark now.</summary>
    static (int Fired, long Seen) Torpedoes(World w, byte side, long seen)
    {
        int n = 0;
        for (long i = w.CombatLog.Written - seen - 1; i >= 0; i--)
            if (w.CombatLog.Newest((int)i) is { Kind: CombatEvent.Torpedoes } e && w.Ships.Store.SlotOf(e.Shooter) is int slot and >= 0 &&
                w.Ships.Store[slot].Side == side)
                n += e.Count;
        return (n, w.CombatLog.Written);
    }

    [Fact]
    public void A_force_sends_in_its_own_flotilla_on_a_line_its_battle_squadron_engages()
    {
        // the force of O7 (dreadnoughts, light cruisers, destroyers screening), one enemy dreadnought 14 km on its beam:
        // the force's admiral sends his destroyers in; the side's commander, on, leaves what's inside the force alone
        var (w, ai, force, _, _, destroyers, _) = Disposed();
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        var guide = Ship(w, w.Groups.GuideShip(w.Groups.Get(force)!));
        Column(w, ai, bb, Off(guide.Position, 90, 14_000), 180, 1, 15, side: 1);
        w.Apply(ai.Orders.FireAtWill(Unit.Group(destroyers)));
        w.Apply(ai.Orders.Commander(0, CommanderMode.On));
        long seen = w.CombatLog.Written;
        int fired = 0;
        double sent = double.NaN;
        for (int min = 1; min <= 40 && fired == 0; min++)
        {
            w.AdvanceTo(w.Time + 60_000);
            if (double.IsNaN(sent) && ai.Tasks.Of(Unit.Group(destroyers)).Kind == TaskKind.Attack)
                sent = min;
            (int n, seen) = Torpedoes(w, 0, seen);
            fired += n;
        }
        var d = w.Groups.Get(destroyers)!;
        output.WriteLine($"sent in at {sent} min, {fired} torpedoes; the destroyers on {ai.Tasks.Of(Unit.Group(destroyers))}, a force {d.IsForce}; " +
                         $"the commander gave them {ai.Commanders.GivenOf(Unit.Group(destroyers))}, the force {ai.Commanders.GivenOf(Unit.Group(force))}; " +
                         $"phase {ai.Commanders.PhaseOf(0)}");
        Assert.InRange(sent, 1, 5);
        Assert.True(fired > 0);
        Assert.Equal(TaskKind.None, ai.Commanders.GivenOf(Unit.Group(destroyers)).Given);   // its admiral's, not the commander's
        if (d.IsForce)
            foreach (int m in d.Members)
                Assert.Equal(TaskKind.None, ai.Commanders.GivenOf(Unit.Group(m)).Given);
    }

    [Fact]
    public void A_force_told_to_attack_hands_it_to_its_divisions()
    {
        // a flotilla divided into two divisions, told by hand to attack a dreadnought: each division runs in under its own
        // admiral, and both fire
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        var at = Earth.Position(56.0, 4.0);
        int flotilla = Column(w, ai, dd, at, 180, 4, 20);
        int enemy = Column(w, ai, bb, Off(at, 90, 14_000), 180, 1, 15, side: 1);
        w.Apply(GroupSystem.Divide(flotilla, 2));
        var f = w.Groups.Get(flotilla)!;
        Assert.True(f.IsForce);
        int target = w.Groups.GuideShip(w.Groups.Get(enemy)!);
        w.Apply(ai.Orders.FireAtWill(Unit.Group(flotilla)));
        w.Apply(ai.Orders.ByHand.Attack(Unit.Group(flotilla), target));
        long seen = w.CombatLog.Written;
        var firedBy = new HashSet<int>();
        var phases = new HashSet<(int, AttackPhase)>();
        for (int min = 1; min <= 40; min++)
        {
            w.AdvanceTo(w.Time + 60_000);
            foreach (int m in f.Members)
                phases.Add((m, ai.Admirals.AttackOf(m).Phase));
            for (long i = w.CombatLog.Written - seen - 1; i >= 0; i--)
                if (w.CombatLog.Newest((int)i) is { Kind: CombatEvent.Torpedoes } e && w.Groups.Of(e.Shooter) is { } sq)
                    firedBy.Add(sq.GroupId);
            seen = w.CombatLog.Written;
        }
        output.WriteLine($"divisions' phases {string.Join(", ", phases.Where(p => p.Item2 != AttackPhase.None))}; fired by {string.Join(", ", firedBy)}; " +
                         $"the force on {ai.Tasks.Of(Unit.Group(flotilla))}");
        foreach (int m in f.Members)
        {
            Assert.Contains((m, AttackPhase.Run), phases);   // each division ran in itself
            Assert.Contains(m, firedBy);
        }
    }

    [Fact]
    public void A_force_deploys_on_the_course_ordered()
    {
        var (w, ai, force, _) = DeploymentTests.Make(deploy: false);   // doctrine doesn't deploy it: the order does
        w.Apply(ai.Orders.ByHand.Deploy(Unit.Group(force), 90));
        var f = w.Groups.Get(force)!;
        double deployed = -1;
        bool began = false;
        for (int s = 1; s <= 45 * 60 && deployed < 0; s++)
        {
            w.AdvanceTo(w.Time + 1_000);
            began |= ai.Admirals.Deploying(force);
            if (began && !ai.Admirals.Deploying(force))
                deployed = s;
        }
        var guide = Ship(w, w.Groups.GuideShip(f));
        output.WriteLine($"in one line after {deployed / 60:F1} min, steering {guide.HeadingDeg:F0}°");
        Assert.True(began, "never deployed");
        Assert.InRange(deployed / 60, 3, 30);
        Assert.Equal(Formation.Column, f.Formation);
        Assert.InRange(guide.HeadingDeg, 80, 100);
    }

    [Fact]
    public void Deployed_by_order_without_a_course_it_crosses_the_enemy_s()
    {
        var (w, ai, force, enemy) = DeploymentTests.Make(deploy: false);
        w.AdvanceTo(w.Time + 60_000);   // the enemy in sight
        w.Apply(ai.Orders.ByHand.Deploy(Unit.Group(force)));
        w.AdvanceTo(w.Time + 30 * 60_000);
        var f = w.Groups.Get(force)!;
        var guide = Ship(w, w.Groups.GuideShip(f));
        var van = Ship(w, w.Groups.Get(enemy)!.Guide);
        double off = guide.HeadingDeg - van.HeadingDeg;
        double across = Math.Abs(Math.Abs(off - 360 * Math.Round(off / 360)) - 90);
        output.WriteLine($"the line steers {guide.HeadingDeg:F0}°, the enemy {van.HeadingDeg:F0}°");
        Assert.Equal(Formation.Column, f.Formation);
        Assert.True(across < 30, $"{across:F0}° from square across his course");
    }
}
