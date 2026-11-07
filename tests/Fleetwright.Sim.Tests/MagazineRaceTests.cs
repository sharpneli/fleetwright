using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A magazine alight through the gas network and the girder (docs/battle/explosions.md EX2, magazines.md V3; research/
/// simulation/damage-research/10 §6): what the physics gives on our own ships, nothing fitted. Bags alone break a battlecruiser
/// in a seaway, and in calm water leave her afloat by a little (explosions.md's first open item); cases open her up and she holds;
/// Seydlitz's brass burns out with her afloat; a flooded magazine holds.</summary>
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

    [Fact]
    public void What_else_drove_her_is_read_with_the_magazine()
    {
        var c = Fleet.Get("lion").Class!;
        double alone = Race(c, "Magazine Q").GirderLoad;
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        int slot = w.Ships.Store.SlotOf(w.Ships.NewestEntity);
        var k = w.Ships.Store.Combat.Condition(slot);
        var rng = RandomStream.Named(1, "magazine race test");
        var r = Magazines.SetOff(c, k, Room(c, "Magazine Q"), w.Ships.Store.Combat.Mounts(slot), Settings, 0, ref rng, 0, 1e13);
        output.WriteLine($"load {alone:0.000} alone, {r.GirderLoad:0.000} with a shell's whip");
        Assert.True(r.GirderLoad > alone, "one moment, one reading of her girder");
    }

    [Fact]
    public void What_a_magazine_wrecks_it_reports()
    {
        var r = Race(Fleet.Get("lion").Class!, "Magazine Q");
        Assert.True(r.Destroyed >= 1, "its own room, at least");
    }

    [Fact]
    public void A_magazine_a_fire_set_off_wakes_the_water()
    {
        var nobody = Settings.DamageControl.Single(d => d.Name == "1890") with { Name = "nobody", WatchS = -1, PartyShare = 0, TurnTo = 0, FloodMagazineS = -1 };
        var settings = Settings with { FireMagazineS = [60, 60, 60, 60, 60], DamageControl = [.. Settings.DamageControl, nobody], DamageControlDefault = "nobody", GirderSeawayMs = 1e9 };
        var c = Fleet.Get("dreadnought").Class!;
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        int entity = w.Ships.NewestEntity;
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(entity));
        int next = Enumerable.Range(0, c.Cells.Length).First(i => c.FireSpaces[i].Use != FireUse.None &&
            c.Neighbours(c.Cells[i].Neighbours).ToArray().Any(n => c.Rooms[c.Cells[n.Cell].Room].Kind == RoomKind.Magazine));
        w.Apply(DamageSystem.BurnCell(entity, next));
        for (int sec = 0; sec < 1800 && !k.MagazineSpent.AsSpan().Contains(true); sec++)
            w.AdvanceTo(w.Time + 1000);
        Assert.Contains(true, k.MagazineSpent);
        output.WriteLine($"{k.Status}, the water due in {k.FloodDueMs - w.Time.Ms} ms");
        Assert.Equal(ShipStatus.Afloat, k.Status);
        Assert.True(k.FloodDueMs - w.Time.Ms <= settings.FloodMs, "what it opened takes the sea from her next second");
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
                         $"section lost {r.D:0.00} at {r.Station:0.00} (load {r.GirderLoad:0.00}), vented {r.Vented}, magazines {r.Magazines}, gave {r.Gas.Bulkheads} bulkheads, {r.Gas.Decks} decks, {r.Gas.Sides} sides, {r.Gas.Bottoms} bottom");
        return r;
    }

    /// <summary>Lids off: every bag lights at once and the gas wins (10 §6: tier 3 at ~1,000 kPa). On her own plating it takes a
    /// seaway to finish her: explosions.md's first open item, and not tuned.</summary>
    [Fact]
    public void Bags_alone_break_a_battlecruiser_in_a_seaway()
    {
        var c = Packed("lion", "bag");
        var r = Race(c, "Magazine Q", sea: 1);
        Assert.True(r.Lost);
        Assert.InRange(r.PeakKPa, 700, 3000);
        Assert.False(double.IsNaN(r.StructureS));
        var calm = Race(c, "Magazine Q");
        Assert.False(calm.Lost);
        Assert.InRange(calm.GirderLoad, 0.6, 1);
    }

    [Fact]
    public void Magazine_cases_alight_beat_her_trunk()
    {
        var r = Race(Packed("lion", null), "Magazine Q");
        Assert.True(r.Tier >= MagazineTier.Breached, "lids on, the trunk doesn't keep up: her plating gives");
        Assert.True(r.Vented >= 1);
        Assert.InRange(r.PeakKPa, 300, 1500);
    }

    /// <summary>What's in the stow decides how far it goes, on one plate and one gas: bags break her and cases don't, a stow
    /// of cool propellant in brass vents and holds. Magazine cases and brass read alike on cordite (explosions.md's third open item).</summary>
    [Fact]
    public void The_propellant_and_its_packing_decide_what_she_lives_through()
    {
        var bags = Race(Packed("lion", "bag"), "Magazine Q", sea: 1);
        var brass = Race(Packed("lion", "brass"), "Magazine Q", sea: 1);
        Assert.True(bags.Lost);
        Assert.False(brass.Lost);
        Assert.True(brass.PeakKPa < bags.PeakKPa);
        var bismarck = Race(Fleet.Get("bismarck").Class!, "Magazine A");   // cool, in brass: Gneisenau
        Assert.False(bismarck.Lost);
        Assert.True(bismarck.Vented >= 1);
        Assert.False(Race(Fleet.Get("colorado").Class!, "Magazine A").Lost);   // stabilised, in tanks: Boise
        var seydlitz = Race(Fleet.Get("seydlitz").Class!, "Magazine X");   // RP C/12, in brass, dry
        Assert.False(seydlitz.Lost, "burns out and fights on, as the record has her");
        Assert.True(seydlitz.Vented >= 1);
    }

    /// <summary>A side torn under the waterline is no way out for the gas (07 §2.4), but the sea's way in: Lion's Q opens her side
    /// and it doesn't let her off.</summary>
    [Fact]
    public void A_torn_side_under_water_lets_the_sea_in()
    {
        var r = Race(Packed("lion", null), "Magazine Q", out var k);
        Assert.True(r.Gas.Sides > 0);
        Assert.True(k.BreachM2.Sum() > 0);
        Assert.True(r.Tier >= MagazineTier.Breached);
    }

    /// <summary>The answer doesn't hang on the solver's step (explosions.md EX2: a magazine's room the right size left the first
    /// solver reading half the pressure): the game's against one eight times finer.</summary>
    [Theory]
    [InlineData("bag")]
    [InlineData(null)]
    public void What_a_magazine_does_doesnt_hang_on_the_step(string? packing)
    {
        var c = Packed("lion", packing);
        var game = Race(c, "Magazine Q", sea: 1);
        RaceResult fine;
        GasNetwork.StepK = 1.0 / 8;
        try
        {
            fine = Race(c, "Magazine Q", sea: 1);
        }
        finally
        {
            GasNetwork.StepK = 1;
        }
        Assert.InRange(game.PeakKPa / fine.PeakKPa, 0.85, 1.15);
        Assert.InRange(game.GirderLoad / fine.GirderLoad, 0.9, 1.1);
    }

    /// <summary>What a magazine held burns once: a second set-off on a ship that lived through the first has nothing to burn.</summary>
    [Fact]
    public void A_magazine_goes_up_once()
    {
        var c = Fleet.Get("seydlitz").Class!;
        var first = Race(c, "Magazine X", out var k);
        Assert.True(first.BurnedKg > 0);
        Assert.False(first.Lost);
        var rng = RandomStream.Named(1, "once");
        var again = Magazines.SetOff(c, k, Room(c, "Magazine X"), default, Settings, 1000, ref rng);
        Assert.Equal(0, again.BurnedKg);
        Assert.False(again.Lost);
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

    /// <summary>A magazine room with no charge in it (a carrier's bombs aren't in the model yet) has nothing to go up: she's not lost
    /// by it, nothing in her is touched, and it doesn't come up again.</summary>
    [Fact]
    public void A_magazine_with_nothing_to_burn_does_nothing()
    {
        var c = Fleet.Get("fleet_carrier").Class!;
        int room = Enumerable.Range(0, c.Rooms.Length).First(i => c.Rooms[i].Kind == RoomKind.Magazine &&
            !Enumerable.Range(0, c.Mounts.Length).Any(m => c.Mounts[m].Magazine == i && c.Mounts[m].Battery >= 0));
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        int slot = w.Ships.Store.SlotOf(w.Ships.NewestEntity);
        var k = w.Ships.Store.Combat.Condition(slot);
        var rng = RandomStream.Named(1, "empty");
        var r = Magazines.SetOff(c, k, room, w.Ships.Store.Combat.Mounts(slot), Settings, 0, ref rng, 1);
        Assert.False(r.Lost, $"{r.Tier}");
        Assert.Equal(0, r.Magazines);
        Assert.True(k.MagazineSpent[room]);
        Assert.Equal(PartState.Ok, k.State[c.RoomNode(room)]);
    }

    /// <summary>A magazine's bulkhead giving lights its passages from whichever side it's worked: an edge goes from its lower cell, so
    /// otherwise it hung on how her cells happen to be numbered. Seydlitz's X has such a bulkhead: 4 magazines lit when it was read from
    /// one side only, all of them now.</summary>
    [Fact]
    public void A_bulkhead_giving_lights_the_passages_however_her_cells_are_numbered()
    {
        var r = Race(Fleet.Get("seydlitz").Class!, "Magazine X");
        Assert.True(r.Magazines > 4, $"{r.Magazines} magazines lit");
    }

    /// <summary>A flame running through a ship's magazines doesn't stop at a number: Seydlitz has nine.</summary>
    [Fact]
    public void Every_magazine_a_ship_has_can_light()
    {
        var c = Fleet.Get("seydlitz").Class!;
        int rooms = Enumerable.Range(0, c.Rooms.Length).Count(i => c.Rooms[i].Kind == RoomKind.Magazine), most = 0;
        for (int room = 0; room < c.Rooms.Length; room++)
            if (c.Rooms[room].Kind == RoomKind.Magazine)
                most = Math.Max(most, Race(c, c.RoomId(room)).Magazines);
        Assert.Equal(9, rooms);
        Assert.True(most > 8, $"at most {most} of her {rooms} lit");
    }

    /// <summary>What a run did on this thread before says nothing of the next: a stow lit part way through a step has put nothing in
    /// yet, whatever the last run's stow in that slot was making.</summary>
    [Fact]
    public void A_run_doesnt_hang_on_the_ones_before_it()
    {
        var c = Fleet.Get("seydlitz").Class!;
        var rooms = Enumerable.Range(0, c.Rooms.Length).Where(i => c.Rooms[i].Kind == RoomKind.Magazine).ToArray();
        GasResult Run(int room)
        {
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
            w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
            var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
            var rng = RandomStream.Named(7, "history");
            return GasNetwork.For(c).Burn(k, room, [], Settings, ref rng);
        }
        foreach (int room in rooms)
        {
            GasResult fresh = default, after = default;
            var first = new Thread(() => fresh = Run(room));
            first.Start();
            first.Join();
            var second = new Thread(() =>
            {
                foreach (int other in rooms)
                    Run(other);
                after = Run(room);
            });
            second.Start();
            second.Join();
            Assert.Equal(fresh.Steps, after.Steps);
            Assert.Equal(fresh.PeakKPa, after.PeakKPa);
            Assert.Equal(fresh.BurnedKg, after.BurnedKg);
        }
    }

    /// <summary>A shield's or casemate's hoist lets the gas out but vents no gunhouse: there's none to burn out, and a fire whose only
    /// way up was a hoist is still contained.</summary>
    [Fact]
    public void A_hoist_vents_no_gunhouse()
    {
        var c = Fleet.Get("pennsylvania").Class!;
        int tested = 0;
        for (int room = 0; room < c.Rooms.Length; room++)
        {
            if (c.Rooms[room].Kind != RoomKind.Magazine || !Enumerable.Range(0, c.Mounts.Length).Any(m => c.Mounts[m].Magazine == room && c.Mounts[m].Battery >= 0 && c.LiftKPa(m) <= 0))
                continue;
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, Settings);
            w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
            var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
            var rng = RandomStream.Named(1, "hoist");
            var net = GasNetwork.For(c);
            var gas = net.Burn(k, room, [], Settings, ref rng);
            output.WriteLine($"{c.RoomId(room)}: peak {gas.PeakKPa:0} kPa, {gas.Magazines} magazines, {gas.Vented} vented");
            for (int m = 0; m < c.Mounts.Length; m++)
                if (c.LiftKPa(m) <= 0)
                    Assert.False(net.VentedMount(m), $"mount {m}, with no gunhouse to lift, counted as vented");
            tested++;
        }
        Assert.True(tested > 0);
    }

    /// <summary>07 §3.1, on her own section: forward, where the gas takes the thin plate and the ship is narrow, she breaks in a
    /// seaway, the sea's moment on what's left of her more than the calm's; aft her charges are fewer and the section holds, breached
    /// and open to the sea. (Whether forward breaks her in calm water too sits at the margin, 0.99-1.00 of her girder: not pinned.)</summary>
    [Fact]
    public void The_sea_decides_at_the_margin_and_an_aft_magazine_leaves_her_breached()
    {
        var c = Packed("lion", "bag");
        var (seaway, calm) = (Race(c, "Magazine B", sea: 1), Race(c, "Magazine B"));
        Assert.True(seaway.Lost);
        Assert.True(seaway.GirderLoad > calm.GirderLoad, $"{seaway.GirderLoad:0.00} in a seaway against {calm.GirderLoad:0.00} calm");
        var aft = Race(c, "Magazine Y", out var k, sea: 1);
        Assert.False(aft.Lost);
        Assert.Equal(MagazineTier.Breached, aft.Tier);
        Assert.True(k.BreachM2.Sum() > 0, "the sea in");
    }

    /// <summary>V5: the BCF's rate first (doors propped, lids off) burns harder than drill, loading her girder at least as hard, and
    /// loses her; and the flame from her gunhouse always reaches the magazine, on drill only one time in ten (below): that's where
    /// they part. (Whether drill's own race loses her in a seaway sits at the margin of her girder: not pinned, explosions.md EX6a.)</summary>
    [Fact]
    public void Rate_first_props_the_doors_and_takes_the_lids_off()
    {
        var c = Fleet.Get("lion").Class!;
        var (drill, rate) = (Race(c, "Magazine Q", doctrine: "1916", sea: 1), Race(c, "Magazine Q", doctrine: "1916 BCF", sea: 1));
        Assert.True(rate.Lost);
        Assert.True(rate.GirderLoad >= drill.GirderLoad, $"{rate.GirderLoad:0.00} against drill's {drill.GirderLoad:0.00}");
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
        Assert.Equal((1 + 1 / 1.2) / 2, mid.LoadShare, 6);
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
