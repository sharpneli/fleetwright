using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen;

/// <summary>An input limit: the numbers at the dotted path (a list on the way checks every entry) must lie in
/// lo..hi.</summary>
public sealed record Limit(string[] Path, double Lo, double Hi);

/// <summary>A design style's hooks, with neutral defaults. A style overrides what it needs.</summary>
public abstract class Style
{
    public abstract string Name { get; }

    /// <summary>The style's own limits, merged over CommonLimits (a path in both keeps its place there).</summary>
    protected virtual IEnumerable<Limit> StyleLimits => [];

    public virtual double DefaultBlockCoefficient => 0.55;

    /// <summary>How ShipDesign sizes the hull.</summary>
    public virtual SizeRules Sizing => new((30.0, 1000.0), 100.0, 0.06, 0.36, 10.5, true);

    /// <summary>Hull length range, the widest beam, GM (fraction of beam) and draught (fraction of beam) the beam must
    /// give, the most slender L/B, and whether a displacement hull's minimum length applies.</summary>
    public sealed record SizeRules((double Min, double Max) Length, double BeamMax, double GmFrac, double Tb, double LbMax,
        bool Slender);

    static Limit Lim(double lo, double hi, params string[] path) => new(path, lo, hi);

    /// <summary>Input limits shared by every style: sanity bounds for the generator, not gameplay rules.</summary>
    public static IReadOnlyList<Limit> CommonLimits { get; } = BuildCommon();

    static List<Limit> BuildCommon()
    {
        var l = new List<Limit>
        {
            Lim(0.42, 0.68, "hull", "block_coefficient"),
            Lim(100, 1500, "hull", "construction", "yield_mpa"), Lim(0.8, 1.5, "hull", "construction", "join_factor"),
            Lim(0.5, 2.0, "hull", "construction", "standard"), Lim(0.3, 2.0, "hull", "freeboard"),
            Lim(1, 2, "hull", "raised", "decks"), Lim(0, 200, "hull", "plating", "shell_mm"),
            Lim(0, 300, "hull", "plating", "deck_wood_mm"),
            Lim(8, 42, "speed_kn"), Lim(1000, 25000, "range_nm"),
            Lim(1, 2000, "main", "calibre_mm"), Lim(1, 200, "main", "calibre_length"), Lim(1, 20, "main", "barrels"),
            Lim(0, 40, "main", "fore"), Lim(0, 40, "main", "aft"), Lim(0, 40, "main", "mid"),
            Lim(0, 20, "main", "wing"), Lim(0, 2000, "main", "armour_mm"),
            Lim(1, 2000, "secondary", "calibre_mm"), Lim(1, 200, "secondary", "calibre_length"),
            Lim(1, 20, "secondary", "barrels"), Lim(0, 100, "secondary", "per_side"), Lim(0, 200, "secondary", "count"),
            Lim(0, 1000, "main", "rounds_per_gun"), Lim(0, 1000, "secondary", "rounds_per_gun"),
            Lim(0, 40, "torpedoes", "mounts"), Lim(1, 20, "torpedoes", "tubes"),
            Lim(0, 500, "aa", "heavy"), Lim(0, 500, "aa", "light"),
            Lim(0, 2000, "armour", "belt_mm"),
            Lim(0, 20, "armour", "tds_m"), Lim(0, 2000, "armour", "bulkhead_mm"),
            Lim(0, 2000, "armour", "belt_bottom_mm"), Lim(0, 30, "armour", "belt_depth_m"), Lim(0, 30, "armour", "belt_height_m"),
            Lim(0, 2000, "armour", "upper_belt", "mm"),
            Lim(0, 2000, "armour", "end_belts", "fore", "mm"), Lim(0, 2000, "armour", "end_belts", "fore", "tip_mm"),
            Lim(0, 2000, "armour", "end_belts", "aft", "mm"), Lim(0, 2000, "armour", "end_belts", "aft", "tip_mm"),
        };
        foreach (var e in new[] { "fore", "aft" })
        {
            l.Add(Lim(0, 1, "armour", "end_belts", e, "reach"));
            l.Add(Lim(0, 2000, "armour", "end_belts", e, "bulkhead_mm"));
        }
        foreach (var k in new[] { "mm", "deck_mm", "bulkhead_mm" })
            l.Add(Lim(0, 2000, "armour", "steering_box", k));
        l.Add(Lim(0, 5, "superstructure", "t_per_m2"));
        l.Add(Lim(1, 30, "superstructure", "tower_levels"));
        l.Add(Lim(0, 30, "superstructure", "levels_over_bridge"));
        l.Add(Lim(1, 30, "superstructure", "deckhouse_levels"));
        l.Add(Lim(0, 200, "superstructure", "plating_mm"));
        l.Add(Lim(0, 500, "superstructure", "control_mm"));
        foreach (var b in new[] { "main", "secondary", "aa" })
        {
            l.Add(Lim(0, 100, "fire_control", b, "directors"));
            l.Add(Lim(0, 50, "fire_control", b, "rangefinder_m"));
            l.Add(Lim(0, 2000, "fire_control", b, "armour_mm"));
            l.Add(Lim(0, 500, "fire_control", b, "radar_t"));
            l.Add(Lim(0, 500, "fire_control", b, "computer_t"));
        }
        l.Add(Lim(0, 500, "fire_control", "search_radar_t"));
        l.Add(Lim(0, 60, "funnels"));
        l.Add(Lim(1, 100, "machinery", "tech", "draught", "velocity_m_s"));
        l.Add(Lim(300, 2000, "machinery", "tech", "draught", "gas_temp_k"));
        return l;
    }

    /// <summary>CommonLimits with the style's own merged over them.</summary>
    public List<Limit> Limits()
    {
        var all = CommonLimits.ToList();
        foreach (var s in StyleLimits)
        {
            int i = all.FindIndex(x => x.Path.SequenceEqual(s.Path));
            if (i >= 0)
                all[i] = s;
            else
                all.Add(s);
        }
        return all;
    }

    // numbers the physics divides by or takes as counts: (path, low, low inclusive, high or none)
    static readonly (string[] Path, double Lo, bool Incl, double? Hi)[] Defined =
    [
        (["hull", "block_coefficient"], 0.0, false, 1.0), (["speed_kn"], 0.0, false, null),
        (["main", "barrels"], 1, true, null), (["main", "calibre_mm"], 0.0, false, null),
        (["main", "calibre_length"], 0.0, false, null), (["secondary", "barrels"], 1, true, null),
        (["secondary", "calibre_mm"], 0.0, false, null), (["secondary", "calibre_length"], 0.0, false, null),
        (["main", "rounds_per_gun"], 0, true, null), (["secondary", "rounds_per_gun"], 0, true, null),
    ];

    /// <summary>The objects a dotted path's parent reaches in the design's JSON: a list on the way checks every
    /// entry.</summary>
    static List<JsonObject> Walk(JsonObject design, string[] path)
    {
        var ds = new List<JsonObject> { design };
        foreach (var k in path[..^1])
            ds = ds.SelectMany(d => d[k] switch
            {
                JsonArray a => a.OfType<JsonObject>(),
                JsonObject o => [o],
                _ => [new JsonObject()],
            }).ToList();
        return ds;
    }

    /// <summary>The numbers at a dotted path ("give a number" for anything else there).</summary>
    static IEnumerable<(string Path, JsonNode V)> At(JsonObject design, string[] path) =>
        Walk(design, path).Where(d => d[path[^1]] != null).Select(d => (string.Join('.', path), d[path[^1]]!));

    static bool IsNumber(JsonNode v) => v.GetValueKind() == JsonValueKind.Number;

    /// <summary>Numbers outside the limits.</summary>
    public static List<string> LimitErrors(Design design, IEnumerable<Limit> limits)
    {
        var root = JsonSerializer.SerializeToNode(design, ShipgenJson.Default.Design)!.AsObject();
        var errs = new List<string>();
        foreach (var lim in limits)
            foreach (var (path, v) in At(root, lim.Path))
            {
                if (!IsNumber(v))
                    errs.Add($"{path} = {v.ToJsonString()}: give a number, {lim.Lo}..{lim.Hi}");
                else if (v.GetValue<double>() is var x && !(lim.Lo <= x && x <= lim.Hi))
                    errs.Add($"{path} = {x} is outside {lim.Lo}..{lim.Hi}");
            }
        return errs;
    }

    /// <summary>Missing gun and torpedo data, and numbers outside the range where the physics means anything at
    /// all.</summary>
    public static List<string> UndefinedErrors(Design design)
    {
        var errs = new List<string>();
        void Required(string group, IReadOnlyList<(string Key, bool Given)>[] items)
        {
            for (int i = 0; i < items.Length; i++)
                errs.AddRange(items[i].Where(k => !k.Given).Select(k => $"{group}[{i}].{k.Key} is missing"));
        }
        static (string, bool)[] Gun(BatteryInput b) =>
            [("calibre_mm", b.CalibreMm != null), ("calibre_length", b.CalibreLength != null), ("barrels", b.Barrels != null)];
        Required("main", [.. design.MainBatteries.Select(Gun)]);
        Required("secondary", [.. (design.Secondary ?? []).Select(Gun)]);
        if (design.Torpedoes is { } tp)
            errs.AddRange(new[] { ("mounts", tp.Mounts != null), ("tubes", tp.Tubes != null) }.Where(k => !k.Item2)
                .Select(k => $"torpedoes.{k.Item1} is missing"));
        var root = JsonSerializer.SerializeToNode(design, ShipgenJson.Default.Design)!.AsObject();
        foreach (var (path, lo, incl, hi) in Defined)
            foreach (var (p, v) in At(root, path))
            {
                double x = IsNumber(v) ? v.GetValue<double>() : double.NaN;
                if (!double.IsFinite(x) || (incl ? x < lo : x <= lo) || (hi is double h && x > h))
                    errs.Add($"{p} = {v.ToJsonString()}: must be {(incl ? "at least" : "above")} {lo}" +
                             (hi is double h2 ? $" and at most {h2}" : ""));
            }
        return errs;
    }

    static readonly string[] SuperstructureKeys = ["t_per_m2", "material", "plating_mm", "control_mm", "tower_levels",
        "deckhouse_levels", "aft_control", "levels_over_bridge"];
    public static readonly string[] RaisedAnchors = ["bow", "fore_group", "bridge", "funnels", "aft_control", "aft_group", "stern"];
    static readonly string[] StandsOnChoices = ["deck", "deckhouse"];

    /// <summary>hull.raised: [{"from": an anchor, "to": an anchor, "decks": n}].</summary>
    public static List<string> RaisedErrors(Design design, Style style)
    {
        var rs = design.Hull?.Raised ?? [];
        if (rs.Count > 0 && !style.RaisedHull)
            return [$"hull.raised: the {style.Name} style lays out its own raised decks"];
        var errs = new List<string>();
        for (int k = 0; k < rs.Count; k++)
        {
            var r = rs[k];
            foreach (var (key, v) in new[] { ("from", r.From), ("to", r.To) })
                if (!RaisedAnchors.Contains(v))
                    errs.Add($"hull.raised[{k}].{key} = {Quote(v)}: use one of {string.Join(", ", RaisedAnchors)}");
            if (!(r.Decks >= 1))
                errs.Add($"hull.raised[{k}].decks: use a whole number of decks, 1 or more");
        }
        return errs;
    }

    /// <summary>superstructure: {"t_per_m2", "material", "tower_levels", ...} (the tower only on styles with one).</summary>
    public static List<string> SuperstructureErrors(Design design, Style style)
    {
        if (design.Superstructure is not { } s)
            return [];
        var extra = s.Extra.KeysOrEmpty().ToList();
        var errs = extra.Select(k => $"superstructure.{k}: not a superstructure setting ({string.Join(", ", SuperstructureKeys)})").ToList();
        if (s.TPerM2 < 0)
            errs.Add("superstructure.t_per_m2 must be a number, 0 or more");
        foreach (var (k, v) in new[] { ("plating_mm", s.PlatingMm), ("control_mm", s.ControlMm) })
            if (v < 0)
                errs.Add($"superstructure.{k}: a number, 0 or more (0: the structure's own gauge)");
        if (s.Material is "")
            errs.Add("superstructure.material: name the material as a string");
        if (s.DeckhouseLevels is int dl)
        {
            if (!style.HasDeckhouseLevels)
                errs.Add($"superstructure.deckhouse_levels: the {style.Name} style has no deckhouse levels yet");
            else if (dl < 1)
                errs.Add("superstructure.deckhouse_levels: use a whole number, 1 or more");
        }
        if (extra.Contains("deckhouse"))
            errs.Add("superstructure.deckhouse is gone: say what each battery stands on instead (secondary.stands_on, " +
                     "main.amidships_stands_on: \"deck\" or \"deckhouse\"); level 1 is built under what needs it");
        if (s.TowerLevels is int tl)
        {
            if (style.MinTowerLevels == 0)
                errs.Add($"superstructure.tower_levels: the {style.Name} style has no bridge tower");
            else if (tl < style.MinTowerLevels)
                errs.Add($"superstructure.tower_levels: use a whole number, {style.MinTowerLevels} or more");
        }
        if (s.AftControl != null && !style.HasControlTowers)
            errs.Add($"superstructure.aft_control: the {style.Name} style has no aft control");
        if (s.LevelsOverBridge is int lob)
        {
            if (!style.HasControlTowers)
                errs.Add($"superstructure.levels_over_bridge: the {style.Name} style has no bridge tower of levels");
            else if (lob < 0)
                errs.Add("superstructure.levels_over_bridge: use a whole number, 0 or more");
        }
        return errs;
    }

    /// <summary>A string as validation messages quote it.</summary>
    public static string Quote(string? s) => s is null ? "null" : $"'{s}'";

    /// <summary>Checks beyond the numeric limits.</summary>
    public virtual List<string> Validate(Design design)
    {
        var errs = Powerplant.Validate(design, DefaultTech);
        errs.AddRange(Crew.Validate(design, CrewStandard));
        if (design.Machinery?.Extra?.ContainsKey("type") == true)
            errs.Add("machinery.type is gone: give the plant's technology as machinery.tech " +
                     "(plant-templates.md has examples by year)");
        foreach (var (k, v) in new[] { ("length", design.Hull?.Length), ("beam", design.Hull?.Beam) })
            if (v != null)
                errs.Add($"hull.{k}: the designer works out the hull's size from what it carries; remove it");
        errs.AddRange(HullWeight.Validate(design));
        errs.AddRange(Armour.ArmourErrors(design));
        errs.AddRange(FireControl.Validate(design));
        errs.AddRange(SuperstructureErrors(design, this));
        errs.AddRange(RaisedErrors(design, this));
        var secondary = design.Secondary ?? [];
        if (secondary.Count > 1 && !TakesSecondaryList)
            errs.Add($"secondary: the {Name} style takes one secondary battery, not a list");
        foreach (var b in secondary)
        {
            if (b.Count != null && b.PerSide != null)
                errs.Add("secondary: give count (total mounts) or per_side (pairs), not both");
            var mount = b.Mount ?? "deck";
            if (mount is not ("deck" or "casemate"))
                errs.Add($"secondary.mount = {Quote(mount)}: use deck or casemate");
            else if (mount == "casemate" && !HasCasemates)
                errs.Add($"secondary.mount: the {Name} style has no casemates");
            if ((b.Tier ?? "lower") is not ("lower" or "upper"))
                errs.Add($"secondary.tier = {Quote(b.Tier)}: use lower or upper (casemates only)");
            if (b.StandsOn != null)
            {
                if (!HasRaisedMounts)
                    errs.Add($"secondary.stands_on: the {Name} style has no deckhouse to raise guns on");
                else if (mount != "deck")
                    errs.Add("secondary.stands_on: deck batteries only (casemates use tier)");
                else if (!StandsOnChoices.Contains(b.StandsOn))
                    errs.Add($"secondary.stands_on = {Quote(b.StandsOn)}: use {string.Join(" or ", StandsOnChoices)}");
            }
        }
        var mains = design.Main ?? [];
        if (mains.Count > 1 && !TakesMainList)
            errs.Add($"main: the {Name} style takes one main battery, not a list of several");
        for (int k = 0; k < mains.Count; k++)
        {
            var main = mains[k];
            string w = $"main[{k}]";
            if (main.Mid > 0 && !HasMidshipsTurrets)
                errs.Add($"{w}.mid: the {Name} style has no midships turrets");
            if (main.Wing > 0 && !HasWingTurrets)
                errs.Add($"{w}.wing: the {Name} style has no wing turrets");
            if (main.AmidshipsStandsOn != null)
            {
                if (!HasRaisedMounts)
                    errs.Add($"{w}.amidships_stands_on: the {Name} style has no deckhouse to raise guns on");
                else if (!StandsOnChoices.Contains(main.AmidshipsStandsOn))
                    errs.Add($"{w}.amidships_stands_on = {Quote(main.AmidshipsStandsOn)}: use " +
                             $"{string.Join(" or ", StandsOnChoices)}");
            }
            if (main.Superfire is { All: null } sf
                && !((sf.Fore is null || 0 <= sf.Fore && sf.Fore <= (main.Fore ?? 0)) && (sf.Aft is null || 0 <= sf.Aft && sf.Aft <= (main.Aft ?? 0))))
                errs.Add($"{w}.superfire: use true, false, or {{\"fore\": n, \"aft\": n}} within the group sizes");
        }
        errs.AddRange(UndefinedErrors(design));
        return errs;
    }

    /// <summary>machinery.tech when the design gives none (null: Powerplant.DefaultTech).</summary>
    public virtual TechInput? DefaultTech => null;
    public virtual bool TakesMainList => false;
    public virtual bool HasMidshipsTurrets => false;
    public virtual bool HasWingTurrets => false;
    public virtual bool TakesSecondaryList => false;
    public virtual bool HasCasemates => false;
    /// <summary>The lowest superstructure.tower_levels the style takes (0: it has no bridge tower).</summary>
    public virtual int MinTowerLevels => 0;
    public virtual bool HasDeckhouseLevels => false;
    public virtual bool HasControlTowers => false;
    public virtual bool HasRaisedMounts => false;
    public virtual bool RaisedHull => false;

    /// <summary>The style's changes to Navarch's tuning.</summary>
    public virtual Tuning Tuning(Design design) => new();

    /// <summary>Lay the ship out for the solved weights res.</summary>
    public abstract Layout BuildLayout(Design design, Navarch.Result res, double shift = 0.0, double spread = 0.0);

    /// <summary>First-pass weights of style-specific items, before the layout exists.</summary>
    public virtual List<Weight> RoughPayload(Design design, double D) => [];

    /// <summary>A strength deck above the main deck, or null.</summary>
    public virtual StrengthDeck? StrengthDeckOf(Design design, double D) => null;

    /// <summary>Style structure that depends on the hull.</summary>
    public virtual List<Weight> StructureWeights(Design design, double L, double B, double T, double D, Geo geo, Tuning tun) => [];

    /// <summary>The weather deck the planking is laid on: (area m2, x, height above the main deck).</summary>
    public virtual (double Area, double X, double Z) WeatherDeck(Design design, double L, double B) =>
        (HullWeight.DeckArea(L, B, design.BlockCoefficient), 0.0, 0.0);

    /// <summary>(standard-load items, full-load-only items).</summary>
    public virtual (List<Weight> Std, List<Weight> Full) PayloadWeights(Design design, double L, double D, Geo geo, Tuning tun,
        Navarch.PayloadContext ctx) => ([], []);

    /// <summary>Extra warnings once the weights are solved.</summary>
    public virtual List<string> Checks(Design design, Navarch.Result r, Tuning tun) => [];

    public virtual string CrewStandard => "H2";
    public virtual double CrewDeckK => 0.8;

    /// <summary>Departments beyond engineering, weapons and deck: (name, men).</summary>
    public virtual List<(string Name, long Men)> CrewExtra(Design design) => [];

    /// <summary>Extra report values.</summary>
    public virtual PyDict Results(Design design, Layout lay, Navarch.Result r) => new();

    /// <summary>Extra lines for the summary sheet.</summary>
    public virtual List<string> Summary(Design design, Layout lay, Navarch.Result r) => [];
}

/// <summary>A strength deck a style builds above the main deck (a carrier's closed hangar): its height, how many
/// decks it adds to the girder, and its armour plates (mm, height above the keel).</summary>
public sealed record StrengthDeck(double H, double Decks, List<(double Mm, double Z)> Plates);

/// <summary>The ship design styles (warship, carrier, merchant, planing). A design picks its style with "style".</summary>
public static class Styles
{
    static readonly string[] Names = ["warship", "carrier", "merchant", "planing"];
    static readonly Dictionary<string, Style> All = new(StringComparer.Ordinal)
    {
        ["warship"] = new WarshipStyle(), ["carrier"] = new CarrierStyle(), ["merchant"] = new MerchantStyle(),
        ["planing"] = new PlaningStyle(),
    };

    public static Style Get(Design design) => Get(design.StyleName);

    public static Style Get(string name) =>
        All.TryGetValue(name, out var st) ? st : throw new StyleError($"unknown style '{name}' (known: {string.Join(", ", Names)})");
}

/// <summary>An unknown style: Validate returns its message.</summary>
public sealed class StyleError(string message) : Exception(message);
