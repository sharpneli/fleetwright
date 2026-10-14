using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Lines of bearing and the manoeuvres (docs/battle/command.md 5.1b): a line ahead turned together 90° becomes
/// a line of bearing where it lay and keeps it, and turned back is a line ahead again; the battle turn-away reverses a
/// line of eight in a few minutes, its old rear leading, no two ships ever close; a force of two squadrons in one line
/// turns as one line. All in the open North Sea, dreadnoughts at 15 kn.</summary>
public class ManoeuvreTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Lat = 56.5, Lon = 4.0, Spacing = 500;

    /// <summary>n dreadnoughts in line ahead heading north, on their stations, in squadrons of <paramref name="per"/>;
    /// two or more squadrons make a force in line ahead (one line).</summary>
    static (World W, AiSystem Ai) Line(int n, int per = 0)
    {
        per = per == 0 ? n : per;
        var w = new World(Start, 5, Settings);
        var ai = new AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 180);
        for (int i = 0; i < n; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(c, la, lo, 0, 15));
            Earth.Advance(ref p, ref f, Spacing);
        }
        for (int s = 0; s < n / per; s++)
        {
            w.Apply(GroupSystem.Create(1 + s * per, Formation.Column, Spacing));
            for (int e = 2 + s * per; e <= (s + 1) * per; e++)
                w.Apply(GroupSystem.Add(s + 1, e));
        }
        if (n / per > 1)
        {
            w.Apply(GroupSystem.Form(1, Formation.Column, Spacing));
            for (int s = 2; s <= n / per; s++)
                w.Apply(GroupSystem.Enter(n / per + 1, s));
        }
        w.Apply(ai.Orders.Speed(Top(w), 15));
        Run(w, 120);
        return (w, ai);
    }

    static Unit Top(World w)
    {
        for (int k = 0; k < w.Groups.Count; k++)
            if (w.Groups[k].Parent == 0)
                return Unit.Group(w.Groups[k].GroupId);
        throw new InvalidOperationException();
    }

    static GroupSystem.Group TopGroup(World w) => w.Groups.Get(Top(w).GroupId)!;

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)(s * 1000));

    /// <summary>The worst ship's distance from its station in the top group's line (from its guide ship as it is), and
    /// its heading off the guide's.</summary>
    static (double Off, double Dh) Worst(World w)
    {
        var line = new List<(int Ship, double AsternM, double AbeamM)>();
        var top = TopGroup(w);
        w.Groups.LineOffsets(top, line);
        var guide = Ship(w, w.Groups.GuideShip(top));
        var up = guide.Position.Normalized();
        var starboard = Vec3d.Cross(guide.Forward, up);
        double off = 0, dh = 0;
        foreach (var (ship, astern, abeam) in line)
        {
            var station = (guide.Position - guide.Forward * astern + starboard * abeam).Normalized() * Earth.Radius;
            var m = Ship(w, ship);
            off = Math.Max(off, Earth.Distance(m.Position, station));
            double d = m.HeadingDeg - guide.HeadingDeg;
            dh = Math.Max(dh, Math.Abs(d - 360 * Math.Round(d / 360)));
        }
        return (off, dh);
    }

    /// <summary>The nearest any two ships come, m.</summary>
    static double Closest(World w, int n)
    {
        double least = double.MaxValue;
        for (int a = 1; a <= n; a++)
            for (int b = a + 1; b <= n; b++)
                least = Math.Min(least, Earth.Distance(Ship(w, a).Position, Ship(w, b).Position));
        return least;
    }

    [Fact]
    public void Turning_the_four_old_formations_gives_the_lines_of_bearing_they_lie_on()
    {
        Assert.Equal((0.0, 1000.0), Round(GroupSystem.OffsetFromGuide(Formation.Bearing, 500, 2, 90)));
        Assert.Equal((1000.0, 0.0), Round(GroupSystem.OffsetFromGuide(Formation.Bearing, 500, 2, 180)));
        foreach (var f in new[] { Formation.Column, Formation.LineAbreast, Formation.EchelonStarboard, Formation.EchelonPort })
            for (int i = 1; i < 4; i++)
                Assert.Equal(Round(GroupSystem.OffsetFromGuide(f, 500, i)), Round(GroupSystem.OffsetFromGuide(Formation.Bearing, 500, i, GroupSystem.BearingOf(f))));
    }

    static (double, double) Round((double A, double B) v) => (Math.Round(v.A, 6) + 0.0, Math.Round(v.B, 6) + 0.0);

    [Fact]
    public void A_line_ahead_turned_together_90_degrees_keeps_a_line_of_bearing_and_turns_back_into_line_ahead()
    {
        var (w, ai) = Line(6);
        w.Apply(ai.Orders.TurnTogether(Top(w), 90));
        var g = TopGroup(w);
        Assert.Equal(Formation.Bearing, g.Formation);   // at once: the line it'll be
        Assert.Equal(90, g.BearingDeg, 6);
        Run(w, 5 * 60);   // a dreadnought at 15 kn is round and steady in about four
        var (off, dh) = Worst(w);
        output.WriteLine($"5 min after turning together 90° to starboard: worst {off:F0} m off station, {dh:F1}° off the guide's heading; guide steers {Ship(w, g.Guide).HeadingDeg:F1}°");
        Assert.True(off < 30, $"{off:F0} m off station");
        Assert.True(dh < 3, $"{dh:F1}°");
        Assert.InRange(Ship(w, g.Guide).HeadingDeg, 89, 91);

        w.Apply(ai.Orders.TurnTogether(Top(w), -90));
        Assert.Equal(Formation.Column, g.Formation);
        Assert.Equal(1, g.Guide);
        Run(w, 8 * 60);
        (off, dh) = Worst(w);
        output.WriteLine($"8 min after turning back: worst {off:F0} m off station, {dh:F1}°");
        Assert.True(off < 60, $"{off:F0} m off station");
        Assert.True(dh < 3, $"{dh:F1}°");
    }

    [Fact]
    public void The_battle_turn_away_reverses_a_line_of_eight_in_a_few_minutes()
    {
        var (w, ai) = Line(8);
        var g = TopGroup(w);
        w.Apply(ai.Orders.BattleTurn(Top(w), toStarboard: true));
        Assert.Equal(8, g.Guide);   // the old rear leads
        Assert.Equal(Formation.Column, g.Formation);
        double closest = double.MaxValue, round = -1;
        for (int s = 5; s <= 10 * 60; s += 5)
        {
            Run(w, 5);
            closest = Math.Min(closest, Closest(w, 8));
            bool all = true;
            for (int e = 1; e <= 8; e++)
            {
                double d = Ship(w, e).HeadingDeg - 180;
                all &= Math.Abs(d - 360 * Math.Round(d / 360)) < 5;
            }
            if (all && round < 0)
                round = s;
        }
        // each ship turned the step after the one astern of her, so the reversed line comes out stretched (as Scheer's
        // did) and closes up on its new guide
        var (stretched, _) = Worst(w);
        Run(w, 10 * 60);
        var (off, dh) = Worst(w);
        output.WriteLine($"every ship round in {round / 60:F1} min; closest any two came {closest:F0} m; worst off station {stretched:F0} m at 10 min, {off:F0} m at 20 min ({dh:F1}°)");
        Assert.InRange(round, 60, 6 * 60);
        Assert.True(closest > 250, $"{closest:F0} m");
        Assert.True(off < 100, $"{off:F0} m off station");
    }

    [Fact]
    public void A_force_in_one_line_turns_together_as_one_line()
    {
        var (w, ai) = Line(8, per: 4);
        var top = TopGroup(w);
        Assert.True(top.IsForce);
        w.Apply(ai.Orders.TurnTogether(Top(w), 90));
        Assert.Equal(Formation.Bearing, top.Formation);
        var line = new List<(int Ship, double AsternM, double AbeamM)>();
        w.Groups.LineOffsets(top, line);
        for (int i = 0; i < line.Count; i++)
        {
            Assert.Equal(0, line[i].AsternM, 3);
            Assert.Equal(i * Spacing, line[i].AbeamM, 3);   // one line abreast, the squadrons' gap as their ships'
        }
        Run(w, 6 * 60);
        var (off, dh) = Worst(w);
        output.WriteLine($"6 min after the force turned 90°: worst {off:F0} m off station, {dh:F1}°");
        Assert.True(off < 100, $"{off:F0} m off station");
        Assert.True(dh < 3, $"{dh:F1}°");

        w.Apply(ai.Orders.TurnTogether(Top(w), -90));
        Assert.Equal(Formation.Column, top.Formation);
        Run(w, 8 * 60);
        (off, dh) = Worst(w);
        output.WriteLine($"8 min after turning back: worst {off:F0} m off station, {dh:F1}°");
        Assert.True(off < 100, $"{off:F0} m off station");
    }

    [Fact]
    public void Turns_replay_from_the_log_and_allocate_nothing()
    {
        var (live, ai) = Line(6);
        w0(live, ai);
        live.Apply(ai.Orders.BattleTurn(Top(live), toStarboard: false));
        Run(live, 4 * 60);
        live.Apply(ai.Orders.TurnTogether(Top(live), 45));
        Run(live, 20);   // the JIT's first pass through a turn under way
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(live, 3 * 60);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        var replay = new World(Start, 5, Settings);
        _ = new AiSystem(replay);
        replay.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        foreach (var sc in live.CommandLog)
        {
            replay.AdvanceTo(sc.At);
            replay.Apply(sc.Command);
        }
        replay.AdvanceTo(live.Time);
        Assert.Equal(live.ComputeHash(), replay.ComputeHash());

        static void w0(World w, AiSystem ai)
        {
            // one turn first, untimed: a turn's arrays are made once a group
            w.Apply(ai.Orders.TurnTogether(Top(w), 10));
            Run(w, 60);
        }
    }
}
