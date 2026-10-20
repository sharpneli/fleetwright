using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The repair parties at water jobs (docs/battle/damage-control.md DC3; 09 §3-§4): 09 §8's thirty splinter holes
/// plugged by a drilled crew and left by nobody; a watertight door left open shut by the men on the spot after the
/// watch's delay, or by a party where there's no watch; a hole beyond repair at sea never worked on.</summary>
public class RepairTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    /// <summary>A stopped ship's condition with a side's doctrine (null: nobody fights her damage), closed up, damaged.</summary>
    static ShipCondition Fresh(ShipClass c, string? doctrine)
    {
        var k = new ShipCondition();
        k.Reset(c);
        k.Readiness = Settings.Readiness.Single(r => r.Name == "closed up");
        k.DamageControl = doctrine is null ? default : Settings.DamageControl.Single(d => d.Name == doctrine);
        k.Damaged = true;
        return k;
    }

    /// <summary>Her flooding and repairs stepped as the damage system does, for a while at 1 s.</summary>
    static void Run(ShipClass c, FloodModel f, ShipCondition k, double seconds, long from = 0)
    {
        var r = RandomStream.FromSeed(5);
        for (int t = 0; t < seconds; t++)
        {
            long now = from + t * 1000L;
            Repairs.Step(c, f, k, 1, now, Settings);
            Flooding.Step(c, f, k, 1, k.PowerCap, 0, Settings, ref r);
            Flooding.Settle(c, f, k, Settings, 1);
        }
    }

    /// <summary>09 §8's thirty splinter holes, 3-12 cm, along her starboard side from a metre under the waterline to 2.5
    /// over it.</summary>
    static void Splinters(ShipClass c, ShipCondition k)
    {
        var r = RandomStream.FromSeed(4);
        double x0 = c.Hull.X0 + 0.15 * c.Length, x1 = c.Hull.X1 - 0.15 * c.Length;
        for (int i = 0; i < 30; i++)
        {
            double d = 0.03 + 0.09 * r.NextDouble(), x = x0 + r.NextDouble() * (x1 - x0), z = c.Vertical.Waterline - 1 + 3.5 * r.NextDouble();
            var (cell, y) = FloodModel.SideCell(c, x, z, true);
            if (cell >= 0)
                k.Hole(cell, Math.PI * d * d / 4, new P3(x, y, z));
        }
    }

    [Fact]
    public void A_drilled_crew_plugs_thirty_splinter_holes_and_nobody_lets_them_flood()
    {
        var c = Class("super_dreadnought");
        var f = new FloodModel(c);
        var drilled = Fresh(c, "1916");
        var nobody = Fresh(c, null);
        Splinters(c, drilled);
        Splinters(c, nobody);
        Run(c, f, drilled, 7200);
        Run(c, f, nobody, 7200);
        int left = 0;
        for (int i = 0; i < c.Cells.Length; i++)
            left += drilled.HoleOpenN[i * ShipCondition.HoleClasses + (int)HoleClass.Plug] > 0 &&
                    drilled.HoleZ[i * ShipCondition.HoleClasses + (int)HoleClass.Plug] < c.Vertical.Waterline ? 1 : 0;
        output.WriteLine($"thirty splinter holes, 2 h: drilled {drilled.WaterT:0} t (cells with holes under the waterline still open {left}), " +
                         $"nobody {nobody.WaterT:0} t, list {nobody.Attitude.HeelDeg:0.0}°");
        Assert.Equal(0, left);
        Assert.True(drilled.WaterT < 0.25 * nobody.WaterT, "the crew holds the small leaks");
    }

    /// <summary>A door edge over the waterline (a main transverse bulkhead), its door left open, one side kept full.</summary>
    static (int Edge, int Pool, int Wet) Door(ShipClass c, FloodModel f, ShipCondition k)
    {
        int edge = Array.FindIndex(f.Edges, e => e.Join == JoinKind.Bulkhead && Leaks.HasDoor(c, c.Neighbour(e.Pool), e.A) &&
                                                 Math.Abs(c.Cells[e.A].Box.Base - c.Cells[e.B].Box.Base) < 0.1 &&
                                                 c.Cells[e.A].BattleCrew + c.Cells[e.B].BattleCrew + c.Cells[e.A].Crew + c.Cells[e.B].Crew > 0);
        var e = f.Edges[edge];
        k.JoinMarks[e.Pool] |= JoinMark.DoorOpen;
        k.WaterM3[e.A] = f.Capacity[e.A];
        return (edge, e.Pool, e.A);
    }

    [Fact]
    public void A_door_left_open_is_shut_by_the_watch_or_later_by_a_party()
    {
        var c = Class("super_dreadnought");
        var f = new FloodModel(c);
        double Shut(string doctrine)
        {
            var k = Fresh(c, doctrine);
            var (_, pool, _) = Door(c, f, k);
            for (int t = 1; t <= 3600; t++)
            {
                Run(c, f, k, 1, t * 1000L);
                if ((k.JoinMarks[pool] & JoinMark.DoorOpen) == 0)
                    return t;
            }
            return double.PositiveInfinity;
        }
        double rn = Shut("1916"), none = Shut("1890");
        output.WriteLine($"a door open with water at it: shut at {rn:0} s (1916, the watch at 180 s), {none:0} s (unorganised: a party)");
        Assert.InRange(rn, 180, 200);
        Assert.InRange(none, 300, 1200);
    }

    [Fact]
    public void In_a_blackout_the_parties_rig_portable_pumps_and_clear_a_flooded_space()
    {
        var c = Class("dreadnought");
        var f = new FloodModel(c);
        // a dry-ish space below the waterline half full of seepage, no hole of its own, and no power for the drainage
        int cell = Array.FindIndex(c.Cells.ToArray(), x => x.BelowWaterline && x.VolumeM3 > 150 && c.Rooms[x.Room].Kind == RoomKind.Stores);
        double Left(string? doctrine)
        {
            var k = Fresh(c, doctrine);
            k.PowerCap = 0;
            k.WaterM3[cell] = 0.5 * f.Capacity[cell];
            Run(c, f, k, 3 * 3600);
            return k.WaterM3.Sum();   // aboard: what leaks down into the spaces below is still hers
        }
        double drilled = Left("1916"), nobody = Left(null);
        output.WriteLine($"{c.Drainage.PortablePumps} portable pumps of {c.Drainage.PortableTph:0} t/h; half of a {f.Capacity[cell]:0} m³ store, aboard " +
                         $"after 3 h in a blackout: drilled {drilled:0} m³, nobody {nobody:0} m³");
        Assert.True(drilled < 0.2 * nobody, "pumped out (puddles under RepairPumpMinM3 left)");
        Assert.True(nobody > 0.4 * f.Capacity[cell], "no power, nobody: it stays");
    }

    [Fact]
    public void A_captain_slows_for_a_bow_hole_by_his_doctrine_unless_ordered_to_hold_speed()
    {
        var c = Class("seydlitz");
        double Kn(string doctrine, bool holds)
        {
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 3, Settings);
            w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 24));
            w.Apply(DamageSystem.SetDamageControl(0, Array.FindIndex(Settings.DamageControl, d => d.Name == doctrine)));
            w.Apply(DamageSystem.HoldSpeed(1, holds));
            // a heavy shell's hole in the bow at the waterline, too big for her pumps: a collision mat, the way off her
            double x = c.Hull.X1 - 0.12 * c.Length;
            w.Apply(DamageSystem.SideHole(1, HoleAt.X, x, 0.5, 1.5));
            w.AdvanceTo(w.Time + 45 * 60_000);
            int slot = w.Ships.Store.SlotOf(1);
            var k = w.Ships.Store.Combat.Condition(slot);
            output.WriteLine($"{doctrine}{(holds ? ", holding speed" : "")}: damage control asks {k.SafeSpeedKn:0.0} kn, she makes {w.Ships.Store[slot].Speed / 0.514444:0.0} kn");
            return w.Ships.Store[slot].Speed / 0.514444;
        }
        double slowed = Kn("1916", false), held = Kn("1916", true), late = Kn("1890", false);
        Assert.InRange(slowed, 0, Settings.MatKn + 1.5);   // the way coming off her after a quarter of an hour
        Assert.True(held > 20, "her orders to hold speed");
        Assert.True(late > 20, "the 1890 doctrine holds its speed for an hour first");
    }

    /// <summary>A dreadnought at 15 kn with parts wrecked (nodes), stepped for a while; her steering then.</summary>
    static Steering SteeringAfter(ShipClass c, int[] nodes, double minutes)
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 3, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 15));
        foreach (int node in nodes)
            w.Apply(DamageSystem.Wreck(1, node, PartState.Disabled));
        w.AdvanceTo(w.Time + (long)(minutes * 60_000));
        return w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1)).Steering;
    }

    [Fact]
    public void Steering_lost_at_the_gear_goes_to_the_engines_then_to_hand_and_a_wrecked_rudder_stays_on_the_engines()
    {
        var c = Class("dreadnought");
        int room = Array.FindIndex(c.Rooms.ToArray(), r => r.Kind == RoomKind.Steering);
        int[] rudders = [.. Enumerable.Range(0, c.Components.Length).Where(i => c.Components[i].Kind == ComponentKind.Rudder)];
        Assert.True(room >= 0 && rudders.Length > 0);
        int[] gear = [c.RoomNode(room)];
        var (g0, g2, g30) = (SteeringAfter(c, gear, 0.5), SteeringAfter(c, gear, 2), SteeringAfter(c, gear, 30));
        var (r2, r30) = (SteeringAfter(c, rudders, 2), SteeringAfter(c, rudders, 30));
        output.WriteLine($"the steering gear wrecked: {g0} at 30 s, {g2} at 2 min, {g30} at 30 min; the rudders ({rudders.Length}): {r2} at 2 min, {r30} at 30 min");
        Assert.True(g0 is Steering.Jammed or Steering.Lost);
        Assert.Equal(Steering.Engines, g2);
        Assert.Equal(Steering.Hand, g30);
        Assert.Equal(Steering.Engines, r2);
        Assert.Equal(Steering.Engines, r30);
    }

    [Fact]
    public void The_log_tells_damage_controls_story()
    {
        var c = Class("dreadnought");
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 3, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 15));
        w.Apply(DamageSystem.Wreck(1, c.RoomNode(Array.FindIndex(c.Rooms.ToArray(), r => r.Kind == RoomKind.Steering)), PartState.Disabled));
        w.AdvanceTo(w.Time + 30 * 60_000);
        var dc = Enumerable.Range(0, w.CombatLog.Count).Select(i => w.CombatLog.Newest(i))
            .Where(e => e.Kind == CombatEvent.DamageControl && e.Target == 1 && (DcEvent)e.Count == DcEvent.Steering).Select(e => (Steering)e.Battery).ToList();
        output.WriteLine("the steering's story, newest first: " + string.Join(", ", dc));
        Assert.Equal([Steering.Hand, Steering.Engines], dc);
    }

    [Fact]
    public void About_the_tables_share_of_disabled_mounts_come_back_within_two_hours()
    {
        var c = Class("dreadnought");
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 3, Settings);
        int cls = w.Ships.AddClass(c), mount = c.Mounts[0].Component, n = 40;
        for (int i = 0; i < n; i++)
        {
            w.Apply(ShipSystem.Spawn(cls, 56 + 0.05 * i, 4, 0, 0));
            w.Apply(DamageSystem.Wreck(w.Ships.NewestEntity, mount, PartState.Disabled));
        }
        w.AdvanceTo(w.Time + 2 * 3_600_000);
        int back = 0;
        for (int e = 1; e <= n; e++)
            back += w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(e)).State[mount] == PartState.Damaged ? 1 : 0;
        output.WriteLine($"{back} of {n} disabled main mounts back (damaged) after 2 h; the table's chance {Settings.RepairPartShare[0]:0%}");
        Assert.InRange(back, 0.1 * n, 0.55 * n);
    }

    [Fact]
    public void A_hole_beyond_repair_at_sea_is_never_worked_on()
    {
        var c = Class("super_dreadnought");
        var f = new FloodModel(c);
        var k = Fresh(c, "1944");
        var (cell, y) = FloodModel.SideCell(c, c.Hydrostatics.Lcf, c.Vertical.Waterline - 0.5, true);
        k.Hole(cell, 20, new P3(c.Hydrostatics.Lcf, y, c.Vertical.Waterline - 0.5));
        Run(c, f, k, 1800);
        Assert.Equal(20, k.HoleOpenM2[cell * ShipCondition.HoleClasses + (int)HoleClass.Beyond], 9);
        Assert.Equal(20, k.BreachM2[cell], 9);
    }
}
