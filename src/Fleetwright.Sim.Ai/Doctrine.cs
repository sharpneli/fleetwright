using System.Text.Json;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>A field of doctrine (docs/battle/command.md "Doctrine: the standing rules"). Every value is a number; the
/// enums below are numbers too. Fields come with the step that first reads them.</summary>
public enum DoctrineField : byte
{
    /// <summary>What a unit does when it first meets an enemy in sight in an encounter (<see cref="Engage"/>).</summary>
    Engage,

    /// <summary>The range Fight keeps, a share of the main battery's reach.</summary>
    RangeShare,

    /// <summary>The ratio of the enemy's heavy guns to the unit's own past which Fight turns away.</summary>
    OutgunnedRatio,

    /// <summary>A ship falls out of the line under this share of its designed speed...</summary>
    FallOutSpeedShare,

    /// <summary>...or of its main guns working...</summary>
    FallOutGunsShare,

    /// <summary>...or listing past this many degrees...</summary>
    FallOutListDeg,

    /// <summary>...or with this share of its reserve buoyancy flooded.</summary>
    FallOutWaterShare,

    /// <summary>Whether captains fire torpedoes at will (<see cref="TorpedoUse"/>).</summary>
    Torpedoes,

    /// <summary>How a ship under fire spoils the enemy's aim (<see cref="Evasion"/>; research/simulation/evasion-research.md §2).</summary>
    Evasion,

    /// <summary>A zigzag's swing either side of the course she wants, degrees...</summary>
    EvadeDeg,

    /// <summary>...and how long each leg, seconds.</summary>
    EvadeLegS,

    /// <summary>A running ship evades fire from beyond this range only, m: closer, the dodge buys nothing (the research's
    /// "steady inside ~10 kyd").</summary>
    EvadeBeyondM,
}

/// <summary>Doctrine's Engage: carry on with the orders given (firing at will), or Fight (and turn away when outgunned).</summary>
public enum Engage : byte
{
    CarryOn,
    Fight,
}

/// <summary>Doctrine's Evasion: hold the course wanted, or zigzag about it. Weaving and chasing salvos come later.</summary>
public enum Evasion : byte
{
    Steady,
    Zigzag,
}

/// <summary>Doctrine's Torpedoes: at will, or only on order.</summary>
public enum TorpedoUse : byte
{
    AtWill,
    OnOrder,
}

/// <summary>A side's or a unit's standing rules (docs/battle/command.md "Doctrine"), read down the chain: a ship takes
/// its own value, else its squadron's, else each force's above, else its side's, else the neutral default
/// (<see cref="Default"/>: carry on, never fall out, the Fight numbers 4.2c had). A scenario gives each side a preset
/// (<see cref="DoctrinePresets"/>: named values, the "default" one with Engage at Fight) and values over it per side and
/// per unit; a command sets one value, so it's logged and replays. AI state: only captains and admirals read it.</summary>
public sealed class Doctrine(GroupSystem groups)
{
    public const int FieldCount = (int)DoctrineField.EvadeBeyondM + 1;

    /// <summary>Every field when nothing sets it: a world told nothing (a dev's, a test's) behaves as before doctrine.</summary>
    public static double Default(DoctrineField f) => f switch
    {
        DoctrineField.Engage => (double)Ai.Engage.CarryOn,
        DoctrineField.RangeShare => 0.6,
        DoctrineField.OutgunnedRatio => 2,
        DoctrineField.FallOutSpeedShare or DoctrineField.FallOutGunsShare => 0,   // never
        DoctrineField.FallOutListDeg => 90,
        DoctrineField.FallOutWaterShare => 2,
        DoctrineField.Torpedoes => (double)TorpedoUse.AtWill,
        DoctrineField.Evasion => (double)Ai.Evasion.Steady,
        DoctrineField.EvadeDeg => 30,
        DoctrineField.EvadeLegS => 75,
        DoctrineField.EvadeBeyondM => 10_000,
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    // by unit (Unit.Id: a ship's entity, a group's id negated) and by side: a value per field, NaN where not set
    readonly SortedDictionary<int, double[]> units = [];
    readonly double[]?[] sides = new double[256][];

    /// <summary>Sets a unit's value (NaN: back to what it inherits).</summary>
    public void Set(Unit u, DoctrineField f, double value)
    {
        if (!units.TryGetValue(u.Id, out var r))
            units[u.Id] = r = New();
        r[(int)f] = value;
    }

    /// <summary>Sets a side's value (NaN: back to the default).</summary>
    public void SetSide(int side, DoctrineField f, double value) => (sides[side & 255] ??= New())[(int)f] = value;

    static double[] New()
    {
        var r = new double[FieldCount];
        Array.Fill(r, double.NaN);
        return r;
    }

    /// <summary>A ship's value: its own, its groups' up the tree, its side's, the default.</summary>
    public double Of(int entity, byte side, DoctrineField f)
    {
        if (units.TryGetValue(entity, out var r) && !double.IsNaN(r[(int)f]))
            return r[(int)f];
        return OfGroup(groups.Of(entity), side, f);
    }

    /// <summary>A group's value: its own, its forces' up the tree, its side's, the default.</summary>
    public double OfGroup(GroupSystem.Group? g, byte side, DoctrineField f)
    {
        for (; g != null; g = g.Parent != 0 ? groups.Get(g.Parent) : null)
            if (units.TryGetValue(-g.GroupId, out var r) && !double.IsNaN(r[(int)f]))
                return r[(int)f];
        return sides[side] is { } s && !double.IsNaN(s[(int)f]) ? s[(int)f] : Default(f);
    }

    /// <summary>Hashed only once anything is set, so a world told nothing hashes as before doctrine.</summary>
    public void Hash(ref StateHash h)
    {
        if (units.Count > 0)
            h.Add(units.Count);
        foreach (var (id, r) in units)
        {
            h.Add(id);
            foreach (double v in r)
                h.Add(v);
        }
        for (int side = 0; side < sides.Length; side++)
            if (sides[side] is { } s)
            {
                h.Add(side);
                foreach (double v in s)
                    h.Add(v);
            }
    }
}

/// <summary>Doctrine's presets (docs/battle/command.md decision 7): named sets of values, as data
/// (<c>Content/Sim/doctrine.json</c>), never code. A field a preset doesn't give is the neutral default. Enum fields
/// take their names ("fight") or numbers.</summary>
public sealed class DoctrinePresets
{
    readonly Dictionary<string, (DoctrineField Field, double Value)[]> presets = new(StringComparer.Ordinal);

    /// <summary>The one every scenario side takes unless it names another.</summary>
    public const string DefaultName = "default";

    /// <summary>Only "default", with Engage at Fight and the rest neutral: for a scenario with no
    /// <c>Content/Sim/doctrine.json</c> beside its folder (one parsed from a string, in tests).</summary>
    public static DoctrinePresets Builtin { get; } = Parse("""{ "default": { "Engage": "fight" } }""");

    public IEnumerable<string> Names => presets.Keys;

    public (DoctrineField Field, double Value)[] this[string name] =>
        presets.TryGetValue(name, out var p) ? p : throw new ArgumentException($"no doctrine preset \"{name}\" (have {string.Join(", ", presets.Keys)})");

    public bool Has(string name) => presets.ContainsKey(name);

    public static DoctrinePresets Load(string path) => Parse(File.ReadAllText(path));

    public static DoctrinePresets Parse(string json)
    {
        var d = new DoctrinePresets();
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (var p in doc.RootElement.EnumerateObject())
            d.presets[p.Name] = [.. p.Value.EnumerateObject().Select(f => Value(f.Name, f.Value))];
        if (!d.presets.ContainsKey(DefaultName))
            throw new ArgumentException($"doctrine presets: no \"{DefaultName}\"");
        return d;
    }

    /// <summary>A field's value as written: a number, or an enum field's name ("fight", "on order").</summary>
    public static (DoctrineField, double) Value(string field, JsonElement v)
    {
        if (!Enum.TryParse<DoctrineField>(field, out var f))
            throw new ArgumentException($"doctrine: no field \"{field}\" (have {string.Join(", ", Enum.GetNames<DoctrineField>())})");
        if (v.ValueKind == JsonValueKind.Number)
            return (f, v.GetDouble());
        string name = (v.GetString() ?? "").Replace(" ", "");
        return f switch
        {
            DoctrineField.Engage when Enum.TryParse<Engage>(name, ignoreCase: true, out var e) => (f, (double)e),
            DoctrineField.Torpedoes when Enum.TryParse<TorpedoUse>(name, ignoreCase: true, out var t) => (f, (double)t),
            DoctrineField.Evasion when Enum.TryParse<Evasion>(name, ignoreCase: true, out var ev) => (f, (double)ev),
            _ => throw new ArgumentException($"doctrine: \"{v}\" isn't a value of {field}"),
        };
    }
}
