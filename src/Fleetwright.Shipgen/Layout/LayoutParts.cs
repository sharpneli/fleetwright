namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>What a superstructure block is for (add_block's role).</summary>
    public static readonly string[] BlockRoles = ["deckhouse", "bridge", "aft_control", "director", "island", "hangar", "casemate"];

    public static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

    /// <summary>Superstructure block standing on z0: footprint, weight, record. points: an outline polygon instead of
    /// the rounded rectangle; x0, x1, y and w then become its bounding box, and rf, rb are 0.</summary>
    public static Block AddBlock(Layout lay, List<Block> blocks, string bid, double x0, double x1, double w, long level,
        double rf, double rb, double y = 0.0, double z0 = 0.0, string kind = "superstructure",
        double? tPerM2 = null, IReadOnlyList<Pt>? points = null, string role = "deckhouse", bool office = false)
    {
        if (!BlockRoles.Contains(role))
            throw new InvalidOperationException(role);
        List<Pt>? pts = null;
        if (points != null && points.Count > 0)
        {
            pts = points.Select(p => new Pt(Math.Round(p.X, 3), Math.Round(p.Y, 3))).ToList();
            x0 = pts.Select(p => p.X).Min();
            x1 = pts.Select(p => p.X).Max();
            double y0 = pts.Select(p => p.Y).Min(), y1 = pts.Select(p => p.Y).Max();
            (y, w, rf, rb) = ((y0 + y1) / 2, y1 - y0, 0.0, 0.0);
        }
        var (rf_, rb_) = Geometry.RrectClamped(x0, y - w / 2, x1, y + w / 2, rf, rb);
        var b = new Block
        {
            Id = bid, Kind = kind, Role = role, X0 = x0, X1 = x1, Y = y, W = w, Level = level, Rf = rf_, Rb = rb_, Office = office,
            Z0 = z0 != 0 ? z0 : null,
        };
        double area, xc;
        if (pts != null)
        {
            (area, xc) = Geometry.PolygonCentroid(pts);
            b.Points = pts;
            b.Area = Math.Round(area, 2);
        }
        else
            (area, xc) = ((x1 - x0) * w, (x0 + x1) / 2);
        blocks.Add(b);
        lay.Occupy(pts != null ? Footprint.Poly(pts) : Footprint.Rect(x0, y - w / 2, x1, y + w / 2), b.Base, b.TopZ, bid);
        double tpm = tPerM2 ?? lay.SupT;
        double wt = area * tpm;
        if (kind == "superstructure")
            wt += BlockPlating(lay, b);
        lay.Weights.Add(new Weight(bid, "superstructure", wt, xc, ZRel.Deck((b.Base + b.TopZ) / 2)));
        return b;
    }

    /// <summary>The hull's own gauge, mm: a box-model hull's planking, else the plate model's minimum gauge.</summary>
    public static double OwnPlateMm(Layout lay) => lay.OwnPlateMmValue ?? HullWeight.TMinMm(lay.Hull.L, lay.Construction);

    /// <summary>A block's wall plating: records its PlateMm and returns the weight of plate beyond the own gauge.</summary>
    public static double BlockPlating(Layout lay, Block b)
    {
        double own = HullWeight.SupPlateK * OwnPlateMm(lay);
        double mm = Math.Max(own, lay.SupPlate.Plating);
        if (b.Role is "bridge" or "aft_control")
            mm = Math.Max(mm, lay.SupPlate.Control);
        b.PlateMm = Math.Round(mm, 1);
        var pts = Geometry.BlockOutline(b);
        int n = pts.Count;
        double perim = Enumerable.Range(0, n).Select(i => double.Hypot(pts[(i - 1 + n) % n].X - pts[i].X, pts[(i - 1 + n) % n].Y - pts[i].Y)).Sum();
        return HullWeight.ExtraPlateT(perim * (b.TopZ - b.Base), mm, own);
    }

    public const double RaisedInset = 0.3;

    /// <summary>A raised stretch of hull: the weather deck `levels` decks above the main deck from x0 to x1.</summary>
    public static RaisedStretch AddRaised(Layout lay, Design design, string rid, double x0, double x1, long levels = 1,
        (long Aft, long Fwd)? breaks = null)
    {
        var hull = lay.Hull;
        double L = hull.L;
        double h = levels * LevelH;
        var pts = hull.Points(inset: RaisedInset, xMin: x0, xMax: x1);
        var stretch = new RaisedStretch(rid, x0, x1, levels);
        lay.Raised.Add(stretch);
        lay.Geo.Raised = lay.Raised;
        lay.Decks.Add(new DeckPlate(rid, "deck", pts, 0.0, h));
        lay.Occupy(Footprint.Poly(pts), 0.0, h, rid);
        var (area, xc) = Geometry.PolygonCentroid(pts);
        var brk = breaks ?? (levels, levels);
        double endM2 = new[] { (x0, brk.Aft), (x1, brk.Fwd) }.Where(t => -L / 2 + 0.5 < t.Item1 && t.Item1 < L / 2 - 0.5)
            .Sum(t => 2 * hull.HalfWidth(t.Item1) * t.Item2 * LevelH);
        double sideM2 = 2 * (x1 - x0) * h;
        var c = HullWeight.ConstructionOf(design);
        double t = HullWeight.RaisedT(L, c, area, sideM2, endM2, HullWeight.PlatingOf(design).ShellMm);
        lay.Weights.Add(new Weight(rid, "hull", t, xc, ZRel.Deck(h * (area + 0.5 * (sideM2 + endM2)) / (area + sideM2 + endM2))));
        return stretch;
    }

    public static readonly string[] RaisedAnchors = Style.RaisedAnchors;

    /// <summary>Does the hull.raised entry q run over the feature?</summary>
    public static bool RaisedCovers(RaisedInput q, string feature)
    {
        int a = Array.IndexOf(RaisedAnchors, q.From), b = Array.IndexOf(RaisedAnchors, q.To);
        int i = Math.Min(a, b), j = Math.Max(a, b);
        int f = Array.IndexOf(RaisedAnchors, feature);
        return i <= f && f <= j;
    }

    /// <summary>Stretches [(x0, x1, levels)] that may overlap, as one stepped profile aft to forward, each with the
    /// decks of its breaks.</summary>
    public static List<(double X0, double X1, long Lv, (long Aft, long Fwd) Brk)> RaisedProfile(
        IReadOnlyList<(double X0, double X1, long N)> spans, double L)
    {
        var set = new HashSet<double> { -L / 2, L / 2 };
        foreach (var (a, b, _) in spans)
            foreach (var x in new[] { a, b })
                set.Add(Math.Min(L / 2, Math.Max(-L / 2, x)));
        var xs = set.Order().ToList();
        var segs = new List<(double A, double B, long Lv)>();
        for (int i = 0; i < xs.Count - 1; i++)
        {
            double a = xs[i], b = xs[i + 1];
            if (b - a < 1e-6)
                continue;
            double m = (a + b) / 2;
            long lv = spans.Where(s => s.X0 <= m && m <= s.X1).Select(s => s.N).DefaultIfEmpty(0L).Max();
            if (segs.Count > 0 && segs[^1].Lv == lv)
                segs[^1] = (segs[^1].A, b, lv);
            else
                segs.Add((a, b, lv));
        }
        var result = new List<(double, double, long, (long, long))>();
        for (int i = 0; i < segs.Count; i++)
        {
            var (a, b, lv) = segs[i];
            if (lv != 0)
            {
                long d0 = lv - (0 <= i - 1 && i - 1 < segs.Count ? segs[i - 1].Lv : 0);
                long d1 = lv - (0 <= i + 1 && i + 1 < segs.Count ? segs[i + 1].Lv : 0);
                result.Add((a, b, lv, (Math.Max(0, d0), Math.Max(0, d1))));
            }
        }
        return result;
    }

    /// <summary>raised_profile's stretches with ids: Forecastle, Poop, Raised deck.</summary>
    public static List<(string Id, double X0, double X1, long Lv, (long, long) Brk)> RaisedNames(
        List<(double X0, double X1, long Lv, (long Aft, long Fwd) Brk)> prof, double L)
    {
        var result = new List<(string? Id, double X0, double X1, long Lv, (long, long) Brk)>();
        for (int k = 0; k < prof.Count; k++)
        {
            var (x0, x1, lv, brk) = prof[k];
            bool bow = Enumerable.Range(k, Math.Max(0, prof.Count - 1 - k)).All(j => prof[j].X1 >= prof[j + 1].X0 - 1e-6)
                       && prof[^1].X1 >= L / 2 - 1e-6;
            bool stern = Enumerable.Range(0, k).All(j => prof[j].X1 >= prof[j + 1].X0 - 1e-6) && prof[0].X0 <= -L / 2 + 1e-6;
            int n = bow ? prof.Count - k : k + 1;
            string? rid = bow || stern ? (bow ? "Forecastle" : "Poop") + (n > 1 ? $" {n}" : "") : null;
            result.Add((rid, x0, x1, lv, brk));
        }
        var mids = Enumerable.Range(0, result.Count).Where(i => result[i].Id is null).ToList();
        for (int k = 0; k < mids.Count; k++)
        {
            var o = result[mids[k]];
            result[mids[k]] = ("Raised deck" + (mids.Count > 1 ? $" {k + 1}" : ""), o.X0, o.X1, o.Lv, o.Brk);
        }
        return result.Select(o => (o.Id!, o.X0, o.X1, o.Lv, o.Brk)).ToList();
    }

    const double DhSliver = 3.0;
    const double RaisedClear = 1.1;

    /// <summary>How much higher a main mount must stand for its barrels to clear the raised stretches they sweep over.</summary>
    public static double RaisedLift(Layout lay, Mount m)
    {
        if (lay.Raised.Count == 0)
            return 0.0;
        double axis = m.Base + 0.55 * (m.Top - m.Base);
        var tr = Arcs.MountTraverse(m)!;
        List<Pt>? sweep = null;
        double need = 0.0;
        foreach (var dk in lay.Decks)
        {
            if (dk.Kind != "deck" || dk.Top + RaisedClear <= axis + need)
                continue;
            sweep ??= Geometry.SectorPolygon(m.X, m.Y, Geometry.TurretReach(m.T) + 0.5, tr[0], tr[1]);
            if (Geometry.PolygonsIntersect(sweep, dk.Points))
                need = dk.Top + RaisedClear - axis;
        }
        return need;
    }

    /// <summary>Where something l long and w wide could stand on the blocks' roofs: (x, y, z0, pair) candidates.</summary>
    public static List<(double X, double Y, double Z0, bool Pair)> RoofSpots(IEnumerable<Block> blocks, double l, double w,
        double step = 0.5)
    {
        var result = new List<(double, double, double, bool)>();
        RoofSpots(blocks, l, w, result, step);
        return result;
    }

    /// <summary>RoofSpots, appended to `result`.</summary>
    public static void RoofSpots(IEnumerable<Block> blocks, double l, double w, List<(double X, double Y, double Z0, bool Pair)> result,
        double step = 0.5)
    {
        using var _0 = Scratch<(double Lo, double Hi)>.Rent(out var sp0);
        using var _1 = Scratch<(double Lo, double Hi)>.Rent(out var sp1);
        foreach (var b in blocks)
        {
            if (b.Kind == "director")
                continue;
            double z0 = b.TopZ;
            double bx0 = b.X0, bx1 = b.X1, by = b.Y, bw = b.W;
            bool one = bx1 - bx0 < l;
            long n = one ? 0 : (long)((bx1 - bx0 - l) / step);
            double ye = bw / 2 - w / 2;
            if (b.Points is { Count: > 0 } && b.Slabs is null)
                b.Slabs = new Slabs(b.Points);
            var slabs = b.Slabs;
            double hwR = w < bw ? w / 2 - 0.3 : 0.0;
            for (long k = 0; k <= n; k++)
            {
                double x = one ? (bx0 + bx1) / 2 : bx0 + l / 2 + k * step;
                if (slabs != null)
                {
                    slabs.At(x + -l / 2, sp0);
                    slabs.At(x + l / 2, sp1);
                }
                if (slabs is null || OnRoof(sp0, sp1, by, hwR))
                    result.Add((x, by, z0, false));
                if (Math.Abs(by) < 1e-6 && ye > w / 2 + 0.1 && (slabs is null || OnRoof(sp0, sp1, ye, hwR)))
                    result.Add((x, ye, z0, true));
            }
        }
    }

    /// <summary>Is a spot of half-width hw at y (and -y, for a pair) inside a polygon roof's spans at both its ends?</summary>
    static bool OnRoof(List<(double Lo, double Hi)> sp0, List<(double Lo, double Hi)> sp1, double y, double hw)
    {
        for (int k = 0; k < (y != 0 ? 2 : 1); k++)
        {
            double s = k == 0 ? 1 : -1;
            double a = s * y - hw, b = s * y + hw;
            if (!Inside(sp0, a, b) || !Inside(sp1, a, b))
                return false;
        }
        return true;
    }

    static bool Inside(List<(double Lo, double Hi)> spans, double a, double b)
    {
        foreach (var (lo, hi) in spans)
            if (lo <= a && b <= hi)
                return true;
        return false;
    }

    /// <summary>Douglas-Peucker: the polyline's points that keep it within tol of the original.</summary>
    public static List<Pt> Thin(List<Pt> line, double tol)
    {
        if (line.Count < 3)
            return [.. line];
        var (ax, ay) = line[0];
        var (bx, by) = line[^1];
        int bi = 1;
        double dmax = double.NaN;
        for (int i = 1; i < line.Count - 1; i++)
        {
            double d = SegDist(line[i].X, line[i].Y, ax, ay, bx, by);
            if (i == 1 || d > dmax)
            {
                bi = i;
                dmax = d;
            }
        }
        if (dmax <= tol)
            return [line[0], line[^1]];
        var left = Thin(line.GetRange(0, bi + 1), tol);
        left.RemoveAt(left.Count - 1);
        left.AddRange(Thin(line.GetRange(bi, line.Count - bi), tol));
        return left;
    }

    /// <summary>A polygon cut into slabs between its vertices' x: where a line across the ship is inside it.</summary>
    public sealed class Slabs
    {
        readonly double[] xs;
        // slab i (between xs[i] and xs[i + 1]) is edges[start[i] .. start[i + 1]], ordered by y at its middle
        readonly (double X0, double Y0, double X1, double Y1)[] edges;
        readonly int[] start;

        public Slabs(IReadOnlyList<Pt> pts)
        {
            int n = pts.Count;
            using var _x = Scratch<double>.Rent(out var xl);
            for (int i = 0; i < n; i++)
                xl.Add(pts[i].X);
            xl.Sort();
            int u = 0;
            for (int i = 0; i < xl.Count; i++)
                if (u == 0 || xl[i] != xl[u - 1])
                    xl[u++] = xl[i];
            xl.RemoveRange(u, xl.Count - u);
            xs = xl.ToArray();
            using var _e = Scratch<(double X0, double Y0, double X1, double Y1)>.Rent(out var all);
            for (int i = 0; i < n; i++)
            {
                var (x0, y0) = pts[i];
                var (x1, y1) = pts[(i + 1) % n];
                if (x0 != x1)
                    all.Add((x0, y0, x1, y1));
            }
            using var _k = Scratch<(double K, int I)>.Rent(out var keyed);
            using var _f = Scratch<(double X0, double Y0, double X1, double Y1)>.Rent(out var flat);
            start = new int[Math.Max(xs.Length, 1)];
            for (int i = 0; i < xs.Length - 1; i++)
            {
                double m = (xs[i] + xs[i + 1]) / 2;
                keyed.Clear();
                for (int j = 0; j < all.Count; j++)
                {
                    var e = all[j];
                    if ((e.X0 > m) != (e.X1 > m))
                        keyed.Add((e.Y0 + (m - e.X0) * (e.Y1 - e.Y0) / (e.X1 - e.X0), j));
                }
                KeyedSort.Sort(keyed);
                foreach (var (_, j) in keyed)
                    flat.Add(all[j]);
                start[i + 1] = flat.Count;
            }
            edges = flat.ToArray();
        }

        /// <summary>The stretches of y inside the polygon at x.</summary>
        public List<(double Lo, double Hi)> At(double x)
        {
            var result = new List<(double, double)>();
            At(x, result);
            return result;
        }

        /// <summary>At(x) into a list the caller keeps (cleared first).</summary>
        public void At(double x, List<(double Lo, double Hi)> result)
        {
            result.Clear();
            int i = xs.UpperBound(x) - 1;
            if (i < 0 || i >= start.Length - 1)
                return;
            for (int k = start[i]; k + 1 < start[i + 1]; k += 2)
            {
                var e = edges[k];
                var f = edges[k + 1];
                result.Add((e.Y0 + (x - e.X0) * (e.Y1 - e.Y0) / (e.X1 - e.X0), f.Y0 + (x - f.X0) * (f.Y1 - f.Y0) / (f.X1 - f.X0)));
            }
        }
    }

    const double MastTK = 0.012;

    /// <summary>A mast's weight at half its height: tripod legs or a pole.</summary>
    public static void MastWeight(Layout lay, Mast m, double top, string name, double bse = 0.0)
    {
        int legs = m.Tripod ? 3 : 1;
        double h = top - bse;
        lay.Weights.Add(new Weight(name, "superstructure", legs * MastTK * Math.Pow(h, 2), m.X, ZRel.Deck(bse + h / 2)));
    }

    static readonly double[] AaRoofPen = [0.05, 0.05, 0.0, 0.0, 0.03];
    const double AaSinglePen = 0.1, AaDeckPen = 0.3;

    /// <summary>The magazines grouped fore and aft of the machinery (warships): (battery, mounts, m3 per mount).</summary>
    public static Dictionary<string, List<(string Bat, long N, double V)>> MagazinePlan(Design design,
        IEnumerable<(string Id, string Grp, Gun G)> wings)
    {
        var result = new Dictionary<string, List<(string, long, double)>> { ["fore"] = [], ["aft"] = [] };
        foreach (var (wid, grp, g) in wings)
            result[grp].Add((wid, 2, Ordnance.AmmoM3(g.T)));
        var secs = Batteries.SecondaryBatteries(design);
        for (int k = 0; k < secs.Count; k++)
        {
            var s = secs[k];
            long n = s.MountsPerSide;
            if (n == 0)
                continue;
            string kind = s.Mount == "casemate" ? "casemate" : "auto";
            var t = Geometry.BatteryType(s, kind).T;
            foreach (var (grp, pairs) in new[] { ("fore", (n + 1) / 2), ("aft", n / 2) })
                if (pairs != 0)
                    result[grp].Add((Batteries.BatteryPrefix(k), 2 * pairs, Ordnance.AmmoM3(t)));
        }
        return result;
    }

    /// <summary>The warship's magazines (ordnance.stow), linked both ways.</summary>
    public static void AddMagazines(Layout lay, List<Mount> mounts, double innerHw, Dictionary<string, (double X0, double X1)>? groups = null)
    {
        groups ??= [];
        var plan = lay.Geo.Plant;
        double ghw = plan is { WingM: not 0 } ? plan.Width / 2 : Math.Min(innerHw, (plan?.Width ?? 2 * innerHw) / 2);
        var zones = new List<Zone>();
        var batteries = new OrderedDictionary<string, List<Mount>>(StringComparer.Ordinal);
        foreach (var m in mounts)
        {
            if (m.Kind is not ("main" or "secondary"))
                continue;
            if (groups.Count > 0 && (m.Kind == "secondary" || (m.Wing && !m.Echelon)))
            {
                string bat = m.Battery!;
                if (!batteries.TryGetValue(bat, out var l))
                    batteries[bat] = l = [];
                l.Add(m);
                continue;
            }
            zones.Add(Ordnance.OwnZone(m, innerHw));
        }
        var rooms = new Dictionary<string, List<ZoneRoom>> { ["fore"] = [], ["aft"] = [] };
        foreach (var (bat, ms) in batteries)
        {
            var pairs = (new HashSet<double>(ms.Select(m => m.X))).OrderDescending().ToList();
            int nFore = pairs.Count;
            if (ms[0].Wing)
                nFore = ms[0].MagazineEnd == "fore" ? pairs.Count : 0;
            else if (groups.ContainsKey("fore") && groups.ContainsKey("aft"))
                nFore = (pairs.Count + 1) / 2;
            nFore = !groups.ContainsKey("aft") ? pairs.Count : !groups.ContainsKey("fore") ? 0 : nFore;
            foreach (var (grp, xs) in new[] { ("fore", pairs.Take(nFore).ToList()), ("aft", pairs.Skip(nFore).ToList()) })
            {
                var sel = ms.Where(m => xs.Contains(m.X)).Select(m => m.Id).ToList();
                if (sel.Count > 0)
                    rooms[grp].Add(new ZoneRoom($"Magazine {bat} {grp}", sel));
            }
        }
        foreach (var grp in new[] { "fore", "aft" })
            if (rooms[grp].Count > 0)
                zones.Add(new Zone(groups[grp].X0, groups[grp].X1, ghw, rooms[grp]));
        Ordnance.Stow(lay, mounts, zones);
    }

    /// <summary>The machinery space for the solved ship res, centred near x. Stores the plan in lay.geo.plant and
    /// returns the block's length.</summary>
    public static double PlanMachinery(Layout lay, Design design, Navarch.Result res, Hull hull, double x = 0.0)
    {
        var p = res.Plant;
        double D = res.Depth, T = res.Draught;
        double tds = design.Armour?.TdsM ?? 0.0;
        double wing = p.Tech.Fuel == "coal" && p.Bunkers == "wing" ? p.WingBunkerM : 0.0;
        double w = Powerplant.SteelFrame * 2 * hull.HalfWidth(x) - 2 * tds - 2 * wing;
        var ag = Armour.ArmourGeometry(design, hull.L, T, D, lay.Geo);
        double top = ag.RoofZ ?? D;
        double db = Powerplant.DoubleBottom(D);
        double h = Math.Max(1.0, top - db);
        var sp = Powerplant.Space(p, res.PowerShp, w, h);
        var (wingT, end) = Powerplant.Bunkers(p, res.Fuel, sp.Length, w, h, hull.L, hull.B, design.BlockCoefficient, D, T, tds);
        var segs = Powerplant.Segments(sp, end);
        lay.Warnings.AddRange(Powerplant.Groups(p).Warns);
        long units = Powerplant.Rated(p, res.PowerShp).Units;
        long nEng = sp.Order.Count(k => k == "engine");
        if (nEng > units)
            lay.Warnings.Add($"machinery.arrangement has {nEng} engine groups for {units} " +
                             "engine unit(s): some engine rooms hold no engine.");
        if (!sp.Fits)
            lay.Fail("beam", $"The plant's units are {sp.Unit.W:F1} m wide, but the machinery space is only " +
                             $"{Math.Max(w, 0.0):F1} m across. Use more shafts (smaller units) or less side protection.");
        lay.Geo.Plant = new PlantPlan(p.Tech.Fuel, sp, segs, wingT, wing, end, w, h, db, top, ag.Armoured, ag.RoofMm, tds,
            Shipgen.Decks.DeckStack(design, D).Select(s => s.Z).ToList());
        return segs.Sum(s => s.Len);
    }

    /// <summary>The plan's segments as (kind, length).</summary>
    public static List<(string Kind, double Len)> PlanSegments(PlantPlan plan) => plan.Segments;

    /// <summary>Place machinery segments one after another, aft from x_front: [(kind, x0, x1)].</summary>
    public static List<(string Kind, double X0, double X1)> StackMachinery(IEnumerable<(string Kind, double Len)> segs, double xFront)
    {
        var result = new List<(string, double, double)>();
        double x = xFront;
        foreach (var (kind, l) in segs)
        {
            result.Add((kind, x - l, x));
            x -= l;
        }
        return result;
    }

    /// <summary>Compartments for the placed machinery segments: boiler rooms, engine rooms and bunkers, wing bunkers and
    /// the casing over a plant taller than its space. Sets lay.geo machinery and machinery_x.</summary>
    public static void AddMachineryRooms(Layout lay, List<(string Kind, double X0, double X1)> placed, double innerHw, double depth)
    {
        var plan = lay.Geo.Plant!;
        var fuel = plan.Fuel;
        var names = new Dictionary<string, string> { ["boiler"] = "Boiler room", ["engine"] = "Engine room", ["bunker"] = "Bunker" };
        var count = new Dictionary<string, int> { ["boiler"] = 0, ["engine"] = 0, ["bunker"] = 0 };
        var rooms = new List<Compartment>();
        double maxRoom = Math.Max(6.0, 0.07 * lay.Hull.L);
        var segRooms = new Dictionary<long, List<string>>();
        for (int si = 0; si < placed.Count; si++)
        {
            var (kind, x0, x1) = placed[si];
            long n = Math.Max(1L, (long)Math.Ceiling((x1 - x0) / maxRoom - 1e-9));
            for (long k = 0; k < n; k++)
            {
                count[kind] += 1;
                double a = x1 - (k + 1) * (x1 - x0) / n, b = x1 - k * (x1 - x0) / n;
                var c = new Compartment
                {
                    Id = $"{names[kind]} {count[kind]}", Kind = kind != "bunker" ? $"{kind}_room" : "bunker", X0 = a, X1 = b,
                    HalfWidth = plan.WingM != 0 ? plan.Width / 2 : Math.Min(innerHw, plan.Width / 2), Fuel = kind == "bunker" ? fuel : null,
                };
                rooms.Add(c);
                if (!segRooms.TryGetValue(si, out var l))
                    segRooms[si] = l = [];
                l.Add(c.Id);
            }
        }
        lay.Compartments.AddRange(rooms);
        double mx0 = placed.Select(s => s.X0).Min(), mx1 = placed.Select(s => s.X1).Max();
        if (plan.WingM > 0)
        {
            double y = plan.Width / 2 + plan.WingM / 2;
            foreach (int side in new[] { 1, -1 })
            {
                string sd = side > 0 ? "S" : "P";
                lay.Compartments.Add(new Compartment
                {
                    Id = $"Wing bunker {sd}", Kind = "bunker", Fuel = fuel, PerSection = $"Wing bunker {{}} {sd}", X0 = mx0, X1 = mx1,
                    Y = side * y, HalfWidth = plan.WingM / 2, Base = plan.InnerBottom - depth, Top = 0.0, Tonnes = Math.Round(plan.WingT / 2, 1),
                });
            }
        }
        var sp = plan.Space;
        if (sp.Protrusion > 0)
        {
            var engines = placed.Where(s => s.Kind == "engine").ToList();
            if (engines.Count == 0)
                engines = placed;
            double cw = sp.Rows != 0 ? Math.Min(plan.Width, sp.Rows * (sp.Unit.W + 0.8)) : plan.Width;
            double top = plan.InnerBottom + sp.Unit.H - depth;
            lay.Casings = engines.Select((e, i) => new Casing($"Machinery casing {i + 1}", e.X0, e.X1, cw, plan.Top - depth, top,
                plan.Armoured ? plan.DeckMm : 0.0)).ToList();
            if (plan.Armoured)
                foreach (var c in lay.Casings)
                {
                    double area = 2 * ((c.X1 - c.X0) + c.W) * (c.Top - c.Base) + (c.X1 - c.X0) * c.W;
                    lay.Weights.Add(new Weight(c.Id, "armour", area * plan.DeckMm / 1000 * 7.85, (c.X0 + c.X1) / 2, ZRel.Deck((c.Base + c.Top) / 2)));
                }
        }
        lay.Geo.Machinery = (mx0, mx1);
        lay.Geo.MachineryX = (mx0 + mx1) / 2;
        foreach (var f in lay.FunnelsPlanned)
            f.Serves = f.Seg is long seg && segRooms.TryGetValue(seg, out var l) ? l : [];
    }

    public const double FunnelAbove = 3.0;
    const double StackNatural = 25.0;
    const double BridgeOverBoilers = 0.85;
    const double BridgeClear = 1.0;
    const int TowerTaperFrom = 6;
    const double TowerTaperW = 0.07, TowerTaperL = 0.04, TowerMinW = 0.45, TowerMinL = 0.6;

    /// <summary>(width, length) of the bridge tower's level k as fractions of the bridge's footprint.</summary>
    static (double W, double L) TowerTaper(long k)
    {
        long n = Math.Max(0, k - TowerTaperFrom);
        if (n == 0)
            return (1.0, 1.0);
        return (Math.Max(TowerMinW, Math.Pow(1 - TowerTaperW, n)), Math.Max(TowerMinL, Math.Pow(1 - TowerTaperL, n)));
    }

    /// <summary>The lowest level the navigating bridge can stand at (2 at least) to see over a turret roof this high.</summary>
    static long BridgeLevel(double? roof)
    {
        if (roof is null)
            return 2;
        return Math.Max(2L, (long)Math.Ceiling((roof.Value + BridgeClear) / LevelH - 1e-9) + 1);
    }

    /// <summary>Funnel count and size for the planned machinery, with funnel tops `top` above the main deck.</summary>
    public static (long N, double W, double L) PlanFunnels(Layout lay, Design design, Navarch.Result res, double beam, double top,
        List<double>? groups = null)
    {
        var plant = lay.Geo.Plant!;
        groups ??= plant.Segments.Where(s => s.Kind == "boiler").Select(s => s.Len).ToList();
        double stack = res.Depth - plant.InnerBottom - 1.0 + top;
        var fp = Powerplant.PlanFunnels(res.Plant, res.PowerShp, groups, beam, stack);
        long sumC = fp.Total;
        if (design.Funnels is double f && f > sumC)
            fp = Powerplant.PlanFunnels(res.Plant, res.PowerShp, groups, beam, stack, extra: (long)f - sumC);
        lay.Geo.FunnelPlan = fp;
        long total = fp.Total;
        if (fp.Needed is long needed)
            lay.Warnings.Add($"The plant's gas needs {needed:N0} funnels; it gets {total}, with the gas at {fp.Velocity:F0} m/s.");
        lay.Geo.SmokeReach = Powerplant.SmokeReach(res.Plant, res.PowerShp);
        return (total, fp.Width, fp.Length);
    }

    /// <summary>Index of the plan's (first, largest) boiler segment, or its engines for an engines-only plant.</summary>
    public static int BoilerSeg(Layout lay)
    {
        var segs = PlanSegments(lay.Geo.Plant!);
        return Enumerable.Range(0, segs.Count).MaxBy(i => (segs[i].Kind == "boiler" ? 1 : 0, segs[i].Kind == "engine" ? 1 : 0, segs[i].Len));
    }

    /// <summary>The machinery segment funnel i serves where the funnels stand together (merchants, carriers).</summary>
    public static long FunnelSeg(Layout lay, long i)
    {
        var segs = PlanSegments(lay.Geo.Plant!);
        var boilers = Enumerable.Range(0, segs.Count).Where(k => segs[k].Kind == "boiler").ToList();
        var counts = lay.Geo.FunnelPlan!.Counts;
        if (boilers.Count == 0 || counts.Count != boilers.Count)
            return BoilerSeg(lay);
        for (int j = 0; j < boilers.Count; j++)
        {
            if (i < counts[j])
                return boilers[j];
            i -= counts[j];
        }
        return boilers[^1];
    }

    /// <summary>Weights of funnel f standing to `top` above the main deck, and of its uptakes from the boilers it serves
    /// (centred at served_x) up to the deck, with armoured gratings where they pierce an armoured deck.</summary>
    public static void AddFunnelWeights(Layout lay, Funnel f, double top, double servedX, double depth)
    {
        var plant = lay.Geo.Plant!;
        double z0 = f.Z0 ?? 0.0;
        double vert = Math.Max(1.0, depth - (plant.InnerBottom + plant.Space.Unit.H)) + z0;
        double horiz = Math.Abs(f.X - servedX);
        var (wF, wU) = Powerplant.FunnelWeight(f.W, f.L, top - z0, vert, horiz);
        lay.Weights.Add(new Weight(f.Id, "superstructure", wF, f.X, ZRel.Deck((z0 + top) / 2)));
        lay.Weights.Add(new Weight($"Uptakes {f.Id}", "machinery", wU, (f.X + servedX) / 2, ZRel.Deck(z0 - vert / 2)));
        var fp = lay.Geo.FunnelPlan;
        if (plant.Armoured && fp != null)
            lay.Weights.Add(new Weight($"Gratings {f.Id}", "armour", 0.6 * fp.Area / Math.Max(1L, fp.Total), servedX,
                ZRel.Deck(plant.Top - depth)));
        f.Top = top;
        lay.FunnelsPlanned.Add(f);
    }

    /// <summary>The funnel top raised to FunnelAbove over the highest block any funnel passes through.</summary>
    public static double RaiseFunnels(Layout lay, List<Funnel> funnels, List<Block> blocks, double top)
    {
        double nw = top;
        foreach (var f in funnels)
        {
            var pts = Footprint.Rect(f.X - f.L / 2, f.Y - f.W / 2, f.X + f.L / 2, f.Y + f.W / 2).Points();
            foreach (var b in blocks)
                if (b.Points is { Count: > 0 } bp && Geometry.PolygonsIntersect(bp, pts))
                    nw = Math.Max(nw, b.TopZ + FunnelAbove);
        }
        if (nw <= top + 1e-6)
            return top;
        var byId = funnels.ToDictionary(f => f.Id, StringComparer.Ordinal);
        foreach (var f in funnels)
            f.Top = nw;
        lay.Footprints = lay.Footprints.Select(o => o with { Top = byId.ContainsKey(o.Owner) ? nw : o.Top }).ToList();
        foreach (var wt in lay.Weights)
        {
            if (byId.TryGetValue(wt.Name, out var f) && wt.Group == "superstructure")
            {
                double z0 = f.Z0 ?? 0.0;
                wt.W = Powerplant.FunnelWeight(f.W, f.L, nw - z0, 0.0, 0.0).Funnel;
                wt.ZRel = ZRel.Deck((z0 + nw) / 2);
            }
        }
        return nw;
    }

    // Warship and carrier planform
    static readonly Dictionary<string, double> Large = new() { ["flare"] = 0.08, ["bow_share"] = 0.5, ["transom"] = 0.1 };
    static readonly (string K, double V)[] Small = [("flare", 0.22), ("mid", 0.25), ("bow_share", 0.53), ("transom", 0.75)];
    const double MidbodyK = 1.5;
    static readonly (double Lo, double Hi) PlanSize = (1500.0, 4000.0);

    /// <summary>Bow and stern tapers for a deck of size_m2 (L x B) whose plan fills cwp(cb) + flare of its box.</summary>
    public static (HullEnd Bow, HullEnd Stern) Planform(double cb, double sizeM2)
    {
        double s = Math.Min(1.0, Math.Max(0.0, (sizeM2 - PlanSize.Lo) / (PlanSize.Hi - PlanSize.Lo)));
        var large = new Dictionary<string, double>(Large) { ["mid"] = Math.Min(0.4, Math.Max(0.0, (cb - 0.5) * MidbodyK)) };
        var k = Small.ToDictionary(kv => kv.K, kv => kv.V + (large[kv.K] - kv.V) * s);
        double mid = k["mid"], transom = k["transom"];
        double bt = (1 - mid) * k["bow_share"], st = (1 - mid) * (1 - k["bow_share"]);
        double target = Math.Min(0.97, Geometry.Cwp(cb) + k["flare"]);
        double Fill(double p) => mid + bt * Hull.EndFill(p, "pointed") + st * (transom + (1 - transom) * Hull.EndFill(p, "round"));
        double lo = 1.05, hi = 12.0;
        for (int i = 0; i < 30; i++)
        {
            double p = (lo + hi) / 2;
            (lo, hi) = Fill(p) < target ? (p, hi) : (lo, p);
        }
        double pw = (lo + hi) / 2;
        return (new HullEnd { Taper = bt, Power = pw }, new HullEnd { Taper = st, Power = pw, Transom = transom, Shape = "round" });
    }

    public static HullSpec HullSpecOf(Design design)
    {
        var (bow, stern) = Planform(design.BlockCoefficient, design.HullLength * design.HullBeam);
        return new HullSpec(design.HullLength, design.HullBeam, bow, stern);
    }

    /// <summary>The citadel: the stretch the vital spaces, belt and citadel armour decks cover.</summary>
    public static void SetCitadel(Layout lay, double x0, double x1) => lay.Geo.Citadel = (x0, x1);

    /// <summary>The steering gear over the rudders, for every style. Returns the room.</summary>
    public static Compartment AddSteering(Layout lay, double? x0 = null, double? x1 = null, double? halfWidth = null,
        string name = "Steering gear")
    {
        double L = lay.Hull.L, B = lay.Hull.B;
        double a = x0 ?? -L / 2 + Propulsion.Steering.X0 * L;
        double b = x1 ?? -L / 2 + Propulsion.Steering.X1 * L;
        var (bse, top) = Ordnance.Span(lay.Geo.Plant!);
        var room = new Compartment
        {
            Id = name, Kind = "steering", X0 = a, X1 = b, Base = bse, Top = top, HalfWidth = halfWidth ?? Propulsion.Steering.Hw * B,
        };
        lay.Compartments.Add(room);
        lay.Geo.Steering = (a, b);
        lay.Geo.SteeringBeam = 2 * Enumerable.Range(0, 8).Select(k => lay.Hull.HalfWidth(a + (b - a) * (k + 0.5) / 8)).Sum() / 8;
        return room;
    }

    static readonly Dictionary<string, int> DrawKind = new() { ["main"] = 2, ["secondary"] = 1 };

    /// <summary>Each mount's z, the draw order: its rank by base height (equal keys share a z).</summary>
    static void DrawOrder(List<Mount> mounts)
    {
        (double, int) Key(Mount m) => (Math.Round(m.Base, 3), DrawKind.TryGetValue(m.Kind, out var k) ? k : 0);
        var keys = mounts.Select(Key).Distinct().ToList();
        keys.Sort();
        var rank = keys.Select((k, i) => (k, i)).ToDictionary(t => t.k, t => (long)t.i);
        foreach (var m in mounts)
            m.Z = rank[Key(m)];
    }

    /// <summary>The laid-out ship's parts on lay and its dressing, for every style.</summary>
    public static Layout FinishLayout(Layout lay, Design design, HullSpec hs, List<Mount> mounts,
        OrderedDictionary<string, TurretType> turretTypes, List<Block> blocks, List<Funnel> funnels, List<Mast> masts, List<AaMount> aaOut,
        double funTop, string? deck = null)
    {
        DrawOrder(mounts);
        for (int k = 0; k < masts.Count; k++)
        {
            masts[k].Top ??= funTop + MastAboveFunnel;
            masts[k].Id ??= masts.Count == 1 ? "Mast" : $"Mast {k + 1}";
        }
        lay.Dressing = new Dressing
        {
            Deck = deck ?? "steel", DeckInset = hs.DeckInset, PlankSpacing = hs.PlankSpacing,
            Masts = masts.Select(m => new MastDressing(m.Id!, m.Yard, m.Tripod, m.Booms)).ToList(),
        };
        lay.TurretTypes = turretTypes;
        lay.Mounts = mounts;
        lay.Blocks = blocks;
        lay.Funnels = funnels;
        lay.Aa = aaOut;
        lay.Masts = masts;
        lay.FunTop = funTop;
        FireControl.SearchRadar(lay, design, blocks, masts, funTop);
        lay.Geo.Windage = LateralProfile(lay, blocks, funnels, masts, mounts, aaOut, funTop);
        return lay;
    }

    const double WindCol = 1.0;

    /// <summary>A mast's default top over the funnels' tops.</summary>
    public const double MastAboveFunnel = 6.0;

    /// <summary>What the wind sees from abeam above the main deck.</summary>
    static Windage LateralProfile(Layout lay, List<Block> blocks, List<Funnel> funnels, List<Mast> masts, List<Mount> mounts,
        List<AaMount> aa, double funTop)
    {
        var cols = new OrderedDictionary<long, List<(double Z0, double Z1, double F)>>();

        void Add(double x0, double x1, double z0, double z1)
        {
            if (z1 <= z0 || x1 <= x0)
                return;
            for (long i = (long)(Math.Floor(x0 / WindCol)); i < (long)(Math.Ceiling(x1 / WindCol)); i++)
            {
                double f = (Math.Min(x1, (i + 1) * WindCol) - Math.Max(x0, i * WindCol)) / WindCol;
                if (!cols.TryGetValue(i, out var l))
                    cols[i] = l = [];
                l.Add((z0, z1, f));
            }
        }

        foreach (var b in blocks)
            Add(b.X0, b.X1, b.Base, b.TopZ);
        foreach (var dk in lay.Decks)
        {
            var xs = dk.Points.Select(p => p.X).ToList();
            Add(xs.Min(), xs.Max(), 0.0, dk.Top);
        }
        foreach (var f in funnels)
            Add(f.X - f.L / 2, f.X + f.L / 2, f.Z0 ?? 0.0, funTop);
        foreach (var m in masts)
        {
            double w = m.Tripod ? 1.5 : 0.7;
            Add(m.X - w / 2, m.X + w / 2, 0.0, m.Top ?? funTop + 6.0);
        }
        foreach (var m in mounts)
        {
            if (m.Casemate || m.Top <= 0)
                continue;
            double r = m.T.R;
            Add(m.X - r, m.X + r, 0.0, m.Top);
        }
        foreach (var a in aa)
        {
            double r = Geometry.AaCfg[a.Type].R;
            Add(a.X - r, a.X + r, a.Base, a.Base + 2.0);
        }
        double area = 0.0, mom = 0.0;
        foreach (var spans in cols.Values)
        {
            spans.Sort();
            (double A, double B)? cur = null;
            foreach (var (z0, z1, f) in spans)
            {
                if (cur is { } c && z0 <= c.B)
                {
                    if (z1 > c.B)
                    {
                        area += (z1 - c.B) * f * WindCol;
                        mom += (z1 - c.B) * f * WindCol * (c.B + z1) / 2;
                        cur = (c.A, z1);
                    }
                    continue;
                }
                cur = (z0, z1);
                area += (z1 - z0) * f * WindCol;
                mom += (z1 - z0) * f * WindCol * (z0 + z1) / 2;
            }
        }
        return new Windage(area, area != 0 ? mom / area : 0.0);
    }

    /// <summary>A, B, C, then A4, A5, ... (letters[0] plus the turret's number in its group).</summary>
    public static string TurretName(string letters, int i) => i < letters.Length ? letters[i].ToString() : $"{letters[0]}{i + 1}";

    /// <summary>How many turrets of the fore and aft groups step up (superfire).</summary>
    public static (int Fore, int Aft) SteppedCounts(BatteryInput main)
    {
        int nf = main.Fore ?? 0, na = main.Aft ?? 0;
        return main.Superfire switch
        {
            null or { All: true } => (nf, na),
            { All: false } => (Math.Min(nf, 1), Math.Min(na, 1)),
            var sf => (sf.Fore ?? nf, sf.Aft ?? na),
        };
    }

    /// <summary>superstructure.deckhouse_levels (1 when the design gives none).</summary>
    static long DeckhouseLevels(Design design) => design.Superstructure?.DeckhouseLevels ?? 1;

    /// <summary>superstructure.tower_levels: the bridge tower's top level; default when the design gives none.</summary>
    public static long TowerLevels(Design design, long def) => design.Superstructure?.TowerLevels ?? def;

    /// <summary>superstructure.aft_control (on by default).</summary>
    static bool AftControl(Design design) => design.Superstructure?.AftControl ?? true;

    /// <summary>superstructure.levels_over_bridge (1 by default).</summary>
    static long LevelsOverBridge(Design design) => design.Superstructure?.LevelsOverBridge ?? 1;
}
