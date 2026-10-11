namespace Fleetwright.Shipgen.Render;

/// <summary>The profile: the ship from the starboard side, drawn the way HullArt draws the plan (the same palette keys,
/// outline, rim light, dazzle scheme and scale rules), so the two read as one drawing. Ship metres with y = -height above
/// the main deck. Parts are drawn far to near (by their starboard edge), the hull among them at half the beam: what is
/// inside the hull's side hides behind it, what stands outside it (casemate guns, sponsons, the flight deck) in
/// front.</summary>
public static class SideArt
{
    /// <summary>A part to draw: its depth (larger is nearer) and its drawing.</summary>
    readonly record struct Item(double Depth, int Order, Action<List<Node>> Draw);

    /// <summary>What every part's drawing needs: the painter, the view, the dazzle scheme (null: none), the turret look,
    /// the blocks that carry a bridge's windows, and whether this is the silhouette (no rails: a hairline fence isn't
    /// part of a recognition outline).</summary>
    sealed record Ctx(Painter P, SideView V, DazzleScheme? Dazzle, string TurretLook, HashSet<string> Bridges, bool Silhouette);

    /// <summary>The profile of v at scale px/m on a canvas from x0 to x1 and from top down to bottom (heights above the
    /// main deck). above: only what is above the waterline (the silhouette's cut).</summary>
    public static Scene Build(SideView v, Palette pal, Shapes sh, string turretLook, double scale, double x0, double x1, double top, double bottom,
        bool above = false)
    {
        var P = new Painter(pal, scale, sh);
        var dz = sh.Dazzle == true && pal.Has("camo") ? DazzleScheme.Of(v.Id, v.Length, v.Beam, pal.Colours("camo")) : null;
        // a bridge's windows go on the top level of each control position
        var bridges = v.Blocks.Where(b => b.Role is "bridge" or "aft_control" or "island").GroupBy(b => b.Role)
            .Select(g => g.MaxBy(b => b.B.Top)!.Id).ToHashSet(StringComparer.Ordinal);
        var c = new Ctx(P, v, dz, turretLook, bridges, above);
        var scene = new Scene(x0, -top, x1 - x0, top - bottom, scale);
        var items = new List<Item>();
        void Add(double depth, Action<List<Node>> draw) => items.Add(new Item(depth, items.Count, draw));
        double hullDepth = v.Beam / 2;
        double wl = v.Waterline;

        Add(hullDepth, o => Hull(o, c));
        foreach (var h in v.Hangars)   // the hangar's side, up to the flight deck when it stands on it
        {
            double up = v.Platforms.Where(p => p.Kind == "flight_deck" && p.B.Base >= h.Top && p.B.Base - h.Top < 1.5).Select(p => p.B.Base)
                .DefaultIfEmpty(h.Top).Min();
            var hb = h with { Top = up };
            Add(h.Y1, o =>
            {
                Box(o, P, hb, P.C("hull"));
                Dazzled(o, c, Rect(hb.X0, hb.X1, hb.Base, hb.Top), hb.Y1, hb.Top, hb.Base, 1.0);
            });
        }
        foreach (var cs in v.Casings)
            Add(cs.Y1, o => Box(o, P, cs, Painter.Shade(P.C("deck"), 0.9), 0.8));
        foreach (var b in v.Barbettes)
            Add(b.B.Y1, o => Box(o, P, b.B, P.C("barbette")));
        if (v.ConningTower is { } ct)
            Add(ct.Y1, o => ConningTower(o, P, ct));
        foreach (var b in v.Blocks)
            Add(b.B.Y1, o => Block(o, c, b));
        foreach (var f in v.Funnels)
            Add(f.B.Y1, o => Funnel(o, c, f));
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
            Add(m.B.Y1, o => Turret(o, c, m, t));
        }
        foreach (var a in v.AaMounts)
            Add(a.Y + a.R, o => Aa(o, P, a));
        foreach (var p in v.Platforms)
            Add(p.B.Y1, o => Platform(o, P, p, v));
        foreach (var bt in v.Dressing.Boats ?? [])
            Add(bt.Y + bt.W / 2, o => Boat(o, P, v, bt));
        foreach (var cr in v.Dressing.Cranes ?? [])
            Add(cr.Y + cr.R, o => Crane(o, P, cr));
        foreach (var ht in v.Dressing.Hatches ?? [])
            Add(ht.Y + ht.W / 2, o => Hatch(o, P, ht));
        // under the water: the starboard and centreline gear in front of the hull, the port gear behind it
        double Under(double y) => y >= -0.01 ? hullDepth + 0.5 : -hullDepth;
        foreach (var r in v.Rudders)
            Add(Under((r.Y0 + r.Y1) / 2), o => Rudder(o, P, v, r));
        foreach (var s in v.Shafts)
            Add(Under(s.Y), o => Shaft(o, P, v, s));
        foreach (var pr in v.Propellers)
            Add(Under(pr.Y), o => Propeller(o, P, pr));

        var drawn = new List<Node>();
        foreach (var it in items.OrderBy(i => i.Depth).ThenBy(i => i.Order))
            it.Draw(drawn);
        if (above)
            scene.Root.Items.Add(new Group(drawn) { Clip = [new RectNode(x0, -top, x1 - x0, top - wl)] });
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

    /// <summary>The dazzle panels over the outline d of a surface whose top edge is at y = edge and height top, down to
    /// bottom (DazzleScheme.Side: they meet the plan's panels across that edge).</summary>
    static void Dazzled(List<Node> o, Ctx c, PathData d, double edge, double top, double bottom, double opacity) =>
        Dazzled(o, c, d, _ => edge, top, bottom, opacity);

    static void Dazzled(List<Node> o, Ctx c, PathData d, Func<double, double> edge, double top, double bottom, double opacity)
    {
        if (c.Dazzle is not { } dz || opacity <= 0)
            return;
        var g = new Group { Clip = [new PathNode(d)] };
        if (opacity < 1)
            g.Opacity(opacity);
        foreach (var (pts, col) in dz.Side(edge, top, bottom))
            g.Items.Add(new PathNode(Painter.Poly(pts)).Fill(col));
        o.Add(g);
    }

    /// <summary>A small round window or scuttle: dark glass in a lighter rim, so it shows on a dark side too.</summary>
    static void Scuttle(List<Node> o, Painter P, double x, double z, string wall, double r = 0.17)
    {
        o.Add(new CircleNode(x, -z, r).Fill(Painter.Shade(wall, 0.35)).Stroke(Painter.Shade(wall, 1.35), P.Sw * 0.5));
    }

    // ------------------------------------------------------------------ the hull

    /// <summary>The hull's side: the topsides in the hull colour under the dazzle, lighter bands where the sides lean in
    /// (as the plan shades them), the bottom in anti-fouling under a boot-topping band at the waterline, the scuttles,
    /// hawse and anchor, the hull number, the outline, and the rails along the open decks.</summary>
    static void Hull(List<Node> o, Ctx c)
    {
        var (P, v) = (c.P, c.V);
        var d = Painter.Poly(Up(v.Hull));
        string hull = P.C("hull");
        o.Add(new PathNode(d).Fill(hull));
        double wl = v.Waterline, x0 = v.HullX0 - 1, x1 = v.HullX1 + 1, top = v.Hull.Max(p => p.Y) + 1;
        Dazzled(o, c, d, v.Planform.HalfWidth, 0.0, v.Keel, 1.0);   // the plan's rim: the hull at its widest
        double band = Math.Min(0.6, 0.12 * -wl);   // the boot top: a painted band, narrower on a low hull
        var side = new Group { Clip = [new PathNode(d)] };
        if (v.Knuckle is double k && k > wl)
        {
            // the bands the plan lightens (TopView.TumblehomeShades of the way up from the knuckle to the deck), the same
            // white over each
            foreach (var f in TopView.TumblehomeShades)
                side.Items.Add(new PathNode(Rect(x0, x1, k * (1 - f), top)).Fill("#ffffff").FillOp(HullArt.TumblehomeLight));
            side.Items.Add(new LineNode(x0, -k, x1, -k).Stroke(Painter.Shade(hull, 0.75), P.Sw * 0.8).StrokeOp(0.6));
        }
        side.Items.Add(new PathNode(Rect(x0, x1, v.Keel - 1, wl)).Fill(P.C("bottom")));
        side.Items.Add(new PathNode(Rect(x0, x1, wl - band / 2, wl + band)).Fill(P.C("boot_top")));
        o.Add(side);
        Scuttles(o, c);
        Anchor(o, c);
        HullNumber(o, c);
        o.Add(P.Ln(new PathNode(d).Fill("none"), 1.4));
        o.Add(Rim(P, d, hull));
        if (!c.Silhouette)
            Rails(o, c);
    }

    /// <summary>The stretches of the deck line: (x0, x1, height), stern to stem.</summary>
    static IEnumerable<(double X0, double X1, double H)> Decks(SideView v)
    {
        var breaks = v.Raised.SelectMany(r => new[] { r.X0, r.X1 }).Where(x => v.HullX0 < x && x < v.HullX1).Append(v.HullX0).Append(v.HullX1)
            .Distinct().Order().ToList();
        for (int i = 0; i + 1 < breaks.Count; i++)
            yield return (breaks[i], breaks[i + 1], v.DeckAt((breaks[i] + breaks[i + 1]) / 2));
    }

    /// <summary>Where the hawse pipe is (the plan's anchor chains run to it), or null without chains.</summary>
    static double? HawseX(SideView v) => v.Dressing.ChainX is double cx && cx != 0 ? v.Length / 2 - (v.Dressing.HawseBack ?? 6.0) : null;

    /// <summary>Rows of scuttles along the side: one a deck below each open deck, and one more a deck lower while it
    /// stays clear of the water; none through the side armour, the casemate ports or the hawse, nor in the fine ends.</summary>
    static void Scuttles(List<Node> o, Ctx c)
    {
        var (P, v) = (c.P, c.V);
        double wl = v.Waterline, pitch = Geometry.DeckPitch, step = 3.0;
        double lo = v.HullX0 + 0.07 * v.Length, hi = Math.Min(v.HullX1 - 0.12 * v.Length, (HawseX(v) ?? 1e9) - 2.0);
        var casemates = v.Mounts.Where(m => m.Casemate && m.Y > 0).Select(m => (m.X, Z: m.B.Base + 0.45 * m.B.H)).ToList();
        string hull = P.C("hull");
        bool Clear(double x, double z) =>
            z > wl + 0.9
            && !v.SideArmour.Any(a => a.X0 - 0.5 <= x && x <= a.X1 + 0.5 && a.Bottom - 0.3 <= z && z <= a.Top + 0.3)
            && !casemates.Any(m => Math.Abs(m.X - x) < 1.6 && Math.Abs(m.Z - z) < 1.6);
        void Row(double xa, double xb, double z)
        {
            for (double x = Math.Ceiling(Math.Max(xa, lo) / step) * step; x <= Math.Min(xb, hi); x += step)
                if (Clear(x, z))
                    Scuttle(o, P, x, z, hull);
        }
        for (int i = 1; i <= 2; i++)
            Row(v.HullX0, v.HullX1, -1.0 - (i - 1) * pitch);
        foreach (var (xa, xb, h) in Decks(v).Where(dk => dk.H > 0))
            Row(xa + 1.0, xb - 1.0, h - 1.0);
    }

    /// <summary>The hawse pipe below the deck edge near the bow, and the stockless anchor hanging from it.</summary>
    static void Anchor(List<Node> o, Ctx c)
    {
        var (P, v) = (c.P, c.V);
        if (HawseX(v) is not double xh || xh < v.HullX0)
            return;
        double k = Math.Clamp(v.Length / 180, 0.4, 1.4), deck = v.DeckAt(xh);
        double zh = deck - 0.9 * k, zb = zh - 1.9 * k;
        if (zb < v.Waterline + 0.3)
            return;
        string chain = P.C("chain");
        o.Add(new EllipseNode(xh, -zh, 0.45 * k, 0.35 * k).Fill(Painter.Shade(P.C("hull"), 0.35)));
        o.Add(P.Ln(new PathNode(Rect(xh - 0.13 * k, xh + 0.13 * k, zb + 0.2 * k, zh)).Fill(chain), 0.6));
        o.Add(P.Ln(new PathNode(Poly(new(xh - 0.75 * k, zb + 0.55 * k), new(xh - 0.55 * k, zb + 0.62 * k), new(xh, zb + 0.22 * k),
            new(xh + 0.55 * k, zb + 0.62 * k), new(xh + 0.75 * k, zb + 0.55 * k), new(xh, zb - 0.05 * k))).Fill(chain), 0.6));
    }

    /// <summary>The look's hull number (shapes hull_number) on the bow, the plan's number from the same source.</summary>
    static void HullNumber(List<Node> o, Ctx c)
    {
        var (P, v) = (c.P, c.V);
        if (P.Shapes.HullNumber != true || v.Platforms.Any(p => p.Kind == "flight_deck"))
            return;
        string text = HullArt.HullNumber(v.Id, P.Shapes);
        double x = v.HullX1 - 0.13 * v.Length, deck = v.DeckAt(x), fb = deck - v.Waterline;
        double size = Math.Min(0.42 * fb, 0.045 * v.Length);
        if (size < 0.8)
            return;
        o.Add(new TextNode(x, -(deck - 0.45 * fb), size, text).Fill(P.P.Or("number", P.C("marking"))).FillOp(0.92));
    }

    /// <summary>Guard rails along the open decks: a rail a metre up on stanchions, stopping at a raised deck's break and
    /// over a hangar's side.</summary>
    static void Rails(List<Node> o, Ctx c)
    {
        var (P, v) = (c.P, c.V);
        var g = new Group().Stroke(P.C("line"), Math.Max(P.Sw * 0.6, 0.05)).StrokeOp(0.6);
        foreach (var (xa, xb, h) in Decks(v))
        {
            double a = xa + 0.3, b = xb - 0.3;
            foreach (var hg in v.Hangars.Where(hg => h <= hg.Base + 0.1).OrderBy(hg => hg.X0))
            {
                if (hg.X1 < a || hg.X0 > b)
                    continue;
                if (hg.X0 - 0.3 > a)
                    RailRun(g, a, hg.X0 - 0.3, h);
                a = Math.Max(a, hg.X1 + 0.3);
            }
            if (b > a)
                RailRun(g, a, b, h);
        }
        o.Add(g);
    }

    static void RailRun(Group g, double a, double b, double h)
    {
        if (b - a < 1.0)
            return;
        g.Items.Add(new LineNode(a, -(h + 1.0), b, -(h + 1.0)));
        g.Items.Add(new LineNode(a, -(h + 0.5), b, -(h + 0.5)));
        int n = Math.Max(1, (int)Math.Round((b - a) / 1.5));
        for (int i = 0; i <= n; i++)
        {
            double x = a + (b - a) * i / n;
            g.Items.Add(new LineNode(x, -h, x, -(h + 1.0)));
        }
    }

    // ------------------------------------------------------------------ upperworks

    /// <summary>A superstructure block in its level's colour under the dazzle; the top level of a bridge carries its
    /// window band, other deckhouses a row of scuttles.</summary>
    static void Block(List<Node> o, Ctx c, SideView.Block b)
    {
        var P = c.P;
        if (b.Director is { } dr)
        {
            Director(o, c, b, dr);
            return;
        }
        var bx = b.B;
        if (bx.H <= 0 || bx.L <= 0)
            return;
        string col = Level(P, b.Level);
        var d = Rect(bx.X0, bx.X1, bx.Base, bx.Top);
        o.Add(P.Ln(new PathNode(d).Fill(col)));
        Dazzled(o, c, d, bx.Y1, bx.Top, bx.Base, P.Shapes.DazzleUpperworks ?? 1.0);
        if (c.Bridges.Contains(b.Id) && bx.H >= 1.6 && bx.L > 1.6)   // the bridge's windows, mullioned
        {
            double z0 = bx.Top - 1.05, z1 = bx.Top - 0.55, xa = bx.X0 + 0.3, xb = bx.X1 - 0.3;
            o.Add(new PathNode(Rect(xa, xb, z0, z1)).Fill(P.C("line")));
            var mull = new Group().Stroke(col, P.Sw * 0.8);
            for (double x = xa + 0.9; x < xb - 0.2; x += 0.9)
                mull.Items.Add(new LineNode(x, -z0, x, -z1));
            o.Add(mull);
        }
        else if (bx.H >= 1.8 && bx.L > 3.0)
            for (double x = bx.X0 + 1.1; x <= bx.X1 - 1.1; x += 2.2)
                Scuttle(o, P, x, bx.Base + 0.62 * bx.H, col, 0.15);
        o.Add(P.Ln(new PathNode(d).Fill("none")));
        o.Add(Rim(P, d, col));
    }

    /// <summary>A director: the hood, a shade lighter than the roof it stands on as in the plan; the rangefinder runs
    /// across the ship, so it shows end-on, its hood a disc on the side; a radar aerial on a short post above.</summary>
    static void Director(List<Node> o, Ctx c, SideView.Block b, TopView.Director dr)
    {
        var P = c.P;
        string col = Level(P, dr.On + 1);
        var bx = b.B;
        if (dr.RangefinderM == 0)   // a gyro sight in an open tub
        {
            double h = Math.Min(bx.H, 1.2);
            Box(o, P, bx with { Top = bx.Base + h }, P.C("tub"));
            return;
        }
        var d = Rect(bx.X0, bx.X1, bx.Base, bx.Top);
        o.Add(P.Ln(new PathNode(d).Fill(col)));
        Dazzled(o, c, d, bx.Y1, bx.Top, bx.Base, P.Shapes.DazzleUpperworks ?? 1.0);
        o.Add(Rim(P, d, col));
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

    /// <summary>A funnel: the casing (raked aft by the look's rake, as the plan displaces its top) under the dazzle, the
    /// painted band and the cap the look gives it.</summary>
    static void Funnel(List<Node> o, Ctx c, SideView.Funnel f)
    {
        var P = c.P;
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
        Dazzled(o, c, casing, b.Y1, b.Top, b.Base, sh.DazzleUpperworks ?? 1.0);
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

    // ------------------------------------------------------------------ guns

    /// <summary>How an armoured turret look stands up from the side: the face's knee (a fraction of the height) and the
    /// slope back to the roof (a fraction of the length, at most a fraction of the height), and the rear's top corner
    /// rounded or cut (a fraction of the height).</summary>
    readonly record struct Profile(double Knee, double SlopeL, double SlopeH, double RearRound, double RearCut);

    static readonly Dictionary<string, Profile> Profiles = new(StringComparer.Ordinal)
    {
        ["standard"] = new(0.55, 0.3, 0.6, 0, 0),
        ["slab"] = new(0.85, 0.08, 0.15, 0, 0),
        ["round"] = new(0.55, 0.25, 0.5, 0.5, 0),
        ["classic"] = new(0.5, 0.22, 0.5, 0.35, 0),
        ["faceted"] = new(0.5, 0.25, 0.55, 0, 0.35),
        ["lancia"] = new(0.45, 0.35, 0.7, 0.55, 0),
        ["quadruple"] = new(0.6, 0.2, 0.5, 0, 0.25),
        ["drum"] = new(1, 0, 0, 0, 0),
    };

    /// <summary>What a look puts on an armoured turret's roof, seen from the side: (from, to) along the turret in units
    /// of its radius (+ toward the face), how tall in units of its height, and rounded (a cupola) or square (a hood).
    /// The plan's hoods and cupolas (TurretArt) at the same places.</summary>
    static readonly Dictionary<string, (double A, double B, double H, bool Round)[]> Roofs = new(StringComparer.Ordinal)
    {
        ["standard"] = [(0.2, 0.42, 0.12, false)],
        ["slab"] = [(0.3, 0.48, 0.1, false), (-0.6, -0.32, 0.12, false)],
        ["round"] = [(0.2, 0.4, 0.14, true)],
        ["classic"] = [(0.15, 0.45, 0.12, true), (-0.3, -0.08, 0.08, false)],
        ["faceted"] = [(0.13, 0.37, 0.14, true), (-0.62, -0.42, 0.14, false)],
        ["lancia"] = [(0.14, 0.46, 0.1, true)],
        ["quadruple"] = [(0.18, 0.5, 0.12, false), (-0.85, -0.67, 0.1, false)],
        ["drum"] = [(0.42, 0.62, 0.12, false)],
    };

    /// <summary>A gun mount at its rest bearing: the gunhouse in the look's way, its face toward the barrels, and the
    /// barrels from the face to the muzzles (side by side, they show as one).</summary>
    static void Turret(List<Node> o, Ctx c, SideView.Mount m, TurretType t)
    {
        var P = c.P;
        var b = m.B;
        double h = b.H;
        if (h <= 0)
            return;
        double cos = Math.Cos(double.DegreesToRadians(m.Rest));
        int f = cos >= 0 ? 1 : -1;
        bool beam = Math.Abs(cos) < 0.5;
        string look = t.Shape == "bb" ? c.TurretLook : "";
        double r = t.R;
        // the body's ends along the ship: the look's own outline, else the hitbox's
        double front = f > 0 ? b.X1 : b.X0, back = f > 0 ? b.X0 : b.X1;
        if (t.Shape == "bb" && look != "standard" && !beam)
        {
            var (pts, _, _, _) = TurretArt.LookTurretBody(look, r);
            (front, back) = (m.X + f * pts.Max(p => p.X), m.X + f * pts.Min(p => p.X));
        }
        double len = Math.Abs(front - back), xc = (front + back) / 2;
        double zt = b.Base + 0.42 * h;   // the trunnions
        double muzzle = f > 0 ? m.BarrelX1 : m.BarrelX0;
        string col = t.Shape is "torp" or "tube" ? P.C("tube") : P.C("turret");
        double X(double local) => m.X + f * local * r;   // a point along the turret, in units of r from its pivot

        if (t.Shape is "torp" or "tube")
        {
            Tubes(o, c, m, t, beam, f);
            return;
        }
        if (!beam && Math.Abs(muzzle - front) > 0.1)
            Barrel(o, P, t, front - f * 0.4, muzzle, zt);
        if (beam)   // seen end-on: a box with both top corners eased, the muzzles toward us
        {
            double e = Math.Min(0.25 * h, 0.15 * b.L);
            var d = Poly(new(b.X0, b.Base), new(b.X1, b.Base), new(b.X1, b.Top - e), new(b.X1 - e, b.Top), new(b.X0 + e, b.Top), new(b.X0, b.Top - e));
            o.Add(P.Ln(new PathNode(d).Fill(col)));
            o.Add(Rim(P, d, col));
            o.Add(P.Ln(new CircleNode(b.Xc, -zt, Math.Max(t.BarrelW * 0.6, 0.12)).Fill(P.C("barrel")), 0.6));
            return;
        }
        if (t.Shape == "open")   // a shield over the gun on its pedestal
        {
            double sf = front, sb = back + f * 0.45 * len;
            o.Add(P.Ln(new PathNode(Rect(xc - 0.15 * len, xc + 0.15 * len, b.Base, zt)).Fill(P.C("tub")), 0.7));
            var d = Poly(new(sb, b.Base + 0.2 * h), new(sf, b.Base + 0.2 * h), new(sf, b.Top - 0.25 * h), new(sf - f * 0.25 * len, b.Top), new(sb, b.Top));
            o.Add(P.Ln(new PathNode(d).Fill(col)));
            o.Add(Rim(P, d, col));
            return;
        }
        if (t.Shape == "dp")   // a dual-purpose gunhouse: a rounder face
        {
            Gunhouse(o, P, col, f, front, back, b.Base, b.Top, new Profile(0.4, 0.4, 0.8, 0.25, 0));
            return;
        }
        switch (look)
        {
            case "hooded":   // an open barbette's ring, the guns under a pear-shaped hood
                {
                    double wall = b.Base + 0.5 * h;
                    var ring = Rect(back, front, b.Base, wall);
                    o.Add(P.Ln(new PathNode(ring).Fill(col)));
                    o.Add(Rim(P, ring, col));
                    string hood = P.P.Or("turret_hood", Painter.Shade(col, 1.35));
                    double ha = X(-0.55), hb = X(0.72);
                    var d = new PathData().M(ha, -wall).L(hb, -wall)
                        .C(hb, -(wall + 0.6 * (b.Top - wall)), (ha + hb) / 2 + f * 0.25 * r, -b.Top, (ha + hb) / 2, -b.Top)
                        .C(ha + f * 0.1 * r, -b.Top, ha, -(wall + 0.5 * (b.Top - wall)), ha, -wall).Z();
                    o.Add(P.Ln(new PathNode(d).Fill(hood), 0.8));
                    o.Add(Rim(P, d, hood));
                    return;
                }
            case "champignon":   // a drum under an overhanging mushroom roof, a conical sighting hood on top
                {
                    double z1 = b.Base + 0.78 * h, z2 = b.Base + 0.92 * h;
                    var drum = Rect(back, front, b.Base, z1);
                    o.Add(P.Ln(new PathNode(drum).Fill(col)));
                    o.Add(Rim(P, drum, col));
                    double lip = 0.08 * len, xa = Math.Min(back, front) - lip, xb = Math.Max(back, front) + lip;
                    var roof = Painter.RrectPath(xa, -z2, xb, -z1, 0.15 * len, 0.15 * len);
                    o.Add(P.Ln(new PathNode(roof).Fill(Painter.Shade(col, 0.85))));
                    o.Add(Rim(P, roof, Painter.Shade(col, 0.85)));
                    double cx = X(-0.05);
                    var cone = Poly(new(cx - 0.3 * r, z2), new(cx + 0.3 * r, z2), new(cx + 0.15 * r, b.Top + 0.1 * h), new(cx - 0.15 * r, b.Top + 0.1 * h));
                    o.Add(P.Ln(new PathNode(cone).Fill(Painter.Shade(col, 1.15)), 0.7));
                    return;
                }
        }
        var prof = Profiles.GetValueOrDefault(look, Profiles["standard"]);
        foreach (var (a, bb, hh, round) in Roofs.GetValueOrDefault(look, Roofs["standard"]))   // hoods and cupolas, under the roof's outline
        {
            double xa = Math.Min(X(a), X(bb)), xb = Math.Max(X(a), X(bb)), zr = b.Top - 0.05 * h, zh = b.Top + hh * h;
            var d = round ? new PathData().M(xa, -zr).C(xa, -zh, xb, -zh, xb, -zr).Z() : Painter.RrectPath(xa, -zh, xb, -zr, 0.05 * h, 0.05 * h);
            o.Add(P.Ln(new PathNode(d).Fill(Painter.Shade(col, 0.85)), 0.6));
        }
        Gunhouse(o, P, col, f, front, back, b.Base, b.Top, prof);
        if (look == "drum")   // the roof's lip
            o.Add(P.Ln(new PathNode(Rect(Math.Min(front, back) - 0.08, Math.Max(front, back) + 0.08, b.Top - 0.12 * h, b.Top)).Fill(Painter.Shade(col, 1.1)), 0.7));
        // rangefinder ears stand out at the turret's sides near the rear: seen across, a box on the side
        var parts = look == "standard" ? Geometry.TurretShapesOf(t).Parts : TurretArt.LookTurretBody(look, r).Parts;
        foreach (var part in parts.Where(p => p.Min(q => q.Y) < 0).Take(1))
        {
            double xa = Math.Min(m.X + f * part.Min(q => q.X), m.X + f * part.Max(q => q.X)), xb = Math.Max(m.X + f * part.Min(q => q.X), m.X + f * part.Max(q => q.X));
            var d = Rect(xa, xb, b.Base + 0.4 * h, b.Base + 0.75 * h);
            o.Add(P.Ln(new PathNode(d).Fill(Painter.Shade(col, 0.85)), 0.7));
        }
    }

    /// <summary>A torpedo mount, standing on what is under it: trainable tubes on a turntable over a pedestal from the
    /// deck (or roof), seen end-on as a row of muzzles when trained on the beam, or as the stacked tubes with their
    /// breech and muzzle bands and the training cab when trained fore and aft; a fixed tube lies in cradles on its
    /// deck.</summary>
    static void Tubes(List<Node> o, Ctx c, SideView.Mount m, TurretType t, bool beam, int f)
    {
        var P = c.P;
        var b = m.B;
        string tube = P.C("tube"), tub = P.C("tub");
        double foot = c.V.SupportAt(m.X, m.Y, b.Base);
        double r = Math.Max(t.BarrelW / 2, 0.15);
        double zt = Math.Max(b.Base + r + 0.15, b.Base + 0.55 * b.H);   // the tubes' axis
        if (t.Shape == "tube" || t.IsFixedTube)
        {
            zt = foot + r + 0.25;
            double xa = Math.Min(m.BarrelX0, b.X0), xb = Math.Max(m.BarrelX1, b.X1);
            foreach (double u in new[] { 0.2, 0.75 })
            {
                double x = xa + (xb - xa) * u;
                o.Add(P.Ln(new PathNode(Poly(new(x - 0.35, foot), new(x + 0.35, foot), new(x + 0.2, zt), new(x - 0.2, zt))).Fill(tub), 0.6));
            }
            var d = Painter.RrectPath(xa, -(zt + r), xb, -(zt - r), r * 0.4, r * 0.4);
            o.Add(P.Ln(new PathNode(d).Fill(tube), 0.7));
            o.Add(Rim(P, d, tube));
            return;
        }
        // the pedestal from the deck to the turntable, the turntable as wide as the mount's ring
        double table = Math.Max(foot + 0.2, zt - r - 0.25), ring = Math.Min(t.R, 0.45 * b.L);
        o.Add(P.Ln(new PathNode(Poly(new(m.X - 0.55 * ring, foot), new(m.X + 0.55 * ring, foot), new(m.X + 0.35 * ring, table),
            new(m.X - 0.35 * ring, table))).Fill(Painter.Shade(tub, 0.9)), 0.7));
        o.Add(P.Ln(new PathNode(Rect(m.X - ring, m.X + ring, table - 0.18, table)).Fill(tub), 0.7));
        if (beam)   // trained on the beam: the muzzles in a row along the ship, in their frame
        {
            long n = Math.Max(1, t.Barrels);
            double sp = n > 1 ? Math.Max(t.Spacing, 2 * r + 0.05) : 0, x0 = m.X - (n - 1) / 2.0 * sp;
            o.Add(P.Ln(new PathNode(Rect(x0 - r - 0.1, x0 + (n - 1) * sp + r + 0.1, table, zt + r * 0.3)).Fill(Painter.Shade(tube, 0.85)), 0.6));
            for (int i = 0; i < n; i++)
            {
                double x = x0 + i * sp;
                o.Add(P.Ln(new CircleNode(x, -zt, r).Fill(tube), 0.6));
                o.Add(new CircleNode(x, -zt, r * 0.62).Fill(Painter.Shade(tube, 0.35)));
            }
            return;
        }
        // trained fore and aft: the tubes one behind another, muzzles toward f
        double xm = f > 0 ? Math.Max(b.X1, m.BarrelX1) : Math.Min(b.X0, m.BarrelX0);
        double xbr = f > 0 ? Math.Min(b.X0, m.BarrelX0) : Math.Max(b.X1, m.BarrelX1);
        var body = Painter.RrectPath(Math.Min(xm, xbr), -(zt + r), Math.Max(xm, xbr), -(zt - r), r * 0.5, r * 0.5);
        o.Add(P.Ln(new PathNode(body).Fill(tube)));
        o.Add(Rim(P, body, tube));
        foreach (double u in new[] { 0.12, 0.88 })   // breech and muzzle bands
        {
            double x = xbr + (xm - xbr) * u;
            o.Add(new PathNode(Rect(x - 0.12, x + 0.12, zt - r, zt + r)).Fill(Painter.Shade(tube, 0.75)));
        }
        double cab = xbr + (xm - xbr) * 0.2;
        o.Add(P.Ln(new PathNode(Rect(Math.Min(cab, cab + f * 1.2), Math.Max(cab, cab + f * 1.2), table, zt + r + 0.7)).Fill(Painter.Shade(tub, 1.1)), 0.7));
    }

    /// <summary>A gunhouse from back to front (f: the face's side), Base..Top, in a profile: the sloped face plate a
    /// shade darker, as the plan draws it.</summary>
    static void Gunhouse(List<Node> o, Painter P, string col, int f, double front, double back, double bse, double top, Profile p)
    {
        double h = top - bse, len = Math.Abs(front - back);
        double slope = Math.Min(p.SlopeL * len, p.SlopeH * h), knee = bse + p.Knee * h;
        var pts = new List<Pt> { new(back, bse), new(front, bse), new(front, knee) };
        if (slope > 0)
            pts.Add(new(front - f * slope, top));
        else
            pts.Add(new(front, top));
        if (p.RearRound > 0)   // the rear's top corner on a quarter circle
        {
            double rr = Math.Min(p.RearRound * h, 0.4 * len);
            for (int i = 0; i <= 6; i++)
            {
                double a = Math.PI / 2 * i / 6;
                pts.Add(new(back + f * rr * (1 - Math.Sin(a)), top - rr * (1 - Math.Cos(a))));
            }
        }
        else if (p.RearCut > 0)
        {
            double rc = Math.Min(p.RearCut * h, 0.3 * len);
            pts.Add(new(back + f * rc, top));
            pts.Add(new(back, top - rc));
        }
        else
            pts.Add(new(back, top));
        var d = Poly([.. pts]);
        o.Add(P.Ln(new PathNode(d).Fill(col)));
        if (slope > 0)   // the face plate, edge-on: a darker strip along the slope
        {
            double t = Math.Min(0.08 * len, 0.5);
            o.Add(new PathNode(Poly(new(front, knee), new(front - f * slope, top), new(front - f * (slope + t), top), new(front - f * t, knee)))
                .Fill(Painter.Shade(col, 0.82)));
        }
        o.Add(P.Ln(new PathNode(d).Fill("none")));
        o.Add(Rim(P, d, col));
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

    // ------------------------------------------------------------------ platforms and dressing

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

    /// <summary>A boat, held by what it stands on: on chocks when it sits close over its deck or roof, else in davits
    /// stepped there (from the side: the posts at its ends, their heads curving in over it, and the falls down to
    /// it).</summary>
    static void Boat(List<Node> o, Painter P, SideView v, Boat bt)
    {
        double top = bt.Top ?? Layout.LevelH + 1.5, h = Math.Min(1.4, 0.45 * bt.W + 0.3), l = bt.L / 2, keel = top - h;
        double foot = v.SupportAt(bt.X, bt.Y, keel);
        string mast = P.C("mast");
        if (keel - foot <= 1.2)   // chocks under the bilges
            foreach (double u in new[] { -0.45, 0.45 })
            {
                double x = bt.X + u * l, zc = keel + 0.2 * h;
                o.Add(P.Ln(new PathNode(Poly(new(x - 0.35, foot), new(x + 0.35, foot), new(x + 0.25, zc), new(x - 0.25, zc))).Fill(P.C("fitting")), 0.6));
            }
        else
        {
            double head = top + 0.9, w = Math.Max(0.16, P.Sw * 1.3);
            foreach (int s in new[] { -1, 1 })
            {
                double x = bt.X + s * (l + 0.15), xi = bt.X + s * (l - 0.5);
                o.Add(new PathNode(new PathData().M(x, -foot).L(x, -(head - 0.4)).C(x, -head, xi, -head, xi, -(head - 0.1)))
                    .Fill("none").Stroke(mast, w).Cap("round"));
                o.Add(new LineNode(xi, -(head - 0.1), xi, -top).Stroke(mast, w * 0.4));
            }
        }
        var d = new PathData().M(bt.X - l, -top).L(bt.X + l, -top).C(bt.X + l * 0.85, -(top - h * 0.8), bt.X + l * 0.5, -keel, bt.X, -keel)
            .C(bt.X - l * 0.6, -keel, bt.X - l, -(top - h * 0.6), bt.X - l, -top).Z();
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

    /// <summary>A rudder on its stock: the blade, and the stock from it up into the hull's bottom.</summary>
    static void Rudder(List<Node> o, Painter P, SideView v, SideView.Box r)
    {
        string col = P.C("bottom");
        double sw = Math.Max(0.3, 0.16 * r.L), xs = r.X1 - Math.Max(sw, 0.22 * r.L);   // the stock, toward the leading edge
        if (v.KeelAt(xs) is double hull && hull > r.Top - 0.3)
            o.Add(P.Ln(new PathNode(Rect(xs - sw / 2, xs + sw / 2, r.Top - 0.4, hull + 0.4)).Fill(Painter.Shade(col, 0.85)), 0.7));
        var d = Painter.RrectPath(r.X0, -r.Top, r.X1, -r.Base, Math.Min(0.4, r.L * 0.3), Math.Min(0.8, r.L * 0.4));
        o.Add(P.Ln(new PathNode(d).Fill(col)));
        o.Add(Rim(P, d, col));
    }

    /// <summary>A shaft outside the hull: the faired boss where it leaves the hull, the shaft, and the A-bracket carrying
    /// it from the hull's bottom just ahead of the propeller.</summary>
    static void Shaft(List<Node> o, Painter P, SideView v, SideView.Shaft s)
    {
        string col = Painter.Shade(P.C("bottom"), 0.8), line = P.C("line");
        void Bar(double xa, double za, double xb, double zb, double w)
        {
            o.Add(new LineNode(xa, -za, xb, -zb).Stroke(line, w + 2 * P.Sw).Cap("round"));
            o.Add(new LineNode(xa, -za, xb, -zb).Stroke(col, w).Cap("round"));
        }
        double len = Math.Abs(s.X1 - s.X0);
        if (len < 0.1)
            return;
        double dir = Math.Sign(s.X0 - s.X1);   // aft along the shaft: from where it leaves the hull to the propeller
        double Z(double x) => s.Z1 + (s.Z0 - s.Z1) * (x - s.X1) / (s.X0 - s.X1);
        Bar(s.X1, s.Z1, s.X0, s.Z0, 2 * s.R);
        // the bossing where it leaves the hull: a slender spindle round the shaft, tapering both ways
        double bl = Math.Min(4.0, 0.3 * len), br = 2.2 * s.R, xa0 = s.X1 - dir * 0.4 * bl, xb0 = s.X1 + dir * bl;
        var boss = new PathData().M(xa0, -Z(xa0))
            .C(xa0 + dir * 0.3 * bl, -(Z(xa0) + br), xb0 - dir * 0.6 * bl, -(Z(xb0) + br), xb0, -Z(xb0))
            .C(xb0 - dir * 0.6 * bl, -(Z(xb0) - br), xa0 + dir * 0.3 * bl, -(Z(xa0) - br), xa0, -Z(xa0)).Z();
        o.Add(P.Ln(new PathNode(boss).Fill(col), 0.7));
        // the A-bracket: a strut from the hull's bottom down to a bearing on the shaft, ahead of the propeller
        double xa = s.X0 - dir * Math.Max(1.2, 0.08 * len), xh = xa - dir * 0.8;
        if (v.KeelAt(xh) is double hull && hull > Z(xa) + 0.4)
        {
            double bw = Math.Min(0.5, 4 * s.R);   // the bearing's half-length, the strut's width: both follow the shaft
            Bar(xa, Z(xa), xh, hull + 0.3, 1.6 * s.R);
            o.Add(P.Ln(new PathNode(Painter.RrectPath(xa - bw, -(Z(xa) + 1.4 * s.R), xa + bw, -(Z(xa) - 1.4 * s.R), 0.4 * bw, 0.4 * bw)).Fill(col), 0.7));
        }
    }

    /// <summary>A propeller from the side on the shaft's end: the hub with its cap and a blade standing up and one down,
    /// mirror images (drawn symmetric by choice), each a broad leaf seen edge-on and swept back a little.</summary>
    static void Propeller(List<Node> o, Painter P, SideView.Propeller p)
    {
        string col = P.C("propeller");
        double x = p.X, z = p.Z, r = p.R, w = Math.Max(0.35, 0.36 * r);
        PathData Blade(double len)   // from the hub's axis out to len (up +, down -)
        {
            double s = Math.Sign(len), a = Math.Abs(len);
            return new PathData().M(x - w / 2, -z).C(x - w / 2, -(z + s * 0.55 * a), x - w * 0.15, -(z + s * a), x + w * 0.1, -(z + s * a))
                .C(x + w * 0.45, -(z + s * 0.9 * a), x + w / 2, -(z + s * 0.4 * a), x + w / 2, -z).Z();
        }
        o.Add(P.Ln(new PathNode(Blade(-r)).Fill(col), 0.7));
        o.Add(P.Ln(new PathNode(Blade(r)).Fill(col), 0.7));
        double hl = Math.Max(0.5, 0.42 * r), hh = Math.Max(0.35, 0.3 * r);
        var hub = new PathData().M(x - hl / 2, -(z + hh / 2)).L(x + hl * 0.2, -(z + hh / 2)).C(x + hl * 0.55, -(z + hh / 2), x + hl * 0.6, -z, x + hl * 0.6, -z)
            .C(x + hl * 0.6, -z, x + hl * 0.55, -(z - hh / 2), x + hl * 0.2, -(z - hh / 2)).L(x - hl / 2, -(z - hh / 2)).Z();
        o.Add(P.Ln(new PathNode(hub).Fill(Painter.Shade(col, 0.9)), 0.7));
    }
}
