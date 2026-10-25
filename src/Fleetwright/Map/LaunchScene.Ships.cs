using System.Numerics;
using Fleetwright.Sim.Missions;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The launch screen's ships (docs/map-ui/launch.md L3, the mockup's first column): each side's order of battle
/// as a tab (yours first, then the enemy's: decision 3, the enemy's can be changed too), its units and under them a row
/// a design: drawn to scale, how many (− n +: the last first, a struck one back first; L3b) and their names, the
/// class's figures, its build, and Change… for the design picker. A row whose design the setup changed says what it was.
/// A unit's ✕ takes it off (struck, its undo in its place); one the setup added is brass, and moved or taken away again.
/// Add a unit… at the foot. The lines are made when the pick changes; a frame allocates nothing (the names are cut to
/// the column once per width).</summary>
public sealed unsafe partial class LaunchScene
{
    /// <summary>A unit's heading or a row of ships of one design.</summary>
    sealed class ShipLine
    {
        public bool IsUnit, Top;
        public int Depth, Side;
        public string Text = "", Right = "";   // a unit's name and its count
        public bool Removed, Added, Movable;   // a unit: taken off; added by the setup (on its own: it can be moved)
        // a row
        public ScenarioUnit Unit = null!;
        public ScenarioShips Ships = null!;
        public string Design = "", Count = "", Names = "", Struck = "", Was = "", Type = "";
        public string[] NameList = [];
        public bool Swapped, Fewer;   // Fewer: − takes one off (not past an added unit's last)
        public string NamesShown = "", StruckShown = "";
        public float NamesWidth = -1;
    }

    readonly List<(int Side, string Label, string Ensign)> shipTabs = [];
    List<ShipLine>[] linesBySide = [];
    int shipTab;
    bool shipsMadeWithDesigns;   // the titles were the designs' (else their files', until those are read)
    ScenarioSetup? pendingEdit;   // a change made while the column is drawn, made at the frame's end

    ShipLine? renaming;   // the unit whose name is being typed
    string renameText = "";
    bool renameFocus;
    (int Side, string Joins)? pendingAdd;   // a "+ division" clicked: the add picker, joining that unit, at the frame's end

    /// <summary>Every unit's name a new one mustn't take: as shown, and as written for one renamed.</summary>
    HashSet<string> TakenNames()
    {
        var names = new HashSet<string>(setup.Changes.OfType<RenameUnit>().Select(c => c.From));
        void Walk(ScenarioUnit u)
        {
            names.Add(u.Name);
            foreach (var x in u.Units)
                Walk(x);
        }
        foreach (var u in current?.Units ?? [])
            Walk(u);
        return names;
    }

    void MadeShips(Scenario s)
    {
        shipsMadeWithDesigns = DesignsRead;
        renaming = null;
        int tabSide = shipTab < shipTabs.Count ? shipTabs[shipTab].Side : -1;
        shipTabs.Clear();
        var order = Enumerable.Range(0, s.Sides.Length).Where(sd => s.Units.Any(u => u.Side == sd)).ToList();
        if (side >= 0 && order.Remove(side))
            order.Insert(0, side);
        foreach (int sd in order)
            shipTabs.Add((sd, side < 0 ? s.Sides[sd] : sd == side ? $"{s.Sides[sd]} · yours" : $"{s.Sides[sd]} · the enemy", s.AboutOf(sd)?.Ensign ?? ""));
        shipTab = Math.Max(0, shipTabs.FindIndex(t => t.Side == tabSide));
        if (side >= 0 && tabSide < 0)
            shipTab = 0;
        linesBySide = new List<ShipLine>[s.Sides.Length];
        for (int sd = 0; sd < s.Sides.Length; sd++)
        {
            var lines = linesBySide[sd] = [];
            foreach (var u in s.Units.Where(u => u.Side == sd))
                Lines(u, 0, [], null, lines);
        }

        void Lines(ScenarioUnit u, int depth, List<string> above, AddUnit? addedIn, List<ShipLine> lines)
        {
            var add = addedIn ?? setup.AddedHolding(u.Name);
            lines.Add(new ShipLine
            {
                IsUnit = true, Top = depth == 0, Depth = depth, Side = u.Side, Text = u.Name, Unit = u, Removed = u.Removed,
                Added = add?.Unit.Name == u.Name, Movable = add?.Unit.Name == u.Name && add.Joins == null,
                Right = depth == 0 || add?.Unit.Name == u.Name ? $"{u.ShipCount} ship{(u.ShipCount == 1 ? "" : "s")}" : "",
            });
            if (u.Removed)
                return;   // its ships under its struck name: none
            var chain = new List<string>(above) { setup.NameAsMade(u.Name) };
            foreach (var sub in u.Units)
                Lines(sub, depth + 1, chain, add, lines);
            foreach (var ships in u.Ships)
            {
                int unnamed = ships.Count - Math.Min(ships.Names.Length, ships.Count);
                var row = new ShipLine
                {
                    Depth = depth, Side = u.Side, Unit = u, Ships = ships, Design = ships.Design, Text = TitleOf(ships.Design),
                    Count = ships.Count.ToString(Inv), NameList = ships.Names, Type = InfoOf(ships.Design)?.Type ?? "",
                    Names = ships.Names.Length > 0 ? string.Join(", ", ships.Names) + (unnamed > 0 ? $" and {unnamed} more" : "")
                        : ships.Count > 0 ? $"{ships.Count} unnamed" : "",
                    Struck = Struck(ships.Struck),
                    Fewer = add == null ? ships.Count > 0 : u.ShipCount > 1,
                };
                // the change that brought this design here, if one did
                if (setup.Changes.OfType<SwapDesign>().LastOrDefault(c => c.To == ships.Design &&
                        (c.Ship != null ? Array.IndexOf(ships.Names, c.Ship) >= 0 : c.Unit == null || chain.Contains(c.Unit))) is { } was)
                {
                    row.Swapped = true;
                    row.Was = $"was {TitleOf(was.From)} · " + (was.Ship != null ? "only her" : was.Unit != null ? (ships.Count == 1 ? "her" : $"all {ships.Count}") : "every one in the scenario");
                }
                lines.Add(row);
            }
        }
    }

    /// <summary>The struck ships in a line: their names, and how many unnamed.</summary>
    static string Struck(string[] struck)
    {
        if (struck.Length == 0)
            return "";
        var named = struck.Where(n => n.Length > 0).ToList();
        int unnamed = struck.Length - named.Count;
        if (unnamed > 0)
            named.Add($"{unnamed} unnamed");
        return string.Join(", ", named);
    }

    /// <summary>The ships column: the sides' tabs, then the units and their rows, scrolling, and Add a unit….</summary>
    void ShipsColumn(Vector2 at, float w, float bottom)
    {
        if (current is not { } s || shipTabs.Count == 0)
            return;
        if (!shipsMadeWithDesigns && DesignsRead)
            MadeShips(s);
        // the tabs
        float tx = at.X;
        for (int i = 0; i < shipTabs.Count; i++)
        {
            var (sd, label, ensign) = shipTabs[i];
            float tw = Width(UiFonts.Sans, 12.5f, label) + 46 * sc;
            var a = new Vector2(tx, at.Y);
            ImGui.SetCursorScreenPos(a);
            ImGui.PushID(i);
            if (ImGui.InvisibleButton("##stab", new Vector2(tw, 26 * sc)))
                shipTab = i;
            ImGui.PopID();
            bool on = i == shipTab, hover = ImGui.IsItemHovered();
            Box(a, a + new Vector2(tw, 26 * sc), on ? Brass with { W = 0.10f } : new Vector4(1, 1, 1, hover ? 0.05f : 0.02f), on ? BrassDark : EdgeC, 2);
            Ensign(ensign, a + new Vector2(10 * sc, 7 * sc), new Vector2(20 * sc, 12 * sc));
            Txt(UiFonts.Sans, 12.5f, a + new Vector2(38 * sc, 5 * sc), on ? Bright : Soft, label);
            tx += tw + 4 * sc;
        }
        const string Hint = "Change a design: it builds while you set up";
        if (tx + Width(UiFonts.SansSmall, 11, Hint) + 12 * sc < at.X + w)
            Txt(UiFonts.SansSmall, 11, new Vector2(at.X + w - Width(UiFonts.SansSmall, 11, Hint), at.Y + 7 * sc), Muted, Hint);
        int tabSide = shipTabs[Math.Min(shipTab, shipTabs.Count - 1)].Side;
        var lines = linesBySide[tabSide];
        // to scale: the longest class of either side fits the thumbnail's room
        float longest = 0;
        foreach (var l in linesBySide)
            foreach (var r in l)
                if (!r.IsUnit && ClassOf(r.Design) is int id and >= 0 && world.Classes.TryGet(id) is { } c)
                    longest = Math.Max(longest, (float)c.Motion.LengthM);
        float pxPerM = longest > 0 ? Math.Min(1.12f * sc, ThumbW * sc / longest) : 1.12f * sc;
        bool ring = side < 0 ? tabSide == 0 : tabSide == side;
        var colour = ring ? Friend : Hostile;

        float top = at.Y + 36 * sc;
        ImGui.SetCursorScreenPos(new Vector2(at.X, top));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        if (ImGui.BeginChild($"##ships{tabSide}", new Vector2(w + 10 * sc, bottom - top - 8 * sc), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            var keep = dl;
            dl = ImGui.GetWindowDrawList();
            var o = ImGui.GetCursorScreenPos();
            float y = o.Y, inner = w - 4 * sc;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (l.IsUnit)
                    y += UnitHead(i, l, new Vector2(o.X, y), inner, ring, colour);
                else
                    y += ShipRow(i, l, new Vector2(o.X + (l.Depth + 1) * 14 * sc, y), inner - (l.Depth + 1) * 14 * sc, pxPerM) + 8 * sc;
            }
            // add a unit
            var aa = new Vector2(o.X + 14 * sc, y + 4 * sc);
            var ab = new Vector2(o.X + inner, aa.Y + 30 * sc);
            ImGui.SetCursorScreenPos(aa);
            if (ImGui.InvisibleButton("##addunit", ab - aa) && !begin)
                OpenAdd(tabSide);
            if (ImGui.IsItemHovered())
                dl.AddRectFilled(aa, ab, U(Brass with { W = 0.06f }), 3 * sc);
            uint dash = U(Hex(0x4A5662));
            Dashed(aa, new Vector2(ab.X, aa.Y), dash);
            Dashed(new Vector2(aa.X, ab.Y), ab, dash);
            Dashed(aa, new Vector2(aa.X, ab.Y), dash);
            Dashed(new Vector2(ab.X, aa.Y), ab, dash);
            Txt(UiFonts.Sans, 12.5f, aa + new Vector2(12 * sc, 7 * sc), Brass, AddLabel);
            Txt(UiFonts.SansSmall, 11.5f, aa + new Vector2(20 * sc + Width(UiFonts.Sans, 12.5f, AddLabel), 8 * sc), Muted, AddNote);
            y += 40 * sc;
            ImGui.SetCursorScreenPos(new Vector2(o.X, y));
            ImGui.Dummy(new Vector2(1, 4 * sc));
            dl = keep;
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    const string AddLabel = "+ Add a unit…", AddNote = "any designs, placed on the opening's map or joined to a force";
    const float ThumbW = 176;   // the room for a row's ship, reference px

    /// <summary>A unit's heading: its name (struck when it's taken off, brass when the setup added it), its count, and its
    /// ✕ (or its undo); an added unit on its own can be moved. Its height.</summary>
    float UnitHead(int i, ShipLine l, Vector2 at, float w, bool ring, Vector4 colour)
    {
        float ux = at.X + l.Depth * 16 * sc, y = at.Y;
        if (l.Top)
        {
            Mark(new Vector2(at.X + 4 * sc, y + 10 * sc), ring, colour, 3.5f, 1);
            ux += 16 * sc;
        }
        float px = l.Top ? 13 : 12.5f;
        float right = at.X + w;
        ImGui.PushID(i);
        // its name: a click renames it
        if (renaming == l)
        {
            PushFields();
            ImGui.SetCursorScreenPos(new Vector2(ux - 6 * sc, y - 1 * sc));
            ImGui.SetNextItemWidth(Math.Min(300 * sc, right - ux - 140 * sc));
            if (renameFocus)
            {
                ImGui.SetKeyboardFocusHere();
                renameFocus = false;
            }
            bool done = ImGui.InputText("##rename", ref renameText, 64, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
            bool left = !done && ImGui.IsItemDeactivated();
            PopFields();
            if (done)
            {
                string to = renameText.Trim();
                if (to.Length > 0 && to != l.Text && !TakenNames().Contains(to))
                    pendingEdit = setup.Rename(l.Text, to);
                renaming = null;
            }
            else if (left)
                renaming = null;
        }
        else
        {
            Txt(UiFonts.Sans, px, new Vector2(ux, y + 2 * sc), l.Removed ? Muted : l.Added ? Brass : Bright, l.Text);
            if (!l.Removed)
            {
                float nw = Width(UiFonts.Sans, px, l.Text);
                ImGui.SetCursorScreenPos(new Vector2(ux, y));
                if (ImGui.InvisibleButton("##name", new Vector2(nw, 20 * sc)) && !begin)
                    (renaming, renameText, renameFocus) = (l, l.Text, true);
                if (ImGui.IsItemHovered())
                {
                    dl.AddLine(new Vector2(ux, y + 18 * sc), new Vector2(ux + nw, y + 18 * sc), U(BrassDark));
                    ImGui.SetTooltip("click to rename");
                }
            }
        }
        if (l.Removed)
        {
            float nw = Width(UiFonts.Sans, px, l.Text);
            dl.AddLine(new Vector2(ux, y + 10 * sc), new Vector2(ux + nw, y + 10 * sc), U(Muted));
            const string Undo = "removed · undo";
            float uw = Width(UiFonts.SansSmall, 11.5f, Undo);
            ImGui.SetCursorScreenPos(new Vector2(right - uw, y + 2 * sc));
            if (ImGui.InvisibleButton("##undo", new Vector2(uw, 18 * sc)) && !begin)
                pendingEdit = setup.Without(new RemoveUnit(setup.NameAsMade(l.Text)).Key);
            Txt(UiFonts.SansSmall, 11.5f, new Vector2(right - uw, y + 3 * sc), ImGui.IsItemHovered() ? Hex(0xE6C77A) : Brass, Undo);
        }
        else
        {
            // ✕: taken off (an added one: taken away)
            var xc = new Vector2(right - 7 * sc, y + 10 * sc);
            ImGui.SetCursorScreenPos(xc - new Vector2(8, 9) * sc);
            if (ImGui.InvisibleButton("##rm", new Vector2(16, 18) * sc) && !begin)
                pendingEdit = l.Added ? setup.WithoutAdded(l.Text) : setup.With(new RemoveUnit(setup.NameAsMade(l.Text)));
            bool hover = ImGui.IsItemHovered();
            uint xcol = U(hover ? Hostile : Hex(0x56626C));
            dl.AddLine(xc - new Vector2(3.5f, 3.5f) * sc, xc + new Vector2(3.5f, 3.5f) * sc, xcol, 1.2f * sc);
            dl.AddLine(xc + new Vector2(-3.5f, 3.5f) * sc, xc + new Vector2(3.5f, -3.5f) * sc, xcol, 1.2f * sc);
            if (hover)
                ImGui.SetTooltip(l.Added ? "take the added unit away" : "remove the unit (its undo stays here)");
            float rx = right - 22 * sc;
            if (l.Right.Length > 0)
            {
                rx -= Width(UiFonts.SansSmall, 11.5f, l.Right);
                Txt(UiFonts.SansSmall, 11.5f, new Vector2(rx, y + 3 * sc), Muted, l.Right);
                rx -= 10 * sc;
            }
            // a division more, joining it
            const string More = "+ division";
            float dw = Width(UiFonts.SansSmall, 11.5f, More);
            rx -= dw;
            ImGui.SetCursorScreenPos(new Vector2(rx, y + 2 * sc));
            if (ImGui.InvisibleButton("##division", new Vector2(dw, 18 * sc)) && !begin)
                pendingAdd = (l.Side, l.Text);
            Txt(UiFonts.SansSmall, 11.5f, new Vector2(rx, y + 3 * sc), ImGui.IsItemHovered() ? Hex(0xE6C77A) : Muted, More);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"add a unit to {l.Text}, on its station there");
            rx -= 10 * sc;
            if (l.Movable)
            {
                const string Move = "move";
                float mw = Width(UiFonts.SansSmall, 11.5f, Move);
                rx -= mw;
                ImGui.SetCursorScreenPos(new Vector2(rx, y + 2 * sc));
                if (ImGui.InvisibleButton("##move", new Vector2(mw, 18 * sc)) && !begin && setup.AddedHolding(l.Text) is { } a)
                    StartPlacing(a.Unit, a.Unit.CourseDeg, a.Unit.Kn);
                Txt(UiFonts.SansSmall, 11.5f, new Vector2(rx, y + 3 * sc), ImGui.IsItemHovered() ? Hex(0xE6C77A) : Brass, Move);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("place it again on the opening's map");
                rx -= 10 * sc;
            }
            if (l.Added)
            {
                bool now = placing?.Unit.Name == l.Text;
                string chip = now ? "PLACING…" : "ADDED";
                rx -= TrackedWidth(UiFonts.SansBold, 10, chip, 0.5f) + 10 * sc;
                Chip(new Vector2(rx, y + 3 * sc), chip, now ? Hex(0x3A2A12) : Hex(0x1E3320), now ? Amber : Ok);
            }
        }
        ImGui.PopID();
        return 24 * sc;
    }

    /// <summary>A design's class id in the pick, or -1.</summary>
    int ClassOf(string design) => world.LoadIds is { } ids && ids.TryGetValue(design, out int id) ? id : -1;

    /// <summary>A row: the ship to scale, how many and their names, the figures, the build, Change…; its height.</summary>
    float ShipRow(int i, ShipLine l, Vector2 at, float w, float pxPerM)
    {
        ImGui.PushID(i);
        float h = Row(l, at, w, pxPerM);
        ImGui.PopID();
        return h;
    }

    float Row(ShipLine l, Vector2 at, float w, float pxPerM)
    {
        bool open = pickerRow == l;
        float h = (l.Swapped ? 94 : 78) * sc;
        Box(at, at + new Vector2(w, h), open ? Brass with { W = 0.06f } : new Vector4(1, 1, 1, 0.015f), open || l.Swapped ? BrassDark : LineC, 3);
        if (l.Swapped)
            dl.AddRectFilled(at, at + new Vector2(3 * sc, h), U(Brass));
        int id = ClassOf(l.Design);
        Thumb(id, at + new Vector2(10 * sc + ThumbW * sc / 2, 30 * sc), pxPerM, l.Ships.Count > 0 ? 1 : 0.35f);
        // − n +, the design, the names (the struck after them)
        float ix = at.X + (20 + ThumbW) * sc, iy = at.Y + 10 * sc;
        float cw = Stepper(new Vector2(ix, iy), l.Count, l.Fewer && !begin, !begin, out int by);
        if (by != 0)
            pendingEdit = setup.Resize(setup.NameAsMade(l.Unit.Name), l.Design, by);
        Txt(UiFonts.Sans, 13, new Vector2(ix + cw + 8 * sc, iy), Bright, l.Text);
        float namesW = at.X + w - 92 * sc - ix;
        if (Math.Abs(namesW - l.NamesWidth) > 0.5f)
        {
            l.NamesWidth = namesW;
            l.NamesShown = Clip(UiFonts.SansSmall, 11, l.Names, namesW);
            float left = namesW - Width(UiFonts.SansSmall, 11, l.NamesShown + (l.Names.Length > 0 ? ", " : ""));
            l.StruckShown = l.Struck.Length == 0 || left < 30 * sc ? "" : Clip(UiFonts.SansSmall, 11, l.Struck, left);
        }
        Txt(UiFonts.SansSmall, 11, new Vector2(ix, iy + 22 * sc), Muted, l.NamesShown);
        if (l.StruckShown.Length > 0)
        {
            float sx = ix + (l.Names.Length > 0 ? Width(UiFonts.SansSmall, 11, l.NamesShown + ", ") : 0);
            var dim = Hex(0x56626C);
            if (l.Names.Length > 0)
                Txt(UiFonts.SansSmall, 11, new Vector2(sx - Width(UiFonts.SansSmall, 11, ", "), iy + 22 * sc), Muted, ",");
            Txt(UiFonts.SansSmall, 11, new Vector2(sx, iy + 22 * sc), dim, l.StruckShown);
            dl.AddLine(new Vector2(sx, iy + 29.5f * sc), new Vector2(sx + Width(UiFonts.SansSmall, 11, l.StruckShown), iy + 29.5f * sc), U(dim));
        }
        ImGui.SetCursorScreenPos(new Vector2(ix, iy + 20 * sc));
        ImGui.InvisibleButton("##names", new Vector2(namesW, 16 * sc));
        if (ImGui.IsItemHovered() && (!ReferenceEquals(l.NamesShown, l.Names) || l.Struck.Length > 0 && !ReferenceEquals(l.StruckShown, l.Struck)))
            ImGui.SetTooltip(l.Struck.Length > 0 ? $"{l.Names}\ntaken off: {l.Struck}" : l.Names);
        // the figures, once compiled; what it was
        float fy = at.Y + (l.Swapped ? 58 : 56) * sc;
        if (FiguresOf(id) is { } f)
            Txt(UiFonts.Mono, 12, new Vector2(at.X + 10 * sc, fy), Text, f.Line);
        else if (id >= 0 && world.Classes.IsReady(id) && world.Classes.ErrorOf(id) is { } err)
            Txt(UiFonts.SansSmall, 11.5f, new Vector2(at.X + 10 * sc, fy), Hostile, Clip(UiFonts.SansSmall, 11.5f, err, w - 20 * sc));
        if (l.Swapped)
            Txt(UiFonts.SansSmall, 11, new Vector2(at.X + 10 * sc, at.Y + 76 * sc), Brass, l.Was);
        // Change…, and the build under it
        if (Button("##change", "Change…", new Vector2(at.X + w - 72 * sc, at.Y + 10 * sc), new Vector2(62 * sc, 22 * sc), px: 11.5f) && !begin)
            OpenPicker(l);
        BuildState(id, new Vector2(at.X + w - 66 * sc, at.Y + 38 * sc));
        return h;
    }

    /// <summary>A − n + (the mockup's stepper): its width; <paramref name="by"/> the click's change, else 0.</summary>
    float Stepper(Vector2 at, string n, bool fewer, bool more, out int by)
    {
        by = 0;
        float bw = 16 * sc, nw = Width(UiFonts.Mono, 12, n) + 8 * sc, h = 18 * sc, w = 2 * bw + nw;
        Box(at, at + new Vector2(w, h), new Vector4(0, 0, 0, 0.25f), EdgeC, 2);
        Txt(UiFonts.Mono, 12, at + new Vector2(bw + 4 * sc, 1.5f * sc), Brass, n);
        for (int k = 0; k < 2; k++)
        {
            var a = at + new Vector2(k == 0 ? 0 : bw + nw, 0);
            bool on = k == 0 ? fewer : more;
            ImGui.SetCursorScreenPos(a);
            if (ImGui.InvisibleButton(k == 0 ? "##fewer" : "##more", new Vector2(bw, h)) && on)
                by = k == 0 ? -1 : 1;
            bool hover = on && ImGui.IsItemHovered();
            if (hover)
                dl.AddRectFilled(a, a + new Vector2(bw, h), U(new Vector4(1, 1, 1, 0.08f)));
            uint col = U(!on ? Hex(0x3B4752) : hover ? Bright : Muted);
            var c = a + new Vector2(bw, h) / 2;
            dl.AddLine(c - new Vector2(3.5f * sc, 0), c + new Vector2(3.5f * sc, 0), col, 1.2f * sc);
            if (k == 1)
                dl.AddLine(c - new Vector2(0, 3.5f * sc), c + new Vector2(0, 3.5f * sc), col, 1.2f * sc);
        }
        return w;
    }

    /// <summary>A class's build in a word: building, its art coming, ready, or failed.</summary>
    void BuildState(int id, Vector2 at)
    {
        if (id < 0)
            return;
        var (word, col) = !world.Classes.IsReady(id) ? ("building", Muted)
            : world.Classes.ErrorOf(id) != null ? ("failed", Hostile)
            : art.Get(id) == null ? ("art…", Amber) : ("ready", Ok);
        dl.AddCircleFilled(at + new Vector2(3 * sc, 7.5f * sc), 3 * sc, U(col), 10);
        Txt(UiFonts.SansSmall, 11, at + new Vector2(10 * sc, 0), Muted, word);
    }

    /// <summary>Text cut to a width with an ellipsis (the string itself when it fits).</summary>
    static string Clip(ImFontPtr font, float px, string s, float width)
    {
        if (Width(font, px, s) <= width)
            return s;
        int n = s.Length;
        while (n > 0 && Width(font, px, s[..n] + "…") > width)
            n--;
        return s[..n].TrimEnd(' ', ',') + "…";
    }
}
