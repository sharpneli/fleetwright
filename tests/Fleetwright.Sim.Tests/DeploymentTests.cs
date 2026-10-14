using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The tactical picture and the deployment (docs/battle/command.md 5.1c): the enemy a navy sees falls into its
/// groups, each with its strength and van; a force of four columns abreast (the Grand Fleet's cruising order, in
/// little) that meets the enemy's battle line deploys into one line ahead across his course, ahead of him (his T crossed),
/// led by the column furthest along it, in about the time Jellicoe's took. Clear weather, guns held, nobody takes up a
/// fight.</summary>
public class DeploymentTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 12);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.0, Lon = 4.0;

    static Vec3d Off(Vec3d from, double bearing, double m)
    {
        var f = Earth.Forward(from, bearing);
        Earth.Advance(ref from, ref f, m);
        return from;
    }

    static int Column(World w, AiSystem ai, int cls, Vec3d at, double heading, int n, byte side = 0, double kn = 14)
    {
        int first = w.Ships.NewestEntity + 1;
        var f = Earth.Forward(at, heading + 180);
        var p = at;
        for (int i = 0; i < n; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(cls, la, lo, heading, kn, side));
            Earth.Advance(ref p, ref f, 500);
        }
        w.Apply(GroupSystem.Create(first, Formation.Column, 500));
        int g = w.Groups[w.Groups.Count - 1].GroupId;
        for (int e = first + 1; e < first + n; e++)
            w.Apply(GroupSystem.Add(g, e));
        w.Apply(ai.Orders.HoldFire(Unit.Group(g)));
        return g;
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    /// <summary>Four columns of three dreadnoughts a mile apart abreast, heading south-east at 14 kn; an enemy line of six
    /// steaming north, its van 26 km to the south-south-west of the force's guide.</summary>
    static (World W, AiSystem Ai, int Force, int Enemy) Make(bool deploy)
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Deploy, deploy ? 1 : 0));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        var at = Earth.Position(Lat, Lon);
        int first = Column(w, ai, bb, at, 135, 3);
        w.Apply(GroupSystem.Form(first, Formation.LineAbreast, 1_850));
        int force = w.Groups.Get(first)!.Parent;
        for (int k = 1; k < 4; k++)
        {
            // abeam to starboard of a south-east course: south-west
            int g = Column(w, ai, bb, Off(at, 225, k * 1_850), 135, 3);
            w.Apply(GroupSystem.Enter(force, g));
        }
        w.Apply(ai.Orders.Speed(Unit.Group(force), 14));
        int enemy = Column(w, ai, bb, Off(at, 200, 26_000), 0, 6, side: 1, kn: 15);
        return (w, ai, force, enemy);
    }

    [Fact]
    public void The_enemy_falls_into_groups_with_their_strength_and_van()
    {
        var (w, ai, _, _) = Make(deploy: false);
        w.AdvanceTo(w.Time + 60_000);
        var groups = ai.Tactical.Of(w, 0);
        Assert.Equal(1, groups.Length);
        var g = groups[0];
        output.WriteLine($"one group of {g.Count}, {g.Capitals} capital, strength {g.Strength:F1}; its van #{g.Van}");
        Assert.Equal(6, g.Count);
        Assert.True(g.Strength > 6 * 4, $"{g.Strength:F1}");   // six dreadnoughts' 12" guns, at least
        Assert.Equal(w.Groups.Get(w.Groups.Count)!.Guide, g.Van);   // the enemy column's guide leads it north
    }

    [Fact]
    public void Four_columns_deploy_into_one_line_across_the_enemy_van_s_course()
    {
        var (w, ai, force, enemy) = Make(deploy: true);
        var f = w.Groups.Get(force)!;
        double deployed = -1, began = -1;
        for (int s = 1; s <= 45 * 60 && deployed < 0; s++)
        {
            w.AdvanceTo(w.Time + 1_000);
            if (began < 0 && ai.Admirals.Deploying(force))
                began = s;
            if (began >= 0 && deployed < 0 && !ai.Admirals.Deploying(force))
                deployed = s;
        }
        // formed: where the line lies on the enemy van
        var guide = Ship(w, w.Groups.GuideShip(f));
        var van = Ship(w, w.Groups.Get(enemy)!.Guide);
        var rel = guide.Position - van.Position;
        double offHisBow = Math.Acos(Vec3d.Dot(rel, van.Forward) / rel.Length) * 180 / Math.PI;
        double r = Earth.Distance(guide.Position, van.Position);
        output.WriteLine($"began deploying {began / 60:F1} min in, in one line {(deployed - began) / 60:F1} min later; the line steers {guide.HeadingDeg:F0}°, its guide {offHisBow:F0}° off the enemy van's bow at {r:F0} m");
        Assert.True(began > 0, "never deployed");
        Assert.True(deployed > began, "never formed one line");
        Assert.InRange((deployed - began) / 60, 5, 30);   // Jellicoe's took about twenty minutes
        Assert.Equal(Formation.Column, f.Formation);
        Assert.True(offHisBow < 60, $"{offHisBow:F0}° off his bow");   // across his way, ahead of him
        Assert.True(r < 30_000, $"{r:F0} m");
    }

    [Fact]
    public void Without_doctrine_it_keeps_its_columns()
    {
        var (w, ai, force, _) = Make(deploy: false);
        w.AdvanceTo(w.Time + 30 * 60_000);
        Assert.Equal(Formation.LineAbreast, w.Groups.Get(force)!.Formation);
    }
}
