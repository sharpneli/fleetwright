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

    [Theory]
    [InlineData("""{ "name": "x" }""", "version")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "ships": [ { "design": "dreadnought" } ] } ] }""", "needs \"at\"")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "at": [56, 3], "course": 0, "kn": 10, "formation": "wedge", "ships": [ { "design": "dreadnought" } ] } ] }""", "wedge")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "at": [56, 3], "course": 0, "kn": 10, "units": [ { "at": [56, 3], "ships": [ { "design": "dreadnought" } ] } ] } ] }""", "only a top unit")]
    [InlineData("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "side": 2, "at": [56, 3], "course": 0, "kn": 10, "ships": [ { "design": "dreadnought" } ] } ] }""", "side 2")]
    [InlineData("""{ "scenario": 1, "start": "31 May", "units": [] }""", "start")]
    public void A_bad_file_says_what_is_wrong(string json, string expected)
    {
        var x = Assert.Throws<ScenarioException>(() => Scenario.Parse(json));
        Assert.Contains(expected, x.Message);
    }

    [Fact]
    public void A_design_not_found_is_named()
    {
        var s = Scenario.Parse("""{ "scenario": 1, "start": "1916-05-31T14:00", "units": [ { "at": [56, 3], "course": 0, "kn": 10, "ships": [ { "design": "no_such_ship" } ] } ] }""");
        using var reg = new Ships.ShipClassRegistry(1);
        var x = Assert.Throws<ScenarioException>(() => ScenarioLoader.RequestClasses(s, reg, Designs));
        Assert.Contains("no_such_ship", x.Message);
    }
}
