namespace Fleetwright.Shipgen;

/// <summary>decks: the hull's deck stack (deck 0 the main deck, 1 the second, ... down to the inner bottom, every
/// DECK_PITCH) and raised stretches of hull (-1, -2, ... over their spans).</summary>
public static class Decks
{
    public const double MIN_TIER = 1.0;
    const int MAX_DECKS = 60;
    static readonly string[] DECK_NAMES = ["Main deck", "Second deck", "Third deck", "Fourth deck", "Fifth deck",
        "Sixth deck", "Seventh deck", "Eighth deck", "Ninth deck", "Tenth deck"];

    /// <summary>A deck's name by its number: 0 the main deck, 1, 2, ... down the stack; -1, -2, ... raised decks.</summary>
    public static string DeckName(long n)
    {
        if (n < 0)
            return $"Raised deck {-n}";
        return n < DECK_NAMES.Length ? DECK_NAMES[n] : $"Deck {n + 1}";
    }

    /// <summary>The hull's decks, every DECK_PITCH down from the main deck, as heights above the keel, top down.</summary>
    public static List<(long N, double Z)> DeckStack(PyDict design, double D)
    {
        double floor = Py.Eq(design.Get("style"), "planing") ? 0.0 : Powerplant.DoubleBottom(D);
        var out_ = new List<(long, double)> { (0, D) };
        while (D - out_.Count * Geometry.DECK_PITCH >= floor + MIN_TIER - 1e-9 && out_.Count <= MAX_DECKS)
            out_.Add((out_.Count, D - out_.Count * Geometry.DECK_PITCH));
        return out_;
    }

    /// <summary>The stretch s0..s1 split where raised stretches (dicts x0, x1, levels) step: [(x0, x1, levels)], each
    /// with the raised decks over it, at most k.</summary>
    public static List<(double X0, double X1, long Lv)> RaisedPieces(IReadOnlyList<PyDict> raised, double s0, double s1, long k)
    {
        var set = new HashSet<double> { s0, s1 };
        foreach (var r in raised)
            foreach (var x in new[] { r.F("x0"), r.F("x1") })
                if (s0 < x && x < s1)
                    set.Add(x);
        var xs = set.Order().ToList();
        var out_ = new List<(double, double, long)>();
        for (int i = 0; i < xs.Count - 1; i++)
        {
            double a = xs[i], b = xs[i + 1];
            double m = (a + b) / 2;
            long lv = Math.Min(k, raised.Where(r => r.F("x0") <= m && m <= r.F("x1")).Select(r => r.I("levels")).DefaultIfEmpty(0L).Max());
            if (out_.Count > 0 && out_[^1].Item3 == lv)
                out_[^1] = (out_[^1].Item1, b, lv);
            else
                out_.Add((a, b, lv));
        }
        return out_;
    }
}

/// <summary>Layout facts: what the layout found that the physics and the later passes need (Layout.geo). Before a
/// layout exists navarch gets a bare Geo(), so the before-layout defaults live here.</summary>
public sealed class Geo
{
    public double Shift;
    public (double X0, double X1)? Machinery;
    public double? MachineryX;
    public PyDict? Plant;
    public PyDict? FunnelPlan;
    public double SmokeReach;
    public (double X0, double X1)? Citadel;
    public (double X0, double X1)? Steering;
    public double? SteeringBeam;
    public List<PyDict> Raised = [];
    public PyDict? Windage;
    public PyDict? Bridge;
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

/// <summary>arcs: fixed firing arcs by mount kind, and the one interval each mount turns within (traverse).</summary>
public static class Arcs
{
    public const double ARC_END = 135.0, ARC_SIDE = 90.0, ARC_BEAM = 65.0, ARC_CROSS = 30.0, ARC_CASEMATE = 60.0;
    public const double ARC_TORPEDO = 60.0, ARC_FIXED = 1.0;

    /// <summary>[start, end] clockwise with 0 &lt;= start &lt; 360; end may exceed 360.</summary>
    static double[] Arc(double centre, double half)
    {
        double start = Geometry.Normalize360(centre - half);
        return [Math.Round(start, 1), Math.Round(start + 2 * half, 1)];
    }

    static bool HasFixed(PyDict m) => m.Get("fixed") is not null;

    public static List<double[]> MountArcs(PyDict m)
    {
        if (HasFixed(m))
            return [Arc(m.F("fixed"), ARC_FIXED)];
        double own = m.F("y") > 0 ? 90.0 : 270.0;
        if (m.B("casemate"))
            return [Arc(own, ARC_CASEMATE)];
        if (m.B("side_mount"))
            return [Arc(own, ARC_SIDE)];
        if (m.B("wing"))
        {
            if (m.B("cross_deck"))
                return [Arc(own, ARC_SIDE), Arc(own + 180.0, ARC_CROSS)];
            return [Arc(own, ARC_SIDE)];
        }
        if (Py.Eq(m.Get("arc_role"), "beam"))
            return [Arc(90.0, ARC_BEAM), Arc(270.0, ARC_BEAM)];
        if (Py.Eq(m["kind"], "torpedo") && Math.Abs(m.F("y")) < 0.5)
            return [Arc(90.0, ARC_TORPEDO), Arc(270.0, ARC_TORPEDO)];
        if (Math.Abs(Math.Abs(Geometry.Wrap180(m.F("rest"))) - 90.0) < 1e-6)
            return [Arc(m.F("rest"), ARC_SIDE)];
        return [Arc(m.F("rest"), ARC_END)];
    }

    /// <summary>A cross-deck wing turret's whole swing [start, end].</summary>
    public static double[] CrossTurn(PyDict m)
    {
        var arcs = MountArcs(m);
        var own = arcs[0];
        var cross = arcs[1];
        if ((Geometry.Normalize360(m.F("rest")) < 90.0) == (m.F("y") < 0))
            return [own[0], cross[1] < own[0] ? cross[1] + 360.0 : cross[1]];
        return [cross[0], own[1] > cross[0] ? own[1] : own[1] + 360.0];
    }

    /// <summary>The one interval [start, end] (end may exceed 360) a mount turns within.</summary>
    public static double[]? MountTraverse(PyDict m)
    {
        var arcs = MountArcs(m);
        if (m.B("cross_deck"))
            return CrossTurn(m);
        if (arcs.Count == 1)
            return [arcs[0][0], arcs[0][1]];
        double rest = Geometry.Normalize360(HasFixed(m) ? m.F("fixed") : m.F("rest"));
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
            m["arcs"] = MountArcs(m);
            m["traverse"] = MountTraverse(m);
            m["rest"] = Geometry.Wrap180(HasFixed(m) ? m.F("fixed") : m.F("rest"));
        }
        var byId = new Dictionary<string, PyDict>(StringComparer.Ordinal);
        foreach (var m in lay.Mounts)
            byId[m.S("id")] = m;
        foreach (PyDict sm in lay.Spec.L("turrets").Cast<PyDict>())
            sm["rest"] = byId[sm.S("id")]["rest"];
    }
}
