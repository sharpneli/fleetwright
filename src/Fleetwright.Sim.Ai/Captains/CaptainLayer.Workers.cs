using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>The captains on the workers (docs/battle/workers.md W13): a think's due ships are a job, a ship an item (her
/// captain's think, her men's reliefs, her guns, her end, her held lights, in that order), each reading the world as it
/// stood at the tick and writing only her captain's record and her chunk's outbox, applied after in chunk order; so the
/// same on any number of workers. Before it, on the sim thread: the captains made that the items need, the grids built,
/// the caches warmed; after it, the waits collected and the counts made again.</summary>
public sealed partial class CaptainLayer
{
    /// <summary>A captain's scratch while he thinks, one per worker lane: what his methods hand each other within his
    /// think (his candidates, his reaches, his crew's lists), never kept from one think to the next but the threat memo.</summary>
    sealed class Mind
    {
        public (double Rel, double Range, bool Small)[] CrewEnemies = new (double, double, bool)[64];
        public int CrewEnemyCount;
        public int[] CrewTargets = new int[64];
        public (long Source, int Group, int Men, bool Supply)[] CrewSources = new (long, int, int, bool)[64];
        public bool CandDark;
        public int[] CandEntity = new int[32], CandRank = new int[32], CandSlot = new int[32], CandPic = new int[32];
        public double[] CandRange = new double[32], CandLead = new double[32], CandBearing = new double[32], CandDone = new double[32];
        public double[] CandMatters = new double[32], CandWant = new double[32], CandClosing = new double[32];
        public Vec3d[] CandPos = new Vec3d[32];
        public sbyte[] CandFoul = new sbyte[32];
        public int CandCount;
        public Unknowns CandUnknowns;
        public double CandBold, CandFinish;
        public int CandWanted, CandUnknown;
        public double CandUnknownM;
        public double[] Reaches = new double[16];
        public int[] OwnTargets = new int[8], HeldTargets = new int[8];
        public readonly MatchupSide ShellTarget = new(), ThreatFrom = new(), ThreatTo = new();
        public int ThreatOwn;
        public long ThreatMs = -1;
    }

    Mind[] minds = [new()];
    Mind M => minds[Workers.Lane];

    // the scratch his methods share, as they were fields before the workers: his lane's
    ref (double Rel, double Range, bool Small)[] crewEnemies => ref M.CrewEnemies;
    ref int crewEnemyCount => ref M.CrewEnemyCount;
    ref int[] crewTargets => ref M.CrewTargets;
    ref (long Source, int Group, int Men, bool Supply)[] crewSources => ref M.CrewSources;
    ref bool candDark => ref M.CandDark;   // darkened in the dark, as of this ship's think (docs/battle/night.md Q9)
    // a ship's candidates, found once for all its batteries: each contact in sight within its widest reach, its entity
    // and slot, range (m), the range it'll be at FireLeadS on (m), bearing from ahead and the rank of its type for a
    // secondary battery; how much each still matters as a target (BL2a: quiet and making away, down to doctrine's
    // Finish), and how much his side's aim wants her (docs/battle/aims.md A3: what it wants sunk, or one that endangers
    // him; 1 for neither)
    ref int[] candEntity => ref M.CandEntity;
    ref int[] candRank => ref M.CandRank;
    ref int[] candSlot => ref M.CandSlot;
    ref int[] candPic => ref M.CandPic;
    ref double[] candRange => ref M.CandRange;
    ref double[] candLead => ref M.CandLead;
    ref double[] candBearing => ref M.CandBearing;
    ref double[] candDone => ref M.CandDone;
    ref double[] candMatters => ref M.CandMatters;
    ref double[] candWant => ref M.CandWant;
    ref double[] candClosing => ref M.CandClosing;
    ref Vec3d[] candPos => ref M.CandPos;
    ref sbyte[] candFoul => ref M.CandFoul;   // its line of fire: -1 not asked yet, 0 clear, 1 foul
    ref int candCount => ref M.CandCount;
    // the ship's doctrine on unknowns and her aggression, as of this think; the nearest unknown in reach she won't fire on
    // (an entity, 0: none), found with her candidates, for a challenge (docs/battle/night.md Q6)
    ref Unknowns candUnknowns => ref M.CandUnknowns;
    ref double candBold => ref M.CandBold;
    ref double candFinish => ref M.CandFinish;
    ref int candWanted => ref M.CandWanted;   // the kinds his side's aim wants sunk first (AimBook.Wanted), as of this think
    ref int candUnknown => ref M.CandUnknown;
    ref double candUnknownM => ref M.CandUnknownM;
    ref double[] reaches => ref M.Reaches;
    ref int[] ownTargets => ref M.OwnTargets;   // his batteries' targets as his orders leave them
    ref int[] heldTargets => ref M.HeldTargets;   // and as the world has them
    MatchupSide shellTarget => M.ShellTarget;
    MatchupSide threatFrom => M.ThreatFrom;
    MatchupSide threatTo => M.ThreatTo;
    ref int threatOwn => ref M.ThreatOwn;
    ref long threatMs => ref M.ThreatMs;

    // a think's items (ships by slot) and whether each is a fighter this think; its chunks' outboxes
    int[] items = new int[64];
    int itemCount;
    bool[] fighterAt = [];
    Outbox[] boxes = [];
    bool thinking;   // a dispatch under way: no captain made or dropped, no count made again
    int preparingProbe = -1, jobProbe = -1;

    /// <summary>A think's items in a chunk: a few ships' worth of work, enough to be worth a worker's while (a captain's
    /// think is some µs), small enough that a tick's dozen spread.</summary>
    const int ThinkChunk = 1, ThinkMinItems = 4;

    readonly struct ThinkJob(CaptainLayer layer, World world, IPicture picture, byte system, int slot, int slots, bool fire) : IJob
    {
        public void Run(int from, int to, int worker)
        {
            var ctx = new AiContext(world, picture, system, layer.boxes[from / ThinkChunk], slot, slots);
            for (int i = from; i < to; i++)
                layer.ThinkOne(ctx, layer.items[i], fire);
        }
    }

    /// <summary>A think, every tick its captains are due (docs/battle/workers.md W12-W13): who's under fire and the
    /// layer's fighting counted, the items found and their captains made, the shared knowledge built for reading; the
    /// items as a job; then the waits for the coarse fire watch collected in item order and the counts made again.</summary>
    public void Think(in AiContext ctx)
    {
        var probes = ctx.World.Probes;
        if (preparingProbe < 0)
            (preparingProbe, jobProbe) = (probes.Register("AI: captains, prepared"), probes.Register("AI: captains, the job"));
        long t0 = Probes.Now;
        thinkMs = ctx.NowMs;
        var store = ctx.Ships;
        for (int i = 0; i < count; i++)
            if (store.SlotOf(captains[i].Entity) < 0)
                Remove(i--);   // the ship is gone
        MarkUnderFire(ctx);
        hurtAny = false;   // the hurt flag afresh: the items' hurt ships add to it
        bool fire = Fighting;
        if (fire)
            CountFighters(ctx);
        FindItems(ctx, fire);
        Prepare(ctx);
        probes.Add(preparingProbe, t0);
        long t1 = Probes.Now;
        thinking = true;
        try
        {
            ctx.World.Workers.For(new ThinkJob(this, ctx.World, ctx.Picture, ctx.System, ctx.Slot, ctx.Slots, fire), itemCount, ThinkChunk, ThinkMinItems);
        }
        finally
        {
            thinking = false;
            seen.Thaw();
            Judge.Thaw();
            probes.Add(jobProbe, t1);
        }
        int chunks = (itemCount + ThinkChunk - 1) / ThinkChunk;
        for (int c = 0; c < chunks; c++)
            boxes[c].MoveTo(ctx.World, ctx.Outbox);
        for (int k = 0; k < itemCount; k++)
            if (Find(store[items[k]].Entity) is int i and >= 0 && captains[i].TorpedoWait)
                torpedoWaiting.Add(captains[i].Entity);   // found waiting by his think: on the coarse fire watch, in item order
        Recount();
    }

    /// <summary>The fire pass's layer-wide part: the waits of the captains due taken off the watch (their thinks find them
    /// again), every engaged ship off rails counted for the layer's period, and the batteries on each ship counted.</summary>
    void CountFighters(in AiContext ctx)
    {
        var store = ctx.Ships;
        for (int k = torpedoWaiting.Count - 1; k >= 0; k--)
        {
            int e = torpedoWaiting[k];
            if (!ctx.Due(e))
                continue;
            if (Find(e) is int wi and >= 0)
                captains[wi].TorpedoWait = false;
            torpedoWaiting.RemoveAt(k);
        }
        if (fighterAt.Length < store.Slots)
            fighterAt = new bool[Math.Max(store.Slots, fighterAt.Length * 2)];
        else
            Array.Clear(fighterAt);
        (fighting, fightingFine) = (0, false);
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int slot = store.OffRailsSlot(k);
            if (encounters.Of(slot, store[slot].Entity) == 0 && store.Combat.Engaged(slot) == 0)
                continue;
            fighterAt[slot] = true;
            if (!store.Combat.Condition(slot).IsGivenUp && store.Combat.Engaged(slot) > 0)
                (fighting, fightingFine) = (fighting + 1, fightingFine || store[slot].Tier == ClockTier.Fine);
        }
        CountOn(store);
    }

    /// <summary>The ships due this tick, by slot: each a work item; a captain made for each that needs one (a fighter, one
    /// whose end he'll judge), so none is made while they think.</summary>
    void FindItems(in AiContext ctx, bool fire)
    {
        var store = ctx.Ships;
        itemCount = 0;
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot) || !ctx.Due(store[slot].Entity))
                continue;
            int e = store[slot].Entity;
            var k = store.Combat.Condition(slot);
            if (fire && fighterAt[slot] && !k.IsGivenUp ||
                k is { Damaged: true, Status: ShipStatus.Afloat, Abandoned: false } && (k.WaterT > 0 || encounters.Of(slot, e) != 0))
                Get(e);
            if (itemCount == items.Length)
                Array.Resize(ref items, items.Length * 2);
            items[itemCount++] = slot;
        }
    }

    /// <summary>What the items read made ready for reading by several threads (docs/battle/workers.md W13): the judge's
    /// rooms and the matchups' profiles, the guns' and star shell's reaches, the squadrons' index, the crews' plans and the
    /// scratch for each lane, the chunks' outboxes. What's worked out a moment (the grids of contacts, friends and
    /// torpedoes, the census, each side's enemies) is by the first to ask, under a lock, the rest reading it after.</summary>
    void Prepare(in AiContext ctx)
    {
        var world = ctx.World;
        var store = ctx.Ships;
        int lanes = world.Workers.Participants;
        if (minds.Length < lanes)
        {
            int had = minds.Length;
            Array.Resize(ref minds, lanes);
            for (int i = had; i < lanes; i++)
                minds[i] = new Mind();
        }
        int chunks = (itemCount + ThinkChunk - 1) / ThinkChunk;
        if (boxes.Length < chunks)
        {
            int had = boxes.Length;
            Array.Resize(ref boxes, Math.Max(chunks, boxes.Length * 2));
            for (int i = had; i < boxes.Length; i++)
                boxes[i] = new Outbox { Deferred = true };
        }
        _ = groups.Of(0);   // the squadrons' index made, if it's stale
        if (crewDueMs.Length < store.Slots)
            Array.Resize(ref crewDueMs, Math.Max(store.Slots, crewDueMs.Length * 2));
        for (int k = 0; k < itemCount; k++)
        {
            int slot = items[k];
            var lo = store.Combat.Loadout(slot);
            for (int b = 0; b < lo.Class.Batteries.Length; b++)
                _ = world.Gunnery.ReachM(lo, b);
            _ = world.StarShells.ReachM(store[slot].Entity);
            var plan = PlanOf(lo.Class);
            foreach (var m in minds)
            {
                if (m.CrewTargets.Length < plan.Targets)
                    Array.Resize(ref m.CrewTargets, plan.Targets);
                if (m.CrewSources.Length < plan.Sources)
                    Array.Resize(ref m.CrewSources, plan.Sources);
            }
        }
        lines.Prepare(lanes);
        torpedoWatch.Prepare(lanes);
        Judge.Prepare(ctx, lanes);
        seen.Prepare(lanes);
    }

    /// <summary>One ship's part of a think, on a worker: her captain's own think (his order), her men's reliefs if she's
    /// hurt out of the fight, her guns if she's in it, his call on her end, the player's lights held.</summary>
    void ThinkOne(in AiContext ctx, int slot, bool fire)
    {
        var store = ctx.Ships;
        var own = store[slot];
        // the scratch his methods hand each other, his own from the start (before the workers a ship's think read the
        // doctrine the last ship's guns had left in it, until her own guns' think set hers)
        (candUnknowns, candBold, candFinish) = ((Unknowns)doctrine.Of(own.Entity, own.Side, DoctrineField.Unknowns),
            doctrine.Of(own.Entity, own.Side, DoctrineField.Aggression), FinishOf(doctrine, own));
        (candWanted, candDark, candCount, candUnknown) = (Book?.Wanted(own.Side) ?? 0, false, -1, 0);
        int i = Find(own.Entity);
        if (i >= 0)
            Behave(ctx, ref captains[i], own, slot);
        var k = store.Combat.Condition(slot);
        bool inFight = fire && (encounters.Of(slot, own.Entity) != 0 || store.Combat.Engaged(slot) > 0);
        if (k.CrewLost > 0 && !inFight)
            Crew(ctx, own, slot, fighting: false);   // a lull after the action is when her guns are manned again
        if (fire && fighterAt[slot] && !k.IsGivenUp)
            FireOne(ctx, ref captains[Find(own.Entity)], own, slot);
        EndOne(ctx, own, slot);
        if (lightsHeld > 0 && Find(own.Entity) is int h and >= 0 && (captains[h].BeamsHeld || captains[h].StarHeld) &&
            store.Combat.Condition(slot).Status == ShipStatus.Afloat && !InFight(ctx, slot, own.Entity))
            HeldLights(ctx, ref captains[h], slot);   // the player's lights out of a fight (docs/battle/night.md Q9)
    }
}
