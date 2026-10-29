using Fleetwright.Map.Flashes;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Tests;

/// <summary>The world view's muzzle flashes (docs/visuals/muzzle-flashes.md F2): the photometry against the research's
/// table, the halo's closed-form integral, the parts' frame average keeping a short flash's energy, and the book taking
/// each mount fired once, deciding each shot's own draw once, and allocating nothing a frame.</summary>
public class FlashTests(ITestOutputHelper output)
{
    static readonly ShipClass Dreadnought = ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(Paths.Shipgen("designs", "dreadnought.json"))));

    [Fact]
    public void A_blackbody_is_as_bright_as_the_research_table_and_redder_as_it_cools()
    {
        Assert.Equal(141, FlashPhotometry.Luminance(1200), 1);
        Assert.Equal(1.18e5, FlashPhotometry.Luminance(1800), 0);
        double l1700 = FlashPhotometry.Luminance(1700);
        Assert.InRange(l1700, 2.15e4, 1.18e5);
        Assert.True(FlashPhotometry.Luminance(900) < 2.73);
        Assert.True(FlashPhotometry.Chroma(1200).Y < FlashPhotometry.Chroma(2000).Y);
        Assert.Equal(1, FlashPhotometry.Lum(FlashPhotometry.UnitLuminance(FlashPhotometry.Chroma(1800))), 4);
    }

    [Fact]
    public void Noon_is_a_hundred_million_times_brighter_than_a_starlit_night()
    {
        double noon = FlashPhotometry.Illuminance(Math.Sin(55 * Math.PI / 180), 0, 0);
        double dusk = FlashPhotometry.Illuminance(Math.Sin(-4 * Math.PI / 180), 0, 0);
        double night = FlashPhotometry.Illuminance(-0.5, 0, 0);
        double moon = FlashPhotometry.Illuminance(-0.5, 1, 1);
        output.WriteLine($"noon {noon:F0} lx, dusk {dusk:F2}, starlight {night:F4}, full moon overhead {moon:F3}");
        Assert.InRange(noon, 80_000, 110_000);
        Assert.InRange(night, 1e-3, 2e-3);
        Assert.InRange(moon, 0.25, 0.26);
        Assert.True(noon > dusk && dusk > moon && moon > night);
    }

    /// <summary>The halo's source: x/(1 + x/knee) integrated over a Gaussian, against a numeric integral.</summary>
    [Theory]
    [InlineData(0.01)]
    [InlineData(3)]
    [InlineData(300)]
    [InlineData(1e6)]
    public void The_compressed_integral_is_closed_form(double q)
    {
        double sa = 2, sb = 1, knee = 6, sum = 0, h = 0.02;
        for (double x = -12; x <= 12; x += h)
            for (double y = -6; y <= 6; y += h)
            {
                double v = q * Math.Exp(-0.5 * (x * x / (sa * sa) + y * y / (sb * sb))) / (2 * Math.PI * sa * sb);
                sum += v / (1 + v / knee) * h * h;
            }
        double closed = FlashRenderer.Compressed(q, sa * sb, knee);
        output.WriteLine($"q {q}: closed {closed:G5}, numeric {sum:G5}");
        Assert.Equal(sum, closed, sum * 1e-3);
        Assert.True(closed <= q * (1 + 1e-9));
    }

    static ShotsView Shots(params (long Ms, int Mount)[] oldestFirst)
    {
        var v = new ShotsView();
        v.EnsureCapacity(oldestFirst.Length);
        var at = Earth.Position(56.5, 4.0);
        for (int i = 0; i < oldestFirst.Length; i++)
        {
            var (ms, mount) = oldestFirst[oldestFirst.Length - 1 - i];
            (v.Ms[i], v.Shooter[i], v.Class[i], v.Mount[i], v.Salvo[i], v.Barrels[i]) = (ms, 1, 0, mount, (int)(ms / 1000), 2);
            (v.Muzzle[i], v.Forward[i], v.ElevationDeg[i]) = (at, Earth.Forward(at, 90), 10);
        }
        v.Count = oldestFirst.Length;
        return v;
    }

    static ShipClass? ClassOf(int id) => id == 0 ? Dreadnought : null;

    [Fact]
    public void Each_mount_fired_is_taken_once_with_its_own_draw()
    {
        var book = new ShotBook();
        book.Update(Shots((1000, 0), (1000, 1), (2000, 0)), 2000, 1, 0.016, ClassOf);
        Assert.Equal(3, book.Count);
        book.Update(Shots((1000, 0), (1000, 1), (2000, 0), (2000, 1), (3000, 2)), 3000, 1, 0.016, ClassOf);
        Assert.Equal(5, book.Count);
        book.Update(Shots((2000, 0), (2000, 1), (3000, 2)), 3000, 1, 0.016, ClassOf);
        Assert.Equal(5, book.Count);

        var gun = MuzzleFlash.Of(Dreadnought, 0);
        ref readonly var e = ref book[0];
        output.WriteLine($"a 12\" twin: λ_f {e.FlashLengthM:F1} m, t_s {e.SecondaryS * 1000:F0} ms, x_M {e.MachDiskM:F1} m, scatter {e.Scatter:F2}");
        Assert.Equal(gun.MachDiskM, e.MachDiskM, 3);
        Assert.InRange(e.SecondaryS, 0.2, 0.5);
        // the tip ahead of the mount's axis by the barrels' reach, on the bore
        double tip = Earth.Distance(book[0].Tip, Earth.Position(56.5, 4.0));
        Assert.Equal(Dreadnought.TurretTypes[Dreadnought.Mounts[0].Type].TipM, tip, 0.05);
        // shots of another moment draw their own scatter
        Assert.NotEqual(book[0].Scatter, book[2].Scatter);
        book.Clear();
        Assert.Equal(0, book.Count);
    }

    /// <summary>A flash shorter than a frame: the averaged parts carry its intensity's integral over the frame.</summary>
    [Fact]
    public void A_frame_average_keeps_a_short_flash()
    {
        var book = new ShotBook();
        book.Update(Shots((1000, 5)), 1000, 1, 0, ClassOf);   // a 3" mount: a 15 ms primary
        var e = book[0];
        Assert.True(e.EmptyS < 1 / 60.0);
        double dt = 1 / 30.0, t = dt;   // the frame [0, dt] holds the whole primary
        FlashEmitters.Averaged(e, t, dt, out var core, out _, out _);
        double sum = 0, h = 1e-5;
        for (double x = 0; x < dt; x += h)
        {
            FlashEmitters.At(e, x, out var c, out _, out _);
            sum += c.Intensity * h;
        }
        output.WriteLine($"core: averaged {core.Intensity:G4} cd against the frame's mean {sum / dt:G4}");
        Assert.True(core.Intensity > 0);
        Assert.InRange(core.Intensity, 0.5 * sum / dt, 1.5 * sum / dt);
    }

    /// <summary>The floor (docs/visuals/muzzle-flashes.md "Time"): a 4 ms flash shown over 33 ms keeps 14% of its peak
    /// (research §7.2), which keeps its Blondel-Rey effective intensity.</summary>
    [Fact]
    public void A_stretched_flash_keeps_its_effective_intensity()
    {
        double t = 0.004, t2 = 0.033, k = FlashRenderer.BlondelRey(t, t2 / t);
        Assert.InRange(k, 0.13, 0.145);
        Assert.Equal(t / (0.2 + t), k * t2 / (0.2 + t2), 9);
        Assert.Equal(1, FlashRenderer.BlondelRey(0.3, 1));
    }

    /// <summary>Under compression a salvo flashes whole or not at all, about the effects' keep share of them; the
    /// selected ship's always.</summary>
    [Fact]
    public void A_salvo_keeps_its_flash_whole_and_the_selected_ships_always()
    {
        var book = new ShotBook();
        var v = new ShotsView();
        v.EnsureCapacity(400);
        var at = Earth.Position(56.5, 4.0);
        for (int i = 0; i < 400; i++)
        {
            // 200 salvos of two mounts, newest first; the shooter 1, or 2 (the selected) every tenth salvo
            int salvo = 199 - i / 2;
            (v.Ms[i], v.Shooter[i], v.Class[i], v.Mount[i], v.Salvo[i], v.Barrels[i]) = (1000 + salvo * 10, salvo % 10 == 0 ? 2 : 1, 0, i % 2, salvo, 2);
            (v.Muzzle[i], v.Forward[i], v.ElevationDeg[i]) = (at, Earth.Forward(at, 90), 10);
        }
        v.Count = 400;
        book.Update(v, 4000, 60, 1 / 60.0, ClassOf, selected: 2);
        Assert.Equal(400, book.Count);
        int kept = 0;
        for (int i = 0; i < book.Count; i += 2)
        {
            Assert.Equal(book[i].Salvo, book[i + 1].Salvo);
            Assert.Equal(book[i].Shows, book[i + 1].Shows);
            if (book[i].Shooter == 2)
                Assert.True(book[i].Shows);
            else if (book[i].Shows)
                kept++;
        }
        output.WriteLine($"{kept} of 180 salvos flash at 60x (the share {Map.Effects.KeepShare(60):F2})");
        Assert.InRange(kept / 180.0, Map.Effects.KeepShare(60) - 0.12, Map.Effects.KeepShare(60) + 0.12);
    }

    /// <summary>A part's light (F4): its drawn brightness compressed, times a·b: a dim flash throws what it physically
    /// would, a blinding one at most knee × a·b however bright.</summary>
    [Fact]
    public void A_flash_throws_its_drawn_brightness()
    {
        var p = new Fleetwright.Sim.Combat.FlashGlow.Part { A = 18, B = 11, Intensity = (float)(1e5 * Math.PI * 18 * 11) };
        double dim = FlashRenderer.Thrown(p, 1e-9, 6), bright = FlashRenderer.Thrown(p, 1e3, 6);
        Assert.Equal(1e-9 * p.Intensity / Math.PI, dim, dim * 1e-3);
        Assert.InRange(bright, 0.99 * 6 * 18 * 11, 6 * 18 * 11);
        Assert.Equal(0, FlashRenderer.Thrown(default, 1, 6));
    }

    /// <summary>Flashes within reach of each other share a light at their weighted centre; apart they're two; past
    /// eight the dimmest goes.</summary>
    [Fact]
    public void A_light_of_nothing_adds_nothing()
    {
        var l = new FlashLights();
        l.Add(Earth.Position(56.5, 4.0) * 1.0, 0, System.Numerics.Vector3.One, 10);
        Assert.Equal(0, l.Count);
        l.Add(Earth.Position(56.5, 4.0), 5, System.Numerics.Vector3.One, 10);
        l.Add(Earth.Position(56.5, 4.0), 0, System.Numerics.Vector3.One, 10);
        Assert.Equal(1, l.Count);
        Assert.False(double.IsNaN(l[0].At.X));
    }

    [Fact]
    public void Flash_lights_gather_within_reach_and_keep_the_brightest()
    {
        var l = new FlashLights();
        var at = Earth.Position(56.5, 4.0);
        var east = Earth.Forward(at, 90);
        l.Add(at, 10, System.Numerics.Vector3.One, 10);
        l.Add(at + east * 5, 30, System.Numerics.Vector3.One, 10);
        Assert.Equal(1, l.Count);
        Assert.Equal(40, l[0].Rad, 9);
        Assert.Equal(3.75, (l[0].At - at).Length, 6);
        l.Add(at + east * 100, 5, System.Numerics.Vector3.One, 10);
        Assert.Equal(2, l.Count);
        for (int i = 2; i < 10; i++)
            l.Add(at + east * (200 * i), i, System.Numerics.Vector3.One, 10);
        Assert.Equal(LightUniforms.MaxFlashes, l.Count);
        double least = double.MaxValue;
        for (int i = 0; i < l.Count; i++)
            least = Math.Min(least, l[i].Rad);
        Assert.Equal(4, least);   // 2 and 3 gave way to brighter ones
    }

    /// <summary>The blast's front (F5): its arrival by scaled distance against muzzle_blast_ref.py's <c>arrival_lut</c>
    /// (which starts at 0.05, ours at the muzzle: ~0.01 apart), slower than sound's near the muzzle, sound's far out.</summary>
    [Theory]
    [InlineData(0.5, 0.3576)]
    [InlineData(1, 0.8256)]
    [InlineData(5, 4.7503)]
    [InlineData(20, 19.6851)]
    [InlineData(39, 38.6536)]
    public void The_blast_arrives_as_the_reference(double rho, double reference)
    {
        double t = BlastRenderer.ArrivalAt(rho);
        output.WriteLine($"rho {rho}: {t:F4} against {reference:F4}");
        Assert.Equal(reference, t, 0.03);
        Assert.True(t < rho);   // faster than sound behind the shock
    }

    /// <summary>A mount firing every <paramref name="everyMs"/> for <paramref name="n"/> shots, into a shot book and a
    /// smoke book stepped frame by frame at 1x; the wind from the south at <paramref name="windMs"/>.</summary>
    static (ShotBook Shots, SmokeBook Smoke) Fire(int n, long everyMs, double windMs, double afterS)
    {
        var shots = new ShotBook();
        var smoke = new SmokeBook();
        var at = Earth.Position(56.5, 4.0);
        long end = n * everyMs + (long)(afterS * 1000);
        for (long ms = 0; ms <= end; ms += 100)
        {
            int fired = (int)Math.Min(ms / everyMs + 1, n);
            var v = new ShotsView();
            v.EnsureCapacity(fired);
            for (int i = 0; i < fired; i++)
            {
                int k = fired - 1 - i;   // newest first
                (v.Ms[i], v.Shooter[i], v.Class[i], v.Mount[i], v.Salvo[i], v.Barrels[i]) = (k * everyMs, 1, 0, 0, k, 2);
                (v.Muzzle[i], v.Forward[i], v.ElevationDeg[i]) = (at, Earth.Forward(at, 90), 10);
            }
            v.Count = fired;
            shots.Update(v, ms, 1, 0.1, ClassOf);
            smoke.Update(shots, shots.SeaClock, 0, 180, windMs);
        }
        return (shots, smoke);
    }

    /// <summary>A puff spreads as the research's table (§5.3: a 16" SPD's σ_h at 5, 30 and 120 s in a 5 m/s wind: 10, 20
    /// and 67 m; here a 12" twin's λ_f, so its core is a little smaller) and drifts with the wind.</summary>
    [Fact]
    public void A_puff_spreads_and_drifts_as_the_research_says()
    {
        var (shots, smoke) = Fire(1, 1000, 5, 120);
        Assert.Equal(1, smoke.Count);
        smoke.State(0, out var c, out double z, out double sigma, out double area, out double age);
        var p = smoke[0];
        double core = 0.35 * p.Lambda, g = 0.07 * 5 + 0.2;
        output.WriteLine($"at {age:F0} s: sigma {sigma:F1} m, {area:F0} m², {z:F1} m up, {Earth.Distance(c, p.Origin):F0} m from its start");
        Assert.Equal(Math.Sqrt(core * core + g * g * age * age + 0.5 * age), sigma, 6);
        Assert.InRange(sigma, 60, 75);
        // drifted north with the wind, some 600 m in 120 s, and carried a little east by the jet
        double north = Vec3d.Dot(c - p.Origin, Earth.Forward(p.Origin, 0)), east = Vec3d.Dot(c - p.Origin, Earth.Forward(p.Origin, 90));
        Assert.InRange(north, 5 * age - 5, 5 * age + 5);
        Assert.InRange(east, 50, 100);
    }

    /// <summary>A ship's settled puffs that overlap merge, keeping their extinction area and centre; apart, they don't.</summary>
    [Fact]
    public void Overlapping_puffs_merge_keeping_their_smoke()
    {
        // every 2 s in a calm: the puffs lie on each other
        var (shots, calm) = Fire(10, 2000, 0, 10);
        double total = 0;
        for (int i = 0; i < calm.Count; i++)
        {
            calm.State(i, out _, out _, out _, out double a, out _);
            total += a;
        }
        output.WriteLine($"calm: {calm.Count} puffs of 10, {total:F0} m² against {10 * shots[0].SmokeM2:F0}");
        Assert.True(calm.Count <= 2);
        Assert.Equal(10 * (shots[0].SmokeM2 + shots[0].FogM2 * 0), total, 0.05 * total + 10 * shots[0].FogM2);
        // every 40 s in a fresh wind: each its own, a trail downwind
        var (_, windy) = Fire(6, 40_000, 8, 5);
        output.WriteLine($"windy: {windy.Count} puffs of 6");
        Assert.Equal(6, windy.Count);
    }

    [Fact]
    public void A_frame_allocates_nothing()
    {
        var book = new ShotBook();
        var smoke = new SmokeBook();
        var v = Shots((1000, 0), (1000, 1), (2000, 2));
        book.Update(v, 2000, 1, 0.016, ClassOf);
        smoke.Update(book, book.SeaClock, 0, 180, 5);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int f = 0; f < 600; f++)
        {
            book.Update(v, 2000 + f * 16, 1, 0.016, ClassOf);
            smoke.Update(book, book.SeaClock, 0, 180, 5);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(smoke.Count > 0);
    }
}
