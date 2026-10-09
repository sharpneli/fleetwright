namespace Fleetwright.Shipgen;

/// <summary>subdivision: the hull below the main deck as a grid of watertight cells, and the rooms that own them.</summary>
public static class Subdivision
{
    const double MIN_SECTION = 0.03, MIN_SECTION_M = 1.0, MIN_SECTION_MAX_M = 8.0, MAX_SECTION = 0.07, MAX_SECTION_M = 2.5;
    const double COLLISION = 0.05, STEEL_FRAME = 0.92;
    const int Z_SAMPLES = 6;
    const double SLIVER_M3 = 1.0, SLIVER_FRAC = 0.05;
    static readonly Dictionary<string, int> ROOM_PRIORITY = new()
    {
        ["magazine"] = 9, ["steering"] = 6, ["boiler_room"] = 7, ["engine_room"] = 7, ["fuel_tank"] = 7, ["bunker"] = 6,
        ["cargo_tank"] = 5, ["hold"] = 5, ["accommodation"] = 2,
    };
    static readonly Dictionary<string, double> PERMEABILITY = new()
    {
        ["magazine"] = 0.6, ["steering"] = 0.85, ["boiler_room"] = 0.85, ["engine_room"] = 0.85, ["fuel_tank"] = 0.95,
        ["bunker"] = 0.95, ["cargo_tank"] = 0.95, ["hold"] = 0.6, ["accommodation"] = 0.95, ["stores"] = 0.6,
        ["double_bottom"] = 0.95, ["tds"] = 0.95,
    };
    const double COAL_PERMEABILITY = 0.4;

    static double Ov(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

    static readonly (string K0, string K1)[] Axes = [("x0", "x1"), ("y0", "y1"), ("base", "top")];

    /// <summary>Does the room's box take the cell: on every axis an overlap of at least half the shorter of the two?</summary>
    static bool Claims(PyDict room, PyDict cell)
    {
        foreach (var (k0, k1) in Axes)
        {
            double o = Ov(room.F(k0), room.F(k1), cell.F(k0), cell.F(k1));
            if (o <= 1e-6 || o < 0.5 * Math.Min(room.F(k1) - room.F(k0), cell.F(k1) - cell.F(k0)) - 1e-6)
                return false;
        }
        return true;
    }

    static double BoxOverlap(PyDict room, PyDict cell)
    {
        double v = 1.0;
        foreach (var (k0, k1) in Axes)
            v *= Math.Max(0.0, Ov(room.F(k0), room.F(k1), cell.F(k0), cell.F(k1)));
        return v;
    }

    /// <summary>Where the hull reaches raised deck k: merged [[x0, x1]], aft to forward.</summary>
    static List<double[]> RaisedSpans(IReadOnlyList<PyDict> raised, long k)
    {
        var out_ = new List<double[]>();
        var spans = raised.Where(s => s.I("levels") >= k).Select(s => (s.F("x0"), s.F("x1"))).ToList();
        spans.Sort();
        foreach (var (x0, x1) in spans)
        {
            if (out_.Count > 0 && x0 <= out_[^1][1] + 1e-6)
                out_[^1][1] = Math.Max(out_[^1][1], x1);
            else
                out_.Add([x0, x1]);
        }
        return out_;
    }

    static object? R(object? v, int n) => v is double d ? Math.Round(d, n) : v;

    /// <summary>The decks, keel up, as heights above the main deck.</summary>
    static List<PyDict> DecksOf(PyDict design, double D, PyDict ag, IReadOnlyList<PyDict> raised)
    {
        var out_ = new List<PyDict> { PyDict.Of(("id", "Keel"), ("kind", "keel"), ("z", -D)) };
        if (!Py.Eq(design.Get("style"), "planing"))
            out_.Add(PyDict.Of(("id", "Inner bottom"), ("kind", "inner_bottom"), ("z", -D + Powerplant.DoubleBottom(D))));
        var arm = new OrderedDictionary<long, List<PyDict>>();
        foreach (PyDict d in ag.L("decks").Cast<PyDict>())
        {
            var p = PyDict.Of(("armour_mm", d["mm"]), ("x0", d["x0"]), ("x1", d["x1"]));
            if (Py.Truthy(d["material"]))
                p["material"] = d["material"];
            long n = d.I("deck");
            if (!arm.TryGetValue(n, out var l))
                arm[n] = l = [];
            l.Add(p);
        }
        var armD = new Dictionary<long, PyDict>();
        foreach (var (n, ps) in arm)
            armD[n] = ps.Count == 1 ? ps[0] : PyDict.Of(("plates", ps.Select(p =>
            {
                var o = new PyDict();
                foreach (var kv in p)
                    o[kv.Key] = R(kv.Value, 3);
                return (object?)o;
            }).ToList()));
        foreach (var (n, z) in Enumerable.Reverse(Decks.DeckStack(design, D)))
        {
            var d = PyDict.Of(("id", Decks.DeckName(n)), ("kind", n == 0 ? "main" : "deck"), ("deck", n), ("z", z - D));
            if (armD.TryGetValue(n, out var a))
                d.Update(a);
            out_.Add(d);
        }
        long top = raised.Select(s => s.I("levels")).DefaultIfEmpty(0L).Max();
        for (long k = 1; k <= top; k++)
        {
            var d = PyDict.Of(("id", Decks.DeckName(-k)), ("kind", "raised"), ("deck", -k), ("z", k * Geometry.DECK_PITCH),
                ("spans", RaisedSpans(raised, k).Select(s => (object?)new List<object?> { Math.Round(s[0], 3), Math.Round(s[1], 3) }).ToList()));
            if (armD.TryGetValue(-k, out var a))
                d.Update(a);
            out_.Add(d);
        }
        return out_;
    }

    /// <summary>A tier is named after the deck it stands on: bottom, hold, then second, third, ...</summary>
    static string TierName(PyDict floor)
    {
        if (Py.In(floor["kind"], "keel", "inner_bottom"))
            return Py.Eq(floor["kind"], "keel") ? "bottom" : "hold";
        string id = floor.S("id").ToLowerInvariant();
        return id.EndsWith(" deck", StringComparison.Ordinal) ? id[..^5] : id;
    }

    const int BREAK_PRIORITY = 8;

    /// <summary>Transverse bulkhead positions, bow to stern: [(x, kind)], with the hull's ends.</summary>
    static List<(double X, string Kind)> Stations(double L, List<PyDict> rooms, (double X0, double X1, string Kind)? cit, double minGap,
        double maxGap, IEnumerable<double> breaks, IEnumerable<double> armoured)
    {
        var cands = new List<(double X, int P, string Kind)> { (L / 2 - COLLISION * L, 9, "collision") };
        cands.AddRange(breaks.Select(x => (x, BREAK_PRIORITY, "main")));
        cands.AddRange(armoured.Select(x => (x, 10, "armoured")));
        if (cit is { } c)
        {
            cands.Add((c.X0, 10, c.Kind));
            cands.Add((c.X1, 10, c.Kind));
        }
        foreach (var r in rooms)
        {
            int p = ROOM_PRIORITY.TryGetValue(r.S("kind"), out var pp) ? pp : 4;
            cands.Add((r.F("x0"), p, "main"));
            cands.Add((r.F("x1"), p, "main"));
        }
        cands = cands.Where(c2 => -L / 2 + minGap <= c2.X && c2.X <= L / 2 - minGap).ToList();
        cands = cands.OrderBy(c2 => -c2.X).ToList();
        var merged = new List<(double X, int P, string Kind)>();
        foreach (var c2 in cands)
        {
            if (merged.Count > 0 && merged[^1].X - c2.X < 0.05)
            {
                var m = merged[^1];
                merged[^1] = (m.X, m.P + c2.P, m.Kind != "main" ? m.Kind : c2.Kind);
            }
            else
                merged.Add(c2);
        }
        var pts = new List<(double X, int P, string Kind)> { (L / 2, 99, "end") };
        pts.AddRange(merged);
        pts.Add((-L / 2, 99, "end"));
        while (pts.Count > 2)
        {
            var (g, i) = Enumerable.Range(0, pts.Count - 1).Select(i => (pts[i].X - pts[i + 1].X, i)).MinBy(t => t);
            if (g >= minGap)
                break;
            var a = pts[i];
            var b = pts[i + 1];
            if (a.Kind == "end" || (b.Kind != "end" && b.P <= a.P))
                pts.RemoveAt(i + 1);
            else
                pts.RemoveAt(i);
        }
        var out_ = new List<(double, string)> { (pts[0].X, "end") };
        for (int k = 0; k < pts.Count - 1; k++)
        {
            double xa = pts[k].X;
            var (xb, _, kb) = pts[k + 1];
            double gap = xa - xb;
            bool inside = rooms.Any(r => r.F("x0") <= xb + 1e-6 && r.F("x1") >= xa - 1e-6);
            if (gap > maxGap && !inside)
            {
                long n = (long)Math.Ceiling(gap / maxGap - 1e-9);
                for (long j = 1; j < n; j++)
                    out_.Add((xa - gap * j / n, "main"));
            }
            out_.Add((xb, kb));
        }
        return out_;
    }

    /// <summary>The subdivision of the laid-out ship: dict(decks, tiers, sections, bulkheads, cells, rooms).</summary>
    public static PyDict Build(Layout lay, PyDict design, Navarch.Result res, PyDict ag, bool armoured, HullForm form)
    {
        var hull = lay.Hull;
        double L = hull.L, B = hull.B;
        double D = res.Depth, T = res.Draught;
        double cb = design.D("hull").F("block_coefficient");
        var plan = lay.Geo.Plant ?? new PyDict();
        var mach = lay.Geo.Machinery;
        double wl = -(D - T);
        double Rz(double z) => z - D;

        var dks = DecksOf(design, D, ag, lay.Raised);
        bool hasBottom = Py.Eq(dks[1]["kind"], "inner_bottom");
        var tiers = new List<PyDict>();
        for (int i = 0; i < dks.Count - 1; i++)
        {
            var lo = dks[i];
            var hi = dks[i + 1];
            string name = Py.Eq(lo["kind"], "keel") && !hasBottom ? "hold" : TierName(lo);
            double sub_ = Math.Min(1.0, Math.Max(0.0, (wl - lo.F("z")) / (hi.F("z") - lo.F("z"))));
            var t = PyDict.Of(("id", name), ("base", lo["z"]), ("top", hi["z"]), ("below_waterline", sub_ >= 1.0 - 1e-6),
                ("submerged", sub_), ("floor", lo["id"]), ("ceiling", hi["id"]));
            if (hi.Has("spans"))
                t["spans"] = hi["spans"];
            tiers.Add(t);
        }
        double ib = hasBottom ? dks[1].F("z") : -D;
        double under = ag["roof_z"] is not null ? ag.F("roof_z") - D : 0.0;
        var adecks = ag.L("decks").Cast<PyDict>().Select(d =>
        {
            var c = d.Copy();
            c["z"] = Rz(d.F("z"));
            return c;
        }).ToList();

        var rooms = new List<PyDict>();
        foreach (var c in lay.Compartments)
        {
            if (Py.In(c["kind"], "citadel", "hangar"))
                continue;
            double y = c.F("y", 0.0), hw = c.F("half_width");
            rooms.Add(PyDict.Of(("src", c), ("id", c["id"]), ("kind", c["kind"]), ("x0", c["x0"]), ("x1", c["x1"]), ("y0", y - hw),
                ("y1", y + hw), ("base", c.Get("base", ib)), ("top", c.Get("top", under))));
        }

        (double X0, double X1, string Kind)? cit = null;
        if (armoured)
            cit = (ag.F("x0"), ag.F("x1"), ag.F("bulkhead_mm") > 0 ? "armoured" : "citadel");
        else if (lay.Geo.Citadel is { } gc && Py.In(design.Get("style", "warship"), "warship", "carrier"))
            cit = (gc.X0, gc.X1, "citadel");
        var st = Stations(L, rooms, cit, Math.Min(MIN_SECTION_MAX_M, Math.Max(MIN_SECTION_M, MIN_SECTION * L)), Math.Max(MAX_SECTION_M, MAX_SECTION * L),
            lay.Raised.SelectMany(s => new[] { s.F("x0"), s.F("x1") }), ag.L("end_bulkheads").Cast<PyDict>().Select(b => b.F("x")));
        var armBh = new List<PyDict>();
        if (cit is { Kind: "armoured" })
            foreach (var x in new[] { ag.F("x0"), ag.F("x1") })
                armBh.Add(PyDict.Of(("x", x), ("mm", ag["bulkhead_mm"]), ("bottom", ag["bulkhead_bottom"]), ("top", ag["bulkhead_top"]),
                    ("material", ag["bulkhead_material"])));
        armBh.AddRange(ag.L("end_bulkheads").Cast<PyDict>());
        var sections = new List<PyDict>();
        for (int i = 0; i < st.Count - 1; i++)
            sections.Add(PyDict.Of(("id", (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)), ("x0", st[i + 1].X), ("x1", st[i].X)));
        int nbh = st.Count - 2;
        var tb = new List<PyDict>();
        long SecLevel(PyDict sec) => lay.DeckLevel((sec.F("x0") + sec.F("x1")) / 2);
        for (int k = 0; k < st.Count - 2; k++)
        {
            var s = st[k + 1];
            double top = Math.Min(SecLevel(sections[k]), SecLevel(sections[k + 1])) * Geometry.DECK_PITCH;
            var d = PyDict.Of(("id", $"Bulkhead {k + 1}"), ("kind", s.Kind is "collision" or "armoured" ? s.Kind : "main"),
                ("x", Math.Round(s.X, 3)), ("base", Math.Round(-D, 2)), ("top", top != 0 ? Math.Round(top, 2) : 0.0));
            if (s.Kind == "armoured")
            {
                var a = armBh.MinBy(b => Math.Abs(b.F("x") - s.X))!;
                d.Update(("armour_mm", Py.RoundObj(a["mm"])), ("armour_bottom", Math.Round(Rz(a.F("bottom")), 2)),
                    ("armour_top", Math.Round(Rz(a.F("top")), 2)));
                if (Py.Truthy(a["material"]))
                    d["armour_material"] = a["material"];
            }
            tb.Add(d);
        }
        if (tb.Count != nbh)
            throw new InvalidOperationException("bulkhead count");

        double tds = plan.F("tds", 0.0);
        double wingM = plan.F("wing_m", 0.0);
        bool centreline = design.DOr("machinery").B("centreline_bulkhead");

        List<double> XsIn(double x0, double x1, int n = 8) => Enumerable.Range(0, n).Select(j => x0 + (x1 - x0) * (j + 0.5) / n).ToList();
        List<double> HwSamples(double x0, double x1, int n = 8) => XsIn(x0, x1, n).Select(hull.HalfWidth).ToList();
        double Widest(double x0, double x1, double z) => (new[] { x0, x1 }.Concat(XsIn(x0, x1)).Select(x => form.HalfWidth(x, z + D))).Max();

        var cells = new List<PyDict>();
        var longi = new List<PyDict>();
        var loBase = new Dictionary<string, double>(StringComparer.Ordinal);
        var belts = new List<PyDict>();
        if (ag.F("belt_mm") > 0)
            belts.Add(PyDict.Of(("x0", ag["x0"]), ("x1", ag["x1"]), ("bottom", ag["belt_bottom"]), ("top", ag["belt_top"]),
                ("mm", ag["belt_mm"]), ("tip_mm", ag["belt_mm"]), ("bottom_mm", ag["belt_bottom_mm"]), ("wl", Rz(ag.F("waterline"))),
                ("extent", "citadel"), ("material", ag["belt_material"])));
        belts.AddRange(ag.L("strakes").Cast<PyDict>());
        belts = belts.Select(b =>
        {
            var c = b.Copy();
            c["bottom"] = Rz(b.F("bottom"));
            c["top"] = Rz(b.F("top"));
            return c;
        }).ToList();
        for (int si = 0; si < sections.Count; si++)
        {
            var sec = sections[si];
            double sx0 = sec.F("x0"), sx1 = sec.F("x1");
            double xm = (sx0 + sx1) / 2;
            var hwsSec = HwSamples(sx0, sx1);
            double hwmax = (new[] { hull.HalfWidth(sx0), hull.HalfWidth(sx1) }.Concat(hwsSec)).Max();
            bool inMach = mach is { } mc && mc.X0 - 1e-6 <= xm && xm <= mc.X1 + 1e-6;
            bool inCit = cit is { } cc && cc.X0 - 1e-6 <= xm && xm <= cc.X1 + 1e-6;
            double? split = null;
            string? sKind = null;
            double sTop = 0;
            if (wingM > 0 && inMach)
                (split, sKind, sTop) = (plan.F("width") / 2, "wing", 0.0);
            else if (tds > 0 && inCit)
            {
                split = Math.Max(0.5, STEEL_FRAME * XsIn(sx0, sx1).Select(form.Waterline).Min() - tds);
                (sKind, sTop) = ("tds", under);
            }
            if (split is double sp && sp >= hwmax - 0.3)
                split = null;
            foreach (var (side, sgn) in new[] { ("S", 1), ("P", -1) })
                if (split is double sp2)
                    longi.Add(PyDict.Of(("id", $"{(sKind == "wing" ? "Wing" : "Torpedo")} bulkhead {sec.S("id")} {side}"), ("kind", sKind),
                        ("section", sec["id"]), ("side", side), ("y", Math.Round(sgn * sp2, 3)), ("x0", Math.Round(sx0, 3)),
                        ("x1", Math.Round(sx1, 3)), ("base", Math.Round(ib, 2)), ("top", Math.Round(sTop, 2))));
            bool cl = centreline && inMach;
            if (cl)
                longi.Add(PyDict.Of(("id", $"Centreline bulkhead {sec.S("id")}"), ("kind", "centreline"), ("section", sec["id"]),
                    ("side", "C"), ("y", 0.0), ("x0", Math.Round(sx0, 3)), ("x1", Math.Round(sx1, 3)), ("base", Math.Round(ib, 2)),
                    ("top", Math.Round(under, 2))));
            for (int ti = 0; ti < tiers.Count; ti++)
            {
                var tr = tiers[ti];
                double x0 = sx0, x1 = sx1;
                if (tr.Has("spans"))
                {
                    var best = (tr.L("spans").Cast<List<object?>>().Select(s => (A: Math.Max(x0, Py.ToDouble(s[0])), B: Math.Min(x1, Py.ToDouble(s[1]))))).MaxBy(v => v.B - v.A);
                    if (best.B - best.A < 1e-3)
                        continue;
                    (x0, x1) = (best.A, best.B);
                }
                bool bottom = hasBottom && ti == 0;
                double trTop = tr.F("top"), trBase = tr.F("base");
                bool centreSplit = cl && !bottom && trTop <= under + 1e-6;
                double hwT = Math.Min(hwmax, Widest(x0, x1, trTop));
                bool banded = split is not null && !bottom && trTop <= sTop + 1e-6 && split.Value < hwT - 0.3;
                if (banded)
                    loBase[sec.S("id")] = Math.Min(loBase.TryGetValue(sec.S("id"), out var lb) ? lb : trBase, trBase);
                List<(string Band, double Y0, double Y1)> ys;
                if (banded)
                {
                    double s2 = split!.Value;
                    ys = [("P", -hwT, -s2)];
                    if (centreSplit)
                        ys.AddRange([("CP", -s2, 0.0), ("CS", 0.0, s2)]);
                    else
                        ys.Add(("C", -s2, s2));
                    ys.Add(("S", s2, hwT));
                }
                else
                    ys = centreSplit ? [("CP", -hwT, 0.0), ("CS", 0.0, hwT)] : [("C", -hwT, hwT)];
                var zs = Enumerable.Range(0, Z_SAMPLES).Select(k => trBase + (trTop - trBase) * (k + 0.5) / Z_SAMPLES).ToList();
                var xsI = XsIn(x0, x1);
                var hwz = zs.Select(z => xsI.Select(x => form.HalfWidth(x, z + D)).ToList()).ToList();
                double dz = (trTop - trBase) / Z_SAMPLES;
                foreach (var (band, y0, y1) in ys)
                {
                    if (y1 - y0 < 1e-6)
                        continue;
                    double vUnder = 0.0, vOver = 0.0;
                    for (int zi = 0; zi < zs.Count; zi++)
                    {
                        var hs = hwz[zi];
                        double v = hs.Select(h => Math.Max(0.0, Math.Min(h, y1) - Math.Max(-h, y0))).Sum() * (x1 - x0) / hs.Count * dz;
                        if (zs[zi] < wl)
                            vUnder += v;
                        else
                            vOver += v;
                    }
                    if (vUnder + vOver < 0.01 && hwT < 0.05)
                        continue;
                    var c = PyDict.Of(("id", $"{sec.S("id")} {tr.S("id")} {band}"), ("section", sec["id"]), ("tier", tr["id"]), ("band", band),
                        ("x0", x0), ("x1", x1), ("y0", y0), ("y1", y1), ("base", tr["base"]), ("top", tr["top"]), ("v_under", vUnder),
                        ("v_over", vOver), ("below_waterline", tr["below_waterline"]), ("si", (long)si), ("ti", (long)ti), ("t0", (long)ti));
                    if (inCit && armoured)
                        c["citadel"] = true;
                    var above = adecks.Where(d => trTop <= d.F("z") + 1e-6 && d.F("x0") <= xm && xm <= d.F("x1")).ToList();
                    if (above.Count > 0)
                    {
                        c["armour_above_mm"] = above.Select(d => d["mm"]).ToList();
                        if (above.Any(d => Py.Truthy(d["material"])))
                            c["armour_above_material"] = above.Select(d => d["material"]).ToList();
                    }
                    bool outer = band is "P" or "S" or "C" || (band is "CP" or "CS" && !banded);
                    var side = belts.Where(b => outer && b.F("x0") <= xm && xm <= b.F("x1") && Ov(b.F("bottom"), b.F("top"), trBase, trTop) > 0)
                        .Select(b => (Mm: Armour.BeltMmAt(b, xm, Math.Min(b.F("top"), trTop)), B: b)).ToList();
                    if (side.Count > 0)
                    {
                        var (mm, b) = side.MaxBy(v => v.Mm);
                        c["belt_mm"] = (long)Math.Round(mm);
                        if (b.B("material"))
                            c["belt_material"] = b["material"];
                    }
                    if (banded && band is "P" or "S" && inCit && tds > 0)
                    {
                        double zm = (trBase + trTop) / 2 + D;
                        c["tds_m"] = Math.Round(Math.Max(0.0, STEEL_FRAME * xsI.Select(x => form.HalfWidth(x, zm)).Min() - split!.Value), 2);
                    }
                    cells.Add(c);
                }
            }
        }
        double vU = cells.Select(c => c.F("v_under")).Sum();
        double underK = vU > 0 ? cb * L * B * T / vU : 1.0;
        foreach (var c in cells)
        {
            double vu = c.F("v_under"), vo = c.F("v_over");
            c.Remove("v_under");
            c.Remove("v_over");
            c["volume_m3"] = vu * underK + vo;
        }
        foreach (var l_ in longi)
            if (loBase.TryGetValue(l_.S("section"), out var lb) && !Py.Eq(l_["kind"], "centreline"))
                l_["base"] = Math.Round(lb, 2);
        longi = longi.Where(l_ => Py.Eq(l_["kind"], "centreline") || loBase.ContainsKey(l_.S("section"))).ToList();

        // ---------------- slivers join the cell above (or below) ----------------
        bool Flat(long t) => !tiers[(int)t].Has("spans");
        foreach (var c in cells.OrderBy(c => c.I("ti")).ToList())
        {
            double box = (c.F("x1") - c.F("x0")) * (c.F("y1") - c.F("y0")) * (c.F("top") - c.F("base"));
            if (c.B("merged") || c.F("volume_m3") >= Math.Max(SLIVER_M3, SLIVER_FRAC * box) || !Flat(c.I("ti")))
                continue;
            foreach (int dt in new[] { 1, -1 })
            {
                var into = cells.Where(d => d.I("si") == c.I("si") && !d.B("merged") && Flat(d.I("ti")) &&
                                            (dt > 0 ? d.I("t0") == c.I("ti") + 1 : d.I("ti") == c.I("t0") - 1) &&
                                            d.F("y0") <= c.F("y0") + 1e-6 && d.F("y1") >= c.F("y1") - 1e-6).ToList();
                if (into.Count > 0)
                {
                    var d = into[0];
                    d["volume_m3"] = d.F("volume_m3") + c.F("volume_m3");
                    if (dt > 0)
                        d.Update(("base", c["base"]), ("t0", c["t0"]));
                    else
                        d.Update(("top", c["top"]), ("ti", c["ti"]));
                    c["merged"] = true;
                    break;
                }
            }
        }
        cells = cells.Where(c => !c.B("merged")).ToList();

        // ---------------- rooms take cells ----------------
        var owner = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in cells)
        {
            ((int, double) Key, PyDict R)? best = null;
            foreach (var r in rooms)
            {
                if (Claims(r, c))
                {
                    var key = (ROOM_PRIORITY.TryGetValue(r.S("kind"), out var p) ? p : 4, BoxOverlap(r, c));
                    if (best is null || key.CompareTo(best.Value.Key) > 0)
                        best = (key, r);
                }
            }
            if (best is { } bb)
                owner[c.S("id")] = bb.R.S("id");
        }
        var byId = new Dictionary<string, PyDict>(StringComparer.Ordinal);
        foreach (var c in cells)
            byId[c.S("id")] = c;
        var roomOut = new OrderedDictionary<string, PyDict>(StringComparer.Ordinal);
        foreach (var r in rooms)
            roomOut[r.S("id")] = PyDict.Of(("id", r["id"]), ("kind", r["kind"]), ("cells", new List<object?>()));
        foreach (var r in rooms)
            foreach (var kv in r.D("src"))
                if (!(kv.Key is "id" or "kind" or "x0" or "x1" or "y" or "half_width" or "base" or "top"))
                    roomOut[r.S("id")][kv.Key] = kv.Value;
        foreach (var (cid, rid) in owner)
            roomOut[rid].L("cells").Add(cid);
        foreach (var r in rooms)
        {
            var fmt = r.D("src").Get("per_section") as string;
            if (!roomOut.TryGetValue(r.S("id"), out var out_))
                continue;
            var secs = (new HashSet<long>(out_.L("cells").Cast<string>().Select(cid => byId[cid].I("si")))).OrderBy(s => s).ToList();
            if (string.IsNullOrEmpty(fmt) || secs.Count < 2)
            {
                out_.Pop("per_section", null);
                continue;
            }
            roomOut.Remove(r.S("id"));
            double vol = (out_.L("cells").Cast<string>().Select(cid => byId[cid].F("volume_m3"))).Sum();
            if (vol == 0)
                vol = 1.0;
            foreach (var si in secs)
            {
                var own = out_.L("cells").Cast<string>().Where(cid => byId[cid].I("si") == si).ToList();
                var part = out_.Where(k => k != "per_section");
                part.Update(("id", fmt.Replace("{}", sections[(int)si].S("id"))), ("cells", own.Cast<object?>().ToList()));
                if (out_.Has("tonnes"))
                    part["tonnes"] = Math.Round(out_.F("tonnes") * own.Select(cid => byId[cid].F("volume_m3")).Sum() / vol, 1);
                roomOut[part.S("id")] = part;
                foreach (var cid in own)
                    owner[cid] = part.S("id");
            }
        }
        var also = new OrderedDictionary<string, List<object?>>(StringComparer.Ordinal);
        foreach (var r in rooms)
        {
            if (!roomOut.TryGetValue(r.S("id"), out var ro) || ro.L("cells").Count > 0)
                continue;
            var rc = ((r.F("x0") + r.F("x1")) / 2, (r.F("y0") + r.F("y1")) / 2, (r.F("base") + r.F("top")) / 2);
            var cand = cells.OrderBy(c => (-BoxOverlap(r, c), Math.Sqrt((rc.Item1 - (c.F("x0") + c.F("x1")) / 2) * (rc.Item1 - (c.F("x0") + c.F("x1")) / 2) + (rc.Item2 - (c.F("y0") + c.F("y1")) / 2) * (rc.Item2 - (c.F("y0") + c.F("y1")) / 2) + (rc.Item3 - (c.F("base") + c.F("top")) / 2) * (rc.Item3 - (c.F("base") + c.F("top")) / 2)))).ToList();
            if (cand.Count > 0)
            {
                string cid = cand[0].S("id");
                if (!also.TryGetValue(cid, out var al))
                    also[cid] = al = [];
                al.Add(r["id"]);
                ro.L("cells").Add(cid);
                ro["shared"] = true;
            }
        }

        foreach (var c in cells)
        {
            if (owner.ContainsKey(c.S("id")))
                continue;
            string use, name;
            if (hasBottom && c.I("ti") == 0)
                (use, name) = ("double_bottom", "Double bottom");
            else if (c.Has("tds_m") && c.S("band") is "P" or "S")
                (use, name) = ("tds", "Torpedo protection");
            else if (tiers[(int)c.I("ti")].F("submerged") >= 0.5)
                (use, name) = ("stores", "Stores");
            else
                (use, name) = ("accommodation", "Quarters");
            string rid = $"{name} {c.S("section")}" + (use == "tds" ? $" {c.S("band")}" : "");
            if (!roomOut.TryGetValue(rid, out var ro))
                roomOut[rid] = ro = PyDict.Of(("id", rid), ("kind", use), ("cells", new List<object?>()));
            ro.L("cells").Add(c["id"]);
            owner[c.S("id")] = rid;
        }

        // ---------------- crew over the quarters, by volume ----------------
        var c_ = lay.Crew ?? new PyDict();
        long nCrew = Py.ToLong(c_.Get("complement", 0L)) - Py.ToLong(c_.Get("quartered_in_superstructure", 0L));
        var quarters = cells.Where(c => Py.Eq(roomOut[owner[c.S("id")]]["kind"], "accommodation")).ToList();
        double qv = quarters.Select(c => c.F("volume_m3")).Sum();
        if (nCrew != 0 && qv > 0)
        {
            var shares = quarters.Select(c => nCrew * c.F("volume_m3") / qv).ToList();
            var men = shares.Select(s => (long)s).ToList();
            foreach (var k in Enumerable.Range(0, quarters.Count).OrderBy(k => men[k] - shares[k]).ToList().Take((int)Math.Max(0, nCrew - men.Sum())))
                men[k] += 1;
            for (int i = 0; i < quarters.Count; i++)
                if (men[i] != 0)
                    quarters[i]["crew"] = men[i];
        }
        foreach (var r in roomOut.Values)
        {
            r.Pop("crew", null);
            long crew = r.L("cells").Cast<string>().Where(cid => owner[cid] == r.S("id")).Sum(cid => Py.ToLong(byId[cid].Get("crew", 0L)));
            if (crew != 0)
                r["crew"] = crew;
        }

        // ---------------- per-cell finish: room, permeability, neighbours ----------------
        var fuel = plan.Get("fuel");
        foreach (var c in cells)
        {
            var r = roomOut[owner[c.S("id")]];
            c["room"] = r["id"];
            if (also.TryGetValue(c.S("id"), out var al))
                c["also"] = al;
            double p = PERMEABILITY.TryGetValue(r.S("kind"), out var pp) ? pp : 0.9;
            if (Py.Eq(r["kind"], "bunker") && Py.Eq(r.Get("fuel", fuel), "coal"))
                p = COAL_PERMEABILITY;
            c["permeability"] = p;
        }
        var grid = new Dictionary<long, List<PyDict>>();
        foreach (var c in cells)
        {
            if (!grid.TryGetValue(c.I("si"), out var l))
                grid[c.I("si")] = l = [];
            l.Add(c);
        }
        bool Meets(PyDict c, PyDict d) => d.I("t0") <= c.I("ti") && c.I("t0") <= d.I("ti");
        var lon = new Dictionary<(string, string), string>();
        foreach (var l_ in longi)
            lon[(l_.S("section"), l_.S("side"))] = l_.S("id");
        foreach (var c in cells)
        {
            var nb = new List<(PyDict D, string Via)>();
            foreach (var d in grid[c.I("si")])
            {
                if (ReferenceEquals(d, c) || !Meets(c, d))
                    continue;
                double y;
                if (Math.Abs(d.F("y0") - c.F("y1")) < 1e-6)
                    y = c.F("y1");
                else if (Math.Abs(d.F("y1") - c.F("y0")) < 1e-6)
                    y = c.F("y0");
                else
                    continue;
                nb.Add((d, lon[(c.S("section"), Math.Abs(y) < 1e-6 ? "C" : y > 0 ? "S" : "P")]));
            }
            foreach (var (dt, dk) in new[] { (-1, tiers[(int)c.I("t0")].S("floor")), (1, tiers[(int)c.I("ti")].S("ceiling")) })
                foreach (var d in grid[c.I("si")])
                    if ((dt < 0 ? d.I("ti") == c.I("t0") - 1 : d.I("t0") == c.I("ti") + 1) && Ov(c.F("y0"), c.F("y1"), d.F("y0"), d.F("y1")) > 1e-6)
                        nb.Add((d, dk));
            foreach (int ds in new[] { -1, 1 })
                if (grid.TryGetValue(c.I("si") + ds, out var gl))
                    foreach (var d in gl)
                        if (Meets(c, d) && Ov(c.F("y0"), c.F("y1"), d.F("y0"), d.F("y1")) > 1e-6)
                            nb.Add((d, tb[(int)Math.Min(c.I("si"), d.I("si"))].S("id")));
            c["neighbours"] = nb.Select(t => (object?)new List<object?> { t.D["id"], Py.Eq(t.D["room"], c["room"]) ? "open" : t.Via }).ToList();
        }

        foreach (var r in roomOut.Values)
        {
            var own = r.L("cells").Cast<string>().Select(cid => byId[cid]).ToList();
            r["volume_m3"] = Math.Round(own.Where(c => owner[c.S("id")] == r.S("id")).Select(c => c.F("volume_m3")).Sum(), 1);
            r["x0"] = Math.Round(own.Select(c => c.F("x0")).Min(), 3);
            r["x1"] = Math.Round(own.Select(c => c.F("x1")).Max(), 3);
            r["base"] = Math.Round(own.Select(c => c.F("base")).Min(), 2);
            r["top"] = Math.Round(own.Select(c => c.F("top")).Max(), 2);
        }

        PyDict Rnd(PyDict c)
        {
            var o = new PyDict();
            foreach (var kv in c)
                if (!(kv.Key is "si" or "ti" or "t0"))
                    o[kv.Key] = R(kv.Value, 3);
            o["volume_m3"] = Math.Round(c.F("volume_m3"), 1);
            o["base"] = Math.Round(c.F("base"), 2);
            o["top"] = Math.Round(c.F("top"), 2);
            return o;
        }

        PyDict RoundDict(PyDict d, int n)
        {
            var o = new PyDict();
            foreach (var kv in d)
                o[kv.Key] = R(kv.Value, n);
            return o;
        }

        return PyDict.Of(
            ("decks", dks.Skip(1).Select(d => (object?)RoundDict(d, 3)).ToList()),
            ("tiers", tiers.Select(t => (object?)RoundDict(t, 2)).ToList()),
            ("sections", sections.Select(s => (object?)PyDict.Of(("id", s["id"]), ("x0", Math.Round(s.F("x0"), 3)), ("x1", Math.Round(s.F("x1"), 3)))).ToList()),
            ("bulkheads", tb.Concat(longi).Cast<object?>().ToList()),
            ("cells", cells.Select(c => (object?)Rnd(c)).ToList()),
            ("rooms", roomOut.Values.OrderBy(r => -r.F("x1")).ToList().Cast<object?>().ToList()));
    }

    /// <summary>plate_mm on the subdivision's decks and unarmoured bulkheads, and deck planking. Returns the ids of the
    /// layout's decks (a flight deck) that take the planking.</summary>
    public static List<string> DeckPlates(PyDict sub, PyDict plating, PyDict design, Layout lay)
    {
        double tds = design.DOr("armour").F("tds_m", 0.0);
        foreach (PyDict d in sub.L("decks").Cast<PyDict>())
        {
            string kind = d.S("kind");
            if (kind == "inner_bottom")
                d["plate_mm"] = plating["inner_bottom_mm"];
            else if (kind == "main")
            {
                d["plate_mm"] = plating["strength_deck_mm"];
                d["plate_end_mm"] = plating["strength_deck_end_mm"];
            }
            else if (kind == "raised")
                d["plate_mm"] = plating["strength_deck_end_mm"];
            else
                d["plate_mm"] = plating["deck_mm"];
        }
        double wood = plating.F("deck_wood_mm");
        var planked = wood != 0 ? lay.Decks.Where(dk => Py.Eq(dk["kind"], "flight_deck")).Select(dk => dk.S("id")).ToList() : [];
        if (wood != 0 && planked.Count == 0)
            foreach (PyDict d in sub.L("decks").Cast<PyDict>())
                if (Py.In(d["kind"], "main", "raised"))
                    d["wood_mm"] = plating["deck_wood_mm"];
        foreach (PyDict b in sub.L("bulkheads").Cast<PyDict>())
            b["plate_mm"] = Py.Eq(b.Get("kind"), "tds") ? Math.Round(Armour.TDS_MM_PER_M * tds, 1) : plating["bulkhead_mm"];
        return planked;
    }
}
