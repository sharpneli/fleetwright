using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>How an observer and a target stand for one look (docs/battle/plan.md step 3.2c): the distance over the
/// sphere, the target's aspect (the angle between its heading and the line from it to the observer: 0 bow on, 90 side on),
/// the visibility and the light.</summary>
public readonly record struct SightLine(double DistanceM, double AspectDeg, double VisibilityM, double SunElevationDeg, bool Silhouetted);

/// <summary>The sighting model (docs/battle/plan.md step 3.2c): whether an eye can make out a ship, and how fast it does,
/// from the geometry and the air. Pure functions of numbers; no state, no allocation.
/// <list type="number">
/// <item><b>The horizon</b> hides the target's lower part: from an eye h over the sea it's d_h = √(2 k R h) away (k the
/// refraction, 7/6: 3.86 √h km), and beyond it the target is hidden to (d - d_h)² / (2 k R).</item>
/// <item><b>What shows</b> is its silhouette above that: side on and end on by its aspect
/// (<see cref="SilhouetteRow"/>), as a solid angle through the eye's magnification.</item>
/// <item><b>The air</b> fades its contrast: C = C₀ e^(-σ d) (Koschmieder). A visibility V means what the logs mean, how
/// far a ship is made out: σ is the one at which a reference ship is just made out at V. In the dark the reach is cut to
/// the night's. C₀ is a grey ship's against the sky, or a silhouette's against a bright horizon.</item>
/// <item><b>The eye</b> needs a threshold contrast, rising for small objects as their solid angle shrinks
/// (Cₜ = C∞ (1 + √(Ω꜀ / Ω))). Its margin over it, m = C / Cₜ, makes the target visible at 1 and plainly so at the full
/// margin.</item>
/// <item><b>Detection is a rate</b> (the full rate times the log of the margin, from 1 to the full margin), so the chance in
/// a look of dt is 1 - e^(-rate·dt) and doesn't depend on how often it's looked at.</item>
/// </list></summary>
public static class SightModel
{

    /// <summary>How far the sea's horizon is from an eye this high, m.</summary>
    public static double HorizonM(double eyeM, SimSettings s) => Math.Sqrt(2 * s.SightRefraction * Earth.Radius * Math.Max(eyeM, 0));

    /// <summary>How much of a target this far away the horizon hides from an eye this high, m.</summary>
    public static double HiddenM(double eyeM, double distanceM, SimSettings s)
    {
        double beyond = distanceM - HorizonM(eyeM, s);
        return beyond <= 0 ? 0 : beyond * beyond / (2 * s.SightRefraction * Earth.Radius);
    }

    /// <summary>The target's area that shows over the horizon at its aspect, m².</summary>
    public static double VisibleAreaM2(ShipClass target, double hiddenM, double aspectDeg)
    {
        var sil = target.Silhouette;
        double a = aspectDeg * Math.PI / 180;
        return Sight.AreaAbove(target.Numbers(sil.SideAreaM2), hiddenM) * Math.Abs(Math.Sin(a)) +
               Sight.AreaAbove(target.Numbers(sil.EndAreaM2), hiddenM) * Math.Abs(Math.Cos(a));
    }

    /// <summary>How far sight reaches in this light, m: unlimited by day, the night's reach in the dark, and between, as
    /// the sun goes from the horizon to <see cref="SimSettings.SightNightSunDeg"/>, geometrically from a hundred times it.</summary>
    public static double LightReachM(double sunElevationDeg, SimSettings s)
    {
        if (sunElevationDeg >= 0)
            return double.PositiveInfinity;
        double f = Math.Clamp(sunElevationDeg / s.SightNightSunDeg, 0, 1);   // 0 at sunset, 1 at night
        return s.SightNightM * Math.Pow(100, 1 - f);
    }

    /// <summary>An eye's magnification: a director's rangefinder's (its make's), binoculars at a masthead, on the bridge
    /// or in a director without a rangefinder read, the conning tower's slits.</summary>
    public static double Magnification(in EyeRow eye, SimSettings s) => eye.Kind switch
    {
        EyeKind.Director or EyeKind.Turret or EyeKind.Rangefinder when eye.Ranges => eye.Optics.Magnification,
        EyeKind.ConningTower => s.SightSlitsMagnification,
        _ => s.SightLookoutMagnification,
    };

    /// <summary>The share of the full detection rate an eye searches at: a director's narrow field less than binoculars.</summary>
    public static double SearchFactor(in EyeRow eye, SimSettings s) => eye.Kind == EyeKind.Director ? s.SightDirectorSearchFactor : 1;

    /// <summary>The threshold contrast for an object of this solid angle (as magnified), sr.</summary>
    public static double Threshold(double omega, SimSettings s) =>
        s.SightThresholdContrast * (1 + Math.Sqrt(s.SightCriticalSolidAngle / Math.Max(omega, 1e-30)));

    /// <summary>The haze's extinction, per metre, for a reach (a visibility, or the light's): the one at which the
    /// reference ship (<see cref="SimSettings.SightReferenceAreaM2"/> side on, a lookout's binoculars) is made out at
    /// exactly that reach by day. None (0) for an unlimited reach.</summary>
    public static double Extinction(double reachM, SimSettings s)
    {
        if (double.IsPositiveInfinity(reachM))
            return 0;
        double m = s.SightLookoutMagnification;
        double omega = s.SightReferenceAreaM2 * m * m / (reachM * reachM);
        return Math.Max(Math.Log(s.SightContrastDay / Threshold(omega, s)), 0.05) / reachM;
    }

    /// <summary>The margin of a target's apparent contrast over the eye's threshold for it (≥ 1: it can be made out);
    /// 0 when nothing of it shows over the horizon.</summary>
    public static double Margin(double eyeM, double magnification, ShipClass target, in SightLine line, SimSettings s)
    {
        double d = Math.Max(line.DistanceM, 1);
        double area = VisibleAreaM2(target, HiddenM(eyeM, d, s), line.AspectDeg);
        if (area <= 0)
            return 0;
        return MarginOfArea(area, d, magnification, line, s);
    }

    /// <summary>The margin for an area showing at a distance (<see cref="Margin"/> after the horizon and aspect).</summary>
    public static double MarginOfArea(double areaM2, double distanceM, double magnification, in SightLine line, SimSettings s)
    {
        double reach = line.Silhouetted ? line.VisibilityM : Math.Min(line.VisibilityM, LightReachM(line.SunElevationDeg, s));
        double c0 = line.Silhouetted ? s.SightContrastSilhouette : s.SightContrastDay;
        double contrast = c0 * Math.Exp(-Extinction(reach, s) * distanceM);
        return contrast / Threshold(areaM2 * magnification * magnification / (distanceM * distanceM), s);
    }

    /// <summary>The detection rate at a margin, per second: none below 1, full at <see cref="SimSettings.SightFullMargin"/>.</summary>
    public static double Rate(double margin, double searchFactor, SimSettings s) =>
        margin <= 1 ? 0 : s.SightRatePerS * searchFactor * Math.Min(1, Math.Log(margin) / Math.Log(s.SightFullMargin));

    /// <summary>The chance of detecting it in a look of dt seconds at a rate.</summary>
    public static double Chance(double rate, double dtS) => rate <= 0 ? 0 : 1 - Math.Exp(-rate * dtS);

    /// <summary>A reading's 1-sigma range error, m: a rangefinder's unit of error (its make's, fire control's one law:
    /// <see cref="FireControl.RangefinderUnitM"/>), or the eye's 20% of the range.</summary>
    public static double RangeSigmaM(in EyeRow eye, double rangeM) =>
        eye.Ranges ? FireControl.RangefinderUnitM(eye.RangefinderM, eye.Optics, rangeM) : 0.2 * rangeM;

    /// <summary>A reading's 1-sigma bearing error, radians: a director's 0.5 mil, binoculars' 2 mil (the reference's
    /// optical and eye figures), the slits' 5.</summary>
    public static double BearingSigmaRad(in EyeRow eye) => eye.Kind switch
    {
        EyeKind.Director => 0.0005,
        EyeKind.Turret => 0.0015,   // fire-control-designer.md §3.1: a turret's 1.5 mil, elsewhere 1
        EyeKind.Rangefinder => 0.001,
        EyeKind.ConningTower => 0.005,
        _ => 0.002,
    };
}
