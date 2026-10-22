using System.Text;
using Fleetwright.Map;

namespace Fleetwright.Tests;

/// <summary>The screens' garbage-free text: numbers written invariant, a text written inside another's holes (a label
/// made by its own W) kept whole, several held at once, and nothing allocated once warm.</summary>
public class U8Tests
{
    static string S(ReadOnlySpan<byte> b) => Encoding.UTF8.GetString(b);

    static ReadOnlySpan<byte> Label(int n) => U8.W($"ship #{n}");

    [Fact]
    public void Writes_numbers_and_keeps_a_text_written_inside_another()
    {
        U8.Frame();
        var a = U8.W($"{12.345:0.0} kn · {7} hits");
        var b = U8.W($"on {Label(42)} at {9800:#,0} m");
        Assert.Equal("12.3 kn · 7 hits", S(a));
        Assert.Equal("on ship #42 at 9,800 m", S(b));
        Assert.Equal("12.3 kn · 7 hits", S(a));   // still whole beside the second
    }

    [Fact]
    public void Allocates_nothing_once_warm()
    {
        U8.Frame();
        _ = U8.W($"{1.5:0.0} {2} {Label(3)}");
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            U8.Frame();
            _ = U8.W($"{i * 1.5:0.0} kn · {i} hits · {Label(i)}");
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
