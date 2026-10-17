using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen upgrade FILE...`: design files rewritten to the current format (DesignFormat.Upgrade, the one
/// upgrade the game does as it reads), each in its own layout: a file an indented writer made (a scenario's designs)
/// is written the same way, and the shipped designs' hand-laid lines keep theirs, with only the top-level values the
/// upgrade changed rewritten and the keys it added on lines of their own at the end. `--materials` also sets the
/// design's `materials` to the catalogue's blocks (MaterialTemplates) for the names its parts give, after a change to
/// the catalogue (a name the catalogue doesn't know keeps the design's block). `--dry` lists what it would do. Every
/// file is validated after; a file already current is left alone.</summary>
static class DesignUpgrade
{
    public static int Run(IReadOnlyList<string> paths, bool dry, bool materials = false)
    {
        int written = 0, current = 0;
        foreach (var p in paths)
        {
            string text = File.ReadAllText(p);
            var before = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
            var after = DesignFormat.Upgrade(text);
            if (materials)
                Materials(after);
            var changed = after.Where(kv => !JsonNode.DeepEquals(kv.Value, before[kv.Key])).Select(kv => kv.Key).ToList();
            var gone = before.Select(kv => kv.Key).Where(k => !after.ContainsKey(k)).ToList();
            if (changed.Count == 0 && gone.Count == 0)
            {
                current++;
                continue;
            }
            Console.WriteLine($"{p}: {string.Join(", ", changed)}{(gone.Count > 0 ? "; removed " + string.Join(", ", gone) : "")}");
            written++;
            if (dry)
                continue;
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            string upgraded = Pretty(text, nl) is { } indent ? Write(after, indent, nl) : Splice(text, before, after, changed, gone, nl);
            var errs = ShipDesign.Validate(Design.Parse(upgraded), limits: false);
            if (errs.Count > 0)
                throw new InvalidOperationException($"{p}: {string.Join("; ", errs)}");
            if (!JsonNode.DeepEquals(JsonNode.Parse(upgraded), after))
                throw new InvalidOperationException($"{p}: the rewritten file doesn't read back as the upgrade");
            File.WriteAllText(p, upgraded);
        }
        Console.WriteLine($"{written} {(dry ? "to write" : "written")}, {current} already current (format {DesignFormat.Current})");
        return 0;
    }

    /// <summary>The design's materials table as the catalogue has it, for the names its parts give.</summary>
    static void Materials(JsonObject design)
    {
        var d = design.Deserialize(ShipgenJson.Default.Design)!;
        var table = new JsonObject();
        foreach (var n in MaterialTemplates.Used(d))
            if ((MaterialTemplates.Get(n) ?? d.Materials?.GetValueOrDefault(n)) is { } m)
                table[n] = JsonSerializer.SerializeToNode(m, ShipgenJson.Default.MaterialInput);
        if (table.Count > 0)
            design["materials"] = table;
        else
            design.Remove("materials");
    }

    static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>A value as the shipped designs lay it out: one line, a space after each comma and colon.</summary>
    public static string Spaced(JsonNode n) => n.ToJsonString(Compact).Replace(",\"", ", \"").Replace("\":", "\": ").Replace("},{", "}, {");

    /// <summary>The indent of a file written by an indented writer (a scenario's designs), when re-writing it gives it
    /// back byte for byte; null for the shipped designs' hand-laid lines.</summary>
    public static int? Pretty(string text, string nl)
    {
        var node = JsonNode.Parse(text)!;
        foreach (int indent in new[] { 1, 2 })
            if (Write(node, indent, nl) == text)
                return indent;
        return null;
    }

    public static string Write(JsonNode node, int indent, string nl) => node.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true, IndentSize = indent, NewLine = nl, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    /// <summary>Into hand-laid lines: each changed top-level value replaced in place, each new key added after the last
    /// value on a line of its own. Removing a key isn't done here (no upgrade has).</summary>
    static string Splice(string text, JsonObject before, JsonObject after, List<string> changed, List<string> gone, string nl)
    {
        if (gone.Count > 0)
            throw new InvalidOperationException($"removing top-level keys ({string.Join(", ", gone)}) from hand-laid lines isn't done");
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var spans = new List<(int Start, int End, string With)>();
        int depth = 0, lastEnd = -1;
        while (reader.Read())
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName when depth == 1:
                    {
                        string key = reader.GetString()!;
                        reader.Read();
                        int start = (int)reader.TokenStartIndex;
                        reader.Skip();
                        lastEnd = (int)(reader.TokenStartIndex + reader.ValueSpan.Length);
                        if (reader.TokenType is JsonTokenType.String)
                            lastEnd += 2;   // the quotes
                        if (changed.Contains(key))
                            spans.Add((start, lastEnd, Spaced(after[key]!)));
                        break;
                    }
                case JsonTokenType.StartObject or JsonTokenType.StartArray:
                    depth++;
                    break;
                case JsonTokenType.EndObject or JsonTokenType.EndArray:
                    depth--;
                    break;
            }
        var added = changed.Where(k => !before.ContainsKey(k)).ToList();
        if (added.Count > 0)
            spans.Add((lastEnd, lastEnd, string.Concat(added.Select(k => $",{nl} \"{k}\": {Spaced(after[k]!)}"))));
        var sb = new StringBuilder();
        int at = 0;
        foreach (var (start, end, with) in spans.OrderBy(s => s.Start))
        {
            sb.Append(Encoding.UTF8.GetString(bytes, at, start - at)).Append(with);
            at = end;
        }
        return sb.Append(Encoding.UTF8.GetString(bytes, at, bytes.Length - at)).ToString();
    }
}
