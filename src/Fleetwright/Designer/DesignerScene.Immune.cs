using System.Numerics;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Designer;

/// <summary>The Protection tab's Immune zone (docs/designer-ui/immune-zone.md, the mockup immune.html): the right column's
/// second tab. Against her own main battery at a calibre (her own shells at it): the plates picked on the left (a click
/// one, ctrl+click several, as on the battle map; the main belt and her thickest deck by default) tested at their
/// centres, or her magazines or machinery; a half-polar chart by range and target angle, blended from holds to beaten; a
/// headline; and what her own hits do at the range picked on the chart, with their dots on the elevation. The runs are
/// <see cref="ImmuneWorker"/>'s.</summary>
public sealed unsafe partial class DesignerScene
{
    ImmuneWorker? immune;
    bool protImmune;   // the right column shows the Immune zone
    readonly List<PRef> immuneSel = [];   // the plates picked (empty: the default)
    List<PRef> immuneNow = [];   // this frame's: the picked, or the default
    int immuneView;   // 0 the plates, 1 the magazines, 2 the machinery
    double immuneCal = double.NaN;   // NaN: her own
    double immuneKm = 15;

    /// <summary>The calibres the stepper walks (and her own), mm.</summary>
    static readonly double[] ImmuneCalibres = [76, 102, 120, 127, 140, 152, 203, 234, 254, 280, 305, 343, 356, 381, 406, 460];

    /// <summary>The plate a row or elevation plate is tested as; null for what isn't a plate a shell beats (the torpedo
    /// protection, the boxes, the steering gear's).</summary>
    static PlatePick? PickOf(PRef r, Design d) => r.Kind switch
    {
        PK.Belt => new(PlatePart.Belt),
        PK.BeltZone => new(PlatePart.BeltZone),
        PK.Upper => new(PlatePart.UpperBelt),
        PK.EndFore => new(PlatePart.EndFore),
        PK.EndAft => new(PlatePart.EndAft),
        PK.Bulkheads => new(PlatePart.Bulkheads),
        PK.Deck => d.Armour?.Decks is { } ds && r.I < ds.Count ? new(PlatePart.Deck, ds[r.I].Deck ?? 0) : null,
        PK.Slope => new(PlatePart.Slopes),
        PK.Main => new(PlatePart.Turrets),
        PK.Barbettes => new(PlatePart.Barbettes),
        PK.Ct => new(PlatePart.ConningTower),
        _ => null,
    };

    /// <summary>A plate clicked on the left (a row or the elevation): with the Immune zone open, it's tested; ctrl+click adds
    /// or drops it from those tested together.</summary>
    void ImmunePick(PRef r)
    {
        if (!protImmune || PickOf(r, doc.Current) == null)
            return;
        if (ImGui.GetIO().KeyCtrl)
        {
            if (immuneSel.Count == 0)
                immuneSel.AddRange(immuneNow);
            if (immuneSel.Contains(r))
            {
                if (immuneSel.Count > 1)
                    immuneSel.Remove(r);
            }
            else
                immuneSel.Add(r);
        }
        else
        {
            immuneSel.Clear();
            immuneSel.Add(r);
        }
        immuneView = 0;
    }

    /// <summary>The plates picked this frame: those clicked (still on the ship), or the main belt and her thickest deck.</summary>
    List<PRef> ImmuneSelection(ProtPane pp)
    {
        var d = doc.Current;
        var sel = immuneSel.Where(r => ProtRowOf(pp, r) != null && PickOf(r, d) != null).ToList();
        if (sel.Count > 0)
            return sel;
        if (ProtRowOf(pp, new PRef(PK.Belt)) != null)
            sel.Add(new PRef(PK.Belt));
        if (d.Armour?.Decks is { Count: > 0 } ds)
        {
            int best = 0;
            for (int i = 1; i < ds.Count; i++)
                if ((ds[i].Mm ?? 0) > (ds[best].Mm ?? 0))
                    best = i;
            if (ProtRowOf(pp, new PRef(PK.Deck, best)) != null)
                sel.Add(new PRef(PK.Deck, best));
        }
        if (sel.Count == 0 && pp.Rows.FirstOrDefault(r => PickOf(r.Ref, d) != null) is { } first)
            sel.Add(first.Ref);
        return sel;
    }

    /// <summary>The right column's two tabs: Sections · plate, Immune zone.</summary>
    void ImmuneTabs(ImDrawListPtr dl, Vector2 pos, float w)
    {
        float h = P(20);
        dl.AddRectFilled(pos, pos + new Vector2(w, h), T.U32(T.HeaderBg));
        dl.AddLine(pos + new Vector2(0, h - 1), pos + new Vector2(w, h - 1), T.U32(T.Line));
        float x = pos.X;
        foreach (var (key, label, on) in new[] { ("S", "SECTIONS · PLATE", !protImmune), ("I", "IMMUNE ZONE", protImmune) })
        {
            var font = UiFonts.Head;
            float tw = Width(font, label) + Width(UiFonts.MonoSmall, key) + P(20);
            var a = new Vector2(x, pos.Y);
            ImGui.SetCursorScreenPos(a);
            if (ImGui.InvisibleButton("##ztab" + key, new Vector2(tw, h)))
                protImmune = key == "I";
            bool hover = ImGui.IsItemHovered();
            if (on)
            {
                dl.AddRectFilled(a, a + new Vector2(tw, h), T.U32(T.BrassDarker));
                dl.AddRectFilled(a + new Vector2(0, h - P(2)), a + new Vector2(tw, h), T.U32(T.Brass));
            }
            else if (hover)
                dl.AddRectFilled(a, a + new Vector2(tw, h), T.U32(T.TabHover));
            var col = on ? T.Brass : T.Muted;
            float kx = a.X + P(6);
            dl.AddRect(new Vector2(kx, a.Y + P(4)), new Vector2(kx + Width(UiFonts.MonoSmall, key) + P(4), a.Y + h - P(5)), T.U32(col));
            dl.Text(UiFonts.MonoSmall, new Vector2(kx + P(2), Mid(UiFonts.MonoSmall, a.Y, h - P(1))), col, key);
            dl.Text(font, new Vector2(kx + Width(UiFonts.MonoSmall, key) + P(9), Mid(font, a.Y, h - P(1))), col, label);
            x += tw;
        }
    }

    /// <summary>The Immune zone: what it's against and what's looked at, the headline, the chart, and the range picked.</summary>
    void ImmunePane(ImDrawListPtr dl, Vector2 pos, Vector2 size, ProtPane pp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Panel2));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        var font = UiFonts.SansSmall;
        var ship = shown?.Ship;
        var design = shown?.Design;
        double own = design?.Main is { Count: > 0 } mains ? mains[0].CalibreMm ?? 0 : 0;
        if (ship == null || design == null || own <= 0)
        {
            dl.Text(UiFonts.Sans, pos + new Vector2(P(10), P(10)), T.Muted, ship == null ? "Building…" : "She has no main battery to test her armour against.");
            return;
        }
        double cal = double.IsNaN(immuneCal) ? own : immuneCal;
        var d = doc.Current;
        var picks = immuneNow.Select(r => PickOf(r, d)).OfType<PlatePick>().Distinct().ToArray();
        immune ??= new ImmuneWorker();
        immune.Want(new ImmuneWorker.Ask(ship, design, cal, picks, immuneView > 0, immuneKm * 1000));
        var res = immune.Now;
        bool ready = res != null && ReferenceEquals(res.Ship, ship) && res.CalibreMm == cal && res.Class != null;

        // the bar: against her own (a calibre), and what's looked at
        float bh = P(22), x = pos.X + P(8);
        dl.AddRectFilled(pos, pos + new Vector2(size.X, bh), T.U32(T.HeaderBg));
        dl.Text(UiFonts.Head, new Vector2(x, Mid(UiFonts.Head, pos.Y, bh)), T.Text, "AGAINST HER OWN");
        x += Width(UiFonts.Head, "AGAINST HER OWN") + P(8);
        var cals = ImmuneCalibres.Append(own).Distinct().Order().ToArray();
        int ci = Array.IndexOf(cals, cal);
        x += dl.Stepper("##izcal", new Vector2(x, pos.Y + P(3)), Fmt(cal, Quantity.Calibre), P(58), ci > 0, ci < cals.Length - 1, out int by) + P(8);
        if (by != 0)
            immuneCal = cals[Math.Clamp(ci + Math.Sign(by), 0, cals.Length - 1)];
        string label = string.Join(" + ", immuneNow.Select(r => ProtRowOf(pp, r)?.Full ?? ""));
        string[] views = [label.Length > 28 ? $"{immuneNow.Count} plates" : label, "Magazines", "Machinery"];
        float sw = SegmentedWidth(views);
        int hit = dl.Segmented("##izview", new Vector2(pos.X + size.X - P(8) - sw, pos.Y + P(3)), views, immuneView, out _);
        if (hit >= 0)
            immuneView = hit;
        if (immune.Busy)
            dl.Busy(new Vector2(pos.X + size.X - P(18) - sw, pos.Y + bh / 2), P(5));
        if (!ready)
        {
            dl.Text(UiFonts.Sans, pos + new Vector2(P(10), bh + P(10)), T.Muted, res?.Error is { } e && ReferenceEquals(res.Ship, ship) ? e : "Firing her shells…");
            return;
        }

        // the zones looked at, and the headline
        var zones = new List<(ImmuneZone Z, ZoneTarget T, bool Lies, PRef R)>();
        if (immuneView == 0)
        {
            foreach (var r in immuneNow)
                if (PickOf(r, d) is { } pk && res!.Plates.TryGetValue(pk, out var z))
                    zones.Add((z, ZoneTarget.Plate, pk.Lies, r));
        }
        else if (res!.Vitals is { } vz)
            zones.Add((vz, immuneView == 1 ? ZoneTarget.Magazines : ZoneTarget.Machinery, false, default));
        float y = pos.Y + bh + P(6);
        if (zones.Count == 0)
        {
            dl.Text(UiFonts.Sans, new Vector2(pos.X + P(10), y), T.Muted, "Firing her shells…");
            return;
        }
        var z0 = zones[0].Z;
        int beam = Array.IndexOf(ImmuneZone.Angles, 90.0);
        var (head, why) = ImmuneHeadline(pp, res!, zones, beam);
        dl.Text(UiFonts.Title, new Vector2(pos.X + P(10), y), T.Bright, head);
        y += UiFonts.Title.FontSize + P(3);
        y += dl.Wrapped(font, new Vector2(pos.X + P(10), y), T.Muted, why, size.X - P(20)) + P(4);

        // the chart, the key (or the cell under the mouse) and the range picked
        float angerH = P(54), keyH = P(18);
        float chartH = pos.Y + size.Y - angerH - keyH - y - P(4);
        float rad = MathF.Max(P(40), MathF.Min(chartH / 2 - P(12), size.X - P(120)));
        var c = new Vector2(pos.X + (size.X - rad) / 2, y + chartH / 2);
        string? hover = ImmuneChart(dl, c, rad, z0, zones, cal);
        float ky = c.Y + rad + P(14);
        if (hover != null)
            dl.Text(font, new Vector2(pos.X + (size.X - Width(font, hover)) / 2, ky), T.Bright, hover);
        else
            ImmuneKey(dl, new Vector2(pos.X + size.X / 2, ky), zones);
        ImmuneAnger(dl, new Vector2(pos.X, pos.Y + size.Y - angerH), new Vector2(size.X, angerH), res!);
    }

    /// <summary>The headline and the line under it: a plate alone, the plates together (their zone, then each in a
    /// sentence), or the vitals (their zone, and what lets shells in at its edges).</summary>
    (string Head, string Why) ImmuneHeadline(ProtPane pp, ImmuneWorker.Results res, List<(ImmuneZone Z, ZoneTarget T, bool Lies, PRef R)> zones, int beam)
    {
        var z0 = zones[0].Z;
        double reach = z0.ReachM;
        static string Km(double m) => $"{m / 1000:0}";
        static string Zone(double inner, double outer, double reach) => inner <= 0 && outer >= reach ? "Immune at every range on the beam"
            : outer - inner < 1000 ? "Not immune on the beam"   // under a km is no zone
            : inner <= 0 ? $"Immune out to {Km(outer)} km on the beam" : outer >= reach ? $"Immune beyond {Km(inner)} km on the beam"
            : $"Immune from {Km(inner)} to {Km(outer)} km on the beam";
        if (immuneView > 0)
        {
            var (z, t, _, _) = zones[0];
            var (inner, outer) = z.Edges(t, beam);
            int Idx(double m) => Math.Clamp((int)Math.Round(m / ImmuneZone.RangeStepM) - 1, 0, z.RangesM.Length - 1);
            var why = new List<string>();
            if (inner > 0)
                why.Add($"Inside {Km(inner)} km shells get in through the {ZoneName(z.Most(t, Idx(inner), beam, false)) ?? "side"}.");
            if (outer < reach)
                why.Add($"Beyond {Km(outer)} km they come down through the {ZoneName(z.Most(t, Idx(outer), beam, true)) ?? "deck"}.");
            if (why.Count == 0)
                why.Add($"Her own {Fmt(res.CalibreMm, Quantity.Calibre)} can't get into her {(t == ZoneTarget.Magazines ? "magazines" : "machinery")} at any range.");
            return (Zone(inner, outer, reach), string.Join(" ", why));
        }
        // each plate in a sentence: a side plate holds beyond a range, a deck gives beyond one
        string Sentence((ImmuneZone Z, ZoneTarget T, bool Lies, PRef R) p)
        {
            var row = ProtRowOf(pp, p.R);
            string name = row != null ? $"{row.Full} ({row.ThickShown})" : "The plate";
            bool many = row?.Full.EndsWith('s') == true;
            var (inner, outer) = p.Z.Edges(ZoneTarget.Plate, beam);
            return p.Lies
                ? outer >= reach ? $"{name} {(many ? "hold" : "holds")} at every range" : $"{name} {(many ? "give" : "gives")} beyond {Km(outer)} km"
                : inner <= 0 ? $"{name} {(many ? "hold" : "holds")} at every range"
                : inner >= reach - 1000 ? $"{name} {(many ? "are" : "is")} beaten at every range"
                : $"{name} {(many ? "hold" : "holds")} beyond {Km(inner)} km";
        }
        if (zones.Count == 1)
        {
            string zone = "being fired";
            if (res.Armour is { } az)
            {
                var (zi, zo) = az.ArmourEdges(beam);
                zone = zi <= 0 && zo >= reach ? "at every range" : zo - zi >= 1000 ? $"from {Km(zi)} to {Km(zo)} km" : "none";
            }
            return (Sentence(zones[0]) + " on the beam",
                $"Her immune zone on the beam (belt, barbettes, deck): {zone}. Click a plate on the left to test it; ctrl+click to test several together.");
        }
        // together: from where every side plate holds to where the first deck gives
        double ji = 0, jo = reach;
        foreach (var p in zones)
        {
            var (i, o) = p.Z.Edges(ZoneTarget.Plate, beam);
            if (p.Lies)
                jo = Math.Min(jo, o);
            else
                ji = Math.Max(ji, i);
        }
        return (Zone(ji, jo, reach), string.Join(". ", zones.Select(Sentence)) + ". Ctrl+click a plate to add or drop it.");
    }

    /// <summary>A plate by the designer's name for the vitals' line: "belt 320 mm", "third deck 80 mm".</summary>
    string? ZoneName(ZonePlate? p)
    {
        if (p is not { } z || shown?.Ship == null || immune?.Now?.Class is not { } c)
            return null;
        return z.Kind switch
        {
            Sim.Ships.PlateKind.SideArmour => $"{c.SidePlates[z.Index].Kind.ToString().ToLowerInvariant()} {z.Mm:0} mm",
            Sim.Ships.PlateKind.ArmourDeck => $"{Decks.DeckNameLower(c.Decks[c.ArmourDecks[z.Index].Deck].Number)} {z.Mm:0} mm",
            Sim.Ships.PlateKind.Inclined => $"{c.Inclined[z.Index].Kind.ToString().ToLowerInvariant()} {z.Mm:0} mm",
            Sim.Ships.PlateKind.Barbette => $"barbette {z.Mm:0} mm",
            _ => $"{z.Kind.ToString().ToLowerInvariant()} {z.Mm:0} mm",
        };
    }

    /// <summary>The share a cell's colour goes by, softened with its neighbours (half its own, the rest from the next ranges
    /// and angles) so a few shells' chance doesn't mottle the chart; null where no shell met the target.</summary>
    static double Soft(ImmuneZone z, ZoneTarget t, int r, int a)
    {
        double sum = 0, w = 0;
        foreach (var (dr, da, k) in (ReadOnlySpan<(int, int, double)>)[(0, 0, 4), (-1, 0, 1), (1, 0, 1), (0, -1, 1), (0, 1, 1)])
        {
            int rr = r + dr, aa = a + da;
            if (rr < 0 || rr >= z.RangesM.Length || aa < 0 || aa >= ImmuneZone.Angles.Length)
                continue;
            ref readonly var c = ref z[t, rr, aa];
            if (c.Shots == 0)
                continue;
            sum += k * c.InShare;
            w += k;
        }
        return w > 0 ? sum / w : 0;
    }

    static Vector4 Mix(Vector4 a, Vector4 b, double t) => Vector4.Lerp(a, b, (float)Math.Clamp(t, 0, 1));

    /// <summary>The chart: her plan at the centre, bow up, the target angle round it and the range out from it; each cell
    /// blended from holds (green) to beaten, red where a side plate gives, amber where a deck does (beaten where any plate
    /// picked is). Rings every 10 km, the range picked dashed. A click picks a range; the cell under the mouse in words.</summary>
    string? ImmuneChart(ImDrawListPtr dl, Vector2 c, float rad, ImmuneZone z0, List<(ImmuneZone Z, ZoneTarget T, bool Lies, PRef R)> zones, double cal)
    {
        int n = z0.RangesM.Length, na = ImmuneZone.Angles.Length;
        double reach = z0.ReachM;
        float Rad(double m) => (float)(m / reach) * rad;
        Vector2 At(double deg, float r) => c + new Vector2(MathF.Sin((float)(deg * Math.PI / 180)) * r, -MathF.Cos((float)(deg * Math.PI / 180)) * r);
        for (int a = 0; a < na; a++)
        {
            double a0 = Math.Max(0, ImmuneZone.Angles[a] - 7.5), a1 = Math.Min(180, ImmuneZone.Angles[a] + 7.5);
            for (int r = 0; r < n; r++)
            {
                double side = 0, deck = 0;
                foreach (var p in zones)
                {
                    double s = Soft(p.Z, p.T, r, a);
                    if (p.Lies)
                        deck = Math.Max(deck, s);
                    else if (p.T == ZoneTarget.Plate)
                        side = Math.Max(side, s);
                    else
                    {
                        // the vitals: red in through the side, amber through the deck
                        ref readonly var cc = ref p.Z[p.T, r, a];
                        (side, deck) = cc.Side >= cc.Deck ? (s, 0.0) : (0.0, s);
                    }
                }
                var col = Mix(T.ZoneHolds, side >= deck ? T.Hostile : T.Amber, (Math.Max(side, deck) - ImmuneZone.Unsure) / (1 - 2 * ImmuneZone.Unsure));
                float r0 = Rad(z0.RangesM[r] - ImmuneZone.RangeStepM), r1 = Rad(z0.RangesM[r]);
                uint u = T.U32(col);
                for (int k = 0; k < 3; k++)
                {
                    double b0 = a0 + (a1 - a0) * k / 3, b1 = a0 + (a1 - a0) * (k + 1) / 3;
                    dl.AddQuadFilled(At(b0, r0), At(b0, r1), At(b1, r1), At(b1, r0), u);
                }
            }
        }
        // rings every 10 km, labelled on the beam; the range picked dashed
        var font = UiFonts.MonoSmall;
        for (double m = 10000; m < reach; m += 10000)
        {
            dl.PathArcTo(c, Rad(m), -MathF.PI / 2, MathF.PI / 2, 48);
            dl.PathStroke(T.U32(T.Edge with { W = 0.6f }), ImDrawFlags.None, 1);
            string s = $"{m / 1000:0}";
            dl.Text(font, new Vector2(c.X + Rad(m) - Width(font, s) - P(2), c.Y + P(2)), T.Bright, s);
        }
        float rk = Rad(immuneKm * 1000);
        for (int k = 0; k < 36; k += 2)
        {
            dl.PathArcTo(c, rk, -MathF.PI / 2 + MathF.PI * k / 36, -MathF.PI / 2 + MathF.PI * (k + 1) / 36, 4);
            dl.PathStroke(T.U32(T.Bright), ImDrawFlags.None, P(1.5f));
        }
        dl.Text(font, new Vector2(c.X + rad + P(4), c.Y - font.FontSize / 2), T.Muted, "km");
        dl.Text(font, new Vector2(c.X - Width(font, "bow") / 2, c.Y - rad - P(14)), T.Muted, "bow");
        dl.Text(font, new Vector2(c.X - Width(font, "stern") / 2, c.Y + rad + P(1)), T.Muted, "stern");
        // her plan, bow up
        Vector2[] hull = [c + new Vector2(0, -P(16)), c + new Vector2(P(4), -P(4)), c + new Vector2(P(4), P(14)), c + new Vector2(-P(4), P(14)), c + new Vector2(-P(4), -P(4))];
        fixed (Vector2* hp = hull)
        {
            dl.AddConvexPolyFilled(ref hp[0], hull.Length, T.U32(T.DrawingMid));
            dl.AddPolyline(ref hp[0], hull.Length, T.U32(T.PlanLine), ImDrawFlags.Closed, 1);
        }
        // the mouse: a cell in words, a click picks its range
        ImGui.SetCursorScreenPos(c - new Vector2(0, rad));
        ImGui.InvisibleButton("##izchart", new Vector2(rad, 2 * rad));
        if (!ImGui.IsItemHovered())
            return null;
        var mv = ImGui.GetIO().MousePos - c;
        float dist = mv.Length();
        if (mv.X < 0 || dist > rad)
            return null;
        if (ImGui.IsItemClicked())
            immuneKm = Math.Max(1, Math.Round(dist / rad * reach / 1000));
        int ai = Math.Clamp((int)Math.Round(Math.Atan2(mv.X, -mv.Y) * 180 / Math.PI / 15), 0, na - 1);
        int ri = Math.Clamp((int)Math.Ceiling(dist / rad * reach / ImmuneZone.RangeStepM) - 1, 0, n - 1);
        {
            double a0 = Math.Max(0, ImmuneZone.Angles[ai] - 7.5), a1 = Math.Min(180, ImmuneZone.Angles[ai] + 7.5);
            float r0 = Rad(z0.RangesM[ri] - ImmuneZone.RangeStepM), r1 = Rad(z0.RangesM[ri]);
            dl.AddQuad(At(a0, r0), At(a0, r1), At(a1, r1), At(a1, r0), T.U32(T.Bright), P(1.5f));
        }
        // words, not percentages: a few shells in a hundred is no reason to change a design
        static string Held(double x) => x < 0.1 ? "holds" : x < 0.5 ? "mostly holds" : x < 0.9 ? "mostly beaten" : "beaten";
        static string Gets(double x) => x < 0.1 ? "safe" : x < 0.5 ? "a few get in" : x < 0.9 ? "most get in" : "all get in";
        string at = $"{z0.RangesM[ri] / 1000:0} km, {ImmuneZone.Angles[ai]:0}° off the bow: ";
        if (immuneView > 0)
        {
            ref readonly var cc = ref zones[0].Z[zones[0].T, ri, ai];
            return at + (cc.Shots > 0 ? Gets(cc.InShare) : "out of reach");
        }
        return at + string.Join(", ", zones.Select(p =>
        {
            ref readonly var cc = ref p.Z[ZoneTarget.Plate, ri, ai];
            string name = (ProtRowOf(snap!.Prot!, p.R)?.Full ?? "plate").ToLowerInvariant();
            return cc.Shots > 0 ? $"{name} {Held(cc.ThroughShare)}" : $"{name} not hit from there";
        }));
    }

    /// <summary>The key: a gradient from holds to beaten for each kind of plate picked (or the vitals' two).</summary>
    void ImmuneKey(ImDrawListPtr dl, Vector2 mid, List<(ImmuneZone Z, ZoneTarget T, bool Lies, PRef R)> zones)
    {
        var font = UiFonts.SansSmall;
        var parts = new List<(string Label, Vector4 To)>();
        if (immuneView > 0)
            parts.AddRange([("in through the side", T.Hostile), ("in through the deck", T.Amber)]);
        else
        {
            if (zones.Any(p => !p.Lies))
                parts.Add((zones.Any(p => p.Lies) ? "side plate beaten" : "beaten", T.Hostile));
            if (zones.Any(p => p.Lies))
                parts.Add(("deck beaten", T.Amber));
        }
        string first = immuneView > 0 ? "safe" : "holds";
        float bw = P(70), total = Width(font, first) + P(6) + parts.Sum(p => bw + P(6) + Width(font, p.Label) + P(12));
        float x = mid.X - total / 2;
        dl.Text(font, new Vector2(x, mid.Y), T.Muted, first);
        x += Width(font, first) + P(6);
        foreach (var (lbl, to) in parts)
        {
            var a = new Vector2(x, mid.Y + P(3));
            uint g0 = T.U32(T.ZoneHolds), g1 = T.U32(to);
            dl.AddRectFilledMultiColor(a, a + new Vector2(bw, P(9)), g0, g1, g1, g0);
            x += bw + P(6);
            dl.Text(font, new Vector2(x, mid.Y), T.Muted, lbl);
            x += Width(font, lbl) + P(12);
        }
    }

    /// <summary>Her own hits at the range picked on the chart, in a sentence (<see cref="ImmuneWorker.AngerShips"/> ships
    /// of <see cref="ImmuneWorker.AngerHits"/> hits each, settled half an hour).</summary>
    void ImmuneAnger(ImDrawListPtr dl, Vector2 pos, Vector2 size, ImmuneWorker.Results res)
    {
        dl.AddLine(pos, pos + new Vector2(size.X, 0), T.U32(T.Line));
        float x = pos.X + P(10), y = pos.Y + P(5);
        var run = res.Anger;
        int b = res.AngerDone;
        var band = run != null && b >= 0 ? run.Bands[b] : null;
        string title = band != null ? $"{ImmuneWorker.AngerHits} HITS AT {band.FromM / 1000:0}-{band.ToM / 1000:0} KM" : $"{ImmuneWorker.AngerHits} HITS AT {immuneKm:0} KM";
        dl.Text(UiFonts.Head, new Vector2(x, y), T.Brass, title);
        dl.Text(UiFonts.SansSmall, new Vector2(x + Width(UiFonts.Head, title) + P(10), y + P(1)), T.Muted, "click the chart for another range");
        y += P(17);
        if (band == null)
        {
            dl.Text(UiFonts.SansSmall, new Vector2(x, y), T.Muted, "Firing at her…");
            return;
        }
        var r = band.Report;
        int ships = Math.Max(r.Ships, 1);
        int mounts = (int)Math.Round(r.MainMounts / ships), working = (int)Math.Round(r.MainWorking / ships);
        string[] how = ["magazine", "flooding", "capsizing", "breaking in two"];
        int worst = 1;
        for (int k = 2; k < r.LostBy.Length; k++)
            if (r.LostBy[k] > r.LostBy[worst])
                worst = k;
        string lost = r.Lost == 0 ? $"All {r.Ships} ships stay afloat." : $"{r.Lost} of {r.Ships} ships are lost (most to {how[Math.Clamp(worst - 1, 0, 3)]}).";
        string rest = $" On average she takes {Math.Round(r.WaterT / ships / 100) * 100:N0} t of water and keeps {100 * r.SpeedShare / ships:0} % of her speed" +
                      (mounts > 0 ? $" and {working} of {mounts} turrets firing." : ".");
        dl.Wrapped(UiFonts.SansSmall, new Vector2(x, y), T.Bright, lost + rest, size.X - P(20));
    }

    /// <summary>Where her own hits at the range picked struck, on the elevation: burst inside her (red) or held or passed
    /// (grey); over her main deck faint (the elevation draws only the turrets there).</summary>
    void ImmuneDots(ImDrawListPtr dl, ElevMap m, Vector2? head = null)
    {
        if (!protImmune || immune?.Now is not { Anger: { } run, AngerDone: >= 0 } res || !ReferenceEquals(res.Ship, shown?.Ship))
            return;
        var band = run.Bands[res.AngerDone];
        if (head == null)
        {
            foreach (var dot in band.Dots)
            {
                var col = dot.Class == 0 ? T.Hostile : T.PlanLine;
                dl.AddCircleFilled(new Vector2(m.X((float)dot.Point.X), m.Z((float)dot.Point.Z)), P(2.2f), T.U32(col with { W = dot.Point.Z > 0.5 ? 0.3f : 0.85f }));
            }
            return;
        }
        // the key, in the elevation's head strip
        var font = UiFonts.SansSmall;
        string t = $"where {ImmuneWorker.AngerHits} hits at {band.FromM / 1000:0}-{band.ToM / 1000:0} km struck:";
        float x = head.Value.X;
        dl.Text(font, new Vector2(x, head.Value.Y), T.Bright, t);
        x += Width(font, t) + P(10);
        foreach (var (lbl, col) in new[] { ("burst inside", T.Hostile), ("held or passed", T.PlanLine) })
        {
            dl.AddCircleFilled(new Vector2(x, head.Value.Y + font.FontSize / 2), P(3), T.U32(col));
            dl.Text(font, new Vector2(x + P(6), head.Value.Y), T.Muted, lbl);
            x += P(6) + Width(font, lbl) + P(14);
        }
    }
}
