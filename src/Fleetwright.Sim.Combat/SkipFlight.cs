using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Combat;

/// <summary>A shell's flight on from where it skipped off the water (docs/battle/shells-armour-damage.md E7's ricochet): flown
/// once, when it skips, by the exterior ballistics' own step (<see cref="Ballistics.Step"/>, the tables' integrator) from the
/// sea at its exit angle and speed, and kept as <see cref="Samples"/> samples every <see cref="SampleS"/> of its arc along
/// the sea from the skip, its height and their rates. A skip is short and flat, so a few dozen numbers a shell stand in for a
/// table of its own (which would be a table a speed for every shell). Read like a table's flight, Hermite in time; the Earth's
/// turning left out (a few centimetres over a skip's seconds). Pure, no garbage.</summary>
public static class SkipFlight
{
    /// <summary>Samples a flight keeps, every <see cref="SampleS"/>: 48 s, past the longest a skip at the steepest exit flies.</summary>
    public const int Samples = 96;
    public const double SampleS = 0.5;
    /// <summary>Doubles a sample (arc along the sea, height, and their rates) and a flight.</summary>
    public const int Fields = 4, Length = Samples * Fields;
    /// <summary>How far under the sea it's flown on, m: past where it comes down, for the hit query's run.</summary>
    const double BelowM = 100;

    /// <summary>Flies a shell (its drag constant, <see cref="Ballistics.DragK"/>) from the sea at an elevation and speed into
    /// its samples; how many it kept.</summary>
    public static int Fly(double dragK, double elevationDeg, double speedMs, Span<double> into)
    {
        var st = Ballistics.Start(elevationDeg, speedMs);
        int per = (int)Math.Round(SampleS / BallisticTable.IntegrationStep), n = 0;
        for (int step = 0; n < Samples; step++)
        {
            if (step % per == 0)
            {
                var (s, h, sd, hd) = Ballistics.Polar(st);
                (into[n * Fields], into[n * Fields + 1], into[n * Fields + 2], into[n * Fields + 3]) = (s, h, sd, hd);
                n++;
                if (h < -BelowM)
                    break;
            }
            st = Ballistics.Step(st, dragK, BallisticTable.IntegrationStep);
        }
        return n;
    }

    /// <summary>Where a flight is t seconds after its skip: arc along the sea and height, and their rates (cubic Hermite between
    /// samples; past its last, on at that sample's rates).</summary>
    public static (double S, double H, double SDot, double HDot) At(ReadOnlySpan<double> f, int n, double t)
    {
        if (n <= 0)
            return default;
        double x = Math.Max(t, 0) / SampleS;
        int a = Math.Min((int)x, n - 1);
        if (a >= n - 1)
        {
            int l = (n - 1) * Fields;
            double dt = t - (n - 1) * SampleS;
            return (f[l] + f[l + 2] * dt, f[l + 1] + f[l + 3] * dt, f[l + 2], f[l + 3]);
        }
        double u = x - a, u2 = u * u, u3 = u2 * u;
        double h00 = 2 * u3 - 3 * u2 + 1, h10 = u3 - 2 * u2 + u, h01 = -2 * u3 + 3 * u2, h11 = u3 - u2;
        double d00 = (6 * u2 - 6 * u) / SampleS, d10 = 3 * u2 - 4 * u + 1, d01 = (-6 * u2 + 6 * u) / SampleS, d11 = 3 * u2 - 2 * u;
        int i = a * Fields, j = i + Fields;
        double s = h00 * f[i] + h10 * SampleS * f[i + 2] + h01 * f[j] + h11 * SampleS * f[j + 2];
        double h = h00 * f[i + 1] + h10 * SampleS * f[i + 3] + h01 * f[j + 1] + h11 * SampleS * f[j + 3];
        double sd = d00 * f[i] + d10 * f[i + 2] + d01 * f[j] + d11 * f[j + 2];
        double hd = d00 * f[i + 1] + d10 * f[i + 3] + d01 * f[j + 1] + d11 * f[j + 3];
        return (s, h, sd, hd);
    }

    /// <summary>A flight in the world from where it skipped (on the sea) along a heading (unit, horizontal there): its position t
    /// seconds after the skip, Earth-centred, and its velocity; as <see cref="BallisticTable.Position"/> places a table's.</summary>
    public static Vec3d Position(Vec3d from, Vec3d forward, ReadOnlySpan<double> f, int n, double t, out Vec3d velocity)
    {
        var up = from.Normalized();
        double h0 = from.Length - Earth.Radius;
        var (s, h, sd, hd) = At(f, n, t);
        double th = s / Earth.Radius, c = Math.Cos(th), sn = Math.Sin(th), r = Earth.Radius + h0 + h;
        var radial = up * c + forward * sn;
        var along = forward * c - up * sn;
        velocity = radial * hd + along * (sd * r / Earth.Radius);
        return radial * r;
    }

    /// <summary>When it comes down through a height over the sea (m) after its top, s; NaN if it never does in its samples.</summary>
    public static double Descends(ReadOnlySpan<double> f, int n, double height)
    {
        for (int k = 1; k < n; k++)
        {
            int a = (k - 1) * Fields, b = k * Fields;
            if (f[b + 3] < 0 && f[a + 1] >= height && f[b + 1] < height)
            {
                // bisect the Hermite between the two samples
                double lo = (k - 1) * SampleS, hi = k * SampleS;
                for (int it = 0; it < 30; it++)
                {
                    double mid = (lo + hi) / 2;
                    if (At(f, n, mid).H >= height)
                        lo = mid;
                    else
                        hi = mid;
                }
                return (lo + hi) / 2;
            }
        }
        return double.NaN;
    }
}
