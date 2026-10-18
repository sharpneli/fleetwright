namespace Fleetwright.Shipgen;

/// <summary>subdivision: the hull below the main deck as a grid of watertight cells, and the rooms that own them.</summary>
public static class Subdivision
{
    const double MinSection = 0.03, MinSectionM = 1.0, MinSectionMaxM = 8.0, MaxSection = 0.07, MaxSectionM = 2.5;
    public const double Collision = 0.05;
    const double SteelFrame = 0.92;
    const int ZSamples = 6;
    const double SliverM3 = 1.0, SliverFrac = 0.05;
    /// <summary>Which room takes a cell two rooms claim, the higher first.</summary>
    /// <summary>The shortest section the subdivision makes in a hull L long, m.</summary>
    internal static double MinSectionOf(double L) => Math.Min(MinSectionMaxM, Math.Max(MinSectionM, MinSection * L));

    internal static int RoomPriority(RoomKind k) => k switch
    {
        RoomKind.Magazine => 9,
        RoomKind.TorpedoRoom or RoomKind.Handling => 8,
        RoomKind.BoilerRoom or RoomKind.EngineRoom or RoomKind.FuelTank => 7,
        RoomKind.Steering or RoomKind.Bunker or RoomKind.TransmittingStation => 6,
        RoomKind.CargoTank or RoomKind.Hold => 5,
        RoomKind.Stores or RoomKind.DoubleBottom or RoomKind.Tds or RoomKind.Hangar => 4,
        RoomKind.Accommodation => 2,
    };

    /// <summary>The share of a room's volume water can fill.</summary>
    static double Permeability(RoomKind k) => k switch
    {
        RoomKind.Magazine or RoomKind.Hold or RoomKind.Stores => 0.6,
        RoomKind.TorpedoRoom or RoomKind.Handling or RoomKind.Steering or RoomKind.BoilerRoom or RoomKind.EngineRoom
            or RoomKind.TransmittingStation => 0.85,
        RoomKind.FuelTank or RoomKind.Bunker or RoomKind.CargoTank or RoomKind.Accommodation or RoomKind.DoubleBottom or RoomKind.Tds => 0.95,
        RoomKind.Hangar => 0.9,
    };
    const double CoalPermeability = 0.4;

    static double Ov(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

    /// <summary>A compartment of the layout as the box it asks for.</summary>
    sealed record RoomBox(Compartment Src, double X0, double X1, double Y0, double Y1, double Base, double Top)
    {
        public string Id => Src.Id;
        public RoomKind Kind => Src.Kind;
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
            result.Add(new SubDeck { Id = "Inner bottom", Kind = "inner_bottom", Z = -D + Geometry.DoubleBottom(D) });
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

    /// <summary>A magazine's crown this close to a deck is on the deck: no platform, the deck is its crown.</summary>
    const double PlatformTol = 0.05;

    /// <summary>Platform decks (protection-plan.md H1): a deck at a magazine's crown where it falls inside a tier, over the
    /// magazines with that crown (Spans, merged). Inserted into dks (keel up) in height order.</summary>
    static void Platforms(List<SubDeck> dks, IEnumerable<RoomBox> rooms)
    {
        var at = new SortedDictionary<double, List<double[]>>();
        foreach (var r in rooms)
        {
            if (r.Kind != RoomKind.Magazine)
                continue;
            double z = Math.Round(r.Top, 3);
            if (dks.Any(d => Math.Abs(d.Z - z) < PlatformTol) || z <= dks[0].Z || z >= 0)
                continue;
            if (!at.TryGetValue(z, out var spans))
                at[z] = spans = [];
            spans.Add([r.X0, r.X1]);
        }
        int k = 0;
        foreach (var (z, spans) in at)
        {
            var merged = new List<double[]>();
            foreach (var s in spans.OrderBy(s => s[0]))
            {
                if (merged.Count > 0 && s[0] <= merged[^1][1] + 1e-6)
                    merged[^1][1] = Math.Max(merged[^1][1], s[1]);
                else
                    merged.Add([s[0], s[1]]);
            }
            k++;
            var deck = new SubDeck
            {
                Id = at.Count > 1 ? $"Platform deck {k}" : "Platform deck", Kind = "platform", Z = z,
                Spans = merged.Select(s => new[] { Math.Round(s[0], 3), Math.Round(s[1], 3) }).ToList(),
            };
            int i = dks.FindIndex(d => d.Z > z);
            dks.Insert(i < 0 ? dks.Count : i, deck);
        }
    }

    /// <summary>Does a platform deck run over the section: over half the shorter of the two, as a room claims a cell (a
    /// magazine shorter than its section takes all of it).</summary>
    static bool Covers(SubDeck d, double x0, double x1)
    {
        foreach (var s in d.Spans!)
        {
            double o = Math.Min(x1, s[1]) - Math.Max(x0, s[0]);
            if (o > 1e-6 && o >= 0.5 * Math.Min(x1 - x0, s[1] - s[0]) - 1e-6)
                return true;
        }
        return false;
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
        double maxGap, IEnumerable<double> breaks, IEnumerable<double> armoured, IEnumerable<double> machinery)
    {
        var cands = new List<(double X, int P, string Kind)> { (L / 2 - Collision * L, 9, "collision") };
        // the machinery's ends hold like the citadel's: a short room beside it (a secondary battery's magazine) joins
        // the section on its other side, rather than taking the bottom of an engine room's
        cands.AddRange(machinery.Select(x => (x, 10, "main")));
        cands.AddRange(breaks.Select(x => (x, BreakPriority, "main")));
        cands.AddRange(armoured.Select(x => (x, 10, "armoured")));
        if (cit is { } c)
        {
            cands.Add((c.X0, 10, c.Kind));
            cands.Add((c.X1, 10, c.Kind));
        }
        foreach (var r in rooms)
        {
            int p = RoomPriority(r.Kind);
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
            // one room (an engine room) is one compartment however long: a room covers the gap when what it leaves is too
            // short to be a section of its own (a station merged a few cm off its end)
            bool inside = rooms.Any(r => Ov(r.X0, r.X1, xb, xa) >= gap - minGap);
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

    /// <summary>The magazine rooms as the subdivision gave them their cells: each room's stretch, its cells' widest
    /// half-width, floor and crown (above the main deck). A short magazine takes its whole section, so a room can be longer
    /// than Stow made it (protection-layers.md finding 1).</summary>
    public static List<(double X0, double X1, double HalfWidth, double Base, double Top)> MagazineRooms(SubdivisionData sub)
    {
        var byId = sub.Cells.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var result = new List<(double X0, double X1, double HalfWidth, double Base, double Top)>();
        foreach (var r in sub.Rooms)
        {
            if (r.Kind != RoomKind.Magazine || r.Cells.Count == 0)
                continue;
            double hw = 0.0;
            foreach (var id in r.Cells)
                if (byId.TryGetValue(id, out var c) && c.Room == r.Id)
                    hw = Math.Max(hw, Math.Max(Math.Abs(c.Y0), Math.Abs(c.Y1)));
            result.Add((r.X0, r.X1, hw, r.Base, r.Top));
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
        double ib = hasBottom ? dks[1].Z : -D;
        double under = ag.RoofZ is double roofZ ? roofZ - D : 0.0;
        var adecks = ag.Decks.Select(d => (Z: Rz(d.Z), d.X0, d.X1, d.Mm, d.Material)).ToList();

        var rooms = lay.Compartments.Where(c => c.Kind != RoomKind.Hangar).Select(c =>
            new RoomBox(c, c.X0, c.X1, (c.Y ?? 0.0) - c.HalfWidth, (c.Y ?? 0.0) + c.HalfWidth, c.Base ?? ib, c.Top ?? under)).ToList();
        Platforms(dks, rooms);
        var tiers = new List<Tier>();
        for (int i = 0; i < dks.Count - 1; i++)
        {
            var (lo, hi) = (dks[i], dks[i + 1]);
            double sub_ = Math.Clamp((wl - lo.Z) / (hi.Z - lo.Z), 0.0, 1.0);
            tiers.Add(new Tier
            {
                Id = lo.Kind == "keel" && !hasBottom ? "hold" : TierName(lo), Base = lo.Z, Top = hi.Z, BelowWaterline = sub_ >= 1.0 - 1e-6,
                Submerged = sub_, Floor = lo.Id, Ceiling = hi.Id, Spans = hi.Kind == "raised" ? hi.Spans : null,
            });
        }
        // a platform deck stands only over its spans: elsewhere the tiers it parts are one space
        var platform = dks.Select(d => d.Kind == "platform" ? d : null).ToList();   // by deck: tier i stands on deck i, under deck i + 1
        var mergedCells = new HashSet<Cell>();

        (double X0, double X1, string Kind)? cit = null;
        if (armoured)
            cit = (ag.X0, ag.X1, ag.BulkheadMm > 0 ? "armoured" : "citadel");
        else if (lay.Geo.Citadel is { } gc && design.StyleName is "warship" or "carrier")
            cit = (gc.X0, gc.X1, "citadel");
        var st = Stations(L, rooms, cit, MinSectionOf(L), Math.Max(MaxSectionM, MaxSection * L),
            lay.Raised.SelectMany(s => new[] { s.X0, s.X1 }), ag.EndBulkheads.Select(b => b.X),
            mach is { } me ? [me.X0, me.X1] : []);
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
        var tdsSpec = Armour.Tds(design);
        var inner = armoured ? Armour.InnerBulkhead(design) : (Mm: 0.0, MagazinesMm: 0.0, InboardM: (double?)null);
        bool innerLine = Math.Max(inner.Mm, inner.MagazinesMm) > 0;
        var boxSpec = Armour.MagazineBox(design);
        double magWing = design.StyleName == "planing" ? 0.0 : Armour.MagazineWingM(design, B);
        if (boxSpec.SideMm > 0 && magWing <= 0)
            magWing = Armour.MagazineWingShare * B;   // the box's sides need a wall to stand on
        var mags = rooms.Where(r => r.Kind == RoomKind.Magazine).ToList();
        var magWingCells = new HashSet<Cell>();   // wing cells a magazine may not take
        var crowns = new Dictionary<int, double>();   // by section: the magazines' crown there
        var stairWalls = new HashSet<Bulkhead>();   // a slope's wing walls, tier by tier
        bool centreline = design.Machinery?.CentrelineBulkhead ?? false;

        List<double> XsIn(double x0, double x1, int n = 8) => Enumerable.Range(0, n).Select(j => x0 + (x1 - x0) * (j + 0.5) / n).ToList();
        List<double> HwSamples(double x0, double x1, int n = 8) => XsIn(x0, x1, n).Select(hull.HalfWidth).ToList();
        double Widest(double x0, double x1, double z) => new[] { x0, x1 }.Concat(XsIn(x0, x1)).Max(x => form.HalfWidth(x, z + D));
        // a tier's widest: at its top while the hull widens upward; above a tumblehome's knuckle the side flares out from
        // a fine waterline toward the ends while it leans in, so a tier there is widest anywhere up its height: sampled
        double zKnuckle = form.KnuckleZ - D;
        double WidestIn(double x0, double x1, double zLo, double zHi)
        {
            double w = Widest(x0, x1, zHi);
            if (zHi <= zKnuckle)
                return w;
            double lo = Math.Max(zLo, zKnuckle);
            const int n = 6;
            for (int i = 0; i < n; i++)
                w = Math.Max(w, Widest(x0, x1, lo + (zHi - lo) * i / n));
            return w;
        }

        var cells = new List<Cell>();
        var longi = new List<Bulkhead>();
        var loBase = new Dictionary<string, double>(StringComparer.Ordinal);
        var belts = new List<Strake>();
        if (ag.BeltMm > 0)
            belts.Add(new Strake("Belt", "belt", Extent.Citadel, ag.BeltMm, ag.BeltMm, ag.BeltX0, ag.BeltX1, ag.BeltBottom, ag.BeltTop, ag.BeltMaterial)
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
            var layerYs = new List<double>();   // a layered TDS's walls outboard of the holding bulkhead, inside out
            if (wingM > 0 && inMach)
                (split, sKind, sTop) = (plan!.Width / 2, "wing", 0.0);
            else if (tds > 0 && inCit)
            {
                double shell = SteelFrame * XsIn(sx0, sx1).Select(form.Waterline).Min();
                split = Math.Max(0.5, shell - tds);
                (sKind, sTop) = ("tds", under);
                for (int k = 1; k < tdsSpec.Layers; k++)
                    layerYs.Add(split.Value + (shell - split.Value) * k / tdsSpec.Layers);
            }
            else if (magWing > 0 || innerLine && inCit)
            {
                // a magazine here: a wing of stores between it and the side, up to its crown (protection-plan.md H2)
                double crown = double.NegativeInfinity;
                foreach (var m in mags)
                {
                    double o = Ov(m.X0, m.X1, sx0, sx1);
                    if (o > 1e-6 && o >= 0.5 * Math.Min(m.X1 - m.X0, sx1 - sx0) - 1e-6)
                        crown = Math.Max(crown, Math.Round(m.Top, 3));   // as the platform deck's height
                }
                // the wing measured in from the side halfway up the magazine: a fine end narrows fast under the waterline,
                // and a box cell can't follow it, so the wing is full width there and thins toward the floor
                if (crown > ib)
                {
                    crowns[si] = crown;
                    double zm = (ib + crown) / 2 + D;
                    (split, sKind, sTop) = (Math.Max(0.5, SteelFrame * XsIn(sx0, sx1).Min(x => form.HalfWidth(x, zm)) - magWing), "wing", crown);
                }
                else if (inner.Mm > 0 && inCit)
                    // the inner bulkhead where nothing else stands: its own wall in from the side, up to the armour deck
                    // (one only beside the magazines, Dreadnought's screens, has no wall of its own elsewhere)
                    (split, sKind, sTop) = (Math.Max(0.5, SteelFrame * XsIn(sx0, sx1).Select(form.Waterline).Min() - (inner.InboardM ?? magWing)), "wing", under);
                if (split is not null && innerLine && inCit)
                    sTop = Math.Max(sTop, under);
            }
            // a sloped deck over it (protection-plan.md H6): the space over the slope is a wing. Cells stay boxes, so each tier
            // the slope passes through gets its wing wall where the slope stands at the tier's top, a staircase down to its
            // foot: what's over the slope is the wing's, and a little under it too (a burst just under the slope is booked to
            // the wing, never one on it to the vitals); the walls a design asked for (a TDS, the inner line, a magazine's
            // wing up to its crown) stay where they are
            // (an internal belt the same: the wedge between it and the shell is the wing)
            var slopes = ag.Inclined.Where(p => p.X0 <= xm && xm <= p.X1)
                .Select(p => (P: p, Z0: Rz(p.Z0), Z1: Rz(p.Z1), Hw: SteelFrame * XsIn(sx0, sx1).Min(x => form.HalfWidth(x, p.SideZ)))).ToList();
            double fixedTop = split is null || sKind == "wing" && wingM > 0 && inMach ? double.NegativeInfinity
                : sKind == "tds" || innerLine && inCit ? double.PositiveInfinity : crowns.GetValueOrDefault(si, double.NegativeInfinity);
            var stairs = new List<(string Tier, double Base, double Top, double Y)>();   // the slope's wing walls, tier by tier
            if (split >= hwmax - 0.3)
                split = null;
            // the inner bulkhead's armour on this section's wall: thicker beside a magazine (protection-plan.md H4)
            double innerMm = innerLine && inCit ? crowns.ContainsKey(si) ? inner.MagazinesMm : inner.Mm : 0.0;
            double innerTop = under;
            if (boxSpec.SideMm > 0 && crowns.TryGetValue(si, out double boxTop) && boxSpec.SideMm > innerMm)
                (innerMm, innerTop) = (boxSpec.SideMm, innerMm > 0 ? Math.Max(under, boxTop) : boxTop);   // the box's side
            if (split is double sp)
                foreach (var (side, sgn) in new[] { ("S", 1), ("P", -1) })
                {
                    longi.Add(new Bulkhead
                    {
                        Id = $"{(sKind == "wing" ? "Wing" : "Torpedo")} bulkhead {sec.Id} {side}", Kind = sKind, Section = sec.Id, Side = side,
                        Y = Math.Round(sgn * sp, 3), X0 = Math.Round(sx0, 3), X1 = Math.Round(sx1, 3), Base = Math.Round(ib, 2),
                        Top = Math.Round(sTop, 2), PlateMm = sKind == "tds" ? Math.Round(tdsSpec.HoldingMm, 1) : null,
                        ArmourMm = innerMm > 0 ? Math.Round(innerMm, 1) : null, ArmourBottom = innerMm > 0 ? Math.Round(ib, 2) : null,
                        ArmourTop = innerMm > 0 ? Math.Round(innerTop, 2) : null,
                        ArmourMaterial = innerMm > 0 && !string.IsNullOrEmpty(ag.BulkheadMaterial) ? ag.BulkheadMaterial : null,
                    });
                    for (int k = 0; k < layerYs.Count; k++)
                        longi.Add(new Bulkhead
                        {
                            Id = $"Torpedo bulkhead {sec.Id} {side}{k + 1}", Kind = "tds", Section = sec.Id, Side = side, Y = Math.Round(sgn * layerYs[k], 3),
                            X0 = Math.Round(sx0, 3), X1 = Math.Round(sx1, 3), Base = Math.Round(ib, 2), Top = Math.Round(sTop, 2),
                            PlateMm = Math.Round(tdsSpec.LayerMm, 1),
                        });
                }
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
                if (platform[ti] is { } pf && !Covers(pf, sx0, sx1))
                    continue;   // the space under it reaches up through
                int tj = ti;
                while (tj + 1 < tiers.Count && platform[tj + 1] is { } up && !Covers(up, sx0, sx1))
                    tj++;
                var trUp = tiers[tj];
                double x0 = sx0, x1 = sx1;
                if (tr.Spans != null)
                {
                    var best = tr.Spans.Select(s => (A: Math.Max(x0, s[0]), B: Math.Min(x1, s[1]))).MaxBy(v => v.B - v.A);
                    if (best.B - best.A < 1e-3)
                        continue;
                    (x0, x1) = (best.A, best.B);
                }
                bool bottom = hasBottom && ti == 0;
                double trTop = trUp.Top, trBase = tr.Base;
                bool centreSplit = cl && !bottom && trTop <= under + 1e-6;
                double hwT = Math.Min(hwmax, WidestIn(x0, x1, trBase, trTop));
                double? ts = split;
                if (!bottom && trBase >= fixedTop - PlatformTol)
                    foreach (var (p, z0, z1, hw) in slopes)
                    {
                        if (trTop <= z0 + PlatformTol || trBase >= z1 - PlatformTol)
                            continue;
                        // where the plate stands furthest in over the tier: a slope at its top, a belt at its foot
                        double U(double z) => p.U0 + (Math.Clamp(z, z0, z1) - z0) * (p.U1 - p.U0) / (z1 - z0);
                        double ys0 = Math.Max(0.5, hw - Math.Max(U(trBase), U(trTop)));
                        if ((ts ?? double.PositiveInfinity) > ys0 && (split is null || ys0 < split.Value - 0.05))
                            ts = ys0;
                    }
                bool stair = ts != split;
                bool banded = ts is not null && !bottom && (stair || trTop <= sTop + PlatformTol) && ts.Value < hwT - 0.3;
                if (banded && !stair)
                    loBase[sec.Id] = Math.Min(loBase.GetValueOrDefault(sec.Id, trBase), trBase);
                if (banded && stair)
                {
                    if (stairs.Count > 0 && Math.Abs(stairs[^1].Y - ts!.Value) < 1e-3 && Math.Abs(stairs[^1].Top - trBase) < 1e-6)
                        stairs[^1] = stairs[^1] with { Top = trUp.Top };
                    else
                        stairs.Add((tr.Id, trBase, trUp.Top, ts!.Value));
                }
                List<(string Band, double Y0, double Y1)> ys;
                if (banded)
                {
                    double s2 = ts!.Value;
                    // a layered TDS: a band a layer, P1 against the holding bulkhead out to the shell; the walls the hull at
                    // this height doesn't reach past leave the outer layers out
                    var walls = stair ? [] : layerYs.Where(y => y < hwT - 0.1).ToList();
                    ys = [];
                    for (int k = walls.Count; k >= 0; k--)
                        ys.Add((walls.Count == 0 && (stair || layerYs.Count == 0) ? "P" : $"P{k + 1}", -(k < walls.Count ? walls[k] : hwT), -(k > 0 ? walls[k - 1] : s2)));
                    if (centreSplit)
                        ys.AddRange([("CP", -s2, 0.0), ("CS", 0.0, s2)]);
                    else
                        ys.Add(("C", -s2, s2));
                    for (int k = 0; k <= walls.Count; k++)
                        ys.Add((walls.Count == 0 && (stair || layerYs.Count == 0) ? "S" : $"S{k + 1}", k > 0 ? walls[k - 1] : s2, k < walls.Count ? walls[k] : hwT));
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
                        Base = tr.Base, Top = trUp.Top, BelowWaterline = trUp.BelowWaterline, VUnder = vUnder, VOver = vOver, Si = si, Ti = tj, T0 = ti,
                    };
                    if (tj > ti)
                        mergedCells.Add(c);
                    if (inCit && armoured)
                        c.Citadel = true;
                    if (banded && Wing(band) && !(wingM > 0 && inMach))
                        magWingCells.Add(c);   // the side protection or a magazine's wing: never the magazine's own
                    var above = adecks.Where(d => trTop <= d.Z + 1e-6 && d.X0 <= xm && xm <= d.X1).ToList();
                    if (above.Count > 0)
                    {
                        c.ArmourAboveMm = above.Select(d => d.Mm).ToList();
                        if (above.Any(d => !string.IsNullOrEmpty(d.Material)))
                            c.ArmourAboveMaterial = above.Select(d => d.Material).ToList();
                    }
                    bool outer = Math.Abs(y1 - hwT) < 1e-6 || Math.Abs(y0 + hwT) < 1e-6;   // against the shell
                    var side = belts.Where(b => outer && b.X0 <= xm && xm <= b.X1 && Ov(b.Bottom, b.Top, trBase, trTop) > 0)
                        .Select(b => (Mm: Armour.BeltMmAt(b, xm, Math.Min(b.Top, trTop)), B: b)).ToList();
                    if (side.Count > 0)
                    {
                        var (mm, b) = side.Any(v => v.B.Kind == "zone") ? side.First(v => v.B.Kind == "zone") : side.MaxBy(v => v.Mm);
                        c.BeltMm = (long)Math.Round(mm);
                        c.BeltMaterial = string.IsNullOrEmpty(b.Material) ? null : b.Material;
                    }
                    if (banded && !stair && Wing(band) && inCit && tds > 0)
                    {
                        double zm = (trBase + trTop) / 2 + D;
                        c.TdsM = Math.Round(Math.Max(0.0, SteelFrame * xsI.Min(x => form.HalfWidth(x, zm)) - split!.Value), 2);
                    }
                    cells.Add(c);
                }
            }
            // the slope's walls, one a tier (protection-plan.md H6)
            foreach (var (tid, b0, b1, y) in stairs)
                foreach (var (side, sgn) in new[] { ("S", 1), ("P", -1) })
                {
                    var w = new Bulkhead
                    {
                        Id = $"Wing bulkhead {sec.Id} {side} ({tid})",
                        Kind = "wing", Section = sec.Id, Side = side, Y = Math.Round(sgn * y, 3), X0 = Math.Round(sx0, 3), X1 = Math.Round(sx1, 3),
                        Base = Math.Round(b0, 2), Top = Math.Round(b1, 2),
                    };
                    longi.Add(w);
                    stairWalls.Add(w);
                }
        }
        double vU = cells.Sum(c => c.VUnder);
        double underK = vU > 0 ? cb * L * B * T / vU : 1.0;
        foreach (var c in cells)
            c.VolumeM3 = c.VUnder * underK + c.VOver;
        foreach (var l_ in longi)
            if (loBase.TryGetValue(l_.Section!, out var lb) && l_.Kind != "centreline" && !stairWalls.Contains(l_))
                l_.Base = Math.Round(lb, 2);
        longi = longi.Where(l_ => l_.Kind == "centreline" || stairWalls.Contains(l_) || loBase.ContainsKey(l_.Section!)).ToList();

        // ---------------- slivers join the cell above (or below) ----------------
        bool Flat(int t) => tiers[t].Spans == null;
        foreach (var c in cells.OrderBy(c => c.Ti).ToList())
        {
            double box = (c.X1 - c.X0) * (c.Y1 - c.Y0) * (c.Top - c.Base);
            if (c.Merged || c.VolumeM3 >= Math.Max(SliverM3, SliverFrac * box) || !Flat(c.Ti))
                continue;
            foreach (int dt in new[] { 1, -1 })
            {
                if (platform[dt > 0 ? c.Ti + 1 : c.T0] is not null ||
                    crowns.TryGetValue(c.Si, out double cz) && Math.Abs((dt > 0 ? tiers[c.Ti].Top : tiers[c.T0].Base) - cz) < PlatformTol)
                    continue;   // never through a magazine's crown
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
                if (!Claims(r, c) || r.Kind == RoomKind.Magazine && magWingCells.Contains(c) || c.TdsM != null && Wing(c.Band))
                    continue;   // a torpedo protection's layers are its own, outboard of the holding bulkhead
                var key = (RoomPriority(r.Kind), BoxOverlap(r, c));
                if (best is null || key.CompareTo(best.Value.Key) > 0)
                    best = (key, r);
            }
            if (best is { } bb)
                owner[c.Id] = bb.R.Id;
        }
        var byId = cells.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var roomOut = new OrderedDictionary<string, Room>(StringComparer.Ordinal);
        foreach (var r in rooms)
            roomOut[r.Id] = new Room { Id = r.Id, Kind = r.Kind, Fuel = r.Src.Fuel, Tonnes = r.Src.Tonnes, Mount = r.Src.Mount, Mounts = r.Src.Mounts,
                Tubes = r.Src.Tubes };
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
            RoomKind use;
            string name;
            if (hasBottom && c.Ti == 0)
                (use, name) = (RoomKind.DoubleBottom, "Double bottom");
            else if (c.TdsM != null && Wing(c.Band))
                (use, name) = (RoomKind.Tds, "Torpedo protection");
            else if ((mergedCells.Contains(c) ? Math.Clamp((wl - c.Base) / (c.Top - c.Base), 0.0, 1.0) : tiers[c.Ti].Submerged) >= 0.5)
                (use, name) = (RoomKind.Stores, "Stores");
            else
                (use, name) = (RoomKind.Accommodation, "Quarters");
            string rid = $"{name} {c.Section}" + (use == RoomKind.Tds ? $" {c.Band}" : "");
            if (!roomOut.TryGetValue(rid, out var ro))
                roomOut[rid] = ro = new Room { Id = rid, Kind = use };
            ro.Cells.Add(c.Id);
            owner[c.Id] = rid;
        }

        // ---------------- crew over the quarters, by volume ----------------
        long nCrew = (lay.Crew?.Complement ?? 0) - (lay.Crew?.QuarteredInSuperstructure ?? 0);
        var quarters = cells.Where(c => roomOut[owner[c.Id]].Kind == RoomKind.Accommodation).ToList();
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
            double p = Permeability(r.Kind);
            if (r.Kind == RoomKind.Bunker && (r.Fuel ?? fuel) == "coal")
                p = CoalPermeability;
            c.Permeability = p;
        }
        var grid = cells.GroupBy(c => c.Si).ToDictionary(g => g.Key, g => g.ToList());
        static bool Meets(Cell c, Cell d) => d.T0 <= c.Ti && c.T0 <= d.Ti;
        var lon = longi.ToDictionary(l_ => (l_.Section!, Math.Round(l_.Y ?? 0.0, 3)), l_ => l_.Id);
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
                nb.Add((d, lon[(c.Section, Math.Round(y, 3))]));
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

    /// <summary>A wing band: outboard of a longitudinal bulkhead (P, S, or a layered TDS's P1, S2, ...).</summary>
    static bool Wing(string band) => band.Length > 0 && band[0] is 'P' or 'S';

    /// <summary>A main bulkhead's lowest strake, thicker for the water's head at its foot (protection-plan.md H3: about
    /// 1.3 times, [INFERRED]); a collision bulkhead's plate, heavier than a main one's and without openings.</summary>
    const double LowStrake = 1.3, CollisionPlate = 1.5;

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
        // the lowest strake up to the hold's top (the tier over the double bottom)
        double lowTop = sub.Tiers.Count > 1 && sub.Tiers[0].Id == "bottom" ? sub.Tiers[1].Top : sub.Tiers[0].Top;
        foreach (var b in sub.Bulkheads)
            switch (b.Kind)
            {
                case "tds":
                    b.PlateMm ??= Math.Round(Armour.TdsMmPerM * tds, 1);   // set by the layers; this only for an old build
                    break;
                case "collision":
                    b.PlateMm = Math.Round(CollisionPlate * plating.BulkheadMm, 1);
                    break;
                case "main" or "armoured":
                    (b.PlateMm, b.PlateLowMm, b.PlateLowTop) = (plating.BulkheadMm, Math.Round(LowStrake * plating.BulkheadMm, 1), lowTop);
                    break;
                default:
                    b.PlateMm = plating.BulkheadMm;
                    break;
            }
        return planked;
    }
}
