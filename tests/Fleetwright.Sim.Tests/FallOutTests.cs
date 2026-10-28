using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Falling out (docs/battle/command.md 5.1a): a ship past doctrine's fall-out leaves her line, which closes up
/// behind her, and goes home (away from the enemy in sight, then for her side's home), with her reason on the snapshot;
/// a ship whose doctrine never falls out stays.</summary>
public class FallOutTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>Two light cruisers of side 0 in a column steaming north, an enemy one 12 km east of them steaming north.</summary>
    static (World W, AiSystem Ai, int Group) Make()
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        int c = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        w.Apply(ShipSystem.Spawn(c, 56.0, 4.0, 0, 15, 0));
        w.Apply(ShipSystem.Spawn(c, 55.995, 4.0, 0, 15, 0));
        w.Apply(ShipSystem.Spawn(c, 56.0, 4.195, 0, 15, 1));
        w.Apply(GroupSystem.Create(1));
        w.Apply(GroupSystem.Add(w.Groups.Of(1)!.GroupId, 2));
        int g = w.Groups.Of(1)!.GroupId;
        w.Apply(ai.Orders.Course(Unit.Group(g), 0));
        w.Apply(ai.Orders.Speed(Unit.Group(g), 15));
        return (w, ai, g);
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    [Fact]
    public void A_ship_past_doctrines_fall_out_leaves_the_line_and_runs_from_the_enemy_then_home()
    {
        var (w, ai, g) = Make();
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.FallOutGunsShare, 1.01));   // any damage at all
        w.Apply(ai.Orders.Home(0, 57.0, 3.0));
        // the enemy holds her fire: a hit on the other ship would put her out too (any damage), and the test is of falling
        // out, not of the enemy's shooting (which since the short-range bug hunt of 2026-10-18 hits at 12 km in minutes)
        w.Apply(ai.Orders.HoldFire(Unit.Ship(3)));
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(DamageSystem.SideHole(1, HoleAt.Mid, 0, 3, 0.01));   // a scratch above the waterline: damaged, nothing lost
        w.AdvanceTo(w.Time + 5_000);
        Assert.Null(w.Groups.Of(1));
        Assert.Equal(new[] { 2 }, w.Groups.Get(g)!.Line.ToArray());   // the line closed up: the other ship guides it
        Assert.Equal(CaptainOrder.Home, ai.Captains.Of(1).Order);
        Assert.Equal(CaptainReason.OutGuns, ai.Captains.ReasonOf(1));
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        Assert.Equal((byte)CaptainReason.OutGuns, snap.Ships.Reason[snap.Ships.IndexOf(1)]);

        // away from the enemy in sight: its bearing behind her
        w.AdvanceTo(w.Time + 3 * 60_000);
        double off = Earth.Bearing(Ship(w, 1).Position, Ship(w, 3).Position) - Ship(w, 1).HeadingDeg;
        off -= 360 * Math.Round(off / 360);
        output.WriteLine($"heading {Ship(w, 1).HeadingDeg:F0}°, the enemy {off:F0}° off her bow");
        Assert.True(Math.Abs(off) > 120, $"the enemy {off:F0}° off her bow");
        Assert.Equal(g, w.Groups.Of(2)!.GroupId);   // the other ship, undamaged, keeps the squadron
        var groups = snap.Groups;
        int gi = Array.IndexOf(groups.Id, g, 0, groups.Count);
        Assert.Equal((1, 2), (groups.MemberCount[gi], groups.GuideShip[gi]));
    }

    /// <summary>The player overrules her captain (docs/map-ui/battle-hud.md decision 2): ordered on after she fell out
    /// and put back in her line, she stays in it through more damage.</summary>
    [Fact]
    public void Ordered_on_after_falling_out_she_stays_in_her_line()
    {
        var (w, ai, g) = Make();
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.FallOutGunsShare, 1.01));   // any damage at all
        w.Apply(ai.Orders.HoldFire(Unit.Ship(3)));
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(DamageSystem.SideHole(1, HoleAt.Mid, 0, 3, 0.01));
        w.AdvanceTo(w.Time + 5_000);
        Assert.Equal(CaptainReason.OutGuns, ai.Captains.ReasonOf(1));
        // the strip's "order her back into line": an order of the player's, then back in her squadron
        w.Apply(ai.Orders.ByHand.Fight(Unit.Ship(1)));
        w.Apply(GroupSystem.Add(g, 1));
        w.AdvanceTo(w.Time + 30_000);
        var cls = Ship(w, 1).Handling.Class;
        w.Apply(DamageSystem.Wreck(1, cls.Mounts[0].Component, PartState.Destroyed));   // hit again: a mount wrecked
        w.AdvanceTo(w.Time + 30_000);
        Assert.Equal(CaptainReason.None, ai.Captains.ReasonOf(1));
        Assert.NotEqual(CaptainOrder.Home, ai.Captains.Of(1).Order);
        Assert.Equal(g, w.Groups.Of(1)?.GroupId);
    }

    /// <summary>Fallen out, with the enemy gone she steers for her side's home and stops within
    /// <see cref="SimSettings.HomeRadiusM"/> of it, her reason then "home".</summary>
    [Fact]
    public void With_nobody_near_she_steers_for_home_and_stops_there()
    {
        var (w, ai, _) = Make();
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.FallOutGunsShare, 1.01));
        var home = Earth.Position(56.25, 4.0);   // 28 km north
        w.Apply(ai.Orders.Home(0, 56.25, 4.0));
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(DamageSystem.SideHole(1, HoleAt.Mid, 0, 3, 0.01));
        w.AdvanceTo(w.Time + 5_000);
        Assert.Equal(CaptainOrder.Home, ai.Captains.Of(1).Order);
        w.Apply(ShipSystem.Drop(3));   // nobody to run from
        w.AdvanceTo(w.Time + 90 * 60_000);
        double left = Earth.Distance(Ship(w, 1).Position, home);
        output.WriteLine($"{left:F0} m from home, {Ship(w, 1).Speed / Handling.MsPerKnot:F1} kn");
        Assert.True(left <= Settings.HomeRadiusM, $"{left:F0} m from home");
        Assert.Equal(0, Ship(w, 1).SpeedOrderKn);   // stopping (a big ship coasts on a while)
        Assert.Equal(CaptainReason.Home, ai.Captains.ReasonOf(1));
    }

    [Fact]
    public void Neutral_doctrine_never_falls_out()
    {
        var (w, ai, _) = Make();
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(DamageSystem.SideHole(1, HoleAt.Mid, 0, 3, 0.01));
        w.AdvanceTo(w.Time + 60_000);
        Assert.NotNull(w.Groups.Of(1));
        Assert.Equal(CaptainReason.None, ai.Captains.ReasonOf(1));
    }

    /// <summary>A fault the captain judges temporary doesn't take her out of the line (the user, 2026-10-20; E7's fix): with
    /// the whole ship's power out for 3 minutes her fitness reads her main guns whole; out for an hour, gone. The power cut
    /// lasting doesn't slow her either way (the mounts are what it reaches).</summary>
    [Fact]
    public void A_blackout_for_minutes_leaves_her_fit_one_for_an_hour_does_not()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var rule = new FallOutRule(0.5, 0.5, 15, 0.5);
        foreach (var (min, whole) in new[] { (3.0, true), (60.0, false) })
        {
            var k = new ShipCondition();
            k.Reset(c);
            k.Damaged = true;
            long now = 600_000;
            Effects.PowerOut(c, k, new P3(0, 0, 0), -1, -1, double.PositiveInfinity, (long)(min * 60_000), now, DownCause.NearMissCascade);
            Functions.Evaluate(k, Settings, now, now + (long)(Settings.FallOutWaitMin * 60_000));
            var fit = Fitness.Of(c, k, 1000);
            output.WriteLine($"power out {min} min: mounts working {k.MountWorks.Count(m => m)} of {k.MountWorks.Length}, " +
                             $"main guns {fit.MainGunsShare:P0} to the captain, fall-out {rule.Of(fit)}");
            Assert.DoesNotContain(true, k.MountWorks);
            Assert.Equal(whole ? 1 : 0, fit.MainGunsShare);
            Assert.Equal(whole ? FallOutCause.None : FallOutCause.Guns, rule.Of(fit));
        }
    }
}
