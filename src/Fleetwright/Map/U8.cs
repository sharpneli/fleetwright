using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Text the screens make every frame without garbage: an interpolated string written straight into a UTF-8
/// buffer (it never becomes a string), then drawn with ImGui's native calls: <c>U8.Text(U8.W($"{kn:0.0} kn"))</c>.
/// Numbers are written by their UTF-8 formatting, invariant; the buffer is shared and freed each frame
/// (<see cref="Frame"/>), so a few texts can be held at once (a table row's cells). A text that doesn't fit is cut.</summary>
public static unsafe class U8
{
    static readonly byte[] buffer = new byte[16 * 1024];
    static int used;

    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The buffer is free again: the map's UI calls it once a frame, before it writes.</summary>
    public static void Frame() => used = 0;

    /// <summary>Writes the text into the buffer and returns it.</summary>
    public static ReadOnlySpan<byte> W(scoped ref Handler h) => h.Done();

    public static void Text(ReadOnlySpan<byte> s)
    {
        fixed (byte* p = s)
            ImGuiNative.igTextUnformatted(p, p + s.Length);
    }

    public static void Text(ReadOnlySpan<byte> s, Vector4 colour)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, colour);
        Text(s);
        ImGui.PopStyleColor();
    }

    public static Vector2 Size(ReadOnlySpan<byte> s)
    {
        Vector2 v;
        fixed (byte* p = s)
            ImGuiNative.igCalcTextSize(&v, p, p + s.Length, 0, -1);
        return v;
    }

    public static void Draw(ImDrawListPtr dl, Vector2 at, uint colour, ReadOnlySpan<byte> s)
    {
        fixed (byte* p = s)
            ImGuiNative.ImDrawList_AddText_Vec2(dl.NativePtr, at, colour, p, p + s.Length);
    }

    /// <summary>Right-aligned in what's left of the line (a table's number column).</summary>
    public static void Right(ReadOnlySpan<byte> s, Vector4 colour)
    {
        float w = Size(s).X, avail = ImGui.GetContentRegionAvail().X;
        if (avail > w)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - w);
        Text(s, colour);
    }

    /// <summary>The interpolated text's builder: each part written straight into the buffer.</summary>
    [InterpolatedStringHandler]
    public ref struct Handler
    {
        readonly int start;
        int at;

        public Handler(int literalLength, int formattedCount) => start = at = used;

        Span<byte> Free => buffer.AsSpan(at);

        // each part claims what it wrote at once, so a text written inside this one's holes (a label made by its own
        // W) lands where this one goes on: copying it in is then a copy onto itself
        void Wrote(int n) => used = at += n;

        public void AppendLiteral(string s) => AppendFormatted(s);

        public void AppendFormatted(string? s)
        {
            if (s != null)
                Wrote(Encoding.UTF8.TryGetBytes(s, Free, out int n) ? n : 0);
        }

        public void AppendFormatted(scoped ReadOnlySpan<byte> utf8)
        {
            int n = Math.Min(utf8.Length, Free.Length);
            utf8[..n].CopyTo(Free);
            Wrote(n);
        }

        public void AppendFormatted(ReadOnlySpan<char> s) => Wrote(Encoding.UTF8.TryGetBytes(s, Free, out int n) ? n : 0);
        public void AppendFormatted(double v, string? format = null) => Wrote(v.TryFormat(Free, out int n, format, Inv) ? n : 0);
        public void AppendFormatted(float v, string? format = null) => Wrote(v.TryFormat(Free, out int n, format, Inv) ? n : 0);
        public void AppendFormatted(int v, string? format = null) => Wrote(v.TryFormat(Free, out int n, format, Inv) ? n : 0);
        public void AppendFormatted(long v, string? format = null) => Wrote(v.TryFormat(Free, out int n, format, Inv) ? n : 0);

        internal ReadOnlySpan<byte> Done()
        {
            used = at;
            return buffer.AsSpan(start, at - start);
        }
    }
}
