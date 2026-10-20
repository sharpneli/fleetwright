using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>B1 of docs/battle/fire.md: the ship's orders before action (stripped, the doors, the decks wetted), spread
/// along the fire links by way (damage/17 §5: steel up in ~18 min, sideways ~40, down hours; an open path in minutes),
/// the weather deck, a fire put out hot lighting again, and a propellant seat's charges.</summary>
public class FireSpreadTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>No harm and no magazines (and nobody fighting: a fresh condition has no doctrine): nothing harmed or set off.</summary>
    static readonly SimSettings Quiet = Settings with { FireHarmS = [], FireMagazineS = [1e12, 1e12, 1e12, 1e12, 1e12] };

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    static ShipCondition Fresh(ShipClass c, string readiness)
    {
        var k = new ShipCondition();
        k.Reset(c);
        k.Readiness = Settings.Readiness.Single(x => x.Name == readiness);
        return k;
    }

    /// <summary>A quarters cell with a space that burns over it (a cell, or the weather deck), a quarters cell beside it
    /// and a cell that burns under it.</summary>
    static (int Cell, int Up, int Side, int Down) Spaces(ShipClass c)
    {
        bool Q(int i) => i < c.Cells.Length && c.FireSpaces[i].Use == FireUse.Quarters;
        bool Burns(int i) => c.FireSpaces[i].Use != FireUse.None;
        int Way(int i, FireWay w, Func<int, bool> ok) =>
            c.FireLinks(c.FireSpaces[i].Links).ToArray().Where(l => l.Way == w && ok(l.Space)).Select(l => l.Space).DefaultIfEmpty(-1).First();
        foreach (int i in Enumerable.Range(0, c.Cells.Length).Where(Q))
        {
            int up = Way(i, FireWay.Up, Burns), side = Way(i, FireWay.Side, Q), down = Way(i, FireWay.Down, x => x < c.Cells.Length && Burns(x));
            if (up >= 0 && side >= 0 && down >= 0)
                return (i, up, side, down);
        }
        throw new InvalidOperationException("no quarters cell with all three");
    }

    /// <summary>Minutes from ignition until each watched space lights (NaN: not in the time).</summary>
    static double[] Run(ShipClass c, ShipCondition k, int lit, int[] watch, double minutes, ulong seed, SimSettings s)
    {
        var f = new FloodModel(c);
        var r = RandomStream.FromSeed(seed);
        Fires.Ignite(c, k, lit, 0.3, s);
        var at = Enumerable.Repeat(double.NaN, watch.Length).ToArray();
        for (int t = 1; t <= minutes * 60; t++)
        {
            Fires.Step(c, f, k, 1, s, ref r);
            for (int j = 0; j < watch.Length; j++)
                if (double.IsNaN(at[j]) && k.Fire[watch[j]].Burning)
                    at[j] = t / 60.0;
        }
        return at;
    }

    static double Median(IEnumerable<double> xs)
    {
        var a = xs.Select(x => double.IsNaN(x) ? double.PositiveInfinity : x).Order().ToArray();
        return a[a.Length / 2];
    }

    [Fact]
    public void Through_steel_a_fire_climbs_in_a_quarter_hour_creeps_sideways_and_seldom_goes_down()
    {
        var c = Class("dreadnought");
        var (cell, up, side, down) = Spaces(c);
        var runs = Enumerable.Range(0, 40).Select(seed =>
        {
            var k = Fresh(c, "closed up");
            k.BreachM2[cell] = 2;   // its shell holes let it breathe enough to flash over
            return Run(c, k, cell, [up, side, down], 60, (ulong)seed, Quiet);
        }).ToList();
        double mUp = Median(runs.Select(x => x[0])), mSide = Median(runs.Select(x => x[1]));
        double downShare = runs.Count(x => !double.IsNaN(x[2])) / (double)runs.Count;
        output.WriteLine($"{c.CellId(cell)}: up {(up < c.Cells.Length ? c.CellId(up) : "the weather deck")} median {mUp:0.0} min, sideways {c.CellId(side)} {mSide:0.0} min, " +
                         $"down {c.CellId(down)} within the hour in {100 * downShare:0}% of runs");
        Assert.InRange(mUp, 12, 30);
        Assert.InRange(mSide, 22, 55);
        Assert.True(downShare <= 0.35, $"down {downShare:P0}");
        Assert.True(runs.All(x => double.IsNaN(x[2]) || x[2] > 40), "nothing goes down through steel inside 40 min");
    }

    [Fact]
    public void An_opening_a_burst_left_is_an_open_path_in_minutes()
    {
        var c = Class("dreadnought");
        var (cell, _, side, _) = Spaces(c);
        int pool = c.FireLinks(c.FireSpaces[cell].Links).ToArray().First(l => l.Space == side).Pool;
        var runs = Enumerable.Range(0, 40).Select(seed =>
        {
            var k = Fresh(c, "closed up");
            k.BreachM2[cell] = 1;
            k.JoinOpenM2[pool] = 1;   // the bulkhead blown open between them
            return Run(c, k, cell, [side], 30, (ulong)seed, Quiet)[0];
        }).ToList();
        output.WriteLine($"through a 1 m² opening: median {Median(runs):0.0} min");
        Assert.InRange(Median(runs), 3, 10);
    }

    [Fact]
    public void Her_orders_strip_her_shut_her_doors_and_wet_her_decks()
    {
        var c = Class("dreadnought");
        var (cell, _, _, _) = Spaces(c);
        var row = c.FireSpaces[cell];
        Assert.True(row.StrippedMJm2 < row.LoadMJm2);
        Assert.Equal(row.LoadMJm2, Fires.Load(row, Fresh(c, "unprepared")));
        Assert.Equal(row.StrippedMJm2, Fires.Load(row, Fresh(c, "prepared")));

        // closed up and unholed a fire starves; with her doors open it flashes over
        var still = Quiet with { FireOpenPathPerMin = 0, FireSteelPerMin = [0, 0, 0] };
        foreach (var (readiness, flashes) in new[] { ("closed up", false), ("prepared", true) })
        {
            var k = Fresh(c, readiness);
            var f = new FloodModel(c);
            var r = RandomStream.FromSeed(1);
            Fires.Ignite(c, k, cell, 0.3, still);
            int flashed = 0;
            var phases = new List<BurnPhase>();
            for (int t = 0; t < 20 * 60; t++)
            {
                flashed += Fires.Step(c, f, k, 1, still, ref r).Flashed;
                if (phases.Count == 0 || phases[^1] != k.Fire[cell].Phase)
                    phases.Add(k.Fire[cell].Phase);
            }
            output.WriteLine($"{readiness}: {string.Join("/", phases)}");
            Assert.Equal(flashes, flashed > 0);
            if (!flashes)
                Assert.Contains(BurnPhase.Starved, phases);
        }
    }

    [Fact]
    public void A_wooden_deck_burns_along_to_the_next_section_unless_it_was_wetted()
    {
        var c = Class("dreadnought");
        int s0 = c.Sections.Length / 2, d0 = c.FireOfDeck(s0), d1 = c.FireOfDeck(s0 + 1), d2 = c.FireOfDeck(s0 - 1);
        foreach (var (readiness, spreads) in new[] { ("unprepared", true), ("prepared", false) })
        {
            var runs = Enumerable.Range(0, 30).Select(seed =>
            {
                var at = Run(c, Fresh(c, readiness), d0, [d1, d2], 30, (ulong)seed, Quiet);
                return Math.Min(double.IsNaN(at[0]) ? 99 : at[0], double.IsNaN(at[1]) ? 99 : at[1]);
            }).ToList();
            double share = runs.Count(x => x < 99) / (double)runs.Count;
            output.WriteLine($"{readiness} deck ({Fires.Load(c.FireSpaces[d0], Fresh(c, readiness)):0} MJ/m²): along within 30 min in " +
                             $"{100 * share:0}%, median {Median(runs):0.0} min");
            Assert.True(spreads ? share > 0.8 : share == 0, $"{readiness}: {share:P0}");
        }
    }

    [Fact]
    public void A_fire_put_out_hot_may_light_again_for_half_an_hour()
    {
        var c = Class("dreadnought");
        var (cell, _, _, _) = Spaces(c);
        var f = new FloodModel(c);
        var still = Quiet with { FireOpenPathPerMin = 0, FireSteelPerMin = [0, 0, 0], FireDeckPerMin = 0 };
        int again = 0, late = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var k = Fresh(c, "prepared");
            var r = RandomStream.FromSeed((ulong)seed);
            Fires.Ignite(c, k, cell, 0.3, still);
            for (int t = 0; t < 20 * 60; t++)
                Fires.Step(c, f, k, 1, still, ref r);
            Fires.Douse(c, k, cell, still);   // put out after 20 min
            Assert.True(k.Fire[cell].Hot);
            int lit = -1;
            for (int t = 0; t < 45 * 60 && lit < 0; t++)
                if (Fires.Step(c, f, k, 1, still, ref r).Reflashed > 0)
                    lit = t;
            again += lit >= 0 ? 1 : 0;
            late += lit > Settings.FireReflashS ? 1 : 0;
        }
        output.WriteLine($"lit again in {again} of 40 (any after {Settings.FireReflashS / 60:0} min: {late})");
        Assert.InRange(again, 22, 38);   // 1 - e^-1.5 = 78%
        Assert.Equal(0, late);
    }

    [Fact]
    public void Ready_charges_burn_hot_for_seconds_and_light_the_gunhouse()
    {
        var c = Class("dreadnought");
        int part = Enumerable.Range(0, c.Components.Length).First(p => c.Components[p].Kind == ComponentKind.Main && c.FireOfPart(p) >= 0);
        int space = c.FireOfPart(part);
        var k = Fresh(c, "closed up");
        var f = new FloodModel(c);
        var r = RandomStream.FromSeed(3);
        Assert.True(Fires.Ignite(c, k, space, 1, Quiet, chargesMJ: 1000));
        double peak = 0, gone = double.NaN, outAt = double.NaN;
        for (int t = 1; t <= 40 * 60 && double.IsNaN(outAt); t++)
        {
            Fires.Step(c, f, k, 1, Quiet, ref r);
            peak = Math.Max(peak, k.Fire[space].Q);
            if (double.IsNaN(gone) && k.Fire[space].Charges <= 0)
                gone = t;
            if (!k.Fire[space].Burning)
                outAt = t / 60.0;
        }
        output.WriteLine($"gunhouse ({c.FireSpaces[space].LoadMJm2:0} MJ/m²): peak {peak:0} MW, charges gone at {gone:0} s, out at {outAt:0.0} min");
        Assert.InRange(peak, 25, 40);   // 1000 MJ over 30 s
        Assert.InRange(gone, 28, 33);
        Assert.InRange(outAt, 0.5, 25);   // then a gunhouse's thin fittings burn locally and die
    }
}
