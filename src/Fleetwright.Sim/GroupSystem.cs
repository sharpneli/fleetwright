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
    CreateOfNewest,
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

    // ------------------------------------------------------------------ formations

    /// <summary>Member i's station: the ship it keeps station on, and how far astern of it and to its starboard. In
    /// column each ship follows the one before it (a turn goes round in succession, near enough); in line abreast and
    /// echelon it's placed on the guide (the group turns together, the outer ships wheeling).</summary>
    public static (int Target, double AsternM, double AbeamM) StationOf(Formation f, double spacing, IReadOnlyList<int> members, int i)
    {
        var (astern, abeam) = OffsetFromGuide(f, spacing, i);
        return f == Formation.Column ? (members[i - 1], spacing, 0) : (members[0], astern, abeam);
    }

    /// <summary>Member i's station from the guide, with every ship on the guide's heading: how far astern and to
    /// starboard.</summary>
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

    /// <summary>A ship out of its group (a guide's place goes to the next ship).</summary>
    public static Command Remove(int entity) => new(Id, (ushort)GroupCommand.Remove, entity);

    public static Command SetFormation(int groupId, Formation formation, double spacingM) =>
        new(Id, (ushort)GroupCommand.SetFormation, A: groupId, B: (int)formation, X: spacingM);

    public static Command Disband(int groupId) => new(Id, (ushort)GroupCommand.Disband, A: groupId);

    public void Apply(World world, in Command c)
    {
        // a group that changes comes off rails whole first (and a ship joining or leaving one with it)
        if (c.Entity != 0)
            world.Ships.Derail(world, c.Entity);
        if ((GroupCommand)c.Code is GroupCommand.SetFormation or GroupCommand.Disband or GroupCommand.Add && Get((int)c.A) is { Guide: > 0 } ga)
            world.Ships.Derail(world, ga.Guide);
        if ((GroupCommand)c.Code == GroupCommand.Join && c.A != 0)
            world.Ships.Derail(world, (int)c.A);
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
            case GroupCommand.CreateOfNewest:
                Group? made = null;
                for (int e = Math.Max(world.Ships.NewestEntity - (int)c.A + 1, 1); e <= world.Ships.NewestEntity; e++)
                {
                    if (world.Ships.Store.SlotOf(e) < 0 || made != null && !SameSide(world, e, made.Guide))
                        continue;
                    world.Ships.Derail(world, e);
                    Leave(e);
                    if (made == null)
                    {
                        made = new Group { GroupId = nextId++, Formation = (Formation)c.B, SpacingM = Spacing(c.X) };
                        made.Name = $"Group {made.GroupId}";
                        groups.Add(made);
                    }
                    made.members.Add(e);
                }
                break;
            case GroupCommand.Add:
                if (Get((int)c.A) is not { } to || world.Ships.Store.SlotOf(c.Entity) < 0 || to.members.Contains(c.Entity) || !SameSide(world, c.Entity, to.Guide))
                    return;
                Leave(c.Entity);
                to.members.Add(c.Entity);
                break;
            case GroupCommand.Join:
                if (Of((int)c.A) is not { } with || world.Ships.Store.SlotOf(c.Entity) < 0 || with.members.Contains(c.Entity) || !SameSide(world, c.Entity, with.Guide))
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
