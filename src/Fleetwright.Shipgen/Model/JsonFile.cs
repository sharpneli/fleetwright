using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen;

/// <summary>JSON trees on disk: .json, .jsonc (comments allowed) and .json.gz, written indented by one space.</summary>
public static class JsonFile
{
    static readonly JsonDocumentOptions Reading = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static readonly JsonSerializerOptions Writing = new()
    {
        WriteIndented = true, IndentSize = 1, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
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

    public static string Write(JsonNode? node) => node?.ToJsonString(Writing) ?? "null";

    public static void Save(string path, JsonNode? node)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } dir)
            Directory.CreateDirectory(dir);
        var bytes = Encoding.UTF8.GetBytes(Write(node) + "\n");
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

    /// <summary>Temporary: a port value (PyDict tree) as a JSON tree, until the output is typed.</summary>
    public static JsonNode? FromPy(object? v) => Parse(PyJson.Dumps(PyJson.Plain(v), null));
}
