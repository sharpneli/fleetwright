using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>What a hit does (docs/battle/fighting.md 4.3a): the anchors of research/simulation/damage-research/
/// 08-gunfire-effects.md §7.4 put into the era, with wide bands, on the bench's given hits (<see cref="GivenHits"/>,
/// through the world's damage system), and the hooks: a wrecked mount stops firing, a wrecked director drops its battery
/// to local control, lost boilers slow the ship at any clock speed (the power rung is held within what is left, so rails
/// and coarse ships steam no faster), a jammed rudder circles; replays and no allocation.</summary>
public class DamageTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    /// <summary>A battery's shell: the one it fires, or the one of a kind it carries.</summary>
    static (ShellTypeRow Shell, double MaxElev) Shell(ShipClass c, int battery, ShellKind? kind = null)
    {
        var b = c.Batteries[battery];
        int type = c.Loadout.Fires(battery);
        if (kind is { } k)
            foreach (var l in c.Loadout.Loads(battery))
                if (c.Loadout.ShellTypes[l.ShellType].Kind == k)
                    type = l.ShellType;
        return (c.Loadout.ShellTypes[type], c.Mounts[c.Links(b.Mounts)[0]].MaxElevationDeg);
    }

    GivenHits.Report Hits(string target, string shooter, int battery, double rangeM, int n, int perShip, ShellKind? kind = null,
        Action<int, HitPath, TerminalResult, EffectsResult>? each = null, double settleS = 0)
    {
        var (shell, elev) = Shell(Class(shooter), battery, kind);
        var r = GivenHits.Run(Class(target), shell, elev, rangeM, 90, n, perShip, 1, Settings, each, settleS);
        output.WriteLine($"{shooter} {battery} on {target} at {rangeM / 1000:0.#} km: " + string.Join(", ",
            Enum.GetValues<Outcome>().Where(o => r.Outcomes[(int)o] > 0).Select(o => $"{o} {100 * r.Share(o):0.#}%")));
        output.WriteLine($"  after {perShip}: speed {100 * r.SpeedShare / r.Ships:0}%, main {r.MainWorking / r.Ships:0.0} of {r.MainMounts / r.Ships:0}, " +
                         $"directors {r.DirectorsWorking / r.Ships:0.0} of {r.Directors / r.Ships:0}, crippled {100.0 * r.Crippled / r.Ships:0}%, lost {100.0 * r.Lost / r.Ships:0}%, " +
                         $"water {r.WaterT / r.Ships:0} t");
        return r;
    }

    [Fact]
    public void Heavy_AP_mostly_passes_through_a_destroyer_and_HE_bursts()
    {
        var ap = Hits("destroyer", "dreadnought", 0, 10_000, 1000, 1);
        Assert.InRange(ap.Share(Outcome.PassedThrough), 0.7, 1);
        var he = Hits("destroyer", "dreadnought", 0, 10_000, 500, 1, ShellKind.He);
        Assert.InRange(he.Share(Outcome.BurstInside), 0.95, 1);
    }

    [Fact]
    public void Ten_4in_HE_hits_usually_cripple_a_destroyer_and_seldom_sink_it()
    {
        // with half an hour of flooding and fire after: crippled more often than sunk (fighting.md 4.3b). Lost up to 20%
        // (user, 2026-10-16, docs/battle/shells-armour-damage.md S1: the sourced lyddite HE took the V-class from 10 to
        // 17%, all magazines): shipgen's destroyers have one layer of steel with the magazine straight behind it, and
        // better protection schemes come with the hull generator's tuning later, so high losses are expected until then
        foreach (string dd in new[] { "destroyer", "tb_destroyer" })
        {
            var r = Hits(dd, "lion", 1, 6_000, 600, 10, settleS: 1800);
            Assert.InRange((double)r.Crippled / r.Ships, 0.5, 1);
            Assert.InRange((double)r.Lost / r.Ships, 0, 0.20);
            Assert.True(r.Crippled > 3 * r.Lost, "crippled far more often than sunk");
        }
    }

    [Fact]
    public void A_battleship_under_medium_HE_loses_upper_works_and_fire_control_and_keeps_speed_and_turrets()
    {
        var c = Class("super_dreadnought");
        var r = Hits("super_dreadnought", "tiger_1914", 1, 8_000, 1500, 30, settleS: 1800);
        // and stays afloat (fighting.md 4.3b), but for the rare magazine a fire cooks off, rare by design (user,
        // 2026-10-15): about 1.5 % of them over 200 ships, so a few in 50 is chance
        Assert.InRange((double)r.Lost / r.Ships, 0, 0.04);
        Assert.InRange(r.SpeedShare / r.Ships, 0.85, 1);
        Assert.InRange(r.MainWorking / r.MainMounts, 0.85, 1);
        Assert.InRange(r.DirectorsWorking / r.Directors, 0, 0.9);   // fire control lost in some
        int upper = 0, below = 0;
        for (int i = 0; i < c.Components.Length; i++)
            if (c.Components[i].Kind is ComponentKind.Superstructure or ComponentKind.Funnel or ComponentKind.Mast)
                upper += r.PartsOut[i];
        for (int i = c.Components.Length; i < c.NodeCount; i++)
            below += r.PartsOut[i];
        output.WriteLine($"  upper works out {upper}, spaces below out {below}");
        Assert.True(upper > 3 * below, "the upper works take it");
    }

    [Fact]
    public void Heavy_AP_at_long_range_seldom_gets_through_the_belt_and_jams_turrets()
    {
        int belt = 0, through = 0;
        var c = Class("super_dreadnought");
        var r = Hits("super_dreadnought", "super_dreadnought", 0, 16_000, 3000, 1, each: (_, path, t, _) =>
        {
            var xs = path.Crossings;
            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i].Kind is CrossingKind.PartIn)
                    return;   // a part first: not a belt hit
                if (xs[i].Plate == PlateKind.SideArmour)
                {
                    if (c.SidePlates[xs[i].Index].Kind != StrakeKind.Belt)
                        return;   // the main belt's, not an upper or end strake's
                    belt++;
                    through += t.StopT > xs[i].T + 0.5 ? 1 : 0;
                    return;
                }
            }
        });
        output.WriteLine($"  belt hits {belt}, got through {through} ({100.0 * through / Math.Max(belt, 1):0}%); jams {100.0 * r.Jams / r.Hits:0.0}% a hit");
        Assert.True(belt > 100);
        Assert.InRange((double)through / belt, 0, 0.3);
        Assert.InRange((double)r.Jams / r.Hits, 0.005, 0.2);
    }

    // ------------------------------------------------------------------ under water (shells-armour-damage.md P6)

    [Fact]
    public void A_shell_under_water_bleeds_speed_by_its_run_and_a_diving_one_keeps_it_longer()
    {
        var (shell, _) = Shell(Class("super_dreadnought"), 0);
        double l = WaterRun.DecayM(shell, Settings), ld = WaterRun.DecayM(shell with { Diving = true }, Settings);
        Assert.Equal(shell.CalibreMm / 1000 * Settings.UnderwaterDecayCalibres, l, 9);
        Assert.Equal(2 * l, ld, 9);
        Assert.Equal(500, WaterRun.Speed(500, 0, l));
        Assert.Equal(500 / Math.E, WaterRun.Speed(500, l, l), 9);
        Assert.True(WaterRun.Speed(500, 10, ld) > WaterRun.Speed(500, 10, l));
        // the run in a time and the time for a run are one law
        double t = WaterRun.TimeS(500, 12, l);
        Assert.Equal(12, WaterRun.RunInM(500, t, l), 9);
        Assert.True(t > 12.0 / 500);
        // the reach: decay lengths for a base fuze, none for a nose fuze
        Assert.Equal(Settings.UnderwaterReachDecays * l, WaterRun.ReachM(shell, 500, Settings), 9);
        Assert.Equal(0, WaterRun.ReachM(shell with { Fuze = Fuze.Nose }, 500, Settings));
        // a fuze started on the water cuts the reach to where it fires
        var on = Settings with { FuzeStartsOnWater = true };
        Assert.Equal(WaterRun.RunInM(500, shell.FuzeDelayS, l), WaterRun.ReachM(shell, 500, on), 9);
        Assert.True(double.IsPositiveInfinity(WaterRun.FuzeRunM(shell, 500, Settings)));
    }

    /// <summary>A hit on the dreadnought's side 2 m under its waterline, having run runM under the water first.</summary>
    static TerminalResult UnderwaterHit(World w, ShellTypeRow shell, double runM)
    {
        var cls = Class("dreadnought");
        w.Damage.Strike(w, new Hit
        {
            Ship = 1, Ms = w.Time.Ms, Point = new P3(0, cls.Beam / 2 + 1, cls.Vertical.Waterline - 2), Direction = new P3(0, -0.94, -0.34),
            SpeedMs = 500, WaterRunM = runM, Shell = shell, ShellType = 0,
        });
        return w.Damage.LastTerminal;
    }

    [Fact]
    public void An_underwater_hit_strikes_at_what_its_run_left_it()
    {
        var w = World("dreadnought", out int c);
        var (shell, _) = Shell(Class("super_dreadnought"), 0);
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 10));
        double l = WaterRun.DecayM(shell, Settings), last = double.PositiveInfinity;
        foreach (double run in new[] { 0.0, 2, 5, 10, 20 })
        {
            var t = UnderwaterHit(w, shell, run);
            output.WriteLine($"run {run} m: {t.StrikeMs:0} m/s, {t.Outcome}");
            Assert.Equal(run > 0, t.Underwater);
            Assert.Equal(500 * Math.Exp(-run / l), t.StrikeMs, 6);
            Assert.True(t.StrikeMs < last || run == 0);
            last = t.StrikeMs;
        }
    }

    [Fact]
    public void A_fuze_started_on_the_water_arms_the_shell_at_the_hull()
    {
        var w = World("dreadnought", out int c, Settings with { FuzeStartsOnWater = true });
        var (shell, _) = Shell(Class("super_dreadnought"), 0);
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 10));
        for (int i = 0; i < 40; i++)
        {
            var t = UnderwaterHit(w, shell, 3);
            Assert.True(t.Outcome is not (Outcome.PassedThrough or Outcome.Defeated or Outcome.Ricochet), $"{t.Outcome}: armed at the hull");
        }
    }

    // ------------------------------------------------------------------ the hooks

    static World World(string design, out int cls, SimSettings? s = null)
    {
        var w = new World(Start, 3, s ?? Settings);
        cls = w.Ships.AddClass(Class(design));
        return w;
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    [Fact]
    public void A_wrecked_mount_stops_firing_and_a_wrecked_director_drops_its_battery_to_local_control()
    {
        var w = World("dreadnought", out int c);
        var cls = Class("dreadnought");
        var p = Earth.Position(56, 4);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 9000);
        var (tl, to) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 15, side: 0));
        w.Apply(ShipSystem.Spawn(c, tl, to, 0, 15, side: 1));
        var mounts = cls.Links(cls.Batteries[0].Mounts);
        int a = mounts[0];
        w.Apply(DamageSystem.Wreck(1, cls.Mounts[a].Component, PartState.Destroyed));
        w.Apply(DamageSystem.Wreck(1, cls.Directors[cls.Links(cls.Batteries[0].Directors)[0]].Component, PartState.Destroyed));
        w.Apply(GunnerySystem.Engage(1, 0, 2));
        w.AdvanceTo(w.Time + 6 * 60_000);
        var slot = w.Ships.Store.SlotOf(1);
        int fired = 0;
        foreach (int m in mounts)
            if (m != a)
                fired += w.Ships.Store.Combat.Mount(slot, m).Rounds < cls.Mounts[m].Rounds ? 1 : 0;
        Assert.Equal(cls.Mounts[a].Rounds, w.Ships.Store.Combat.Mount(slot, a).Rounds);
        Assert.True(fired > 0, "the others fire");
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        Assert.True(snap.Batteries.Local[0], "no director: local control");
        Assert.Equal(cls.Mounts.Length - 1, snap.Conditions.MountsWorking[snap.Conditions.IndexOf(1)]);
    }

    [Fact]
    public void Boilers_lost_slow_the_ship_at_any_clock_speed()
    {
        var cls = Class("dreadnought");
        double top = cls.Motion.MaxSpeedKn;
        foreach (double speed in new[] { 1.0, 1000 })
        {
            var w = World("dreadnought", out int c);
            w.Apply(ShipSystem.Spawn(c, 56, 4, 0, top));
            w.Apply(Command.SetSpeed(speed));
            w.AdvanceTo(w.Time + 60_000);
            for (int r = 0; r < cls.Rooms.Length; r++)
                if (cls.Rooms[r].Kind == RoomKind.BoilerRoom)
                    w.Apply(DamageSystem.Wreck(1, cls.RoomNode(r), PartState.Damaged));   // each at half: half the steam
            w.AdvanceTo(w.Time + 30 * 60_000);
            double kn = Ship(w, 1).Speed / Handling.MsPerKnot;
            output.WriteLine($"at {speed}x: {Ship(w, 1).Tier}, power {Ship(w, 1).PowerCap:0.00}, {kn:0.0} kn of {top:0.0}");
            Assert.Equal(0.5, Ship(w, 1).PowerCap, 3);
            Assert.InRange(kn, 0.7 * top, 0.9 * top);   // half the plant's power: 80-85% of the speed (P ∝ v³, or a little less steep)
        }
    }

    [Fact]
    public void A_rudder_jammed_over_keeps_the_ship_circling()
    {
        var w = World("dreadnought", out int c, Settings with { RudderJamChance = 1 });
        var cls = Class("dreadnought");
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 15));
        w.Apply(ShipSystem.Rudder(1, -20));
        w.AdvanceTo(w.Time + 20_000);
        int steering = Enumerable.Range(0, cls.Rooms.Length).First(r => cls.Rooms[r].Kind == RoomKind.Steering);
        w.Apply(DamageSystem.Wreck(1, cls.RoomNode(steering), PartState.Destroyed));
        Assert.Equal(Steering.Jammed, Ship(w, 1).Steering);
        w.Apply(ShipSystem.Course(1, 0));   // the helm can't move it
        double h0 = Ship(w, 1).HeadingDeg;
        w.AdvanceTo(w.Time + 60_000);
        double turned = h0 - Ship(w, 1).HeadingDeg;
        turned -= 360 * Math.Floor(turned / 360);
        output.WriteLine($"rudder {Ship(w, 1).Rudder * 180 / Math.PI:0.0}°, turned {turned:0}° to port in a minute");
        Assert.True(Ship(w, 1).Rudder < -0.2, "still over to port");
        Assert.InRange(turned, 20, 359);
    }

    // ------------------------------------------------------------------ magazines set off, for the screens

    static Snapshot Snap(World w)
    {
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        return snap;
    }

    [Fact]
    public void A_magazine_set_off_by_a_hit_records_its_room_once()
    {
        var w = World("dreadnought", out int c, Settings with { MagazineFlashChance = 1, TurretFlashChance = 1 });
        var cls = Class("dreadnought");
        var (shell, _) = Shell(Class("super_dreadnought"), 0);
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 10));
        w.AdvanceTo(w.Time + 1000);
        var rng = RandomStream.Named(2, "test magazine hits");
        int n = 0;
        for (; n < 2000 && Ship(w, 1).Alive && w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1)).Status == ShipStatus.Afloat; n++)
            w.Damage.Strike(w, new Hit
            {
                Ship = 1, Ms = w.Time.Ms, Point = new P3(rng.NextDouble(-60, 60), cls.Beam / 2 + 1, rng.NextDouble(-2, 10)),
                Direction = new P3(0, -0.8, -0.6), SpeedMs = 500, Shell = shell, ShellType = 0,
            });
        var b = Snap(w).Blasts;
        output.WriteLine($"blew after {n} hits: room {b.Room[0]}, {b.Cause[0]}");
        Assert.Equal(1, b.Count);
        Assert.Equal(RoomKind.Magazine, cls.Rooms[b.Room[0]].Kind);
        Assert.Equal(BlastCause.Shell, b.Cause[0]);
        Assert.Equal(1, b.Ship[0]);
        Assert.True((b.At[0] - Ship(w, 1).Position).Length < 1);
    }

    [Fact]
    public void A_magazine_set_off_by_a_fire_records_its_room()
    {
        var w = World("dreadnought", out int c, Settings with { FireSpreadPerMin = 1, FireOutPerMin = 0, FireMagazineInPerMin = 1, FireMagazineNextPerMin = 1 });
        var cls = Class("dreadnought");
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 10));
        int mag = Enumerable.Range(0, cls.Rooms.Length).First(r => cls.Rooms[r].Kind == RoomKind.Magazine);
        w.Apply(DamageSystem.Burn(1, (cls.Rooms[mag].X0 + cls.Rooms[mag].X1) / 2));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1));
        for (int min = 0; min < 30 && k.Status == ShipStatus.Afloat; min++)
            w.AdvanceTo(w.Time + 60_000);
        var b = Snap(w).Blasts;
        output.WriteLine($"{k.Loss}, {b.Count} blasts");
        Assert.Equal(LossKind.Magazine, k.Loss);
        Assert.Equal(1, b.Count);
        Assert.Equal(RoomKind.Magazine, cls.Rooms[b.Room[0]].Kind);
        Assert.Equal(BlastCause.Fire, b.Cause[0]);
    }

    [Fact]
    public void The_dev_sets_off_a_mounts_magazine_and_the_record_outlives_the_ship_for_its_while()
    {
        var w = World("dreadnought", out int c, Settings with { BlastLifeMs = 120_000 });
        var cls = Class("dreadnought");
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 10));
        int mount = Enumerable.Range(0, cls.Mounts.Length).First(m => cls.Mounts[m].Magazine >= 0);
        w.Apply(DamageSystem.Sink(1, LossKind.Magazine, mount));
        w.Apply(DamageSystem.Sink(1, LossKind.Magazine, mount));   // going down already: nothing more
        var b = Snap(w).Blasts;
        Assert.Equal(1, b.Count);
        Assert.Equal(cls.Mounts[mount].Magazine, b.Room[0]);
        Assert.Equal(BlastCause.Dev, b.Cause[0]);
        w.AdvanceTo(w.Time + 90_000);
        Assert.Equal(-1, w.Ships.Store.SlotOf(1));   // gone under
        Assert.Equal(1, Snap(w).Blasts.Count);
        w.AdvanceTo(w.Time + 60_000);
        Assert.Equal(0, Snap(w).Blasts.Count);
    }

    [Fact]
    public void Hits_replay_and_allocate_nothing()
    {
        var w = World("dreadnought", out int c);
        var cls = Class("dreadnought");
        var (shell, _) = Shell(Class("super_dreadnought"), 0);
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 10));
        w.AdvanceTo(w.Time + 1000);
        var rng = RandomStream.Named(1, "test hits");
        Hit Next() => new()
        {
            Ship = 1, Ms = w.Time.Ms, Point = new P3(rng.NextDouble(-60, 60), cls.Beam / 2 + 1, rng.NextDouble(-6, 4)), Direction = new P3(0, -0.96, -0.28),
            SpeedMs = 450, Shell = shell, ShellType = 0,
        };
        for (int i = 0; i < 20; i++)
            w.Damage.Strike(w, Next());
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
            w.Damage.Strike(w, Next());
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        // the same hits on a second world: the same condition
        var w2 = World("dreadnought", out int c2);
        w2.Apply(ShipSystem.Spawn(c2, 56, 4, 0, 10));
        w2.AdvanceTo(w2.Time + 1000);
        rng = RandomStream.Named(1, "test hits");
        for (int i = 0; i < 120; i++)
            w2.Damage.Strike(w2, Next() with { Ms = w2.Time.Ms });
        Assert.Equal(w.ComputeHash(), w2.ComputeHash());
    }
}
