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
        int type = b.Fires;
        if (kind is { } k)
            foreach (var l in c.ShellLoads(b.Shells))
                if (c.ShellTypes[l.ShellType].Kind == k)
                    type = l.ShellType;
        return (c.ShellTypes[type], c.Mounts[c.Links(b.Mounts)[0]].MaxElevationDeg);
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
        // with half an hour of flooding and fire after: crippled more often than sunk (fighting.md 4.3b)
        foreach (string dd in new[] { "destroyer", "tb_destroyer" })
        {
            var r = Hits(dd, "lion", 1, 6_000, 600, 10, settleS: 1800);
            Assert.InRange((double)r.Crippled / r.Ships, 0.5, 1);
            Assert.InRange((double)r.Lost / r.Ships, 0, 0.15);
            Assert.True(r.Crippled > 3 * r.Lost, "crippled far more often than sunk");
        }
    }

    [Fact]
    public void A_battleship_under_medium_HE_loses_upper_works_and_fire_control_and_keeps_speed_and_turrets()
    {
        var c = Class("super_dreadnought");
        var r = Hits("super_dreadnought", "tiger_1914", 1, 8_000, 1500, 30, settleS: 1800);
        Assert.Equal(0, r.Lost);   // and stays afloat (fighting.md 4.3b)
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
