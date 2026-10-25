using System.Numerics;
using Fleetwright.Sim.Missions;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The launch screen's ships (docs/map-ui/launch.md L3, the mockup's first column): each side's order of battle
/// as a tab (yours first, then the enemy's: decision 3, the enemy's can be changed too), its units and under them a row
/// a design: drawn to scale, how many and their names, the class's figures, its build, and Change… for the design
/// picker. A row whose design the setup changed says what it was. The lines are made when the pick changes; a frame
/// allocates nothing (the names are cut to the column once per width).</summary>
public sealed unsafe partial class LaunchScene
{
    /// <summary>A unit's heading or a row of ships of one design.</summary>
    sealed class ShipLine
    {
        public bool IsUnit, Top;
        public int Depth, Side;
        public string Text = "", Right = "";   // a unit's name and its count
        // a row
        public ScenarioUnit Unit = null!;
        public ScenarioShips Ships = null!;
        public string Design = "", Count = "", Names = "", Was = "", Type = "";
        public string[] NameList = [];
        public bool Swapped;
        public string NamesShown = "";
        public float NamesWidth = -1;
    }

    readonly List<(int Side, string Label, string Ensign)> shipTabs = [];
    List<ShipLine>[] linesBySide = [];
    int shipTab;
    bool shipsMadeWithDesigns;   // the titles were the designs' (else their files', until those are read)

    void MadeShips(Scenario s)
    {
        shipsMadeWithDesigns = DesignsRead;
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
                Lines(u, 0, [], lines);
        }

        void Lines(ScenarioUnit u, int depth, List<string> above, List<ShipLine> lines)
        {
            lines.Add(new ShipLine
            {
                IsUnit = true, Top = depth == 0, Depth = depth, Side = u.Side, Text = u.Name,
                Right = depth == 0 ? $"{u.ShipCount} ship{(u.ShipCount == 1 ? "" : "s")}" : "",
            });
            var chain = new List<string>(above) { u.Name };
            foreach (var sub in u.Units)
                Lines(sub, depth + 1, chain, lines);
            foreach (var ships in u.Ships)
            {
                var row = new ShipLine
                {
                    Depth = depth, Side = u.Side, Unit = u, Ships = ships, Design = ships.Design, Text = TitleOf(ships.Design),
                    Count = ships.Count.ToString(Inv), NameList = ships.Names, Type = InfoOf(ships.Design)?.Type ?? "",
                    Names = ships.Names.Length > 0 ? string.Join(", ", ships.Names) + (ships.Names.Length < ships.Count ? $" and {ships.Count - ships.Names.Length} more" : "")
                        : $"{ships.Count} unnamed",
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

    /// <summary>The ships column: the sides' tabs, then the units and their rows, scrolling.</summary>
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
                {
                    float ux = o.X + l.Depth * 16 * sc;
                    if (l.Top)
                    {
                        Mark(new Vector2(o.X + 4 * sc, y + 10 * sc), ring, colour, 3.5f, 1);
                        ux += 16 * sc;
                    }
                    Txt(UiFonts.Sans, l.Top ? 13 : 12.5f, new Vector2(ux, y + 2 * sc), Bright, l.Text);
                    if (l.Right.Length > 0)
                        Txt(UiFonts.SansSmall, 11.5f, new Vector2(o.X + inner - Width(UiFonts.SansSmall, 11.5f, l.Right), y + 3 * sc), Muted, l.Right);
                    y += 24 * sc;
                }
                else
                    y += ShipRow(i, l, new Vector2(o.X + (l.Depth + 1) * 14 * sc, y), inner - (l.Depth + 1) * 14 * sc, pxPerM) + 8 * sc;
            }
            ImGui.SetCursorScreenPos(new Vector2(o.X, y));
            ImGui.Dummy(new Vector2(1, 4 * sc));
            dl = keep;
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    const float ThumbW = 176;   // the room for a row's ship, reference px

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
        Thumb(id, at + new Vector2(10 * sc + ThumbW * sc / 2, 30 * sc), pxPerM);
        // how many, the design, the names
        float ix = at.X + (20 + ThumbW) * sc, iy = at.Y + 10 * sc;
        float cw = Width(UiFonts.Mono, 12, l.Count) + 14 * sc;
        Box(new Vector2(ix, iy), new Vector2(ix + cw, iy + 18 * sc), new Vector4(0, 0, 0, 0.25f), EdgeC, 2);
        Txt(UiFonts.Mono, 12, new Vector2(ix + 7 * sc, iy + 1.5f * sc), Brass, l.Count);
        Txt(UiFonts.Sans, 13, new Vector2(ix + cw + 8 * sc, iy), Bright, l.Text);
        float namesW = at.X + w - 92 * sc - ix;
        if (Math.Abs(namesW - l.NamesWidth) > 0.5f)
            (l.NamesWidth, l.NamesShown) = (namesW, Clip(UiFonts.SansSmall, 11, l.Names, namesW));
        Txt(UiFonts.SansSmall, 11, new Vector2(ix, iy + 22 * sc), Muted, l.NamesShown);
        ImGui.SetCursorScreenPos(new Vector2(ix, iy + 20 * sc));
        ImGui.InvisibleButton("##names", new Vector2(namesW, 16 * sc));
        if (ImGui.IsItemHovered() && !ReferenceEquals(l.NamesShown, l.Names))
            ImGui.SetTooltip(l.Names);
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
