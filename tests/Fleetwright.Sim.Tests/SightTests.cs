using Fleetwright.Sim.Core;
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
        Assert.DoesNotContain(eyes, e => e.Kind == EyeKind.Masthead);   // her masts carry no tops: no lookout aloft (F4d)
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
    [InlineData(24, 20000, 7, 25000, 30, false, 0.083104166702, 1926.04124999, 1.29595461697)]
    [InlineData(24, 30000, 7, 25000, 30, false, 8.30538589554, 602.676846267, 0.427893508467)]
    [InlineData(20, 30000, 20, 40000, 30, false, 10.9478277071, 444.130337571, 1.59975553324)]
    [InlineData(15, 34000, 7, double.PositiveInfinity, 30, false, 24.4565288112, 1.63041356626, 0.127747417247)]
    [InlineData(24, 4000, 7, 25000, -15, false, 0, 1941, 1.4278574755)]
    [InlineData(24, 25000, 7, 25000, -4, true, 2.51251756067, 1488.74683908, 1.90273745711)]
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

    /// <summary>The record (research/simulation/fire-control-research.md §2) on the ships' own eyes and silhouettes
    /// (fire-control.md F4d: their tops at their heights, their directors' optics), each first sighting the median one
    /// closing at 30 kn on a 45° aspect: Lion's battlecruisers first sighted Hipper's at ~24 km in a visibility of about
    /// that; Prince of Wales (KGV) Bismarck at ~35 km in clear Arctic air (the horizons' limit). A destroyer later than a
    /// battleship, bow on later than side on, at night a few kilometres (a lookout at Bismarck's highest eye).</summary>
    [Fact]
    public void The_reaches_match_the_record()
    {
        var bis = Fleet.Get("bismarck").Class!;
        var dd = Fleet.Get("destroyer").Class!;
        var lion = Fleet.Scenario("lion");
        var derf = Fleet.Scenario("derfflinger");
        var kgv = Fleet.Scenario("kgv");
        double kn30 = 30 * 0.5144;
        double jutland = Combat.SightModel.FirstSightingM(lion, derf, 25_000, 45, kn30, S) / 1000;
        double strait = Combat.SightModel.FirstSightingM(kgv, bis, double.PositiveInfinity, 45, kn30, S) / 1000;
        output.WriteLine($"Jutland: Lion first sights Derfflinger at {jutland:F1} km (~24); Denmark Strait: KGV Bismarck at {strait:F1} km (~35)");
        Assert.InRange(jutland, 22, 27);
        Assert.InRange(strait, 32, 38);
        Assert.True(Combat.SightModel.FirstSightingM(lion, dd, 25_000, 45, kn30, S) < Combat.SightModel.FirstSightingM(lion, bis, 25_000, 45, kn30, S) - 2000,
            "a destroyer later than a battleship");
        double eye = bis.Eyes[0].HeightM;
        Assert.True(Reach(eye, 7, 1, bis, 25_000, 30, false, 0, 0) < Reach(eye, 7, 1, bis, 25_000, 30, false, 90, 0) - 3);
        Assert.InRange(Reach(eye, 7, 1, bis, 25_000, -15, false, 90, 0), 3, 7);
        Assert.True(Reach(eye, 7, 1, bis, 25_000, -4, true, 90, 0) > Reach(eye, 7, 1, bis, 25_000, -4, false, 90, 0) + 3, "silhouetted at dusk");
        Assert.True(Reach(20, 20, 0.3, bis, 25_000, 30, false, 90, 0) > Reach(20, 7, 1, bis, 25_000, 30, false, 90, 0), "a rangefinder's magnification");
    }

    /// <summary>fire-control.md F4d: on the same target in clear air a taller eye sees further (the horizon), and a
    /// battleship's top is made out while her hull is still under the horizon.</summary>
    [Fact]
    public void Taller_eyes_see_further_and_a_top_shows_before_the_hull()
    {
        var lion = Fleet.Scenario("lion");
        double low = Combat.SightModel.MadeOutAtM(15, 7, lion, double.PositiveInfinity, 90, 30, false, S);
        double high = Combat.SightModel.MadeOutAtM(35, 7, lion, double.PositiveInfinity, 90, 30, false, S);
        output.WriteLine($"Lion side on in clear air: from 15 m {low / 1000:F1} km, from 35 m {high / 1000:F1} km");
        Assert.True(high > low + 4000, "a taller eye sees further");
        double deck = 0;
        foreach (double t in lion.Hull.DeckTops)
            deck = Math.Max(deck, t);
        double hidden = Combat.SightModel.HiddenM(35, high, S);
        Assert.True(hidden > deck - lion.Vertical.Waterline, $"hull down: {hidden:F1} m hidden, her deck {deck - lion.Vertical.Waterline:F1} m up");
    }

    /// <summary>fire-control.md F4d: a lookout stands in a top, a crow's nest's too; a bare mast has none aloft.</summary>
    [Fact]
    public void Lookouts_stand_in_tops_and_crows_nests_not_on_bare_masts()
    {
        var dd = Fleet.Get("destroyer").Class!;
        var nest = dd.Eyes.ToArray().Single(e => e.Kind == EyeKind.Top);
        Assert.Equal("Foremast top", dd.ComponentId(nest.Component));
        Assert.Equal(nest.HeightM, dd.Eyes[0].HeightM);   // her highest post
        var bis = Fleet.Get("bismarck").Class!;
        Assert.DoesNotContain(bis.Eyes.ToArray(), e => e.Kind is EyeKind.Top or EyeKind.Masthead);
    }

    /// <summary>fire-control.md F5b: each rangefinder stands on what's under it (a foretop director on her tripod, the
    /// gunhouses' sets on their turrets, after control's on a roof), and its make's kind is the instrument's.</summary>
    [Fact]
    public void Rangefinders_stand_on_their_supports()
    {
        var lion = Fleet.Get("lion").Class!;
        var eyes = lion.Eyes.ToArray();
        Assert.Equal(InstrumentSupport.Tripod, eyes.Single(e => e.Kind == EyeKind.Director).Support);
        Assert.All(eyes.Where(e => e.Kind == EyeKind.Turret), e => Assert.Equal(InstrumentSupport.Turret, e.Support));
        Assert.Equal(InstrumentSupport.Deck, eyes.Single(e => e.Kind == EyeKind.Rangefinder).Support);
        Assert.All(lion.Instruments.ToArray(), i => Assert.Equal(InstrumentKind.Coincidence, i.Kind));
        var derf = Fleet.Scenario("derfflinger");
        Assert.All(derf.Instruments.ToArray(), i => Assert.Equal(InstrumentKind.Stereo, i.Kind));
    }

    /// <summary>fire-control.md F5b (designer §3.1): the nominal ×28 on a tower at 25 m is ×28; a pole mast's top shakes
    /// it down, a gunhouse less than a roof, a gyro mounting lifts it, and past the glass's top magnification the image dims.</summary>
    [Fact]
    public void Useful_magnification_follows_the_support()
    {
        var o = new OpticsRow(28, 2, 12, 4, 0.75, InstrumentKind.Coincidence, 30);
        double M(InstrumentSupport s, double h, OpticsRow? row = null) => Combat.FireControl.Magnification(row ?? o, s, h);
        Assert.Equal(28, M(InstrumentSupport.Tower, 25), 6);
        Assert.Equal(28, M(InstrumentSupport.Tower, 10), 6);   // no stiffer for standing lower than 25 m
        Assert.InRange(M(InstrumentSupport.Pole, 40), 19, 22);   // designer D3: a pole mast ~×21-23 at 25-30
        Assert.True(M(InstrumentSupport.Turret, 11) > M(InstrumentSupport.Deck, 11));
        Assert.True(M(InstrumentSupport.Pole, 40, o with { Gyro = true }) > M(InstrumentSupport.Pole, 40));
        var big = o with { Magnification = 50 };
        Assert.InRange(M(InstrumentSupport.Tower, 25, big) / M(InstrumentSupport.Tower, 25, o with { Magnification = 35 }), 0.9, 1.03);   // ×50 no better than ×35
    }

    /// <summary>fire-control.md F5d: one reading's error in clear air, on a tower at 25 m, against the research's table 2
    /// (fire-control-research.md §3.3: the FQ2 9 ft 69 yd and the FT24 15 ft 42 yd at 10 kyd, 166 yd at 20) and Lienau's
    /// Zeiss 3 m (±65 m at 10 km, 165 m at 16 km).</summary>
    [Fact]
    public void One_reading_matches_the_research()
    {
        var o2 = Shipgen.FireControlTemplates.Get("Barr & Stroud FQ/FT (O2)")!.Value.Optics!;
        var zeiss = Shipgen.FireControlTemplates.Get("Zeiss stereo (WWI)")!.Value.Optics!;
        double U(Shipgen.OpticsTechInput b, double baseM, double rangeM)
        {
            var o = FireControlBlocks.Optics(b);
            return Combat.FireControl.RangefinderUnitM(baseM, o, Combat.FireControl.Magnification(o, InstrumentSupport.Tower, 25), rangeM);
        }
        const double kyd = 914.4, yd = 0.9144;
        Assert.InRange(U(o2, 2.74, 10 * kyd) / yd, 62, 76);
        Assert.InRange(U(o2, 4.57, 10 * kyd) / yd, 38, 46);
        Assert.InRange(U(o2, 4.57, 20 * kyd) / yd, 150, 183);
        Assert.InRange(U(zeiss, 3, 10_000), 58, 72);
        Assert.InRange(U(zeiss, 3, 16_000), 148, 182);
    }

    /// <summary>fire-control.md F5c: the air on a rangefinder. In clear air a capital ship at 15 km is ranged at the
    /// trained acuity; in Jutland's haze (16 km) a coincidence set's error grows about twice, a stereo set's about a
    /// third (the reference's k_optic: 2.25 and 1.4), and coated glass less; at dusk the margin, and so the error, worsens
    /// again; past the night's reach nothing is made out to range.</summary>
    [Fact]
    public void The_air_worsens_the_rangefinders_stereo_less()
    {
        var lion = Fleet.Get("lion").Class!;
        var derf = Fleet.Scenario("derfflinger");
        var dir = lion.Eyes.ToArray().Single(e => e.Kind == EyeKind.Director);
        var stereo = derf.Eyes.ToArray().Single(e => e.Kind == EyeKind.Director);
        double K(in EyeRow eye, ShipClass target, double vis, double sun = 30, double range = 15_000)
        {
            double m = Combat.SightModel.Margin(eye.HeightM, Combat.SightModel.Magnification(eye, S), target, new Combat.SightLine(range, 80, vis, sun, false), S);
            return m < 1 ? double.PositiveInfinity : Combat.SightModel.RangingFactor(m, eye.Optics, S);
        }
        double clear = K(dir, derf, 35_000), haze = K(dir, derf, 16_000), stereoHaze = K(stereo, lion, 16_000), dusk = K(dir, derf, 16_000, -10, 10_000);
        output.WriteLine($"coincidence k: clear {clear:F2}, haze {haze:F2}, haze at dusk {dusk:F2}; stereo in haze {stereoHaze:F2}");
        Assert.InRange(clear, 1, 1.1);
        Assert.InRange(haze, 1.6, 2.6);
        Assert.InRange(stereoHaze, 1.15, 1.6);
        Assert.True(dusk > K(dir, derf, 16_000, 30, 10_000) && double.IsFinite(dusk));
        Assert.True(double.IsPositiveInfinity(K(dir, derf, 16_000, -18)));   // a dark night: 15 km is past its reach
        var coated = dir.Optics with { Coated = true };
        Assert.True(Combat.SightModel.RangingFactor(1.5, coated, S) < Combat.SightModel.RangingFactor(1.5, dir.Optics, S));
    }

    /// <summary>fire-control.md F4d: the world's reach is the tallest eye's and silhouette's horizons among the classes at
    /// sea, it grows as a taller one comes, and the encounters hold ships within it; with the smoke field on, the tallest
    /// funnel smoke's top where it's higher (docs/battle/smoke.md Z6).</summary>
    [Fact]
    public void The_reach_follows_the_tallest_eye_and_top_at_sea()
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, S with { Smoke = false });
        Assert.Equal(0, w.Reach.SightM);
        var dd = Fleet.Get("destroyer").Class!;
        var derf = Fleet.Scenario("derfflinger");
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(dd), 56, 4, 0, 0, 1));
        double first = w.Reach.SightM;
        Assert.Equal(Combat.SightModel.ReachM(dd.Eyes[0].HeightM, dd.Silhouette.TopM, S), first, 6);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(derf), 58, 4, 180, 0, 2));
        double both = Combat.SightModel.ReachM(Math.Max(dd.Eyes[0].HeightM, derf.Eyes[0].HeightM), Math.Max(dd.Silhouette.TopM, derf.Silhouette.TopM), S);
        output.WriteLine($"reach: a destroyer alone {first / 1000:F1} km, with Derfflinger {both / 1000:F1} km");
        Assert.Equal(both, w.Reach.SightM, 6);
        Assert.Equal(Math.Max(both, S.EncounterRangeMinM), w.Reach.EncounterM, 6);
        Assert.Equal(0, w.Reach.SmokeTopM);
        // the field on: her smoke shows over a horizon further off
        var ws = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, S with { Smoke = true });
        ws.Apply(ShipSystem.Spawn(ws.Ships.AddClass(dd), 56, 4, 0, 0, 1));
        ws.Apply(ShipSystem.Spawn(ws.Ships.AddClass(derf), 58, 4, 180, 0, 2));
        double smoke = Math.Max(Combat.SmokeSources.TopM(dd, S), Combat.SmokeSources.TopM(derf, S));
        output.WriteLine($"with the smoke field: her smoke's top {smoke:F0} m, the reach {ws.Reach.SightM / 1000:F1} km");
        Assert.Equal(smoke, ws.Reach.SmokeTopM, 6);
        Assert.True(smoke > Math.Max(dd.Silhouette.TopM, derf.Silhouette.TopM));
        Assert.Equal(Combat.SightModel.ReachM(Math.Max(dd.Eyes[0].HeightM, derf.Eyes[0].HeightM), smoke, S), ws.Reach.SightM, 6);
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
