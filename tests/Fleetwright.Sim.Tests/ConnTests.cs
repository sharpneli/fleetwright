using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The conn (docs/battle/command.md "The conn"): the player's absolute control of a ship or a line. Nothing but
/// the player's helm orders moves it: no combing a spread (the lookouts report its side instead), no turning away, no
/// falling out. Helm orders keep the conn; a general order hands it back.</summary>
public class ConnTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static double OffNorth(World w, int e)
    {
        double d = Ship(w, e).HeadingDeg;
        return Math.Abs(d - 360 * Math.Round(d / 360));
    }

    /// <summary>Battleships of side 0 steaming north at 15 kn (one alone, or two in a column), a destroyer of side 1 3 km
    /// off the first one's starboard beam holding her fire, both sides combing torpedoes and turning away from them.</summary>
    static (World W, AiSystem Ai) Make(int battleships)
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TorpedoEvasion, (double)TorpedoEvasion.Comb));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TurnAwayDeg, 45));
        int bb = w.Ships.AddClass(Fleet.Get("super_dreadnought").Class!), dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        var p = Earth.Position(56.0, 4.0);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 3_000);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(bb, 56.0, 4.0, 0, 15, 0));
        for (int i = 1; i < battleships; i++)
            w.Apply(ShipSystem.Spawn(bb, 56.0 - 0.005 * i, 4.0, 0, 15, 0));
        int dEntity = w.Ships.NewestEntity + 1;
        w.Apply(ShipSystem.Spawn(dd, la, lo, 0, 15, 1));
        if (battleships > 1)
        {
            w.Apply(GroupSystem.Create(1, Formation.Column, 550));
            for (int i = 2; i <= battleships; i++)
                w.Apply(GroupSystem.Add(w.Groups.Of(1)!.GroupId, i));
        }
        for (int e = 1; e <= dEntity; e++)
            w.Apply(ai.Orders.HoldFire(Unit.Ship(e)));
        return (w, ai);
    }

    /// <summary>A battleship on the conn holds her course through a spread her captain would have combed; her lookouts
    /// report the tracks to starboard.</summary>
    [Fact]
    public void On_the_conn_she_holds_on_through_a_spread_and_her_lookouts_call_it()
    {
        var (w, ai) = Make(1);
        var o = ai.Orders.ByHand;
        w.Apply(o.Course(Unit.Ship(1), 0));
        w.Apply(o.Conn(Unit.Ship(1), true));
        w.AdvanceTo(w.Time + 30_000);
        w.Apply(TorpedoSystem.Launch(2, 1, 4, truth: true));
        double turned = 0;
        sbyte called = 0;
        var snap = new Snapshot();
        for (int s = 0; s < 240; s += 5)
        {
            w.AdvanceTo(w.Time + 5_000);
            turned = Math.Max(turned, OffNorth(w, 1));
            w.Fill(snap, truth: true);
            int k = snap.Ships.IndexOf(1);
            if (k >= 0 && snap.Ships.Tracks[k] != 0)
                called = snap.Ships.Tracks[k];
            Assert.NotEqual(CaptainReason.Combing, ai.Captains.ReasonOf(1));
        }
        output.WriteLine($"turned up to {turned:F0}° off north; the lookouts called {called}; torpedoes hit {w.Torpedoes.Hits}");
        Assert.True(turned < 3, $"turned {turned:F0}°");
        Assert.Equal(1, called);   // to starboard, where the destroyer is
    }

    /// <summary>The conn stays through the player's helm orders (a course, a speed) and goes with a general order (Fight)
    /// or when it's given back.</summary>
    [Fact]
    public void Helm_orders_keep_the_conn_a_general_order_ends_it()
    {
        var (w, ai) = Make(1);
        var o = ai.Orders.ByHand;
        var snap = new Snapshot();
        byte Conn()
        {
            w.Fill(snap, truth: true);
            return snap.Ships.Conn[snap.Ships.IndexOf(1)];
        }
        w.Apply(o.Conn(Unit.Ship(1), true));
        Assert.Equal(1, Conn());
        w.Apply(o.Course(Unit.Ship(1), 30));
        w.Apply(o.Speed(Unit.Ship(1), 18));
        w.AdvanceTo(w.Time + 5_000);
        Assert.Equal(1, Conn());
        w.Apply(o.Fight(Unit.Ship(1)));
        Assert.Equal(0, Conn());
        w.Apply(o.Conn(Unit.Ship(1), true));
        w.Apply(o.Conn(Unit.Ship(1), false));
        Assert.Equal(0, Conn());
        w.Apply(ai.Orders.Conn(Unit.Ship(1), true));   // not by hand: a dev's or a scenario's order holds, it doesn't take the conn
        Assert.Equal(0, Conn());
    }

    /// <summary>A column on the conn: the ship the spread is aimed at holds her station instead of combing, and her
    /// admiral doesn't turn the line away; every ship of it shows the line's conn.</summary>
    [Fact]
    public void A_column_on_the_conn_steams_through_a_spread()
    {
        var (w, ai) = Make(2);
        int g = w.Groups.Of(1)!.GroupId;
        var o = ai.Orders.ByHand;
        w.Apply(o.Course(Unit.Group(g), 0));
        w.Apply(o.Conn(Unit.Group(g), true));
        w.AdvanceTo(w.Time + 30_000);
        w.Apply(TorpedoSystem.Launch(3, 1, 4, truth: true));
        double turned = 0;
        for (int s = 0; s < 240; s += 5)
        {
            w.AdvanceTo(w.Time + 5_000);
            turned = Math.Max(turned, Math.Max(OffNorth(w, 1), OffNorth(w, 2)));
        }
        output.WriteLine($"the column turned up to {turned:F0}° off north; torpedoes hit {w.Torpedoes.Hits}");
        Assert.True(turned < 5, $"turned {turned:F0}°");
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        Assert.Equal(2, snap.Ships.Conn[snap.Ships.IndexOf(2)]);
        Assert.True(snap.Groups.Conn[Array.IndexOf(snap.Groups.Id, g, 0, snap.Groups.Count)]);
    }

    /// <summary>The same column without the conn combs or turns away: what the conn takes off.</summary>
    [Fact]
    public void Without_the_conn_the_column_turns_from_the_spread()
    {
        var (w, ai) = Make(2);
        int g = w.Groups.Of(1)!.GroupId;
        w.Apply(ai.Orders.ByHand.Course(Unit.Group(g), 0));
        w.AdvanceTo(w.Time + 30_000);
        w.Apply(TorpedoSystem.Launch(3, 1, 4, truth: true));
        double turned = 0;
        for (int s = 0; s < 240; s += 5)
        {
            w.AdvanceTo(w.Time + 5_000);
            turned = Math.Max(turned, Math.Max(OffNorth(w, 1), OffNorth(w, 2)));
        }
        Assert.True(turned > 30, $"turned {turned:F0}°");
    }

    /// <summary>A ship of a column on the conn, damaged past doctrine's fall-out, stays in the line.</summary>
    [Fact]
    public void In_a_line_on_the_conn_she_does_not_fall_out()
    {
        var (w, ai) = Make(2);
        int g = w.Groups.Of(1)!.GroupId;
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.FallOutGunsShare, 1.01));   // any damage at all
        w.Apply(ai.Orders.ByHand.Conn(Unit.Group(g), true));
        w.AdvanceTo(w.Time + 30_000);
        w.Apply(DamageSystem.SideHole(2, HoleAt.Mid, 0, 3, 0.01));
        w.AdvanceTo(w.Time + 30_000);
        Assert.Equal(CaptainReason.None, ai.Captains.ReasonOf(2));
        Assert.Equal(g, w.Groups.Of(2)?.GroupId);
        Assert.True(w.Groups.InLine(2));
    }
}
