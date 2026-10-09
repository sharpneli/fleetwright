namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    public const double DH_INSET = 0.6;
    const double DH_TURRET_CLEAR = 1.0, DH_FIT = 0.375, DH_STEP_COST = 3.0, DH_MIN_FACE = 1.5, DH_MIN_DROP = 0.75;
    public const double FP_MARGIN = 0.3;
    static readonly (double Cap, double Frac) BEVEL_OUTER = (3.0, 0.22), BEVEL_INNER = (0.8, 0.07), BEVEL_UPPER = (2.0, 0.15);
    const double DH_CELL = 0.5, DH_MIN_RUN = 3.0, DH_MIN_W = 3.0, DH_NOTCH_MIN = 0.5, DH_NOTCH_STEP = 0.25, DH_JOIN = 1.5;
    const double DH_SHOULDER = 2.0, DH_SHOULDER_MIN = 0.5, DH_KEEP = 0.7;

    /// <summary>Andrew's monotone chain: the convex hull of the points, counter-clockwise.</summary>
    public static List<Pt> ConvexHull(IEnumerable<Pt> pts)
    {
        var p = pts.Distinct().ToList();
        p.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        if (p.Count < 3)
            return p;

        List<Pt> Half(IEnumerable<Pt> seq)
        {
            var out_ = new List<Pt>();
            foreach (var q in seq)
            {
                while (out_.Count >= 2 && ((out_[^1].X - out_[^2].X) * (q.Y - out_[^2].Y) -
                                           (out_[^1].Y - out_[^2].Y) * (q.X - out_[^2].X)) <= 0)
                    out_.RemoveAt(out_.Count - 1);
                out_.Add(q);
            }
            return out_;
        }

        var lower = Half(p);
        var upper = Half(Enumerable.Reverse(p));
        return lower.Take(lower.Count - 1).Concat(upper.Take(upper.Count - 1)).ToList();
    }

    /// <summary>Where the line across the ship at y crosses a footprint grown by margin: [(x0, x1)].</summary>
    static List<(double, double)> FpIntervals(Footprint fp, double y, double margin)
    {
        if (fp.Kind == 'c')
        {
            double dy = y - fp.Y;
            double r = fp.R + margin;
            if (Math.Abs(dy) >= r)
                return [];
            double h = Math.Sqrt(r * r - dy * dy);
            return [(fp.X - h, fp.X + h)];
        }
        if (fp.Kind == 'r')
            return fp.B - margin < y && y < fp.D + margin ? [(fp.A - margin, fp.C + margin)] : [];
        if (!(fp.B - margin < y && y < fp.D + margin))
            return [];
        var pts = fp.Pts!;
        var xs = new List<double>();
        for (int i = 0; i < pts.Count; i++)
        {
            var (x0, y0) = pts[i];
            var (x1, y1) = pts[(i + 1) % pts.Count];
            if ((y0 > y) != (y1 > y))
                xs.Add(x0 + (y - y0) * (x1 - x0) / (y1 - y0));
        }
        return xs.Count > 0 ? [(xs.Min() - margin, xs.Max() + margin)] : [];
    }

    /// <summary>A superstructure level's outline: a room laid out like a deck. See the Python docstring.</summary>
    static List<Pt> LevelOutline(Layout lay, double x0, double x1, double w, double bse, double top, List<Pt>? support = null,
        IReadOnlyList<(double X0, double Y0, double X1, double Y1)>? keep = null, IReadOnlyCollection<string>? ignore = null,
        IReadOnlyList<(double N0, double N1, double H)>? notches = null)
    {
        keep ??= [];
        ignore ??= [];
        notches ??= [];
        support ??= lay.DeckBand();
        var sup = new Slabs(support);
        double H = w / 2;
        double st = DH_FIT;
        int n = (int)Math.Max(1L, (long)(Math.Ceiling(H / st - 1e-6)));
        var ys = Enumerable.Range(0, n + 1).Select(j => Math.Min(H, j * st)).ToList();
        double floor = -0.3 * (x1 - x0);
        var ends = lay.EndMounts;
        var scan = lay.Scan;
        var endIds = new HashSet<string>(ends.Select(m => m.Id), StringComparer.Ordinal);
        var fps = lay.Footprints.Where(o => o.Top > bse + 1e-6 && o.Base < top - 1e-6 && !ignore.Contains(o.Owner)
                                            && !endIds.Contains(o.Owner)).Select(o => o.Fp).ToList();
        var prof = new Dictionary<int, List<Pt>>();
        foreach (var (e, xc) in new[] { (1, x1), (-1, x0) })
        {
            double uc = e * xc;
            var ms = ends.Where(m => e * (m.X - xc) > 0).ToList();
            var rad = ms.ToDictionary(m => m.Id, m => Armament.BodyReach(m.T) + DH_TURRET_CLEAR);
            double cap = 0.0;
            var req = Enumerable.Repeat(floor, n + 1).ToList();
            foreach (var (bx0, by0, bx1, by1) in keep)
            {
                double far = Math.Max(e * bx0, e * bx1) - uc + 0.3;
                for (int j = 0; j <= n; j++)
                {
                    double y = ys[j];
                    if (new[] { 1, -1 }.Any(s => by0 - st <= s * y && s * y <= by1 + st))
                        req[j] = Math.Max(req[j], far);
                }
            }
            cap = Math.Max(cap, req.Max());
            double loU = uc + floor, hiU = uc + cap + FP_MARGIN;

            bool NearZ(double xa, double xb)
            {
                double ua = Math.Min(e * xa, e * xb), ub = Math.Max(e * xa, e * xb);
                return ub > loU && ua < hiU;
            }

            var owners = new HashSet<string>(ms.Select(m => m.Id), StringComparer.Ordinal);
            var polys = new List<(List<Pt> P, (double A0, double B0, double A1, double B1) Bx)>();
            foreach (var sw in lay.Sweeps)
                if (owners.Contains(sw.Owner) || (sw.Axis < top && !endIds.Contains(sw.Owner)))
                    for (int i = 0; i < sw.Polys.Count; i++)
                        if (NearZ(sw.Boxes[i].X0, sw.Boxes[i].X1))
                            polys.Add((sw.Polys[i], sw.Boxes[i]));
            var circles = ms.Where(m => NearZ(m.X - rad[m.Id], m.X + rad[m.Id]))
                .Select(m => (Cx: m.X, Cy: m.Y, R: rad[m.Id])).ToList();
            var nearFps = fps.Where(fp => NearZ(fp.BBox.X0 - FP_MARGIN, fp.BBox.X1 + FP_MARGIN)).ToList();
            if (polys.Count == 0 && circles.Count == 0 && nearFps.Count == 0 && req.Max() <= cap)
            {
                prof[e] = [new(e * (uc + cap), 0.0), new(e * (uc + cap), H)];
                continue;
            }

            double Raw(double y)
            {
                var ivs = new List<(double, double)>();
                foreach (var (p, (a0, b0, a1, b1)) in polys)
                {
                    if (b0 <= y && y <= b1)
                    {
                        var key = (p, y);
                        if (!scan.TryGetValue(key, out var sl))
                        {
                            var xs = new List<double>();
                            for (int i = 0; i < p.Count; i++)
                            {
                                var (px, py) = p[i];
                                var (qx, qy) = p[(i + 1) % p.Count];
                                if ((py > y) != (qy > y))
                                    xs.Add(px + (y - py) * (qx - px) / (qy - py));
                            }
                            xs = xs.Order().ToList();
                            sl = [];
                            for (int i = 0; i < xs.Count - 1; i += 2)
                                sl.Add((xs[i], xs[i + 1]));
                            scan[key] = sl;
                        }
                        ivs.AddRange(sl);
                    }
                }
                foreach (var (cx, cy, r) in circles)
                {
                    if (Math.Abs(y - cy) < r)
                    {
                        double h = Math.Sqrt(r * r - Math.Pow(y - cy, 2));
                        ivs.Add((cx - h, cx + h));
                    }
                }
                foreach (var fp in nearFps)
                    ivs.AddRange(FpIntervals(fp, y, FP_MARGIN));
                double d = cap;
                foreach (var (xa, xb) in ivs)
                {
                    if (notches.Any(nt => xa < nt.N1 && nt.N0 < xb && Math.Abs(y) > nt.H - 1e-6))
                        continue;
                    double ua = Math.Min(e * xa, e * xb), ub = Math.Max(e * xa, e * xb);
                    if (ub > uc + floor)
                        d = Math.Min(d, ua - uc - 0.3);
                }
                return Math.Max(floor, d);
            }

            var A = ys.Zip(req).Select(t => Math.Max(Math.Min(Raw(t.First), Raw(-t.First)), t.Second)).ToList();
            double dIn = Math.Max(floor, A.Min() - st);
            var dgrid = Enumerable.Range(0, Math.Max(0, (int)((cap - dIn) / st) + 1)).Select(i => dIn + st * i).ToList();
            dgrid.Add(cap);
            var hws = new List<double>();
            foreach (var d in dgrid)
            {
                var spans = sup.At(e * (uc + d));
                hws.Add(spans.Where(s => s.Lo <= 0 && 0 <= s.Hi).Select(s => Math.Min(-s.Lo, s.Hi)).DefaultIfEmpty(-1.0).Min());
            }
            var E = ys.Select(y =>
            {
                for (int i = 0; i < dgrid.Count; i++)
                    if (hws[i] < y - 1e-9)
                        return dgrid[i];
                return cap;
            }).ToList();
            var Aeff = A.Zip(E).Select(t => Math.Min(cap, t.Second <= t.First ? cap : t.First)).ToList();
            (double Score, List<(double Y, double D)> Pts)? best = null;
            int faceMin = (int)(long)(Math.Ceiling(DH_MIN_FACE / 2 / st - 1e-9));
            var iList = new List<int> { 0 };
            for (int i = Math.Max(1, faceMin); i <= n; i++)
                iList.Add(i);
            foreach (int i in iList)
            {
                double d0 = Aeff.Take(i + 1).Min();
                if (req.Take(i + 1).Any(r => r > d0 + 1e-9))
                    continue;
                double ya = ys[i], d1 = d0, lo = double.NegativeInfinity;
                for (int k = i + 1; k <= n; k++)
                {
                    double t = (ys[k] - ya) / (H - ya);
                    if (Aeff[k] < d0)
                        d1 = Math.Min(d1, d0 - (d0 - Aeff[k]) / t);
                    if (req[k] > floor)
                        lo = Math.Max(lo, d0 - (d0 - req[k]) / t);
                }
                if (d1 < lo - 1e-9 || d1 < floor)
                    continue;
                if (d0 - d1 < DH_MIN_DROP && i < n)
                    continue;
                var f = Enumerable.Range(0, n + 1).Select(k => k <= i ? d0 : d0 - (d0 - d1) * (ys[k] - ya) / (H - ya)).ToList();
                double area = Enumerable.Range(0, n + 1).Select(k => Math.Min(f[k], E[k]) * (0 < k && k < n ? st : st / 2)).Sum();
                double score = area - (i < n ? DH_STEP_COST : 0.0);
                if (best is null || score > best.Value.Score + 1e-6)
                {
                    List<(double, double)> pts = i == n
                        ? [(0.0, d0), (H, d0)]
                        : (i != 0 ? new List<(double, double)> { (0.0, d0) } : []).Concat(new[] { (ya, d0), (H, d1) }).ToList();
                    best = (score, pts);
                }
            }
            best ??= (0.0, [(0.0, req.Max()), (H, req.Max())]);
            prof[e] = best.Value.Pts.Select(p => new Pt(e * (uc + p.D), p.Y)).ToList();
        }
        var half = prof[1].Concat(Enumerable.Reverse(prof[-1])).ToList();
        var all = half.Concat(Enumerable.Reverse(half).Select(p => new Pt(p.X, -p.Y))).ToList();
        return Geometry.ClipConvex(Geometry.SimplifyPolygon(all), support);
    }

    /// <summary>Chamfers a level's near-square corners: a big facet on the end facing the nearer end of the ship, a small
    /// cut elsewhere.</summary>
    static List<Pt> BevelOutline(List<Pt> pts, IReadOnlyList<(double X0, double Y0, double X1, double Y1)>? keep = null,
        IReadOnlyList<double>? flush = null, (double Cap, double Frac)? inner = null)
    {
        keep ??= [];
        flush ??= [];
        if (pts.Count < 3)
            return pts;
        double w = pts.Select(p => p.Y).Max() - pts.Select(p => p.Y).Min();
        double xm = (pts.Select(p => p.X).Min() + pts.Select(p => p.X).Max()) / 2;
        var out_ = new List<Pt>();
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            var b = pts[i];
            var a = pts[(i - 1 + n) % n];
            var c = pts[(i + 1) % n];
            var ua = (X: a.X - b.X, Y: a.Y - b.Y);
            var uc = (X: c.X - b.X, Y: c.Y - b.Y);
            double la = double.Hypot(ua.X, ua.Y), lc = double.Hypot(uc.X, uc.Y);
            if (la < 1e-6 || lc < 1e-6 || Math.Abs(ua.X * uc.X + ua.Y * uc.Y) > 0.5 * la * lc ||
                flush.Any(xf => Math.Abs(b.X - xf) < 1e-3))
            {
                out_.Add(b);
                continue;
            }
            bool outer = (b.X - xm) * (xm >= 0 ? 1 : -1) > 0;
            var (cap, frac) = outer ? BEVEL_OUTER : (inner ?? BEVEL_INNER);
            double size = Math.Min(cap, Math.Min(frac * w, Math.Min(0.4 * la, 0.4 * lc)));
            if (size < 0.25 || keep.Any(k => k.X0 - size < b.X && b.X < k.X1 + size && k.Y0 - size < b.Y && b.Y < k.Y1 + size))
            {
                out_.Add(b);
                continue;
            }
            out_.Add(new Pt(b.X + ua.X / la * size, b.Y + ua.Y / la * size));
            out_.Add(new Pt(b.X + uc.X / lc * size, b.Y + uc.Y / lc * size));
        }
        return out_;
    }

    /// <summary>One superstructure level as a block, shaped by level_outline, bevelled and notched. Returns the block, or
    /// null if nothing of it stands on its support.</summary>
    static Block? AddLevel(Layout lay, List<Block> blocks, string bid, long level, double x0, double x1, double w,
        List<Pt>? support = null, IReadOnlyList<(double, double, double, double)>? keep = null,
        IReadOnlyCollection<string>? ignore = null, IReadOnlyList<(double N0, double N1, double H)>? notches = null,
        (Block? Aft, Block? Fwd) joins = default, (double, double)? bevel = null, string role = "deckhouse", bool office = false)
    {
        keep ??= [];
        notches ??= [];
        if (level <= lay.DeckLevels((x0 + x1) / 2, Math.Max(0.0, (x1 - x0) / 2 - 0.75)).Lo)
            return null;
        double bse = LEVEL_H * (level - 1), top = LEVEL_H * level;
        var ign = (ignore ?? []).ToList();
        foreach (var j in new[] { joins.Aft, joins.Fwd })
            if (j != null)
                ign.Add(j.Id);
        var pts = LevelOutline(lay, x0, x1, w, bse, top, support, keep, ign, notches);
        var js = new[] { (x0, joins.Aft), (x1, joins.Fwd) };
        for (int e = 0; e < 2; e++)
        {
            var (xf, j) = js[e];
            if (j != null && pts.Count >= 3)
                pts = ShoulderOutline(pts, xf, e != 0 ? 1 : -1, j);
        }
        if (pts.Count < 3)
            return null;
        pts = BevelOutline(pts, keep, js.Where(t => t.Item2 != null).Select(t => t.Item1).ToList(), bevel);
        if (pts.Count < 3 || Geometry.PolygonCentroid(pts).Area < 1.0)
            return null;
        var convex = pts;
        if (notches.Count > 0)
            pts = NotchOutline(pts, notches);
        var b = AddBlock(lay, blocks, bid, x0, x1, w, level, 0.0, 0.0, points: pts, role: role, office: office);
        if (notches.Count > 0)
        {
            b.Support = convex.ToList();
            b.Notches = notches.ToList();
        }
        return b;
    }

    /// <summary>A convex outline whose end at xf (e: 1 forward, -1 aft) butts against block: where it is wider than the
    /// block's face there, each side falls back in a straight shoulder.</summary>
    static List<Pt> ShoulderOutline(List<Pt> pts, double xf, int e, Block block)
    {
        var xs = pts.Select(p => p.X).ToList();
        var sp = new Slabs(block.Points!).At(xf + e * 0.01);
        var own = new Slabs(pts).At(xf - e * 0.01);
        if (sp.Count == 0 || own.Count == 0)
            return pts;
        foreach (int s in new[] { 1, -1 })
        {
            double face = sp.SelectMany(t => new[] { s * t.Lo, s * t.Hi }).Max();
            double side = own.SelectMany(t => new[] { s * t.Lo, s * t.Hi }).Max();
            if (side - face < DH_SHOULDER_MIN)
                continue;
            double run = Math.Min(DH_SHOULDER * (side - face), 0.25 * (xs.Max() - xs.Min()));
            var A = (X: xf, Y: s * face);
            var B = (X: xf - e * run, Y: s * side);
            var d = (X: B.X - A.X, Y: B.Y - A.Y);
            double nn = double.Hypot(d.X, d.Y);
            d = (d.X / nn, d.Y / nn);
            var nrm = (X: -d.Y, Y: d.X);
            var rf = (X: xf - e * run, Y: 0.0);
            if (nrm.X * (rf.X - A.X) + nrm.Y * (rf.Y - A.Y) < 0)
                nrm = (-nrm.X, -nrm.Y);
            var P1 = new Pt(A.X - 200 * d.X, A.Y - 200 * d.Y);
            var P2 = new Pt(A.X + 200 * d.X, A.Y + 200 * d.Y);
            pts = Geometry.ClipConvex(pts, [P1, P2, new(P2.X + 200 * nrm.X, P2.Y + 200 * nrm.Y), new(P1.X + 200 * nrm.X, P1.Y + 200 * nrm.Y)]);
        }
        return pts;
    }

    /// <summary>A convex outline narrowed to half-width h along x0 .. x1 for each notch, with 45-degree shoulders.</summary>
    static List<Pt> NotchOutline(List<Pt> pts, IReadOnlyList<(double N0, double N1, double H)> notches)
    {
        var sl = new Slabs(pts);
        var xsV = (new HashSet<double>(pts.Select(p => p.X))).Order().ToList();
        double xa = xsV[0], xb = xsV[^1];

        double Cap(double x) => notches.Select(nt => nt.H + Math.Max(0.0, Math.Max(nt.N0 - x, x - nt.N1))).DefaultIfEmpty(double.PositiveInfinity).Min();

        (double Lo, double Hi) Span(double x)
        {
            var sp = sl.At(Math.Min(Math.Max(x, xa + 1e-6), xb - 1e-6));
            return sp.Count > 0 ? (sp.Select(t => t.Lo).Min(), sp.Select(t => t.Hi).Max()) : (0.0, 0.0);
        }

        var brk = new HashSet<double>(xsV);
        foreach (var (n0, n1, h) in notches)
        {
            var (lo_, hi_) = Span((n0 + n1) / 2);
            double reach = Math.Max(hi_, -lo_) - h;
            brk.UnionWith([n0, n1, n0 - reach, n1 + reach]);
        }
        var brkL = brk.Where(x => xa <= x && x <= xb).Order().ToList();
        double Up(double x) => Math.Min(Span(x).Hi, Cap(x));
        double Dn(double x) => Math.Max(Span(x).Lo, -Cap(x));
        var xs = new List<double>();
        for (int i = 0; i < brkL.Count - 1; i++)
        {
            double a = brkL[i], b = brkL[i + 1];
            xs.Add(a);
            foreach (var f in new Func<double, double>[] { x => Span(x).Hi, x => -Span(x).Lo })
            {
                double da = f(a) - Cap(a), db = f(b) - Cap(b);
                if (da * db < 0)
                    xs.Add(a + (b - a) * da / (da - db));
            }
        }
        xs.Add(brkL[^1]);
        xs = (new HashSet<double>(xs)).Order().ToList();
        var top = xs.Select(x => new Pt(x, Up(x))).ToList();
        var bot = Enumerable.Reverse(xs).Select(x => new Pt(x, Dn(x))).ToList();
        if (top.Count == 0)
            return pts;
        var all = new List<Pt> { bot[^1] };
        all.AddRange(top);
        all.AddRange(bot.Take(bot.Count - 1));
        return Geometry.SimplifyPolygon(all);
    }

    /// <summary>The deckhouse's levels above the first, up to level n, over the middle (x0 .. x1).</summary>
    static List<Block> AddDeckhouseLevels(Layout lay, List<Block> blocks, long n, double x0, double x1, List<Block> baseBlocks,
        double dhW, IReadOnlyCollection<string> through)
    {
        if (n <= 1)
            return [];
        var hull = lay.Hull;
        var cells = Enumerable.Range(0, Math.Max(0, (int)((x1 - x0) / DH_CELL))).Select(i => x0 + DH_CELL * i).ToList();
        var made = new List<Block>();

        bool Ok(double x, double w, double bse, double top, Func<double, bool>? support)
        {
            if (support != null && !support(x))
                return false;
            double hw = Math.Min(hull.HalfWidth(x), hull.HalfWidth(x + DH_CELL)) - 0.6;
            if (w / 2 > hw)
                return false;
            var fp = Footprint.Rect(x, -w / 2, x + DH_CELL, w / 2);
            return lay.FreeAt(fp, bse, top, 0.3, through) && lay.Clear(fp, top);
        }

        List<(double, double)> Runs(double w, double bse, double top, Func<double, bool>? support)
        {
            var out_ = new List<(double, double)>();
            double? start = null;
            foreach (var xo in cells.Select(c => (double?)c).Append(null))
            {
                if (xo is double x && Ok(x, w, bse, top, support))
                {
                    start ??= x;
                    continue;
                }
                if (start is double s)
                {
                    double end = xo ?? cells[^1] + DH_CELL;
                    if (end - s >= DH_MIN_RUN - 1e-6)
                        out_.Add((s, end));
                    start = null;
                }
            }
            return out_;
        }

        (List<Block> Out, double Lo) Level(long k, double wMax, Func<double, bool> support, Func<double, bool>? skip = null,
            List<Block>? under = null)
        {
            under ??= [];
            double bse = LEVEL_H * (k - 1), top = LEVEL_H * k;
            Func<double, bool> sup = skip != null ? x => support(x) && !skip(x) : support;
            if (wMax < DH_MIN_W)
                return ([], 0.0);
            double Total(List<(double A, double B)> rr) => rr.Select(r => r.B - r.A).Sum();
            double floor = Total(Runs(DH_MIN_W, bse, top, sup));
            if (floor <= 0)
                return ([], 0.0);
            double lo = DH_MIN_W, hi = wMax;
            if (Total(Runs(hi, bse, top, sup)) >= DH_KEEP * floor)
                lo = hi;
            while (hi - lo > 0.25)
            {
                double mid = (lo + hi) / 2;
                (lo, hi) = Total(Runs(mid, bse, top, sup)) >= DH_KEEP * floor ? (mid, hi) : (lo, mid);
            }
            var out_ = new List<Block>();

            (List<double[]> Pieces, List<(double, double, double)> Notches) Pieced(double lo_)
            {
                var pieces = new List<double[]>();
                var notches = new List<(double, double, double)>();
                foreach (var (a, b) in Runs(lo_, bse, top, sup))
                {
                    if (pieces.Count > 0)
                    {
                        double g0 = pieces[^1][1];
                        var gap = cells.Where(x => g0 - 1e-6 <= x && x < a - 1e-6).ToList();
                        double wn = lo_ - DH_NOTCH_STEP;
                        while (wn >= Math.Max(DH_MIN_W, DH_NOTCH_MIN * lo_) && !gap.All(x => Ok(x, wn, bse, top, sup)))
                            wn -= DH_NOTCH_STEP;
                        if (wn >= Math.Max(DH_MIN_W, DH_NOTCH_MIN * lo_))
                        {
                            pieces[^1][1] = b;
                            notches.Add((g0, a, wn / 2));
                            continue;
                        }
                    }
                    pieces.Add([a, b]);
                }
                return (pieces, notches);
            }

            var (pieces, notches) = Pieced(lo);
            for (int it = 0; it < 8; it++)
            {
                var shallow = notches.Where(nt => lo / 2 - nt.Item3 < DH_MIN_DROP).Select(nt => 2 * nt.Item3).ToList();
                if (shallow.Count == 0)
                    break;
                lo = shallow.Min();
                (pieces, notches) = Pieced(lo);
            }
            var towers = blocks.Where(t => Math.Abs(t.Base - bse) < 1e-6 && t.Kind == "superstructure"
                                           && t.Role != "deckhouse" && Math.Abs(t.Y) < t.W / 2).ToList();
            foreach (var piece in pieces)
            {
                double a = piece[0], b = piece[1];
                var jAft = towers.FirstOrDefault(t => a - DH_JOIN <= t.X1 && t.X1 <= a + 1e-6);
                var jFwd = towers.FirstOrDefault(t => b - 1e-6 <= t.X0 && t.X0 <= b + DH_JOIN);
                (a, b) = (jAft != null ? jAft.X1 : a, jFwd != null ? jFwd.X0 : b);
                var on_ = under.MaxBy(u => Math.Min(b, u.X1) - Math.Max(a, u.X0));
                var mine = notches.Where(nt => a <= nt.Item1 && nt.Item2 <= b).ToList();
                foreach (var u in under)
                    if (u.Notches is { } un)
                        mine.AddRange(un.Where(nt => nt.Item2 > a && nt.Item1 < b));
                List<Pt>? support_ = on_ is null ? null
                    : (on_.Support is { Count: > 0 } s_ ? s_ : on_.Points);
                var blk = AddLevel(lay, blocks, $"Deckhouse {k}" + (out_.Count == 0 ? "" : $"-{out_.Count + 1}"), k, a, b, lo,
                    support: support_, ignore: through, notches: mine, joins: (jAft, jFwd), bevel: BEVEL_UPPER);
                if (blk != null)
                {
                    made.Add(blk);
                    out_.Add(blk);
                }
            }
            return (out_, lo);
        }

        Func<double, bool> On(List<Block> bb) => x => bb.Any(b => b.X0 <= x && x + DH_CELL <= b.X1 + 1e-6);
        var below = baseBlocks.ToList();
        double w = dhW;
        if (below.Select(b => b.X1 - b.X0).Sum() < 0.5 * (x1 - x0))
        {
            var (ext, _) = Level(1, dhW, x => true, skip: On(below));
            below.AddRange(ext);
        }
        for (long k = 2; k <= n; k++)
        {
            (below, w) = Level(k, w, On(below), under: below);
            if (below.Count == 0)
            {
                lay.Warnings.Add($"Only {k - 1} of {n} deckhouse levels fit amidships.");
                break;
            }
        }
        return made;
    }
}
