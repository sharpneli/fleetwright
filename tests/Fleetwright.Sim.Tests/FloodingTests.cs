using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Fire, flooding and going down (docs/battle/fighting.md 4.3b): the anchors of damage-model-research.md §11 with
/// wide bands, on holes made by the dev's <see cref="DamageSystem.Breach"/> in ships stopped at sea: a waterline hole
/// amidships, a holed end, a deck pushed under the waterline, machinery spaces flooded in a destroyer and a capital
/// ship; the step's size; replays, no allocation, the rails rule, and what the enemy sees.</summary>
public class FloodingTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    /// <summary>A ship of a design stopped at sea (entity 1) in a world of its own.</summary>
    static (World W, ShipClass C) Ship(string design, SimSettings? s = null, double kn = 0)
    {
        var w = new World(Start, 3, s ?? Settings);
        var c = Class(design);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, kn));
        return (w, c);
    }

    static ShipCondition Condition(World w, int e = 1) => w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(e));

    /// <summary>A hole on the side at x (ship frame), z metres over the waterline.</summary>
    static void Hole(World w, ShipClass c, double x, double z, double areaM2, bool starboard = true) =>
        w.Apply(DamageSystem.SideHole(1, HoleAt.X, x, z, areaM2, starboard));

    /// <summary>Every cell of n rooms of a kind (those nearest amidships) holed at its floor.</summary>
    static void Flood(World w, ShipClass c, RoomKind kind, int n) => w.Apply(DamageSystem.FloodRooms(1, kind, n));

    void Report(string what, ShipCondition k) =>
        output.WriteLine($"{what}: {k.WaterT:0} t, sinkage {k.Attitude.SinkageM:0.00} m, trim {k.Attitude.TrimDeg:0.00}°, heel {k.Attitude.HeelDeg:0.0}°, " +
                         $"GM {k.GmM:0.00} m, {k.Status} {k.Loss}");

    [Fact]
    public void A_waterline_hole_amidships_takes_hundreds_of_tonnes_and_lists_the_ship_a_few_degrees()
    {
        var (w, c) = Ship("lion");   // wing bunkers amidships: a hole there floods one side
        Hole(w, c, c.Hydrostatics.Lcf, -0.5, 0.5);
        w.AdvanceTo(w.Time + 30 * 60_000);
        var k = Condition(w);
        Report("lion, 0.5 m² amidships", k);
        Assert.InRange(k.WaterT, 100, 1500);
        Assert.InRange(k.Attitude.HeelDeg, 0.5, 6);
        Assert.Equal(ShipStatus.Afloat, k.Status);
    }

    [Fact]
    public void A_holed_end_floods_along_it_and_trims_the_ship_by_the_bow()
    {
        var (w, c) = Ship("super_dreadnought");
        foreach (double x in new[] { 85.0, 78, 70, 62 })
        {
            Hole(w, c, x, -1, 2);
            Hole(w, c, x, -1, 2, starboard: false);
        }
        w.AdvanceTo(w.Time + 60 * 60_000);
        var k = Condition(w);
        Report("QE, the bow holed", k);
        Assert.InRange(k.WaterT, 500, 3000);
        Assert.True(k.Attitude.TrimDeg > 0.3, "down by the bow");
        Assert.InRange(Math.Abs(k.Attitude.HeelDeg), 0, 1);
        int wet = 0;
        for (int r = 0; r < c.Rooms.Length; r++)
            wet += c.Links(c.Rooms[r].Cells).ToArray().Sum(i => k.WaterM3[i]) > 10 ? 1 : 0;
        Assert.True(wet >= 4, $"along it: {wet} spaces wet");
    }

    [Fact]
    public void A_deck_pushed_under_the_waterline_floods_fast()
    {
        // a deck over the waterline with water on it, the space under it dry: the leak through it, then the same with the
        // ship settled until that deck is a metre under the sea outside
        var c = Class("super_dreadnought");
        var f = new FloodModel(c);
        var e = f.Edges.First(x => x.Join == JoinKind.Deck && x.Upper >= 0 && x.Z > c.Vertical.Waterline + 0.5 && x.Z < c.Vertical.Waterline + 3 &&
                                   Math.Abs(f.Cx[x.A] - c.Hydrostatics.Lcf) < 30);
        int upper = e.Upper, lower = upper == e.A ? e.B : e.A;
        double moved(double sinkage)
        {
            var (w, _) = Ship("super_dreadnought");
            var k = Condition(w);
            k.WaterM3[upper] = 0.3 * f.Capacity[upper];
            k.Attitude = new Attitude(sinkage, 0, 0);
            Flooding.Step(c, f, k, 10, 0, Settings);
            return k.WaterM3[lower];
        }
        double dry = moved(0), sunk = moved(e.Z - c.Vertical.Waterline + 1);
        output.WriteLine($"{c.CellId(upper)} over {c.CellId(lower)}: {dry:0.00} m³ in 10 s, {sunk:0.0} m³ with the deck under");
        Assert.True(sunk > 50 * Math.Max(dry, 0.01), "fast once under");
    }

    [Fact]
    public void A_destroyer_with_three_machinery_spaces_flooded_goes_down_and_with_one_lives()
    {
        foreach (var (boilers, engines, lost) in new[] { (1, 0, false), (2, 1, true) })
        {
            var (w, c) = Ship("destroyer");
            Flood(w, c, RoomKind.BoilerRoom, boilers);
            Flood(w, c, RoomKind.EngineRoom, engines);
            var status = w.Damage.Settle(w, 1, 4 * 3600);
            var k = Condition(w);
            Report($"destroyer, {boilers} boiler and {engines} engine rooms flooded", k);
            Assert.Equal(lost ? ShipStatus.GoingDown : ShipStatus.Afloat, status);
        }
    }

    [Fact]
    public void A_capital_ship_lives_through_thousands_of_tonnes()
    {
        var (w, c) = Ship("super_dreadnought");
        // its engine room (a hole in the side there opens the wing over the deck's slope since protection-plan.md H8:
        // the room is flooded as the destroyer's are)
        Flood(w, c, RoomKind.EngineRoom, 1);
        var status = w.Damage.Settle(w, 1, 4 * 3600);
        var k = Condition(w);
        Report("QE, an engine room flooded", k);
        Assert.Equal(ShipStatus.Afloat, status);
        Assert.InRange(k.WaterT, 1000, 5000);
    }

    [Fact]
    public void Flooding_at_a_1_s_and_a_5_s_step_agrees()
    {
        double Water(int ms, string design, double x)
        {
            var (w, c) = Ship(design, Settings with { FloodMs = ms });
            Hole(w, c, x, -1, 0.5);
            w.Damage.Settle(w, 1, 20 * 60);
            return Condition(w).WaterT;
        }
        foreach (var design in new[] { "super_dreadnought", "destroyer" })
        {
            double x = Class(design).Hydrostatics.Lcf, a = Water(1000, design, x), b = Water(5000, design, x);
            output.WriteLine($"{design}: {a:0} t at 1 s, {b:0} t at 5 s");
            Assert.InRange(b / a, 0.95, 1.05);
        }
    }

    [Fact]
    public void A_wet_or_burning_ship_stays_off_rails_and_its_steps_replay_and_allocate_nothing()
    {
        World Run()
        {
            var (w, c) = Ship("dreadnought", kn: 12);
            w.Apply(Command.SetSpeed(1000));
            Hole(w, c, c.Hydrostatics.Lcf + 20, -0.5, 0.3);
            Condition(w).CellFire[c.Cells.Length / 2] = true;
            w.AdvanceTo(w.Time + 10 * 60_000);
            return w;
        }
        var w1 = Run();
        int slot = w1.Ships.Store.SlotOf(1);
        Assert.False(w1.Ships.Store.OnRails(slot), "wet: off rails");
        Assert.True(Condition(w1).WaterT > 0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        w1.AdvanceTo(w1.Time + 10 * 60_000);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        var w2 = Run();
        w2.AdvanceTo(w2.Time + 10 * 60_000);
        Assert.Equal(w1.ComputeHash(), w2.ComputeHash());
    }

    [Fact]
    public void A_ship_going_down_leaves_its_group_falls_silent_and_is_seen_to_go()
    {
        var w = new World(Start, 3, Settings);
        var dd = w.Ships.AddClass(Class("destroyer"));
        w.Apply(ShipSystem.Spawn(dd, 56, 4, 0, 0, side: 0));
        w.Apply(ShipSystem.Spawn(dd, 56, 4.01, 0, 0, side: 0));
        w.Apply(ShipSystem.Spawn(dd, 56.05, 4, 180, 0, side: 1));
        w.Apply(GroupSystem.Create(1));
        w.Apply(GroupSystem.Add(w.Groups.Of(1)!.GroupId, 2));
        w.AdvanceTo(w.Time + 60_000);
        var c = Class("destroyer");
        Flood(w, c, RoomKind.BoilerRoom, 2);
        Flood(w, c, RoomKind.EngineRoom, 1);
        WorldTime gone = default;
        for (int i = 0; i < 6 * 60 && w.Ships.Store.SlotOf(1) >= 0; i++)
        {
            w.AdvanceTo(w.Time + 60_000);
            if (w.Ships.Store.SlotOf(1) >= 0 && Condition(w) is { Status: ShipStatus.GoingDown } k && gone == default)
            {
                gone = w.Time;
                Assert.Equal(0, k.MountsWorking);
                Assert.Null(w.Groups.Of(1));
                var pic = w.Sightings.Of(1)!;
                Assert.True((pic[pic.IndexOf(1)].Seen & SeenFlags.GoingDown) != 0 || !pic[pic.IndexOf(1)].InSight, "seen going down");
            }
        }
        output.WriteLine($"going down after {(gone.Ms - Start.Ms) / 60_000} min, gone after {(w.Time.Ms - Start.Ms) / 60_000} min");
        Assert.True(gone != default, "it went down");
        Assert.True(w.Ships.Store.SlotOf(1) < 0, "dropped");
        var p = w.Sightings.Of(1)!;
        int ci = p.IndexOf(1);
        Assert.True(ci < 0 || (p[ci].Seen & SeenFlags.Gone) != 0, "seen to go");
    }

    [Fact]
    public void Damage_shows_in_the_snapshot_and_a_jammed_turret_keeps_its_bearing()
    {
        var (w, c) = Ship("super_dreadnought");
        var p = Earth.Position(56, 4);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 14000);
        var (lat, lon) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), lat, lon, 0, 0, side: 1));
        w.Apply(GunnerySystem.Engage(1, 0, 2));
        w.AdvanceTo(w.Time + 20_000);
        int slot = w.Ships.Store.SlotOf(1);
        double was = w.Ships.Store[slot].MountBearingDeg(0);
        w.Apply(DamageSystem.Jam(1, 0));
        w.Apply(DamageSystem.WreckKind(1, ComponentKind.Funnel, 0));
        w.Apply(DamageSystem.SideHole(1, HoleAt.Stern, 0, -1, 4));
        w.Apply(DamageSystem.Burn(1, 10));
        w.Apply(ShipSystem.Course(2, 90));   // the target moves on: the others train after it
        w.AdvanceTo(w.Time + 5 * 60_000);
        Assert.Equal(was, w.Ships.Store[slot].MountBearingDeg(0), 6);
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        var v = snap.Ships;
        int k = v.IndexOf(1);
        var parts = Enumerable.Range(v.PartStart[k], v.PartCount[k]).Select(j => (c.Components[v.Parts[j]].Kind, v.PartStates[j], v.PartFlags[j])).ToList();
        output.WriteLine($"attitude {v.Attitude[k]}, parts: {string.Join(", ", parts)}, fires {v.FireCount[k]}");
        Assert.Contains(parts, x => x.Kind == ComponentKind.Funnel && x.Item2 == PartState.Destroyed);
        Assert.Contains(parts, x => x.Kind == ComponentKind.Main && (x.Item3 & PartFlags.Jammed) != 0);
        Assert.True(v.Attitude[k].TrimDeg < 0, "down by the stern");
    }
}
