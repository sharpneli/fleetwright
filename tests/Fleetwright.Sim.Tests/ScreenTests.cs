using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The screen and its counter-attack (docs/battle/command.md 5.1b): two light cruisers told to Screen a line of
/// battleships steaming south take their station off it toward destroyers sighted to the east, counter-attack them as
/// they close on the line, and go back to their station once they've gone by. Guns held all round (only the steering is
/// measured), and the line doesn't turn away.</summary>
public class ScreenTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    /// <summary>Ships 1-4 dreadnoughts in line ahead (group 1) heading south at 15 kn; ships 5-6 light cruisers (group 2)
    /// a mile astern of them, told to screen group 1; ships 7-10 destroyers (side 1) 14 km east of the line's guide,
    /// steaming west at 28 kn, through it.</summary>
    static (World W, AiSystem Ai) Make()
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), cl = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!),
            dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        var p = Earth.Position(56.0, 4.0);
        var f = Earth.Forward(p, 0);
        for (int i = 0; i < 6; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(i < 4 ? bb : cl, la, lo, 180, 15, 0));
            Earth.Advance(ref p, ref f, i == 3 ? 1_800 : 600);
        }
        w.Apply(GroupSystem.Create(1, Formation.Column, 600));
        for (int e = 2; e <= 4; e++)
            w.Apply(GroupSystem.Add(1, e));
        w.Apply(GroupSystem.Create(5, Formation.Column, 600));
        w.Apply(GroupSystem.Add(2, 6));
        p = Earth.Position(56.0, 4.0);
        f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 14_000);
        f = Earth.Forward(p, 180);
        Earth.Advance(ref p, ref f, 3_000);
        for (int i = 0; i < 4; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(dd, la, lo, 270, 28, 1));
            Earth.Advance(ref p, ref f, 400);
        }
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TurnAwayDeg, 0));
        w.Apply(ai.Orders.HoldFire(Unit.Group(1)));
        w.Apply(ai.Orders.HoldFire(Unit.Group(2)));
        for (int e = 7; e <= 10; e++)
        {
            w.Apply(ai.Orders.HoldFire(Unit.Ship(e)));
            w.Apply(ai.Orders.Course(Unit.Ship(e), 270));
        }
        w.Apply(ai.Orders.Speed(Unit.Group(1), 15));
        w.Apply(ai.Orders.Screen(Unit.Group(2), Unit.Group(1)));
        return (w, ai);
    }

    [Fact]
    public void A_screen_stations_toward_the_enemy_counter_attacks_and_comes_back()
    {
        var (w, ai) = Make();
        double eastMost = double.MinValue;
        bool counter = false, back = false;
        int fighter = 0;
        for (int s = 10; s <= 40 * 60; s += 10)
        {
            w.AdvanceTo(w.Time + 10_000);
            var line = Ship(w, 1);
            var screen = Ship(w, w.Groups.Get(2)!.Guide);
            if (s <= 5 * 60)
                eastMost = Math.Max(eastMost, Vec3d.Dot(screen.Position - line.Position, Earth.Forward(line.Position, 90)));
            // split, one of the two counter-attacks (docs/battle/admirals-orders.md O1)
            foreach (int e in new[] { 5, 6 })
            {
                var (order, target, _) = ai.Captains.Of(e);
                counter |= order == CaptainOrder.Fight && target >= 7;
                back |= counter && order == CaptainOrder.Station && e == fighter;
                if (order == CaptainOrder.Fight && target >= 7)
                    fighter = e;
            }
        }
        output.WriteLine($"the screen got {eastMost:F0} m east of the line's guide in its first 5 min; counter-attacked {counter}; back on station {back}");
        Assert.True(eastMost > 1_000, $"{eastMost:F0} m");   // from a mile astern of the line, out to the side the enemy is on
        Assert.True(counter, "never counter-attacked");
        Assert.True(back, "never went back to its station");
    }
}
