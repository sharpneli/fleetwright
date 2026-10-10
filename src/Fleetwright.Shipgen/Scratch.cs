namespace Fleetwright.Shipgen;

/// <summary>Per-thread pools of scratch lists for the build's hot loops: a design runs many layouts while its hull is
/// sized, and the game can't afford the garbage. <c>using var _ = Scratch&lt;T&gt;.Rent(out var list);</c> hands out an
/// empty list that goes back (cleared) when the lease is disposed. A rented list must not outlive its lease.</summary>
public static class Scratch<T>
{
    const int Keep = 32;

    [ThreadStatic]
    static Stack<List<T>>? free;

    public static Lease Rent(out List<T> list)
    {
        var f = free ??= new Stack<List<T>>();
        list = f.Count > 0 ? f.Pop() : [];
        return new Lease(list);
    }

    public readonly struct Lease(List<T> list) : IDisposable
    {
        public void Dispose()
        {
            list.Clear();
            var f = free ??= new Stack<List<T>>();
            if (f.Count < Keep)
                f.Push(list);
        }
    }
}
