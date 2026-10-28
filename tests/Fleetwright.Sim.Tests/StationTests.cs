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

    [Fact]
    public void The_conn_shifts_aft_when_the_bridge_party_is_lost()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var k = Fresh(c);
        var parts = Enumerable.Range(0, c.Components.Length).Where(i => c.Components[i].Kind == Shipgen.ComponentKind.Superstructure && c.Components[i].BattleCrew > 0).ToList();
        var fore = parts.Where(i => c.Components[i].Role is not (ComponentRole.AftControl or ComponentRole.Director or ComponentRole.Rangefinder)).ToList();
        var aft = parts.Where(i => c.Components[i].Role == ComponentRole.AftControl).ToList();
        output.WriteLine($"command forward {fore.Sum(i => c.Components[i].BattleCrew)} men in {fore.Count} blocks, aft {aft.Sum(i => c.Components[i].BattleCrew)} in {aft.Count}");
        Assert.NotEmpty(aft);
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.True(k.Conned);
        Assert.Equal(0, k.ConnAt);

        foreach (int i in fore)
            k.Gone[i] = c.Components[i].BattleCrew;
        Functions.Evaluate(k, Settings, 1000, 1000);
        Assert.False(k.Conned);   // the control shifting aft
        Assert.Equal(1000 + (long)(Settings.ConnShiftS * 1000), k.ConnShiftMs);
        Assert.Equal(k.ConnShiftMs, k.NextClearMs);
        long there = k.ConnShiftMs;
        Assert.True(k.Clear(there));
        Functions.Evaluate(k, Settings, there, there);
        Assert.True(k.Conned);
        Assert.Equal(1, k.ConnAt);

        foreach (int i in aft)
            k.Gone[i] = c.Components[i].BattleCrew;
        Functions.Evaluate(k, Settings, there + 1000, there + 1000);
        Assert.False(k.Conned);
        Assert.Equal(-1, k.ConnAt);
    }

    [Fact]
    public void A_stokehold_keeps_its_steam_a_while_then_steams_by_its_men()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var k = Fresh(c);
        var rooms = Enumerable.Range(0, c.Rooms.Length).Where(r => c.Rooms[r].Kind == Shipgen.RoomKind.BoilerRoom).ToList();
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.Equal(1, k.PowerCap, 9);
        foreach (int r in rooms)
            foreach (int cell in c.Links(c.Rooms[r].Cells))
                k.Gone[Casualties.Cell(c, cell)] = c.Cells[cell].BattleCrew;
        Functions.Evaluate(k, Settings, 1000, 1000);
        Assert.Equal(1, k.PowerCap, 9);   // what's in the fires burns on
        Assert.Equal(Settings.StokeholdFloor, k.PowerCapLasts, 9);   // her captain sees where it's going
        long until = 1000 + (long)(Settings.StokeholdHoldS * 1000);
        Assert.Equal(until, k.NextClearMs);
        Assert.True(k.Clear(until));
        Functions.Evaluate(k, Settings, until, until);
        output.WriteLine($"stokeholds empty: power {k.PowerCap:P0} after {Settings.StokeholdHoldS / 60:0} min");
        Assert.Equal(Settings.StokeholdFloor, k.PowerCap, 9);
        Array.Clear(k.Gone);
        Functions.Evaluate(k, Settings, until + 1000, until + 1000);
        Assert.Equal(1, k.PowerCap, 9);   // manned again: at once
    }

    [Fact]
    public void A_gunhouse_ablaze_holds_nobody_at_work_until_it_clears()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var k = Fresh(c);
        int m = Enumerable.Range(0, c.Mounts.Length).First(i => k.PartSpace[c.Mounts[i].Component] >= 0 && c.Components[c.Mounts[i].Component].BattleCrew > 0);
        int space = k.PartSpace[c.Mounts[m].Component];
        k.Fire[space].Phase = BurnPhase.Developed;
        Functions.Evaluate(k, Settings, 0, 0);
        Assert.False(k.MountWorks[m]);
        Assert.Equal(0, k.MountCrew[m]);
        Assert.Equal(c.Components[c.Mounts[m].Component].BattleCrew, Casualties.Men(c, k, Casualties.Part(c.Mounts[m].Component)));   // not casualties
        k.Fire[space].Phase = BurnPhase.Out;
        Functions.Evaluate(k, Settings, 1000, 1000);
        Assert.True(k.MountWorks[m]);
        Assert.Equal(1, k.MountCrew[m]);
    }
}
