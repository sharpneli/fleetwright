using System.Runtime.ExceptionServices;

namespace Fleetwright.Sim;

/// <summary>The thread that works a ship's batch of explosions as soon as it closes (docs/battle/workers.md W16d), so a magazine's
/// race costs the sim thread nothing unless it lands before the work is done (then it waits). What it works and what lands when
/// are the rule's, so the world is the same with it and without: the host's choice (<see cref="Threaded"/>), never world state.</summary>
public sealed partial class DamageSystem
{
    BatchWorker? worker;

    /// <summary>Whether batches are worked on a thread of their own (the game's host turns it on); without, each is worked on the
    /// sim thread when it lands. The same world either way.</summary>
    public bool Threaded
    {
        get => worker is not null;
        set
        {
            if (value && worker is null)
                worker = new BatchWorker(this);
            else if (!value && worker is not null)
            {
                worker.Dispose();
                worker = null;
            }
        }
    }

    /// <summary>A closed batch to the thread, if there is one.</summary>
    void Submit(Batch b)
    {
        if (worker is null)
            return;
        b.Submitted = true;
        worker.Submit(b);
    }

    /// <summary>A batch the thread has: back once it's done (worked, or failed: the failure's thrown here, on the sim thread).</summary>
    void Await(Batch b)
    {
        if (!b.Submitted)
            return;
        BatchWorker.Wait(b);
        b.Submitted = false;
        if (b.Error is { } e)
        {
            b.Error = null;
            e.Throw();
        }
    }

    sealed class BatchWorker : IDisposable
    {
        readonly DamageSystem damage;
        readonly Thread thread;
        readonly Queue<Batch> queue = new(64);
        readonly HitScratch scratch = new();
        bool stop;

        public BatchWorker(DamageSystem damage)
        {
            this.damage = damage;
            thread = new Thread(Run) { IsBackground = true, Name = "Fleetwright damage" };
            thread.Start();
        }

        public void Submit(Batch b)
        {
            b.Done = false;
            lock (queue)
            {
                queue.Enqueue(b);
                Monitor.Pulse(queue);
            }
        }

        public static void Wait(Batch b)
        {
            if (Volatile.Read(ref b.Done))
                return;
            lock (b)
                while (!b.Done)
                    Monitor.Wait(b);
        }

        void Run()
        {
            while (true)
            {
                Batch b;
                lock (queue)
                {
                    while (queue.Count == 0 && !stop)
                        Monitor.Wait(queue);
                    if (stop && queue.Count == 0)
                        return;
                    b = queue.Dequeue();
                }
                try
                {
                    damage.WorkBatch(b, b.Copy.Class, scratch);
                }
                catch (Exception e)
                {
                    b.Error = ExceptionDispatchInfo.Capture(e);
                }
                lock (b)
                {
                    b.Done = true;
                    Monitor.PulseAll(b);
                }
            }
        }

        /// <summary>Stops once what it has is worked.</summary>
        public void Dispose()
        {
            lock (queue)
            {
                stop = true;
                Monitor.Pulse(queue);
            }
            thread.Join();
        }
    }
}
