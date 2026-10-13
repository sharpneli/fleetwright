using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Sighting's facts about a class (docs/battle/plan.md step 3.2a): its observation posts with their heights over
/// the sea and blind arcs, and its silhouette, compiled from its own rows.</summary>
public class SightTests(ITestOutputHelper output)
{
    static string Arcs(ulong blind)
    {
        var parts = new List<string>();
        for (int s = 0; s < Sight.Sectors; s++)
        {
            if ((blind & (1UL << s)) == 0 || s > 0 && (blind & (1UL << (s - 1))) != 0)
                continue;
            int e = s;
            while (e + 1 < Sight.Sectors && (blind & (1UL << (e + 1))) != 0)
                e++;
            parts.Add($"{s * Sight.SectorDeg:0}-{(e + 1) * Sight.SectorDeg:0}");
        }
        return parts.Count == 0 ? "all round" : "blind " + string.Join(", ", parts);
    }

    [Theory]
    [InlineData("dreadnought")]
    [InlineData("bismarck")]
    [InlineData("destroyer")]
    [InlineData("brennus")]
    [InlineData("tramp_1914")]
    public void Every_class_has_posts_and_a_silhouette(string design)
    {
        var c = Fleet.Get(design).Class!;
        var sil = c.Silhouette;
        var side = c.Numbers(sil.SideAreaM2);
        var end = c.Numbers(sil.EndAreaM2);
        output.WriteLine($"{design}: top {sil.TopM:F1} m over the sea; side on {side[0]:F0} m² ({Sight.AreaAbove(side, 10):F0} above 10 m, {Sight.AreaAbove(side, 20):F0} above 20 m), end on {end[0]:F0} m²; {sil.Funnels} funnels, {sil.Masts} masts, {sil.MainMounts} main mounts");
        foreach (var e in c.Eyes)
            output.WriteLine($"  {e.Kind,-12} {c.ComponentId(e.Component),-26} {e.HeightM,5:F1} m  rf {e.RangefinderM:F1}  {Arcs(e.Blind)}");
        Assert.NotEmpty(c.Eyes.ToArray());
        Assert.True(c.Eyes[0].HeightM >= c.Eyes[^1].HeightM);
        Assert.True(sil.TopM > c.Vertical.Freeboard);
        Assert.True(side[0] > c.Length * c.Vertical.Freeboard * 0.8, "the hull's side at least");
        for (int m = 1; m < side.Length; m++)
            Assert.True(side[m] <= side[m - 1] && end[m] <= end[m - 1]);
        Assert.Equal(0, Sight.AreaAbove(side, sil.TopM + 1));
    }

    /// <summary>Bismarck's posts as her plan puts them: the mastheads and the foretop director see all round; the
    /// bridge is blind astern (the funnel), aft control dead ahead (the funnel) and astern (AA director 2 stands right
    /// behind it on its level); the directors carry their rangefinders; heights over the sea from the waterline.</summary>
    [Fact]
    public void Bismarcks_posts_see_as_her_plan_has_them()
    {
        var c = Fleet.Get("bismarck").Class!;
        var eyes = c.Eyes.ToArray();
        var main = eyes.First(e => c.ComponentId(e.Component) == "Main director");
        Assert.Equal(EyeKind.Director, main.Kind);
        Assert.Equal(0UL, main.Blind);
        Assert.Equal(c.Directors[c.Components[main.Component].Detail].RangefinderM, main.RangefinderM);
        Assert.Equal(c.Components[main.Component].Bounds.Base + Sight.EyeOverFloorM - c.Vertical.Waterline, main.HeightM, 6);
        Assert.All(eyes.Where(e => e.Kind == EyeKind.Masthead), e => Assert.Equal(0UL, e.Blind));
        var bridge = eyes.Single(e => e.Kind == EyeKind.Bridge);
        var aft = eyes.Single(e => e.Kind == EyeKind.AftControl);
        Assert.True(bridge.Sees(0) && bridge.Sees(90) && !bridge.Sees(180));
        Assert.True(aft.Sees(90) && aft.Sees(270) && !aft.Sees(0) && !aft.Sees(180));
        Assert.Equal(eyes.Length, eyes.Select(e => e.Component).Distinct().Count());
    }

    [Fact]
    public void Every_golden_class_has_a_post_and_its_silhouette_shrinks_with_height()
    {
        int n = 0;
        foreach (var b in Fleet.All)
        {
            if (b.Class is not { } c)
                continue;
            n++;
            Assert.True(c.Eyes.Length > 0, b.Case.Name);
            var side = c.Numbers(c.Silhouette.SideAreaM2);
            Assert.True(side.Length > 1 && side[0] > 0, b.Case.Name);
            foreach (var e in c.Eyes)
                Assert.True(e.HeightM > 0 && e.HeightM < c.Silhouette.TopM + 0.01, $"{b.Case.Name}: {c.ComponentId(e.Component)} at {e.HeightM:F1} m");
        }
        output.WriteLine($"{n} classes");
    }
}
