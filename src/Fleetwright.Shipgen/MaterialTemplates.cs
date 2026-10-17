namespace Fleetwright.Shipgen;

/// <summary>The materials catalogue (docs/battle/shells-armour-damage.md P0, P1b): the blocks the parts' material
/// names stand for, the one source of `Content/Templates/materials.json` and docs/shipgen/material-templates.md
/// (`shipgen templates` writes both). A design copies the ones its parts name into its `materials` (`shipgen upgrade
/// --materials` refreshes them after a change here); a name the design doesn't carry takes the catalogue's, and a name
/// in neither takes steel's, with a warning.
/// <list type="bullet">
/// <item>q, q_scale, the face and the elongation: research/simulation/armor/00-overview.md §4.2, the reconciled table
/// (armor_pen_ref.py MATERIALS; its id in each note): q against an 8" capped shell on Thompson's scale, WWII STS 1;</item>
/// <item>density: armor/03 §7 (the steels 7.85, wrought iron 7.7, compound 7.75, oak and teak 0.65-0.75), aluminium
/// alloy 2.7; every plate is weighed with it (P1c, <see cref="Density"/>).</item>
/// </list>
/// A name is a plate, never a period: where the designs used one name for two plates (British WWI KC and WWII CA, the
/// Terni of 1912 and of 1937), each has its own. Group: "structure" for the hull plating's and superstructure's picker,
/// "armour" for the rest; the armour picker offers them all.</summary>
public static class MaterialTemplates
{
    public sealed record Entry(string Name, string Group, MaterialInput Value, string Note);

    const double In = 25.4;

    /// <summary>Where a plate's back gives, by its class (damage/14 §2.1 [EST], armor/00 §1): spall from u = V/V_L of
    /// spall_u, holed by a breaking shell from holed_u.</summary>
    static readonly BackInput Ductile = new() { SpallU = 0.90, HoledU = 1.00 }, Hard = new() { SpallU = 0.85, HoledU = 0.94 },
        Brittle = new() { SpallU = 0.75, HoledU = 0.95 };

    /// <summary>The fragments' classes (THOR's constants by class, E1).</summary>
    public static readonly string[] FragmentClasses = ["mild", "ductile", "fh", "brittle"];

    static Entry M(string name, string group, double density, double q, string note, double? elongation = null) =>
        new(name, group, new MaterialInput
        {
            DensityTPerM3 = density, Q = q, ElongationPct = elongation, Back = Ductile, Fragments = group == "armour" ? "ductile" : "mild",
        }, note);

    /// <summary>Face-hardened: q at 8", k past it, the face's share and the thinnest plate made so (inches).</summary>
    static Entry Fh(string name, double q, double k, double face, double minIn, string note, double density = 7.85, bool? shatters = null,
        BackInput? back = null) =>
        new(name, "armour", new MaterialInput
        {
            DensityTPerM3 = density, Q = q, QScale = k, FaceHardened = new FaceInput { Share = face, MinMm = Math.Round(minIn * In, 1), Shatters = shatters },
            Back = back ?? Hard, Fragments = back == Brittle ? "brittle" : "fh",
        }, note);

    /// <summary>The catalogue: the structural materials, then the armour, roughly by its age.</summary>
    public static readonly IReadOnlyList<Entry> All =
    [
        M("steel", "structure", 7.85, 0.70, "Shipbuilding (mild) steel, the plating's default name: Okun's backing ladder 0.7 (MS)."),
        M("mild steel", "structure", 7.85, 0.70, "Mild steel: hull plate, and the splinter decks of the 1890s on. Okun's ladder 0.7 (MS)."),
        M("high-tensile steel", "structure", 7.85, 0.80, "HT and Krupp low-nickel structural steel, the laminated decks of 1900-1925: Okun's ladder 0.8 (HT)."),
        M("wrought iron", "structure", 7.7, 0.53, "The ironclads' plate and hulls (s.g. 7.6-7.9). Spezia 1876 ~0.50, Tresidder/Thompson 0.56, Okun 0.6 (WI)."),
        M("wood", "structure", 0.7, 0.01, "Planking and wooden hulls (oak and teak 0.65-0.75). Okun counts teak at 1/100 of its thickness."),
        M("cement", "structure", 2.3, 0.04, "Cement and concrete, armour backing (US Class A's 2\"). Okun counts cement at 1/25 of its thickness."),
        M("aluminium alloy", "structure", 2.7, 0.33, "Light alloy deckhouses. [EST] about steel's protection per weight (2.7/7.85); no research row."),
        Fh("compound", 0.72, 0, 0.30, 0, "Steel face welded to a wrought-iron back, a third and two thirds (1877-1891). Annapolis < 0.75-0.80; shatters chilled shot only (CMP): no bare steel shell. Its back is brittle.", 7.75, false, Brittle),
        M("nickel steel", "armour", 7.85, 0.85, "Homogeneous nickel steel (1889-1896). Annapolis > 0.77-0.83, Johnson's limit 0.92 (NIS)."),
        Fh("Harvey nickel steel", 0.82, 0, 0.08, 3.2, "Harveyized: carburized nickel steel, a thin hard face (1891-1899). NO 1937's limit 0.79 (HRV)."),
        Fh("Krupp cemented", 0.88, 0.05, 0.34, 3.2, "KC a/A and its licensees (1894-1918): decremental face-hardened Ni-Cr. Capped trials 0.87-0.97, Okun 0.83 of WWII FH (KCA)."),
        Fh("Krupp cemented (KC n/A)", 1.01, 0.03, 0.41, 3.9, "KC neuer Art, the German plate of 1928-1945. NPG 1946-47 graded it 83-113% (KCNA)."),
        Fh("cemented armour", 0.85, 0, 0.30, 4.0, "British (and licensees') KC of 1900-1932: \"inferior to KC a/A\" (Okun) (BKC)."),
        Fh("cemented armour (CA)", 0.99, -0.05, 0.30, 4.0, "British Cemented Armour of 1933-1945: a thin face on a tough back, slightly better against bigger shells (CA)."),
        Fh("Vickers cemented", 0.85, 0, 0.30, 4.0, "VC, Vickers' KC (Japan's of the Kongo and Nagato era): as British WWI KC (BKC)."),
        Fh("Terni cemented", 0.85, 0, 0.30, 4.0, "Italian KC before 1918, as British WWI KC (BKC)."),
        Fh("Terni cemented (1930s)", 1.03, -0.08, 0.45, 4.0, "Italian Terni cemented of 1929-1943: the best of the five at 15-18\" in Okun's FACEHARD tables; little test data (TKC)."),
        Fh("Witkowitz cemented", 0.97, 0, 0.34, 3.2, "Witkowitz KC (Austria-Hungary, 1905-1918): \"~95% of WWII FH\" (Okun) (WKC)."),
        Fh("French cemented (1930s)", 0.95, 0.05, 0.35, 4.0, "French face-hardened plate of 1932-1945: Okun assumed it equal to US; German tests found it inferior [UNCERTAIN] (FRFH)."),
        Fh("Class A armour", 0.88, 0.05, 0.34, 3.2, "US face-hardened plate before the thick chill (Pennsylvania, Colorado: KC-type). [EST] as KC a/A; no research row."),
        Fh("Class A", 1.03, 0.17, 0.55, 5.0, "US Class A of 1933-1945, the thick chill: FACEHARD (NL) 1.03 at 8\", 0.92 at 16\"; OS 751 agrees (USA)."),
        Fh("Vickers hardened", 0.89, 0, 0.35, 11.0, "Japanese VH of 1937-1945, only over 11\": FACEHARD NL 0.89-0.90; Shinano's 26\" plate ~0.8 (VH)."),
        M("Krupp non-cemented", "armour", 7.85, 0.92, "KNC, Krupp-type homogeneous Ni-Cr (1894-1925): Okun 0.95 homogeneous to WWI (KNC).", 22),
        M("special treatment steel", "armour", 7.85, 0.95, "US Class B and STS of 1910-1932 (CB1).", 23),
        M("Class B", "armour", 7.85, 1.00, "US Class B of the 1930s and WWII: the standard (STS).", 25),
        M("STS", "armour", 7.85, 1.00, "US special treatment steel of 1930-1945, the scale's reference: NPG Class B 91-106% (STS).", 25),
        M("non-cemented armour", "armour", 7.85, 1.00, "British NCA of 1926-1945 (NCA).", 25),
        M("Wotan hart", "armour", 7.85, 1.00, "Wotan hart (1925-1945), 18% elongation: 0.85 against a 16\" (WH).", 18),
        M("Wotan", "armour", 7.85, 0.95, "Wotan weich (1925-1945), the softer deck plate (WW).", 22),
        M("Molybdenum steel", "armour", 7.85, 0.97, "Japanese homogeneous NVNC/CNC (1926-1945): Okun's M79 Q 0.97.", 23),
        M("special steel", "armour", 7.85, 0.97, "French homogeneous deck plate of the 1930s. [EST] as Japanese NVNC; no research row.", 23),
    ];

    static readonly Dictionary<string, Entry> byName = All.ToDictionary(e => e.Name, StringComparer.Ordinal);

    /// <summary>The catalogue's block of a name, or null.</summary>
    public static MaterialInput? Get(string name) => byName.GetValueOrDefault(name)?.Value;

    /// <summary>A material's density, t/m³ (P1c): the design's block's, else the catalogue's, else steel's; steel's for a
    /// part with no material named. What every plate of armour and structure is weighed with.</summary>
    public static double Density(Design design, string? name) =>
        string.IsNullOrEmpty(name) ? Weight.Steel
            : design.Materials?.GetValueOrDefault(name)?.DensityTPerM3 ?? Get(name)?.DensityTPerM3 ?? Weight.Steel;

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
        var face = given?.FaceHardened is { } f
            ? new FaceInput { Share = f.Share ?? basis.FaceHardened?.Share, MinMm = f.MinMm ?? basis.FaceHardened?.MinMm, Shatters = f.Shatters ?? basis.FaceHardened?.Shatters }
            : basis.FaceHardened;
        return new MaterialInput
        {
            DensityTPerM3 = given?.DensityTPerM3 ?? basis.DensityTPerM3, Q = given?.Q ?? basis.Q, QScale = given?.QScale ?? basis.QScale,
            FaceHardened = face, ElongationPct = given?.ElongationPct ?? basis.ElongationPct,
            Back = given?.Back is { } b ? new BackInput { SpallU = b.SpallU ?? basis.Back?.SpallU, HoledU = b.HoledU ?? basis.Back?.HoledU } : basis.Back,
            Fragments = given?.Fragments ?? basis.Fragments,
        };
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
            foreach (var part in (ReadOnlySpan<string>)["belt", "upper", "fore", "aft"])
                Add(Armour.Backing(design, part)?.Material);
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

    static readonly string[] Fields = ["density_t_per_m3", "q", "q_scale", "face_hardened", "elongation_pct", "back", "fragments"];

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
            foreach (var (k, v) in new[] { ("density_t_per_m3", x.DensityTPerM3), ("q", x.Q), ("elongation_pct", x.ElongationPct) })
                if (v is double d && !(d > 0 && double.IsFinite(d)))
                    errs.Add($"{at}.{k}: a number, more than 0");
            if (x.QScale is double qs && !double.IsFinite(qs))
                errs.Add($"{at}.q_scale: a number");
            if (x.Fragments is { } fc && !FragmentClasses.Contains(fc))
                errs.Add($"{at}.fragments = {Style.Quote(fc)}: use {string.Join(", ", FragmentClasses)}");
            if (x.FaceHardened is { } f)
            {
                foreach (var k in f.Extra.KeysOrEmpty())
                    errs.Add($"{at}.face_hardened.{k}: not a face field (share, min_mm, shatters)");
                if (f.Share is < 0 or > 1)
                    errs.Add($"{at}.face_hardened.share: a share, 0 to 1");
                if (f.MinMm is double m && !(m >= 0 && double.IsFinite(m)))
                    errs.Add($"{at}.face_hardened.min_mm: 0 or more");
            }
            if (x.Back is { } b)
            {
                foreach (var k in b.Extra.KeysOrEmpty())
                    errs.Add($"{at}.back.{k}: not a back field (spall_u, holed_u)");
                if (b.SpallU is <= 0 or > 1 || b.HoledU is <= 0 or > 1)
                    errs.Add($"{at}.back: spall_u and holed_u are shares of the Navy limit, more than 0 up to 1");
                else if (b.SpallU is double su && b.HoledU is double hu && su > hu)
                    errs.Add($"{at}.back: spall_u ({su}) comes before holed_u ({hu})");
            }
        }
        return errs;
    }
}
