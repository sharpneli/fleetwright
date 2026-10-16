using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fleetwright.Shipgen;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen scheme`: a protection scheme (docs/shipgen/protection-templates.md, Content/Templates/protection.json)
/// put on designs by ProtectionSchemes.Apply: each design's standard displacement and length before and after, and with
/// --write its file's armour line rewritten (the shipped designs' one-key-a-line form; values the scheme didn't touch
/// keep their text, new keys go before the materials).</summary>
static class SchemeCommand
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(string catalogue, string scheme, IReadOnlyList<string> paths, bool write)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(catalogue));
        ProtectionSchemeInput? s = null;
        foreach (var e in doc.RootElement.EnumerateArray())
            if (e.GetProperty("note").GetString() == scheme || e.GetProperty("name").GetString() == scheme)
                s = e.GetProperty("value").Deserialize(ShipgenJson.Default.ProtectionSchemeInput);
        if (s is null)
        {
            Console.Error.WriteLine($"no scheme '{scheme}' in {catalogue}");
            return 2;
        }
        int bad = 0;
        foreach (var path in paths)
        {
            var before = Design.Load(path);
            var after = ProtectionSchemes.Apply(before, s);
            var errs = ShipDesign.Validate(after);
            if (errs.Count > 0)
            {
                Console.WriteLine($"{Path.GetFileNameWithoutExtension(path)}: {errs[0]}");
                bad++;
                continue;
            }
            var (r0, r1) = (ShipDesign.Build(before).Report.Results, ShipDesign.Build(after).Report.Results);
            Console.WriteLine(string.Format(Inv, "{0,-20} std {1,7:N0} -> {2,7:N0} t ({3:+0.0;-0.0}%)  L {4:0.0} -> {5:0.0} m", Path.GetFileNameWithoutExtension(path),
                r0.StandardDisplacementT, r1.StandardDisplacementT, 100.0 * (r1.StandardDisplacementT - r0.StandardDisplacementT) / r0.StandardDisplacementT,
                r0.LengthM, r1.LengthM));
            if (write)
                Rewrite(path, JsonSerializer.SerializeToNode(after.Armour, ShipgenJson.Default.ArmourInput)!);
        }
        return bad > 0 ? 1 : 0;
    }

    /// <summary>The file's top-level armour value replaced by the new armour, merged onto the old so untouched values keep
    /// their text; the rest of the file as it was, character for character.</summary>
    static void Rewrite(string path, JsonNode armour)
    {
        string text = File.ReadAllText(path);
        var (start, end) = TopLevelValue(text, "armour");
        if (start < 0)
            throw new InvalidOperationException($"{path}: no armour");
        var old = JsonNode.Parse(text[start..end])!;
        File.WriteAllText(path, text[..start] + Format(Merge(old, armour)) + text[end..]);
    }

    /// <summary>Where a top-level key's object value stands in a JSON text (its first and one-past-last character), or
    /// (-1, -1).</summary>
    static (int Start, int End) TopLevelValue(string text, string key)
    {
        int depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '"')
            {
                int j = StringEnd(text, i);
                // a key of the outermost object: its value follows the colon
                if (depth == 1 && text.AsSpan(i + 1, j - i - 1).SequenceEqual(key) && text[(j + 1)..].TrimStart().StartsWith(':'))
                {
                    int v = text.IndexOf(':', j) + 1;
                    while (char.IsWhiteSpace(text[v]))
                        v++;
                    return (v, ValueEnd(text, v));
                }
                i = j;
            }
            else if (ch is '{' or '[')
                depth++;
            else if (ch is '}' or ']')
                depth--;
        }
        return (-1, -1);
    }

    /// <summary>The closing quote of the string opening at i.</summary>
    static int StringEnd(string text, int i)
    {
        int j = i + 1;
        while (text[j] != '"')
            j += text[j] == '\\' ? 2 : 1;
        return j;
    }

    /// <summary>One past the end of the JSON object or array starting at i.</summary>
    static int ValueEnd(string text, int i)
    {
        int depth = 0;
        for (int k = i; k < text.Length; k++)
        {
            char ch = text[k];
            if (ch == '"')
                k = StringEnd(text, k);
            else if (ch is '{' or '[')
                depth++;
            else if (ch is '}' or ']' && --depth == 0)
                return k + 1;
        }
        throw new InvalidOperationException("an unclosed value");
    }

    /// <summary>The new value, with the old one's nodes kept where they're equal (their text: 1.0 stays 1.0) and its
    /// keys' order; new keys go before "materials".</summary>
    static JsonNode Merge(JsonNode? old, JsonNode now)
    {
        if (old is not null && JsonNode.DeepEquals(Normal(old), Normal(now)))
            return old.DeepClone();
        if (old is JsonObject o && now is JsonObject n)
        {
            var result = new JsonObject();
            foreach (var (k, v) in o)
                if (k != "materials" && n[k] is { } nv)
                    result[k] = Merge(v, nv);
            foreach (var (k, v) in n)
                if (k != "materials" && !o.ContainsKey(k) && v is not null)
                    result[k] = v.DeepClone();
            if (n["materials"] is { } m)
                result["materials"] = Merge(o["materials"], m);
            return result;
        }
        if (old is JsonArray oa && now is JsonArray na && oa.Count == na.Count)
            return new JsonArray([.. oa.Select((v, k) => Merge(v, na[k]!))]);
        return now.DeepClone();
    }

    /// <summary>A value with its numbers as doubles, so 1.0 and 1 compare equal.</summary>
    static JsonNode? Normal(JsonNode? n) => n switch
    {
        JsonObject o => new JsonObject(o.Select(kv => KeyValuePair.Create(kv.Key, Normal(kv.Value)))),
        JsonArray a => new JsonArray([.. a.Select(Normal)]),
        JsonValue v when v.GetValueKind() == JsonValueKind.Number => JsonValue.Create(v.GetValue<double>()),
        _ => n?.DeepClone(),
    };

    /// <summary>JSON on one line as the shipped designs have it: ", " and ": " between items.</summary>
    static string Format(JsonNode? n)
    {
        var sb = new StringBuilder();
        void W(JsonNode? x)
        {
            switch (x)
            {
                case JsonObject o:
                    sb.Append('{');
                    bool first = true;
                    foreach (var (k, v) in o)
                    {
                        if (!first)
                            sb.Append(", ");
                        first = false;
                        sb.Append(JsonValue.Create(k)!.ToJsonString()).Append(": ");
                        W(v);
                    }
                    sb.Append('}');
                    break;
                case JsonArray a:
                    sb.Append('[');
                    for (int k = 0; k < a.Count; k++)
                    {
                        if (k > 0)
                            sb.Append(", ");
                        W(a[k]);
                    }
                    sb.Append(']');
                    break;
                default:
                    sb.Append(x?.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? "null");
                    break;
            }
        }
        W(n);
        return sb.ToString();
    }
}
