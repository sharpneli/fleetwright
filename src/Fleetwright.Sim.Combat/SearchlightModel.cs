using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A searchlight's beam (docs/battle/night.md Q4): pure functions of numbers, no state. The beam is a Gaussian
/// about its axis, its full spread (<see cref="SearchlightRow.SpreadDeg"/>) the width at half its peak; held on a ship by
/// hand it wanders by its control's pointing error, so its mean light is the beam blurred by that (wider, its peak lower by
/// as much). What it does, each through the haze by Allard's law as every light does (<see cref="ScatterPerM"/>):
/// <list type="bullet">
/// <item><b>It lights what it's on</b> (<see cref="Toward"/>: the lux on a ship in it, for every eye that sees her lit side).</item>
/// <item><b>Its lamp shows</b>: to an eye in the beam, the mirror's full glare; outside it, the front glass's stray light
/// forward of it (both a point to aim at); from beside, the beam itself, its first <see cref="SimSettings.SearchlightColumnM"/>
/// scattering a share of its light all round (<see cref="ColumnCd"/>: a streak that gives her bearing, not a point).</item>
/// <item><b>It veils its own ship's view down it</b> (<see cref="BackscatterCdM2"/>): its light scattered back off the haze
/// between, from where the eye's line enters the beam on (beside the lamp, the eye's line runs into it some way out),
/// so the illuminating ship sees her target worse than a consort off to the side does.</item>
/// <item><b>It glares in the eyes it's on</b> (<see cref="GlareCdM2"/>, Stiles and Holladay's disability glare): a veil
/// over what's near the lamp in their view, so a ship lit sees the lamp and little round it.</item>
/// </list></summary>
public static class SearchlightModel
{
    /// <summary>A beam's own 1-sigma spread, degrees: its full spread taken as the full width at half its peak.</summary>
    public static double BeamSigmaDeg(double spreadDeg) => spreadDeg / 2.3548;

    /// <summary>A beam's mean light about where it's held, its pointing wandering by a 1-sigma error (degrees): the
    /// spread of the blurred beam (degrees) and the share of its peak it keeps (σ_b² / σ²; 1 held exactly).</summary>
    public static (double SigmaDeg, double Keep) Held(double spreadDeg, double pointingDeg)
    {
        double b = BeamSigmaDeg(spreadDeg), s2 = b * b + pointingDeg * pointingDeg;
        return (Math.Sqrt(s2), b * b / s2);
    }

    /// <summary>The light a beam sends at an angle off its axis (degrees), cd: its peak by its (blurred) profile.</summary>
    public static double Toward(double beamCd, double offDeg, double sigmaDeg, double keep)
    {
        double off = Off(offDeg);
        return beamCd * keep * Math.Exp(-off * off / (2 * sigmaDeg * sigmaDeg));
    }

    /// <summary>The lamp as an eye at an angle off its axis sees it, cd: the beam's light (<see cref="Toward"/>), and the
    /// front glass's stray light forward of it (<see cref="SimSettings.SearchlightStrayShare"/> of its peak, as the cosine).</summary>
    public static double LampCd(double beamCd, double offDeg, double sigmaDeg, double keep, SimSettings s)
    {
        double off = Off(offDeg);
        return Toward(beamCd, off, sigmaDeg, keep) + (off < 90 ? beamCd * s.SearchlightStrayShare * Math.Cos(off * Math.PI / 180) : 0);
    }

    /// <summary>A beam's light, lumens: its peak over a Gaussian's solid angle (2πσ²).</summary>
    public static double FluxLm(double beamCd, double spreadDeg)
    {
        double sigma = BeamSigmaDeg(spreadDeg) * Math.PI / 180;
        return beamCd * 2 * Math.PI * sigma * sigma;
    }

    /// <summary>The air's scattering a metre: Allard's 3.912 / the visibility (haze scatters what it takes out), and at
    /// least clear air's own (<see cref="SimSettings.SearchlightClearAirPerM"/>).</summary>
    public static double ScatterPerM(double visibilityM, SimSettings s) =>
        Math.Max(double.IsPositiveInfinity(visibilityM) || visibilityM <= 0 ? 0 : 3.912 / visibilityM, s.SearchlightClearAirPerM);

    /// <summary>A beam seen from beside, as a light at its lamp, cd: the share of its light its first
    /// <see cref="SimSettings.SearchlightColumnM"/> scatter, spread evenly all round [EST: a rough phase function].</summary>
    public static double ColumnCd(double beamCd, double spreadDeg, double visibilityM, SimSettings s) =>
        FluxLm(beamCd, spreadDeg) * (1 - Math.Exp(-ScatterPerM(visibilityM, s) * s.SearchlightColumnM)) / (4 * Math.PI);

    /// <summary>The veil a beam's own light scattered back lays over an eye near its lamp looking at a target in it (the
    /// target <paramref name="distanceM"/> from the lamp), cd/m²: the light scattered toward the eye
    /// (<see cref="SimSettings.SearchlightBackscatter"/>) all along its line from where it enters the beam to the target.
    /// Its line, from an eye <paramref name="eyeOffsetM"/> off the lamp, meets the beam's edge where the beam has spread
    /// that far toward it (beside the lamp a few hundred metres out, from a consort a kilometre off only near her); the
    /// beam is its mirror's width out to where its spread passes that (its light even across it, E = I / r_f²), and falls
    /// as 1/r² beyond. As much as the target stands in the beam (<paramref name="offDeg"/> off its axis, the blurred
    /// profile): a beam pointed elsewhere doesn't lie along the line.</summary>
    public static double BackscatterCdM2(double beamCd, double spreadDeg, double mirrorM, double offDeg, double sigmaDeg, double keep,
        double eyeOffsetM, double distanceM, double visibilityM, SimSettings s)
    {
        double share = Toward(1, offDeg, sigmaDeg, keep);
        if (!(share > 1e-6) || !(distanceM > 0))
            return 0;
        double half = spreadDeg / 2 * Math.PI / 180, r = Math.Max(mirrorM, 0.1) / 2;
        double rf = r / half, r0 = Math.Max(eyeOffsetM - r, 0) / (half + Math.Max(eyeOffsetM, 0) / distanceM);
        if (r0 >= distanceM)
            return 0;
        double near = r0 < rf ? (Math.Min(rf, distanceM) - r0) / (rf * rf) : 0;
        double far = Math.Max(1 / Math.Max(r0, rf) - 1 / distanceM, 0);
        return beamCd * share * ScatterPerM(visibilityM, s) * s.SearchlightBackscatter * (near + far) / (4 * Math.PI);
    }

    /// <summary>A light's glare in an eye, cd/m² over what it looks at (Stiles and Holladay's disability glare): the lux it
    /// puts at the eye times <see cref="SimSettings.SearchlightGlareK"/> over the square of the angle (degrees) between the
    /// lamp and what the eye looks at, that at least <see cref="SimSettings.SearchlightGlareMinDeg"/>.</summary>
    public static double GlareCdM2(double eyeLux, double offDeg, SimSettings s)
    {
        if (!(eyeLux > 0))
            return 0;
        double a = Math.Max(Off(offDeg), s.SearchlightGlareMinDeg);
        return s.SearchlightGlareK * eyeLux / (a * a);
    }

    /// <summary>A light's lux at a distance through the haze (Allard's law, as <c>Lights.On</c>'s).</summary>
    public static double LuxAt(double candela, double distanceM, double visibilityM)
    {
        double d = Math.Max(distanceM, 1), sigma = double.IsPositiveInfinity(visibilityM) || visibilityM <= 0 ? 0 : 3.912 / visibilityM;
        return candela * Math.Exp(-sigma * d) / (d * d);
    }

    /// <summary>A light's pointing error held on a ship, degrees 1-sigma, by its control.</summary>
    public static double PointingDeg(SearchlightControl control, SimSettings s) =>
        s.SearchlightPointingDeg[Math.Min((int)control, s.SearchlightPointingDeg.Length - 1)];

    /// <summary>A searchlight's reach, m (to 10 m): the furthest its own ship's lookout (a lookout's binoculars,
    /// <paramref name="eyeOffsetM"/> off the lamp) makes out the reference ship (<see cref="SimSettings.SightReferenceAreaM2"/>
    /// side on) lit by it, held on her, through the haze, under a sky of this luminance (cd/m²), his view down the beam
    /// veiled by its own light. The captains' "how far my lights are any use" and the tables' useful range.</summary>
    public static double ReachM(in SearchlightRow light, double eyeOffsetM, double visibilityM, double skyCdM2, SimSettings s)
    {
        var (sigma, keep) = Held(light.SpreadDeg, PointingDeg(light.Control, s));
        double cd = light.BeamCd, spread = light.SpreadDeg, mirror = light.MirrorM;
        double Margin(double d)
        {
            double veil = BackscatterCdM2(cd, spread, mirror, 0, sigma, keep, eyeOffsetM, d, visibilityM, s);
            double glass = SightModel.LookoutGlass(SightModel.PupilMm(skyCdM2 + veil), s);
            var line = new SightLine(d, 90, visibilityM, skyCdM2, 0, Glass: glass, LitLux: LuxAt(cd * keep, d, visibilityM), VeilCdM2: veil);
            return SightModel.MarginOfArea(s.SightReferenceAreaM2, d, s.SightLookoutMagnification, line, s);
        }
        double lo = 10, hi = 100_000;
        if (Margin(lo) < 1)
            return 0;
        while (hi - lo > 10)
        {
            double mid = 0.5 * (lo + hi);
            (lo, hi) = Margin(mid) >= 1 ? (mid, hi) : (lo, mid);
        }
        return lo;
    }

    /// <summary>A class's biggest searchlight (by its beam; -1 for none), and how far its operator's lookout stands off
    /// it, m: the class's eye nearest it.</summary>
    public static (int Light, double OffsetM) Biggest(ShipClass c)
    {
        var rows = c.Searchlights;
        if (rows.Length == 0)
            return (-1, 0);
        int big = 0;
        for (int i = 1; i < rows.Length; i++)
            if (rows[i].BeamCd > rows[big].BeamCd)
                big = i;
        ref readonly var l = ref rows[big];
        double offset = double.MaxValue;
        foreach (ref readonly var e in c.Eyes)
            offset = Math.Min(offset, Math.Sqrt((e.X - l.X) * (e.X - l.X) + (e.Y - l.Y) * (e.Y - l.Y) + (e.HeightM - l.HeightM) * (e.HeightM - l.HeightM)));
        return (big, offset == double.MaxValue ? 0 : offset);
    }

    static double Off(double deg)
    {
        double a = deg % 360;
        if (a > 180)
            a -= 360;
        else if (a < -180)
            a += 360;
        return Math.Abs(a);
    }
}
