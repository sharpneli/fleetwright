namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>What a superstructure block is for (add_block's role).</summary>
    public static readonly string[] BLOCK_ROLES = ["deckhouse", "bridge", "aft_control", "director", "island", "hangar", "casemate"];

    public static double BlockBase(PyDict b) => b.F("z0", 0.0) + LEVEL_H * (b.F("level") - 1);
    public static double BlockTop(PyDict b) => b.F("z0", 0.0) + LEVEL_H * b.F("level");

    public static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

    /// <summary>Superstructure block standing on z0: footprint, weight, record. points: an outline polygon instead of
    /// the rounded rectangle; x0, x1, y and w then become its bounding box, and rf, rb are 0.</summary>
    public static PyDict AddBlock(Layout lay, List<PyDict> blocks, string bid, double x0, double x1, double w, long level,
        double rf, double rb, double y = 0.0, double z0 = 0.0, string? layer = null, string kind = "superstructure",
        double? tPerM2 = null, IReadOnlyList<Pt>? points = null, string role = "deckhouse", bool office = false)
    {
        if (!BLOCK_ROLES.Contains(role))
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
        var b = PyDict.Of(("id", bid), ("kind", kind), ("role", role), ("x0", x0), ("x1", x1), ("y", y), ("w", w),
            ("level", level), ("rf", rf_), ("rb", rb_));
        if (office)
            b["office"] = true;
        double area, xc;
        if (pts != null)
        {
            (area, xc) = Geometry.PolygonCentroid(pts);
            b.Update(("points", pts), ("area", Math.Round(area, 2)));
        }
        else
            (area, xc) = ((x1 - x0) * w, (x0 + x1) / 2);
        if (z0 != 0)
            b["z0"] = z0;
        if (layer != null || z0 != 0)
            b["layer"] = layer ?? "upper";
        blocks.Add(b);
        lay.Occupy(pts != null ? Footprint.Poly(pts) : Footprint.Rect(x0, y - w / 2, x1, y + w / 2), BlockBase(b), BlockTop(b), bid);
        double tpm = tPerM2 ?? lay.SupT;
        double wt = area * tpm;
        if (kind == "superstructure")
            wt += BlockPlating(lay, b);
        lay.Weights.Add(new Weight(bid, "superstructure", wt, xc, ZRel.Deck((BlockBase(b) + BlockTop(b)) / 2)));
        return b;
    }

    /// <summary>The hull's own gauge, mm: a box-model hull's planking, else the plate model's minimum gauge.</summary>
    public static double OwnPlateMm(Layout lay) => lay.OwnPlateMmValue ?? HullWeight.TMinMm(lay.Hull.L, lay.Construction);

    /// <summary>A block's wall plating: records b["_plate_mm"] and returns the weight of plate beyond the own gauge.</summary>
    public static double BlockPlating(Layout lay, PyDict b)
    {
        double own = HullWeight.SUP_PLATE_K * OwnPlateMm(lay);
        double mm = Math.Max(own, lay.SupPlate.Plating);
        if (Py.In(b["role"], "bridge", "aft_control"))
            mm = Math.Max(mm, lay.SupPlate.Control);
        b["_plate_mm"] = Math.Round(mm, 1);
        var pts = Geometry.BlockOutline(b);
        int n = pts.Count;
        double perim = Enumerable.Range(0, n).Select(i => double.Hypot(pts[(i - 1 + n) % n].X - pts[i].X, pts[(i - 1 + n) % n].Y - pts[i].Y)).Sum();
        return HullWeight.ExtraPlateT(perim * (BlockTop(b) - BlockBase(b)), mm, own);
    }

    const double RAISED_INSET = 0.3;

    /// <summary>A raised stretch of hull: the weather deck `levels` decks above the main deck from x0 to x1.</summary>
    public static PyDict AddRaised(Layout lay, PyDict design, string rid, double x0, double x1, long levels = 1,
        (long Aft, long Fwd)? breaks = null)
    {
        var hull = lay.Hull;
        double L = hull.L;
        double h = levels * LEVEL_H;
        var pts = hull.Points(inset: RAISED_INSET, xMin: x0, xMax: x1);
        lay.Raised.Add(PyDict.Of(("id", rid), ("x0", x0), ("x1", x1), ("levels", levels)));
        lay.Geo.Raised = lay.Raised;
        lay.Decks.Add(PyDict.Of(("id", rid), ("kind", "deck"), ("points", pts), ("base", 0.0), ("top", h)));
        lay.Occupy(Footprint.Poly(pts), 0.0, h, rid);
        var (area, xc) = Geometry.PolygonCentroid(pts);
        var brk = breaks ?? (levels, levels);
        object endObj = Py.SumObj(new[] { (x0, brk.Aft), (x1, brk.Fwd) }.Where(t => -L / 2 + 0.5 < t.Item1 && t.Item1 < L / 2 - 0.5)
            .Select(t => (object?)(2 * hull.HalfWidth(t.Item1) * t.Item2 * LEVEL_H)));
        double endM2 = Py.ToDouble(endObj);
        double sideM2 = 2 * (x1 - x0) * h;
        var c = HullWeight.Construction(design);
        double t = HullWeight.RaisedT(L, c, area, sideM2, endM2, HullWeight.Plating(design).F("shell_mm"));
        lay.Weights.Add(new Weight(rid, "hull", t, xc, ZRel.Deck(h * (area + 0.5 * (sideM2 + endM2)) / (area + sideM2 + endM2))));
        return lay.Raised[^1];
    }

    public static readonly string[] RAISED_ANCHORS = Style.RAISED_ANCHORS;

    /// <summary>Does the hull.raised entry q run over the feature?</summary>
    public static bool RaisedCovers(PyDict q, string feature)
    {
        int a = Array.IndexOf(RAISED_ANCHORS, q.S("from")), b = Array.IndexOf(RAISED_ANCHORS, q.S("to"));
        int i = Math.Min(a, b), j = Math.Max(a, b);
        int f = Array.IndexOf(RAISED_ANCHORS, feature);
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
        var out_ = new List<(double, double, long, (long, long))>();
        for (int i = 0; i < segs.Count; i++)
        {
            var (a, b, lv) = segs[i];
            if (lv != 0)
            {
                long d0 = lv - (0 <= i - 1 && i - 1 < segs.Count ? segs[i - 1].Lv : 0);
                long d1 = lv - (0 <= i + 1 && i + 1 < segs.Count ? segs[i + 1].Lv : 0);
                out_.Add((a, b, lv, (Math.Max(0, d0), Math.Max(0, d1))));
            }
        }
        return out_;
    }

    /// <summary>raised_profile's stretches with ids: Forecastle, Poop, Raised deck.</summary>
    public static List<(string Id, double X0, double X1, long Lv, (long, long) Brk)> RaisedNames(
        List<(double X0, double X1, long Lv, (long Aft, long Fwd) Brk)> prof, double L)
    {
        var out_ = new List<(string? Id, double X0, double X1, long Lv, (long, long) Brk)>();
        for (int k = 0; k < prof.Count; k++)
        {
            var (x0, x1, lv, brk) = prof[k];
            bool bow = Enumerable.Range(k, Math.Max(0, prof.Count - 1 - k)).All(j => prof[j].X1 >= prof[j + 1].X0 - 1e-6)
                       && prof[^1].X1 >= L / 2 - 1e-6;
            bool stern = Enumerable.Range(0, k).All(j => prof[j].X1 >= prof[j + 1].X0 - 1e-6) && prof[0].X0 <= -L / 2 + 1e-6;
            int n = bow ? prof.Count - k : k + 1;
            string? rid = bow || stern ? (bow ? "Forecastle" : "Poop") + (n > 1 ? $" {n}" : "") : null;
            out_.Add((rid, x0, x1, lv, brk));
        }
        var mids = Enumerable.Range(0, out_.Count).Where(i => out_[i].Id is null).ToList();
        for (int k = 0; k < mids.Count; k++)
        {
            var o = out_[mids[k]];
            out_[mids[k]] = ("Raised deck" + (mids.Count > 1 ? $" {k + 1}" : ""), o.X0, o.X1, o.Lv, o.Brk);
        }
        return out_.Select(o => (o.Id!, o.X0, o.X1, o.Lv, o.Brk)).ToList();
    }

    const double DH_SLIVER = 3.0;
    const double RAISED_CLEAR = 1.1;

    /// <summary>How much higher a main mount must stand for its barrels to clear the raised stretches they sweep over.</summary>
    public static double RaisedLift(Layout lay, PyDict m)
    {
        if (lay.Raised.Count == 0)
            return 0.0;
        double axis = m.F("base") + 0.55 * (m.F("top") - m.F("base"));
        var tr = Arcs.MountTraverse(m)!;
        List<Pt>? sweep = null;
        double need = 0.0;
        foreach (var dk in lay.Decks)
        {
            if (!Py.Eq(dk["kind"], "deck") || dk.F("top") + RAISED_CLEAR <= axis + need)
                continue;
            sweep ??= Geometry.SectorPolygon(m.F("x"), m.F("y"), Geometry.TurretReach(m.D("t")) + 0.5, tr[0], tr[1]);
            if (Geometry.PolygonsIntersect(sweep, Geometry.Pts(dk["points"])))
                need = dk.F("top") + RAISED_CLEAR - axis;
        }
        return need;
    }

    /// <summary>Where something l long and w wide could stand on the blocks' roofs: (x, y, z0, pair) candidates.</summary>
    public static List<(double X, double Y, double Z0, bool Pair)> RoofSpots(IEnumerable<PyDict> blocks, double l, double w,
        double step = 0.5)
    {
        var out_ = new List<(double, double, double, bool)>();
        foreach (var b in blocks)
        {
            if (Py.Eq(b.Get("kind"), "director"))
                continue;
            double z0 = BlockTop(b);
            double bx0 = b.F("x0"), bx1 = b.F("x1"), by = b.F("y"), bw = b.F("w");
            var xs = new List<double>();
            if (bx1 - bx0 < l)
                xs.Add((bx0 + bx1) / 2);
            else
            {
                long n = (long)((bx1 - bx0 - l) / step);
                for (long k = 0; k <= n; k++)
                    xs.Add(bx0 + l / 2 + k * step);
            }
            double ye = bw / 2 - w / 2;
            if (b.B("points") && !b.Has("_slabs"))
                b["_slabs"] = new Slabs(Geometry.Pts(b["points"]));
            var slabs = b.Get("_slabs") as Slabs;
            double hwR = w < bw ? w / 2 - 0.3 : 0.0;
            var sp0 = new List<(double Lo, double Hi)>();
            var sp1 = new List<(double Lo, double Hi)>();
            foreach (var x in xs)
            {
                if (slabs != null)
                {
                    slabs.At(x + -l / 2, sp0);
                    slabs.At(x + l / 2, sp1);
                }
                if (slabs is null || OnRoof(sp0, sp1, by, hwR))
                    out_.Add((x, by, z0, false));
                if (Math.Abs(by) < 1e-6 && ye > w / 2 + 0.1 && (slabs is null || OnRoof(sp0, sp1, ye, hwR)))
                    out_.Add((x, ye, z0, true));
            }
        }
        return out_;
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
        readonly List<double> xs;
        readonly List<List<(double X0, double Y0, double X1, double Y1)>> slabs = [];

        public Slabs(IReadOnlyList<Pt> pts)
        {
            xs = (new HashSet<double>(pts.Select(p => p.X))).Order().ToList();
            int n = pts.Count;
            var edges = new List<(double, double, double, double)>();
            for (int i = 0; i < n; i++)
            {
                var (x0, y0) = pts[i];
                var (x1, y1) = pts[(i + 1) % n];
                if (x0 != x1)
                    edges.Add((x0, y0, x1, y1));
            }
            for (int i = 0; i < xs.Count - 1; i++)
            {
                double m = (xs[i] + xs[i + 1]) / 2;
                slabs.Add(edges.Where(e => (e.Item1 > m) != (e.Item3 > m)).OrderBy(e => e.Item2 + (m - e.Item1) * (e.Item4 - e.Item2) / (e.Item3 - e.Item1)).ToList());
            }
        }

        /// <summary>The stretches of y inside the polygon at x.</summary>
        public List<(double Lo, double Hi)> At(double x)
        {
            var out_ = new List<(double, double)>();
            At(x, out_);
            return out_;
        }

        /// <summary>At(x) into a list the caller keeps (cleared first).</summary>
        public void At(double x, List<(double Lo, double Hi)> out_)
        {
            out_.Clear();
            int i = xs.UpperBound(x) - 1;
            if (i < 0 || i >= slabs.Count)
                return;
            var es = slabs[i];
            for (int k = 0; k + 1 < es.Count; k += 2)
            {
                var e = es[k];
                var f = es[k + 1];
                out_.Add((e.Y0 + (x - e.X0) * (e.Y1 - e.Y0) / (e.X1 - e.X0), f.Y0 + (x - f.X0) * (f.Y1 - f.Y0) / (f.X1 - f.X0)));
            }
        }
    }

    const double MAST_T_K = 0.012;

    /// <summary>A mast's weight at half its height: tripod legs or a pole.</summary>
    public static void MastWeight(Layout lay, PyDict m, double top, string name, double bse = 0.0)
    {
        int legs = m.B("tripod") ? 3 : 1;
        double h = top - bse;
        lay.Weights.Add(new Weight(name, "superstructure", legs * MAST_T_K * Math.Pow(h, 2), m.F("x"), ZRel.Deck(bse + h / 2)));
    }

    static readonly double[] AA_ROOF_PEN = [0.05, 0.05, 0.0, 0.0, 0.03];
    const double AA_SINGLE_PEN = 0.1, AA_DECK_PEN = 0.3;

    /// <summary>The magazines grouped fore and aft of the machinery (warships): (battery, mounts, m3 per mount).</summary>
    public static Dictionary<string, List<(string Bat, long N, double V)>> MagazinePlan(PyDict design,
        IEnumerable<(string Id, string Grp, Gun G)> wings)
    {
        var out_ = new Dictionary<string, List<(string, long, double)>> { ["fore"] = [], ["aft"] = [] };
        foreach (var (wid, grp, g) in wings)
            out_[grp].Add((wid, 2, Ordnance.AmmoM3(g.T)));
        var secs = Batteries.SecondaryBatteries(design);
        for (int k = 0; k < secs.Count; k++)
        {
            var s = secs[k];
            long n = Py.ToLong(s["per_side"]);
            if (n == 0)
                continue;
            string kind = Py.Eq(s.Get("mount"), "casemate") ? "casemate" : "auto";
            var t = Geometry.BatteryType(s, kind).T;
            foreach (var (grp, pairs) in new[] { ("fore", (n + 1) / 2), ("aft", n / 2) })
                if (pairs != 0)
                    out_[grp].Add((Batteries.BatteryPrefix(k), 2 * pairs, Ordnance.AmmoM3(t)));
        }
        return out_;
    }

    /// <summary>The warship's magazines (ordnance.stow), linked both ways.</summary>
    public static void AddMagazines(Layout lay, List<PyDict> mounts, double innerHw, Dictionary<string, (double X0, double X1)>? groups = null)
    {
        groups ??= [];
        var plan = lay.Geo.Plant ?? new PyDict();
        double ghw = plan.B("wing_m") ? plan.F("width") / 2 : Math.Min(innerHw, plan.F("width", 2 * innerHw) / 2);
        var zones = new List<PyDict>();
        var batteries = new OrderedDictionary<string, List<PyDict>>(StringComparer.Ordinal);
        foreach (var m in mounts)
        {
            if (!Py.In(m["kind"], "main", "secondary"))
                continue;
            if (groups.Count > 0 && (Py.Eq(m["kind"], "secondary") || (m.B("wing") && !m.B("echelon"))))
            {
                string bat = m.S("battery");
                if (!batteries.TryGetValue(bat, out var l))
                    batteries[bat] = l = [];
                l.Add(m);
                continue;
            }
            zones.Add(Ordnance.OwnZone(m, innerHw));
        }
        var rooms = new Dictionary<string, List<object?>> { ["fore"] = [], ["aft"] = [] };
        foreach (var (bat, ms) in batteries)
        {
            var pairs = (new HashSet<double>(ms.Select(m => m.F("x")))).OrderDescending().ToList();
            int nFore = pairs.Count;
            if (ms[0].B("wing"))
                nFore = Py.Eq(ms[0]["magazine_end"], "fore") ? pairs.Count : 0;
            else if (groups.ContainsKey("fore") && groups.ContainsKey("aft"))
                nFore = (pairs.Count + 1) / 2;
            nFore = !groups.ContainsKey("aft") ? pairs.Count : !groups.ContainsKey("fore") ? 0 : nFore;
            foreach (var (grp, xs) in new[] { ("fore", pairs.Take(nFore).ToList()), ("aft", pairs.Skip(nFore).ToList()) })
            {
                var sel = ms.Where(m => xs.Contains(m.F("x"))).Select(m => m["id"]).ToList();
                if (sel.Count > 0)
                    rooms[grp].Add(PyDict.Of(("id", $"Magazine {bat} {grp}"), ("mounts", sel)));
            }
        }
        foreach (var grp in new[] { "fore", "aft" })
            if (rooms[grp].Count > 0)
                zones.Add(PyDict.Of(("x0", groups[grp].X0), ("x1", groups[grp].X1), ("half_width", ghw), ("rooms", rooms[grp])));
        Ordnance.Stow(lay, mounts, zones);
    }

    /// <summary>The machinery space for the solved ship res, centred near x. Stores the plan in lay.geo.plant and
    /// returns the block's length.</summary>
    public static double PlanMachinery(Layout lay, PyDict design, Navarch.Result res, Hull hull, double x = 0.0)
    {
        var p = res.Plant;
        var armour = design.DOr("armour");
        double D = res.Depth, T = res.Draught;
        double tds = armour.F("tds_m", 0.0);
        double wing = Py.Eq(p.D("tech")["fuel"], "coal") && Py.Eq(p["bunkers"], "wing") ? p.F("wing_bunker_m") : 0.0;
        double w = Powerplant.STEEL_FRAME * 2 * hull.HalfWidth(x) - 2 * tds - 2 * wing;
        var ag = Armour.ArmourGeometry(design, hull.L, T, D, lay.Geo);
        bool armoured = Py.Truthy(ag["armoured"]);
        double top = ag["roof_z"] is not null ? ag.F("roof_z") : D;
        double db = Powerplant.DoubleBottom(D);
        double h = Math.Max(1.0, top - db);
        var sp = Powerplant.Space(p, res.PowerShp, w, h);
        double cb = design.D("hull").F("block_coefficient");
        var (wingT, end) = Powerplant.Bunkers(p, res.Fuel, sp.F("length"), w, h, hull.L, hull.B, cb, D, T, tds);
        var segs = Powerplant.Segments(p, sp, end);
        lay.Warnings.AddRange(Powerplant.Groups(p).Warns);
        long units = Powerplant.Rated(p, res.PowerShp).I("units");
        long nEng = sp.L("order").Count(k => Py.Eq(k, "engine"));
        if (nEng > units)
            lay.Warnings.Add($"machinery.arrangement has {nEng} engine groups for {units} " +
                             "engine unit(s): some engine rooms hold no engine.");
        if (!Py.Truthy(sp["fits"]))
        {
            var unit = (object?[])sp["unit"]!;
            lay.Fail("beam", $"The plant's units are {unit[1]:F1} m wide, but the machinery space is only " +
                             $"{Math.Max(w, 0.0):F1} m across. Use more shafts (smaller units) or less side protection.");
        }
        lay.Geo.Plant = PyDict.Of(("fuel", p.D("tech")["fuel"]), ("space", sp),
            ("segments", segs.Select(s => (object?)new object?[] { s.Kind, s.Len }).ToList()), ("wing_t", wingT), ("wing_m", wing),
            ("end_m", end), ("width", w), ("height", h), ("inner_bottom", db), ("top", top), ("armoured", armoured),
            ("deck_mm", ag["roof_mm"]), ("tds", tds), ("decks", Shipgen.Decks.DeckStack(design, D).Select(s => (object?)s.Z).ToList()));
        return segs.Select(s => s.Len).Sum();
    }

    /// <summary>The plan's segments as (kind, length).</summary>
    public static List<(string Kind, double Len)> PlanSegments(PyDict plan) =>
        plan.L("segments").Cast<object?[]>().Select(s => ((string)s[0]!, Py.ToDouble(s[1]))).ToList();

    /// <summary>Place machinery segments one after another, aft from x_front: [(kind, x0, x1)].</summary>
    public static List<(string Kind, double X0, double X1)> StackMachinery(IEnumerable<(string Kind, double Len)> segs, double xFront)
    {
        var out_ = new List<(string, double, double)>();
        double x = xFront;
        foreach (var (kind, l) in segs)
        {
            out_.Add((kind, x - l, x));
            x -= l;
        }
        return out_;
    }

    /// <summary>Compartments for the placed machinery segments: boiler rooms, engine rooms and bunkers, wing bunkers and
    /// the casing over a plant taller than its space. Sets lay.geo machinery and machinery_x.</summary>
    public static void AddMachineryRooms(Layout lay, List<(string Kind, double X0, double X1)> placed, double innerHw, double depth)
    {
        var plan = lay.Geo.Plant!;
        var fuel = plan["fuel"];
        var names = new Dictionary<string, string> { ["boiler"] = "Boiler room", ["engine"] = "Engine room", ["bunker"] = "Bunker" };
        var count = new Dictionary<string, int> { ["boiler"] = 0, ["engine"] = 0, ["bunker"] = 0 };
        var rooms = new List<PyDict>();
        double maxRoom = Math.Max(6.0, 0.07 * lay.Hull.L);
        var segRooms = new Dictionary<long, List<object?>>();
        for (int si = 0; si < placed.Count; si++)
        {
            var (kind, x0, x1) = placed[si];
            long n = Math.Max(1L, (long)Math.Ceiling((x1 - x0) / maxRoom - 1e-9));
            for (long k = 0; k < n; k++)
            {
                count[kind] += 1;
                double a = x1 - (k + 1) * (x1 - x0) / n, b = x1 - k * (x1 - x0) / n;
                var c = PyDict.Of(("id", $"{names[kind]} {count[kind]}"), ("kind", kind != "bunker" ? $"{kind}_room" : "bunker"),
                    ("x0", a), ("x1", b),
                    ("half_width", plan.B("wing_m") ? plan.F("width") / 2 : Math.Min(innerHw, plan.F("width") / 2)));
                if (kind == "bunker")
                    c["fuel"] = fuel;
                rooms.Add(c);
                if (!segRooms.TryGetValue(si, out var l))
                    segRooms[si] = l = [];
                l.Add(c["id"]);
            }
        }
        lay.Compartments.AddRange(rooms);
        double mx0 = placed.Select(s => s.X0).Min(), mx1 = placed.Select(s => s.X1).Max();
        if (plan.F("wing_m") > 0)
        {
            double y = plan.F("width") / 2 + plan.F("wing_m") / 2;
            foreach (int side in new[] { 1, -1 })
            {
                string sd = side > 0 ? "S" : "P";
                lay.Compartments.Add(PyDict.Of(("id", $"Wing bunker {sd}"), ("kind", "bunker"), ("fuel", fuel),
                    ("per_section", $"Wing bunker {{}} {sd}"), ("x0", mx0), ("x1", mx1), ("y", side * y),
                    ("half_width", plan.F("wing_m") / 2), ("base", plan.F("inner_bottom") - depth), ("top", 0.0),
                    ("tonnes", Math.Round(plan.F("wing_t") / 2, 1))));
            }
        }
        var sp = plan.D("space");
        if (sp.F("protrusion") > 0)
        {
            var engines = placed.Where(s => s.Kind == "engine").ToList();
            if (engines.Count == 0)
                engines = placed;
            var unit = (object?[])sp["unit"]!;
            double cw = sp.I("rows") != 0 ? Math.Min(plan.F("width"), sp.I("rows") * (Py.ToDouble(unit[1]) + 0.8)) : plan.F("width");
            double top = plan.F("inner_bottom") + Py.ToDouble(unit[2]) - depth;
            lay.Casings = engines.Select((e, i) => PyDict.Of(("id", $"Machinery casing {i + 1}"), ("x0", e.X0), ("x1", e.X1),
                ("w", cw), ("base", plan.F("top") - depth), ("top", top),
                ("armour_mm", Py.Truthy(plan["armoured"]) ? plan["deck_mm"] : 0L))).ToList();
            if (Py.Truthy(plan["armoured"]))
                foreach (var c in lay.Casings)
                {
                    double area = 2 * ((c.F("x1") - c.F("x0")) + c.F("w")) * (c.F("top") - c.F("base")) + (c.F("x1") - c.F("x0")) * c.F("w");
                    lay.Weights.Add(new Weight(c.S("id"), "armour", area * plan.F("deck_mm") / 1000 * 7.85,
                        (c.F("x0") + c.F("x1")) / 2, ZRel.Deck((c.F("base") + c.F("top")) / 2)));
                }
        }
        lay.Geo.Machinery = (mx0, mx1);
        lay.Geo.MachineryX = (mx0 + mx1) / 2;
        foreach (var f in lay.FunnelsPlanned)
            f["serves"] = f.Get("seg") is object seg && segRooms.TryGetValue(Py.ToLong(seg), out var l) ? l : new List<object?>();
    }

    public const double FUNNEL_ABOVE = 3.0;
    const double STACK_NATURAL = 25.0;
    const double BRIDGE_OVER_BOILERS = 0.85;
    const double BRIDGE_CLEAR = 1.0;
    const int TOWER_TAPER_FROM = 6;
    const double TOWER_TAPER_W = 0.07, TOWER_TAPER_L = 0.04, TOWER_MIN_W = 0.45, TOWER_MIN_L = 0.6;

    /// <summary>(width, length) of the bridge tower's level k as fractions of the bridge's footprint.</summary>
    static (double W, double L) TowerTaper(long k)
    {
        long n = Math.Max(0, k - TOWER_TAPER_FROM);
        if (n == 0)
            return (1.0, 1.0);
        return (Math.Max(TOWER_MIN_W, Math.Pow(1 - TOWER_TAPER_W, n)), Math.Max(TOWER_MIN_L, Math.Pow(1 - TOWER_TAPER_L, n)));
    }

    /// <summary>The lowest level the navigating bridge can stand at (2 at least) to see over a turret roof this high.</summary>
    static long BridgeLevel(double? roof)
    {
        if (roof is null)
            return 2;
        return Math.Max(2L, (long)Math.Ceiling((roof.Value + BRIDGE_CLEAR) / LEVEL_H - 1e-9) + 1);
    }

    /// <summary>Funnel count and size for the planned machinery, with funnel tops `top` above the main deck.</summary>
    public static (long N, double W, double L) PlanFunnels(Layout lay, PyDict design, Navarch.Result res, double beam, double top,
        List<double>? groups = null)
    {
        var plant = lay.Geo.Plant!;
        groups ??= PlanSegments(plant).Where(s => s.Kind == "boiler").Select(s => s.Len).ToList();
        double stack = res.Depth - plant.F("inner_bottom") - 1.0 + top;
        var fp = Powerplant.FunnelPlan(res.Plant, res.PowerShp, groups, beam, stack);
        long sumC = fp.L("counts").Sum(c => Py.ToLong(c));
        if (Py.Truthy(design.Get("funnels")) && design.F("funnels") > sumC)
            fp = Powerplant.FunnelPlan(res.Plant, res.PowerShp, groups, beam, stack, extra: Py.ToLong(design["funnels"]) - sumC);
        lay.Geo.FunnelPlan = fp;
        long total = fp.L("counts").Sum(c => Py.ToLong(c));
        if (Py.Truthy(fp.Get("needed")))
            lay.Warnings.Add($"The plant's gas needs {fp.I("needed"):N0} funnels; it gets {total}, with the gas " +
                             $"at {fp.F("velocity"):F0} m/s.");
        lay.Geo.SmokeReach = Powerplant.SmokeReach(res.Plant, res.PowerShp);
        return (total, fp.F("width"), fp.F("length"));
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
        var counts = lay.Geo.FunnelPlan!.L("counts").Select(c => Py.ToLong(c)).ToList();
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
    public static void AddFunnelWeights(Layout lay, PyDict f, double top, double servedX, double depth)
    {
        var plant = lay.Geo.Plant!;
        var sp = plant.D("space");
        double z0 = f.F("z0", 0.0);
        double vert = Math.Max(1.0, depth - (plant.F("inner_bottom") + Py.ToDouble(((object?[])sp["unit"]!)[2]))) + z0;
        double horiz = Math.Abs(f.F("x") - servedX);
        var (wF, wU) = Powerplant.FunnelWeight(f.F("w"), f.F("l"), top - z0, vert, horiz);
        lay.Weights.Add(new Weight(f.S("id"), "superstructure", wF, f.F("x"), ZRel.Deck((z0 + top) / 2)));
        lay.Weights.Add(new Weight($"Uptakes {f.S("id")}", "machinery", wU, (f.F("x") + servedX) / 2, ZRel.Deck(z0 - vert / 2)));
        var fp = lay.Geo.FunnelPlan;
        if (Py.Truthy(plant["armoured"]) && fp != null && fp.Count > 0)
            lay.Weights.Add(new Weight($"Gratings {f.S("id")}", "armour",
                0.6 * fp.F("area") / Math.Max(1L, fp.L("counts").Sum(c => Py.ToLong(c))), servedX, ZRel.Deck(plant.F("top") - depth)));
        f["top"] = top;
        lay.FunnelsPlanned.Add(f);
    }

    /// <summary>The funnel top raised to FUNNEL_ABOVE over the highest block any funnel passes through.</summary>
    public static double RaiseFunnels(Layout lay, List<PyDict> funnels, List<PyDict> blocks, double top)
    {
        double nw = top;
        foreach (var f in funnels)
        {
            var pts = Footprint.Rect(f.F("x") - f.F("l") / 2, f.F("y") - f.F("w") / 2, f.F("x") + f.F("l") / 2, f.F("y") + f.F("w") / 2).Points();
            foreach (var b in blocks)
                if (b.B("points") && Geometry.PolygonsIntersect(Geometry.Pts(b["points"]), pts))
                    nw = Math.Max(nw, BlockTop(b) + FUNNEL_ABOVE);
        }
        if (nw <= top + 1e-6)
            return top;
        var byId = new Dictionary<string, PyDict>(StringComparer.Ordinal);
        foreach (var f in funnels)
            byId[f.S("id")] = f;
        foreach (var f in funnels)
            f["top"] = nw;
        lay.Footprints = lay.Footprints.Select(o => o with { Top = byId.ContainsKey(o.Owner) ? nw : o.Top }).ToList();
        foreach (var wt in lay.Weights)
        {
            if (byId.TryGetValue(wt.Name, out var f) && wt.Group == "superstructure")
            {
                double z0 = f.F("z0", 0.0);
                wt.W = Powerplant.FunnelWeight(f.F("w"), f.F("l"), nw - z0, 0.0, 0.0).Funnel;
                wt.ZRel = ZRel.Deck((z0 + nw) / 2);
            }
        }
        return nw;
    }

    // Warship and carrier planform
    static readonly Dictionary<string, double> LARGE = new() { ["flare"] = 0.08, ["bow_share"] = 0.5, ["transom"] = 0.1 };
    static readonly (string K, double V)[] SMALL = [("flare", 0.22), ("mid", 0.25), ("bow_share", 0.53), ("transom", 0.75)];
    const double MIDBODY_K = 1.5;
    static readonly (double Lo, double Hi) PLAN_SIZE = (1500.0, 4000.0);

    /// <summary>Bow and stern tapers for a deck of size_m2 (L x B) whose plan fills cwp(cb) + flare of its box.</summary>
    public static PyDict Planform(double cb, double sizeM2)
    {
        double s = Math.Min(1.0, Math.Max(0.0, (sizeM2 - PLAN_SIZE.Lo) / (PLAN_SIZE.Hi - PLAN_SIZE.Lo)));
        var large = new Dictionary<string, double>(LARGE) { ["mid"] = Math.Min(0.4, Math.Max(0.0, (cb - 0.5) * MIDBODY_K)) };
        var k = SMALL.ToDictionary(kv => kv.K, kv => kv.V + (large[kv.K] - kv.V) * s);
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
        return PyDict.Of(("bow", PyDict.Of(("taper", bt), ("power", pw))),
            ("stern", PyDict.Of(("taper", st), ("power", pw), ("transom", transom), ("shape", "round"))));
    }

    public static PyDict HullSpec(PyDict design)
    {
        var h = design.D("hull");
        var pf = Planform(h.F("block_coefficient"), h.F("length") * h.F("beam"));
        return PyDict.Of(("length", h["length"]), ("beam", h["beam"]), ("bow", pf["bow"]), ("stern", pf["stern"]));
    }

    /// <summary>The citadel: the stretch the vital spaces, belt and citadel armour decks cover.</summary>
    public static void SetCitadel(Layout lay, double x0, double x1) => lay.Geo.Citadel = (x0, x1);

    /// <summary>The steering gear over the rudders, for every style. Returns the room.</summary>
    public static PyDict AddSteering(Layout lay, double? x0 = null, double? x1 = null, double? halfWidth = null,
        string name = "Steering gear")
    {
        double L = lay.Hull.L, B = lay.Hull.B;
        double a = x0 ?? -L / 2 + Propulsion.STEERING.X0 * L;
        double b = x1 ?? -L / 2 + Propulsion.STEERING.X1 * L;
        var (bse, top) = Ordnance.Span(lay.Geo.Plant!);
        var room = PyDict.Of(("id", name), ("kind", "steering"), ("x0", a), ("x1", b), ("base", bse), ("top", top),
            ("half_width", halfWidth ?? Propulsion.STEERING.Hw * B));
        lay.Compartments.Add(room);
        lay.Geo.Steering = (a, b);
        lay.Geo.SteeringBeam = 2 * Enumerable.Range(0, 8).Select(k => lay.Hull.HalfWidth(a + (b - a) * (k + 0.5) / 8)).Sum() / 8;
        return room;
    }

    static readonly Dictionary<string, int> DRAW_KIND = new() { ["main"] = 2, ["secondary"] = 1 };

    /// <summary>Each mount's z, the draw order: its rank by base height (equal keys share a z).</summary>
    static void DrawOrder(List<PyDict> mounts)
    {
        (double, int) Key(PyDict m) => (Math.Round(m.F("base"), 3), DRAW_KIND.TryGetValue(m.S("kind"), out var k) ? k : 0);
        var keys = mounts.Select(Key).Distinct().ToList();
        keys.Sort();
        var rank = keys.Select((k, i) => (k, i)).ToDictionary(t => t.k, t => (long)t.i);
        foreach (var m in mounts)
            m["z"] = rank[Key(m)];
    }

    /// <summary>The laid-out ship's renderer spec (lay.spec) and its parts on lay, for every style.</summary>
    public static Layout FinishLayout(Layout lay, PyDict design, PyDict hs, List<PyDict> mounts, PyDict turretTypes,
        List<PyDict> blocks, List<PyDict> funnels, List<PyDict> masts, List<PyDict> aaOut, double funTop, object? deck = null,
        params (string Key, object? Value)[] extra)
    {
        DrawOrder(mounts);
        var spec = PyDict.Of(("id", design["id"]), ("name", design.Get("name", design["id"])), ("class", design.Get("type", "")),
            ("length", lay.Hull.L), ("beam", lay.Hull.B), ("bow", hs["bow"]), ("stern", hs["stern"]), ("deck", deck ?? "steel"),
            ("turret_types", turretTypes),
            ("turrets", mounts.Select(m => (object?)PyDict.Of(("id", m["id"]), ("type", m["type"]), ("x", m["x"]), ("y", m["y"]),
                ("z", m["z"]), ("rest", m["rest"]))).ToList()),
            ("superstructure", blocks.Select(b => (object?)b.Where(k => !(k is "id" or "kind" or "role" or "office") && !k.StartsWith('_'))).ToList()),
            ("funnels", funnels.Select(f => (object?)f.Where(k => !(k is "id" or "seg" or "serves"))).ToList()),
            ("masts", masts.Cast<object?>().ToList()),
            ("aa", aaOut.Select(a => (object?)a.Where(k => !(k is "id" or "base"))).ToList()));
        if (lay.Raised.Count > 0)
            spec["raised_decks"] = lay.Raised.Select(s => (object?)PyDict.Of(("x0", s["x0"]), ("x1", s["x1"]), ("levels", s["levels"]))).ToList();
        foreach (var (k, v) in extra)
            spec[k] = v;
        lay.Spec = spec;
        lay.Mounts = mounts;
        lay.Blocks = blocks;
        lay.Funnels = funnels;
        lay.Aa = aaOut;
        lay.FunTop = funTop;
        FireControl.SearchRadar(lay, design, blocks, masts, funTop);
        lay.Geo.Windage = LateralProfile(lay, blocks, funnels, masts, mounts, aaOut, funTop);
        return lay;
    }

    const double WIND_COL = 1.0;

    /// <summary>What the wind sees from abeam above the main deck: dict(area_m2, z_m).</summary>
    static PyDict LateralProfile(Layout lay, List<PyDict> blocks, List<PyDict> funnels, List<PyDict> masts, List<PyDict> mounts,
        List<PyDict> aa, double funTop)
    {
        var cols = new OrderedDictionary<long, List<(double Z0, double Z1, double F)>>();

        void Add(double x0, double x1, double z0, double z1)
        {
            if (z1 <= z0 || x1 <= x0)
                return;
            for (long i = (long)(Math.Floor(x0 / WIND_COL)); i < (long)(Math.Ceiling(x1 / WIND_COL)); i++)
            {
                double f = (Math.Min(x1, (i + 1) * WIND_COL) - Math.Max(x0, i * WIND_COL)) / WIND_COL;
                if (!cols.TryGetValue(i, out var l))
                    cols[i] = l = [];
                l.Add((z0, z1, f));
            }
        }

        foreach (var b in blocks)
            Add(b.F("x0"), b.F("x1"), BlockBase(b), BlockTop(b));
        foreach (var dk in lay.Decks)
        {
            var xs = Geometry.Pts(dk["points"]).Select(p => p.X).ToList();
            Add(xs.Min(), xs.Max(), 0.0, dk.F("top"));
        }
        foreach (var f in funnels)
            Add(f.F("x") - f.F("l") / 2, f.F("x") + f.F("l") / 2, f.F("z0", 0.0), funTop);
        foreach (var m in masts)
        {
            double w = m.B("tripod") ? 1.5 : 0.7;
            Add(m.F("x") - w / 2, m.F("x") + w / 2, 0.0, m.F("top", funTop + 6.0));
        }
        foreach (var m in mounts)
        {
            if (m.B("casemate") || m.F("top") <= 0)
                continue;
            double r = m.D("t").F("r");
            Add(m.F("x") - r, m.F("x") + r, 0.0, m.F("top"));
        }
        foreach (var a in aa)
        {
            double r = Geometry.AA_CFG[a.S("type")].R;
            Add(a.F("x") - r, a.F("x") + r, a.F("base"), a.F("base") + 2.0);
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
                        area += (z1 - c.B) * f * WIND_COL;
                        mom += (z1 - c.B) * f * WIND_COL * (c.B + z1) / 2;
                        cur = (c.A, z1);
                    }
                    continue;
                }
                cur = (z0, z1);
                area += (z1 - z0) * f * WIND_COL;
                mom += (z1 - z0) * f * WIND_COL * (z0 + z1) / 2;
            }
        }
        return PyDict.Of(("area_m2", area), ("z_m", area != 0 ? mom / area : 0.0));
    }

    /// <summary>A, B, C, then A4, A5, ... (letters[0] plus the turret's number in its group).</summary>
    public static string TurretName(string letters, int i) => i < letters.Length ? letters[i].ToString() : $"{letters[0]}{i + 1}";

    /// <summary>How many turrets of the fore and aft groups step up (superfire).</summary>
    public static (double Fore, double Aft) SteppedCounts(PyDict main)
    {
        var sf = main.Get("superfire", true);
        double nf = main.F("fore", 0), na = main.F("aft", 0);
        if (sf is true)
            return (nf, na);
        if (sf is false)
            return (Math.Min(nf, 1), Math.Min(na, 1));
        var d = (PyDict)sf!;
        return (d.F("fore", nf), d.F("aft", na));
    }

    /// <summary>What a battery stands on: "deck" (the default) or "deckhouse".</summary>
    public static object? StandsOn(PyDict spec, string key = "stands_on") => spec.Get(key, "deck");

    /// <summary>superstructure.deckhouse_levels (1 when the design gives none).</summary>
    static long DeckhouseLevels(PyDict design) => (long)(design.DOr("superstructure").F("deckhouse_levels", 1));

    /// <summary>superstructure.tower_levels: the bridge tower's top level; default when the design gives none.</summary>
    public static long TowerLevels(PyDict design, long def) => (long)(design.DOr("superstructure").F("tower_levels", def));

    /// <summary>superstructure.aft_control (on by default).</summary>
    static bool AftControl(PyDict design) => Py.Truthy(design.DOr("superstructure").Get("aft_control", true));

    /// <summary>superstructure.levels_over_bridge (1 by default).</summary>
    static long LevelsOverBridge(PyDict design) => (long)(design.DOr("superstructure").F("levels_over_bridge", 1));
}
