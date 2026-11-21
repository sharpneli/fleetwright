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
/// when it's picked (<see cref="Select"/>, docs/map-ui/launch.md L0), the world placed on a thread once they're compiled
/// and it's begun (<see cref="Begin"/>) while the old one runs on, then the new one takes its place between frames
/// (<see cref="Generation"/> counts the worlds, so a scene drops what it held of the old one). The art follows while
/// it runs.</para></summary>
public sealed class WorldSession : IDisposable
{
    SimHost host;
    Workers workers;   // the world's (docs/battle/workers.md): one set a world, disposed after its host

    /// <summary>The newest snapshot, taken at the start of this frame. The UI thread's; valid until the next frame.</summary>
    public Snapshot Current { get; private set; }

    /// <summary>The classes in play, shared with the art (<see cref="ShipArtLibrary"/>): the app's, made before the
    /// world. Thread-safe; the map requests designs here and spawns ships by the ids it gives.</summary>
    public ShipClassRegistry Classes { get; }

    /// <summary>The world's AI (admirals and captains). Owned by the sim thread: the UI only builds orders with it
    /// (<see cref="AiSystem.Orders"/>, which reads nothing but its id) and sends them. A new world brings a new one.</summary>
    public AiSystem Ai { get; private set; }

    /// <summary>The player's orders to units, by hand (<see cref="UnitOrders.Hand"/>): what the map gives in play,
    /// which holds a unit against doctrine's standing orders. A dev flag's go by <see cref="AiSystem.Orders"/>.</summary>
    public UnitOrders Orders => Ai.Orders.ByHand;

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

    /// <summary>The scenario loading, if one is (begun, its world not in yet): its name, and its classes ready of all.</summary>
    public (string Name, int Ready, int Of)? Loading => loading is { } l
        ? (l.Scenario.Name, Ready(l.Classes), l.Classes.Count)
        : null;

    /// <summary>The scenario picked (<see cref="Select(string)"/>), its classes building, not yet begun; or null.</summary>
    public Scenario? Selected => selected?.Scenario;

    /// <summary>The file the picked scenario was read from, or null.</summary>
    public string? SelectedPath => selected?.Path;

    /// <summary>The class ids of the scenario begun, else of the one picked (the launch screen's load, a segment each), or
    /// null; and the seconds since it was picked.</summary>
    public Dictionary<string, int>.ValueCollection? LoadClasses => (loading ?? selected)?.Classes.Values;

    public double LoadClock => (loading ?? selected)?.Clock.Elapsed.TotalSeconds ?? 0;

    /// <summary>The same classes by design name.</summary>
    public IReadOnlyDictionary<string, int>? LoadIds => (loading ?? selected)?.Classes;

    /// <summary>A scenario is begun and its world not in yet.</summary>
    public bool Beginning => loading != null;

    /// <summary>Where the picked scenario's classes are: compiled (it may begin), drawn (their art in), of all.</summary>
    public (int Compiled, int Drawn, int Of) SelectedClasses => selected is { } p
        ? (Ready(p.Classes), Drawn(p.Classes), p.Classes.Count)
        : (0, 0, 0);

    /// <summary>The world's instruments on the UI side (docs/battle/plan.md step 2.7b), fed every frame.</summary>
    public WorldPerf Perf { get; } = new();

    /// <summary>The last scenario's failure to load, or null.</summary>
    public string? LoadError { get; private set; }

    sealed class Pending
    {
        public required Scenario Scenario;
        public required Dictionary<string, int> Classes;
        public string? Path;   // the file it was read from, if it was
        public Task<(World World, AiSystem Ai, ScenarioStart Start)>? Task;
        public Func<EndView?>? Judge;
        public int Placed;   // the commands the placing gave: a replay's first (ReplayWriter)
        public bool Replayed;   // a replay played to its moment (LoadReplay): begun paused
        public readonly Stopwatch Clock = Stopwatch.StartNew();   // from its pick
        public double BeginSeconds, ClassesSeconds, PlacedSeconds;
    }

    Pending? selected, loading, drawing;   // picked; begun; the world's, until its art is in

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

    /// <summary>The launch screen's copies edited in the designer and not saved (docs/map-ui/launch.md): this run's
    /// own folder in the temp folder, cleared at the next start (<see cref="ClearUnsaved"/>). Before
    /// <see cref="DesignFolders"/>, which names it (statics start in order).</summary>
    public static readonly string UnsavedFolder =
        Path.Combine(Path.GetTempPath(), "Fleetwright", "Unsaved", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Where a scenario looks for a design after its own Designs/: the shipped ones, then the player's (a setup
    /// may swap one in, docs/map-ui/launch.md), then the copies edited this run and not saved.</summary>
    public static readonly string[] DesignFolders = [Path.Combine(AppContext.BaseDirectory, "Content", "Designs"), Designer.DesignDoc.UserFolder, UnsavedFolder];

    /// <summary>The unsaved copies of runs that have ended taken away (another run's still running are kept).</summary>
    public static void ClearUnsaved()
    {
        var root = Path.GetDirectoryName(UnsavedFolder)!;
        if (!Directory.Exists(root))
            return;
        foreach (var dir in Directory.GetDirectories(root))
            try
            {
                bool running = int.TryParse(Path.GetFileName(dir), out int pid) && pid != Environment.ProcessId && IsRunning(pid);
                if (!running)
                    Directory.Delete(dir, recursive: true);
            }
            catch (Exception x) when (x is IOException or UnauthorizedAccessException)
            {
            }
    }

    static bool IsRunning(int pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>The dev's -smokefield[=0]: the smoke field on or off (docs/battle/smoke.md; else settings.json's Smoke,
    /// on since 2026-10-21).</summary>
    public static bool? SmokeField { get; set; }

    /// <summary>A world with the shipped settings (Content/Sim/settings.json), the app's class registry and the land.
    /// <paramref name="setup"/> runs on it before its thread starts: the dev ships (-ships=) until missions place
    /// forces.</summary>
    public WorldSession(WorldTime start, ulong seed, ShipClassRegistry classes, Shore shore, Action<World>? setup = null)
    {
        Classes = classes;
        Shore = shore;
        settingsJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Content", "Sim", "settings.json"));
        Settings = SimSettings.Parse(settingsJson);
        if (SmokeField is bool smoke)
            Settings = Settings with { Smoke = smoke };
        var w = new World(start, seed, Settings, classes, shore);
        Ai = new AiSystem(w);
        w.Apply(Command.SetPlayerSide(0));   // the dev world's player: side 0, the spawner's first
        setup?.Invoke(w);
        w.Workers = workers = new Workers(Workers.AppCount);
        host = Start(w);
        Current = host.Latest();
    }

    readonly string settingsJson;   // as read: a replay carries it

    // ------------------------------------------------------------------ replays

    /// <summary>The replay being written of the scenario running (docs/missions/plan.md: the file plus the command log), or
    /// null (no scenario, or it couldn't be opened). Its commands come from the host (<see cref="SimHost.TakeApplied"/>) each
    /// frame; the world's time every few seconds; its end, time and hash, when the world gives way or the app quits.</summary>
    public string? ReplayPath => replay?.Path ?? lastReplay;

    string? lastReplay;   // the one ended last: a crash report after the session's gone names it

    ReplayWriter? replay;
    World? replayWorld;   // read only once its host has stopped, for the end's hash
    readonly List<StampedCommand> taken = new(64);
    double replayClock;
    bool errorRecorded, stoppedReported;

    void BeginReplay(Pending p, World w)
    {
        try
        {
            replay = ReplayWriter.Begin(p.Scenario, DesignFolders, settingsJson, SmokeField, AppLog.Version, p.Placed);
            replayWorld = w;
            Console.WriteLine($"  replay: {replay.Path}");
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or ScenarioException)
        {
            Console.Error.WriteLine($"  replay: not recorded ({x.Message})");
            (replay, replayWorld) = (null, null);
        }
    }

    /// <summary>The commands the host has passed on since the last frame into the replay, every few seconds how far the
    /// game has got, and the sim's error if it stopped.</summary>
    void RecordReplay(double dt)
    {
        if (replay == null)
            return;
        try
        {
            host.TakeApplied(taken);
            foreach (var c in taken)
                replay.Command(c);
            taken.Clear();
            if ((replayClock += dt) >= 5)
            {
                replayClock = 0;
                replay.Progress(Current.Time);
            }
            if (Current.Error is { } error && !errorRecorded)
            {
                errorRecorded = true;
                replay.Error(error);
            }
        }
        catch (IOException x)
        {
            Console.Error.WriteLine($"replay: stopped recording ({x.Message})");
            replay.Dispose();
            (replay, replayWorld) = (null, null);
        }
    }

    /// <summary>The replay's end, once its world's host has stopped (<paramref name="stopped"/>): the last commands, the
    /// time and the world's hash (none after the sim's error: the world stopped part way through a step).</summary>
    void EndReplay(SimHost stopped)
    {
        if (replay == null)
            return;
        try
        {
            stopped.TakeApplied(taken);
            foreach (var c in taken)
                replay.Command(c);
            taken.Clear();
            if (replayWorld is { } w && !errorRecorded)
            {
                var clock = Stopwatch.StartNew();
                replay.End(w.Time, w.ComputeHash());
                Console.WriteLine($"replay: ended at {w.Time.ToUtc():HH:mm:ss}, {w.CommandLog.Count} commands ({clock.Elapsed.TotalMilliseconds:F0} ms for the hash)");
            }
        }
        catch (IOException x)
        {
            Console.Error.WriteLine($"replay: its end not written ({x.Message})");
        }
        replay.Dispose();
        lastReplay = replay.Path;
        (replay, replayWorld) = (null, null);
        errorRecorded = false;
    }

    /// <summary>Opens a replay (<see cref="Replay"/>) as a new world: its scenario made again from the file, its classes asked
    /// for here, the world placed and the commands played to <paramref name="at"/> (a time of day: the first at or after the
    /// start; null: as far as the record got) on a thread, then swapped in paused, the player's from there. The world
    /// records a replay of its own, the played commands in it. A replay that can't be played is <see cref="LoadError"/>.</summary>
    public void LoadReplay(string path, TimeSpan? at)
    {
        LoadError = null;
        try
        {
            var r = Replay.Read(path);
            if (r.Build != AppLog.Version)
                Console.WriteLine($"replay: recorded by {r.Build}, played by {AppLog.Version}: it may go its own way");
            string scratch = Path.Combine(Path.GetTempPath(), "Fleetwright", "Replay", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var s = r.Unpack(scratch);
            var to = r.Reached;
            if (at is { } t)
            {
                var day = r.Start.ToUtc().Date + t;
                to = WorldTime.FromUtc(day < r.Start.ToUtc() ? day.AddDays(1) : day);
            }
            var p = new Pending { Scenario = s, Classes = ScenarioLoader.RequestClasses(s, Classes, []), Path = path, Replayed = true };
            p.Task = Task.Run(() =>
            {
                var rb = r.Rebuild(s, p.Classes, Classes, Shore);   // waits for the classes
                p.ClassesSeconds = p.Clock.Elapsed.TotalSeconds;
                p.Placed = rb.World.CommandLog.Count;
                var clock = Stopwatch.StartNew();
                int next = r.Play(rb.World, r.Placed, to);
                string error = r.Error is { } e ? $"; it stopped on: {e.Split('\n')[0]}" : "";
                Console.WriteLine($"replay {Path.GetFileName(path)}: {next - r.Placed} of {r.Commands.Count - r.Placed} commands played to " +
                                  $"{to.ToUtc():HH:mm:ss} in {clock.Elapsed.TotalSeconds:F1} s" + (r.End is null ? " (it has no end: a crash?)" : "") + error);
                p.Judge = MakeJudge(s, rb.World, rb.Ai, rb.Start);
                p.PlacedSeconds = p.Clock.Elapsed.TotalSeconds;
                return (rb.World, rb.Ai, rb.Start);
            });
            (selected, loading) = (null, p);
        }
        catch (Exception x) when (x is ReplayException or ScenarioException or IOException or UnauthorizedAccessException)
        {
            LoadError = x.Message;
            Console.Error.WriteLine($"replay {path}: {x.Message}");
        }
    }

    /// <summary>Starts a world's thread and waits for its first snapshot, so no frame shows an empty one.</summary>
    /// <summary>A scenario's judge for the host (docs/map-ui/battle-hud.md U7): its <see cref="ScenarioEnd"/>, checked on
    /// the sim thread; once it's over, the end with the truth on both sides: each side's count and each ship's fate.</summary>
    static Func<EndView?> MakeJudge(Scenario s, World w, AiSystem ai, ScenarioStart start)
    {
        var end = new ScenarioEnd(s, w, ai);
        return () =>
        {
            if (!end.Check())
                return null;
            var sides = end.Result();
            var lost = new Dictionary<int, PlayLine>();
            foreach (var l in end.Timeline)
                if (l.Kind == PlayEvent.Lost)
                    lost.TryAdd(l.Entity, l);
            var ships = new List<ShipEnd>();
            var store = w.Ships.Store;
            var seen = new HashSet<int>();
            for (int slot = 0; slot < store.Slots; slot++)
            {
                if (!store.IsAlive(slot))
                    continue;
                var x = store[slot];
                seen.Add(x.Entity);
                var k = store.Combat.Condition(slot);
                bool gone = lost.TryGetValue(x.Entity, out var l);
                ships.Add(new ShipEnd(x.Entity, x.Side, start.ShipNames.GetValueOrDefault(x.Entity) ?? $"{x.Handling.Class.Name} #{x.Entity}",
                    gone, gone ? (Sim.Combat.LossKind)l.Count : Sim.Combat.LossKind.None, gone ? l.Ms : 0, k is { Damaged: true }, k?.Hits ?? 0,
                    end.GivenUpOf(x.Entity), end.Fate(x.Entity) == ShipFate.Taken));
            }
            foreach (var (entity, l) in lost)
                if (!seen.Contains(entity))
                    ships.Add(new ShipEnd(entity, end.SideOf(entity), start.ShipNames.GetValueOrDefault(entity) ?? $"#{entity}", true,
                        (Sim.Combat.LossKind)l.Count, l.Ms, true, 0, end.GivenUpOf(entity)));
            return new EndView
            {
                Reason = end.Reason.ToString().ToLowerInvariant(),
                EndMs = end.EndMs,
                Sides = [.. sides.Select(r => new SideEnd(r.Name, r.Ships, r.Tonnes, r.Lost, r.LostTonnes, r.Damaged, r.Afloat, r.AimsMet, r.Hits, r.Torpedoes, r.Killed, r.Saved, r.Score))],
                Ships = [.. ships.OrderBy(x => x.Side).ThenBy(x => x.Entity)],
            };
        };
    }

    static SimHost Start(World w, Func<EndView?>? judge = null)
    {
        var h = new SimHost(w) { Judge = judge };
        h.Start();
        var s = h.Latest();
        for (int k = 0; k < 1000 && s.Sequence == 0; k++)
        {
            Thread.Sleep(1);
            s = h.Latest();
        }
        return h;
    }

    /// <summary>Picks a scenario from its file (<see cref="Select(Scenario, string?)"/>); a file that isn't one is
    /// <see cref="LoadError"/>. The file picked already stays picked, not read again.</summary>
    public void Select(string path)
    {
        if (selected?.Path == path)
            return;
        try
        {
            Select(Sim.Missions.Scenario.Load(path), path);
        }
        catch (Exception x) when (x is ScenarioException or IOException)
        {
            (LoadError, selected) = (x.Message, null);
        }
    }

    /// <summary>Picks a scenario: its classes requested now, so they build while the player reads it
    /// (docs/map-ui/launch.md L0), the player's side's art first. A class asked for before is kept (the registry is by
    /// design hash), so switching costs only what's new.</summary>
    public void Select(Scenario s, string? path = null)
    {
        LoadError = null;
        try
        {
            var classes = ScenarioLoader.RequestClasses(s, Classes, DesignFolders);
            var p = selected = new Pending { Scenario = s, Classes = classes, Path = path };
            foreach (var u in s.Units)
                if (u.Side == s.Player)
                    PreferArt(u, classes);
            // the world placed as soon as the classes are compiled, so Begin only swaps it in (the placing is a pure
            // function of the scenario: the matchups warmed, every gun's tables, ~1.5 s at Jutland)
            p.Task = Task.Run(() =>
            {
                foreach (int id in p.Classes.Values)
                    Classes.Get(id);   // waits for each compile
                p.ClassesSeconds = p.Clock.Elapsed.TotalSeconds;
                var w = new World(s.Start, s.Seed, Settings, Classes, Shore);
                var ai = new AiSystem(w);
                var start = ScenarioLoader.Apply(s, w, ai, p.Classes);
                p.Placed = w.CommandLog.Count;
                p.Judge = MakeJudge(s, w, ai, start);
                p.PlacedSeconds = p.Clock.Elapsed.TotalSeconds;
                return (w, ai, start);
            });
        }
        catch (Exception x) when (x is ScenarioException or IOException or System.Text.Json.JsonException)
        {
            (LoadError, selected) = (x.Message, null);
        }
    }

    void PreferArt(ScenarioUnit u, Dictionary<string, int> classes)
    {
        foreach (var s in u.Ships)
            Classes.Prefer(classes[s.Design]);
        foreach (var c in u.Units)
            PreferArt(c, classes);
    }

    /// <summary>Reads a scenario file and begins it (<see cref="Select(string)"/>, <see cref="Begin"/>).</summary>
    public void Load(string path)
    {
        Select(path);
        if (selected?.Path == path)
            Begin();
    }

    /// <summary>Picks a scenario and begins it.</summary>
    public void Load(Scenario s)
    {
        Select(s);
        if (selected?.Scenario == s)
            Begin();
    }

    /// <summary>Begins the picked scenario: its world, placed on a thread once its classes are compiled (their art
    /// follows while it runs), replaces this one at the first frame it's ready (<see cref="Frame"/>). A load already
    /// begun is dropped. The pick is used up: playing it again picks it again.</summary>
    public void Begin()
    {
        if (selected is not { } p)
            return;
        (selected, loading) = (null, p);
        p.BeginSeconds = p.Clock.Elapsed.TotalSeconds;
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
                // (a replay's paused at its moment, the player's from there)
                if (p.Replayed)
                    askedPaused = true;
                w.Apply(askedSpeed);
                if (askedPaused)
                    w.Apply(Command.Pause());
                foreach (var rule in interest)
                    if (rule is { } r)
                        w.Apply(r);   // and the player's interest rules (what an encounter does to the clock)
                var (old, oldWorkers) = (host, workers);
                w.Workers = workers = new Workers(Workers.AppCount);
                host = Start(w, p.Judge);
                host.Truth = truth;
                host.Detail = detail;
                old.Dispose();
                oldWorkers.Dispose();   // the old sim thread is done with them
                EndReplay(old);
                stoppedReported = false;
                (Ai, Scenario, ScenarioStart) = (ai, p.Scenario, start);
                Generation++;
                LoadSeconds = (p.Clock.Elapsed.TotalSeconds, p.ClassesSeconds);
                string begun = p.BeginSeconds >= 0.01 ? $", begun {p.BeginSeconds:F2} s after" : "";
                Console.WriteLine($"scenario {p.Scenario.Name}: {start.Ships} ships in {start.Groups} groups, " +
                                  $"{p.Classes.Count} classes compiled in {p.ClassesSeconds:F2} s, placed {p.PlacedSeconds:F2}, running {LoadSeconds.Total:F2} s after asking{begun}");
                foreach (var x in start.Warnings)
                    Console.WriteLine($"  {x}");
                // the load's timeline (docs/battle/plan.md step 2.7b): each class's build and compile on the building
                // threads; the art's when it's in
                var times = p.Classes.Select(kv => (kv.Key, T: Classes.TimingsOf(kv.Value))).OrderByDescending(x => x.T.Build + x.T.Compile).ToArray();
                Console.WriteLine($"  classes: build {times.Sum(x => x.T.Build):F2} s, compile {times.Sum(x => x.T.Compile):F2} s of thread time; slowest: " +
                                  string.Join(", ", times.Take(3).Select(x => $"{x.Key} {x.T.Build + x.T.Compile:F2} s")) +
                                  $"; art {Drawn(p.Classes)} of {p.Classes.Count} in");
                Console.WriteLine($"  memory: {Memory()}");
                BeginReplay(p, w);
                drawing = p;
            }
        }
        if (drawing is { } d && Drawn(d.Classes) == d.Classes.Count)
        {
            drawing = null;
            var times = d.Classes.Select(kv => (kv.Key, T: Classes.TimingsOf(kv.Value))).OrderByDescending(x => x.T.Hook).ToArray();
            Console.WriteLine($"  art in {d.Clock.Elapsed.TotalSeconds:F2} s after asking: {times.Sum(x => x.T.Hook):F2} s of thread time; slowest: " +
                              string.Join(", ", times.Take(3).Select(x => $"{x.Key} {x.T.Hook:F2} s")) + $"; memory: {Memory()}");
        }
        host.Elapse(dt);
        Current = host.Latest();
        RecordReplay(dt);
        if (Current.Error is { } simError && !stoppedReported)
        {
            stoppedReported = true;
            AppLog.Crash(null, "the sim thread stopped on an error", fatal: false, detail: simError);
        }
        // every building thread while no world runs: an empty one, or paused (docs/map-ui/launch.md L0)
        Classes.Idle = Current.Paused || Current.Ships.Count == 0;
        Clock(dt);
        Perf.Frame(Current, dt, Generation, DrawMs);
    }

    int Ready(Dictionary<string, int> ids)
    {
        int n = 0;
        foreach (int id in ids.Values)
            n += Classes.IsReady(id) ? 1 : 0;
        return n;
    }

    int Drawn(Dictionary<string, int> ids)
    {
        int n = 0;
        foreach (int id in ids.Values)
            n += Classes.IsDrawn(id) ? 1 : 0;
        return n;
    }

    /// <summary>The process's memory for the load's lines: the managed heap now, the working set now and at its peak.</summary>
    static string Memory()
    {
        using var p = Process.GetCurrentProcess();
        return $"heap {GC.GetTotalMemory(false) / 1048576.0:F0} MB, working set {p.WorkingSet64 / 1048576.0:F0} MB (peak {p.PeakWorkingSet64 / 1048576.0:F0} MB)";
    }

    /// <summary>The world time the map draws at this frame, ms (docs/battle/plan.md step 2.7c): the newest snapshot's
    /// time, smoothed. The sim publishes on its own beat (every few ms while behind), so a frame gets one snapshot's
    /// worth of motion or two; drawn at its snapshot's time, a ship at 1,000x lurched. This clock runs on at the speed
    /// the world is keeping and leans a tenth of the way to each new snapshot; it never runs back, gets no more than a
    /// frame's step ahead of a new snapshot, runs on up to <see cref="LeadS"/> of real time past one while no new one
    /// comes (a sim stall: workers.md W16e), and jumps to it after a pause, a load or a longer stall. Its rate is
    /// the achieved speed, never above the asked: a drop from a run or the flat-out fine tier (docs/map-ui/input.md) is
    /// in the snapshot at once, while the achieved speed is measured over a window and lags; and a clock left far ahead
    /// by the old rate jumps back to the world. Poses are extrapolated to it along their arcs (ShipsView.PoseAt).</summary>
    public long DrawMs { get; private set; }

    double drawMs = double.NaN;
    int drawGeneration = -1;
    long drawSequence = -1;

    /// <summary>How far past the newest snapshot the clock may run on while no new one comes, s of real time (docs/battle/workers.md
    /// W16e): a stall of the sim's (at a high speed it waits on a magazine's race) shows as the ships going on along their arcs, not a
    /// freeze and a jump.</summary>
    const double LeadS = 0.3;

    void Clock(double dt)
    {
        var s = Current;
        double latest = s.Time.Ms;
        double rate = s.Paused ? 0 : Math.Clamp(s.AchievedSpeed, 0, s.Speed);
        double step = rate * dt * 1000, lead = rate * LeadS * 1000, far = Math.Max(4 * step, 1) * 4;
        bool fresh = s.Sequence != drawSequence;
        if (double.IsNaN(drawMs) || drawGeneration != Generation || s.Paused || latest - drawMs > far || drawMs - latest > Math.Max(far, lead + step))
            drawMs = latest;   // a new world, a pause, a stall, or ahead from a faster clock: where the world is
        else
        {
            // on at the speed the world kept; a new snapshot leaned towards, and the clock never more than a frame past it; between
            // snapshots on as far as the lead
            double next = drawMs + step;
            if (fresh)
                next += (latest - next) * 0.1;
            drawMs = Math.Clamp(next, drawMs, Math.Max(latest + (fresh ? step : lead), drawMs));
        }
        (drawGeneration, drawSequence) = (Generation, s.Sequence);
        DrawMs = (long)Math.Round(drawMs);
    }

    /// <summary>A ship's name from the scenario, or null.</summary>
    public string? NameOf(int entity) => ScenarioStart?.ShipNames.GetValueOrDefault(entity);

    /// <summary>The snapshots show the truth, every ship (a dev's view), not the player's picture; kept across a scenario's
    /// new world.</summary>
    public bool Truth
    {
        get => truth;
        set
        {
            truth = value;
            host.Truth = value;
        }
    }

    /// <summary>The ships the map's cards want detailed (<see cref="Snapshot.Details"/>): set when they change, kept
    /// across a new world.</summary>
    public void Detail(ReadOnlySpan<int> entities)
    {
        if (entities.SequenceEqual(detail))
            return;
        detail = entities.ToArray();   // only when the cards change
        host.Detail = detail;
    }
    int[] detail = [];

    bool truth;

    /// <summary>Sends a command to the world; the clock's (speed, pause, the interest rules) are remembered, so a
    /// scenario's new world starts as asked even before the old world's snapshot shows it.</summary>
    public void Send(in Command c)
    {
        if (c.System == 0)
            switch ((WorldCommand)c.Code)
            {
                case WorldCommand.SetSpeed:
                    // with its tier held, if it was; a run, as it'll end (the coarse tier's hold kept, as a halt keeps it)
                    askedSpeed = c.Y > 0 ? Command.SetSpeed(c.Y, (ClockHold)c.A == ClockHold.Coarse ? ClockHold.Coarse : ClockHold.None) : c;
                    break;
                case WorldCommand.Step:
                    // a new world takes the step from its start (the pause it ends in is the world's, as a rule's is)
                    (askedSpeed, askedPaused) = (c, false);
                    break;
                case WorldCommand.Pause:
                    askedPaused = true;
                    break;
                case WorldCommand.Resume:
                    askedPaused = false;
                    break;
                case WorldCommand.SetInterest when c.Entity is >= 0 and < 2:
                    interest[c.Entity] = c;
                    break;
            }
        host.Send(c);
    }

    Command askedSpeed = Command.SetSpeed(1);
    bool askedPaused;
    readonly Command?[] interest = new Command?[2];   // the player's interest rules, by kind

    public void Dispose()
    {
        host.Dispose();
        workers.Dispose();
        EndReplay(host);
    }
}
