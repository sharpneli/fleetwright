using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;

namespace Fleetwright.Sim.Tests;

/// <summary>A setup (docs/map-ui/launch.md L2): the scenario as written plus its changes, applied as a pure function
/// (the written one untouched), saved as a scenario file with "from" that reads back to the same scenario.</summary>
public class ScenarioSetupTests
{
    static readonly string Folder = Path.Combine(Fleet.Repo, "Content", "Scenarios");
    static Scenario Tsushima() => Scenario.Load(Path.Combine(Folder, "tsushima.json"));

    static IEnumerable<(string Unit, ScenarioShips Ships)> Squadrons(IEnumerable<ScenarioUnit> units)
    {
        foreach (var u in units)
        {
            foreach (var s in u.Ships)
                yield return (u.Name, s);
            foreach (var x in Squadrons(u.Units))
                yield return x;
        }
    }

    static string[] Designs(Scenario s) => [.. Squadrons(s.Units).SelectMany(x => Enumerable.Repeat(x.Ships.Design, x.Ships.Count))];

    [Fact]
    public void A_swap_by_unit_changes_that_unit_only_and_leaves_the_written_scenario_alone()
    {
        var written = Tsushima();
        var before = Designs(written);
        var s = new ScenarioSetup().With(new SwapDesign("1st Armoured Division", null, "borodino", "mikasa")).Apply(written);
        Assert.Equal(before, Designs(written));   // untouched
        var div = Squadrons(s.Units).Where(x => x.Unit == "1st Armoured Division").ToList();
        Assert.All(div, x => Assert.Equal("mikasa", x.Ships.Design));
        Assert.Equal(["Knyaz Suvorov", "Imperator Aleksandr III", "Borodino", "Oryol"], div.SelectMany(x => x.Ships.Names));
        Assert.Equal(written.ShipCount, s.ShipCount);
        Assert.Same(written, s.Written);
        Assert.Equal("Tsushima", s.Name);
    }

    [Fact]
    public void A_swap_of_one_ship_splits_her_entry_and_keeps_the_order()
    {
        var written = Tsushima();
        var s = new ScenarioSetup().With(new SwapDesign(null, "Imperator Aleksandr III", "borodino", "mikasa")).Apply(written);
        var div = Squadrons(s.Units).Where(x => x.Unit == "1st Armoured Division").Select(x => x.Ships).ToList();
        Assert.Equal(["borodino", "mikasa", "borodino"], div.Select(x => x.Design));
        Assert.Equal([1, 1, 2], div.Select(x => x.Count));
        Assert.Equal(["Knyaz Suvorov", "Imperator Aleksandr III", "Borodino", "Oryol"], div.SelectMany(x => x.Names));
        // every ship of a design, anywhere; one that finds nothing is an error
        var all = new ScenarioSetup().With(new SwapDesign(null, null, "buyny", "ikazuchi")).Apply(written);
        Assert.DoesNotContain("buyny", Designs(all));
        Assert.Throws<ScenarioException>(() => new ScenarioSetup().With(new SwapDesign("Cruiser Division", null, "borodino", "mikasa")).Apply(written));
    }

    [Fact]
    public void A_design_changed_again_by_the_picker_leaves_one_change_and_none_when_put_back()
    {
        // docs/map-ui/launch.md L3: the picker's swaps (ScenarioSetup.Swap) leave no trail going back and forth
        var written = Tsushima();
        const string Div = "1st Armoured Division";
        var setup = new ScenarioSetup().Swap(Div, null, "borodino", "mikasa").Swap(Div, null, "mikasa", "izumo");
        Assert.Equal([new SwapDesign(Div, null, "borodino", "izumo")], setup.Changes);
        Assert.All(Squadrons(setup.Apply(written).Units).Where(x => x.Unit == Div), x => Assert.Equal("izumo", x.Ships.Design));
        Assert.True(setup.Swap(Div, null, "izumo", "borodino").IsEmpty);
        // another reach is a change of its own: one ship of the swapped division, then every one of her new design
        var one = setup.Swap(null, "Oryol", "izumo", "mikasa");
        Assert.Equal(2, one.Changes.Count);
        var s = one.Apply(written);
        Assert.Equal(["izumo", "izumo", "izumo", "mikasa"], Squadrons(s.Units).Where(x => x.Unit == Div).SelectMany(x => Enumerable.Repeat(x.Ships.Design, x.Ships.Count)));
        Assert.Equal("Oryol: Izumo, now Mikasa", one.Changes[1] is SwapDesign sw ? sw.Describe(written, d => char.ToUpperInvariant(d[0]) + d[1..]) : "");
        Assert.Same(setup, setup.Swap(Div, null, "izumo", "izumo"));
    }

    [Fact]
    public void A_sides_doctrine_is_its_presets_then_its_own_values()
    {
        // the launch screen's matchup reads each side's range share as its world will start with it (L3)
        var s = Tsushima() with { Doctrine = [(1, "default", [(Fleetwright.Sim.Ai.DoctrineField.RangeShare, 0.45)])] };
        Assert.Equal(0.45, ScenarioLoader.SideDoctrine(s, 1, Fleetwright.Sim.Ai.DoctrineField.RangeShare));
        Assert.Equal(0.6, ScenarioLoader.SideDoctrine(s, 0, Fleetwright.Sim.Ai.DoctrineField.RangeShare));   // doctrine.json's default
    }

    [Fact]
    public void Side_settings_replace_their_own_side_and_a_change_of_the_same_key_replaces_the_last()
    {
        var written = Tsushima();
        var setup = new ScenarioSetup()
            .With(new PlayAs("Japan"))
            .With(new SideSetting(SideKind.Crews, 1, "green"))
            .With(new SideSetting(SideKind.Crews, 1, "average"))
            .With(new SideSetting(SideKind.Readiness, 0, "closed up"))
            .With(new SetSeed(7));
        Assert.Equal(4, setup.Changes.Count);
        var s = setup.Apply(written);
        Assert.Equal(1, s.Player);
        Assert.Contains((0, CrewQuality.Average), s.Crews);   // Russia's as written
        Assert.Contains((1, CrewQuality.Average), s.Crews);
        Assert.Equal("closed up", s.Readiness.Single(x => x.Side == 0).Name);
        Assert.Equal("prepared", s.Readiness.Single(x => x.Side == 1).Name);
        Assert.Equal(7UL, s.Seed);
        Assert.Equal(-1, new ScenarioSetup().With(new PlayAs(null)).Apply(written).Player);   // watching
        // undone, one by one, it's the written scenario again
        var none = setup;
        foreach (var c in setup.Changes)
            none = none.Without(c.Key);
        Assert.True(none.IsEmpty);
        Assert.Equal(written.Seed, none.Apply(written).Seed);
    }

    [Fact]
    public void A_setup_saved_reads_back_to_the_same_scenario()
    {
        var written = Tsushima();
        var setup = new ScenarioSetup { Name = "Tsushima · test", From = "tsushima" }
            .With(new PlayAs(null))
            .With(new SwapDesign("Cruiser Division", null, "aurora", "izumo"))
            .With(new SwapDesign(null, "Oslyabya", "oslyabya", "borodino"))
            .With(new SideSetting(SideKind.Fuel, 0, "0.6"))
            .With(new SideSetting(SideKind.FireDiscipline, 1, "rn-1914"))
            .With(new SetSeed(1906));
        var set = setup.Apply(written);
        var dir = Path.Combine(Path.GetTempPath(), "fleetwright-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "test.json");
            File.WriteAllText(path, setup.ToJson(written));
            Assert.True(Scenario.IsScenario(path));
            var read = Scenario.Load(path, [Folder]);   // its "from" in the shipped folder
            Assert.Equal("Tsushima · test", read.Name);
            Assert.Equal(set.Player, read.Player);
            Assert.Equal(Designs(set), Designs(read));
            Assert.Equal(set.Fuel, read.Fuel);
            Assert.Equal(set.FireDisciplines, read.FireDisciplines);
            Assert.Equal(set.Seed, read.Seed);
            Assert.Equal(setup.Changes.OfType<SwapDesign>(), read.Setup!.Changes.OfType<SwapDesign>());   // in order: a swap may follow another
            Assert.Equal(setup.Changes.OrderBy(c => c.Key), read.Setup.Changes.OrderBy(c => c.Key));
            Assert.Equal(written.Folder, read.Folder);   // the written one's designs
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
        Assert.Throws<ScenarioException>(() => Scenario.Parse("""{ "scenario": 1, "from": "tsushima", "brief": "no" }""", Folder));
    }
}
