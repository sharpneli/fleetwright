using Fleetwright.Shipgen;
using System.Reflection;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Explosions in time (docs/battle/workers.md ET): an explosion's gas is a process on her, stepped by the world on its own
/// lattice and landing on her as it goes, not worked to its end at one moment. A magazine fire takes its seconds; a burst's own
/// moment has only its near field; the world is the same however its clock is stepped, in either tier; her gas goes down with her;
/// and every field of a live network is in the hash or named as not needing to be.</summary>
public class GasInTimeTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);

    static World Make(int ships = 1, ShipClass? cls = null)
    {
        var w = new World(Start, 3, Settings);
        int c = w.Ships.AddClass(cls ?? Fleet.Get("dreadnought").Class!);
        for (int i = 0; i < ships; i++)
            w.Apply(ShipSystem.Spawn(c, 56, 4 + 0.05 * i, 0, 0));
        w.AdvanceTo(w.Time + 1000);
        return w;
    }

    static ShipCondition Her(World w, int entity = 1) => w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(entity));

    [Fact]
    public void A_magazine_fire_takes_its_own_time_and_lands_as_it_burns()
    {
        var w = Make();
        var k = Her(w);
        long t0 = w.Time.Ms;
        w.Apply(DamageSystem.Ignite(1));
        Assert.NotNull(k.Gas);
        Assert.Equal(0, k.Killed + k.Wounded);   // alight, and nothing of it on her yet
        // step it on in slices: what it does comes over its seconds, not at once
        int landings = 0;
        double was = 0;
        long end = t0;
        for (long t = t0 + Settings.GasSliceMs; t <= t0 + 240_000 && k.Gas is not null && k.Status == ShipStatus.Afloat; t += Settings.GasSliceMs)
        {
            w.AdvanceTo(new WorldTime(t + 1));
            double now = k.Killed + k.Wounded + Sum(k.JoinOpenM2) + Sum(k.EdgeLost) + Sum(k.FaceLost);
            landings += now > was ? 1 : 0;
            (was, end) = (now, t);
        }
        output.WriteLine($"her gas ran {(end - t0) / 1000.0:0.0} s, landing more on her in {landings} slices; killed {k.Killed}, wounded {k.Wounded}, {k.Status}");
        Assert.True(end - t0 >= 3000, "a magazine's race takes seconds");
        Assert.True(landings >= 5, "what it does lands slice by slice");
        Assert.True(k.Killed > 0);
        if (k.Status == ShipStatus.Afloat)
            Assert.Null(k.Gas);   // done, and back to its pool
        Assert.Equal(1, w.Damage.MagazineCounts.ByHit.Sum() + w.Damage.MagazineCounts.ByFire.Sum());
    }

    static double Sum(double[] a)
    {
        double s = 0;
        foreach (double x in a)
            s += x;
        return s;
    }

    [Fact]
    public void Her_gas_is_the_same_however_the_clock_is_stepped()
    {
        var a = Make();
        var b = Make();
        a.Apply(DamageSystem.Ignite(1));
        b.Apply(DamageSystem.Ignite(1));
        var end = a.Time + 40_000;
        a.AdvanceTo(end);
        for (var t = b.Time; t < end; t += 37)
            b.AdvanceTo(t);
        b.AdvanceTo(end);
        Assert.Equal(a.ComputeHash(), b.ComputeHash());
        // and half way through, while she still burns
        var c = Make();
        c.Apply(DamageSystem.Ignite(1));
        c.AdvanceTo(c.Time + 2_050);
        Assert.NotNull(Her(c).Gas);
        var d = Make();
        d.Apply(DamageSystem.Ignite(1));
        for (int i = 0; i < 41; i++)
            d.AdvanceTo(d.Time + 50);
        Assert.Equal(c.ComputeHash(), d.ComputeHash());
    }

    [Fact]
    public void In_the_coarse_tier_her_gas_takes_its_time_too()
    {
        var w = Make();
        w.Apply(Command.SetSpeed(Settings.CoarseMinSpeed * 2));
        w.AdvanceTo(w.Time + 1000);
        Assert.True(w.Coarse);
        var k = Her(w);
        w.Apply(DamageSystem.Ignite(1));
        w.AdvanceTo(w.Time + 1500);
        Assert.NotNull(k.Gas);   // not worked to its end at the moment it lit
        Assert.True(k.Killed > 0, "and landing as it goes");
        w.AdvanceTo(w.Time + 240_000);
        Assert.True(k.Gas is null || k.Status != ShipStatus.Afloat);
    }

    [Fact]
    public void Her_gas_goes_down_with_her_and_its_network_is_used_again()
    {
        // a class of her own: the pools are by class, and the shared dreadnought's is in use by tests running beside this
        var w = Make(2, ShipClassCompiler.Compile(Fleet.Get("dreadnought").Ship!));
        var k = Her(w);
        w.Apply(DamageSystem.Ignite(1));
        w.AdvanceTo(w.Time + 300);
        var net = k.Gas;
        Assert.NotNull(net);
        w.Apply(DamageSystem.Sink(1, LossKind.Foundered));
        Assert.Null(k.Gas);
        w.Apply(DamageSystem.Ignite(2));
        Assert.Same(net, Her(w, 2).Gas);   // her class's pool had it back
    }

    [Fact]
    public void A_burst_has_only_its_near_field_at_its_moment_and_its_gas_after()
    {
        var w = Make();
        var k = Her(w);
        var cls = k.Class;
        int cell = Enumerable.Range(0, cls.Cells.Length).First(i => cls.Cells[i].Room >= 0 && cls.Rooms[cls.Cells[i].Room].Kind == RoomKind.EngineRoom);
        var x = Explosives.Get("TNT")!;
        var f = new ShellTypeRow { CalibreMm = 305, FillerKg = 60, FillerRe = x.Re ?? 1, FillerQDet = x.QDet ?? 4.5, FillerQComb = x.QComb ?? 15, FillerAirM3Kg = x.AirM3Kg ?? 0, FillerEta = x.Eta ?? 1 };
        var b = cls.Cells[cell].Box;
        var at = new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, (b.Base + b.Top) / 2);
        var net = GasNetwork.Of(k, Settings, w.Time.Ms);
        var now = net.AddBurst(cell, at, 60, f, NearField.Charge(Math.Cbrt(60.0), 1, 0, Settings));
        double open = Sum(k.JoinOpenM2), lost = Sum(k.EdgeLost);
        Assert.False(net.Done);
        Assert.Equal(0, net.Report().Bulkheads + net.Report().Decks + net.Report().Doors);   // the gas hasn't done anything yet
        net.AdvanceTo(w.Time.Ms + 200);
        var r = RandomStream.Named(1, "gas in time");
        var fx = net.Land(w.Time.Ms + 200, ref r);
        var gas = net.Report();
        output.WriteLine($"at once: {now.Holed} holed, {open:0.0} m² open; 200 ms on: {gas.Bulkheads} bulkheads, {gas.Decks} decks, {gas.Doors} doors, {Sum(k.JoinOpenM2):0.0} m² open, killed {fx.Killed}");
        Assert.True(gas.Bulkheads + gas.Decks + gas.Doors > 0, "its gas gave something after its moment");
        Assert.True(Sum(k.JoinOpenM2) > open || Sum(k.EdgeLost) > lost);
        net.Release();
    }

    /// <summary>Every instance field of a network is in <see cref="GasNetwork.Hash"/>, or named here as not needing to be: what's the
    /// class's (bound once), what a step works out afresh, a run's ids and stamps (compared, never carried), the bench's views. A field
    /// added and neither hashed nor named here fails, so a new piece of the gas's state can't slip out of the replay check.</summary>
    [Fact]
    public void Every_field_of_her_gas_is_hashed_or_named()
    {
        string[] hashed =
        [
            "startMs", "time", "started", "finished", "lastSourceS", "stepDt", "quietS", "stepsN", "sourceCell", "armed", "moving", "n", "links",
            "nStows", "stowsLanded", "burned", "fastKg", "peakSource", "whipKg", "whipJ", "whipPeak", "whipSince", "res", "region", "v", "m", "u", "p",
            "rt", "vent", "lagged", "felt", "hottest", "hyd", "dose", "spilled", "nTouched", "touched", "feltLanded", "hotLanded", "open",
            "opened", "edges", "faceBack", "faceDry", "faces", "stows", "linkFrom", "linkTo", "linkMount", "linkAt", "linkArea", "linkOpen",
            "rounds", "ventedMount", "liftedMount", "liftMPa", "ventLanded", "liftLanded", "trainStamp", "edgeStamp", "faceStamp", "stamp", "touchStamp",
        ];
        string[] named =
        [
            // the class's, bound once
            "c", "canon", "area", "mm", "heldMm", "acrossHere", "acrossThere", "ductile", "hull", "partNode",
            // her condition and the settings it reads
            "k", "s",
            // worked out afresh by each step or advance, or once a run from her condition: the step's start, its lags, the soonest a
            // dose lights, the waterline, the liquid, panels' masses and rupture pressures, the flash discipline, the hull's period and
            // the choked flow's constant
            "pStart", "lag", "plainLag", "soonest", "water", "liq", "heavyJ", "heavyStamp", "faceHeavy", "faceHeavyStamp", "ruptureJ", "run",
            "ruptureClass", "ruptureSettings", "flash", "period", "chokeK",
            // a run's ids and its explosions' walks, compared never carried; a burst's own moment
            "id", "nearId", "nearStamp", "walk", "walkStamp", "nWalk", "intoBubble", "faceNear", "nearShare", "walkShare", "commits", "edgeDrawn", "faceDrawn",
            // a part's burst, run at once on a thread's network, never a live one
            "partM2", "partMm", "partWalls",
            // a landing's scratch
            "rose",
            // reports, read by no step
            "SourceCause", "MagazineCause", "Girder", "SourceAt", "sourced",
        ];
        var fields = typeof(GasNetwork).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(fi => fi.Name).ToArray();
        var missing = fields.Except(hashed).Except(named).ToArray();
        var gone = hashed.Concat(named).Except(fields).ToArray();
        Assert.True(missing.Length == 0, "neither hashed nor named: " + string.Join(", ", missing));
        Assert.True(gone.Length == 0, "named but no longer a field: " + string.Join(", ", gone));
    }
}
