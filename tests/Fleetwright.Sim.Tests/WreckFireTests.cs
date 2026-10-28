using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Wrecks with no sign of life (docs/battle/crew.md N5b): a ship seen burning whose guns have gone quiet reads as
/// silent, and burning, silent and stopped as done for; at will a line shifts its fire to another target in reach, and
/// with none its doctrine ceases or finishes her; a gun of hers seen firing brings the fire back at once.</summary>
public class WreckFireTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false, HitsDoDamage = false };
    const double Lat = 56.0, Lon = 4.0;

    /// <summary>Ours (entity 1) on 0°, stopped; the cripple (2) on her starboard beam at 7 km, burning and holding her fire;
    /// with another, a healthy one (3) at 13 km beyond her; the side's doctrine on wrecks given.</summary>
    static (World W, AiSystem Ai) Make(WreckFire wrecks, bool other)
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 0, 0));
        foreach (double m in other ? new[] { 7_000.0, 13_000 } : [7_000])
        {
            var p = Earth.Position(Lat, Lon);
            var f = Earth.Forward(p, 90);
            Earth.Advance(ref p, ref f, m);
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(c, la, lo, 0, 0, 1));
        }
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Wrecks, (double)wrecks));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(2)));
        Ablaze(w, 2);
        return (w, ai);
    }

    /// <summary>A ship on fire in the open: her upper works' spaces burning developed with fuel for hours (the dev's fire,
    /// to start her fire steps and have her damaged, then the open fires set beside it).</summary>
    static void Ablaze(World w, int entity)
    {
        w.Apply(DamageSystem.Burn(entity, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(entity));
        var c = k.Class;
        for (int i = 0; i < c.FireSpaces.Length; i++)
            if (c.FireSpaces[i].Kind == FireSpaceKind.Part)
            {
                double area = Math.Max(c.FireSpaces[i].AreaM2, 1);
                k.Fire[i] = new BurnState { Phase = BurnPhase.Developed, Q = 5, E = 1e9, E0 = 1e9, Involved = area, InvolvedMax = area, Seat = 0.1 };
            }
    }

    static int Target(World w) => w.Ships.Store.Combat.Target(w.Ships.Store.SlotOf(1), 0);

    static Contact Seen(World w, int entity) => w.Sightings.Of(0)![w.Sightings.Of(0)!.IndexOf(entity)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)(s * 1000));

    [Fact]
    public void A_burning_ship_gone_quiet_is_silent_and_her_guns_firing_bring_the_fire_back()
    {
        var (w, ai) = Make(WreckFire.Cease, other: true);
        Run(w, 60);
        Assert.Equal(2, Target(w));   // the nearer first
        long t0 = w.Time.Ms;
        while (Target(w) == 2 && w.Time.Ms - t0 < 15 * 60_000)
            Run(w, 10);
        var c = Seen(w, 2);
        output.WriteLine($"shifted after {(w.Time.Ms - t0) / 60_000.0:0.0} min: seen {c.Seen}, done for {c.DoneFor(w.Time.Ms, Settings):0.00}");
        Assert.Equal(3, Target(w));
        Assert.True((c.Seen & SeenFlags.Silent) != 0 && (c.Seen & SeenFlags.Burning) != 0);
        Assert.True(w.Time.Ms - t0 >= Settings.SeenSilentS * 1000, "not before she's been quiet a while");

        // a sign of life: a gun of hers fires, and she's taken under fire again within a salvo's time
        w.Apply(ai.Orders.FireAtWill(Unit.Ship(2)));
        long fired = 0;
        for (int i = 0; i < 600 && Target(w) != 2; i++)
        {
            Run(w, 1);
            if (fired == 0 && w.Ships.Store.Combat.LastFired(w.Ships.Store.SlotOf(2)) > 0)
                fired = w.Ships.Store.Combat.LastFired(w.Ships.Store.SlotOf(2));
        }
        output.WriteLine($"her first salvo at +{(fired - t0) / 1000.0:0} s; back on her {(w.Time.Ms - fired) / 1000.0:0} s after it");
        Assert.True(fired > 0);
        Assert.Equal(2, Target(w));
        Assert.True((Seen(w, 2).Seen & SeenFlags.Silent) == 0);
        Assert.InRange(w.Time.Ms - fired, 0, 30_000);
    }

    [Fact]
    public void With_nothing_else_in_reach_the_doctrine_ceases_or_finishes_her()
    {
        foreach (var wrecks in new[] { WreckFire.Cease, WreckFire.Finish })
        {
            var (w, _) = Make(wrecks, other: false);
            Run(w, 60);
            Assert.Equal(2, Target(w));
            Run(w, 10 * 60);
            output.WriteLine($"{wrecks}: target {Target(w)}, done for {Seen(w, 2).DoneFor(w.Time.Ms, Settings):0.00}");
            Assert.Equal(wrecks == WreckFire.Cease ? 0 : 2, Target(w));
        }
    }

    [Fact]
    public void A_ship_holding_her_fire_unhurt_isnt_silent()
    {
        var (w, _) = Make(WreckFire.Cease, other: true);
        Run(w, 8 * 60);
        var c = Seen(w, 3);
        Assert.Equal(SeenFlags.None, c.Seen & SeenFlags.Silent);
        Assert.Equal(0, c.DoneFor(w.Time.Ms, Settings));
    }
}
