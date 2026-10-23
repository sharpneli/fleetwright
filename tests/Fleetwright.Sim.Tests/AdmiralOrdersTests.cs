using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The admiral's orders (docs/battle/admirals-orders.md): the split screen (O1), half a screen on either side of
/// the bearing it screens on, and only the half an attack comes at counter-attacking; and Deploy as an order (O2). Clear
/// weather, guns held all round (only the steering is measured), and no line turns away.</summary>
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
    static (World W, AiSystem Ai, int Line, int Screen) Make(bool force = false, double splitDeg = 0)
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TurnAwayDeg, 0));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
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
    public void Only_the_half_the_attack_comes_at_counter_attacks()
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
        var halves = w.Groups.Get(screen)!.Line.ToArray();
        var fought = new HashSet<int>();
        for (int s = 10; s <= 25 * 60; s += 10)
        {
            w.AdvanceTo(w.Time + 10_000);
            foreach (int e in halves)
                if (ai.Captains.Of(e) is (CaptainOrder.Fight, var target, _) && target > 8)
                    fought.Add(e);
        }
        output.WriteLine($"the screen's line {string.Join(", ", halves)}; counter-attacked: {string.Join(", ", fought)}");
        Assert.NotEmpty(fought);
        bool first = fought.All(e => Array.IndexOf(halves, e) < 2), second = fought.All(e => Array.IndexOf(halves, e) >= 2);
        Assert.True(first ^ second, "both halves counter-attacked");
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
