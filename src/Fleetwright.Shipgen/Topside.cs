namespace Fleetwright.Shipgen;

/// <summary>hull.section's topside: the hull's side above its widest point, as a share of the planform's half-width
/// (Hull, the maximum beam). Heights are metres above the main deck, so the layout and the hull form share one side
/// whatever the waterline. Between the waterline and KnuckleH the hull form ramps from the waterline out to the
/// planform; from KnuckleH up, through the main deck and the raised stretches' sides, it is this.</summary>
public abstract class Topside
{
    /// <summary>The topsides a design may name, the default first.</summary>
    public static readonly string[] Names = ["wall_sided"];

    /// <summary>The widest point's height over the main deck (0 or less).</summary>
    public abstract double KnuckleH { get; }

    /// <summary>The side's half-width h above the main deck (h ≥ KnuckleH) as a share of the planform's at x.</summary>
    public abstract double Ratio(double x, double h);

    /// <summary>Is the side the planform at every height (Ratio is 1 throughout)?</summary>
    public virtual bool Plain => false;

    /// <summary>The design's topside. freeboard: the main deck's height over the waterline, which scales the knuckle.</summary>
    public static Topside Of(Design design, double freeboard) => design.Hull?.Section?.Topside switch
    {
        null or "wall_sided" => WallSided.Instance,
        var t => throw new ArgumentException($"hull.section.topside = {t}: not a topside"),
    };

    /// <summary>hull.section's errors.</summary>
    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        if (design.Hull?.Section is not { } s)
            return errs;
        errs.AddRange(s.Extra.KeysOrEmpty().Select(k => $"hull.section.{k}: not a section setting (topside)"));
        if (s.Topside is { } t && !Names.Contains(t))
            errs.Add($"hull.section.topside = {Style.Quote(t)}: use one of {string.Join(", ", Names)}");
        return errs;
    }
}

/// <summary>Sides straight up from the widest point to the deck edge (flared at the ends, where the deck is wider than
/// the waterline): the planform at every height above the waterline.</summary>
public sealed class WallSided : Topside
{
    public static readonly WallSided Instance = new();

    WallSided()
    {
    }

    public override double KnuckleH => 0.0;
    public override double Ratio(double x, double h) => 1.0;
    public override bool Plain => true;
}

/// <summary>The hull's side at one height over the main deck, seen from above.</summary>
public sealed class SidePlanform(Hull hull, Topside topside, double h) : Planform(hull.L, hull.B)
{
    public override double HalfWidth(double x) => hull.HalfWidth(x) * topside.Ratio(x, h);
}
