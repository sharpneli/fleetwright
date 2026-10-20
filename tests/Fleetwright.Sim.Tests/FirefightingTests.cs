using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>B4 of docs/battle/fire.md: firefighting, rough on purpose (decision 3): her parties from her complement, the
/// fire watch's hose, the parties sent to the worst fires; the fire main by section (dry past a cut on an unlooped main,
/// only the cut on a looped one, rigged past after the doctrine's time, as strong as her pumps); a hit breaking the main
/// more often on a small ship; one fire on a prepared Great War ship put out in minutes, and held without water.</summary>
public class FirefightingTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    static DamageControlDoctrine Doctrine(string name) => Settings.DamageControl.Single(d => d.Name == name);

    static ShipCondition Fresh(ShipClass c, string doctrine)
    {
        var k = new ShipCondition();
        k.Reset(c);
        (k.Readiness, k.DamageControl) = (Settings.Readiness[1], Doctrine(doctrine));
        return k;
    }

    [Fact]
    public void The_main_is_dry_past_a_cut_unless_looped_and_rigged_past_in_the_doctrines_time()
    {
        var c = Class("dreadnought");
        int pump = c.PumpSection, fore = c.Sections.Length - 1, aft = 0;
        output.WriteLine($"pump section {pump} of {c.Sections.Length}: " + string.Join(", ", Enumerable.Range(0, c.Rooms.Length)
            .Where(r => c.Rooms[r].Kind is RoomKind.BoilerRoom or RoomKind.EngineRoom).Select(r => $"{c.Rooms[r].Kind} {c.Rooms[r].X0:0}-{c.Rooms[r].X1:0} {c.Rooms[r].VolumeM3:0}")) +
            $"; sections {c.Sections[0].X0:0}..{c.Sections[^1].X1:0}");
        Assert.InRange(pump, 2, c.Sections.Length - 3);   // amidships
        var k = Fresh(c, "rn-1916");
        Assert.Equal(1, Fires.SectionWater(c, k, fore, 1000, Settings));
        k.MainCutMs[pump + 1] = 1000;
        double cutAt = Fires.SectionWater(c, k, pump + 1, 2000, Settings), past = Fires.SectionWater(c, k, fore, 2000, Settings);
        double behind = Fires.SectionWater(c, k, aft, 2000, Settings);
        output.WriteLine($"pumps in section {pump}; a cut in {pump + 1}: there {cutAt}, past it {past}, the other way {behind}");
        Assert.Equal((Settings.FireDryShare, Settings.FireDryShare, 1.0), (cutAt, past, behind));
        long rigged = 1000 + (long)(Doctrine("rn-1916").RestoreS * 1000);
        Assert.Equal((Settings.FireJumperShare, 1.0), (Fires.SectionWater(c, k, pump + 1, rigged, Settings), Fires.SectionWater(c, k, fore, rigged, Settings)));

        var looped = Fresh(c, "usn-1944");
        looped.MainCutMs[pump + 1] = 1000;
        Assert.Equal((Settings.FireDryShare, 1.0), (Fires.SectionWater(c, looped, pump + 1, 2000, Settings), Fires.SectionWater(c, looped, fore, 2000, Settings)));

        // the pumps on steam: her plant's power, a diesel's floor on the looped main
        (k.PowerCap, looped.PowerCap) = (0.2, 0.2);
        Assert.Equal(0.2, Fires.SectionWater(c, k, aft, 2000, Settings), 9);
        Assert.Equal(Doctrine("usn-1944").PumpFloor, Fires.SectionWater(c, looped, aft, 2000, Settings), 9);

        // never rigged past by the unorganised
        var none = Fresh(c, "unorganised-1890");
        none.MainCutMs[pump] = 1000;
        Assert.Equal(Settings.FireDryShare, Fires.SectionWater(c, none, fore, 10_000_000, Settings));
    }

    [Fact]
    public void A_hit_breaks_a_small_ships_main_more_often()
    {
        double Share(string design)
        {
            var c = Class(design);
            var f = new FloodModel(c);
            int cuts = 0;
            for (int i = 0; i < 2000; i++)
            {
                var k = Fresh(c, "usn-1942");
                var r = RandomStream.FromSeed((ulong)i);
                Fires.Hit(c, f, k, c.Sections.Length / 2, 1000, Settings, ref r);
                cuts += k.MainCutMs[c.Sections.Length / 2] > 0 ? 1 : 0;
            }
            output.WriteLine($"{design} ({new FloodModel(c).DisplacementT:0} t): main broken by {100.0 * cuts / 2000:0}% of hits");
            return cuts / 2000.0;
        }
        double dd = Share("destroyer"), bb = Share("dreadnought");
        Assert.True(dd > 1.5 * bb, $"{dd:P0} against {bb:P0}");
        Assert.InRange(bb, 0.05, 0.15);   // 0.25 × (2000 / ~18,000)^0.4 ≈ 0.1
    }

    [Fact]
    public void Her_parties_come_from_her_complement_and_go_to_the_worst_fire()
    {
        var c = Class("dreadnought");
        var f = new FloodModel(c);
        var k = Fresh(c, "rn-1916");
        int a = Enumerable.Range(0, c.Cells.Length).First(i => c.FireSpaces[i].Use == FireUse.Quarters && c.FireSpaces[i].AreaM2 > 30);
        Fires.Ignite(c, k, a, 0.3, Settings);
        var r = RandomStream.FromSeed(1);
        Fires.Step(c, f, k, 1, Settings, 1, ref r, 1000);
        double men = Enumerable.Range(0, k.PartyCount).Sum(p => k.Parties[p].Men);
        output.WriteLine($"{k.PartyCount} parties, {men:0} men of {f.Complement:0}; sent: " +
                         string.Join(", ", Enumerable.Range(0, k.PartyCount).Select(p => $"{k.Parties[p].Target}@{k.Parties[p].ArriveMs / 1000.0:0}s")));
        Assert.InRange(k.PartyCount, Settings.FirePartiesMin, Settings.FirePartiesMax);
        Assert.Equal(f.Complement * Doctrine("rn-1916").PartyShare, men, 6);
        Assert.Equal(Settings.FirePartiesAFire, Enumerable.Range(0, k.PartyCount).Count(p => k.Parties[p].Target == a));
        Assert.All(Enumerable.Range(0, k.PartyCount).Where(p => k.Parties[p].Target == a), p =>
            Assert.True(k.Parties[p].ArriveMs >= 1000 + Doctrine("rn-1916").MusterS * 1000, "mustered first"));

        // nobody fights for a doctrine of none
        var none = new ShipCondition();
        none.Reset(c);
        Fires.Ignite(c, none, a, 0.3, Settings);
        for (int t = 0; t < 600; t++)
            Fires.Step(c, f, none, 1, Settings, 1, ref r, 1000 + t * 1000);
        Assert.Equal(0, none.FireFlow[a].Lpm);
        Assert.Equal(0, none.PartyCount);
    }

    /// <summary>One fire lit in a ship's quarters, run on through the world: minutes until it was out (NaN: still burning
    /// after the hour), the fires lit in all, the water taken (t).</summary>
    (double OutMin, int Fires, double WaterT) OneFire(string design, string doctrine, string readiness, ulong seed, bool dry = false)
    {
        // "nobody": no watch, no parties, no turning to
        var nobody = Doctrine("unorganised-1890") with { Name = "nobody", WatchS = -1, PartyShare = 0, TurnTo = 0 };
        var s = Settings with { DamageControl = [.. Settings.DamageControl, nobody], DamageControlDefault = doctrine, ReadinessDefault = readiness };
        var w = new World(Start, seed, s);
        int cls = w.Ships.AddClass(Class(design));
        w.Apply(ShipSystem.Spawn(cls, 56, 4, 0, 10));
        var c = Class(design);
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1));
        if (dry)
            Array.Fill(k.MainCutMs, 1);   // every section's main cut from the start
        int cell = Enumerable.Range(0, c.Cells.Length).First(i => c.FireSpaces[i].Use == FireUse.Quarters && c.FireSpaces[i].AreaM2 > 30);
        w.Apply(DamageSystem.BurnCell(1, cell));
        int lit = 0;
        double outMin = double.NaN;
        for (int sec = 0; sec < 3600 && k.Status == ShipStatus.Afloat; sec += 10)
        {
            w.AdvanceTo(w.Time + 10_000);
            lit = Math.Max(lit, k.Fire.Count(x => x.Phase != BurnPhase.None));
            if (double.IsNaN(outMin) && k.Fires == 0)
                outMin = (sec + 10) / 60.0;
        }
        return (outMin, lit, k.WaterT);
    }

    [Fact]
    public void One_fire_on_a_prepared_Great_War_ship_is_out_in_minutes_and_unfought_it_runs_on()
    {
        var fought = Enumerable.Range(0, 12).Select(i => OneFire("dreadnought", "rn-1916", "prepared", (ulong)i)).ToList();
        var dry = Enumerable.Range(0, 12).Select(i => OneFire("dreadnought", "rn-1916", "prepared", (ulong)i, dry: true)).ToList();
        var unfought = Enumerable.Range(0, 12).Select(i => OneFire("dreadnought", "nobody", "prepared", (ulong)i)).ToList();
        string Show(List<(double OutMin, int Fires, double WaterT)> runs) =>
            $"out {runs.Count(x => !double.IsNaN(x.OutMin))} of {runs.Count}, median {runs.Select(x => double.IsNaN(x.OutMin) ? 99 : x.OutMin).Order().ElementAt(runs.Count / 2):0} min, " +
            $"spaces lit {runs.Average(x => x.Fires):0.0}, water {runs.Average(x => x.WaterT):0} t";
        output.WriteLine($"fought: {Show(fought)}");
        output.WriteLine($"mains cut: {Show(dry)}");
        output.WriteLine($"unfought: {Show(unfought)}");
        Assert.True(fought.Count(x => x.OutMin <= 30) >= 10, "the record's \"quickly out\"");
        Assert.True(fought.Average(x => x.Fires) < 3, "caught before it spread far");
        Assert.True(dry.Average(x => x.OutMin is var m && double.IsNaN(m) ? 99 : m) >= fought.Average(x => double.IsNaN(x.OutMin) ? 99 : x.OutMin),
            "buckets are slower than the main");
        Assert.True(unfought.Average(x => x.Fires) > fought.Average(x => x.Fires) + 1, "unfought it spreads");
    }
}
