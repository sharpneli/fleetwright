using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>How an observer and a target stand for one look (docs/battle/plan.md step 3.2c): the distance over the
/// sphere, the target's aspect (the angle between its heading and the line from it to the observer: 0 bow on, 90 side on),
/// the visibility and the light; and the smoke on the line from the eye (docs/battle/smoke.md Z3), none by default.</summary>
public readonly record struct SightLine(double DistanceM, double AspectDeg, double VisibilityM, double SunElevationDeg, bool Silhouetted,
    SmokeTau Smoke = default);

/// <summary>The smoke's optical depths on the lines from an eye to the target (docs/battle/smoke.md Z3): to three points
/// along her (the middles of her fore, middle and aft thirds: F, M, A), each at three heights
/// (<see cref="SimSettings.SmokeBands"/> of her silhouette's top, low to high: 0, 1, 2). Added to the haze's
/// (τ = σ·d + τ_smoke: the one optical depth), so the contrast falls as e^(−τ).</summary>
public readonly record struct SmokeTau(double F0, double F1, double F2, double M0, double M1, double M2, double A0, double A1, double A2)
{
    /// <summary>The same depths at every point along her: low, middle and high.</summary>
    public SmokeTau(double low, double mid, double high) : this(low, mid, high, low, mid, high, low, mid, high) { }

    /// <summary>Nine depths, point-major (fore, middle, aft; each low to high).</summary>
    public static SmokeTau Of(ReadOnlySpan<double> t) => new(t[0], t[1], t[2], t[3], t[4], t[5], t[6], t[7], t[8]);

    /// <summary>No smoke on the lines.</summary>
    public bool Clear => F0 == 0 && F1 == 0 && F2 == 0 && M0 == 0 && M1 == 0 && M2 == 0 && A0 == 0 && A1 == 0 && A2 == 0;

    /// <summary>The depth to a point (0 fore, 1 middle, 2 aft) at a band.</summary>
    public double this[int point, int band] => (point * 3 + band) switch
    {
        0 => F0, 1 => F1, 2 => F2, 3 => M0, 4 => M1, 5 => M2, 6 => A0, 7 => A1, _ => A2,
    };

    /// <summary>The middle's depths, low, middle and high.</summary>
    public double Low => M0;
    public double Mid => M1;
    public double High => M2;
}

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

    /// <summary>The furthest an eye this high can see anything of a ship this tall, m: the two horizons (fire-control.md
    /// F4d: what the world's reach is derived from).</summary>
    public static double ReachM(double eyeM, double topM, SimSettings s) => HorizonM(eyeM, s) + HorizonM(topM, s);

    /// <summary>The furthest range an eye makes the target out at all (a margin of 1), m, to 100 m: for listings and
    /// tests, not the sim's looks.</summary>
    public static double MadeOutAtM(double eyeM, double magnification, ShipClass target, double visibilityM, double aspectDeg, double sunDeg,
        bool silhouetted, SimSettings s)
    {
        for (double d = Math.Floor(ReachM(eyeM, target.Silhouette.TopM, s) / 100) * 100; d > 100; d -= 100)
            if (Margin(eyeM, magnification, target, new SightLine(d, aspectDeg, visibilityM, sunDeg, silhouetted), s) >= 1)
                return d;
        return 0;
    }

    /// <summary>The median range an observer's eyes first sight a target at, closing on it at a steady speed from
    /// beyond reach, m: where their rates (its <see cref="SimSettings.SightEyes"/> highest, all bearing) have added up to
    /// an even chance (a cumulative hazard of ln 2), in 100 m steps; 0 if not before they meet. For listings and the
    /// record's anchors (fire-control.md F4d), not the sim's looks.</summary>
    public static double FirstSightingM(ShipClass observer, ShipClass target, double visibilityM, double aspectDeg, double closingMs, SimSettings s)
    {
        var eyes = observer.Eyes;
        int n = Math.Min(eyes.Length, s.SightEyes);
        double hazard = 0, step = 100, dt = step / closingMs;
        for (double d = Math.Floor(ReachM(n > 0 ? eyes[0].HeightM : 0, target.Silhouette.TopM, s) / step) * step; d > step; d -= step)
        {
            var line = new SightLine(d, aspectDeg, visibilityM, 30, false);
            for (int i = 0; i < n; i++)
                hazard += Rate(Margin(eyes[i].HeightM, Magnification(eyes[i], s), target, line, s), SearchFactor(eyes[i], s), s) * dt;
            if (hazard >= Math.Log(2))
                return d;
        }
        return 0;
    }

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

    /// <summary>A ship's fires as a light at night (docs/battle/fire.md B2; 17 §8.5), its margin over what an eye this high
    /// makes out: the heat that shows (MW) as a lamp of <see cref="SimSettings.FireGlowCdPerMW"/>, at a height on the ship,
    /// through the haze by Allard's law (3.912 / the visibility), against <see cref="SimSettings.FireGlowLux"/> in the dark
    /// and a hundred times it at sunset, as <see cref="LightReachM"/> ramps; none by day or below the horizon. Smoke on the
    /// line at its height (docs/battle/smoke.md Z3) dims it as the haze does.</summary>
    public static double GlowMargin(double eyeM, double fireMW, double heightM, double distanceM, double visibilityM, double sunElevationDeg,
        SimSettings s, double smokeTau = 0)
    {
        if (fireMW <= 0 || sunElevationDeg >= 0 || HiddenM(eyeM, distanceM, s) >= heightM)
            return 0;
        double f = Math.Clamp(sunElevationDeg / s.SightNightSunDeg, 0, 1), d = Math.Max(distanceM, 1);
        double sigma = double.IsPositiveInfinity(visibilityM) || visibilityM <= 0 ? 0 : 3.912 / visibilityM;
        return s.FireGlowCdPerMW * fireMW * Math.Exp(-sigma * d - smokeTau) / (d * d * s.FireGlowLux * Math.Pow(100, 1 - f));
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
        double d = Math.Max(line.DistanceM, 1), hidden = HiddenM(eyeM, d, s);
        double area = VisibleAreaM2(target, hidden, line.AspectDeg);
        if (area <= 0)
            return 0;
        if (line.Smoke.Clear)
            return MarginOfArea(area, d, magnification, line, s);
        // smoke on the lines (docs/battle/smoke.md Z3): her height in three slabs about the bands, her length in thirds,
        // each cell with its point's and band's depth (side on a third of the slab each, end on the slab all at her end
        // nearest the eye); the eye makes out the best of any patch of adjacent thirds by adjacent slabs (the whole ship,
        // her upper works, her fore part clear of her smoke astern, a hull under smoke lying high), its area at the
        // contrast through the worst depth on it
        var bands = s.SmokeBands;
        var sil = target.Silhouette;
        double top = sil.TopM, rad = line.AspectDeg * Math.PI / 180, sin = Math.Abs(Math.Sin(rad)), cos = Math.Abs(Math.Cos(rad));
        var sideTable = target.Numbers(sil.SideAreaM2);
        var endTable = target.Numbers(sil.EndAreaM2);
        Span<double> cut = stackalloc double[4];
        (cut[0], cut[1], cut[2], cut[3]) = (hidden, Math.Max(hidden, top * (bands[0] + bands[1]) / 2), Math.Max(hidden, top * (bands[1] + bands[2]) / 2), double.MaxValue);
        Span<double> side = stackalloc double[3];
        Span<double> end = stackalloc double[3];
        for (int j = 0; j < 3; j++)
        {
            double over = j < 2 ? Sight.AreaAbove(sideTable, cut[j + 1]) : 0, overEnd = j < 2 ? Sight.AreaAbove(endTable, cut[j + 1]) : 0;
            side[j] = (Sight.AreaAbove(sideTable, cut[j]) - over) * sin / 3;
            end[j] = (Sight.AreaAbove(endTable, cut[j]) - overEnd) * cos;
        }
        int near = Math.Cos(rad) >= 0 ? 0 : 2;   // her bow toward the eye, or her stern
        Span<double> through = stackalloc double[9];
        for (int p = 0; p < 3; p++)
            for (int j = 0; j < 3; j++)
                through[p * 3 + j] = Math.Exp(-line.Smoke[p, j]);
        double contrast = Contrast(d, line, s), m2 = magnification * magnification / (d * d), best = 0;
        for (int p0 = 0; p0 < 3; p0++)
            for (int p1 = p0; p1 < 3; p1++)
                for (int j0 = 0; j0 < 3; j0++)
                {
                    double a = 0, t = 1;
                    for (int j1 = j0; j1 < 3; j1++)
                    {
                        for (int p = p0; p <= p1; p++)
                        {
                            a += side[j1] + (p == near ? end[j1] : 0);
                            t = Math.Min(t, through[p * 3 + j1]);
                        }
                        if (a > 0)
                            best = Math.Max(best, contrast * t / Threshold(a * m2, s));
                    }
                }
        return best;
    }

    /// <summary>The margin for an area showing at a distance (<see cref="Margin"/> after the horizon and aspect), through
    /// the haze alone.</summary>
    public static double MarginOfArea(double areaM2, double distanceM, double magnification, in SightLine line, SimSettings s) =>
        Contrast(distanceM, line, s) / Threshold(areaM2 * magnification * magnification / (distanceM * distanceM), s);

    /// <summary>The margin an eye makes out a ship's smoke at (docs/battle/smoke.md Z6, <see cref="SmokePlume"/>): what of
    /// it shows over the horizon at the view (the sine of the line of sight against its trail), at its own contrast through
    /// the haze (the visibility, or the light's reach: smoke makes no silhouette of its own), against the eye's threshold
    /// for that solid angle. 0 when none of it shows.</summary>
    public static double SmokeMargin(double eyeM, double magnification, in SmokePlume plume, double sinView, in SightLine line, SimSettings s)
    {
        double d = Math.Max(line.DistanceM, 1);
        var (area, shade) = plume.Showing(HiddenM(eyeM, d, s), sinView);
        if (!(area > 0) || !(shade > 0))
            return 0;
        double reach = Math.Min(line.VisibilityM, LightReachM(line.SunElevationDeg, s));
        return shade * Math.Exp(-Extinction(reach, s) * d) / Threshold(area * magnification * magnification / (d * d), s);
    }

    /// <summary>The farthest any of a plume could be made out, m (<see cref="SmokeMargin"/>'s bound): its darkest
    /// smoke's contrast faded through the haze (or the light's reach) to the eye's threshold for the largest object;
    /// 0 when it never shows (the dark, or no smoke dark enough).</summary>
    public static double SmokeFarthestM(in SmokePlume plume, double visibilityM, double sunElevationDeg, SimSettings s)
    {
        var v = plume.Puffs;
        double shade = Math.Max(Math.Max(Math.Max(v.S0, v.S1), Math.Max(v.S2, v.S3)), plume.Contrast);
        if (!(shade > s.SightThresholdContrast))
            return 0;
        double ext = Extinction(Math.Min(visibilityM, LightReachM(sunElevationDeg, s)), s);
        return ext > 0 ? Math.Log(shade / s.SightThresholdContrast) / ext : double.PositiveInfinity;
    }

    /// <summary>A target's apparent contrast through the haze at a distance: a grey ship's against the sky, or a silhouette's
    /// against a bright horizon, faded by the visibility (or the light's reach, whichever is shorter).</summary>
    static double Contrast(double distanceM, in SightLine line, SimSettings s)
    {
        double reach = line.Silhouetted ? line.VisibilityM : Math.Min(line.VisibilityM, LightReachM(line.SunElevationDeg, s));
        double c0 = line.Silhouetted ? s.SightContrastSilhouette : s.SightContrastDay;
        return c0 * Math.Exp(-Extinction(reach, s) * distanceM);
    }

    /// <summary>The smoke's depth on the line to her middle at a height, between its bands (held beyond the lowest and the
    /// highest).</summary>
    public static double SmokeAt(in SmokeTau tau, double heightM, double topM, SimSettings s)
    {
        if (tau.Clear)
            return 0;
        var bands = s.SmokeBands;
        double h = heightM / Math.Max(topM, 1e-6);
        if (h <= bands[0])
            return tau.Low;
        if (h >= bands[2])
            return tau.High;
        return h <= bands[1]
            ? tau.Low + (tau.Mid - tau.Low) * (h - bands[0]) / (bands[1] - bands[0])
            : tau.Mid + (tau.High - tau.Mid) * (h - bands[1]) / (bands[2] - bands[1]);
    }

    /// <summary>The detection rate at a margin, per second: none below 1, full at <see cref="SimSettings.SightFullMargin"/>.</summary>
    public static double Rate(double margin, double searchFactor, SimSettings s) =>
        margin <= 1 ? 0 : s.SightRatePerS * searchFactor * Math.Min(1, Math.Log(margin) / Math.Log(s.SightFullMargin));

    /// <summary>The chance of detecting it in a look of dt seconds at a rate.</summary>
    public static double Chance(double rate, double dtS) => rate <= 0 ? 0 : 1 - Math.Exp(-rate * dtS);

    /// <summary>A reading's 1-sigma range error, m, at a margin through the eye: a rangefinder's unit of error (its make's,
    /// fire control's one law: <see cref="FireControl.RangefinderUnitM"/>) times the air's factor
    /// (<see cref="RangingFactor"/>), or the eye's 20% of the range.</summary>
    public static double RangeSigmaM(in EyeRow eye, double rangeM, double margin, SimSettings s) =>
        eye.Ranges
            ? FireControl.RangefinderUnitM(eye.RangefinderM, eye.Optics, FireControl.Magnification(eye.Optics, eye.Support, eye.HeightM), rangeM) *
              RangingFactor(margin, eye.Optics, s)
            : 0.2 * rangeM;

    /// <summary>How much the air worsens a rangefinder's readings (fire-control.md F5c), from the target's margin over the
    /// threshold contrast through it (haze, the light, a small, distant or hull-down target, a silhouette against a low
    /// sun all in it): 1 from <see cref="SimSettings.RangingClearMargin"/> up, under it (clear / margin) to the kind's power,
    /// a coincidence set's more than a stereo set's; coated glass takes a share of the excess. Its error and bias take it,
    /// its bearing's the square root (fire_control_ref.py's k_optic).</summary>
    public static double RangingFactor(double margin, in OpticsRow o, SimSettings s)
    {
        if (margin >= s.RangingClearMargin)
            return 1;
        double k = Math.Pow(s.RangingClearMargin / Math.Max(margin, 1), o.Kind == InstrumentKind.Stereo ? s.RangingPowerStereo : s.RangingPowerCoincidence);
        return o.Coated ? 1 + s.RangingCoatedShare * (k - 1) : k;
    }

    /// <summary>A reading's 1-sigma bearing error, radians, by the post: a director's 0.7 mil, a gunhouse's 1.5, a control
    /// position's rangefinder 1 (fire-control-designer.md §3.1), binoculars' 2 mil (the reference's eye figure), the
    /// slits' 5. The one law: the picture's fixes and fire control's readings both take it.</summary>
    public static double BearingSigmaRad(in EyeRow eye) => eye.Kind switch
    {
        EyeKind.Director => 0.0007,
        EyeKind.Turret => 0.0015,
        EyeKind.Rangefinder => 0.001,
        EyeKind.ConningTower => 0.005,
        _ => 0.002,
    };
}
