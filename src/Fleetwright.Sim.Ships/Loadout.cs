using System.Globalization;
using System.Text.Json;
using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>The shells a ship's batteries carry (docs/battle/shells-armour-damage.md S5, decision 6: the loadout is the
/// admiral's, the armour the designer's): each battery's loads in the magazine order, the shell types they are, and each
/// mount's rounds of each load at the start. A class carries its design's own (<see cref="ShipClass.Loadout"/>); a ship
/// may carry another of the same class (<see cref="ShipClass.Load"/>), set outside the design. Immutable, shared by
/// every ship that carries it; what a ship has fired is hers (CombatStore).
/// <para>Loads are a pool: a battery's are <see cref="Shells"/>'s slice of it, and a ship's rounds left by load are
/// one per pool entry (a mount's from <see cref="MountLoadStart"/>).</para></summary>
public sealed class Loadout
{
    internal ShellTypeRow[] types = [];
    internal string[] typeIds = [];
    internal ShellLoad[] loads = [];
    internal Slice[] shells = [];
    internal int[] fires = [], mountLoadStart = [], loadRounds = [];

    Loadout(ShipClass c, string name, ContentHash hash) => (Class, Name, Hash) = (c, name, hash);

    /// <summary>The class it's of: its batteries and mounts.</summary>
    public ShipClass Class { get; }

    /// <summary>What it's called ("" for the design's own).</summary>
    public string Name { get; }

    /// <summary>The design with its batteries' shells as loaded (the class's design hash for its own): equal means the
    /// same loadout.</summary>
    public ContentHash Hash { get; }

    /// <summary>Whether it's the design's own.</summary>
    public bool IsDefault => ReferenceEquals(this, Class.Loadout);

    /// <summary>The shells it carries, each kind once.</summary>
    public ReadOnlySpan<ShellTypeRow> ShellTypes => types;

    /// <summary>A shell type's id, shared across classes: the gun and the block ("305/45 RN 1906 APC (powder)"), so
    /// equal ids are equal rows but for the muzzle velocity (it follows the outfit's heaviest shell).</summary>
    public string ShellTypeId(int i) => typeIds[i];

    /// <summary>A battery's loads (the class's Batteries), shares adding up to 1, in the magazine order
    /// (<see cref="MagazineOrder"/>).</summary>
    public ReadOnlySpan<ShellLoad> Loads(int battery) => loads.AsSpan(shells[battery].Start, shells[battery].Count);

    /// <summary>A battery's loads as a slice of the pool.</summary>
    public Slice Shells(int battery) => shells[battery];

    /// <summary>The shell type a battery fires first, its lead: the first of its loads in the magazine order. Each mount
    /// moves on to the next kind as it runs out.</summary>
    public int Fires(int battery) => fires[battery];

    /// <summary>All the batteries' loads, the pool's length (a ship's rounds left by load).</summary>
    public int LoadCount => loads.Length;

    /// <summary>Where a mount's rounds by load start in <see cref="LoadRounds"/> (its battery's loads in order); the next
    /// mount's start ends them. Torpedo mounts have none.</summary>
    public int MountLoadStart(int mount) => mountLoadStart[mount];

    /// <summary>Each mount's rounds of each of its battery's loads at the start: its rounds split by the outfit's shares
    /// (largest remainder, so they add up), int.MaxValue each for a mount that counts none.</summary>
    public ReadOnlySpan<int> LoadRounds => loadRounds;

    /// <summary>The batteries' shells as shipgen resolved them (the report's, or a loadout's), into a loadout of the
    /// class: each battery's by its kind and gun, a shell type per block and gun, in the magazine order.</summary>
    internal static Loadout Compile(ShipClass c, IReadOnlyList<BatteryShells>? report, string name, ContentHash hash)
    {
        var l = new Loadout(c, name, hash);
        var types = new List<ShellTypeRow>();
        var typeIds = new List<string>();
        var typeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var loads = new List<ShellLoad>();
        // one id, one row: a battery's own shell under a block another battery of the gun names too gets an id of its own
        int TypeOf(string id, in ShellTypeRow t)
        {
            string key = id;
            for (int n = 2; typeIndex.TryGetValue(key, out int k); n++)
            {
                if (types[k] == t)
                    return k;
                key = FormattableString.Invariant($"{id} #{n}");
            }
            typeIndex[key] = types.Count;
            types.Add(t);
            typeIds.Add(key);
            return types.Count - 1;
        }
        static ShellTypeRow Given(ShellReport s, BatteryRow b) => new()
        {
            CalibreMm = b.CalibreMm, MassKg = s.MassKg, MuzzleVelocityMs = s.MuzzleVelocityMs, FormFactor = s.FormFactor, Kind = Kinds.ShellKindOf(s.Kind),
            FillerKg = s.FillerKg, FillerRe = s.FillerRe, FillerInPlate = s.FillerInPlate, FillerFull = s.FillerFull, Fuze = Kinds.FuzeOf(s.Fuze),
            FuzeDelayS = s.FuzeDelayS, FuzeStartCal = s.FuzeStartCal, FuzeOblique = s.FuzeOblique, FuzeTwoPlate = s.FuzePlates == "two",
            FuzeScatter = s.FuzeScatter, FuzeBlind = s.FuzeBlind,
            Cap = Kinds.CapOf(s.Cap), CapShare = s.CapShare, BreakObDeg = s.BreakObDeg, BreakTD = s.BreakTD, BodyQuality = s.BodyQuality,
        };

        var batteries = c.Batteries;
        l.shells = new Slice[batteries.Length];
        l.fires = new int[batteries.Length];
        for (int b = 0; b < batteries.Length; b++)
        {
            var row = batteries[b];
            int l0 = loads.Count;
            string which = row.Kind == Battery.Main ? "main" : "secondary";
            var given = report?.FirstOrDefault(s => s.Battery == which && s.CalibreMm == row.CalibreMm && s.CalibreLength == row.CalibreLength)
                ?? throw new ShipCompileException($"{which} {row.CalibreMm}/{row.CalibreLength}: no shells in the report");
            foreach (var s in given.Shells)
                loads.Add(new ShellLoad(TypeOf(FormattableString.Invariant($"{row.CalibreMm:0.#}/{row.CalibreLength:0.#} {s.Ammo}"), Given(s, row)), s.Share));
            // in the magazine order (stable: a kind's loads keep the design's order)
            var kind = row.Kind;
            var ordered = loads.Skip(l0).Select((x, i) => (x, i)).OrderBy(x => MagazineOrder.Rank(kind, types[x.x.ShellType].Kind)).ThenBy(x => x.i)
                .Select(x => x.x).ToList();
            loads.RemoveRange(l0, loads.Count - l0);
            loads.AddRange(ordered);
            l.shells[b] = new Slice(l0, loads.Count - l0);
            l.fires[b] = loads[l0].ShellType;
        }
        l.types = [.. types];
        l.typeIds = [.. typeIds];
        l.loads = [.. loads];
        l.Rounds();
        return l;
    }

    /// <summary>Rounds split by shares: each its share rounded down, the rest one each to the largest remainders (the
    /// earlier first on a tie), so they add up to the total.</summary>
    public static int[] Split(int total, IReadOnlyList<double> shares)
    {
        var n = new int[shares.Count];
        var rest = new double[shares.Count];
        int given = 0;
        for (int k = 0; k < shares.Count; k++)
        {
            double exact = total * shares[k];
            n[k] = (int)Math.Floor(exact);
            rest[k] = exact - n[k];
            given += n[k];
        }
        foreach (int k in Enumerable.Range(0, rest.Length).OrderByDescending(k => rest[k]).ThenBy(k => k).Take(Math.Max(total - given, 0)))
            n[k]++;
        return n;
    }

    /// <summary>Each mount's rounds by its battery's loads: its rounds split by the shares (<see cref="Split"/>).</summary>
    void Rounds()
    {
        var mounts = Class.Mounts;
        var starts = new int[mounts.Length + 1];
        var rounds = new List<int>();
        for (int i = 0; i < mounts.Length; i++)
        {
            starts[i] = rounds.Count;
            ref readonly var m = ref mounts[i];
            if (m.Battery < 0)
                continue;
            var loads = Loads(m.Battery);
            if (m.Rounds <= 0)
            {
                for (int k = 0; k < loads.Length; k++)
                    rounds.Add(int.MaxValue);
                continue;
            }
            var shares = new double[loads.Length];
            for (int k = 0; k < loads.Length; k++)
                shares[k] = loads[k].Share;
            rounds.AddRange(Split(m.Rounds, shares));
        }
        starts[mounts.Length] = rounds.Count;
        mountLoadStart = starts;
        loadRounds = [.. rounds];
    }
}

/// <summary>A loadout as given (docs/battle/shells-armour-damage.md S5): by battery ("main", "secondary", "main[i]",
/// "secondary[i]"), the shells it carries and their shares; each ammo a catalogue template's name
/// (docs/shipgen/shell-templates.md), a block of the design's, a kind (the battery's own block of it), or a block
/// given here (<see cref="Blocks"/>). <see cref="ShipClass.Load"/> makes it a <see cref="Loadout"/> of a class.</summary>
public sealed record LoadoutSpec(string Name, IReadOnlyList<(string Battery, List<ShellEntryInput> Shells)> Batteries,
    IReadOnlyDictionary<string, AmmoInput>? Blocks = null)
{
    /// <summary>A scenario's <c>"loadout": { "main": [{ "ammo": "RN 1912 HE (lyddite)", "share": 0.5 }, { "ammo": "ap" }] }</c>:
    /// an ammo is a name or a block (an object, as a design's ammunition gives one); shares left out split what the
    /// others leave evenly.</summary>
    public static LoadoutSpec FromJson(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new FormatException("a loadout is an object of batteries");
        var batteries = new List<(string, List<ShellEntryInput>)>();
        var blocks = new Dictionary<string, AmmoInput>(StringComparer.Ordinal);
        foreach (var b in e.EnumerateObject())
        {
            if (b.Value.ValueKind != JsonValueKind.Array)
                throw new FormatException($"loadout.{b.Name}: a list of shells");
            var shells = new List<ShellEntryInput>();
            foreach (var s in b.Value.EnumerateArray())
            {
                var ammo = s.ValueKind == JsonValueKind.Object && s.TryGetProperty("ammo", out var a) ? a
                    : throw new FormatException($"loadout.{b.Name}: each shell is {{ \"ammo\": ..., \"share\": ... }}");
                string ammoName;
                if (ammo.ValueKind == JsonValueKind.String)
                    ammoName = ammo.GetString()!;
                else if (ammo.ValueKind == JsonValueKind.Object)
                {
                    ammoName = FormattableString.Invariant($"{name} {b.Name} {shells.Count + 1}");
                    blocks[ammoName] = ammo.Deserialize(ShipgenJson.Default.AmmoInput) ?? throw new FormatException($"loadout.{b.Name}: an empty block");
                }
                else
                    throw new FormatException($"loadout.{b.Name}: ammo is a name or a block");
                double? Num(string key) => s.TryGetProperty(key, out var v) ? v.GetDouble() : null;
                shells.Add(new ShellEntryInput { Ammo = ammoName, Share = Num("share"), MassKg = Num("mass_kg"), MuzzleVelocityMs = Num("muzzle_velocity_ms") });
            }
            batteries.Add((b.Name, Even(shells)));
        }
        return new LoadoutSpec(name, batteries, blocks.Count > 0 ? blocks : null);
    }

    /// <summary>The dev flag's and the benches' form: batteries (<see cref="ParseBattery"/>) with | between them,
    /// <c>main:he|secondary:ap</c>; its name is the text unless given.</summary>
    public static LoadoutSpec Parse(string text, string? name = null) =>
        new(name ?? text, [.. text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ParseBattery)]);

    /// <summary>One battery of the dev flag's and the benches' form: <c>main:RN 1912 HE (lyddite)@0.5+ap</c> (shares after
    /// @, those left out splitting the rest evenly). Several batteries are several of these.</summary>
    public static (string Battery, List<ShellEntryInput> Shells) ParseBattery(string s)
    {
        int colon = s.IndexOf(':');
        if (colon <= 0 || colon == s.Length - 1)
            throw new FormatException($"\"{s}\": battery:shell[@share][+shell[@share]...]");
        var shells = new List<ShellEntryInput>();
        foreach (var part in s[(colon + 1)..].Split('+'))
        {
            int at = part.LastIndexOf('@');
            double? share = null;
            if (at > 0)
                share = double.TryParse(part[(at + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v
                    : throw new FormatException($"\"{part}\": a share after @");
            shells.Add(new ShellEntryInput { Ammo = (at > 0 ? part[..at] : part).Trim(), Share = share });
        }
        return (s[..colon].Trim(), Even(shells));
    }

    /// <summary>Shares left out split what the given ones leave, evenly.</summary>
    static List<ShellEntryInput> Even(List<ShellEntryInput> shells)
    {
        int open = shells.Count(x => x.Share is null);
        if (open == 0)
            return shells;
        double rest = Math.Max(0, 1 - shells.Sum(x => x.Share ?? 0)) / open;
        return [.. shells.Select(x => x.Share is null ? x with { Share = rest } : x)];
    }
}
