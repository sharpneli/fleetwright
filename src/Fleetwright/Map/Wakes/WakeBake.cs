using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Wakes;

/// <summary>What the wake bake needs of a hull: its level waterline as half-breadths every <see cref="Dx"/> metres from
/// the stern (<see cref="X0"/>, ship frame) to the stem, its draught and the entrance's half-angle. From the hitbox
/// model's hull (<see cref="Of"/>), so the wake comes off the hull the hit queries see.</summary>
public sealed class WakeHull
{
    public required double[] Half { get; init; }
    public required double X0 { get; init; }
    public required double Dx { get; init; }
    public required double DraughtM { get; init; }

    public double X1 => X0 + (Half.Length - 1) * Dx;
    public double LengthM => X1 - X0;
    public double MidX => 0.5 * (X0 + X1);
    public double BeamM { get; private init; }

    /// <summary>The waterline's half-angle at the entrance, degrees: its mean slope over the forward 8% of its length,
    /// held to 6-30° (a fine destroyer's ~8-12°, a battleship's 10-18°: research §2.3).</summary>
    public double EntranceDeg { get; private init; }

    const int Samples = 2000;

    public static WakeHull Of(ShipClass c)
    {
        var hull = c.Hull;
        double wl = c.Vertical.Waterline, a = hull.StationX(0), b = hull.StationX(hull.StripCount);
        double step = (b - a) / Samples;
        int lo = 0, hi = Samples;
        while (lo < hi && hull.HalfWidth(a + lo * step, wl) <= 1e-3)
            lo++;
        while (hi > lo && hull.HalfWidth(a + hi * step, wl) <= 1e-3)
            hi--;
        // a sample to either side, where the waterline closes: the half-breadth goes to nothing there
        lo = Math.Max(lo - 1, 0);
        hi = Math.Min(hi + 1, Samples);
        var half = new double[hi - lo + 1];
        for (int i = 0; i < half.Length; i++)
            half[i] = Math.Max(hull.HalfWidth(a + (lo + i) * step, wl), 0);
        return Make(half, a + lo * step, step, c.Motion.DraughtM);
    }

    /// <summary>A hull from half-breadths (tests and the look tool's stand-ins).</summary>
    public static WakeHull Make(double[] half, double x0, double dx, double draughtM)
    {
        double beam = 2 * half.Max(), length = (half.Length - 1) * dx;
        // the entrance: from the stem back 8% of the length
        int back = Math.Max(1, (int)Math.Round(0.08 * length / dx));
        double run = back * dx, rise = half[^(back + 1)] - half[^1];
        double entrance = Math.Clamp(Math.Atan2(rise, run) * 180 / Math.PI, 6, 30);
        return new WakeHull { Half = half, X0 = x0, Dx = dx, DraughtM = draughtM, BeamM = beam, EntranceDeg = entrance };
    }

    /// <summary>A transom's dead water: behind a transom the hull's run is carried on by a hollow that closes at about
    /// this half-angle (degrees), so the forcing doesn't end in a step (the linear model's stern overshoot, research §4).
    /// For the forcing alone: the water there is still water.</summary>
    const double DeadWaterDeg = 12;

    /// <summary>The transom's half-breadth (0: the waterline closes at the stern) and the dead water's length behind it.</summary>
    public double TransomHalf => Half.Length > 1 && Half[0] <= 1e-3 ? Half[1] : Half[0];
    public double DeadWaterM => TransomHalf / Math.Tan(DeadWaterDeg * Math.PI / 180);

    /// <summary>The half-breadth the bake's forcing sees: the waterline, carried on behind a transom by its dead water
    /// (a cosine closing over <see cref="DeadWaterM"/>).</summary>
    public double ForcingAt(double x)
    {
        double stern = X0 + Dx;   // the transom: the first sample past the closing one
        if (x >= stern || TransomHalf <= 1e-3)
            return HalfAt(x);
        double s = (stern - x) / DeadWaterM;
        return s >= 1 ? 0 : TransomHalf * (0.5 + 0.5 * Math.Cos(Math.PI * s));
    }

    /// <summary>The half-breadth at x (ship frame), linear between samples, 0 outside the waterline.</summary>
    public double HalfAt(double x)
    {
        double f = (x - X0) / Dx;
        if (f <= 0 || f >= Half.Length - 1)
            return 0;
        int i = (int)f;
        return Half[i] + (Half[i + 1] - Half[i]) * (f - i);
    }
}

/// <summary>A ship's steady wave field at one speed, near the hull: the crop the stamps draw (docs/visuals/wakes.md K1).
/// <see cref="Width"/> x <see cref="Height"/> texels over x from <see cref="X0"/> (aft) to <see cref="X1"/> (fore) and
/// y from -<see cref="HalfY"/> (port, row 0) to +<see cref="HalfY"/> (starboard), ship frame metres, texel centres at
/// the cells' middles. Per texel: the height (m) and the slope moments ∂η/∂x, ∂η/∂y, their squares (LEAN: the mips
/// average them into the variance the pixel can't resolve).</summary>
public sealed class WakeField
{
    public const int Width = 384, Height = 256;

    public readonly float[] Eta = new float[Width * Height];
    public readonly float[] Moments = new float[Width * Height * 4];
    public double X0, X1, HalfY;
    public double SpeedMs, Zb, FroudeL, EntranceDeg;
    /// <summary>The bake's own grid: cells, spacing, and how long it took.</summary>
    public int GridX, GridY;
    public double GridDx, BakeMs;

    public double TexelX => (X1 - X0) / Width;
    public double TexelY => 2 * HalfY / Height;
}

/// <summary>The steady Kelvin wave field of a hull at one speed (docs/visuals/wakes.md K1; research/vfx/wake_bake_ref_v2.py
/// <c>bake2</c>, its height half): a thin-ship source q = U·∂m/∂x on the waterplane m, the steady linear free surface by
/// one forward and one inverse FFT in the ship's frame, with Rayleigh damping (waves only astern) and the draught's
/// attenuation; the stem's crest scaled to Noblesse's bow-wave height; the hull zeroed, the field faded astern and at the
/// crop's edges. The foam half of the research's bake is left out. Pure CPU, deterministic; the scratch is reused.</summary>
public static class WakeBake
{
    const double G = 9.81;

    /// <summary>The bake's grid: at most this many cells a side, and the hull resolved to L/80 where that fits.</summary>
    public const int MaxCells = 1024;
    const double CellsPerLength = 80;

    /// <summary>Rayleigh damping, as a fraction of g/U: enforces waves astern, and fades the far wedge before it wraps
    /// round the grid's back edge to show beside the bow. Slow, the grid is sized by the length and the wrap is far off,
    /// so it is light and the Kelvin arms keep their reach; fast, the grid is sized by the waves and the wrap comes
    /// close, so it is heavier (the research's 0.08 showed streaks at 39 kn; 0.3 throughout erased the arms at 16).</summary>
    const double DampingSlow = 0.12, DampingFast = 0.3, DampingFromFr = 0.3, DampingToFr = 0.55;

    /// <summary>The linear model overshoots at a transom and at high Froude (a trough of 10 m beside a destroyer at
    /// 39 kn, research §4): η is limited softly, A·tanh(η/A), at this many bow-wave heights.</summary>
    const double LimitZb = 1.25;

    /// <summary>The crop: this many lengths astern of the stern, and the share of the span ahead of the stem.</summary>
    public const double CropAsternL = 3, CropAheadSpan = 0.3;

    /// <summary>The fade astern, 1/(1 + behind/(FadeL·L)): the Kelvin arms read near the ship and soften further back.</summary>
    const double FadeL = 1.5;

    /// <summary>The crop's edges fade over this share of it, so a stamp's border never shows.</summary>
    const double EdgeShare = 0.1;

    /// <summary>Scratch for one baker at a time: the grids and the FFTs by size.</summary>
    public sealed class Scratch
    {
        internal float[] Re = [], Im = [], ColRe = [], ColIm = [], RowRe = [], RowIm = [];
        readonly Dictionary<int, Fft> ffts = [];

        internal Fft FftOf(int n)
        {
            if (!ffts.TryGetValue(n, out var f))
                ffts[n] = f = new Fft(n);
            return f;
        }

        internal void Ensure(int cells, int side)
        {
            if (Re.Length < cells)
                (Re, Im) = (new float[cells], new float[cells]);
            if (ColRe.Length < side)
                (ColRe, ColIm, RowRe, RowIm) = (new float[side], new float[side], new float[side], new float[side]);
        }
    }

    /// <summary>Noblesse's bow-wave height, m: Zb·g/U² = 2.2·tan β / (cos β·(1 + Fr_D)), Fr_D = U/√(gT) (research §2.3).</summary>
    public static double BowWaveHeight(double speedMs, double draughtM, double entranceDeg)
    {
        double beta = entranceDeg * Math.PI / 180, frD = speedMs / Math.Sqrt(G * draughtM);
        return 2.2 * speedMs * speedMs / G * Math.Tan(beta) / (Math.Cos(beta) * (1 + frD));
    }

    public static WakeField Bake(WakeHull hull, double speedMs, Scratch s, WakeField? into = null)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        double L = hull.LengthM, B = hull.BeamM, T = hull.DraughtM, U = Math.Max(speedMs, 0.5), xm = hull.MidX;
        double lambda = 2 * Math.PI * U * U / G, span = Math.Max(L, lambda);

        // the domain (research §2.4), x and y from the waterline's middle
        double ahead = 0.5 * L + 0.6 * span, behind = 0.5 * L + 4 * span, halfW = 0.45 * behind + B;
        int nx = Pow2(Math.Min((ahead + behind) * CellsPerLength / L, MaxCells));
        double dx = (ahead + behind) / nx;
        int ny = Pow2(Math.Min(2 * halfW / dx, MaxCells));
        s.Ensure(nx * ny, Math.Max(nx, ny));
        var (re, im) = (s.Re, s.Im);
        Array.Clear(re, 0, nx * ny);
        Array.Clear(im, 0, nx * ny);
        double X(int i) => -behind + i * dx;
        double Y(int j) => (j - ny / 2) * dx;

        // the waterplane, anti-aliased: per column, four sub-columns, each covering its rows exactly
        int jLo = ny, jHi = -1;
        for (int i = 0; i < nx; i++)
            for (int sub = 0; sub < 4; sub++)
            {
                double h = hull.ForcingAt(xm + X(i) + ((sub + 0.5) / 4 - 0.5) * dx);
                if (h <= 0)
                    continue;
                int r = (int)Math.Ceiling(h / dx) + 1;
                for (int j = Math.Max(ny / 2 - r, 0); j <= Math.Min(ny / 2 + r, ny - 1); j++)
                {
                    double y = Y(j), cover = Math.Min(y + 0.5 * dx, h) - Math.Max(y - 0.5 * dx, -h);
                    if (cover > 0)
                    {
                        re[j * nx + i] += (float)(0.25 * Math.Min(cover / dx, 1));
                        (jLo, jHi) = (Math.Min(jLo, j), Math.Max(jHi, j));
                    }
                }
            }

        // Two symmetries halve the work. The waterplane is real, so its spectrum is Hermitian: the columns kx > 0 are
        // the conjugates of kx < 0, and two real rows go through one complex FFT. And it is even across the track
        // (row j is row ny - j), as is the response, so η is too: only the starboard half is brought back.
        var fx = s.FftOf(nx);
        var fy = s.FftOf(ny);
        int half = nx / 2;

        // forward along x: the rows that hold the hull (the rest are zero), two real rows at a time
        for (int j = jLo; j <= jHi; j += 2)
        {
            int j2 = j + 1;
            Span<float> ra = re.AsSpan(j * nx, nx), ia = im.AsSpan(j * nx, nx);
            if (j2 <= jHi)
                re.AsSpan(j2 * nx, nx).CopyTo(ia);
            fx.Transform(ra, ia, false);
            if (j2 > jHi)
                continue;
            // Z = A + iB: A[k] = (Z[k] + conj Z[-k]) / 2, B[k] = (Z[k] - conj Z[-k]) / 2i
            Span<float> rb = re.AsSpan(j2 * nx, nx), ib = im.AsSpan(j2 * nx, nx);
            Span<float> tr = s.RowRe.AsSpan(0, nx), ti = s.RowIm.AsSpan(0, nx);
            for (int k = 0; k < nx; k++)
            {
                int nk = (nx - k) & (nx - 1);
                float zr = ra[k], zi = ia[k], wr = ra[nk], wi = ia[nk];
                (tr[k], ti[k]) = (0.5f * (zr + wr), 0.5f * (zi - wi));
                (rb[k], ib[k]) = (0.5f * (zi + wi), 0.5f * (wr - zr));
            }
            tr.CopyTo(ra);
            ti.CopyTo(ia);
        }
        // along y: the columns kx = 0 ... nx/2 (the rest are their conjugates)
        Columns(fy, re, im, nx, ny, half + 1, s, false);

        // the response, in place: η̂ = g·U·kx²·m̂·smooth·att / D, D = g|k| - U²kx² - i·ε·U·kx. The light smoothing
        // (σ ≈ 0.04 B, research §6, and at least 1.5 cells: the waves near the grid's Nyquist alias into streaks) and
        // q = U ∂m/∂x are taken here, free. The factor depends on |ky| alone: one for row j and row ny - j.
        double fr = Math.Clamp((U / Math.Sqrt(G * L) - DampingFromFr) / (DampingToFr - DampingFromFr), 0, 1);
        double sigma = Math.Max(1.5 * dx, 0.04 * B), eps = (DampingSlow + (DampingFast - DampingSlow) * fr * fr * (3 - 2 * fr)) * G / U;
        double dkx = 2 * Math.PI / (nx * dx), dky = 2 * Math.PI / (ny * dx);
        var smoothX = s.RowRe.AsSpan(0, half + 1);
        for (int i = 0; i <= half; i++)
            smoothX[i] = (float)Math.Exp(-0.5 * sigma * sigma * (i * dkx) * (i * dkx));
        for (int j = 0; j <= ny / 2; j++)
        {
            double ky = j * dky, smoothY = Math.Exp(-0.5 * sigma * sigma * ky * ky);
            int jm = (ny - j) & (ny - 1);
            for (int i = 0; i <= half; i++)
            {
                double kx = i * dkx, k = Math.Sqrt(kx * kx + ky * ky);
                double fr2 = 0, fi2 = 0;
                if (k > 1e-9)
                {
                    // num / (dr + i·di)
                    double att = (1 - Math.Exp(-k * T)) / k;
                    double num = G * U * kx * kx * att * smoothX[i] * smoothY;
                    double dr = G * k - U * U * kx * kx, di = -eps * U * kx, den = dr * dr + di * di;
                    (fr2, fi2) = (num * dr / den, -num * di / den);
                }
                Mul(re, im, j * nx + i, fr2, fi2);
                if (jm != j)
                    Mul(re, im, jm * nx + i, fr2, fi2);
            }
        }

        // inverse: the same columns, then the starboard rows the crop needs, two at a time (each one's transform is
        // real, so Z = Pa + i·Pb comes back as ηa + i·ηb)
        var f = into ?? new WakeField();
        f.X0 = xm - 0.5 * L - CropAsternL * L;
        f.X1 = xm + 0.5 * L + CropAheadSpan * span;
        f.HalfY = 0.36 * (f.X1 - f.X0) + B;
        Columns(fy, re, im, nx, ny, half + 1, s, true);
        int rLo = ny / 2;
        int rHi = Math.Clamp((int)Math.Ceiling(f.HalfY / dx) + ny / 2 + 2, rLo + 1, ny - 1);
        for (int j = rLo; j <= rHi; j += 2)
        {
            int j2 = Math.Min(j + 1, rHi);
            Span<float> ra = re.AsSpan(j * nx, nx), ia = im.AsSpan(j * nx, nx);
            Span<float> rb = re.AsSpan(j2 * nx, nx), ib = im.AsSpan(j2 * nx, nx);
            Span<float> tr = s.RowRe.AsSpan(0, nx), ti = s.RowIm.AsSpan(0, nx);
            for (int i = 0; i < nx; i++)
            {
                // the conjugate half of each row: P[nx - i] = conj P[i]
                int src = i <= half ? i : nx - i;
                float sg = i <= half ? 1 : -1;
                float ar = ra[src], ai = sg * ia[src], br = j2 != j ? rb[src] : 0, bi = j2 != j ? sg * ib[src] : 0;
                (tr[i], ti[i]) = (ar - bi, ai + br);
            }
            fx.Transform(tr, ti, true);
            tr.CopyTo(ra);
            if (j2 != j)
                ti.CopyTo(rb);
        }

        // η on the bake's grid, bilinear (re holds it, unscaled), into the crop's starboard half, mirrored to port
        double Eta(double x, double y)
        {
            double gx = (x - xm + behind) / dx, gy = y / dx + ny / 2;
            int i = (int)Math.Floor(gx), j = (int)Math.Floor(gy);
            if (i < 0 || j < rLo || i >= nx - 1 || j >= rHi)
                return 0;
            double u = gx - i, v = gy - j;
            int c = j * nx + i;
            return (re[c] * (1 - u) + re[c + 1] * u) * (1 - v) + (re[c + nx] * (1 - u) + re[c + nx + 1] * u) * v;
        }
        double tx = f.TexelX, ty = f.TexelY;
        for (int r = WakeField.Height / 2; r < WakeField.Height; r++)
        {
            double y = -f.HalfY + (r + 0.5) * ty;
            int m = WakeField.Height - 1 - r;
            for (int q = 0; q < WakeField.Width; q++)
                f.Eta[m * WakeField.Width + q] = f.Eta[r * WakeField.Width + q] = (float)Eta(f.X0 + (q + 0.5) * tx, y);
        }

        // the stem's crest, positive and scaled to Zb: the highest water just off the hull round the stem
        double stem = xm + 0.5 * L, crest = 0, trough = 0;
        for (int r = 0; r < WakeField.Height; r++)
        {
            double y = -f.HalfY + (r + 0.5) * ty;
            if (Math.Abs(y) > B)
                continue;
            for (int q = 0; q < WakeField.Width; q++)
            {
                double x = f.X0 + (q + 0.5) * tx;
                if (Math.Abs(x - stem) > 0.05 * L || Math.Abs(y) < hull.HalfAt(x))
                    continue;
                double e = f.Eta[r * WakeField.Width + q];
                (crest, trough) = (Math.Max(crest, e), Math.Min(trough, e));
            }
        }
        double sign = crest >= -trough ? 1 : -1, peak = Math.Max(crest, -trough);
        f.Zb = BowWaveHeight(U, T, hull.EntranceDeg);
        double scale = peak > 1e-12 ? sign * f.Zb / peak : 0;

        // shaping: nothing inside the hull (soft over a texel), faded astern and at the crop's edges
        for (int r = 0; r < WakeField.Height; r++)
        {
            double y = -f.HalfY + (r + 0.5) * ty, ey = Edge(r, WakeField.Height);
            for (int q = 0; q < WakeField.Width; q++)
            {
                double x = f.X0 + (q + 0.5) * tx;
                double outside = Math.Clamp((Math.Abs(y) - hull.HalfAt(x)) / ty + 0.5, 0, 1);
                double back = Math.Max(xm - 0.5 * L - x, 0), fade = 1 / (1 + back / (FadeL * L));
                int c = r * WakeField.Width + q;
                double e = f.Eta[c] * scale, a = LimitZb * f.Zb;
                f.Eta[c] = (float)(a * Math.Tanh(e / a) * outside * fade * ey * Edge(q, WakeField.Width));
            }
        }

        // the slopes and their squares, central differences (one-sided at the edges, where the fade has it at nothing)
        for (int r = 0; r < WakeField.Height; r++)
            for (int q = 0; q < WakeField.Width; q++)
            {
                int c = r * WakeField.Width + q;
                int q0 = Math.Max(q - 1, 0), q1 = Math.Min(q + 1, WakeField.Width - 1);
                int r0 = Math.Max(r - 1, 0), r1 = Math.Min(r + 1, WakeField.Height - 1);
                float sx = (float)((f.Eta[r * WakeField.Width + q1] - f.Eta[r * WakeField.Width + q0]) / ((q1 - q0) * tx));
                float sy = (float)((f.Eta[r1 * WakeField.Width + q] - f.Eta[r0 * WakeField.Width + q]) / ((r1 - r0) * ty));
                f.Moments[4 * c] = sx;
                f.Moments[4 * c + 1] = sy;
                f.Moments[4 * c + 2] = sx * sx;
                f.Moments[4 * c + 3] = sy * sy;
            }

        f.SpeedMs = U;
        f.FroudeL = U / Math.Sqrt(G * L);
        f.EntranceDeg = hull.EntranceDeg;
        (f.GridX, f.GridY, f.GridDx) = (nx, ny, dx);
        f.BakeMs = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        return f;
    }

    static double Edge(int i, int n)
    {
        double t = Math.Min(i + 0.5, n - i - 0.5) / (EdgeShare * n);
        if (t >= 1)
            return 1;
        return t * t * (3 - 2 * t);
    }

    static int Pow2(double cells)
    {
        int n = 64;
        while (n < cells && n < MaxCells)
            n <<= 1;
        return n;
    }

    static void Mul(float[] re, float[] im, int c, double fr, double fi)
    {
        double a = re[c], b = im[c];
        re[c] = (float)(a * fr - b * fi);
        im[c] = (float)(a * fi + b * fr);
    }

    /// <summary>The first <paramref name="count"/> columns transformed along y.</summary>
    static void Columns(Fft f, float[] re, float[] im, int nx, int ny, int count, Scratch s, bool inverse)
    {
        Span<float> cr = s.ColRe.AsSpan(0, ny), ci = s.ColIm.AsSpan(0, ny);
        for (int i = 0; i < count; i++)
        {
            for (int j = 0; j < ny; j++)
                (cr[j], ci[j]) = (re[j * nx + i], im[j * nx + i]);
            f.Transform(cr, ci, inverse);
            for (int j = 0; j < ny; j++)
                (re[j * nx + i], im[j * nx + i]) = (cr[j], ci[j]);
        }
    }
}
