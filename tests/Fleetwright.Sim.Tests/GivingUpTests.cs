using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A ship given up (docs/battle/striking.md SK1): her colours struck (her guns stood down, her men still at the
/// pumps), her men ordered off (nothing worked, she floods as her holes let her), scuttled (her charges go and the
/// flooding model sinks her, research/simulation/striking-colours.md §4.2: 10-30 min from the order); the log says so,
/// and a replay is the same.</summary>
public class GivingUpTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>Two ships of a design side by side in a group (entities 1 and 2), stopped at sea.</summary>
    static World Pair(string design = "light_cruiser")
    {
        var w = new World(Start, 3, Settings);
        int c = w.Ships.AddClass(Fleet.Get(design).Class!);
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, 10, side: 0));
        w.Apply(ShipSystem.Spawn(c, 56, 4.01, 0, 10, side: 0));
        w.Apply(GroupSystem.Create(1));
        w.Apply(GroupSystem.Add(w.Groups.Of(1)!.GroupId, 2));
        w.AdvanceTo(w.Time + 10_000);
        return w;
    }

    static ShipCondition K(World w, int e = 1) => w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(e));

    static int Logged(World w, int e)
    {
        int n = 0;
        for (int i = 0; i < w.CombatLog.Count; i++)
            if (w.CombatLog.Newest(i) is { Kind: CombatEvent.GaveUp } x && x.Target == e)
                n++;
        return n;
    }

    [Fact]
    public void Her_colours_struck_her_guns_stand_down_and_her_men_stay()
    {
        var w = Pair();
        w.Apply(DamageSystem.StrikeColours(1));
        w.AdvanceTo(w.Time + 60_000);
        var k = K(w);
        Assert.True(k.ColoursDown);
        Assert.False(k.Abandoned);
        Assert.Equal(0, k.MountsWorking);
        Assert.All(k.TubeWorks, t => Assert.False(t));
        Assert.Equal(1, k.PowerCap, 3);   // her plant still worked: a prize can steam
        Assert.True(k.Conned);
        Assert.Null(w.Groups.Of(1));
        Assert.Equal(1, Logged(w, 1));
        w.Apply(DamageSystem.StrikeColours(1));   // once
        w.AdvanceTo(w.Time + 1000);
        Assert.Equal(1, Logged(w, 1));
    }

    [Fact]
    public void Abandoned_nothing_is_worked_and_she_floods_as_her_holes_let_her()
    {
        var w = Pair();
        var c = K(w).Class;
        w.Apply(DamageSystem.FloodRooms(1, RoomKind.BoilerRoom, 1));
        w.Apply(DamageSystem.FloodRooms(2, RoomKind.BoilerRoom, 1));
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(DamageSystem.Abandon(1));
        w.AdvanceTo(w.Time + 30 * 60_000);
        var (a, b) = (K(w), K(w, 2));
        output.WriteLine($"abandoned: {a.WaterT:0} t, power {a.PowerCap:0.00}, {a.Status}; manned: {b.WaterT:0} t, power {b.PowerCap:0.00}");
        Assert.True(a.Abandoned);
        Assert.Equal(0, a.MountsWorking);
        Assert.Equal(0, a.PowerCap);
        Assert.False(a.Conned);
        Assert.True(a.OffMs > a.AbandonMs, "her men take a while to get off");
        double men = Math.Max(w.Damage.Flood(c).Complement - a.Killed, 0);
        Assert.Equal(a.AbandonMs + (long)((Settings.AbandonLeadS + men * Settings.AbandonManS) * 1000), a.OffMs);
        Assert.True(a.WaterT >= b.WaterT - 1, "nobody at her pumps or her holes");
    }

    [Fact]
    public void Scuttled_her_charges_sink_her_in_about_the_records_time()
    {
        foreach (string design in new[] { "light_cruiser", "destroyer", "armoured_cruiser" })
        {
            var w = Pair(design);
            w.Apply(DamageSystem.Scuttle(1));
            long order = w.Time.Ms;
            long going = 0;
            for (int i = 0; i < 180 && going == 0; i++)
            {
                w.AdvanceTo(w.Time + 30_000);
                if (w.Ships.Store.SlotOf(1) < 0 || K(w).Status == ShipStatus.GoingDown)
                    going = w.Time.Ms;
            }
            double min = (going - order) / 60_000.0;
            output.WriteLine($"{design}: going down {min:0.0} min after the order{(going == 0 ? " (afloat after 90 min)" : "")}");
            Assert.True(going > 0, $"{design} sank");
            Assert.InRange(min, 12, 60);   // the charges 10 min after the order, then the water [§4.2: 10-30 min, Dresden ~30]
            Assert.True(K(w, 2).Status == ShipStatus.Afloat);
        }
    }

    [Fact]
    public void A_scuttling_replays_the_same()
    {
        ulong Run()
        {
            var w = Pair();
            w.Apply(DamageSystem.StrikeColours(1));
            w.AdvanceTo(w.Time + 60_000);
            w.Apply(DamageSystem.Scuttle(1));
            w.AdvanceTo(w.Time + 20 * 60_000);
            return w.ComputeHash();
        }
        Assert.Equal(Run(), Run());
    }
}
