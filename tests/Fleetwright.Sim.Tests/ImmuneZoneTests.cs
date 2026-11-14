using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The designer's immune zone (docs/designer-ui/immune-zone.md IZ0, <see cref="ImmuneZone"/>): the same on any
/// threads and in passes, its range table beside the world's, her own shells at her own calibre the loadout's, and more
/// armour never letting more in.</summary>
public class ImmuneZoneTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static Design Load(string name) => Design.Load(Path.Combine(Fleet.Shipgen, "designs", name + ".json"));

    /// <summary>Her main battery's AP and its guns' elevation.</summary>
    static (ShellTypeRow Shell, double MaxElev) Ap(ShipClass c)
    {
        int b = Array.FindIndex(c.Batteries.ToArray(), r => r.Kind == Battery.Main);
        var lo = c.Loadout;
        int type = lo.Fires(b);
        foreach (var l in lo.Loads(b))
            if (lo.ShellTypes[l.ShellType].Kind == ShellKind.Ap)
                type = l.ShellType;
        double elev = 0;
        foreach (int m in c.Links(c.Batteries[b].Mounts))
            elev = Math.Max(elev, c.Mounts[m].MaxElevationDeg);
        return (lo.ShellTypes[type], elev);
    }

    static ImmuneZone Zone(ShipClass c, int shots, int threads)
    {
        var (shell, elev) = Ap(c);
        var z = new ImmuneZone(c, shell, elev, Settings, 7);
        z.Fill(shots, threads);
        return z;
    }

    /// <summary>The range table on its own gives the world's table's falls and striking speeds.</summary>
    [Fact]
    public void The_range_table_is_the_worlds()
    {
        foreach (string name in (ReadOnlySpan<string>)["dreadnought", "bismarck", "destroyer"])
        {
            var (shell, elev) = Ap(Fleet.Get(name).Class!);
            var key = BallisticKey.Of(shell, elev);
            var light = new RangeTable(key, ImmuneZone.MuzzleM);
            var table = new BallisticTable(key);
            double reach = table.Fall(Math.Min(elev, table.MaxElevationDeg - 2 * BallisticTable.ElevationStep), ImmuneZone.MuzzleM).Range;
            output.WriteLine($"{name}: reach {light.ReachM:0} m against {reach:0}");
            Assert.InRange(light.ReachM / reach, 0.995, 1.005);
            for (double r = 2000; r < reach - 1000; r += 2000)
            {
                var f = table.Fall(table.ElevationFor(r, ImmuneZone.MuzzleM), ImmuneZone.MuzzleM);
                var (fall, strike) = light.At(r);
                output.WriteLine($"  {r,6:0} m: fall {fall:0.000}° against {f.FallDeg:0.000}°, {strike:0.0} m/s against {f.StrikeMs:0.0}");
                Assert.InRange(fall - f.FallDeg, -0.05, 0.05);
                Assert.InRange(strike / f.StrikeMs, 0.997, 1.003);
            }
        }
    }

    /// <summary>Filled at once on one thread, or in passes on several: the same cells.</summary>
    [Fact]
    public void The_same_on_any_threads_and_in_passes()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var one = Zone(c, 12, 1);
        var (shell, elev) = Ap(c);
        var passes = new ImmuneZone(c, shell, elev, Settings, 7);
        passes.Fill(5, 4);
        passes.Fill(12, 3);
        int shots = 0;
        foreach (var v in ImmuneZone.All)
            for (int r = 0; r < one.RangesM.Length; r++)
                for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                {
                    Assert.Equal(one[v, r, a], passes[v, r, a]);
                    shots += one[v, r, a].Shots;
                }
        Assert.True(shots > 1000, $"{shots} shells walked");
    }

    /// <summary>More shells where it matters: a refined cell is the cell filled to that many at once (the same shells), the
    /// rest are left as they were, and only some are refined.</summary>
    [Fact]
    public void A_refined_cell_is_the_cell_filled()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var (shell, elev) = Ap(c);
        var refined = new ImmuneZone(c, shell, elev, Settings, 7);
        refined.Fill(6, ImmuneZone.Armour, 4);
        refined.Refine(18, ImmuneZone.Armour, 4);
        var full = new ImmuneZone(c, shell, elev, Settings, 7);
        full.Fill(18, ImmuneZone.Armour, 4);
        int some = 0, all = 0;
        foreach (var t in ImmuneZone.Armour)
            for (int r = 0; r < full.RangesM.Length; r++)
                for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                {
                    all++;
                    if (refined[t, r, a].Tried < 18)
                        continue;
                    some++;
                    Assert.Equal(full[t, r, a], refined[t, r, a]);
                }
        output.WriteLine($"{some} of {all} cells refined");
        Assert.InRange(some, 1, all / 2);
    }

    /// <summary>Her own shells at her battery's own calibre are the loadout's.</summary>
    [Fact]
    public void Her_shells_at_her_own_calibre_are_the_loadouts()
    {
        foreach (string name in (ReadOnlySpan<string>)["dreadnought", "bismarck", "yamato"])
        {
            var d = Load(name);
            var c = Fleet.Get(name).Class!;
            int b = Array.FindIndex(c.Batteries.ToArray(), r => r.Kind == Battery.Main);
            var own = Loadout.ShellsAt(d, true, 0, d.Main![0].CalibreMm!.Value);
            var loads = c.Loadout.Loads(b);
            Assert.Equal(loads.Length, own.Count);
            for (int i = 0; i < own.Count; i++)
                Assert.Equal(c.Loadout.ShellTypes[loads[i].ShellType], own[i].Shell);
            // and at another, the same shells scaled: heavier, the same kinds
            var big = Loadout.ShellsAt(d, true, 0, d.Main![0].CalibreMm!.Value + 50);
            for (int i = 0; i < own.Count; i++)
            {
                Assert.Equal(own[i].Kind, big[i].Kind);
                Assert.True(big[i].Shell.MassKg > own[i].Shell.MassKg);
            }
        }
    }

    /// <summary>A thicker belt lets fewer shells through it, and a thicker deck fewer through it (summed over the zone: one
    /// cell's few shells are noisy).</summary>
    [Fact]
    public void More_armour_lets_fewer_through()
    {
        static double Sum(ImmuneZone z, ZoneTarget t)
        {
            double s = 0;
            for (int r = 0; r < z.RangesM.Length; r++)
                for (int a = 0; a < ImmuneZone.Angles.Length; a++)
                    s += z[t, r, a].ThroughShare;
            return s;
        }
        int threads = Math.Max(2, Environment.ProcessorCount / 2);
        ImmuneZone Of(Design d, ZoneTarget t)
        {
            var c = ShipClassCompiler.Compile(ShipDesign.Build(d));
            var (shell, elev) = Ap(c);
            var z = new ImmuneZone(c, shell, elev, Settings, 7);
            z.Fill(16, [t], threads);
            return z;
        }
        var d = Load("dreadnought");
        var thick = d with { Armour = d.Armour! with { BeltMm = d.Armour!.BeltMm * 1.4 } };
        double before = Sum(Of(d, ZoneTarget.Belt), ZoneTarget.Belt), after = Sum(Of(thick, ZoneTarget.Belt), ZoneTarget.Belt);
        output.WriteLine($"dreadnought, through the belt: {before:0.00} with her belt, {after:0.00} with it 1.4 times as thick");
        Assert.True(before > 1);
        Assert.True(after < before);
        var b = Load("bismarck");
        var heavy = b with { Armour = b.Armour! with { Decks = [.. b.Armour!.Decks!.Select(x => x with { Mm = x.Mm * 1.5 })] } };
        before = Sum(Of(b, ZoneTarget.Deck), ZoneTarget.Deck);
        after = Sum(Of(heavy, ZoneTarget.Deck), ZoneTarget.Deck);
        output.WriteLine($"bismarck, through the deck: {before:0.00} with her decks, {after:0.00} with them 1.5 times as thick");
        Assert.True(before > 1);
        Assert.True(after < before);
    }
}

/// <summary>Shells fired in anger (docs/designer-ui/immune-zone.md IZ2, <see cref="AngerRun"/>): each band a world of its
/// own, so the same ships and hits on any number of threads.</summary>
public class AngerRunTests
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    [Fact]
    public void The_same_on_any_threads()
    {
        var c = Fleet.Get("destroyer").Class!;
        var (shell, elev) = (c.Loadout.ShellTypes[c.Loadout.Fires(0)], c.Mounts[c.Links(c.Batteries[0].Mounts)[0]].MaxElevationDeg);
        var one = new AngerRun(c, shell, elev, Settings, 3, hitsPerShip: 4, settleMin: 5);
        one.Fill(2, 1);
        var many = new AngerRun(c, shell, elev, Settings, 3, hitsPerShip: 4, settleMin: 5);
        many.Fill(1, 4);
        many.Fill(2, 3);
        Assert.Equal(one.Bands.Length, many.Bands.Length);
        Assert.True(one.Bands.Length > 3);
        for (int b = 0; b < one.Bands.Length; b++)
        {
            var (x, y) = (one.Bands[b].Report, many.Bands[b].Report);
            Assert.Equal((x.Ships, x.Hits, x.Lost, x.Fires, x.Killed, x.WaterT), (y.Ships, y.Hits, y.Lost, y.Fires, y.Killed, y.WaterT));
            Assert.Equal(one.Bands[b].Dots, many.Bands[b].Dots);
        }
    }
}
