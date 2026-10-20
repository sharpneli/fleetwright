using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Leaks with structure (docs/battle/damage-control.md DC1; damage/17 §4-§7, 09 §2.2-§2.4): a boundary's closures
/// loosening by its load, a sprung seam opening with the head and working open (the same at a 1 s and a 15 s step), the
/// rating and its cuts, the ram head, and a watertight door left open.</summary>
public class LeakTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    static ShipCondition Fresh(ShipClass c)
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 3, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        return w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1));
    }

    /// <summary>The first edge between a boiler room's and an engine room's cells (a main machinery bulkhead), from the
    /// boiler room's side, and its face.</summary>
    static (int From, int Pool, double Area) MachineryBulkhead(ShipClass c)
    {
        var cells = c.Cells;
        for (int i = 0; i < cells.Length; i++)
            if (c.Rooms[cells[i].Room].Kind == RoomKind.BoilerRoom)
                for (int j = cells[i].Neighbours.Start; j < cells[i].Neighbours.End; j++)
                {
                    ref readonly var e = ref c.Neighbour(j);
                    if (e.Join == JoinKind.Bulkhead && c.Rooms[cells[e.Cell].Room].Kind == RoomKind.EngineRoom)
                        return (i, j, Blast.Area(cells[i].Box, cells[e.Cell].Box, e.Join));
                }
        throw new InvalidOperationException($"no boiler room beside an engine room in {c.Name}");
    }

    [Fact]
    public void A_machinery_bulkheads_closures_loosen_more_the_harder_it_was_loaded()
    {
        var c = Class("super_dreadnought");
        var (from, pool, area) = MachineryBulkhead(c);
        Span<int> n = stackalloc int[7];
        Leaks.Count(c, from, pool, area, n, Settings);
        output.WriteLine($"{area:0} m²: doors {n[0]}, hatches {n[1]}, tubes {n[2]}, vents {n[3]}, rods {n[4]}, glands {n[5]}, flanges {n[6]}");
        Assert.True(n[(int)Closure.StuffingTube] >= 5, "a machinery bulkhead is full of cables");
        double Mean(double d)
        {
            var r = RandomStream.FromSeed(7);
            double sum = 0;
            for (int run = 0; run < 400; run++)
            {
                var k = Fresh(c);
                sum += Leaks.Sprung(c, k, from, pool, 0, d, area, Settings, ref r);
            }
            return sum / 400 * 1e4;
        }
        double d02 = Mean(0.2), d05 = Mean(0.5), d1 = Mean(1), d2 = Mean(2), d3 = Mean(3);
        output.WriteLine($"closures' leak, cm², mean: D 0.2 {d02:0.0}, 0.5 {d05:0.0}, 1 {d1:0.0}, 2 {d2:0.0}, 3 {d3:0.0} (17 §4's 150 m² bulkhead: 22, 42, 80, 110)");
        Assert.Equal(0, d02);
        Assert.True(d05 < d1 && d1 < d2 && d2 <= d3 * 1.05);
        Assert.InRange(d1, 5, 150);
    }

    [Fact]
    public void The_rating_falls_with_the_load_that_sprung_it_and_rises_with_shores()
    {
        var c = Class("super_dreadnought");
        var f = new FloodModel(c);
        var k = Fresh(c);
        int edge = Array.FindIndex(f.Edges, e => e.Join == JoinKind.Bulkhead);
        int p = f.Edges[edge].Pool;
        double intact = Flooding.Rating(f, k, edge, p, Settings);
        Assert.Equal(Settings.FloodRatingK * f.BuiltM[edge], intact, 9);
        k.JoinD[p] = 2;
        Assert.Equal(intact * 0.8, Flooding.Rating(f, k, edge, p, Settings), 9);
        k.JoinD[p] = 9;
        Assert.Equal(intact * Settings.FloodRatingSprungMin, Flooding.Rating(f, k, edge, p, Settings), 9);
        k.JoinMarks[p] |= JoinMark.Shored;
        Assert.Equal(intact * Settings.FloodRatingSprungMin * Settings.ShoreRatingK, Flooding.Rating(f, k, edge, p, Settings), 9);
        Assert.True(f.BuiltM.Where((b, i) => f.Edges[i].Join != JoinKind.Open).All(b => b >= 0.5 && !double.IsInfinity(b)));
    }

    [Fact]
    public void The_ram_head_is_09s_at_the_stem_and_nothing_aft_or_astern()
    {
        var c = Class("lion");
        double v = 28 * 0.514444, stem = Flooding.Ram(c, c.Hull.X1, v, Settings);
        output.WriteLine($"28 kn: stem {stem:0.00} m, 12 kn {Flooding.Ram(c, c.Hull.X1, 12 * 0.514444, Settings):0.00} m");
        Assert.InRange(stem, 5.0, 5.6);   // 09 §2.2: ~5.3 m
        Assert.Equal(0, Flooding.Ram(c, c.Hull.X1 - 0.3 * c.Length, v, Settings));
        Assert.Equal(0, Flooding.Ram(c, c.Hull.X1, -v, Settings));
    }

    /// <summary>A cell full to its ceiling beside a dry one across an edge, stepped for a while; the water that crossed.</summary>
    static double Crossed(ShipClass c, int edge, Action<ShipCondition, int> set, double seconds, double dt)
    {
        var f = new FloodModel(c);
        var k = Fresh(c);
        var e = f.Edges[edge];
        set(k, e.Pool);
        int hi = c.Cells[e.A].Box.Base >= c.Cells[e.B].Box.Base ? e.A : e.B, lo = hi == e.A ? e.B : e.A;
        k.WaterM3[hi] = f.Capacity[hi];
        var r = RandomStream.FromSeed(11);
        for (double t = 0; t < seconds; t += dt)
        {
            Flooding.Step(c, f, k, dt, 0, 0, Settings, ref r);
            k.WaterM3[hi] = f.Capacity[hi];   // kept full, as a holed space is
        }
        return k.WaterM3[lo];
    }

    /// <summary>An edge between two cells over the waterline side by side across a main transverse bulkhead with a door.</summary>
    static int DoorEdge(ShipClass c, FloodModel f) =>
        Array.FindIndex(f.Edges, e => e.Join == JoinKind.Bulkhead && Leaks.HasDoor(c, c.Neighbour(e.Pool), e.A) &&
                                      Math.Abs(c.Cells[e.A].Box.Base - c.Cells[e.B].Box.Base) < 0.1);

    [Fact]
    public void A_watertight_door_left_open_floods_the_next_space_in_minutes()
    {
        var c = Class("super_dreadnought");
        int edge = DoorEdge(c, new FloodModel(c));
        Assert.True(edge >= 0);
        double shut = Crossed(c, edge, (k, p) => { }, 600, 1), open = Crossed(c, edge, (k, p) => k.JoinMarks[p] |= JoinMark.DoorOpen, 600, 1);
        output.WriteLine($"10 min across a watertight bulkhead: door shut {shut:0.0} m³, open {open:0} m³");
        Assert.True(open > 100 * Math.Max(shut, 0.01));
    }

    [Fact]
    public void A_sprung_seam_works_open_the_same_at_a_1_s_and_a_15_s_step()
    {
        var c = Class("super_dreadnought");
        int edge = DoorEdge(c, new FloodModel(c));
        void Sprung(ShipCondition k, int p)
        {
            (k.JoinSeamM2[p], k.JoinD[p], k.JoinWork[p], k.JoinWorkAt[p]) = (0.002, 2, 1, 0.05);   // 20 cm², the working's first step early
        }
        double one = Crossed(c, edge, Sprung, 4 * 3600, 1), fifteen = Crossed(c, edge, Sprung, 4 * 3600, 15);
        double plain = Crossed(c, edge, (k, p) => (k.JoinSeamM2[p], k.JoinD[p]) = (0.002, 2), 4 * 3600, 1);
        output.WriteLine($"4 h through 20 cm² of sprung seams: {one:0} m³ at 1 s, {fifteen:0} m³ at 15 s; never working {plain:0} m³");
        Assert.InRange(fifteen, one * 0.9, one * 1.1);
        Assert.True(one > plain, "worked open, it passes more");
    }
}
