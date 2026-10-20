using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The venting race (docs/battle/magazines.md V3; research/simulation/damage-research/10 §6): the research's
/// anchors on our own ships. Charges in bags alone sever a battlecruiser at her magazine (the BCF at Jutland, lids off);
/// in Clarkson cases her turret burns out (Lion's Q); brass burns out lower still (Seydlitz); a flooded magazine holds.</summary>
public class MagazineRaceTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>A shipped design built with every gun block packed so.</summary>
    static ShipClass Packed(string name, string? packaging)
    {
        var d = Design.Load(Path.Combine(Fleet.Shipgen, "designs", name + ".json"));
        if (packaging != null)
            d = d with { GunTech = d.GunTech?.ToDictionary(kv => kv.Key, kv => kv.Value with { Packaging = packaging }) };
        return ShipClassCompiler.Compile(ShipDesign.Build(d));
    }

    static int Room(ShipClass c, string id) => Enumerable.Range(0, c.Rooms.Length).Single(i => c.RoomId(i) == id);

    /// <summary>A magazine set off on a fresh ship of the class, a share of it flooded, in a sea (a share of a seaway's).</summary>
    RaceResult Race(ShipClass c, string magazine, double flooded = 0, double sea = 0, string? doctrine = null) =>
        Race(c, magazine, out _, flooded, sea, doctrine);

    RaceResult Race(ShipClass c, string magazine, out ShipCondition k, double flooded = 0, double sea = 0, string? doctrine = null)
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        int slot = w.Ships.Store.SlotOf(w.Ships.NewestEntity);
        k = w.Ships.Store.Combat.Condition(slot);
        if (doctrine != null)
            k.Flash = Magazines.Of(Settings, doctrine);
        int room = Room(c, magazine);
        foreach (int cell in c.Links(c.Rooms[room].Cells))
            k.WaterM3[cell] = flooded * c.Cells[cell].VolumeM3 * Math.Clamp(c.Cells[cell].Permeability, 0.05, 1);
        var rng = RandomStream.Named(1, "magazine race test");
        var r = Magazines.SetOff(c, k, room, w.Ships.Store.Combat.Mounts(slot), Settings, 0, ref rng, sea);
        output.WriteLine($"{c.Name} {magazine}: {r.Tier}, peak {r.PeakKPa:0} kPa, burned {r.BurnedKg / 1000:0.0} t of {r.Charges:0} charges, " +
                         $"girder {r.D:0.00} at {r.Station:0.00} (load {r.GirderLoad:0.00}), vented {r.Vented}, magazines {r.Magazines}");
        return r;
    }

    [Fact]
    public void Bags_alone_sever_a_battlecruiser_at_her_magazine()
    {
        var r = Race(Packed("lion", "bag"), "Magazine Q");
        Assert.True(r.Lost, "lids off: every bag lights at once and the gas wins (10 §6: tier 3 at ~1,000 kPa)");
        Assert.InRange(r.PeakKPa, 700, 3000);
        Assert.False(double.IsNaN(r.StructureS));
    }

    [Fact]
    public void Clarkson_cases_burn_her_turret_out()
    {
        var r = Race(Packed("lion", null), "Magazine Q");
        Assert.Equal(MagazineTier.BurnedOut, r.Tier);   // Lion's Q: the charges light one from the next, the trunk keeps up
        Assert.True(r.Vented >= 1);
        Assert.InRange(r.PeakKPa, 100, 700);
    }

    [Fact]
    public void Brass_cases_burn_out_lower_than_Clarkson_cases()
    {
        var clarkson = Race(Packed("lion", null), "Magazine Q");
        var brass = Race(Packed("lion", "brass"), "Magazine Q");
        Assert.False(brass.Lost);
        Assert.True(brass.PeakKPa < clarkson.PeakKPa / 1.5, "brass lights a charge from the next more slowly (Seydlitz, Gneisenau)");
        Assert.Equal(MagazineTier.BurnedOut, Race(Fleet.Get("seydlitz").Class!, "Magazine X").Tier);
    }

    [Fact]
    public void A_flooded_magazine_holds()
    {
        var c = Packed("lion", "bag");
        var dry = Race(c, "Magazine Q");
        var wet = Race(c, "Magazine Q", flooded: 0.95);
        Assert.False(wet.Lost, "flooded past 90% the bags left dry burn out through the trunk (10 §6.2)");
        Assert.True(wet.BurnedKg < 0.1 * dry.BurnedKg);
    }

    [Fact]
    public void A_magazine_across_the_bulkhead_lights_when_it_gives()
    {
        var r = Race(Packed("lion", "bag"), "Magazine A", sea: 1);
        Assert.True(r.Magazines >= 2, "Hood's 4\" to her 15\": the bulkhead gave and the next magazine lit");
        Assert.Equal(MagazineTier.TwoGroups, r.Tier);   // forward of amidships: broken in a seaway (V4), breached in calm water
    }

    /// <summary>V4 (07 §3.1): amidships the girder breaks in calm water; a quarter-length magazine breaks her only in a
    /// seaway, and in calm water leaves her breached (her deck and sides gone at the magazine, open to the sea).</summary>
    [Fact]
    public void The_girder_breaks_amidships_in_calm_water_and_toward_the_ends_in_a_seaway()
    {
        var c = Packed("lion", "bag");
        Assert.True(Race(c, "Magazine Q").Lost);
        var calm = Race(c, "Magazine Y", out var k);
        Assert.Equal(MagazineTier.Breached, calm.Tier);
        Assert.True(k.BreachM2.Sum() > 10, "her sides gone at the magazine: the sea in");
        Assert.True(Race(c, "Magazine Y", sea: 1).Lost);
    }

    /// <summary>V4: a magazine near an end takes the end off and leaves her afloat (New Orleans), everything beyond it open
    /// to the sea.</summary>
    [Fact]
    public void An_end_magazine_takes_her_end_off()
    {
        var c = Packed("gangut", "bag");
        var r = Race(c, "Magazine Y", out var k);
        Assert.True(r.EndLost);
        Assert.False(r.Lost);
        var mag = c.Rooms[Room(c, "Magazine Y")];
        int aft = Enumerable.Range(0, c.Cells.Length).Count(i => c.Cells[i].Box.X1 <= mag.X1 && k.BreachM2[i] > 0);
        Assert.True(aft > 5, $"the stern beyond the magazine open to the sea ({aft} spaces)");
    }

    /// <summary>V5: the BCF's rate first (doors propped, lids off) severs Lion where her drill burns her turret out; and
    /// the flame from her gunhouse always reaches the magazine.</summary>
    [Fact]
    public void Rate_first_props_the_doors_and_takes_the_lids_off()
    {
        var c = Fleet.Get("lion").Class!;
        Assert.Equal(MagazineTier.BurnedOut, Race(c, "Magazine Q", doctrine: "1916").Tier);
        Assert.True(Race(c, "Magazine Q", doctrine: "1916 BCF").Lost);

        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        int q = Enumerable.Range(0, c.Mounts.Length).First(m => c.ComponentId(c.Mounts[m].Component) == "Q");
        var rng = RandomStream.Named(1, "rate first");
        k.Flash = Magazines.Of(Settings, "1916 BCF");
        for (int i = 0; i < 20; i++)
        {
            var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
            Assert.Equal(c.Mounts[q].Magazine, Magazines.Light(c, k, q, 0, false, Settings, ref rng, ref res));
        }
    }

    /// <summary>V5: the discipline's hoists (fire-control F6d's supply): rate first brings the rounds faster than drill
    /// once the ready-use rounds are fired.</summary>
    [Fact]
    public void Rate_first_hurries_the_hoists()
    {
        var c = Fleet.Get("lion").Class!;
        long Last(double factor)
        {
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
            w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
            int slot = w.Ships.Store.SlotOf(w.Ships.NewestEntity);
            int m = Enumerable.Range(0, c.Mounts.Length).First(i => c.Mounts[i].Kind == ComponentKind.Main);
            int guns = c.TurretTypes[c.Mounts[m].Type].Barrels;
            long ms = 0;
            for (int i = 0; i < 30; i++)
            {
                w.Ships.Store.Combat.Fired(slot, c, m, 0, guns, ms, 1, factor);
                ms = w.Ships.Store.Combat.Mount(slot, m).LoadedMs;
            }
            return ms;
        }
        double drill = Last(1), rate = Last(Magazines.Of(Settings, "1916 BCF").HoistShare / DesignerPlaceholders.HoistShare);
        output.WriteLine($"30 salvos: drill {drill / 1000:0} s, rate first {rate / 1000:0} s");
        Assert.True(rate < 0.85 * drill);
    }

    [Fact]
    public void The_race_allocates_nothing()
    {
        var c = Packed("lion", null);
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        int slot = w.Ships.Store.SlotOf(w.Ships.NewestEntity);
        var k = w.Ships.Store.Combat.Condition(slot);
        int room = Room(c, "Magazine Q");
        var rng = RandomStream.Named(1, "magazine race alloc");
        Magazines.SetOff(c, k, room, w.Ships.Store.Combat.Mounts(slot), Settings, 0, ref rng);   // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        Magazines.SetOff(c, k, room, w.Ships.Store.Combat.Mounts(slot), Settings, 0, ref rng);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
