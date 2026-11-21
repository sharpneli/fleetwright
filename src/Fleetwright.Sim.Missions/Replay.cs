using System.Globalization;
using System.Text.Json;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Missions;

/// <summary>A replay that can't be read or played back as recorded.</summary>
public sealed class ReplayException(string message) : Exception(message);

/// <summary>A scenario as it was played (docs/missions/plan.md: "a replay is the file plus the command log"), read back
/// from a <c>.replay</c> file (<see cref="ReplayWriter"/>). It carries all a world needs to be made again: the
/// scenario's files and the setup, the designs, the doctrine and the settings, the start and the seed; then every
/// command applied from outside with the time it landed (the placing's first, <see cref="Placed"/> of them, then the
/// player's), how far the game got, and its end's hash when it ended cleanly. Commands the sim gives itself (the AI's)
/// aren't in it: the same AI gives them again (<see cref="World.Issue"/>).</summary>
public sealed class Replay
{
    public const int FormatVersion = 1;

    /// <summary>The build that recorded it ("1.0.0+df91dc66"): another build may play it differently.</summary>
    public required string Build { get; init; }
    public required string ScenarioName { get; init; }
    public required string Recorded { get; init; }
    public required WorldTime Start { get; init; }
    public required ulong Seed { get; init; }

    /// <summary>How many of <see cref="Commands"/> the scenario's placing gave (<see cref="ScenarioLoader.Apply"/>).</summary>
    public required int Placed { get; init; }
    public required IReadOnlyList<(string Name, string Json)> Files { get; init; }
    public string? Setup { get; init; }
    public required IReadOnlyDictionary<string, string> Designs { get; init; }
    public string? Doctrine { get; init; }
    public required string Settings { get; init; }
    public bool? Smoke { get; init; }

    public List<StampedCommand> Commands { get; } = [];

    /// <summary>The furthest world time recorded: a command's, the game's progress or its end.</summary>
    public WorldTime Reached { get; private set; }

    /// <summary>The world's time and hash when the game ended cleanly (a new scenario, the app quit); null after a crash.</summary>
    public (WorldTime Time, ulong Hash)? End { get; private set; }

    /// <summary>The sim's error, if it stopped on one.</summary>
    public string? Error { get; private set; }

    /// <summary>The line the record breaks off at (a crash cut it short), 0 if it's whole.</summary>
    public int Cut { get; private set; }

    public static Replay Read(string path)
    {
        Replay? r = null;
        int line = 0;
        try
        {
            foreach (string text in File.ReadLines(path))
            {
                line++;
                if (text.Length == 0)
                    continue;
                using var doc = JsonDocument.Parse(text);
                var e = doc.RootElement;
                if (r == null)
                {
                    r = Header(e);
                    continue;
                }
                if (e.TryGetProperty("c", out var c))
                {
                    var cmd = ReadCommand(c);
                    r.Commands.Add(cmd);
                    r.Reached = Max(r.Reached, cmd.At);
                }
                else if (e.TryGetProperty("t", out var t))
                    r.Reached = Max(r.Reached, new WorldTime(t.GetInt64()));
                else if (e.TryGetProperty("end", out var end))
                {
                    var at = new WorldTime(end.GetInt64());
                    r.End = (at, ulong.Parse(e.GetProperty("hash").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    r.Reached = Max(r.Reached, at);
                }
                else if (e.TryGetProperty("error", out var err))
                    r.Error = err.GetString();
            }
        }
        catch (Exception x) when (x is JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            // a line cut short by a crash ends the record there; anything else is a broken file
            if (r == null)
                throw new ReplayException($"{Path.GetFileName(path)}: not a replay ({x.Message})");
            r.Cut = line;
        }
        return r ?? throw new ReplayException($"{Path.GetFileName(path)}: empty");
    }

    static WorldTime Max(WorldTime a, WorldTime b) => a.Ms >= b.Ms ? a : b;

    static Replay Header(JsonElement e)
    {
        if (!e.TryGetProperty("replay", out var v) || v.GetInt32() != FormatVersion)
            throw new ReplayException($"not a replay of version {FormatVersion}");
        var files = e.GetProperty("files").EnumerateArray().Select(f => (f.GetProperty("name").GetString()!, f.GetProperty("json").GetString()!)).ToList();
        var designs = e.GetProperty("designs").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
        var start = new WorldTime(e.GetProperty("start").GetInt64());
        return new Replay
        {
            Build = e.GetProperty("build").GetString()!,
            ScenarioName = e.GetProperty("scenario").GetString()!,
            Recorded = e.GetProperty("recorded").GetString()!,
            Start = start,
            Seed = ulong.Parse(e.GetProperty("seed").GetString()!, CultureInfo.InvariantCulture),
            Placed = e.GetProperty("placed").GetInt32(),
            Files = files,
            Setup = e.TryGetProperty("setup", out var su) && su.ValueKind == JsonValueKind.String ? su.GetString() : null,
            Designs = designs,
            Doctrine = e.TryGetProperty("doctrine", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null,
            Settings = e.GetProperty("settings").GetString()!,
            Smoke = e.TryGetProperty("smoke", out var sm) && sm.ValueKind is JsonValueKind.True or JsonValueKind.False ? sm.GetBoolean() : null,
            Reached = start,
        };
    }

    static StampedCommand ReadCommand(JsonElement c)
    {
        var a = c.EnumerateArray().ToArray();
        if (a.Length != 8)
            throw new FormatException($"a command of {a.Length} fields (want 8)");
        return new StampedCommand(new WorldTime(a[0].GetInt64()),
            new Command((byte)a[1].GetInt32(), (ushort)a[2].GetInt32(), a[3].GetInt32(), a[4].GetInt64(), Real(a[5]), a[6].GetInt32(), Real(a[7])));
    }

    static double Real(JsonElement e) => e.ValueKind == JsonValueKind.String
        ? double.Parse(e.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture)
        : e.GetDouble();

    /// <summary>The world made again, placed as it was and not yet run (<see cref="Unpack"/>, the classes requested from
    /// <paramref name="registry"/>, then <see cref="Rebuild(Scenario, IReadOnlyDictionary{string, int}, ShipClassRegistry, Shore?)"/>).
    /// Play the rest with <see cref="Play"/>.</summary>
    public Rebuilt Rebuild(ShipClassRegistry registry, Shore? shore, string scratch)
    {
        var s = Unpack(scratch);
        return Rebuild(s, ScenarioLoader.RequestClasses(s, registry, []), registry, shore);
    }

    /// <summary>The scenario read back from the replay's files, written under <paramref name="scratch"/> (emptied first):
    /// the scenario's own and its "from"s, the setup, the designs in its Designs/, the doctrine beside it; at the
    /// recorded start and seed. Its designs are found in its own folder (<see cref="ScenarioLoader.RequestClasses"/>
    /// needs no other).</summary>
    public Scenario Unpack(string scratch)
    {
        if (Files.Count == 0)
            throw new ReplayException("no scenario in it");
        if (Directory.Exists(scratch))
            Directory.Delete(scratch, recursive: true);
        string scenarios = Path.Combine(scratch, "Scenarios"), designs = Path.Combine(scenarios, "Designs"), sim = Path.Combine(scratch, "Sim");
        Directory.CreateDirectory(designs);
        Directory.CreateDirectory(sim);
        foreach (var (name, json) in Files)
            File.WriteAllText(Path.Combine(scenarios, name + ".json"), json);
        foreach (var (name, json) in Designs)
            File.WriteAllText(Path.Combine(designs, name + ".json"), json);
        if (Doctrine != null)
            File.WriteAllText(Path.Combine(sim, "doctrine.json"), Doctrine);
        string path = Path.Combine(scenarios, Files[0].Name + ".json");
        if (Setup != null)
            File.WriteAllText(path = Path.Combine(scenarios, "_setup.json"), Setup);
        try
        {
            return Scenario.Load(path) with { Start = Start, Seed = Seed };
        }
        catch (ScenarioException x)
        {
            throw new ReplayException($"its scenario doesn't read in this build: {x.Message}");
        }
    }

    /// <summary>The unpacked scenario's world placed (its classes waited for) and the placing checked against the record.</summary>
    public Rebuilt Rebuild(Scenario s, IReadOnlyDictionary<string, int> classes, ShipClassRegistry registry, Shore? shore)
    {
        var settings = SimSettings.Parse(Settings);
        if (Smoke is bool on)
            settings = settings with { Smoke = on };
        foreach (var (name, id) in classes)
            if (registry.Get(id) == null)
                throw new ReplayException($"design {name} doesn't build: {registry.ErrorOf(id)}");
        var w = new World(s.Start, s.Seed, settings, registry, shore);
        var ai = new AiSystem(w);
        var start = ScenarioLoader.Apply(s, w, ai, classes);
        var log = w.CommandLog;
        if (log.Count != Placed)
            throw new ReplayException($"the scenario places {log.Count} commands in this build, {Placed} when recorded (by {Build})");
        for (int i = 0; i < Placed; i++)
        {
            var (a, b) = (log[i].Command, Commands[i].Command);
            // the registry's ids (a spawn's class, a loadout) are the app's numbering: everything else as recorded
            bool ids = a.System == ShipSystem.Id && a.Code is (ushort)ShipCommand.Spawn or (ushort)ShipCommand.Load;
            if (a.System != b.System || a.Code != b.Code || a.Entity != b.Entity || log[i].At != Commands[i].At ||
                !ids && a != b || ids && (a.A != b.A || a.X != b.X || a.Y != b.Y))
                throw new ReplayException($"the scenario places differently in this build: command {i} is {a}, recorded {b} (by {Build})");
        }
        return new Rebuilt(s, w, ai, start, new Dictionary<string, int>(classes, StringComparer.Ordinal));
    }

    /// <summary>Plays the recorded commands from <paramref name="from"/> on, each at its time, up to
    /// <paramref name="to"/> (the world advanced there too): the index of the first not played. A ship spawned from the
    /// dev panel names a class by the recording app's ids, which this world doesn't share: an error.</summary>
    public int Play(World w, int from, WorldTime to)
    {
        int i = from;
        for (; i < Commands.Count && Commands[i].At <= to; i++)
        {
            var (at, c) = Commands[i];
            if (c.System == ShipSystem.Id && c.Code is (ushort)ShipCommand.Spawn or (ushort)ShipCommand.Load)
                throw new ReplayException($"at {at.ToUtc():HH:mm:ss}: a ship {(c.Code == (ushort)ShipCommand.Spawn ? "spawned" : "loaded")} from the dev panel: a replay holds the scenario's ships only");
            w.AdvanceTo(at);
            w.Apply(c);
        }
        w.AdvanceTo(to);
        return i;
    }
}

/// <summary>A replay's world made again (<see cref="Replay.Rebuild"/>): the scenario, the world placed and its AI, what the
/// placing reported, the class ids by design name.</summary>
public sealed record Rebuilt(Scenario Scenario, World World, AiSystem Ai, ScenarioStart Start, Dictionary<string, int> Classes);

/// <summary>Records a scenario's play as a <see cref="Replay"/> file, a line a record, each flushed as it's written so a
/// crash leaves everything up to it: the header (the scenario and all it needs), then the commands as they land, the
/// game's progress, and its end. Written by the app's thread from the commands the sim host passes on
/// (<see cref="SimHost.TakeApplied"/>), never from the sim's.</summary>
public sealed class ReplayWriter : IDisposable
{
    /// <summary>The newest replays kept; older ones are deleted when a new one starts.</summary>
    const int Keep = 30;

    public static string Folder { get; } =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fleetwright", "Replays");

    readonly FileStream file;
    readonly Utf8JsonWriter json;

    public string Path { get; }

    ReplayWriter(string path)
    {
        Path = path;
        file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        json = new Utf8JsonWriter(file, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>A new replay of <paramref name="s"/> in <see cref="Folder"/> (or <paramref name="folder"/>), its header
    /// written: the scenario's files and setup, the designs as found in <paramref name="designFolders"/>, the doctrine,
    /// the settings' file as read and the smoke field's override, the build. <paramref name="placed"/>: the commands the
    /// placing gave, the first of those to come.</summary>
    public static ReplayWriter Begin(Scenario s, IReadOnlyList<string> designFolders, string settingsJson, bool? smoke, string build, int placed,
        string? folder = null)
    {
        folder ??= Folder;
        Directory.CreateDirectory(folder);
        Prune(folder);
        string stem = new([.. s.Name.Select(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-' ? ch : '_')]);
        var w = new ReplayWriter(System.IO.Path.Combine(folder, $"{DateTime.Now:yyyyMMdd-HHmmss} {stem.Trim()}.replay"));
        var played = s.Written ?? s;
        string? setup = s.Setup is { } st ? (st.From.Length > 0 ? st : st with { From = played.Files[0].Name }).ToJson(played) : null;
        var j = w.json;
        j.WriteStartObject();
        j.WriteNumber("replay", Replay.FormatVersion);
        j.WriteString("build", build);
        j.WriteString("scenario", s.Name);
        j.WriteString("recorded", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        j.WriteNumber("start", s.Start.Ms);
        j.WriteString("seed", s.Seed.ToString(CultureInfo.InvariantCulture));
        j.WriteNumber("placed", placed);
        j.WriteStartArray("files");
        foreach (var (name, text) in played.Files)
        {
            j.WriteStartObject();
            j.WriteString("name", name);
            j.WriteString("json", text);
            j.WriteEndObject();
        }
        j.WriteEndArray();
        if (setup != null)
            j.WriteString("setup", setup);
        j.WriteStartObject("designs");
        foreach (var (name, path) in ScenarioLoader.DesignPaths(s, designFolders))
            j.WriteString(name, File.ReadAllText(path));
        j.WriteEndObject();
        if (ScenarioLoader.PresetsPath(s) is { } doctrine)
            j.WriteString("doctrine", File.ReadAllText(doctrine));
        j.WriteString("settings", settingsJson);
        if (smoke is bool on)
            j.WriteBoolean("smoke", on);
        j.WriteEndObject();
        w.Line();
        return w;
    }

    /// <summary>A command as it landed.</summary>
    public void Command(in StampedCommand sc)
    {
        var c = sc.Command;
        json.WriteStartObject();
        json.WriteStartArray("c");
        json.WriteNumberValue(sc.At.Ms);
        json.WriteNumberValue(c.System);
        json.WriteNumberValue(c.Code);
        json.WriteNumberValue(c.Entity);
        json.WriteNumberValue(c.A);
        Real(c.X);
        json.WriteNumberValue(c.B);
        Real(c.Y);
        json.WriteEndArray();
        json.WriteEndObject();
        Line();
    }

    /// <summary>How far the game has got: a crash's replay plays to the last of these.</summary>
    public void Progress(WorldTime t)
    {
        json.WriteStartObject();
        json.WriteNumber("t", t.Ms);
        json.WriteEndObject();
        Line();
    }

    /// <summary>The game's clean end: the world's time and hash, for a replay to be checked against.</summary>
    public void End(WorldTime t, ulong hash)
    {
        json.WriteStartObject();
        json.WriteNumber("end", t.Ms);
        json.WriteString("hash", hash.ToString("x16", CultureInfo.InvariantCulture));
        json.WriteEndObject();
        Line();
    }

    /// <summary>The sim's error: it stopped there.</summary>
    public void Error(string text)
    {
        json.WriteStartObject();
        json.WriteString("error", text);
        json.WriteEndObject();
        Line();
    }

    // a double's shortest round trip, or its text where JSON has no number for it (NaN, the infinities)
    void Real(double x)
    {
        if (double.IsFinite(x))
            json.WriteNumberValue(x);
        else
            json.WriteStringValue(x.ToString("R", CultureInfo.InvariantCulture));
    }

    void Line()
    {
        json.Flush();
        json.Reset();
        file.WriteByte((byte)'\n');
        file.Flush();
    }

    static void Prune(string folder)
    {
        try
        {
            foreach (var old in new DirectoryInfo(folder).GetFiles("*.replay").OrderByDescending(f => f.LastWriteTimeUtc).Skip(Keep - 1))
                old.Delete();
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        json.Dispose();
        file.Dispose();
    }
}
