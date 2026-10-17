using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A burst in a closed space (docs/battle/shells-armour-damage.md E5; damage/11 §5.5's tests, wide bands): the
/// destroyer's gradient (a 5" nothing, an 8" its neighbours, a bomb the next bulkheads and decks), a 16" AP in a small
/// space and in an engine room, the open deck, black powder against TNT, and no garbage.</summary>
public class BlastTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShellTypeRow Charge(double kg, string explosive)
    {
        var x = Explosives.Get(explosive)!;
        return new ShellTypeRow
        {
            FillerKg = kg, FillerRe = x.Re ?? 1, FillerQDet = x.QDet ?? 4.5, FillerQComb = x.QComb ?? 15, FillerAirM3Kg = x.AirM3Kg ?? 0, FillerEta = x.Eta ?? 1,
        };
    }

    /// <summary>A ship of a design in a world of its own, and its condition.</summary>
    static (ShipClass C, ShipCondition K) Ship(string design)
    {
        var w = new World(Start, 3, Settings);
        var c = Fleet.Get(design).Class!;
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        return (c, w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1)));
    }

    /// <summary>The largest cell of the room of a kind nearest amidships; or the cell holding a room whose gas is nearest a
    /// volume.</summary>
    static int Cell(ShipClass c, ShipCondition k, RoomKind kind)
    {
        int room = Enumerable.Range(0, c.Rooms.Length).Where(i => c.Rooms[i].Kind == kind)
            .OrderBy(i => Math.Abs((c.Rooms[i].X0 + c.Rooms[i].X1) / 2 - c.Hydrostatics.Lcf)).First();
        int best = -1;
        foreach (int ci in c.Links(c.Rooms[room].Cells))
            if (best < 0 || c.Cells[ci].VolumeM3 > c.Cells[best].VolumeM3)
                best = ci;
        return best;
    }

    /// <summary>The space (a cell and those joined to it open) of stores or quarters whose gas is nearest a volume.</summary>
    static int CellOf(ShipClass c, ShipCondition k, double gasM3) =>
        Enumerable.Range(0, c.Cells.Length).Where(i => c.Cells[i].Room >= 0 && c.Rooms[c.Cells[i].Room].Kind is RoomKind.Stores or RoomKind.Accommodation)
            .OrderBy(i => Math.Abs(SpaceM3(c, k, i) - gasM3)).First();

    static double SpaceM3(ShipClass c, ShipCondition k, int cell)
    {
        var seen = new HashSet<int> { cell };
        var todo = new Stack<int>([cell]);
        double v = 0;
        while (todo.Count > 0)
        {
            int i = todo.Pop();
            v += Blast.GasM3(c, k, i);
            foreach (var e in c.Neighbours(c.Cells[i].Neighbours))
                if (e.Join == JoinKind.Open && seen.Add(e.Cell))
                    todo.Push(e.Cell);
        }
        return v;
    }

    BlastResult Burst(ShipClass c, ShipCondition k, int cell, double kg, string explosive, string what)
    {
        var f = Charge(kg, explosive);
        var b = c.Cells[cell].Box;
        double size = Math.Cbrt(kg * f.FillerRe);
        var r = RandomStream.Named(11, "blast tests");
        var res = Blast.Cascade(c, k, cell, new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, (b.Base + b.Top) / 2), kg, f, 0, Settings.WreckRadiusK * size,
            Settings.NearHoleK * size, Settings, ref r);
        output.WriteLine($"{what}: {c.CellId(cell)} ({Blast.GasM3(c, k, cell):0} m³): P {res.FirstMPa:0.000} MPa, felt {res.FeltMPa:0.000}, {res.Spaces} spaces " +
                         $"({res.VolumeM3:0} m³), ruptured {res.Ruptured}, doors {res.Doors}, deformed {res.Deformed}, opened {res.OpenM2:0.0} m², to the sea or air {res.HoleM2:0.00} m²");
        return res;
    }

    /// <summary>11 §5.5 test 1: a 5" HE burst in a destroyer compartment breaches no watertight bulkhead and blows no door.</summary>
    [Fact]
    public void A_5in_burst_in_a_destroyer_compartment_breaches_nothing()
    {
        var (c, k) = Ship("destroyer");
        var res = Burst(c, k, Cell(c, k, RoomKind.Accommodation), 3.3, "Explosive D", "5\" AAC");
        Assert.Equal(0, res.Ruptured);
        Assert.Equal(0, res.Doors);
        Assert.InRange(res.FeltMPa, 0.05, 0.15);
    }

    /// <summary>11 §5.5 test 2: an 8" HE in the same space opens its way into the next spaces (its doors blown or the near
    /// zone holing the bulkheads) and loads it past what disables most people (0.12 MPa, Bowen's 50%).</summary>
    [Fact]
    public void An_8in_burst_opens_the_next_spaces_and_disables_the_crew()
    {
        var (c, k) = Ship("destroyer");
        var res = Burst(c, k, Cell(c, k, RoomKind.Accommodation), 9.7, "Explosive D", "8\" HC");
        Assert.True(res.Spaces >= 3, $"{res.Spaces} spaces");
        Assert.True(res.FeltMPa >= 0.12, $"{res.FeltMPa:0.000} MPa");
    }

    /// <summary>11 §5.5 test 3: a 250 kg SAP bomb (60 kg TNT) in a destroyer carries away the next bulkheads and decks, and
    /// little beyond about 6 m: the gas goes on through blown doors (the cascade errs that way, 11 §6), but what it reaches
    /// beyond 10 m of the burst space feels less than tears a bulkhead (5 mm: 0.3 MPa).</summary>
    [Fact]
    public void A_bomb_carries_away_the_next_bulkheads_and_little_beyond()
    {
        var (c, k) = Ship("destroyer");
        int cell = Cell(c, k, RoomKind.Accommodation);
        var res = Burst(c, k, cell, 60, "TNT", "250 kg SAP");
        Assert.True(res.Ruptured >= 2, $"{res.Ruptured} ruptured");
        var b = c.Cells[cell].Box;
        double farFelt = 0, far = 0;
        for (int i = 0; i < res.Spaces; i++)
        {
            var o = c.Cells[k.BlastRegion[i]].Box;
            double d = Math.Max(o.X0 - b.X1, b.X0 - o.X1);
            far = Math.Max(far, d);
            if (d > 10)
                farFelt = Math.Max(farFelt, k.BlastFelt[k.BlastRegion[i]]);
        }
        output.WriteLine($"  the farthest space reached starts {far:0.0} m from the burst space; beyond 10 m they felt at most {farFelt:0.000} MPa");
        Assert.True(farFelt < 0.3, $"{farFelt:0.000} MPa");
    }

    /// <summary>11 §5.5 test 4: a 16" AP burst (18.6 kg Explosive D) in a ~150 m³ space disables nearly all its crew (felt
    /// well past 0.12 MPa); in a battleship's engine room it does little beyond its near zone and kills nobody by blast
    /// (under 0.1 MPa, nothing ruptured).</summary>
    [Fact]
    public void A_16in_ap_burst_disables_a_small_space_and_hardly_a_big_engine_room()
    {
        var (c, k) = Ship("battleship");
        var small = Burst(c, k, CellOf(c, k, 150), 18.6, "Explosive D", "16\" AP in ~150 m³");
        Assert.True(small.FeltMPa >= 0.25, $"{small.FeltMPa:0.000} MPa");
        var (c2, k2) = Ship("battleship");
        var engine = Burst(c2, k2, Cell(c2, k2, RoomKind.EngineRoom), 18.6, "Explosive D", "16\" AP in the engine room");
        Assert.True(engine.FeltMPa < 0.1, $"{engine.FeltMPa:0.000} MPa");
        Assert.Equal(0, engine.Ruptured);
    }

    /// <summary>11 §5.5 test 5: the same 16" AP on an open deck wrecks to about 2.5 m (the near zone, 1.0·W^⅓), nothing
    /// beyond its fragments.</summary>
    [Fact]
    public void A_16in_ap_on_an_open_deck_wrecks_to_about_2_5_m()
    {
        double rw = Settings.WreckRadiusK * Math.Cbrt(18.6 * Explosives.Get("Explosive D")!.Re!.Value);
        output.WriteLine($"wreck radius {rw:0.00} m");
        Assert.InRange(rw, 2.2, 3.0);
    }

    /// <summary>11 §5.5 test 6: black powder gives a sixth to an eighth of the same mass of TNT's gas pressure (wider here:
    /// a fifth to a ninth, as the air in the space allows TNT's afterburn).</summary>
    [Fact]
    public void Black_powder_is_weak_inside()
    {
        foreach (double v in new[] { 20.0, 100, 1000 })
        {
            double bp = Blast.Pressure(Charge(10, "Black powder"), 10, v, Settings), tnt = Blast.Pressure(Charge(10, "TNT"), 10, v, Settings);
            output.WriteLine($"10 kg in {v} m³: black powder {bp:0.000} MPa, TNT {tnt:0.000} ({bp / tnt:0.000})");
            Assert.InRange(bp / tnt, 1 / 9.5, 1 / 5.0);
        }
        // 11 §2.1: TNT against DDESB's fully confined 0.21 MPa at 0.0497 kg/m³
        Assert.InRange(Blast.Pressure(Charge(1, "TNT"), 1, 1 / 0.0497, Settings), 0.17, 0.23);
    }

    /// <summary>11 §5.5 test 7: no allocation per burst.</summary>
    [Fact]
    public void A_cascade_allocates_nothing()
    {
        var (c, k) = Ship("destroyer");
        int cell = Cell(c, k, RoomKind.Accommodation);
        var f = Charge(9.7, "Explosive D");
        Blast.Prepare(c, k);
        var r = RandomStream.Named(1, "alloc");
        var at = new P3(0, 0, -1);
        Blast.Cascade(c, k, cell, at, 9.7, f, 0, 2, 1, Settings, ref r);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
            Blast.Cascade(c, k, cell, at, 9.7, f, 0, 2, 1, Settings, ref r);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
