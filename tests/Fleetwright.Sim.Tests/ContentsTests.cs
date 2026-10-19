using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>What stands in a ship's spaces in a shell's path (docs/battle/shell-contents.md C1; damage/09).</summary>
public class ContentsTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    /// <summary>A round bar struck at an offset uniform across it crosses its mean chord (π/4)(OD² − ID²)/OD of steel:
    /// a 12" gun at the slide (OD 2.4 calibres) 475 mm, a 400 mm shaft with a 0.45 bore 251 mm (damage/09 §1.3).</summary>
    [Fact]
    public void A_bars_chord_averages_the_research_table()
    {
        foreach (var (od, bore, mean) in new[] { (2.4 * 304.8, 1 / 2.4, 475.0), (400, 0.45, 251.0), (450, 0, 353.0) })
        {
            const int n = 200_000;
            double sum = 0;
            for (int i = 0; i < n; i++)
                sum += Contents.BarChordMm(od, bore, (i + 0.5) / n * od / 2);
            Assert.Equal(mean, sum / n, 0);
            Assert.Equal(mean, Contents.BarMeanChordMm(od, bore), 0);
        }
    }

    /// <summary>No switch between a destroyer's contents and a large ship's: a ship's place between them by her
    /// displacement, the rows field by field.</summary>
    [Fact]
    public void Contents_grow_with_the_ship()
    {
        Assert.Equal(0, Contents.SizeOf(800));
        Assert.Equal(0, Contents.SizeOf(Contents.SmallT), 9);
        Assert.Equal(1, Contents.SizeOf(Contents.LargeT), 9);
        Assert.Equal(1, Contents.SizeOf(60_000));
        var (small, large) = Contents.Of(RoomKind.EngineRoom, BoilerKind.SmallTube, EngineKind.DirectTurbine)!.Value;
        Assert.Equal(small.ChordM, Contents.Between(small, large, 0).ChordM, 9);
        Assert.Equal(large.ChordM, Contents.Between(small, large, 1).ChordM, 9);
        double half = Contents.Between(small, large, 0.5).Mix[0].Core.HiMm;
        Assert.Equal((small.Mix[0].Core.HiMm + large.Mix[0].Core.HiMm) / 2, half, 9);
        Assert.Null(Contents.Of(RoomKind.Accommodation, BoilerKind.SmallTube, EngineKind.Vte));
    }

    /// <summary>A liquid inside the hull is the sea's physics by density (C2): at the sea's density it's the sea's decay to
    /// the bit, oil's a little longer; a granular medium's 96,000 over its density in calibres (coal: 120).</summary>
    [Fact]
    public void Media_decay_by_density()
    {
        var shell = new ShellTypeRow { CalibreMm = 381 };
        Assert.Equal(WaterRun.DecayM(shell, Settings), WaterRun.DecayM(shell, Settings, WaterRun.SeaRho, MediumKind.Liquid), 12);
        Assert.Equal(0.381 * 25 * 1025 / 950, WaterRun.DecayM(shell, Settings, Stowage.OilRho, MediumKind.Liquid), 9);
        Assert.Equal(0.381 * 120, WaterRun.DecayM(shell, Settings, Stowage.CoalRho, MediumKind.Granular), 9);
        // 4 m of a liquid layer keeps a 15" at 66% (damage/09 §3)
        Assert.Equal(0.66, Math.Exp(-4 / WaterRun.DecayM(shell, Settings, WaterRun.SeaRho, MediumKind.Liquid)), 2);
    }

    /// <summary>A run through a medium on the fuze's clock (C2): slowed by e^(−x/L); a fuze with less time left than the run
    /// takes bursts in it, where the time runs out; with more, the rest is spent at the new speed beyond.</summary>
    [Fact]
    public void A_fuze_runs_on_through_a_medium()
    {
        double v = 500, burstT = 10 + 0.004 * 500;   // 4 ms left at 500 m/s, from T 10
        WaterRun.Through(ref v, ref burstT, 10, 10, 5);
        Assert.Equal(10 + WaterRun.RunInM(500, 0.004, 5), burstT, 9);
        Assert.Equal(500 * Math.Exp(-(burstT - 10) / 5), v, 9);

        (v, burstT) = (500, 10 + 0.05 * 500);   // 50 ms: through 2 m of it, the rest beyond
        WaterRun.Through(ref v, ref burstT, 10, 2, 5);
        double v1 = 500 * Math.Exp(-2 / 5.0);
        Assert.Equal(v1, v, 9);
        Assert.Equal(12 + (0.05 - WaterRun.TimeS(500, 2, 5)) * v1, burstT, 9);

        (v, burstT) = (500, double.NaN);
        WaterRun.Through(ref v, ref burstT, 10, 2, 5);
        Assert.Equal(v1, v, 9);
        Assert.True(double.IsNaN(burstT));
    }

    /// <summary>The fuel by the period's practice (C2): the protective side spaces (an oil ship's TDS layers) are kept
    /// full, the rest (her double bottom) burnt first; with the doctrine off they fall alike.</summary>
    [Fact]
    public void Protective_spaces_are_burnt_last()
    {
        var c = Class("kgv");
        int tds = -1, db = -1;
        for (int i = 0; i < c.Rooms.Length; i++)
            if (c.Stowages[i].Some && c.Stowages[i].Protective && tds < 0)
                tds = i;
            else if (c.Stowages[i].Some && c.Rooms[i].Kind == RoomKind.DoubleBottom && db < 0)
                db = i;
        Assert.True(tds >= 0 && db >= 0);
        var full = new double[c.Rooms.Length];
        var half = new double[c.Rooms.Length];
        Stowage.Levels(c, 1, true, full);
        Stowage.Levels(c, 0.5, true, half);
        Assert.Equal(full[tds], half[tds], 9);
        Assert.True(half[db] < full[db] - 0.05 || double.IsNaN(half[db]));
        Stowage.Levels(c, 0.5, false, half);
        Assert.True(half[tds] < full[tds]);
    }

    /// <summary>A shell falling into a coal ship's wing bunker (C3a): the trace shows the run through the coal, and the shell
    /// slowed by e^(−x/L) over it, L 120 calibres.</summary>
    [Fact]
    public void Coal_slows_a_shell_a_little()
    {
        var target = Fleet.Scenario("hessen");
        var shooter = Fleet.Scenario("iron_duke");
        var shell = shooter.Loadout.ShellTypes[shooter.Loadout.Fires(0)];
        double elev = shooter.Mounts[shooter.Links(shooter.Batteries[0].Mounts)[0]].MaxElevationDeg;
        int room = -1;
        for (int i = 0; i < target.Rooms.Length && room < 0; i++)
            if (target.Stowages[i].Some && target.Stowages[i].Kind == MediumKind.Granular)
                room = i;
        Assert.True(room >= 0);
        double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y = 0;
        foreach (int cell in target.Links(target.Rooms[room].Cells))
            (x0, x1, y) = (Math.Min(x0, target.Cells[cell].Box.X0), Math.Max(x1, target.Cells[cell].Box.X1), (target.Cells[cell].Box.Y0 + target.Cells[cell].Box.Y1) / 2);
        ref readonly var st = ref target.Stowages[room];
        var level = new double[target.Rooms.Length];
        Stowage.Levels(target, Settings.FuelShareDefault, Settings.ProtectiveBunkersLast, level);
        double z = level[room] - 0.5;   // in the coal (under her waterline at 0.8 of a load: a falling shell reaches it)
        output.WriteLine($"coal to {level[room]:0.0} m, waterline {target.Vertical.Waterline:0.0} m");
        var range = new ShotRange(Settings);
        double decay = WaterRun.DecayM(shell, Settings, Stowage.CoalRho, MediumKind.Granular);
        int runs = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var s = range.Fire(target, shell, "iron duke main", elev, 18_000, y > 0 ? 270 : 90, new P3(x0 + (x1 - x0) * seed / 13.0, y, z), seed);
            foreach (var step in s.Steps)
                if (step.Call == PlateCall.Medium && step.Name == "coal")
                {
                    runs++;
                    Assert.Equal(step.InMs * Math.Exp(-step.RunM / decay), step.OutMs, 6);
                }
        }
        output.WriteLine($"coal runs {runs}");
        Assert.True(runs > 0);
    }

    /// <summary>Heavy shells plunging into a destroyer's engine room (under her waterline: only falling shells reach it):
    /// most strike something there (its band, its λ), and a fair share burst on it (damage/09 §2.4: WDR 51, Johnston);
    /// the rest go on through her.</summary>
    [Fact]
    public void Heavy_shells_strike_a_destroyers_machinery()
    {
        var target = Class("destroyer");
        var shooter = Class("lion");
        var shell = shooter.Loadout.ShellTypes[shooter.Loadout.Fires(0)];
        double elev = shooter.Mounts[shooter.Links(shooter.Batteries[0].Mounts)[0]].MaxElevationDeg;
        int room = -1;
        for (int i = 0; i < target.Rooms.Length && room < 0; i++)
            if (target.Rooms[i].Kind == RoomKind.EngineRoom)
                room = i;
        Assert.True(room >= 0);
        ref readonly var k = ref target.ContentsOf(room);
        Assert.True(k.Some);
        double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity, z = k.ZHi - 0.5;
        foreach (int cell in target.Links(target.Rooms[room].Cells))
            (x0, x1) = (Math.Min(x0, target.Cells[cell].Box.X0), Math.Max(x1, target.Cells[cell].Box.X1));
        var range = new ShotRange(Settings);
        int shots = 0, struck = 0, burst = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            double x = x0 + (x1 - x0) * (seed - 0.5) / 40;
            var s = range.Fire(target, shell, "lion main", elev, 20_000, 90, new P3(x, 0, z), seed);
            if (!s.Struck)
                continue;
            shots++;
            bool hit = false;
            foreach (ref readonly var c in s.Path.Crossings)
                hit |= c.Kind == CrossingKind.Struck && c.Index == room;
            struck += hit ? 1 : 0;
            if (s.Terminal.Bursts && s.Steps.Length > 0 && s.Fuze == FuzeCause.Contents)
                burst++;
        }
        output.WriteLine($"{shots} shots: struck a solid {struck}, burst on what they struck {burst}");
        Assert.True(shots >= 30);
        // a plunging line through the band runs ~9 m in it against λ 2.2 m: nearly all strike (1 − e^(−4)); the fuze starts
        // on what's heavy enough (the rotor, the gearing, the thrust blocks; not the condenser's 9 mm)
        Assert.InRange((double)struck / shots, 0.8, 1.0);
        Assert.InRange((double)burst / shots, 0.4, 0.95);
    }
}
