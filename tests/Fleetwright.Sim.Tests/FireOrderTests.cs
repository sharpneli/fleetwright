using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Ordering fire (docs/battle/fighting.md 4.2c): fire at will by default, from the navy's picture, the nearest
/// for a main battery and the smallest for a secondary, without flicking between targets; hold fire and fire at will
/// again; a group's Engage distributed over the enemy's line from the van, and a ship's own order standing until the
/// group is given another; Fight closing to its band and turning away when outgunned; the squadrons scenario fighting,
/// replayed to the same hash, without garbage.</summary>
public class FireOrderTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.0, Lon = 4.0;

    /// <summary>A ship to place: its design, where it is from (Lat, Lon) by bearing and range, its course, speed and side.</summary>
    record Placed(string Design, double BearingDeg, double RangeM, double CourseDeg, double Kn, byte Side);

    /// <summary>A world in clear weather with the AI, the ships placed in order (entities 1, 2, ...).</summary>
    static (World W, AiSystem Ai) Make(params Placed[] ships)
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        foreach (var s in ships)
        {
            int c = w.Ships.AddClass(Fleet.Get(s.Design).Class!);
            var p = Earth.Position(Lat, Lon);
            var f = Earth.Forward(p, s.BearingDeg);
            if (s.RangeM > 0)
                Earth.Advance(ref p, ref f, s.RangeM);
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(c, la, lo, s.CourseDeg, s.Kn, s.Side));
        }
        return (w, ai);
    }

    static void Run(World w, double seconds) => w.AdvanceTo(w.Time + (long)Math.Round(seconds * 1000));

    static int Target(World w, int ship, int battery) => w.Ships.Store.Combat.Target(w.Ships.Store.SlotOf(ship), battery);

    static double Apart(World w, int a, int b) =>
        Earth.Distance(w.Ships.Store[w.Ships.Store.SlotOf(a)].Position, w.Ships.Store[w.Ships.Store.SlotOf(b)].Position);

    [Fact]
    public void Ships_fire_at_will_on_each_other_by_default()
    {
        var (w, _) = Make(new Placed("super_dreadnought", 0, 0, 0, 15, 0), new Placed("super_dreadnought", 90, 13_000, 0, 15, 1));
        Run(w, 6 * 60);
        Assert.Equal(2, Target(w, 1, 0));
        Assert.Equal(1, Target(w, 2, 0));
        var a = w.Gunnery.TallyOf(1, 0)!.Value;
        var b = w.Gunnery.TallyOf(2, 0)!.Value;
        output.WriteLine($"1 on 2: {a.Salvos} salvos, {a.Hits} hits; 2 on 1: {b.Salvos} salvos, {b.Hits} hits");
        Assert.True(a.Salvos > 0 && b.Salvos > 0, "both opened fire");
    }

    [Fact]
    public void Hold_fire_ceases_and_fire_at_will_opens_again()
    {
        var (w, ai) = Make(new Placed("super_dreadnought", 0, 0, 0, 15, 0), new Placed("super_dreadnought", 90, 13_000, 0, 15, 1));
        Run(w, 30);
        Assert.Equal(2, Target(w, 1, 0));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(1)));
        Assert.Equal(0, w.Ships.Store.Combat.Engaged(w.Ships.Store.SlotOf(1)));   // at once
        Run(w, 5 * 60);
        Assert.Equal(0, w.Ships.Store.Combat.Engaged(w.Ships.Store.SlotOf(1)));
        Assert.Equal(FireOrder.Hold, ai.Captains.FireOf(1).Order);
        Assert.Equal(1, Target(w, 2, 0));   // the other doesn't
        w.Apply(ai.Orders.FireAtWill(Unit.Ship(1)));
        Run(w, 2);
        Assert.Equal(2, Target(w, 1, 0));
    }

    [Fact]
    public void Main_batteries_take_the_nearest_secondaries_the_smallest_and_neither_flicks()
    {
        // a battleship at 7 km and a destroyer at 10 km, both abeam: the 15" on the battleship, the 6" on the destroyer
        var (w, _) = Make(new Placed("super_dreadnought", 0, 0, 0, 12, 0), new Placed("dreadnought", 80, 7_000, 0, 12, 1),
            new Placed("destroyer", 100, 10_000, 0, 12, 1));
        var cls = Fleet.Get("super_dreadnought").Class!;
        int secondary = Array.FindIndex(cls.Batteries.ToArray(), b => b.Kind == Ships.Battery.Secondary);
        Assert.True(secondary > 0);
        Run(w, 60);
        Assert.Equal(2, Target(w, 1, 0));
        Assert.Equal(3, Target(w, 1, secondary));
        int changes = 0, last = Target(w, 1, 0);
        for (int s = 0; s < 6 * 60; s++)
        {
            Run(w, 1);
            if (w.Ships.Store.SlotOf(2) < 0 || w.Ships.Store.SlotOf(1) < 0)
                break;   // the battleship gone (sunk at 241 s since fire-control.md F4b): the destroyer next isn't a flick; nor is
                         // anything once the shooter herself is lost (a magazine, since docs/battle/magazines.md V2's trains)
            if (Target(w, 1, 0) != last)
                (changes, last) = (changes + 1, Target(w, 1, 0));
        }
        Assert.Equal(0, changes);
    }

    [Fact]
    public void A_ship_seen_going_down_is_no_target_at_will_or_engaged()
    {
        // at will: the battleship on the nearer dreadnought; she founders (10 minutes going under) and the guns shift at once
        var (w, _) = Make(new Placed("super_dreadnought", 0, 0, 0, 12, 0), new Placed("dreadnought", 80, 9_000, 0, 12, 1),
            new Placed("dreadnought", 100, 12_000, 0, 12, 1));
        Run(w, 60);
        Assert.Equal(2, Target(w, 1, 0));
        w.Apply(DamageSystem.Sink(2, LossKind.Foundered));
        Run(w, 3);
        Assert.Equal(3, Target(w, 1, 0));

        // engaged by the ship's own order: at will again, on the other
        var (we, ai) = Make(new Placed("super_dreadnought", 0, 0, 0, 12, 0), new Placed("dreadnought", 80, 9_000, 0, 12, 1),
            new Placed("dreadnought", 100, 12_000, 0, 12, 1));
        Run(we, 30);
        we.Apply(ai.Orders.Engage(Unit.Ship(1), 2));
        Run(we, 30);
        Assert.Equal(2, Target(we, 1, 0));
        we.Apply(DamageSystem.Sink(2, LossKind.Foundered));
        Run(we, 3);
        Assert.Equal(FireOrder.AtWill, ai.Captains.FireOf(1).Order);
        Assert.Equal(3, Target(we, 1, 0));

        // a group's Engage on the enemy's leader: his line counted without her
        var (wg, aig) = Lines(own: 3);
        Run(wg, 30);
        wg.Apply(aig.Orders.Engage(Unit.Group(wg.Groups.Of(1)!.GroupId), 4));
        Run(wg, 60);
        Assert.Equal([4, 5, 6], new[] { 1, 2, 3 }.Select(e => Target(wg, e, 0)));
        wg.Apply(DamageSystem.Sink(4, LossKind.Foundered));
        Run(wg, 3);
        Assert.DoesNotContain(4, new[] { 1, 2, 3 }.Select(e => Target(wg, e, 0)));
    }

    /// <summary>Four QEs (1-4) in column against three dreadnoughts (5-7) in column 13 km to the east, both north.</summary>
    static (World W, AiSystem Ai) Lines(int own = 4)
    {
        var ships = new List<Placed>();
        for (int i = 0; i < own; i++)
            ships.Add(new Placed("super_dreadnought", 180, i * 500, 0, 15, 0));
        for (int i = 0; i < 3; i++)
            ships.Add(new Placed("dreadnought", 90 + Math.Atan2(i * 500, 13_000) * 180 / Math.PI, Math.Sqrt(13_000.0 * 13_000 + i * 500.0 * i * 500), 0, 15, 1));
        var (w, ai) = Make([.. ships]);
        w.Apply(GroupSystem.Create(1));
        for (int i = 2; i <= own; i++)
            w.Apply(GroupSystem.Add(1, i));
        w.Apply(GroupSystem.Create(own + 1));
        for (int i = own + 2; i <= own + 3; i++)
            w.Apply(GroupSystem.Add(own + 1, i));
        return (w, ai);
    }

    [Fact]
    public void A_groups_engage_is_counted_from_the_van_and_a_ships_own_order_stands()
    {
        var (w, ai) = Lines();
        int group = w.Groups.Of(1)!.GroupId;
        Run(w, 30);   // the enemy sighted and made out
        w.Apply(ai.Orders.Engage(Unit.Group(group), 5));
        Run(w, 60);
        // four on three: the van doubles on the enemy's leader (k·m/n: 0, 0, 1, 2)
        Assert.Equal([5, 5, 6, 7], new[] { 1, 2, 3, 4 }.Select(e => Target(w, e, 0)));

        // the player puts ship 3 on the leader: it stays there, and the admiral counts it out
        w.Apply(ai.Orders.Engage(Unit.Ship(3), 5));
        Run(w, 30);
        Assert.Equal(5, Target(w, 3, 0));
        Assert.Equal((FireOrder.Engage, 5, 0), ai.Captains.FireOf(3));
        Assert.Equal([5, 6, 7], new[] { 1, 2, 4 }.Select(e => Target(w, e, 0)));   // three on three now

        // the group given fire at will again: all of it, ship 3 too
        w.Apply(ai.Orders.FireAtWill(Unit.Group(group)));
        Assert.Equal((FireOrder.AtWill, 0, group), ai.Captains.FireOf(3));

        // three on three: ship k on enemy k
        var (w3, ai3) = Lines(own: 3);
        Run(w3, 30);
        w3.Apply(ai3.Orders.Engage(Unit.Group(w3.Groups.Of(1)!.GroupId), 4));
        Run(w3, 60);
        Assert.Equal([4, 5, 6], new[] { 1, 2, 3 }.Select(e => Target(w3, e, 0)));
    }

    [Fact]
    public void Fight_closes_to_its_band_with_the_guns_bearing()
    {
        var (w, ai) = Make(new Placed("super_dreadnought", 0, 0, 90, 18, 0), new Placed("dreadnought", 90, 19_000, 0, 12, 1));
        w.Apply(ai.Orders.Fight(Unit.Ship(1)));
        Run(w, 30 * 60);
        double band = Doctrine.Default(DoctrineField.RangeShare) * w.Gunnery.ReachM(Fleet.Get("super_dreadnought").Class!.Loadout, 0);
        double range = Apart(w, 1, 2);
        var s1 = w.Ships.Store[w.Ships.Store.SlotOf(1)];
        double rel = Earth.Bearing(s1.Position, w.Ships.Store[w.Ships.Store.SlotOf(2)].Position) - s1.HeadingDeg;
        rel -= 360 * Math.Round(rel / 360);
        var t = w.Gunnery.TallyOf(1, 0)!.Value;
        output.WriteLine($"band {band / 1000:F1} km: at {range / 1000:F1} km, the enemy {rel:F0}° off the bow; {t.Salvos} salvos, {t.Hits} hits");
        Assert.InRange(range, 0.75 * band, 1.3 * band);
        Assert.InRange(Math.Abs(rel), 45, 135);
        Assert.Equal(CaptainOrder.Fight, ai.Captains.Of(1).Order);
        Assert.True(t.Salvos > 5);
    }

    [Fact]
    public void Fight_turns_away_when_outmatched()
    {
        // a battlecruiser, faster than the four battleships she meets (28 kn against 24): she can get away, and does. Judged
        // over the first 5 minutes: hit and slowed later she can't refuse them, and with nowhere to fall back on she fights
        // (she turned back at about 330 s once the battleships' after control rangefinders read too, fire-control.md F4b, and
        // at about 250 s once their turrets' report to the computer as well, F5)
        var ships = new List<Placed> { new("lion", 0, 0, 90, 18, 0) };
        for (int i = 0; i < 4; i++)
            ships.Add(new Placed("super_dreadnought", 90 + i * 3, 16_000, 270, 15, 1));
        var (w, ai) = Make([.. ships]);
        w.Apply(ai.Orders.Fight(Unit.Ship(1)));
        Run(w, 30);
        double before = Apart(w, 1, 2), away = double.NaN;
        for (int k = 0; k < 10 && double.IsNaN(away); k++)
        {
            Run(w, 30);
            if (w.Ships.Store[w.Ships.Store.SlotOf(1)].HeadingDeg is >= 180 and <= 360)
                away = Apart(w, 1, 2);
        }
        output.WriteLine($"from {before / 1000:F1} km to {away / 1000:F1} km when she'd turned away");
        Assert.False(double.IsNaN(away), "she never turned away, west");
        Assert.True(away > before - 3_000, "not closing");   // a little, while she turns
    }

    [Fact]
    public void Fight_with_no_escape_fights()
    {
        // a dreadnought (21 kn) against four faster battleships (24 kn): she can't get away, and nobody of hers is near to
        // fall back on, so she fights at her band (docs/battle/command.md 5.1g)
        var ships = new List<Placed> { new("dreadnought", 0, 0, 90, 18, 0) };
        for (int i = 0; i < 4; i++)
            ships.Add(new Placed("super_dreadnought", 90 + i * 3, 16_000, 270, 15, 1));
        var (w, ai) = Make([.. ships]);
        w.Apply(ai.Orders.Fight(Unit.Ship(1)));
        Run(w, 30);
        double before = Apart(w, 1, 2), after = before;
        for (int k = 0; k < 16 && w.Ships.Store.SlotOf(1) >= 0 && w.Ships.Store.SlotOf(2) >= 0; k++)
        {
            after = Apart(w, 1, 2);   // over 8 minutes, or until one of them is gone (fire-control.md F5: sunk inside them)
            Run(w, 30);
        }
        output.WriteLine($"from {before / 1000:F1} km to {after / 1000:F1} km");
        Assert.True(after < before, "she closes to fight");
    }

    [Fact]
    public void The_squadrons_fight_replay_and_allocate_nothing()
    {
        var s = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "squadrons.json"));
        var designs = new[] { Path.Combine(Fleet.Shipgen, "designs") };
        var w = new World(s.Start, s.Seed, Settings);
        var ai = new AiSystem(w);
        var classes = ScenarioLoader.RequestClasses(s, w.Ships.Registry, designs);
        var start = ScenarioLoader.Apply(s, w, ai, classes);
        Assert.Equal(9, start.Ships);
        w.AdvanceTo(s.Start + 12 * 60_000);   // closed, tracking, firing: pools at their size
        long before = GC.GetAllocatedBytesForCurrentThread();
        w.AdvanceTo(s.Start + 20 * 60_000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        // every salvo fired, from the combat log (a battery's tally is its engagement's: a new target restarts it)
        int british = 0, german = 0;
        for (int i = 0; i < w.CombatLog.Count; i++)
            if (w.CombatLog.Newest(i) is { Kind: CombatEvent.Fired } f)
                (british, german) = f.Shooter <= 4 ? (british + 1, german) : (british, german + 1);
        output.WriteLine($"salvos: British {british}, German {german}; hits so far {w.Shells.Hits}; {allocated} bytes in eight minutes");
        Assert.True(british > 20 && german > 20, "both sides fire");
        Assert.True(w.Shells.Hits > 0);
        Assert.Equal(0, allocated);

        var r = new World(s.Start, s.Seed, Settings, w.Ships.Registry);
        _ = new AiSystem(r);
        foreach (var c in w.CommandLog)
        {
            r.AdvanceTo(c.At);
            r.Apply(c.Command);
        }
        r.AdvanceTo(w.Time);
        Assert.Equal(w.ComputeHash(), r.ComputeHash());
    }
}
