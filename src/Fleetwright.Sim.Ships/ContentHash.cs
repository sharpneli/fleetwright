using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>A SHA-256 of some content, as four ulongs: equatable, no allocation, written as it is. Two of them name a
/// class (docs/battle/plan.md, "The code, checked again"): the design hash (the canonical design JSON: the registry's
/// key and a mission's or save's reference to a design) and the layout hash (the compiled class's structure: ids,
/// kinds and links in index order, which says that a rebuilt class has the indices a saved one had).</summary>
public readonly record struct ContentHash(ulong A, ulong B, ulong C, ulong D)
{
    public static ContentHash Of(ReadOnlySpan<byte> bytes)
    {
        Span<byte> h = stackalloc byte[32];
        SHA256.HashData(bytes, h);
        return FromBytes(h);
    }

    /// <summary>The design hash: the design as shipgen writes it (its typed form, so whitespace, key order and number
    /// spelling in the file don't matter).</summary>
    public static ContentHash OfDesign(Design design) =>
        Of(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(design, ShipgenJson.Default.Design));

    static ContentHash FromBytes(ReadOnlySpan<byte> h) => new(BinaryPrimitives.ReadUInt64BigEndian(h),
        BinaryPrimitives.ReadUInt64BigEndian(h[8..]), BinaryPrimitives.ReadUInt64BigEndian(h[16..]), BinaryPrimitives.ReadUInt64BigEndian(h[24..]));

    public override string ToString() => $"{A:x16}{B:x16}{C:x16}{D:x16}";

    /// <summary>The first 12 hex digits, for logs.</summary>
    public string Short => $"{A:x16}"[..12];

    /// <summary>Builds a hash from values fed in order (the layout hash): each value by its bits, a string by its
    /// length and UTF-8 bytes, so no two sequences run together.</summary>
    internal sealed class Builder : IDisposable
    {
        readonly IncrementalHash sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        public void Add(int v)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(b, v);
            sha.AppendData(b);
        }

        public void Add(string? s)
        {
            if (s is null)
            {
                Add(-1);
                return;
            }
            var bytes = Encoding.UTF8.GetBytes(s);
            Add(bytes.Length);
            sha.AppendData(bytes);
        }

        public void Add(ReadOnlySpan<int> vs)
        {
            Add(vs.Length);
            foreach (var v in vs)
                Add(v);
        }

        public ContentHash Finish()
        {
            Span<byte> h = stackalloc byte[32];
            sha.GetHashAndReset(h);
            return FromBytes(h);
        }

        public void Dispose() => sha.Dispose();
    }
}
