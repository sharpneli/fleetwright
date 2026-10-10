namespace Fleetwright.Shipgen;

/// <summary>hull.section's topside: the hull's side above its widest point, as a share of the planform's half-width
/// (Hull, the maximum beam). Heights are metres above the main deck, so the layout and the hull form share one side
/// whatever the waterline. Between the waterline and KnuckleH the hull form ramps from the waterline out to the
/// planform; from KnuckleH up, through the main deck and the raised stretches' sides, it is this.</summary>
public abstract class Topside
{
    /// <summary>The topsides a design may name, the default first.</summary>
    public static readonly string[] Names = ["wall_sided", "tumblehome"];

    /// <summary>The widest point's height over the main deck (0 or less).</summary>
    public abstract double KnuckleH { get; }

    /// <summary>The side's half-width h above the main deck (h ≥ KnuckleH) as a share of the planform's at x.</summary>
    public abstract double Ratio(double x, double h);

    /// <summary>Is the side the planform at every height (Ratio is 1 throughout)?</summary>
    public virtual bool Plain => false;

    /// <summary>The side's mean share of the planform's half-width along the length at h over the main deck (1 under
    /// the knuckle), for the weights: a deck, an armour deck or the girder's breadth there.</summary>
    public double MeanRatio(double L, double h)
    {
        if (Plain || h <= KnuckleH)
            return 1.0;
        const int n = 40;
        double sum = 0.0;
        for (int i = 0; i < n; i++)
            sum += Ratio(-L / 2 + L * (i + 0.5) / n, h);
        return sum / n;
    }

    /// <summary>How much longer the side is than it is tall between hLo and hHi over the main deck (1 where it is
    /// upright): plate laid on it from hLo to hHi, over the middle of the hull where the half-breadth is B / 2.</summary>
    public double SlantFactor(double B, double hLo, double hHi)
    {
        if (Plain || hHi <= KnuckleH || hHi <= hLo)
            return 1.0;
        const int n = 24;
        double lo = Math.Max(hLo, KnuckleH), len = Math.Max(0.0, Math.Min(hHi, KnuckleH) - hLo), dh = (hHi - lo) / n;
        for (int i = 0; i < n; i++)
        {
            double h0 = lo + i * dh, h1 = h0 + dh;
            double dy = B / 2 * (Ratio(0.0, h1) - Ratio(0.0, h0));
            len += Math.Sqrt(dh * dh + dy * dy);
        }
        return len / (hHi - hLo);
    }

    /// <summary>The side's mean share of the planform's half-width at x between hLo and hHi over the main deck (1
    /// where it is upright): a plate across the hull there, such as an armoured bulkhead.</summary>
    public double BandRatio(double x, double hLo, double hHi)
    {
        if (Plain || hHi <= KnuckleH || hHi <= hLo)
            return 1.0;
        const int n = 24;
        double sum = 0.0;
        for (int i = 0; i < n; i++)
        {
            double h = hLo + (hHi - hLo) * (i + 0.5) / n;
            sum += h <= KnuckleH ? 1.0 : Ratio(x, h);
        }
        return sum / n;
    }

    /// <summary>The design's topside. freeboard: the main deck's height over the waterline, which scales the knuckle.</summary>
    public static Topside Of(Design design, double freeboard)
    {
        var s = design.Hull?.Section;
        return s?.Topside switch
        {
            null or "wall_sided" => WallSided.Instance,
            "tumblehome" => new Tumblehome(s.Strength ?? 1.0, s.Knuckle ?? Tumblehome.DefaultKnuckle, s.Extent == "midships",
                design.HullLength, freeboard),
            var t => throw new ArgumentException($"hull.section.topside = {t}: not a topside"),
        };
    }

    /// <summary>hull.section's errors.</summary>
    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        if (design.Hull?.Section is not { } s)
            return errs;
        errs.AddRange(s.Extra.KeysOrEmpty().Select(k =>
            $"hull.section.{k}: not a section setting (topside, strength, knuckle, extent)"));
        if (s.Topside is { } t && !Names.Contains(t))
            errs.Add($"hull.section.topside = {Style.Quote(t)}: use one of {string.Join(", ", Names)}");
        else if (s.Topside != "tumblehome")
        {
            foreach (var (k, given) in new[] { ("strength", s.Strength != null), ("knuckle", s.Knuckle != null), ("extent", s.Extent != null) })
                if (given)
                    errs.Add($"hull.section.{k}: a tumblehome setting; give topside tumblehome or remove it");
        }
        if (s.Strength is double st && !(st >= 0))
            errs.Add("hull.section.strength: a number, 0 or more (1: a strong French tumblehome)");
        if (s.Knuckle is double kn && !(kn >= 0 && kn <= Tumblehome.MaxKnuckle))
            errs.Add($"hull.section.knuckle: the widest point's height over the waterline as a share of the freeboard, 0 to {Tumblehome.MaxKnuckle}");
        if (s.Extent is { } e && e is not ("full" or "midships"))
            errs.Add($"hull.section.extent = {Style.Quote(e)}: use full or midships");
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

/// <summary>Sides that lean in above the widest point (the French battleships of the 1890s): the half-width falls from
/// the planform's at the knuckle to (1 - DeckIn strength) of it at the main deck along a curve that steepens upward,
/// and carries on up a raised stretch's sides along its tangent, never under MinRatio. "midships" fades it out toward
/// the ends (the Borodinos' flat-sided ends).</summary>
public sealed class Tumblehome : Topside
{
    public const double DeckIn = 0.28, Curve = 1.6, MinRatio = 0.15, DefaultKnuckle = 0.2, MaxKnuckle = 0.9;
    const double FadeFrom = 0.5, FadeRun = 0.35;
    readonly double a, hK, halfL;
    readonly bool midships;

    /// <summary>strength: 1 takes the main deck in to 0.72 of the planform's half-width, 2 to 0.44; knuckle: the widest
    /// point's height over the waterline as a share of the freeboard.</summary>
    public Tumblehome(double strength, double knuckle, bool midships, double L, double freeboard)
    {
        a = DeckIn * strength;
        hK = -(1 - Math.Min(knuckle, MaxKnuckle)) * Math.Max(freeboard, 0.1);
        halfL = L / 2;
        this.midships = midships;
    }

    public override double KnuckleH => hK;

    public override double Ratio(double x, double h)
    {
        if (h <= hK)
            return 1.0;
        double H = -hK;
        double s = h <= 0 ? Math.Pow((h - hK) / H, Curve) : 1 + Curve * h / H;
        return Math.Max(MinRatio, 1 - a * Envelope(x) * s);
    }

    double Envelope(double x)
    {
        if (!midships)
            return 1.0;
        double t = Math.Clamp((Math.Abs(x) / halfL - FadeFrom) / FadeRun, 0.0, 1.0);
        return 1 - t * t * (3 - 2 * t);
    }
}

/// <summary>The hull's side at one height over the main deck, seen from above.</summary>
public sealed class SidePlanform(Hull hull, Topside topside, double h) : Planform(hull.L, hull.B)
{
    public override double HalfWidth(double x) => hull.HalfWidth(x) * topside.Ratio(x, h);
}
