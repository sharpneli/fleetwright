using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The aims (docs/battle/aims.md A1): read from the file, given to the AI at load, and read as the world stands:
/// a Reach's arrivals (on entering, or after a stay) kept whatever comes after, Sink's merchants' tonnage, Stop the
/// mirror of the aim it's about, each scored and weighted; a side's aims decided ends it once nobody is in touch.</summary>
public class AimsTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly string[] Designs = [Path.Combine(Fleet.Shipgen, "designs")];
    const double KmPerLonDeg = 61.5;   // at 56.5° N

    /// <summary>Britain's "Runner", a light cruiser steaming west at 15 kn from 4° E; Germany's "Far" 150 km east steaming
    /// east, out of reach; <paramref name="units"/> more units, <paramref name="aims"/> the aims.</summary>
    static string Runner(string aims, string units = "", string until = "1916-05-31T16:00") => $$"""
        { "scenario": 1, "start": "1916-05-31T14:00", "seed": 3, "sides": ["Britain", "Germany"], "until": "{{until}}",
          "aims": { {{aims}} },
          "units": [
            { "name": "Runner", "side": 0, "at": [56.5, 4.0], "course": 270, "kn": 15, "ships": [ { "design": "light_cruiser", "name": "West" } ] },
            { "name": "Far", "side": 1, "at": [56.5, {{4.0 + 150 / KmPerLonDeg:R}}], "course": 90, "kn": 15, "ships": [ { "design": "light_cruiser", "name": "East" } ] }
            {{units}} ] }
        """;

    /// <summary>A zone <paramref name="westKm"/> west of 4° E on 56.5° N.</summary>
    static string West(double westKm, double km) => $$"""{ "at": [56.5, {{4.0 - westKm / KmPerLonDeg:R}}], "km": {{km:R}} }""";

    static (World W, ScenarioEnd End, ScenarioStart Start, AiSystem Ai) Play(string json, Action<World, ScenarioStart>? first = null, double hours = 4)
    {
        var s = Scenario.Parse(json);
        var w = new World(s.Start, s.Seed, Settings);
        var ai = new AiSystem(w);
        var start = ScenarioLoader.Apply(s, w, ai, ScenarioLoader.RequestClasses(s, w.Ships.Registry, Designs));
        w.Apply(Command.SetPlayerSide(-1));
        var end = new ScenarioEnd(s, w, ai);
        first?.Invoke(w, start);
        while (!end.Check() && w.Time.Ms - s.Start.Ms < hours * 3_600_000)
            w.AdvanceTo(w.Time + 60_000);
        return (w, end, start, ai);
    }

    static int Named(ScenarioStart start, string name) => start.ShipNames.First(x => x.Value == name).Key;

    [Fact]
    public void Aims_are_read_with_their_figures_and_their_errors_named()
    {
        var s = Scenario.Parse(Runner($$"""
            "Britain": [ { "reach": "Runner", "zone": {{West(20, 3)}}, "of": 1, "stay": 30, "weight": 2 },
                         { "sink": "merchants", "tonnes": 9000, "where": { "at": [57, 3], "km": 200 } } ],
            "Germany": { "stop": "Britain", "weight": 0.5 }
            """));
        var b = s.AimsOf(0).ToArray();
        Assert.Equal((AimKind.Reach, 2.0, 1, 1_800_000L), (b[0].Kind, b[0].Weight, b[0].Of, b[0].StayMs));
        Assert.Equal(3000, b[0].Zone.RadiusM);
        Assert.Equal(["Runner"], s.AimEntriesOf(0).First().Units);
        Assert.Equal((AimKind.Sink, 9000.0, 200_000.0), (b[1].Kind, b[1].Tonnes, b[1].Zone.RadiusM));
        var g = Assert.Single(s.AimsOf(1));
        Assert.Equal((AimKind.Stop, 0, 0.5), (g.Kind, g.About, g.Weight));

        foreach (var (aim, says) in new[]
        {
            ("""{ "reach": "Runner" }""", "zone"),
            ("""{ "reach": "Runner", "zone": { "at": [56, 4] } }""", "a zone is"),
            ("""{ "sink": "warships", "tonnes": 100 }""", "merchants"),
            ("""{ "sink": "merchants" }""", "tonnes"),
            ("""{ "stop": "Britain" }""", "another of the sides"),
            ("""{ "reach": "Runner", "sink": "merchants" }""", "one of reach"),
            ("""{ "reach": "Runner", "zone": { "at": [56, 4], "km": 3 }, "by": 5 }""", "no \"by\""),
        })
        {
            var x = Assert.Throws<ScenarioException>(() => Scenario.Parse(Runner($"\"Britain\": {aim}")));
            Assert.Contains(says, x.Message);
        }
        var y = Assert.Throws<ScenarioException>(() => Play(Runner($$"""
            "Britain": { "reach": "Nobody", "zone": {{West(20, 3)}} }
            """)));
        Assert.Contains("no such unit", y.Message);
    }

    /// <summary>The runner steams into her zone: the Reach is met, Germany's Stop lost (its score 0), and with every aim
    /// decided and nobody in touch it's done at once.</summary>
    [Fact]
    public void A_ship_that_reaches_her_zone_meets_the_aim_and_the_stop_of_it_is_lost()
    {
        var (w, end, start, ai) = Play(Runner($$"""
            "Britain": { "reach": "Runner", "zone": {{West(10, 2)}} }, "Germany": { "stop": "Britain" }
            """));
        output.WriteLine($"{end.Reason} at {(end.EndMs - T0) / 60_000.0:0} min");
        Assert.True(ai.Aims.HasArrived(0, 0, Named(start, "West")));
        Assert.Equal(AimState.Met, end.Reading(0, 0).State);
        Assert.Equal(AimState.Lost, end.Reading(1, 0).State);
        Assert.Equal(EndReason.Done, end.Reason);
        // 8 km to the zone's edge at 15 kn: about 17 minutes
        Assert.InRange((end.EndMs - T0) / 60_000.0, 15, 22);
        var r = end.Result();
        Assert.Equal((true, 1.0), (r[0].AimsMet, r[0].Score));
        Assert.Equal((false, 0.0), (r[1].AimsMet, r[1].Score));
    }

    static readonly long T0 = WorldTime.FromUtc(1916, 5, 31, 14).Ms;

    /// <summary>Passing through a zone 4 km across at 15 kn, she's in it about 9 minutes: a stay of 3 counts, one of 20
    /// doesn't, and the arrival is in the state hash.</summary>
    [Fact]
    public void A_stay_counts_only_once_she_has_been_in_the_zone_that_long()
    {
        ulong? hash = null;
        foreach (var (stay, arrives) in new[] { (3, true), (20, false) })
        {
            var (w, end, start, ai) = Play(Runner($$"""
                "Britain": { "reach": "Runner", "zone": {{West(10, 2)}}, "stay": {{stay}} }
                """, until: "1916-05-31T15:00"));
            Assert.Equal(arrives, ai.Aims.HasArrived(0, 0, Named(start, "West")));
            Assert.Equal(arrives ? AimState.Met : AimState.Open, end.Reading(0, 0).State);
            Assert.NotEqual(hash, w.ComputeHash());
            hash ??= w.ComputeHash();
        }
    }

    /// <summary>Sunk on her way, the runner can't arrive: the Reach is lost and Germany's Stop met.</summary>
    [Fact]
    public void A_reach_whose_ships_are_lost_is_lost()
    {
        var (_, end, _, _) = Play(Runner($$"""
            "Britain": { "reach": "Runner", "zone": {{West(30, 2)}} }, "Germany": { "stop": "Britain" }
            """), first: (w, start) => w.Apply(DamageSystem.Sink(Named(start, "West"), LossKind.Foundered)));
        Assert.Equal(AimState.Lost, end.Reading(0, 0).State);
        Assert.Equal(AimState.Met, end.Reading(1, 0).State);
        Assert.Equal(1.0, end.Result()[1].Score);
    }

    /// <summary>Two German tramps beside the runner's way: one sunk is half the tonnage of the two, the Sink's score a
    /// half and Germany's Stop of it a half; the second struck, it's met. Britain's second aim, the runner home (weight
    /// a half), weighs in the side's score.</summary>
    [Fact]
    public void Sink_counts_the_merchants_tonnage_lost_and_scores_weigh()
    {
        string tramps = """
            , { "name": "Tramps", "side": 1, "at": [57.5, 2.0], "course": 0, "kn": 0, "formation": "abreast", "spacing": 2000,
                "ships": [ { "design": "tramp_1914", "names": ["One", "Two"] } ] }
            """;
        string aims(double tonnes) => $$"""
            "Britain": [ { "sink": "merchants", "tonnes": {{tonnes:R}} }, { "reach": "all", "zone": {{West(400, 2)}}, "of": 1, "weight": 0.5 } ],
            "Germany": { "stop": "Britain" }
            """;
        var probe = Play(Runner(aims(1e9), tramps), hours: 0.02);
        double tramp = probe.W.Ships.Store[probe.W.Ships.Store.SlotOf(Named(probe.Start, "One"))].Handling.Class.Hydrostatics.DisplacementT;
        Assert.Equal(AimState.Lost, probe.End.Reading(0, 0).State);   // there isn't that much to sink

        var (w, end, start, _) = Play(Runner(aims(2 * tramp), tramps, until: "1916-05-31T14:30"),
            first: (w, start) => w.Apply(DamageSystem.Sink(Named(start, "One"), LossKind.Foundered)));
        var sink = end.Reading(0, 0);
        output.WriteLine($"tramp {tramp:0} t: sunk {sink.Done:0} of {sink.Of:0}, score {sink.Score:0.00}; Britain {end.Score(0):0.00}");
        Assert.Equal((AimState.Open, 0.5), (sink.State, sink.Score));
        Assert.Equal(0.5, end.Reading(1, 0).Score, 9);
        Assert.Equal(0.5 / 1.5, end.Score(0), 9);   // the hunt half done, the way home not made

        w.Apply(DamageSystem.StrikeColours(Named(start, "Two")));
        w.AdvanceTo(w.Time + 60_000);
        Assert.Equal(AimState.Met, end.Reading(0, 0).State);
        Assert.Equal(AimState.Lost, end.Reading(1, 0).State);
    }
}
