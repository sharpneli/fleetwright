using Fleetwright.Map;
using Fleetwright.Sim.Core;

namespace Fleetwright.Tests;

/// <summary>The time-lapse sun: live up to 100,000x, gone well before the terminator sweeps three times a second, eased
/// rather than switched; and the day's mean is the sun's hours: half at the equinox, polar day and night at the
/// solstice.</summary>
public class SunLapseTests
{
    [Fact]
    public void Live_at_100k_gone_before_three_days_a_second()
    {
        Assert.Equal(1, SunLapse.Target(0));
        Assert.Equal(1, SunLapse.Target(100_000));
        Assert.Equal(0, SunLapse.Target(3 * 86_400 * 0.85));
        Assert.Equal(0, SunLapse.Target(300_000));
        Assert.Equal(0, SunLapse.Target(1_000_000));
    }

    [Fact]
    public void A_speed_change_eases()
    {
        var lapse = new SunLapse();
        lapse.Update(100_000, 1 / 60.0);
        Assert.Equal(1, lapse.Motion);   // the first frame starts where it should be
        lapse.Update(1_000_000, 1 / 60.0);
        Assert.InRange(lapse.Motion, 0.6, 0.99);
        for (int i = 0; i < 60; i++)
            lapse.Update(1_000_000, 1 / 60.0);
        Assert.InRange(lapse.Motion, 0, 0.05);
    }

    [Fact]
    public void The_faster_the_spin_the_quicker_the_fade()
    {
        static int FramesToFade(double speed)
        {
            var lapse = new SunLapse();
            lapse.Update(0, 1 / 60.0);
            int frames = 0;
            while (lapse.Motion > 0.05)
            {
                lapse.Update(speed, 1 / 60.0);
                frames++;
            }
            return frames;
        }
        int at300k = FramesToFade(300_000), at1M = FramesToFade(1_000_000);
        Assert.InRange(at300k, 40, 50);   // as before: about 0.75 s to 5 %
        Assert.InRange(at1M, 10, 16);     // about a quarter of a second
    }

    [Fact]
    public void Day_average_is_the_hours_of_sun()
    {
        double equator = Math.Sin(0), arctic = Math.Sin(80 * Math.PI / 180), solstice = Math.Sin(23.44 * Math.PI / 180);
        Assert.InRange(SunLapse.DayAverage(equator, 0, -0.10, 0.12), 0.48, 0.52);
        Assert.InRange(SunLapse.DayAverage(arctic, 0, -0.10, 0.12), 0.45, 0.55);
        Assert.Equal(1, SunLapse.DayAverage(arctic, solstice, -0.10, 0.12), 3);
        Assert.Equal(0, SunLapse.DayAverage(-arctic, solstice, -0.10, 0.12), 3);
        Assert.Equal(1, SunLapse.DayAverage(Earth.Up(80, 0), Earth.Up(23.44, 90)), 3);
    }
}
