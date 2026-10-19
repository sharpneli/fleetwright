using System.Numerics;
using Fleetwright.Map.Fires;
using Fleetwright.Map.Flashes;
using Fleetwright.Sim.Core;

namespace Fleetwright.Tests;

/// <summary>Continuous smoke (docs/visuals/hits-and-fires.md I5): a source lays what it made since its last puff, so its
/// strength never depends on the spacing, never dumps after a gap, and its plume keeps rising when its puffs merge.</summary>
public class SmokeTrailsTests
{
    [Fact]
    public void A_source_lays_its_rate_whatever_the_frames_and_no_dump_after_a_gap()
    {
        var book = new SmokeBook();
        var trails = new SmokeTrails();
        var at = Earth.Position(56.5, 4);
        double now = 0;
        for (int f = 0; f < 600; f++, now += 1.0 / 60)
        {
            trails.Begin();
            trails.Emit(book, 1, 1 << 29, now, 2, at, 10, 100, 0, 15, Vector3.One, 4, 30, 25, 0.5f);
        }
        double sum = 0;
        for (int i = 0; i < book.Count; i++)
            sum += book[i].Ap;
        Assert.InRange(sum, 100 * 9, 100 * 11);   // ~10 s at 100 m²/s
        // seen again after a minute away: at most three spacings' worth
        int before = book.Count;
        trails.Begin();
        trails.Emit(book, 1, 1 << 29, now + 60, 2, at, 10, 100, 0, 15, Vector3.One, 4, 30, 25, 0.5f);
        Assert.Equal(before + 1, book.Count);
        Assert.True(book[book.Count - 1].Ap <= 100 * 6 + 1e-3);
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
        book.Update(shots, 5, 0, 0);   // settled and overlapping, still low: they merge
        Assert.Equal(1, book.Count);
        book.State(0, 5, out _, out double z5, out _, out _, out _);
        book.State(0, 80, out _, out double z80, out _, out _, out _);
        Assert.True(z80 - z5 > 25, $"risen {z80 - z5:0.0} m after the merge");
    }
}
