using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Spall behind a plate that held (docs/battle/shells-armour-damage.md E3; damage/14 §6.5's tests, wide bands):
/// the cloud's numbers against 14 §3.2, what a light shell's cloud can't harm, backing, the cone, no garbage, and no
/// run of magazines going up from plates that held over them.</summary>
public class SpallTests(ITestOutputHelper output)
{
    static readonly P3 O = new(0, 0, 0), Down = new(0, 0, -1);

    /// <summary>14 §3.2: a 15" Greenboy on 330 mm KC at u 0.90 (V_L 409 m/s), s 0.56: 114 kg, half one piece, ~11 flakes of
    /// 5 kg at ~63 m/s.</summary>
    [Fact]
    public void A_15in_near_its_limit_throws_the_notes_cloud()
    {
        var c = Spall.Stopped(O, Down, 381, 330, 0.90 * 409, 0, 0.56, 0.5);
        output.WriteLine($"{c.MassKg:0} kg, big {c.BigKg:0} kg ({c.BigJ / 1000:0} kJ), {c.Flakes:0.0} x {c.FlakeKg:0.0} kg at {c.AxialMs:0} m/s");
        Assert.InRange(c.MassKg, 100, 130);
        Assert.InRange(c.BigKg, 0.45 * c.MassKg, 0.55 * c.MassKg);
        Assert.InRange(c.Flakes, 9, 14);
        Assert.InRange(c.FlakeKg, 4, 6);
        Assert.InRange(c.AxialMs, 55, 75);
    }

    /// <summary>14 §6.5 test 1: a 6" stopped by 3" of STS throws flakes of ~100 J, under what a pipe or cable needs.</summary>
    [Fact]
    public void A_6in_cloud_is_too_weak_for_pipes_and_cables()
    {
        var c = Spall.Stopped(O, Down, 152, 76, 0.975 * Terminal.LimitMs(76, 152, 45.4, 10), 10, 0.75, 0.5);   // STS: u 0.975 at s 0.75
        output.WriteLine($"{c.Flakes:0} flakes of {c.FlakeKg * 1000:0} g, {c.FlakeJ(0):0} J on the axis; big {c.BigJ:0} J");
        Assert.Equal(0, Spall.Counted(c, 500));
        Assert.True(Spall.Counted(c, 79) > 0, "but over the hazard to a person");
    }

    [Fact]
    public void Most_of_the_cloud_lies_inside_30_degrees()
    {
        Assert.InRange(Spall.ShareInside(double.DegreesToRadians(30)), 0.6, 0.8);
        Assert.InRange(Spall.ShareInside(double.DegreesToRadians(20)), 0.25, 0.45);
        Assert.Equal(1, Spall.ShareInside(double.DegreesToRadians(45)), 6);
    }

    /// <summary>14 §6.5 test 6: backing of the scab's mass per area takes the flakes, twice that the big piece too.</summary>
    [Fact]
    public void Backing_catches_the_cloud()
    {
        var c = Spall.Stopped(O, Down, 381, 330, 370, 0, 0.6, 0.5);
        double scab = Spall.ScabShare * 330;
        var half = Spall.Backed(c, 330, 0.5 * scab);
        Assert.Equal(c.Flakes / 2, half.Flakes, 6);
        var one = Spall.Backed(c, 330, scab);
        Assert.Equal(0, one.Flakes);
        Assert.True(one.BigKg > 0);
        Assert.Equal(0, Spall.Backed(c, 330, 2 * scab).BigKg);
    }

    [Fact]
    public void Only_the_cone_takes_it_and_the_query_allocates_nothing()
    {
        var c = Spall.Stopped(O, Down, 381, 330, 370, 0, 0.6, 0.5);
        var under = new Box(-0.5, 0.5, -0.5, 0.5, -3, -2);
        var aside = new Box(4, 5, -0.5, 0.5, -1, 0);   // 76° off the axis
        Assert.True(Spall.Hits(c, under, 500) > 0.5);
        Assert.Equal(0, Spall.Hits(c, aside, 500));
        double sum = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            sum += Spall.Hits(c, under, 500) + Spall.Counted(c, 500);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    /// <summary>Plunging fire stopped by the decks over the magazines (the user's worry, 2026-10-17): what the sourced rule
    /// gives, measured, with a wide guard only (no anchor yet: the combined results are looked at after phase E).</summary>
    [Fact]
    public void Plunging_fire_stopped_by_the_decks_sets_magazines_off_by_spall()
    {
        var target = Fleet.Get("kgv").Class!;
        var shooter = Fleet.Get("bismarck").Class!;
        var b = shooter.Batteries[0];
        var shell = shooter.Loadout.ShellTypes[shooter.Loadout.Fires(0)];
        var r = GivenHits.Run(target, shell, shooter.Mounts[shooter.Links(b.Mounts)[0]].MaxElevationDeg, 32_000, 90, 3000, 1, 1,
            Fleetwright.Sim.Core.SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")));
        double spall = (double)r.Spalls / r.Hits, bySpall = (double)r.MagazinesBy[(int)MagazineVia.Spall] / r.Hits;
        output.WriteLine($"spall {100 * spall:0.0}% of hits; magazines by spall {100 * bySpall:0.00}%, by all {100.0 * r.Magazines / r.Hits:0.00}%");
        Assert.True(spall > 0.03, "plates hold shells in their band at this range");
        Assert.InRange(bySpall, 0, 0.02);
    }
}
