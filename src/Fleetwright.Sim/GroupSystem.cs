using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>How a group's ships are placed about its guide (the first member).</summary>
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

/// <summary>What the group system understands (<see cref="GroupSystem"/>; its commands are built by its statics).</summary>
public enum GroupCommand : ushort
{
    Create = 1,
    Add,
    Remove,
    SetFormation,
    Disband,
    Join,
}

/// <summary>The world's groups (docs/battle/plan.md step 2.4): ships that move together, in order (the first is the
/// guide, the ship the group's orders go to), with a formation and a spacing. A group is the world's state, not the
/// AI's: the map draws it, its admiral (the AI's layer above captains) keeps it in formation, and later the rails
/// clock tier moves it whole. A ship is in at most one group. Groups are made and changed by commands, so they replay;
/// they hold no ship state of their own. The world registers it second, after the ships (<see cref="Id"/>).</summary>
public sealed class GroupSystem : IWorldSystem
{
    public const byte Id = 2;

    /// <summary>One group: its id, name, formation, spacing in metres, and members by entity, the guide first.</summary>
    public sealed class Group
    {
        public int GroupId { get; internal init; }
        public string Name { get; internal set; } = "";
        public Formation Formation { get; internal set; }
        public double SpacingM { get; internal set; }
        internal readonly List<int> members = [];
        public IReadOnlyList<int> Members => members;
        public int Guide => members.Count > 0 ? members[0] : 0;
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
            if (g.members.Contains(entity))
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

    // ------------------------------------------------------------------ commands

    /// <summary>A new group, guided by a ship (taken out of any group it was in); its id is the next.</summary>
    public static Command Create(int guide, Formation formation = Formation.Column, double spacingM = 600) =>
        new(Id, (ushort)GroupCommand.Create, guide, B: (int)formation, X: spacingM);

    /// <summary>A ship to the end of a group (out of any other).</summary>
    public static Command Add(int groupId, int entity) => new(Id, (ushort)GroupCommand.Add, entity, A: groupId);

    /// <summary>A ship to the end of the group another ship is in (out of any other): how a UI adds to a group it has
    /// just formed, before it knows the new group's id.</summary>
    public static Command Join(int entity, int alongside) => new(Id, (ushort)GroupCommand.Join, entity, A: alongside);

    /// <summary>A ship out of its group (a guide's place goes to the next ship).</summary>
    public static Command Remove(int entity) => new(Id, (ushort)GroupCommand.Remove, entity);

    public static Command SetFormation(int groupId, Formation formation, double spacingM) =>
        new(Id, (ushort)GroupCommand.SetFormation, A: groupId, B: (int)formation, X: spacingM);

    public static Command Disband(int groupId) => new(Id, (ushort)GroupCommand.Disband, A: groupId);

    public void Apply(World world, in Command c)
    {
        switch ((GroupCommand)c.Code)
        {
            case GroupCommand.Create:
                if (world.Ships.Store.SlotOf(c.Entity) < 0)
                    return;
                Leave(c.Entity);
                var g = new Group { GroupId = nextId++, Formation = (Formation)c.B, SpacingM = Spacing(c.X) };
                g.Name = $"Group {g.GroupId}";
                g.members.Add(c.Entity);
                groups.Add(g);
                break;
            case GroupCommand.Add:
                if (Get((int)c.A) is not { } to || world.Ships.Store.SlotOf(c.Entity) < 0 || to.members.Contains(c.Entity))
                    return;
                Leave(c.Entity);
                to.members.Add(c.Entity);
                break;
            case GroupCommand.Join:
                if (Of((int)c.A) is not { } with || world.Ships.Store.SlotOf(c.Entity) < 0 || with.members.Contains(c.Entity))
                    return;
                Leave(c.Entity);
                with.members.Add(c.Entity);
                break;
            case GroupCommand.Remove:
                Leave(c.Entity);
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

    void Leave(int entity)
    {
        for (int i = 0; i < groups.Count; i++)
            if (groups[i].members.Remove(entity) && groups[i].members.Count == 0)
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
            h.Add(g.members.Count);
            foreach (int m in g.members)
                h.Add(m);
        }
    }

    public void Fill(Snapshot s)
    {
        var v = s.Groups;
        int members = 0;
        foreach (var g in groups)
            members += g.members.Count;
        v.EnsureCapacity(groups.Count, members);
        int n = 0, k = 0;
        foreach (var g in groups)
        {
            (v.Id[n], v.Name[n], v.Formation[n], v.SpacingM[n], v.MemberStart[n], v.MemberCount[n]) =
                (g.GroupId, g.Name, g.Formation, g.SpacingM, k, g.members.Count);
            foreach (int m in g.members)
                v.Members[k++] = m;
            n++;
        }
        v.Count = n;
    }
}
