using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;

namespace Fleetwright.Shipgen.Tests;

/// <summary>The lighting's surface (docs/visuals/ship-lighting.md L2): normals and AO from the relief, on the CPU; and
/// on the GPU, the relief layer from plateaus, which paint leaves alone.</summary>
public class ReliefTests
{
    const double PxPerM = 10;

    /// <summary>A 60 x 60 px canvas (6 m), all covered, with a block 3 m tall over x, y in [25, 35).</summary>
    static (ReliefImage Relief, Image8 Coverage) Block()
    {
        var r = new ReliefImage(60, 60);
        var cov = new Image8(60, 60, 4);
        for (int y = 0; y < 60; y++)
            for (int x = 0; x < 60; x++)
            {
                cov.Data[(y * 60 + x) * 4 + 3] = 255;
                if (x is >= 25 and < 35 && y is >= 25 and < 35)
                    r.M[y * 60 + x] = 3;
            }
        return (r, cov);
    }

    static (double X, double Y, double Z, double Ao) At(Image8 n, int x, int y)
    {
        int i = (y * n.Width + x) * 4;
        return (n.Data[i] / 255.0 * 2 - 1, n.Data[i + 1] / 255.0 * 2 - 1, n.Data[i + 2] / 255.0 * 2 - 1, n.Data[i + 3] / 255.0);
    }

    [Fact]
    public void A_block_is_flat_on_top_and_its_walls_face_out_no_steeper_than_the_clamp()
    {
        var (r, cov) = Block();
        var n = ReliefMaps.NormalAo(r, PxPerM, cov);
        var top = At(n, 30, 30);
        Assert.True(top.Z > 0.99, $"top {top}");
        var west = At(n, 25, 30);   // the wall facing -x (aft)
        var south = At(n, 30, 34);  // the wall facing +y (starboard)
        Assert.True(west.X < -0.5 && Math.Abs(west.Y) < 0.05, $"west wall {west}");
        Assert.True(south.Y > 0.5 && Math.Abs(south.X) < 0.05, $"south wall {south}");
        double minZ = 1 / Math.Sqrt(1 + ReliefMaps.MaxSlope * ReliefMaps.MaxSlope);
        Assert.True(west.Z >= minZ - 0.02, $"steeper than the clamp: {west.Z} < {minZ}");
        var far = At(n, 3, 3);
        Assert.True(far.Z > 0.99);
    }

    [Fact]
    public void The_deck_darkens_at_the_walls_foot_and_not_far_from_it()
    {
        var (r, cov) = Block();
        var n = ReliefMaps.NormalAo(r, PxPerM, cov);
        double foot = At(n, 22, 30).Ao, far = At(n, 3, 3).Ao, top = At(n, 30, 30).Ao;   // far: 3.1 m off, past the reach
        Assert.True(foot < 0.85, $"foot {foot}");
        Assert.True(far > 0.98, $"far {far}");
        Assert.True(top > 0.98, $"top {top}");
    }

    [Fact]
    public void Empty_pixels_face_up_and_the_mips_weigh_by_coverage()
    {
        var (r, cov) = Block();
        for (int y = 0; y < 60; y++)   // only the left half covered
            for (int x = 30; x < 60; x++)
                cov.Data[(y * 60 + x) * 4 + 3] = 0;
        var n = ReliefMaps.NormalAo(r, PxPerM, cov);
        int e = (45 * 60 + 45) * 4;
        Assert.Equal((128, 128, 255, 255), (n.Data[e], n.Data[e + 1], n.Data[e + 2], n.Data[e + 3]));
        // level 1 at the west wall's column: the covered pixel's tilt survives next to an empty neighbour
        var colour = new List<Image8> { cov, Mips.Half(cov, max: false) };
        var chain = ReliefMaps.Chain(n, colour);
        Assert.Equal(2, chain.Length);
        var wall = At(chain[1], 12, 15);   // pixels 24-25 x 30-31: the wall's foot and face, both covered
        Assert.True(wall.X < -0.2, $"{wall}");
        var edge = At(chain[1], 15, 10);   // pixels 30-31: empty, so straight up
        Assert.True(edge.Z > 0.99, $"{edge}");
    }

}

/// <summary>The relief layer on the GPU: plateaus write it, paint leaves it alone, later draws win.</summary>
[Trait("Category", "Gpu")]
[Collection("Gpu")]   // one GPU device at a time
public class ReliefBakeTests
{
    const double PxPerM = 10;

    [Fact]
    public void Plateaus_write_the_relief_and_paint_leaves_it_alone()
    {
        var sc = new Scene(0, 0, 4, 4, PxPerM);
        sc.Root.Items.Add(new RectNode(0, 0, 2, 4).Fill("#808080").Plateau(3));
        sc.Root.Items.Add(new RectNode(1, 1, 2, 2).Fill("#ff0000"));              // paint: no profile
        sc.Root.Items.Add(new RectNode(3, 0, 1, 1).Fill("#808080").Plateau(1.5));
        sc.Root.Items.Add(new RectNode(3, 0, 1, 1).Fill("#808080").Plateau(0.5));  // later wins
        using var gpu = new GpuBaker();
        var r = gpu.RenderRelief(Lower.Run(sc));
        float H(double xm, double ym) => r.M[(int)(ym * PxPerM) * r.Width + (int)(xm * PxPerM)];
        Assert.Equal(3f, H(1.5, 1.5));   // under the paint
        Assert.Equal(3f, H(0.5, 3.5));
        Assert.Equal(0f, H(2.5, 3.5));   // nothing with a profile
        Assert.Equal(0.5f, H(3.5, 0.5));
    }
}
