namespace Fleetwright.Designer;

/// <summary>
/// A limit a design is measured against: a treaty, a slipway's capacity, a dock. Limits come from the gameplay layer
/// and never restrict the designer: a design over one still builds, saves and can be accepted; the overview and the
/// remarks only show by how much it's over (cheat a treaty and face the consequences). Null: no limit on that figure.
/// </summary>
public sealed record DesignLimits(string Name, string Note)
{
    public double? StandardT { get; init; }
    public double? FullT { get; init; }
    public double? LengthM { get; init; }
    public double? BeamM { get; init; }
    public double? DraughtM { get; init; }
    public double? MinGmM { get; init; }
    public double? MaxCalibreMm { get; init; }

    /// <summary>Stand-ins until the gameplay layer hands limits in: none, two slipways and a few treaties (tonnes; the
    /// treaties wrote long tons).</summary>
    public static readonly DesignLimits[] Mock =
    [
        new("No limits", ""),
        new("Slipway No. 1", "the large slip: 40,000 t standard, 250 m") { StandardT = 40000, LengthM = 250 },
        new("Slipway No. 3", "a cruiser slip: 14,000 t standard, 200 m") { StandardT = 14000, LengthM = 200 },
        new("Washington 1922 · capital ship", "35,000 long tons standard, guns up to 16 in") { StandardT = 35560, MaxCalibreMm = 406.4 },
        new("London 1930 · heavy cruiser", "10,000 long tons standard, guns up to 8 in") { StandardT = 10160, MaxCalibreMm = 203.2 },
        new("London 1936 · battleship", "35,000 long tons standard, guns up to 14 in") { StandardT = 35560, MaxCalibreMm = 355.6 },
    ];
}
