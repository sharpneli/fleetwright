using System.Globalization;
using System.Text.Json;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

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
///   "weather": { "visibility_km": 25, "wind_from": 225, "wind_kn": 8 }, "crews": { "Germany": "elite" },
///   "units": [
///     { "name": "Battlecruiser Fleet", "side": 0, "at": [56.77, 4.6], "course": 112, "kn": 19,
///       "formation": "column", "spacing": 3000,
///       "units": [
///         { "name": "1st Battlecruiser Squadron", "formation": "column", "spacing": 500,
///           "ships": [ { "design": "lion", "names": ["Lion", "Princess Royal", "Queen Mary"] } ] } ] },
///     { "name": "Patrol", "side": 0, "at": [58, 2], "course": 0, "kn": 10, "route": [[59, 2], [58, 2]], "loop": true,
///       "ships": [ { "design": "light_cruiser", "count": 2 } ] } ] }
/// </code>
/// <para>A ship entry may give her shells a dye (<c>"dye": "orange"</c>, or <c>"dyes": [...]</c> one a ship; none if not
/// given; docs/visuals/splashes.md "Dye").</para>
/// <para>A ship entry or a unit (for every ship below it that doesn't give its own) may give the ships a loadout other
/// than their design's (docs/battle/shells-armour-damage.md S5: the Admiralty's choice, not the designer's):
/// <c>"loadout": { "main": [{ "ammo": "RN 1912 HE (lyddite)", "share": 0.5 }, { "ammo": "ap" }] }</c>, by battery (main,
/// secondary, main[i], secondary[i]); an ammo a template's name (docs/shipgen/shell-templates.md), a block of the
/// design's, a kind (ap, common, he: the battery's own of it), or a block written out; shares left out split the
/// rest. One that doesn't fit a design is a warning, and her design's own shells are kept.</para>
/// <para>A side's gun crews are green, average, trained or elite (<c>"crews"</c>; trained if not listed). A side's
/// fire discipline (<c>"fire_discipline": { "Germany": "german" }</c>; the settings' first if not listed) is how its
/// batteries range and fire for effect (docs/battle/fire-control.md F6: a name of the settings' <c>FireDisciplines</c>). A side's readiness
/// (<c>"readiness": { "Russia": "prepared" }</c>; the settings' default if not listed) is its ships' orders before action
/// for fire (docs/battle/fire.md B1: a name of the settings' <c>Readiness</c>: stripped, doors, decks wetted), and its
/// firefighting (<c>"damage_control": { "Russia": "1905" }</c>; B4: a name of the settings' <c>DamageControl</c>) how
/// its crews fight her fires.</para>
/// <para>Each side's doctrine (docs/battle/command.md "Doctrine") is a preset of <c>Content/Sim/doctrine.json</c>,
/// <c>"doctrine": { "Germany": "default" }</c>, with values over it, <c>{ "Germany": { "preset": "default",
/// "RangeShare": 0.5 } }</c>; a side not listed takes "default" (Engage: fight). A unit sets values of its own,
/// <c>"doctrine": { "Torpedoes": "on order" }</c>. A side's <c>"home": { "Germany": [54.0, 8.0] }</c> is where its ships
/// out of the fight go.</para>
/// <para>A unit names its admiral, <c>"admiral": "R-Adm Enkvist"</c>, or gives his temper too (docs/map-ui/launch.md
/// "Tempers"): <c>"admiral": { "name": "R-Adm Enkvist", "temper": -0.4, "why": "kept his cruisers clear of the line" }</c>,
/// -1 timid to +1 bold, added to his side's aggression; a unit with no admiral named takes its force's. A ship entry gives
/// its captains' by name, <c>"captains": { "Bedovy": -0.5 }</c>, added to their admiral's.</para>
/// <para>It ends (docs/battle/command.md "The end", <see cref="ScenarioEnd"/>) at <c>"until": "1916-06-01T04:00"</c> (UTC;
/// <see cref="SimSettings.PlayLimitMs"/> after the start if not given), or before that when it's decided or quiet. Each
/// side wants its <c>"aims": { "Britain": "destroy everyone" }</c> (a name, or a list of them); a side not listed wants to
/// destroy everyone, v0's one aim.</para>
/// <para>Sides are at war with each other unless a pair is listed in <c>"peace"</c>. A unit's side, formation and spacing default to its parent's (side 0, column, 600 m for a squadron, 3 km for
/// a force). A top unit of one ship is that ship, not a group. A top unit with a route steams it (from where it's
/// placed, its course only the start's heading).</para>
/// <para><c>"from": "jutland"</c> takes another scenario of the same folder: its top units first (only those named in
/// <c>"only": [...]</c>, if given; <c>[]</c> takes none), then this file's own; its start, seed, camera, sides and brief
/// unless this file gives them. The scale test's ladder is made this way, so a corrected Jutland corrects every rung.</para>
/// <para>For the launch screen (docs/map-ui/launch.md), what it says of itself: <c>"place": "Tsushima Strait"</c>, and
/// each side's <c>"about": { "Russia": { "character": "The hammer: ...", "commander": "V-Adm Rozhestvensky",
/// "ensign": "russia" } }</c> (an ensign the screen draws: britain, germany, russia, japan, france, italy, ottoman). Both taken from "from"
/// unless given. <c>"test": true</c> lists it under Tests (the scale test's rungs, the benches), never taken from
/// "from".</para>
/// <para>A record, so a setup is a copy with its changes (<c>s with { PlayerName = "Japan" }</c>).</para></summary>
public sealed record Scenario
{
    public const int Version = 1;

    public required string Name { get; init; }
    public string Brief { get; init; } = "";

    /// <summary>Where it's fought, in a few words ("Tsushima Strait"), or "".</summary>
    public string Place { get; init; } = "";

    /// <summary>A test, not a scenario to play (the scale test's rungs, the benches): the launch screen lists it under
    /// Tests.</summary>
    public bool Test { get; init; }

    /// <summary>What the file says of each side (its character, its commander, its ensign), by index, where it does.</summary>
    public List<(int Side, SideAbout About)> About { get; init; } = [];

    /// <summary>The setup this is the scenario as set by (docs/map-ui/launch.md), and the scenario as written; null for
    /// the written one itself.</summary>
    public ScenarioSetup? Setup { get; init; }
    public Scenario? Written { get; init; }

    public SideAbout? AboutOf(int side)
    {
        foreach (var (sd, a) in About)
            if (sd == side)
                return a;
        return null;
    }

    public required WorldTime Start { get; init; }
    public ulong Seed { get; init; } = 1;

    /// <summary>Where the map opens: latitude, longitude, metres per pixel.</summary>
    public (double Lat, double Lon, double MetresPerPixel)? Camera { get; init; }

    /// <summary>The sides by index (a ship's side byte).</summary>
    public required string[] Sides { get; init; }

    /// <summary>The side the player commands (its name, and its index or -1 for none): docs/battle/plan.md step 3.1c.</summary>
    public string? PlayerName { get; init; }
    public int Player => PlayerName == null ? -1 : Array.IndexOf(Sides, PlayerName);

    /// <summary>The weather (docs/battle/plan.md step 3.2b), the same everywhere; null for the world's default.</summary>
    public WeatherHere? Weather { get; init; }

    /// <summary>The pairs of sides at peace (by index); every other pair is at war (docs/battle/plan.md step 3.1a).</summary>
    public List<(int A, int B)> Peace { get; init; } = [];

    /// <summary>The sides' gun crews where they aren't trained (docs/battle/fighting.md 4.2b), by side index.</summary>
    public List<(int Side, CrewQuality Quality)> Crews { get; init; } = [];

    /// <summary>The sides' fuel where it's given, a share of a full load (docs/battle/shell-contents.md C2), by side
    /// index.</summary>
    public List<(int Side, double Share)> Fuel { get; init; } = [];

    /// <summary>The sides' fire disciplines where they're given (docs/battle/fire-control.md F6), by side index: a name of
    /// the settings' FireDisciplines (the loader checks it).</summary>
    public List<(int Side, string Name)> FireDisciplines { get; init; } = [];

    /// <summary>The sides' readiness for fire where it's given (docs/battle/fire.md B1), by side index: a name of the
    /// settings' Readiness (the loader checks it).</summary>
    public List<(int Side, string Name)> Readiness { get; init; } = [];

    /// <summary>The sides' firefighting doctrines where they're given (docs/battle/fire.md B4), by side index: a name of the
    /// settings' DamageControl (the loader checks it).</summary>
    public List<(int Side, string Name)> DamageControl { get; init; } = [];

    /// <summary>Each side's doctrine where the file gives it, by index: a preset, and values over it.</summary>
    public List<(int Side, string Preset, List<(DoctrineField Field, double Value)> Values)> Doctrine { get; init; } = [];

    /// <summary>The sides' homes where the file gives them, by index.</summary>
    public List<(int Side, double Lat, double Lon)> Homes { get; init; } = [];

    /// <summary>When it ends at the latest (docs/battle/command.md "The end"); null for the start plus
    /// <see cref="SimSettings.PlayLimitMs"/>.</summary>
    public WorldTime? Until { get; init; }

    /// <summary>Each side's aims by index, where the file gives them; a side not listed wants to destroy everyone.</summary>
    public List<(int Side, Aim Aim)> AimList { get; init; } = [];

    /// <summary>A side's aims: the file's, else destroy everyone (v0's one aim).</summary>
    public IEnumerable<Aim> AimsOf(int side)
    {
        bool any = false;
        foreach (var (s, a) in AimList)
            if (s == side)
            {
                any = true;
                yield return a;
            }
        if (!any)
            yield return new Aim(AimKind.DestroyEveryone);
    }

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

    public static Scenario Load(string path, IReadOnlyList<string>? fromFolders = null)
    {
        try
        {
            return Parse(File.ReadAllText(path), Path.GetDirectoryName(Path.GetFullPath(path)), 0, fromFolders ?? []);
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

    public static Scenario Parse(string json, string? folder = null) => Parse(json, folder, 0, []);

    /// <summary>Reads a file whose <c>"from"</c> may be in <paramref name="fromFolders"/> too (a setup in the player's
    /// folder of a shipped scenario).</summary>
    public static Scenario Parse(string json, string? folder, IReadOnlyList<string> fromFolders) => Parse(json, folder, 0, fromFolders);

    static Scenario Parse(string json, string? folder, int depth, IReadOnlyList<string> fromFolders)
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
                string[] look = [.. (folder != null ? [folder] : Array.Empty<string>()), .. fromFolders];
                if (depth > 4 || look.Length == 0)
                    throw new ScenarioException($"from \"{fromName}\": {(look.Length == 0 ? "no folder to find it in" : "too deep")}");
                string? fromPath = look.Select(f => Path.Combine(f, fromName + ".json")).FirstOrDefault(File.Exists)
                    ?? throw new ScenarioException($"from \"{fromName}\": not in {string.Join(" or ", look)}");
                try
                {
                    from = Parse(File.ReadAllText(fromPath), Path.GetDirectoryName(fromPath), depth + 1, fromFolders);
                }
                catch (ScenarioException x)
                {
                    throw new ScenarioException($"from \"{fromName}\": {x.Message}");
                }
                // a setup: the scenario it's of, as set (ScenarioSetup); its folder the written one's, for its designs
                if (ScenarioSetup.IsSetup(r))
                    return ScenarioSetup.Parse(r, from).Apply(from);
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
            // "weather": { "visibility_km": 25, "wind_from": 225, "wind_kn": 8 }; taken from "from" unless given
            var weather = r.TryGetProperty("weather", out var we) ? ParseWeather(we) : from?.Weather;
            // "crews": { "Germany": "elite" }, by the sides' names; taken from "from" (with its sides) unless given
            var crews = r.TryGetProperty("crews", out var cr) ? ParseCrews(cr, sides)
                : from != null && !r.TryGetProperty("sides", out _) ? from.Crews : [];
            var fuelShares = r.TryGetProperty("fuel", out var fu) ? ParseFuel(fu, sides)
                : from != null && !r.TryGetProperty("sides", out _) ? from.Fuel : [];
            // "fire_discipline": { "Germany": "german" }, by the sides' names; taken from "from" (with its sides) unless given
            var disciplines = r.TryGetProperty("fire_discipline", out var fd) ? ParseNames("fire_discipline", "a discipline's name", fd, sides)
                : from != null && !r.TryGetProperty("sides", out _) ? from.FireDisciplines : [];
            // "readiness": { "Russia": "prepared" }, the same way
            var readiness = r.TryGetProperty("readiness", out var rd) ? ParseNames("readiness", "a readiness's name", rd, sides)
                : from != null && !r.TryGetProperty("sides", out _) ? from.Readiness : [];
            var firefighting = r.TryGetProperty("damage_control", out var ff) ? ParseNames("damage_control", "a doctrine's name", ff, sides)
                : from != null && !r.TryGetProperty("sides", out _) ? from.DamageControl : [];
            if (r.TryGetProperty("fight", out _))
                throw new ScenarioException("fight: gone; doctrine's Engage is \"fight\" by default (\"doctrine\": { \"Germany\": { \"Engage\": \"carry on\" } } not to)");
            // "doctrine": { "Germany": "default" | { "preset": "default", "RangeShare": 0.5 } }; "home": { "Germany": [54, 8] };
            // by the sides' names, taken from "from" (with its sides) unless given
            var doctrine = r.TryGetProperty("doctrine", out var dc) ? ParseDoctrine(dc, sides)
                : from != null && !r.TryGetProperty("sides", out _) ? from.Doctrine : [];
            var homes = r.TryGetProperty("home", out var ho) ? ParseHomes(ho, sides)
                : from != null && !r.TryGetProperty("sides", out _) ? from.Homes : [];
            // "until": "1916-06-01T04:00"; "aims": { "Britain": "destroy everyone" }; both taken from "from" unless given
            WorldTime? until = r.TryGetProperty("until", out var un) ? ParseTime(un.GetString() ?? "", "until") : from?.Until;
            var aims = r.TryGetProperty("aims", out var ai) ? ParseAims(ai, sides)
                : from != null && !r.TryGetProperty("sides", out _) ? from.AimList : [];
            var about = r.TryGetProperty("about", out var ab) ? ParseAbout(ab, sides)
                : from != null && !r.TryGetProperty("sides", out _) ? from.About : [];
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
                Place = Str(r, "place", from?.Place ?? ""),
                Test = r.TryGetProperty("test", out var te) && te.ValueKind == JsonValueKind.True,
                About = about,
                Start = from != null && !r.TryGetProperty("start", out _) ? from.Start : ParseTime(Str(r, "start", ""), "start"),
                Seed = r.TryGetProperty("seed", out var seed) && seed.TryGetUInt64(out ulong sv) ? sv : from?.Seed ?? 1,
                Camera = r.TryGetProperty("camera", out var cam) ? ParseCamera(cam) : from?.Camera,
                Sides = sides,
                Peace = peace,
                PlayerName = player,
                Weather = weather,
                Crews = crews,
                Fuel = fuelShares,
                FireDisciplines = disciplines,
                Readiness = readiness,
                DamageControl = firefighting,
                Doctrine = doctrine,
                Homes = homes,
                Until = until,
                AimList = aims,
                Units = units,
                Folder = folder,
            };
        }
    }

    static List<(int, string, List<(DoctrineField, double)>)> ParseDoctrine(JsonElement e, string[] sides)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new ScenarioException("doctrine: an object of sides' names, { \"Germany\": \"default\" }");
        var list = new List<(int, string, List<(DoctrineField, double)>)>();
        foreach (var p in e.EnumerateObject())
        {
            int side = Array.IndexOf(sides, p.Name);
            if (side < 0)
                throw new ScenarioException($"doctrine: \"{p.Name}\" isn't one of the sides ({string.Join(", ", sides)})");
            if (p.Value.ValueKind == JsonValueKind.String)
                list.Add((side, p.Value.GetString()!, []));
            else if (p.Value.ValueKind == JsonValueKind.Object)
                list.Add((side, p.Value.TryGetProperty("preset", out var pr) ? pr.GetString() ?? "" : DoctrinePresets.DefaultName,
                    DoctrineValues(p.Value, $"doctrine of {p.Name}", unit: false, "preset")));
            else
                throw new ScenarioException($"doctrine of {p.Name}: a preset's name, or {{ \"preset\": ..., fields }}");
        }
        return list;
    }

    /// <summary>Doctrine's fields and their values in an object (<paramref name="skip"/> aside): a side's or a unit's
    /// (a unit's aggression is its admiral's "temper", a side's has none).</summary>
    internal static List<(DoctrineField, double)> DoctrineValues(JsonElement e, string path, bool unit, string? skip = null)
    {
        var values = new List<(DoctrineField, double)>();
        foreach (var f in e.EnumerateObject())
        {
            if (f.Name == skip)
                continue;
            if (f.Name == (unit ? nameof(DoctrineField.Aggression) : nameof(DoctrineField.Temper)))
                throw new ScenarioException(unit ? $"{path}: Aggression is a side's; a unit's is its admiral's, \"admiral\": {{ \"temper\": 0.3 }}"
                    : $"{path}: a Temper is an admiral's or a captain's (a unit's \"admiral\", a ship entry's \"captains\"); a side's is its Aggression");
            try
            {
                values.Add(DoctrinePresets.Value(f.Name, f.Value));
            }
            catch (ArgumentException x)
            {
                throw new ScenarioException($"{path}: {x.Message}");
            }
        }
        return values;
    }

    static List<(int, double, double)> ParseHomes(JsonElement e, string[] sides)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new ScenarioException("home: an object of sides' names, { \"Germany\": [54.0, 8.0] }");
        var list = new List<(int, double, double)>();
        foreach (var p in e.EnumerateObject())
        {
            int side = Array.IndexOf(sides, p.Name);
            if (side < 0)
                throw new ScenarioException($"home: \"{p.Name}\" isn't one of the sides ({string.Join(", ", sides)})");
            var (lat, lon) = LatLon(p.Value, $"home of {p.Name}");
            list.Add((side, lat, lon));
        }
        return list;
    }

    static List<(int, Aim)> ParseAims(JsonElement e, string[] sides)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new ScenarioException("aims: an object of sides' names, { \"Britain\": \"destroy everyone\" }");
        var list = new List<(int, Aim)>();
        foreach (var p in e.EnumerateObject())
        {
            int side = Array.IndexOf(sides, p.Name);
            if (side < 0)
                throw new ScenarioException($"aims: \"{p.Name}\" isn't one of the sides ({string.Join(", ", sides)})");
            var names = p.Value.ValueKind == JsonValueKind.Array ? p.Value.EnumerateArray().Select(x => x.GetString()).ToArray() : [p.Value.GetString()];
            foreach (var name in names)
                list.Add((side, name switch
                {
                    "destroy everyone" => new Aim(AimKind.DestroyEveryone),
                    _ => throw new ScenarioException($"aims: \"{name}\" for {p.Name}: \"destroy everyone\" (v0's one aim)"),
                }));
        }
        return list;
    }

    static List<(int, SideAbout)> ParseAbout(JsonElement e, string[] sides)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new ScenarioException("about: an object of sides' names, { \"Russia\": { \"character\": ..., \"commander\": ..., \"ensign\": ... } }");
        var list = new List<(int, SideAbout)>();
        foreach (var p in e.EnumerateObject())
        {
            int side = Array.IndexOf(sides, p.Name);
            if (side < 0)
                throw new ScenarioException($"about: \"{p.Name}\" isn't one of the sides ({string.Join(", ", sides)})");
            if (p.Value.ValueKind != JsonValueKind.Object)
                throw new ScenarioException($"about {p.Name}: {{ \"character\": ..., \"commander\": ..., \"ensign\": ... }}");
            list.Add((side, new SideAbout(Str(p.Value, "character", ""), Str(p.Value, "commander", ""), Str(p.Value, "ensign", ""))));
        }
        return list;
    }

    internal static List<(int, CrewQuality)> ParseCrews(JsonElement e, string[] sides)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new ScenarioException("crews: an object of sides' names, { \"Germany\": \"elite\" }");
        var list = new List<(int, CrewQuality)>();
        foreach (var p in e.EnumerateObject())
        {
            int side = Array.IndexOf(sides, p.Name);
            if (side < 0)
                throw new ScenarioException($"crews: \"{p.Name}\" isn't one of the sides ({string.Join(", ", sides)})");
            if (!Enum.TryParse<CrewQuality>(p.Value.GetString(), ignoreCase: true, out var q) || !Enum.IsDefined(q))
                throw new ScenarioException($"crews: \"{p.Value}\" for {p.Name}: green, average, trained or elite");
            list.Add((side, q));
        }
        return list;
    }

    internal static List<(int, double)> ParseFuel(JsonElement e, string[] sides)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new ScenarioException("fuel: an object of sides' names, { \"Britain\": 0.6 } (a share of a full load)");
        var list = new List<(int, double)>();
        foreach (var p in e.EnumerateObject())
        {
            int side = Array.IndexOf(sides, p.Name);
            if (side < 0)
                throw new ScenarioException($"fuel: \"{p.Name}\" isn't one of the sides ({string.Join(", ", sides)})");
            if (p.Value.ValueKind != JsonValueKind.Number || !(p.Value.GetDouble() is >= 0 and <= 1))
                throw new ScenarioException($"fuel: {p.Value} for {p.Name}: a share of a full load, 0 to 1");
            list.Add((side, p.Value.GetDouble()));
        }
        return list;
    }

    /// <summary>A setting by side, a name each (fire_discipline, readiness): { "Germany": "german" }.</summary>
    internal static List<(int, string)> ParseNames(string key, string what, JsonElement e, string[] sides)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new ScenarioException($"{key}: an object of sides' names, {{ \"{sides.FirstOrDefault() ?? "Germany"}\": ... }}");
        var list = new List<(int, string)>();
        foreach (var p in e.EnumerateObject())
        {
            int side = Array.IndexOf(sides, p.Name);
            if (side < 0)
                throw new ScenarioException($"{key}: \"{p.Name}\" isn't one of the sides ({string.Join(", ", sides)})");
            if (p.Value.ValueKind != JsonValueKind.String)
                throw new ScenarioException($"{key}: {p.Value} for {p.Name}: {what}");
            list.Add((side, p.Value.GetString()!));
        }
        return list;
    }

    internal static WeatherHere ParseWeather(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new ScenarioException("weather: an object, { \"visibility_km\": 25, \"wind_from\": 225, \"wind_kn\": 8, \"air_over_sea_k\": 0 }");
        double Num(string name, double fallback) => e.TryGetProperty(name, out var v) ? v.GetDouble() : fallback;
        double vis = Num("visibility_km", double.NaN), from = Num("wind_from", 0), kn = Num("wind_kn", 0), airSea = Num("air_over_sea_k", 0);
        if (!(vis > 0))
            throw new ScenarioException("weather: \"visibility_km\", more than 0");
        if (!(kn >= 0))
            throw new ScenarioException("weather: \"wind_kn\", 0 or more");
        if (!(Math.Abs(airSea) <= 30))
            throw new ScenarioException("weather: \"air_over_sea_k\", the air's temperature over the sea's, -30 to 30");
        return new WeatherHere(vis * 1000, from, kn * 0.514444, airSea);
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

    internal static ScenarioUnit ParseUnit(JsonElement e, string path, bool top, int parentSide, int sides, LoadoutSpec? parentLoadout = null)
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
        var loadout = ParseLoadout(e, path) ?? parentLoadout;
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

        var (admiral, temper, why) = ParseAdmiral(e, path);
        var unit = new ScenarioUnit
        {
            Name = name,
            Admiral = admiral,
            Temper = temper,
            TemperWhy = why,
            Side = side,
            Formation = formation,
            SpacingM = spacing,
            At = at,
            CourseDeg = course ?? 0,
            Kn = kn ?? 0,
            Route = route,
            Loop = e.TryGetProperty("loop", out var lp) && lp.GetBoolean(),
            Doctrine = e.TryGetProperty("doctrine", out var dc) ? DoctrineValues(dc, $"{path} doctrine", unit: true) : [],
            Tasks = ParseTasks(e, path, top),
            Loadout = loadout,
        };
        if (hasUnits)
        {
            int i = 0;
            foreach (var u in us.EnumerateArray())
                unit.Units.Add(ParseUnit(u, $"{path} units[{i++}]", top: false, side, sides, loadout));
            if (unit.Units.Count == 0)
                throw new ScenarioException($"{path}: a force of no units");
        }
        else
        {
            int i = 0;
            foreach (var x in ss.EnumerateArray())
                unit.Ships.Add(ParseShips(x, $"{path} ships[{i++}]", loadout));
            if (unit.ShipCount == 0)
                throw new ScenarioException($"{path}: a squadron of no ships");
        }
        return unit;
    }

    /// <summary>A unit's <c>"admiral"</c>: his name (<c>"R-Adm Enkvist"</c>), or <c>{ "name": ..., "temper": -0.4, "why":
    /// "kept his cruisers clear of the line" }</c> (docs/map-ui/launch.md "Tempers": why the scenario gave it, in a few
    /// words).</summary>
    static (string Name, double? Temper, string Why) ParseAdmiral(JsonElement e, string path)
    {
        if (!e.TryGetProperty("admiral", out var a))
            return ("", null, "");
        if (a.ValueKind == JsonValueKind.String)
            return (a.GetString()!, null, "");
        if (a.ValueKind != JsonValueKind.Object)
            throw new ScenarioException($"{path}: \"admiral\", his name or {{ \"name\": ..., \"temper\": 0.3, \"why\": ... }}");
        foreach (var p in a.EnumerateObject())
            if (p.Name is not ("name" or "temper" or "why"))
                throw new ScenarioException($"{path} admiral: \"name\", \"temper\" and \"why\"; not \"{p.Name}\"");
        return (Str(a, "name", ""), a.TryGetProperty("temper", out var t) ? Temper(t, $"{path} admiral") : null, Str(a, "why", ""));
    }

    /// <summary>A temper as written: -1 (timid) to +1 (bold).</summary>
    internal static double Temper(JsonElement t, string path) =>
        t.ValueKind == JsonValueKind.Number && t.GetDouble() is var v and >= -1 and <= 1 ? v
            : throw new ScenarioException($"{path}: a temper is -1 (timid) to +1 (bold)");

    /// <summary>A top unit's <c>"task"</c>, or its <c>"tasks"</c> one after another (its plan); none if it has neither.</summary>
    static List<ScenarioTask> ParseTasks(JsonElement e, string path, bool top)
    {
        bool one = e.TryGetProperty("task", out var tk), many = e.TryGetProperty("tasks", out var ts);
        if (one && many)
            throw new ScenarioException($"{path}: \"task\" or \"tasks\", not both");
        if (!one && !many)
            return [];
        if (!top)
            throw new ScenarioException($"{path}: only a top unit has a task");
        if (one)
            return [ParseTask(tk, $"{path} task")];
        if (ts.ValueKind != JsonValueKind.Array || ts.GetArrayLength() == 0)
            throw new ScenarioException($"{path}: \"tasks\", a list of tasks done one after another");
        int i = 0;
        return [.. ts.EnumerateArray().Select(t => ParseTask(t, $"{path} tasks[{i++}]"))];
    }

    static ScenarioTask ParseTask(JsonElement e, string path)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new ScenarioException($"{path}: an object, {{ \"do\": \"scout\", \"of\": \"Grand Fleet\" }}");
        string kind = Str(e, "do", "");
        if (!ScenarioTask.Kinds.Contains(kind))
            throw new ScenarioException($"{path}: \"do\": \"{kind}\" isn't one of {string.Join(", ", ScenarioTask.Kinds.Select(k => $"\"{k}\""))}");
        string? of = e.TryGetProperty("of", out var o) ? o.GetString() : null;
        (double, double)? at = e.TryGetProperty("at", out var a) ? LatLon(a, path + " at") : e.TryGetProperty("to", out var to) ? LatLon(to, path + " to") : null;
        bool needsOf = kind is "join" or "scout" or "screen" or "cover" or "shadow", needsAt = kind is "go to" or "patrol" or "search";
        if (needsOf && string.IsNullOrEmpty(of))
            throw new ScenarioException($"{path}: \"{kind}\" needs \"of\", a unit's name");
        if (needsAt && at == null)
            throw new ScenarioException($"{path}: \"{kind}\" needs {(kind == "patrol" ? "\"to\"" : "\"at\"")}, [lat, lon]");
        double km = e.TryGetProperty("distance_km", out var d) ? d.GetDouble() : e.TryGetProperty("leg_km", out var l) ? l.GetDouble() : 0;
        if (km < 0 || km > 500)
            throw new ScenarioException($"{path}: {km} km");
        return new ScenarioTask
        {
            Kind = kind, Of = of, At = at, DistanceM = km * 1000,
            OffBowDeg = e.TryGetProperty("off_bow", out var ob) ? ob.GetDouble() : 0,
            Kn = e.TryGetProperty("kn", out var kn) ? kn.GetDouble() : 0,
        };
    }

    /// <summary>A unit's or ship entry's <c>"loadout"</c>, or null for none.</summary>
    static LoadoutSpec? ParseLoadout(JsonElement e, string path)
    {
        if (!e.TryGetProperty("loadout", out var l))
            return null;
        try
        {
            return LoadoutSpec.FromJson(l, path);
        }
        catch (Exception x) when (x is FormatException or JsonException or InvalidOperationException)
        {
            throw new ScenarioException($"{path}: {x.Message}");
        }
    }

    static ScenarioShips ParseShips(JsonElement e, string path, LoadoutSpec? unitLoadout = null)
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
        // the dye loads (docs/visuals/splashes.md "Dye"): one for all of them, or each ship's in order
        var dyes = e.TryGetProperty("dyes", out var ds) ? ds.EnumerateArray().Select(d => ParseDye(d.GetString(), path)).ToArray()
            : e.TryGetProperty("dye", out var d1) ? Enumerable.Repeat(ParseDye(d1.GetString(), path), count).ToArray()
            : [];
        if (dyes.Length > count)
            throw new ScenarioException($"{path}: {dyes.Length} dyes for {count} ships");
        // the captains' tempers by their ships' names (docs/map-ui/launch.md "Tempers"): one a ship in order
        var tempers = Array.Empty<double>();
        if (e.TryGetProperty("captains", out var cp))
        {
            if (cp.ValueKind != JsonValueKind.Object)
                throw new ScenarioException($"{path}: \"captains\", an object of ships' names and tempers, {{ \"Bedovy\": -0.5 }}");
            tempers = new double[count];
            foreach (var p in cp.EnumerateObject())
            {
                int k = Array.IndexOf(names, p.Name);
                if (k < 0)
                    throw new ScenarioException($"{path} captains: no ship \"{p.Name}\" here (have {string.Join(", ", names)})");
                tempers[k] = Temper(p.Value, $"{path} captains: {p.Name}");
            }
        }
        return new ScenarioShips { Design = design, Count = count, Names = names, Dyes = dyes, Tempers = tempers, Loadout = ParseLoadout(e, path) ?? unitLoadout };
    }

    static Dye ParseDye(string? s, string path) =>
        Enum.TryParse<Dye>(s, ignoreCase: true, out var d) && Enum.IsDefined(d) && !int.TryParse(s, out _) ? d
            : throw new ScenarioException($"{path}: no dye \"{s}\" ({string.Join(", ", Enum.GetNames<Dye>().Select(n => n.ToLowerInvariant()))})");

    /// <summary>A formation as a file names it.</summary>
    internal static string FormationName(Formation f) => f switch
    {
        Formation.LineAbreast => "abreast",
        Formation.EchelonStarboard => "echelon-s",
        Formation.EchelonPort => "echelon-p",
        _ => "column",
    };

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

    internal static WorldTime ParseTime(string s, string key)
    {
        if (!DateTime.TryParseExact(s, ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd"], CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t))
            throw new ScenarioException($"{key} \"{s}\" (want UTC, 1916-05-31T14:00)");
        return WorldTime.FromUtc(t);
    }

    static string Str(JsonElement e, string key, string fallback) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;
}

/// <summary>One unit of a scenario: a force (<see cref="Units"/>) or a squadron (<see cref="Ships"/>).</summary>
/// <summary>What a scenario says of a side for the launch screen: its character in a line, its commander, its ensign
/// (a name the screen draws; "" for none).</summary>
public sealed record SideAbout(string Character, string Commander, string Ensign);

public sealed class ScenarioUnit
{
    public string Name { get; init; } = "";
    public int Side { get; init; }

    /// <summary>Its admiral as the scenario names him ("R-Adm Enkvist", "Rozhestvensky (in Suvorov)"), or "".</summary>
    public string Admiral { get; init; } = "";

    /// <summary>His temper (docs/map-ui/launch.md "Tempers"), -1 timid to +1 bold, added to the side's aggression; null
    /// for none given. A unit with an admiral of its own (<see cref="OwnTemper"/>) has his (0 if none is given); one
    /// without takes its force's. <see cref="TemperWhy"/>: why the scenario gave it, in a few words.</summary>
    public double? Temper { get; init; }
    public string TemperWhy { get; init; } = "";

    /// <summary>Whether its admiral has a temper of his own (<see cref="Temper"/>): it's a top unit, names him, or has a
    /// temper given.</summary>
    public bool OwnTemper => At != null || Admiral.Length > 0 || Temper != null;
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

    /// <summary>Doctrine's values the unit sets for itself (docs/battle/command.md "Doctrine"), over its side's.</summary>
    public List<(DoctrineField Field, double Value)> Doctrine { get; init; } = [];

    /// <summary>A top unit's opening tasks (docs/battle/command.md 5.1c), one after another (its plan, 5.1d); none: it
    /// steams its course or route.</summary>
    public List<ScenarioTask> Tasks { get; init; } = [];

    /// <summary>The loadout its ships carry (its own <c>"loadout"</c>, else its parent's), unless a ship entry gives one.</summary>
    public LoadoutSpec? Loadout { get; init; }

    /// <summary>Taken off by a setup (docs/map-ui/launch.md L3b): every ship under it struck, so none is placed; kept in
    /// the tree for the launch screen to show it struck, with its undo.</summary>
    public bool Removed { get; init; }

    public bool IsForce => Units.Count > 0;
    public int ShipCount => IsForce ? Units.Sum(u => u.ShipCount) : Ships.Sum(s => s.Count);

    /// <summary>A copy with other sub-units and ships, the rest the same (a setup's changes: the written one untouched).</summary>
    internal ScenarioUnit With(IEnumerable<ScenarioUnit> units, IEnumerable<ScenarioShips> ships, bool? removed = null)
    {
        var u = new ScenarioUnit
        {
            Name = Name, Side = Side, Formation = Formation, SpacingM = SpacingM, At = At, CourseDeg = CourseDeg, Kn = Kn,
            Route = Route, Loop = Loop, Doctrine = Doctrine, Tasks = Tasks, Loadout = Loadout, Removed = removed ?? Removed,
            Admiral = Admiral, Temper = Temper, TemperWhy = TemperWhy,
        };
        u.Units.AddRange(units);
        u.Ships.AddRange(ships);
        return u;
    }

    /// <summary>A copy called by another name.</summary>
    internal ScenarioUnit Renamed(string name) => Copy(name, Tasks, Admiral, Temper);

    /// <summary>A copy with other tasks.</summary>
    internal ScenarioUnit Tasked(List<ScenarioTask> tasks) => Copy(Name, tasks, Admiral, Temper);

    /// <summary>A copy with its admiral's temper set (null: none given).</summary>
    internal ScenarioUnit Tempered(double? temper) => Copy(Name, Tasks, Admiral, temper);

    /// <summary>A copy under another admiral (his temper kept).</summary>
    internal ScenarioUnit Commanded(string admiral) => Copy(Name, Tasks, admiral, Temper);

    ScenarioUnit Copy(string name, List<ScenarioTask> tasks, string admiral, double? temper)
    {
        var u = new ScenarioUnit
        {
            Name = name, Side = Side, Formation = Formation, SpacingM = SpacingM, At = At, CourseDeg = CourseDeg, Kn = Kn,
            Route = Route, Loop = Loop, Doctrine = Doctrine, Tasks = tasks, Loadout = Loadout, Removed = Removed,
            Admiral = admiral, Temper = temper, TemperWhy = TemperWhy,
        };
        u.Units.AddRange(Units);
        u.Ships.AddRange(Ships);
        return u;
    }
}

/// <summary>Ships of one design in a squadron, in order: <see cref="Count"/> of them, the first named by
/// <see cref="Names"/>.</summary>
public sealed class ScenarioShips
{
    public required string Design { get; init; }
    public int Count { get; init; } = 1;
    public string[] Names { get; init; } = [];

    /// <summary>Each ship's dye load in order (<c>"dye"</c> for all, <c>"dyes"</c> each); those past its end carry none.</summary>
    public Dye[] Dyes { get; init; } = [];

    /// <summary>Each ship's captain's temper in order (<c>"captains"</c>, by name; docs/map-ui/launch.md "Tempers"), added
    /// to her admiral's and the side's; those past its end have none.</summary>
    public double[] Tempers { get; init; } = [];

    /// <summary>The <paramref name="i"/>-th ship's captain's temper.</summary>
    public double TemperOf(int i) => i < Tempers.Length ? Tempers[i] : 0;

    /// <summary>The loadout they carry (docs/battle/shells-armour-damage.md S5): the entry's, else its unit's; null for
    /// their design's own.</summary>
    public LoadoutSpec? Loadout { get; init; }

    /// <summary>The ships a setup took off the end of the entry (docs/map-ui/launch.md L3b), their names in order ("" for
    /// one unnamed): not in <see cref="Count"/>, never placed; the launch screen shows them struck.</summary>
    public string[] Struck { get; init; } = [];

    /// <summary>Ships <paramref name="from"/> to <paramref name="from"/> + <paramref name="count"/> of the entry, of a
    /// design: their names and dyes go with them, and the struck with the slice that ends the entry.</summary>
    internal ScenarioShips Slice(int from, int count, string design) => new()
    {
        Design = design, Count = count, Loadout = Loadout,
        Names = from < Names.Length ? Names[from..Math.Min(from + count, Names.Length)] : [],
        Dyes = from < Dyes.Length ? Dyes[from..Math.Min(from + count, Dyes.Length)] : [],
        Tempers = from < Tempers.Length ? Tempers[from..Math.Min(from + count, Tempers.Length)] : [],
        Struck = from + count >= Count ? Struck : [],
    };

    /// <summary>The entry with <paramref name="n"/> more ships at its end, unnamed.</summary>
    internal ScenarioShips Grown(int n) => new() { Design = Design, Count = Count + n, Names = Names, Dyes = Dyes, Tempers = Tempers, Loadout = Loadout, Struck = Struck };

    /// <summary>The entry with the <paramref name="i"/>-th ship's captain's temper set.</summary>
    internal ScenarioShips Tempered(int i, double temper)
    {
        var t = new double[Math.Max(Tempers.Length, i + 1)];
        Tempers.CopyTo(t, 0);
        t[i] = temper;
        return new() { Design = Design, Count = Count, Names = Names, Dyes = Dyes, Tempers = t, Loadout = Loadout, Struck = Struck };
    }

    /// <summary>The entry with its last <paramref name="n"/> ships taken off: struck (kept by name for the screen), or
    /// gone.</summary>
    internal ScenarioShips Shrunk(int n, bool strike)
    {
        int keep = Count - n;
        var off = Enumerable.Range(keep, n).Select(i => i < Names.Length ? Names[i] : "");
        return new()
        {
            Design = Design, Count = keep, Loadout = Loadout,
            Names = Names[..Math.Min(keep, Names.Length)], Dyes = Dyes[..Math.Min(keep, Dyes.Length)],
            Tempers = Tempers[..Math.Min(keep, Tempers.Length)],
            Struck = strike ? [.. off, .. Struck] : Struck,
        };
    }
}

/// <summary>A top unit's opening task (docs/battle/command.md 5.1c): <c>"task": { "do": "scout", "of": "Grand Fleet",
/// "distance_km": 30 }</c>. Kinds: "go to" (<c>"at"</c>, <c>"kn"</c>), "patrol" (<c>"to"</c>), "search" (<c>"at"</c>,
/// <c>"leg_km"</c>), "join", "scout" (<c>"off_bow"</c> degrees), "screen", "cover" and "shadow" (<c>"of"</c> a unit's name,
/// any level of the scenario;
/// <c>"distance_km"</c>), "withdraw" (<c>"at"</c>, else home), "attack" (a flotilla's torpedo attack on the biggest enemy in
/// sight, done once its torpedoes are spent). <c>"tasks": [...]</c> gives several, each begun when the one
/// before it ends (the unit's plan, 5.1d); the side's commander gives the tasks after these.</summary>
public sealed class ScenarioTask
{
    public required string Kind { get; init; }
    public string? Of { get; init; }
    public (double Lat, double Lon)? At { get; init; }
    public double DistanceM { get; init; }
    public double OffBowDeg { get; init; }
    public double Kn { get; init; }

    public static readonly string[] Kinds = ["go to", "patrol", "search", "join", "scout", "screen", "cover", "shadow", "withdraw", "attack"];

    /// <summary>The task, of another unit (the one it named renamed).</summary>
    internal ScenarioTask OfUnit(string unit) => new() { Kind = Kind, Of = unit, At = At, DistanceM = DistanceM, OffBowDeg = OffBowDeg, Kn = Kn };
}

public sealed class ScenarioException(string message) : Exception(message);
