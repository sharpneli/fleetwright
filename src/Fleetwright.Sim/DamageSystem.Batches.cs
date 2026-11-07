using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>Explosions landing a second later (docs/battle/workers.md W16): in the fine tier an explosion on a ship (a hit of any
/// kind, a magazine her fire sets off) goes into a batch, worked on a copy of her condition taken when the batch closes and landed on
/// the damage lattice at least <see cref="SimSettings.HitDelayMs"/> after it came; meanwhile her condition stands as it was and what
/// would change it waits behind the batch. When it lands and what it does are the rule's alone, so the world is the same whoever
/// works it out and when.</summary>
public sealed partial class DamageSystem
{
    /// <summary>A ship's explosions on their way: each with what it took from the world when it came, her condition as the batch
    /// closed (the copy they're worked on) and her warheads and condition's hash then (the guard), and what came for her condition
    /// meanwhile and waits: her step, her outages' clear, a salvo's or her own guns' blast on her men.</summary>
    sealed class Batch
    {
        public int Entity, Generation, Ticket;
        public long LandMs;
        public bool Closed, Worked;
        public int Count, BearingN, MountN;
        public HitInputs[] Inputs = new HitInputs[4];
        public Worked[] Results = new Worked[4];
        public double[] Bearings = new double[64];
        public MountState[] Mounts = new MountState[64];
        /// <summary>Her condition as it closed, the work's (from the class's pool, <see cref="Copies"/>).</summary>
        public ShipCondition Copy = null!;
        public int[] Warheads = [];
        public ulong Hash;
        public bool StepWaits, ClearWaits;
        public long StepGen, ClearGen;
        public int Ops;
        public WaitOp[] Waiting = new WaitOp[16];
        /// <summary>With the thread (W16d): handed to it, done (written by it under the batch's lock), and what it threw.</summary>
        public bool Submitted, Done;
        public System.Runtime.ExceptionServices.ExceptionDispatchInfo? Error;

        public void Start(int entity, int generation, int ticket)
        {
            (Entity, Generation, Ticket, LandMs, Closed, Worked, Count, Ops) = (entity, generation, ticket, -1, false, false, 0, 0);
            (StepWaits, ClearWaits, Submitted, Done, Error) = (false, false, false, false, null);
        }

        /// <summary>Room for n explosions of a class of so many mounts.</summary>
        public void Room(int n, int mounts, int launchers)
        {
            if (Warheads.Length < launchers)
                Warheads = new int[launchers];
            if (Inputs.Length < n)
                (Inputs, Results) = (new HitInputs[n], new Worked[n]);
            if (Bearings.Length < n * mounts)
                Bearings = new double[n * mounts];
            if (Mounts.Length < n * mounts)
                Mounts = new MountState[n * mounts];
        }

        public void Wait(in WaitOp op)
        {
            if (Ops == Waiting.Length)
                Array.Resize(ref Waiting, Ops * 2);
            Waiting[Ops++] = op;
        }
    }

    enum WaitKind : byte
    {
        Salvo,
        OwnGun,
    }

    /// <summary>A change to her condition that came while a batch was out: a salvo's pressure on her men (Amount), or her own guns'
    /// shock (Mount), at its moment.</summary>
    struct WaitOp
    {
        public WaitKind Kind;
        public long Ms;
        public double Amount;
        public int Mount;
    }

    const ushort LandEvent = 4;

    /// <summary>By slot: the batch on its way, and the one gathering behind it (closing when the first lands).</summary>
    Batch?[] onWay = [], behind = [];
    readonly Stack<Batch> spareBatches = new(64);
    /// <summary>Each class's conditions to work her batches on: a couple made when the class comes into the world, more only when
    /// more of her class have batches out at once than ever before.</summary>
    readonly Dictionary<ShipClass, Stack<ShipCondition>> copies = [];

    Stack<ShipCondition> Copies(ShipClass cls)
    {
        if (!copies.TryGetValue(cls, out var pool))
        {
            copies[cls] = pool = new Stack<ShipCondition>(8);
            pool.Push(new ShipCondition(cls));
            pool.Push(new ShipCondition(cls));
            // and batches with room for a salvo's worth of her explosions, every spare one grown to the biggest class yet, so
            // the first ones in play don't allocate
            (roomMounts, roomLaunchers) = (Math.Max(roomMounts, cls.Mounts.Length), Math.Max(roomLaunchers, cls.Launchers.Length));
            spareBatches.Push(new Batch());
            spareBatches.Push(new Batch());
            foreach (var b in spareBatches)
                b.Room(Room, roomMounts, roomLaunchers);
            if (warheadsNow.Length < roomLaunchers)
                warheadsNow = new int[roomLaunchers];
        }
        return pool;
    }

    /// <summary>The explosions a new batch has room for before it grows.</summary>
    const int Room = 8;
    int[] warheadsNow = new int[8];
    int roomMounts, roomLaunchers;
    int tickets;
    /// <summary>The slot whose step is running (-1 none): a batch it opens closes when the step is done, not in it.</summary>
    int steppingSlot = -1;
    /// <summary>The slot whose batch is landing (-1 none): what its landing sets off goes in the batch behind, whatever the tier, and
    /// nothing flushes it from inside.</summary>
    int landingSlot = -1;
    /// <summary>A bench's <see cref="Settle"/> is stepping a ship outside the world's events: what its steps set off lands at once.</summary>
    bool settling;

    /// <summary>Whether explosions land later now: in the fine tier, with a delay set, the world's own events running.</summary>
    bool Deferring(World world) => settings.HitDelayMs > 0 && !world.Coarse && !settling;

    /// <summary>A hit comes (the shells', the torpedoes', her own stores'): in the fine tier into her batch, to land on the damage
    /// lattice a second or two on; in the coarse tier at once (<see cref="Strike"/>, after anything still on its way to her).</summary>
    public void Take(World world, in Hit hit)
    {
        var store = ships.Store;
        int slot = store.SlotOf(hit.Ship);
        if (slot < 0 || Spared?.Invoke(hit) == true)
            return;
        if (!Deferring(world) && slot != landingSlot)
        {
            Flush(world, slot);
            StrikeAt(world, slot, hit);
            return;
        }
        var cls = store[slot].Handling.Class;
        touched = true;
        Prepare(cls);
        var b = Gather(world, slot);
        Add(b, Capture(world, hit, slot, cls, scratch, (ulong)hits++), cls, scratch);
        if (ReferenceEquals(onWay[slot], b) && !b.Closed && steppingSlot != slot)
            Close(world, slot, b);
    }

    /// <summary>A magazine her fire set off (fire B2), as an explosion of hers: into her batch like a hit.</summary>
    void TakeSetOff(World world, int slot, int room)
    {
        var cls = ships.Store[slot].Handling.Class;
        var b = Gather(world, slot);
        var at = Capture(world, new Hit { Ship = ships.Store[slot].Entity, Ms = world.Time.Ms }, slot, cls, scratch, (ulong)hits++);
        (at.SetOff, at.Room) = (true, room);
        Add(b, at, cls, scratch);
    }

    /// <summary>Her batch to put an explosion in: the one gathering behind the one on its way, or a new one on its way, landing at
    /// the first lattice moment a delay from now.</summary>
    Batch Gather(World world, int slot)
    {
        Grow();
        if (OnWay(slot) is not null)
        {
            var g = behind[slot];
            if (g is null || !Hers(g, slot))
            {
                if (g is not null)
                    Release(g);
                behind[slot] = g = NewBatch(slot);
            }
            return g;
        }
        var b = NewBatch(slot);
        onWay[slot] = b;
        Send(world, b, world.Time.Ms);
        return b;
    }

    /// <summary>A batch on its way from now: its landing on the lattice, its event.</summary>
    void Send(World world, Batch b, long now)
    {
        long d = settings.FloodMs;
        b.LandMs = (now + settings.HitDelayMs + d - 1) / d * d;
        world.Schedule(new WorldTime(b.LandMs), EventTier.Fine, b.Entity, Id, LandEvent, b.Generation, b.Ticket);
    }

    /// <summary>A batch done with (landed, or left by a ship gone): its copy back to her class's pool, it to the spares.</summary>
    void Release(Batch b)
    {
        if (b.Submitted)
        {
            BatchWorker.Wait(b);   // never back in the pool while the thread has it
            (b.Submitted, b.Error) = (false, null);
        }
        if (b.Closed)
            Copies(b.Copy.Class).Push(b.Copy);
        (b.Copy, b.Closed) = (null!, false);
        spareBatches.Push(b);
    }

    Batch NewBatch(int slot)
    {
        var b = spareBatches.Count > 0 ? spareBatches.Pop() : new Batch();
        b.Room(Room, roomMounts, roomLaunchers);   // nothing to do unless a class came in mid-fight
        b.Start(ships.Store[slot].Entity, ships.Store[slot].Generation, ++tickets);
        return b;
    }

    /// <summary>Whether a batch is still this slot's ship's (not one gone, her slot taken by another).</summary>
    bool Hers(Batch b, int slot) => b.Entity == ships.Store[slot].Entity && b.Generation == ships.Store[slot].Generation;

    /// <summary>The batch on its way to her, if any (one left by a ship gone from the slot is let go).</summary>
    Batch? OnWay(int slot)
    {
        if (slot >= onWay.Length || onWay[slot] is not { } b)
            return null;
        if (Hers(b, slot))
            return b;
        onWay[slot] = null;
        Release(b);
        if (behind[slot] is { } g)
        {
            behind[slot] = null;
            Release(g);
        }
        return null;
    }

    void Grow()
    {
        int n = ships.Store.Capacity;
        if (onWay.Length < n)
        {
            Array.Resize(ref onWay, Math.Max(n, onWay.Length * 2));
            Array.Resize(ref behind, onWay.Length);
        }
    }

    /// <summary>An explosion into a batch, with her mounts and bearings as it took them (into sx).</summary>
    static void Add(Batch b, in HitInputs at, ShipClass cls, HitScratch sx)
    {
        int i = b.Count++;
        if (i == b.Inputs.Length)
        {
            Array.Resize(ref b.Inputs, i * 2);
            Array.Resize(ref b.Results, i * 2);
        }
        b.Inputs[i] = at;
        (b.BearingN, b.MountN) = (cls.Mounts.Length, at.Mounts);
        if (b.Bearings.Length < (i + 1) * b.BearingN)
            Array.Resize(ref b.Bearings, Math.Max((i + 1) * b.BearingN, b.Bearings.Length * 2));
        if (b.Mounts.Length < (i + 1) * b.MountN)
            Array.Resize(ref b.Mounts, Math.Max((i + 1) * b.MountN, b.Mounts.Length * 2));
        Array.Copy(sx.Bearings, 0, b.Bearings, i * b.BearingN, b.BearingN);
        Array.Copy(sx.Mounts, 0, b.Mounts, i * b.MountN, b.MountN);
    }

    /// <summary>A batch closes: her orders as they stand, then her condition as it is copied for the work, with her warheads and
    /// its hash for the guard when it lands.</summary>
    void Close(World world, int slot, Batch b)
    {
        var k = ships.Store.Combat.Condition(slot);
        Ready(slot, k);
        var pool = Copies(k.Class);
        b.Copy = pool.Count > 0 ? pool.Pop() : new ShipCondition(k.Class);
        b.Copy.CopyFrom(k);
        if (b.Warheads.Length < k.Warheads.Length)
            b.Warheads = new int[k.Warheads.Length];
        k.Warheads.CopyTo(b.Warheads, 0);
        b.Hash = HashOf(k);
        b.Closed = true;
        Submit(b);   // worked from now, off the sim thread, if there's a thread
    }

    static ulong HashOf(ShipCondition k)
    {
        var h = new StateHash();
        k.Hash(ref h);
        return h.Value;
    }

    /// <summary>A batch's explosions worked on its copy, in order, each at its own moment and with what it took.</summary>
    void WorkBatch(Batch b, ShipClass cls, HitScratch sx)
    {
        for (int i = 0; i < b.Count; i++)
        {
            ref readonly var at = ref b.Inputs[i];
            sx.Reserve(cls);
            Array.Copy(b.Bearings, i * b.BearingN, sx.Bearings, 0, b.BearingN);
            b.Mounts.AsSpan(i * b.MountN, b.MountN).CopyTo(sx.MountsFor(b.MountN));
            b.Results[i] = at.SetOff ? WorkSetOff(at, cls, b.Copy, sx) : Work(at, cls, b.Copy, sx);
        }
        b.Worked = true;
    }

    /// <summary>A batch lands (its event, or a flush): worked if it isn't yet, her condition the copy's (her warheads the fewer of the
    /// two: her torpedoes fired meanwhile), each explosion's world part in order, then what waited behind it, and the batch gathered
    /// behind it closes and goes on its way.</summary>
    void Landing(World world, int slot, Batch b)
    {
        var k = ships.Store.Combat.Condition(slot);
        var cls = k.Class;
        int was = landingSlot;
        landingSlot = slot;
        try
        {
            LandBatch(world, slot, k, cls, b);
        }
        finally
        {
            landingSlot = was;
        }
        onWay[slot] = null;
        Release(b);
        if (behind[slot] is { } g)
        {
            behind[slot] = null;
            onWay[slot] = g;
            Send(world, g, world.Time.Ms);
            Close(world, slot, g);
        }
    }

    void LandBatch(World world, int slot, ShipCondition k, ShipClass cls, Batch b)
    {
        Guard(k, b);
        Await(b);   // the thread's work, if it has it: waited for only if it isn't done
        if (!b.Worked)
            WorkBatch(b, cls, scratch);
        int n = k.Warheads.Length;
        for (int l = 0; l < n; l++)
            b.Warheads[l] = Math.Min(k.Warheads[l], b.Copy.Warheads[l]);
        k.CopyFrom(b.Copy);
        b.Warheads.AsSpan(0, n).CopyTo(k.Warheads);
        for (int i = 0; i < b.Count; i++)
        {
            if (b.Inputs[i].SetOff)
                LandSetOff(world, slot, k, b.Inputs[i], b.Results[i]);
            else
                Land(world, slot, k, b.Inputs[i], b.Results[i]);
        }
        int entity = ships.Store[slot].Entity;
        for (int i = 0; i < b.Ops; i++)
        {
            ref readonly var op = ref b.Waiting[i];
            if (k.Status != ShipStatus.Afloat)
                break;
            if (op.Kind == WaitKind.Salvo)
                Pressed(world, slot, k, op.Amount, op.Ms);
            else
            {
                var r = stream.Derive(0x06E5u).Derive((ulong)entity).Derive((ulong)op.Ms).Derive((ulong)op.Mount);
                OwnGunsOut(world, slot, k, op.Mount, op.Ms, ref r);
            }
        }
        if (b.ClearWaits)
            RunClear(world, slot, k, b.ClearGen);
        if (b.StepWaits)
            RunStep(world, slot, k, b.StepGen);
    }

    /// <summary>Everything on its way to her lands now (a command on her, her going under, the coarse tier taking her): what lands
    /// and in what order is as it would have been, only sooner.</summary>
    void Flush(World world, int slot)
    {
        if (slot == landingSlot)
            return;   // landing now: what it sets off goes behind, and lands after it
        while (OnWay(slot) is { } b)
        {
            if (!b.Closed)
                Close(world, slot, b);
            Landing(world, slot, b);
        }
    }

    /// <summary>The guard (W16): her condition, warheads aside, as it was when the batch closed. Anything else that changed it
    /// meanwhile didn't wait behind the batch, and the landing would undo it.</summary>
    void Guard(ShipCondition k, Batch b)
    {
        if (!b.Closed)
            return;
        // her warheads as they were when it closed, for the hash, and hers put back
        int n = k.Warheads.Length;
        if (warheadsNow.Length < n)
            warheadsNow = new int[n];
        k.Warheads.CopyTo(warheadsNow, 0);
        b.Warheads.AsSpan(0, n).CopyTo(k.Warheads);
        ulong h = HashOf(k);
        warheadsNow.AsSpan(0, n).CopyTo(k.Warheads);
        if (h != b.Hash)
            throw new InvalidOperationException($"{k.Class.Name}: her condition changed while a batch of explosions was on its way to her " +
                "(docs/battle/workers.md W16: what changes it waits behind the batch)");
    }

    /// <summary>A magazine her fire set off, worked: its race on her condition, and what she can still do after it if she lives.</summary>
    Worked WorkSetOff(in HitInputs at, ShipClass cls, ShipCondition k, HitScratch sx)
    {
        var w = new Worked();
        if (k.Status != ShipStatus.Afloat)
            return w;
        var rng = stream.Derive(at.Index);
        w.Race = Magazines.SetOff(cls, k, at.Room, sx.Mounts.AsSpan(0, at.Mounts), settings, at.Now, ref rng, at.Sea, 0);
        if (w.Race.Magazines == 0)
            return w;
        if (w.Race.Lost)
        {
            w.Blew = true;
            (k.Status, k.Loss) = (ShipStatus.GoingDown, LossKind.Magazine);
            return w;
        }
        k.FloodDueMs = Math.Min(k.FloodDueMs, at.Now + settings.FloodMs);   // what it opened takes the sea from her next second
        (w.Recomputed, w.Change) = (true, RecomputeOwn(k, at.Experience, at.Rudder, at.Now, ref rng));
        return w;
    }

    /// <summary>A magazine her fire set off, landed: counted, and she blows up, or her mounts and power as she now is and her steps.</summary>
    void LandSetOff(World world, int slot, ShipCondition k, in HitInputs at, in Worked w)
    {
        if (w.Race.Magazines == 0)
            return;
        MagazineCounts.ByFire[(int)w.Race.Tier]++;
        LogRace(slot, w.Race.Tier);
        if (w.Blew)
            Blew(world, slot, k, at.Room, BlastCause.Fire, default, w.Race);
        else if (w.Recomputed)
        {
            RecomputeWorld(world, slot, k, w.Change);
            Wake(world, slot, k);
        }
    }
}
