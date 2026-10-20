using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>B3 of docs/battle/fire.md: water on a fire (damage/17 §6, §8.7-2 and 7): fog beats a Shadwell-size fire in a
/// few minutes, a jet slower; one hose in the first minute beats three later; a hose short of the need holds a fire and
/// floods the ship at 12-21 t a hose-hour; water that stops under control may rekindle it; water left in a holed space
/// above the sea runs out over the hole's edge.</summary>
public class FireWaterTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static FireSpaceRow Room(double area, double q, double growth = 1.3) => new()
    {
        Kind = FireSpaceKind.Cell, Use = FireUse.Quarters, AreaM2 = area, HeightM = 2.6, LoadMJm2 = q, StrippedMJm2 = q, Growth = growth,
    };

    static FireWater Hose(string nozzle, double lpm)
    {
        var n = Settings.FireNozzles.Single(x => x.Name == nozzle);
        return new FireWater(lpm * n.FogShare, lpm, n.BoilShare);
    }

    /// <summary>A fire with one door, the hose on it from a time: control (min after the water), out (min from ignition;
    /// NaN: not in 4 h), whether it flashed over, and the water left aboard (t).</summary>
    static (double Control, double Out, bool Flashed, double WaterT) Attack(FireSpaceRow row, FireWater hose, double atMin, double dt = 2)
    {
        var x = new BurnState { Phase = BurnPhase.Grow, Q = 0.1, Seat = 0.1, E0 = row.LoadMJm2 * row.AreaM2, E = row.LoadMJm2 * row.AreaM2 };
        double aoh = Settings.FireLeakAoh + Settings.FireDoorAoh, t = 0, control = double.NaN, water = 0;
        bool flashed = false;
        while (x.Burning && t < 4 * 3600)
        {
            var e = Fires.Advance(row, ref x, aoh, row.LoadMJm2, dt, Settings, false, t >= atMin * 60 ? hose : default, out double aboard);
            t += dt;
            water += aboard;
            flashed |= e == BurnEvent.Flashover;
            if (double.IsNaN(control) && x.Phase == BurnPhase.Control)
                control = t / 60 - atMin;
        }
        return (control, x.Burning ? double.NaN : t / 60, flashed, water / 1000);
    }

    [Fact]
    public void Fog_beats_a_Shadwell_size_fire_in_minutes_and_a_jet_takes_longer()
    {
        var fog = Attack(Room(30, 480), Hose("fog", 360), 4);
        var jet = Attack(Room(30, 480), Hose("jet", 360), 4);
        output.WriteLine($"fog: control {fog.Control:0.0} min after the water, out at {fog.Out:0.0}, {fog.WaterT:0.0} t; " +
                         $"jet: {jet.Control:0.0}, {jet.Out:0.0}, {jet.WaterT:0.0} t");
        Assert.InRange(fog.Control, 0.5, 3);   // Shadwell 1994: under control in 2 min
        Assert.InRange(fog.Out, 8, 14);   // done 9.1 min after entry
        Assert.True(jet.Control > fog.Control && jet.Out > fog.Out);
        Assert.True(fog.WaterT < 5);
    }

    [Fact]
    public void One_hose_in_the_first_minute_beats_three_later()
    {
        var early = Attack(Room(90, 480), Hose("all-purpose", 250), 1);
        var late = Attack(Room(90, 480), Hose("all-purpose", 250), 6);
        var three = Attack(Room(90, 480), Hose("all-purpose", 750), 6);
        output.WriteLine($"one hose at 1 min: out {early.Out:0.0} (flashed {early.Flashed}); at 6 min: {late.Out:0.0} (flashed {late.Flashed}); " +
                         $"three at 6 min: {three.Out:0.0}");
        Assert.False(early.Flashed);
        Assert.InRange(early.Out, 2, 6);
        Assert.True(late.Flashed);
        Assert.True(double.IsNaN(late.Out) || late.Out > 60, "one hose after flashover only holds it");
        Assert.InRange(three.Out, 12, 22);
    }

    [Fact]
    public void A_hose_short_of_the_need_holds_the_fire_and_floods_the_ship()
    {
        // a flashed-over 90 m² messdeck (needs 360 L/min of fog) under a 400 L/min jet for an hour
        var row = Room(90, 480);
        var x = new BurnState { Phase = BurnPhase.Developed, Q = 2.2, Seat = 0.1, Involved = 90, E0 = 480 * 90, E = 480 * 90 };
        double aoh = Settings.FireLeakAoh + Settings.FireDoorAoh, water = 0, q = 0;
        for (int t = 0; t < 3600; t += 2)
        {
            Fires.Advance(row, ref x, aoh, 480, 2, Settings, false, Hose("jet", 400), out double aboard);
            water += aboard;
            q += x.Q / 1800;
        }
        output.WriteLine($"held at {q:0.00} MW on average, {water / 1000:0.0} t aboard in the hour, {x.Phase}");
        Assert.Equal(BurnPhase.Developed, x.Phase);
        Assert.InRange(q, 1.2, 2.0);   // 1 - 0.7·held of the door's 2.2 MW
        Assert.InRange(water / 1000, 12, 23);   // 17 §6.5: 12-21 t a hose-hour
    }

    [Fact]
    public void Water_on_a_ships_fire_goes_into_her_and_stopped_under_control_may_rekindle_it()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var f = new FloodModel(c);
        int cell = Enumerable.Range(0, c.Cells.Length).First(i => c.FireSpaces[i].Use == FireUse.Quarters && c.FireSpaces[i].AreaM2 > 30);
        var quiet = Settings with { FireHarmS = [], FireMagazineS = [1e12, 1e12, 1e12, 1e12, 1e12], FireOpenPathPerMin = 0,
            FireSteelPerMin = [0, 0, 0], FireDeckPerMin = 0 };
        int rekindled = 0;
        double water = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var k = new ShipCondition();
            k.Reset(c);
            k.Readiness = Settings.Readiness[1];
            var r = RandomStream.FromSeed((ulong)seed);
            Fires.Ignite(c, k, cell, 0.3, quiet);
            for (int t = 0; t < 10 * 60; t++)
                Fires.Step(c, f, k, 1, quiet, 1, ref r);
            k.FireFlow[cell] = Hose("fog", 2000);
            for (int t = 0; t < 600 && k.Fire[cell].Phase != BurnPhase.Control; t++)
                Fires.Step(c, f, k, 1, quiet, 1, ref r);
            Assert.Equal(BurnPhase.Control, k.Fire[cell].Phase);
            water += k.WaterM3[cell];
            k.FireFlow[cell] = default;   // the hose goes before it's out
            Fires.Step(c, f, k, 1, quiet, 1, ref r);
            rekindled += k.Fire[cell].Phase == BurnPhase.Grow ? 1 : 0;
            Assert.Equal(k.Fire[cell].Burning, k.CellFire[cell]);
        }
        output.WriteLine($"{water / 40:0.00} m³ aboard at control, rekindled {rekindled} of 40");
        Assert.True(water > 0);
        Assert.InRange(rekindled, 5, 20);   // 30%
    }

    [Fact]
    public void Water_in_a_holed_space_over_the_sea_runs_out_over_the_holes_edge()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var f = new FloodModel(c);
        var k = new ShipCondition();
        k.Reset(c);
        double wl = c.Vertical.Waterline;
        int high = Enumerable.Range(0, c.Cells.Length).First(i => c.Cells[i].Box.Base > wl + 1 && c.Cells[i].Box.Y1 > c.Beam / 2 - 1);
        var b = c.Cells[high].Box;
        k.WaterM3[high] = 0.3 * f.Capacity[high];
        (k.BreachM2[high], k.BreachZ[high], k.BreachY[high]) = (0.5, b.Base + 1.0, b.Y1);   // its lower edge 0.6 m up
        double before = k.WaterM3[high], out_ = 0;
        for (int t = 0; t < 600; t++)
            out_ += Flooding.Step(c, f, k, 1, 0, Settings).OutM3;   // no pumps: what leaves goes through the hole
        double aboard = k.WaterM3.Sum();
        output.WriteLine($"{c.CellId(high)}: {before:0} m³, out through a hole 0.6 m over its floor {out_:0.0} m³ in 10 min, {aboard:0.0} m³ still aboard " +
                         "(leaked down through her decks)");
        Assert.Equal(before, aboard + out_, 3);
        Assert.True(out_ > 1, "it runs out over the hole's edge");

        // a hole over the water lets nothing out
        k.Reset(c);
        k.WaterM3[high] = 0.1 * f.Capacity[high];
        (k.BreachM2[high], k.BreachZ[high], k.BreachY[high]) = (0.5, b.Top - 0.5, b.Y1);
        double none = 0;
        for (int t = 0; t < 60; t++)
            none += Flooding.Step(c, f, k, 1, 0, Settings).OutM3;
        Assert.Equal(0, none);
    }
}
