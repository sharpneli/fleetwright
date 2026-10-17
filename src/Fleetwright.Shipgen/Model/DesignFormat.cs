using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen;

/// <summary>The design file's format version (the top-level "version" key) and the upgrades from older ones. A file
/// without the key is version 0, from before versions. When the format changes (a key renamed, moved, or one whose
/// meaning or default changes), bump <see cref="Current"/> and add the step from the old version: it rewrites the raw
/// JSON, so it can still see keys the typed design no longer has. A new optional key with a default that keeps old
/// designs as they were needs no step.</summary>
public static class DesignFormat
{
    /// <summary>The version this build writes.</summary>
    public const int Current = 3;

    /// <summary>Steps[v] upgrades a version v design to v + 1.</summary>
    static readonly Action<JsonObject>[] Steps =
    [
        _ => { },   // 0 → 1: versions began; nothing else changed
        MaterialsTable,   // 1 → 2: the materials table (docs/battle/shells-armour-damage.md P0)
        NoOutcomeRows,    // 2 → 3: the shells' outcome rows gone (P3)
    ];

    /// <summary>2 → 3: an ammunition block named the sim's outcome-table row (`outcomes`, S0's transitional field); its
    /// fuze's blinds and its explosive's rates replace it (P3). The field goes, and a block whose fuze gives no `blind`
    /// takes its row's dud share (the rows' last numbers: rn1916 0.05, km1916 0.15, rn1918 0.10, ww2 0.08, ijn_t91 0.13).
    /// The loadout blocks in a scenario aren't designs; they're read as given.</summary>
    static void NoOutcomeRows(JsonObject d)
    {
        if (d["ammunition"] is not JsonObject table)
            return;
        foreach (var (_, node) in table)
        {
            if (node is not JsonObject a || a["outcomes"] is not JsonValue row)
                continue;
            a.Remove("outcomes");
            double dud = row.TryGetValue(out string? name) ? name switch
            {
                "km1916" => 0.15, "rn1918" => 0.10, "ww2" => 0.08, "ijn_t91" => 0.13, _ => 0.05,
            } : 0.05;
            if (a["fuze"] is JsonObject fuze && !fuze.ContainsKey("blind"))
                fuze["blind"] = dud;
        }
    }

    /// <summary>1 → 2: a material was a name the game looked up; now it's the design's block (MaterialTemplates).
    /// Each name the parts give (any "material" or "deck_material", and armour.materials' values) gets the catalogue's
    /// block in `materials`, where the catalogue knows it and the design doesn't have one. A version 1 design has no
    /// other "material" keys, so the walk needn't know the parts.</summary>
    static void MaterialsTable(JsonObject d)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        void Walk(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    foreach (var (k, v) in o)
                        if (k is "material" or "deck_material" && v is JsonValue s && s.TryGetValue(out string? name) && name.Length > 0)
                            names.Add(name);
                        else
                            Walk(v);
                    break;
                case JsonArray a:
                    foreach (var v in a)
                        Walk(v);
                    break;
            }
        }
        Walk(d);
        if (d["armour"]?["materials"] is JsonObject parts)
            foreach (var (_, v) in parts)
                if (v is JsonValue s && s.TryGetValue(out string? name) && name.Length > 0)
                    names.Add(name);
        var table = d["materials"] as JsonObject;
        foreach (var n in names)
            if (MaterialTemplates.Get(n) is { } m && table?.ContainsKey(n) != true)
            {
                if (table is null)
                    d["materials"] = table = [];
                table[n] = JsonSerializer.SerializeToNode(m, ShipgenJson.Default.MaterialInput);
            }
    }

    static readonly JsonDocumentOptions ReadOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>The design JSON as this build reads it: older versions upgraded to <see cref="Current"/>, a newer one
    /// left as it is (<see cref="Warnings"/> says so).</summary>
    public static JsonObject Upgrade(string json)
    {
        var d = JsonNode.Parse(json, documentOptions: ReadOptions) as JsonObject
            ?? throw new JsonException("a design is a JSON object");
        int v = Version(d);
        if (v < Current)
        {
            for (; v < Current; v++)
                Steps[v](d);
            d["version"] = Current;
        }
        return d;
    }

    static int Version(JsonObject d)
    {
        if (d["version"] is not JsonValue n)
            return 0;
        if (!n.TryGetValue(out int v) || v < 0)
            throw new JsonException($"version: {n.ToJsonString()} isn't a design format version (a whole number, 0 or more)");
        return v;
    }

    /// <summary>The build's warning for a design from a newer Fleetwright: it loads, but what this one doesn't know is
    /// ignored.</summary>
    public static string[] Warnings(Design d) => d.Version > Current
        ? [$"Made with a newer Fleetwright (design format {d.Version}, this one reads up to {Current}): what it doesn't know is ignored."]
        : [];
}
