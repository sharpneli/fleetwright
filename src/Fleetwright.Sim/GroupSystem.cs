using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>How a group's ships are placed about its guide (the first in its line).</summary>
public enum Formation : byte
{
    /// <summary>Line ahead: each ship in the wake of the one before, a spacing astern.</summary>
    Column,

    /// <summary>Line abreast: each ship a spacing further to starboard of the guide, level with it.</summary>
    LineAbreast,

    /// <summary>Echelon: each ship a spacing further back along a 45° line to starboard, or to port.</summary>
    EchelonStarboard,
    EchelonPort,
}

/// <summary>Whether a member keeps its place in its group's formation (docs/battle/plan.md step 2.6). A detached ship
/// stays a member but is out of the line: it has no station, and its group's rails don't carry it.</summary>
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
}

/// <summary>The world's groups (docs/battle/plan.md step 2.4): ships that move together, in order, with a formation
/// and a spacing. The members not detached are the group's line, and the first of the line is the guide, the ship the
/// group's orders go to. A group is the world's state, not the AI's: the map draws it, its admiral (the AI's layer
/// above captains) keeps it in formation, and the rails clock tier moves its line whole. A ship is in at most one
/// group. Groups are made and changed by commands, so they replay; they hold no ship state of their own. The world
/// registers it second, after the ships (<see cref="Id"/>).</summary>
public sealed class GroupSystem : IWorldSystem
{
    public const byte Id = 2;

    /// <summary>One group: its id, name, formation, spacing in metres, and members by entity in order, each in the line
    /// or detached.</summary>
    public sealed class Group
    {
        public int GroupId { get; internal init; }
        public string Name { get; internal set; } = "";
        public Formation Formation { get; internal set; }
        public double SpacingM { get; internal set; }
        readonly List<int> members = [];
        readonly List<Detachment> detached = [];
        readonly List<int> line = [];
        public IReadOnlyList<int> Members => members;

        /// <summary>The members in the formation, in order: the guide first.</summary>
        public IReadOnlyList<int> Line => line;

        /// <summary>The first member in the line, or 0 when every member is detached.</summary>
        public int Guide => line.Count > 0 ? line[0] : 0;

        public Detachment DetachmentAt(int i) => detached[i];

        /// <summary>A member's detachment (<see cref="Detachment.InLine"/> for a ship that isn't a member).</summary>
        public Detachment DetachmentOf(int entity)
        {
            int i = members.IndexOf(entity);
            return i >= 0 ? detached[i] : Detachment.InLine;
        }

        internal bool Contains(int entity) => members.Contains(entity);

        internal void Add(int entity)
        {
            members.Add(entity);
            detached.Add(Detachment.InLine);
            Relink();
        }

        internal bool Remove(int entity)
        {
            int i = members.IndexOf(entity);
            if (i < 0)
                return false;
            members.RemoveAt(i);
            detached.RemoveAt(i);
            Relink();
            return true;
        }

        internal void Detach(int entity, Detachment d)
        {
            int i = members.IndexOf(entity);
            if (i < 0)
                return;
            detached[i] = d;
            Relink();
        }

        void Relink()
        {
            line.Clear();
            for (int i = 0; i < members.Count; i++)
                if (detached[i] == Detachment.InLine)
                    line.Add(members[i]);
        }
    }

    readonly List<Group> groups = [];   // by id order
    int nextId = 1;

    internal GroupSystem()
    {
    }

    public IReadOnlyList<Group> Groups => groups;

    /// <summary>The groups by index, for loops that mustn't allocate (a foreach over the interface boxes).</summary>
    public int Count => groups.Count;
    public Group this[int i] => groups[i];

    /// <summary>The group a ship is in, or null.</summary>
    public Group? Of(int entity)
    {
        foreach (var g in groups)
            if (g.Contains(entity))
                return g;
        return null;
    }

    public Group? Get(int groupId)
    {
        foreach (var g in groups)
            if (g.GroupId == groupId)
                return g;
        return null;
    }

    /// <summary>Whether a ship is in a group's line: a member, not detached.</summary>
    public bool InLine(int entity) => Of(entity) is { } g && g.DetachmentOf(entity) == Detachment.InLine;

    // ------------------------------------------------------------------ formations

    /// <summary>The station of the i-th ship of a line (<see cref="Group.Line"/>, i from 1): the ship it keeps station
    /// on, and how far astern of it and to its starboard. In column each ship follows the one before it (a turn goes
    /// round in succession, near enough); in line abreast and echelon it's placed on the guide (the group turns
    /// together, the outer ships wheeling).</summary>
    public static (int Target, double AsternM, double AbeamM) StationOf(Formation f, double spacing, IReadOnlyList<int> line, int i)
    {
        var (astern, abeam) = OffsetFromGuide(f, spacing, i);
        return f == Formation.Column ? (line[i - 1], spacing, 0) : (line[0], astern, abeam);
    }

    /// <summary>The i-th ship of a line's station from the guide, with every ship on the guide's heading: how far
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

    // ------------------------------------------------------------------ commands

    /// <summary>A new group, guided by a ship (taken out of any group it was in); its id is the next.</summary>
    public static Command Create(int guide, Formation formation = Formation.Column, double spacingM = 600) =>
        new(Id, (ushort)GroupCommand.Create, guide, B: (int)formation, X: spacingM);

    /// <summary>A ship to the end of a group (out of any other); refused for a ship of another side.</summary>
    public static Command Add(int groupId, int entity) => new(Id, (ushort)GroupCommand.Add, entity, A: groupId);

    /// <summary>A ship to the end of the group another ship is in (out of any other): how a UI adds to a group it has
    /// just formed, before it knows the new group's id. Refused for a ship of another side.</summary>
    public static Command Join(int entity, int alongside) => new(Id, (ushort)GroupCommand.Join, entity, A: alongside);

    /// <summary>A new group of the last ships spawned, in the order they were spawned, the first the guide: how a UI
    /// forms a group of ships it has just spawned, before it knows their ids (sent right after the spawns, it names
    /// exactly them, on every replay too). Ships among them that are gone, or of another side than the first, are left
    /// out.</summary>
    public static Command CreateOfNewest(int count, Formation formation = Formation.Column, double spacingM = 600) =>
        new(Id, (ushort)GroupCommand.CreateOfNewest, A: count, B: (int)formation, X: spacingM);

    /// <summary>A ship out of its group (a guide's place goes to the next ship in the line).</summary>
    public static Command Remove(int entity) => new(Id, (ushort)GroupCommand.Remove, entity);

    /// <summary>A member out of its group's line, or back into it (<see cref="Detachment.InLine"/>). It keeps its place
    /// among the members, so a guide that rejoins guides again. The AI gives these, from what the player's orders mean
    /// (docs/battle/plan.md step 2.6).</summary>
    public static Command Detach(int entity, Detachment d) => new(Id, (ushort)GroupCommand.Detach, entity, B: (int)d);

    public static Command SetFormation(int groupId, Formation formation, double spacingM) =>
        new(Id, (ushort)GroupCommand.SetFormation, A: groupId, B: (int)formation, X: spacingM);

    public static Command Disband(int groupId) => new(Id, (ushort)GroupCommand.Disband, A: groupId);

    public void Apply(World world, in Command c)
    {
        var code = (GroupCommand)c.Code;
        // a group that changes comes off rails whole first (and a ship joining or leaving one with it)
        if (c.Entity != 0)
            world.Ships.Derail(world, c.Entity);
        if (code is GroupCommand.SetFormation or GroupCommand.Disband or GroupCommand.Add && Get((int)c.A) is { Guide: > 0 } ga)
            world.Ships.Derail(world, ga.Guide);
        if (code == GroupCommand.Join && c.A != 0)
            world.Ships.Derail(world, (int)c.A);
        if (code == GroupCommand.Detach && Of(c.Entity) is { Guide: > 0 } gd)
            world.Ships.Derail(world, gd.Guide);   // a ship rejoining is on rails of its own: its line comes off too
        switch (code)
        {
            case GroupCommand.Create:
                if (world.Ships.Store.SlotOf(c.Entity) < 0)
                    return;
                Leave(c.Entity);
                var g = new Group { GroupId = nextId++, Formation = (Formation)c.B, SpacingM = Spacing(c.X) };
                g.Name = $"Group {g.GroupId}";
                g.Add(c.Entity);
                groups.Add(g);
                break;
            case GroupCommand.CreateOfNewest:
                Group? made = null;
                for (int e = Math.Max(world.Ships.NewestEntity - (int)c.A + 1, 1); e <= world.Ships.NewestEntity; e++)
                {
                    if (world.Ships.Store.SlotOf(e) < 0 || made != null && !SameSide(world, e, made.Members[0]))
                        continue;
                    world.Ships.Derail(world, e);
                    Leave(e);
                    if (made == null)
                    {
                        made = new Group { GroupId = nextId++, Formation = (Formation)c.B, SpacingM = Spacing(c.X) };
                        made.Name = $"Group {made.GroupId}";
                        groups.Add(made);
                    }
                    made.Add(e);
                }
                break;
            case GroupCommand.Add:
                if (Get((int)c.A) is not { } to || world.Ships.Store.SlotOf(c.Entity) < 0 || to.Contains(c.Entity) || !SameSide(world, c.Entity, to.Members[0]))
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
                if ((Detachment)c.B is not (Detachment.InLine or Detachment.Nudged or Detachment.Pinned))
                    throw new ArgumentException($"no detachment {c.B}");
                Of(c.Entity)?.Detach(c.Entity, (Detachment)c.B);
                break;
            case GroupCommand.SetFormation:
                if (Get((int)c.A) is { } f)
                    (f.Formation, f.SpacingM) = ((Formation)c.B, Spacing(c.X));
                break;
            case GroupCommand.Disband:
                if (Get((int)c.A) is { } d)
                    groups.Remove(d);
                break;
            default:
                throw new ArgumentException($"unknown group command {c.Code}");
        }
    }

    static double Spacing(double m) => m > 0 ? m : 600;

    /// <summary>A group is of one side: a ship joins only its own side's groups.</summary>
    static bool SameSide(World world, int a, int b)
    {
        var s = world.Ships.Store;
        int sa = s.SlotOf(a), sb = s.SlotOf(b);
        return sa >= 0 && sb >= 0 && s[sa].Side == s[sb].Side;
    }

    void Leave(int entity)
    {
        for (int i = 0; i < groups.Count; i++)
            if (groups[i].Remove(entity) && groups[i].Members.Count == 0)
            {
                groups.RemoveAt(i);
                return;
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
            (v.Id[n], v.Name[n], v.Formation[n], v.SpacingM[n], v.MemberStart[n], v.MemberCount[n], v.Guide[n]) =
                (g.GroupId, g.Name, g.Formation, g.SpacingM, k, g.Members.Count, g.Guide);
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
