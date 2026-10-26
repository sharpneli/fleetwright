using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;

namespace Fleetwright.Sim.Tests;

/// <summary>Scenarios (docs/battle/plan.md step 2.7a): a file of forces read, given to a world as commands, every ship
/// spawned on the station its group reckons, the groups named, the top units ordered; the same file gives the same
/// world, and its log replays it.</summary>
public class ScenarioTests
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly string[] Designs = [Path.Combine(Fleet.Shipgen, "designs")];

    const string Small = """
        { "scenario": 1, "name": "Small", "start": "1916-05-31T14:00", "seed": 7, "sides": ["Britain", "Germany"],
          "units": [
            { "name": "Force A", "side": 0, "at": [56.5, 3.0], "course": 90, "kn": 15, "formation": "abreast", "spacing": 2000,
              "units": [
                { "name": "1st Squadron", "ships": [ { "design": "dreadnought", "names": ["Alpha", "Bravo"] },
                                                    { "design": "dreadnought", "count": 1 } ] },
                { "name": "2nd Squadron", "formation": "echelon-s", "spacing": 400,
                  "ships": [ { "design": "light_cruiser", "count": 2 } ] } ] },
            { "name": "Line", "side": 1, "at": [56.6, 4.5], "course": 270, "kn": 12, "formation": "column", "spacing": 1000,
              "units": [
                { "name": "A", "ships": [ { "design": "light_cruiser", "count": 2 } ] },
                { "name": "B", "ships": [ { "design": "dreadnought", "count": 2 } ] } ] },
            { "name": "Lone", "side": 1, "at": [57.0, 4.0], "course": 0, "kn": 10, "route": [[57.2, 4.0], [57.2, 4.4]], "loop": true,
              "ships": [ { "design": "light_cruiser", "name": "Picket" } ] } ] }
        """;

    static (World W, AiSystem Ai, ScenarioStart Start) Load(string json)
    {
        var s = Scenario.Parse(json);
        var w = new World(s.Start, s.Seed, Settings);
        var ai = new AiSystem(w);
        var start = ScenarioLoader.Apply(s, w, ai, ScenarioLoader.RequestClasses(s, w.Ships.Registry, Designs));
        return (w, ai, start);
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    /// <summary>A scenario puts every side under its commander, the player's too (docs/battle/command.md decision 14): the
    /// player takes the units they want, by hand, from the first moment.</summary>
    [Fact]
    public void Every_side_starts_under_its_commander_and_the_player_takes_units_by_hand()
    {
        var (w, ai, _) = Load(Small);
        w.Apply(Command.SetPlayerSide(0));
        var force = Unit.Group(w.Groups.Groups.Single(x => x.Name == "Force A").GroupId);
        var line = Unit.Group(w.Groups.Groups.Single(x => x.Name == "Line").GroupId);
        Assert.True(ai.Commanders.Commands(force, 0, w.PlayerSide));
        Assert.True(ai.Commanders.Commands(line, 1, w.PlayerSide));
        w.Apply(ai.Orders.ByHand.Course(force, 120));
        Assert.False(ai.Commanders.Commands(force, 0, w.PlayerSide));
        w.Apply(ai.Orders.Automate(force, true));   // its AI tag: back to him
        Assert.True(ai.Commanders.Commands(force, 0, w.PlayerSide));
    }

    [Fact]
    public void Ships_spawn_on_the_stations_their_groups_reckon()
    {
        var (w, _, start) = Load(Small);
        Assert.Empty(start.Warnings);
        Assert.Equal(10, start.Ships);
        Assert.Equal(6, start.Groups);   // two forces of two squadrons; the lone ship is no group

        var offsets = new List<(int Ship, double AsternM, double AbeamM)>();
        foreach (var name in new[] { "Force A", "Line" })
        {
            var g = w.Groups.Groups.Single(x => x.Name == name);
            Assert.True(g.IsForce);
            w.Groups.LineOffsets(g, offsets);
            Assert.Equal(name == "Force A" ? 5 : 4, offsets.Count);
            var guide = Ship(w, offsets[0].Ship);
            var gp = guide.Position;
            var starboard = Vec3d.Cross(guide.Forward, gp.Normalized());
            foreach (var (e, astern, abeam) in offsets)
            {
                var station = (gp - guide.Forward * astern + starboard * abeam).Normalized() * Earth.Radius;
                Assert.True(Earth.Distance(Ship(w, e).Position, station) < 1, $"{name}: #{e} off its station");
                Assert.Equal(guide.HeadingDeg, Ship(w, e).HeadingDeg, 6);
            }
        }
        Assert.Contains(w.Groups.Groups, g => g.Name == "2nd Squadron" && g.Formation == Formation.EchelonStarboard && g.SpacingM == 400);
        Assert.Equal(["Alpha", "Bravo", "Picket"], start.ShipNames.OrderBy(kv => kv.Key).Select(kv => kv.Value));
        Assert.Equal(1, Ship(w, start.ShipNames.Single(kv => kv.Value == "Picket").Key).Side);
    }

    [Fact]
    public void Top_units_steam_as_ordered_and_hold_formation()
    {
        var (w, _, start) = Load(Small);
        w.AdvanceTo(w.Time + 10 * 60_000);
        var a = w.Groups.Groups.Single(x => x.Name == "Force A");
        var guide = Ship(w, w.Groups.GuideShip(a));
        Assert.InRange(guide.HeadingDeg, 89, 91);
        Assert.InRange(guide.Speed / Handling.MsPerKnot, 14, 16);
        var picket = Ship(w, start.ShipNames.Single(kv => kv.Value == "Picket").Key);
        Assert.Equal(2, picket.RouteCount);
        Assert.True(Earth.Distance(picket.Position, Earth.Position(57.2, 4.0)) < Earth.Distance(Earth.Position(57.0, 4.0), Earth.Position(57.2, 4.0)) - 2000);   // steaming for its first waypoint
    }

    [Fact]
    public void The_same_file_gives_the_same_world_and_its_log_replays_it()
    {
        var (w1, _, _) = Load(Small);
        var (w2, _, _) = Load(Small);
        w1.AdvanceTo(w1.Time + 30 * 60_000);
        w2.AdvanceTo(w2.Time + 30 * 60_000);
        Assert.Equal(w1.ComputeHash(), w2.ComputeHash());

        var s = Scenario.Parse(Small);
        var r = new World(s.Start, s.Seed, Settings, w1.Ships.Registry);
        new AiSystem(r);
        foreach (var c in w1.CommandLog)
        {
            r.AdvanceTo(c.At);
            r.Apply(c.Command);
        }
        r.AdvanceTo(w1.Time);
        Assert.Equal(w1.ComputeHash(), r.ComputeHash());
    }

    /// <summary>The core rule (the user, 2026-10-21): within the fine clock tier the sim behaves exactly the same whatever
    /// the time compression. A fight (the squadrons scenario: sighting, fire control, shells, damage) run at 1x and at the
    /// fine tier's fastest comes out the same world, with the smoke field and without it; and with the fine tier held at
    /// the top speed (§ held, docs/map-ui/input.md: the clock flat out, the tiers and rails as at the fine tier's top).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_fight_is_the_same_at_any_fine_speed(bool smoke)
    {
        var s = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "squadrons.json"));
        ulong Run(double speed, bool holdFine = false)
        {
            var w = new World(s.Start, s.Seed, Settings with { Smoke = smoke });
            var ai = new AiSystem(w);
            ScenarioLoader.Apply(s, w, ai, ScenarioLoader.RequestClasses(s, w.Ships.Registry, Designs));
            // contact mustn't drop the clock to 1x, or both would fight at 1x
            w.Apply(Command.SetInterest(InterestKind.Own, InterestAction.Nothing));
            w.Apply(Command.SetInterest(InterestKind.Others, InterestAction.Nothing));
            w.Apply(Command.SetSpeed(speed, holdFine));
            w.AdvanceTo(w.Time + 20 * 60_000);
            Assert.Equal(speed, w.Speed);
            Assert.Equal(holdFine, w.FineHeld);
            Assert.False(w.Coarse);
            Assert.False(w.Paused);
            Assert.True(w.CombatLog.Count > 100, $"{w.CombatLog.Count} in the combat log: a fight");
            Assert.Equal(smoke, w.Smoke.Count > 0);
            w.Apply(Command.SetSpeed(1));   // the asked speed is world state, in the hash: the same for both to compare
            return w.ComputeHash();
        }
        ulong slow = Run(1);
        Assert.Equal(slow, Run(Settings.CoarseMinSpeed - 1));
        Assert.Equal(slow, Run(Settings.MaxSpeed, holdFine: true));
    }

    /// <summary>The host cuts a slow stretch into slices that end when a snapshot is due (World.Advance's deadline):
    /// cut at every moment it may be, the world comes out the same as one run straight through.</summary>
    [Fact]
    public void Slices_cut_by_a_deadline_change_nothing()
    {
        var (w1, _, _) = Load(Small);
        var (w2, _, _) = Load(Small);
        var end = w1.Time + 10 * 60_000;
        w1.AdvanceTo(end);
        w2.SetTarget(end);
        int slices = 0;
        while (!w2.Advance(int.MaxValue, deadline: 0))   // a deadline long past: every slice is one moment
            slices++;
        Assert.True(slices > 50, $"{slices} slices");   // the ships ride rails: ~100 moments in ten minutes
        Assert.Equal(w1.Time, w2.Time);
        Assert.Equal(w1.ComputeHash(), w2.ComputeHash());
    }

    [Theory]
    [InlineData("""{ "name": "x" }""", "version")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "ships": [ { "design": "dreadnought" } ] } ] }""", "needs \"at\"")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "at": [56, 3], "course": 0, "kn": 10, "formation": "wedge", "ships": [ { "design": "dreadnought" } ] } ] }""", "wedge")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "at": [56, 3], "course": 0, "kn": 10, "units": [ { "at": [56, 3], "ships": [ { "design": "dreadnought" } ] } ] } ] }""", "only a top unit")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "side": 2, "at": [56, 3], "course": 0, "kn": 10, "ships": [ { "design": "dreadnought" } ] } ] }""", "side 2")]
    [InlineData("""{ "scenario": 1, "start": "31 May", "units": [] }""", "start")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "sides": ["A", "B"], "peace": [["A", "C"]], "units": [] }""", "\"C\" isn't another")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "sides": ["A", "B"], "peace": [["A", "A"]], "units": [] }""", "\"A\" isn't another")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "sides": ["A", "B"], "peace": [["A"]], "units": [] }""", "a pair of sides")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "at": [56, 3], "course": 0, "kn": 10, "task": { "do": "loiter" }, "ships": [ { "design": "dreadnought" } ] } ] }""", "\"loiter\" isn't one of")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "at": [56, 3], "course": 0, "kn": 10, "task": { "do": "scout" }, "ships": [ { "design": "dreadnought" } ] } ] }""", "needs \"of\"")]
    public void A_bad_file_says_what_is_wrong(string json, string expected)
    {
        var x = Assert.Throws<ScenarioException>(() => Scenario.Parse(json));
        Assert.Contains(expected, x.Message);
    }

    /// <summary>Opening tasks (docs/battle/command.md 5.1c): a top unit's "task" is given once every unit is placed, naming
    /// another by its name at any level; one naming a unit that isn't there is a warning, and the unit steams on.</summary>
    [Fact]
    public void Opening_tasks_are_given_by_the_units_names()
    {
        string json = Small
            .Replace("\"name\": \"Lone\", \"side\": 1,", "\"name\": \"Lone\", \"side\": 1, \"task\": { \"do\": \"scout\", \"of\": \"B\", \"distance_km\": 12 },")
            .Replace("\"name\": \"Force A\", \"side\": 0,", "\"name\": \"Force A\", \"side\": 0, \"task\": { \"do\": \"cover\", \"of\": \"Nobody\" },");
        var (w, ai, start) = Load(json);
        int lone = w.Ships.NewestEntity;
        Assert.Equal(Ai.Tasks.TaskKind.Scout, ai.Tasks.Of(Unit.Ship(lone)).Kind);
        Assert.Contains(start.Warnings, x => x.Contains("Nobody"));
        Assert.Equal(Ai.Tasks.TaskKind.None, ai.Tasks.Of(Unit.Group(1)).Kind);
    }

    /// <summary>The shipped Jutland reads, its order of battle is the historical count, and every design it names is
    /// there (in its Designs/ or the shipped designs). Not built here: that's the scale test's (shipgen scenario).</summary>
    [Fact]
    public void The_Jutland_reads_with_its_order_of_battle()
    {
        var s = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "jutland.json"));
        Assert.Equal(250, s.ShipCount);
        Assert.Equal(151, s.Units.Where(u => u.Side == 0).Sum(u => u.ShipCount));
        Assert.Equal(99, s.Units.Where(u => u.Side == 1).Sum(u => u.ShipCount));
        var names = s.DesignNames();
        Assert.InRange(names.Count, 50, 60);
        foreach (var n in names)
            Assert.True(File.Exists(Path.Combine(s.Folder!, "Designs", n + ".json")) || File.Exists(Path.Combine(Designs[0], n + ".json")), n);
        Assert.True(Scenario.IsScenario(Path.Combine(Fleet.Repo, "Content", "Scenarios", "jutland.json")));
        Assert.False(Scenario.IsScenario(Path.Combine(Designs[0], "dreadnought.json")));
    }

    /// <summary>The shipped Tsushima reads: its sides, crews and order of battle, and every design it names is there.</summary>
    [Fact]
    public void The_Tsushima_reads_with_its_order_of_battle()
    {
        var s = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "tsushima.json"));
        Assert.Equal(["Russia", "Japan"], s.Sides);
        Assert.Equal(0, s.Player);
        Assert.Equal(13, s.Units.Where(u => u.Side == 0).Sum(u => u.ShipCount));
        Assert.Equal(14, s.Units.Where(u => u.Side == 1).Sum(u => u.ShipCount));
        Assert.Equal([(0, CrewQuality.Average)], s.Crews);
        foreach (var n in s.DesignNames())
            Assert.True(File.Exists(Path.Combine(s.Folder!, "Designs", n + ".json")) || File.Exists(Path.Combine(Designs[0], n + ".json")), n);
    }

    /// <summary>What a scenario says of itself for the launch screen (docs/map-ui/launch.md L1): its place and each side's
    /// character, commander and ensign, taken through "from" (the ladder's rungs have the Jutland's); a side picked
    /// there is a copy with its player changed, the file's own untouched.</summary>
    [Fact]
    public void A_scenario_says_its_place_and_sides_and_a_setup_is_a_copy()
    {
        var s = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "tsushima.json"));
        Assert.Equal("Tsushima Strait", s.Place);
        Assert.Equal("V-Adm Rozhestvensky", s.AboutOf(0)!.Commander);
        Assert.Equal("japan", s.AboutOf(1)!.Ensign);
        Assert.StartsWith("The hunters", s.AboutOf(1)!.Character);
        var rung = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "ladder-1-squadron.json"));
        Assert.Equal("off the Jutland Bank", rung.Place);
        Assert.Equal("britain", rung.AboutOf(0)!.Ensign);
        var japan = s with { PlayerName = "Japan" };
        Assert.Equal(1, japan.Player);
        Assert.Equal(0, s.Player);
        Assert.Same(s.Units, japan.Units);
        Assert.Throws<ScenarioException>(() => Scenario.Parse("""{ "scenario": 1, "start": "1916-05-31T14:00", "sides": ["A"], "about": { "B": {} } }"""));
    }

    /// <summary>The scale test's world rungs keep their tramps at peace with both fleets (docs/battle/plan.md step 3.1a):
    /// given to a world, the neutral side is hostile to neither, and the fleets still to each other.</summary>
    [Fact]
    public void Peace_is_read_by_the_sides_names_and_given_to_the_world()
    {
        var s = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "ladder-4-world.json"));
        Assert.Equal(["Britain", "Germany", "Neutral"], s.Sides);
        Assert.Equal([(0, 2), (1, 2)], s.Peace);
        var w = new World(s.Start, s.Seed, Settings);
        foreach (var (a, b) in s.Peace)
            w.Apply(Command.SetRelation((byte)a, (byte)b, Relation.Peace));
        Assert.False(w.Relations.Hostile(0, 2) || w.Relations.Hostile(2, 1));
        Assert.True(w.Relations.Hostile(0, 1));
        Assert.Empty(Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "jutland.json")).Peace);
    }

    /// <summary>The player's side by its name (docs/battle/plan.md step 3.1c), taken through "from" where the sides
    /// have it, and given to the world.</summary>
    [Fact]
    public void The_player_is_a_side_by_name_and_goes_to_the_world()
    {
        Assert.Equal(0, Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "jutland.json")).Player);
        Assert.Equal(0, Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "ladder-4-world.json")).Player);
        Assert.Equal(-1, Scenario.Parse(Small).Player);
        var (w, _, _) = Load(Small.Replace("\"sides\": [\"Britain\", \"Germany\"],", "\"sides\": [\"Britain\", \"Germany\"], \"player\": \"Germany\","));
        Assert.Equal(1, w.PlayerSide);
        var x = Assert.Throws<ScenarioException>(() => Scenario.Parse(Small.Replace("\"sides\":", "\"player\": \"France\", \"sides\":")));
        Assert.Contains("\"France\" isn't one of the sides", x.Message);
    }

    /// <summary>A side's gun crews by its name (docs/battle/fighting.md 4.2b), trained where not given.</summary>
    /// <summary>Dye loads (docs/visuals/splashes.md "Dye"): one for a whole entry, or each ship's in order, onto the
    /// ships; a colour the palette hasn't is named.</summary>
    [Fact]
    public void Dyes_go_to_the_ships_they_are_given()
    {
        var (w, _, start) = Load(Small
            .Replace("\"names\": [\"Alpha\", \"Bravo\"] }", "\"names\": [\"Alpha\", \"Bravo\"], \"dyes\": [\"orange\", \"Blue\"] }")
            .Replace("\"name\": \"Picket\" }", "\"name\": \"Picket\", \"dye\": \"pink\" }"));
        int Named(string n) => start.ShipNames.First(kv => kv.Value == n).Key;
        Assert.Equal(Ships.Dye.Orange, Ship(w, Named("Alpha")).Dye);
        Assert.Equal(Ships.Dye.Blue, Ship(w, Named("Bravo")).Dye);
        Assert.Equal(Ships.Dye.Pink, Ship(w, Named("Picket")).Dye);
        Assert.Equal(Ships.Dye.None, Ship(w, Named("Alpha") + 2).Dye);   // the next entry's
        var x = Assert.Throws<ScenarioException>(() => Scenario.Parse(Small.Replace("\"name\": \"Picket\" }", "\"name\": \"Picket\", \"dye\": \"mauve\" }")));
        Assert.Contains("orange", x.Message);
    }

    /// <summary>Admirals' and captains' tempers (docs/map-ui/launch.md "Tempers") go to the world as doctrine's Temper: a
    /// unit with an admiral of its own has his, one without takes its force's, a captain's is added on her ship; a unit's
    /// Aggression is refused.</summary>
    [Fact]
    public void Tempers_go_to_the_world_by_admiral_and_captain()
    {
        var (w, ai, start) = Load(Small
            .Replace("\"spacing\": 2000,", "\"spacing\": 2000, \"admiral\": { \"name\": \"V-Adm X\", \"temper\": 0.4, \"why\": \"bold\" },")
            .Replace("{ \"name\": \"2nd Squadron\",", "{ \"name\": \"2nd Squadron\", \"admiral\": \"R-Adm Y\",")
            .Replace("\"names\": [\"Alpha\", \"Bravo\"] }", "\"names\": [\"Alpha\", \"Bravo\"], \"captains\": { \"Bravo\": -0.3 } }")
            .Replace("\"name\": \"Picket\" }", "\"name\": \"Picket\", \"captains\": { \"Picket\": 0.2 } }")
            .Replace("\"loop\": true,", "\"loop\": true, \"admiral\": { \"temper\": -0.5 },"));
        int Named(string n) => start.ShipNames.First(kv => kv.Value == n).Key;
        var d = ai.Doctrine;
        double Of(int e) => d.Of(e, Ship(w, e).Side, DoctrineField.Aggression);
        Assert.Equal(0.4, Of(Named("Alpha")), 9);    // the 1st Squadron has no admiral: the force's
        Assert.Equal(0.1, Of(Named("Bravo")), 9);    // and her own
        Assert.Equal(0, Of(Named("Alpha") + 3), 9);  // the 2nd Squadron's admiral, of no temper given
        Assert.Equal(-0.3, Of(Named("Picket")), 9);  // a ship on her own: her admiral's and her captain's
        var s = Scenario.Parse(Small.Replace("\"spacing\": 2000,", "\"spacing\": 2000, \"admiral\": { \"temper\": 0.4 },"));
        Assert.Equal(0.4, s.Units[0].Temper);
        Assert.True(s.Units[0].Units[1].OwnTemper is false && s.Units[0].OwnTemper);
        Assert.Contains("a side's", Assert.Throws<ScenarioException>(() => Scenario.Parse(Small.Replace("\"spacing\": 2000,", "\"spacing\": 2000, \"doctrine\": { \"Aggression\": 0.5 },"))).Message);
        Assert.Contains("no ship", Assert.Throws<ScenarioException>(() => Scenario.Parse(Small.Replace("\"name\": \"Picket\" }", "\"name\": \"Picket\", \"captains\": { \"Pickett\": 0.2 } }"))).Message);
        Assert.Contains("temper", Assert.Throws<ScenarioException>(() => Scenario.Parse(Small.Replace("\"spacing\": 2000,", "\"spacing\": 2000, \"admiral\": { \"temper\": 2 },"))).Message);
    }

    [Fact]
    public void Crews_are_a_sides_by_name_and_go_to_the_world()
    {
        var (w, _, _) = Load(Small.Replace("\"sides\": [\"Britain\", \"Germany\"],", "\"sides\": [\"Britain\", \"Germany\"], \"crews\": { \"Germany\": \"elite\" },"));
        Assert.Equal(CrewQuality.Trained, w.Gunnery.Crew(0));
        Assert.Equal(CrewQuality.Elite, w.Gunnery.Crew(1));
        var x = Assert.Throws<ScenarioException>(() => Scenario.Parse(Small.Replace("\"sides\":", "\"crews\": { \"Germany\": \"heroic\" }, \"sides\":")));
        Assert.Contains("green, average, trained or elite", x.Message);
    }

    [Fact]
    public void A_design_not_found_is_named()
    {
        var s = Scenario.Parse("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "at": [56, 3], "course": 0, "kn": 10, "ships": [ { "design": "no_such_ship" } ] } ] }""");
        using var reg = new Ships.ShipClassRegistry(1);
        var x = Assert.Throws<ScenarioException>(() => ScenarioLoader.RequestClasses(s, reg, Designs));
        Assert.Contains("no_such_ship", x.Message);
    }

    /// <summary>Every shipped scenario reads: each design it names is there, each ensign is one the launch screen draws,
    /// its player is one of its sides; the benches and the scale test's rungs are tests (docs/missions/scenarios.md).</summary>
    [Fact]
    public void Every_shipped_scenario_reads_with_its_designs_and_ensigns()
    {
        string[] drawn = ["britain", "germany", "russia", "japan", "france", "italy", "ottoman", "finland", "sweden"];
        var folder = Path.Combine(Fleet.Repo, "Content", "Scenarios");
        var played = new List<string>();
        foreach (var path in Directory.GetFiles(folder, "*.json"))
        {
            var s = Scenario.Load(path);
            string name = Path.GetFileNameWithoutExtension(path);
            foreach (var n in s.DesignNames())
                Assert.True(File.Exists(Path.Combine(folder, "Designs", n + ".json")) || File.Exists(Path.Combine(Designs[0], n + ".json")), $"{name}: {n}");
            foreach (var (_, a) in s.About)
                Assert.Contains(a.Ensign, drawn);
            Assert.True(s.PlayerName == null || s.Player >= 0, name);
            if (!s.Test)
                played.Add(name);
        }
        Assert.Equal(["aland", "cape-sarych", "coronel", "dogger-bank", "dover-strait", "heligoland-bight", "jutland", "prizes-war", "tsushima"], played.Order());
    }
}
