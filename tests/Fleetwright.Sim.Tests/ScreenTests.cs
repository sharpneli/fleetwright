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
            dd = w.Ships.AddClass(Fleet.Scenario("m_class"));
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
            var screen = Ship(w, w.Groups.GuideShip(w.Groups.Get(2)!));
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

    /// <summary>Where a destroyer screen keeps station off the line it screens against an enemy battle line, averaged over
    /// minutes 10-15: its guide's distance from the line's guide, m, and how far of it lies toward him (abeam, to the east),
    /// m: four dreadnoughts in line south at 15 kn, four M-class boats told to screen them, four enemy dreadnoughts in line
    /// on a parallel course <paramref name="apartM"/> to the east; guns held, in clear air, or (<paramref name="onScreen"/>)
    /// the enemy's line ordered to engage the screen's leader, his hits doing no damage.</summary>
    static (double Off, double Toward) ScreenOut(double apartM, bool onScreen = false)
    {
        var w = new World(Start, 3, Settings with { HitsDoDamage = false });
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), dd = w.Ships.AddClass(Fleet.Scenario("m_class"));
        void Line(double east, int cls, byte side, double northFrom)
        {
            var p = Earth.Position(56.0, 4.0);
            var f = Earth.Forward(p, 90);
            Earth.Advance(ref p, ref f, east);
            f = Earth.Forward(p, 0);
            Earth.Advance(ref p, ref f, northFrom);
            f = Earth.Forward(p, 180);
            for (int i = 0; i < 4; i++)
            {
                var (la, lo) = Earth.LatLon(p);
                w.Apply(ShipSystem.Spawn(cls, la, lo, 180, 15, side));
                Earth.Advance(ref p, ref f, 600);
            }
        }
        Line(0, bb, 0, 0);
        Line(0, dd, 0, -3_000);
        Line(apartM, bb, 1, 0);
        for (int g = 0; g < 3; g++)
        {
            w.Apply(GroupSystem.Create(1 + 4 * g, Formation.Column, g == 1 ? 400 : 600));
            for (int e = 2 + 4 * g; e <= 4 + 4 * g; e++)
                w.Apply(GroupSystem.Add(g + 1, e));
        }
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TurnAwayDeg, 0));
        // the default preset's floors: what the enemy's batteries would take (Content/Sim/doctrine.json)
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.HitFloor, 0.01));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.RoundFloor, 8.5e-6));
        for (int g = 1; g <= (onScreen ? 2 : 3); g++)
            w.Apply(ai.Orders.HoldFire(Unit.Group(g)));
        if (onScreen)
            w.Apply(ai.Orders.Engage(Unit.Group(3), 5));
        w.Apply(ai.Orders.Speed(Unit.Group(1), 15));
        w.Apply(ai.Orders.Course(Unit.Group(3), 180));
        w.Apply(ai.Orders.Speed(Unit.Group(3), 15));
        w.Apply(ai.Orders.Screen(Unit.Group(2), Unit.Group(1)));
        double sum = 0, off = 0;
        int n = 0;
        for (int s = 10; s <= 15 * 60; s += 10)
        {
            w.AdvanceTo(w.Time + 10_000);
            if (s < 10 * 60)
                continue;
            var line = Ship(w, 1);
            var screen = Ship(w, w.Groups.GuideShip(w.Groups.Get(2)!));
            (sum, off, n) = (sum + Vec3d.Dot(screen.Position - line.Position, Earth.Forward(line.Position, 90)), off + Earth.Distance(screen.Position, line.Position), n + 1);
        }
        return (off / n, sum / n);
    }

    [Fact]
    public void A_screen_keeps_out_of_the_enemy_lines_guns_and_on_his_side_of_its_own()
    {
        // docs/battle/balance.md BL5f (the user: "protect their own battleline... far enough to be safe(ish) but at minimum
        // on the enemy's side of the formation... if the lines are far enough the destroyers should be further to defend
        // against enemy torpedo attack"; "the destroyers should care more about enemy's secondaries... don't need to be too
        // scared of [the big guns], unless they actually start shooting at the destroyers"): out of his secondaries' reach
        // the screen takes its full distance toward him; nearer, it closes on its own line, never crossing to its far side;
        // with his big guns falling about it, it keeps out of theirs too
        var at = new[] { 22_000.0, 15_000, 12_000, 8_000 }.Select(d => (d, ScreenOut(d))).ToArray();
        foreach (var (d, (o, t)) in at)
            output.WriteLine($"the enemy line {d / 1000:0} km off: the screen {o:0} m off its line's guide, {t:0} m of it toward him");
        var shot = ScreenOut(15_000, onScreen: true);
        output.WriteLine($"15 km off, his big guns on it: {shot.Off:0} m off, {shot.Toward:0} m toward him");
        Assert.True(at[0].Item2.Off > 3_500, "out at its distance when he's far");
        Assert.True(at[^1].Item2.Off < 1_500, "close in when he's near");
        Assert.True(shot.Off < at[1].Item2.Off - 1_000 && shot.Toward > 0, "in closer under his big guns, still on his side");
        Assert.All(at, x => Assert.True(x.Item2.Toward > 0, "on his side of its own line"));
        for (int i = 1; i < at.Length; i++)
            Assert.True(at[i].Item2.Off <= at[i - 1].Item2.Off + 300, "no further out the nearer he is");
    }
}
