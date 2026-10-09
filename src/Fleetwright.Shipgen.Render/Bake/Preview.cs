namespace Fleetwright.Shipgen.Render.Bake;

/// <summary>The shaded previews (render.composite and shadow.py's reference shadows), on the CPU: the hull, the
/// turrets turned to their angles, each turret's silhouette cast away from the sun where the hull is lower than its
/// roof, and the height map's shadow over everything, all on a canvas padded so shadows can fall on the sea.</summary>
public static class Preview
{
    public static readonly (double Az, double El) Sun = (300.0, 50.0);   // ship-local bearing and elevation, degrees
    const double ShadowOpacity = 0.4;

    /// <summary>preview_rest.png: turrets at rest.</summary>
    public static Image8 Rest(ShipSprites sp, Baked b) => Compose(sp, b, m => m.RestDeg);

    /// <summary>preview_starboard.png: turrets trained to starboard, as near as their arcs allow.</summary>
    public static Image8 Starboard(ShipSprites sp, Baked b) => Compose(sp, b, m => NearestAllowed(m, 90.0));

    static double NearestAllowed(SpriteMount m, double a)
    {
        var arcs = m.ArcsDeg.Select(x => (Lo: x[0], Hi: x[1])).ToList();
        if (arcs.Count == 0 || Geometry.AngleAllowed(arcs, a))
            return a;
        double best = a, bd = 1e9;
        foreach (var (lo, hi) in arcs)
            foreach (var e in new[] { lo, hi })
            {
                double d = Math.Abs(Geometry.Wrap180(e - a));
                if (d < bd)
                    (best, bd) = (e, d);
            }
        return best;
    }

    public static Image8 Compose(ShipSprites sp, Baked b, Func<SpriteMount, double> angle)
    {
        var meta = sp.Meta;
        double S = meta.ScalePxPerM;
        var sh = meta.Shadow;
        double k = Math.Tan(Sun.El * Math.PI / 180);
        int pad = Math.Max(0, (int)Math.Ceiling(sh.MaxHeightM / k * S));
        int W = b.Hull.Width + 2 * pad, H = b.Hull.Height + 2 * pad;
        var canvas = new double[W * H * 4];          // premultiplied
        Over(canvas, W, H, Premul(b.Hull), b.Hull.Width, b.Hull.Height, pad, pad);
        var turrets = new double[W * H * 4];
        var tShadow = new double[W * H];
        var height = PadHeight(b.Height, pad);
        double az = Sun.Az * Math.PI / 180;
        var rotated = new Dictionary<(string, double), double[]>();   // mounts of one type mostly share an angle
        foreach (var m in meta.Mounts.OrderBy(m => m.Z))
        {
            var img = b.Turrets[m.Type];
            double deg = angle(m);
            if (!rotated.TryGetValue((m.Type, deg), out var rot))
                rotated[(m.Type, deg)] = rot = Rotate(img, deg);
            double cx = m.Px[0] + pad, cy = m.Px[1] + pad;
            int x0 = (int)Math.Round(cx - img.Width / 2.0, MidpointRounding.ToEven);
            int y0 = (int)Math.Round(cy - img.Height / 2.0, MidpointRounding.ToEven);
            Over(turrets, W, H, rot, img.Width, img.Height, x0, y0);
            double L = (m.TopM - sh.DeckM) / k * S;   // shadow.sun_offset_px
            int sx = (int)Math.Round(cx - L * Math.Cos(az) - img.Width / 2.0, MidpointRounding.ToEven);
            int sy = (int)Math.Round(cy - L * Math.Sin(az) - img.Height / 2.0, MidpointRounding.ToEven);
            double top = m.TopM;
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    int X = sx + x, Y = sy + y;
                    if (X < 0 || Y < 0 || X >= W || Y >= H || height[Y * W + X] >= top)
                        continue;
                    tShadow[Y * W + X] = Math.Max(tShadow[Y * W + X], rot[(y * img.Width + x) * 4 + 3]);
                }
        }
        // the turrets over the hull
        for (int i = 0; i < W * H; i++)
        {
            double a = turrets[i * 4 + 3];
            for (int c = 0; c < 4; c++)
                canvas[i * 4 + c] = turrets[i * 4 + c] + canvas[i * 4 + c] * (1 - a);
        }
        // turrets don't shadow themselves; the height map's shadow goes over everything
        var hs = ShadowMask(height, W, H, S, Sun.Az, Sun.El);
        for (int i = 0; i < W * H; i++)
        {
            double mask = Math.Max(tShadow[i] * (1 - turrets[i * 4 + 3]), hs[i]);
            double a = mask * ShadowOpacity;
            for (int c = 0; c < 4; c++)
                canvas[i * 4 + c] = (c == 3 ? a : 0) + canvas[i * 4 + c] * (1 - a);
        }
        var o = new Image8(W, H, 4);
        for (int i = 0; i < W * H; i++)
        {
            double a = canvas[i * 4 + 3];
            o.Data[i * 4 + 3] = (byte)Math.Clamp(Math.Round(a * 255), 0, 255);
            if (a > 0)
                for (int c = 0; c < 3; c++)
                    o.Data[i * 4 + c] = (byte)Math.Clamp(Math.Round(canvas[i * 4 + c] / a * 255), 0, 255);
        }
        return o;
    }

    static double[] Premul(Image8 im)
    {
        var o = new double[im.Width * im.Height * 4];
        for (int i = 0; i < im.Width * im.Height; i++)
        {
            double a = im.Data[i * 4 + 3] / 255.0;
            for (int c = 0; c < 3; c++)
                o[i * 4 + c] = im.Data[i * 4 + c] / 255.0 * a;
            o[i * 4 + 3] = a;
        }
        return o;
    }

    /// <summary>src (premultiplied, w x h) over dst at (x0, y0).</summary>
    static void Over(double[] dst, int W, int H, double[] src, int w, int h, int x0, int y0)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int X = x0 + x, Y = y0 + y;
                if (X < 0 || Y < 0 || X >= W || Y >= H)
                    continue;
                int s = (y * w + x) * 4, d = (Y * W + X) * 4;
                double a = src[s + 3];
                for (int c = 0; c < 4; c++)
                    dst[d + c] = src[s + c] + dst[d + c] * (1 - a);
            }
    }

    /// <summary>The cubic convolution kernel PIL's BICUBIC uses (a = -0.5).</summary>
    static double Cubic(double x)
    {
        const double A = -0.5;
        x = Math.Abs(x);
        if (x < 1)
            return ((A + 2) * x - (A + 3)) * x * x + 1;
        if (x < 2)
            return (((x - 5) * x + 8) * x - 4) * A;
        return 0;
    }

    /// <summary>The image turned clockwise by deg about its centre, the same size (premultiplied, bicubic as PIL's
    /// rotate with BICUBIC, which render.composite and verify.py used).</summary>
    internal static double[] Rotate(Image8 im, double deg)
    {
        var src = Premul(im);
        int w = im.Width, h = im.Height;
        var o = new double[w * h * 4];
        double a = deg * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
        double cx = w / 2.0, cy = h / 2.0;
        Span<double> kx = stackalloc double[4], ky = stackalloc double[4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // where this output pixel's centre comes from: the inverse rotation
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                double sx = ca * dx + sa * dy + cx - 0.5, sy = -sa * dx + ca * dy + cy - 0.5;
                int ix = (int)Math.Floor(sx), iy = (int)Math.Floor(sy);
                if (ix < -2 || iy < -2 || ix > w + 1 || iy > h + 1)
                    continue;
                double fx = sx - ix, fy = sy - iy;
                for (int k = 0; k < 4; k++)
                {
                    kx[k] = Cubic(fx - (k - 1));
                    ky[k] = Cubic(fy - (k - 1));
                }
                int oi = (y * w + x) * 4;
                for (int j = 0; j < 4; j++)
                {
                    int Y = iy + j - 1;
                    if (Y < 0 || Y >= h)
                        continue;
                    for (int i = 0; i < 4; i++)
                    {
                        int X = ix + i - 1;
                        if (X < 0 || X >= w)
                            continue;
                        double k = kx[i] * ky[j];
                        int si = (Y * w + X) * 4;
                        for (int c = 0; c < 4; c++)
                            o[oi + c] += src[si + c] * k;
                    }
                }
                double al = Math.Clamp(o[oi + 3], 0, 1);
                o[oi + 3] = al;
                for (int c = 0; c < 3; c++)
                    o[oi + c] = Math.Clamp(o[oi + c], 0, al);
            }
        return o;
    }

    /// <summary>The height map in metres on the padded canvas (0, the sea, beyond it).</summary>
    static double[] PadHeight(Image8 h, int pad)
    {
        int W = h.Width + 2 * pad, H = h.Height + 2 * pad;
        var o = new double[W * H];
        for (int y = 0; y < h.Height; y++)
            for (int x = 0; x < h.Width; x++)
                o[(y + pad) * W + x + pad] = h.Data[y * h.Width + x] * HeightMap.HeightStepM;
        return o;
    }

    /// <summary>shadow.shadow_mask: per pixel, 0-1, how far a column toward the sun stands above the sun's ray
    /// (softened over 0.3 m), marching a pixel at a time.</summary>
    public static double[] ShadowMask(double[] Hm, int W, int H, double S, double azDeg, double elDeg, double softM = 0.3)
    {
        double k = Math.Tan(elDeg * Math.PI / 180);
        double dx = Math.Cos(azDeg * Math.PI / 180), dy = Math.Sin(azDeg * Math.PI / 180);
        var shade = new double[W * H];
        double hmax = Hm.Length > 0 ? Hm.Max() : 0;
        int tMax = (int)(hmax / k * S) + 1;
        var steps = new List<(int Ox, int Oy, double Rise)>();
        (int, int)? last = null;
        for (int t = 1; t <= tMax; t++)
        {
            int ox = (int)Math.Round(t * dx, MidpointRounding.ToEven), oy = (int)Math.Round(t * dy, MidpointRounding.ToEven);
            if (last == (ox, oy))
                continue;
            last = (ox, oy);
            steps.Add((ox, oy, Math.Sqrt(ox * ox + oy * oy) / S * k));
        }
        int n = steps.Count;
        var sox = new int[n];
        var soy = new int[n];
        var rises = new double[n];
        var minRise = new double[n];   // the lowest rise from each step on: no column beyond it can beat hmax over that
        for (int i = n - 1; i >= 0; i--)
        {
            (sox[i], soy[i], rises[i]) = steps[i];
            minRise[i] = i == n - 1 ? rises[i] : Math.Min(rises[i], minRise[i + 1]);
        }
        // Only columns above the sea can shade (heights are >= 0, so a sea column's s is <= 0), and they lie in the
        // height map's bounding box. Each offset moves one way along the ray, so the steps landing in the box are one run.
        int bx0 = W, by0 = H, bx1 = -1, by1 = -1;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (Hm[y * W + x] > 0)
                    (bx0, by0, bx1, by1) = (Math.Min(bx0, x), Math.Min(by0, y), Math.Max(bx1, x), Math.Max(by1, y));
        if (bx1 < 0)
            return shade;
        Parallel.For(0, H, y =>
        {
            var (ya, yb) = Run(soy, by0 - y, by1 - y);
            for (int x = 0; x < W; x++)
            {
                var (xa, xb) = Run(sox, bx0 - x, bx1 - x);
                double hp = Hm[y * W + x], best = 0;
                for (int i = Math.Max(xa, ya), end = Math.Min(xb, yb); i < end && best < 1; i++)
                {
                    // the same arithmetic as s with occ = hmax, so the cut-off is exact
                    if ((hmax - (hp + minRise[i])) / softM <= best)
                        break;
                    double s = (Hm[(y + soy[i]) * W + x + sox[i]] - (hp + rises[i])) / softM;
                    if (s > best)
                        best = Math.Min(1, s);
                }
                shade[y * W + x] = Math.Floor(best * 255) / 255;
            }
        });
        return shade;
    }

    /// <summary>The steps [a, b) whose offset is in [lo, hi], for offsets that only grow or only shrink.</summary>
    static (int, int) Run(int[] off, int lo, int hi)
    {
        int n = off.Length;
        if (n == 0)
            return (0, 0);
        return off[n - 1] >= off[0]
            ? (First(off, lo, true), First(off, hi + 1, true))
            : (First(off, hi, false), First(off, lo - 1, false));
    }

    /// <summary>The first index whose offset is past v: >= v along growing offsets, <= v along shrinking ones.</summary>
    static int First(int[] off, int v, bool growing)
    {
        int a = 0, b = off.Length;
        while (a < b)
        {
            int m = (a + b) >> 1;
            if (growing ? off[m] >= v : off[m] <= v)
                b = m;
            else
                a = m + 1;
        }
        return a;
    }
}
