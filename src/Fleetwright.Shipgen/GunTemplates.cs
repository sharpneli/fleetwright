namespace Fleetwright.Shipgen;

/// <summary>The gun catalogue: blocks of how a period's guns are worked, laid and charged, as shares of the sim's
/// placeholder rules (DesignerPlaceholders.Gun, Charge and Pattern give the rules and their sources). The one source of
/// `Content/Templates/guns.json` and docs/shipgen/gun-templates.md (`shipgen templates` writes both). A battery names its
/// block (`main[i].gun`, `secondary[i].gun`); a design copies the blocks it names into `gun_tech`, and a name the design
/// doesn't carry takes the catalogue's. The look never sets them: it's the drawing's only (the user, 2026-10-19).
/// <list type="bullet">
/// <item>Reload: the rule's 7·(d/102 mm)^1.5 s (at most 36) times the share: 1.1 in the Great War, 0.9 in the Second,
/// 0.5 for the Cold War's automatic guns; before 1906 a heavy gun is slower, growing from 150 mm to its slowing at 300 mm:
/// ×1.3, ×2.5 under 40 calibres and ×4 under 30 (12"/35 70-100 s).</item>
/// <item>Training: the rule's 2·(381 mm/d)^1.5 °/s times the share (0.7 before 1906, 1.2 between the wars, 1.5 in the
/// Second, 3 in the Cold War), ×1.6 more on a dual-purpose mount from the treaty years (5"/38 25).</item>
/// <item>The highest elevation: 13° before 1906, 18° in the Great War, 30° between the wars, 40° in the Second, 45°
/// after and on a dual-purpose mount from the treaty years.</item>
/// <item>The pattern: before 1906 3.5%; the Great War's heavy guns 3.3·(305/d)³, its lighter ones 2.5; from the treaty
/// years 1.9 and 1.8; the Cold War's 1.5.</item>
/// <item>The propellant: double-base cordite and its kin, single-base for the USN, France and Russia, diglycol for
/// Germany from the treaty years, triple-base in the Cold War; black powder under 36 calibres before 1906.</item>
/// <item>The packaging (docs/battle/magazines.md decision 1; research/simulation/damage-research/10 §1.3): double-base
/// in silk bags in Clarkson cases (the RN's, and Japan's on its pattern), or a brass cartridge case with the fore charge
/// in a tin (the German blocks, "brass-cased"); single-base in airtight tanks (the USN's); diglycol and triple-base in
/// brass. How readily a burning charge lights the next in its package decides the venting race; it costs in the
/// campaign as any design choice does.</item>
/// </list></summary>
public static class GunTemplates
{
    /// <summary>A block: its name, its period (the group), sources and value.</summary>
    public sealed record Template(string Name, string Group, string Note, GunTechInput Value);

    /// <summary>The propellants a block may give (the sim's Propellants by these names).</summary>
    public static readonly string[] Propellants = ["black powder", "single-base", "double-base", "diglycol", "triple-base", "lova"];

    /// <summary>How a block's charges are packed (the sim's Packaging by these names): silk bags alone, bags in Clarkson
    /// cases, a brass cartridge case, bags in airtight tanks.</summary>
    public static readonly string[] Packagings = ["bag", "clarkson", "brass", "tank"];

    /// <summary>A propellant's usual packing, the default for a block that doesn't say (design format 13).</summary>
    public static string PackagingOf(string? propellant) => propellant switch
    {
        "black powder" => "bag",
        "double-base" => "clarkson",
        "single-base" => "tank",
        _ => "brass",
    };

    /// <summary>A period's numbers, before the propellant.</summary>
    sealed record Period(string Name, double Reload, double Slow, double Slow40, double Slow30, double Train, double DpTrain, double MaxElev,
        double DpMaxElev, double Pattern, double PatternExp, double PatternLight, double BlackUnder, string Note);

    static readonly Period[] Periods =
    [
        new("Victorian", 1, 1.3, 2.5, 4, 0.7, 1, 13, 13, 3.5, 0, 3.5, 36,
            "Before 1906: heavy guns slow to load (12\"/35 70-100 s), hand-worked mounts, 13° elevation, the brown-powder short guns."),
        new("Great War", 1.1, 1, 1, 1, 1, 1, 18, 18, 3.3, 3, 2.5, 0,
            "1906-1921: RN 13.5\" and 15\" 20°, German 13.5-16°; the 12\" 3.3-3.5%, the 15\" 1.7 (\"the most accurate battleship gun\")."),
        new("Treaty", 1, 1, 1, 1, 1.2, 1.6, 30, 45, 1.9, 0, 1.8, 0,
            "1922-1936: delay coils and stiffer mounts (16\" 1.9%, 8\" 2.0); dual-purpose mounts at 45°."),
        new("Second War", 0.9, 1, 1, 1, 1.5, 1.6, 40, 45, 1.9, 0, 1.8, 0,
            "1937-1949: USN 16\" training 4 °/s, 38 cm 5; the 5\"/38 25 and 1.7%."),
        new("Cold War", 0.5, 1, 1, 1, 3, 1.6, 45, 45, 1.5, 0, 1.5, 0,
            "From 1950: automatic guns, power-worked mounts."),
    ];

    static Template T(Period p, string propellant, string note, string? packaging = null) => new(
        $"{p.Name}, {propellant}" + (packaging is null ? "" : $", {packaging}-cased"), p.Name, p.Note + " " + note, new GunTechInput
    {
        ReloadShare = p.Reload, HeavySlow = p.Slow, HeavySlowUnder40Cal = p.Slow40, HeavySlowUnder30Cal = p.Slow30, TrainShare = p.Train,
        DpTrainShare = p.DpTrain, MaxElevationDeg = p.MaxElev, DpMaxElevationDeg = p.DpMaxElev, PatternPct = p.Pattern, PatternExp = p.PatternExp,
        PatternLightPct = p.PatternLight, Propellant = propellant, BlackPowderUnderCal = p.BlackUnder,
        Packaging = packaging ?? PackagingOf(propellant),
    });

    const string Brass = "The main charge in a brass cartridge case, the fore charge in silk in a tin (Germany: RP C/12; " +
        "Seydlitz's 62 charges burned at Dogger Bank and her magazines held).";

    const string Cordite = "Double-base: cordite MD and SC, the German RPC/12, Japan's and Italy's.";
    const string Single = "Single-base nitrocellulose: the USN's SPD, the French poudre B, the Russian pyroxylin.";
    const string Diglycol = "Diglycol, cool double-base: the German RPC/38.";

    public static readonly IReadOnlyList<Template> All =
    [
        T(Periods[0], "double-base", Cordite),
        T(Periods[0], "double-base", Brass, "brass"),
        T(Periods[0], "single-base", Single),
        T(Periods[1], "double-base", Cordite),
        T(Periods[1], "double-base", Brass, "brass"),
        T(Periods[1], "single-base", Single),
        T(Periods[2], "double-base", Cordite),
        T(Periods[2], "double-base", Brass, "brass"),
        T(Periods[2], "single-base", Single),
        T(Periods[2], "diglycol", Diglycol),
        T(Periods[3], "double-base", Cordite),
        T(Periods[3], "double-base", Brass, "brass"),
        T(Periods[3], "single-base", Single),
        T(Periods[3], "diglycol", Diglycol),
        T(Periods[4], "triple-base", "Triple-base: Cordite N/NQ, SPCG, M30/M31."),
    ];

    static readonly Dictionary<string, Template> byName = All.ToDictionary(t => t.Name);

    /// <summary>The block a battery that names none fires with.</summary>
    public const string Default = "Second War, double-base";

    public static Template? Get(string name) => byName.GetValueOrDefault(name);

    /// <summary>A name's block: the design's (gun_tech), else the catalogue's, else null.</summary>
    public static GunTechInput? Resolve(Design design, string? name) =>
        design.GunTech?.GetValueOrDefault(name ?? Default) ?? Get(name ?? Default)?.Value;

    /// <summary>The block the look's era and navy stood for before design format 9 (DesignFormat's upgrade only: the look
    /// is the drawing's now).</summary>
    public static string ForLook(string? era, string? navy)
    {
        string period = era switch
        {
            "victorian" => "Victorian", "great_war" => "Great War", "treaty" => "Treaty", "cold_war" => "Cold War", _ => "Second War",
        };
        string propellant = period == "Cold War" ? "triple-base"
            : navy is "brooklyn" or "toulon" or "kronstadt" ? "single-base"
            : navy == "kiel" && period is "Treaty" or "Second War" ? "diglycol"
            : "double-base";
        return $"{period}, {propellant}";
    }

    static readonly string[] Fields =
    [
        "reload_share", "heavy_slow", "heavy_slow_under_40_cal", "heavy_slow_under_30_cal", "train_share", "dp_train_share", "max_elevation_deg",
        "dp_max_elevation_deg", "pattern_pct", "pattern_exp", "pattern_light_pct", "propellant", "black_powder_under_cal", "packaging",
    ];

    /// <summary>The design's gun blocks and the names its batteries give: every field given, the shares and elevations
    /// over 0, the propellant one of <see cref="Propellants"/>; each name in gun_tech or the catalogue.</summary>
    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        foreach (var (name, b) in design.GunTech ?? [])
        {
            string at = $"gun_tech.{Style.Quote(name)}";
            foreach (var k in b.Extra.KeysOrEmpty())
                errs.Add($"{at}.{k}: not a gun field ({string.Join(", ", Fields)})");
            foreach (var (k, v) in new (string, double?)[]
                     {
                         ("reload_share", b.ReloadShare), ("heavy_slow", b.HeavySlow), ("heavy_slow_under_40_cal", b.HeavySlowUnder40Cal),
                         ("heavy_slow_under_30_cal", b.HeavySlowUnder30Cal), ("train_share", b.TrainShare), ("dp_train_share", b.DpTrainShare),
                         ("max_elevation_deg", b.MaxElevationDeg), ("dp_max_elevation_deg", b.DpMaxElevationDeg), ("pattern_pct", b.PatternPct),
                         ("pattern_light_pct", b.PatternLightPct),
                     })
                if (v is not > 0)
                    errs.Add($"{at}.{k}: give it, more than 0");
            foreach (var (k, v) in new (string, double?)[] { ("pattern_exp", b.PatternExp), ("black_powder_under_cal", b.BlackPowderUnderCal) })
                if (v is not >= 0)
                    errs.Add($"{at}.{k}: give it, 0 or more");
            if (!Propellants.Contains(b.Propellant))
                errs.Add($"{at}.propellant = {Style.Quote(b.Propellant ?? "")}: use {string.Join(", ", Propellants)}");
            if (!Packagings.Contains(b.Packaging))
                errs.Add($"{at}.packaging = {Style.Quote(b.Packaging ?? "")}: use {string.Join(", ", Packagings)}");
        }
        foreach (var (list, key) in new[] { (design.Main, "main"), (design.Secondary, "secondary") })
            for (int i = 0; i < (list?.Count ?? 0); i++)
                if (list![i].Gun is { } g && Resolve(design, g) is null)
                    errs.Add($"{key}[{i}].gun: no block {Style.Quote(g)} in gun_tech or the catalogue");
        return errs;
    }
}

/// <summary>The torpedo catalogue: a period's torpedo as a 21"'s settings, its warhead's TNT share, its launchers' gear
/// and its reliability (DesignerPlaceholders.Torpedo gives the rule and the sources; the gyro, failure and dud shares were
/// the sim's settings by era, docs/battle/fighting.md 4.6). The one source of `Content/Templates/torpedoes.json` and
/// docs/shipgen/torpedo-templates.md. `torpedoes.torpedo` names the block; a design copies it into `torpedo_tech`, and a
/// name the design doesn't carry takes the catalogue's. The look never sets it.</summary>
public static class TorpedoTemplates
{
    public sealed record Template(string Name, string Note, TorpedoTechInput Value);

    static Template T(string name, double fastKn, double fastM, double slowKn, double slowM, double share, double train, double mountReload, double gyro,
        double fail, double dud, string note) =>
        new(name, note, new TorpedoTechInput
        {
            FastKn = fastKn, FastRangeM = fastM, SlowKn = slowKn, SlowRangeM = slowM, ChargeShare = share, MountTrainDegS = train,
            MountReloadS = mountReload, TubeReloadS = 180, GyroDeg = gyro, Fail = fail, Dud = dud,
        });

    public static readonly IReadOnlyList<Template> All =
    [
        T("Victorian torpedo", 30, 1_000, 24, 2_700, 0.45, 3, 600, 5, 0.25, 0.15,
            "Before 1906: the 1890s' Whiteheads, wet guncotton; no gyro angling to speak of."),
        T("Great War torpedo", 41, 3_900, 28, 9_200, 0.6, 3, 600, 20, 0.12, 0.1,
            "1906-1921: the RN's 21\" Mk II 45 kn for 3,840 m or 29 kn for 9,100; the German G/7 37 kn for 4,000 m or 27 kn for " +
            "9,300 and its 195 kg of hexanite."),
        T("Treaty torpedo", 43, 5_000, 30, 11_000, 0.85, 3, 600, 90, 0.1, 0.08, "1922-1936."),
        T("Second War torpedo", 45, 6_000, 32, 12_500, 1, 6, 300, 90, 0.08, 0.1,
            "1937-1949: Mk 15 45 kn for 5,500 m, G7a 44 for 5,000; power-trained mounts, the IJN's reload gear. Oxygen " +
            "torpedoes (the Type 93's 20 km at 48 kn) aren't told apart."),
        T("Cold War torpedo", 45, 9_000, 35, 18_000, 1, 6, 300, 90, 0.05, 0.05, "From 1950."),
    ];

    static readonly Dictionary<string, Template> byName = All.ToDictionary(t => t.Name);

    /// <summary>The block torpedoes that name none are.</summary>
    public const string Default = "Second War torpedo";

    public static Template? Get(string name) => byName.GetValueOrDefault(name);

    /// <summary>A name's block: the design's (torpedo_tech), else the catalogue's, else null.</summary>
    public static TorpedoTechInput? Resolve(Design design, string? name) =>
        design.TorpedoTech?.GetValueOrDefault(name ?? Default) ?? Get(name ?? Default)?.Value;

    /// <summary>The block the look's era stood for before design format 9 (DesignFormat's upgrade only).</summary>
    public static string ForLook(string? era) => era switch
    {
        "victorian" => "Victorian torpedo", "great_war" => "Great War torpedo", "treaty" => "Treaty torpedo", "cold_war" => "Cold War torpedo",
        _ => "Second War torpedo",
    };

    static readonly string[] Fields =
        ["fast_kn", "fast_range_m", "slow_kn", "slow_range_m", "charge_share", "mount_train_deg_s", "mount_reload_s", "tube_reload_s", "gyro_deg", "fail", "dud"];

    /// <summary>The design's torpedo blocks and the name its torpedoes give: every field given, the settings and gear over
    /// 0, the shares within 0-1; the name in torpedo_tech or the catalogue.</summary>
    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        foreach (var (name, b) in design.TorpedoTech ?? [])
        {
            string at = $"torpedo_tech.{Style.Quote(name)}";
            foreach (var k in b.Extra.KeysOrEmpty())
                errs.Add($"{at}.{k}: not a torpedo field ({string.Join(", ", Fields)})");
            foreach (var (k, v) in new (string, double?)[]
                     {
                         ("fast_kn", b.FastKn), ("fast_range_m", b.FastRangeM), ("slow_kn", b.SlowKn), ("slow_range_m", b.SlowRangeM),
                         ("mount_train_deg_s", b.MountTrainDegS), ("mount_reload_s", b.MountReloadS), ("tube_reload_s", b.TubeReloadS), ("gyro_deg", b.GyroDeg),
                     })
                if (v is not > 0)
                    errs.Add($"{at}.{k}: give it, more than 0");
            foreach (var (k, v) in new (string, double?)[] { ("charge_share", b.ChargeShare), ("fail", b.Fail), ("dud", b.Dud) })
                if (v is not (>= 0 and <= 1))
                    errs.Add($"{at}.{k}: give it, a share from 0 to 1");
        }
        if (design.Torpedoes?.Torpedo is { } n && Resolve(design, n) is null)
            errs.Add($"torpedoes.torpedo: no block {Style.Quote(n)} in torpedo_tech or the catalogue");
        return errs;
    }
}
