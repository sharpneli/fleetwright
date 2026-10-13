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

        /// <summary>A force: its members are groups. A squadron's are ships.</summary>
        public bool IsForce { get; internal init; }

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
            int n = 0;
            bool changed = false;
            for (int i = 0; i < members.Count; i++)
            {
                if (detached[i] != Detachment.InLine || IsForce && Owner != null && !Owner.HasLine(members[i]))
                    continue;
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

    /// <summary>The last ship of a group's line, down the tree; 0 if there's none.</summary>
    public int LastShip(Group g)
    {
        if (!g.IsForce)
            return g.Line.Count > 0 ? g.Line[^1] : 0;
        for (int k = g.Line.Count - 1; k >= 0; k--)
            if (Get(g.Line[k]) is { } sub && LastShip(sub) is int s and > 0)
                return s;
        return 0;
    }

    // ------------------------------------------------------------------ formations

    /// <summary>The station of the i-th ship of a squadron's line (<see cref="Group.Line"/>, i from 1): the ship it
    /// keeps station on, and how far astern of it and to its starboard. In column each ship follows the one before it
    /// (a turn goes round in succession, near enough); in line abreast and echelon it's placed on the guide (the group
    /// turns together, the outer ships wheeling).</summary>
    public static (int Target, double AsternM, double AbeamM) StationOf(Formation f, double spacing, IReadOnlyList<int> line, int i)
    {
        var (astern, abeam) = OffsetFromGuide(f, spacing, i);
        return f == Formation.Column ? (line[i - 1], spacing, 0) : (line[0], astern, abeam);
    }

    /// <summary>The station of the i-th group of a force's line (i from 1), for its guide ship: the ship it keeps
    /// station on, and how far astern of it and to its starboard. In column it follows the last ship of the group
    /// before (one line); in line abreast and echelon it's placed on the force's guide ship.</summary>
    public (int Target, double AsternM, double AbeamM) StationOfGroup(Group force, int i)
    {
        var line = force.Line;
        if (force.Formation == Formation.Column)
            return (Get(line[i - 1]) is { } before ? LastShip(before) : 0, force.SpacingM, 0);
        var (astern, abeam) = OffsetFromGuide(force.Formation, force.SpacingM, i);
        return (GuideShip(force), astern, abeam);
    }

    /// <summary>The i-th member of a line's station from the guide, with every ship on the guide's heading: how far
    /// astern and to starboard.</summary>
    public static (double AsternM, double AbeamM) OffsetFromGuide(Formation f, double spacing, int i)
    {
        const double Diag = 0.70710678118654752;
        return f switch
        {
            Formation.Column => (i * spacing, 0),
            Formation.LineAbreast => (0, i * spacing),
            Formation.EchelonStarboard => (i * spacing * Diag, i * spacing * Diag),
            Formation.EchelonPort => (i * spacing * Diag, -i * spacing * Diag),
            _ => throw new ArgumentOutOfRangeException(nameof(f)),
        };
    }

    /// <summary>Every ship of a group's line, down the tree, with its station from the group's guide ship (which is
    /// first, at 0, 0), every ship on the guide's heading: the one geometry the rails, the admirals and the tests read.
    /// A force's stations are its groups' guides' (<see cref="StationOfGroup"/>), its groups' own added on.</summary>
    public void LineOffsets(Group g, List<(int Ship, double AsternM, double AbeamM)> into)
    {
        into.Clear();
        Offsets(g, 0, 0, into);
    }

    void Offsets(Group g, double astern0, double abeam0, List<(int Ship, double AsternM, double AbeamM)> into)
    {
        var line = g.Line;
        if (!g.IsForce)
        {
            for (int i = 0; i < line.Count; i++)
            {
                var (a, b) = OffsetFromGuide(g.Formation, g.SpacingM, i);
                into.Add((line[i], astern0 + a, abeam0 + b));
            }
            return;
        }
        for (int k = 0; k < line.Count; k++)
        {
            if (Get(line[k]) is not { } sub)
                continue;
            var (a, b) = GroupStation(g.Formation, g.SpacingM, k, (astern0, abeam0), into.Count > 0 ? (into[^1].AsternM, into[^1].AbeamM) : null);
            Offsets(sub, a, b, into);
        }
    }

    /// <summary>Where the k-th group of a force's line puts its guide ship (k from 0), about the guide ship of the
    /// whole line, given the force's own guide ship's place there and the last ship placed before it (null: none):
    /// in line ahead a spacing astern of that last ship, else the force's offset for k. The one rule
    /// <see cref="LineOffsets"/> and a scenario's placing (spawning a tree of groups on their stations) share.</summary>
    public static (double AsternM, double AbeamM) GroupStation(Formation f, double spacing, int k, (double AsternM, double AbeamM) forceGuide,
        (double AsternM, double AbeamM)? lastBefore)
    {
        if (k == 0)
            return forceGuide;
        if (f == Formation.Column)
            return lastBefore is { } last ? (last.AsternM + spacing, last.AbeamM) : forceGuide;   // astern of the last ship before
        var (oa, ob) = OffsetFromGuide(f, spacing, k);
        return (forceGuide.AsternM + oa, forceGuide.AbeamM + ob);
    }

    // ------------------------------------------------------------------ commands

    /// <summary>A new squadron, guided by a ship (taken out of any squadron it was in); its id is the next.</summary>
    public static Command Create(int guide, Formation formation = Formation.Column, double spacingM = 600) =>
        new(Id, (ushort)GroupCommand.Create, guide, B: (int)formation, X: spacingM);

    /// <summary>A ship to the end of a squadron (out of any other); refused for a ship of another side.</summary>
    public static Command Add(int groupId, int entity) => new(Id, (ushort)GroupCommand.Add, entity, A: groupId);

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

    public static Command SetFormation(int groupId, Formation formation, double spacingM) =>
        new(Id, (ushort)GroupCommand.SetFormation, A: groupId, B: (int)formation, X: spacingM);

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
                if (Get((int)c.A) is not { IsForce: false } to || world.Ships.Store.SlotOf(c.Entity) < 0 || to.Contains(c.Entity) || !SameSide(world, c.Entity, to.Members[0]))
                    return;
                Leave(c.Entity);
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
                    (f.Formation, f.SpacingM) = ((Formation)c.B, Spacing(c.X));
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

    Group New(bool force, Formation f, double spacing)
    {
        var g = new Group { GroupId = nextId++, Formation = f, SpacingM = Spacing(spacing), IsForce = force, Owner = this };
        g.Name = force ? $"Force {g.GroupId}" : $"Group {g.GroupId}";
        AddGroup(g);
        return g;
    }

    static Detachment Detached(int b) => (Detachment)b is Detachment.InLine or Detachment.Nudged or Detachment.Pinned
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
            h.Add(g.SpacingM);
            h.Add(g.Members.Count);
            for (int i = 0; i < g.Members.Count; i++)
            {
                h.Add(g.Members[i]);
                h.Add((int)g.DetachmentAt(i));
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
            if (SideOf(g) is int side and >= 0 && !s.ShownSides[side])
                continue;   // the enemy's: not in the player's view
            (v.Id[n], v.Name[n], v.Formation[n], v.SpacingM[n], v.MemberStart[n], v.MemberCount[n]) =
                (g.GroupId, g.Name, g.Formation, g.SpacingM, k, g.Members.Count);
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
