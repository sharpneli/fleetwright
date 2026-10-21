using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Falling back on the main body (docs/battle/command.md 5.1c): a squadron of two dreadnoughts told to fight six
/// turns away, as Fight does when outmatched; with its own battle fleet of eight 60 km to the north it runs for it (the
/// enemy to the south-west, straight away would be north-east). Alone it can't get away from ships as fast as itself,
/// so it fights (5.1g: no escape); two battlecruisers alone, faster, run straight away. Guns held.</summary>
public class FallBackTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 12);
    // clear air: these test their own rules; the smoke field's part in sight is SmokeSightTests', its allocation SmokeSystemTests'
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };

    static Vec3d Off(Vec3d from, double bearing, double m)
    {
        var f = Earth.Forward(from, bearing);
        Earth.Advance(ref from, ref f, m);
        return from;
    }

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

    static double RunCourse(bool support, string own = "dreadnought")
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), mine = w.Ships.AddClass(Fleet.Get(own).Class!);
        var at = Earth.Position(56.0, 4.0);
        int squadron = Column(w, ai, mine, at, 90, 2);
        if (support)
            Column(w, ai, bb, Off(at, 0, 60_000), 180, 8);
        Column(w, ai, bb, Off(at, 225, 18_000), 45, 6, side: 1);
        w.AdvanceTo(w.Time + 30_000);
        w.Apply(ai.Orders.Fight(Unit.Group(squadron)));
        w.AdvanceTo(w.Time + 6 * 60_000);
        return w.Ships.Store[w.Ships.Store.SlotOf(w.Groups.Get(squadron)!.Guide)].HeadingDeg;
    }

    static double Off(double a, double b)
    {
        double d = a - b;
        return Math.Abs(d - 360 * Math.Round(d / 360));
    }

    [Fact]
    public void Outmatched_it_runs_for_its_main_body_or_fights_if_it_cant_get_away()
    {
        double toSupport = RunCourse(support: true), alone = RunCourse(support: false), faster = RunCourse(support: false, own: "lion");
        output.WriteLine($"running with its fleet to the north it steers {toSupport:F0}°; alone {alone:F0}°; two battlecruisers alone {faster:F0}°");
        Assert.True(Off(toSupport, 0) < 25, $"{toSupport:F0}°");   // north, for the fleet
        Assert.True(Off(alone, 45) > 60, $"{alone:F0}°");          // no escape: it fights
        Assert.True(Off(faster, 45) < 30, $"{faster:F0}°");        // straight away from the south-west
    }
}
