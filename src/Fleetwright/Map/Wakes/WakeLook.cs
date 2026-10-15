using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Render.Bake;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Wakes;

/// <summary>The bake's look tool (<c>-wakebake=destroyer:15,25,35</c>, docs/visuals/wakes.md K1): for each speed, the
/// height field (blue troughs, white crests, scaled to the bow wave) and the slopes lit by a sun from the north-west at
/// 45° over a level grey, the waterline drawn dark, bow to the right, starboard down; and a line of numbers.</summary>
public static class WakeLook
{
    public static int Write(string arg, string? outDir, Func<string, string> findDesign)
    {
        var parts = arg.Split(':');
        string path = findDesign(parts[0]);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Design not found: {path}");
            return 1;
        }
        double[] knots = parts.Length > 1 ? [.. parts[1].Split(',').Select(k => double.Parse(k, System.Globalization.CultureInfo.InvariantCulture))] : [15, 25, 35];
        outDir ??= "wakes";
        var cls = ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(path)));
        var hull = WakeHull.Of(cls);
        string name = Path.GetFileNameWithoutExtension(path);
        Console.WriteLine($"{name}: waterline {hull.LengthM:0.0} x {hull.BeamM:0.00} m, draught {hull.DraughtM:0.00} m, entrance {hull.EntranceDeg:0.0}°");
        var scratch = new WakeBake.Scratch();
        foreach (double kn in knots)
        {
            var f = WakeBake.Bake(hull, kn * 0.514444, scratch);
            double hi = f.Eta.Max(), lo = f.Eta.Min();
            Console.WriteLine($"  {kn:0.#} kn: Fr_L {f.FroudeL:0.00}, Zb {f.Zb:0.00} m, η {lo:0.00} to {hi:0.00} m, grid {f.GridX}x{f.GridY} at {f.GridDx:0.00} m, crop {f.X1 - f.X0:0} x {2 * f.HalfY:0} m, {f.BakeMs:0} ms");
            Png.Save(Path.Combine(outDir, $"{name}_{kn:0.#}kn_eta.png"), Eta(f, hull));
            Png.Save(Path.Combine(outDir, $"{name}_{kn:0.#}kn_lit.png"), Lit(f, hull));
        }
        Console.WriteLine($"Written to {Path.GetFullPath(outDir)}");
        return 0;
    }

    // the image shows both sides: row R is port above the track (R < H, starboard row H - 1 - R) and starboard below
    const int W = WakeField.Width, H = WakeField.Height;

    static int Row(int R) => R < H ? H - 1 - R : R - H;

    static double EtaAt(WakeField f, int r, int q) => f.Eta[Math.Clamp(r, 0, H - 1) * W + Math.Clamp(q, 0, W - 1)];

    static Image8 Eta(WakeField f, WakeHull h)
    {
        var im = new Image8(W, 2 * H, 4);
        double s = Math.Max(f.Zb, 1e-6);
        for (int R = 0; R < 2 * H; R++)
            for (int q = 0; q < W; q++)
            {
                double e = Math.Clamp(EtaAt(f, Row(R), q) / s, -1, 1);
                (double cr, double cg, double cb) = e >= 0 ? (0.5 + 0.5 * e, 0.55 + 0.45 * e, 0.6 + 0.4 * e) : (0.5 * (1 + e), 0.55 * (1 + e), 0.6 + 0.2 * e);
                Put(im, R, q, cr, cg, cb, f, h);
            }
        return im;
    }

    static Image8 Lit(WakeField f, WakeHull h)
    {
        var im = new Image8(W, 2 * H, 4);
        // toward the sun: from the north-west (bow to the image's right, so north-west is up and to the left), 45° up
        double lx = -0.5, ly = -0.5, lz = 0.7071;
        for (int R = 0; R < 2 * H; R++)
            for (int q = 0; q < W; q++)
            {
                int r = Row(R);
                double side = R < H ? -1 : 1;   // y grows down the image; port's rows run the other way
                double sx = (EtaAt(f, r, q + 1) - EtaAt(f, r, q - 1)) / (2 * f.TexelX);
                double sy = side * (EtaAt(f, r + 1, q) - EtaAt(f, r == 0 ? 0 : r - 1, q)) / (2 * f.TexelY);
                double nx = -sx * 3, ny = -sy * 3, n = Math.Sqrt(nx * nx + ny * ny + 1);
                double l = Math.Max((nx * lx + ny * ly + lz) / n, 0) / lz;
                Put(im, R, q, 0.45 * l, 0.5 * l, 0.55 * l, f, h);
            }
        return im;
    }

    static void Put(Image8 im, int R, int q, double cr, double cg, double cb, WakeField f, WakeHull h)
    {
        double x = f.X0 + (q + 0.5) * f.TexelX, y = (Row(R) + 0.5) * f.TexelY;
        if (y < h.HalfAt(x))
            (cr, cg, cb) = (0.15, 0.13, 0.12);
        int c = 4 * (R * im.Width + q);
        im.Data[c] = (byte)Math.Clamp(cr * 255, 0, 255);
        im.Data[c + 1] = (byte)Math.Clamp(cg * 255, 0, 255);
        im.Data[c + 2] = (byte)Math.Clamp(cb * 255, 0, 255);
        im.Data[c + 3] = 255;
    }
}
