namespace Fleetwright.Shipgen.Render;

/// <summary>shipgen.py's drawing primitives: each adds display-list nodes in ship-local metres. The render path always
/// drew with shadows off (the game casts them from the height map), so the baked drop shadows aren't ported.</summary>
public sealed class Painter
{
    public readonly Palette P;         // the palette (DefaultPalette under the look's)
    public readonly Shapes Shapes;     // the look's drawing variations (Looks.Shapes)
    public List<(List<Pt> Pts, string Col)> Dazzle = [];   // dazzle camouflage panels, set by HullArt
    public readonly double Sw;         // outline width: at least ~0.6 px whatever the scale

    public Painter(Palette palette, double scale, Shapes? shapes)
    {
        P = palette;
        Shapes = shapes ?? new Shapes();
        Sw = Math.Max(0.12, 0.6 / scale);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Palette colour by key.</summary>
    public string C(string key) => P[key];

    /// <summary>The outline: palette "line", sw * k wide, round joins (Painter.stroke).</summary>
    public T Ln<T>(T n, double k = 1.0) where T : Node => n.Stroke(C("line"), Sw * k).Join("round");

    /// <summary>k &lt; 1 darkens, k &gt; 1 lightens toward white.</summary>
    public static string Shade(string hex, double k)
    {
        Span<long> c = [Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16)];
        for (int i = 0; i < 3; i++)
            c[i] = k <= 1 ? (long)Math.Round(c[i] * k) : (long)Math.Round(c[i] + (255 - c[i]) * (k - 1));
        return "#" + string.Concat(c.ToArray().Select(v => Math.Max(0L, Math.Min(255L, v)).ToString("x2")));
    }

    /// <summary>A list item by Python index (negative counts from the end).</summary>
    public static T At<T>(IReadOnlyList<T> l, long i) => l[(int)(i < 0 ? i + l.Count : i)];

    /// <summary>Rectangle with separate corner radii for the front (+x) and back (-x) ends (shipgen.rrect_path).</summary>
    public static PathData RrectPath(double x0, double y0, double x1, double y1, double rf = 0.0, double rb = 0.0)
    {
        double h = (y1 - y0) / 2;
        rf = Math.Min(rf, Math.Min(h, (x1 - x0) / 2));
        rb = Math.Min(rb, Math.Min(h, (x1 - x0) / 2));
        var d = new PathData().M(x0 + rb, y0).L(x1 - rf, y0);
        if (rf > 0)
            d.A(rf, rf, 0, false, true, x1, y0 + rf);
        d.L(x1, y1 - rf);
        if (rf > 0)
            d.A(rf, rf, 0, false, true, x1 - rf, y1);
        d.L(x0 + rb, y1);
        if (rb > 0)
            d.A(rb, rb, 0, false, true, x0, y1 - rb);
        d.L(x0, y0 + rb);
        if (rb > 0)
            d.A(rb, rb, 0, false, true, x0 + rb, y0);
        return d.Z();
    }

    public static PathData Poly(IEnumerable<Pt> pts) => PathData.Poly(pts);

    /// <summary>The hull (or an inset deck) outline.</summary>
    public static PathData HullPath(Hull hull, double inset = 0.0, double? maxHw = null, double? xMin = null,
        double? xMax = null, int n = 260) => Poly(hull.Points(inset, maxHw, xMin, xMax, n));

    /// <summary>The dazzle panels clipped to the outline d, or null with no dazzle.</summary>
    public Node? Dazzled(PathData d, double opacity = 1.0)
    {
        if (Dazzle.Count == 0 || opacity <= 0)
            return null;
        var g = new Group { Clip = [new PathNode(d)] };
        if (opacity < 1)
            g.Opacity(opacity);
        foreach (var (pts, col) in Dazzle)
            g.Items.Add(new PathNode(Poly(pts)).Fill(col));
        return g;
    }

    static void Add(List<Node> o, Node? n)
    {
        if (n != null)
            o.Add(n);
    }

    // ------------------------------------------------------------------ parts

    /// <summary>AA guns (baked into the hull layers; too small to rotate usefully).</summary>
    public void Aa(List<Node> o, TopView.Aa a)
    {
        var cfg = Geometry.AaCfg[a.Type];
        double x = a.X, y = a.Y, d = a.Dir;
        double r = cfg.R, ln = cfg.BarrelLen, bw = cfg.BarrelW, sp = cfg.Spacing;
        long n = cfg.Barrels;
        var g = new Group().Tr(new Translate(x, y), new Rotate(d));
        g.Items.Add(Ln(new CircleNode(0, 0, r).Fill(C("tub"))));
        g.Items.Add(new CircleNode(0, 0, r * 0.72).Fill(Shade(C("tub"), 0.8)));
        for (int i = 0; i < n; i++)
        {
            double yy = (i - (n - 1) / 2.0) * sp;
            g.Items.Add(new RectNode(0, yy - bw / 2, ln, bw).Fill(C("barrel")));
        }
        g.Items.Add(Ln(new RectNode(-r * 0.45, -r * 0.45, r * 0.8, r * 0.9, r * 0.15).Fill(C("turret")), 0.7));
        o.Add(g);
    }

    public void Boat(List<Node> o, Boat b)
    {
        double x = b.X, y = b.Y, l = b.L, w = b.W;
        o.Add(Ln(new EllipseNode(x, y, l / 2, w / 2).Fill(C("boat"))));
        o.Add(new EllipseNode(x, y, l / 2 - 0.5, w / 2 - 0.35).Fill(Shade(C("boat"), 0.82)));
    }

    public void Fitting(List<Node> o, Fitting ft) => Fitting(o, ft.X, ft.Y, ft.L, ft.W, ft.Color, ft.R);

    /// <summary>A coloured box: colour a palette key or a colour, its corners rounded r (by default a fifth of its
    /// shorter side).</summary>
    public void Fitting(List<Node> o, double x, double y, double l, double w, string colour, double? r = null)
    {
        double rr = r ?? Math.Min(l, w) * 0.2;
        o.Add(Ln(new PathNode(RrectPath(x - l / 2, y - w / 2, x + l / 2, y + w / 2, rr, rr)).Fill(P.Or(colour, colour)), 0.8));
    }

    /// <summary>Superstructure block; higher "level" =&gt; lighter.</summary>
    public void Block(List<Node> o, TopView.Block b)
    {
        if (b.Director != null)
        {
            Director(o, b);
            return;
        }
        long lvl = b.Level;
        double x0 = b.X0, x1 = b.X1, y = b.Y, w = b.W;
        double rf = b.Rf, rb = b.Rb;
        var mode = Shapes.Blocks;
        PathData d;
        if (b.Points is { Count: > 0 })       // the layout's own outline, sharp-cornered
            d = Poly(b.Points);
        else if (mode == "chamfer")     // cut corners instead of round ones
        {
            double h = w / 2;
            double cf = Math.Min(rf, Math.Min(h, (x1 - x0) / 2)), cb = Math.Min(rb, Math.Min(h, (x1 - x0) / 2));
            d = Poly([new(x0 + cb, y - h), new(x1 - cf, y - h), new(x1, y - h + cf), new(x1, y + h - cf), new(x1 - cf, y + h),
                      new(x0 + cb, y + h), new(x0, y + h - cb), new(x0, y - h + cb)]);
        }
        else if (mode == "tower" && lvl >= (Shapes.TowerLevel ?? 3))   // a round-fronted tower bridge
            d = RrectPath(x0, y - w / 2, x1, y + w / 2, Math.Min(w / 2, (x1 - x0) * 0.6), rb * 0.4);
        else
        {
            double kf, kb;
            if (Shapes.BlockRound is { Length: > 0 } br)
                (kf, kb) = (br[0], br[1]);
            else
                (kf, kb) = mode switch
                {
                    "boxy" => (0.35, 0.35), "soft" => (1.6, 1.4), "bowfront" => (1.5, 0.4), "tower" => (1.2, 0.5),
                    _ => (1.0, 1.0),
                };
            d = RrectPath(x0, y - w / 2, x1, y + w / 2, rf * kf, rb * kb);
        }
        var cols = P.Colours("levels");
        string col = At(cols, Math.Min(lvl, cols.Count) - 1);
        o.Add(Ln(new PathNode(d).Fill(col)));
        Add(o, Dazzled(d, Shapes.DazzleUpperworks ?? 1.0));
        // a thin lighter rim on the port/fwd edge suggests light from the upper-left
        o.Add(new PathNode(d).Fill("none").Stroke(Shade(col, 1.25), Sw * 0.9)
            .Tr(new Translate(-Sw * 0.6, -Sw * 0.6)).StrokeOp(0.7));
    }

    /// <summary>A fire-control director on its roof, facing ahead, drawn from Geometry.DirectorParts (the same shape
    /// as its hitbox): the hood, the rangefinder's tube and end hoods sticking out each side, and a radar aerial on
    /// the hood's roof when it has one. Without a rangefinder (a gyro sight): a round tub with the sight in it.</summary>
    public void Director(List<Node> o, TopView.Block b)
    {
        var dr = b.Director!;
        double x0 = b.X0, x1 = b.X1, y = b.Y, w = b.W;
        double l = x1 - x0, xc = (x0 + x1) / 2;
        double rf = dr.RangefinderM;
        var parts = Geometry.DirectorParts(xc, y, l, w, rf);
        var cols = P.Colours("levels");
        string col = At(cols, Math.Min(dr.On + 1, cols.Count) - 1);   // a shade lighter than the roof it stands on
        var hood = Poly(parts.Hood);
        if (rf != 0)
        {
            if (parts.Tube != null)
                o.Add(Ln(new PathNode(Poly(parts.Tube)).Fill(Shade(col, 0.7))));
            foreach (var e in parts.Ends)
                o.Add(Ln(new PathNode(Poly(e)).Fill(Shade(col, 0.85))));
            o.Add(Ln(new PathNode(hood).Fill(col)));
            Add(o, Dazzled(hood, Shapes.DazzleUpperworks ?? 1.0));
            // the hood's roof: a sighting hatch aft, and the radar aerial (a flat dish seen edge-on) forward
            double hw = parts.Hood.Select(p => p.Y).Max() - y;
            o.Add(Ln(new PathNode(RrectPath(x0 + 0.12 * l, y - 0.4 * hw, x0 + 0.32 * l, y + 0.4 * hw, 0.1, 0.1))
                .Fill(Shade(col, 0.88)), 0.6));
            if (dr.Radar)
            {
                double ax = xc + 0.22 * l, aw = 1.5 * hw;
                o.Add(new LineNode(xc, y, ax, y).Stroke(C("mast"), Math.Max(Sw * 1.2, 0.18)));
                o.Add(Ln(new PathNode(RrectPath(ax - 0.18, y - aw / 2, ax + 0.18, y + aw / 2, 0.12, 0.12)).Fill(C("mast")), 0.6));
            }
        }
        else
        {
            double r = Math.Min(l, w) / 2;
            o.Add(Ln(new PathNode(hood).Fill(C("tub"))));
            o.Add(new CircleNode(xc, y, r * 0.72).Fill(Shade(C("tub"), 0.8)));
            o.Add(Ln(new PathNode(RrectPath(xc - 0.3 * r, y - 0.35 * r, xc + 0.25 * r, y + 0.35 * r, 0.08, 0.08)).Fill(col), 0.6));
            o.Add(new LineNode(xc + 0.25 * r, y, xc + 0.6 * r, y).Stroke(C("barrel"), Math.Max(Sw * 1.2, 0.15)));
        }
        o.Add(new PathNode(hood).Fill("none").Stroke(Shade(col, 1.25), Sw * 0.9)
            .Tr(new Translate(-Sw * 0.6, -Sw * 0.6)).StrokeOp(0.7));
    }

    public void Funnel(List<Node> o, TopView.Funnel fn)
    {
        double x = fn.X, y = fn.Y, l = fn.L, w = fn.W;
        var mode = Shapes.Funnel;
        double r = w / 2 * (Shapes.FunnelRound ?? (mode == "box" ? 0.44 : 1.0));
        var d = RrectPath(x - l / 2, y - w / 2, x + l / 2, y + w / 2, r, r);
        var inner = RrectPath(x - l / 2 + 0.6, y - w / 2 + 0.6, x + l / 2 - 0.6, y + w / 2 - 0.6, Math.Max(0.0, r - 0.6),
            Math.Max(0.0, r - 0.6));
        if (mode == "oval")       // a smooth superellipse, fuller than an ellipse
        {
            double n = Shapes.FunnelSquareness ?? 2.6;
            const int k = 48;
            PathData Sup(double a, double b_) => Poly(Enumerable.Range(0, k).Select(i =>
            {
                double t = 2 * Math.PI * i / k, c = Math.Cos(t), s = Math.Sin(t);
                return new Pt(x + a * Math.CopySign(Math.Pow(Math.Abs(c), 2 / n), c),
                    y + b_ * Math.CopySign(Math.Pow(Math.Abs(s), 2 / n), s));
            }));
            (d, inner) = (Sup(l / 2, w / 2), Sup(l / 2 - 0.6, w / 2 - 0.6));
        }
        double rake = Shapes.FunnelRake ?? 0.0;   // a raked funnel: the top seen displaced aft of its foot
        var top = o;
        if (rake != 0)   // the foot, then the casing's forward face sloping back to the top
        {
            o.Add(Ln(new PathNode(d).Fill(Shade(C("funnel"), 0.9))));
            o.Add(new RectNode(x - rake, y - w / 2, rake, w).Fill(Shade(C("funnel"), 0.9)));
            o.Add(Ln(new LineNode(x - rake, y - w / 2, x, y - w / 2)));
            o.Add(Ln(new LineNode(x - rake, y + w / 2, x, y + w / 2)));
            var g = new Group().Tr(new Translate(-rake, 0));
            o.Add(g);
            top = g.Items;
        }
        top.Add(Ln(new PathNode(d).Fill(C("funnel"))));
        Add(top, Dazzled(d, Shapes.DazzleUpperworks ?? 1.0));
        top.Add(new PathNode(inner).Fill(C("funnel_cap")));
        if (P.Has("funnel_band"))   // a painted top band (a look's funnel marking), seen from above as a rim
        {
            var band = RrectPath(x - l / 2 + 0.2, y - w / 2 + 0.2, x + l / 2 - 0.2, y + w / 2 - 0.2, r - 0.2, r - 0.2);
            top.Add(new PathNode(band).Fill("none").Stroke(C("funnel_band"), Shapes.FunnelBandW ?? 0.25));
        }
        long pipes = fn.Pipes;   // uptake openings
        for (int i = 0; i < pipes; i++)
        {
            double cx = x - l / 2 + 0.6 + (l - 1.2) * (i + 0.5) / pipes;
            top.Add(new EllipseNode(cx, y, (l - 1.2) / pipes / 2 - 0.25, w / 2 - 1.0).Fill("#0b0d0f"));
        }
        if (mode == "capped")     // a cowl cap: a lighter ring standing proud round the top
        {
            var cap = RrectPath(x - l / 2 + 0.35, y - w / 2 + 0.35, x + l / 2 - 0.35, y + w / 2 - 0.35, r - 0.35, r - 0.35);
            top.Add(new PathNode(cap).Fill("none").Stroke(Shade(C("funnel"), 1.25), 0.45));
        }
        var capMode = Shapes.FunnelCap;
        if (capMode == "pan")         // an Italian "frying pan": a flat plate over the top, overhanging aft, open forward
        {
            double pl = l * 0.8, pw = w * 1.08, px = x - l * 0.22;
            top.Add(Ln(new PathNode(RrectPath(px - pl / 2, y - pw / 2, px + pl / 2, y + pw / 2, pw * 0.3, pw / 2))
                .Fill(Shade(C("funnel"), 0.8)), 0.9));
            for (int i = 1; i < 4; i++)   // the plate's stiffeners
            {
                double gx = px - pl / 2 + pl * i / 4;
                top.Add(new LineNode(gx, y - pw / 2 + 0.3, gx, y + pw / 2 - 0.3).Stroke(Shade(C("funnel"), 0.6), Sw * 0.8));
            }
        }
        else if (capMode == "hat")       // a French "chapeau": a broad black cap with smoke vanes all round
        {
            double hl = l + 1.2, hw_ = w + 1.2;
            var hd = RrectPath(x - hl / 2, y - hw_ / 2, x + hl / 2, y + hw_ / 2, Math.Min(hw_ / 2, r + 0.6), Math.Min(hw_ / 2, r + 0.6));
            top.Add(Ln(new PathNode(hd).Fill(C("funnel_cap"))));
            top.Add(new PathNode(inner).Fill(Shade(C("funnel_cap"), 1.25)).Tr(new Translate(x * 0.12, y * 0.12), new Scale(0.88)));
            long nv = Math.Max(2L, (long)(l / 1.5));
            for (int i = 0; i < nv; i++)   // vanes across the cap
            {
                double vx = x - l / 2 + 0.3 + (l - 0.6) * (i + 0.5) / nv;
                top.Add(new LineNode(vx, y - w / 2 - 0.4, vx, y + w / 2 + 0.4).Stroke(Shade(C("funnel_cap"), 2.2), Sw * 0.7)
                    .StrokeOp(0.7));
            }
        }
        // cap grating highlight
        top.Add(new PathNode(d).Fill("none").Stroke(Shade(C("funnel"), 1.3), Sw).Tr(new Translate(-Sw * 0.6, -Sw * 0.6))
            .StrokeOp(0.6));
    }

    public void Mast(List<Node> o, TopView.Mast m)
    {
        double x = m.X, y = m.Y;
        double span = m.Yard;
        string mast = C("mast");
        if (span != 0)
            o.Add(new LineNode(x, y - span / 2, x, y + span / 2).Stroke(mast, Math.Max(Sw * 1.6, 0.35)).Cap("round"));
        foreach (var bo in m.Booms ?? [])   // cargo derricks, heel at the mast
        {
            var (bx, by) = (bo[0], bo[1]);
            o.Add(new LineNode(x, y, bx, by).Stroke(mast, Math.Max(Sw * 1.4, 0.3)).Cap("round"));
            o.Add(new CircleNode(bx, by, 0.3).Fill(mast));
        }
        var mode = Shapes.Mast;
        bool tripod = m.Tripod;
        double legs = Shapes.Tripod ?? (mode is "pole" or "fighting_top" or "cage" or "lattice" ? 0.0 : 1.0);   // 0: every mast a pole
        if (tripod && legs != 0)
            foreach (int ang in new[] { 150, 210 })
            {
                double lx = x + 4.0 * legs * Math.Cos(double.DegreesToRadians(ang));
                double ly = y + 3.0 * legs * (ang == 150 ? 1 : -1);
                o.Add(new LineNode(x, y, lx, ly).Stroke(mast, Math.Max(Sw * 1.3, 0.3)).Cap("round"));
            }
        double cage = Shapes.CageR ?? (mode == "cage" ? 3.0 : 0.0);
        if (cage != 0 && tripod)   // a US cage mast from above: twisted struts from a wide foot to a narrow head
        {
            for (int i = 0; i < 12; i++)
            {
                double a0 = double.DegreesToRadians(i * 30), a1 = double.DegreesToRadians(i * 30 + 75);
                o.Add(new LineNode(x + cage * Math.Cos(a0), y + cage * Math.Sin(a0), x + 0.4 * cage * Math.Cos(a1),
                    y + 0.4 * cage * Math.Sin(a1)).Stroke(mast, Math.Max(Sw * 0.8, 0.15)));
            }
            o.Add(new CircleNode(x, y, cage).Fill("none").Stroke(mast, Math.Max(Sw * 0.8, 0.15)));
            o.Add(Ln(new CircleNode(x, y, 0.45 * cage).Fill(Shade(mast, 0.85)), 0.6));
        }
        double lat = Shapes.LatticeR ?? (mode == "lattice" ? 2.2 : 0.0);
        if (lat != 0 && tripod)   // a lattice mast: a square braced truss narrowing to a radar platform
        {
            foreach (double k in new[] { 1.0, 0.55 })
            {
                double q = lat * k;
                o.Add(new RectNode(x - q, y - q, 2 * q, 2 * q).Fill("none").Stroke(mast, Math.Max(Sw * 0.9, 0.18)));
            }
            foreach (var (sx, sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                o.Add(new LineNode(x + sx * lat, y + sy * lat, x - sx * 0.55 * lat, y + sy * 0.55 * lat)
                    .Stroke(mast, Math.Max(Sw * 0.7, 0.14)));
            o.Add(Ln(new RectNode(x - 0.4 * lat, y - 0.4 * lat, 0.8 * lat, 0.8 * lat).Fill(Shade(mast, 1.3)), 0.6));
            o.Add(new LineNode(x, y - 1.3 * lat, x, y + 1.3 * lat).Stroke(Shade(mast, 0.8), Math.Max(Sw * 1.6, 0.35)));
        }
        double top = Shapes.TopR ?? (mode == "fighting_top" ? 1.6 : 0.0);
        long tiers = top != 0 ? Shapes.TopTiers ?? 1 : 0;
        for (int i = 0; i < tiers; i++)   // a fighting top; tiers stack a pagoda
        {
            double r = top * (1 - 0.3 * i);
            o.Add(Ln(new CircleNode(x, y, r).Fill(Shade(mast, 0.75 + 0.2 * i)), 0.8));
            o.Add(new CircleNode(x, y, r - 0.35).Fill("none").Stroke(Shade(mast, 1.2 + 0.2 * i), 0.2));
        }
        o.Add(Ln(new CircleNode(x, y, 0.7).Fill(mast), 0.6));
    }

    /// <summary>Revolving crane: pedestal and cab, jib pointing along dir (degrees, 0 = ahead).</summary>
    public void Crane(List<Node> o, Crane c)
    {
        double x = c.X, y = c.Y, r = c.R;
        double a = double.DegreesToRadians(c.Dir);
        double jx = x + c.Jib * Math.Cos(a), jy = y + c.Jib * Math.Sin(a);
        string col = C("crane");
        o.Add(Ln(new CircleNode(x, y, r).Fill(Shade(col, 1.3))));
        o.Add(new LineNode(x, y, jx, jy).Stroke(col, Math.Max(0.45, r * 0.4)).Cap("round"));
        o.Add(new CircleNode(jx, jy, 0.35).Fill(col));
        o.Add(Ln(new CircleNode(x, y, r * 0.55).Fill(Shade(col, 1.6)), 0.6));
    }

    /// <summary>Cargo hatch: coaming, tarpaulin cover and the battens across it.</summary>
    public void Hatch(List<Node> o, Hatch h)
    {
        double x = h.X, y = h.Y, l = h.L, w = h.W;
        o.Add(Ln(new RectNode(x - l / 2, y - w / 2, l, w, 0.25).Fill(C("hatch_coaming"))));
        o.Add(new RectNode(x - l / 2 + 0.35, y - w / 2 + 0.35, l - 0.7, w - 0.7).Fill(C("hatch")));
        long n = Math.Max(1L, (long)(l / 1.6));
        for (int i = 1; i < n; i++)
        {
            double bx = x - l / 2 + l * i / n;
            o.Add(new LineNode(bx, y - w / 2 + 0.35, bx, y + w / 2 - 0.35).Stroke(Shade(C("hatch"), 0.7), Sw * 0.8));
        }
    }

    /// <summary>The conning tower from above: an armoured drum with a lighter roof and the dark band of its vision slits
    /// round the forward half.</summary>
    public void ConningTower(List<Node> o, double x, double y, double r)
    {
        string col = C("turret");
        o.Add(Ln(new CircleNode(x, y, r).Fill(col), 1.1));
        o.Add(new CircleNode(x, y, r * 0.78).Fill(Shade(col, 1.12)));
        double rs = r * 0.88, a = double.DegreesToRadians(65);
        o.Add(new PathNode(new PathData().M(x + rs * Math.Cos(a), y - rs * Math.Sin(a)).A(rs, rs, 0, false, true, x + rs * Math.Cos(a), y + rs * Math.Sin(a)))
            .Fill("none").Stroke(Shade(col, 0.45), Math.Max(Sw, 0.1 * r)));
    }

    public void Barbette(List<Node> o, double x, double y, double r)
    {
        o.Add(Ln(new CircleNode(x, y, r).Fill(C("barbette"))));
        o.Add(new CircleNode(x, y, r * 0.8).Fill(Shade(C("barbette"), 0.75)));
    }
}
