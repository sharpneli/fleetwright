namespace Fleetwright.Shipgen.Render;

/// <summary>The profile: the ship from the starboard side, drawn the way HullArt draws the plan (the same palette keys,
/// outline, rim light and scale rules), so the two read as one drawing. Ship metres with y = -height above the main
/// deck. Parts are drawn far to near (by their starboard edge), the hull among them at half the beam: what is inside the
/// hull's side hides behind it, what stands outside it (casemate guns, sponsons, the flight deck) in front.</summary>
public static class SideArt
{
    /// <summary>A part to draw: its depth (larger is nearer) and its drawing.</summary>
    readonly record struct Item(double Depth, int Order, Action<List<Node>> Draw);

    /// <summary>The profile of v at scale px/m on a canvas from x0 to x1 and from top down to bottom (heights above the
    /// main deck). above: only what is above the waterline (the silhouette's cut).</summary>
    public static Scene Build(SideView v, Palette pal, Shapes sh, string turretLook, double scale, double x0, double x1, double top, double bottom,
        bool above = false)
    {
        var P = new Painter(pal, scale, sh);
        var scene = new Scene(x0, -top, x1 - x0, top - bottom, scale);
        var items = new List<Item>();
        void Add(double depth, Action<List<Node>> draw) => items.Add(new Item(depth, items.Count, draw));
        double hullDepth = v.Beam / 2;
        double wl = v.Waterline;

        Add(hullDepth, o => Hull(o, v, P));
        foreach (var h in v.Hangars)   // the hangar's side, up to the flight deck when it stands on it
        {
            double up = v.Platforms.Where(p => p.Kind == "flight_deck" && p.B.Base >= h.Top && p.B.Base - h.Top < 1.5).Select(p => p.B.Base)
                .DefaultIfEmpty(h.Top).Min();
            Add(h.Y1, o => Box(o, P, h with { Top = up }, P.C("hull")));
        }
        foreach (var c in v.Casings)
            Add(c.Y1, o => Box(o, P, c, Painter.Shade(P.C("deck"), 0.9), 0.8));
        foreach (var b in v.Barbettes)
            Add(b.B.Y1, o => Box(o, P, b.B, P.C("barbette")));
        if (v.ConningTower is { } ct)
            Add(ct.Y1, o => ConningTower(o, P, ct));
        foreach (var b in v.Blocks)
            Add(b.B.Y1, o => Block(o, P, b));
        foreach (var f in v.Funnels)
            Add(f.B.Y1, o => Funnel(o, P, f));
        foreach (var m in v.Masts)
            Add(m.Y + m.R, o => Mast(o, P, m));
        foreach (var m in v.Mounts)
        {
            var t = v.TurretTypes[m.Type];
            if (m.Casemate)   // in the hull's side: only the starboard ones show, in front of it
            {
                if (m.Y > 0)
                    Add(hullDepth + 1, o => Casemate(o, P, m, t));
                continue;
            }
            Add(m.B.Y1, o => Turret(o, P, m, t, turretLook));
        }
        foreach (var a in v.AaMounts)
            Add(a.Y + a.R, o => Aa(o, P, a));
        foreach (var p in v.Platforms)
            Add(p.B.Y1, o => Platform(o, P, p, v));
        foreach (var bt in v.Dressing.Boats ?? [])
            Add(bt.Y + bt.W / 2, o => Boat(o, P, bt));
        foreach (var cr in v.Dressing.Cranes ?? [])
            Add(cr.Y + cr.R, o => Crane(o, P, cr));
        foreach (var ht in v.Dressing.Hatches ?? [])
            Add(ht.Y + ht.W / 2, o => Hatch(o, P, ht));
        // under the water: the starboard and centreline gear in front of the hull, the port gear behind it
        double Under(double y) => y >= -0.01 ? hullDepth + 0.5 : -hullDepth;
        foreach (var r in v.Rudders)
            Add(Under((r.Y0 + r.Y1) / 2), o => Rudder(o, P, r));
        foreach (var s in v.Shafts)
            Add(Under(s.Y), o => Shaft(o, P, s));
        foreach (var pr in v.Propellers)
            Add(Under(pr.Y), o => Propeller(o, P, pr));

        var drawn = new List<Node>();
        foreach (var it in items.OrderBy(i => i.Depth).ThenBy(i => i.Order))
            it.Draw(drawn);
        if (above)
        {
            var g = new Group(drawn) { Clip = [new RectNode(x0, -top, x1 - x0, top - wl)] };
            scene.Root.Items.Add(g);
        }
        else
            scene.Root.Items.AddRange(drawn);
        return scene;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>(x, z) to the scene's (x, -z).</summary>
    static IEnumerable<Pt> Up(IEnumerable<Pt> xz) => xz.Select(p => new Pt(p.X, -p.Y));

    static PathData Poly(params Pt[] xz) => Painter.Poly(Up(xz));

    static PathData Rect(double x0, double x1, double z0, double z1) => Poly(new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1));

    /// <summary>The plan's light: a thin lighter rim on the edges toward the light (there aft and to port; here aft and
    /// up), the same width, offset and opacity as Painter's.</summary>
    static Node Rim(Painter P, PathData d, string col) =>
        new PathNode(d).Fill("none").Stroke(Painter.Shade(col, 1.25), P.Sw * 0.9).Tr(new Translate(-P.Sw * 0.6, -P.Sw * 0.6)).StrokeOp(0.7);

    /// <summary>A box in a colour: outlined and rim-lit.</summary>
    static void Box(List<Node> o, Painter P, SideView.Box b, string col, double k = 1.0)
    {
        if (b.H <= 0 || b.L <= 0)
            return;
        var d = Rect(b.X0, b.X1, b.Base, b.Top);
        o.Add(P.Ln(new PathNode(d).Fill(col), k));
        o.Add(Rim(P, d, col));
    }

    static string Level(Painter P, long level)
    {
        var cols = P.P.Colours("levels");
        return Painter.At(cols, Math.Clamp(level, 1, cols.Count) - 1);
    }

    // ------------------------------------------------------------------ the hull

    /// <summary>The hull's side: the topsides in the hull colour, the bottom in anti-fouling under a boot-topping band at
    /// the waterline, the knuckle's crease where the sides lean in, and the outline.</summary>
    static void Hull(List<Node> o, SideView v, Painter P)
    {
        var d = Painter.Poly(Up(v.Hull));
        string hull = P.C("hull");
        o.Add(new PathNode(d).Fill(hull));
        double wl = v.Waterline, x0 = v.HullX0 - 1, x1 = v.HullX1 + 1;
        double band = Math.Min(0.6, 0.12 * -wl);   // the boot top: a painted band, narrower on a low hull
        var under = new Group { Clip = [new PathNode(d)] };
        under.Items.Add(new PathNode(Rect(x0, x1, v.Keel - 1, wl)).Fill(P.C("bottom")));
        under.Items.Add(new PathNode(Rect(x0, x1, wl - band / 2, wl + band)).Fill(P.C("boot_top")));
        if (v.Knuckle is double k && k > wl + band)
            under.Items.Add(new LineNode(x0, -k, x1, -k).Stroke(Painter.Shade(hull, 0.75), P.Sw * 0.8));
        o.Add(under);
        o.Add(P.Ln(new PathNode(d).Fill("none"), 1.4));
        o.Add(Rim(P, d, hull));
    }

    // ------------------------------------------------------------------ upperworks

    static void Block(List<Node> o, Painter P, SideView.Block b)
    {
        if (b.Director is { } dr)
        {
            Director(o, P, b, dr);
            return;
        }
        Box(o, P, b.B, Level(P, b.Level));
    }

    /// <summary>A director: the hood, a shade lighter than the roof it stands on as in the plan; the rangefinder runs
    /// across the ship, so it shows end-on, its hood a disc on the side; a radar aerial on a short post above.</summary>
    static void Director(List<Node> o, Painter P, SideView.Block b, TopView.Director dr)
    {
        string col = Level(P, dr.On + 1);
        var bx = b.B;
        if (dr.RangefinderM == 0)   // a gyro sight in an open tub
        {
            double h = Math.Min(bx.H, 1.2);
            Box(o, P, bx with { Top = bx.Base + h }, P.C("tub"));
            return;
        }
        Box(o, P, bx, col);
        double r = Math.Min(bx.H * 0.28, bx.L * 0.2);
        o.Add(P.Ln(new CircleNode(bx.Xc, -(bx.Base + bx.H * 0.55), r).Fill(Painter.Shade(col, 0.85)), 0.7));
        if (dr.Radar)
        {
            double top = bx.Top + Math.Max(1.0, bx.H * 0.5);
            o.Add(new LineNode(bx.Xc, -bx.Top, bx.Xc, -top).Stroke(P.C("mast"), Math.Max(P.Sw * 1.2, 0.18)));
            o.Add(P.Ln(new PathNode(Rect(bx.Xc - bx.L * 0.4, bx.Xc + bx.L * 0.4, top - 0.2, top + 0.2)).Fill(P.C("mast")), 0.6));
        }
    }

    /// <summary>The conning tower: an armoured drum with its band of vision slits under the roof.</summary>
    static void ConningTower(List<Node> o, Painter P, SideView.Box b)
    {
        string col = P.C("turret");
        Box(o, P, b, col, 1.1);
        double s = Math.Min(0.35, b.H * 0.1);
        o.Add(new PathNode(Rect(b.X0 + P.Sw, b.X1 - P.Sw, b.Top - 3 * s, b.Top - 2 * s)).Fill(Painter.Shade(col, 0.45)));
    }

    /// <summary>A funnel: the casing (raked aft by the look's rake, as the plan displaces its top), the painted band and
    /// the cap the look gives it.</summary>
    static void Funnel(List<Node> o, Painter P, SideView.Funnel f)
    {
        var b = f.B;
        if (b.H <= 0)
            return;
        var sh = P.Shapes;
        double rake = sh.FunnelRake ?? 0.0;
        // a point on the casing's front (s = 0) or back (s = 1) at height z, the rake growing with height
        double X(double s, double z) => (s == 0 ? b.X1 : b.X0) - rake * (z - b.Base) / b.H;
        PathData Band(double z0, double z1) => Poly(new(X(1, z0), z0), new(X(0, z0), z0), new(X(0, z1), z1), new(X(1, z1), z1));
        string col = P.C("funnel");
        var casing = Band(b.Base, b.Top);
        o.Add(P.Ln(new PathNode(casing).Fill(col)));
        var Dazzle = P.Dazzled(casing, sh.DazzleUpperworks ?? 1.0);
        if (Dazzle != null)
            o.Add(Dazzle);
        double cap = Math.Min(1.0, 0.07 * b.H);
        if (P.P.Has("funnel_band"))
        {
            double bw = 3 * (sh.FunnelBandW ?? 0.25);
            o.Add(new PathNode(Band(b.Top - cap - 0.4 - bw, b.Top - cap - 0.4)).Fill(P.C("funnel_band")));
        }
        string capCol = P.C("funnel_cap");
        switch (sh.FunnelCap)
        {
            case "pan":   // a flat plate over the top, overhanging aft
                o.Add(new PathNode(Band(b.Top - cap, b.Top)).Fill(capCol));
                o.Add(P.Ln(new PathNode(Rect(X(1, b.Top) - 0.25 * b.L, X(0, b.Top) - 0.1 * b.L, b.Top, b.Top + 0.3)).Fill(Painter.Shade(col, 0.8)), 0.9));
                break;
            case "hat":   // a broad black cap
                o.Add(P.Ln(new PathNode(Rect(X(1, b.Top) - 0.6, X(0, b.Top) + 0.6, b.Top - 1.1 * cap, b.Top + 0.2)).Fill(capCol)));
                break;
            default:
                o.Add(new PathNode(Band(b.Top - cap, b.Top)).Fill(capCol));
                if (sh.Funnel == "capped")   // a cowl cap: a lighter rim standing proud round the top
                    o.Add(new PathNode(Band(b.Top - cap - 0.45, b.Top - cap)).Fill(Painter.Shade(col, 1.25)));
                break;
        }
        o.Add(P.Ln(new PathNode(casing).Fill("none")));
        o.Add(Rim(P, casing, col));
    }

    /// <summary>A mast: a tapering pole with a crosstree near its head, and what the look builds round it (tripod legs,
    /// fighting tops, a cage or a lattice), as the plan does from above.</summary>
    static void Mast(List<Node> o, Painter P, SideView.Mast m)
    {
        var sh = P.Shapes;
        string mast = P.C("mast");
        double x = m.X, b = m.Base, t = m.Top, h = t - b;
        if (h <= 0)
            return;
        var mode = sh.Mast;
        double w = Math.Max(P.Sw * 1.6, 0.35);
        foreach (var bo in m.Booms ?? [])   // cargo derricks, heel at the mast, topped up a little
        {
            double bx = bo[0], len = Math.Abs(bx - x);
            o.Add(new LineNode(x, -(b + 1.5), bx, -(b + 1.5 + 0.3 * len)).Stroke(mast, Math.Max(P.Sw * 1.4, 0.3)).Cap("round"));
        }
        double legs = sh.Tripod ?? (mode is "pole" or "fighting_top" or "cage" or "lattice" ? 0.0 : 1.0);
        if (m.Tripod && legs != 0)   // the legs spread aft, as in the plan, and meet the pole about half way up
        {
            double lx = x + 4.0 * legs * Math.Cos(double.DegreesToRadians(150)), lz = b + Math.Min(0.55 * h, 12.0);
            o.Add(new LineNode(lx, -b, x, -lz).Stroke(mast, Math.Max(P.Sw * 1.3, 0.3)).Cap("round"));
        }
        double cage = sh.CageR ?? (mode == "cage" ? 3.0 : 0.0);
        if (cage != 0 && m.Tripod)   // a US cage mast: twisted struts from a wide foot to a narrow head
        {
            double ct = b + 0.85 * h;
            var g = new Group().Stroke(mast, Math.Max(P.Sw * 0.8, 0.15));
            for (int i = 0; i <= 6; i++)
            {
                double u = -1 + i / 3.0;
                g.Items.Add(new LineNode(x + u * cage, -b, x - u * 0.4 * cage, -ct));
            }
            g.Items.Add(new LineNode(x - cage, -b, x - 0.4 * cage, -ct));
            g.Items.Add(new LineNode(x + cage, -b, x + 0.4 * cage, -ct));
            o.Add(g);
            o.Add(P.Ln(new PathNode(Rect(x - 0.6 * cage, x + 0.6 * cage, ct - 0.6, ct)).Fill(Painter.Shade(mast, 0.85)), 0.6));
        }
        double lat = sh.LatticeR ?? (mode == "lattice" ? 2.2 : 0.0);
        if (lat != 0 && m.Tripod)   // a lattice mast: a braced truss narrowing to a radar platform
        {
            double lt = b + 0.8 * h;
            var g = new Group().Stroke(mast, Math.Max(P.Sw * 0.7, 0.14));
            g.Items.Add(new LineNode(x - lat, -b, x - 0.55 * lat, -lt));
            g.Items.Add(new LineNode(x + lat, -b, x + 0.55 * lat, -lt));
            const int n = 5;
            for (int i = 0; i < n; i++)
            {
                double z0 = b + (lt - b) * i / n, z1 = b + (lt - b) * (i + 1) / n;
                double w0 = lat * (1 - 0.45 * i / n), w1 = lat * (1 - 0.45 * (i + 1) / n);
                g.Items.Add(new LineNode(x - w0, -z0, x + w1, -z1));
                g.Items.Add(new LineNode(x + w0, -z0, x - w1, -z1));
            }
            o.Add(g);
            o.Add(P.Ln(new PathNode(Rect(x - 0.7 * lat, x + 0.7 * lat, lt - 0.4, lt)).Fill(Painter.Shade(mast, 1.3)), 0.6));
        }
        // the pole, from its full radius at the foot to half that at the head
        var pole = Poly(new(x - m.R, b), new(x + m.R, b), new(x + 0.4 * m.R, t), new(x - 0.4 * m.R, t));
        o.Add(P.Ln(new PathNode(pole).Fill(mast), 0.6));
        double tr = b + 0.82 * h;   // the crosstree where the yard crosses
        o.Add(new LineNode(x - 1.0, -tr, x + 1.0, -tr).Stroke(mast, w).Cap("round"));
        double topR = sh.TopR ?? (mode == "fighting_top" ? 1.6 : 0.0);
        long tiers = topR != 0 ? sh.TopTiers ?? 1 : 0;
        for (int i = 0; i < tiers; i++)   // a fighting top below the crosstree; tiers stack a pagoda
        {
            double r = topR * (1 - 0.3 * i), z = b + 0.62 * h + 1.6 * i;
            string col = Painter.Shade(mast, 0.75 + 0.2 * i);
            var d = Rect(x - r, x + r, z, z + 1.2);
            o.Add(P.Ln(new PathNode(d).Fill(col), 0.8));
            o.Add(Rim(P, d, col));
        }
    }

    /// <summary>A gun mount at its rest bearing: the gunhouse in the look's way, its face toward the barrels, and the
    /// barrels from the face to the muzzles (side by side, they show as one).</summary>
    static void Turret(List<Node> o, Painter P, SideView.Mount m, TurretType t, string look)
    {
        var b = m.B;
        double h = b.H;
        if (h <= 0)
            return;
        double c = Math.Cos(double.DegreesToRadians(m.Rest));
        int f = c >= 0 ? 1 : -1;
        bool beam = Math.Abs(c) < 0.5;
        double front = f > 0 ? b.X1 : b.X0, back = f > 0 ? b.X0 : b.X1;
        double zt = b.Base + 0.42 * h;   // the trunnions
        double muzzle = f > 0 ? m.BarrelX1 : m.BarrelX0;
        string col = t.Shape is "torp" or "tube" ? P.C("tube") : P.C("turret");

        if (t.Shape is "torp" or "tube")   // trainable tubes: a cylinder on a low mount
        {
            double r = Math.Min(h * 0.35, 0.6), zc = b.Base + h * 0.55;
            o.Add(P.Ln(new PathNode(Rect(b.Xc - 0.4, b.Xc + 0.4, b.Base, zc)).Fill(P.C("tub")), 0.7));
            var tube = Painter.RrectPath(b.X0, -(zc + r), b.X1, -(zc - r), r, r);
            o.Add(P.Ln(new PathNode(tube).Fill(col)));
            o.Add(Rim(P, tube, col));
            return;
        }
        if (!beam && Math.Abs(muzzle - front) > 0.1)
            Barrel(o, P, t, front - f * 0.4, muzzle, zt);
        PathData d;
        if (t.Shape == "open")   // a shield over the gun on its pedestal
        {
            double sf = front, sb = back + f * 0.45 * b.L;
            o.Add(P.Ln(new PathNode(Rect(b.Xc - 0.15 * b.L, b.Xc + 0.15 * b.L, b.Base, zt)).Fill(P.C("tub")), 0.7));
            d = Poly(new(sb, b.Base + 0.2 * h), new(sf, b.Base + 0.2 * h), new(sf, b.Top - 0.25 * h), new(sf - f * 0.25 * b.L, b.Top), new(sb, b.Top));
        }
        else if (beam)   // seen end-on: a box with both top corners eased
        {
            double e = Math.Min(0.25 * h, 0.15 * b.L);
            d = Poly(new(b.X0, b.Base), new(b.X1, b.Base), new(b.X1, b.Top - e), new(b.X1 - e, b.Top), new(b.X0 + e, b.Top), new(b.X0, b.Top - e));
        }
        else   // the gunhouse: a sloped face over the guns (dual-purpose mounts a rounder one)
        {
            double slope = Math.Min((t.Shape == "dp" ? 0.4 : 0.3) * b.L, (t.Shape == "dp" ? 0.8 : 0.6) * h);
            double knee = b.Base + (t.Shape == "dp" ? 0.4 : 0.55) * h;
            d = Poly(new(back, b.Base), new(front, b.Base), new(front, knee), new(front - f * slope, b.Top), new(back, b.Top));
        }
        o.Add(P.Ln(new PathNode(d).Fill(col)));
        var dz = P.Dazzled(d, P.Shapes.DazzleUpperworks ?? 1.0);
        if (dz != null)
            o.Add(dz);
        o.Add(Rim(P, d, col));
        if (beam && Math.Abs(muzzle - front) <= 0.1 + b.L)   // barrels toward us: the muzzles
            o.Add(P.Ln(new CircleNode(b.Xc, -zt, Math.Max(t.BarrelW * 0.6, 0.12)).Fill(P.C("barrel")), 0.6));
    }

    /// <summary>A barrel from x0 (at the breech end) to the muzzle x1 at height z: tapering, with a collar where it leaves
    /// the face.</summary>
    static void Barrel(List<Node> o, Painter P, TurretType t, double x0, double x1, double z)
    {
        double w0 = Math.Max(t.BarrelW * 1.25, 0.12), w1 = Math.Max(t.BarrelW * 0.75, 0.08);
        var d = Poly(new(x0, z - w0 / 2), new(x1, z - w1 / 2), new(x1, z + w1 / 2), new(x0, z + w0 / 2));
        o.Add(P.Ln(new PathNode(d).Fill(P.C("barrel")), 0.7));
        double dir = Math.Sign(x1 - x0), cl = Math.Min(1.2, Math.Abs(x1 - x0) * 0.12);
        o.Add(P.Ln(new PathNode(Rect(Math.Min(x0, x0 + dir * cl), Math.Max(x0, x0 + dir * cl), z - w0 * 0.75, z + w0 * 0.75))
            .Fill(Painter.Shade(P.C("barrel"), 1.15)), 0.6));
    }

    /// <summary>A casemate gun: its port in the hull's side and the barrel out of it.</summary>
    static void Casemate(List<Node> o, Painter P, SideView.Mount m, TurretType t)
    {
        var b = m.B;
        double zt = b.Base + 0.45 * b.H;
        double c = Math.Cos(double.DegreesToRadians(m.Rest));
        double muzzle = c >= 0 ? m.BarrelX1 : m.BarrelX0;
        double pw = Math.Max(0.8, t.BarrelW * 4), ph = Math.Max(0.6, t.BarrelW * 3);
        o.Add(P.Ln(new PathNode(Painter.RrectPath(m.X - pw / 2, -(zt + ph / 2), m.X + pw / 2, -(zt - ph / 2), 0.2, 0.2))
            .Fill(Painter.Shade(P.C("hull"), 0.55)), 0.7));
        if (Math.Abs(muzzle - m.X) > pw / 2)
            Barrel(o, P, t, m.X, muzzle, zt);
    }

    /// <summary>A light AA mount: its tub, the shield and the barrels raised a little.</summary>
    static void Aa(List<Node> o, Painter P, SideView.Aa a)
    {
        var cfg = Geometry.AaCfg[a.Type];
        double x = a.X, b = a.Base, h = a.Top - a.Base, r = a.R;
        double tub = Math.Min(0.9, 0.45 * h);
        var tubD = Rect(x - r, x + r, b, b + tub);
        double c = Math.Cos(double.DegreesToRadians(a.Dir));
        int f = c >= 0 ? 1 : -1;
        double zt = b + tub + 0.35 * (h - tub), ang = double.DegreesToRadians(20);
        double ln = cfg.BarrelLen * Math.Max(0.35, Math.Abs(c));
        o.Add(new LineNode(x, -zt, x + f * ln * Math.Cos(ang), -(zt + ln * Math.Sin(ang))).Stroke(P.C("barrel"), Math.Max(cfg.BarrelW * 1.2, P.Sw * 1.2)));
        var shield = Poly(new(x - f * 0.45 * r, b + tub), new(x + f * 0.45 * r, b + tub), new(x + f * 0.3 * r, zt + 0.35 * (h - tub)),
            new(x - f * 0.45 * r, zt + 0.35 * (h - tub)));
        o.Add(P.Ln(new PathNode(shield).Fill(P.C("turret")), 0.7));
        o.Add(P.Ln(new PathNode(tubD).Fill(P.C("tub"))));
        o.Add(Rim(P, tubD, P.C("tub")));
    }

    /// <summary>The flight deck's edge, or a sponson with a knee under it.</summary>
    static void Platform(List<Node> o, Painter P, SideView.Platform p, SideView v)
    {
        var b = p.B;
        if (p.Kind == "flight_deck")
        {
            // pillars carry it where no hangar does, as on an open-ended flight deck
            var g = new Group().Stroke(Painter.Shade(P.C("hull"), 0.8), Math.Max(0.35, P.Sw * 2));
            for (double x = b.X0 + 4; x < b.X1 - 2; x += 8)
            {
                if (v.Hangars.Any(h => h.X0 - 1 <= x && x <= h.X1 + 1))
                    continue;
                double deck = v.DeckAt(x);
                if (x > v.HullX0 && x < v.HullX1 && b.Base - deck > 0.5)
                    g.Items.Add(new LineNode(x, -deck, x, -b.Base));
            }
            o.Add(g);
            Box(o, P, b, P.C("flight_deck"), 1.3);
            return;
        }
        string col = p.Elevator ? P.C("flight_deck") : P.C("deck");
        double kd = Math.Min(1.6, 0.6 * b.L);
        var knee = Poly(new(b.X0 + 0.15 * b.L, b.Base), new(b.X1 - 0.15 * b.L, b.Base), new(b.Xc, b.Base - kd));
        o.Add(P.Ln(new PathNode(knee).Fill(Painter.Shade(P.C("hull"), 1.1)), 0.7));
        Box(o, P, b, col, 0.9);
    }

    /// <summary>A boat at its davits: the hull below its gunwale.</summary>
    static void Boat(List<Node> o, Painter P, Boat bt)
    {
        double top = bt.Top ?? Layout.LevelH + 1.5, h = Math.Min(1.4, 0.45 * bt.W + 0.3), l = bt.L / 2;
        var d = new PathData().M(bt.X - l, -top).L(bt.X + l, -top).C(bt.X + l * 0.85, -(top - h * 0.8), bt.X + l * 0.5, -(top - h), bt.X, -(top - h))
            .C(bt.X - l * 0.6, -(top - h), bt.X - l, -(top - h * 0.6), bt.X - l, -top).Z();
        string col = P.C("boat");
        o.Add(P.Ln(new PathNode(d).Fill(col)));
        o.Add(new LineNode(bt.X - l + 0.3, -(top - 0.25), bt.X + l - 0.3, -(top - 0.25)).Stroke(Painter.Shade(col, 0.82), P.Sw));
    }

    /// <summary>A revolving crane: the post, the cab and the jib toward its bearing, topped up.</summary>
    static void Crane(List<Node> o, Painter P, Crane c)
    {
        string col = P.C("crane");
        double cab = Math.Max(1.0, c.R * 0.8), zc = c.Top - cab;
        double reach = c.Jib * Math.Max(0.25, Math.Abs(Math.Cos(double.DegreesToRadians(c.Dir)))) * (Math.Cos(double.DegreesToRadians(c.Dir)) >= 0 ? 1 : -1);
        o.Add(P.Ln(new PathNode(Rect(c.X - 0.35 * c.R, c.X + 0.35 * c.R, 0, zc)).Fill(Painter.Shade(col, 1.3)), 0.7));
        o.Add(new LineNode(c.X, -zc, c.X + reach, -(zc + Math.Abs(reach) * 0.5)).Stroke(col, Math.Max(0.45, c.R * 0.4)).Cap("round"));
        o.Add(P.Ln(new PathNode(Rect(c.X - c.R, c.X + c.R, zc, c.Top)).Fill(Painter.Shade(col, 1.6)), 0.7));
    }

    /// <summary>A cargo hatch's coaming and its tarpaulin.</summary>
    static void Hatch(List<Node> o, Painter P, Hatch h)
    {
        o.Add(P.Ln(new PathNode(Rect(h.X - h.L / 2, h.X + h.L / 2, 0, 1.2)).Fill(P.C("hatch_coaming"))));
        o.Add(new PathNode(Rect(h.X - h.L / 2 + 0.2, h.X + h.L / 2 - 0.2, 1.0, 1.2)).Fill(P.C("hatch")));
    }

    // ------------------------------------------------------------------ under the water

    static void Rudder(List<Node> o, Painter P, SideView.Box r)
    {
        var d = Painter.RrectPath(r.X0, -r.Top, r.X1, -r.Base, Math.Min(0.4, r.L * 0.3), Math.Min(0.8, r.L * 0.4));
        o.Add(P.Ln(new PathNode(d).Fill(P.C("bottom"))));
        o.Add(Rim(P, d, P.C("bottom")));
    }

    static void Shaft(List<Node> o, Painter P, SideView.Shaft s)
    {
        string col = Painter.Shade(P.C("bottom"), 0.8);
        o.Add(new LineNode(s.X1, -s.Z1, s.X0, -s.Z0).Stroke(P.C("line"), 2 * s.R + 2 * P.Sw).Cap("round"));
        o.Add(new LineNode(s.X1, -s.Z1, s.X0, -s.Z0).Stroke(col, 2 * s.R).Cap("round"));
    }

    /// <summary>A propeller seen across its shaft: the blades edge-on round the boss.</summary>
    static void Propeller(List<Node> o, Painter P, SideView.Propeller p)
    {
        string col = P.C("propeller");
        o.Add(P.Ln(new EllipseNode(p.X, -p.Z, Math.Max(0.25, 0.2 * p.R), p.R).Fill(col)));
        o.Add(P.Ln(new EllipseNode(p.X, -p.Z, Math.Max(0.3, 0.22 * p.R), Math.Max(0.25, 0.18 * p.R)).Fill(Painter.Shade(col, 0.8)), 0.6));
    }
}
