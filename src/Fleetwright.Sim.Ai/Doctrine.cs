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

    /// <summary>How outmatched a unit fights on: it turns away once its matchup against the enemy (docs/battle/command.md
    /// 5.1g) is under 1 / this, and a commander declines battle by it.</summary>
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

    /// <summary>What a ship does about a torpedo her lookouts see coming at her (<see cref="TorpedoEvasion"/>).</summary>
    TorpedoEvasion,

    /// <summary>A line that sees a torpedo attack coming (enemy torpedo craft closing on it, or tracks seen) turns away
    /// together through this many degrees, and back once it's past (docs/battle/command.md 5.1b). 0: it holds on.</summary>
    TurnAwayDeg,

    /// <summary>How a group of torpedo craft attacks (<see cref="Ai.TorpedoAttack"/>): each boat alone, or the flotilla
    /// together (docs/battle/command.md 5.1b).</summary>
    TorpedoAttack,

    /// <summary>A flotilla attacking together is beaten off (gives up its attack and runs) once this share of the boats it
    /// came in with is gone from its line.</summary>
    AttackBreakOffShare,

    /// <summary>Whether a force in a cruising order deploys into one line of battle on meeting the enemy's main body (1),
    /// or doesn't (0) (docs/battle/command.md 5.1c).</summary>
    Deploy,

    /// <summary>A side's commander breaks off (every unit home) once this share of the tonnage it came with is lost
    /// (docs/battle/command.md 5.1d). A side's value: read from its doctrine, not a unit's.</summary>
    BreakOffShare,

    /// <summary>Whether a side's heavy ships accept action at night or avoid it (<see cref="Ai.NightAction"/>): avoiding,
    /// its commander withdraws them from contact by night, his scouts keeping in touch. A side's value.</summary>
    NightAction,

    /// <summary>Whether a side's torpedo craft attack by night (1) or not (0): attacking, its commander sends them in on
    /// any heavy enemy in reach, engaged or not. A side's value.</summary>
    NightAttack,

    /// <summary>The confidence a unit is sent after an enemy at (docs/battle/command.md 5.1g): its matchup against him
    /// this much or more (1: it expects to win). A unit that meets an enemy fights him down to 1 / OutgunnedRatio.</summary>
    SeekRatio,

    /// <summary>How aggressive a side is, -1 (timid) to +1 (bold), 0 as its doctrine is written: the C-in-C's standing
    /// order. A side's value only; a unit's own is its <see cref="Temper"/>, added to it. What a ship or a group reads
    /// (side + tempers, clamped to ±1) scales the fields <see cref="SimSettings.AggressionShift"/> lists, each by (1 +
    /// shift x aggression), so a bold unit takes worse odds, closes nearer, stays in the line longer and breaks off
    /// later.</summary>
    Aggression,

    /// <summary>Whether a force's admiral disposes his members by their roles (1) or keeps them all in his line (0)
    /// (docs/battle/admirals-orders.md O7): his scouts scouting ahead of his main body, his flotillas and screens
    /// screening it. After <see cref="Aggression"/>, so the fields' numbers in logged commands stand.</summary>
    Dispose,

    /// <summary>A temper, -1 (timid) to +1 (bold), added to the side's <see cref="Aggression"/> (docs/map-ui/launch.md
    /// "Tempers", decision 1): a group's is its admiral's, taken by the groups under it that have none of their own; a
    /// ship's is her captain's, hers alone. A captain's decisions read side + her admiral + herself, an admiral's side +
    /// himself. A unit's value only.</summary>
    Temper,

    /// <summary>How a battery picks its shell (<see cref="ShellUse"/>; docs/battle/shell-choice.md A3): by its target, or
    /// by the magazine order. After <see cref="Temper"/>, so the fields' numbers in logged commands stand.</summary>
    Shells,

    /// <summary>A secondary battery firing at will takes only a contact it hits with this share of its shells or more, as
    /// the matchup reckons it (<see cref="Combat.Matchup.HitChance"/>, by its fire control's level, the contact as she's made out,
    /// at her range): no splashing at the edge of its reach (the user, 2026-10-27; docs/battle/fire-control.md F10's
    /// ammunition floor, the secondaries' first). 0: anything in reach. An ordered Engage fires regardless. Last, so
    /// the fields' numbers in logged commands stand.</summary>
    SecondaryHitFloor,

    /// <summary>When a secondary's casemated guns are closed up (<see cref="ManGuns"/>; docs/battle/crew.md N3a, decision
    /// 10: a navy's doctrine, set by the scenario from the record): the rest of the time their crews wait under cover.</summary>
    ManCasemates,

    /// <summary>When a secondary's open and shielded guns are closed up (<see cref="ManGuns"/>; N3a).</summary>
    ManOpenGuns,

    /// <summary>Where the reliefs come from first (<see cref="ReliefOrder"/>; N3a, decision 1).</summary>
    ReliefOrder,

    /// <summary>A ship seen done for (burning, silent and stopped: <c>Contact.DoneFor</c>; docs/battle/crew.md N5b) with
    /// nothing else in reach: fire at will finishes her, or ceases (<see cref="WreckFire"/>). Either way at will takes any
    /// other target in reach first; an ordered Engage stands.</summary>
    Wrecks,

    /// <summary>What a captain does about a ship in sight his navy can't tell for friend or foe (<see cref="Ai.Unknowns"/>;
    /// docs/battle/night.md Q6): fire on her, challenge her first, or hold his fire until she's made out or gives herself
    /// away. A bold captain (his aggression over 0) fires anyway where no friend is about her
    /// (<see cref="SimSettings.UnknownFriendM"/>). An ordered Engage fires regardless. After <see cref="Wrecks"/>, so the
    /// fields' numbers in logged commands stand.</summary>
    Unknowns,

    /// <summary>The share of the enemy's challenges a side's ships can answer with his reply (0-1; docs/battle/night.md Q6:
    /// the challenge known from captured signal books or overheard), so he takes them for friends. A side's value.</summary>
    AnswerChallenge,

    /// <summary>When a captain burns his searchlights in the dark (<see cref="SearchlightUse"/>; docs/battle/night.md Q7):
    /// never, only once he's under fire or lit, to light what his guns can't see, or to search for torpedo craft as well.
    /// After <see cref="AnswerChallenge"/>, so the fields' numbers in logged commands stand.</summary>
    Searchlights,

    /// <summary>When a captain fires star shell (<see cref="StarShellUse"/>; Q7): never, to light what his guns can't see,
    /// or to find as well: a contact held but not in sight, or one not made out, within its reach.</summary>
    StarShell,

    /// <summary>A captain challenges a ship he can't make out only within this range, m (Q7): beyond it he holds his fire
    /// and keeps dark until she's nearer.</summary>
    ChallengeM,

    /// <summary>What a ship does when a light (an enemy's beam, a flare) is on her in the dark (<see cref="Ai.WhenLit"/>;
    /// Q7): fight on, or put out her own lights and turn away from it.</summary>
    WhenLit,

    /// <summary>A squadron's spacing at night, a share of its day's (docs/battle/night.md Q7: close order, each ship
    /// keeping the next ahead in sight); 1 keeps the day's.</summary>
    NightSpacingShare,

    /// <summary>A unit darkened (1) or not (0) (docs/battle/night.md Q9, the strict silent setting): its ships show nothing
    /// of their own in the dark, no searchlights, no star shell by doctrine, no challenge lamp (an unknown held, not
    /// challenged), and their guns hold until she or her squadron is fired upon, then fight by their orders (torpedo craft
    /// creeping in for the best ambush). Torpedoes, the player's own orders (an Engage, Beams, Star shell) and what's
    /// burning go as ever.</summary>
    Darkened,
}

/// <summary>Doctrine's Shells: each battery loads the kind its target calls for (the captain's choice, the matchup's
/// judgement), or fires by the magazine order (a main battery AP first, a secondary HE).</summary>
/// <summary>When a gun's crew is closed up (docs/battle/crew.md N3a): always; while an enemy worth its fire is in its reach
/// and bearing; or only against torpedo craft in reach, with a watch of two guns a battery closed up meanwhile.</summary>
/// <summary>Doctrine's Unknowns (docs/battle/night.md Q6): fire on a ship that can't be made out, challenge her first, or
/// hold fire until she's known.</summary>
public enum Unknowns : byte
{
    Fire,
    Challenge,
    Hold,
}

/// <summary>Doctrine's Searchlights (docs/battle/night.md Q7).</summary>
public enum SearchlightUse : byte
{
    Never,
    WhenFiredOn,
    ToFire,
    Search,
}

/// <summary>Doctrine's StarShell (docs/battle/night.md Q7).</summary>
public enum StarShellUse : byte
{
    Never,
    ToFire,
    ToFind,
}

/// <summary>Doctrine's WhenLit (docs/battle/night.md Q7).</summary>
public enum WhenLit : byte
{
    FightOn,
    TurnAway,
}

public enum ManGuns : byte
{
    Always,
    InRange,
    TorpedoCraft,
}

/// <summary>Where a ship's reliefs come from first (N3a, decision 1): her men under cover and her idle hands, a live mount's
/// handling room kept; or (the USN's 1899 bill) the supply's spare men first.</summary>
public enum WreckFire : byte
{
    Finish,
    Cease,
}

public enum ReliefOrder : byte
{
    HandlingRoomProtected,
    ReserveInSupply,
}

public enum ShellUse : byte
{
    ByTarget,
    MagazineOrder,
}

/// <summary>Doctrine's NightAction: heavy ships accept action at night, or avoid it.</summary>
public enum NightAction : byte
{
    Accept,
    Avoid,
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

/// <summary>Doctrine's TorpedoEvasion: nothing, or comb the track (turn parallel to it, bow or stern on).</summary>
public enum TorpedoEvasion : byte
{
    None,
    Comb,
}

/// <summary>Doctrine's TorpedoAttack: each torpedo craft attacks alone (docs/battle/fighting.md 4.6), or a flotilla
/// attacks together, its admiral leading (docs/battle/command.md 5.1b).</summary>
public enum TorpedoAttack : byte
{
    Alone,
    Together,
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
public sealed class Doctrine(GroupSystem groups, IReadOnlyDictionary<string, double>? aggressionShift = null)
{
    // how far aggression moves each field (SimSettings.AggressionShift), by field; 0 for those it doesn't
    readonly double[] shift = Shifts(aggressionShift);

    static double[] Shifts(IReadOnlyDictionary<string, double>? given)
    {
        var r = new double[FieldCount];
        foreach (var (name, k) in given ?? new Dictionary<string, double>())
        {
            if (!Enum.TryParse<DoctrineField>(name, out var f) || f == DoctrineField.Aggression)
                throw new ArgumentException($"AggressionShift: no doctrine field \"{name}\" to shift");
            r[(int)f] = k;
        }
        return r;
    }

    public const int FieldCount = (int)DoctrineField.Darkened + 1;

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
        DoctrineField.TorpedoEvasion => (double)Ai.TorpedoEvasion.None,
        DoctrineField.TurnAwayDeg => 0,
        DoctrineField.TorpedoAttack => (double)Ai.TorpedoAttack.Alone,
        DoctrineField.AttackBreakOffShare => 1,   // never
        DoctrineField.Deploy => 0,
        DoctrineField.BreakOffShare => 2,   // never
        DoctrineField.NightAction => (double)Ai.NightAction.Accept,
        DoctrineField.NightAttack => 0,
        DoctrineField.SeekRatio => 1,
        DoctrineField.Aggression => 0,
        DoctrineField.Dispose => 0,
        DoctrineField.Temper => 0,
        DoctrineField.Shells => (double)ShellUse.MagazineOrder,
        DoctrineField.SecondaryHitFloor => 0,
        DoctrineField.ManCasemates or DoctrineField.ManOpenGuns => (double)ManGuns.Always,   // as before the crews' posture
        DoctrineField.ReliefOrder => (double)Ai.ReliefOrder.HandlingRoomProtected,
        DoctrineField.Wrecks => (double)WreckFire.Finish,   // as before: fire on while she's the one in reach
        DoctrineField.Unknowns => (double)Ai.Unknowns.Fire,   // as before friend or foe
        DoctrineField.AnswerChallenge => 0,
        DoctrineField.Searchlights => (double)SearchlightUse.ToFire,   // as Q4 left them
        DoctrineField.StarShell => (double)StarShellUse.ToFire,   // as Q5 left it
        DoctrineField.ChallengeM => double.PositiveInfinity,   // as Q6 left it
        DoctrineField.WhenLit => (double)Ai.WhenLit.FightOn,
        DoctrineField.NightSpacingShare => 1,
        DoctrineField.Darkened => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    // by unit (Unit.Id: a ship's entity, a group's id negated) and by side: a value per field, NaN where not set
    readonly SortedDictionary<int, double[]> units = [];
    readonly double[]?[] sides = new double[256][];

    /// <summary>Sets a unit's value (NaN: back to what it inherits). Aggression is a side's: a unit's is its temper.</summary>
    public void Set(Unit u, DoctrineField f, double value)
    {
        if (f == DoctrineField.Aggression)
            throw new ArgumentException("Aggression is a side's; a unit's own is its Temper");
        if (!units.TryGetValue(u.Id, out var r))
            units[u.Id] = r = New();
        r[(int)f] = value;
    }

    /// <summary>Sets a side's value (NaN: back to the default). A temper is a unit's.</summary>
    public void SetSide(int side, DoctrineField f, double value) =>
        (sides[side & 255] ??= New())[(int)(f != DoctrineField.Temper ? f : throw new ArgumentException("a Temper is a unit's; a side's is its Aggression"))] = value;

    static double[] New()
    {
        var r = new double[FieldCount];
        Array.Fill(r, double.NaN);
        return r;
    }

    /// <summary>A ship's value: its own, its groups' up the tree, its side's, the default; scaled by her aggression.
    /// Her Aggression is what she reads: her side's, her admiral's temper and her own, clamped to ±1.</summary>
    public double Of(int entity, byte side, DoctrineField f) =>
        f == DoctrineField.Aggression ? AggressionOf(entity, side)
        : Scaled(f, RawOf(entity, side, f), shift[(int)f] != 0 ? AggressionOf(entity, side) : 0);

    /// <summary>A group's value: its own, its forces' up the tree, its side's, the default; scaled by its aggression.
    /// Its Aggression is what its admiral reads: his side's and his temper, clamped to ±1.</summary>
    public double OfGroup(GroupSystem.Group? g, byte side, DoctrineField f) =>
        f == DoctrineField.Aggression ? AggressionOfGroup(g, side)
        : Scaled(f, RawOfGroup(g, side, f), shift[(int)f] != 0 ? AggressionOfGroup(g, side) : 0);

    /// <summary>A side's own value as set (NaN if it isn't), unscaled: what the UI shows.</summary>
    public double SideValue(int side, DoctrineField f) => sides[side & 255] is { } s ? s[(int)f] : double.NaN;

    double Scaled(DoctrineField f, double value, double aggression) =>
        aggression == 0 ? value : value * (1 + shift[(int)f] * aggression);

    // the side's aggression with the tempers on it (decision 1): an admiral's his nearest group's own, a captain's hers
    double AggressionOf(int entity, byte side)
    {
        double captain = units.TryGetValue(entity, out var r) && !double.IsNaN(r[(int)DoctrineField.Temper]) ? r[(int)DoctrineField.Temper] : 0;
        return Math.Clamp(SideAggression(side) + AdmiralTemper(groups.Of(entity)) + captain, -1, 1);
    }

    double AggressionOfGroup(GroupSystem.Group? g, byte side) => Math.Clamp(SideAggression(side) + AdmiralTemper(g), -1, 1);

    double SideAggression(byte side) =>
        sides[side] is { } s && !double.IsNaN(s[(int)DoctrineField.Aggression]) ? s[(int)DoctrineField.Aggression] : 0;

    double AdmiralTemper(GroupSystem.Group? g)
    {
        for (; g != null; g = g.Parent != 0 ? groups.Get(g.Parent) : null)
            if (units.TryGetValue(-g.GroupId, out var r) && !double.IsNaN(r[(int)DoctrineField.Temper]))
                return r[(int)DoctrineField.Temper];
        return 0;
    }

    double RawOf(int entity, byte side, DoctrineField f)
    {
        if (units.TryGetValue(entity, out var r) && !double.IsNaN(r[(int)f]))
            return r[(int)f];
        return RawOfGroup(groups.Of(entity), side, f);
    }

    double RawOfGroup(GroupSystem.Group? g, byte side, DoctrineField f)
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
        {
            // "from": an earlier preset's fields first, this one's over them (a period's night doctrine over the default)
            var fields = new List<(DoctrineField Field, double Value)>();
            if (p.Value.TryGetProperty("from", out var from))
                fields.AddRange(d.presets.TryGetValue(from.GetString() ?? "", out var b) ? b
                    : throw new ArgumentException($"doctrine preset \"{p.Name}\": from \"{from}\", which isn't defined before it"));
            foreach (var f in p.Value.EnumerateObject())
            {
                if (f.Name == "from")
                    continue;
                var v = Value(f.Name, f.Value);
                fields.RemoveAll(x => x.Field == v.Item1);
                fields.Add(v);
            }
            d.presets[p.Name] = [.. fields];
        }
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
            DoctrineField.TorpedoEvasion when Enum.TryParse<TorpedoEvasion>(name, ignoreCase: true, out var te) => (f, (double)te),
            DoctrineField.TorpedoAttack when Enum.TryParse<TorpedoAttack>(name, ignoreCase: true, out var ta) => (f, (double)ta),
            DoctrineField.NightAction when Enum.TryParse<NightAction>(name, ignoreCase: true, out var na) => (f, (double)na),
            DoctrineField.Shells when Enum.TryParse<ShellUse>(name, ignoreCase: true, out var su) => (f, (double)su),
            DoctrineField.ManCasemates or DoctrineField.ManOpenGuns when Enum.TryParse<ManGuns>(name, ignoreCase: true, out var mg) => (f, (double)mg),
            DoctrineField.ReliefOrder when Enum.TryParse<ReliefOrder>(name, ignoreCase: true, out var ro) => (f, (double)ro),
            DoctrineField.Wrecks when Enum.TryParse<WreckFire>(name, ignoreCase: true, out var wf) => (f, (double)wf),
            DoctrineField.Unknowns when Enum.TryParse<Unknowns>(name, ignoreCase: true, out var un) => (f, (double)un),
            DoctrineField.Searchlights when Enum.TryParse<SearchlightUse>(name, ignoreCase: true, out var sl) => (f, (double)sl),
            DoctrineField.StarShell when Enum.TryParse<StarShellUse>(name, ignoreCase: true, out var ss) => (f, (double)ss),
            DoctrineField.WhenLit when Enum.TryParse<WhenLit>(name, ignoreCase: true, out var wl) => (f, (double)wl),
            _ => throw new ArgumentException($"doctrine: \"{v}\" isn't a value of {field}"),
        };
    }

    /// <summary>An enum field's value by its name as a file writes it ("carry on", "at will"), or null for a field that's a
    /// number.</summary>
    public static string? NameOf(DoctrineField f, double v)
    {
        string? name = f switch
        {
            DoctrineField.Engage => ((Engage)(int)v).ToString(),
            DoctrineField.Torpedoes => ((TorpedoUse)(int)v).ToString(),
            DoctrineField.Evasion => ((Evasion)(int)v).ToString(),
            DoctrineField.TorpedoEvasion => ((TorpedoEvasion)(int)v).ToString(),
            DoctrineField.TorpedoAttack => ((TorpedoAttack)(int)v).ToString(),
            DoctrineField.NightAction => ((NightAction)(int)v).ToString(),
            DoctrineField.Shells => ((ShellUse)(int)v).ToString(),
            DoctrineField.ManCasemates or DoctrineField.ManOpenGuns => ((ManGuns)(int)v).ToString(),
            DoctrineField.ReliefOrder => ((ReliefOrder)(int)v).ToString(),
            DoctrineField.Wrecks => ((WreckFire)(int)v).ToString(),
            DoctrineField.Unknowns => ((Unknowns)(int)v).ToString(),
            DoctrineField.Searchlights => ((SearchlightUse)(int)v).ToString(),
            DoctrineField.StarShell => ((StarShellUse)(int)v).ToString(),
            DoctrineField.WhenLit => ((WhenLit)(int)v).ToString(),
            _ => null,
        };
        // "CarryOn" -> "carry on"
        return name == null ? null : string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? " " + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
