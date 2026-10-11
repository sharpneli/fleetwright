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
    public const int Current = 1;

    /// <summary>Steps[v] upgrades a version v design to v + 1.</summary>
    static readonly Action<JsonObject>[] Steps =
    [
        _ => { },   // 0 → 1: versions began; nothing else changed
    ];

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
