namespace Fleetwright.Shipgen;

/// <summary>A collision footprint: a circle ("c"), an axis-aligned rect ("r") or a polygon ("p", with its box). Compared
/// by reference, as Python's id(fp) caches did.</summary>
public sealed class Footprint
{
    public readonly char Kind;
    public readonly double A, B, C, D;      // c: x, y, r; r: x0, y0, x1, y1; p: its box x0, y0, x1, y1
    public readonly List<Pt>? Pts;
    PreparedPolygon? prepared;

    Footprint(char kind, double a, double b, double c, double d, List<Pt>? pts)
    {
        Kind = kind;
        A = a;
        B = b;
        C = c;
        D = d;
        Pts = pts;
    }

    public static Footprint Circle(double x, double y, double r) => new('c', x, y, r, 0, null);
    public static Footprint Rect(double x0, double y0, double x1, double y1) => new('r', x0, y0, x1, y1, null);

    public static Footprint Poly(IEnumerable<Pt> pts)
    {
        var p = pts.ToList();
        var (x0, y0, x1, y1) = Geometry.Bounds(p);
        return new('p', x0, y0, x1, y1, p);
    }

    public double X => A;
    public double Y => B;
    public double R => C;

    /// <summary>Bounding box (x0, y0, x1, y1).</summary>
    public (double X0, double Y0, double X1, double Y1) BBox => Kind switch
    {
        'c' => (A - C, B - C, A + C, B + C),
        _ => (A, B, C, D),
    };

    /// <summary>A rect or polygon footprint as a polygon (a rect grown by margin).</summary>
    public List<Pt> Points(double margin = 0.0)
    {
        if (Kind == 'p')
            return Pts!;
        return [new(A - margin, B - margin), new(C + margin, B - margin), new(C + margin, D + margin), new(A - margin, D + margin)];
    }

    /// <summary>The polygon prepared for many tests (geometry.PreparedPolygon), made once.</summary>
    public PreparedPolygon Prepared => prepared ??= new PreparedPolygon(Pts!);
}

/// <summary>One placed footprint: standing from base to top above the main deck, owned by an id.</summary>
public sealed record Placed(Footprint Fp, double Base, double Top, string Owner);

/// <summary>A main turret's barrel sweep zone.</summary>
public sealed class Sweep
{
    public required string Owner;
    public required List<List<Pt>> Polys;
    public required List<(double X0, double Y0, double X1, double Y1)> Boxes;
    public required List<(double Cx, double Cy, double R, double A0, double A1)> Sectors;
    public required double Axis;
}

/// <summary>layout: turns a player's design (counts and calibres) into exact positions. The Layout object collects
/// what the layout places; its static methods are the module's functions.</summary>
public sealed partial class Layout
{
    public const double LEVEL_H = Geometry.DECK_PITCH;
    public const double CASEMATE_BEAM = 0.7;
    const double FP_CELL = 8.0;
    const int PREP_MIN = 24;

    public readonly double SupT;
    public readonly (double Plating, double Control) SupPlate;
    public readonly PyDict Construction;
    public readonly double? OwnPlateMmValue;
    public readonly List<PyDict> Directors = [];
    public List<Placed> Footprints = [];
    (List<Placed> List, int Count, Dictionary<long, List<((double X0, double Y0, double X1, double Y1) B, Placed O)>> Cells)? fpIndex;
    public readonly HashSet<Footprint> Overhangs = new(ReferenceEqualityComparer.Instance);
    public List<Weight> Weights = [];
    public List<string> Errors = [], Warnings = [];
    public PyDict Spec = new();
    public Geo Geo = new();
    public (double Lo, double Hi) ShiftRange = (0.0, 0.0);
    public List<PyDict> Compartments = [];
    public List<PyDict> Decks = [];
    public List<PyDict> Raised = [];
    public List<PyDict> Sponsons = [];
    public List<Sweep> Sweeps = [];
    public List<PyDict> FunnelsPlanned = [];
    public List<PyDict> Casings = [];
    public PyDict? ConningTower;
    public HashSet<string> Short = new(StringComparer.Ordinal);
    public List<PyDict> EndMounts = [];
    public OrderedDictionary<string, List<string>> Smoke = new(StringComparer.Ordinal);
    public PyDict? Crew;
    List<Pt>? deckBand;
    public readonly Dictionary<(List<Pt> Poly, double Y), List<(double, double)>> Scan = new(ScanKeyComparer.Instance);

    public Hull Hull = null!;
    public List<PyDict> Mounts = [];
    public List<PyDict> Blocks = [];
    public List<PyDict> Funnels = [];
    public List<PyDict> Aa = [];
    public double FunTop;

    sealed class ScanKeyComparer : IEqualityComparer<(List<Pt> Poly, double Y)>
    {
        public static readonly ScanKeyComparer Instance = new();
        public bool Equals((List<Pt> Poly, double Y) a, (List<Pt> Poly, double Y) b) =>
            ReferenceEquals(a.Poly, b.Poly) && a.Y.Equals(b.Y);
        public int GetHashCode((List<Pt> Poly, double Y) k) =>
            HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(k.Poly), k.Y);
    }

    public Layout(PyDict? design = null, double? supT = null, double? ownPlateMm = null)
    {
        var sup = (design ?? new PyDict()).DOr("superstructure");
        SupT = sup.F("t_per_m2", supT ?? Navarch.SUPERSTRUCTURE_T_PER_M2);
        SupPlate = (sup.F("plating_mm", 0.0), sup.F("control_mm", 0.0));
        Construction = HullWeight.Construction(design ?? new PyDict());
        OwnPlateMmValue = ownPlateMm;
    }

    /// <summary>Something doesn't fit: an error, and what more of (need: "length", "beam" or null) would fix it.</summary>
    public void Fail(string? need, string msg)
    {
        Errors.Add(msg);
        if (need != null)
            Short.Add(need);
    }

    static long Cell(double x) => (long)Math.Floor(x / FP_CELL);

    /// <summary>The x-cell index of the footprints, kept up with Footprints as it grows (rebuilt when the list is
    /// replaced): free and free_at look only at the footprints whose box may reach them.</summary>
    Dictionary<long, List<((double X0, double Y0, double X1, double Y1) B, Placed O)>> Cells()
    {
        var fps = Footprints;
        if (fpIndex is not { } idx || !ReferenceEquals(idx.List, fps) || idx.Count > fps.Count)
            idx = (fps, 0, new Dictionary<long, List<((double, double, double, double), Placed)>>());
        var cells = idx.Cells;
        for (int i = idx.Count; i < fps.Count; i++)
        {
            var o = fps[i];
            var b = o.Fp.BBox;
            for (long c = Cell(b.X0); c <= Cell(b.X1); c++)
            {
                if (!cells.TryGetValue(c, out var l))
                    cells[c] = l = [];
                l.Add((b, o));
            }
        }
        fpIndex = (fps, fps.Count, cells);
        return cells;
    }

    bool IsRaised(string owner)
    {
        foreach (var s in Raised)
            if (string.Equals(s.S("id"), owner, StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>Is the deck under a footprint free of everything placed, whatever its height? Raised stretches of hull
    /// don't count.</summary>
    public bool Free(Footprint fp, double margin = 0.4, IReadOnlyCollection<string>? ignore = null)
    {
        var (x0, y0, x1, y1) = fp.BBox;
        (x0, y0, x1, y1) = (x0 - margin, y0 - margin, x1 + margin, y1 + margin);
        var cells = Cells();
        long c0 = Cell(x0);
        for (long c = c0; c <= Cell(x1); c++)
        {
            if (!cells.TryGetValue(c, out var l))
                continue;
            foreach (var (b, o) in l)
                if (c == Math.Max(c0, Cell(b.X0)) && b.X0 < x1 && x0 < b.X1 && b.Y0 < y1 && y0 < b.Y1
                    && !(ignore?.Contains(o.Owner) ?? false) && !IsRaised(o.Owner) && Overlap(fp, o.Fp, margin))
                    return false;
        }
        return true;
    }

    /// <summary>Is a footprint standing from base to top clear of everything placed whose height overlaps it?</summary>
    public bool FreeAt(Footprint fp, double bse, double top, double margin = 0.4, IReadOnlyCollection<string>? ignore = null)
    {
        var (x0, y0, x1, y1) = fp.BBox;
        (x0, y0, x1, y1) = (x0 - margin, y0 - margin, x1 + margin, y1 + margin);
        var cells = Cells();
        long c0 = Cell(x0);
        for (long c = c0; c <= Cell(x1); c++)
        {
            if (!cells.TryGetValue(c, out var l))
                continue;
            foreach (var (b, o) in l)
            {
                if (c != Math.Max(c0, Cell(b.X0)))
                    continue;
                if (o.Top <= bse + 1e-6 || o.Base >= top - 1e-6 || (ignore?.Contains(o.Owner) ?? false))
                    continue;
                if (b.X0 < x1 && x0 < b.X1 && b.Y0 < y1 && y0 < b.Y1 && Overlap(fp, o.Fp, margin))
                    return false;
            }
        }
        return true;
    }

    public void Occupy(Footprint fp, double bse, double top, string owner) => Footprints.Add(new Placed(fp, bse, top, owner));

    /// <summary>A main turret claims the area its barrels sweep: its traverse, out to the muzzles.</summary>
    public void ReserveSweep(PyDict m)
    {
        var tr = Arcs.MountTraverse(m)!;
        double R = Geometry.TurretReach(m.D("t")) + 0.5;
        var poly = Geometry.SectorPolygon(m.F("x"), m.F("y"), R, tr[0], tr[1]);
        Sweeps.Add(new Sweep
        {
            Owner = m.S("id"),
            Polys = [poly],
            Boxes = [Geometry.Bounds(poly)],
            Sectors = [(m.F("x"), m.F("y"), R, tr[0], tr[1])],
            Axis = m.F("base") + 0.55 * (m.F("top") - m.F("base")),
        });
    }

    /// <summary>Is a footprint standing `top` metres above the deck clear of every gun sweep lower than it?</summary>
    public bool Clear(Footprint fp, double top)
    {
        (double X, double Y, double R)? circle = fp.Kind == 'c' ? (fp.X, fp.Y, fp.R) : null;
        var poly = fp.Kind == 'c' ? Geometry.CirclePolygon(fp.X, fp.Y, fp.R, 16) : fp.Points();
        return Clear(poly, top, circle);
    }

    public bool Clear(List<Pt> poly, double top, (double X, double Y, double R)? circle = null)
    {
        var (x0, y0, x1, y1) = Geometry.Bounds(poly);
        foreach (var sw in Sweeps)
        {
            if (sw.Axis >= top)
                continue;
            for (int i = 0; i < sw.Polys.Count; i++)
            {
                var p = sw.Polys[i];
                var (a0, b0, a1, b1) = sw.Boxes[i];
                var sec = sw.Sectors[i];
                if (!(a0 <= x1 && x0 <= a1 && b0 <= y1 && y0 <= b1))
                    continue;
                if (circle is { } c)
                {
                    double d = SectorDist(sec, c.X, c.Y);
                    if (d >= c.R)
                        continue;
                    if (d < c.R - 0.2 - 0.005 * sec.R)
                        return false;
                }
                if (Geometry.PolygonsIntersect(poly, p))
                    return false;
            }
        }
        return true;
    }

    /// <summary>How many decks the weather deck stands above the main deck at x (the highest under x - r .. x + r).</summary>
    public long DeckLevel(double x, double r = 0.0) =>
        Raised.Where(s => s.F("x0") - r <= x && x <= s.F("x1") + r).Select(s => s.I("levels")).DefaultIfEmpty(0L).Max();

    /// <summary>(lowest, highest) deck_level under a footprint reaching x - r .. x + r.</summary>
    public (long Lo, long Hi) DeckLevels(double x, double r = 0.0)
    {
        var pts = new List<double> { x - r, x + r };
        foreach (var s in Raised)
            foreach (var e in new[] { s.F("x0"), s.F("x1") })
                foreach (var d in new[] { -1e-6, 1e-6 })
                    if (x - r < e + d && e + d < x + r)
                        pts.Add(e + d);
        var lv = pts.Select(p => Raised.Where(s => s.F("x0") <= p && p <= s.F("x1")).Select(s => s.I("levels")).DefaultIfEmpty(0L).Max()).ToList();
        return (lv.Min(), lv.Max());
    }

    /// <summary>Height of the weather deck at x (m above the main deck).</summary>
    public double DeckZ(double x, double r = 0.0) => DeckLevel(x, r) * LEVEL_H;

    /// <summary>Is (x, y) on a deck that overhangs the hull (a flight deck)?</summary>
    public bool OnDeck(double x, double y) =>
        Decks.Any(dk => Py.Eq(dk["kind"], "flight_deck") && Geometry.PointInPolygon(x, y, Geometry.Pts(dk["points"])));

    /// <summary>The main deck less DH_INSET at the sides, as a convex polygon (cached).</summary>
    public List<Pt> DeckBand()
    {
        if (deckBand is null)
        {
            double xa = -Hull.L / 2, xb = Hull.L / 2;
            long k = Math.Max(2L, (long)(xb - xa));
            var xs = Enumerable.Range(0, (int)k + 1).Select(i => xa + (xb - xa) * i / k);
            var band = Thin(xs.Where(x => Hull.HalfWidth(x) - DH_INSET > 0.1).Select(x => new Pt(x, Hull.HalfWidth(x) - DH_INSET)).ToList(), 0.05);
            var all = band.Concat(Enumerable.Reverse(band).Select(p => new Pt(p.X, -p.Y))).ToList();
            deckBand = ConvexHull(all);
        }
        return deckBand;
    }

    // ------------------------------------------------------------------ collision helpers

    public static double SegDist(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay;
        double den = dx * dx + dy * dy;
        if (den == 0)
            den = 1.0;
        double t = Math.Max(0.0, Math.Min(1.0, ((px - ax) * dx + (py - ay) * dy) / den));
        return double.Hypot(px - ax - t * dx, py - ay - t * dy);
    }

    /// <summary>Distance from (px, py) to a pie slice (cx, cy, R, a0, a1), 0 inside.</summary>
    static double SectorDist((double Cx, double Cy, double R, double A0, double A1) sec, double px, double py)
    {
        var (cx, cy, R, a0, a1) = sec;
        double d = double.Hypot(px - cx, py - cy);
        double a = double.RadiansToDegrees(Math.Atan2(py - cy, px - cx));
        bool inside = a1 - a0 >= 360 || Geometry.Normalize360(a - a0) <= a1 - a0;
        if (inside)
            return Math.Max(0.0, d - R);
        double best = double.NaN;
        bool first = true;
        foreach (var b in new[] { a0, a1 })
        {
            double ex = cx + R * Math.Cos(double.DegreesToRadians(b)), ey = cy + R * Math.Sin(double.DegreesToRadians(b));
            double v = SegDist(px, py, cx, cy, ex, ey);
            if (first || v < best)
                best = v;
            first = false;
        }
        return best;
    }

    public static bool Overlap(Footprint a, Footprint b, double margin = 0.0)
    {
        if (a.Kind == 'p' || b.Kind == 'p')
        {
            var (p, o) = a.Kind == 'p' ? (a, b) : (b, a);
            if (o.Kind == 'c')
            {
                var pts = p.Pts!;
                if (Geometry.PointInPolygon(o.X, o.Y, pts))
                    return true;
                double best = double.PositiveInfinity;
                bool first = true;
                for (int i = 0; i < pts.Count; i++)
                {
                    var q0 = pts[(i - 1 + pts.Count) % pts.Count];
                    var q1 = pts[i];
                    double d = SegDist(o.X, o.Y, q0.X, q0.Y, q1.X, q1.Y);
                    if (first || d < best)
                        best = d;
                    first = false;
                }
                return best < o.R + margin;
            }
            if (p.Pts!.Count >= PREP_MIN)
                return p.Prepared.Intersects(o.Points(margin));
            return Geometry.PolygonsIntersect(p.Pts, o.Points(margin));
        }
        if (a.Kind == 'c' && b.Kind == 'c')
            return double.Hypot(a.X - b.X, a.Y - b.Y) < a.R + b.R + margin;
        if (a.Kind == 'r' && b.Kind == 'r')
            return !(a.C + margin <= b.A || b.C + margin <= a.A || a.D + margin <= b.B || b.D + margin <= a.B);
        var (c, r) = a.Kind == 'c' ? (a, b) : (b, a);
        double nx = Math.Min(Math.Max(c.X, r.A), r.C);
        double ny = Math.Min(Math.Max(c.Y, r.B), r.D);
        return double.Hypot(c.X - nx, c.Y - ny) < c.R + margin;
    }

    /// <summary>n of the positions spots, as near c as possible (least sum of |x - c|), spaced sp_same on one side of
    /// x = 0 and sp_cross across it. Sorted, or null if no n fit.</summary>
    public static List<double>? NearestSpaced(IEnumerable<double> spots, int n, double c, double spCross, double spSame)
    {
        var xs = spots.Order().ToList();
        if (n <= 0 || xs.Count < n)
            return n <= 0 ? [] : null;
        int neg = xs.LowerBound(0.0);
        var cost = xs.Select(x => Math.Abs(x - c)).ToList();
        var back = new List<List<int>>();
        for (int step = 0; step < n - 1; step++)
        {
            var pre = new List<(double V, int J)>();
            (double V, int J) run = (double.PositiveInfinity, -1);
            for (int j = 0; j < cost.Count; j++)
            {
                var cand = (cost[j], j);
                if (cand.CompareTo(run) < 0)
                    run = cand;
                pre.Add(run);
            }
            var nxt = new List<double>();
            var ptr = new List<int>();
            for (int j = 0; j < xs.Count; j++)
            {
                double x = xs[j];
                int lim = xs.UpperBound(x - spSame) - 1;
                if (x >= 0.0)
                    lim = Math.Max(lim, Math.Min(xs.UpperBound(x - spCross), neg) - 1);
                var (v, i) = lim >= 0 ? pre[lim] : (double.PositiveInfinity, -1);
                nxt.Add(v + Math.Abs(x - c));
                ptr.Add(i);
            }
            cost = nxt;
            back.Add(ptr);
        }
        int jb = Enumerable.Range(0, xs.Count).MinBy(k => cost[k]);
        if (double.IsPositiveInfinity(cost[jb]))
            return null;
        var out_ = new List<double> { xs[jb] };
        for (int b = back.Count - 1; b >= 0; b--)
        {
            jb = back[b][jb];
            out_.Add(xs[jb]);
        }
        return out_.Order().ToList();
    }
}
