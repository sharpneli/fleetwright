namespace Fleetwright.Shipgen;

/// <summary>An input limit: the dotted path's numbers must lie in lo..hi (lo, hi kept as Python ints or floats, since
/// the message prints them).</summary>
public sealed record Limit(string[] Path, object Lo, object Hi);

/// <summary>styles.base.Style: style hooks with neutral defaults. A style overrides what it needs.</summary>
public abstract class Style
{
    public abstract string Name { get; }

    /// <summary>The style's own limits, merged over COMMON_LIMITS (a path in both keeps its place there).</summary>
    protected virtual IEnumerable<Limit> StyleLimits => [];

    public virtual double DEFAULT_CB => 0.55;

    /// <summary>Sizing (shipdesign.size): length (min, max), beam_max, gm_frac, tb, lb_max, slender.</summary>
    public virtual SizeRules SIZE => new((30.0, 1000.0), 100.0, 0.06, 0.36, 10.5, true);

    public sealed record SizeRules((double Min, double Max) Length, double BeamMax, double GmFrac, double Tb, double LbMax,
        bool Slender);

    static Limit Lim(object lo, object hi, params string[] path) => new(path, lo, hi);

    /// <summary>Input limits shared by every style: sanity bounds for the generator, not gameplay rules.</summary>
    public static IReadOnlyList<Limit> COMMON_LIMITS { get; } = BuildCommon();

    static List<Limit> BuildCommon()
    {
        var l = new List<Limit>
        {
            Lim(0.42, 0.68, "hull", "block_coefficient"),
            Lim(100L, 1500L, "hull", "construction", "yield_mpa"), Lim(0.8, 1.5, "hull", "construction", "join_factor"),
            Lim(0.5, 2.0, "hull", "construction", "standard"), Lim(0.3, 2.0, "hull", "freeboard"),
            Lim(1L, 2L, "hull", "raised", "decks"), Lim(0L, 200L, "hull", "plating", "shell_mm"),
            Lim(0L, 300L, "hull", "plating", "deck_wood_mm"),
            Lim(8L, 42L, "speed_kn"), Lim(1000L, 25000L, "range_nm"),
            Lim(1L, 2000L, "main", "calibre_mm"), Lim(1L, 200L, "main", "calibre_length"), Lim(1L, 20L, "main", "barrels"),
            Lim(0L, 40L, "main", "fore"), Lim(0L, 40L, "main", "aft"), Lim(0L, 40L, "main", "mid"),
            Lim(0L, 20L, "main", "wing"), Lim(0L, 2000L, "main", "armour_mm"),
            Lim(1L, 2000L, "secondary", "calibre_mm"), Lim(1L, 200L, "secondary", "calibre_length"),
            Lim(1L, 20L, "secondary", "barrels"), Lim(0L, 100L, "secondary", "per_side"), Lim(0L, 200L, "secondary", "count"),
            Lim(0L, 1000L, "main", "rounds_per_gun"), Lim(0L, 1000L, "secondary", "rounds_per_gun"),
            Lim(0L, 40L, "torpedoes", "mounts"), Lim(1L, 20L, "torpedoes", "tubes"),
            Lim(0L, 500L, "aa", "heavy"), Lim(0L, 500L, "aa", "light"),
            Lim(0L, 2000L, "armour", "belt_mm"),
            Lim(0L, 20L, "armour", "tds_m"), Lim(0L, 2000L, "armour", "bulkhead_mm"),
            Lim(0L, 2000L, "armour", "belt_bottom_mm"), Lim(0L, 30L, "armour", "belt_depth_m"), Lim(0L, 30L, "armour", "belt_height_m"),
            Lim(0L, 2000L, "armour", "upper_belt", "mm"),
            Lim(0L, 2000L, "armour", "end_belts", "fore", "mm"), Lim(0L, 2000L, "armour", "end_belts", "fore", "tip_mm"),
            Lim(0L, 2000L, "armour", "end_belts", "aft", "mm"), Lim(0L, 2000L, "armour", "end_belts", "aft", "tip_mm"),
        };
        foreach (var e in new[] { "fore", "aft" })
        {
            l.Add(Lim(0L, 1L, "armour", "end_belts", e, "reach"));
            l.Add(Lim(0L, 2000L, "armour", "end_belts", e, "bulkhead_mm"));
        }
        foreach (var k in new[] { "mm", "deck_mm", "bulkhead_mm" })
            l.Add(Lim(0L, 2000L, "armour", "steering_box", k));
        l.Add(Lim(0L, 5L, "superstructure", "t_per_m2"));
        l.Add(Lim(1L, 30L, "superstructure", "tower_levels"));
        l.Add(Lim(0L, 30L, "superstructure", "levels_over_bridge"));
        l.Add(Lim(1L, 30L, "superstructure", "deckhouse_levels"));
        l.Add(Lim(0L, 200L, "superstructure", "plating_mm"));
        l.Add(Lim(0L, 500L, "superstructure", "control_mm"));
        foreach (var b in new[] { "main", "secondary", "aa" })
        {
            l.Add(Lim(0L, 100L, "fire_control", b, "directors"));
            l.Add(Lim(0L, 50L, "fire_control", b, "rangefinder_m"));
            l.Add(Lim(0L, 2000L, "fire_control", b, "armour_mm"));
            l.Add(Lim(0L, 500L, "fire_control", b, "radar_t"));
            l.Add(Lim(0L, 500L, "fire_control", b, "computer_t"));
        }
        l.Add(Lim(0L, 500L, "fire_control", "search_radar_t"));
        l.Add(Lim(0L, 60L, "funnels"));
        l.Add(Lim(1L, 100L, "machinery", "tech", "draught", "velocity_m_s"));
        l.Add(Lim(300L, 2000L, "machinery", "tech", "draught", "gas_temp_k"));
        return l;
    }

    /// <summary>{**COMMON_LIMITS, **self.LIMITS}.</summary>
    public List<Limit> Limits()
    {
        var out_ = COMMON_LIMITS.ToList();
        foreach (var s in StyleLimits)
        {
            int i = out_.FindIndex(x => x.Path.SequenceEqual(s.Path));
            if (i >= 0)
                out_[i] = s;
            else
                out_.Add(s);
        }
        return out_;
    }

    // numbers the physics divides by or takes as counts: (path, low, low_inclusive, high or null)
    static readonly (string[] Path, object Lo, bool Incl, object? Hi)[] DEFINED =
    [
        (["hull", "block_coefficient"], 0.0, false, 1.0), (["speed_kn"], 0.0, false, null),
        (["main", "barrels"], 1L, true, null), (["main", "calibre_mm"], 0.0, false, null),
        (["main", "calibre_length"], 0.0, false, null), (["secondary", "barrels"], 1L, true, null),
        (["secondary", "calibre_mm"], 0.0, false, null), (["secondary", "calibre_length"], 0.0, false, null),
        (["main", "rounds_per_gun"], 0L, true, null), (["secondary", "rounds_per_gun"], 0L, true, null),
    ];

    static readonly (string Group, string[] Keys)[] REQUIRED =
    [
        ("main", ["calibre_mm", "calibre_length", "barrels"]),
        ("secondary", ["calibre_mm", "calibre_length", "barrels"]),
        ("torpedoes", ["mounts", "tubes"]),
    ];

    /// <summary>The dicts a dotted path's parent reaches: a list on the way checks every entry.</summary>
    public static List<PyDict> Walk(PyDict design, string[] path)
    {
        var ds = new List<object?> { design };
        foreach (var k in path[..^1])
        {
            var next = new List<object?>();
            foreach (var d in ds)
            {
                var v = ((PyDict)d!).Get(k);
                if (v is List<object?> l)
                    next.AddRange(l);
                else
                    next.Add(Py.Truthy(v) ? v : new PyDict());
            }
            ds = next;
        }
        return ds.Select(d => d as PyDict ?? throw new PyTypeError($"argument of type '{d?.GetType().Name}' is not iterable"))
            .ToList();
    }

    /// <summary>Missing gun and torpedo data (REQUIRED), and numbers outside the range where the physics means
    /// anything at all (DEFINED).</summary>
    public static List<string> UndefinedErrors(PyDict design)
    {
        var errs = new List<string>();
        foreach (var (group, keys) in REQUIRED)
        {
            var v = design.Get(group);
            var items = Batteries.AsList(v);
            for (int i = 0; i < items.Count; i++)
            {
                string where = v is List<object?> ? $"{group}[{i}]" : group;
                if (items[i] is not PyDict b)
                {
                    errs.Add($"{where}: give an object with {string.Join(", ", keys)}");
                    continue;
                }
                errs.AddRange(keys.Where(k => !b.Has(k)).Select(k => $"{where}.{k} is missing"));
            }
        }
        foreach (var (path, lo, incl, hi) in DEFINED)
            foreach (var d in Walk(design, path))
            {
                if (!d.Has(path[^1]))
                    continue;
                var v = d[path[^1]];
                bool bad = !Py.IsNumber(v) || v is bool || !double.IsFinite(Py.ToDouble(v)) ||
                           (incl ? Py.ToDouble(v) < Py.ToDouble(lo) : Py.ToDouble(v) <= Py.ToDouble(lo)) ||
                           (hi is not null && Py.ToDouble(v) > Py.ToDouble(hi));
                if (bad)
                    errs.Add($"{string.Join('.', path)} = {Py.Repr(v)}: must be {(incl ? "at least" : "above")} {Py.Str(lo)}" +
                             (hi is not null ? $" and at most {Py.Str(hi)}" : ""));
            }
        return errs;
    }

    static readonly string[] SUPERSTRUCTURE_KEYS = ["t_per_m2", "material", "plating_mm", "control_mm", "tower_levels",
        "deckhouse_levels", "aft_control", "levels_over_bridge"];
    public static readonly string[] RAISED_ANCHORS = ["bow", "fore_group", "bridge", "funnels", "aft_control", "aft_group", "stern"];
    static readonly string[] STANDS_ON = ["deck", "deckhouse"];

    /// <summary>hull.raised: [{"from": an anchor, "to": an anchor, "decks": n}].</summary>
    public static List<string> RaisedErrors(PyDict design, Style style)
    {
        var rsV = design.DOr("hull").Get("raised", new List<object?>());
        if (rsV is not List<object?> rs)
            return ["hull.raised: use a list of raised stretches, e.g. [{\"from\": \"bow\", \"to\": \"bridge\", " +
                    "\"decks\": 1}]"];
        if (rs.Count > 0 && !style.RAISED_HULL)
            return [$"hull.raised: the {style.Name} style lays out its own raised decks"];
        var errs = new List<string>();
        for (int k = 0; k < rs.Count; k++)
        {
            if (rs[k] is not PyDict r)
            {
                errs.Add($"hull.raised[{k}]: use {{\"from\", \"to\", \"decks\"}}");
                continue;
            }
            foreach (var key in new[] { "from", "to" })
                if (!RAISED_ANCHORS.Any(a => Py.Eq(r.Get(key), a)))
                    errs.Add($"hull.raised[{k}].{key} = {Py.Repr(r.Get(key))}: use one of {string.Join(", ", RAISED_ANCHORS)}");
            if (!(Py.IsIntNotBool(r.Get("decks")) && Py.ToLong(r["decks"]) >= 1))
                errs.Add($"hull.raised[{k}].decks: use a whole number of decks, 1 or more");
        }
        return errs;
    }

    /// <summary>superstructure: {"t_per_m2", "material", "tower_levels"} (the tower only on styles with one).</summary>
    public static List<string> SuperstructureErrors(PyDict design, Style style)
    {
        var sV = design.Get("superstructure");
        if (sV is null)
            return [];
        if (sV is not PyDict s)
            return ["superstructure: use {\"t_per_m2\", \"material\", \"tower_levels\"}"];
        var errs = s.Keys.Where(k => !SUPERSTRUCTURE_KEYS.Contains(k))
            .Select(k => $"superstructure.{k}: not a superstructure setting ({string.Join(", ", SUPERSTRUCTURE_KEYS)})").ToList();
        if (s.Has("t_per_m2") && !(Py.IsNumber(s["t_per_m2"]) && Py.ToDouble(s["t_per_m2"]) >= 0))
            errs.Add("superstructure.t_per_m2 must be a number, 0 or more");
        foreach (var k in new[] { "plating_mm", "control_mm" })
            if (s.Has(k) && !(Py.IsNumber(s[k]) && Py.ToDouble(s[k]) >= 0))
                errs.Add($"superstructure.{k}: a number, 0 or more (0: the structure's own gauge)");
        if (s.Has("material") && !(s["material"] is string ms && ms.Length > 0))
            errs.Add("superstructure.material: name the material as a string");
        if (s.Has("deckhouse_levels"))
        {
            if (!style.DECKHOUSE_LEVELS)
                errs.Add($"superstructure.deckhouse_levels: the {style.Name} style has no deckhouse levels yet");
            else if (!Py.IsInt(s["deckhouse_levels"]) || Py.ToLong(s["deckhouse_levels"]) < 1)
                errs.Add("superstructure.deckhouse_levels: use a whole number, 1 or more");
        }
        if (s.Has("deckhouse"))
            errs.Add("superstructure.deckhouse is gone: say what each battery stands on instead (secondary.stands_on, " +
                     "main.amidships_stands_on: \"deck\" or \"deckhouse\"); level 1 is built under what needs it");
        if (s.Has("tower_levels"))
        {
            if (style.MIN_TOWER == 0)
                errs.Add($"superstructure.tower_levels: the {style.Name} style has no bridge tower");
            else if (!Py.IsInt(s["tower_levels"]) || Py.ToLong(s["tower_levels"]) < style.MIN_TOWER)
                errs.Add($"superstructure.tower_levels: use a whole number, {style.MIN_TOWER} or more");
        }
        if (s.Has("aft_control"))
        {
            if (!style.CONTROL_TOWERS)
                errs.Add($"superstructure.aft_control: the {style.Name} style has no aft control");
            else if (s["aft_control"] is not bool)
                errs.Add("superstructure.aft_control: true or false");
        }
        if (s.Has("levels_over_bridge"))
        {
            if (!style.CONTROL_TOWERS)
                errs.Add($"superstructure.levels_over_bridge: the {style.Name} style has no bridge tower of levels");
            else if (!Py.IsIntNotBool(s["levels_over_bridge"]) || Py.ToLong(s["levels_over_bridge"]) < 0)
                errs.Add("superstructure.levels_over_bridge: use a whole number, 0 or more");
        }
        return errs;
    }

    /// <summary>Checks beyond the numeric limits.</summary>
    public virtual List<string> Validate(PyDict design)
    {
        var errs = Powerplant.Validate(design, DefaultTech);
        errs.AddRange(Crew.Validate(design, CREW_STANDARD));
        if (design.DOr("machinery").Has("type"))
            errs.Add("machinery.type is gone: give the plant's technology as machinery.tech " +
                     "(plant-templates.md has examples by year)");
        errs.AddRange(new[] { "length", "beam" }.Where(k => design.DOr("hull").Has(k))
            .Select(k => $"hull.{k}: the designer works out the hull's size from what it carries; remove it"));
        errs.AddRange(HullWeight.Validate(design));
        errs.AddRange(Armour.ArmourErrors(design));
        errs.AddRange(FireControl.Validate(design));
        errs.AddRange(SuperstructureErrors(design, this));
        errs.AddRange(RaisedErrors(design, this));
        if (design.Get("secondary") is List<object?> && !SECONDARY_LIST)
            errs.Add($"secondary: the {Name} style takes one secondary battery, not a list");
        foreach (var o in Batteries.AsList(design.Get("secondary")))
        {
            if (o is not PyDict b)
                continue;
            if (b.Has("count") && b.Has("per_side"))
                errs.Add("secondary: give count (total mounts) or per_side (pairs), not both");
            var mount = b.Get("mount", "deck");
            if (!Py.In(mount, "deck", "casemate"))
                errs.Add($"secondary.mount = {Py.Repr(mount)}: use deck or casemate");
            else if (Py.Eq(mount, "casemate") && !CASEMATES)
                errs.Add($"secondary.mount: the {Name} style has no casemates");
            if (!Py.In(b.Get("tier", "lower"), "lower", "upper"))
                errs.Add($"secondary.tier = {Py.Repr(b["tier"])}: use lower or upper (casemates only)");
            if (b.Has("stands_on"))
            {
                if (!RAISED_MOUNTS)
                    errs.Add($"secondary.stands_on: the {Name} style has no deckhouse to raise guns on");
                else if (!Py.Eq(mount, "deck"))
                    errs.Add("secondary.stands_on: deck batteries only (casemates use tier)");
                else if (!STANDS_ON.Any(x => Py.Eq(b["stands_on"], x)))
                    errs.Add($"secondary.stands_on = {Py.Repr(b["stands_on"])}: use {string.Join(" or ", STANDS_ON)}");
            }
        }
        var mains = design.Get("main");
        if (mains is not null && !(mains is List<object?> || mains is PyDict))
        {
            errs.Add("main: use a list of batteries, each {\"calibre_mm\", \"calibre_length\", \"barrels\", " +
                     "\"armour_mm\", \"fore\", \"aft\", ...}");
            mains = new List<object?>();
        }
        var ml = Batteries.AsList(mains);
        if (ml.Count > 1 && !MAIN_LIST)
            errs.Add($"main: the {Name} style takes one main battery, not a list of several");
        for (int k = 0; k < ml.Count; k++)
        {
            if (ml[k] is not PyDict main)
                continue;
            string w = $"main[{k}]";
            if (main.B("mid") && !MIDSHIPS_TURRETS)
                errs.Add($"{w}.mid: the {Name} style has no midships turrets");
            if (main.B("wing") && !WING_TURRETS)
                errs.Add($"{w}.wing: the {Name} style has no wing turrets");
            if (main.Has("amidships_stands_on"))
            {
                if (!RAISED_MOUNTS)
                    errs.Add($"{w}.amidships_stands_on: the {Name} style has no deckhouse to raise guns on");
                else if (!STANDS_ON.Any(x => Py.Eq(main["amidships_stands_on"], x)))
                    errs.Add($"{w}.amidships_stands_on = {Py.Repr(main["amidships_stands_on"])}: use " +
                             $"{string.Join(" or ", STANDS_ON)}");
            }
            if (main.Get("echelon", false) is not bool)
                errs.Add($"{w}.echelon: use true or false");
            if (main.Get("cross_deck", false) is not bool)
                errs.Add($"{w}.cross_deck: use true or false");
            var sf = main.Get("superfire", true);
            bool sfOk = sf is bool || sf is PyDict sfd && sfd.Where(kv => kv.Key is "fore" or "aft")
                .All(kv => Py.IsInt(kv.Value) && 0 <= Py.ToLong(kv.Value) && Py.ToDouble(kv.Value) <= main.F(kv.Key, 0));
            if (!sfOk)
                errs.Add($"{w}.superfire: use true, false, or {{\"fore\": n, \"aft\": n}} within the group sizes");
        }
        errs.AddRange(UndefinedErrors(design));
        return errs;
    }

    /// <summary>machinery.tech when the design gives none (null: powerplant.DEFAULT_TECH).</summary>
    public virtual PyDict? DefaultTech => null;
    public virtual bool MAIN_LIST => false;
    public virtual bool MIDSHIPS_TURRETS => false;
    public virtual bool WING_TURRETS => false;
    public virtual bool SECONDARY_LIST => false;
    public virtual bool CASEMATES => false;
    public virtual long MIN_TOWER => 0;
    public virtual bool DECKHOUSE_LEVELS => false;
    public virtual bool CONTROL_TOWERS => false;
    public virtual bool RAISED_MOUNTS => false;
    public virtual bool RAISED_HULL => false;

    /// <summary>Overrides of navarch.TUNING for this design.</summary>
    public virtual PyDict Tuning(PyDict design) => new();

    /// <summary>Lay the ship out for the solved weights res.</summary>
    public abstract Layout BuildLayout(PyDict design, Navarch.Result res, double shift = 0.0, double spread = 0.0);

    /// <summary>First-pass weights of style-specific items, before the layout exists.</summary>
    public virtual List<Weight> RoughPayload(PyDict design, double D) => [];

    /// <summary>A strength deck above the main deck: dict(h, decks, plates) or null.</summary>
    public virtual PyDict? StrengthDeck(PyDict design, double D) => null;

    /// <summary>Style structure that depends on the hull.</summary>
    public virtual List<Weight> StructureWeights(PyDict design, double L, double B, double T, double D, Geo geo, PyDict tun) => [];

    /// <summary>The weather deck the planking is laid on: (area m2, x, height above the main deck).</summary>
    public virtual (double Area, double X, double Z) WeatherDeck(PyDict design, double L, double B) =>
        (HullWeight.DeckArea(L, B, design.D("hull").F("block_coefficient")), 0.0, 0.0);

    /// <summary>(standard-load items, full-load-only items).</summary>
    public virtual (List<Weight> Std, List<Weight> Full) PayloadWeights(PyDict design, double L, double D, Geo geo, PyDict tun,
        Navarch.PayloadContext ctx) => ([], []);

    /// <summary>Extra warnings once the weights are solved.</summary>
    public virtual List<string> Checks(PyDict design, Navarch.Result r, PyDict tun) => [];

    public virtual string CREW_STANDARD => "H2";
    public virtual double CREW_DECK_K => 0.8;

    /// <summary>Departments beyond engineering, weapons and deck: {name: men}.</summary>
    public virtual PyDict CrewExtra(PyDict design) => new();

    /// <summary>Extra report values.</summary>
    public virtual PyDict Results(PyDict design, Layout lay, Navarch.Result r) => new();

    /// <summary>Extra lines for the summary sheet.</summary>
    public virtual List<object?> Summary(PyDict design, Layout lay, Navarch.Result r) => [];
}

/// <summary>styles: ship design styles (warship, carrier, merchant, planing). A design picks its style with "style".</summary>
public static class Styles
{
    static readonly string[] Names = ["warship", "carrier", "merchant", "planing"];
    static readonly Dictionary<string, Style> All = new(StringComparer.Ordinal)
    {
        ["warship"] = new WarshipStyle(), ["carrier"] = new CarrierStyle(), ["merchant"] = new MerchantStyle(),
        ["planing"] = new PlaningStyle(),
    };

    public static Style Get(PyDict design) => Get(design.Get("style", "warship"));

    public static Style Get(object? name)
    {
        if (name is string s && All.TryGetValue(s, out var st))
            return st;
        throw new StyleError($"unknown style {Py.Repr(name)} (known: {string.Join(", ", Names)})");
    }
}

/// <summary>styles.get's KeyError: validate() returns its message.</summary>
public sealed class StyleError(string message) : Exception(message);
