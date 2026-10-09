using System.Text;

namespace Fleetwright.Shipgen.Render;

/// <summary>The hull image: everything that doesn't rotate (shipgen.build_hull and its helpers).</summary>
public static class HullArt
{
    public const double PAD_M = 3.0;  // empty margin around each hull sprite, metres

    static IEnumerable<PyDict> Items(PyDict spec, string key) =>
        (spec.Get(key) as List<object?> ?? []).Cast<PyDict>();

    static object? Neg(object? v) => v switch
    {
        long l => -l,
        double d => -d,
        _ => -Py.ToDouble(v),
    };

    /// <summary>Spec expansion: mirroring and edge-relative placement.</summary>
    public static List<PyDict> Expand(IEnumerable<PyDict> items, Hull hull)
    {
        var out_ = new List<PyDict>();
        foreach (var it0 in items)
        {
            var it = it0.Copy();
            if (it.Has("edge"))   // place 'edge' metres inboard from the hull side
            {
                int side = it.F("y", 1) >= 0 ? 1 : -1;
                it["y"] = side * (hull.HalfWidth(it.F("x")) - it.F("edge"));
            }
            if (it.B("mirror") && Math.Abs(it.F("y", 0)) > 1e-6)
            {
                var a = it.Copy();
                var b = it.Copy();
                b["y"] = Neg(a["y"]);
                foreach (var k in new[] { "dir", "rest" })
                    if (b.Has(k))
                        b[k] = Neg(b[k]);
                if (it.Has("id"))
                {
                    a["id"] = it.S("id") + (a.F("y") > 0 ? "S" : "P");
                    b["id"] = it.S("id") + (b.F("y") > 0 ? "S" : "P");
                }
                out_.Add(a);
                out_.Add(b);
            }
            else
                out_.Add(it);
        }
        return out_;
    }

    public static List<PyDict> Expand(PyDict spec, string key, Hull hull) => Expand(Items(spec, key), hull);

    /// <summary>The spec's turret types (the layout generates every type a design uses).</summary>
    public static PyDict TurretTypes(PyDict spec) => spec.Get("turret_types") as PyDict ?? new PyDict();

    /// <summary>Flight deck as drawn: outline points, plank lines, elevators, wires (segments), painted marks and the
    /// hull number. Layouts give this directly; the hand-authored short form (x0, x1, half_width, bow_taper, wires
    /// as x positions) is expanded here into an axial deck.</summary>
    public static PyDict FlightDeckSpec(PyDict fd)
    {
        if (fd.Has("points"))
            return fd;
        double x0 = fd.F("x0"), x1 = fd.F("x1"), hw = fd.F("half_width"), tap = fd.F("bow_taper", 14);
        var marks = new List<object?>
        {
            PyDict.Of(("x1", x0 + 4), ("y1", 0L), ("x2", x1 - 4), ("y2", 0L), ("color", "marking"), ("width", 0.5), ("dash", "5 5")),
        };
        foreach (int side in new[] { -1, 1 })
            marks.Add(PyDict.Of(("x1", x0 + 1.5), ("y1", side * (hw - 1.0)), ("x2", x1 - tap), ("y2", side * (hw - 1.0)),
                ("color", "marking"), ("width", 0.35), ("opacity", 0.85)));
        for (int i = 0; i < 6; i++)   // stern ramp stripes
        {
            double sx = x0 + 0.8 + i * 1.6;
            marks.Add(PyDict.Of(("x1", sx), ("y1", -hw + 1.5), ("x2", sx), ("y2", hw - 1.5), ("color", "stripe"), ("width", 0.7),
                ("opacity", i % 2 == 0 ? 0.9 : 0L)));
        }
        return PyDict.Of(
            ("points", new List<Pt> { new(x0, -hw), new(x1 - tap, -hw), new(x1, -hw * 0.42), new(x1, hw * 0.42), new(x1 - tap, hw), new(x0, hw) }),
            ("planks", PyDict.Of(("x0", x0), ("x1", x1), ("y0", -hw), ("y1", hw), ("step", 1.4))),
            ("elevators", fd.Get("elevators", new List<object?>())), ("edge_elevators", fd.Get("edge_elevators", new List<object?>())),
            ("wires", (fd.Get("wires") as List<object?> ?? []).Select(wx => (object?)Py.List(Py.ToDouble(wx), -hw + 2, Py.ToDouble(wx), hw - 2)).ToList()),
            ("marks", marks),
            ("number", Py.Truthy(fd.Get("number")) ? PyDict.Of(("x", x1 - tap - 14), ("y", 0L), ("text", fd["number"])) : null));
    }

    /// <summary>Half the canvas (x, y) in metres. With a scale, snapped so the canvas is a multiple of align px (even at
    /// least) and the origin lands on an exact pixel.</summary>
    public static (double Hx, double Hy) ShipExtent(PyDict spec, Hull hull, double? scale = null, int align = 2)
    {
        double hw = hull.B / 2;
        if (spec.Get("flight_deck") is PyDict fd && Py.Truthy(fd))
            hw = Math.Max(hw, Geometry.Pts(FlightDeckSpec(fd)["points"]).Select(p => Math.Abs(p.Y)).Max());
        foreach (var key in new[] { "sponsons", "superstructure", "fittings" })
            foreach (var it in Expand(spec, key, hull))
                hw = Math.Max(hw, Math.Abs(it.F("y", 0)) + it.F("w", 0) / 2);
        var types = TurretTypes(spec);
        foreach (var m in Items(spec, "turrets"))   // casemate guns stand on the hull side: keep their barrels on the canvas
        {
            var t = types.D(m.S("type"));
            if (t.S("shape", null) == "casemate")
                hw = Math.Max(hw, Math.Abs(m.F("y", 0)) + Geometry.TurretReach(t));
        }
        double hx = hull.L / 2 + PAD_M, hy = hw + PAD_M;
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
    static void Vents(List<Node> o, PyDict spec, Hull hull, Painter P)
    {
        var rng = new ShipRng(spec.S("id"));
        var types = TurretTypes(spec);
        var obstacles = new List<(double X, double Y, double R)>();
        foreach (var m in Expand(spec, "turrets", hull))
            obstacles.Add((m.F("x"), m.F("y", 0), types.D(m.S("type")).F("r") * 1.15));
        foreach (var a in Expand(spec, "aa", hull))
            obstacles.Add((a.F("x"), a.F("y"), 2.4));
        foreach (var b in Expand(spec, "boats", hull))
            obstacles.Add((b.F("x"), b.F("y"), b.F("l", 7) / 2 + 0.6));
        var rects = Expand(spec, "funnels", hull).Select(fn => (fn.F("x") - fn.F("l") / 2 - 0.8, fn.F("y", 0) - fn.F("w") / 2 - 0.8,
            fn.F("x") + fn.F("l") / 2 + 2.5, fn.F("y", 0) + fn.F("w") / 2 + 2.5)).ToList();
        rects.AddRange(Expand(spec, "superstructure", hull).Where(b => b.I("level", 1) > 1).Select(b =>
            (b.F("x0") - 0.8, b.F("y", 0) - b.F("w") / 2 - 0.8, b.F("x1") + 2.0, b.F("y", 0) + b.F("w") / 2 + 2.0)));
        foreach (var blk in Expand(spec, "superstructure", hull))
        {
            if (blk.I("level", 1) != 1 || !Py.Truthy(blk.Get("vents", true)))
                continue;
            double y0 = blk.F("y", 0) - blk.F("w") / 2 + 1.2, y1 = blk.F("y", 0) + blk.F("w") / 2 - 1.2;
            double x0 = blk.F("x0") + Math.Max(1.2, blk.F("rb", 0)), x1 = blk.F("x1") - Math.Max(1.2, blk.F("rf", 0));
            long n = (long)((x1 - x0) * (y1 - y0) / 35);
            var pts = blk.B("points") ? Geometry.Pts(blk["points"]) : null;
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
                string col = Painter.Shade((string)P.P.L("levels")[0]!, 0.78);
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
    static List<(double X0, double X1, double Y0, double Y1)> DeckObstacles(PyDict spec, Hull hull, bool small = false)
    {
        var types = TurretTypes(spec);
        var out_ = new List<(double, double, double, double)>();
        foreach (var m in Expand(spec, "turrets", hull))
        {
            double r = types.D(m.S("type")).F("r") * 1.15;
            out_.Add((m.F("x") - r, m.F("x") + r, m.F("y", 0) - r, m.F("y", 0) + r));
        }
        foreach (var b in Expand(spec, "superstructure", hull))
            out_.Add((b.F("x0"), b.F("x1"), b.F("y", 0) - b.F("w") / 2, b.F("y", 0) + b.F("w") / 2));
        foreach (var fn in Expand(spec, "funnels", hull))
            out_.Add((fn.F("x") - fn.F("l") / 2, fn.F("x") + fn.F("l") / 2, fn.F("y", 0) - fn.F("w") / 2, fn.F("y", 0) + fn.F("w") / 2));
        if (small)
        {
            foreach (var a in Expand(spec, "aa", hull))
                out_.Add((a.F("x") - 2.0, a.F("x") + 2.0, a.F("y") - 2.0, a.F("y") + 2.0));
            foreach (var it in Expand(spec, "boats", hull).Concat(Expand(spec, "fittings", hull)))
            {
                double l = it.F("l", 7), w = it.F("w", 2.2);
                out_.Add((it.F("x") - l / 2, it.F("x") + l / 2, it.F("y") - w / 2, it.F("y") + w / 2));
            }
            foreach (var m in Expand(spec, "masts", hull))
                out_.Add((m.F("x") - 1.0, m.F("x") + 1.0, m.F("y", 0) - 1.0, m.F("y", 0) + 1.0));
        }
        return out_;
    }

    /// <summary>The open foredeck and quarterdeck: (front of the foremost obstacle, back of the aftmost), counting
    /// only obstacles within halfY of the centreline when given.</summary>
    static (double Fwd, double Aft) OpenEnds(PyDict spec, Hull hull, double? halfY = null, bool small = false)
    {
        var obs = DeckObstacles(spec, hull, small).Where(o => halfY is not double h || (o.Y0 < h && o.Y1 > -h)).ToList();
        if (obs.Count == 0)
            return (0.0, 0.0);
        return (obs.Select(o => o.X1).Max(), obs.Select(o => o.X0).Min());
    }

    /// <summary>Dazzle camouflage: slanted panels cut across the whole ship, in a repeatable pattern per design.
    /// Panels are clipped to the hull band, the superstructure and the funnels as they're drawn.</summary>
    static List<(List<Pt>, string)> DazzlePanels(PyDict spec, Hull hull, List<string> colours)
    {
        var rng = new ShipRng($"{spec.S("id")}/dazzle");
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
        var out_ = new List<(List<Pt>, string)>();
        string? last = null;
        for (int i = 0; i + 1 < cuts.Count; i++)
        {
            var ((c0, k0), (c1, k1)) = (cuts[i], cuts[i + 1]);
            var opts = new List<string?> { null };
            opts.AddRange(colours);
            var col = rng.Choice(opts.Where(c => c != last).ToList());
            last = col;
            if (col != null)
                out_.Add(([new(c0 - k0 * Y, -Y), new(c1 - k1 * Y, -Y), new(c1 + k1 * Y, Y), new(c0 + k0 * Y, Y)], col));
            if (rng.Random() < 0.5)   // a wedge from one side into the panel, in another colour
            {
                int side = rng.Choice([-1, 1]);
                double mid = (c0 + c1) / 2;
                var wcol = rng.Choice(colours.Where(c => c != col).ToList());
                double tx = mid + rng.Uniform(-0.3, 0.3) * step;
                double ty = side * Y * rng.Uniform(-0.2, 0.4);
                out_.Add(([new(mid - 0.35 * step, side * Y), new(mid + 0.35 * step, side * Y), new(tx, ty)], wcol));
            }
        }
        return out_;
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
    static (List<Node> Paint, List<Node> Number) DeckPaint(PyDict spec, Hull hull, Painter P, PathData deckD)
    {
        var sh = P.Shapes;
        double L = hull.L;
        var out_ = new List<Node>();
        double tip = L / 2 - 0.04 * L;
        bool flight = Py.Truthy(spec.Get("flight_deck"));
        if (sh.Get("deck_stripes") is PyDict st && Py.Truthy(st) && !flight)   // alternating bands, chevrons pointing ahead by default
        {
            var (fwd, aft) = OpenEnds(spec, hull);
            long n = st.I("n", 5);
            double k = st.S("pattern", "chevron") == "chevron" ? st.F("slope", 0.8) : 0.0;
            var cols = (st.Get("colours") as List<object?> ?? ["recog_a", "recog_b"])
                .Select(c => P.P.TryGet((string)c!, out var v) ? (string)v! : (string)c!).ToList();
            double Y = hull.B / 2;
            var spans = new List<(double X0, double X1, int Dir)> { (fwd + 1.0, tip + 0.04 * L, 1) };
            if (st.S("ends", null) == "both")
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
                out_.Add(outer);
            }
        }
        if (Py.Truthy(sh.Get("awnings")) && !flight)   // canvas on stanchions over the quarterdeck, ridged along the centreline
        {
            var (_, aft) = OpenEnds(spec, hull);
            double x0 = -L / 2 + 0.03 * L, x1 = aft - 1.2;
            if (x1 - x0 > 6.0)
            {
                var d = Painter.HullPath(hull, inset: 1.1, xMin: x0, xMax: x1);
                string col = P.P.TryGet("awning", out var aw) ? (string)aw! : "#ece7d6";
                out_.Add(P.Ln(new PathNode(d).Fill(col), 0.9));
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
                out_.Add(g);
            }
        }
        var num = new List<Node>();
        if (Py.Truthy(sh.Get("hull_number")) && !flight)   // painted big across the foredeck
        {
            string text = Py.Truthy(sh.Get("number")) ? Py.Str(sh["number"])
                : (100 + Crc32(Encoding.UTF8.GetBytes(spec.S("id"))) % 900).ToString();
            double hw = hull.HalfWidth(L / 2 - 0.15 * L);
            double size = 0.75 * 2 * hw / (0.62 * text.Length);
            var (fwd, _) = OpenEnds(spec, hull, halfY: 0.62 * text.Length * size / 2, small: true);
            double x1 = L / 2 - 0.12 * L;
            size = Math.Min(size, Math.Min((x1 - fwd) * 0.8, 0.05 * L));
            if (size > 1.2)
            {
                double nx = x1 - size * 0.6;
                string col = P.P.TryGet("number", out var nc) ? (string)nc! : P.C("marking");
                num.Add(new TextNode(nx, 0, size, text).Fill(col).FillOp(0.92).Tr(new Rotate(90, nx, 0, About: true)));
            }
        }
        return (out_, num);
    }

    /// <summary>The hull as a look draws it (shapes: bow_power and transom added, bow_flare). Drawing only, and only
    /// ever fuller than the layout's hull, so deck-edge fittings stay on deck; the hitbox keeps the layout's hull.</summary>
    public static PyDict LookHullSpec(PyDict spec)
    {
        var sh = spec.Get("shapes") as PyDict ?? new PyDict();
        if (!new[] { "bow_power", "bow_flare", "transom" }.Any(k => Py.Truthy(sh.Get(k))))
            return spec;
        var hull = new Hull(spec);
        var bow = PyDict.Merge(hull.Bow, PyDict.Of(("power", hull.Bow.F("power") + sh.F("bow_power", 0.0)),
            ("flare", sh.Get("bow_flare", 0.0))));
        var stern = PyDict.Merge(hull.Stern, PyDict.Of(("transom", Math.Min(0.9, hull.Stern.F("transom") + sh.F("transom", 0.0)))));
        return PyDict.Merge(spec, PyDict.Of(("bow", bow), ("stern", stern)));
    }

    /// <summary>What build_hull returns: the drawing, the turret mounts (expanded), the hull as drawn and the clutter
    /// items (for the height map).</summary>
    public sealed record Result(Scene Scene, List<PyDict> Mounts, Hull Hull, List<Clutter.Item> Clutter);

    public static Result Build(PyDict spec, double scale, int align = 2)
    {
        var pal = PyDict.Merge(Looks.DEFAULT_PALETTE, spec.Get("palette") as PyDict);
        var P = new Painter(pal, scale, spec.Get("shapes") as PyDict);
        var sh = P.Shapes;
        var lookSpec = LookHullSpec(spec);
        var hull = new Hull(lookSpec);
        var (hx, hy) = ShipExtent(spec, hull, scale, align);
        var scene = new Scene(-hx, -hy, 2 * hx, 2 * hy, scale);
        // tumblehome: the hull's sides bulge out below a narrower deck, seen from above as a wide band round the deck
        var outer = Py.Truthy(sh.Get("tumblehome"))
            ? new Hull(PyDict.Merge(lookSpec, PyDict.Of(("beam", hull.B * (1 + sh.F("tumblehome"))))))
            : hull;
        var hullD = Painter.HullPath(outer);
        if (Py.Truthy(sh.Get("dazzle")) && Py.Truthy(pal.Get("camo")))
            P.Dazzle = DazzlePanels(spec, hull, [.. pal.L("camo").Cast<string>()]);

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
        double inset = spec.F("deck_inset", 0.55);
        double? mhw = spec.Get("deck_max_hw") is { } mh ? Py.ToDouble(mh) : null;
        double? dx0 = spec.Get("deck_x0") is { } a0 ? Py.ToDouble(a0) : null;
        double? dx1 = spec.Get("deck_x1") is { } a1 ? Py.ToDouble(a1) : null;
        var deckD = Painter.HullPath(hull, inset: inset, maxHw: mhw, xMin: dx0, xMax: dx1);
        bool wood = spec.S("deck", null) == "wood";
        string deckCol = wood ? P.C("wood") : P.C("deck");
        low.Add(new PathNode(deckD).Fill(deckCol));
        Add(low, P.Dazzled(deckD, P.ShF("dazzle_decks", 0.0)));   // painted decks, under the planking lines

        // planking / plating lines
        double spacing = spec.F("plank_spacing", 1.25);
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
        string lineCol = wood ? P.C("deck_line") : P.P.TryGet("steel_line", out var sl) ? (string)sl! : P.C("deck_line");
        // a margin plank round the deck's edge (every deck has one, the raised ones too): the planking stops at it
        double margin = 2 * spacing / 3;
        var mainMarginD = Painter.HullPath(hull, inset: inset + margin,
            maxHw: mhw is double m1 && m1 != 0 ? m1 - margin : null,
            xMin: dx0 is double v0 ? v0 + margin : null, xMax: dx1 is double v1 ? v1 - margin : null);
        double lineOp = Math.Round(P.ShF("deck_line_opacity", 0.45), 3);
        low.Add(new PathNode(mainMarginD).Fill("none").Stroke(lineCol, P.Sw * 0.7).StrokeOp(lineOp));
        var planks = new Group(lines) { Clip = [new PathNode(mainMarginD)] }.Stroke(lineCol, P.Sw * 0.7).StrokeOp(lineOp);
        low.Add(planks);

        // raised decks (forecastle, bridge deck, poop): the hull outline between x0 and x1, a step up, lighter per
        // deck. Each is laid as its own deck: planks shifted half a plank on alternate levels, butt seams from its
        // own end, and a margin plank round its edge, so the texture never runs on across a break
        foreach (var rd in Items(spec, "raised_decks"))
        {
            long lv = rd.I("levels", 1);
            double rdIn = rd.F("inset", 0.3);
            var rdD = Painter.HullPath(hull, inset: rdIn, xMin: rd.F("x0"), xMax: rd.F("x1"));
            var marginD = Painter.HullPath(hull, inset: rdIn + margin, xMin: rd.F("x0") + margin,
                xMax: rd.F("x1") - (rd.F("x1") < hull.L / 2 - 0.5 ? margin : 0.0));
            var rdLines = PlankLines(rd.F("x0"), rd.F("x1"), lv % 2 != 0 ? spacing / 2 : 0.0, 1.5 * spacing * lv);
            low.Add(P.Ln(new PathNode(rdD).Fill(Painter.Shade(deckCol, Math.Pow(1.07, lv)))));
            Add(low, P.Dazzled(rdD, P.ShF("dazzle_decks", 0.0)));
            low.Add(new PathNode(marginD).Fill("none").Stroke(lineCol, P.Sw * 0.7).StrokeOp(0.45));
            low.Add(new Group(rdLines) { Clip = [new PathNode(marginD)] }.Stroke(lineCol, P.Sw * 0.7).StrokeOp(0.45));
        }
        foreach (var ht in Items(spec, "hatches"))
            P.Hatch(low, ht);
        var (paint, number) = DeckPaint(spec, hull, P, deckD);
        low.AddRange(paint);

        // bow details: anchor chains, breakwater, bollards
        if (Py.Truthy(spec.Get("chain_x")))
        {
            double bx = spec.F("chain_x");
            double xHaw = hull.L / 2 - spec.F("hawse_back", 6.0);
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
        if (Py.Truthy(spec.Get("breakwater_x")))
        {
            double bw = spec.F("breakwater_x");
            double hwb = hull.HalfWidth(bw) - 1.2;
            low.Add(new PathNode(new PathData().M(bw - hwb * 0.55, -hwb).L(bw, 0).L(bw - hwb * 0.55, hwb)).Fill("none")
                .Stroke(Painter.Shade(deckCol, 0.6), Math.Max(0.45, P.Sw * 2.2)));
        }
        foreach (var bxo in spec.Get("bollards") as List<object?> ?? [])
        {
            double bxx = Py.ToDouble(bxo);
            foreach (int side in new[] { -1, 1 })
            {
                double by = side * (hull.HalfWidth(bxx) - 1.1);
                low.Add(P.Ln(new CircleNode(bxx, by, 0.35).Fill(P.C("fitting")), 0.6));
                low.Add(P.Ln(new CircleNode(bxx + 0.9, by, 0.35).Fill(P.C("fitting")), 0.6));
            }
        }

        low.AddRange(number);   // over the chains, so it stays readable

        // --- aircraft carrier flight deck ---------------------------------------
        if (spec.Get("flight_deck") is PyDict fd0 && Py.Truthy(fd0))
        {
            foreach (var sp in Expand(spec, "sponsons", hull))
                P.Fitting(low, PyDict.Merge(sp, PyDict.Of(("color", P.C("deck")), ("r", 0.8))));
            var fd = FlightDeckSpec(fd0);
            foreach (var el in (fd.Get("edge_elevators") as List<object?> ?? []).Cast<PyDict>())
                P.Fitting(low, PyDict.Merge(el, PyDict.Of(("color", P.C("flight_deck")), ("r", 0.4))));
            var fdD = Painter.Poly(Geometry.Pts(fd["points"]));
            low.Add(P.Ln(new PathNode(fdD).Fill(P.C("flight_deck")), 1.3));
            var pk = fd.D("planks");
            var fl = new Group { Clip = [new PathNode(fdD)] }.Stroke(Painter.Shade(P.C("flight_deck"), 0.75), P.Sw * 0.7).StrokeOp(0.6);
            double yy = pk.F("y0");
            while (yy < pk.F("y1"))
            {
                fl.Items.Add(new LineNode(pk.F("x0"), yy, pk.F("x1"), yy));
                yy += pk.F("step");
            }
            low.Add(fl);
            string mk = P.C("marking");
            var g = new Group { Clip = [new PathNode(fdD)] };
            foreach (var el in (fd.Get("elevators") as List<object?> ?? []).Cast<PyDict>())   // elevators
            {
                double ex = el.F("x"), ey = el.F("y", 0), elL = el.F("l"), elW = el.F("w");
                g.Items.Add(new RectNode(ex - elL / 2, ey - elW / 2, elL, elW).Fill(Painter.Shade(P.C("flight_deck"), 0.88))
                    .Stroke(Painter.Shade(P.C("flight_deck"), 0.55), P.Sw * 1.2));
            }
            foreach (var wr in fd.Get("wires") as List<object?> ?? [])   // arresting wires
            {
                var w = (System.Collections.IList)wr!;
                g.Items.Add(new LineNode(Py.ToDouble(w[0]), Py.ToDouble(w[1]), Py.ToDouble(w[2]), Py.ToDouble(w[3]))
                    .Stroke(Painter.Shade(mk, 0.75), Math.Max(0.25, P.Sw)).StrokeOp(0.8));
            }
            // painted lines: centreline dashes, deck-edge stripes, ramp stripes, catapult tracks
            foreach (var ln in (fd.Get("marks") as List<object?> ?? []).Cast<PyDict>())
            {
                var l = new LineNode(ln.F("x1"), ln.F("y1"), ln.F("x2"), ln.F("y2")).Stroke(P.C(ln.S("color")), ln.F("width"));
                if (Py.Truthy(ln.Get("dash")))
                    l.Dash([.. ln.S("dash").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => double.Parse(t, System.Globalization.CultureInfo.InvariantCulture))]);
                if (ln.Has("opacity"))
                    l.StrokeOp(ln.F("opacity"));
                g.Items.Add(l);
            }
            if (fd.Get("number") is PyDict num && Py.Truthy(num))
            {
                double nx = num.F("x"), ny = num.F("y");
                g.Items.Add(new TextNode(nx, ny, num.F("size", 9), Py.Str(num["text"])).Fill(mk)
                    .Tr(new Rotate(90, nx, ny, About: true)));
            }
            low.Add(g);
        }

        // --- superstructure, fittings, AA, boats --------------------------------
        // a look's clutter kit (Clutter): roof finishes drawn with each block, the gear itself after all blocks
        var kit = Clutter.Kit(sh.Get("clutter") as string);
        var items = kit != null ? Clutter.Plan(spec, new Hull(spec), TurretTypes(spec), sh) : [];
        double roofPlanks = kit != null ? (sh.TryGet("roof_planks", out var rp) ? Py.ToDouble(rp) : kit.RoofPlanks) : 0;
        bool rails = kit != null && (sh.TryGet("roof_rails", out var rr) ? Py.Truthy(rr) : kit.RoofRails);
        foreach (var sb in Expand(spec, "superstructure", hull))
        {
            string layer = sb.S("layer", sb.I("level", 1) <= 1 ? "base" : "upper")!;
            var o = layer == "base" ? low : high;
            P.Block(o, sb);
            if (kit != null && !sb.B("director"))
                Clutter.RoofFinish(o, sb, P, roofPlanks, rails, P.C("wood"));
        }
        if (kit != null)
        {
            var levels = pal.L("levels");
            foreach (var it in items)
            {
                string col = it.Level == 0 ? deckCol : (string)Painter.At(levels, Math.Min(it.Level, levels.Count) - 1)!;
                Clutter.Draw(it.Level <= 1 ? low : high, it, P, col);
            }
        }
        else
            Vents(low, spec, hull, P);
        foreach (var ft in Expand(spec, "fittings", hull))
            P.Fitting(ft.S("layer", null) == "upper" ? high : low, ft);
        foreach (var a in Expand(spec, "aa", hull))
            P.Aa(a.S("layer", null) == "upper" ? high : low, a);
        foreach (var b in Expand(spec, "boats", hull))
            P.Boat(b.S("layer", null) == "base" ? low : high, b);

        // --- turret barbettes (low pass) ------------------------------------------
        var mounts = Expand(spec, "turrets", hull);
        var types = TurretTypes(spec);
        foreach (var m in mounts)
        {
            var t = types.D(m.S("type"));
            if (Geometry.HasBarbette(t))
                P.Barbette(low, m.F("x"), m.F("y", 0), t.F("r") * t.F("barbette_k", 0.95));
        }

        // --- tall stuff ----------------------------------------------------------
        foreach (var fn in Expand(spec, "funnels", hull))
            P.Funnel(high, fn);
        foreach (var m in Expand(spec, "masts", hull))
            P.Mast(high, m);
        foreach (var c in Expand(spec, "cranes", hull))
            P.Crane(high, c);

        scene.Root.Items.AddRange(low);
        scene.Root.Items.AddRange(high);
        return new Result(scene, mounts, hull, items);
    }
}
