using Fleetwright.Designer;
using Fleetwright.Shipgen;
using G = Fleetwright.Designer.MainArrangement.Group;

namespace Fleetwright.Tests;

/// <summary>The main battery tab's turret edits: they follow the engine's end order and superfiring run, keep every
/// shipped design's arrangement when they write it back, and leave the design valid.</summary>
public class MainArrangementTests
{
    static Design Load(string name) => Design.Load(Paths.Shipgen("designs", name + ".json"));

    static string Ends(Design d) => string.Join(" | ", new[] { G.Fore, G.Aft }.Select(g =>
    {
        var (order, run) = MainArrangement.End(d, g);
        return string.Join(" ", order.Select((k, j) => (char)('A' + k) + (j < run && j > 0 ? "^" : "")));
    }));

    /// <summary>Writing an end's run back as it is changes nothing the engine reads, in every shipped design.</summary>
    [Fact]
    public void SetRun_round_trips_every_design()
    {
        foreach (var p in Directory.GetFiles(Paths.Shipgen("designs"), "*.json"))
        {
            var d = Design.Load(p);
            var back = d;
            foreach (var g in new[] { G.Fore, G.Aft })
                back = MainArrangement.SetRun(back, g, MainArrangement.End(d, g).Run);
            Assert.True(Ends(back) == Ends(d), Path.GetFileName(p));
        }
    }

    [Fact]
    public void Adding_a_rear_turret_superfires()
    {
        var d = Load("lord_nelson");
        Assert.Equal("A | A", Ends(d));
        var x = MainArrangement.Add(d, 0, G.Aft);
        Assert.Equal("A | A A^", Ends(x));
        Assert.Equal("A · X↑ Y", MainArrangement.Positions(x, 0, MainArrangement.Slots(x)));
        Assert.Equal("A | A A", Ends(MainArrangement.Add(d, 0, G.Aft, flush: true)));
        Assert.Empty(ShipDesign.Validate(x, limits: true));
    }

    [Fact]
    public void Steps_stay_one_run()
    {
        var d = Load("bismarck");
        var three = MainArrangement.Add(d, 0, G.Fore);   // A B^ C^
        Assert.Equal("A A^ A^ | A A^", Ends(three));
        Assert.Equal("A A^ A | A A^", Ends(MainArrangement.ToggleStep(three, G.Fore, 2)));
        Assert.Equal("A A A | A A^", Ends(MainArrangement.ToggleStep(three, G.Fore, 1)));   // C comes down with B
        var flat = MainArrangement.ToggleStep(three, G.Fore, 1);
        Assert.Equal("A A^ A^ | A A^", Ends(MainArrangement.ToggleStep(flat, G.Fore, 2)));   // stepping C steps B
    }

    [Fact]
    public void Battery_order_sets_who_is_outboard()
    {
        var d = Load("bismarck");
        d = d with { Main = [.. d.Main!, MainArrangement.Unplaced(d.Main![0]) with { CalibreMm = 150 }] };
        Assert.Equal(2, MainArrangement.InsertAt(d, 0, G.Fore));
        d = MainArrangement.Add(d, 1, G.Fore);
        Assert.Equal("A A^ B^ | A A^", Ends(d));
        d = MainArrangement.MoveBattery(d, 1, -1);
        Assert.Equal("A B^ B^ | B B^", Ends(d));   // the run stays three deep at the fore end
        Assert.Empty(ShipDesign.Validate(d, limits: true));
    }

    [Fact]
    public void Removing_inside_the_run_shortens_it()
    {
        var d = MainArrangement.Add(Load("bismarck"), 0, G.Fore);
        var slot = MainArrangement.Slots(d).First(s => s.Group == G.Fore && s.Index == 1);
        Assert.Equal("A A^ | A A^", Ends(MainArrangement.Remove(d, slot)));
    }

    [Fact]
    public void Turrets_move_between_batteries_and_groups()
    {
        var d = Load("lord_nelson");
        var w = MainArrangement.Slots(d).First(s => s.Group == G.Wing && s.Battery == 1);
        var moved = MainArrangement.Reassign(d, w, 2);
        Assert.Equal(0, moved.Main![1].Wing);
        Assert.Equal(3, moved.Main![2].Wing);
        var split = MainArrangement.SplitOff(d, MainArrangement.Slots(d).First(s => s.Group == G.Aft));
        Assert.Equal(4, split.Main!.Count);
        Assert.Equal(1, split.Main[3].Aft);
        Assert.Equal(0, split.Main[0].Aft);
        var q = MainArrangement.MoveTo(d, MainArrangement.Slots(d).First(s => s.Group == G.Fore), G.Aft);
        Assert.Equal("| A A^", Ends(q).Trim());
        Assert.Empty(ShipDesign.Validate(split, limits: true));
    }

    [Fact]
    public void Every_preset_builds()
    {
        var d = Load("dreadnought");
        foreach (var p in MainArrangement.Presets)
        {
            var next = MainArrangement.Apply(d, 0, p);
            Assert.True(ShipDesign.Validate(next, limits: true).Count == 0, p.Name);
            ShipDesign.Build(next);
        }
    }
}
