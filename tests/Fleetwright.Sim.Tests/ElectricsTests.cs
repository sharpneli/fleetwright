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
            Effects.PowerOut(c, k, default, -1, -1, double.PositiveInfinity, 60_000, now);
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
