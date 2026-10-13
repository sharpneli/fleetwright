using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;

namespace Fleetwright.Sim.Tests;

/// <summary>Weather and light (docs/battle/plan.md step 3.2b): one weather for the world, set by a command and a
/// scenario; the sun's elevation and bearing at a place.</summary>
public class WeatherTests
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>Off Jutland on 31 May 1916 (57°N 5.5°E): local noon about 11:38 UTC with the sun 55° up, high to the
    /// south-west in the afternoon, set between 19:30 and 20:30 UTC, and never 12° under all night (57°N in early June
    /// has nautical twilight till dawn).</summary>
    [Fact]
    public void The_sun_off_Jutland_on_the_day()
    {
        var p = Earth.Position(57, 5.5);
        double El(int h, int m) => Light.SunElevationDeg(p, WorldTime.FromUtc(1916, 5, 31, h, m));
        Assert.InRange(El(11, 30), 54, 56);   // local noon: 90 - 57 + 21.9° of declination
        Assert.InRange(Light.SunBearingDeg(p, WorldTime.FromUtc(1916, 5, 31, 11, 38)), 175, 185);
        Assert.InRange(Light.SunBearingDeg(p, WorldTime.FromUtc(1916, 5, 31, 16, 0)), 240, 265);
        Assert.InRange(El(16, 0), 30, 40);
        Assert.True(El(19, 30) > 0 && El(20, 30) < 0, $"{El(19, 30):F1}° at 19:30, {El(20, 30):F1}° at 20:30");
        Assert.True(El(23, 30) > -12, $"{El(23, 30):F1}° at midnight's lowest");
    }

    [Fact]
    public void The_weather_is_set_by_a_command_hashed_and_snapshot()
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 3, Settings);
        Assert.Equal(Settings.DefaultVisibilityM, w.Weather.At(Earth.Position(0, 0), w.Time).VisibilityM);
        ulong before = w.ComputeHash();
        w.Apply(Command.SetWeather(12_000, 225, 4));
        var here = w.Weather.At(Earth.Position(57, 5), w.Time);
        Assert.Equal(new WeatherHere(12_000, 225, 4), here);
        Assert.NotEqual(before, w.ComputeHash());
        var s = new Snapshot();
        w.Fill(s);
        Assert.Equal(here, s.Weather);
        Assert.Throws<ArgumentException>(() => w.Apply(Command.SetWeather(0)));
    }

    [Fact]
    public void A_scenario_gives_its_weather_and_one_from_it_inherits_it()
    {
        var j = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "jutland.json"));
        Assert.Equal(25_000, j.Weather!.Value.VisibilityM);
        Assert.Equal(225, j.Weather.Value.WindFromDeg);
        Assert.Equal(j.Weather, Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "ladder-4-world.json")).Weather);
        var x = Assert.Throws<ScenarioException>(() => Scenario.Parse("""{ "scenario": 1, "start": "1916-05-31T14:00", "weather": { "wind_kn": 5 }, "units": [] }"""));
        Assert.Contains("visibility_km", x.Message);
    }
}
