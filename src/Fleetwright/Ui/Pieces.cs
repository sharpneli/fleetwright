using System.Numerics;
using ImGuiNET;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Ui;

/// <summary>
/// The UI language's pieces (docs/ui-language.md "The pieces"), one implementation for every game screen: panes with
/// title strips, sub-headers, table header rows and rows, group rows, steppers, drop-downs, segmented controls, buttons
/// with their keys, status chips, a work surface's tags and overlay toggles, the numbered tabs, the resource bar's cells,
/// the status bar. A new screen is built from these, not drawn afresh, so a change of style is a change here (the shapes)
/// or in <see cref="Theme"/> (the colours).
/// <para>Most are extension methods on a draw list, at screen positions, in <see cref="Theme"/>'s colours and at the fonts'
/// own sizes (<see cref="UiFonts"/>); sizes are reference pixels (1080 px tall = 1) through <see cref="P"/>. A piece that
/// takes clicks is an ImGui item under its drawing and returns what was clicked; whether a click may change anything is
/// the caller's to decide. A few are ImGui-flow pieces (at the cursor, for screens laid out by ImGui's layout): the
/// segmented button, the tooltip, a figure right-aligned in a table cell. Nothing here allocates per frame: the strings
/// are the caller's, and the text pieces take UTF-8 too (the map's <c>U8</c> text, written without garbage).</para>
/// </summary>
public static unsafe class Pieces
{
    /// <summary>Reference pixels to the UI's.</summary>
    public static float P(float px) => px * UiFonts.Scale;

    /// <summary>A pane's title strip, a table row, a table's header row.</summary>
    public static float TitleH => P(20);
    public static float RowH => P(20);
    public static float HeadH => P(18);

    public static uint U(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    // ------------------------------------------------------------------ text

    public static float Width(ImFontPtr f, string s) => f.CalcTextSizeA(f.FontSize, float.MaxValue, 0, s).X;

    /// <summary>Where text sits centred in a row <paramref name="h"/> tall from <paramref name="top"/>.</summary>
    public static float Mid(ImFontPtr f, float top, float h) => top + MathF.Round((h - f.FontSize) / 2);

    public static float WrappedHeight(ImFontPtr f, string s, float wrap) => f.CalcTextSizeA(f.FontSize, float.MaxValue, wrap, s).Y;

    /// <summary>Text at its font's own size.</summary>
    public static void Text(this ImDrawListPtr dl, ImFontPtr f, Vector2 at, Vector4 col, string s) => dl.AddText(f, f.FontSize, at, U(col), s);

    /// <summary>Text right-aligned at <paramref name="right"/>.</summary>
    public static void TextRight(this ImDrawListPtr dl, ImFontPtr f, float right, float y, Vector4 col, string s) =>
        dl.AddText(f, f.FontSize, new Vector2(right - Width(f, s), y), U(col), s);

    /// <summary>Text wrapped at <paramref name="wrap"/> wide; returns its height.</summary>
    public static float Wrapped(this ImDrawListPtr dl, ImFontPtr f, Vector2 at, Vector4 col, string s, float wrap)
    {
        dl.AddText(f, f.FontSize, at, U(col), s, wrap);
        return WrappedHeight(f, s, wrap);
    }

    // UTF-8 text: what a screen writes every frame without making strings (the map's U8)
    public static float Width(ImFontPtr f, ReadOnlySpan<byte> s)
    {
        Vector2 v;
        fixed (byte* p = s)
            ImGuiNative.ImFont_CalcTextSizeA(&v, f.NativePtr, f.FontSize, float.MaxValue, 0, p, p + s.Length, null);
        return v.X;
    }

    public static void Text(this ImDrawListPtr dl, ImFontPtr f, Vector2 at, Vector4 col, ReadOnlySpan<byte> s)
    {
        fixed (byte* p = s)
            ImGuiNative.ImDrawList_AddText_FontPtr(dl.NativePtr, f.NativePtr, f.FontSize, at, U(col), p, p + s.Length, 0, null);
    }

    public static void TextRight(this ImDrawListPtr dl, ImFontPtr f, float right, float y, Vector4 col, ReadOnlySpan<byte> s) =>
        dl.Text(f, new Vector2(right - Width(f, s), y), col, s);

    /// <summary>Text from <paramref name="at"/>, cut at <paramref name="right"/> (a cell's edge); returns where it ends.</summary>
    public static float TextClipped(this ImDrawListPtr dl, ImFontPtr f, Vector2 at, float right, Vector4 col, ReadOnlySpan<byte> s)
    {
        dl.PushClipRect(new Vector2(at.X, at.Y - P(2)), new Vector2(right, at.Y + f.FontSize + P(2)), true);
        dl.Text(f, at, col, s);
        dl.PopClipRect();
        return Math.Min(at.X + Width(f, s), right);
    }

    public static void HLine(this ImDrawListPtr dl, float x0, float x1, float y, Vector4 col) => dl.AddLine(new Vector2(x0, y), new Vector2(x1, y), U(col));

    /// <summary>Text cut to a width with an ellipsis (the string itself when it fits). Allocates when it cuts: call it
    /// when what's shown changes, not every frame.</summary>
    public static string Clip(ImFontPtr font, string s, float width)
    {
        if (Width(font, s) <= width)
            return s;
        int n = s.Length;
        while (n > 0 && Width(font, s[..n] + "…") > width)
            n--;
        return s[..n].TrimEnd(' ', ',') + "…";
    }

    // ------------------------------------------------------------------ panes

    /// <summary>A pane: its body, the near-black edge round it with a bevel inside, its title strip (a rivet, then the
    /// name in the accent's engraved caps, at most one note after it); returns where the strip's right end is, for its
    /// toggles or count.</summary>
    public static float Pane(this ImDrawListPtr dl, Vector2 a, Vector2 size, string title, string? note = null)
    {
        var b = a + size;
        dl.AddRectFilled(a, b, U(T.Panel));
        dl.AddRectFilledMultiColor(a, a + new Vector2(size.X, TitleH), U(T.TitleTop), U(T.TitleTop), U(T.TitleBottom), U(T.TitleBottom));
        dl.HLine(a.X + 1, b.X - 1, a.Y + 1, T.Bevel);
        dl.HLine(a.X, b.X, a.Y + TitleH, T.Edge);
        dl.HLine(a.X + 1, b.X - 1, a.Y + TitleH + 1, T.Bevel);
        dl.AddRect(a, b, U(T.Edge));
        float x = a.X + P(10);
        dl.Rivet(new Vector2(x, a.Y + TitleH / 2));
        x += P(7);
        dl.Text(UiFonts.Head, new Vector2(x, Mid(UiFonts.Head, a.Y, TitleH)), T.Brass, title);
        if (note != null)
            dl.Text(UiFonts.SansSmall, new Vector2(x + P(6) + Width(UiFonts.Head, title), Mid(UiFonts.SansSmall, a.Y, TitleH)), T.Muted, note);
        return b.X - P(6);
    }

    /// <summary>A title strip's rivet: a 5 px graphite dot, lit at its top.</summary>
    public static void Rivet(this ImDrawListPtr dl, Vector2 c)
    {
        dl.AddCircleFilled(c, P(2.5f), U(T.Rivet), 12);
        dl.PathArcTo(c, P(2), MathF.PI * 1.15f, MathF.PI * 1.85f, 6);
        dl.PathStroke(U(new Vector4(1, 1, 1, 0.2f)), ImDrawFlags.None, 1);
    }

    /// <summary>A muted note at a title strip's or sub-header's right end; returns where it starts.</summary>
    public static float RightNote(this ImDrawListPtr dl, float right, float top, float h, string s)
    {
        float x = right - Width(UiFonts.SansSmall, s);
        dl.Text(UiFonts.SansSmall, new Vector2(x, Mid(UiFonts.SansSmall, top, h)), T.Muted, s);
        return x;
    }

    /// <summary>A pane's body as a child window under its title strip (scrolling when its content is longer), and under
    /// <paramref name="head"/> more of fixed rows (a sub-header, a table's header row); <paramref name="body"/> is its draw
    /// list. End it with <see cref="EndBody"/> whatever this returns.</summary>
    public static bool BeginBody(string id, Vector2 a, Vector2 size, out ImDrawListPtr body, bool scroll = true, float head = 0)
    {
        ImGui.SetCursorScreenPos(a + new Vector2(1, TitleH + 1 + head));
        bool open = ImGui.BeginChild(id, size - new Vector2(2, TitleH + 2 + head), ImGuiChildFlags.None,
            scroll ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        body = ImGui.GetWindowDrawList();
        return open;
    }

    /// <summary>A body's (or any child window's) end: its content <paramref name="height"/> tall, for the scrollbar.</summary>
    public static void EndBody(float height)
    {
        ImGui.SetCursorPos(new Vector2(0, Math.Max(height, 0)));
        ImGui.Dummy(new Vector2(1, 0));
        ImGui.EndChild();
    }

    /// <summary>A sub-header splitting a pane: a small caps label, a muted note, controls at the right (returned: where
    /// they end).</summary>
    public static float SubHeader(this ImDrawListPtr dl, Vector2 a, float w, string cap, string? note = null)
    {
        dl.AddRectFilled(a, a + new Vector2(w, RowH), U(T.HeaderBg));
        dl.HLine(a.X, a.X + w, a.Y + RowH - 1, T.Line);
        dl.Text(UiFonts.SansSmall, new Vector2(a.X + P(6), Mid(UiFonts.SansSmall, a.Y, RowH)), T.Text, cap);
        if (note != null)
            dl.Text(UiFonts.SansSmall, new Vector2(a.X + P(14) + Width(UiFonts.SansSmall, cap), Mid(UiFonts.SansSmall, a.Y, RowH)), T.Muted, note);
        return a.X + w - P(6);
    }

    /// <summary>A table's header row; its cells by <see cref="HeadCell"/>.</summary>
    public static void Head(this ImDrawListPtr dl, Vector2 a, float w)
    {
        dl.AddRectFilled(a, a + new Vector2(w, HeadH), U(T.HeaderBg));
        dl.HLine(a.X, a.X + w, a.Y + HeadH - 1, T.Edge);
    }

    /// <summary>A header cell: from <paramref name="x"/>, or right-aligned at it.</summary>
    public static void HeadCell(this ImDrawListPtr dl, Vector2 a, float x, string s, bool right = false)
    {
        float y = Mid(UiFonts.SansSmall, a.Y, HeadH);
        if (right)
            dl.TextRight(UiFonts.SansSmall, x, y, T.Muted, s);
        else
            dl.Text(UiFonts.SansSmall, new Vector2(x, y), T.Muted, s);
    }

    /// <summary>A header row with a label at the left and a note at the right, at the ImGui cursor across
    /// <paramref name="w"/>; the cursor goes under it.</summary>
    public static void HeadStrip(this ImDrawListPtr dl, float w, string label, string? note)
    {
        var p = ImGui.GetCursorScreenPos();
        dl.Head(p, w);
        dl.HeadCell(p, p.X + P(6), label);
        if (note != null)
            dl.HeadCell(p, p.X + w - P(6), note, right: true);
        ImGui.Dummy(new Vector2(w, HeadH));
        ImGui.SetCursorScreenPos(p + new Vector2(0, HeadH));
    }

    /// <summary>A table row's ground: every other a shade lighter, a rule under it; tinted under the pointer, warm with a
    /// brass bar when selected.</summary>
    public static void RowBg(this ImDrawListPtr dl, Vector2 a, float w, float h, int n, bool selected = false, bool hover = false)
    {
        var b = a + new Vector2(w, h);
        if (selected)
            dl.AddRectFilled(a, b, U(T.Selected));
        else if (hover)
            dl.AddRectFilled(a, b, U(T.Hover));
        else if (n % 2 == 1)
            dl.AddRectFilled(a, b, U(T.RowAlt));
        dl.HLine(a.X, b.X, b.Y - 1, T.Line);
        if (selected)
            dl.AddRectFilled(a, new Vector2(a.X + P(3), b.Y), U(T.Brass));
    }

    /// <summary>A group row's ground (<c>Panel2</c>).</summary>
    public static void GroupBg(this ImDrawListPtr dl, Vector2 a, float w, float h, bool hover = false)
    {
        dl.AddRectFilled(a, a + new Vector2(w, h), U(hover ? T.Hover : T.Panel2));
        dl.HLine(a.X, a.X + w, a.Y + h - 1, T.Line);
    }

    /// <summary>A row's hit area, letting the controls drawn over it take their own clicks: (clicked, hovered). A row
    /// answers a click only when the pointer was over it the frame before (ImGui's rule for items that allow overlap):
    /// a scripted click moves the pointer there first.</summary>
    public static (bool Clicked, bool Hover) HitRow(string id, Vector2 a, Vector2 size)
    {
        ImGui.SetCursorScreenPos(a);
        ImGui.SetNextItemAllowOverlap();
        bool click = ImGui.InvisibleButton(id, size);
        return (click, ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlappedByItem));
    }

    /// <summary>A bar of <paramref name="frac"/> in a well (the ledger's weights, a share).</summary>
    public static void Bar(this ImDrawListPtr dl, Vector2 p, Vector2 size, float frac, Vector4 colour)
    {
        dl.AddRectFilled(p, p + size, U(T.Inset));
        dl.AddRectFilled(p, p + new Vector2(size.X * Math.Clamp(frac, 0, 1), size.Y), U(colour));
    }

    /// <summary>A ledger bar of a segment a member (a unit's ships), each filled in its kind's colour
    /// (<paramref name="colours"/>[kind]), 1 px apart while each is 2 px or more, else touching; a 1 px edge round it.</summary>
    public static void SegmentBar(this ImDrawListPtr dl, Vector2 a, Vector2 size, ReadOnlySpan<byte> kinds, ReadOnlySpan<Vector4> colours)
    {
        dl.AddRectFilled(a, a + size, U(T.Inset));
        int n = kinds.Length;
        if (n > 0)
        {
            float inner = size.X - 2, gap = (inner - (n - 1)) / n >= 2 ? 1 : 0, seg = (inner - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                float x0 = a.X + 1 + i * (seg + gap);
                dl.AddRectFilled(new Vector2(MathF.Round(x0), a.Y + 1), new Vector2(MathF.Round(x0 + seg), a.Y + size.Y - 1), U(colours[kinds[i]]));
            }
        }
        dl.AddRect(a, a + size, U(T.Edge));
    }

    /// <summary>A ledger bar of two sides: the friend's share cream from the left, the rest the enemy's.</summary>
    public static void ShareBar(this ImDrawListPtr dl, Vector2 a, float w, float h, float friend)
    {
        dl.AddRectFilled(a, a + new Vector2(w, h), U(T.HostileBar));
        dl.AddRectFilled(a, a + new Vector2(w * Math.Clamp(friend, 0, 1), h), U(T.FriendBar));
        dl.AddRect(a, a + new Vector2(w, h), U(T.Edge));
    }

    // ------------------------------------------------------------------ marks (the fonts have no ▼ ◀ ▶ ✕ ○: drawn)

    /// <summary>A small filled triangle pointing <paramref name="dir"/> (0 right, 1 down, 2 left, 3 up).</summary>
    public static void Arrow(this ImDrawListPtr dl, Vector2 c, int dir, float r, Vector4 col)
    {
        Vector2 f = dir switch { 0 => new(1, 0), 1 => new(0, 1), 2 => new(-1, 0), _ => new(0, -1) }, s = new(-f.Y, f.X);
        dl.AddTriangleFilled(c + f * r, c - f * r * 0.7f + s * r * 0.9f, c - f * r * 0.7f - s * r * 0.9f, U(col));
    }

    /// <summary>A twisty: ▼ open, ▶ shut.</summary>
    public static void Twisty(this ImDrawListPtr dl, Vector2 c, bool open, Vector4? colour = null)
    {
        float r = P(3.5f);
        uint col = U(colour ?? T.Muted);
        if (open)
            dl.AddTriangleFilled(c + new Vector2(-r, -r * 0.6f), c + new Vector2(r, -r * 0.6f), c + new Vector2(0, r * 0.8f), col);
        else
            dl.AddTriangleFilled(c + new Vector2(-r * 0.6f, -r), c + new Vector2(r * 0.8f, 0), c + new Vector2(-r * 0.6f, r), col);
    }

    public static void Cross(this ImDrawListPtr dl, Vector2 c, float r, Vector4 col)
    {
        dl.AddLine(c - new Vector2(r, r), c + new Vector2(r, r), U(col), P(1.2f));
        dl.AddLine(c + new Vector2(-r, r), c + new Vector2(r, -r), U(col), P(1.2f));
    }

    /// <summary>A ✕ that takes a click (a row's remove, a change's undo): true when clicked.</summary>
    public static bool CrossButton(this ImDrawListPtr dl, string id, Vector2 c, string tip)
    {
        ImGui.SetCursorScreenPos(c - new Vector2(P(8), P(9)));
        bool hit = ImGui.InvisibleButton(id, new Vector2(P(16), P(18)));
        bool hover = ImGui.IsItemHovered();
        dl.Cross(c, P(3.5f), hover ? T.Bright : T.Muted);
        if (hover)
            ImGui.SetTooltip(tip);
        return hit;
    }

    /// <summary>A radio mark: ◉ on, ○ off.</summary>
    public static void Radio(this ImDrawListPtr dl, Vector2 c, bool on)
    {
        dl.AddCircle(c, P(5), U(on ? T.Brass : T.Muted), 16, P(1.2f));
        if (on)
            dl.AddCircleFilled(c, P(2.5f), U(T.Brass), 12);
    }

    /// <summary>Work under way: a turning arc (a status bar's dot while it builds).</summary>
    public static void Busy(this ImDrawListPtr dl, Vector2 c, float r)
    {
        float a = (float)(ImGui.GetTime() * 6);
        dl.PathArcTo(c, r, a, a + 4.2f, 16);
        dl.PathStroke(U(T.Brass), ImDrawFlags.None, P(1.5f));
    }

    // ------------------------------------------------------------------ controls

    /// <summary>A stepper (◀ inset value ▶): its width. <paramref name="by"/> is the step asked for (Shift ×4; the wheel
    /// over the value steps too), else 0.</summary>
    public static float Stepper(this ImDrawListPtr dl, string id, Vector2 at, string value, float valueW, bool down, bool up, out int by)
    {
        by = 0;
        float bw = P(13), h = P(15), w = 2 * bw + valueW + P(4);
        ImGui.PushID(id);
        int mul = ImGui.GetIO().KeyShift ? 4 : 1;
        for (int k = 0; k < 2; k++)
        {
            var a = at + new Vector2(k == 0 ? 0 : bw + valueW + P(4), 0);
            bool on = k == 0 ? down : up;
            ImGui.SetCursorScreenPos(a);
            if (ImGui.InvisibleButton(k == 0 ? "##dn" : "##up", new Vector2(bw, h)) && on)
                by = k == 0 ? -mul : mul;
            bool hover = on && ImGui.IsItemHovered();
            dl.AddRectFilled(a, a + new Vector2(bw, h), U(hover ? T.Button : T.StepBox));
            dl.AddRect(a, a + new Vector2(bw, h), U(T.StepEdge));
            dl.Arrow(a + new Vector2(bw, h) / 2, k == 0 ? 2 : 0, P(3), on ? T.Bright : T.StepEdge);
        }
        var v = at + new Vector2(bw + P(2), 0);
        dl.AddRectFilled(v, v + new Vector2(valueW, h), U(T.Inset));
        dl.AddRect(v, v + new Vector2(valueW, h), U(T.InsetEdge));
        dl.TextRight(UiFonts.Mono, v.X + valueW - P(4), Mid(UiFonts.Mono, v.Y, h), T.Bright, value);
        ImGui.SetCursorScreenPos(v);
        ImGui.InvisibleButton("##v", new Vector2(valueW, h));
        if (ImGui.IsItemHovered())
        {
            ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY);   // the value takes the wheel, the pane doesn't scroll
            float wheel = ImGui.GetIO().MouseWheel;
            if (wheel > 0 && up)
                by = mul;
            else if (wheel < 0 && down)
                by = -mul;
        }
        ImGui.PopID();
        return w;
    }

    /// <summary>A drop-down (an inset field, a square ▼ box at its end): the option picked, else -1. Its value is brass when
    /// it isn't as written.</summary>
    public static int DropDown(this ImDrawListPtr dl, string id, Vector2 at, float w, string now, string[] options, bool changed = false)
    {
        int picked = -1;
        float h = P(16);
        ImGui.SetCursorScreenPos(at);
        if (ImGui.InvisibleButton(id, new Vector2(w, h)))
            ImGui.OpenPopup(id);
        bool hover = ImGui.IsItemHovered();
        dl.AddRectFilled(at, at + new Vector2(w, h), U(T.Inset));
        dl.AddRect(at, at + new Vector2(w, h), U(hover ? T.ButtonEdge : T.InsetEdge));
        var box = new Vector2(at.X + w - P(15), at.Y + 1);
        dl.AddRectFilled(box, new Vector2(at.X + w - 1, at.Y + h - 1), U(T.DropBox));
        dl.Arrow(box + new Vector2(P(7), (h - 2) / 2), 1, P(3), T.Bright);
        dl.PushClipRect(at, new Vector2(box.X - P(2), at.Y + h), true);
        dl.Text(UiFonts.SansSmall, new Vector2(at.X + P(5), Mid(UiFonts.SansSmall, at.Y, h)), changed ? T.Brass : T.Bright, now);
        dl.PopClipRect();
        ImGui.SetNextWindowPos(at + new Vector2(0, h));
        ImGui.SetNextWindowSizeConstraints(new Vector2(w, 0), new Vector2(Math.Max(w, P(420)), P(420)));
        if (ImGui.BeginPopup(id))
        {
            ImGui.PushFont(UiFonts.SansSmall);
            for (int i = 0; i < options.Length; i++)
                if (ImGui.Selectable(options[i], options[i] == now))
                    picked = i;
            ImGui.PopFont();
            ImGui.EndPopup();
        }
        return picked;
    }

    /// <summary>The width of a segmented control of <paramref name="labels"/>.</summary>
    public static float SegmentedWidth(string[] labels)
    {
        float w = 1;
        foreach (var l in labels)
            w += Width(UiFonts.SansSmall, l) + P(14) - 1;
        return w;
    }

    /// <summary>A segmented control: joined grey buttons, the chosen one brass on dark brass. The one clicked, else -1;
    /// <paramref name="width"/> its whole width.</summary>
    public static int Segmented(this ImDrawListPtr dl, string id, Vector2 at, string[] labels, int on, out float width, bool enabled = true)
    {
        int hit = -1;
        float x = at.X, h = P(16), onX = 0, onW = 0;
        ImGui.PushID(id);
        for (int i = 0; i < labels.Length; i++)
        {
            float w = Width(UiFonts.SansSmall, labels[i]) + P(14);
            var a = new Vector2(x, at.Y);
            ImGui.SetCursorScreenPos(a);
            ImGui.PushID(i);
            if (ImGui.InvisibleButton("##seg", new Vector2(w, h)) && i != on && enabled)
                hit = i;
            ImGui.PopID();
            bool hover = enabled && ImGui.IsItemHovered();
            bool sel = i == on;
            dl.AddRectFilled(a, a + new Vector2(w, h), U(sel ? T.BrassDarker : hover ? T.ButtonHover : T.Button));
            if (!sel)
                dl.AddRect(a, a + new Vector2(w, h), U(T.ButtonEdge));
            else
                (onX, onW) = (x, w);
            dl.Text(UiFonts.SansSmall, new Vector2(a.X + P(7), Mid(UiFonts.SansSmall, a.Y, h)), sel ? T.Brass : enabled ? T.Text : T.Muted, labels[i]);
            x += w - 1;
        }
        if (onW > 0)   // the chosen one's brass border over its neighbours' grey
            dl.AddRect(new Vector2(onX, at.Y), new Vector2(onX + onW, at.Y + h), U(T.Brass));
        ImGui.PopID();
        width = x + 1 - at.X;
        return hit;
    }

    /// <summary>A filter that's on or off on its own: a single segment, brass when on, laid rightwards from
    /// <paramref name="x"/> (moved past it). True when clicked.</summary>
    public static bool Flag(this ImDrawListPtr dl, string id, ref float x, float y, string label, bool on)
    {
        float w = Width(UiFonts.SansSmall, label) + P(14), h = P(16);
        var a = new Vector2(x, y);
        ImGui.SetCursorScreenPos(a);
        bool hit = ImGui.InvisibleButton(id, new Vector2(w, h));
        bool hover = ImGui.IsItemHovered();
        dl.AddRectFilled(a, a + new Vector2(w, h), U(on ? T.BrassDarker : hover ? T.ButtonHover : T.Button));
        dl.AddRect(a, a + new Vector2(w, h), U(on ? T.Brass : T.ButtonEdge));
        dl.Text(UiFonts.SansSmall, new Vector2(a.X + P(7), Mid(UiFonts.SansSmall, a.Y, h)), on ? T.Brass : T.Text, label);
        x += w + P(3);
        return hit;
    }

    /// <summary>A key with shift is written "Shift+…" and drawn with shift as a mark (the fonts have no ⇧).</summary>
    const string ShiftPrefix = "Shift+";

    /// <summary>A kbd chip's width.</summary>
    public static float KbdWidth(string key)
    {
        bool shift = key.StartsWith(ShiftPrefix, StringComparison.Ordinal);
        var rest = shift ? key.AsSpan(ShiftPrefix.Length) : key;
        return KeyWidth(rest) + (shift ? P(15) : P(6));
    }

    /// <summary>The fonts have ↑ ↓ but no ← →: those are drawn (<see cref="SideArrow"/>), 0 left, 1 right; else -1.</summary>
    static int SideArrowOf(ReadOnlySpan<char> key) => key is "←" ? 0 : key is "→" ? 1 : -1;

    static float KeyWidth(ReadOnlySpan<char> key) => SideArrowOf(key) >= 0 ? P(8) : Width(UiFonts.MonoSmall, key);

    static float Width(ImFontPtr f, ReadOnlySpan<char> s)
    {
        Span<byte> b = stackalloc byte[64];
        int n = System.Text.Encoding.UTF8.GetBytes(s, b);
        return Width(f, (ReadOnlySpan<byte>)b[..n]);
    }

    /// <summary>A kbd chip: its width. Shift is drawn as an outlined arrow up before the key.</summary>
    public static float Kbd(this ImDrawListPtr dl, Vector2 at, string key, Vector4? edge = null, Vector4? ink = null)
    {
        float w = KbdWidth(key), h = P(13);
        var e = U(edge ?? T.TagEdge);
        dl.AddRect(at, at + new Vector2(w, h), e, P(2));
        dl.AddLine(new Vector2(at.X + 1, at.Y + h - 1), new Vector2(at.X + w - 1, at.Y + h - 1), e);
        var col = ink ?? T.KeyText;
        bool shift = key.StartsWith(ShiftPrefix, StringComparison.Ordinal);
        var rest = shift ? key.AsSpan(ShiftPrefix.Length) : key;
        float x = at.X + P(3);
        if (shift)
        {
            dl.ShiftMark(new Vector2(at.X + P(6.5f), at.Y + (h - 1) / 2), col);
            x = at.X + P(12);
        }
        if (SideArrowOf(rest) is int dir and >= 0)
            dl.SideArrow(new Vector2(x + P(4), at.Y + (h - 1) / 2), dir, col);
        else
        {
            Span<byte> b = stackalloc byte[64];
            int n = System.Text.Encoding.UTF8.GetBytes(rest, b);
            dl.Text(UiFonts.MonoSmall, new Vector2(x, Mid(UiFonts.MonoSmall, at.Y, h - 1)), col, (ReadOnlySpan<byte>)b[..n]);
        }
        return w;
    }

    /// <summary>A key's arrow pointing left (0) or right (1), centred on <paramref name="c"/>: a shaft and a head.</summary>
    public static void SideArrow(this ImDrawListPtr dl, Vector2 c, int dir, Vector4 col)
    {
        float r = P(3.5f), s = dir == 0 ? -1 : 1;
        uint u = U(col);
        dl.AddLine(new Vector2(c.X - r, c.Y), new Vector2(c.X + r, c.Y), u, 1);
        dl.AddLine(new Vector2(c.X + s * r, c.Y), new Vector2(c.X + s * (r - P(2.5f)), c.Y - P(2.5f)), u, 1);
        dl.AddLine(new Vector2(c.X + s * r, c.Y), new Vector2(c.X + s * (r - P(2.5f)), c.Y + P(2.5f)), u, 1);
    }

    /// <summary>Shift's mark: an outlined arrow up, centred on <paramref name="c"/>.</summary>
    public static void ShiftMark(this ImDrawListPtr dl, Vector2 c, Vector4 col)
    {
        float r = P(3.5f), s = P(1.6f), y0 = c.Y - P(4), y1 = c.Y + P(4), knee = c.Y;
        Span<Vector2> p = stackalloc Vector2[7];
        p[0] = new(c.X, y0);
        p[1] = new(c.X + r, knee);
        p[2] = new(c.X + s, knee);
        p[3] = new(c.X + s, y1);
        p[4] = new(c.X - s, y1);
        p[5] = new(c.X - s, knee);
        p[6] = new(c.X - r, knee);
        dl.AddPolyline(ref p[0], p.Length, U(col), ImDrawFlags.Closed, 1);
    }

    /// <summary>A button's light: lit as the order in force (the accent on its tint), or armed, waiting for a click on
    /// the map (amber).</summary>
    public enum Lit : byte { None, On, Armed }

    /// <summary>A menu button's caret room.</summary>
    static float CaretW => P(11);

    public static float ButtonWidth(string label, string? key, bool primary = false, bool menu = false) =>
        Width(primary ? UiFonts.SansBold : UiFonts.SansSmall, label) + P(16) + (key != null ? KbdWidth(key) + P(6) : 0) + (menu ? CaretW : 0);

    /// <summary>A button: 20 px, grey, its key in a kbd chip after the word; the primary one filled with the accent, dark
    /// ink on it, never larger; lit on its tint, or amber while armed; a menu's with a caret after it. <paramref name="w"/>
    /// stretches it (0: to its label).</summary>
    public static bool Button(this ImDrawListPtr dl, string id, Vector2 at, string label, string? key = null, bool primary = false, bool enabled = true,
        float w = 0, Lit lit = Lit.None, bool menu = false)
    {
        if (w <= 0)
            w = ButtonWidth(label, key, primary, menu);
        float h = P(20);
        ImGui.SetCursorScreenPos(at);
        bool hit = ImGui.InvisibleButton(id, new Vector2(w, h)) && enabled;
        bool hover = enabled && ImGui.IsItemHovered();
        var top = primary ? (hover ? T.PrimaryHover : T.PrimaryTop) : hover ? T.ButtonHover : T.Button;
        var fill = primary ? T.PrimaryBottom : hover ? T.Button : T.ToggleBottom;
        var edge = primary ? T.BrassDark : T.ButtonEdge;
        var ink = primary ? T.PrimaryInk : T.Text;
        Vector4? keyEdge = primary ? T.BrassDark : null, keyInk = primary ? T.PrimaryInk : null;
        if (!enabled)
            (top, fill, edge, ink, keyEdge, keyInk) = (T.Panel2, T.Panel2, T.Line, T.Muted, T.Line, T.Faint);
        else if (lit == Lit.On)
            (top, fill, edge, ink, keyEdge, keyInk) = (hover ? T.BrassHover : T.BrassDarker, T.BrassDarker, T.Brass, T.Brass, T.BrassDark, T.Brass);
        else if (lit == Lit.Armed)
            (top, fill, edge, ink, keyEdge, keyInk) = (T.AmberTint, T.AmberTint, T.Amber, T.Amber, T.AmberEdge, T.Amber);
        dl.AddRectFilledMultiColor(at, at + new Vector2(w, h), U(top), U(top), U(fill), U(fill));
        dl.AddRect(at, at + new Vector2(w, h), U(edge));
        var font = primary ? UiFonts.SansBold : UiFonts.SansSmall;
        float content = Width(font, label) + (key != null ? KbdWidth(key) + P(6) : 0) + (menu ? CaretW : 0);
        float x = at.X + MathF.Round((w - content) / 2);
        dl.Text(font, new Vector2(x, Mid(font, at.Y, h)), ink, label);
        x += Width(font, label);
        if (key != null)
            x += P(6) + dl.Kbd(new Vector2(x + P(6), at.Y + P(3.5f)), key, keyEdge, keyInk);
        if (menu)
            dl.Arrow(new Vector2(x + P(7), at.Y + h / 2 + P(0.5f)), 1, P(2.8f), ink);
        return hit;
    }

    /// <summary>A lit button's edge drawn again over its neighbours' in a joined row (they share their edges).</summary>
    public static void LitEdge(this ImDrawListPtr dl, Vector2 a, float w, Lit lit)
    {
        if (lit != Lit.None)
            dl.AddRect(a, a + new Vector2(w, P(20)), U(lit == Lit.On ? T.Brass : T.Amber));
    }

    /// <summary>A link-like action in a row (brass, or muted; bright under the pointer): true when clicked; its width out.</summary>
    public static bool Link(this ImDrawListPtr dl, string id, Vector2 at, string label, out float w, bool muted = false)
    {
        w = Width(UiFonts.SansSmall, label);
        ImGui.SetCursorScreenPos(at);
        bool hit = ImGui.InvisibleButton(id, new Vector2(w, P(16)));
        bool hover = ImGui.IsItemHovered();
        dl.Text(UiFonts.SansSmall, new Vector2(at.X, Mid(UiFonts.SansSmall, at.Y, P(16))), hover ? T.Bright : muted ? T.Muted : T.Brass, label);
        return hit;
    }

    /// <summary>A status chip's colour: ready, a consequence, plain, the enemy or a ship going down, or quiet (a state
    /// that's the usual one: the AI's unit beside the player's plain "you").</summary>
    public enum ChipKind { Ok, Amber, Plain, Hostile, Quiet, Lamp }

    public static float ChipWidth(string label) => Width(UiFonts.SansSmall, label) + P(8);
    public static float ChipWidth(ReadOnlySpan<byte> label) => Width(UiFonts.SansSmall, label) + P(8);

    /// <summary>A status chip: small text in a 1 px border on a dark tint of its colour; its width.</summary>
    public static float Chip(this ImDrawListPtr dl, Vector2 at, string label, ChipKind k)
    {
        float w = ChipWidth(label);
        ChipGround(dl, at, w, k, out var ink);
        dl.Text(UiFonts.SansSmall, new Vector2(at.X + P(4), Mid(UiFonts.SansSmall, at.Y, P(15))), ink, label);
        return w;
    }

    public static float Chip(this ImDrawListPtr dl, Vector2 at, ReadOnlySpan<byte> label, ChipKind k)
    {
        float w = ChipWidth(label);
        ChipGround(dl, at, w, k, out var ink);
        dl.Text(UiFonts.SansSmall, new Vector2(at.X + P(4), Mid(UiFonts.SansSmall, at.Y, P(15))), ink, label);
        return w;
    }

    static void ChipGround(ImDrawListPtr dl, Vector2 at, float w, ChipKind k, out Vector4 ink)
    {
        Vector4 bg, edge;
        (bg, edge, ink) = k switch
        {
            ChipKind.Ok => (T.OkTint, T.OkEdge, T.Ok),
            ChipKind.Amber => (T.AmberTint, T.AmberEdge, T.Amber),
            ChipKind.Hostile => (T.HostileTint, T.HostileEdge, T.Hostile),
            ChipKind.Quiet => (T.Panel2, T.ButtonEdge, T.Muted),
            ChipKind.Lamp => (T.LampTint, T.LampEdge, T.Lamp),
            _ => (T.PlainTint, T.PlainEdge, T.Bright),
        };
        dl.AddRectFilled(at, at + new Vector2(w, P(15)), U(bg));
        dl.AddRect(at, at + new Vector2(w, P(15)), U(edge));
    }

    public static Vector2 TagSize(string s, ImFontPtr? font = null)
    {
        var f = font ?? UiFonts.MonoSmall;
        return new(Width(f, s) + P(6), f.FontSize + P(2));
    }

    /// <summary>A work surface's tag: mono (or <paramref name="font"/>) in a dark translucent box with a 1 px border,
    /// from its top left.</summary>
    public static void Tag(this ImDrawListPtr dl, Vector2 at, string s, Vector4? ink = null, ImFontPtr? font = null)
    {
        var f = font ?? UiFonts.MonoSmall;
        var size = TagSize(s, f);
        dl.AddRectFilled(at, at + size, U(T.TagBg));
        dl.AddRect(at, at + size, U(T.TagEdge));
        dl.Text(f, at + new Vector2(P(3), P(1)), ink ?? T.TagText, s);
    }

    public static Vector2 TagSize(ReadOnlySpan<byte> s) => new(Width(UiFonts.MonoSmall, s) + P(6), UiFonts.MonoSmall.FontSize + P(2));

    public static void Tag(this ImDrawListPtr dl, Vector2 at, ReadOnlySpan<byte> s, Vector4? ink = null)
    {
        var size = TagSize(s);
        dl.AddRectFilled(at, at + size, U(T.TagBg));
        dl.AddRect(at, at + size, U(T.TagEdge));
        dl.Text(UiFonts.MonoSmall, at + new Vector2(P(3), P(1)), ink ?? T.TagText, s);
    }

    /// <summary>A tag centred on a point (a mount's on the plan).</summary>
    public static void TagCentred(this ImDrawListPtr dl, Vector2 centre, string s, ImFontPtr? font = null) =>
        dl.Tag(centre - TagSize(s, font) / 2, s, null, font);

    /// <summary>An overlay toggle in a title strip (a boxed letter and a word; brass when on), laid leftwards from
    /// <paramref name="right"/> (moved past it): true when clicked.</summary>
    public static bool Toggle(this ImDrawListPtr dl, string id, ref float right, float top, string key, string label, bool on)
    {
        float w = P(22) + Width(UiFonts.SansSmall, label), h = P(15);
        right -= w;
        var a = new Vector2(right, top + MathF.Round((TitleH - h) / 2));
        ImGui.SetCursorScreenPos(a);
        bool hit = ImGui.InvisibleButton(id, new Vector2(w, h));
        bool hover = ImGui.IsItemHovered();
        var top0 = on ? T.BrassDark : T.ToggleTop;
        var bottom = on ? T.BrassDarker : T.ToggleBottom;
        dl.AddRectFilledMultiColor(a, a + new Vector2(w, h), U(top0), U(top0), U(bottom), U(bottom));
        dl.AddRect(a, a + new Vector2(w, h), U(T.Edge));
        var fg = on ? T.BrassLight : hover ? T.Text : T.Muted;
        dl.AddRect(a + new Vector2(P(3), P(2)), a + new Vector2(P(14), P(13)), U(fg), P(2));
        dl.Text(UiFonts.SansSmall, new Vector2(a.X + P(5), Mid(UiFonts.SansSmall, a.Y, h)), fg, key);
        dl.Text(UiFonts.SansSmall, new Vector2(a.X + P(18), Mid(UiFonts.SansSmall, a.Y, h)), fg, label);
        right -= P(3);
        return hit;
    }

    /// <summary>The numbered tabs (the designer's 0–7, the launch screen's 1–3) from <paramref name="a"/>, the strip
    /// 22 px tall, each with its key boxed; the chosen one taller, joined to the pane under it. The one clicked, else -1.</summary>
    public static int NumberedTabs(this ImDrawListPtr dl, Vector2 a, float width, string[] names, string[] keys, int on)
    {
        int hit = -1;
        float x = 0;
        for (int i = 0; i < names.Length; i++)
        {
            bool sel = i == on;
            float w = P(28) + Width(UiFonts.SansBold, names[i]), h = sel ? P(22) : P(20);
            var ta = new Vector2(a.X + x, a.Y + P(22) - h);
            ImGui.SetCursorScreenPos(ta);
            ImGui.PushID(i);
            if (ImGui.InvisibleButton("##tab", new Vector2(w, h)))
                hit = i;
            bool hover = ImGui.IsItemHovered();
            ImGui.PopID();
            var top = sel ? T.TitleTop : hover ? T.TabHover : T.TabTop;
            var bottom = sel ? T.Panel : T.TabBottom;
            dl.AddRectFilledMultiColor(ta, ta + new Vector2(w, h), U(top), U(top), U(bottom), U(bottom));
            dl.AddLine(ta, ta + new Vector2(w, 0), U(T.Edge));
            dl.AddLine(ta, ta + new Vector2(0, h), U(T.Edge));
            dl.AddLine(ta + new Vector2(w, 0), ta + new Vector2(w, h), U(T.Edge));
            var key = ta + new Vector2(P(6), (h - P(13)) / 2);
            dl.AddRect(key, key + new Vector2(P(12), P(13)), U(sel ? T.Brass : T.TagEdge), P(2));
            dl.Text(UiFonts.SansSmall, key + new Vector2(P(3), 0), sel ? T.Brass : T.Muted, keys[i]);
            dl.Text(UiFonts.SansBold, new Vector2(ta.X + P(22), Mid(UiFonts.SansBold, ta.Y, h)), sel ? T.Brass : hover ? T.Text : T.Muted, names[i]);
            x += w + P(2);
            if (x > width)
                break;
        }
        return hit;
    }

    /// <summary>A dock's tab (the ship cards' pinned ones), 20 px under a 22 px strip's top: the live one brass on its tint
    /// and border, the others grey; its width out. True when clicked.</summary>
    public static bool DockTab(this ImDrawListPtr dl, string id, Vector2 at, string label, bool on, float extra, out float w)
    {
        w = Width(UiFonts.SansSmall, label) + P(18) + extra;
        var a = at + new Vector2(0, P(2));
        ImGui.SetCursorScreenPos(a);
        bool hit = ImGui.InvisibleButton(id, new Vector2(w, P(20)));
        bool hover = ImGui.IsItemHovered();
        dl.AddRectFilled(a, a + new Vector2(w, P(20)), U(on ? T.BrassDarker : hover ? T.TabHover : T.Panel2));
        var edge = U(on ? T.Brass : T.Edge);
        dl.AddLine(a, a + new Vector2(w, 0), edge);
        dl.AddLine(a, a + new Vector2(0, P(20)), edge);
        dl.AddLine(a + new Vector2(w, 0), a + new Vector2(w, P(20)), edge);
        dl.Text(UiFonts.SansSmall, new Vector2(a.X + P(9), Mid(UiFonts.SansSmall, a.Y, P(20))), on ? T.Brass : hover ? T.Bright : T.Text, label);
        return hit;
    }

    // ------------------------------------------------------------------ the frame's bars

    /// <summary>The resource bar's ground across <paramref name="w"/>.</summary>
    public static void ResourceBarBg(this ImDrawListPtr dl, Vector2 a, float w, float h)
    {
        dl.AddRectFilledMultiColor(a, a + new Vector2(w, h), U(T.ResourceTop), U(T.ResourceTop), U(T.ResourceBottom), U(T.ResourceBottom));
        dl.HLine(a.X, a.X + w, a.Y + h - 1, T.Edge);
    }

    /// <summary>A resource bar's cell from <paramref name="x"/> (moved past it): a rule, a small label over a mono figure.
    /// A caller drawing its own figure (two colours, say) passes "" and the figure's <paramref name="drawnW"/>, and draws
    /// it at the returned point.</summary>
    public static Vector2 ResourceCell(this ImDrawListPtr dl, ref float x, float top, float h, string label, string value, Vector4? colour = null, float drawnW = 0)
    {
        dl.AddLine(new Vector2(x, top), new Vector2(x, top + h - 1), U(T.Edge));
        x += P(10);
        var at = new Vector2(x, top + P(16));
        dl.Text(UiFonts.SansSmall, new Vector2(x, top + P(2)), T.Muted, label);
        dl.Text(UiFonts.Mono, at, colour ?? T.Bright, value);
        x += Math.Max(Width(UiFonts.SansSmall, label), Math.Max(Width(UiFonts.Mono, value), drawnW)) + P(10);
        return at;
    }

    /// <summary>The rule closing a row of resource cells.</summary>
    public static void ResourceEnd(this ImDrawListPtr dl, float x, float top, float h) => dl.AddLine(new Vector2(x, top), new Vector2(x, top + h - 1), U(T.Edge));

    /// <summary>The status bar's ground across <paramref name="w"/>.</summary>
    public static void StatusBarBg(this ImDrawListPtr dl, Vector2 a, float w, float h)
    {
        dl.AddRectFilled(a, a + new Vector2(w, h), U(T.MenuBar));
        dl.HLine(a.X, a.X + w, a.Y, T.Edge);
    }

    // ------------------------------------------------------------------ ImGui-flow pieces (at the cursor)

    /// <summary>A segment of a segmented control laid out by ImGui (SameLine between them): grey with a border, the chosen
    /// one brass on dark brass. True when clicked.</summary>
    public static bool SegButton(string label, bool on, float width = 0)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, on ? T.BrassDarker : T.Button);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, on ? T.BrassHover : T.ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, on ? T.BrassHover : T.DropBox);
        ImGui.PushStyleColor(ImGuiCol.Text, on ? T.Brass : T.Text);
        ImGui.PushStyleColor(ImGuiCol.Border, on ? T.Brass : T.ButtonEdge);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
        bool clicked = ImGui.Button(label, new Vector2(width, 0));
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(5);
        return clicked;
    }

    /// <summary>An F-key screen tab in the menu bar (a menu item): the current one in the accent on its tint. True when
    /// clicked.</summary>
    public static bool ScreenTab(string name, bool current)
    {
        if (current)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, T.BrassDarker);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, T.BrassHover);
            ImGui.PushStyleColor(ImGuiCol.Text, T.Brass);
        }
        bool hit = ImGui.MenuItem(name, null, current);
        if (current)
            ImGui.PopStyleColor(3);
        return hit;
    }

    /// <summary>A tooltip of wrapped text, <paramref name="wrap"/> reference px wide.</summary>
    public static void Tip(string text, float wrap = 320)
    {
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(P(wrap));
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    /// <summary>Text right-aligned in the current table cell (a figure).</summary>
    public static void RightInCell(ImFontPtr font, string text, Vector4 colour)
    {
        ImGui.PushFont(font);
        float w = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - w));
        ImGui.TextColored(colour, text);
        ImGui.PopFont();
    }
}
