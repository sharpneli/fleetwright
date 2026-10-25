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
