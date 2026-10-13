namespace Fleetwright.Sim.Core;

/// <summary>The weather at a place and time (docs/battle/plan.md step 3.2b): the meteorological visibility (how far a
/// large dark object shows against the horizon sky by day, Koschmieder's ε = 0.02), and the wind (the way it blows from,
/// and its speed). Smoke and a sea state read the wind later.</summary>
public readonly record struct WeatherHere(double VisibilityM, double WindFromDeg, double WindMs);

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
