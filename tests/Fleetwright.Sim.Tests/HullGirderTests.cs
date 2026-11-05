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
    public void Intact_she_stands_in_a_seaway()
    {
        foreach (var name in new[] { "destroyer", "v25", "lion", "iron_duke", "dreadnought" })
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
}
