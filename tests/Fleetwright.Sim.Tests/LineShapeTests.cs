using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A unit's whole line in one order (docs/map-ui/battle-hud.md P1, <see cref="GroupSystem.Shape"/>): the Grand
/// Fleet in little, three squadrons of two divisions of four in six columns abreast, into one line ahead, back into its
/// columns, and abreast, each by one command to the fleet, however deep it nests. On the same course the far columns
/// drop back one column's length each (at most <see cref="SimSettings.FollowMaxSpeedDiffKn"/> slower), so the line takes
/// about an hour: the deployment's turn (<see cref="DeploymentTests"/>) is the quick way.</summary>
public class LineShapeTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 12);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };
    const double Kn = 14;

    static int Column(World w, AiSystem ai, int cls, Vec3d at, int n)
    {
        int first = w.Ships.NewestEntity + 1;
        var f = Earth.Forward(at, 180);
        var p = at;
        for (int i = 0; i < n; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(cls, la, lo, 0, Kn));
            Earth.Advance(ref p, ref f, 500);
        }
        w.Apply(GroupSystem.Create(first, Formation.Column, 500));
        int g = w.Groups[w.Groups.Count - 1].GroupId;
        for (int e = first + 1; e < first + n; e++)
            w.Apply(GroupSystem.Add(g, e));
        w.Apply(ai.Orders.HoldFire(Unit.Group(g)));
        return g;
    }

    /// <summary>Six columns of four dreadnoughts a mile apart abreast, steaming north at 14 kn: three forces of two.</summary>
    static (World W, AiSystem Ai, int Fleet) Make()
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        var at = Earth.Position(56, 4);
        int fleet = 0;
        for (int sq = 0; sq < 3; sq++)
        {
            int a = Column(w, ai, bb, Off(at, 90, sq * 3_600), 4), b = Column(w, ai, bb, Off(at, 90, sq * 3_600 + 1_800), 4);
            w.Apply(GroupSystem.Form(a, Formation.LineAbreast, 1_800));
            int force = w.Groups.Get(a)!.Parent;
            w.Apply(GroupSystem.Enter(force, b));
            if (sq == 0)
            {
                w.Apply(GroupSystem.Form(force, Formation.LineAbreast, 3_600));
                fleet = w.Groups.Get(force)!.Parent;
            }
            else
                w.Apply(GroupSystem.Enter(fleet, force));
        }
        w.Apply(ai.Orders.Speed(Unit.Group(fleet), Kn));
        return (w, ai, fleet);
    }

    static Vec3d Off(Vec3d from, double bearing, double m)
    {
        var f = Earth.Forward(from, bearing);
        Earth.Advance(ref from, ref f, m);
        return from;
    }

    /// <summary>The worst ship's distance from her station in the fleet's line, metres.</summary>
    static double Worst(World w, int fleet)
    {
        var line = new List<(int Ship, double AsternM, double AbeamM)>();
        var g = w.Groups.Get(fleet)!;
        w.Groups.LineOffsets(g, line);
        var guide = w.Ships.Store[w.Ships.Store.SlotOf(w.Groups.GuideShip(g))];
        var starboard = Vec3d.Cross(guide.Forward, guide.Position.Normalized());
        double worst = 0;
        foreach (var (ship, astern, abeam) in line)
        {
            var at = w.Ships.Store[w.Ships.Store.SlotOf(ship)];
            worst = Math.Max(worst, (at.Position - (guide.Position - guide.Forward * astern + starboard * abeam)).Length);
        }
        return worst;
    }

    /// <summary>Minutes until every ship is within 400 m of her station (at most <paramref name="max"/>; -1 if never).</summary>
    static double Form(World w, int fleet, double max)
    {
        for (int s = 1; s <= max * 60; s++)
        {
            w.AdvanceTo(w.Time + 1_000);
            if (s % 10 == 0 && Worst(w, fleet) < 400)
                return s / 60.0;
        }
        return -1;
    }

    [Fact]
    public void Columns_are_as_far_apart_as_a_column_is_long()
    {
        var (w, _, fleet) = Make();
        w.Apply(GroupSystem.Shape(fleet, LineShape.Columns));
        var top = w.Groups.Get(fleet)!;
        Assert.Equal(Formation.LineAbreast, top.Formation);
        Assert.Equal(4_000, top.SpacingM);   // two columns of 4 x 500 m in each squadron
        foreach (int m in top.Line)
        {
            var sq = w.Groups.Get(m)!;
            Assert.Equal((Formation.LineAbreast, 2_000.0), (sq.Formation, sq.SpacingM));
            foreach (int d in sq.Line)
                Assert.Equal((Formation.Column, 500.0), (w.Groups.Get(d)!.Formation, w.Groups.Get(d)!.SpacingM));
        }
        w.Apply(GroupSystem.Shape(fleet, LineShape.Abreast));
        Assert.Equal((Formation.LineAbreast, 4_000.0), (top.Formation, top.SpacingM));   // 8 ships a squadron at 500 m
        Assert.Equal(2_000, w.Groups.Get(top.Line[0])!.SpacingM);
        Assert.Equal(Formation.LineAbreast, w.Groups.Get(w.Groups.Get(top.Line[0])!.Line[0])!.Formation);
    }

    /// <summary>Abreast at a spacing given (a search's scouting line, docs/battle/balance.md BL5): all 24 ships one line,
    /// each that far from the next, however deep the fleet nests.</summary>
    [Fact]
    public void Abreast_at_a_spacing_is_one_scouting_line_of_every_ship()
    {
        var (w, _, fleet) = Make();
        w.Apply(GroupSystem.Shape(fleet, LineShape.Abreast, 7_200));
        var line = new List<(int Ship, double AsternM, double AbeamM)>();
        w.Groups.LineOffsets(w.Groups.Get(fleet)!, line);
        var abeam = line.Select(l => l.AbeamM).OrderBy(x => x).ToList();
        output.WriteLine($"abeam {string.Join(", ", abeam.Select(x => $"{x / 1000:F1}"))} km; astern {line.Max(l => Math.Abs(l.AsternM)):F0} m at most");
        Assert.Equal(24, abeam.Count);
        for (int i = 1; i < abeam.Count; i++)
            Assert.Equal(7_200, abeam[i] - abeam[i - 1], 1e-6);
        Assert.All(line, l => Assert.Equal(0, l.AsternM, 1e-6));
    }

    [Fact]
    public void Six_columns_form_one_line_and_back_by_one_order_each()
    {
        var (w, ai, fleet) = Make();
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(ai.Orders.Shape(Unit.Group(fleet), LineShape.OneLine));
        var top = w.Groups.Get(fleet)!;
        Assert.Equal((Formation.Column, 500.0), (top.Formation, top.SpacingM));
        foreach (int m in top.Line)
            Assert.Equal((Formation.Column, 500.0), (w.Groups.Get(m)!.Formation, w.Groups.Get(m)!.SpacingM));
        double line = Form(w, fleet, 90);
        output.WriteLine($"one line ahead of 24 in {line:F1} min");
        Assert.True(line > 0, $"not one line after 90 min: worst {Worst(w, fleet):F0} m off");

        w.Apply(ai.Orders.Shape(Unit.Group(fleet), LineShape.Columns));
        double columns = Form(w, fleet, 90);
        output.WriteLine($"six columns again in {columns:F1} min");
        Assert.True(columns > 0, $"not in columns after 90 min: worst {Worst(w, fleet):F0} m off");
    }
}
