using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Fleetwright.Sim.Core;

/// <summary>A job's work on a run of items (docs/battle/workers.md): items <c>[from, to)</c>, on participant
/// <paramref name="worker"/> (0 the dispatching thread, 1.. the workers: an index into per-worker scratch). It reads the
/// world and writes only its own items' slots; it never dispatches.</summary>
public interface IJob
{
    void Run(int from, int to, int worker);
}

/// <summary>The sim's workers (docs/battle/workers.md W1): a few fixed threads that take a moment's per-item work off the
/// sim thread. A dispatch (<see cref="For{T}"/>) cuts the items into chunks of the job's own size, never the worker
/// count's, the dispatching thread takes chunks too, and it returns when every chunk is done; so a job writing only its
/// items' slots, and summing per chunk, gives the same result on any number of workers. With no threads, a batch of one
/// chunk, or one under the job's threshold, the job runs inline: the same code path. The host's, never the world's
/// state: not in the settings, the hash or a save.
/// <para>Nothing allocates per dispatch after a job type's first (its holder). Workers spin a short while after their
/// last chunk (<c>spinMicroseconds</c>), so back-to-back dispatches pay no wake-up, then sleep. One thread dispatches
/// at a time; a job dispatching from inside a job is an error.</para></summary>
public sealed class Workers : IDisposable
{
    /// <summary>No threads: every dispatch runs inline, in order (the default for a world).</summary>
    public static readonly Workers Inline = new(0);

    const int Pad = 16;   // longs between participants' counters: a cache line and more, so they never share one

    abstract class Holder
    {
        public abstract void Run(int from, int to, int worker);
        public abstract void Clear();
    }

    sealed class Holder<T> : Holder where T : struct, IJob
    {
        public T Job;
        public override void Run(int from, int to, int worker) => Job.Run(from, to, worker);
        public override void Clear() => Job = default;
    }

    static int typeCount;

    static class Slot<T> where T : struct, IJob
    {
        public static readonly int Id = Interlocked.Increment(ref typeCount) - 1;
    }

    readonly Thread[] threads;
    readonly AutoResetEvent[] wakes;
    readonly long spinTicks;
    readonly long[] busy, allocated, sleeping;
    Holder?[] holders = new Holder?[8];

    // the dispatch: its holder, items and chunk, written before info; info and ticket carry the generation in their top
    // half, so a worker late from the last dispatch can never take a chunk of this one under the last one's count
    Holder? current;
    int items, chunk, gen;
    long info;     // gen << 32 | chunks
    long ticket;   // gen << 32 | the next chunk
    int done;      // chunks finished
    Exception? failure;
    bool dispatching;
    volatile bool stop;

    // the shuffled-chunk mode (principle 5): inline, the chunks in an order of the seed's
    readonly bool shuffle;
    ulong shuffleState;
    int[] order = [];

    /// <param name="threads">Worker threads beside the dispatching one; 0 runs every dispatch inline.</param>
    /// <param name="spinMicroseconds">How long a worker spins for the next dispatch before it sleeps.</param>
    /// <param name="shuffleSeed">Not 0, with no threads: each dispatch's chunks run in a seeded random order, so a job
    /// whose result depends on its chunks' order fails every time (docs/battle/workers.md principle 5).</param>
    public Workers(int threads, double spinMicroseconds = 250, ulong shuffleSeed = 0)
    {
        if (shuffleSeed != 0 && threads > 0)
            throw new ArgumentException("the shuffled mode runs on one thread");
        this.threads = new Thread[Math.Max(threads, 0)];
        wakes = new AutoResetEvent[this.threads.Length];
        spinTicks = (long)(spinMicroseconds * 1e-6 * Stopwatch.Frequency);
        busy = new long[(this.threads.Length + 1) * Pad];
        allocated = new long[(this.threads.Length + 1) * Pad];
        sleeping = new long[(this.threads.Length + 1) * Pad];
        shuffle = shuffleSeed != 0;
        shuffleState = shuffleSeed;
        for (int i = 0; i < this.threads.Length; i++)
        {
            int w = i + 1;
            wakes[i] = new AutoResetEvent(false);
            this.threads[i] = new Thread(() => Work(w)) { Name = $"Sim worker {w}", IsBackground = true };
            this.threads[i].Start();
        }
    }

    /// <summary>The worker threads (0: inline).</summary>
    public int Count => threads.Length;

    /// <summary>Per-worker scratch slots a job needs: the workers and the dispatching thread.</summary>
    public int Participants => threads.Length + 1;

    /// <summary>The headless default (decision 1): every core less one, at most 8.</summary>
    public static int HeadlessCount => Math.Clamp(Environment.ProcessorCount - 1, 0, 8);

    /// <summary>The app's default (decision 1): every core less two (the render thread, the builders), at most 8.</summary>
    public static int AppCount => Math.Clamp(Environment.ProcessorCount - 2, 0, 8);

    /// <summary>What the worker threads have allocated, in all (their own counts, read as each leaves a dispatch): the
    /// dispatching thread's own count misses it.</summary>
    public long AllocatedBytes
    {
        get
        {
            long sum = 0;
            for (int w = 1; w <= threads.Length; w++)
                sum += Volatile.Read(ref allocated[w * Pad]);
            return sum;
        }
    }

    /// <summary>Runs <paramref name="job"/> over items <c>[0, count)</c> in chunks of <paramref name="chunkSize"/>;
    /// inline under <paramref name="minItems"/>. Returns the time every participant spent on it, summed (ticks of
    /// <see cref="Stopwatch"/>): against the dispatch's wall time, its parallel share. A job's exception is rethrown
    /// here, after every chunk is done.</summary>
    public long For<T>(in T job, int count, int chunkSize, int minItems = 0) where T : struct, IJob
    {
        if (count <= 0)
            return 0;
        int chunks = (count + chunkSize - 1) / chunkSize;
        if (threads.Length == 0 || chunks == 1 || count < minItems)
            return RunInline(job, count, chunkSize, chunks);
        if (dispatching)
            throw new InvalidOperationException("a dispatch from inside a dispatch");
        dispatching = true;
        long busy0 = BusySum();
        var h = HolderOf<T>();
        h.Job = job;
        (current, items, chunk, done, failure) = (h, count, chunkSize, 0, null);
        gen++;
        Volatile.Write(ref info, ((long)gen << 32) | (uint)chunks);
        Interlocked.Exchange(ref ticket, (long)gen << 32);
        WakeNext(1);
        Claim(0);
        var spin = new SpinWait();
        while (Volatile.Read(ref done) < chunks)
            spin.SpinOnce(sleep1Threshold: -1);
        h.Clear();
        current = null;
        dispatching = false;
        if (failure is { } x)
            ExceptionDispatchInfo.Throw(x);
        return BusySum() - busy0;
    }

    long RunInline<T>(in T job, int count, int chunkSize, int chunks) where T : struct, IJob
    {
        long t = Stopwatch.GetTimestamp();
        if (!shuffle)
            for (int from = 0; from < count; from += chunkSize)   // chunk by chunk: a job's per-chunk parts are the same
                job.Run(from, Math.Min(from + chunkSize, count), 0);
        else
        {
            if (order.Length < chunks)
                order = new int[Math.Max(chunks, 2 * order.Length)];
            for (int c = 0; c < chunks; c++)
                order[c] = c;
            for (int c = chunks - 1; c > 0; c--)   // Fisher-Yates on a splitmix stream of the seed's
            {
                shuffleState += 0x9E3779B97F4A7C15;
                int k = (int)(RandomStream.Mix(shuffleState, 0) % (ulong)(c + 1));
                (order[c], order[k]) = (order[k], order[c]);
            }
            for (int i = 0; i < chunks; i++)
            {
                int from = order[i] * chunkSize;
                job.Run(from, Math.Min(from + chunkSize, count), 0);
            }
        }
        return Stopwatch.GetTimestamp() - t;
    }

    Holder<T> HolderOf<T>() where T : struct, IJob
    {
        int id = Slot<T>.Id;
        if (id >= holders.Length)
            Array.Resize(ref holders, Math.Max(id + 1, 2 * holders.Length));
        return (Holder<T>)(holders[id] ??= new Holder<T>());
    }

    /// <summary>Wakes the first sleeping worker from <paramref name="from"/> on; each one woken wakes the next, so the
    /// wake-ups' system calls are spread over the workers, not queued on the dispatching thread.</summary>
    void WakeNext(int from)
    {
        for (int w = from; w <= threads.Length; w++)
            if (Interlocked.Exchange(ref sleeping[w * Pad], 0) != 0)
            {
                wakes[w - 1].Set();
                return;
            }
    }

    long BusySum()
    {
        long sum = 0;
        for (int w = 0; w <= threads.Length; w++)
            sum += Volatile.Read(ref busy[w * Pad]);
        return sum;
    }

    /// <summary>Takes chunks of the current dispatch until none is left; the generation of the ticket it last saw.</summary>
    int Claim(int w)
    {
        long t = Stopwatch.GetTimestamp();
        int g;
        while (true)
        {
            long v = Interlocked.Increment(ref ticket) - 1;
            g = (int)(v >> 32);
            int c = (int)(v & 0xFFFF_FFFF);
            long inf = Volatile.Read(ref info);
            if ((int)(inf >> 32) != g || c >= (int)(inf & 0xFFFF_FFFF))
                break;
            int from = c * chunk;
            try
            {
                current!.Run(from, Math.Min(from + chunk, items), w);
            }
            catch (Exception x)
            {
                Interlocked.CompareExchange(ref failure, x, null);
            }
            Interlocked.Increment(ref done);
        }
        Volatile.Write(ref busy[w * Pad], busy[w * Pad] + Stopwatch.GetTimestamp() - t);
        return g;
    }

    void Work(int w)
    {
        int seen = 0;
        while (true)
        {
            long since = Stopwatch.GetTimestamp();
            while ((int)(Volatile.Read(ref info) >> 32) == seen)
            {
                if (stop)
                    return;
                if (Stopwatch.GetTimestamp() - since < spinTicks)
                {
                    Thread.SpinWait(20);
                    continue;
                }
                // asleep: the flag set first, then the dispatch looked for again, so a dispatch either sees the flag
                // and wakes the worker or is seen here (both sides fence)
                Interlocked.Exchange(ref sleeping[w * Pad], 1);
                if ((int)(Volatile.Read(ref info) >> 32) != seen || stop)
                {
                    Volatile.Write(ref sleeping[w * Pad], 0);
                    break;
                }
                wakes[w - 1].WaitOne();
                since = Stopwatch.GetTimestamp();
            }
            if (stop)
                return;
            WakeNext(w + 1);
            seen = Claim(w);
            Volatile.Write(ref allocated[w * Pad], GC.GetAllocatedBytesForCurrentThread());
        }
    }

    public void Dispose()
    {
        if (threads.Length == 0)
            return;
        stop = true;
        foreach (var e in wakes)
            e.Set();
        foreach (var t in threads)
            t.Join();
        foreach (var e in wakes)
            e.Dispose();
    }
}
