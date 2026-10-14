namespace Fleetwright.Shipgen.Render.Bake;

/// <summary>A relief layer: metres per pixel (over the waterline on the hull, over the base on a turret), rows top to
/// bottom; 0 where no draw with a profile reached (<see cref="GpuBaker.RenderRelief"/>).</summary>
public sealed class ReliefImage(int width, int height)
{
    public readonly int Width = width, Height = height;
    public readonly float[] M = new float[width * height];
}

/// <summary>The sprites' surface for the lighting (docs/visuals/ship-lighting.md, "The bake"), made from the relief on
/// the CPU, on the building thread: a normal per pixel and ambient occlusion, as RGBA8 (xyz biased to 0-255 in the
/// sprite's frame: x to the bow (+x), y to starboard (+y, down the rows), z up; AO in a, 1 open). The relief is blurred
/// about a pixel and differentiated (Sobel), its slopes clamped, so a wall reads as a bright or dark rim a pixel or two
/// wide rather than a black line: the rim on every block's edge is most of the realism. AO is how far the surroundings
/// stand above a pixel within a couple of metres (the deck darkens at a deckhouse's foot, inside gun tubs, round the
/// barbettes). Where the sprite is empty the normal is straight up and AO 1; the mips are averaged by coverage, so a
/// silhouette's edge doesn't take the empty canvas's normal, and never renormalised (a shortened normal is the
/// roughness's input later: Toksvig).</summary>
public static class ReliefMaps
{
    /// <summary>The steepest slope a normal shows (rise over run): about 68°, so a wall is a rim, not a black line.</summary>
    public const double MaxSlope = 2.5;

    /// <summary>How far AO looks, metres, and how dark a pixel walled in on every side gets (1 - this).</summary>
    public static readonly double[] AoReachM = [0.25, 0.5, 1.0, 1.5, 2.0];
    public const double AoStrength = 0.85;

    /// <summary>The normal and AO of every pixel. <paramref name="coverage"/>: the layer's colour (straight alpha), the
    /// same size; empty where its alpha is 0.</summary>
    public static Image8 NormalAo(ReliefImage r, double pxPerM, Image8 coverage)
    {
        int w = r.Width, h = r.Height;
        if (coverage.Width != w || coverage.Height != h || coverage.Channels != 4)
            throw new ArgumentException("coverage must be the relief's size, RGBA");
        var b = Blur(r.M, w, h);
        var ao = new float[w * h];
        var taps = AoTaps(pxPerM);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                ao[y * w + x] = coverage.Data[(y * w + x) * 4 + 3] == 0 ? 1 : (float)Ao(r, x, y, taps);
        ao = Blur(ao, w, h);   // the taps' pattern smoothed away
        var o = new Image8(w, h, 4);
        var d = o.Data;
        double k = pxPerM / 8;   // a Sobel sum is 8 x the slope per pixel
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x, oi = i * 4;
                if (coverage.Data[oi + 3] == 0)
                {
                    (d[oi], d[oi + 1], d[oi + 2], d[oi + 3]) = ((byte)128, (byte)128, (byte)255, (byte)255);
                    continue;
                }
                int xl = Math.Max(x - 1, 0), xr = Math.Min(x + 1, w - 1), yu = Math.Max(y - 1, 0), yd = Math.Min(y + 1, h - 1);
                double gx = (b[yu * w + xr] + 2 * b[y * w + xr] + b[yd * w + xr] - b[yu * w + xl] - 2 * b[y * w + xl] - b[yd * w + xl]) * k;
                double gy = (b[yd * w + xl] + 2 * b[yd * w + x] + b[yd * w + xr] - b[yu * w + xl] - 2 * b[yu * w + x] - b[yu * w + xr]) * k;
                double g = Math.Sqrt(gx * gx + gy * gy);
                if (g > MaxSlope)
                {
                    gx *= MaxSlope / g;
                    gy *= MaxSlope / g;
                }
                double inv = 1 / Math.Sqrt(gx * gx + gy * gy + 1);
                d[oi] = Biased(-gx * inv);
                d[oi + 1] = Biased(-gy * inv);
                d[oi + 2] = Biased(inv);
                d[oi + 3] = (byte)Math.Round(255 * ao[i]);
            }
        return o;
    }

    static byte Biased(double v) => (byte)Math.Clamp(Math.Round((v * 0.5 + 0.5) * 255), 0, 255);

    /// <summary>A w x h layer blurred by [1 2 1]/4 each way, the edges clamped.</summary>
    static float[] Blur(float[] m, int w, int h)
    {
        var t = new float[m.Length];
        var b = new float[m.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                t[i] = (m[y * w + Math.Max(x - 1, 0)] + 2 * m[i] + m[y * w + Math.Min(x + 1, w - 1)]) / 4;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                b[i] = (t[Math.Max(y - 1, 0) * w + x] + 2 * t[i] + t[Math.Min(y + 1, h - 1) * w + x]) / 4;
            }
        return b;
    }

    /// <summary>Per direction (16), the pixel offsets at each AO reach and the reach in metres.</summary>
    static (int Dx, int Dy, double M)[][] AoTaps(double pxPerM)
    {
        return [.. Enumerable.Range(0, 16).Select(k =>
        {
            double a = k * Math.PI / 8;
            return AoReachM.Select(reach =>
            {
                double px = Math.Max(reach * pxPerM, 1);
                int dx = (int)Math.Round(Math.Cos(a) * px), dy = (int)Math.Round(Math.Sin(a) * px);
                return (dx, dy, Math.Sqrt(dx * dx + dy * dy) / pxPerM);
            }).Where(t => t.Item1 != 0 || t.Item2 != 0).Distinct().ToArray();
        })];
    }

    /// <summary>1 open, less where the surroundings stand above the pixel: per direction the sine of the highest
    /// horizon among the taps, averaged over the directions.</summary>
    static double Ao(ReliefImage r, int x, int y, (int Dx, int Dy, double M)[][] taps)
    {
        int w = r.Width, h = r.Height;
        float hp = r.M[y * w + x];
        double sum = 0;
        foreach (var dir in taps)
        {
            double best = 0;
            foreach (var (dx, dy, m) in dir)
            {
                int qx = x + dx, qy = y + dy;
                float hq = qx < 0 || qy < 0 || qx >= w || qy >= h ? 0 : r.M[qy * w + qx];
                best = Math.Max(best, (hq - hp) / m);
            }
            sum += best / Math.Sqrt(1 + best * best);
        }
        return 1 - AoStrength * sum / taps.Length;
    }

    /// <summary>The normal + AO mip chain to go with a colour chain (the same levels): each level the previous one
    /// halved, its pixels averaged by their coverage (the colour level's alpha), not renormalised; straight up and
    /// open where nothing covers.</summary>
    public static Image8[] Chain(Image8 level0, IReadOnlyList<Image8> colour)
    {
        var chain = new Image8[colour.Count];
        chain[0] = level0;
        for (int k = 1; k < colour.Count; k++)
        {
            var src = chain[k - 1];
            var cov = colour[k - 1];
            var o = new Image8(src.Width / 2, src.Height / 2, 4);
            for (int y = 0; y < o.Height; y++)
                for (int x = 0; x < o.Width; x++)
                {
                    double wsum = 0, nx = 0, ny = 0, nz = 0, ao = 0;
                    for (int j = 0; j < 4; j++)
                    {
                        int i = ((2 * y + (j >> 1)) * src.Width + 2 * x + (j & 1)) * 4;
                        double wgt = cov.Data[i + 3];
                        wsum += wgt;
                        nx += wgt * src.Data[i];
                        ny += wgt * src.Data[i + 1];
                        nz += wgt * src.Data[i + 2];
                        ao += wgt * src.Data[i + 3];
                    }
                    int oi = (y * o.Width + x) * 4;
                    if (wsum == 0)
                        (o.Data[oi], o.Data[oi + 1], o.Data[oi + 2], o.Data[oi + 3]) = ((byte)128, (byte)128, (byte)255, (byte)255);
                    else
                    {
                        o.Data[oi] = (byte)Math.Round(nx / wsum);
                        o.Data[oi + 1] = (byte)Math.Round(ny / wsum);
                        o.Data[oi + 2] = (byte)Math.Round(nz / wsum);
                        o.Data[oi + 3] = (byte)Math.Round(ao / wsum);
                    }
                }
            chain[k] = o;
        }
        return chain;
    }

    /// <summary>The relief as grey for looking at: black 0, white the tallest.</summary>
    public static Image8 Preview(ReliefImage r)
    {
        var o = new Image8(r.Width, r.Height, 1);
        float max = Math.Max(r.M.Max(), 1e-3f);
        for (int i = 0; i < r.M.Length; i++)
            o.Data[i] = (byte)Math.Clamp(Math.Round(r.M[i] / max * 255), 0, 255);
        return o;
    }
}
