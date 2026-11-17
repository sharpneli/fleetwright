using System.Globalization;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen allocation`: how each side's admirals have allocated their forces (docs/battle/allocation.md AL0):
/// every unit down each side's tree, its ships, what it brings and the job it has (its role and task), at the start and
/// again at the first contact. The scenarios given, or the plan's cases (tests/Fleetwright.Sim.Tests/Allocation/ and three
/// of the shipped).</summary>
static class AllocationBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Each strength's gunfight shown (<c>--explain</c>).</summary>
    public static bool Explain;

    public static int Run(string root, IReadOnlyList<string> paths, SimSettings settings, string landPath, double minutes, bool land)
    {
        string repo = Path.GetFullPath(Path.Combine(root, ".."));
        string shipped = Path.Combine(repo, "Content", "Scenarios");
        if (paths.Count == 0)
            paths = [.. Directory.GetFiles(Path.Combine(repo, "tests", "Fleetwright.Sim.Tests", "Allocation"), "*.json").Order(StringComparer.Ordinal),
                Path.Combine(shipped, "prizes-war.json"), Path.Combine(shipped, "yalu.json"), Path.Combine(shipped, "jutland.json")];
        var presets = DoctrinePresets.Load(Path.Combine(repo, "Content", "Sim", "doctrine.json"));
        string[] designs = [Path.Combine(root, "designs"), Path.Combine(shipped, "Designs")];
        var shore = land ? Shore.Build(Land.Load(landPath)) : null;
        using var registry = new ShipClassRegistry(0);
        foreach (var path in paths)
        {
            var s = Scenario.Load(path, [shipped]);
            var classes = ScenarioLoader.RequestClasses(s, registry, designs);
            registry.WaitAll();
            foreach (var (name, id) in classes)
                if (registry.ErrorOf(id) is { } err)
                    Console.WriteLine($"  {name}: {err}");
            var w = new World(s.Start, s.Seed, settings, registry, shore);
            var ai = new AiSystem(w);
            var start = ScenarioLoader.Apply(s, w, ai, classes, presets);
            w.Apply(Command.SetPlayerSide(-1));
            Console.WriteLine($"{s.Name} ({Path.GetFileName(path)}): {s.ShipCount} ships");
            w.AdvanceTo(w.Time + 1000);   // the first thinks
            Print(w, ai, s, start, "at the start");
            long until = w.Time.Ms + (long)(minutes * 60_000);
            bool contact = false, shown = false;
            long half = w.Time.Ms + 30 * 60_000;
            while (w.Time.Ms < until && !(contact = Contact(ai, s)))
            {
                w.AdvanceTo(w.Time + 60_000);
                if (!shown && w.Time.Ms >= half && !Contact(ai, s))
                {
                    shown = true;
                    Print(w, ai, s, start, "half an hour in");   // the stations taken up (AL4's lookouts)
                }
            }
            if (contact)
            {
                var met = w.Time;
                w.AdvanceTo(w.Time + 5 * 60_000);   // the commanders' thinks on it
                Print(w, ai, s, start, $"five minutes after the first contact ({met})");
            }
            else
                Console.WriteLine($"  no contact in {minutes:0} min");
            Console.WriteLine();
        }
        return 0;
    }

    static bool Contact(AiSystem ai, Scenario s)
    {
        for (int side = 0; side < s.Sides.Length; side++)
            if (ai.Commanders.PhaseOf(side).Phase is CommanderPhase.Contact or CommanderPhase.Battle)
                return true;
        return false;
    }

    static void Print(World w, AiSystem ai, Scenario s, ScenarioStart start, string when)
    {
        Console.WriteLine($"  {when}, {w.Time}:");
        var groups = w.Groups;
        var store = w.Ships.Store;
        for (int side = 0; side < s.Sides.Length; side++)
        {
            var (phase, reason) = ai.Commanders.PhaseOf(side);
            Console.WriteLine($"    {s.Sides[side]}: the commander {phase}{(reason != CommanderReason.None ? $" ({reason})" : "")}");
            for (int k = 0; k < groups.Count; k++)
                if (groups[k].Parent == 0 && Side(w, groups[k]) == side)
                    Group(w, ai, start, groups[k], 3);
            for (int slot = 0; slot < store.Slots; slot++)
                if (store.IsAlive(slot) && store[slot].Side == side && groups.Of(store[slot].Entity) == null)
                    Line(w, ai, Unit.Ship(store[slot].Entity), 3, start.ShipNames.GetValueOrDefault(store[slot].Entity) ?? $"#{store[slot].Entity}", [slot]);
        }
    }

    static int Side(World w, GroupSystem.Group g) =>
        w.Ships.Store.SlotOf(w.Groups.GuideShip(g)) is int slot and >= 0 ? w.Ships.Store[slot].Side : -1;

    static void Group(World w, AiSystem ai, ScenarioStart start, GroupSystem.Group g, int depth)
    {
        var store = w.Ships.Store;
        var slots = new List<int>();
        Ships(w, g, slots);
        Line(w, ai, Unit.Group(g.GroupId), depth, g.Name + (g.IsForce ? " (force)" : ""), slots);
        if (g.Wings > 0 && store.SlotOf(g.Guide) is int lead and >= 0)
        {
            // its lookouts (AL4): where each is off the guide now, and where her station is
            var guide = store[lead];
            var parts = new List<string>();
            for (int i = g.Line.Count - g.Wings; i < g.Line.Count; i++)
                if (store.SlotOf(g.Line[i]) is int slot and >= 0)
                {
                    var p = store[slot].Position;
                    double km = Earth.Distance(guide.Position, p) / 1000, off = ((Earth.Bearing(guide.Position, p) - guide.HeadingDeg) % 360 + 540) % 360 - 180;
                    parts.Add(string.Format(Inv, "{0} {1:0.0} km {2:+0;-0}°", start.ShipNames.GetValueOrDefault(g.Line[i]) ?? $"#{g.Line[i]}", km, off));
                }
            Console.WriteLine(string.Format(Inv, "{0}lookouts at {1:0.0} km, 45°: {2}", new string(' ', 2 * depth + 2), g.WingM / 1000, string.Join(", ", parts)));
        }
        if (g.IsForce)
            foreach (int m in g.Members)
                if (w.Groups.Get(m) is { } sub)
                    Group(w, ai, start, sub, depth + 1);
    }

    static void Ships(World w, GroupSystem.Group g, List<int> into)
    {
        foreach (int m in g.Members)
            if (!g.IsForce)
            {
                if (w.Ships.Store.SlotOf(m) is int slot and >= 0)
                    into.Add(slot);
            }
            else if (w.Groups.Get(m) is { } sub)
                Ships(w, sub, into);
    }

    static void Line(World w, AiSystem ai, Unit u, int depth, string name, List<int> slots)
    {
        var store = w.Ships.Store;
        double tons = 0, kn = double.MaxValue;
        var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (int slot in slots)
        {
            var cls = store[slot].Handling.Class;
            tons += cls.Motion.FullDisplacementT;
            kn = Math.Min(kn, cls.Motion.MaxSpeedKn);
            kinds[cls.Name] = kinds.GetValueOrDefault(cls.Name) + 1;
        }
        var (kind, state, _) = ai.Tasks.Of(u);
        string role = ai.Roles.Of(u) + (ai.Roles.IsSet(u) ? " (set)" : "");
        string ships = string.Join(" + ", kinds.Select(p => $"{p.Value} {p.Key}"));
        string pad = new(' ', 2 * depth);
        var m = ai.Measures.Of(u);
        string measures = string.Format(Inv, "str {0,8:#,0} q {1,4:0.00} {2,5:0.0} kn {3} scr {4,5:0.00} sct {5,5:0.00}", m.StrengthT, m.Quality, m.SpeedKn, m.Attacker ? "att" : "   ", m.Screening, m.Scouting);
        if (Explain && ai.Measures.Explain(u) is var x)
            measures += string.Format(Inv, " [{0,6:0} m, lost {1:0.0000} v {2:0.0000}]", x.RangeM, x.LostA, x.LostB);
        Console.WriteLine(string.Format(Inv, "{0}{1,-" + Math.Max(4, 34 - 2 * depth) + "} {2,-10} {3,-18} {4,7:#,0} t  {5}  {6}",
            pad, name, role, kind == Sim.Ai.Tasks.TaskKind.None ? "its orders" : $"{kind} {state}", tons, measures, ships));
    }
}
