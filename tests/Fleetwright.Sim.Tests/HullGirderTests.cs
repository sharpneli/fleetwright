using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit;

namespace Fleetwright.Sim.Tests;

/// <summary>The hull as a beam (docs/battle/explosions.md EX3): the section built from the boundaries the gas tears, against
/// shipgen's own sizing, and how its readings go with what's lost.</summary>
public class HullGirderTests
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static string DesignPath(string name)
    {
        string shipped = Path.Combine(Fleet.Shipgen, "designs", name + ".json");
        return File.Exists(shipped) ? shipped : Path.Combine(Fleet.Repo, "Content", "Scenarios", "Designs", name + ".json");
    }

    static ShipClass Class(string name) =>
        ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(DesignPath(name))));

    [Theory]
    [InlineData("destroyer")]
    [InlineData("iron_duke")]
    [InlineData("lion")]
    public void The_section_is_what_shipgen_sized(string name)
    {
        var built = ShipDesign.Build(Design.Load(DesignPath(name)));
        var h = new HullGirder(ShipClassCompiler.Compile(built));
        var g = built.Report.Hull.Girder!;
        Assert.InRange(h.MidIM4 / (g.PlatingM4 + g.ArmourDecksM4), 0.7, 1.3);
    }

    [Fact]
    public void A_side_belongs_to_one_space_at_a_height()
    {
        var over = new List<string>();
        foreach (var b in Fleet.All.Where(b => b.Class is not null))
        {
            var c = b.Class!;
            var hull = HullFaces.Of(c);
            for (int sec = 0; sec < c.Sections.Length; sec++)
                for (int f = 0; f < 2; f++)
                {
                    double sum = 0, top = double.NegativeInfinity, bottom = double.PositiveInfinity;
                    for (int i = 0; i < c.Cells.Length; i++)
                        if (c.Cells[i].Section == sec && hull.Area[i * 4 + f] > 0)
                        {
                            var box = c.Cells[i].Box;
                            sum += hull.Area[i * 4 + f] / (box.X1 - box.X0);
                            (top, bottom) = (Math.Max(top, box.Top), Math.Min(bottom, box.Base));
                        }
                    if (sum > 0 && sum > top - bottom + 0.1)
                        over.Add($"{b.Case.Name} section {sec}: {sum:0.0} m of side in {top - bottom:0.0} m");
                }
        }
        Assert.True(over.Count == 0, $"{over.Count}: " + string.Join("; ", over.Take(12)));
    }

    [Fact]
    public void Intact_she_stands_in_a_seaway()
    {
        foreach (var name in new[] { "destroyer", "v25", "lion", "iron_duke", "dreadnought", "yamato", "littorio", "bismarck", "kgv", "battleship" })
        {
            var r = new HullGirder(Class(name)).Read(default, default, null, 1, 0, Settings);
            Assert.True(r.Load < 1, $"{name}: {r.Load:0.00} at {r.Station:0.00}");
            Assert.True(r.EndLoad < 1, $"{name}: her end, {r.EndLoad:0.00} at {r.EndStation:0.00}");
        }
    }

    [Fact]
    public void A_section_gone_is_a_higher_load_than_a_crack()
    {
        var c = Class("destroyer");
        var h = new HullGirder(c);
        var intact = h.Read(default, default, null, 0, 0, Settings);
        var faces = new double[c.Cells.Length * 4];
        for (int i = 0; i < c.Cells.Length; i++)
            if (c.Cells[i].Section == intact.Section)
                faces[i * 4 + 2] = 0.2;   // a crack in her weather deck
        var cracked = h.Read(default, faces, null, 0, 0, Settings);
        for (int i = 0; i < c.Cells.Length; i++)
            if (c.Cells[i].Section == intact.Section)
                faces[i * 4 + 2] = 1;   // all of it
        var gone = h.Read(default, faces, null, 0, 0, Settings);
        Assert.True(cracked.Load > intact.Load);
        Assert.True(gone.Load > cracked.Load);
        Assert.True(gone.D > cracked.D);
    }

    [Fact]
    public void The_same_shell_whips_a_small_hull_harder_and_cordite_harder_than_a_shell()
    {
        var destroyer = new HullGirder(Class("destroyer"));
        var lion = new HullGirder(Class("lion"));
        double shell = 270e6 * 62;              // a 46 cm HE's burst: its joules × its kg
        double cordite = 10e9 * 20_000;         // 20 t of cordite in a second
        Assert.True(destroyer.Whip(shell, 0.2) > lion.Whip(shell, 0.2));
        Assert.InRange(lion.Whip(cordite, 0.2), 0.1, 0.4);        // a fraction of a design moment (07 §3.1 has it a spike of that order)
        Assert.InRange(destroyer.Whip(shell, 0.2), 0.02, 0.2);    // a shell in the air hardly whips her
    }

    [Fact]
    public void A_sliver_of_keel_does_not_set_her_capacity_and_losing_plate_never_raises_it()
    {
        var c = Class("yamato");
        var h = new HullGirder(c);
        var r = h.Read(default, default, null, 1, 0, Settings);
        Assert.True(r.Load < 1, $"intact in a seaway: {r.Load:0.00} at {r.Station:0.00}");
        // taking any one outside plate of her worst section out, whichever, leaves her no stronger
        for (int i = 0; i < c.Cells.Length; i++)
        {
            if (c.Cells[i].Section != r.Section)
                continue;
            for (int f = 0; f < 4; f++)
            {
                var faces = new double[c.Cells.Length * 4];
                faces[i * 4 + f] = 1;
                var less = h.Read(default, faces, null, 1, 0, Settings);
                Assert.True(less.Load >= r.Load - 0.03 * r.Load, $"cell {i} face {f}: {less.Load:0.000} against {r.Load:0.000}");
            }
        }
    }

    [Fact]
    public void Flooding_her_ends_hogs_her_and_her_middle_sags_her()
    {
        var c = Class("destroyer");
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 50, -20, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        var h = new HullGirder(c);
        var dry = h.Read(default, default, null, 0, 0, Settings);

        // water amidships eases her still-water hog at first: the middle's load falls
        var sections = c.Sections;
        int mid = dry.Section, ends = 0;
        double t = 0;
        for (int i = 0; i < c.Cells.Length; i++)
            if (c.Cells[i].Section == mid)
            {
                k.WaterM3[i] = 0.4 * c.Cells[i].VolumeM3;
                t += k.WaterM3[i] * FloodModel.SeaT;
            }
        k.WaterT = t;
        var sag = h.Read(default, default, k, 0, 0, Settings);
        Assert.True(sag.Load < dry.Load, $"water amidships: {sag.Load:0.000} against {dry.Load:0.000}");

        // the same water in her ends adds to it
        Array.Clear(k.WaterM3);
        t = 0;
        for (int i = 0; i < c.Cells.Length; i++)
            if (c.Cells[i].Section <= 2 || c.Cells[i].Section >= sections.Length - 3)
            {
                k.WaterM3[i] = 0.4 * c.Cells[i].VolumeM3;
                t += k.WaterM3[i] * FloodModel.SeaT;
                ends++;
            }
        k.WaterT = t;
        Assert.True(ends > 0);
        var hog = h.Read(default, default, k, 0, 0, Settings);
        Assert.True(hog.Load > dry.Load, $"water in her ends: {hog.Load:0.000} against {dry.Load:0.000}");
    }

    (ShipClass C, World W, ShipCondition K) Ship(string name)
    {
        var c = Class(name);
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 50, -20, 0, 0));
        return (c, w, w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity)));
    }

    /// <summary>Her end's section all gone: that end comes off, once, everything past the cut open to the sea and what stood there
    /// gone, and she's not broken for it (07 §3.2). What floods past the cut carries nothing across it.</summary>
    [Fact]
    public void An_end_gone_comes_off_and_she_lives()
    {
        var (c, _, k) = Ship("destroyer");
        var h = HullGirder.Of(c);
        double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity;
        foreach (ref readonly var cell in c.Cells)
            (x0, x1) = (Math.Min(x0, cell.Box.X0), Math.Max(x1, cell.Box.X1));
        double cut = x0 + 0.92 * (x1 - x0);
        for (int i = 0; i < c.Cells.Length; i++)
            if ((c.Cells[i].Box.X0 + c.Cells[i].Box.X1) / 2 > cut)
                for (int f = 0; f < 4; f++)
                    k.FaceLost[i * 4 + f] = 1;
        for (int i = 0; i < c.Cells.Length; i++)
            foreach (ref readonly var e in c.Neighbours(c.Cells[i].Neighbours))
                if ((c.Cells[i].Box.X0 + c.Cells[i].Box.X1) / 2 > cut && (c.Cells[e.Cell].Box.X0 + c.Cells[e.Cell].Box.X1) / 2 > cut)
                    k.EdgeLost[Leaks.Entry(c, e.Cell, Pool(c, e.Cell, i))] = 1;
        k.GirderStale = true;
        var r = h.Bear(k, 0, 1000, 1, Settings);
        Assert.True(r.EndOff);
        Assert.False(r.Broken, $"{r.Load:0.00}");
        Assert.True(double.IsFinite(k.CutBowX));
        Assert.True(k.BreachM2.Sum() > 1);
        var held = k.BreachM2.Sum();
        var again = h.Bear(k, 0, 2000, 1, Settings);
        Assert.False(again.EndOff);
        Assert.Equal(held, k.BreachM2.Sum());
        int part = Enumerable.Range(0, c.Components.Length).FirstOrDefault(j => (c.Components[j].Bounds.X0 + c.Components[j].Bounds.X1) / 2 > k.CutBowX, -1);
        if (part >= 0)
            Assert.Equal(PartState.Destroyed, k.State[part]);

        // the water past the cut adds no moment
        var before = h.Read(k, 0, 0, Settings);
        for (int i = 0; i < c.Cells.Length; i++)
            if ((c.Cells[i].Box.X0 + c.Cells[i].Box.X1) / 2 > k.CutBowX + 3)
                k.WaterM3[i] = c.Cells[i].VolumeM3 * 0.5;
        k.WaterT = 50;
        Assert.Equal(before.Load, h.Read(k, 0, 0, Settings).Load, 3);
    }

    static int Pool(ShipClass c, int from, int to)
    {
        var nb = c.Cells[from].Neighbours;
        for (int j = nb.Start; j < nb.End; j++)
            if (c.Neighbour(j).Cell == to)
                return j;
        return nb.Start;
    }

    /// <summary>The destroyer the plan began with (explosions.md "Why now"): one 46 cm burst inside cuts her section; a shell can break
    /// a ship in a seaway, and in calm water she holds.</summary>
    [Fact]
    public void A_big_shell_inside_a_destroyer_breaks_her_in_a_seaway()
    {
        var (c, _, k) = Ship("destroyer");
        var x = Explosives.Get("TNA")!;
        var shell = new ShellTypeRow { CalibreMm = 460, FillerKg = 61.7, FillerRe = x.Re ?? 1, FillerQDet = x.QDet ?? 4.5, FillerQComb = x.QComb ?? 15, FillerAirM3Kg = x.AirM3Kg ?? 0, FillerEta = x.Eta ?? 1 };
        int room = Enumerable.Range(0, c.Rooms.Length).Where(i => c.Rooms[i].Kind == RoomKind.EngineRoom).OrderBy(i => Math.Abs((c.Rooms[i].X0 + c.Rooms[i].X1) / 2 - c.Hydrostatics.Lcf)).First();
        int cell = -1;
        foreach (int ci in c.Links(c.Rooms[room].Cells))
            if (cell < 0 || c.Cells[ci].VolumeM3 > c.Cells[cell].VolumeM3)
                cell = ci;
        var b = c.Cells[cell].Box;
        double size = Math.Cbrt(61.7 * shell.FillerRe);
        var rng = RandomStream.Named(1, "girder test");
        var g = GasNetwork.For(c).Detonate(k, cell, new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, (b.Base + b.Top) / 2), 61.7, shell, NearField.Charge(size, 1, 0, Settings), Settings, ref rng);
        var h = HullGirder.Of(c);
        var whip = h.Whip(g.WhipEM, Settings.GirderWhipK);
        Assert.False(h.Read(k, 0, whip, Settings).Broken);
        Assert.True(h.Read(k, 1, whip, Settings).Broken);
    }

    /// <summary>Her section cut, the water going in does the rest in time (Bristol, Strong, Beatty: explosions.md): the damage
    /// system's own step reads her girder as she floods, and she's lost broken in two.</summary>
    [Fact]
    public void A_ship_cut_nearly_through_breaks_as_she_floods()
    {
        var (c, w, k) = Ship("destroyer");
        var h = HullGirder.Of(c);
        var intact = h.Read(k, 0, 0, Settings);
        var section = intact.Section;
        // as much of her section gone as she can just stand on in calm water
        double share = 0;
        for (double s = 0.1; s <= 1 && h.Read(k, 0, 0, Settings).Load < 0.8; s += 0.02)
        {
            share = s;
            for (int i = 0; i < c.Cells.Length; i++)
                if (c.Cells[i].Section == section)
                    for (int f = 0; f < 4; f++)
                        k.FaceLost[i * 4 + f] = s;
            k.GirderStale = true;
        }
        var cutDown = h.Read(k, 0, 0, Settings);
        Assert.False(cutDown.Broken, $"standing, in calm water: {cutDown.Load:0.00} with {share:0.00} gone");
        Assert.True(cutDown.Load >= 0.8, $"{cutDown.Load:0.00}");
        // the sea into her ends: a hog on what's left
        for (int i = 0; i < c.Cells.Length; i++)
            if (c.Cells[i].Section <= 2 || c.Cells[i].Section >= c.Sections.Length - 3)
            {
                var b = c.Cells[i].Box;
                if (c.Cells[i].VolumeM3 > 5)
                    k.Hole(i, 0.3, new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, b.Base + 0.1));
            }
        k.Damaged = true;
        w.Damage.Settle(w, w.Ships.NewestEntity, 600);
        Assert.Equal(LossKind.BrokeInTwo, k.Loss);
        // where she broke is kept: her pieces part there (docs/visuals/destruction.md decision 1)
        Assert.True(k.Broken);
        Assert.InRange(k.BreakX, c.Hull.X0, c.Hull.X1);
        Assert.True(k.BreakMs > 0);
    }

    /// <summary>A broken ship's pieces (docs/visuals/destruction.md decision 1): each turns about the break, its broken end going
    /// under first and its far end standing up out of the sea, then all of it gone when she is; the frame's maths round-trips
    /// about the break as about the centre of flotation.</summary>
    [Fact]
    public void A_broken_ships_pieces_go_down_by_their_broken_ends()
    {
        var c = Class("destroyer");
        double at = c.Hydrostatics.Lcf + 5, wl = c.Vertical.Waterline;
        long loss = 0, gone = 300_000;
        foreach (bool bow in new[] { true, false })
        {
            double far = bow ? c.Hull.X1 : c.Hull.X0;
            var early = Attitude.Piece(c, Attitude.Upright, at, bow, loss, gone, 60_000);
            var w = HitQuery.WaterOf(c, early);
            Assert.True(w.At(at, 0) > wl + 1, $"the break under at a minute: sea {w.At(at, 0) - wl:0.0} m over the waterline there");
            Assert.True(w.At(far, 0) < wl - 5, $"the far end standing up at a minute: sea {w.At(far, 0) - wl:0.0} m");
            Assert.Equal(bow, early.TrimDeg < 0);   // bow down is positive: the bow's piece stands bow up
            var end = Attitude.Piece(c, Attitude.Upright, at, bow, loss, gone, gone);
            Assert.True(HitQuery.WaterOf(c, end).At(far, 0) > c.Solids.Bounds.Top, "all of her under when she's gone");
            var frame = ShipFrame.At(new Vec3d(Earth.Radius, 0, 0), new Vec3d(0, 1, 0));
            var p = new P3(far * 0.8, 2, wl + 3);
            var back = HitQuery.ToShip(c, frame, early, HitQuery.ToWorld(c, frame, early, p));
            Assert.True(Math.Abs(back.X - p.X) + Math.Abs(back.Y - p.Y) + Math.Abs(back.Z - p.Z) < 1e-6);
        }
    }

    /// <summary>A charge that tears her open is recorded for the screens (docs/visuals/destruction.md D1): each outside face it tore
    /// stamped with when, and what its gas lifted off her deck wrecked and flagged with the speed it left at.</summary>
    [Fact]
    public void A_big_burst_records_the_plating_it_tore_and_what_it_threw()
    {
        var (c, _, k) = Ship("destroyer");
        var x = Explosives.Get("TNA")!;
        var shell = new ShellTypeRow { CalibreMm = 2000, FillerKg = 8600, FillerRe = x.Re ?? 1, FillerQDet = x.QDet ?? 4.5, FillerQComb = x.QComb ?? 15, FillerAirM3Kg = x.AirM3Kg ?? 0, FillerEta = x.Eta ?? 1 };
        int room = Enumerable.Range(0, c.Rooms.Length).Where(i => c.Rooms[i].Kind == RoomKind.BoilerRoom).OrderBy(i => Math.Abs((c.Rooms[i].X0 + c.Rooms[i].X1) / 2 - c.Hydrostatics.Lcf)).First();
        int cell = c.Links(c.Rooms[room].Cells)[0];
        var b = c.Cells[cell].Box;
        var rng = RandomStream.Named(1, "destruction test");
        var net = GasNetwork.For(c);
        net.Detonate(k, cell, new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, (b.Base + b.Top) / 2), shell.FillerKg, shell,
            NearField.Charge(Math.Cbrt(shell.FillerKg), 1, 0, Settings), Settings, ref rng);
        net.Land(1000, ref rng);   // what it did, on her (the game lands each slice as it goes)
        int torn = 0, thrown = 0;
        for (int f = 0; f < k.FaceTornM2.Length; f++)
        {
            Assert.Equal(k.FaceTornM2[f] > 0, k.FaceTornMs[f] != 0);
            torn += k.FaceTornM2[f] > 0 ? 1 : 0;
        }
        for (int j = 0; j < k.ThrownMs.Length; j++)
            if (k.ThrownMs[j] != 0)
            {
                thrown++;
                Assert.Equal(PartState.Destroyed, k.State[j]);   // lifted off her deck, it's wrecked
                Assert.InRange(k.ThrownV[j], 0, Settings.ThrownMostMs);
            }
        Assert.True(torn >= 4, $"{torn} faces torn");
        var why = new System.Text.StringBuilder();
        for (int f = 2; f < k.FaceTornM2.Length; f += 4)
            if (k.FaceTornM2[f] > 0)
            {
                var cb = c.Cells[f / 4].Box;
                why.Append($" cell {f / 4} x {cb.X0:0}..{cb.X1:0} top {cb.Top:0.0} torn {k.FaceTornM2[f]:0} felt {net.FeltMPa(f / 4):0.000} MPa;");
            }
        for (int j = 0; j < c.Components.Length; j++)
            if (k.State[j] == PartState.Destroyed)
                why.Append($" {c.Components[j].Kind} x {c.Components[j].Bounds.X0:0}..{c.Components[j].Bounds.X1:0} base {c.Components[j].Bounds.Base:0.0};");
        Assert.True(thrown >= 1, $"{thrown} parts thrown:{why}");
    }

    /// <summary>A read allocates nothing, stale or not.</summary>
    [Fact]
    public void A_read_allocates_nothing()
    {
        var (c, _, k) = Ship("lion");
        var h = HullGirder.Of(c);
        k.WaterM3[0] = 10;
        k.WaterT = 10;
        h.Bear(k, 0, 0, 1, Settings);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            k.GirderStale = true;
            h.Bear(k, 1e12, i, 1, Settings);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
