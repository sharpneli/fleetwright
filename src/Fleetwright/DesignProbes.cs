using Fleetwright.Shipgen;

namespace Fleetwright;

/// <summary>
/// Builds batches of designs in the background for their numbers only: the same validation and
/// <see cref="ShipDesign.Build"/> as any build, without the hitbox mesh or the sprites (the visual steps). The designer's
/// trim sheet asks it what one step of each lever would do.
///
/// One batch runs at a time, spread over a few threads of their own. <see cref="Start"/> supersedes the running batch:
/// builds already under way finish into the old batch, which nobody reads any more, and the threads move on to the new
/// one. A batch is written only by the threads (each slot once) and read by the caller, so the caller can poll it each
/// frame without locks: a slot's state is published after its result.
/// </summary>
public sealed class DesignProbes : IDisposable
{
    /// <summary>The numbers a probe keeps of the ship (the ship itself is dropped).</summary>
    public readonly record struct Result(double StdT, double FullT, double LengthM, double BeamM, double DraughtM, double GmM);

    public enum State { Waiting, Built, Failed, Skipped }

    /// <summary>A batch of designs to build; a null design is skipped (nothing to build in that slot).</summary>
    public sealed class Batch
    {
        readonly Design?[] designs;
        readonly Result[] results;
        readonly int[] states;
        int next = -1, finished;
        internal bool Cancelled;

        internal Batch(object key, Design?[] designs)
        {
            Key = key;
            this.designs = designs;
            results = new Result[designs.Length];
            states = new int[designs.Length];
            for (int i = 0; i < designs.Length; i++)
                if (designs[i] == null)
                {
                    states[i] = (int)State.Skipped;
                    finished++;
                }
        }

        /// <summary>What the caller made the batch for (compared by reference to tell its own batch).</summary>
        public object Key { get; }

        public int Count => designs.Length;

        /// <summary>Slots done (built, failed or skipped).</summary>
        public int Finished => Volatile.Read(ref finished);

        public bool Done => Finished == Count;

        public State StateOf(int i) => (State)Volatile.Read(ref states[i]);

        public bool TryGet(int i, out Result r)
        {
            bool ok = StateOf(i) == State.Built;
            r = ok ? results[i] : default;
            return ok;
        }

        /// <summary>The next slot to build, or -1 when every slot is taken. Called under the runner's lock.</summary>
        internal int Take()
        {
            while (++next < designs.Length)
                if (designs[next] != null)
                    return next;
            return -1;
        }

        internal bool HasWork => !Cancelled && next + 1 < designs.Length;

        internal void Run(int i, bool limits)
        {
            var design = designs[i]!;
            var state = State.Failed;
            try
            {
                if (ShipDesign.Validate(design, limits).Count == 0)
                {
                    var r = ShipDesign.Build(design).Report.Results;
                    results[i] = new Result(r.StandardDisplacementT, r.FullDisplacementT, r.LengthM, r.BeamM, r.DraughtM, r.GmFullM);
                    state = State.Built;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"design probes: {design.Id}: {e.GetType().Name}: {e.Message}");
            }
            Volatile.Write(ref states[i], (int)state);
            Interlocked.Increment(ref finished);
        }
    }

    readonly object gate = new();
    readonly Thread[] threads;
    readonly bool limits;
    Batch? current;
    bool stopping;

    /// <param name="limits">Hold designs to the styles' input limits, as the designer's own builds do.</param>
    public DesignProbes(bool limits = true)
    {
        this.limits = limits;
        int n = Math.Clamp(Environment.ProcessorCount - 2, 1, 8);   // the render thread and the design worker keep a core each
        threads = new Thread[n];
        for (int i = 0; i < n; i++)
        {
            threads[i] = new Thread(Run) { Name = $"Design probes {i + 1}", IsBackground = true, Priority = ThreadPriority.BelowNormal };
            threads[i].Start();
        }
    }

    /// <summary>The batch last started (null before the first or after <see cref="Cancel"/>).</summary>
    public Batch? Current => Volatile.Read(ref current);

    /// <summary>Starts building <paramref name="designs"/>, superseding the running batch.</summary>
    public Batch Start(object key, Design?[] designs)
    {
        var b = new Batch(key, designs);
        lock (gate)
        {
            if (current != null)
                current.Cancelled = true;
            Volatile.Write(ref current, b);
            Monitor.PulseAll(gate);
        }
        return b;
    }

    /// <summary>Stops handing out the running batch's slots (builds under way finish); <see cref="Current"/> is null after.</summary>
    public void Cancel()
    {
        lock (gate)
        {
            if (current != null)
                current.Cancelled = true;
            Volatile.Write(ref current, null);
        }
    }

    void Run()
    {
        while (true)
        {
            Batch b;
            int i;
            lock (gate)
            {
                while (!stopping && current is not { HasWork: true })
                    Monitor.Wait(gate);
                if (stopping)
                    return;
                b = current!;
                i = b.Take();
            }
            if (i >= 0)
                b.Run(i, limits);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            stopping = true;
            Monitor.PulseAll(gate);
        }
        foreach (var t in threads)
            t.Join();
    }
}
