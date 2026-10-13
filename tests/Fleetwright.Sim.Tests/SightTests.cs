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

/// <summary>The sighting model (docs/battle/plan.md step 3.2c): its reference, its independence of how often it looks,
/// the record's reaches, and a printed table of reaches for tuning.</summary>
public class SightModelTests(ITestOutputHelper output)
{
    static readonly Core.SimSettings S = Core.SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>The furthest range where a look finds the target at a rate of at least one in <paramref name="perS"/>
    /// seconds (0: where it can be made out at all), km.</summary>
    static double Reach(double eyeM, double mag, double search, ShipClass t, double vis, double sun, bool sil, double aspect, double perS)
    {
        for (double d = 80_000; d > 100; d -= 100)
        {
            var line = new Combat.SightLine(d, aspect, vis, sun, sil);
            double m = Combat.SightModel.Margin(eyeM, mag, t, line, S);
            if (perS == 0 ? m >= 1 : Combat.SightModel.Rate(m, search, S) >= 1 / perS)
                return d / 1000;
        }
        return 0;
    }

    /// <summary>research/simulation/sighting_ref.py's table, written apart from the sim: the hidden height and the margin
    /// for the area it gives (its schematic battleship), to the last digits it printed.</summary>
    [Theory]
    [InlineData(24, 20000, 7, 25000, 30, false, 0.083104166702, 1926.04124999, 1.64878924876)]
    [InlineData(24, 30000, 7, 25000, 30, false, 8.30538589554, 602.676846267, 0.614042920136)]
    [InlineData(20, 30000, 20, 40000, 30, false, 10.9478277071, 444.130337571, 2.09890480629)]
    [InlineData(15, 34000, 7, double.PositiveInfinity, 30, false, 24.4565288112, 1.63041356626, 0.127747417247)]
    [InlineData(24, 4000, 7, 25000, -15, false, 0, 1941, 1.5566438374)]
    [InlineData(24, 25000, 7, 25000, -4, true, 2.51251756067, 1488.74683908, 2.57097622057)]
    public void The_model_agrees_with_its_reference(double eye, double d, double mag, double vis, double sun, bool sil, double hidden, double area, double margin)
    {
        Assert.Equal(hidden, Combat.SightModel.HiddenM(eye, d, S), 1e-8);
        double m = Combat.SightModel.MarginOfArea(area, d, mag, new Combat.SightLine(d, 90, vis, sun, sil), S);
        Assert.Equal(margin, m, margin * 1e-9);
    }

    /// <summary>A look's chance comes from a rate, so ten looks of 0.5 s find it as often as one of 5 s.</summary>
    [Fact]
    public void How_often_it_looks_changes_nothing()
    {
        foreach (double margin in new[] { 1.2, 2, 5 })
        {
            double rate = Combat.SightModel.Rate(margin, 1, S);
            double missTen = Math.Pow(1 - Combat.SightModel.Chance(rate, 0.5), 10);
            Assert.Equal(Combat.SightModel.Chance(rate, 5), 1 - missTen, 12);
        }
        Assert.Equal(0, Combat.SightModel.Rate(0.99, 1, S));
        Assert.Equal(S.SightRatePerS, Combat.SightModel.Rate(S.SightFullMargin * 2, 1, S), 12);
    }

    /// <summary>The record (research/simulation/fire-control-research.md §2) with the classes' own silhouettes and a
    /// lookout at Bismarck's masthead: the Jutland's battlecruisers made each other out at ~24-26 km in a visibility of
    /// about that; the Denmark Strait's ~35 km in clear Arctic air (the horizon's limit); a destroyer later than a
    /// battleship, bow on later than side on; at night a few kilometres.</summary>
    [Fact]
    public void The_reaches_match_the_record()
    {
        var bis = Fleet.Get("bismarck").Class!;
        var dd = Fleet.Get("destroyer").Class!;
        double eye = bis.Eyes[0].HeightM;
        Assert.InRange(Reach(eye, 7, 1, bis, 25_000, 30, false, 90, 0), 24, 29);
        Assert.InRange(Reach(eye, 7, 1, bis, 1e9, 30, false, 90, 0), 32, 38);
        Assert.True(Reach(eye, 7, 1, dd, 25_000, 30, false, 90, 0) < Reach(eye, 7, 1, bis, 25_000, 30, false, 90, 0) - 2);
        Assert.True(Reach(eye, 7, 1, bis, 25_000, 30, false, 0, 0) < Reach(eye, 7, 1, bis, 25_000, 30, false, 90, 0) - 3);
        Assert.InRange(Reach(eye, 7, 1, bis, 25_000, -15, false, 90, 0), 3, 7);
        Assert.True(Reach(eye, 7, 1, bis, 25_000, -4, true, 90, 0) > Reach(eye, 7, 1, bis, 25_000, -4, false, 90, 0) + 3, "silhouetted at dusk");
        Assert.True(Reach(20, 20, 0.3, bis, 25_000, 30, false, 90, 0) > Reach(20, 7, 1, bis, 25_000, 30, false, 90, 0), "a rangefinder's magnification");
    }

    /// <summary>For tuning: how far each kind of eye makes out each kind of ship, by weather and light.</summary>
    [Fact]
    public void Print_reaches()
    {
        foreach (var target in new[] { "bismarck", "dreadnought", "destroyer" })
        {
            var t = Fleet.Get(target).Class!;
            output.WriteLine($"target {target} (top {t.Silhouette.TopM:F1} m), side on:");
            foreach (var (name, eye, mag, search) in new[] { ("masthead 24 m x7", 24.0, 7.0, 1.0), ("director 20 m x20", 20.0, 20.0, 0.3), ("bridge 15 m x7", 15.0, 7.0, 1.0) })
            {
                string row = $"  {name,-20}";
                foreach (double vis in new[] { 10_000.0, 25_000, 40_000, 1e9 })
                    row += $"  V {(vis > 1e8 ? "inf" : (vis / 1000).ToString("0")),3} km: seen {Reach(eye, mag, search, t, vis, 30, false, 90, 0),4:F1} / 1 min {Reach(eye, mag, search, t, vis, 30, false, 90, 60),4:F1}";
                output.WriteLine(row);
            }
            output.WriteLine($"  night (sun -15°), V 25: masthead seen {Reach(24, 7, 1, t, 25_000, -15, false, 90, 0):F1} km; dusk (sun -4°) {Reach(24, 7, 1, t, 25_000, -4, false, 90, 0):F1} km, silhouetted {Reach(24, 7, 1, t, 25_000, -4, true, 90, 0):F1} km; bow on by day V 25: {Reach(24, 7, 1, t, 25_000, 30, false, 0, 0):F1} km");
        }
    }
}
