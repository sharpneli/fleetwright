using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The splash columns hiding a target (docs/battle/error-sources.md ES2): <see cref="SplashColumn.Hides"/>'s
/// geometry, an eye 10 m up looking east at a ship 5 km off (80 m long, 12 m in the beam, her top 20 m up).</summary>
public class SplashColumnTests
{
    static readonly Vec3d Eye = Earth.Position(56, 4);
    static readonly Vec3d Target = At(5_000, 0);

    /// <summary>A point so far east of the eye and so far north of the line.</summary>
    static Vec3d At(double east, double north)
    {
        var p = Eye;
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, east);
        var g = Earth.Forward(p, 0);
        Earth.Advance(ref p, ref g, north);
        return p;
    }

    /// <summary>Whether 12" columns (falling steeply) down at these points this long ago hide her, heading this way.</summary>
    static bool Hidden(double headingDeg, double ageS, double scale, params (double East, double North)[] falls)
    {
        var at = falls.Select(f => At(f.East, f.North)).ToArray();
        var peak = falls.Select(_ => (float)SplashColumn.PeakM(305, 30)).ToArray();
        var cal = falls.Select(_ => 305f).ToArray();
        long now = 100_000;
        var ms = falls.Select(_ => now - (long)(ageS * 1000)).ToArray();
        return SplashColumn.Hides(Eye, 10, Target, Earth.Forward(Target, headingDeg), 80, 12, 20, at, peak, cal, ms, now, scale);
    }

    [Fact]
    public void The_column_is_the_drawings()
    {
        Assert.Equal(54.9, SplashColumn.PeakM(305, 30), 1);
        Assert.Equal(27.45, SplashColumn.PeakM(305, 2), 2);   // half at a flat fall
        Assert.Equal(7.6, SplashColumn.WidthM(305), 1);
        Assert.Equal(3.35, SplashColumn.RiseS(54.9), 2);
        Assert.Equal(54.9, SplashColumn.HeightAt(54.9, 3.35), 1);
        Assert.Equal(0, SplashColumn.HeightAt(54.9, SplashColumn.LifeS(54.9)));
        Assert.True(SplashColumn.DenseS(54.9) < SplashColumn.LifeS(54.9));
    }

    [Fact]
    public void Shorts_side_by_side_hide_her_end_on_never_broadside()
    {
        (double, double)[] pair = [(4_700, -3), (4_700, 3)];
        Assert.True(Hidden(90, 2, 1, pair), "two 12-inch shorts side by side cover her stern on");
        Assert.False(Hidden(90, 2, 1, (4_700, 0)), "one is narrower than her beam: her edge shows");
        Assert.False(Hidden(0, 2, 1, pair), "broadside on they cover a tenth of her");
        Assert.False(Hidden(90, 2, 1, (5_300, -3), (5_300, 3)), "overs stand behind her");
        Assert.False(Hidden(90, 0.3, 1, pair), "still rising, under the line to her top");
        Assert.False(Hidden(90, 5, 1, pair), "thinned as they fall");
        Assert.False(Hidden(90, 2, 1, (4_700, -3), (4_700, 12)), "a gap between them");
        Assert.False(Hidden(90, 2, 0, pair), "masking off");
        Assert.True(Hidden(0, 2, 12, pair), "twelve times the size, broadside too");
    }
}
