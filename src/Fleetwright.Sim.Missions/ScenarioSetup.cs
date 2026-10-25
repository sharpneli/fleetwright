using System.Globalization;
using System.Text;
using System.Text.Json;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Missions;

/// <summary>A setup (docs/map-ui/launch.md "A setup is a scenario file"): a scenario as written plus its changes, in
/// order. Applying it is a pure function on the parsed scenario (<see cref="Apply"/>: the written one is never touched);
/// the launch screen edits the changes and the file is them written out, so the two can't disagree. The file is a
/// scenario file with <c>"from"</c> and none of <c>"units"</c>, <c>"only"</c> or <c>"sides"</c> (those compose a
/// scenario, as the scale test's ladder does):
/// <code>
/// { "scenario": 1, "name": "Tsushima · 14 in Borodinos", "from": "tsushima", "player": "Russia",
///   "designs": [ { "unit": "1st Armoured Division", "from": "borodino", "to": "borodino_14in" },
///                { "ship": "Oslyabya", "from": "oslyabya", "to": "borodino" }, { "from": "buyny", "to": "ikazuchi" } ],
///   "crews": { "Japan": "average" }, "readiness": { "Russia": "closed up" }, "damage_control": { "Russia": "1916" },
///   "fire_discipline": { "Russia": "rn-1914" }, "fuel": { "Russia": 0.6 }, "seed": 1906 }
/// </code>
/// <para>A swap names a unit (any level: every ship of that design under it), or a ship by her name (only her), or
/// neither (every ship of that design). <c>"player": null</c> watches. A side's setting replaces the written one for that
/// side only. Tempers, doctrine, the day and adding or removing ships join as the screen's steps bring them (L3b-L5).</para></summary>
public sealed record ScenarioSetup
{
    /// <summary>The setup's own name ("Tsushima · 14 in Borodinos"), or "" for the written one's.</summary>
    public string Name { get; init; } = "";

    /// <summary>The scenario it's of, as <c>"from"</c> names it (its file's name, "tsushima").</summary>
    public string From { get; init; } = "";

    /// <summary>The changes in the order they apply; one per <see cref="SetupChange.Key"/>.</summary>
    public IReadOnlyList<SetupChange> Changes { get; init; } = [];

    public bool IsEmpty => Changes.Count == 0;

    /// <summary>The setup with a change made: it takes the place of any change of the same key.</summary>
    public ScenarioSetup With(SetupChange c)
    {
        var list = Changes.ToList();
        int at = list.FindIndex(x => x.Key == c.Key);
        if (at >= 0)
            list[at] = c;
        else
            list.Add(c);
        return this with { Changes = list };
    }

    /// <summary>The setup without a change (the screen's ✕), or without every change of a key.</summary>
    public ScenarioSetup Without(string key) => this with { Changes = [.. Changes.Where(x => x.Key != key)] };

    /// <summary>The scenario as set: the written one with every change applied in order; it remembers both
    /// (<see cref="Scenario.Setup"/>, <see cref="Scenario.Written"/>). A change that finds nothing to change (a swap of a
    /// design the unit hasn't) is a <see cref="ScenarioException"/>.</summary>
    public Scenario Apply(Scenario written)
    {
        written = written.Written ?? written;
        var s = written;
        foreach (var c in Changes)
            s = c.Apply(s);
        return s with { Name = Name.Length > 0 ? Name : written.Name, Setup = this, Written = written };
    }

    // ------------------------------------------------------------------ the file

    /// <summary>Whether a file's object is a setup: <c>"from"</c>, and nothing that composes a scenario.</summary>
    internal static bool IsSetup(JsonElement r) =>
        r.TryGetProperty("from", out _) && !r.TryGetProperty("units", out _) && !r.TryGetProperty("only", out _) && !r.TryGetProperty("sides", out _);

    static readonly string[] Keys = ["scenario", "name", "from", "player", "designs", "crews", "readiness", "damage_control", "fire_discipline", "fuel", "seed"];

    /// <summary>A setup file's changes, read against the scenario it's of.</summary>
    internal static ScenarioSetup Parse(JsonElement r, Scenario written)
    {
        foreach (var p in r.EnumerateObject())
            if (Array.IndexOf(Keys, p.Name) < 0)
                throw new ScenarioException($"a setup takes {string.Join(", ", Keys.Skip(1))}; not \"{p.Name}\"");
        var sides = written.Sides;
        var changes = new List<SetupChange>();
        if (r.TryGetProperty("player", out var pl))
        {
            string? player = pl.ValueKind == JsonValueKind.Null ? null : pl.GetString();
            if (player != null && Array.IndexOf(sides, player) < 0)
                throw new ScenarioException($"player: \"{player}\" isn't one of the sides ({string.Join(", ", sides)})");
            changes.Add(new PlayAs(player));
        }
        if (r.TryGetProperty("designs", out var ds))
        {
            if (ds.ValueKind != JsonValueKind.Array)
                throw new ScenarioException("designs: a list of swaps, [ { \"unit\": ..., \"from\": ..., \"to\": ... } ]");
            int i = 0;
            foreach (var d in ds.EnumerateArray())
            {
                string from = Str(d, "from"), to = Str(d, "to");
                if (from.Length == 0 || to.Length == 0)
                    throw new ScenarioException($"designs[{i}]: \"from\" and \"to\", the designs' names");
                changes.Add(new SwapDesign(Str(d, "unit") is { Length: > 0 } u ? u : null, Str(d, "ship") is { Length: > 0 } sh ? sh : null, from, to));
                i++;
            }
        }
        if (r.TryGetProperty("crews", out var cr))
            foreach (var (side, q) in Scenario.ParseCrews(cr, sides))
                changes.Add(new SideSetting(SideKind.Crews, side, q.ToString().ToLowerInvariant()));
        foreach (var (key, kind, what) in Named)
            if (r.TryGetProperty(key, out var e))
                foreach (var (side, name) in Scenario.ParseNames(key, what, e, sides))
                    changes.Add(new SideSetting(kind, side, name));
        if (r.TryGetProperty("fuel", out var fu))
            foreach (var (side, share) in Scenario.ParseFuel(fu, sides))
                changes.Add(new SideSetting(SideKind.Fuel, side, share.ToString("0.###", CultureInfo.InvariantCulture)));
        if (r.TryGetProperty("seed", out var se))
        {
            if (!se.TryGetUInt64(out ulong seed))
                throw new ScenarioException("seed: a whole number");
            changes.Add(new SetSeed(seed));
        }
        return new ScenarioSetup { Name = Str(r, "name"), From = Str(r, "from"), Changes = changes };
    }

    static readonly (string Key, SideKind Kind, string What)[] Named =
    [
        ("readiness", SideKind.Readiness, "a readiness's name"),
        ("damage_control", SideKind.DamageControl, "a doctrine's name"),
        ("fire_discipline", SideKind.FireDiscipline, "a discipline's name"),
    ];

    static string Str(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    /// <summary>The setup as a file of the scenario <see cref="From"/>: read back (<see cref="Scenario.Load"/>, with
    /// the folder its "from" is in), it applies to the same scenario.</summary>
    public string ToJson(Scenario written)
    {
        string from = From.Length > 0 ? From : throw new InvalidOperationException("a setup's file needs the scenario it's of (From)");
        written = written.Written ?? written;
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteNumber("scenario", Scenario.Version);
            if (Name.Length > 0)
                w.WriteString("name", Name);
            w.WriteString("from", from);
            foreach (var c in Changes.OfType<PlayAs>())
                if (c.Side == null)
                    w.WriteNull("player");
                else
                    w.WriteString("player", c.Side);
            var swaps = Changes.OfType<SwapDesign>().ToList();
            if (swaps.Count > 0)
            {
                w.WriteStartArray("designs");
                foreach (var sw in swaps)
                {
                    w.WriteStartObject();
                    if (sw.Unit != null)
                        w.WriteString("unit", sw.Unit);
                    if (sw.Ship != null)
                        w.WriteString("ship", sw.Ship);
                    w.WriteString("from", sw.From);
                    w.WriteString("to", sw.To);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            foreach (var kind in Enum.GetValues<SideKind>())
            {
                var set = Changes.OfType<SideSetting>().Where(x => x.Kind == kind).ToList();
                if (set.Count == 0)
                    continue;
                w.WriteStartObject(SideSetting.FileKey(kind));
                foreach (var x in set)
                    if (kind == SideKind.Fuel)
                        w.WriteNumber(written.Sides[x.Side], double.Parse(x.Value, CultureInfo.InvariantCulture));
                    else
                        w.WriteString(written.Sides[x.Side], x.Value);
                w.WriteEndObject();
            }
            foreach (var c in Changes.OfType<SetSeed>())
                w.WriteNumber("seed", c.Seed);
            w.WriteEndObject();
        }
        return $"// a setup of {written.Name} (docs/map-ui/launch.md \"A setup is a scenario file\"): the scenario \"{from}\" and these changes\n"
               + Encoding.UTF8.GetString(ms.ToArray()) + "\n";
    }
}

/// <summary>One change of a setup: what it's of (<see cref="Key"/>: a later change of the same key replaces it), how it
/// changes a scenario, and how it reads in the list of changes against the written one.</summary>
public abstract record SetupChange
{
    public abstract string Key { get; }

    internal abstract Scenario Apply(Scenario s);

    /// <summary>The change in a line, as the launch screen's list and the result say it.</summary>
    public abstract string Describe(Scenario written);
}

/// <summary>Play as a side (its name), or watch (null).</summary>
public sealed record PlayAs(string? Side) : SetupChange
{
    public override string Key => "player";

    internal override Scenario Apply(Scenario s) =>
        Side == null || Array.IndexOf(s.Sides, Side) >= 0 ? s with { PlayerName = Side }
            : throw new ScenarioException($"player: \"{Side}\" isn't one of the sides ({string.Join(", ", s.Sides)})");

    public override string Describe(Scenario written) =>
        (Side == null ? "Watch" : $"Play as {Side}") + $" (written: {(written.PlayerName is { } p ? "play as " + p : "watch")})";
}

/// <summary>Ships of one design given another: every one under a unit, one ship by her name, or every one in the
/// scenario.</summary>
public sealed record SwapDesign(string? Unit, string? Ship, string From, string To) : SetupChange
{
    public override string Key => $"design|{Unit}|{Ship}|{From}";

    internal override Scenario Apply(Scenario s)
    {
        bool found = false;
        var units = s.Units.Select(u => Swap(u, Unit == null || u.Name == Unit, ref found)).ToList();
        if (!found)
            throw new ScenarioException($"designs: no {From} {(Ship != null ? $"named {Ship}" : Unit != null ? $"in {Unit}" : "in the scenario")}");
        return s with { Units = units };
    }

    ScenarioUnit Swap(ScenarioUnit u, bool inScope, ref bool found)
    {
        var units = new List<ScenarioUnit>(u.Units.Count);
        foreach (var sub in u.Units)
            units.Add(Swap(sub, inScope || sub.Name == Unit, ref found));
        var ships = new List<ScenarioShips>(u.Ships.Count);
        foreach (var e in u.Ships)
        {
            if (e.Design != From || !inScope)
            {
                ships.Add(e);
                continue;
            }
            int k = Ship == null ? -1 : Array.IndexOf(e.Names, Ship);
            if (Ship == null)
            {
                ships.Add(e.Slice(0, e.Count, To));
                found = true;
            }
            else if (k >= 0)
            {
                // only her: the entry split round her, the order kept (the stations go by it)
                if (k > 0)
                    ships.Add(e.Slice(0, k, e.Design));
                ships.Add(e.Slice(k, 1, To));
                if (k + 1 < e.Count)
                    ships.Add(e.Slice(k + 1, e.Count - k - 1, e.Design));
                found = true;
            }
            else
                ships.Add(e);
        }
        return u.With(units, ships);
    }

    public override string Describe(Scenario written) => $"{Ship ?? Unit ?? "Every ship"}: {From} → {To}";
}

public enum SideKind
{
    Crews,
    Readiness,
    DamageControl,
    FireDiscipline,
    Fuel,
}

/// <summary>A side's setting: its gun crews, its readiness, its damage control, its fire discipline (names of the
/// settings'), or its fuel (a share of a full load), replacing the written one for that side.</summary>
public sealed record SideSetting(SideKind Kind, int Side, string Value) : SetupChange
{
    public override string Key => $"{Kind}|{Side}";

    internal static string FileKey(SideKind k) => k switch
    {
        SideKind.Crews => "crews",
        SideKind.Readiness => "readiness",
        SideKind.DamageControl => "damage_control",
        SideKind.FireDiscipline => "fire_discipline",
        _ => "fuel",
    };

    static string Label(SideKind k) => k switch
    {
        SideKind.Crews => "gun crews",
        SideKind.Readiness => "readiness",
        SideKind.DamageControl => "damage control",
        SideKind.FireDiscipline => "fire discipline",
        _ => "fuel",
    };

    internal override Scenario Apply(Scenario s)
    {
        if (Side < 0 || Side >= s.Sides.Length)
            throw new ScenarioException($"{FileKey(Kind)}: no side {Side}");
        return Kind switch
        {
            SideKind.Crews => s with { Crews = Over(s.Crews, Enum.Parse<CrewQuality>(Value, ignoreCase: true)) },
            SideKind.Readiness => s with { Readiness = Over(s.Readiness, Value) },
            SideKind.DamageControl => s with { DamageControl = Over(s.DamageControl, Value) },
            SideKind.FireDiscipline => s with { FireDisciplines = Over(s.FireDisciplines, Value) },
            _ => s with { Fuel = Over(s.Fuel, double.Parse(Value, CultureInfo.InvariantCulture)) },
        };
    }

    /// <summary>A side's entry replaced (or added), the others kept.</summary>
    List<(int Side, T)> Over<T>(List<(int Side, T)> list, T value) => [.. list.Where(x => x.Side != Side), (Side, value)];

    public override string Describe(Scenario written)
    {
        string was = Kind switch
        {
            SideKind.Crews => written.Crews.Where(x => x.Side == Side).Select(x => x.Quality.ToString().ToLowerInvariant()).FirstOrDefault() ?? "trained",
            SideKind.Readiness => written.Readiness.Where(x => x.Side == Side).Select(x => x.Name).FirstOrDefault() ?? "the default",
            SideKind.DamageControl => written.DamageControl.Where(x => x.Side == Side).Select(x => x.Name).FirstOrDefault() ?? "the default",
            SideKind.FireDiscipline => written.FireDisciplines.Where(x => x.Side == Side).Select(x => x.Name).FirstOrDefault() ?? "the default",
            _ => written.Fuel.Where(x => x.Side == Side).Select(x => x.Share.ToString("0.###", CultureInfo.InvariantCulture)).FirstOrDefault() ?? "full",
        };
        return $"{written.Sides[Side]}'s {Label(Kind)}: {Value} (written: {was})";
    }
}

/// <summary>The world's seed: the same fight again for the same seed.</summary>
public sealed record SetSeed(ulong Seed) : SetupChange
{
    public override string Key => "seed";

    internal override Scenario Apply(Scenario s) => s with { Seed = Seed };

    public override string Describe(Scenario written) => $"Seed {Seed} (written: {written.Seed})";
}
