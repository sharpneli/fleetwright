using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The sun at high time compression. Past a couple of days a second the terminator sweeps the globe at a
/// flashing rate (3 a second is photosensitive-epilepsy territory, about 260,000x; at 1,000,000x it jumps 70 degrees a
/// frame), so the map fades from the live sun to a time-lapse: each latitude lit by its mean over the day, ship shadows
/// gone, and the sun's track drawn along its parallel. <see cref="Motion"/> is the fade: 1 live, 0 time-lapse.</summary>
public sealed class SunLapse
{
    /// <summary>Days of world time per real second where the fade starts and ends.</summary>
    public const double FadeFrom = 1.5, FadeTo = 2.5;

    // A speed change fades rather than pops, and the achieved speed's noise can't flicker it. The ease lasts about as
    // many turns of the Earth at any speed (0.85 days a time constant, 0.25 s at 300,000x), so the faster the spin,
    // the quicker the fade; never longer than 0.25 s, never shorter than a few frames.
    const double EaseDays = 0.85, EaseMaxSeconds = 0.25, EaseMinSeconds = 0.03;

    double motion = -1;   // unset until the first frame, which starts where it should be

    /// <summary>1 for the live sun, 0 for the time-lapse, between while fading.</summary>
    public double Motion => Math.Max(motion, 0);

    /// <summary>The fade wanted at a speed (world seconds per real second, 0 when paused).</summary>
    public static double Target(double speed)
    {
        double daysPerSecond = speed / 86_400;
        double t = Math.Clamp((daysPerSecond - FadeFrom) / (FadeTo - FadeFrom), 0, 1);
        return 1 - t * t * (3 - 2 * t);
    }

    /// <summary>Eases <see cref="Motion"/> toward the speed's target over <paramref name="dt"/> real seconds.</summary>
    public void Update(double speed, double dt)
    {
        double target = Target(speed);
        double ease = Math.Clamp(EaseDays * 86_400 / Math.Max(speed, 1), EaseMinSeconds, EaseMaxSeconds);
        motion = motion < 0 ? target : motion + (target - motion) * (1 - Math.Exp(-Math.Max(dt, 0) / ease));
    }

    /// <summary>Over one turn of the Earth at a latitude, the mean of the terminator smoothstep(lo, hi, s), s the sine
    /// of the sun's elevation. The same 32-step sum as globe.frag's dayAverage, so ships match the sea under them.</summary>
    public static double DayAverage(double sinLat, double sinDec, double lo, double hi) => DayMeans(sinLat, sinDec, lo, hi).Day;

    /// <summary><see cref="DayAverage(double, double, double, double)"/> (Day), and the mean of the smoothstep times
    /// max(s, 0) (Lit: what a level deck gets of the sun): globe.frag's dayAverage, both terms.</summary>
    public static (double Day, double Lit) DayMeans(double sinLat, double sinDec, double lo, double hi)
    {
        double a = sinLat * sinDec, b = Math.Sqrt(Math.Max(1 - sinLat * sinLat, 0)) * Math.Sqrt(Math.Max(1 - sinDec * sinDec, 0));
        double day = 0, lit = 0;
        for (int i = 0; i < 32; i++)
        {
            double s = a + b * Math.Cos((i + 0.5) * (2 * Math.PI / 32)), d = SmoothStep(lo, hi, s);
            day += d;
            lit += d * Math.Max(s, 0);
        }
        return (day / 32, lit / 32);
    }

    /// <summary>The fraction of the day the sun is up at a point, for a sun direction (both unit, Earth-fixed).</summary>
    public static double DayAverage(Vec3d up, Vec3d sun) => DayAverage(up.Z, sun.Z, -0.10, 0.12);

    /// <summary><see cref="DayMeans(double, double, double, double)"/> at a point, for a sun direction.</summary>
    public static (double Day, double Lit) DayMeans(Vec3d up, Vec3d sun) => DayMeans(up.Z, sun.Z, -0.10, 0.12);

    static double SmoothStep(double e0, double e1, double x)
    {
        double t = Math.Clamp((x - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
