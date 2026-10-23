using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Where a label goes against its anchor: beside it, past the symbol's gap.</summary>
public enum LabelPlace : byte
{
    None,
    Right,
    Left,
    Above,
    Below,
}

/// <summary>What a label is of: the scene draws each kind its own way.</summary>
public enum LabelKind : byte
{
    Ship,
    Group,
    Contact,
    Encounter,
    Wreck,
    Range,
    Torpedo,
}

/// <summary>The map's labels, all placed by one placer (docs/map-ui/symbols.md Y5; docs/map-ui/README.md "Labels never
/// pile up"): each frame the scene asks for a label by its anchor, size, the places it may go (in order) and a priority;
/// the placer takes them by priority, tries each one's place of last frame first (so a label doesn't jump while it
/// still fits) and then its others, against one occupancy grid of the screen (the anchors' symbols reserved first),
/// and drops what doesn't fit (the hover still names it). Labels fade in and out over <see cref="SymbolLook.LabelFade"/>
/// of real time. Linear in the labels; the arrays and the states are kept: no garbage once grown.</summary>
public sealed class MapLabels
{
    public struct Request
    {
        public long Key;
        public float X, Y, W, H, Gap, Reserve;
        public int Priority, Index;
        public LabelKind Kind;
        public LabelPlace P0, P1, P2, P3;
    }

    struct State
    {
        public LabelPlace Place;
        public float Alpha, X, Y;
        public long Seen;
    }

    Request[] requests = new Request[256];
    long[] order = new long[256];
    Vector2[] at = new Vector2[256];
    LabelPlace[] placed = new LabelPlace[256];
    float[] alpha = new float[256];
    int count;
    readonly Dictionary<long, State> states = [];
    readonly List<long> stale = [];
    bool[] grid = [];
    int gw, gh;
    float cell = 6, x0, y0, w, h;
    long frame;

    public int Count => count;
    public ref readonly Request this[int i] => ref requests[i];

    /// <summary>Where label i goes this frame (its top left, UI px), on which side, and how strongly (0: not drawn).</summary>
    public (Vector2 At, LabelPlace Place, float Alpha) Result(int i) => (at[i], placed[i], alpha[i]);

    /// <summary>Starts a frame's labels over the viewport.</summary>
    public void Begin(Vector2 pos, Vector2 size, float cellPx)
    {
        (x0, y0, w, h, cell) = (pos.X, pos.Y, size.X, size.Y, Math.Max(cellPx, 2));
        count = 0;
        frame++;
    }

    /// <summary>Asks for a label of size (w, h) at anchor (x, y), UI px: its places in order of preference, past the
    /// anchor's <paramref name="gap"/>; <paramref name="reserve"/> the half size of the anchor's symbol, kept clear of
    /// every label. Lower priorities are placed first.</summary>
    public void Add(LabelKind kind, int key, int index, int priority, float x, float y, float width, float height, float gap,
        float reserve, LabelPlace p0, LabelPlace p1 = LabelPlace.None, LabelPlace p2 = LabelPlace.None, LabelPlace p3 = LabelPlace.None)
    {
        if (count == requests.Length)
        {
            int n = count * 2;
            Array.Resize(ref requests, n);
            Array.Resize(ref order, n);
            Array.Resize(ref at, n);
            Array.Resize(ref placed, n);
            Array.Resize(ref alpha, n);
        }
        requests[count++] = new Request
        {
            Key = (long)kind << 32 | (uint)key, Kind = kind, Index = index, Priority = priority,
            X = x, Y = y, W = width, H = height, Gap = gap, Reserve = reserve, P0 = p0, P1 = p1, P2 = p2, P3 = p3,
        };
    }

    /// <summary>Places the frame's labels; <paramref name="dt"/> the real seconds since the last frame (the fades).</summary>
    public void Place(float dt)
    {
        int gwNew = (int)(w / cell) + 1, ghNew = (int)(h / cell) + 1;
        if (grid.Length < gwNew * ghNew)
            grid = new bool[gwNew * ghNew];
        (gw, gh) = (gwNew, ghNew);
        Array.Clear(grid, 0, gw * gh);
        for (int i = 0; i < count; i++)
        {
            ref var r = ref requests[i];
            if (r.Reserve > 0)
                Mark(r.X - r.Reserve, r.Y - r.Reserve, 2 * r.Reserve, 2 * r.Reserve);
            order[i] = (long)r.Priority << 32 | (uint)i;
        }
        Array.Sort(order, 0, count);
        float step = SymbolLook.LabelFade > 0 ? dt / SymbolLook.LabelFade : 1;
        for (int o = 0; o < count; o++)
        {
            int i = (int)(order[o] & 0xFFFFFFFF);
            ref var r = ref requests[i];
            ref var st = ref CollectionsMarshal.GetValueRefOrAddDefault(states, r.Key, out bool known);
            var got = LabelPlace.None;
            Vector2 pos = default;
            // last frame's place first, while it's still one of this label's
            if (known && st.Place != LabelPlace.None && Allowed(r, st.Place) && TryAt(r, st.Place, out pos))
                got = st.Place;
            else
                foreach (var p in (ReadOnlySpan<LabelPlace>)[r.P0, r.P1, r.P2, r.P3])
                    if (p != LabelPlace.None && TryAt(r, p, out pos))
                    {
                        got = p;
                        break;
                    }
            if (got != LabelPlace.None)
            {
                Mark(pos.X, pos.Y, r.W, r.H);
                (st.Place, st.X, st.Y) = (got, pos.X - r.X, pos.Y - r.Y);
                st.Alpha = Math.Min(1, (known ? st.Alpha : 0) + step);
            }
            else
                st.Alpha = known ? Math.Max(0, st.Alpha - step) : 0;   // fading out where it was, over whatever took its place
            st.Seen = frame;
            placed[i] = got != LabelPlace.None ? got : st.Place;
            at[i] = new Vector2(r.X + st.X, r.Y + st.Y);
            alpha[i] = st.Alpha;
        }
        // forget the labels no longer asked for
        stale.Clear();
        foreach (var (k, st) in states)
            if (st.Seen != frame)
                stale.Add(k);
        foreach (long k in stale)
            states.Remove(k);
    }

    static bool Allowed(in Request r, LabelPlace p) => p == r.P0 || p == r.P1 || p == r.P2 || p == r.P3;

    bool TryAt(in Request r, LabelPlace p, out Vector2 pos)
    {
        pos = p switch
        {
            LabelPlace.Right => new Vector2(r.X + r.Gap, r.Y - r.H / 2),
            LabelPlace.Left => new Vector2(r.X - r.Gap - r.W, r.Y - r.H / 2),
            LabelPlace.Above => new Vector2(r.X - r.W / 2, r.Y - r.Gap - r.H),
            _ => new Vector2(r.X - r.W / 2, r.Y + r.Gap),
        };
        if (pos.X < x0 || pos.Y < y0 || pos.X + r.W > x0 + w || pos.Y + r.H > y0 + h)
            return false;
        int cx0 = (int)((pos.X - x0) / cell), cy0 = (int)((pos.Y - y0) / cell);
        int cx1 = Math.Min((int)((pos.X + r.W - x0) / cell), gw - 1), cy1 = Math.Min((int)((pos.Y + r.H - y0) / cell), gh - 1);
        for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
                if (grid[cx + cy * gw])
                    return false;
        return true;
    }

    void Mark(float x, float y, float width, float height)
    {
        int cx0 = Math.Max((int)((x - x0) / cell), 0), cy0 = Math.Max((int)((y - y0) / cell), 0);
        int cx1 = Math.Min((int)((x + width - x0) / cell), gw - 1), cy1 = Math.Min((int)((y + height - y0) / cell), gh - 1);
        for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
                grid[cx + cy * gw] = true;
    }

    static readonly Vector4 HaloRgba = new(0.035f, 0.05f, 0.06f, 0.6f);
    static readonly Vector2[] HaloAt = [new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)];

    /// <summary>A label's text with its halo (the ground's dark under it, a pixel round it, <see cref="SymbolLook.Halo"/>;
    /// 0: the old drop shadow), at <paramref name="alphaGain"/> of its colour.</summary>
    public static void Text(ImDrawListPtr dl, Vector2 pos, Vector4 colour, ReadOnlySpan<byte> text, float alphaGain)
    {
        float a = colour.W * alphaGain;
        if (a <= 0.004f)
            return;
        if (SymbolLook.Halo > 0)
        {
            uint halo = ImGui.GetColorU32(HaloRgba with { W = HaloRgba.W * Math.Min(1, a * 1.2f) * SymbolLook.Halo });
            float o = Math.Max(1, MathF.Round(UiFonts.Scale));
            foreach (var d in HaloAt)
                U8.Draw(dl, pos + d * o, halo, text);
        }
        else
            U8.Draw(dl, pos + new Vector2(1, 1), ImGui.GetColorU32(new Vector4(0, 0, 0, 0.75f * a)), text);
        U8.Draw(dl, pos, ImGui.GetColorU32(colour with { W = a }), text);
    }

    /// <summary><see cref="Text(ImDrawListPtr, Vector2, Vector4, ReadOnlySpan{byte}, float)"/> for a kept string.</summary>
    public static void Text(ImDrawListPtr dl, Vector2 pos, Vector4 colour, string text, float alphaGain)
    {
        float a = colour.W * alphaGain;
        if (a <= 0.004f)
            return;
        if (SymbolLook.Halo > 0)
        {
            uint halo = ImGui.GetColorU32(HaloRgba with { W = HaloRgba.W * Math.Min(1, a * 1.2f) * SymbolLook.Halo });
            float o = Math.Max(1, MathF.Round(UiFonts.Scale));
            foreach (var d in HaloAt)
                dl.AddText(pos + d * o, halo, text);
        }
        else
            dl.AddText(pos + new Vector2(1, 1), ImGui.GetColorU32(new Vector4(0, 0, 0, 0.75f * a)), text);
        dl.AddText(pos, ImGui.GetColorU32(colour with { W = a }), text);
    }
}
