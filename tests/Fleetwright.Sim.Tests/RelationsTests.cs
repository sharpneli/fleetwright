using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>Relations (docs/battle/plan.md step 3.1a): a war/peace table between sides, world state changed by a logged
/// command. Sides at peace pass each other on rails; war declared with them in range takes them off rails at once, and
/// peace made lets them go back on.</summary>
public class RelationsTests
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lon = 4.0, Kn = 15;

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    /// <summary>Two ships on one meridian steaming at each other, sides 1 and 2, the given relation set before they
    /// spawn; both on rails a minute on.</summary>
    static World HeadOn(Relation r)
    {
        var w = new World(Start, 5, Settings);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(Command.SetRelation(1, 2, r));
        w.Apply(ShipSystem.Spawn(c, 55, Lon, 0, Kn, 1));
        w.Apply(ShipSystem.Spawn(c, 57, Lon, 180, Kn, 2));
        Run(w, 60);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails);
        return w;
    }

    static double Apart(World w) => Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position);

    [Fact]
    public void The_table_is_symmetric_and_a_side_is_never_its_own_enemy()
    {
        var w = new World(Start, 5, Settings);
        ulong before = w.ComputeHash();
        Assert.True(w.Relations.Hostile(0, 1) && !w.Relations.Hostile(3, 3));
        w.Apply(Command.SetRelation(3, 1, Relation.Peace));
        Assert.Equal(Relation.Peace, w.Relations.Of(1, 3));
        Assert.False(w.Relations.Hostile(1, 3) || w.Relations.Hostile(3, 1));
        Assert.True(w.Relations.Hostile(1, 2));
        Assert.NotEqual(before, w.ComputeHash());
        w.Apply(Command.SetRelation(1, 3, Relation.War));
        Assert.Equal(before, w.ComputeHash());   // all at war again: the default table, which hashes to nothing
        Assert.Equal(2, w.Relations.Version);
        Assert.Throws<ArgumentException>(() => w.Apply(new Command(0, (ushort)WorldCommand.SetRelation, 1, A: 7, B: 2)));
    }

    [Fact]
    public void Sides_at_peace_pass_each_other_on_rails()
    {
        var w = HeadOn(Relation.Peace);
        Assert.False(w.Ships.Opposed);
        double closest = Apart(w);
        for (int m = 0; m < 6 * 60; m++)
        {
            Run(w, 60);
            Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails, $"at {m} min, {Apart(w) / 1000:F0} km apart");
            closest = Math.Min(closest, Apart(w));
        }
        Assert.True(closest < 5_000, $"they passed {closest:F0} m apart");
    }

    [Fact]
    public void War_declared_in_range_takes_both_off_rails_at_once_and_peace_lets_them_back_on()
    {
        var w = HeadOn(Relation.Peace);
        while (Apart(w) > 30_000)
            Run(w, 60);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails);
        w.Apply(Command.SetRelation(2, 1, Relation.War));
        Assert.True(w.Ships.Opposed);
        long declared = w.Time.Ms;
        w.AdvanceTo(w.Time + 1);
        Assert.False(Ship(w, 1).OnRails || Ship(w, 2).OnRails);
        Assert.Equal(declared, w.CommandLog[^1].At.Ms);

        Run(w, 600);
        Assert.False(Ship(w, 1).OnRails || Ship(w, 2).OnRails, "at war within range: on the fine tick");
        w.Apply(Command.SetRelation(1, 2, Relation.Peace));
        Assert.False(w.Ships.Opposed);
        Run(w, 600);
        Assert.True(Ship(w, 1).OnRails && Ship(w, 2).OnRails, $"at peace {Apart(w) / 1000:F0} km apart: back on rails");
    }

    [Fact]
    public void Relations_changed_mid_run_replay_from_the_log()
    {
        var w = HeadOn(Relation.Peace);
        Run(w, 3000);
        w.Apply(Command.SetRelation(1, 2, Relation.War));
        Run(w, 3600);
        var end = w.Time;

        var r = new World(Start, 5, Settings);
        r.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        foreach (var sc in w.CommandLog)
        {
            r.AdvanceTo(sc.At);
            r.Apply(sc.Command);
        }
        r.AdvanceTo(end);
        Assert.Equal(w.ComputeHash(), r.ComputeHash());
    }
}
