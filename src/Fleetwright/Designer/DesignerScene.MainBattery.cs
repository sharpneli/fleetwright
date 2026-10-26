using System.Globalization;
using System.Numerics;
using Fleetwright.Shipgen;
using ImGuiNET;
using G = Fleetwright.Designer.MainArrangement.Group;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Designer;

/// <summary>
/// The Main battery tab, after docs/designer-ui/mainbattery.md: a table of the batteries (what each gun is), the
/// arrangement (where the turrets are: + slots add the active battery's turret, the step between two turrets toggles
/// superfiring, right-click for the rest), the guns bearing by bearing, the arrangement presets and the selected
/// turret. The edits are <see cref="MainArrangement"/>'s; the numbers go through the battery knobs.
/// </summary>
public sealed unsafe partial class DesignerScene
{
    int mainActive;   // the battery the + slots add
    MainArrangement.Slot? mainSel;   // the selected turret
    MainArrangement.Slot menuSlot;   // the turret the context menu is for
    string? planFocus;   // the mount the plan rings (the hovered or selected turret), by name
    bool openChipMenu;   // open the context menu (asked for under another ID scope)

    // the calibre field being typed into
    string? fieldId;
    string fieldText = "";

    static readonly Vector4[] BatteryColours = [T.Blue, Hex(0xD98B5F), Hex(0x8BC48A), Hex(0xB48CD9), Hex(0xD9C85F), Hex(0x5FC8D9)];
    static readonly string[] TierText = ["", "↑1", "↑2", "↑3", "↑4", "↑5", "↑6", "↑7"];
    static readonly string[] BarrelText = ["1", "2", "3", "4", "5", "6"];
    static readonly string[] Letters = Enumerable.Range(0, 26).Select(i => ((char)('A' + i)).ToString()).ToArray();

    static Vector4 Hex(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
    static Vector4 BatteryColour(int k) => BatteryColours[k % BatteryColours.Length];
    static string Letter(int k) => k < Letters.Length ? Letters[k] : (k + 1).ToString(CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ the snapshot's part

    sealed record BatteryRow(int K, NumRow Cal, NumRow Len, int Barrels, string Positions, string Turrets, string Guns, NumRow Faces,
        NumRow Rounds, ShellCell? Shells, string Tonnes);

    /// <summary>A turret (or a + slot) on the schematic, in its 940 × 280 units: bow right, starboard down.</summary>
    /// A + slot's InsertX: where between the turrets the new one actually goes, when that isn't the slot itself (NaN).
    sealed record Chip(MainArrangement.Slot Slot, float X, float Y, float R, int Barrels, float Face, string Tag, int Tier, string Tip, bool Ghost,
        float InsertX = float.NaN);

    /// <summary>The step between an end's (J-1)-th and J-th turrets.</summary>
    sealed record StepLink(G End, int J, float X, float Y, bool On, string Tip);

    sealed record WaistRow(int K, string Label, bool Wing, bool Echelon, bool Cross, bool Deckhouse);

    sealed record Selected(MainArrangement.Slot Slot, string Title, string Battery, string Where, string Arcs, bool CanStep, bool Stepped);

    sealed record MainPane(BatteryRow[] Rows, Chip[] Chips, StepLink[] Links, WaistRow[] Waist, float[] Rose, float RoseMax,
        (string Label, string Value, string Note)[] RoseText, Selected? Sel, bool HasMid, bool HasWing, bool Raised, bool TakesList,
        string ActiveNote);

    // GhostR: the + slots' size, whatever the active battery, so picking another battery moves nothing
    const float SchW = 940, SchH = 280, SchCy = 136, SchBow = 858, SchStern = 82, WingDy = 62, EchelonDx = 26, GhostR = 18;

    static float ChipR(BatteryInput b) => 9 + (float)(b.CalibreMm ?? 152) / 38 + ((b.Barrels ?? 1) - 1) * 1.6f;

    MainPane MainPaneOf(Design d, Ship? ship, Overlay? over)
    {
        var mains = d.Main ?? [];
        var style = Styles.Get(d);
        mainActive = Math.Clamp(mainActive, 0, Math.Max(0, mains.Count - 1));
        var slots = MainArrangement.Slots(d);
        if (mainSel is { } ms && !slots.Contains(ms))
            mainSel = null;
        string Gun(BatteryInput b) => $"{b.Barrels ?? 1} × {Fmt(b.CalibreMm ?? 0, Quantity.Calibre)}";

        // turret tonnes by battery: each built turret's weight, shared among the batteries of its turret type
        var tonnes = new double[mains.Count];
        if (ship != null)
        {
            var tids = mains.Select(b => b.CalibreMm != null && b.CalibreLength != null && b.Barrels != null ? Geometry.BatteryType(b).Id : "").ToArray();
            var weight = ship.Report.Weights.Where(w => w.Name.StartsWith("Turret ", StringComparison.Ordinal))
                .GroupBy(w => w.Name[7..]).ToDictionary(g => g.Key, g => g.Sum(w => w.T));
            foreach (var c in ship.Hitboxes.Components)
                if (c.Kind == ComponentKind.Main && c.Type != null && weight.TryGetValue(c.Id, out double t))
                {
                    double n = 0;
                    for (int k = 0; k < mains.Count; k++)
                        if (tids[k] == c.Type)
                            n += mains[k].Turrets;
                    for (int k = 0; k < mains.Count && n > 0; k++)
                        if (tids[k] == c.Type)
                            tonnes[k] += t * mains[k].Turrets / n;
                }
        }

        var rows = new BatteryRow[mains.Count];
        for (int k = 0; k < mains.Count; k++)
        {
            var b = mains[k];
            var rounds = Row(Knobs.MainRounds(k), d, ship) with { Delta = "" };
            rows[k] = new BatteryRow(k, Row(Knobs.MainCalibre(k), d, ship) with { Delta = "" }, Row(Knobs.MainLength(k), d, ship) with { Delta = "" },
                b.Barrels ?? 1, MainArrangement.Positions(d, k, slots), b.Turrets.ToString(CultureInfo.InvariantCulture),
                (b.Turrets * (b.Barrels ?? 1)).ToString(CultureInfo.InvariantCulture), Row(Knobs.MainArmour(k), d, ship) with { Delta = "" },
                rounds, ShellCellOf(d, true, k, Letter(k), rounds.Num), ship != null ? N0(tonnes[k]) : "–");
        }

        // the schematic: the ends from the bow and stern inwards (the active battery's + slots in their true places), the
        // waist spread between them
        var chips = new List<Chip>();
        var links = new List<StepLink>();
        bool any = mains.Count > 0;
        foreach (var end in new[] { G.Fore, G.Aft })
        {
            var (order, run) = MainArrangement.End(d, end);
            float dir = end == G.Fore ? -1 : 1, x = end == G.Fore ? SchBow : SchStern, prevR = 0;
            string letters = end == G.Fore ? "ABC" : "YXW";
            int first = chips.Count;
            Chip? prev = null;
            // the turrets as the design has them; the layout depends on nothing else
            for (int idx = 0; idx < order.Count; idx++)
            {
                int k = order[idx];
                var b = mains[k];
                float r = ChipR(b);
                if (idx > 0)
                    x += dir * (prevR + r + 38);
                prevR = r;
                string name = Layout.TurretName(letters, idx);
                var slot = slots.First(s => s.Group == end && s.Index == idx);
                bool firstOfBattery = idx == 0 || order[idx - 1] != k;
                string place = slot.Tier > 0 ? $"tier {slot.Tier}: fires over the turret{(slot.Tier > 1 ? "s" : "")} outboard of it"
                    : slot.Blocked ? "on the deck behind another turret: no arc over it" : "on the deck, outermost";
                var chip = new Chip(slot, x, SchCy, r, b.Barrels ?? 1, end == G.Fore ? 0 : 180, firstOfBattery ? $"{Letter(k)} {Gun(b)}" : "", slot.Tier,
                    $"{name} · battery {Letter(k)} · {Gun(b)}\n{place}\nclick selects · right-click: battery, move, remove · Del removes", false);
                if (prev != null)
                {
                    float mx = prev.X + dir * (prev.R + 19);   // the middle of the gap
                    bool on = slot.Tier > 0;
                    string tip = on
                        ? $"{name} superfires over {prev.Slot.Name}\nclick: put {name} on the deck{(idx + 1 < order.Count ? " (the turrets inboard of it come down too)" : "")}"
                        : $"{name} on the deck\nclick: step {name} up to fire over {prev.Slot.Name}{(idx > 1 ? " (and everything outboard of it)" : "")}";
                    links.Add(new StepLink(end, idx, mx, SchCy, on, tip));
                }
                chips.Add(chip);
                prev = chip;
            }
            // the + slot, inboard of them all; when the active battery's turret goes further out (battery order), hovering
            // it marks the gap it goes into
            if (!any)
                continue;
            int ins = MainArrangement.InsertAt(d, mainActive, end);
            bool stepped = MainArrangement.AddsStepped(d, mainActive, end, flush: false);
            float gx = order.Count == 0 ? x : x + dir * (prevR + GhostR + 38);
            float insertX = ins >= order.Count ? float.NaN : chips[first + ins].X - dir * (chips[first + ins].R + 19);
            var ab = mains[mainActive];
            string gname = Layout.TurretName(letters, ins);
            string where = stepped ? ", superfiring" : ins > 0 ? ", on the deck" : "";
            string why = ins < order.Count ? $"\nIt goes after battery {Letter(mainActive)}'s own turrets (marked): battery order sets who is outboard." : "";
            chips.Add(new Chip(new MainArrangement.Slot(mainActive, end, -1, gname, 0, false, 0), gx, SchCy, GhostR, ab.Barrels ?? 1, end == G.Fore ? 0 : 180,
                "", 0, $"+ {gname}{where}\nbattery {Letter(mainActive)} · {Gun(ab)}{why}\nShift+click: on the deck instead", true, insertX));
        }
        float foreIn = chips.Where(c => c.Slot.Group == G.Fore).Select(c => c.X - c.R).DefaultIfEmpty(SchBow).Min();
        float aftIn = chips.Where(c => c.Slot.Group == G.Aft).Select(c => c.X + c.R).DefaultIfEmpty(SchStern).Max();
        var waist = slots.Where(s => s.Group == G.Wing && s.Side < 0).Concat(slots.Where(s => s.Group == G.Mid)).ToList();
        bool waistGhost = any && (style.HasMidshipsTurrets || style.HasWingTurrets);
        int nw = waist.Count + (waistGhost ? 1 : 0);
        float a = aftIn + 60, f = foreIn - 60;
        for (int i = 0; i < nw; i++)
        {
            float x = nw == 1 ? (a + f) / 2 : f - (f - a) * i / (nw - 1);
            if (i == waist.Count)
            {
                var b = mains[mainActive];
                float r = GhostR;
                if (style.HasMidshipsTurrets)
                    chips.Add(new Chip(new MainArrangement.Slot(mainActive, G.Mid, -1, "Q", 0, false, 0), x, SchCy, r, b.Barrels ?? 1, 0, "", 0,
                        $"+ a midships turret\nbattery {Letter(mainActive)} · {Gun(b)}", true));
                if (style.HasWingTurrets)
                    foreach (int side in new[] { -1, 1 })
                        chips.Add(new Chip(new MainArrangement.Slot(mainActive, G.Wing, -1, "W", 0, false, side), x, SchCy + side * WingDy, r, b.Barrels ?? 1,
                            side < 0 ? 270 : 90, "", 0, $"+ a wing pair\nbattery {Letter(mainActive)} · {Gun(b)}", true));
                continue;
            }
            var s = waist[i];
            var bb = mains[s.Battery];
            float rr = ChipR(bb);
            string tag = $"{Letter(s.Battery)} {Gun(bb)}";
            if (s.Group == G.Mid)
            {
                chips.Add(new Chip(s, x, SchCy, rr, bb.Barrels ?? 1, 0, tag, 0,
                    $"{s.Name} · battery {Letter(s.Battery)} · {Gun(bb)}\nmidships, on the centreline\nclick selects · right-click: battery, move, remove", false));
                continue;
            }
            float st = bb.Echelon == true ? EchelonDx : 0;
            foreach (var w in slots.Where(q => q.Group == G.Wing && q.Index == s.Index))
                chips.Add(new Chip(w, x - w.Side * st, SchCy + w.Side * WingDy, rr, bb.Barrels ?? 1, w.Side < 0 ? 270 : 90, tag, 0,
                    $"{w.Name} · battery {Letter(w.Battery)} · {Gun(bb)}\nwing, {(w.Side < 0 ? "port" : "starboard")}{(bb.Echelon == true ? ", in echelon" : "")}\n" +
                    "click selects · right-click: battery, move, remove (the pair)", false));
        }

        var waistRows = new List<WaistRow>();
        for (int k = 0; k < mains.Count; k++)
            if ((mains[k].Wing ?? 0) + (mains[k].Mid ?? 0) > 0)
                waistRows.Add(new WaistRow(k, $"{Letter(k)} {((mains[k].Wing ?? 0) > 0 ? "wings" : "midships")}", (mains[k].Wing ?? 0) > 0,
                    mains[k].Echelon == true, mains[k].CrossDeck == true, mains[k].AmidshipsStandsOn == "deckhouse"));

        // guns bearing, from the built ship's main mounts: bin i is the bearing i + ½° (an arc that ends on the bow line
        // doesn't count as bearing ahead)
        var rose = new float[360];
        var mountArcs = new Dictionary<string, string>();
        int total = 0;
        if (over != null)
            foreach (var m in over.Mounts)
            {
                if (!m.Main)
                    continue;
                total += m.Guns;
                foreach (var (from, to) in m.Arcs)
                    for (int deg = (int)MathF.Floor(from - 0.5f); deg + 0.5f <= to; deg++)
                        if (deg + 0.5f >= from)
                            rose[((deg % 360) + 360) % 360] += m.Guns;
                mountArcs[m.Id] = string.Join(", ", m.Arcs.Select(x => $"{x.From % 360:0}°–{x.To % 360:0}°"));
            }
        float max = 0;
        int at = 0;
        for (int i = 0; i < 360; i++)
            if (rose[i] > max)
                (max, at) = (rose[i], i);
        var roseText = new (string, string, string)[]
        {
            ("Ahead · astern", $"{Math.Min(rose[359], rose[0]):0} · {Math.Min(rose[179], rose[180]):0}", "guns"),
            ("Broadside port · stbd", $"{rose[269]:0} · {rose[89]:0}", $"of {total}"),
            ("Most at once", $"{max:0}", at == 0 ? "ahead" : at == 180 ? "astern" : at > 180 ? $"at {360 - at}° port" : $"at {at}° stbd"),
        };

        Selected? sel = null;
        if (mainSel is { } q2)
        {
            var b = mains[q2.Battery];
            string where = q2.Group switch
            {
                G.Fore or G.Aft => $"{(q2.Group == G.Fore ? "fore" : "aft")} end, " +
                                   (q2.Index == 0 ? "outermost" : $"{Ordinal(q2.Index + 1)} from the {(q2.Group == G.Fore ? "bow" : "stern")}") +
                                   (q2.Tier > 0 ? $" · superfiring, tier {q2.Tier}" : " · on the deck"),
                G.Mid => "midships, on the centreline",
                _ => $"wing pair {q2.Index + 1}, {(b.Echelon == true ? "in echelon" : "abreast")}",
            };
            sel = new Selected(q2, q2.Name, $"battery {Letter(q2.Battery)} · {Gun(b)} / {b.CalibreLength ?? 0:0}", where,
                mountArcs.TryGetValue(q2.Name, out var arcs) ? "arcs " + arcs : "arcs: after the build",
                MainArrangement.IsEnd(q2.Group) && q2.Index > 0, q2.Tier > 0);
        }

        return new MainPane(rows, chips.ToArray(), links.ToArray(), waistRows.ToArray(), rose, Math.Max(1, total), roseText, sel,
            style.HasMidshipsTurrets, style.HasWingTurrets, style.HasRaisedMounts, style.TakesMainList,
            mains.Count > 0 ? $"apply to battery {Letter(mainActive)} · keeps its gun, replaces its places" : "");
    }

    static string Ordinal(int n) => n + (n % 100 is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

    // ------------------------------------------------------------------ drawing

    static readonly float[] ColW = [34, 40, 138, 104, 128, 0, 42, 40, 104, 130, 180, 84, 54, 46];   // 0: the positions take what's left
    readonly float[] colX = new float[ColW.Length + 1];   // the table's column edges, laid out each frame
    static readonly string[] ColHead = ["", "", "CALIBRE", "LENGTH", "GUNS / TURRET", "POSITIONS · BOW TO STERN (↑ SUPERFIRES)", "TURR.", "GUNS",
        "FACES", "ROUNDS / GUN", "SHELLS", "HOUSING", "TURRET T", ""];

    void MainBatteryPane(Vector2 size)
    {
        var mp = snap!.Main!;
        ImGui.BeginChild("##mainbattery", size, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var dl = ImGui.GetWindowDrawList();
        var o = ImGui.GetCursorScreenPos();
        float w = size.X;
        planFocus = mainSel?.Name;

        BatteryTable(dl, o, w, mp);
        float tableH = P(18) + RowH * Math.Max(mp.Rows.Length + 1, 4);
        float gap = P(6), roseW = MathF.Round(Math.Clamp(w * 0.25f, P(240), P(330)));
        float lowerH = P(94);
        float arrH = Math.Max(P(150), size.Y - tableH - lowerH - 3 * gap);
        var arrPos = o + new Vector2(gap, tableH + gap);
        var arrSize = new Vector2(w - roseW - 3 * gap, arrH);
        ArrangementView(dl, arrPos, arrSize, mp);
        Rose(dl, arrPos + new Vector2(arrSize.X + gap, 0), new Vector2(roseW, arrH), mp);
        var lowPos = arrPos + new Vector2(0, arrH + gap);
        PresetStrip(dl, lowPos, new Vector2(arrSize.X, lowerH), mp);
        Inspector(dl, lowPos + new Vector2(arrSize.X + gap, 0), new Vector2(roseW, lowerH), mp);
        ChipMenu(mp);
        ImGui.EndChild();
    }

    void BatteryTable(ImDrawListPtr dl, Vector2 o, float w, MainPane mp)
    {
        var x = colX;
        float fixedW = 0;
        foreach (var c in ColW)
            fixedW += P(c);
        x[0] = o.X;
        for (int i = 0; i < ColW.Length; i++)
            x[i + 1] = x[i] + (ColW[i] > 0 ? P(ColW[i]) : Math.Max(P(120), w - fixedW));
        float hh = P(18);
        dl.AddRectFilled(o, o + new Vector2(w, hh), T.U32(T.HeaderBg));
        dl.AddLine(o + new Vector2(0, hh - 1), o + new Vector2(w, hh - 1), T.U32(T.Edge));
        for (int i = 0; i < ColHead.Length; i++)
            if (ColHead[i].Length > 0)
                dl.Text(UiFonts.SansSmall, new Vector2(x[i] + P(4), o.Y + (hh - UiFonts.SansSmall.FontSize) / 2), T.Muted, ColHead[i]);

        var d = doc.Current;
        float fh = ImGui.GetFrameHeight();
        for (int n = 0; n < mp.Rows.Length; n++)
        {
            var r = mp.Rows[n];
            int k = r.K;
            var p = new Vector2(o.X, o.Y + hh + n * RowH);
            bool on = k == mainActive;
            ImGui.PushID(1000 + k);
            // the row selects its battery; the controls on it take their own clicks
            ImGui.SetCursorScreenPos(p);
            ImGui.SetNextItemAllowOverlap();
            if (ImGui.InvisibleButton("##row", new Vector2(w, RowH)) && !on)
            {
                mainActive = k;
                stale = true;
            }
            bool hover = ImGui.IsItemHovered();
            dl.RowBg(p, w, RowH, n);
            if (on)
            {
                dl.AddRectFilled(p, p + new Vector2(w, RowH), T.U32(T.Brass with { W = hover ? 0.13f : 0.10f }));
                dl.AddRectFilled(p, p + new Vector2(P(3), RowH), T.U32(T.Brass));
            }
            else if (hover)
                dl.AddRectFilled(p, p + new Vector2(w, RowH), T.U32(new Vector4(1, 1, 1, 0.03f)));
            float cy = p.Y + (RowH - fh) / 2;
            void At(int col, float dx = 4) => ImGui.SetCursorScreenPos(new Vector2(x[col] + P(dx), cy));
            float ty = p.Y + (RowH - UiFonts.Mono.FontSize) / 2;

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            At(0, 2);
            ImGui.BeginDisabled(k == 0);
            if (ImGui.ArrowButton("##up", ImGuiDir.Up))
                EditBatteryOrder(k, -1);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(k == mp.Rows.Length - 1);
            if (ImGui.ArrowButton("##dn", ImGuiDir.Down))
                EditBatteryOrder(k, 1);
            ImGui.EndDisabled();
            ImGui.PopStyleVar();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                Tip("▲ ▼ move the battery up or down the list: at each end the first battery's turrets stand outboard");

            float sw = P(9);
            var swp = new Vector2(x[1] + P(4), p.Y + (RowH - sw) / 2);
            dl.AddRectFilled(swp, swp + new Vector2(sw, sw), T.U32(BatteryColour(k)));
            dl.Text(UiFonts.SansBold, new Vector2(swp.X + sw + P(6), p.Y + (RowH - UiFonts.SansBold.FontSize) / 2), on ? T.Brass : T.Bright, Letter(k));

            At(2);
            NumberField(r.Cal, P(76));
            At(3);
            Spinner(r.Len, P(56), extras: false);
            At(4);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 0));
            for (int g = 1; g <= BarrelText.Length; g++)
            {
                if (g > 1)
                    ImGui.SameLine();
                if (SegButton(BarrelText[g - 1], r.Barrels == g, P(19)) && r.Barrels != g)
                    Edit(Knobs.MainBarrels(k).Apply(d, g), $"Battery {Letter(k)}: {g} guns per turret");
            }
            ImGui.PopStyleVar();

            dl.PushClipRect(new Vector2(x[5], p.Y), new Vector2(x[6] - P(4), p.Y + RowH), true);
            dl.Text(UiFonts.Mono, new Vector2(x[5] + P(4), ty), T.Text, r.Positions);
            dl.PopClipRect();
            dl.Text(UiFonts.Mono, new Vector2(x[7] - P(6) - Width(UiFonts.Mono, r.Turrets), ty), T.Bright, r.Turrets);
            dl.Text(UiFonts.Mono, new Vector2(x[8] - P(6) - Width(UiFonts.Mono, r.Guns), ty), T.Bright, r.Guns);
            At(8);
            Spinner(r.Faces, P(56), extras: false);
            At(9);
            Spinner(r.Rounds, P(40), extras: true);
            At(10);
            ShellCellUi(r.Shells, P(172));   // where the material was: the turrets' material stays the scheme's, hidden (user, 2026-10-16)
            At(11);
            if (HousingCell(Housings.Of(d.Main![k]), P(78)) is { } h)
                Edit(Knobs.MainHousing(k).Set(d, h), $"Battery {Letter(k)}: {Knobs.HousingLabels[Array.IndexOf(Housings.All, h)]}");
            dl.Text(UiFonts.Mono, new Vector2(x[13] - P(6) - Width(UiFonts.Mono, r.Tonnes), ty), T.Muted, r.Tonnes);
            At(13, 2);
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.BeginDisabled(!mp.TakesList);
            if (ImGui.SmallButton("+"))
            {
                Edit(MainArrangement.Duplicate(d, k), $"Battery {Letter(k)} duplicated");
                mainActive = k + 1;
            }
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                Tip("Duplicate: the same gun as a new battery with no turrets (place them with the + slots)");
            ImGui.SameLine(0, P(2));
            if (ImGui.SmallButton("x"))
            {
                mainSel = null;
                Edit(RemoveAt(d, k, main: true), $"Main battery {Letter(k)} removed");
            }
            if (ImGui.IsItemHovered())
                Tip("Remove the battery");
            ImGui.PopFont();
            ImGui.PopID();
        }

        var ap = new Vector2(o.X + P(38), o.Y + hh + mp.Rows.Length * RowH + (RowH - fh) / 2);
        ImGui.SetCursorScreenPos(ap);
        ImGui.PushFont(UiFonts.SansSmall);
        if (mp.Rows.Length == 0 || mp.TakesList)
        {
            if (ImGui.SmallButton("+ Battery"))
            {
                Edit(mp.Rows.Length == 0 ? AddMain(d) : d with { Main = [.. d.Main ?? [], MainArrangement.Unplaced(AddMain(d).Main![^1])] },
                    "Main battery added");
                mainActive = mp.Rows.Length;
            }
            ImGui.SameLine(0, P(8));
            ImGui.TextColored(T.Muted, mp.Rows.Length == 0 ? "No main battery: an unarmed hull." : "click a row to make it the battery the + slots add");
        }
        ImGui.PopFont();
    }

    void EditBatteryOrder(int k, int dir)
    {
        Edit(MainArrangement.MoveBattery(doc.Current, k, dir), $"Battery {Letter(k)} moved {(dir < 0 ? "outboard" : "inboard")}");
        if (mainActive == k)
            mainActive = k + dir;
        else if (mainActive == k + dir)
            mainActive = k;
        mainSel = null;
    }

    /// <summary>A segmented button: brass when on.</summary>
    /// <summary>A battery's housing (docs/battle/magazines.md V1), any of the three at any calibre: the one picked, if any.
    /// A casemate battery's is its position, shown and not picked.</summary>
    static string? HousingCell(string current, float width, bool enabled = true)
    {
        string? picked = null;
        int at = Array.IndexOf(Housings.All, current);
        ImGui.SetNextItemWidth(width);
        ImGui.BeginDisabled(!enabled);
        if (ImGui.BeginCombo("##housing", enabled ? current : "casemate"))
        {
            for (int i = 0; i < Housings.All.Length; i++)
                if (ImGui.Selectable(Knobs.HousingLabels[i], i == at) && i != at)
                    picked = Housings.All[i];
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            Tip("How the guns are housed, at any calibre: a turret on a barbette (armoured, the hoists down its trunk), a gunhouse " +
                "(enclosed and light, its hoists down a trunk) or a shield (open behind, the gun's ready rounds on deck). An enclosed " +
                "mount's trunk is a flash's way down to the magazine; a shield's crew stand in the open.");
        return picked;
    }

    /// <summary>A number typed in place: ◂ [value unit] ▸. The number is the field (in the displayed units; the unit is
    /// drawn in the field, not typed); Enter or leaving it applies, and a value that doesn't parse or is out of the
    /// knob's range is dropped and the old one comes back. The wheel over it steps it.</summary>
    void NumberField(NumRow r, float valueW)
    {
        var k = r.Knob;
        ImGui.PushID(k.Id);   // its own ID scope, as the spinner's
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        if (ImGui.ArrowButton("##m", ImGuiDir.Left))
            Step(r, ImGui.GetIO().KeyShift ? -4 : -1);
        ImGui.SameLine();
        bool mine = fieldId == k.Id;
        string text = mine ? fieldText : r.Plain;
        ImGui.SetNextItemWidth(valueW);
        ImGui.PushFont(UiFonts.Mono);
        ImGui.PushStyleColor(ImGuiCol.Text, T.Bright);
        ImGui.InputText("##f", ref text, 12, ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        ImGui.PopStyleColor();
        ImGui.PopFont();
        if (ImGui.IsItemActivated())
        {
            fieldId = k.Id;
            mine = true;
        }
        if (mine && ImGui.IsItemActive())
            fieldText = text;
        if (mine && ImGui.IsItemDeactivated())
        {
            fieldId = null;
            var (lo, hi) = k.Range(doc.Current);
            if (ImGui.IsItemDeactivatedAfterEdit() && !ImGui.IsKeyPressed(ImGuiKey.Escape)
                && Units.TryParse(fieldText, k.Quantity, units, out double metric) && metric >= lo - 1e-6 && metric <= hi + 1e-6
                && (r.Num is not { } old || Math.Abs(old - metric) > 1e-9))
            {
                SetValue(k, metric);
                doc.Seal();
            }
        }
        var a = ImGui.GetItemRectMin();
        var b = ImGui.GetItemRectMax();
        string unit = Units.Unit(k.Quantity, units);
        ImGui.GetWindowDrawList().AddText(UiFonts.Mono, UiFonts.Mono.FontSize,
            new Vector2(b.X - P(4) - Width(UiFonts.Mono, unit), (a.Y + b.Y - UiFonts.Mono.FontSize) / 2), T.U32(T.Muted), unit);
        if (!ImGui.IsItemActive() && ImGui.IsItemHovered())
        {
            ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY);
            if (ImGui.GetIO().MouseWheel is var wheel && wheel != 0)
                Step(r, Math.Sign(wheel) * (ImGui.GetIO().KeyShift ? 4 : 1));
        }
        ImGui.SameLine();
        if (ImGui.ArrowButton("##p", ImGuiDir.Right))
            Step(r, ImGui.GetIO().KeyShift ? 4 : 1);
        ImGui.PopStyleVar();
        ImGui.PopID();
    }

    // ------------------------------------------------------------------ the arrangement

    void ArrangementView(ImDrawListPtr dl, Vector2 pos, Vector2 size, MainPane mp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Plan));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        float sc = Math.Min(size.X / SchW, size.Y / SchH);
        var off = pos + (size - new Vector2(SchW, SchH) * sc) / 2;
        Vector2 Px(float x, float y) => off + new Vector2(x, y) * sc;
        dl.PushClipRect(pos, pos + size, true);
        for (float gx = pos.X + P(24); gx < pos.X + size.X; gx += P(24))
            dl.AddLine(new Vector2(gx, pos.Y), new Vector2(gx, pos.Y + size.Y), T.U32(new Vector4(0.59f, 0.75f, 0.9f, 0.05f)));
        for (float gy = pos.Y + P(24); gy < pos.Y + size.Y; gy += P(24))
            dl.AddLine(new Vector2(pos.X, gy), new Vector2(pos.X + size.X, gy), T.U32(new Vector4(0.59f, 0.75f, 0.9f, 0.05f)));

        // the hull outline, bow right like the plan above
        float cy = SchCy;
        dl.PathLineTo(Px(40, cy - 52));
        dl.PathBezierCubicCurveTo(Px(300, cy - 78), Px(700, cy - 80), Px(800, cy - 60));
        dl.PathBezierCubicCurveTo(Px(870, cy - 44), Px(910, cy - 14), Px(926, cy));
        dl.PathBezierCubicCurveTo(Px(910, cy + 14), Px(870, cy + 44), Px(800, cy + 60));
        dl.PathBezierCubicCurveTo(Px(700, cy + 80), Px(300, cy + 78), Px(40, cy + 52));
        dl.PathBezierCubicCurveTo(Px(26, cy + 30), Px(22, cy - 30), Px(40, cy - 52));
        dl.PathStroke(T.U32(T.PlanLine with { W = 0.55f }), ImDrawFlags.Closed, 1);
        for (float x = 30; x < 930; x += 11)
            dl.AddLine(Px(x, cy), Px(x + 6, cy), T.U32(T.PlanLine with { W = 0.25f }));
        dl.Text(UiFonts.SansSmall, Px(912, 14) - new Vector2(Width(UiFonts.SansSmall, "BOW"), 0), T.PlanLine with { W = 0.7f }, "BOW");
        dl.Text(UiFonts.SansSmall, Px(34, 14), T.PlanLine with { W = 0.7f }, "STERN");

        var io = ImGui.GetIO();
        var d = doc.Current;
        bool anyHovered = false;

        // the steps between neighbours at an end
        for (int i = 0; i < mp.Links.Length; i++)
        {
            var l = mp.Links[i];
            var c = Px(l.X, l.Y);
            float hs = P(9);
            ImGui.SetCursorScreenPos(c - new Vector2(hs, hs));
            ImGui.PushID(2000 + i);
            if (ImGui.InvisibleButton("##link", new Vector2(2 * hs, 2 * hs)))
                Edit(MainArrangement.ToggleStep(d, l.End, l.J), $"{(l.End == G.Fore ? "Fore" : "Aft")} turrets: {(l.On ? "on the deck" : "superfiring")} from {Layout.TurretName(l.End == G.Fore ? "ABC" : "YXW", l.J)}");
            bool hover = ImGui.IsItemHovered();
            ImGui.PopID();
            dl.AddRectFilled(c - new Vector2(hs, hs), c + new Vector2(hs, hs), T.U32(l.On ? new Vector4(0.29f, 0.24f, 0.13f, 1) : new Vector4(0.1f, 0.145f, 0.19f, 1)), P(2));
            dl.AddRect(c - new Vector2(hs, hs), c + new Vector2(hs, hs), T.U32(l.On ? T.Brass : hover ? T.Text : new Vector4(0.29f, 0.34f, 0.39f, 1)), P(2));
            float t = P(4);
            if (l.On)
                dl.AddTriangleFilled(c + new Vector2(0, -t), c + new Vector2(t, t * 0.8f), c + new Vector2(-t, t * 0.8f), T.U32(new Vector4(1, 0.906f, 0.659f, 1)));
            else
            {
                dl.AddLine(c + new Vector2(-t, -P(1.5f)), c + new Vector2(t, -P(1.5f)), T.U32(T.Muted), P(1.2f));
                dl.AddLine(c + new Vector2(-t, P(1.5f)), c + new Vector2(t, P(1.5f)), T.U32(T.Muted), P(1.2f));
            }
            if (hover)
            {
                anyHovered = true;
                Tip(l.Tip);
            }
        }

        for (int i = 0; i < mp.Chips.Length; i++)
        {
            var ch = mp.Chips[i];
            var c = Px(ch.X, ch.Y);
            float r = ch.R * sc;
            ImGui.SetCursorScreenPos(c - new Vector2(r, r));
            ImGui.PushID(3000 + i);
            bool clicked = ImGui.InvisibleButton("##chip", new Vector2(2 * r, 2 * r), ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
            bool hover = ImGui.IsItemHovered();
            bool right = clicked && ImGui.IsMouseReleased(ImGuiMouseButton.Right);
            ImGui.PopID();
            var col = BatteryColour(ch.Slot.Battery);
            if (ch.Ghost)
            {
                if (clicked && !right)
                    AddTurret(d, ch.Slot, io.KeyShift);
                DashedCircle(dl, c, r, T.U32(col with { W = hover ? 1 : 0.8f }));
                if (hover)
                {
                    dl.AddCircleFilled(c, r - 1, T.U32(col with { W = 0.15f }));
                    if (!float.IsNaN(ch.InsertX))
                    {
                        var m = Px(ch.InsertX, ch.Y);
                        dl.AddLine(m - new Vector2(0, 26 * sc), m + new Vector2(0, 26 * sc), T.U32(col), P(2));
                        dl.AddTriangleFilled(m - new Vector2(5, 32) * sc, m - new Vector2(-5, 32) * sc, m - new Vector2(0, 24) * sc, T.U32(col));
                    }
                }
                float t = P(5);
                dl.AddLine(c - new Vector2(t, 0), c + new Vector2(t, 0), T.U32(col), P(1.5f));
                dl.AddLine(c - new Vector2(0, t), c + new Vector2(0, t), T.U32(col), P(1.5f));
            }
            else
            {
                if (clicked)
                {
                    if (mainSel != ch.Slot || mainActive != ch.Slot.Battery)
                    {
                        mainSel = ch.Slot;
                        mainActive = ch.Slot.Battery;
                        stale = true;
                    }
                    if (right)
                    {
                        menuSlot = ch.Slot;
                        openChipMenu = true;
                    }
                }
                ChipShape(dl, c, r, sc, ch, col, mainSel is { } s && SameTurret(s, ch.Slot), hover);
            }
            if (hover)
            {
                anyHovered = true;
                if (!ch.Ghost)
                    planFocus = ch.Slot.Name;
                Tip(ch.Tip);
            }
        }

        // the wheel over an end or the waist adds or removes the active battery's turrets there
        var mouse = io.MousePos;
        if (!anyHovered && mp.Rows.Length > 0 && ImGui.IsWindowHovered() && mouse.X >= pos.X && mouse.Y >= pos.Y && mouse.X < pos.X + size.X
            && mouse.Y < pos.Y + size.Y && io.MouseWheel != 0)
        {
            float sx = (mouse.X - off.X) / sc;
            var g = sx > 600 ? G.Fore : sx < 330 ? G.Aft : mp.HasWing ? G.Wing : mp.HasMid ? G.Mid : (G?)null;
            if (g is { } grp)
                WheelTurret(d, grp, io.MouseWheel > 0);
        }
        dl.PopClipRect();

        // the waist's settings, one cluster per battery with waist turrets, along the bottom
        ImGui.SetCursorScreenPos(pos + new Vector2(P(8), size.Y - P(8) - ImGui.GetFrameHeight()));
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 0));
        if (mp.Waist.Length == 0 && mp.Rows.Length > 0 && (mp.HasMid || mp.HasWing))
            ImGui.TextColored(T.Muted, mp.HasWing ? "No waist turrets: the + on the centreline adds a midships turret, either side of it a wing pair."
                : "No midships turrets: the + on the centreline adds one.");
        for (int i = 0; i < mp.Waist.Length; i++)
        {
            var wr = mp.Waist[i];
            ImGui.PushID(4000 + i);
            if (i > 0)
                ImGui.SameLine(0, P(12));
            var cp = ImGui.GetCursorScreenPos();
            dl.AddRectFilled(cp + new Vector2(0, P(3)), cp + new Vector2(P(8), P(11)), T.U32(BatteryColour(wr.K)));
            ImGui.SetCursorScreenPos(cp + new Vector2(P(12), 0));
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(wr.Label);
            ImGui.SameLine(0, P(6));
            if (wr.Wing)
            {
                if (SegButton("abreast", !wr.Echelon) && wr.Echelon)
                    Edit(Knobs.MainCrossDeck(wr.K).Set(Knobs.MainEchelon(wr.K).Set(d, false), false), $"Battery {Letter(wr.K)} wings abreast");
                ImGui.SameLine();
                if (SegButton("echelon", wr.Echelon) && !wr.Echelon)
                    Edit(Knobs.MainEchelon(wr.K).Set(d, true), $"Battery {Letter(wr.K)} wings in echelon");
                ImGui.SameLine(0, P(4));
                ImGui.BeginDisabled(!wr.Echelon);
                if (SegButton("cross-deck", wr.Cross))
                    Edit(Knobs.MainCrossDeck(wr.K).Set(d, !wr.Cross), $"Battery {Letter(wr.K)} cross-deck fire {(wr.Cross ? "off" : "on")}");
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    Tip("Fire across the deck to the far side: needs the wings in echelon (an abreast pair blocks each other)");
                ImGui.SameLine(0, P(4));
            }
            if (mp.Raised)
            {
                if (SegButton("deck", !wr.Deckhouse) && wr.Deckhouse)
                    Edit(Knobs.MainStandsOn(wr.K).Set(d, "deck"), $"Battery {Letter(wr.K)} waist turrets on the deck");
                ImGui.SameLine();
                if (SegButton("deckhouse", wr.Deckhouse) && !wr.Deckhouse)
                    Edit(Knobs.MainStandsOn(wr.K).Set(d, "deckhouse"), $"Battery {Letter(wr.K)} waist turrets on the deckhouse");
            }
            ImGui.PopID();
        }
        ImGui.PopStyleVar();
        ImGui.PopFont();
    }

    static bool SameTurret(MainArrangement.Slot a, MainArrangement.Slot b) =>
        a.Group == b.Group && a.Index == b.Index && (a.Group != G.Wing || a.Side == b.Side);

    static void DashedCircle(ImDrawListPtr dl, Vector2 c, float r, uint col)
    {
        const int N = 16;
        for (int i = 0; i < N; i++)
        {
            float a0 = i * MathF.Tau / N, a1 = a0 + MathF.Tau / N * 0.55f;
            dl.PathArcTo(c, r, a0, a1, 4);
            dl.PathStroke(col, ImDrawFlags.None, P(1.3f));
        }
    }

    static void ChipShape(ImDrawListPtr dl, Vector2 c, float r, float sc, Chip ch, Vector4 col, bool sel, bool hover)
    {
        if (ch.Tier > 0)
            dl.AddCircleFilled(c + new Vector2(3, 4) * sc, r + sc, T.U32(new Vector4(0, 0, 0, 0.45f)));
        // the barrels, toward the end the turret faces (degrees clockwise from ahead: bow right, starboard down)
        float ang = ch.Face * MathF.PI / 180, dx = MathF.Cos(ang), dy = MathF.Sin(ang);
        float len = r + (6 + ch.R * 0.35f) * sc, spread = 4.2f * sc;
        for (int n = 0; n < ch.Barrels; n++)
        {
            float o = (n - (ch.Barrels - 1) / 2f) * spread;
            var ov = new Vector2(-dy * o, dx * o);
            dl.AddLine(c + ov + new Vector2(dx, dy) * r * 0.5f, c + ov + new Vector2(dx, dy) * len, T.U32(col), 2.4f * sc);
        }
        float f = ch.Tier > 0 ? 0.62f : 0.42f;
        dl.AddCircleFilled(c, r, T.U32(new Vector4(col.X * f, col.Y * f, col.Z * f, 1)));
        dl.AddCircle(c, r, T.U32(sel ? new Vector4(1, 0.906f, 0.659f, 1) : hover ? T.Bright : col), 0, sel ? P(2.5f) : P(1.4f));
        if (ch.Tier > 0)
            dl.AddCircle(c, r - 4 * sc, T.U32(new Vector4(1, 1, 1, 0.35f)));
        float nw = Width(UiFonts.Mono, ch.Slot.Name);
        dl.Text(UiFonts.Mono, c - new Vector2(nw / 2, UiFonts.Mono.FontSize / 2), T.Bright, ch.Slot.Name);
        if (ch.Tag.Length > 0)
        {
            float tw = Width(UiFonts.SansSmall, ch.Tag);
            float ty = ch.Slot.Group == G.Wing ? (ch.Slot.Side < 0 ? c.Y + r + P(2) : c.Y - r - P(2) - UiFonts.SansSmall.FontSize)
                : c.Y - r - P(4) - UiFonts.SansSmall.FontSize;
            dl.Text(UiFonts.SansSmall, new Vector2(c.X - tw / 2, ty), col, ch.Tag);
        }
        if (ch.Tier > 0)
        {
            var bp = c + new Vector2(r * 0.55f, r * 0.35f);
            string t = TierText[Math.Min(ch.Tier, TierText.Length - 1)];
            var bs = new Vector2(Width(UiFonts.SansSmall, t) + P(5), UiFonts.SansSmall.FontSize + P(1));
            dl.AddRectFilled(bp, bp + bs, T.U32(new Vector4(0.29f, 0.24f, 0.13f, 1)), P(2));
            dl.AddRect(bp, bp + bs, T.U32(T.Brass), P(2));
            dl.Text(UiFonts.SansSmall, bp + new Vector2(P(2.5f), 0), new Vector4(1, 0.906f, 0.659f, 1), t);
        }
    }

    void AddTurret(Design d, MainArrangement.Slot ghost, bool flush)
    {
        int k = ghost.Battery;
        bool stepped = MainArrangement.IsEnd(ghost.Group) && MainArrangement.AddsStepped(d, k, ghost.Group, flush);
        string what = ghost.Group switch
        {
            G.Mid => "midships turret",
            G.Wing => "wing pair",
            _ => $"turret {ghost.Name}{(stepped ? ", superfiring" : ghost.Name is "A" or "Y" ? "" : ", on the deck")}",
        };
        Edit(MainArrangement.Add(d, k, ghost.Group, flush), $"Battery {Letter(k)}: {what} added");
        mainSel = null;
    }

    void WheelTurret(Design d, G g, bool up)
    {
        var mains = d.Main ?? [];
        if (up)
        {
            AddTurret(d, new MainArrangement.Slot(mainActive, g, -1, MainArrangement.IsEnd(g) ? Layout.TurretName(g == G.Fore ? "ABC" : "YXW",
                MainArrangement.InsertAt(d, mainActive, g)) : "", 0, false, 0), false);
            return;
        }
        if (MainArrangement.Count(mains[mainActive], g) == 0)
            return;
        // the active battery's innermost turret there
        var slot = MainArrangement.Slots(d).Last(s => s.Battery == mainActive && s.Group == g);
        RemoveTurret(d, slot);
    }

    void RemoveTurret(Design d, MainArrangement.Slot s)
    {
        Edit(MainArrangement.Remove(d, s), $"Battery {Letter(s.Battery)}: {(s.Group == G.Wing ? "wing pair" : "turret " + s.Name)} removed");
        mainSel = null;
    }

    void ChipMenu(MainPane mp)
    {
        if (openChipMenu)
        {
            ImGui.OpenPopup("##chipmenu");
            openChipMenu = false;
        }
        if (!ImGui.BeginPopup("##chipmenu"))
            return;
        var d = doc.Current;
        var s = menuSlot;
        int nb = d.Main?.Count ?? 0;
        if (s.Battery >= nb)
        {
            ImGui.EndPopup();
            return;
        }
        ImGui.TextColored(T.Muted, $"{s.Name} · battery {Letter(s.Battery)}");
        ImGui.Separator();
        if (MainArrangement.IsEnd(s.Group) && s.Index > 0 && ImGui.MenuItem(s.Tier > 0 ? "Put on the deck" : "Superfire"))
            Edit(MainArrangement.ToggleStep(d, s.Group, s.Index), $"Turret {s.Name} {(s.Tier > 0 ? "on the deck" : "superfiring")}");
        if (nb > 1 || mp.TakesList)
        {
            ImGui.Separator();
            ImGui.TextColored(T.Muted, "Battery");
            for (int k = 0; k < nb; k++)
                if (k != s.Battery && ImGui.MenuItem($"{Letter(k)} · {d.Main![k].Barrels ?? 1} × {Fmt(d.Main[k].CalibreMm ?? 0, Quantity.Calibre)}"))
                {
                    Edit(MainArrangement.Reassign(d, s, k), $"Turret {s.Name} to battery {Letter(k)}");
                    mainActive = k;
                    mainSel = null;
                }
            if (mp.TakesList && ImGui.MenuItem("New battery from this turret"))
            {
                Edit(MainArrangement.SplitOff(d, s), $"Turret {s.Name} split off as battery {Letter(nb)}");
                mainActive = nb;
                mainSel = null;
            }
        }
        ImGui.Separator();
        ImGui.TextColored(T.Muted, "Move to");
        foreach (var (g, label, ok) in new[] { (G.Fore, "Fore end (inboard)", true), (G.Aft, "Aft end (inboard)", true), (G.Mid, "Midships", mp.HasMid),
                     (G.Wing, "Wings (as a pair)", mp.HasWing) })
            if (g != s.Group && ok && ImGui.MenuItem(label))
            {
                Edit(MainArrangement.MoveTo(d, s, g), $"Turret {s.Name} moved: {label.ToLowerInvariant()}");
                mainSel = null;
            }
        ImGui.Separator();
        if (ImGui.MenuItem(s.Group == G.Wing ? "Remove the pair" : "Remove", "Del"))
            RemoveTurret(d, s);
        ImGui.EndPopup();
    }

    // ------------------------------------------------------------------ guns bearing, presets, the selected turret

    void Rose(ImDrawListPtr dl, Vector2 pos, Vector2 size, MainPane mp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Panel2));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        dl.HeadStrip(size.X, "GUNS BEARING", "main battery · by bearing");
        float textH = RowH * 3 + P(6);
        float R = Math.Max(P(30), Math.Min(size.X / 2 - P(34), (size.Y - P(18) - textH) / 2 - P(14)));
        var c = new Vector2(pos.X + size.X / 2, pos.Y + P(18) + P(14) + R);
        uint grid = T.U32(T.Bevel);
        for (int ring = 1; ring <= 4; ring++)
            dl.AddCircle(c, R * ring / 4, grid, 48);
        for (int a = 0; a < 360; a += 45)
        {
            float rad = a * MathF.PI / 180;
            dl.AddLine(c, c + new Vector2(MathF.Sin(rad), -MathF.Cos(rad)) * R, grid);
        }
        // the ship, bow up
        float sh = Math.Min(P(22), R * 0.25f);
        dl.AddQuadFilled(c + new Vector2(0, -sh), c + new Vector2(sh * 0.3f, sh * 0.2f), c + new Vector2(0, sh), c + new Vector2(-sh * 0.3f, sh * 0.2f),
            T.U32(new Vector4(0.16f, 0.2f, 0.25f, 1)));
        Vector2 At(int deg)
        {
            float rad = deg * MathF.PI / 180, v = mp.Rose[deg % 360] / mp.RoseMax * R;
            return c + new Vector2(MathF.Sin(rad), -MathF.Cos(rad)) * v;
        }
        uint fill = T.U32(T.Blue with { W = 0.28f });
        for (int a = 0; a < 360; a++)
            dl.AddTriangleFilled(c, At(a), At(a + 1), fill);
        for (int a = 0; a <= 360; a++)
            dl.PathLineTo(At(a));
        dl.PathStroke(T.U32(T.Blue), ImDrawFlags.None, P(1.4f));
        void Label(int deg, string t)
        {
            float rad = deg * MathF.PI / 180;
            var p = c + new Vector2(MathF.Sin(rad), -MathF.Cos(rad)) * (R + P(12));
            dl.Text(UiFonts.SansSmall, p - new Vector2(Width(UiFonts.SansSmall, t) / 2, UiFonts.SansSmall.FontSize / 2), T.Muted, t);
        }
        Label(0, "ahead");
        Label(180, "astern");
        Label(90, "stbd");
        Label(270, "port");
        float y = pos.Y + size.Y - textH;
        for (int i = 0; i < mp.RoseText.Length; i++)
        {
            var (l, v, n) = mp.RoseText[i];
            float ty = y + i * RowH + (RowH - UiFonts.Sans.FontSize) / 2;
            dl.Text(UiFonts.Sans, new Vector2(pos.X + P(8), ty), T.Muted, l);
            dl.Text(UiFonts.Mono, new Vector2(pos.X + size.X * 0.62f - Width(UiFonts.Mono, v), ty), T.Bright, v);
            dl.Text(UiFonts.SansSmall, new Vector2(pos.X + size.X - P(8) - Width(UiFonts.SansSmall, n), ty + P(1)), T.Muted, n);
        }
    }

    void PresetStrip(ImDrawListPtr dl, Vector2 pos, Vector2 size, MainPane mp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Panel));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        dl.HeadStrip(size.X, "ARRANGEMENTS", mp.ActiveNote);
        var ps = MainArrangement.Presets;
        float bw = MathF.Floor((size.X - P(8) - P(4) * (ps.Length - 1)) / ps.Length), bh = size.Y - P(18) - P(8);
        var d = doc.Current;
        for (int i = 0; i < ps.Length; i++)
        {
            var p = ps[i];
            var a = pos + new Vector2(P(4) + i * (bw + P(4)), P(18) + P(4));
            bool ok = mp.Rows.Length > 0 && (!p.NeedsMid || mp.HasMid) && (!p.NeedsWing || mp.HasWing);
            ImGui.SetCursorScreenPos(a);
            ImGui.PushID(5000 + i);
            ImGui.BeginDisabled(!ok);
            if (ImGui.InvisibleButton("##pre", new Vector2(bw, bh)))
            {
                Edit(MainArrangement.Apply(d, mainActive, p), $"Battery {Letter(mainActive)}: {p.Name}");
                mainSel = null;
            }
            ImGui.EndDisabled();
            bool hover = ImGui.IsItemHovered();
            ImGui.PopID();
            var top = hover && ok ? new Vector4(0.19f, 0.235f, 0.28f, 1) : new Vector4(0.15f, 0.19f, 0.227f, 1);
            dl.AddRectFilledMultiColor(a, a + new Vector2(bw, bh), T.U32(top), T.U32(top), T.U32(T.Panel), T.U32(T.Panel));
            dl.AddRect(a, a + new Vector2(bw, bh), T.U32(T.Edge));
            var tc = ok ? (hover ? T.Bright : T.Text) : T.Muted;
            dl.PushClipRect(a, a + new Vector2(bw, bh), true);
            dl.Text(UiFonts.SansBold, a + V(5, 2), tc, p.Name);
            dl.Text(UiFonts.SansSmall, a + new Vector2(P(5), P(3) + UiFonts.SansBold.FontSize), T.Muted, p.Short);
            Pictogram(dl, a + new Vector2(P(5), bh - P(16)), new Vector2(bw - P(10), P(13)), p, ok);
            dl.PopClipRect();
        }
    }

    static void Pictogram(ImDrawListPtr dl, Vector2 a, Vector2 s, MainArrangement.Preset p, bool ok)
    {
        float cy = a.Y + s.Y / 2;
        uint line = T.U32(new Vector4(0.29f, 0.34f, 0.39f, 1));
        dl.AddLine(new Vector2(a.X, a.Y), new Vector2(a.X + s.X * 0.86f, a.Y), line);
        dl.AddLine(new Vector2(a.X, a.Y + s.Y), new Vector2(a.X + s.X * 0.86f, a.Y + s.Y), line);
        dl.AddLine(new Vector2(a.X + s.X * 0.86f, a.Y), new Vector2(a.X + s.X, cy), line);
        dl.AddLine(new Vector2(a.X + s.X * 0.86f, a.Y + s.Y), new Vector2(a.X + s.X, cy), line);
        dl.AddLine(new Vector2(a.X, a.Y), new Vector2(a.X, a.Y + s.Y), line);
        var col = ok ? T.Blue : T.Muted;
        var up = ok ? new Vector4(0.56f, 0.71f, 0.91f, 1) : T.Muted;
        float r = Math.Max(2, s.Y * 0.2f), step = r * 2.6f;
        void Dot(float x, float y, bool hi) => dl.AddCircleFilled(new Vector2(x, y), hi ? r * 1.2f : r, T.U32(hi ? up : col));
        int sf = p.SteppedFore ?? p.Fore, sa = p.SteppedAft ?? p.Aft;
        for (int i = 0; i < p.Fore; i++)
            Dot(a.X + s.X * 0.86f - r - i * step, cy, i > 0 && i < sf);
        for (int i = 0; i < p.Aft; i++)
            Dot(a.X + r * 2 + i * step, cy, i > 0 && i < sa);
        float mid = a.X + s.X * 0.48f;
        for (int i = 0; i < p.Mid; i++)
            Dot(mid, cy, false);
        for (int i = 0; i < p.Wing; i++)
        {
            float x = mid + step * 0.6f - i * step * 1.4f + (p.Mid > 0 ? step : 0);
            float e = p.Echelon ? r : 0;
            Dot(x + e, a.Y + r, false);
            Dot(x - e, a.Y + s.Y - r, false);
        }
    }

    void Inspector(ImDrawListPtr dl, Vector2 pos, Vector2 size, MainPane mp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Panel));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        dl.HeadStrip(size.X, "SELECTED", mp.Sel == null ? "click a turret" : null);
        float x = pos.X + P(8), y = pos.Y + P(21);
        if (mp.Sel is not { } s)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + size.X - P(16));
            ImGui.TextColored(T.Muted, "A turret's place, tier and arcs show here, and the plan rings it. Right-click a turret for its battery, place and removal.");
            ImGui.PopTextWrapPos();
            ImGui.PopFont();
            return;
        }
        dl.Text(UiFonts.SansBold, new Vector2(x, y), T.Bright, s.Title);
        float tx = x + Width(UiFonts.SansBold, s.Title) + P(8);
        dl.AddRectFilled(new Vector2(tx, y + P(3)), new Vector2(tx + P(8), y + P(11)), T.U32(BatteryColour(s.Slot.Battery)));
        dl.Text(UiFonts.Sans, new Vector2(tx + P(12), y), T.Text, s.Battery);
        dl.Text(UiFonts.SansSmall, new Vector2(x, y + P(17)), T.Muted, s.Where);
        dl.Text(UiFonts.SansSmall, new Vector2(x, y + P(31)), T.Muted, s.Arcs);
        ImGui.SetCursorScreenPos(new Vector2(x, pos.Y + size.Y - P(6) - ImGui.GetFrameHeight()));
        ImGui.PushFont(UiFonts.SansSmall);
        var d = doc.Current;
        if (s.CanStep)
        {
            if (ImGui.SmallButton(s.Stepped ? "Put on the deck" : "Superfire"))
                Edit(MainArrangement.ToggleStep(d, s.Slot.Group, s.Slot.Index), $"Turret {s.Title} {(s.Stepped ? "on the deck" : "superfiring")}");
            ImGui.SameLine(0, P(4));
        }
        if (ImGui.SmallButton("More..."))
        {
            menuSlot = s.Slot;
            openChipMenu = true;
        }
        ImGui.SameLine(0, P(4));
        if (ImGui.SmallButton("Remove (Del)"))
            RemoveTurret(d, s.Slot);
        ImGui.PopFont();
    }
}
