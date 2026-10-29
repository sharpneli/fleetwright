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

    /// <summary>The ambient from above (the sky) and from below (the sea), by day and at dusk (the sun on the horizon).
    /// Below the horizon the night eye takes over (<see cref="NightEyeLevel"/>).</summary>
    public Vector3 SkyDay = new(0.20f, 0.22f, 0.26f), SeaDay = new(0.05f, 0.09f, 0.11f);
    public Vector3 SkyDusk = new(0.17f, 0.135f, 0.13f), SeaDusk = new(0.03f, 0.04f, 0.06f);

    /// <summary>The night's colours, only their chroma used (the night eye sets the level): the starlit sky's, and what
    /// the sea sends back up.</summary>
    public Vector3 SkyNight = new(0.055f, 0.065f, 0.09f), SeaNight = new(0.015f, 0.02f, 0.03f);

    /// <summary>The night eye (docs/visuals/night-lights.md Q10a): the display's level for a lit white deck, linear, by
    /// the real light on it (log10 lux), a partly adapted eye: the night's levels keep their order while their ratios
    /// shrink. It ends where the day's model leaves off at sunset (<see cref="SkyDusk"/>); below its first row it falls
    /// on at its first rows' slope. By eye [EST]: moonless near black, a full moon a dim grey-blue.</summary>
    public double[] NightEyeLogLux = [-4, -3, -2, -1, -0.6, 0, 1, 2, 2.7];
    public double[] NightEyeLevel = [0.005, 0.011, 0.024, 0.048, 0.070, 0.085, 0.11, 0.13, 0.145];

    /// <summary>A scale on the night eye's levels (captures: -lights=eye:1.5).</summary>
    public float EyeGain = 1;

    /// <summary>What the sea sends back up at night, over what falls on it (its ambient from below).</summary>
    public float NightBelow = 0.06f;

    /// <summary>The moon's light: a full one overhead (<see cref="Moon.Light(Vec3d, WorldTime)"/> = 1) is
    /// <see cref="NightLight.FullMoonLux"/>, through the night eye like all the night's light, cool. The moonlit sky
    /// adds <see cref="MoonSky"/> of it to the ambient.</summary>
    public float MoonSky = 0.2f;
    public Vector3 MoonTint = new(0.78f, 0.87f, 1.0f);

    /// <summary>The eye at night (docs/visuals/night-lights.md decision 2, Purkinje): the natural light's colours fade to
    /// the rods' grey-blue as the light falls, from <see cref="PurkinjeLogLux"/>[0] (none) to [1] (all of it, times
    /// <see cref="Purkinje"/>); artificial lights keep theirs. <see cref="RodTint"/>: the rods' world's colour.</summary>
    public double[] PurkinjeLogLux = [1.0, -1.5];
    public float Purkinje = 1;
    public Vector3 RodTint = new(0.62f, 0.86f, 1.45f);

    /// <summary>The moon's light over a night, as a fraction of it overhead: what time-lapse shows instead of the moon
    /// spinning round the sky (up about half the time, and low much of it).</summary>
    public float MoonNightMean = 0.3f;

    /// <summary>A scale on every material's roughness (L4: the looks' materials tables give each part its own), the
    /// specular's scale (0 for the calibration check: the sky's reflection goes with it), the shoulder's knee (linear:
    /// above it highlights roll off toward 1 instead of clipping), the shadows' opacity relative to the light they take
    /// away, and the horizon's brightness over the zenith's in what gloss reflects (the analytic sky).</summary>
    public float Roughness = 1f, Specular = 1f, Knee = 0.8f, Shadow = 0.9f, Horizon = 1.4f;

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
            case "horizon": Horizon = v; break;
            case "moonsky": MoonSky = v; break;
            case "eye": EyeGain = v; break;
            case "below": NightBelow = v; break;
            case "purkinje": Purkinje = v; break;
            default: return false;
        }
        return true;
    }
}

/// <summary>A vec4 a point light: an array of the Lights block (std140, 16 bytes a slot).</summary>
[System.Runtime.CompilerServices.InlineArray(LightUniforms.MaxLights)]
public struct LightArray
{
    Vector4 e;
}

/// <summary>The Lights uniform block of Content/Shaders/Source/Include/shiplight.glsl (std140, 5 vec4s, then the
/// muzzle flashes' lights: docs/visuals/muzzle-flashes.md F4).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct LightUniforms
{
    /// <summary>The most point lights a frame (docs/visuals/hits-and-fires.md decision 5): the flashes' pool (muzzle
    /// flashes, bursts, magazines), the fires' and the night's (star shell's flares, searchlights' beams:
    /// docs/visuals/night-lights.md Q10b), each of eight, so none crowds another out.</summary>
    public const int MaxLights = 24, MaxFlashes = 8, MaxFires = 8, MaxNight = 8;

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
    /// <summary>x: how many point lights there are (0-24: the flashes', the fires', then the night's); y: where the
    /// night's start (they light the water's body, the flashes and fires its glints and foam alone).</summary>
    public Vector4 FlashInfo;
    /// <summary>xyz: east, north and up at the view's centre, Earth-fixed: the axes of shaders that work in a local
    /// frame (the splash columns).</summary>
    public Vector4 FlashEast, FlashNorth, FlashUp;
    /// <summary>Per flash light, xyz: where it is, metres from the eye, Earth-fixed axes; w: its size, m (its falloff
    /// is softened inside it).</summary>
    public LightArray FlashPos;
    /// <summary>Per flash light, rgb: its intensity in display units × m²: a matte white surface facing it from d metres
    /// is lit by rgb / d², in the lights' units (as the sun's radiance is).</summary>
    public LightArray FlashRad;
    /// <summary>Per light, a spot's (a searchlight's beam): xyz its axis, Earth-fixed; w 1 / σ² of its Gaussian about it
    /// (σ in radians), 0 for a point light all round.</summary>
    public LightArray FlashDir;
    /// <summary>The haze between the eye and the sea, the world view's alone (docs/visuals/haze.md,
    /// <see cref="Map.HazeLook"/>): x its optical depth straight down from the eye (0: none), y the slab's
    /// backscatter.</summary>
    public Vector4 Haze;
    /// <summary>xy: 2 tan of the half fields over the target's pixels; zw: the tans (a pixel's slant off straight
    /// down).</summary>
    public Vector4 HazeView;
    /// <summary>rgb: the light on a level white deck (the sun's, the moon's and the sky's; at night the sea's night
    /// light, as the sea shows it), what the haze sends back up a share of.</summary>
    public Vector4 HazeLight;
    /// <summary>rgb: the sea's mean seen from above, what the haze scatters on toward the eye (by day the ambient from
    /// below, at night the night sea as drawn).</summary>
    public Vector4 HazeSea;
    /// <summary>The eye at night (<see cref="LightTable.Purkinje"/>): x how much of the natural light's colour goes to the
    /// rods' grey-blue; yzw the rods' tint (luminance 1).</summary>
    public Vector4 Night;
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

    /// <summary>The real light on a level surface the frame's light stands for, lux (under the cloud): the night eye's
    /// input, and the flashes' background (docs/visuals/muzzle-flashes.md, "Brightness without HDR").</summary>
    public double Lux;

    /// <summary>The flat look: the albedo as drawn, no light (the poster's "flat").</summary>
    public static ShipLights Flat => new() { Uniforms = new LightUniforms { Sky = new(1, 1, 1, 1) }, SunDir = new(0, 0, 1), MoonDir = new(0, 0, 1) };

    /// <summary>The light at a place, once a frame. Every vector unit, in one set of axes (Earth-fixed on the map,
    /// ship-local in the viewer): <paramref name="up"/> the local vertical, <paramref name="sun"/> and
    /// <paramref name="moon"/> toward each. <paramref name="moonLight"/>: <see cref="Moon.Light(Vec3d, WorldTime)"/>;
    /// <paramref name="moonPhaseLaw"/>: <see cref="Moon.PhaseLaw"/> of its phase (for time-lapse).
    /// <paramref name="motion"/>: <c>SunLapse.Motion</c>, 1 live; below it, <paramref name="dayMeans"/> is
    /// <c>SunLapse.DayMeans</c> there and the light fades to it. <paramref name="cloud"/>: the weather's cover, 0 clear
    /// to 1 overcast, which passes <paramref name="overcastShare"/> of the sky's light (the sim's
    /// <c>SimSettings.NightOvercastShare</c>).
    /// <para>By day the light table's model; once the sun is down, the night eye (docs/visuals/night-lights.md Q10a): the
    /// real light (the twilight, the stars, the moon, under the cloud, as <see cref="NightLight"/> has them) in lux, times
    /// the display's level for a white deck over the real one's, so the night's lights keep their ratios.</para></summary>
    public static ShipLights Compute(LightTable t, Vec3d up, Vec3d sun, Vec3d moon, double moonLight, double moonPhaseLaw,
        double motion = 1, (double Day, double Lit) dayMeans = default, double cloud = 0, double overcastShare = 0.15)
    {
        double s = Vec3d.Dot(sun, up), sm = Vec3d.Dot(moon, up);
        motion = Math.Clamp(motion, 0, 1);
        double c = Math.Clamp(cloud, 0, 1), through = 1 - c * (1 - overcastShare);
        float m = (float)motion;

        // the night, live: the real light in lux, the twilight's and the stars' through the cloud, the moon's direct
        // through what's clear and diffuse through the overcast
        double sunEl = Math.Asin(Math.Clamp(s, -1, 1)) * 180 / Math.PI;
        double twilight = NightLight.SunLux(Math.Min(sunEl, 0)) * through, stars = NightLight.StarLux * through;
        double moonFacing = NightLight.FullMoonLux * Math.Max(moonLight, 0), moonUp = Math.Max(sm, 0);
        double moonDirect = moonFacing * (1 - c), moonDiffuse = moonFacing * moonUp * c * overcastShare;
        double nightLux = twilight + stars + moonDirect * moonUp + moonDiffuse;
        double perLux = Eye(t, nightLux) / nightLux;
        var skyTint = Vector3.Lerp(Chroma(t.SkyNight), Chroma(t.SkyDusk), (float)SmoothStep(-12, -3, sunEl));
        var e = Extinct(t, sm);
        var moonRadNight = t.MoonTint * (e / Math.Max(Lum(e), 1e-6f)) * (float)(perLux * moonDirect);   // the moon's own dimming is in its light: here only its warmth
        var skyNight = skyTint * (float)(perLux * (twilight + stars + moonDiffuse)) + moonRadNight * (float)(t.MoonSky * moonUp);
        var seaNight = Chroma(t.SeaNight) * (float)(perLux * nightLux * t.NightBelow);

        // the day, live: the sun through the air, the ambient from dusk to day; handing over to the night as the sun goes
        // the last two degrees under the horizon (the night eye at sunset meets the dusk's ambient)
        var sunLive = t.SunZenith * Extinct(t, s) * (float)SmoothStep(-0.02, 0.06, s);
        float day = (float)SmoothStep(0.0, 0.2, s), dayShare = (float)SmoothStep(-0.035, 0.0, s);
        var skyLive = Vector3.Lerp(Vector3.Lerp(skyNight, t.SkyDusk, dayShare), t.SkyDay, day);
        var seaLive = Vector3.Lerp(Vector3.Lerp(seaNight, t.SeaDusk, dayShare), t.SeaDay, day);
        var moonRad = moonRadNight * (1 - dayShare) * m;

        // time-lapse: the day's mean on a level deck, from overhead; the night's by the moon's mean for its phase
        double lapseLux = stars + NightLight.FullMoonLux * moonPhaseLaw * t.MoonNightMean * ((1 - c) + c * overcastShare);
        double lapseLevel = Eye(t, lapseLux);
        var sunLapse = t.SunZenith * (float)dayMeans.Lit;
        var skyLapse = Vector3.Lerp(Chroma(t.SkyNight) * (float)lapseLevel, t.SkyDay, (float)dayMeans.Day);
        var seaLapse = Vector3.Lerp(Chroma(t.SeaNight) * (float)(lapseLevel * t.NightBelow), t.SeaDay, (float)dayMeans.Day);
        var sunRad = sunLive * m + sunLapse * (1 - m);
        double w1 = motion * Lum(sunLive), w0 = (1 - motion) * Lum(sunLapse);
        var sunDir = w1 + w0 > 1e-9 ? (sun * w1 + up * w0).Normalized() : sun;
        var sky = skyLive * m + skyLapse * (1 - m);
        var sea = seaLive * m + seaLapse * (1 - m);

        // shadows by the stronger light, by what each puts on a level deck
        double sunH = motion * Lum(sunLive) * Math.Max(s, 0), moonH = Lum(moonRad) * moonUp;
        double dom = Math.Max(sunH, moonH), weak = Math.Min(sunH, moonH), amb = Lum(sky);
        double opacity = dom > 1e-9 ? t.Shadow * (1 - Math.Pow((amb + weak) / (amb + dom), 1 / 2.2)) : 0;

        // the light the eye's adapted to: the day's by the sun where it's up (the day's model takes no cloud), the night's
        // below; in time-lapse the mean's
        double lux = s >= 0 ? NightLight.SunLux(sunEl) : nightLux;
        lux = lux * motion + (dayMeans.Day * NightLight.SunLux(30) + (1 - dayMeans.Day) * lapseLux) * (1 - motion);
        double rods = t.Purkinje * SmoothStep(t.PurkinjeLogLux[0], t.PurkinjeLogLux[1], Math.Log10(Math.Max(lux, 1e-8)));

        var rod = Chroma(t.RodTint);
        double level = Encode(Lum(sunRad) * Math.Max(Vec3d.Dot(sunDir, up), 0) + amb);
        return new ShipLights
        {
            Uniforms = new LightUniforms
            {
                SunRad = new Vector4(sunRad, 0),
                MoonRad = new Vector4(moonRad, 0),
                Sky = new Vector4(sky, (float)Math.Min(level, 1)),
                Sea = new Vector4(sea, t.Horizon),
                Look = new Vector4(1, t.Roughness, t.Knee, t.Specular * m),
                Night = new Vector4((float)rods, rod.X, rod.Y, rod.Z),
            },
            SunDir = sunDir, MoonDir = moon,
            ShadowDir = sunH >= moonH ? sun : moon, ShadowOpacity = opacity,
            Lux = lux,
        };
    }

    /// <summary>The night eye: the display's level for a lit white deck under <paramref name="lux"/>, linear; log-linear
    /// between the table's rows, on at the end rows' slopes below the first and held past the last.</summary>
    public static double Eye(LightTable t, double lux)
    {
        double x = Math.Log10(Math.Max(lux, 1e-8));
        var (xs, ys) = (t.NightEyeLogLux, t.NightEyeLevel);
        if (x >= xs[^1])
            return ys[^1] * t.EyeGain;
        int i = 1;
        while (i < xs.Length - 1 && x > xs[i])
            i++;
        double f = (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
        return Math.Exp(Math.Log(ys[i - 1]) + f * (Math.Log(ys[i]) - Math.Log(ys[i - 1]))) * t.EyeGain;
    }

    /// <summary>The rods' view of a linear colour (Purkinje: blues brighter, reds dark) as <see cref="LightUniforms.Night"/>
    /// sets it, shiplight.glsl's nightSee: what the natural light shows at night.</summary>
    public static Vector3 NightSee(in LightUniforms u, Vector3 c)
    {
        float rods = u.Night.X;
        if (rods <= 0)
            return c;
        float s = 0.03f * c.X + 0.55f * c.Y + 0.42f * c.Z;
        return Vector3.Lerp(c, new Vector3(u.Night.Y, u.Night.Z, u.Night.W) * s, rods);
    }

    /// <summary>A colour scaled to luminance 1.</summary>
    static Vector3 Chroma(Vector3 c) => c / Math.Max(Lum(c), 1e-6f);

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
