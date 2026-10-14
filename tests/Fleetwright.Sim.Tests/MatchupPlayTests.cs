using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The matchup played out (docs/battle/command.md 5.1g, piece 3), on the 1914 classes: a light cruiser hunts a
/// destroyer and runs from a battleship; a pack of destroyers presses its attack home on a battleship; a commander sends
/// his light cruisers after the enemy's destroyers and his battle squadron after the enemy's.</summary>
public class MatchupPlayTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 12);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.0, Lon = 4.0;

    static (World W, AiSystem Ai, Dictionary<string, int> Ids) Make(SimSettings? settings = null)
    {
        var w = new World(Start, 5, settings ?? Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(30_000));
        var ids = MatchupTests.Classes.Value.ToDictionary(kv => kv.Key, kv => w.Ships.AddClass(kv.Value));
        return (w, ai, ids);
    }

    static Vec3d Off(double bearing, double m)
    {
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, bearing);
        Earth.Advance(ref p, ref f, m);
        return p;
    }

    /// <summary>n ships of a class in a column from a point (a squadron if more than one); the unit.</summary>
    static Unit Column(World w, int cls, Vec3d at, double heading, int n, byte side, double kn = 15, Formation formation = Formation.Column)
    {
        int first = w.Ships.NewestEntity + 1;
        var f = Earth.Forward(at, heading + 180);
        var p = at;
        for (int i = 0; i < n; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(cls, la, lo, heading, kn, side));
            Earth.Advance(ref p, ref f, 500);
        }
        if (n == 1)
            return Unit.Ship(first);
        w.Apply(GroupSystem.Create(first, formation, 500));
        int g = w.Groups[w.Groups.Count - 1].GroupId;
        for (int e = first + 1; e < first + n; e++)
            w.Apply(GroupSystem.Add(g, e));
        return Unit.Group(g);
    }

    static double Apart(World w, int a, int b) =>
        Earth.Distance(w.Ships.Store[w.Ships.Store.SlotOf(a)].Position, w.Ships.Store[w.Ships.Store.SlotOf(b)].Position);

    [Theory]
    [InlineData("v25", true)]
    [InlineData("iron_duke", false)]
    public void A_light_cruiser_hunts_a_destroyer_and_runs_from_a_battleship(string enemy, bool closes)
    {
        var (w, ai, ids) = Make();
        Column(w, ids["arethusa"], Earth.Position(Lat, Lon), 0, 1, side: 0, kn: 20);
        Column(w, ids[enemy], Off(90, 16_000), 0, 1, side: 1, kn: 15);
        w.Apply(ShipSystem.LookNow());
        w.AdvanceTo(Start + 30_000);
        w.Apply(ai.Orders.Fight(Unit.Ship(1)));
        double before = Apart(w, 1, 2);
        w.AdvanceTo(w.Time + 10 * 60_000);
        double after = w.Ships.Store.SlotOf(2) >= 0 ? Apart(w, 1, 2) : 0;
        output.WriteLine($"Arethusa against a {enemy}: {before / 1000:F1} km, ten minutes on {after / 1000:F1} km");
        if (closes)
            Assert.True(after < before - 3_000, "she closes him");
        else
            Assert.True(after > before, "she runs");
    }

    [Fact]
    public void A_pack_of_destroyers_presses_its_attack_home_on_a_battleship()
    {
        var (w, ai, ids) = Make();
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.TorpedoAttack, (double)TorpedoAttack.Together));
        var bb = Column(w, ids["iron_duke"], Earth.Position(Lat, Lon), 180, 1, side: 0, kn: 15);
        var pack = Column(w, ids["v25"], Off(90, 15_000), 270, 8, side: 1, kn: 25, Formation.LineAbreast);
        w.Apply(ShipSystem.LookNow());
        w.AdvanceTo(Start + 30_000);
        w.Apply(ai.Orders.Fight(pack));
        var seen = new List<AttackPhase>();
        for (int t = 0; t < 30 * 60 && w.Torpedoes.Fired == 0; t += 10)
        {
            w.AdvanceTo(w.Time + 10_000);
            var phase = ai.Admirals.AttackOf(pack.GroupId).Phase;
            if (seen.Count == 0 || seen[^1] != phase)
                seen.Add(phase);
        }
        output.WriteLine($"phases {string.Join(" > ", seen)}; torpedoes {w.Torpedoes.Fired}");
        Assert.Contains(AttackPhase.Run, seen);
        Assert.True(w.Torpedoes.Fired > 0, "the pack fired");
        _ = bb;
    }

    [Fact]
    public void Light_cruisers_go_for_the_destroyers_and_the_battle_squadron_for_the_battle_squadron()
    {
        // both enemy groups in sight but out of contact (beyond the fight radius, here 15 km: closer, a fight is
        // doctrine's, not his); the battle squadron the graver, nearest the line
        var (w, ai, ids) = Make(Settings with { FightCompareM = 15_000 });
        var line = Column(w, ids["iron_duke"], Earth.Position(Lat, Lon), 0, 4, side: 0);
        var cruisers = Column(w, ids["arethusa"], Off(270, 6_000), 0, 4, side: 0);
        var boats = Column(w, ids["v25"], Off(90, 18_000), 0, 4, side: 1, kn: 12, Formation.LineAbreast);
        var battle = Column(w, ids["derfflinger"], Off(0, 20_000), 180, 3, side: 1, kn: 12);
        w.Apply(ai.Orders.HoldFire(line));
        w.Apply(ai.Orders.HoldFire(cruisers));
        w.Apply(ShipSystem.LookNow());
        w.Apply(ai.Orders.Commander(0, CommanderMode.On));
        w.AdvanceTo(Start + 3 * 60_000);
        var pic = w.Sightings.Of(0);
        output.WriteLine($"phase {ai.Commanders.PhaseOf(0)}; contacts {pic?.Count}: {string.Join(", ", Enumerable.Range(0, pic?.Count ?? 0).Select(i => $"{pic![i].Entity} {pic[i].Level}{(pic[i].InSight ? "" : "?")}"))}");
        var toLine = ai.Commanders.GivenOf(line);
        var toCruisers = ai.Commanders.GivenOf(cruisers);
        int Group(int target) => w.Groups.Of(target)?.GroupId ?? 0;
        output.WriteLine($"the line given {toLine} (target's group {Group(toLine.Target)}); the cruisers {toCruisers} (target's group {Group(toCruisers.Target)}); " +
                         $"enemy groups: battle {battle.GroupId}, boats {boats.GroupId}");
        Assert.Equal((TaskKind.Intercept, CommanderWhy.Contact), (toLine.Given, toLine.Why));
        Assert.Equal(battle.GroupId, Group(toLine.Target));
        Assert.Equal((TaskKind.Intercept, CommanderWhy.Contact), (toCruisers.Given, toCruisers.Why));
        Assert.Equal(boats.GroupId, Group(toCruisers.Target));
    }
}
