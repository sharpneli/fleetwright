using System.Globalization;
using System.Text;
using System.Text.Json;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Missions;

/// <summary>A setup (docs/map-ui/launch.md "A setup is a scenario file"): a scenario as written plus its changes, in
/// order. Applying it is a pure function on the parsed scenario (<see cref="Apply"/>: the written one is never touched);
/// the launch screen edits the changes and the file is them written out, so the two can't disagree. The file is a
/// scenario file with <c>"from"</c> and none of <c>"units"</c>, <c>"only"</c> or <c>"sides"</c> (those compose a
/// scenario, as the scale test's ladder does):
/// <code>
/// { "scenario": 1, "name": "Tsushima · 14 in Borodinos", "from": "tsushima", "player": "Russia",
///   "designs": [ { "unit": "1st Armoured Division", "from": "borodino", "to": "borodino_14in" },
///                { "ship": "Oslyabya", "from": "oslyabya", "to": "borodino" }, { "from": "buyny", "to": "ikazuchi" } ],
///   "add": [ { "name": "3rd Armoured Division", "side": 0, "at": [34.035, 129.8], "course": 23, "kn": 11,
///              "ships": [ { "design": "oslyabya", "count": 4 } ] },
///            { "name": "Reserve", "joins": "Second Pacific Squadron", "ships": [ { "design": "aurora", "count": 2 } ] } ],
///   "counts": [ { "unit": "Cruiser Division", "design": "aurora", "by": 1 } ], "remove": [ "1st Destroyer Detachment" ],
///   "tempers": { "Cruiser Division": 0.2, "Imperator Aleksandr III": 0.3 },
///   "doctrine": { "Russia": { "Aggression": 0.5, "NightAction": "accept" } },
///   "crews": { "Japan": "average" }, "readiness": { "Russia": "closed up" }, "damage_control": { "Russia": "1916" },
///   "fire_discipline": { "Russia": "rn-1914" }, "fuel": { "Russia": 0.6 },
///   "weather": { "visibility_km": 8, "wind_from": 200, "wind_kn": 12 }, "until": "1905-05-28T06:00", "seed": 1906 }
/// </code>
/// <para>A swap names a unit (any level: every ship of that design under it), or a ship by her name (only her), or
/// neither (every ship of that design). <c>"player": null</c> watches. An added unit is placed on its own (a top unit:
/// "at", "course", "kn") or <c>"joins"</c> a force on its station (L3b); it takes a unit's name, side, formation, spacing
/// and units or ships (design, names, count), nothing more. A count takes ships off the end of a squadron's ships of a
/// design (struck, the last first) or adds unnamed ones; a removed unit has every ship under it struck. A side's setting
/// replaces the written one for that side only, and so does a side's doctrine field (its aggression, its rules: L4). A
/// temper names a unit (its admiral's) or a ship (her captain's); an added unit's admiral and his temper are its own
/// ("admiral"). The day (L5) is the weather (all of it: visibility and wind), the end, and the seed: a number, or
/// <c>"new"</c> for a new one each time it's begun (the launch screen draws it; <see cref="Apply"/> keeps the written
/// one).</para>
/// <para>A unit renamed (<c>"rename": { "5th Battle Squadron": "Evan-Thomas's squadron" }</c>) is renamed last, the
/// scenario's tasks that name it with it: every other change names units as written (or as added). A unit joining a
/// squadron makes it a force of the same name: its own ships its first unit ("5th Battle Squadron I"), the new one
/// astern.</para>
/// <para>The changes apply by kind (<see cref="SetupChange.Order"/>: the side, the units added, the swaps, the counts, the
/// removals and tempers, the renames, the sides' settings and doctrine, the day), each kind in the order made, so the file (written by kind)
/// reads back to the same scenario. A count names the design its ships have after the swaps, which <see cref="Swap"/>
/// keeps.</para></summary>
public sealed record ScenarioSetup
{
    /// <summary>The setup's own name ("Tsushima · 14 in Borodinos"), or "" for the written one's.</summary>
    public string Name { get; init; } = "";

    /// <summary>The scenario it's of, as <c>"from"</c> names it (its file's name, "tsushima").</summary>
    public string From { get; init; } = "";

    /// <summary>The changes in the order they apply; one per <see cref="SetupChange.Key"/>.</summary>
    public IReadOnlyList<SetupChange> Changes { get; init; } = [];

    public bool IsEmpty => Changes.Count == 0;

    /// <summary>The setup with a change made: it takes the place of any change of the same key.</summary>
    public ScenarioSetup With(SetupChange c)
    {
        var list = Changes.ToList();
        int at = list.FindIndex(x => x.Key == c.Key);
        if (at >= 0)
            list[at] = c;
        else
            list.Add(c);
        return this with { Changes = list };
    }

    /// <summary>The setup with the ships of design <paramref name="from"/> given <paramref name="to"/> (under a unit, one
    /// ship, or every one: <see cref="SwapDesign"/>), as the launch screen's picker does it: the change that brought
    /// <paramref name="from"/> there with the same reach is changed again, or taken away when <paramref name="to"/> puts
    /// back what it replaced, so going back and forth leaves no trail.</summary>
    public ScenarioSetup Swap(string? unit, string? ship, string from, string to)
    {
        if (from == to)
            return this;
        // a unit the setup added has its own design changed
        if (unit != null && ship == null && AddedHolding(unit) is { } add)
            return With(add with { Unit = Tree.Rebuild(add.Unit, unit, u => Swapped(u, from, to)) });
        var prior = Changes.OfType<SwapDesign>().FirstOrDefault(c => c.To == from && c.Unit == unit && c.Ship == ship);
        var next = prior == null ? With(new SwapDesign(unit, ship, from, to))
            : prior.From == to ? Without(prior.Key)
            : With(prior with { To = to });
        // the counts of the ships swapped name their new design (they apply after the swaps); a ship swapped on her own
        // leaves the count to the rest
        if (ship != null)
            return next;
        foreach (var c in next.Changes.OfType<ChangeCount>().Where(c => c.Design == from && (unit == null || c.Unit == unit)).ToList())
            next = next.Without(c.Key).Recount(c.Unit, to, c.By);
        return next;
    }

    static ScenarioUnit Swapped(ScenarioUnit u, string from, string to) =>
        u.With(u.Units.Select(x => Swapped(x, from, to)).ToList(), u.Ships.Select(e => e.Design == from ? e.Slice(0, e.Count, to) : e).ToList());

    /// <summary>The setup with <paramref name="by"/> more ships of a design in a squadron, or fewer (the launch screen's +
    /// and −): a unit the setup added has its own ships changed; any other's count is one change a row, taken away when
    /// it comes back to as written.</summary>
    public ScenarioSetup Resize(string unit, string design, int by)
    {
        if (by == 0)
            return this;
        if (AddedHolding(unit) is { } add)
            return With(add with { Unit = Tree.Rebuild(add.Unit, unit, u => ChangeCount.Resized(u, design, by, strike: false)) });
        return Recount(unit, design, by);
    }

    ScenarioSetup Recount(string unit, string design, int by)
    {
        var prior = Changes.OfType<ChangeCount>().FirstOrDefault(c => c.Unit == unit && c.Design == design);
        int total = (prior?.By ?? 0) + by;
        return total != 0 ? With(new ChangeCount(unit, design, total)) : prior != null ? Without(prior.Key) : this;
    }

    /// <summary>The unit the setup added that is, or holds, a unit of that name.</summary>
    public AddUnit? AddedHolding(string unit) => Changes.OfType<AddUnit>().FirstOrDefault(a => Tree.Holds(a.Unit, unit));

    /// <summary>The setup without a unit it added (its ✕), and the changes made to it, units joining it among them.</summary>
    public ScenarioSetup WithoutAdded(string name)
    {
        var add = Changes.OfType<AddUnit>().FirstOrDefault(a => a.Unit.Name == name);
        if (add == null)
            return this;
        bool Of(SetupChange c) => ReferenceEquals(c, add) || c is ChangeCount cc && Tree.Holds(add.Unit, cc.Unit) || c is SwapDesign { Unit: { } u } && Tree.Holds(add.Unit, u)
                                  || c is RemoveUnit r && Tree.Holds(add.Unit, r.Name);
        var next = this with { Changes = [.. Changes.Where(c => !Of(c))] };
        foreach (var j in next.Changes.OfType<AddUnit>().Where(j => j.Joins != null && (Tree.Holds(add.Unit, j.Joins) || j.Joins == name + " I")).ToList())
            next = next.WithoutAdded(j.Unit.Name);
        return next;
    }

    /// <summary>The name a unit is called by in the other changes: as written (or as added), before a rename.</summary>
    public string NameAsMade(string shown) => Changes.OfType<RenameUnit>().FirstOrDefault(c => c.To == shown)?.From ?? shown;

    /// <summary>The setup with a unit called by another name (the screen's rename, by the name it's shown by): a unit the
    /// setup added renamed in its own change (and a unit joining it told so); any other by a change of its own, one a
    /// unit, gone when it's called as written again.</summary>
    public ScenarioSetup Rename(string shown, string to)
    {
        if (shown == to)
            return this;
        string from = NameAsMade(shown);
        if (from == shown && AddedHolding(from) is { } add)
        {
            var next = Replace(add, add with { Unit = Tree.Rebuild(add.Unit, from, u => u.Renamed(to)) });
            return next with { Changes = [.. next.Changes.Select(c => c is AddUnit a && a.Joins == from ? a with { Joins = to } : c)] };
        }
        return from == to ? Without(new RenameUnit(from, to).Key) : With(new RenameUnit(from, to));
    }

    /// <summary>The setup with a unit's admiral's temper or a ship's captain's (by the name it's shown by) set: a unit the
    /// setup added has it in its own change; any other by a change of its own, gone when it's as written again.</summary>
    public ScenarioSetup Temper(Scenario written, string shown, double temper)
    {
        string name = NameAsMade(shown);
        if (AddedHolding(name) is { } add)
            return Replace(add, add with { Unit = Tree.Rebuild(add.Unit, name, u => u.Tempered(temper)) });
        written = written.Written ?? written;
        double was = Tree.Find(written.Units, name) is { } u ? u.Temper ?? 0 : SetTemper.CaptainOf(written.Units, name) ?? 0;
        return Math.Abs(temper - was) < 1e-9 ? Without(new SetTemper(name, temper).Key) : With(new SetTemper(name, temper));
    }

    /// <summary>The setup with a side's doctrine field set, or without the change when it's as written again.</summary>
    public ScenarioSetup Doctrine(Scenario written, int side, DoctrineField f, double value, DoctrinePresets presets)
    {
        written = written.Written ?? written;
        var c = new SetDoctrine(side, f, value);
        return Math.Abs(value - ScenarioLoader.SideDoctrine(written, side, f, presets)) < 1e-9 ? Without(c.Key) : With(c);
    }

    /// <summary>The setup with one change put in another's place.</summary>
    ScenarioSetup Replace(SetupChange old, SetupChange by) => this with { Changes = [.. Changes.Select(c => ReferenceEquals(c, old) ? by : c)] };

    /// <summary>The setup without a change (the screen's ✕), or without every change of a key.</summary>
    public ScenarioSetup Without(string key) => this with { Changes = [.. Changes.Where(x => x.Key != key)] };

    /// <summary>The scenario as set: the written one with every change applied in order; it remembers both
    /// (<see cref="Scenario.Setup"/>, <see cref="Scenario.Written"/>). A change that finds nothing to change (a swap of a
    /// design the unit hasn't) is a <see cref="ScenarioException"/>.</summary>
    public Scenario Apply(Scenario written)
    {
        written = written.Written ?? written;
        var s = written;
        foreach (var c in Changes.OrderBy(c => c.Order))   // stable: each kind in the order made
            s = c.Apply(s);
        return s with { Name = Name.Length > 0 ? Name : written.Name, Setup = this, Written = written };
    }

    // ------------------------------------------------------------------ the file

    /// <summary>Whether a file's object is a setup: <c>"from"</c>, and nothing that composes a scenario.</summary>
    internal static bool IsSetup(JsonElement r) =>
        r.TryGetProperty("from", out _) && !r.TryGetProperty("units", out _) && !r.TryGetProperty("only", out _) && !r.TryGetProperty("sides", out _);

    static readonly string[] Keys = ["scenario", "name", "from", "player", "rename", "add", "designs", "counts", "remove", "tempers", "doctrine", "crews", "readiness", "damage_control", "fire_discipline", "fuel", "weather", "until", "seed"];

    /// <summary>A setup file's changes, read against the scenario it's of.</summary>
    internal static ScenarioSetup Parse(JsonElement r, Scenario written)
    {
        foreach (var p in r.EnumerateObject())
            if (Array.IndexOf(Keys, p.Name) < 0)
                throw new ScenarioException($"a setup takes {string.Join(", ", Keys.Skip(1))}; not \"{p.Name}\"");
        var sides = written.Sides;
        var changes = new List<SetupChange>();
        if (r.TryGetProperty("player", out var pl))
        {
            string? player = pl.ValueKind == JsonValueKind.Null ? null : pl.GetString();
            if (player != null && Array.IndexOf(sides, player) < 0)
                throw new ScenarioException($"player: \"{player}\" isn't one of the sides ({string.Join(", ", sides)})");
            changes.Add(new PlayAs(player));
        }
        if (r.TryGetProperty("rename", out var rn))
        {
            if (rn.ValueKind != JsonValueKind.Object)
                throw new ScenarioException("rename: an object of units' names, { \"5th Battle Squadron\": \"Evan-Thomas's squadron\" }");
            foreach (var p in rn.EnumerateObject())
                changes.Add(new RenameUnit(p.Name, p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } to ? to
                    : throw new ScenarioException($"rename: \"{p.Name}\": a new name")));
        }
        if (r.TryGetProperty("add", out var ad))
        {
            if (ad.ValueKind != JsonValueKind.Array)
                throw new ScenarioException("add: a list of units, [ { \"name\": ..., \"side\": 0, \"at\": [lat, lon], ... } ]");
            int i = 0;
            foreach (var a in ad.EnumerateArray())
                changes.Add(AddUnit.Parse(a, $"add[{i++}]", written, changes.OfType<AddUnit>()));
        }
        if (r.TryGetProperty("counts", out var cs))
        {
            if (cs.ValueKind != JsonValueKind.Array)
                throw new ScenarioException("counts: a list, [ { \"unit\": ..., \"design\": ..., \"by\": 1 } ]");
            int i = 0;
            foreach (var c in cs.EnumerateArray())
            {
                string unit = Str(c, "unit"), design = Str(c, "design");
                if (unit.Length == 0 || design.Length == 0 || !c.TryGetProperty("by", out var by) || !by.TryGetInt32(out int n) || n == 0)
                    throw new ScenarioException($"counts[{i}]: \"unit\", \"design\" and \"by\", how many more (less than 0: fewer)");
                changes.Add(new ChangeCount(unit, design, n));
                i++;
            }
        }
        if (r.TryGetProperty("remove", out var rm))
        {
            if (rm.ValueKind != JsonValueKind.Array)
                throw new ScenarioException("remove: a list of units' names");
            foreach (var n in rm.EnumerateArray())
                changes.Add(new RemoveUnit(n.ValueKind == JsonValueKind.String && n.GetString() is { Length: > 0 } name ? name
                    : throw new ScenarioException("remove: a list of units' names")));
        }
        if (r.TryGetProperty("designs", out var ds))
        {
            if (ds.ValueKind != JsonValueKind.Array)
                throw new ScenarioException("designs: a list of swaps, [ { \"unit\": ..., \"from\": ..., \"to\": ... } ]");
            int i = 0;
            foreach (var d in ds.EnumerateArray())
            {
                string from = Str(d, "from"), to = Str(d, "to");
                if (from.Length == 0 || to.Length == 0)
                    throw new ScenarioException($"designs[{i}]: \"from\" and \"to\", the designs' names");
                changes.Add(new SwapDesign(Str(d, "unit") is { Length: > 0 } u ? u : null, Str(d, "ship") is { Length: > 0 } sh ? sh : null, from, to));
                i++;
            }
        }
        if (r.TryGetProperty("tempers", out var tm))
        {
            if (tm.ValueKind != JsonValueKind.Object)
                throw new ScenarioException("tempers: an object of units' and ships' names, { \"Cruiser Division\": 0.2 }");
            foreach (var p in tm.EnumerateObject())
                changes.Add(new SetTemper(p.Name, Scenario.Temper(p.Value, $"tempers: {p.Name}")));
        }
        if (r.TryGetProperty("doctrine", out var dc))
        {
            if (dc.ValueKind != JsonValueKind.Object)
                throw new ScenarioException("doctrine: an object of sides' names, { \"Russia\": { \"Aggression\": 0.5 } }");
            foreach (var p in dc.EnumerateObject())
            {
                int side = Array.IndexOf(sides, p.Name);
                if (side < 0)
                    throw new ScenarioException($"doctrine: \"{p.Name}\" isn't one of the sides ({string.Join(", ", sides)})");
                if (p.Value.ValueKind != JsonValueKind.Object)
                    throw new ScenarioException($"doctrine of {p.Name}: an object of fields, {{ \"Aggression\": 0.5 }}");
                foreach (var (f, v) in Scenario.DoctrineValues(p.Value, $"doctrine of {p.Name}", unit: false))
                    changes.Add(new SetDoctrine(side, f, v));
            }
        }
        if (r.TryGetProperty("crews", out var cr))
            foreach (var (side, q) in Scenario.ParseCrews(cr, sides))
                changes.Add(new SideSetting(SideKind.Crews, side, Experience.Name(q)));
        foreach (var (key, kind, what) in Named)
            if (r.TryGetProperty(key, out var e))
                foreach (var (side, name) in Scenario.ParseNames(key, what, e, sides))
                    changes.Add(new SideSetting(kind, side, name));
        if (r.TryGetProperty("fuel", out var fu))
            foreach (var (side, share) in Scenario.ParseFuel(fu, sides))
                changes.Add(new SideSetting(SideKind.Fuel, side, share.ToString("0.###", CultureInfo.InvariantCulture)));
        if (r.TryGetProperty("weather", out var we))
            changes.Add(new SetWeather(Scenario.ParseWeather(we)));
        if (r.TryGetProperty("until", out var un))
            changes.Add(new SetEnd(Scenario.ParseTime(un.GetString() ?? "", "until")));
        if (r.TryGetProperty("seed", out var se))
        {
            if (se.ValueKind == JsonValueKind.String && se.GetString() == "new")
                changes.Add(new SetSeed(null));
            else if (se.TryGetUInt64(out ulong seed))
                changes.Add(new SetSeed(seed));
            else
                throw new ScenarioException("seed: a whole number, or \"new\" for a new one each time");
        }
        return new ScenarioSetup { Name = Str(r, "name"), From = Str(r, "from"), Changes = changes };
    }

    static readonly (string Key, SideKind Kind, string What)[] Named =
    [
        ("readiness", SideKind.Readiness, "a readiness's name"),
        ("damage_control", SideKind.DamageControl, "a doctrine's name"),
        ("fire_discipline", SideKind.FireDiscipline, "a discipline's name"),
    ];

    static string Str(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    /// <summary>The setup as a file of the scenario <see cref="From"/>: read back (<see cref="Scenario.Load"/>, with
    /// the folder its "from" is in), it applies to the same scenario.</summary>
    public string ToJson(Scenario written)
    {
        string from = From.Length > 0 ? From : throw new InvalidOperationException("a setup's file needs the scenario it's of (From)");
        written = written.Written ?? written;
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteNumber("scenario", Scenario.Version);
            if (Name.Length > 0)
                w.WriteString("name", Name);
            w.WriteString("from", from);
            foreach (var c in Changes.OfType<PlayAs>())
                if (c.Side == null)
                    w.WriteNull("player");
                else
                    w.WriteString("player", c.Side);
            var renames = Changes.OfType<RenameUnit>().ToList();
            if (renames.Count > 0)
            {
                w.WriteStartObject("rename");
                foreach (var c in renames)
                    w.WriteString(c.From, c.To);
                w.WriteEndObject();
            }
            var adds = Changes.OfType<AddUnit>().ToList();
            if (adds.Count > 0)
            {
                w.WriteStartArray("add");
                foreach (var a in adds)
                    a.Write(w);
                w.WriteEndArray();
            }
            var swaps = Changes.OfType<SwapDesign>().ToList();
            if (swaps.Count > 0)
            {
                w.WriteStartArray("designs");
                foreach (var sw in swaps)
                {
                    w.WriteStartObject();
                    if (sw.Unit != null)
                        w.WriteString("unit", sw.Unit);
                    if (sw.Ship != null)
                        w.WriteString("ship", sw.Ship);
                    w.WriteString("from", sw.From);
                    w.WriteString("to", sw.To);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            var counts = Changes.OfType<ChangeCount>().ToList();
            if (counts.Count > 0)
            {
                w.WriteStartArray("counts");
                foreach (var c in counts)
                {
                    w.WriteStartObject();
                    w.WriteString("unit", c.Unit);
                    w.WriteString("design", c.Design);
                    w.WriteNumber("by", c.By);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            var removed = Changes.OfType<RemoveUnit>().ToList();
            if (removed.Count > 0)
            {
                w.WriteStartArray("remove");
                foreach (var c in removed)
                    w.WriteStringValue(c.Name);
                w.WriteEndArray();
            }
            var tempers = Changes.OfType<SetTemper>().ToList();
            if (tempers.Count > 0)
            {
                w.WriteStartObject("tempers");
                foreach (var c in tempers)
                    w.WriteNumber(c.Name, c.Value);
                w.WriteEndObject();
            }
            var doctrine = Changes.OfType<SetDoctrine>().ToList();
            if (doctrine.Count > 0)
            {
                w.WriteStartObject("doctrine");
                foreach (var side in doctrine.Select(c => c.Side).Distinct())
                {
                    w.WriteStartObject(written.Sides[side]);
                    foreach (var c in doctrine.Where(c => c.Side == side))
                        if (DoctrinePresets.NameOf(c.Field, c.Value) is { } word)
                            w.WriteString(c.Field.ToString(), word);
                        else
                            w.WriteNumber(c.Field.ToString(), c.Value);
                    w.WriteEndObject();
                }
                w.WriteEndObject();
            }
            foreach (var kind in Enum.GetValues<SideKind>())
            {
                var set = Changes.OfType<SideSetting>().Where(x => x.Kind == kind).ToList();
                if (set.Count == 0)
                    continue;
                w.WriteStartObject(SideSetting.FileKey(kind));
                foreach (var x in set)
                    if (kind == SideKind.Fuel || kind == SideKind.Crews && !Experience.Names.Contains(x.Value))
                        w.WriteNumber(written.Sides[x.Side], double.Parse(x.Value, CultureInfo.InvariantCulture));
                    else
                        w.WriteString(written.Sides[x.Side], x.Value);
                w.WriteEndObject();
            }
            foreach (var c in Changes.OfType<SetWeather>())
            {
                w.WriteStartObject("weather");
                w.WriteNumber("visibility_km", Math.Round(c.Weather.VisibilityM / 1000, 3));
                w.WriteNumber("wind_from", c.Weather.WindFromDeg);
                w.WriteNumber("wind_kn", Math.Round(c.Weather.WindMs / 0.514444, 3));
                if (c.Weather.AirOverSeaK != 0)
                    w.WriteNumber("air_over_sea_k", c.Weather.AirOverSeaK);
                if (c.Weather.Cloud != 0)
                    w.WriteNumber("cloud", c.Weather.Cloud);
                w.WriteEndObject();
            }
            foreach (var c in Changes.OfType<SetEnd>())
                w.WriteString("until", c.Until.ToUtc().ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture));
            foreach (var c in Changes.OfType<SetSeed>())
                if (c.Seed is { } seed)
                    w.WriteNumber("seed", seed);
                else
                    w.WriteString("seed", "new");
            w.WriteEndObject();
        }
        return $"// a setup of {written.Name} (docs/map-ui/launch.md \"A setup is a scenario file\"): the scenario \"{from}\" and these changes\n"
               + Encoding.UTF8.GetString(ms.ToArray()) + "\n";
    }
}

/// <summary>One change of a setup: what it's of (<see cref="Key"/>: a later change of the same key replaces it), how it
/// changes a scenario, and how it reads in the list of changes against the written one.</summary>
public abstract record SetupChange
{
    public abstract string Key { get; }

    /// <summary>Where its kind applies among the others (<see cref="ScenarioSetup.Apply"/>), as the file is written.</summary>
    internal abstract int Order { get; }

    internal abstract Scenario Apply(Scenario s);

    /// <summary>The change in a line, as the launch screen's list and the result say it.</summary>
    public abstract string Describe(Scenario written);
}

/// <summary>Play as a side (its name), or watch (null).</summary>
public sealed record PlayAs(string? Side) : SetupChange
{
    public override string Key => "player";
    internal override int Order => 0;

    internal override Scenario Apply(Scenario s) =>
        Side == null || Array.IndexOf(s.Sides, Side) >= 0 ? s with { PlayerName = Side }
            : throw new ScenarioException($"player: \"{Side}\" isn't one of the sides ({string.Join(", ", s.Sides)})");

    public override string Describe(Scenario written) =>
        (Side == null ? "Watch" : $"Play as {Side}") + $" (written: {(written.PlayerName is { } p ? "play as " + p : "watch")})";
}

/// <summary>Ships of one design given another: every one under a unit, one ship by her name, or every one in the
/// scenario.</summary>
public sealed record SwapDesign(string? Unit, string? Ship, string From, string To) : SetupChange
{
    public override string Key => $"design|{Unit}|{Ship}|{From}";
    internal override int Order => 2;

    internal override Scenario Apply(Scenario s)
    {
        bool found = false;
        var units = s.Units.Select(u => Swap(u, Unit == null || u.Name == Unit, ref found)).ToList();
        if (!found)
            throw new ScenarioException($"designs: no {From} {(Ship != null ? $"named {Ship}" : Unit != null ? $"in {Unit}" : "in the scenario")}");
        return s with { Units = units };
    }

    ScenarioUnit Swap(ScenarioUnit u, bool inScope, ref bool found)
    {
        var units = new List<ScenarioUnit>(u.Units.Count);
        foreach (var sub in u.Units)
            units.Add(Swap(sub, inScope || sub.Name == Unit, ref found));
        var ships = new List<ScenarioShips>(u.Ships.Count);
        foreach (var e in u.Ships)
        {
            if (e.Design != From || !inScope)
            {
                ships.Add(e);
                continue;
            }
            int k = Ship == null ? -1 : Array.IndexOf(e.Names, Ship);
            if (Ship == null)
            {
                ships.Add(e.Slice(0, e.Count, To));
                found = true;
            }
            else if (k >= 0)
            {
                // only her: the entry split round her, the order kept (the stations go by it)
                if (k > 0)
                    ships.Add(e.Slice(0, k, e.Design));
                ships.Add(e.Slice(k, 1, To));
                if (k + 1 < e.Count)
                    ships.Add(e.Slice(k + 1, e.Count - k - 1, e.Design));
                found = true;
            }
            else
                ships.Add(e);
        }
        return u.With(units, ships);
    }

    public override string Describe(Scenario written) => Describe(written, d => d);

    /// <summary>The change in a line with the designs by their names (<paramref name="nameOf"/> a design file's: the
    /// launch screen's titles).</summary>
    public string Describe(Scenario written, Func<string, string> nameOf) => $"{Ship ?? Unit ?? "Every ship"}: {nameOf(From)}, now {nameOf(To)}";
}

public enum SideKind
{
    Crews,
    Readiness,
    DamageControl,
    FireDiscipline,
    Fuel,
}

/// <summary>A side's setting: its gun crews, its readiness, its damage control, its fire discipline (names of the
/// settings'), or its fuel (a share of a full load), replacing the written one for that side.</summary>
public sealed record SideSetting(SideKind Kind, int Side, string Value) : SetupChange
{
    public override string Key => $"{Kind}|{Side}";
    internal override int Order => 6;

    internal static string FileKey(SideKind k) => k switch
    {
        SideKind.Crews => "crews",
        SideKind.Readiness => "readiness",
        SideKind.DamageControl => "damage_control",
        SideKind.FireDiscipline => "fire_discipline",
        _ => "fuel",
    };

    static string Label(SideKind k) => k switch
    {
        SideKind.Crews => "gun crews",
        SideKind.Readiness => "readiness",
        SideKind.DamageControl => "damage control",
        SideKind.FireDiscipline => "fire discipline",
        _ => "fuel",
    };

    internal override Scenario Apply(Scenario s)
    {
        if (Side < 0 || Side >= s.Sides.Length)
            throw new ScenarioException($"{FileKey(Kind)}: no side {Side}");
        return Kind switch
        {
            SideKind.Crews => s with
            {
                Crews = Over(s.Crews, Experience.TryParse(Value, out double x) ? x
                    : throw new ScenarioException($"crews: \"{Value}\": green, average, trained or elite, or 0 to 3")),
            },
            SideKind.Readiness => s with { Readiness = Over(s.Readiness, Value) },
            SideKind.DamageControl => s with { DamageControl = Over(s.DamageControl, Value) },
            SideKind.FireDiscipline => s with { FireDisciplines = Over(s.FireDisciplines, Value) },
            _ => s with { Fuel = Over(s.Fuel, double.Parse(Value, CultureInfo.InvariantCulture)) },
        };
    }

    /// <summary>A side's entry replaced (or added), the others kept.</summary>
    List<(int Side, T)> Over<T>(List<(int Side, T)> list, T value) => [.. list.Where(x => x.Side != Side), (Side, value)];

    public override string Describe(Scenario written)
    {
        string was = Kind switch
        {
            SideKind.Crews => written.Crews.Where(x => x.Side == Side).Select(x => Experience.Name(x.Experience)).FirstOrDefault() ?? "trained",
            SideKind.Readiness => written.Readiness.Where(x => x.Side == Side).Select(x => x.Name).FirstOrDefault() ?? "the default",
            SideKind.DamageControl => written.DamageControl.Where(x => x.Side == Side).Select(x => x.Name).FirstOrDefault() ?? "the default",
            SideKind.FireDiscipline => written.FireDisciplines.Where(x => x.Side == Side).Select(x => x.Name).FirstOrDefault() ?? "the default",
            _ => written.Fuel.Where(x => x.Side == Side).Select(x => x.Share.ToString("0.###", CultureInfo.InvariantCulture)).FirstOrDefault() ?? "full",
        };
        return $"{written.Sides[Side]}'s {Label(Kind)}: {Value} (written: {was})";
    }
}

/// <summary>A side's doctrine field (docs/map-ui/launch.md L4): its aggression, or one of its rules
/// (<see cref="DoctrineRules"/>), over its preset's and the file's.</summary>
public sealed record SetDoctrine(int Side, DoctrineField Field, double Value) : SetupChange
{
    public override string Key => $"doctrine|{Side}|{Field}";
    internal override int Order => 6;

    internal override Scenario Apply(Scenario s)
    {
        if (Side < 0 || Side >= s.Sides.Length)
            throw new ScenarioException($"doctrine: no side {Side}");
        if (Field == DoctrineField.Temper)
            throw new ScenarioException("doctrine: a Temper is an admiral's or a captain's (\"tempers\")");
        var given = s.Doctrine.FirstOrDefault(d => d.Side == Side);
        var values = (given.Values ?? []).Where(v => v.Field != Field).Append((Field, Value)).ToList();
        return s with { Doctrine = [.. s.Doctrine.Where(d => d.Side != Side), (Side, given.Preset ?? DoctrinePresets.DefaultName, values)] };
    }

    public override string Describe(Scenario written) => Describe(written, ScenarioLoader.PresetsFor(written));

    /// <summary>The change in a line against the side's doctrine as written (its preset's values among it).</summary>
    public string Describe(Scenario written, DoctrinePresets presets)
    {
        double was = ScenarioLoader.SideDoctrine(written, Side, Field, presets);
        if (Field == DoctrineField.Aggression)
            return $"{written.Sides[Side]}'s aggression {DoctrineRules.Signed(Value)} (written: {DoctrineRules.Signed(was)})";
        if (DoctrineRules.Of(Field) is { } rule)
            return $"{written.Sides[Side]}: {rule.Label.ToLowerInvariant()}: {rule.Word(Value)} (written: {rule.Word(was)})";
        return $"{written.Sides[Side]}: {Field} {Value.ToString("0.###", CultureInfo.InvariantCulture)} (written: {was.ToString("0.###", CultureInfo.InvariantCulture)})";
    }
}

/// <summary>Doctrine's rules as the launch screen sets them (docs/map-ui/launch.md L4), each a field and its two values
/// in words.</summary>
public static class DoctrineRules
{
    public sealed record Rule(DoctrineField Field, string Label, (double Value, string Word)[] Options)
    {
        public string Word(double v)
        {
            foreach (var (value, word) in Options)
                if (Math.Abs(value - v) < 1e-9)
                    return word;
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    public static readonly Rule[] All =
    [
        new(DoctrineField.Engage, "On meeting the enemy", [((double)Engage.Fight, "fight"), ((double)Engage.CarryOn, "carry on")]),
        new(DoctrineField.Deploy, "Deploy into one line on his main body", [(1, "yes"), (0, "no")]),
        new(DoctrineField.Torpedoes, "Torpedoes", [((double)TorpedoUse.AtWill, "at will"), ((double)TorpedoUse.OnOrder, "on order")]),
        new(DoctrineField.Evasion, "Under fire", [((double)Evasion.Zigzag, "zigzag"), ((double)Evasion.Steady, "steady")]),
        new(DoctrineField.TorpedoEvasion, "Torpedo tracks", [((double)TorpedoEvasion.Comb, "comb"), ((double)TorpedoEvasion.None, "hold on")]),
        new(DoctrineField.NightAction, "Heavy ships at night", [((double)NightAction.Accept, "accept"), ((double)NightAction.Avoid, "avoid")]),
        new(DoctrineField.NightAttack, "Torpedo craft attack by night", [(1, "yes"), (0, "no")]),
        new(DoctrineField.Dispose, "Admirals place scouts and screens", [(1, "by roles"), (0, "all in line")]),
        new(DoctrineField.Shells, "Shells", [((double)ShellUse.ByTarget, "by target"), ((double)ShellUse.MagazineOrder, "by the magazine order")]),
        new(DoctrineField.SecondaryHitFloor, "Secondaries at will", [(0.01, "1 hit in 100 or better"), (0, "anything in reach")]),
        new(DoctrineField.ManCasemates, "Casemate guns closed up", [((double)ManGuns.Always, "always"), ((double)ManGuns.InRange, "in range"),
            ((double)ManGuns.TorpedoCraft, "for torpedo craft")]),
        new(DoctrineField.ManOpenGuns, "Open guns closed up", [((double)ManGuns.Always, "always"), ((double)ManGuns.InRange, "in range"),
            ((double)ManGuns.TorpedoCraft, "for torpedo craft")]),
        new(DoctrineField.ReliefOrder, "Reliefs", [((double)ReliefOrder.HandlingRoomProtected, "handling rooms kept"),
            ((double)ReliefOrder.ReserveInSupply, "from the supply first")]),
        new(DoctrineField.Wrecks, "Wrecks seen done for", [((double)WreckFire.Cease, "cease fire"), ((double)WreckFire.Finish, "finish them")]),
        new(DoctrineField.Unknowns, "Ships not made out", [((double)Unknowns.Challenge, "challenge"), ((double)Unknowns.Hold, "hold fire"),
            ((double)Unknowns.Fire, "fire")]),
        new(DoctrineField.Searchlights, "Searchlights at night", [((double)SearchlightUse.ToFire, "to fire"), ((double)SearchlightUse.Search, "to search and fire"),
            ((double)SearchlightUse.WhenFiredOn, "once fired on"), ((double)SearchlightUse.Never, "never")]),
        new(DoctrineField.StarShell, "Star shell", [((double)StarShellUse.ToFire, "to fire"), ((double)StarShellUse.ToFind, "to find and fire"),
            ((double)StarShellUse.Never, "never")]),
        new(DoctrineField.WhenLit, "When lit at night", [((double)WhenLit.TurnAway, "lights out, turn away"), ((double)WhenLit.FightOn, "fight on")]),
        new(DoctrineField.NightSpacingShare, "Lines at night", [(0.5, "close up to half"), (1, "the day's spacing")]),
    ];

    public static Rule? Of(DoctrineField f) => Array.Find(All, r => r.Field == f);

    /// <summary>An aggression or a temper as the screen writes it: "+0.50", "−0.40", "0".</summary>
    public static string Signed(double v) => Math.Abs(v) < 0.005 ? "0" : (v > 0 ? "+" : "−") + Math.Abs(v).ToString("0.00", CultureInfo.InvariantCulture);
}

/// <summary>A temper (docs/map-ui/launch.md "Tempers"): a unit's admiral's, or a ship's captain's by her name.</summary>
public sealed record SetTemper(string Name, double Value) : SetupChange
{
    public override string Key => $"temper|{Name}";
    internal override int Order => 4;

    internal override Scenario Apply(Scenario s)
    {
        var units = Tree.Rebuild(s.Units, Name, u => u.Tempered(Value), out bool found);
        if (found)
            return s with { Units = units };
        // a ship: her entry in the squadron she's in
        foreach (var top in s.Units)
            if (Holder(top) is { } sq)
                return s with { Units = Tree.Rebuild(s.Units, sq.Name, u => u.With(u.Units, u.Ships.Select(e => Her(e) is int k ? e.Tempered(k, Value) : e).ToList()), out _) };
        throw new ScenarioException($"tempers: no unit or ship \"{Name}\"");
    }

    int? Her(ScenarioShips e) => Array.IndexOf(e.Names, Name) is var k and >= 0 && k < e.Count ? k : null;

    ScenarioUnit? Holder(ScenarioUnit u) => u.Ships.Any(e => Her(e) != null) ? u : u.Units.Select(Holder).FirstOrDefault(x => x != null);

    /// <summary>A ship's captain's temper as the units have it, by her name; null if there's no such ship.</summary>
    public static double? CaptainOf(IEnumerable<ScenarioUnit> units, string ship)
    {
        foreach (var u in units)
        {
            foreach (var e in u.Ships)
                if (Array.IndexOf(e.Names, ship) is var k and >= 0 && k < e.Count)
                    return e.TemperOf(k);
            if (CaptainOf(u.Units, ship) is { } t)
                return t;
        }
        return null;
    }

    public override string Describe(Scenario written)
    {
        if (Tree.Find(written.Units, Name) is { } u)
            return $"{(u.Admiral.Length > 0 ? AdmiralName(u.Admiral) : Name)}'s temper {DoctrineRules.Signed(Value)} (written: {DoctrineRules.Signed(u.Temper ?? 0)})";
        return $"{Name}'s captain: temper {DoctrineRules.Signed(Value)} (written: {DoctrineRules.Signed(CaptainOf(written.Units, Name) ?? 0)})";
    }

    /// <summary>An admiral's name without what the scenario adds in brackets ("Rozhestvensky (in Suvorov)").</summary>
    public static string AdmiralName(string admiral) => admiral.IndexOf(" (", StringComparison.Ordinal) is var k and > 0 ? admiral[..k] : admiral;
}

/// <summary>The world's seed: the same fight again for the same seed; null for a new one each time it's begun (drawn by
/// the launch screen: applied, the written one stands).</summary>
public sealed record SetSeed(ulong? Seed) : SetupChange
{
    public override string Key => "seed";
    internal override int Order => 7;

    internal override Scenario Apply(Scenario s) => Seed is { } seed ? s with { Seed = seed } : s;

    public override string Describe(Scenario written) => Seed is { } seed ? $"Seed {seed} (written: {written.Seed})" : $"Seed: a new one each time (written: {written.Seed})";
}

/// <summary>The weather (docs/map-ui/launch.md L5): its visibility and wind, all of it in place of the written.</summary>
public sealed record SetWeather(WeatherHere Weather) : SetupChange
{
    public override string Key => "weather";
    internal override int Order => 7;

    internal override Scenario Apply(Scenario s) =>
        Weather.VisibilityM > 0 && Weather.WindMs >= 0 ? s with { Weather = Weather } : throw new ScenarioException("weather: a visibility more than 0, a wind 0 or more");

    public override string Describe(Scenario written) =>
        $"Weather: {Words(Weather)} (written: {(written.Weather is { } w ? Words(w) : "the default")})";

    /// <summary>The weather in a few words: "8 km, wind SW 12 kn, cloud 6/10".</summary>
    public static string Words(WeatherHere w) =>
        $"{(w.VisibilityM / 1000).ToString("0.#", CultureInfo.InvariantCulture)} km, wind {Compass(w.WindFromDeg)} {(w.WindMs / 0.514444).ToString("0", CultureInfo.InvariantCulture)} kn" +
        (w.Cloud > 0 ? $", cloud {Math.Round(w.Cloud * 10).ToString("0", CultureInfo.InvariantCulture)}/10" : "");

    /// <summary>A direction by the sixteen points.</summary>
    public static string Compass(double deg) => Points[(int)Math.Round(((deg % 360) + 360) % 360 / 22.5) % 16];

    public static readonly string[] Points = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];
}

/// <summary>When it ends at the latest (docs/map-ui/launch.md L5), in place of the written <c>"until"</c>.</summary>
public sealed record SetEnd(WorldTime Until) : SetupChange
{
    public override string Key => "until";
    internal override int Order => 7;

    internal override Scenario Apply(Scenario s) =>
        Until.Ms > s.Start.Ms ? s with { Until = Until } : throw new ScenarioException("until: after the start");

    public override string Describe(Scenario written) =>
        $"Ends {Utc(Until)} (written: {(written.Until is { } u ? Utc(u) : "the default")})";

    static string Utc(WorldTime t) => t.ToUtc().ToString("d MMM HH:mm", CultureInfo.InvariantCulture) + " GMT";
}

/// <summary>A unit added (docs/map-ui/launch.md L3b): placed on its own, a top unit with its place, course and speed, or
/// joining a force (<see cref="Joins"/>, its name) as its last unit, on its station there as the force's own are. Its
/// name is new to the scenario. Equal to another of the same unit as written out (a file read back is the same
/// change).</summary>
public sealed record AddUnit(ScenarioUnit Unit, string? Joins) : SetupChange
{
    public override string Key => $"add|{Unit.Name}";
    internal override int Order => 1;

    internal override Scenario Apply(Scenario s)
    {
        if (Unit.Side < 0 || Unit.Side >= s.Sides.Length)
            throw new ScenarioException($"add: {Unit.Name}: no side {Unit.Side}");
        if (Tree.Find(s.Units, Unit.Name) != null)
            throw new ScenarioException($"add: a unit \"{Unit.Name}\" is in the scenario already");
        if (Joins == null)
            return Unit.At != null ? s with { Units = [.. s.Units, Unit] }
                : throw new ScenarioException($"add: {Unit.Name}: on its own, it needs \"at\", \"course\" and \"kn\"");
        var units = Tree.Rebuild(s.Units, Joins, f =>
            f.Side != Unit.Side ? throw new ScenarioException($"add: {Unit.Name} joins {Joins}, of another side")
            : f.IsForce ? f.With([.. f.Units, Unit], f.Ships)
            : f.With([Own(f), Unit], []), out bool found);
        return found ? s with { Units = units } : throw new ScenarioException($"add: {Unit.Name} joins \"{Joins}\": no such unit");
    }

    /// <summary>A squadron's own ships as the first unit of the force it becomes when a unit joins it: in its formation and
    /// spacing (the force keeps them too, so in a column the new unit follows on at the same distance), called by its
    /// name and "I".</summary>
    static ScenarioUnit Own(ScenarioUnit sq)
    {
        var u = new ScenarioUnit { Name = sq.Name + " I", Side = sq.Side, Formation = sq.Formation, SpacingM = sq.SpacingM, Loadout = sq.Loadout };
        u.Ships.AddRange(sq.Ships);
        return u;
    }

    public override string Describe(Scenario written) => Describe(written, d => d);

    /// <summary>The change in a line with the designs by their names (the launch screen's titles).</summary>
    public string Describe(Scenario written, Func<string, string> nameOf)
    {
        var ships = new List<string>();
        void Walk(ScenarioUnit u)
        {
            foreach (var e in u.Ships)
                ships.Add($"{e.Count} × {nameOf(e.Design)}");
            foreach (var x in u.Units)
                Walk(x);
        }
        Walk(Unit);
        string side = Unit.Side < written.Sides.Length ? written.Sides[Unit.Side] + ": " : "";
        return $"{side}added {Unit.Name} ({string.Join(", ", ships)}){(Joins != null ? ", joining " + Joins : "")}";
    }

    public bool Equals(AddUnit? o) => o is not null && Joins == o.Joins && Json() == o.Json();

    public override int GetHashCode() => HashCode.Combine(Unit.Name, Joins);

    string Json()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
            Write(w);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>The unit as the file's "add" has it.</summary>
    internal void Write(Utf8JsonWriter w) => Write(w, Unit, Joins == null, Joins);

    static void Write(Utf8JsonWriter w, ScenarioUnit u, bool top, string? joins)
    {
        w.WriteStartObject();
        w.WriteString("name", u.Name);
        if (joins != null)
            w.WriteString("joins", joins);
        if (top || joins != null)
            w.WriteNumber("side", u.Side);
        if (top && u.At is { } at)
        {
            w.WriteStartArray("at");
            w.WriteNumberValue(at.Lat);
            w.WriteNumberValue(at.Lon);
            w.WriteEndArray();
            w.WriteNumber("course", u.CourseDeg);
            w.WriteNumber("kn", u.Kn);
        }
        if (u.Temper != null)
        {
            w.WriteStartObject("admiral");
            if (u.Admiral.Length > 0)
                w.WriteString("name", u.Admiral);
            w.WriteNumber("temper", u.Temper.Value);
            w.WriteEndObject();
        }
        else if (u.Admiral.Length > 0)
            w.WriteString("admiral", u.Admiral);
        w.WriteString("formation", Scenario.FormationName(u.Formation));
        w.WriteNumber("spacing", u.SpacingM);
        if (u.IsForce)
        {
            w.WriteStartArray("units");
            foreach (var x in u.Units)
                Write(w, x, false, null);
            w.WriteEndArray();
        }
        else
        {
            w.WriteStartArray("ships");
            foreach (var e in u.Ships)
            {
                w.WriteStartObject();
                w.WriteString("design", e.Design);
                if (e.Names.Length > 0)
                {
                    w.WriteStartArray("names");
                    foreach (var n in e.Names)
                        w.WriteStringValue(n);
                    w.WriteEndArray();
                }
                w.WriteNumber("count", e.Count);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        w.WriteEndObject();
    }

    static readonly string[] UnitKeys = ["name", "side", "at", "course", "kn", "admiral", "formation", "spacing", "units", "ships"], ShipKeys = ["design", "names", "name", "count"];

    /// <summary>A file's added unit: a unit as a scenario's "units" have it, of those keys only, and "joins".</summary>
    internal static AddUnit Parse(JsonElement a, string path, Scenario written, IEnumerable<AddUnit> earlier)
    {
        if (a.ValueKind != JsonValueKind.Object)
            throw new ScenarioException($"{path}: a unit, {{ \"name\": ..., \"side\": 0, \"at\": [lat, lon], \"course\": ..., \"kn\": ..., \"ships\": [...] }}");
        Check(a, path, top: true);
        string? joins = a.TryGetProperty("joins", out var j) && j.ValueKind == JsonValueKind.String ? j.GetString() : null;
        int side = joins != null ? (Tree.Find(written.Units, joins) ?? earlier.Select(x => Tree.Find([x.Unit], joins)).FirstOrDefault(f => f != null))?.Side ?? 0 : 0;
        var unit = Scenario.ParseUnit(a, path, top: joins == null, side, written.Sides.Length);
        return unit.Name.Length > 0 ? new AddUnit(unit, joins) : throw new ScenarioException($"{path}: an added unit needs a \"name\"");
    }

    static void Check(JsonElement e, string path, bool top)
    {
        foreach (var p in e.EnumerateObject())
            if (Array.IndexOf(UnitKeys, p.Name) < 0 && !(top && p.Name == "joins"))
                throw new ScenarioException($"{path}: an added unit takes {string.Join(", ", UnitKeys)}{(top ? " and joins" : "")}; not \"{p.Name}\" (more is a scenario's own)");
        if (e.TryGetProperty("units", out var us) && us.ValueKind == JsonValueKind.Array)
            foreach (var u in us.EnumerateArray())
                if (u.ValueKind == JsonValueKind.Object)
                    Check(u, path, top: false);
        if (e.TryGetProperty("ships", out var ss) && ss.ValueKind == JsonValueKind.Array)
            foreach (var sh in ss.EnumerateArray())
                if (sh.ValueKind == JsonValueKind.Object)
                    foreach (var p in sh.EnumerateObject())
                        if (Array.IndexOf(ShipKeys, p.Name) < 0)
                            throw new ScenarioException($"{path}: an added unit's ships take {string.Join(", ", ShipKeys)}; not \"{p.Name}\"");
    }
}

/// <summary>A squadron's ships of a design, <see cref="By"/> more (unnamed, at the end of its last entry of that design)
/// or fewer (the last first, struck: the launch screen shows them so, none is placed).</summary>
public sealed record ChangeCount(string Unit, string Design, int By) : SetupChange
{
    public override string Key => $"count|{Unit}|{Design}";
    internal override int Order => 3;

    internal override Scenario Apply(Scenario s)
    {
        var units = Tree.Rebuild(s.Units, Unit, u => u.IsForce ? Inner(u) : Resized(u, Design, By, strike: true), out bool found);
        return found ? s with { Units = units } : throw new ScenarioException($"counts: no unit \"{Unit}\"");
    }

    /// <summary>A force named (a squadron a unit joined since): its first squadron of the design.</summary>
    ScenarioUnit Inner(ScenarioUnit force)
    {
        ScenarioUnit? sq = null;
        void Find(ScenarioUnit u)
        {
            if (sq == null && u.Ships.Any(e => e.Design == Design))
                sq = u;
            foreach (var x in u.Units)
                Find(x);
        }
        Find(force);
        return sq == null ? throw new ScenarioException($"counts: no {Design} in {Unit}") : Tree.Rebuild(force, sq.Name, u => Resized(u, Design, By, strike: true));
    }

    /// <summary>A squadron with <paramref name="by"/> more of a design, or fewer: struck, or gone (an added unit's own).</summary>
    internal static ScenarioUnit Resized(ScenarioUnit sq, string design, int by, bool strike)
    {
        var ships = sq.Ships.ToList();
        int last = ships.FindLastIndex(e => e.Design == design);
        if (last < 0)
            throw new ScenarioException($"counts: no {design} in {sq.Name}");
        if (by > 0)
            ships[last] = ships[last].Grown(by);
        for (int i = last, left = -by; left > 0; i--)
        {
            if (i < 0)
                throw new ScenarioException($"counts: fewer than {-by} {design} in {sq.Name} to take off");
            if (ships[i].Design != design)
                continue;
            int k = Math.Min(left, ships[i].Count);
            ships[i] = ships[i].Shrunk(k, strike);
            left -= k;
        }
        if (!strike)
            ships.RemoveAll(e => e.Count == 0);
        return sq.With(sq.Units, ships);
    }

    public override string Describe(Scenario written) => Describe(written, d => d);

    public string Describe(Scenario written, Func<string, string> nameOf) =>
        By > 0 ? $"{Unit}: {By} more {nameOf(Design)}" : $"{Unit}: {-By} {nameOf(Design)} taken off";
}

/// <summary>A unit called by another name (any level), and the scenario's tasks that name it with it.</summary>
public sealed record RenameUnit(string From, string To) : SetupChange
{
    public override string Key => $"rename|{From}";
    internal override int Order => 5;

    internal override Scenario Apply(Scenario s)
    {
        if (Tree.Find(s.Units, To) != null)
            throw new ScenarioException($"rename: a unit \"{To}\" is in the scenario already");
        var units = Tree.Rebuild(s.Units, From, u => u.Renamed(To), out bool found);
        if (!found)
            throw new ScenarioException($"rename: no unit \"{From}\"");
        // the tasks that name it (the top units' "of")
        for (int i = 0; i < units.Count; i++)
            if (units[i].Tasks.Any(t => t.Of == From))
                units[i] = units[i].Tasked([.. units[i].Tasks.Select(t => t.Of == From ? t.OfUnit(To) : t)]);
        return s with { Units = units };
    }

    public override string Describe(Scenario written) => $"{From} renamed {To}";
}

/// <summary>A unit taken off (any level): every ship under it struck, the unit kept to be shown so.</summary>
public sealed record RemoveUnit(string Name) : SetupChange
{
    public override string Key => $"remove|{Name}";
    internal override int Order => 4;

    internal override Scenario Apply(Scenario s)
    {
        var units = Tree.Rebuild(s.Units, Name, u => Struck(u, top: true), out bool found);
        return found ? s with { Units = units } : throw new ScenarioException($"remove: no unit \"{Name}\"");
    }

    static ScenarioUnit Struck(ScenarioUnit u, bool top) =>
        u.With(u.Units.Select(x => Struck(x, top: false)).ToList(), u.Ships.Select(e => e.Shrunk(e.Count, strike: true)).ToList(), removed: top ? true : null);

    public override string Describe(Scenario written) =>
        Tree.Find(written.Units, Name) is { } u ? $"Removed {Name} ({u.ShipCount} ship{(u.ShipCount == 1 ? "" : "s")})" : $"Removed {Name}";
}

/// <summary>A scenario's tree of units, by name: the first of a name, at any level.</summary>
static class Tree
{
    internal static bool Holds(ScenarioUnit u, string name) => u.Name == name || u.Units.Any(x => Holds(x, name));

    internal static ScenarioUnit? Find(IEnumerable<ScenarioUnit> units, string name)
    {
        foreach (var u in units)
            if (u.Name == name)
                return u;
            else if (Find(u.Units, name) is { } f)
                return f;
        return null;
    }

    /// <summary>A unit's tree with the unit of that name made over by <paramref name="f"/>, the units above it copied.</summary>
    internal static ScenarioUnit Rebuild(ScenarioUnit u, string name, Func<ScenarioUnit, ScenarioUnit> f) => Rebuild([u], name, f, out _)[0];

    /// <summary>The units with the first of that name made over by <paramref name="f"/>, the units above it copied, the
    /// rest shared.</summary>
    internal static List<ScenarioUnit> Rebuild(IReadOnlyList<ScenarioUnit> units, string name, Func<ScenarioUnit, ScenarioUnit> f, out bool found)
    {
        bool hit = false;
        ScenarioUnit Walk(ScenarioUnit u)
        {
            if (hit)
                return u;
            if (u.Name == name)
            {
                hit = true;
                return f(u);
            }
            var subs = u.Units.Select(Walk).ToList();
            for (int i = 0; i < subs.Count; i++)
                if (!ReferenceEquals(subs[i], u.Units[i]))
                    return u.With(subs, u.Ships);
            return u;
        }
        var list = units.Select(Walk).ToList();
        found = hit;
        return list;
    }
}
