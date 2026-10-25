using System.Collections.Concurrent;
using System.Numerics;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Missions;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The launch screen's design picker (docs/map-ui/launch.md L3, the mockup's <c>#design</c>): over the columns
/// right of the ships, for one row. Every design the scenario can name (the scenario's, the player's, the shipped: decision
/// 6, every filter off at first), their classes asked for without their art as it opens so their figures fill in, the
/// one picked drawn; the old and the new side by side with what changes; used for the row's unit, one ship of it, or
/// every ship of that design in the scenario, as a <see cref="SwapDesign"/> of the setup (<see cref="ScenarioSetup.Swap"/>:
/// one that puts back what the last change of the same reach replaced takes that change away). A copy of a design goes to the designer; saved
/// there, it's back here as the player's, picked. In its add mode (L3b, <c>LaunchScene.Add.cs</c>) it makes a new unit
/// of the design picked instead.</summary>
public sealed unsafe partial class LaunchScene
{
    /// <summary>A design the picker offers.</summary>
    sealed class Candidate
    {
        public required DesignInfo Info;
        public bool Scenario, InUse;   // named by the scenario as written; the row's design now
        public string Chip = "", Sub = "";
    }

    /// <summary>The design picker opens a design's copy in the designer.</summary>
    public Action<Design>? EditCopy { get; init; }

    ShipLine? pickerRow;
    readonly List<Candidate> candidates = [];
    readonly List<int> shownCands = [];
    bool candsDirty;
    int chosen = -1, scope, scopeShip, pickerYear, unitCount, everyCount;
    bool sameType, fromScenario = true, fromYours = true, fromShipped = true, upToYear;
    string search = "", newFrom = "", pickerTitle = "", pickerNames = "", typeChip = "", yearChip = "", unitScope = "", everyScope = "";
    readonly Dictionary<string, int> designIds = new(StringComparer.OrdinalIgnoreCase);   // a file's class, by its path
    readonly HashSet<string> asked = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentQueue<(string Path, int Id)> answered = new();
    DateTime? copyStarted;

    void OpenPicker(ShipLine row)
    {
        adding = false;
        pickerRow = row;
        (scope, scopeShip, search, chosen) = (0, 0, "", -1);
        cmp.Names = "";
        ResetMatchup();
        pickerTitle = $"{row.Count} × {row.Text} · {row.Unit.Name}";
        pickerNames = row.Names;
        pickerYear = current!.Start.ToUtc().Year;
        typeChip = TypeName(row.Type);
        yearChip = $"UP TO {pickerYear} ONLY";
        (unitCount, everyCount) = (0, 0);
        void Count(ScenarioUnit u, bool inUnit)
        {
            inUnit |= u == row.Unit;
            foreach (var sub in u.Units)
                Count(sub, inUnit);
            foreach (var sh in u.Ships)
                if (sh.Design == row.Design)
                {
                    everyCount += sh.Count;
                    unitCount += inUnit ? sh.Count : 0;
                }
        }
        foreach (var u in current.Units)
            Count(u, false);
        unitScope = unitCount == 1 ? $"her, of {row.Unit.Name}" : $"all {unitCount} of {row.Unit.Name}";
        everyScope = $"every {row.Text} in the scenario ({everyCount})";
        newFrom = $"+ New design from {row.Text}…";
        Candidates();
    }

    void ClosePicker()
    {
        pickerRow = null;
        adding = false;
        copyStarted = null;
    }

    static string TypeName(string type) => type switch
    {
        "BB" => "BATTLESHIPS",
        "BC" => "BATTLECRUISERS",
        "CA" => "ARMOURED CRUISERS",
        "CL" => "LIGHT CRUISERS",
        "DD" => "DESTROYERS",
        "" => "SAME TYPE",
        _ => type + "S",
    };

    /// <summary>The candidates, from the designs read: the row's own first, then the player's newest first, the
    /// scenario's, the shipped by year; each one's class asked for (no art) the first time it's listed.</summary>
    void Candidates()
    {
        candidates.Clear();
        var row = pickerRow!;
        if (!DesignsRead)
            return;
        var mine = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var named = new HashSet<string>(written!.DesignNames(), StringComparer.OrdinalIgnoreCase);
        foreach (var u in written.Units.Where(u => u.Side == row.Side))
            foreach (var n in new Scenario { Name = "", Start = written.Start, Sides = written.Sides, Units = [u] }.DesignNames())
                mine.Add(n);
        foreach (var d in Designs())
        {
            var c = new Candidate { Info = d, Scenario = named.Contains(d.Stem) && !d.Yours, InUse = d.Stem.Equals(row.Design, StringComparison.OrdinalIgnoreCase) };
            c.Chip = d.Yours ? "YOURS" : c.Scenario ? "SCENARIO" : "SHIPPED";
            var sub = new List<string>();
            if (c.Scenario && !mine.Contains(d.Stem))
                sub.Add("the enemy's own class");
            if (!d.Yours && d.Year > 0)
                sub.Add(d.Year > pickerYear ? $"tech {d.Year}: {d.Year - pickerYear} year{(d.Year - pickerYear == 1 ? "" : "s")} late" : $"tech {d.Year}");
            if (d.Sub.Length > 0)
                sub.Add(d.Sub);
            c.Sub = string.Join(" · ", sub);
            candidates.Add(c);
        }
        candidates.Sort((a, b) =>
        {
            int Rank(Candidate c) => c.InUse ? 0 : c.Info.Yours ? 1 : c.Scenario ? 2 : 3;
            int ra = Rank(a), rb = Rank(b);
            return ra != rb ? ra.CompareTo(rb)
                : ra == 1 ? b.Info.Modified.CompareTo(a.Info.Modified)
                : (a.Info.Year, a.Info.Title).CompareTo((b.Info.Year, b.Info.Title));
        });
        chosen = candidates.FindIndex(c => c.InUse);
        if (adding)   // a design of the side's own to start from
            chosen = Math.Max(0, candidates.FindIndex(c => c.Scenario && mine.Contains(c.Info.Stem)));
        candsDirty = true;
        // their classes, without art: each design read and asked for on a thread
        var ask = candidates.Select(c => c.Info).Where(d => asked.Add(d.Path)).ToArray();
        var classes = world.Classes;
        if (ask.Length > 0)
            Task.Run(() =>
            {
                foreach (var d in ask)
                    try
                    {
                        d.Design ??= Design.Load(d.Path);
                        answered.Enqueue((d.Path, classes.Request(d.Design, art: false)));
                    }
                    catch (Exception x)
                    {
                        Console.Error.WriteLine($"launch: design {d.Path}: {x.Message}");
                    }
            });
    }

    int IdOf(Candidate c) => designIds.TryGetValue(c.Info.Path, out int id) ? id : c.InUse ? ClassOf(c.Info.Stem) : -1;

    void Choose(int i)
    {
        chosen = i;
        // the one picked is drawn (its art: the comparison's, and the map's if it's used)
        if (i >= 0 && candidates[i].Info.Design is { } d)
            world.Classes.Request(d);
    }

    void Filter()
    {
        candsDirty = false;
        shownCands.Clear();
        var row = pickerRow!;
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (!c.InUse && !(c.Info.Yours ? fromYours : c.Scenario ? fromScenario : fromShipped))
                continue;
            if (sameType && c.Info.Type != row.Type)
                continue;
            if (upToYear && c.Info.Year > pickerYear)
                continue;
            if (search.Length > 0 && !Matches(c, search.Trim()))
                continue;
            shownCands.Add(i);
        }
    }

    bool Matches(Candidate c, string q) =>
        c.Info.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || c.Info.Stem.Contains(q, StringComparison.OrdinalIgnoreCase)
        || c.Sub.Contains(q, StringComparison.OrdinalIgnoreCase) || c.Info.Year.ToString(Inv) == q
        || FiguresOf(IdOf(c)) is { } f && f.Line.Contains(q, StringComparison.OrdinalIgnoreCase);

    /// <summary>The design picked, used for the reach chosen: a change of the setup, or the one that brought the row's
    /// design changed again (taken away when it puts back what it replaced).</summary>
    void UseChosen()
    {
        if (pickerRow is not { } row || chosen < 0)
            return;
        string to = candidates[chosen].Info.Stem, from = row.Design;
        string? unit = scope == 0 ? row.Unit.Name : null, ship = scope == 1 && row.NameList.Length > 0 ? row.NameList[Math.Min(scopeShip, row.NameList.Length - 1)] : null;
        ClosePicker();
        if (to != from)
            Edit(setup.Swap(unit, ship, from, to));
    }

    /// <summary>The screen is entered again (back from the designer): the player's designs read again, and a copy made
    /// from the picker and saved since is picked there.</summary>
    public void Entered()
    {
        RescanYours();
        if (pickerRow == null)
            return;
        var since = copyStarted;
        copyStarted = null;
        string? was = chosen >= 0 ? candidates[chosen].Info.Path : null;
        Candidates();
        if (candidates.FindIndex(c => c.Info.Path == was) is int keep and >= 0)
            chosen = keep;
        if (since is { } t && candidates.FindIndex(c => c.Info.Yours && c.Info.Modified >= t) is int i and >= 0)
        {
            int newest = i;
            for (int k = 0; k < candidates.Count; k++)
                if (candidates[k].Info.Yours && candidates[k].Info.Modified > candidates[newest].Info.Modified)
                    newest = k;
            fromYours = true;
            search = "";
            Choose(newest);
        }
    }

    void Copy(Candidate? c)
    {
        var d = c?.Info.Design ?? (c != null ? TryLoad(c.Info.Path) : null);
        if (d == null || EditCopy == null)
            return;
        copyStarted = DateTime.Now.AddSeconds(-1);
        EditCopy(d with { Name = c!.Info.Title + " copy" });
    }

    static Design? TryLoad(string path)
    {
        try
        {
            return Design.Load(path);
        }
        catch (Exception x) when (x is IOException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ the frame

    /// <summary>The picker, a window of its own over the columns right of the ships (so it takes the clicks there).</summary>
    void PickerUi(Vector2 at, Vector2 size)
    {
        if (pickerRow is not { } row || current == null)
            return;
        while (answered.TryDequeue(out var a))
            designIds[a.Path] = a.Id;
        if (candidates.Count == 0 && DesignsRead)
            Candidates();
        if (candsDirty)
            Filter();
        ImGui.SetNextWindowPos(at);
        ImGui.SetNextWindowSize(size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Bg);
        bool open = ImGui.Begin("##picker", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                                            ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
        if (open)
        {
            dl = ImGui.GetWindowDrawList();
            Picker(row, at, size);
        }
        ImGui.End();
    }

    void Picker(ShipLine row, Vector2 o, Vector2 size)
    {
        float x0 = o.X, x1 = o.X + size.X, y1 = o.Y + size.Y;
        dl.AddRectFilledMultiColor(o, o + size, U(Hex(0x101B24)), U(Hex(0x101B24)), U(Bg), U(Bg));
        VLine(x0, o.Y, y1);
        float x = x0 + 20 * sc, y = o.Y + 16 * sc;
        Txt(UiFonts.Title, 17, new Vector2(x, y), Bright, pickerTitle);
        Txt(UiFonts.SansSmall, 11.5f, new Vector2(x, y + 24 * sc), Muted, adding ? AddSub : cmp.Names);
        // close
        var cb = new Vector2(x1 - 66 * sc, y);
        if (Button("##pclose", "     Esc", cb, new Vector2(48 * sc, 22 * sc), px: 11))
        {
            ClosePicker();
            return;
        }
        var xc = cb + new Vector2(13 * sc, 11 * sc);
        dl.AddLine(xc - new Vector2(4, 4) * sc, xc + new Vector2(4, 4) * sc, U(Text), 1.2f * sc);
        dl.AddLine(xc + new Vector2(-4, 4) * sc, xc + new Vector2(4, -4) * sc, U(Text), 1.2f * sc);

        // in the add mode, the new unit's name, ships, formation and where it goes
        float formH = adding ? AddForm(x, o.Y + 62 * sc, x1) + 10 * sc : 0;
        // the filters: a search, the type, the sources, the year
        float fy = o.Y + 62 * sc + formH, fx = x;
        ImGui.SetCursorScreenPos(new Vector2(fx, fy));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0, 0, 0, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.Border, EdgeC);
        ImGui.PushStyleColor(ImGuiCol.Text, Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 3) * sc);
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.SetNextItemWidth(180 * sc);
        if (ImGui.InputTextWithHint("##psearch", "name, calibre, year…", ref search, 64))
            candsDirty = true;
        ImGui.PopFont();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
        fx += 190 * sc;
        if (!adding)
        {
            if (Toggle("##ptype", typeChip, ref fx, fy, sameType))
                (sameType, candsDirty) = (true, true);
            if (Toggle("##pany", "ANY TYPE", ref fx, fy, !sameType))
                (sameType, candsDirty) = (false, true);
            fx += 8 * sc;
        }
        if (Toggle("##pscen", "SCENARIO'S", ref fx, fy, fromScenario))
            (fromScenario, candsDirty) = (!fromScenario, true);
        if (Toggle("##pyours", "YOURS", ref fx, fy, fromYours))
            (fromYours, candsDirty) = (!fromYours, true);
        if (Toggle("##pship", "SHIPPED", ref fx, fy, fromShipped))
            (fromShipped, candsDirty) = (!fromShipped, true);
        fx += 8 * sc;
        if (Toggle("##pyear", yearChip, ref fx, fy, upToYear))
            (upToYear, candsDirty) = (!upToYear, true);
        // what the matchup is of, in the room left (none for a unit added: it meets whom it meets)
        if (!adding)
            FoeReady(row);
        float mw = x1 - 20 * sc - (fx + 12 * sc);
        if (!adding && matchupLine.Length > 0 && mw > 40 * sc)
        {
            if (!ReferenceEquals(matchupFor, matchupLine) || Math.Abs(mw - matchupWidth) > 0.5f)
                (matchupFor, matchupWidth, matchupShown) = (matchupLine, mw, Clip(UiFonts.SansSmall, 11, matchupLine, mw));
            RightSmall(matchupShown, x1 - 20 * sc, fy + 3 * sc, Muted, 11);
            ImGui.SetCursorScreenPos(new Vector2(x1 - 20 * sc - Width(UiFonts.SansSmall, 11, matchupShown), fy));
            ImGui.InvisibleButton("##pmatchup", new Vector2(Width(UiFonts.SansSmall, 11, matchupShown), 20 * sc));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(MatchupHelp);
        }

        // the list's columns, from the right
        float cMatch = x1 - 30 * sc, cBelt = x1 - 112 * sc, cMain = cBelt - 70 * sc, cSpeed = cMain - 96 * sc, cDispl = cSpeed - 68 * sc, cName = x0 + 208 * sc;
        float hy = o.Y + 100 * sc + formH;
        Tracked(UiFonts.Sans, 10, new Vector2(cName, hy), Muted, "DESIGN", 0.8f);
        RightTracked("DISPL.", cDispl, hy);
        RightTracked("SPEED", cSpeed, hy);
        RightTracked("MAIN", cMain, hy);
        RightTracked("BELT MM", cBelt, hy);
        if (!adding)
            RightTracked("MATCHUP", cMatch, hy);
        double inUse = adding ? double.NaN : MatchupOf(row, ClassOf(row.Design))?.Value ?? double.NaN;
        HLine(x0 + 12 * sc, x1 - 12 * sc, hy + 18 * sc);

        float footH = 50 * sc, compareH = adding ? 0 : 270 * sc;
        float listTop = hy + 22 * sc, listBottom = y1 - footH - compareH;
        // to scale, the row's own ship fitting the room (a longer one is cut at its cell's edge: every design is listed,
        // the silly ones too)
        float rowLength = adding ? 200 : ClassOf(row.Design) is int rid and >= 0 && world.Classes.TryGet(rid) is { } rc ? (float)rc.Motion.LengthM : 0;
        float pxPerM = rowLength > 0 ? Math.Min(1.0f * sc, 168 * sc / rowLength) : sc;

        ImGui.SetCursorScreenPos(new Vector2(x0 + 1, listTop));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        int use = -1;
        bool copyRow = false;
        if (ImGui.BeginChild("##pcands", new Vector2(size.X - 2, listBottom - listTop), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            var keep = dl;
            dl = ImGui.GetWindowDrawList();
            float ry = ImGui.GetCursorScreenPos().Y, rh = 40 * sc;
            if (!DesignsRead)
                Txt(UiFonts.SansSmall, 12, new Vector2(cName, ry + 8 * sc), Muted, "reading the designs…");
            foreach (int i in shownCands)
            {
                var c = candidates[i];
                var a = new Vector2(x0 + 12 * sc, ry);
                ImGui.SetCursorScreenPos(a);
                ImGui.PushID(i);
                if (ImGui.InvisibleButton("##cand", new Vector2(x1 - x0 - 34 * sc, rh)))
                    Choose(i);
                ImGui.PopID();
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && !adding)
                    use = i;
                bool on = i == chosen, hover = ImGui.IsItemHovered();
                if (on || hover)
                    dl.AddRectFilled(a, a + new Vector2(x1 - x0 - 34 * sc, rh), U(on ? Brass with { W = 0.08f } : new Vector4(1, 1, 1, 0.03f)));
                if (on)
                    dl.AddRectFilled(a, a + new Vector2(3 * sc, rh), U(Brass));
                HLine(a.X, x1 - 22 * sc, ry + rh);
                int id = IdOf(c);
                dl.PushClipRect(new Vector2(x0 + 14 * sc, ry), new Vector2(cName - 10 * sc, ry + rh), true);
                Thumb(id, new Vector2(x0 + 100 * sc, ry + rh / 2), pxPerM);
                dl.PopClipRect();
                float tx = cName;
                Txt(UiFonts.Sans, 12.5f, new Vector2(tx, ry + 5 * sc), Bright, c.Info.Title);
                tx += Width(UiFonts.Sans, 12.5f, c.Info.Title) + 8 * sc;
                Chip(new Vector2(tx, ry + 6 * sc), c.Chip, c.Info.Yours ? BrassBg : AiBg, c.Info.Yours ? Brass : Hex(0x9DBFE6));
                if (c.InUse)
                    Chip(new Vector2(tx + TrackedWidth(UiFonts.SansBold, 10, c.Chip, 0.5f) + 16 * sc, ry + 6 * sc), "IN USE", Hex(0x1E3320), Ok);
                if (c.Sub.Length > 0)
                    Txt(UiFonts.SansSmall, 11, new Vector2(cName, ry + 22 * sc), Muted, c.Sub);
                if (FiguresOf(id) is { } f)
                {
                    RightMono(f.Tonnes, cDispl, ry + 12 * sc);
                    RightMono(f.Knots, cSpeed, ry + 12 * sc);
                    RightMono(f.Main, cMain, ry + 12 * sc);
                    RightMono(f.Belt, cBelt, ry + 12 * sc);
                    if (!adding && MatchupOf(row, id) is { } m)
                        RightMono(m.Text, cMatch, ry + 12 * sc, c.InUse || !(Math.Abs(m.Value / inUse - 1) > 0.02) ? Text : m.Value > inUse ? Ok : Hostile);
                }
                else if (id >= 0 && world.Classes.IsReady(id) && world.Classes.ErrorOf(id) != null)
                    RightSmall("didn't build", cBelt, ry + 12 * sc, Hostile);
                else
                    RightSmall("building…", cDispl, ry + 12 * sc, Muted);
                ry += rh;
            }
            if (DesignsRead && shownCands.Count == 0)
                Txt(UiFonts.SansSmall, 12, new Vector2(cName, ry + 8 * sc), Muted, "nothing listed: the filters leave none");
            // a new design from the row's
            if (!adding)
            {
                var na = new Vector2(x0 + 12 * sc, ry + 4 * sc);
                ImGui.SetCursorScreenPos(na);
                copyRow = ImGui.InvisibleButton("##pnew", new Vector2(x1 - x0 - 34 * sc, 24 * sc));
                Txt(UiFonts.SansSmall, 12, na + new Vector2(10 * sc, 4 * sc), ImGui.IsItemHovered() ? Hex(0xE6C77A) : Brass, newFrom);
                Txt(UiFonts.SansSmall, 11.5f, new Vector2(cName, na.Y + 4.5f * sc), Muted, "opens the designer on a copy; saved, it's back here with \"yours\"");
            }
            ImGui.SetCursorScreenPos(new Vector2(x0, ry + 32 * sc));
            ImGui.Dummy(new Vector2(1, 2 * sc));
            dl = keep;
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        if (use >= 0)
        {
            Choose(use);
            UseChosen();
            return;
        }
        if (copyRow)
        {
            Copy(candidates.Find(c => c.InUse) ?? new Candidate { Info = InfoOf(row.Design)! });
            return;
        }

        if (!adding)
            Compare(row, new Vector2(x0, listBottom), new Vector2(size.X, compareH));

        // the foot: a copy to the designer, cancel, use (or add)
        float by = y1 - footH + 12 * sc, bh = 26 * sc;
        HLine(x0, x1, y1 - footH);
        var pick = chosen >= 0 ? candidates[chosen] : null;
        if (Button("##pcopy", "Edit a copy in the designer…", new Vector2(x0 + 20 * sc, by), new Vector2(190 * sc, bh), enabled: pick != null, px: 11.5f))
            Copy(pick);
        if (adding)
        {
            AddFoot(pick, x0 + 222 * sc, x1, by, bh);
            return;
        }
        string useLabel = cmp.Use;
        float uw = Math.Max(120 * sc, Width(UiFonts.SansBold, 12, useLabel) + 28 * sc);
        if (Button("##puse", useLabel, new Vector2(x1 - 20 * sc - uw, by), new Vector2(uw, bh), primary: true, enabled: pick is { InUse: false }, px: 12))
            UseChosen();
        else if (Button("##pcancel", "Cancel", new Vector2(x1 - 28 * sc - uw - 76 * sc, by), new Vector2(76 * sc, bh), px: 11.5f))
            ClosePicker();
    }

    /// <summary>The comparison's strings, made when what it shows changes.</summary>
    sealed class CompareText
    {
        public (int Chosen, int Old, int New, bool OldF, bool NewF, bool Ready, bool Drawn, int Ship, float Sc, bool Judged) Key;
        public string OldHead = "", NewHead = "", ShipOnly = "", Use = "", Note = "", State = "", Names = "";
        public readonly string[] Old = new string[7], New = new string[7], Delta = new string[7];
        public string MatchupLabel = "";
    }

    readonly CompareText cmp = new();
    string matchupShown = "", matchupFor = "";
    float matchupWidth;

    const string MatchupHelp = "The commander's own judgement of a fight (docs/battle/command.md 5.1g): as many of her as the row's unit has,\n" +
                               "against the enemy unit whose ships are nearest the row's in size, each side at its doctrine's range.\n" +
                               "The share of their tonnage she puts out of the fight for the share of her own: 1 is even, more is better.\n" +
                               "Green and red: better or worse than the design in use.";
    static readonly string[] CompareRows = ["Displacement, t", "Length, m", "Speed, kn", "Main battery", "Reach, km", "Belt, mm"];

    void MakeCompare(ShipLine row, Candidate? pick, int oldId, int newId, Figures? of, Figures? nf)
    {
        var om = MatchupOf(row, oldId);
        var nm = pick is { InUse: false } ? MatchupOf(row, newId) : null;
        var key = (chosen, oldId, newId, of != null, nf != null, newId >= 0 && world.Classes.IsReady(newId), newId >= 0 && art.Get(newId) != null, scopeShip, sc,
            om != null && (nm != null || pick is not { InUse: false }));
        if (cmp.Key == key && cmp.Names.Length > 0)
            return;
        cmp.Key = key;
        cmp.Names = Clip(UiFonts.SansSmall, 11.5f, pickerNames, 760 * sc);
        cmp.OldHead = Clip(UiFonts.SansSmall, 10.5f, row.Text.ToUpperInvariant(), 94 * sc);
        cmp.NewHead = pick != null ? Clip(UiFonts.SansSmall, 10.5f, pick.Info.Title.ToUpperInvariant(), 94 * sc) : "";
        cmp.ShipOnly = row.NameList.Length > 0 ? $"{row.NameList[Math.Min(scopeShip, row.NameList.Length - 1)]} only" : "";
        cmp.Use = pick == null || pick.InUse ? "In use" : $"Use {pick.Info.Title}";
        for (int r = 0; r < 6; r++)
            (cmp.Old[r], cmp.New[r], cmp.Delta[r]) = (of != null ? Cell(of, r) : "", nf != null ? Cell(nf, r) : "", Delta(of, nf, r));
        // the matchup's row: against the enemy line, its name cut to the label's room
        cmp.MatchupLabel = Clip(UiFonts.SansSmall, 12, matchupLabel, 300 * sc);
        (cmp.Old[6], cmp.New[6]) = (om?.Text ?? "", nm?.Text ?? "");
        cmp.Delta[6] = om is { } o1 && nm is { } n1 ? (Math.Abs(n1.Value - o1.Value) < 0.005 ? "–" : (n1.Value > o1.Value ? "+" : "") + (n1.Value - o1.Value).ToString("0.00", Inv)) : "";
        cmp.Note = "";
        if (of != null && nf != null && Math.Abs(nf.Profile.LengthM - of.Profile.LengthM) >= 0.5)
        {
            double d = nf.Profile.LengthM - of.Profile.LengthM;
            cmp.Note = $"{(d > 0 ? "Longer" : "Shorter")} by {Math.Abs(d):0} m: " + (row.Unit.SpacingM > 0
                ? $"the unit keeps its {row.Unit.SpacingM:0} m from ship to ship, so its line is that much {(d > 0 ? "longer" : "shorter")}."
                : "her station is as before.");
        }
        cmp.State = pick is not { InUse: false } || newId < 0 ? ""
            : !world.Classes.IsReady(newId) ? "Its class is building."
            : world.Classes.ErrorOf(newId) is { } err ? "It didn't build, so it can't be used: " + err
            : $"Built in {world.Classes.TimingsOf(newId).Build + world.Classes.TimingsOf(newId).Compile:0.0} s; " +
              (art.Get(newId) == null ? "its art is being drawn (the map shows icons until it's in)." : "drawn.");
    }

    /// <summary>The row's design and the one picked, drawn and figured side by side; what to use it for.</summary>
    void Compare(ShipLine row, Vector2 o, Vector2 size)
    {
        float x0 = o.X, x = x0 + 20 * sc, y = o.Y + 14 * sc;
        HLine(x0, x0 + size.X, o.Y);
        var pick = chosen >= 0 ? candidates[chosen] : null;
        bool other = pick is { InUse: false };
        int oldId = ClassOf(row.Design), newId = pick != null ? IdOf(pick) : -1;
        var of = FiguresOf(oldId);
        var nf = other ? FiguresOf(newId) : null;
        MakeCompare(row, pick, oldId, newId, of, nf);
        var oc = oldId >= 0 ? world.Classes.TryGet(oldId) : null;
        var nc = other && newId >= 0 ? world.Classes.TryGet(newId) : null;
        float longest = Math.Max((float)(oc?.Motion.LengthM ?? 0), (float)(nc?.Motion.LengthM ?? 0));
        float k = longest > 0 ? Math.Min(1.6f * sc, 250 * sc / longest) : sc;
        Thumb(oldId, new Vector2(x + 128 * sc, y + 16 * sc), k);
        Txt(UiFonts.SansSmall, 12, new Vector2(x + 268 * sc, y + 8 * sc), Soft, row.Text);
        if (other)
        {
            Thumb(newId, new Vector2(x + 128 * sc, y + 58 * sc), k);
            Txt(UiFonts.SansSmall, 12, new Vector2(x + 268 * sc, y + 50 * sc), Brass, pick!.Info.Title);
        }
        // the figures, the old and the new
        float ty = y + 92 * sc, cOld = x0 + 432 * sc, cNew = x0 + 530 * sc, cDelta = x0 + 600 * sc;
        RightSmall(cmp.OldHead, cOld, ty, Muted, 10.5f);
        if (other)
        {
            RightSmall(cmp.NewHead, cNew, ty, Muted, 10.5f);
            RightSmall("CHANGE", cDelta, ty, Muted, 10.5f);
        }
        ty += 18 * sc;
        for (int r = 0; r < 7; r++, ty += 18 * sc)
        {
            if (r == 6 && cmp.MatchupLabel.Length == 0)
                break;
            Txt(UiFonts.SansSmall, 12, new Vector2(x, ty), Soft, r < 6 ? CompareRows[r] : cmp.MatchupLabel);
            RightMono(cmp.Old[r], cOld, ty, Muted);
            if (nf != null)
            {
                RightMono(cmp.New[r], cNew, ty, Bright);
                // the matchup's change is better or worse; the others are only changes
                var dc = cmp.Delta[r] == "–" ? Muted : r < 6 ? Ok : cmp.Delta[r].StartsWith('+') ? Ok : Hostile;
                RightMono(cmp.Delta[r], cDelta, ty, dc);
            }
        }

        // what it's used for
        float ux = x0 + 628 * sc, uy = y, nw = x0 + size.X - 20 * sc - ux;
        Tracked(UiFonts.Sans, 10, new Vector2(ux, uy), Muted, "USE IT FOR", 0.8f);
        uy += 22 * sc;
        if (Radio("##sc0", unitScope, new Vector2(ux, uy), scope == 0))
            scope = 0;
        uy += 21 * sc;
        if (row.NameList.Length > 0 && row.Ships.Count > 1)
        {
            if (Radio("##sc1", cmp.ShipOnly, new Vector2(ux, uy), scope == 1))
                scope = 1;
            if (row.NameList.Length > 1)
            {
                ImGui.SetCursorScreenPos(new Vector2(ux + 28 * sc + Width(UiFonts.SansSmall, 12, cmp.ShipOnly), uy - 2 * sc));
                Designer.DesignerTheme.Push();
                ImGui.PushFont(UiFonts.SansSmall);
                ImGui.SetNextItemWidth(26 * sc);
                if (ImGui.BeginCombo("##scship", "", ImGuiComboFlags.NoPreview))
                {
                    for (int n = 0; n < row.NameList.Length; n++)
                        if (ImGui.Selectable(row.NameList[n], n == scopeShip))
                            (scopeShip, scope) = (n, 1);
                    ImGui.EndCombo();
                }
                ImGui.PopFont();
                Designer.DesignerTheme.Pop();
            }
            uy += 21 * sc;
        }
        if (Radio("##sc2", everyScope, new Vector2(ux, uy), scope == 2))
            scope = 2;
        uy += 30 * sc;
        if (cmp.Note.Length > 0)
            uy += Wrapped(UiFonts.SansSmall, 11.5f, new Vector2(ux, uy), Muted, cmp.Note, nw) + 8 * sc;
        if (cmp.State.Length > 0)
            Wrapped(UiFonts.SansSmall, 11.5f, new Vector2(ux, uy), world.Classes.ErrorOf(newId) != null ? Hostile : Muted, cmp.State, nw);
    }

    static string Cell(Figures f, int r) => r switch
    {
        0 => f.Displacement,
        1 => f.Length,
        2 => f.Speed,
        3 => f.Main,
        4 => f.Reach,
        _ => f.Belt,
    };

    static string Delta(Figures? a, Figures? b, int r)
    {
        if (a == null || b == null)
            return "";
        (double x, double y, string fmt) = r switch
        {
            0 => (a.Profile.DisplacementT, b.Profile.DisplacementT, "N0"),
            1 => (a.Profile.LengthM, b.Profile.LengthM, "0"),
            2 => (a.Profile.SpeedKn, b.Profile.SpeedKn, "0"),
            4 => (a.Profile.MainReachM / 1000, b.Profile.MainReachM / 1000, "0.0"),
            5 => (a.BeltMm, b.BeltMm, "0"),
            _ => (0, 0, ""),
        };
        if (r == 3)
            return a.Main == b.Main ? "–" : "";
        string d = (y - x).ToString(fmt, Inv);
        return d.TrimStart('-').Trim('0', '.', ',').Length == 0 ? "–" : (y > x ? "+" : "") + d;
    }

    bool Toggle(string id, string label, ref float x, float y, bool on)
    {
        float w = TrackedWidth(UiFonts.Sans, 10, label, 0.4f) + 14 * sc;
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        bool hit = ImGui.InvisibleButton(id, new Vector2(w, 20 * sc));
        bool hover = ImGui.IsItemHovered();
        Box(new Vector2(x, y), new Vector2(x + w, y + 20 * sc), on ? Hex(0x2A2412) : new Vector4(1, 1, 1, hover ? 0.04f : 0), on ? BrassDark : Hex(0x3B4752), 2);
        Tracked(UiFonts.Sans, 10, new Vector2(x + 7 * sc, y + 3 * sc), on ? Brass : Muted, label, 0.4f);
        x += w + 4 * sc;
        return hit;
    }

    bool Radio(string id, string label, Vector2 at, bool on)
    {
        ImGui.SetCursorScreenPos(at - new Vector2(0, 2 * sc));
        bool hit = ImGui.InvisibleButton(id, new Vector2(22 * sc + Width(UiFonts.SansSmall, 12, label), 18 * sc));
        bool hover = ImGui.IsItemHovered();
        var c = at + new Vector2(6 * sc, 7.5f * sc);
        dl.AddCircle(c, 5.5f * sc, U(on || hover ? Brass : Muted), 16, 1.2f * sc);
        if (on)
            dl.AddCircleFilled(c, 2.8f * sc, U(Brass), 12);
        Txt(UiFonts.SansSmall, 12, at + new Vector2(20 * sc, 0), on ? Bright : Soft, label);
        return hit;
    }

    void RightTracked(string s, float right, float y) =>
        Tracked(UiFonts.Sans, 10, new Vector2(right - TrackedWidth(UiFonts.Sans, 10, s, 0.8f), y), Muted, s, 0.8f);

    void RightMono(string s, float right, float y, Vector4? col = null) =>
        Txt(UiFonts.Mono, 12, new Vector2(right - Width(UiFonts.Mono, 12, s), y), col ?? Text, s);

    void RightSmall(string s, float right, float y, Vector4 col, float px = 11.5f) =>
        Txt(UiFonts.SansSmall, px, new Vector2(right - Width(UiFonts.SansSmall, px, s), y), col, s);
}
