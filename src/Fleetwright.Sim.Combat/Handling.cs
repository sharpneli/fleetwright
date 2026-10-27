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

    /// <summary>How close the ship's centre may come to the coast, metres (the backstop's line: half its beam and
    /// <see cref="SimSettings.CoastMinClearanceM"/>), and where it keeps when it slides along a coast, its standoff
    /// (docs/world/terrain-plan.md, "Standoff and minimum").</summary>
    public double MinClearanceM { get; }
    public double StandoffM { get; }

    readonly double crossover;   // the speed below which the bollard cap, not eta P / U, limits thrust: eta / bollard, m/s

    public Handling(ShipClass c, SimSettings s)
    {
        var m = c.Motion;
        crossover = s.PropulsiveEfficiency / s.BollardThrustPerWatt;
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
        MinClearanceM = m.BeamM / 2 + s.CoastMinClearanceM;
        StandoffM = Math.Max(Math.Clamp(MinClearanceM + s.CoastStandoffLengths * m.LengthM, s.CoastStandoffMinM, s.CoastStandoffMaxM), 1.5 * MinClearanceM);
        (CoarseAccelUp, CoarseAccelDown, CoarseAccelAstern) = Calibrate(s);
    }

    /// <summary>The coarse tier's constant accelerations, m/s² (docs/battle/plan.md step 2.8): working up ahead, slowing
    /// ahead, and going astern or stopping by it. Each is the fine model's own over its sea trial (0 to 90% of the top
    /// speed at full power; the top speed to half with the engines stopped; the top speed to a stop at full astern), so
    /// coarse ships take as long as fine ones over those.</summary>
    public double CoarseAccelUp { get; }
    public double CoarseAccelDown { get; }
    public double CoarseAccelAstern { get; }

    /// <summary>The fine model's trials, on a ship of its own at a step of <see cref="SimSettings.CoarseCalibrationStepS"/>
    /// (the step's forces are stable at any step): once per class in a world, a few thousand steps.</summary>
    (double Up, double Down, double Astern) Calibrate(SimSettings s)
    {
        var store = new ShipStore(s with { ShipCapacity = 1, RouteCapacity = 1 });
        double maxKn = Class.Motion.MaxSpeedKn, top = SteadySpeedMs(PowerFor(maxKn)), dt = s.CoarseCalibrationStepS;
        if (!(top > 0))
            return (1, 1, 1);
        var p = Earth.Position(0, 0);
        var f = Earth.Forward(p, 0);
        double Trial(double fromMs, double orderKn, Func<double, bool> done)
        {
            int i = store.Spawn(-1, 0, this, p, f, fromMs, 0);
            store.OrderSpeed(i, orderKn);
            double t = 0;
            while (!done(store.speed[i]) && t < s.CoarseCalibrationMaxS)
            {
                ShipMotion.Step(store, i, dt, s);
                t += dt;
            }
            store.Drop(i);
            return Math.Max(t, dt);
        }
        double up = 0.9 * top / Trial(0, maxKn, u => u >= 0.9 * top);
        double down = 0.5 * top / Trial(top, 0, u => u <= 0.5 * top);
        double astern = top / Trial(top, -maxKn, u => u <= 0);
        return (up, down, astern);
    }

    /// <summary>The rudder whose steady turn has a curvature, rad/m, positive to starboard: what a coarse ship's turn
    /// hands the fine tick, so the swing carries on as it was.</summary>
    public double RudderFor(double curvature)
    {
        double y = Math.Min(Math.Abs(curvature) * SteadyRadiusM, 1);   // the effect, as a share of hard over's
        return Math.Sign(curvature) * HardOverRad * (1 - Math.Sqrt(1 - y));   // Effective's x(2 - x), inverted
    }

    /// <summary>The shaft power the hull needs at a speed through the water, watts (either way: astern as ahead).</summary>
    public double PowerAtW(double speedMs) => Class.PowerAt(Math.Abs(speedMs) / MsPerKnot) * WattsPerShp;

    /// <summary>The power ordered for a speed, watts: what holds it straight ahead under the step's forces, within what
    /// the plant makes; negative astern. Above the bollard crossover that's the hull's own curve; below it the thrust
    /// per watt is capped, so holding the speed takes more (the engines turn faster for it, as a real ship's would).</summary>
    public double PowerFor(double kn) => kn >= 0
        ? Math.Min(HoldingPowerW(kn * MsPerKnot), MaxPowerW)
        : -Math.Min(HoldingPowerW(-kn * MsPerKnot), AsternPowerW);

    /// <summary>The power that holds a speed straight ahead under the step's forces, watts.</summary>
    public double HoldingPowerW(double speedMs) => PowerAtW(speedMs) * Math.Max(1, crossover / Math.Max(speedMs, 1e-9));

    /// <summary>The speed a power holds straight ahead under the step's forces, m/s: <see cref="HoldingPowerW"/>'s
    /// inverse (it rises), by bisection; nothing allocated.</summary>
    public double SteadySpeedMs(double powerW)
    {
        if (powerW <= 0)
            return 0;
        double lo = 0, hi = Class.PowerCurveKn[^1] * MsPerKnot;
        if (HoldingPowerW(hi) <= powerW)
            return hi;
        for (int k = 0; k < 60; k++)
        {
            double mid = 0.5 * (lo + hi);
            if (HoldingPowerW(mid) < powerW)
                lo = mid;
            else
                hi = mid;
        }
        return 0.5 * (lo + hi);
    }

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

    /// <summary>How far behind a coarse turn's start at once the fine tick's heading runs, s (docs/battle/coarse.md J4):
    /// the rudder put over to <paramref name="rudderRad"/> at its gear's rate (half its travel's time) and the swing built
    /// over Nomoto's T at the speed, the lag of the fine model's own first-order answer to the helm. A coarse turn runs
    /// straight on for it first.</summary>
    public double HelmLagS(double speedMs, double rudderRad, SimSettings s) => T(speedMs, s) + Math.Abs(rudderRad) / RudderRateRadS / 2;

    /// <summary>Nomoto's T at a speed, seconds.</summary>
    public double T(double speedMs, SimSettings s) => TPrime * LengthM / Math.Max(Math.Abs(speedMs), s.NomotoMinSpeedMs);
}
