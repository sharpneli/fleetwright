namespace Fleetwright.Sim.Core;

/// <summary>The weather at a place and time (docs/battle/plan.md step 3.2b): the meteorological visibility (how far a
/// large dark object shows against the horizon sky by day, Koschmieder's ε = 0.02), and the wind (the way it blows from,
/// and its speed), and the air's temperature over the sea's, K (docs/battle/smoke.md Z0: warm air over a cold sea is
/// stable and smoke lies thin, low and long; 0 neutral), and the cloud cover, 0 clear to 1 overcast (docs/battle/night.md
/// Q1: an overcast takes most of the moon's and the stars' light), and how strongly the sea glows where it's stirred, 0 to
/// 1 (phosphorescence: docs/visuals/night-lights.md decision 5, the look's now, the sight's cue later), and the swell from
/// elsewhere beside the wind's own sea (docs/battle/balance.md BL1a: its significant height, m, its peak period, s, and the
/// way it comes from, degrees, NaN 50° off the wind): the sea drawn and the sea the ships roll in
/// (<see cref="SeaState.Of"/>).</summary>
public readonly record struct WeatherHere(double VisibilityM, double WindFromDeg, double WindMs, double AirOverSeaK = 0, double Cloud = 0,
    double Phosphorescence = 0, double SwellHsM = WeatherHere.DefaultSwellHsM, double SwellTpS = WeatherHere.DefaultSwellTpS,
    double SwellFromDeg = double.NaN)
{
    /// <summary>The swell until a weather gives one: low and long, the cross-sea that makes the surface read as ocean
    /// (docs/visuals/sea.md W1).</summary>
    public const double DefaultSwellHsM = 1.0, DefaultSwellTpS = 12;

    /// <summary>The way the swell comes from, degrees: its own, or 50° off the wind's.</summary>
    public double SwellFrom => double.IsNaN(SwellFromDeg) ? (WindFromDeg + 50) % 360 : SwellFromDeg;
}

/// <summary>The world's weather: a function of position and time, so it can vary later (fronts, squalls, a forecast)
/// without anything asking it changing. One weather per scenario for now (<see cref="UniformWeather"/>).</summary>
public interface IWeather
{
    WeatherHere At(Vec3d position, WorldTime time);
}

/// <summary>The same weather everywhere, always.</summary>
public sealed record UniformWeather(WeatherHere Here) : IWeather
{
    public WeatherHere At(Vec3d position, WorldTime time) => Here;
}

/// <summary>Daylight at a place (docs/battle/plan.md step 3.2b): the sun's elevation and bearing there, from
/// <see cref="Sun"/>. Night, twilight and a target against the bright horizon are read from these.</summary>
public static class Light
{
    /// <summary>The sun's elevation over the horizon at a place, degrees (negative below it). A sphere's horizon, no
    /// refraction: twilight's bands are wide enough.</summary>
    public static double SunElevationDeg(Vec3d position, WorldTime time) =>
        Math.Asin(Math.Clamp(Vec3d.Dot(position.Normalized(), Sun.Direction(time)), -1, 1)) * 180 / Math.PI;

    /// <summary>The bearing of the sun from a place, degrees clockwise from north (toward the point it's overhead).</summary>
    public static double SunBearingDeg(Vec3d position, WorldTime time) => Earth.Bearing(position, Sun.Direction(time) * Earth.Radius);
}
