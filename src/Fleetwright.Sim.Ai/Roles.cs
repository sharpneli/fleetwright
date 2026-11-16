using System.Runtime.InteropServices;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>What a unit is for (docs/battle/admirals-orders.md O6, decision 4): the job its admiral (the side's commander,
/// or its force's admiral) has it on. The player can set one (<see cref="Heavy"/>, <see cref="Scout"/>,
/// <see cref="Screen"/>, <see cref="Flotilla"/>, <see cref="Auxiliary"/>); else it's allocated with the units beside it
/// (docs/battle/allocation.md: the jobs the task needs, filled from what the admiral has).</summary>
public enum UnitRole : byte
{
    /// <summary>Not to be used: fallen out, held by the player, or gone (a commander's own, at a think). As a set role:
    /// none set, allocated.</summary>
    None,

    /// <summary>The side's main body: the commander's pick at a think (the allocation's, <see cref="Roles.IsMain"/>),
    /// never set.</summary>
    MainBody,

    /// <summary>The line: the strong units at about the slowest's speed, and what's left with nothing else to do.</summary>
    Heavy,

    /// <summary>Scouting, and in battle the fast wing: the strong units with speed to spare, and the units that hold
    /// their own best against the enemy's scouts, as many as the task wants ahead.</summary>
    Scout,

    /// <summary>A torpedo flotilla: screens the line and is sent in to attack. Allocated to units whose torpedoes beat
    /// their guns against the enemy's main body.</summary>
    Flotilla,

    /// <summary>No main battery (a seaplane carrier, a tender): kept out of the fight.</summary>
    Auxiliary,

    /// <summary>Screening: screens the line and counter-attacks, never sent in to attack. Allocated to units fast enough
    /// to keep station whose guns are a match for the enemy's attackers; four battleships set as a screen are one.</summary>
    Screen,
}

/// <summary>The units' jobs (docs/battle/allocation.md AL3): the one place every layer and the map ask what a unit is
/// for. A role set by the player (<see cref="AiCommand.Role"/>, logged) holds; else it's the allocation's. Each admiral
/// (the side's commander over its top units, a force's admiral over his members) allocates his units to the jobs his
/// task needs, by what each brings (<see cref="ForceMeasures"/>), in one cheap pass (<see cref="Allocate"/>). Thought over
/// at the AI's thinks only (<see cref="Refresh"/>: every <see cref="SimSettings.AllocationPeriodMs"/> and whenever the
/// units change), never when the map asks, so a replay allocates alike. AI state: hashed, filled into the snapshot.</summary>
public sealed class Roles(World world, ForceMeasures measures, Doctrine doctrine)
{
    readonly GroupSystem groups = world.Groups;
    readonly ShipStore store = world.Ships.Store;
    readonly SortedDictionary<int, UnitRole> set = [];
    readonly Dictionary<int, UnitRole> allocated = [];
    readonly List<(int Unit, UnitRole Role)> made = new(64);   // the allocation in the order made: what's hashed
    readonly HashSet<int> mains = [];
    long nextMs = long.MinValue;
    long shape = long.MinValue;   // the units as they were at the last allocation
    bool provisional;             // allocated outside a think (a test, the map before the first): the first think does it again
    readonly List<Unit> units = new(64);
    readonly List<(double Ship, double V, double S)> lineUnits = new(64);
    UnitMeasure[] m = new UnitMeasure[64];
    UnitRole[] job = new UnitRole[64];
    readonly List<int> order = new(64);

    internal Tasks.TaskLayer? Tasks { get; set; }
    internal Commanders.CommanderLayer? Commanders { get; set; }
    internal Commanders.AimBook? Book { get; set; }

    /// <summary>The roles the player can set, in the order a picker offers them.</summary>
    public static readonly UnitRole[] Settable = [UnitRole.Heavy, UnitRole.Scout, UnitRole.Screen, UnitRole.Flotilla, UnitRole.Auxiliary];

    /// <summary>A unit's role set (<see cref="UnitRole.None"/>: back to allocated).</summary>
    internal void Set(Unit u, UnitRole role)
    {
        if (role == UnitRole.None)
            set.Remove(u.Id);
        else if (Array.IndexOf(Settable, role) < 0)
            throw new ArgumentException($"not a role to set: {role}");
        else
            set[u.Id] = role;
    }

    /// <summary>Whether the player set a unit's role.</summary>
    public bool IsSet(Unit u) => set.ContainsKey(u.Id);

    /// <summary>A unit's role: the one set, else its admiral's allocation.</summary>
    public UnitRole Of(Unit u)
    {
        if (set.TryGetValue(u.Id, out var r))
            return r;
        Provisional();
        return allocated.TryGetValue(u.Id, out var a) ? a : UnitRole.None;
    }

    /// <summary>Whether a unit is its side's main body (the strongest of the line among the side's top units).</summary>
    public bool IsMain(Unit u)
    {
        Provisional();
        return mains.Contains(u.Id);
    }

    /// <summary>Nothing thought yet (a test, the map's first frame): allocated for now, and again at the first think.</summary>
    void Provisional()
    {
        if (nextMs != long.MinValue || provisional)
            return;
        lock (allocated)
            if (nextMs == long.MinValue && !provisional)
            {
                AllocateAll();
                provisional = true;
            }
    }

    /// <summary>At the AI's think (on the sim's thread, before the layers think): the allocation thought over when it's
    /// due, or the units have changed.</summary>
    internal void Refresh(long nowMs)
    {
        long now = Shape();
        if (nowMs < nextMs && now == shape && !provisional)
            return;
        AllocateAll();
        (nextMs, shape, provisional) = (nowMs + world.Settings.AllocationPeriodMs, now, false);
    }

    /// <summary>The units as they stand, in a number: a change of a group's parent or members, or a ship's coming or
    /// going, changes it.</summary>
    long Shape()
    {
        var h = new StateHash();
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            h.Add(g.GroupId);
            h.Add(g.Parent);
            var members = g.Members;
            for (int i = 0; i < members.Count; i++)   // by index: a foreach over the interface boxes its enumerator
                h.Add(members[i]);
        }
        h.Add(store.Count);
        return (long)h.Value;
    }

    void AllocateAll()
    {
        allocated.Clear();
        made.Clear();
        mains.Clear();
        // each side's top units, by its commander's task
        Span<bool> sides = stackalloc bool[256];
        for (int slot = 0; slot < store.Slots; slot++)
            if (store.IsAlive(slot))
                sides[store[slot].Side] = true;
        for (int side = 0; side < 256; side++)
        {
            if (!sides[side])
                continue;
            units.Clear();
            for (int k = 0; k < groups.Count; k++)
                if (groups[k].Parent == 0 && SideOf(groups[k]) == side)
                    units.Add(Unit.Group(groups[k].GroupId));
            for (int slot = 0; slot < store.Slots; slot++)
                if (store.IsAlive(slot) && store[slot].Side == side && groups.Of(store[slot].Entity) == null)
                    units.Add(Unit.Ship(store[slot].Entity));
            int main = Allocate((byte)side, SideNeeds((byte)side));
            if (main >= 0)
                mains.Add(units[main].Id);
        }
        // each force's members, by the force's task
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            if (!g.IsForce || SideOf(g) is not (>= 0 and var side))
                continue;
            units.Clear();
            var members = g.Members;
            for (int i = 0; i < members.Count; i++)
                if (groups.Get(members[i]) != null)
                    units.Add(Unit.Group(members[i]));
            Allocate((byte)side, ForceNeeds(g));
        }
    }

    /// <summary>What a task needs of the units it has (docs/battle/allocation.md "The jobs, set by the task"): scouts ahead of
    /// its main body, how many; whether its scouting force must be a match for the enemy's; whether it wants a screen
    /// (against the enemy's attackers).</summary>
    readonly record struct Needs(int Scouts, bool Match, bool Screen);

    /// <summary>The side's top units: on passage (a Reach his side pursues: no scouting force) or withdrawing (home), a
    /// screen; else the approach to a fleet action: scouts ahead, a match for the enemy's, and a screen.</summary>
    Needs SideNeeds(byte side)
    {
        var phase = Commanders?.PhaseOf(side).Phase ?? Sim.Ai.Commanders.CommanderPhase.None;
        if (phase is Sim.Ai.Commanders.CommanderPhase.Retiring or Sim.Ai.Commanders.CommanderPhase.Home || Book?.ReachOpen(side) == true)
            return new Needs(0, false, true);
        return new Needs(world.Settings.AllocationScouts, true, true);
    }

    /// <summary>A force's members, by its task: scouting, shadowing, closing the enemy or on its orders, its own scouts
    /// ahead and a screen; searching (its admiral spreads its ships), attacking or screening, none; on passage (going
    /// somewhere, joining, covering, patrolling, withdrawing), a screen.</summary>
    Needs ForceNeeds(GroupSystem.Group g) => (Tasks?.Of(Unit.Group(g.GroupId)).Kind ?? Sim.Ai.Tasks.TaskKind.None) switch
    {
        Sim.Ai.Tasks.TaskKind.Search or Sim.Ai.Tasks.TaskKind.Attack or Sim.Ai.Tasks.TaskKind.Screen => new Needs(0, false, false),
        Sim.Ai.Tasks.TaskKind.GoTo or Sim.Ai.Tasks.TaskKind.Join or Sim.Ai.Tasks.TaskKind.Cover or Sim.Ai.Tasks.TaskKind.Patrol
            or Sim.Ai.Tasks.TaskKind.Withdraw => new Needs(0, false, true),
        _ => new Needs(world.Settings.AllocationScouts, false, true),
    };

    /// <summary>One admiral's units (<see cref="units"/>) to jobs (docs/battle/allocation.md "The allocation"), a greedy
    /// pass; the main body's index, -1 with none:
    /// <list type="number">
    /// <item>No main battery: kept back (<see cref="UnitRole.Auxiliary"/>).</item>
    /// <item>The line: of the armed units that aren't attackers (all of them, if every one is), the strong at about the
    /// slowest strong one's speed (<see cref="ForceMeasures.Line"/>), the strongest its main body.</item>
    /// <item>Scouts: the strong with speed to spare, always (the scouting force, the fast wing in battle); then, for the
    /// positions the task wants ahead, the units faster than the line by the margin that hold their own best against the
    /// enemy's scouts (<see cref="UnitMeasure.Scouting"/>; his line the yardstick, <see cref="UnitMeasure.Quality"/>, with
    /// none), and more while the scouting force is no match for his (doctrine's bar). A boat (<see cref="UnitMeasure.Boat"/>:
    /// an attacker of the boats' size) never scouts.</item>
    /// <item>The rest: attackers to the flotillas; a match for the enemy's attackers, and fast enough to keep station,
    /// to the screen when the task wants one; else with the line.</item>
    /// </list></summary>
    int Allocate(byte side, Needs needs)
    {
        int n = units.Count;
        if (n == 0)
            return -1;
        if (m.Length < n)
            (m, job) = (new UnitMeasure[2 * n], new UnitRole[2 * n]);
        bool anyGun = false;
        for (int i = 0; i < n; i++)
        {
            m[i] = measures.Of(units[i]);
            job[i] = m[i].Armed ? UnitRole.None : UnitRole.Auxiliary;
            anyGun |= m[i].Armed && !m[i].Attacker;
        }
        var s = world.Settings;
        double accept = 1 / Math.Max(doctrine.OfGroup(null, side, DoctrineField.OutgunnedRatio), 1e-6);
        // the line
        lineUnits.Clear();
        for (int i = 0; i < n; i++)
            lineUnits.Add(job[i] == UnitRole.None && (!anyGun || !m[i].Attacker) ? (m[i].ShipStrengthT, m[i].SpeedKn, m[i].StrengthT) : (0, 0, 0));
        Span<bool> strong = stackalloc bool[n], inLine = stackalloc bool[n];
        var (lineKn, main) = ForceMeasures.Line(CollectionsMarshal.AsSpan(lineUnits), strong, inLine, s);
        for (int i = 0; i < n; i++)
            if (inLine[i])
                job[i] = UnitRole.Heavy;
        // the scouts: the fast strong (the scouting force, the fast wing in battle), then the best of the rest to the
        // positions the task wants ahead (for the sight: they're on top of the fast wing)
        double scoutsT = 0;
        int scouts = 0;
        for (int i = 0; i < n; i++)
            if (strong[i] && !inLine[i])
            {
                job[i] = UnitRole.Scout;
                scoutsT += m[i].StrengthT;
            }
        bool enemyScouts = false;
        for (int i = 0; i < n; i++)
            enemyScouts |= m[i].Scouting > 0;
        order.Clear();
        for (int i = 0; i < n; i++)
            if (job[i] == UnitRole.None && main >= 0 && m[i].SpeedKn >= lineKn + s.AllocationSpareKn && !m[i].Boat)
                order.Add(i);
        for (int a = 1; a < order.Count; a++)   // the best first, ties in unit order (a handful: insertion)
        {
            int x = order[a], b = a;
            for (; b > 0 && ScoutWorth(m[order[b - 1]], enemyScouts) < ScoutWorth(m[x], enemyScouts); b--)
                order[b] = order[b - 1];
            order[b] = x;
        }
        double bar = needs.Match ? accept * measures.EnemyScoutsT(side) : 0;
        foreach (int i in order)
        {
            if (scouts >= needs.Scouts && scoutsT >= bar)
                break;
            job[i] = UnitRole.Scout;
            (scoutsT, scouts) = (scoutsT + m[i].StrengthT, scouts + 1);
        }
        // the rest
        for (int i = 0; i < n; i++)
        {
            if (job[i] != UnitRole.None)
                continue;
            job[i] = m[i].Attacker ? UnitRole.Flotilla
                : needs.Screen && m[i].Screening >= accept && m[i].SpeedKn >= lineKn ? UnitRole.Screen
                : UnitRole.Heavy;
        }
        for (int i = 0; i < n; i++)
        {
            allocated[units[i].Id] = job[i];
            made.Add((units[i].Id, job[i]));
        }
        return main;
    }

    /// <summary>How good a scout a unit is: how it holds its own against the enemy's scouts, or with none against his
    /// line.</summary>
    static double ScoutWorth(in UnitMeasure m, bool enemyScouts) => enemyScouts ? m.Scouting : m.Quality;

    int SideOf(GroupSystem.Group g) =>
        store.SlotOf(groups.GuideShip(g)) is int slot and >= 0 ? store[slot].Side : groups.SideOf(g);

    public void Hash(ref StateHash h)
    {
        h.Add(set.Count);
        foreach (var (unit, role) in set)
        {
            h.Add(unit);
            h.Add((int)role);
        }
        h.Add(nextMs);
        h.Add(made.Count);
        foreach (var (unit, role) in made)
        {
            h.Add(unit);
            h.Add((int)role);
            h.Add(mains.Contains(unit));
        }
    }

    public void Fill(Snapshot s)
    {
        var gv = s.Groups;
        for (int k = 0; k < gv.Count; k++)
        {
            var u = Unit.Group(gv.Id[k]);
            (gv.Role[k], gv.RoleSet[k]) = ((byte)Of(u), IsSet(u));
        }
        var v = s.Ships;
        for (int k = 0; k < v.Count; k++)
        {
            var u = Unit.Ship(v.Entity[k]);
            (v.Role[k], v.RoleSet[k]) = groups.Of(v.Entity[k]) != null ? ((byte)0, false) : ((byte)Of(u), IsSet(u));
        }
    }
}
