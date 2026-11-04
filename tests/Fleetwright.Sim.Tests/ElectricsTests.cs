using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>What a ship's plant feeds and what it costs her to lose it, by what she carries (docs/battle/electrics.md;
/// bands from research/simulation/electric-plant.md "Checks").</summary>
public class ElectricsTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipCondition Fresh(ShipClass c)
    {
        var k = new ShipCondition();
        k.Reset(c);
        return k;
    }

    /// <summary>Check 3: a British 1914-16 ship on steam hydraulics draws no kW for her turrets, her battle load 0.3-0.8 of a
    /// 750 kW plant. Check 4: a German one of 1912-16 stands on hers, 0.3-0.7 of 1,200-2,400 kW.</summary>
    [Theory]
    [InlineData("tiger_1914", TurretDrive.SteamHydraulic, 750, 0.3, 0.8)]
    [InlineData("seydlitz", TurretDrive.ElectroHydraulic, 1200, 0.3, 0.7)]
    public void A_ships_battle_load_follows_her_drives(string design, TurretDrive drive, double plantKw, double lo, double hi)
    {
        var c = Fleet.Get(design).Class!;
        var e = c.Electric;
        output.WriteLine($"{design}: {c.TurretDrive}, connected {e.ConnectedKw:0}, battle {e.BattleDayKw:0} ({e.BattleDayKw / plantKw:0.00} of {plantKw:0})");
        Assert.Equal(drive, c.TurretDrive);
        Assert.Equal(drive != TurretDrive.SteamHydraulic, e.Consumers.Any(u => u.Name.Contains("gunhouse")));
        Assert.InRange(e.BattleDayKw / plantKw, lo, hi);
    }

    /// <summary>The same hull on electric drives draws far more than on steam hydraulics, whatever her year: the load is what
    /// she carries (the user: no era multiplier).</summary>
    [Fact]
    public void The_load_is_what_she_carries_not_her_year()
    {
        var built = Fleet.Get("tiger_1914");
        var design = built.Ship!.Design with { Fittings = built.Ship!.Design.Fittings! with { TurretDrive = "electro_hydraulic", Auxiliaries = "electric" } };
        var electric = ShipClassCompiler.Compile(ShipDesign.Build(design));
        output.WriteLine($"Tiger {built.Class!.Electric.BattleDayKw:0} kW as built, {electric.Electric.BattleDayKw:0} on electric drives");
        Assert.True(electric.Electric.BattleDayKw > 2 * built.Class!.Electric.BattleDayKw);
    }

    /// <summary>Check 7: a heavy gunhouse's connected load in the research's 0.25-0.4 kW a revolving tonne at its rates (0.7 in
    /// the 1940s US turrets' faster ones).</summary>
    [Theory]
    [InlineData("seydlitz", 0.25, 0.5)]
    [InlineData("borodino", 0.25, 0.5)]
    public void A_gunhouse_draws_by_its_weight(string design, double lo, double hi)
    {
        var c = Fleet.Get(design).Class!;
        int main = c.Links(c.Batteries[0].Mounts)[0];
        double t = c.Mounts[main].RevolvingT;
        var u = c.Electric.Consumers.First(x => x.Name.Contains("gunhouse") && x.Name.Contains($"{c.Mounts[main].CalibreMm:0} mm"));
        output.WriteLine($"{u.Name}: {u.Kw:0} kW, {u.Kw / t:0.00} kW/t");
        Assert.InRange(u.Kw / t, lo, hi);
    }

    /// <summary>Check 12: an electric trip leaves a steam-hydraulic turret training at its own rate; a German one falls to hand.</summary>
    [Fact]
    public void A_trip_stops_what_stands_on_the_plant_and_no_more()
    {
        foreach (var (design, reached) in new[] { ("tiger_1914", false), ("seydlitz", true) })
        {
            var c = Fleet.Get(design).Class!;
            var k = Fresh(c);
            long now = 600_000;
            Effects.PowerOut(c, k, default, -1, -1, double.PositiveInfinity, 60_000, now, Settings);
            Functions.Evaluate(k, Settings, now, now);
            int main = c.Links(c.Batteries[0].Mounts)[0];
            Assert.Equal(reached, k.ByHand(main, now));
            Assert.True(k.MountWorks[main]);
        }
    }

    /// <summary>EL2 with the drives: her steam gone, a steam-hydraulic turret falls to hand as an electric one does; her engine
    /// rooms gone with steam up, the dynamos stop and the steam-hydraulic turret trains on.</summary>
    [Fact]
    public void A_steam_hydraulic_turret_cares_about_steam_not_dynamos()
    {
        var c = Fleet.Get("tiger_1914").Class!;
        int main = c.Links(c.Batteries[0].Mounts)[0];
        long now = 600_000;
        var engines = Fresh(c);
        for (int r = 0; r < c.Rooms.Length; r++)
            if (c.Rooms[r].Kind == RoomKind.EngineRoom)
                engines.State[c.RoomNode(r)] = PartState.Disabled;
        Functions.Evaluate(engines, Settings, now, now);
        Assert.True(engines.PlantOut);
        Assert.False(engines.ByHand(main, now));
        var boilers = Fresh(c);
        for (int r = 0; r < c.Rooms.Length; r++)
            if (c.Rooms[r].Kind == RoomKind.BoilerRoom)
                boilers.State[c.RoomNode(r)] = PartState.Disabled;
        Functions.Evaluate(boilers, Settings, now, now);
        Assert.True(boilers.ByHand(main, now));
    }

    /// <summary>EL5: a ship-wide trip through the world. Her battery stays with its table, its guns on their own sights, not
    /// local control; when the power's back the director has it again, and her gyros settle after.</summary>
    [Fact]
    public void A_blackout_keeps_the_table_working_by_hand()
    {
        var c = Fleet.Get("seydlitz").Class!;
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 3, Settings with { HitsDoDamage = false });
        int cls = w.Ships.AddClass(c);
        w.Apply(ShipSystem.Spawn(cls, 56, 4, 0, 0));
        w.Apply(ShipSystem.Spawn(cls, 56, 4.2, 0, 0, side: 1));
        w.Apply(GunnerySystem.Engage(1, 0, 2));
        w.AdvanceTo(w.Time + 60_000);
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1));
        bool Local()
        {
            var snap = new Snapshot();
            w.Fill(snap, truth: true);
            return snap.Batteries.Local[0];
        }
        Assert.False(Local());
        w.Apply(DamageSystem.Blackout(1, 3));
        w.AdvanceTo(w.Time + 30_000);
        Assert.True(k.NoPower(w.Time.Ms));
        Assert.True(k.DirectorDark(0, w.Time.Ms));
        Assert.False(Local());
        w.AdvanceTo(w.Time + 4 * 60_000);
        Assert.False(k.NoPower(w.Time.Ms));
        Assert.False(k.DirectorDark(0, w.Time.Ms));
        Assert.True(k.GyroOut(w.Time.Ms), "settling");
        w.AdvanceTo(w.Time + (long)(Settings.GyroSettleMin * 60_000));
        Assert.False(k.GyroOut(w.Time.Ms));
    }

    /// <summary>EL5: a trip stops electric pumps and leaves steam ones; a shaft cut no longer touches either.</summary>
    [Fact]
    public void Pumps_stop_with_their_own_supply()
    {
        long now = 600_000;
        foreach (var (design, left) in new[] { ("kgv", 0.0), ("tiger_1914", 1.0) })
        {
            var c = Fleet.Get(design).Class!;
            var k = Fresh(c);
            k.Blackout(now, 60_000, Settings);
            Functions.Evaluate(k, Settings, now, now);
            Assert.Equal(left, k.PumpShare);
            Assert.True(k.Dark);
        }
        var tiger = Fleet.Get("tiger_1914").Class!;
        var cut = Fresh(tiger);
        foreach (ref readonly var line in tiger.ShaftLines)
            cut.State[line.Shaft] = PartState.Disabled;
        Functions.Evaluate(cut, Settings, now, now);
        Assert.Equal(0, cut.PowerCap);
        Assert.Equal(1, cut.PumpShare);
    }

    /// <summary>EL5: the helm goes with what drives it: electric steering with the plant, steam with the steam.</summary>
    [Fact]
    public void The_helm_goes_with_what_drives_it()
    {
        long now = 600_000;
        bool Steers(string design, RoomKind kind)
        {
            var c = Fleet.Get(design).Class!;
            var k = Fresh(c);
            for (int r = 0; r < c.Rooms.Length; r++)
                if (c.Rooms[r].Kind == kind)
                    k.State[c.RoomNode(r)] = PartState.Disabled;
            return Functions.Evaluate(k, Settings, now, now);
        }
        Assert.False(Steers("kgv", RoomKind.EngineRoom));        // electric, the dynamos gone
        Assert.True(Steers("tiger_1914", RoomKind.EngineRoom));  // steam, up
        Assert.False(Steers("tiger_1914", RoomKind.BoilerRoom)); // steam, gone
    }

    /// <summary>EL5: past 5° of list a gunhouse by hand trains at half its rate.</summary>
    [Fact]
    public void A_list_halves_a_hand_rate()
    {
        var c = Fleet.Get("seydlitz").Class!;
        int m = c.Links(c.Batteries[0].Mounts)[0];
        Assert.Equal(NonPen.HandShare(c, c.Mounts[m], Settings) / 2, NonPen.HandShare(c, c.Mounts[m], Settings, 8), 9);
    }

    /// <summary>EL6: a torpedo trips a ship's power through the world's damage system, most of the time at least a section's, about a
    /// third of the time all of it (Inflexible, Marlborough, Seydlitz, Szent István); well-secured gear far less (North Carolina).</summary>
    [Fact]
    public void A_torpedo_trips_her_power()
    {
        var torpedo = Fleet.Get("destroyer").Class!.Torpedoes[0];
        var (g0, g2) = (GivenHits.Torpedoes(Fleet.Get("seydlitz").Class!, torpedo, 4.5, double.NaN, 300, 1, 1, Settings),
            GivenHits.Torpedoes(Fleet.Get("battleship").Class!, torpedo, 4.5, double.NaN, 300, 1, 1, Settings));
        output.WriteLine($"G0 Seydlitz: {g0.Blackouts} blackouts, {g0.Shocks} shocks of {g0.Hits}; G2 battleship: {g2.Blackouts}, {g2.Shocks} of {g2.Hits}");
        Assert.InRange((double)g0.Blackouts / g0.Hits, 0.2, 0.4);
        Assert.InRange((double)g0.Shocks / g0.Hits, 0.6, 0.9);
        Assert.True(g2.Blackouts < 0.3 * g0.Blackouts, $"{g2.Blackouts} against {g0.Blackouts}");
    }

    /// <summary>EL4b: a gunhouse trains and elevates by its drive, near the research's §4 for the turrets it names.</summary>
    [Theory]
    [InlineData("tiger_1914", 1.9, 2.8, 5, 7.5)]        // hydraulic, the 15" Mk I 2 and 5
    [InlineData("kgv", 1.8, 2.6, 5, 8.5)]               // hydraulic, 2 and 8
    [InlineData("seydlitz", 2.5, 3.5, 3.5, 7)]          // German electro-hydraulic, 3 and 4
    [InlineData("borodino", 2.2, 3.0, 2.2, 3.0)]        // direct electric, 2.4-3 and 2.2-2.7
    [InlineData("littorio", 4.5, 7, 4.5, 7)]            // direct electric, 6 and 6
    [InlineData("bismarck", 4, 6, 6, 12)]               // electro-hydraulic, 5.4-6
    [InlineData("battleship", 3, 5, 6, 12)]             // the USN's 1940s, Iowa's 4 and 12
    public void A_gunhouse_trains_by_its_drive(string design, double trainLo, double trainHi, double elevLo, double elevHi)
    {
        var c = Fleet.Get(design).Class!;
        ref readonly var m = ref c.Mounts[c.Links(c.Batteries[0].Mounts)[0]];
        output.WriteLine($"{design} {m.CalibreMm:0} mm, {c.TurretDrive}: train {m.TrainDegS:0.00}, elevate {m.ElevateDegS:0.00}");
        Assert.InRange(m.TrainDegS, trainLo, trainHi);
        Assert.InRange(m.ElevateDegS, elevLo, elevHi);
    }

    /// <summary>The format step sets the drives from navy and plant year, once; the ship reads the block.</summary>
    [Theory]
    [InlineData("portsmouth", 1916, TurretDrive.SteamHydraulic, AuxiliaryDrive.Steam)]
    [InlineData("kiel", 1916, TurretDrive.ElectroHydraulic, AuxiliaryDrive.Steam)]
    [InlineData("brooklyn", 1900, TurretDrive.Electric, AuxiliaryDrive.Steam)]
    [InlineData("brooklyn", 1941, TurretDrive.ElectroHydraulic, AuxiliaryDrive.Electric)]
    [InlineData("kronstadt", 1904, TurretDrive.Electric, AuxiliaryDrive.Steam)]
    public void A_navy_and_year_give_the_drives(string navy, int year, TurretDrive turret, AuxiliaryDrive aux)
    {
        Assert.Equal(turret, Fittings.TurretDriveBy(navy, year, false));
        Assert.Equal(aux, Fittings.AuxiliariesBy(navy, year, false));
    }
}
