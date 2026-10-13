using System.Diagnostics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map;

/// <summary>The app's world: made once by the app, not by a tab, so switching tabs leaves it running
/// (docs/world/architecture.md, "Screens"). It runs on its own thread (<see cref="SimHost"/>); the frame loop reports
/// real time each frame (<see cref="Frame"/>) and the scenes read <see cref="Current"/> and send commands. Nothing
/// here holds a reference into the world.
/// <para>A scenario (<see cref="Load"/>, docs/battle/plan.md step 2.7a) makes a new world: its classes are requested
/// and the world placed on a thread while the old one runs on, then the new one takes its place between frames
/// (<see cref="Generation"/> counts the worlds, so a scene drops what it held of the old one).</para></summary>
public sealed class WorldSession : IDisposable
{
    SimHost host;

    /// <summary>The newest snapshot, taken at the start of this frame. The UI thread's; valid until the next frame.</summary>
    public Snapshot Current { get; private set; }

    /// <summary>The classes in play, shared with the art (<see cref="ShipArtLibrary"/>): the app's, made before the
    /// world. Thread-safe; the map requests designs here and spawns ships by the ids it gives.</summary>
    public ShipClassRegistry Classes { get; }

    /// <summary>The world's AI (admirals and captains). Owned by the sim thread: the UI only builds orders with it
    /// (<see cref="AiSystem.Orders"/>, which reads nothing but its id) and sends them. A new world brings a new one.</summary>
    public AiSystem Ai { get; private set; }

    /// <summary>The world's settings: immutable, so the UI may read them (a route's capacity, the snap speed).</summary>
    public SimSettings Settings { get; }

    /// <summary>The world's land: immutable, so the map draws its finest level from the same instance.</summary>
    public Shore Shore { get; }

    /// <summary>Which world this is: 0 the first, one more for each scenario loaded.</summary>
    public int Generation { get; private set; }

    /// <summary>The scenario the world was made from, or null (the dev ships, or nothing).</summary>
    public Scenario? Scenario { get; private set; }

    /// <summary>What the scenario's placing reported (the ships' names, what didn't come out as written), or null.</summary>
    public ScenarioStart? ScenarioStart { get; private set; }

    /// <summary>A scenario's load: seconds from asking to the new world running, and the classes' share of it.</summary>
    public (double Total, double Classes) LoadSeconds { get; private set; }

    /// <summary>The scenario loading, if one is: its name, and its classes ready of all.</summary>
    public (string Name, int Ready, int Of)? Loading => loading is { } l
        ? (l.Scenario.Name, l.Classes?.Values.Count(Classes.IsReady) ?? 0, l.Scenario.DesignNames().Count)
        : null;

    /// <summary>The world's instruments on the UI side (docs/battle/plan.md step 2.7b), fed every frame.</summary>
    public WorldPerf Perf { get; } = new();

    /// <summary>The last scenario's failure to load, or null.</summary>
    public string? LoadError { get; private set; }

    sealed class Pending
    {
        public required Scenario Scenario;
        public Dictionary<string, int>? Classes;
        public Task<(World World, AiSystem Ai, ScenarioStart Start)>? Task;
        public readonly Stopwatch Clock = Stopwatch.StartNew();
        public double ClassesSeconds;
    }

    Pending? loading;

    /// <summary>The shipped 1:10M land and its index, built on a thread (~0.2-0.4 s): the app starts it before the
    /// window opens and hands the result to the world.</summary>
    public static Task<Shore> LoadShore() =>
        Task.Run(() => Shore.Build(Land.Load(Path.Combine(AppContext.BaseDirectory, "Content", "World", "land-10m.bin"))));

    /// <summary>The shipped scenarios (Content/Scenarios next to the exe), by path.</summary>
    public static string[] ShippedScenarios()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Content", "Scenarios");
        return Directory.Exists(dir) ? [.. Directory.GetFiles(dir, "*.json").Where(Sim.Missions.Scenario.IsScenario).Order(StringComparer.Ordinal)] : [];
    }

    /// <summary>The shipped designs, where a scenario looks after its own Designs/.</summary>
    static readonly string[] DesignFolders = [Path.Combine(AppContext.BaseDirectory, "Content", "Designs")];

    /// <summary>A world with the shipped settings (Content/Sim/settings.json), the app's class registry and the land.
    /// <paramref name="setup"/> runs on it before its thread starts: the dev ships (-ships=) until missions place
    /// forces.</summary>
    public WorldSession(WorldTime start, ulong seed, ShipClassRegistry classes, Shore shore, Action<World>? setup = null)
    {
        Classes = classes;
        Shore = shore;
        Settings = SimSettings.Load(Path.Combine(AppContext.BaseDirectory, "Content", "Sim", "settings.json"));
        var w = new World(start, seed, Settings, classes, shore);
        Ai = new AiSystem(w);
        setup?.Invoke(w);
        host = Start(w);
        Current = host.Latest();
    }

    /// <summary>Starts a world's thread and waits for its first snapshot, so no frame shows an empty one.</summary>
    static SimHost Start(World w)
    {
        var h = new SimHost(w);
        h.Start();
        var s = h.Latest();
        for (int k = 0; k < 1000 && s.Sequence == 0; k++)
        {
            Thread.Sleep(1);
            s = h.Latest();
        }
        return h;
    }

    /// <summary>Reads a scenario file and starts loading it (<see cref="Load(Scenario)"/>); a file that isn't one is
    /// <see cref="LoadError"/>.</summary>
    public void Load(string path)
    {
        try
        {
            Load(Sim.Missions.Scenario.Load(path));
        }
        catch (Exception x) when (x is ScenarioException or IOException)
        {
            LoadError = x.Message;
        }
    }

    /// <summary>Starts loading a scenario: its classes requested now (they build in the background, with their art), the
    /// world made and placed on a thread once they're built; it replaces this world at a frame
    /// (<see cref="Frame"/>). A load already going is let finish and dropped.</summary>
    public void Load(Scenario s)
    {
        LoadError = null;
        var p = new Pending { Scenario = s };
        loading = p;
        try
        {
            p.Classes = ScenarioLoader.RequestClasses(s, Classes, DesignFolders);
        }
        catch (Exception x) when (x is ScenarioException or IOException or System.Text.Json.JsonException)
        {
            (LoadError, loading) = (x.Message, null);
            return;
        }
        p.Task = Task.Run(() =>
        {
            foreach (int id in p.Classes.Values)
                Classes.Get(id);   // waits for each build (and its art)
            p.ClassesSeconds = p.Clock.Elapsed.TotalSeconds;
            var w = new World(s.Start, s.Seed, Settings, Classes, Shore);
            var ai = new AiSystem(w);
            var start = ScenarioLoader.Apply(s, w, ai, p.Classes);
            return (w, ai, start);
        });
    }

    /// <summary>The frame loop's call, every frame: a loaded scenario's world in place of this one, real time passed,
    /// then the newest snapshot.</summary>
    public void Frame(float dt)
    {
        if (loading is { Task.IsCompleted: true } p)
        {
            loading = null;
            if (p.Task.IsFaulted)
                LoadError = p.Task.Exception!.GetBaseException().Message;
            else
            {
                var (w, ai, start) = p.Task.Result;
                // the clock as the player left it: the new world runs at the speed asked of the old, paused if it was
                w.Apply(Command.SetSpeed(Current.Speed));
                if (Current.Paused)
                    w.Apply(Command.Pause());
                var old = host;
                host = Start(w);
                old.Dispose();
                (Ai, Scenario, ScenarioStart) = (ai, p.Scenario, start);
                Generation++;
                LoadSeconds = (p.Clock.Elapsed.TotalSeconds, p.ClassesSeconds);
                Console.WriteLine($"scenario {p.Scenario.Name}: {start.Ships} ships in {start.Groups} groups, " +
                                  $"{p.Classes!.Count} classes ready in {p.ClassesSeconds:F2} s, running {LoadSeconds.Total:F2} s after asking");
                foreach (var x in start.Warnings)
                    Console.WriteLine($"  {x}");
                // the load's timeline (docs/battle/plan.md step 2.7b): each class's build, compile and art, on the
                // building threads
                var times = p.Classes.Select(kv => (kv.Key, T: Classes.TimingsOf(kv.Value))).OrderByDescending(x => x.T.Total).ToArray();
                Console.WriteLine($"  classes: build {times.Sum(x => x.T.Build):F2} s, compile {times.Sum(x => x.T.Compile):F2} s, " +
                                  $"art {times.Sum(x => x.T.Hook):F2} s of thread time; slowest: " +
                                  string.Join(", ", times.Take(3).Select(x => $"{x.Key} {x.T.Total:F2} s (art {x.T.Hook:F2})")));
            }
        }
        host.Elapse(dt);
        Current = host.Latest();
        Perf.Frame(Current, dt, Generation);
    }

    /// <summary>A ship's name from the scenario, or null.</summary>
    public string? NameOf(int entity) => ScenarioStart?.ShipNames.GetValueOrDefault(entity);

    public void Send(in Command c) => host.Send(c);

    public void Dispose() => host.Dispose();
}
