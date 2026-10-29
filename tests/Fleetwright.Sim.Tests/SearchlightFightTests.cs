using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Searchlights in the fight (docs/battle/night.md Q4): the beam's light by range and haze, a consort seeing the
/// lit ship further than the operator, the lamp seen far, and in the world: an order struck, exposed and held on its
/// target, lighting her for every eye, showing the lamp to her, sweeping an arc, doused; the same twice over.</summary>
public class SearchlightFightTests(ITestOutputHelper output)
{
    static readonly SimSettings S = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly double Moonless = NightLight.ClearSkyCdM2(-30, S);

    /// <summary>A Great War 36-inch's light on a ship falls as the square of the range and faster in haze, and off its
    /// axis by its profile; its reach shortens with the haze through the 2-4 km of the record's useful range.</summary>
    [Fact]
    public void A_beam_lights_less_with_range_and_haze()
    {
        var lion = Fleet.Scenario("lion");
        var (big, offset) = SearchlightModel.Biggest(lion);
        var l = lion.Searchlights[big];
        var (sigma, keep) = SearchlightModel.Held(l.SpreadDeg, SearchlightModel.PointingDeg(l.Control, S));
        double peak = l.BeamCd * keep;
        Assert.True(SearchlightModel.LuxAt(peak, 1000, 25_000) > 3.5 * SearchlightModel.LuxAt(peak, 2000, 25_000));
        Assert.True(SearchlightModel.LuxAt(peak, 2000, 25_000) > SearchlightModel.LuxAt(peak, 2000, 5_000));
        Assert.True(SearchlightModel.Toward(l.BeamCd, 2 * sigma, sigma, keep) < 0.2 * SearchlightModel.Toward(l.BeamCd, 0, sigma, keep));
        double clear = SearchlightModel.ReachM(l, offset, 25_000, Moonless, S), hazy = SearchlightModel.ReachM(l, offset, 5_000, Moonless, S);
        output.WriteLine($"Lion's 36-inch reaches {clear / 1000:F1} km in 25 km visibility, {hazy / 1000:F1} km in 5 km");
        Assert.InRange(clear, 3_500, 7_000);
        Assert.InRange(hazy, 1_000, 2_500);
        Assert.True(SearchlightModel.ReachM(l, offset, 10_000, Moonless, S) is > 2_000 and < 4_000);
    }

    /// <summary>The operator, looking down his own beam, has its light scattered back in his eyes: a consort a kilometre
    /// off sees the lit destroyer much better, and both better than in the dark.</summary>
    [Fact]
    public void A_consort_sees_the_lit_ship_better_than_the_operator()
    {
        var lion = Fleet.Scenario("lion");
        var v25 = Fleet.Scenario("v25");
        var (big, offset) = SearchlightModel.Biggest(lion);
        var l = lion.Searchlights[big];
        var (sigma, keep) = SearchlightModel.Held(l.SpreadDeg, SearchlightModel.PointingDeg(l.Control, S));
        double d = 2_500, vis = 10_000, eye = lion.Eyes[0].HeightM, lux = SearchlightModel.LuxAt(l.BeamCd * keep, d, vis);
        double Margin(double veil, double lit) => SightModel.Margin(eye, S.SightLookoutMagnification, v25,
            new SightLine(d, 90, vis, Moonless, 0, Glass: SightModel.LookoutGlass(SightModel.PupilMm(Moonless + veil), S), LitLux: lit, VeilCdM2: veil), S);
        double dark = Margin(0, 0);
        double op = Margin(SearchlightModel.BackscatterCdM2(l.BeamCd, l.SpreadDeg, l.MirrorM, 0, sigma, keep, offset, d, vis, S), lux);
        double consort = Margin(SearchlightModel.BackscatterCdM2(l.BeamCd, l.SpreadDeg, l.MirrorM, 0, sigma, keep, 1000, d, vis, S), lux);
        output.WriteLine($"V25 at {d / 1000} km, {vis / 1000} km visibility: margin dark {dark:F2}, lit to the operator {op:F2}, to a consort {consort:F1}");
        Assert.True(dark < 1, "unlit, not made out");
        Assert.True(op >= 1, "the operator makes her out by his beam");
        Assert.True(consort > 3 * op, "a consort far better");
    }

    /// <summary>The lamp shows to the horizon to an eye in its beam, far less off it, and its beam from beside further
    /// than the front glass at 45°.</summary>
    [Fact]
    public void The_lamp_shows_far_in_its_beam()
    {
        var lion = Fleet.Scenario("lion");
        var l = lion.Searchlights[SearchlightModel.Biggest(lion).Light];
        var (sigma, keep) = SearchlightModel.Held(l.SpreadDeg, SearchlightModel.PointingDeg(l.Control, S));
        double At(double cd, double d) => SightModel.LightMargin(15, cd, l.HeightM, d, 25_000, Moonless, S);
        Assert.True(At(SearchlightModel.LampCd(l.BeamCd, 0, sigma, keep, S), 25_000) > 10, "in its beam, past 20 km");
        Assert.True(At(SearchlightModel.LampCd(l.BeamCd, 45, sigma, keep, S), 25_000) < 1, "45° off, not at 25 km");
        Assert.True(At(SearchlightModel.ColumnCd(l.BeamCd, l.SpreadDeg, 25_000, S), 12_000) > 1, "its beam from beside at 12 km");
    }

    static World Night(out int lion, out int destroyer)
    {
        var w = new World(WorldTime.FromUtc(1916, 1, 15, 23), 7, S);
        int c = w.Ships.AddClass(Fleet.Scenario("lion"));
        int d = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetWeather(20_000));
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 0, 1));
        w.Apply(ShipSystem.Spawn(d, 56, 4.012, 180, 0, 2));   // 0.75 km east of her, abeam to starboard (inside a moonless night's sight)
        (lion, destroyer) = (1, 2);
        return w;
    }

    /// <summary>Made ready on a contact, the lights are trained dark on her: lit, she's lit the moment the iris opens (the
    /// German method, docs/battle/night.md Q3b); lights ready on another first train onto her, then open.</summary>
    [Fact]
    public void Lights_trained_dark_on_a_ship_expose_at_once()
    {
        long Exposing(bool readyOnHer)
        {
            var w = Night(out int lion, out int dd);
            w.AdvanceTo(w.Time + 20_000);
            w.Apply(SearchlightSystem.Order(lion, SearchlightOrder.Ready, readyOnHer ? dd : 0));
            w.AdvanceTo(w.Time + 20_000);   // struck and trained
            Assert.False(w.Searchlights.OrderOf(lion, w.Time.Ms).Exposed, "ready: dark");
            w.Apply(SearchlightSystem.Order(lion, SearchlightOrder.Illuminate, dd));
            long t0 = w.Time.Ms;
            while (!w.Searchlights.OrderOf(lion, w.Time.Ms).Exposed)
                w.AdvanceTo(w.Time + 250);
            return w.Time.Ms - t0;
        }
        long trained = Exposing(true), untrained = Exposing(false);
        output.WriteLine($"exposed {trained} ms after the order when trained dark on her, {untrained} ms when not");
        Assert.InRange(trained, 0, (long)(S.SearchlightExposeS * 1000) + 250);
        Assert.InRange(untrained, (long)((S.SearchlightTrainS + S.SearchlightExposeS) * 1000) - 250, (long)((S.SearchlightTrainS + S.SearchlightExposeS) * 1000) + 250);
    }

    /// <summary>Ordered on a contact, the lights are struck, then exposed and held on her: she's lit for every eye, her
    /// eyes see the lamp (a point to aim at), and her lookouts' view round it is glared; doused, all of it goes.</summary>
    [Fact]
    public void Lights_held_on_a_ship_light_her_and_show_the_lamp()
    {
        var w = Night(out int lion, out int dd);
        w.AdvanceTo(w.Time + 20_000);
        Assert.True(w.Sightings.Of(1)!.IndexOf(dd) >= 0, "Lion holds the destroyer");
        w.Apply(SearchlightSystem.Order(lion, SearchlightOrder.Illuminate, dd));
        w.AdvanceTo(w.Time + 5_000);
        Assert.False(w.Searchlights.OrderOf(lion, w.Time.Ms).Exposed, "the arcs not yet struck");
        Assert.Equal(0, w.Lights.Beams);
        w.AdvanceTo(w.Time + 12_000);
        Assert.True(w.Searchlights.OrderOf(lion, w.Time.Ms).Exposed);
        Assert.Equal(S.SearchlightsOnOne, w.Lights.Beams);
        var store = w.Ships.Store;
        int ls = store.SlotOf(lion), ds = store.SlotOf(dd);
        var (lux, from) = w.Lights.On(ds, store[ds].Position, 20_000);
        var (lamp, column, h) = w.Lights.LampsOf(ls, store[ds].Position, 20_000, S);
        double veil = w.Lights.VeilCdM2(ds, store[ds].Position, store[ls].Position + store[ls].Forward * 200, 20_000, S);
        output.WriteLine($"on her {lux:F1} lux from {from:F0}°; Lion's lamps toward her {lamp:0.0e0} cd (beams {column:0.0e0} cd from beside) {h:F1} m up; " +
                         $"glare 200 m off them {veil:F2} cd/m²");
        Assert.InRange(lux, 50, 500);   // 0.75 km off (5-100 at 1.7 km before docs/battle/night.md Q1b)
        Assert.InRange(Math.Abs(from - 270 - 360 * Math.Round((from - 270) / 360)), 0, 5);   // from the west, where Lion is
        Assert.True(lamp > 1e7, "her eyes look into the beam");
        Assert.True(veil > 100 * Moonless, "glared round the lamp");
        // a ship off to the side sees no lamp, but the beam
        var side = Earth.Position(56.02, 4.0);
        var (lampSide, columnSide, _) = w.Lights.LampsOf(ls, side, 20_000, S);
        Assert.True(lampSide < 1e-3 * lamp && columnSide > 100);
        w.Apply(SearchlightSystem.Order(lion, SearchlightOrder.Off));
        w.AdvanceTo(w.Time + 1_000);
        Assert.Equal(0, w.Lights.Beams);
    }

    /// <summary>Searching, her lights split the arc and sweep it back and forth, never outside it, nor where her own upper
    /// works stand in their way.</summary>
    [Fact]
    public void Searching_lights_sweep_their_arc()
    {
        var w = Night(out int lion, out _);
        w.Apply(SearchlightSystem.Order(lion, SearchlightOrder.Search, count: 1, centreDeg: 90, halfDeg: 30));
        w.AdvanceTo(w.Time + 15_000);
        double lo = double.MaxValue, hi = double.MinValue;
        for (int t = 0; t < 60; t++)
        {
            w.AdvanceTo(w.Time + 1_000);
            for (int i = 0; i < w.Lights.Count; i++)
                if (!double.IsNaN(w.Lights.AxisDeg(i)))
                    (lo, hi) = (Math.Min(lo, w.Lights.AxisDeg(i)), Math.Max(hi, w.Lights.AxisDeg(i)));
        }
        output.WriteLine($"swept {lo:F1}° to {hi:F1}°");   // (Lion's upper works stand in her lights' way 60-67.5° off the bow)
        Assert.True(lo >= 59.9 && hi <= 120.1 && hi - lo >= 45, "within the arc, and most of it");
    }

    /// <summary>The same orders give the same world, beams and all.</summary>
    [Fact]
    public void The_same_orders_give_the_same_world()
    {
        ulong Run()
        {
            var w = Night(out int lion, out int dd);
            w.AdvanceTo(w.Time + 20_000);
            w.Apply(SearchlightSystem.Order(lion, SearchlightOrder.Illuminate, dd));
            w.Apply(SearchlightSystem.Order(dd, SearchlightOrder.Search, centreDeg: -90, halfDeg: 45));
            w.AdvanceTo(w.Time + 60_000);
            return w.ComputeHash();
        }
        Assert.Equal(Run(), Run());
    }
}
