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
        Assert.Contains((0, Experience.Average), s.Crews);   // Russia's as written
        Assert.Contains((1, Experience.Average), s.Crews);
        // a number between the names (docs/battle/crew.md decision 6), written as one
        var between = (setup with { From = "tsushima" }).With(new SideSetting(SideKind.Crews, 1, "1.5"));
        Assert.Contains((1, 1.5), between.Apply(written).Crews);
        Assert.Contains("\"Japan\": 1.5", between.ToJson(written));
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

    static ScenarioUnit Squadron(string name, int side, string design, int count, (double, double)? at = null) =>
        Scenario.Parse($$"""
            { "scenario": 1, "name": "x", "start": "1905-05-27T04:40", "sides": ["a", "b"], "units": [
              { "name": "{{name}}", "side": {{side}}, "at": [{{at?.Item1 ?? 34}}, {{at?.Item2 ?? 130}}], "course": 23, "kn": 11,
                "ships": [ { "design": "{{design}}", "count": {{count}} } ] } ] }
            """).Units[0];

    [Fact]
    public void A_count_strikes_the_last_ships_or_adds_unnamed_ones_and_a_removed_unit_places_none()
    {
        // docs/map-ui/launch.md L3b: the rows' − and +, the units' ✕
        var written = Tsushima();
        var setup = new ScenarioSetup().Resize("1st Destroyer Detachment", "buyny", -1).Resize("Cruiser Division", "aurora", 1);
        var s = setup.Apply(written);
        var dd = Squadrons(s.Units).Single(x => x.Unit == "1st Destroyer Detachment").Ships;
        Assert.Equal(3, dd.Count);
        Assert.Equal(["Buyny", "Bravy", "Bedovy"], dd.Names);
        Assert.Equal(["Grozny"], dd.Struck);
        var cr = Squadrons(s.Units).Single(x => x.Unit == "Cruiser Division").Ships;
        Assert.Equal((3, 2), (cr.Count, cr.Names.Length));   // one more, unnamed
        Assert.Equal(written.ShipCount, s.ShipCount);
        Assert.Equal(3, ScenarioLoader.Stations(s.Units.Single(u => u.Name == "1st Destroyer Detachment")).Count);   // the struck aren't placed
        // back to as written: no change left
        Assert.True(setup.Resize("1st Destroyer Detachment", "buyny", 1).Resize("Cruiser Division", "aurora", -1).IsEmpty);
        // a unit removed: every ship under it struck, it stays to be shown so
        var gone = new ScenarioSetup().With(new RemoveUnit("Second Pacific Squadron")).Apply(written);
        var sps = gone.Units.Single(u => u.Name == "Second Pacific Squadron");
        Assert.True(sps.Removed);
        Assert.Equal(0, sps.ShipCount);
        Assert.Equal(["Oslyabya", "Sissoi Veliky", "Navarin"], sps.Units[1].Ships[0].Struck);
        Assert.Empty(ScenarioLoader.Stations(sps));
        Assert.Throws<ScenarioException>(() => new ScenarioSetup().Resize("Cruiser Division", "aurora", -3).Apply(written));
    }

    [Fact]
    public void A_count_follows_its_ships_through_a_swap_and_applies_after_the_swaps()
    {
        var written = Tsushima();
        const string Dd = "1st Destroyer Detachment";
        // − then a swap of the row: the count names the new design; either way round the same ships
        var a = new ScenarioSetup().Resize(Dd, "buyny", -1).Swap(Dd, null, "buyny", "ikazuchi");
        var b = new ScenarioSetup().Swap(Dd, null, "buyny", "ikazuchi").Resize(Dd, "ikazuchi", -1);
        Assert.Contains(new ChangeCount(Dd, "ikazuchi", -1), a.Changes);
        var sa = Squadrons(a.Apply(written).Units).Single(x => x.Unit == Dd).Ships;
        var sb = Squadrons(b.Apply(written).Units).Single(x => x.Unit == Dd).Ships;
        Assert.Equal(("ikazuchi", 3, "Grozny"), (sa.Design, sa.Count, string.Join(",", sa.Struck)));
        Assert.Equal((sb.Design, sb.Count, string.Join(",", sb.Struck)), (sa.Design, sa.Count, string.Join(",", sa.Struck)));
        // swapped back, the count goes back with it
        Assert.Equal([new ChangeCount(Dd, "buyny", -1)], a.Swap(Dd, null, "ikazuchi", "buyny").Changes);
    }

    [Fact]
    public void A_unit_is_added_on_its_own_or_joining_a_force_and_its_own_changes_fold_into_it()
    {
        var written = Tsushima();
        var own = Squadron("3rd Armoured Division", 0, "oslyabya", 4, (34.035, 129.8));
        var setup = new ScenarioSetup().With(new AddUnit(own, null));
        var s = setup.Apply(written);
        Assert.Equal(written.ShipCount + 4, s.ShipCount);
        Assert.Same(own, s.Units[^1]);
        // its − and its design's change are its own, not changes of their own
        setup = setup.Resize("3rd Armoured Division", "oslyabya", -1).Swap("3rd Armoured Division", null, "oslyabya", "borodino");
        var add = Assert.IsType<AddUnit>(Assert.Single(setup.Changes));
        Assert.Equal(("borodino", 3), (add.Unit.Ships[0].Design, add.Unit.Ships[0].Count));
        Assert.True(setup.WithoutAdded("3rd Armoured Division").IsEmpty);
        // joining a force: its last unit, on its station there
        var reserve = new ScenarioUnit { Name = "Reserve", Side = 0, Formation = Formation.Column, SpacingM = 600 };
        reserve.Ships.Add(new ScenarioShips { Design = "aurora", Count = 2 });
        var joined = new ScenarioSetup().With(new AddUnit(reserve, "Second Pacific Squadron")).Apply(written);
        var sps = joined.Units.Single(u => u.Name == "Second Pacific Squadron");
        Assert.Equal("Reserve", sps.Units[^1].Name);
        Assert.Equal(9, ScenarioLoader.Stations(sps).Count);
        Assert.Throws<ScenarioException>(() => new ScenarioSetup().With(new AddUnit(Squadron("Cruiser Division", 0, "aurora", 1), null)).Apply(written));   // a name taken
        // joining a squadron: it becomes a force of its name, its own ships first, the new one astern at its spacing
        var cd = new ScenarioSetup().With(new AddUnit(reserve, "Cruiser Division")).Resize("Cruiser Division", "aurora", -1).Apply(written)
            .Units.Single(u => u.Name == "Cruiser Division");
        Assert.Equal(["Cruiser Division I", "Reserve"], cd.Units.Select(u => u.Name));
        Assert.Equal(["Oleg"], cd.Units[0].Ships[0].Names);   // a count of the squadron as written finds its ships
        Assert.Equal((600.0, 23.0), (cd.SpacingM, cd.CourseDeg));
        Assert.Equal([0, 600, 1200], ScenarioLoader.Stations(cd).Select(x => x.AsternM));   // Oleg, then the two of Reserve
    }

    [Fact]
    public void A_unit_renamed_is_renamed_last_and_the_tasks_that_name_it_with_it()
    {
        var written = Scenario.Load(Path.Combine(Folder, "jutland.json"));
        var setup = new ScenarioSetup().Rename("Battlecruiser Fleet", "Beatty's");
        Assert.Equal("Battlecruiser Fleet", setup.NameAsMade("Beatty's"));
        // a change made after by the name it's shown by names it as written
        setup = setup.Resize(setup.NameAsMade("1st Battlecruiser Squadron"), "lion", -1).With(new RemoveUnit("2nd Battlecruiser Squadron"));
        var s = setup.Apply(written);
        Assert.Contains(s.Units, u => u.Name == "Beatty's");
        Assert.DoesNotContain(s.Units, u => u.Name == "Battlecruiser Fleet");
        var fifth = s.Units.Single(u => u.Name == "5th Battle Squadron");
        Assert.Equal("Beatty's", fifth.Tasks[0].Of);   // its cover of the Battlecruiser Fleet goes with the name
        Assert.All(s.Units.Where(u => u.Name.EndsWith("Flotilla")).SelectMany(u => u.Tasks), t => Assert.NotEqual("Battlecruiser Fleet", t.Of));
        // renamed again, one change; back to as written, none
        Assert.Single(setup.Rename("Beatty's", "Lion's").Changes.OfType<RenameUnit>());
        Assert.Empty(setup.Rename("Beatty's", "Battlecruiser Fleet").Changes.OfType<RenameUnit>());
        Assert.Throws<ScenarioException>(() => new ScenarioSetup().Rename("Grand Fleet", "Battlecruiser Fleet").Apply(written));   // a name taken
        // an added unit is renamed in its own change, and one joining it follows
        var a = Squadron("Spare", 0, "lion", 2, (56.7, 4.6));
        var b = new ScenarioUnit { Name = "Spare 2", Side = 0, Formation = Formation.Column, SpacingM = 600 };
        b.Ships.Add(new ScenarioShips { Design = "lion", Count = 1 });
        var added = new ScenarioSetup().With(new AddUnit(a, null)).With(new AddUnit(b, "Spare")).Rename("Spare", "Reserve");
        Assert.Equal(["Reserve I", "Spare 2"], added.Apply(written).Units.Single(u => u.Name == "Reserve").Units.Select(u => u.Name));
        Assert.Equal(["Reserve", "Spare 2"], added.Changes.OfType<AddUnit>().Select(x => x.Unit.Name));
        Assert.Equal("Reserve", added.Changes.OfType<AddUnit>().Last().Joins);
    }

    /// <summary>L4: tempers by a unit's or a ship's name, and a side's doctrine fields, each one change, gone when set back
    /// to as written; an added unit's temper is in its own change.</summary>
    [Fact]
    public void Tempers_and_doctrine_are_changes_gone_when_put_back()
    {
        var written = Tsushima();
        var presets = ScenarioLoader.PresetsFor(written);
        Assert.Equal(-0.4, Tree(written, "Cruiser Division").Temper);
        Assert.Equal(0.3, SetTemper.CaptainOf(written.Units, "Imperator Aleksandr III"));
        var setup = new ScenarioSetup()
            .Temper(written, "Cruiser Division", 0.2)
            .Temper(written, "Imperator Aleksandr III", -0.1)
            .Temper(written, "Oryol", 0.5)
            .Doctrine(written, 0, Fleetwright.Sim.Ai.DoctrineField.Aggression, 0.5, presets)
            .Doctrine(written, 0, Fleetwright.Sim.Ai.DoctrineField.NightAction, (double)Fleetwright.Sim.Ai.NightAction.Accept, presets);
        Assert.Equal(5, setup.Changes.Count);
        var s = setup.Apply(written);
        Assert.Equal(0.2, Tree(s, "Cruiser Division").Temper);
        Assert.Equal(-0.1, SetTemper.CaptainOf(s.Units, "Imperator Aleksandr III"));
        Assert.Equal(0.5, SetTemper.CaptainOf(s.Units, "Oryol"));
        Assert.Equal(0, SetTemper.CaptainOf(s.Units, "Borodino"));
        Assert.Equal(0.5, ScenarioLoader.SideDoctrine(s, 0, Fleetwright.Sim.Ai.DoctrineField.Aggression, presets));
        Assert.Equal(0.6, ScenarioLoader.SideDoctrine(s, 0, Fleetwright.Sim.Ai.DoctrineField.RangeShare, presets));   // the preset's kept
        Assert.Equal("Russia: heavy ships at night: accept (written: avoid)", setup.Changes[4].Describe(written));
        Assert.Equal("R-Adm Enkvist's temper +0.20 (written: −0.40)", setup.Changes[0].Describe(written));
        // put back: no change
        Assert.Equal(4, setup.Temper(written, "Cruiser Division", -0.4).Changes.Count);
        Assert.Equal(4, setup.Doctrine(written, 0, Fleetwright.Sim.Ai.DoctrineField.Aggression, 0, presets).Changes.Count);
        Assert.Throws<ScenarioException>(() => new ScenarioSetup().With(new SetTemper("Knyaz Potemkin", 0.2)).Apply(written));
        // an added unit's in its own change
        var added = new ScenarioSetup().With(new AddUnit(Squadron("Reserve", 0, "aurora", 2, (34.0, 129.8)), null)).Temper(written, "Reserve", 0.3);
        Assert.Equal(0.3, Assert.Single(added.Changes.OfType<AddUnit>()).Unit.Temper);
    }

    /// <summary>L5: the day as changes; a seed "new each time" keeps the written one when applied (the screen draws one)
    /// and reads back as such.</summary>
    [Fact]
    public void The_day_is_changes_and_a_new_seed_each_time_keeps_the_written()
    {
        var written = Tsushima();
        var setup = new ScenarioSetup { From = "tsushima" }.With(new SetSeed(null)).With(new SetEnd(written.Start + 3_600_000L));
        var s = setup.Apply(written);
        Assert.Equal(written.Seed, s.Seed);
        Assert.Equal(written.Start + 3_600_000L, s.Until);
        Assert.Contains("\"seed\": \"new\"", setup.ToJson(written));
        Assert.Throws<ScenarioException>(() => new ScenarioSetup().With(new SetEnd(written.Start)).Apply(written));
        Assert.Equal("Weather: 8 km, wind SSW 12 kn (written: 13 km, wind SW 18 kn)", new SetWeather(new WeatherHere(8000, 200, 12 * 0.514444)).Describe(written));
    }

    static ScenarioUnit Tree(Scenario s, string name)
    {
        ScenarioUnit? Find(IEnumerable<ScenarioUnit> units) => units.Select(u => u.Name == name ? u : Find(u.Units)).FirstOrDefault(u => u != null);
        return Find(s.Units)!;
    }

    [Fact]
    public void A_setup_saved_reads_back_to_the_same_scenario()
    {
        var written = Tsushima();
        var presets = ScenarioLoader.PresetsFor(written);
        var setup = new ScenarioSetup { Name = "Tsushima · test", From = "tsushima" }
            .Temper(written, "Cruiser Division", 0.2)
            .Temper(written, "Imperator Aleksandr III", -0.1)
            .Doctrine(written, 0, Fleetwright.Sim.Ai.DoctrineField.Aggression, 0.5, presets)
            .Doctrine(written, 1, Fleetwright.Sim.Ai.DoctrineField.Engage, (double)Fleetwright.Sim.Ai.Engage.CarryOn, presets)
            .Doctrine(written, 1, Fleetwright.Sim.Ai.DoctrineField.Dispose, 0, presets)
            .With(new PlayAs(null))
            .With(new SwapDesign("Cruiser Division", null, "aurora", "izumo"))
            .With(new SwapDesign(null, "Oslyabya", "oslyabya", "borodino"))
            .With(new AddUnit(Squadron("3rd Armoured Division", 0, "oslyabya", 4, (34.035, 129.8)), null))
            .Temper(written, "3rd Armoured Division", 0.3)
            .Resize("1st Destroyer Detachment", "buyny", -2)
            .With(new RemoveUnit("1st Destroyer Flotilla"))
            .Rename("Cruiser Division", "Enkvist's cruisers")
            .With(new SideSetting(SideKind.Fuel, 0, "0.6"))
            .With(new SideSetting(SideKind.FireDiscipline, 1, "rn-1914"))
            .With(new SetWeather(new WeatherHere(8000, 200, 12 * 0.514444)))
            .With(new SetEnd(written.Start + 20 * 3_600_000L))
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
            Assert.Equal(set.ShipCount, read.ShipCount);
            Assert.Equal(Squadrons(set.Units).Select(x => string.Join(",", x.Ships.Struck)), Squadrons(read.Units).Select(x => string.Join(",", x.Ships.Struck)));
            Assert.Equal(set.Fuel, read.Fuel);
            Assert.Equal(set.FireDisciplines, read.FireDisciplines);
            Assert.Equal(set.Seed, read.Seed);
            Assert.Equal(8000, read.Weather!.Value.VisibilityM, 6);
            Assert.Equal(200, read.Weather.Value.WindFromDeg, 6);
            Assert.Equal(12, read.Weather.Value.WindMs / 0.514444, 3);
            Assert.Equal(set.Until, read.Until);
            Assert.Equal(set.Doctrine.Select(d => (d.Side, d.Preset, string.Join(",", d.Values))), read.Doctrine.Select(d => (d.Side, d.Preset, string.Join(",", d.Values))));
            Assert.Equal(0.2, Tree(read, "Enkvist's cruisers").Temper);
            Assert.Equal(0.3, Tree(read, "3rd Armoured Division").Temper);
            Assert.Equal(-0.1, SetTemper.CaptainOf(read.Units, "Imperator Aleksandr III"));
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
