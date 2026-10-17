using Fleetwright.Shipgen;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A burst's pieces (docs/battle/shells-armour-damage.md E1) against damage/13's anchors, with its wide bands:
/// the 6" HC's side spray (WDR 51), Okun's 8" HE table, the splinter knee, Boise's largest piece, black powder, a
/// low-order burst, the ring, and the query costing nothing in garbage.</summary>
public class FragmentTests(ITestOutputHelper output)
{
    static Fragments Shell(double cal, double kg, double filler, string explosive, string kind, double size = 1, double speed = 1)
    {
        var x = Explosives.Get(explosive)!;
        var c = ShellTemplates.CasingOf(kind);
        var k = kind switch { "ap" => ShellKind.Ap, "common" => ShellKind.Common, _ => ShellKind.He };
        return Fragments.Of(new FragmentInputs(cal, kg, filler, k, x.GurneyMs!.Value, x.Brisance!.Value, size, speed, c.Nose!.Value,
            c.Ogive!.Value, c.Body!.Value, c.Plug!.Value, c.Tail!.Value, ShellTemplates.FillerBodyOf(kind)))!;
    }

    static readonly Fragments Hc6 = Shell(152, 47.6, 6.0, "Explosive D", "he"), He8 = Shell(203, 109, 7.3, "TNT", "he"),
        Aac5 = Shell(127, 25, 3.3, "Explosive D", "he"), T91 = Shell(203, 125.9, 3.11, "TNA", "ap", 1.25),
        Ap16 = Shell(406, 1225, 18.55, "Explosive D", "ap", 1.25), Hc16 = Shell(406, 862, 69.7, "Explosive D", "he");

    [Fact]
    public void The_6in_HC_throws_its_side_spray_at_WDR_51s_speed()
    {
        double fps = Hc6.High.SideMs / 0.3048;
        output.WriteLine($"6\" HC: {fps:0} ft/s (WDR 51 ~3,200)");
        Assert.InRange(fps, 3200 * 0.9, 3200 * 1.1);
    }

    /// <summary>13 §4.1: the median and the top 1% within ~10% near the burst; the 1% column out to 20 m.</summary>
    [Fact]
    public void Okuns_8in_HE_table_is_regenerated()
    {
        var b = He8.High;
        double At(double cal, double share) => b.DefeatMm(cal * 0.203, share, FragmentClass.Ductile) / 203;
        Assert.InRange(At(20, 0.5) / 0.0213, 0.85, 1.2);
        foreach (var (cal, okun) in new[] { (20, 0.106), (50, 0.100), (100, 0.091) })
            Assert.InRange(At(cal, 0.01) / okun, 0.9, 1.15);
    }

    /// <summary>WDR 51: 25-30 lb STS the knee against 5-8" HE; capital-ship HE needs 1-1.5".</summary>
    [Fact]
    public void Splinter_plating_has_its_knee()
    {
        double Through(Fragments f, double mm) => f.High.ThroughCount(5, f.High.SideMs, Fragments.MildMm(mm, FragmentClass.Ductile)) / f.High.SideCount;
        foreach (var f in new[] { Aac5, Hc6 })
        {
            Assert.InRange(Through(f, 6.1), 0.2, 0.5);    // 10 lb: a third or more come through
            Assert.InRange(Through(f, 15.6), 0, 0.05);    // 25 lb: almost nothing
        }
        Assert.InRange(Through(Hc16, 15.6), 0.2, 0.4);    // a 16" HC's heavy pieces go through 25 lb
        Assert.InRange(Through(Hc16, 38.1), 0, 0.05);     // 1.5" holds most
    }

    [Fact]
    public void AP_throws_few_heavy_slow_pieces_and_HE_many_fast_light_ones()
    {
        Assert.True(Ap16.High.SideCount < Hc16.High.SideCount / 2);
        Assert.True(Ap16.High.SideMs < 0.6 * Hc16.High.SideMs);
        Assert.InRange(Ap16.High.NoseKg, 300, 500);       // the AP nose slug, a third of the casing
        // Boise hit 5: the largest piece recovered "about 2 inches by 4 inches", ~2.4 kg in its wall
        Assert.InRange(T91.High.MassAt(1), 2, 5);
    }

    [Fact]
    public void Black_powder_and_low_order_throw_fewer_slower_larger_pieces()
    {
        var powder = Shell(152, 45.4, 2.7, "Black powder", "common", 0.6, 0.8).High;
        Assert.InRange(powder.SideMs, 120, 260);           // Okun: CPC pieces at up to ~600 ft/s
        Assert.True(Hc6.Low.SideCount < Hc6.High.SideCount / 3);
        Assert.True(Hc6.Low.SideMs < 0.5 * Hc6.High.SideMs);
    }

    /// <summary>A box square to the shell's line in the ring takes many times the fragments of the same box well off it,
    /// and the ring leans forward with the shell's speed.</summary>
    [Fact]
    public void The_ring_and_its_lean()
    {
        var b = Hc6.High;
        var at = new P3(0, 0, 0);
        var dir = new P3(1, 0, 0);
        Box Box(double x, double y) => new(x - 0.5, x + 0.5, y - 0.5, y + 0.5, -0.5, 0.5);
        double ring = b.Hits(at, dir, 0, Box(0, 5), 0), off = b.Hits(at, dir, 0, Box(5 * Math.Tan(Math.PI / 6), 5), 0);
        output.WriteLine($"in the ring {ring:0.0}, 30° off {off:0.0}");
        Assert.True(ring > 5 * off);
        // at 500 m/s the ring leans ~28° forward: the box square to the line is out of it, one 28° ahead is in
        double square = b.Hits(at, dir, 500, Box(0, 5), 0), ahead = b.Hits(at, dir, 500, Box(5 * Math.Tan(double.DegreesToRadians(28)), 5), 0);
        Assert.True(ahead > 3 * square);
        // a plate cuts it: 25 lb lets few through
        Assert.True(b.Hits(at, dir, 0, Box(0, 5), Fragments.MildMm(15.6, FragmentClass.Ductile)) < 0.1 * ring);
    }

    [Fact]
    public void The_query_allocates_nothing()
    {
        var b = Hc16.High;
        var box = new Box(3, 6, -2, 2, 0, 3);
        double sum = b.Hits(new P3(0, 0, 1), new P3(1, 0, 0), 200, box, 6);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            sum += b.Hits(new P3(0, 0, 1 + i % 3), new P3(1, 0, 0), 200, box, 6) + b.LineHits(5, 0.05, b.SideMs);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    /// <summary>The shell rows a class carries have their fragments, and compare equal when made of the same numbers.</summary>
    [Fact]
    public void Shell_types_carry_their_fragments()
    {
        var c = Fleet.Get("dreadnought").Class!;
        foreach (ref readonly var t in c.Loadout.ShellTypes)
            Assert.NotNull(t.Fragments);
        var a = Shell(152, 47.6, 6.0, "Explosive D", "he");
        Assert.Equal(Hc6, a);
        Assert.Equal(Hc6.GetHashCode(), a.GetHashCode());
    }
}
