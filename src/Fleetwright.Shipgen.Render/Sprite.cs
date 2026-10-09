namespace Fleetwright.Shipgen.Render;

/// <summary>The height map: the static part of the ship (hull, superstructure, funnels, masts, AA, boats, barbettes and
/// the clutter) as columns, grey = metres above the waterline / HEIGHT_STEP_M (0 = sea). Every pixel is a solid
/// column from the sea up to its height; the game casts shadows from it (see ../shipgen/shadow.py for the reference
/// shadow march until it moves here).</summary>
public static class HeightMap
{
    public const double HEIGHT_STEP_M = 0.25;      // one grey level = 0.25 m; 255 = 63.75 m above the waterline

    public static Rgb Grey(double hM) => Rgb.Grey((int)Math.Max(0L, Math.Min(255L, (long)Math.Round(hM / HEIGHT_STEP_M))));

    /// <summary>The height map from the columns (lowest first, so the taller one wins where they overlap), on the
    /// hull's canvas; hull: the hull as drawn (a look may fill it out). Also returns the tallest column.</summary>
    public static (Scene Scene, double MaxHeight) Build(IReadOnlyList<PyDict> columns, Scene hullScene, Hull hull)
    {
        var sc = new Scene(hullScene.X, hullScene.Y, hullScene.W, hullScene.H, hullScene.Scale);
        var o = sc.Root.Items;
        o.Add(new RectNode(sc.X, sc.Y, sc.W, sc.H).Fill("#000"));
        foreach (var c in columns)
        {
            Node n = c.S("shape") switch
            {
                "hull" => new PathNode(Painter.HullPath(hull)),
                "polygon" => new PathNode(Painter.Poly(Geometry.Pts(c["points"]))),
                "rect" => new RectNode(c.F("x"), c.F("y"), c.F("w"), c.F("h")),
                "circle" => new CircleNode(c.F("cx"), c.F("cy"), c.F("r")),
                "ellipse" => new EllipseNode(c.F("cx"), c.F("cy"), c.F("rx"), c.F("ry")),
                var sh => throw new PyValueError($"unknown column shape {Py.Repr(sh)}"),
            };
            o.Add(n.Fill(Grey(c.F("top"))));
        }
        return (sc, columns.Select(c => c.F("top")).Max());
    }
}

/// <summary>A designed ship's sprites as display lists, and sprite.json (render.render_ship's layout half): the hull
/// and each turret type drawn in the ship's look, the height map, canvas sizes, mount pixels, mips and shadow
/// data. The rasteriser (Step 5) turns the scenes into the PNGs that sprite.json names.</summary>
public sealed class ShipSprites
{
    public required PyDict Meta;                    // sprite.json
    public required Scene Hull;
    public required Dictionary<string, Scene> Turrets;
    public required Scene Height;
    public required List<Clutter.Item> Clutter;

    public const string ORIENTATION = "bow points +x (right); angles clockwise, 0 = ahead";

    /// <summary>[x, y, w, h] of each mip level in the packed image: level 0 on the left, level 1 to its right at the
    /// top, then each next level alternately below and to the right of the previous one. The whole chain fits in
    /// 1.5w x h. Canvases are multiples of 2^(levels+1) px, so every rect is whole pixels with an even size.</summary>
    public static List<object?> MipRects(long w, long h, int levels)
    {
        var rects = new List<object?> { Py.List(0L, 0L, w, h) };
        long x = w, y = 0;
        for (int k = 1; k <= levels; k++)
        {
            long lw = w >> k, lh = h >> k;
            rects.Add(Py.List(x, y, lw, lh));
            if (k % 2 == 1)
                y += lh;
            else
                x += lw;
        }
        return rects;
    }

    static string MipsFile(string rel) => rel[..^".png".Length] + "_mips.png";

    /// <summary>Draw a designed ship (ShipDesign.Build's dict) at S px/m with the given mip levels. look ({"navy",
    /// "era"}, either may be left out) overrides the design's own look key by key, so the game can repaint a design
    /// into a later era without touching it.</summary>
    public static ShipSprites Build(Ship ship, double S, int mips = 0, LookInput? look = null)
    {
        var design = ship.Design;
        var rd = (PyDict)JsonBridge.ToPy(ship.Render, ShipgenJson.Default.RenderData)!;
        if (look != null)
        {
            var own = Looks.LookOf(design);
            design = design with { Look = own with { Navy = look.Navy ?? own.Navy, Era = look.Era ?? own.Era } };
        }
        int align = 1 << (mips + 1);
        var spec = (PyDict)PyJson.Plain(rd["spec"])!;   // a deep copy
        spec["palette"] = Looks.Palette(design);
        var pal = PyDict.Merge(Looks.DEFAULT_PALETTE, spec.D("palette"));
        string turretLook = Looks.Get(design).S("turrets");
        spec["shapes"] = Looks.Shapes(design);

        var turrets = new Dictionary<string, Scene>(StringComparer.Ordinal);
        var tmeta = new PyDict();
        foreach (var (tid, tv) in spec.D("turret_types"))
        {
            var t = (PyDict)tv!;
            var sc = TurretArt.Build(t, pal, S, align, turretLook, spec.D("shapes"));
            turrets[tid] = sc;
            long w = sc.WidthPx, h = sc.HeightPx;
            tmeta[tid] = PyDict.Merge(PyDict.Of(("file", $"turrets/{tid}.png"), ("size_px", Py.List(w, h)),
                ("pivot_px", Py.List(w / 2.0, h / 2.0)), ("desc", t["desc"])), Geometry.GunOf(t));
        }

        var hr = HullArt.Build(spec, S, align);
        double deckM = rd.F("deck_m");
        var layoutColumns = rd.L("columns").Cast<PyDict>().ToList();
        var columns = layoutColumns.Concat(Render.Clutter.HeightColumns(hr.Clutter, layoutColumns, hr.Hull)).ToList();
        var (height, maxH) = HeightMap.Build(columns, hr.Scene, hr.Hull);
        long W = hr.Scene.WidthPx, H = hr.Scene.HeightPx;
        double ox = W / 2.0, oy = H / 2.0;
        var byId = rd.L("mounts").Cast<PyDict>().ToDictionary(m => m.S("id"), StringComparer.Ordinal);
        var meta = PyDict.Of(("id", design.Id), ("name", design.Name ?? design.Id), ("scale_px_per_m", S),
            ("size_px", Py.List(W, H)), ("origin_px", Py.List(ox, oy)),
            ("orientation", ORIENTATION),
            ("layer_order", Py.List("hull", "turrets (ascending z)")),
            ("layers", PyDict.Of(("hull", "hull.png"))), ("turret_types", tmeta),
            ("mips", PyDict.Of(("levels", (long)mips), ("scale_px_per_m", Enumerable.Range(0, mips + 1).Select(k => (object?)(S / (1L << k))).ToList()),
                ("rule", "each layer has <name>_mips.png with level k at mip_rects[k] = [x, y, w, h]; " +
                         "within level k, size, origin_px, pivot_px and mount px are the level-0 values / 2^k"))),
            ("shadow", PyDict.Of(("height_map", "height.png"), ("height_step_m", HeightMap.HEIGHT_STEP_M),
                ("deck_m", Math.Round(deckM, 2)), ("max_height_m", Math.Round(maxH, 2)),
                ("note", "height map grey value x height_step_m = metres above the waterline (0 = sea); " +
                         "mount top_m is the turret roof above the waterline; see shadow.py"))),
            ("mounts", new List<object?>()));
        var mounts = meta.L("mounts");
        foreach (var m in spec.L("turrets").Cast<PyDict>())
        {
            var lm = byId[m.S("id")];
            var md = PyDict.Of(("id", m["id"]), ("kind", lm["kind"]), ("type", m["type"]), ("pos_m", Py.List(m["x"], m["y"])),
                ("px", Py.List(ox + m.F("x") * S, oy + m.F("y") * S)), ("rest_deg", lm["rest"]),
                ("arcs_deg", lm["arcs"]), ("traverse_deg", lm["traverse"]), ("z", m["z"]),
                ("top_m", Math.Round(deckM + lm.F("top"), 2)));
            if (lm.Has("mount"))
                md["mount"] = lm["mount"];
            mounts.Add(md);
        }
        meta.D("layers")["hull_mips"] = MipsFile("hull.png");
        meta.D("shadow")["height_map_mips"] = MipsFile("height.png");
        meta["mip_rects"] = MipRects(W, H, mips);
        foreach (var (_, tv) in tmeta)
        {
            var tm = (PyDict)tv!;
            tm["mips_file"] = MipsFile(tm.S("file"));
            var size = tm.L("size_px");
            tm["mip_rects"] = MipRects(Py.ToLong(size[0]), Py.ToLong(size[1]), mips);
        }
        return new ShipSprites { Meta = meta, Hull = hr.Scene, Turrets = turrets, Height = height, Clutter = hr.Clutter };
    }
}
