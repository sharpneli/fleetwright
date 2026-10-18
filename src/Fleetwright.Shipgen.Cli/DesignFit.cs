using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen fit FILE... --main SPEC [--secondary SPEC] [--aa SPEC] [--dry]` (docs/battle/fire-control.md F3):
/// sets batteries' fire-control fits in design files. A SPEC is a preset's name (FireControlTemplates.Presets) or block
/// names joined by `|`; a battery without directors is left alone. The design's fire_control_tech is then the blocks its
/// fits name, the catalogue's for a name the catalogue has, the design's own for the rest. Each file keeps its layout
/// (DesignUpgrade's writer) and is validated after.</summary>
static class DesignFit
{
    public static int Run(IReadOnlyList<string> paths, IReadOnlyDictionary<string, string> specs, bool dry)
    {
        var fits = new Dictionary<string, List<string>>();
        foreach (var (bat, spec) in specs)
            fits[bat] = FireControlTemplates.PresetNamed(spec) is { } p ? [.. p.Fit] : [.. spec.Split('|', StringSplitOptions.TrimEntries)];
        if (fits.Count == 0)
            throw new ArgumentException("give a fit: --main, --secondary or --aa (a preset's name, or blocks joined by |)");
        foreach (var p in paths)
        {
            string text = File.ReadAllText(p);
            var before = JsonNode.Parse(text)!.AsObject();
            var after = before.DeepClone().AsObject();
            if (after["fire_control"] is not JsonObject fc)
            {
                Console.WriteLine($"{p}: no fire_control");
                continue;
            }
            var set = new List<string>();
            foreach (var (bat, names) in fits)
            {
                if (fc[bat] is not JsonObject spec || !(spec["directors"] is JsonValue v && v.TryGetValue(out double n) && n > 0))
                    continue;
                spec["fit"] = new JsonArray([.. names.Select(x => (JsonNode)JsonValue.Create(x)!)]);
                set.Add($"{bat} [{string.Join(", ", names)}]");
            }
            // the table: the blocks the fits name, the catalogue's where it has them
            var old = after["fire_control_tech"] as JsonObject;
            var table = new JsonObject();
            foreach (var bat in (ReadOnlySpan<string>)["main", "secondary", "aa"])
                foreach (var name in (fc[bat]?["fit"] as JsonArray ?? []).Select(x => x!.GetValue<string>()))
                    if (!table.ContainsKey(name))
                        table[name] = FireControlTemplates.Get(name) is { } t
                            ? JsonSerializer.SerializeToNode(t.Value, ShipgenJson.Default.FcTechInput)
                            : old?[name]?.DeepClone() ?? throw new InvalidOperationException($"{p}: no block \"{name}\" in the design or the catalogue");
            if (table.Count > 0)
                after["fire_control_tech"] = table;
            else
                after.Remove("fire_control_tech");
            var changed = after.Where(kv => !JsonNode.DeepEquals(kv.Value, before[kv.Key])).Select(kv => kv.Key).ToList();
            if (changed.Count == 0)
            {
                Console.WriteLine($"{p}: as it is");
                continue;
            }
            Console.WriteLine($"{p}: {string.Join("; ", set)}");
            if (dry)
                continue;
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            string written = DesignUpgrade.Pretty(text, nl) is { } indent ? DesignUpgrade.Write(after, indent, nl)
                : DesignUpgrade.Splice(text, before, after, changed, [], nl);
            var errs = ShipDesign.Validate(Design.Parse(written), limits: false);
            if (errs.Count > 0)
                throw new InvalidOperationException($"{p}: {string.Join("; ", errs)}");
            File.WriteAllText(p, written);
        }
        return 0;
    }
}
