using System.Diagnostics;
using Fleetwright.Designer;
using Fleetwright.Shipgen;

namespace Fleetwright.Tests;

/// <summary>The Overview tab's parts: the probes build exactly what a build builds, a newer batch supersedes an older
/// one, and the broad-stroke levers move whole systems without breaking the design.</summary>
public class OverviewTests
{
    static Design Load(string name) => Design.Load(Paths.Shipgen("designs", name + ".json"));

    static DesignProbes.Batch Wait(DesignProbes.Batch b)
    {
        var sw = Stopwatch.StartNew();
        while (!b.Done && sw.Elapsed < TimeSpan.FromMinutes(2))
            Thread.Sleep(10);
        Assert.True(b.Done, "the batch didn't finish");
        return b;
    }

    /// <summary>A probe is an ordinary build: the same numbers, a skipped slot stays skipped, an invalid design fails.</summary>
    [Fact]
    public void Probes_are_builds()
    {
        var d = Load("dreadnought");
        var faster = Knobs.Speed.Apply(d, 22);
        var invalid = d with { SpeedKn = -5 };
        using var probes = new DesignProbes();
        var b = Wait(probes.Start("test", [d, null, faster, invalid]));
        var own = ShipDesign.Build(faster).Report.Results;
        Assert.True(b.TryGet(2, out var r));
        Assert.Equal(own.StandardDisplacementT, r.StdT);
        Assert.Equal(own.GmFullM, r.GmM);
        Assert.Equal(DesignProbes.State.Built, b.StateOf(0));
        Assert.Equal(DesignProbes.State.Skipped, b.StateOf(1));
        Assert.Equal(DesignProbes.State.Failed, b.StateOf(3));
    }

    /// <summary>Starting a batch while one runs leaves the runner on the new one, which finishes; the old one is let go.</summary>
    [Fact]
    public void A_newer_batch_supersedes()
    {
        var d = Load("bismarck");
        using var probes = new DesignProbes();
        var old = probes.Start("old", Enumerable.Range(0, 24).Select(i => (Design?)Knobs.Belt.Apply(d, 200 + 5 * i)).ToArray());
        var key = new object();
        var now = probes.Start(key, [d, Knobs.Belt.Apply(d, 330)]);
        Assert.Same(now, probes.Current);
        Wait(now);
        Assert.Same(key, probes.Current!.Key);
        Assert.True(old.Finished < old.Count || old.Done);   // the old batch stopped handing out work (or was already through)
        probes.Cancel();
        Assert.Null(probes.Current);
    }

    [Fact]
    public void End_belts_step_together()
    {
        var d = Load("dreadnought");   // 152 fore, 102 aft
        Assert.Equal(127, Knobs.EndBelts.Get(d));
        var x = Knobs.EndBelts.Apply(d, 137);
        Assert.Equal(162, x.Armour!.EndBelts!.Fore!.Mm);
        Assert.Equal(112, x.Armour!.EndBelts!.Aft!.Mm);
        Assert.Empty(ShipDesign.Validate(x, limits: true));
        Assert.False(Knobs.EndBelts.AppliesTo(Load("heavy_cruiser")));   // no end belts: nothing to step
    }

    [Fact]
    public void Main_levers_move_every_battery()
    {
        var d = Load("bismarck") with
        {
            Main = [new BatteryInput { CalibreMm = 305, CalibreLength = 50, Barrels = 2, Fore = 2, Aft = 1, ArmourMm = 280 },
                    new BatteryInput { CalibreMm = 234, CalibreLength = 45, Barrels = 2, Fore = 0, Aft = 1, ArmourMm = 200 }],
        };
        var x = Knobs.MainCalibres.Apply(d, 315);
        Assert.Equal([315.0, 244.0], x.Main!.Select(b => b.CalibreMm!.Value));
        var y = Knobs.MainFaces.Apply(d, 270);
        Assert.Equal([270.0, 190.0], y.Main!.Select(b => b.ArmourMm!.Value));
    }

    /// <summary>Scaling armour scales every thickness the design gives and touches nothing else; 1.0 is the identity.</summary>
    [Fact]
    public void Armour_scales_every_plate()
    {
        var d = Load("bismarck");
        Assert.Equal(d.ToJson(), Knobs.ScaleArmour(d, 1).ToJson());
        var x = Knobs.ScaleArmour(d, 0.9);
        Assert.Equal(288, x.Armour!.BeltMm);
        Assert.Equal(153, x.Armour.BeltBottomMm);
        Assert.Equal(198, x.Armour.BulkheadMm);
        Assert.Equal(130, x.Armour.UpperBelt!.Mm);   // 145 × 0.9 rounds down: 130.4999…
        Assert.Equal([45.0, 90.0], x.Armour.Decks!.Select(k => k.Mm!.Value));
        Assert.Equal(324, x.Main![0].ArmourMm);
        Assert.Equal(d.SpeedKn, x.SpeedKn);
        Assert.Equal(d.Secondary![0].ArmourMm, x.Secondary![0].ArmourMm);   // gun shields aren't protection plates
        Assert.Empty(ShipDesign.Validate(x, limits: true));
    }
}
