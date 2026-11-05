using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The venting race (docs/battle/magazines.md V3; research/simulation/damage-research/10 §6): the research's
/// anchors on our own ships. Charges in bags alone sever a battlecruiser at her magazine (the BCF at Jutland, lids off);
/// in Clarkson cases, lids on, a magazine alight takes her deck at least (magazines.md "Too safe"); brass burns out far
/// lower (Seydlitz); a flooded magazine holds.</summary>
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

    /// <summary>A scenario's design (Content/Scenarios/Designs), compiled.</summary>
    static ShipClass Scenario(string name) => ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "Designs", name + ".json"))));

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
    public void Clarkson_cases_alight_beat_her_trunk()
    {
        var r = Race(Packed("lion", null), "Magazine Q");
        Assert.True(r.Tier >= MagazineTier.Breached, "lids on, the record's dry cordite magazines still blew: the trunk doesn't keep up");
        Assert.True(r.Vented >= 1);
        Assert.InRange(r.PeakKPa, 700, 1500);
    }

    [Fact]
    public void The_propellant_decides_whether_she_burns_out()
    {
        Assert.True(Race(Packed("lion", "brass"), "Magazine Q").Lost, "hot propellant goes in brass as in Clarkson cases (Pommern, not Gneisenau)");
        Assert.Equal(MagazineTier.BurnedOut, Race(Fleet.Get("bismarck").Class!, "Magazine A").Tier);   // cool, in brass: Gneisenau
        Assert.False(Race(Fleet.Get("colorado").Class!, "Magazine A").Lost);   // stabilised, in tanks: Boise
        Assert.True(Race(Fleet.Get("seydlitz").Class!, "Magazine X").Tier >= MagazineTier.Breached);   // RP C/12, in brass, dry
    }

    /// <summary>"Too safe": a magazine's boundaries give at what its own steel gives at, and what they open goes with their
    /// area: a destroyer's isn't a battleship's, and a magazine alight doesn't sit "contained" in her.</summary>
    [Fact]
    public void A_thin_hull_gives_at_what_a_thin_hull_gives_at()
    {
        double[] Give(ShipClass c, string magazine)
        {
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
            w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
            var kpa = new double[4];
            Magazines.GiveKPa(c, w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity)), Room(c, magazine), Settings, kpa);
            return kpa;
        }
        var mClass = Scenario("m_class");
        var (lion, boat) = (Give(Fleet.Get("lion").Class!, "Magazine A"), Give(mClass, "Magazine B"));
        for (int b = 0; b < 4; b++)
            Assert.True(boat[b] < lion[b] / 1.5, $"boundary {b}: {boat[b]:0} kPa against {lion[b]:0}");
        Assert.InRange(lion[0], 200, 300);   // the research's table, at Lion's own steel
        Assert.InRange(lion[1], 600, 800);
        Assert.InRange(lion[2], 900, 1100);

        var r = Race(mClass, "Magazine B");
        Assert.True(r.D > 0, $"a boat's magazine alight opens her up (peak {r.PeakKPa:0} kPa)");
        var rc = Race(Scenario("arethusa"), "Magazine A");
        Assert.True(rc.D > 0, $"a light cruiser's too (peak {rc.PeakKPa:0} kPa)");
    }

    /// <summary>A side torn under the waterline is no way out for the gas until the deck has opened one up: Lion's Q has
    /// the thinnest side of her magazines, and it goes first, and she's still broken at Q, not saved by it.</summary>
    [Fact]
    public void A_torn_side_under_water_doesnt_save_her()
    {
        var r = Race(Packed("lion", null), "Magazine Q");
        Assert.True(r.Lost);
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

    /// <summary>V5: the BCF's rate first (doors propped, lids off) burns harder than drill, and the flame from her
    /// gunhouse always reaches the magazine; on drill only one time in ten does (below): that's where they part.</summary>
    [Fact]
    public void Rate_first_props_the_doors_and_takes_the_lids_off()
    {
        var c = Fleet.Get("lion").Class!;
        var (drill, rate) = (Race(c, "Magazine Q", doctrine: "1916"), Race(c, "Magazine Q", doctrine: "1916 BCF"));
        Assert.True(rate.Lost);
        Assert.True(rate.PeakKPa >= drill.PeakKPa);

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

    /// <summary>V6, Lion's Q (Harvey's order): a flash in the train that doesn't reach the magazine orders it flooded; a
    /// quarter of an hour on, the magazine is full of water, and charges set off there later are contained.</summary>
    [Fact]
    public void A_flash_in_the_train_floods_its_magazine_in_time()
    {
        var c = Packed("lion", "bag");   // lids off: dry, it would sever her
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        int slot = w.Ships.Store.SlotOf(w.Ships.NewestEntity);
        var k = w.Ships.Store.Combat.Condition(slot);
        k.Flash = Magazines.Of(Settings, "1942");   // safe: the flash stops at the shut doors
        int q = Enumerable.Range(0, c.Mounts.Length).First(m => c.ComponentId(c.Mounts[m].Component) == "Q");
        var rng = RandomStream.Named(1, "harvey");
        var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
        Assert.Equal(-1, Magazines.Light(c, k, q, 0, false, Settings, ref rng, ref res, w.Time.Ms));
        int mag = c.Mounts[q].Magazine;
        Assert.True(k.MagazineOrderMs[mag] > w.Time.Ms, "the order given, the valves a walk away");
        w.Damage.Settle(w, w.Ships.NewestEntity, 27 * 60);   // the longest walk and the rule's 20 min to fill
        Assert.True(k.MagazineFlood[mag]);
        var r = Magazines.SetOff(c, k, mag, w.Ships.Store.Combat.Mounts(slot), Settings, w.Time.Ms, ref rng);
        output.WriteLine($"flooded: {r.Tier}, peak {r.PeakKPa:0} kPa, burned {r.BurnedKg / 1000:0.0} t");
        Assert.False(r.Lost);
        Assert.True(r.BurnedKg < 3000);
    }

    /// <summary>A side's stupidity (docs/battle/magazines.md): reads between the flash disciplines, over every ship's doctrine;
    /// its stack lies where her train has room and makes a hit likelier to light it and a bigger fire when it does.</summary>
    [Fact]
    public void Stupidity_reads_between_the_disciplines_and_a_stack_makes_a_bigger_fire()
    {
        var mid = Magazines.At(Settings, 0.75);
        Assert.Equal(1.25, mid.HoistShare, 6);
        Assert.Equal(0.15, mid.StackShare, 6);
        Assert.Equal(0.55, mid.DoorOpen[(int)StationKind.Handling], 6);
        Assert.Equal(Magazines.Of(Settings, "1916").HoistShare, Magazines.At(Settings, 0.5).HoistShare, 6);
        Assert.Equal(Magazines.Of(Settings, "1916 BCF").StackShare, Magazines.At(Settings, 1).StackShare, 6);

        var c = Packed("lion", "bag");
        int q = Enumerable.Range(0, c.Mounts.Length).First(m => c.ComponentId(c.Mounts[m].Component) == "Q");
        var rate = Magazines.At(Settings, 1);
        Assert.Equal(0, Magazines.Stacked(c, q, StationKind.Handling, Magazines.At(Settings, 0.5)));   // drill stacks none
        Assert.Equal(0, Magazines.Stacked(c, q, StationKind.Gunhouse, rate));   // nor in the gunhouse
        double each = Magazines.Stacked(c, q, StationKind.Handling, rate);
        Assert.True(each > 0);
        var train = c.Train(q);
        int places = 0;
        foreach (ref readonly var st in train)
            places += st.Kind is StationKind.Chamber or StationKind.Handling or StationKind.Passage ? 1 : 0;
        Assert.Equal(0.3 * c.Mounts[q].Rounds, each * places, 6);   // the share of her rounds, all told

        // a side's stupidity over a ship's own doctrine, and back
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0, side: 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        Assert.Equal(1.5, w.Damage.FlashOf(0, k).HoistShare, 6);
        w.Apply(DamageSystem.SetStupidity(0, 1));
        Assert.Equal(1.0, w.Damage.FlashOf(0, k).HoistShare, 6);
        Assert.Equal(1.5, w.Damage.FlashOf(1, k).HoistShare, 6);   // the other side's unchanged
        w.Apply(DamageSystem.SetStupidity(0, -1));
        Assert.Equal(1.5, w.Damage.FlashOf(0, k).HoistShare, 6);
    }

    /// <summary>What the player is told follows the water: a flash in the turret gives the order, the valves open a walk
    /// later, and the magazine fills over minutes. An order is never reported as a flooded magazine.</summary>
    [Fact]
    public void The_detail_tells_a_flooding_ordered_from_a_magazine_flooded()
    {
        var c = Packed("lion", "bag");
        int q = Enumerable.Range(0, c.Mounts.Length).First(m => c.ComponentId(c.Mounts[m].Component) == "Q");

        // Settle runs from the world's clock, so each moment is its own ship, flashed and settled once
        double ShareAfter(int seconds)
        {
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
            w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
            int entity = w.Ships.NewestEntity;
            var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(entity));
            k.Flash = Magazines.Of(Settings, "1942");   // safe: the flash stops at the shut doors
            var rng = RandomStream.Named(1, "harvey");
            var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
            Assert.Equal(-1, Magazines.Light(c, k, q, 0, false, Settings, ref rng, ref res, w.Time.Ms));
            if (seconds > 0)
                w.Damage.Settle(w, entity, seconds);
            var snap = new Snapshot();
            (snap.DetailWanted[0], snap.DetailWantedCount) = (entity, 1);
            w.Fill(snap, truth: true);
            var d = snap.Details;
            int r = d.IndexOf(entity);
            Assert.Equal(1, d.MagazineCount[r]);
            double share = d.MagazineShare[d.MagazineStart[r]];
            output.WriteLine($"{seconds,4} s after the flash: {share:0.00}");
            return share;
        }

        Assert.Equal(-1, ShareAfter(0));   // ordered this instant: no water
        Assert.Equal(-1, ShareAfter(20));   // the shortest walk to the valves is 30 s
        foreach (int s in (int[])[150, 300, 600, 900, 1200])
            ShareAfter(s);
        Assert.InRange(ShareAfter(1600), 0.995, 1);   // the walk, at most 360 s, and 1,200 s to fill
    }

    /// <summary>The fill against the falling head (research 11 §2.3): the rule's time to full, the second half longer than
    /// the first by the magazine's depth under the sea, and no more than its cells hold.</summary>
    [Fact]
    public void A_magazine_fills_in_the_rules_time_slowing_as_it_rises()
    {
        var c = Packed("lion", "bag");
        int q = Enumerable.Range(0, c.Mounts.Length).First(m => c.ComponentId(c.Mounts[m].Component) == "Q");
        int mag = c.Mounts[q].Magazine;
        var f = new FloodModel(c);
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        var cells = c.Links(c.Rooms[mag].Cells);
        double cap = 0;
        foreach (int cell in cells)
            cap += f.Capacity[cell];
        var rng = RandomStream.Named(1, "fill");
        Magazines.Order(k, mag, 1);
        double half = 0, full = 0;
        for (int t = 1; t <= 3000 && full == 0; t++)
        {
            Magazines.Valves(c, f, k, 1, t * 1000L, Settings, ref rng);
            double held = 0;
            foreach (int cell in cells)
                held += k.WaterM3[cell];
            if (half == 0 && held >= cap / 2)
                half = t;
            if (held >= 0.999 * cap)
                full = t;
        }
        output.WriteLine($"Q's magazine, {cap:0} m3: half at {half / 60:0.0} min, full at {full / 60:0.0} min, {full / half:0.00} x");
        Assert.InRange(full, Settings.FireMagazineFloodS * 0.95, Settings.FireMagazineFloodS * 1.05);
        Assert.InRange(full / half, 2.05, 2.5);
    }

    /// <summary>V6: a magazine with sprinklers (a WWII outfit) has its stow wetted within a minute of the order.</summary>
    [Fact]
    public void Sprinklers_wet_the_stow_within_a_minute()
    {
        var c = Fleet.Get("battleship").Class!;
        Assert.True(c.MagazineSprinklers);
        Assert.False(Fleet.Get("lion").Class!.MagazineSprinklers);
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        int mag = Enumerable.Range(0, c.Rooms.Length).First(i => c.Rooms[i].Kind == RoomKind.Magazine);
        Magazines.Order(k, mag, w.Time.Ms + 120_000);
        w.Damage.Settle(w, w.Ships.NewestEntity, 70);
        Assert.True(k.MagazineWet[mag] >= Settings.MagazineSprinkleShare - 1e-9, $"wet {k.MagazineWet[mag]:0.00}");
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

    /// <summary>A ship's own doctrine over her side's (magazines.md "Too safe": the BCF's squadrons at Jutland): her flash
    /// discipline goes by it, and her sister's stays her side's.</summary>
    [Fact]
    public void A_ships_own_doctrine_stands_over_her_sides()
    {
        var c = Fleet.Get("lion").Class!;
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        int cls = w.Ships.AddClass(c);
        w.Apply(ShipSystem.Spawn(cls, 56, 4, 0, 0));
        int own = w.Ships.NewestEntity;
        w.Apply(ShipSystem.Spawn(cls, 56, 4.2, 0, 0));
        int sister = w.Ships.NewestEntity;
        w.Apply(DamageSystem.SetShipDamageControl(own, Array.FindIndex(Settings.DamageControl, d => d.Name == "1916 BCF")));
        var (a, b) = (w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(own)), w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(sister)));
        Assert.Equal("rate first", w.Damage.FlashOf(0, a).Name);
        Assert.Equal("drill", w.Damage.FlashOf(0, b).Name);
    }

    /// <summary>"Ammunition passages": a gun far from its magazine has a passage in its train, from the geometry alone; one
    /// over her magazine has none. It owns no cell and shares those it runs through.</summary>
    [Fact]
    public void A_gun_far_from_its_magazine_has_a_passage()
    {
        var c = Fleet.Get("lion").Class!;
        bool any = false;
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            var train = c.Train(m);
            if (train.Length == 0)
                continue;
            ref readonly var mag = ref c.Rooms[c.Mounts[m].Magazine];
            ref readonly var at = ref c.Components[c.Mounts[m].Component];
            double x = (at.Bounds.X0 + at.Bounds.X1) / 2, r = (at.Bounds.X1 - at.Bounds.X0) / 2;
            bool has = train.ToArray().Any(st => st.Kind == StationKind.Passage);
            any |= has;
            if (x > mag.X0 - r / 2 && x < mag.X1 + r / 2)
                Assert.False(has, $"mount {m} stands over its magazine");
        }
        Assert.True(any);
        for (int room = 0; room < c.Rooms.Length; room++)
            if (c.Rooms[room].Kind == RoomKind.Passage)
            {
                Assert.True(c.Rooms[room].Shared);
                Assert.Equal(0, c.Rooms[room].VolumeM3);
                Assert.InRange(Magazines.Exposed(c, room), 0.02, 0.5);   // a passage is a small part of the spaces it runs through
            }
    }

    /// <summary>"Ammunition passages": Defence's chain, a magazine alight lights others along the passage when the gas breaks
    /// through; a magazine on no passage stands alone unless a bulkhead joins it.</summary>
    [Fact]
    public void Magazines_light_along_their_passage()
    {
        var c = Scenario("minotaur");
        var groups = Enumerable.Range(0, c.Rooms.Length).Where(r => c.Rooms[r].Kind == RoomKind.Magazine && c.PassageGroup(r) >= 0).ToList();
        Assert.True(groups.Count >= 2);
        var r = Race(c, c.RoomId(groups[0]));
        Assert.True(r.Magazines >= 2, $"{r.Magazines} alight");
    }

    /// <summary>A casemate's flame goes down her hoist into the passage (Kent) a fifth of the time on drill, and from there
    /// through the magazine's door one time in ten.</summary>
    [Fact]
    public void A_casemate_reaches_her_magazine_through_hoist_and_door()
    {
        var c = Scenario("iron_duke");
        int m = Enumerable.Range(0, c.Mounts.Length).First(i => c.Mounts[i].Casemate && c.Train(i).Length > 0);
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        k.Flash = Magazines.Of(Settings, "1916");
        var rng = RandomStream.Named(1, "casemate door test");
        int reached = 0, runs = 8000;
        for (int i = 0; i < runs; i++)
        {
            var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
            reached += Magazines.Light(c, k, m, 0, false, Settings, ref rng, ref res) >= 0 ? 1 : 0;
        }
        Assert.InRange((double)reached / runs, 0.01, 0.035);
    }

    /// <summary>A wing turret sharing a magazine has no handling room; the door into the magazine is still the magazine's,
    /// open about one time in ten on drill, not her working chamber's.</summary>
    [Fact]
    public void A_wing_turret_still_has_the_magazines_door()
    {
        var c = Fleet.Get("nassau").Class!;
        int wing = Enumerable.Range(0, c.Mounts.Length).First(m => c.Train(m).Length == 3 && c.Train(m)[1].Kind == StationKind.Chamber && c.Train(m)[2].Kind == StationKind.Magazine);
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        k.Flash = Magazines.Of(Settings, "1916");
        var rng = RandomStream.Named(1, "wing door test");
        int reached = 0, runs = 4000;
        for (int i = 0; i < runs; i++)
        {
            var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
            reached += Magazines.Light(c, k, wing, 0, false, Settings, ref rng, ref res) >= 0 ? 1 : 0;
        }
        Assert.InRange((double)reached / runs, 0.07, 0.13);
    }

    /// <summary>"Too safe": on drill the flash doors in the train don't stop a flame (the record's ran the train), and the
    /// magazine's own door does when it's shut, about 9 in 10.</summary>
    [Fact]
    public void On_drill_a_flame_runs_the_train_to_the_magazines_door()
    {
        var c = Fleet.Get("lion").Class!;
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        k.Flash = Magazines.Of(Settings, "1916");
        int q = Enumerable.Range(0, c.Mounts.Length).First(m => c.ComponentId(c.Mounts[m].Component) == "Q");
        var rng = RandomStream.Named(1, "drill train test");
        int reached = 0, runs = 4000, stations = 0;
        for (int i = 0; i < runs; i++)
        {
            var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
            reached += Magazines.Light(c, k, q, 0, false, Settings, ref rng, ref res) >= 0 ? 1 : 0;
            stations += res.Stations;
        }
        Assert.InRange((double)reached / runs, 0.07, 0.13);
        Assert.InRange((double)stations / runs, 3, 4.2);   // gunhouse, chamber, handling room, and the magazine one time in ten
    }
}
