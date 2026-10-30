using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The turn-away from torpedoes (docs/battle/command.md 5.1b): a line of battleships steaming south that sees
/// destroyers closing from the east turns away together, to starboard, through doctrine's points, and back to its course
/// once they've gone by; with no TurnAwayDeg it holds on, and so it does on the player's helm order (docs/map-ui/battle-hud.md
/// decision 6) and steaming at boats lying still ahead (only their own run in is an attack). Guns held both ways, so
/// nothing but the steering is measured.</summary>
public class TurnAwayTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>Four dreadnoughts (side 0) in line ahead heading south at 15 kn; four destroyers (side 1) 12 km to the east
    /// of the guide, abreast, steaming west at 28 kn, straight at the line and through it (or, <paramref name="lying"/>,
    /// stopped 7 km ahead of the guide and 1.5 km to the east of his track). <paramref name="hand"/>: the line's speed
    /// is the player's helm order, by hand.</summary>
    static (World W, AiSystem Ai) Make(double turnAwayDeg, bool hand = false, bool lying = false)
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TurnAwayDeg, turnAwayDeg));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), dd = w.Ships.AddClass(Fleet.Scenario("m_class"));
        var p = Earth.Position(56.0, 4.0);
        var f = Earth.Forward(p, 0);
        for (int i = 0; i < 4; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(bb, la, lo, 180, 15, 0));
            Earth.Advance(ref p, ref f, 600);
        }
        w.Apply(GroupSystem.Create(1, Formation.Column, 600));
        for (int e = 2; e <= 4; e++)
            w.Apply(GroupSystem.Add(1, e));
        p = Earth.Position(56.0, 4.0);
        f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, lying ? 1_500 : 12_000);
        f = Earth.Forward(p, 180);
        Earth.Advance(ref p, ref f, lying ? 7_000 : 2_000);
        for (int i = 0; i < 4; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(dd, la, lo, 270, lying ? 0 : 28, 1));
            Earth.Advance(ref p, ref f, 400);
        }
        w.Apply(ai.Orders.HoldFire(Unit.Group(1)));
        for (int e = 5; e <= 8; e++)
        {
            w.Apply(ai.Orders.HoldFire(Unit.Ship(e)));
            w.Apply(lying ? ai.Orders.Speed(Unit.Ship(e), 0) : ai.Orders.Course(Unit.Ship(e), 270));
        }
        w.Apply((hand ? ai.Orders.ByHand : ai.Orders).Speed(Unit.Group(1), 15));
        return (w, ai);
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    [Fact]
    public void A_line_turns_away_from_destroyers_closing_and_back_once_they_are_past()
    {
        var (w, _) = Make(45);
        double most = 0, back = -1;
        for (int s = 10; s <= 30 * 60; s += 10)
        {
            w.AdvanceTo(w.Time + 10_000);
            double h = Ship(w, w.Groups[0].Guide).HeadingDeg;
            most = Math.Max(most, h - 180);
            if (most > 40 && back < 0 && Math.Abs(h - 180) < 3)
                back = s;
        }
        output.WriteLine($"the line turned away to {180 + most:F0}° and was back on 180° at {back / 60:F1} min");
        Assert.InRange(most, 40, 50);
        Assert.True(back > 0, "never turned back");
        Assert.Equal(Formation.Column, w.Groups[0].Formation);
    }

    /// <summary>The guide's largest turn off 180° over the first 15 minutes, degrees.</summary>
    static double MostOff(World w)
    {
        double most = 0;
        for (int s = 10; s <= 15 * 60; s += 10)
        {
            w.AdvanceTo(w.Time + 10_000);
            double d = Ship(w, 1).HeadingDeg - 180;
            most = Math.Max(most, Math.Abs(d - 360 * Math.Round(d / 360)));
        }
        return most;
    }

    [Fact]
    public void Without_doctrine_it_holds_on()
    {
        double most = MostOff(Make(0).W);
        Assert.True(most < 5, $"{most:F0}°");
    }

    [Fact]
    public void On_the_player_s_helm_it_holds_on()
    {
        double most = MostOff(Make(45, hand: true).W);
        Assert.True(most < 5, $"{most:F0}°");
    }

    [Fact]
    public void Steaming_at_boats_lying_still_is_no_attack()
    {
        double most = MostOff(Make(45, lying: true).W);
        Assert.True(most < 5, $"{most:F0}°");
    }
}
