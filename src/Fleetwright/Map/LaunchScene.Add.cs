using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Adding a unit (docs/map-ui/launch.md L3b, the mockup's <c>#add</c> and <c>#place</c>): the design picker in
/// its add mode names the new unit, how many of the design picked, its formation and spacing, and whether it joins a
/// force (on its station there: no placing) or stands on its own. On its own it's placed in the placing view, the opening
/// widened over the header: the unit at the cursor with its ships astern and half an hour's course, a press places her
/// head (not on land), a drag sets the course; the course and speed typed in the bar, beside where she is and how far
/// from her own and from the enemy. Either way it's an <see cref="AddUnit"/> of the setup; one on its own can be placed
/// again (its "move"). No matchup: an added unit meets whom it meets (the scenario's maker places, the user 2026-10-25).</summary>
public sealed unsafe partial class LaunchScene
{
    bool adding;
    int addSide, addCount = 4, addFormation, addSpacing = 600, addJoins;
    string addName = "", addCountText = "4";
    readonly List<(string Shown, string Made)> joinable = [];   // the side's units: as shown, and as the changes name them
    string[] joinLabels = [];
    HashSet<string> unitNames = [];   // every unit's in the pick: a new one's is new
    (int, int, bool, int) addNoteKey = (-2, 0, false, 0);
    string addNote = "";
    const string AddSub = "A squadron of one design; more rows come with Change… and − + once it's in the order of battle";
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
        joinLabels = ["on its own: placed on the map", .. joinable.Select(j => $"{j.Shown}: on its station")];
        addName = "New squadron";
        for (int k = 2; unitNames.Contains(addName); k++)
            addName = $"New squadron {k}";
        (addCount, addCountText, addFormation, addSpacing, addJoins) = (4, "4", 0, 600, joinable.FindIndex(j => j.Shown == joins) + 1);
        pickerTitle = $"Add a unit · {s.Sides[sd]}";
        pickerYear = s.Start.ToUtc().Year;
        yearChip = $"UP TO {pickerYear} ONLY";
        pickerRow = new ShipLine { Side = sd, Unit = new ScenarioUnit { Name = "", Side = sd }, Ships = new ScenarioShips { Design = "" } };
        Candidates();
    }

    /// <summary>The add mode's form: the name, the ships, the formation and spacing, where it goes. Its height.</summary>
    float AddForm(float x, float y, float x1)
    {
        float k = 90 * sc, row = 28 * sc, y0 = y;
        PushFields();
        Txt(UiFonts.SansSmall, 12, new Vector2(x, y + 3 * sc), Soft, "Name");
        ImGui.SetCursorScreenPos(new Vector2(x + k, y));
        ImGui.SetNextItemWidth(260 * sc);
        ImGui.InputText("##addname", ref addName, 64);
        y += row;
        Txt(UiFonts.SansSmall, 12, new Vector2(x, y + 3 * sc), Soft, "Ships");
        float sw = Stepper(new Vector2(x + k, y + 2 * sc), addCountText, addCount > 1, addCount < 99, out int by);
        if (by != 0)
            (addCount, addCountText) = (addCount + by, (addCount + by).ToString(Inv));
        Txt(UiFonts.SansSmall, 11.5f, new Vector2(x + k + sw + 10 * sc, y + 3 * sc), Muted, "of the design picked below, unnamed");
        y += row;
        Txt(UiFonts.SansSmall, 12, new Vector2(x, y + 3 * sc), Soft, "Formation");
        ImGui.SetCursorScreenPos(new Vector2(x + k, y));
        ImGui.SetNextItemWidth(170 * sc);
        ImGui.Combo("##addform", ref addFormation, AddFormationNames, AddFormationNames.Length);
        Txt(UiFonts.SansSmall, 12, new Vector2(x + k + 190 * sc, y + 3 * sc), Soft, "Spacing");
        ImGui.SetCursorScreenPos(new Vector2(x + k + 246 * sc, y));
        ImGui.SetNextItemWidth(70 * sc);
        if (ImGui.InputInt("##addsp", ref addSpacing, 0, 0))
            addSpacing = Math.Clamp(addSpacing, 100, 20_000);
        Txt(UiFonts.SansSmall, 12, new Vector2(x + k + 322 * sc, y + 3 * sc), Muted, "m");
        y += row;
        Txt(UiFonts.SansSmall, 12, new Vector2(x, y + 3 * sc), Soft, "Joins");
        ImGui.SetCursorScreenPos(new Vector2(x + k, y));
        ImGui.SetNextItemWidth(Math.Min(420 * sc, x1 - x - k - 20 * sc));
        ImGui.Combo("##addjoins", ref addJoins, joinLabels, joinLabels.Length, 16);
        PopFields();
        Txt(UiFonts.SansSmall, 11.5f, new Vector2(x + k + Math.Min(420 * sc, x1 - x - k - 20 * sc) + 10 * sc, y + 3 * sc), Muted,
            addJoins == 0 ? "" : "a squadron joined becomes a force: its ships, then this");
        y += row;
        HLine(x - 20 * sc, x1, y + 2 * sc);
        return y - y0 + 4 * sc;
    }

    /// <summary>The add mode's foot: what it adds, cancel, and place it (or add it, joining).</summary>
    void AddFoot(Candidate? pick, float x, float x1, float by, float bh)
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
        Txt(UiFonts.SansSmall, 11.5f, new Vector2(x, by + 5 * sc), taken || name.Length == 0 ? Hostile : Muted, addNote);
        string go = addJoins > 0 ? "Add it" : "Place it on the map…";
        float gw = Math.Max(120 * sc, Width(UiFonts.SansBold, 12, go) + 28 * sc);
        bool ok = pick != null && name.Length > 0 && !taken;
        if (Button("##addgo", go, new Vector2(x1 - 20 * sc - gw, by), new Vector2(gw, bh), primary: true, enabled: ok, px: 12))
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
        else if (Button("##addcancel", "Cancel", new Vector2(x1 - 28 * sc - gw - 76 * sc, by), new Vector2(76 * sc, bh), px: 11.5f))
            ClosePicker();
    }

    /// <summary>The form's fields framed as the picker's search is, in the designer's theme otherwise.</summary>
    void PushFields()
    {
        Designer.DesignerTheme.Push();
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0, 0, 0, 0.3f));
        ImGui.PushStyleColor(ImGuiCol.Border, EdgeC);
        ImGui.PushStyleColor(ImGuiCol.Text, Bright);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 3) * sc);
        ImGui.PushFont(UiFonts.SansSmall);
    }

    static void PopFields()
    {
        ImGui.PopFont();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(3);
        Designer.DesignerTheme.Pop();
    }

    // ------------------------------------------------------------------ the placing view

    sealed class Placing
    {
        public required ScenarioUnit Unit;   // its ships; its place, course and speed are set where it's placed
        public required List<(double AsternM, double AbeamM, int Side)> Stations;
        public required string Title, Hint;
        public int Course, Kn;
        public Vec3d? Head, Ghost;   // pressed (the course dragged from it); under the cursor
        public Vector2 DownAt;
        public (long, long, int, bool) WhereKey = (long.MinValue, 0, 0, false);
        public string Where = "";
        public bool Land;
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
            Title = $"Placing {unit.Name}", Hint = $"{n} × {design} · click to place her head, drag for the course",
        };
    }

    /// <summary>The placing view over the opening: the input, the unit where it would go, and the bar.</summary>
    void PlacingOverlay(OpeningView v)
    {
        var p = placing!;
        float barH = 30 * sc;
        var at = v.At;
        ImGui.SetCursorScreenPos(at + new Vector2(0, barH));
        ImGui.InvisibleButton("##placemap", v.Size - new Vector2(0, barH));
        var mouse = ImGui.GetMousePos();
        if (ImGui.IsItemActivated())
            (p.Head, p.DownAt) = (v.From(mouse), mouse);
        if (ImGui.IsItemActive() && p.Head is { } h)
        {
            if (Vector2.Distance(mouse, p.DownAt) > 6 * sc)
                p.Course = (int)Math.Round(Earth.Bearing(h, v.From(mouse))) % 360;
        }
        else if (ImGui.IsItemHovered())
            p.Ghost = v.From(mouse);
        if (ImGui.IsItemDeactivated() && p.Head is { } placed)
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
            uint col = U(p.Land ? Hostile : Amber);
            var fwd = Earth.Forward(g, p.Course);
            var starboard = Vec3d.Cross(fwd, g.Normalized());
            foreach (var (astern, abeam, _) in p.Stations)
                dl.AddCircleFilled(v.Of((g - fwd * astern + starboard * abeam).Normalized() * Earth.Radius), 1.8f * sc, col, 6);
            var hs = v.Of(g);
            Dashed(hs, v.Of((g + fwd * (p.Kn * 1852 * 0.5)).Normalized() * Earth.Radius), col);
            dl.AddCircle(hs, 7 * sc, col, 16, 1.5f * sc);
            Txt(UiFonts.SansSmall, 11.5f, hs + new Vector2(-12 * sc - Width(UiFonts.SansSmall, 11.5f, p.Unit.Name), 8 * sc), p.Land ? Hostile : Amber, p.Unit.Name);
            Where(p, v, g);
        }
        // the bar: what's placed and how, the course and speed, where, cancel
        var b1 = at + new Vector2(v.Size.X, barH);
        dl.AddRectFilled(at, b1, U(new Vector4(10 / 255f, 14 / 255f, 17 / 255f, 0.9f)));
        dl.AddLine(new Vector2(at.X, b1.Y), b1, U(LineC));
        float x = at.X + 12 * sc, ty = at.Y + 7 * sc;
        Txt(UiFonts.SansBold, 12.5f, new Vector2(x, ty), Amber, p.Title);
        x += Width(UiFonts.SansBold, 12.5f, p.Title) + 12 * sc;
        Txt(UiFonts.SansSmall, 12, new Vector2(x, ty + 1 * sc), Muted, p.Hint);
        x += Width(UiFonts.SansSmall, 12, p.Hint) + 18 * sc;
        PushFields();
        Txt(UiFonts.SansSmall, 12, new Vector2(x, ty + 1 * sc), Soft, "course");
        x += Width(UiFonts.SansSmall, 12, "course") + 6 * sc;
        ImGui.SetCursorScreenPos(new Vector2(x, at.Y + 4 * sc));
        ImGui.SetNextItemWidth(46 * sc);
        if (ImGui.InputInt("##pcourse", ref p.Course, 0, 0))
            p.Course = (p.Course % 360 + 360) % 360;
        x += 50 * sc;
        Txt(UiFonts.SansSmall, 12, new Vector2(x, ty + 1 * sc), Soft, "°   speed");
        x += Width(UiFonts.SansSmall, 12, "°   speed") + 6 * sc;
        ImGui.SetCursorScreenPos(new Vector2(x, at.Y + 4 * sc));
        ImGui.SetNextItemWidth(36 * sc);
        if (ImGui.InputInt("##pkn", ref p.Kn, 0, 0))
            p.Kn = Math.Clamp(p.Kn, 0, 60);
        x += 40 * sc;
        Txt(UiFonts.SansSmall, 12, new Vector2(x, ty + 1 * sc), Soft, "kn");
        PopFields();
        float cw = 70 * sc;
        if (Button("##placecancel", "Cancel  Esc", new Vector2(b1.X - 8 * sc - cw, at.Y + 4 * sc), new Vector2(cw, 22 * sc), px: 11))
        {
            placing = null;
            return;
        }
        if (p.Where.Length > 0)
            Txt(UiFonts.Mono, 11.5f, new Vector2(b1.X - 20 * sc - cw - Width(UiFonts.Mono, 11.5f, p.Where), ty + 1 * sc), p.Land ? Hostile : Text, p.Where);
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
    }
}
