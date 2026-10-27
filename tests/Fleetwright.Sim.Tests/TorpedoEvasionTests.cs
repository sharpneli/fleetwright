using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Torpedoes seen and combed (docs/battle/command.md 5.1a, decision 10): a battleship whose lookouts see a spread
/// coming at her turns parallel to the tracks and gives her course back once they're past; with no torpedo evasion she
/// holds on. Measured: the hits either way.</summary>
public class TorpedoEvasionTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>A battleship (side 0) steaming north at 15 kn, a destroyer (side 1) 3 km off her starboard beam holding
    /// her fire; the destroyer's spread of 4 launched at her as she truly is.</summary>
    static World Make(TorpedoEvasion evasion, ulong seed, double speed = 1)
    {
        var w = new World(Start, seed, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetSpeed(speed));
        w.Apply(Command.SetWeather(25_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TorpedoEvasion, (double)evasion));
        int bb = w.Ships.AddClass(Fleet.Get("super_dreadnought").Class!), dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        var p = Earth.Position(56.0, 4.0);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 3_000);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(bb, 56.0, 4.0, 0, 15, 0));
        w.Apply(ShipSystem.Spawn(dd, la, lo, 0, 15, 1));
        w.Apply(ai.Orders.Course(Unit.Ship(1), 0));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(2)));
        w.Apply(ai.Orders.HoldFire(Unit.Ship(1)));
        w.AdvanceTo(w.Time + 30_000);
        w.Apply(TorpedoSystem.Launch(2, 1, 4, truth: true));
        return w;
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    [Fact]
    public void Seeing_a_spread_coming_she_combs_it_then_resumes_her_course()
    {
        var w = Make(TorpedoEvasion.Comb, 7);
        double turned = 0;
        for (int s = 0; s < 240; s += 5)
        {
            w.AdvanceTo(w.Time + 5_000);
            double d = Ship(w, 1).HeadingDeg;
            turned = Math.Max(turned, Math.Abs(d - 360 * Math.Round(d / 360)));
        }
        output.WriteLine($"she turned up to {turned:F0}° off north; torpedoes hit {w.Torpedoes.Hits}");
        Assert.True(turned > 45, $"turned {turned:F0}°");
        w.AdvanceTo(w.Time + 5 * 60_000);
        Assert.Equal(0, Ship(w, 1).CourseOrderDeg, 3);   // her course given back
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void What_combing_buys_her(double speed)
    {
        int comb = 0, steady = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var a = Make(TorpedoEvasion.Comb, seed, speed);
            a.AdvanceTo(a.Time + 6 * 60_000);
            comb += a.Torpedoes.Hits;
            var b = Make(TorpedoEvasion.None, seed, speed);
            b.AdvanceTo(b.Time + 6 * 60_000);
            steady += b.Torpedoes.Hits;
        }
        output.WriteLine($"at {speed}x, hits of 24 torpedoes: combing {comb}, holding on {steady}");
        Assert.True(comb < steady, $"combing {comb}, holding on {steady}");
    }

    /// <summary>The coarse tier (docs/battle/coarse.md J3): her captain thinks once a minute, but his lookouts are woken
    /// the moment the tracks come into sight, worked out ahead along both tracks, so she's ordered to comb when she would
    /// be on the fine tick (within its second's think), and gives her course back once they're past. (She then turns at
    /// once, a coarse turn having no helm lag: J4's to match.)</summary>
    [Fact]
    public void Coarse_she_combs_when_the_tracks_come_into_sight()
    {
        long Combs(World w)
        {
            for (int ms = 0; ms < 240_000; ms += 50)
            {
                w.AdvanceTo(w.Time + 50);
                if (Math.Abs(Ship(w, 1).CourseOrderDeg) > 1e-6)
                    return w.Time.Ms;
            }
            return -1;
        }
        var fine = Make(TorpedoEvasion.Comb, 7);
        var coarse = Make(TorpedoEvasion.Comb, 7, 1000);
        long f = Combs(fine), c = Combs(coarse);
        output.WriteLine($"ordered to comb {(f - Start.Ms) / 1000.0:F2} s in on the fine tick, {(c - Start.Ms) / 1000.0:F2} s coarse");
        Assert.Equal(ClockTier.Coarse, Ship(coarse, 1).Tier);
        Assert.True(f > 0 && c > 0, "she combs either way");
        Assert.InRange((c - f) / 1000.0, -1.05, 0.05);   // coarse at the moment of sight; the fine tick at its next second
        coarse.AdvanceTo(coarse.Time + 6 * 60_000);
        Assert.Equal(0, Ship(coarse, 1).CourseOrderDeg, 3);   // her course given back
    }
}
