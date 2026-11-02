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
        int n, int seed0 = 1, double zigDeg = 0, double legS = 90, double minutes = 15, SimSettings? with = null)
    {
        var tallies = new GunnerySystem.Tally[n];
        Parallel.For(0, n, k =>
        {
            var w = Make(shooter, target, rangeM, bearingDeg, 0, 25, seed0 + k, level, settings: with ?? Settings with { Smoke = false });
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

    /// <summary>The first straddle's time over n duels, its median (s; a duel without one counts as the whole run), and the
    /// hits in the first minutes.</summary>
    static (double StraddleS, double HitPct) Opening(ShipClass shooter, ShipClass target, double rangeM, int level, int n, double minutes,
        SimSettings? with = null)
    {
        var tallies = new GunnerySystem.Tally[n];
        Parallel.For(0, n, k =>
        {
            var w = Make(shooter, target, rangeM, 90, 0, 25, 1 + k, level, settings: with ?? Settings with { Smoke = false });
            w.AdvanceTo(Start + (long)(minutes * 60_000));
            tallies[k] = w.Gunnery.TallyOf(1, 0)!.Value;
        });
        var times = tallies.Select(t => t.FirstStraddleMs >= 0 ? (t.FirstStraddleMs - Start.Ms) / 1000.0 : minutes * 60).Order().ToArray();
        return (times[n / 2], 100.0 * tallies.Sum(t => t.Hits) / Math.Max(tallies.Sum(t => t.Shells), 1));
    }

    /// <summary>Where the levels must differ (the user, 2026-11-02: "In ideal circumstances one is not really expecting any
    /// difference, so we should do a test with a situation where difference is expected"), QE on QE at 16 kyd, each on the
    /// ladder's rung that holds the mechanism (balance.md BL1ag): with her own zigzag (10° either side every 90 s, past the
    /// plain clock's 5°) and the readings' errors alone, level 1's hand plot is put out by each turn and ranges again where
    /// level 3's Dreyer, fed her own course and speed, keeps its solution (measured 15 v 81%); and opening fire, level 3's
    /// 4 m rangefinder (the FA's 2 m reads her range some 2.7 times worse at the range) straddles her sooner (193 v 266 s).
    /// With every error on, the zigzag shows it too since fc-ladder-check.md FL1 took the own-turn kick (one lumped draw a
    /// salvo, which took level 3 to 10.6%) for the table's own reckoning (level 1 12.1%, level 3 14.6%); the opening is
    /// FL2's (both straddle at about 140 s).</summary>
    [Fact]
    public void Where_the_levels_differ_level_3_does_better()
    {
        var qe = Class("super_dreadnought");
        var readings = Settings with { Smoke = false, GunneryErrors = GunneryErrorScale.Parse("all=0,readings=1") };
        var zig = new double[4];
        var open = new double[4];
        foreach (int level in new[] { 1, 3 })
        {
            var (h, sh, _, _) = Duels(qe, qe, 16 * Kyd, 90, level, 16, zigDeg: 10, with: readings);
            zig[level] = 100.0 * h / sh;
            open[level] = Opening(qe, qe, 16 * Kyd, level, 16, 5, readings).StraddleS;
            var (ah, ash, _, _) = Duels(qe, qe, 16 * Kyd, 90, level, 16, zigDeg: 10);
            var (aos, aop) = Opening(qe, qe, 16 * Kyd, level, 16, 5);
            output.WriteLine($"level {level}, the readings only: zigzagging {zig[level]:F1}% hits, the first straddle at {open[level]:F0} s; " +
                             $"every error on: zigzagging {100.0 * ah / ash:F1}%, the first straddle at {aos:F0} s, {aop:F1}% in 5 min");
        }
        Assert.True(zig[3] > 2 * zig[1], "her own turns put the plain clock out, not the helm-free table");
        Assert.True(open[3] < 0.85 * open[1], "the longer base straddles sooner");
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
