namespace Fleetwright.Shipgen;

/// <summary>The materials catalogue (docs/battle/shells-armour-damage.md P0): the blocks the parts' material names
/// stand for, the one source of `Content/Templates/materials.json` and docs/shipgen/material-templates.md (`shipgen
/// templates` writes both). A design copies the ones its parts name into its `materials`; a name the design doesn't
/// carry takes the catalogue's, and a name in neither takes steel's, with a warning.
/// <list type="bullet">
/// <item>q: the game's armour qualities as they were in settings.json (P0 is parity), the names it didn't list at its
/// 0.6 for the others (marked); P1b puts armor/00 §4.2's reconciled values in;</item>
/// <item>density: armor/03 §7 (the steels 7.85, wrought iron 7.7, compound 7.75, oak and teak 0.65-0.75), aluminium
/// alloy 2.7; the weights read it from P1c.</item>
/// </list>
/// Group: "structure" for the hull plating's and superstructure's picker, "armour" for the rest; the armour picker
/// offers them all.</summary>
public static class MaterialTemplates
{
    public sealed record Entry(string Name, string Group, MaterialInput Value, string Note);

    const string Unlisted = "q 0.6: not in the old quality table, so its catch-all (P1b sets it).";

    static Entry M(string name, string group, double density, double q, string note) =>
        new(name, group, new MaterialInput { DensityTPerM3 = density, Q = q }, note);

    /// <summary>The catalogue: the structural materials, then the armour, roughly by its age.</summary>
    public static readonly IReadOnlyList<Entry> All =
    [
        M("steel", "structure", 7.85, 0.55, "Shipbuilding steel, the plating's default name."),
        M("mild steel", "structure", 7.85, 0.5, "Mild (carbon) steel: hull plate, and the splinter decks of the 1890s on."),
        M("high-tensile steel", "structure", 7.85, 0.7, "HT and Ducol-type manganese steels: strength plating and protective decks."),
        M("wrought iron", "structure", 7.7, 0.45, "The ironclads' plate and hulls (s.g. 7.6-7.9)."),
        M("wood", "structure", 0.7, 0.05, "Planking and wooden hulls (oak and teak 0.65-0.75)."),
        M("aluminium alloy", "structure", 2.7, 0.25, "Light alloy deckhouses."),
        M("compound", "armour", 7.75, 0.6, "Steel face welded to a wrought-iron back, about a third and two thirds (1877-1890s)."),
        M("nickel steel", "armour", 7.85, 0.75, "Homogeneous nickel steel (1889 on)."),
        M("Harvey nickel steel", "armour", 7.85, 0.8, "Harveyized: carburized nickel steel, face-hardened (1891 on)."),
        M("Krupp cemented", "armour", 7.85, 1, "KC: the face-hardened Ni-Cr plate of 1894 on."),
        M("Krupp cemented (KC n/A)", "armour", 7.85, 1, "KC neuer Art, the German plate of the 1930s."),
        M("cemented armour", "armour", 7.85, 1, "British cemented, Krupp's process (KC under licence)."),
        M("Vickers cemented", "armour", 7.85, 1, "VC: British face-hardened plate."),
        M("Terni cemented", "armour", 7.85, 1, "Italian face-hardened plate."),
        M("Witkowitz cemented", "armour", 7.85, 1, "Austro-Hungarian face-hardened plate."),
        M("Class A", "armour", 7.85, 1, "US face-hardened plate."),
        M("Class A armour", "armour", 7.85, 1, "US face-hardened plate (the older designs' name)."),
        M("non-cemented armour", "armour", 7.85, 0.9, "British homogeneous armour (NC)."),
        M("Krupp non-cemented", "armour", 7.85, 0.6, "KNC: Krupp's homogeneous plate. " + Unlisted),
        M("Vickers hardened", "armour", 7.85, 0.9, "VH: British homogeneous hardened plate."),
        M("Class B", "armour", 7.85, 1, "US homogeneous plate."),
        M("STS", "armour", 7.85, 1, "Special treatment steel: the US homogeneous plate of the 1930s on."),
        M("special treatment steel", "armour", 7.85, 0.6, "STS by its long name. " + Unlisted),
        M("special steel", "armour", 7.85, 0.95, "Homogeneous Ni-Cr deck plate."),
        M("Molybdenum steel", "armour", 7.85, 0.6, "Molybdenum-alloyed homogeneous plate (Japan, Italy). " + Unlisted),
        M("Wotan", "armour", 7.85, 0.6, "Wotan weich, Krupp's homogeneous deck plate of the 1930s. " + Unlisted),
        M("Wotan hart", "armour", 7.85, 0.6, "Wotan hart, the hard homogeneous deck and bulkhead plate. " + Unlisted),
    ];

    static readonly Dictionary<string, Entry> byName = All.ToDictionary(e => e.Name, StringComparer.Ordinal);

    /// <summary>The catalogue's block of a name, or null.</summary>
    public static MaterialInput? Get(string name) => byName.GetValueOrDefault(name)?.Value;

    /// <summary>What a name nobody knows takes.</summary>
    public static MaterialInput Steel => byName["steel"].Value;

    /// <summary>The names in a group ("armour": all of them).</summary>
    public static IEnumerable<string> Names(string group) => All.Where(e => group == "armour" || e.Group == group).Select(e => e.Name);

    /// <summary>A name's block resolved: the design's, its missing fields the catalogue's of the same name (else
    /// steel's). Null when the name is in neither the design nor the catalogue.</summary>
    public static MaterialInput? Resolve(Design design, string name)
    {
        var known = Get(name);
        var given = design.Materials?.GetValueOrDefault(name);
        if (given is null && known is null)
            return null;
        var basis = known ?? Steel;
        return new MaterialInput { DensityTPerM3 = given?.DensityTPerM3 ?? basis.DensityTPerM3, Q = given?.Q ?? basis.Q };
    }

    /// <summary>Every material name the design's parts give (the plating, the superstructure, the batteries, the armour
    /// scheme's map and the parts' own), in ordinal order.</summary>
    public static SortedSet<string> Used(Design design)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        void Add(string? n)
        {
            if (!string.IsNullOrEmpty(n))
                names.Add(n);
        }
        Add(design.Hull?.Plating?.Material);
        Add(design.Superstructure?.Material);
        foreach (var b in design.MainBatteries.Concat(design.Secondary ?? []))
            Add(b.Material);
        if (design.Armour is { } a)
        {
            foreach (var n in a.Materials?.Values ?? Enumerable.Empty<string>())
                Add(n);
            foreach (var d in a.Decks ?? [])
                Add(d.Material);
            Add(a.UpperBelt?.Material);
            Add(a.EndBelts?.Of("fore")?.Material);
            Add(a.EndBelts?.Of("aft")?.Material);
            Add(a.SteeringBox?.Material);
            Add(a.SteeringBox?.DeckMaterial);
        }
        return names;
    }

    /// <summary>The resolved block of every name the parts give, for the report; a name in neither the design nor the
    /// catalogue takes steel's and a warning.</summary>
    public static Dictionary<string, MaterialInput> Report(Design design, List<string> warnings)
    {
        var table = new Dictionary<string, MaterialInput>(StringComparer.Ordinal);
        foreach (var n in Used(design))
        {
            var m = Resolve(design, n);
            if (m is null)
            {
                warnings.Add($"material {Style.Quote(n)}: not in the design's materials or the catalogue, so it's taken as steel");
                m = Steel;
            }
            table[n] = m;
        }
        return table;
    }

    static readonly string[] Fields = ["density_t_per_m3", "q"];

    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        foreach (var (name, x) in design.Materials ?? [])
        {
            string at = $"materials.\"{name}\"";
            if (name.Length == 0)
                errs.Add("materials: a material needs a name");
            if (x is null)
            {
                errs.Add($"{at}: give its numbers");
                continue;
            }
            foreach (var k in x.Extra.KeysOrEmpty())
                errs.Add($"{at}.{k}: not a material field ({string.Join(", ", Fields)})");
            foreach (var (k, v) in new[] { ("density_t_per_m3", x.DensityTPerM3), ("q", x.Q) })
                if (v is double d && !(d > 0 && double.IsFinite(d)))
                    errs.Add($"{at}.{k}: a number, more than 0");
        }
        return errs;
    }
}
