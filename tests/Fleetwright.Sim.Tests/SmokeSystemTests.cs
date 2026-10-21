using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The smoke field's store (docs/battle/smoke.md Z1, Z4): off by default; ships off rails lay their trails, back
/// along their course when they come off; the trails hold together and stay bounded; every source its own trail (funnels,
/// guns, fires, a magazine); a screen burns rich; the same world at any fine speed and on replay; nothing allocated once
/// grown.</summary>
public class SmokeSystemTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(new DateTime(1916, 5, 31, 14, 0, 0, DateTimeKind.Utc));
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly SimSettings On = Settings with { Smoke = true, RailsEnabled = false };
    const double Lat = 56.5, Lon = 4.0, Kn = 18;

    /// <summary>A column of dreadnoughts steaming east off rails, a light SW wind.</summary>
    static World Column(SimSettings s, int ships = 3)
    {
        var w = new World(Start, 5, s);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(Command.SetWeather(25_000, 225, 4));
        for (int i = 0; i < ships; i++)
            w.Apply(ShipSystem.Spawn(c, Lat - 0.005 * i, Lon - 0.01 * i, 90, Kn));
        return w;
    }

    static void Run(World w, double minutes) => w.AdvanceTo(w.Time + (long)Math.Round(minutes * 60_000));

    [Fact]
    public void Off_by_default()
    {
        var w = Column(Settings with { RailsEnabled = false });
        Run(w, 10);
        Assert.Equal(0, w.Smoke.Count);
        Assert.Equal(0, w.Smoke.Laid);
    }

    [Fact]
    public void Ships_on_rails_lay_none()
    {
        // a ship spawns on the fine tick and settles onto rails once she's steady: what she laid till then only ages
        var w = Column(Settings with { Smoke = true });
        Run(w, 5);
        for (int e = 1; e <= 3; e++)
            Assert.True(w.Ships.Store[w.Ships.Store.SlotOf(e)].OnRails, $"#{e} on rails");
        long laid = w.Smoke.Laid;
        Run(w, 30);
        output.WriteLine($"laid {laid} before the rails took them, none after; {w.Smoke.Count} left of it");
        Assert.Equal(laid, w.Smoke.Laid);
    }

    [Fact]
    public void A_ship_lays_her_trail_astern_and_back_along_her_course()
    {
        var w = Column(On, 1);
        Run(w, 0.05);
        // she came off rails at the start: her trail laid back half an hour along her course, as if she'd smoked all along
        var ship = w.Ships.Store[w.Ships.Store.SlotOf(1)];
        double now = w.Time.Ms / 1000.0, furthest = 0;
        var east = Earth.East(ship.Position);
        for (int i = 0; i < w.Smoke.Slots; i++)
            if (w.Smoke.IsLive(i))
            {
                var p = w.Smoke.State(i, now);
                furthest = Math.Max(furthest, -Vec3d.Dot(p.Centre - ship.Position.Normalized() * Earth.Radius, east));
            }
        output.WriteLine($"{w.Smoke.Count} puffs at the start; the oldest {furthest / 1000:F1} km astern");
        Assert.True(w.Smoke.Count > 5);
        // 18 kn against a 4 m/s quartering wind's drift: some 8 m/s through the air, ~15 km in half an hour
        Assert.InRange(furthest, 8_000, 25_000);
        Run(w, 10);
        Assert.True(w.Smoke.Count > 100, $"{w.Smoke.Count} after ten minutes");
    }

    [Fact]
    public void Trails_merge_and_stay_bounded()
    {
        var w = Column(On, 1);
        Run(w, 60);
        int perHour = w.Smoke.Count;
        Run(w, 60);
        output.WriteLine($"one ship: {perHour} puffs after an hour, {w.Smoke.Count} after two; laid {w.Smoke.Laid}, merged {w.Smoke.Merged}, retired {w.Smoke.Retired}");
        Assert.True(w.Smoke.Merged > w.Smoke.Laid / 2, "most puffs merge");
        Assert.InRange(w.Smoke.Count, 50, 600);
        Assert.True(w.Smoke.Count < 1.5 * perHour, "the second hour adds little");
    }

    [Fact]
    public void The_same_world_at_any_fine_speed()
    {
        ulong Run(double speed)
        {
            var w = AllSources(On);
            w.Apply(Command.SetSpeed(speed));
            SmokeSystemTests.Run(w, 30);
            Assert.True(w.Smoke.Count > 0);
            w.Apply(Command.SetSpeed(1));
            return w.ComputeHash();
        }
        Assert.Equal(Run(1), Run(Settings.CoarseMinSpeed - 1));
    }

    [Fact]
    public void A_replay_hashes_the_same()
    {
        var live = AllSources(On);
        Run(live, 10);
        live.Apply(ShipSystem.Course(2, 45));
        live.Apply(SmokeSystem.MakeSmoke(6, false));
        Run(live, 15);
        Assert.True(live.Smoke.Count > 0);
        var replay = new World(Start, 5, On);
        replay.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        replay.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        foreach (var sc in live.CommandLog)
        {
            replay.AdvanceTo(sc.At);
            replay.Apply(sc.Command);
        }
        replay.AdvanceTo(live.Time);
        Assert.Equal(live.ComputeHash(), replay.ComputeHash());
    }

    /// <summary>The column, and an enemy squadron abreast of it some 12 km off, in sight: lines asked every look.</summary>
    static World Engaged(SimSettings s)
    {
        var w = Column(s, 4);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);   // the same class: its id again
        for (int i = 0; i < 3; i++)
            w.Apply(ShipSystem.Spawn(c, Lat + 0.11, Lon - 0.01 * i, 90, Kn, 1));
        return w;
    }

    /// <summary>The engaged world with every source going (Z4): the enemy's leader firing on the column's, the column's
    /// second ship on fire (her side unprepared, with 1890's firefighting: it takes hold), the enemy's second making
    /// smoke, the column's last blowing up.</summary>
    static World AllSources(SimSettings s)
    {
        var w = Engaged(s);
        w.Apply(GunnerySystem.Engage(5, 0, 1));
        w.Apply(DamageSystem.SetReadiness(0, Array.FindIndex(s.Readiness, r => r.Name == "unprepared")));
        w.Apply(DamageSystem.SetDamageControl(0, Array.FindIndex(s.DamageControl, d => d.Name == "1890")));
        foreach (double x in new[] { -30.0, 0, 30 })
            w.Apply(DamageSystem.Burn(2, x));
        w.Apply(SmokeSystem.MakeSmoke(6, true));
        w.Apply(DamageSystem.Sink(4, LossKind.Magazine));
        return w;
    }

    [Fact]
    public void Every_source_lays_its_own_trail()
    {
        var w = AllSources(On);
        Run(w, 3);
        var count = new int[4];
        var area = new double[4];
        double now = w.Time.Ms / 1000.0;
        for (int i = 0; i < w.Smoke.Slots; i++)
            if (w.Smoke.IsLive(i))
            {
                int k = (int)w.Smoke.KindOf(i);
                count[k]++;
                area[k] += w.Smoke.State(i, now).Area;
            }
        output.WriteLine(string.Join("; ", Enum.GetValues<SmokeSystem.SourceKind>().Select(k => $"{k}: {count[(int)k]} puffs, {area[(int)k]:F0} m²")));
        Assert.True(count[(int)SmokeSystem.SourceKind.Funnels] > 0);
        Assert.True(count[(int)SmokeSystem.SourceKind.Guns] > 0, "the guns' smoke");
        Assert.True(count[(int)SmokeSystem.SourceKind.Burst] > 1, "the fireball and the wreck's fires");
        Assert.True(w.Smoke.Screening(6));
        for (int m = 0; m < 12; m++)
        {
            Run(w, 1);
            var cond = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(2));
            int burning = 0;
            for (int f = 0; f < cond.Fire.Length; f++)
                burning += cond.Fire[f].Burning ? 1 : 0;
            output.WriteLine($"{m + 4} min: {burning} spaces burning, {cond.FireShowMW:F1} MW showing");
        }
        now = w.Time.Ms / 1000.0;
        int fires = 0;
        for (int i = 0; i < w.Smoke.Slots; i++)
            fires += w.Smoke.IsLive(i) && w.Smoke.KindOf(i) == SmokeSystem.SourceKind.Fires ? 1 : 0;
        output.WriteLine($"after 15 min: {fires} fire puffs; she shows {w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(2)).FireShowMW:F1} MW");
        Assert.True(fires > 0, "her fire's smoke");
    }

    [Fact]
    public void A_screen_burns_rich()
    {
        // the same ship with and without making smoke: her trail's smoke by its ratio, the screen's a kg over her plant's
        double Laid(bool screen)
        {
            var w = Engaged(On);
            if (screen)
                w.Apply(SmokeSystem.MakeSmoke(6, true));
            Run(w, 2);
            double a = 0, now = w.Time.Ms / 1000.0;
            for (int i = 0; i < w.Smoke.Slots; i++)
                if (w.Smoke.IsLive(i) && w.Smoke.EntityOf(i) == 6 && now - w.Smoke.Puff(i).Born < 60)
                    a += w.Smoke.State(i, now).Area;
            return a;
        }
        var c = Fleet.Get("dreadnought").Class!;
        double plain = Laid(false), rich = Laid(true), want = Math.Max(On.SmokeScreenM2PerKg / c.Smoke.M2PerKg, 1);
        output.WriteLine($"her last minute's smoke {plain:F0} m², making smoke {rich:F0} m² (×{rich / plain:F2}, want ×{want:F2})");
        Assert.Equal(want, rich / plain, 0.02 * want);
    }

    [Fact]
    public void The_index_finds_what_summing_every_puff_finds()
    {
        var w = Engaged(On);
        long t = (w.Time.Ms / On.SmokeIndexMs + 2) * On.SmokeIndexMs + 20 * 60_000;
        w.AdvanceTo(new WorldTime(t + 1));   // the tick at t has run: the index is the states at t
        var store = w.Ships.Store;
        Span<double> got = stackalloc double[SmokeSystem.PairTaus];
        Span<double> all = stackalloc double[SmokeSystem.PairTaus];
        int pairs = 0;
        for (int o = 1; o <= 4; o++)
            for (int g = 5; g <= 7; g++)
            {
                int os = store.SlotOf(o), ts = store.SlotOf(g);
                w.Smoke.TauUncached(w, os, ts, got);
                // every puff, its state at t, on the same lines
                all.Clear();
                var oc = store[os].Handling.Class;
                int ne = Math.Min(oc.Eyes.Length, SmokeSystem.PairEyes);
                var from = new double[ne];
                for (int e = 0; e < ne; e++)
                    from[e] = oc.Eyes[SmokeModel.PairEye(e, oc.Eyes.Length)].HeightM;
                double top = store[ts].Handling.Class.Silhouette.TopM;
                double[] to = [.. On.SmokeBands.Select(b => b * top)];
                var a = w.Smoke.Frame.ToAir(store[os].Position.Normalized() * Earth.Radius);
                var b = w.Smoke.Frame.ToAir(store[ts].Position.Normalized() * Earth.Radius);
                var line = new SmokeLine(a, b, from[0], to[0], On.SightRefraction);
                for (int i = 0; i < w.Smoke.Slots; i++)
                    if (w.Smoke.IsLive(i))
                    {
                        var st = SmokeModel.State(w.Smoke.Puff(i), t / 1000.0, AirFrame.Identity, w.Smoke.Air) with { Centre = w.Smoke.Puff(i).Origin };
                        SmokeModel.TauGrid(line, st, from, to, all[..(ne * to.Length)]);
                    }
                for (int j = 0; j < ne * to.Length; j++)
                {
                    Assert.True(Math.Abs(got[j] - all[j]) <= 1e-9 * Math.Max(all[j], 1e-6) + 1e-12, $"#{o} on #{g}, line {j}: {got[j]} against {all[j]}");
                    Assert.True(all[j] < SmokeSystem.SmokeOpaqueTau);   // no early out to tell apart
                }
                pairs += all[0] > 0.01 ? 1 : 0;
            }
        output.WriteLine($"{pairs} of 12 pairs' lines through smoke; {w.Smoke.Indexed} puffs indexed in {w.Smoke.Chunks} chunks");
        Assert.True(pairs >= 3, "some lines cross the trails");
    }

    [Fact]
    public void Nothing_allocated_once_grown()
    {
        var w = Engaged(On);
        Run(w, 60);   // grown: the trails' puffs at their steady count
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(w, 30);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"half an hour of seven ships smoking in sight of each other: {allocated} bytes, {w.Smoke.Count} puffs, {w.Smoke.Asked} lines asked");
        Assert.True(w.Smoke.Worked > 100, "lines worked out");
        Assert.Equal(0, allocated);
    }
}
