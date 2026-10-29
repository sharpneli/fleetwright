using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>How an observer and a target stand for one look (docs/battle/plan.md step 3.2c): the distance over the
/// sphere, the target's aspect (the angle between its heading and the line from it to the observer: 0 bow on, 90 side on),
/// the visibility; the light (docs/battle/night.md Q1): the horizon sky's luminance, cd/m² (<see cref="NightSky.SkyCdM2"/>),
/// and how far she stands dark against a bright horizon beyond her, 0 to 1 (a low sun's, the moon's path); the smoke on
/// the line from the eye (docs/battle/smoke.md Z3), none by default; her speed through the water, m/s (her bow wave, 0:
/// none asked); the share of the sky's light the eye's glass brings to it (<see cref="SightModel.GlassLight"/>, 1 the
/// naked eye); a light's illuminance on her side toward the eye, lux (docs/battle/night.md Q2: a fire near her, later a
/// beam or a flare); the luminance the eye is adapted to if more than the scene through its glass (its own guns'
/// flash, <see cref="SightModel.DazzleCdM2"/>; 0: the scene's); and a veil's luminance over the scene, cd/m²
/// (docs/battle/night.md Q4: her own beam's light scattered back down the line, a beam's glare in the eye), which lowers
/// her contrast as it brightens what the eye is adapted to.</summary>
public readonly record struct SightLine(double DistanceM, double AspectDeg, double VisibilityM, double SkyCdM2, double Silhouette,
    SmokeTau Smoke = default, double SpeedMs = 0, double Glass = 1, double LitLux = 0, double AdaptedCdM2 = 0, double VeilCdM2 = 0)
{
    /// <summary>The share of her contrast against the sky a veil leaves: L / (L + L_veil).</summary>
    public double VeilShare => VeilCdM2 > 0 ? SkyCdM2 / (SkyCdM2 + VeilCdM2) : 1;
}

/// <summary>How the dark raises the eye's threshold (docs/battle/night.md Q1), both ≥ 1 and exactly 1 by day: the large
/// object's threshold contrast by <paramref name="Floor"/>, and the patch the eye sums its light over (the critical solid
/// angle's side) by <paramref name="Summation"/>, so small and distant things fade first (<see cref="SightModel.DarkOf"/>).</summary>
public readonly record struct Dark(double Floor, double Summation)
{
    /// <summary>Daylight: nothing added.</summary>
    public static readonly Dark Day = new(1, 1);

    /// <summary>Whether the dark adds anything.</summary>
    public bool Any => Floor > 1 || Summation > 1;
}

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
/// far a ship is made out by day: σ is the one at which a reference ship is just made out at V. C₀ is a grey ship's
/// against the sky in the sun, darker against it in the sky's light alone (after sunset, under cloud), or a silhouette's
/// against a bright horizon.</item>
/// <item><b>The eye</b> needs a threshold contrast, rising for small objects as their solid angle shrinks
/// (Cₜ = C∞ (1 + √(Ω꜀ / Ω))), and in the dark (docs/battle/night.md Q1): C∞ and Ω꜀ grow as the sky's luminance through the
/// eye's glass falls (<see cref="DarkOf"/>). Its margin over it, m = C / Cₜ, makes the target visible at 1 and
/// plainly so at the full margin.</item>
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
    public static double MadeOutAtM(double eyeM, double magnification, ShipClass target, double visibilityM, double aspectDeg, double skyCdM2,
        double silhouette, SimSettings s, double glass = 1)
    {
        for (double d = Math.Floor(ReachM(eyeM, target.Silhouette.TopM, s) / 100) * 100; d > 100; d -= 100)
            if (Margin(eyeM, magnification, target, new SightLine(d, aspectDeg, visibilityM, skyCdM2, silhouette, Glass: glass), s) >= 1)
                return d;
        return 0;
    }

    /// <summary>The median range an observer's eyes first sight a target at, closing on it at a steady speed from
    /// beyond reach, m: where their rates (its <see cref="SimSettings.SightEyes"/> highest, all bearing) have added up to
    /// an even chance (a cumulative hazard of ln 2), in 100 m steps; 0 if not before they meet. By day unless a sky's
    /// luminance is given (docs/battle/night.md Q1: <see cref="NightSky.SkyCdM2"/>), the target at a speed for her bow
    /// wave. For listings and the record's anchors (fire-control.md F4d), not the sim's looks.</summary>
    public static double FirstSightingM(ShipClass observer, ShipClass target, double visibilityM, double aspectDeg, double closingMs, SimSettings s,
        double skyCdM2 = double.NaN, double speedMs = 0)
    {
        var eyes = observer.Eyes;
        int n = Math.Min(eyes.Length, s.SightEyes);
        double hazard = 0, step = 100, dt = step / closingMs, sky = double.IsNaN(skyCdM2) ? NightLight.ClearSkyCdM2(30) : skyCdM2;
        double pupil = PupilMm(sky);
        for (double d = Math.Floor(ReachM(n > 0 ? eyes[0].HeightM : 0, target.Silhouette.TopM, s) / step) * step; d > step; d -= step)
        {
            var line = new SightLine(d, aspectDeg, visibilityM, sky, 0, SpeedMs: speedMs);
            for (int i = 0; i < n; i++)
            {
                var eye = line with { Glass = GlassLight(eyes[i], pupil, s) };
                double mag = Magnification(eyes[i], s);
                hazard += Rate(Math.Max(Margin(eyes[i].HeightM, mag, target, eye, s), BowWaveMargin(eyes[i].HeightM, mag, target, eye, s)),
                    SearchFactor(eyes[i], s), s) * dt;
            }
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

    /// <summary>How far sight reaches in this light alone, m (docs/battle/night.md Q1): where a lookout's binoculars just make
    /// out the reference ship (<see cref="SimSettings.SightReferenceAreaM2"/> side on, in the sky's own light) in clear
    /// air under this sky (cd/m²); unlimited by day, where the dark adds nothing to the eye's threshold. To 10 m.</summary>
    public static double LightReachM(double skyCdM2, SimSettings s) =>
        !DarkOf(skyCdM2 * LookoutGlass(PupilMm(skyCdM2), s), s).Any ? double.PositiveInfinity : SeenToM(double.PositiveInfinity, skyCdM2, s);

    /// <summary>How far the reference ship is made out by a lookout's binoculars through the haze (a visibility) and the
    /// dark together, m, to 10 m (the captains' and the shadowers' "how far I can see"): the visibility itself by day.</summary>
    public static double SeenToM(double visibilityM, double skyCdM2, SimSettings s)
    {
        var dark = DarkOf(skyCdM2 * LookoutGlass(PupilMm(skyCdM2), s), s);
        if (!dark.Any && double.IsFinite(visibilityM))
            return visibilityM;
        double m = s.SightLookoutMagnification, sigma = Extinction(visibilityM, s), c0 = InherentContrast(skyCdM2, 0, s);
        double Over(double d) => c0 * Math.Exp(-sigma * d) / Threshold(s.SightReferenceAreaM2 * m * m / (d * d), dark, s);
        double lo = 1, hi = 1e7;
        if (Over(lo) < 1)
            return 0;
        while (hi - lo > 10)
        {
            double mid = 0.5 * (lo + hi);
            (lo, hi) = Over(mid) >= 1 ? (mid, hi) : (lo, mid);
        }
        return lo;
    }

    /// <summary>How far a torpedo's track is seen in this light, m, from its reach by day (docs/battle/night.md Q1), to a
    /// naked eye: a thin streak of foam seen at a grazing angle, its solid angle <see cref="SimSettings.TorpedoTrackSolidAngle"/>
    /// at its day's reach and shrinking as the cube of the distance (its length foreshortened by the eye's height over the
    /// range), its contrast what the day's threshold asks there; in the dark the range where the dark's threshold
    /// (<see cref="Threshold(double, in Dark, SimSettings)"/>) asks the same. 0 when no track shows at all. Its
    /// phosphorescence waits for the research.</summary>
    public static double TrackSeenM(double dayM, double skyCdM2, SimSettings s)
    {
        var dark = DarkOf(skyCdM2, s);
        if (!dark.Any)
            return dayM;
        double omegaDay = s.TorpedoTrackSolidAngle, c = Threshold(omegaDay, s);
        double x = (c / (s.SightThresholdContrast * dark.Floor) - 1) / dark.Summation;   // √(Ω꜀ / Ω) where it's just seen
        if (x <= 0)
            return 0;
        double omega = s.SightCriticalSolidAngle / (x * x);
        return Math.Min(dayM, dayM * Math.Cbrt(omegaDay / omega));
    }

    /// <summary>A ship's fires as a light at night (docs/battle/fire.md B2; 17 §8.5): the heat that shows (MW) as a lamp of
    /// <see cref="SimSettings.FireGlowCdPerMW"/> (<see cref="LightMargin"/>).</summary>
    public static double GlowMargin(double eyeM, double fireMW, double heightM, double distanceM, double visibilityM, double skyCdM2,
        SimSettings s, double smokeTau = 0) =>
        fireMW <= 0 ? 0 : LightMargin(eyeM, s.FireGlowCdPerMW * fireMW, heightM, distanceM, visibilityM, skyCdM2, s, smokeTau);

    /// <summary>A light's margin over what an eye this high makes out (docs/battle/night.md Q2: the one law for every
    /// source, a fire, a salvo's flash, a funnel's glow, later a searchlight's lamp and a flare): its intensity (cd, a
    /// flash's effective one) at a height, through the haze by Allard's law (3.912 / the visibility), against
    /// <see cref="SimSettings.FireGlowLux"/> under a dark sky, more against a brighter one by the eye's square root law
    /// (<see cref="SimSettings.FireGlowSkyCdM2"/>), so by day it barely counts; none below the horizon. Smoke on the line
    /// at its height (docs/battle/smoke.md Z3) dims it as the haze does; an eye dazzled by its own guns sees it as against
    /// the sky it's adapted to.</summary>
    public static double LightMargin(double eyeM, double candela, double heightM, double distanceM, double visibilityM, double skyCdM2,
        SimSettings s, double smokeTau = 0, double adaptedCdM2 = 0)
    {
        if (candela <= 0 || HiddenM(eyeM, distanceM, s) >= heightM)
            return 0;
        double d = Math.Max(distanceM, 1), sky = Math.Max(Math.Max(skyCdM2, 0), adaptedCdM2);
        double sigma = double.IsPositiveInfinity(visibilityM) || visibilityM <= 0 ? 0 : 3.912 / visibilityM;
        return candela * Math.Exp(-sigma * d - smokeTau) / (d * d * s.FireGlowLux * Math.Sqrt(1 + sky / s.FireGlowSkyCdM2));
    }

    /// <summary>The chance a flash is seen (docs/battle/night.md Q2): a light for an instant, so not a rate over time but
    /// once, rising from none at a margin of 1 to sure at the full margin, as a sighting's rate does.</summary>
    public static double FlashChance(double margin, SimSettings s) =>
        margin <= 1 ? 0 : Math.Min(1, Math.Log(margin) / Math.Log(s.SightFullMargin));

    /// <summary>What her own guns' flash leaves her eyes adapted to, cd/m² (docs/battle/night.md Q2): her salvo's light
    /// (cd, <c>CombatStore.LastFlash</c>) times <see cref="SimSettings.SightDazzleCdM2PerCd"/>, fading over
    /// <see cref="SimSettings.SightDazzleS"/> as the eyes recover [EST]. A ship firing fast in the dark sees less.</summary>
    public static double DazzleCdM2(double flashCd, double ageS, SimSettings s) =>
        flashCd <= 0 || ageS < 0 ? 0 : flashCd * s.SightDazzleCdM2PerCd * Math.Exp(-ageS / s.SightDazzleS);

    /// <summary>The eye's pupil, mm, adapted to a scene of this luminance (cd/m²): Moon and Spencer (1944),
    /// 4.9 − 3 tanh(0.4 log₁₀ L), about 3 mm by day and 7.5 in the dark.</summary>
    public static double PupilMm(double skyCdM2) => 4.9 - 3 * Math.Tanh(0.4 * Math.Log10(Math.Max(skyCdM2, 1e-9)));

    /// <summary>The share of the sky's light an eye's glass brings to it (docs/battle/night.md Q1), with its pupil this
    /// wide: the glass's transmission, and its exit pupil's area over the pupil's where it's the narrower (a 7×50 night
    /// glass fills a dark-adapted eye, a 6×30 half fills it, a rangefinder's narrow image far less). By day the pupil is
    /// narrow and only the transmission counts, which the bright day doesn't feel (<see cref="DarkOf"/>).</summary>
    public static double GlassLight(in EyeRow eye, double pupilMm, SimSettings s) =>
        Share(DesignerPlaceholders.Glass(eye.Kind, eye.Ranges, eye.Optics.Coated, Magnification(eye, s)), pupilMm);

    /// <summary>A lookout's binoculars' share (<see cref="GlassLight"/>).</summary>
    public static double LookoutGlass(double pupilMm, SimSettings s) =>
        Share(DesignerPlaceholders.Glass(EyeKind.Bridge, false, false, s.SightLookoutMagnification), pupilMm);

    /// <summary>A rangefinder's or a director's telescope's share (<see cref="GlassLight"/>), for fire control's sensors.</summary>
    public static double TelescopeGlass(in OpticsRow optics, double pupilMm) =>
        Share(DesignerPlaceholders.Glass(EyeKind.Director, true, optics.Coated, optics.Magnification), pupilMm);

    static double Share((double ExitPupilMm, double Transmission) g, double pupilMm) =>
        g.Transmission * Math.Min(1, g.ExitPupilMm * g.ExitPupilMm / (pupilMm * pupilMm));

    /// <summary>The dark on a line (<see cref="DarkOf(double, SimSettings)"/>): the scene through the eye's glass (the sky
    /// and any veil over it, docs/battle/night.md Q4), or what the eye is adapted to when it's more (dazzled, Q2): the
    /// threshold then as for that light, and over the scene by as much as that is brighter (the eye adapted to L_a needs
    /// ΔL ≈ Cₜ(L_a)·L_a).</summary>
    public static Dark DarkOf(in SightLine line, SimSettings s)
    {
        double scene = (line.SkyCdM2 + line.VeilCdM2) * line.Glass;
        if (!(line.AdaptedCdM2 > scene))
            return DarkOf(scene, s);
        var d = DarkOf(line.AdaptedCdM2, s);
        return new Dark(Math.Max(d.Floor, 1) * line.AdaptedCdM2 / Math.Max(scene, 1e-12), Math.Max(d.Summation, 1));
    }

    /// <summary>How much the dark raises the eye's threshold (docs/battle/night.md Q1), from the sky's luminance as the eye
    /// gets it through its glass (cd/m²): each factor (1 + √(L_k / L)) over its value at <see cref="SimSettings.SightWeberCdM2"/>
    /// (flat by day, the de Vries-Rose square root law in the dark), its own knee for the large object's threshold
    /// (<see cref="SimSettings.SightDarkCdM2"/>) and for the summation's side (<see cref="SimSettings.SightDarkSummationCdM2"/>);
    /// exactly none brighter than that.</summary>
    public static Dark DarkOf(double eyeCdM2, SimSettings s)
    {
        double l = Math.Max(eyeCdM2, 1e-12), w = s.SightWeberCdM2;
        if (l >= w)
            return Dark.Day;
        static double K(double knee, double l, double w) => (1 + Math.Sqrt(knee / l)) / (1 + Math.Sqrt(knee / w));
        return new Dark(K(s.SightDarkCdM2, l, w), K(s.SightDarkSummationCdM2, l, w));
    }

    /// <summary>A ship's own contrast against the horizon sky in this light (cd/m²): the day's grey ship with the sun on
    /// her sides, darker against the sky in its light alone (after sunset, under an overcast, at night), between on the
    /// sky's luminance (log); and toward a silhouette's against a bright horizon by the share (0-1). Lit by a light near
    /// her (lux on her side toward the eye, docs/battle/night.md Q2), the parts it falls on shine back at
    /// <see cref="SimSettings.SightHullAlbedo"/>: brighter than the sky by ρE / πL less her own darkness, the better of
    /// that and her dark shape.</summary>
    public static double InherentContrast(double skyCdM2, double silhouette, SimSettings s, double litLux = 0)
    {
        double sky = Math.Max(skyCdM2, 1e-12);
        double day = Math.Clamp(Math.Log(sky / s.SightContrastNightCdM2) / Math.Log(s.SightContrastDayCdM2 / s.SightContrastNightCdM2), 0, 1);
        double own = s.SightContrastNight + (s.SightContrastDay - s.SightContrastNight) * day;
        double c = own + (s.SightContrastSilhouette - own) * Math.Clamp(silhouette, 0, 1);
        return litLux > 0 ? Math.Max(c, s.SightHullAlbedo * litLux / (Math.PI * sky) - own) : c;
    }

    /// <summary>Her bow wave's margin (docs/battle/night.md Q1): white water at her stem as high as
    /// <see cref="SimSettings.SightBowWaveHeight"/> of her speed's head and as long as <see cref="SimSettings.SightBowWaveLength"/>
    /// of her, side on along her side, bow on both sheets across her beam, none from astern; what shows over the horizon
    /// at <see cref="SimSettings.SightFoamContrast"/>, through the haze and the low smoke. A detection, not her shape:
    /// it tells nothing of her type or class. 0 when she's stopped or it's hidden.</summary>
    public static double BowWaveMargin(double eyeM, double magnification, ShipClass target, in SightLine line, SimSettings s)
    {
        double v = Math.Abs(line.SpeedMs);
        if (v <= 0)
            return 0;
        double d = Math.Max(line.DistanceM, 1), h = s.SightBowWaveHeight * v * v / (2 * 9.81), shown = h - HiddenM(eyeM, d, s);
        if (shown <= 0)
            return 0;
        double a = line.AspectDeg * Math.PI / 180;
        var m = target.Motion;
        double area = shown * (s.SightBowWaveLength * m.LengthM * Math.Abs(Math.Sin(a)) + 2 * m.BeamM * Math.Max(Math.Cos(a), 0));
        if (area <= 0)
            return 0;
        double c = s.SightFoamContrast * line.VeilShare * Math.Exp(-Extinction(line.VisibilityM, s) * d - (line.Smoke.Clear ? 0 : line.Smoke.Low));
        return c / Threshold(area * magnification * magnification / (d * d), DarkOf(line, s), s);
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

    /// <summary>The threshold contrast for an object of this solid angle (as magnified), sr, by day.</summary>
    public static double Threshold(double omega, SimSettings s) =>
        s.SightThresholdContrast * (1 + Math.Sqrt(s.SightCriticalSolidAngle / Math.Max(omega, 1e-30)));

    /// <summary>The threshold contrast in the dark (<see cref="DarkOf"/>): C∞ by the floor's factor, the critical solid
    /// angle by the square of the summation's (it's a side).</summary>
    public static double Threshold(double omega, in Dark dark, SimSettings s) =>
        !dark.Any
            ? Threshold(omega, s)
            : s.SightThresholdContrast * dark.Floor *
              (1 + dark.Summation * Math.Sqrt(s.SightCriticalSolidAngle / Math.Max(omega, 1e-30)));

    /// <summary>The haze's extinction, per metre, for a visibility: the one at which the
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
        var dark = DarkOf(line, s);
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
                            best = Math.Max(best, contrast * t / Threshold(a * m2, dark, s));
                    }
                }
        return best;
    }

    /// <summary>The margin for an area showing at a distance (<see cref="Margin"/> after the horizon and aspect), through
    /// the haze alone.</summary>
    public static double MarginOfArea(double areaM2, double distanceM, double magnification, in SightLine line, SimSettings s) =>
        Contrast(distanceM, line, s) / Threshold(areaM2 * magnification * magnification / (distanceM * distanceM), DarkOf(line, s), s);

    /// <summary>The margin an eye makes out a ship's smoke at (docs/battle/smoke.md Z6, <see cref="SmokePlume"/>): what of
    /// it shows over the horizon at the view (the sine of the line of sight against its trail), at its own contrast through
    /// the haze (smoke makes no silhouette of its own), against the eye's threshold for that solid angle in the light
    /// (docs/battle/night.md Q1). 0 when none of it shows.</summary>
    public static double SmokeMargin(double eyeM, double magnification, in SmokePlume plume, double sinView, in SightLine line, SimSettings s)
    {
        double d = Math.Max(line.DistanceM, 1);
        var (area, shade) = plume.Showing(HiddenM(eyeM, d, s), sinView);
        if (!(area > 0) || !(shade > 0))
            return 0;
        return shade * line.VeilShare * Math.Exp(-Extinction(line.VisibilityM, s) * d) /
               Threshold(area * magnification * magnification / (d * d), DarkOf(line, s), s);
    }

    /// <summary>The farthest any of a plume could be made out, m (<see cref="SmokeMargin"/>'s bound): its darkest
    /// smoke's contrast faded through the haze to the eye's threshold for the largest object in the light (cd/m², through
    /// the best glass there is: the naked eye's); 0 when it never shows (the dark, or no smoke dark enough).</summary>
    public static double SmokeFarthestM(in SmokePlume plume, double visibilityM, double skyCdM2, SimSettings s)
    {
        var v = plume.Puffs;
        double shade = Math.Max(Math.Max(Math.Max(v.S0, v.S1), Math.Max(v.S2, v.S3)), plume.Contrast);
        double least = s.SightThresholdContrast * DarkOf(skyCdM2, s).Floor;
        if (!(shade > least))
            return 0;
        double ext = Extinction(visibilityM, s);
        return ext > 0 ? Math.Log(shade / least) / ext : double.PositiveInfinity;
    }

    /// <summary>A target's apparent contrast through the haze at a distance: her own in the light (<see cref="InherentContrast"/>)
    /// faded by the visibility, and by a veil over her (docs/battle/night.md Q4).</summary>
    static double Contrast(double distanceM, in SightLine line, SimSettings s) =>
        InherentContrast(line.SkyCdM2, line.Silhouette, s, line.LitLux) * line.VeilShare * Math.Exp(-Extinction(line.VisibilityM, s) * distanceM);

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
