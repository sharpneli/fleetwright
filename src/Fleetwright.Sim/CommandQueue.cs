namespace Fleetwright.Sim;

/// <summary>Commands from any thread to the one advancing the world: a ring of structs behind a lock. A few commands
/// a second at most, so the lock costs nothing; ConcurrentQueue would drop its segments as garbage. The ring grows
/// (allocating) only if it fills; a command is never dropped.</summary>
public sealed class CommandQueue(int capacity)
{
    readonly object gate = new();
    Command[] ring = new Command[Math.Max(capacity, 4)];
    int head, count;

    public void Enqueue(in Command c)
    {
        lock (gate)
        {
            if (count == ring.Length)
            {
                var bigger = new Command[ring.Length * 2];
                for (int k = 0; k < count; k++)
                    bigger[k] = ring[(head + k) % ring.Length];
                (ring, head) = (bigger, 0);
            }
            ring[(head + count++) % ring.Length] = c;
        }
    }

    public bool TryDequeue(out Command c)
    {
        lock (gate)
        {
            if (count == 0)
            {
                c = default;
                return false;
            }
            c = ring[head];
            head = (head + 1) % ring.Length;
            count--;
            return true;
        }
    }
}
