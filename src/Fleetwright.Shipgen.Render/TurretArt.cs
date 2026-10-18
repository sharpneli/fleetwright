namespace Fleetwright.Shipgen.Render;

/// <summary>Turret sprites (shipgen.build_turret): one per turret type, pivot at the image centre, barrels along +x.</summary>
public static class TurretArt
{
    static readonly Dictionary<string, double> BarrelRoot = new(StringComparer.Ordinal)
    {
        ["bb"] = 0.5, ["dp"] = 0.3, ["open"] = -0.3, ["casemate"] = 0.0,
    };

    /// <summary>Half the canvas in metres. With a scale, snapped so the canvas is a multiple of align px (even at
    /// least) and the pivot lands exactly at the image centre.</summary>
    public static double Extent(TurretType t, double? scale = null, int align = 2)
    {
        double half = Math.Max(t.R * 1.15, Geometry.TurretReach(t)) + 1.0;
        if (scale is double s && s != 0)
        {
            double a = align / 2.0;
            half = Math.Ceiling(half * s / a) * a / s;
        }
        return half;
    }

    /// <summary>A look's drawing of an armoured (bb) turret, in units of r: (body outline, rangefinder parts, front
    /// face x, front face half-width). Drawing only: the hitbox keeps Geometry.TurretShapesOf, and each outline stays
    /// close to it. The rangefinder's ears or bar only with a rangefinder (rangefinderM, its base): in the look's style,
    /// reaching the hitbox bar's ends (the base + 0.5 m); a Victorian drum's sighting hoods are dressing, always.</summary>
    public static (List<Pt> Body, List<List<Pt>> Parts, double Xf, double Hf) LookTurretBody(string look, double r, double rangefinderM = 0)
    {
        var (body, parts, xf, hf) = LookTurretShape(look, r);
        if (look == "drum")
            return (body, parts, xf, hf);
        if (!(rangefinderM > 0))
            return (body, [], xf, hf);
        // stretch each part across: its outer edge at the bar's end, its own width kept
        double end = rangefinderM / 2 + 0.25;
        var scaled = parts.Select(p =>
        {
            double y0 = p.Min(q => q.Y), y1 = p.Max(q => q.Y);
            bool across = y0 < 0 && y1 > 0;
            return p.Select(q => across ? q with { Y = q.Y / Math.Max(-y0, y1) * end }
                : q with { Y = Math.Sign(q.Y) * (end - (Math.Max(Math.Abs(y0), Math.Abs(y1)) - Math.Abs(q.Y))) }).ToList();
        }).ToList();
        return (body, scaled, xf, hf);
    }

    static (List<Pt> Body, List<List<Pt>> Parts, double Xf, double Hf) LookTurretShape(string look, double r)
    {
        // top: points from the front centreline round the port side (y < 0) to the rear
        List<Pt> Mirror(IEnumerable<(double X, double Y)> top)
        {
            var t = top.ToList();
            return [.. t.Select(p => new Pt(p.X * r, p.Y * r)), .. Enumerable.Reverse(t).Select(p => new Pt(p.X * r, -p.Y * r))];
        }

        List<List<Pt>> Ears(double x0, double x1, double y0, double y1, double rad = 0.0) =>
        [
            Geometry.RrectPolygon(x0 * r, y0 * r, x1 * r, y1 * r, rad * r, rad * r, seg: 4),
            Geometry.RrectPolygon(x0 * r, -y1 * r, x1 * r, -y0 * r, rad * r, rad * r, seg: 4),
        ];

        IEnumerable<(double, double)> Rear(double cx, double rx, double ry, int step) =>
            Enumerable.Range(0, 90 / step + 1).Select(i => i * step)
                .Select(a => (cx - rx * Math.Sin(double.DegreesToRadians(a)), -ry * Math.Cos(double.DegreesToRadians(a))));

        List<Pt> Drum(double cx, double rr) =>
            [.. Enumerable.Range(0, 36).Select(i => new Pt((cx + rr * Math.Cos(double.DegreesToRadians(i * 10))) * r, rr * Math.Sin(double.DegreesToRadians(i * 10)) * r))];

        switch (look)
        {
            case "slab":       // boxy, slab-sided, rangefinder hoods on the rear corners
                return (Geometry.RrectPolygon(-0.95 * r, -0.82 * r, 0.86 * r, 0.82 * r, 0.32 * r, 0.5 * r),
                    Ears(-0.72, -0.5, -1.0, -0.7), 0.86 * r, 0.6 * r);
            case "round":      // flat face, straight cheeks, a rounded rear; a long rangefinder right across
                return (Mirror(new[] { (0.84, -0.5), (0.45, -0.84) }.Concat(Rear(-0.05, 0.92, 0.9, 10))),
                    [Geometry.RrectPolygon(-0.6 * r, -1.08 * r, -0.42 * r, 1.08 * r, 0.09 * r, 0.09 * r, seg: 4)], 0.84 * r, 0.5 * r);
            case "classic":    // straight sides into a semicircular rear
                return (Mirror(new[] { (0.86, -0.62), (0.76, -0.8) }.Concat(Rear(-0.18, 0.8, 0.8, 10))),
                    Ears(-0.64, -0.46, -1.0, 1.0)[..1], 0.86 * r, 0.62 * r);
            case "faceted":    // angled cheeks and rear corners, flat sides
                return (Mirror([(0.86, -0.52), (0.58, -0.86), (-0.78, -0.86), (-0.95, -0.62), (-0.95, 0.0)]),
                    Ears(-0.6, -0.44, -0.98, -0.8), 0.86 * r, 0.52 * r);
            case "drum":       // a Victorian round turret: a drum with sighting hoods standing out at the rear sides
                return (Drum(-0.05, 0.9), Ears(-0.55, -0.35, -0.98, -0.72, 0.08), 0.85 * r, 0.0);
            case "hooded":     // an open Victorian barbette ring, the guns under a pear-shaped hood on the turntable
                return (Drum(-0.05, 0.93), [], 0.88 * r, 0.0);
            case "lancia":     // long and narrow: a wedge prow of a face, flat sides, a rounded bustle; rangefinder across
                return (Mirror(new[] { (0.96, 0.0), (0.84, -0.42), (0.5, -0.82) }.Concat(Rear(-0.6, 0.36, 0.82, 15))),
                    [Geometry.RrectPolygon(-0.64 * r, -1.04 * r, -0.46 * r, 1.04 * r, 0.07 * r, 0.07 * r, seg: 4)], 0.9 * r, 0.0);
            case "champignon": // a French drum under an overhanging mushroom roof, a conical sighting hood on top
                return (Drum(-0.05, 0.95), [], 0.9 * r, 0.0);
            case "quadruple":  // a wide flat face, chamfered front corners, a tapering rear; two halves for four guns
                return (Mirror([(0.86, -0.68), (0.7, -0.86), (-0.5, -0.86), (-0.95, -0.45), (-0.95, 0.0)]),
                    Ears(-0.62, -0.46, -1.02, -0.84), 0.86 * r, 0.68 * r);
        }
        throw new ArgumentException($"unknown turret look '{look}'");
    }

    /// <summary>A turret sprite. Outlines come from Geometry.TurretShapesOf, the same polygons used for hitboxes; a
    /// look other than "standard" redraws armoured turrets in its own style (LookTurretBody). shapes: the look's
    /// drawing variations (turret_bands: recognition bands painted across armoured turret roofs). The parts stand in
    /// the relief layer over the turret's base (the lighting's normals and AO): the gun house a plateau at its roof
    /// (Geometry.TurretHeight), the barrels and tubes cylinders on their axis (BarrelBand's 0.55 of it), the face plates
    /// slopes, cupolas and hatches domes and hoods plateaus a little over the roof; paint on the roof (bands) carries
    /// none. The light is the lighting's: no highlight is painted in (L3).</summary>
    public static Scene Build(TurretType t, Palette palette, double scale, int align = 2, string look = "standard",
        Shapes? shapes = null)
    {
        var P = new Painter(palette, scale, shapes) { Outline = Looks.PlanOutline(shapes) };
        var p = palette;
        double r = t.R;
        long n = t.Barrels;
        double bl = t.BarrelLen, bw = t.BarrelW, sp = t.Spacing;
        double half = Extent(t, scale, align);
        var scene = new Scene(-half, -half, 2 * half, 2 * half, scale);
        var s = scene.Root.Items;
        string bodyCol = P.C("turret");
        var G = Geometry.TurretShapesOf(t);
        string Shade(string c, double k) => Painter.Shade(c, k);
        double Gy(int i) => (i - (n - 1) / 2.0) * sp;
        double th = Geometry.TurretHeight(t), bTop = 0.55 * th + bw / 2;
        const double Fitting = 0.3;   // a hood over the roof, m (nominal: no hitbox gives it)
        double axis = 0.55 * th, drop = 0.3 * th;   // the barrels' axis; how far a face plate slopes down to its front edge
        // a round cupola or hatch: a dome on the roof, as high as most of its radius (at most 0.6 m)
        T Cup<T>(T node, double cx, double cy, double rx, double ry) where T : Node
        {
            double rise = Math.Min(0.6, 0.8 * Math.Min(rx, ry));
            return P.Ln(node, 0.5).Profile(Relief.Dome(cx, cy, rx, ry, th + rise, rise));
        }
        // a face plate from its back edge at the roof (x0) down to its front (x1)
        Relief Face(double x0, double x1) => Relief.Slope(x0, 0, th, -drop / (x1 - x0), 0);

        void Barrels(double xStart)
        {
            for (int i = 0; i < G.Barrels.Count; i++)
            {
                double y = Gy(i);
                double xe = xStart + Geometry.BarrelShown(t);
                s.Add(P.Ln(new PathNode(Painter.Poly(G.Barrels[i])).Fill(P.C("barrel")), 0.8).Profile(Relief.Cylinder(xStart, y, xe, y, bw / 2, bTop)));
                s.Add(P.Ln(new RectNode(xe - bw * 0.9, y - bw * 0.58, bw * 0.9, bw * 1.16).Fill(Shade(P.C("barrel"), 0.7)), 0.6)
                    .Profile(Relief.Cylinder(xStart, y, xe, y, bw * 0.58, axis + bw * 0.58)));   // the muzzle's swell
            }
        }

        string shape = t.Shape;
        if (shape == "bb" && look != "standard")
        {
            var (pts, parts, xf, hf) = LookTurretBody(look, r, t.RangefinderM ?? 0);
            var body = Painter.Poly(pts);
            Barrels(BarrelRoot["bb"] * r);
            for (int i = 0; i < n; i++)   // gun ports
                s.Add(P.Ln(new RectNode(xf - 0.1 * r, Gy(i) - bw * 0.85, 0.2 * r, bw * 1.7, bw * 0.3).Fill(Shade(bodyCol, 0.55)), 0.6)
                    .Profile(Relief.Cylinder(0, Gy(i), 1, Gy(i), bw * 0.85, axis + bw * 0.85)));   // the blast bag
            s.Add(P.Ln(new PathNode(body).Fill(bodyCol), 1.2)
                .Profile(look == "drum" ? Relief.Dome(-0.05 * r, 0, 0.9 * r, 0.9 * r, th, 0.25 * th) : Relief.Plateau(th)));
            if (hf != 0)   // sloped face plate (a drum turret has none)
                s.Add(new PathNode(new PathData().M(xf, -hf).L(xf, hf).L(xf - 0.22 * r, hf - 0.08 * r).L(xf - 0.22 * r, -hf + 0.08 * r).Z())
                    .Fill(bodyCol).Profile(Face(xf - 0.22 * r, xf)));
            foreach (var part in parts)
                s.Add(P.Ln(new PathNode(Painter.Poly(part)).Fill(Shade(bodyCol, 0.85)), 0.8).Plateau(th + Fitting));
            string hood = Shade(bodyCol, 0.8);
            T H<T>(T node) where T : Node => P.Ln(node, 0.5).Plateau(th + Fitting);
            switch (look)
            {
                case "slab":      // three periscope hoods forward, a vent box aft
                    foreach (double yy in new[] { -0.5 * r, 0.0, 0.5 * r })
                        s.Add(H(new RectNode(0.3 * r, yy - 0.08 * r, 0.18 * r, 0.16 * r, 0.03 * r).Fill(hood)));
                    s.Add(H(new RectNode(-0.6 * r, -0.25 * r, 0.28 * r, 0.5 * r, 0.05 * r).Fill(hood)));
                    break;
                case "round":     // two round cupolas forward, a hatch amidships
                    foreach (double yy in new[] { -0.32 * r, 0.32 * r })
                        s.Add(Cup(new CircleNode(0.3 * r, yy, 0.1 * r).Fill(hood), 0.3 * r, yy, 0.1 * r, 0.1 * r));
                    s.Add(Cup(new CircleNode(-0.05 * r, 0, 0.12 * r).Fill(hood), -0.05 * r, 0, 0.12 * r, 0.12 * r));
                    break;
                case "classic":   // sighting hoods on the roof sides, a hatch aft
                    foreach (double yy in new[] { -0.5 * r, 0.5 * r })
                        s.Add(H(new RectNode(0.15 * r, yy - 0.1 * r, 0.3 * r, 0.2 * r, 0.08 * r).Fill(hood)));
                    s.Add(H(new RectNode(-0.3 * r, -0.12 * r, 0.22 * r, 0.24 * r, 0.04 * r).Fill(hood)));
                    break;
                case "drum":      // two small sighting hoods at the front edge, a round roof hatch
                    foreach (double yy in new[] { -0.38 * r, 0.38 * r })
                        s.Add(H(new RectNode(0.42 * r, yy - 0.08 * r, 0.2 * r, 0.16 * r, 0.05 * r).Fill(Shade(bodyCol, 1.5))));
                    s.Add(Cup(new CircleNode(-0.2 * r, 0, 0.14 * r).Fill(Shade(bodyCol, 1.4)), -0.2 * r, 0, 0.14 * r, 0.14 * r));
                    break;
                case "faceted":   // domed cupolas forward, a rangefinder hood across the rear
                    foreach (double yy in new[] { -0.42 * r, 0.42 * r })
                        s.Add(Cup(new CircleNode(0.25 * r, yy, 0.12 * r).Fill(hood), 0.25 * r, yy, 0.12 * r, 0.12 * r));
                    s.Add(H(new RectNode(-0.62 * r, -0.68 * r, 0.2 * r, 1.36 * r, 0.06 * r).Fill(hood)));
                    break;
                case "hooded":    // the ring's dark well, then the light hood over the guns' breeches
                    {
                        double cx = -0.05 * r;
                        s.Add(H(new CircleNode(cx, 0, 0.74 * r).Fill(Shade(bodyCol, 0.5))).Plateau(0.35 * th));   // the ring's well
                        double hw = Math.Min(0.6 * r, (n - 1) / 2.0 * sp + 0.35 * r);
                        List<Pt> pear = [new(0.62 * r, -hw * 0.8), new(0.72 * r, 0.0), new(0.62 * r, hw * 0.8), new(0.2 * r, hw),
                            new(-0.45 * r, hw * 0.55), new(-0.6 * r, 0.0), new(-0.45 * r, -hw * 0.55), new(0.2 * r, -hw)];
                        string hoodCol = p.Or("turret_hood", Shade(bodyCol, 1.35));
                        s.Add(P.Ln(new PathNode(Painter.Poly(pear)).Fill(hoodCol), 0.8)
                            .Profile(Relief.Dome(0.06 * r, 0, 0.66 * r, hw, th, 0.3 * th)));   // the hood's curve over the breeches
                        s.Add(Cup(new CircleNode(-0.2 * r, 0, 0.1 * r).Fill(Shade(hoodCol, 0.85)), -0.2 * r, 0, 0.1 * r, 0.1 * r));
                        break;
                    }
                case "lancia":    // the wedge's crease, two low cupolas forward, a hatch on the bustle
                    s.Add(new PathNode(new PathData().M(0.96 * r, 0).L(-0.4 * r, 0)).Stroke(Shade(bodyCol, 0.7), 0.05 * r));
                    foreach (double yy in new[] { -0.45 * r, 0.45 * r })
                        s.Add(Cup(new EllipseNode(0.3 * r, yy, 0.16 * r, 0.09 * r).Fill(hood), 0.3 * r, yy, 0.16 * r, 0.09 * r));
                    s.Add(Cup(new CircleNode(-0.8 * r, 0, 0.1 * r).Fill(hood), -0.8 * r, 0, 0.1 * r, 0.1 * r));
                    break;
                case "champignon":   // the mushroom roof's lip, the cone rising to a sighting hood at the centre
                    foreach (double rr in new[] { 0.78, 0.52, 0.3 })   // the cone's rings, one dome of a roof under them
                        s.Add(H(new CircleNode(-0.05 * r, 0, rr * r).Fill(Shade(bodyCol, 0.85)))
                            .Profile(Relief.Dome(-0.05 * r, 0, 0.78 * r, 0.78 * r, th + 0.3 * r, 0.3 * r)));
                    s.Add(H(new RectNode(-0.05 * r, -0.11 * r, 0.32 * r, 0.22 * r, 0.06 * r).Fill(Shade(bodyCol, 0.75))).Plateau(th + 0.3 * r + Fitting));
                    for (int a = 0; a < 360; a += 30)   // rivets round the lip
                        s.Add(new CircleNode((-0.05 + 0.87 * Math.Cos(double.DegreesToRadians(a))) * r, 0.87 * Math.Sin(double.DegreesToRadians(a)) * r, 0.025 * r)
                            .Fill(Shade(bodyCol, 0.6)));
                    break;
                case "quadruple":    // the wall between the two halves, a periscope hood over each gun pair
                    if (n >= 4)
                        s.Add(P.Ln(new RectNode(-0.95 * r, -0.05 * r, 1.79 * r, 0.1 * r).Fill(Shade(bodyCol, 0.65)), 0.4).Plateau(th + 0.15));
                    foreach (double yy in n >= 2 ? new[] { -0.48 * r, 0.48 * r } : [0.0])
                        s.Add(H(new RectNode(0.18 * r, yy - 0.1 * r, 0.32 * r, 0.2 * r, 0.05 * r).Fill(hood)));
                    s.Add(H(new RectNode(-0.85 * r, -0.2 * r, 0.18 * r, 0.4 * r, 0.04 * r).Fill(hood)));
                    break;
            }
            var bands = P.Shapes.TurretBands ?? [];
            for (int i = 0; i < bands.Count; i++)   // recognition bands across the roof
            {
                string key = bands[i];
                double bx = (-0.1 - 0.26 * i) * r;
                var g = new Group { Clip = [new PathNode(body)] };
                g.Items.Add(new RectNode(bx - 0.1 * r, -1.2 * r, 0.2 * r, 2.4 * r).Fill(p.Or(key, key)).FillOp(0.92));
                s.Add(g);
            }
        }
        else if (shape == "bb")
        {
            var body = Painter.Poly(G.Body);
            Barrels(BarrelRoot["bb"] * r);
            for (int i = 0; i < n; i++)   // blast bags / gun ports
                s.Add(P.Ln(new RectNode(0.75 * r, Gy(i) - bw * 0.85, 0.2 * r, bw * 1.7, bw * 0.3).Fill(Shade(bodyCol, 0.55)), 0.6)
                    .Profile(Relief.Cylinder(0, Gy(i), 1, Gy(i), bw * 0.85, axis + bw * 0.85)));   // the blast bag
            s.Add(P.Ln(new PathNode(body).Fill(bodyCol), 1.2).Plateau(th));
            // sloped front face plate
            s.Add(new PathNode(new PathData().M(0.85 * r, -0.5 * r).L(0.85 * r, 0.5 * r).L(0.62 * r, 0.42 * r).L(0.62 * r, -0.42 * r).Z())
                .Fill(bodyCol).Profile(Face(0.62 * r, 0.85 * r)));
            foreach (var part in G.Parts)   // rangefinder ears
                s.Add(P.Ln(new PathNode(Painter.Poly(part)).Fill(Shade(bodyCol, 0.85)), 0.8).Plateau(th + Fitting));
            foreach (double yy in new[] { -0.45 * r, 0.45 * r })   // roof hatches / periscope hoods
                s.Add(P.Ln(new RectNode(0.2 * r, yy - 0.09 * r, 0.22 * r, 0.18 * r, 0.05 * r).Fill(Shade(bodyCol, 0.8)), 0.5).Plateau(th + Fitting));
            s.Add(Cup(new CircleNode(-0.15 * r, 0, 0.1 * r).Fill(Shade(bodyCol, 0.8)), -0.15 * r, 0, 0.1 * r, 0.1 * r));
        }
        else if (shape == "dp")
        {
            var body = Painter.Poly(G.Body);
            Barrels(BarrelRoot["dp"] * r);
            s.Add(P.Ln(new PathNode(body).Fill(bodyCol), 1.1).Plateau(th));
            s.Add(P.Ln(new RectNode(-0.55 * r, -0.18 * r, 0.3 * r, 0.36 * r, 0.06 * r).Fill(Shade(bodyCol, 0.8)), 0.5).Plateau(th + Fitting));
        }
        else if (shape == "open")
        {
            s.Add(P.Ln(new CircleNode(0, 0, r).Fill(P.C("tub"))).Plateau(0.15));   // the platform
            Barrels(BarrelRoot["open"] * r);
            s.Add(new PathNode(new PathData().M(0.25 * r, -0.85 * r).A(0.9 * r, 0.9 * r, 0, false, true, 0.25 * r, 0.85 * r))
                .Fill("none").Stroke(bodyCol, 0.28 * r).Cap("round").Plateau(th));   // the shield
            s.Add(P.Ln(new RectNode(-0.5 * r, -0.25 * r, 0.6 * r, 0.5 * r).Fill(bodyCol), 0.6).Plateau(0.5 * th));
        }
        else if (shape == "casemate")   // the port shield on the hull side and the barrels run out through it
        {
            double rc = Geometry.CasemateShieldR(r, n, sp, bw);
            Barrels(BarrelRoot["casemate"] * r);
            s.Add(P.Ln(new CircleNode(0, 0, rc).Fill(P.C("hull")), 1.1).Profile(Relief.Dome(0, 0, rc, rc, th, 0.4 * th)));   // the curved shield
            s.Add(new RectNode(0.2 * rc, -(n - 1) / 2.0 * sp - bw * 0.8, 0.8 * rc, (n - 1) * sp + bw * 1.6).Fill(Shade(P.C("hull"), 0.45)));
            for (int i = 0; i < n; i++)   // the barrels' roots in the gun port
                s.Add(new RectNode(0.2 * rc, Gy(i) - bw * 0.62, 0.8 * rc, bw * 1.24).Fill(P.C("barrel")).Profile(Relief.Cylinder(0, Gy(i), 1, Gy(i), bw / 2, bTop)));
        }
        else if (shape == "torp")
        {
            s.Add(P.Ln(new CircleNode(0, 0, r).Fill(Shade(bodyCol, 0.85))).Plateau(0.2 * th));
            for (int i = 0; i < G.Barrels.Count; i++)
            {
                s.Add(P.Ln(new PathNode(Painter.Poly(G.Barrels[i])).Fill(P.C("tube")), 0.8).Profile(Relief.Cylinder(0, Gy(i), 1, Gy(i), bw / 2, bTop)));
                s.Add(new CircleNode(bl / 2 - bw / 2, Gy(i), bw * 0.3).Fill("#111"));
            }
            var (bar, cab) = (G.Parts[0], G.Parts[1]);
            s.Add(P.Ln(new PathNode(Painter.Poly(bar)).Fill(Shade(bodyCol, 0.9)), 0.7).Plateau(0.6 * th));
            s.Add(P.Ln(new PathNode(Painter.Poly(cab)).Fill(bodyCol), 0.7).Plateau(th));
        }
        else if (shape == "tube")
        {
            // thin outlines: at 0.55 m a normal outline would visibly fatten the tube
            s.Add(P.Ln(new PathNode(Painter.Poly(G.Body)).Fill(Shade(bodyCol, 0.8)), 0.4).Plateau(bw / 2));
            for (int i = 0; i < G.Barrels.Count; i++)
            {
                double y = Gy(i);
                s.Add(P.Ln(new PathNode(Painter.Poly(G.Barrels[i])).Fill(P.C("tube")), 0.4).Profile(Relief.Cylinder(0, y, 1, y, bw / 2, bw)));
                s.Add(new CircleNode(bl / 2 - bw / 2, y, bw * 0.3).Fill("#111"));
            }
        }
        else
            throw new ArgumentException($"unknown turret shape {shape}");
        return scene;
    }
}
