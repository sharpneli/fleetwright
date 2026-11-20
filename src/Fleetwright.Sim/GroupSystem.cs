using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>How a group's members are placed about its guide (the first in its line): ships in a squadron, squadrons
/// (or forces) in a force.</summary>
public enum Formation : byte
{
    /// <summary>Line ahead: each in the wake of the one before, a spacing astern. Squadrons in line ahead make one
    /// line: each squadron's guide follows the last ship of the one before.</summary>
    Column,

    /// <summary>Line abreast: each a spacing further to starboard of the guide, level with it.</summary>
    LineAbreast,

    /// <summary>Echelon: each a spacing further back along a 45° line to starboard, or to port.</summary>
    EchelonStarboard,
    EchelonPort,

    /// <summary>A line of bearing at any angle (<see cref="GroupSystem.Group.BearingDeg"/>): each a spacing further along
    /// the line, placed on the guide. What a turn together leaves (docs/battle/command.md 5.1b); the four above are
    /// lines of bearing too (<see cref="GroupSystem.BearingOf"/>), and line ahead keeps its name, since its ships turn in
    /// succession.</summary>
    Bearing,
}

/// <summary>A unit's whole line at once, down its tree (<see cref="GroupSystem.Shape"/>, docs/map-ui/battle-hud.md P1):
/// the formations a fleet sails in, each one order however deep it nests.</summary>
public enum LineShape : byte
{
    /// <summary>One line ahead: every squadron and force in column, the squadrons as far apart as their own ships.</summary>
    OneLine,

    /// <summary>The cruising order: every squadron a column in line ahead, the columns abreast, as far apart as the
    /// longest column is long, so its columns turning in succession through 8 points make one line (the deployment).</summary>
    Columns,

    /// <summary>Every ship abreast of the guide, each squadron at its own spacing, the squadrons clear of each other.</summary>
    Abreast,
}

/// <summary>Whether a member keeps its place in its group's formation (docs/battle/plan.md step 2.6). A detached member
/// (a ship, or a squadron in a force) stays a member but is out of the line: it has no station, and its group's rails
/// don't carry it.</summary>
public enum Detachment : byte
{
    /// <summary>In the line, on its station.</summary>
    InLine,

    /// <summary>Taken out by an order to it alone: an order to its group gathers it back in.</summary>
    Nudged,

    /// <summary>Detached on purpose (a scout, a ship sent home): it stays out until it's handed back.</summary>
    Pinned,

    /// <summary>Out of the line for the AI's own purposes (its task, or its side's commander's order): not the
    /// player's, so its superior's orders wait for the task's end without a notice, and its group's gather it not.</summary>
    Tasked,
}

/// <summary>What the group system understands (<see cref="GroupSystem"/>; its commands are built by its statics).</summary>
public enum GroupCommand : ushort
{
    Create = 1,
    Add,
    Remove,
    SetFormation,
    Disband,
    Join,
    CreateOfNewest,
    Detach,
    Form,
    Enter,
    Exit,
    DetachGroup,
    EnterAlongside,
    TurnTogether,
    Reverse,
    Divide,
    Merge,
    Subordinate,
    Shape,
    Wings,
    Straggle,
}

/// <summary>The world's groups (docs/battle/plan.md steps 2.4 and 2.6): units that move together, in order, with a
/// formation and a spacing. A squadron's members are ships; a force's are groups (squadrons, or forces: as many levels
/// as nest), never both. The members not detached are the group's line, and the first of the line is its guide: the
/// member the group's orders go to. A group is the world's state, not the AI's: the map draws it, its admiral (the
/// AI's layer above captains) keeps it in formation, and the rails clock tier moves its line whole. A ship is in at most
/// one squadron, a group in at most one force. Groups are made and changed by commands, so they replay; they hold no
/// ship state of their own. The world registers it second, after the ships (<see cref="Id"/>).</summary>
public sealed class GroupSystem : IWorldSystem
{
    public const byte Id = 2;

    /// <summary>One group: its id, name, formation, spacing in metres, the force it's in (0 for none), and members in
    /// order (ships' entities in a squadron, group ids in a force), each in the line or detached.</summary>
    public sealed class Group
    {
        public int GroupId { get; internal init; }
        public string Name { get; internal set; } = "";
        public Formation Formation { get; internal set; }
        public double SpacingM { get; internal set; }

        /// <summary>A <see cref="Formation.Bearing"/>'s line: the bearing its members lie on from the guide, degrees
        /// clockwise from the guide's heading (180 astern, 90 abeam to starboard).</summary>
        public double BearingDeg { get; internal set; }

        /// <summary>The bearing its line lies on from the guide, whatever its formation (<see cref="BearingOf"/>).</summary>
        public double LineBearingDeg => BearingOf(Formation, BearingDeg);

        /// <summary>A squadron's lookouts on its bows (docs/battle/allocation.md AL4): how far off its guide, 0 for none. In
        /// line ahead its last ships take the stations (<see cref="Wings"/>), the rest keep the column.</summary>
        public double WingM { get; internal set; }

        /// <summary>How many of its line are out on its bows: up to two (the last on the starboard bow, the one before her
        /// on the port), leaving two in the column; none but in a squadron in line ahead with <see cref="WingM"/>.</summary>
        public int Wings => !IsForce && Formation == Formation.Column && WingM > 0 && StragglerCount == 0 ? Math.Clamp(line.Count - 2, 0, 2) : 0;

        /// <summary>How many of a squadron's line straggle (<see cref="Straggle"/>): its last, nobody keeping station on them;
        /// none when they all do (then they're simply its line).</summary>
        public int StragglerCount { get; private set; }

        /// <summary>How many of its line keep the formation: all but its stragglers.</summary>
        public int Keeping => line.Count - StragglerCount;

        /// <summary>A force: its members are groups. A squadron's are ships.</summary>
        public bool IsForce { get; internal set; }

        /// <summary>The id of the force it's in, or 0.</summary>
        public int Parent { get; internal set; }

        readonly List<int> members = [];
        readonly List<Detachment> detached = [];
        readonly List<int> line = [];
        internal GroupSystem? Owner;   // a force asks it which of its groups have anyone in line
        public IReadOnlyList<int> Members => members;

        /// <summary>The members in the formation, in order: the guide first.</summary>
        public IReadOnlyList<int> Line => line;

        /// <summary>The first member in the line (a ship, or a group in a force), or 0 when every member is detached.</summary>
        public int Guide => line.Count > 0 ? line[0] : 0;

        public Detachment DetachmentAt(int i) => detached[i];

        /// <summary>A member's detachment (<see cref="Detachment.InLine"/> for one that isn't a member).</summary>
        public Detachment DetachmentOf(int member)
        {
            int i = members.IndexOf(member);
            return i >= 0 ? detached[i] : Detachment.InLine;
        }

        internal bool Contains(int member) => members.Contains(member);

        internal void Add(int member)
        {
            Owner?.MembershipChanged();
            members.Add(member);
            detached.Add(Detachment.InLine);
            Relink();
        }

        /// <summary>A member at a place in the order (past the end: the end), in the line.</summary>
        internal void Insert(int member, int at)
        {
            Owner?.MembershipChanged();
            at = Math.Clamp(at, 0, members.Count);
            members.Insert(at, member);
            detached.Insert(at, Detachment.InLine);
            Relink();
        }

        /// <summary>A member moved to a place in the order (counted as the order is before the move), keeping its
        /// detachment.</summary>
        internal void Move(int member, int at)
        {
            int i = members.IndexOf(member);
            if (i < 0)
                return;
            var d = detached[i];
            members.RemoveAt(i);
            detached.RemoveAt(i);
            at = Math.Clamp(at > i ? at - 1 : at, 0, members.Count);
            members.Insert(at, member);
            detached.Insert(at, d);
            Relink();
        }

        /// <summary>A member's place taken by another group, as detached as it was (a squadron wrapped in a force).</summary>
        internal void Replace(int member, int by)
        {
            int i = members.IndexOf(member);
            if (i < 0)
                return;
            Owner?.MembershipChanged();
            members[i] = by;
            Relink();
        }

        internal bool Remove(int member)
        {
            int i = members.IndexOf(member);
            if (i < 0)
                return false;
            Owner?.MembershipChanged();
            members.RemoveAt(i);
            detached.RemoveAt(i);
            Relink();
            return true;
        }

        /// <summary>Every member out (a squadron becoming a force of its divisions).</summary>
        internal void Clear()
        {
            Owner?.MembershipChanged();
            members.Clear();
            detached.Clear();
            Relink();
        }

        /// <summary>The members in the reverse order: the far end of the line guides.</summary>
        internal void Reverse()
        {
            Owner?.MembershipChanged();
            members.Reverse();
            detached.Reverse();
            Relink();
        }

        internal void Detach(int member, Detachment d)
        {
            int i = members.IndexOf(member);
            if (i < 0)
                return;
            detached[i] = d;
            Relink();
        }

        /// <summary>The line again from the members: those not detached, and in a force only groups with anyone in
        /// their own line (a squadron whose every ship is out of it isn't in its force's line, so it doesn't guide it).
        /// Whether it changed.</summary>
        internal bool Relink()
        {
            int n = 0, stragglers = 0;
            bool changed = false;
            // a squadron's stragglers go last (<see cref="Straggle"/>): the ships keeping the formation first, in their order
            for (int pass = 0; pass < (IsForce ? 1 : 2); pass++)
                for (int i = 0; i < members.Count; i++)
                {
                    if (detached[i] != Detachment.InLine || IsForce && Owner != null && !Owner.HasLine(members[i]))
                        continue;
                    if (!IsForce && (Owner?.IsStraggling(members[i]) == true) != (pass == 1))
                        continue;
                    stragglers += pass;
                    if (n == line.Count)
                        line.Add(members[i]);
                    else if (line[n] != members[i])
                        line[n] = members[i];
                    else
                    {
                        n++;
                        continue;
                    }
                    changed = true;
                    n++;
                }
            if (line.Count > n)
            {
                line.RemoveRange(n, line.Count - n);
                changed = true;
            }
            StragglerCount = stragglers < n ? stragglers : 0;
            return changed;
        }
    }

    readonly List<Group> groups = [];   // by id order
    int nextId = 1;

    internal GroupSystem()
    {
    }

    /// <summary>The ships, for a group's side (set by the world).</summary>
    internal Combat.ShipStore? Store { get; init; }

    /// <summary>The side a group is on (its ships' one side), or -1 with none of them at sea.</summary>
    public int SideOf(Group g)
    {
        for (int i = 0; i < g.Members.Count; i++)
        {
            int m = g.Members[i];
            if (g.IsForce)
            {
                if (Get(m) is { } sub && SideOf(sub) is int s and >= 0)
                    return s;
            }
            else if (Store?.SlotOf(m) is int slot and >= 0)
                return Store[slot].Side;
        }
        return -1;
    }

    public IReadOnlyList<Group> Groups => groups;

    /// <summary>The groups by index, for loops that mustn't allocate (a foreach over the interface boxes).</summary>
    public int Count => groups.Count;
    public Group this[int i] => groups[i];

    /// <summary>The squadron a ship is in, or null: by an index rebuilt after membership changes (2.7c: the rails
    /// check asks it of every ship every few seconds, and the scan of every group's members it was made that O(n²)).</summary>
    public Group? Of(int entity)
    {
        if (squadronStale)
            IndexSquadrons();
        return (uint)entity < (uint)squadronOf.Length ? squadronOf[entity] : null;
    }

    Group?[] squadronOf = [];   // by entity
    bool squadronStale = true;

    /// <summary>Whether a ship straggles (<see cref="Straggle"/>): out of her line's chain, at its tail, until she rejoins at its
    /// rear.</summary>
    public bool IsStraggling(int entity) => (uint)entity < (uint)straggling.Length && straggling[entity];

    bool[] straggling = [];   // by entity

    /// <summary>Sets a ship's flag; only one in a squadron straggles, and the squadron index has made room for every one
    /// (<see cref="IndexSquadrons"/>).</summary>
    void SetStraggling(int entity, bool on)
    {
        if (entity <= 0 || !on && !IsStraggling(entity))
            return;
        if (entity >= straggling.Length)
            Array.Resize(ref straggling, Math.Max(entity + 1, straggling.Length * 2));   // not met: Straggle asks Of first
        straggling[entity] = on;
    }

    internal void MembershipChanged() => squadronStale = true;

    void IndexSquadrons()
    {
        Array.Clear(squadronOf);
        foreach (var g in groups)
        {
            if (g.IsForce)
                continue;
            var members = g.Members;
            for (int i = 0; i < members.Count; i++)   // by index: an interface's foreach would box its enumerator
            {
                int e = members[i];
                if (e < 0)
                    continue;
                if (e >= squadronOf.Length)
                    Array.Resize(ref squadronOf, Math.Max(e + 1, squadronOf.Length * 2));
                squadronOf[e] ??= g;   // the first squadron that holds it, as the scan found
            }
        }
        if (straggling.Length < squadronOf.Length)
            Array.Resize(ref straggling, squadronOf.Length);   // room for every ship in a squadron: a straggler's flag allocates nothing in a fight
        squadronStale = false;
    }

    /// <summary>A group by its id, or null: by an index (ids are handed out increasing and never reused), not a scan of
    /// every group (it's asked per ship and per unit: the scan made those O(n²)).</summary>
    public Group? Get(int groupId) => (uint)groupId < (uint)byId.Length ? byId[groupId] : null;

    Group?[] byId = new Group?[64];

    void AddGroup(Group g)
    {
        MembershipChanged();
        groups.Add(g);
        if (g.GroupId >= byId.Length)
            Array.Resize(ref byId, Math.Max(g.GroupId + 1, byId.Length * 2));
        byId[g.GroupId] = g;
    }

    void RemoveGroup(Group g)
    {
        MembershipChanged();
        groups.Remove(g);
        if ((uint)g.GroupId < (uint)byId.Length && byId[g.GroupId] == g)
            byId[g.GroupId] = null;
    }

    /// <summary>Names a group: the start state's (a scenario's "2nd Battle Squadron"), set before the world runs, so a
    /// replay of the same start has it; not a command (commands carry no text). The default is "Group n" or
    /// "Force n".</summary>
    public void SetName(int groupId, string name)
    {
        if (Get(groupId) is { } g)
            g.Name = name;
    }

    /// <summary>Whether a group has anyone in its line.</summary>
    bool HasLine(int groupId) => Get(groupId) is { } g && g.Line.Count > 0;

    /// <summary>Every force's line again, after its groups' lines changed (as deep as forces nest).</summary>
    void RelinkForces()
    {
        for (bool changed = true; changed;)
        {
            changed = false;
            for (int k = 0; k < groups.Count; k++)
                if (groups[k].IsForce)
                    changed |= groups[k].Relink();
        }
    }

    /// <summary>Whether a ship is in a squadron's line: a member, not detached.</summary>
    public bool InLine(int entity) => Of(entity) is { } g && g.DetachmentOf(entity) == Detachment.InLine;

    /// <summary>Whether a group is in a force's line: a member, not detached.</summary>
    public bool InForceLine(Group g) => g.Parent != 0 && Get(g.Parent) is { } f && f.DetachmentOf(g.GroupId) == Detachment.InLine;

    /// <summary>The outermost group whose line a group moves with: itself, or the force whose line it's in, and so up.</summary>
    public Group TopOf(Group g)
    {
        while (g.Parent != 0 && Get(g.Parent) is { } f && f.DetachmentOf(g.GroupId) == Detachment.InLine)
            g = f;
        return g;
    }

    /// <summary>The outermost group a ship moves with (its squadron's top), or null for a ship alone or detached.</summary>
    public Group? UnitOf(int entity) => Of(entity) is { } g && g.DetachmentOf(entity) == Detachment.InLine ? TopOf(g) : null;

    /// <summary>The ship that leads a group: its guide, or its guide group's, down to a ship; 0 if there's none.</summary>
    public int GuideShip(Group g)
    {
        while (g.IsForce)
        {
            if (g.Guide == 0 || Get(g.Guide) is not { } sub)
                return 0;
            g = sub;
        }
        return g.Guide;
    }

    /// <summary>The last ship of a group's line, down the tree, that keeps the formation (its stragglers aren't followed); 0 if
    /// there's none.</summary>
    public int LastShip(Group g)
    {
        if (!g.IsForce)
            return g.Line.Count > 0 ? g.Line[g.Keeping - 1] : 0;   // its stragglers aren't followed
        for (int k = g.Line.Count - 1; k >= 0; k--)
            if (Get(g.Line[k]) is { } sub && LastShip(sub) is int s and > 0)
                return s;
        return 0;
    }

    // ------------------------------------------------------------------ formations

    /// <summary>The bearing a formation's line lies on from its guide, degrees clockwise from the guide's heading: line
    /// ahead 180, line abreast 90, echelon 135 to starboard and 225 to port, a line of bearing its own.</summary>
    public static double BearingOf(Formation f, double bearingDeg = 0) => f switch
    {
        Formation.Column => 180,
        Formation.LineAbreast => 90,
        Formation.EchelonStarboard => 135,
        Formation.EchelonPort => 225,
        Formation.Bearing => bearingDeg,
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    /// <summary>The station of the i-th ship of a squadron's line (<see cref="Group.Line"/>, i from 1): the ship it
    /// keeps station on, and how far astern of it and to its starboard. In column each ship follows the one before it
    /// (a turn goes round in succession, near enough); in any other line it's placed on the guide (the group turns
    /// together, the outer ships wheeling).</summary>
    public static (int Target, double AsternM, double AbeamM) StationOf(Formation f, double spacing, IReadOnlyList<int> line, int i, double bearingDeg = 0)
    {
        var (astern, abeam) = OffsetFromGuide(f, spacing, i, bearingDeg);
        return f == Formation.Column ? (line[i - 1], spacing, 0) : (line[0], astern, abeam);
    }

    /// <summary>The station of the i-th ship of a squadron's line (i from 1): a lookout on its guide's bow
    /// (<see cref="Group.Wings"/>), else the formation's.</summary>
    public static (int Target, double AsternM, double AbeamM) StationOf(Group g, int i)
    {
        if (WingOf(g, i) is int k and >= 0)
        {
            var (a, b) = WingOffset(g.WingM, k);
            return (g.Line[0], a, b);
        }
        return StationOf(g.Formation, g.SpacingM, g.Line, i, g.BearingDeg);
    }

    /// <summary>Which of a squadron's lookouts the i-th of its line is: 0 on the starboard bow (the last), 1 on the port;
    /// -1 for one in the formation.</summary>
    public static int WingOf(Group g, int i)
    {
        int k = g.Line.Count - 1 - i;
        return i >= 1 && k < g.Wings ? k : -1;
    }

    /// <summary>Whether a ship is out on her squadron's bow as a lookout (<see cref="Group.Wings"/>).</summary>
    public bool IsWing(int ship)
    {
        if (Of(ship) is not { Wings: > 0 } g)
            return false;
        var line = g.Line;
        for (int i = line.Count - g.Wings; i < line.Count; i++)
            if (line[i] == ship)
                return i >= 1;
        return false;
    }

    /// <summary>A lookout's station from the guide (docs/battle/allocation.md AL4): <paramref name="wingM"/> off her,
    /// 45° off her bow, the first to starboard and the second to port.</summary>
    public static (double AsternM, double AbeamM) WingOffset(double wingM, int k)
    {
        const double Diag = 0.70710678118654752;
        return (-wingM * Diag, (k == 0 ? 1 : -1) * wingM * Diag);
    }

    /// <summary>The station of the i-th group of a force's line (i from 1), for its guide ship: the ship it keeps
    /// station on, and how far astern of it and to its starboard. Where the force's line goes on from the group before
    /// (<see cref="Chains"/>: line ahead, or a line of bearing its groups share) it's a spacing on along it from the last
    /// ship of that group (one line); else it's placed on the force's guide ship.</summary>
    public (int Target, double AsternM, double AbeamM) StationOfGroup(Group force, int i)
    {
        var line = force.Line;
        if (force.Formation == Formation.Column)
            return (Get(line[i - 1]) is { } before ? LastShip(before) : 0, force.SpacingM, 0);
        if (Get(line[i - 1]) is { } prior && Chains(force, prior))
        {
            var (a, b) = OffsetFromGuide(Formation.Bearing, force.SpacingM, 1, force.BearingDeg);
            return (LastShip(prior), a, b);
        }
        var (astern, abeam) = OffsetFromGuide(force.Formation, force.SpacingM, i, force.BearingDeg);
        return (GuideShip(force), astern, abeam);
    }

    /// <summary>Whether a force's line goes on from the last ship of a group of it, one line: in line ahead always (as
    /// before lines of bearing), and in a line of bearing when that group lies on the same bearing (line ahead turned
    /// together stays one line).</summary>
    static bool Chains(Group force, Group before) => force.Formation == Formation.Column ||
        force.Formation == Formation.Bearing && Math.Abs(Wrap180(force.BearingDeg - before.LineBearingDeg)) < 0.5;

    static double Wrap180(double d) => d - 360 * Math.Round(d / 360);

    static double Wrap360(double d) => ((d % 360) + 360) % 360;

    /// <summary>The i-th member of a line's station from the guide, with every ship on the guide's heading: how far
    /// astern and to starboard, i spacings along the line's bearing.</summary>
    public static (double AsternM, double AbeamM) OffsetFromGuide(Formation f, double spacing, int i, double bearingDeg = 0)
    {
        const double Diag = 0.70710678118654752;
        switch (f)
        {
            case Formation.Column: return (i * spacing, 0);
            case Formation.LineAbreast: return (0, i * spacing);
            case Formation.EchelonStarboard: return (i * spacing * Diag, i * spacing * Diag);
            case Formation.EchelonPort: return (i * spacing * Diag, -i * spacing * Diag);
            case Formation.Bearing:
                double r = bearingDeg * Math.PI / 180;
                return (-i * spacing * Math.Cos(r), i * spacing * Math.Sin(r));
            default: throw new ArgumentOutOfRangeException(nameof(f));
        }
    }

    /// <summary>Every ship of a group's line, down the tree, with its station from the group's guide ship (which is
    /// first, at 0, 0), every ship on the guide's heading: the one geometry the rails, the admirals and the tests read.
    /// A force's stations are its groups' guides' (<see cref="StationOfGroup"/>), its groups' own added on.</summary>
    public void LineOffsets(Group g, List<(int Ship, double AsternM, double AbeamM)> into)
    {
        into.Clear();
        Offsets(g, 0, 0, into);
        Trailing(g, into);
    }

    /// <summary>A group's line laid into the list, its stragglers left out but those of each run of it that chains into one
    /// line (<see cref="Chains"/>) once the run's laid, astern of it (<see cref="Trailing"/>); those of its last run are left
    /// for the caller, whose line may go on from it.</summary>
    void Offsets(Group g, double astern0, double abeam0, List<(int Ship, double AsternM, double AbeamM)> into)
    {
        var line = g.Line;
        if (!g.IsForce)
        {
            for (int i = 0; i < g.Keeping; i++)
            {
                var (a, b) = WingOf(g, i) is int k and >= 0 ? WingOffset(g.WingM, k) : OffsetFromGuide(g.Formation, g.SpacingM, i, g.BearingDeg);
                into.Add((line[i], astern0 + a, abeam0 + b));
            }
            return;
        }
        Group? before = null;
        int run = 0;
        for (int k = 0; k < line.Count; k++)
        {
            if (Get(line[k]) is not { } sub)
                continue;
            bool chained = before != null && Chains(g, before);
            if (before != null && !chained)
            {
                for (int r = run; r < k; r++)
                    if (Get(line[r]) is { } done)
                        Trailing(done, into);
                run = k;
            }
            var (a, b) = GroupStation(g.Formation, g.SpacingM, k, (astern0, abeam0), into.Count > 0 ? (into[^1].AsternM, into[^1].AbeamM) : null,
                g.BearingDeg, chained);
            Offsets(sub, a, b, into);
            before = sub;
        }
    }

    /// <summary>The stragglers of a group's last run (the part of its line its caller's may go on from), each a spacing of
    /// her squadron's on along its line from the last ship laid: at the tail of the line she's in.</summary>
    void Trailing(Group g, List<(int Ship, double AsternM, double AbeamM)> into)
    {
        var line = g.Line;
        if (!g.IsForce)
        {
            var (a, b) = OffsetFromGuide(Formation.Bearing, g.SpacingM, 1, g.LineBearingDeg);
            for (int i = g.Keeping; i < line.Count; i++)
            {
                var (la, lb) = into.Count > 0 ? (into[^1].AsternM, into[^1].AbeamM) : (0.0, 0.0);
                into.Add((line[i], la + a, lb + b));
            }
            return;
        }
        int run = 0;
        for (int k = 1; k < line.Count; k++)
            if (Get(line[k - 1]) is { } prior && !Chains(g, prior))
                run = k;
        for (int k = run; k < line.Count; k++)
            if (Get(line[k]) is { } sub)
                Trailing(sub, into);
    }

    /// <summary>Whether another ship of her line keeps station on a ship (a straggler is out of the chain, so the ship astern of
    /// her closes up on the one ahead of her: <see cref="Straggle"/>): in a squadron in line ahead the ship astern of her; any
    /// squadron's guide, its others; and its last ship the next group's guide, where a force's line goes on from it (as far up
    /// as it's its force's last).</summary>
    public bool Leads(int ship)
    {
        if (Of(ship) is not { } g || g.DetachmentOf(ship) != Detachment.InLine)
            return false;
        var line = g.Line;
        int i = -1;
        for (int k = 0; k < g.Keeping; k++)
            if (line[k] == ship)
                i = k;
        if (i < 0)
            return false;   // a straggler
        if (i < g.Keeping - 1)
            return i == 0 || g.Formation == Formation.Column;   // abreast or on a bearing the others are all on the guide
        return TrailFollowed(g);
    }

    /// <summary>Whether a group's last ship (<see cref="LastShip"/>) has another group's guide on her: its force's line goes
    /// on from it, at the first level up where it isn't its force's last.</summary>
    public bool TrailFollowed(Group g)
    {
        for (var cur = g; cur.Parent != 0 && InForceLine(cur) && Get(cur.Parent) is { } p; cur = p)
        {
            int k = -1;
            for (int j = 0; j < p.Line.Count; j++)
                if (p.Line[j] == cur.GroupId)
                    k = j;
            if (k < 0)
                return false;
            if (k < p.Line.Count - 1)
                return Chains(p, cur);
        }
        return false;
    }

    /// <summary>Where the k-th group of a force's line puts its guide ship (k from 0), about the guide ship of the
    /// whole line, given the force's own guide ship's place there and the last ship placed before it (null: none):
    /// in line ahead a spacing astern of that last ship, in a line of bearing that goes on from the group before
    /// (<paramref name="chained"/>, <see cref="Chains"/>) a spacing on along the bearing from it, else the force's offset
    /// for k. The one rule <see cref="LineOffsets"/> and a scenario's placing (spawning a tree of groups on their
    /// stations) share.</summary>
    public static (double AsternM, double AbeamM) GroupStation(Formation f, double spacing, int k, (double AsternM, double AbeamM) forceGuide,
        (double AsternM, double AbeamM)? lastBefore, double bearingDeg = 0, bool chained = false)
    {
        if (k == 0)
            return forceGuide;
        if (f == Formation.Column)
            return lastBefore is { } last ? (last.AsternM + spacing, last.AbeamM) : forceGuide;   // astern of the last ship before
        if (chained && lastBefore is { } end)
        {
            var (ca, cb) = OffsetFromGuide(Formation.Bearing, spacing, 1, bearingDeg);
            return (end.AsternM + ca, end.AbeamM + cb);
        }
        var (oa, ob) = OffsetFromGuide(f, spacing, k, bearingDeg);
        return (forceGuide.AsternM + oa, forceGuide.AbeamM + ob);
    }

    /// <summary>A group's whole line into a shape (<see cref="LineShape"/>): in columns, the columns as far apart as the
    /// longest is long (the era's rule: each turning in succession through 8 points, they make one line); abreast, every ship a place
    /// at the widest spacing of its squadrons, or at <paramref name="spacingM"/> if one's given (every squadron's own
    /// spacing set to it: a scouting line).</summary>
    void Shape(Group g, LineShape shape, double spacingM = 0)
    {
        bool given = shape == LineShape.Abreast && spacingM > 0;
        double column = shape == LineShape.Columns ? Longest(g) : 0, ship = shape == LineShape.Abreast ? given ? spacingM : Widest(g) : 0;
        Shape(g, shape, column, ship, given);
    }

    /// <summary><see cref="Shape(Group, LineShape, double)"/> down the tree: a squadron keeps its spacing (abreast, takes
    /// the one given); a force in one line its first squadron's, in columns or abreast as many of the unit's slots as its
    /// widest member takes.</summary>
    void Shape(Group g, LineShape shape, double column, double ship, bool given)
    {
        if (!g.IsForce)
        {
            (g.Formation, g.BearingDeg) = (shape == LineShape.Abreast ? Formation.LineAbreast : Formation.Column, 0);
            if (given)
                g.SpacingM = Spacing(ship);
            return;
        }
        double spacing;
        if (shape == LineShape.OneLine)
        {
            var squadron = g;
            while (squadron.IsForce && squadron.Line.Count > 0 && Get(squadron.Line[0]) is { } down)
                squadron = down;
            spacing = squadron.SpacingM;
        }
        else
        {
            int slots = 1;
            foreach (int m in g.Line)
                if (Get(m) is { } sub)
                    slots = Math.Max(slots, Slots(sub, shape));
            spacing = slots * (shape == LineShape.Columns ? column : ship);
        }
        (g.Formation, g.SpacingM, g.BearingDeg) = (shape == LineShape.OneLine ? Formation.Column : Formation.LineAbreast, Spacing(spacing), 0);
        foreach (int m in g.Line)
            if (Get(m) is { } sub)
                Shape(sub, shape, column, ship, given);
    }

    /// <summary>How many places abreast a group takes in columns (a squadron one) or abreast (a squadron one a ship): its
    /// line's members as wide as its widest.</summary>
    int Slots(Group g, LineShape shape)
    {
        if (!g.IsForce)
            return shape == LineShape.Columns ? 1 : Math.Max(1, g.Members.Count);
        int widest = 1;
        foreach (int m in g.Line)
            if (Get(m) is { } sub)
                widest = Math.Max(widest, Slots(sub, shape));
        return Math.Max(1, g.Line.Count) * widest;
    }

    /// <summary>The longest column under a group: its ships, one spacing each (a column's length and the gap astern of it).</summary>
    double Longest(Group g)
    {
        if (!g.IsForce)
            return Math.Max(1, g.Members.Count) * g.SpacingM;
        double longest = 0;
        foreach (int m in g.Line)
            if (Get(m) is { } sub)
                longest = Math.Max(longest, Longest(sub));
        return longest;
    }

    /// <summary>The widest spacing of a squadron under a group: abreast, a ship's place.</summary>
    double Widest(Group g)
    {
        if (!g.IsForce)
            return g.SpacingM;
        double widest = 0;
        foreach (int m in g.Line)
            if (Get(m) is { } sub)
                widest = Math.Max(widest, Widest(sub));
        return widest;
    }

    /// <summary>A group's line turned together through an angle (+ to starboard), and every group in its line with it,
    /// as its ships will lie once each has turned: the line stays where it is, so its bearing from the guide's new
    /// heading is the old less the angle. A line that would lie ahead of the guide's beam is taken in the reverse order
    /// (its far end guides: after a turn through 16 points the rear ship leads), and one that comes out astern is line
    /// ahead again.</summary>
    void Turn(Group g, double deg)
    {
        double b = Wrap360(g.LineBearingDeg - deg);
        if (Math.Cos(b * Math.PI / 180) > 1e-9)
        {
            g.Reverse();
            b = Wrap360(b + 180);
        }
        (g.Formation, g.BearingDeg) = Math.Abs(b - 180) < 0.5 ? (Formation.Column, 0) : (Formation.Bearing, b);
        if (!g.IsForce)
            return;
        for (int k = 0; k < g.Line.Count; k++)
            if (Get(g.Line[k]) is { } sub)
                Turn(sub, deg);
    }

    // ------------------------------------------------------------------ commands

    /// <summary>A new squadron, guided by a ship (taken out of any squadron it was in); its id is the next.</summary>
    public static Command Create(int guide, Formation formation = Formation.Column, double spacingM = 600) =>
        new(Id, (ushort)GroupCommand.Create, guide, B: (int)formation, X: spacingM);

    /// <summary>A ship to a squadron (out of any other), at its end or before its <paramref name="at"/>-th member; one already
    /// in it is moved there. Refused for a ship of another side.</summary>
    public static Command Add(int groupId, int entity, int at = -1) => new(Id, (ushort)GroupCommand.Add, entity, A: groupId, B: at + 1);

    /// <summary>A ship to the end of the squadron another ship is in (out of any other): how a UI adds to a group it
    /// has just formed, before it knows the new group's id. Refused for a ship of another side.</summary>
    public static Command Join(int entity, int alongside) => new(Id, (ushort)GroupCommand.Join, entity, A: alongside);

    /// <summary>A new squadron of the last ships spawned, in the order they were spawned, the first the guide: how a UI
    /// forms a group of ships it has just spawned, before it knows their ids (sent right after the spawns, it names
    /// exactly them, on every replay too). Ships among them that are gone, or of another side than the first, are left
    /// out.</summary>
    public static Command CreateOfNewest(int count, Formation formation = Formation.Column, double spacingM = 600) =>
        new(Id, (ushort)GroupCommand.CreateOfNewest, A: count, B: (int)formation, X: spacingM);

    /// <summary>A ship out of its squadron (a guide's place goes to the next ship in the line).</summary>
    public static Command Remove(int entity) => new(Id, (ushort)GroupCommand.Remove, entity);

    /// <summary>A ship out of its squadron's line, or back into it (<see cref="Detachment.InLine"/>). It keeps its place
    /// among the members, so a guide that rejoins guides again. The AI gives these, from what the player's orders mean
    /// (docs/battle/plan.md step 2.6).</summary>
    public static Command Detach(int entity, Detachment d) => new(Id, (ushort)GroupCommand.Detach, entity, B: (int)d);

    /// <summary>A new force, guided by a group (taken out of any force it was in); its id is the next.</summary>
    public static Command Form(int groupId, Formation formation = Formation.LineAbreast, double spacingM = 3000) =>
        new(Id, (ushort)GroupCommand.Form, A: groupId, B: (int)formation, X: spacingM);

    /// <summary>A group to the end of a force (out of any other); refused for a group of another side, or one the
    /// force is inside.</summary>
    public static Command Enter(int forceId, int groupId) => new(Id, (ushort)GroupCommand.Enter, A: groupId, Y: forceId);

    /// <summary>A group to the end of the force another group is in: how a UI adds to a force it has just formed,
    /// before it knows the force's id (as <see cref="Join"/> for ships). Refused as <see cref="Enter"/> is.</summary>
    public static Command EnterAlongside(int groupId, int alongsideGroupId) =>
        new(Id, (ushort)GroupCommand.EnterAlongside, A: groupId, Y: alongsideGroupId);

    /// <summary>A group out of its force.</summary>
    public static Command Exit(int groupId) => new(Id, (ushort)GroupCommand.Exit, A: groupId);

    /// <summary>A group out of its force's line, or back into it, as <see cref="Detach"/> for a ship.</summary>
    public static Command DetachGroup(int groupId, Detachment d) => new(Id, (ushort)GroupCommand.DetachGroup, A: groupId, B: (int)d);

    /// <summary>A group's formation and spacing, and a line of bearing's bearing (<see cref="Group.BearingDeg"/>).</summary>
    public static Command SetFormation(int groupId, Formation formation, double spacingM, double bearingDeg = 0) =>
        new(Id, (ushort)GroupCommand.SetFormation, A: groupId, B: (int)formation, X: spacingM, Y: bearingDeg);

    /// <summary>A group's line, and every group in it, turned together through an angle, degrees (+ to starboard): the
    /// formation its ships will be in once each has turned (<see cref="Turn"/>). Its admiral turns them
    /// (docs/battle/command.md 5.1b).</summary>
    public static Command TurnTogether(int groupId, double deg) => new(Id, (ushort)GroupCommand.TurnTogether, A: groupId, X: deg);

    /// <summary>A group's members in the reverse order: its far end guides (a force deploying on its other wing,
    /// docs/battle/command.md 5.1c).</summary>
    public static Command Reverse(int groupId) => new(Id, (ushort)GroupCommand.Reverse, A: groupId);

    /// <summary>A squadron divided (docs/battle/admirals-orders.md O1b): it becomes a force of two new squadrons, its
    /// divisions, the first of its first <paramref name="first"/> members and the second of the rest, each in its
    /// formation and spacing and each member as detached as it was; the force keeps the squadron's id, name, place in
    /// its own force and its admiral, in one line where it was (line ahead, else a line of bearing on its old line's
    /// bearing, the divisions chained). Refused for a force, or a split that leaves a division empty. The player's and the
    /// admirals' to give.</summary>
    public static Command Divide(int groupId, int first) => new(Id, (ushort)GroupCommand.Divide, A: groupId, B: first);

    /// <summary>A force of squadrons merged into one squadron (docs/battle/admirals-orders.md O1c), <see cref="Divide"/>'s
    /// inverse: every ship of its squadrons, in their order, each as detached as she was in hers, in the first squadron's
    /// formation and spacing; the squadrons go, and the force, now a squadron, keeps its id, name, place and admiral.
    /// Refused for a squadron, or a force with a force in it.</summary>
    public static Command Merge(int forceId) => new(Id, (ushort)GroupCommand.Merge, A: forceId);

    /// <summary>A group put under another's admiral (docs/battle/admirals-orders.md O8c, the rail's drag and drop): into it
    /// if it's a force (out of any other); if it's a squadron, that squadron becomes the guide of a new force in its
    /// place (in its force, as detached as it was, named after it), the group astern of it in line ahead. Refused for
    /// groups of different sides, a group under itself or anything inside it.</summary>
    public static Command Subordinate(int groupId, int underGroupId) => new(Id, (ushort)GroupCommand.Subordinate, A: groupId, Y: underGroupId);

    /// <summary>A group's whole line, down its tree, into a <see cref="LineShape"/>: each group's formation and spacing
    /// (<see cref="Shape(Group, LineShape, double)"/>); abreast, at a spacing a ship if one's given. Its orders stand.</summary>
    public static Command Shape(int groupId, LineShape shape, double spacingM = 0) =>
        new(Id, (ushort)GroupCommand.Shape, A: groupId, B: (int)shape, X: spacingM);

    /// <summary>A squadron's lookouts out on its bows <paramref name="wingM"/> off its guide, or in (0)
    /// (<see cref="Group.WingM"/>, docs/battle/allocation.md AL4): its admiral's to give.</summary>
    public static Command SetWings(int groupId, double wingM) => new(Id, (ushort)GroupCommand.Wings, A: groupId, X: wingM);

    /// <summary>A ship out of her line's chain as a straggler, or back (docs/battle/command.md "Stragglers"): straggling she's
    /// last of her squadron's line and nobody keeps station on her (<see cref="LastShip"/>, <see cref="Leads"/>), the ships
    /// astern of her closing up on the one ahead of her; back, she's moved to her squadron's end, where she is by then. Her
    /// admiral's to give; she's no longer one once she leaves her squadron.</summary>
    public static Command Straggle(int entity, bool on) => new(Id, (ushort)GroupCommand.Straggle, entity, A: on ? 1 : 0);

    /// <summary>A group gone: its members are free (a force's groups stay squadrons and forces of their own), and it
    /// leaves its force.</summary>
    public static Command Disband(int groupId) => new(Id, (ushort)GroupCommand.Disband, A: groupId);

    public void Apply(World world, in Command c)
    {
        Change(world, c);
        RelinkForces();
    }

    void Change(World world, in Command c)
    {
        var code = (GroupCommand)c.Code;
        world.Ships.DerailFor(world, c);   // what changes comes off rails whole first (ShipSystem.Tiers.cs)
        switch (code)
        {
            case GroupCommand.Create:
                if (world.Ships.Store.SlotOf(c.Entity) < 0)
                    return;
                Leave(c.Entity);
                var g = New(false, (Formation)c.B, c.X);
                g.Add(c.Entity);
                break;
            case GroupCommand.CreateOfNewest:
                Group? made = null;
                for (int e = Math.Max(world.Ships.NewestEntity - (int)c.A + 1, 1); e <= world.Ships.NewestEntity; e++)
                {
                    if (world.Ships.Store.SlotOf(e) < 0 || made != null && !SameSide(world, e, made.Members[0]))
                        continue;
                    Leave(e);
                    made ??= New(false, (Formation)c.B, c.X);
                    made.Add(e);
                }
                break;
            case GroupCommand.Add:
                if (Get((int)c.A) is not { IsForce: false } to || world.Ships.Store.SlotOf(c.Entity) < 0 || !SameSide(world, c.Entity, to.Members[0]))
                    return;
                if (to.Contains(c.Entity))
                {
                    if (c.B > 0)
                        to.Move(c.Entity, c.B - 1);   // within it: to its new place
                    return;
                }
                Leave(c.Entity);
                if (c.B > 0)
                    to.Insert(c.Entity, c.B - 1);
                else
                    to.Add(c.Entity);
                break;
            case GroupCommand.Join:
                if (Of((int)c.A) is not { } with || world.Ships.Store.SlotOf(c.Entity) < 0 || with.Contains(c.Entity) || !SameSide(world, c.Entity, with.Members[0]))
                    return;
                Leave(c.Entity);
                with.Add(c.Entity);
                break;
            case GroupCommand.Remove:
                Leave(c.Entity);
                break;
            case GroupCommand.Detach:
                Of(c.Entity)?.Detach(c.Entity, Detached(c.B));
                break;
            case GroupCommand.Form:
                if (Get((int)c.A) is not { } first)
                    return;
                LeaveForce(first);
                var force = New(true, (Formation)c.B, c.X);
                force.Add(first.GroupId);
                first.Parent = force.GroupId;
                break;
            case GroupCommand.Enter or GroupCommand.EnterAlongside:
                var intoId = code == GroupCommand.Enter ? (int)c.Y : Get((int)c.Y)?.Parent ?? 0;
                if (Get((int)c.A) is not { } entering || Get(intoId) is not { IsForce: true } into || entering.Parent == into.GroupId ||
                    Within(into, entering) || !SameSide(world, AnyShip(entering), AnyShip(into)))
                    return;
                LeaveForce(entering);
                into.Add(entering.GroupId);
                entering.Parent = into.GroupId;
                break;
            case GroupCommand.Exit:
                if (Get((int)c.A) is { } leaving)
                    LeaveForce(leaving);
                break;
            case GroupCommand.DetachGroup:
                if (Get((int)c.A) is { Parent: > 0 } sub && Get(sub.Parent) is { } parent)
                    parent.Detach(sub.GroupId, Detached(c.B));
                break;
            case GroupCommand.SetFormation:
                if (Get((int)c.A) is { } f)
                    (f.Formation, f.SpacingM, f.BearingDeg) = (Formed(c.B), Spacing(c.X), (Formation)c.B == Formation.Bearing ? Wrap360(c.Y) : 0);
                break;
            case GroupCommand.Reverse:
                Get((int)c.A)?.Reverse();
                break;
            case GroupCommand.Straggle:
                if (Of(c.Entity) is not { } straggler || (c.A != 0) == IsStraggling(c.Entity))
                    return;
                SetStraggling(c.Entity, c.A != 0);
                if (c.A == 0)
                    straggler.Move(c.Entity, straggler.Members.Count);   // back at her squadron's end
                straggler.Relink();
                break;
            case GroupCommand.Wings:
                if (Get((int)c.A) is { IsForce: false } winged)
                    winged.WingM = double.IsFinite(c.X) ? Math.Max(c.X, 0) : 0;
                break;
            case GroupCommand.Shape:
                if (Get((int)c.A) is { } shaped)
                    Shape(shaped, (LineShape)c.B, c.X);
                break;
            case GroupCommand.TurnTogether:
                if (Get((int)c.A) is { } turned)
                    Turn(turned, c.X);
                break;
            case GroupCommand.Divide:
                if (Get((int)c.A) is { IsForce: false } whole && c.B >= 1 && c.B < whole.Members.Count)
                    Divide(whole, c.B);
                break;
            case GroupCommand.Subordinate:
                if (Get((int)c.A) is { } under && Get((int)c.Y) is { } over && under != over && !Within(over, under) &&
                    SameSide(world, AnyShip(under), AnyShip(over)))
                    Subordinate(under, over);
                break;
            case GroupCommand.Merge:
                if (Get((int)c.A) is { IsForce: true } merged && merged.Members.Count > 0 && merged.Members.All(m => Get(m) is { IsForce: false }))
                    Merge(merged);
                break;
            case GroupCommand.Disband:
                if (Get((int)c.A) is { } d)
                {
                    if (d.IsForce)
                        foreach (int m in d.Members)
                            if (Get(m) is { } freed)
                                freed.Parent = 0;
                    LeaveForce(d);
                    RemoveGroup(d);
                }
                break;
            default:
                throw new ArgumentException($"unknown group command {c.Code}");
        }
    }

    /// <summary><see cref="Divide(int, int)"/>: the squadron's members into two new squadrons, it a force of them. Allocates
    /// (a reorganisation, as a spawn does).</summary>
    void Divide(Group g, int first)
    {
        var members = g.Members.ToArray();
        var detachments = new Detachment[members.Length];
        for (int i = 0; i < members.Length; i++)
            detachments[i] = g.DetachmentAt(i);
        var divisions = new[] { New(false, g.Formation, g.SpacingM), New(false, g.Formation, g.SpacingM) };
        for (int k = 0; k < 2; k++)
            (divisions[k].BearingDeg, divisions[k].Name) = (g.BearingDeg, g.Name + (k == 0 ? " I" : " II"));
        double bearing = g.LineBearingDeg;
        g.Clear();
        for (int i = 0; i < members.Length; i++)
        {
            var d = divisions[i < first ? 0 : 1];
            d.Add(members[i]);
            if (detachments[i] != Detachment.InLine)
                d.Detach(members[i], detachments[i]);
        }
        (g.IsForce, g.WingM) = (true, 0);   // its lookouts come in: the divisions' admirals put out their own
        foreach (var d in divisions)
        {
            d.Parent = g.GroupId;
            g.Add(d.GroupId);
        }
        (g.Formation, g.BearingDeg) = g.Formation == Formation.Column ? (Formation.Column, 0) : (Formation.Bearing, bearing);
    }

    /// <summary><see cref="Subordinate(int, int)"/>: into a force, or a squadron wrapped in a new force in its place.</summary>
    void Subordinate(Group g, Group over)
    {
        if (over.IsForce)
        {
            if (g.Parent == over.GroupId)
                return;
            LeaveForce(g);
            over.Add(g.GroupId);
            g.Parent = over.GroupId;
            return;
        }
        if (g.Parent != 0)
            LeaveForce(g);
        var f = New(true, Formation.Column, over.SpacingM);
        f.Name = over.Name + " force";
        if (over.Parent != 0 && Get(over.Parent) is { } parent)
        {
            parent.Replace(over.GroupId, f.GroupId);
            f.Parent = parent.GroupId;
        }
        over.Parent = f.GroupId;
        f.Add(over.GroupId);
        f.Add(g.GroupId);
        g.Parent = f.GroupId;
    }

    /// <summary><see cref="Merge(int)"/>: the force's squadrons' ships into it, it a squadron, its squadrons gone.
    /// Allocates (a reorganisation).</summary>
    void Merge(Group f)
    {
        var squadrons = f.Members.Select(m => Get(m)!).ToArray();
        var first = squadrons[0];
        var ships = new List<(int Ship, Detachment D)>();
        foreach (var sq in squadrons)
            for (int i = 0; i < sq.Members.Count; i++)
                ships.Add((sq.Members[i], sq.DetachmentAt(i)));
        f.Clear();
        foreach (var sq in squadrons)
        {
            sq.Parent = 0;
            RemoveGroup(sq);
        }
        f.IsForce = false;
        (f.Formation, f.SpacingM, f.BearingDeg) = (first.Formation, first.SpacingM, first.BearingDeg);
        foreach (var (ship, d) in ships)
        {
            f.Add(ship);
            if (d != Detachment.InLine)
                f.Detach(ship, d);
        }
    }

    Group New(bool force, Formation f, double spacing)
    {
        var g = new Group { GroupId = nextId++, Formation = f, SpacingM = Spacing(spacing), IsForce = force, Owner = this };
        g.Name = force ? $"Force {g.GroupId}" : $"Group {g.GroupId}";
        AddGroup(g);
        return g;
    }

    static Formation Formed(int b) => (uint)b <= (uint)Formation.Bearing ? (Formation)b : throw new ArgumentException($"no formation {b}");

    static Detachment Detached(int b) => (Detachment)b is Detachment.InLine or Detachment.Nudged or Detachment.Pinned or Detachment.Tasked
        ? (Detachment)b : throw new ArgumentException($"no detachment {b}");

    static double Spacing(double m) => m > 0 ? m : 600;

    /// <summary>A group is of one side: a ship joins only its own side's groups.</summary>
    static bool SameSide(World world, int a, int b)
    {
        var s = world.Ships.Store;
        int sa = s.SlotOf(a), sb = s.SlotOf(b);
        return sa >= 0 && sb >= 0 && s[sa].Side == s[sb].Side;
    }

    /// <summary>Any ship of a group, down the tree (its side's), or 0.</summary>
    int AnyShip(Group g)
    {
        while (g.IsForce)
        {
            if (g.Members.Count == 0 || Get(g.Members[0]) is not { } sub)
                return 0;
            g = sub;
        }
        return g.Members.Count > 0 ? g.Members[0] : 0;
    }

    /// <summary>Whether a group is <paramref name="outer"/> or inside it, so it can't take it in.</summary>
    bool Within(Group g, Group outer)
    {
        for (var at = g; ; at = Get(at.Parent)!)
        {
            if (at.GroupId == outer.GroupId)
                return true;
            if (at.Parent == 0)
                return false;
        }
    }

    void Leave(int entity)
    {
        SetStraggling(entity, false);
        for (int i = 0; i < groups.Count; i++)
            if (!groups[i].IsForce && groups[i].Remove(entity))
            {
                if (groups[i].Members.Count == 0)
                {
                    var empty = groups[i];
                    LeaveForce(empty);
                    RemoveGroup(empty);
                }
                return;
            }
    }

    /// <summary>A group out of its force; a force left empty goes, out of its own.</summary>
    void LeaveForce(Group g)
    {
        if (g.Parent == 0 || Get(g.Parent) is not { } f)
        {
            g.Parent = 0;
            return;
        }
        f.Remove(g.GroupId);
        g.Parent = 0;
        if (f.Members.Count == 0)
        {
            LeaveForce(f);
            RemoveGroup(f);
        }
    }

    public void Handle(World world, in SimEvent e)
    {
    }

    public void Hash(ref StateHash h)
    {
        h.Add(nextId);
        h.Add(groups.Count);
        foreach (var g in groups)
        {
            h.Add(g.GroupId);
            h.Add(g.IsForce);
            h.Add(g.Parent);
            h.Add((int)g.Formation);
            if (g.Formation == Formation.Bearing)
                h.Add(g.BearingDeg);   // only then: a world of the four old formations hashes as before
            h.Add(g.SpacingM);
            if (g.WingM > 0)
                h.Add(g.WingM);   // only then: a world without lookouts hashes as before
            h.Add(g.Members.Count);
            for (int i = 0; i < g.Members.Count; i++)
            {
                h.Add(g.Members[i]);
                h.Add((int)g.DetachmentAt(i));
                if (IsStraggling(g.Members[i]))
                    h.Add(-1);   // only then: a world without stragglers hashes as before
            }
        }
    }

    public void Fill(Snapshot s)
    {
        var v = s.Groups;
        int members = 0;
        foreach (var g in groups)
            members += g.Members.Count;
        v.EnsureCapacity(groups.Count, members);
        int n = 0, k = 0;
        foreach (var g in groups)
        {
            int side = SideOf(g);
            if (side >= 0 && !s.ShownSides[side])
                continue;   // the enemy's: not in the player's view
            (v.Id[n], v.Name[n], v.Formation[n], v.SpacingM[n], v.MemberStart[n], v.MemberCount[n]) =
                (g.GroupId, g.Name, g.Formation, g.SpacingM, k, g.Members.Count);
            v.Side[n] = side;
            v.BearingDeg[n] = g.BearingDeg;
            (v.Guide[n], v.GuideShip[n], v.Force[n], v.Parent[n]) = (g.Guide, GuideShip(g), g.IsForce, g.Parent);
            for (int i = 0; i < g.Members.Count; i++)
            {
                v.Detached[k] = g.DetachmentAt(i);
                v.Members[k++] = g.Members[i];
            }
            n++;
        }
        v.Count = n;
    }
}
