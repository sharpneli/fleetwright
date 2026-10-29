using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The captains at night by doctrine (docs/battle/night.md Q7): a ship lit turns away from the light with her own
/// lights out, or fights on; searchlights never, to fire, or to search; a ship not made out challenged only within the
/// doctrine's range.</summary>
public class NightDoctrineTests(ITestOutputHelper output)
{
    static readonly SimSettings S = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };
    const double Lat = 56, Lon = 4;

    /// <summary>A light cruiser (side 1, entity 1) steaming north at 10 kn and a destroyer (side 2, entity 2) stopped 1.9 km
    /// to the east of her, on the Dover Strait's moonless night; the AI on both, both sides' star shell off.</summary>
    static (World W, AiSystem Ai) Night()
    {
        var w = new World(WorldTime.FromUtc(1917, 4, 21, 0), 7, S);
        var ai = new AiSystem(w);
        int cl = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        int dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetWeather(20_000));
        w.Apply(ShipSystem.Spawn(cl, Lat, Lon, 0, 10, 1));
        w.Apply(ShipSystem.Spawn(dd, Lat, Lon + 0.03, 180, 0, 2));
        foreach (int side in new[] { 1, 2 })
            w.Apply(ai.Orders.SideDoctrine(side, DoctrineField.StarShell, (double)StarShellUse.Never));
        return (w, ai);
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    /// <summary>Held in the destroyer's beam, the cruiser turns stern to it with her own lights out by "turn away"; by
    /// "fight on" she holds her course.</summary>
    [Fact]
    public void A_ship_lit_turns_away_with_her_lights_out_or_fights_on()
    {
        (double Course, CaptainReason Why, SearchlightOrder Lights) Play(WhenLit rule)
        {
            var (w, ai) = Night();
            w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.WhenLit, (double)rule));
            w.Apply(ai.Orders.SideDoctrine(2, DoctrineField.Unknowns, (double)Unknowns.Hold));
            w.AdvanceTo(w.Time + 30_000);
            w.Apply(SearchlightSystem.Order(2, SearchlightOrder.Illuminate, 1));
            w.AdvanceTo(w.Time + 30_000);
            return (Ship(w, 1).CourseOrderDeg, ai.Captains.ReasonOf(1), w.Searchlights.OrderOf(1, w.Time.Ms).Order);
        }
        var away = Play(WhenLit.TurnAway);
        var on = Play(WhenLit.FightOn);
        output.WriteLine($"turn away: course {away.Course:F0}°, {away.Why}, her lights {away.Lights}; fight on: course {on.Course:F0}°, {on.Why}");
        Assert.Equal(CaptainReason.Lit, away.Why);
        Assert.InRange(away.Course, 240, 300);   // the light is east of her: west
        Assert.Equal(SearchlightOrder.Off, away.Lights);
        Assert.NotEqual(CaptainReason.Lit, on.Why);
        Assert.True(on.Course < 20 || on.Course > 340, "held on");
    }

    /// <summary>A destroyer she's engaged and can't see in the dark: lit with her searchlights "to fire", never "never"; and
    /// "search" sweeps toward a ship not made out that she holds her fire on.</summary>
    [Fact]
    public void Searchlights_by_doctrine_never_to_fire_or_to_search()
    {
        SearchlightOrder Play(SearchlightUse use, Unknowns unknowns)
        {
            var (w, ai) = Night();
            w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Searchlights, (double)use));
            w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Unknowns, (double)unknowns));
            w.Apply(ai.Orders.SideDoctrine(2, DoctrineField.Unknowns, (double)Unknowns.Hold));
            w.Apply(ai.Orders.SideDoctrine(2, DoctrineField.Searchlights, (double)SearchlightUse.Never));
            var seen = SearchlightOrder.Off;
            for (int s = 0; s < 90; s++)
            {
                w.AdvanceTo(w.Time + 1_000);
                var o = w.Searchlights.OrderOf(1, w.Time.Ms).Order;
                if (o > seen)
                    seen = o;
            }
            return seen;
        }
        var toFire = Play(SearchlightUse.ToFire, Unknowns.Fire);
        var never = Play(SearchlightUse.Never, Unknowns.Fire);
        var search = Play(SearchlightUse.Search, Unknowns.Hold);
        var held = Play(SearchlightUse.ToFire, Unknowns.Hold);
        output.WriteLine($"to fire: {toFire}; never: {never}; search, holding her fire: {search}; to fire, holding: {held}");
        Assert.Equal(SearchlightOrder.Illuminate, toFire);
        Assert.Equal(SearchlightOrder.Off, never);
        Assert.Equal(SearchlightOrder.Search, search);
        Assert.True(held <= SearchlightOrder.Ready, "nothing to fire at: nothing lit");
    }

    /// <summary>A ship not made out 1.9 km off is challenged within a 2.5 km challenge range, not within 1 km.</summary>
    [Fact]
    public void A_ship_not_made_out_is_challenged_only_within_the_doctrines_range()
    {
        bool Play(double rangeM)
        {
            var (w, ai) = Night();
            w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Unknowns, (double)Unknowns.Challenge));
            w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.ChallengeM, rangeM));
            w.Apply(ai.Orders.SideDoctrine(2, DoctrineField.Unknowns, (double)Unknowns.Hold));
            for (int s = 0; s < 60; s++)
            {
                w.AdvanceTo(w.Time + 1_000);
                if (w.Challenges.ChallengeOf(1).Target == 2)
                    return true;
            }
            return false;
        }
        bool within = Play(2_500), beyond = Play(1_000);
        output.WriteLine($"challenged within 2.5 km: {within}; within 1 km: {beyond}");
        Assert.True(within);
        Assert.False(beyond);
    }
}
