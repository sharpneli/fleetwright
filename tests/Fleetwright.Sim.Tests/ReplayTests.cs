using Fleetwright.Shipgen;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>Replays (src/Fleetwright.Sim.Missions/Replay.cs): a scenario as played, written as it goes, read back and played
/// again on a registry of its own, is the same world; a record a crash cut short reads to its last whole line.</summary>
public class ReplayTests
{
    static readonly string SettingsPath = Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json");
    static readonly SimSettings Settings = SimSettings.Load(SettingsPath);
    static readonly string[] Designs = [Path.Combine(Fleet.Shipgen, "designs")];

    static string Scratch(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), "fleetwright-tests", name + "-" + Environment.ProcessId);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>The squadrons scenario under a setup (a ship's design swapped, a temper), played by the app's way: its
    /// registry already numbering another class first, so its ids aren't a fresh registry's. Twelve minutes of a fight with
    /// a speed, a pause, an order by hand and the interest rules among them; written, read back, made again on a registry
    /// of its own and played: the same hash at the end, every command played.</summary>
    [Fact]
    public void A_replay_plays_back_to_the_same_world_on_a_registry_of_its_own()
    {
        var written = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "squadrons.json"));
        var s = new ScenarioSetup { From = "squadrons" }
            .With(new SwapDesign(null, "Tiger", "tiger_1914", "lion"))
            .With(new SetTemper("1st Scouting Group", 0.3))
            .Apply(written) with { Seed = 1234 };
        using var app = new ShipClassRegistry(2);
        app.Request(Design.Load(Path.Combine(Fleet.Shipgen, "designs", "destroyer.json")));   // the app asked for another first
        var classes = ScenarioLoader.RequestClasses(s, app, Designs);
        var w = new World(s.Start, s.Seed, Settings, app);
        var ai = new AiSystem(w);
        ScenarioLoader.Apply(s, w, ai, classes);
        int placed = w.CommandLog.Count;

        w.Apply(Command.SetInterest(InterestKind.Own, InterestAction.Nothing));
        w.Apply(Command.SetSpeed(10));
        w.AdvanceTo(w.Time + 3 * 60_000 + 417);
        w.Apply(Command.Pause());
        var force = Unit.Group(w.Groups.Groups.Single(x => x.Name == "1st Battlecruiser Squadron").GroupId);
        w.Apply(ai.Orders.ByHand.Course(force, 100));
        w.Apply(Command.Resume());
        w.AdvanceTo(w.Time + 9 * 60_000 + 33);
        Assert.True(w.CombatLog.Count > 20, $"{w.CombatLog.Count} in the combat log: a fight");

        string folder = Scratch("replays");
        string path;
        using (var rec = ReplayWriter.Begin(s, Designs, File.ReadAllText(SettingsPath), null, "test", placed, folder))
        {
            foreach (var c in w.CommandLog)
                rec.Command(c);
            rec.Progress(w.Time);
            rec.End(w.Time, w.ComputeHash());
            path = rec.Path;
        }

        var r = Replay.Read(path);
        Assert.Equal(0, r.Cut);
        Assert.Equal(w.CommandLog.Count, r.Commands.Count);
        Assert.Equal(w.Time, r.End!.Value.Time);
        using var fresh = new ShipClassRegistry(2);
        var rb = r.Rebuild(fresh, null, Scratch("replay-world"));
        Assert.NotEqual(classes["lion"], rb.Classes["lion"]);   // the ids differ: the remap mattered
        Assert.Equal(w.Groups.Groups.Count, rb.World.Groups.Groups.Count);
        int next = r.Play(rb.World, r.Placed, r.End.Value.Time);
        Assert.Equal(r.Commands.Count, next);
        Assert.Equal(r.End.Value.Hash, rb.World.ComputeHash());
    }

    /// <summary>A crash leaves a line half written: the record reads to the last whole one, and says where it broke off.</summary>
    [Fact]
    public void A_replay_cut_short_reads_to_its_last_whole_line()
    {
        var s = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "squadrons.json"));
        string folder = Scratch("replays-cut");
        string path;
        using (var rec = ReplayWriter.Begin(s, Designs, File.ReadAllText(SettingsPath), true, "test", 0, folder))
        {
            rec.Command(new StampedCommand(s.Start + 1000, Command.SetSpeed(30)));
            rec.Command(new StampedCommand(s.Start + 2000, Command.SetSea(double.NaN, 12, double.NaN)));
            rec.Progress(s.Start + 5000);
            path = rec.Path;
        }
        File.AppendAllText(path, "{\"c\":[7000,0,1,0,");
        var r = Replay.Read(path);
        Assert.Equal(2, r.Commands.Count);
        Assert.True(double.IsNaN(r.Commands[1].Command.X));
        Assert.Equal(s.Start + 5000, r.Reached);
        Assert.Null(r.End);
        Assert.Equal(5, r.Cut);
        Assert.True(r.Smoke);
        Assert.Equal("Squadrons", r.ScenarioName);
    }
}
