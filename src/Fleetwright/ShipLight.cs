using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Sim.Core;

namespace Fleetwright;

/// <summary>The lights the ships are lit by, set by eye in one place (docs/visuals/ship-lighting.md: PBR-inspired, the
/// lights not held to physical units): the sun's colour and the air's extinction, the hemisphere's ambient by day, at
/// dusk and at night, how the moon shows, the default material and the output's shoulder. The map, the ship viewer and
/// the poster read <see cref="Current"/>; the viewer's panel edits it live. The water pass reads the same lights when it
/// comes. All colours linear.</summary>
public sealed class LightTable
{
    public static LightTable Current { get; } = new();

    /// <summary>The sun overhead. With <see cref="SkyDay"/> it sums to 1, so a flat matte deck under the sun at the
    /// zenith shows its colour as drawn (the calibration rule: the look gains relief and glints, it doesn't shift).</summary>
    public Vector3 SunZenith = new(0.80f, 0.78f, 0.74f);

    /// <summary>The air's extinction per air mass past the first, per channel: the sun warms and dims low down.</summary>
    public Vector3 Extinction = new(0.09f, 0.15f, 0.30f);

    /// <summary>The ambient from above (the sky) and from below (the sea), by day, at dusk (the sun on the horizon) and
    /// at night. The night's sky is the readability floor: on a moonless night your own ships still read as ships
    /// (about today's 0.3 of their colour).</summary>
    public Vector3 SkyDay = new(0.20f, 0.22f, 0.26f), SeaDay = new(0.05f, 0.09f, 0.11f);
    public Vector3 SkyDusk = new(0.17f, 0.135f, 0.13f), SeaDusk = new(0.03f, 0.04f, 0.06f);
    public Vector3 SkyNight = new(0.055f, 0.065f, 0.09f), SeaNight = new(0.015f, 0.02f, 0.03f);

    /// <summary>The moon: a full one overhead (<see cref="Moon.Light(Vec3d, WorldTime)"/> = 1) at night gives this much
    /// light, cool; the moonlit sky adds <see cref="MoonSky"/> of it to the ambient. Shown only as the night comes (the
    /// eye's adaptation): by day it is lost under the sun.</summary>
    public float MoonStrength = 0.10f, MoonSky = 0.2f;
    public Vector3 MoonTint = new(0.78f, 0.87f, 1.0f);

    /// <summary>The moon's light over a night, as a fraction of it overhead: what time-lapse shows instead of the moon
    /// spinning round the sky (up about half the time, and low much of it).</summary>
    public float MoonNightMean = 0.3f;

    /// <summary>The default material (L1: one paint everywhere, eggshell: 0.4 glared the whole ship white under a high sun with the flat normal), the specular's scale (0 for the calibration check),
    /// the shoulder's knee (linear: above it highlights roll off toward 1 instead of clipping), and the shadows'
    /// opacity relative to the light they take away.</summary>
    public float Roughness = 0.6f, Specular = 1f, Knee = 0.8f, Shadow = 0.9f;

    /// <summary>Sets one scalar knob from "name:value" (the -lights= flag); false if it isn't one.</summary>
    public bool Set(string nameValue)
    {
        var p = nameValue.Split(':');
        if (p.Length != 2 || !float.TryParse(p[1], System.Globalization.CultureInfo.InvariantCulture, out float v))
            return false;
        switch (p[0])
        {
            case "roughness": Roughness = v; break;
            case "specular": Specular = v; break;
            case "knee": Knee = v; break;
            case "shadow": Shadow = v; break;
            case "moon": MoonStrength = v; break;
            case "moonsky": MoonSky = v; break;
            default: return false;
        }
        return true;
    }
}

/// <summary>The Lights uniform block of Content/Shaders/Source/Include/shiplight.glsl (std140, 5 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct LightUniforms
{
    /// <summary>rgb: the sun's light, linear (0 when down); w unused.</summary>
    public Vector4 SunRad;
    /// <summary>rgb: the moon's light; w unused.</summary>
    public Vector4 MoonRad;
    /// <summary>rgb: the ambient from above; w: the light's level, sRGB-coded (a flat white deck), for unlit constants.</summary>
    public Vector4 Sky;
    /// <summary>rgb: the ambient from below; w unused.</summary>
    public Vector4 Sea;
    /// <summary>x: 1 lit, 0 flat (the albedo as drawn); y: roughness; z: the shoulder's knee; w: the specular's scale.</summary>
    public Vector4 Look;
}

/// <summary>One frame's light at a place: the uniforms, the directions (in the caller's axes) and the shadows.</summary>
public struct ShipLights
{
    public LightUniforms Uniforms;

    /// <summary>Toward the sun (in time-lapse, blended toward up: the day's mean falls on a level deck) and the moon.</summary>
    public Vec3d SunDir, MoonDir;

    /// <summary>Toward the light the shadows are cast by, the stronger of the two; and their opacity (0: none). Through
    /// twilight they fade out from one light and in from the other, crossing where the two are equal.</summary>
    public Vec3d ShadowDir;
    public double ShadowOpacity;

    /// <summary>The flat look: the albedo as drawn, no light (the poster's "flat").</summary>
    public static ShipLights Flat => new() { Uniforms = new LightUniforms { Sky = new(1, 1, 1, 1) }, SunDir = new(0, 0, 1), MoonDir = new(0, 0, 1) };

    /// <summary>The light at a place, once a frame. Every vector unit, in one set of axes (Earth-fixed on the map,
    /// ship-local in the viewer): <paramref name="up"/> the local vertical, <paramref name="sun"/> and
    /// <paramref name="moon"/> toward each. <paramref name="moonLight"/>: <see cref="Moon.Light(Vec3d, WorldTime)"/>;
    /// <paramref name="moonPhaseLaw"/>: <see cref="Moon.PhaseLaw"/> of its phase (for time-lapse).
    /// <paramref name="motion"/>: <c>SunLapse.Motion</c>, 1 live; below it, <paramref name="dayMeans"/> is
    /// <c>SunLapse.DayMeans</c> there and the light fades to it.</summary>
    public static ShipLights Compute(LightTable t, Vec3d up, Vec3d sun, Vec3d moon, double moonLight, double moonPhaseLaw,
        double motion = 1, (double Day, double Lit) dayMeans = default)
    {
        double s = Vec3d.Dot(sun, up), sm = Vec3d.Dot(moon, up);
        motion = Math.Clamp(motion, 0, 1);

        // live: the sun through the air, the ambient from night through dusk to day as the globe's terminator
        var sunLive = t.SunZenith * Extinct(t, s) * (float)SmoothStep(-0.02, 0.06, s);
        float dusk = (float)SmoothStep(-0.10, 0.0, s), day = (float)SmoothStep(0.0, 0.2, s);
        var skyLive = Vector3.Lerp(Vector3.Lerp(t.SkyNight, t.SkyDusk, dusk), t.SkyDay, day);
        var seaLive = Vector3.Lerp(Vector3.Lerp(t.SeaNight, t.SeaDusk, dusk), t.SeaDay, day);
        // time-lapse: the day's mean on a level deck, from overhead
        var sunLapse = t.SunZenith * (float)dayMeans.Lit;
        var skyLapse = Vector3.Lerp(t.SkyNight, t.SkyDay, (float)dayMeans.Day);
        var seaLapse = Vector3.Lerp(t.SeaNight, t.SeaDay, (float)dayMeans.Day);
        float m = (float)motion;
        var sunRad = sunLive * m + sunLapse * (1 - m);
        double w1 = motion * Lum(sunLive), w0 = (1 - motion) * Lum(sunLapse);
        var sunDir = w1 + w0 > 1e-9 ? (sun * w1 + up * w0).Normalized() : sun;
        var sky = skyLive * m + skyLapse * (1 - m);
        var sea = seaLive * m + seaLapse * (1 - m);

        // the moon, as the night comes (the eye's adaptation: by day it is lost under the sun); live its own light, in
        // time-lapse the night's mean for its phase
        float nightLive = 1 - (float)SmoothStep(-0.10, 0.12, s), nightLapse = 1 - (float)dayMeans.Day;
        var e = Extinct(t, sm);
        var chroma = e / Math.Max(Lum(e), 1e-6f);   // the moon's own dimming is in its light: here only its warmth
        var moonRad = t.MoonTint * chroma * (float)(t.MoonStrength * moonLight * motion) * nightLive;
        var moonMean = t.MoonTint * (float)(t.MoonStrength * moonPhaseLaw * t.MoonNightMean * (1 - motion)) * nightLapse;
        sky += moonRad * t.MoonSky + moonMean;

        // shadows by the stronger light, by what each puts on a level deck
        double sunH = motion * Lum(sunLive) * Math.Max(s, 0), moonH = Lum(moonRad) * Math.Max(sm, 0);
        double dom = Math.Max(sunH, moonH), weak = Math.Min(sunH, moonH), amb = Lum(sky);
        double opacity = dom > 1e-9 ? t.Shadow * (1 - Math.Pow((amb + weak) / (amb + dom), 1 / 2.2)) : 0;

        double level = Encode(Lum(sunRad) * Math.Max(Vec3d.Dot(sunDir, up), 0) + amb);
        return new ShipLights
        {
            Uniforms = new LightUniforms
            {
                SunRad = new Vector4(sunRad, 0),
                MoonRad = new Vector4(moonRad, 0),
                Sky = new Vector4(sky, (float)Math.Min(level, 1)),
                Sea = new Vector4(sea, 0),
                Look = new Vector4(1, t.Roughness, t.Knee, t.Specular * m),
            },
            SunDir = sunDir, MoonDir = moon,
            ShadowDir = sunH >= moonH ? sun : moon, ShadowOpacity = opacity,
        };
    }

    /// <summary>The light left after the air at an elevation (its sine), per channel, relative to overhead.</summary>
    static Vector3 Extinct(LightTable t, double sinElevation)
    {
        float extra = (float)(Earth.AirMass(sinElevation) - 1);
        return new Vector3(MathF.Exp(-t.Extinction.X * extra), MathF.Exp(-t.Extinction.Y * extra), MathF.Exp(-t.Extinction.Z * extra));
    }

    static float Lum(Vector3 c) => 0.2126f * c.X + 0.7152f * c.Y + 0.0722f * c.Z;

    /// <summary>Linear to sRGB-coded.</summary>
    static double Encode(double c) => c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

    static double SmoothStep(double a, double b, double x)
    {
        double k = Math.Clamp((x - a) / (b - a), 0, 1);
        return k * k * (3 - 2 * k);
    }
}
