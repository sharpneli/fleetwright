using System.Globalization;
using Fleetwright.Shipgen;

namespace Fleetwright.Designer;

/// <summary>The shell picker's edits (docs/designer-ui/shells.md; docs/battle/shells-armour-damage.md S4), each a new
/// design: a battery's loadout (its <c>shells</c>, the blocks they name copied into the design's <c>ammunition</c> from
/// the templates, as plant templates are copied), and the families' order for picking. A battery without shells
/// carries the default loadout; editing it writes that loadout out first. Blocks no battery names are dropped.</summary>
static class ShellEdits
{
    public static readonly string[] Kinds = ["ap", "common", "he"];
    public static string KindLabel(string kind) => kind switch { "ap" => "AP", "common" => "common", _ => "HE" };

    /// <summary>A gun under this is a light gun: it takes its family's light block where there is one (the templates'
    /// own line, ShellTemplates' Light; the rule `shipgen ammo-templates` switched the designs by).</summary>
    const double LightGunMm = 200;

    static List<BatteryInput>? List(Design d, bool main) => main ? d.Main : d.Secondary;

    /// <summary>A battery's loadout as entries: its own, or the default loadout's (its blocks then come from the catalogue).</summary>
    public static List<ShellEntryInput> Loadout(Design d, bool main, int k) =>
        List(d, main)![k].Shells is { Count: > 0 } own ? [.. own]
            : [.. ShellTemplates.DefaultLoadout(main).Select(o => new ShellEntryInput { Ammo = o.Name, Share = o.Share })];

    /// <summary>The template a block is (by its name), or null for one of the design's own.</summary>
    public static ShellTemplates.Template? TemplateOf(string? ammo) => ammo is null ? null : ShellTemplates.Get(ammo);

    /// <summary>The family of a battery's loadout: its blocks' one family, "mixed", or "custom" for blocks of the design's
    /// own.</summary>
    public static string FamilyOf(Design d, bool main, int k)
    {
        var fams = Loadout(d, main, k).Select(e => TemplateOf(e.Ammo)?.Group ?? "custom").Distinct().ToList();
        return fams.Count == 1 ? fams[0] : "mixed";
    }

    /// <summary>Every gun battery's family: the one they share, else "mixed".</summary>
    public static string FamilyOfAll(Design d)
    {
        var fams = new HashSet<string>();
        foreach (var main in new[] { true, false })
            for (int k = 0; k < (List(d, main)?.Count ?? 0); k++)
                if (List(d, main)![k].CalibreMm != null)
                    fams.Add(FamilyOf(d, main, k));
        return fams.Count == 1 ? fams.First() : fams.Count == 0 ? "" : "mixed";
    }

    /// <summary>A family's block of a kind for a gun: its light block under 200 mm where it has one, else its heavy one;
    /// null when it has none of the kind.</summary>
    public static ShellTemplates.Template? BlockOf(string family, string kind, double calibreMm)
    {
        var ts = ShellTemplates.All.Where(t => t.Group == family && t.Value.Kind == kind).ToList();
        return (calibreMm < LightGunMm ? ts.FirstOrDefault(t => t.Light) : null) ?? ts.FirstOrDefault(t => !t.Light) ?? ts.FirstOrDefault();
    }

    /// <summary>The design with a battery's loadout set, the templates it names copied in, and blocks nobody names dropped.</summary>
    static Design With(Design d, bool main, int k, List<ShellEntryInput>? shells)
    {
        var list = List(d, main)!.ToList();
        list[k] = list[k] with { Shells = shells };
        d = main ? d with { Main = list } : d with { Secondary = list };
        var table = new Dictionary<string, AmmoInput>(d.Ammunition ?? [], StringComparer.Ordinal);
        foreach (var e in shells ?? [])
            if (TemplateOf(e.Ammo) is { } t)
                table[e.Ammo!] = t.Value;
        var named = (d.Main ?? []).Concat(d.Secondary ?? []).SelectMany(b => b.Shells ?? []).Select(e => e.Ammo).ToHashSet();
        foreach (var name in table.Keys.ToList())
            if (!named.Contains(name))
                table.Remove(name);
        return d with { Ammunition = table.Count > 0 ? table : null };
    }

    /// <summary>Every kind of a battery's loadout to a family's block; a kind the family lacks takes its common shell (the
    /// families before HE shells), else its AP.</summary>
    public static Design SetFamily(Design d, bool main, int k, string family)
    {
        double cal = List(d, main)![k].CalibreMm ?? 0;
        var shells = Loadout(d, main, k).Select(e =>
        {
            string kind = TemplateOf(e.Ammo)?.Value.Kind ?? d.Ammunition?.GetValueOrDefault(e.Ammo ?? "")?.Kind ?? "ap";
            var t = BlockOf(family, kind, cal) ?? BlockOf(family, "common", cal) ?? BlockOf(family, "ap", cal);
            return t is null ? e : e with { Ammo = t.Name };
        }).ToList();
        return With(d, main, k, Merge(shells));
    }

    /// <summary>Every gun battery to a family (the Templates panel's Shells row).</summary>
    public static Design SetFamilyAll(Design d, string family)
    {
        foreach (var main in new[] { true, false })
            for (int k = 0; k < (List(d, main)?.Count ?? 0); k++)
                if (List(d, main)![k].CalibreMm != null)
                    d = SetFamily(d, main, k, family);
        return d;
    }

    /// <summary>One entry of a battery's loadout to another block.</summary>
    public static Design SetBlock(Design d, bool main, int k, int entry, string template)
    {
        var shells = Loadout(d, main, k);
        shells[entry] = shells[entry] with { Ammo = template, MassKg = null, MuzzleVelocityMs = null };
        return With(d, main, k, Merge(shells));
    }

    /// <summary>One entry's share, in whole percent (kept 5 to 100 less 5 for each other entry); the others rescale to
    /// make up the rest, rounded to whole percent, the largest taking what rounding leaves.</summary>
    public static Design SetShare(Design d, bool main, int k, int entry, int pct)
    {
        var shells = Loadout(d, main, k);
        int n = shells.Count;
        if (n < 2)
            return d;
        pct = Math.Clamp(pct, 5, 100 - 5 * (n - 1));
        double others = shells.Where((_, i) => i != entry).Sum(e => e.Share ?? 0);
        var shares = shells.Select((e, i) => i == entry ? pct : others > 0 ? Math.Max(5, Math.Round((e.Share ?? 0) / others * (100 - pct))) : (100.0 - pct) / (n - 1)).ToArray();
        Settle(shares, entry);
        return With(d, main, k, shells.Select((e, i) => e with { Share = shares[i] / 100 }).ToList());
    }

    /// <summary>A kind the battery lacks, from its family (the first entry's), at 20%.</summary>
    public static Design AddKind(Design d, bool main, int k, string kind)
    {
        var shells = Loadout(d, main, k);
        string family = TemplateOf(shells[0].Ammo)?.Group ?? ShellTemplates.DefaultFamily;
        var t = BlockOf(family, kind, List(d, main)![k].CalibreMm ?? 0) ?? BlockOf(ShellTemplates.DefaultFamily, kind, List(d, main)![k].CalibreMm ?? 0);
        if (t is null)
            return d;
        d = With(d, main, k, [.. shells, new ShellEntryInput { Ammo = t.Name, Share = 0 }]);
        return SetShare(d, main, k, shells.Count, 20);
    }

    /// <summary>An entry gone, its share to the largest left.</summary>
    public static Design Remove(Design d, bool main, int k, int entry)
    {
        var shells = Loadout(d, main, k);
        if (shells.Count < 2)
            return d;
        double gone = shells[entry].Share ?? 0;
        shells.RemoveAt(entry);
        int big = shells.Select((e, i) => (e.Share ?? 0, i)).Max().i;
        shells[big] = shells[big] with { Share = Math.Round(((shells[big].Share ?? 0) + gone) * 100) / 100 };
        return With(d, main, k, shells);
    }

    /// <summary>The default loadout again: the battery gives no shells.</summary>
    public static Design Default(Design d, bool main, int k) => With(d, main, k, null);

    /// <summary>Two entries of one block become one (a family change can make AP and common the same block).</summary>
    static List<ShellEntryInput> Merge(List<ShellEntryInput> shells)
    {
        var res = new List<ShellEntryInput>();
        foreach (var e in shells)
        {
            int i = res.FindIndex(x => x.Ammo == e.Ammo);
            if (i < 0)
                res.Add(e);
            else
                res[i] = res[i] with { Share = Math.Round(((res[i].Share ?? 0) + (e.Share ?? 0)) * 100) / 100 };
        }
        return res;
    }

    /// <summary>Whole percents adding up to 100: what rounding left goes to the largest other than the one set.</summary>
    static void Settle(double[] shares, int keep)
    {
        double rest = 100 - shares.Sum();
        int big = -1;
        for (int i = 0; i < shares.Length; i++)
            if (i != keep && (big < 0 || shares[i] > shares[big]))
                big = i;
        shares[big] += rest;
    }

    // ------------------------------------------------------------------ the families, by the design's navy and year

    /// <summary>The look's navy (its dockyard) as the templates' prefix: RN, DE, US, JP, FR, IT, RU; null for generic.</summary>
    public static string? NavyOf(Design d) => d.Look?.Navy switch
    {
        "portsmouth" => "RN",
        "kiel" => "DE",
        "brooklyn" => "US",
        "kure" => "JP",
        "toulon" => "FR",
        "la_spezia" => "IT",
        "kronstadt" => "RU",
        _ => null,
    };

    /// <summary>The look's era as a year, for ordering only (the sim never reads it).</summary>
    public static int YearOf(Design d) => d.Look?.Era switch
    {
        "victorian" => 1890,
        "great_war" => 1912,
        "treaty" => 1928,
        "cold_war" => 1955,
        _ => 1940,
    };

    public const string AnyNavy = "Any navy";

    static readonly string[] Navies = ["RN", "DE", "US", "JP", "FR", "IT", "RU"];
    static readonly Dictionary<string, string> NavyNames = new()
    {
        ["RN"] = "Royal Navy", ["DE"] = "German", ["US"] = "US Navy", ["JP"] = "Japanese", ["FR"] = "French", ["IT"] = "Italian", ["RU"] = "Russian",
        [AnyNavy] = AnyNavy,
    };
    public static string NavyName(string navy) => NavyNames.GetValueOrDefault(navy, navy);

    /// <summary>A family's navy (its prefix) and year ("RN 1906"; "1912 standard" is any navy's).</summary>
    public static (string Navy, int Year) Parse(string family)
    {
        var parts = family.Split(' ');
        string navy = Navies.Contains(parts[0]) ? parts[0] : AnyNavy;
        int year = parts.Select(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out int y) && y > 1800 ? y : 0).FirstOrDefault(y => y > 0);
        return (navy, year);
    }

    /// <summary>The families as the tree lists them: the design's navy first, then any navy's, then the others; each
    /// navy's families oldest first. Each navy carries the family nearest the design's year (the latest not after it,
    /// else the earliest), the one to show first.</summary>
    public static List<(string Navy, List<string> Families, string Nearest)> Tree(Design d)
    {
        string? own = NavyOf(d);
        int year = YearOf(d);
        return [.. ShellTemplates.All.Select(t => t.Group).Distinct()
            .GroupBy(f => Parse(f).Navy)
            .OrderBy(g => g.Key == own ? 0 : g.Key == AnyNavy ? 1 : 2).ThenBy(g => Array.IndexOf(Navies, g.Key))
            .Select(g =>
            {
                var fams = g.OrderBy(f => Parse(f).Year).ToList();
                string nearest = fams.LastOrDefault(f => Parse(f).Year <= year) ?? fams[0];
                return (g.Key, fams, nearest);
            })];
    }
}
