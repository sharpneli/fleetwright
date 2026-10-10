using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Fleetwright.Shipgen;

namespace Fleetwright.Designer;

/// <summary>A ready-made block for a part of the design: a plant's technology, a hull construction, a crew standard,
/// an armour material scheme. <see cref="Value"/> is the block as the design takes it.</summary>
public sealed record Template<T>(string Name, string Group, string Note, T Value);

/// <summary>
/// The template catalogues (Content/Templates, game assets written by tools/designer_templates.py from the shipgen
/// docs and the shipped designs). In the game the tech tree will filter them; the designer offers them all for now.
/// Loaded once, on first use; a missing file is an empty catalogue.
/// </summary>
public static class Templates
{
    static readonly Lazy<IReadOnlyList<Template<TechInput>>> plants = new(() => Load("plants.json", ShipgenJson.Default.TechInput));
    static readonly Lazy<IReadOnlyList<Template<ConstructionInput>>> hulls = new(() => Load("hulls.json", ShipgenJson.Default.ConstructionInput));
    static readonly Lazy<IReadOnlyList<Template<CrewStandardInput>>> crew = new(() => Load("crew.json", ShipgenJson.Default.CrewStandardInput));
    static readonly Lazy<IReadOnlyList<Template<Dictionary<string, string>>>> armour =
        new(() => Load("armour.json", ShipgenJson.Default.DictionaryStringString));

    public static IReadOnlyList<Template<TechInput>> Plants => plants.Value;
    public static IReadOnlyList<Template<ConstructionInput>> Hulls => hulls.Value;
    public static IReadOnlyList<Template<CrewStandardInput>> Crew => crew.Value;
    public static IReadOnlyList<Template<Dictionary<string, string>>> Armour => armour.Value;

    /// <summary>Where the catalogues are read from (tests point it at the repo's Content folder).</summary>
    public static string Folder { get; set; } = Path.Combine(AppContext.BaseDirectory, "Content", "Templates");

    static IReadOnlyList<Template<T>> Load<T>(string file, JsonTypeInfo<T> info)
    {
        var path = Path.Combine(Folder, file);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"templates missing: {path}");
            return [];
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var list = new List<Template<T>>();
        foreach (var e in doc.RootElement.EnumerateArray())
            list.Add(new Template<T>(e.GetProperty("name").GetString() ?? "", e.GetProperty("group").GetString() ?? "",
                e.GetProperty("note").GetString() ?? "", e.GetProperty("value").Deserialize(info)!));
        return list;
    }

    /// <summary>The armour parts every style may name, and the carriers' flight deck.</summary>
    public static Dictionary<string, string> MaterialsFor(Design d, Dictionary<string, string> scheme)
    {
        var m = new Dictionary<string, string>(scheme, StringComparer.Ordinal);
        if (d.StyleName == "carrier")
            m["flight_deck"] = scheme.GetValueOrDefault("decks", "mild steel");
        return m;
    }

    /// <summary>Does the design's materials map match the scheme (the flight deck aside)?</summary>
    public static bool SameMaterials(Dictionary<string, string>? a, Dictionary<string, string> scheme)
    {
        if (a == null)
            return false;
        var keys = a.Keys.Where(k => k != "flight_deck").ToList();
        return keys.Count == scheme.Count && keys.All(k => scheme.TryGetValue(k, out var v) && v == a[k]);
    }
}
