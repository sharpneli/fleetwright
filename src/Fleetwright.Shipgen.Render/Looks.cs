using System.Reflection;

namespace Fleetwright.Shipgen.Render;

/// <summary>looks: how a navy paints and builds its ships in a given era, with no effect on the design. The tables
/// (DEFAULT_PALETTE, STYLE_PALETTES, NAVIES) are Data/looks.jsonc, which also documents every key; this is the logic
/// that resolves a design's look into a palette and drawing shapes.</summary>
public static class Looks
{
    /// <summary>looks.jsonc, parsed once. Shared and read-only: everything below copies before changing.</summary>
    static readonly PyDict Data = LoadData();

    static PyDict LoadData()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("looks.jsonc")
                      ?? throw new InvalidOperationException("looks.jsonc is not embedded");
        using var r = new StreamReader(s);
        return (PyDict)PyJson.Parse(r.ReadToEnd())!;
    }

    public static readonly PyDict DEFAULT_PALETTE = Data.D("default_palette");
    static readonly PyDict STYLE_PALETTES = Data.D("style_palettes");
    public static readonly PyDict NAVIES = Data.D("navies");

    /// <summary>Colour groups an adjust operation can name (any palette key works too).</summary>
    static readonly Dictionary<string, string[]> GROUPS = new(StringComparer.Ordinal)
    {
        ["hull"] = ["hull"],
        ["decks"] = ["deck", "wood", "deck_line", "steel_line", "flight_deck"],
        ["upperworks"] = ["levels", "boat", "fitting", "crane"],
        ["armament"] = ["turret", "barbette", "barrel", "tube", "tub"],
        ["funnels"] = ["funnel", "funnel_cap", "funnel_band"],
        ["rigging"] = ["mast", "chain"],
        ["markings"] = ["marking", "stripe", "track", "recog_a", "recog_b", "number"],
        ["cargo"] = ["hatch", "hatch_coaming"],
        ["camouflage"] = ["camo"],
        ["canvas"] = ["awning"],
    };

    /// <summary>The styles that wear navy paint; an adjust with "styles": NAVAL leaves merchants their own colours.</summary>
    static readonly string[] NAVAL = ["warship", "carrier", "planing"];

    /// <summary>The eras a look can be drawn in, oldest first.</summary>
    public static readonly string[] ERAS = ["victorian", "great_war", "treaty", "wwii", "cold_war"];

    // How muted each era's national looks are drawn (user, 2026-10-05: "a slowly diminishing wildness factor", from
    // the Great War, still the wildest, to the Cold War, which is muted by hand and gets none). 0 = as written; m takes
    // m of the saturation from the paint (MUTE_PAINT: decks, turret roofs, funnels) and m/2 from the hull and
    // upperworks (MUTE_BODY, teak included), keeping each navy's own hue and lightness. Markings (MUTE_MARKS: stripes,
    // camouflage, numbers, turret bands) also lose contrast, pulled m toward the look's own upperworks grey
    // (desaturating alone barely quietens a red and white pattern), and painted decks (dazzle_decks) fade by m. The
    // values were set by measuring the sprites' 90th-percentile chroma over every navy (heavy cruiser, Dreadnought,
    // destroyer): unmuted Great War 40, treaty 42, WWII 50 against the Cold War's 32; these give about 36, 35, 33.5,
    // an even step down. Naval styles only, except camouflage, which is navy paint on a merchant too. Every navy,
    // generic included; the Victorian liveries stay as written. (Pulling toward the generic look instead was tried:
    // blue decks mixed with generic teak turned a muddy brown and the navies lost their identity.)
    static readonly Dictionary<string, double> ERA_MUTE = new(StringComparer.Ordinal)
    {
        ["victorian"] = 0.0, ["great_war"] = 0.12, ["treaty"] = 0.27, ["wwii"] = 0.43, ["cold_war"] = 0.0,
    };
    static readonly string[] MUTE_PAINT = ["deck", "deck_line", "steel_line", "flight_deck", "turret", "barbette",
        "funnel", "funnel_band", "funnel_cap", "awning", "stripe"];
    static readonly string[] MUTE_BODY = ["hull", "levels", "boat", "fitting", "tub", "wood"];   // teak is a material more than paint
    static readonly string[] MUTE_MARKS = ["camo", "recog_a", "recog_b", "number"];

    static readonly PyDict DEFAULT_LOOK = PyDict.Of(("navy", "generic"), ("era", "wwii"));

    // ------------------------------------------------------------------ colours

    static double[] Rgb3(string c) => [Convert.ToInt32(c[1..3], 16), Convert.ToInt32(c[3..5], 16), Convert.ToInt32(c[5..7], 16)];

    static string Hex(double[] c) =>
        "#" + string.Concat(c.Select(v => Math.Max(0L, Math.Min(255L, (long)Math.Round(v))).ToString("x2")));

    static double[] Mix(double[] a, double[] b, double t) => [.. a.Zip(b, (x, y) => x + (y - x) * t)];

    /// <summary>One adjust operation (lighten, saturate, tint; any of them, in that order) on one #rrggbb colour.</summary>
    public static string AdjustColour(string c, PyDict op)
    {
        var rgb = Rgb3(c);
        double k = op.F("lighten", 0.0);
        if (k != 0)
            rgb = Mix(rgb, k > 0 ? [255, 255, 255] : [0, 0, 0], Math.Abs(k));
        k = op.F("saturate", 0.0);
        if (k != 0)
        {
            double g = 0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2];
            double[] grey = [g, g, g];
            rgb = k < 0 ? Mix(rgb, grey, -k) : [.. rgb.Zip(grey, (v, gg) => v + (v - gg) * k)];
        }
        if (Py.Truthy(op.Get("tint")))
        {
            var tint = op.L("tint");
            rgb = Mix(rgb, Rgb3((string)tint[0]!), Py.ToDouble(tint[1]));
        }
        return Hex(rgb);
    }

    /// <summary>The palette keys an operation touches (null: all of them).</summary>
    static HashSet<string>? OpKeys(object? keys)
    {
        keys = Py.Truthy(keys) ? keys : "all";
        var out_ = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in keys is string s ? [s] : ((List<object?>)keys!).Cast<string>())
        {
            if (k == "all")
                return null;
            out_.UnionWith(GROUPS.TryGetValue(k, out var g) ? g : [k]);
        }
        return out_;
    }

    static object AdjustValue(object? v, Func<string, string> f) =>
        v is List<object?> l ? l.Select(c => (object?)f((string)c!)).ToList() : f((string)v!);

    /// <summary>pal with each adjust operation applied in turn. List values (levels) are adjusted item by item.</summary>
    static PyDict AdjustPalette(PyDict pal, IEnumerable<PyDict> ops)
    {
        pal = pal.Copy();
        foreach (var op in ops)
        {
            var keys = OpKeys(op.Get("keys"));
            foreach (var k in pal.Keys.ToList())
            {
                if (keys != null && !keys.Contains(k))
                    continue;
                pal[k] = AdjustValue(pal[k], c => AdjustColour(c, op));
            }
        }
        return pal;
    }

    // ------------------------------------------------------------------ looks

    public static string StyleName(PyDict design) => design.S("style", "warship")!;

    /// <summary>The design's look, {"navy", "era"}, with defaults filled in.</summary>
    public static PyDict LookOf(PyDict design) => PyDict.Merge(DEFAULT_LOOK, design.Get("look") as PyDict);

    /// <summary>The (navy, era) actually drawn: the design's navy, or "generic" if that navy has no entry for the era.</summary>
    public static (string Navy, string Era) Resolve(PyDict design)
    {
        var lk = LookOf(design);
        string navy = lk.S("navy"), era = lk.S("era");
        return (NAVIES.D(navy).D("eras").Has(era) ? navy : "generic", era);
    }

    /// <summary>The look as text for the preview sheet, e.g. "kure / wwii" ("" for the default look).</summary>
    public static string LookLabel(PyDict design)
    {
        var lk = LookOf(design);
        if (Py.Eq(lk["navy"], DEFAULT_LOOK["navy"]) && Py.Eq(lk["era"], DEFAULT_LOOK["era"]))
            return "";
        var (navy, _) = Resolve(design);
        return $"{lk["navy"]} / {lk["era"]}" + (navy != lk.S("navy") ? " (drawn as generic)" : "");
    }

    static PyDict ByStyleMerge(PyDict a, PyDict b)
    {
        var out_ = new PyDict();
        foreach (var s in a.Keys.Concat(b.Keys).Distinct())
            out_[s] = PyDict.Merge(a.Get(s) as PyDict, b.Get(s) as PyDict);
        return out_;
    }

    static List<object?> Cat(object? a, object? b) => [.. (a as List<object?> ?? []), .. (b as List<object?> ?? [])];

    /// <summary>One look with its "from" chain resolved: every key filled in, adjust lists concatenated.</summary>
    public static PyDict Look(string navy, string era) => Look(navy, era, []);

    static PyDict Look(string navy, string era, List<(string, string)> seen)
    {
        if (seen.Contains((navy, era)))
            throw new PyValueError($"look {navy}/{era}: 'from' loops back on itself");
        var lk = NAVIES.D(navy).D("eras").D(era);
        if (!lk.Has("from"))
            return PyDict.Merge(PyDict.Of(("palette", new PyDict()), ("by_style", new PyDict()), ("turrets", "standard"),
                ("shapes", new PyDict())), lk);
        string src = lk.S("from");
        var (pn, pe) = src.Contains('/') ? (src.Split('/')[0], src.Split('/')[1]) : (navy, src);
        var base_ = Look(pn, pe, [.. seen, (navy, era)]);
        var out_ = PyDict.Merge(base_, lk.Where(k => k != "from"));
        foreach (var k in new[] { "palette", "shapes" })
            out_[k] = PyDict.Merge(base_.Get(k) as PyDict, lk.Get(k) as PyDict);
        foreach (var k in new[] { "by_style", "shapes_by_style" })
            out_[k] = ByStyleMerge(base_.Get(k) as PyDict ?? new PyDict(), lk.Get(k) as PyDict ?? new PyDict());
        out_["adjust"] = Cat(base_.Get("adjust"), lk.Get("adjust"));
        PyDict ab = base_.Get("adjust_by_style") as PyDict ?? new PyDict(), al = lk.Get("adjust_by_style") as PyDict ?? new PyDict();
        var abs = new PyDict();
        foreach (var s in ab.Keys.Concat(al.Keys).Distinct())
            abs[s] = Cat(ab.Get(s), al.Get(s));
        out_["adjust_by_style"] = abs;
        return out_;
    }

    public static PyDict Get(PyDict design)
    {
        var (navy, era) = Resolve(design);
        return Look(navy, era);
    }

    /// <summary>ERA_MUTE for the design's look, or 0 where muting doesn't apply (merchants, but for their marks).</summary>
    static double MuteAmount(PyDict design, bool marks = false)
    {
        var (_, era) = Resolve(design);
        if (!NAVAL.Contains(StyleName(design)) && !marks)
            return 0.0;
        return ERA_MUTE.TryGetValue(era, out var m) ? m : 0.0;
    }

    /// <summary>c with m of its saturation taken away, and with a ref (a mark's background) pulled m toward it.</summary>
    static string MuteColour(string c, double m, string? ref_ = null)
    {
        c = AdjustColour(c, PyDict.Of(("saturate", -m)));
        return ref_ != null ? AdjustColour(c, PyDict.Of(("tint", Py.List(ref_, m)))) : c;
    }

    static string MarksRef(PyDict design)
    {
        var d = design.Copy();
        d["palette"] = new PyDict();
        return (string)PyDict.Merge(DEFAULT_PALETTE, Palette(d, mute: false)).L("levels")[1]!;
    }

    /// <summary>The look's drawing shapes for the design (Data/looks.jsonc, "shapes").</summary>
    public static PyDict Shapes(PyDict design)
    {
        var lk = Get(design);
        var out_ = PyDict.Merge(PyDict.Of(("clutter", LookOf(design)["era"])), lk.D("shapes"),
            lk.Get("shapes_by_style") is PyDict sbs ? sbs.Get(StyleName(design)) as PyDict : null);
        if (Py.Truthy(LookOf(design).Get("number")))
            out_["number"] = Py.Str(LookOf(design)["number"]);
        double m = MuteAmount(design, marks: true);
        if (m != 0)
        {
            if (Py.Truthy(out_.Get("dazzle_decks")))
                out_["dazzle_decks"] = out_.F("dazzle_decks") * (1 - m);
            if (Py.Truthy(out_.Get("turret_bands")))   // literal colours here; palette keys are muted in Palette()
            {
                string ref_ = MarksRef(design);
                out_["turret_bands"] = out_.L("turret_bands").Select(c => (object?)(((string)c!).StartsWith('#')
                    ? MuteColour((string)c, m, ref_) : c)).ToList();
            }
        }
        return out_;
    }

    /// <summary>The design's palette overrides (merged over DEFAULT_PALETTE by the renderer), muted by ERA_MUTE.</summary>
    public static PyDict Palette(PyDict design, bool mute = true)
    {
        var lk = Get(design);
        string st = StyleName(design);
        var pal = PyDict.Merge(lk.D("palette"), STYLE_PALETTES.Get(st) as PyDict, lk.D("by_style").Get(st) as PyDict);
        var ops = Cat(lk.Get("adjust"), (lk.Get("adjust_by_style") as PyDict)?.Get(st)).Cast<PyDict>()
            .Where(op => op.Get("styles") is not List<object?> styles || styles.Contains(st)).ToList();
        if (ops.Count > 0)
            pal = AdjustPalette(PyDict.Merge(DEFAULT_PALETTE, pal), ops);
        double m = mute ? MuteAmount(design) : 0.0, mm = mute ? MuteAmount(design, marks: true) : 0.0;
        if (mm != 0)
        {
            pal = PyDict.Merge(DEFAULT_PALETTE, pal);
            string ref_ = (string)pal.L("levels")[1]!;
            foreach (var (keys, mk, r) in new (string[], double, string?)[] { (MUTE_PAINT, m, null), (MUTE_BODY, m / 2, null), (MUTE_MARKS, mm, ref_) })
                foreach (var k in keys)
                {
                    var v = pal.Get(k);
                    if (v != null && mk != 0)
                        pal[k] = AdjustValue(v, c => MuteColour(c, mk, r));
                }
        }
        return PyDict.Merge(pal, design.Get("palette") as PyDict);
    }
}
