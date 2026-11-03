using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>An unstabilised instrument in a seaway (docs/battle/error-sources.md ES5, <see cref="FireControl.Held"/>): a
/// still ship, an eye and a gyro mounting hold her always; her roll costs a target abeam and not one ahead, her pitch the
/// other way; a higher magnification's narrower field loses her sooner; the Bismarck trials' case; and through her own
/// roll in the world's sea, a rolling destroyer's rangefinder against a battleship's.</summary>
public class InstrumentHoldTests(Xunit.Abstractions.ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };

    static OpticsRow Optics(double magnification, bool gyro = false) => new(magnification, 2, 10, 5, 0.5, Gyro: gyro);

    static double Held(OpticsRow o, double rollDegS, double pitchDegS, double relativeDeg, double scale = 1) =>
        FireControl.Held(o, double.DegreesToRadians(rollDegS), double.DegreesToRadians(pitchDegS), relativeDeg, Settings.InstrumentFollowS,
            Settings.InstrumentFieldDeg, scale);

    [Fact]
    public void A_still_ship_an_eye_and_a_gyro_mounting_hold_her_always()
    {
        Assert.Equal(1, Held(Optics(28), 0, 0, 90));
        Assert.Equal(1, Held(Optics(0), 6, 3, 90));
        Assert.Equal(1, Held(Optics(28, gyro: true), 6, 3, 90));
        Assert.Equal(1, Held(Optics(28), 6, 3, 90, scale: 0));
    }

    [Fact]
    public void Her_roll_costs_a_target_abeam_her_pitch_one_ahead()
    {
        var o = Optics(28);
        double rollAbeam = Held(o, 3, 0, 90), rollAhead = Held(o, 3, 0, 0), pitchAhead = Held(o, 0, 3, 0), pitchAbeam = Held(o, 0, 3, 90);
        output.WriteLine($"3°/s of roll: abeam {rollAbeam:P0}, ahead {rollAhead:P0}; of pitch: ahead {pitchAhead:P0}, abeam {pitchAbeam:P0}");
        Assert.True(rollAbeam < 0.8);
        Assert.Equal(1, rollAhead, 6);
        Assert.Equal(rollAbeam, pitchAhead, 6);
        Assert.Equal(1, pitchAbeam, 6);
        Assert.True(Held(o, 3, 0, 270) == rollAbeam, "either beam");
    }

    [Fact]
    public void A_narrower_field_loses_her_sooner()
    {
        double x15 = Held(Optics(15), 3, 1, 70), x28 = Held(Optics(28), 3, 1, 70), x40 = Held(Optics(40), 3, 1, 70);
        output.WriteLine($"x15 {x15:P0}, x28 {x28:P0}, x40 {x40:P0}");
        Assert.True(x15 > x28 && x28 > x40);
    }

    [Fact]
    public void The_Bismarck_trials_could_lose_her()
    {
        // stabilisation-tech.md Q4: a ±1° oscillation "could lose the target" in a 1.5° field at ×40. As a residual of 1σ
        // 0.71° (a sinusoid's RMS), the share she's held: lost some of the time, not all
        double rateDegS = 1 / Math.Sqrt(2) / Settings.InstrumentFollowS;
        double held = Held(Optics(40), rateDegS, 0, 90);
        output.WriteLine($"a ±1° oscillation at ×40: held {held:P0}");
        Assert.InRange(held, 0.5, 0.9);
    }

    [Fact]
    public void A_destroyer_rolling_beam_on_loses_her_more_than_a_battleship()
    {
        var sea = new RollSea(new SeaSpectrum(SeaState.Of(new WeatherHere(25_000, 90, 18, SwellHsM: 4, SwellTpS: 11, SwellFromDeg: 90))));
        double Of(string name)
        {
            var m = Fleet.Get(name).Class!.Motion;
            var roll = sea.At(0, 8, m.LengthM, m.BeamM, m.RollPeriodS, m.RollDamping, Settings.RollExcitation);
            var pitch = sea.Pitch(0, 8, m.LengthM, m.BeamM, Settings.PitchPeriodK * Math.Sqrt(Math.Max(m.DraughtM, 0.5)), Settings.PitchDamping);
            return FireControl.Held(Optics(24), roll.RateSigmaRadS, pitch.RateSigmaRadS, 90, Settings.InstrumentFollowS, Settings.InstrumentFieldDeg, 1);
        }
        double dd = Of("tb_destroyer"), bb = Of("dreadnought");
        output.WriteLine($"a ×24 abeam, the sea on the beam: destroyer {dd:P0}, dreadnought {bb:P0}");
        Assert.True(dd < bb);
        Assert.True(bb > 0.7, "generous to the gunner: a battleship mostly holds her");
    }
}
