using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>One near field (docs/battle/explosions.md EX4): one reach and one hole, whatever set the charge off, its plate, its
/// distance and the water backing it deciding.</summary>
public class NearFieldTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static (ShipClass C, ShipCondition K) Ship(string design)
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 3, Settings);
        var c = Fleet.Get(design).Class!;
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        return (c, w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1)));
    }

    static ShellTypeRow Filler(double kg)
    {
        var x = Explosives.Get("TNT")!;
        return new ShellTypeRow { FillerKg = kg, FillerRe = x.Re ?? 1, FillerQDet = x.QDet ?? 4.5, FillerQComb = x.QComb ?? 15, FillerAirM3Kg = x.AirM3Kg ?? 0, FillerEta = x.Eta ?? 1 };
    }

    static int EngineRoom(ShipClass c) =>
        Enumerable.Range(0, c.Cells.Length).Where(i => c.Cells[i].Room >= 0 && c.Rooms[c.Cells[i].Room].Kind == RoomKind.EngineRoom)
            .OrderBy(i => Math.Abs((c.Cells[i].Box.X0 + c.Cells[i].Box.X1) / 2 - c.Hydrostatics.Lcf)).ThenByDescending(i => c.Cells[i].VolumeM3).First();

    static P3 Middle(in Box b) => new((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, (b.Base + b.Top) / 2);

    /// <summary>The two measured ends of the one rule: 11 §3.1's contact hole in air, the torpedo's 1.4·W^⅔ m² and 0.7·W^⅓ under water.</summary>
    [Fact]
    public void Air_and_water_are_the_two_ends_of_one_rule()
    {
        const double size = 4;
        Assert.Equal(0.45 * size * Math.Sqrt(6.0 / 16), NearField.HoleM(size, 16, 0, Settings), 9);
        double tamped = NearField.HoleM(size, 16, 1, Settings);
        Assert.Equal(1.4 * size * size, Math.PI * tamped * tamped, 1);
        Assert.Equal(tamped, NearField.HoleM(size, 300, 1, Settings), 9);
        Assert.Equal(1.0 * size, NearField.Charge(size, 1, 0, Settings).ReachM, 9);
        Assert.Equal(0.7 * size, NearField.Charge(size, 1, 1, Settings).ReachM, 9);
    }

    /// <summary>No step from air to water: a charge going down reads a little more each metre, and it's whole by the settings' depth.</summary>
    [Fact]
    public void Going_deeper_has_no_step()
    {
        const double size = 6;
        double last = NearField.HoleM(size, 16, NearField.Tamp(0, size, Settings), Settings);
        for (double depth = 0.1; depth < 5; depth += 0.1)
        {
            double hole = NearField.HoleM(size, 16, NearField.Tamp(depth, size, Settings), Settings);
            Assert.InRange(hole - last, 0, 0.15);
            last = hole;
        }
        Assert.Equal(1, NearField.Tamp(Settings.TampDepthK * size, size, Settings));
        Assert.Equal(0, NearField.Tamp(-1, size, Settings));
    }

    /// <summary>Off the plate it fades to nothing with no step, and the thicker the plate the nearer it must be (Marblehead's 76 mm
    /// ruptured only in contact, 11 §3.2).</summary>
    [Fact]
    public void A_thick_plate_is_holed_only_close_to()
    {
        var ch = NearField.Charge(4, 1, 0, Settings);
        double last = double.MaxValue;
        for (double d = 0; d < ch.ReachM + 0.5; d += 0.1)
        {
            double hole = NearField.HoleM(ch, 8, d, Settings);
            Assert.True(hole <= last);
            last = hole;
        }
        Assert.Equal(0, last);
        Assert.True(NearField.HoleM(ch, 76, 0, Settings) > 0, "in contact");
        Assert.Equal(0, NearField.HoleM(ch, 76, 2, Settings));
        Assert.True(NearField.HoleM(ch, 8, 2, Settings) > 0, "a thin one at the same distance");
    }

    /// <summary>However a plate is split between spaces, it's holed once.</summary>
    [Fact]
    public void A_plate_split_between_spaces_is_holed_once()
    {
        const double r = 1.3;
        double whole = NearField.Disc(r, 5, 2, 0, 10, 0, 4);
        Assert.Equal(Math.PI * r * r, whole, 9);
        double split = NearField.Disc(r, 5, 2, 0, 4.6, 0, 4) + NearField.Disc(r, 5, 2, 4.6, 10, 0, 4);
        Assert.Equal(whole, split, 9);
        Assert.Equal(0, NearField.Disc(r, 5, 2, 7, 10, 0, 4));
    }

    /// <summary>A burst near her side holes it: the sea's way in and out of her section, both from the one hole; none in mid-ship in a
    /// big ship, where nothing's within the plate's share of the reach.</summary>
    [Fact]
    public void A_burst_close_to_her_side_holes_it_and_her_section()
    {
        var (c, k) = Ship("destroyer");
        var f = Filler(60);
        int cell = -1;
        for (int i = 0; i < c.Cells.Length && cell < 0; i++)
            if (k.Faces.Area[i * 4 + 1] > 20 && c.Cells[i].Box.Base > c.Vertical.Waterline)
                cell = i;
        Assert.True(cell >= 0);
        var b = c.Cells[cell].Box;
        var at = new P3((b.X0 + b.X1) / 2, k.Faces.Point[cell * 4 + 1].Y - 0.5, (b.Base + b.Top) / 2);
        var r = RandomStream.Named(5, "near field");
        var res = GasNetwork.For(c).Detonate(k, cell, at, 60, f, NearField.Charge(Math.Cbrt(60.0), 1, 0, Settings), Settings, ref r);
        output.WriteLine($"{c.CellId(cell)}: holed {res.Holed}, {res.HoleM2:0.0} m² to the sea or air, side torn {k.FaceTornM2[cell * 4 + 1]:0.0} m², lost {k.FaceLost[cell * 4 + 1]:0.00}");
        Assert.True(k.FaceTornM2[cell * 4 + 1] > 1);
        Assert.True(k.FaceLost[cell * 4 + 1] > 0);
        Assert.True(k.BreachM2[cell] >= k.FaceTornM2[cell * 4 + 1], "the same hole lets the sea in");
    }

    /// <summary>A big charge's near field goes through what it holes, past the next space, where a small one stays home.</summary>
    [Fact]
    public void A_big_charge_goes_on_through_what_it_opens()
    {
        var (c, k) = Ship("destroyer");
        int cell = EngineRoom(c);
        var r = RandomStream.Named(5, "near field");
        var net = GasNetwork.For(c);
        var small = net.Detonate(k, cell, Middle(c.Cells[cell].Box), 3, Filler(3), NearField.Charge(Math.Cbrt(3.0), 1, 0, Settings), Settings, ref r);
        (c, k) = Ship("destroyer");
        var big = GasNetwork.For(c).Detonate(k, cell, Middle(c.Cells[cell].Box), 2400, Filler(2400), NearField.Charge(Math.Cbrt(2400.0), 1, 0, Settings), Settings, ref r);
        output.WriteLine($"3 kg: holed {small.Holed}, 2.4 t: holed {big.Holed}");
        Assert.True(big.Holed > 3 * Math.Max(small.Holed, 1));
    }

    /// <summary>What a plate holds back stays behind it: a charge against an armoured deck's plate doesn't reach what lies beyond.</summary>
    [Fact]
    public void What_a_plate_holds_back_stays_behind_it()
    {
        var (c, k) = Ship("destroyer");
        int cell = EngineRoom(c);
        var r = RandomStream.Named(5, "near field");
        var weak = new NearCharge(0.5, 0, 6);   // a far reach and a small hole: thin plate goes, and it walks
        var far = GasNetwork.For(c).Detonate(k, cell, Middle(c.Cells[cell].Box), 0.1, Filler(0.1), weak, Settings, ref r);
        (c, k) = Ship("destroyer");
        var none = GasNetwork.For(c).Detonate(k, cell, Middle(c.Cells[cell].Box), 0.1, Filler(0.1), default, Settings, ref r);
        output.WriteLine($"holed {far.Holed} with a near field, {none.Holed} without");
        Assert.Equal(0, none.Holed);
        Assert.True(far.Holed > 0);
    }

    /// <summary>A flooded space backs a charge in it, by its depth under the water there.</summary>
    [Fact]
    public void Water_in_a_space_backs_a_charge_in_it()
    {
        var (c, k) = Ship("destroyer");
        int cell = EngineRoom(c);
        var b = c.Cells[cell].Box;
        var low = new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, b.Base + 0.1);
        Assert.Equal(0, NearField.TampIn(c, k, cell, low, 1, Settings));
        k.WaterM3[cell] = c.Cells[cell].VolumeM3 * Math.Clamp(c.Cells[cell].Permeability, 0.05, 1);
        Assert.True(NearField.TampIn(c, k, cell, low, 1, Settings) > 0.5);
    }

    /// <summary>A hole by a torpedo or a near miss is in her section as a burst's is.</summary>
    [Fact]
    public void A_punched_hole_counts_against_her_section()
    {
        var (c, k) = Ship("destroyer");
        int cell = -1;
        for (int i = 0; i < c.Cells.Length && cell < 0; i++)
            if (k.Faces.Area[i * 4] > 20)
                cell = i;
        var p = k.Faces.Point[cell * 4];
        NearField.Punch(c, k, cell, 5, p, Settings);
        Assert.Equal(5, k.FaceTornM2[cell * 4], 9);
        Assert.Equal(Blast.HoleShare(5, k.Faces.Area[cell * 4]), k.FaceLost[cell * 4], 9);
        Assert.True(k.GirderStale);
        Assert.Equal(5, k.BreachM2[cell], 9);
    }

    /// <summary>A Tallboy's charge in a destroyer leaves nothing of her section, whatever vents it: the shortcut for an open space
    /// doesn't count the holes the charge makes (the gas goes out by them, in the run), and she breaks.</summary>
    [Fact]
    public void A_tallboys_charge_breaks_a_destroyer()
    {
        var (c, k) = Ship("destroyer");
        int cell = EngineRoom(c);
        var r = RandomStream.Named(5, "near field");
        var g = GasNetwork.For(c).Detonate(k, cell, Middle(c.Cells[cell].Box), 2400, Filler(2400), NearField.Charge(Math.Cbrt(2400.0), 1, 0, Settings), Settings, ref r);
        var h = HullGirder.Of(c);
        var calm = h.Read(k, 0, h.Whip(g.WhipEM, Settings.GirderWhipK), Settings);
        output.WriteLine($"peak {g.PeakKPa:0} kPa, holed {g.Holed}, section lost {calm.D:0.00}, load {calm.Load:0.00}");
        Assert.False(g.Open);
        Assert.True(g.WhipEM > 0);
        Assert.True(calm.Broken);
    }
}
