using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The smoke field's store (docs/battle/smoke.md Z1): off by default; ships off rails lay their trails, back
/// along their course when they come off; the trails hold together and stay bounded; the same world at any fine speed and
/// on replay; nothing allocated once grown.</summary>
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
            var w = Column(On);
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
        var live = Column(On);
        Run(live, 10);
        live.Apply(ShipSystem.Course(2, 45));
        Run(live, 15);
        Assert.True(live.Smoke.Count > 0);
        var replay = new World(Start, 5, On);
        replay.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        foreach (var sc in live.CommandLog)
        {
            replay.AdvanceTo(sc.At);
            replay.Apply(sc.Command);
        }
        replay.AdvanceTo(live.Time);
        Assert.Equal(live.ComputeHash(), replay.ComputeHash());
    }

    [Fact]
    public void Nothing_allocated_once_grown()
    {
        var w = Column(On, 6);
        Run(w, 60);   // grown: the trails' puffs at their steady count
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(w, 30);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"half an hour of six ships smoking: {allocated} bytes, {w.Smoke.Count} puffs");
        Assert.Equal(0, allocated);
    }
}
