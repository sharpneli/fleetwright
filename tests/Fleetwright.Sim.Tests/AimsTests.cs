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

    static (World W, ScenarioEnd End, ScenarioStart Start, AiSystem Ai) Play(string json, Action<World, ScenarioStart>? first = null, double hours = 4,
        Action<World, ScenarioStart, AiSystem>? minute = null)
    {
        var s = Scenario.Parse(json);
        var w = new World(s.Start, s.Seed, Settings);
        var ai = new AiSystem(w);
        var start = ScenarioLoader.Apply(s, w, ai, ScenarioLoader.RequestClasses(s, w.Ships.Registry, Designs));
        w.Apply(Command.SetPlayerSide(-1));
        var end = new ScenarioEnd(s, w, ai);
        first?.Invoke(w, start);
        while (!end.Check() && w.Time.Ms - s.Start.Ms < hours * 3_600_000)
        {
            w.AdvanceTo(w.Time + 60_000);
            minute?.Invoke(w, start, ai);
        }
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

    // --- A2: the side that wants a place ---

    /// <summary>Two tramps steaming north at 8 kn, a Reach 40 km west, no route: the commander sends them there (GoTo),
    /// and in the zone they stop (Withdraw's end) and count as arrived.</summary>
    [Fact]
    public void An_unopposed_convoy_is_sent_to_its_zone_and_stops_there()
    {
        string convoy = """
            , { "name": "Convoy", "side": 0, "at": [56.3, 4.0], "course": 0, "kn": 8, "spacing": 800,
                "ships": [ { "design": "tramp_1914", "names": ["One", "Two"] } ] }
            """;
        var zone = $$"""{ "at": [56.3, {{4.0 - 40 / KmPerLonDeg:R}}], "km": 4 }""";
        var (w, end, start, ai) = Play(Runner($$""" "Britain": { "reach": "Convoy", "zone": {{zone}} } """, convoy, until: "1916-05-31T19:00"), hours: 5);
        var store = w.Ships.Store;
        foreach (var name in new[] { "One", "Two" })
        {
            var x = store[store.SlotOf(Named(start, name))];
            output.WriteLine($"{name}: arrived {ai.Aims.HasArrived(0, 0, x.Entity)}, {x.SpeedOrderKn:0.0} kn ordered");
            Assert.True(ai.Aims.HasArrived(0, 0, x.Entity));
            Assert.InRange(x.SpeedOrderKn, 0, 1);   // stopped, the second keeping station on the first
        }
        Assert.Equal(AimState.Met, end.Reading(0, 0).State);
        Assert.Contains(end.Timeline, l => l.Kind == PlayEvent.Ordered && l.Side == 0 && (CommanderWhy)(l.Other % 256) == CommanderWhy.Reach);
    }

    /// <summary>A light cruiser (29 kn) for a zone 60 km west, a slower, stronger German dreadnought 12 km off her beam:
    /// her commander declines it, and she still makes for her zone (it's her home now) and gets there.</summary>
    [Fact]
    public void A_breaker_that_declines_a_stronger_enemy_still_makes_for_her_zone()
    {
        string json = $$"""
            { "scenario": 1, "start": "1916-05-31T14:00", "seed": 3, "sides": ["Britain", "Germany"], "until": "1916-05-31T20:00",
              "aims": { "Britain": { "reach": "Runner", "zone": {{West(60, 4)}} }, "Germany": { "stop": "Britain" } },
              "units": [
                { "name": "Runner", "side": 0, "at": [56.5, 4.0], "course": 270, "kn": 20, "ships": [ { "design": "light_cruiser", "name": "West" } ] },
                { "name": "Battle", "side": 1, "at": [56.61, 4.0], "course": 270, "kn": 18, "ships": [ { "design": "dreadnought", "names": ["D1", "D2"] } ] } ] }
            """;
        var (w, end, start, ai) = Play(json, hours: 6, minute: (w, start, ai) =>
        {
            if ((w.Time.Ms - T0) % 1_200_000 == 0 && w.Ships.Store.SlotOf(Named(start, "West")) is int slot and >= 0)
                output.WriteLine($"{(w.Time.Ms - T0) / 60_000,4} min West at {Earth.LatLon(w.Ships.Store[slot].Position)}, {w.Ships.Store[slot].SpeedOrderKn:0} kn, " +
                                 $"task {ai.Tasks.Of(Unit.Ship(Named(start, "West")))}, {ai.Commanders.PhaseOf(0)}");
        });
        foreach (var l in end.Timeline.Where(l => l.Kind is PlayEvent.Ordered or PlayEvent.Phase && l.Side == 0).Take(12))
            output.WriteLine($"{(l.Ms - T0) / 60_000.0,5:0} min  {l.Kind} {l.Other}");
        Assert.True(Aims.Afloat(w.Ships.Store, w.Ships.Store.SlotOf(Named(start, "West"))));
        Assert.Equal(AimState.Met, end.Reading(0, 0).State);
        Assert.Contains(end.Timeline, l => l.Kind == PlayEvent.Ordered && l.Side == 0 && (CommanderWhy)(l.Other % 256) == CommanderWhy.Outmatched);
    }

    /// <summary>A convoy for a zone 40 km west with a cruiser by it, and the cruiser's own way home 50 km north as a
    /// second aim: the cruiser keeps by the convoy until it's in, then makes for home, and the tramps stay at the beach.</summary>
    [Fact]
    public void Once_the_convoy_is_in_its_escort_makes_for_home_and_the_convoy_stays()
    {
        string units = """
            , { "name": "Convoy", "side": 0, "at": [56.3, 4.0], "course": 270, "kn": 8, "spacing": 800,
                "ships": [ { "design": "tramp_1914", "names": ["One", "Two"] } ] }
            , { "name": "Escort", "side": 0, "at": [56.33, 4.0], "course": 270, "kn": 8, "ships": [ { "design": "light_cruiser", "name": "Guard" } ] }
            """;
        var beach = $$"""{ "at": [56.3, {{4.0 - 40 / KmPerLonDeg:R}}], "km": 4 }""";
        var home = """{ "at": [56.75, 3.35], "km": 4 }""";
        var (w, end, start, ai) = Play(Runner($$"""
            "Britain": [ { "reach": "Convoy", "zone": {{beach}} }, { "reach": "Escort", "zone": {{home}}, "weight": 0.5 } ]
            """, units, until: "1916-05-31T23:00"), hours: 9, minute: (w, start, ai) =>
        {
            if ((w.Time.Ms - T0) % 1_200_000 != 0)
                return;
            foreach (var n in new[] { "One", "Guard" })
                if (w.Ships.Store.SlotOf(Named(start, n)) is int slot and >= 0)
                    output.WriteLine($"{(w.Time.Ms - T0) / 60_000,4} min {n} at {Earth.LatLon(w.Ships.Store[slot].Position)}, {w.Ships.Store[slot].SpeedOrderKn:0} kn, " +
                                     $"task {ai.Tasks.Of(w.Groups.Of(Named(start, n)) is { } g ? Unit.Group(g.GroupId) : Unit.Ship(Named(start, n)))}, {ai.Commanders.PhaseOf(0)}, pursued {ai.Aims.Pursued(0)}");
        });
        var store = w.Ships.Store;
        var guard = store[store.SlotOf(Named(start, "Guard"))];
        output.WriteLine($"convoy {end.Reading(0, 0)}, escort {end.Reading(0, 1)}; Guard at {Earth.LatLon(guard.Position)}");
        Assert.Equal(AimState.Met, end.Reading(0, 0).State);
        Assert.Equal(AimState.Met, end.Reading(0, 1).State);
        // still at the beach, stopped: coasting on for hours after (no anchors yet, docs/battle/bugs.md), so within a coast
        var at = Earth.Position(56.3, 4.0 - 40 / KmPerLonDeg);
        foreach (var name in new[] { "One", "Two" })
        {
            var x = store[store.SlotOf(Named(start, name))];
            Assert.InRange(x.SpeedOrderKn, 0, 1);
            Assert.True(Earth.Distance(x.Position, at) <= 15_000);
        }
        Assert.Equal(1.0, end.Score(0), 9);
    }
}
