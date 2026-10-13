using System.Text.Json;
using Fleetwright.Shipgen;

namespace Fleetwright.Designer;

/// <summary>Designs made from others by the designer's knob steps, without the designer (docs/battle/plan.md step
/// 2.7a): the scenarios' classes beyond the shipped designs (a Jutland's ~55, as Orion, King George V and Iron Duke
/// were steps from one another). A step is what <c>-steps=</c> takes and the + and - buttons do, in metric:
/// "speed:-2" (two steps), "main0_cal:+1", "deck0_extent:full", "distiller:off"; or "speed=21" for a number's value.
/// The knobs live in the exe, so variants are written once, as design files, by the <c>-variants=</c> dev flag, and
/// read like any design after.</summary>
public static class DesignVariants
{
    /// <summary>The design with the steps applied in order; a step that names no knob of the design, or no value it
    /// takes, is reported and skipped.</summary>
    public static Design Apply(Design d, IEnumerable<string> steps, List<string> errors)
    {
        foreach (var st in steps)
        {
            bool absolute = st.Contains('=');
            var parts = st.Split(absolute ? '=' : ':');
            var knob = parts.Length == 2 ? Knobs.All(d).FirstOrDefault(k => k.Id == parts[0]) : null;
            switch (knob)
            {
                case NumberKnob k when absolute && double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out double v):
                    d = k.Apply(d, v);
                    break;
                case NumberKnob k when !absolute && int.TryParse(parts[1], out int n):
                    d = k.Apply(d, StepTarget(k, d, n));
                    break;
                case ChoiceKnob c when c.Values.Contains(parts[1]):
                    d = c.Set(d, parts[1]);
                    break;
                case ToggleKnob t when parts[1] is "on" or "off":
                    d = t.Set(d, parts[1] == "on");
                    break;
                default:
                    errors.Add($"no step '{st}' (knob:steps, knob=value, knob:choice or knob:on/off, e.g. speed:-2)");
                    break;
            }
        }
        return d;
    }

    /// <summary>Where n steps from a knob's value land on the metric grid: the designer's rule (DesignerScene.StepTarget),
    /// Auto read from the built ship.</summary>
    static double StepTarget(NumberKnob k, Design d, int n)
    {
        double step = k.Step(UnitSystem.Metric);
        double? value = k.Get(d);
        if (value == null && k.Auto != null)
            value = k.Auto(d, ShipDesign.Build(d));
        double v = value ?? k.Range(d).Lo;
        double on = n > 0 ? Math.Floor(v / step + 1e-9) : Math.Ceiling(v / step - 1e-9);
        return (on + n) * step;
    }

    /// <summary>The <c>-variants=SPEC.json</c> dev flag: writes each variant of a spec as a design, validated and built
    /// (its weight reported), into the spec's "out" folder (relative to the spec). The spec:
    /// <code>{ "out": "../../Content/Scenarios/Designs", "base_folder": "../../shipgen/designs",
    ///   "variants": [ { "id": "orion", "name": "HMS Orion-like", "base": "super_dreadnought", "navy": "portsmouth", "steps": ["speed:-1"] } ] }</code>
    /// A variant's base is a design in the base folder or an earlier variant. Returns the number that failed.</summary>
    public static int Write(string specPath)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(specPath))!;
        using var spec = JsonDocument.Parse(File.ReadAllText(specPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = spec.RootElement;
        string outDir = Path.GetFullPath(Path.Combine(dir, root.GetProperty("out").GetString()!));
        string baseDir = Path.GetFullPath(Path.Combine(dir, root.GetProperty("base_folder").GetString()!));
        Directory.CreateDirectory(outDir);
        var made = new Dictionary<string, Design>(StringComparer.Ordinal);
        int failed = 0;
        foreach (var v in root.GetProperty("variants").EnumerateArray())
        {
            string id = v.GetProperty("id").GetString()!;
            string from = v.GetProperty("base").GetString()!;
            var errors = new List<string>();
            var d = made.TryGetValue(from, out var m) ? m : Design.Load(Path.Combine(baseDir, from + ".json"));
            var steps = v.TryGetProperty("steps", out var s) ? s.EnumerateArray().Select(x => x.GetString()!).ToArray() : [];
            d = Apply(d, steps, errors) with
            {
                Id = id,
                Name = v.TryGetProperty("name", out var n) ? n.GetString() : d.Name,
                Type = v.TryGetProperty("type", out var t) ? t.GetString() : d.Type,
            };
            if (v.TryGetProperty("navy", out var navy) | v.TryGetProperty("era", out var era))
                d = d with
                {
                    Look = (d.Look ?? new LookInput()) with
                    {
                        Navy = navy.ValueKind == JsonValueKind.String ? navy.GetString() : d.Look?.Navy,
                        Era = era.ValueKind == JsonValueKind.String ? era.GetString() : d.Look?.Era,
                    },
                };
            errors.AddRange(ShipDesign.Validate(d, limits: false));
            string weight = "";
            if (errors.Count == 0)
            {
                try
                {
                    var ship = ShipDesign.Build(d);
                    weight = $"{ship.Report.Results.StandardDisplacementT} t, {ship.Report.Results.LengthM:F0} m";
                }
                catch (Exception x)
                {
                    errors.Add($"didn't build: {x.Message}");
                }
            }
            if (errors.Count > 0)
            {
                failed++;
                Console.Error.WriteLine($"{id} (from {from}): {string.Join("; ", errors)}");
                continue;
            }
            made[id] = d;
            File.WriteAllText(Path.Combine(outDir, id + ".json"), d.ToJson(indented: true));
            Console.WriteLine($"{id,-22} from {from,-20} {weight,16}  {string.Join(" ", steps)}");
        }
        return failed;
    }
}
