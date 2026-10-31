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

/// <summary>The captain's call (docs/battle/striking.md SK2): a ship that can't hurt, can't get away and has an enemy in
/// sight is given up after his delay (research §1.2: 5-50 min) as her navy's doctrine says, struck or scuttled or fought
/// on; never one the player holds; and a ship that won't float is abandoned, enemy or not.</summary>
public class CaptainsCallTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>A light cruiser of side 0 (entity 1), stopped, her rudder and (by default) her guns and tubes wrecked; an
    /// enemy one (2) 5 km east, stopped and holding her fire.</summary>
    static (World W, Ai.AiSystem Ai) Cornered(double strike, double scuttle, bool wreck = true)
    {
        var w = new World(Start, 7, Settings);
        var ai = new Ai.AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        int c = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        w.Apply(ShipSystem.Spawn(c, 56.0, 4.0, 0, 0, 0));
        w.Apply(ShipSystem.Spawn(c, 56.0, 4.08, 180, 0, 1));
        w.Apply(ai.Orders.SideDoctrine(0, Ai.DoctrineField.StrikeColours, strike));
        w.Apply(ai.Orders.SideDoctrine(0, Ai.DoctrineField.Scuttle, scuttle));
        w.Apply(ai.Orders.HoldFire(Ai.Unit.Ship(2)));
        w.Apply(ai.Orders.HoldFire(Ai.Unit.Ship(1)));   // her torpedoes at will would sink the other before the test
        w.AdvanceTo(w.Time + 30_000);
        if (wreck)
        {
            var cls = w.Ships.Store[w.Ships.Store.SlotOf(1)].Handling.Class;
            for (int m = 0; m < cls.Mounts.Length; m++)
                w.Apply(DamageSystem.Wreck(1, cls.Mounts[m].Component, PartState.Destroyed));
        }
        w.Apply(DamageSystem.WreckKind(1, ComponentKind.Rudder, 0));   // she can't get away
        return (w, ai);
    }

    static ShipCondition K(World w, int e = 1) => w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(e));

    /// <summary>Minutes from now until her people give her up (NaN: not within the time).</summary>
    static double Until(World w, int minutes)
    {
        long from = w.Time.Ms;
        for (int i = 0; i < minutes * 2; i++)
        {
            w.AdvanceTo(w.Time + 30_000);
            if (w.Ships.Store.SlotOf(1) < 0 || K(w).IsGivenUp)
                return (w.Time.Ms - from) / 60_000.0;
        }
        return double.NaN;
    }

    [Fact]
    public void Hopeless_and_cornered_she_strikes_by_her_navys_doctrine_after_his_delay()
    {
        var (w, _) = Cornered(strike: 2, scuttle: 0);
        double min = Until(w, 60);
        output.WriteLine($"struck after {min:0.0} min");
        Assert.InRange(min, Settings.GiveUpDelayMinS / 60 - 1, Settings.GiveUpDelayMaxS / 60 + 1);
        Assert.True(K(w).ColoursDown);
        Assert.False(K(w).Abandoned);
    }

    [Fact]
    public void A_navy_that_scuttles_scuttles_and_one_that_does_neither_fights_on()
    {
        var (w, _) = Cornered(strike: -2, scuttle: 1);
        double min = Until(w, 60);
        output.WriteLine($"scuttled after {min:0.0} min");
        Assert.False(double.IsNaN(min));
        Assert.True(K(w).Abandoned && (K(w).GivenUp & GivenUp.Scuttled) != 0 && !K(w).ColoursDown);

        var (w2, _) = Cornered(strike: -2, scuttle: 0);
        Assert.True(double.IsNaN(Until(w2, 60)), "fought on");
    }

    [Fact]
    public void Her_guns_whole_or_the_enemy_away_she_isnt_given_up()
    {
        var (w, _) = Cornered(strike: 2, scuttle: 1, wreck: false);
        Assert.True(double.IsNaN(Until(w, 60)), "her guns whole");

        var (w2, ai2) = Cornered(strike: 2, scuttle: 1);
        w2.Apply(ShipSystem.Drop(2));   // nobody in reach: she holds on (research §1.2)
        Assert.True(double.IsNaN(Until(w2, 60)), "no enemy");
        _ = ai2;
    }

    [Fact]
    public void A_ship_the_player_holds_fights_on()
    {
        var (w, ai) = Cornered(strike: 2, scuttle: 1);
        w.Apply(ai.Orders.ByHand.Course(Ai.Unit.Ship(1), 0));
        Assert.True(double.IsNaN(Until(w, 60)), "held by hand");
    }

    [Fact]
    public void A_ship_that_wont_float_is_abandoned_before_she_goes_enemy_or_not()
    {
        // an armoured cruiser alone, her doors open, holed a little in every machinery space: she founders in about 85 min
        var w = new World(Start, 7, Settings with { Readiness = [.. Settings.Readiness.Select(r => r with { WtDoorsOpen = 1 })] });
        var ai = new Ai.AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Get("armoured_cruiser").Class!);
        w.Apply(ShipSystem.Spawn(c, 56.0, 4.0, 0, 0, 0));
        w.Apply(ai.Orders.Course(Ai.Unit.Ship(1), 0));
        w.AdvanceTo(w.Time + 30_000);
        var cls = w.Ships.Store[w.Ships.Store.SlotOf(1)].Handling.Class;
        foreach (var room in cls.Rooms)
            if (room.Kind is RoomKind.BoilerRoom or RoomKind.EngineRoom)
                foreach (int cell in cls.Links(room.Cells))
                {
                    var b = cls.Cells[cell].Box;
                    w.Apply(DamageSystem.Breach(1, cell, 0.05, (b.Y0 + b.Y1) / 2, b.Base + 0.05));
                }
        long hit = w.Time.Ms;
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(ai.Orders.Speed(Ai.Unit.Ship(1), 0));   // the AI woken once she's wet (a dev hole isn't news to it; a hit comes with a contact)
        long abandoned = 0, going = 0;
        for (int i = 0; i < 6 * 60 && going == 0; i++)
        {
            w.AdvanceTo(w.Time + 60_000);
            if (w.Ships.Store.SlotOf(1) < 0)
                break;
            var k = K(w);
            if (abandoned == 0 && k.Abandoned)
                abandoned = k.AbandonMs;
            if (k.Status == ShipStatus.GoingDown)
                going = k.LossMs;
        }
        output.WriteLine($"abandoned {(abandoned - hit) / 60_000.0:0} min after the holes, going down {(going - hit) / 60_000.0:0} min after");
        Assert.True(going > 0, "she sank");
        Assert.True(abandoned > 0 && abandoned < going, "her men ordered off first");
    }

    [Fact]
    public void The_call_replays_the_same()
    {
        ulong Run()
        {
            var (w, _) = Cornered(strike: 0.5, scuttle: 0.5);
            w.AdvanceTo(w.Time + 50 * 60_000);
            return w.ComputeHash();
        }
        Assert.Equal(Run(), Run());
    }
}

/// <summary>Read by the enemy (docs/battle/striking.md SK3, SK5): her colours struck are seen where her colours can be
/// made out, and for good; seen so she's out (no target: the fire on her stops at the captains' next think), and she
/// heats no encounter, so it closes; out of the fight for the aims.</summary>
public class ReadByTheEnemyTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    [Fact]
    public void Her_colours_seen_down_the_fire_on_her_stops_and_the_encounter_closes()
    {
        var w = new World(Start, 7, Settings);
        var ai = new Ai.AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        // an armoured cruiser (1) under a light cruiser's fire (2) at 8 km: hit, not sunk, before she strikes
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(Fleet.Get("armoured_cruiser").Class!), 56.0, 4.0, 0, 0, 0));
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(Fleet.Get("light_cruiser").Class!), 56.0, 4.13, 180, 0, 1));
        w.Apply(ai.Orders.HoldFire(Ai.Unit.Ship(1)));
        w.AdvanceTo(w.Time + 30_000);
        w.Apply(ai.Orders.Engage(Ai.Unit.Ship(2), 1));
        w.AdvanceTo(w.Time + 60_000);
        var store = w.Ships.Store;
        Assert.True(store.SlotOf(1) >= 0 && store.Combat.Condition(store.SlotOf(1)).Status == ShipStatus.Afloat);
        Assert.True(store.Combat.Engaged(store.SlotOf(2)) > 0, "engaged on her");
        w.Apply(DamageSystem.StrikeColours(1));
        w.AdvanceTo(w.Time + 10_000);
        var pic = w.Sightings.Of(1)!;
        Assert.True((pic[pic.IndexOf(1)].Seen & SeenFlags.ColoursDown) != 0, "her colours seen down");
        Assert.True(pic.Out(1));
        Assert.Equal(0, store.Combat.Engaged(store.SlotOf(2)));
        Assert.False(Ai.Commanders.Aims.Afloat(store, store.SlotOf(1)));
        int hits = w.Ships.Store.Combat.Condition(store.SlotOf(1)).Hits;
        w.AdvanceTo(w.Time + Settings.EncounterCloseMs + 120_000);
        Assert.Equal(hits, w.Ships.Store.Combat.Condition(store.SlotOf(1)).Hits);
        Assert.Equal(0, w.Encounters.Count);
        Assert.True((pic[pic.IndexOf(1)].Seen & SeenFlags.ColoursDown) != 0, "for good");
        output.WriteLine($"hits on her {hits}, encounters {w.Encounters.Count}");
    }
}
