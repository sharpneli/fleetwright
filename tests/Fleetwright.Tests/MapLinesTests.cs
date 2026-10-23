using Fleetwright.Map;
using System.Numerics;

namespace Fleetwright.Tests;

/// <summary>The map's curves (docs/map-ui/symbols.md Y1): as smooth as their size on the screen needs, never more
/// than the screen shows, and a still frame's rebuild makes no garbage.</summary>
public class MapLinesTests
{
    const double W = 1920, H = 1080;
    static readonly Vector4 White = new(1, 1, 1, 1);

    [Theory]
    [InlineData(4)]
    [InlineData(40)]
    [InlineData(400)]
    [InlineData(4000)]
    public void A_ring_stays_within_its_chord_error(double r)
    {
        var lines = new MapLines();
        lines.Begin(W, H);
        int n = lines.Segments(r);
        double sagitta = r * (1 - Math.Cos(Math.PI / n));
        Assert.True(sagitta <= SymbolLook.Chord + 1e-9 || n == 8, $"r {r}: {n} pieces, {sagitta:0.000} px off");
        Assert.True(r * (1 - Math.Cos(Math.PI / (n - 1))) > SymbolLook.Chord || n == 8, $"r {r}: {n} pieces is more than it needs");
    }

    [Fact]
    public void Only_a_rings_arc_on_the_screen_is_built()
    {
        var lines = new MapLines();
        lines.Begin(W, H);
        lines.Circle(W / 2, H / 2, 300, White, 1);
        int whole = lines.Vertices.Length;
        Assert.True(whole > 0);

        // a ring 20,000 px across round a point off the left edge: only the arc across the screen, a few hundred pieces
        lines.Begin(W, H);
        lines.Circle(-10_000 + 500, H / 2, 10_000, White, 1);
        int arc = lines.Vertices.Length;
        Assert.InRange(arc, 6, lines.Segments(10_000) * 6 / 4);

        // off the screen, or round the whole of it: nothing
        lines.Begin(W, H);
        lines.Circle(-500, -500, 100, White, 1);
        lines.Circle(W / 2, H / 2, 50_000, White, 1);
        Assert.Equal(0, lines.Vertices.Length);
    }

    [Fact]
    public void Rings_allocate_nothing_once_grown()
    {
        var lines = new MapLines();
        void Frame()
        {
            lines.Begin(W, H);
            for (int i = 0; i < 200; i++)
                lines.Circle(100 + i * 8, 500, 20 + i * 40, White, 1.5f);
        }
        Frame();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Frame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
