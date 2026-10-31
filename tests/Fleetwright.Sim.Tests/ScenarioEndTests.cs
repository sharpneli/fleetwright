using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A scenario's end (docs/battle/command.md 5.0): <c>"until"</c> and <c>"aims"</c> read, and the judge's ends:
/// decided when a side has nothing left afloat, the time limit, quiet once the sides have met and parted, and done when
/// every side is going home out of touch (5.1d).</summary>
public class ScenarioEndTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly string[] Designs = [Path.Combine(Fleet.Shipgen, "designs")];

    /// <summary>A light cruiser a side, <paramref name="apartKm"/> apart east and west, each steaming away from the other.</summary>
    static string Two(double apartKm, string extra = "") => $$"""
        { "scenario": 1, "start": "1916-05-31T14:00", "seed": 3, "sides": ["Britain", "Germany"] {{extra}},
          "units": [
            { "side": 0, "at": [56.5, 4.0], "course": 270, "kn": 15, "ships": [ { "design": "light_cruiser", "name": "West" } ] },
            { "side": 1, "at": [56.5, {{4.0 + apartKm / 61.5:R}}], "course": 90, "kn": 15, "ships": [ { "design": "light_cruiser", "name": "East" } ] } ] }
        """;

    /// <summary>The scenario played as the bench plays it: a check a game minute until it's over, or the hours run out.</summary>
    static (World W, ScenarioEnd End, ScenarioStart Start) Play(string json, SimSettings settings, double hours = 30, Action<World, ScenarioStart>? first = null,
        bool selfPlayed = false)
    {
        var s = Scenario.Parse(json);
        var w = new World(s.Start, s.Seed, settings);
        var ai = new AiSystem(w);
        var start = ScenarioLoader.Apply(s, w, ai, ScenarioLoader.RequestClasses(s, w.Ships.Registry, Designs));
        if (selfPlayed)
            w.Apply(Command.SetPlayerSide(-1));   // as the bench plays it: every side the AI's
        var end = new ScenarioEnd(s, w, selfPlayed ? ai : null);
        first?.Invoke(w, start);
        while (!end.Check() && w.Time.Ms - s.Start.Ms < hours * 3_600_000)
            w.AdvanceTo(w.Time + 60_000);
        return (w, end, start);
    }

    static int Named(ScenarioStart start, string name) => start.ShipNames.First(x => x.Value == name).Key;

    [Fact]
    public void Until_and_aims_are_read_and_every_side_wants_to_destroy_everyone_by_default()
    {
        var s = Scenario.Parse(Two(100, """, "until": "1916-05-31T18:00", "aims": { "Germany": ["destroy everyone"] }"""));
        Assert.Equal(WorldTime.FromUtc(1916, 5, 31, 18), s.Until);
        Assert.Equal([new Aim(AimKind.DestroyEveryone)], s.AimsOf(0));
        Assert.Equal([new Aim(AimKind.DestroyEveryone)], s.AimsOf(1));
        Assert.Null(Scenario.Parse(Two(100)).Until);
        var x = Assert.Throws<ScenarioException>(() => Scenario.Parse(Two(100, """, "aims": { "Germany": "win" }""")));
        Assert.Contains("destroy everyone", x.Message);
        x = Assert.Throws<ScenarioException>(() => Scenario.Parse(Two(100, """, "until": "tomorrow" """)));
        Assert.Contains("until", x.Message);
    }

    [Fact]
    public void A_side_with_nothing_afloat_decides_it()
    {
        var (w, end, start) = Play(Two(100), Settings, first: (w, start) => w.Apply(DamageSystem.Sink(Named(start, "East"), LossKind.Foundered)));
        Assert.Equal(EndReason.Decided, end.Reason);
        Assert.Equal(WorldTime.FromUtc(1916, 5, 31, 14).Ms, end.EndMs);   // at the first check: going down counts as lost
        var lost = Assert.Single(end.Timeline, l => l.Kind == PlayEvent.Lost);
        Assert.Equal((1, Named(start, "East")), (lost.Side, lost.Entity));
        var r = end.Result();
        Assert.Equal((1, 0, 1, true), (r[0].Ships, r[0].Lost, r[0].Afloat, r[0].AimsMet));
        Assert.Equal((1, 1, 0, false), (r[1].Ships, r[1].Lost, r[1].Afloat, r[1].AimsMet));
    }

    /// <summary>A side whose last ship struck her colours has nothing left in the fight (docs/battle/striking.md SK5): it's
    /// decided, the timeline says she struck, and she's lost: taken with the enemy by her, struck (a hulk) with him far
    /// off; a scuttled one counts as scuttled.</summary>
    [Fact]
    public void A_side_whose_last_ship_strikes_has_lost_her_taken_if_the_enemy_holds_the_sea_about_her()
    {
        foreach (var (km, what, want) in new[] { (5.0, 0, ShipFate.Taken), (40.0, 0, ShipFate.Struck), (5.0, 1, ShipFate.Scuttled) })
        {
            var (_, end, start) = Play(Two(km), Settings, first: (w, start) =>
                w.Apply(what == 0 ? DamageSystem.StrikeColours(Named(start, "East")) : DamageSystem.Scuttle(Named(start, "East"))));
            Assert.Equal(EndReason.Decided, end.Reason);
            Assert.Contains(end.Timeline, l => l.Kind == PlayEvent.GaveUp && l.Entity == Named(start, "East") && l.Side == 1);
            var r = end.Result();
            Assert.Equal((1, 1, 0), (r[1].Ships, r[1].Lost, r[1].Afloat));
            Assert.Equal(want, end.Fate(Named(start, "East")));
            Assert.Equal((want == ShipFate.Taken ? 1 : 0, want == ShipFate.Struck ? 1 : 0, want == ShipFate.Scuttled ? 1 : 0), (r[1].Taken, r[1].Struck, r[1].Scuttled));
            Assert.Equal(ShipFate.Afloat, end.Fate(Named(start, "West")));
        }
    }

    [Fact]
    public void Time_runs_out_at_until()
    {
        var (_, end, _) = Play(Two(100, """, "until": "1916-05-31T14:30" """), Settings);
        Assert.Equal(EndReason.Time, end.Reason);
        Assert.Equal(WorldTime.FromUtc(1916, 5, 31, 14, 30).Ms, end.EndMs);
        Assert.DoesNotContain(end.Timeline, l => l.Kind == PlayEvent.Met);
    }

    /// <summary>Two ships that meet at 40 km and steam apart (out of each other's reach, holding their courses): their
    /// encounter closes, and the quiet after it ends the scenario.</summary>
    [Fact]
    public void Sides_that_met_and_parted_end_it_quietly()
    {
        var settings = Settings with { QuietEndMs = 1_800_000 };
        var (w, end, _) = Play(Two(40), settings);
        Assert.Equal(EndReason.Quiet, end.Reason);
        var met = Assert.Single(end.Timeline, l => l.Kind == PlayEvent.Met);
        Assert.True(end.EndMs - met.Ms >= settings.QuietEndMs + settings.EncounterCloseMs);
        Assert.Equal(0, w.Encounters.Count);
        Assert.All(end.Result(), r => Assert.Equal(0, r.Lost));
    }

    /// <summary>Done (docs/battle/command.md "The end"): two battlecruisers decline eight dreadnoughts and get away, the
    /// dreadnoughts lose them, search and give up; every side is going home and none is in touch, so it's over then, long
    /// before either is home (hundreds of kilometres off). The quiet is put off: the search outlasts its two hours here.</summary>
    [Fact]
    public void Sides_going_home_out_of_touch_end_it_done()
    {
        const string json = """
            { "scenario": 1, "start": "1916-05-31T14:00", "seed": 3, "sides": ["Britain", "Germany"],
              "home": { "Britain": [56.3, -1.5], "Germany": [54.2, 7.6] },
              "weather": { "visibility_km": 25, "wind_from": 225, "wind_kn": 8 },
              "units": [
                { "name": "Battle squadron", "side": 0, "at": [56.5, 4.0], "course": 0, "kn": 12, "formation": "column", "spacing": 600,
                  "ships": [ { "design": "dreadnought", "names": ["A", "B", "C", "D", "E", "F", "G", "H"] } ] },
                { "name": "Battlecruisers", "side": 1, "at": [56.5, 4.36], "course": 0, "kn": 15, "formation": "column", "spacing": 600,
                  "ships": [ { "design": "lion", "names": ["X", "Y"] } ] } ] }
            """;
        var (w, end, _) = Play(json, Settings with { QuietEndMs = 24 * 3_600_000 }, hours: 30, selfPlayed: true);   // done, not the quiet
        var phases = end.Timeline.Where(l => l.Kind == PlayEvent.Phase).Select(l => ((CommanderPhase)l.Count, l.Side)).ToList();
        output.WriteLine($"{end.Reason} at {new WorldTime(end.EndMs)}: " + string.Join(", ", phases));
        var store = w.Ships.Store;
        Vec3d[] homes = [Earth.Position(56.3, -1.5), Earth.Position(54.2, 7.6)];
        double farthest = 0;
        for (int slot = 0; slot < store.Slots; slot++)
            if (Aims.Afloat(store, slot))
                farthest = Math.Max(farthest, Earth.Distance(store[slot].Position, homes[store[slot].Side]));
        Assert.Equal(EndReason.Done, end.Reason);
        Assert.Contains((CommanderPhase.Retiring, 1), phases);
        Assert.True(farthest > 100_000, $"the farthest {farthest / 1000:F0} km from home");
    }
}
