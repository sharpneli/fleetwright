namespace Fleetwright.Shipgen;

/// <summary>A design's outfit (docs/battle/fire.md): what burns aboard, as numbers. The templates are the research's
/// fire loads by period (damage/17 §2.2, fire_ref.py's FIRE_LOAD; paint apart, §2.3; growth §3) with a name for a label;
/// a design carries the block, and the sim reads only its numbers.</summary>
public static class Outfits
{
    /// <summary>A template: its block, and who it fits.</summary>
    public sealed record Template(string Name, OutfitInput Value, string Note);

    static FireLoadsInput L(double quarters, double cabins, double offices, double stores, double machinery, double deck) =>
        new() { Quarters = quarters, Cabins = cabins, Offices = offices, Stores = stores, Machinery = machinery, Deck = deck };

    static Template T(string name, FireLoadsInput fitted, FireLoadsInput stripped, double paint, double growth, string note, bool sprinklers = false) =>
        new(name, new OutfitInput { Name = name, Fitted = fitted, Stripped = stripped, PaintMjM2 = paint, Growth = growth, MagazineSprinklers = sprinklers }, note);

    public static readonly Template[] All =
    [
        T("Victorian (1890)", L(450, 900, 420, 1500, 150, 400), L(380, 600, 420, 1500, 150, 350), 30, 2.0,
            "Varnished and oiled wooden partitions, panelled cabins, boats and hammocks on deck (the Yalu's \"alleys of roaring " +
            "flame\", Santiago). Stripped: the woodwork and boats landed before action, as the Chinese did before the Yalu; not " +
            "enough. Growth twice a furnished room's [EST]."),
        T("Pre-dreadnought (1905)", L(450, 900, 420, 1500, 150, 400), L(380, 600, 420, 1500, 150, 350), 60, 1.5,
            "Less varnished wood, more coats of paint (\"war paint\" burned on Alexander III). Stripped as the Russians were at " +
            "Tsushima: woodwork out \"as far as possible\", yet Orel had 34 fires."),
        T("Great War (1914)", L(420, 750, 420, 1500, 150, 300), L(400, 450, 420, 1500, 150, 150), 50, 1.0,
            "Stripped at mobilisation (Queen Mary, August 1914: boats, wooden fittings, furniture landed); wooden weather decks " +
            "stay, wetted before action. As fitted is the peacetime ship [EST]."),
        T("Interwar (1939)", L(380, 650, 500, 1500, 150, 250), L(240, 280, 300, 1200, 120, 120), 100, 1.3,
            "Comfort back: linoleum, cork, upholstery, up to 80-90 coats of paint (Savo's inventory). Stripped: inflammables " +
            "landed, the paint left on (scraping it is a refit). Magazine sprinklers (the USN's 1932 bulletin; research 10 " +
            "§4.1).", sprinklers: true),
        T("Post-Savo (1943)", L(240, 280, 300, 1200, 120, 120), L(240, 280, 300, 1200, 120, 120), 10, 0.7,
            "The USN after Savo: linoleum up, paint scraped to bare steel, cork, rugs and curtains banned, retardant fabrics " +
            "(NBS berthing 240 MJ/m²); already stripped. Magazine sprinklers.", sprinklers: true),
    ];

    public static Template? Get(string name) => All.FirstOrDefault(t => t.Name == name);

    /// <summary>The outfit a navy fitted in a year [EST from 17 §2.1]: Victorian before 1900, pre-dreadnought to 1909, the
    /// Great War's to 1918, interwar to 1942; from 1943 the USN's (and the generic look's, as the shell templates take it)
    /// post-Savo, everyone else's interwar.</summary>
    public static Template ByNavyAndYear(string? navy, int year) => year switch
    {
        < 1900 => All[0],
        < 1910 => All[1],
        < 1919 => All[2],
        < 1943 => All[3],
        _ => navy is "brooklyn" or "generic" or null ? All[4] : All[3],
    };

    /// <summary>The template a design falls back on: its plant's year's (machinery.tech's name ends with it; no navy: the
    /// look is the drawing's only), else the Great War's. Format 8's upgrade read the look's navy into each design's
    /// outfit.</summary>
    public static Template Default(Design design) =>
        Fittings.YearOf(design.Machinery?.Tech?.Name) is int year ? ByNavyAndYear(null, year) : All[2];

    /// <summary>The design's outfit with every number given: its own block's, the rest its default template's.</summary>
    public static OutfitInput Of(Design design)
    {
        var basis = Default(design).Value;
        var o = design.Outfit;
        if (o is null)
            return basis;
        return new OutfitInput
        {
            Name = o.Name ?? basis.Name, Fitted = Loads(o.Fitted, basis.Fitted!), Stripped = Loads(o.Stripped, basis.Stripped!),
            PaintMjM2 = o.PaintMjM2 ?? basis.PaintMjM2, Growth = o.Growth ?? basis.Growth,
            MagazineSprinklers = o.MagazineSprinklers ?? basis.MagazineSprinklers,
        };
    }

    static FireLoadsInput Loads(FireLoadsInput? given, FireLoadsInput basis) => given is null ? basis : new FireLoadsInput
    {
        Quarters = given.Quarters ?? basis.Quarters, Cabins = given.Cabins ?? basis.Cabins, Offices = given.Offices ?? basis.Offices,
        Stores = given.Stores ?? basis.Stores, Machinery = given.Machinery ?? basis.Machinery, Deck = given.Deck ?? basis.Deck,
    };

    static readonly string[] LoadKeys = ["quarters", "cabins", "offices", "stores", "machinery", "deck"];

    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        if (design.Outfit is not { } o)
            return errs;
        errs.AddRange(o.Extra.KeysOrEmpty().Select(k => $"outfit.{k}: not an outfit setting (name, fitted, stripped, paint_mj_m2, growth, magazine_sprinklers)"));
        foreach (var (what, l) in new[] { ("fitted", o.Fitted), ("stripped", o.Stripped) })
        {
            if (l is null)
                continue;
            errs.AddRange(l.Extra.KeysOrEmpty().Select(k => $"outfit.{what}.{k}: not a kind of space ({string.Join(", ", LoadKeys)})"));
            foreach (var (k, v) in LoadKeys.Zip(new[] { l.Quarters, l.Cabins, l.Offices, l.Stores, l.Machinery, l.Deck }))
                if (v is { } x && !(x >= 0 && x <= 10_000))
                    errs.Add($"outfit.{what}.{k}: {x} MJ/m² isn't a fire load (0 to 10,000)");
        }
        if (o.PaintMjM2 is { } p && !(p >= 0 && p <= 1000))
            errs.Add($"outfit.paint_mj_m2: {p} isn't a paint load (0 to 1,000 MJ/m²)");
        if (o.Growth is { } g && !(g > 0 && g <= 10))
            errs.Add($"outfit.growth: {g} isn't a growth factor (over 0, up to 10)");
        return errs;
    }
}
