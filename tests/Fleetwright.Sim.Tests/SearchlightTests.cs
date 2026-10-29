using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Searchlights in the design (docs/battle/night.md Q3): shipgen sites them, the class carries a row for each
/// with its beam, they're manned, and damage, the loss of what carries them and a power cut put them out.</summary>
public class SearchlightTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>A battlecruiser and a destroyer of the Great War carry their template's lights: 36-inch on the one, smaller
    /// on the other, high over the sea, each with its men and a function of its own.</summary>
    [Fact]
    public void Great_War_ships_carry_their_lights_manned_and_high()
    {
        foreach (var (name, min, mirror) in new[] { ("lion", 8, (0.90, 0.92)), ("v25", 1, (0.61, 0.85)) })
        {
            var c = Fleet.Scenario(name);
            var k = new ShipCondition();
            k.Reset(c);
            Functions.Evaluate(k, Settings, 0, 0);
            Assert.True(c.Searchlights.Length >= min, $"{name}: {c.Searchlights.Length} lights");
            for (int i = 0; i < c.Searchlights.Length; i++)
            {
                ref readonly var l = ref c.Searchlights[i];
                double men = BattleStations.Share(c, k, Casualties.Part(l.Component));
                output.WriteLine($"{name} {c.ComponentId(l.Component)}: on {(l.On >= 0 ? c.ComponentId(l.On) : "the deck")}, " +
                                 $"{l.HeightM:0.0} m up, {100 * l.MirrorM:0} cm, {l.BeamCd:0.0e0} cd, {l.SpreadDeg:0.0}°, {l.Control}, {l.PowerKw:0.0} kW, " +
                                 $"{c.Components[l.Component].BattleCrew} men");
                Assert.Equal(ComponentKind.Searchlight, c.Components[l.Component].Kind);
                Assert.InRange(l.MirrorM, mirror.Item1, mirror.Item2);
                Assert.InRange(l.HeightM, 3, 40);
                Assert.Equal(SearchlightTemplates.BeamCd(l.MirrorM, 16000), l.BeamCd, 6);
                Assert.True(c.Components[l.Component].BattleCrew > 0);
                Assert.Equal(BattleStation.Searchlights, BattleStations.Of(c, Casualties.Part(l.Component)));
                Assert.Equal(1, men);
                Assert.True(k.SearchlightWorks[i]);
            }
        }
    }

    /// <summary>A light struck is out; one on a funnel's platform goes with the funnel; the power cut puts them all out
    /// until it's back.</summary>
    [Fact]
    public void A_hit_its_funnel_or_the_power_puts_a_light_out()
    {
        var c = Fleet.Scenario("lion");
        int onFunnel = -1, other = -1;
        for (int i = 0; i < c.Searchlights.Length; i++)
            if (c.Searchlights[i].On >= 0 && c.Components[c.Searchlights[i].On].Kind == ComponentKind.Funnel)
                onFunnel = onFunnel < 0 ? i : onFunnel;
            else
                other = other < 0 ? i : other;
        Assert.True(onFunnel >= 0 && other >= 0, "a light on a funnel and one elsewhere");

        var k = new ShipCondition();
        k.Reset(c);
        k.Worsen(c.Searchlights[other].Component, PartState.Disabled);
        k.Worsen(c.Searchlights[onFunnel].On, PartState.Destroyed);
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.False(k.SearchlightWorks[other], "struck");
        Assert.False(k.SearchlightWorks[onFunnel], "its funnel gone");
        Assert.Contains(true, k.SearchlightWorks);

        k.Reset(c);
        long now = 600_000;
        Effects.PowerOut(c, k, new P3(0, 0, 0), -1, -1, double.PositiveInfinity, 3 * 60_000, now, DownCause.HitCascade);
        Functions.Evaluate(k, Settings, now, now);
        Assert.DoesNotContain(true, k.SearchlightWorks);
        k.Clear(now + 3 * 60_000);
        Functions.Evaluate(k, Settings, now + 3 * 60_000, now + 3 * 60_000);
        Assert.DoesNotContain(false, k.SearchlightWorks);
    }
}
