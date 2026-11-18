using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Whose splash (docs/battle/balance.md BL1c, fire-control.md F7; research/simulation/manual-gunnery-research.md
/// §5): a destroyer's guns firing independently at a ship are told apart from each other's falls less often when others
/// fire at her too, and her spots go doubtful; so are salvos (docs/battle/shell-consumption.md SC2: no salvo was ever told by
/// its pattern alone), less with a fall-of-shot clock, hardly at all when each ship's shells carry their own dye.</summary>
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

    /// <summary>Whose salvo (docs/battle/shell-consumption.md SC2): two battleships of one calibre on one target at 14 km.
    /// Alone, every fall she sees is her own; with a consort's salvos landing about her too, her spotter tells his own
    /// less often, the less the wider his window (by the watch alone, 15 s either way, against a fall-of-shot clock's
    /// 5), and nearly always when each ship's shells carry their own dye. The research's model (whose-salvo.md §6 S1,
    /// two heavy ships at 40 s): 0.72 without a clock, 0.91 with one; ours count only the falls before his call.</summary>
    [Fact]
    public void A_consorts_salvos_on_her_cost_the_spotter_his_own_unless_timed_or_dyed()
    {
        var qe = Fleet.Get("super_dreadnought").Class!;
        double Told(SimSettings settings, int ships, bool dyed)
        {
            int seen = 0, told = 0;
            for (int seed = 1; seed <= 4; seed++)
            {
                var w = new World(Start, (ulong)seed, settings with { HitsDoDamage = false });
                int c = w.Ships.AddClass(qe);
                var p = Earth.Position(Lat, Lon);
                var f = Earth.Forward(p, 90);
                Earth.Advance(ref p, ref f, 14_000);
                var (tl, to) = Earth.LatLon(p);
                w.Apply(ShipSystem.Spawn(c, tl, to, 0, 15, side: 1));   // entity 1: the target
                for (int s = 0; s < ships; s++)
                {
                    var q = Earth.Position(Lat, Lon);
                    var g = Earth.Forward(q, 180);
                    Earth.Advance(ref q, ref g, 900 * s);
                    var (ql, qo) = Earth.LatLon(q);
                    w.Apply(ShipSystem.Spawn(c, ql, qo, 0, 15, side: 0));
                    if (dyed)
                        w.Apply(ShipSystem.Dye(2 + s, (Dye)(1 + s)));
                    w.Apply(GunnerySystem.Engage(2 + s, 0, 1));
                }
                w.Gunnery.Spotted = r =>
                {
                    if (r.Shooter == 2 && r.Seen)
                        (seen, told) = (seen + 1, told + (r.Told ? 1 : 0));
                };
                w.AdvanceTo(Start + 15 * 60_000);
            }
            return (double)told / Math.Max(seen, 1);
        }
        var noClock = Settings with { SplashWindowClockS = Settings.SplashWindowS };
        double alone = Told(Settings, 1, false), timed = Told(Settings, 2, false), watch = Told(noClock, 2, false), dyed = Told(noClock, 2, true);
        output.WriteLine($"told: alone {alone:P0}; with a consort, by the watch {watch:P0}, with the clock {timed:P0}, dyed (by the watch) {dyed:P0}");
        Assert.Equal(1, alone);
        Assert.True(watch < 0.95, "by the watch, a consort's salvos cost him his own");
        Assert.True(timed > watch && dyed > watch && dyed > 0.95, "the clock or her own colour keeps it");
    }

    /// <summary>A light cruiser's battery's margin over the air on a destroyer 4.5 km off, both steaming north, in a
    /// visibility, two minutes on.</summary>
    static double Margin(double visibilityM)
    {
        var w = new World(Start, 9, Settings);
        w.Apply(Command.SetWeather(visibilityM));
        int c = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!), d = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 4_500);
        var (tl, to) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 15, side: 0));
        w.Apply(ShipSystem.Spawn(d, tl, to, 0, 15, side: 1));
        w.Apply(GunnerySystem.Engage(1, -1, 2));
        w.AdvanceTo(Start + 2 * 60_000);
        return w.Gunnery.MarginOf(1, 0);
    }

    [Fact]
    public void The_air_on_the_sights_and_the_spots_grows_toward_the_edge_of_sight()
    {
        // docs/battle/balance.md BL1i: the layer's wire and the spotter's eye work through the same air as the rangefinder,
        // by the battery's margin over it: nothing in clear air, more toward the edge of sight (radar's ranging and spotting,
        // a later cut, step round it)
        double clear = Margin(25_000), mist = Margin(6_000), edge = Margin(4_800);
        output.WriteLine($"margins: clear {clear:0.00}, mist {mist:0.00}, the edge {edge:0.00}");
        Assert.True(clear > mist && mist > edge && edge >= 1, $"{clear}, {mist}, {edge}");
        Assert.Equal(1, SightModel.HazeFactor(clear, Settings.AimHazePower, Settings));
        double aim = SightModel.HazeFactor(edge, Settings.AimHazePower, Settings), spot = SightModel.HazeFactor(edge, Settings.SpotHazePower, Settings);
        Assert.True(aim > 1 && spot > aim, $"aim x{aim:0.00}, spot x{spot:0.00}");
    }
}
