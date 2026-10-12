using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>The world's event queue: a binary heap over a preallocated array, in <see cref="SimEvent.Compare"/>'s
/// order. Pushing and popping allocate nothing until the array has to grow (its first size is in
/// <see cref="SimSettings.EventCapacity"/>).</summary>
public sealed class Scheduler
{
    SimEvent[] heap;
    int count;
    long nextSeq;

    public Scheduler(int capacity) => heap = new SimEvent[Math.Max(capacity, 16)];

    public int Count => count;

    /// <summary>The next event to run. The queue must not be empty.</summary>
    public ref readonly SimEvent Peek()
    {
        if (count == 0)
            throw new InvalidOperationException("the event queue is empty");
        return ref heap[0];
    }

    public void Push(WorldTime time, EventTier tier, int entity, byte system, ushort code, int generation, long a)
    {
        if (count == heap.Length)
            Array.Resize(ref heap, heap.Length * 2);
        var e = new SimEvent(time, tier, entity, nextSeq++, system, code, generation, a);
        int i = count++;
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (SimEvent.Compare(heap[parent], e) <= 0)
                break;
            heap[i] = heap[parent];
            i = parent;
        }
        heap[i] = e;
    }

    public SimEvent Pop()
    {
        var top = Peek();
        var last = heap[--count];
        int i = 0;
        while (true)
        {
            int child = 2 * i + 1;
            if (child >= count)
                break;
            if (child + 1 < count && SimEvent.Compare(heap[child + 1], heap[child]) < 0)
                child++;
            if (SimEvent.Compare(last, heap[child]) <= 0)
                break;
            heap[i] = heap[child];
            i = child;
        }
        heap[i] = last;
        heap[count] = default;
        return top;
    }

    /// <summary>Mixes the queue into a hash, independent of the heap's layout: a loaded save rebuilds the heap in
    /// another order and must hash the same.</summary>
    public void Hash(ref StateHash h)
    {
        ulong sum = 0;
        for (int k = 0; k < count; k++)
        {
            ref readonly var e = ref heap[k];
            var eh = new StateHash();
            eh.Add(e.Time.Ms);
            eh.Add((int)e.Tier);
            eh.Add(e.Entity);
            eh.Add(e.Seq);
            eh.Add((int)e.System);
            eh.Add((int)e.Code);
            eh.Add(e.Generation);
            eh.Add(e.A);
            sum += eh.Value;
        }
        h.Add(count);
        h.Add(nextSeq);
        h.Add(sum);
    }
}
