namespace Fleetwright.Map;

/// <summary>The unit the map's distances read in: the grid's squares, its legend, the scale bar.</summary>
public enum DistanceUnit { NauticalMiles, Kilometres }

/// <summary>The map's distance unit, one setting (nautical miles for now; docs/visuals/grid.md), and distances shown in
/// it: in nautical miles, cables under a mile and metres under a cable.</summary>
public static class MapUnits
{
    public const double MetresPerNauticalMile = 1852, MetresPerCable = MetresPerNauticalMile / 10;

    public static DistanceUnit Unit = DistanceUnit.NauticalMiles;

    /// <summary>The unit's length, metres.</summary>
    public static double Metres => Unit == DistanceUnit.NauticalMiles ? MetresPerNauticalMile : 1000;

    /// <summary>A round length's own base, metres: the unit, or under it the cable (nm) or the metre.</summary>
    public static double Base(double m) =>
        m >= Metres * (1 - 1e-9) ? Metres : Unit == DistanceUnit.NauticalMiles && m >= MetresPerCable * (1 - 1e-9) ? MetresPerCable : 1;

    public static string Distance(double m)
    {
        if (Unit == DistanceUnit.Kilometres)
            return m >= 10_000 ? $"{m / 1000:#,0} km" : m >= 1000 ? $"{m / 1000:0.0} km" : $"{m:0} m";
        double b = Base(m);
        if (b == MetresPerNauticalMile)
            return m >= 10 * b ? $"{m / b:#,0} nm" : $"{m / b:0.#} nm";
        if (b == MetresPerCable)
        {
            string n = $"{m / b:0.#}";
            return n == "1" ? "1 cable" : $"{n} cables";
        }
        return $"{m:0} m";
    }
}
