using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The sea's motion over a hole (docs/battle/damage-control.md DC10): the flow through it the mean over the sea's height
/// moving about its level (<see cref="Flooding.MeanRoot"/>), its spread from the world's sea at the hole
/// (<see cref="ShipCondition.SeaSd"/>). A hole over the waterline stays dry in still water and takes the sea in a seaway, more on
/// her weather side than her lee.</summary>
public class FloodSeaTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>E[√max(min(μ + σZ, cap), 0)] by brute force: Simpson over ±10 sigmas.</summary>
    static double Brute(double mu, double sd, double cap)
    {
        const int n = 400_000;
        double a = -10, b = 10, h = (b - a) / n, sum = 0;
        for (int i = 0; i <= n; i++)
        {
            double z = a + i * h, head = Math.Min(mu + sd * z, cap);
            double f = (head > 0 ? Math.Sqrt(head) : 0) * Math.Exp(-0.5 * z * z) / Math.Sqrt(2 * Math.PI);
            sum += f * (i == 0 || i == n ? 1 : i % 2 == 1 ? 4 : 2);
        }
        return sum * h / 3;
    }

    [Theory]
    [InlineData(0.5, 0.3, double.PositiveInfinity)]
    [InlineData(-0.5, 0.3, double.PositiveInfinity)]
    [InlineData(0, 1, double.PositiveInfinity)]
    [InlineData(-2, 0.4, double.PositiveInfinity)]
    [InlineData(4, 0.6, double.PositiveInfinity)]
    [InlineData(2, 0.5, 1.5)]
    [InlineData(0.2, 0.6, 0.4)]
    [InlineData(-0.3, 0.6, 0.1)]
    public void The_mean_root_head_matches_the_integral(double mu, double sd, double cap)
    {
        double got = Flooding.MeanRoot(mu, sd, cap), want = Brute(mu, sd, cap);
        output.WriteLine($"μ {mu}, σ {sd}, cap {cap}: {got:0.00000} against {want:0.00000}");
        Assert.True(Math.Abs(got - want) <= Math.Max(0.005 * want, 2e-4), $"{got} against {want}");
    }

    [Fact]
    public void Still_water_is_the_root_of_the_head()
    {
        Assert.Equal(Math.Sqrt(0.7), Flooding.MeanRoot(0.7, 0, double.PositiveInfinity), 12);
        Assert.Equal(Math.Sqrt(0.4), Flooding.MeanRoot(0.7, 0, 0.4), 12);
        Assert.Equal(0, Flooding.MeanRoot(-0.1, 0, double.PositiveInfinity));
        Assert.Equal(0, Flooding.MeanRoot(1, 0.5, 0));
    }

    /// <summary>The water a destroyer stopped beam-on to the wind takes in her first five minutes (before her damage control has
    /// it plugged) through a hole 0.4 m over her waterline amidships, on the side the wind comes from or her lee (wind in m/s;
    /// 0 still water), and the sea's spread at it.</summary>
    static (double WaterT, double Sd) Holed(double windMs, bool weather, SimSettings? s = null)
    {
        var w = new World(Start, 3, s ?? Settings);
        w.Apply(Command.SetWeather(25_000, 270, windMs));   // from the west; she heads north, her port side to it
        var c = Fleet.Get("destroyer").Class!;
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        w.Apply(DamageSystem.SideHole(1, HoleAt.X, c.Hydrostatics.Lcf, 0.4, 0.1, starboard: !weather));
        w.Damage.Settle(w, 1, 5 * 60);
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1));
        double sd = 0;
        for (int i = 0; i < k.BreachM2.Length; i++)
            if (k.BreachM2[i] > 0)
                sd = Math.Max(sd, Flooding.SeaSd(k, i, s ?? Settings));
        return (k.WaterT, sd);
    }

    [Fact]
    public void A_hole_over_the_waterline_stays_dry_in_still_water_and_takes_the_sea_in_a_seaway()
    {
        var still = Holed(0, weather: true);
        var weather = Holed(10, weather: true);
        var lee = Holed(10, weather: false);
        var off = Holed(10, weather: true, Settings with { FloodSeaK = 0 });
        output.WriteLine($"0.1 m² 0.4 m up amidships, 5 min: still {still.WaterT:0.0} t; a 20 kn wind on her weather side {weather.WaterT:0.0} t " +
                         $"(σ {weather.Sd:0.00} m), her lee {lee.WaterT:0.0} t (σ {lee.Sd:0.00} m); the sea's gain 0 {off.WaterT:0.0} t");
        Assert.True(still.WaterT < 0.5, "still water: nothing over the hole's edge");
        Assert.True(off.WaterT < 0.5, "gain 0: still water");
        Assert.True(weather.WaterT > 5, "a seaway: the sea comes in over it");
        Assert.True(weather.Sd > lee.Sd && weather.WaterT > lee.WaterT, "more on her weather side");
    }
}
