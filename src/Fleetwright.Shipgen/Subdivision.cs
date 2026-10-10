namespace Fleetwright.Shipgen;

/// <summary>subdivision: the hull below the main deck as a grid of watertight cells, and the rooms that own them.</summary>
public static class Subdivision
{
    const double MinSection = 0.03, MinSectionM = 1.0, MinSectionMaxM = 8.0, MaxSection = 0.07, MaxSectionM = 2.5;
    const double Collision = 0.05, SteelFrame = 0.92;
    const int ZSamples = 6;
    const double SliverM3 = 1.0, SliverFrac = 0.05;
    static readonly Dictionary<string, int> RoomPriority = new()
    {
        ["magazine"] = 9, ["steering"] = 6, ["boiler_room"] = 7, ["engine_room"] = 7, ["fuel_tank"] = 7, ["bunker"] = 6,
        ["cargo_tank"] = 5, ["hold"] = 5, ["accommodation"] = 2,
    };
    static readonly Dictionary<string, double> Permeability = new()
    {
        ["magazine"] = 0.6, ["steering"] = 0.85, ["boiler_room"] = 0.85, ["engine_room"] = 0.85, ["fuel_tank"] = 0.95,
        ["bunker"] = 0.95, ["cargo_tank"] = 0.95, ["hold"] = 0.6, ["accommodation"] = 0.95, ["stores"] = 0.6,
        ["double_bottom"] = 0.95, ["tds"] = 0.95,
    };
    const double CoalPermeability = 0.4;

    static double Ov(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

    /// <summary>A compartment of the layout as the box it asks for.</summary>
    sealed record RoomBox(Compartment Src, double X0, double X1, double Y0, double Y1, double Base, double Top)
    {
        public string Id => Src.Id;
        public string Kind => Src.Kind;
    }

    static (double Lo, double Hi)[] Box(RoomBox r) => [(r.X0, r.X1), (r.Y0, r.Y1), (r.Base, r.Top)];
    static (double Lo, double Hi)[] Box(Cell c) => [(c.X0, c.X1), (c.Y0, c.Y1), (c.Base, c.Top)];

    /// <summary>Does the room's box take the cell: on every axis an overlap of at least half the shorter of the two?</summary>
    static bool Claims(RoomBox room, Cell cell)
    {
        var (a, b) = (Box(room), Box(cell));
        for (int k = 0; k < 3; k++)
        {
            double o = Ov(a[k].Lo, a[k].Hi, b[k].Lo, b[k].Hi);
            if (o <= 1e-6 || o < 0.5 * Math.Min(a[k].Hi - a[k].Lo, b[k].Hi - b[k].Lo) - 1e-6)
                return false;
        }
        return true;
    }

    static double BoxOverlap(RoomBox room, Cell cell)
    {
        var (a, b) = (Box(room), Box(cell));
        double v = 1.0;
        for (int k = 0; k < 3; k++)
            v *= Math.Max(0.0, Ov(a[k].Lo, a[k].Hi, b[k].Lo, b[k].Hi));
        return v;
    }

    /// <summary>Where the hull reaches raised deck k: merged [[x0, x1]], aft to forward.</summary>
    static List<double[]> RaisedSpans(IReadOnlyList<RaisedStretch> raised, long k)
    {
        var result = new List<double[]>();
        foreach (var (x0, x1) in raised.Where(s => s.Levels >= k).Select(s => (s.X0, s.X1)).Order())
        {
            if (result.Count > 0 && x0 <= result[^1][1] + 1e-6)
                result[^1][1] = Math.Max(result[^1][1], x1);
            else
                result.Add([x0, x1]);
        }
        return result;
    }

    /// <summary>The decks, keel up, as heights above the main deck.</summary>
    static List<SubDeck> DecksOf(Design design, double D, ArmourLayout ag, IReadOnlyList<RaisedStretch> raised)
    {
        var result = new List<SubDeck> { new() { Id = "Keel", Kind = "keel", Z = -D } };
        if (design.StyleName != "planing")
            result.Add(new SubDeck { Id = "Inner bottom", Kind = "inner_bottom", Z = -D + Powerplant.DoubleBottom(D) });
        var armour = ag.Decks.GroupBy(d => d.Deck).ToDictionary(g => g.Key, g => g.ToList());

        void Armour(SubDeck d, long n)
        {
            if (!armour.TryGetValue(n, out var ps))
                return;
            if (ps.Count == 1)
            {
                (d.ArmourMm, d.X0, d.X1) = (ps[0].Mm, ps[0].X0, ps[0].X1);
                d.Material = string.IsNullOrEmpty(ps[0].Material) ? null : ps[0].Material;
            }
            else
                d.Plates = ps.Select(p => new SubDeck.DeckPlate(Math.Round(p.Mm, 3), Math.Round(p.X0, 3), Math.Round(p.X1, 3))
                    { Material = string.IsNullOrEmpty(p.Material) ? null : p.Material }).ToList();
        }

        foreach (var (n, z) in Enumerable.Reverse(Decks.DeckStack(design, D)))
        {
            var d = new SubDeck { Id = Decks.DeckName(n), Kind = n == 0 ? "main" : "deck", Deck = n, Z = z - D };
            Armour(d, n);
            result.Add(d);
        }
        long top = raised.Select(s => s.Levels).DefaultIfEmpty(0L).Max();
        for (long k = 1; k <= top; k++)
        {
            var d = new SubDeck
            {
                Id = Decks.DeckName(-k), Kind = "raised", Deck = -k, Z = k * Geometry.DeckPitch,
                Spans = RaisedSpans(raised, k).Select(s => new[] { Math.Round(s[0], 3), Math.Round(s[1], 3) }).ToList(),
            };
            Armour(d, -k);
            result.Add(d);
        }
        return result;
    }

    /// <summary>A tier is named after the deck it stands on: bottom, hold, then second, third, ...</summary>
    static string TierName(SubDeck floor)
    {
        if (floor.Kind is "keel" or "inner_bottom")
            return floor.Kind == "keel" ? "bottom" : "hold";
        string id = floor.Id.ToLowerInvariant();
        return id.EndsWith(" deck", StringComparison.Ordinal) ? id[..^5] : id;
    }

    const int BreakPriority = 8;

    /// <summary>Transverse bulkhead positions, bow to stern: [(x, kind)], with the hull's ends.</summary>
    static List<(double X, string Kind)> Stations(double L, List<RoomBox> rooms, (double X0, double X1, string Kind)? cit, double minGap,
        double maxGap, IEnumerable<double> breaks, IEnumerable<double> armoured)
    {
        var cands = new List<(double X, int P, string Kind)> { (L / 2 - Collision * L, 9, "collision") };
        cands.AddRange(breaks.Select(x => (x, BreakPriority, "main")));
        cands.AddRange(armoured.Select(x => (x, 10, "armoured")));
        if (cit is { } c)
        {
            cands.Add((c.X0, 10, c.Kind));
            cands.Add((c.X1, 10, c.Kind));
        }
        foreach (var r in rooms)
        {
            int p = RoomPriority.GetValueOrDefault(r.Kind, 4);
            cands.Add((r.X0, p, "main"));
            cands.Add((r.X1, p, "main"));
        }
        cands = cands.Where(c2 => -L / 2 + minGap <= c2.X && c2.X <= L / 2 - minGap).OrderBy(c2 => -c2.X).ToList();
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
        var result = new List<(double, string)> { (pts[0].X, "end") };
        for (int k = 0; k < pts.Count - 1; k++)
        {
            double xa = pts[k].X;
            var (xb, _, kb) = pts[k + 1];
            double gap = xa - xb;
            bool inside = rooms.Any(r => r.X0 <= xb + 1e-6 && r.X1 >= xa - 1e-6);
            if (gap > maxGap && !inside)
            {
                long n = (long)Math.Ceiling(gap / maxGap - 1e-9);
                for (long j = 1; j < n; j++)
                    result.Add((xa - gap * j / n, "main"));
            }
            result.Add((xb, kb));
        }
        return result;
    }

    /// <summary>The subdivision of the laid-out ship.</summary>
    public static SubdivisionData Build(Layout lay, Design design, Navarch.Result res, ArmourLayout ag, bool armoured, HullForm form)
    {
        var hull = lay.Hull;
        double L = hull.L, B = hull.B;
        double D = res.Depth, T = res.Draught;
        double cb = design.BlockCoefficient;
        var plan = lay.Geo.Plant;
        var mach = lay.Geo.Machinery;
        double wl = -(D - T);
        double Rz(double z) => z - D;

        var dks = DecksOf(design, D, ag, lay.Raised);
        bool hasBottom = dks[1].Kind == "inner_bottom";
        var tiers = new List<Tier>();
        for (int i = 0; i < dks.Count - 1; i++)
        {
            var (lo, hi) = (dks[i], dks[i + 1]);
            double sub_ = Math.Clamp((wl - lo.Z) / (hi.Z - lo.Z), 0.0, 1.0);
            tiers.Add(new Tier
            {
                Id = lo.Kind == "keel" && !hasBottom ? "hold" : TierName(lo), Base = lo.Z, Top = hi.Z, BelowWaterline = sub_ >= 1.0 - 1e-6,
                Submerged = sub_, Floor = lo.Id, Ceiling = hi.Id, Spans = hi.Spans,
            });
        }
        double ib = hasBottom ? dks[1].Z : -D;
        double under = ag.RoofZ is double roofZ ? roofZ - D : 0.0;
        var adecks = ag.Decks.Select(d => (Z: Rz(d.Z), d.X0, d.X1, d.Mm, d.Material)).ToList();

        var rooms = lay.Compartments.Where(c => c.Kind is not ("citadel" or "hangar")).Select(c =>
            new RoomBox(c, c.X0, c.X1, (c.Y ?? 0.0) - c.HalfWidth, (c.Y ?? 0.0) + c.HalfWidth, c.Base ?? ib, c.Top ?? under)).ToList();

        (double X0, double X1, string Kind)? cit = null;
        if (armoured)
            cit = (ag.X0, ag.X1, ag.BulkheadMm > 0 ? "armoured" : "citadel");
        else if (lay.Geo.Citadel is { } gc && design.StyleName is "warship" or "carrier")
            cit = (gc.X0, gc.X1, "citadel");
        var st = Stations(L, rooms, cit, Math.Min(MinSectionMaxM, Math.Max(MinSectionM, MinSection * L)), Math.Max(MaxSectionM, MaxSection * L),
            lay.Raised.SelectMany(s => new[] { s.X0, s.X1 }), ag.EndBulkheads.Select(b => b.X));
        var armBh = new List<ArmourBulkhead>();
        if (cit is { Kind: "armoured" })
            armBh.AddRange(Armour.CitadelBulkheads(ag));
        armBh.AddRange(ag.EndBulkheads);
        var sections = Enumerable.Range(0, st.Count - 1)
            .Select(i => new Section((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), st[i + 1].X, st[i].X)).ToList();
        var tb = new List<Bulkhead>();
        long SecLevel(Section sec) => lay.DeckLevel((sec.X0 + sec.X1) / 2);
        for (int k = 0; k < st.Count - 2; k++)
        {
            var s = st[k + 1];
            double top = Math.Min(SecLevel(sections[k]), SecLevel(sections[k + 1])) * Geometry.DeckPitch;
            var d = new Bulkhead
            {
                Id = $"Bulkhead {k + 1}", Kind = s.Kind is "collision" or "armoured" ? s.Kind : "main", X = Math.Round(s.X, 3),
                Base = Math.Round(-D, 2), Top = Math.Round(top, 2),
            };
            if (s.Kind == "armoured")
            {
                var a = armBh.MinBy(b => Math.Abs(b.X - s.X))!;
                (d.ArmourMm, d.ArmourBottom, d.ArmourTop) = (Math.Round(a.Mm), Math.Round(Rz(a.Bottom), 2), Math.Round(Rz(a.Top), 2));
                if (a.LowerTop > a.Bottom && a.LowerMm != a.Mm)
                    (d.ArmourLowerMm, d.ArmourLowerTop) = (Math.Round(a.LowerMm), Math.Round(Rz(a.LowerTop), 2));
                d.ArmourMaterial = string.IsNullOrEmpty(a.Material) ? null : a.Material;
            }
            tb.Add(d);
        }

        double tds = plan?.Tds ?? 0.0;
        double wingM = plan?.WingM ?? 0.0;
        bool centreline = design.Machinery?.CentrelineBulkhead ?? false;

        List<double> XsIn(double x0, double x1, int n = 8) => Enumerable.Range(0, n).Select(j => x0 + (x1 - x0) * (j + 0.5) / n).ToList();
        List<double> HwSamples(double x0, double x1, int n = 8) => XsIn(x0, x1, n).Select(hull.HalfWidth).ToList();
        double Widest(double x0, double x1, double z) => new[] { x0, x1 }.Concat(XsIn(x0, x1)).Max(x => form.HalfWidth(x, z + D));
        // a tier's widest: at its top while the hull widens upward; a tumblehome is widest at its knuckle, and narrows
        // above it, so a tier through the knuckle is widest there and one above it at its base
        double zKnuckle = form.KnuckleZ - D;
        double WidestIn(double x0, double x1, double zLo, double zHi)
        {
            double w = Widest(x0, x1, zHi);
            if (zLo < zKnuckle && zKnuckle < zHi)
                w = Math.Max(w, Widest(x0, x1, zKnuckle));
            else if (zLo >= zKnuckle)
                w = Math.Max(w, Widest(x0, x1, zLo));
            return w;
        }

        var cells = new List<Cell>();
        var longi = new List<Bulkhead>();
        var loBase = new Dictionary<string, double>(StringComparer.Ordinal);
        var belts = new List<Strake>();
        if (ag.BeltMm > 0)
            belts.Add(new Strake("Belt", "belt", "citadel", ag.BeltMm, ag.BeltMm, ag.X0, ag.X1, ag.BeltBottom, ag.BeltTop, ag.BeltMaterial)
                { BottomMm = ag.BeltBottomMm, Wl = Rz(ag.Waterline) });
        belts.AddRange(ag.Strakes);
        belts = belts.Select(b => b with { Bottom = Rz(b.Bottom), Top = Rz(b.Top) }).ToList();
        for (int si = 0; si < sections.Count; si++)
        {
            var sec = sections[si];
            double sx0 = sec.X0, sx1 = sec.X1;
            double xm = (sx0 + sx1) / 2;
            double hwmax = new[] { hull.HalfWidth(sx0), hull.HalfWidth(sx1) }.Concat(HwSamples(sx0, sx1)).Max();
            bool inMach = mach is { } mc && mc.X0 - 1e-6 <= xm && xm <= mc.X1 + 1e-6;
            bool inCit = cit is { } cc && cc.X0 - 1e-6 <= xm && xm <= cc.X1 + 1e-6;
            double? split = null;
            string sKind = "";
            double sTop = 0;
            if (wingM > 0 && inMach)
                (split, sKind, sTop) = (plan!.Width / 2, "wing", 0.0);
            else if (tds > 0 && inCit)
            {
                split = Math.Max(0.5, SteelFrame * XsIn(sx0, sx1).Select(form.Waterline).Min() - tds);
                (sKind, sTop) = ("tds", under);
            }
            if (split >= hwmax - 0.3)
                split = null;
            if (split is double sp)
                foreach (var (side, sgn) in new[] { ("S", 1), ("P", -1) })
                    longi.Add(new Bulkhead
                    {
                        Id = $"{(sKind == "wing" ? "Wing" : "Torpedo")} bulkhead {sec.Id} {side}", Kind = sKind, Section = sec.Id, Side = side,
                        Y = Math.Round(sgn * sp, 3), X0 = Math.Round(sx0, 3), X1 = Math.Round(sx1, 3), Base = Math.Round(ib, 2),
                        Top = Math.Round(sTop, 2),
                    });
            bool cl = centreline && inMach;
            if (cl)
                longi.Add(new Bulkhead
                {
                    Id = $"Centreline bulkhead {sec.Id}", Kind = "centreline", Section = sec.Id, Side = "C", Y = 0.0, X0 = Math.Round(sx0, 3),
                    X1 = Math.Round(sx1, 3), Base = Math.Round(ib, 2), Top = Math.Round(under, 2),
                });
            for (int ti = 0; ti < tiers.Count; ti++)
            {
                var tr = tiers[ti];
                double x0 = sx0, x1 = sx1;
                if (tr.Spans != null)
                {
                    var best = tr.Spans.Select(s => (A: Math.Max(x0, s[0]), B: Math.Min(x1, s[1]))).MaxBy(v => v.B - v.A);
                    if (best.B - best.A < 1e-3)
                        continue;
                    (x0, x1) = (best.A, best.B);
                }
                bool bottom = hasBottom && ti == 0;
                double trTop = tr.Top, trBase = tr.Base;
                bool centreSplit = cl && !bottom && trTop <= under + 1e-6;
                double hwT = Math.Min(hwmax, WidestIn(x0, x1, trBase, trTop));
                bool banded = split is not null && !bottom && trTop <= sTop + 1e-6 && split.Value < hwT - 0.3;
                if (banded)
                    loBase[sec.Id] = Math.Min(loBase.GetValueOrDefault(sec.Id, trBase), trBase);
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
                var zs = Enumerable.Range(0, ZSamples).Select(k => trBase + (trTop - trBase) * (k + 0.5) / ZSamples).ToList();
                var xsI = XsIn(x0, x1);
                var hwz = zs.Select(z => xsI.Select(x => form.HalfWidth(x, z + D)).ToList()).ToList();
                double dz = (trTop - trBase) / ZSamples;
                foreach (var (band, y0, y1) in ys)
                {
                    if (y1 - y0 < 1e-6)
                        continue;
                    double vUnder = 0.0, vOver = 0.0;
                    for (int zi = 0; zi < zs.Count; zi++)
                    {
                        var hs = hwz[zi];
                        double v = hs.Sum(h => Math.Max(0.0, Math.Min(h, y1) - Math.Max(-h, y0))) * (x1 - x0) / hs.Count * dz;
                        if (zs[zi] < wl)
                            vUnder += v;
                        else
                            vOver += v;
                    }
                    if (vUnder + vOver < 0.01 && hwT < 0.05)
                        continue;
                    var c = new Cell
                    {
                        Id = $"{sec.Id} {tr.Id} {band}", Section = sec.Id, Tier = tr.Id, Band = band, X0 = x0, X1 = x1, Y0 = y0, Y1 = y1,
                        Base = tr.Base, Top = tr.Top, BelowWaterline = tr.BelowWaterline, VUnder = vUnder, VOver = vOver, Si = si, Ti = ti, T0 = ti,
                    };
                    if (inCit && armoured)
                        c.Citadel = true;
                    var above = adecks.Where(d => trTop <= d.Z + 1e-6 && d.X0 <= xm && xm <= d.X1).ToList();
                    if (above.Count > 0)
                    {
                        c.ArmourAboveMm = above.Select(d => d.Mm).ToList();
                        if (above.Any(d => !string.IsNullOrEmpty(d.Material)))
                            c.ArmourAboveMaterial = above.Select(d => d.Material).ToList();
                    }
                    bool outer = band is "P" or "S" or "C" || (band is "CP" or "CS" && !banded);
                    var side = belts.Where(b => outer && b.X0 <= xm && xm <= b.X1 && Ov(b.Bottom, b.Top, trBase, trTop) > 0)
                        .Select(b => (Mm: Armour.BeltMmAt(b, xm, Math.Min(b.Top, trTop)), B: b)).ToList();
                    if (side.Count > 0)
                    {
                        var (mm, b) = side.MaxBy(v => v.Mm);
                        c.BeltMm = (long)Math.Round(mm);
                        c.BeltMaterial = string.IsNullOrEmpty(b.Material) ? null : b.Material;
                    }
                    if (banded && band is "P" or "S" && inCit && tds > 0)
                    {
                        double zm = (trBase + trTop) / 2 + D;
                        c.TdsM = Math.Round(Math.Max(0.0, SteelFrame * xsI.Min(x => form.HalfWidth(x, zm)) - split!.Value), 2);
                    }
                    cells.Add(c);
                }
            }
        }
        double vU = cells.Sum(c => c.VUnder);
        double underK = vU > 0 ? cb * L * B * T / vU : 1.0;
        foreach (var c in cells)
            c.VolumeM3 = c.VUnder * underK + c.VOver;
        foreach (var l_ in longi)
            if (loBase.TryGetValue(l_.Section!, out var lb) && l_.Kind != "centreline")
                l_.Base = Math.Round(lb, 2);
        longi = longi.Where(l_ => l_.Kind == "centreline" || loBase.ContainsKey(l_.Section!)).ToList();

        // ---------------- slivers join the cell above (or below) ----------------
        bool Flat(int t) => tiers[t].Spans == null;
        foreach (var c in cells.OrderBy(c => c.Ti).ToList())
        {
            double box = (c.X1 - c.X0) * (c.Y1 - c.Y0) * (c.Top - c.Base);
            if (c.Merged || c.VolumeM3 >= Math.Max(SliverM3, SliverFrac * box) || !Flat(c.Ti))
                continue;
            foreach (int dt in new[] { 1, -1 })
            {
                var d = cells.FirstOrDefault(d => d.Si == c.Si && !d.Merged && Flat(d.Ti) && (dt > 0 ? d.T0 == c.Ti + 1 : d.Ti == c.T0 - 1) &&
                                                  d.Y0 <= c.Y0 + 1e-6 && d.Y1 >= c.Y1 - 1e-6);
                if (d is null)
                    continue;
                d.VolumeM3 += c.VolumeM3;
                if (dt > 0)
                    (d.Base, d.T0) = (c.Base, c.T0);
                else
                    (d.Top, d.Ti) = (c.Top, c.Ti);
                c.Merged = true;
                break;
            }
        }
        cells = cells.Where(c => !c.Merged).ToList();

        // ---------------- rooms take cells ----------------
        var owner = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in cells)
        {
            ((int, double) Key, RoomBox R)? best = null;
            foreach (var r in rooms)
            {
                if (!Claims(r, c))
                    continue;
                var key = (RoomPriority.GetValueOrDefault(r.Kind, 4), BoxOverlap(r, c));
                if (best is null || key.CompareTo(best.Value.Key) > 0)
                    best = (key, r);
            }
            if (best is { } bb)
                owner[c.Id] = bb.R.Id;
        }
        var byId = cells.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var roomOut = new OrderedDictionary<string, Room>(StringComparer.Ordinal);
        foreach (var r in rooms)
            roomOut[r.Id] = new Room { Id = r.Id, Kind = r.Kind, Fuel = r.Src.Fuel, Tonnes = r.Src.Tonnes, Mount = r.Src.Mount, Mounts = r.Src.Mounts };
        foreach (var (cid, rid) in owner)
            roomOut[rid].Cells.Add(cid);
        foreach (var r in rooms)
        {
            var fmt = r.Src.PerSection;
            if (!roomOut.TryGetValue(r.Id, out var whole))
                continue;
            var secs = whole.Cells.Select(cid => byId[cid].Si).Distinct().Order().ToList();
            if (string.IsNullOrEmpty(fmt) || secs.Count < 2)
                continue;
            roomOut.Remove(r.Id);
            double vol = whole.Cells.Sum(cid => byId[cid].VolumeM3);
            if (vol == 0)
                vol = 1.0;
            foreach (var si in secs)
            {
                var own = whole.Cells.Where(cid => byId[cid].Si == si).ToList();
                var part = whole with { Id = fmt.Replace("{}", sections[si].Id), Cells = own };
                if (whole.Tonnes is double t)
                    part.Tonnes = Math.Round(t * own.Sum(cid => byId[cid].VolumeM3) / vol, 1);
                roomOut[part.Id] = part;
                foreach (var cid in own)
                    owner[cid] = part.Id;
            }
        }
        var also = new OrderedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var r in rooms)
        {
            if (!roomOut.TryGetValue(r.Id, out var ro) || ro.Cells.Count > 0)
                continue;
            var (rx, ry, rz) = ((r.X0 + r.X1) / 2, (r.Y0 + r.Y1) / 2, (r.Base + r.Top) / 2);
            double Distance(Cell c)
            {
                var (dx, dy, dz2) = (rx - (c.X0 + c.X1) / 2, ry - (c.Y0 + c.Y1) / 2, rz - (c.Base + c.Top) / 2);
                return Math.Sqrt(dx * dx + dy * dy + dz2 * dz2);
            }
            var nearest = cells.OrderBy(c => -BoxOverlap(r, c)).ThenBy(Distance).FirstOrDefault();
            if (nearest is null)
                continue;
            if (!also.TryGetValue(nearest.Id, out var al))
                also[nearest.Id] = al = [];
            al.Add(r.Id);
            ro.Cells.Add(nearest.Id);
            ro.Shared = true;
        }

        foreach (var c in cells)
        {
            if (owner.ContainsKey(c.Id))
                continue;
            string use, name;
            if (hasBottom && c.Ti == 0)
                (use, name) = ("double_bottom", "Double bottom");
            else if (c.TdsM != null && c.Band is "P" or "S")
                (use, name) = ("tds", "Torpedo protection");
            else if (tiers[c.Ti].Submerged >= 0.5)
                (use, name) = ("stores", "Stores");
            else
                (use, name) = ("accommodation", "Quarters");
            string rid = $"{name} {c.Section}" + (use == "tds" ? $" {c.Band}" : "");
            if (!roomOut.TryGetValue(rid, out var ro))
                roomOut[rid] = ro = new Room { Id = rid, Kind = use };
            ro.Cells.Add(c.Id);
            owner[c.Id] = rid;
        }

        // ---------------- crew over the quarters, by volume ----------------
        long nCrew = (lay.Crew?.Complement ?? 0) - (lay.Crew?.QuarteredInSuperstructure ?? 0);
        var quarters = cells.Where(c => roomOut[owner[c.Id]].Kind == "accommodation").ToList();
        double qv = quarters.Sum(c => c.VolumeM3);
        if (nCrew != 0 && qv > 0)
        {
            var shares = quarters.Select(c => nCrew * c.VolumeM3 / qv).ToList();
            var men = shares.Select(s => (long)s).ToList();
            foreach (var k in Enumerable.Range(0, quarters.Count).OrderBy(k => men[k] - shares[k]).Take((int)Math.Max(0, nCrew - men.Sum())).ToList())
                men[k] += 1;
            for (int i = 0; i < quarters.Count; i++)
                if (men[i] != 0)
                    quarters[i].Crew = men[i];
        }
        foreach (var r in roomOut.Values)
        {
            long crew = r.Cells.Where(cid => owner[cid] == r.Id).Sum(cid => byId[cid].Crew ?? 0);
            r.Crew = crew != 0 ? crew : null;
        }

        // ---------------- per-cell finish: room, permeability, neighbours ----------------
        var fuel = plan?.Fuel;
        foreach (var c in cells)
        {
            var r = roomOut[owner[c.Id]];
            c.Room = r.Id;
            c.Also = also.GetValueOrDefault(c.Id);
            double p = Permeability.GetValueOrDefault(r.Kind, 0.9);
            if (r.Kind == "bunker" && (r.Fuel ?? fuel) == "coal")
                p = CoalPermeability;
            c.Permeability = p;
        }
        var grid = cells.GroupBy(c => c.Si).ToDictionary(g => g.Key, g => g.ToList());
        static bool Meets(Cell c, Cell d) => d.T0 <= c.Ti && c.T0 <= d.Ti;
        var lon = longi.ToDictionary(l_ => (l_.Section!, l_.Side!), l_ => l_.Id);
        foreach (var c in cells)
        {
            var nb = new List<(Cell D, string Via)>();
            foreach (var d in grid[c.Si])
            {
                if (ReferenceEquals(d, c) || !Meets(c, d))
                    continue;
                double y;
                if (Math.Abs(d.Y0 - c.Y1) < 1e-6)
                    y = c.Y1;
                else if (Math.Abs(d.Y1 - c.Y0) < 1e-6)
                    y = c.Y0;
                else
                    continue;
                nb.Add((d, lon[(c.Section, Math.Abs(y) < 1e-6 ? "C" : y > 0 ? "S" : "P")]));
            }
            foreach (var (dt, dk) in new[] { (-1, tiers[c.T0].Floor), (1, tiers[c.Ti].Ceiling) })
                foreach (var d in grid[c.Si])
                    if ((dt < 0 ? d.Ti == c.T0 - 1 : d.T0 == c.Ti + 1) && Ov(c.Y0, c.Y1, d.Y0, d.Y1) > 1e-6)
                        nb.Add((d, dk));
            foreach (int ds in new[] { -1, 1 })
                if (grid.TryGetValue(c.Si + ds, out var gl))
                    foreach (var d in gl)
                        if (Meets(c, d) && Ov(c.Y0, c.Y1, d.Y0, d.Y1) > 1e-6)
                            nb.Add((d, tb[Math.Min(c.Si, d.Si)].Id));
            c.Neighbours = nb.Select(t => new[] { t.D.Id, t.D.Room == c.Room ? "open" : t.Via }).ToList();
        }

        foreach (var r in roomOut.Values)
        {
            var own = r.Cells.Select(cid => byId[cid]).ToList();
            r.VolumeM3 = Math.Round(own.Where(c => owner[c.Id] == r.Id).Sum(c => c.VolumeM3), 1);
            r.X0 = Math.Round(own.Min(c => c.X0), 3);
            r.X1 = Math.Round(own.Max(c => c.X1), 3);
            r.Base = Math.Round(own.Min(c => c.Base), 2);
            r.Top = Math.Round(own.Max(c => c.Top), 2);
        }

        // ---------------- published precision ----------------
        foreach (var c in cells)
        {
            (c.X0, c.X1, c.Y0, c.Y1) = (Math.Round(c.X0, 3), Math.Round(c.X1, 3), Math.Round(c.Y0, 3), Math.Round(c.Y1, 3));
            (c.Base, c.Top, c.VolumeM3, c.Permeability) = (Math.Round(c.Base, 2), Math.Round(c.Top, 2), Math.Round(c.VolumeM3, 1), Math.Round(c.Permeability, 3));
        }
        foreach (var d in dks)
        {
            d.Z = Math.Round(d.Z, 3);
            (d.ArmourMm, d.X0, d.X1) = (Round(d.ArmourMm, 3), Round(d.X0, 3), Round(d.X1, 3));
        }
        foreach (var t in tiers)
            (t.Base, t.Top, t.Submerged) = (Math.Round(t.Base, 2), Math.Round(t.Top, 2), Math.Round(t.Submerged, 2));
        return new SubdivisionData(dks.Skip(1).ToList(), tiers,
            sections.Select(s => s with { X0 = Math.Round(s.X0, 3), X1 = Math.Round(s.X1, 3) }).ToList(), tb.Concat(longi).ToList(), cells,
            roomOut.Values.OrderBy(r => -r.X1).ToList());
    }

    static double? Round(double? v, int n) => v is double d ? Math.Round(d, n) : null;

    /// <summary>plate_mm on the subdivision's decks and unarmoured bulkheads, and deck planking. Returns the ids of the
    /// layout's decks (a flight deck) that take the planking.</summary>
    public static List<string> DeckPlates(SubdivisionData sub, HullPlates plating, Design design, Layout lay)
    {
        double tds = design.Armour?.TdsM ?? 0.0;
        foreach (var d in sub.Decks)
        {
            switch (d.Kind)
            {
                case "inner_bottom":
                    d.PlateMm = plating.InnerBottomMm;
                    break;
                case "main":
                    (d.PlateMm, d.PlateEndMm) = (plating.StrengthDeckMm, plating.StrengthDeckEndMm);
                    break;
                case "raised":
                    d.PlateMm = plating.StrengthDeckEndMm;
                    break;
                default:
                    d.PlateMm = plating.DeckMm;
                    break;
            }
        }
        double wood = plating.DeckWoodMm;
        var planked = wood != 0 ? lay.Decks.Where(dk => dk.Kind == "flight_deck").Select(dk => dk.Id).ToList() : [];
        if (wood != 0 && planked.Count == 0)
            foreach (var d in sub.Decks.Where(d => d.Kind is "main" or "raised"))
                d.WoodMm = plating.DeckWoodMm;
        foreach (var b in sub.Bulkheads)
            b.PlateMm = b.Kind == "tds" ? Math.Round(Armour.TdsMmPerM * tds, 1) : plating.BulkheadMm;
        return planked;
    }
}
