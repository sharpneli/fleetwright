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
    static readonly string[] LABELS = ["id", "name", "look", "material", "materials", "type"];   // free text, not choices

    /// <summary>A step on a path into a design: a dict key or a list index.</summary>
    public readonly record struct Step(string? Key, int Index)
    {
        public override string ToString() => Key ?? Index.ToString();
    }

    public sealed record Change(string Path, string Was, string Now)
    {
        public override string ToString() => $"{Path} {Was} -> {Now}";
    }

    static IEnumerable<(Step S, object? V)> Items(object? c) => c switch
    {
        PyDict d => d.Select(kv => (new Step(kv.Key, 0), kv.Value)),
        List<object?> l => l.Select((v, i) => (new Step(null, i), v)),
        _ => [],
    };

    static bool Label(Step s) => s.Key != null && LABELS.Contains(s.Key);

    /// <summary>Every leaf passing keep, list entries by index, labels left out.</summary>
    static List<(Step[] Path, object? V)> Leaves(object? d, Func<object?, bool> keep, Step[]? path = null)
    {
        path ??= [];
        var out_ = new List<(Step[], object?)>();
        foreach (var (s, v) in Items(d))
        {
            if (Label(s))
                continue;
            if (v is PyDict or List<object?>)
                out_.AddRange(Leaves(v, keep, [.. path, s]));
            else if (keep(v))
                out_.Add(([.. path, s], v));
        }
        return out_;
    }

    static List<(Step[] Path, object C)> Containers(object? d, Step[]? path = null)
    {
        path ??= [];
        var out_ = new List<(Step[], object)>();
        foreach (var (s, v) in Items(d))
            if (!Label(s) && v is PyDict or List<object?>)
            {
                out_.Add(([.. path, s], v!));
                out_.AddRange(Containers(v, [.. path, s]));
            }
        return out_;
    }

    static string Key(Step[] path) => path.Reverse().First(s => s.Key != null).Key!;

    static string Show(Step[] path) => string.Join(".", path.Select(s => s.ToString()));

    static void SetAt(object d, Step[] path, object? v)
    {
        foreach (var s in path[..^1])
            d = s.Key != null ? ((PyDict)d)[s.Key]! : ((List<object?>)d)[s.Index]!;
        var last = path[^1];
        if (last.Key != null)
            ((PyDict)d)[last.Key] = v;
        else
            ((List<object?>)d)[last.Index] = v;
    }

    /// <summary>{key: the string values the designs give it}: the options a mutant may swap in.</summary>
    public static Dictionary<string, List<string>> CorpusChoices(IEnumerable<PyDict> designs)
    {
        var seen = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var d in designs)
            foreach (var (path, v) in Leaves(d, v => v is string))
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
                return (Py.ToDouble(l.Lo), Py.ToDouble(l.Hi));
        return null;
    }

    static Change? MutateNumber(PyDict d, Random rng, List<Limit> limits)
    {
        var nums = Leaves(d, v => v is long or double);
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
            nv = Py.ToDouble(v) * Math.Exp(Math.Log(0.25) + (Math.Log(4) - Math.Log(0.25)) * rng.NextDouble());
        object nvo = v is long ? (long)Math.Round(nv) : Math.Round(nv, 3);
        SetAt(d, path, nvo);
        return new Change(Show(path), Py.Repr(v), Py.Repr(nvo));
    }

    static Change? MutateChoice(PyDict d, Random rng, Dictionary<string, List<string>> choices)
    {
        var opts = Leaves(d, v => v is bool)
            .Concat(Leaves(d, v => v is string).Where(p => choices.TryGetValue(Key(p.Path), out var c) && c.Count > 1)).ToList();
        if (opts.Count == 0)
            return null;
        var (path, v) = opts[rng.Next(opts.Count)];
        object nv;
        if (v is bool b)
            nv = !b;
        else
        {
            var others = choices[Key(path)].Where(c => c != (string)v!).ToList();
            nv = others[rng.Next(others.Count)];
        }
        SetAt(d, path, nv);
        return new Change(Show(path), Py.Repr(v), Py.Repr(nv));
    }

    static Change? MutateStructure(PyDict d, Random rng)
    {
        var conts = Containers(d).Where(c => c.C is PyDict { Count: > 0 } or List<object?> { Count: > 0 }).ToList();
        if (conts.Count == 0)
            return null;
        var (path, c) = conts[rng.Next(conts.Count)];
        if (c is List<object?> l)
        {
            int i = rng.Next(l.Count);
            if (l[0] is PyDict && rng.NextDouble() < 0.5)
            {
                l.Insert(i, PyJson.Parse(PyJson.Dumps(l[i], null)));
                return new Change(Show(path), $"{l.Count - 1} entries", $"{l.Count} (entry {i} twice)");
            }
            l.RemoveAt(i);
            return new Change(Show(path), $"{l.Count + 1} entries", $"{l.Count} (entry {i} dropped)");
        }
        var dict = (PyDict)c;
        var keys = dict.Keys.ToList();
        var k = keys[rng.Next(keys.Count)];
        var was = PyJson.Dumps(dict[k], null);
        dict.Remove(k);
        return new Change(Show([.. path, new Step(k, 0)]), was.Length > 40 ? was[..40] : was, "(deleted)");
    }

    /// <summary>A mutant of design: 1-4 changes as mode asks ("all": numbers 60%, choices 25%, structure 15%).</summary>
    public static (PyDict Design, List<Change> Changes) Mutate(PyDict design, Random rng, bool useLimits,
        Dictionary<string, List<string>> choices, string mode = "all")
    {
        var d = (PyDict)PyJson.Parse(PyJson.Dumps(design, null))!;
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
                limits = useLimits ? Styles.Get(d.Get("style", "warship") as string ?? "").Limits() : [];
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

    /// <summary>Is every number in a JSON-like value finite?</summary>
    public static bool Finite(object? x) => x switch
    {
        double d => double.IsFinite(d),
        PyDict d => d.Values.All(Finite),
        System.Collections.IEnumerable e and not string => e.Cast<object?>().All(Finite),
        Pt p => double.IsFinite(p.X) && double.IsFinite(p.Y),
        IPyValue v => Finite(v.ToPy()),
        _ => true,
    };

    /// <summary>One mutant built: "invalid" (validate refused it: fine), "ok", "errors" (built with errors: fine),
    /// or "crash" with what broke. The caller enforces the time and memory caps.</summary>
    public static (string Outcome, string Detail) Check(PyDict mutant, bool useLimits)
    {
        Design d;
        try
        {
            d = Design.Parse(PyJson.Dumps(mutant, null));
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
            if (!Finite(PyJson.Plain(ship.D("report"))))
                return ("crash", "a NaN or infinity in the report");
            if (!Finite(PyJson.Plain(ship["hitboxes"])) || !Finite(PyJson.Plain(ship.D("render")["columns"])))
                return ("crash", "a NaN or infinity in the hitboxes or height columns");
            var rep = ship.D("report");
            return Py.Truthy(rep["valid"]) ? ("ok", "") : ("errors", (string)rep.L("errors")[0]!);
        }
        catch (Exception e)
        {
            return ("crash", $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }
}
