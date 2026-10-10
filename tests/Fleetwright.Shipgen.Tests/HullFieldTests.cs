namespace Fleetwright.Shipgen.Tests;

/// <summary>HullField against the exported hull form it compiles: the same half-breadths, ray crossings where a fine
/// march finds the hull's surface, outward normals, and no garbage.</summary>
public class HullFieldTests
{
    // wall-sided, tumblehome, tumblehome with a forecastle, a small ship, and one with raised stretches
    static readonly string[] Names = ["bismarck", "bouvet", "danton", "destroyer", "richelieu"];

    static readonly Dictionary<string, Hitboxes> Built = Names.ToDictionary(n => n,
        n => ShipDesign.Build(Design.Load(Paths.Shipgen("designs", $"{n}.json"))).Hitboxes);

    public static TheoryData<string> Designs => [.. Names];

    [Theory]
    [MemberData(nameof(Designs))]
    public void HalfWidth_matches_the_report(string name)
    {
        var hb = Built[name];
        var f = new HullField(hb.HullForm, hb.Vertical);
        var rng = new Random(1);
        for (int k = 0; k < 20000; k++)
        {
            double x = f.X0 - 1 + (f.X1 - f.X0 + 2) * rng.NextDouble(), z = f.Keel - 1 + (f.Top - f.Keel + 2) * rng.NextDouble();
            double want = hb.HullForm.HalfWidth(x, z), got = f.HalfWidth(x, z);
            Assert.True(Math.Abs(want - got) < 1e-9, $"{name} at x {x}, z {z}: report {want}, field {got}");
        }
    }

    [Theory]
    [MemberData(nameof(Designs))]
    public void Raycast_finds_where_a_march_crosses_the_surface(string name)
    {
        var hb = Built[name];
        var f = new HullField(hb.HullForm, hb.Vertical);
        var rng = new Random(2);
        const double Step = 0.01;
        int rays = 1000, bad = 0, outward = 0, sides = 0;
        var notes = new List<string>();
        for (int k = 0; k < rays; k++)
        {
            // from well outside, at a random point in the hull's box: shells, splinters, sight lines
            double az = 2 * Math.PI * rng.NextDouble(), el = (rng.NextDouble() - 0.3) * 1.4;
            double tx = f.X0 + (f.X1 - f.X0) * rng.NextDouble(), ty = f.YMax * (2 * rng.NextDouble() - 1);
            double tz = f.Keel + (f.Top - f.Keel) * rng.NextDouble();
            double R = f.X1 - f.X0;
            double dx = Math.Cos(el) * Math.Cos(az), dy = Math.Cos(el) * Math.Sin(az), dz = Math.Sin(el);
            double ox = tx - R * dx, oy = ty - R * dy, oz = tz - R * dz;
            bool hitRay = f.Raycast(ox, oy, oz, dx, dy, dz, 2 * R, out var hit);
            // every crossing along the ray, in and out in turn (the ray starts outside)
            var ts = new List<double>();
            for (double t0 = 0; ts.Count < 64 && f.Raycast(ox + t0 * dx, oy + t0 * dy, oz + t0 * dz, dx, dy, dz, 2 * R - t0, out var h);)
            {
                ts.Add(t0 + h.T);
                t0 += h.T + 1e-7;
            }
            // the march: the first step whose inside-ness differs from the start's
            double tm = double.NaN;
            for (double t = Step; t <= 2 * R; t += Step)
                if (f.Inside(ox + t * dx, oy + t * dy, oz + t * dz))
                {
                    tm = t;
                    break;
                }
            // it agrees if it changed just after a crossing, and every in-and-out before it is too short for a step
            // to land in (a graze)
            int at = ts.FindIndex(t => t > tm - Step - 1e-6);
            bool Grazes(int upTo) => Enumerable.Range(0, upTo / 2).All(p => ts[2 * p + 1] - ts[2 * p] < Step);
            bool agree = double.IsNaN(tm) ? ts.Count % 2 == 0 && Grazes(ts.Count)
                : at >= 0 && at % 2 == 0 && ts[at] <= tm + 1e-9 && Grazes(at);
            if (!agree)
            {
                bad++;
                notes.Add($"ray {k}: march {tm:F3}, raycast {(hitRay ? hit.T.ToString("F3") + " " + hit.Face : "none")}");
            }
            if (hitRay && hit.Face == HullFace.Side)
            {
                sides++;
                double e = 0.03;
                if (!f.Inside(hit.X + e * hit.Nx, hit.Y + e * hit.Ny, hit.Z + e * hit.Nz) && f.Inside(hit.X - e * hit.Nx, hit.Y - e * hit.Ny, hit.Z - e * hit.Nz))
                    outward++;
                Assert.Equal(1.0, Math.Sqrt(hit.Nx * hit.Nx + hit.Ny * hit.Ny + hit.Nz * hit.Nz), 9);
            }
        }
        Assert.True(bad == 0, $"{name}: {bad} of {rays} rays disagree:\n{string.Join("\n", notes.Take(10))}");
        Assert.True(sides > rays / 4, $"{name}: only {sides} side hits");
        Assert.True(outward >= 0.97 * sides, $"{name}: {outward} of {sides} side normals point out");
    }

    [Fact]
    public void Queries_allocate_nothing()
    {
        var hb = Built["bouvet"];
        var f = new HullField(hb.HullForm, hb.Vertical);
        double sum = 0;
        void Run()
        {
            for (int k = 0; k < 2000; k++)
            {
                double x = -60 + 0.06 * k;
                sum += f.HalfWidth(x, -3.0) + f.DeckTop(x) + (f.Inside(x, 1.0, -2.0) ? 1 : 0);
                if (f.Raycast(x, -40, 5, 0.1, 1, -0.2, 100, out var hit))
                    sum += hit.T;
            }
        }
        Run();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }
}
