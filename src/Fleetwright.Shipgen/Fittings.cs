using System.Text.RegularExpressions;

namespace Fleetwright.Shipgen;

/// <summary>How the ship's fittings stand shock, and how its hull is joined (docs/battle/shells-armour-damage.md E0,
/// decision 10): `fittings.shock_grade` and `hull.construction`'s `shell` and `internals`. Read by the armour shock and
/// the belt's seep (E4), the blast's boundaries (E5), the near misses (E7), and later the service networks and the
/// flooding's seams. The grades' catalogue is here (the one source: `shipgen templates` writes
/// Content/Templates/fittings.json and docs/shipgen/fittings-templates.md), with the rule the format upgrade sets old
/// designs' grades by.</summary>
public static partial class Fittings
{
    /// <summary>A grade's template: its block, and who fitted to it (damage/17 service §2.2 [EST from §1.3]).</summary>
    public sealed record Template(string Name, ShockGrade Grade, string Note)
    {
        public FittingsInput Value => new() { ShockGrade = Grade.ToString() };
    }

    public static readonly Template[] All =
    [
        new("Unsecured (G0)", ShockGrade.G0, "Gear as fitted, nothing secured against shock: breakers trip at a hull shock factor of 0.07 " +
            "(half the time). Everyone before 1940, the RN to late 1940, the USN to early 1942; merchant-built and converted hulls " +
            "throughout; the IJN and Kriegsmarine throughout [UNCERTAIN: no record of their securing work]. Before about 1910 a ship has " +
            "few breakers to trip (fuses, knife switches, dynamos on their own governors)."),
        new("Secured (G1)", ShockGrade.G1, "Fuse and switch securing alterations: breakers at 0.22. The RN from late 1940 (after " +
            "Southampton), the USN from 1942. Cheap: fixes and procedures, no weight."),
        new("Shock-tested (G2)", ShockGrade.G2, "High-impact shock-tested gear on resilient mounts: breakers at 0.55 [EST]. USN new " +
            "construction and refits from 1943; RN new construction from 1943 [UNCERTAIN]. +1-2% of electrical weight [EST], not built."),
        new("Grade-A (G3)", ShockGrade.G3, "Post-war grade-A shock gear: breakers at 1.1 [EST]."),
    ];

    /// <summary>The design's grade: G0 when it gives none.</summary>
    public static ShockGrade GradeOf(Design design) =>
        TryParse(design.Fittings?.ShockGrade, out var g) ? g : ShockGrade.G0;

    /// <summary>What works the design's gunhouses, and its steering and pumps: docs/battle/electrics.md EL4.</summary>
    public static TurretDrive TurretDriveOf(Design design) =>
        SnakeEnum<TurretDrive>.TryParse(design.Fittings?.TurretDrive, out var d) ? d : TurretDrive.ElectroHydraulic;

    public static AuxiliaryDrive AuxiliariesOf(Design design) =>
        SnakeEnum<AuxiliaryDrive>.TryParse(design.Fittings?.Auxiliaries, out var d) ? d : AuxiliaryDrive.Steam;

    /// <summary>Her plant, kW (docs/battle/electrics.md EL7): the design's, else what her drives carried a tonne (research/simulation/
    /// electric-plant.md §1's groups: steam turrets and auxiliaries 17-40 W/t; electric turrets 45-90; steam turrets and
    /// electric auxiliaries, KGV and Yamato, about 70; both electric 190-260). A plant is the price of what stands on it.</summary>
    public static double PlantKw(Design design, double displacementT) => design.Fittings?.GeneratorKw ?? displacementT / 1000 *
        (TurretDriveOf(design), AuxiliariesOf(design)) switch
        {
            (TurretDrive.ElectroHydraulic or TurretDrive.Electric, AuxiliaryDrive.Electric) => 200,
            (TurretDrive.ElectroHydraulic or TurretDrive.Electric, _) => 65,
            (TurretDrive.SteamHydraulic, AuxiliaryDrive.Electric) => 70,
            (TurretDrive.SteamHydraulic, _) => 30,
            (_, AuxiliaryDrive.Electric) => 30,
            _ => 10,
        };

    public static double DieselShareOf(Design design) => Math.Clamp(design.Fittings?.DieselShare ?? 0, 0, 1);

    /// <summary>Her plant's weight, t [EST: no source gave a set's weight; diesels the heavier (Laudahn), boards and cable with them].</summary>
    public static double PlantT(Design design, double displacementT)
    {
        double diesel = DieselShareOf(design);
        return PlantKw(design, displacementT) * (diesel * DieselKgPerKw + (1 - diesel) * SteamSetKgPerKw) / 1000;
    }

    const double SteamSetKgPerKw = 70, DieselKgPerKw = 100;

    /// <summary>What share of a navy's plant in a year was diesel (electric-plant.md §1), read once by the format step, as
    /// <see cref="TurretDriveBy"/> [EST]: German capital ships a third from the Kaiser class (their plant labels read 1910), the RN's
    /// a fifth from Dreadnought, Yamato's half, Richelieu's a third; the USN's emergency sets 5% from 1941, on any ship; the rest none.</summary>
    public static double DieselShareBy(string? navy, int year, string? type, bool merchant)
    {
        bool capital = type is "BB" or "BC" or "CA";
        return (navy ?? "generic") switch
        {
            _ when merchant => 0,
            "brooklyn" or "generic" => year >= 1941 ? 0.05 : 0,
            "kiel" when capital => year >= 1910 ? 1.0 / 3 : 0,
            "portsmouth" when capital => year >= 1905 ? 0.2 : 0,
            "kure" when capital => year >= 1935 ? 0.5 : 0,
            "toulon" when capital => year >= 1935 ? 1.0 / 3 : 0,
            _ => 0,
        };
    }

    /// <summary>A grade by its name, "G0" to "G3" exactly.</summary>
    public static bool TryParse(string? s, out ShockGrade g)
    {
        g = ShockGrade.G0;
        if (s is not { Length: 2 } || s[0] != 'G' || s[1] is < '0' or > '3')
            return false;
        g = (ShockGrade)(s[1] - '0');
        return true;
    }

    /// <summary>A joint by its name, riveted or welded.</summary>
    public static bool TryParseJoint(string? s, out Joint j) => SnakeEnum<Joint>.TryParse(s, out j);

    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        if (design.Fittings is { } f)
        {
            errs.AddRange(f.Extra.KeysOrEmpty().Select(k => $"fittings.{k}: not a fittings setting (shock_grade, turret_drive, auxiliaries, generator_kw, diesel_share)"));
            if (f.TurretDrive is { } td && !SnakeEnum<TurretDrive>.TryParse(td, out _))
                errs.Add($"fittings.turret_drive: \"{td}\" isn't a drive (hand, steam_hydraulic, electro_hydraulic, electric)");
            if (f.GeneratorKw is < 0)
                errs.Add($"fittings.generator_kw: {f.GeneratorKw} is under nothing");
            if (f.DieselShare is < 0 or > 1)
                errs.Add($"fittings.diesel_share: {f.DieselShare} isn't a share (0-1)");
            if (f.Auxiliaries is { } ax && !SnakeEnum<AuxiliaryDrive>.TryParse(ax, out _))
                errs.Add($"fittings.auxiliaries: \"{ax}\" isn't a drive (steam, electric)");
            if (f.ShockGrade is { } g && !TryParse(g, out _))
                errs.Add($"fittings.shock_grade: \"{g}\" isn't a grade (G0 unsecured, G1 secured, G2 shock-tested, G3 post-war)");
        }
        if (design.Hull?.Construction is { } c)
            foreach (var (k, v) in new[] { ("shell", c.Shell), ("internals", c.Internals) })
                if (v is not null && !TryParseJoint(v, out _))
                    errs.Add($"hull.construction.{k}: \"{v}\" isn't a joint (riveted or welded)");
        return errs;
    }

    // ------------------------------------------------------------------ the format upgrade's rules (DesignFormat 3 → 4)

    /// <summary>The joints a construction's name says: "riveted shell, welded internals" both; "riveted" alone riveted
    /// throughout, "welded" (all or mostly) welded throughout; null when it says neither.</summary>
    public static (Joint Shell, Joint Internals)? JointsFromName(string? name)
    {
        if (name is null)
            return null;
        var s = name.ToLowerInvariant();
        bool riveted = s.Contains("riveted"), welded = s.Contains("welded");
        if (s.Contains("riveted shell") && s.Contains("welded internals"))
            return (Joint.Riveted, Joint.Welded);
        if (riveted && !welded)
            return (Joint.Riveted, Joint.Riveted);
        if (welded && !riveted)
            return (Joint.Welded, Joint.Welded);
        return null;
    }

    [GeneratedRegex(@"\((\d{4})\)\s*$")]
    private static partial Regex YearLabel();

    /// <summary>The year a template's name ends with ("Geared turbines, oil-fired small-tube boilers (1915)"), or null.</summary>
    public static int? YearOf(string? label) =>
        label is not null && YearLabel().Match(label) is { Success: true } m ? int.Parse(m.Groups[1].Value) : null;

    /// <summary>What drove a navy's gunhouses in a year (research/simulation/electric-plant.md §4), read once by the format
    /// step that gave designs the block; a ship reads her block, never her year. The RN on steam hydraulics to the end of the
    /// period (KGV's pumps [UNCERTAIN]); the USN (and the generic look) electric from 1898, electro-hydraulic from 1906;
    /// Germany from the dreadnoughts of 1908, whose plant labels read 1905 (before them, as the RN [EST]); France electric from 1897; Russia electric from 1895, electro-hydraulic
    /// from 1909; Italy electric [EST before the 1930s]; Japan on hydraulics, steam-driven [UNCERTAIN]; the Baltic neutrals
    /// electro-hydraulic [EST]. After the war, electro-hydraulic. A merchant hull's few guns by hand.</summary>
    public static TurretDrive TurretDriveBy(string? navy, int year, bool merchant)
    {
        if (merchant)
            return TurretDrive.Hand;
        if (year >= 1946)
            return TurretDrive.ElectroHydraulic;
        return (navy ?? "generic") switch
        {
            "portsmouth" or "kure" => TurretDrive.SteamHydraulic,
            "brooklyn" or "generic" => year >= 1906 ? TurretDrive.ElectroHydraulic : year >= 1898 ? TurretDrive.Electric : TurretDrive.SteamHydraulic,
            "kiel" => year >= 1905 ? TurretDrive.ElectroHydraulic : TurretDrive.SteamHydraulic,
            "toulon" => year >= 1897 ? TurretDrive.Electric : TurretDrive.SteamHydraulic,
            "kronstadt" => year >= 1909 ? TurretDrive.ElectroHydraulic : year >= 1895 ? TurretDrive.Electric : TurretDrive.SteamHydraulic,
            "la_spezia" => year >= 1900 ? TurretDrive.Electric : TurretDrive.SteamHydraulic,
            _ => TurretDrive.ElectroHydraulic,
        };
    }

    /// <summary>What drove a navy's steering and pumps in a year, as <see cref="TurretDriveBy"/> [EST, the research's open item
    /// 10]: the USN's pumps electric by about 1911, the rest by the 1920s (Hood's still mixed, Prince of Wales's steering
    /// electric, Audacious's auxiliaries "almost entirely steam").</summary>
    public static AuxiliaryDrive AuxiliariesBy(string? navy, int year, bool merchant) =>
        !merchant && year >= ((navy ?? "generic") is "brooklyn" or "generic" ? 1911 : 1925) ? AuxiliaryDrive.Electric : AuxiliaryDrive.Steam;

    /// <summary>The grade a navy fitted to in a year (17 service §2.2): the RN secured from late 1940 (a 1940 label
    /// and on), the USN from 1942 and shock-tested from 1943, everyone post-war (1946 on) grade A, the rest and the
    /// merchant hulls unsecured. look.navy names the navy ("portsmouth" the RN, "brooklyn" the USN); the generic look
    /// is taken as the USN's, as the shell templates took it (S1).</summary>
    public static ShockGrade ByNavyAndYear(string? navy, int year, bool merchant)
    {
        if (merchant)
            return ShockGrade.G0;
        if (year >= 1946)
            return ShockGrade.G3;
        return (navy ?? "generic") switch
        {
            "portsmouth" when year >= 1940 => ShockGrade.G1,
            "brooklyn" or "generic" when year >= 1943 => ShockGrade.G2,
            "brooklyn" or "generic" when year >= 1942 => ShockGrade.G1,
            _ => ShockGrade.G0,
        };
    }
}
