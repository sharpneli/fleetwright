namespace Fleetwright.Shipgen;

/// <summary>What a ray crossed first: the hull's side, a deck top, the end wall of a raised stretch, or a flat end of the
/// hull (a transom).</summary>
public enum HullFace : byte
{
    Side,
    Deck,
    Break,
    End,
}

/// <summary>A ray's first crossing of the hull: the ray parameter, the point, the outward unit normal and the face.</summary>
public readonly record struct HullHit(double T, double X, double Y, double Z, double Nx, double Ny, double Nz, HullFace Face);

/// <summary>The exported hull (Hitboxes.HullForm and the deck tops) compiled for queries: the half-breadth, inside
/// tests and ray crossings, in ship-local metres with heights over the main deck. No call allocates.
/// <para>The form is a height field per side: |y| = W(x, z). Between two stations, split at both stations' heights, W is
/// exactly bilinear in (x, z) (HullFormReport.HalfWidth interpolates linearly in z per station, then in x), so a ray
/// meets a cell's side where one quadratic is zero: the crossing is exact for the table, and the table is the
/// physical hull. Above the top row W keeps the top row's value; the hull ends at DeckTop(x).</para></summary>
public sealed class HullField
{
    readonly double[] xs;       // the stations' x, ascending
    readonly int[] first;       // strip i (stations i, i + 1): rows first[i] .. first[i + 1] - 1
    readonly double[] zs, wa, wb;   // a strip's rows: height, half-breadth at its aft and fore station
    readonly double[] stripMax;     // a strip's widest half-breadth
    readonly double[] deckX, deckTop;   // DeckTop: deckTop[k] over deckX[k] .. deckX[k + 1]

    public readonly double X0, X1, Keel, Top, YMax;
    const double StepH = 1e-7;

    public HullField(HullFormReport form, Vertical vertical)
    {
        var st = form.Stations;
        if (st.Count < 2)
            throw new ArgumentException("the hull form has fewer than two stations");
        int n = st.Count;
        xs = st.Select(s => s.X).ToArray();
        first = new int[n];
        var z = new List<double>();
        var a = new List<double>();
        var b = new List<double>();
        stripMax = new double[n - 1];
        for (int i = 0; i < n - 1; i++)
        {
            first[i] = z.Count;
            // both stations' heights, and just under each place a station's width jumps (its bottom, where it has
            // width already, as at a stern cut up above the waterline; a height repeated by the rounding): there the
            // form steps, and the row under it keeps the step a step
            var rows = st[i].Z.Concat(st[i + 1].Z).Concat(Jumps(st[i])).Concat(Jumps(st[i + 1])).Distinct().Order().ToList();
            foreach (var r in rows)
            {
                z.Add(r);
                a.Add(AtAbove(st[i], r));
                b.Add(AtAbove(st[i + 1], r));
                stripMax[i] = Math.Max(stripMax[i], Math.Max(a[^1], b[^1]));
            }
        }
        first[n - 1] = z.Count;
        (zs, wa, wb) = (z.ToArray(), a.ToArray(), b.ToArray());
        (X0, X1) = (xs[0], xs[^1]);
        YMax = stripMax.Max();
        Keel = zs.Min();

        // the deck tops: the main deck (0), and the raised stretches over it, highest wins
        var cuts = new SortedSet<double> { X0, X1 };
        var raised = vertical.Raised ?? [];
        foreach (var r in raised)
        {
            cuts.Add(Math.Clamp(r.X0, X0, X1));
            cuts.Add(Math.Clamp(r.X1, X0, X1));
        }
        deckX = cuts.ToArray();
        deckTop = new double[deckX.Length - 1];
        for (int k = 0; k < deckTop.Length; k++)
        {
            double xm = (deckX[k] + deckX[k + 1]) / 2;
            deckTop[k] = raised.Where(r => r.X0 <= xm && xm <= r.X1).Select(r => r.Top).Append(0.0).Max();
        }
        Top = deckTop.Max();
    }

    /// <summary>HullFormReport's per-station interpolation, as it is just above z (the two differ only where the width
    /// jumps, at a repeated height).</summary>
    static double AtAbove(StationReport s, double z)
    {
        var (zz, ys) = (s.Z, s.Y);
        if (z >= zz[^1])
            return ys[^1];
        if (z < zz[0])
            return 0.0;
        int j = 0;
        while (zz[j + 1] <= z)
            j++;
        return ys[j] + (ys[j + 1] - ys[j]) * (z - zz[j]) / (zz[j + 1] - zz[j]);
    }

    /// <summary>Just under each height where the station's width jumps: its bottom, if it has width there, and each
    /// repeated height whose widths differ.</summary>
    static IEnumerable<double> Jumps(StationReport s)
    {
        var (zz, ys) = (s.Z, s.Y);
        if (AtAbove(s, zz[0]) > 0)
            yield return zz[0] - StepH;
        for (int j = 1; j < zz.Length; j++)
            if (zz[j] == zz[j - 1] && ys[j] != ys[j - 1] && j > 1)
                yield return zz[j] - StepH;
    }

    /// <summary>The strip x falls in (HullFormReport's choice at a station: the strip aft of it), or -1 off the ends.</summary>
    int Strip(double x)
    {
        if (x <= xs[0] || x >= xs[^1])
            return -1;
        int j = Array.BinarySearch(xs, x);
        if (j < 0)
            j = ~j;
        return Math.Clamp(j - 1, 0, xs.Length - 2);
    }

    /// <summary>The row k of strip i whose band zs[k] .. zs[k + 1] holds z; the last row when z is at or above it, and
    /// -1 under the first.</summary>
    int Row(int i, double z)
    {
        int lo = first[i], hi = first[i + 1] - 1;
        if (z < zs[lo])
            return -1;
        if (z >= zs[hi])
            return hi;
        int j = Array.BinarySearch(zs, lo, hi - lo + 1, z);
        if (j < 0)
            j = ~j - 1;
        return j;
    }

    /// <summary>The half-breadth at x and z (over the main deck): HullFormReport.HalfWidth, by binary search.</summary>
    public double HalfWidth(double x, double z)
    {
        int i = Strip(x);
        if (i < 0)
            return 0.0;
        int k = Row(i, z);
        if (k < 0)
            return 0.0;
        double fx = (x - xs[i]) / (xs[i + 1] - xs[i]);
        if (k == first[i + 1] - 1)
            return wa[k] + (wb[k] - wa[k]) * fx;
        double fz = (z - zs[k]) / (zs[k + 1] - zs[k]);
        double a = wa[k] + (wa[k + 1] - wa[k]) * fz, b = wb[k] + (wb[k + 1] - wb[k]) * fz;
        return a + (b - a) * fx;
    }

    /// <summary>The strips between stations, for walking the surface cell by cell (a mesh of it).</summary>
    public int StripCount => xs.Length - 1;

    public double StationX(int i) => xs[i];

    /// <summary>Strip i's row heights, ascending.</summary>
    public ReadOnlySpan<double> RowHeights(int i) => zs.AsSpan(first[i], first[i + 1] - first[i]);

    /// <summary>The half-breadth in strip i at fx (0: its aft station, 1: its fore) and z; unlike HalfWidth, exact on the
    /// strip's own stations, the hull's ends included.</summary>
    public double InStrip(int i, double fx, double z)
    {
        int k = Row(i, z);
        if (k < 0)
            return 0.0;
        if (k == first[i + 1] - 1)
            return wa[k] + (wb[k] - wa[k]) * fx;
        double fz = (z - zs[k]) / (zs[k + 1] - zs[k]);
        double a = wa[k] + (wa[k + 1] - wa[k]) * fz, b = wb[k] + (wb[k + 1] - wb[k]) * fz;
        return a + (b - a) * fx;
    }

    /// <summary>Where the deck's height changes along the length (the hull's ends first and last), and the heights
    /// between: DeckTops[k] over DeckBreaks[k] .. DeckBreaks[k + 1].</summary>
    public ReadOnlySpan<double> DeckBreaks => deckX;

    public ReadOnlySpan<double> DeckTops => deckTop;

    /// <summary>The hull's top at x: the main deck (0), or a raised stretch's deck.</summary>
    public double DeckTop(double x)
    {
        int j = Array.BinarySearch(deckX, x);
        if (j < 0)
            j = ~j - 1;
        return deckTop[Math.Clamp(j, 0, deckTop.Length - 1)];
    }

    /// <summary>Is the point inside the hull (under its deck, within its side)?</summary>
    public bool Inside(double x, double y, double z) => z <= DeckTop(x) && Math.Abs(y) < HalfWidth(x, z);

    /// <summary>The ray o + t d's first crossing of the hull's surface for t in (0, tMax], in either direction.</summary>
    public bool Raycast(double ox, double oy, double oz, double dx, double dy, double dz, double tMax, out HullHit hit)
    {
        hit = default;
        double best = tMax;
        bool found = false;
        // the bounding box first: most rays at a ship miss it or cross it briefly
        if (!Slab(ox, dx, X0, X1, ref best, out double t0) || !Slab(oy, dy, -YMax, YMax, ref best, out double ty)
            || !Slab(oz, dz, Keel, Top, ref best, out double tz))
            return false;
        t0 = Math.Max(t0, Math.Max(ty, tz));
        if (t0 > best)
            return false;
        best = Math.Min(tMax, best + 1e-9);   // nothing to find past the box's far side

        // the side: the strips the ray's x crosses, each split at its rows
        int i0 = Strip(Math.Clamp(ox + t0 * dx, X0 + 1e-9, X1 - 1e-9));
        int step = dx >= 0 ? 1 : -1;
        for (int i = i0; i >= 0 && i < xs.Length - 1; i += step)
        {
            double ta, tb;
            if (Math.Abs(dx) < 1e-12)
                (ta, tb) = (0.0, best);
            else
            {
                double t1 = (xs[i] - ox) / dx, t2 = (xs[i + 1] - ox) / dx;
                (ta, tb) = (Math.Max(0.0, Math.Min(t1, t2)), Math.Min(best, Math.Max(t1, t2)));
            }
            if (ta > best)
                break;
            if (ta > tb)
                continue;
            // skip the strip if the ray stays wider than it the whole way
            double ya = Math.Abs(oy + ta * dy), yb = Math.Abs(oy + tb * dy);
            bool crossesCentre = (oy + ta * dy) * (oy + tb * dy) <= 0;
            if (!crossesCentre && Math.Min(ya, yb) > stripMax[i])
                continue;
            found |= SideOfStrip(i, ox, oy, oz, dx, dy, dz, ta, tb, ref best, ref hit);
            if (Math.Abs(dx) < 1e-12)
                break;
        }

        // the hull's flat ends: a transom (or a stem with width at its station), where the end station has width
        if (Math.Abs(dx) > 1e-12)
            foreach (int e in Sides)
            {
                double xe = e > 0 ? X1 : X0, t = (xe - ox) / dx;
                if (t <= 0 || t >= best)
                    continue;
                double y = oy + t * dy, z = oz + t * dz;
                int st = e > 0 ? xs.Length - 2 : 0;
                double w = EndWidth(st, e > 0, z);
                if (z <= DeckTop(xe) && Math.Abs(y) < w)
                {
                    best = t;
                    hit = new HullHit(t, xe, y, z, e, 0, 0, HullFace.End);
                    found = true;
                }
            }

        // the deck tops and the raised stretches' end walls
        for (int k = 0; k < deckTop.Length; k++)
        {
            double top = deckTop[k];
            if (Math.Abs(dz) > 1e-12)
            {
                double t = (top - oz) / dz;
                if (t > 0 && t < best)
                {
                    double x = ox + t * dx, y = oy + t * dy;
                    if (x >= deckX[k] && x <= deckX[k + 1] && Math.Abs(y) < HalfWidth(x, top))
                    {
                        best = t;
                        hit = new HullHit(t, x, y, top, 0, 0, 1, HullFace.Deck);
                        found = true;
                    }
                }
            }
            if (k > 0 && deckTop[k - 1] != top && Math.Abs(dx) > 1e-12)
            {
                double xb = deckX[k], t = (xb - ox) / dx;
                double lo = Math.Min(top, deckTop[k - 1]), hi = Math.Max(top, deckTop[k - 1]);
                if (t > 0 && t < best)
                {
                    double y = oy + t * dy, z = oz + t * dz;
                    if (z > lo && z <= hi && Math.Abs(y) < HalfWidth(xb, z))
                    {
                        best = t;
                        hit = new HullHit(t, xb, y, z, top > deckTop[k - 1] ? -1 : 1, 0, 0, HullFace.Break);
                        found = true;
                    }
                }
            }
        }
        return found;
    }

    /// <summary>The end station's half-breadth at z (just inside the hull: HalfWidth is 0 on the end station itself).</summary>
    double EndWidth(int i, bool fore, double z)
    {
        int k = Row(i, z);
        if (k < 0)
            return 0.0;
        var w = fore ? wb : wa;
        if (k == first[i + 1] - 1)
            return w[k];
        double fz = (z - zs[k]) / (zs[k + 1] - zs[k]);
        return w[k] + (w[k + 1] - w[k]) * fz;
    }

    /// <summary>Clips the ray to lo .. hi along one axis: the entry t (0 if inside), and best shrunk to the exit.</summary>
    static bool Slab(double o, double d, double lo, double hi, ref double best, out double t0)
    {
        t0 = 0.0;
        if (Math.Abs(d) < 1e-12)
            return o >= lo && o <= hi;
        double ta = (lo - o) / d, tb = (hi - o) / d;
        if (ta > tb)
            (ta, tb) = (tb, ta);
        t0 = Math.Max(0.0, ta);
        best = Math.Min(best, tb);
        return t0 <= best;
    }

    bool SideOfStrip(int i, double ox, double oy, double oz, double dx, double dy, double dz, double ta, double tb, ref double best,
        ref HullHit hit)
    {
        bool found = false;
        double xa = xs[i], lx = xs[i + 1] - xa;
        double u0 = (ox - xa) / lx, u1 = dx / lx;
        int last = first[i + 1] - 1;
        for (int k = first[i]; k <= last; k++)
        {
            // the cell's heights: up to the next row, or for the last row up to the top (W constant in z there)
            double zLo = zs[k], zHi = k < last ? zs[k + 1] : Top;
            if (zHi <= zLo)
                continue;
            double c0 = ta, c1 = Math.Min(tb, best);
            if (Math.Abs(dz) < 1e-12)
            {
                if (oz < zLo || oz > zHi)
                    continue;
            }
            else
            {
                double s1 = (zLo - oz) / dz, s2 = (zHi - oz) / dz;
                c0 = Math.Max(c0, Math.Min(s1, s2));
                c1 = Math.Min(c1, Math.Max(s1, s2));
            }
            if (c0 > c1)
                continue;
            // W = A + B u + C v + D u v over the cell, u along x, v along z (v fixed at 0 in the last row)
            double w00 = wa[k], w10 = wb[k];
            double w01 = k < last ? wa[k + 1] : w00, w11 = k < last ? wb[k + 1] : w10;
            double lz = k < last ? zHi - zLo : 1.0;
            double v0 = k < last ? (oz - zLo) / lz : 0.0, v1 = k < last ? dz / lz : 0.0;
            double A = w00, B = w10 - w00, C = w01 - w00, D = w11 - w10 - w01 + w00;
            double q2 = D * u1 * v1;
            double q1 = B * u1 + C * v1 + D * (u0 * v1 + u1 * v0);
            double q0 = A + B * u0 + C * v0 + D * u0 * v0;
            foreach (int s in Sides)
            {
                // s (oy + dy t) = W(t)
                double t = FirstRoot(-q2, s * dy - q1, s * oy - q0, c0, c1);
                if (!(t < best) || t <= 0)
                    continue;
                double x = ox + t * dx, z = oz + t * dz;
                if (z > DeckTop(x) + 1e-9)
                    continue;
                double u = u0 + u1 * t, v = v0 + v1 * t;
                double gx = (B + D * v) / lx, gz = k < last ? (C + D * u) / lz : 0.0;
                double nx = -gx, ny = s, nz = -gz, len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                best = t;
                hit = new HullHit(t, x, oy + t * dy, z, nx / len, ny / len, nz / len, HullFace.Side);
                found = true;
            }
        }
        return found;
    }

    static readonly int[] Sides = [1, -1];

    /// <summary>The smallest root of a t² + b t + c in lo .. hi, or +∞.</summary>
    static double FirstRoot(double a, double b, double c, double lo, double hi)
    {
        double scale = Math.Abs(b) + Math.Abs(c) + 1e-300;
        if (Math.Abs(a) < 1e-12 * scale)
        {
            if (Math.Abs(b) < 1e-300)
                return double.PositiveInfinity;
            double t = -c / b;
            return t >= lo && t <= hi ? t : double.PositiveInfinity;
        }
        double disc = b * b - 4 * a * c;
        if (disc < 0)
            return double.PositiveInfinity;
        double sq = Math.Sqrt(disc);
        // the stable pair: q = -(b + sign(b) sq) / 2, roots q / a and c / q
        double q = -0.5 * (b + (b >= 0 ? sq : -sq));
        double r1 = q / a, r2 = q != 0 ? c / q : r1;
        if (r1 > r2)
            (r1, r2) = (r2, r1);
        if (r1 >= lo && r1 <= hi)
            return r1;
        return r2 >= lo && r2 <= hi ? r2 : double.PositiveInfinity;
    }
}
