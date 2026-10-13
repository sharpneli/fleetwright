using System.Diagnostics;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Rails and the land (docs/world/terrain-plan.md, step S2): a unit on rails heading for a coast comes off short
/// of it, at its guide's look-ahead and a margin (a formation's widest station abeam further), and the fine tick's slide
/// takes it along; it doesn't go back on rails heading for the land; at any clock speed no ship is ever on land. And
/// what coast-following on the fine tick costs the clock, S3's baseline.</summary>
public class RailsLandTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static Shore Shore => TestLand.Shore;

    static (World W, AiSystem Ai, int Class) Make(double speed, string design = "dreadnought")
    {
        var w = new World(Start, 11, Settings, shore: Shore);
        var ai = new AiSystem(w);
        w.Apply(Command.SetSpeed(speed));
        return (w, ai, w.Ships.AddClass(Fleet.Get(design).Class!));
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static double ClearanceOf(Vec3d p) => Shore.IsLand(p) ? -1 : Shore.Clearance(p, 20_000, out _);

    /// <summary>What a run saw of some ships: their closest approach to the coast, whether each was on rails, and the
    /// clearance of each where it last came off rails.</summary>
    sealed record Seen(double[] Closest, bool[] Railed, double[] OffAt);

    static Seen Run(World w, double seconds, double stepS, params int[] ships)
    {
        var seen = new Seen(ships.Select(_ => double.PositiveInfinity).ToArray(), new bool[ships.Length], ships.Select(_ => double.NaN).ToArray());
        var was = new bool[ships.Length];
        for (double t = 0; t < seconds; t += stepS)
        {
            w.AdvanceTo(w.Time + (long)(stepS * 1000));
            for (int k = 0; k < ships.Length; k++)
            {
                var s = Ship(w, ships[k]);
                double c = ClearanceOf(s.Position);
                seen.Closest[k] = Math.Min(seen.Closest[k], c);
                seen.Railed[k] |= s.OnRails;
                if (was[k] && !s.OnRails)
                    seen.OffAt[k] = c;
                was[k] = s.OnRails;
            }
        }
        return seen;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100_000)]
    public void A_ship_on_rails_into_Norway_comes_off_short_of_the_coast_and_slides(double speed)
    {
        var (w, _, c) = Make(speed);
        w.Apply(ShipSystem.Spawn(c, 59.0, 3.4, 90, 15));   // ~120 km off Karmøy, steaming at it
        var seen = Run(w, 6 * 3600, 1, 1);
        var s = Ship(w, 1);
        var (lat, lon) = Earth.LatLon(s.Position);
        output.WriteLine($"at {speed}x: on rails {seen.Railed[0]}, came off {seen.OffAt[0]:F0} m off the coast, closest {seen.Closest[0]:F0} m, " +
                         $"backstops {s.Backstops}; now {lat:F3}, {lon:F3}, side {s.LandSide}, on rails {s.OnRails}");
        Assert.True(seen.Railed[0], "never went on rails");
        Assert.True(seen.OffAt[0] > s.Handling.StandoffM + Settings.RailsLandMarginM, $"came off rails {seen.OffAt[0]:F0} m off the coast");
        Assert.True(seen.Closest[0] >= s.Handling.MinClearanceM, $"came {seen.Closest[0]:F0} m off the coast");
        Assert.Equal(0, s.Backstops);
    }

    [Fact]
    public void A_line_abreast_on_rails_comes_off_further_out_by_its_width()
    {
        var (w, _, c) = Make(1);
        for (int i = 0; i < 3; i++)
            w.Apply(ShipSystem.Spawn(c, 59.0 - 0.009 * i, 3.4, 90, 15));   // ~1 km apart, abreast heading east
        w.Apply(GroupSystem.CreateOfNewest(3, Formation.LineAbreast, 1000));
        var seen = Run(w, 5 * 3600, 1, 1, 2, 3);
        var g = Ship(w, 1);
        output.WriteLine($"guide on rails {seen.Railed[0]}, came off {seen.OffAt[0]:F0} m off the coast; closest {string.Join(", ", seen.Closest.Select(x => x.ToString("F0")))} m");
        Assert.True(seen.Railed[0], "never went on rails");
        Assert.True(seen.OffAt[0] > g.Handling.StandoffM + 1000 + Settings.RailsLandMarginM, $"came off rails {seen.OffAt[0]:F0} m off the coast");
        for (int i = 0; i < 3; i++)
            Assert.True(seen.Closest[i] >= Ship(w, i + 1).Handling.MinClearanceM, $"ship {i + 1} came {seen.Closest[i]:F0} m off the coast");
    }

    [Fact]
    public void A_route_leg_across_an_island_comes_off_before_it()
    {
        var (w, _, c) = Make(30_000);
        w.Apply(ShipSystem.Spawn(c, 55.13, 13.6, 90, 15));
        w.Apply(ShipSystem.AddWaypoint(1, 55.13, 15.8));   // through Bornholm
        var seen = Run(w, 6 * 3600, 1, 1);
        var s = Ship(w, 1);
        output.WriteLine($"on rails {seen.Railed[0]}, came off {seen.OffAt[0]:F0} m off the coast, closest {seen.Closest[0]:F0} m, backstops {s.Backstops}, now {Earth.LatLon(s.Position)}");
        Assert.True(seen.Railed[0]);
        Assert.True(seen.Closest[0] >= s.Handling.MinClearanceM, $"came {seen.Closest[0]:F0} m off the coast");
        Assert.True(Earth.LatLon(s.Position).Lon > 15.5, "didn't get past the island");
    }

    [Fact]
    public void No_ship_is_ever_on_land_at_a_million_times()
    {
        // destroyers every which way in the Skagerrak and the Kattegat at 1M x for six hours (sliding ships are on the
        // fine tick, so it's kept small): every pose checked ten seconds apart
        var (w, _, c) = Make(1_000_000, "destroyer");
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3; j++)
                w.Apply(ShipSystem.Spawn(c, 57.4 + 0.35 * j, 8.5 + 1.1 * i, (i * 70 + j * 45) % 360, 20));
        int n = w.Ships.Store.Count;
        double closest = double.PositiveInfinity;
        int backstops = 0;
        for (int m = 0; m < 6 * 360; m++)
        {
            w.AdvanceTo(w.Time + 10_000);
            for (int e = 1; e <= n; e++)
                if (w.Ships.Store.SlotOf(e) >= 0)
                    closest = Math.Min(closest, ClearanceOf(Ship(w, e).Position) - Ship(w, e).Handling.MinClearanceM);
        }
        for (int e = 1; e <= n; e++)
            backstops += Ship(w, e).Backstops;
        output.WriteLine($"{n} destroyers, six hours at 1M x: {closest:F0} m outside their minimum at the closest, {backstops} backstops");
        Assert.True(closest >= 0, $"a ship came {-closest:F0} m inside its minimum");
    }

    /// <summary>S3's baseline: the clock a ship and a squadron following Norway's coast on the fine tick allow, against
    /// asked (the most the sim can keep, game seconds a wall second, with nothing else in the world).</summary>
    [Fact]
    public void What_a_coast_costs_the_clock()
    {
        Measure(1, 10_000, write: false);   // warm: the first run is the JIT's
        foreach (int count in new[] { 1, 4 })
            foreach (double speed in new[] { 10_000.0, 100_000.0, 1_000_000.0 })
                Measure(count, speed);
    }

    void Measure(int count, double speed, bool write = true)
    {
            {
                var (w, _, c) = Make(speed);
                for (int i = 0; i < count; i++)
                    w.Apply(ShipSystem.Spawn(c, 59.0 - 0.006 * i, 4.9, 90, 15));
                if (count > 1)
                    w.Apply(GroupSystem.CreateOfNewest(count, Formation.Column, 600));
                w.AdvanceTo(w.Time + 2 * 3_600_000);   // onto the coast and sliding
                double hours = 1;
                var sw = Stopwatch.StartNew();
                w.AdvanceTo(w.Time + (long)(hours * 3_600_000));
                sw.Stop();
                double kept = hours * 3600 / sw.Elapsed.TotalSeconds;
                int fine = w.Ships.Store.FineCount;
                if (write)
                    output.WriteLine($"{count} ship{(count > 1 ? "s" : "")} along Norway, asked {speed:#,0}x: keeps {Math.Min(kept, speed):#,0}x " +
                                 $"(the sim's most {kept:#,0}x), {fine} on the fine tick at the end");
            }
    }

    [Fact]
    public void Rails_with_the_land_are_the_same_twice()
    {
        ulong Once()
        {
            var (w, _, c) = Make(100_000);
            w.Apply(ShipSystem.Spawn(c, 59.0, 3.4, 90, 15));
            w.Apply(ShipSystem.Spawn(c, 58.5, 3.4, 60, 18));
            w.AdvanceTo(w.Time + 8 * 3_600_000);
            return w.ComputeHash();
        }
        Assert.Equal(Once(), Once());
    }
}
