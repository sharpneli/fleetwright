namespace Fleetwright.Sim;

/// <summary>Three preallocated instances handed between one writer and one reader without locks or allocation: the
/// writer fills <see cref="Back"/> and publishes it; the reader takes the newest published one as
/// <see cref="Front"/>. Neither ever waits on the other, and the reader never sees an instance being written.</summary>
public sealed class TripleBuffer<T> where T : class
{
    const int Fresh = 4;
    readonly T[] items;
    int back, front;
    int middle;   // the index between them, with Fresh set when the writer has published since the reader last took

    public TripleBuffer(Func<T> make)
    {
        items = [make(), make(), make()];
        (back, middle, front) = (0, 1, 2);
    }

    /// <summary>The writer's instance.</summary>
    public T Back => items[back];

    /// <summary>The reader's instance: the newest it has taken.</summary>
    public T Front => items[front];

    /// <summary>Writer: hands <see cref="Back"/> over and takes the spare.</summary>
    public void Publish() => back = Interlocked.Exchange(ref middle, back | Fresh) & 3;

    /// <summary>Reader: takes the newest published instance, if there's one newer than <see cref="Front"/>.</summary>
    public bool Acquire()
    {
        if ((Volatile.Read(ref middle) & Fresh) == 0)
            return false;
        front = Interlocked.Exchange(ref middle, front) & 3;
        return true;
    }
}
