using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Sight through the smoke (docs/battle/smoke.md Z3): no smoke is the margin as it was; smoke on the hull leaves
/// her tops, smoke lying high leaves her hull, smoke all over takes it all; an eye's depths read from a pair's rows; a
/// fire's glow dimmed as the haze dims it; the looks and fire control in a world through the smoke.</summary>
public class SmokeSightTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly ShipClass Target = Fleet.Get("dreadnought").Class!;
    const double Eye = 30, Mag = 7;
    static readonly SightLine Line = new(10_000, 90, 25_000, NightLight.ClearSkyCdM2(30, Settings), 0);

    static double Margin(SmokeTau tau) => SightModel.Margin(Eye, Mag, Target, Line with { Smoke = tau }, Settings);

    [Fact]
    public void No_smoke_is_the_margin_as_it_was()
    {
        double clear = Margin(default);
        double area = SightModel.VisibleAreaM2(Target, SightModel.HiddenM(Eye, Line.DistanceM, Settings), Line.AspectDeg);
        Assert.Equal(SightModel.MarginOfArea(area, Line.DistanceM, Mag, Line, Settings), clear);
        // a trace of smoke goes through the slabs: the whole ship at T = 1 is the clear margin
        Assert.Equal(clear, Margin(new SmokeTau(1e-15, 1e-15, 1e-15)), 1e-9);
    }

    [Fact]
    public void Smoke_on_her_hull_leaves_her_tops()
    {
        double clear = Margin(default), hull = Margin(new SmokeTau(10, 0, 0)), all = Margin(new SmokeTau(10, 10, 10));
        output.WriteLine($"margin clear {clear:F2}, her hull in smoke {hull:F2}, all over {all:F5}");
        Assert.InRange(hull, all * 100, clear);   // a smaller, harder target: her upper works and tops
        Assert.Equal(clear * Math.Exp(-10), all, 1e-9);
    }

    [Fact]
    public void Smoke_lying_high_leaves_her_hull()
    {
        double clear = Margin(default), high = Margin(new SmokeTau(0, 0, 10)), all = Margin(new SmokeTau(10, 10, 10));
        output.WriteLine($"margin clear {clear:F2}, smoke over her tops {high:F2}");
        Assert.InRange(high, all * 100, clear);
        // the worse the smoke on any part, never the better she shows
        Assert.True(Margin(new SmokeTau(1, 1, 1)) <= Margin(new SmokeTau(1, 0.5, 0.2)));
        Assert.True(Margin(new SmokeTau(1, 0.5, 0.2)) <= clear);
    }

    [Fact]
    public void An_eye_reads_the_row_nearest_it()
    {
        var eyes = Target.Eyes;
        int rows = Math.Min(eyes.Length, SmokeModel.PairEyes);
        var pair = new double[SmokeModel.PairEyes * SmokeModel.PairRow];
        for (int r = 0; r < rows; r++)
            for (int k = 0; k < SmokeModel.PairRow; k++)
                pair[r * SmokeModel.PairRow + k] = 100 * r + k;
        for (int r = 0; r < rows; r++)
        {
            var eye = eyes[SmokeModel.PairEye(r, eyes.Length)];
            var at = SmokeModel.EyeTau(pair, eyes, eye.X, eye.Y, eye.HeightM);
            Assert.Equal(100 * r, at.F0);   // a row's eye reads its own row: fore point low ...
            Assert.Equal(100 * r + 8, at.A2);   // ... to aft point high
        }
        // every eye reads the row of the row's eye nearest it
        var all = eyes.ToArray();
        for (int e = 0; e < all.Length; e++)
        {
            var me = all[e];
            int nearest = Enumerable.Range(0, rows).MinBy(r =>
            {
                var q = all[SmokeModel.PairEye(r, all.Length)];
                return Math.Pow(q.X - me.X, 2) + Math.Pow(q.Y - me.Y, 2) + Math.Pow(q.HeightM - me.HeightM, 2);
            });
            Assert.Equal(100 * nearest + 4, SmokeModel.EyeTau(pair, eyes, me.X, me.Y, me.HeightM).Mid);
        }
        output.WriteLine($"{all.Length} eyes, {rows} rows: " + string.Join(", ", all.Select(e => $"{e.Kind} at {e.X:F0} m, {e.HeightM:F0} m up")));
    }

    static readonly WorldTime Start = WorldTime.FromUtc(new DateTime(1916, 5, 31, 14, 0, 0, DateTimeKind.Utc));

    /// <summary>A dreadnought chasing another 10 km ahead, both north at 18 kn, the wind from astern, her main battery
    /// engaging: the chase's trail and her own lie along the line between them. The seconds the chase was in her navy's
    /// sight, the salvos she fired, and the depth on the line from her highest eye to the chase's middle at the end.</summary>
    static (World W, int Seen, int Salvos, double Tau) Chase(bool smoke, double minutes)
    {
        var w = new World(Start, 9, Settings with { Smoke = smoke, RailsEnabled = false });
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(Command.SetWeather(25_000, 180, 5));
        w.Apply(ShipSystem.Spawn(c, 56.5, 4.0, 0, 18, 0));
        w.Apply(ShipSystem.Spawn(c, 56.59, 4.0, 0, 18, 1));
        w.Apply(GunnerySystem.Engage(1, 0, 2));
        int seen = 0;
        for (int s = 0; s < minutes * 60; s++)
        {
            w.AdvanceTo(w.Time + 1000);
            var pic = w.Sightings.Of(0);
            seen += pic != null && pic.IndexOf(2) is int i and >= 0 && pic[i].InSight ? 1 : 0;
        }
        Span<double> tau = stackalloc double[SmokeSystem.PairTaus];
        if (smoke)
            w.Smoke.TauUncached(w, w.Ships.Store.SlotOf(1), w.Ships.Store.SlotOf(2), tau);
        return (w, seen, w.Gunnery.TallyOf(1, 0)?.Salvos ?? 0, tau[1]);
    }

    [Fact]
    public void A_chase_down_the_wind_is_lost_in_its_smoke()
    {
        // the stern chase in the record (the Falklands: "own funnel smoke across the range"), at its worst: no tuning
        // asked of it yet (decision 5), only that the smoke blinds the looks and the guns that read them
        var (_, clear, fired, _) = Chase(false, 10);
        var (w, smoked, firedSmoked, tau) = Chase(true, 10);
        output.WriteLine($"in sight {clear} s of 600 without the smoke, {smoked} s through it; {fired} salvos against {firedSmoked}; τ highest eye to her middle {tau:F2}; lines asked {w.Smoke.Asked}, worked out {w.Smoke.Worked}");
        Assert.True(clear > 500, "made out in clear air");
        Assert.True(tau > 1, "smoke on the line");
        Assert.True(smoked < clear / 2, "the smoke hides her");
        Assert.True(firedSmoked < fired, "and the guns fire less");
        Assert.True(w.Smoke.Worked * 5 < w.Smoke.Asked, "the looks and fire control share the pair's answer");
    }

    /// <summary>A battlecruiser steaming at a stopped dreadnought from 30 km on a 40 km day, into a wind of 8 m/s: her smoke
    /// streams astern of her, so her bow and upper works forward stay clear (docs/battle/bugs.md, 2026-10-22: the lines
    /// ended at her middle, in her funnels' smoke). The seconds she's in the dreadnought's navy's sight over ten
    /// minutes.</summary>
    static int BowOn(bool smoke)
    {
        var w = new World(Start, 3, Settings with { Smoke = smoke, RailsEnabled = false });
        int o = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        int t = w.Ships.AddClass(Fleet.Get("lion").Class!);
        w.Apply(Command.SetWeather(40_000, 270, 8));
        w.Apply(ShipSystem.Spawn(o, 56.5, 4.0, 0, 0, 0));
        w.Apply(ShipSystem.Spawn(t, 56.5, 4.0 + 30 / (111.32 * Math.Cos(56.5 * Math.PI / 180)), 270, 28, 1));
        w.AdvanceTo(w.Time + 5 * 60_000);   // her trail laid, made out
        int seen = 0;
        for (int s = 0; s < 600; s++)
        {
            w.AdvanceTo(w.Time + 1000);
            var pic = w.Sightings.Of(0);
            seen += pic != null && pic.IndexOf(2) is int i and >= 0 && pic[i].InSight ? 1 : 0;
        }
        return seen;
    }

    [Fact]
    public void A_ship_bow_on_is_not_hidden_by_her_own_smoke_astern()
    {
        int clear = BowOn(false), smoked = BowOn(true);
        output.WriteLine($"in sight {clear} s of 600 without the smoke, {smoked} s with it");
        Assert.True(clear > 550);
        Assert.True(smoked > 0.9 * clear);
    }

    /// <summary>A dreadnought at 18 kn into a wind from ahead, a target stopped 12 km ahead: her smoke streams aft over her
    /// stern, so her foremost eye looks ahead clear of it while her aftmost looks out through it.</summary>
    [Fact]
    public void A_foretop_looks_ahead_clear_of_her_own_smoke_aft()
    {
        var w = new World(Start, 3, Settings with { Smoke = true, RailsEnabled = false });
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(Command.SetWeather(25_000, 0, 6));
        w.Apply(ShipSystem.Spawn(c, 56.5, 4.0, 0, 18, 0));
        w.Apply(ShipSystem.Spawn(c, 56.61, 4.0, 0, 0, 1));
        w.AdvanceTo(w.Time + 5 * 60_000);
        var store = w.Ships.Store;
        // her puffs as they are now (the index's are up to SmokeIndexMs old: at 18 kn she's run 140 m on from them)
        var puffs = new List<LinePuff>();
        for (int i = 0; i < w.Smoke.Slots; i++)
            if (w.Smoke.IsLive(i))
            {
                var st = SmokeModel.State(w.Smoke.Puff(i), w.Time.Ms / 1000.0, w.Smoke.Frame, w.Smoke.Air);
                puffs.Add(new LinePuff(st, w.Smoke.Frame.ToAir(st.Centre.Normalized() * Earth.Radius), w.Smoke.Frame.ToAir(st.Axis)));
            }
        Span<double> tau = stackalloc double[SmokeSystem.PairTaus];
        w.Smoke.TauOf(w, store.SlotOf(1), store.SlotOf(2), puffs.ToArray(), Settings.SmokeLineSplit, tau);
        var eyes = store[store.SlotOf(1)].Handling.Class.Eyes;
        for (int r = 0; r < Math.Min(eyes.Length, SmokeModel.PairEyes); r++)
        {
            var e = eyes[SmokeModel.PairEye(r, eyes.Length)];
            output.WriteLine($"row {r}: {e.Kind} at {e.X:F0} m, {e.HeightM:F0} m up: τ to fore/mid/aft middles {tau[r * 9 + 1]:F3} {tau[r * 9 + 4]:F3} {tau[r * 9 + 7]:F3}");
        }
        var fore = eyes.ToArray().MaxBy(e => e.X);
        var aft = eyes.ToArray().MinBy(e => e.X);
        double foreTau = SmokeModel.EyeTau(tau, eyes, fore.X, fore.Y, fore.HeightM).Mid, aftTau = SmokeModel.EyeTau(tau, eyes, aft.X, aft.Y, aft.HeightM).Mid;
        output.WriteLine($"foremost eye ({fore.Kind} at {fore.X:F0} m, {fore.HeightM:F0} m up): τ {foreTau:F3}; aftmost ({aft.Kind} at {aft.X:F0} m, {aft.HeightM:F0} m up): τ {aftTau:F3}");
        Assert.True(foreTau < 0.05, "ahead of her funnels, clear");
        Assert.True(aftTau > 10 * Math.Max(foreTau, 0.01), "astern of them, through her smoke");
    }

    /// <summary>An ironclad's black-powder salvo (docs/battle/smoke.md Z4): its cloud kept pace with her by a wind from
    /// astern at her own speed, a target abeam 3 km off: the line goes opaque for a minute after each salvo, then clears
    /// as the cloud spreads and drifts ahead.</summary>
    [Fact]
    public void A_powder_salvo_lays_its_cloud()
    {
        var w = new World(Start, 9, Settings with { Smoke = true, RailsEnabled = false });
        int c = w.Ships.AddClass(Fleet.Get("devastation").Class!);
        w.Apply(Command.SetWeather(25_000, 180, 10 * Handling.MsPerKnot));
        w.Apply(ShipSystem.Spawn(c, 56.5, 4.0, 0, 10, 0));
        w.Apply(ShipSystem.Spawn(c, 56.5, 4.0 + 3_000 / (111_320 * Math.Cos(56.5 * Math.PI / 180)), 0, 10, 1));
        w.Apply(GunnerySystem.Engage(1, 0, 2));
        Span<double> tau = stackalloc double[SmokeSystem.PairTaus];
        double peak = 0;
        bool cleared = false;
        for (int s = 0; s < 600; s += 15)
        {
            w.AdvanceTo(w.Time + 15_000);
            int guns = 0;
            double area = 0;
            for (int i = 0; i < w.Smoke.Slots; i++)
                if (w.Smoke.IsLive(i) && w.Smoke.KindOf(i) == SmokeSystem.SourceKind.Guns)
                    (guns, area) = (guns + 1, area + w.Smoke.State(i, w.Time.Ms / 1000.0).Area);
            w.Smoke.TauUncached(w, w.Ships.Store.SlotOf(1), w.Ships.Store.SlotOf(2), tau);
            output.WriteLine($"{s + 15,4} s: {w.Gunnery.TallyOf(1, 0)?.Salvos ?? 0} salvos, {guns} gun puffs ({area:F0} m²), τ eye 1 {tau[0]:F2} {tau[1]:F2} {tau[2]:F2}");
            if (guns > 0)
            {
                cleared |= peak > SmokeSystem.SmokeOpaqueTau && tau[1] < 1;
                peak = Math.Max(peak, tau[1]);
            }
        }
        Assert.True(peak > SmokeSystem.SmokeOpaqueTau, $"the cloud on the line: τ {peak:F1} at most");
        Assert.True(cleared, "and it clears");
    }

    [Fact]
    public void A_glow_is_dimmed_by_the_smoke_on_its_line()
    {
        double clear = SightModel.GlowMargin(Eye, 20, 15, 8_000, 25_000, NightLight.ClearSkyCdM2(-12, Settings), Settings);
        double smoked = SightModel.GlowMargin(Eye, 20, 15, 8_000, 25_000, NightLight.ClearSkyCdM2(-12, Settings), Settings, 2);
        Assert.True(clear > 0);
        Assert.Equal(clear * Math.Exp(-2), smoked, 1e-12);
        // the depth at its height, between the bands
        var tau = new SmokeTau(1, 3, 5);
        double top = Target.Silhouette.TopM, b = Settings.SmokeBands[0];
        Assert.Equal(1, SightModel.SmokeAt(tau, 0, top, Settings));
        Assert.Equal(5, SightModel.SmokeAt(tau, top, top, Settings));
        Assert.Equal(2, SightModel.SmokeAt(tau, top * (b + Settings.SmokeBands[1]) / 2, top, Settings), 1e-9);
    }

    /// <summary>The coarse tier's young steps (docs/battle/coarse.md J5): a battleship engaging another 7 km abeam in calm
    /// air, both at 25 kn, their own funnels' and guns' smoke on the line between them. Between the coarse ticks her newest
    /// smoke stands on it as it does on the fine tick (the minute's laying alone left the line all but clear: a ship's
    /// last minute of smoke, the densest there is, wasn't laid until she'd run 770 m past it).</summary>
    [Fact]
    public void Between_the_coarse_ticks_her_newest_smoke_is_on_the_lines()
    {
        double Mean(double speed)
        {
            var w = new World(Start, 9, Settings with { Smoke = true });
            int c = w.Ships.AddClass(Fleet.Get("super_dreadnought").Class!);
            w.Apply(Command.SetSpeed(speed));
            w.Apply(Command.SetWeather(8_000));
            w.Apply(ShipSystem.Spawn(c, 56.5, 4.0, 0, 25, 0));
            w.Apply(ShipSystem.Spawn(c, 56.5, 4.0 + 7_000 / (111_320 * Math.Cos(56.5 * Math.PI / 180)), 0, 25, 1));
            w.Apply(GunnerySystem.Engage(1, 0, 2));
            Span<double> tau = stackalloc double[SmokeSystem.PairTaus];
            double sum = 0;
            int n = 0;
            w.AdvanceTo(w.Time + 120_000);
            for (int s = 0; s < 240; s += 5)
            {
                w.AdvanceTo(w.Time + 5_000);
                w.Smoke.TauUncached(w, w.Ships.Store.SlotOf(1), w.Ships.Store.SlotOf(2), tau);
                double most = 0;
                foreach (double x in tau)
                    most = Math.Max(most, x);
                (sum, n) = (sum + Math.Min(most, 3), n + 1);   // (a salvo's cloud right on the line counts as opaque)
            }
            Assert.Equal(speed >= Settings.CoarseMinSpeed ? ClockTier.Coarse : ClockTier.Fine, w.Ships.Store[w.Ships.Store.SlotOf(1)].Tier);
            return sum / n;
        }
        double fine = Mean(1), coarse = Mean(1000);
        output.WriteLine($"the most smoke on a line between them, mean over 4 minutes: fine τ {fine:F2}, coarse {coarse:F2}");
        Assert.True(fine > 0.1, "her own smoke on the line in fine");
        Assert.InRange(coarse, 0.6 * fine, 1.6 * fine);
    }
}
