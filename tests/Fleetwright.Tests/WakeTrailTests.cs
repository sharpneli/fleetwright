using Fleetwright.Map;
using Fleetwright.Map.Wakes;
using Fleetwright.Sim.Core;

namespace Fleetwright.Tests;

/// <summary>The ships' trails (docs/visuals/wakes.md K4): a point every 10 s and on every 3° of turn, ten minutes
/// kept, the order kept through growing; the foam lane none for a ship at rest and white behind one under way.</summary>
public class WakeTrailTests
{
    static readonly Vec3d Start = Earth.Up(56.5, 4.0) * Earth.Radius;

    // a ship steaming from Start, heading east, turning at rate (rad/s), each second for seconds
    static (WakeTrail Trail, Vec3d P, Vec3d F) Steam(double speed, double turnRadPerS, int seconds, WakeTrail? t = null)
    {
        t ??= new WakeTrail { SternX = -50, Beam = 10, Known = true };
        var p = Start;
        var f = Earth.East(Start.Normalized());
        for (int s = 0; s <= seconds; s++)
        {
            t.Sample(p, f, speed, s * 1000L);
            var up = p.Normalized();
            var side = Vec3d.Cross(f, up);
            f = (f * Math.Cos(turnRadPerS) + side * Math.Sin(turnRadPerS)).Normalized();
            p += f * speed;
            p = p.Normalized() * Earth.Radius;
        }
        return (t, p, f);
    }

    [Fact]
    public void StraightIsAPointEveryTenSecondsATurnEveryThreeDegrees()
    {
        var (straight, _, _) = Steam(8, 0, 120);
        Assert.Equal(13, straight.Count);   // 0, 10, ... 120 s
        var (turning, _, _) = Steam(8, 2 * Math.PI / 180, 120);   // 2°/s: a point every 2 s, the turn's
        Assert.InRange(turning.Count, 55, 65);
    }

    [Fact]
    public void TenMinutesAreKeptInOrder()
    {
        var (t, _, _) = Steam(8, 1 * Math.PI / 180, 1500);   // 25 minutes, turning: past the first growths and wraps
        Assert.True(t[0].Ms >= 1500_000 - WakeTrail.KeepMs, "nothing older than ten minutes");
        for (int i = 1; i < t.Count; i++)
        {
            Assert.True(t[i].Ms > t[i - 1].Ms, $"point {i} in order");
            Assert.True(t[i].S > t[i - 1].S, $"point {i} further on");
        }
        Assert.InRange(t.Count, 2, WakeTrail.MaxPoints);
    }

    [Fact]
    public void NoFoamAtRestWhiteUnderWay()
    {
        var view = new MapView(MapCamera.At(56.5, 4.01, 1), 1920, 1080);
        var foam = new FoamTrails();

        var (still, p0, f0) = Steam(0, 0, 60);
        foam.Begin();
        foam.Add(view, still, still.Head(p0, f0, 0, 60_000), 60_000, 1);
        Assert.Equal(0, foam.Vertices.Length);

        var (going, p1, f1) = Steam(10, 0, 60);
        foam.Begin();
        foam.Add(view, going, going.Head(p1, f1, 10, 60_000), 60_000, 1);
        Assert.True(foam.Vertices.Length >= 6 && foam.Vertices.Length % 6 == 0);
        float most = 0;
        foreach (var v in foam.Vertices)
            most = Math.Max(most, v.Intensity);
        Assert.InRange(most, 0.7, 1.3);   // white near the ship (1.0 at its peak)
    }

    [Fact]
    public void TheFoamByTheShipHoldsWhenAPointIsLaidAtTheStern()
    {
        // 60 s at 10 m/s: the trail's newest point (60 s) is where the stern is now, as it is every 10 s of game time
        var view = new MapView(MapCamera.At(56.5, 4.01, 1), 1920, 1080);
        var (t, _, _) = Steam(10, 0, 60);
        var head = t.Newest;   // the stern now, a point just laid there
        var foam = new FoamTrails();
        foam.Add(view, t, head, 60_000, 1);
        // the ramp-in ends a beam behind the stern, at full foam: no segment stretched back to the point before
        float atRamp = 0;
        foreach (var v in foam.Vertices)
            if (Math.Abs(v.Lane.X - (head.S - t.Beam)) < 0.5)
                atRamp = Math.Max(atRamp, v.Intensity);
        Assert.InRange(atRamp, 0.7, 1.3);
    }
}
