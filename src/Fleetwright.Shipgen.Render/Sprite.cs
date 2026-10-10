namespace Fleetwright.Shipgen.Render;

/// <summary>The height map: the static part of the ship (hull, superstructure, funnels, masts, AA, boats, barbettes and
/// the clutter) as columns, grey = metres above the waterline / HeightStepM (0 = sea). Every pixel is a solid
/// column from the sea up to its height; the game casts shadows from it (see ../shipgen/shadow.py for the reference
/// shadow march until it moves here).</summary>
public static class HeightMap
{
    public const double HeightStepM = 0.25;      // one grey level = 0.25 m; 255 = 63.75 m above the waterline

    public static Rgb Grey(double hM) => Rgb.Grey((int)Math.Max(0L, Math.Min(255L, (long)Math.Round(hM / HeightStepM))));

    /// <summary>The height map from the columns (lowest first, so the taller one wins where they overlap), on the
    /// hull's canvas; hull: the hull as drawn (a look may fill it out). Also returns the tallest column.</summary>
    public static (Scene Scene, double MaxHeight) Build(IReadOnlyList<HeightColumn> columns, Scene hullScene, Hull hull)
    {
        var sc = new Scene(hullScene.X, hullScene.Y, hullScene.W, hullScene.H, hullScene.Scale);
        var o = sc.Root.Items;
        o.Add(new RectNode(sc.X, sc.Y, sc.W, sc.H).Fill("#000"));
        foreach (var c in columns)
        {
            Node n = c.Shape switch
            {
                "hull" => new PathNode(Painter.HullPath(hull)),
                "polygon" => new PathNode(Painter.Poly(c.Points!)),
                "rect" => new RectNode(c.X!.Value, c.Y!.Value, c.W!.Value, c.H!.Value),
                "circle" => new CircleNode(c.Cx!.Value, c.Cy!.Value, c.R!.Value),
                "ellipse" => new EllipseNode(c.Cx!.Value, c.Cy!.Value, c.Rx!.Value, c.Ry!.Value),
                var sh => throw new ArgumentException($"unknown column shape '{sh}'"),
            };
            o.Add(n.Fill(Grey(c.Top)));
        }
        return (sc, columns.Max(c => c.Top));
    }
}

/// <summary>A designed ship's sprites as display lists, and sprite.json (render.render_ship's layout half): the hull
/// and each turret type drawn in the ship's look, the height map, canvas sizes, mount pixels, mips and shadow
/// data. The rasteriser (Step 5) turns the scenes into the PNGs that sprite.json names.</summary>
public sealed class ShipSprites
{
    public required SpriteMeta Meta;                // sprite.json
    public required Scene Hull;
    public required Dictionary<string, Scene> Turrets;
    public required Scene Height;
    public required List<Clutter.Item> Clutter;

    public const string Orientation = "bow points +x (right); angles clockwise, 0 = ahead";

    /// <summary>[x, y, w, h] of each mip level in the packed image: level 0 on the left, level 1 to its right at the
    /// top, then each next level alternately below and to the right of the previous one. The whole chain fits in
    /// 1.5w x h. Canvases are multiples of 2^(levels+1) px, so every rect is whole pixels with an even size.</summary>
    public static List<long[]> MipRects(long w, long h, int levels)
    {
        var rects = new List<long[]> { new[] { 0L, 0L, w, h } };
        long x = w, y = 0;
        for (int k = 1; k <= levels; k++)
        {
            long lw = w >> k, lh = h >> k;
            rects.Add([x, y, lw, lh]);
            if (k % 2 == 1)
                y += lh;
            else
                x += lw;
        }
        return rects;
    }

    static string MipsFile(string rel) => rel[..^".png".Length] + "_mips.png";

    /// <summary>Draw a designed ship at S px/m with the given mip levels. look (navy, era; either may be left out)
    /// overrides the design's own look key by key, so the game can repaint a design into a later era without touching
    /// it.</summary>
    public static ShipSprites Build(Ship ship, double S, int mips = 0, LookInput? look = null)
    {
        var design = ship.Design;
        var v = TopView.Of(ship);
        if (look != null)
        {
            var own = Looks.LookOf(design);
            design = design with { Look = own with { Navy = look.Navy ?? own.Navy, Era = look.Era ?? own.Era } };
        }
        int align = 1 << (mips + 1);
        var pal = Looks.Palette(design);
        var shapes = Looks.Shapes(design);
        string turretLook = Looks.TurretLook(design);

        var turrets = new Dictionary<string, Scene>(StringComparer.Ordinal);
        var tmeta = new OrderedDictionary<string, TurretSprite>(StringComparer.Ordinal);
        foreach (var (tid, t) in v.TurretTypes)
        {
            var sc = TurretArt.Build(t, pal, S, align, turretLook, shapes);
            turrets[tid] = sc;
            long w = sc.WidthPx, h = sc.HeightPx;
            string file = $"turrets/{tid}.png";
            tmeta[tid] = new TurretSprite(file, [w, h], [w / 2.0, h / 2.0], t.Desc, t.CalibreMm, MipsFile(file), MipRects(w, h, mips))
                { CalibreLength = t.CalibreLength };
        }

        var hr = HullArt.Build(v, pal, shapes, S, align);
        double deckM = v.DeckM;
        var fixedColumns = v.HeightColumns();
        var columns = fixedColumns.Concat(Render.Clutter.HeightColumns(hr.Clutter, fixedColumns, hr.Hull)).ToList();
        var (height, maxH) = HeightMap.Build(columns, hr.Scene, hr.Hull);
        long W = hr.Scene.WidthPx, H = hr.Scene.HeightPx;
        double ox = W / 2.0, oy = H / 2.0;
        var mounts = v.Mounts.Select(m => new SpriteMount(m.Id, m.Kind, m.Type, [m.X, m.Y], [ox + m.X * S, oy + m.Y * S], m.Rest, m.Arcs,
            m.Traverse, m.Z, Math.Round(deckM + m.Top, 2)) { Mount = m.Casemate ? "casemate" : null }).ToList();
        var meta = new SpriteMeta
        {
            Id = design.Id, Name = design.Name ?? design.Id, ScalePxPerM = S, SizePx = [W, H], OriginPx = [ox, oy],
            Layers = new SpriteLayers("hull.png", MipsFile("hull.png")), TurretTypes = tmeta,
            Mips = new SpriteMips(mips, [.. Enumerable.Range(0, mips + 1).Select(k => S / (1L << k))],
                "each layer has <name>_mips.png with level k at mip_rects[k] = [x, y, w, h]; " +
                "within level k, size, origin_px, pivot_px and mount px are the level-0 values / 2^k"),
            Shadow = new SpriteShadow("height.png", HeightMap.HeightStepM, Math.Round(deckM, 2), Math.Round(maxH, 2),
                "height map grey value x height_step_m = metres above the waterline (0 = sea); " +
                "mount top_m is the turret roof above the waterline; see shadow.py", MipsFile("height.png")),
            Mounts = mounts, MipRects = MipRects(W, H, mips),
        };
        return new ShipSprites { Meta = meta, Hull = hr.Scene, Turrets = turrets, Height = height, Clutter = hr.Clutter };
    }
}
