using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Fleetwright.Shipgen;

/// <summary>JSON trees on disk: .json, .jsonc (comments allowed) and .json.gz, written indented by one space.</summary>
public static class JsonFile
{
    static readonly JsonDocumentOptions Reading = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static JsonSerializerOptions Writing(int indent) => new()
    {
        WriteIndented = true, IndentSize = indent, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static JsonNode? Parse(string text) => JsonNode.Parse(text, documentOptions: Reading);

    public static JsonNode? Load(string path) => Parse(ReadText(path));

    /// <summary>The file's text, unzipped when it ends in .gz.</summary>
    public static string ReadText(string path)
    {
        if (!path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            return File.ReadAllText(path, Encoding.UTF8);
        using var fs = File.OpenRead(path);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var sr = new StreamReader(gz, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    public static string Write(JsonNode? node, int indent = 1) => node?.ToJsonString(Writing(indent)) ?? "null";

    public static void Save(string path, JsonNode? node, int indent = 1)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } dir)
            Directory.CreateDirectory(dir);
        var bytes = Encoding.UTF8.GetBytes(Write(node, indent) + "\n");
        if (!path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllBytes(path, bytes);
            return;
        }
        using var fs = File.Create(path);
        using var gz = new GZipStream(fs, CompressionLevel.SmallestSize);
        gz.Write(bytes);
    }

    /// <summary>Whether a JSON number is written as an integer (no fraction or exponent) and fits a long.</summary>
    public static bool IsInteger(JsonValue v, out long n)
    {
        n = 0;
        return v.GetValueKind() == JsonValueKind.Number && v.TryGetValue(out JsonElement e) ? e.TryGetInt64(out n) : v.TryGetValue(out n);
    }

    /// <summary>A typed value written as JSON (the shipgen context's options).</summary>
    public static void Save<T>(string path, T value, JsonTypeInfo<T> info, int indent = 1) =>
        Save(path, JsonSerializer.SerializeToNode(value, info), indent);
}
