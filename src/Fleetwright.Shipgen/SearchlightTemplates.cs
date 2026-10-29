namespace Fleetwright.Shipgen;

/// <summary>A design's searchlights (docs/battle/night.md Q3, Q3b): how many, their mirrors, the arc that lights them, the
/// beam's spread and how they're trained. The templates are labels on the numbers by navy and period, from
/// research/simulation/night-fighting.md §1 where it found them ([SRC] in the notes) and estimates where it didn't
/// ([EST]); a design carries the block, and shipgen sizes and sites
/// the lights from it (Layout.PlaceSearchlights): a capital ship's count and mirror, scaled down to a torpedo craft's by
/// her waterplane. The photometry, weight, power and crew of one light are here too, the one place they live.</summary>
public static class SearchlightTemplates
{
    /// <summary>A template: its block, and who it fits.</summary>
    public sealed record Template(string Name, SearchlightsInput Value, string Note);

    static Template T(string name, int capital, double mirror, double small, double lamp, double spread, string control, string note) =>
        new(name, new SearchlightsInput
        {
            Name = name, CapitalCount = capital, MirrorCm = mirror, SmallMirrorCm = small, LampCdCm2 = lamp, SpreadDeg = spread, Control = control,
        }, note);

    public static readonly Template[] All =
    [
        T("Victorian (1890)", 6, 90, 40, 16000, 3.0, "local",
            "90 cm Mangin or Schuckert projectors on the continental battleships, not less than six on a large ship (1894), " +
            "plain carbon arcs (a crater of 1.6-1.8e4 cd/cm2), a 3 deg beam, trained at the light, to search for torpedo boats " +
            "[SRC]; the boats a small one [EST]."),
        T("Pre-dreadnought (1904)", 6, 75, 40, 16000, 3.0, "remote",
            "Russia's six 75 cm Mangin lights in pairs (the fore bridge, amidships, the after bridge or the mainmast) with " +
            "electric remote control, 40 cm on the boats, 1898-1905 [SRC]; the other navies' of the period by it [EST]."),
        T("RN pre-dreadnought (1904)", 6, 61, 51, 16000, 3.0, "local",
            "24-inch on the big ships (landed from the tops to the shelter deck in 1906), 20-inch on the destroyers, each " +
            "trained at the light [SRC]; the count [EST]."),
        T("Great War (1914)", 8, 91, 61, 16000, 3.0, "local",
            "36-inch plain arcs on the big ships (searchlight towers by the funnels, the bridge's wings; the RN's from mid-1915), " +
            "24-inch on the destroyers, each light trained by its own crew to orders by voice pipe ('never really efficient', " +
            "the RN at Jutland) [SRC]; the count [EST]."),
        T("RN post-Jutland (1918)", 8, 91, 61, 16000, 3.0, "remote",
            "As in 1916, now four 36-inch over a searchlight control position with directing sights (August 1917) [SRC]."),
        T("German Great War (1914)", 8, 110, 60, 16000, 3.0, "remote",
            "Plain arcs kept (the navy rejected Beck's high-intensity arc: worse in the Baltic mist), iris shutters, the lights " +
            "trained dark from a control position before they were switched on: the lights that found the 4th Flotilla at " +
            "Jutland, 'much more powerful than ours' (Jellicoe) [SRC]; the 110 cm and the count [EST]."),
        T("USN high-intensity (1918)", 8, 91, 61, 60000, 1.8, "remote",
            "36-inch high-intensity arcs (Beck in Texas 1914, the 1917 specification's 5e4 cd/cm2 and more, Sperry's 9e4), " +
            "eight on a battleship, distant electric control, tied into fire control by 1918; the beam's spread about 2/3 " +
            "the plain arc's [SRC/INFERRED]."),
        T("Interwar (1930)", 6, 110, 61, 60000, 1.4, "remote",
            "High-intensity arcs everywhere, fewer and bigger lights, trained from control positions [EST]."),
        T("Second War (1940)", 6, 150, 61, 75000, 1.2, "director",
            "150 cm (44-inch in the RN) high-intensity lights slaved to the fire-control directors [EST]."),
        T("RN Victorian (1890)", 6, 61, 40, 16000, 3.0, "local",
            "24-inch projectors with plain carbon arcs on the bridges, the fighting tops and the flying deck, to search for " +
            "torpedo boats [SRC]; the count and the boats' [EST]."),
        T("RN dreadnought (1910)", 8, 61, 51, 16000, 3.0, "local",
            "Twin 24-inch mountings (St Vincent, Colossus, Iron Duke), not 36-inch, hand-worked to orders by voice pipe, " +
            "until the 36-inch lights of mid-1915 [SRC]; the count [EST]."),
    ];

    public static Template? Get(string name) => All.FirstOrDefault(t => t.Name == name);

    /// <summary>The searchlights a navy fitted in a year (night-fighting.md §1.2): Victorian before 1898 (the RN's 24-inch),
    /// the pre-dreadnoughts' to 1909 (the RN's 24-inch), the Great War's to 1919 (the RN's twin 24-inch to 1915 and its
    /// 36-inch from 1916, controlled from 1917; the German navy's own; the USN's high-intensity arcs from 1915), interwar to
    /// 1937, then the Second War's. look.navy names the navy ("portsmouth" the RN, "kiel" the German, "brooklyn" the USN).</summary>
    public static Template ByNavyAndYear(string? navy, int year) => year switch
    {
        < 1898 => navy == "portsmouth" ? All[9] : All[0],
        < 1910 => navy == "portsmouth" ? All[2] : All[1],
        < 1920 => navy switch
        {
            "kiel" => All[5],
            "portsmouth" => year < 1916 ? All[10] : year < 1917 ? All[3] : All[4],
            "brooklyn" when year >= 1915 => All[6],
            _ => All[3],
        },
        < 1938 => All[7],
        _ => All[8],
    };

    /// <summary>The template a design falls back on: its plant's year's (no navy: the look is the drawing's only), else
    /// the Great War's. Format 14's upgrade read the look's navy into each design's block.</summary>
    public static Template Default(Design design) =>
        Fittings.YearOf(design.Machinery?.Tech?.Name) is int year ? ByNavyAndYear(null, year) : All[3];

    /// <summary>The design's searchlights with every number given: its own block's, the rest its default template's
    /// (count stays null unless the design gives it: then her size sets it).</summary>
    public static SearchlightsInput Of(Design design)
    {
        var basis = Default(design).Value;
        var s = design.Searchlights;
        if (s is null)
            return basis;
        return new SearchlightsInput
        {
            Name = s.Name ?? basis.Name, Count = s.Count, CapitalCount = s.CapitalCount ?? basis.CapitalCount,
            MirrorCm = s.MirrorCm ?? basis.MirrorCm, SmallMirrorCm = s.SmallMirrorCm ?? basis.SmallMirrorCm,
            LampCdCm2 = s.LampCdCm2 ?? basis.LampCdCm2, SpreadDeg = s.SpreadDeg ?? basis.SpreadDeg, Control = s.Control ?? basis.Control,
        };
    }

    public static readonly string[] Controls = ["local", "remote", "director"];

    /// <summary>A capital ship's waterplane and a torpedo craft's, m² (L × B), the ends of the sizing.</summary>
    public const double CapitalM2 = 4900, SmallM2 = 600;

    /// <summary>How many lights a ship L × B carries: the block's count, else the capital count in proportion to her
    /// waterplane up to a capital ship's (the deck she has to site them on: a destroyer one or two of a battleship's 8, a
    /// light cruiser three) and by its 0.25 power beyond (the arcs are covered: a bigger ship takes bigger lights, not
    /// many more), at least one.</summary>
    public static int CountFor(SearchlightsInput s, double L, double B)
    {
        if (s.Count is { } given)
            return given;
        if (s.CapitalCount is not (> 0 and var n))
            return 0;
        double a = L * B / CapitalM2;
        return Math.Max(1, (int)Math.Round(n * (a < 1 ? a : Math.Pow(a, 0.25))));
    }

    /// <summary>Her lights' mirror, m: the small mirror at a torpedo craft's waterplane and under, the capital one at a
    /// capital ship's and over, between by the logarithm of the waterplane.</summary>
    public static double MirrorM(SearchlightsInput s, double L, double B)
    {
        double t = Math.Clamp(Math.Log(L * B / SmallM2) / Math.Log(CapitalM2 / SmallM2), 0.0, 1.0);
        double small = s.SmallMirrorCm ?? s.MirrorCm ?? 61, big = s.MirrorCm ?? 61;
        return (small + t * (big - small)) / 100;
    }

    /// <summary>The beam's peak intensity, cd: the crater's luminance over the mirror's face (0.9 of it clear of the lamp's
    /// shadow) at a silvered glass's 0.85 [EST]. A 36-inch plain arc 8·10⁷, high-intensity 3·10⁸; a 150 cm high-intensity
    /// arc 10⁹.</summary>
    public static double BeamCd(double mirrorM, double lampCdCm2) => lampCdCm2 * 1e4 * Math.PI * mirrorM * mirrorM / 4 * 0.9 * 0.85;

    /// <summary>One light's weight with its pedestal and platform, t: 1.2 for a 36-inch, as its mirror's area [EST].</summary>
    public static double WeightT(double mirrorM) => 1.2 * Math.Pow(mirrorM / 0.91, 2);

    /// <summary>The arc's draw, kW: 6.5 for a 36-inch plain arc (100 A at 65 V), as its mirror to the 1.5 and its crater's
    /// brightness to the 0.35 (a high-intensity arc about 150 A at 75 V) [EST].</summary>
    public static double PowerKw(double mirrorM, double lampCdCm2) => 6.5 * Math.Pow(mirrorM / 0.91, 1.5) * Math.Pow(lampCdCm2 / 16000, 0.35);

    /// <summary>Men at one light: three at a 36-inch (the operator, his mate at the shutter, a man at the carbons), two at a
    /// small one [EST].</summary>
    public static long Crew(double mirrorM) => Math.Max(1, (long)Math.Round(1 + 2 * mirrorM / 0.91));

    /// <summary>The light's hitbox: its drum and platform, m (a 36-inch 0.8 round and 1.6 high).</summary>
    public static double HousingR(double mirrorM) => 0.5 * mirrorM + 0.35;
    public static double HousingH(double mirrorM) => mirrorM + 0.7;

    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        if (design.Searchlights is not { } s)
            return errs;
        errs.AddRange(s.Extra.KeysOrEmpty().Select(k =>
            $"searchlights.{k}: not a searchlights setting (name, count, capital_count, mirror_cm, small_mirror_cm, lamp_cd_cm2, spread_deg, control)"));
        if (s.Count is { } n && !(n >= 0 && n <= 40))
            errs.Add($"searchlights.count: {n} isn't a number of lights (0 to 40)");
        if (s.CapitalCount is { } c && !(c >= 0 && c <= 40))
            errs.Add($"searchlights.capital_count: {c} isn't a number of lights (0 to 40)");
        foreach (var (k, v) in new[] { ("mirror_cm", s.MirrorCm), ("small_mirror_cm", s.SmallMirrorCm) })
            if (v is { } m && !(m >= 20 && m <= 250))
                errs.Add($"searchlights.{k}: {m} isn't a mirror (20 to 250 cm)");
        if (s.LampCdCm2 is { } l && !(l >= 1000 && l <= 200000))
            errs.Add($"searchlights.lamp_cd_cm2: {l} isn't an arc's brightness (1,000 to 200,000 cd/cm²)");
        if (s.SpreadDeg is { } d && !(d >= 0.3 && d <= 20))
            errs.Add($"searchlights.spread_deg: {d} isn't a beam's spread (0.3 to 20°)");
        if (s.Control is { } k2 && !Controls.Contains(k2))
            errs.Add($"searchlights.control: \"{k2}\" isn't a control (local, remote, director)");
        return errs;
    }
}
