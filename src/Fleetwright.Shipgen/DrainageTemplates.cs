namespace Fleetwright.Shipgen;

/// <summary>A design's drainage (docs/battle/damage-control.md DC4): what's built in against water, as numbers. The fixed
/// drainage pumps (on the ship's power), the portable pumps carried (petrol, steam or hand: no power), how fast her
/// counterflooding valves let water into the tanks on the high side (DC5), and how tight her intact boundaries are (×1
/// air-tested … ×25 unsealed: their glands, stuffing tubes and gaskets). The templates are labels on the numbers by navy
/// and period (09 §5, 10 §2.4) [EST]; a design carries the block, and the sim reads only its numbers. No weight yet:
/// the pumps are in the machinery's.</summary>
public static class DrainageTemplates
{
    /// <summary>A template: its block, and who it fits.</summary>
    public sealed record Template(string Name, DrainageInput Value, string Note);

    static Template T(string name, double pumps, int portable, double portableTph, double counterflood, double tightness, string note) =>
        new(name, new DrainageInput
        {
            Name = name, PumpsTphPer1000T = pumps, PortablePumps = portable, PortableTph = portableTph, CounterfloodTpmPer10000T = counterflood,
            Tightness = tightness,
        }, note);

    public static readonly Template[] All =
    [
        T("Victorian (1890)", 30, 4, 5, 0, 3,
            "Steam drainage pumps and hand pumps; no counterflooding system (the sea valves are for the magazines); " +
            "bulkheads never air-tested, many pipes and cables through them [EST]."),
        T("Pre-dreadnought (1905)", 40, 4, 8, 8, 2,
            "Main drainage with steam ejectors; Makarov's and Krylov's counterflooding tables, a few tanks flooded by hand " +
            "valves (Orel, Retvizan) [EST]."),
        T("RN Great War (1914)", 50, 4, 15, 10, 1.5,
            "Main drainage on the dynamos; counterflooding by hand at the valves, little organised for it (no unified damage " +
            "control until 1943) [EST]."),
        T("German Great War (1914)", 50, 4, 15, 20, 1,
            "Close subdivision, good closure discipline and systematic counterflooding (Seydlitz, Lützow trimmed by it) [EST]."),
        T("Interwar (1939)", 60, 6, 30, 18, 1,
            "Electric drainage, submersibles; remote counterflood valves (North Carolina: ~80 t/min on 45,000 t) [EST from WDR 61]."),
        T("USN 1943", 70, 8, 60, 20, 1,
            "Wartime lessons: handy-billies (14 t/h) and P-500s (115 t/h) standard, emergency diesels, air-tested compartments " +
            "(09 §5.2) [EST]."),
    ];

    public static Template? Get(string name) => All.FirstOrDefault(t => t.Name == name);

    /// <summary>The drainage a navy built in a year [EST]: Victorian before 1900, pre-dreadnought to 1909, the Great War's to
    /// 1918 (the German navy's own), interwar to 1942, from 1943 the USN's (and the generic look's) wartime fit.</summary>
    public static Template ByNavyAndYear(string? navy, int year) => year switch
    {
        < 1900 => All[0],
        < 1910 => All[1],
        < 1919 => navy == "kiel" ? All[3] : All[2],
        < 1943 => All[4],
        _ => navy is "brooklyn" or "generic" or null ? All[5] : All[4],
    };

    /// <summary>The template a design falls back on: its plant's year's (no navy: the look is the drawing's only), else
    /// the RN Great War's. Format 11's upgrade read the look's navy into each design's block.</summary>
    public static Template Default(Design design) =>
        Fittings.YearOf(design.Machinery?.Tech?.Name) is int year ? ByNavyAndYear(null, year) : All[2];

    /// <summary>The design's drainage with every number given: its own block's, the rest its default template's.</summary>
    public static DrainageInput Of(Design design)
    {
        var basis = Default(design).Value;
        var d = design.Drainage;
        if (d is null)
            return basis;
        return new DrainageInput
        {
            Name = d.Name ?? basis.Name, PumpsTphPer1000T = d.PumpsTphPer1000T ?? basis.PumpsTphPer1000T,
            PortablePumps = d.PortablePumps ?? basis.PortablePumps, PortableTph = d.PortableTph ?? basis.PortableTph,
            CounterfloodTpmPer10000T = d.CounterfloodTpmPer10000T ?? basis.CounterfloodTpmPer10000T, Tightness = d.Tightness ?? basis.Tightness,
        };
    }

    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        if (design.Drainage is not { } d)
            return errs;
        errs.AddRange(d.Extra.KeysOrEmpty().Select(k =>
            $"drainage.{k}: not a drainage setting (name, pumps_tph_per_1000t, portable_pumps, portable_tph, counterflood_tpm_per_10000t, tightness)"));
        if (d.PumpsTphPer1000T is { } p && !(p >= 0 && p <= 1000))
            errs.Add($"drainage.pumps_tph_per_1000t: {p} isn't a pumping rate (0 to 1,000 t/h per 1,000 t)");
        if (d.PortablePumps is { } n && !(n >= 0 && n <= 100))
            errs.Add($"drainage.portable_pumps: {n} isn't a number of pumps (0 to 100)");
        if (d.PortableTph is { } q && !(q >= 0 && q <= 1000))
            errs.Add($"drainage.portable_tph: {q} isn't a portable pump's rate (0 to 1,000 t/h)");
        if (d.CounterfloodTpmPer10000T is { } c && !(c >= 0 && c <= 1000))
            errs.Add($"drainage.counterflood_tpm_per_10000t: {c} isn't a counterflooding rate (0 to 1,000 t/min per 10,000 t)");
        if (d.Tightness is { } t && !(t >= 1 && t <= 100))
            errs.Add($"drainage.tightness: {t} isn't a tightness (1 air-tested to 100)");
        return errs;
    }
}
