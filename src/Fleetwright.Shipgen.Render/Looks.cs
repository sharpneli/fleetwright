using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen.Render;

/// <summary>looks: how a navy paints and builds its ships in a given era, with no effect on the design. The tables
/// (default_palette, style_palettes, navies) are Data/looks.jsonc, which also documents every key; this is the logic
/// that resolves a design's look into a palette and drawing shapes. A look is merged as JSON along its "from" chain,
/// then read as a Palette and Shapes.</summary>
public static class Looks
{
    /// <summary>looks.jsonc, parsed once. Shared and read-only: everything below copies before changing.</summary>
    static readonly JsonObject Data = LoadData();

    static JsonObject LoadData()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("looks.jsonc")
                      ?? throw new InvalidOperationException("looks.jsonc is not embedded");
        using var r = new StreamReader(s);
        return JsonFile.Parse(r.ReadToEnd())!.AsObject();
    }

    static JsonObject Obj(JsonNode? n) => n as JsonObject ?? [];

    static Dictionary<string, Paint> Paints(JsonNode? n) =>
        n is JsonObject o ? JsonSerializer.Deserialize(o, RenderJson.Default.DictionaryStringPaint)! : [];

    public static readonly Palette DEFAULT_PALETTE = new(Paints(Data["default_palette"]));
    static readonly JsonObject STYLE_PALETTES = Obj(Data["style_palettes"]);
    static readonly JsonObject NAVIES = Obj(Data["navies"]);

    /// <summary>The navies that have looks, as looks.jsonc lists them.</summary>
    public static IEnumerable<string> Navies => NAVIES.Select(kv => kv.Key);

    /// <summary>The eras a navy has a look for.</summary>
    public static IEnumerable<string> ErasOf(string navy) => Eras(navy).Select(kv => kv.Key);

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


    // ------------------------------------------------------------------ colours

    static double[] Rgb3(string c) => [Convert.ToInt32(c[1..3], 16), Convert.ToInt32(c[3..5], 16), Convert.ToInt32(c[5..7], 16)];

    static string Hex(double[] c) =>
        "#" + string.Concat(c.Select(v => Math.Max(0L, Math.Min(255L, (long)Math.Round(v))).ToString("x2")));

    static double[] Mix(double[] a, double[] b, double t) => [.. a.Zip(b, (x, y) => x + (y - x) * t)];

    /// <summary>One adjust operation from looks.jsonc: lighten (toward white, or black when negative), saturate (away
    /// from or toward grey), tint (toward a colour by an amount), in that order, on the palette keys (null: all) of
    /// the styles it names (null: every style).</summary>
    public sealed record Adjust(double Lighten = 0, double Saturate = 0, (string Colour, double K)? Tint = null)
    {
        public HashSet<string>? Keys { get; init; }
        public List<string>? Styles { get; init; }

        public static Adjust From(JsonObject o)
        {
            List<string> Strings(JsonNode? n) => n is JsonArray a ? [.. a.Select(x => (string)x!)] : [(string)n!];
            HashSet<string>? keys = null;
            if (o["keys"] is { } k && !(k is JsonArray { Count: 0 } || (k is JsonValue v && (string?)v is null or "")))
            {
                var names = Strings(k);
                if (!names.Contains("all"))
                    keys = [.. names.SelectMany(n => GROUPS.TryGetValue(n, out var g) ? g : [n])];
            }
            var tint = o["tint"] is JsonArray { Count: 2 } t ? ((string)t[0]!, (double)t[1]!) : ((string, double)?)null;
            return new Adjust((double?)o["lighten"] ?? 0, (double?)o["saturate"] ?? 0, tint)
            {
                Keys = keys, Styles = o["styles"] is JsonArray s ? Strings(s) : null,
            };
        }
    }

    /// <summary>One adjust operation on one #rrggbb colour.</summary>
    public static string AdjustColour(string c, Adjust op)
    {
        var rgb = Rgb3(c);
        double k = op.Lighten;
        if (k != 0)
            rgb = Mix(rgb, k > 0 ? [255, 255, 255] : [0, 0, 0], Math.Abs(k));
        k = op.Saturate;
        if (k != 0)
        {
            double g = 0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2];
            double[] grey = [g, g, g];
            rgb = k < 0 ? Mix(rgb, grey, -k) : [.. rgb.Zip(grey, (v, gg) => v + (v - gg) * k)];
        }
        if (op.Tint is { } tint)
            rgb = Mix(rgb, Rgb3(tint.Colour), tint.K);
        return Hex(rgb);
    }

    /// <summary>pal with each adjust operation applied in turn. List values (levels) are adjusted item by item.</summary>
    static Dictionary<string, Paint> AdjustPalette(Dictionary<string, Paint> pal, IEnumerable<Adjust> ops)
    {
        pal = new(pal, StringComparer.Ordinal);
        foreach (var op in ops)
            foreach (var k in pal.Keys.ToList())
                if (op.Keys is null || op.Keys.Contains(k))
                    pal[k] = pal[k].Select(c => AdjustColour(c, op));
        return pal;
    }

    // ------------------------------------------------------------------ looks

    /// <summary>The design's look with the defaults filled in (generic, wwii).</summary>
    public static LookInput LookOf(Design design) =>
        new() { Navy = design.Look?.Navy ?? "generic", Era = design.Look?.Era ?? "wwii", Number = design.Look?.Number };

    static JsonObject Eras(string navy) => Obj(Obj(NAVIES[navy])["eras"]);

    /// <summary>The (navy, era) actually drawn: the design's navy, or "generic" if that navy has no entry for the era.</summary>
    public static (string Navy, string Era) Resolve(Design design)
    {
        var lk = LookOf(design);
        string navy = lk.Navy!, era = lk.Era!;
        return (Eras(navy).ContainsKey(era) ? navy : "generic", era);
    }

    /// <summary>The look as text for the preview sheet, e.g. "kure / wwii" ("" for the default look).</summary>
    public static string LookLabel(Design design)
    {
        var lk = LookOf(design);
        if (lk.Navy == "generic" && lk.Era == "wwii")
            return "";
        var (navy, _) = Resolve(design);
        return $"{lk.Navy} / {lk.Era}" + (navy != lk.Navy ? " (drawn as generic)" : "");
    }

    /// <summary>The objects merged key by key, later ones winning (a copy: the inputs are left alone).</summary>
    static JsonObject Merge(params JsonNode?[] objs)
    {
        var out_ = new JsonObject();
        foreach (var o in objs.OfType<JsonObject>())
            foreach (var (k, v) in o)
                out_[k] = v?.DeepClone();
        return out_;
    }

    /// <summary>{style: {...}} merged style by style.</summary>
    static JsonObject ByStyleMerge(JsonNode? a, JsonNode? b)
    {
        var (oa, ob) = (Obj(a), Obj(b));
        var out_ = new JsonObject();
        foreach (var s in oa.Select(kv => kv.Key).Concat(ob.Select(kv => kv.Key)).Distinct())
            out_[s] = Merge(oa[s], ob[s]);
        return out_;
    }

    static JsonArray Cat(JsonNode? a, JsonNode? b) =>
        [.. (a as JsonArray ?? []).Select(x => x?.DeepClone()), .. (b as JsonArray ?? []).Select(x => x?.DeepClone())];

    /// <summary>One look with its "from" chain resolved: every key filled in, adjust lists concatenated.</summary>
    static JsonObject Look(string navy, string era, List<(string, string)>? seen = null)
    {
        seen ??= [];
        if (seen.Contains((navy, era)))
            throw new InvalidOperationException($"look {navy}/{era}: 'from' loops back on itself");
        var lk = Obj(Eras(navy)[era]);
        if (lk["from"] is not { } from)
            return Merge(new JsonObject { ["palette"] = new JsonObject(), ["by_style"] = new JsonObject(), ["turrets"] = "standard",
                ["shapes"] = new JsonObject() }, lk);
        string src = (string)from!;
        var (pn, pe) = src.Contains('/') ? (src.Split('/')[0], src.Split('/')[1]) : (navy, src);
        var base_ = Look(pn, pe, [.. seen, (navy, era)]);
        var own = Merge(lk);
        own.Remove("from");
        var out_ = Merge(base_, own);
        foreach (var k in new[] { "palette", "shapes" })
            out_[k] = Merge(base_[k], lk[k]);
        foreach (var k in new[] { "by_style", "shapes_by_style" })
            out_[k] = ByStyleMerge(base_[k], lk[k]);
        out_["adjust"] = Cat(base_["adjust"], lk["adjust"]);
        var (ab, al) = (Obj(base_["adjust_by_style"]), Obj(lk["adjust_by_style"]));
        var abs = new JsonObject();
        foreach (var s in ab.Select(kv => kv.Key).Concat(al.Select(kv => kv.Key)).Distinct())
            abs[s] = Cat(ab[s], al[s]);
        out_["adjust_by_style"] = abs;
        return out_;
    }

    static JsonObject Get(Design design)
    {
        var (navy, era) = Resolve(design);
        return Look(navy, era);
    }

    /// <summary>How the look draws armoured turrets ("standard" or TurretArt.LookTurretBody's).</summary>
    public static string TurretLook(Design design) => (string)Get(design)["turrets"]!;

    /// <summary>ERA_MUTE for the design's look, or 0 where muting doesn't apply (merchants, but for their marks).</summary>
    static double MuteAmount(Design design, bool marks = false)
    {
        var (_, era) = Resolve(design);
        if (!NAVAL.Contains(design.StyleName) && !marks)
            return 0.0;
        return ERA_MUTE.GetValueOrDefault(era, 0.0);
    }

    /// <summary>c with m of its saturation taken away, and with a ref (a mark's background) pulled m toward it.</summary>
    static string MuteColour(string c, double m, string? ref_ = null)
    {
        c = AdjustColour(c, new Adjust(Saturate: -m));
        return ref_ != null ? AdjustColour(c, new Adjust(Tint: (ref_, m))) : c;
    }

    static string MarksRef(Design design) => DEFAULT_PALETTE.Over(Overrides(design with { Palette = null }, mute: false)).Colours("levels")[1];

    /// <summary>The look's drawing shapes for the design (Data/looks.jsonc, "shapes").</summary>
    public static Shapes Shapes(Design design)
    {
        var lk = Get(design);
        var merged = Merge(new JsonObject { ["clutter"] = LookOf(design).Era }, lk["shapes"], Obj(lk["shapes_by_style"])[design.StyleName]);
        var out_ = JsonSerializer.Deserialize(merged, RenderJson.Default.Shapes)!;
        if (!string.IsNullOrEmpty(design.Look?.Number))
            out_ = out_ with { Number = design.Look.Number };
        double m = MuteAmount(design, marks: true);
        if (m == 0)
            return out_;
        if (out_.DazzleDecks is double dd && dd != 0)
            out_ = out_ with { DazzleDecks = dd * (1 - m) };
        if (out_.TurretBands is { Count: > 0 } bands)   // literal colours here; palette keys are muted in Palette()
        {
            string ref_ = MarksRef(design);
            out_ = out_ with { TurretBands = [.. bands.Select(c => c.StartsWith('#') ? MuteColour(c, m, ref_) : c)] };
        }
        return out_;
    }

    /// <summary>The design's palette: DEFAULT_PALETTE under the look's own colours, muted by ERA_MUTE.</summary>
    public static Palette Palette(Design design) => DEFAULT_PALETTE.Over(Overrides(design));

    /// <summary>The look's colours over DEFAULT_PALETTE (and the design's own on top), muted by ERA_MUTE.</summary>
    static Dictionary<string, Paint> Overrides(Design design, bool mute = true)
    {
        var lk = Get(design);
        string st = design.StyleName;
        var pal = Paints(Merge(lk["palette"], STYLE_PALETTES[st], Obj(lk["by_style"])[st]));
        var ops = Cat(lk["adjust"], Obj(lk["adjust_by_style"])[st]).Select(op => Adjust.From(op!.AsObject()))
            .Where(op => op.Styles is null || op.Styles.Contains(st)).ToList();
        Dictionary<string, Paint> WithDefaults() =>
            new(DEFAULT_PALETTE.Over(pal).Paints, StringComparer.Ordinal);
        if (ops.Count > 0)
            pal = AdjustPalette(WithDefaults(), ops);
        double m = mute ? MuteAmount(design) : 0.0, mm = mute ? MuteAmount(design, marks: true) : 0.0;
        if (mm != 0)
        {
            pal = WithDefaults();
            string ref_ = pal["levels"].Colours[1];
            foreach (var (keys, mk, r) in new (string[], double, string?)[] { (MUTE_PAINT, m, null), (MUTE_BODY, m / 2, null), (MUTE_MARKS, mm, ref_) })
                foreach (var k in keys)
                    if (pal.TryGetValue(k, out var v) && mk != 0)
                        pal[k] = v.Select(c => MuteColour(c, mk, r));
        }
        foreach (var (k, paint) in design.Palette ?? [])
            pal[k] = paint;
        return pal;
    }
}
