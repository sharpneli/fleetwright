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
