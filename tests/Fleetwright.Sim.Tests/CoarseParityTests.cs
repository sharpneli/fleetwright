using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The coarse tier's kept parity checks (docs/battle/coarse.md J8, "When it runs"): a small panel of duels played
/// both ways, the coarse tier against the fine one within the bench's bands (the full panel is `shipgen coarse-parity`);
/// and the coarse invariants not held elsewhere: the tier crossed mid-engagement keeps its engagement and its shells in
/// flight, and coarse fire allocates nothing. A fine change that coarse can't follow (something counted in ticks, a
/// cadence coarse doesn't share) fails here first.</summary>
public class CoarseParityTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.0, Lon = 4.0, Coarse = 1000;

    /// <summary>A panel duel: the shooter steaming north, the target abeam to starboard on the same course, zigzagging
    /// as the bench's target does if asked (each leg <see cref="LegS"/> × 0.7-1.3, to ±<see cref="ZigDeg"/> of north on a
    /// side drawn by the seed); damage on or off.</summary>
    public sealed record Duel(string Name, string Shooter, int Battery, string Target, double RangeM, double Kn, double TargetKn, double ZigDeg,
        double LegS, bool Damage, double Minutes)
    {
        public override string ToString() => Name;
    }

    static readonly Duel[] Duels =
    [
        new("battleship v battleship, 14 km", "super_dreadnought", 0, "super_dreadnought", 14_000, 15, 15, 0, 60, false, 15),
        new("secondaries v destroyer, 6 km, zigzag", "super_dreadnought", 1, "destroyer", 6_000, 20, 30, 30, 75, false, 15),
        new("light cruiser v destroyer, 8 km, zigzag", "light_cruiser", 0, "destroyer", 8_000, 25, 30, 30, 75, false, 20),
    ];

    public static TheoryData<Duel> Panel => new(Duels);

    // the duels' hits vary from 2 to 25 a run: 48 seeds put the coarse tier's difference from the fine tick's within a
    // standard error of ~6.5%, so the bands sit at about three of them (salvos vary little)
    const int Seeds = 48;
    const double EngageAfterS = 90;

    /// <summary>A duel's world: the two ships at sea, the shooter's battery engaged at once (or after
    /// <paramref name="engageAfterS"/>: the panel's, once the coarse tier's first look has had them see each other, so it
    /// compares the fire and not the minute's first sighting).</summary>
    static World Make(Duel d, double speed, int seed, double engageAfterS = 0)
    {
        var w = new World(Start, (ulong)seed, Settings with { HitsDoDamage = d.Damage });
        int a = w.Ships.AddClass(Fleet.Get(d.Shooter).Class!), b = w.Ships.AddClass(Fleet.Get(d.Target).Class!);
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, d.RangeM);
        var (tl, to) = Earth.LatLon(p);
        if (speed != 1)
            w.Apply(Command.SetSpeed(speed));
        w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, d.Kn, side: 0));
        w.Apply(ShipSystem.Spawn(b, tl, to, 0, d.TargetKn, side: 1));
        if (engageAfterS > 0)
            w.AdvanceTo(w.Time + (long)(engageAfterS * 1000));
        w.Apply(GunnerySystem.Engage(1, d.Battery, 2));
        return w;
    }

    /// <summary>A duel played to its end: the shooter's salvos and hits, the target steered leg by leg (the same orders at
    /// the same moments in either tier: the legs drawn from the seed, not the world).</summary>
    static (int Salvos, int Hits) Play(Duel d, double speed, int seed)
    {
        var w = Make(d, speed, seed, EngageAfterS);
        var rng = new Random(seed * 7919 + 17);
        long from = w.Time.Ms, end = from + (long)(d.Minutes * 60_000);
        for (long t = from + (long)(d.LegS * 1000 * (0.3 + 0.7 * rng.NextDouble())); t < end; t += (long)(d.LegS * 1000 * (0.7 + 0.6 * rng.NextDouble())))
        {
            w.AdvanceTo(new WorldTime(t));
            if (d.ZigDeg > 0 && w.Ships.Store.SlotOf(2) >= 0)
                w.Apply(ShipSystem.Course(2, (360 + (rng.Next(2) * 2 - 1) * d.ZigDeg) % 360));
        }
        w.AdvanceTo(new WorldTime(end));
        Assert.Equal(speed >= Settings.CoarseMinSpeed ? ClockTier.Coarse : ClockTier.Fine, w.Ships.Store[w.Ships.Store.SlotOf(1)].Tier);
        var tally = w.Gunnery.TallyOf(1, d.Battery) ?? default;
        return (tally.Salvos, tally.Hits);
    }

    /// <summary>The panel, on sums over fixed seeds: the coarse tier's salvos within 15% of the fine tick's (decision 5's
    /// band), its hits within 20% (the band at three standard errors of 48 seeds' hits).</summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void The_coarse_tier_fires_and_hits_as_the_fine_tick_does(Duel d)
    {
        var runs = new (int Salvos, int Hits)[2 * Seeds];
        Parallel.For(0, runs.Length, i => runs[i] = Play(d, i < Seeds ? 1 : Coarse, 1 + i % Seeds));
        var (fs, fh) = (runs[..Seeds].Sum(r => r.Salvos), runs[..Seeds].Sum(r => r.Hits));
        var (cs, ch) = (runs[Seeds..].Sum(r => r.Salvos), runs[Seeds..].Sum(r => r.Hits));
        output.WriteLine($"{d.Name}, {Seeds} seeds: fine {fs} salvos, {fh} hits; coarse {cs} salvos ({(cs - fs) * 100.0 / fs:+0;-0}%), " +
                         $"{ch} hits ({(ch - fh) * 100.0 / Math.Max(fh, 1):+0;-0}%)");
        Assert.True(fs > 0 && fh > 0, "the fine tick fires and hits");
        Assert.InRange(cs, 0.85 * fs, 1.15 * fs);
        Assert.InRange(ch, 0.8 * fh, 1.2 * fh);
    }

    static Vec3d Drawn(World w, int e)
    {
        var s = w.Ships.Store[w.Ships.Store.SlotOf(e)];
        var (p, f) = (s.Position, s.Forward);
        ShipMotion.Extrapolate(ref p, ref f, s.Speed, s.YawRate, (w.Time.Ms - s.PoseMs) / 1000.0);
        return p;
    }

    /// <summary>The tier crossed mid-engagement, with shells in the air (coarse.md's invariants): fine to coarse and back
    /// moves neither ship, and loses neither the engagement (its salvos, its phase) nor a shell in flight; the battery fires
    /// on in each tier.</summary>
    [Fact]
    public void Crossing_the_tier_mid_engagement_keeps_the_fire_and_the_shells_in_flight()
    {
        var d = Duels[0] with { RangeM = 12_000 };
        var w = Make(d, 1, 3);
        w.AdvanceTo(w.Time + 4 * 60_000);
        void Cross(double speed, ClockTier tier)
        {
            while (w.Shells.InFlight == 0)
                w.AdvanceTo(w.Time + 100);
            var (a, b) = (Drawn(w, 1), Drawn(w, 2));
            var before = w.Gunnery.TallyOf(1, 0)!.Value;
            int flying = w.Shells.InFlight;
            w.Apply(Command.SetSpeed(speed));
            Assert.Equal(tier, w.Ships.Store[w.Ships.Store.SlotOf(1)].Tier);
            double moved = Math.Max(Earth.Distance(a, Drawn(w, 1)), Earth.Distance(b, Drawn(w, 2)));
            var after = w.Gunnery.TallyOf(1, 0)!.Value;
            output.WriteLine($"to {tier}: moved {moved * 1000:F2} mm, {flying} shells in the air, salvos {before.Salvos} -> {after.Salvos}, phase {after.Phase}");
            Assert.True(moved < 0.001, $"moved {moved:F4} m");
            Assert.Equal(flying, w.Shells.InFlight);
            Assert.Equal((before.Salvos, before.Shells, before.Phase), (after.Salvos, after.Shells, after.Phase));
            int hits = w.Shells.Hits + w.Shells.Misses;
            w.AdvanceTo(w.Time + 3 * 60_000);
            Assert.True(w.Shells.Hits + w.Shells.Misses > hits, "its shells came down");
            Assert.True(w.Gunnery.TallyOf(1, 0)!.Value.Salvos > after.Salvos, $"it fires on, {tier}");
        }
        Cross(Coarse, ClockTier.Coarse);
        Cross(1, ClockTier.Fine);
    }

    /// <summary>Coarse fire allocates nothing (coarse.md's invariants): a light cruiser engaging a zigzagging destroyer
    /// with damage on, the second ten minutes against the first's warmed buffers.</summary>
    [Fact]
    public void Coarse_fire_allocates_nothing()
    {
        var d = Duels[^1] with { RangeM = 7_000, Damage = true };
        var w = Make(d, Coarse, 5);
        int side = 1;
        void Legs(int n)
        {
            for (int i = 0; i < n; i++)
            {
                w.Apply(ShipSystem.Course(2, (360 + side * d.ZigDeg) % 360));
                side = -side;
                w.AdvanceTo(w.Time + (long)(d.LegS * 1000));
            }
        }   // (a regular zigzag: what allocates is the same either way)
        Legs(8);
        int salvos = w.Gunnery.TallyOf(1, 0)!.Value.Salvos;
        long before = GC.GetAllocatedBytesForCurrentThread();
        Legs(8);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var t = w.Gunnery.TallyOf(1, 0)!.Value;
        output.WriteLine($"10 minutes of coarse fire: {t.Salvos - salvos} salvos, {t.Hits} hits in all, {allocated} bytes allocated");
        Assert.True(t.Salvos > salvos, "it fired");
        Assert.Equal(0, allocated);
    }
}
