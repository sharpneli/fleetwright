using System.Numerics;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Flashes;

/// <summary>Light in physical units for the flashes (docs/visuals/muzzle-flashes.md, "Brightness without HDR"): a hot
/// gas's luminance and colour by its temperature (research/vfx/muzzle_flash_ref.py's <c>bb_luminance</c> and
/// <c>bb_rgb</c>: Planck × CIE 1931), and the luminance of a lit white deck from the sun's and moon's elevations, the
/// physical twin of the display's light level (<c>LightUniforms.Sky.w</c>). A flash is drawn as many times brighter
/// than the display's white deck as it is than the real one.</summary>
public static class FlashPhotometry
{
    // its chroma, linear sRGB, the largest channel 1
    static readonly (double T, Vector3 C)[] Chromas =
    [
        (1000, new(1, .029f, 0)), (1200, new(1, .073f, 0)), (1400, new(1, .121f, 0)), (1600, new(1, .17f, 0)), (1800, new(1, .219f, 0)),
        (2000, new(1, .266f, .008f)), (2200, new(1, .313f, .028f)), (2500, new(1, .38f, .068f)), (3000, new(1, .484f, .155f)),
    ];

    /// <summary>The sodium D line's and potassium's yellow (research §2.6): mixed 55% into the secondary flash, which a
    /// pure 1600-1900 K blackbody would draw salmon.</summary>
    public static readonly Vector3 Sodium = new(1f, 0.62f, 0.18f);
    public const float SodiumShare = 0.55f;

    /// <summary>A blackbody's luminance at <paramref name="kelvin"/>, cd/m²: the sim's (<see cref="FlashGlow.Luminance"/>).</summary>
    public static double Luminance(double kelvin) => FlashGlow.Luminance(kelvin);

    /// <summary>A blackbody's chroma at <paramref name="kelvin"/>, linear sRGB, the largest channel 1.</summary>
    public static Vector3 Chroma(double kelvin)
    {
        if (kelvin <= Chromas[0].T)
            return Chromas[0].C;
        for (int i = 1; i < Chromas.Length; i++)
            if (kelvin <= Chromas[i].T)
                return Vector3.Lerp(Chromas[i - 1].C, Chromas[i].C, (float)((kelvin - Chromas[i - 1].T) / (Chromas[i].T - Chromas[i - 1].T)));
        return Chromas[^1].C;
    }

    /// <summary>A colour scaled to a luminance of 1 (Rec. 709 weights).</summary>
    public static Vector3 UnitLuminance(Vector3 c)
    {
        float y = Lum(c);
        return y > 1e-6f ? c / y : Vector3.One;
    }

    public static float Lum(Vector3 c) => 0.2126f * c.X + 0.7152f * c.Y + 0.0722f * c.Z;

    /// <summary>A full moon overhead at its mean distance, lux (Moon.Light's 1): the sim's (<see cref="NightLight.FullMoonLux"/>).</summary>
    public const double FullMoonLux = NightLight.FullMoonLux;

    /// <summary>The starlit night sky's floor, lux: the sim's (<see cref="NightLight.StarLux"/>).</summary>
    public const double StarLux = NightLight.StarLux;

    /// <summary>A level surface's illuminance, lux, from the sun at <paramref name="sunSin"/> (the sine of its
    /// elevation), the moon's light (Moon.Light, already on a surface facing it) at <paramref name="moonSin"/>, and the
    /// stars: the sim's night light (<see cref="NightLight.Lux"/>, docs/battle/night.md Q1), so the look and the sight
    /// can't disagree.</summary>
    public static double Illuminance(double sunSin, double moonLight, double moonSin) => NightLight.Lux(sunSin, moonLight, moonSin);

    /// <summary>A level white (Lambertian, albedo 1) deck's luminance under an illuminance, cd/m².</summary>
    public static double WhiteDeck(double lux) => lux / Math.PI;
}
