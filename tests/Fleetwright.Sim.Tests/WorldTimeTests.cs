using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The clock's arithmetic.</summary>
public class WorldTimeTests
{
    [Fact]
    public void An_event_ahead_is_at_least_a_millisecond_and_never_wraps_into_the_past()
    {
        // a damaged ship nearly stopped, her rhumb line nearly along a parallel: the polar cap some 1e16 m away at
        // 1e-4 m/s (Tsushima with 16 Bismarcks at 1M after the fight, 2026-10-25); cast to a long it saturated, and the
        // clock plus long.MaxValue came out a millisecond before the clock
        var now = WorldTime.FromUtc(new DateTime(1905, 5, 27, 10, 10, 0, DateTimeKind.Utc));
        Assert.Equal(WorldTime.FarAheadMs, WorldTime.AheadMs(1e16 / 1e-4));
        Assert.True(now + WorldTime.AheadMs(1e16 / 1e-4) > now);
        Assert.Equal(WorldTime.FarAheadMs, WorldTime.AheadMs(double.PositiveInfinity));
        Assert.Equal(1, WorldTime.AheadMs(0));
        Assert.Equal(1, WorldTime.AheadMs(0.0002));
        Assert.Equal(1501, WorldTime.AheadMs(1.5001));
    }
}
