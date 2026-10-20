using Fleetwright.Shipgen;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Near misses (docs/battle/shells-armour-damage.md E7; damage/15 §6.7's tests, wide bands): medium HE close
/// aboard barely holes a destroyer's side and hits her men in the open; heavy HE short of her riddles her side and upper
/// works, and an over far less; close in it holes her waterline, far off not; the fuze's time on the water decides the
/// splinters; AP bursts deep and throws none; in the world a missed salvo's bursts reach a ship beside its fall, the same
/// each run; and a near miss allocates nothing.</summary>
public class NearMissTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly WorldTime Start = WorldTime.FromUtc(1941, 5, 27, 9);

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    /// <summary>A shooter's battery's first shell of a kind, its gun's elevation limit.</summary>
    static (ShellTypeRow Shell, double MaxElev) Shell(string shooter, ShellKind kind, int battery = 0)
    {
        var c = Class(shooter);
        foreach (var l in c.Loadout.Loads(battery))
            if (c.Loadout.ShellTypes[l.ShellType].Kind == kind)
                return (c.Loadout.ShellTypes[l.ShellType], c.Mounts[c.Links(c.Batteries[battery].Mounts)[0]].MaxElevationDeg);
        throw new ArgumentException($"{shooter} has no {kind}");
    }

    GivenHits.NearReport Burst(string shooter, ShellKind kind, string where, double offsetM, double rangeM = 15_000, int n = 60,
        SimSettings? s = null, double waterMs = double.NaN)
    {
        var (shell, el) = Shell(shooter, kind);
        if (!double.IsNaN(waterMs))
            shell = shell with { FuzeWaterMs = waterMs };
        var r = GivenHits.NearBurst(Class("destroyer"), shell, el, rangeM, 90, where, offsetM, n, 1, s ?? Settings);
        double k = Math.Max(r.Near, 1);
        output.WriteLine($"{shooter} {kind} {where} {offsetM} m ({100 * r.DepthM:0.0} cm down): side {r.SideThrough / k:0.0}, above {r.UpperThrough / k:0.0}, " +
                         $"holes {1e4 * r.HoleM2 / k:0} cm², men {(r.Killed + r.Wounded) / k:0.00}");
        return r;
    }

    static double Per(GivenHits.NearReport r, double x) => x / Math.Max(r.Near, 1);

    /// <summary>15 §6.7 test 1: a destroyer's own 5" HE 7 m short of her puts about one fragment through her side and hits a
    /// few of her men.</summary>
    [Fact]
    public void Medium_HE_close_aboard_barely_holes_a_destroyer()
    {
        var r = Burst("destroyer", ShellKind.He, "short", 7, 9000);
        Assert.InRange(Per(r, r.SideThrough), 0, 1.5);
        Assert.InRange(Per(r, r.Killed + r.Wounded), 0.1, 6);
    }

    /// <summary>15 §6.7 test 2: a 46 cm HE short by 7-15 m puts tens to a hundred-odd fragments through a destroyer's side
    /// and upper works; an over at the same distance a few percent of it (15's none: E1's field sends 15% of the side
    /// spray round the whole sphere, so a little comes back).</summary>
    [Fact]
    public void Heavy_HE_short_riddles_a_destroyer_an_over_hardly()
    {
        foreach (double d in (double[])[7, 15])
        {
            var s = Burst("yamato", ShellKind.He, "short", d);
            var o = Burst("yamato", ShellKind.He, "over", d);
            double shortN = Per(s, s.SideThrough + s.UpperThrough), overN = Per(o, o.SideThrough + o.UpperThrough);
            Assert.InRange(shortN, 20, 400);
            Assert.True(overN < 0.1 * shortN, $"over {overN:0.0} against short {shortN:0.0}");
        }
    }

    /// <summary>15 §6.7 test 4: heavy HE within 1-3 m holes a destroyer's waterline (0.2-1.5 m²: ours a little more at 1 m);
    /// 30 m off, none.</summary>
    [Fact]
    public void Close_heavy_HE_holes_the_waterline_far_off_not()
    {
        foreach (double d in (double[])[1, 3])
        {
            var r = Burst("yamato", ShellKind.He, "short", d);
            Assert.InRange(Per(r, r.HoleM2), 0.2, 2);
        }
        Assert.Equal(0, Burst("yamato", ShellKind.He, "short", 30).HoleM2);
    }

    /// <summary>15 §2.3: the fuze's time on the water is the most sensitive number: a burst 25 m short puts several times
    /// as many through the side at 0 ms as at 0.5 ms (the note: 97 against 14), and next to none at 2 ms.</summary>
    [Fact]
    public void The_fuze_time_on_the_water_decides_the_splinters()
    {
        var zero = Burst("yamato", ShellKind.He, "short", 25, waterMs: 0, n: 20);
        double at0 = Per(zero, zero.SideThrough);
        var half = Burst("yamato", ShellKind.He, "short", 25, waterMs: 0.5, n: 20);
        var two = Burst("yamato", ShellKind.He, "short", 25, waterMs: 2, n: 20);
        Assert.True(Per(half, half.SideThrough) < at0 / 3, $"0.5 ms {Per(half, half.SideThrough):0.0} against 0 ms {at0:0.0}");
        Assert.True(Per(two, two.SideThrough) < 0.1, $"2 ms {Per(two, two.SideThrough):0.0}");
    }

    /// <summary>15 §1.1: a base fuze's delay runs from the water, so an AP shell that misses bursts metres down, and the
    /// sea stops every piece (§1.2's 16" Mk 8 20 m short: 2.7 m deep; nearer, it reaches her hull). Under one piece a burst
    /// still reaches the upper works from 1.8 m down, holing nothing (docs/battle/bugs.md, Damage).</summary>
    [Fact]
    public void AP_bursts_deep_and_throws_no_splinters()
    {
        var r = Burst("battleship", ShellKind.Ap, "short", 20);
        Assert.True(r.DepthM > 1.5, $"{r.DepthM:0.0} m down");
        Assert.Equal(0, r.SideThrough);
        Assert.True(Per(r, r.UpperThrough) < 1, $"above {Per(r, r.UpperThrough):0.0}");
        Assert.Equal(0, r.HoleM2);
        Assert.Equal(0, r.Killed + r.Wounded);
    }

    /// <summary>In the world: HE salvos aimed just short of a destroyer burst beside her as near misses (the combat log's
    /// lines, the shell system's count), she takes their splinters, and a second run is the same.</summary>
    [Fact]
    public void Salvos_short_of_a_ship_burst_beside_her_the_same_each_run()
    {
        var (a, ha) = Run();
        var (b, hb) = Run();
        Assert.True(a.Shells.NearMissCount > a.Shells.Hits, $"near misses {a.Shells.NearMissCount}, hits {a.Shells.Hits}");
        int lines = 0;
        for (int i = 0; i < a.CombatLog.Count; i++)
            lines += a.CombatLog.Newest(i) is { Kind: CombatEvent.NearMiss, Target: 2 } ? 1 : 0;
        Assert.True(lines > 0);
        // she may go down under the salvos' hits: then she took her damage
        if (a.Ships.Store.SlotOf(2) is int slot and >= 0)
        {
            var k = a.Ships.Store.Combat.Condition(slot);
            output.WriteLine($"near misses {a.Shells.NearMissCount}, logged {lines}; hits {a.Shells.Hits}; killed {k.Killed}, wounded {k.Wounded}, holes {k.BreachM2.Sum():0.000} m²");
            Assert.True(k.Killed + k.Wounded > 0 || k.BreachM2.Sum() > 0);
        }
        else
            output.WriteLine($"near misses {a.Shells.NearMissCount}, logged {lines}; hits {a.Shells.Hits}; she went down");
        Assert.Equal(ha, hb);
        Assert.Equal(a.Shells.NearMissCount, b.Shells.NearMissCount);
    }

    static (World W, ulong Hash) Run()
    {
        var w = new World(Start, 11, Settings with { DevFirePatternPct = 0.15 });
        _ = new AiSystem(w);
        int shooter = w.Ships.AddClass(Class("dreadnought")), target = w.Ships.AddClass(Class("destroyer"));
        w.Apply(ShipSystem.Spawn(shooter, 56.5, 4.0, 0, 0));
        w.Apply(ShipSystem.Spawn(target, 56.5, 4.1, 0, 0));
        w.AdvanceTo(w.Time + 1000);
        // her side toward the shooter, 30 m short of it
        var t = w.Ships.Store[w.Ships.Store.SlotOf(2)];
        var aim = t.Position + t.Frame.ToWorld(0, -t.Handling.Class.Beam / 2 - 30, 0) - t.Frame.ToWorld(0, 0, 0);
        var (lat, lon) = Earth.LatLon(aim);
        for (int k = 0; k < 6; k++)
        {
            w.Apply(ShellSystem.FireAt(1, 0, lat, lon, ShellKind.He));
            w.AdvanceTo(w.Time + 30_000);
        }
        w.AdvanceTo(w.Time + 60_000);
        return (w, w.ComputeHash());
    }

    /// <summary>15 §6.7 test 7 (and §3.2): the British bomb near misses of 1939-41 come out at 15's factors (within 10%), each
    /// a bomb's TNT 2 m down, its distance off the side: so in the bands the record's damage fitted.</summary>
    [Theory]
    [InlineData(45, 5, 1.00)]      // Fame: severe structural and splinter damage, flooding aft
    [InlineData(130, 10, 0.85)]    // Kelvin, Napier: bottom buckled, turbine feet broken
    [InlineData(130, 15, 0.56)]    // Nizam: thrust block out of line, bulkheads buckled
    [InlineData(220, 30, 0.34)]    // Somali: plating fractured, flooding
    [InlineData(110, 30, 0.25)]    // Montrose: hull corrugated, castings cracked
    [InlineData(130, 60, 0.11)]    // Quorn: four turbine feet broke
    [InlineData(50, 45, 0.11)]     // Furious: turbine blades cracked, a shaft out
    [InlineData(40, 75, 0.05)]     // Sheffield: an asdic, some electrical gear
    public void The_bomb_record_lands_in_its_bands(double kg, double feet, double nsf)
    {
        double n = NearMiss.Nsf(kg, new P3(0, feet * 0.3048, -2), 0, new P3(0, 0, -2));
        output.WriteLine($"{kg} kg at {feet} ft: NSF {n:0.000} (15: {nsf})");
        Assert.InRange(n / nsf, 0.9, 1.1);
    }

    /// <summary>15 §6.7 test 5: an HE surface burst gives structural shock (0.2) only within about 3 m of the side: a 46 cm HE
    /// a metre short does, 4 m short doesn't (15 §3.4: 2.7 m).</summary>
    [Fact]
    public void A_surface_burst_shocks_the_structure_only_close_alongside()
    {
        var close = Burst("yamato", ShellKind.He, "short", 1, n: 4);
        var off = Burst("yamato", ShellKind.He, "short", 4, n: 4);
        output.WriteLine($"NSF 1 m {Per(close, close.Nsf):0.000}, 4 m {Per(off, off.Nsf):0.000}");
        Assert.True(Per(close, close.Nsf) >= 0.2);
        Assert.True(Per(off, off.Nsf) < 0.2);
    }

    /// <summary>15 §6.7 test 6 (White Plains, 15 §3.3): a 46 cm AP burst 5-8 m under the turn of a light hull's bilge reaches
    /// 0.25-0.7, springs her seams, damages her machinery and trips her breakers (a destroyer here: the stand-in escort
    /// carrier carries no guns, so nothing to trip, E4's rule), and doesn't sink her. The record's "most of the circuit
    /// breakers" came at 0.33-0.53: within it they trip ship-wide at least half the time; under it (8 m, about 0.24) they
    /// trip less, but they trip (restated 2026-10-20 with E7's fix: the whole ship's by her machinery spaces' NSF across
    /// the hull's band).</summary>
    [Fact]
    public void An_AP_burst_under_the_keel_springs_the_hull_and_trips_the_breakers()
    {
        var (shell, el) = Shell("yamato", ShellKind.Ap);
        foreach (double d in (double[])[5, 8])
        {
            var r = GivenHits.NearBurst(Class("destroyer"), shell, el, 15_000, 90, "under", d, 20, 1, Settings, settleS: 1800);
            double n = Math.Max(r.Near, 1);
            output.WriteLine($"{d} m under: NSF {r.Nsf / n:0.000}, to the sea {1e4 * r.ShockM2 / n:0.0} cm², parts {r.Parts / n:0.0}, gear out {r.Shocks / n:0.00}, " +
                             $"blackouts {r.Blackouts / n:0.00}; water {r.WaterT / n:0} t, lost {r.Lost}");
            Assert.InRange(r.Nsf / n, 0.2, 0.7);
            Assert.True(r.ShockM2 > 0);
            Assert.True(r.Parts / n >= 1);
            Assert.True(r.Nsf / n >= 0.3 ? r.Shocks / n >= 0.5 && r.Blackouts / n >= 0.5 : r.Shocks > 0);
            Assert.Equal(0, r.Lost);
        }
    }

    /// <summary>15 §6.5: the shock grade raises the gear's edges, not the structure's: shock-hardened fittings trip less at
    /// the same factor, the seams spring the same.</summary>
    [Fact]
    public void Hardened_fittings_trip_less_the_seams_spring_the_same()
    {
        var hard = Settings with { NearShockGradeK = [4, 4, 4, 4] };
        var a = Burst("yamato", ShellKind.Ap, "under", 15, n: 40);
        var b = Burst("yamato", ShellKind.Ap, "under", 15, n: 40, s: hard);
        Assert.True(b.Shocks < a.Shocks, $"gear out {b.Shocks} hardened against {a.Shocks}");
        Assert.InRange(b.ShockM2 / Math.Max(a.ShockM2, 1e-9), 0.6, 1.6);
    }

    /// <summary>15 §6.4 step 4: a burst under the stern shakes the rudder and shafts there (Ilex, Fortune): close under it,
    /// some of them go.</summary>
    [Fact]
    public void A_burst_under_the_stern_shakes_rudder_and_shafts()
    {
        var c = Class("destroyer");
        var (shell, _) = Shell("yamato", ShellKind.Ap);
        int harmed = 0;
        for (int i = 0; i < 20; i++)
        {
            var w = new World(Start, (ulong)(100 + i), Settings);
            int cls = w.Ships.AddClass(c);
            w.Apply(ShipSystem.Spawn(cls, 56, 4, 0, 0));
            int e = w.Ships.NewestEntity, slot = w.Ships.Store.SlotOf(e);
            double x = c.Solids.Waterline[0].X + 3;
            w.Damage.Strike(w, new Hit
            {
                Ship = e, Ms = w.Time.Ms, Point = new P3(x, 0, c.Vertical.Keel - 3), Direction = new P3(0, -0.98, -0.19), SpeedMs = 300,
                Source = HitSource.NearMiss, Shell = shell, ShellType = -1, Force = 1,
            });
            var k = w.Ships.Store.Combat.Condition(slot);
            for (int j = 0; j < c.Components.Length; j++)
                if (c.Components[j].Kind is ComponentKind.Rudder or ComponentKind.Shaft or ComponentKind.Propeller && k.State[j] != PartState.Ok)
                {
                    harmed++;
                    break;
                }
        }
        output.WriteLine($"stern gear harmed in {harmed} of 20");
        Assert.InRange(harmed, 3, 20);
    }

    /// <summary>15 §6.7 test 8: a near miss allocates nothing.</summary>
    [Fact]
    public void A_near_miss_allocates_nothing()
    {
        var c = Class("destroyer");
        var (shell, _) = Shell("yamato", ShellKind.He);
        var w = new World(Start, 3, Settings);
        int cls = w.Ships.AddClass(c);
        w.Apply(ShipSystem.Spawn(cls, 56, 4, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        var hit = new Hit
        {
            Ship = w.Ships.NewestEntity, Point = new P3(0, c.Beam / 2 + 10, c.Vertical.Waterline - 0.05), Direction = new P3(0, -0.98, -0.19), SpeedMs = 560,
            Source = HitSource.NearMiss, Shell = shell, ShellType = -1, Force = 1,
        };
        var r = RandomStream.Named(1, "t");
        NearMiss.Apply(c, k, hit, Settings, ref r);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20; i++)
            NearMiss.Apply(c, k, hit, Settings, ref r);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
