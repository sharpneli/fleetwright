using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Stations work by their men (docs/battle/crew.md N2): a mount's crew thinning stretches its reload, below the
/// few it's fought with it's silent, its barbette's handling men set its supply; a director with nobody is out, an eye
/// short-handed reads slower; the walking wounded back count again.</summary>
public class StationTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipCondition Fresh(ShipClass c)
    {
        var k = new ShipCondition();
        k.Reset(c);
        return k;
    }

    static int Crew(ShipClass c, int g) => g < c.Components.Length ? c.Components[g].BattleCrew : c.Cells[g - c.Components.Length].BattleCrew;

    [Fact]
    public void A_mounts_crew_thins_its_rate_and_below_the_few_it_is_silent()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var k = Fresh(c);
        int m = Enumerable.Range(0, c.Mounts.Length).First(i => c.Mounts[i].Barbette >= 0 && c.Mounts[i].Kind == Shipgen.ComponentKind.Main);
        int g = Casualties.Part(c.Mounts[m].Component), need = Crew(c, g);
        int barrels = c.TurretTypes[c.Mounts[m].Type].Barrels, few = Math.Min(3, (int)Math.Round(c.Mounts[m].CalibreMm * Settings.GunMinMenPerMm)) * barrels;
        output.WriteLine($"mount {m}: {need} men in the gunhouse, fought with {few}, {Crew(c, Casualties.Part(c.Mounts[m].Barbette))} handling");
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.Equal(1, k.MountCrew[m]);
        Assert.True(k.MountWorks[m]);

        k.Gone[g] = need / 2;
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.Equal((double)(need - need / 2) / need, k.MountCrew[m], 9);
        Assert.True(k.MountWorks[m] && k.MountLasts[m]);

        k.Gone[g] = need - few + 1;
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.False(k.MountWorks[m]);
        Assert.False(k.MountLasts[m]);   // silent for want of men, and so it stays: her captain's fall-out reads it
        Assert.Equal(0, k.MountCrew[m]);

        // her handling men gone: no rounds come up
        k.Gone[g] = 0;
        int h = Casualties.Part(c.Mounts[m].Barbette);
        k.Gone[h] = Crew(c, h) / 2;
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.True(k.MountWorks[m]);
        Assert.InRange(k.MountSupply[m], 0.4, 0.6);
        k.Gone[h] = Crew(c, h);
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.False(k.MountWorks[m]);
    }

    [Fact]
    public void A_director_with_nobody_is_out_and_an_eye_short_handed_reads_slower()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var k = Fresh(c);
        int g = Casualties.Part(c.Directors[0].Component), need = Crew(c, g);
        Assert.True(need > 0);
        int eye = Enumerable.Range(0, c.Eyes.Length).First(e => c.Eyes[e].Component == c.Directors[0].Component);
        k.Gone[g] = need / 2;
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.True(k.DirectorWorks[0]);
        Assert.Equal((double)(need - need / 2) / need, k.EyeCrew[eye], 9);
        k.Gone[g] = need;
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.False(k.DirectorWorks[0]);
        Assert.False(k.EyeWorks[eye]);

        // the walking wounded back: a change, and they count again
        k.Gone[g] = need;
        k.Back(g, need, 60_000);
        Assert.True(k.Clear(60_000));
        Functions.Evaluate(k, Settings, 60_000, 60_000);
        Assert.True(k.DirectorWorks[0]);
        Assert.Equal(1, k.EyeCrew[eye]);
    }
}
