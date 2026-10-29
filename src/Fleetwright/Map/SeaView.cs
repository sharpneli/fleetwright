using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The Sea uniform block of Content/Shaders/Source/Include/seashade.glsl (std140, 13 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SeaUniforms
{
    public Vector4 On, Tiles, Off01, Off23, HexOff, HexS, Axes, VarU, Mss2, Body, Moon, Night, NightDir;
}

/// <summary>The sea's frame and clock for the map (docs/visuals/sea.md W2-W4), on the CPU in double; the shader gets
/// <see cref="Uniforms"/>.
/// <list type="bullet">
/// <item>The frame is camera-relative: an anchor on the globe with its tangent plane (east and north there). On the
/// planning table it follows the camera; while the world view shows it stays put, so what's in view is consistent and
/// panning is seamless. Scrolled out to the table and back, the water may differ, as agreed.</item>
/// <item>The view's centre goes into the anchor's plane (gnomonic) and is wrapped on each texture's and hex lattice's
/// period, so the shader's floats stay small at any distance; the camera's east and north are turned into the plane's
/// axes (the meridians' convergence).</item>
/// <item>The sea's clock runs with the world's: frozen when paused, at its speed up to <see cref="SpeedCap"/>, wrapped
/// at <see cref="SeaSim.PeriodS"/> (the frequencies are quantised to it).</item>
/// </list>
/// Nothing here allocates.</summary>
public sealed class SeaView
{
    /// <summary>The fastest the waves run, times real time: past it they'd only alias.</summary>
    public const double SpeedCap = 8;

    /// <summary>Choppiness λ: how far the crests lean in toward each other.</summary>
    public static float Chop = 1.0f;

    /// <summary>The water body's reflectance, linear: a deep open-ocean blue, by eye, so that at a high sun the sea's
    /// mean comes near the chart's day sea and the cross-fade doesn't flash.</summary>
    public static Vector3 Body = new(0.010f, 0.050f, 0.105f);

    /// <summary>How much of the water body's light follows the facet it falls on (the rest goes by a level sea, the
    /// light diffusing under the surface): what shows the waves when the sun is too low for its glint to reach a view
    /// from above. By eye.</summary>
    public static float FacetLight = 0.7f;

    /// <summary>How much the slopes are steepened for the body's light alone (1: as they are), like a hillshade's
    /// vertical exaggeration, under a high sun; and under a low one (tuned, docs/visuals/sea.md "Low sun and night"):
    /// below <see cref="LowSunDeg"/>[0] the sun's glint stops reaching a view from above and the waves go flat, so the
    /// relief grows to <see cref="LowSunRelief"/> by <see cref="LowSunDeg"/>[1].</summary>
    public static float Relief = 1, LowSunRelief = 4;

    public static readonly double[] LowSunDeg = [40, 15];

    /// <summary>The night's light on the water (docs/visuals/night-lights.md Q10a): realistic, a night sea seen from
    /// above is lit by the sky all round, black and still, and so is one under a sun too low for its glint to reach the
    /// eye. So as the sun goes (fading in from <see cref="NightFromDeg"/>: 12° over the horizon to 2° under it) this share
    /// of the sky's own light (the night eye's, <see cref="ShipLights"/>) is taken from all round and given a direction,
    /// for the waves alone: the level stays the real night's, the swell shows in it. In twilight it comes from the sun's
    /// side, with no moon from there 40° up, while the moon is up at all from the moon's bearing, its height kept
    /// where it shades (25-60°).</summary>
    public static float NightShare = 0.6f;

    /// <summary>The sun's elevations, degrees, between which the night's light fades in: from the first down to the second.</summary>
    public static readonly double[] NightFromDeg = [12, -2];

    /// <summary>The slopes' steepening for the night's light (as <see cref="Relief"/> for the day's).</summary>
    public static float NightRelief = 7;

    /// <summary>A debug view of the sea: 0 shaded, 1 its normal, 2 its slopes' spread.</summary>
    public static int Debug;

    /// <summary>The wakes' gain over the bakes' realistic slopes (docs/visuals/wakes.md K5); 0 draws none.</summary>
    public static float WakeGain = 1;

    /// <summary>The foam lanes' gain over their realistic white water (docs/visuals/wakes.md K4); 0 draws none.</summary>
    public static float FoamGain = 1;

    /// <summary>Sets a knob by name, "facet:0.7", "relief:1", "lowsunrelief:4", "chop:1", "debug:1", "nightrelief:7"
    /// and "nightshare:0.6" (the share of the night sky's light given a direction, for the waves) (the -sea= flag,
    /// for side-by-side captures); false for a bad one.</summary>
    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !float.TryParse(p[1], System.Globalization.CultureInfo.InvariantCulture, out float v))
            return false;
        switch (p[0])
        {
            case "facet": FacetLight = v; break;
            case "relief": Relief = v; break;
            case "lowsunrelief": LowSunRelief = v; break;
            case "chop": Chop = v; break;
            case "debug": Debug = (int)v; break;
            case "wake": WakeGain = v; break;
            case "foam": FoamGain = v; break;
            case "nightrelief": NightRelief = v; break;
            case "nightshare": NightShare = v; break;
            default: return false;
        }
        return true;
    }

    Vec3d anchor, east, north;
    bool anchored;

    /// <summary>The sea's clock, seconds within the period.</summary>
    public double ClockS { get; private set; }

    /// <summary>How much of the world the view shows (<see cref="WorldView"/>): 0, no sea to simulate or shade.</summary>
    public double World { get; private set; }

    public SeaUniforms Uniforms;

    /// <param name="sim">The sea's sim, for its spectrum's statistics.</param>
    /// <param name="dtS">Real seconds since the last frame.</param>
    /// <param name="speed">The world clock's speed, 0 when paused.</param>
    /// <param name="lights">This frame's light (the ships'), for the moon's direction.</param>
    /// <param name="sunMotion"><see cref="SunLapse.Motion"/>: below 1 the glints fade out with the ships' shadows.</param>
    /// <param name="moonLight"><see cref="Moon.Light(Vec3d, WorldTime)"/> at the view's centre.</param>
    public void Update(in MapView view, SeaSim sim, double dtS, double speed, in ShipLights lights, double sunMotion, double moonLight)
    {
        World = WorldView.Factor(view);
        ClockS = (ClockS + dtS * Math.Clamp(speed, 0, SpeedCap)) % SeaSim.PeriodS;
        var target = (-view.Forward).Normalized();
        if (World <= 0 || !anchored)
        {
            (anchor, east, north, anchored) = (target, Earth.East(target), Earth.North(target), true);
        }
        var spec = sim.Spectrum;
        if (World <= 0 || spec == null)
        {
            Uniforms.On = Vector4.Zero;
            World = 0;
            return;
        }

        // the view's centre in the anchor's plane, and the camera's axes there
        double cosA = Vec3d.Dot(target, anchor);
        double x = Earth.Radius * Vec3d.Dot(target, east) / cosA, y = Earth.Radius * Vec3d.Dot(target, north) / cosA;
        var r = new Vector2((float)Vec3d.Dot(view.Right, east), (float)Vec3d.Dot(view.Right, north));
        var u = new Vector2((float)Vec3d.Dot(view.Up, east), (float)Vec3d.Dot(view.Up, north));
        r = Vector2.Normalize(r);
        u = Vector2.Normalize(u);

        double[] tile = SeaSpectrum.TileM;
        double l3 = tile[2] / 7.93, s0 = 0.6 * tile[0], s1 = 0.6 * tile[1];
        double mpp = view.MetresPerPixel;

        // the slope variance no cascade resolves, topped up to Cox and Munk's; the ripples take half of it
        var (tx, ty) = spec.CoxMunkMss;
        double rx = 0, ry = 0;
        foreach (var st in sim.Stats)
            (rx, ry) = (rx + st.MssX, ry + st.MssY);
        double ux = Math.Max(tx - rx, 0.0004), uy = Math.Max(ty - ry, 0.0004);
        var c2 = sim.Stats[2];
        double kx = c2.MssX > 0 ? Math.Sqrt(0.5 * ux / c2.MssX) : 0, ky = c2.MssY > 0 ? Math.Sqrt(0.5 * uy / c2.MssY) : 0;

        Uniforms = new SeaUniforms
        {
            On = new Vector4((float)World, (float)mpp, (float)(1 - SmoothStep(0.5, 1.5, mpp)), Debug),
            Tiles = new Vector4((float)tile[0], (float)tile[1], (float)tile[2], (float)l3),
            Off01 = new Vector4(Wrap(x, tile[0]), Wrap(y, tile[0]), Wrap(x, tile[1]), Wrap(y, tile[1])),
            Off23 = new Vector4(Wrap(x, tile[2]), Wrap(y, tile[2]), Wrap(x, l3), Wrap(y, l3)),
            // the hex hash repeats every 256 cells along the lattice's skewed axes: 256 S along x, 512·√3/2 S along y
            HexOff = new Vector4(Wrap(x, 256 * s0), Wrap(y, 443.405 * s0), Wrap(x, 256 * s1), Wrap(y, 443.405 * s1)),
            HexS = new Vector4((float)s0, (float)s1, (float)(SeaSim.Levels - 1), (float)spec.HsTarget),
            Axes = new Vector4(r.X, r.Y, u.X, u.Y),
            VarU = new Vector4((float)ux, (float)uy, (float)kx, (float)ky),
            Mss2 = new Vector4((float)c2.MssX, (float)c2.MssY, (float)Math.Clamp(sunMotion, 0, 1), (float)Earth.Radius),
            Body = new Vector4(Body, FacetLight),
            Moon = new Vector4((float)lights.MoonDir.X, (float)lights.MoonDir.Y, (float)lights.MoonDir.Z, DayRelief(target, lights)),
        };
        var (nightRad, nightDir, nightShare) = NightLight(target, lights, moonLight);
        Uniforms.Night = new Vector4(nightRad, NightRelief);
        Uniforms.NightDir = new Vector4((float)nightDir.X, (float)nightDir.Y, (float)nightDir.Z, nightShare);
    }

    /// <summary>The relief for the sun's and moon's light on the body: <see cref="Relief"/> under a high sun, growing to
    /// <see cref="LowSunRelief"/> as it gets low.</summary>
    static float DayRelief(Vec3d up, in ShipLights lights)
    {
        double sunEl = Math.Asin(Math.Clamp(Vec3d.Dot(lights.SunDir, up), -1, 1)) * 180 / Math.PI;
        return (float)(Relief + (LowSunRelief - Relief) * (1 - SmoothStep(LowSunDeg[1], LowSunDeg[0], sunEl)));
    }

    /// <summary>The night's light at a place (<see cref="NightShare"/>): its colour, faded in as the sun goes, the way
    /// toward it, Earth-fixed, and the share of the sky's light it takes (what the sky's all-round light keeps is the
    /// rest).</summary>
    internal static (Vector3 Rad, Vec3d Dir, float Share) NightLight(Vec3d up, in ShipLights lights, double moonLight)
    {
        double sunEl = Math.Asin(Math.Clamp(Vec3d.Dot(lights.SunDir, up), -1, 1)) * 180 / Math.PI;
        double night = 1 - SmoothStep(NightFromDeg[1], NightFromDeg[0], sunEl);
        if (night <= 0)
            return (Vector3.Zero, up, 0);
        double moonUp = SmoothStep(0, 0.004, moonLight);   // up at all: even a thin crescent gives the direction
        double moonEl = Math.Asin(Math.Clamp(Vec3d.Dot(lights.MoonDir, up), -1, 1)) * 180 / Math.PI;
        var moon = Toward(up, lights.MoonDir, Math.Clamp(moonEl, 25, 60));
        var stars = Toward(up, lights.SunDir, 40);
        var dir = (stars * (1 - moonUp) + moon * moonUp).Normalized();
        // the direction shapes the waves, not the level sea's brightness: what a level sea lets in from it is evened
        // to what it lets in from all round (the sky's ambient's own), so the share moved gives back the light it took
        float share = (float)(NightShare * night);
        double even = 1 / Math.Max(LetIn(Vec3d.Dot(dir, up)), 1e-3);
        var sky = lights.Uniforms.Sky;
        return (new Vector3(sky.X, sky.Y, sky.Z) * (float)(share * even), dir, share);
    }

    /// <summary>What a level sea lets in from a light at an elevation (its sine): its cosine through 1 - Fresnel, as
    /// seashade.glsl's seaLetIn.</summary>
    internal static double LetIn(double c) => c > 0 ? c * (1 - (0.02 + 0.98 * Math.Pow(1 - c, 5))) : 0;

    /// <summary>The direction on <paramref name="toward"/>'s bearing from a place, at an elevation.</summary>
    static Vec3d Toward(Vec3d up, Vec3d toward, double elevationDeg)
    {
        var h = toward - up * Vec3d.Dot(toward, up);
        h = h.Length > 1e-9 ? h.Normalized() : Earth.North(up);
        double e = elevationDeg * Math.PI / 180;
        return h * Math.Cos(e) + up * Math.Sin(e);
    }

    static float Wrap(double v, double period) => (float)(v - Math.Floor(v / period) * period);

    static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
