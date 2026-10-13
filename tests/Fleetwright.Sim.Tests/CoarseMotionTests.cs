using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The coarse tier's motion (docs/battle/plan.md step 2.8b), on a store alone: the accelerations come from the
/// fine model's own trials, a segment laid again part way lays the same track, and turns and routes come out as the
/// fine helmsman's would. The world's side of the tier (when ships are coarse, the step, the handovers) is
/// CoarseTierTests'.</summary>
public class CoarseMotionTests(ITestOutputHelper output)
{
    const double Kn = Handling.MsPerKnot;
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>A store with one ship of a design at sea, coarse, its orders as given.</summary>
    static (ShipStore Store, int Slot) Coarse(string design, double kn, double headingDeg = 0)
    {
        var h = new Handling(Fleet.Get(design).Class!, Settings);
        var store = new ShipStore(Settings);
        var p = Earth.Position(56.5, 4.0);
        int i = store.Spawn(1, 0, h, p, Earth.Forward(p, headingDeg), kn * Kn, 0);
        store.NowMs = 0;
        store.Coarsen(i);
        CoarseMotion.Plan(store, i, Settings);
        return (store, i);
    }

    /// <summary>Runs a coarse ship's segments on to a time, laying each next one at its end as the world's events do,
    /// and every <paramref name="relayMs"/> besides (0: never), as an order or the coarse step would.</summary>
    static void Run(ShipStore s, int i, long untilMs, long relayMs = 0)
    {
        long nextRelay = relayMs > 0 ? s.NowMs + relayMs : long.MaxValue;
        while (true)
        {
            long end = s.SegmentEndMs(i), at = Math.Min(Math.Min(end, nextRelay), untilMs);
            s.NowMs = at;
            if (at == untilMs)
                return;
            if (at == nextRelay)
                nextRelay += relayMs;
            s.ReanchorCoarse(i);
            CoarseMotion.Plan(s, i, Settings);
        }
    }

    [Theory]
    [InlineData("bismarck")]
    [InlineData("destroyer")]
    public void The_accelerations_are_the_fine_models_own_at_its_tick(string design)
    {
        var cls = Fleet.Get(design).Class!;
        var coarse = new Handling(cls, Settings);
        var fine = new Handling(cls, Settings with { CoarseCalibrationStepS = Settings.ShipTickMs / 1000.0 });
        double top = coarse.SteadySpeedMs(coarse.PowerFor(cls.Motion.MaxSpeedKn));
        output.WriteLine($"{design}: top {top / Kn:F1} kn; 0 -> 90% {0.9 * top / coarse.CoarseAccelUp:F0} s, " +
                         $"to half stopped {0.5 * top / coarse.CoarseAccelDown:F0} s, to a stop astern {top / coarse.CoarseAccelAstern:F0} s");
        Assert.Equal(fine.CoarseAccelUp, coarse.CoarseAccelUp, fine.CoarseAccelUp * 0.03);
        Assert.Equal(fine.CoarseAccelDown, coarse.CoarseAccelDown, fine.CoarseAccelDown * 0.03);
        Assert.Equal(fine.CoarseAccelAstern, coarse.CoarseAccelAstern, fine.CoarseAccelAstern * 0.03);
    }

    [Fact]
    public void A_segment_laid_again_part_way_lays_the_same_track()
    {
        // a turn of 135 degrees while slowing, then a route of three legs: laid only at their ends, or every 7 s too
        var (a, i) = Coarse("dreadnought", 20);
        var (b, j) = Coarse("dreadnought", 20);
        foreach (var (s, k) in new[] { (a, i), (b, j) })
        {
            s.OrderCourse(k, 135);
            s.OrderSpeed(k, 12);
            s.ReanchorCoarse(k);
            CoarseMotion.Plan(s, k, Settings);
        }
        Run(a, i, 600_000);
        Run(b, j, 600_000, 7_000);
        a.PoseNow(i, out var pa, out var fa);
        b.PoseNow(j, out var pb, out var fb);
        output.WriteLine($"after the turn: {Earth.Distance(pa, pb) * 1000:F3} mm apart, heading {a[i].HeadingDeg:F4}");
        Assert.True(Earth.Distance(pa, pb) < 0.01);
        Assert.Equal(135, a[i].HeadingDeg, 0.1);
        Assert.Equal(12 * Kn, a[i].Speed, 1e-6);

        foreach (var (s, k) in new[] { (a, i), (b, j) })
        {
            s.PoseNow(k, out var p, out _);
            foreach (double brg in new[] { 0.0, 90, 200 })
                s.AddWaypoint(k, Ahead(p, brg, 8_000, out p));
            s.ReanchorCoarse(k);
            CoarseMotion.Plan(s, k, Settings);
        }
        Run(a, i, 6_000_000);
        Run(b, j, 6_000_000, 7_000);
        a.PoseNow(i, out pa, out _);
        b.PoseNow(j, out pb, out _);
        output.WriteLine($"after the route: {Earth.Distance(pa, pb) * 1000:F3} mm apart, helm {a[i].Helm}");
        Assert.True(Earth.Distance(pa, pb) < 0.01);
        Assert.Equal(Helm.Course, a[i].Helm);
    }

    /// <summary>A point a distance along a bearing from another.</summary>
    static Vec3d Ahead(Vec3d from, double bearingDeg, double metres, out Vec3d to)
    {
        var f = Earth.Forward(from, bearingDeg);
        to = from;
        Earth.Advance(ref to, ref f, metres);
        return to;
    }

    [Fact]
    public void A_route_passes_its_waypoints_and_holds_the_last_course()
    {
        var (s, i) = Coarse("destroyer", 25);
        s.PoseNow(i, out var p, out _);
        var wps = new List<Vec3d>();
        foreach (double brg in new[] { 45.0, 135, 300 })
            wps.Add(s.AddWaypoint(i, Ahead(p, brg, 5_000, out p)) ? p : default);
        s.ReanchorCoarse(i);
        CoarseMotion.Plan(s, i, Settings);
        double radius = s[i].Handling.RadiusFor(s[i].CourseRudder);
        var nearest = new double[wps.Count];
        Array.Fill(nearest, double.MaxValue);
        for (long t = 0; t <= 1_800_000; t += 1_000)
        {
            Run(s, i, t);
            var at = s[i].Position;
            for (int k = 0; k < wps.Count; k++)
                nearest[k] = Math.Min(nearest[k], Earth.Distance(at, wps[k]));
        }
        output.WriteLine($"radius {radius:F0} m; nearest {string.Join(", ", nearest.Select(d => $"{d:F0} m"))}");
        for (int k = 0; k < wps.Count - 1; k++)
            Assert.True(nearest[k] < radius, $"waypoint {k} passed {nearest[k]:F0} m off");   // cut by the wheel-over, as the helmsman does
        Assert.True(nearest[^1] <= Settings.ArrivalRadiusM + 1);
        Assert.Equal(Helm.Course, s[i].Helm);
        output.WriteLine($"last heading {s[i].HeadingDeg:F2}");
    }

    [Fact]
    public void A_coarse_turn_is_the_fine_turns_circle()
    {
        // the standard rudder's tactical diameter coarse (the steady circle, no swing to build) and fine
        var (s, i) = Coarse("dreadnought", 20);
        s.OrderCourse(i, 180);
        s.ReanchorCoarse(i);
        CoarseMotion.Plan(s, i, Settings);
        var p0 = s[i].Position;
        var frame = s[i].Frame;
        Run(s, i, 400_000);
        double coarseTd = frame.ToLocal(s[i].Position).Y;

        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 7, Settings);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(ShipSystem.Spawn(c, 56.5, 4.0, 0, 20));
        w.Apply(ShipSystem.Course(1, 180));
        var fineFrame = w.Ships.Store[0].Frame;
        double fineTd = 0;
        for (int k = 0; k < 4000 && fineTd == 0; k++)
        {
            w.AdvanceTo(w.Time + 100);
            if (w.Ships.Store[0].HeadingDeg is > 179 and < 181)
                fineTd = fineFrame.ToLocal(w.Ships.Store[0].Position).Y;
        }
        output.WriteLine($"tactical diameter at the standard rudder: coarse {coarseTd:F0} m, fine {fineTd:F0} m");
        Assert.True(fineTd > 0);
        Assert.Equal(fineTd, coarseTd, 0.25 * fineTd);
        _ = p0;
    }
}
