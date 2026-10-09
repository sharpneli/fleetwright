using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen.Golden;

/// <summary>One golden case (shipgen/golden/cases.json): a design file and how it was made.</summary>
public sealed record GoldenCase(string Name, string DesignPath, string? Source, bool Limits, IReadOnlyList<string> Changes);

/// <summary>The C# side of ../shipgen/tools/golden.py's design capture: the same record per case, so a golden case
/// file and ours compare as JSON trees (GoldenDiff).</summary>
public static class GoldenCases
{
    /// <summary>The cases listed in ROOT/golden/cases.json (ROOT: the repo's shipgen folder).</summary>
    public static List<GoldenCase> Load(string root)
    {
        var doc = JsonFile.Load(Path.Combine(root, "golden", "cases.json"))!;
        return doc["cases"]!.AsArray().Select(c => new GoldenCase(
            (string)c!["case"]!, Path.Combine(root, ((string)c["design"]!).Replace('/', Path.DirectorySeparatorChar)),
            (string?)c["source"], (bool)c["limits"]!, c["changes"]!.AsArray().Select(x => (string)x!).ToList())).ToList();
    }

    public static string GoldenFile(string root, string name) => Path.Combine(root, "golden", "design", name + ".json.gz");

    static JsonNode? Try(Func<JsonNode?> f)
    {
        try
        {
            return f();
        }
        catch (NotImplementedException)
        {
            throw;
        }
        catch (Exception e)
        {
            return new JsonObject { ["raised"] = new JsonObject { ["type"] = e.GetType().Name, ["message"] = e.Message } };
        }
    }

    static JsonArray Strings(List<string> xs) => new(xs.Select(x => (JsonNode?)x).ToArray());

    /// <summary>golden.py's capture_case: validation strings, the build (when validate(limits=False) passes), the
    /// hint check and the build time.</summary>
    public static JsonObject Capture(Design design)
    {
        var rec = new JsonObject
        {
            ["validate_limits"] = Try(() => Strings(ShipDesign.Validate(design, limits: true))),
            ["validate_no_limits"] = Try(() => Strings(ShipDesign.Validate(design, limits: false))),
            ["looks_validate"] = Try(() => Strings(Looks.Validate(design))),
        };
        if (rec["validate_no_limits"] is JsonArray { Count: 0 })
        {
            var sw = Stopwatch.StartNew();
            var build = Try(() => JsonFile.FromPy(ShipDesign.Build(design)));
            rec["build_s"] = Math.Round(sw.Elapsed.TotalSeconds, 3);
            rec["build"] = build;
            if (build is JsonObject b && !b.ContainsKey("raised"))
            {
                double L = (double)b["report"]!["results"]!["length_m"]!;
                var hinted = Try(() => JsonFile.FromPy(ShipDesign.Build(design, L)));
                var hint = new JsonObject { ["length_m"] = L };
                if (hinted is JsonObject h && h.ContainsKey("raised"))
                    hint["raised"] = h["raised"]!.DeepClone();
                else
                    hint["equal"] = JsonNode.DeepEquals(build, hinted);
                rec["hint"] = hint;
            }
        }
        return rec;
    }

    static JsonObject Ours(GoldenCase c)
    {
        var ours = Capture(Design.Load(c.DesignPath));
        ours.Remove("build_s");
        return ours;
    }

    static JsonObject Golden(string root, GoldenCase c)
    {
        var golden = JsonFile.Load(GoldenFile(root, c.Name))!.AsObject();
        golden.Remove("build_s");
        return golden;
    }

    /// <summary>Rewrite the case's golden record from ours when the two differ by the golden rules (after a deliberate
    /// change to the output). Returns whether it was rewritten.</summary>
    public static bool Update(string root, GoldenCase c)
    {
        var ours = Ours(c);
        if (GoldenDiff.Compare(Golden(root, c), ours, 1).Count == 0)
            return false;
        JsonFile.Save(GoldenFile(root, c.Name), ours);
        return true;
    }

    /// <summary>The differences between the golden record for a case and ours (build_s left out).</summary>
    public static List<Difference> Check(string root, GoldenCase c, int max = 20) => GoldenDiff.Compare(Golden(root, c), Ours(c), max);
}
