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

    [Theory]
    [InlineData(Dash.Dashed, 37.0)]
    [InlineData(Dash.Dotted, 151.3)]
    public void A_rings_dashes_fit_it_and_stay_put_on_it(Dash dash, double r)
    {
        var lines = new MapLines();
        lines.Begin(W, H);
        lines.Circle(W / 2, H / 2, r, White, 1.5f, dash, cased: true);
        var v = lines.Vertices;
        double around = 2 * Math.PI * r, period = v[0].Style.X;
        Assert.True(period > 0);
        double k = around / period;
        Assert.True(Math.Abs(k - Math.Round(k)) < 1e-3, $"{k:0.0000} dashes round the ring");
        Assert.True(v[0].Edge.W > 0, "cased");

        // cut by the screen's edge, each piece keeps the distance along the ring it had whole: no crawl as it pans
        lines.Begin(W, H);
        lines.Circle(10, H / 2, r, White, 1.5f, dash);
        foreach (var vx in lines.Vertices)
            Assert.InRange(vx.Edge.Z, 0, (float)around + 0.01f);
        Assert.Contains(lines.Vertices.ToArray(), vx => vx.Edge.Z > around / 2);
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

/// <summary>The map's symbols (docs/map-ui/symbols.md Y3): a quad each, none off the screen or invisible, no garbage.</summary>
public class MapSymbolsTests
{
    [Fact]
    public void A_symbol_is_one_quad_and_none_off_the_screen()
    {
        var s = new MapSymbols();
        s.Begin(1920, 1080, 1);
        var c = new Vector4(1, 1, 1, 1);
        s.Add(SymbolShape.Diamond, 100, 100, 5, default, c, 1.5f);
        s.Add(SymbolShape.Arrow, 200, 100, 6, c, c, 1, Math.PI / 3);
        Assert.Equal(12, s.Vertices.Length);
        s.Add(SymbolShape.Circle, -50, 100, 5, c, c, 1);       // off the screen
        s.Add(SymbolShape.Circle, 300, 100, 5, default, default, 1);   // nothing to draw
        Assert.Equal(12, s.Vertices.Length);
    }

    [Fact]
    public void Symbols_allocate_nothing_once_grown()
    {
        var s = new MapSymbols();
        var c = new Vector4(1, 0.8f, 0.3f, 1);
        void Frame()
        {
            s.Begin(1920, 1080, 1);
            for (int i = 0; i < 2000; i++)
                s.Add((SymbolShape)(i % 5), 10 + i % 1900, 10 + i / 4 % 1000, 6, c, c, 1.5f, i);
        }
        Frame();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Frame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
