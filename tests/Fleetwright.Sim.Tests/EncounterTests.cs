using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Encounters (docs/battle/plan.md step 3.1b): ships of sides at war within the encounter range open one at
/// the contact; it holds its ships on the fine tick, merges with another its ships come near, splits when they part,
/// and closes once its sides have been apart for <see cref="SimSettings.EncounterCloseMs"/>.</summary>
public class EncounterTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Kn = 15;

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static int Of(World w, int e) => w.Encounters.Of(w.Ships.Store.SlotOf(e), e);

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    static (World W, int Class) Make()
    {
        var w = new World(Start, 9, Settings);
        return (w, w.Ships.AddClass(Fleet.Get("dreadnought").Class!));
    }

    /// <summary>A ship at a point a distance and bearing from another place.</summary>
    static int SpawnFrom(World w, int c, double lat, double lon, double bearing, double metres, double heading, double kn, byte side)
    {
        var p = Earth.Position(lat, lon);
        var f = Earth.Forward(p, bearing);
        Earth.Advance(ref p, ref f, metres);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(c, la, lo, heading, kn, side));
        return w.Ships.NewestEntity;
    }

    [Fact]
    public void Ships_at_war_open_one_at_the_contact_held_on_the_fine_tick_until_it_closes()
    {
        var (w, c) = Make();
        w.Apply(ShipSystem.Spawn(c, 55, 4, 0, Kn, 1));
        w.Apply(ShipSystem.Spawn(c, 57, 4, 180, Kn, 2));
        Run(w, 60);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails);
        Assert.Equal(0, w.Encounters.Count);
        while (Ship(w, 1).OnRails)
            Run(w, 1);
        // the rails' contact opens it then and there
        Assert.Equal(1, w.Encounters.Count);
        var e = w.Encounters.Open[0];
        Assert.Equal(1, e.Id);
        Assert.Equal(2, e.Ships);
        Assert.True(w.Time.Ms - e.OpenedMs < 1000, $"opened {w.Time.Ms - e.OpenedMs} ms before the look");
        Assert.True(Of(w, 1) == 1 && Of(w, 2) == 1);
        Assert.InRange(e.RadiusM, 24_000, 26_000);   // half the range: its ships about its middle

        // they pass and open the range: the encounter stays while they're within the range and margin, and for the
        // close time after; its ships on the fine tick throughout
        long cool = -1;
        while (w.Encounters.Count > 0)
        {
            Run(w, 5);
            if (w.Encounters.Count > 0)
                Assert.False(Ship(w, 1).OnRails || Ship(w, 2).OnRails);
            double d = Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position);
            if (cool < 0 && d > Settings.EncounterRangeM + Settings.EncounterMarginM && Vec3d.Dot(Ship(w, 1).Forward, Ship(w, 2).Position - Ship(w, 1).Position) < 0)
                cool = w.Time.Ms;
        }
        long closedAfter = w.Time.Ms - cool;
        output.WriteLine($"closed {closedAfter / 1000.0:F0} s after the sides were out of reach");
        Assert.InRange(closedAfter, Settings.EncounterCloseMs - 10_000, Settings.EncounterCloseMs + 10_000);
        Assert.True(Of(w, 1) == 0 && Of(w, 2) == 0);
        Run(w, 600);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails, "apart, the encounter closed: back on rails");
    }

    [Fact]
    public void Two_encounters_whose_ships_come_near_merge_into_the_older_and_part_again_as_a_new_one()
    {
        var (w, c) = Make();
        // two fights on one parallel, each a pair 30 km apart: the first lying stopped, the second 160 km east of it
        // and opened a minute later, its ships one astern of the other
        int a1 = SpawnFrom(w, c, 56, 3, 0, 0, 270, 0, 1);
        int b1 = SpawnFrom(w, c, 56, 3, 90, 30_000, 270, 0, 2);
        Run(w, 60);
        int a2 = SpawnFrom(w, c, 56, 3, 90, 160_000, 90, Kn, 1);
        int b2 = SpawnFrom(w, c, 56, 3, 90, 190_000, 90, Kn, 2);
        Run(w, 10);
        Assert.Equal(2, w.Encounters.Count);
        Assert.True(Of(w, a1) == 1 && Of(w, b1) == 1 && Of(w, a2) == 2 && Of(w, b2) == 2);

        // the second fight turns west and closes on the first: its ships come within the range and margin of the first's
        w.Apply(ShipSystem.Course(a2, 270));
        w.Apply(ShipSystem.Course(b2, 270));
        while (w.Encounters.Count == 2)
            Run(w, 5);
        Assert.Equal(1, w.Encounters.Count);
        Assert.Equal(1, w.Encounters.Open[0].Id);
        Assert.Equal(4, w.Encounters.Open[0].Ships);
        Assert.True(Of(w, a2) == 1 && Of(w, b2) == 1);
        double gap = Earth.Distance(Ship(w, b1).Position, Ship(w, a2).Position);
        output.WriteLine($"merged with the nearest ships {gap / 1000:F1} km apart");
        Assert.True(gap <= Settings.EncounterRangeM + Settings.EncounterMarginM + 1000, $"{gap:F0} m");

        // and parts again, both fights still hot: the first keeps its id, the second opens anew
        w.Apply(ShipSystem.Course(a2, 90));
        w.Apply(ShipSystem.Course(b2, 90));
        while (w.Encounters.Count == 1)
            Run(w, 5);
        Assert.Equal(2, w.Encounters.Count);
        Assert.True(Of(w, a1) == 1 && Of(w, b1) == 1, "the first fight kept its id");
        Assert.True(Of(w, a2) == 3 && Of(w, b2) == 3, "the second opened anew");
        Assert.Equal(3, w.Encounters.Opened);
    }

    [Fact]
    public void Peace_made_mid_fight_cools_the_encounter_and_it_closes()
    {
        var (w, c) = Make();
        SpawnFrom(w, c, 56, 3, 0, 0, 0, Kn, 1);
        SpawnFrom(w, c, 56, 3, 90, 20_000, 0, Kn, 2);
        Run(w, 10);
        Assert.Equal(1, w.Encounters.Count);
        w.Apply(Command.SetRelation(1, 2, Relation.Peace));
        long peace = w.Time.Ms;
        while (w.Encounters.Count > 0)
            Run(w, 5);
        Assert.InRange(w.Time.Ms - peace, Settings.EncounterCloseMs - 10_000, Settings.EncounterCloseMs + 10_000);
        Run(w, 600);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails, "at peace, the encounter closed: back on rails");
    }

    [Fact]
    public void Ships_of_one_side_near_each_other_open_none()
    {
        var (w, c) = Make();
        for (int i = 0; i < 6; i++)
            SpawnFrom(w, c, 56, 3, 90, i * 20_000, 0, Kn, 1);
        Run(w, 600);
        Assert.Equal(0, w.Encounters.Count);
        Assert.Equal(0, w.Encounters.Opened);
    }

    /// <summary>Two lines abreast of 100 ships each, 40 km apart in a single cluster, on the fine tick (all within
    /// reach): one encounter of all 200, updated without garbage and the same cut and replayed.</summary>
    [Fact]
    public void Two_hundred_ships_in_one_fight_are_one_encounter_without_garbage()
    {
        var (w, c) = Make();
        for (int i = 0; i < 100; i++)
        {
            SpawnFrom(w, c, 56, 3, 90, i * 400, 0, Kn, 1);
            SpawnFrom(w, c, 56.36, 3, 90, i * 400, 180, Kn, 2);
        }
        for (int e = 1; e <= 200; e++)
            w.Apply(ShipSystem.Rudder(e, 0));   // held amidships: never steady, on the fine tick throughout
        Run(w, 60);
        Assert.Equal(1, w.Encounters.Count);
        Assert.Equal(200, w.Encounters.Open[0].Ships);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(w, 600);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"10 minutes of 200 ships in one encounter: {allocated} bytes allocated");
        Assert.Equal(0, allocated);
        Assert.Equal(1, w.Encounters.Count);
    }
}
