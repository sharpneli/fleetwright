using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>The shell by target (docs/battle/shell-choice.md): a battery's guns hold the round they were loaded with
/// (A1), a load ordered takes over once it's fired (A2, decision 1: a change costs the round in the gun), and a
/// captain orders the kind his target calls for (A3).</summary>
public class ShellChoiceTests
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipClass Dreadnought => Fleet.Get("dreadnought").Class!;

    /// <summary>Two dreadnoughts 4 km apart, the first's main battery engaged on the second.</summary>
    static World Engaged()
    {
        var w = new World(Start, 3, Settings);
        int ci = w.Ships.AddClass(Dreadnought);
        w.Apply(ShipSystem.Spawn(ci, 56.5, 4.0, 0, 0, side: 0));
        w.Apply(ShipSystem.Spawn(ci, 56.536, 4.0, 0, 0, side: 1));
        w.Apply(GunnerySystem.Engage(1, 0, 2));
        return w;
    }

    /// <summary>What each of a battery's mounts fires, second by second until each that has fired has fired
    /// <paramref name="times"/> times: the kind its rounds went down by (a mount that never bore, none), by mount.</summary>
    static List<ShellKind>[] Fired(World w, int battery, int times, int limitS = 900)
    {
        var store = w.Ships.Store;
        int slot = store.SlotOf(1);
        var lo = store.Combat.Loadout(slot);
        var mounts = lo.Class.Links(lo.Class.Batteries[battery].Mounts).ToArray();
        var loads = lo.Loads(battery).ToArray();
        int[] Left(int m) => [.. Enumerable.Range(0, loads.Length).Select(k => store.Combat.LoadRounds(slot, m, k))];
        var before = mounts.Select(Left).ToArray();
        var seen = mounts.Select(_ => new List<ShellKind>()).ToArray();
        for (int s = 0; s < limitS && !(seen.Any(x => x.Count > 0) && seen.All(x => x.Count == 0 || x.Count >= times)); s++)
        {
            w.AdvanceTo(w.Time + 1000);
            for (int j = 0; j < mounts.Length; j++)
            {
                var now = Left(mounts[j]);
                for (int k = 0; k < loads.Length; k++)
                    if (now[k] < before[j][k])
                        seen[j].Add(lo.ShellTypes[loads[k].ShellType].Kind);
                before[j] = now;
            }
        }
        Assert.True(seen.Count(x => x.Count >= times) >= 2, "two mounts or more fired");
        return seen;
    }

    /// <summary>With no order a main battery fires AP (the magazine order); ordered HE, each mount fires the AP round
    /// in its guns first, then HE; ordered back to the magazine order, the HE in its guns, then AP (and one coming to
    /// bear only now, the AP it was loaded with at the start).</summary>
    [Fact]
    public void A_load_ordered_comes_up_after_the_round_in_the_gun()
    {
        var w = Engaged();
        foreach (var m in Fired(w, 0, 1))
            Assert.All(m, k => Assert.Equal(ShellKind.Ap, k));

        w.Apply(GunnerySystem.Load(1, 0, ShellKind.He));
        Assert.Equal(ShellKind.He, w.Ships.Store.Combat.Ordered(w.Ships.Store.SlotOf(1), 0));
        var he = Fired(w, 0, 2);
        foreach (var m in he.Where(m => m.Count > 0))
        {
            Assert.Equal(ShellKind.Ap, m[0]);
            Assert.All(m.Skip(1), k => Assert.Equal(ShellKind.He, k));
        }

        w.Apply(GunnerySystem.Load(1, 0, null));
        var back = Fired(w, 0, 2);
        for (int j = 0; j < back.Length; j++)
            if (back[j].Count > 0)
            {
                Assert.Equal(he[j].Count > 0 ? ShellKind.He : ShellKind.Ap, back[j][0]);
                Assert.All(back[j].Skip(1), k => Assert.Equal(ShellKind.Ap, k));
            }
    }

    /// <summary>A kind the battery doesn't carry loads nothing of it: its guns go on by the magazine order.</summary>
    [Fact]
    public void A_kind_the_battery_lacks_leaves_the_magazine_order()
    {
        var w = Engaged();
        var lo = w.Ships.Store.Combat.Loadout(w.Ships.Store.SlotOf(1));
        var lacks = Enum.GetValues<ShellKind>().First(k => !lo.Loads(0).ToArray().Any(l => lo.ShellTypes[l.ShellType].Kind == k));
        w.Apply(GunnerySystem.Load(1, 0, lacks));
        foreach (var m in Fired(w, 0, 2))
            Assert.All(m, k => Assert.Equal(ShellKind.Ap, k));
    }

    /// <summary>A captain firing at will, his side's doctrine by target: a dreadnought's main battery on a destroyer
    /// comes round to HE (AP passes through her), on another dreadnought it stays on AP; by the magazine order nothing
    /// is ordered.</summary>
    [Theory]
    [InlineData("destroyer", ShellUse.ByTarget, ShellKind.He)]
    [InlineData("dreadnought", ShellUse.ByTarget, null)]
    [InlineData("destroyer", ShellUse.MagazineOrder, null)]
    public void A_captain_loads_what_his_target_calls_for(string target, ShellUse use, ShellKind? loads)
    {
        var w = new World(Start, 5, Settings);
        var ai = new AiSystem(w);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(Dreadnought), 56.5, 4.0, 90, 0, side: 0));
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(Fleet.Get(target).Class!), 56.554, 4.0, 90, 0, side: 1));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Shells, (double)use));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Shells, (double)ShellUse.MagazineOrder));
        w.AdvanceTo(w.Time + 3 * 60_000);
        var combat = w.Ships.Store.Combat;
        int slot = w.Ships.Store.SlotOf(1);
        Assert.Equal(2, combat.Target(slot, 0));
        Assert.Equal(loads, combat.Ordered(slot, 0));
    }

    /// <summary>The player's kind for a ship's main guns holds against her captain (A5, decision 3): HE on a battleship,
    /// where he'd keep AP, until her main guns are out of it; then the choice is his again (AP), and what ran out shows.
    /// Her turrets all on the centreline, so all of them bear and spend it (a wing turret that never bears keeps its
    /// rounds, and so the battery its kind).</summary>
    [Fact]
    public void The_players_kind_holds_until_it_runs_out()
    {
        var w = new World(Start, 5, Settings);
        var ai = new AiSystem(w);
        var c = Fleet.Get("super_dreadnought").Class!;
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56.5, 4.0, 90, 0, side: 0));
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56.554, 4.0, 90, 0, side: 1));
        var errors = new List<string>();
        var few = c.Load(LoadoutSpec.Parse("main:ap@0.97+he@0.03"), errors);
        Assert.True(few != null, string.Join("; ", errors));
        w.Apply(ShipSystem.Load(1, w.Ships.Registry.AddLoadout(few!)));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Shells, (double)ShellUse.ByTarget));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Shells, (double)ShellUse.MagazineOrder));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(2)));   // a target that doesn't sink her first
        w.Apply(ai.Orders.Shells(Unit.Ship(1), ShellKind.He));
        var combat = w.Ships.Store.Combat;
        int slot = w.Ships.Store.SlotOf(1);
        w.AdvanceTo(w.Time + 60_000);
        Assert.Equal(ShellKind.He, combat.Ordered(slot, 0));
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        int k = snap.Ships.IndexOf(1);
        Assert.Equal((byte)(ShellKind.He + 1), snap.Ships.ShellHeld[k]);
        Assert.Equal((byte)(ShellKind.He + 1), snap.Ships.BatteryOrdered[snap.Ships.BatteryStart[k]]);

        // her few HE rounds spent: the choice is her captain's again, and he takes AP on a dreadnought
        for (int i = 0; i < 30 && snap.Ships.ShellSpent[k] == 0; i++)
        {
            w.AdvanceTo(w.Time + 60_000);
            w.Fill(snap, truth: true);
            k = snap.Ships.IndexOf(1);
        }
        Assert.Equal((byte)(ShellKind.He + 1), snap.Ships.ShellSpent[k]);
        Assert.Equal(0, snap.Ships.ShellHeld[k]);
        w.AdvanceTo(w.Time + 10_000);
        Assert.Equal(ShellKind.Ap, combat.Ordered(slot, 0));
    }
}
