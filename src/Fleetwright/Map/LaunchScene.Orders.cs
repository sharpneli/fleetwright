using System.Numerics;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Missions;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The launch screen's standing orders (docs/map-ui/launch.md L4, tab 2 of the mockup launch-bureau.html): of
/// the side shown, in three columns of tables: its aggression (the C-in-C's, for every unit) with what it moves, written
/// against now; each admiral's temper (added to it: the sum is what he reads, his tooltip why the scenario gave it) and
/// the captains with tempers of their own (every captain's is on the order of battle's selected class); the rules. Each
/// figure is a stepper of 0.05 (Shift ×4), each step a change of the setup; a frame allocates nothing.</summary>
public sealed unsafe partial class LaunchScene
{
    DoctrinePresets presets = DoctrinePresets.Builtin;   // the pick's (Content/Sim/doctrine.json)

    sealed class OrdersView
    {
        public Scenario? Of;
        public int Side;
        public string Doctrine = "", Value = "", Written = "", Reads = "", AllFields = "";
        public double Aggression, WrittenValue;
        public (string Label, string Was, string Now, bool Changed)[] Moves = [];
        public readonly List<TemperRow> Tempers = [], Captains = [];
        public (DoctrineRules.Rule Rule, int On, int Was, string[] Words)[] Rules = [];
    }

    sealed class TemperRow
    {
        public string Name = "", Unit = "", Why = "", Value = "", Sum = "";
        public double Temper, Written;
        public bool Captain;
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

    /// <summary>A temper's figure, signed to two places ("0.00" for none).</summary>
    static string Temper(double t) => Math.Abs(t) < 0.005 ? "0.00" : DoctrineRules.Signed(t);

    /// <summary>The side the first two tabs show (S).</summary>
    int OrdersSide => shipTabs.Count == 0 ? -1 : shipTabs[Math.Min(shipTab, shipTabs.Count - 1)].Side;

    /// <summary>The page's lines for a side, made again when the pick or the side changes.</summary>
    OrdersView View(Scenario s, int sd)
    {
        if (ordersView is { } v && ReferenceEquals(v.Of, s) && v.Side == sd)
            return v;
        v = ordersView = new OrdersView { Of = s, Side = sd };
        var w = written!;
        double a = v.Aggression = ScenarioLoader.SideDoctrine(s, sd, DoctrineField.Aggression, presets);
        v.WrittenValue = ScenarioLoader.SideDoctrine(w, sd, DoctrineField.Aggression, presets);
        string preset = s.Doctrine.FirstOrDefault(d => d.Side == sd).Preset ?? DoctrinePresets.DefaultName;
        v.Doctrine = $"doctrine {preset}";
        (v.Value, v.Written, v.Reads) = (Temper(a), Temper(v.WrittenValue), Word(a));
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
        // the admirals with tempers of their own, and the captains with theirs
        void Walk(ScenarioUnit u, bool top)
        {
            if (u.Removed || u.Side != sd)
                return;
            double ut = u.Temper ?? 0;
            if (top || u.OwnTemper)
            {
                var inWritten = FindUnit(w.Units, setup.NameAsMade(u.Name));
                double wt = inWritten?.Temper ?? 0;
                v.Tempers.Add(new TemperRow
                {
                    Name = u.Admiral.Length > 0 ? SetTemper.AdmiralName(u.Admiral) : "the senior officer", Unit = u.Name,
                    Why = inWritten != null && Math.Abs(ut - wt) < 1e-9 ? u.TemperWhy : "",
                    Temper = ut, Written = wt, Value = Temper(ut), Sum = Temper(Math.Clamp(a + ut, -1, 1)),
                });
            }
            foreach (var ships in u.Ships)
                for (int k = 0; k < Math.Min(ships.Names.Length, ships.Count); k++)
                    if (ships.Names[k].Length > 0 && ships.TemperOf(k) is var ct && Math.Abs(ct) > 1e-9)
                        v.Captains.Add(new TemperRow
                        {
                            Name = ships.Names[k], Unit = u.Name, Captain = true, Temper = ct,
                            Written = SetTemper.CaptainOf(w.Units, ships.Names[k]) ?? 0, Value = Temper(ct), Sum = Temper(Math.Clamp(a + ut + ct, -1, 1)),
                        });
            foreach (var x in u.Units)
                Walk(x, false);
        }
        foreach (var u in s.Units)
            Walk(u, true);
        v.Rules = [.. DoctrineRules.All.Select(r =>
        {
            double now = ScenarioLoader.SideDoctrine(s, sd, r.Field, presets), was = ScenarioLoader.SideDoctrine(w, sd, r.Field, presets);
            return (r, Array.FindIndex(r.Options, o => Math.Abs(o.Value - now) < 1e-9), Array.FindIndex(r.Options, o => Math.Abs(o.Value - was) < 1e-9),
                r.Options.Select(o => o.Word).ToArray());
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

    /// <summary>A temper stepped by <paramref name="by"/> twentieths, on the grid and within ±1.</summary>
    static double Stepped(double t, int by) => Math.Clamp(Math.Round((t + by * 0.05) * 20) / 20, -1, 1);

    void OrdersPage(Vector2 a, Vector2 size)
    {
        int sd = OrdersSide;
        if (current is not { } s || sd < 0)
            return;
        var v = View(s, sd);
        float w1 = MathF.Round(size.X * 0.31f), w2 = MathF.Round(size.X * 0.37f), w3 = size.X - w1 - w2;
        AggressionColumn(a, new Vector2(w1, size.Y), v, sd);
        dl.AddLine(new Vector2(a.X + w1, a.Y), new Vector2(a.X + w1, a.Y + size.Y), U(T.Edge));
        TempersColumn(new Vector2(a.X + w1 + 1, a.Y), new Vector2(w2 - 1, size.Y), v);
        dl.AddLine(new Vector2(a.X + w1 + w2, a.Y), new Vector2(a.X + w1 + w2, a.Y + size.Y), U(T.Edge));
        RulesColumn(new Vector2(a.X + w1 + w2 + 1, a.Y), new Vector2(w3 - 1, size.Y), v, sd);
    }

    void AggressionColumn(Vector2 a, Vector2 size, OrdersView v, int sd)
    {
        float w = size.X, y = a.Y;
        float right = dl.SubHeader(a, w, "AGGRESSION", "the C-in-C's, for every unit");
        float dx = dl.RightNote(right, y, RowH, v.Doctrine);
        if (ImGui.IsMouseHoveringRect(new Vector2(dx, y), new Vector2(right, y + RowH)))
            ImGui.SetTooltip(v.AllFields);
        y += RowH;
        var h = new Vector2(a.X, y);
        float sx = a.X + P(96), wx = a.X + w - P(80), rx = a.X + w - P(74);
        dl.Head(h, w);
        dl.HeadCell(h, a.X + P(6), "LEVER");
        dl.HeadCell(h, sx, "VALUE");
        dl.HeadCell(h, wx, "WRITTEN", right: true);
        dl.HeadCell(h, rx, "READS");
        y += HeadH;
        var ra = new Vector2(a.X, y);
        dl.RowBg(ra, w, RowH, 0);
        dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Bright, "Aggression");
        dl.Stepper("##aggr", new Vector2(sx, ra.Y + P(2.5f)), v.Value, P(44), v.Aggression > -1 + 1e-9, v.Aggression < 1 - 1e-9, out int by);
        if (by != 0)
            pendingEdit = setup.Doctrine(written!, sd, DoctrineField.Aggression, Stepped(v.Aggression, by), presets);
        dl.TextRight(UiFonts.Mono, wx, Mid(UiFonts.Mono, ra.Y, RowH), T.Muted, v.Written);
        dl.Text(UiFonts.SansSmall, new Vector2(rx, Mid(UiFonts.SansSmall, ra.Y, RowH)), T.Text, v.Reads);
        y += RowH + P(6);
        float r2 = dl.SubHeader(new Vector2(a.X, y), w, "WHAT IT MOVES");
        dl.RightNote(r2, y, RowH, "cautious −1 … bold +1");
        y += RowH;
        h = new Vector2(a.X, y);
        float cWas = a.X + w - P(74), cNow = a.X + w - P(8);
        dl.Head(h, w);
        dl.HeadCell(h, a.X + P(6), "MOVE");
        dl.HeadCell(h, cWas, "WRITTEN", right: true);
        dl.HeadCell(h, cNow, "NOW", right: true);
        y += HeadH;
        for (int i = 0; i < v.Moves.Length; i++)
        {
            var (label, was, now, changed) = v.Moves[i];
            ra = new Vector2(a.X, y);
            dl.RowBg(ra, w, RowH, i);
            dl.PushClipRect(ra, new Vector2(cWas - P(56), ra.Y + RowH), true);
            dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Text, label);
            dl.PopClipRect();
            dl.TextRight(UiFonts.Mono, cWas, Mid(UiFonts.Mono, ra.Y, RowH), T.Muted, was);
            dl.TextRight(UiFonts.Mono, cNow, Mid(UiFonts.Mono, ra.Y, RowH), changed ? T.Brass : T.Bright, now);
            y += RowH;
        }
    }

    void TempersColumn(Vector2 a, Vector2 size, OrdersView v)
    {
        float w = size.X;
        ImGui.SetCursorScreenPos(a);
        var keep = dl;
        float y = 0;
        if (ImGui.BeginChild("##tempers", size, ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            dl = ImGui.GetWindowDrawList();
            var o = ImGui.GetCursorScreenPos();
            w = ImGui.GetContentRegionAvail().X;
            y = o.Y;
            float right = dl.SubHeader(o, w, "TEMPERS", "the admiral's, added to the side's");
            dl.RightNote(right, y, RowH, "sum");
            y += RowH;
            y = TemperTable(o.X, y, w, v.Tempers, "ADMIRAL", v.Of!);
            y += P(6);
            dl.SubHeader(new Vector2(o.X, y), w, "CAPTAINS", "with tempers of their own, added to the admiral's");
            y += RowH;
            if (v.Captains.Count == 0)
            {
                dl.Text(UiFonts.SansSmall, new Vector2(o.X + P(6), Mid(UiFonts.SansSmall, y, RowH)), T.Muted, "None: every captain reads his admiral's.");
                y += RowH;
            }
            else
                y = TemperTable(o.X, y, w, v.Captains, "SHIP", v.Of!);
            y += P(6);
            y += dl.Wrapped(UiFonts.SansSmall, new Vector2(o.X + P(6), y), T.Muted,
                "Any captain's temper is on the order of battle: select her class. A timid captain under a bold admiral fights harder than alone, never as hard as a bold one under the same flag.", w - P(12));
            y += P(6) - o.Y;
        }
        ImGui.SetCursorPos(new Vector2(0, Math.Max(y, 0)));
        ImGui.Dummy(new Vector2(1, 0));
        ImGui.EndChild();
        dl = keep;
    }

    /// <summary>A table of tempers: who, his unit, his temper's stepper, the sum he reads; returns where it ends.</summary>
    float TemperTable(float x0, float y, float w, List<TemperRow> rows, string who, Scenario s)
    {
        var h = new Vector2(x0, y);
        float ux = x0 + w * 0.40f, sx = x0 + w - P(64) - (2 * P(13) + P(44) + P(4)), sumX = x0 + w - P(8);
        dl.Head(h, w);
        dl.HeadCell(h, x0 + P(6), who);
        dl.HeadCell(h, ux, "UNIT");
        dl.HeadCell(h, sx, "TEMPER");
        dl.HeadCell(h, sumX, "SUM", right: true);
        y += HeadH;
        for (int i = 0; i < rows.Count; i++)
        {
            var t = rows[i];
            var ra = new Vector2(x0, y);
            ImGui.PushID(t.Captain ? 4096 + i : i);
            var (_, hover) = HitRow("##t", ra, new Vector2(w, RowH));
            dl.RowBg(ra, w, RowH, i, hover: hover);
            dl.PushClipRect(ra, new Vector2(ux - P(6), ra.Y + RowH), true);
            dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Bright, t.Name);
            dl.PopClipRect();
            dl.PushClipRect(new Vector2(ux, ra.Y), new Vector2(sx - P(6), ra.Y + RowH), true);
            dl.Text(UiFonts.SansSmall, new Vector2(ux, Mid(UiFonts.SansSmall, ra.Y, RowH)), T.Muted, t.Unit);
            dl.PopClipRect();
            dl.Stepper("##st", new Vector2(sx, ra.Y + P(2.5f)), t.Value, P(44), t.Temper > -1 + 1e-9, t.Temper < 1 - 1e-9, out int by);
            if (by != 0)
                pendingEdit = setup.Temper(written!, t.Captain ? t.Name : t.Unit, Stepped(t.Temper, by));
            var sumCol = Math.Abs(t.Temper - t.Written) > 1e-9 ? T.Brass : t.Sum == "0.00" ? T.Muted : t.Sum[0] == '+' ? T.Amber : T.Text;
            dl.TextRight(UiFonts.Mono, sumX, Mid(UiFonts.Mono, ra.Y, RowH), sumCol, t.Sum);
            if (hover && t.Why.Length > 0)
                ImGui.SetTooltip(t.Why);
            else if (hover && Math.Abs(t.Temper - t.Written) > 1e-9)
                ImGui.SetTooltip($"the scenario's: {Temper(t.Written)}");
            ImGui.PopID();
            y += RowH;
        }
        return y;
    }

    void RulesColumn(Vector2 a, Vector2 size, OrdersView v, int sd)
    {
        float w = size.X, y = a.Y;
        float right = dl.SubHeader(a, w, "RULES");
        dl.RightNote(right, y, RowH, "every unit, unless ordered");
        y += RowH;
        var h = new Vector2(a.X, y);
        dl.Head(h, w);
        dl.HeadCell(h, a.X + P(6), "RULE");
        float segX = a.X + w * 0.55f;
        dl.HeadCell(h, segX, "SETTING");
        y += HeadH;
        for (int i = 0; i < v.Rules.Length; i++)
        {
            var (rule, on, was, words) = v.Rules[i];
            var ra = new Vector2(a.X, y);
            dl.RowBg(ra, w, RowH, i);
            dl.PushClipRect(ra, new Vector2(segX - P(6), ra.Y + RowH), true);
            dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Text, rule.Label);
            dl.PopClipRect();
            ImGui.PushID(i);
            int hit = dl.Segmented("##rule", new Vector2(segX, ra.Y + P(2)), words, on, out float sw);
            ImGui.PopID();
            if (hit >= 0)
                pendingEdit = setup.Doctrine(written!, sd, rule.Field, rule.Options[hit].Value, presets);
            if (on != was)
                dl.Text(UiFonts.SansSmall, new Vector2(segX + sw + P(8), Mid(UiFonts.SansSmall, ra.Y, RowH)), T.Muted, "changed");
            y += RowH;
        }
    }
}
