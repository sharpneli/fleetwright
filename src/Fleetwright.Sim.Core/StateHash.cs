namespace Fleetwright.Sim.Core;

/// <summary>A 64-bit hash over the sim's state, value by value: two runs that should be the same world compare by it
/// (the determinism tests now; saves and replays later). Doubles go in by their bits, so -0 and 0 differ, as they
/// would in a later step.</summary>
public struct StateHash
{
    ulong h;

    public StateHash() => h = 14695981039346656037;

    public readonly ulong Value => h;

    public void Add(ulong v) => h = RandomStream.Mix(h, v);
    public void Add(long v) => Add((ulong)v);
    public void Add(int v) => Add((ulong)(uint)v);
    public void Add(bool v) => Add(v ? 1UL : 0UL);
    public void Add(double v) => Add((ulong)BitConverter.DoubleToInt64Bits(v));
    public void Add(Vec3d v)
    {
        Add(v.X);
        Add(v.Y);
        Add(v.Z);
    }
}
