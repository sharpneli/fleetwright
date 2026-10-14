using Fleetwright.Shipgen;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Missions;

/// <summary>What a scenario started: each named ship's entity, and what didn't come out as written (a design that
/// didn't build, a ship that found no sea).</summary>
public sealed class ScenarioStart
{
    public Dictionary<int, string> ShipNames { get; } = [];
    public List<string> Warnings { get; } = [];
    public int Ships { get; internal set; }
    public int Groups { get; internal set; }
}

/// <summary>Gives a <see cref="Scenario"/> to a world, in two steps: <see cref="RequestClasses"/> asks the registry for
/// every design at once (they build in parallel, in the background), then <see cref="Apply"/>, on a world that isn't
/// running yet, spawns every ship on its station, forms the groups bottom-up and gives the top units their orders, all
/// as commands, so the world's log replays it. Applying waits for the classes it spawns.</summary>
public static class ScenarioLoader
{
    /// <summary>Every design the scenario names, requested: the class id by design name. A design is looked for in the
    /// scenario's own <c>Designs/</c>, then in <paramref name="designFolders"/> in order (the shipped designs).</summary>
    public static Dictionary<string, int> RequestClasses(Scenario s, ShipClassRegistry registry, IReadOnlyList<string> designFolders)
    {
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string name in s.DesignNames())
        {
            string path = FindDesign(s, name, designFolders)
                ?? throw new ScenarioException($"no design \"{name}\" (in the scenario's Designs/ or {string.Join(", ", designFolders)})");
            ids[name] = registry.Request(Design.Load(path));
        }
        return ids;
    }

    static string? FindDesign(Scenario s, string name, IReadOnlyList<string> folders)
    {
        if (s.Folder != null && Path.Combine(s.Folder, "Designs", name + ".json") is var own && File.Exists(own))
            return own;
        foreach (var f in folders)
            if (Path.Combine(f, name + ".json") is var p && File.Exists(p))
                return p;
        return null;
    }

    /// <summary>The scenario into a world that hasn't run: its ships, groups and orders. The world's start time and
    /// seed are the caller's (<see cref="Scenario.Start"/>, <see cref="Scenario.Seed"/>).</summary>
    public static ScenarioStart Apply(Scenario s, World w, AiSystem ai, IReadOnlyDictionary<string, int> classes)
    {
        var start = new ScenarioStart();
        foreach (var (a, b) in s.Peace)
            w.Apply(Command.SetRelation((byte)a, (byte)b, Relation.Peace));
        if (s.Player >= 0)
            w.Apply(Command.SetPlayerSide(s.Player));
        if (s.Weather is { } we)
            w.Apply(Command.SetWeather(we.VisibilityM, we.WindFromDeg, we.WindMs));
        foreach (var (side, quality) in s.Crews)
            w.Apply(GunnerySystem.SetCrew(side, quality));
        foreach (var (side, factor) in s.Propellant)
            w.Apply(DamageSystem.SetPropellant(side, factor));
        var placed = new List<(ScenarioUnit Squadron, int Index, double AsternM, double AbeamM)>();
        var entities = new Dictionary<(ScenarioUnit, int), int>();   // a unit by identity (it has no Equals of its own)
        foreach (var top in s.Units)
        {
            // every ship's station about the top unit's guide, as the group system reckons it (GroupSystem.LineOffsets)
            placed.Clear();
            Place(top, (0, 0), placed);
            var (lat0, lon0) = top.At!.Value;
            var at = Earth.Position(lat0, lon0);
            var fwd = Earth.Forward(at, top.CourseDeg);
            var starboard = Vec3d.Cross(fwd, at.Normalized());
            foreach (var (sq, i, astern, abeam) in placed)
            {
                var spec = ShipAt(sq, i, out int nth);
                int cls = classes[spec.Design];
                if (w.Ships.Registry.Get(cls) == null)
                {
                    if (!start.Warnings.Any(x => x.StartsWith(spec.Design + ":", StringComparison.Ordinal)))
                        start.Warnings.Add($"{spec.Design}: {w.Ships.Registry.ErrorOf(cls) ?? "didn't build"}");
                    continue;
                }
                var (lat, lon) = Earth.LatLon(at - fwd * astern + starboard * abeam);
                // no faster than her design steams: spawned past it, she'd only creep down toward it, never steady (so
                // never on rails: 2.7c's tramps at 10 kn of 9)
                double kn = top.Kn, most = w.Ships.Registry.Get(cls)!.Motion.MaxSpeedKn;
                if (kn > most && most > 0)
                {
                    if (!start.Warnings.Any(x => x.StartsWith(spec.Design + " at", StringComparison.Ordinal)))
                        start.Warnings.Add($"{spec.Design} at {kn} kn, past her {most:0.#} kn: spawned at {most:0.#}");
                    kn = most;
                }
                int before = w.Ships.NewestEntity;
                w.Apply(ShipSystem.Spawn(cls, lat, lon, top.CourseDeg, kn, (byte)sq.Side));
                int e = w.Ships.NewestEntity;
                if (e == before)
                {
                    start.Warnings.Add($"{sq.Name}: a {spec.Design} at {lat:F3}, {lon:F3} found no sea");
                    continue;
                }
                entities[(sq, i)] = e;
                start.Ships++;
                if (nth < spec.Names.Length && spec.Names[nth].Length > 0)
                    start.ShipNames[e] = spec.Names[nth];
            }

            // the groups bottom-up, then the top unit's orders
            int topGroup = Form(top, w, entities, start);
            Unit unit;
            if (topGroup > 0)
                unit = Unit.Group(topGroup);
            else if (entities.TryGetValue((top, 0), out int lone))
                unit = Unit.Ship(lone);
            else
                continue;
            var o = ai.Orders;
            if (top.Route.Count > 0)
            {
                w.Apply(o.ClearRoute(unit));
                foreach (var (lat, lon) in top.Route)
                    w.Apply(o.AddWaypoint(unit, lat, lon));
                if (top.Loop)
                    w.Apply(o.LoopRoute(unit));
            }
            else
                w.Apply(o.Course(unit, top.CourseDeg));
            w.Apply(o.Speed(unit, top.Kn));
            if (s.Fight.Contains(top.Side))
                w.Apply(o.Standing(unit, fight: true));
        }
        return start;
    }

    /// <summary>Every ship of a unit with its station about the top unit's guide ship, in line order: the walk of
    /// GroupSystem.LineOffsets over the scenario's tree, by the same rules (<see cref="GroupSystem.OffsetFromGuide"/>,
    /// <see cref="GroupSystem.GroupStation"/>).</summary>
    static void Place(ScenarioUnit u, (double AsternM, double AbeamM) origin, List<(ScenarioUnit, int, double, double)> into)
    {
        if (!u.IsForce)
        {
            for (int i = 0; i < u.ShipCount; i++)
            {
                var (a, b) = GroupSystem.OffsetFromGuide(u.Formation, u.SpacingM, i);
                into.Add((u, i, origin.AsternM + a, origin.AbeamM + b));
            }
            return;
        }
        for (int k = 0; k < u.Units.Count; k++)
        {
            (double, double)? last = into.Count > 0 ? (into[^1].Item3, into[^1].Item4) : null;
            Place(u.Units[k], GroupSystem.GroupStation(u.Formation, u.SpacingM, k, origin, last), into);
        }
    }

    /// <summary>The i-th ship of a squadron: its design entry, and which of that entry's ships it is.</summary>
    static ScenarioShips ShipAt(ScenarioUnit sq, int i, out int nth)
    {
        foreach (var s in sq.Ships)
        {
            if (i < s.Count)
            {
                nth = i;
                return s;
            }
            i -= s.Count;
        }
        throw new ArgumentOutOfRangeException(nameof(i));
    }

    /// <summary>A unit's group, made of what spawned (its id; 0 if none spawned, or a top unit of one ship).</summary>
    static int Form(ScenarioUnit u, World w, Dictionary<(ScenarioUnit, int), int> entities, ScenarioStart start)
    {
        bool top = u.At != null;
        int id = 0;
        if (!u.IsForce)
        {
            if (top && u.ShipCount == 1)
                return 0;   // a ship on its own, not a group
            for (int i = 0; i < u.ShipCount; i++)
            {
                if (!entities.TryGetValue((u, i), out int e))
                    continue;
                if (id == 0)
                {
                    w.Apply(GroupSystem.Create(e, u.Formation, u.SpacingM));
                    id = w.Groups.Of(e)!.GroupId;
                }
                else
                    w.Apply(GroupSystem.Add(id, e));
            }
        }
        else
        {
            foreach (var c in u.Units)
            {
                int g = Form(c, w, entities, start);
                if (g == 0)
                    continue;
                if (id == 0)
                {
                    w.Apply(GroupSystem.Form(g, u.Formation, u.SpacingM));
                    id = w.Groups.Get(g)!.Parent;
                }
                else
                    w.Apply(GroupSystem.Enter(id, g));
            }
        }
        if (id > 0)
        {
            start.Groups++;
            if (u.Name.Length > 0)
                w.Groups.SetName(id, u.Name);
        }
        return id;
    }
}
