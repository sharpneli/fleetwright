using System.Diagnostics;

namespace Fleetwright.Sim.Core;

/// <summary>Wall-clock time spent in named parts of one thread's work (docs/battle/plan.md step 2.7b): each probe sums
/// its time and calls and keeps its longest call since the last <see cref="ResetMax"/>. Made for hot code: a probe is
/// registered once (its name, at construction), timing is two <see cref="Stopwatch.GetTimestamp"/> calls and nothing
/// allocates; a copy goes out in each snapshot (<see cref="CopyFrom"/>), so the UI reads the sim thread's without a
/// lock. The probes only measure: nothing in the sim reads them back, so they never change what the world does.
/// <code>long t = Probes.Now; ... probes.Add(id, t);</code></summary>
public sealed class Probes
{
    public const int Capacity = 32;

    public readonly string[] Names = new string[Capacity];
    public readonly long[] Ticks = new long[Capacity];
    public readonly long[] Calls = new long[Capacity];
    public readonly long[] Max = new long[Capacity];

    /// <summary>How many are registered.</summary>
    public int Count { get; private set; }

    public static long Now => Stopwatch.GetTimestamp();

    public static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    /// <summary>A new probe's id (at construction time, not in hot code: it may allocate). A name already registered
    /// gives that probe's id.</summary>
    public int Register(string name)
    {
        for (int i = 0; i < Count; i++)
            if (Names[i] == name)
                return i;
        if (Count == Capacity)
            throw new InvalidOperationException($"more than {Capacity} probes");
        Names[Count] = name;
        return Count++;
    }

    /// <summary>A call of a probe, begun at <paramref name="since"/> (<see cref="Now"/>), ends now.</summary>
    public void Add(int probe, long since)
    {
        long d = Stopwatch.GetTimestamp() - since;
        Ticks[probe] += d;
        Calls[probe]++;
        if (d > Max[probe])
            Max[probe] = d;
    }

    /// <summary>A call of a probe that took <paramref name="ticks"/>, timed elsewhere (on a worker: docs/battle/workers.md).</summary>
    public void AddTicks(int probe, long ticks)
    {
        Ticks[probe] += ticks;
        Calls[probe]++;
        if (ticks > Max[probe])
            Max[probe] = ticks;
    }

    /// <summary>The longest calls start over (after a copy goes out, so each copy's are its own window's).</summary>
    public void ResetMax() => Array.Clear(Max);

    /// <summary>Copies another thread's probes in; allocates nothing.</summary>
    public void CopyFrom(Probes p)
    {
        Count = p.Count;
        Array.Copy(p.Names, Names, Capacity);
        Array.Copy(p.Ticks, Ticks, Capacity);
        Array.Copy(p.Calls, Calls, Capacity);
        Array.Copy(p.Max, Max, Capacity);
    }
}
