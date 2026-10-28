using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The men's nerve (docs/battle/crew.md N3b): a station's duress against its men's nerve by their experience. A
/// green crew at an open gun hit hard has some of its men go, an elite one hardly any, and trained men in a turret none;
/// they come back once it's been quiet a while; with its officer hit a green station's men freeze until a relief officer
/// comes; a station once over its nerve stands less after; the shaken and the frozen aren't at work.</summary>
public class SteadinessTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static (ShipClass C, ShipCondition K) Ship(string design, double experience)
    {
        var w = new World(Start, 3, Settings);
        var c = Fleet.Get(design).Class!;
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1));
        k.Experience = experience;
        return (c, k);
    }

    /// <summary>The open station with the most men (a gun's or the bridge's), and a turret's gun crew.</summary>
    static int Open(ShipClass c, ShipCondition k) =>
        Enumerable.Range(0, k.StationGroup.Length).Where(n => k.StationOpen[n]).Select(n => k.StationGroup[n]).MaxBy(g => c.Components[g].BattleCrew);

    static int Turret(ShipClass c) => Casualties.Part(c.Mounts[Enumerable.Range(0, c.Mounts.Length).First(c.Enclosed)].Component);

    /// <summary>A shell through a station and a share of its men hit (its officer or not), and who stays worked out then.</summary>
    static void Hit(ShipClass c, ShipCondition k, int g, double share, long now, bool officer = false)
    {
        Casualties.Begin(k, now);
        Steadiness.Through(k, g, Settings);
        int men = Casualties.Men(c, k, g), hurt = (int)Math.Round(share * men);
        k.Gone[g] += hurt;
        k.CrewLost += hurt;
        Steadiness.Hurt(c, k, g, hurt, men, officer, now, Settings);
        Functions.Evaluate(k, Settings, now, now);
    }

    static int Shaken(ShipCondition k, int g) => k.Shaken[k.StationOf[g]];

    [Fact]
    public void Green_men_at_an_open_station_hit_hard_go_and_elite_ones_hardly()
    {
        var going = new int[4];
        for (int x = 0; x < 4; x++)
        {
            var (c, k) = Ship("dreadnought", x);
            int g = Open(c, k);
            Hit(c, k, g, 0.4, 1000);
            going[x] = Shaken(k, g);
            output.WriteLine($"{Experience.Names[x]}: {going[x]} of {Casualties.Men(c, k, g)} gone (crew {c.Components[g].BattleCrew}), duress {Steadiness.Duress(c, k, g, 1000, Settings):0.00}");
        }
        Assert.True(going[0] > 0, "green men go");
        Assert.True(going[0] > going[3], "elite men hold better");
        for (int x = 1; x < 4; x++)
            Assert.True(going[x] <= going[x - 1]);
    }

    [Fact]
    public void Trained_men_in_a_turret_hold()
    {
        var (c, k) = Ship("dreadnought", Experience.Trained);
        int g = Turret(c);
        Hit(c, k, g, 0.4, 1000);
        Assert.Equal(0, Shaken(k, g));
        Assert.Equal(Casualties.Men(c, k, g), BattleStations.AtWork(c, k, g));
    }

    [Fact]
    public void The_shaken_come_back_once_it_has_been_quiet_a_while()
    {
        var (c, k) = Ship("dreadnought", Experience.Green);
        int g = Open(c, k);
        Hit(c, k, g, 0.4, 1000);
        int gone = Shaken(k, g);
        Assert.True(gone > 0);
        Assert.Equal(Casualties.Men(c, k, g) - gone, BattleStations.AtWork(c, k, g));   // not at work
        Assert.True(k.NerveDueMs > 1000, "looked at again");
        // a minute on they're still away; half an hour on, back
        Functions.Evaluate(k, Settings, 61_000, 61_000);
        Assert.True(Shaken(k, g) > 0);
        long t = 61_000;
        while (k.NerveDueMs > 0 && t < 3_600_000)
        {
            t = k.NerveDueMs;
            Functions.Evaluate(k, Settings, t, t);
        }
        output.WriteLine($"{gone} gone; back by {t / 60_000.0:0.0} min");
        Assert.Equal(0, Shaken(k, g));
        Assert.InRange(t, 5 * 60_000, 40 * 60_000);
    }

    [Fact]
    public void With_its_officer_hit_a_green_station_freezes_until_a_relief_officer_comes()
    {
        var (c, k) = Ship("dreadnought", Experience.Green);
        int g = Turret(c), n = k.StationOf[g];
        Hit(c, k, g, 0.2, 1000, officer: true);
        output.WriteLine($"frozen {k.Frozen[n]} of {Casualties.Men(c, k, g)}, the relief officer at {k.OfficerAt[n] / 1000.0:0} s");
        Assert.True(k.Frozen[n] > 0);
        Assert.True(k.OfficerAt[n] > 1000);
        Assert.True(k.NerveDueMs <= k.OfficerAt[n]);
        Functions.Evaluate(k, Settings, k.OfficerAt[n], k.OfficerAt[n]);
        Assert.Equal(0, k.Frozen[n]);
        Assert.Equal(0, k.OfficerAt[n]);

        // trained men don't freeze
        var (c2, k2) = Ship("dreadnought", Experience.Trained);
        Hit(c2, k2, g, 0.2, 1000, officer: true);
        Assert.Equal(0, k2.Frozen[n]);
    }

    [Fact]
    public void A_station_once_over_its_nerve_stands_less_after()
    {
        var (c, k) = Ship("dreadnought", Experience.Green);
        int g = Open(c, k), n = k.StationOf[g];
        Hit(c, k, g, 0.5, 1000);
        Assert.True(k.Over[n]);
        Assert.InRange(k.NerveKept[n], 0.9, 0.95);
        Functions.Evaluate(k, Settings, 3_600_000, 3_600_000);
        Assert.False(k.Over[n]);
        Assert.True(k.NerveKept[n] < 1, "some of it lost for the action");
    }

    [Fact]
    public void The_nerve_rows_read_between_the_whole_numbers()
    {
        Assert.Equal(Settings.Nerves[2], Settings.NerveAt(2));
        var half = Settings.NerveAt(1.5);
        Assert.Equal((Settings.Nerves[1].Nerve + Settings.Nerves[2].Nerve) / 2, half.Nerve, 9);
        Assert.Equal((Settings.Nerves[1].BackS + Settings.Nerves[2].BackS) / 2, half.BackS, 9);
    }

    [Fact]
    public void Reliefs_wait_while_a_station_is_over_their_nerve()
    {
        var (c, k) = Ship("dreadnought", Experience.Trained);
        int g = Turret(c);
        Hit(c, k, g, 0.6, 1000);
        double nerve = Steadiness.ReliefNerve(k, Settings);
        Assert.True(Steadiness.Duress(c, k, g, 1000, Settings) > nerve, "under fire: they wait");
        Assert.True(Steadiness.Duress(c, k, g, 15 * 60_000, Settings) < nerve, "a lull a while on");
    }
}
