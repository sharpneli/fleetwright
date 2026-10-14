using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The scouting tasks (docs/battle/command.md 5.1c): a light cruiser scouting ahead of a battle squadron keeps
/// her station, and on meeting battlecruisers shadows them rather than fight (doctrine says fight); a squadron of light
/// cruisers searching an area spreads into a scouting line and finds an enemy crossing it in most seeds; a squadron
/// covering another keeps station toward the enemy it knows of. Clear weather, guns held.</summary>
public class ScoutingTests(ITestOutputHelper output)
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

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    [Fact]
    public void A_scout_keeps_ahead_and_shadows_what_it_cannot_fight()
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Engage, (double)Engage.Fight));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), lc = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        var home = Earth.Position(Lat, Lon);
        int squadron = Column(w, ai, bb, home, 0, 3);
        var (sla, slo) = Earth.LatLon(Off(home, 0, 5_000));
        w.Apply(ShipSystem.Spawn(lc, sla, slo, 0, 15));
        int scout = w.Ships.NewestEntity;
        w.Apply(ai.Orders.HoldFire(Unit.Ship(scout)));
        w.Apply(ai.Orders.Scout(Unit.Ship(scout), Unit.Group(squadron)));
        w.AdvanceTo(w.Time + 90 * 60_000);   // closing 15 km at the leader's speed and FollowMaxSpeedDiffKn more
        var guide = Ship(w, w.Groups.Get(squadron)!.Guide);
        double ahead = Vec3d.Dot(Ship(w, scout).Position - guide.Position, guide.Forward);
        output.WriteLine($"after 90 min the scout is {ahead:F0} m ahead of the squadron");
        Assert.InRange(ahead, Settings.ScoutM - 3_000, Settings.ScoutM + 3_000);

        // battlecruisers 20 km ahead of the scout, coming south
        Column(w, ai, bb, Off(Ship(w, scout).Position, 0, 20_000), 180, 3, side: 1, kn: 18);
        for (int m = 0; m < 10; m++)
            w.AdvanceTo(w.Time + 60_000);
        output.WriteLine($"then: {ai.Tasks.Of(Unit.Ship(scout))}, her captain {ai.Captains.Of(scout).Order}");
        Assert.Equal(TaskState.InContact, ai.Tasks.Of(Unit.Ship(scout)).Status);
        Assert.Equal(CaptainOrder.Shadow, ai.Captains.Of(scout).Order);
    }

    [Fact]
    public void A_scouting_line_finds_an_enemy_crossing_its_area_in_most_seeds()
    {
        int found = 0;
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var w = new World(Start, seed, Settings);
            var ai = new AiSystem(w);
            w.Apply(Command.SetWeather(15_000));
            int lc = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!), bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
            var home = Earth.Position(Lat, Lon);
            int scouts = Column(w, ai, lc, home, 0, 4, kn: 20);
            var datum = Off(home, 0, 60_000);
            var (dla, dlo) = Earth.LatLon(datum);
            w.Apply(ai.Orders.Search(Unit.Group(scouts), dla, dlo));
            // an enemy squadron crossing the area east to west, somewhere north or south of the datum by the seed
            var crossing = Off(Off(datum, 0, (int)seed * 7_000 - 21_000), 90, 60_000);
            Column(w, ai, bb, crossing, 270, 2, side: 1, kn: 12);
            double s;
            for (s = 0; s < 6 * 3600 && ai.Tasks.Of(Unit.Group(scouts)).Status == TaskState.OnTask; s += 60)
                w.AdvanceTo(w.Time + 60_000);
            var (kind, state, why) = ai.Tasks.Of(Unit.Group(scouts));
            var g = w.Groups.Get(scouts)!;
            output.WriteLine($"seed {seed}: {state} {why} after {s / 60:F0} min; the line {g.Formation} {g.SpacingM:F0} m apart");
            Assert.Equal(Formation.LineAbreast, g.Formation);
            if (why == TaskReason.InContact)
                found++;
        }
        Assert.True(found >= 3, $"found in {found} of 5");
    }

    [Fact]
    public void A_cover_keeps_toward_the_enemy()
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), lc = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        var home = Earth.Position(Lat, Lon);
        int cruisers = Column(w, ai, lc, home, 0, 2);
        int battle = Column(w, ai, bb, Off(home, 180, 15_000), 0, 2);
        Column(w, ai, bb, Off(home, 90, 22_000), 0, 2, side: 1, kn: 15);   // the enemy, in sight to the east
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(ai.Orders.Cover(Unit.Group(battle), Unit.Group(cruisers), 10_000));
        w.AdvanceTo(w.Time + 40 * 60_000);
        var covered = Ship(w, w.Groups.Get(cruisers)!.Guide);
        var cover = Ship(w, w.Groups.Get(battle)!.Guide);
        double east = Vec3d.Dot(cover.Position - covered.Position, Earth.Forward(covered.Position, 90));
        output.WriteLine($"after 40 min the cover is {east:F0} m east of the cruisers: {ai.Tasks.Of(Unit.Group(battle))}");
        Assert.True(east > 5_000, $"{east:F0} m");
    }
}
