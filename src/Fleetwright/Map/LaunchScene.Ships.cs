using System.Numerics;
using Fleetwright.Sim.Missions;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The launch screen's order of battle (docs/map-ui/launch.md L3, tab 1 of the mockup launch-bureau.html): the
/// side shown (yours first, then the enemy's: decision 3, the enemy's can be changed too) as an outliner tree in a table,
/// a unit a group row (its admiral, his temper's chip, its ships and tonnage, + division, ✕), under it a row a design
/// (drawn to scale, ◀ n ▶ for its ships: the last first, a struck one back first; L3b; the class's figures, its build,
/// ▾ for the design picker) and under that its ships by name (a captain's temper marked ▲ ▼, the struck dim). Clicking a
/// design's row selects it: the selected class pane beside the table shows her drawn large, her particulars, the design's
/// buttons and her captains' tempers. A unit's ✕ takes it off (struck, its undo in its place); one the setup added is
/// brass-chipped, and moved or taken away again. + Add a unit… at the foot. The lines are made when the pick changes; a
/// frame allocates nothing.</summary>
public sealed unsafe partial class LaunchScene
{
    /// <summary>A unit's group row or a row of ships of one design.</summary>
    sealed class ShipLine
    {
        public bool IsUnit, Top;
        public int Depth, Side;
        public string Text = "", Right = "";   // a unit's name and its count
        public string Admiral = "", Chip = "";   // a unit's admiral, and his temper when he has one of his own (L4)
        public bool Bold;
        public bool Removed, Added, Movable;   // a unit: taken off; added by the setup (on its own: it can be moved)
        public string Tons = "";
        public int TonsKnown = -1;   // the classes figured when Tons was made
        // a row
        public ScenarioUnit Unit = null!;
        public ScenarioShips Ships = null!;
        public string Design = "", Count = "", Was = "", Type = "", Unnamed = "";
        public string[] NameList = [], StruckList = [];
        public bool Swapped, Fewer;   // Fewer: ◀ takes one off (not past an added unit's last)
        public Captain[] Captains = [];   // the live named ships, each with her captain's temper
        public string NamesTip = "";
    }

    sealed record Captain(string Name, double Temper, double Written, string Mark, string Value);

    readonly List<(int Side, string Label, string Ensign)> shipTabs = [];
    string[] sideLabels = [];
    List<ShipLine>[] linesBySide = [];
    int shipTab;
    int tabsPlayedAs = -2;   // the player's side the tabs were made for: a new one shows its ships
    bool shipsMadeWithDesigns;   // the titles were the designs' (else their files', until those are read)
    ScenarioSetup? pendingEdit;   // a change made while the column is drawn, made at the frame's end

    ShipLine? renaming;   // the unit whose name is being typed
    string renameText = "";
    bool renameFocus;
    (int Side, string? Joins)? pendingAdd;   // a "+ division" clicked: the add picker, joining that unit, at the frame's end

    (int Side, string Unit, string Design)? selKey;   // the selected design's row
    readonly HashSet<string> shutUnits = [];   // units folded shut, by name
    readonly HashSet<(int, string, string)> namesToggled = [];   // rows whose ships' names show other than by default
    const int SmallShips = 30;   // a side up to this many ships shows its ships' names at first

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
        sideLabels = [.. shipTabs.Select(t => t.Label)];
        shipTab = Math.Max(0, shipTabs.FindIndex(t => t.Side == tabSide));
        if (side >= 0 && (tabSide < 0 || side != tabsPlayedAs))
            shipTab = 0;   // the side played as, first
        tabsPlayedAs = side;
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
                Right = u.ShipCount.ToString(Inv),
                Admiral = u.Admiral,
                Chip = u.OwnTemper && u.Temper is double t && Math.Abs(t) > 1e-9 ? $"{(t > 0 ? "bold" : "cautious")} {DoctrineRules.Signed(t)}" : "",
                Bold = u.Temper > 0,
            });
            if (u.Removed)
                return;   // its ships under its struck name: none
            var chain = new List<string>(above) { setup.NameAsMade(u.Name) };
            foreach (var sub in u.Units)
                Lines(sub, depth + 1, chain, add, lines);
            foreach (var ships in u.Ships)
            {
                int live = Math.Min(ships.Names.Length, ships.Count), unnamed = ships.Count - live;
                var captains = new List<Captain>();
                for (int k = 0; k < live; k++)
                {
                    string name = ships.Names[k];
                    if (name.Length == 0)
                        continue;
                    double ct = ships.TemperOf(k), wt = SetTemper.CaptainOf(written!.Units, name) ?? 0;
                    captains.Add(new Captain(name, ct, wt, Math.Abs(ct) > 1e-9 ? DoctrineRules.Signed(ct) : "", Temper(ct)));
                }
                var row = new ShipLine
                {
                    Depth = depth, Side = u.Side, Unit = u, Ships = ships, Design = ships.Design, Text = TitleOf(ships.Design),
                    Count = ships.Count.ToString(Inv), NameList = ships.Names, Type = InfoOf(ships.Design)?.Type ?? "",
                    StruckList = [.. ships.Struck.Select(n => n.Length > 0 ? n : "unnamed")],
                    Unnamed = unnamed > 0 && live > 0 ? $"and {unnamed} unnamed" : unnamed > 0 ? $"{unnamed} unnamed" : "",
                    Captains = [.. captains],
                    NamesTip = ships.Struck.Length > 0 ? $"taken off: {Struck(ships.Struck)}" : "",
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
        var named = struck.Where(n => n.Length > 0).ToList();
        int unnamed = struck.Length - named.Count;
        if (unnamed > 0)
            named.Add($"{unnamed} unnamed");
        return string.Join(", ", named);
    }

    /// <summary>A design's class id in the pick, or -1.</summary>
    int ClassOf(string design) => world.LoadIds is { } ids && ids.TryGetValue(design, out int id) ? id : -1;

    /// <summary>The selected design's row on the side shown: the one picked, else the side's first.</summary>
    ShipLine? Sel
    {
        get
        {
            int sd = OrdersSide;
            if (sd < 0 || sd >= linesBySide.Length)
                return null;
            ShipLine? first = null;
            foreach (var l in linesBySide[sd])
            {
                if (l.IsUnit)
                    continue;
                first ??= l;
                if (selKey is var (s, u, d) && s == sd && l.Unit.Name == u && l.Design == d)
                    return l;
            }
            return first;
        }
    }

    bool NamesShown(ShipLine l)
    {
        int sideShips = 0;
        foreach (var r in linesBySide[l.Side])
            sideShips += r.IsUnit ? 0 : r.Ships.Count;
        return sideShips <= SmallShips != namesToggled.Contains((l.Side, l.Unit.Name, l.Design));
    }

    /// <summary>A unit's tonnage, once every class under it is figured; made again as figures come in.</summary>
    string TonsOf(ShipLine l)
    {
        if (l.TonsKnown == figuredCount)
            return l.Tons;
        l.TonsKnown = figuredCount;
        double t = 0;
        bool all = true;
        void Walk(ScenarioUnit u)
        {
            foreach (var x in u.Units)
                if (!x.Removed)
                    Walk(x);
            foreach (var sh in u.Ships)
                if (FiguresOf(ClassOf(sh.Design)) is { } f)
                    t += f.Profile.DisplacementT * sh.Count;
                else
                    all = false;
        }
        Walk(l.Unit);
        return l.Tons = all ? t.ToString("N0", Inv) : "";
    }

    // ------------------------------------------------------------------ the page

    // the table's columns from its right edge (reference px): the ✕/▾, state, reach, belt, second, main, kn, t, ships, to scale
    static readonly float[] OobCols = [22, 66, 46, 42, 70, 64, 32, 60, 66, 100];
    readonly float[] oobX = new float[11];   // each column's left edge; [0] the name's

    void OrderOfBattle(Vector2 a, Vector2 size)
    {
        if (!shipsMadeWithDesigns && DesignsRead && current is { } s)
            MadeShips(s);
        int sd = OrdersSide;
        if (sd < 0 || sd >= linesBySide.Length)
            return;
        float selW = MathF.Round(Math.Clamp(size.X * 0.36f, P(380), P(480)));
        float tw = size.X - selW;
        ShipsTable(a, new Vector2(tw, size.Y), sd);
        dl.AddLine(new Vector2(a.X + tw, a.Y), new Vector2(a.X + tw, a.Y + size.Y), U(T.Edge));
        SelectedClass(new Vector2(a.X + tw + 1, a.Y), new Vector2(selW - 1, size.Y));
    }

    void ShipsTable(Vector2 a, Vector2 size, int sd)
    {
        var lines = linesBySide[sd];
        float w = size.X;
        // the sub-header: the side's ships and tonnage
        int ships = 0;
        foreach (var l in lines)
            ships += l.IsUnit && l.Top && !l.Removed ? l.Unit.ShipCount : 0;
        float right = dl.SubHeader(a, w, "ORDER OF BATTLE", sideLabels[Math.Min(shipTab, sideLabels.Length - 1)]);
        dl.TextRight(UiFonts.SansSmall, right, Mid(UiFonts.SansSmall, a.Y, RowH), T.Muted, sideShipsNote.Length > 0 && sideShipsFor == (sd, ships) ? sideShipsNote : SideShipsNote(sd, ships));
        // the columns
        float x = a.X + w;
        for (int i = 0; i < OobCols.Length; i++)
        {
            x -= P(OobCols[i]);
            oobX[10 - i] = x;
        }
        oobX[0] = a.X;
        var h = new Vector2(a.X, a.Y + RowH);
        dl.Head(h, w);
        dl.HeadCell(h, a.X + P(6), "UNIT · CLASS · SHIP");
        dl.HeadCell(h, oobX[1] + P(4), "TO SCALE");
        dl.HeadCell(h, oobX[2] + P(4), "SHIPS");
        dl.HeadCell(h, oobX[4] - P(6), "T", right: true);
        dl.HeadCell(h, oobX[5] - P(6), "KN", right: true);
        dl.HeadCell(h, oobX[5] + P(4), "MAIN");
        dl.HeadCell(h, oobX[6] + P(4), "SECOND");
        dl.HeadCell(h, oobX[8] - P(6), "BELT", right: true);
        dl.HeadCell(h, oobX[9] - P(6), "REACH", right: true);
        dl.HeadCell(h, oobX[9] + P(4), "STATE");

        // to scale: the longest class of either side fits the column
        float longest = 0;
        foreach (var l in linesBySide)
            foreach (var r in l)
                if (!r.IsUnit && ClassOf(r.Design) is int cid and >= 0 && world.Classes.TryGet(cid) is { } c)
                    longest = Math.Max(longest, (float)c.Motion.LengthM);
        float pxPerM = longest > 0 ? Math.Min(P(0.8f), (P(OobCols[9]) - P(10)) / longest) : P(0.6f);

        var bodyAt = new Vector2(a.X, a.Y + RowH + HeadH);
        var bodySize = new Vector2(w, size.Y - RowH - HeadH);
        ImGui.SetCursorScreenPos(bodyAt);
        var keep = dl;
        float y = 0;
        if (ImGui.BeginChild($"##oob{sd}", bodySize, ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            dl = ImGui.GetWindowDrawList();
            var o = ImGui.GetCursorScreenPos();
            float top = ImGui.GetWindowPos().Y, bottom = top + bodySize.Y;
            float cw = ImGui.GetContentRegionAvail().X;
            // the columns again, in the child's width (its scrollbar takes some)
            float shift = cw - w;
            y = o.Y;
            int n = 0;
            int shutDepth = int.MaxValue;   // under a shut unit: its rows are skipped
            var sel = Sel;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (l.IsUnit)
                {
                    if (l.Depth <= shutDepth)
                        shutDepth = int.MaxValue;
                    if (l.Depth > shutDepth)
                        continue;
                    if (y + RowH >= top && y <= bottom)
                        UnitRow(i, l, new Vector2(o.X, y), cw, shift);
                    y += RowH;
                    if (shutUnits.Contains(l.Text))
                        shutDepth = l.Depth;
                    n = 0;
                    continue;
                }
                if (l.Depth >= shutDepth)
                    continue;
                bool names = NamesShown(l);
                if (y + RowH >= top && y <= bottom)
                    ClassRow(i, l, new Vector2(o.X, y), cw, shift, pxPerM, l == sel, names, n);
                y += RowH;
                n++;
                if (!names)
                    continue;
                int rows = l.Captains.Length + (l.Unnamed.Length > 0 ? 1 : 0) + l.StruckList.Length;
                for (int k = 0; k < rows; k++, y += RowH, n++)
                    if (y + RowH >= top && y <= bottom)
                        ShipRow(i, k, l, new Vector2(o.X, y), cw, n);
            }
            // add a unit: a brass row at the table's end
            var aa = new Vector2(o.X, y);
            var (click, hover) = HitRow("##addunit", aa, new Vector2(cw, RowH));
            if (click && !begin)
                OpenAdd(sd);
            dl.RowBg(aa, cw, RowH, n, hover: hover);
            dl.Text(UiFonts.Sans, new Vector2(aa.X + P(20), Mid(UiFonts.Sans, aa.Y, RowH)), hover ? T.Bright : T.Brass, AddLabel);
            dl.Text(UiFonts.SansSmall, new Vector2(aa.X + P(28) + Width(UiFonts.Sans, AddLabel), Mid(UiFonts.SansSmall, aa.Y, RowH)), T.Muted, AddNote);
            y += RowH + P(4) - o.Y;
        }
        ImGui.SetCursorPos(new Vector2(0, Math.Max(y, 0)));
        ImGui.Dummy(new Vector2(1, 0));
        ImGui.EndChild();
        dl = keep;
    }

    const string AddLabel = "+ Add a unit…", AddNote = "joins a unit on its station, or stands alone placed on the opening  (A)";

    string sideShipsNote = "";
    (int, int) sideShipsFor = (-1, -1);

    string SideShipsNote(int sd, int ships)
    {
        sideShipsFor = (sd, ships);
        return sideShipsNote = $"{ships} ship{(ships == 1 ? "" : "s")}";
    }

    /// <summary>A unit's group row: its twisty, its name (a click renames it; struck when it's taken off), its admiral, his
    /// temper's chip, ADDED or PLACING, its ships and tonnage, + division and move, its ✕ (or its undo).</summary>
    void UnitRow(int i, ShipLine l, Vector2 a, float w, float shift)
    {
        ImGui.PushID(i);
        var (click, hover) = HitRow("##unit", a, new Vector2(w, RowH));
        dl.GroupBg(a, w, RowH, hover);
        float x = a.X + P(6) + l.Depth * P(14);
        bool shut = shutUnits.Contains(l.Text);
        if (!l.Removed)
            dl.Twisty(new Vector2(x + P(4), a.Y + RowH / 2), !shut);
        if (click && !l.Removed && ImGui.GetMousePos().X < x + P(12) && !shutUnits.Remove(l.Text))
            shutUnits.Add(l.Text);
        x += P(12);
        float ty = Mid(UiFonts.SansBold, a.Y, RowH);
        float nameW = Width(UiFonts.SansBold, l.Text);
        float figures = oobX[2] + shift;   // the group row's own cells start at Ships
        if (renaming == l)
        {
            ImGui.SetCursorScreenPos(new Vector2(x - P(4), a.Y + P(2)));
            ImGui.SetNextItemWidth(Math.Min(P(260), figures - x - P(10)));
            if (renameFocus)
            {
                ImGui.SetKeyboardFocusHere();
                renameFocus = false;
            }
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(4), 0));
            bool done = ImGui.InputText("##rename", ref renameText, 64, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
            ImGui.PopStyleVar();
            bool left = !done && ImGui.IsItemDeactivated();
            if (done)
            {
                string to = renameText.Trim();
                if (to.Length > 0 && to != l.Text && !TakenNames().Contains(to))
                    pendingEdit = setup.Rename(l.Text, to);
                renaming = null;
            }
            else if (left)
                renaming = null;
            x += Math.Min(P(260), figures - x - P(10)) + P(4);
        }
        else
        {
            dl.Text(UiFonts.SansBold, new Vector2(x, ty), l.Removed ? T.Muted : T.Bright, l.Text);
            if (l.Removed)
                dl.HLine(x, x + nameW, a.Y + RowH / 2, T.Muted);
            else
            {
                ImGui.SetCursorScreenPos(new Vector2(x, a.Y));
                if (ImGui.InvisibleButton("##name", new Vector2(nameW, RowH)) && !begin)
                    (renaming, renameText, renameFocus) = (l, l.Text, true);
                if (ImGui.IsItemHovered())
                {
                    dl.HLine(x, x + nameW, a.Y + RowH - P(4), T.BrassDark);
                    ImGui.SetTooltip("click to rename");
                }
            }
            x += nameW + P(6);
        }
        float room = figures - P(8);
        if (!l.Removed)
        {
            // the admiral, then the chips after him (they say a state: kept whole, the admiral cut to make room)
            bool now = placing?.Unit.Name == l.Text;
            string added = !l.Added ? "" : now ? "placing…" : "added";
            float chipsW = (l.Chip.Length > 0 ? ChipWidth(l.Chip) + P(4) : 0) + (added.Length > 0 ? ChipWidth(added) + P(4) : 0);
            float adW = l.Admiral.Length > 0 ? Math.Min(Width(UiFonts.SansSmall, l.Admiral), Math.Max(0, room - chipsW - x)) : 0;
            if (adW > P(20))
            {
                dl.PushClipRect(new Vector2(x, a.Y), new Vector2(x + adW, a.Y + RowH), true);
                dl.Text(UiFonts.SansSmall, new Vector2(x, Mid(UiFonts.SansSmall, a.Y, RowH)), T.Muted, l.Admiral);
                dl.PopClipRect();
                x += adW + P(6);
            }
            if (l.Chip.Length > 0 && x + ChipWidth(l.Chip) < room)
            {
                dl.Chip(new Vector2(x, a.Y + P(2.5f)), l.Chip, l.Bold ? ChipKind.Amber : ChipKind.Plain);
                if (ImGui.IsMouseHoveringRect(new Vector2(x, a.Y), new Vector2(x + ChipWidth(l.Chip), a.Y + RowH)))
                    ImGui.SetTooltip("his temper, added to the side's aggression (Standing orders)");
                x += ChipWidth(l.Chip) + P(4);
            }
            if (added.Length > 0 && x + ChipWidth(added) < room)
                dl.Chip(new Vector2(x, a.Y + P(2.5f)), added, now ? ChipKind.Amber : ChipKind.Ok);
            // its ships and tonnage under Ships and t
            dl.TextRight(UiFonts.Mono, oobX[3] + shift - P(6), Mid(UiFonts.Mono, a.Y, RowH), T.Bright, l.Right);
            dl.TextRight(UiFonts.Mono, oobX[4] + shift - P(6), Mid(UiFonts.Mono, a.Y, RowH), T.Bright, TonsOf(l));
            // + division and move in the figures' room
            float lx = oobX[5] + shift + P(4);
            if (dl.Link("##division", new Vector2(lx, a.Y + P(2)), "+ division", out float dw, muted: true))
                pendingAdd = (l.Side, l.Text);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"add a unit to {l.Text}, on its station there");
            lx += dw + P(12);
            if (l.Movable)
            {
                if (dl.Link("##move", new Vector2(lx, a.Y + P(2)), "move", out _) && setup.AddedHolding(l.Text) is { } ad)
                    StartPlacing(ad.Unit, ad.Unit.CourseDeg, ad.Unit.Kn);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("place it again on the opening");
            }
            if (dl.CrossButton("##rm", new Vector2(oobX[10] + shift + P(11), a.Y + RowH / 2),
                    l.Added ? "take the added unit away" : "remove the unit (its undo stays here)") && !begin)
                pendingEdit = l.Added ? setup.WithoutAdded(l.Text) : setup.With(new RemoveUnit(setup.NameAsMade(l.Text)));
        }
        else
        {
            const string Undo = "removed · undo";
            float uw = Width(UiFonts.SansSmall, Undo);
            if (dl.Link("##undo", new Vector2(a.X + w - P(8) - uw, a.Y + P(2)), Undo, out _))
                pendingEdit = setup.Without(new RemoveUnit(setup.NameAsMade(l.Text)).Key);
        }
        ImGui.PopID();
    }

    /// <summary>A design's row: her twisty (her ships' names), name, drawn to scale, ◀ n ▶, figures, build, ▾ (the
    /// picker). A click selects her.</summary>
    void ClassRow(int i, ShipLine l, Vector2 a, float w, float shift, float pxPerM, bool selected, bool names, int n)
    {
        ImGui.PushID(i);
        var (click, hover) = HitRow("##row", a, new Vector2(w, RowH));
        var key = (l.Side, l.Unit.Name, l.Design);
        float x = a.X + P(6) + (l.Depth + 1) * P(14);
        if (click)
        {
            if (ImGui.GetMousePos().X < x + P(12) && !namesToggled.Remove(key))
                namesToggled.Add(key);
            selKey = key;
        }
        dl.RowBg(a, w, RowH, n, selected, hover);
        dl.Twisty(new Vector2(x + P(4), a.Y + RowH / 2), names);
        x += P(12);
        float scaleX = oobX[1] + shift;
        dl.PushClipRect(new Vector2(x, a.Y), new Vector2(scaleX - P(4), a.Y + RowH), true);
        dl.Text(UiFonts.Sans, new Vector2(x, Mid(UiFonts.Sans, a.Y, RowH)), selected ? T.Brass : T.Bright, l.Text);
        if (l.Swapped)
            dl.Text(UiFonts.SansSmall, new Vector2(x + Width(UiFonts.Sans, l.Text) + P(6), Mid(UiFonts.SansSmall, a.Y, RowH)), T.Muted, l.Was);
        dl.PopClipRect();
        int id = ClassOf(l.Design);
        dl.PushClipRect(new Vector2(scaleX, a.Y), new Vector2(oobX[2] + shift, a.Y + RowH), true);
        Thumb(id, new Vector2(scaleX + P(OobCols[9]) / 2, a.Y + RowH / 2), pxPerM, l.Ships.Count > 0 ? 1 : 0.35f);
        dl.PopClipRect();
        dl.Stepper("##n", new Vector2(oobX[2] + shift + P(2), a.Y + P(2.5f)), l.Count, P(26), l.Fewer, true, out int by);
        if (by != 0)
            pendingEdit = setup.Resize(setup.NameAsMade(l.Unit.Name), l.Design, Math.Sign(by));
        float my = Mid(UiFonts.Mono, a.Y, RowH);
        if (FiguresOf(id) is { } f)
        {
            dl.TextRight(UiFonts.Mono, oobX[4] + shift - P(6), my, T.Bright, f.Displacement);
            dl.TextRight(UiFonts.Mono, oobX[5] + shift - P(6), my, T.Bright, f.Speed);
            dl.Text(UiFonts.Mono, new Vector2(oobX[5] + shift + P(4), my), T.Text, f.Main);
            dl.Text(UiFonts.Mono, new Vector2(oobX[6] + shift + P(4), my), T.Text, f.Second);
            dl.TextRight(UiFonts.Mono, oobX[8] + shift - P(6), my, T.Bright, f.Belt);
            dl.TextRight(UiFonts.Mono, oobX[9] + shift - P(6), my, T.Bright, f.Reach);
        }
        BuildState(id, new Vector2(oobX[9] + shift + P(4), a.Y));
        // ▾: the design picker
        var dc = new Vector2(oobX[10] + shift + P(11), a.Y + RowH / 2);
        ImGui.SetCursorScreenPos(dc - new Vector2(P(8), P(9)));
        if (ImGui.InvisibleButton("##change", new Vector2(P(16), P(18))) && !begin)
            OpenPicker(l);
        bool ch = ImGui.IsItemHovered();
        dl.Arrow(dc, 1, P(3.5f), ch ? T.Bright : T.Muted);
        if (ch)
            ImGui.SetTooltip("change the design… (D)");
        if (hover && !ch && l.NamesTip.Length > 0)
            ImGui.SetTooltip(l.NamesTip);
        ImGui.PopID();
    }

    /// <summary>A ship of a row, by name: her captain's temper marked (bold ▲ amber, timid ▼ blue); then the unnamed; then
    /// the struck, dim and crossed out. A click selects her design's row.</summary>
    void ShipRow(int i, int k, ShipLine l, Vector2 a, float w, int n)
    {
        ImGui.PushID(i * 1024 + k);
        var (click, hover) = HitRow("##ship", a, new Vector2(w, RowH));
        ImGui.PopID();
        if (click)
            selKey = (l.Side, l.Unit.Name, l.Design);
        dl.RowBg(a, w, RowH, n, hover: hover);
        float x = a.X + P(6) + (l.Depth + 2) * P(14) + P(16);
        float ty = Mid(UiFonts.Sans, a.Y, RowH);
        if (k < l.Captains.Length)
        {
            var c = l.Captains[k];
            dl.Text(UiFonts.Sans, new Vector2(x, ty), T.Text, c.Name);
            if (c.Mark.Length > 0)
                Mark(new Vector2(x + Width(UiFonts.Sans, c.Name) + P(8), a.Y), c.Temper, c.Mark);
            return;
        }
        k -= l.Captains.Length;
        if (l.Unnamed.Length > 0 && k == 0)
        {
            dl.Text(UiFonts.SansSmall, new Vector2(x, Mid(UiFonts.SansSmall, a.Y, RowH)), T.Muted, l.Unnamed);
            return;
        }
        k -= l.Unnamed.Length > 0 ? 1 : 0;
        var dim = T.Muted with { W = 0.7f };
        string s = l.StruckList[k];
        dl.Text(UiFonts.Sans, new Vector2(x, ty), dim, s);
        dl.HLine(x, x + Width(UiFonts.Sans, s), a.Y + RowH / 2, dim);
        if (k == 0)
            dl.Text(UiFonts.SansSmall, new Vector2(x + Width(UiFonts.Sans, s) + P(8), Mid(UiFonts.SansSmall, a.Y, RowH)), T.Muted, "taken off: the row's stepper puts her back");
    }

    /// <summary>A captain's temper by her name: ▲ bold amber, ▼ timid plain, and the figure.</summary>
    void Mark(Vector2 at, double temper, string figure)
    {
        var col = temper > 0 ? T.Amber : T.Text;
        dl.Arrow(new Vector2(at.X + P(4), at.Y + RowH / 2), temper > 0 ? 3 : 1, P(4), col);
        dl.Text(UiFonts.Mono, new Vector2(at.X + P(12), Mid(UiFonts.Mono, at.Y, RowH)), col, figure);
    }

    /// <summary>A class's build in a word: building, its art coming, ready, or failed.</summary>
    void BuildState(int id, Vector2 at)
    {
        if (id < 0)
            return;
        var (word, col) = !world.Classes.IsReady(id) ? ("building", T.Muted)
            : world.Classes.ErrorOf(id) != null ? ("failed", T.Amber)
            : art.Get(id) == null ? ("art…", T.Muted) : ("ready", T.Ok);
        dl.AddCircleFilled(at + new Vector2(P(3), RowH / 2), P(3), U(col), 10);
        dl.Text(UiFonts.SansSmall, new Vector2(at.X + P(10), Mid(UiFonts.SansSmall, at.Y, RowH)), col == T.Ok ? T.Ok : T.Muted, word);
    }

    // ------------------------------------------------------------------ the selected class

    sealed class SelText
    {
        public ShipLine? Of;
        public int Id = -2;
        public bool Figured;
        public string File = "", Source = "", Length = "", Year = "";
        public (string Label, string Value, string Note)[] Rows = [];
    }

    readonly SelText selText = new();

    void MakeSel(ShipLine l, int id, Figures? f)
    {
        if (selText.Of == l && selText.Id == id && selText.Figured == (f != null))
            return;
        (selText.Of, selText.Id, selText.Figured) = (l, id, f != null);
        var info = InfoOf(l.Design);
        selText.File = info != null ? Path.GetFileName(info.Path) : l.Design + ".json";
        selText.Source = info == null ? "" : info.Yours ? "your design" : written!.DesignNames().Contains(l.Design, StringComparer.OrdinalIgnoreCase) ? "scenario design" : "shipped design";
        selText.Year = info is { Year: > 0 } ? $"tech {info.Year}" : "";
        selText.Length = f != null ? $"{f.Profile.LengthM:0.0} m oa" : "";
        var rows = new List<(string, string, string)>();
        if (f != null)
        {
            rows.Add(("Displacement", f.Tonnes, "standard"));
            rows.Add(("Speed", f.Knots, ""));
            rows.Add(("Main", f.Main == "—" ? "—" : f.Main + " mm", ""));
            rows.Add(("Secondary", f.Second, ""));
            rows.Add(("Belt", f.BeltMm > 0 ? f.Belt + " mm" : "—", "at the waterline"));
            rows.Add(("Reach", f.Profile.MainReachM > 0 ? f.Reach + " km" : "—", "the main guns'"));
        }
        if (l.Swapped)
            rows.Add(("Changed", l.Was, ""));
        selText.Rows = [.. rows];
    }

    void SelectedClass(Vector2 a, Vector2 size)
    {
        var l = Sel;
        float w = size.X;
        if (l == null)
        {
            dl.SubHeader(a, w, "SELECTED CLASS");
            return;
        }
        int id = ClassOf(l.Design);
        var f = FiguresOf(id);
        MakeSel(l, id, f);
        float right = dl.SubHeader(a, w, "SELECTED CLASS");
        float nx = a.X + P(14) + Width(UiFonts.SansSmall, "SELECTED CLASS");
        float srcX = right - Width(UiFonts.SansSmall, selText.Source);
        dl.PushClipRect(new Vector2(nx, a.Y), new Vector2(srcX - P(8), a.Y + RowH), true);
        dl.Text(UiFonts.SansBold, new Vector2(nx, Mid(UiFonts.SansBold, a.Y, RowH)), T.Bright, l.Text);
        dl.Text(UiFonts.SansSmall, new Vector2(nx + Width(UiFonts.SansBold, l.Text) + P(8), Mid(UiFonts.SansSmall, a.Y, RowH)), T.Muted, selText.File);
        dl.PopClipRect();
        dl.Text(UiFonts.SansSmall, new Vector2(srcX, Mid(UiFonts.SansSmall, a.Y, RowH)), T.Muted, selText.Source);

        ImGui.SetCursorScreenPos(a + new Vector2(0, RowH));
        var keep = dl;
        float y = 0;
        if (ImGui.BeginChild("##selclass", new Vector2(w, size.Y - RowH), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            dl = ImGui.GetWindowDrawList();
            var o = ImGui.GetCursorScreenPos();
            float cw = ImGui.GetContentRegionAvail().X;
            y = o.Y;
            // her drawing, to scale on the plan ground, her length over her
            float planH = P(104);
            var p0 = new Vector2(o.X, y);
            dl.AddRectFilled(p0, p0 + new Vector2(cw, planH), U(T.Plan));
            dl.HLine(p0.X, p0.X + cw, p0.Y + planH - 1, T.Edge);
            if (id >= 0 && world.Classes.TryGet(id) is { } c)
            {
                float len = (float)c.Motion.LengthM;
                float k = Math.Min(P(4), (cw - P(40)) / Math.Max(len, 1));
                var mid = new Vector2(p0.X + cw / 2, p0.Y + planH / 2 + P(10));
                Thumb(id, mid, k);
                float x0 = mid.X - len * k / 2, x1 = mid.X + len * k / 2, ly = p0.Y + P(16);
                uint line = U(T.PlanLine with { W = 0.7f });
                dl.AddLine(new Vector2(x0, ly), new Vector2(x1, ly), line);
                dl.AddLine(new Vector2(x0, ly - P(4)), new Vector2(x0, ly + P(4)), line);
                dl.AddLine(new Vector2(x1, ly - P(4)), new Vector2(x1, ly + P(4)), line);
                if (selText.Length.Length > 0)
                    dl.Tag(new Vector2(mid.X - TagSize(selText.Length).X / 2, ly - P(8)), selText.Length);
            }
            dl.TextRight(UiFonts.MonoSmall, p0.X + cw - P(8), p0.Y + planH - P(16), T.PlanLine, "to scale");
            y += planH;
            // her particulars
            var h = new Vector2(o.X, y);
            dl.Head(h, cw);
            float vx = o.X + P(200);
            dl.HeadCell(h, o.X + P(6), "PARTICULAR");
            dl.HeadCell(h, vx, "VALUE", right: true);
            dl.HeadCell(h, vx + P(12), "NOTE");
            y += HeadH;
            if (selText.Rows.Length == 0)
            {
                dl.Text(UiFonts.SansSmall, new Vector2(o.X + P(6), Mid(UiFonts.SansSmall, y, RowH)), T.Muted, id >= 0 && world.Classes.ErrorOf(id) is { } err ? err : "her class is building");
                y += RowH;
            }
            for (int r = 0; r < selText.Rows.Length; r++)
            {
                var (label, value, note) = selText.Rows[r];
                var ra = new Vector2(o.X, y);
                dl.RowBg(ra, cw, RowH, r);
                dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Muted, label);
                if (label == "Changed")
                    dl.Text(UiFonts.SansSmall, new Vector2(vx - P(110), Mid(UiFonts.SansSmall, ra.Y, RowH)), T.Text, value);
                else
                    dl.TextRight(UiFonts.Mono, vx, Mid(UiFonts.Mono, ra.Y, RowH), T.Bright, value);
                dl.Text(UiFonts.SansSmall, new Vector2(vx + P(12), Mid(UiFonts.SansSmall, ra.Y, RowH)), T.Muted, note);
                y += RowH;
            }
            // the design's buttons
            float right2 = dl.SubHeader(new Vector2(o.X, y), cw, "DESIGN");
            if (selText.Year.Length > 0)
                dl.RightNote(right2, y, RowH, selText.Year);
            y += RowH + P(6);
            const string ChangeL = "Change design…", CopyL = "Edit a copy in the designer";
            if (dl.Button("##seldesign", new Vector2(o.X + P(6), y), ChangeL, "D", enabled: !begin))
                OpenPicker(l);
            if (dl.Button("##selcopy", new Vector2(o.X + P(10) + ButtonWidth(ChangeL, "D"), y), CopyL, "E", enabled: !begin && EditCopy != null))
                Copy(InUseCandidate(l));
            y += P(28);
            // her captains
            dl.SubHeader(new Vector2(o.X, y), cw, "CAPTAINS", "added to the admiral's temper");
            y += RowH;
            if (l.Captains.Length == 0)
            {
                dl.Text(UiFonts.SansSmall, new Vector2(o.X + P(6), Mid(UiFonts.SansSmall, y, RowH)), T.Muted, "Her captains are unnamed: a named ship's captain takes a temper.");
                y += RowH;
            }
            float sx = o.X + cw - P(6) - (2 * P(13) + P(44) + P(4));
            for (int k = 0; k < l.Captains.Length; k++)
            {
                var cap = l.Captains[k];
                var ra = new Vector2(o.X, y);
                dl.RowBg(ra, cw, RowH, k);
                dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Text, cap.Name);
                if (Math.Abs(cap.Temper - cap.Written) > 1e-9)
                    dl.TextRight(UiFonts.SansSmall, sx - P(8), Mid(UiFonts.SansSmall, ra.Y, RowH), T.Muted, "changed");
                ImGui.PushID(k);
                dl.Stepper("##cap", new Vector2(sx, ra.Y + P(2.5f)), cap.Value, P(44), cap.Temper > -1 + 1e-9, cap.Temper < 1 - 1e-9, out int by);
                ImGui.PopID();
                if (by != 0)
                    pendingEdit = setup.Temper(written!, cap.Name, Math.Clamp(Math.Round((cap.Temper + by * 0.05) * 20) / 20, -1, 1));
                y += RowH;
            }
            y += P(4) - o.Y;
        }
        ImGui.SetCursorPos(new Vector2(0, Math.Max(y, 0)));
        ImGui.Dummy(new Vector2(1, 0));
        ImGui.EndChild();
        dl = keep;
    }
}
