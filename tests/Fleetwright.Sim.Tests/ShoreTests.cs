using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The land index (docs/world/terrain-plan.md T1) against <see cref="Land"/> itself and brute force over
/// every segment: land or sea, the distance bounds, clearances and first contacts, the straits, no allocation.</summary>
public class ShoreTests
{
    static Shore Shore => TestLand.Shore;
    static Land Land => TestLand.Shore.Land;

    // the LandTests places, at 1:10M
    static readonly (string Name, double Lat, double Lon, bool Land)[] Places =
    [
        ("Paris", 48.86, 2.35, true), ("Moscow", 55.75, 37.62, true), ("the Sahara", 23, 13, true),
        ("the Great Plains", 40, -100, true), ("Brasília", -15.8, -47.9, true), ("by the South Pole", -89.9, 45, true),
        ("Australia's middle", -25, 134, true), ("Hoy, Orkney", 58.95, -3.10, true),
        ("the North Sea, Jutland bank", 56.7, 5.8, false), ("the mid-Atlantic", 30, -40, false),
        ("the Pacific", 0, -150, false), ("the North Pole", 90, 0, false), ("the Indian Ocean", -20, 80, false),
        ("the Caspian", 42, 51, false), ("the Mediterranean", 35, 18, false),
    ];

    [Fact]
    public void Knows_land_from_sea()
    {
        foreach (var (name, lat, lon, want) in Places)
            Assert.True(Shore.IsLand(Earth.Up(lat, lon)) == want, $"{name} should be {(want ? "land" : "sea")}");
    }

    [Fact]
    public void Agrees_with_the_land_everywhere()
    {
        var rng = new Random(1);
        int n = 0;
        foreach (var p in Uniform(rng, 4000).Concat(NearCoast(rng, 4000, 5000)).Concat(NearSeams(rng, 2000)))
        {
            Assert.True(Shore.IsLand(p) == Land.IsLand(p), $"at {Earth.LatLon(p)}: the index says {(Shore.IsLand(p) ? "land" : "sea")}");
            n++;
        }
        Assert.Equal(10_000, n);
    }

    [Fact]
    public void Bounds_are_never_over_the_distance()
    {
        var rng = new Random(2);
        foreach (var p in Uniform(rng, 300).Concat(NearCoast(rng, 300, 30_000)).Concat(NearSeams(rng, 300)))
        {
            double bound = Shore.BoundM(p);
            double d = BruteDistance(p, bound + 120_000, out _);
            Assert.True(bound <= d + 0.01, $"at {Earth.LatLon(p)}: bound {bound:F0} m, coast at {d:F0} m");
        }
    }

    [Fact]
    public void Clearance_is_the_nearest_segment()
    {
        var rng = new Random(3);
        foreach (var p in NearCoast(rng, 400, 20_000).Concat(NearSeams(rng, 100)))
        {
            double got = Shore.Clearance(p, 20_000, out var at);
            double want = BruteDistance(p, 20_000, out var wantAt);
            if (want >= 20_000)
            {
                Assert.True(double.IsPositiveInfinity(got), $"at {Earth.LatLon(p)}: {got:F1} m, nothing within 20 km");
                continue;
            }
            Assert.True(Math.Abs(got - want) < 0.01, $"at {Earth.LatLon(p)}: {got:F3} m, want {want:F3} m");
            Assert.True(Earth.Distance(at, wantAt) < 0.01 || Math.Abs(Earth.Distance(p, at) - Earth.Distance(p, wantAt)) < 0.01,
                $"at {Earth.LatLon(p)}: nearest {Earth.LatLon(at)}, want {Earth.LatLon(wantAt)}");
        }
    }

    [Fact]
    public void First_contact_is_where_the_track_comes_that_close()
    {
        var rng = new Random(4);
        foreach (var p in NearCoast(rng, 300, 8000))
        {
            double course = rng.NextDouble() * 360, r = 50 + 450 * rng.NextDouble();
            var forward = Earth.Forward(p, course);
            double hit = Shore.FirstContact(p, forward, 20_000, r);
            CheckTrack(p, hit, r, 0.5, 0.5, s =>
            {
                var (x, f) = (p * Earth.Radius, forward);
                Earth.Advance(ref x, ref f, s);
                return x.Normalized();
            });
            double rhumb = Math.Abs(p.Z) > 0.97 ? double.NaN : Shore.FirstContactRhumb(p, course, 20_000, r);
            if (!double.IsNaN(rhumb))
                CheckTrack(p, rhumb, r, 0.5, 2.5, s =>
                {
                    Earth.Rhumb(p, course, s, out var x, out _);
                    return x;
                });
        }

        // the track marched every 10 m: nothing that close before the contact, and the contact touching (a rhumb
        // line's within two metres more: its chords are solved a metre wider, and lie up to a metre off it)
        void CheckTrack(Vec3d p, double hit, double r, double tol, double touch, Func<double, Vec3d> at)
        {
            if (Shore.IsLand(p))
            {
                Assert.Equal(0, hit);
                return;
            }
            double end = double.IsPositiveInfinity(hit) ? 20_000 : hit;
            for (double s = 0; s < end - tol; s += 10)
            {
                var x = at(s);
                Assert.False(Shore.IsLand(x), $"from {Earth.LatLon(p)}: land at {s} m before the contact at {hit:F1} m");
                Assert.True(Shore.Clearance(x, r - tol, out _) > r - tol,
                    $"from {Earth.LatLon(p)}: within {r - tol:F0} m at {s} m, before the contact at {hit:F1} m");
            }
            if (!double.IsPositiveInfinity(hit))
                Assert.True(Shore.Clearance(at(hit), r + touch, out _) <= r + touch,
                    $"from {Earth.LatLon(p)}: the contact at {hit:F1} m isn't within {r:F0} m");
        }
    }

    [Fact]
    public void Legs_clear_and_not()
    {
        Assert.True(Shore.ArcClear(Earth.Up(30, -40), Earth.Up(35, -30), 1000));                  // mid-Atlantic
        Assert.True(Shore.ArcClear(Earth.Up(56.7, 5.8), Earth.Up(57.5, 4.0), 1000));              // the North Sea
        Assert.False(Shore.ArcClear(Earth.Up(56.7, 5.8), Earth.Up(60.0, 8.0), 1000));             // into Norway
        Assert.False(Shore.ArcClear(Earth.Up(48.86, 2.35), Earth.Up(48.9, 2.4), 10));             // on land at Paris
        Assert.Equal(0, Shore.FirstContact(Earth.Up(48.86, 2.35), Earth.Forward(Earth.Up(48.86, 2.35), 0), 1000, 10));
        Assert.True(double.IsPositiveInfinity(Shore.FirstContactRhumb(Earth.Up(0, -150), 90, 500_000, 1000)));
    }

    // name, one end, the other, the flood's step (m), and the box round the ends (degrees)
    public static TheoryData<string, double, double, double, double, double, double> Straits => new()
    {
        { "the Bosporus", 40.99, 28.99, 41.24, 29.13, 150, 0.1 },
        { "the Dardanelles", 40.03, 26.17, 40.45, 26.75, 300, 0.1 },
        { "the Øresund", 55.55, 12.75, 56.10, 12.60, 300, 0.1 },
        { "the Great Belt", 55.15, 11.00, 55.80, 11.00, 500, 0.3 },
        { "Dover", 50.80, 1.30, 51.20, 1.70, 1000, 0.1 },
        { "Messina", 38.05, 15.55, 38.30, 15.65, 200, 0.1 },
        { "Hoxa Sound", 58.88, -3.08, 58.74, -3.02, 150, 0.1 },
        { "Tsushima", 34.00, 129.50, 35.00, 130.00, 2000, 0.1 },
        { "Malacca", 2.30, 101.20, 1.15, 103.80, 1000, 0.1 },
    };

    /// <summary>Open at a destroyer's minimum clearance (half its beam and a few tens of metres): a flood over a grid
    /// across the strait, each step a leg <see cref="Shore.ArcClear"/> at 50 m, joins its two ends.</summary>
    [Theory]
    [MemberData(nameof(Straits))]
    public void Straits_are_open(string name, double lat0, double lon0, double lat1, double lon1, double step, double pad)
    {
        const double clearance = 50;
        Assert.False(Shore.IsLand(Earth.Up(lat0, lon0)), $"{name}: one end is on land");
        Assert.False(Shore.IsLand(Earth.Up(lat1, lon1)), $"{name}: the other end is on land");
        double la0 = Math.Min(lat0, lat1) - pad, lo0 = Math.Min(lon0, lon1) - pad;
        double dLat = step / 111_200, dLon = dLat / Math.Cos(Math.Max(Math.Abs(lat0), Math.Abs(lat1)) * Math.PI / 180);
        int ni = (int)((Math.Max(lon0, lon1) + pad - lo0) / dLon) + 1, nj = (int)((Math.Max(lat0, lat1) + pad - la0) / dLat) + 1;
        Vec3d At(int i, int j) => Earth.Up(la0 + j * dLat, lo0 + i * dLon);
        (int, int) Near(double lat, double lon) => ((int)Math.Round((lon - lo0) / dLon), (int)Math.Round((lat - la0) / dLat));
        var (si, sj) = Near(lat0, lon0);
        var goal = Near(lat1, lon1);
        var seen = new bool[ni, nj];
        var queue = new Queue<(int, int)>();
        seen[si, sj] = true;
        queue.Enqueue((si, sj));
        while (queue.Count > 0)
        {
            var (i, j) = queue.Dequeue();
            if ((i, j) == goal)
                return;
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = i + di, b = j + dj;
                if (a < 0 || b < 0 || a >= ni || b >= nj || seen[a, b])
                    continue;
                if (!Shore.ArcClear(At(i, j), At(a, b), clearance))
                    continue;
                seen[a, b] = true;
                queue.Enqueue((a, b));
            }
        }
        Assert.Fail($"{name} is shut at {clearance} m");
    }

    [Fact]
    public void Queries_allocate_nothing()
    {
        var rng = new Random(5);
        var at = NearCoast(rng, 500, 5000).Concat(Uniform(rng, 500)).ToArray();
        double sink = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var p in at)
            {
                sink += Shore.IsLand(p) ? 1 : 0;
                sink += Shore.BoundM(p);
                sink += Math.Min(1e9, Shore.Clearance(p, 5000, out _));
                sink += Math.Min(1e9, Shore.FirstContact(p, Earth.Forward(p, 45), 30_000, 200));
                if (Math.Abs(p.Z) < 0.97)
                    sink += Math.Min(1e9, Shore.FirstContactRhumb(p, 45, 30_000, 200));
                sink += Shore.ArcClear(p, p + Earth.Forward(p, 300) * 0.001, 100) ? 1 : 0;
            }
            if (pass == 1)
                Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.True(sink > 0);
    }

    [Fact]
    public void Builds_the_same_every_time()
    {
        var again = Shore.Build(Land);
        Assert.Equal(Shore.ContentHash(), again.ContentHash());
        Assert.Equal(Land.Hash, Land.Load(Path.Combine(Fleet.Repo, "Content", "World", "land-10m.bin")).Hash);
    }

    static IEnumerable<Vec3d> Uniform(Random rng, int n)
    {
        for (int k = 0; k < n; k++)
        {
            double z = 2 * rng.NextDouble() - 1, lon = 2 * Math.PI * rng.NextDouble(), c = Math.Sqrt(1 - z * z);
            yield return new Vec3d(c * Math.Cos(lon), c * Math.Sin(lon), z);
        }
    }

    /// <summary>A random point of a random ring, moved up to a distance in a random direction.</summary>
    static IEnumerable<Vec3d> NearCoast(Random rng, int n, double metres)
    {
        for (int k = 0; k < n; k++)
        {
            var ring = Land.Ring(rng.Next(Land.RingCount));
            var p = ring[rng.Next(ring.Length)] * Earth.Radius;
            var f = Earth.Forward(p, rng.NextDouble() * 360);
            Earth.Advance(ref p, ref f, rng.NextDouble() * metres);
            yield return p.Normalized();
        }
    }

    /// <summary>Points on and beside the cube's seams and corners, where the cells change faces.</summary>
    static IEnumerable<Vec3d> NearSeams(Random rng, int n)
    {
        for (int k = 0; k < n; k++)
        {
            double edge = rng.Next(2) == 0 ? -1 : 1, off = (rng.NextDouble() - 0.5) * 0.004;
            double along = k % 4 == 0 ? edge * (1 - rng.NextDouble() * 0.004) : 2 * rng.NextDouble() - 1;
            var (x, y) = rng.Next(2) == 0 ? (edge + off, along) : (along, edge + off);
            yield return CubeSphere.Unproject(rng.Next(CubeSphere.FaceCount), x, y);
        }
    }

    /// <summary>The distance to the nearest coast segment by brute force (every segment with an end within reach plus
    /// a degree; none is longer), metres; and its nearest point. Edges on ±180° aren't coast.</summary>
    static double BruteDistance(Vec3d p, double reachM, out Vec3d nearest)
    {
        var q = p.Normalized();
        double cosReach = Math.Cos(Math.Min(Math.PI, reachM / Earth.Radius + Math.PI / 180)), best = double.PositiveInfinity;
        nearest = default;
        for (int r = 0; r < Land.RingCount; r++)
        {
            var (c, cos) = Land.Cap(r);
            if (Earth.Angle(q, c) > Math.Acos(Math.Clamp(cos, -1, 1)) + reachM / Earth.Radius + Math.PI / 180)
                continue;
            var ring = Land.Ring(r);
            for (int k = 0; k < ring.Length; k++)
            {
                Vec3d a = ring[k], b = ring[(k + 1) % ring.Length];
                if (Vec3d.Dot(q, a) < cosReach && Vec3d.Dot(q, b) < cosReach)
                    continue;
                if (a.X < 0 && Math.Abs(a.Y) < 1e-12 && b.X < 0 && Math.Abs(b.Y) < 1e-12)
                    continue;
                double d = ArcAngle(q, a, b, out var at);
                if (d < best)
                {
                    best = d;
                    nearest = at;
                }
            }
        }
        return best * Earth.Radius;
    }

    /// <summary>The angle from q to the arc a-b: to the foot on its circle if that lies between the ends (the angles
    /// a-foot and foot-b add up to a-b), else to the nearer end.</summary>
    static double ArcAngle(Vec3d q, Vec3d a, Vec3d b, out Vec3d at)
    {
        var n = Vec3d.Cross(a, b).Normalized();
        var foot = (q - n * Vec3d.Dot(q, n)).Normalized();
        if (Math.Abs(Earth.Angle(a, foot) + Earth.Angle(foot, b) - Earth.Angle(a, b)) < 1e-12)
        {
            at = foot;
            return Earth.Angle(q, foot);
        }
        double da = Earth.Angle(q, a), db = Earth.Angle(q, b);
        at = da <= db ? a : b;
        return Math.Min(da, db);
    }
}

/// <summary>The 1:10M land and its index, built once for the tests that need them.</summary>
static class TestLand
{
    static readonly Lazy<Shore> shore = new(() => Shore.Build(Land.Load(Path.Combine(Fleet.Repo, "Content", "World", "land-10m.bin"))));

    public static Shore Shore => shore.Value;
}
