using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Gratings, backing and the plates' scatter (docs/battle/shells-armour-damage.md P7).</summary>
public class PlateTests(ITestOutputHelper output)
{
    static Design Dreadnought => Fleet.Get("dreadnought").Ship!.Design;

    static ShipClass Build(Design d) => ShipClassCompiler.Compile(ShipDesign.Build(d));

    /// <summary>A shell down an uptake meets its armoured grating: armour.gratings of the deck's plate (0.4 unless
    /// given).</summary>
    [Fact]
    public void A_grating_is_its_share_of_the_deck()
    {
        foreach (double? share in new double?[] { null, 0.25 })
        {
            var d = Dreadnought;
            var c = share is null ? Fleet.Get("dreadnought").Class! : Build(d with { Armour = d.Armour! with { Gratings = share } });
            Assert.Equal(share ?? Shipgen.Armour.GratingShareDefault, c.GratingShare);
            var path = new HitPath();
            int found = 0;
            foreach (int u in c.Solids.Uptakes)
            {
                var b = c.Solids.Solids[u].Box;
                double x = (b.X0 + b.X1) / 2, y = (b.Y0 + b.Y1) / 2;
                ShipTracer.Trace(c, new P3(x, y, b.Top + 5), new P3(x, y, c.Vertical.Keel), [], Waterplane.Level(c), path);
                foreach (ref readonly var xc in path.Crossings)
                    if (xc.Plate == PlateKind.Grating)
                    {
                        found++;
                        Assert.Equal(c.ArmourDecks[xc.Index].ThicknessMm * c.GratingShare, xc.Mm, 9);
                    }
            }
            output.WriteLine($"share {c.GratingShare}: {found} gratings met");
            Assert.True(found > 0);
        }
    }

    /// <summary>armour.backing: weighed over the belt's face by its material, compiled onto the belt (and the upper and end
    /// belts, which take it unless they give their own), and crossed between the armour and the hull's plating going in
    /// and coming out; to a fuze or a cap it is its weight in steel.</summary>
    [Fact]
    public void A_belts_backing_weighs_and_lies_behind_it()
    {
        var d = Dreadnought;
        var backed = d with { Armour = d.Armour! with { Backing = new BackingInput { Material = "wood", Mm = 100 } } };
        var ship = ShipDesign.Build(backed);
        var plain = ShipDesign.Build(d);
        var c = ShipClassCompiler.Compile(ship);
        var w = ship.Report.Weights.Single(x => x.Name == "Belt backing");
        ref readonly var belt = ref c.SidePlates[0];
        Assert.Equal(StrakeKind.Belt, belt.Kind);
        double face = 2 * (belt.X1 - belt.X0) * (belt.Top - belt.Bottom) * 0.1 * 0.7;
        output.WriteLine($"belt backing {w.T:0.0} t (face {face:0.0} t); displacement {plain.Report.Results.StandardDisplacementT} -> {ship.Report.Results.StandardDisplacementT} t");
        Assert.InRange(w.T, 0.99 * face, 1.15 * face);
        for (int i = 0; i < c.SidePlates.Length; i++)
            if (c.SidePlates[i].Kind is StrakeKind.Belt or StrakeKind.Upper or StrakeKind.End or StrakeKind.Zone)
                Assert.Equal(100, c.SidePlates[i].BackingMm);
        var wood = c.MaterialOf(c.SidePlates[0].BackingMaterial);
        Assert.Equal(0.01, wood.Q, 9);
        Assert.Equal(100 * 0.7 / 7.85, wood.SteelMm(100), 9);

        // across the ship through the belt amidships: armour, backing, plating in; plating, backing, armour out
        var path = new HitPath();
        double x0 = (belt.X0 + belt.X1) / 2, z = (belt.Bottom + belt.Top) / 2;
        ShipTracer.Trace(c, new P3(x0, 60, z), new P3(x0, -60, z), [], Waterplane.Level(c), path);
        var order = new List<string>();
        foreach (ref readonly var x in path.Crossings)
            if (x.Plate is PlateKind.SideArmour or PlateKind.Backing || x.Kind is CrossingKind.HullIn or CrossingKind.HullOut)
                order.Add(x.Kind is CrossingKind.HullIn or CrossingKind.HullOut ? x.Kind.ToString() : x.Plate.ToString());
        Assert.Equal(["SideArmour", "Backing", "HullIn", "HullOut", "Backing", "SideArmour"], order);

        Assert.Null(plain.Report.Weights.FirstOrDefault(x => x.Name == "Belt backing"));
    }

    /// <summary>A plate's quality is its lot's on the ship (the same each time she's hit there, another ship's another),
    /// about N(1, 0.06), and each shot scatters about it by N(0, 0.03): through 7% of the time at 0.9 of the limit, half
    /// at it, 93% at 1.1 (armor/00 §3).</summary>
    [Fact]
    public void Plate_quality_is_the_ships_lot_and_the_shots_scatter()
    {
        var s = new SimSettings();
        var lots = RandomStream.Named(1, "plate lots");
        var her = lots.Derive(7);
        Assert.Equal(Terminal.PlateQuality(her, PlateKind.SideArmour, 0, s), Terminal.PlateQuality(lots.Derive(7), PlateKind.SideArmour, 0, s));
        Assert.NotEqual(Terminal.PlateQuality(her, PlateKind.SideArmour, 0, s), Terminal.PlateQuality(her, PlateKind.SideArmour, 1, s));
        Assert.NotEqual(Terminal.PlateQuality(her, PlateKind.SideArmour, 0, s), Terminal.PlateQuality(lots.Derive(8), PlateKind.SideArmour, 0, s));

        const int N = 20_000;
        var shots = RandomStream.FromSeed(3);
        double sum = 0, sum2 = 0;
        int[] through = new int[3];
        double[] at = [0.9, 1.0, 1.1];
        for (int k = 0; k < N; k++)
        {
            double q = Terminal.PlateQuality(lots.Derive((ulong)k), PlateKind.ArmourDeck, 2, s);
            (sum, sum2) = (sum + q, sum2 + q * q);
            double f = q * (1 + s.PlateShotSigma * shots.NextGaussian());
            for (int j = 0; j < 3; j++)
                through[j] += at[j] >= f ? 1 : 0;
        }
        double mean = sum / N, sd = Math.Sqrt(sum2 / N - mean * mean);
        output.WriteLine($"lots: mean {mean:0.000}, sd {sd:0.000}; through at 0.9/1.0/1.1: " + string.Join(" / ", through.Select(t => $"{100.0 * t / N:0.0}%")));
        Assert.InRange(mean, 0.995, 1.005);
        Assert.InRange(sd, 0.057, 0.063);
        Assert.InRange(100.0 * through[0] / N, 5, 9);
        Assert.InRange(100.0 * through[1] / N, 48, 52);
        Assert.InRange(100.0 * through[2] / N, 91, 95);
    }
}
