namespace Fleetwright.Shipgen;

/// <summary>Special functions .NET's Math lacks.</summary>
public static class SpecialFunctions
{
    // Lanczos approximation, g = 6.0246..., 13 terms (the coefficients CPython's math.gamma uses)
    const double LanczosG = 6.024680040776729583740234375;
    const double LanczosGMinusHalf = 5.524680040776729583740234375;
    static readonly double[] LanczosNum =
    [
        23531376880.410759688572007674451636754734846804940,
        42919803642.649098768957899047001988850926355848959,
        35711959237.355668049440185451547166705960488635843,
        17921034426.037209699919755754458931112671403265390,
        6039542586.3520280050642916443072979210699388420708,
        1439720407.3117216736632230727949123939715485786772,
        248874557.86205415651146038641322942321632125127801,
        31426415.585400194380614231628318205362874684987640,
        2876370.6289353724412254090516208496135991145378768,
        186056.26539522349504029498971604569928220784236328,
        8071.6720023658162106380029022722506138218516325024,
        210.82427775157934587250973392071336271166969580291,
        2.5066282746310002701649081771338373386264310793408,
    ];
    static readonly double[] LanczosDen =
        [0.0, 39916800.0, 120543840.0, 150917976.0, 105258076.0, 45995730.0, 13339535.0, 2637558.0, 357423.0, 32670.0,
         1925.0, 66.0, 1.0];

    static double LanczosSum(double x)
    {
        double num = 0.0, den = 0.0;
        if (x < 5.0)
        {
            for (int i = LanczosNum.Length - 1; i >= 0; i--)
            {
                num = num * x + LanczosNum[i];
                den = den * x + LanczosDen[i];
            }
        }
        else
        {
            for (int i = 0; i < LanczosNum.Length; i++)
            {
                num = num / x + LanczosNum[i];
                den = den / x + LanczosDen[i];
            }
        }
        return num / den;
    }

    /// <summary>The gamma function for 0 &lt; x &lt;= 140 (the range the hull model needs).</summary>
    public static double Gamma(double x)
    {
        if (!(x > 0 && x <= 140))
            throw new ArgumentOutOfRangeException(nameof(x), x, "Gamma takes 0 < x <= 140");
        double y = x + LanczosGMinusHalf;
        double z = x > LanczosGMinusHalf ? y - x - LanczosGMinusHalf : y - LanczosGMinusHalf - x;
        z = z * LanczosG / y;
        double r = LanczosSum(x) / Math.Exp(y);
        r += z * r;
        return r * Math.Pow(y, x - 0.5);
    }
}

/// <summary>Binary searches on an ascending list.</summary>
public static class SortedListExtensions
{
    /// <summary>The first index whose value is not less than x (where x would go before its equals).</summary>
    public static int LowerBound(this IReadOnlyList<double> a, double x)
    {
        int lo = 0, hi = a.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (a[mid] < x)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    /// <summary>The first index whose value is greater than x (where x would go after its equals).</summary>
    public static int UpperBound(this IReadOnlyList<double> a, double x)
    {
        int lo = 0, hi = a.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (x < a[mid])
                hi = mid;
            else
                lo = mid + 1;
        }
        return lo;
    }
}
