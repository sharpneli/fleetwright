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

    static Image8 Eta(WakeField f, WakeHull h)
    {
        var im = new Image8(WakeField.Width, WakeField.Height, 4);
        double s = Math.Max(f.Zb, 1e-6);
        for (int r = 0; r < WakeField.Height; r++)
            for (int q = 0; q < WakeField.Width; q++)
            {
                double e = Math.Clamp(f.Eta[r * WakeField.Width + q] / s, -1, 1);
                (double cr, double cg, double cb) = e >= 0 ? (0.5 + 0.5 * e, 0.55 + 0.45 * e, 0.6 + 0.4 * e) : (0.5 * (1 + e), 0.55 * (1 + e) , 0.6 + 0.2 * e);
                Put(im, r, q, cr, cg, cb, f, h);
            }
        return im;
    }

    static Image8 Lit(WakeField f, WakeHull h)
    {
        var im = new Image8(WakeField.Width, WakeField.Height, 4);
        // toward the sun: from the north-west (bow up the image's right, so north-west is up and to the left), 45° up
        double lx = -0.5, ly = -0.5, lz = 0.7071;
        for (int r = 0; r < WakeField.Height; r++)
            for (int q = 0; q < WakeField.Width; q++)
            {
                int c = 4 * (r * WakeField.Width + q);
                double nx = -f.Moments[c] * 3, ny = -f.Moments[c + 1] * 3, n = Math.Sqrt(nx * nx + ny * ny + 1);
                double l = Math.Max((nx * lx + ny * ly + lz) / n, 0) / lz;
                Put(im, r, q, 0.45 * l, 0.5 * l, 0.55 * l, f, h);
            }
        return im;
    }

    static void Put(Image8 im, int r, int q, double cr, double cg, double cb, WakeField f, WakeHull h)
    {
        double x = f.X0 + (q + 0.5) * f.TexelX, y = -f.HalfY + (r + 0.5) * f.TexelY;
        if (Math.Abs(y) < h.HalfAt(x))
            (cr, cg, cb) = (0.15, 0.13, 0.12);
        int c = 4 * (r * im.Width + q);
        im.Data[c] = (byte)Math.Clamp(cr * 255, 0, 255);
        im.Data[c + 1] = (byte)Math.Clamp(cg * 255, 0, 255);
        im.Data[c + 2] = (byte)Math.Clamp(cb * 255, 0, 255);
        im.Data[c + 3] = 255;
    }
}
