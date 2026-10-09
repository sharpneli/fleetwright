namespace Fleetwright.Shipgen;

/// <summary>decks: the hull's deck stack (deck 0 the main deck, 1 the second, ... down to the inner bottom, every
/// DeckPitch) and raised stretches of hull (-1, -2, ... over their spans).</summary>
public static class Decks
{
    public const double MinTier = 1.0;
    const int MaxDecks = 60;
    static readonly string[] DeckNames = ["Main deck", "Second deck", "Third deck", "Fourth deck", "Fifth deck",
        "Sixth deck", "Seventh deck", "Eighth deck", "Ninth deck", "Tenth deck"];

    /// <summary>A deck's name by its number: 0 the main deck, 1, 2, ... down the stack; -1, -2, ... raised decks.</summary>
    public static string DeckName(long n)
    {
        if (n < 0)
            return $"Raised deck {-n}";
        return n < DeckNames.Length ? DeckNames[n] : $"Deck {n + 1}";
    }

    /// <summary>The hull's decks, every DeckPitch down from the main deck, as heights above the keel, top down.</summary>
    public static List<(long N, double Z)> DeckStack(Design design, double D)
    {
        double floor = design.StyleName == "planing" ? 0.0 : Powerplant.DoubleBottom(D);
        var result = new List<(long, double)> { (0, D) };
        while (D - result.Count * Geometry.DeckPitch >= floor + MinTier - 1e-9 && result.Count <= MaxDecks)
            result.Add((result.Count, D - result.Count * Geometry.DeckPitch));
        return result;
    }

    /// <summary>The stretch s0..s1 split where raised stretches step: [(x0, x1, levels)], each with the raised decks
    /// over it, at most k.</summary>
    public static List<(double X0, double X1, long Lv)> RaisedPieces(IReadOnlyList<RaisedStretch> raised, double s0, double s1, long k)
    {
        var set = new HashSet<double> { s0, s1 };
        foreach (var r in raised)
            foreach (var x in new[] { r.X0, r.X1 })
                if (s0 < x && x < s1)
                    set.Add(x);
        var xs = set.Order().ToList();
        var result = new List<(double, double, long)>();
        for (int i = 0; i < xs.Count - 1; i++)
        {
            double a = xs[i], b = xs[i + 1];
            double m = (a + b) / 2;
            long lv = Math.Min(k, raised.Where(r => r.X0 <= m && m <= r.X1).Select(r => r.Levels).DefaultIfEmpty(0L).Max());
            if (result.Count > 0 && result[^1].Item3 == lv)
                result[^1] = (result[^1].Item1, b, lv);
            else
                result.Add((a, b, lv));
        }
        return result;
    }
}

/// <summary>A raised stretch of hull (forecastle, poop, raised deck): the weather deck `Levels` decks above the main
/// deck from X0 to X1.</summary>
public sealed record RaisedStretch(string Id, double X0, double X1, long Levels);

/// <summary>The machinery space as the layout planned it: the fuel, the space, its segments forward to aft (boiler,
/// engine, bunker), the wing bunkers' tonnes and width, the end bunkers' length, the space's width and height, the inner
/// bottom and roof (above the keel), whether an armour deck roofs it and how thick, the side protection, and the deck
/// stack (heights above the keel, the main deck first).</summary>
public sealed record PlantPlan(string Fuel, MachinerySpace Space, List<(string Kind, double Len)> Segments, double WingT,
    double WingM, double EndM, double Width, double Height, double InnerBottom, double Top, bool Armoured, double DeckMm,
    double Tds, List<double> Decks);

/// <summary>The navigating bridge: its level and floor (m above the main deck), the level it needs to see over the
/// forward turrets, the tower's top level and that turret roof's height.</summary>
public sealed record BridgePlan(long Level, double Floor, long Need, long Tower, double? TurretRoof);

/// <summary>What the wind sees from abeam above the main deck: its area and the height of its centre.</summary>
public sealed record Windage(double AreaM2, double ZM);

/// <summary>Layout facts: what the layout found that the physics and the later passes need (Layout.Geo). Before a
/// layout exists navarch gets a bare Geo, so the before-layout defaults live here.</summary>
public sealed class Geo
{
    public double Shift;
    public (double X0, double X1)? Machinery;
    public double? MachineryX;
    public PlantPlan? Plant;
    public FunnelPlan? FunnelPlan;
    public double SmokeReach;
    public (double X0, double X1)? Citadel;
    public (double X0, double X1)? Steering;
    public double? SteeringBeam;
    public List<RaisedStretch> Raised = [];
    public Windage? Windage;
    public BridgePlan? Bridge;
    public List<(double X0, double X1)>? Holds;
    public (double X, double Z)? Magazine;
    public (double X, double Z)? Avgas;

    /// <summary>Where the plant's and fuel's weights stand: the machinery's middle, or a little abaft amidships.</summary>
    public double MachineryMid(double L) => MachineryX ?? -0.02 * L;

    /// <summary>The citadel's stretch, or the middle 0.6 L.</summary>
    public (double X0, double X1) CitadelSpan(double L) => Citadel ?? (-0.3 * L, 0.3 * L);

    /// <summary>The steering gear's stretch: the layout's or the rule's.</summary>
    public (double X0, double X1) SteeringSpan(double L) => Steering ?? Propulsion.SteeringSpan(L);
}

/// <summary>Fixed firing arcs by mount kind, and the one interval each mount turns within (its traverse).</summary>
public static class Arcs
{
    public const double ArcEnd = 135.0, ArcSide = 90.0, ArcBeam = 65.0, ArcCross = 30.0, ArcCasemate = 60.0;
    public const double ArcTorpedo = 60.0, ArcFixed = 1.0;

    /// <summary>[start, end] clockwise with 0 &lt;= start &lt; 360; end may exceed 360.</summary>
    static double[] Arc(double centre, double half)
    {
        double start = Geometry.Normalize360(centre - half);
        return [Math.Round(start, 1), Math.Round(start + 2 * half, 1)];
    }

    public static List<double[]> MountArcs(Mount m)
    {
        if (m.Fixed is double fixedBearing)
            return [Arc(fixedBearing, ArcFixed)];
        double own = m.Y > 0 ? 90.0 : 270.0;
        if (m.Casemate)
            return [Arc(own, ArcCasemate)];
        if (m.SideMount)
            return [Arc(own, ArcSide)];
        if (m.Wing)
        {
            if (m.CrossDeck)
                return [Arc(own, ArcSide), Arc(own + 180.0, ArcCross)];
            return [Arc(own, ArcSide)];
        }
        if (m.ArcRole == "beam")
            return [Arc(90.0, ArcBeam), Arc(270.0, ArcBeam)];
        if (m.Kind == "torpedo" && Math.Abs(m.Y) < 0.5)
            return [Arc(90.0, ArcTorpedo), Arc(270.0, ArcTorpedo)];
        if (Math.Abs(Math.Abs(Geometry.Wrap180(m.Rest)) - 90.0) < 1e-6)
            return [Arc(m.Rest, ArcSide)];
        return [Arc(m.Rest, ArcEnd)];
    }

    /// <summary>A cross-deck wing turret's whole swing [start, end].</summary>
    public static double[] CrossTurn(Mount m)
    {
        var arcs = MountArcs(m);
        var own = arcs[0];
        var cross = arcs[1];
        if ((Geometry.Normalize360(m.Rest) < 90.0) == (m.Y < 0))
            return [own[0], cross[1] < own[0] ? cross[1] + 360.0 : cross[1]];
        return [cross[0], own[1] > cross[0] ? own[1] : own[1] + 360.0];
    }

    /// <summary>The one interval [start, end] (end may exceed 360) a mount turns within.</summary>
    public static double[]? MountTraverse(Mount m)
    {
        var arcs = MountArcs(m);
        if (m.CrossDeck)
            return CrossTurn(m);
        if (arcs.Count == 1)
            return [arcs[0][0], arcs[0][1]];
        double rest = Geometry.Normalize360(m.Fixed ?? m.Rest);
        double[]? best = null;
        foreach (var (p, q) in new[] { (arcs[0], arcs[1]), (arcs[1], arcs[0]) })
        {
            double lo = p[0], hi = q[1];
            while (hi < lo)
                hi += 360.0;
            if (hi - lo < 360.0 && (lo <= rest && rest <= hi || lo <= rest + 360.0 && rest + 360.0 <= hi) &&
                (best is null || hi - lo < best[1] - best[0]))
                best = [lo, hi];
        }
        return best;
    }

    /// <summary>Give every mount its fixed arcs and its traverse, and wrap its rest bearing to -180..180.</summary>
    public static void AssignArcs(Layout lay)
    {
        foreach (var m in lay.Mounts)
        {
            m.Arcs = MountArcs(m);
            m.Traverse = MountTraverse(m);
            m.Rest = Geometry.Wrap180(m.Fixed ?? m.Rest);
        }
        var byId = lay.Mounts.ToDictionary(m => m.Id, StringComparer.Ordinal);
        foreach (var sm in lay.Spec.Turrets)
            sm.Rest = byId[sm.Id].Rest;
    }
}
