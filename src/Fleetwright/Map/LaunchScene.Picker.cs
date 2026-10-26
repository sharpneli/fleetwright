using System.Collections.Concurrent;
using System.Numerics;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Missions;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

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
        pickerNames = string.Join(", ", row.NameList.Take(row.Ships.Count));
        pickerYear = current!.Start.ToUtc().Year;
        typeChip = TypeName(row.Type);
        yearChip = $"up to {pickerYear}";
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
        "BB" => "Battleships",
        "BC" => "Battlecruisers",
        "CA" => "Armoured cruisers",
        "CL" => "Light cruisers",
        "DD" => "Destroyers",
        "" => "Same type",
        _ => type + "s",
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
            c.Chip = d.Yours ? "yours" : c.Scenario ? "scenario" : "shipped";
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
        string? unit = scope == 0 ? setup.NameAsMade(row.Unit.Name) : null, ship = scope == 1 && row.NameList.Length > 0 ? row.NameList[Math.Min(scopeShip, row.NameList.Length - 1)] : null;
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

    bool pickerEnter;   // Enter pressed with the picker open: use (or add) the design picked

    /// <summary>The row's design as a candidate (for a copy of it), whether or not the picker is open.</summary>
    Candidate? InUseCandidate(ShipLine l) => InfoOf(l.Design) is { } d ? new Candidate { Info = d, InUse = true } : null;

    static float PickRowH => P(26);

    /// <summary>The picker, a window of its own over the centre column (so it takes the clicks there): the panes it
    /// replaces while it's open.</summary>
    void PickerUi(Vector2 at, Vector2 size)
    {
        bool enter = pickerEnter;
        pickerEnter = false;
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
        bool open = ImGui.Begin("##picker", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                                            ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar();
        if (open)
        {
            dl = ImGui.GetWindowDrawList();
            ImGui.PushFont(UiFonts.Sans);
            Picker(row, at, size, enter);
            ImGui.PopFont();
        }
        ImGui.End();
    }

    void Picker(ShipLine row, Vector2 o, Vector2 size, bool enter)
    {
        float x0 = o.X, x1 = o.X + size.X, y1 = o.Y + size.Y, w = size.X;
        float right = dl.Pane(o, size, adding ? "ADD A UNIT" : "CHANGE DESIGN", pickerTitle);
        float cw = ButtonWidth("Close", "Esc");
        if (dl.Button("##pclose", new Vector2(right - cw, o.Y + P(1)), "Close", "Esc"))
        {
            ClosePicker();
            return;
        }
        float y = o.Y + TitleH + 1;

        // in the add mode, the new unit's name, ships, formation and where it goes
        if (adding)
            y = AddForm(x0 + 1, y, w - 2);

        // the filters: a search, the type, the sources, the year; what the matchup is of at the right
        float fy = y;
        dl.AddRectFilled(new Vector2(x0 + 1, fy), new Vector2(x1 - 1, fy + P(24)), U(T.HeaderBg));
        dl.HLine(x0 + 1, x1 - 1, fy + P(24) - 1, T.Line);
        float fx = x0 + P(7), cy = fy + P(4);
        ImGui.SetCursorScreenPos(new Vector2(fx, fy + P(3)));
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(5), P(1)));
        ImGui.SetNextItemWidth(P(180));
        if (ImGui.InputTextWithHint("##psearch", "name, calibre, year…", ref search, 64))
            candsDirty = true;
        ImGui.PopStyleVar();
        ImGui.PopFont();
        fx += P(188);
        if (!adding)
        {
            typeLabels[0] = typeChip;
            int t = dl.Segmented("##ptype", new Vector2(fx, cy), typeLabels, sameType ? 0 : 1, out float tw);
            if (t >= 0)
                (sameType, candsDirty) = (t == 0, true);
            fx += tw + P(10);
        }
        if (dl.Flag("##pscen", ref fx, cy, "scenario's", fromScenario))
            (fromScenario, candsDirty) = (!fromScenario, true);
        if (dl.Flag("##pyours", ref fx, cy, "yours", fromYours))
            (fromYours, candsDirty) = (!fromYours, true);
        if (dl.Flag("##pship", ref fx, cy, "shipped", fromShipped))
            (fromShipped, candsDirty) = (!fromShipped, true);
        fx += P(6);
        if (dl.Flag("##pyear", ref fx, cy, yearChip, upToYear))
            (upToYear, candsDirty) = (!upToYear, true);
        if (!adding)
            FoeReady(row);
        float mw = x1 - P(8) - (fx + P(12));
        if (!adding && matchupLine.Length > 0 && mw > P(40))
        {
            if (!ReferenceEquals(matchupFor, matchupLine) || Math.Abs(mw - matchupWidth) > 0.5f)
                (matchupFor, matchupWidth, matchupShown) = (matchupLine, mw, Clip(UiFonts.SansSmall, matchupLine, mw));
            float mx = dl.RightNote(x1 - P(8), fy, P(24), matchupShown);
            if (ImGui.IsMouseHoveringRect(new Vector2(mx, fy), new Vector2(x1, fy + P(24))))
                ImGui.SetTooltip(MatchupHelp);
        }
        y = fy + P(24);

        // the list's columns, from the right
        float cMatch = x1 - P(12), cBelt = cMatch - P(78), cMain = cBelt - P(60), cSpeed = cMain - P(84), cDispl = cSpeed - P(52);
        float cName = x0 + P(196), cNote = cName + P(250);
        var h = new Vector2(x0 + 1, y);
        dl.Head(h, w - 2);
        dl.HeadCell(h, x0 + P(10), "TO SCALE");
        dl.HeadCell(h, cName, "DESIGN");
        dl.HeadCell(h, cNote, "NOTE");
        dl.HeadCell(h, cDispl, "T", right: true);
        dl.HeadCell(h, cSpeed, "KN", right: true);
        dl.HeadCell(h, cMain, "MAIN", right: true);
        dl.HeadCell(h, cBelt, "BELT", right: true);
        if (!adding)
            dl.HeadCell(h, cMatch, "MATCHUP", right: true);
        y += HeadH;
        double inUse = adding ? double.NaN : MatchupOf(row, ClassOf(row.Design))?.Value ?? double.NaN;

        float footH = P(32), compareH = adding ? 0 : MathF.Round(Math.Clamp(size.Y * 0.3f, P(200), P(250)));
        float listTop = y, listBottom = y1 - footH - compareH;
        // to scale, the row's own ship fitting the room (a longer one is cut at its cell's edge: every design is listed,
        // the silly ones too)
        float rowLength = adding ? 200 : ClassOf(row.Design) is int rid and >= 0 && world.Classes.TryGet(rid) is { } rc ? (float)rc.Motion.LengthM : 0;
        float pxPerM = rowLength > 0 ? Math.Min(P(1), P(164) / rowLength) : P(1);

        ImGui.SetCursorScreenPos(new Vector2(x0 + 1, listTop));
        int use = -1;
        bool copyRow = false;
        var keep = dl;
        if (ImGui.BeginChild("##pcands", new Vector2(w - 2, listBottom - listTop), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            dl = ImGui.GetWindowDrawList();
            var lo = ImGui.GetCursorScreenPos();
            float lw = ImGui.GetContentRegionAvail().X, shift = lw - (w - 2);
            float ry = lo.Y, rh = PickRowH, top = ImGui.GetWindowPos().Y, bottom = top + listBottom - listTop;
            if (!DesignsRead)
                dl.Text(UiFonts.SansSmall, new Vector2(cName, Mid(UiFonts.SansSmall, ry, RowH)), T.Muted, "reading the designs…");
            int n = 0;
            foreach (int i in shownCands)
            {
                var c = candidates[i];
                var a = new Vector2(lo.X, ry);
                if (ry + rh < top || ry > bottom)
                {
                    ry += rh;
                    n++;
                    continue;
                }
                ImGui.PushID(i);
                var (click, hover) = HitRow("##cand", a, new Vector2(lw, rh));
                ImGui.PopID();
                if (click)
                    Choose(i);
                if (hover && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && !adding)
                    use = i;
                dl.RowBg(a, lw, rh, n++, i == chosen, hover);
                int id = IdOf(c);
                dl.PushClipRect(new Vector2(a.X + P(6), ry), new Vector2(cName - P(10), ry + rh), true);
                Thumb(id, new Vector2(x0 + P(96), ry + rh / 2), pxPerM);
                dl.PopClipRect();
                float tx = cName, ty = Mid(UiFonts.Sans, ry, rh);
                dl.PushClipRect(new Vector2(tx, ry), new Vector2(cNote - P(8), ry + rh), true);
                dl.Text(UiFonts.Sans, new Vector2(tx, ty), i == chosen ? T.Brass : T.Bright, c.Info.Title);
                tx += Width(UiFonts.Sans, c.Info.Title) + P(8);
                tx += dl.Chip(new Vector2(tx, ry + (rh - P(15)) / 2), c.Chip, c.Info.Yours ? ChipKind.Amber : ChipKind.Blue) + P(4);
                if (c.InUse)
                    dl.Chip(new Vector2(tx, ry + (rh - P(15)) / 2), "in use", ChipKind.Ok);
                dl.PopClipRect();
                float figs = cDispl + shift - P(64);
                if (c.Sub.Length > 0)
                {
                    dl.PushClipRect(new Vector2(cNote, ry), new Vector2(figs, ry + rh), true);
                    dl.Text(UiFonts.SansSmall, new Vector2(cNote, Mid(UiFonts.SansSmall, ry, rh)), T.Muted, c.Sub);
                    dl.PopClipRect();
                }
                float my = Mid(UiFonts.Mono, ry, rh);
                if (FiguresOf(id) is { } f)
                {
                    dl.TextRight(UiFonts.Mono, cDispl + shift, my, T.Bright, f.Displacement);
                    dl.TextRight(UiFonts.Mono, cSpeed + shift, my, T.Bright, f.Speed);
                    dl.TextRight(UiFonts.Mono, cMain + shift, my, T.Text, f.Main);
                    dl.TextRight(UiFonts.Mono, cBelt + shift, my, T.Bright, f.Belt);
                    if (!adding && MatchupOf(row, id) is { } m)
                        dl.TextRight(UiFonts.Mono, cMatch + shift, my, c.InUse || !(Math.Abs(m.Value / inUse - 1) > 0.02) ? T.Text : m.Value > inUse ? T.Ok : T.Amber, m.Text);
                }
                else if (id >= 0 && world.Classes.IsReady(id) && world.Classes.ErrorOf(id) != null)
                    dl.TextRight(UiFonts.SansSmall, cBelt + shift, Mid(UiFonts.SansSmall, ry, rh), T.Amber, "didn't build");
                else
                    dl.TextRight(UiFonts.SansSmall, cDispl + shift, Mid(UiFonts.SansSmall, ry, rh), T.Muted, "building…");
                ry += rh;
            }
            if (DesignsRead && shownCands.Count == 0)
            {
                dl.Text(UiFonts.SansSmall, new Vector2(cName, Mid(UiFonts.SansSmall, ry, RowH)), T.Muted, "nothing listed: the filters leave none");
                ry += RowH;
            }
            // a new design from the row's: a brass row at the end
            if (!adding)
            {
                var na = new Vector2(lo.X, ry);
                var (click, hover) = HitRow("##pnew", na, new Vector2(lw, RowH));
                copyRow = click;
                dl.RowBg(na, lw, RowH, n, hover: hover);
                dl.Text(UiFonts.Sans, new Vector2(na.X + P(10), Mid(UiFonts.Sans, na.Y, RowH)), hover ? T.Bright : T.Brass, newFrom);
                dl.Text(UiFonts.SansSmall, new Vector2(cNote, Mid(UiFonts.SansSmall, na.Y, RowH)), T.Muted, "opens the designer on a copy; saved, it's back here as yours");
                ry += RowH;
            }
            ImGui.SetCursorPos(new Vector2(0, ry + P(4) - lo.Y));
            ImGui.Dummy(new Vector2(1, 0));
        }
        ImGui.EndChild();
        dl = keep;
        if (use >= 0)
        {
            Choose(use);
            UseChosen();
            return;
        }
        if (copyRow)
        {
            Copy(candidates.Find(c => c.InUse) ?? InUseCandidate(row));
            return;
        }

        if (!adding)
            Compare(row, new Vector2(x0 + 1, listBottom), new Vector2(w - 2, compareH));

        // the foot: a copy to the designer, cancel, use (or add)
        float fby = y1 - footH;
        dl.AddRectFilled(new Vector2(x0 + 1, fby), new Vector2(x1 - 1, y1 - 1), U(T.Panel2));
        dl.HLine(x0 + 1, x1 - 1, fby, T.Edge);
        float by = fby + P(6);
        var pick = chosen >= 0 ? candidates[chosen] : null;
        const string CopyL = "Edit a copy in the designer…";
        if (dl.Button("##pcopy", new Vector2(x0 + P(8), by), CopyL, "E", enabled: pick != null && EditCopy != null) ||
            pick != null && !ImGui.GetIO().WantTextInput && ImGui.IsKeyPressed(ImGuiKey.E) && !ImGui.GetIO().KeyCtrl)
            Copy(pick);
        if (adding)
        {
            AddFoot(pick, x0 + P(16) + ButtonWidth(CopyL, "E"), x1 - P(8), by, enter);
            return;
        }
        string useLabel = cmp.Use;
        float uw = ButtonWidth(useLabel, "Enter", primary: true);
        bool canUse = pick is { InUse: false };
        if (dl.Button("##puse", new Vector2(x1 - P(8) - uw, by), useLabel, "Enter", primary: true, enabled: canUse) || enter && canUse)
            UseChosen();
        else if (dl.Button("##pcancel", new Vector2(x1 - P(12) - uw - ButtonWidth("Cancel", "Esc"), by), "Cancel", "Esc"))
            ClosePicker();
    }

    readonly string[] typeLabels = ["", "Any type"];

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
                               "Green and amber: better or worse than the design in use.";
    static readonly string[] CompareRows = ["Displacement, t", "Length, m", "Speed, kn", "Main battery", "Reach, km", "Belt, mm"];

    void MakeCompare(ShipLine row, Candidate? pick, int oldId, int newId, Figures? of, Figures? nf)
    {
        var om = MatchupOf(row, oldId);
        var nm = pick is { InUse: false } ? MatchupOf(row, newId) : null;
        var key = (chosen, oldId, newId, of != null, nf != null, newId >= 0 && world.Classes.IsReady(newId), newId >= 0 && art.Get(newId) != null, scopeShip, UiFonts.Scale,
            om != null && (nm != null || pick is not { InUse: false }));
        if (cmp.Key == key && cmp.Use.Length > 0)
            return;
        cmp.Key = key;
        cmp.Names = pickerNames;
        cmp.OldHead = Clip(UiFonts.SansSmall, row.Text.ToUpperInvariant(), P(94));
        cmp.NewHead = pick != null ? Clip(UiFonts.SansSmall, pick.Info.Title.ToUpperInvariant(), P(94)) : "";
        cmp.ShipOnly = row.NameList.Length > 0 ? $"{row.NameList[Math.Min(scopeShip, row.NameList.Length - 1)]} only" : "";
        cmp.Use = pick == null || pick.InUse ? "In use" : $"Use {pick.Info.Title}";
        for (int r = 0; r < 6; r++)
            (cmp.Old[r], cmp.New[r], cmp.Delta[r]) = (of != null ? Cell(of, r) : "", nf != null ? Cell(nf, r) : "", Delta(of, nf, r));
        // the matchup's row: against the enemy line, its name cut to the label's room
        cmp.MatchupLabel = Clip(UiFonts.Sans, matchupLabel, P(170));
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
        float w = size.X;
        var pick = chosen >= 0 ? candidates[chosen] : null;
        bool other = pick is { InUse: false };
        int oldId = ClassOf(row.Design), newId = pick != null ? IdOf(pick) : -1;
        var of = FiguresOf(oldId);
        var nf = other ? FiguresOf(newId) : null;
        MakeCompare(row, pick, oldId, newId, of, nf);
        dl.AddLine(new Vector2(o.X, o.Y), new Vector2(o.X + w, o.Y), U(T.Edge));
        dl.SubHeader(o + new Vector2(0, 1), w, "COMPARED", "the design in use over the one picked");
        float y = o.Y + 1 + RowH;
        float drawW = MathF.Round(w * 0.30f), tableW = MathF.Round(w * 0.38f), useW = w - drawW - tableW;
        float bodyH = size.Y - RowH - 1;

        // the two drawn to scale on the plan ground, the old above the new
        var p0 = new Vector2(o.X, y);
        dl.AddRectFilled(p0, p0 + new Vector2(drawW, bodyH), U(T.Plan));
        var oc = oldId >= 0 ? world.Classes.TryGet(oldId) : null;
        var nc = other && newId >= 0 ? world.Classes.TryGet(newId) : null;
        float longest = Math.Max((float)(oc?.Motion.LengthM ?? 0), (float)(nc?.Motion.LengthM ?? 0));
        float k = longest > 0 ? Math.Min(P(1.6f), (drawW - P(24)) / longest) : P(1);
        float cx = p0.X + drawW / 2;
        Thumb(oldId, new Vector2(cx, p0.Y + bodyH * 0.32f), k);
        dl.Tag(new Vector2(p0.X + P(8), p0.Y + P(8)), row.Text);
        if (other)
        {
            Thumb(newId, new Vector2(cx, p0.Y + bodyH * 0.74f), k);
            dl.Tag(new Vector2(p0.X + P(8), p0.Y + bodyH * 0.52f), pick!.Info.Title, T.Brass);
        }
        dl.TextRight(UiFonts.MonoSmall, p0.X + drawW - P(8), p0.Y + bodyH - P(16), T.PlanLine, "to scale");

        // the figures, the old and the new
        float tx = o.X + drawW;
        dl.AddLine(new Vector2(tx, y), new Vector2(tx, y + bodyH), U(T.Edge));
        var h = new Vector2(tx + 1, y);
        float cOld = tx + tableW - P(150), cNew = tx + tableW - P(72), cDelta = tx + tableW - P(8);
        dl.Head(h, tableW - 1);
        dl.HeadCell(h, tx + P(6), "PARTICULAR");
        dl.HeadCell(h, cOld, cmp.OldHead, right: true);
        if (other)
        {
            dl.HeadCell(h, cNew, cmp.NewHead, right: true);
            dl.HeadCell(h, cDelta, "CHANGE", right: true);
        }
        float ty = y + HeadH;
        for (int r = 0; r < 7; r++, ty += RowH)
        {
            if (r == 6 && cmp.MatchupLabel.Length == 0)
                break;
            var ra = new Vector2(tx + 1, ty);
            dl.RowBg(ra, tableW - 1, RowH, r);
            dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Muted, r < 6 ? CompareRows[r] : cmp.MatchupLabel);
            float my = Mid(UiFonts.Mono, ra.Y, RowH);
            dl.TextRight(UiFonts.Mono, cOld, my, T.Text, cmp.Old[r]);
            if (nf != null)
            {
                dl.TextRight(UiFonts.Mono, cNew, my, T.Bright, cmp.New[r]);
                // the matchup's change is better or worse; the others are only changes
                var dc = cmp.Delta[r] == "–" || r < 6 ? T.Muted : cmp.Delta[r].StartsWith('+') ? T.Ok : T.Amber;
                dl.TextRight(UiFonts.Mono, cDelta, my, dc, cmp.Delta[r]);
            }
        }

        // what it's used for
        float ux = tx + tableW;
        dl.AddLine(new Vector2(ux, y), new Vector2(ux, y + bodyH), U(T.Edge));
        h = new Vector2(ux + 1, y);
        dl.Head(h, useW - 1);
        dl.HeadCell(h, ux + P(6), "USE IT FOR");
        float uy = y + HeadH;
        int un = 0;
        if (ScopeRow("##sc0", new Vector2(ux + 1, uy), useW - 1, unitScope, scope == 0, un++))
            scope = 0;
        uy += RowH;
        if (row.NameList.Length > 0 && row.Ships.Count > 1)
        {
            if (ScopeRow("##sc1", new Vector2(ux + 1, uy), useW - 1, cmp.ShipOnly, scope == 1, un++))
                scope = 1;
            if (row.NameList.Length > 1)
            {
                int ship = dl.DropDown("##scship", new Vector2(ux + useW - P(130), uy + P(2)), P(122), row.NameList[Math.Min(scopeShip, row.NameList.Length - 1)], row.NameList);
                if (ship >= 0)
                    (scopeShip, scope) = (ship, 1);
            }
            uy += RowH;
        }
        if (ScopeRow("##sc2", new Vector2(ux + 1, uy), useW - 1, everyScope, scope == 2, un++))
            scope = 2;
        uy += RowH + P(6);
        float nw = useW - P(16);
        if (cmp.Note.Length > 0)
            uy += dl.Wrapped(UiFonts.SansSmall, new Vector2(ux + P(8), uy), T.Muted, cmp.Note, nw) + P(6);
        if (cmp.State.Length > 0)
            dl.Wrapped(UiFonts.SansSmall, new Vector2(ux + P(8), uy), newId >= 0 && world.Classes.ErrorOf(newId) != null ? T.Amber : T.Muted, cmp.State, nw);
    }

    /// <summary>A row of "use it for": a radio mark and its words; true when clicked.</summary>
    bool ScopeRow(string id, Vector2 a, float w, string label, bool on, int n)
    {
        var (click, hover) = HitRow(id, a, new Vector2(w, RowH));
        dl.RowBg(a, w, RowH, n, on, hover);
        dl.Radio(new Vector2(a.X + P(14), a.Y + RowH / 2), on);
        dl.Text(UiFonts.Sans, new Vector2(a.X + P(26), Mid(UiFonts.Sans, a.Y, RowH)), on ? T.Brass : T.Text, label);
        return click;
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
}
