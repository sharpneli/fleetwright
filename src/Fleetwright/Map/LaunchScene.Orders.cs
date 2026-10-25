using System.Numerics;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Missions;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The launch screen's standing orders (docs/map-ui/launch.md L4, the mockup's <c>#orders</c> second column): of
/// the side whose ships the first column shows, its aggression (the C-in-C's, for every unit) with what it moves, written
/// against now; each admiral's temper on it (the side's + his: what he reads), with why the scenario gave it; and the
/// rules. Captains' tempers sit on their ships' rows (<c>LaunchScene.Ships.cs</c>). A slider makes its change when it's
/// let go (each change places the world again); a frame allocates nothing until a value moves.</summary>
public sealed unsafe partial class LaunchScene
{
    DoctrinePresets presets = DoctrinePresets.Builtin;   // the pick's (Content/Sim/doctrine.json)

    // a slider being dragged: its id and the value under the mouse (the change is made on release)
    string? dragId;
    double dragValue;

    sealed class OrdersView
    {
        public Scenario? Of;
        public int Side;
        public string? DragId;
        public double DragValue;
        public string Title = "", Doctrine = "", Value = "", AllFields = "";
        public double Aggression, Set, Written;   // as shown (dragged), as set, as written
        public (string Label, string Was, string Now, bool Changed)[] Moves = [];
        public readonly List<TemperRow> Tempers = [];
        public (DoctrineRules.Rule Rule, int On, int Was)[] Rules = [];
    }

    sealed class TemperRow
    {
        public string Name = "", Unit = "", Why = "", Effect = "", Id = "", Shown = "";
        public double Temper, Set, Written;
    }

    OrdersView? ordersView;

    /// <summary>What each field aggression moves reads as (settings' AggressionShift lists which), in the order shown.</summary>
    static readonly (DoctrineField Field, string Label, Func<double, string> Format)[] Moved =
    [
        (DoctrineField.OutgunnedRatio, "Turns away when outgunned", v => $"{v:0.0} : 1"),
        (DoctrineField.SeekRatio, "Sent after an enemy at a matchup of", v => $"{v:0.00}"),
        (DoctrineField.RangeShare, "Fights at, of the main guns' reach", Percent),
        (DoctrineField.FallOutSpeedShare, "Falls out under, of her speed", v => v <= 0 ? "never" : Percent(v)),
        (DoctrineField.FallOutGunsShare, "Falls out under, of her main guns", v => v <= 0 ? "never" : Percent(v)),
        (DoctrineField.FallOutListDeg, "Falls out past a list of", v => v >= 90 ? "never" : $"{v:0}°"),
        (DoctrineField.FallOutWaterShare, "Falls out flooded, of her reserve", v => v >= 1 ? "never" : Percent(v)),
        (DoctrineField.TurnAwayDeg, "Turns from torpedo attacks", v => v <= 0 ? "holds on" : $"{v:0}°"),
        (DoctrineField.AttackBreakOffShare, "Flotilla beaten off at boats lost", v => v >= 1 ? "never" : Percent(v)),
        (DoctrineField.BreakOffShare, "The side breaks off at tonnage lost", v => v >= 1 ? "never" : Percent(v)),
    ];

    static string Percent(double v) => $"{v * 100:0}%";

    static string Word(double a) => a > 0.65 ? "bold" : a > 0.15 ? "forward" : a < -0.65 ? "timid" : a < -0.15 ? "cautious" : "as written";

    /// <summary>The side the column is of: the ships' tab's.</summary>
    int OrdersSide => shipTabs.Count == 0 ? -1 : shipTabs[Math.Min(shipTab, shipTabs.Count - 1)].Side;

    /// <summary>The column's lines for a side, made again when the pick, the side or a dragged value changes.</summary>
    OrdersView View(Scenario s, int sd)
    {
        if (ordersView is { } v && ReferenceEquals(v.Of, s) && v.Side == sd && v.DragId == dragId && v.DragValue == dragValue)
            return v;
        v = ordersView = new OrdersView { Of = s, Side = sd, DragId = dragId, DragValue = dragValue };
        var w = written!;
        v.Set = ScenarioLoader.SideDoctrine(s, sd, DoctrineField.Aggression, presets);
        double a = v.Aggression = dragId == "side" ? dragValue : v.Set;
        v.Written = ScenarioLoader.SideDoctrine(w, sd, DoctrineField.Aggression, presets);
        v.Title = $"STANDING ORDERS · {s.Sides[sd].ToUpperInvariant()}{(side < 0 ? " (AI)" : sd == side ? "" : " · THE ENEMY (AI)")}";
        string preset = s.Doctrine.FirstOrDefault(d => d.Side == sd).Preset ?? DoctrinePresets.DefaultName;
        v.Doctrine = $"doctrine \"{preset}\"";
        v.Value = $"{DoctrineRules.Signed(a)} · {Word(a)}";
        // what aggression moves: as the side's doctrine has it, and now
        var shifts = world.Settings.AggressionShift;
        v.Moves = [.. Moved.Where(m => shifts.ContainsKey(m.Field.ToString())).Select(m =>
        {
            double was = ScenarioLoader.SideDoctrine(s, sd, m.Field, presets), now = was * (1 + shifts[m.Field.ToString()] * Math.Clamp(a, -1, 1));
            string ws = m.Format(was), ns = m.Format(now);
            return (m.Label, ws, ns, ws != ns);
        })];
        var all = Enum.GetValues<DoctrineField>().Where(f => f != DoctrineField.Temper)
            .Select(f => $"{f}: {DoctrinePresets.NameOf(f, ScenarioLoader.SideDoctrine(s, sd, f, presets)) ?? ScenarioLoader.SideDoctrine(s, sd, f, presets).ToString("0.###", Inv)}");
        v.AllFields = $"Doctrine \"{preset}\" with the scenario's and your changes over it (docs/battle/command.md):\n" + string.Join("\n", all);
        // the admirals with tempers of their own
        void Walk(ScenarioUnit u, bool top)
        {
            if (u.Side == sd && !u.Removed && (top || u.OwnTemper))
            {
                double t = dragId == "t:" + u.Name ? dragValue : u.Temper ?? 0;
                string made = setup.NameAsMade(u.Name);
                var inWritten = FindUnit(w.Units, made);
                double wt = inWritten?.Temper ?? 0;
                v.Tempers.Add(new TemperRow
                {
                    Name = u.Admiral.Length > 0 ? SetTemper.AdmiralName(u.Admiral) : "the senior officer", Unit = u.Name, Shown = u.Name,
                    Why = inWritten != null && Math.Abs(t - wt) < 1e-9 ? u.TemperWhy : "", Id = "t:" + u.Name,
                    Temper = t, Set = u.Temper ?? 0, Written = wt, Effect = DoctrineRules.Signed(Math.Clamp(a + t, -1, 1)),
                });
            }
            if (!u.Removed)
                foreach (var x in u.Units)
                    Walk(x, false);
        }
        foreach (var u in s.Units)
            Walk(u, true);
        v.Rules = [.. DoctrineRules.All.Select(r =>
        {
            double now = ScenarioLoader.SideDoctrine(s, sd, r.Field, presets), was = ScenarioLoader.SideDoctrine(w, sd, r.Field, presets);
            return (r, Array.FindIndex(r.Options, o => Math.Abs(o.Value - now) < 1e-9), Array.FindIndex(r.Options, o => Math.Abs(o.Value - was) < 1e-9));
        })];
        return v;
    }

    static ScenarioUnit? FindUnit(IEnumerable<ScenarioUnit> units, string name)
    {
        foreach (var u in units)
            if (u.Name == name)
                return u;
            else if (FindUnit(u.Units, name) is { } f)
                return f;
        return null;
    }

    /// <summary>The standing orders column: the aggression and what it moves, the tempers, the rules; scrolling.</summary>
    void OrdersColumn(Vector2 at, float w, float bottom)
    {
        int sd = OrdersSide;
        if (current is not { } s || sd < 0)
            return;
        var v = View(s, sd);
        ImGui.SetCursorScreenPos(at);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        if (ImGui.BeginChild("##orders", new Vector2(w + 10 * sc, bottom - at.Y - 8 * sc), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            var keep = dl;
            dl = ImGui.GetWindowDrawList();
            var o = ImGui.GetCursorScreenPos();
            float x = o.X, y = o.Y;
            w -= 6 * sc;
            Tracked(UiFonts.Sans, 10.5f, new Vector2(x, y), Muted, v.Title, 0.9f);
            float dw = Width(UiFonts.SansSmall, 11, v.Doctrine);
            Txt(UiFonts.SansSmall, 11, new Vector2(x + w - dw - Width(UiFonts.SansSmall, 11, "  all fields…"), y), Muted, v.Doctrine);
            const string All = "all fields…";
            float aw = Width(UiFonts.SansSmall, 11, All);
            Txt(UiFonts.SansSmall, 11, new Vector2(x + w - aw, y), Brass, All);
            ImGui.SetCursorScreenPos(new Vector2(x + w - aw, y));
            ImGui.InvisibleButton("##allfields", new Vector2(aw, 16 * sc));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(v.AllFields);
            y += 24 * sc;

            // the aggression, and what it moves
            float boxTop = y, bh = (112 + 17 * v.Moves.Length) * sc;
            Box(new Vector2(x, y), new Vector2(x + w, y + bh), new Vector4(1, 1, 1, 0.015f), LineC);
            float ix = x + 12 * sc, iw = w - 24 * sc;
            y += 10 * sc;
            Txt(UiFonts.Sans, 14, new Vector2(ix, y), Bright, "Aggression");
            Txt(UiFonts.SansSmall, 12, new Vector2(ix + Width(UiFonts.Sans, 14, "Aggression") + 8 * sc, y + 2 * sc), Muted, "the C-in-C's, for every unit");
            Txt(UiFonts.Mono, 13, new Vector2(ix + iw - Width(UiFonts.Mono, 13, v.Value), y + 1 * sc), Brass, v.Value);
            y += 24 * sc;
            if (Slider("side", new Vector2(ix + 4 * sc, y), iw - 8 * sc, v.Set, v.Written, big: true, out double na) && !begin)
                pendingEdit = setup.Doctrine(written!, sd, DoctrineField.Aggression, na, presets);
            y += 46 * sc;
            float cw = 64 * sc;
            Tracked(UiFonts.Sans, 10, new Vector2(ix, y), Muted, "MOVES", 0.6f);
            Tracked(UiFonts.Sans, 10, new Vector2(ix + iw - cw - 8 * sc - TrackedWidth(UiFonts.Sans, 10, "WRITTEN", 0.6f), y), Muted, "WRITTEN", 0.6f);
            Tracked(UiFonts.Sans, 10, new Vector2(ix + iw - TrackedWidth(UiFonts.Sans, 10, "NOW", 0.6f), y), Muted, "NOW", 0.6f);
            y += 17 * sc;
            foreach (var (label, was, now, changed) in v.Moves)
            {
                Txt(UiFonts.SansSmall, 12, new Vector2(ix, y), Soft, label);
                Txt(UiFonts.Mono, 11.5f, new Vector2(ix + iw - cw - 8 * sc - Width(UiFonts.Mono, 11.5f, was), y + 1 * sc), Muted, was);
                Txt(UiFonts.Mono, 11.5f, new Vector2(ix + iw - Width(UiFonts.Mono, 11.5f, now), y + 1 * sc), changed ? Brass : Bright, now);
                y += 17 * sc;
            }
            y = boxTop + bh + 16 * sc;

            // the tempers
            Tracked(UiFonts.Sans, 10.5f, new Vector2(x, y), Muted, "TEMPERS", 0.9f);
            const string Sum = "the side's + the admiral's + the captain's";
            Txt(UiFonts.SansSmall, 11, new Vector2(x + w - Width(UiFonts.SansSmall, 11, Sum), y), Muted, Sum);
            y += 22 * sc;
            float sw = 120 * sc, ew = 46 * sc;
            foreach (var t in v.Tempers)
            {
                float nx = x + 2 * sc, room = w - sw - ew - 24 * sc;
                Txt(UiFonts.Sans, 12.5f, new Vector2(nx, y + 3 * sc), Bright, t.Name);
                float after = nx + Width(UiFonts.Sans, 12.5f, t.Name) + 6 * sc;
                string rest = t.Why.Length > 0 ? $"{t.Unit} · {t.Why}" : t.Unit;
                if (after < nx + room)
                    Txt(UiFonts.SansSmall, 11, new Vector2(after, y + 4.5f * sc), Muted, Clip(UiFonts.SansSmall, 11, rest, nx + room - after));
                if (Slider(t.Id, new Vector2(x + w - sw - ew - 10 * sc, y + 3 * sc), sw, t.Set, t.Written, big: false, out double nt) && !begin)
                    pendingEdit = setup.Temper(written!, t.Shown, nt);
                Txt(UiFonts.Mono, 11.5f, new Vector2(x + w - Width(UiFonts.Mono, 11.5f, t.Effect), y + 4 * sc), t.Effect == "0" ? Muted : Brass, t.Effect);
                y += 26 * sc;
                HLine(x, x + w, y - 2 * sc);
            }
            y += 4 * sc;
            y += Wrapped(UiFonts.SansSmall, 11, new Vector2(x, y), Muted,
                "Captains' tempers sit on their ships (by the name: bold up, timid down; click the names): a timid captain under a bold admiral fights harder than alone, never as hard as a bold one under the same flag.", w) + 14 * sc;

            // the rules, two a row
            float colW = (w - 14 * sc) / 2;
            for (int i = 0; i < v.Rules.Length; i++)
            {
                var (rule, on, was) = v.Rules[i];
                float rx = x + (i % 2) * (colW + 14 * sc), ry = y + (i / 2) * 46 * sc;
                Txt(UiFonts.SansSmall, 11.5f, new Vector2(rx, ry), on != was ? Brass : Soft, rule.Label);
                float bx = rx;
                for (int k = 0; k < rule.Options.Length; k++)
                {
                    string word = rule.Options[k].Word;
                    float bw = Width(UiFonts.SansSmall, 11.5f, word) + 16 * sc;
                    var a0 = new Vector2(bx, ry + 18 * sc);
                    ImGui.SetCursorScreenPos(a0);
                    ImGui.PushID(i * 4 + k);
                    if (ImGui.InvisibleButton("##rule", new Vector2(bw, 21 * sc)) && k != on && !begin)
                        pendingEdit = setup.Doctrine(written!, sd, rule.Field, rule.Options[k].Value, presets);
                    ImGui.PopID();
                    bool hover = ImGui.IsItemHovered();
                    if (k == on)
                        dl.AddRectFilled(a0, a0 + new Vector2(bw, 21 * sc), U(Hex(0x2A2412)));
                    else if (hover)
                        dl.AddRectFilled(a0, a0 + new Vector2(bw, 21 * sc), U(new Vector4(1, 1, 1, 0.05f)));
                    Txt(UiFonts.SansSmall, 11.5f, a0 + new Vector2(8 * sc, 3 * sc), k == on ? (on != was ? Brass : Bright) : Muted, word);
                    if (k > 0)
                        VLine(bx, a0.Y, a0.Y + 21 * sc);
                    bx += bw;
                }
                dl.AddRect(new Vector2(rx, ry + 18 * sc), new Vector2(bx, ry + 39 * sc), U(EdgeC), 2 * sc);
            }
            y += (v.Rules.Length + 1) / 2 * 46 * sc;
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            ImGui.Dummy(new Vector2(1, 4 * sc));
            dl = keep;
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    /// <summary>A temper's or the aggression's slider, -1 to +1 by 0.05 (the mockup's): the track cautious to bold, a tick
    /// at the scenario's value. True when it's let go at another value (<paramref name="released"/>).</summary>
    bool Slider(string id, Vector2 at, float w, double value, double written, bool big, out double released)
    {
        released = value;
        float h = (big ? 26 : 18) * sc;
        ImGui.SetCursorScreenPos(at);
        bool pressed = ImGui.InvisibleButton(id, new Vector2(w, h)), done = false;
        double under = Math.Round(Math.Clamp((ImGui.GetMousePos().X - at.X) / w * 2 - 1, -1, 1) * 20) / 20;
        if (ImGui.IsItemActive() && !begin)
            (dragId, dragValue) = (id, under);
        else if (pressed && dragId != id && !begin)
        {
            // pressed and let go within a frame
            released = under;
            done = Math.Abs(under - value) > 1e-9;
        }
        else if (dragId == id)
        {
            released = dragValue;
            done = Math.Abs(dragValue - value) > 1e-9;
            dragId = null;
        }
        double shown = dragId == id ? dragValue : value;
        float X(double p) => at.X + (float)((p + 1) / 2) * w;
        float ty = at.Y + (big ? 11 : 7) * sc, th = (big ? 4 : 3) * sc;
        uint blue = U(Hex(0x3C5C7A)), grey = U(Hex(0x2A3138)), brown = U(Hex(0x7A4A2C));
        dl.AddRectFilledMultiColor(new Vector2(at.X, ty), new Vector2(X(0), ty + th), blue, grey, grey, blue);
        dl.AddRectFilledMultiColor(new Vector2(X(0), ty), new Vector2(at.X + w, ty + th), grey, brown, brown, grey);
        dl.AddLine(new Vector2(X(0), at.Y + (big ? 6 : 3) * sc), new Vector2(X(0), at.Y + (big ? 20 : 15) * sc), U(Hex(0x56636E)));
        if (Math.Abs(written) > 1e-9)
            dl.AddRectFilled(new Vector2(X(written) - 1 * sc, at.Y + 2 * sc), new Vector2(X(written) + 1 * sc, at.Y + h - 2 * sc), U(Muted));
        float tw = (big ? 12 : 9) * sc, tH = (big ? 16 : 13) * sc, tY = at.Y + (big ? 5 : 2) * sc;
        dl.AddRectFilled(new Vector2(X(shown) - tw / 2, tY), new Vector2(X(shown) + tw / 2, tY + tH), U(Brass), 2 * sc);
        if (big)
        {
            Txt(UiFonts.SansSmall, 10.5f, new Vector2(at.X, at.Y + 24 * sc), Muted, "cautious");
            Txt(UiFonts.SansSmall, 10.5f, new Vector2(X(0) - Width(UiFonts.SansSmall, 10.5f, "as written") / 2, at.Y + 24 * sc), Muted, "as written");
            Txt(UiFonts.SansSmall, 10.5f, new Vector2(at.X + w - Width(UiFonts.SansSmall, 10.5f, "bold"), at.Y + 24 * sc), Muted, "bold");
        }
        else if (ImGui.IsItemHovered() && dragId == null && Math.Abs(written - value) > 1e-9)
            ImGui.SetTooltip($"the scenario's: {DoctrineRules.Signed(written)}");
        return done;
    }

    /// <summary>A row's captains (the names line clicked): each named ship's temper, set on release.</summary>
    void CaptainsPopup(ShipLine l)
    {
        if (!ImGui.BeginPopup("##captains"))
            return;
        var keep = dl;
        dl = ImGui.GetWindowDrawList();
        var o = ImGui.GetCursorScreenPos();
        float w = 330 * sc, y = o.Y;
        Txt(UiFonts.SansBold, 12, o, Bright, $"Captains · {l.Text}");
        y += 22 * sc;
        int live = Math.Min(l.Ships.Names.Length, l.Ships.Count);
        for (int k = 0; k < live; k++)
        {
            string name = l.Ships.Names[k];
            if (name.Length == 0)
                continue;
            string id = "c:" + name;
            double t = l.Ships.TemperOf(k), wt = SetTemper.CaptainOf(written!.Units, name) ?? 0;
            Txt(UiFonts.SansSmall, 12, new Vector2(o.X, y + 2 * sc), Text, name);
            if (Slider(id, new Vector2(o.X + w - 180 * sc, y + 2 * sc), 120 * sc, t, wt, big: false, out double nt) && !begin)
                pendingEdit = setup.Temper(written!, name, nt);
            string shown = DoctrineRules.Signed(dragId == id ? dragValue : t);
            Txt(UiFonts.Mono, 11.5f, new Vector2(o.X + w - Width(UiFonts.Mono, 11.5f, shown), y + 3 * sc), shown == "0" ? Muted : Brass, shown);
            y += 24 * sc;
        }
        if (live == 0 || l.Ships.Names.All(n => n.Length == 0))
        {
            Txt(UiFonts.SansSmall, 11.5f, new Vector2(o.X, y), Muted, "Her captains are unnamed: name a ship to give hers a temper.");
            y += 18 * sc;
        }
        y += 4 * sc;
        y += Wrapped(UiFonts.SansSmall, 11, new Vector2(o.X, y), Muted, "Added to her admiral's temper and the side's aggression.", w);
        ImGui.SetCursorScreenPos(new Vector2(o.X, y));
        ImGui.Dummy(new Vector2(w, 2 * sc));
        dl = keep;
        ImGui.EndPopup();
    }
}
