using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The Sea uniform block of Content/Shaders/Source/Include/seashade.glsl (std140, 11 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SeaUniforms
{
    public Vector4 On, Tiles, Off01, Off23, HexOff, HexS, Axes, VarU, Mss2, Body, Moon;
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
    /// vertical exaggeration: a look knob for when the sun is too low for the glint to show the waves from above.</summary>
    public static float Relief = 1;

    /// <summary>A debug view of the sea: 0 shaded, 1 its normal, 2 its slopes' spread.</summary>
    public static int Debug;

    /// <summary>Sets a knob by name, "facet:0.7", "relief:3", "chop:1", "debug:1" (the -sea= flag, for side-by-side
    /// captures); false for a bad one.</summary>
    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !float.TryParse(p[1], System.Globalization.CultureInfo.InvariantCulture, out float v))
            return false;
        switch (p[0])
        {
            case "facet": FacetLight = v; break;
            case "relief": Relief = v; break;
            case "chop": Chop = v; break;
            case "debug": Debug = (int)v; break;
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
    public void Update(in MapView view, SeaSim sim, double dtS, double speed, in ShipLights lights, double sunMotion)
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
            Moon = new Vector4((float)lights.MoonDir.X, (float)lights.MoonDir.Y, (float)lights.MoonDir.Z, Relief),
        };
    }

    static float Wrap(double v, double period) => (float)(v - Math.Floor(v / period) * period);

    static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
