using System.Numerics;
using ImGuiNET;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Map;

/// <summary>The launch screen's pieces in the UI language (docs/ui-language.md "The pieces", the mockup
/// launch-bureau.html): panes with title strips, sub-headers, table rows, steppers, drop-downs, segmented controls,
/// buttons with their keys, status chips, the work surface's tags and overlay toggles. Drawn on <see cref="dl"/> in
/// <see cref="T"/>'s colours at the fonts' own sizes; a control that takes clicks is an ImGui item under its drawing.</summary>
public sealed unsafe partial class LaunchScene
{
    static float P(float px) => px * UiFonts.Scale;

    static float TitleH => P(20);
    static float RowH => P(20);
    static float HeadH => P(18);

    static uint U(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    /// <summary>Text at its font's own size.</summary>
    void Txt(ImFontPtr f, Vector2 at, Vector4 col, string s) => dl.AddText(f, f.FontSize, at, U(col), s);

    /// <summary>Text right-aligned at <paramref name="right"/>.</summary>
    void TxtR(ImFontPtr f, float right, float y, Vector4 col, string s) => dl.AddText(f, f.FontSize, new Vector2(right - Width(f, s), y), U(col), s);

    /// <summary>Text centred in a row <paramref name="h"/> tall from <paramref name="top"/>.</summary>
    static float Mid(ImFontPtr f, float top, float h) => top + MathF.Round((h - f.FontSize) / 2);

    static float Width(ImFontPtr f, string s) => f.CalcTextSizeA(f.FontSize, float.MaxValue, 0, s).X;

    /// <summary>Text wrapped at <paramref name="wrap"/> wide; returns its height.</summary>
    float Wrapped(ImFontPtr f, Vector2 at, Vector4 col, string s, float wrap)
    {
        dl.AddText(f, f.FontSize, at, U(col), s, wrap);
        return f.CalcTextSizeA(f.FontSize, float.MaxValue, wrap, s).Y;
    }

    static float WrappedHeight(ImFontPtr f, string s, float wrap) => f.CalcTextSizeA(f.FontSize, float.MaxValue, wrap, s).Y;

    void HLine(float x0, float x1, float y, Vector4 col) => dl.AddLine(new Vector2(x0, y), new Vector2(x1, y), U(col));

    // ------------------------------------------------------------------ panes

    /// <summary>A pane: its body, the near-black edge round it, its title strip (the name in brass caps, at most one note
    /// after it); returns where the strip's right end is, for its toggles or count.</summary>
    float Pane(Vector2 a, Vector2 size, string title, string? note = null)
    {
        var b = a + size;
        dl.AddRectFilled(a, b, U(T.Panel));
        dl.AddRectFilledMultiColor(a, a + new Vector2(size.X, TitleH), U(T.TitleTop), U(T.TitleTop), U(T.TitleBottom), U(T.TitleBottom));
        HLine(a.X, b.X, a.Y + TitleH, T.Edge);
        dl.AddRect(a, b, U(T.Edge));
        Txt(UiFonts.SansBold, new Vector2(a.X + P(7), Mid(UiFonts.SansBold, a.Y, TitleH)), T.Brass, title);
        if (note != null)
            Txt(UiFonts.SansSmall, new Vector2(a.X + P(15) + Width(UiFonts.SansBold, title), Mid(UiFonts.SansSmall, a.Y, TitleH)), T.Muted, note);
        return b.X - P(6);
    }

    /// <summary>A muted note at a title strip's or sub-header's right end; returns where it starts.</summary>
    float RightNote(float right, float top, float h, string s)
    {
        float x = right - Width(UiFonts.SansSmall, s);
        Txt(UiFonts.SansSmall, new Vector2(x, Mid(UiFonts.SansSmall, top, h)), T.Muted, s);
        return x;
    }

    /// <summary>A pane's body as a child window under its title strip (scrolling when its content is longer):
    /// <see cref="dl"/> is the child's until <see cref="EndBody"/>.</summary>
    bool BeginBody(string id, Vector2 a, Vector2 size, out ImDrawListPtr keep, bool scroll = true)
    {
        keep = dl;
        ImGui.SetCursorScreenPos(a + new Vector2(1, TitleH + 1));
        bool open = ImGui.BeginChild(id, size - new Vector2(2, TitleH + 2), ImGuiChildFlags.None,
            scroll ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (open)
            dl = ImGui.GetWindowDrawList();
        return open;
    }

    /// <summary>The body's end: its content <paramref name="height"/> tall (for the scrollbar), the parent's draw list back.</summary>
    void EndBody(ImDrawListPtr keep, float height)
    {
        ImGui.SetCursorPos(new Vector2(0, Math.Max(height, 0)));
        ImGui.Dummy(new Vector2(1, 0));
        ImGui.EndChild();
        dl = keep;
    }

    /// <summary>A sub-header splitting a pane: a small caps label, a muted note, controls at the right (returned: where
    /// they end).</summary>
    float SubHeader(Vector2 a, float w, string cap, string? note = null)
    {
        dl.AddRectFilled(a, a + new Vector2(w, RowH), U(T.HeaderBg));
        HLine(a.X, a.X + w, a.Y + RowH - 1, T.Line);
        Txt(UiFonts.SansSmall, new Vector2(a.X + P(6), Mid(UiFonts.SansSmall, a.Y, RowH)), T.Text, cap);
        if (note != null)
            Txt(UiFonts.SansSmall, new Vector2(a.X + P(14) + Width(UiFonts.SansSmall, cap), Mid(UiFonts.SansSmall, a.Y, RowH)), T.Muted, note);
        return a.X + w - P(6);
    }

    /// <summary>A table's header row; its cells by <see cref="HeadCell"/>.</summary>
    void Head(Vector2 a, float w)
    {
        dl.AddRectFilled(a, a + new Vector2(w, HeadH), U(T.HeaderBg));
        HLine(a.X, a.X + w, a.Y + HeadH - 1, T.Edge);
    }

    /// <summary>A header cell: from <paramref name="x"/>, or right-aligned at it.</summary>
    void HeadCell(Vector2 a, float x, string s, bool right = false)
    {
        float y = Mid(UiFonts.SansSmall, a.Y, HeadH);
        if (right)
            TxtR(UiFonts.SansSmall, x, y, T.Muted, s);
        else
            Txt(UiFonts.SansSmall, new Vector2(x, y), T.Muted, s);
    }

    /// <summary>A table row's ground: every other a shade lighter, a rule under it; tinted under the pointer, warm with a
    /// brass bar when selected.</summary>
    void RowBg(Vector2 a, float w, float h, int n, bool selected = false, bool hover = false)
    {
        var b = a + new Vector2(w, h);
        if (selected)
            dl.AddRectFilled(a, b, U(T.Selected));
        else if (hover)
            dl.AddRectFilled(a, b, U(T.Hover));
        else if (n % 2 == 1)
            dl.AddRectFilled(a, b, U(new Vector4(1, 1, 1, 0.018f)));
        HLine(a.X, b.X, b.Y - 1, T.Line);
        if (selected)
            dl.AddRectFilled(a, new Vector2(a.X + P(3), b.Y), U(T.Brass));
    }

    /// <summary>A group row's ground (<c>Panel2</c>).</summary>
    void GroupBg(Vector2 a, float w, float h, bool hover = false)
    {
        dl.AddRectFilled(a, a + new Vector2(w, h), U(hover ? T.Hover : T.Panel2));
        HLine(a.X, a.X + w, a.Y + h - 1, T.Line);
    }

    /// <summary>A row's hit area, letting the controls drawn over it take their own clicks: (clicked, hovered).</summary>
    static (bool Clicked, bool Hover) HitRow(string id, Vector2 a, Vector2 size)
    {
        ImGui.SetCursorScreenPos(a);
        ImGui.SetNextItemAllowOverlap();
        bool click = ImGui.InvisibleButton(id, size);
        return (click, ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlappedByItem));
    }

    // ------------------------------------------------------------------ marks

    /// <summary>A small filled triangle pointing <paramref name="dir"/> (0 right, 1 down, 2 left, 3 up).</summary>
    void Arrow(Vector2 c, int dir, float r, Vector4 col)
    {
        Vector2 f = dir switch { 0 => new(1, 0), 1 => new(0, 1), 2 => new(-1, 0), _ => new(0, -1) }, s = new(-f.Y, f.X);
        dl.AddTriangleFilled(c + f * r, c - f * r * 0.7f + s * r * 0.9f, c - f * r * 0.7f - s * r * 0.9f, U(col));
    }

    /// <summary>A twisty: ▼ open, ▶ shut.</summary>
    void Twisty(Vector2 c, bool open) => Arrow(c, open ? 1 : 0, P(3.5f), T.Muted);

    void Cross(Vector2 c, float r, Vector4 col)
    {
        dl.AddLine(c - new Vector2(r, r), c + new Vector2(r, r), U(col), P(1.2f));
        dl.AddLine(c + new Vector2(-r, r), c + new Vector2(r, -r), U(col), P(1.2f));
    }

    /// <summary>A ✕ that takes a click: true when clicked.</summary>
    bool CrossButton(string id, Vector2 c, string tip)
    {
        ImGui.SetCursorScreenPos(c - new Vector2(P(8), P(9)));
        bool hit = ImGui.InvisibleButton(id, new Vector2(P(16), P(18)));
        bool hover = ImGui.IsItemHovered();
        Cross(c, P(3.5f), hover ? T.Bright : T.Muted);
        if (hover)
            ImGui.SetTooltip(tip);
        return hit;
    }

    /// <summary>A radio mark: ◉ on, ○ off.</summary>
    void Radio(Vector2 c, bool on)
    {
        dl.AddCircle(c, P(5), U(on ? T.Brass : T.Muted), 16, P(1.2f));
        if (on)
            dl.AddCircleFilled(c, P(2.5f), U(T.Brass), 12);
    }

    // ------------------------------------------------------------------ controls

    /// <summary>A stepper (◀ inset value ▶): its width. <paramref name="by"/> is the step asked for (Shift ×4, the wheel
    /// over the value steps too), else 0.</summary>
    float Stepper(string id, Vector2 at, string value, float valueW, bool down, bool up, out int by)
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
            if (ImGui.InvisibleButton(k == 0 ? "##dn" : "##up", new Vector2(bw, h)) && on && !begin)
                by = k == 0 ? -mul : mul;
            bool hover = on && ImGui.IsItemHovered();
            dl.AddRectFilled(a, a + new Vector2(bw, h), U(hover ? T.Button : T.StepBox));
            dl.AddRect(a, a + new Vector2(bw, h), U(T.StepEdge));
            Arrow(a + new Vector2(bw, h) / 2, k == 0 ? 2 : 0, P(3), on ? T.Bright : T.StepEdge);
        }
        var v = at + new Vector2(bw + P(2), 0);
        dl.AddRectFilled(v, v + new Vector2(valueW, h), U(T.Inset));
        dl.AddRect(v, v + new Vector2(valueW, h), U(new Vector4(0.102f, 0.125f, 0.149f, 1)));
        TxtR(UiFonts.Mono, v.X + valueW - P(4), Mid(UiFonts.Mono, v.Y, h), T.Bright, value);
        ImGui.SetCursorScreenPos(v);
        ImGui.InvisibleButton("##v", new Vector2(valueW, h));
        if (ImGui.IsItemHovered() && !begin)
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
    int DropDown(string id, Vector2 at, float w, string now, string[] options, bool changed = false)
    {
        int picked = -1;
        float h = P(16);
        ImGui.SetCursorScreenPos(at);
        if (ImGui.InvisibleButton(id, new Vector2(w, h)) && !begin)
            ImGui.OpenPopup(id);
        bool hover = ImGui.IsItemHovered();
        dl.AddRectFilled(at, at + new Vector2(w, h), U(T.Inset));
        dl.AddRect(at, at + new Vector2(w, h), U(hover ? T.ButtonEdge : new Vector4(0.102f, 0.125f, 0.149f, 1)));
        var box = new Vector2(at.X + w - P(15), at.Y + 1);
        dl.AddRectFilled(box, new Vector2(at.X + w - 1, at.Y + h - 1), U(T.DropBox));
        Arrow(box + new Vector2(P(7), (h - 2) / 2), 1, P(3), T.Bright);
        dl.PushClipRect(at, new Vector2(box.X - P(2), at.Y + h), true);
        Txt(UiFonts.SansSmall, new Vector2(at.X + P(5), Mid(UiFonts.SansSmall, at.Y, h)), changed ? T.Brass : T.Bright, now);
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

    /// <summary>A segmented control: joined grey buttons, the chosen one brass. The one clicked, else -1;
    /// <paramref name="width"/> its whole width.</summary>
    int Segmented(string id, Vector2 at, string[] labels, int on, out float width, bool enabled = true)
    {
        int hit = -1;
        float x = at.X, h = P(16);
        ImGui.PushID(id);
        for (int i = 0; i < labels.Length; i++)
        {
            float w = Width(UiFonts.SansSmall, labels[i]) + P(14);
            var a = new Vector2(x, at.Y);
            ImGui.SetCursorScreenPos(a);
            ImGui.PushID(i);
            if (ImGui.InvisibleButton("##seg", new Vector2(w, h)) && i != on && enabled && !begin)
                hit = i;
            ImGui.PopID();
            bool hover = enabled && ImGui.IsItemHovered();
            bool sel = i == on;
            dl.AddRectFilled(a, a + new Vector2(w, h), U(sel ? T.BrassDarker : hover ? T.ButtonHover : T.Button));
            Txt(UiFonts.SansSmall, new Vector2(a.X + P(7), Mid(UiFonts.SansSmall, a.Y, h)), sel ? T.Brass : enabled ? T.Text : T.Muted, labels[i]);
            x += w - 1;
        }
        // the borders after the fills, the chosen one's brass over its neighbours'
        float bx = at.X;
        for (int i = 0; i < labels.Length; i++)
        {
            float w = Width(UiFonts.SansSmall, labels[i]) + P(14);
            if (i != on)
                dl.AddRect(new Vector2(bx, at.Y), new Vector2(bx + w, at.Y + h), U(T.ButtonEdge));
            bx += w - 1;
        }
        if (on >= 0 && on < labels.Length)
        {
            float ox = at.X;
            for (int i = 0; i < on; i++)
                ox += Width(UiFonts.SansSmall, labels[i]) + P(14) - 1;
            dl.AddRect(new Vector2(ox, at.Y), new Vector2(ox + Width(UiFonts.SansSmall, labels[on]) + P(14), at.Y + h), U(T.Brass));
        }
        ImGui.PopID();
        width = x + 1 - at.X;
        return hit;
    }

    /// <summary>A kbd chip: its width.</summary>
    float Kbd(Vector2 at, string key, Vector4? edge = null, Vector4? ink = null)
    {
        float w = Width(UiFonts.MonoSmall, key) + P(6), h = P(13);
        var e = U(edge ?? T.TagEdge);
        dl.AddRect(at, at + new Vector2(w, h), e, P(2));
        dl.AddLine(new Vector2(at.X + 1, at.Y + h - 1), new Vector2(at.X + w - 1, at.Y + h - 1), e);
        Txt(UiFonts.MonoSmall, new Vector2(at.X + P(3), Mid(UiFonts.MonoSmall, at.Y, h - 1)), ink ?? T.KeyText, key);
        return w;
    }

    static float ButtonWidth(string label, string? key, bool primary = false) =>
        Width(primary ? UiFonts.SansBold : UiFonts.SansSmall, label) + P(16) + (key != null ? Width(UiFonts.MonoSmall, key) + P(12) : 0);

    /// <summary>A button: 20 px, grey, its key in a kbd chip after the word; the primary one brass on dark brass, never
    /// larger. <paramref name="w"/> stretches it (0: to its label).</summary>
    bool Button(string id, Vector2 at, string label, string? key = null, bool primary = false, bool enabled = true, float w = 0)
    {
        if (w <= 0)
            w = ButtonWidth(label, key, primary);
        float h = P(20);
        ImGui.SetCursorScreenPos(at);
        bool hit = ImGui.InvisibleButton(id, new Vector2(w, h)) && enabled;
        bool hover = enabled && ImGui.IsItemHovered();
        var fill = primary ? (hover ? new Vector4(0.306f, 0.247f, 0.118f, 1) : T.BrassDarker) : hover ? T.ButtonHover : T.Button;
        var edge = primary ? T.Brass : T.ButtonEdge;
        var ink = primary ? T.Brass : T.Text;
        if (!enabled)
            (fill, edge, ink) = (T.Panel2, T.Line, T.Muted);
        dl.AddRectFilled(at, at + new Vector2(w, h), U(fill));
        dl.AddRect(at, at + new Vector2(w, h), U(edge));
        var font = primary ? UiFonts.SansBold : UiFonts.SansSmall;
        float content = Width(font, label) + (key != null ? Width(UiFonts.MonoSmall, key) + P(12) : 0);
        float x = at.X + MathF.Round((w - content) / 2);
        Txt(font, new Vector2(x, Mid(font, at.Y, h)), ink, label);
        if (key != null)
            Kbd(new Vector2(x + Width(font, label) + P(6), at.Y + P(3.5f)), key, primary && enabled ? T.BrassDark : null, primary && enabled ? T.Brass : null);
        return hit;
    }

    /// <summary>A link-like action in a row (brass, brighter under the pointer): true when clicked; its width out.</summary>
    bool Link(string id, Vector2 at, string label, out float w, bool muted = false)
    {
        w = Width(UiFonts.SansSmall, label);
        ImGui.SetCursorScreenPos(at);
        bool hit = ImGui.InvisibleButton(id, new Vector2(w, P(16))) && !begin;
        bool hover = ImGui.IsItemHovered();
        Txt(UiFonts.SansSmall, new Vector2(at.X, Mid(UiFonts.SansSmall, at.Y, P(16))), hover ? T.Bright : muted ? T.Muted : T.Brass, label);
        return hit;
    }

    enum ChipKind { Ok, Amber, Blue }

    /// <summary>A status chip: small text in a 1 px border on a dark tint of its colour; its width.</summary>
    float Chip(Vector2 at, string label, ChipKind k)
    {
        var (bg, edge, ink) = k switch
        {
            ChipKind.Ok => (T.OkTint, T.OkEdge, T.Ok),
            ChipKind.Amber => (T.AmberTint, T.AmberEdge, T.Amber),
            _ => (T.BlueTint, T.BlueEdge, T.Blue),
        };
        float w = Width(UiFonts.SansSmall, label) + P(8), h = P(15);
        dl.AddRectFilled(at, at + new Vector2(w, h), U(bg));
        dl.AddRect(at, at + new Vector2(w, h), U(edge));
        Txt(UiFonts.SansSmall, new Vector2(at.X + P(4), Mid(UiFonts.SansSmall, at.Y, h)), ink, label);
        return w;
    }

    static float ChipWidth(string label) => Width(UiFonts.SansSmall, label) + P(8);

    /// <summary>A work surface's tag: mono in a dark translucent box with a 1 px border.</summary>
    void Tag(Vector2 at, string s, Vector4? ink = null)
    {
        var size = new Vector2(Width(UiFonts.MonoSmall, s) + P(6), UiFonts.MonoSmall.FontSize + P(2));
        dl.AddRectFilled(at, at + size, U(T.TagBg));
        dl.AddRect(at, at + size, U(T.TagEdge));
        Txt(UiFonts.MonoSmall, at + new Vector2(P(3), P(1)), ink ?? T.TagText, s);
    }

    static Vector2 TagSize(string s) => new(Width(UiFonts.MonoSmall, s) + P(6), UiFonts.MonoSmall.FontSize + P(2));

    /// <summary>An overlay toggle in a title strip (a boxed letter and a word; brass when on), laid leftwards from
    /// <paramref name="right"/>: true when clicked.</summary>
    bool Toggle(string id, ref float right, float top, string key, string label, bool on)
    {
        float w = P(22) + Width(UiFonts.SansSmall, label), h = P(15);
        right -= w;
        var a = new Vector2(right, top + (TitleH - h) / 2);
        ImGui.SetCursorScreenPos(a);
        bool hit = ImGui.InvisibleButton(id, new Vector2(w, h));
        bool hover = ImGui.IsItemHovered();
        var top0 = on ? T.BrassDark : new Vector4(0.18f, 0.22f, 0.26f, 1);
        var bottom = on ? T.BrassDarker : new Vector4(0.137f, 0.169f, 0.2f, 1);
        dl.AddRectFilledMultiColor(a, a + new Vector2(w, h), U(top0), U(top0), U(bottom), U(bottom));
        dl.AddRect(a, a + new Vector2(w, h), U(T.Edge));
        var fg = on ? new Vector4(0.94f, 0.835f, 0.54f, 1) : hover ? T.Text : T.Muted;
        dl.AddRect(a + new Vector2(P(3), P(2)), a + new Vector2(P(14), P(13)), U(fg), P(2));
        Txt(UiFonts.SansSmall, new Vector2(a.X + P(5), Mid(UiFonts.SansSmall, a.Y, h)), fg, key);
        Txt(UiFonts.SansSmall, new Vector2(a.X + P(18), Mid(UiFonts.SansSmall, a.Y, h)), fg, label);
        right -= P(3);
        return hit;
    }

    /// <summary>A ledger bar: a thin well, the friend's share blue from the left, the rest the enemy's.</summary>
    void ShareBar(Vector2 a, float w, float h, float friend)
    {
        dl.AddRectFilled(a, a + new Vector2(w, h), U(T.HostileBar));
        dl.AddRectFilled(a, a + new Vector2(w * Math.Clamp(friend, 0, 1), h), U(T.FriendBar));
        dl.AddRect(a, a + new Vector2(w, h), U(T.Edge));
    }
}
