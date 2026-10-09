namespace Fleetwright.Shipgen.Render;

/// <summary>Turret sprites (shipgen.build_turret): one per turret type, pivot at the image centre, barrels along +x.</summary>
public static class TurretArt
{
    static readonly Dictionary<string, double> BARREL_ROOT = new(StringComparer.Ordinal)
    {
        ["bb"] = 0.5, ["dp"] = 0.3, ["open"] = -0.3, ["casemate"] = 0.0,
    };

    /// <summary>Half the canvas in metres. With a scale, snapped so the canvas is a multiple of align px (even at
    /// least) and the pivot lands exactly at the image centre.</summary>
    public static double Extent(PyDict t, double? scale = null, int align = 2)
    {
        double half = Py.Max(t.F("r") * 1.15, Geometry.TurretReach(t)) + 1.0;
        if (scale is double s && s != 0)
        {
            double a = align / 2.0;
            half = Math.Ceiling(half * s / a) * a / s;
        }
        return half;
    }

    /// <summary>A look's drawing of an armoured (bb) turret, in units of r: (body outline, rangefinder parts, front
    /// face x, front face half-width). Drawing only: the hitbox keeps Geometry.TurretShapesOf, and each outline stays
    /// close to it.</summary>
    public static (List<Pt> Body, List<List<Pt>> Parts, double Xf, double Hf) LookTurretBody(string look, double r)
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
                .Select(a => (cx - rx * Math.Sin(Py.Radians(a)), -ry * Math.Cos(Py.Radians(a))));

        List<Pt> Drum(double cx, double rr) =>
            [.. Enumerable.Range(0, 36).Select(i => new Pt((cx + rr * Math.Cos(Py.Radians(i * 10))) * r, rr * Math.Sin(Py.Radians(i * 10)) * r))];

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
        throw new PyValueError($"unknown turret look {Py.Repr(look)}");
    }

    /// <summary>A turret sprite. Outlines come from Geometry.TurretShapesOf, the same polygons used for hitboxes; a
    /// look other than "standard" redraws armoured turrets in its own style (LookTurretBody). shapes: the look's
    /// drawing variations (turret_bands: recognition bands painted across armoured turret roofs).</summary>
    public static Scene Build(PyDict t, PyDict palette, double scale, int align = 2, string look = "standard",
        PyDict? shapes = null)
    {
        var P = new Painter(palette, scale, shapes);
        var p = palette;
        double r = t.F("r");
        long n = t.I("barrels");
        double bl = t.F("barrel_len"), bw = t.F("barrel_w"), sp = t.F("spacing");
        double half = Extent(t, scale, align);
        var scene = new Scene(-half, -half, 2 * half, 2 * half, scale);
        var s = scene.Root.Items;
        string bodyCol = t.S("color", P.C("turret"))!;
        var G = Geometry.TurretShapesOf(t);
        string Shade(string c, double k) => Painter.Shade(c, k);
        double Gy(int i) => (i - (n - 1) / 2.0) * sp;

        void Barrels(double xStart)
        {
            for (int i = 0; i < G.Barrels.Count; i++)
            {
                double y = Gy(i);
                double xe = xStart + Geometry.BarrelShown(t);
                s.Add(P.Ln(new PathNode(Painter.Poly(G.Barrels[i])).Fill(P.C("barrel")), 0.8));
                s.Add(new LineNode(xStart, y - bw * 0.18, xe - bw, y - bw * 0.18).Stroke(Shade(P.C("barrel"), 1.6), bw * 0.18).StrokeOp(0.55));
                s.Add(P.Ln(new RectNode(xe - bw * 0.9, y - bw * 0.58, bw * 0.9, bw * 1.16).Fill(Shade(P.C("barrel"), 0.7)), 0.6));
            }
        }

        string shape = t.S("shape", "bb")!;
        if (shape == "bb" && look != "standard")
        {
            var (pts, parts, xf, hf) = LookTurretBody(look, r);
            var body = Painter.Poly(pts);
            Barrels(BARREL_ROOT["bb"] * r);
            for (int i = 0; i < n; i++)   // gun ports
                s.Add(P.Ln(new RectNode(xf - 0.1 * r, Gy(i) - bw * 0.85, 0.2 * r, bw * 1.7, bw * 0.3).Fill(Shade(bodyCol, 0.55)), 0.6));
            s.Add(P.Ln(new PathNode(body).Fill(bodyCol), 1.2));
            s.Add(new PathNode(body).Fill(Shade(bodyCol, 1.12)).Tr(new Translate(0.02 * r, 0), new Scale(0.8)));
            if (hf != 0)   // sloped face plate (a drum turret has none)
                s.Add(new PathNode(new PathData().M(xf, -hf).L(xf, hf).L(xf - 0.22 * r, hf - 0.08 * r).L(xf - 0.22 * r, -hf + 0.08 * r).Z())
                    .Fill(Shade(bodyCol, 0.82)));
            foreach (var part in parts)
                s.Add(P.Ln(new PathNode(Painter.Poly(part)).Fill(Shade(bodyCol, 0.85)), 0.8));
            string hood = Shade(bodyCol, 0.8);
            T H<T>(T node) where T : Node => P.Ln(node, 0.5);
            switch (look)
            {
                case "slab":      // three periscope hoods forward, a vent box aft
                    foreach (double yy in new[] { -0.5 * r, 0.0, 0.5 * r })
                        s.Add(H(new RectNode(0.3 * r, yy - 0.08 * r, 0.18 * r, 0.16 * r, 0.03 * r).Fill(hood)));
                    s.Add(H(new RectNode(-0.6 * r, -0.25 * r, 0.28 * r, 0.5 * r, 0.05 * r).Fill(hood)));
                    break;
                case "round":     // two round cupolas forward, a hatch amidships
                    foreach (double yy in new[] { -0.32 * r, 0.32 * r })
                        s.Add(H(new CircleNode(0.3 * r, yy, 0.1 * r).Fill(hood)));
                    s.Add(H(new CircleNode(-0.05 * r, 0, 0.12 * r).Fill(hood)));
                    break;
                case "classic":   // sighting hoods on the roof sides, a hatch aft
                    foreach (double yy in new[] { -0.5 * r, 0.5 * r })
                        s.Add(H(new RectNode(0.15 * r, yy - 0.1 * r, 0.3 * r, 0.2 * r, 0.08 * r).Fill(hood)));
                    s.Add(H(new RectNode(-0.3 * r, -0.12 * r, 0.22 * r, 0.24 * r, 0.04 * r).Fill(hood)));
                    break;
                case "drum":      // two small sighting hoods at the front edge, a round roof hatch
                    foreach (double yy in new[] { -0.38 * r, 0.38 * r })
                        s.Add(H(new RectNode(0.42 * r, yy - 0.08 * r, 0.2 * r, 0.16 * r, 0.05 * r).Fill(Shade(bodyCol, 1.5))));
                    s.Add(H(new CircleNode(-0.2 * r, 0, 0.14 * r).Fill(Shade(bodyCol, 1.4))));
                    break;
                case "faceted":   // domed cupolas forward, a rangefinder hood across the rear
                    foreach (double yy in new[] { -0.42 * r, 0.42 * r })
                    {
                        s.Add(H(new CircleNode(0.25 * r, yy, 0.12 * r).Fill(hood)));
                        s.Add(new CircleNode(0.22 * r, yy - 0.03 * r, 0.05 * r).Fill(Shade(bodyCol, 1.2)));
                    }
                    s.Add(H(new RectNode(-0.62 * r, -0.68 * r, 0.2 * r, 1.36 * r, 0.06 * r).Fill(hood)));
                    break;
                case "hooded":    // the ring's dark well, then the light hood over the guns' breeches
                    {
                        double cx = -0.05 * r;
                        s.Add(H(new CircleNode(cx, 0, 0.74 * r).Fill(Shade(bodyCol, 0.5))));
                        double hw = Py.Min(0.6 * r, (n - 1) / 2.0 * sp + 0.35 * r);
                        List<Pt> pear = [new(0.62 * r, -hw * 0.8), new(0.72 * r, 0.0), new(0.62 * r, hw * 0.8), new(0.2 * r, hw),
                            new(-0.45 * r, hw * 0.55), new(-0.6 * r, 0.0), new(-0.45 * r, -hw * 0.55), new(0.2 * r, -hw)];
                        string hoodCol = p.TryGet("turret_hood", out var hc) ? (string)hc! : Shade(bodyCol, 1.35);
                        s.Add(P.Ln(new PathNode(Painter.Poly(pear)).Fill(hoodCol), 0.8));
                        s.Add(new PathNode(Painter.Poly(pear)).Fill(Shade(hoodCol, 1.15)).Tr(new Translate(0.04 * r, 0), new Scale(0.7)));
                        s.Add(H(new CircleNode(-0.2 * r, 0, 0.1 * r).Fill(Shade(hoodCol, 0.85))));
                        break;
                    }
                case "lancia":    // the wedge's crease, two low cupolas forward, a hatch on the bustle
                    s.Add(new PathNode(new PathData().M(0.96 * r, 0).L(-0.4 * r, 0)).Stroke(Shade(bodyCol, 0.7), 0.05 * r));
                    foreach (double yy in new[] { -0.45 * r, 0.45 * r })
                        s.Add(H(new EllipseNode(0.3 * r, yy, 0.16 * r, 0.09 * r).Fill(hood)));
                    s.Add(H(new CircleNode(-0.8 * r, 0, 0.1 * r).Fill(hood)));
                    break;
                case "champignon":   // the mushroom roof's lip, the cone rising to a sighting hood at the centre
                    foreach (var (k, rr) in new[] { (0.85, 0.78), (1.15, 0.52), (1.3, 0.3) })
                        s.Add(H(new CircleNode(-0.05 * r, 0, rr * r).Fill(Shade(bodyCol, k))));
                    s.Add(H(new RectNode(-0.05 * r, -0.11 * r, 0.32 * r, 0.22 * r, 0.06 * r).Fill(Shade(bodyCol, 0.75))));
                    for (int a = 0; a < 360; a += 30)   // rivets round the lip
                        s.Add(new CircleNode((-0.05 + 0.87 * Math.Cos(Py.Radians(a))) * r, 0.87 * Math.Sin(Py.Radians(a)) * r, 0.025 * r)
                            .Fill(Shade(bodyCol, 0.6)));
                    break;
                case "quadruple":    // the wall between the two halves, a periscope hood over each gun pair
                    if (n >= 4)
                        s.Add(P.Ln(new RectNode(-0.95 * r, -0.05 * r, 1.79 * r, 0.1 * r).Fill(Shade(bodyCol, 0.65)), 0.4));
                    foreach (double yy in n >= 2 ? new[] { -0.48 * r, 0.48 * r } : [0.0])
                        s.Add(H(new RectNode(0.18 * r, yy - 0.1 * r, 0.32 * r, 0.2 * r, 0.05 * r).Fill(hood)));
                    s.Add(H(new RectNode(-0.85 * r, -0.2 * r, 0.18 * r, 0.4 * r, 0.04 * r).Fill(hood)));
                    break;
            }
            var bands = P.Shapes.Get("turret_bands") as List<object?> ?? [];
            for (int i = 0; i < bands.Count; i++)   // recognition bands across the roof
            {
                string key = (string)bands[i]!;
                double bx = (-0.1 - 0.26 * i) * r;
                var g = new Group { Clip = [new PathNode(body)] };
                g.Items.Add(new RectNode(bx - 0.1 * r, -1.2 * r, 0.2 * r, 2.4 * r).Fill(p.TryGet(key, out var bc) ? (string)bc! : key).FillOp(0.92));
                s.Add(g);
            }
        }
        else if (shape == "bb")
        {
            var body = Painter.Poly(G.Body);
            Barrels(BARREL_ROOT["bb"] * r);
            for (int i = 0; i < n; i++)   // blast bags / gun ports
                s.Add(P.Ln(new RectNode(0.75 * r, Gy(i) - bw * 0.85, 0.2 * r, bw * 1.7, bw * 0.3).Fill(Shade(bodyCol, 0.55)), 0.6));
            s.Add(P.Ln(new PathNode(body).Fill(bodyCol), 1.2));
            s.Add(new PathNode(body).Fill(Shade(bodyCol, 1.12)).Tr(new Translate(0.02 * r, 0), new Scale(0.8)));
            // sloped front face plate
            s.Add(new PathNode(new PathData().M(0.85 * r, -0.5 * r).L(0.85 * r, 0.5 * r).L(0.62 * r, 0.42 * r).L(0.62 * r, -0.42 * r).Z())
                .Fill(Shade(bodyCol, 0.82)));
            foreach (var part in G.Parts)   // rangefinder ears
                s.Add(P.Ln(new PathNode(Painter.Poly(part)).Fill(Shade(bodyCol, 0.85)), 0.8));
            foreach (double yy in new[] { -0.45 * r, 0.45 * r })   // roof hatches / periscope hoods
                s.Add(P.Ln(new RectNode(0.2 * r, yy - 0.09 * r, 0.22 * r, 0.18 * r, 0.05 * r).Fill(Shade(bodyCol, 0.8)), 0.5));
            s.Add(P.Ln(new CircleNode(-0.15 * r, 0, 0.1 * r).Fill(Shade(bodyCol, 0.8)), 0.5));
        }
        else if (shape == "dp")
        {
            var body = Painter.Poly(G.Body);
            Barrels(BARREL_ROOT["dp"] * r);
            s.Add(P.Ln(new PathNode(body).Fill(bodyCol), 1.1));
            s.Add(new PathNode(body).Fill(Shade(bodyCol, 1.12)).Tr(new Translate(-0.05 * r, 0), new Scale(0.78)));
            s.Add(P.Ln(new RectNode(-0.55 * r, -0.18 * r, 0.3 * r, 0.36 * r, 0.06 * r).Fill(Shade(bodyCol, 0.8)), 0.5));
        }
        else if (shape == "open")
        {
            s.Add(P.Ln(new CircleNode(0, 0, r).Fill(P.C("tub"))));
            Barrels(BARREL_ROOT["open"] * r);
            s.Add(new PathNode(new PathData().M(0.25 * r, -0.85 * r).A(0.9 * r, 0.9 * r, 0, false, true, 0.25 * r, 0.85 * r))
                .Fill("none").Stroke(bodyCol, 0.28 * r).Cap("round"));
            s.Add(P.Ln(new RectNode(-0.5 * r, -0.25 * r, 0.6 * r, 0.5 * r).Fill(bodyCol), 0.6));
        }
        else if (shape == "casemate")   // the port shield on the hull side and the barrels run out through it
        {
            double rc = Geometry.CASEMATE_SHIELD * r;
            Barrels(BARREL_ROOT["casemate"] * r);
            s.Add(P.Ln(new CircleNode(0, 0, rc).Fill(P.C("hull")), 1.1));
            s.Add(new CircleNode(0, 0, rc * 0.72).Fill(Shade(P.C("hull"), 1.25)));
            s.Add(new RectNode(0.2 * rc, -(n - 1) / 2.0 * sp - bw * 0.8, 0.8 * rc, (n - 1) * sp + bw * 1.6).Fill(Shade(P.C("hull"), 0.45)));
            for (int i = 0; i < n; i++)   // the barrels' roots in the gun port
                s.Add(new RectNode(0.2 * rc, Gy(i) - bw * 0.62, 0.8 * rc, bw * 1.24).Fill(P.C("barrel")));
        }
        else if (shape == "torp")
        {
            s.Add(P.Ln(new CircleNode(0, 0, r).Fill(Shade(bodyCol, 0.85))));
            for (int i = 0; i < G.Barrels.Count; i++)
            {
                s.Add(P.Ln(new PathNode(Painter.Poly(G.Barrels[i])).Fill(P.C("tube")), 0.8));
                s.Add(new CircleNode(bl / 2 - bw / 2, Gy(i), bw * 0.3).Fill("#111"));
            }
            var (bar, cab) = (G.Parts[0], G.Parts[1]);
            s.Add(P.Ln(new PathNode(Painter.Poly(bar)).Fill(Shade(bodyCol, 0.9)), 0.7));
            s.Add(P.Ln(new PathNode(Painter.Poly(cab)).Fill(bodyCol), 0.7));
        }
        else if (shape == "tube")
        {
            // thin outlines: at 0.55 m a normal outline would visibly fatten the tube
            s.Add(P.Ln(new PathNode(Painter.Poly(G.Body)).Fill(Shade(bodyCol, 0.8)), 0.4));
            for (int i = 0; i < G.Barrels.Count; i++)
            {
                double y = Gy(i);
                s.Add(P.Ln(new PathNode(Painter.Poly(G.Barrels[i])).Fill(P.C("tube")), 0.4));
                s.Add(new LineNode(-bl / 2 + 0.6, y - bw * 0.2, bl / 2 - 0.6, y - bw * 0.2).Stroke(Shade(P.C("tube"), 1.5), bw * 0.15)
                    .StrokeOp(0.6));
                s.Add(new CircleNode(bl / 2 - bw / 2, y, bw * 0.3).Fill("#111"));
            }
        }
        else
            throw new PyValueError($"unknown turret shape {shape}");
        return scene;
    }
}
