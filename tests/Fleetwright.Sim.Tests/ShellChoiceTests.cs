using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>The shell by target (docs/battle/shell-choice.md): a battery's guns hold the round they were loaded with
/// (A1), and a load ordered takes over once it's fired (A2, decision 1: a change costs the round in the gun).</summary>
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

    /// <summary>What each of a battery's mounts that bear fires, second by second until each that has fired has fired
    /// <paramref name="times"/> times: the kind its rounds went down by (a mount that never bore, none).</summary>
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
        return [.. seen.Where(x => x.Count > 0)];
    }

    /// <summary>With no order a main battery fires AP (the magazine order); ordered HE, each mount fires the AP round
    /// in its guns first, then HE; ordered back to the magazine order, the HE in its guns, then AP.</summary>
    [Fact]
    public void A_load_ordered_comes_up_after_the_round_in_the_gun()
    {
        var w = Engaged();
        foreach (var m in Fired(w, 0, 1))
            Assert.All(m, k => Assert.Equal(ShellKind.Ap, k));

        w.Apply(GunnerySystem.Load(1, 0, ShellKind.He));
        Assert.Equal(ShellKind.He, w.Ships.Store.Combat.Ordered(w.Ships.Store.SlotOf(1), 0));
        foreach (var m in Fired(w, 0, 2))
        {
            Assert.Equal(ShellKind.Ap, m[0]);
            Assert.All(m.Skip(1), k => Assert.Equal(ShellKind.He, k));
        }

        w.Apply(GunnerySystem.Load(1, 0, null));
        foreach (var m in Fired(w, 0, 2))
        {
            Assert.Equal(ShellKind.He, m[0]);
            Assert.All(m.Skip(1), k => Assert.Equal(ShellKind.Ap, k));
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
}
