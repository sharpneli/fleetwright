using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Fire control (docs/battle/fighting.md 4.2b): the chain against research/simulation/fire_control_ref.py's
/// <c>engage()</c> in the same clear weather (the reference run on these cases: 8 × 15"/42 at level 3 on a QE-size target
/// at 16 kyd 5.0%, levels 0 to 3 in order; a Lion-like 13.5" on a battlecruiser at 15 km 4.7%, a Seydlitz-like 11" 5.5%),
/// mounts that train onto the target and fire only when they bear, an engaged ship kept off rails and silent in the
/// coarse tier, replays, and no allocation.</summary>
public class GunneryTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { HitsDoDamage = false };   // fire control alone
    const double Lat = 56.0, Lon = 4.0, Kyd = 914.4;

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    /// <summary>Experience and work-up (docs/battle/crew.md decision 6): the crew rows are anchors at the whole numbers,
    /// read exactly there and linearly between, held past the ends; a worked-up crew shoots at its experience exactly,
    /// one new to the ship at 0.3 of it.</summary>
    [Fact]
    public void The_gunnery_level_reads_the_crew_rows_between_the_whole_numbers()
    {
        var s = SimSettings.Default;
        for (int k = 0; k < s.GunneryCrews.Length; k++)
        {
            Assert.Equal(k, s.GunneryLevel(k, 1));
            Assert.Equal(s.GunneryCrews[k], s.GunneryCrewAt(k));
        }
        var mid = s.GunneryCrewAt(1.5);
        Assert.Equal((s.GunneryCrews[1].Operator + s.GunneryCrews[2].Operator) / 2, mid.Operator, 12);
        Assert.Equal((s.GunneryCrews[1].Pyramid + s.GunneryCrews[2].Pyramid) / 2, mid.Pyramid, 12);
        Assert.Equal(s.GunneryCrews[^1], s.GunneryCrewAt(3.4));
        Assert.Equal(s.GunneryCrews[0], s.GunneryCrewAt(-1));
        Assert.Equal(0.6, s.GunneryLevel(Experience.Trained, 0), 12);
        Assert.Equal(1.3, s.GunneryLevel(Experience.Trained, 0.5), 12);
        Assert.True(Experience.TryParse("Elite", out double e) && e == 3);
        Assert.True(Experience.TryParse("2.5", out e) && e == 2.5);
        Assert.False(Experience.TryParse("3.5", out _));
        Assert.Equal("trained", Experience.Name(2));
        Assert.Equal("1.25", Experience.Name(1.25));
    }

    /// <summary>A shooter steaming north and a target at a bearing and range from it on a course, both at speed, sides
    /// 0 and 1; the shooter's battery engaged on it.</summary>
    static World Make(ShipClass shooter, ShipClass target, double rangeM, double bearingDeg, double courseDeg, double kn, int seed, int level = -1,
        int battery = 0, SimSettings? settings = null)
    {
        var w = new World(Start, (ulong)seed, settings ?? Settings);
        int a = w.Ships.AddClass(shooter), b = w.Ships.AddClass(target);
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, bearingDeg);
        Earth.Advance(ref p, ref f, rangeM);
        var (tl, to) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, kn, side: 0));
        w.Apply(ShipSystem.Spawn(b, tl, to, courseDeg, kn, side: 1));
        w.Apply(GunnerySystem.Engage(1, battery, 2, level));
        return w;
    }

    /// <summary>Hits and shells over n duels of a quarter of an hour, on threads, in clear air (fire control's own rules;
    /// the smoke's part is SmokeSightTests').</summary>
    static (int Hits, int Shells, int Straddles, int Salvos) Duels(ShipClass shooter, ShipClass target, double rangeM, double bearingDeg, int level,
        int n, int seed0 = 1, double zigDeg = 0, double legS = 90, double minutes = 15, SimSettings? with = null, double courseDeg = 0)
    {
        var tallies = new GunnerySystem.Tally[n];
        Parallel.For(0, n, k =>
        {
            var w = Make(shooter, target, rangeM, bearingDeg, courseDeg, 25, seed0 + k, level, settings: with ?? Settings with { Smoke = false });
            // her own zigzag, if any: legs either side of her course from the first leg's end
            var end = Start + (long)(minutes * 60_000);
            for (int leg = 1; zigDeg > 0 && w.Time.Ms < end.Ms; leg++)
            {
                w.AdvanceTo(new WorldTime(Math.Min(Start.Ms + (long)(leg * legS * 1000), end.Ms)));
                w.Apply(ShipSystem.Course(1, leg % 2 == 1 ? zigDeg : 360 - zigDeg));
            }
            w.AdvanceTo(end);
            tallies[k] = w.Gunnery.TallyOf(1, 0)!.Value;
        });
        return (tallies.Sum(t => t.Hits), tallies.Sum(t => t.Shells), tallies.Sum(t => t.Straddles), tallies.Sum(t => t.Salvos));
    }

    /// <summary>The opening over n duels (fc-ladder-check.md FL2): the first salvo's fall over or short of her, rms (m), and
    /// the share of first salvos that straddled (%), when it was fired on average (s), and the hits in the first minutes (%).</summary>
    static (double FirstFallM, double FirstStraddlePct, double FirstS, double HitPct) Opening(ShipClass shooter, ShipClass target, double rangeM,
        int level, int n, double minutes, SimSettings? with = null)
    {
        var tallies = new GunnerySystem.Tally[n];
        var first = new GunnerySystem.SalvoReport?[n];
        Parallel.For(0, n, k =>
        {
            var w = Make(shooter, target, rangeM, 90, 0, 25, 1 + k, level, settings: with ?? Settings with { Smoke = false });
            w.Gunnery.Reported = r => first[k] ??= r.Shooter == 1 ? r : null;
            w.AdvanceTo(Start + (long)(minutes * 60_000));
            tallies[k] = w.Gunnery.TallyOf(1, 0)!.Value;
        });
        var fs = first.Where(r => r.HasValue).Select(r => r!.Value).ToArray();
        return (Math.Sqrt(fs.Average(r => r.MeanX * r.MeanX)), 100.0 * fs.Count(r => r.Straddle) / fs.Length, fs.Average(r => (r.FiredMs - Start.Ms) / 1000.0),
            100.0 * tallies.Sum(t => t.Hits) / Math.Max(tallies.Sum(t => t.Shells), 1));
    }

    /// <summary>The Type 92's own course is followed by hand from the gyro's pointer (fc-ladder-check.md FL1;
    /// ship-motion/own-motion-inputs.md §2), 0.5 s behind [EST, the user: "I definitely don't have that much latency if I
    /// just follow something"], while the director's train comes in at once: her turns turn its bearings by the lag. In the
    /// levels' zigzag (QE on QE at 16 kyd, 10° either side every 90 s, at most 0.47°/s) half a second is a quarter of a
    /// degree, 60 m along a target 190 m long, and costs nothing; twice the lag costs (readings' rung, her own inputs on, 16
    /// duels: 81.8% with them exact, 82.1 at 0.5 s, 68.8 at 1 s; fc-ladder-check.md FL2 put the own rung back in, "all=0"
    /// had taken it out).</summary>
    [Fact]
    public void The_Type_92_follows_her_course_by_hand_half_a_second_behind()
    {
        var qe = Class("super_dreadnought");
        int level = FireControlPresets.Find("japanese-plot");
        double Zig(string errors)
        {
            var (h, sh, _, _) = Duels(qe, qe, 16 * Kyd, 90, level, 16, zigDeg: 10,
                with: Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse(errors) });
            return 100.0 * h / sh;
        }
        double exact = Zig("all=0,readings=1"), half = Zig("all=0,readings=1,own=1"), one = Zig("all=0,readings=1,own=2");
        double allExact = Zig("own=0"), allHalf = Zig("all=1");
        output.WriteLine($"zigzagging, the readings only: her course exact {exact:F1}%, 0.5 s behind {half:F1}%, 1 s {one:F1}%; every error on: exact {allExact:F1}%, 0.5 s {allHalf:F1}%");
        Assert.InRange(half, 0.97 * exact, 1.03 * exact);
        Assert.True(one < 0.95 * exact, "a hand-followed course late enough turns the bearings off her");
    }

    /// <summary>Where the levels must differ (the user, 2026-11-02: "In ideal circumstances one is not really expecting any
    /// difference, so we should do a test with a situation where difference is expected"), QE on QE at 16 kyd, each on the
    /// ladder's rung that holds the mechanism (balance.md BL1ag): with her own zigzag (10° either side every 90 s, past the
    /// plain clock's 5°) and the readings' errors alone, level 1's hand plot is put out by each turn and ranges again where
    /// level 3's Dreyer, fed her own course and speed, keeps its solution (measured 15 v 81%). With every error on, the
    /// zigzag shows it too since fc-ladder-check.md FL1 took the own-turn kick (one lumped draw a salvo, which took level 3
    /// to 10.6%) for the table's own reckoning (level 1 12.1%, level 3 14.6%). Opening fire (FL2), level 3's 4 m
    /// rangefinder (the FA's 2 m reads her range some 3 times worse at the range) puts its first salvo nearer her: on the
    /// readings' rung 131 v 221 m rms over 32 duels; every error on (the residual bias and the pattern beside it) 219 v 395 m,
    /// 39 v 23% of first salvos straddling. The first straddle's median time hid it (both about 140 s): level 3's first
    /// salvo straddles a little under half the time, so the median fell on the second salvo for both.</summary>
    [Fact]
    public void Where_the_levels_differ_level_3_does_better()
    {
        var qe = Class("super_dreadnought");
        var readings = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1") };
        var zig = new double[4];
        var open = new double[4];
        foreach (int level in new[] { 1, 3 })
        {
            var (h, sh, _, _) = Duels(qe, qe, 16 * Kyd, 90, level, 16, zigDeg: 10, with: readings);
            zig[level] = 100.0 * h / sh;
            var (of, ost, ot, _) = Opening(qe, qe, 16 * Kyd, level, 32, 2, readings);
            open[level] = of;
            var (ah, ash, _, _) = Duels(qe, qe, 16 * Kyd, 90, level, 16, zigDeg: 10);
            var (af, ast, _, ap) = Opening(qe, qe, 16 * Kyd, level, 32, 5);
            output.WriteLine($"level {level}, the readings only: zigzagging {zig[level]:F1}% hits, the first salvo at {ot:F0} s falling {of:F0} m rms " +
                             $"({ost:F0}% straddled); every error on: zigzagging {100.0 * ah / ash:F1}%, the first salvo {af:F0} m rms ({ast:F0}% straddled), " +
                             $"{ap:F1}% in 5 min");
        }
        Assert.True(zig[3] > 2 * zig[1], "her own turns put the plain clock out, not the helm-free table");
        Assert.True(open[3] < 0.75 * open[1], "the longer base puts the first salvo nearer");
    }

    /// <summary>Level 0 to 1 where they must differ (fc-ladder-check.md FL3), Mikasa on Mikasa at 7 km, the plot the same
    /// hand plot: the eye guesses her range to 20% (a man's misjudgement his own all through, so his readings don't average
    /// it out) where the FA reads it to some 100 m, so on the readings' rung the eye's first salvo falls far off and it
    /// ranges for minutes (measured, 48 duels: 788 v 174 m rms, effect at 271 v 131 s); open sights lay to 1.15 mil, the
    /// early telescope to 0.73, some 75 v 50 m in range a gun at the range, so on the laying's rung (with the pattern) the
    /// telescope hits more firing for effect (19.0 v 22.3%). Every error on, measured 9.9 v 16.1% in 10 minutes.</summary>
    [Fact]
    public void Where_the_levels_differ_level_1_ranges_where_the_eye_guesses()
    {
        var mikasa = Class("mikasa");
        var readings = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1") };
        var laying = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,laying=1,dispersion=1") };
        var fall = new double[2];
        var laid = new double[2];
        foreach (int level in new[] { 0, 1 })
        {
            var (f, st, at, _) = Opening(mikasa, mikasa, 7_000, level, 24, 3, readings);
            fall[level] = f;
            laid[level] = Opening(mikasa, mikasa, 7_000, level, 24, 10, laying).HitPct;
            var (af, ast, _, ap) = Opening(mikasa, mikasa, 7_000, level, 24, 10);
            output.WriteLine($"level {level}: the readings only, the first salvo at {at:F0} s falling {f:F0} m rms ({st:F0}% straddled); the laying " +
                             $"and the pattern only, {laid[level]:F1}% hits in 10 min; every error on, the first salvo {af:F0} m rms ({ast:F0}%), {ap:F1}% in 10 min");
        }
        Assert.True(fall[1] < 0.4 * fall[0], "the rangefinder's first range is nearer than the eye's");
        Assert.True(laid[1] > laid[0], "the telescope lays closer than open sights");
    }

    /// <summary>A class refitted: its main battery's fire control the blocks named (the catalogue's, or its design's own).</summary>
    static ShipClass Refit(string name, string optics, string computer, string sights, Func<Shipgen.ComputerTechInput, Shipgen.ComputerTechInput>? change = null)
    {
        var d = Shipgen.Design.Load(Fleet.Get(name).Case.DesignPath);
        if (change != null)
        {
            // the computer's block changed, as the design's own
            var b = Shipgen.FireControlTemplates.Get(computer)!.Value;
            var tech = new Dictionary<string, Shipgen.FcTechInput>(d.FireControlTech ?? []) { [computer + ", changed"] = b with { Computer = change(b.Computer!) } };
            (d, computer) = (d with { FireControlTech = tech }, computer + ", changed");
        }
        d = d with { FireControl = d.FireControl! with { Main = d.FireControl.Main! with { Fit = [optics, computer, sights] } } };
        return ShipClassCompiler.Compile(Shipgen.ShipDesign.Build(d));
    }

    /// <summary>Hits per shell (%) over n duels, the shooter steaming north at 18 kn, the target at a bearing and range from
    /// her on a course at a speed, zigzagging zig° either side of it every legS if asked.</summary>
    static double Engaged(ShipClass shooter, ShipClass target, double bearingDeg, double rangeM, double courseDeg, double kn, double minutes, SimSettings s,
        int n = 16, double zig = 0, double legS = 75)
    {
        var (hits, shells) = EngagedHits(shooter, target, bearingDeg, rangeM, courseDeg, kn, minutes, s, n, zig, legS);
        return 100.0 * hits / Math.Max(shells, 1);
    }

    /// <summary><see cref="Engaged"/>'s hits and shells, in a swell from abeam of her (her own course north) if asked
    /// (Hs, m; a 10 s period).</summary>
    static (int Hits, int Shells) EngagedHits(ShipClass shooter, ShipClass target, double bearingDeg, double rangeM, double courseDeg, double kn, double minutes,
        SimSettings s, int n = 16, double zig = 0, double legS = 75, double swellHs = 0)
    {
        var tallies = new GunnerySystem.Tally[n];
        Parallel.For(0, n, k =>
        {
            var w = new World(Start, (ulong)(1 + k), s);
            int a = w.Ships.AddClass(shooter), b = w.Ships.AddClass(target);
            if (swellHs > 0)
                w.Apply(Command.SetSea(swellHs, 10, 90));
            var p = Earth.Position(Lat, Lon);
            var f = Earth.Forward(p, bearingDeg);
            Earth.Advance(ref p, ref f, rangeM);
            var (tl, to) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, 18, side: 0));
            w.Apply(ShipSystem.Spawn(b, tl, to, courseDeg, kn, side: 1));
            w.AdvanceTo(Start + 1);
            w.Apply(GunnerySystem.Engage(1, 0, 2));
            var end = Start + (long)(minutes * 60_000);
            for (int leg = 1; zig > 0 && w.Time.Ms < end.Ms; leg++)
            {
                w.AdvanceTo(new WorldTime(Math.Min(Start.Ms + (long)(leg * legS * 1000), end.Ms)));
                w.Apply(ShipSystem.Course(2, courseDeg + (leg % 2 == 1 ? zig : -zig)));
            }
            w.AdvanceTo(end);
            tallies[k] = w.Gunnery.TallyOf(1, 0)!.Value;
        });
        return (tallies.Sum(t => t.Hits), tallies.Sum(t => t.Shells));
    }

    /// <summary>Level 1 to 2 where they must differ (fc-ladder-check.md FL4), Dreadnought on Dreadnought at 11 km, each part
    /// behind the same others. The computer: a hand plot's rates are its trend alone and the range it passes stays on the
    /// sights till the next plot, where the Dumaresq makes the rate from her course and speed at once and the clock runs
    /// the range on; on a target closing across (her course 225°, the shooter's 0°) the plot ranges longer and hits less
    /// (readings' rung, measured 32 duels: 59.2 v 69.0%), on a parallel one alike (70.8 v 70.0: its steady range is right
    /// there). Here both at 25 kn, 24 duels: 50.2 v 55.9% closing across, 69.3 v 68.2 parallel, every error on 10.5 v 12.5.
    /// The rangefinder, FA to FQ/FT: the first salvo nearer (the readings alone, 15 and 18 kn: 151 v 103 m rms). The sights,
    /// the early telescope's 0.73 mil to 0.35: some 56 against 27 m in range a gun at the range beside a pattern of some
    /// 200 m, so a little (14.0 v 15.5% here).</summary>
    [Fact]
    public void Where_the_levels_differ_level_2_runs_the_range_on()
    {
        var target = Class("dreadnought");
        var readings = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1,prior=1") };
        var ranges = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1") };
        var laying = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,laying=1,dispersion=1") };
        const string fq = "Barr & Stroud FQ/FT (O2)", plot = "Hand plot", clock = "Dumaresq and Vickers clock";
        double Pct(ShipClass c, double course, SimSettings? s = null)
        {
            var (h, sh, _, _) = Duels(c, target, 11_000, 90, -1, 24, minutes: 10, with: s, courseDeg: course);
            return 100.0 * h / sh;
        }
        var byPlot = Refit("dreadnought", fq, plot, "Telescopic sights");
        var byClock = Refit("dreadnought", fq, clock, "Telescopic sights");
        double plotX = Pct(byPlot, 225, readings), clockX = Pct(byClock, 225, readings), plotP = Pct(byPlot, 0, readings), clockP = Pct(byClock, 0, readings);
        output.WriteLine($"the readings only, closing across: the hand plot {plotX:F1}%, the Dumaresq and clock {clockX:F1}%; parallel: {plotP:F1}, {clockP:F1}%; " +
                         $"every error on, closing across: {Pct(byPlot, 225):F1}, {Pct(byClock, 225):F1}%");
        var fa = Opening(Refit("dreadnought", "Barr & Stroud FA (O1)", clock, "Early telescopic sights"), target, 11_000, -1, 24, 3, ranges).FirstFallM;
        var fqf = Opening(Refit("dreadnought", fq, clock, "Early telescopic sights"), target, 11_000, -1, 24, 3, ranges).FirstFallM;
        double early = Pct(Refit("dreadnought", fq, clock, "Early telescopic sights"), 0, laying), tele = Pct(byClock, 0, laying);
        output.WriteLine($"the first salvo, the readings only: the FA {fa:F0} m rms, the FQ/FT {fqf:F0} m; the laying and the pattern only: early telescope " +
                         $"{early:F1}%, telescope {tele:F1}%");
        Assert.True(clockX > 1.06 * plotX, "the clock runs the range on where the plot's held range goes stale");
        Assert.InRange(plotP, 0.94 * clockP, 1.08 * clockP);
        Assert.True(fqf < 0.8 * fa, "the longer base's first range is nearer");
        Assert.True(tele > 0.98 * early, "the telescope lays no worse");
    }

    /// <summary>Level 2 to 3 where they must differ (fc-ladder-check.md FL5). The computer, QE on QE at 16 kyd behind the
    /// same FQ/FT and director, her own zigzag (10° either side every 90 s): the Dumaresq has her course set on it by hand
    /// and is put out by each turn, the Dreyer from the Mk I* takes it from the gyro and keeps its solution. The guns, the
    /// Dreyer behind both, at 9,000 yd in clear air: each layer's own telescope against the director, and the record has
    /// the layers as good or better in good visibility (the 1912 trial repeated: "Orion apparently beat Thunderer"); the
    /// director's lead was in smoke and a heavy sea (67 v 15%), which needs the smoke over her guns and the spray on their
    /// sights the chain doesn't have yet (its layers do lose her in the smoke there is: FL5).</summary>
    [Fact]
    public void Where_the_levels_differ_level_3_keeps_its_solution_through_her_turns()
    {
        var qe = Class("super_dreadnought");
        var readings = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1") };
        var guns = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1,laying=1,dispersion=1") };
        const string fq = "Barr & Stroud FQ/FT (O2)", dreyer = "Dreyer table Mk III-V", director = "Director, follow the pointer";
        double Zig(string computer, SimSettings? s)
        {
            var (h, sh, _, _) = Duels(Refit("super_dreadnought", fq, computer, director), qe, 16 * Kyd, 90, -1, 16, zigDeg: 10, with: s);
            return 100.0 * h / sh;
        }
        double Clear(string sights, SimSettings? s)
        {
            var (h, sh, _, _) = Duels(Refit("super_dreadnought", fq, dreyer, sights), qe, 9_000 * 0.9144, 90, -1, 16, minutes: 10, with: s);
            return 100.0 * h / sh;
        }
        double dumaresq = Zig("Dumaresq and Vickers clock", readings), table = Zig(dreyer, readings);
        double layers = Clear("Telescopic sights", guns), aloft = Clear(director, guns);
        output.WriteLine($"zigzagging, the readings only: the Dumaresq {dumaresq:F1}%, the Dreyer {table:F1}%; every error on: " +
                         $"{Zig("Dumaresq and Vickers clock", null):F1}, {Zig(dreyer, null):F1}%");
        output.WriteLine($"9,000 yd in clear air, the readings, the laying and the pattern: the layers {layers:F1}%, the director {aloft:F1}%; every error on: " +
                         $"{Clear("Telescopic sights", null):F1}, {Clear(director, null):F1}%");
        Assert.True(table > 2 * dumaresq, "her own turns put the hand-set Dumaresq out, not the gyro-fed table");
        Assert.True(layers > 0.95 * aloft, "in clear air the layers do as well as the director");
    }

    /// <summary>Level 3 to 4 where they must differ (fc-ladder-check.md FL6), QE behind the same FQ/FT and director. A fast
    /// target crossing close: a destroyer at 30 kn passing at 4 km, past the Dreyer's ±15°/min bearing gear and through the
    /// range rate its constant-rate clock can't follow, where the AFCT tracks her true course (measured, 24 duels: the Dreyer
    /// 38.9%, the AFCT 73.5; the Dreyer with the AFCT's latency and residuals, the tracker alone, 60.4). Her zigzag (20°
    /// either side every 75 s at 14 km): both straight-course machines, the AFCT ahead by its 8 s latency against the
    /// Dreyer's 15 (17.6 v 20.6%; with the latency and residuals the Dreyer 20.6). Steady at 14 km the two filters are one
    /// (the Dreyer with the AFCT's latency and residuals 74.8, the AFCT 74.7); the officer's step and doubt from his machine's
    /// residual bias (1.2 against 0.6%) and the latency make the rest (67.0). The long-base stereo over the FQ/FT and the
    /// power drive over follow the pointer, a little each on their rungs (printed).</summary>
    [Fact]
    public void Where_the_levels_differ_level_4_follows_a_fast_crossing()
    {
        var qe = Class("super_dreadnought");
        var dd = Class("destroyer");
        var readings = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1,prior=1") };
        var ranges = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1") };
        var laying = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,laying=1,dispersion=1") };
        const string fq = "Barr & Stroud FQ/FT (O2)", o3 = "Stereo, long base (O3)", dreyer = "Dreyer table Mk III-V", afct = "Admiralty Fire Control Table",
            pointer = "Director, follow the pointer", power = "Director, power drive";
        var byDreyer = Refit("super_dreadnought", fq, dreyer, pointer);
        var byLike = Refit("super_dreadnought", fq, dreyer, pointer, c => c with { ResidPct = 0.6, DeflBiasMil = 1.5, LatencyS = 8 });
        var byAfct = Refit("super_dreadnought", fq, afct, pointer);
        double passD = Engaged(byDreyer, dd, 30, 8_000, 180, 30, 6, readings), passL = Engaged(byLike, dd, 30, 8_000, 180, 30, 6, readings),
            passA = Engaged(byAfct, dd, 30, 8_000, 180, 30, 6, readings);
        double zigD = Engaged(byDreyer, qe, 90, 14_000, 0, 18, 10, readings, zig: 20), zigA = Engaged(byAfct, qe, 90, 14_000, 0, 18, 10, readings, zig: 20);
        double steadyL = Engaged(byLike, qe, 90, 14_000, 0, 18, 10, readings), steadyA = Engaged(byAfct, qe, 90, 14_000, 0, 18, 10, readings);
        var all = Settings with { Smoke = false };
        output.WriteLine($"the readings and the priors: a destroyer passing at 4 km at 30 kn, the Dreyer {passD:F1}%, the Dreyer with the AFCT's latency and " +
                         $"residuals {passL:F1}, the AFCT {passA:F1}; her zigzag {zigD:F1}, {zigA:F1}; steady, the like Dreyer {steadyL:F1}, the AFCT {steadyA:F1}; " +
                         $"every error on, the destroyer passing {Engaged(byDreyer, dd, 30, 8_000, 180, 30, 6, all):F1}, {Engaged(byAfct, dd, 30, 8_000, 180, 30, 6, all):F1}%");
        var stereoPointer = Refit("super_dreadnought", o3, afct, pointer);
        double rf = Engaged(byAfct, qe, 90, 14_630, 0, 18, 10, ranges), stereo = Engaged(stereoPointer, qe, 90, 14_630, 0, 18, 10, ranges);
        double lp = Engaged(stereoPointer, qe, 90, 14_630, 0, 18, 10, laying), lw = Engaged(Refit("super_dreadnought", o3, afct, power), qe, 90, 14_630, 0, 18, 10, laying);
        output.WriteLine($"16 kyd steady: the readings only, the FQ/FT {rf:F1}%, the long-base stereo {stereo:F1}; the laying and the pattern, follow the pointer " +
                         $"{lp:F1}, the power drive {lw:F1}");
        Assert.True(passA > 1.5 * passD, "the true-course table follows a fast crossing the rate clock can't");
        Assert.True(passA > 1.1 * passL, "the tracker's own part of it");
        Assert.True(zigA > zigD, "the shorter latency re-learns her turns sooner");
        Assert.InRange(steadyL, 0.96 * steadyA, 1.04 * steadyA);
        Assert.True(stereo > 0.98 * rf && lw > 0.98 * lp, "the longer base and the power drive no worse");
    }

    /// <summary>Level 4 to 5 where they must differ (fc-ladder-check.md FL7), QE refitted. The mount, behind the long-base
    /// stereo and the AFCT at 16 km: the director's layer fires on the roll at the key, the stable vertical anywhere in it
    /// (ship-motion-gunnery-research.md: "continuous fire at any point of roll, tilt-corrected"), so in a beam swell the
    /// director waits for her roll and lays worse as it grows, the vertical neither (measured, 24 duels, hits a run: calm
    /// 281 v 318, a 2 m swell 271 v 324, 4 m 250 v 317; the director's 15.0% a shell falling to 13.4, the vertical's 14.8
    /// holding). The computer, the Mk 8 against the AFCT behind the same: steady alike (76.4 v 76.0%), ahead on a fast
    /// crossing by its 3 s latency and readier manoeuvre allowance (a destroyer at 30 kn passing at 4 km 92.2 v 83.5). The
    /// coated, stabilised O4 over the O3 at 20 kyd, a little (48 duels: 61.0 v 58.8% on the readings' rung; 6 m at ×25 against
    /// 5 m at ×28, 6 readings a minute against 4). And the vertical's own error in her long turns: its erection cut out by
    /// the turn's acceleration, it drifts at some 2.5′ a minute (QE's 450° at 18 kn with 20° of helm, some 7 minutes, leaves
    /// 17′; the research's 4-5 minute turn 12-15′, ~200 yd at 20 kyd: North Carolina 1945's "several hundreds of yards",
    /// aiming at the sun and moon), walked out by the spots as it grows ("no more than would be desirable when using a
    /// rocking ladder": the falls after it 44 m rms against 45 with it exact).</summary>
    [Fact]
    public void Where_the_levels_differ_level_5_fires_anywhere_in_the_roll()
    {
        var qe = Class("super_dreadnought");
        var dd = Class("destroyer");
        var guns = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1,laying=1,dispersion=1") };
        var readings = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1,prior=1") };
        var ranges = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1") };
        const string o3 = "Stereo, long base (O3)", o4 = "Coated, stabilised (O4)", afct = "Admiralty Fire Control Table", mk8 = "Ford Mk 8 rangekeeper",
            power = "Director, power drive", vertical = "Stable vertical, remote power control";
        var byPower = Refit("super_dreadnought", o3, afct, power);
        var byVertical = Refit("super_dreadnought", o3, afct, vertical);
        var (pc, pcs) = EngagedHits(byPower, qe, 90, 16_000, 0, 18, 10, guns, 24);
        var (vc, vcs) = EngagedHits(byVertical, qe, 90, 16_000, 0, 18, 10, guns, 24);
        var (ps, pss) = EngagedHits(byPower, qe, 90, 16_000, 0, 18, 10, guns, 24, swellHs: 4);
        var (vs, vss) = EngagedHits(byVertical, qe, 90, 16_000, 0, 18, 10, guns, 24, swellHs: 4);
        output.WriteLine($"the mount at 16 km: calm, the director {pc} hits of {pcs}, the vertical {vc} of {vcs}; a 4 m swell abeam, {ps} of {pss}, {vs} of {vss}");
        var byMk8 = Refit("super_dreadnought", o3, mk8, power);
        double steadyA = Engaged(byPower, qe, 90, 16_000, 0, 18, 10, readings), steadyM = Engaged(byMk8, qe, 90, 16_000, 0, 18, 10, readings);
        double passA = Engaged(byPower, dd, 30, 8_000, 180, 30, 6, readings), passM = Engaged(byMk8, dd, 30, 8_000, 180, 30, 6, readings);
        output.WriteLine($"the computer: steady at 16 km the AFCT {steadyA:F1}%, the Mk 8 {steadyM:F1}; a destroyer passing at 4 km {passA:F1}, {passM:F1}");
        double rf3 = Engaged(Refit("super_dreadnought", o3, mk8, vertical), qe, 90, 18_300, 0, 18, 10, ranges, 48);
        double rf4 = Engaged(Refit("super_dreadnought", o4, mk8, vertical), qe, 90, 18_300, 0, 18, 10, ranges, 48);
        output.WriteLine($"the readings only at 20 kyd: the O3 {rf3:F1}%, the O4 {rf4:F1}");
        // her long turn: 450° from her course, the vertical's tilt at its end and the falls after it, the own rung on and off
        var level5 = Refit("super_dreadnought", o4, mk8, vertical);
        (double Tilt, double Minutes, double Fall) Turn(SimSettings s)
        {
            const int n = 12;
            var tilt = new double[n];
            var minutes = new double[n];
            var falls = new List<double>[n];
            Parallel.For(0, n, k =>
            {
                var w = Make(level5, qe, 20_000, 90, 0, 18, 1 + k, settings: s);
                var l = falls[k] = [];
                w.Gunnery.Reported = r => { if (r.Shooter == 1 && r.Battery == 0 && r.FiredMs > Start.Ms + 240_000) l.Add(r.MeanX); };
                w.AdvanceTo(Start + 240_000L);
                // one turn, the helm held over till she's come 450° round, then steadied
                w.Apply(ShipSystem.Rudder(1, 20));
                long from = w.Time.Ms;
                double turned = 0, was = w.Ships.Store[w.Ships.Store.SlotOf(1)].HeadingDeg;
                while (turned < 450)
                {
                    w.AdvanceTo(w.Time + 1000);
                    double h = w.Ships.Store[w.Ships.Store.SlotOf(1)].HeadingDeg;
                    turned += Math.Abs(Math.IEEERemainder(h - was, 360));
                    was = h;
                }
                w.Apply(ShipSystem.Course(1, was));
                (tilt[k], minutes[k]) = (w.Gunnery.TallyOf(1, 0)!.Value.VerticalTiltRad, (w.Time.Ms - from) / 60_000.0);
                w.AdvanceTo(Start + 720_000L);
            });
            var all = falls.SelectMany(l => l).ToArray();
            return (tilt.Average() * 10_800 / Math.PI, minutes.Average(), Math.Sqrt(all.Average(x => x * x)));
        }
        var drift = Turn(Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,own=1") });
        var exact = Turn(Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0") });
        output.WriteLine($"a 450° turn at 20 kyd in {drift.Minutes:F1} min: the vertical {drift.Tilt:F1}′ off at its end, the falls after it {drift.Fall:F0} m rms " +
                         $"(exact {exact.Fall:F0})");
        Assert.True(vs > 1.15 * ps, "in a swell the vertical fires anywhere in the roll, the director waits for it");
        Assert.True((double)ps / pss < 0.95 * pc / pcs && (double)vs / vss > 0.95 * vc / vcs, "the director's laying worse as she rolls more, the vertical's not");
        Assert.InRange(steadyM, 0.96 * steadyA, 1.04 * steadyA);
        Assert.True(passM > 1.05 * passA, "the Mk 8 follows a fast crossing more closely");
        Assert.True(rf4 > rf3, "the O4's longer base and readings");
        double free = Settings.StableVerticalDriftArcminMin * drift.Minutes;
        Assert.InRange(drift.Tilt, 0.7 * free, 1.05 * free);
        Assert.True(drift.Fall < 1.2 * exact.Fall + 5, "the spots walk the vertical's drift out as it grows");
    }

    /// <summary>Her salvos after a time: hits per shell (%) and the falls' mean over or short of her, rms (m), over n duels,
    /// the shooter steaming north at 18 kn and the target abeam of her at a range, steaming north at 18 kn; from circleS on
    /// the target circles with her helm held at rudderDeg if asked, or zigzags zig° either side every legS from the start,
    /// or weaves from a minute in, her course ordered weave° times the sine of her time over weaveS every 3 s; the salvos
    /// counted from countS.</summary>
    static (double HitPct, double AlongM) Falls(ShipClass shooter, ShipClass target, double rangeM, SimSettings s, int n, double rudderDeg = 0,
        double circleS = 240, double zig = 0, double countS = 300, double minutes = 10, double legS = 75, double weave = 0, double weaveS = 90)
    {
        var hits = new int[n];
        var shells = new int[n];
        var along = new List<double>[n];
        Parallel.For(0, n, k =>
        {
            var w = new World(Start, (ulong)(1 + k), s);
            int a = w.Ships.AddClass(shooter), b = w.Ships.AddClass(target);
            var p = Earth.Position(Lat, Lon);
            var f = Earth.Forward(p, 90);
            Earth.Advance(ref p, ref f, rangeM);
            var (tl, to) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, 18, side: 0));
            w.Apply(ShipSystem.Spawn(b, tl, to, 0, 18, side: 1));
            w.AdvanceTo(Start + 1);
            w.Apply(GunnerySystem.Engage(1, 0, 2));
            var l = along[k] = [];
            long from = Start.Ms + (long)(countS * 1000);
            w.Gunnery.Reported = r =>
            {
                if (r.Shooter == 1 && r.Battery == 0 && r.FiredMs > from)
                {
                    (hits[k], shells[k]) = (hits[k] + r.Hits, shells[k] + r.Shells);
                    l.Add(r.MeanX);
                }
            };
            for (int leg = 1; zig > 0 && leg * legS < minutes * 60; leg++)
            {
                w.AdvanceTo(Start + (long)(leg * legS * 1000));
                w.Apply(ShipSystem.Course(2, leg % 2 == 1 ? zig : 360 - zig));
            }
            for (double t = 60; weave > 0 && t < minutes * 60; t += 3)
            {
                w.AdvanceTo(Start + (long)(t * 1000));
                w.Apply(ShipSystem.Course(2, (weave * Math.Sin(2 * Math.PI * (t - 60) / weaveS) + 360) % 360));
            }
            if (rudderDeg != 0)
            {
                w.AdvanceTo(Start + (long)(circleS * 1000));
                w.Apply(ShipSystem.Rudder(2, rudderDeg));
            }
            w.AdvanceTo(Start + (long)(minutes * 60_000));
        });
        var all = along.SelectMany(l => l).ToArray();
        return (100.0 * hits.Sum() / Math.Max(shells.Sum(), 1), Math.Sqrt(all.Average(x => x * x)));
    }

    /// <summary>Level 6 to 7's computer where they must differ (fc-ladder-check.md FL9), QE refitted behind the same O4 and
    /// stable vertical: the Mk 8 takes her on a straight line, so on a steady circle it chases a velocity that keeps turning
    /// and never converges ("straight-line machines never converge on a circle", fire-control-research.md §5.6), where the
    /// 1950s curved-course analog carries a decaying acceleration and projects along her curve. Her circle at 16 km, 18 kn
    /// and 15° of helm (some 0.96°/s, her speed falling to 15 kn), the salvos from a minute after the helm went over; the
    /// readings' rung (24 duels, measured): the falls 312 m rms over or short of her against 129; every error on, hits 1.6
    /// against 6.0% (steady 16.9 and 18.3). Steady alike, on the readings' rung 98.5 and 98.9% (the research: a little worse
    /// for the extra states, not seen here); her zigzag (20° either side every 75 s at 14 km) 25.7 against 32.9. The digital
    /// turn filter (FL10's) and both behind the auto-track stand-in, printed.</summary>
    [Fact]
    public void Where_the_levels_differ_level_7_follows_her_circle()
    {
        var qe = Class("super_dreadnought");
        var readings = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1,prior=1") };
        var all = Settings with { Smoke = false };
        const string o4 = "Coated, stabilised (O4)", track = "Auto-track ranging (radar stand-in)", mk8 = "Ford Mk 8 rangekeeper",
            curved = "Curved-course analog computer (1950s)", digital = "Digital turn filter (Mk 86, WM-25)", vertical = "Stable vertical, remote power control";
        var byMk8 = Refit("super_dreadnought", o4, mk8, vertical);
        var byCurved = Refit("super_dreadnought", o4, curved, vertical);
        var (_, circleM) = Falls(byMk8, qe, 16_000, readings, 24, rudderDeg: 15);
        var (_, circleC) = Falls(byCurved, qe, 16_000, readings, 24, rudderDeg: 15);
        var (hitM, _) = Falls(byMk8, qe, 16_000, all, 24, rudderDeg: 15);
        var (hitC, _) = Falls(byCurved, qe, 16_000, all, 24, rudderDeg: 15);
        var (steadyM, _) = Falls(byMk8, qe, 16_000, readings, 24);
        var (steadyC, _) = Falls(byCurved, qe, 16_000, readings, 24);
        var (zigM, _) = Falls(byMk8, qe, 14_000, readings, 24, zig: 20, countS: 0);
        var (zigC, _) = Falls(byCurved, qe, 14_000, readings, 24, zig: 20, countS: 0);
        output.WriteLine($"her circle at 16 km, readings: the falls {circleM:F0} m rms (the Mk 8), {circleC:F0} (the curved-course analog); every error on, " +
                         $"hits {hitM:F1}%, {hitC:F1}; steady, readings {steadyM:F1}, {steadyC:F1}; her zigzag {zigM:F1}, {zigC:F1}");
        var (hitD, _) = Falls(Refit("super_dreadnought", o4, digital, vertical), qe, 16_000, all, 24, rudderDeg: 15);
        var (hitTM, _) = Falls(Refit("super_dreadnought", track, mk8, vertical), qe, 16_000, all, 24, rudderDeg: 15);
        var (hitTC, _) = Falls(Refit("super_dreadnought", track, curved, vertical), qe, 16_000, all, 24, rudderDeg: 15);
        output.WriteLine($"her circle, every error on: the digital turn filter {hitD:F1}%; behind the auto-track stand-in the Mk 8 {hitTM:F1}, the curved-course analog {hitTC:F1}");
        Assert.True(circleC < 0.6 * circleM, "the curved-course analog projects along her curve");
        Assert.True(hitC > 2 * hitM, "and hits her as she circles");
        Assert.InRange(steadyC, 0.96 * steadyM, 1.04 * steadyM);
        Assert.True(zigC > zigM, "her zigzag's turns followed sooner");
    }

    /// <summary>Level 7 to 8's computer where they must differ (fc-ladder-check.md FL10), QE refitted behind the same O4 and
    /// stable vertical at 16 km, both her at 18 kn: the digital filter carries her rate of turn, so on a steady circle it
    /// follows where the curved-course analog's decaying acceleration lags (her circle at 15° of helm, ~0.96°/s, the
    /// readings' rung, 24 duels, measured: the falls 45 against 129 m rms; every error on, hits 10.7 against 6.0%); her
    /// zigzag (30° either side every 2 min) it follows sooner too, a turn learnt and shed, since its turn-rate noise is a
    /// capital ship's helm (19.2 against 14.2%; at the reference's 5e-7 it shed a turn so slowly it fell behind the Mk 8,
    /// 7.3%). A weave fools it ("devastating against a turn-following digital filter", evasion-research.md: it over-
    /// extrapolates every swing; a straight-line machine averages it out): ±30° on a 90 s sine, 17.3 against 30.0%. Steady,
    /// alike (94.0 v 95.8). The Mk 8 on the weave, and both behind the level's digital ranging stand-in, printed.</summary>
    [Fact]
    public void Where_the_levels_differ_level_8_follows_her_turns()
    {
        var qe = Class("super_dreadnought");
        var readings = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1,own=1,prior=1") };
        var all = Settings with { Smoke = false };
        const string o4 = "Coated, stabilised (O4)", ranging = "Digital ranging (radar and laser stand-in)", mk8 = "Ford Mk 8 rangekeeper",
            curved = "Curved-course analog computer (1950s)", digital = "Digital turn filter (Mk 86, WM-25)", vertical = "Stable vertical, remote power control";
        var byCurved = Refit("super_dreadnought", o4, curved, vertical);
        var byDigital = Refit("super_dreadnought", o4, digital, vertical);
        var (_, circleC) = Falls(byCurved, qe, 16_000, readings, 24, rudderDeg: 15);
        var (_, circleD) = Falls(byDigital, qe, 16_000, readings, 24, rudderDeg: 15);
        var (hitC, _) = Falls(byCurved, qe, 16_000, all, 24, rudderDeg: 15);
        var (hitD, _) = Falls(byDigital, qe, 16_000, all, 24, rudderDeg: 15);
        var (zigC, _) = Falls(byCurved, qe, 16_000, readings, 24, zig: 30, legS: 120, countS: 180, minutes: 12);
        var (zigD, _) = Falls(byDigital, qe, 16_000, readings, 24, zig: 30, legS: 120, countS: 180, minutes: 12);
        var (weaveC, _) = Falls(byCurved, qe, 16_000, readings, 24, weave: 30, countS: 180, minutes: 12);
        var (weaveD, _) = Falls(byDigital, qe, 16_000, readings, 24, weave: 30, countS: 180, minutes: 12);
        var (steadyC, _) = Falls(byCurved, qe, 16_000, readings, 24, countS: 180, minutes: 12);
        var (steadyD, _) = Falls(byDigital, qe, 16_000, readings, 24, countS: 180, minutes: 12);
        output.WriteLine($"readings: her circle, the falls {circleC:F0} m rms (the curved-course analog), {circleD:F0} (the digital filter); her zigzag " +
                         $"{zigC:F1}%, {zigD:F1}; her weave {weaveC:F1}, {weaveD:F1}; steady {steadyC:F1}, {steadyD:F1}. Every error on, her circle {hitC:F1}, {hitD:F1}");
        var (weaveM, _) = Falls(Refit("super_dreadnought", o4, mk8, vertical), qe, 16_000, readings, 24, weave: 30, countS: 180, minutes: 12);
        var (hitRC, _) = Falls(Refit("super_dreadnought", ranging, curved, vertical), qe, 16_000, all, 24, rudderDeg: 15);
        var (hitRD, _) = Falls(Refit("super_dreadnought", ranging, digital, vertical), qe, 16_000, all, 24, rudderDeg: 15);
        output.WriteLine($"her weave, readings: the Mk 8 {weaveM:F1}%; her circle every error on, behind the digital ranging stand-in: {hitRC:F1}, {hitRD:F1}");
        Assert.True(circleD < 0.6 * circleC, "the turn filter follows her circle");
        Assert.True(hitD > 1.4 * hitC, "and hits her on it");
        Assert.True(zigD > zigC, "her zigzag's turns learnt and shed sooner");
        Assert.True(weaveD < 0.8 * weaveC, "a weave fools the turn filter");
        Assert.InRange(steadyD, 0.95 * steadyC, 1.04 * steadyC);
    }

    [Fact]
    public void The_levels_keep_their_order_and_level_3_lands_near_the_reference()
    {
        var qe = Class("super_dreadnought");
        var pct = new double[4];
        foreach (int level in new[] { 0, 1, 3 })
        {
            var (hits, shells, straddles, salvos) = Duels(qe, qe, 16 * Kyd, 90, level, 16);
            pct[level] = 100.0 * hits / shells;
            output.WriteLine($"level {level}: {pct[level]:F1}% hits ({hits} of {shells}), {100.0 * straddles / salvos:F0}% of {salvos} salvos straddled");
        }
        // the reference on a QE-size box: 5.0% at level 3; level 0 about a fifth of it. The walk (balance.md BL1v) puts
        // level 3 at 8%: its spots centre the fall where the bracket's lapsed hold went back to the bias, and the hits may
        // rise until the degraders come (the user, 2026-11-02); BL1ac (the salvo error's parts each in its place, no lumped
        // salvo_pct) 18%. Level 1 (local control) and level 3 (a director) hit alike at this range since then (96 duels:
        // 15.1, 15.6%): level 3's lead was mostly its smaller salvo_pct, and 16 duels put them either way (balance.md BL1ad,
        // open); the eyeball's stays far below
        Assert.InRange(pct[3], 2.5, 20);
        Assert.True(pct[0] < pct[1] && pct[0] < pct[3], "the levels' order");
    }

    [Fact]
    public void Close_in_the_hits_climb_steeply()
    {
        // every error grows with the range (rangefinding as its square) and the hittable space with the flatter fall:
        // QE on QE at 2 and 6 km against 16 kyd (measured: level 3 72, 35 and 4.6%; level 0 34, 7 and 0.6%)
        var qe = Class("super_dreadnought");
        foreach (var (level, near) in new[] { (3, 50.0), (0, 20.0) })
        {
            var pct = new[] { 2_000, 6_000, 16 * Kyd }.Select(r =>
            {
                var (h, s, _, _) = Duels(qe, qe, r, 90, level, 8);
                return 100.0 * h / s;
            }).ToArray();
            output.WriteLine($"level {level}: {pct[0]:F1}% at 2 km, {pct[1]:F1}% at 6 km, {pct[2]:F1}% at 16 kyd");
            Assert.True(pct[0] > near && pct[0] > pct[1] && pct[1] > pct[2], $"level {level}");
        }
    }

    [Fact]
    public void Jutland_like_pairs_land_near_the_reference_in_clear_weather()
    {
        var lion = Class("lion");
        var seydlitz = Class("seydlitz");
        var (bh, bs, _, _) = Duels(lion, seydlitz, 15_000, 80, -1, 16);
        var (gh, gs, _, _) = Duels(seydlitz, lion, 15_000, 80, -1, 16);
        double british = 100.0 * bh / bs, german = 100.0 * gh / gs;
        output.WriteLine($"Lion on Seydlitz {british:F1}% (the reference 4.7% in clear weather; the record 1.4% in haze and smoke), " +
                         $"Seydlitz on Lion {german:F1}% (the reference 5.5%; the record 4.0%)");
        // within ×2 of the reference and the record, until BL1ac took the lumped salvo error out (Lion 12.9%, Seydlitz 9.3%):
        // the hits may rise until the degraders come (the user, 2026-11-02)
        Assert.InRange(british, 4.7 / 2, 4.7 * 3);
        Assert.InRange(german, 4.0 / 2, 4.0 * 3);
    }

    /// <summary>A shift of target keeps what the spots learnt of the guns (docs/battle/balance.md BL1ad): Lion fires for
    /// effect on one Seydlitz at 15 km, then shifts to a second as far off on the other bow. The residual ballistic bias is
    /// her guns' and the air's, so her first salvo on the second falls far nearer her than a battery's coming to her fresh,
    /// on the ladder's rung with the bias and the shells' dispersion only (with every error on, the computer's own error on
    /// the new target is about the bias's size: fire for effect 102 s fresh, 99 s shifted carrying nothing, 91 s carrying
    /// it; the track and the bracket are the new target's own).</summary>
    [Fact]
    public void A_shift_of_target_keeps_what_the_spots_learnt_of_the_guns()
    {
        var settings = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,bias=1,dispersion=1") };
        double shifted = FirstFall(settings, true), fresh = FirstFall(settings, false);
        output.WriteLine($"the first salvo's fall off the second target, RMS: {shifted:F0} m after a shift, {fresh:F0} m engaged fresh");
        Assert.True(shifted < 0.5 * fresh, "the bias's correction carried");
    }

    /// <summary>The RMS over seeds of the first salvo's fall off a second Seydlitz (its ladder's step taken off), Lion
    /// engaging her ten minutes in: after firing on a first one, or fresh.</summary>
    static double FirstFall(SimSettings settings, bool shift)
    {
        const int n = 24;
        var fall = new double[n];
        Parallel.For(0, n, k =>
        {
            var w = new World(Start, (ulong)(k + 1), settings);
            int a = w.Ships.AddClass(Class("lion")), t = w.Ships.AddClass(Class("seydlitz"));
            w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, 20, side: 0));
            foreach (double bearing in new[] { 70.0, 110.0 })
            {
                var p = Earth.Position(Lat, Lon);
                var f = Earth.Forward(p, bearing);
                Earth.Advance(ref p, ref f, 15_000);
                var (tl, to) = Earth.LatLon(p);
                w.Apply(ShipSystem.Spawn(t, tl, to, 0, 20, side: 1));
            }
            var salvos = new List<GunnerySystem.SalvoReport>();
            w.Gunnery.Reported = salvos.Add;
            if (shift)
                w.Apply(GunnerySystem.Engage(1, 0, 2));
            w.AdvanceTo(Start + 10 * 60_000);
            Assert.True(!shift || w.Gunnery.TallyOf(1, 0) is { Phase: FirePhase.Effect }, "firing for effect on the first");
            int had = salvos.Count;
            w.Apply(GunnerySystem.Engage(1, 0, 3));
            w.AdvanceTo(w.Time + 5 * 60_000);
            fall[k] = salvos[had].MeanX - salvos[had].LadderM;
        });
        return Math.Sqrt(fall.Average(x => x * x));
    }

    [Fact]
    public void Turrets_train_onto_the_target_and_only_those_that_bear_fire()
    {
        // the target dead ahead: a QE's after turrets can't bear, so salvos are the forward turrets' four guns
        var qe = Class("super_dreadnought");
        var w = Make(qe, qe, 14_000, 0, 0, 10, 3, settings: Settings with { SplashLifeMs = 3_600_000 });   // every mount fired kept
        w.AdvanceTo(Start + 6 * 60_000);
        var ship = w.Ships.Store[w.Ships.Store.SlotOf(1)];
        var mounts = qe.Links(qe.Batteries[0].Mounts).ToArray();
        int forward = 0;
        foreach (int m in mounts)
        {
            ref readonly var row = ref qe.Mounts[m];
            double brg = ship.MountBearingDeg(m);
            output.WriteLine($"mount {qe.ComponentId(row.Component)} at x {row.X:F0}: bearing {brg:F1}° (rest {row.RestDeg:F0}°)");
            if (row.X > 0)
            {
                forward += qe.TurretTypes[row.Type].Barrels;
                Assert.True(Math.Abs(brg - 360 * Math.Round(brg / 360)) < 2, $"a forward turret on the target ahead: {brg:F1}°");
            }
        }
        var t = w.Gunnery.TallyOf(1, 0)!.Value;
        Assert.True(t.Salvos > 0, "it fired");
        // partial salvos ranging and halves firing for effect (fire-control.md F6): never more than the forward guns
        Assert.True(t.Shells <= t.Salvos * forward, $"{t.Shells} shells in {t.Salvos} salvos of at most {forward} guns");
        output.WriteLine($"{t.Salvos} salvos of at most {forward} guns, {t.Shells} shells");

        // the mounts fired, for the screens (docs/visuals/muzzle-flashes.md F0): only the forward turrets, all barrels
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        var s = snap.Shots;
        Assert.True(s.Count > 0, "mounts recorded");
        for (int i = 0; i < s.Count; i++)
        {
            Assert.True(qe.Mounts[s.Mount[i]].X > 0, $"mount {s.Mount[i]} is forward");
            Assert.Equal(qe.TurretTypes[qe.Mounts[s.Mount[i]].Type].Barrels, s.Barrels[i]);
            double brg = Earth.Heading(s.Muzzle[i], s.Forward[i]);
            Assert.True(Math.Min(brg, 360 - brg) < 3, $"fired ahead: {brg:F1}°");
        }
    }

    [Fact]
    public void An_engaged_ship_stays_off_rails_and_its_guns_fire_in_the_coarse_tier()
    {
        var qe = Class("super_dreadnought");
        // a target far beyond any encounter: steady ships go on rails at 1x, but not one with a battery engaged
        var w = Make(qe, qe, 90_000, 90, 0, 15, 5);
        w.AdvanceTo(Start + 10 * 60_000);
        Assert.False(w.Ships.Store[w.Ships.Store.SlotOf(1)].OnRails, "engaged: off rails");
        Assert.True(w.Ships.Store[w.Ships.Store.SlotOf(2)].OnRails, "the target, steaming steady, is on rails");
        Assert.Equal(BatteryState.NoContact, w.Gunnery.TallyOf(1, 0)!.Value.State);
        w.Apply(GunnerySystem.Cease(1));
        w.AdvanceTo(w.Time + 10 * 60_000);
        Assert.True(w.Ships.Store[w.Ships.Store.SlotOf(1)].OnRails, "ceased: on rails");

        // in range, at 1,000x: coarse, and the fine chain fires from and at coarse ships as it does on the fine tick
        // (docs/battle/coarse.md J2): a steady duel, so its salvos and hits come out near the fine tick's, over a few
        // seeds (one duel's own smoke on the lines moves a single seed's salvos by a fifth, J5)
        (int Salvos, int Hits) Duel(double speed)
        {
            var (salvos, hits) = (0, 0);
            for (int seed = 5; seed < 9; seed++)
            {
                var c = Make(qe, qe, 14_000, 90, 0, 15, seed);
                c.Apply(Command.SetSpeed(speed));
                c.AdvanceTo(Start + 15 * 60_000);
                Assert.Equal(speed >= Settings.CoarseMinSpeed ? ClockTier.Coarse : ClockTier.Fine, c.Ships.Store[c.Ships.Store.SlotOf(1)].Tier);
                var tally = c.Gunnery.TallyOf(1, 0)!.Value;
                (salvos, hits) = (salvos + tally.Salvos, hits + tally.Hits);
            }
            return (salvos, hits);
        }
        var fine = Duel(1);
        var t = Duel(1000);
        output.WriteLine($"fine: {fine.Salvos} salvos, {fine.Hits} hits; coarse: {t.Salvos} salvos, {t.Hits} hits");
        Assert.True(t.Salvos > 0 && t.Hits > 0, "coarse fires and hits");
        Assert.InRange(t.Salvos, fine.Salvos * 0.85, fine.Salvos * 1.15);
    }

    [Fact]
    public void Replays_give_the_same_world()
    {
        var qe = Class("super_dreadnought");
        var live = Make(qe, qe, 15_000, 70, 20, 20, 11);
        live.AdvanceTo(Start + 4 * 60_000);
        live.Apply(GunnerySystem.Cease(1));
        live.Apply(GunnerySystem.Engage(1, -1, 2));
        live.AdvanceTo(Start + 9 * 60_000);
        Assert.True(live.Gunnery.TallyOf(1, 0)!.Value.Salvos > 0);
        ulong h = live.ComputeHash();
        var replay = new World(Start, 11, Settings);
        replay.Ships.AddClass(qe);
        foreach (var sc in live.CommandLog)
        {
            replay.AdvanceTo(sc.At);
            replay.Apply(sc.Command);
        }
        replay.AdvanceTo(live.Time);
        Assert.Equal(h, replay.ComputeHash());
    }

    [Fact]
    public void A_director_holds_one_target()
    {
        // Bismarck's 150 mm and 105 mm starboard batteries share their side's director (fire-control.md F0)
        var bismarck = Class("bismarck");
        var dd = Class("destroyer");
        int b150 = -1, b105 = -1;
        for (int b = 0; b < bismarck.Batteries.Length; b++)
            if (bismarck.Batteries[b].Side == Side.Starboard)
                (b150, b105) = bismarck.Batteries[b].CalibreMm > 120 ? (b, b105) : (b150, b);
        Assert.Equal(bismarck.Links(bismarck.Batteries[b150].Units).ToArray(), bismarck.Links(bismarck.Batteries[b105].Units).ToArray());

        var w = new World(Start, 5, Settings);
        int a = w.Ships.AddClass(bismarck), d = w.Ships.AddClass(dd);
        w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, 10, side: 0));
        foreach (double brg in new[] { 60.0, 120.0 })
        {
            var p = Earth.Position(Lat, Lon);
            var f = Earth.Forward(p, brg);
            Earth.Advance(ref p, ref f, 7_000);
            var (tl, to) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(d, tl, to, 0, 10, side: 1));
        }
        w.Apply(GunnerySystem.Engage(1, b150, 2));
        w.Apply(GunnerySystem.Engage(1, b105, 3));
        w.AdvanceTo(Start + 4 * 60_000);
        var (t150, t105) = (w.Gunnery.TallyOf(1, b150)!.Value, w.Gunnery.TallyOf(1, b105)!.Value);
        output.WriteLine($"apart: 150 mm local {t150.Local}, {t150.Salvos} salvos; 105 mm local {t105.Local}, {t105.Salvos} salvos");
        Assert.False(t150.Local);
        Assert.True(t105.Local);   // its director's on the other target: each mount for itself

        w.Apply(GunnerySystem.Engage(1, b105, 2));   // on the same target, they share it
        w.AdvanceTo(Start + 5 * 60_000);
        Assert.False(w.Gunnery.TallyOf(1, b105)!.Value.Local);

        w.Apply(GunnerySystem.Engage(1, b105, 3));
        w.AdvanceTo(Start + 6 * 60_000);
        Assert.True(w.Gunnery.TallyOf(1, b105)!.Value.Local);
        w.Apply(GunnerySystem.Cease(1, b150));       // let go: the 105 mm take the director again
        w.AdvanceTo(Start + 6 * 60_000 + 2_000);
        Assert.False(w.Gunnery.TallyOf(1, b105)!.Value.Local);
    }

    [Fact]
    public void Fire_control_allocates_nothing()
    {
        var qe = Class("super_dreadnought");
        var w = Make(qe, qe, 15_000, 90, 0, 20, 2);
        w.AdvanceTo(Start + 5 * 60_000);   // tracking, the first salvos and spots: pools at their size
        long before = GC.GetAllocatedBytesForCurrentThread();
        w.AdvanceTo(Start + 15 * 60_000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var t = w.Gunnery.TallyOf(1, 0)!.Value;
        output.WriteLine($"{t.Salvos} salvos, {t.Shells} shells, {t.Hits} hits; {allocated} bytes in the last ten minutes");
        Assert.True(t.Salvos >= 6);
        Assert.Equal(0, allocated);
    }
}
