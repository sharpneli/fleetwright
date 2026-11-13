using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A shell skipping off the water (docs/battle/shells-armour-damage.md E7's ricochet): one falling flatter than
/// <see cref="SimSettings.WaterRicochetDeg"/> that doesn't burst on the graze flies on from where it skipped, and hits what's
/// beyond it; a nose-fuzed shell bursts on the graze instead (but for its fuze's failures); with no skips allowed it stops at
/// the water, as before; and the skips replay exactly.</summary>
public class RicochetTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>A dreadnought's salvos at a point short of a destroyer 4 km off broadside, both stopped: the hits on her, the
    /// skips, the shells fired.</summary>
    static (int Hits, int Skips, int Skipped, int Shells, ulong Hash) Salvos(SimSettings s, ShellKind kind, double shortM, int salvos = 6)
    {
        var w = new World(Start, 11, s with { GunneryErrors = s.GunneryErrors with { Dispersion = 1 } });
        _ = new AiSystem(w);
        int shooter = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), target = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(ShipSystem.Spawn(shooter, 56.5, 4.0, 0, 0));
        double dLon = 4000 / (Earth.Radius * Math.Cos(double.DegreesToRadians(56.5)) * Math.PI / 180);
        w.Apply(ShipSystem.Spawn(target, 56.5, 4.0 + dLon, 0, 0));
        w.AdvanceTo(w.Time + 1000);
        double aimLon = 4.0 + dLon * (4000 - shortM) / 4000;
        for (int k = 0; k < salvos; k++)
        {
            w.Apply(ShellSystem.FireAt(1, 0, 56.5, aimLon, kind));
            w.AdvanceTo(w.Time + 30_000);
        }
        w.AdvanceTo(w.Time + 60_000);
        return (w.Shells.Hits, w.Shells.SkipCount, w.Shells.SkippedShells, w.Shells.Hits + w.Shells.Misses, w.ComputeHash());
    }

    [Fact]
    public void A_shell_that_skips_flies_on_and_hits_what_is_beyond()
    {
        var on = Salvos(Settings, ShellKind.Ap, 300);
        var off = Salvos(Settings with { WaterRicochetSkips = 0 }, ShellKind.Ap, 300);
        var straight = Salvos(Settings with { WaterRicochetScatterDeg = 0 }, ShellKind.Ap, 300);
        output.WriteLine($"AP 300 m short of a destroyer at 4 km: skipping {on.Hits} hits of {on.Shells} shells, {on.Skipped} skipped ({on.Skips} skips); " +
                         $"no scatter {straight.Hits} hits; stopping at the water {off.Hits} hits, {off.Skips} skips");
        Assert.True(on.Skipped >= on.Shells / 2, "a flat fall skips");
        Assert.True(on.Hits > 0, "and some hit her beyond");
        Assert.Equal(0, off.Skips);
        Assert.True(off.Hits < on.Hits, "stopped at the water, fewer reach her");
    }

    [Fact]
    public void A_nose_fuze_bursts_on_the_graze()
    {
        var he = Salvos(Settings, ShellKind.He, 300);
        output.WriteLine($"HE 300 m short: {he.Skipped} of {he.Shells} shells skipped ({he.Skips} skips), {he.Hits} hits");
        Assert.True(he.Skipped <= Math.Max(2, he.Shells / 8), "only its fuze's failures skip");
    }

    [Fact]
    public void Skips_replay_exactly()
    {
        var a = Salvos(Settings, ShellKind.Ap, 300, 3);
        var b = Salvos(Settings, ShellKind.Ap, 300, 3);
        Assert.Equal(a.Hash, b.Hash);
        Assert.Equal(a.Skips, b.Skips);
    }
}
