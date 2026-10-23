using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The admiral's orders (docs/battle/admirals-orders.md): the split screen (O1b), a squadron told to screen
/// divided into two divisions, one on either side of the bearing it screens on, and only the division an attack comes at
/// counter-attacking; and Deploy as an order (O2). Clear weather, guns held all round (only the steering is measured),
/// and no line turns away.</summary>
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

    /// <summary>Four dreadnoughts in line ahead heading south at 15 kn (group 1, ships 1-4); four destroyers in a column a
    /// mile astern of them (ships 5-8), one squadron or two of two in a force, told to screen the line.</summary>
    static (World W, AiSystem Ai, int Line, int Screen) Make(bool force = false, double splitDeg = 0, string screenDesign = "destroyer")
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TurnAwayDeg, 0));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), dd = w.Ships.AddClass(Fleet.Get(screenDesign).Class!);
        var at = Earth.Position(56.0, 4.0);
        int line = Column(w, ai, bb, at, 180, 4, 15);
        int screen;
        if (!force)
            screen = Column(w, ai, dd, Off(at, 0, 3_400), 180, 4, 15);
        else
        {
            int a = Column(w, ai, dd, Off(at, 0, 3_400), 180, 2, 15), b = Column(w, ai, dd, Off(Off(at, 0, 3_400), 270, 1_000), 180, 2, 15);
            w.Apply(GroupSystem.Form(a, Formation.LineAbreast, 1_000));
            screen = w.Groups.Get(a)!.Parent;
            w.Apply(GroupSystem.EnterAlongside(b, a));
            w.Apply(ai.Orders.HoldFire(Unit.Group(screen)));
        }
        w.Apply(ai.Orders.Speed(Unit.Group(line), 15));
        w.Apply(ai.Orders.Screen(Unit.Group(screen), Unit.Group(line), splitDeg: splitDeg));
        return (w, ai, line, screen);
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

    [Fact]
    public void A_squadron_told_to_screen_is_divided_into_two_divisions()
    {
        var (w, _, _, screen) = Make();
        var f = w.Groups.Get(screen)!;
        Assert.True(f.IsForce);   // the same unit, its id kept, a force of two squadrons now
        Assert.Equal(2, f.Members.Count);
        Assert.Equal(new[] { 5, 6 }, w.Groups.Get(f.Members[0])!.Members);
        Assert.Equal(new[] { 7, 8 }, w.Groups.Get(f.Members[1])!.Members);
        Assert.Equal(w.Groups.Get(f.Members[0])!.Name + "I", w.Groups.Get(f.Members[1])!.Name);
    }

    [Fact]
    public void A_squadron_screening_splits_on_either_bow()
    {
        var (w, _, line, _) = Make();
        w.AdvanceTo(w.Time + 40 * 60_000);
        int guide = w.Groups.Get(line)!.Guide;
        var at = new (double Rel, double R)[4];
        for (int i = 0; i < 4; i++)
        {
            at[i] = Where(w, guide, 5 + i);
            output.WriteLine($"#{5 + i}: {at[i].Rel:F0}° off the line's bow, {at[i].R:F0} m");
        }
        // the first half (the screen's guide's) on one bow, the rest on the other, each about 45° off it and 5 km out
        Assert.True(Math.Sign(at[0].Rel) == Math.Sign(at[1].Rel) && Math.Sign(at[2].Rel) == Math.Sign(at[3].Rel) && Math.Sign(at[0].Rel) != Math.Sign(at[2].Rel));
        foreach (var (rel, r) in at)
        {
            Assert.InRange(Math.Abs(rel), 25, 70);
            Assert.InRange(r, 4_000, 6_000);
        }
    }

    [Fact]
    public void A_screen_in_one_body_keeps_its_formation_ahead()
    {
        var (w, _, line, _) = Make(splitDeg: -1);
        w.AdvanceTo(w.Time + 40 * 60_000);
        int guide = w.Groups.Get(line)!.Guide;
        for (int i = 0; i < 4; i++)
        {
            var (rel, r) = Where(w, guide, 5 + i);
            output.WriteLine($"#{5 + i}: {rel:F0}° off the line's bow, {r:F0} m");
            Assert.InRange(Math.Abs(rel), 0, 15);   // its column astern of its guide, dead ahead of the line
        }
    }

    [Fact]
    public void A_force_screening_splits_by_squadron()
    {
        var (w, _, line, screen) = Make(force: true);
        w.AdvanceTo(w.Time + 40 * 60_000);
        int guide = w.Groups.Get(line)!.Guide;
        var f = w.Groups.Get(screen)!;
        var sides = new List<int>();
        foreach (int sq in f.Line)
            foreach (int e in w.Groups.Get(sq)!.Members)
            {
                var (rel, r) = Where(w, guide, e);
                output.WriteLine($"squadron {sq}, #{e}: {rel:F0}° off the line's bow, {r:F0} m");
                Assert.InRange(Math.Abs(rel), 20, 75);
                Assert.InRange(r, 3_500, 6_500);
                sides.Add(Math.Sign(rel) * sq);
            }
        // each squadron on one bow, the two on opposite bows
        Assert.Equal(2, sides.Distinct().Count());
        Assert.NotEqual(Math.Sign(sides[0]), Math.Sign(sides[^1]));
    }

    [Fact]
    public void Only_the_division_the_attack_comes_at_counter_attacks()
    {
        var (w, ai, line, screen) = Make();
        w.AdvanceTo(w.Time + 30 * 60_000);   // on its stations on either bow
        int lineGuide = w.Groups.Get(line)!.Guide;
        var g = Ship(w, lineGuide);
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
        var f = w.Groups.Get(screen)!;
        var first = w.Groups.Get(f.Members[0])!.Members.ToArray();
        var ships = first.Concat(w.Groups.Get(f.Members[1])!.Members).ToArray();
        var fought = new HashSet<int>();
        for (int s = 10; s <= 25 * 60; s += 10)
        {
            w.AdvanceTo(w.Time + 10_000);
            foreach (int e in ships)
                if (ai.Captains.Of(e) is (CaptainOrder.Fight, var target, _) && target > 8)
                    fought.Add(e);
        }
        output.WriteLine($"the divisions {string.Join(", ", first)} and the rest; counter-attacked: {string.Join(", ", fought)}");
        Assert.NotEmpty(fought);
        bool one = fought.All(first.Contains), other = fought.All(e => !first.Contains(e));
        Assert.True(one ^ other, "both divisions counter-attacked");
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

    /// <summary>The screen's ships' bearings off the line's bow, at the end of a run with an enemy abeam.</summary>
    double[] ScreenAgainst(string screenDesign, string enemyDesign, int enemies)
    {
        var (w, ai, line, screen) = Make(screenDesign: screenDesign);
        w.AdvanceTo(w.Time + 30 * 60_000);   // on the bows
        EnemyAbeam(w, ai, line, enemyDesign, enemies);
        w.AdvanceTo(w.Time + 30 * 60_000);
        int guide = w.Groups.Get(line)!.Guide;
        var rel = new double[4];
        for (int i = 0; i < 4; i++)
        {
            double r;
            (rel[i], r) = Where(w, guide, 5 + i);
            output.WriteLine($"#{5 + i} ({screenDesign}): {rel[i]:F0}° off the line's bow, {r:F0} m");
        }
        return rel;
    }

    [Fact]
    public void Destroyers_screen_a_battle_line_from_its_disengaged_side()
    {
        // four destroyers no match for four dreadnoughts abeam to port: to starboard, one division ahead, one astern
        var rel = ScreenAgainst("destroyer", "dreadnought", 4);
        Assert.All(rel, r => Assert.InRange(r, 20, 170));
        Assert.True(rel.Count(r => r < 90) == 2 && rel.Count(r => r > 90) == 2, "a division at the van and one at the rear");
    }

    [Fact]
    public void A_screen_that_is_a_match_for_the_enemy_keeps_toward_him()
    {
        // four dreadnoughts screening against two light cruisers abeam to port: toward them
        var rel = ScreenAgainst("dreadnought", "light_cruiser", 2);
        Assert.All(rel, r => Assert.InRange(r, -170, -20));
    }

    [Fact]
    public void Roles_are_worked_out_from_the_ships_and_set_by_the_player()
    {
        var (w, ai, line, screen) = Make(splitDeg: -1);
        Assert.Equal(UnitRole.Heavy, ai.Roles.Of(Unit.Group(line)));
        Assert.Equal(UnitRole.Flotilla, ai.Roles.Of(Unit.Group(screen)));
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
