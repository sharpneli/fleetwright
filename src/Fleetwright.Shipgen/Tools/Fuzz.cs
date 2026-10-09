using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen.Tools;

/// <summary>fuzz: the design side's robustness test (shipgen's fuzz.py). Mutants of the designs must build: with
/// errors and warnings if they're silly, but never an exception, a hang, a memory blow-up or a NaN in the published
/// result. A mutant makes 1-4 changes, by mode:
/// <list type="bullet">
/// <item>numbers: a number with an input limit is drawn inside its range (a third of the time at an end of it);
/// any other number (plant tech, crew standard, ...) is scaled by 0.25-4x, or zeroed now and then;</item>
/// <item>choices: a tickbox flipped, or a multiple choice swapped for another option the designs use for that key
/// (any combination of ticks must give a ship);</item>
/// <item>structure: a list entry duplicated or dropped, or an optional key deleted so its default applies.</item>
/// </list>
/// Labels (id, name, look, materials) are left alone. The RNG is .NET's seeded System.Random, so a run is
/// reproducible from its seed (the mutants differ from Python's, which don't need to match). The CLI runs each
/// mutant in a child process with a time and memory cap (`shipgen fuzz`).</summary>
public static class Fuzz
{
    static readonly string[] Labels = ["id", "name", "look", "material", "materials", "type"];   // free text, not choices

    /// <summary>A step on a path into a design: an object key or an array index.</summary>
    public readonly record struct Step(string? Key, int Index)
    {
        public override string ToString() => Key ?? Index.ToString();
    }

    public sealed record Change(string Path, string Was, string Now)
    {
        public override string ToString() => $"{Path} {Was} -> {Now}";
    }

    static IEnumerable<(Step S, JsonNode? V)> Items(JsonNode? c) => c switch
    {
        JsonObject o => o.Select(kv => (new Step(kv.Key, 0), kv.Value)),
        JsonArray a => a.Select((v, i) => (new Step(null, i), v)),
        _ => [],
    };

    static bool Label(Step s) => s.Key != null && Labels.Contains(s.Key);

    static JsonValueKind Kind(JsonNode? v) => v?.GetValueKind() ?? JsonValueKind.Null;

    /// <summary>Every leaf passing keep, array entries by index, labels left out.</summary>
    static List<(Step[] Path, JsonValue V)> Leaves(JsonNode? d, Func<JsonValueKind, bool> keep, Step[]? path = null)
    {
        path ??= [];
        var result = new List<(Step[], JsonValue)>();
        foreach (var (s, v) in Items(d))
        {
            if (Label(s))
                continue;
            if (v is JsonObject or JsonArray)
                result.AddRange(Leaves(v, keep, [.. path, s]));
            else if (v is JsonValue jv && keep(Kind(jv)))
                result.Add(([.. path, s], jv));
        }
        return result;
    }

    static List<(Step[] Path, JsonNode C)> Containers(JsonNode? d, Step[]? path = null)
    {
        path ??= [];
        var result = new List<(Step[], JsonNode)>();
        foreach (var (s, v) in Items(d))
            if (!Label(s) && v is JsonObject or JsonArray)
            {
                result.Add(([.. path, s], v!));
                result.AddRange(Containers(v, [.. path, s]));
            }
        return result;
    }

    static string Key(Step[] path) => path.Reverse().First(s => s.Key != null).Key!;

    static string Show(Step[] path) => string.Join(".", path.Select(s => s.ToString()));

    static void SetAt(JsonNode d, Step[] path, JsonNode v)
    {
        foreach (var s in path[..^1])
            d = s.Key != null ? d[s.Key]! : d[s.Index]!;
        var last = path[^1];
        if (last.Key != null)
            d[last.Key] = v;
        else
            d[last.Index] = v;
    }

    static bool IsNumber(JsonValueKind k) => k == JsonValueKind.Number;
    static bool IsString(JsonValueKind k) => k == JsonValueKind.String;
    static bool IsBool(JsonValueKind k) => k is JsonValueKind.True or JsonValueKind.False;

    /// <summary>{key: the string values the designs give it}: the options a mutant may swap in.</summary>
    public static Dictionary<string, List<string>> CorpusChoices(IEnumerable<JsonObject> designs)
    {
        var seen = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var d in designs)
            foreach (var (path, v) in Leaves(d, IsString))
            {
                var k = Key(path);
                if (!seen.TryGetValue(k, out var set))
                    seen[k] = set = new SortedSet<string>(StringComparer.Ordinal);
                set.Add((string)v!);
            }
        return seen.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.Ordinal);
    }

    static (double Lo, double Hi)? LimitOf(List<Limit> limits, Step[] path)
    {
        var keys = path.Where(s => s.Key != null).Select(s => s.Key!).ToArray();
        foreach (var l in limits)
            if (l.Path.SequenceEqual(keys))
                return (l.Lo, l.Hi);
        return null;
    }

    static Change? MutateNumber(JsonObject d, Random rng, List<Limit> limits)
    {
        var nums = Leaves(d, IsNumber);
        if (nums.Count == 0)
            return null;
        var (path, v) = nums[rng.Next(nums.Count)];
        double nv;
        if (LimitOf(limits, path) is var (lo, hi))
        {
            if (rng.NextDouble() < 1.0 / 3)
                nv = rng.Next(2) == 0 ? lo : hi;
            else if (lo > 0 && hi / lo > 20)
                nv = Math.Exp(Math.Log(lo) + (Math.Log(hi) - Math.Log(lo)) * rng.NextDouble());
            else
                nv = lo + (hi - lo) * rng.NextDouble();
        }
        else if (rng.NextDouble() < 0.1)
            nv = 0;
        else
            nv = (double)v * Math.Exp(Math.Log(0.25) + (Math.Log(4) - Math.Log(0.25)) * rng.NextDouble());
        JsonValue nvo = JsonFile.IsInteger(v, out _) ? JsonValue.Create((long)Math.Round(nv)) : JsonValue.Create(Math.Round(nv, 3));
        string was = v.ToJsonString();
        SetAt(d, path, nvo);
        return new Change(Show(path), was, nvo.ToJsonString());
    }

    static Change? MutateChoice(JsonObject d, Random rng, Dictionary<string, List<string>> choices)
    {
        var opts = Leaves(d, IsBool)
            .Concat(Leaves(d, IsString).Where(p => choices.TryGetValue(Key(p.Path), out var c) && c.Count > 1)).ToList();
        if (opts.Count == 0)
            return null;
        var (path, v) = opts[rng.Next(opts.Count)];
        JsonValue nv;
        if (IsBool(Kind(v)))
            nv = JsonValue.Create(!(bool)v);
        else
        {
            var others = choices[Key(path)].Where(c => c != (string)v!).ToList();
            nv = JsonValue.Create(others[rng.Next(others.Count)]);
        }
        string was = v.ToJsonString();
        SetAt(d, path, nv);
        return new Change(Show(path), was, nv.ToJsonString());
    }

    static Change? MutateStructure(JsonObject d, Random rng)
    {
        var conts = Containers(d).Where(c => c.C is JsonObject { Count: > 0 } or JsonArray { Count: > 0 }).ToList();
        if (conts.Count == 0)
            return null;
        var (path, c) = conts[rng.Next(conts.Count)];
        if (c is JsonArray l)
        {
            int i = rng.Next(l.Count);
            if (l[0] is JsonObject && rng.NextDouble() < 0.5)
            {
                l.Insert(i, l[i]!.DeepClone());
                return new Change(Show(path), $"{l.Count - 1} entries", $"{l.Count} (entry {i} twice)");
            }
            l.RemoveAt(i);
            return new Change(Show(path), $"{l.Count + 1} entries", $"{l.Count} (entry {i} dropped)");
        }
        var obj = (JsonObject)c;
        var k = obj.ElementAt(rng.Next(obj.Count)).Key;
        var was = obj[k]?.ToJsonString() ?? "null";
        obj.Remove(k);
        return new Change(Show([.. path, new Step(k, 0)]), was.Length > 40 ? was[..40] : was, "(deleted)");
    }

    /// <summary>A mutant of design: 1-4 changes as mode asks ("all": numbers 60%, choices 25%, structure 15%).</summary>
    public static (JsonObject Design, List<Change> Changes) Mutate(JsonObject design, Random rng, bool useLimits,
        Dictionary<string, List<string>> choices, string mode = "all")
    {
        var d = design.DeepClone().AsObject();
        var changes = new List<Change>();
        int n = rng.Next(1, 5);
        for (int i = 0; i < n; i++)
        {
            string kind = mode;
            if (mode == "all")
            {
                double r = rng.NextDouble();
                kind = r < 0.6 ? "numbers" : r < 0.85 ? "choices" : "structure";
            }
            List<Limit> limits;
            try
            {
                string style = d["style"] is JsonValue sv && sv.TryGetValue(out string? s) ? s : d.ContainsKey("style") ? "" : "warship";
                limits = useLimits ? Styles.Get(style).Limits() : [];
            }
            catch (Exception)   // the style itself was swapped for one that doesn't exist: validate says so
            {
                limits = [];
            }
            var ch = kind switch
            {
                "numbers" => MutateNumber(d, rng, limits),
                "choices" => MutateChoice(d, rng, choices),
                _ => MutateStructure(d, rng),
            };
            if (ch != null)
                changes.Add(ch);
        }
        return (d, changes);
    }

    /// <summary>No NaN or infinity in the tree (the serializer writes them as the strings "NaN", "Infinity").</summary>
    public static bool Finite(JsonNode? n) => n switch
    {
        JsonObject o => o.All(kv => Finite(kv.Value)),
        JsonArray a => a.All(Finite),
        JsonValue v => v.GetValueKind() != JsonValueKind.String || v.GetValue<string>() is not ("NaN" or "Infinity" or "-Infinity"),
        _ => true,
    };

    /// <summary>One mutant built: "invalid" (validate refused it: fine), "ok", "errors" (built with errors: fine),
    /// or "crash" with what broke. The caller enforces the time and memory caps.</summary>
    public static (string Outcome, string Detail) Check(JsonObject mutant, bool useLimits)
    {
        Design d;
        try
        {
            d = Design.Parse(mutant.ToJsonString());
        }
        catch (System.Text.Json.JsonException e)
        {
            return ("invalid", e.Message);
        }
        var errs = ShipDesign.Validate(d, useLimits);
        if (errs.Count > 0)
            return ("invalid", errs[0]);
        try
        {
            var ship = ShipDesign.Build(d);
            var json = ShipgenJson.Default;
            var report = JsonSerializer.SerializeToNode(ship.Report, json.Report)!.AsObject();
            report.Remove("inputs");
            if (!Finite(report))
                return ("crash", "a NaN or infinity in the report");
            if (!Finite(JsonSerializer.SerializeToNode(ship.Hitboxes, json.Hitboxes)) ||
                !Finite(JsonSerializer.SerializeToNode(ship.Render.Columns, json.ListHeightColumn)))
                return ("crash", "a NaN or infinity in the hitboxes or height columns");
            return ship.Report.Valid ? ("ok", "") : ("errors", ship.Report.Errors[0]);
        }
        catch (Exception e)
        {
            return ("crash", $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }
}
