using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Whose splash (docs/battle/balance.md BL1c, fire-control.md F7; research/simulation/manual-gunnery-research.md
/// §5): a destroyer's guns firing independently at a ship are told apart from each other's falls less often when others
/// fire at her too, and her spots go doubtful; salvos are always told.</summary>
public class SplashTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    const double Lat = 56.0, Lon = 4.0;

    /// <summary>The first shooter's spots: how many, and how many told apart. Shooters 5.2 km off a light cruiser (beyond point blank, inside the independent discipline's 5.5 km) steaming
    /// north, each 300 m astern of the last, all firing independently at her.</summary>
    static (int Spots, int Told) Play(int shooters, string discipline)
    {
        var w = new World(Start, 5, Settings);
        int d = w.Ships.AddClass(Fleet.Get("destroyer").Class!), c = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 5_200);
        var (tl, to) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(c, tl, to, 0, 12, side: 1));
        for (int i = 0; i < shooters; i++)
        {
            var q = Earth.Position(Lat, Lon);
            var g = Earth.Forward(q, 180);
            Earth.Advance(ref q, ref g, 300 * i);
            var (la, lo) = Earth.LatLon(q);
            w.Apply(ShipSystem.Spawn(d, la, lo, 0, 12, side: 0));
        }
        w.Apply(GunnerySystem.SetDiscipline(0, Array.FindIndex(Settings.FireDisciplines, x => x.Name == discipline)));
        int spots = 0, told = 0;
        w.Gunnery.Spotted = s =>
        {
            if (s.Shooter == 2 && s.Seen)
                (spots, told) = (spots + 1, told + (s.Told ? 1 : 0));
        };
        for (int i = 0; i < shooters; i++)
            w.Apply(GunnerySystem.Engage(2 + i, -1, 1));
        w.AdvanceTo(Start + 8 * 60_000);
        return (spots, told);
    }

    [Fact]
    public void Independent_fire_is_told_apart_less_when_others_fire_at_her_too()
    {
        var (alone, aloneTold) = Play(1, "independent");
        var (four, fourTold) = Play(4, "independent");
        output.WriteLine($"alone: {aloneTold} of {alone} told; four on her: {fourTold} of {four}");
        Assert.True(alone > 20 && four > 20, $"spots {alone}, {four}");
        // alone, her own guns' falls are one group to her control officer (research §5); with three others on the same
        // ship, about half (1/(1 + 0.6 N), N their falls within the window)
        Assert.Equal(alone, aloneTold);
        Assert.InRange((double)fourTold / four, 0.2, 0.75);
    }

    [Fact]
    public void Salvos_are_always_told()
    {
        var (spots, told) = Play(4, "rn-1914");
        output.WriteLine($"salvos: {told} of {spots} told");
        Assert.True(spots > 5);
        Assert.Equal(spots, told);
    }
}
