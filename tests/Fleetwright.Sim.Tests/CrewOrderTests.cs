using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The captain's order of his men (docs/battle/crew.md N3a): by doctrine his secondaries' crews close up on the
/// engaged side and wait under cover on the other, or only a watch with no torpedo craft about; a station short of men
/// is refilled from the men under cover, who walk there.</summary>
public class CrewOrderTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false, HitsDoDamage = false };
    const double Lat = 56.0, Lon = 4.0;

    /// <summary>Her (entity 1) on 0° and an enemy dreadnought on her starboard beam at a range, the side's doctrine given.</summary>
    static (World W, AiSystem Ai) Make(ManGuns rule, double rangeM = 8_000)
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 0, 0));
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, rangeM);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(c, la, lo, 0, 0, 1));
        foreach (var field in new[] { DoctrineField.ManCasemates, DoctrineField.ManOpenGuns })
            w.Apply(ai.Orders.SideDoctrine(0, field, (double)rule));
        return (w, ai);
    }

    static ShipCondition Her(World w) => w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1));

    [Fact]
    public void In_range_the_engaged_side_closes_up_and_the_other_waits_under_cover()
    {
        var (w, _) = Make(ManGuns.InRange);
        w.AdvanceTo(w.Time + 90_000);
        var k = Her(w);
        var c = k.Class;
        var secondaries = Enumerable.Range(0, c.Mounts.Length).Where(m => c.Mounts[m].Battery >= 0 && !c.Batteries[c.Mounts[m].Battery].Chief && !c.Enclosed(m)).ToList();
        Assert.NotEmpty(secondaries);
        foreach (int m in secondaries)
        {
            bool bears = CombatStore.InArcs(c, c.Mounts[m], 90);
            output.WriteLine($"mount {m} at y {c.Mounts[m].Y:+0.0;-0.0}: {(k.ClosedUp[m] ? "closed up" : "under cover")}, {k.Covered[m]} under cover, bears {bears}");
            Assert.Equal(bears, k.ClosedUp[m]);
            if (!bears)
            {
                Assert.True(k.Covered[m] > 0);
                Assert.False(k.MountWorks[m]);
            }
        }
        foreach (int m in Enumerable.Range(0, c.Mounts.Length).Except(secondaries))
            Assert.True(k.ClosedUp[m]);   // the main battery always manned
    }

    [Fact]
    public void With_an_enemy_on_each_beam_both_sides_close_up()
    {
        var (w, ai) = Make(ManGuns.InRange);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 270);
        Earth.Advance(ref p, ref f, 8_000);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(c, la, lo, 0, 0, 1));
        w.AdvanceTo(w.Time + 90_000);
        var k = Her(w);
        var cls = k.Class;
        var secondaries = Enumerable.Range(0, cls.Mounts.Length).Where(m => cls.Mounts[m].Battery >= 0 && !cls.Batteries[cls.Mounts[m].Battery].Chief && !cls.Enclosed(m)).ToList();
        foreach (int m in secondaries)
            output.WriteLine($"mount {m} at y {cls.Mounts[m].Y:+0.0;-0.0}: {(k.ClosedUp[m] ? "closed up" : "under cover")}, bears 90 {CombatStore.InArcs(cls, cls.Mounts[m], 90)}, 270 {CombatStore.InArcs(cls, cls.Mounts[m], 270)}");
        foreach (int m in secondaries)
            Assert.True(k.ClosedUp[m]);
    }

    [Fact]
    public void For_torpedo_craft_only_a_watch_is_closed_up()
    {
        var (w, _) = Make(ManGuns.TorpedoCraft);
        w.AdvanceTo(w.Time + 90_000);
        var k = Her(w);
        var c = k.Class;
        for (int b = 0; b < c.Batteries.Length; b++)
        {
            if (c.Batteries[b].Chief)
                continue;
            var mounts = Enumerable.Range(0, c.Mounts.Length).Where(m => c.Mounts[m].Battery == b && !c.Enclosed(m)).ToList();
            int up = mounts.Count(m => k.ClosedUp[m]);
            output.WriteLine($"battery {b}: {up} of {mounts.Count} closed up");
            Assert.Equal(Math.Min(2, mounts.Count), up);
        }
    }

    [Fact]
    public void A_short_gun_is_refilled_from_the_men_under_cover()
    {
        var (w, _) = Make(ManGuns.InRange);
        w.AdvanceTo(w.Time + 60_000);
        var k = Her(w);
        var c = k.Class;
        int main = Enumerable.Range(0, c.Mounts.Length).First(m => c.Batteries[c.Mounts[m].Battery].Chief);
        int g = Casualties.Part(c.Mounts[main].Component), need = c.Components[c.Mounts[main].Component].BattleCrew;
        int covered = k.Covered.Sum();
        Assert.True(covered > 0, "men under cover to relieve with");
        k.Gone[g] = need / 2;
        k.CrewLost += need / 2;
        w.AdvanceTo(w.Time + 2_000);
        int coming = Reliefs.Inbound(k, g);
        output.WriteLine($"main mount {main}: {need / 2} of {need} out; {coming} on their way from {covered} under cover");
        Assert.Equal(Math.Min(need / 2, covered), coming);
        w.AdvanceTo(w.Time + 5 * 60_000);
        Assert.Equal(need, Casualties.Men(c, k, g));
        Assert.Equal(coming, k.In[g]);
        Assert.InRange(BattleStations.Share(c, k, g), 0.8, 0.9);   // half of them reliefs from another trade
    }
}
