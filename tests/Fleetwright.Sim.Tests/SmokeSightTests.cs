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
    static readonly SightLine Line = new(10_000, 90, 25_000, 30, false);

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
    public void An_eye_reads_its_row_or_between_two()
    {
        var eyes = Target.Eyes;
        int rows = Math.Min(eyes.Length, SmokeModel.PairEyes);
        var pair = new double[SmokeModel.PairEyes * SmokeModel.PairBands];
        for (int r = 0; r < rows; r++)
            for (int j = 0; j < SmokeModel.PairBands; j++)
                pair[r * SmokeModel.PairBands + j] = 10 * r + j;
        for (int r = 0; r < rows; r++)
        {
            var at = SmokeModel.EyeTau(pair, eyes, eyes[SmokeModel.PairEye(r, eyes.Length)].HeightM);
            Assert.Equal(new SmokeTau(10 * r, 10 * r + 1, 10 * r + 2), at);
        }
        // the highest and the lowest rows are her highest and lowest eyes; a height between two rows is between them
        Assert.Equal(0, SmokeModel.PairEye(0, eyes.Length));
        Assert.Equal(eyes.Length - 1, SmokeModel.PairEye(rows - 1, eyes.Length));
        double hi = eyes[SmokeModel.PairEye(0, eyes.Length)].HeightM, lo = eyes[SmokeModel.PairEye(1, eyes.Length)].HeightM;
        var mid = SmokeModel.EyeTau(pair, eyes, (hi + lo) / 2);
        output.WriteLine($"{eyes.Length} eyes, {hi:F1} m and {lo:F1} m the top rows; between them {mid}");
        Assert.Equal(5, mid.Low, 1e-9);
        Assert.Equal(new SmokeTau(0, 1, 2), SmokeModel.EyeTau(pair, eyes, hi + 10));
        Assert.Equal(SmokeModel.EyeTau(pair, eyes, eyes[^1].HeightM), SmokeModel.EyeTau(pair, eyes, 0));
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

    [Fact]
    public void A_glow_is_dimmed_by_the_smoke_on_its_line()
    {
        double clear = SightModel.GlowMargin(Eye, 20, 15, 8_000, 25_000, -12, Settings);
        double smoked = SightModel.GlowMargin(Eye, 20, 15, 8_000, 25_000, -12, Settings, 2);
        Assert.True(clear > 0);
        Assert.Equal(clear * Math.Exp(-2), smoked, 1e-12);
        // the depth at its height, between the bands
        var tau = new SmokeTau(1, 3, 5);
        double top = Target.Silhouette.TopM, b = Settings.SmokeBands[0];
        Assert.Equal(1, SightModel.SmokeAt(tau, 0, top, Settings));
        Assert.Equal(5, SightModel.SmokeAt(tau, top, top, Settings));
        Assert.Equal(2, SightModel.SmokeAt(tau, top * (b + Settings.SmokeBands[1]) / 2, top, Settings), 1e-9);
    }
}
