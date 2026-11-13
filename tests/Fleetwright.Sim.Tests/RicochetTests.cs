using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A shell skipping off the water (docs/battle/shells-armour-damage.md E7's ricochet; research
/// damage-research/16-water-ricochet.md): the graze alone (<see cref="WaterSkip"/>) against the research's figures; then in a
/// world, a shell meeting the sea flatter than <see cref="SimSettings.WaterRicochetDeg"/> that doesn't burst on the graze flies
/// on from where it skipped, turned to its rifling's side, and hits what's beyond it; a rough sea's faces turn many flat falls
/// into plunges; with no skips allowed it stops at the water, as before; and the skips replay exactly.</summary>
public class RicochetTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    // ------------------------------------------------------------------ the graze alone

    [Fact]
    public void Speed_kept_falls_steeply_toward_the_limit()
    {
        // 16 §2.1: 0.95 at 2°, 0.66-0.79 at 5°, about half or less near 7°
        Assert.InRange(WaterSkip.Kept(2, Settings), 0.93, 0.97);
        Assert.InRange(WaterSkip.Kept(5, Settings), 0.66, 0.79);
        Assert.InRange(WaterSkip.Kept(7, Settings), 0.35, 0.55);
    }

    [Fact]
    public void A_pointed_shell_leaves_steeper_than_it_met_the_sea()
    {
        var r = RandomStream.FromSeed(5);
        var exits = new double[4001];
        for (int i = 0; i < exits.Length; i++)
            exits[i] = WaterSkip.ExitDeg(2, Settings, ref r);
        Array.Sort(exits);
        output.WriteLine($"met at 2°: exit 5% {exits[200]:0.0}°, median {exits[2000]:0.0}°, 95% {exits[3800]:0.0}°");
        Assert.InRange(exits[2000], 5.6, 6.4);   // 16 R3: median 5° + ½ × 2°
        Assert.InRange(exits[200], 1.8, 2.7);
        Assert.InRange(exits[3800], 13, 19);
    }

    [Fact]
    public void A_nose_fuze_needs_the_sea_to_meet_it_hard_enough()
    {
        // the RN's 1921 minima: the 4" at 546 m/s fires from 1°49' on the water
        Assert.True(WaterSkip.NoseFires(546, 1.9, Settings));
        Assert.False(WaterSkip.NoseFires(546, 1.5, Settings));
    }

    [Fact]
    public void A_sensitive_base_fuze_starts_on_a_graze_a_robust_one_seldom()
    {
        var ijuin = new ShellTypeRow { CalibreMm = 305, MassKg = 386, Fuze = Fuze.Base, FuzeStartCal = 0.01 };
        var heavy = new ShellTypeRow { CalibreMm = 381, MassKg = 871, Fuze = Fuze.Base, FuzeStartCal = 0.0625 };
        var light = new ShellTypeRow { CalibreMm = 102, MassKg = 14.1, Fuze = Fuze.Base, FuzeStartCal = 0.07 };
        double a = WaterSkip.BaseStarts(ijuin, 600, Settings), b = WaterSkip.BaseStarts(heavy, 500, Settings),
            c = WaterSkip.BaseStarts(light, 500, Settings);
        output.WriteLine($"graze starts: 12\" Ijuin at 600 m/s {a:0.00} ({WaterSkip.GrazeG(ijuin, 600, Settings):0} g), 15\" robust at 500 m/s " +
                         $"{b:0.00} ({WaterSkip.GrazeG(heavy, 500, Settings):0} g), 4\" robust at 500 m/s {c:0.00}");
        Assert.InRange(a, 0.6, 0.95);   // 16 R4: sensitive 0.6-0.9
        Assert.InRange(b, 0.05, 0.3);   // robust heavy AP 0.1-0.3
        Assert.True(c > b, "a small shell's graze is harder on its fuze");
    }

    [Fact]
    public void A_flat_shell_meets_the_faces_turned_to_it()
    {
        // a 2° fall on a sea whose faces have a 5° rms slope along its line: more of the faces turned toward it are met
        var r = RandomStream.FromSeed(9);
        double tan5 = Math.Tan(double.DegreesToRadians(5)), sum = 0;
        int steep = 0, n = 4000;
        for (int i = 0; i < n; i++)
        {
            var (along, _) = WaterSkip.Facet(2, tan5, 0, ref r);
            double meet = 2 + double.RadiansToDegrees(Math.Atan(along));
            Assert.True(meet > 0, "it never meets a back it can't reach");
            sum += along;
            steep += meet >= Settings.WaterRicochetDeg ? 1 : 0;
        }
        output.WriteLine($"2° fall, 5° rms faces: mean face {double.RadiansToDegrees(Math.Atan(sum / n)):0.0}° toward it, " +
                         $"{100.0 * steep / n:0}% met at {Settings.WaterRicochetDeg}° or steeper");
        Assert.True(sum / n > 0.02, "weighted toward the faces turned to it");
        Assert.InRange(steep, n / 20, n / 2);
        var calm = WaterSkip.Facet(2, 0, 0, ref r);
        Assert.Equal((0.0, 0.0), calm);
    }

    // ------------------------------------------------------------------ in the world

    /// <summary>A dreadnought's salvos at a point short of a destroyer 4 km off broadside (east of her), both stopped, the
    /// wind m/s from the north (0: the default sea, a low swell): the hits on her, the skips, the shells fired, the mean turn
    /// of a first skip's heading off due east, degrees (positive: right).</summary>
    static (int Hits, int Skips, int Skipped, int Shells, double TurnDeg, ulong Hash, int Plunged, int Bursts) Salvos(SimSettings s, ShellKind kind, double shortM,
        int salvos = 6, double windMs = 0)
    {
        var w = new World(Start, 11, s with { GunneryErrors = s.GunneryErrors with { Dispersion = 1 } });
        _ = new AiSystem(w);
        if (windMs > 0)
            w.Apply(Command.SetWeather(25_000, 0, windMs));
        int shooter = w.Ships.AddClass(Fleet.Get("dreadnought").Class!), target = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(ShipSystem.Spawn(shooter, 56.5, 4.0, 0, 0));
        double dLon = 4000 / (Earth.Radius * Math.Cos(double.DegreesToRadians(56.5)) * Math.PI / 180);
        w.Apply(ShipSystem.Spawn(target, 56.5, 4.0 + dLon, 0, 0));
        w.AdvanceTo(w.Time + 1000);
        double aimLon = 4.0 + dLon * (4000 - shortM) / 4000, turn = 0;
        int turns = 0;
        var seen = new bool[4096];
        for (int k = 0; k < salvos; k++)
        {
            w.Apply(ShellSystem.FireAt(1, 0, 56.5, aimLon, kind));
            for (int t = 0; t < 30; t++)
            {
                w.AdvanceTo(w.Time + 1000);
                for (int i = 0; i < w.Shells.Slots; i++)
                {
                    ref readonly var sh = ref w.Shells[i];
                    if (sh.Alive && sh.Skips == 1 && i < seen.Length && !seen[i])
                    {
                        seen[i] = true;
                        turn += Earth.Heading(sh.Muzzle, sh.Forward) - 90;
                        turns++;
                    }
                }
            }
            Array.Clear(seen);
        }
        w.AdvanceTo(w.Time + 60_000);
        return (w.Shells.Hits, w.Shells.SkipCount, w.Shells.SkippedShells, w.Shells.Hits + w.Shells.Misses, turns > 0 ? turn / turns : double.NaN,
            w.ComputeHash(), w.Shells.PlungedCount, w.Shells.GrazeBursts);
    }

    [Fact]
    public void A_shell_that_skips_flies_on_turned_right_and_hits_what_is_beyond()
    {
        var flat = Settings with { WaterRicochetSeaK = 0 };
        var on = Salvos(flat, ShellKind.Ap, 100);
        var off = Salvos(flat with { WaterRicochetSkips = 0 }, ShellKind.Ap, 100);
        output.WriteLine($"AP 100 m short of a destroyer at 4 km, a flat sea: skipping {on.Hits} hits of {on.Shells} shells, {on.Skipped} skipped " +
                         $"({on.Skips} skips), turned {on.TurnDeg:0.0}° right; stopping at the water {off.Hits} hits, {off.Skips} skips");
        Assert.True(on.Skipped >= on.Shells / 2, "a flat fall skips");
        Assert.True(on.Hits > off.Hits, "and some hit her beyond");
        Assert.Equal(0, off.Skips);
        Assert.InRange(on.TurnDeg, 2, 8);   // 5° to the rifling's side
    }

    [Fact]
    public void A_rough_sea_turns_flat_falls_into_plunges()
    {
        var flat = Salvos(Settings with { WaterRicochetSeaK = 0 }, ShellKind.Ap, 300);
        var rough = Salvos(Settings, ShellKind.Ap, 300, windMs: 15);
        output.WriteLine($"AP 300 m short, flat sea {flat.Skipped} of {flat.Shells} skipped ({flat.Plunged} plunged, {flat.Bursts} burst on a graze); " +
                         $"a 15 m/s wind's sea {rough.Skipped} of {rough.Shells} ({rough.Plunged} plunged, {rough.Bursts} burst)");
        Assert.True(rough.Skipped < flat.Skipped, "the faces turned to it plunge it");
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
