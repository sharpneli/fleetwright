using System.Numerics;
using Fleetwright.Map.Fires;
using Fleetwright.Map.Flashes;
using Fleetwright.Sim.Core;

namespace Fleetwright.Tests;

/// <summary>Continuous smoke (docs/visuals/hits-and-fires.md I5): a source lays what it made over each spacing, so its
/// strength never depends on the spacing; on a lattice of world times, each puff where the source was then, so its trail
/// is the same whatever the frame rate or the time compression (the user, 2026-10-21); and its plume keeps rising when
/// its puffs merge.</summary>
public class SmokeTrailsTests
{
    static readonly Vec3d At = Earth.Position(56.5, 4);

    /// <summary>A source steaming east at 10 m/s for <paramref name="seconds"/> of world time, a frame every
    /// <paramref name="frameS"/> of it: the book it leaves.</summary>
    static SmokeBook Steam(double seconds, double frameS, double fadeS = 0)
    {
        var book = new SmokeBook();
        var trails = new SmokeTrails();
        var v = Earth.East(At) * 10;
        for (int f = 0; f * frameS <= seconds + 1e-9; f++)
        {
            double now = f * frameS;
            trails.Begin();
            trails.Emit(book, 1, 1 << 29, now, 1.2, At + v * now, v, 10, 100, 0, 15, Vector3.One, 4, 30, 25, 0.5f, fadeS: fadeS);
        }
        return book;
    }

    [Fact]
    public void A_source_lays_its_rate_whatever_the_frames()
    {
        foreach (double frame in new[] { 1.0 / 60, 0.5, 3.6 })
        {
            var book = Steam(36, frame);
            double sum = 0;
            for (int i = 0; i < book.Count; i++)
                sum += book[i].Ap;
            Assert.InRange(sum, 100 * 35, 100 * 37.3);   // ~36 s at 100 m²/s
        }
    }

    [Fact]
    public void The_trail_is_the_same_at_any_time_compression()
    {
        // a frame of 1/60 s of world time (1x) against frames of 3.6 s (216x at 60 fps): the same puffs, born at the
        // same times in the same places
        var slow = Steam(36, 1.0 / 60);
        var fast = Steam(36, 3.6);
        Assert.Equal(slow.Count, fast.Count);
        for (int i = 0; i < slow.Count; i++)
        {
            Assert.Equal(slow[i].Born, fast[i].Born, 6);
            Assert.True((slow[i].Origin - fast[i].Origin).Length < 0.01, $"puff {i} {(slow[i].Origin - fast[i].Origin).Length:0.00} m apart");
            Assert.True(fast[i].World);
        }
    }

    [Fact]
    public void A_frame_lays_no_more_than_will_show()
    {
        // a frame of five minutes: a fading plume lays only what's still alive, one that doesn't fade its newest
        Assert.InRange(Steam(300, 300, fadeS: 8).Count, 1, (int)Math.Ceiling(8 / 1.2) + 2);
        Assert.InRange(Steam(300, 300).Count, 1, SmokeTrails.MaxBackfill + 1);
    }

    [Fact]
    public void A_moving_source_lays_its_puffs_by_the_distance_it_runs()
    {
        Assert.Equal(1.2, SmokeTrails.Spacing(10, 4), 9);    // 20 kn: every 12 m
        Assert.Equal(4, SmokeTrails.Spacing(0, 4), 9);       // stopped: the source's own spacing
        Assert.Equal(0.5, SmokeTrails.Spacing(40, 4), 9);    // never faster than twice a second
    }

    [Fact]
    public void Merged_puffs_keep_rising()
    {
        var book = new SmokeBook();
        var at = Earth.Position(56.5, 4);
        var shots = new ShotBook();
        for (int i = 0; i < 2; i++)
            book.Add(new SmokeBook.Puff
            {
                Born = i * 0.5, Origin = at, Z0 = 10, JetZ = 40, JetS = 25, Anchor = 10, Lambda = 4, Ap = 500, FogS = 1, Tint = Vector3.One,
                Shooter = 1 << 29, Keep = true,
            });
        for (int f = 0; f < 3; f++)
            book.Update(shots, 5, 0, 0, 0);   // settled and overlapping, still low: they merge (every third frame)
        Assert.Equal(1, book.Count);
        book.State(0, out _, out double z5, out _, out _, out _);
        book.Update(shots, 80, 0, 0, 0);
        book.State(0, out _, out double z80, out _, out _, out _);
        Assert.True(z80 - z5 > 25, $"risen {z80 - z5:0.0} m after the merge");
    }
}
