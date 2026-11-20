using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Shipgen;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Stragglers (docs/battle/command.md "Stragglers"; the user, 2026-11-20): a ship that can't keep up and stays in
/// her line (neutral doctrine never falls out, nor does a brave captain's ship, nor one whose loss is back within its wait)
/// mustn't hold up the ships astern of her. They close up on the ship ahead of her; she drops to the tail, hauled out while
/// the line passes, and rejoins at the rear where nobody would follow her.</summary>
public class StragglerTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 12);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };
    const double Kn = 15, Spacing = 500;

    /// <summary>Dreadnoughts in one line ahead steaming north at 15 kn: squadrons of <paramref name="n"/> ships each, one
    /// alone or several in a force in line ahead. The unit's id.</summary>
    static (World W, AiSystem Ai, int Unit) Make(int n, int squadrons = 1)
    {
        var w = new World(Start, 3, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        int bb = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        var p = Earth.Position(56, 4);
        var f = Earth.Forward(p, 180);
        for (int i = 0; i < n * squadrons; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(bb, la, lo, 0, Kn));
            Earth.Advance(ref p, ref f, Spacing);
        }
        int unit = 0;
        for (int s = 0; s < squadrons; s++)
        {
            int first = s * n + 1;
            w.Apply(GroupSystem.Create(first, Formation.Column, Spacing));
            int g = w.Groups.Of(first)!.GroupId;
            for (int e = first + 1; e < first + n; e++)
                w.Apply(GroupSystem.Add(g, e));
            if (squadrons == 1)
                unit = g;
            else if (s == 0)
            {
                w.Apply(GroupSystem.Form(g, Formation.Column, Spacing));
                unit = w.Groups.Get(g)!.Parent;
            }
            else
                w.Apply(GroupSystem.Enter(unit, g));
        }
        w.Apply(ai.Orders.HoldFire(Unit.Group(unit)));
        w.Apply(ai.Orders.Course(Unit.Group(unit), 0));
        w.Apply(ai.Orders.Speed(Unit.Group(unit), Kn));
        return (w, ai, unit);
    }

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static double Gap(World w, int a, int b) => Earth.Distance(Ship(w, a).Position, Ship(w, b).Position);

    void Print(World w, int n, string when)
    {
        var line = string.Join("  ", Enumerable.Range(1, n).Select(e =>
            $"{e}: {Ship(w, e).Speed / Handling.MsPerKnot:F1} kn{(e > 1 ? $" {Gap(w, 1, e):F0} m" : "")}{(w.Groups.IsStraggling(e) ? " (straggling)" : "")}"));
        output.WriteLine($"{when}: {line}");
    }

    /// <summary>Runs on 30 minutes, printing every five, and gives the closest any of <paramref name="others"/> came to the
    /// ship <paramref name="of"/>.</summary>
    double Run(World w, int n, int of, params int[] others)
    {
        double closest = double.MaxValue;
        for (int m = 1; m <= 30 * 6; m++)
        {
            w.AdvanceTo(w.Time + 10_000);
            foreach (int e in others)
                closest = Math.Min(closest, Gap(w, of, e));
            if (m % 30 == 0)
                Print(w, n, $"{m / 6} min after");
        }
        output.WriteLine($"closest to {of}: {closest:F0} m");
        return closest;
    }

    /// <summary>The second of four dreadnoughts loses most of her steam: the third and fourth close up on the guide past
    /// her, hauled out of their way, and she rejoins at the rear.</summary>
    [Fact]
    public void The_line_astern_of_a_straggler_closes_up_on_the_ship_ahead_of_her()
    {
        var (w, _, g) = Make(4);
        w.AdvanceTo(w.Time + 2 * 60_000);
        Print(w, 4, "formed");
        w.Apply(DamageSystem.FloodRooms(2, RoomKind.BoilerRoom, 3));
        double closest = Run(w, 4, 2, 3, 4);
        var group = w.Groups.Get(g)!;
        output.WriteLine($"line: {string.Join(", ", group.Line)}, members: {string.Join(", ", group.Members)}");
        Assert.True(Ship(w, 2).Speed < 0.7 * Ship(w, 1).Speed, "she's slowed");
        Assert.True(Ship(w, 3).Speed > 0.9 * Ship(w, 1).Speed, "the third keeps the line's speed");
        Assert.True(Gap(w, 1, 3) < 1.5 * Spacing, $"the third {Gap(w, 1, 3):F0} m astern of the guide");
        Assert.True(Gap(w, 1, 4) < 2.5 * Spacing, $"the fourth {Gap(w, 1, 4):F0} m astern of the guide");
        Assert.True(closest > 0.4 * Spacing, $"passed {closest:F0} m off her");   // she turned out of the line's way
        Assert.False(w.Groups.IsStraggling(2), "back in her line");
        Assert.Equal(new[] { 1, 3, 4, 2 }, group.Members.ToArray());   // at its rear
    }

    /// <summary>A battle line of two squadrons of three in one line ahead: the first squadron's last ship loses her steam. The
    /// second squadron's guide closes up on the ship ahead of her, and she keeps to the tail of the whole line, a straggler
    /// (back at her own squadron's end she'd hold up the second squadron again).</summary>
    [Fact]
    public void In_a_force_the_next_squadron_closes_up_past_her()
    {
        var (w, _, _) = Make(3, 2);
        w.AdvanceTo(w.Time + 2 * 60_000);
        Print(w, 6, "formed");
        w.Apply(DamageSystem.FloodRooms(3, RoomKind.BoilerRoom, 3));
        Run(w, 6, 3, 4, 5, 6);
        Assert.True(Gap(w, 1, 4) < 2.5 * Spacing, $"the second squadron's guide {Gap(w, 1, 4):F0} m astern of the guide");
        Assert.True(Gap(w, 1, 6) < 4.5 * Spacing, $"the last {Gap(w, 1, 6):F0} m astern of the guide");
        Assert.True(w.Groups.IsStraggling(3));
        Assert.Equal(3, w.Groups.Of(3)!.Line[^1]);
    }

    /// <summary>The second squadron's guide loses her steam: its next ship guides it, closed up on the first squadron, and
    /// she rejoins at her squadron's end, the end of the line.</summary>
    [Fact]
    public void A_squadrons_guide_straggling_hands_it_to_her_next()
    {
        var (w, _, _) = Make(3, 2);
        w.AdvanceTo(w.Time + 2 * 60_000);
        w.Apply(DamageSystem.FloodRooms(4, RoomKind.BoilerRoom, 3));
        Run(w, 6, 4, 5, 6);
        var b = w.Groups.Of(4)!;
        Assert.Equal(5, b.Guide);
        Assert.True(Gap(w, 1, 5) < 3.5 * Spacing, $"the second squadron's new guide {Gap(w, 1, 5):F0} m astern of the guide");
        Assert.True(Gap(w, 1, 6) < 4.5 * Spacing, $"the last {Gap(w, 1, 6):F0} m astern of the guide");
        Assert.Equal(new[] { 5, 6, 4 }, b.Members.ToArray());
    }

    /// <summary>The second ship's rudder is wrecked on a straight course: hit, but keeping her station, she keeps her place
    /// (a straggler is one that can't close a station she's lost).</summary>
    [Fact]
    public void A_ship_keeping_up_keeps_her_place_whatever_she_steers_by()
    {
        var (w, _, g) = Make(4);
        w.AdvanceTo(w.Time + 2 * 60_000);
        w.Apply(DamageSystem.WreckKind(2, ComponentKind.Rudder, 0));
        Run(w, 4, 2, 3, 4);
        output.WriteLine($"her steering: {w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(2)).Steering}");
        Assert.False(w.Groups.IsStraggling(2));
        Assert.Equal(new[] { 1, 2, 3, 4 }, w.Groups.Get(g)!.Line.ToArray());
        Assert.True(Gap(w, 1, 4) < 3.5 * Spacing, $"the fourth {Gap(w, 1, 4):F0} m astern of the guide");
    }

    /// <summary>A line keeping station with nobody slowed has no stragglers: those astern of their stations close them.</summary>
    [Fact]
    public void Ships_working_up_to_their_stations_dont_straggle()
    {
        var (w, ai, g) = Make(4);
        w.AdvanceTo(w.Time + 2 * 60_000);
        w.Apply(ai.Orders.Speed(Unit.Group(g), 19));   // the line rings up: each works up after the one ahead
        for (int m = 0; m < 20; m++)
        {
            w.AdvanceTo(w.Time + 30_000);
            for (int e = 1; e <= 4; e++)
                Assert.False(w.Groups.IsStraggling(e), $"{e} straggling at {m / 2.0} min");
        }
        w.Apply(ai.Orders.Course(Unit.Group(g), 90));   // and turns in succession
        for (int m = 0; m < 20; m++)
        {
            w.AdvanceTo(w.Time + 30_000);
            for (int e = 1; e <= 4; e++)
                Assert.False(w.Groups.IsStraggling(e), $"{e} straggling at {m / 2.0} min of the turn");
        }
        Print(w, 4, "after");
    }
}
