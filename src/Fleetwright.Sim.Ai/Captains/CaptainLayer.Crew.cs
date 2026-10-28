using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>Where her men stand: the captain's order (docs/battle/crew.md N3a, decisions 1-2 and 12), at each think of a
/// ship fighting or hurt, by her side's doctrine and his temper; the player picks the doctrine, not the men.
/// <list type="bullet">
/// <item><b>Posture.</b> The main battery, the turrets, the conn, the directors and the stokehold are always manned. A
/// secondary's casemated and open guns by <see cref="DoctrineField.ManCasemates"/> and <see cref="DoctrineField.ManOpenGuns"/>
/// (<see cref="ManGuns"/>): always; while an enemy in sight is in the battery's reach and in the mount's arcs; or only for
/// torpedo craft so, a watch of two guns a battery closed up meanwhile. A wrecked gun's men go under cover. His temper
/// moves the rule a step: a timid captain the stricter one, a bold one the guns manned.</item>
/// <item><b>Reliefs.</b> A station short of its men (with those on their way) is filled in order: the conn and the
/// steering, then the main battery's guns and their handling rooms, then the secondaries' (the secondaries first when
/// torpedo craft are in reach); from the men under cover, the wrecked guns' crews and their handling men, the parties
/// standing by (lent: back to them for a job), and the stokers the speed asked doesn't need. A live mount's handling room
/// is kept, unless her doctrine keeps its reserve in the supply (<see cref="ReliefOrder.ReserveInSupply"/>: the supply's
/// spare men first). They wait for a lull while a station's duress is over their nerve (N3b,
/// <see cref="Steadiness.ReliefNerve"/>), unless the captain is bold.</item>
/// <item>He looks it over every <see cref="SimSettings.CrewPlanS"/> a ship.</item>
/// </list></summary>
public sealed partial class CaptainLayer
{
    // the enemies in sight within the widest reach this think: bearing off the bow, range, a torpedo craft
    (double Rel, double Range, bool Small)[] crewEnemies = new (double, double, bool)[64];
    int crewEnemyCount;
    int[] crewTargets = new int[64];   // the stations to fill, in order

    /// <summary>The reliefs of every ship hurt that isn't fighting (those are the fire pass's), captain or not: a lull after
    /// the action is when her guns are manned again (Southampton's stokers).</summary>
    void HurtPass(in AiContext ctx)
    {
        var store = ctx.Ships;
        hurtAny = false;
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot))
                continue;
            var k = store.Combat.Condition(slot);
            if (k.CrewLost <= 0 || Fighting && (encounters.Of(slot, store[slot].Entity) != 0 || store.Combat.Engaged(slot) > 0))
                continue;   // a fighting ship's are the fire pass's
            Crew(ctx, store[slot], slot, fighting: false);
        }
    }

    bool hurtAny;   // a ship hurt as of the passes: her captain thinks for her reliefs

    /// <summary>Whether any ship afloat has men out of action (her captain thinks for her reliefs): as the passes last saw
    /// it, or while none was, by a look over the ships (a first hit outside a fight).</summary>
    bool Hurt()
    {
        if (hurtAny)
            return true;
        if (Store is not { } store)
            return false;
        for (int slot = 0; slot < store.Slots; slot++)
            if (store.IsAlive(slot) && store.Combat.Condition(slot) is { CrewLost: > 0, Status: ShipStatus.Afloat })
                return true;
        return false;
    }

    /// <summary>Her men where her captain wants them (a ship fighting: her posture; hurt: her reliefs).</summary>
    void Crew(in AiContext ctx, ShipState own, int slot, bool fighting)
    {
        var k = ctx.Ships.Combat.Condition(slot);
        if (k.Status != ShipStatus.Afloat || k.ClosedUp.Length == 0)
            return;
        var cls = own.Handling.Class;
        _ = PlanOf(cls);   // made at her first look (her class's stations, the scratch sized for them), not when she's first hurt
        if (crewDueMs.Length <= slot)
            Array.Resize(ref crewDueMs, Math.Max(slot + 1, crewDueMs.Length * 2));
        if (ctx.NowMs < crewDueMs[slot])
        {
            hurtAny |= k.CrewLost > 0;
            return;   // looked over lately
        }
        crewDueMs[slot] = ctx.NowMs + (long)(ctx.Settings.CrewPlanS * 1000);
        bool small = false;
        if (fighting)
            small = Posture(ctx, own, slot, cls, k);
        if (k.CrewLost > 0)
        {
            hurtAny = true;
            Relieve(ctx, own, slot, cls, k, small);
        }
    }

    /// <summary>Each secondary mount closed up or under cover by doctrine and temper; whether torpedo craft are in reach.</summary>
    bool Posture(in AiContext ctx, ShipState own, int slot, ShipClass cls, ShipCondition k)
    {
        var reach = Reaches(ctx, ctx.Ships.Combat.Loadout(slot));
        Enemies(ctx, own, reach[^1]);
        bool small = false;
        for (int i = 0; i < crewEnemyCount; i++)
            small |= crewEnemies[i].Small;
        double temper = doctrine.Of(own.Entity, own.Side, DoctrineField.Temper);
        int shift = temper <= -0.5 ? 1 : temper >= 0.5 ? -1 : 0;
        var casemates = (ManGuns)Math.Clamp((int)doctrine.Of(own.Entity, own.Side, DoctrineField.ManCasemates) + shift, 0, 2);
        var open = (ManGuns)Math.Clamp((int)doctrine.Of(own.Entity, own.Side, DoctrineField.ManOpenGuns) + shift, 0, 2);
        Span<int> watch = stackalloc int[Math.Min(cls.Batteries.Length, 64)];
        long closed = 0, change = 0;
        for (int m = 0; m < cls.Mounts.Length; m++)
        {
            if (m > 0 && m % 63 == 0)
            {
                if (change != 0)
                    ctx.Issue(DamageSystem.Posture(own.Entity, m - 63, closed, change));   // one order for each 63 mounts
                (closed, change) = (0, 0);   // the next 63's bits, none carried over
            }
            ref readonly var row = ref cls.Mounts[m];
            int b = row.Battery;
            bool want;
            if (b < 0 || cls.Batteries[b].Chief || cls.Enclosed(m) || row.Kind == ComponentKind.Torpedo)
                want = true;
            else if (!k.MountSound[m])
                want = false;
            else
            {
                want = (row.Casemate ? casemates : open) switch
                {
                    ManGuns.Always => true,
                    ManGuns.InRange => Bears(cls, row, reach[b], smallOnly: false),
                    _ => Bears(cls, row, reach[b], smallOnly: true) || b < watch.Length && watch[b]++ < 2,
                };
            }
            int bit = m % 63;
            closed |= want ? 1L << bit : 0;
            change |= want != k.ClosedUp[m] ? 1L << bit : 0;
        }
        if (change != 0)
            ctx.Issue(DamageSystem.Posture(own.Entity, cls.Mounts.Length - 1 - (cls.Mounts.Length - 1) % 63, closed, change));
        return small;
    }

    /// <summary>Whether a mount bears on an enemy (a torpedo craft only) within its battery's reach.</summary>
    bool Bears(ShipClass cls, in MountRow row, double reach, bool smallOnly)
    {
        for (int i = 0; i < crewEnemyCount; i++)
        {
            var (rel, range, small) = crewEnemies[i];
            if (range <= reach && (small || !smallOnly) && CombatStore.InArcs(cls, row, rel))
                return true;
        }
        return false;
    }

    // her side's enemies in sight this think (by side: when worked out, how many, each's position and whether a torpedo
    // craft), worked out once a side a think for all its ships
    readonly long[] sideSeenMs = new long[256];
    readonly int[] sideSeenCount = new int[256];
    readonly (Vec3d At, bool Small)[]?[] sideSeen = new (Vec3d, bool)[]?[256];

    /// <summary>The enemies her side holds in sight within a range of her.</summary>
    void Enemies(in AiContext ctx, ShipState own, double within)
    {
        crewEnemyCount = 0;
        int side = own.Side;
        if (sideSeenMs[side] != ctx.NowMs + 1)
        {
            (sideSeenMs[side], sideSeenCount[side]) = (ctx.NowMs + 1, 0);
            var pic = ctx.World.Sightings.Of(own.Side);
            if (pic != null)
                for (int i = 0; i < pic.Count; i++)
                {
                    if (!pic[i].InSight || pic.Lost(pic[i].Entity) || !ctx.World.Relations.Hostile(own.Side, pic[i].Side))
                        continue;
                    var list = sideSeen[side] ??= new (Vec3d, bool)[64];
                    if (sideSeenCount[side] == list.Length)
                        Array.Resize(ref sideSeen[side], list.Length * 2);
                    sideSeen[side]![sideSeenCount[side]++] = (pic.EstimateAt(i, ctx.NowMs, ctx.Settings).Position, pic[i].Type is ShipType.Small or ShipType.Destroyer);
                }
        }
        var p = own.Position;
        var seen = sideSeen[side];
        for (int i = 0; i < sideSeenCount[side]; i++)
        {
            var (q, small) = seen![i];
            double range = (q - p).Length;
            if (range > within)
                continue;
            if (crewEnemyCount == crewEnemies.Length)
                Array.Resize(ref crewEnemies, crewEnemies.Length * 2);
            crewEnemies[crewEnemyCount++] = (Earth.Bearing(p, q) - own.HeadingDeg, range, small);
        }
    }

    /// <summary>A class's stations a captain refills and the places his spare men come from, worked out once a class: the
    /// conn's blocks and the steering, each mount's gun and handling groups, its stokehold's cells and their crews.</summary>
    sealed class CrewPlan
    {
        public int[] Conn = [];
        public (int Mount, int Gun, int Handling, bool Chief)[] Mounts = [];
        public (int Group, int Crew)[] Stokers = [];
    }

    readonly Dictionary<ShipClass, CrewPlan> crewPlans = [];
    long[] crewDueMs = [];   // by slot: when her captain next looks over where her men stand

    CrewPlan PlanOf(ShipClass cls)
    {
        if (crewPlans.TryGetValue(cls, out var plan))
            return plan;
        var conn = new List<int>();
        for (int i = 0; i < cls.Components.Length; i++)
            if (cls.Components[i].Kind == ComponentKind.Superstructure && cls.Components[i].BattleCrew > 0 &&
                cls.Components[i].Role is ComponentRole.Bridge or ComponentRole.Island)
                conn.Add(Casualties.Part(i));
        var stokers = new List<(int, int)>();
        for (int i = 0; i < cls.Cells.Length; i++)
        {
            int g = Casualties.Cell(cls, i);
            if (cls.Cells[i].BattleCrew <= 0)
                continue;
            if (BattleStations.Of(cls, g) == BattleStation.Steering)
                conn.Add(g);
            else if (cls.Cells[i].Room >= 0 && cls.Rooms[cls.Cells[i].Room].Kind == RoomKind.BoilerRoom)
                stokers.Add((g, cls.Cells[i].BattleCrew));
        }
        var mounts = new List<(int, int, int, bool)>();
        for (int m = 0; m < cls.Mounts.Length; m++)
        {
            ref readonly var row = ref cls.Mounts[m];
            if (row.Kind == ComponentKind.Torpedo)
                continue;
            mounts.Add((m, Casualties.Part(row.Component), row.Barbette >= 0 ? Casualties.Part(row.Barbette) : -1,
                row.Battery >= 0 && cls.Batteries[row.Battery].Chief));
        }
        // the scratch big enough for her: every station a target, every mount, party and stokehold a source
        int targets = conn.Count + 2 * mounts.Count, sources = 3 * mounts.Count + ShipCondition.MaxParties + stokers.Count;
        if (crewTargets.Length < targets)
            Array.Resize(ref crewTargets, targets);
        if (crewSources.Length < sources)
            Array.Resize(ref crewSources, sources);
        return crewPlans[cls] = new CrewPlan { Conn = [.. conn], Mounts = [.. mounts], Stokers = [.. stokers] };
    }

    /// <summary>Her stations short of men filled in order from her spare men (see the class): the spare men gathered once,
    /// then shared down the stations in order.</summary>
    void Relieve(in AiContext ctx, ShipState own, int slot, ShipClass cls, ShipCondition k, bool small)
    {
        var plan = PlanOf(cls);
        int n = 0;
        foreach (int g in plan.Conn)
            AddTarget(ref n, g);
        // the guns closed up and sound, and their handling rooms: the main battery first, unless torpedo craft are near
        for (int pass = 0; pass < 2; pass++)
            foreach (var (m, gun, handling, chief) in plan.Mounts)
            {
                if (chief != (pass == (small ? 1 : 0)) || !k.ClosedUp[m] || !k.MountSound[m])
                    continue;
                AddTarget(ref n, gun);
                if (handling >= 0)
                    AddTarget(ref n, handling);
            }
        int sources = -1;   // gathered when a station first needs them
        bool bold = doctrine.Of(own.Entity, own.Side, DoctrineField.Temper) >= 0.5;
        double nerve = Steadiness.ReliefNerve(k, ctx.Settings);
        for (int t = 0; t < n; t++)
        {
            int g = crewTargets[t];
            int need = g < cls.Components.Length ? cls.Components[g].BattleCrew : cls.Cells[g - cls.Components.Length].BattleCrew;
            int want = need - Casualties.Men(cls, k, g) - Reliefs.Inbound(k, g);
            if (want < 1 || !bold && Steadiness.Duress(cls, k, g, ctx.NowMs, ctx.Settings) > nerve)
                continue;   // full, or under fire there: they wait for a lull
            if (sources < 0)
                sources = Sources(ctx, own, cls, k, plan);
            bool supply = g < cls.Components.Length && cls.Components[g].Kind == ComponentKind.Barbette;
            for (int i = 0; i < sources && want > 0; i++)
            {
                ref var src = ref crewSources[i];
                if (src.Men <= 0 || src.Group == g || supply && src.Supply)
                    continue;
                int men = Math.Min(src.Men, want);
                ctx.Issue(DamageSystem.Relieve(own.Entity, src.Source, g, men));
                (src.Men, want) = (src.Men - men, want - men);
            }
        }
    }

    (long Source, int Group, int Men, bool Supply)[] crewSources = new (long, int, int, bool)[64];

    void AddTarget(ref int n, int g)
    {
        if (n == crewTargets.Length)
            Array.Resize(ref crewTargets, n * 2);
        crewTargets[n++] = g;
    }

    /// <summary>Her spare men in the order they're sent, as many as each source has: the supply's spare first by doctrine
    /// (the USN's bill: a live mount's handling men over one a barrel; never to a handling room), the men under cover, the
    /// wrecked guns' crews and their handling men, the parties standing by, the stokers the speed asked doesn't need.</summary>
    int Sources(in AiContext ctx, ShipState own, ShipClass cls, ShipCondition k, CrewPlan plan)
    {
        int n = 0;
        void Add(long source, int group, int men, bool supply = false)
        {
            if (men <= 0)
                return;
            if (n == crewSources.Length)
                Array.Resize(ref crewSources, n * 2);
            crewSources[n++] = (source, group, men, supply);
        }
        if ((ReliefOrder)(int)doctrine.Of(own.Entity, own.Side, DoctrineField.ReliefOrder) == ReliefOrder.ReserveInSupply)
            for (int m = 0; m < cls.Mounts.Length; m++)
            {
                ref readonly var row = ref cls.Mounts[m];
                if (row.Barbette < 0 || !k.MountSound[m])
                    continue;
                int g = Casualties.Part(row.Barbette);
                Add(Reliefs.Source(Reliefs.From.Station, g), g, Casualties.Men(cls, k, g) - Math.Max(cls.TurretTypes[row.Type].Barrels, 1), supply: true);
            }
        for (int m = 0; m < cls.Mounts.Length; m++)
            Add(Reliefs.Source(Reliefs.From.Covered, m), -1, k.Covered[m]);
        for (int m = 0; m < cls.Mounts.Length; m++)
        {
            ref readonly var row = ref cls.Mounts[m];
            if (k.MountSound[m])
                continue;
            int gun = Casualties.Part(row.Component);
            Add(Reliefs.Source(Reliefs.From.Station, gun), gun, Casualties.Men(cls, k, gun));
            if (row.Barbette >= 0)
                Add(Reliefs.Source(Reliefs.From.Station, Casualties.Part(row.Barbette)), Casualties.Part(row.Barbette), Casualties.Men(cls, k, Casualties.Part(row.Barbette)));
        }
        for (int p = 0; p < k.PartyCount; p++)
        {
            ref readonly var party = ref k.Parties[p];
            if (party.Mustered && party.Target < 0 && party.Job == RepairJob.None && k.PartyLent[p] == 0)
                Add(Reliefs.Source(Reliefs.From.Party, p), -1, (int)party.Men);
        }
        double top = cls.Motion.MaxSpeedKn, power = top > 0 ? Math.Pow(Math.Min(own.SpeedOrderKn / top, 1), 3) : 1;
        double floor = ctx.Settings.StokeholdFloor, keep = Math.Clamp((power - floor) / (1 - floor), 0, 1);
        foreach (var (g, crew) in plan.Stokers)
            Add(Reliefs.Source(Reliefs.From.Station, g), g, Casualties.Men(cls, k, g) - (int)Math.Ceiling(keep * crew));
        return n;
    }
}
