using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a class's facts become for moving it: worked out once per class from its <see cref="MotionFacts"/>
/// and the world's <see cref="SimSettings"/>, held beside the class, never in it, so tuning the model changes neither
/// the class nor its hash (docs/battle/plan.md step 2.2). SI units: metres, seconds, kilograms, watts, radians.</summary>
public sealed class Handling
{
    public const double WattsPerShp = 745.7;
    public const double MsPerKnot = 1852.0 / 3600;

    public ShipClass Class { get; }
    public double LengthM { get; }

    /// <summary>The displacement at full load with the water moved along with it.</summary>
    public double MassKg { get; }

    /// <summary>The steady radius of a hard-over turn, and the rudder that makes it.</summary>
    public double SteadyRadiusM { get; }
    public double HardOverRad { get; }
    public double RudderRateRadS { get; }

    /// <summary>Nomoto's T' with the single-rudder factor in: T = TPrime·L/U.</summary>
    public double TPrime { get; }

    /// <summary>The most the plant makes ahead (its rating at overload) and astern, and how fast its power grows and
    /// falls, W/s (falling is the throttle shut, far quicker than raising steam).</summary>
    public double MaxPowerW { get; }
    public double AsternPowerW { get; }
    public double PowerRateWs { get; }
    public double PowerCutWs { get; }

    /// <summary>The extra resistance at a steady hard-over turn, as a multiple of the straight-ahead resistance: with
    /// the power fixed and P ∝ U³, it loses the fraction f of speed when (1 - f)³·(1 + g) = 1.</summary>
    public double TurnDragFactor { get; }

    public Handling(ShipClass c, SimSettings s)
    {
        var m = c.Motion;
        Class = c;
        LengthM = m.LengthM;
        MassKg = m.FullDisplacementT * 1000 * (1 + s.AddedMassFraction);
        SteadyRadiusM = s.SteadyRadiusOverTd * m.TacticalDiameterM;
        HardOverRad = m.HardOverDeg * Math.PI / 180;
        RudderRateRadS = m.RudderRateDegS * Math.PI / 180;
        TPrime = s.NomotoTPrime * (m.Rudders >= 2 ? 1 : s.SingleRudderTFactor);
        MaxPowerW = Math.Max(m.RatedShp * Math.Max(m.OverloadMax, 1), m.PowerShp) * WattsPerShp;
        AsternPowerW = MaxPowerW * s.AsternPowerFraction;
        PowerRateWs = MaxPowerW / m.PowerRampS;
        PowerCutWs = MaxPowerW / m.PowerCutS;
        double slender = Math.Clamp((m.LengthM / m.BeamM - 7) / 3, 0, 1);
        double f = s.TurnSpeedLossBeamy + (s.TurnSpeedLossSlender - s.TurnSpeedLossBeamy) * slender;
        TurnDragFactor = 1 / ((1 - f) * (1 - f) * (1 - f)) - 1;
    }

    /// <summary>The shaft power the hull needs at a speed through the water, watts (either way: astern as ahead).</summary>
    public double PowerAtW(double speedMs) => Class.PowerAt(Math.Abs(speedMs) / MsPerKnot) * WattsPerShp;

    /// <summary>The power ordered for a speed, watts: what holds it straight ahead, within what the plant makes;
    /// negative astern.</summary>
    public double PowerFor(double kn) => kn >= 0
        ? Math.Min(Class.PowerAt(kn) * WattsPerShp, MaxPowerW)
        : -Math.Min(Class.PowerAt(-kn) * WattsPerShp, AsternPowerW);

    /// <summary>The rudder's effect for its angle: hard over gives its full effect, less gives more than its share
    /// (δ_eff/δmax = x(2 - x), x = |δ|/δmax: 15° of 35° turns in 1.53 times the hard-over circle, 25° in 1.15, near
    /// the trawler trials of manoeuvring.md §4: 1.56 and 1.15).</summary>
    public double Effective(double rudder)
    {
        double x = Math.Min(Math.Abs(rudder) / HardOverRad, 1);
        return Math.Sign(rudder) * HardOverRad * x * (2 - x);
    }

    /// <summary>The steady rate of turn a rudder gives at a speed, rad/s, positive to starboard.</summary>
    public double SteadyRate(double rudder, double speedMs) => speedMs * Effective(rudder) / (SteadyRadiusM * HardOverRad);

    /// <summary>The radius a rudder turns in, metres.</summary>
    public double RadiusFor(double rudder) => SteadyRadiusM * HardOverRad / Math.Max(Math.Abs(Effective(rudder)), 1e-9);

    /// <summary>Nomoto's T at a speed, seconds.</summary>
    public double T(double speedMs, SimSettings s) => TPrime * LengthM / Math.Max(Math.Abs(speedMs), s.NomotoMinSpeedMs);
}
