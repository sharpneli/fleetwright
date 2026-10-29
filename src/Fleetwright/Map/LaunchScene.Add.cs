using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>Adding a unit (docs/map-ui/launch.md L3b, the mockup's <c>#add</c> and <c>#place</c>): the design picker in
/// its add mode names the new unit, how many of the design picked, its formation and spacing, and whether it joins a
/// force (on its station there: no placing) or stands on its own; a table of rows over the picker's list. On its own it's
/// placed in the placing view, the opening with a bar over it: the unit at the cursor with its ships astern and half an
/// hour's course, a press places her head (not on land), a drag sets the course; the course and speed stepped in the bar,
/// beside where she is and how far from her own and from the enemy. Either way it's an <see cref="AddUnit"/> of the
/// setup; one on its own can be placed again (its "move"). No matchup: an added unit meets whom it meets (the scenario's
/// maker places, the user 2026-10-25).</summary>
public sealed unsafe partial class LaunchScene
{
    bool adding;
    int addSide, addCount = 4, addFormation, addSpacing = 600, addJoins;
    string addName = "", addCountText = "4", addSpacingText = "600 m";
    readonly List<(string Shown, string Made)> joinable = [];   // the side's units: as shown, and as the changes name them
    string[] joinLabels = [];
    HashSet<string> unitNames = [];   // every unit's in the pick: a new one's is new
    (int, int, bool, int) addNoteKey = (-2, 0, false, 0);
    string addNote = "";
    static readonly Formation[] AddFormations = [Formation.Column, Formation.LineAbreast, Formation.EchelonStarboard, Formation.EchelonPort];
    static readonly string[] AddFormationNames = ["line ahead", "line abreast", "echelon to starboard", "echelon to port"];

    /// <summary>The picker in its add mode, for a side; joining a unit (as shown) to start with, or on its own.</summary>
    void OpenAdd(int sd, string? joins = null)
    {
        if (current is not { } s)
            return;
        ClosePicker();
        placing = null;
        adding = true;
        addSide = sd;
        (scope, scopeShip, search, chosen, sameType) = (0, 0, "", -1, false);
        joinable.Clear();
        unitNames = TakenNames();
        void Walk(ScenarioUnit u)
        {
            if (u.Side == sd && !u.Removed)
                joinable.Add((u.Name, setup.NameAsMade(u.Name)));
            foreach (var x in u.Units)
                Walk(x);
        }
        foreach (var u in s.Units)
            Walk(u);
        joinLabels = ["on its own: placed on the opening", .. joinable.Select(j => $"{j.Shown}: on its station")];
        addName = "New squadron";
        for (int k = 2; unitNames.Contains(addName); k++)
            addName = $"New squadron {k}";
        (addCount, addCountText, addFormation, addSpacing, addSpacingText, addJoins) = (4, "4", 0, 600, "600 m", joinable.FindIndex(j => j.Shown == joins) + 1);
        pickerTitle = s.Sides[sd];
        pickerYear = s.Start.ToUtc().Year;
        yearChip = $"up to {pickerYear}";
        pickerRow = new ShipLine { Side = sd, Unit = new ScenarioUnit { Name = "", Side = sd }, Ships = new ScenarioShips { Design = "" } };
        Candidates();
    }

    /// <summary>The add mode's form: the name, the ships, the formation and spacing, where it goes, as a table's rows.
    /// Returns where it ends.</summary>
    float AddForm(float x0, float y, float w)
    {
        dl.SubHeader(new Vector2(x0, y), w, "THE NEW UNIT", "one design; more come with Change design… and the steppers once it's in");
        y += RowH;
        float c0 = x0 + P(96);
        int n = 0;
        Vector2 Row(string label)
        {
            var ra = new Vector2(x0, y);
            dl.RowBg(ra, w, RowH, n++);
            dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Text, label);
            y += RowH;
            return ra;
        }
        var r = Row("Name");
        ImGui.SetCursorScreenPos(new Vector2(c0, r.Y + P(2)));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(5), P(1)));
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.SetNextItemWidth(P(260));
        ImGui.InputText("##addname", ref addName, 64);
        ImGui.PopFont();
        ImGui.PopStyleVar();
        r = Row("Ships");
        float sw = dl.Stepper("##addn", new Vector2(c0, r.Y + P(2.5f)), addCountText, P(30), addCount > 1, addCount < 99, out int by);
        if (by != 0)
        {
            addCount = Math.Clamp(addCount + by, 1, 99);
            addCountText = addCount.ToString(Inv);
        }
        dl.Text(UiFonts.SansSmall, new Vector2(c0 + sw + P(8), Mid(UiFonts.SansSmall, r.Y, RowH)), T.Muted, "of the design picked below, unnamed");
        r = Row("Formation");
        int f = dl.DropDown("##addform", new Vector2(c0, r.Y + P(2)), P(170), AddFormationNames[addFormation], AddFormationNames);
        if (f >= 0)
            addFormation = f;
        r = Row("Spacing");
        sw = dl.Stepper("##addsp", new Vector2(c0, r.Y + P(2.5f)), addSpacingText, P(60), addSpacing > 100, addSpacing < 20_000, out by);
        if (by != 0)
        {
            addSpacing = Math.Clamp(addSpacing + 100 * by, 100, 20_000);
            addSpacingText = $"{addSpacing} m";
        }
        dl.Text(UiFonts.SansSmall, new Vector2(c0 + sw + P(8), Mid(UiFonts.SansSmall, r.Y, RowH)), T.Muted, "from ship to ship");
        r = Row("Joins");
        float jw = Math.Min(P(420), w - (c0 - x0) - P(12));
        int j = dl.DropDown("##addjoins", new Vector2(c0, r.Y + P(2)), jw, joinLabels[addJoins], joinLabels);
        if (j >= 0)
            addJoins = j;
        if (addJoins > 0)
            dl.Text(UiFonts.SansSmall, new Vector2(c0 + jw + P(8), Mid(UiFonts.SansSmall, r.Y, RowH)), T.Muted, "a squadron joined becomes a force");
        return y;
    }

    /// <summary>The add mode's foot: what it adds, cancel, and place it (or add it, joining).</summary>
    void AddFoot(Candidate? pick, float x, float x1, float by, bool enter)
    {
        string name = addName.Trim();
        bool taken = unitNames.Contains(name);
        var f = pick != null ? FiguresOf(IdOf(pick)) : null;
        var key = (chosen, addCount, f != null, name.Length == 0 ? 0 : taken ? 1 : 2);
        if (key != addNoteKey)
        {
            addNoteKey = key;
            addNote = name.Length == 0 ? "The unit needs a name."
                : taken ? $"A unit \"{name}\" is in the scenario already: another name."
                : pick == null ? ""
                : f != null ? $"{addCount} × {pick.Info.Title}: {f.Profile.DisplacementT * addCount:N0} t at {f.Speed} kn."
                : $"{addCount} × {pick.Info.Title}";
        }
        string go = addJoins > 0 ? "Add it" : "Place it on the opening…";
        float gw = ButtonWidth(go, "Enter", primary: true), cw = ButtonWidth("Cancel", "Esc");
        dl.PushClipRect(new Vector2(x, by), new Vector2(x1 - gw - cw - P(16), by + P(20)), true);
        dl.Text(UiFonts.SansSmall, new Vector2(x, Mid(UiFonts.SansSmall, by, P(20))), taken || name.Length == 0 ? T.Amber : T.Muted, addNote);
        dl.PopClipRect();
        bool ok = pick != null && name.Length > 0 && !taken;
        if (dl.Button("##addgo", new Vector2(x1 - gw, by), go, "Enter", primary: true, enabled: ok) || enter && ok)
        {
            var unit = new ScenarioUnit { Name = name, Side = addSide, Formation = AddFormations[addFormation], SpacingM = addSpacing };
            unit.Ships.Add(new ScenarioShips { Design = pick!.Info.Stem, Count = addCount });
            if (addJoins > 0)
                pendingEdit = setup.With(new AddUnit(unit, joinable[addJoins - 1].Made));
            else
            {
                // the side's own course and speed to start from, no faster than she steams
                var lead = current!.Units.FirstOrDefault(u => u.Side == addSide && u.At != null && u.ShipCount > 0);
                double kn = lead?.Kn ?? 10;
                if (f != null && f.Profile.SpeedKn > 0)
                    kn = Math.Min(kn, Math.Floor(f.Profile.SpeedKn));
                StartPlacing(unit, lead?.CourseDeg ?? 0, kn);
            }
            ClosePicker();
        }
        else if (dl.Button("##addcancel", new Vector2(x1 - gw - cw - P(4), by), "Cancel", "Esc"))
            ClosePicker();
    }

    // ------------------------------------------------------------------ the placing view

    sealed class Placing
    {
        public required ScenarioUnit Unit;   // its ships; its place, course and speed are set where it's placed
        public required List<(double AsternM, double AbeamM, int Side)> Stations;
        public required string Title, Hint;
        public int Course, Kn;
        public string CourseText = "", KnText = "";
        public Vec3d? Head, Ghost;   // pressed (the course dragged from it); under the cursor
        public Vector2 DownAt;
        public (long, long, int, bool) WhereKey = (long.MinValue, 0, 0, false);
        public string Where = "";
        public bool Land;

        public void Texts() => (CourseText, KnText) = ($"{Course:000}°", $"{Kn} kn");
    }

    Placing? placing;

    /// <summary>A unit to place on the opening, at a course and speed to start from.</summary>
    void StartPlacing(ScenarioUnit unit, double course, double kn)
    {
        int n = unit.ShipCount;
        string design = unit.Ships.Count > 0 ? TitleOf(unit.Ships[0].Design) : "ships";
        placing = new Placing
        {
            Unit = unit, Stations = ScenarioLoader.Stations(unit), Course = ((int)Math.Round(course) % 360 + 360) % 360, Kn = (int)Math.Round(kn),
            Title = unit.Name, Hint = $"{n} × {design} · click to place her head, drag for the course, right-click cancels",
        };
        placing.Texts();
        (opening, openView) = (null, null);   // framed again with room to place her
    }

    /// <summary>The placing view over the opening: a left press (an armed action; the right cancels it) her head, its drag her
    /// course, its release places her (the opening's input, <see cref="OpeningInput"/>); the unit where it would go; the bar.</summary>
    void PlacingOverlay(OpeningView v)
    {
        var p = placing!;
        float barH = RowH + P(4);
        var at = v.At;
        var mouse = ImGui.GetMousePos();
        if (placePressed)
            (p.Head, p.DownAt) = (v.From(mouse), mouse);
        if (placeHeld && p.Head is { } h)
        {
            if (Vector2.Distance(mouse, p.DownAt) > P(6))
            {
                p.Course = (int)Math.Round(Earth.Bearing(h, v.From(mouse))) % 360;
                p.Texts();
            }
        }
        else if (openHovered)
            p.Ghost = v.From(mouse);
        if (placeReleased && p.Head is { } placed)
        {
            p.Head = null;
            if (!world.Shore.IsLand(placed))
            {
                Place(p, placed);
                return;
            }
        }
        // the unit where it would go: its ships astern, half an hour's course, its name
        if ((p.Head ?? p.Ghost) is { } g)
        {
            p.Land = world.Shore.IsLand(g);
            var colour = p.Land ? T.Muted : T.Amber;
            uint col = U(colour);
            var fwd = Earth.Forward(g, p.Course);
            var starboard = Vec3d.Cross(fwd, g.Normalized());
            foreach (var (astern, abeam, _) in p.Stations)
                dl.AddCircleFilled(v.Of((g - fwd * astern + starboard * abeam).Normalized() * Earth.Radius), P(1.8f), col, 6);
            var hs = v.Of(g);
            Dashed(hs, v.Of((g + fwd * (p.Kn * 1852 * 0.5)).Normalized() * Earth.Radius), col);
            dl.AddCircle(hs, P(7), col, 16, P(1.5f));
            var ts = TagSize(p.Unit.Name);
            dl.Tag(hs + new Vector2(-P(12) - ts.X, P(8)), p.Unit.Name, colour);
            Where(p, v, g);
        }
        // the bar: how it's placed, the course and speed, where, cancel
        var b1 = at + new Vector2(v.Size.X, barH);
        dl.AddRectFilled(at, b1, U(T.HeaderBg));
        dl.HLine(at.X, b1.X, b1.Y - 1, T.Edge);
        float x = at.X + P(8), ty = Mid(UiFonts.SansSmall, at.Y, barH);
        dl.Text(UiFonts.SansSmall, new Vector2(x, ty), T.Muted, p.Hint);
        x += Width(UiFonts.SansSmall, p.Hint) + P(16);
        dl.Text(UiFonts.SansSmall, new Vector2(x, ty), T.Text, "Course");
        x += Width(UiFonts.SansSmall, "Course") + P(6);
        x += dl.Stepper("##pcourse", new Vector2(x, at.Y + (barH - P(15)) / 2), p.CourseText, P(40), true, true, out int by) + P(14);
        if (by != 0)
        {
            p.Course = ((p.Course + 5 * by) % 360 + 360) % 360;
            p.Texts();
        }
        dl.Text(UiFonts.SansSmall, new Vector2(x, ty), T.Text, "Speed");
        x += Width(UiFonts.SansSmall, "Speed") + P(6);
        x += dl.Stepper("##pkn", new Vector2(x, at.Y + (barH - P(15)) / 2), p.KnText, P(40), p.Kn > 0, p.Kn < 60, out by) + P(14);
        if (by != 0)
        {
            p.Kn = Math.Clamp(p.Kn + by, 0, 60);
            p.Texts();
        }
        float cw = ButtonWidth("Cancel", "Esc");
        if (dl.Button("##placecancel", new Vector2(b1.X - P(6) - cw, at.Y + (barH - P(20)) / 2), "Cancel", "Esc"))
        {
            placing = null;
            opening = null;
            return;
        }
        if (p.Where.Length > 0)
            dl.TextRight(UiFonts.MonoSmall, b1.X - P(14) - cw, Mid(UiFonts.MonoSmall, at.Y, barH), p.Land ? T.Amber : T.Text, p.Where);
    }

    /// <summary>Where the unit would go, in words: its place, the nearest of its own and the nearest enemy; made when it
    /// moves.</summary>
    void Where(Placing p, OpeningView v, Vec3d g)
    {
        var (lat, lon) = Earth.LatLon(g);
        var key = ((long)Math.Round(lat * 2000), (long)Math.Round(lon * 2000), p.Course, p.Land);
        if (key == p.WhereKey || current is not { } s)
            return;
        p.WhereKey = key;
        string? ownName = null;
        double own = double.MaxValue, foe = double.MaxValue;
        foreach (var (u, head, ships) in v.Placed)
        {
            if (u.Name == p.Unit.Name)
                continue;   // moving: herself
            if (u.Side == p.Unit.Side)
            {
                double d = Earth.Distance(g, head);
                if (d < own)
                    (own, ownName) = (d, u.Name);
            }
            else if (!s.Peace.Contains((u.Side, p.Unit.Side)) && !s.Peace.Contains((p.Unit.Side, u.Side)))
                foreach (var ship in ships)
                    foe = Math.Min(foe, Earth.Distance(g, ship));
        }
        p.Where = $"{Dms(lat, 'N', 'S')} {Dms(lon, 'E', 'W')}"
                  + (ownName != null ? $" · {own / 1000:0.0} km from {ownName}" : "")
                  + (foe < double.MaxValue ? $" · {foe / 1000:0.0} km from the enemy" : "")
                  + (p.Land ? " · on land" : "");
    }

    /// <summary>The unit placed: its head here, at the course and speed set; an added unit placed again keeps its
    /// change's place in the list.</summary>
    void Place(Placing p, Vec3d head)
    {
        var (lat, lon) = Earth.LatLon(head);
        var u = new ScenarioUnit
        {
            Name = p.Unit.Name, Side = p.Unit.Side, Formation = p.Unit.Formation, SpacingM = p.Unit.SpacingM,
            At = (Math.Round(lat, 4), Math.Round(lon, 4)), CourseDeg = p.Course, Kn = p.Kn,
        };
        u.Units.AddRange(p.Unit.Units);
        u.Ships.AddRange(p.Unit.Ships);
        pendingEdit = setup.With(new AddUnit(u, null));
        placing = null;
        opening = null;
    }
}
