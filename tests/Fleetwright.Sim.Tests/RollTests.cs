using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;

namespace Fleetwright.Sim.Tests;

/// <summary>Ships' roll in the weather's sea (docs/battle/balance.md BL1a, <see cref="RollSea"/>) against the research's
/// physics (research/simulation/ship-motion/ship-motion-gunnery-research.md §3.2): no sea, no roll; beam on rolls most and
/// head on least; resonance with the swell, which bilge keels damp, and a stiff ship lively in it; a long-period ship
/// rolling in the swell and not in a short wind sea, at her own period near resonance; the weather's swell set by its own command and kept by the weather's; a scenario's swell read and
/// written; no garbage.</summary>
public class RollTests
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);

    static RollSea Sea(double windKn, double swellM, double swellS = 12, double swellFrom = 0) =>
        new(new SeaSpectrum(SeaState.Of(new WeatherHere(25_000, 0, windKn * 0.514444, SwellHsM: swellM, SwellTpS: swellS, SwellFromDeg: swellFrom))));

    static double Deg(RollState r) => double.RadiansToDegrees(r.AmplitudeRad);

    [Fact]
    public void A_flat_sea_rolls_nobody()
    {
        var r = Sea(0, 0).At(90, 5, 14, 0.1, Settings.RollExcitation);
        Assert.Equal(0, r.AmplitudeRad);
        Assert.Equal(0, r.RateSigmaRadS);
    }

    [Fact]
    public void Beam_on_rolls_most_and_head_on_least()
    {
        // a swell from the north: heading 0 meets it head on, 90 beam on
        var sea = Sea(0, 3, 13, 0);
        double head = Deg(sea.At(0, 0, 13, 0.1, Settings.RollExcitation)), beam = Deg(sea.At(90, 0, 13, 0.1, Settings.RollExcitation));
        Assert.True(beam > 3 * head, $"beam {beam:0.0}° against head {head:0.0}°");
    }

    [Fact]
    public void A_ship_in_tune_with_the_swell_rolls_hard_bilge_keels_halve_it_and_a_stiff_ship_is_lively()
    {
        // research §3.2: the heaviest rolling near T_ship/T_wave 1, a tender ship (long period) steady, and a short-period
        // ship in long waves riding their slope ("lively", "jerky": Bismarck), fast; §3.3: bilge keels about x0.5 at resonance
        var sea = Sea(0, 3, 13, 0);
        var tuned = sea.At(90, 0, 13, 0.1, Settings.RollExcitation);
        var stiff = sea.At(90, 0, 7, 0.1, Settings.RollExcitation);
        double tender = Deg(sea.At(90, 0, 22, 0.1, Settings.RollExcitation)), bare = Deg(sea.At(90, 0, 13, 0.04, Settings.RollExcitation));
        Assert.True(Deg(tuned) > 3 * tender, $"tuned {Deg(tuned):0.0}°, a 22 s ship {tender:0.0}°");
        Assert.InRange(Deg(tuned) / bare, 0.35, 0.65);
        Assert.True(stiff.RateSigmaRadS > tuned.RateSigmaRadS, $"a 7 s ship's roll rate {double.RadiansToDegrees(stiff.RateSigmaRadS):0.0}°/s, the tuned ship's {double.RadiansToDegrees(tuned.RateSigmaRadS):0.0}°/s");
    }

    [Fact]
    public void A_long_period_ship_rolls_in_the_swell_not_in_a_short_wind_sea()
    {
        // research §7.3: "small ships roll in the wind sea and big ships roll in the swell"
        double wind = Deg(Sea(18, 0).At(90, 0, 15, 0.1, Settings.RollExcitation));
        double swell = Deg(Sea(0, 2, 14, 270).At(0, 0, 15, 0.1, Settings.RollExcitation));
        double destroyer = Deg(Sea(18, 0).At(90, 0, 8.5, 0.1, Settings.RollExcitation));
        Assert.True(swell > 2 * wind, $"a 15 s ship in a 2 m swell {swell:0.0}°, in a Bft 5 wind sea {wind:0.0}°");
        Assert.True(destroyer > 2 * wind, $"a 8.5 s ship in the wind sea {destroyer:0.0}° against the 15 s ship's {wind:0.0}°");
    }

    [Fact]
    public void She_rolls_at_her_own_period_near_resonance_and_at_the_waves_far_from_it()
    {
        // research §3.2: T_roll when 0.7 < r < 1.4, the encounter period otherwise
        Assert.InRange(Sea(0, 2, 13, 0).At(90, 0, 14, 0.1, Settings.RollExcitation).PeriodS, 12.5, 15);
        Assert.InRange(Sea(20, 0).At(90, 0, 18, 0.1, Settings.RollExcitation).PeriodS, 5, 9);
    }

    [Fact]
    public void The_swell_is_its_own_command_and_the_weather_keeps_it()
    {
        var w = new World(Start, 1, Settings);
        Assert.Equal(WeatherHere.DefaultSwellHsM, w.Weather.At(default, w.Time).SwellHsM);
        w.Apply(Command.SetSea(4, 15, 300));
        w.Apply(Command.SetWeather(9000, 200, 10));
        var h = w.Weather.At(default, w.Time);
        Assert.Equal((4.0, 15.0, 300.0), (h.SwellHsM, h.SwellTpS, h.SwellFromDeg));
        Assert.Equal(9000, h.VisibilityM);
        Assert.True(w.Sea.HsM > 4, $"the roll sea's height {w.Sea.HsM:0.00} m");
        w.Apply(Command.SetSea(0, 12));
        Assert.True(double.IsNaN(w.Weather.At(default, w.Time).SwellFromDeg));
        Assert.Throws<ArgumentException>(() => w.Apply(Command.SetSea(-1, 12)));
    }

    [Fact]
    public void A_scenarios_swell_is_read_and_written()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""{ "visibility_km": 8, "wind_from": 200, "wind_kn": 12, "swell_m": 2.5, "swell_s": 14, "swell_from": 250 }""");
        var h = Scenario.ParseWeather(doc.RootElement);
        Assert.Equal((2.5, 14.0, 250.0), (h.SwellHsM, h.SwellTpS, h.SwellFromDeg));
        using var calm = System.Text.Json.JsonDocument.Parse("""{ "visibility_km": 8 }""");
        var c = Scenario.ParseWeather(calm.RootElement);
        Assert.Equal((WeatherHere.DefaultSwellHsM, WeatherHere.DefaultSwellTpS), (c.SwellHsM, c.SwellTpS));
        Assert.True(double.IsNaN(c.SwellFromDeg));
    }

    [Fact]
    public void A_roll_costs_no_garbage()
    {
        var sea = Sea(20, 2);
        sea.At(45, 8, 13, 0.1, Settings.RollExcitation);
        long before = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0;
        for (int i = 0; i < 100; i++)
            sum += sea.At(i * 3.6, 8, 13, 0.1, Settings.RollExcitation).AmplitudeRad;
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    [Fact]
    public void Casemates_go_under_on_the_weather_side_in_a_heavy_sea()
    {
        // research §1.2: Coronel's main-deck casemates (about 3 m) out on the weather side at about H_s 5 m, a turret's guns
        // never; Iron Duke's (about 4.5 m) only in a heavier sea; nothing in a calm. A sea from the west (270), the ship
        // heading north at 15 kn: her port side is the weather side.
        var heavy = new RollSea(new SeaSpectrum(SeaState.Of(new WeatherHere(25_000, 270, 0, SwellHsM: 5, SwellTpS: 11, SwellFromDeg: 270))));
        var calm = Sea(0, 0);
        Assert.True(heavy.Awash(3, 0.1, -1, 0, 15), "a port casemate amidships, weather side");
        Assert.False(heavy.Awash(3, 0.1, 1, 0, 15), "a starboard casemate, lee side");
        Assert.False(heavy.Awash(8, 0.6, -1, 0, 15), "a turret's guns");
        Assert.True(heavy.Awash(4.5, 0.8, 0, 270, 20), "a bow casemate heading into it");
        Assert.False(calm.Awash(1.5, 0.8, -1, 0, 25), "a calm");
    }
}
