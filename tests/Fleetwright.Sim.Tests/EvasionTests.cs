using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Evasion under fire (docs/battle/command.md 5.1a): a ship running from fire beyond doctrine's range zigzags
/// about the course she wants, legs of doctrine's length either side by its swing; with steady doctrine she runs
/// straight. Measured: the hits she takes either way.</summary>
public class EvasionTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>A light cruiser (side 0, fighting by doctrine, so outgunned she turns away) 14 km west of a battleship
    /// (side 1) firing at will; both steaming north.</summary>
    static (World W, AiSystem Ai) Make(Evasion style, ulong seed = 7)
    {
        var w = new World(Start, seed, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Engage, (double)Engage.Fight));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Evasion, (double)style));
        int lc = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!), bb = w.Ships.AddClass(Fleet.Get("super_dreadnought").Class!);
        var p = Earth.Position(56.0, 4.0);
        var f = Earth.Forward(p, 90);
        var q = p;
        Earth.Advance(ref q, ref f, 14_000);
        var (la, lo) = Earth.LatLon(q);
        w.Apply(ShipSystem.Spawn(lc, 56.0, 4.0, 0, 20, 0));
        w.Apply(ShipSystem.Spawn(bb, la, lo, 0, 12, 1));
        return (w, ai);
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    /// <summary>The spread of the course ordered over a stretch, degrees, and the hits taken by the end of it.</summary>
    static (double Spread, int Hits) Run(World w, double minutes)
    {
        double lo = double.MaxValue, hi = double.MinValue, first = double.NaN;
        for (int s = 0; s < minutes * 60; s += 5)
        {
            w.AdvanceTo(w.Time + 5_000);
            if (w.Ships.Store.SlotOf(1) < 0)
                break;
            double c = Ship(w, 1).CourseOrderDeg;
            first = double.IsNaN(first) ? c : first;
            double d = c - first;
            d -= 360 * Math.Round(d / 360);
            (lo, hi) = (Math.Min(lo, d), Math.Max(hi, d));
        }
        int hits = w.Ships.Store.SlotOf(1) is int k and >= 0 ? w.Ships.Store.Combat.Condition(k).Hits : -1;
        return (hi - lo, hits);
    }

    [Fact]
    public void Running_under_fire_she_zigzags_and_steady_she_does_not()
    {
        var (w, ai) = Make(Evasion.Zigzag);
        w.AdvanceTo(w.Time + 3 * 60_000);   // met, outgunned, turned away
        Assert.Equal(CaptainOrder.Fight, ai.Captains.Of(1).Order);
        var (spread, _) = Run(w, 6);
        output.WriteLine($"zigzag: her course ordered over 6 min spans {spread:F0}°");
        Assert.InRange(spread, 45, 90);   // ±30° about a course that turns a little as she draws away

        var (w2, ai2) = Make(Evasion.Steady);
        w2.AdvanceTo(w2.Time + 3 * 60_000);
        var (spread2, _) = Run(w2, 6);
        output.WriteLine($"steady: {spread2:F0}°");
        Assert.True(spread2 < 15, $"steady spans {spread2:F0}°");
    }

    /// <summary>Not a pass/fail on the numbers (a few seeds is no statistic): what the zigzag buys her against a
    /// battleship's fire as it is in v0, for command.md.</summary>
    [Fact]
    public void What_the_zigzag_buys_her()
    {
        int zig = 0, steady = 0;
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var (w, _) = Make(Evasion.Zigzag, seed);
            zig += Run(w, 15).Hits;
            var (w2, _) = Make(Evasion.Steady, seed);
            steady += Run(w2, 15).Hits;
        }
        output.WriteLine($"hits taken over 4 runs of 15 min: zigzag {zig}, steady {steady}");
    }
}
