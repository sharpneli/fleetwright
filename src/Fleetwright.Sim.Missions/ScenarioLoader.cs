using Fleetwright.Shipgen;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Commanders;
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
    /// seed are the caller's (<see cref="Scenario.Start"/>, <see cref="Scenario.Seed"/>). Doctrine's presets are
    /// <paramref name="presets"/>, else <c>Content/Sim/doctrine.json</c> beside the scenario's folder, else the builtin
    /// default (<see cref="DoctrinePresets.Builtin"/>).</summary>
    public static ScenarioStart Apply(Scenario s, World w, AiSystem ai, IReadOnlyDictionary<string, int> classes, DoctrinePresets? presets = null)
    {
        var start = new ScenarioStart();
        presets ??= PresetsFor(s);
        for (int side = 0; side < s.Sides.Length; side++)
        {
            // every side's doctrine: its preset (the default unless named), then its own values over it
            var given = s.Doctrine.FirstOrDefault(d => d.Side == side);
            string name = given.Preset ?? DoctrinePresets.DefaultName;
            if (!presets.Has(name))
                throw new ScenarioException($"doctrine of {s.Sides[side]}: no preset \"{name}\" (have {string.Join(", ", presets.Names)})");
            foreach (var (f, v) in presets[name].Concat(given.Values ?? []))
                w.Apply(ai.Orders.SideDoctrine(side, f, v));
        }
        foreach (var (side, lat, lon) in s.Homes)
            w.Apply(ai.Orders.Home(side, lat, lon));
        for (int side = 0; side < s.Sides.Length; side++)
            w.Apply(ai.Orders.Commander(side, CommanderMode.Auto));   // every side's commander, on unless it's the player's
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
        var tasked = new List<(ScenarioUnit Top, Unit Unit)>();
        var loadouts = new Dictionary<(int Class, LoadoutSpec Spec), int>();   // a loadout of a class, made once (-1: it didn't fit)
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
                if (nth < spec.Dyes.Length && spec.Dyes[nth] != Dye.None)
                    w.Apply(ShipSystem.Dye(e, spec.Dyes[nth]));
                if (spec.Loadout is { } ls)
                {
                    if (!loadouts.TryGetValue((cls, ls), out int lo))
                    {
                        var errors = new List<string>();
                        var made = w.Ships.Registry.Get(cls)!.Load(ls, errors);
                        loadouts[(cls, ls)] = lo = made == null ? -1 : w.Ships.Registry.AddLoadout(made);
                        if (made == null)
                            start.Warnings.Add($"{spec.Design}: {ls.Name}: {string.Join("; ", errors)} (her design's shells kept)");
                    }
                    if (lo >= 0)
                        w.Apply(ShipSystem.Load(e, lo));
                }
            }

            // the groups bottom-up, then the top unit's orders
            int topGroup = Form(top, w, ai, entities, start);
            Unit unit;
            if (topGroup > 0)
                unit = Unit.Group(topGroup);
            else if (entities.TryGetValue((top, 0), out int lone))
            {
                unit = Unit.Ship(lone);
                foreach (var (f, v) in top.Doctrine)
                    w.Apply(ai.Orders.Doctrine(unit, f, v));
            }
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
            if (top.Tasks.Count > 0)
                tasked.Add((top, unit));
        }
        foreach (var (top, unit) in tasked)
            for (int k = 0, given = 0; k < top.Tasks.Count; k++)
                if (Task(top, top.Tasks[k], unit, w, ai, s, start, entities) is { } c)
                    w.Apply(given++ == 0 ? c : UnitOrders.Then(c));   // the first at once, the rest one after another
        // the matchup's profiles and tables for every class at load, not at the first judgement in play (docs/battle/command.md 5.1g)
        ai.Judge.Matchups.Warm(w);
        return start;
    }

    /// <summary>A top unit's opening task (docs/battle/command.md 5.1c), once every unit is placed: its "of" is the first
    /// group of that name (any level), else a top unit of one ship of that name; one not found (a ladder took only part
    /// of a scenario) is a warning, and the task is left out.</summary>
    static Command? Task(ScenarioUnit top, ScenarioTask t, Unit unit, World w, AiSystem ai, Scenario s, ScenarioStart start, Dictionary<(ScenarioUnit, int), int> entities)
    {
        var o = ai.Orders;
        Unit of = default;
        if (t.Of != null)
        {
            for (int k = 0; k < w.Groups.Count && of.Id == 0; k++)
                if (w.Groups[k].Name == t.Of)
                    of = Unit.Group(w.Groups[k].GroupId);
            if (of.Id == 0 && s.Units.FirstOrDefault(u => u.Name == t.Of && !u.IsForce && u.ShipCount == 1) is { } lone &&
                entities.TryGetValue((lone, 0), out int e))
                of = Unit.Ship(e);
            if (of.Id == 0)
            {
                start.Warnings.Add($"{top.Name}: its task \"{t.Kind}\" of \"{t.Of}\": no such unit here; left out");
                return null;
            }
        }
        var (lat, lon) = t.At ?? (0, 0);
        return t.Kind switch
        {
            "go to" => o.GoTo(unit, lat, lon, t.Kn),
            "patrol" => o.Patrol(unit, lat, lon),
            "search" => o.Search(unit, lat, lon, t.DistanceM),
            "join" => o.Join(unit, of),
            "scout" => o.Scout(unit, of, t.DistanceM, t.OffBowDeg),
            "screen" => o.Screen(unit, of, t.DistanceM),
            "cover" => o.Cover(unit, of, t.DistanceM),
            "shadow" => o.Shadow(unit, of, t.DistanceM),
            _ => t.At != null ? o.Withdraw(unit, lat, lon) : o.WithdrawHome(unit),
        };
    }

    /// <summary>Doctrine's presets for a scenario read from a file: Content/Sim/doctrine.json beside its folder
    /// (Content/Scenarios), else the builtin default.</summary>
    static DoctrinePresets PresetsFor(Scenario s)
    {
        if (s.Folder != null && Path.Combine(s.Folder, "..", "Sim", "doctrine.json") is var p && File.Exists(p))
        {
            try
            {
                return DoctrinePresets.Load(p);
            }
            catch (Exception x) when (x is ArgumentException or System.Text.Json.JsonException)
            {
                throw new ScenarioException($"{p}: {x.Message}");
            }
        }
        return DoctrinePresets.Builtin;
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
    static int Form(ScenarioUnit u, World w, AiSystem ai, Dictionary<(ScenarioUnit, int), int> entities, ScenarioStart start)
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
                int g = Form(c, w, ai, entities, start);
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
            foreach (var (f, v) in u.Doctrine)
                w.Apply(ai.Orders.Doctrine(Unit.Group(id), f, v));
        }
        return id;
    }
}
