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
            errs.AddRange(f.Extra.KeysOrEmpty().Select(k => $"fittings.{k}: not a fittings setting (shock_grade)"));
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
