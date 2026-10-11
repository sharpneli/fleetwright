using System.Numerics;
using Fleetwright.Shipgen;
using ImGuiNET;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Designer;

/// <summary>
/// The Overview tab, after docs/designer-ui/overview.md: a trim sheet of broad-stroke levers (a knot, 10 mm of belt),
/// each with what one step down and up would do to the standard displacement, measured by real builds in the
/// background (<see cref="DesignProbes"/>); the limits the design is measured against (from the gameplay layer, mocked
/// for now: they show, they never refuse); the verdict and the weight by group; and Fit, greyed out until the
/// autodesigner (shared with the AI) exists.
/// </summary>
public sealed unsafe partial class DesignerScene
{
    /// <summary>A trim sheet lever: its group, label, what it drives, its knob (null: the all-armour scale), and which
    /// way it usually moves the weight (+1 more is heavier, -1 more is lighter, 0 either way), so a step that goes the
    /// other way can be marked.</summary>
    sealed record Lever(int Group, string Label, string Drives, NumberKnob? Knob, int Sense = 1);

    static readonly (string Name, int Tab, string[] Groups)[] OvGroups =
    [
        ("SPEED & MACHINERY", 4, ["machinery"]),
        ("PROTECTION", 3, ["armour"]),
        ("ARMAMENT", 1, ["armament"]),
        ("HULL & CREW", 5, ["hull", "superstructure", "misc"]),
    ];

    static readonly Lever[] Levers =
    [
        new(0, "Speed", "power, machinery, fuel; the hull lengthens for speed", Knobs.Speed),
        new(0, "Range", "fuel, bunkers", Knobs.Range),
        new(0, "Machinery rating", "higher: a lighter plant worked harder, less margin", Knobs.Stress, -1),
        new(1, "Belt", "immune zone, inner edge", Knobs.Belt),
        new(1, "Upper belt", "the side above the belt", Knobs.UpperBelt),
        new(1, "Armour deck", "immune zone, outer edge; top weight", Knobs.ArmourDeck),
        new(1, "Citadel bulkheads", "raking fire along the citadel", Knobs.Bulkheads),
        new(1, "End belts, both", "flooding at the ends", Knobs.EndBelts),
        new(1, "Turret faces", "main turrets; the barbettes follow", Knobs.MainFaces),
        new(1, "All armour, scale", "every plate above by the same share", null),
        new(2, "Main calibre", "guns, turrets, barbettes, magazines", Knobs.MainCalibres),
        new(2, "Main rounds per gun", "magazine length, combat endurance", Knobs.MainRoundsAll),
        new(2, "Secondaries per side", "the first battery: anti-torpedo-boat fire", Knobs.SecPerSide(0)),
        new(2, "Light AA mounts", "air defence", Knobs.AaLight),
        new(3, "Freeboard", "wetness, hull girder, GM", Knobs.Freeboard),
        new(3, "Hull form, Cb", "full to fine: resistance, length, beam", Knobs.BlockCoefficient, 0),
        new(3, "Bridge tower", "horizon, top weight", Knobs.TowerLevels),
        new(3, "Stores for", "provisions, fresh water", Knobs.Endurance),
    ];

    // ------------------------------------------------------------------ state

    readonly DesignProbes probes = new();
    sealed record ProbeKey(string Json, UnitSystem Units);
    ProbeKey? ovKey;   // what ovBatch was started for
    DesignProbes.Batch? ovBatch;
    int ovSeen = -1;   // ovBatch.Finished when the cells were last formatted
    readonly string[] ovCell = new string[2 * Levers.Length];   // "−129 t", "…" or "—"
    readonly bool[] ovLumpy = new bool[2 * Levers.Length];   // the step goes against the lever's usual sense
    int? ovForceTip;   // dev hook: draw this cell's tooltip (2 × lever + 1 for the step up)

    int limitPick;   // DesignLimits.Mock; the gameplay layer will hand limits in

    // the all-armour scale: a run of steps scales the design as it was when the run began, so rounding never compounds;
    // any other edit ends the run and the scale reads 100 % again
    Design? scaleFrom, scaleMade;
    int scalePct = 100;
    int ScalePct => ReferenceEquals(doc.Current, scaleMade) ? scalePct : 100;

    DesignLimits Limits => DesignLimits.Mock[Math.Clamp(limitPick, 0, DesignLimits.Mock.Length - 1)];

    // ------------------------------------------------------------------ the snapshot's part

    sealed record OvRow(Lever L, NumRow? Num, string Value, string Step, bool Changed, bool Applies);

    /// <summary>A limits table row: the figure, ≤ or ≥, the limit (empty: none), the value now, the margin.</summary>
    sealed record LimitRow(string Label, string Op, string Limit, string Now, string Margin, bool Over, float Frac);

    sealed record OvPane(OvRow[] Rows, string[] GroupT, LimitRow[] Limits, string Verdict, string VerdictNote, int VerdictKind,
        (float Frac, Vector4 Colour, string Tip)[] Bar, float LimitTick);

    /// <summary>Each limit the design is measured against: label, ≤ (or ≥ for GM), the limit, the value now, its
    /// quantity, and how far past it (0: within).</summary>
    List<(string Label, bool AtMost, double Limit, double Now, Quantity? Q, double Over)> LimitChecks(Design d, Results res)
    {
        var l = Limits;
        var list = new List<(string, bool, double, double, Quantity?, double)>();
        void Add(string label, double? lim, double now, Quantity? q, bool atMost = true)
        {
            if (lim is { } x)
                list.Add((label, atMost, x, now, q, atMost ? Math.Max(0, now - x) : Math.Max(0, x - now)));
        }
        Add("Standard", l.StandardT, res.StandardDisplacementT, null);
        Add("Full load", l.FullT, res.FullDisplacementT, null);
        Add("Length", l.LengthM, res.LengthM, Quantity.Length);
        Add("Beam", l.BeamM, res.BeamM, Quantity.Length);
        Add("Draught", l.DraughtM, res.DraughtM, Quantity.Length);
        Add("GM", l.MinGmM, res.GmFullM, Quantity.Length, atMost: false);
        Add("Main calibre", l.MaxCalibreMm, (d.Main ?? []).Select(b => b.CalibreMm ?? 0).DefaultIfEmpty(0).Max(), Quantity.Calibre);
        return list;
    }

    /// <summary>A limit's figure: tonnes (<paramref name="q"/> null), metres to 0.1, or the quantity's own format.</summary>
    string LimitFmt(double v, Quantity? q) => q switch
    {
        null => N0(v) + " t",
        Quantity.Length => Fmt(v, Quantity.Length, 0.1),
        { } other => Fmt(v, other),
    };

    /// <summary>The remarks a limit adds: one per figure over it (a remark, not an error: nothing is refused).</summary>
    IEnumerable<string> LimitRemarks(Design d, Ship ship)
    {
        foreach (var c in LimitChecks(d, ship.Report.Results))
            if (c.Over > 0)
                yield return $"{c.Label} {LimitFmt(c.Over, c.Q)} {(c.AtMost ? "over" : "short of")} {Limits.Name}: " +
                             $"{LimitFmt(c.Now, c.Q)} against {LimitFmt(c.Limit, c.Q)}. It still builds; the limit only reports.";
    }

    OvPane OvPaneOf(Design d, Ship? ship)
    {
        var rows = new OvRow[Levers.Length];
        for (int i = 0; i < Levers.Length; i++)
        {
            var lv = Levers[i];
            if (lv.Knob is { } k)
            {
                var r = Row(k, d, ship, lv.Label);
                bool changed = k.Value(doc.Baseline, baselineShip) is { } b0 && r.Num is { } v1 && Math.Abs(v1 - b0) > 1e-9;
                string step = Fmt(k, k.Step(units), unit: false);
                rows[i] = new OvRow(lv, r, r.Value, step, changed, r.Applies && r.Num != null);
            }
            else
                rows[i] = new OvRow(lv, null, $"{ScalePct} %", "5 %", ScalePct != 100, d.Armour != null || d.Main is { Count: > 0 });
        }

        var groupT = new string[OvGroups.Length];
        var res = ship?.Report.Results;
        for (int g = 0; g < OvGroups.Length; g++)
            groupT[g] = ship != null ? N0(OvGroups[g].Groups.Sum(x => ship.Report.WeightGroupsT.GetValueOrDefault(x))) + " t" : "";

        var limits = new List<LimitRow>();
        string verdict = "", note = "";
        int kind = 0;   // 0 no limits, 1 within, 2 over
        float tick = -1;
        var bar = new List<(float, Vector4, string)>();
        if (res != null)
        {
            var checks = LimitChecks(d, res);
            var byLabel = checks.ToDictionary(c => c.Label);
            void Show(string label, string op, double now, Quantity? q)
            {
                if (byLabel.TryGetValue(label, out var c))
                {
                    double room = c.AtMost ? c.Limit - c.Now : c.Now - c.Limit;
                    string margin = c.Over > 0 ? LimitFmt(c.Over, q) + (c.AtMost ? " over" : " short") : LimitFmt(room, q) + " spare";
                    limits.Add(new LimitRow(label, op, LimitFmt(c.Limit, q), LimitFmt(now, q), margin, c.Over > 0,
                        (float)Math.Clamp(c.Now / Math.Max(1e-6, c.Limit) / 1.15, 0, 1)));
                }
                else
                    limits.Add(new LimitRow(label, op, "", LimitFmt(now, q), "", false, 0));
            }
            Show("Standard", "max", res.StandardDisplacementT, null);
            Show("Full load", "max", res.FullDisplacementT, null);
            Show("Length", "max", res.LengthM, Quantity.Length);
            Show("Beam", "max", res.BeamM, Quantity.Length);
            Show("Draught", "max", res.DraughtM, Quantity.Length);
            Show("GM", "min", res.GmFullM, Quantity.Length);
            Show("Main calibre", "max", (d.Main ?? []).Select(b => b.CalibreMm ?? 0).DefaultIfEmpty(0).Max(), Quantity.Calibre);

            var over = checks.Where(c => c.Over > 0).ToList();
            if (checks.Count == 0)
                note = "No limits. Pick one above to measure the design against it.";
            else if (over.Count > 0)
            {
                kind = 2;
                verdict = LimitFmt(over[0].Over, over[0].Q);
                note = $"{over[0].Label.ToLowerInvariant()} {(over[0].AtMost ? "over" : "short of")} {Limits.Name}" +
                       (over.Count > 1 ? " · and " + string.Join(", ", over.Skip(1).Select(c => c.Label.ToLowerInvariant())) : "");
            }
            else
            {
                kind = 1;
                var c = byLabel.GetValueOrDefault("Standard", checks[0]);
                verdict = LimitFmt(c.AtMost ? c.Limit - c.Now : c.Now - c.Limit, c.Q);
                note = $"to spare · within {Limits.Name}";
            }

            // the standard displacement by group (fuel isn't in it), against the standard limit
            double std = res.StandardDisplacementT;
            double max = Math.Max(std, Limits.StandardT ?? 0) * 1.04;
            foreach (var (g, t) in ship!.Report.WeightGroupsT.OrderByDescending(kv => kv.Value))
                if (g != "fuel" && t > 0)
                    bar.Add(((float)(t / max), T.Group(g), $"{GroupName(g)}: {N0(t)} t"));
            if (Limits.StandardT is { } lim)
                tick = (float)(lim / max);
        }
        return new OvPane(rows, groupT, limits.ToArray(), verdict, note, kind, bar.ToArray(), tick);
    }

    // ------------------------------------------------------------------ probes

    /// <summary>The design one step of lever <paramref name="i"/> away (<paramref name="dir"/> ±1), as a click on its
    /// arrow would make it; null when the lever doesn't apply or is at its end stop.</summary>
    Design? LeverStep(int i, Design d, Ship? ship, int dir)
    {
        var lv = Levers[i];
        if (lv.Knob is not { } k)
        {
            if (d.Armour == null && d.Main is not { Count: > 0 })
                return null;
            int pct = ScalePct, next = Math.Max(5, pct + 5 * dir);
            if (next == pct)
                return null;
            var scaled = Knobs.ScaleArmour(d, (double)next / pct);
            return scaled.ToJson() == d.ToJson() ? null : scaled;
        }
        if (!k.AppliesTo(d) || k.Value(d, ship) is not { } v)
            return null;
        var stepped = k.Apply(d, StepTarget(k, v, dir));
        return k.Value(stepped, ship) is { } nv && Math.Abs(nv - v) > 1e-9 ? stepped : null;
    }

    /// <summary>Keeps the probes on the design shown while the Overview is open (one batch per design and units: the
    /// steps are in the units shown), and formats each cell once, when its build lands.</summary>
    void PollProbes()
    {
        if (section != 0)
        {
            if (ovBatch != null)
            {
                probes.Cancel();
                (ovBatch, ovKey) = (null, null);
            }
            return;
        }
        var json = doc.History[doc.Index].Json;
        if (ovKey == null || !ReferenceEquals(ovKey.Json, json) || ovKey.Units != units)
        {
            var d = doc.Current;
            var ship = shown?.Ship;
            var designs = new Design?[1 + 2 * Levers.Length];
            designs[0] = d;
            for (int i = 0; i < Levers.Length; i++)
            {
                designs[1 + 2 * i] = LeverStep(i, d, ship, -1);
                designs[2 + 2 * i] = LeverStep(i, d, ship, 1);
            }
            ovKey = new ProbeKey(json, units);
            ovBatch = probes.Start(ovKey, designs);
            ovSeen = -1;
            Array.Fill(ovCell, "…");
            Array.Clear(ovLumpy);
        }
        var b = ovBatch!;
        int finished = b.Finished;
        if (finished == ovSeen)
            return;
        ovSeen = finished;
        bool hasBase = b.TryGet(0, out var r0);
        for (int c = 0; c < ovCell.Length; c++)
        {
            switch (b.StateOf(c + 1))
            {
                case DesignProbes.State.Skipped:
                case DesignProbes.State.Failed:
                    ovCell[c] = "—";
                    break;
                case DesignProbes.State.Built when hasBase && ReferenceEquals(ovCell[c], "…"):
                    b.TryGet(c + 1, out var r);
                    double dt = r.StdT - r0.StdT;
                    ovCell[c] = (dt > 0.5 ? "+" : dt < -0.5 ? "−" : "±") + N0(Math.Abs(dt)) + " t";
                    int sense = Levers[c / 2].Sense * (c % 2 == 0 ? -1 : 1);
                    ovLumpy[c] = sense != 0 && Math.Sign(dt) == -sense && Math.Abs(dt) > 0.5;
                    break;
                case DesignProbes.State.Built when !hasBase && b.StateOf(0) == DesignProbes.State.Failed:
                    ovCell[c] = "—";
                    break;
            }
        }
    }

    // ------------------------------------------------------------------ drawing

    static readonly float[] OvColW = [158, 152, 54, 92, 92, 0, 40];   // 0: what it drives takes what's left
    readonly float[] ovX = new float[OvColW.Length + 1];
    static readonly string[] OvHead = ["LEVER", "VALUE", "STEP", "ONE STEP −", "ONE STEP +", "DRIVES", ""];

    void OverviewPane(Vector2 size)
    {
        var op = snap!.Ov!;
        float leftW = MathF.Round(Math.Clamp(size.X * 0.62f, P(700), size.X - P(380)));
        ImGui.BeginChild("##trim", new Vector2(leftW, size.Y), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollWithMouse);
        TrimTable(op, leftW - (ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ScrollbarSize : 0));
        ImGui.EndChild();
        ImGui.SameLine(0, 0);
        var p = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(p, p + new Vector2(0, size.Y), T.U32(T.Edge));
        ImGui.SetCursorScreenPos(p + new Vector2(1, 0));
        ImGui.BeginChild("##limits", new Vector2(size.X - leftW - 1, size.Y), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        LimitsPanel(op, size.X - leftW - 1);
        ImGui.EndChild();
    }

    void TrimTable(OvPane op, float w)
    {
        var dl = ImGui.GetWindowDrawList();
        var b = ovBatch;
        string probeNote = b == null ? "" : b.Done ? $"{b.Count - 1} probe builds, one step each way" : $"probing {b.Finished}/{b.Count}";
        SubHeader(dl, w, "TRIM SHEET · broad strokes; the tabs hold the detail", probeNote);
        if (b is { Done: false })   // a progress bar under the note
        {
            var e = ImGui.GetCursorScreenPos() + new Vector2(w - P(6), -P(3));
            float bw = P(90);
            dl.AddRectFilled(e - new Vector2(bw, 0), e + new Vector2(0, P(2)), T.U32(T.Bevel));
            dl.AddRectFilled(e - new Vector2(bw, 0), e + new Vector2(bw * b.Finished / b.Count - bw, P(2)), T.U32(T.Brass));
        }

        var o = ImGui.GetCursorScreenPos();
        var x = ovX;
        float fixedW = 0;
        foreach (var c in OvColW)
            fixedW += P(c);
        x[0] = o.X;
        for (int i = 0; i < OvColW.Length; i++)
            x[i + 1] = x[i] + (OvColW[i] > 0 ? P(OvColW[i]) : Math.Max(P(120), w - fixedW));
        float hh = P(17);
        dl.AddRectFilled(o, o + new Vector2(w, hh), T.U32(T.HeaderBg));
        dl.AddLine(o + new Vector2(0, hh - 1), o + new Vector2(w, hh - 1), T.U32(T.Edge));
        for (int i = 0; i < OvHead.Length; i++)
        {
            float tx = i is 3 or 4 ? x[i + 1] - P(8) - Width(UiFonts.SansSmall, OvHead[i]) : x[i] + P(6);
            Text(dl, UiFonts.SansSmall, new Vector2(tx, o.Y + (hh - UiFonts.SansSmall.FontSize) / 2), T.Muted, OvHead[i]);
        }

        float y = o.Y + hh, gh = P(19), fh = ImGui.GetFrameHeight();
        int n = 0;
        for (int g = 0; g < OvGroups.Length; g++)
        {
            // the group's heading: its weight, and a link to its tab
            var gp = new Vector2(o.X, y);
            ImGui.SetCursorScreenPos(gp);
            ImGui.PushID(500 + g);
            if (ImGui.InvisibleButton("##g", new Vector2(w, gh)))
                Open(OvGroups[g].Tab);
            bool gHover = ImGui.IsItemHovered();
            ImGui.PopID();
            dl.AddRectFilled(gp, gp + new Vector2(w, gh), T.U32(new Vector4(0.098f, 0.125f, 0.149f, 1)));
            dl.AddLine(gp + new Vector2(0, gh - 1), gp + new Vector2(w, gh - 1), T.U32(T.Edge));
            float ty = gp.Y + (gh - UiFonts.SansSmall.FontSize) / 2;
            Text(dl, UiFonts.SansSmall, new Vector2(x[0] + P(6), ty), T.Brass, OvGroups[g].Name);
            Text(dl, UiFonts.Mono, new Vector2(x[0] + P(12) + Width(UiFonts.SansSmall, OvGroups[g].Name), gp.Y + (gh - UiFonts.Mono.FontSize) / 2),
                T.Muted, op.GroupT[g]);
            string go = OvTabLink[g];
            Text(dl, UiFonts.SansSmall, new Vector2(x[^1] - P(8) - Width(UiFonts.SansSmall, go), ty), gHover ? T.Brass : T.Muted, go);
            y += gh;

            for (int i = 0; i < Levers.Length; i++)
            {
                if (Levers[i].Group != g)
                    continue;
                LeverRow(dl, op.Rows[i], i, new Vector2(o.X, y), w, n++, fh);
                y += RowH;
            }
        }
        ImGui.SetCursorScreenPos(new Vector2(o.X, y));
        ImGui.Dummy(new Vector2(w, P(4)));
    }

    static readonly string[] OvTabLink = ["4 · open tab ›", "3 · open tab ›", "1 · open tab ›", "5 · open tab ›"];

    void LeverRow(ImDrawListPtr dl, OvRow r, int i, Vector2 p, float w, int n, float fh)
    {
        var x = ovX;
        RowBg(dl, p, w, n);
        if (r.Changed)
            dl.AddRectFilled(p, p + new Vector2(w, RowH), T.U32(T.Brass with { W = 0.06f }));
        float cy = p.Y + (RowH - fh) / 2;
        float ty = p.Y + (RowH - UiFonts.Sans.FontSize) / 2, my = p.Y + (RowH - UiFonts.Mono.FontSize) / 2;
        Text(dl, UiFonts.Sans, new Vector2(x[0] + P(6), ty), r.Applies ? T.Text : T.Muted, r.L.Label);

        ImGui.PushID(i);
        ImGui.SetCursorScreenPos(new Vector2(x[1] + P(4), cy));
        ImGui.BeginDisabled(!r.Applies);
        if (r.Num is { } num)
            Spinner(num, P(104), extras: false);
        else
            ScaleSpinner(r, P(104));
        ImGui.EndDisabled();
        Text(dl, UiFonts.Mono, new Vector2(x[3] - P(8) - Width(UiFonts.Mono, r.Step), my), T.Muted, r.Step);

        // one step each way: the probe's figure, a click takes the step
        for (int dir = -1; dir <= 1; dir += 2)
        {
            int c = 2 * i + (dir > 0 ? 1 : 0);
            string cell = r.Applies ? ovCell[c] ?? "…" : "—";
            bool live = r.Applies && cell != "—";   // a cell still building takes its step too: the step doesn't wait on the probe
            var a = new Vector2(x[dir < 0 ? 3 : 4] + P(4), p.Y + P(2));
            var size = new Vector2(P(OvColW[3]) - P(8), RowH - P(4));
            ImGui.SetCursorScreenPos(a);
            ImGui.PushID(dir);
            bool clicked = ImGui.InvisibleButton("##d", size) && live;
            bool hover = ImGui.IsItemHovered() && live;
            ImGui.PopID();
            if (hover)
            {
                dl.AddRectFilled(a, a + size, T.U32(T.Inset));
                dl.AddRect(a, a + size, T.U32(T.Bevel));
            }
            bool lumpy = live && ovLumpy[c];
            var col = !live || cell == "…" ? T.Muted : lumpy ? T.Amber : hover ? T.Bright : T.Text;
            float cw = Width(UiFonts.Mono, cell), cxr = a.X + size.X - P(4) - cw;
            Text(dl, UiFonts.Mono, new Vector2(cxr, my), col, cell);
            if (lumpy)   // a warning triangle: this step goes against the lever's usual sense
            {
                float tm = p.Y + RowH / 2, tx = cxr - P(12);
                dl.AddTriangleFilled(new Vector2(tx, tm + P(5)), new Vector2(tx + P(9), tm + P(5)), new Vector2(tx + P(4.5f), tm - P(4)), T.U32(T.Amber));
                dl.AddLine(new Vector2(tx + P(4.5f), tm - P(1)), new Vector2(tx + P(4.5f), tm + P(2)), T.U32(T.Panel), P(1.2f));
            }
            if ((hover || ovForceTip == c) && ovBatch is { } b && b.TryGet(0, out var r0) && b.TryGet(c + 1, out var r1))
            {
                string tip = DeltaTip(r, dir, r0, r1, lumpy);
                if (hover)
                    Tip(tip);
                else
                    TipAt(tip, a + new Vector2(0, size.Y + P(4)));
            }
            if (clicked)
                StepLever(r, dir);
        }

        // what it drives, clipped to its column
        dl.PushClipRect(new Vector2(x[5], p.Y), new Vector2(x[6] - P(4), p.Y + RowH), true);
        Text(dl, UiFonts.SansSmall, new Vector2(x[5] + P(6), p.Y + (RowH - UiFonts.SansSmall.FontSize) / 2), T.Muted,
            r.Applies || r.Num == null ? r.L.Drives : LeverOff(r.L));
        dl.PopClipRect();

        if (r.Changed)
        {
            ImGui.SetCursorScreenPos(new Vector2(x[6] + P(2), cy));
            ImGui.PushFont(UiFonts.SansSmall);
            if (ImGui.SmallButton("reset"))
                ResetLever(r);
            ImGui.PopFont();
            if (ImGui.IsItemHovered())
                Tip("Back to the value as opened.");
        }
        ImGui.PopID();
    }

    static string LeverOff(Lever l) => l.Knob?.Id switch
    {
        "end_belts" => "no end belts: add them on the Protection tab",
        "sec0_per_side" => "no secondary battery in pairs",
        _ => "doesn't apply to this design",
    };

    string DeltaTip(OvRow r, int dir, DesignProbes.Result a, DesignProbes.Result b, bool lumpy)
    {
        string to = $"{Math.Max(5, ScalePct + 5 * dir)} %";
        if (r.Num is { Num: { } v } num && num.Knob.Value(num.Knob.Apply(doc.Current, StepTarget(num.Knob, v, dir)), null) is { } nv)
            to = Fmt(num.Knob, nv);
        string Sig(double v, string f) => (v > 0 ? "+" : v < 0 ? "−" : "±") + Math.Abs(v).ToString(f, System.Globalization.CultureInfo.InvariantCulture);
        string text = $"{r.L.Label} to {to}: one probe build\n\n" +
                      $"Standard   {Sig(b.StdT - a.StdT, "N0")} t\n" +
                      $"Full load  {Sig(b.FullT - a.FullT, "N0")} t\n" +
                      $"Length     {Sig(b.LengthM - a.LengthM, "0.0")} m\n" +
                      $"Beam       {Sig(b.BeamM - a.BeamM, "0.00")} m\n" +
                      $"Draught    {Sig(b.DraughtM - a.DraughtM, "0.00")} m\n" +
                      $"GM         {Sig(b.GmM - a.GmM, "0.00")} m";
        if (lumpy)
            text += $"\n\nA step {(dir < 0 ? "down adds" : "up saves")} weight here: the hull settles on another form " +
                    $"(beam {Sig(b.BeamM - a.BeamM, "0.00")} m, draught {Sig(b.DraughtM - a.DraughtM, "0.00")} m). That's the physics; two steps may come out differently.";
        return text + "\n\nClick to take the step.";
    }

    void StepLever(OvRow r, int dir)
    {
        if (r.Num is { } num)
        {
            Step(num, dir);
            doc.Seal();
        }
        else
            ScaleStep(dir);
    }

    void ResetLever(OvRow r)
    {
        if (r.Num is { } num)
        {
            SetValue(num.Knob, num.Knob.Get(doc.Baseline));
            doc.Seal();
        }
        else if (scaleFrom != null && ReferenceEquals(doc.Current, scaleMade))
        {
            Edit(scaleFrom, "All armour back to 100 %");
            doc.Seal();
        }
    }

    /// <summary>The all-armour scale as a spinner: ◂ [95 %] ▸, the wheel over the value steps it.</summary>
    void ScaleSpinner(OvRow r, float valueW)
    {
        ImGui.PushID("scale");
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        int shift = ImGui.GetIO().KeyShift ? 4 : 1;
        if (ImGui.ArrowButton("##m", ImGuiDir.Left))
            ScaleStep(-shift);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, T.Inset);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, T.Inset);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, T.Inset);
        ImGui.PushStyleColor(ImGuiCol.Text, T.Bright);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(1, 0.5f));
        ImGui.PushFont(UiFonts.Mono);
        ImGui.Button(r.Value, new Vector2(valueW, 0));
        ImGui.PopFont();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY);
            if (ImGui.GetIO().MouseWheel is var wheel && wheel != 0)
                ScaleStep(Math.Sign(wheel) * shift);
            Tip("Every armour thickness the design gives, scaled together from the armour as it was when you started " +
                "scaling. Any other edit sets the scale back to 100 % of the armour as it then is.");
        }
        ImGui.SameLine();
        if (ImGui.ArrowButton("##p", ImGuiDir.Right))
            ScaleStep(shift);
        ImGui.PopStyleVar();
        ImGui.PopID();
    }

    void ScaleStep(int steps)
    {
        int pct = ScalePct, next = Math.Max(5, pct + 5 * steps);
        if (next == pct)
            return;
        if (!ReferenceEquals(doc.Current, scaleMade))
            scaleFrom = doc.Current;
        var before = doc.History[doc.Index];
        Edit(Knobs.ScaleArmour(scaleFrom!, next / 100.0), $"All armour 100 % to {next} %", "armour_scale");
        if (ReferenceEquals(doc.History[doc.Index], before))
            return;   // nothing to scale
        scalePct = next;
        scaleMade = doc.Current;
    }

    void LimitsPanel(OvPane op, float w)
    {
        var dl = ImGui.GetWindowDrawList();
        var p0 = ImGui.GetCursorScreenPos();
        SubHeader(dl, w, "LIMITS · they report, never refuse", null);
        // the pick, over the header's right end
        float comboW = P(210);
        ImGui.SetCursorScreenPos(p0 + new Vector2(w - comboW - P(3), P(1)));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(5), P(1)));
        ImGui.SetNextItemWidth(comboW);
        ImGui.PushFont(UiFonts.SansSmall);
        if (ImGui.BeginCombo("##limits", Limits.Name))
        {
            for (int i = 0; i < DesignLimits.Mock.Length; i++)
            {
                var l = DesignLimits.Mock[i];
                if (ImGui.Selectable(l.Name, i == limitPick))
                {
                    limitPick = i;
                    stale = true;
                }
                if (ImGui.IsItemHovered() && l.Note.Length > 0)
                    Tip(l.Note);
            }
            ImGui.EndCombo();
        }
        ImGui.PopFont();
        ImGui.PopStyleVar();
        ImGui.SetCursorScreenPos(p0 + new Vector2(0, P(18)));

        // the table: figure, ≤, limit, now, margin, bar
        var o = ImGui.GetCursorScreenPos();
        float hh = P(17);
        float[] cx = [o.X + P(8), o.X + P(96), o.X + P(118), o.X + P(214), o.X + P(312), o.X + P(420)];
        dl.AddRectFilled(o, o + new Vector2(w, hh), T.U32(T.HeaderBg));
        dl.AddLine(o + new Vector2(0, hh - 1), o + new Vector2(w, hh - 1), T.U32(T.Edge));
        float hy = o.Y + (hh - UiFonts.SansSmall.FontSize) / 2;
        Text(dl, UiFonts.SansSmall, new Vector2(cx[3] - Width(UiFonts.SansSmall, "LIMIT"), hy), T.Muted, "LIMIT");
        Text(dl, UiFonts.SansSmall, new Vector2(cx[4] - Width(UiFonts.SansSmall, "NOW"), hy), T.Muted, "NOW");
        Text(dl, UiFonts.SansSmall, new Vector2(cx[5] - Width(UiFonts.SansSmall, "MARGIN"), hy), T.Muted, "MARGIN");
        float y = o.Y + hh;
        float rh = P(19);
        for (int i = 0; i < op.Limits.Length; i++)
        {
            var r = op.Limits[i];
            var p = new Vector2(o.X, y);
            bool set = r.Limit.Length > 0;
            RowBg(dl, p, w, i);
            float ty = p.Y + (rh - UiFonts.Sans.FontSize) / 2, my = p.Y + (rh - UiFonts.Mono.FontSize) / 2;
            Text(dl, UiFonts.Sans, new Vector2(cx[0], ty), set ? T.Text : T.Muted, r.Label);
            Text(dl, UiFonts.Mono, new Vector2(cx[1], my), T.Muted, r.Op);
            string lim = set ? r.Limit : "—";
            Text(dl, UiFonts.Mono, new Vector2(cx[3] - Width(UiFonts.Mono, lim), my), set ? T.Bright : T.Muted, lim);
            Text(dl, UiFonts.Mono, new Vector2(cx[4] - Width(UiFonts.Mono, r.Now), my), set ? T.Bright : T.Text, r.Now);
            if (set)
            {
                Text(dl, UiFonts.Mono, new Vector2(cx[5] - Width(UiFonts.Mono, r.Margin), my), r.Over ? T.Amber : T.Ok, r.Margin);
                float bx = cx[5] + P(10), bw = w - (bx - o.X) - P(10);
                if (bw > P(20))
                {
                    var b0 = new Vector2(bx, p.Y + rh / 2 - P(3));
                    dl.AddRectFilled(b0, b0 + new Vector2(bw, P(6)), T.U32(T.Inset));
                    dl.AddRectFilled(b0, b0 + new Vector2(bw * r.Frac, P(6)), T.U32(r.Over ? T.Amber : new Vector4(0.31f, 0.38f, 0.44f, 1)));
                    float tx = bx + bw / 1.15f;
                    dl.AddLine(new Vector2(tx, b0.Y - P(3)), new Vector2(tx, b0.Y + P(9)), T.U32(T.Bright), P(2));
                }
            }
            y += rh;
        }

        // the verdict
        y += P(8);
        var v0 = new Vector2(o.X + P(8), y);
        var v1 = new Vector2(o.X + w - P(8), y + P(34));
        var (bg, edge, ink) = op.VerdictKind switch
        {
            2 => (new Vector4(0.18f, 0.153f, 0.09f, 1), T.BrassDark, new Vector4(0.945f, 0.843f, 0.612f, 1)),
            1 => (new Vector4(0.11f, 0.2f, 0.161f, 1), new Vector4(0.184f, 0.353f, 0.251f, 1), new Vector4(0.624f, 0.867f, 0.718f, 1)),
            _ => (T.Panel2, T.Bevel, T.Muted),
        };
        dl.AddRectFilled(v0, v1, T.U32(bg));
        dl.AddRect(v0, v1, T.U32(edge));
        float vx = v0.X + P(10);
        if (op.Verdict.Length > 0)
        {
            Text(dl, UiFonts.MonoBig, new Vector2(vx, (v0.Y + v1.Y - UiFonts.MonoBig.FontSize) / 2), ink, op.Verdict);
            vx += Width(UiFonts.MonoBig, op.Verdict) + P(10);
        }
        dl.PushClipRect(v0, v1, true);
        Text(dl, UiFonts.Sans, new Vector2(vx, (v0.Y + v1.Y - UiFonts.Sans.FontSize) / 2), ink, op.VerdictNote);
        dl.PopClipRect();
        y = v1.Y + P(8);

        // the weight bar: standard by group, the standard limit as a tick
        var w0 = new Vector2(o.X + P(8), y);
        float ww = w - P(16), wh = P(16);
        dl.AddRectFilled(w0, w0 + new Vector2(ww, wh), T.U32(T.Inset));
        float bxp = w0.X;
        foreach (var (frac, colour, tip) in op.Bar)
        {
            float sw = ww * frac;
            dl.AddRectFilled(new Vector2(bxp, w0.Y), new Vector2(bxp + sw, w0.Y + wh), T.U32(colour));
            dl.AddLine(new Vector2(bxp + sw, w0.Y), new Vector2(bxp + sw, w0.Y + wh), T.U32(new Vector4(0, 0, 0, 0.5f)));
            if (ImGui.IsMouseHoveringRect(new Vector2(bxp, w0.Y), new Vector2(bxp + sw, w0.Y + wh)))
                Tip(tip);
            bxp += sw;
        }
        dl.AddRect(w0, w0 + new Vector2(ww, wh), T.U32(T.Edge));
        if (op.LimitTick >= 0)
        {
            float tx = w0.X + ww * Math.Min(1, op.LimitTick);
            dl.AddLine(new Vector2(tx, w0.Y - P(4)), new Vector2(tx, w0.Y + wh + P(4)), T.U32(T.Bright), P(2));
        }
        y += wh + P(4);
        Text(dl, UiFonts.SansSmall, new Vector2(o.X + P(8), y), T.Muted, "standard displacement by weight group · hover a part");
        y += P(22);

        // Fit: a placeholder for the autodesigner
        ImGui.SetCursorScreenPos(new Vector2(o.X, y));
        SubHeader(dl, w, "FIT TO THE LIMITS", "coming with the autodesigner");
        ImGui.SetCursorScreenPos(new Vector2(o.X + P(8), ImGui.GetCursorScreenPos().Y + P(6)));
        ImGui.BeginDisabled();
        ImGui.Button("Fit with one lever");
        ImGui.SameLine(0, P(6));
        ImGui.Button("Fit evenly");
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            Tip("Fit will trim the design to the limits for you. It comes with the autodesigner, the same one the AI designs " +
                "its ships with. Until then, the one-step columns show what each lever buys.");
        ImGui.SetCursorScreenPos(new Vector2(o.X + P(8), ImGui.GetCursorScreenPos().Y + P(4)));
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.PushTextWrapPos(o.X - ImGui.GetWindowPos().X + w - P(8));
        ImGui.TextColored(T.Muted, "The autodesigner, shared with the AI, will trim to the limits: one lever alone, or every unlocked lever evenly.");
        ImGui.PopTextWrapPos();
        ImGui.PopFont();
    }
}
