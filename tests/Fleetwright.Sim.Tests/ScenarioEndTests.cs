using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;

namespace Fleetwright.Sim.Tests;

/// <summary>A scenario's end (docs/battle/command.md 5.0): <c>"until"</c> and <c>"aims"</c> read, and the judge's three
/// ends: decided when a side has nothing left afloat, the time limit, and quiet once the sides have met and parted.</summary>
public class ScenarioEndTests
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
    static (World W, ScenarioEnd End, ScenarioStart Start) Play(string json, SimSettings settings, double hours = 30, Action<World, ScenarioStart>? first = null)
    {
        var s = Scenario.Parse(json);
        var w = new World(s.Start, s.Seed, settings);
        var ai = new AiSystem(w);
        var start = ScenarioLoader.Apply(s, w, ai, ScenarioLoader.RequestClasses(s, w.Ships.Registry, Designs));
        var end = new ScenarioEnd(s, w);
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
}
