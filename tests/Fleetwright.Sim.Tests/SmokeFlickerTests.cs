using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The smoke's flicker on the guns' lines (docs/battle/error-sources.md ES6, <see cref="SmokeModel.Flicker"/>): its
/// mean holds the field's; none at no spread; through smoke whose mean shuts her she's glimpsed some of the time, and
/// through thin smoke lost some of the time, where the mean alone gave all or nothing.</summary>
public class SmokeFlickerTests(Xunit.Abstractions.ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    [Fact]
    public void The_flicker_keeps_the_fields_mean()
    {
        var r = RandomStream.Named(1, "flicker");
        double sum = 0;
        const int n = 200_000;
        for (int i = 0; i < n; i++)
            sum += SmokeModel.Flicker(r.NextGaussian(), Settings.SmokeFlickerLn);
        output.WriteLine($"mean {sum / n:F4} at a spread of {Settings.SmokeFlickerLn}");
        Assert.Equal(1, sum / n, 2);
        Assert.Equal(1, SmokeModel.Flicker(2.5, 0));
    }

    [Fact]
    public void Through_smoke_that_shuts_her_on_the_mean_she_is_glimpsed_and_thin_smoke_loses_her_at_times()
    {
        // a line whose clear margin is 20: lost past τ = ln 20 = 3.0. On the mean, τ 4.5 shuts her always and τ 2 never
        var r = RandomStream.Named(2, "flicker");
        double shut = Math.Log(20);
        int glimpsed = 0, lost = 0;
        const int n = 20_000;
        for (int i = 0; i < n; i++)
        {
            glimpsed += 4.5 * SmokeModel.Flicker(r.NextGaussian(), Settings.SmokeFlickerLn) < shut ? 1 : 0;
            lost += 2 * SmokeModel.Flicker(r.NextGaussian(), Settings.SmokeFlickerLn) >= shut ? 1 : 0;
        }
        output.WriteLine($"through τ 4.5 glimpsed {100.0 * glimpsed / n:F1}% of the windows; through τ 2 lost {100.0 * lost / n:F1}%");
        Assert.InRange((double)glimpsed / n, 0.15, 0.6);
        Assert.InRange((double)lost / n, 0.1, 0.5);
    }
}
