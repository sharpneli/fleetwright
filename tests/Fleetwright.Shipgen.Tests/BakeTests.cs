using Fleetwright.Shipgen.Golden;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using Fleetwright.Shipgen.Render.Golden;

namespace Fleetwright.Shipgen.Tests;

/// <summary>The GPU bake (PORTING.md Step 5) against Python's PNGs: coverage IoU of the turrets (Python's own clutter
/// and dazzle differ from ours, which barely moves coverage). The hull and height map are drawn from the physical hull
/// since 2026-10-10, no longer Python's look hull, so they aren't compared. Needs a GPU (SDL_GPU, Vulkan); `shipgen
/// png-check` runs every design.</summary>
[Trait("Category", "Gpu")]
[Collection("Gpu")]   // one GPU device at a time
public class BakeTests
{
    static readonly string Root = Paths.Shipgen();

    [Fact]
    public void TurretBakesMatchPythonCoverage()
    {
        // a battleship, a carrier (text, flight deck), a small craft (small turrets) and the widest canvas (tiles)
        string[] names = ["bismarck", "fleet_carrier", "mtb", "gangut"];
        var cases = GoldenCases.Load(Root).Where(c => names.Contains(c.Name)).ToList();
        using var gpu = new GpuBaker();
        foreach (var c in cases)
        {
            var sp = RenderGolden.Draw(c);
            var b = ShipBake.Bake(sp, gpu);
            string g = Paths.Shipgen("golden", "sprite", c.Name);
            foreach (var (tid, im) in b.Turrets)
                Assert.True(ShipBake.CoverageIoU(Png.Load(Path.Combine(g, "turrets", tid + ".png")), im) >= 0.965, $"{c.Name} {tid}");
            var rects = sp.Meta.MipRects;
            var atlas = Mips.Atlas(b.Hull, rects, max: false);
            Assert.Equal(b.Hull.Width * 3 / 2, atlas.Width);
        }
    }

    [Fact]
    public void PngRoundTrips()
    {
        var im = new Image8(6, 4, 4);
        new Random(1).NextBytes(im.Data);
        var path = Path.Combine(Path.GetTempPath(), $"fw_png_{Environment.ProcessId}.png");
        Png.Save(path, im);
        var back = Png.Load(path);
        File.Delete(path);
        Assert.Equal(im.Data, back.Data);
    }

    /// <summary>The height map's mips take the max, so a coarser level never lowers a column.</summary>
    [Fact]
    public void HeightMipsKeepTheMax()
    {
        var im = new Image8(4, 4, 1);
        im.Data[5] = 200;
        var half = Mips.Half(im, max: true);
        Assert.Equal(200, half.Data[0]);
        Assert.Equal(0, half.Data[3]);
    }
}
