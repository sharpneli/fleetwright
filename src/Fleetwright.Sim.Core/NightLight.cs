namespace Fleetwright.Sim.Core;

/// <summary>The light at a place as the eye meets it (docs/battle/night.md Q1). <paramref name="SkyCdM2"/>: the horizon
/// sky's luminance all round, the background a ship is seen against and the eye adapts to (cd/m²); on a bearing it's
/// <see cref="Toward"/>: <paramref name="TwilightCdM2"/> of it is the twilight's, which gathers in an arch toward the sun
/// (<paramref name="SunBearingDeg"/>) as strongly as <paramref name="Arch"/> says. <paramref name="MoonBearingDeg"/>: the
/// moon's bearing, degrees clockwise from north. <paramref name="MoonSilhouette"/>: how strongly a ship within the
/// silhouette arc of it stands dark against its path on the sea and the brighter sky about it, 0 (no moon, or overcast) to
/// 1. <paramref name="Lux"/>: a level surface's illuminance, for listings.</summary>
public readonly record struct NightSky(double SkyCdM2, double MoonBearingDeg, double MoonSilhouette, double Lux, double TwilightCdM2 = 0,
    double SunBearingDeg = 0, double Arch = 0)
{
    /// <summary>The horizon sky's luminance on a bearing, cd/m²: the twilight's share as e^(k cos Δ) / I₀(k) of its
    /// mean (a von Mises arch about the sun's bearing: its mean all round is the mean), the rest the same all round.</summary>
    public double Toward(double bearingDeg)
    {
        if (Arch <= 0 || TwilightCdM2 <= 0)
            return SkyCdM2;
        double c = Math.Cos((bearingDeg - SunBearingDeg) * Math.PI / 180);
        return SkyCdM2 - TwilightCdM2 + TwilightCdM2 * Math.Exp(Arch * c) / NightLight.BesselI0(Arch);
    }

    /// <summary>The brightest of the horizon, toward the sun, cd/m².</summary>
    public double Brightest => Toward(SunBearingDeg);
}

/// <summary>The night's light (docs/battle/night.md Q1): the sun below the horizon (the twilight curve), the moon (its
/// phase, height and distance: <see cref="Moon.Light(double, double, double)"/>), the stars and the airglow, under the
/// weather's cloud. One function of place and time, read by the sim's sight and by the display's light, so the two
/// can't disagree. By day it's far more than any eye needs: sight's night terms vanish there.</summary>
public static class NightLight
{
    /// <summary>A full moon overhead at its mean distance, lux (<see cref="Moon.Light(Vec3d, WorldTime)"/>'s 1).</summary>
    public const double FullMoonLux = 0.25;

    /// <summary>A clear moonless night's sky: starlight and airglow on a level surface, lux (0.5-2·10⁻³ is quoted; the
    /// airglow most of it).</summary>
    public const double StarLux = 1e-3;

    // a level surface's illuminance from the sun and the sky by the sun's elevation, degrees, lux (clear air: the standard
    // daylight and twilight curve, log10 interpolated; below the last row it falls on at the last rows' slope)
    static readonly (double Deg, double Lux)[] Daylight =
    [
        (-18, 7e-4), (-12, 8e-3), (-9, 0.1), (-6, 3), (-3, 60), (0, 500), (5, 6000), (10, 15_000), (20, 40_000), (30, 60_000),
        (45, 85_000), (60, 100_000), (90, 120_000),
    ];

    /// <summary>The sun's and its sky's light on a level surface at an elevation (degrees), clear air, lux: twilight's
    /// smooth fall through the civil, nautical and astronomical bands into the dark.</summary>
    public static double SunLux(double sunElevationDeg)
    {
        double deg = sunElevationDeg;
        if (deg <= Daylight[0].Deg)
        {
            var (a, b) = (Daylight[0], Daylight[1]);
            return Math.Pow(10, Math.Log10(a.Lux) + (deg - a.Deg) * (Math.Log10(b.Lux) - Math.Log10(a.Lux)) / (b.Deg - a.Deg));
        }
        for (int i = 1; i < Daylight.Length; i++)
            if (deg <= Daylight[i].Deg)
            {
                var (a, b) = (Daylight[i - 1], Daylight[i]);
                double f = (deg - a.Deg) / (b.Deg - a.Deg);
                return Math.Pow(10, Math.Log10(a.Lux) + f * (Math.Log10(b.Lux) - Math.Log10(a.Lux)));
            }
        return Daylight[^1].Lux;
    }

    /// <summary>A level surface's illuminance, lux, clear air: from the sun at <paramref name="sunSin"/> (the sine of
    /// its elevation), the moon's light (<see cref="Moon.Light(Vec3d, WorldTime)"/>, on a surface facing it) at
    /// <paramref name="moonSin"/>, and the stars.</summary>
    public static double Lux(double sunSin, double moonLight, double moonSin) =>
        SunLux(Math.Asin(Math.Clamp(sunSin, -1, 1)) * 180 / Math.PI) + FullMoonLux * Math.Max(moonLight, 0) * Math.Max(moonSin, 0) + StarLux;

    /// <summary>The share of the sky's light that comes through a cloud cover (0 clear to 1 overcast): an overcast passes
    /// <see cref="SimSettings.NightOvercastShare"/> of it.</summary>
    public static double Through(double cloud, SimSettings s) => 1 - Math.Clamp(cloud, 0, 1) * (1 - s.NightOvercastShare);

    /// <summary>The light at a place and time under a cloud cover (0-1). The horizon sky's luminance is the diffuse
    /// light's (the twilight's and the stars', through the cloud) over π, and the moon's scattered share: clear air
    /// scatters <see cref="SimSettings.NightMoonSkyShare"/> of it into the sky, an overcast passes its share as diffuse
    /// light. The moon's path shows by its brightness against that sky (<see cref="SimSettings.NightMoonPathShare"/> of
    /// the moon's light, as a luminance), where the moon isn't hidden, fading as it climbs to
    /// <see cref="SimSettings.SightSilhouetteMoonHighDeg"/>.</summary>
    public static NightSky At(Vec3d position, WorldTime time, double cloud, SimSettings s)
    {
        double sunEl = Light.SunElevationDeg(position, time);
        var up = position.Normalized();
        var toMoon = Moon.Position(time) - position;
        double dist = toMoon.Length, sinMoon = Vec3d.Dot(toMoon / dist, up);
        double moon = FullMoonLux * Moon.Light(sinMoon, Moon.Phase(time).AngleDeg, dist);   // on a surface facing it
        double moonLevel = moon * Math.Max(sinMoon, 0);
        double c = Math.Clamp(cloud, 0, 1), through = Through(c, s);
        double twilight = SunLux(sunEl) * through / Math.PI;
        double sky = twilight + (StarLux * through + moonLevel * ((1 - c) * s.NightMoonSkyShare + c * s.NightOvercastShare)) / Math.PI;
        double path = (1 - c) * s.NightMoonPathShare * moon / Math.PI;
        double climb = Math.Clamp(1 - sinMoon / Math.Sin(s.SightSilhouetteMoonHighDeg * Math.PI / 180), 0, 1);
        double silhouette = path > 0 ? climb * path / (path + sky) : 0;
        double lux = (SunLux(sunEl) + StarLux) * through + moonLevel * ((1 - c) + c * s.NightOvercastShare);
        // the twilight's arch forms as the sun goes down, under a clear sky (an overcast spreads it all round)
        double arch = (1 - c) * s.NightTwilightArch * Math.Clamp(-sunEl / s.NightTwilightArchDeg, 0, 1);
        return new NightSky(sky, Earth.Bearing(position, position + toMoon), silhouette, lux, twilight, Light.SunBearingDeg(position, time), arch);
    }

    /// <summary>The modified Bessel function I₀ (its series: the von Mises arch's mean over a circle).</summary>
    public static double BesselI0(double x)
    {
        double sum = 1, term = 1, q = x * x / 4;
        for (int k = 1; k < 30 && term > 1e-12 * sum; k++)
            sum += term *= q / (k * k);
        return sum;
    }

    /// <summary>The horizon sky's luminance under a clear sky with no moon, for the sun at an elevation, cd/m², its mean
    /// all round: for listings and tests that ask for "day" or "a dark night" by the sun alone.</summary>
    public static double ClearSkyCdM2(double sunElevationDeg) => (SunLux(sunElevationDeg) + StarLux) / Math.PI;
}
