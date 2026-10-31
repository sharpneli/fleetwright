using System.Diagnostics;
using System.Numerics;
using Fleetwright.Shipgen;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Designer;

/// <summary>
/// The designer's screen, after docs/designer-ui/bureau.html: everything on one screen at table density.
/// <list type="bullet">
/// <item>Menu bar: File, Edit and View (the scene tabs sit at its right end, from <see cref="SceneSwitcher"/>).</item>
/// <item>Resource bar: the name and type label (click to edit) and the figures every edit moves.</item>
/// <item>Left: the outliner (sections with their weight items; a click opens the section's tab) over the templates the
/// design is built from (a click changes one).</item>
/// <item>Centre: the plan and profile, the section tabs (0–7), the open section as dense tables flowed into columns.</item>
/// <item>Right: the weight ledger, the particulars, the remarks.</item>
/// <item>Status bar: the build, the history (click to go back), the keys.</item>
/// </list>
/// Every string comes from the snapshot, so a frame that changes nothing allocates nothing.
/// </summary>
public sealed unsafe partial class DesignerScene
{
    static float LeftW => P(300);
    static float RightW => P(340);
    static float ResBarH => P(34);
    static float StatusH => P(20);

    static readonly string[] DigitText = ["0", "1", "2", "3", "4", "5", "6", "7"];
    static readonly string[] TemplateLabels = ["Plant", "Hull", "Crew", "Armour", "Protection", "Shells", "Guns", "Torpedoes", "Shock", "Outfit"];

    // the plan and profile's overlays: arcs of fire, armour plates, magazines, tags (A, R, M, T)
    bool showArcs = true, showArmour = true, showMags, showTags = true;
    static readonly (string Key, string Label)[] OverlayNames = [("A", "Arcs"), ("R", "Armour"), ("M", "Mags"), ("T", "Tags")];

    readonly bool[] expanded = [true, true, true, true, true, true, true, true];   // the outliner's sections showing their items
    string? pendingPopup;   // a popup a menu asked for, opened in the designer's own window

    public void BuildUi()
    {
        Sync();
        PollProbes();
        if (snap == null || stale)
        {
            snap = Build();
            stale = false;
        }
        Keys();
        T.Push();
        MenuBar();
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.WorkPos);
        ImGui.SetNextWindowSize(vp.WorkSize);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.Begin("##designer", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus |
                                  ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar();
        ImGui.PushFont(UiFonts.Sans);
        if (pendingPopup != null)
        {
            ImGui.OpenPopup(pendingPopup);
            pendingPopup = null;
        }
        SavePopup();
        StylePopup();

        var size = vp.WorkSize;
        float bodyH = size.Y - ResBarH - StatusH, midW = size.X - LeftW - RightW;
        ResourceBar(size.X);

        var s = snap!;
        float tplH = TitleH + RowH * s.Templates.Length + P(4);
        if (BeginPane("##outliner", "DESIGN OUTLINER", "t · bar % std", new Vector2(0, ResBarH), new Vector2(LeftW, bodyH - tplH)))
            Outliner();
        EndPane();
        if (BeginPane("##templates", "TEMPLATES", "click to change", new Vector2(0, ResBarH + bodyH - tplH), new Vector2(LeftW, tplH), scroll: false))
            TemplatesPanel();
        EndPane();

        Centre(new Vector2(LeftW, ResBarH), new Vector2(midW, bodyH));

        float x = LeftW + midW;
        float ledgerH = TitleH + RowH * (s.Ledger.Length + 3) + P(4);
        float partH = TitleH + RowH * s.Particulars.Length + P(24);
        if (BeginPane("##ledger", "WEIGHT LEDGER", "standard", new Vector2(x, ResBarH), new Vector2(RightW, ledgerH), scroll: false))
            Ledger();
        EndPane();
        if (BeginPane("##particulars", "PARTICULARS", null, new Vector2(x, ResBarH + ledgerH), new Vector2(RightW, partH)))
            Particulars();
        EndPane();
        if (BeginPane("##remarks", "REMARKS", s.RemarkCount, new Vector2(x, ResBarH + ledgerH + partH),
                new Vector2(RightW, bodyH - ledgerH - partH)))
            Remarks();
        EndPane();

        StatusBar(new Vector2(0, size.Y - StatusH), size.X);
        ImGui.PopFont();
        ImGui.End();
        T.Pop();
    }

    void Keys()
    {
        var io = ImGui.GetIO();
        if (io.WantTextInput)
            return;
        if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Z))
            Undo();
        else if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Y))
            Redo();
        else if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S))
            AskSave();
        else if (!io.KeyCtrl)
        {
            for (int i = 0; i < Sections.Length; i++)
                if (ImGui.IsKeyPressed(ImGuiKey._0 + i))
                    Open(i);
            if (ImGui.IsKeyPressed(ImGuiKey.A))
                ToggleOverlay(0);
            if (ImGui.IsKeyPressed(ImGuiKey.R))
                ToggleOverlay(1);
            if (ImGui.IsKeyPressed(ImGuiKey.M))
                ToggleOverlay(2);
            if (ImGui.IsKeyPressed(ImGuiKey.T))
                ToggleOverlay(3);
            if (section == 1 && mainSel is { } sel && ImGui.IsKeyPressed(ImGuiKey.Delete))
                RemoveTurret(doc.Current, sel);
            if (section == 2 && secSel is { } ss && ImGui.IsKeyPressed(ImGuiKey.Delete))
                RemoveSecPair(ss);
            if (section == 3 && ImGui.IsKeyPressed(ImGuiKey.Delete))
                ProtDelete();
        }
    }

    bool OverlayOn(int i) => i switch { 0 => showArcs, 1 => showArmour, 2 => showMags, _ => showTags };

    void ToggleOverlay(int i)
    {
        switch (i)
        {
            case 0: showArcs = !showArcs; break;
            case 1: showArmour = !showArmour; break;
            case 2: showMags = !showMags; break;
            default: showTags = !showTags; break;
        }
    }

    void Undo()
    {
        doc.Undo();
        stale = true;
    }

    void Redo()
    {
        doc.Redo();
        stale = true;
    }

    void Open(int s)
    {
        expanded[s] = true;
        if (s == section)
            return;
        section = s;
        editing = null;
        doc.Seal();
        stale = true;
    }

    void AskSave()
    {
        saveName = Path.GetFileNameWithoutExtension(DesignDoc.FileName(doc.Current));
        pendingPopup = "save";
    }

    // ------------------------------------------------------------------ chrome (the pieces are Ui/Pieces.cs)

    /// <summary>A pane at <paramref name="pos"/> in the designer window (<see cref="Pieces.Pane"/>), its body a child
    /// window with its rows edge to edge until <see cref="EndPane"/>.</summary>
    static bool BeginPane(string id, string title, string? note, Vector2 pos, Vector2 size, bool scroll = true)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(P(6), 0));   // rows sit edge to edge; EndPane pops it
        ImGui.SetCursorPos(pos);
        var p = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().Pane(p, size, title, note);
        return BeginBody(id, p, size, out _, scroll);
    }

    static void EndPane()
    {
        ImGui.EndChild();
        ImGui.PopStyleVar();
    }

    // ------------------------------------------------------------------ menu bar and resource bar

    static readonly string[] Navies = ["(the design's own)", .. Shipgen.Render.Looks.Navies];
    static readonly string[] Eras = ["(the design's own)", .. Shipgen.Render.Looks.Eras];

    /// <summary>The designer's menus, appended to the main menu bar (the scene switcher puts the scenes at its end).</summary>
    void MenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
            return;
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.IsWindowAppearing())
            {
                shippedDesigns = List(Path.Combine(AppContext.BaseDirectory, "Content", "Designs"));
                userDesigns = List(DesignDoc.UserFolder);
            }
            if (ImGui.MenuItem("New: an empty 8 kn hull"))
                Load(DesignDoc.New());
            DesignMenu("Open my design", userDesigns);
            if (ImGui.MenuItem("Open the save folder in Explorer"))
                ShowFolder(DesignDoc.UserFolder);
            DesignMenu("Open a shipped design", shippedDesigns);
            ImGui.Separator();
            if (ImGui.MenuItem("Save as…", "Ctrl+S"))
                AskSave();
            if (onClose != null)
            {
                ImGui.Separator();
                if (ImGui.MenuItem("Accept design"))
                    onClose(doc.Current);
                if (ImGui.MenuItem("Cancel"))
                    onClose(null);
            }
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Edit"))
        {
            if (ImGui.MenuItem("Undo", "Ctrl+Z", false, doc.CanUndo))
                Undo();
            if (ImGui.MenuItem("Redo", "Ctrl+Y", false, doc.CanRedo))
                Redo();
            ImGui.Separator();
            if (ImGui.MenuItem("Reset to as opened", null, false, doc.Index > 0 || doc.Dirty))
            {
                doc.Reset();
                stale = true;
            }
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("View"))
        {
            if (ImGui.MenuItem("Metric", null, units == UnitSystem.Metric))
                SetUnits(UnitSystem.Metric);
            if (ImGui.MenuItem("Imperial", null, units == UnitSystem.Imperial))
                SetUnits(UnitSystem.Imperial);
            ImGui.Separator();
            for (int i = 0; i < OverlayNames.Length; i++)
                if (ImGui.MenuItem(OverlayNames[i].Label, OverlayNames[i].Key, OverlayOn(i)))
                    ToggleOverlay(i);
            ImGui.Separator();
            if (ImGui.MenuItem(snap!.Style + "…"))
            {
                pennant = doc.Current.Look?.Number ?? "";
                pendingPopup = "style";
            }
            ImGui.EndMenu();
        }
        ImGui.EndMainMenuBar();
    }

    void SetUnits(UnitSystem u)
    {
        if (u == units)
            return;
        units = u;
        if (session != null)
            session.Units = u;
        stale = true;
    }

    /// <summary>Opens <paramref name="folder"/> in the file browser, to share designs by hand (made if missing).</summary>
    static void ShowFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"designer: {folder}: {e.Message}");
        }
    }

    void DesignMenu(string title, string[] files)
    {
        if (!ImGui.BeginMenu(title, files.Length > 0))
            return;
        foreach (var f in files)
            if (ImGui.MenuItem(Path.GetFileNameWithoutExtension(f)))
            {
                try
                {
                    Load(DesignDoc.Open(f));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"designer: {f}: {e.Message}");
                }
            }
        ImGui.EndMenu();
    }

    void ResourceBar(float width)
    {
        var s = snap!;
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        dl.ResourceBarBg(p, width, ResBarH);

        // the type label over the name: click to edit, Enter keeps it, Escape or clicking away doesn't
        ImGui.SetCursorPos(V(10, 1));
        TextField("##type", s.TypeLabel, UiFonts.SansSmall, T.Muted, P(200), doc.Current.Type ?? "",
            v => doc.Current with { Type = v.Length > 0 ? v : null }, "Type label");
        ImGui.SetCursorPos(V(10, 14));
        TextField("##name", s.Title, UiFonts.Title, T.Bright, P(260), doc.Current.Name ?? "",
            v => doc.Current with { Name = v.Length > 0 ? v : null }, "Name");
        ImGui.SameLine(0, P(8));
        ImGui.SetCursorPosY(P(17));
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.TextColored(T.Muted, s.File);
        ImGui.PopFont();
        float x = Math.Max(P(250), ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + P(12));

        bool busy = worker.Busy;
        float sx = p.X + x;
        for (int i = 0; i < s.Stats.Length; i++)
        {
            var (label, value) = s.Stats[i];
            float cell = sx;
            dl.ResourceCell(ref sx, p.Y, ResBarH, label, value, busy ? T.Muted : null);
            if (i == 0)   // the standard displacement: its groups on hover
            {
                ImGui.SetCursorScreenPos(new Vector2(cell, p.Y));
                ImGui.InvisibleButton("##std", new Vector2(sx - cell, ResBarH));
                if (ImGui.IsItemHovered())
                    LedgerTip("Standard displacement");
            }
        }
        dl.ResourceEnd(sx, p.Y, ResBarH);

        if (onClose != null)
        {
            ImGui.SetCursorPos(new Vector2(width - P(190), P(6)));
            ImGui.PushStyleColor(ImGuiCol.Button, T.BrassDark);
            ImGui.PushStyleColor(ImGuiCol.Text, T.Brass);
            if (ImGui.Button("Accept design"))
                onClose(doc.Current);
            ImGui.PopStyleColor(2);
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                onClose(null);
        }
    }

    /// <summary>The ledger as a tooltip: each group's tonnes.</summary>
    void LedgerTip(string title)
    {
        var s = snap!;
        ImGui.BeginTooltip();
        ImGui.PushFont(UiFonts.SansBold);
        ImGui.TextColored(T.Brass, title);
        ImGui.PopFont();
        if (ImGui.BeginTable("##tip", 2, ImGuiTableFlags.SizingFixedFit))
        {
            foreach (var g in s.Ledger)
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(g.Name);
                ImGui.TableNextColumn();
                RightInCell(UiFonts.Mono, g.T, T.Bright);
            }
            ImGui.EndTable();
        }
        ImGui.EndTooltip();
    }

    /// <summary>Text that turns into an input when clicked; Enter applies <paramref name="apply"/> as a history entry.</summary>
    void TextField(string id, string shown, ImFontPtr font, Vector4 colour, float width, string current, Func<string, Design> apply, string what)
    {
        if (editing == id)
        {
            ImGui.SetNextItemWidth(width);
            if (editFocus)
            {
                ImGui.SetKeyboardFocusHere();
                editFocus = false;
            }
            if (ImGui.InputText(id, ref editText, 80, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
            {
                var v = editText.Trim();
                editing = null;
                Edit(apply(v), $"{what}: {(v.Length > 0 ? v : "none")}");
            }
            else if (ImGui.IsItemDeactivated())
                editing = null;
            return;
        }
        ImGui.PushFont(font);
        ImGui.TextColored(colour, shown);
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
            ImGui.SetTooltip("click to edit");
        }
        if (ImGui.IsItemClicked())
        {
            editing = id;
            editText = current;
            editFocus = true;
        }
    }

    // ------------------------------------------------------------------ popups

    /// <summary>The navy and era the sprite is drawn in: tried on without touching the design (and shown in the other
    /// scenes too), or kept in it.</summary>
    void StylePopup()
    {
        if (!ImGui.BeginPopup("style"))
            return;
        ImGui.TextColored(T.Muted, "How she looks: a navy's paint and fittings in an era");
        var look = Look;
        int ni = Math.Max(0, Array.IndexOf(Navies, look?.Navy)), ei = Math.Max(0, Array.IndexOf(Eras, look?.Era));
        ImGui.SetNextItemWidth(P(260));
        bool changed = ImGui.Combo("navy", ref ni, Navies, Navies.Length);
        ImGui.SetNextItemWidth(P(260));
        changed |= ImGui.Combo("era", ref ei, Eras, Eras.Length);
        if (changed)
            SetLook(ni == 0 && ei == 0 ? null : new LookInput { Navy = ni > 0 ? Navies[ni] : null, Era = ei > 0 ? Eras[ei] : null });
        ImGui.Separator();
        ImGui.SetNextItemWidth(P(120));
        if (ImGui.InputText("pennant number", ref pennant, 12, ImGuiInputTextFlags.EnterReturnsTrue) || ImGui.IsItemDeactivatedAfterEdit())
        {
            var v = pennant.Trim();
            var d = doc.Current;
            Edit(d with { Look = (d.Look ?? new LookInput()) with { Number = v.Length > 0 ? v : null } }, $"Pennant number: {(v.Length > 0 ? v : "none")}");
        }
        ImGui.TextColored(T.Muted, "painted on the hull by the looks that carry numbers (cold war)");
        if (look != null)
        {
            if (ImGui.Button("Keep in the design"))
            {
                var d = doc.Current;
                var kept = (d.Look ?? new LookInput()) with { Navy = look.Navy ?? d.Look?.Navy, Era = look.Era ?? d.Look?.Era };
                Edit(d with { Look = kept }, $"Look: {kept.Navy ?? "generic"}, {kept.Era ?? "wwii"}");
                SetLook(null);
            }
            ImGui.SameLine();
            if (ImGui.Button("Back to the design's own"))
                SetLook(null);
        }
        ImGui.EndPopup();
    }

    static string[] List(string dir) => Directory.Exists(dir)
        ? Directory.GetFiles(dir, "*.json").Order(StringComparer.Ordinal).ToArray()
        : [];

    void SavePopup()
    {
        if (!ImGui.BeginPopup("save"))
            return;
        ImGui.TextColored(T.Muted, DesignDoc.UserFolder);
        ImGui.SetNextItemWidth(P(260));
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();
        bool enter = ImGui.InputText("##name", ref saveName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Save") || enter) && saveName.Trim().Length > 0)
        {
            var file = DesignDoc.FileName(doc.Current with { Name = saveName.Trim() });
            doc.Save(Path.Combine(DesignDoc.UserFolder, file));
            session?.Saved(doc.SourcePath!);
            stale = true;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    // ------------------------------------------------------------------ left: outliner and templates

    void Outliner()
    {
        var s = snap!;
        var dl = ImGui.GetWindowDrawList();
        float w = ImGui.GetContentRegionAvail().X;
        float tW = P(50), barW = P(30);
        for (int n = 0; n < s.Outline.Length; n++)
        {
            var r = s.Outline[n];
            if (r.Child && !expanded[r.Section])
                continue;
            bool hasItems = !r.Child && n + 1 < s.Outline.Length && s.Outline[n + 1].Child;
            float h = r.Child ? P(18) : P(21);
            var p = ImGui.GetCursorScreenPos();
            ImGui.PushID(n);
            bool clicked = ImGui.InvisibleButton("##o", new Vector2(w, h));
            bool hover = ImGui.IsItemHovered();
            ImGui.PopID();
            if (clicked)
            {
                if (hasItems && ImGui.GetMousePos().X < p.X + P(18))
                    expanded[r.Section] = !expanded[r.Section];
                else
                    Open(r.Section);
            }
            bool sel = r.Section == section;
            var bg = sel ? (r.Child ? T.Brass with { W = 0.05f } : T.Brass with { W = 0.14f })
                : r.Child ? Vector4.Zero : T.Panel2;
            if (hover)
                bg += new Vector4(0.04f, 0.04f, 0.04f, 0.06f);
            dl.AddRectFilled(p, p + new Vector2(w, h), T.U32(bg));
            dl.AddLine(p + new Vector2(0, h - 1), p + new Vector2(w, h - 1), T.U32(T.Line));
            float cy = p.Y + h / 2;
            float x = p.X + P(r.Child ? 30 : 6);
            if (hasItems)
                dl.Twisty(new Vector2(p.X + P(10), cy), expanded[r.Section], T.Muted);
            if (!r.Child)
            {
                x += P(12);
                dl.AddRectFilled(new Vector2(x, cy - P(4)), new Vector2(x + P(8), cy + P(4)), T.U32(r.Group.Length > 0 ? T.Group(r.Group) : T.Brass));
                x += P(13);
                dl.Text(UiFonts.Mono, new Vector2(x, cy - UiFonts.Mono.FontSize / 2), sel ? T.Brass : T.Muted, DigitText[r.Section]);
                x += P(12);
            }
            float right = p.X + w - tW - barW - P(10);
            var font = r.Child ? UiFonts.SansSmall : UiFonts.SansBold;
            dl.PushClipRect(new Vector2(x, p.Y), new Vector2(right, p.Y + h), true);
            dl.Text(font, new Vector2(x, cy - font.FontSize / 2), r.Child ? T.Text : sel ? T.Brass : T.Bright, r.Label);
            if (r.Detail.Length > 0)
                dl.Text(UiFonts.SansSmall, new Vector2(x + Width(font, r.Label) + P(8), cy - UiFonts.SansSmall.FontSize / 2), T.Muted, r.Detail);
            dl.PopClipRect();
            float tw = Width(UiFonts.Mono, r.T);
            dl.Text(UiFonts.Mono, new Vector2(p.X + w - barW - P(8) - tw, cy - UiFonts.Mono.FontSize / 2), r.Child ? T.Muted : T.Text, r.T);
            if (r.Frac > 0)
                dl.Bar(new Vector2(p.X + w - barW - P(4), cy - P(2.5f)), new Vector2(barW, P(5)), r.Frac * 2.5f,
                    r.Group.Length > 0 ? T.Group(r.Group) : T.Muted);
        }
    }

    void TemplatesPanel()
    {
        var s = snap!;
        var dl = ImGui.GetWindowDrawList();
        float w = ImGui.GetContentRegionAvail().X;
        for (int i = 0; i < s.Templates.Length; i++)
        {
            var t = s.Templates[i];
            var p = ImGui.GetCursorScreenPos();
            ImGui.PushID(i);
            if (ImGui.InvisibleButton("##t", new Vector2(w, RowH)))
                ImGui.OpenPopup("pick");
            bool hover = ImGui.IsItemHovered();
            dl.RowBg(p, w, RowH, i);
            if (hover)
                dl.AddRectFilled(p, p + new Vector2(w, RowH), T.U32(T.Brass with { W = 0.08f }));
            float ty = p.Y + (RowH - UiFonts.Sans.FontSize) / 2;
            dl.Text(UiFonts.Sans, new Vector2(p.X + P(6), ty), T.Muted, TemplateLabels[i]);
            dl.PushClipRect(p + new Vector2(P(72), 0), p + new Vector2(w - P(14), RowH), true);
            dl.Text(UiFonts.Sans, new Vector2(p.X + P(72), ty), hover ? T.Brass : T.Text, t.Names[t.Index]);
            dl.PopClipRect();
            dl.Twisty(new Vector2(p.X + w - P(8), p.Y + RowH / 2), true, T.Muted);
            if (hover)
                Tip(t.Index < t.Count && t.Tips != null ? t.Tips[t.Index] : t.Names[t.Index], 460);
            if (ImGui.BeginPopup("pick"))
            {
                ImGui.TextColored(T.Muted, t.Label);
                ImGui.Separator();
                t.Popup?.Invoke();
                PickList(t);
                ImGui.EndPopup();
            }
            ImGui.PopID();
        }
    }

    /// <summary>A picker's catalogue: entries under their groups' headings, each one's tooltip on hover; the design's own
    /// block (not in the catalogue) shown after, not pickable.</summary>
    void PickList(PickRow t)
    {
        string? group = null;
        for (int k = 0; k < t.Count; k++)
        {
            if (t.Groups?[k] is { } g && g != group)
            {
                group = g;
                ImGui.TextColored(T.Brass, g);
            }
            if (ImGui.Selectable(t.Names[k], k == t.Index) && k != t.Index)
                t.Apply(k);
            if (t.Tips != null && ImGui.IsItemHovered())
                Tip(t.Tips[k], 460);
        }
        if (t.Index >= t.Count && t.Popup == null)
            ImGui.TextColored(T.Muted, t.Names[t.Index]);
    }
    // ------------------------------------------------------------------ centre: plan, profile, tabs, tables

    void Centre(Vector2 pos, Vector2 size)
    {
        var s = snap!;
        float pad = P(6);
        float w = size.X - 2 * pad - 2;
        float topH = MathF.Round(Math.Clamp(size.Y * 0.27f, P(150), P(420)));
        float profH = MathF.Round(Math.Clamp(size.Y * 0.12f, P(70), P(190)));
        float planH = TitleH + pad + topH + pad + profH + pad + 2;
        topSize = new Vector2(w, topH);
        profileSize = new Vector2(w, profH);
        Offscreen(ref topTarget, topSize);
        Offscreen(ref profileTarget, profileSize);
        var tint = worker.Busy ? new Vector4(1, 1, 1, 0.75f) : Vector4.One;

        if (BeginPane("##plan", "PLAN & PROFILE", null, pos, new Vector2(size.X, planH), scroll: false))
        {
            var pdl = ImGui.GetWindowDrawList();
            ImGui.SetCursorPos(new Vector2(pad, pad));
            var p = ImGui.GetCursorScreenPos();
            ImGui.Image((nint)topTarget!.Resolve, topSize, Vector2.Zero, Vector2.One, tint);
            ImGui.SetCursorPos(new Vector2(pad, pad + topH + pad));
            var pq = ImGui.GetCursorScreenPos();
            ImGui.Image((nint)profileTarget!.Resolve, profileSize, Vector2.Zero, Vector2.One, tint);
            if (s.Over is { } ov && sprite.Meta != null)
            {
                pdl.PushClipRect(p, p + topSize, true);
                PlanOverlays(pdl, p, ov);
                pdl.PopClipRect();
                pdl.PushClipRect(pq, pq + profileSize, true);
                if (showArmour)
                    ArmourOverlay(pdl, pq, ov);
                pdl.PopClipRect();
            }
            ScaleBar(pdl, p + new Vector2(P(10), topH - P(12)), topView.Zoom);
        }
        EndPane();
        OverlayToggles(pos + new Vector2(size.X, 0));

        float tabY = pos.Y + planH + P(4);
        Tabs(new Vector2(pos.X + P(4), tabY), size.X - P(8));
        float detailY = tabY + P(22);
        var detail = new Vector2(size.X, pos.Y + size.Y - detailY);
        ImGui.SetCursorPos(new Vector2(pos.X, detailY));
        var dl = ImGui.GetWindowDrawList();
        var q = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(q, q + detail, T.U32(T.Panel));
        dl.AddRect(q, q + detail, T.U32(T.Edge));
        dl.AddLine(q + new Vector2(1, 0), q + new Vector2(detail.X - 1, 0), T.U32(T.TitleTop));
        ImGui.SetCursorPos(new Vector2(pos.X + 1, detailY + 1));
        var inner = detail - new Vector2(2, 2);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(P(6), 0));
        if (section == 0 && s.Ov != null)
            OverviewPane(inner);
        else if (section == 1 && s.Main != null)
            MainBatteryPane(inner);
        else if (section == 2 && s.Sec != null)
            SecondaryPane(inner);
        else if (section == 3 && s.Prot != null)
            ProtectionPane(inner);
        else
            Flow("##panel", inner, s.Panel, columns: 0);
        ImGui.PopStyleVar();
    }

    /// <summary>The overlay toggles at the right end of the plan's title bar (<paramref name="topRight"/>, in the window).</summary>
    void OverlayToggles(Vector2 topRight)
    {
        var dl = ImGui.GetWindowDrawList();
        var w = ImGui.GetWindowPos();
        float right = w.X + topRight.X - P(6);
        for (int i = OverlayNames.Length - 1; i >= 0; i--)
        {
            ImGui.PushID(i);
            if (dl.Toggle("##ov", ref right, w.Y + topRight.Y, OverlayNames[i].Key, OverlayNames[i].Label, OverlayOn(i)))
                ToggleOverlay(i);
            ImGui.PopID();
        }
    }

    /// <summary>The plan's overlays: the length overall over the ship, the arcs of fire, the mounts' tags. Ship metres
    /// map to the image as the sprite renderer maps them (bow right, starboard down, the ship's middle at the middle).</summary>
    void PlanOverlays(ImDrawListPtr dl, Vector2 img, Overlay ov)
    {
        float z = topView.Zoom;
        var mid = img + topSize / 2;
        Vector2 Px(float x, float y) => mid + new Vector2(x * z, y * z);
        if (showArcs)
            foreach (var m in ov.Mounts)
            {
                float r = ov.Length * (m.Main ? 0.13f : 0.065f) * z;
                foreach (var (from, to) in m.Arcs)
                    Sector(dl, Px(m.X, m.Y), r, from, to, m.Main);
            }
        // the main battery tab's hovered or selected turret, ringed; a click on a main turret selects it there
        if (section == 1 && planFocus != null)
            foreach (var m in ov.Mounts)
                if (m.Main && m.Id == planFocus)
                    dl.AddCircle(Px(m.X, m.Y), P(14), T.U32(T.BrassLight), 0, P(2));
        if (section == 1 && snap?.Main is { } mp && ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var mouse = ImGui.GetMousePos();
            foreach (var m in ov.Mounts)
                if (m.Main && Vector2.Distance(mouse, Px(m.X, m.Y)) < P(14))
                    foreach (var ch in mp.Chips)
                        if (!ch.Ghost && ch.Slot.Name == m.Id)
                        {
                            mainSel = ch.Slot;
                            mainActive = ch.Slot.Battery;
                            stale = true;
                        }
        }
        // the Secondary tab's hovered or selected pair (or its battery), ringed; a click on a secondary selects its pair
        if (section == 2 && secFocus is { } f)
            foreach (var m in ov.Mounts)
                if (!m.Main && SecondaryArrangement.ParseId(m.Id) is var (k, n, _) && k == f.K && (f.N == 0 || n == f.N))
                    dl.AddCircle(Px(m.X, m.Y), P(f.N == 0 ? 8 : 11), T.U32(T.BrassLight with { W = f.N == 0 ? 0.6f : 1 }), 0, P(f.N == 0 ? 1.2f : 2));
        if (section == 2 && snap?.Sec != null && ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var mouse = ImGui.GetMousePos();
            foreach (var m in ov.Mounts)
                if (!m.Main && Vector2.Distance(mouse, Px(m.X, m.Y)) < P(10) && SecondaryArrangement.ParseId(m.Id) is var (k, n, _))
                {
                    (secActive, secSel) = (k, (k, n));
                    stale = true;
                }
        }
        if (showTags)
        {
            // the length overall, with ticks at the ends, over the ship
            float y = img.Y + P(14);
            float x0 = Px(-ov.Length / 2, 0).X, x1 = Px(ov.Length / 2, 0).X;
            uint line = T.U32(T.PlanLine);
            dl.AddLine(new Vector2(x0, y), new Vector2(x1, y), line);
            dl.AddLine(new Vector2(x0, y - P(5)), new Vector2(x0, y + P(5)), line);
            dl.AddLine(new Vector2(x1, y - P(5)), new Vector2(x1, y + P(5)), line);
            float tw = Width(UiFonts.Mono, ov.Dimensions);
            var tp = new Vector2((x0 + x1 - tw) / 2, y - UiFonts.Mono.FontSize / 2);
            dl.AddRectFilled(tp - V(6, 0), tp + new Vector2(tw + P(6), UiFonts.Mono.FontSize), T.U32(T.Plan));
            dl.Text(UiFonts.Mono, tp, T.TagText, ov.Dimensions);
            foreach (var m in ov.Mounts)
            {
                // main turrets tagged off the port side, the others just outboard of themselves
                var at = m.Main ? Px(m.X, -ov.Beam / 2) - new Vector2(0, P(14)) : Px(m.X, m.Y) + new Vector2(0, m.Y > 0 ? P(12) : -P(12));
                dl.TagCentred(at, m.Tag, m.Main ? UiFonts.Mono : UiFonts.SansSmall);
            }
        }
    }

    /// <summary>An arc of fire: a translucent sector, in slices of at most 90° so each fills as a convex shape, and its
    /// outline. Degrees clockwise from dead ahead, which on the plan (bow right, starboard down) is ImGui's angle.</summary>
    static void Sector(ImDrawListPtr dl, Vector2 c, float r, float fromDeg, float toDeg, bool main)
    {
        const float Rad = MathF.PI / 180;
        float a0 = fromDeg * Rad, a1 = toDeg * Rad;
        int slices = Math.Max(1, (int)MathF.Ceiling((a1 - a0) / (MathF.PI / 2)));
        uint fill = T.U32(T.Arc with { W = 0.06f });
        for (int i = 0; i < slices && main; i++)   // the secondaries' arcs are outlines only: a dozen fills is fog
        {
            dl.PathLineTo(c);
            dl.PathArcTo(c, r, a0 + (a1 - a0) * i / slices, a0 + (a1 - a0) * (i + 1) / slices, 12);
            dl.PathFillConvex(fill);
        }
        dl.PathLineTo(c);
        dl.PathArcTo(c, r, a0, a1, 48);
        dl.PathStroke(T.U32(T.Arc with { W = main ? 0.45f : 0.22f }), ImDrawFlags.Closed, 1);
    }

    /// <summary>The armour on the profile: each plate in the thickness ramp, labelled where the label fits. Ship metres
    /// map through the profile camera (orthographic, from the side).</summary>
    void ArmourOverlay(ImDrawListPtr dl, Vector2 img, Overlay ov)
    {
        var (right, up, _) = profileCam.Basis();
        float aspect = profileSize.X / Math.Max(1, profileSize.Y), hh = profileCam.HalfHeight;
        var target = profileCam.Target;
        Vector2 Px(float x, float z)
        {
            var d = new Vector3(x, 0, z) - target;
            return img + new Vector2((Vector3.Dot(d, right) / (hh * aspect) + 1) / 2 * profileSize.X, (1 - Vector3.Dot(d, up) / hh) / 2 * profileSize.Y);
        }
        var font = UiFonts.SansSmall;
        foreach (var pl in ov.Plates)
        {
            var a = Px(pl.X0, pl.Z1);
            var b = Px(pl.X1, pl.Z0);
            var min = Vector2.Min(a, b);
            var max = Vector2.Max(Vector2.Max(a, b), min + new Vector2(1.5f, 1.5f));
            dl.AddRectFilled(min, max, T.U32(T.Armour(pl.Mm)));
            dl.AddRect(min, max, T.U32(new Vector4(0, 0, 0, 0.4f)));
        }
        foreach (var pl in ov.Plates)   // the labels after every plate, so a deck crossing a belt doesn't hide its label
        {
            if (pl.Label.Length == 0)
                continue;
            var a = Px(pl.X0, pl.Z1);
            var b = Px(pl.X1, pl.Z0);
            var min = Vector2.Min(a, b);
            var max = Vector2.Max(a, b);
            float tw = Width(font, pl.Label);
            if (max.Y - min.Y >= font.FontSize && max.X - min.X >= tw + P(4))
                dl.Text(font, (min + max) / 2 - new Vector2(tw / 2, font.FontSize / 2),
                    pl.Mm >= 140 ? new Vector4(0.965f, 0.937f, 0.886f, 1) : new Vector4(0.149f, 0.133f, 0.11f, 1), pl.Label);
        }
    }

    /// <summary>The section tabs 0–7 at <paramref name="pos"/> (in the window).</summary>
    void Tabs(Vector2 pos, float width)
    {
        int hit = ImGui.GetWindowDrawList().NumberedTabs(ImGui.GetWindowPos() + pos, width, Sections, DigitText, section);
        if (hit >= 0)
            Open(hit);
    }

    /// <summary>A scale bar of a round length that fits about 120 px.</summary>
    void ScaleBar(ImDrawListPtr dl, Vector2 at, float pxPerM)
    {
        if (pxPerM <= 0)
            return;
        double unitM = units == UnitSystem.Metric ? 1 : 0.3048;
        double want = P(120) / pxPerM / unitM;
        double len = new[] { 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000 }.FirstOrDefault(x => x >= want * 0.6, 1000);
        float px = (float)(len * unitM * pxPerM);
        uint c = T.U32(T.Text);
        dl.AddLine(at, at + new Vector2(px, 0), c, P(1.5f));
        dl.AddLine(at + V(0, -4), at + V(0, 4), c, 1.5f);
        dl.AddLine(at + new Vector2(px, P(-4)), at + new Vector2(px, P(4)), c, 1.5f);
        dl.AddText(UiFonts.SansSmall, UiFonts.SansSmall.FontSize, at + new Vector2(px + P(6), P(-8)), c, units == UnitSystem.Metric ? ScaleText(len, "m") : ScaleText(len, "ft"));
    }

    readonly Dictionary<(double, string), string> scaleTexts = [];
    string ScaleText(double len, string unit)
    {
        if (!scaleTexts.TryGetValue((len, unit), out var t))
            scaleTexts[(len, unit)] = t = $"{len:0} {unit}";
        return t;
    }

    /// <summary>
    /// The open section's items as dense tables, flowed newspaper-style into columns (<paramref name="columns"/>, or as
    /// many of about 470 px as fit when 0): a heading and its rows stay together, and the columns come out about even.
    /// </summary>
    void Flow(string id, Vector2 size, List<Item> items, int columns)
    {
        ImGui.BeginChild(id, size);
        int cols = columns > 0 ? columns : Math.Clamp((int)(size.X / P(470)), 1, 3);

        // rows per item as shown (a closed Details folds its rows away), then where each column starts
        int total = 0;
        bool folded = false;
        for (int n = 0; n < items.Count; n++)
            total += Rows(items[n], ref folded);
        Span<int> starts = stackalloc int[4];
        starts[0] = 0;
        int col = 1, acc = 0;
        folded = false;
        for (int n = 0; n < items.Count && col < cols; n++)
        {
            if (items[n] is Header && n > 0 && acc >= (total * col + cols - 1) / cols)
                starts[col++] = n;
            acc += Rows(items[n], ref folded);
        }
        for (int c = col; c <= cols; c++)
            starts[c] = items.Count;
        cols = col;

        if (ImGui.BeginTable("##flow", cols, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoPadOuterX))
        {
            for (int c = 0; c < cols; c++)
            {
                ImGui.TableNextColumn();
                Column(items, starts[c], c + 1 < cols ? starts[c + 1] : items.Count);
            }
            ImGui.EndTable();
        }
        ImGui.EndChild();
    }

    int Rows(Item item, ref bool folded)
    {
        switch (item)
        {
            case Header { Details: true } h:
                folded = !openDetails.Contains((section, h.Text));
                return 1;
            case Header:
                folded = false;
                return 1;
            case NumRow or ChoiceRow or ToggleRow or PickRow:
                return folded ? 0 : 1;
            case Note t:
                return 1 + t.Text.Length / 90;
            default:
                return 1;
        }
    }

    void Column(List<Item> items, int from, int to)
    {
        var dl = ImGui.GetWindowDrawList();
        float w = ImGui.GetContentRegionAvail().X;
        float labelW = MathF.Round(Math.Clamp(w * 0.42f, P(140), P(220)));
        bool folded = false;
        int row = 0;
        // a column that starts mid-section (no heading of its own) still opens under the right Details state
        for (int n = from - 1; n >= 0; n--)
            if (items[n] is Header h0)
            {
                folded = h0.Details && !openDetails.Contains((section, h0.Text));
                break;
            }
        for (int n = from; n < to; n++)
        {
            if (folded && items[n] is NumRow or ChoiceRow or ToggleRow or PickRow)
                continue;
            ImGui.PushID(n);
            var p = ImGui.GetCursorScreenPos();
            switch (items[n])
            {
                case Header { Details: true } h:
                {
                    bool open = openDetails.Contains((section, h.Text));
                    if (n > from)
                        ImGui.Dummy(V(0, 3));
                    p = ImGui.GetCursorScreenPos();
                    float hh = P(18);
                    if (ImGui.InvisibleButton("##d", new Vector2(w, hh)) && !openDetails.Remove((section, h.Text)))
                        openDetails.Add((section, h.Text));
                    bool hover = ImGui.IsItemHovered();
                    dl.AddRectFilled(p, p + new Vector2(w, hh), T.U32(hover ? T.Panel2 : T.HeaderBg));
                    dl.AddLine(p + new Vector2(0, hh - 1), p + new Vector2(w, hh - 1), T.U32(T.Edge));
                    dl.Twisty(p + new Vector2(P(9), hh / 2), open, T.Muted);
                    dl.Text(UiFonts.SansSmall, p + new Vector2(P(18), (hh - UiFonts.SansSmall.FontSize) / 2), hover ? T.Text : T.Muted, h.Text);
                    folded = !open;
                    row = 0;
                    break;
                }
                case Header h:
                    folded = false;
                    if (n > from)
                        ImGui.Dummy(V(0, 3));
                    dl.HeadStrip(w, h.Upper, null);
                    if (h.Button != null)
                    {
                        var after = ImGui.GetCursorScreenPos();
                        ImGui.SetCursorScreenPos(after + new Vector2(w - P(64), -P(17)));
                        ImGui.PushFont(UiFonts.SansSmall);
                        if (ImGui.SmallButton(h.Button))
                            h.OnButton!();
                        ImGui.PopFont();
                        ImGui.SetCursorScreenPos(after);
                    }
                    row = 0;
                    break;
                case Note t:
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + P(6));
                    ImGui.PushFont(UiFonts.SansSmall);
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + w - P(12));
                    ImGui.TextColored(T.Muted, t.Text);
                    ImGui.PopTextWrapPos();
                    ImGui.PopFont();
                    break;
                case Act a:
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + P(6));
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + P(2));
                    if (ImGui.SmallButton(a.Label))
                        a.OnClick();
                    break;
                default:
                    dl.RowBg(p, w, RowH, row++);
                    ImGui.SetCursorScreenPos(p + new Vector2(P(6), (RowH - ImGui.GetFrameHeight()) / 2));
                    ItemRow(items[n], labelW, w);
                    ImGui.SetCursorScreenPos(p + new Vector2(0, RowH));
                    ImGui.Dummy(new Vector2(w, 0));
                    break;
            }
            ImGui.PopID();
        }
    }

    /// <summary>A knob's row: its label, then the control from <paramref name="labelW"/> on (SameLine's offset counts
    /// from the table column's start).</summary>
    void ItemRow(Item item, float labelW, float w)
    {
        switch (item)
        {
            case NumRow r:
                NumberRow(r, labelW);
                break;
            case ChoiceRow c:
                ImGui.BeginDisabled(!c.Applies);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(c.Knob.Label);
                ImGui.SameLine(labelW);
                ImGui.SetNextItemWidth(w - labelW - P(8));
                int idx = c.Index;
                if (ImGui.Combo("##c", ref idx, c.Labels, c.Labels.Length) && idx < c.Knob.Values.Length)
                    Edit(c.Knob.Set(doc.Current, c.Knob.Values[idx]), $"{c.Knob.Label}: {c.Labels[idx]}");
                ImGui.EndDisabled();
                break;
            case PickRow pr:
                ImGui.BeginDisabled(!pr.Applies);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(pr.Label);
                ImGui.SameLine(labelW);
                ImGui.SetNextItemWidth(w - labelW - P(8));
                if (ImGui.BeginCombo("##p", pr.Names[pr.Index], ImGuiComboFlags.HeightLarge))
                {
                    PickList(pr);
                    ImGui.EndCombo();
                }
                else if (ImGui.IsItemHovered() && pr.Index < pr.Count && pr.Tips != null)
                    Tip(pr.Tips[pr.Index], 460);
                ImGui.EndDisabled();
                break;
            case ToggleRow tr:
                ImGui.BeginDisabled(!tr.Applies);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(tr.Knob.Label);
                ImGui.SameLine(labelW);
                bool v = tr.Value;
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, V(1, 1));
                if (ImGui.Checkbox("##t", ref v))
                    Edit(tr.Knob.Set(doc.Current, v), $"{tr.Knob.Label} {(v ? "on" : "off")}");
                ImGui.PopStyleVar();
                ImGui.EndDisabled();
                break;
        }
    }

    /// <summary>A number knob as a spinner: ◂ value ▸ (the wheel over the value steps it, a click types it), then
    /// Auto or its button.</summary>
    void NumberRow(NumRow r, float labelW)
    {
        ImGui.BeginDisabled(!r.Applies);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(r.Label);
        ImGui.SameLine(labelW);
        Spinner(r, P(96), extras: true);
        ImGui.EndDisabled();
    }

    /// <summary>The spinner itself, <paramref name="valueW"/> wide between its arrows; with extras, Auto (or its
    /// button) and the change since opened after it.</summary>
    void Spinner(NumRow r, float valueW, bool extras)
    {
        // its own ID scope: several spinners share a table row, and their arrows, value and auto buttons would clash
        ImGui.PushID(r.Knob.Id);
        SpinnerIn(r, valueW, extras);
        ImGui.PopID();
    }

    void SpinnerIn(NumRow r, float valueW, bool extras)
    {
        var k = r.Knob;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        if (ImGui.ArrowButton("##m", ImGuiDir.Left))
            Step(r, ImGui.GetIO().KeyShift ? -4 : -1);
        ImGui.SameLine();
        if (editing == k.Id)
        {
            ImGui.SetNextItemWidth(valueW);
            if (editFocus)
            {
                ImGui.SetKeyboardFocusHere();
                editFocus = false;
            }
            if (ImGui.InputText("##v", ref editText, 32, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
            {
                if (Units.TryParse(editText, k.Quantity, units, out double metric))
                {
                    SetValue(k, metric);
                    doc.Seal();
                }
                editing = null;
            }
            else if (ImGui.IsItemDeactivated())
                editing = null;
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, T.Inset);
            ImGui.PushStyleColor(ImGuiCol.Text, r.IsAuto ? T.Text : T.Bright);
            ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(1, 0.5f));
            ImGui.PushFont(UiFonts.Mono);
            if (ImGui.Button(r.Value, new Vector2(valueW, 0)))
            {
                editing = k.Id;
                editText = r.Num is { } x ? Fmt(k, x, unit: false).Replace(",", "") : "";
                editFocus = true;
            }
            ImGui.PopFont();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY);   // the value takes the wheel, the panel doesn't scroll
                if (ImGui.GetIO().MouseWheel is var wheel && wheel != 0)
                    Step(r, Math.Sign(wheel) * (ImGui.GetIO().KeyShift ? 4 : 1));
            }
        }
        ImGui.SameLine();
        if (ImGui.ArrowButton("##p", ImGuiDir.Right))
            Step(r, ImGui.GetIO().KeyShift ? 4 : 1);
        ImGui.PopStyleVar();
        if (!extras)
            return;
        ImGui.SameLine(0, P(8));
        ImGui.PushFont(UiFonts.SansSmall);
        if (r.IsAuto && k.CanAuto)
            ImGui.TextColored(T.Muted, "auto");
        else if (k.CanAuto)
        {
            if (ImGui.SmallButton("auto"))
            {
                SetValue(k, null);
                doc.Seal();
            }
        }
        else
            ImGui.Dummy(Vector2.Zero);   // ends the line SameLine opened
        ImGui.PopFont();
        if (r.Delta.Length > 0)
        {
            ImGui.SameLine(0, P(10));
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TextColored(T.Brass, r.Delta);
            ImGui.PopFont();
        }
    }

    // ------------------------------------------------------------------ right: ledger, particulars, remarks

    void Ledger()
    {
        var s = snap!;
        var dl = ImGui.GetWindowDrawList();
        float w = ImGui.GetContentRegionAvail().X;
        float tX = w * 0.42f, pctX = w * 0.62f, barX = w * 0.70f, barW = w - barX - P(8);
        var p = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(p, p + new Vector2(w, RowH), T.U32(T.HeaderBg));
        dl.AddLine(p + new Vector2(0, RowH - 1), p + new Vector2(w, RowH - 1), T.U32(T.Edge));
        float hy = (RowH - UiFonts.SansSmall.FontSize) / 2;
        dl.Text(UiFonts.SansSmall, p + new Vector2(P(6), hy), T.Muted, "GROUP");
        dl.Text(UiFonts.SansSmall, p + new Vector2(tX - Width(UiFonts.SansSmall, "T"), hy), T.Muted, "T");
        dl.Text(UiFonts.SansSmall, p + new Vector2(pctX - Width(UiFonts.SansSmall, "%"), hy), T.Muted, "%");
        ImGui.Dummy(new Vector2(w, RowH));
        float my = (RowH - UiFonts.Mono.FontSize) / 2, sy = (RowH - UiFonts.Sans.FontSize) / 2;
        for (int i = 0; i < s.Ledger.Length; i++)
        {
            var g = s.Ledger[i];
            p = ImGui.GetCursorScreenPos();
            ImGui.PushID(i);
            ImGui.InvisibleButton("##g", new Vector2(w, RowH));
            bool hover = ImGui.IsItemHovered();
            ImGui.PopID();
            dl.RowBg(p, w, RowH, i);
            if (hover)
            {
                dl.AddRectFilled(p, p + new Vector2(w, RowH), T.U32(T.Brass with { W = 0.08f }));
                ImGui.BeginTooltip();
                ImGui.PushFont(UiFonts.SansBold);
                ImGui.TextColored(T.Brass, g.Name);
                ImGui.PopFont();
                if (ImGui.BeginTable("##items", 2, ImGuiTableFlags.SizingFixedFit))
                {
                    foreach (var (name, t) in g.Tip)
                    {
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(name);
                        ImGui.TableNextColumn();
                        RightInCell(UiFonts.Mono, t, T.Bright);
                    }
                    ImGui.EndTable();
                }
                ImGui.EndTooltip();
            }
            dl.AddRectFilled(p + new Vector2(P(6), RowH / 2 - P(4)), p + new Vector2(P(14), RowH / 2 + P(4)), T.U32(T.Group(g.Group)));
            dl.Text(UiFonts.Sans, p + new Vector2(P(19), sy), T.Text, g.Name);
            dl.Text(UiFonts.Mono, p + new Vector2(tX - Width(UiFonts.Mono, g.T), my), T.Bright, g.T);
            dl.Text(UiFonts.Mono, p + new Vector2(pctX - Width(UiFonts.Mono, g.Pct), my), T.Muted, g.Pct);
            dl.Bar(p + new Vector2(barX, RowH / 2 - P(3)), new Vector2(barW, P(6)), g.Frac * 2, T.Group(g.Group));
        }
        p = ImGui.GetCursorScreenPos();
        dl.AddLine(p, p + new Vector2(w, 0), T.U32(T.TitleTop));
        dl.Text(UiFonts.SansBold, p + new Vector2(P(6), (RowH - UiFonts.SansBold.FontSize) / 2), T.Bright, "Standard");
        dl.Text(UiFonts.Mono, p + new Vector2(tX - Width(UiFonts.Mono, s.Std), my), T.Bright, s.Std);
        ImGui.Dummy(new Vector2(w, RowH));
        p = ImGui.GetCursorScreenPos();
        dl.Text(UiFonts.Sans, p + new Vector2(P(6), sy), T.Muted, "Full load");
        dl.Text(UiFonts.Mono, p + new Vector2(tX - Width(UiFonts.Mono, s.FullT), my), T.Text, s.FullT);
        ImGui.Dummy(new Vector2(w, RowH));
    }

    void Particulars()
    {
        var s = snap!;
        var dl = ImGui.GetWindowDrawList();
        float w = ImGui.GetContentRegionAvail().X, valueX = P(92);
        for (int i = 0; i < s.Particulars.Length; i++)
        {
            var (label, value) = s.Particulars[i];
            var p = ImGui.GetCursorScreenPos();
            float y0 = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(valueX, y0 + P(2)));
            ImGui.PushTextWrapPos(w - P(6));
            ImGui.TextColored(T.Bright, value);
            ImGui.PopTextWrapPos();
            float h = Math.Max(RowH, ImGui.GetCursorPosY() - y0);
            dl.Text(UiFonts.Sans, p + new Vector2(P(6), P(2)), T.Muted, label);
            dl.AddLine(p + new Vector2(0, h - 1), p + new Vector2(w, h - 1), T.U32(T.Line));
            ImGui.SetCursorPos(new Vector2(0, y0 + h));
        }
        ImGui.Dummy(Vector2.Zero);
    }

    void Remarks()
    {
        var s = snap!;
        var dl = ImGui.GetWindowDrawList();
        float w = ImGui.GetContentRegionAvail().X;
        if (s.Remarks.Length == 0)
        {
            ImGui.SetCursorPos(V(8, 4));
            ImGui.TextColored(T.Muted, "None.");
        }
        foreach (var (error, text) in s.Remarks)
        {
            var p = ImGui.GetCursorScreenPos();
            float y0 = ImGui.GetCursorPosY();
            dl.Text(UiFonts.SansBold, p + V(7, 3), error ? T.Amber : T.Brass, error ? "!" : "•");
            ImGui.SetCursorPos(new Vector2(P(20), y0 + P(3)));
            ImGui.PushTextWrapPos(w - P(6));
            if (error)
                ImGui.TextColored(T.Amber, text);
            else
                ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            float h = ImGui.GetCursorPosY() - y0 + P(2);
            dl.AddLine(p + new Vector2(0, h - 1), p + new Vector2(w, h - 1), T.U32(T.Line));
            ImGui.SetCursorPos(new Vector2(0, y0 + h));
        }
        ImGui.Dummy(Vector2.Zero);
    }

    // ------------------------------------------------------------------ status bar

    const string Hints = "0–7 tabs · Ctrl+Z undo · Ctrl+S save · wheel over a value steps it (Shift ×4) · click it to type";
    const string Separator = "›";

    void StatusBar(Vector2 pos, float width)
    {
        var s = snap!;
        ImGui.SetCursorPos(pos);
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        dl.StatusBarBg(p, width, StatusH);
        ImGui.PushFont(UiFonts.SansSmall);
        float ty = (StatusH - UiFonts.SansSmall.FontSize) / 2;
        bool busy = worker.Busy, failed = latest is { Ship: null };
        var dot = p + new Vector2(P(10), StatusH / 2);
        if (busy)
            dl.Busy(dot, P(4));
        else
            dl.AddCircleFilled(dot, P(3.5f), T.U32(failed ? T.Amber : T.Ok));
        string st = busy ? s.Settling : s.Status;
        dl.Text(UiFonts.SansSmall, p + new Vector2(P(20), ty), busy ? T.Brass : T.Muted, st);
        float x = P(20) + ImGui.CalcTextSize(st).X + P(18);

        // the history: the newest entries that fit before the hints, oldest of them first; a click goes back to one
        float hintsX = width - P(10) - ImGui.CalcTextSize(Hints).X;
        dl.Text(UiFonts.SansSmall, p + new Vector2(x, ty), T.Muted, "history:");
        x += ImGui.CalcTextSize("history:").X + P(6);
        float sepW = ImGui.CalcTextSize(Separator).X + P(10);
        int first = s.HistoryLabels.Length;
        for (float used = 0; first > 0; first--)
        {
            float lw = ImGui.CalcTextSize(s.HistoryLabels[first - 1]).X + sepW;
            if (x + used + lw > hintsX - P(16))
                break;
            used += lw;
        }
        for (int i = first; i < s.HistoryLabels.Length; i++)
        {
            if (i > first)
            {
                dl.Text(UiFonts.SansSmall, p + new Vector2(x + P(3), ty), T.Muted, Separator);
                x += sepW;
            }
            ImGui.SetCursorPos(pos + new Vector2(x, 0));
            ImGui.PushID(i);
            bool current = i == doc.Index;
            var size = new Vector2(ImGui.CalcTextSize(s.HistoryLabels[i]).X, StatusH);
            if (ImGui.InvisibleButton("##h", size))
            {
                doc.Jump(i);
                stale = true;
            }
            bool hover = ImGui.IsItemHovered();
            ImGui.PopID();
            dl.Text(UiFonts.SansSmall, p + new Vector2(x, ty), current ? T.Brass : hover ? T.Bright : i > doc.Index ? T.Muted * new Vector4(1, 1, 1, 0.5f) : T.Text,
                s.HistoryLabels[i]);
            x += size.X;
        }
        dl.Text(UiFonts.SansSmall, p + new Vector2(hintsX, ty), T.Muted, Hints);
        ImGui.PopFont();
    }
}
