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

    /// <summary>Hits and shells over n duels of a quarter of an hour, on threads.</summary>
    static (int Hits, int Shells, int Straddles, int Salvos) Duels(ShipClass shooter, ShipClass target, double rangeM, double bearingDeg, int level,
        int n, int seed0 = 1)
    {
        var tallies = new GunnerySystem.Tally[n];
        Parallel.For(0, n, k =>
        {
            var w = Make(shooter, target, rangeM, bearingDeg, 0, 25, seed0 + k, level);
            w.AdvanceTo(Start + 15 * 60_000);
            tallies[k] = w.Gunnery.TallyOf(1, 0)!.Value;
        });
        return (tallies.Sum(t => t.Hits), tallies.Sum(t => t.Shells), tallies.Sum(t => t.Straddles), tallies.Sum(t => t.Salvos));
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
        // the reference on a QE-size box: 5.0% at level 3; level 0 about a fifth of it
        Assert.InRange(pct[3], 2.5, 8);
        Assert.True(pct[0] < pct[1] && pct[1] < pct[3], "the levels' order");
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
        Assert.InRange(british, 4.7 / 2, 4.7 * 2);
        Assert.InRange(german, 4.0 / 2, 4.0 * 2);   // and within ×2 of the record itself
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
        Assert.Equal(t.Salvos * forward, t.Shells);
        output.WriteLine($"{t.Salvos} salvos of {forward} guns");

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
    public void An_engaged_ship_stays_off_rails_and_its_guns_are_silent_in_the_coarse_tier()
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

        // in range, at 1,000x: coarse, so silent (fighting.md decision 4, until 4.9)
        var c = Make(qe, qe, 14_000, 90, 0, 15, 5);
        c.Apply(Command.SetSpeed(1000));
        c.AdvanceTo(Start + 15 * 60_000);
        var t = c.Gunnery.TallyOf(1, 0)!.Value;
        Assert.Equal(BatteryState.Silent, t.State);
        Assert.Equal(0, t.Salvos);
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
