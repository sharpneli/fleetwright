using System.Globalization;

namespace Fleetwright.Designer;

/// <summary>The units numbers show in. The design always stores metric; in the game this becomes the nation's
/// option (historical navies use their own units, for flavour).</summary>
public enum UnitSystem
{
    Metric,
    Imperial,
}

/// <summary>What a knob's number measures, which decides its unit and how it shows.</summary>
public enum Quantity
{
    /// <summary>Knots in either system.</summary>
    Speed,

    /// <summary>Nautical miles in either system.</summary>
    Range,

    /// <summary>Plate thickness: mm or inches.</summary>
    Armour,

    /// <summary>Gun bore: mm or inches.</summary>
    Calibre,

    /// <summary>Lengths: metres or feet.</summary>
    Length,

    /// <summary>A whole number.</summary>
    Count,

    /// <summary>A plain number (coefficients, factors).</summary>
    Ratio,

    Days,
}

public static class Units
{
    const double MmPerIn = 25.4, MPerFt = 0.3048;

    /// <summary>Metric value per displayed unit.</summary>
    public static double Factor(Quantity q, UnitSystem u) => u == UnitSystem.Metric ? 1 : q switch
    {
        Quantity.Armour or Quantity.Calibre => MmPerIn,
        Quantity.Length => MPerFt,
        _ => 1,
    };

    public static string Unit(Quantity q, UnitSystem u) => q switch
    {
        Quantity.Speed => "kn",
        Quantity.Range => "nm",
        Quantity.Armour or Quantity.Calibre => u == UnitSystem.Metric ? "mm" : "in",
        Quantity.Length => u == UnitSystem.Metric ? "m" : "ft",
        Quantity.Days => "days",
        _ => "",
    };

    /// <summary>Decimals worth showing for a step of <paramref name="step"/> (in displayed units).</summary>
    public static int Decimals(double step) => step >= 1 ? 0 : step >= 0.1 ? 1 : 2;

    /// <summary>A metric value in displayed units, e.g. "279 mm" or "11.0 in".</summary>
    public static string Format(double metric, Quantity q, UnitSystem u, double displayStep, bool withUnit = true)
    {
        double v = metric / Factor(q, u);
        string s = v.ToString("N" + Decimals(displayStep), CultureInfo.InvariantCulture);
        string unit = Unit(q, u);
        return withUnit && unit.Length > 0 ? s + " " + unit : s;
    }

    /// <summary>A typed value back to metric: a bare number is in the displayed unit; "12in", "12\"", "305mm",
    /// "30ft", "9.1m" name their own.</summary>
    public static bool TryParse(string text, Quantity q, UnitSystem u, out double metric)
    {
        metric = 0;
        var t = text.Trim().ToLowerInvariant().Replace(" ", "");
        double factor = Factor(q, u);
        foreach (var (suffix, f) in new[] { ("mm", 1.0), ("nm", 1.0), ("kn", 1.0), ("in", MmPerIn), ("\"", MmPerIn), ("ft", MPerFt),
                     ("'", MPerFt), ("m", 1.0) })   // longest first: "nm" and "mm" before "m"
            if (t.EndsWith(suffix, StringComparison.Ordinal))
            {
                t = t[..^suffix.Length];
                factor = f;
                break;
            }
        if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            return false;
        metric = v * factor;
        return true;
    }
}
