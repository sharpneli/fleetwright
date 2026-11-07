using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>High-explosive stores aboard (docs/battle/explosions.md EX6c; research 15): Held's rule through a case against 15 §2.1's
/// table; a torpedo mount's warheads going off set off the next mount by their pieces (a neighbour's own fragments), a shell's never
/// do; a fire cooks them off in its time; wet guncotton nothing sets off.</summary>
public class StoreTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static double E(double mildMm) => Math.Pow(mildMm / 25.4, Fragments.ThorA);

    [Fact]
    public void Held_through_a_case_sorts_the_attackers_as_research_15_does()
    {
        double e = E(6);
        // 15 §2.1, a 6 mm warhead: a neighbour's 30 g at 2,800 m/s sets off TNT (2.25), a 6 in HE's 25 g at 900 doesn't (0.11), nor its
        // 200 g at 800 (0.31); Torpex (16) is on the line for that 200 g (1.01)
        Assert.True(FragmentBurst.HeldKg(0, 2800, e, 52) <= 0.03);
        Assert.True(FragmentBurst.HeldKg(0, 900, e, 52) > 0.025);
        Assert.True(FragmentBurst.HeldKg(0, 800, e, 52) > 0.2);
        double torpex = FragmentBurst.HeldKg(0, 800, e, 16);
        output.WriteLine($"Torpex at 800 m/s through 6 mm: {1000 * torpex:0} g");
        Assert.InRange(torpex, 0.12, 0.3);
        // bare Comp B (16): 1 g needs ~1,780 m/s, 1 kg ~565 (15 §2.1's first table)
        Assert.InRange(FragmentBurst.HeldKg(0, 1800, 0, 16), 0.0008, 0.0012);
        Assert.InRange(FragmentBurst.HeldKg(0, 570, 0, 16), 0.85, 1.1);
    }

    static (World W, int Entity, int Slot, ShipCondition K) Ship(string design, ulong seed, SimSettings? s = null)
    {
        var cls = Fleet.Get(design).Class!;
        var w = new World(Start, seed, s ?? Settings);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(cls), 56, 4, 0, 0));
        int entity = w.Ships.NewestEntity, slot = w.Ships.Store.SlotOf(entity);
        return (w, entity, slot, w.Ships.Store.Combat.Condition(slot));
    }

    [Fact]
    public void A_mount_going_off_sets_off_the_next_by_its_pieces()
    {
        var cls = Fleet.Get("destroyer").Class!;
        Assert.True(cls.Launchers.Length >= 2);
        int next = 0;
        const int Runs = 10;
        for (ulong seed = 1; seed <= Runs; seed++)
        {
            var (w, entity, _, k) = Ship("destroyer", seed);
            ref readonly var row = ref cls.Launchers[0];
            w.Damage.Strike(w, new Hit
            {
                Ship = entity, Ms = w.Time.Ms, Point = row.StoreAt, Direction = row.StoreAxis, Source = HitSource.Store, Store = 0,
                Shell = cls.Torpedoes[row.Torpedo].Warhead, ShellType = -1,
            });
            Assert.Equal(0, k.Warheads[0]);   // gone with it
            next += k.StoreLit[1] || k.Warheads[1] == 0 ? 1 : 0;
        }
        output.WriteLine($"the next mount went {next} of {Runs}");
        Assert.True(next >= Runs * 0.7);
    }

    [Fact]
    public void A_shells_burst_beside_a_mount_doesnt_set_it_off()
    {
        // 15 §2.1: an HE shell's pieces through a TNT warhead's 6 mm case come to a tenth to a third of Held's line (Chōkai's)
        var cls = Fleet.Get("destroyer").Class!;
        var shell = Fleet.Get("light_cruiser").Class!.Loadout.ShellTypes.ToArray().First(t => t.Kind == ShellKind.He);
        ref readonly var row = ref cls.Launchers[0];
        int lit = 0;
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var (_, _, _, k) = Ship("destroyer", seed);
            var r = RandomStream.FromSeed(seed);
            var at = new P3(row.StoreAt.X + 2, row.StoreAt.Y, row.StoreAt.Z + 1);   // 2 m off, over the deck
            lit += Stores.Burst(cls, k, at, new P3(0, 0, -1), 0, shell.Fragments!.High, 1, shell.FillerKg * shell.FillerRe, -1, -1, 0, -1, Settings, ref r);
        }
        Assert.Equal(0, lit);
    }

    [Fact]
    public void A_fire_cooks_them_off_in_its_time()
    {
        var cls = Fleet.Get("destroyer").Class!;
        var tp = cls.Torpedoes[cls.Launchers[0].Torpedo];
        // a 6 mm TNT case: research 15 §3.2's 5 mm 1.3/3.5/16 min, by its wall
        Assert.InRange(Stores.CookS(tp, 0, Settings) / 60, 1.4, 1.8);
        Assert.InRange(Stores.CookS(tp, 1, Settings) / 60, 3.8, 4.6);
        var s = Settings with { CookOffHighShare = 1 };
        double sum = 0;
        int n = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var (w, _, _, k) = Ship("destroyer", seed, s);
            var f = w.Damage.Flood(cls);
            int fs = cls.Launchers[0].StoreFire >= 0 ? cls.Launchers[0].StoreFire : cls.Launchers[0].StoreDeckFire;
            var r = RandomStream.FromSeed(seed);
            double t = 0;
            while (!k.StoreLit[0] && t < 4 * 3600)
            {
                k.Fire[fs] = new BurnState { Phase = BurnPhase.Developed, Q = 5 };
                Stores.Cook(cls, f, k, 10, s, ref r);
                t += 10;
            }
            Assert.True(k.StoreLit[0]);
            (sum, n) = (sum + t, n + 1);
        }
        output.WriteLine($"mean cook-off {sum / n / 60:0.0} min");
        Assert.InRange(sum / n / 60, 2, 30);
    }

    [Fact]
    public void A_deck_fire_by_a_mount_heats_its_warheads_but_burns_out_first()
    {
        // research 15 §3.2: TNT warheads in deck fires held (Kalk, Stormes: "no positive case ... by prolonged roasting"); the fire's
        // heat reaches them in her own steps, a fire in the open is hot smoke to them, and a destroyer's deck burns out in minutes
        var cls = Fleet.Get("destroyer").Class!;
        ref readonly var row = ref cls.Launchers[0];
        var s = Settings with { CookOffHighShare = 1 };
        int fs = row.StoreDeckFire;
        foreach (double settle in (double[])[300, 7200])
        {
            var (w, entity, _, k) = Ship("destroyer", 1, s);
            Assert.True(Fires.Ignite(cls, k, fs, 2, s));
            w.Damage.Settle(w, entity, settle);
            output.WriteLine($"after {settle / 60:0} min: fire {k.Fire[fs].Phase}; heat {k.StoreHeat[0]:0.00}, warheads {k.Warheads[0]}");
            if (settle < 600)
                Assert.True(k.StoreHeat[0] > 0.1);   // heating while it burns
            else
                Assert.Equal(0, k.StoreHeat[0]);    // cooled since
            Assert.Equal(cls.Launchers[0].Torpedoes, k.Warheads[0]);
            Assert.Equal(0, w.Damage.StoresSetOff + w.Damage.StoresBurned);
        }
    }

    [Fact]
    public void Wet_guncotton_takes_nothing()
    {
        var x = Fleetwright.Shipgen.Explosives.Get("Wet guncotton")!;
        Assert.Equal(0, x.HeldK);
        Assert.Equal(0, x.CookK);
    }
}
