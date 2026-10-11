namespace Fleetwright.Shipgen.Render;

/// <summary>Dazzle camouflage (shapes "dazzle"): slanted panels cut across the whole ship, in a repeatable pattern per
/// design, and wedges driven into some of them from a side. One scheme serves both views. The plan sees each cut as a
/// straight line across the ship, x = c + k y. The profile sees the same cut carried over the edge and down the side as
/// if the side were unrolled flat beside the deck: at depth d below an edge at y, x = c + k (y + d). So a panel's edges
/// meet across every deck and roof edge, and a wedge from the starboard edge runs on down the side.</summary>
public sealed class DazzleScheme
{
    /// <summary>A panel between two cuts (c0, k0) and (c1, k1), or a wedge from one side (Side, its base Mid ± Half at
    /// the side, its tip at (Tx, Ty)), in a colour.</summary>
    public readonly record struct Piece(bool Wedge, double C0, double K0, double C1, double K1, int Side, double Mid, double Half, double Tx,
        double Ty, string Colour);

    /// <summary>The pieces in drawing order, and the half-width the plan's panels and wedge bases reach to.</summary>
    public readonly List<Piece> Pieces;
    public readonly double Y;

    DazzleScheme(List<Piece> pieces, double y) => (Pieces, Y) = (pieces, y);

    /// <summary>The scheme for a ship of length l and beam b, seeded by its id.</summary>
    public static DazzleScheme Of(string id, double l, double b, IReadOnlyList<string> colours)
    {
        var rng = new ShipRng($"{id}/dazzle");
        double Y = b / 2 + 2.0;
        long n = Math.Max(5L, (long)Math.Round(l / 18));
        double step = l / n;
        var cuts = new List<(double C, double K)> { (-l / 2 - 2.0, 0.0) };
        for (int i = 1; i < n; i++)
        {
            double c = -l / 2 + step * i + rng.Uniform(-0.2, 0.2) * step;
            cuts.Add((c, rng.Uniform(-0.45, 0.45) * step / Y));
        }
        cuts.Add((l / 2 + 2.0, 0.0));
        var pieces = new List<Piece>();
        string? last = null;
        for (int i = 0; i + 1 < cuts.Count; i++)
        {
            var ((c0, k0), (c1, k1)) = (cuts[i], cuts[i + 1]);
            var opts = new List<string?> { null };
            opts.AddRange(colours);
            var col = rng.Choice(opts.Where(c => c != last).ToList());
            last = col;
            if (col != null)
                pieces.Add(new Piece(false, c0, k0, c1, k1, 0, 0, 0, 0, 0, col));
            if (rng.Random() < 0.5)   // a wedge from one side into the panel, in another colour
            {
                int side = rng.Choice([-1, 1]);
                double mid = (c0 + c1) / 2;
                var wcol = rng.Choice(colours.Where(c => c != col).ToList());
                double tx = mid + rng.Uniform(-0.3, 0.3) * step;
                double ty = side * Y * rng.Uniform(-0.2, 0.4);
                pieces.Add(new Piece(true, 0, 0, 0, 0, side, mid, 0.35 * step, tx, ty, wcol));
            }
        }
        return new DazzleScheme(pieces, Y);
    }

    /// <summary>The plan's polygons (ship metres, y to starboard).</summary>
    public List<(List<Pt>, string)> Plan() => Pieces.Select(p => (p.Wedge
        ? new List<Pt> { new(p.Mid - p.Half, p.Side * Y), new(p.Mid + p.Half, p.Side * Y), new(p.Tx, p.Ty) }
        : [new(p.C0 - p.K0 * Y, -Y), new(p.C1 - p.K1 * Y, -Y), new(p.C1 + p.K1 * Y, Y), new(p.C0 + p.K0 * Y, Y)], p.Colour)).ToList();

    /// <summary>The profile's polygons for a surface whose top edge is at y = edge(x) (its starboard side, following the
    /// hull's narrowing ends) and height top, reaching down to height bottom, in the profile's scene coordinates (x,
    /// -height). A panel's edge is the plan's cut carried over that edge and down; a starboard wedge comes over the edge
    /// as a diamond, the plan's triangle mirrored about its base, so it meets the plan's at the edge.</summary>
    public List<(List<Pt>, string)> Side(Func<double, double> edge, double top, double bottom)
    {
        var o = new List<(List<Pt>, string)>();
        // unrolled: u = edge(x) + (top - z), the distance from the centreline over the edge and down
        Pt At(double x, double u) => new(x, -(top - (u - edge(x))));
        const int n = 8;
        double d0 = -1.0, d1 = top - bottom + 1.0;
        // the cut x = c + k u at depth d below the edge, u = edge(x) + d: a few steps of the fixed point
        Pt Cut(double c, double k, double d)
        {
            double x = c + k * (edge(c) + d);
            for (int i = 0; i < 4; i++)
                x = c + k * (edge(x) + d);
            return new(x, -(top - d));
        }
        foreach (var p in Pieces)
        {
            if (!p.Wedge)
            {
                var pts = new List<Pt>();
                for (int i = 0; i <= n; i++)
                    pts.Add(Cut(p.C0, p.K0, d0 + (d1 - d0) * i / n));
                for (int i = n; i >= 0; i--)
                    pts.Add(Cut(p.C1, p.K1, d0 + (d1 - d0) * i / n));
                o.Add((pts, p.Colour));
            }
            else if (p.Side > 0)
                o.Add(([At(p.Tx, p.Ty), At(p.Mid + p.Half, Y), At(p.Tx, 2 * Y - p.Ty), At(p.Mid - p.Half, Y)], p.Colour));
        }
        return o;
    }
}
