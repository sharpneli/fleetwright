namespace Fleetwright.Shipgen.Render;

/// <summary>clutter: the small gear on a warship's roofs and open decks, drawn only (the design never sees it).
///
/// Ventilators, skylights, hatches, boats on their chocks, Carley floats, ready-use lockers, searchlights, coal
/// scuttles, paravanes and so on, picked by era: a look's shapes key "clutter" names a kit in Kits (Looks.Shapes
/// defaults it to the look's era). Placement is repeatable (seeded by the design's id) and keeps to open roof and
/// deck: never under a higher block, a funnel, a mast, a turret, an AA mount or another item. Most items come in
/// mirrored pairs, as on real ships, so the result reads as fitted out rather than strewn about.
///
/// Nothing here changes the layout, hitboxes, sprite.json or the report. Each item also gives the height map a low
/// column (HeightColumns), so it casts its own small shadow.
///
/// Shapes keys read here: clutter (a Kits name, or null / "" for none), clutter_density (a factor on every kit's
/// counts, 1), roof_planks (0 = steel roofs; otherwise the smallest block area, m^2, whose roof is planked like a
/// deck; the kit's default) and roof_rails (guardrails round open roofs; the kit's default).</summary>
public static class Clutter
{
    /// <summary>An item's size (l along the ship, w across, h tall, all m) and how it is placed:
    /// pair (mirrored about the centreline, anywhere across; a lone one on the centreline when the roof is narrow),
    /// centre (on the centreline), edge (mirrored, just inboard of the roof's or deck's edge) or row (2-4 side by
    /// side, along the edge or, half the time, anywhere across like pair). CentreP: a pair item's chance of one on
    /// the centreline instead. Ends / DeckEnds: kept beyond that fraction of L from amidships (everywhere / on the
    /// open deck only). Near "funnel": within NearFunnel m (along the ship) of a funnel, where the boiler rooms
    /// breathe.</summary>
    sealed record ItemSpec(double L, double W, double H, string Place, double CentreP = 0.0, double Ends = 0.0,
        double DeckEnds = 0.0, string? Near = null);

    static readonly Dictionary<string, ItemSpec> Items = new(StringComparer.Ordinal)
    {
        ["cowl"] = new(1.3, 1.3, 1.8, "pair"),
        ["cowl_small"] = new(0.9, 0.9, 1.2, "pair"),
        ["mushroom"] = new(0.9, 0.9, 0.8, "pair"),
        ["vent_box"] = new(1.8, 1.1, 1.1, "pair"),
        ["skylight"] = new(3.0, 1.5, 0.7, "pair", CentreP: 0.35, DeckEnds: 0.15),
        ["hatch"] = new(1.4, 1.0, 1.0, "pair"),
        ["capstan"] = new(1.3, 1.3, 0.7, "centre", Ends: 0.3),   // forecastle and quarterdeck only
        ["reel"] = new(1.2, 0.9, 1.0, "pair"),
        ["locker"] = new(1.2, 0.8, 0.9, "row"),
        ["float"] = new(2.2, 1.0, 0.5, "row"),
        ["raft"] = new(0.7, 1.5, 0.7, "row"),
        ["searchlight"] = new(1.7, 1.7, 1.6, "pair"),
        ["scuttle"] = new(0.5, 0.5, 0.0, "edge"),
        ["paravane"] = new(3.2, 0.7, 0.4, "edge"),
        ["dc_rack"] = new(4.0, 1.2, 1.0, "edge"),
        // big gear, for the big roofs (kit "big")
        ["boiler_cowl"] = new(2.4, 2.4, 3.2, "pair", Near: "funnel"),
        ["engine_skylight"] = new(6.5, 3.4, 1.0, "pair", CentreP: 0.7),
        ["searchlight_tower"] = new(3.4, 3.4, 2.6, "pair"),
        ["fan_house"] = new(4.2, 2.6, 2.0, "pair", CentreP: 0.4, Near: "funnel"),
    };

    /// <summary>Boats: length and beam, m. Nested boats ride inside a bigger one (Victorian and Great War practice).</summary>
    static readonly Dictionary<string, (double L, double W)> Boats = new(StringComparer.Ordinal)
    {
        ["cutter"] = (8.5, 2.2),
        ["whaler"] = (7.5, 1.9),
        ["gig"] = (9.0, 1.8),
        ["pinnace"] = (11.0, 2.8),   // steam pinnace: a cabin and a little funnel
        ["launch"] = (10.5, 2.8),    // motor launch: a cabin forward
        ["dinghy"] = (4.5, 1.5),
        ["whaleboat"] = (8.0, 2.4),  // motor whaleboat with a canopy forward
    };

    /// <summary>A kit. Roof / Deck: (item, items per 100 m^2 of open surface); Big: (item, items per 100 m^2 of a
    /// roof's area beyond what the small items count, RoofSat); Boats: the boats on big roofs, biggest first;
    /// Nest: a dinghy rides in each cutter; Scuttles: coal scuttles along the deck edges amidships.</summary>
    public sealed record KitSpec((string Item, double Per100)[] Roof, (string Item, double Per100)[] Big,
        (string Item, double Per100)[] Deck, string[] Boats, bool Nest, bool Scuttles, double RoofPlanks, bool RoofRails,
        int MaxBoats = 8);

    static readonly Dictionary<string, KitSpec> Kits = new(StringComparer.Ordinal)
    {
        ["victorian"] = new(
            Roof: [("cowl", 1.6), ("skylight", 0.7), ("hatch", 0.6), ("cowl_small", 1.0)],
            Big: [("boiler_cowl", 0.3), ("engine_skylight", 0.2)],
            Deck: [("cowl", 0.35), ("skylight", 0.25), ("capstan", 0.15), ("hatch", 0.2), ("reel", 0.15)],
            Boats: ["cutter", "pinnace", "gig", "whaler"], Nest: true, Scuttles: true, RoofPlanks: 60.0, RoofRails: true),
        ["great_war"] = new(
            Roof: [("cowl", 1.2), ("searchlight", 0.5), ("skylight", 0.4), ("hatch", 0.5), ("cowl_small", 0.9)],
            Big: [("boiler_cowl", 0.25), ("engine_skylight", 0.15), ("searchlight_tower", 0.1)],
            Deck: [("cowl", 0.3), ("skylight", 0.15), ("capstan", 0.12), ("hatch", 0.2), ("reel", 0.2), ("paravane", 0.05)],
            Boats: ["pinnace", "cutter", "whaler", "pinnace", "gig"], Nest: true, Scuttles: true, RoofPlanks: 60.0,
            RoofRails: true),
        ["treaty"] = new(
            Roof: [("mushroom", 1.2), ("searchlight", 0.45), ("vent_box", 0.5), ("hatch", 0.5), ("float", 0.4)],
            Big: [("fan_house", 0.25), ("searchlight_tower", 0.12), ("engine_skylight", 0.08)],
            Deck: [("mushroom", 0.3), ("capstan", 0.1), ("hatch", 0.2), ("reel", 0.2), ("paravane", 0.06)],
            Boats: ["launch", "cutter", "pinnace", "whaler"], MaxBoats: 6, Nest: false, Scuttles: false, RoofPlanks: 0.0,
            RoofRails: true),
        ["wwii"] = new(
            Roof: [("locker", 1.3), ("float", 1.0), ("vent_box", 0.8), ("mushroom", 0.8), ("hatch", 0.4)],
            Big: [("fan_house", 0.3), ("searchlight_tower", 0.08)],
            Deck: [("locker", 0.25), ("float", 0.25), ("mushroom", 0.25), ("hatch", 0.2), ("reel", 0.2), ("paravane", 0.04),
                   ("dc_rack", 0.0)],
            Boats: ["launch", "whaler"], MaxBoats: 4, Nest: false, Scuttles: false, RoofPlanks: 0.0, RoofRails: true),
        ["cold_war"] = new(
            Roof: [("raft", 1.4), ("vent_box", 1.0), ("mushroom", 0.6), ("hatch", 0.5)],
            Big: [("fan_house", 0.3)],
            Deck: [("raft", 0.08), ("vent_box", 0.2), ("hatch", 0.25), ("reel", 0.25), ("dc_rack", 0.0)],
            Boats: ["whaleboat"], MaxBoats: 2, Nest: false, Scuttles: false, RoofPlanks: 0.0, RoofRails: true),
    };

    public static KitSpec? Kit(string? name) => name != null && Kits.TryGetValue(name, out var k) ? k : null;

    const double Margin = 0.45;      // clear space round every item and from a roof's edge, m
    const double BoatRoofW = 8.0;  // a roof this wide (and wider) carries boats
    // Roof counts grow with area only up to about RoofSat m^2, then with its square root: a long boat deck between
    // the funnels (Kongo, Dante) held boats and a few vents, not gear strewn end to end at the small-roof density.
    // Roofs at level 3 and up (bridge and fire-control platforms) carry HighRoof of the count. Decks keep the
    // plain per-area count.
    const double RoofSat = 600.0;
    const double HighRoof = 0.6;
    // The rest of a big roof's area goes to the kit's big gear, and each m^2 the big gear covers takes BigShare m^2
    // from the area the small items count (the clear space round a fan house or a skylight is part of it).
    const double BigShare = 5.0;
    const double NearFunnel = 7.0;
    const int Candidates = 8;   // valid spots tried per item; the emptiest wins (best-candidate sampling: an even spread)

    /// <summary>One placed item. Boats have Boat (the kind) and Nest; Face turns a cowl's mouth (1 ahead, -1 aft, 0
    /// outboard).</summary>
    public sealed record Item(string Kind, double X, double Y, double L, double W, double H, long Level, string? Boat = null,
        bool Nest = false, int Face = 1);

    // ------------------------------------------------------------------ geometry

    readonly record struct Box(double X0, double Y0, double X1, double Y1);

    static Box Rect(double x, double y, double l, double w) => new(x - l / 2, y - w / 2, x + l / 2, y + w / 2);

    static bool Overlap(Box a, Box b) => a.X0 < b.X1 && b.X0 < a.X1 && a.Y0 < b.Y1 && b.Y0 < a.Y1;

    static Box BBox(IReadOnlyList<Pt> pts)
    {
        var (x0, y0, x1, y1) = (pts.Select(p => p.X).Min(), pts.Select(p => p.Y).Min(), pts.Select(p => p.X).Max(),
            pts.Select(p => p.Y).Max());
        return new(x0, y0, x1, y1);
    }

    /// <summary>Can rectangle r reach bounding box b? (A point outside a polygon's box never tests inside it; eps
    /// keeps a rounded edge crossing from making that a near miss.)</summary>
    static bool Near(Box r, Box b, double eps = 1e-6) =>
        r.X0 <= b.X1 + eps && b.X0 - eps <= r.X1 && r.Y0 <= b.Y1 + eps && b.Y0 - eps <= r.Y1;

    /// <summary>Points over the rectangle r, its corners and edges included.</summary>
    static List<Pt> Samples(Box r, double step = 0.6)
    {
        long nx = Math.Max(1L, (long)Math.Ceiling((r.X1 - r.X0) / step));
        long ny = Math.Max(1L, (long)Math.Ceiling((r.Y1 - r.Y0) / step));
        var result = new List<Pt>((int)((nx + 1) * (ny + 1)));
        for (long i = 0; i <= nx; i++)
            for (long j = 0; j <= ny; j++)
                result.Add(new(r.X0 + (r.X1 - r.X0) * i / nx, r.Y0 + (r.Y1 - r.Y0) * j / ny));
        return result;
    }

    /// <summary>The polygon moved inward by d (each edge offset, neighbours intersected); fine for the gentle outlines
    /// of superstructure blocks.</summary>
    public static List<Pt> InsetPolygon(IReadOnlyList<Pt> pts, double d)
    {
        int n = pts.Count;
        double area2 = Enumerable.Range(0, n).Select(i => pts[i].X * pts[(i + 1) % n].Y - pts[(i + 1) % n].X * pts[i].Y).Sum();
        int sgn = area2 > 0 ? 1 : -1;
        var lines = new List<(Pt P, Pt R)>(n);
        for (int i = 0; i < n; i++)
        {
            var (x0, y0) = pts[i];
            var (x1, y1) = pts[(i + 1) % n];
            double dx = x1 - x0, dy = y1 - y0;
            double ln = double.Hypot(dx, dy);
            if (ln == 0)
                ln = 1e-9;
            double nx = -dy / ln * sgn, ny = dx / ln * sgn;     // inward normal
            lines.Add((new(x0 + nx * d, y0 + ny * d), new(dx, dy)));
        }
        var result = new List<Pt>(n);
        for (int i = 0; i < n; i++)
        {
            var (p, r) = lines[(i - 1 + n) % n];
            var (q, s) = lines[i];
            double den = r.X * s.Y - r.Y * s.X;
            if (Math.Abs(den) < 1e-9)
            {
                result.Add(q);
                continue;
            }
            double t = ((q.X - p.X) * s.Y - (q.Y - p.Y) * s.X) / den;
            result.Add(new(p.X + t * r.X, p.Y + t * r.Y));
        }
        return result;
    }

    /// <summary>Where the line at y crosses the polygon's edges, sorted: PointInPolygon(x, y, pts) is an odd count of
    /// them past x (RowInside), with the same arithmetic, for testing many points on one row.</summary>
    static List<double> RowCrossings(double y, IReadOnlyList<Pt> pts)
    {
        var result = new List<double>();
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            var (x1, y1) = pts[i];
            var (x2, y2) = pts[(i + 1) % n];
            if ((y1 > y) != (y2 > y))
                result.Add(x1 + (y - y1) * (x2 - x1) / (y2 - y1));
        }
        result.Sort();
        return result;
    }

    static bool RowInside(double x, List<double> crossings) => (crossings.Count - crossings.UpperBound(x)) % 2 == 1;

    /// <summary>An open roof (a superstructure block) or the deck, as a polygon plus what stands higher on it.</summary>
    sealed class Surface
    {
        public readonly List<Pt> Pts;
        public readonly long Level;
        public readonly string Kind;
        public readonly Box BBox;
        public double Area;
        public readonly List<Pt> Inner;
        public readonly Box? InnerBox;

        public Surface(List<Pt> pts, long level, string kind)
        {
            Pts = pts;
            Level = level;
            Kind = kind;
            BBox = Clutter.BBox(pts);
            Area = Geometry.PolygonArea(pts);
            Inner = pts.Count >= 3 ? InsetPolygon(pts, Margin) : pts;
            InnerBox = Inner.Count > 0 ? Clutter.BBox(Inner) : null;
        }

        public bool Holds(Box r)
        {
            if (InnerBox is Box b && !(b.X0 - 1e-6 <= r.X0 && r.X1 <= b.X1 + 1e-6 && b.Y0 - 1e-6 <= r.Y0 && r.Y1 <= b.Y1 + 1e-6))
                return false;   // every corner is a sample: one outside the box fails
            return Samples(r).All(p => Geometry.PointInPolygon(p.X, p.Y, Inner));
        }
    }

    sealed class Placer
    {
        public readonly TopView View;
        public readonly Hull Hull;
        public readonly List<(long Level, List<Pt> Poly, Box Box)> BlockPolys = [];
        public readonly List<(List<Pt> Poly, Box Box)> Directors = [];
        public readonly List<(double X, double Y, double R)> Circles = [];
        public readonly List<Box> Rects = [];
        public readonly List<(double X, double L)> Funnels = [];
        public readonly List<Box> Taken = [];

        public Placer(TopView v)
        {
            View = v;
            Hull = v.Hull;
            var hull = v.Hull;
            var spec = v.Dressing;
            foreach (var b in v.Blocks)
            {
                var poly = b.Outline;
                if (b.Director != null)
                    Directors.Add((poly, BBox(poly)));
                else
                    BlockPolys.Add((b.Level, poly, BBox(poly)));
            }
            foreach (var m in v.Mounts)
                Circles.Add((m.X, m.Y, v.TurretTypes[m.Type].R * 1.2));
            foreach (var a in v.AaMounts)
                Circles.Add((a.X, a.Y, 2.4));
            foreach (var fn in v.Funnels)
                Funnels.Add((fn.X, fn.L));
            foreach (var fn in v.Funnels)
                Rects.Add(Rect(fn.X, fn.Y, fn.L + 1.6, fn.W + 1.6));
            foreach (var m in v.Masts)
            {
                double y = m.Y;
                Circles.Add((m.X, y, 1.4));
                if (m.Tripod)   // the legs run aft; keep them clear whichever look draws them
                    Rects.Add(new(m.X - 5.5, y - 4.0, m.X + 0.5, y + 4.0));
            }
            foreach (var (x, y, l, w) in (spec.Boats ?? []).Select(b => (b.X, b.Y, b.L, b.W)).Concat((spec.Fittings ?? []).Select(f => (f.X, f.Y, f.L, f.W)))
                         .Concat((spec.Hatches ?? []).Select(h => (h.X, h.Y, h.L, h.W))))
                Rects.Add(Rect(x, y, l + 0.8, w + 0.8));
            foreach (var c in spec.Cranes ?? [])
                Circles.Add((c.X, c.Y, 2.0));
            foreach (double bx in spec.Bollards ?? [])   // a pair of bitts each side by the deck edge
            {
                double by = hull.HalfWidth(bx) - 1.1;
                foreach (int s in new[] { -1, 1 })
                    Rects.Add(new(bx - 0.6, s * by - 0.6, bx + 1.5, s * by + 0.6));
            }
            if (spec.ChainX is double cx && cx != 0)
                Rects.Add(new(cx - 1.5, -hull.B, hull.L, hull.B));
            if (spec.BreakwaterX is double bw && bw != 0)
                Rects.Add(new(bw - hull.B * 0.4, -hull.B, bw + 1.0, hull.B));
        }

        public bool Free(Surface surf, Box r)
        {
            if (!surf.Holds(r))
                return false;
            var g = new Box(r.X0 - Margin, r.Y0 - Margin, r.X1 + Margin, r.Y1 + Margin);
            if (Taken.Any(t => Overlap(g, t)) || Rects.Any(t => Overlap(g, t)))
                return false;
            foreach (var (cx, cy, cr) in Circles)
            {
                double dx = Math.Max(g.X0 - cx, Math.Max(0, cx - g.X1));
                double dy = Math.Max(g.Y0 - cy, Math.Max(0, cy - g.Y1));
                if (dx * dx + dy * dy < cr * cr)
                    return false;
            }
            var pts = Samples(g, 0.5);
            foreach (var (lvl, poly, box) in BlockPolys)
                if ((surf.Kind == "deck" || lvl > surf.Level) && Near(g, box) && pts.Any(p => Geometry.PointInPolygon(p.X, p.Y, poly)))
                    return false;
            foreach (var (poly, box) in Directors)
                if (Near(g, box) && pts.Any(p => Geometry.PointInPolygon(p.X, p.Y, poly)))
                    return false;
            if (surf.Kind == "deck" && DeckLevel(g.X0) != DeckLevel(g.X1))   // astride a break in the deck
                return false;
            return true;
        }

        long DeckLevel(double x) =>
            View.RaisedDecks.Where(rd => rd.X0 <= x && x <= rd.X1).Select(rd => rd.Levels).DefaultIfEmpty(0L).Max();
    }

    // ------------------------------------------------------------------ placement

    static List<Surface> Surfaces(TopView v)
    {
        var hull = v.Hull;
        var result = v.Blocks.Where(b => b.Director == null).Select(b => new Surface(b.Outline, b.Level, "roof")).ToList();
        // the deck as a polygon a little inside its edge
        const int n = 80;
        var xs = Enumerable.Range(0, n).Select(i => -hull.L / 2 + hull.L * (i + 0.5) / n).ToList();
        var pts = xs.Select(x => new Pt(x, -Math.Max(0.0, hull.HalfWidth(x) - 0.6))).ToList();
        pts.AddRange(Enumerable.Reverse(xs).Select(x => new Pt(x, Math.Max(0.0, hull.HalfWidth(x) - 0.6))));
        result.Add(new Surface(pts, 0, "deck"));
        return result;
    }

    /// <summary>Candidate y positions (each a set placed together) for an item at x.</summary>
    static double[]? MirrorYs(Surface surf, double x, double l, double w, string place, ShipRng rng, double centreP = 0.0)
    {
        double lo = 0, hi = 0;
        bool first = true;
        foreach (double xx in new[] { x - l / 2, x, x + l / 2 })
        {
            if (Geometry.PolygonYSpan(surf.Inner, xx) is not { } sp)
                return null;
            (lo, hi) = first ? (sp.Lo, sp.Hi) : (Math.Max(lo, sp.Lo), Math.Min(hi, sp.Hi));
            first = false;
        }
        if (hi - lo < w)
            return null;
        bool onCentre = lo + w / 2 <= 0 && 0 <= hi - w / 2;
        if (place == "centre" || (onCentre && rng.Random() < centreP))
            return onCentre ? [0.0] : null;
        double edge = Math.Min(-lo, hi) - w / 2 - 0.05;
        if (place == "edge" || (place == "row" && rng.Random() < 0.5))
            return edge > w / 2 ? [-edge, edge] : null;
        if (edge > w / 2 + Margin)   // anywhere across, mirrored
        {
            double y = rng.Uniform(w / 2 + Margin, edge);
            return [-y, y];
        }
        return onCentre ? [0.0] : null;   // too narrow for a pair: one on the centreline
    }

    /// <summary>How far a candidate stands from what's already placed, the obstacles, its mirror twin and the
    /// surface's edge (counted double, so items don't all run to the sides). Bigger is emptier.</summary>
    static double Spread(Placer P, Surface surf, double[] xs, double[] ys)
    {
        double best = 1e9;
        foreach (double x in xs)
            foreach (double y in ys)
            {
                foreach (var r in P.Taken)
                    best = Math.Min(best, double.Hypot(x - (r.X0 + r.X1) / 2, y - (r.Y0 + r.Y1) / 2));
                foreach (var (cx, cy, cr) in P.Circles)
                    best = Math.Min(best, double.Hypot(x - cx, y - cy) - cr);
                if (ys.Length == 2)
                    best = Math.Min(best, 2 * Math.Abs(y));
                if (Geometry.PolygonYSpan(surf.Inner, x) is { } sp)
                    best = Math.Min(best, 2 * Math.Min(y - sp.Lo, sp.Hi - y) + 1.0);
            }
        return best;
    }

    /// <summary>The area the kit's small counts scale with (RoofSat, HighRoof).</summary>
    static double OpenArea(Surface surf)
    {
        if (surf.Kind == "deck")
            return surf.Area;
        double a = surf.Area <= RoofSat ? surf.Area : Math.Sqrt(surf.Area * RoofSat);
        return a * (surf.Level >= 3 ? HighRoof : 1.0);
    }

    /// <summary>The area the kit's big counts scale with: what RoofSat left out of the small count.</summary>
    static double BigArea(Surface surf)
    {
        if (surf.Kind == "deck" || surf.Area <= RoofSat)
            return 0.0;
        return (surf.Area - Math.Sqrt(surf.Area * RoofSat)) * (surf.Level >= 3 ? HighRoof : 1.0);
    }

    /// <summary>Scatter the kit's items over one surface: (kind, x, y), spread evenly over the open area. Counts are
    /// per 100 m^2 of area.</summary>
    static List<(string Kind, double X, double Y)> PlaceItems(Placer P, Surface surf, (string Item, double Per100)[] kitItems,
        double density, ShipRng rng, double area)
    {
        var result = new List<(string, double, double)>();
        double x0 = surf.BBox.X0, x1 = surf.BBox.X1;
        double L = P.Hull.L;
        foreach (var (kind, per100) in kitItems)
        {
            var it = Items[kind];
            var near = it.Near != null
                ? P.Funnels.Where(f => f.X + f.L / 2 + NearFunnel > x0 && f.X - f.L / 2 - NearFunnel < x1)
                    .Select(f => (A: f.X - f.L / 2 - NearFunnel, B: f.X + f.L / 2 + NearFunnel)).ToList()
                : [];
            if (it.Near != null && near.Count == 0)
                continue;
            double want = area / 100.0 * per100 * density;
            long n = (long)want + (rng.Random() < want - (long)want ? 1 : 0);
            int fails = 0;
            while (n > 0 && fails < 3)
            {
                var cands = new List<(double Spread, double[] Xs, double[] Ys, List<Box> Rs)>();
                for (int tries = 0; tries < 40; tries++)
                {
                    if (cands.Count >= Candidates)
                        break;
                    double x;
                    if (near.Count > 0)
                    {
                        var (a, b) = rng.Choice(near);
                        x = rng.Uniform(Math.Max(a, x0), Math.Min(b, x1));
                    }
                    else
                        x = rng.Uniform(x0, x1);
                    double ends = it.Ends != 0 ? it.Ends : surf.Kind == "deck" ? it.DeckEnds : 0.0;
                    if (Math.Abs(x) < ends * L)
                        continue;
                    if (MirrorYs(surf, x, it.L, it.W, it.Place, rng, it.CentreP) is not { } ys)
                        continue;
                    int count = it.Place == "row" ? rng.Choice([2, 3, 3, 4]) : 1;
                    var xs = Enumerable.Range(0, count).Select(k => x + (k - (count - 1) / 2.0) * (it.L + 0.25)).ToArray();
                    var rs = xs.SelectMany(xx => ys.Select(y => Rect(xx, y, it.L, it.W))).ToList();
                    if (rs.All(r => P.Free(surf, r)))
                        cands.Add((Spread(P, surf, xs, ys), xs, ys, rs));
                }
                if (cands.Count == 0)
                {
                    fails++;
                    continue;
                }
                var best = cands.MaxBy(c => c.Spread);
                P.Taken.AddRange(best.Rs);
                foreach (var xx in best.Xs)
                    foreach (var y in best.Ys)
                        result.Add((kind, xx, y));
                n -= best.Rs.Count;   // every item counts, a row of four as four
            }
        }
        return result;
    }

    /// <summary>Boats in rows along a wide roof (the boat deck): an outboard pair of rows, then the centreline, each
    /// filled from aft to fore with the kit's boats in turn, as many as fit (up to the kit's MaxBoats).</summary>
    static List<(string Name, double X, double Y)> PlaceBoats(Placer P, Surface surf, KitSpec kit, ShipRng rng)
    {
        var result = new List<(string, double, double)>();
        double x0 = surf.BBox.X0, x1 = surf.BBox.X1;
        var pool = kit.Boats;
        int k = rng.RandRange(pool.Length);
        foreach (var row in new[] { "outboard", "centre" })
        {
            double x = x0;
            while (x < x1 && result.Count < kit.MaxBoats)
            {
                bool placed = false;
                for (int j = 0; j < pool.Length; j++)
                {
                    string name = pool[(k + j) % pool.Length];
                    var (l, w) = Boats[name];
                    double cx = x + l / 2;
                    var sp = new[] { cx - l / 2, cx, cx + l / 2 }.Select(xx => Geometry.PolygonYSpan(surf.Inner, xx)).ToList();
                    if (sp.Any(s => s == null))
                        continue;
                    double edge = sp.Select(s => Math.Min(-s!.Value.Lo, s.Value.Hi)).Min() - w / 2 - 0.2;
                    double[] ys;
                    if (row == "outboard")
                    {
                        if (edge < w / 2 + 0.6)
                            continue;
                        ys = [-edge, edge];
                    }
                    else
                        ys = [0.0];
                    var rs = ys.Select(y => Rect(cx, y, l + 0.2, w + 0.2)).ToList();
                    if (rs.All(r => P.Free(surf, r)))
                    {
                        P.Taken.AddRange(rs);
                        foreach (var y in ys)
                            result.Add((name, cx, y));
                        k += j + 1;
                        x += l + 0.6;
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                    x += 0.5;
            }
        }
        return result;
    }

    /// <summary>Every clutter item for the ship. Repeatable.</summary>
    public static List<Item> Plan(TopView v, Shapes shapes)
    {
        var kitName = shapes.Clutter;
        var kit = Kit(kitName ?? "");
        if (kit == null)
            return [];
        double density = shapes.ClutterDensity ?? 1.0;
        var rng = new ShipRng($"{v.Id}/clutter/{kitName}");
        var hull = v.Hull;
        var P = new Placer(v);
        var items = new List<Item>();
        var surfs = Surfaces(v);
        bool flight = v.FlightDeck != null;
        foreach (var s in surfs)   // what's left open once the blocks above are drawn over it
        {
            var (x0, y0, x1, y1) = (s.BBox.X0, s.BBox.Y0, s.BBox.X1, s.BBox.Y1);     // a 1 m grid, a row at a time
            var above = P.BlockPolys.Where(b => s.Kind == "deck" || b.Level > s.Level).Select(b => b.Poly).ToList();
            s.Area = 0;
            long rows = (long)(y1 - y0), cols = (long)(x1 - x0);
            for (long j = 0; j < rows; j++)
            {
                double y = y0 + 0.5 + j;
                var own = RowCrossings(y, s.Pts);
                var over = above.Select(poly => RowCrossings(y, poly)).Where(c => c.Count > 0).ToList();
                for (long i = 0; i < cols; i++)
                {
                    double x = x0 + 0.5 + i;
                    if (RowInside(x, own) && !over.Any(c => RowInside(x, c)))
                        s.Area += 1;
                }
            }
        }
        // boats first, on the widest open roofs (the boat deck)
        if (kit.Boats.Length > 0 && !flight)
        {
            var roofs = surfs.Where(s => s.Kind == "roof" && s.BBox.Y1 - s.BBox.Y0 >= BoatRoofW && s.Area >= 60).OrderBy(s => -s.Area).ToList();
            foreach (var s in roofs.Take(1))
                foreach (var (name, x, y) in PlaceBoats(P, s, kit, rng))
                {
                    var (l, w) = Boats[name];
                    items.Add(new Item("boat", x, y, l, w, 1.4, s.Level, Boat: name, Nest: kit.Nest));
                }
        }
        foreach (var s in surfs)
        {
            if (flight && s.Kind == "deck")
                continue;
            double area = OpenArea(s);
            List<(string Kind, double X, double Y)> big;
            if (s.Kind == "roof" && kit.Big.Length > 0 && BigArea(s) > 0)   // the big gear first, then less small gear
            {
                big = PlaceItems(P, s, kit.Big, density, rng, BigArea(s));
                area = Math.Max(0.0, area - BigShare * big.Select(b => Items[b.Kind].L * Items[b.Kind].W).Sum());
            }
            else
                big = [];
            var small = PlaceItems(P, s, s.Kind == "roof" ? kit.Roof : kit.Deck, density, rng, area);
            foreach (var (kind, x, y) in big.Concat(small))
            {
                var it = Items[kind];
                items.Add(new Item(kind, x, y, it.L, it.W, it.H, s.Level, Face: rng.Choice([1, 1, -1, 0])));
            }
        }
        var deck = surfs[^1];
        if (kit.Scuttles && !flight)   // coal scuttles in pairs along the sides amidships
        {
            double L = hull.L;
            double x = -0.2 * L;
            while (x < 0.2 * L)
            {
                double hw = hull.HalfWidth(x) - 1.3;
                var rs = new[] { -1, 1 }.Select(s => Rect(x, s * hw, 0.5, 0.5)).ToList();
                if (hw > 2 && rs.All(r => P.Free(deck, r)))
                {
                    P.Taken.AddRange(rs);
                    foreach (int s in new[] { -1, 1 })
                        items.Add(new Item("scuttle", x, s * hw, 0.5, 0.5, 0.0, 0));
                }
                x += 4.5;
            }
        }
        if (kit.Deck.Any(d => d.Item == "dc_rack") && hull.L < 140 && !flight)   // depth charges at the stern
            foreach (double x in new[] { -hull.L / 2 + 3.0, -hull.L / 2 + 4.5, -hull.L / 2 + 6.0 })
            {
                double hw = Math.Min(hull.HalfWidth(x - 2.0), hull.HalfWidth(x + 2.0)) - 1.3;
                var rs = new[] { -1, 1 }.Select(s => Rect(x, s * (hw - 0.6), 4.0, 1.2)).ToList();
                if (hw > 2 && rs.All(r => P.Free(deck, r)))
                {
                    P.Taken.AddRange(rs);
                    foreach (int s in new[] { -1, 1 })
                        items.Add(new Item("dc_rack", x, s * (hw - 0.6), 4.0, 1.2, 1.0, 0));
                    break;
                }
            }
        return items;
    }

    // ------------------------------------------------------------------ drawing

    /// <summary>One item. surfaceCol: the colour it stands on.</summary>
    public static void Draw(List<Node> o, Item it, Painter P, string surfaceCol)
    {
        var p = P.P;
        double x = it.X, y = it.Y, l = it.L, w = it.W;
        string k = it.Kind;
        string Shade(string c, double f) => Painter.Shade(c, f);
        string metal = Shade(surfaceCol, 0.8);
        const string dark = "#1d2125";
        string fitting = P.C("fitting");
        T St<T>(T n) where T : Node => P.Ln(n, 0.6);
        string Pal(string key, string def) => p.Or(key, def);
        switch (k)
        {
            case "cowl" or "cowl_small" or "boiler_cowl":   // a cowl ventilator: the round trunk and its bell mouth turned to the wind
                {
                    double r = l / 2;
                    int fx = it.Face;
                    var (mx, my) = fx != 0 ? (x + fx * r * 0.25, y) : (x, y + (y >= 0 ? r * 0.25 : -r * 0.25));
                    o.Add(St(new CircleNode(x, y, r).Fill(Shade(surfaceCol, 0.92))));
                    o.Add(new EllipseNode(mx, my, r * (fx != 0 ? 0.45 : 0.62), r * (fx != 0 ? 0.62 : 0.45)).Fill(dark));
                    return;
                }
            case "mushroom":
                {
                    double r = l / 2;
                    o.Add(St(new CircleNode(x, y, r).Fill(metal)));
                    o.Add(new CircleNode(x, y, r * 0.45).Fill(Shade(surfaceCol, 1.08)));
                    return;
                }
            case "vent_box":
                o.Add(St(new RectNode(x - l / 2, y - w / 2, l, w, 0.1).Fill(metal)));
                for (int i = 1; i < 4; i++)
                {
                    double xx = x - l / 2 + l * i / 4;
                    o.Add(new LineNode(xx, y - w / 2 + 0.15, xx, y + w / 2 - 0.15).Stroke(Shade(metal, 0.7), P.Sw * 0.8));
                }
                return;
            case "skylight" or "engine_skylight":   // a pitched glazed skylight: frame, two glass slopes and the glazing bars
                {
                    string glass = Pal("glass", "#5f7782");
                    o.Add(St(new RectNode(x - l / 2, y - w / 2, l, w, 0.1).Fill(fitting)));
                    o.Add(new RectNode(x - l / 2 + 0.2, y - w / 2 + 0.2, l - 0.4, w / 2 - 0.2).Fill(Shade(glass, 1.15)));
                    o.Add(new RectNode(x - l / 2 + 0.2, y, l - 0.4, w / 2 - 0.2).Fill(glass));
                    long n = Math.Max(2L, (long)(l / 0.6));
                    for (int i = 1; i < n; i++)
                    {
                        double xx = x - l / 2 + l * i / n;
                        o.Add(new LineNode(xx, y - w / 2 + 0.2, xx, y + w / 2 - 0.2).Stroke(fitting, P.Sw * 0.7));
                    }
                    return;
                }
            case "hatch":   // a companionway: a box with its sliding hood
                o.Add(St(new RectNode(x - l / 2, y - w / 2, l, w, 0.1).Fill(Shade(surfaceCol, 0.88))));
                o.Add(P.Ln(new PathNode(Painter.RrectPath(x - l / 2 + 0.15, y - w / 2 + 0.15, x + 0.1, y + w / 2 - 0.15, 0.0, 0.35))
                    .Fill(Shade(surfaceCol, 1.08)), 0.4));
                return;
            case "capstan":
                {
                    double r = l / 2;
                    o.Add(St(new CircleNode(x, y, r).Fill(fitting)));
                    o.Add(new CircleNode(x, y, r * 0.55).Fill(Shade(fitting, 1.3)));
                    for (int a = 0; a < 360; a += 45)
                        o.Add(new LineNode(x, y, x + r * Math.Cos(double.DegreesToRadians(a)), y + r * Math.Sin(double.DegreesToRadians(a)))
                            .Stroke(Shade(fitting, 0.6), P.Sw * 0.7));
                    return;
                }
            case "reel":   // a hose or wire reel on its frame
                o.Add(St(new RectNode(x - l / 2, y - w / 2, l, w).Fill(fitting)));
                o.Add(new RectNode(x - l / 2 + 0.2, y - w / 2 + 0.1, l - 0.4, w - 0.2).Fill(Shade(fitting, 0.65)));
                for (int i = 1; i < 5; i++)
                    o.Add(new LineNode(x - l / 2 + 0.2, y - w / 2 + w * i / 5, x + l / 2 - 0.2, y - w / 2 + w * i / 5)
                        .Stroke(Shade(fitting, 1.2), P.Sw * 0.6));
                return;
            case "locker":   // a ready-use ammunition locker
                o.Add(St(new RectNode(x - l / 2, y - w / 2, l, w, 0.08).Fill(Shade(surfaceCol, 0.9))));
                o.Add(new LineNode(x, y - w / 2, x, y + w / 2).Stroke(Shade(surfaceCol, 0.65), P.Sw * 0.7));
                return;
            case "float":   // a Carley float: an oval ring round its net
                {
                    string col = Pal("float", "#b7a87e");
                    o.Add(St(new PathNode(Painter.RrectPath(x - l / 2, y - w / 2, x + l / 2, y + w / 2, w / 2, w / 2)).Fill(col)));
                    o.Add(new PathNode(Painter.RrectPath(x - l / 2 + 0.25, y - w / 2 + 0.25, x + l / 2 - 0.25, y + w / 2 - 0.25,
                        w / 2 - 0.25, w / 2 - 0.25)).Fill(Shade(col, 0.55)));
                    return;
                }
            case "raft":   // an inflatable liferaft canister lying across its cradle
                {
                    string col = Pal("raft", "#e9e7e0");
                    o.Add(St(new PathNode(Painter.RrectPath(x - l / 2, y - w / 2, x + l / 2, y + w / 2, l / 2, l / 2)).Fill(col)));
                    o.Add(new LineNode(x - l / 2, y, x + l / 2, y).Stroke(Shade(col, 0.7), P.Sw * 0.7));
                    return;
                }
            case "searchlight":   // a round platform with its lamp
                {
                    double r = l / 2;
                    o.Add(St(new CircleNode(x, y, r).Fill(Shade(surfaceCol, 0.85))));
                    o.Add(P.Ln(new CircleNode(x, y, r * 0.5).Fill(fitting), 0.5));
                    o.Add(new CircleNode(x + r * 0.28, y, r * 0.3).Fill("#d9dccf"));
                    return;
                }
            case "searchlight_tower":   // a raised platform with its rail, the lamp on it
                {
                    double r = l / 2;
                    o.Add(St(new CircleNode(x, y, r).Fill(Shade(surfaceCol, 0.8))));
                    o.Add(new CircleNode(x, y, r - 0.2).Fill("none").Stroke(P.C("mast"), Math.Max(0.1, P.Sw * 0.8))
                        .Dash(0.12, 0.9).StrokeOp(0.8));
                    o.Add(P.Ln(new CircleNode(x, y, r * 0.4).Fill(fitting), 0.5));
                    o.Add(new CircleNode(x + r * 0.2, y, r * 0.24).Fill("#d9dccf"));
                    return;
                }
            case "fan_house":   // a boiler-room fan house: a steel box, louvres down both sides, a cap on top
                o.Add(St(new RectNode(x - l / 2, y - w / 2, l, w, 0.15).Fill(metal)));
                o.Add(P.Ln(new RectNode(x - l / 2 + 0.5, y - w / 2 + 0.55, l - 1.0, w - 1.1, 0.1).Fill(Shade(metal, 1.12)), 0.5));
                for (int i = 1; i < 9; i++)
                {
                    double xx = x - l / 2 + l * i / 9;
                    foreach (var (ya, yb) in new[] { (y - w / 2 + 0.1, y - w / 2 + 0.45), (y + w / 2 - 0.45, y + w / 2 - 0.1) })
                        o.Add(new LineNode(xx, ya, xx, yb).Stroke(dark, P.Sw * 0.8).StrokeOp(0.7));
                }
                return;
            case "scuttle":
                o.Add(P.Ln(new CircleNode(x, y, 0.25).Fill(fitting), 0.5));
                o.Add(new CircleNode(x, y, 0.13).Fill(dark));
                return;
            case "paravane":   // a paravane on the deck edge: a stubby torpedo with its fins
                {
                    string col = Pal("paravane", Shade(fitting, 1.35));
                    o.Add(St(new PathNode(Painter.RrectPath(x - l / 2, y - 0.2, x + l / 2, y + 0.2, 0.2, 0.2)).Fill(col)));
                    o.Add(P.Ln(new PathNode(new PathData().M(x - l / 2 + 0.3, y - w / 2).L(x - l / 2 + 0.9, y).L(x - l / 2 + 0.3, y + w / 2).Z())
                        .Fill(col), 0.5));
                    return;
                }
            case "dc_rack":   // a depth-charge rack: two rails and the drums between them
                o.Add(new RectNode(x - l / 2, y - w / 2, l, w).Fill("none").Stroke(fitting, P.Sw * 1.2));
                for (int i = 0; i < 5; i++)
                    o.Add(P.Ln(new CircleNode(x - l / 2 + 0.4 + i * (l - 0.8) / 4, y, 0.38).Fill(Shade(fitting, 0.8)), 0.5));
                return;
            case "boat":
                DrawBoat(o, it, P);
                return;
        }
    }

    static PathData BoatPath(double x, double y, double l, double w, bool dbl = false)
    {
        double x0 = x - l / 2, x1 = x + l / 2;
        if (dbl)   // a whaler: pointed both ends
            return new PathData().M(x0, y).C(x0 + 0.25 * l, y - 0.55 * w, x1 - 0.25 * l, y - 0.55 * w, x1, y)
                .C(x1 - 0.25 * l, y + 0.55 * w, x0 + 0.25 * l, y + 0.55 * w, x0, y).Z();
        return new PathData().M(x0, y - 0.36 * w).C(x0 + 0.5 * l, y - 0.56 * w, x0 + 0.85 * l, y - 0.42 * w, x1, y)
            .C(x0 + 0.85 * l, y + 0.42 * w, x0 + 0.5 * l, y + 0.56 * w, x0, y + 0.36 * w).Z();
    }

    /// <summary>A ship's boat on its chocks, bow forward: the hull, the inside, thwarts, and per kind a cabin, funnel
    /// or canopy. Victorian and Great War cutters carry a dinghy nested inside.</summary>
    static void DrawBoat(List<Node> o, Item it, Painter P)
    {
        double x = it.X, y = it.Y, l = it.L, w = it.W;
        string kind = it.Boat!;
        string col = P.C("boat");
        bool dbl = kind is "whaler" or "gig";
        foreach (double cx in new[] { x - 0.28 * l, x + 0.22 * l })   // chocks
            o.Add(P.Ln(new RectNode(cx - 0.2, y - w / 2 - 0.25, 0.4, w + 0.5).Fill(P.C("fitting")), 0.5));
        o.Add(P.Ln(new PathNode(BoatPath(x, y, l, w, dbl)).Fill(col)));
        if (kind is "pinnace" or "launch" or "whaleboat")   // decked boats
        {
            o.Add(new PathNode(BoatPath(x, y, l - 0.6, w - 0.5)).Fill(Painter.Shade(col, 0.9)));
            if (kind == "pinnace")
            {
                o.Add(P.Ln(new RectNode(x - 0.35 * l, y - 0.3 * w, 0.3 * l, 0.6 * w, 0.2).Fill(Painter.Shade(col, 1.05)), 0.6));
                o.Add(P.Ln(new CircleNode(x + 0.05 * l, y, 0.16 * w).Fill(P.C("funnel")), 0.6));
                o.Add(new CircleNode(x + 0.05 * l, y, 0.09 * w).Fill(P.P.Or("funnel_cap", "#222")));
            }
            else if (kind == "launch")
                o.Add(P.Ln(new RectNode(x - 0.05 * l, y - 0.3 * w, 0.32 * l, 0.6 * w, 0.25).Fill(Painter.Shade(col, 1.06)), 0.6));
            else
                o.Add(P.Ln(new PathNode(BoatPath(x + 0.12 * l, y, 0.62 * l, w - 0.4)).Fill(Painter.Shade(col, 0.78)), 0.5));
            return;
        }
        o.Add(new PathNode(BoatPath(x, y, l - 0.5, w - 0.45, dbl)).Fill(Painter.Shade(col, 0.72)));
        for (int i = 1; i < 5; i++)   // thwarts
        {
            double tx = x - l / 2 + l * i / 5;
            o.Add(new LineNode(tx, y - 0.38 * w, tx, y + 0.38 * w).Stroke(Painter.Shade(col, 0.9), Math.Max(0.15, P.Sw)));
        }
        if (it.Nest && kind == "cutter")   // a dinghy stowed inside
        {
            double dl = 0.5 * l, dw = 0.62 * w;
            o.Add(P.Ln(new PathNode(BoatPath(x - 0.05 * l, y, dl, dw)).Fill(Painter.Shade(col, 1.04)), 0.6));
            o.Add(new PathNode(BoatPath(x - 0.05 * l, y, dl - 0.35, dw - 0.3)).Fill(Painter.Shade(col, 0.8)));
        }
    }

    /// <summary>A block's roof: planked like a deck (Victorian boat decks and the like) inside a steel waterway, and a
    /// guardrail round its edge. Drawn over the block, under whatever stands on it.</summary>
    public static void RoofFinish(List<Node> o, TopView.Block b, Painter P, double areaMin, bool rails, string wood)
    {
        var pts = b.Outline;
        if (pts.Count < 3)
            return;
        long lvl = b.Level;
        if (areaMin != 0 && Geometry.PolygonArea(pts) >= areaMin)
        {
            var inner = InsetPolygon(pts, 0.7);
            string col = Painter.Shade(wood, Math.Pow(1.03, lvl));
            double xmin = inner.Select(q => q.X).Min(), xmax = inner.Select(q => q.X).Max();
            double ymin = inner.Select(q => q.Y).Min(), ymax = inner.Select(q => q.Y).Max();
            var g = new Group { Clip = [new PathNode(Painter.Poly(inner))] }.Stroke(P.C("deck_line"), P.Sw * 0.6).StrokeOp(0.4);
            double yy = ymin;
            while (yy < ymax)
            {
                g.Items.Add(new LineNode(xmin, yy, xmax, yy));
                yy += 1.0;
            }
            double xx = xmin;
            while (xx < xmax)
            {
                g.Items.Add(new LineNode(xx, ymin, xx, ymax).Dash(1, 2));
                xx += 7.0;
            }
            o.Add(new PathNode(Painter.Poly(inner)).Fill(col));
            o.Add(g);
        }
        if (rails && Geometry.PolygonArea(pts) >= 12.0)
        {
            var rl = Painter.Poly(InsetPolygon(pts, 0.2));
            o.Add(new PathNode(rl).Fill("none").Stroke(P.C("mast"), Math.Max(0.06, P.Sw * 0.5)).StrokeOp(0.55));
            o.Add(new PathNode(rl).Fill("none").Stroke(P.C("mast"), Math.Max(0.14, P.Sw * 1.1)).Dash(0.12, 1.4).StrokeOp(0.7));
        }
    }

    /// <summary>Height-map columns for the items, each standing on whatever column is tallest under its centre.</summary>
    public static List<HeightColumn> HeightColumns(List<Item> items, IReadOnlyList<HeightColumn> columns, Hull hull)
    {
        double Under(double x, double y)
        {
            double best = 0.0;
            foreach (var c in columns)
            {
                bool inside = c.Shape switch
                {
                    "hull" => Math.Abs(y) <= hull.HalfWidth(x),
                    "polygon" => Geometry.PointInPolygon(x, y, c.Points!),
                    "rect" => c.X <= x && x <= c.X + c.W && c.Y <= y && y <= c.Y + c.H,
                    "circle" => Math.Pow(x - c.Cx!.Value, 2) + Math.Pow(y - c.Cy!.Value, 2) <= Math.Pow(c.R!.Value, 2),
                    "ellipse" => Math.Pow((x - c.Cx!.Value) / c.Rx!.Value, 2) + Math.Pow((y - c.Cy!.Value) / c.Ry!.Value, 2) <= 1,
                    _ => false,
                };
                if (inside)
                    best = Math.Max(best, c.Top);
            }
            return best;
        }
        var result = new List<HeightColumn>();
        foreach (var it in items)
        {
            if (it.H <= 0)
                continue;
            double top = Under(it.X, it.Y) + it.H;
            if (it.Kind is "cowl" or "cowl_small" or "boiler_cowl" or "mushroom" or "searchlight" or "searchlight_tower")
                result.Add(new(top, "circle") { Cx = it.X, Cy = it.Y, R = it.L / 2 });
            else if (it.Kind == "boat")
                result.Add(new(top, "ellipse") { Cx = it.X, Cy = it.Y, Rx = it.L / 2, Ry = it.W / 2 });
            else
                result.Add(new(top, "rect") { X = it.X - it.L / 2, Y = it.Y - it.W / 2, W = it.L, H = it.W });
        }
        return result;
    }
}
