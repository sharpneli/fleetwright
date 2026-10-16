using System.Globalization;
using System.Numerics;
using Fleetwright.Shipgen;
using ImGuiNET;
using Part = Fleetwright.Designer.ProtectionEdits.Part;
using PE = Fleetwright.Designer.ProtectionEdits;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Designer;

/// <summary>
/// The Protection tab's drawings (docs/designer-ui/protection-layers.md): the elevation, with the rooms, the compartments,
/// the zones, slopes and inner bulkhead, and two flags for the sections' cuts; and the two half-sections side by side,
/// each its own station (ProtectionSection), with the selected part's brass handles. Drags preview on screen and edit
/// once, on release.
/// </summary>
public sealed unsafe partial class DesignerScene
{
    // the sections' cache: one per half, rebuilt when the ship or the station changes
    readonly ProtectionSection?[] protSec = new ProtectionSection?[2];
    readonly (Ship? Ship, float X)[] protSecKey = new (Ship?, float)[2];
    object? protCutsDoc;
    bool protUnlinkPick;
    Func<ProtectionSection.ArmourPoly, bool>? secLit, secDim;

    // ------------------------------------------------------------------ the cuts

    /// <summary>The cuts' stations from their stops (the stops move when the design does), with the defaults for a new
    /// design: 1 at the machinery, 2 at the foremost magazine.</summary>
    void ResolveCuts(ProtPane pp)
    {
        CutStop? Find(string? id) => id == null ? null : pp.Stops.FirstOrDefault(s => s.Id == id);
        if (!pp.Built)
            return;   // the stops come with the build: until then the cuts stay as they are
        if (!ReferenceEquals(protCutsDoc, doc))
        {
            protCutsDoc = doc;
            protLinked = false;
            protCutStop[0] = "mach";
            protCutStop[1] = (pp.Stops.FirstOrDefault(s => s.Id == "mag:A") ?? pp.Stops.FirstOrDefault(s => s.Id.StartsWith("mag:", StringComparison.Ordinal)))?.Id ?? "fore";
        }
        if (protUnlinkPick)
        {
            protUnlinkPick = false;
            var other = pp.Stops.FirstOrDefault(s => Math.Abs(s.X - protCutX[0]) > 0.01f && s.Id.StartsWith("mag:", StringComparison.Ordinal))
                        ?? pp.Stops.FirstOrDefault(s => Math.Abs(s.X - protCutX[0]) > 0.01f);
            if (other != null)
                protCutStop[1] = other.Id;
        }
        for (int k = 0; k < 2; k++)
        {
            if (Find(protCutStop[k]) is { } s)
                protCutX[k] = s.X;
            else if (protCutStop[k] != null)
            {
                // a stop this ship hasn't (no magazine A): the first that suits
                var f = k == 0 ? pp.Stops.FirstOrDefault() : pp.Stops.FirstOrDefault(q => q.Id.StartsWith("mag:", StringComparison.Ordinal)) ?? pp.Stops.LastOrDefault();
                protCutStop[k] = f?.Id;
                protCutX[k] = f?.X ?? 0;
            }
            protCutX[k] = Math.Clamp(protCutX[k], -pp.L / 2 + 0.5f, pp.L / 2 - 0.5f);
        }
        if (protLinked)
            (protCutStop[1], protCutX[1]) = (protCutStop[0], protCutX[0]);
    }

    void ToggleLinked()
    {
        if (!protLinked)
        {
            protCut2Saved = (protCutStop[1], protCutX[1]);
            protLinked = true;
            (protCutStop[1], protCutX[1]) = (protCutStop[0], protCutX[0]);
        }
        else
        {
            protLinked = false;
            (protCutStop[1], protCutX[1]) = protCut2Saved;
            if (protCut2Saved.Stop == protCutStop[0] && Math.Abs(protCut2Saved.X - protCutX[0]) < 0.01f)
                protUnlinkPick = true;
        }
    }

    void SetCut(int k, string? stop, float x)
    {
        (protCutStop[k], protCutX[k]) = (stop, x);
        if (protLinked)
            (protCutStop[1 - k], protCutX[1 - k]) = (stop, x);
    }

    static string CutName(ProtPane pp, float x, string? stop)
    {
        if (stop != null)
            for (int i = 0; i < pp.Stops.Length; i++)
                if (pp.Stops[i].Id == stop)
                    return pp.Stops[i].Label;
        foreach (var r in pp.Rooms)
            if (r.Kind != "handling" && x >= r.X0 && x <= r.X1)
                return r.Name;
        return x > pp.X1 ? "Fore end" : x < pp.X0 ? "Aft end" : "Citadel";
    }

    bool OneCut => Math.Abs(protCutX[0] - protCutX[1]) < 0.01f;

    ProtectionSection? SectionAt(int k, ProtPane pp)
    {
        if (pp.Ship is not { } ship)
            return null;
        float x = protCutX[k];
        if (protSec[k] == null || !ReferenceEquals(protSecKey[k].Ship, ship) || protSecKey[k].X != x)
        {
            // the other half may already have it
            int o = 1 - k;
            protSec[k] = protSec[o] != null && ReferenceEquals(protSecKey[o].Ship, ship) && protSecKey[o].X == x ? protSec[o]
                : ProtectionSection.Build(ship, x, pp.DeckRow);
            protSecKey[k] = (ship, x);
        }
        return protSec[k];
    }

    void CutMenu(ProtPane pp)
    {
        if (openCutMenu >= 0)
        {
            ImGui.SetNextWindowPos(cutAnchor[openCutMenu]);
            ImGui.OpenPopup("##cutmenu" + openCutMenu);
            cutMenuFor = openCutMenu;
            openCutMenu = -1;
        }
        for (int k = 0; k < 2; k++)
        {
            if (!ImGui.BeginPopup("##cutmenu" + k))
                continue;
            ImGui.TextColored(T.Muted, k == 0 ? "Section 1: cut at" : "Section 2: cut at");
            ImGui.Separator();
            for (int i = 0; i < pp.Stops.Length; i++)
            {
                var s = pp.Stops[i];
                if (ImGui.Selectable(pp.CutLabels[i] + "##cut" + i, Math.Abs(protCutX[k] - s.X) < 0.01f))
                    SetCut(k, s.Id, s.X);
                ImGui.SameLine(P(220));
                ImGui.TextColored(T.Muted, $"x {Sx1(s.X)}");
            }
            ImGui.EndPopup();
        }
    }

    readonly Vector2[] cutAnchor = new Vector2[2];
    int cutMenuFor = -1;

    // ------------------------------------------------------------------ the elevation

    /// <summary>Ship metres to the elevation's pixels: x to scale, heights stretched to fill.</summary>
    readonly record struct ElevMap(float Left, float Top, float Kx, float Kz, float L, float ZTop)
    {
        public float X(float x) => Left + (x + L / 2) * Kx;
        public float Z(float z) => Top + (ZTop - z) * Kz;
        public float InvX(float px) => (px - Left) / Kx - L / 2;
        public float InvZ(float py) => ZTop - (py - Top) / Kz;
    }

    void ProtElevation(ImDrawListPtr dl, Vector2 pos, Vector2 size, ProtPane pp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Plan));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "ELEVATION", null);
        var font = UiFonts.SansSmall;
        Text(dl, font, new Vector2(pos.X + P(6) + Width(font, "ELEVATION") + P(10), pos.Y + (P(18) - font.FontSize) / 2), T.Muted, "heights stretched · deck by deck");
        ElevToggles(dl, pos, size, pp);
        var io = ImGui.GetIO();
        float left = P(58), right = P(8), top = P(34), bottom = P(16);
        float fl = protFrameL > 0 ? protFrameL : pp.L, ft = protFrameL > 0 ? protFrameTop : pp.ZTop, fb = protFrameL > 0 ? protFrameBot : pp.Keel - 0.4f;
        float kx = (size.X - left - right) / fl;
        float kz = (size.Y - top - bottom) / Math.Max(1, ft - fb);
        var m = new ElevMap(pos.X + left, pos.Y + top, kx, kz, fl, ft);

        // the hull side: quads from the deck line down to each station's keel
        for (int i = 0; i + 1 < pp.Profile.Length; i++)
        {
            var (p0, p1) = (pp.Profile[i], pp.Profile[i + 1]);
            float t0 = HullTop(pp, p0.X), t1 = HullTop(pp, p1.X);
            dl.AddQuadFilled(new Vector2(m.X(p0.X), m.Z(t0)), new Vector2(m.X(p1.X), m.Z(t1)), new Vector2(m.X(p1.X), m.Z(p1.Y)),
                new Vector2(m.X(p0.X), m.Z(p0.Y)), T.U32(new Vector4(0.10f, 0.176f, 0.267f, 1)));
        }
        uint hullLine = T.U32(new Vector4(0.435f, 0.576f, 0.71f, 1));
        for (int i = 0; i + 1 < pp.Profile.Length; i++)
        {
            var (p0, p1) = (pp.Profile[i], pp.Profile[i + 1]);
            dl.AddLine(new Vector2(m.X(p0.X), m.Z(p0.Y)), new Vector2(m.X(p1.X), m.Z(p1.Y)), hullLine);
            dl.AddLine(new Vector2(m.X(p0.X), m.Z(HullTop(pp, p0.X))), new Vector2(m.X(p1.X), m.Z(HullTop(pp, p1.X))), hullLine);
        }
        // the rooms that matter: magazines, handling rooms, machinery
        if (protRooms)
            foreach (var r in pp.Rooms)
            {
                var f = ProtectionSection.FillOf(r.Kind);
                dl.AddRectFilled(new Vector2(m.X(r.X0), m.Z(r.Z1)), new Vector2(m.X(r.X1), m.Z(r.Z0)), T.U32(f with { W = r.Kind == "magazine" ? 0.5f : f.W * 0.9f }));
            }
        // deck lanes, dashed, named in the margin
        uint lane = T.U32(new Vector4(0.61f, 0.76f, 0.886f, 0.28f));
        foreach (var (name, z, x0, x1) in pp.Lanes)
        {
            Dashed(dl, new Vector2(m.X(x0), m.Z(z)), new Vector2(m.X(x1), m.Z(z)), lane, P(3));
            Text(dl, font, new Vector2(pos.X + left - P(6) - Width(font, name), m.Z(z) - font.FontSize / 2), new Vector4(0.5f, 0.61f, 0.72f, 1), name);
        }
        Dashed(dl, new Vector2(m.X(-pp.L / 2 + 0.05f * pp.L), m.Z(pp.Ib)), new Vector2(m.X(pp.L / 2 - 0.06f * pp.L), m.Z(pp.Ib)), lane, P(3));
        Text(dl, font, new Vector2(pos.X + left - P(6) - Width(font, "Inner btm"), m.Z(pp.Ib) - font.FontSize / 2), new Vector4(0.5f, 0.61f, 0.72f, 1), "Inner btm");
        dl.AddLine(new Vector2(m.X(-pp.L / 2) - P(4), m.Z(pp.Wl)), new Vector2(m.X(pp.L / 2) + P(4), m.Z(pp.Wl)), T.U32(T.Blue));
        Text(dl, font, new Vector2(pos.X + P(4), m.Z(pp.Wl) - font.FontSize / 2), T.Blue, "WL");
        // the citadel's brackets (none on a ship with no armour)
        if (pp.HasCitadel)
        {
            uint brk = T.U32(T.Brass with { W = 0.22f });
            Dashed(dl, new Vector2(m.X(pp.X0), pos.Y + P(28)), new Vector2(m.X(pp.X0), pos.Y + size.Y - bottom), brk, P(3), vertical: true);
            Dashed(dl, new Vector2(m.X(pp.X1), pos.Y + P(28)), new Vector2(m.X(pp.X1), pos.Y + size.Y - bottom), brk, P(3), vertical: true);
            Text(dl, font, new Vector2((m.X(pp.X0) + m.X(pp.X1) - Width(font, pp.Citadel)) / 2, pos.Y + size.Y - bottom + P(1)), new Vector4(0.55f, 0.48f, 0.33f, 1), pp.Citadel);
        }
        Text(dl, font, new Vector2(MathF.Max(pos.X + left, m.X(-pp.L / 2)), pos.Y + size.Y - bottom + P(1)), new Vector4(0.37f, 0.48f, 0.59f, 1), "aft");
        Text(dl, font, new Vector2(m.X(pp.L / 2) - Width(font, "fore"), pos.Y + size.Y - bottom + P(1)), new Vector4(0.37f, 0.48f, 0.59f, 1), "fore");

        // the interaction area under everything drawn from here
        var area0 = new Vector2(pos.X + 1, pos.Y + P(18));
        ImGui.SetCursorScreenPos(area0);
        ImGui.SetNextItemAllowOverlap();
        ImGui.InvisibleButton("##elev", new Vector2(size.X - 2, size.Y - P(18) - 1), ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        bool hover = ImGui.IsItemHovered() && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId);
        bool clickL = ImGui.IsItemClicked(ImGuiMouseButton.Left), clickR = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        if (hover)
            ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY);
        var mouse = io.MousePos;

        // the plates
        protHits.Clear();
        dl.PushClipRect(pos + new Vector2(1, P(18)), pos + size - new Vector2(1, 1), true);
        InnerBand(dl, m, pp);
        foreach (var pl in pp.Plates)
            DrawPlate(dl, m, pl, pp);
        foreach (var (tag, tx, tz, mm, r) in pp.Tags)
            TagBox(dl, m, tag, tx, tz, mm, r);
        CompOverlay(dl, m, pp);
        // the + ghosts: on hover a dashed outline and its +
        PGhost? ghostHot = null;
        foreach (var g in pp.Ghosts)
        {
            var (a, b) = GhostRect(m, g);
            bool on = protDrag == DragKind.None && hover && Inside(mouse, a, b) && !HitPlate(mouse).HasValue && !NearFlag(m, mouse)
                      || protForceTip != null && g.Ref.Kind == PK.Deck && protForceTip == g.Force;
            if (!on)
                continue;
            ghostHot = g;
            dl.AddRectFilled(a, b, T.U32(T.Brass with { W = 0.12f }));
            dl.AddRect(a, b, T.U32(T.Brass with { W = 0.75f }));
            Text(dl, UiFonts.SansBold, (a + b) / 2 - new Vector2(Width(UiFonts.SansBold, "+") / 2, UiFonts.SansBold.FontSize / 2), new Vector4(1, 0.906f, 0.659f, 1), "+");
            if (protForceTip != null)
                TipAt(g.Tip, b + new Vector2(P(6), P(-4)));
            else
                Tip(g.Tip);
        }
        // the cut flags, the selected plate's handles, and a drag in progress
        int flagHot = Flags(dl, m, pos, size, pp, mouse, hover && ghostHot == null, clickL);
        var plateHot = protDrag == DragKind.None && hover && ghostHot == null && flagHot < 0 ? HitPlate(mouse) : null;
        var handleHot = flagHot >= 0 ? DragKind.Cut : ElevHandles(dl, m, pp, mouse, hover, clickL);
        dl.PopClipRect();
        if (plateHot is { } ph && handleHot == DragKind.None)
        {
            protHotNext = ph;
            if (ProtRowOf(pp, ph) is { } row)
                Tip(row.Hover);
            if (clickL)
            {
                protSel = ph;
                stale = true;
            }
            if (clickR)
                (protSel, protMenuFor, openProtMenu) = (ph, ph, true);
            if (io.MouseWheel != 0 && ThickKnob(ph) is { } k)
                Step(Row(k, doc.Current, shown?.Ship), Math.Sign(io.MouseWheel) * (io.KeyShift ? 4 : 1));
        }
        else if (ghostHot is { } gh && clickL)
            AddFromGhost(gh);
        else if (plateHot == null && ghostHot == null && handleHot == DragKind.None && hover && protDrag == DragKind.None)
        {
            // a room's or the warned run's tooltip
            if (protComp && pp.Warned is { } wr && mouse.X >= m.X(wr.X0) && mouse.X <= m.X(wr.X1) && mouse.Y >= m.Z(0) && mouse.Y <= m.Z(pp.Keel))
                Tip(wr.Tip);
            else if (protRooms)
                foreach (var r in pp.Rooms)
                    if (Inside(mouse, new Vector2(m.X(r.X0), m.Z(r.Z1)), new Vector2(m.X(r.X1), m.Z(r.Z0))))
                    {
                        Tip(r.Tip);
                        break;
                    }
        }
    }

    /// <summary>The header's two toggles: Rooms, and Compartments with the compartment standard.</summary>
    void ElevToggles(ImDrawListPtr dl, Vector2 pos, Vector2 size, ProtPane pp)
    {
        var font = UiFonts.SansSmall;
        ImGui.PushFont(font);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(5), 0));
        string comp = pp.CompLabel;
        float wc = Width(font, comp) + P(10), wr = Width(font, "Rooms") + P(10);
        ImGui.SetCursorScreenPos(new Vector2(pos.X + size.X - P(4) - wc, pos.Y + P(1)));
        ImGui.PushStyleColor(ImGuiCol.Text, protComp ? new Vector4(1, 0.906f, 0.659f, 1) : pp.CompOk ? T.Text : T.Amber);
        ImGui.PushID("comp");
        if (Seg(comp, protComp, wc))
            protComp = !protComp;
        ImGui.PopID();
        ImGui.PopStyleColor();
        {
            var mx = ImGui.GetItemRectMax();
            var c = new Vector2(mx.X - P(9), (ImGui.GetItemRectMin().Y + mx.Y) / 2);
            if (pp.CompOk)
            {
                uint ok = T.U32(T.Ok);
                dl.AddLine(c + new Vector2(-P(3.5f), 0), c + new Vector2(-P(1), P(2.5f)), ok, P(1.5f));
                dl.AddLine(c + new Vector2(-P(1), P(2.5f)), c + new Vector2(P(3.5f), -P(3)), ok, P(1.5f));
            }
            else
            {
                dl.AddTriangle(c + new Vector2(0, -P(4)), c + new Vector2(P(4.5f), P(3.5f)), c + new Vector2(-P(4.5f), P(3.5f)), T.U32(T.Amber), P(1.2f));
                dl.AddLine(c + new Vector2(0, -P(1.5f)), c + new Vector2(0, P(1.2f)), T.U32(T.Amber), P(1.2f));
            }
        }
        if (ImGui.IsItemHovered())
            Tip(pp.CompTip);
        if (protForceTip == "comp")
            TipAt(pp.CompTip, ImGui.GetItemRectMin() + new Vector2(-P(200), P(20)));
        ImGui.SetCursorScreenPos(new Vector2(pos.X + size.X - P(8) - wc - wr, pos.Y + P(1)));
        if (Seg("Rooms", protRooms, wr))
            protRooms = !protRooms;
        if (ImGui.IsItemHovered())
            Tip("Magazines, handling rooms and machinery, tinted");
        ImGui.PopStyleVar();
        ImGui.PopFont();
    }

    /// <summary>The inner bulkhead: a dashed outline from the inner bottom to the roof deck over the citadel, solid where
    /// it's thicker (or only) at the magazines.</summary>
    void InnerBand(ImDrawListPtr dl, ElevMap m, ProtPane pp)
    {
        var a = doc.Current.Armour;
        if (!PE.Has(doc.Current, Part.Inner) || a?.InnerBulkhead is not { } ib)
            return;
        bool lit = ProtLit(new PRef(PK.Inner));
        double mm = ib.Mm ?? 0, mags = ib.MagazinesMm ?? mm;
        float y0 = m.Z(pp.RoofZ), y1 = m.Z(pp.Ib);
        if (mm > 0)
        {
            uint c = T.U32(lit ? new Vector4(1, 0.906f, 0.659f, 0.95f) : T.Armour(mm));
            float x0 = m.X(pp.X0), x1 = m.X(pp.X1);
            Dashed(dl, new Vector2(x0, y0), new Vector2(x1, y0), c, P(4));
            Dashed(dl, new Vector2(x0, y1), new Vector2(x1, y1), c, P(4));
            Dashed(dl, new Vector2(x0, y0), new Vector2(x0, y1), c, P(4), vertical: true);
            Dashed(dl, new Vector2(x1, y0), new Vector2(x1, y1), c, P(4), vertical: true);
        }
        if (mags > 0)
            foreach (var r in pp.Rooms)
            {
                if (r.Kind != "magazine")
                    continue;
                float x0 = MathF.Max(pp.X0, r.X0), x1 = MathF.Min(pp.X1, r.X1);
                if (x1 <= x0)
                    continue;
                var p0 = new Vector2(m.X(x0), y0);
                var p1 = new Vector2(m.X(x1), y1);
                dl.AddRectFilled(p0, p1, T.U32(T.Armour(mags) with { W = lit ? 0.45f : 0.3f }));
                dl.AddRect(p0, p1, T.U32(lit ? new Vector4(1, 0.906f, 0.659f, 0.9f) : T.Armour(mags)));
                protHits.Add((p0, p1, new PRef(PK.Inner)));
            }
    }

    /// <summary>A tag on the elevation (a belt's shape, a deck's slopes): a small plate-coloured label, clickable.</summary>
    void TagBox(ImDrawListPtr dl, ElevMap m, string tag, float x, float z, double mm, PRef r)
    {
        var font = UiFonts.SansSmall;
        float w = Width(font, tag) + P(6), h = font.FontSize + P(2);
        bool belt = r.Kind == PK.Belt;
        var a = belt ? new Vector2(m.X(x) - P(3) - w, m.Z(z) - P(4) - h) : new Vector2(m.X(x) + P(3), m.Z(z) - h / 2);
        var b = a + new Vector2(w, h);
        dl.AddRectFilled(a, b, T.U32(T.Armour(mm)), P(1.5f));
        dl.AddRect(a, b, T.U32(ProtLit(r) ? new Vector4(1, 0.906f, 0.659f, 0.95f) : new Vector4(0, 0, 0, 0.6f)), P(1.5f));
        Text(dl, font, a + new Vector2(P(3), P(1)), mm >= 140 ? new Vector4(0.965f, 0.937f, 0.886f, 1) : new Vector4(0.149f, 0.133f, 0.11f, 1), tag);
        protHits.Add((a, b, r));
    }

    /// <summary>The compartments: the main transverse bulkheads, and the run that sinks her hatched amber.</summary>
    void CompOverlay(ImDrawListPtr dl, ElevMap m, ProtPane pp)
    {
        if (!protComp)
            return;
        foreach (var (x, kind) in pp.Compartments)
        {
            float k = MathF.Max(pp.Keel, KeelAt(pp, x));
            dl.AddLine(new Vector2(m.X(x), m.Z(HullTop(pp, x))), new Vector2(m.X(x), m.Z(k)),
                T.U32(kind == "collision" ? T.Amber : new Vector4(0.918f, 0.941f, 0.957f, 0.55f)));
        }
        if (pp.Warned is { } w)
        {
            var a = new Vector2(m.X(w.X0), m.Z(0));
            var b = new Vector2(m.X(w.X1), m.Z(pp.Keel));
            dl.PushClipRect(a, b, true);
            uint hatch = T.U32(T.Amber with { W = 0.55f });
            for (float x = a.X - (b.Y - a.Y); x < b.X; x += P(5))
                dl.AddLine(new Vector2(x, b.Y), new Vector2(x + (b.Y - a.Y), a.Y), hatch, P(1.4f));
            dl.PopClipRect();
            dl.AddRect(a, b, T.U32(T.Amber), 0, ImDrawFlags.None, P(1.2f));
            var font = UiFonts.SansSmall;
            const string label = "these flooded sink her";
            ProtectionText(dl, font, new Vector2((a.X + b.X - Width(font, label)) / 2, b.Y - font.FontSize - P(4)), T.Amber, label);
        }
    }

    static void ProtectionText(ImDrawListPtr dl, ImFontPtr font, Vector2 at, Vector4 ink, string text) =>
        DesignerSceneText.Halo(dl, font, at, ink, T.Plan, text);

    static float KeelAt(ProtPane pp, float x)
    {
        var p = pp.Profile;
        if (p.Length == 0)
            return pp.Keel;
        if (x <= p[0].X)
            return p[0].Y;
        for (int i = 1; i < p.Length; i++)
            if (x <= p[i].X)
                return p[i - 1].Y + (p[i].Y - p[i - 1].Y) * (x - p[i - 1].X) / MathF.Max(1e-4f, p[i].X - p[i - 1].X);
        return p[^1].Y;
    }

    bool NearFlag(ElevMap m, Vector2 mouse)
    {
        for (int k = 0; k < 2; k++)
            if (!(protLinked && k == 1) && MathF.Abs(mouse.X - m.X(protCutX[k])) <= P(9) && mouse.Y <= m.Top + P(4))
                return true;
        return false;
    }

    /// <summary>The two cuts' flags along the top: dragged along the ship, snapping to the stops within 3 m. Returns the
    /// flag under the mouse (-1: none).</summary>
    int Flags(ImDrawListPtr dl, ElevMap m, Vector2 pos, Vector2 size, ProtPane pp, Vector2 mouse, bool hover, bool press)
    {
        int hot = -1;
        var font = UiFonts.SansSmall;
        uint brass = T.U32(T.Brass);
        for (int k = 0; k < 2; k++)
        {
            if (protLinked && k == 1)
                continue;
            float px = m.X(protCutX[k]);
            float fy = m.Top - P(13);
            Dashed(dl, new Vector2(px, fy + P(14)), new Vector2(px, pos.Y + size.Y - P(16)), T.U32(T.Brass with { W = 0.75f }), P(4), vertical: true);
            bool on = hover && hot < 0 && protDrag == DragKind.None && MathF.Abs(mouse.X - px) <= P(9) && mouse.Y >= fy - P(1) && mouse.Y <= fy + P(16);
            bool dragging = protDrag == DragKind.Cut && protDragI == k;
            var a = new Vector2(px - P(8), fy);
            var b = new Vector2(px + P(8), fy + P(11));
            dl.AddRectFilled(a, b, T.U32(on || dragging ? new Vector4(0.42f, 0.35f, 0.19f, 1) : new Vector4(0.29f, 0.243f, 0.125f, 1)));
            dl.AddTriangleFilled(new Vector2(px - P(3), b.Y), new Vector2(px + P(3), b.Y), new Vector2(px, b.Y + P(4)), T.U32(new Vector4(0.29f, 0.243f, 0.125f, 1)));
            dl.AddRect(a, b, brass);
            if (protLinked)
                Diamond(dl, new Vector2(px, a.Y + P(5.5f)), P(3.5f), new Vector4(1, 0.906f, 0.659f, 1));
            else
            {
                string n = k == 0 ? "1" : "2";
                Text(dl, font, new Vector2(px - Width(font, n) / 2, a.Y + (P(11) - font.FontSize) / 2), new Vector4(1, 0.906f, 0.659f, 1), n);
            }
            if (on)
            {
                hot = k;
                Tip(k == 0 ? flagTip0 ??= "Section 1\nDrag along the ship; it snaps to the machinery, each magazine and the ends."
                    : flagTip1 ??= "Section 2\nDrag along the ship; it snaps to the machinery, each magazine and the ends.");
                if (press)
                    StartDrag(DragKind.Cut, k);
            }
        }
        if (protDrag == DragKind.Cut)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
                protDrag = DragKind.None;
            else
            {
                float x = Math.Clamp(m.InvX(mouse.X), -pp.L / 2 + 0.5f, pp.L / 2 - 0.5f);
                string? stop = null;
                foreach (var s in pp.Stops)
                    if (MathF.Abs(s.X - x) < 3)
                        (stop, x) = (s.Id, s.X);
                if (stop == null)
                    x = MathF.Round(x * 2) / 2;   // half a metre: a section is rebuilt only when it moves that far
                SetCut(protDragI, stop, x);
                Tip(CutName(pp, x, stop));
            }
        }
        return hot;
    }

    string? flagTip0, flagTip1;

    static bool Inside(Vector2 p, Vector2 a, Vector2 b) => p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X) && p.Y >= Math.Min(a.Y, b.Y) && p.Y <= Math.Max(a.Y, b.Y);

    PRef? HitPlate(Vector2 p)
    {
        for (int i = protHits.Count - 1; i >= 0; i--)
            if (Inside(p, protHits[i].A, protHits[i].B))
                return protHits[i].R;
        return null;
    }

    static float HullTop(ProtPane pp, float x)
    {
        float t = 0;
        foreach (var l in pp.Lanes)
            if (l.Z > 0 && x >= l.X0 && x <= l.X1)
                t = MathF.Max(t, l.Z);
        return t;
    }

    static (Vector2, Vector2) GhostRect(ElevMap m, PGhost g) => g.Ref.Kind == PK.Deck
        ? (new Vector2(m.X(g.X0) + 1, m.Z(g.Z0) - P(5)), new Vector2(m.X(g.X1) - 1, m.Z(g.Z0) + P(5)))
        : (new Vector2(m.X(g.X0) + 1, m.Z(g.Z1)), new Vector2(m.X(g.X1) - 1, m.Z(g.Z0)));

    void DrawPlate(ImDrawListPtr dl, ElevMap m, PPlate pl, ProtPane pp)
    {
        var col = T.Armour(pl.Mm);
        bool lit = ProtLit(pl.Ref);
        bool dim = protHot is { } h && !lit;
        if (dim)
            col = col with { W = 0.55f };
        if (protDrag == DragKind.DeckMove && pl.Ref == protSel)
            col = col with { W = 0.45f };
        uint edge = T.U32(lit ? new Vector4(1, 0.906f, 0.659f, 0.95f) : new Vector4(0, 0, 0, 0.45f));
        var font = UiFonts.SansSmall;
        if (pl.Ref.Kind == PK.Tds)
        {
            var a = new Vector2(m.X(pl.X0), m.Z(pl.Z1));
            var b = new Vector2(m.X(pl.X1), m.Z(pl.Z0));
            dl.PushClipRect(a, b, true);
            uint hatch = T.U32(T.Blue with { W = lit ? 0.4f : 0.22f });
            for (float x = a.X - (b.Y - a.Y); x < b.X; x += P(6))
                dl.AddLine(new Vector2(x, b.Y), new Vector2(x + (b.Y - a.Y), a.Y), hatch, P(2));
            dl.PopClipRect();
            dl.AddRect(a, b, T.U32(T.Blue with { W = lit ? 0.8f : 0.35f }));
            Text(dl, font, new Vector2(a.X + P(6), b.Y - font.FontSize - P(3)), new Vector4(0.5f, 0.69f, 0.88f, 1), pl.Label);
            protHits.Add((a, b, pl.Ref));
            return;
        }
        if (pl.Faint)
        {
            // a band: the slopes from their deck down to the belt's lower edge, a box's sides, the lower belt
            var a = new Vector2(m.X(pl.X0), m.Z(pl.Z1));
            var b = new Vector2(m.X(pl.X1), m.Z(pl.Z0));
            float alpha = pl.Ref.Kind == PK.Slope ? 0.22f : pl.Ref.Kind == PK.Box ? 0.38f : 0.38f;
            dl.AddRectFilled(a, b, T.U32(col with { W = (lit ? alpha + 0.12f : alpha) * (dim ? 0.7f : 1) }));
            if (pl.Ref.Kind == PK.Slope)
                Dashed(dl, new Vector2(a.X, b.Y), b, T.U32(T.Armour(pl.Mm)), P(4));
            if (lit)
                dl.AddRect(a, b, edge);
            if (pl.Label.Length > 0 && b.X - a.X > Width(font, pl.Label) + P(8))
                Text(dl, font, (a + b) / 2 - new Vector2(Width(font, pl.Label) / 2, font.FontSize / 2), T.ArmourRamp[0], pl.Label);
            protHits.Add((a, b, pl.Ref));
            return;
        }
        if (pl.Flat)
        {
            float th = Math.Clamp((float)pl.Mm / 25 * S, P(2.5f), P(6));
            var a = new Vector2(m.X(pl.X0), m.Z(pl.Z0));
            var b = new Vector2(m.X(pl.X1), a.Y + th);
            dl.AddRectFilled(a, b, T.U32(col));
            dl.AddRect(a, b, edge);
            if (b.X - a.X > P(16) && pl.Ref.Kind != PK.Box)
            {
                float lx = MathF.Min(a.X + P(24), (a.X + b.X) / 2), ly = b.Y + P(2);
                float lw = Width(font, pl.Label) + P(6);
                dl.AddRectFilled(new Vector2(lx - lw / 2, ly), new Vector2(lx + lw / 2, ly + font.FontSize + P(1)), T.U32(new Vector4(0.055f, 0.07f, 0.082f, 0.82f)), P(2));
                Text(dl, font, new Vector2(lx - lw / 2 + P(3), ly), T.Armour(pl.Mm), pl.Label);
            }
            protHits.Add((a - new Vector2(0, P(4)), b + new Vector2(0, P(4)), pl.Ref));
            return;
        }
        if (pl.Bulkhead)
        {
            float x = m.X(pl.X0), hw = pl.Inside ? P(1.5f) : P(2.5f);
            var a = new Vector2(x - hw, m.Z(pl.Z1));
            var b = new Vector2(x + hw, m.Z(pl.Z0));
            dl.AddRectFilled(a, b, T.U32(col));
            dl.AddRect(a, b, edge);
            protHits.Add((new Vector2(x - P(6), a.Y), new Vector2(x + P(6), b.Y), pl.Ref));
            return;
        }
        {
            var a = new Vector2(m.X(pl.X0), m.Z(pl.Z1));
            var b = new Vector2(MathF.Max(m.X(pl.X1), a.X + 1.5f), MathF.Max(m.Z(pl.Z0), a.Y + 2));
            if (pl.Inside)
            {
                Dashed(dl, a, new Vector2(a.X, b.Y), T.U32(col with { W = 0.8f }), P(3), vertical: true);
                Dashed(dl, new Vector2(b.X, a.Y), b, T.U32(col with { W = 0.8f }), P(3), vertical: true);
                return;
            }
            if (pl.Ref.Kind == PK.Main)
            {
                // the gunhouse: its sides in the side's thickness, the face toward its end
                dl.AddRectFilled(a, b, T.U32(T.Armour(pl.Mm * 0.55) with { W = col.W }));
                dl.AddRect(a, b, edge);
                bool fore = pl.X0 + pl.X1 > 0;
                float fx = fore ? b.X - P(3) : a.X;
                dl.AddRectFilled(new Vector2(fx, a.Y), new Vector2(fx + P(3), b.Y), T.U32(col));
                if (pl.Label.Length > 0)
                    Text(dl, font, new Vector2((a.X + b.X - Width(font, pl.Label)) / 2, a.Y - font.FontSize - P(1)), T.Text, pl.Label);
            }
            else
            {
                dl.AddRectFilled(a, b, T.U32(pl.Hatched ? col with { W = col.W * 0.6f } : col));
                if (pl.Hatched)
                {
                    // behind the shell: hatched over
                    dl.PushClipRect(a, b, true);
                    uint hatch = T.U32(new Vector4(0.055f, 0.07f, 0.082f, 0.55f));
                    for (float x = a.X - (b.Y - a.Y); x < b.X; x += P(4))
                        dl.AddLine(new Vector2(x, b.Y), new Vector2(x + (b.Y - a.Y), a.Y), hatch, P(1.2f));
                    dl.PopClipRect();
                }
                dl.AddRect(a, b, edge);
                float lw = Width(font, pl.Label);
                if (pl.Label.Length > 0 && pl.Ref.Kind == PK.Ct)
                    Text(dl, font, new Vector2((a.X + b.X - lw) / 2, a.Y - font.FontSize - P(1)), T.Text, pl.Label);
                else if (pl.Label.Length > 0 && b.X - a.X > lw + P(8) && b.Y - a.Y >= font.FontSize)
                    Text(dl, font, (a + b) / 2 - new Vector2(lw / 2, font.FontSize / 2), pl.Mm >= 140 ? new Vector4(0.965f, 0.937f, 0.886f, 1) : new Vector4(0.149f, 0.133f, 0.11f, 1), pl.Label);
            }
            protHits.Add((a, b, pl.Ref));
        }
    }

    static void Dashed(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col, float dash, bool vertical = false)
    {
        float len = vertical ? b.Y - a.Y : b.X - a.X;
        for (float t = 0; t < len; t += 2 * dash)
        {
            float e = MathF.Min(t + dash, len);
            if (vertical)
                dl.AddLine(new Vector2(a.X, a.Y + t), new Vector2(a.X, a.Y + e), col);
            else
                dl.AddLine(new Vector2(a.X + t, a.Y), new Vector2(a.X + e, a.Y), col);
        }
    }

    /// <summary>The selected plate's brass handles on the elevation; a press starts a drag, which previews and edits on
    /// release. Returns the handle under the mouse.</summary>
    DragKind ElevHandles(ImDrawListPtr dl, ElevMap m, ProtPane pp, Vector2 mouse, bool hover, bool press)
    {
        var hot = DragKind.None;
        var d = doc.Current;
        var sel = protSel;
        int forced = protForceTip is { } ft && ft.StartsWith("handle:", StringComparison.Ordinal) ? int.Parse(ft[7..], CultureInfo.InvariantCulture) : -1;
        int n = 0;
        void H(DragKind kind, int i, Vector2 at, bool horizontal, string tip)
        {
            bool on = hover && Vector2.Distance(mouse, at) <= P(8) && protDrag == DragKind.None;
            Handle(dl, at, horizontal, on || protDrag == kind && protDragI == i);
            if (on)
            {
                hot = kind;
                Tip(tip);
                if (press)
                    StartDrag(kind, i);
            }
            if (n++ == forced)
                TipAt(tip, at + new Vector2(P(10), P(8)));
        }
        var row = ProtRowOf(pp, sel);
        if (row != null && sel.Kind is PK.Belt or PK.BeltZone or PK.EndFore or PK.EndAft && PE.Has(d, Part.Belt))
        {
            float xa = sel.Kind is PK.Belt or PK.BeltZone ? pp.X0 : sel.Kind == PK.EndFore ? pp.X1 : -pp.L / 2;
            float xb = sel.Kind is PK.Belt or PK.BeltZone ? pp.X1 : sel.Kind == PK.EndFore ? pp.L / 2 : pp.X0;
            float hx = m.X(xa) + MathF.Min(P(70), (m.X(xb) - m.X(xa)) * 0.25f);
            H(DragKind.BeltTop, 0, new Vector2(hx, m.Z(pp.BeltTop)), false, pp.BeltTip);
            H(DragKind.BeltBottom, 0, new Vector2(hx, m.Z(pp.BeltBottom)), false, pp.BeltTip);
        }
        if (sel.Kind is PK.EndFore or PK.EndAft)
        {
            bool fore = sel.Kind == PK.EndFore;
            float tipX = fore ? pp.EndTipFore : pp.EndTipAft;
            if (!float.IsNaN(tipX))
                H(DragKind.Reach, fore ? 0 : 1, new Vector2(m.X(tipX), (m.Z(pp.BeltTop) + m.Z(pp.BeltBottom)) / 2), true,
                    fore ? "Drag the tip: the end belt's reach, in tenths of the way to the stem" : "Drag the tip: the end belt's reach, in tenths of the way to the stern");
        }
        if (sel.Kind == PK.Upper && !float.IsNaN(pp.UpTop))
            H(DragKind.UpperTop, 0, new Vector2(m.X(pp.UpX0) + MathF.Min(P(70), (m.X(pp.UpX1) - m.X(pp.UpX0)) * 0.25f), m.Z(pp.UpTop)), false,
                "Drag the top edge: the upper belt up to another deck");
        if (sel.Kind == PK.Deck && sel.I >= 0)
            foreach (var pl in pp.Plates)
                if (pl.Flat && pl.Ref == sel)
                {
                    H(DragKind.DeckMove, sel.I, new Vector2((m.X(pl.X0) + m.X(pl.X1)) / 2, m.Z(pl.Z0) + P(2)), false, "Drag the deck to another deck");
                    break;
                }
        // a drag in progress: the snapped edge under the mouse, applied once on release
        if (protDragI < 9 && protDrag is DragKind.BeltTop or DragKind.BeltBottom or DragKind.Reach or DragKind.UpperTop or DragKind.DeckMove)
        {
            var io = ImGui.GetIO();
            double v = protDrag switch
            {
                DragKind.BeltTop => Math.Clamp(Math.Round((m.InvZ(io.MousePos.Y) - pp.Wl) * 10) / 10, 0.3, -pp.Wl),
                DragKind.BeltBottom => Math.Clamp(Math.Round((pp.Wl - m.InvZ(io.MousePos.Y)) * 10) / 10, 0, pp.Wl - pp.Ib - 0.2),
                DragKind.Reach => Math.Clamp(Math.Round(10 * (protDragI == 0 ? (m.InvX(io.MousePos.X) - pp.X1) / (pp.L / 2 - pp.X1)
                    : (pp.X0 - m.InvX(io.MousePos.X)) / (pp.X0 + pp.L / 2))) / 10, 0.1, 1),
                DragKind.UpperTop => NearestDeck(pp, m.InvZ(io.MousePos.Y), upper: true),
                _ => NearestDeck(pp, m.InvZ(io.MousePos.Y), upper: false),
            };
            protDragValue = v;
            DragPreview(dl, m, pp, v);
            DragTip(pp);
            EndDragIfReleased();
        }
        return hot;
    }

    static readonly Vector4 Light = new(1, 0.906f, 0.659f, 1);

    /// <summary>A plate's outline where the drag would put it: brass, over a faint fill.</summary>
    static void Target(ImDrawListPtr dl, Vector2 a, Vector2 b)
    {
        dl.AddRectFilled(a, b, T.U32(T.Brass with { W = 0.16f }));
        dl.AddRect(a, b, T.U32(Light), 0, ImDrawFlags.None, P(1.5f));
    }

    /// <summary>The deck lane at height z lit in brass, its name in the margin.</summary>
    static void LitLane(ImDrawListPtr dl, ElevMap m, ProtPane pp, float z)
    {
        foreach (var (name, lz, x0, x1) in pp.Lanes)
        {
            if (Math.Abs(lz - z) > 0.01f)
                continue;
            Dashed(dl, new Vector2(m.X(x0), m.Z(z)), new Vector2(m.X(x1), m.Z(z)), T.U32(T.Brass with { W = 0.8f }), P(3));
            var font = UiFonts.SansSmall;
            var at = new Vector2(m.Left - P(6) - Width(font, name), m.Z(z) - font.FontSize / 2);
            dl.AddRectFilled(at - new Vector2(P(3), P(1)), at + new Vector2(Width(font, name) + P(3), font.FontSize + P(1)), T.U32(T.BrassDark), P(2));
            Text(dl, font, at, Light, name);
            return;
        }
    }

    /// <summary>What the drag would do, drawn at its target: the belt (and the end belts, which share its heights) at its
    /// new edges, the end belt to its new tip, the upper belt to its new deck, the armour deck on its new deck with that
    /// deck's lane lit. The plate where it is now is left as it is, so both show.</summary>
    void DragPreview(ImDrawListPtr dl, ElevMap m, ProtPane pp, double v)
    {
        uint light = T.U32(Light);
        switch (protDrag)
        {
            case DragKind.BeltTop or DragKind.BeltBottom:
            {
                float top = protDrag == DragKind.BeltTop ? pp.Wl + (float)v : pp.BeltTop;
                float bot = protDrag == DragKind.BeltBottom ? pp.Wl - (float)v : pp.BeltBottom;
                float a = !float.IsNaN(pp.EndTipAft) ? pp.EndTipAft : pp.X0, b = !float.IsNaN(pp.EndTipFore) ? pp.EndTipFore : pp.X1;
                Target(dl, new Vector2(m.X(a), m.Z(top)), new Vector2(m.X(b), m.Z(bot)));
                float edge = protDrag == DragKind.BeltTop ? top : bot;
                dl.AddLine(new Vector2(m.X(a), m.Z(edge)), new Vector2(m.X(b), m.Z(edge)), light, P(2.5f));
                break;
            }
            case DragKind.Reach:
            {
                float x = protDragI == 0 ? pp.X1 + (float)v * (pp.L / 2 - pp.X1) : pp.X0 - (float)v * (pp.X0 + pp.L / 2);
                float a = protDragI == 0 ? pp.X1 : x, b = protDragI == 0 ? x : pp.X0;
                Target(dl, new Vector2(m.X(a), m.Z(pp.BeltTop)), new Vector2(m.X(b), m.Z(pp.BeltBottom)));
                dl.AddLine(new Vector2(m.X(x), m.Z(pp.BeltTop) - P(4)), new Vector2(m.X(x), m.Z(pp.BeltBottom) + P(4)), light, P(2.5f));
                break;
            }
            case DragKind.UpperTop:
            {
                float z = DeckZOf(pp, (long)v);
                LitLane(dl, m, pp, z);
                Target(dl, new Vector2(m.X(pp.UpX0), m.Z(z)), new Vector2(m.X(pp.UpX1), m.Z(pp.BeltTop)));
                dl.AddLine(new Vector2(m.X(pp.UpX0), m.Z(z)), new Vector2(m.X(pp.UpX1), m.Z(z)), light, P(2.5f));
                break;
            }
            case DragKind.DeckMove:
            {
                float z = DeckZOf(pp, (long)v);
                LitLane(dl, m, pp, z);
                foreach (var pl in pp.Plates)
                {
                    if (!pl.Flat || pl.Ref != protSel)
                        continue;
                    float th = Math.Clamp((float)pl.Mm / 25 * S, P(2.5f), P(6));
                    var a = new Vector2(m.X(pl.X0), m.Z(z));
                    var b = new Vector2(m.X(pl.X1), a.Y + th);
                    dl.AddRectFilled(a, b, T.U32(T.Armour(pl.Mm) with { W = 0.85f }));
                    dl.AddRect(a - new Vector2(1, 1), b + new Vector2(1, 1), light, 0, ImDrawFlags.None, P(1.5f));
                    // from where it is to where it would go
                    float cx = (a.X + b.X) / 2;
                    if (Math.Abs(pl.Z0 - z) > 0.01f)
                        Dashed(dl, new Vector2(cx, MathF.Min(m.Z(pl.Z0), a.Y)), new Vector2(cx, MathF.Max(m.Z(pl.Z0), a.Y)), light, P(2), vertical: true);
                }
                break;
            }
        }
    }

    static void Handle(ImDrawListPtr dl, Vector2 at, bool horizontal, bool on)
    {
        var half = horizontal ? new Vector2(P(3), P(7)) : new Vector2(P(7), P(3));
        dl.AddRectFilled(at - half, at + half, T.U32(on ? new Vector4(1, 0.906f, 0.659f, 1) : T.Brass), P(1.5f));
        dl.AddRect(at - half, at + half, T.U32(T.Ground), P(1.5f));
        if (horizontal)
            dl.AddLine(at - new Vector2(0, P(4)), at + new Vector2(0, P(4)), T.U32(T.BrassDark));
        else
            dl.AddLine(at - new Vector2(P(4), 0), at + new Vector2(P(4), 0), T.U32(T.BrassDark));
    }

    static void RoundHandle(ImDrawListPtr dl, Vector2 at, bool on)
    {
        dl.AddCircleFilled(at, P(4.5f), T.U32(on ? new Vector4(1, 0.906f, 0.659f, 1) : T.Brass));
        dl.AddCircle(at, P(4.5f), T.U32(T.Ground));
    }

    static float DeckZOf(ProtPane pp, long n)
    {
        foreach (var c in pp.DeckChoices)
            if (c.N == n)
                return c.Z;
        return 0;
    }

    /// <summary>The deck nearest height z: any of the stack for an armour deck, one over the belt and no lower than the
    /// main deck for the upper belt's top.</summary>
    static double NearestDeck(ProtPane pp, float z, bool upper)
    {
        long best = 0;
        float bd = float.MaxValue;
        foreach (var c in pp.DeckChoices)
            if ((upper ? c.N <= 0 && c.Z > pp.BeltTop + 0.3f : c.N >= 0) && Math.Abs(c.Z - z) < bd)
                (best, bd) = (c.N, Math.Abs(c.Z - z));
        return best;
    }

    void StartDrag(DragKind kind, int i)
    {
        protDrag = kind;
        protDragI = i;
        protDragValue = double.NaN;
        protDragTipFor = double.NaN;
    }

    void DragTip(ProtPane pp)
    {
        if (double.IsNaN(protDragValue))
            return;
        if (protDragValue != protDragTipFor)   // the text only when the snapped value changes
        {
            protDragTipFor = protDragValue;
            protDragTip = protDrag switch
            {
                DragKind.BeltTop => $"Belt top {protDragValue:+0.0} m over the waterline",
                DragKind.BeltBottom => $"Belt bottom {protDragValue:0.0} m under the waterline; slopes meet the side here",
                DragKind.Reach => $"End belt reach {protDragValue:0.0}" + (protDragValue >= 0.995 ? ": to the end" : $": {protDragValue * 100:0} % of the way"),
                DragKind.UpperTop => $"Upper belt up to the {Decks.DeckNameLower((long)protDragValue)}",
                DragKind.DeckMove => $"Armour deck on the {Decks.DeckNameLower((long)protDragValue)}",
                DragKind.Lean => $"Belt's lean {protDragValue:0}°, its top outboard",
                DragKind.Knee => $"Slopes at {protDragValue:0}°; the foot stays on the belt's lower edge",
                DragKind.Wing => $"Magazine wings {protDragValue:0.0} m each side",
                DragKind.InnerWall => $"Inner bulkhead {protDragValue:0.0} m in from the side",
                _ => $"Torpedo protection {protDragValue:0.0} m deep",
            } + "\nlet go to apply";
        }
        Tip(protDragTip);
    }

    /// <summary>On release, the drag's one edit (none if the value didn't move).</summary>
    void EndDragIfReleased()
    {
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            return;
        var kind = protDrag;
        protDrag = DragKind.None;
        if (double.IsNaN(protDragValue))
            return;
        var d = doc.Current;
        int knee = kind == DragKind.Knee ? protKneeRow : -1;
        NumberKnob k = kind switch
        {
            DragKind.BeltTop => Knobs.BeltHeight,
            DragKind.BeltBottom => Knobs.BeltDepth,
            DragKind.Reach => Knobs.EndBeltReach(protDragI == 0 ? "fore" : "aft"),
            DragKind.UpperTop => Knobs.UpperBeltToDeck,
            DragKind.DeckMove => Knobs.DeckLevel(protDragI),
            DragKind.Lean => Knobs.BeltIncline,
            DragKind.Knee => Knobs.DeckSlopeDeg(knee),
            DragKind.Wing => Knobs.MagazineWing,
            DragKind.InnerWall => Knobs.InnerInboard,
            _ => Knobs.Tds,
        };
        if (k.Value(d, shown?.Ship) is { } now && Math.Abs(now - protDragValue) < 1e-6)
            return;
        long deck = (long)protDragValue;
        if (kind == DragKind.DeckMove)
            Edit(k.Apply(d, deck), $"Armour deck {protDragI + 1} moved to the {Decks.DeckNameLower(deck)}");
        else if (kind == DragKind.UpperTop)
            Edit(k.Apply(d, deck), $"Upper belt up to the {Decks.DeckNameLower(deck)}");
        else
            SetValue(k, protDragValue);
        doc.Seal();
    }

    int protKneeRow = -1;

    void AddFromGhost(PGhost g)
    {
        var d = doc.Current;
        switch (g.Ref.Kind)
        {
            case PK.Deck:
                Edit(PE.AddDeck(d, g.Deck, g.Extent, 25, out int i), $"Armour deck added: {Decks.DeckNameLower(g.Deck)}, {PE.ExtentText(g.Extent)}");
                protSel = new PRef(PK.Deck, i);
                break;
            default:
                AddPart(PartOf(g.Ref.Kind));
                break;
        }
    }

    // ------------------------------------------------------------------ the sections: two halves, each its own station

    static PRef RefOf(ProtectionSection.ArmourPoly p) => p.Kind switch
    {
        ProtectionSection.Kind.Belt or ProtectionSection.Kind.LowerBelt => new PRef(PK.Belt),
        ProtectionSection.Kind.BeltZone => new PRef(PK.BeltZone),
        ProtectionSection.Kind.Upper => new PRef(PK.Upper),
        ProtectionSection.Kind.EndFore => new PRef(PK.EndFore),
        ProtectionSection.Kind.EndAft => new PRef(PK.EndAft),
        ProtectionSection.Kind.Deck => new PRef(PK.Deck, p.Row),
        ProtectionSection.Kind.DeckZone => new PRef(PK.DeckZone, p.Row),
        ProtectionSection.Kind.Slope => new PRef(PK.Slope, p.Row),
        ProtectionSection.Kind.Inner => new PRef(PK.Inner),
        ProtectionSection.Kind.Box => new PRef(PK.Box),
        _ => new PRef(PK.Barbettes),
    };

    void ProtSections(ImDrawListPtr dl, Vector2 pos, Vector2 size, ProtPane pp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Plan));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "SECTIONS", null);
        var font = UiFonts.SansSmall;
        SectionHeader(dl, pos, size, pp);
        var s0 = SectionAt(0, pp);
        var s1 = SectionAt(1, pp);
        if (s0 == null || s1 == null)
            return;
        var io = ImGui.GetIO();
        bool one = OneCut;
        float l = P(36), r = P(8), t = P(48), b = P(20), gap = one ? 0 : P(16);
        float fB = protFrameB > 0 ? protFrameB : pp.B, zBot = protFrameL > 0 ? protFrameBot + 0.6f : pp.Keel - 0.3f, zTop = pp.SecTop;
        float k = MathF.Min((size.X - l - r - gap) / 2 / (fB / 2 + 0.7f), (size.Y - t - b) / MathF.Max(1, zTop - zBot));
        float scx = pos.X + l + (size.X - l - r) / 2;
        float top = pos.Y + t + ((size.Y - t - b) - (zTop - zBot) * k) / 2;
        var maps = protMaps;
        var secs = protSecs;
        maps[0] = new ProtectionSection.Map(scx, -1, gap, k, top, zTop);
        maps[1] = new ProtectionSection.Map(scx, 1, gap, k, top, zTop);
        (secs[0], secs[1]) = (s0, s1);
        // the deck names in the margin (the heights are the same in both halves)
        foreach (var (z, name) in s0.Decks)
            Text(dl, font, new Vector2(pos.X + l - P(5) - Width(font, name), maps[0].Y(z) - font.FontSize / 2), new Vector4(0.5f, 0.61f, 0.72f, 1), name);
        secLit ??= p => ProtLit(RefOf(p));
        secDim ??= p => protHot is { } h && h != RefOf(p) && protSel != RefOf(p);
        for (int h = 0; h < 2; h++)
        {
            var clipA = h == 0 ? new Vector2(pos.X + 1, pos.Y + P(18)) : new Vector2(scx + gap / 2, pos.Y + P(18));
            var clipB = h == 0 ? new Vector2(scx - gap / 2, pos.Y + size.Y - 1) : new Vector2(pos.X + size.X - 1, pos.Y + size.Y - 1);
            dl.PushClipRect(clipA, clipB, true);
            secs[h].Draw(dl, maps[h], secLit, secDim, font, UiFonts.SansSmall);
            LeanArc(dl, maps[h], secs[h], font);
            dl.PopClipRect();
        }
        // the split: one whole section has its centreline; two stations a gutter with a break line and numbered badges
        uint brass = T.U32(T.Brass);
        if (one)
        {
            Dashed(dl, new Vector2(scx, pos.Y + t - P(6)), new Vector2(scx, pos.Y + size.Y - b + P(4)), T.U32(new Vector4(0.61f, 0.76f, 0.886f, 0.35f)), P(5), vertical: true);
            Text(dl, font, new Vector2(scx - Width(font, "CL") / 2, pos.Y + t - P(8) - font.FontSize), new Vector4(0.37f, 0.48f, 0.59f, 1), "CL");
            CutCaption(dl, new Vector2(scx + P(14), pos.Y + P(30)), 1, protLinked ? "" : "1", CutName(pp, protCutX[0], protCutStop[0]), font);
        }
        else
        {
            float y0 = pos.Y + t - P(16), y1 = pos.Y + size.Y - b + P(4), ym = (y0 + y1) / 2;
            dl.AddRectFilled(new Vector2(scx - gap / 2, y0), new Vector2(scx + gap / 2, y1), T.U32(new Vector4(0.055f, 0.086f, 0.129f, 0.9f)));
            for (int bi = 0; bi < 2; bi++)
            {
                float xx = scx + (bi == 0 ? -P(2.5f) : P(2.5f));
                dl.AddLine(new Vector2(xx, y0), new Vector2(xx, ym - P(9)), brass, P(1.3f));
                dl.AddLine(new Vector2(xx, ym - P(9)), new Vector2(xx + P(5), ym - P(5)), brass, P(1.3f));
                dl.AddLine(new Vector2(xx + P(5), ym - P(5)), new Vector2(xx - P(5), ym + P(5)), brass, P(1.3f));
                dl.AddLine(new Vector2(xx - P(5), ym + P(5)), new Vector2(xx, ym + P(9)), brass, P(1.3f));
                dl.AddLine(new Vector2(xx, ym + P(9)), new Vector2(xx, y1), brass, P(1.3f));
            }
            CutCaption(dl, new Vector2(scx - gap / 2 - P(11), pos.Y + P(30)), -1, "1", CutName(pp, protCutX[0], protCutStop[0]), font);
            CutCaption(dl, new Vector2(scx + gap / 2 + P(11), pos.Y + P(30)), 1, "2", CutName(pp, protCutX[1], protCutStop[1]), font);
            Text(dl, font, new Vector2(pos.X + l + P(2), pos.Y + P(22)), new Vector4(0.61f, 0.71f, 0.79f, 1), CutXText(0));
        }
        Text(dl, font, new Vector2(pos.X + size.X - r - P(2) - Width(font, CutXText(1)), pos.Y + P(22)), new Vector4(0.61f, 0.71f, 0.79f, 1), CutXText(1));
        // the legend
        float lx = pos.X + P(6), ly = pos.Y + size.Y - P(15);
        foreach (var (kind, label) in ProtectionSection.Legend)
        {
            if (kind == "tds" && !PE.Has(doc.Current, Part.Tds) || kind == "bunker" && !s0.HasCoal && !s1.HasCoal)
                continue;
            var f = ProtectionSection.FillOf(kind);
            dl.AddRectFilled(new Vector2(lx, ly + P(2)), new Vector2(lx + P(9), ly + P(10)), T.U32(f with { W = MathF.Max(0.5f, f.W + 0.3f) }));
            Text(dl, font, new Vector2(lx + P(12), ly), T.Muted, label);
            lx += P(18) + Width(font, label);
        }
        Text(dl, font, new Vector2(lx + P(6), ly), T.Muted, "to scale · plates ×3");

        // the interaction: handles, then plates, walls and cells
        ImGui.SetCursorScreenPos(pos + new Vector2(1, P(40)));
        ImGui.SetNextItemAllowOverlap();
        ImGui.InvisibleButton("##sect", new Vector2(size.X - 2, size.Y - P(40) - P(18)), ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        bool hover = ImGui.IsItemHovered() && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId);
        bool clickL = ImGui.IsItemClicked(ImGuiMouseButton.Left), clickR = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        if (hover)
            ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY);
        var mouse = io.MousePos;
        var handleHot = SectionHandles(dl, maps, secs, pp, mouse, hover, clickL, one);
        if (protDrag != DragKind.None && protDragI >= 10)
        {
            SectionDrag(dl, maps[protDragI - 10], secs[protDragI - 10], pp, mouse);
            return;
        }
        if (!hover || handleHot != DragKind.None || protDrag != DragKind.None)
            return;
        var hm = maps[mouse.X < scx ? 0 : 1];
        var hs = secs[mouse.X < scx ? 0 : 1];
        PRef? hot = null;
        if (hs.HitArmour(hm, mouse, P(4)) is { } poly)
            hot = RefOf(poly);
        else if (hs.HitWall(hm, mouse, P(4)) is { } wall)
        {
            if (wall.Kind == "tds")
                hot = new PRef(PK.Tds);
            else if (hs.WingWall is { } ww && MathF.Abs(ww.Y - wall.Y) < 0.05f && ProtRowOf(pp, new PRef(PK.Wings)) != null)
                hot = new PRef(PK.Wings);
            else
                Tip(wall.Tip);
        }
        else if (hs.HitCell(hm, mouse) is { } cell)
            Tip(cell.Tip);
        if (hot is { } hh && ProtRowOf(pp, hh) is { } row)
        {
            protHotNext = hh;
            Tip(row.Hover);
            if (clickL)
            {
                protSel = hh;
                stale = true;
            }
            if (clickR)
                (protSel, protMenuFor, openProtMenu) = (hh, hh, true);
            if (io.MouseWheel != 0 && ThickKnob(hh) is { } kk)
                Step(Row(kk, doc.Current, shown?.Ship), Math.Sign(io.MouseWheel) * (io.KeyShift ? 4 : 1));
        }
    }

    readonly ProtectionSection.Map[] protMaps = new ProtectionSection.Map[2];
    readonly ProtectionSection[] protSecs = new ProtectionSection[2];

    readonly string[] cutXText = ["", ""];
    readonly float[] cutXFor = [float.NaN, float.NaN];

    string CutXText(int k)
    {
        if (cutXFor[k] != protCutX[k])
            (cutXFor[k], cutXText[k]) = (protCutX[k], $"x {Sx1(protCutX[k])} m");
        return cutXText[k];
    }

    /// <summary>A half's numbered badge and its cut's name, toward its own side.</summary>
    static void CutCaption(ImDrawListPtr dl, Vector2 c, int side, string n, string name, ImFontPtr font)
    {
        dl.AddCircleFilled(c, P(7), T.U32(new Vector4(0.29f, 0.243f, 0.125f, 1)));
        dl.AddCircle(c, P(7), T.U32(T.Brass));
        if (n.Length == 0)
            Diamond(dl, c, P(3.5f), new Vector4(1, 0.906f, 0.659f, 1));
        else
            Text(dl, font, c - new Vector2(Width(font, n) / 2, font.FontSize / 2), new Vector4(1, 0.906f, 0.659f, 1), n);
        float w = Width(font, name);
        Text(dl, font, new Vector2(side > 0 ? c.X + P(11) : c.X - P(11) - w, c.Y - font.FontSize / 2), T.Brass, name);
    }

    /// <summary>The section's header: section 1's dropdown, Whole section, section 2's dropdown ("as 1" when locked).</summary>
    void SectionHeader(ImDrawListPtr dl, Vector2 pos, Vector2 size, ProtPane pp)
    {
        var font = UiFonts.SansSmall;
        ImGui.PushFont(font);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(5), 0));
        float ddW = P(160);
        float x = pos.X + P(6) + Width(font, "SECTIONS") + P(10);
        var c1 = new Vector2(x + P(7), pos.Y + P(9));
        Badge(dl, c1, "1", font);
        ImGui.SetCursorScreenPos(new Vector2(x + P(18), pos.Y + P(1)));
        ImGui.PushID("cut1");
        if (ImGui.Button(CutName(pp, protCutX[0], protCutStop[0]), new Vector2(ddW, 0)))
            openCutMenu = 0;
        ImGui.PopID();
        cutAnchor[0] = new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y + 1);
        Twisty(dl, new Vector2(ImGui.GetItemRectMax().X - P(7), pos.Y + P(9)), true, T.Muted);
        if (ImGui.IsItemHovered())
            Tip("Section 1's station: the machinery, each magazine, the ends; or drag its flag on the elevation");
        // from the right: badge 2, its dropdown, Whole section
        float rx = pos.X + size.X - P(6);
        var c2 = new Vector2(rx - P(7), pos.Y + P(9));
        Badge(dl, c2, "2", font, dim: protLinked);
        ImGui.SetCursorScreenPos(new Vector2(rx - P(18) - ddW, pos.Y + P(1)));
        ImGui.PushID("cut2");
        if (protLinked)
        {
            ImGui.BeginDisabled();
            ImGui.Button("as 1", new Vector2(ddW, 0));
            ImGui.EndDisabled();
        }
        else if (ImGui.Button(CutName(pp, protCutX[1], protCutStop[1]), new Vector2(ddW, 0)))
            openCutMenu = 1;
        ImGui.PopID();
        cutAnchor[1] = new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y + 1);
        Twisty(dl, new Vector2(ImGui.GetItemRectMax().X - P(7), pos.Y + P(9)), true, T.Muted);
        const string whole = "Whole section";
        float ww = Width(font, whole) + P(10);
        ImGui.SetCursorScreenPos(new Vector2(rx - P(26) - ddW - ww, pos.Y + P(1)));
        ImGui.PushID("whole");
        if (Seg(whole, protLinked, ww))
            ToggleLinked();
        ImGui.PopID();
        if (ImGui.IsItemHovered())
            Tip(protLinked ? "One whole section, at section 1's station: the flag and the dropdown move both halves.\nClick to compare two stations again (2 goes back where it was)."
                : "Whole section: the right half takes the left's station and stays with it as you move it.");
        ImGui.PopStyleVar();
        ImGui.PopFont();
    }

    static void Diamond(ImDrawListPtr dl, Vector2 c, float r, Vector4 col) =>
        dl.AddQuadFilled(c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0), T.U32(col));

    static void Badge(ImDrawListPtr dl, Vector2 c, string n, ImFontPtr font, bool dim = false)
    {
        float a = dim ? 0.45f : 1;
        dl.AddCircleFilled(c, P(6.5f), T.U32(new Vector4(0.29f, 0.243f, 0.125f, a)));
        dl.AddCircle(c, P(6.5f), T.U32(T.Brass with { W = a }));
        Text(dl, font, c - new Vector2(Width(font, n) / 2, font.FontSize / 2), new Vector4(1, 0.906f, 0.659f, a), n);
    }

    /// <summary>Where the belt leans: an upright from its top, an arc to the lean, the angle.</summary>
    void LeanArc(ImDrawListPtr dl, ProtectionSection.Map m, ProtectionSection s, ImFontPtr font)
    {
        if (s.Belt is not { Lean: > 0 } b)
            return;
        uint brass = T.U32(T.Brass);
        float r = 1.6f, a = b.Lean * MathF.PI / 180;
        Dashed(dl, m.P(b.TopY, b.Top), m.P(b.TopY, b.Top - 2.4f), brass, P(2), vertical: true);
        const int n = 10;
        for (int i = 0; i < n; i++)
        {
            float a0 = a * i / n, a1 = a * (i + 1) / n;
            dl.AddLine(m.P(b.TopY - MathF.Sin(a0) * r, b.Top - MathF.Cos(a0) * r), m.P(b.TopY - MathF.Sin(a1) * r, b.Top - MathF.Cos(a1) * r), brass);
        }
        string t = leanText.Lean == b.Lean ? leanText.Text : (leanText = (b.Lean, $"{b.Lean:0}°")).Text;
        var at = m.P(b.TopY, b.Top - 2.2f);
        Text(dl, font, new Vector2(m.Side > 0 ? at.X + P(4) : at.X - P(4) - Width(font, t), at.Y), T.Brass, t);
    }

    (float Lean, string Text) leanText = (float.NaN, "");

    /// <summary>The selected part's handles on the halves; a press starts a drag (protDragI 10 + half).</summary>
    DragKind SectionHandles(ImDrawListPtr dl, ProtectionSection.Map[] maps, ProtectionSection[] secs, ProtPane pp, Vector2 mouse, bool hover, bool press, bool one)
    {
        var hot = DragKind.None;
        var d = doc.Current;
        var sel = protSel;
        int forced = protForceTip is { } ft && ft.StartsWith("sechandle:", StringComparison.Ordinal) ? int.Parse(ft[10..], CultureInfo.InvariantCulture) : -1;
        int n = 0;
        void H(DragKind kind, int half, Vector2 at, int shape, string tip, int row = -1)
        {
            bool on = hover && Vector2.Distance(mouse, at) <= P(8) && protDrag == DragKind.None;
            bool active = protDrag == kind && protDragI == 10 + half;
            if (shape == 2)
                RoundHandle(dl, at, on || active);
            else
                Handle(dl, at, shape == 1, on || active);
            if (on)
            {
                hot = kind;
                Tip(tip);
                if (press)
                {
                    StartDrag(kind, 10 + half);
                    protKneeRow = row;
                }
            }
            if (n++ == forced)
                TipAt(tip, at + new Vector2(P(10), P(8)));
        }
        for (int h = one ? 1 : 0; h < 2; h++)
        {
            var s = secs[h];
            var m = maps[h];
            if (sel.Kind is PK.Belt or PK.BeltZone && s.Belt is { } b && PE.Has(d, Part.Belt))
            {
                float YAt(float z) => b.Inside ? b.TopY - (b.Top - z) * MathF.Tan(b.Lean * MathF.PI / 180) : s.HalfB(z);
                H(DragKind.BeltTop, h, m.P(YAt(b.Top) - 0.2f, b.Top), 0, pp.BeltTip);
                H(DragKind.BeltBottom, h, m.P(YAt(b.Bottom) - 0.2f, b.Bottom), 0, pp.BeltTip);
                if (h == 1 || one || secs[1].Belt == null)
                {
                    float a = b.Lean * MathF.PI / 180;
                    H(DragKind.Lean, h, m.P(b.TopY + MathF.Sin(a) * 1.4f + 0.15f, b.Top + MathF.Cos(a) * 1.4f), 2,
                        leanTip ??= "The belt's lean, its top outboard (0-30°)\nDrag out to lean it.");
                }
            }
            if (sel.Kind is PK.Deck or PK.Slope or PK.DeckZone)
                foreach (var kn in s.Knees)
                    if (kn.Row == sel.I)
                    {
                        H(DragKind.Knee, h, m.P(kn.Y, kn.Z), 1, kneeTip ??= "Where the slope starts: drag in or out to set its angle.\nIts foot stays on the belt's lower edge.", kn.Row);
                        break;
                    }
            if (sel.Kind == PK.Tds && s.Holding is { } hd)
                H(DragKind.TdsDepth, h, m.P(hd.Y, (hd.Z0 + hd.Z1) / 2), 1, pp.TdsTip);
            if (sel.Kind == PK.Inner && s.OwnInner is { } oi)
                H(DragKind.InnerWall, h, m.P(oi.Y, (oi.Z0 + oi.Z1) / 2), 1, innerTip ??= "The inner bulkhead's own wall: drag it in or out from the side");
            if (sel.Kind is PK.Wings or PK.Box && !PE.Has(d, Part.Tds) && s.WingWall is { } ww)
                H(DragKind.Wing, h, m.P(ww.Y, (ww.Z0 + ww.Z1) / 2), 1,
                    wingTip ??= "The magazine's wing wall: drag it for the wings' width (stores between magazine and side).\nThe handle sets the input, not the cell: in a fine end the wing is measured halfway up the magazine.");
        }
        return hot;
    }

    string? leanTip, kneeTip, innerTip, wingTip;

    void WallDrag(ImDrawListPtr dl, ProtectionSection.Map m, Vector2 p, float wallY, float z0, float z1, NumberKnob k, double lo, double hi)
    {
        float side = wallY + (float)(k.Value(doc.Current, shown?.Ship) ?? 0);
        protDragValue = Math.Clamp(Math.Round((side - p.X) * 10) / 10, lo, hi);
        float y = side - (float)protDragValue;
        dl.AddLine(m.P(y, z1), m.P(y, z0), T.U32(Light), P(2.5f));
    }

    /// <summary>A section drag in progress: the value under the mouse, drawn at its target, applied once on release.</summary>
    void SectionDrag(ImDrawListPtr dl, ProtectionSection.Map m, ProtectionSection s, ProtPane pp, Vector2 mouse)
    {
        var p = m.Inv(mouse);
        uint light = T.U32(Light);
        switch (protDrag)
        {
            case DragKind.BeltTop or DragKind.BeltBottom when s.Belt is { } b:
            {
                protDragValue = protDrag == DragKind.BeltTop ? Math.Clamp(Math.Round((p.Y - pp.Wl) * 10) / 10, 0.3, -pp.Wl)
                    : Math.Clamp(Math.Round((pp.Wl - p.Y) * 10) / 10, 0, pp.Wl - pp.Ib - 0.2);
                float z = protDrag == DragKind.BeltTop ? pp.Wl + (float)protDragValue : pp.Wl - (float)protDragValue;
                dl.AddLine(m.P(0, z), m.P(s.HalfB(z), z), light, P(2.5f));
                float zt = protDrag == DragKind.BeltTop ? z : b.Top, zb = protDrag == DragKind.BeltBottom ? z : b.Bottom;
                var a0 = m.P(s.HalfB((zt + zb) / 2), zt);
                var a1 = m.P(s.HalfB((zt + zb) / 2) - 10 / m.K, zb);
                Target(dl, Vector2.Min(a0, a1), Vector2.Max(a0, a1));
                break;
            }
            case DragKind.Lean when s.Belt is { } b:
            {
                double deg = Math.Atan2(Math.Max(0, p.X - b.TopY), Math.Max(0.3, p.Y - b.Top)) * 180 / Math.PI;
                protDragValue = Math.Clamp(Math.Round(deg), 0, 30);
                float a = (float)(protDragValue * Math.PI / 180);
                dl.AddLine(m.P(b.TopY + MathF.Sin(a) * 1.4f, b.Top + MathF.Cos(a) * 1.4f), m.P(b.TopY - MathF.Tan(a) * (b.Top - b.Bottom), b.Bottom), light, P(2.5f));
                break;
            }
            case DragKind.Knee:
            {
                ProtectionSection.Knee? kn = null;
                foreach (var q in s.Knees)
                    if (q.Row == protKneeRow)
                        kn = q;
                if (kn is not { } k || pp.Ship?.Design.Armour?.Decks?.ElementAtOrDefault(k.Row) is not { } dk)
                    break;
                float deckZ = DeckZOf(pp, dk.Deck ?? 0);
                float reach = MathF.Max(0.3f, k.FootY - p.X);
                protDragValue = Math.Clamp(Math.Round(Math.Atan2(deckZ - k.FootZ, reach) * 180 / Math.PI), 10, 60);
                float r2 = (deckZ - k.FootZ) / MathF.Tan((float)(protDragValue * Math.PI / 180));
                dl.AddLine(m.P(k.FootY - r2, deckZ), m.P(k.FootY, k.FootZ), light, P(2.5f));
                dl.AddLine(m.P(0, deckZ), m.P(MathF.Max(0, k.FootY - r2), deckZ), T.U32(T.Brass with { W = 0.6f }), P(1.5f));
                break;
            }
            // a wall's value is measured in from the side the engine measures from (the waterline's, or halfway up the
            // magazine's, half-breadth): taken as where the build put the wall plus its value now, so a drag starts from it
            case DragKind.TdsDepth when s.Holding is { } hd:
                WallDrag(dl, m, p, hd.Y, hd.Z0, hd.Z1, Knobs.Tds, 0.5, pp.B / 2 - 2);
                break;
            case DragKind.Wing when s.WingWall is { } ww:
                WallDrag(dl, m, p, ww.Y, ww.Z0, ww.Z1, Knobs.MagazineWing, 0.3, 0.3 * pp.B);
                break;
            case DragKind.InnerWall when s.OwnInner is { } oi:
                WallDrag(dl, m, p, oi.Y, oi.Z0, oi.Z1, Knobs.InnerInboard, 0.5, 8);
                break;
        }
        DragTip(pp);
        EndDragIfReleased();
    }
}
