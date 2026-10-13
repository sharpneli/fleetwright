using System.Globalization;
using System.Text.Json;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Missions;

/// <summary>A scenario (docs/battle/plan.md step 2.7a): forces placed on the globe at a moment, with their orders, from a
/// JSON file (<c>Content/Scenarios/*.json</c>). The missions' first scaffolding (docs/missions/plan.md "A mission, as
/// data"): the start time, the sides, and the forces as a tree of units, each a force of units or a squadron of ships,
/// in a formation; the top units placed and ordered, every ship below spawned on its station. Objectives, events and
/// pictures come with the missions. Plain data: <see cref="ScenarioLoader"/> gives it to a world.
/// <para>The file:</para>
/// <code>
/// { "scenario": 1, "name": "Jutland", "brief": "...", "start": "1916-05-31T14:00", "seed": 1916,
///   "camera": [56.8, 5.2, 400], "sides": ["Britain", "Germany", "Neutral"],
///   "peace": [["Britain", "Neutral"], ["Germany", "Neutral"]], "player": "Britain",
///   "units": [
///     { "name": "Battlecruiser Fleet", "side": 0, "at": [56.77, 4.6], "course": 112, "kn": 19,
///       "formation": "column", "spacing": 3000,
///       "units": [
///         { "name": "1st Battlecruiser Squadron", "formation": "column", "spacing": 500,
///           "ships": [ { "design": "lion", "names": ["Lion", "Princess Royal", "Queen Mary"] } ] } ] },
///     { "name": "Patrol", "side": 0, "at": [58, 2], "course": 0, "kn": 10, "route": [[59, 2], [58, 2]], "loop": true,
///       "ships": [ { "design": "light_cruiser", "count": 2 } ] } ] }
/// </code>
/// <para>Sides are at war with each other unless a pair is listed in <c>"peace"</c>. A unit's side, formation and spacing default to its parent's (side 0, column, 600 m for a squadron, 3 km for
/// a force). A top unit of one ship is that ship, not a group. A top unit with a route steams it (from where it's
/// placed, its course only the start's heading).</para>
/// <para><c>"from": "jutland"</c> takes another scenario of the same folder: its top units first (only those named in
/// <c>"only": [...]</c>, if given; <c>[]</c> takes none), then this file's own; its start, seed, camera, sides and brief
/// unless this file gives them. The scale test's ladder is made this way, so a corrected Jutland corrects every rung.</para></summary>
public sealed class Scenario
{
    public const int Version = 1;

    public required string Name { get; init; }
    public string Brief { get; init; } = "";
    public required WorldTime Start { get; init; }
    public ulong Seed { get; init; } = 1;

    /// <summary>Where the map opens: latitude, longitude, metres per pixel.</summary>
    public (double Lat, double Lon, double MetresPerPixel)? Camera { get; init; }

    /// <summary>The sides by index (a ship's side byte).</summary>
    public required string[] Sides { get; init; }

    /// <summary>The side the player commands (its name, and its index or -1 for none): docs/battle/plan.md step 3.1c.</summary>
    public string? PlayerName { get; init; }
    public int Player => PlayerName == null ? -1 : Array.IndexOf(Sides, PlayerName);

    /// <summary>The pairs of sides at peace (by index); every other pair is at war (docs/battle/plan.md step 3.1a).</summary>
    public List<(int A, int B)> Peace { get; init; } = [];

    public required List<ScenarioUnit> Units { get; init; }

    /// <summary>The folder the file was read from: its <c>Designs/</c> holds the scenario's own designs.</summary>
    public string? Folder { get; init; }

    /// <summary>Every design named, once each, in the order first named.</summary>
    public List<string> DesignNames()
    {
        var names = new List<string>();
        void Walk(ScenarioUnit u)
        {
            foreach (var s in u.Ships)
                if (!names.Contains(s.Design))
                    names.Add(s.Design);
            foreach (var c in u.Units)
                Walk(c);
        }
        foreach (var u in Units)
            Walk(u);
        return names;
    }

    public int ShipCount => Units.Sum(u => u.ShipCount);

    public static Scenario Load(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path), Path.GetDirectoryName(Path.GetFullPath(path)));
        }
        catch (ScenarioException x)
        {
            throw new ScenarioException($"{Path.GetFileName(path)}: {x.Message}");
        }
    }

    /// <summary>Whether a JSON file is a scenario (its "scenario" version field), without reading the rest: the app
    /// tells a scenario dropped on the window from a design.</summary>
    public static bool IsScenario(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), Options);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("scenario", out _);
        }
        catch (Exception x) when (x is JsonException or IOException)
        {
            return false;
        }
    }

    static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static Scenario Parse(string json, string? folder = null) => Parse(json, folder, 0);

    static Scenario Parse(string json, string? folder, int depth)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, Options);
        }
        catch (JsonException x)
        {
            throw new ScenarioException($"not JSON: {x.Message}");
        }
        using (doc)
        {
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object)
                throw new ScenarioException("not a scenario (want an object)");
            int version = r.TryGetProperty("scenario", out var v) && v.TryGetInt32(out int vi) ? vi : 0;
            if (version != Version)
                throw new ScenarioException($"not a scenario of version {Version} (\"scenario\": {Version})");
            // "from": another scenario in the same folder whose top units (those named in "only", or all) come first, and
            // whose start, seed, camera and sides stand unless this one gives its own (the scale test's ladder takes
            // parts of the Jutland, so a corrected position corrects every rung)
            Scenario? from = null;
            if (Str(r, "from", "") is { Length: > 0 } fromName)
            {
                if (depth > 4 || folder == null)
                    throw new ScenarioException($"from \"{fromName}\": {(folder == null ? "no folder to find it in" : "too deep")}");
                string fromPath = Path.Combine(folder, fromName + ".json");
                if (!File.Exists(fromPath))
                    throw new ScenarioException($"from \"{fromName}\": no {fromPath}");
                try
                {
                    from = Parse(File.ReadAllText(fromPath), folder, depth + 1);
                }
                catch (ScenarioException x)
                {
                    throw new ScenarioException($"from \"{fromName}\": {x.Message}");
                }
            }
            var sides = r.TryGetProperty("sides", out var sd) && sd.ValueKind == JsonValueKind.Array
                ? sd.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
                : from?.Sides ?? ["Side 0"];
            if (sides.Length is 0 or > byte.MaxValue + 1)
                throw new ScenarioException("sides: 1 to 256");
            // "peace": [["Britain", "Neutral"], ...], by the sides' names; a file giving its own sides gives its own peace
            var peace = r.TryGetProperty("peace", out var pe) ? ParsePeace(pe, sides)
                : from != null && !r.TryGetProperty("sides", out _) ? from.Peace : [];
            // "player": a side's name; taken from "from" (where these sides have it) unless given
            string? player = r.TryGetProperty("player", out var pl) ? pl.GetString() : from?.PlayerName;
            if (r.TryGetProperty("player", out _) && Array.IndexOf(sides, player) < 0)
                throw new ScenarioException($"player: \"{player}\" isn't one of the sides ({string.Join(", ", sides)})");
            if (Array.IndexOf(sides, player) < 0)
                player = null;
            var units = new List<ScenarioUnit>();
            if (from != null)
            {
                var only = r.TryGetProperty("only", out var o) ? o.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : null;
                foreach (var name in only ?? [])
                    if (!from.Units.Any(u => u.Name == name))
                        throw new ScenarioException($"only: no top unit \"{name}\" in {Str(r, "from", "")}");
                units.AddRange(only == null ? from.Units : from.Units.Where(u => only.Contains(u.Name)));
            }
            if (r.TryGetProperty("units", out var us) && us.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var u in us.EnumerateArray())
                    units.Add(ParseUnit(u, $"units[{i++}]", top: true, 0, sides.Length));
            }
            return new Scenario
            {
                Name = Str(r, "name", "") is { Length: > 0 } n ? n : "Scenario",
                Brief = Str(r, "brief", from?.Brief ?? ""),
                Start = from != null && !r.TryGetProperty("start", out _) ? from.Start : ParseTime(Str(r, "start", "")),
                Seed = r.TryGetProperty("seed", out var seed) && seed.TryGetUInt64(out ulong sv) ? sv : from?.Seed ?? 1,
                Camera = r.TryGetProperty("camera", out var cam) ? ParseCamera(cam) : from?.Camera,
                Sides = sides,
                Peace = peace,
                PlayerName = player,
                Units = units,
                Folder = folder,
            };
        }
    }

    static List<(int, int)> ParsePeace(JsonElement e, string[] sides)
    {
        if (e.ValueKind != JsonValueKind.Array)
            throw new ScenarioException("peace: a list of pairs of sides");
        var pairs = new List<(int, int)>();
        int i = 0;
        foreach (var p in e.EnumerateArray())
        {
            var names = p.ValueKind == JsonValueKind.Array ? p.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : [];
            if (names.Length != 2)
                throw new ScenarioException($"peace[{i}]: a pair of sides, [\"one\", \"another\"]");
            int a = Array.IndexOf(sides, names[0]), b = Array.IndexOf(sides, names[1]);
            if (a < 0 || b < 0 || a == b)
                throw new ScenarioException($"peace[{i}]: \"{(a < 0 ? names[0] : names[1])}\" isn't another of the sides ({string.Join(", ", sides)})");
            pairs.Add((a, b));
            i++;
        }
        return pairs;
    }

    static ScenarioUnit ParseUnit(JsonElement e, string path, bool top, int parentSide, int sides)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new ScenarioException($"{path}: not an object");
        string name = Str(e, "name", "");
        if (name.Length > 0)
            path = $"{path} ({name})";
        int side = e.TryGetProperty("side", out var s) ? s.GetInt32() : parentSide;
        if (side < 0 || side >= sides)
            throw new ScenarioException($"{path}: side {side} isn't one of the {sides} sides");
        bool hasUnits = e.TryGetProperty("units", out var us) && us.ValueKind == JsonValueKind.Array;
        bool hasShips = e.TryGetProperty("ships", out var ss) && ss.ValueKind == JsonValueKind.Array;
        if (hasUnits == hasShips)
            throw new ScenarioException($"{path}: a unit has \"units\" (a force) or \"ships\" (a squadron), one of them");
        var formation = ParseFormation(Str(e, "formation", "column"), path);
        double spacing = e.TryGetProperty("spacing", out var sp) ? sp.GetDouble() : hasUnits ? 3000 : 600;
        if (spacing is <= 0 or > 100_000)
            throw new ScenarioException($"{path}: spacing {spacing} m (want 0 to 100 km)");

        (double, double)? at = e.TryGetProperty("at", out var a) ? LatLon(a, path + " at") : null;
        double? course = e.TryGetProperty("course", out var c) ? c.GetDouble() : null;
        double? kn = e.TryGetProperty("kn", out var k) ? k.GetDouble() : null;
        if (top && (at == null || course == null || kn == null))
            throw new ScenarioException($"{path}: a top unit needs \"at\", \"course\" and \"kn\"");
        if (!top && (at != null || course != null || kn != null || e.TryGetProperty("route", out _)))
            throw new ScenarioException($"{path}: only a top unit is placed and ordered (\"at\", \"course\", \"kn\", \"route\")");
        if (kn is < 0 or > 60)
            throw new ScenarioException($"{path}: {kn} kn");

        var route = new List<(double Lat, double Lon)>();
        if (e.TryGetProperty("route", out var rt))
        {
            int i = 0;
            foreach (var p in rt.EnumerateArray())
                route.Add(LatLon(p, $"{path} route[{i++}]"));
        }

        var unit = new ScenarioUnit
        {
            Name = name,
            Side = side,
            Formation = formation,
            SpacingM = spacing,
            At = at,
            CourseDeg = course ?? 0,
            Kn = kn ?? 0,
            Route = route,
            Loop = e.TryGetProperty("loop", out var lp) && lp.GetBoolean(),
        };
        if (hasUnits)
        {
            int i = 0;
            foreach (var u in us.EnumerateArray())
                unit.Units.Add(ParseUnit(u, $"{path} units[{i++}]", top: false, side, sides));
            if (unit.Units.Count == 0)
                throw new ScenarioException($"{path}: a force of no units");
        }
        else
        {
            int i = 0;
            foreach (var x in ss.EnumerateArray())
                unit.Ships.Add(ParseShips(x, $"{path} ships[{i++}]"));
            if (unit.ShipCount == 0)
                throw new ScenarioException($"{path}: a squadron of no ships");
        }
        return unit;
    }

    static ScenarioShips ParseShips(JsonElement e, string path)
    {
        string design = Str(e, "design", "");
        if (design.Length == 0)
            throw new ScenarioException($"{path}: no \"design\"");
        var names = e.TryGetProperty("names", out var ns) ? ns.EnumerateArray().Select(n => n.GetString() ?? "").ToArray()
            : e.TryGetProperty("name", out var n1) ? [n1.GetString() ?? ""]
            : [];
        int count = e.TryGetProperty("count", out var c) ? c.GetInt32() : Math.Max(names.Length, 1);
        if (count < 1 || count > 10_000 || names.Length > count)
            throw new ScenarioException($"{path}: count {count} (and {names.Length} names)");
        return new ScenarioShips { Design = design, Count = count, Names = names };
    }

    static Formation ParseFormation(string s, string path) => s switch
    {
        "column" => Formation.Column,
        "abreast" => Formation.LineAbreast,
        "echelon-s" => Formation.EchelonStarboard,
        "echelon-p" => Formation.EchelonPort,
        _ => throw new ScenarioException($"{path}: formation \"{s}\" (want column, abreast, echelon-s or echelon-p)"),
    };

    static (double Lat, double Lon) LatLon(JsonElement e, string path)
    {
        if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() != 2)
            throw new ScenarioException($"{path}: want [lat, lon]");
        double lat = e[0].GetDouble(), lon = e[1].GetDouble();
        if (lat is < -90 or > 90 || lon is < -180 or > 360)
            throw new ScenarioException($"{path}: [{lat}, {lon}] isn't on the globe");
        return (lat, lon);
    }

    static (double, double, double) ParseCamera(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() != 3 || e[2].GetDouble() <= 0)
            throw new ScenarioException("camera: want [lat, lon, metres per pixel]");
        return (e[0].GetDouble(), e[1].GetDouble(), e[2].GetDouble());
    }

    static WorldTime ParseTime(string s)
    {
        if (!DateTime.TryParseExact(s, ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd"], CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t))
            throw new ScenarioException($"start \"{s}\" (want UTC, 1916-05-31T14:00)");
        return WorldTime.FromUtc(t);
    }

    static string Str(JsonElement e, string key, string fallback) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;
}

/// <summary>One unit of a scenario: a force (<see cref="Units"/>) or a squadron (<see cref="Ships"/>).</summary>
public sealed class ScenarioUnit
{
    public string Name { get; init; } = "";
    public int Side { get; init; }
    public Formation Formation { get; init; }
    public double SpacingM { get; init; }

    /// <summary>A top unit's place, heading and speed (its guide ship's); null below the top.</summary>
    public (double Lat, double Lon)? At { get; init; }
    public double CourseDeg { get; init; }
    public double Kn { get; init; }

    /// <summary>A top unit's waypoints, steamed from where it's placed; empty: it holds its course.</summary>
    public List<(double Lat, double Lon)> Route { get; init; } = [];
    public bool Loop { get; init; }

    public List<ScenarioUnit> Units { get; } = [];
    public List<ScenarioShips> Ships { get; } = [];

    public bool IsForce => Units.Count > 0;
    public int ShipCount => IsForce ? Units.Sum(u => u.ShipCount) : Ships.Sum(s => s.Count);
}

/// <summary>Ships of one design in a squadron, in order: <see cref="Count"/> of them, the first named by
/// <see cref="Names"/>.</summary>
public sealed class ScenarioShips
{
    public required string Design { get; init; }
    public int Count { get; init; } = 1;
    public string[] Names { get; init; } = [];
}

public sealed class ScenarioException(string message) : Exception(message);
