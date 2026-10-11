using System.Globalization;
using System.Numerics;
using Fleetwright.Shipgen;
using ImGuiNET;
using Pos = Fleetwright.Designer.SecondaryArrangement.Pos;
using SA = Fleetwright.Designer.SecondaryArrangement;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Designer;

/// <summary>
/// The Secondary · AA · TT tab, after docs/designer-ui/secondary.md: a table of the secondary batteries (the position is
/// one click), the broadside strip (a half-plan over one track per position, schematic, with what didn't fit as
/// ghosts and a + at each track's head), the secondaries' guns bearing, and the AA, torpedo and selected-battery panels.
/// The places are the built ship's; the edits are <see cref="SecondaryArrangement"/>'s and the battery knobs.
/// </summary>
public sealed unsafe partial class DesignerScene
{
    int secActive;   // the battery the track + adds to
    (int K, int N)? secSel;   // the selected pair: battery, pair number (1-based, as in its tag)
    (int K, int N) secMenuFor;
    bool openSecMenu;
    bool secRoseAll;   // the rose adds the main battery
    int secHotK = -1, secHotNext = -1;   // the battery hovered in the strip (this frame's, next frame's)
    (int K, int N)? secFocus;   // what the plan rings: a pair, or a whole battery (N = 0)
    string? secForceTip;   // dev hook: draw this tooltip without a mouse ("lane:2", "short:1")

    static readonly Vector4[] SecColours = [Hex(0xD98B5F), Hex(0x8BC48A), Hex(0xB48CD9), Hex(0x5FC2C0), Hex(0xD9C25F), Hex(0xD97A9A)];
    static Vector4 SecColour(int k) => SecColours[k % SecColours.Length];
    static string SecName(int k) => Batteries.BatteryPrefix(k).TrimEnd('-');
    static readonly string[] PosLabel = ["deckhouse", "deck", "upper", "lower"];
    static readonly string[] TrackLabel = ["deckhouse", "deck", "casemate ↑", "casemate ↓"];
    // the tracks top to bottom (the deck line under the deck track; a lower casemate is in the hull side)
    static readonly Pos[] TrackOrder = [Pos.House, Pos.Upper, Pos.Deck, Pos.Lower];

    // ------------------------------------------------------------------ the snapshot's part

    sealed record SecRow(int K, NumRow Cal, NumRow Len, int Barrels, Pos Pos, string Axis, NumRow Per, int Placed, int Wanted,
        string Guns, NumRow Shields, NumRow Rounds, ChoiceRow Material, string Tonnes, string ShortTip);

    /// <summary>A built secondary mount (the starboard one of a pair, or an end mount): battery, pair number, plan place,
    /// its track, gun axis above the waterline, body radius and barrel length (m), barrels, tooltip.</summary>
    sealed record SecMount(int K, int N, float X, float Y, Pos Track, float Axis, float R, float Barrel, int Barrels, bool Casemate, string Tip);

    sealed record PlanMark(float X, float Y, float R, string Id, int Tier);

    sealed record AaRow(string Label, NumRow Knob, string Type, int Placed, int Wanted, int Barrels, int Roofs, string Tonnes);

    sealed record SecPane(SecRow[] Rows, SecMount[] Mounts, Vector2[] Hull, (Vector2[] Pts, long Level)[] Blocks, PlanMark[] Mains,
        (Vector2 P, bool Heavy, bool Roof)[] Aa, Vector2[] Tt, float L, float B, float AmidX0, float AmidX1, float[] TrackAxis,
        float[][] RoseBy, float[] RoseMain, int RoseTotal, AaRow[] AaRows, string AaPlace, string AaDirs, NumRow TtMounts, int TtTubes,
        string[] TtLines, string SecDirs, bool[] Offered, bool TakesList, bool Counted, bool Built);

    SecPane SecPaneOf(Design d, Ship? ship, Overlay? over)
    {
        var secs = d.Secondary ?? [];
        var style = Styles.Get(d);
        secActive = Math.Clamp(secActive, 0, Math.Max(0, secs.Count - 1));
        if (secSel is { } ss && (ss.K >= secs.Count))
            secSel = null;
        var hb = ship?.Hitboxes;
        double fb = hb?.Vertical.Freeboard ?? 5.0;
        float GunAxis(Component c) => (float)(fb + c.Base + 0.55 * (c.Top - c.Base));

        // the built secondaries, by battery
        var mounts = new List<SecMount>();
        var placedMounts = new int[secs.Count];
        var placedPairs = new int[secs.Count];
        var ids = new List<string>[secs.Count];
        for (int k = 0; k < secs.Count; k++)
            ids[k] = [];
        var axisSum = new double[4];
        var axisN = new int[4];
        if (hb != null)
            foreach (var c in hb.Components)
            {
                if (c.Kind != "secondary" || c.X is not { } x || SA.ParseId(c.Id) is not var (k, n, side) || k >= secs.Count)
                    continue;
                ids[k].Add(c.Id);
                placedMounts[k]++;
                if (side < 0)
                    continue;
                if (side > 0)
                    placedPairs[k]++;
                var b = secs[k];
                var pos = SA.Of(b);
                float axis = GunAxis(c);
                axisSum[(int)pos] += axis;
                axisN[(int)pos]++;
                var tty = c.Type != null && hb.TurretTypes.TryGetValue(c.Type, out var ty) ? ty : null;
                bool cas = c.Mount == "casemate";
                string kind = cas ? "casemate" : tty?.Shape switch { "bb" => "turret on a barbette", "dp" => "shielded mount", "open" => "open mount", _ => "mount" };
                string arcs = c.ArcsDeg is { Count: > 0 } a ? string.Join(", ", a.Where(q => q.Length == 2).Select(q => $"{q[0] % 360:0}°–{q[1] % 360:0}°")) : "–";
                string pair = side == 0 ? c.Id : $"{SecName(k)}{n}S / {SecName(k)}{n}P";
                mounts.Add(new SecMount(k, n, (float)x, (float)(c.Y ?? 0), pos, axis, (float)(c.BroadphaseR ?? 1.5), (float)(tty?.BarrelLen ?? 3),
                    (int)(tty?.Barrels ?? 1), cas,
                    $"{pair} · {b.Barrels ?? 1} × {Fmt(b.CalibreMm ?? 0, Quantity.Calibre)}/{b.CalibreLength ?? 0:0}, {kind}\n" +
                    $"{SA.Text(pos)}, {(x >= 0 ? $"{x:0} m forward of" : $"{-x:0} m aft of")} midships\n" +
                    $"gun axis {axis:0.0} m above the full-load waterline · arcs {arcs}\n" +
                    "click selects · wheel: per side ±1 · right-click: position, split, move, remove"));
            }
        // the nominal heights where a track has no mounts yet (a casemate's ports half a deck from the main deck)
        double lh = Layout.LevelH;
        double[] nominal = [fb + lh + 1.2, fb + 1.2, fb + lh / 2, fb - lh / 2];
        var trackAxis = new float[4];
        for (int p = 0; p < 4; p++)
            trackAxis[p] = (float)(axisN[p] > 0 ? axisSum[p] / axisN[p] : nominal[p]);

        // tonnes by battery: its mounts, magazines and barbettes, by mount id
        var tonnes = new double[secs.Count];
        if (ship != null)
        {
            var byId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int k = 0; k < secs.Count; k++)
                foreach (var id in ids[k])
                    byId[id] = k;
            foreach (var w in ship.Report.Weights)
            {
                int sp = w.Name.LastIndexOf(' ');
                if (sp > 0 && byId.TryGetValue(w.Name[(sp + 1)..], out int k))
                    tonnes[k] += w.T;
            }
        }

        bool counted = secs.Any(b => b.Count != null);
        var rows = new SecRow[secs.Count];
        for (int k = 0; k < secs.Count; k++)
        {
            var b = secs[k];
            var mat = Knobs.SecMaterial(k);
            string? mv = mat.Get(d) ?? mat.Default;
            int mi = Array.IndexOf(mat.Values, mv);
            var mlabels = mi < 0 ? [.. mat.Labels, mv ?? "custom"] : mat.Labels;
            bool byCount = b.Count != null;
            int placed = byCount ? placedMounts[k] : placedPairs[k], wanted = SA.Wanted(b);
            var pos = SA.Of(b);
            string where = pos is Pos.House or Pos.Deck ? "amidships, with the other deck secondaries" : $"along the side, in the {PosLabel[(int)pos]} casemate tier";
            rows[k] = new SecRow(k, Row(Knobs.SecCalibre(k), d, ship) with { Delta = "" }, Row(Knobs.SecLength(k), d, ship) with { Delta = "" },
                b.Barrels ?? 1, pos, $"{trackAxis[(int)pos]:0.0} m",
                Row(byCount ? Knobs.SecCount(k) : Knobs.SecPerSide(k), d, ship) with { Delta = "" }, placed, wanted,
                (placedMounts[k] * (b.Barrels ?? 1)).ToString(CultureInfo.InvariantCulture),
                Row(Knobs.SecArmour(k), d, ship) with { Delta = "" }, Row(Knobs.SecRounds(k), d, ship) with { Delta = "" },
                new ChoiceRow(mat, mlabels, mi < 0 ? mlabels.Length - 1 : mi, true), ship != null ? N0(tonnes[k]) : "–",
                $"Only {placed} of {wanted} {(byCount ? "mounts" : "per side")} fit {where}.\n" +
                "Fewer or smaller mounts, another position, or a longer hull. The ledger counts only what fits.");
        }

        // the plan: the deck outline's starboard half, the superstructure, main turrets, AA, torpedo tubes
        var hull = new List<Vector2>();
        var blocks = new List<(Vector2[], long)>();
        var mains = new List<PlanMark>();
        var aa = new List<(Vector2, bool, bool)>();
        var tt = new List<Vector2>();
        int aaHeavy = 0, aaLight = 0, roofsHeavy = 0, roofsLight = 0, ttEdge = 0, ttCentre = 0, ttHouse = 0, ttTubes = 0, ttMounts = 0;
        double aaHeavyT = 0, aaLightT = 0, ttT = 0;
        if (hb != null)
        {
            foreach (var p in hb.Hull)
                if (p.Y >= -1e-6)
                    hull.Add(new Vector2((float)p.X, (float)p.Y));
            hull.Sort((a, b) => a.X.CompareTo(b.X));
            var weight = ship!.Report.Weights.GroupBy(w => w.Name).ToDictionary(g => g.Key, g => g.Sum(w => w.T), StringComparer.Ordinal);
            foreach (var c in hb.Components)
                switch (c.Kind)
                {
                    case "superstructure" when c.Points is { Count: > 2 } pts && c.Role != "director":
                        blocks.Add((pts.Select(q => new Vector2((float)q.X, (float)q.Y)).ToArray(), c.Level ?? 1));
                        break;
                    case "main" when c.X is { } x:
                        mains.Add(new PlanMark((float)x, (float)(c.Y ?? 0), (float)(c.BroadphaseR ?? 4), c.Id, c.Base > 1.5 ? 1 : 0));
                        break;
                    case "aa" when c.X is { } x:
                        bool heavy = c.Type == "quad40", roof = c.Base > 0.5;
                        aa.Add((new Vector2((float)x, (float)(c.Y ?? 0)), heavy, roof));
                        double wt = weight.GetValueOrDefault(c.Id);
                        if (heavy)
                            (aaHeavy, roofsHeavy, aaHeavyT) = (aaHeavy + 1, roofsHeavy + (roof ? 1 : 0), aaHeavyT + wt);
                        else
                            (aaLight, roofsLight, aaLightT) = (aaLight + 1, roofsLight + (roof ? 1 : 0), aaLightT + wt);
                        break;
                    case "torpedo" when c.X is { } x:
                        tt.Add(new Vector2((float)x, (float)(c.Y ?? 0)));
                        ttMounts++;
                        ttTubes = (int)(c.Torpedoes ?? 0);
                        if (Math.Abs(c.Y ?? 0) > 0.5)
                            ttEdge++;
                        else if (c.Base > lh * 0.7)
                            ttHouse++;
                        else
                            ttCentre++;
                        ttT += weight.GetValueOrDefault(c.Id);
                        break;
                }
        }
        float L = (float)(hb?.Length ?? 100), B = (float)(hb?.Beam ?? 12);
        // the amidships stretch the deck guns share: between the end turrets
        float amid0 = mains.Where(m => m.X < -0.2f * L).Select(m => m.X + m.R).DefaultIfEmpty(-0.42f * L).Max();
        float amid1 = mains.Where(m => m.X > 0.2f * L).Select(m => m.X - m.R).DefaultIfEmpty(0.42f * L).Min();

        // guns bearing: the secondaries by battery (bin i is the bearing i + ½°), and the main battery for the toggle
        var roseBy = new float[secs.Count][];
        for (int k = 0; k < secs.Count; k++)
            roseBy[k] = new float[360];
        var roseMain = new float[360];
        int roseTotal = 0;
        if (over != null)
            foreach (var m in over.Mounts)
            {
                float[] into;
                if (m.Main)
                    into = roseMain;
                else if (SA.ParseId(m.Id) is var (k, _, _) && k < secs.Count)
                {
                    into = roseBy[k];
                    roseTotal += m.Guns;
                }
                else
                    continue;
                foreach (var (from, to) in m.Arcs)
                    for (int deg = (int)MathF.Floor(from - 0.5f); deg + 0.5f <= to; deg++)
                        if (deg + 0.5f >= from)
                            into[((deg % 360) + 360) % 360] += m.Guns;
            }

        var aaRows = new[]
        {
            new AaRow("Heavy", Row(Knobs.AaHeavy, d, ship) with { Delta = "" }, "quad 40 mm", aaHeavy, d.Aa?.Heavy ?? 0, 4 * aaHeavy, roofsHeavy,
                ship != null ? N0(aaHeavyT) : "–"),
            new AaRow("Light", Row(Knobs.AaLight, d, ship) with { Delta = "" }, "single 20 mm", aaLight, d.Aa?.Light ?? 0, aaLight, roofsLight,
                ship != null ? N0(aaLightT) : "–"),
        };
        int aaAll = aaHeavy + aaLight, aaRoofs = roofsHeavy + roofsLight;
        string Dirs(string battery, string what)
        {
            int n = (int)(Knobs.Directors(battery).Value(d, ship) ?? 0);
            return $"{n} {what} director{(n == 1 ? "" : "s")}";
        }

        int wantTt = d.Torpedoes?.Mounts ?? 0;
        string placeTt = ttMounts == 0 ? wantTt > 0 ? "none fitted: see the remarks" : "none" :
            string.Join(" · ", new[]
            {
                ttEdge > 0 ? $"{ttEdge / 2} pair{(ttEdge / 2 == 1 ? "" : "s")} at the deck edges" : "",
                ttCentre > 0 ? $"{ttCentre} on the centreline" : "",
                ttHouse > 0 ? $"{ttHouse} on the deckhouse" : "",
            }.Where(s => s.Length > 0));
        int perSide = ttEdge / 2 * ttTubes + (ttCentre + ttHouse) * ttTubes;
        string[] ttLines =
        [
            ttMounts > 0 ? $"{ttMounts}/{Math.Max(wantTt, ttMounts)} · {placeTt}" : placeTt,
            ttMounts > 0 ? $"{perSide} tubes a side · {ttMounts * ttTubes} torpedoes, no reloads" : "–",
            ship != null ? $"{N0(ttT)} t" : "–",
        ];

        return new SecPane(rows, mounts.ToArray(), hull.ToArray(), blocks.ToArray(), mains.ToArray(), aa.ToArray(), tt.ToArray(), L, B,
            amid0, amid1, trackAxis, roseBy, roseMain, roseTotal, aaRows,
            aaAll > 0 ? $"{aaRoofs} on roofs · {aaAll - aaRoofs} at the deck edge" : "none", Dirs("aa", "AA"),
            Row(Knobs.TorpedoMounts, d, ship) with { Delta = "" }, d.Torpedoes?.Tubes ?? 4, ttLines, Dirs("secondary", "secondary"),
            SA.All.Select(p => SA.Offers(style, p)).ToArray(), style.TakesSecondaryList || secs.Count == 0, counted, ship != null);
    }

    // ------------------------------------------------------------------ drawing

    static readonly float[] SecColW = [34, 40, 138, 104, 92, 0, 74, 50, 44, 104, 120, 150, 46, 46];   // 0: the position takes what's left
    readonly float[] secX = new float[SecColW.Length + 1];
    static readonly string[] SecHead = ["", "", "CALIBRE", "LENGTH", "GUNS / MOUNT", "TURRET ON", "PER SIDE", "PLACED", "GUNS", "SHIELDS",
        "ROUNDS / GUN", "MATERIAL", "T", ""];

    void SecondaryPane(Vector2 size)
    {
        var sp = snap!.Sec!;
        ImGui.BeginChild("##secondary", size, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var dl = ImGui.GetWindowDrawList();
        var o = ImGui.GetCursorScreenPos();
        float w = size.X;
        secHotK = secHotNext;
        secHotNext = -1;
        secFocus = secSel ?? (sp.Rows.Length > 0 ? (secActive, 0) : null);

        SecTable(dl, o, w, sp);
        float tableH = P(18) + RowH * Math.Max(sp.Rows.Length + 1, 4);
        float gap = P(6), roseW = MathF.Round(Math.Clamp(w * 0.25f, P(240), P(330)));
        float lowerH = P(150);
        float stripH = Math.Max(P(170), size.Y - tableH - lowerH - 3 * gap);
        var stripPos = o + new Vector2(gap, tableH + gap);
        var stripSize = new Vector2(w - roseW - 3 * gap, stripH);
        Broadside(dl, stripPos, stripSize, sp);
        SecRose(dl, stripPos + new Vector2(stripSize.X + gap, 0), new Vector2(roseW, stripH), sp);
        var low = stripPos + new Vector2(0, stripH + gap);
        float third = MathF.Floor((w - 3 * gap - 2 * gap) / 3);
        AaPanel(dl, low, new Vector2(third, lowerH), sp);
        TtPanel(dl, low + new Vector2(third + gap, 0), new Vector2(third, lowerH), sp);
        SecInspector(dl, low + new Vector2(2 * (third + gap), 0), new Vector2(w - 3 * gap - 2 * (third + gap), lowerH), sp);
        SecMenu(sp);
        ImGui.EndChild();
    }

    void SecTable(ImDrawListPtr dl, Vector2 o, float w, SecPane sp)
    {
        var x = secX;
        float fixedW = 0;
        foreach (var c in SecColW)
            fixedW += P(c);
        x[0] = o.X;
        for (int i = 0; i < SecColW.Length; i++)
            x[i + 1] = x[i] + (SecColW[i] > 0 ? P(SecColW[i]) : Math.Max(P(230), w - fixedW));
        float hh = P(18);
        dl.AddRectFilled(o, o + new Vector2(w, hh), T.U32(T.HeaderBg));
        dl.AddLine(o + new Vector2(0, hh - 1), o + new Vector2(w, hh - 1), T.U32(T.Edge));
        float segW = P(4) + Width(UiFonts.SansSmall, "deckhouse") + P(12) + Width(UiFonts.SansSmall, "deck") + P(12);
        for (int i = 0; i < SecHead.Length; i++)
        {
            string head = i == 6 && sp.Counted ? "MOUNTS" : SecHead[i];
            if (head.Length == 0)
                continue;
            float tx = i is 7 or 8 or 12 ? x[i + 1] - P(6) - Width(UiFonts.SansSmall, head) : x[i] + P(4);
            Text(dl, UiFonts.SansSmall, new Vector2(tx, o.Y + (hh - UiFonts.SansSmall.FontSize) / 2), T.Muted, head);
        }
        if (sp.Offered[(int)Pos.Upper])
            Text(dl, UiFonts.SansSmall, new Vector2(x[5] + P(4) + segW + P(4), o.Y + (hh - UiFonts.SansSmall.FontSize) / 2), T.Muted, "CASEMATE");

        var d = doc.Current;
        float fh = ImGui.GetFrameHeight();
        for (int n = 0; n < sp.Rows.Length; n++)
        {
            var r = sp.Rows[n];
            int k = r.K;
            var p = new Vector2(o.X, o.Y + hh + n * RowH);
            bool on = k == secActive;
            ImGui.PushID(1000 + k);
            ImGui.SetCursorScreenPos(p);
            ImGui.SetNextItemAllowOverlap();
            if (ImGui.InvisibleButton("##row", new Vector2(w, RowH)) && (!on || secSel != null))
            {
                secActive = k;
                secSel = null;
                stale = true;
            }
            bool hover = ImGui.IsItemHovered() || secHotK == k;
            RowBg(dl, p, w, n);
            if (on)
            {
                dl.AddRectFilled(p, p + new Vector2(w, RowH), T.U32(T.Brass with { W = hover ? 0.13f : 0.10f }));
                dl.AddRectFilled(p, p + new Vector2(P(3), RowH), T.U32(T.Brass));
            }
            else if (hover)
                dl.AddRectFilled(p, p + new Vector2(w, RowH), T.U32(new Vector4(1, 1, 1, 0.04f)));
            float cy = p.Y + (RowH - fh) / 2;
            void At(int col, float dx = 4) => ImGui.SetCursorScreenPos(new Vector2(x[col] + P(dx), cy));
            float ty = p.Y + (RowH - UiFonts.Mono.FontSize) / 2;

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            At(0, 2);
            ImGui.BeginDisabled(k == 0);
            if (ImGui.ArrowButton("##up", ImGuiDir.Up))
                SecOrder(k, -1);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(k == sp.Rows.Length - 1);
            if (ImGui.ArrowButton("##dn", ImGuiDir.Down))
                SecOrder(k, 1);
            ImGui.EndDisabled();
            ImGui.PopStyleVar();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                Tip("▲ ▼ move the battery up or down the list: the first deck battery gets the middle of the amidships stretch, the next ones fill outward");

            float sw = P(9);
            var swp = new Vector2(x[1] + P(4), p.Y + (RowH - sw) / 2);
            dl.AddRectFilled(swp, swp + new Vector2(sw, sw), T.U32(SecColour(k)));
            Text(dl, UiFonts.SansBold, new Vector2(swp.X + sw + P(6), p.Y + (RowH - UiFonts.SansBold.FontSize) / 2), on ? T.Brass : T.Bright, SecName(k));

            At(2);
            NumberField(r.Cal, P(76));
            At(3);
            Spinner(r.Len, P(56), extras: false);
            At(4);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 0));
            for (int g = 1; g <= 4; g++)
            {
                if (g > 1)
                    ImGui.SameLine();
                if (Seg(BarrelText[g - 1], r.Barrels == g, P(19)) && r.Barrels != g)
                    Edit(Knobs.SecBarrels(k).Apply(d, g), $"Battery {SecName(k)}: {g} guns per mount");
            }
            ImGui.PopStyleVar();

            // the position: one click sets mount, tier and stands_on
            At(5);
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 0));
            bool firstSeg = true;
            foreach (var pos in SA.All)
            {
                if (!sp.Offered[(int)pos])
                    continue;
                if (!firstSeg)
                    ImGui.SameLine(0, pos == Pos.Upper ? P(5) : 1);
                firstSeg = false;
                if (Seg(PosLabel[(int)pos], r.Pos == pos) && r.Pos != pos)
                    Edit(SA.SetPos(d, k, pos), $"Battery {SecName(k)} {SA.Text(pos)}");
                if (ImGui.IsItemHovered())
                    Tip(PosTip(pos, sp.TrackAxis[(int)pos]));
            }
            ImGui.PopStyleVar();
            ImGui.PopFont();
            Text(dl, UiFonts.SansSmall, new Vector2(x[6] - P(6) - Width(UiFonts.SansSmall, r.Axis), p.Y + (RowH - UiFonts.SansSmall.FontSize) / 2), T.Muted, r.Axis);

            At(6);
            Spinner(r.Per, P(26), extras: false);
            string placed = sp.Built ? $"{r.Placed}/{r.Wanted}" : "–";
            bool shortOf = sp.Built && r.Placed < r.Wanted;
            var pp = new Vector2(x[8] - P(6) - Width(UiFonts.Mono, placed), ty);
            Text(dl, UiFonts.Mono, pp, shortOf ? T.Amber : T.Muted, placed);
            if (shortOf && ImGui.IsMouseHoveringRect(pp, pp + new Vector2(Width(UiFonts.Mono, placed), UiFonts.Mono.FontSize)))
                Tip(r.ShortTip);
            if (shortOf && secForceTip == $"short:{k}")
                TipAt(r.ShortTip, pp + new Vector2(0, UiFonts.Mono.FontSize + P(4)));
            Text(dl, UiFonts.Mono, new Vector2(x[9] - P(6) - Width(UiFonts.Mono, r.Guns), ty), T.Bright, r.Guns);
            At(9);
            Spinner(r.Shields, P(56), extras: false);
            At(10);
            Spinner(r.Rounds, P(40), extras: true);
            At(11);
            ImGui.SetNextItemWidth(P(142));
            int mi = r.Material.Index;
            if (ImGui.Combo("##mat", ref mi, r.Material.Labels, r.Material.Labels.Length) && mi < r.Material.Knob.Values.Length)
                Edit(r.Material.Knob.Set(d, r.Material.Knob.Values[mi]), $"Battery {SecName(k)} material: {r.Material.Labels[mi]}");
            Text(dl, UiFonts.Mono, new Vector2(x[13] - P(6) - Width(UiFonts.Mono, r.Tonnes), ty), T.Muted, r.Tonnes);
            At(13, 2);
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.BeginDisabled(!sp.TakesList);
            if (ImGui.SmallButton("+"))
            {
                Edit(SA.Duplicate(d, k), $"Battery {SecName(k)} duplicated");
                secActive = k + 1;
                secSel = null;
            }
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                Tip("Duplicate: the same gun as a new battery with one pair");
            ImGui.SameLine(0, P(2));
            if (ImGui.SmallButton("x"))
            {
                secSel = null;
                Edit(RemoveAt(d, k, main: false), $"Secondary battery {SecName(k)} removed");
            }
            if (ImGui.IsItemHovered())
                Tip("Remove the battery");
            ImGui.PopFont();
            ImGui.PopID();
        }

        var ap = new Vector2(o.X + P(38), o.Y + hh + sp.Rows.Length * RowH + (RowH - fh) / 2);
        ImGui.SetCursorScreenPos(ap);
        ImGui.PushFont(UiFonts.SansSmall);
        if (sp.TakesList)
        {
            if (ImGui.SmallButton("+ Battery"))
            {
                Edit(AddSecondary(d), "Secondary battery added");
                secActive = sp.Rows.Length;
                secSel = null;
            }
            ImGui.SameLine(0, P(8));
        }
        ImGui.TextColored(T.Muted, sp.Rows.Length == 0 ? "No secondary battery."
            : "or click a track's + in the strip below to start a battery there with the selected gun");
        ImGui.PopFont();
    }

    static string PosTip(Pos p, float axis) => p switch
    {
        Pos.House => "On the deckhouse: a deck mount raised a level, clear of the main turrets' blast, with better arcs and more top weight.",
        Pos.Deck => "On the deck, beside the deckhouse: shares the amidships stretch with the deckhouse guns.",
        Pos.Upper => "Upper casemate: a housing on the main deck, drier than the lower tier; single guns, 60° either side of the beam.",
        _ => "Lower casemate: in the hull side half a deck under the main deck, armoured by the side but low and wet in a seaway.",
    } + $"\nGun axis about {axis:0.0} m above the full-load waterline.";

    void SecOrder(int k, int dir)
    {
        Edit(SA.MoveBattery(doc.Current, k, dir), $"Secondary battery {SecName(k)} moved {(dir < 0 ? "up" : "down")}");
        if (secActive == k)
            secActive = k + dir;
        else if (secActive == k + dir)
            secActive = k;
        secSel = null;
    }

    // ------------------------------------------------------------------ the broadside strip

    void Broadside(ImDrawListPtr dl, Vector2 pos, Vector2 size, SecPane sp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Plan));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "BROADSIDE", "starboard half in plan · positions below, schematic: the number is the gun axis above the waterline");
        var d = doc.Current;
        var io = ImGui.GetIO();
        float labelW = P(104), pad = P(12);
        float x0 = pos.X + labelW, x1 = pos.X + size.X - pad;
        float kx = (x1 - x0) / sp.L;
        float bodyTop = pos.Y + P(18), bodyH = size.Y - P(18);
        float planH = MathF.Round(bodyH * 0.38f);
        float cl = bodyTop + planH;   // the centreline: the plan band's bottom edge
        float ky = Math.Min(kx, (planH - P(10)) / (sp.B / 2));
        float X(float m) => x0 + (m + sp.L / 2) * kx;
        Vector2 Pl(float mx, float my) => new(X(mx), cl - my * ky);
        dl.PushClipRect(pos + new Vector2(1, P(18)), pos + size - new Vector2(1, 1), true);
        for (float gx = pos.X + P(24); gx < pos.X + size.X; gx += P(24))
            dl.AddLine(new Vector2(gx, bodyTop), new Vector2(gx, pos.Y + size.Y), T.U32(new Vector4(0.59f, 0.75f, 0.9f, 0.05f)));
        for (float gy = bodyTop + P(24); gy < pos.Y + size.Y; gy += P(24))
            dl.AddLine(new Vector2(pos.X, gy), new Vector2(pos.X + size.X, gy), T.U32(new Vector4(0.59f, 0.75f, 0.9f, 0.05f)));

        // ---- the plan band
        dl.PushClipRect(new Vector2(pos.X, bodyTop), new Vector2(pos.X + size.X, cl + 0.5f), true);
        if (sp.Hull.Length > 1)
        {
            foreach (var h in sp.Hull)
                dl.PathLineTo(Pl(h.X, h.Y));
            dl.PathLineTo(Pl(sp.Hull[^1].X, 0));
            dl.PathLineTo(Pl(sp.Hull[0].X, 0));
            dl.PathFillConvex(T.U32(T.PlanLine with { W = 0.06f }));
            foreach (var h in sp.Hull)
                dl.PathLineTo(Pl(h.X, h.Y));
            dl.PathStroke(T.U32(T.PlanLine with { W = 0.8f }), ImDrawFlags.None, 1);
        }
        foreach (var (pts, lv) in sp.Blocks.OrderBy(b => b.Level))
        {
            foreach (var q in pts)
                dl.PathLineTo(Pl(q.X, q.Y));
            dl.PathFillConvex(T.U32(lv > 1 ? new Vector4(0.17f, 0.23f, 0.29f, 1) : new Vector4(0.13f, 0.19f, 0.25f, 1)));
            foreach (var q in pts)
                dl.PathLineTo(Pl(q.X, q.Y));
            dl.PathStroke(T.U32(new Vector4(0.24f, 0.32f, 0.4f, 1)), ImDrawFlags.Closed, 1);
        }
        foreach (var m in sp.Mains)
        {
            var c = Pl(m.X, m.Y);
            dl.AddCircleFilled(c, m.R * ky, T.U32(new Vector4(0.23f, 0.27f, 0.32f, 1)));
            dl.AddCircle(c, m.R * ky, T.U32(new Vector4(0.34f, 0.39f, 0.43f, 1)));
        }
        dl.PopClipRect();
        foreach (var m in sp.Mains)
            if (m.Y >= 0)
            {
                var c = Pl(m.X, m.Y);
                float tw = Width(UiFonts.SansSmall, m.Id);
                Text(dl, UiFonts.SansSmall, new Vector2(c.X - tw / 2, Math.Min(c.Y, cl) - UiFonts.SansSmall.FontSize - P(2)), T.Muted, m.Id);
            }
        dl.AddLine(new Vector2(X(-sp.L / 2) - P(6), cl), new Vector2(X(sp.L / 2) + P(6), cl), T.U32(new Vector4(0.31f, 0.42f, 0.52f, 1)));
        // the amidships stretch, under the plan's top edge
        if (sp.AmidX1 > sp.AmidX0)
        {
            float by = bodyTop + P(16);
            uint bc = T.U32(new Vector4(0.31f, 0.42f, 0.52f, 1));
            dl.AddLine(new Vector2(X(sp.AmidX0), by + P(3)), new Vector2(X(sp.AmidX0), by), bc);
            dl.AddLine(new Vector2(X(sp.AmidX0), by), new Vector2(X(sp.AmidX1), by), bc);
            dl.AddLine(new Vector2(X(sp.AmidX1), by), new Vector2(X(sp.AmidX1), by + P(3)), bc);
            string lab = $"between the end turrets · {sp.AmidX1 - sp.AmidX0:0} m";
            float lw = Width(UiFonts.SansSmall, lab);
            var lp = new Vector2((X(sp.AmidX0) + X(sp.AmidX1) - lw) / 2, by - UiFonts.SansSmall.FontSize - P(1));
            dl.AddRectFilled(lp - V(3, 0), lp + new Vector2(lw + P(3), UiFonts.SansSmall.FontSize), T.U32(T.Plan));
            Text(dl, UiFonts.SansSmall, lp, new Vector4(0.44f, 0.51f, 0.6f, 1), lab);
        }
        foreach (var (p, heavy, roof) in sp.Aa)
            if (p.Y >= 0)
                dl.AddCircleFilled(Pl(p.X, p.Y), heavy ? P(2.6f) : P(1.5f), T.U32(new Vector4(0.91f, 0.93f, 0.95f, roof ? 0.9f : 0.7f)));
        foreach (var t in sp.Tt)
            if (t.Y >= -0.5f)
            {
                var c = Pl(t.X, t.Y);
                dl.AddRectFilled(c - new Vector2(3.5f * kx, P(1.6f)), c + new Vector2(3.5f * kx, P(1.6f)), T.U32(new Vector4(0.62f, 0.78f, 0.91f, 0.85f)));
            }

        // ---- the tracks
        var shown = TrackOrder.Where(p => sp.Offered[(int)p]).ToList();
        float tTop = cl + P(26), tBot = pos.Y + size.Y - P(16);
        float pitch = shown.Count > 1 ? (tBot - tTop) / (shown.Count - 1 + 0.6f) : 0;
        var trackY = new float[4];
        for (int i = 0; i < shown.Count; i++)
            trackY[(int)shown[i]] = tTop + i * pitch + (shown[i] == Pos.Lower ? pitch * 0.6f : 0);
        float deckY = sp.Offered[(int)Pos.Lower] ? trackY[(int)Pos.Lower] - pitch * 0.65f : tBot - P(4);
        float wlY = sp.Offered[(int)Pos.Lower] ? trackY[(int)Pos.Lower] + pitch * 0.55f : tBot + P(8);
        float roofY = sp.Offered[(int)Pos.House] ? trackY[(int)Pos.House] + P(8) : deckY;
        float hullL = X(-sp.L / 2), hullR = X(sp.L / 2);
        dl.AddRectFilled(new Vector2(hullL, deckY), new Vector2(hullR, wlY), T.U32(T.PlanLine with { W = 0.06f }));
        dl.AddLine(new Vector2(hullL, deckY), new Vector2(hullR, deckY), T.U32(T.PlanLine with { W = 0.8f }));
        dl.AddLine(new Vector2(hullL - P(8), wlY), new Vector2(hullR + P(8), wlY), T.U32(T.Blue), P(1.2f));
        Text(dl, UiFonts.SansSmall, new Vector2(hullR + P(2), wlY - UiFonts.SansSmall.FontSize - P(1)), T.Blue, "WL");
        // the superstructure as blocks in the elevation: its extent from the plan, its level
        foreach (var (pts, lv) in sp.Blocks.OrderBy(b => b.Level))
        {
            float bx0 = pts.Min(q => q.X), bx1 = pts.Max(q => q.X);
            float top = lv > 1 ? Math.Max(cl + P(10), roofY - (lv - 1) * P(8)) : roofY;
            dl.AddRectFilled(new Vector2(X(bx0), top), new Vector2(X(bx1), deckY), T.U32(lv > 1 ? new Vector4(0.17f, 0.23f, 0.29f, 0.45f) : new Vector4(0.13f, 0.19f, 0.25f, 0.7f)));
            dl.AddRect(new Vector2(X(bx0), top), new Vector2(X(bx1), deckY), T.U32(new Vector4(0.2f, 0.28f, 0.35f, 1)));
        }
        foreach (var m in sp.Mains)
        {
            if (Math.Abs(m.Y) > 0.5f)
                continue;
            float w = m.R * kx, z = deckY - m.Tier * P(12), hgt = P(16);
            var grey = T.U32(new Vector4(0.23f, 0.27f, 0.32f, 1));
            if (m.Tier > 0)
                dl.AddRectFilled(new Vector2(X(m.X) - w * 0.7f, z), new Vector2(X(m.X) + w * 0.7f, deckY), T.U32(new Vector4(0.18f, 0.22f, 0.26f, 1)));
            dl.AddQuadFilled(new Vector2(X(m.X) - w, z), new Vector2(X(m.X) - w * 0.8f, z - hgt), new Vector2(X(m.X) + w * 0.8f, z - hgt),
                new Vector2(X(m.X) + w, z), grey);
            float dir = m.X > 0 ? 1 : -1;
            dl.AddLine(new Vector2(X(m.X) + dir * w * 0.7f, z - hgt / 2), new Vector2(X(m.X) + dir * w * 2.1f, z - hgt / 2), grey, P(2));
        }

        bool anyHover = false;
        // the track heads: label, the true height, and the + that adds the selected battery's pair there
        for (int i = 0; i < shown.Count; i++)
        {
            var p = shown[i];
            float y = trackY[(int)p];
            dl.AddLine(new Vector2(x0 - P(6), y), new Vector2(x1, y), T.U32(T.PlanLine with { W = 0.12f }));
            var a = new Vector2(pos.X + P(4), y - P(7));
            ImGui.SetCursorScreenPos(a);
            ImGui.PushID(6000 + i);
            bool click = ImGui.InvisibleButton("##lane", new Vector2(labelW - P(6), P(14)));
            bool hover = ImGui.IsItemHovered();
            ImGui.PopID();
            bool here = sp.Rows.Length > 0 && sp.Rows[secActive].Pos == p;
            var lc = here ? SecColour(secActive) : hover ? T.Text : new Vector4(0.34f, 0.39f, 0.43f, 1);
            var bp = new Vector2(pos.X + P(6), y - P(5.5f));
            DashedRect(dl, bp, bp + V(11, 11), T.U32(lc));
            Text(dl, UiFonts.SansSmall, bp + new Vector2(P(5.5f) - Width(UiFonts.SansSmall, "+") / 2, P(5.5f) - UiFonts.SansSmall.FontSize / 2), lc, "+");
            Text(dl, UiFonts.SansSmall, new Vector2(pos.X + P(22), y - UiFonts.SansSmall.FontSize / 2), new Vector4(0.58f, 0.65f, 0.69f, 1), TrackLabel[(int)p]);
            string ax = $"{sp.TrackAxis[(int)p]:0.0}";
            Text(dl, UiFonts.SansSmall, new Vector2(x0 - P(8) - Width(UiFonts.SansSmall, ax), y - UiFonts.SansSmall.FontSize / 2), new Vector4(0.44f, 0.51f, 0.6f, 1), ax);
            string tip = LaneTip(sp, p);
            if (hover)
            {
                anyHover = true;
                Tip(tip);
            }
            if (secForceTip == $"lane:{(int)p}")
                TipAt(tip, a + new Vector2(P(30), P(16)));
            if (click)
                LaneAdd(d, sp, p);
        }

        // the secondaries: a plan glyph and a track glyph each, one button over both
        for (int i = 0; i < sp.Mounts.Length; i++)
        {
            var m = sp.Mounts[i];
            var col = SecColour(m.K);
            bool sel = secSel is { } s && s.K == m.K && s.N == m.N;
            bool lit = m.K == secActive || m.K == secHotK;
            float a = lit ? 1 : 0.62f;
            float r = Math.Max(P(2.6f), m.R * ky);
            var pc = Pl(m.X, m.Y);
            float ty = trackY[(int)m.Track];
            float tr = Math.Max(P(3), m.R * kx);
            var fill = new Vector4(col.X * 0.45f, col.Y * 0.45f, col.Z * 0.45f, a);
            var edge = col with { W = a };
            // plan
            if (m.Casemate)
            {
                dl.AddRectFilled(new Vector2(pc.X - tr, pc.Y - r * 0.8f), new Vector2(pc.X + tr, pc.Y + r * 0.8f), T.U32(fill));
                dl.AddRect(new Vector2(pc.X - tr, pc.Y - r * 0.8f), new Vector2(pc.X + tr, pc.Y + r * 0.8f), T.U32(edge));
                dl.AddLine(new Vector2(pc.X, pc.Y - r * 0.8f), new Vector2(pc.X, pc.Y - r * 0.8f - m.Barrel * 0.5f * ky - P(2)), T.U32(edge), P(1.5f));
            }
            else
            {
                dl.AddLine(new Vector2(pc.X, pc.Y - r), new Vector2(pc.X, pc.Y - r - m.Barrel * 0.6f * ky), T.U32(edge), m.Barrels > 1 ? P(1.8f) : P(1.1f));
                dl.AddCircleFilled(pc, r, T.U32(fill));
                dl.AddCircle(pc, r, T.U32(edge), 0, P(1.2f));
            }
            // track
            float dirX = m.X >= 0 ? 1 : -1;
            switch (m.Track)
            {
                case Pos.Lower:
                    dl.AddRectFilled(new Vector2(X(m.X) - tr, ty - P(5)), new Vector2(X(m.X) + tr, ty + P(5)), T.U32(fill));
                    dl.AddRect(new Vector2(X(m.X) - tr, ty - P(5)), new Vector2(X(m.X) + tr, ty + P(5)), T.U32(edge));
                    dl.AddCircleFilled(new Vector2(X(m.X), ty), P(2.1f), T.U32(edge));
                    break;
                case Pos.Upper:
                    dl.AddRectFilled(new Vector2(X(m.X) - tr, ty - P(7)), new Vector2(X(m.X) + tr, deckY), T.U32(fill));
                    dl.AddRect(new Vector2(X(m.X) - tr, ty - P(7)), new Vector2(X(m.X) + tr, deckY), T.U32(edge));
                    dl.AddCircleFilled(new Vector2(X(m.X), ty), P(2.1f), T.U32(edge));
                    break;
                default:
                    float bse = m.Track == Pos.Deck ? deckY : roofY, top = ty - P(6);
                    dl.AddQuadFilled(new Vector2(X(m.X) - tr, bse), new Vector2(X(m.X) - tr * 0.75f, top), new Vector2(X(m.X) + tr * 0.75f, top),
                        new Vector2(X(m.X) + tr, bse), T.U32(fill));
                    dl.PathLineTo(new Vector2(X(m.X) - tr, bse));
                    dl.PathLineTo(new Vector2(X(m.X) - tr * 0.75f, top));
                    dl.PathLineTo(new Vector2(X(m.X) + tr * 0.75f, top));
                    dl.PathLineTo(new Vector2(X(m.X) + tr, bse));
                    dl.PathStroke(T.U32(edge), ImDrawFlags.Closed, 1);
                    dl.AddLine(new Vector2(X(m.X) + dirX * tr * 0.7f, ty), new Vector2(X(m.X) + dirX * (tr + m.Barrel * 0.6f * kx), ty), T.U32(edge), P(1.3f));
                    break;
            }
            string tag = $"{SecName(m.K)}{m.N}";
            float tw = Width(UiFonts.SansSmall, tag);
            float tagY = m.Track == Pos.Lower ? ty + P(6) : ty - P(8) - UiFonts.SansSmall.FontSize;
            Text(dl, UiFonts.SansSmall, new Vector2(X(m.X) - tw / 2, tagY), edge, tag);
            if (sel)
            {
                uint brass = T.U32(new Vector4(1, 0.906f, 0.659f, 1));
                dl.AddRect(pc - new Vector2(tr + P(3), r + P(3)), pc + new Vector2(tr + P(3), r + P(3)), brass, 0, ImDrawFlags.None, P(1.4f));
                dl.AddRect(new Vector2(X(m.X) - tr - P(3), ty - P(10)), new Vector2(X(m.X) + tr + P(3), ty + P(8)), brass, 0, ImDrawFlags.None, P(1.4f));
            }
            // one button over the track glyph, one over the plan glyph
            for (int b = 0; b < 2; b++)
            {
                var (bmin, bmax) = b == 0
                    ? (new Vector2(X(m.X) - tr - P(1), ty - P(9)), new Vector2(X(m.X) + tr + P(1), ty + P(7)))
                    : (pc - new Vector2(Math.Max(tr, r) + P(1), r + P(1)), pc + new Vector2(Math.Max(tr, r) + P(1), r + P(1)));
                ImGui.SetCursorScreenPos(bmin);
                ImGui.PushID(7000 + 2 * i + b);
                bool clicked = ImGui.InvisibleButton("##sm", Vector2.Max(bmax - bmin, V(4, 4)), ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
                bool hover = ImGui.IsItemHovered();
                bool right = clicked && ImGui.IsMouseReleased(ImGuiMouseButton.Right);
                ImGui.PopID();
                if (clicked)
                {
                    if (secSel != (m.K, m.N) || secActive != m.K)
                    {
                        secSel = (m.K, m.N);
                        secActive = m.K;
                        stale = true;
                    }
                    if (right)
                    {
                        secMenuFor = (m.K, m.N);
                        openSecMenu = true;
                    }
                }
                if (hover)
                {
                    anyHover = true;
                    secHotNext = m.K;
                    secFocus = (m.K, m.N);
                    Tip(m.Tip);
                    if (io.MouseWheel != 0)
                    {
                        ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY);
                        PairWheel(d, m.K, io.MouseWheel > 0);
                    }
                }
            }
        }

        // what didn't fit: amber ghosts at the end of their track
        foreach (var r in sp.Rows)
        {
            int miss = sp.Built ? r.Wanted - r.Placed : 0;
            if (miss <= 0 || !sp.Offered[(int)r.Pos])
                continue;
            float y = trackY[(int)r.Pos];
            int shownN = Math.Min(miss, 6);
            float gx = x1 - P(4);
            for (int j = 0; j < shownN; j++)
            {
                var g0 = new Vector2(gx - P(12) - j * P(14), y - P(5));
                DashedRect(dl, g0, g0 + V(10, 10), T.U32(T.Amber));
            }
            string lab = $"{SecName(r.K)} ×{miss} no room";
            var lp = new Vector2(gx - P(16) - shownN * P(14) - Width(UiFonts.SansSmall, lab), y - UiFonts.SansSmall.FontSize / 2);
            Text(dl, UiFonts.SansSmall, lp, T.Amber, lab);
            if (ImGui.IsMouseHoveringRect(lp, new Vector2(gx, y + P(6))))
            {
                anyHover = true;
                Tip(r.ShortTip);
            }
        }
        dl.PopClipRect();
        if (!anyHover && sp.Rows.Length == 0 && !sp.Built)
            Text(dl, UiFonts.SansSmall, pos + new Vector2(labelW, size.Y / 2), T.Muted, "building…");
    }

    static void DashedRect(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col)
    {
        float dash = P(2), gap = P(1.5f);
        void Edge(Vector2 p0, Vector2 p1)
        {
            float len = Vector2.Distance(p0, p1);
            var dir = (p1 - p0) / Math.Max(len, 1e-3f);
            for (float t = 0; t < len; t += dash + gap)
                dl.AddLine(p0 + dir * t, p0 + dir * Math.Min(len, t + dash), col);
        }
        Edge(a, new Vector2(b.X, a.Y));
        Edge(new Vector2(b.X, a.Y), b);
        Edge(b, new Vector2(a.X, b.Y));
        Edge(new Vector2(a.X, b.Y), a);
    }

    string LaneTip(SecPane sp, Pos p)
    {
        if (sp.Rows.Length == 0)
            return $"+ a battery {SA.Text(p)}";
        var r = sp.Rows[secActive];
        string gun = $"{r.Barrels} × {r.Cal.Value}/{r.Len.Plain}";
        if (r.Pos == p)
            return $"+ a pair of {SecName(r.K)} {SA.Text(p)}\n{r.Per.Value} → one more";
        if (!sp.TakesList)
            return $"Move battery {SecName(r.K)} {SA.Text(p)}\n(this style takes one secondary battery)";
        return $"+ new battery {SecName(sp.Rows.Length)} {SA.Text(p)}\nwith {SecName(r.K)}'s gun ({gun}), one pair.\nTo move all of {SecName(r.K)} here, use its position buttons.";
    }

    void LaneAdd(Design d, SecPane sp, Pos p)
    {
        if (sp.Rows.Length == 0)
        {
            Edit(SA.SetPos(AddSecondary(d), 0, p), $"Secondary battery added {SA.Text(p)}");
            secActive = 0;
            return;
        }
        int k = secActive;
        var (next, j) = SA.AddPairAt(d, k, p, sp.TakesList);
        string label = j != k ? $"New battery {SecName(j)} {SA.Text(p)} with {SecName(k)}'s gun"
            : SA.Of(d.Secondary![k]) == p ? $"Battery {SecName(k)}: a pair added" : $"Battery {SecName(k)} {SA.Text(p)}";
        Edit(next, label);
        secActive = j;
        secSel = null;
    }

    void PairWheel(Design d, int k, bool up)
    {
        Edit(up ? SA.AddPair(d, k) : SA.RemovePair(d, k), $"Battery {SecName(k)}: a pair {(up ? "added" : "removed")}");
        secSel = null;
    }

    void RemoveSecPair((int K, int N) s)
    {
        Edit(SA.RemovePair(doc.Current, s.K), $"Battery {SecName(s.K)}: pair {SecName(s.K)}{s.N} removed");
        secSel = null;
    }

    void SecMenu(SecPane sp)
    {
        if (openSecMenu)
        {
            ImGui.OpenPopup("##secmenu");
            openSecMenu = false;
        }
        if (!ImGui.BeginPopup("##secmenu"))
            return;
        var d = doc.Current;
        var (k, n) = secMenuFor;
        var secs = d.Secondary ?? [];
        if (k >= secs.Count)
        {
            ImGui.EndPopup();
            return;
        }
        var pos = SA.Of(secs[k]);
        ImGui.TextColored(T.Muted, $"{SecName(k)}{n} pair · battery {SecName(k)}");
        ImGui.Separator();
        foreach (var p in SA.All)
            if (sp.Offered[(int)p] && ImGui.MenuItem($"Battery {SA.Text(p)}", p == pos ? "now" : null, false, p != pos))
                Edit(SA.SetPos(d, k, p), $"Battery {SecName(k)} {SA.Text(p)}");
        if (sp.TakesList)
        {
            ImGui.Separator();
            if (ImGui.MenuItem("New battery from this pair"))
            {
                Edit(SA.SplitPair(d, k), $"A pair of {SecName(k)} split off as battery {SecName(k + 1)}");
                secActive = k + 1;
                secSel = null;
            }
            for (int j = 0; j < secs.Count; j++)
                if (j != k && ImGui.MenuItem($"Move this pair to {SecName(j)} ({secs[j].Barrels ?? 1} × {Fmt(secs[j].CalibreMm ?? 0, Quantity.Calibre)}, {PosLabel[(int)SA.Of(secs[j])]})"))
                {
                    Edit(SA.MovePair(d, k, j), $"A pair of {SecName(k)} moved to {SecName(j)}");
                    secActive = j;
                    secSel = null;
                }
        }
        ImGui.Separator();
        if (ImGui.MenuItem("Remove this pair", "Del"))
            RemoveSecPair((k, n));
        ImGui.EndPopup();
    }

    // ------------------------------------------------------------------ guns bearing

    void SecRose(ImDrawListPtr dl, Vector2 pos, Vector2 size, SecPane sp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Panel2));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "GUNS BEARING", null);
        // the toggle in the header: secondaries, or with the main battery
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(5), 0));
        float bw0 = Width(UiFonts.SansSmall, "secondaries") + P(10), bw1 = Width(UiFonts.SansSmall, "+ main") + P(10);
        ImGui.SetCursorScreenPos(new Vector2(pos.X + size.X - P(4) - bw0 - bw1 - 1, pos.Y + P(2)));
        if (Seg("secondaries", !secRoseAll, bw0))
            secRoseAll = false;
        ImGui.SameLine();
        if (Seg("+ main", secRoseAll, bw1))
            secRoseAll = true;
        ImGui.PopStyleVar(2);
        ImGui.PopFont();

        int nb = sp.RoseBy.Length;
        var sum = new float[360];
        for (int a = 0; a < 360; a++)
            for (int k = 0; k < nb; k++)
                sum[a] += sp.RoseBy[k][a];
        float peak = 1;
        for (int a = 0; a < 360; a++)
            peak = Math.Max(peak, sum[a] + (secRoseAll ? sp.RoseMain[a] : 0));
        float textH = RowH * 3 + P(4);
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
        float sh = Math.Min(P(22), R * 0.25f);
        dl.AddQuadFilled(c + new Vector2(0, -sh), c + new Vector2(sh * 0.3f, sh * 0.2f), c + new Vector2(0, sh), c + new Vector2(-sh * 0.3f, sh * 0.2f),
            T.U32(new Vector4(0.16f, 0.2f, 0.25f, 1)));
        Vector2 At(Func<int, float> f, int deg)
        {
            float rad = deg * MathF.PI / 180, v = f(deg % 360) / peak * R;
            return c + new Vector2(MathF.Sin(rad), -MathF.Cos(rad)) * v;
        }
        void Poly(Func<int, float> f, Vector4 fill, Vector4 line)
        {
            uint fc = T.U32(fill);
            for (int a = 0; a < 360; a++)
                dl.AddTriangleFilled(c, At(f, a), At(f, a + 1), fc);
            for (int a = 0; a <= 360; a++)
                dl.PathLineTo(At(f, a));
            dl.PathStroke(T.U32(line), ImDrawFlags.None, P(1.2f));
        }
        if (secRoseAll)
            Poly(a => sum[a] + sp.RoseMain[a], T.Blue with { W = 0.2f }, T.Blue);
        // stacked by battery: the outermost line is all the secondaries
        for (int k = nb - 1; k >= 0; k--)
        {
            int kk = k;
            var col = SecColour(k);
            Poly(a =>
            {
                float v = 0;
                for (int j = 0; j <= kk; j++)
                    v += sp.RoseBy[j][a];
                return v;
            }, new Vector4(col.X * 0.42f, col.Y * 0.42f, col.Z * 0.42f, 1), col);
        }
        void Label(int deg, string t)
        {
            float rad = deg * MathF.PI / 180;
            var p = c + new Vector2(MathF.Sin(rad), -MathF.Cos(rad)) * (R + P(12));
            Text(dl, UiFonts.SansSmall, p - new Vector2(Width(UiFonts.SansSmall, t) / 2, UiFonts.SansSmall.FontSize / 2), T.Muted, t);
        }
        Label(0, "ahead");
        Label(180, "astern");
        Label(90, "stbd");
        Label(270, "port");
        float y = pos.Y + size.Y - textH;
        var lines = new (string, string, string)[]
        {
            ("Broadside port · stbd", $"{sum[269]:0} · {sum[89]:0}", $"of {sp.RoseTotal} guns"),
            ("Ahead · astern", $"{Math.Min(sum[359], sum[0]):0} · {Math.Min(sum[179], sum[180]):0}", "guns"),
        };
        for (int i = 0; i < lines.Length; i++)
        {
            var (l, v, n) = lines[i];
            float ty = y + i * RowH + (RowH - UiFonts.Sans.FontSize) / 2;
            Text(dl, UiFonts.Sans, new Vector2(pos.X + P(8), ty), T.Muted, l);
            Text(dl, UiFonts.Mono, new Vector2(pos.X + size.X * 0.66f - Width(UiFonts.Mono, v), ty), T.Bright, v);
            Text(dl, UiFonts.SansSmall, new Vector2(pos.X + size.X - P(8) - Width(UiFonts.SansSmall, n), ty + P(1)), T.Muted, n);
        }
        float bx = pos.X + P(8), by = y + 2 * RowH + (RowH - UiFonts.SansSmall.FontSize) / 2;
        Text(dl, UiFonts.SansSmall, new Vector2(bx, by), T.Muted, "per broadside");
        bx += Width(UiFonts.SansSmall, "per broadside") + P(8);
        for (int k = 0; k < nb && bx < pos.X + size.X - P(30); k++)
        {
            string t = $"{SecName(k)} {sp.RoseBy[k][89]:0}";
            Text(dl, UiFonts.Mono, new Vector2(bx, by - P(1)), SecColour(k), t);
            bx += Width(UiFonts.Mono, t) + P(9);
        }
    }

    // ------------------------------------------------------------------ AA, torpedoes, the selected battery

    void AaPanel(ImDrawListPtr dl, Vector2 pos, Vector2 size, SecPane sp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Panel2));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "ANTI-AIRCRAFT", "on roofs, then the deck edge, from the middle out");
        float x = pos.X + P(8), y = pos.Y + P(22);
        float[] cx = [x, x + P(46), x + P(140), x + P(236), x + P(286), size.X + pos.X - P(10)];
        string[] head = ["", "MOUNTS", "TYPE", "BARRELS", "PLACED", "T"];
        for (int i = 1; i < head.Length; i++)
            Text(dl, UiFonts.SansSmall, new Vector2(i >= 3 ? cx[i] - Width(UiFonts.SansSmall, head[i]) : cx[i], y), T.Muted, head[i]);
        y += P(16);
        float fh = ImGui.GetFrameHeight();
        for (int i = 0; i < sp.AaRows.Length; i++)
        {
            var r = sp.AaRows[i];
            float ty = y + (RowH - UiFonts.Mono.FontSize) / 2;
            Text(dl, UiFonts.Sans, new Vector2(cx[0], y + (RowH - UiFonts.Sans.FontSize) / 2), T.Muted, r.Label);
            ImGui.SetCursorScreenPos(new Vector2(cx[1], y + (RowH - fh) / 2));
            ImGui.PushID(8000 + i);
            Spinner(r.Knob, P(30), extras: false);
            ImGui.PopID();
            Text(dl, UiFonts.SansSmall, new Vector2(cx[2], y + (RowH - UiFonts.SansSmall.FontSize) / 2), T.Muted, r.Type);
            string b = sp.Built ? r.Barrels.ToString(CultureInfo.InvariantCulture) : "–";
            Text(dl, UiFonts.Mono, new Vector2(cx[3] - Width(UiFonts.Mono, b), ty), T.Bright, b);
            string pl = sp.Built ? $"{r.Placed}/{r.Wanted}" : "–";
            Text(dl, UiFonts.Mono, new Vector2(cx[4] - Width(UiFonts.Mono, pl), ty), sp.Built && r.Placed < r.Wanted ? T.Amber : T.Muted, pl);
            Text(dl, UiFonts.Mono, new Vector2(cx[5] - Width(UiFonts.Mono, r.Tonnes), ty), T.Muted, r.Tonnes);
            y += RowH;
        }
        y += P(6);
        KeyValue(dl, x, ref y, "Placement", sp.AaPlace);
        KeyValue(dl, x, ref y, "Directors", sp.AaDirs + " · set on Fire control (6)");
    }

    void KeyValue(ImDrawListPtr dl, float x, ref float y, string key, string value)
    {
        Text(dl, UiFonts.Sans, new Vector2(x, y), T.Muted, key);
        Text(dl, UiFonts.Sans, new Vector2(x + P(80), y), T.Text, value);
        y += P(17);
    }

    void TtPanel(ImDrawListPtr dl, Vector2 pos, Vector2 size, SecPane sp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Panel2));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "TORPEDO TUBES", "533 mm · placed by the yard");
        var d = doc.Current;
        float x = pos.X + P(8), y = pos.Y + P(24);
        float fh = ImGui.GetFrameHeight();
        Text(dl, UiFonts.Sans, new Vector2(x, y + (RowH - UiFonts.Sans.FontSize) / 2), T.Muted, "Mounts");
        ImGui.SetCursorScreenPos(new Vector2(x + P(60), y + (RowH - fh) / 2));
        ImGui.PushID(8100);
        Spinner(sp.TtMounts, P(30), extras: false);
        ImGui.PopID();
        y += RowH + P(2);
        Text(dl, UiFonts.Sans, new Vector2(x, y + (RowH - UiFonts.Sans.FontSize) / 2), T.Muted, "Tubes");
        ImGui.SetCursorScreenPos(new Vector2(x + P(60), y + (RowH - fh) / 2));
        bool any = (d.Torpedoes?.Mounts ?? 0) > 0;
        ImGui.BeginDisabled(!any);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 0));
        int top = Math.Max(5, sp.TtTubes);
        for (int g = 1; g <= top; g++)
        {
            if (g > 5 && g != sp.TtTubes)
                continue;
            if (g > 1)
                ImGui.SameLine();
            ImGui.PushID(8200 + g);
            if (Seg(g.ToString(CultureInfo.InvariantCulture), sp.TtTubes == g, P(19)) && sp.TtTubes != g)
                Edit(Knobs.TorpedoTubes.Apply(d, g), $"Torpedoes: {g} tubes per mount");
            ImGui.PopID();
        }
        ImGui.SameLine(0, P(2));
        ImGui.PushID(8250);
        if (ImGui.ArrowButton("##more", ImGuiDir.Right))
            Edit(Knobs.TorpedoTubes.Apply(d, Math.Min(10, sp.TtTubes + 1)), $"Torpedoes: {Math.Min(10, sp.TtTubes + 1)} tubes per mount");
        ImGui.PopID();
        ImGui.PopStyleVar();
        ImGui.EndDisabled();
        Text(dl, UiFonts.SansSmall, new Vector2(x + P(60) + 6 * P(20) + P(26), y + (RowH - UiFonts.SansSmall.FontSize) / 2), T.Muted, "per mount");
        y += RowH + P(8);
        KeyValue(dl, x, ref y, "Placed", sp.TtLines[0]);
        KeyValue(dl, x, ref y, "Broadside", sp.TtLines[1]);
        KeyValue(dl, x, ref y, "Weight", sp.TtLines[2]);
    }

    void SecInspector(ImDrawListPtr dl, Vector2 pos, Vector2 size, SecPane sp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Panel2));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "SELECTED", secSel != null ? "mount pair" : "battery · click a mount for a pair");
        float x = pos.X + P(8), y = pos.Y + P(22);
        if (sp.Rows.Length == 0)
        {
            Text(dl, UiFonts.SansSmall, new Vector2(x, y), T.Muted, "No secondary battery: + Battery, or a track's + in the strip.");
            return;
        }
        var d = doc.Current;
        var r = sp.Rows[secActive];
        var b = d.Secondary![secActive];
        string name = secSel is { } s ? $"{SecName(s.K)}{s.N}S / P" : SecName(r.K);
        string title = $"{name} · {(r.Barrels > 1 ? r.Barrels + " × " : "")}{r.Cal.Value}/{r.Len.Plain}, {SA.Text(r.Pos)}";
        float sw = P(9);
        dl.AddRectFilled(new Vector2(x, y + P(4)), new Vector2(x + sw, y + P(4) + sw), T.U32(SecColour(r.K)));
        dl.PushClipRect(pos, pos + size, true);
        Text(dl, UiFonts.SansBold, new Vector2(x + sw + P(6), y), T.Bright, title);
        y += P(20);
        var ms = sp.Mounts.Where(m => m.K == r.K).ToList();
        string span = ms.Count > 0 ? $"x {ms.Min(m => m.X):0} … {ms.Max(m => m.X):0} m" : "not placed";
        string kind = SA.Casemate(r.Pos) ? "casemate" : (b.CalibreMm ?? 0) >= 150 ? "turret on a barbette" : (b.CalibreMm ?? 0) >= 76 ? "shielded mount" : "open mount";
        KeyValue(dl, x, ref y, "Mount", $"{kind} · shields {r.Shields.Value} {r.Material.Labels[r.Material.Index]}");
        KeyValue(dl, x, ref y, "Place", sp.Built ? $"{r.Placed} of {r.Wanted} {(b.Count != null ? "mounts" : "per side")} · {span}" : "after the build");
        KeyValue(dl, x, ref y, "Height", $"gun axis {r.Axis} above the full-load waterline");
        KeyValue(dl, x, ref y, "Weight", $"{r.Tonnes} t: mounts, magazines ({r.Rounds.Value} rounds a gun), barbettes");
        KeyValue(dl, x, ref y, "Directed by", sp.SecDirs + ", shared by every secondary");
        dl.PopClipRect();
        ImGui.SetCursorScreenPos(new Vector2(x, pos.Y + size.Y - P(6) - ImGui.GetFrameHeight()));
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.BeginDisabled(!sp.TakesList || r.Wanted < 1);
        if (ImGui.SmallButton("Split a pair off"))
        {
            Edit(SA.SplitPair(d, r.K), $"A pair of {SecName(r.K)} split off as battery {SecName(r.K + 1)}");
            secActive = r.K + 1;
            secSel = null;
        }
        ImGui.EndDisabled();
        ImGui.SameLine(0, P(4));
        ImGui.BeginDisabled(!sp.TakesList);
        if (ImGui.SmallButton("Duplicate"))
        {
            Edit(SA.Duplicate(d, r.K), $"Battery {SecName(r.K)} duplicated");
            secActive = r.K + 1;
            secSel = null;
        }
        ImGui.EndDisabled();
        ImGui.SameLine(0, P(4));
        ImGui.BeginDisabled(r.Wanted < 1);
        if (ImGui.SmallButton("Remove a pair (Del)"))
            RemoveSecPair(secSel ?? (r.K, 0));
        ImGui.EndDisabled();
        ImGui.PopFont();
    }

    /// <summary>A tooltip at a place rather than at the mouse (the dev hook's forced tooltips).</summary>
    static void TipAt(string text, Vector2 at)
    {
        ImGui.SetNextWindowPos(at);
        Tip(text);
    }

    /// <summary>The dev hook (-designer-ui=): UI state the screenshots need without a mouse. sec:K selects battery K,
    /// secsel:K:N pair N of it, secmenu opens its context menu, secrose:all adds the main battery to the rose,
    /// sectip:lane:P or sectip:short:K draws that tooltip.</summary>
    void ApplyDevUi(IEnumerable<string> items)
    {
        foreach (var it in items)
        {
            var p = it.Split(':');
            int Int(int i) => p.Length > i && int.TryParse(p[i], out int v) ? v : 0;
            switch (p[0])
            {
                case "sec":
                    secActive = Int(1);
                    break;
                case "secsel":
                    (secActive, secSel) = (Int(1), (Int(1), Int(2)));
                    break;
                case "secmenu" when secSel is { } s:
                    (secMenuFor, openSecMenu) = (s, true);
                    break;
                case "secrose":
                    secRoseAll = p.Length > 1 && p[1] == "all";
                    break;
                case "sectip" when p.Length == 3:
                    secForceTip = $"{p[1]}:{p[2]}";
                    break;
                default:
                    Console.Error.WriteLine($"designer: no UI state '{it}'");
                    break;
            }
        }
        stale = true;
    }
}
