using System.Diagnostics;

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
        var doc = (PyDict)PyJson.Load(Path.Combine(root, "golden", "cases.json"))!;
        return doc.L("cases").Cast<PyDict>().Select(c => new GoldenCase(
            c.S("case"), Path.Combine(root, c.S("design").Replace('/', Path.DirectorySeparatorChar)),
            c.S("source", null), Py.Truthy(c["limits"]), c.L("changes").Cast<string>().ToList())).ToList();
    }

    public static string GoldenFile(string root, string name) => Path.Combine(root, "golden", "design", name + ".json.gz");

    /// <summary>Python's exception names for ours, as the capture records them.</summary>
    public static string PyName(Exception e) => e switch
    {
        PyKeyError => "KeyError",
        PyValueError => "ValueError",
        PyTypeError => "TypeError",
        DivideByZeroException => "ZeroDivisionError",
        OverflowException => "OverflowError",
        _ => e.GetType().Name,
    };

    static PyDict Raised(Exception e) =>
        PyDict.Of(("raised", PyDict.Of(("type", PyName(e)), ("message", e.Message))));

    static object? Try(Func<object?> f)
    {
        try
        {
            return PyJson.Plain(f());
        }
        catch (NotImplementedException)
        {
            throw;
        }
        catch (Exception e)
        {
            return Raised(e);
        }
    }

    /// <summary>golden.py's capture_case: validation strings, the build (when validate(limits=False) passes), the
    /// hint check and the build time.</summary>
    public static PyDict Capture(Design design)
    {
        var rec = new PyDict();
        rec["validate_limits"] = Try(() => ShipDesign.Validate(design, limits: true));
        rec["validate_no_limits"] = Try(() => ShipDesign.Validate(design, limits: false));
        rec["looks_validate"] = Try(() => Looks.Validate(design));
        if (rec["validate_no_limits"] is List<object?> errs && errs.Count == 0)
        {
            var sw = Stopwatch.StartNew();
            object? build = Try(() => ShipDesign.Build(design));
            rec["build_s"] = Math.Round(sw.Elapsed.TotalSeconds, 3);
            rec["build"] = build;
            if (build is PyDict b && !b.Has("raised"))
            {
                object? L = b.D("report").D("results")["length_m"];
                object? hinted = Try(() => ShipDesign.Build(design, Py.ToDouble(L)));
                rec["hint"] = hinted is PyDict h && h.Has("raised")
                    ? PyDict.Merge(PyDict.Of(("length_m", L)), h)
                    : PyDict.Of(("length_m", L), ("equal", GoldenDiff.Compare(build, hinted, 1, "$").Count == 0
                                                            && ExactlyEqual(build, hinted)));
            }
        }
        return rec;
    }

    /// <summary>Python's == on JSON trees (exact floats), for the hint check.</summary>
    static bool ExactlyEqual(object? a, object? b) => Py.Eq(a, b);

    /// <summary>Rewrite the case's golden record from ours when the two differ by the golden rules (after a deliberate
    /// change to the output). Returns whether it was rewritten.</summary>
    public static bool Update(string root, GoldenCase c)
    {
        var golden = (PyDict)PyJson.Load(GoldenFile(root, c.Name))!;
        var ours = Capture(Design.Load(c.DesignPath));
        golden.Remove("build_s");
        ours.Remove("build_s");
        if (GoldenDiff.Compare(golden, ours, 1).Count == 0)
            return false;
        PyJson.Save(GoldenFile(root, c.Name), ours, 1);
        return true;
    }

    /// <summary>The differences between the golden record for a case and ours (build_s left out).</summary>
    public static List<Difference> Check(string root, GoldenCase c, int max = 20)
    {
        var golden = (PyDict)PyJson.Load(GoldenFile(root, c.Name))!;
        var ours = Capture(Design.Load(c.DesignPath));
        golden.Remove("build_s");
        ours.Remove("build_s");
        return GoldenDiff.Compare(golden, ours, max);
    }
}
