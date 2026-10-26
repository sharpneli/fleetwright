using System.Globalization;
using System.Numerics;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The haze between the eye and the sea in the world view (docs/visuals/haze.md): the weather's visibility
/// as the air the camera looks down through, so a hazy day washes the view out the higher it is, and never more than
/// that much air would. Display only; the extinction is the sim's own (<see cref="SightModel.Extinction"/>), so what
/// the player sees and what the lookouts see come from one haze.
/// <list type="bullet">
/// <item><b>The air's column.</b> Deep haze (the aerosol of the sea's boundary layer) thins with height over
/// <see cref="HazeScaleM"/>; past its densest (a visibility of <see cref="DeepestM"/>), what more a thicker day has is
/// mist and fog lying low over the water, thinning over <see cref="FogScaleM"/>. The optical depth straight down from
/// the eye is each one's surface extinction times its scale height times 1 - e^(-altitude / scale).</item>
/// <item><b>What it does</b> (shiplight.glsl's <c>hazeLinear</c>, every world-view shader): the scene through it times
/// e^-τ, plus the slab's backscatter of the light on it (<see cref="Backscatter"/>), plus the sea's light scattered on
/// through it.</item>
/// </list>
/// Its knobs, for side-by-side captures: <c>-haze=on:0,gain:2,hazescale:1000,fogscale:250,deepest:4000,back:0.15,slant:1</c>.</summary>
public static class HazeLook
{
    public static bool On = true;

    /// <summary>A scale on the optical depth (1: the air as the weather has it), for captures only.</summary>
    public static double Gain = 1;

    /// <summary>The deep haze's scale height, m: OPAC's maritime aerosols (Hess, Koepke and Schult 1998).</summary>
    public static double HazeScaleM = 1000;

    /// <summary>Mist and fog's, m: a sea fog's top is commonly a few hundred metres up.</summary>
    public static double FogScaleM = 250;

    /// <summary>The visibility, m, at which the deep haze is at its densest: thicker, the rest lies low over the water.
    /// Softly: the deep haze's extinction is cap·(1 - e^(-σ / cap)), the fog's the rest.</summary>
    public static double DeepestM = 4000;

    /// <summary>The slab's backscatter, γ in R = γτ / (1 + γτ): for a thin haze the share of its optical depth it sends
    /// back up to an eye straight above (a marine aerosol's lidar ratio, ~20 sr, gives ~0.14 at a high sun); a thick
    /// one's albedo climbs toward a fog top's.</summary>
    public static double Backscatter = 0.15;

    /// <summary>How much a pixel's ray slants through the haze: 1 the camera's own (a corner looks through ~25% more
    /// air than the centre), 0 every pixel as straight down.</summary>
    public static double Slant = 1;

    /// <summary>The haze's optical depth straight down to the sea from an altitude, m, for a visibility, m.</summary>
    public static double OpticalDepth(double visibilityM, double altitudeM, SimSettings s)
    {
        double sigma = SightModel.Extinction(visibilityM, s), cap = SightModel.Extinction(DeepestM, s);
        if (sigma <= 0 || altitudeM <= 0)
            return 0;
        double deep = cap * (1 - Math.Exp(-sigma / cap)), low = sigma - deep;
        return deep * HazeScaleM * (1 - Math.Exp(-altitudeM / HazeScaleM)) + low * FogScaleM * (1 - Math.Exp(-altitudeM / FogScaleM));
    }

    /// <summary>The frame's haze for the light block: through the view from its altitude, as much of it as the world
    /// shows (<paramref name="world"/>, <see cref="WorldView.Factor(in MapView)"/>: the table is a chart, no air).</summary>
    public static (Vector4 Haze, Vector4 View, Vector4 Light) Uniforms(in MapView view, WeatherHere weather, SimSettings s,
        in ShipLights lights, double world)
    {
        if (!On || Gain <= 0 || world <= 0)
            return default;
        double tau = Gain * world * OpticalDepth(weather.VisibilityM, view.Altitude, s);
        if (tau <= 0)
            return default;
        var up = view.Eye.Normalized();
        var u = lights.Uniforms;
        var light = new Vector3(u.SunRad.X, u.SunRad.Y, u.SunRad.Z) * (float)Math.Max(Vec3d.Dot(lights.SunDir, up), 0)
            + new Vector3(u.MoonRad.X, u.MoonRad.Y, u.MoonRad.Z) * (float)Math.Max(Vec3d.Dot(lights.MoonDir, up), 0)
            + new Vector3(u.Sky.X, u.Sky.Y, u.Sky.Z);
        double sx = Slant * view.TanHalfX, sy = Slant * view.TanHalfY;
        return (new Vector4((float)tau, (float)Backscatter, 0, 0),
            new Vector4((float)(2 * sx / view.Width), (float)(2 * sy / view.Height), (float)sx, (float)sy),
            new Vector4(light, 0));
    }

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "on": On = v != 0; break;
            case "gain": Gain = Math.Max(v, 0); break;
            case "hazescale": HazeScaleM = Math.Max(v, 1); break;
            case "fogscale": FogScaleM = Math.Max(v, 1); break;
            case "deepest": DeepestM = Math.Max(v, 100); break;
            case "back": Backscatter = Math.Max(v, 0); break;
            case "slant": Slant = Math.Clamp(v, 0, 1); break;
            default: return false;
        }
        return true;
    }
}
