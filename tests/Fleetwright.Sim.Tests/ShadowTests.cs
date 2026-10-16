using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Captains on pictures (docs/battle/plan.md step 3.2f): an enemy is only what the navy's picture holds of it.
/// A shadower keeps a sighted enemy at a range inside sight and so keeps it in the picture; a captain told to shadow or
/// run from a ship his navy doesn't know of holds his course; running from a contact runs from where it's reckoned.</summary>
public class ShadowTests(ITestOutputHelper output)
{
    static readonly WorldTime Day = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    /// <summary>A light cruiser (side 1, ship 1) 30 km west of a dreadnought (side 2, ship 2) steaming north at 15 kn;
    /// both on the fine tick (an encounter: within 50 km). Visibility 25 km: not in sight yet.</summary>
    static (World W, AiSystem Ai) Scene()
    {
        var w = new World(Day, 6, Settings);
        var ai = new AiSystem(w);
        int cruiser = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(Command.SetWeather(25_000));
        var p = Earth.Position(56, 4);
        var f = Earth.Forward(p, 270);
        Earth.Advance(ref p, ref f, 30_000);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(cruiser, la, lo, 0, 15, 1));
        w.Apply(ShipSystem.Spawn(bb, 56, 4, 0, 15, 2));
        // the scene is about keeping station on an enemy, not fighting her: neither fires (a hit can sink either)
        w.Apply(ai.Orders.HoldFire(Unit.Ship(1)));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(2)));
        return (w, ai);
    }

    static bool Holds(World w, int side, int entity) => w.Sightings.Of(side)?.IndexOf(entity) is int i and >= 0;

    [Fact]
    public void A_captain_told_to_shadow_a_ship_his_navy_doesnt_know_holds_his_course()
    {
        var (w, ai) = Scene();
        Run(w, 5);
        Assert.False(Holds(w, 1, 2));
        w.Apply(ai.Orders.Shadow(Unit.Ship(1), Unit.Ship(2)));
        Run(w, 120);
        Assert.False(Holds(w, 1, 2), "30 km in a 25 km visibility");
        Assert.InRange(Ship(w, 1).CourseOrderDeg, -1, 1);
    }

    [Fact]
    public void A_shadower_closes_to_sight_then_keeps_the_enemy_at_the_range_and_in_the_picture()
    {
        var (w, ai) = Scene();
        // closing to find her: east until she's seen
        w.Apply(ShipSystem.Course(1, 90));
        while (!Holds(w, 1, 2))
            Run(w, 5);
        double found = Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position);
        w.Apply(ai.Orders.Shadow(Unit.Ship(1), Unit.Ship(2)));
        double range = Math.Clamp(Settings.ShadowVisibilityShare * 25_000, Settings.ShadowMinM, Settings.ShadowMaxM);
        Run(w, 1800);
        double worst = 0;
        for (int m = 0; m < 60; m++)
        {
            Run(w, 60);
            double d = Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position);
            worst = Math.Max(worst, Math.Abs(d - range));
            var pic = w.Sightings.Of(1)!;
            Assert.True(pic[pic.IndexOf(2)].InSight, $"lost her at {d / 1000:F1} km");
        }
        output.WriteLine($"found at {found / 1000:F1} km; shadowing at {range / 1000:F1} km, within {worst:F0} m of it for an hour");
        Assert.True(worst < 1500, $"{worst:F0} m off the range");
    }

    [Fact]
    public void Running_from_a_contact_runs_from_where_it_is_reckoned()
    {
        var (w, ai) = Scene();
        w.Apply(ShipSystem.Course(1, 90));
        while (!Holds(w, 1, 2))
            Run(w, 5);
        w.Apply(ai.Orders.Run(Unit.Ship(1), Unit.Ship(2)));
        Run(w, 5);
        var pic = w.Sightings.Of(1)!;
        var (est, _, _) = pic.EstimateAt(pic.IndexOf(2), w.Time.Ms, Settings);
        double away = Earth.Bearing(est, Ship(w, 1).Position);
        double off = Math.Abs(Ship(w, 1).CourseOrderDeg - away);
        off = Math.Min(off, 360 - off);
        Assert.True(off < 3, $"ordered {Ship(w, 1).CourseOrderDeg:F1}°, directly away {away:F1}°");
    }
}
