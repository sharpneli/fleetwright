using System.Text;

namespace Fleetwright.Shipgen.Render;

/// <summary>The hull image: everything that doesn't rotate (shipgen.build_hull and its helpers).</summary>
public static class HullArt
{
    public const double PadM = 3.0;  // empty margin around each hull sprite, metres

    /// <summary>Half the canvas (x, y) in metres. With a scale, snapped so the canvas is a multiple of align px (even at
    /// least) and the origin lands on an exact pixel.</summary>
    public static (double Hx, double Hy) ShipExtent(TopView v, Hull hull, double? scale = null, int align = 2)
    {
        double hw = hull.B / 2;
        if (v.FlightDeck is { } fd)
            hw = Math.Max(hw, fd.Outline.Max(p => Math.Abs(p.Y)));
        foreach (var (y, w) in v.Sponsons.Where(s => !s.Elevator).Select(s => ((s.Y0 + s.Y1) / 2, s.Y1 - s.Y0))
                     .Concat(v.Blocks.Select(b => (b.Y, b.W))).Concat((v.Dressing.Fittings ?? []).Select(f => (f.Y, f.W))))
            hw = Math.Max(hw, Math.Abs(y) + w / 2);
        foreach (var m in v.Mounts)   // casemate guns stand on the hull side: keep their barrels on the canvas
        {
            var t = v.TurretTypes[m.Type];
            if (t.Shape == "casemate")
                hw = Math.Max(hw, Math.Abs(m.Y) + Geometry.TurretReach(t));
        }
        double hx = hull.L / 2 + PadM, hy = hw + PadM;
        if (scale is double s && s != 0)
        {
            double a = align / 2.0;
            hx = Math.Ceiling(hx * s / a) * a / s;
            hy = Math.Ceiling(hy * s / a) * a / s;
        }
        return (hx, hy);
    }

    /// <summary>Scatter small ventilators and hatches on level-1 deckhouses, avoiding everything else (drawn only when
    /// the look has no clutter kit).</summary>
    static void Vents(List<Node> o, TopView v, Painter P)
    {
        var rng = new ShipRng(v.Id);
        var obstacles = new List<(double X, double Y, double R)>();
        foreach (var m in v.Mounts)
            obstacles.Add((m.X, m.Y, v.TurretTypes[m.Type].R * 1.15));
        foreach (var a in v.AaMounts)
            obstacles.Add((a.X, a.Y, 2.4));
        foreach (var b in v.Dressing.Boats ?? [])
            obstacles.Add((b.X, b.Y, b.L / 2 + 0.6));
        var rects = v.Funnels.Select(fn => (fn.X - fn.L / 2 - 0.8, fn.Y - fn.W / 2 - 0.8, fn.X + fn.L / 2 + 2.5, fn.Y + fn.W / 2 + 2.5)).ToList();
        rects.AddRange(v.Blocks.Where(b => b.Level > 1).Select(b => (b.X0 - 0.8, b.Y - b.W / 2 - 0.8, b.X1 + 2.0, b.Y + b.W / 2 + 2.0)));
        foreach (var blk in v.Blocks.Where(b => b.Level == 1))
        {
            double y0 = blk.Y - blk.W / 2 + 1.2, y1 = blk.Y + blk.W / 2 - 1.2;
            double x0 = blk.X0 + Math.Max(1.2, blk.Rb), x1 = blk.X1 - Math.Max(1.2, blk.Rf);
            long n = (long)((x1 - x0) * (y1 - y0) / 35);
            var pts = blk.Points is { Count: > 0 } ? blk.Points : null;
            for (long it = 0; it < n * 4; it++)
            {
                if (n <= 0)
                    break;
                double x = rng.Uniform(x0, x1), y = rng.Uniform(y0, y1);
                if (obstacles.Any(ob => double.Hypot(x - ob.X, y - ob.Y) < ob.R))
                    continue;
                if (rects.Any(r => r.Item1 <= x && x <= r.Item3 && r.Item2 <= y && y <= r.Item4))
                    continue;
                if (pts != null && !(new[] { -1.2, 1.2 }.All(dx => new[] { -1.2, 1.2 }.All(dy => Geometry.PointInPolygon(x + dx, y + dy, pts)))))
                    continue;
                obstacles.Add((x, y, 1.6));
                n--;
                string col = Painter.Shade(P.P.Colours("levels")[0], 0.78);
                if (rng.Random() < 0.5)
                {
                    o.Add(P.Ln(new CircleNode(x, y, 0.55).Fill(col), 0.6));
                    o.Add(new CircleNode(x + 0.12, y + 0.15, 0.3).Fill("#1d2125"));
                }
                else
                {
                    var (l, w) = rng.Choice([(1.6, 1.0), (1.0, 1.4), (2.2, 1.2)]);
                    o.Add(P.Ln(new RectNode(x - l / 2, y - w / 2, l, w, 0.2).Fill(col), 0.6));
                }
            }
        }
    }

    /// <summary>Footprints (x0, x1, y0, y1) of what stands on deck: turrets, superstructure and funnels; with small,
    /// also AA, boats, masts and fittings. The look features that paint the open deck keep clear of them.</summary>
    static List<(double X0, double X1, double Y0, double Y1)> DeckObstacles(TopView v, bool small = false)
    {
        var result = new List<(double, double, double, double)>();
        foreach (var m in v.Mounts)
        {
            double r = v.TurretTypes[m.Type].R * 1.15;
            result.Add((m.X - r, m.X + r, m.Y - r, m.Y + r));
        }
        foreach (var b in v.Blocks)
            result.Add((b.X0, b.X1, b.Y - b.W / 2, b.Y + b.W / 2));
        foreach (var fn in v.Funnels)
            result.Add((fn.X - fn.L / 2, fn.X + fn.L / 2, fn.Y - fn.W / 2, fn.Y + fn.W / 2));
        if (small)
        {
            var d = v.Dressing;
            foreach (var a in v.AaMounts)
                result.Add((a.X - 2.0, a.X + 2.0, a.Y - 2.0, a.Y + 2.0));
            foreach (var (x, y, l, w) in (d.Boats ?? []).Select(b => (b.X, b.Y, b.L, b.W)).Concat((d.Fittings ?? []).Select(f => (f.X, f.Y, f.L, f.W))))
                result.Add((x - l / 2, x + l / 2, y - w / 2, y + w / 2));
            foreach (var m in v.Masts)
                result.Add((m.X - 1.0, m.X + 1.0, m.Y - 1.0, m.Y + 1.0));
        }
        return result;
    }

    /// <summary>The open foredeck and quarterdeck: (front of the foremost obstacle, back of the aftmost), counting
    /// only obstacles within halfY of the centreline when given.</summary>
    static (double Fwd, double Aft) OpenEnds(TopView v, double? halfY = null, bool small = false)
    {
        var obs = DeckObstacles(v, small).Where(o => halfY is not double h || (o.Y0 < h && o.Y1 > -h)).ToList();
        if (obs.Count == 0)
            return (0.0, 0.0);
        return (obs.Max(o => o.X1), obs.Min(o => o.X0));
    }

    /// <summary>Dazzle camouflage: slanted panels cut across the whole ship, in a repeatable pattern per design.
    /// Panels are clipped to the hull band, the superstructure and the funnels as they're drawn.</summary>
    static List<(List<Pt>, string)> DazzlePanels(TopView v, Hull hull, IReadOnlyList<string> colours)
    {
        var rng = new ShipRng($"{v.Id}/dazzle");
        double L = hull.L, Y = hull.B / 2 + 2.0;
        long n = Math.Max(5L, (long)Math.Round(L / 18));
        double step = L / n;
        var cuts = new List<(double C, double K)> { (-L / 2 - 2.0, 0.0) };
        for (int i = 1; i < n; i++)
        {
            double c = -L / 2 + step * i + rng.Uniform(-0.2, 0.2) * step;
            cuts.Add((c, rng.Uniform(-0.45, 0.45) * step / Y));
        }
        cuts.Add((L / 2 + 2.0, 0.0));
        var result = new List<(List<Pt>, string)>();
        string? last = null;
        for (int i = 0; i + 1 < cuts.Count; i++)
        {
            var ((c0, k0), (c1, k1)) = (cuts[i], cuts[i + 1]);
            var opts = new List<string?> { null };
            opts.AddRange(colours);
            var col = rng.Choice(opts.Where(c => c != last).ToList());
            last = col;
            if (col != null)
                result.Add(([new(c0 - k0 * Y, -Y), new(c1 - k1 * Y, -Y), new(c1 + k1 * Y, Y), new(c0 + k0 * Y, Y)], col));
            if (rng.Random() < 0.5)   // a wedge from one side into the panel, in another colour
            {
                int side = rng.Choice([-1, 1]);
                double mid = (c0 + c1) / 2;
                var wcol = rng.Choice(colours.Where(c => c != col).ToList());
                double tx = mid + rng.Uniform(-0.3, 0.3) * step;
                double ty = side * Y * rng.Uniform(-0.2, 0.4);
                result.Add(([new(mid - 0.35 * step, side * Y), new(mid + 0.35 * step, side * Y), new(tx, ty)], wcol));
            }
        }
        return result;
    }

    /// <summary>zlib.crc32.</summary>
    internal static uint Crc32(byte[] data)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            c ^= b;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }
        return ~c;
    }

    /// <summary>A look's paint and canvas on the open deck: recognition stripes on the forecastle (and quarterdeck)
    /// and peacetime awnings over the quarterdeck; then, separately (drawn over the anchor chains), a hull number on
    /// the foredeck.</summary>
    static (List<Node> Paint, List<Node> Number) DeckPaint(TopView v, Hull hull, Painter P, PathData deckD)
    {
        var sh = P.Shapes;
        double L = hull.L;
        var result = new List<Node>();
        double tip = L / 2 - 0.04 * L;
        bool flight = v.FlightDeck != null;
        if (sh.DeckStripes is { } st && !flight)   // alternating bands, chevrons pointing ahead by default
        {
            var (fwd, aft) = OpenEnds(v);
            long n = st.N ?? 5;
            double k = (st.Pattern ?? "chevron") == "chevron" ? st.Slope ?? 0.8 : 0.0;
            var cols = (st.Colours ?? ["recog_a", "recog_b"]).Select(c => P.P.Or(c, c)).ToList();
            double Y = hull.B / 2;
            var spans = new List<(double X0, double X1, int Dir)> { (fwd + 1.0, tip + 0.04 * L, 1) };
            if (st.Ends == "both")
                spans.Add((-L / 2, aft - 1.0, -1));
            foreach (var (x0, x1, dirn) in spans)
            {
                if (x1 - x0 < 4.0)
                    continue;
                double band = (x1 - x0) / (2 * n);
                var inner = new Group { Clip = [new RectNode(x0, -Y, x1 - x0, 2 * Y)] };
                long count = (long)((x1 - x0 + k * Y) / band) + 4;
                for (long j = 0; j < count; j++)
                {
                    double u0 = (dirn > 0 ? x0 : x1) + dirn * band * (j - 1);
                    double u1 = u0 + dirn * band;
                    List<Pt> pts = k != 0
                        ? [new(u0, 0), new(u0 - dirn * k * Y, -Y), new(u1 - dirn * k * Y, -Y), new(u1, 0), new(u1 - dirn * k * Y, Y), new(u0 - dirn * k * Y, Y)]
                        : [new(u0 - 0.6 * Y, -Y), new(u1 - 0.6 * Y, -Y), new(u1 + 0.6 * Y, Y), new(u0 + 0.6 * Y, Y)];
                    inner.Items.Add(new PathNode(Painter.Poly(pts)).Fill(cols[(int)(j % cols.Count)]));
                }
                var outer = new Group { Clip = [new PathNode(deckD)] };
                outer.Items.Add(inner);
                result.Add(outer);
            }
        }
        if (sh.Awnings == true && !flight)   // canvas on stanchions over the quarterdeck, ridged along the centreline
        {
            var (_, aft) = OpenEnds(v);
            double x0 = -L / 2 + 0.03 * L, x1 = aft - 1.2;
            if (x1 - x0 > 6.0)
            {
                var d = Painter.HullPath(hull, inset: 1.1, xMin: x0, xMax: x1);
                string col = P.P.Or("awning", "#ece7d6");
                result.Add(P.Ln(new PathNode(d).Fill(col), 0.9));
                var g = new Group { Clip = [new PathNode(d)] };
                g.Items.Add(new RectNode(x0, 0, x1 - x0, hull.B).Fill("#000").FillOp(0.1));
                var ribs = new Group().Stroke(Painter.Shade(col, 0.72), P.Sw * 0.8);
                for (long i = 1; i < (long)((x1 - x0) / 2.5) + 1; i++)
                {
                    double x = x0 + 2.5 * i;
                    ribs.Items.Add(new LineNode(x, -hull.B, x, hull.B));
                }
                g.Items.Add(ribs);
                g.Items.Add(new LineNode(x0, 0, x1, 0).Stroke(Painter.Shade(col, 0.65), P.Sw * 1.2));
                result.Add(g);
            }
        }
        var num = new List<Node>();
        if (sh.HullNumber == true && !flight)   // painted big across the foredeck
        {
            string text = !string.IsNullOrEmpty(sh.Number) ? sh.Number : (100 + Crc32(Encoding.UTF8.GetBytes(v.Id)) % 900).ToString();
            double hw = hull.HalfWidth(L / 2 - 0.15 * L);
            double size = 0.75 * 2 * hw / (0.62 * text.Length);
            var (fwd, _) = OpenEnds(v, halfY: 0.62 * text.Length * size / 2, small: true);
            double x1 = L / 2 - 0.12 * L;
            size = Math.Min(size, Math.Min((x1 - fwd) * 0.8, 0.05 * L));
            if (size > 1.2)
            {
                double nx = x1 - size * 0.6;
                string col = P.P.Or("number", P.C("marking"));
                num.Add(new TextNode(nx, 0, size, text).Fill(col).FillOp(0.92).Tr(new Rotate(90, nx, 0, About: true)));
            }
        }
        return (result, num);
    }

    /// <summary>The hull as a look draws it (shapes: bow_power and transom added, bow_flare). Drawing only, and only
    /// ever fuller than the layout's hull, so deck-edge fittings stay on deck; the hitbox keeps the layout's hull.</summary>
    public static HullSpec LookHullSpec(TopView v, Shapes sh)
    {
        var hs = new HullSpec(v.Hull.L, v.Hull.B, v.Hull.Bow, v.Hull.Stern);
        if ((sh.BowPower ?? 0) == 0 && (sh.BowFlare ?? 0) == 0 && (sh.Transom ?? 0) == 0)
            return hs;
        var hull = new Hull(hs);
        return hs with
        {
            Bow = hull.Bow with { Power = hull.Bow.Power!.Value + (sh.BowPower ?? 0.0), Flare = sh.BowFlare ?? 0.0 },
            Stern = hull.Stern with { Transom = Math.Min(0.9, hull.Stern.Transom!.Value + (sh.Transom ?? 0.0)) },
        };
    }

    /// <summary>What build_hull returns: the drawing, the hull as drawn and the clutter items (for the height
    /// map).</summary>
    public sealed record Result(Scene Scene, Hull Hull, List<Clutter.Item> Clutter);

    public static Result Build(TopView v, Palette pal, Shapes sh, double scale, int align = 2)
    {
        var spec = v.Dressing;
        var P = new Painter(pal, scale, sh);
        var lookSpec = LookHullSpec(v, sh);
        var hull = new Hull(lookSpec);
        var (hx, hy) = ShipExtent(v, hull, scale, align);
        var scene = new Scene(-hx, -hy, 2 * hx, 2 * hy, scale);
        // tumblehome: the hull's sides bulge out below a narrower deck, seen from above as a wide band round the deck
        var outer = sh.Tumblehome is double th && th != 0 ? new Hull(lookSpec with { Beam = hull.B * (1 + th) }) : hull;
        var hullD = Painter.HullPath(outer);
        if (sh.Dazzle == true && pal.Has("camo"))
            P.Dazzle = DazzlePanels(v, hull, pal.Colours("camo"));

        // two passes in one image: low (hull, deck, level 1, what stands on the deck), then high (what stands on a
        // roof, and the tall stuff), so a roof never draws over what stands on it
        var low = new List<Node>();
        var high = new List<Node>();
        void Add(List<Node> o, Node? n)
        {
            if (n != null)
                o.Add(n);
        }

        // --- hull and deck -------------------------------------------------------
        low.Add(P.Ln(new PathNode(hullD).Fill(P.C("hull")), 1.4));
        Add(low, P.Dazzled(hullD));
        double inset = spec.DeckInset ?? 0.55;
        var deckD = Painter.HullPath(hull, inset: inset);
        bool wood = spec.Deck == "wood";
        string deckCol = wood ? P.C("wood") : P.C("deck");
        low.Add(new PathNode(deckD).Fill(deckCol));
        Add(low, P.Dazzled(deckD, sh.DazzleDecks ?? 0.0));   // painted decks, under the planking lines

        // planking / plating lines
        double spacing = spec.PlankSpacing ?? 1.25;
        double seam = wood ? 9.0 : 6.0;

        List<Node> PlankLines(double x0, double x1, double yOff = 0.0, double dashOff = 0.0)
        {
            // planks (or plates) along the deck from x0 to x1, their seams across it from x0 on: yOff shifts the
            // planks across, dashOff the butt joints along each seam
            var o = new List<Node>();
            double yy = -hull.B / 2 + yOff;
            while (yy < hull.B / 2)
            {
                o.Add(new LineNode(x0, yy, x1, yy));
                yy += spacing;
            }
            double xx = x0;
            while (xx < x1)
            {
                var ln = new LineNode(xx, -hull.B / 2, xx, hull.B / 2).Dash(spacing, spacing * 2);
                if (dashOff != 0)
                    ln.DashOff(dashOff);
                o.Add(ln);
                xx += seam;
            }
            return o;
        }

        var lines = PlankLines(-hull.L / 2, hull.L / 2);
        string lineCol = wood ? P.C("deck_line") : P.P.Or("steel_line", P.C("deck_line"));
        // a margin plank round the deck's edge (every deck has one, the raised ones too): the planking stops at it
        double margin = 2 * spacing / 3;
        var mainMarginD = Painter.HullPath(hull, inset: inset + margin);
        double lineOp = Math.Round(sh.DeckLineOpacity ?? 0.45, 3);
        low.Add(new PathNode(mainMarginD).Fill("none").Stroke(lineCol, P.Sw * 0.7).StrokeOp(lineOp));
        var planks = new Group(lines) { Clip = [new PathNode(mainMarginD)] }.Stroke(lineCol, P.Sw * 0.7).StrokeOp(lineOp);
        low.Add(planks);

        // raised decks (forecastle, bridge deck, poop): the hull outline between x0 and x1, a step up, lighter per
        // deck. Each is laid as its own deck: planks shifted half a plank on alternate levels, butt seams from its
        // own end, and a margin plank round its edge, so the texture never runs on across a break
        const double rdIn = 0.3;
        foreach (var rd in v.RaisedDecks)
        {
            long lv = rd.Levels;
            var rdD = Painter.HullPath(hull, inset: rdIn, xMin: rd.X0, xMax: rd.X1);
            var marginD = Painter.HullPath(hull, inset: rdIn + margin, xMin: rd.X0 + margin, xMax: rd.X1 - (rd.X1 < hull.L / 2 - 0.5 ? margin : 0.0));
            var rdLines = PlankLines(rd.X0, rd.X1, lv % 2 != 0 ? spacing / 2 : 0.0, 1.5 * spacing * lv);
            low.Add(P.Ln(new PathNode(rdD).Fill(Painter.Shade(deckCol, Math.Pow(1.07, lv)))));
            Add(low, P.Dazzled(rdD, sh.DazzleDecks ?? 0.0));
            low.Add(new PathNode(marginD).Fill("none").Stroke(lineCol, P.Sw * 0.7).StrokeOp(0.45));
            low.Add(new Group(rdLines) { Clip = [new PathNode(marginD)] }.Stroke(lineCol, P.Sw * 0.7).StrokeOp(0.45));
        }
        foreach (var ht in spec.Hatches ?? [])
            P.Hatch(low, ht);
        var (paint, number) = DeckPaint(v, hull, P, deckD);
        low.AddRange(paint);

        // bow details: anchor chains, breakwater, bollards
        if (spec.ChainX is double bx && bx != 0)
        {
            double xHaw = hull.L / 2 - (spec.HawseBack ?? 6.0);
            double hwHaw = hull.HalfWidth(xHaw) - 0.6;
            foreach (int side in new[] { -1, 1 })
            {
                low.Add(new LineNode(bx, side * 1.6, xHaw, side * hwHaw).Stroke(P.C("chain"), Math.Max(0.45, P.Sw * 2)).Dash(0.45, 0.25));
                low.Add(P.Ln(new CircleNode(bx, side * 1.6, 0.9).Fill(P.C("fitting"))));
                double px = xHaw - 1.2, py = side * (hwHaw + 0.2);
                low.Add(new PathNode(new PathData().M(px, py).L(px + 1.6, py + side * 0.6).L(px + 2.2, py + side * 0.6 - side * 0.9).Z())
                    .Fill(P.C("chain")));
            }
        }
        if (spec.BreakwaterX is double bw && bw != 0)
        {
            double hwb = hull.HalfWidth(bw) - 1.2;
            low.Add(new PathNode(new PathData().M(bw - hwb * 0.55, -hwb).L(bw, 0).L(bw - hwb * 0.55, hwb)).Fill("none")
                .Stroke(Painter.Shade(deckCol, 0.6), Math.Max(0.45, P.Sw * 2.2)));
        }
        foreach (double bxx in spec.Bollards ?? [])
        {
            foreach (int side in new[] { -1, 1 })
            {
                double by = side * (hull.HalfWidth(bxx) - 1.1);
                low.Add(P.Ln(new CircleNode(bxx, by, 0.35).Fill(P.C("fitting")), 0.6));
                low.Add(P.Ln(new CircleNode(bxx + 0.9, by, 0.35).Fill(P.C("fitting")), 0.6));
            }
        }

        low.AddRange(number);   // over the chains, so it stays readable

        // --- aircraft carrier flight deck ---------------------------------------
        if (v.FlightDeck is { } fdp && spec.FlightDeck is { } fd)
        {
            foreach (var sp in v.Sponsons.Where(s => !s.Elevator))
                P.Fitting(low, (sp.X0 + sp.X1) / 2, (sp.Y0 + sp.Y1) / 2, sp.X1 - sp.X0, sp.Y1 - sp.Y0, P.C("deck"), 0.8);
            foreach (var el in v.Sponsons.Where(s => s.Elevator))
                P.Fitting(low, (el.X0 + el.X1) / 2, (el.Y0 + el.Y1) / 2, el.X1 - el.X0, el.Y1 - el.Y0, P.C("flight_deck"), 0.4);
            var fdD = Painter.Poly(fdp.Outline);
            low.Add(P.Ln(new PathNode(fdD).Fill(P.C("flight_deck")), 1.3));
            var pk = fd.Planks;
            var fl = new Group { Clip = [new PathNode(fdD)] }.Stroke(Painter.Shade(P.C("flight_deck"), 0.75), P.Sw * 0.7).StrokeOp(0.6);
            double yy = pk.Y0;
            while (yy < pk.Y1)
            {
                fl.Items.Add(new LineNode(pk.X0, yy, pk.X1, yy));
                yy += pk.Step;
            }
            low.Add(fl);
            string mk = P.C("marking");
            var g = new Group { Clip = [new PathNode(fdD)] };
            foreach (var el in fd.Elevators)   // elevators
                g.Items.Add(new RectNode(el.X - el.L / 2, el.Y - el.W / 2, el.L, el.W).Fill(Painter.Shade(P.C("flight_deck"), 0.88))
                    .Stroke(Painter.Shade(P.C("flight_deck"), 0.55), P.Sw * 1.2));
            foreach (var w in fd.Wires)   // arresting wires
                g.Items.Add(new LineNode(w[0], w[1], w[2], w[3]).Stroke(Painter.Shade(mk, 0.75), Math.Max(0.25, P.Sw)).StrokeOp(0.8));
            // painted lines: centreline dashes, deck-edge stripes, ramp stripes, catapult tracks
            foreach (var ln in fd.Marks)
            {
                var l = new LineNode(ln.X1, ln.Y1, ln.X2, ln.Y2).Stroke(P.C(ln.Color), ln.Width);
                if (!string.IsNullOrEmpty(ln.Dash))
                    l.Dash([.. ln.Dash.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => double.Parse(t, System.Globalization.CultureInfo.InvariantCulture))]);
                if (ln.Opacity is double op)
                    l.StrokeOp(op);
                g.Items.Add(l);
            }
            if (fd.Number is { } num)
                g.Items.Add(new TextNode(num.X, num.Y, num.Size, num.Text).Fill(mk).Tr(new Rotate(90, num.X, num.Y, About: true)));
            low.Add(g);
        }

        // --- superstructure, fittings, AA, boats --------------------------------
        // a look's clutter kit (Clutter): roof finishes drawn with each block, the gear itself after all blocks
        var kit = Clutter.Kit(sh.Clutter);
        var items = kit != null ? Clutter.Plan(v, sh) : [];
        double roofPlanks = kit != null ? sh.RoofPlanks ?? kit.RoofPlanks : 0;
        bool rails = kit != null && (sh.RoofRails ?? kit.RoofRails);
        foreach (var sb in v.Blocks)
        {
            var o = sb.Upper ? high : low;
            P.Block(o, sb);
            if (kit != null && sb.Director == null)
                Clutter.RoofFinish(o, sb, P, roofPlanks, rails, P.C("wood"));
        }
        if (kit != null)
        {
            var levels = pal.Colours("levels");
            foreach (var it in items)
            {
                string col = it.Level == 0 ? deckCol : Painter.At(levels, Math.Min(it.Level, levels.Count) - 1);
                Clutter.Draw(it.Level <= 1 ? low : high, it, P, col);
            }
        }
        else
            Vents(low, v, P);
        foreach (var ft in spec.Fittings ?? [])
            P.Fitting(low, ft);
        foreach (var a in v.AaMounts)
            P.Aa(a.Upper ? high : low, a);
        foreach (var b in spec.Boats ?? [])
            P.Boat(high, b);

        // --- turret barbettes (low pass) ------------------------------------------
        foreach (var b in v.Barbettes)
            P.Barbette(low, b.X, b.Y, b.R);

        // --- tall stuff ----------------------------------------------------------
        foreach (var fn in v.Funnels)
            P.Funnel(high, fn);
        foreach (var m in v.Masts)
            P.Mast(high, m);
        foreach (var c in spec.Cranes ?? [])
            P.Crane(high, c);

        scene.Root.Items.AddRange(low);
        scene.Root.Items.AddRange(high);
        return new Result(scene, hull, items);
    }
}
