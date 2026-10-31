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

    /// <summary>A3's convoy: two tramps for a zone 70 km west at 8 kn, a light cruiser by them; a German battlecruiser to
    /// stop it, 25 km south of the zone with no orders. <paramref name="escort"/> the escort's design and where it is.</summary>
    static string Convoy(string escort = "light_cruiser", string escortAt = "[56.33, 4.0]", string raider = "[56.08, 2.86]", string until = "1916-05-31T22:00") => $$"""
        { "scenario": 1, "start": "1916-05-31T14:00", "seed": 3, "sides": ["Britain", "Germany"], "until": "{{until}}",
          "aims": { "Britain": { "reach": "Convoy", "zone": { "at": [56.3, {{4.0 - 70 / KmPerLonDeg:R}}], "km": 4 } }, "Germany": { "stop": "Britain" } },
          "units": [
            { "name": "Convoy", "side": 0, "at": [56.3, 4.0], "course": 270, "kn": 8, "spacing": 800,
              "ships": [ { "design": "tramp_1914", "names": ["One", "Two"] } ] },
            { "name": "Escort", "side": 0, "at": {{escortAt}}, "course": 270, "kn": 8, "ships": [ { "design": "{{escort}}", "name": "Guard" } ] },
            { "name": "Raider", "side": 1, "at": {{raider}}, "course": 90, "kn": 0, "ships": [ { "design": "lion", "name": "Hunter" } ] } ] }
        """;

    /// <summary>What a ship's chief battery is on.</summary>
    static int Chief(World w, int ship)
    {
        var store = w.Ships.Store;
        int slot = store.SlotOf(ship);
        var cls = store[slot].Handling.Class;
        for (int b = 0; b < cls.Batteries.Length; b++)
            if (cls.Batteries[b].Chief)
                return store.Combat.Target(slot, b);
        return 0;
    }

    /// <summary>The matchup's threat (<see cref="Matchup.Threat"/>, the outs expected over an hour's action): a
    /// dreadnought's guns endanger a battlecruiser at 8 km, a light cruiser's a tenth as much, a tramp's not at all; and
    /// the other way, the battlecruiser the light cruiser far more.</summary>
    [Fact]
    public void A_threat_is_what_her_guns_can_do_to_him()
    {
        var light = Play(Convoy(), hours: 0);
        var heavy = Play(Convoy("dreadnought"), hours: 0);
        MatchupEntry E(string design)
        {
            var p = design == "light_cruiser" ? light : heavy;
            var store = p.W.Ships.Store;
            int slot = store.SlotOf(Named(p.Start, design switch { "lion" => "Hunter", "tramp_1914" => "One", _ => "Guard" }));
            return new(p.Ai.Judge.Matchups.Profile(p.W, store.Combat.Loadout(slot)), 1);
        }
        double T(string from, string to) => Matchup.Threat([E(from)], [E(to)], 8000, Settings.WantedThreatMin, 1, Settings);
        output.WriteLine($"dreadnought on lion {T("dreadnought", "lion"):0.000}, light cruiser on lion {T("light_cruiser", "lion"):0.000}, " +
                         $"tramp on lion {T("tramp_1914", "lion"):0.000}, lion on light cruiser {T("lion", "light_cruiser"):0.000}");
        Assert.True(T("dreadnought", "lion") > 2 * Settings.WantedThreatOuts);
        Assert.True(T("light_cruiser", "lion") < T("dreadnought", "lion") / 5);
        Assert.True(T("tramp_1914", "lion") < 0.01);
        Assert.True(T("lion", "light_cruiser") > T("light_cruiser", "lion") * 5);
    }

    /// <summary>The captains' choice (docs/battle/aims.md A3; the user: reasonable about it, round the escort if he can): a
    /// battlecruiser out to stop a convoy, its tramps 12 km east and its escort 8 km north. With a light cruiser escort,
    /// which can hardly hurt her, her main guns go on the tramps; with a dreadnought, which can put her out, on the
    /// dreadnought, and she goes round it for the tramps (the dreadnought's range opening as the tramps' closes), her guns
    /// going on the tramps once they're the nearer.</summary>
    [Fact]
    public void A_captain_shoots_the_transports_over_an_escort_unless_it_endangers_him()
    {
        List<(int On, double EscortKm, double TrampKm)> Run(string escort)
        {
            var json = Convoy(escort, $"[{56.3 + 8 / 111.2:R}, {4.0 - 12 / KmPerLonDeg:R}]", raider: $"[56.3, {4.0 - 12 / KmPerLonDeg:R}]");
            var by = new List<(int, double, double)>();
            Play(json, hours: 0.1, minute: (w, start, ai) =>
            {
                var store = w.Ships.Store;
                var h = store[store.SlotOf(Named(start, "Hunter"))];
                double Km(string n) => Earth.Distance(h.Position, store[store.SlotOf(Named(start, n))].Position) / 1000;
                by.Add((Chief(w, h.Entity), Km("Guard"), Km("One")));
                output.WriteLine($"{escort} {by.Count} min: on {by[^1].Item1}, {ai.Commanders.PhaseOf(1)}, heading {h.HeadingDeg:0}, escort {Km("Guard"):0.0} km, tramp {Km("One"):0.0} km");
            });
            return by;
        }
        var probe = Play(Convoy(), hours: 0);
        int guard = Named(probe.Start, "Guard");
        var light = Run("light_cruiser");
        var heavy = Run("dreadnought");
        // the commander's first think has his side's aim (the first minute is before it)
        Assert.NotEqual(guard, light[2].On);
        Assert.NotEqual(0, light[2].On);
        Assert.Equal(guard, heavy[2].On);
        Assert.True(heavy[^1].EscortKm > heavy[2].EscortKm && heavy[^1].TrampKm < heavy[2].TrampKm, "round the dreadnought for the tramps");
        Assert.NotEqual(guard, heavy[^1].On);
    }

    /// <summary>A3's commander: his battlecruiser, with no orders, searches the zone the convoy makes for (he's never held
    /// it), finds it on its way in and is sent after the tramps; she fights the escort while it's between them at point
    /// blank and the tramps are out of reach, then goes for the tramps, and the convoy is stopped with its escort still
    /// afloat.</summary>
    [Fact]
    public void A_stop_side_finds_the_convoy_on_its_way_and_shoots_the_transports_first()
    {
        int onTramps = 0, onGuard = 0;
        var seq = new System.Text.StringBuilder();
        bool guardAtEnd = false;   // the escort afloat when the convoy's aim was decided
        var (w, end, start, ai) = Play(Convoy(), hours: 8, minute: (w, start, ai) =>
        {
            if (w.Ships.Store.SlotOf(Named(start, "Hunter")) < 0)
                return;
            int t = Chief(w, Named(start, "Hunter"));
            if (ai.Aims.Read(w, 0, 0).State != AimState.Open)
                t = 0;   // the convoy's aim decided: nothing wanted any more
            if (t == Named(start, "Guard"))
                onGuard++;
            else if (t != 0)
                onTramps++;
            if (t != 0)
                seq.Append(t == Named(start, "Guard") ? 'G' : 'T');
            if (ai.Aims.Read(w, 0, 0).State == AimState.Open)
                guardAtEnd = w.Ships.Store.SlotOf(Named(start, "Guard")) is int gs and >= 0 && Aims.Afloat(w.Ships.Store, gs);
            if ((w.Time.Ms - T0) % 1_800_000 == 0)
                output.WriteLine($"{(w.Time.Ms - T0) / 60_000,4} min Hunter task {ai.Tasks.Of(Unit.Ship(Named(start, "Hunter")))}, {ai.Commanders.PhaseOf(1)}, " +
                                 $"on {t}, convoy {ai.Aims.Read(w, 0, 0)}");
        });
        foreach (var l in end.Timeline.Where(l => l.Kind is PlayEvent.Ordered or PlayEvent.Phase && l.Side == 1).Take(12))
            output.WriteLine($"{(l.Ms - T0) / 60_000.0,5:0} min  {l.Kind} {l.Other}");
        output.WriteLine($"main battery minutes on the tramps {onTramps}, on the escort {onGuard}; convoy {end.Reading(0, 0)}, stop {end.Reading(1, 0)}");
        output.WriteLine($"by the minute: {seq}");
        foreach (var l in end.Timeline.Where(l => l.Kind is PlayEvent.Lost or PlayEvent.GaveUp or PlayEvent.Fired or PlayEvent.Hit))
            output.WriteLine($"{(l.Ms - T0) / 60_000.0,5:0} min  {l.Kind} {start.ShipNames.GetValueOrDefault(l.Entity)} {start.ShipNames.GetValueOrDefault(l.Other)} {l.Count}");
        Assert.True(onTramps > 0);
        Assert.True(guardAtEnd, "the convoy stopped with its escort still afloat");
        Assert.NotEqual(AimState.Met, end.Reading(0, 0).State);
        Assert.Contains(end.Timeline, l => l.Kind == PlayEvent.Ordered && l.Side == 1 && (CommanderWhy)(l.Other % 256) == CommanderWhy.Contact);
    }

    /// <summary>A4's hunting ground: three tramps alone on routes across a ground 40 km about [56.5, 4.3], each 25 km
    /// out; a German light cruiser raider at its western edge, out to sink two of them's tonnage, then home 100 km west.
    /// <paramref name="more"/> more units.</summary>
    static string Raid(string more = "", string until = "1916-05-31T22:00", double tonnes = 0) => $$"""
        { "scenario": 1, "start": "1916-05-31T14:00", "seed": 3, "sides": ["Britain", "Germany"], "until": "{{until}}",
          "aims": { "Germany": [ { "sink": "merchants", "tonnes": {{(tonnes > 0 ? tonnes : 2 * 4000.0):R}}, "where": { "at": [56.5, 4.3], "km": 40 } },
                                 { "reach": "Raider", "zone": { "at": [56.5, {{4.3 - 100 / KmPerLonDeg:R}}], "km": 5 }, "weight": 0.5 } ],
                    "Britain": { "stop": "Germany" } },
          "units": [
            { "name": "One", "side": 0, "at": [{{56.5 - 25 / 111.2:R}}, 4.3], "course": 0, "kn": 8, "route": [[57.2, 4.3]], "ships": [ { "design": "tramp_1914", "name": "One" } ] },
            { "name": "Two", "side": 0, "at": [56.5, {{4.3 + 25 / KmPerLonDeg:R}}], "course": 270, "kn": 8, "route": [[56.5, 3.0]], "ships": [ { "design": "tramp_1914", "name": "Two" } ] },
            { "name": "Three", "side": 0, "at": [{{56.5 + 25 / 111.2:R}}, 4.3], "course": 180, "kn": 8, "route": [[55.8, 4.3]], "ships": [ { "design": "tramp_1914", "name": "Three" } ] },
            { "name": "Raider", "side": 1, "at": [56.5, {{4.3 - 30 / KmPerLonDeg:R}}], "course": 90, "kn": 12, "ships": [ { "design": "light_cruiser", "name": "Emden" } ] }
            {{more}} ] }
        """;

    /// <summary>A line a ten minutes of a raid: the raider's commander and task, her chief battery's target, each ship.</summary>
    void Watch(World w, ScenarioStart start, AiSystem ai, params string[] names)
    {
        if ((w.Time.Ms - T0) % 600_000 != 0)
            return;
        var store = w.Ships.Store;
        var line = new List<string>();
        foreach (var n in names)
            if (store.SlotOf(Named(start, n)) is int slot and >= 0)
            {
                var x = store[slot];
                var (lat, lon) = Earth.LatLon(x.Position);
                line.Add($"{n} {lat:0.000},{lon:0.000} {x.HeadingDeg:0}° {x.SpeedOrderKn:0}kn {ai.Captains.Of(x.Entity).Order}{(ai.Captains.Fleeing(x.Entity) ? " fleeing" : "")}{(Aims.Afloat(store, slot) ? "" : " OUT")}");
            }
        output.WriteLine($"{(w.Time.Ms - T0) / 60_000,4} min {ai.Commanders.PhaseOf(1)} {ai.Tasks.Of(Unit.Ship(Named(start, "Emden")))} on {Chief(w, Named(start, "Emden"))} | " + string.Join(" | ", line));
    }

    /// <summary>A4: the raider searches her hunting ground (no orders given), finds the tramps on their routes, and sinks
    /// her tonnage; each tramp flees her as she comes (no main battery: what she is), the ones that get away going back to
    /// their routes once she's out of sight; with her tonnage, the raider makes for home and gets there.</summary>
    [Fact]
    public void A_raider_hunts_her_ground_and_the_merchants_scatter_from_her()
    {
        var fled = new HashSet<string>();
        var (w, end, start, ai) = Play(Raid(), hours: 8, minute: (w, start, ai) =>
        {
            Watch(w, start, ai, "Emden", "One", "Two", "Three");
            foreach (var n in new[] { "One", "Two", "Three" })
                if (ai.Captains.Fleeing(Named(start, n)))
                    fled.Add(n);
        });
        var store = w.Ships.Store;
        var free = new[] { "One", "Two", "Three" }.Where(n => store.SlotOf(Named(start, n)) is int slot and >= 0 && Aims.Afloat(store, slot)).ToList();
        output.WriteLine($"fled: {string.Join(", ", fled)}; afloat: {string.Join(", ", free)}; sink {end.Reading(1, 0)}, home {end.Reading(1, 1)}; {end.Reason}");
        foreach (var l in end.Timeline.Where(l => l.Kind is PlayEvent.Lost or PlayEvent.GaveUp))
            output.WriteLine($"{(l.Ms - T0) / 60_000.0,5:0} min  {l.Kind} {start.ShipNames.GetValueOrDefault(l.Entity)} by {l.Other}: {l.Count}");
        Assert.Contains(end.Timeline, l => l.Kind == PlayEvent.Ordered && l.Side == 1 && (CommanderWhy)(l.Other % 256) == CommanderWhy.Idle);   // her search
        Assert.Equal(AimState.Met, end.Reading(1, 0).State);
        Assert.Equal(AimState.Met, end.Reading(1, 1).State);
        Assert.Equal(3, fled.Count);   // every tramp she came at ran
        Assert.NotEmpty(free);
        foreach (var n in free)
        {
            var x = store[store.SlotOf(Named(start, n))];
            Assert.Equal(Helm.Route, x.Helm);   // back on her way
            Assert.False(ai.Captains.Fleeing(x.Entity));
        }
    }

    /// <summary>A4: the hunters (Britain's Stop of the raid) search the hunting ground they know she works, with no orders
    /// given; an armoured cruiser, stronger and slower than the raider, comes on her there; she declines it, and makes
    /// for home with what she's sunk (her way home her second aim), getting there.</summary>
    [Fact]
    public void A_raider_declines_a_stronger_hunter_and_makes_for_home_with_her_score()
    {
        string hunter = $$""", { "name": "Hunter", "side": 0, "at": [{{56.5 + 30 / 111.2:R}}, 4.3], "course": 180, "kn": 10, "ships": [ { "design": "armoured_cruiser", "name": "Hunter" } ] }""";
        var (w, end, start, ai) = Play(Raid(hunter, tonnes: 20_000), hours: 9, minute: (w, start, ai) => Watch(w, start, ai, "Emden", "Hunter", "One", "Two", "Three"));
        foreach (var l in end.Timeline.Where(l => l.Kind is PlayEvent.Ordered && l.Side is 0 or 1).Take(16))
            output.WriteLine($"{(l.Ms - T0) / 60_000.0,5:0} min  side {l.Side} ordered {l.Entity}: {l.Other}");
        output.WriteLine($"sink {end.Reading(1, 0)}, home {end.Reading(1, 1)}, stop {end.Reading(0, 0)}; {end.Reason}");
        var store = w.Ships.Store;
        Assert.True(Aims.Afloat(store, store.SlotOf(Named(start, "Emden"))));
        Assert.Contains(end.Timeline, l => l.Kind == PlayEvent.Ordered && l.Side == 0 && l.Entity == Unit.Ship(Named(start, "Hunter")).Id &&
                                           (CommanderWhy)(l.Other % 256) == CommanderWhy.Idle);   // the hunter's search
        Assert.Contains(end.Timeline, l => l.Kind == PlayEvent.Ordered && l.Side == 1 && (CommanderWhy)(l.Other % 256) == CommanderWhy.Outmatched);
        Assert.Equal(AimState.Met, end.Reading(1, 1).State);
        Assert.NotEqual(AimState.Met, end.Reading(1, 0).State);
    }
}
