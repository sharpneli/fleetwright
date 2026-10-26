using Fleetwright.Map;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Tests;

/// <summary>The world view's haze (docs/visuals/haze.md): the sim's own extinction, more of it the higher the eye, never
/// more than the whole column of air; none in clear air; a fog lies low, so it adds most near the water.</summary>
public class HazeLookTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Paths.Root, "Content", "Sim", "settings.json"));

    [Fact]
    public void Clear_air_has_none()
    {
        Assert.Equal(0, HazeLook.OpticalDepth(double.PositiveInfinity, 5000, Settings));
        Assert.Equal(0, HazeLook.OpticalDepth(8000, 0, Settings));
    }

    [Fact]
    public void Grows_with_the_eyes_height_and_stops_at_the_column()
    {
        foreach (double vis in new[] { 500.0, 1000, 5000, 8000, 25000 })
        {
            double last = 0;
            foreach (double h in new[] { 100.0, 300, 1000, 3000, 6000, 9000 })
            {
                double tau = HazeLook.OpticalDepth(vis, h, Settings);
                Assert.True(tau > last, $"{vis} m at {h} m");
                last = tau;
            }
            // the whole column: no eye, however high, sees more haze than the air holds
            double sigma = SightModel.Extinction(vis, Settings);
            Assert.True(HazeLook.OpticalDepth(vis, 1e7, Settings) <= sigma * HazeLook.HazeScaleM * 1.0001);
            // and never more than the same air would be along the line of sight at the surface's density
            Assert.True(HazeLook.OpticalDepth(vis, 300, Settings) <= sigma * 300);
        }
    }

    [Fact]
    public void Thicker_weather_is_hazier_at_any_height()
    {
        foreach (double h in new[] { 150.0, 1500, 6000 })
            Assert.True(HazeLook.OpticalDepth(1000, h, Settings) > HazeLook.OpticalDepth(5000, h, Settings)
                && HazeLook.OpticalDepth(5000, h, Settings) > HazeLook.OpticalDepth(25000, h, Settings));
    }

    [Fact]
    public void A_fog_lies_low()
    {
        // past the deep haze's densest, what more there is is near the water: at 6 km up a 1 km fog has much less than
        // its surface density through a 1 km layer, and most of its extra over a 4 km haze within the first few hundred metres
        double fog = HazeLook.OpticalDepth(1000, 6000, Settings), haze = HazeLook.OpticalDepth(HazeLook.DeepestM, 6000, Settings);
        Assert.True(fog < SightModel.Extinction(1000, Settings) * 1000 * 0.5);
        double low = HazeLook.OpticalDepth(1000, 500, Settings) - HazeLook.OpticalDepth(HazeLook.DeepestM, 500, Settings);
        Assert.True(low > 0.7 * (fog - haze));
    }

    [Fact]
    public void Table()
    {
        // the doc's table: what shows of the scene (e^-tau) straight down at a zoom's eye, by visibility
        output.WriteLine("vis km   sigma/km   0.1 m/px (150 m)   0.5 (740 m)   1 (1.5 km)   2 (3 km)   4 (5.9 km)");
        foreach (double vis in new[] { 500.0, 1000, 2000, 5000, 8000, 13000, 25000 })
        {
            var row = $"{vis / 1000,6:0.0}   {SightModel.Extinction(vis, Settings) * 1000,8:0.000}";
            foreach (double mpp in new[] { 0.1, 0.5, 1, 2, 4 })
            {
                double tau = HazeLook.OpticalDepth(vis, mpp * 1080 / (2 * MapCamera.TanHalfY), Settings);
                row += $"   {tau,5:0.000} {Math.Exp(-tau),5:0.00}";
            }
            output.WriteLine(row);
        }
    }
}
