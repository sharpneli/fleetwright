using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The admirals' lines at night (docs/battle/night.md Q7): a squadron closes up at dusk to doctrine's share of its
/// spacing and opens out again by day; a ship in line ahead that can't make out her next ahead in the dark holds the
/// course and speed she had until she sees her again.</summary>
public class NightLineTests(ITestOutputHelper output)
{
    static readonly SimSettings S = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };
    const double Lat = 56, Lon = 4;

    /// <summary>Four destroyers in line ahead, 600 m apart, steaming north at 15 kn, from a time; their squadron.</summary>
    static (World W, AiSystem Ai, int Group) Line(WorldTime at, double cloud = 0, double spacingM = 600)
    {
        var w = new World(at, 3, S);
        var ai = new AiSystem(w);
        int dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetWeather(20_000, cloud: cloud));
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 180);
        for (int i = 0; i < 4; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(dd, la, lo, 0, 15, 1));
            Earth.Advance(ref p, ref f, spacingM);
        }
        w.Apply(GroupSystem.Create(1, Formation.Column, spacingM));
        int g = w.Groups[w.Groups.Count - 1].GroupId;
        for (int e = 2; e <= 4; e++)
            w.Apply(GroupSystem.Add(g, e));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.NightSpacingShare, 0.5));
        w.Apply(ai.Orders.Speed(Unit.Group(g), 15));
        return (w, ai, g);
    }

    /// <summary>At dusk on the Dover Strait's spring evening the squadron closes from 600 m to 300; at dawn it opens to 600
    /// again.</summary>
    [Fact]
    public void A_squadron_closes_up_at_night_and_opens_out_by_day()
    {
        var (w, _, g) = Line(WorldTime.FromUtc(1917, 4, 20, 19, 30));   // the sun at -4.7°
        w.AdvanceTo(w.Time + 2 * 60_000);
        double dusk = w.Groups.Get(g)!.SpacingM;
        w.AdvanceTo(WorldTime.FromUtc(1917, 4, 20, 20, 0));   // -8°
        double night = w.Groups.Get(g)!.SpacingM;
        w.AdvanceTo(WorldTime.FromUtc(1917, 4, 21, 4, 30));   // the sun back over -6° by about 03:40
        double day = w.Groups.Get(g)!.SpacingM;
        output.WriteLine($"spacing at dusk {dusk:F0} m, at night {night:F0} m, at dawn {day:F0} m");
        Assert.Equal(600, dusk, 0);
        Assert.Equal(300, night, 0);
        Assert.Equal(600, day, 0);
    }

    /// <summary>Under a black overcast (the Dover Strait's), a destroyer 600 m astern of another can't make her out and holds
    /// her course and speed; 150 m astern, she keeps station by her.</summary>
    [Fact]
    public void A_ship_that_cannot_see_her_next_ahead_holds_her_course()
    {
        CaptainReason Play(double spacingM)
        {
            var (w, ai, _) = Line(WorldTime.FromUtc(1917, 4, 21, 0), cloud: 1, spacingM: spacingM);
            w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.NightSpacingShare, 1));
            w.AdvanceTo(w.Time + 30_000);
            return ai.Captains.ReasonOf(2);
        }
        var far = Play(600);
        var near = Play(150);
        output.WriteLine($"600 m astern: {far}; 150 m astern: {near}");
        Assert.Equal(CaptainReason.LostTouch, far);
        Assert.NotEqual(CaptainReason.LostTouch, near);
    }
}
