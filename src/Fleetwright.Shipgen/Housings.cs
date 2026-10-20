namespace Fleetwright.Shipgen;

/// <summary>How a battery's guns are housed (docs/battle/magazines.md V1), the designer's choice at any calibre:
/// <list type="bullet">
/// <item><b>barbette:</b> an armoured turret on a barbette (the drawing's "bb"): a gunhouse, a working chamber in the
/// barbette, hoists down a trunk to the handling room;</item>
/// <item><b>gunhouse:</b> a light gunhouse (the drawing's "dp"): enclosed, its crew inside, its hoists down a trunk;</item>
/// <item><b>shield:</b> a shield over the gun on its pedestal, open behind (the drawing's "open"): ammunition up to the deck
/// by hoist or whip, ready rounds by the gun.</item>
/// </list>
/// A casemate is a secondary battery's position (its <c>mount</c>), not a housing. The calibre ties nothing: the Monmouths'
/// twin 6″ and Fubuki's 12.7 cm had gunhouses, Chester's 6″ stood behind shields.</summary>
public static class Housings
{
    public static readonly string[] All = ["barbette", "gunhouse", "shield"];

    /// <summary>The turret type's shape for a housing.</summary>
    public static string Shape(string housing) => housing switch
    {
        "barbette" => "bb",
        "gunhouse" => "dp",
        "shield" => "open",
        _ => throw new ArgumentException($"no housing \"{housing}\" ({string.Join(", ", All)})"),
    };

    /// <summary>A starting suggestion for a battery that doesn't say (a new battery in the designer; the format's 11 → 12
    /// step, which writes it into older designs so they build as before): a barbette from 150 mm, a gunhouse from 76 mm,
    /// a shield under that. Never a rule: the design keeps what was chosen.</summary>
    public static string Suggest(double calibreMm) => calibreMm >= 150 ? "barbette" : calibreMm >= 76 ? "gunhouse" : "shield";

    /// <summary>A battery's housing: its own, else the suggestion.</summary>
    public static string Of(BatteryInput b) => b.Housing ?? Suggest(b.CalibreMm ?? 0);

    /// <summary>Each battery's housing one of <see cref="All"/>.</summary>
    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        foreach (var (list, key) in new[] { (design.Main, "main"), (design.Secondary, "secondary") })
            for (int i = 0; i < (list?.Count ?? 0); i++)
                if (list![i].Housing is { } h && !All.Contains(h))
                    errs.Add($"{key}[{i}].housing = {Style.Quote(h)}: use {string.Join(", ", All)}");
        return errs;
    }
}
