using System.Globalization;
using System.Numerics;
using Fleetwright.Shipgen;
using ImGuiNET;
using Part = Fleetwright.Designer.ProtectionEdits.Part;
using PE = Fleetwright.Designer.ProtectionEdits;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Designer;

/// <summary>
/// The Protection tab, after docs/designer-ui/protection.md: the elevation (the ship's side deck by deck, heights
/// stretched, every plate where the build put it, + ghosts where a missing part would go), the midship section to scale,
/// the plate table (only the parts the ship has; a + in each group's header adds a missing one, × removes), the selected
/// plate, and the coverage presets. Drags preview on screen and edit once, on release: a build is expensive.
/// </summary>
public sealed unsafe partial class DesignerScene
{
    /// <summary>What a plate, row or ghost is: a part, an armour deck (I: its index in the design), a battery's turrets
    /// (I: the battery), the barbettes, the conning tower, the flight deck.</summary>
    enum PK { Belt, Upper, EndFore, EndAft, Bulkheads, Tds, Deck, Steering, Main, Barbettes, Ct, FlightDeck }

    readonly record struct PRef(PK Kind, int I = 0);

    enum DragKind { None, BeltTop, BeltBottom, Reach, UpperTop, DeckMove, TdsDepth }

    PRef protSel = new(PK.Belt);
    PRef? protHot;   // hovered this frame (row or plate): both drawings and the table light it
    PRef? protHotNext;
    PRef protMenuFor;
    bool openProtMenu;
    DragKind protDrag;
    int protDragI;   // the deck, or the end belt (0 fore, 1 aft)
    double protDragValue = double.NaN;   // the snapped value under the mouse
    string protDragTip = "";
    double protDragTipFor = double.NaN;
    string? protForceTip;   // dev hook: "cover:N", "ghost:N:EXT", "handle:N"
    readonly List<(Vector2 A, Vector2 B, PRef R)> protHits = [];   // this frame's plates on the elevation, topmost last

    static readonly string[] ExtentValues = ["citadel", "full", "fore", "aft", "ends"];
    static readonly string[] ExtentShort = ["cit", "full", "fore", "aft", "ends"];
    static readonly string[] GroupTitle = ["SIDE", "ARMOUR DECKS", "ENDS", "GUNS AND CONNING TOWER"];

    // ------------------------------------------------------------------ the snapshot's part

    /// <summary>A plate on the elevation, in ship metres (z above the main deck): its rectangle, thickness and label.
    /// Flat: an armour deck, drawn a few pixels thick at Z0; Inside: a barbette's part under the deck, dashed.</summary>
    sealed record PPlate(PRef Ref, float X0, float X1, float Z0, float Z1, double Mm, string Label, bool Flat = false, bool Inside = false,
        bool Bulkhead = false);

    /// <summary>A + where a missing part or a deck's bare stretch would go.</summary>
    sealed record PGhost(PRef Ref, int Deck, string Extent, float X0, float X1, float Z0, float Z1, string Tip)
    {
        /// <summary>The dev hook's name for it (protghost:DECK:EXT).</summary>
        public string Force { get; } = $"ghost:{Deck}:{Extent}";
    }

    sealed record ProtRow(PRef Ref, int Group, string Name, Vector4 Swatch, NumRow? Thick, string ThickText, NumRow? Second, string SecondPre,
        NumRow? WhereA, string WhereAPre, NumRow? WhereB, ChoiceKnob? Extent, string ExtentNow, NumberKnob? DeckKnob, long DeckNow, string DeckLabel,
        string WhereText, ChoiceRow? Material, string MaterialText, string T, float Share, string WeightTip, bool Removable,
        (string Label, string Value)[] Lines, string Note)
    {
        /// <summary>The plate's tooltip on the drawings.</summary>
        public string Hover { get; init; } = "";
    }

    sealed record PChip(Part? Part, int Deck, string Label, string Tip);

    sealed record ProtPane(ProtRow[] Rows, PChip[][] Chips, PPlate[] Plates, PGhost[] Ghosts, (string Short, float Z, float X0, float X1)[] Lanes,
        Vector2[] Profile, float L, float B, float Wl, float Keel, float Ib, float ZTop, float X0, float X1, float BeltTop, float BeltBottom,
        float RoofZ, string Citadel, (long N, string Name, float Z)[] DeckChoices,
        // the midship section: its station, the half-breadths at heights, the plates that cross it
        float SecX, float[] SecZ, float[] SecY, (float Z0, float Z1, double Mm, double Lower, PRef R)[] SecSide,
        (float Z, double Mm, PRef R, string Label)[] SecDecks, float Tds,
        string Reads, PE.Coverage Coverage, string[] CoverTips, PickRow Scheme, string ImmuneGun, NumRow BeltHeight, NumRow BeltDepth, NumRow TdsRow,
        bool Built)
    {
        public string BeltTip { get; init; } = "";
        public string TdsTip { get; init; } = "";
        /// <summary>The end belts' tips and the upper belt's span and top edge, for the handles (NaN: none).</summary>
        public float EndTipFore { get; init; } = float.NaN;
        public float EndTipAft { get; init; } = float.NaN;
        public float UpX0 { get; init; } = float.NaN;
        public float UpX1 { get; init; } = float.NaN;
        public float UpTop { get; init; } = float.NaN;
    }

    static readonly string[] CoverForce = ["cover:0", "cover:1", "cover:2"];

    static string DeckShort(long n, string id) => n switch
    {
        0 => "Main",
        1 => "2nd",
        2 => "3rd",
        < 0 => id,
        _ => $"{n + 1}th",
    };

    ProtPane ProtPaneOf(Design d, Ship? ship)
    {
        var a = d.Armour ?? new ArmourInput();
        var hb = ship?.Hitboxes;
        float L = (float)(hb?.Length ?? 100), B = (float)(hb?.Beam ?? 12);
        float wl = (float)(hb?.Vertical.Waterline ?? -4), keel = (float)(hb?.Vertical.Keel ?? -10);
        var belt = hb?.Armour.Belt;
        var fwd = hb?.Armour.Bulkheads?.FirstOrDefault(b => b.Id == "Forward bulkhead");
        var aft = hb?.Armour.Bulkheads?.FirstOrDefault(b => b.Id == "Aft bulkhead");
        float X0 = (float)(belt?.X0 ?? aft?.X ?? -0.3 * L), X1 = (float)(belt?.X1 ?? fwd?.X ?? 0.3 * L);
        float beltTop = (float)(belt?.Top ?? wl + (a.BeltHeightM ?? 2)), beltBot = (float)(belt?.Bottom ?? wl - (a.BeltDepthM ?? 1.5));
        var decks = hb?.Decks ?? [];
        float ib = (float)(decks.FirstOrDefault(x => x.Kind == "inner_bottom")?.Z ?? keel + 1.1);
        float roofZ = (float)(hb?.Vertical.ArmourDeck ?? wl);
        var raised = hb?.Vertical.Raised ?? [];

        // the decks an armour deck can lie on: the stack (not the inner bottom) and the raised decks, top first
        var choices = new List<(long N, string Name, float Z)>();
        foreach (var r in raised.Select((r, i) => (r, i)))
            choices.Add((-(r.i + 1), r.r.Id, (float)r.r.Top));
        foreach (var x in decks)
            if (x.Deck is { } n && n >= 0 && x.Kind != "inner_bottom" && x.Z > ib + 0.5)
                choices.Add((n, Decks.DeckName(n), (float)x.Z));
        choices.Sort((p, q) => q.Z.CompareTo(p.Z));
        int deepest = (int)choices.Where(c => c.N >= 0).Select(c => c.N).DefaultIfEmpty(1).Max();
        float DeckZ(long n) => choices.FirstOrDefault(c => c.N == n) is { Name: not null } c ? c.Z : -2.6f * n;

        // weights by line
        var w = new Dictionary<string, double>(StringComparer.Ordinal);
        if (ship != null)
            foreach (var x in ship.Report.Weights)
                if (x.Group == "armour")
                    w[x.Name] = w.GetValueOrDefault(x.Name) + x.T;
        double armourT = ship?.Report.WeightGroupsT.GetValueOrDefault("armour") ?? 0;
        double std = ship?.Report.Results.StandardDisplacementT ?? 0;
        double Sum(Func<string, bool> f) => w.Where(kv => f(kv.Key)).Sum(kv => kv.Value);

        // the built plates, each traced to its row
        var mainIds = hb?.Components.Where(c => c.Kind == "main").Select(c => c.Id).ToHashSet() ?? [];
        var designDecks = a.Decks ?? [];
        int DeckRow(string name, string ext)
        {
            static bool Covers(string? e, string rep) => (e ?? "citadel") == rep || e == "ends" && rep is "fore" or "aft" || rep == "full" && e == "full";
            for (int i = 0; i < designDecks.Count; i++)
                if ((designDecks[i].Mm ?? 0) > 0 && Decks.DeckName(designDecks[i].Deck ?? 0) == name && Covers(designDecks[i].Extent, ext))
                    return i;
            for (int i = 0; i < designDecks.Count; i++)
                if ((designDecks[i].Mm ?? 0) > 0 && Covers(designDecks[i].Extent, ext))
                    return i;
            return -1;
        }
        string Mm(double mm) => Fmt(mm, Quantity.Armour, unit: false);
        var plates = new List<PPlate>();
        var deckT = new double[designDecks.Count];
        var sec = new List<(float, float, double, double, PRef)>();
        var secDecks = new List<(float, double, PRef, string)>();
        float secX = (X0 + X1) / 2;
        if (hb != null)
        {
            if (a.TdsM is > 0)
                plates.Add(new PPlate(new PRef(PK.Tds), X0, X1, ib, Math.Min(roofZ, wl), 0, $"TDS {a.TdsM:0.0} m"));
            foreach (var s in hb.Armour.Strakes ?? [])
            {
                var r = s.Kind switch { "upper" => new PRef(PK.Upper), "box" => new PRef(PK.Steering), _ => new PRef(s.Extent == "fore" ? PK.EndFore : PK.EndAft) };
                if (s.Kind == "end" && s.TipMm is { } tip && Math.Abs(tip - s.ThicknessMm) > 0.5)
                {
                    // root and tip halves, so the taper shows
                    float m = (float)(s.X0 + s.X1) / 2;
                    bool fore = s.Extent == "fore";
                    plates.Add(new PPlate(r, (float)s.X0, m, (float)s.Bottom, (float)s.Top, fore ? s.ThicknessMm : tip, Mm(fore ? s.ThicknessMm : tip)));
                    plates.Add(new PPlate(r, m, (float)s.X1, (float)s.Bottom, (float)s.Top, fore ? tip : s.ThicknessMm, Mm(fore ? tip : s.ThicknessMm)));
                }
                else
                    plates.Add(new PPlate(r, (float)s.X0, (float)s.X1, (float)s.Bottom, (float)s.Top, s.ThicknessMm, s.Kind == "box" ? "" : Mm(s.ThicknessMm)));
                if (s.Kind == "upper" && s.X0 <= secX && secX <= s.X1)
                    sec.Add(((float)s.Bottom, (float)s.Top, s.ThicknessMm, s.ThicknessMm, r));
            }
            if (belt != null)
            {
                float tf = (float)(belt.TaperFrom ?? belt.Bottom);
                double low = belt.BottomMm ?? belt.ThicknessMm;
                plates.Add(new PPlate(new PRef(PK.Belt), (float)belt.X0, (float)belt.X1, tf, (float)belt.Top, belt.ThicknessMm, Mm(belt.ThicknessMm)));
                if (tf > belt.Bottom + 0.05)
                    plates.Add(new PPlate(new PRef(PK.Belt), (float)belt.X0, (float)belt.X1, (float)belt.Bottom, tf, (belt.ThicknessMm + low) / 2,
                        Math.Abs(low - belt.ThicknessMm) > 0.5 ? "to " + Mm(low) : ""));
                sec.Add(((float)belt.Bottom, (float)belt.Top, belt.ThicknessMm, low, new PRef(PK.Belt)));
            }
            foreach (var bh in hb.Armour.Bulkheads ?? [])
            {
                var r = bh.Id.Contains("Steering", StringComparison.Ordinal) ? new PRef(PK.Steering)
                    : bh.Id.Contains("end belt", StringComparison.Ordinal) ? new PRef(PK.EndAft) : new PRef(PK.Bulkheads);
                float lt = (float)(bh.LowerTop ?? bh.Bottom);
                plates.Add(new PPlate(r, (float)bh.X, (float)bh.X, Math.Max(lt, (float)bh.Bottom), (float)bh.Top, bh.ThicknessMm, "", Bulkhead: true));
                if (lt > bh.Bottom + 0.05)
                    plates.Add(new PPlate(r, (float)bh.X, (float)bh.X, (float)bh.Bottom, lt, bh.LowerMm ?? bh.ThicknessMm / 2, "", Bulkhead: true, Inside: true));
            }
            var barbR = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var c in hb.Components)
            {
                if (c.X is not { } x)
                    continue;
                switch (c.Kind)
                {
                    case "barbette" when c.R is { } r && c.ArmourMm is { } mm and > 0 && c.Id.EndsWith(" barbette", StringComparison.Ordinal)
                                                      && mainIds.Contains(c.Id[..^9]):
                        barbR[c.Id[..^9]] = r;
                        float deckTop = raised.Any(q => x >= q.X0 && x <= q.X1) ? (float)raised.Where(q => x >= q.X0 && x <= q.X1).Max(q => q.Top) : 0;
                        if (c.Top > deckTop)
                            plates.Add(new PPlate(new PRef(PK.Barbettes), (float)(x - 0.8 * r), (float)(x + 0.8 * r), deckTop, (float)c.Top, mm, ""));
                        plates.Add(new PPlate(new PRef(PK.Barbettes), (float)(x - 0.8 * r), (float)(x + 0.8 * r), (float)c.Base, Math.Min(deckTop, (float)c.Top),
                            mm, "", Inside: true));
                        break;
                    case "conning_tower" when c.R is { } r && c.ArmourMm is { } mm and > 0:
                        plates.Add(new PPlate(new PRef(PK.Ct), (float)(x - r), (float)(x + r), (float)c.Base, (float)c.Top, mm, "CT"));
                        break;
                }
            }
            foreach (var c in hb.Components)
                if (c.Kind == "main" && c.X is { } x && c.ArmourMm is { } face and > 0)
                {
                    float r = (float)(barbR.GetValueOrDefault(c.Id, 5) * 1.15);
                    plates.Add(new PPlate(new PRef(PK.Main, 0), (float)x - r, (float)x + r, (float)c.Base, (float)c.Top, face, c.Id.Length <= 3 ? c.Id : ""));
                }
            foreach (var dk in hb.Armour.Decks ?? [])
            {
                var r = dk.Extent == "steering" ? new PRef(PK.Steering) : new PRef(PK.Deck, DeckRow(dk.Deck, dk.Extent));
                plates.Add(new PPlate(r, (float)dk.X0, (float)dk.X1, (float)dk.Z, (float)dk.Z, dk.ThicknessMm, Mm(dk.ThicknessMm), Flat: true));
                if (r.Kind == PK.Deck && r.I >= 0)
                {
                    string name = $"Deck armour ({dk.Deck.ToLowerInvariant()}" + (dk.Extent is "fore" or "aft" ? $", {dk.Extent})" : ")");
                    deckT[r.I] += w.GetValueOrDefault(name);
                    if (dk.X0 <= secX && secX <= dk.X1)
                        secDecks.Add(((float)dk.Z, dk.ThicknessMm, r, Mm(dk.ThicknessMm)));
                }
            }
        }

        // + ghosts: a deck's bare stretch, and the side parts the ship lacks
        var ghosts = new List<PGhost>();
        var bare = (Ext: new[] { "aft", "citadel", "fore" }, X0: new[] { -L / 2 + 0.04f * L, X0, X1 }, X1: new[] { X0, X1, L / 2 - 0.04f * L });
        foreach (var (n, name, z) in choices)
        {
            if (n < 0)
                continue;
            for (int s = 0; s < 3; s++)
            {
                float m = (bare.X0[s] + bare.X1[s]) / 2;
                if (bare.X1[s] - bare.X0[s] < 4 || plates.Any(p => p.Flat && p.Ref.Kind == PK.Deck && Math.Abs(p.Z0 - z) < 0.05 && p.X0 <= m && m <= p.X1))
                    continue;
                string ext = bare.Ext[s];
                double t = (bare.X1[s] - bare.X0[s]) * B * (ext == "citadel" ? 0.92 : 0.62) * 25 / 1000 * Weight.Steel;
                ghosts.Add(new PGhost(new PRef(PK.Deck, -1), (int)n, ext, bare.X0[s], bare.X1[s], z, z,
                    $"+ armour deck on the {name.ToLowerInvariant()}, {PE.ExtentText(ext)}, {Fmt(25, Quantity.Armour)}\nabout {N0(t)} t\n" +
                    "Then: full length or both ends in its row."));
            }
        }
        string AddTip(Part p) => $"+ {PE.Name(p).ToLowerInvariant()}: " + p switch
        {
            Part.Belt => $"over the citadel, {Fmt(100, Quantity.Armour)}",
            Part.Upper => $"from the belt up to the main deck, over the citadel, {Fmt(Math.Round((a.BeltMm ?? 200) * 0.05, MidpointRounding.AwayFromZero) * 10, Quantity.Armour)}",
            Part.EndFore => $"from the citadel to the stem, {Fmt(Math.Round((a.BeltMm ?? 200) * 0.04, MidpointRounding.AwayFromZero) * 10, Quantity.Armour)}",
            Part.EndAft => $"from the citadel to the stern, {Fmt(Math.Round((a.BeltMm ?? 200) * 0.04, MidpointRounding.AwayFromZero) * 10, Quantity.Armour)}",
            Part.Bulkheads => $"across both citadel ends, {Fmt(Math.Round((a.BeltMm ?? 200) * 0.07, MidpointRounding.AwayFromZero) * 10, Quantity.Armour)}",
            Part.Tds => "4.0 m deep, from the inner bottom to the roof deck",
            _ => "over the rudders: sides, roof and ends",
        } + "\nStarting values from the belt.";
        bool hasBelt = PE.Has(d, Part.Belt);
        if (hb != null)
        {
            if (!hasBelt)
                ghosts.Add(new PGhost(new PRef(PK.Belt), 0, "", X0, X1, beltBot, beltTop, AddTip(Part.Belt)));
            if (hasBelt && !PE.Has(d, Part.Upper))
                ghosts.Add(new PGhost(new PRef(PK.Upper), 0, "", X0, X1, beltTop, 0, AddTip(Part.Upper)));
            if (hasBelt && !PE.Has(d, Part.EndFore))
                ghosts.Add(new PGhost(new PRef(PK.EndFore), 0, "", X1, L / 2 - 0.02f * L, beltBot, beltTop, AddTip(Part.EndFore)));
            if (hasBelt && !PE.Has(d, Part.EndAft))
                ghosts.Add(new PGhost(new PRef(PK.EndAft), 0, "", -L / 2 + 0.02f * L, X0, beltBot, beltTop, AddTip(Part.EndAft)));
            if (!PE.Has(d, Part.Steering))
                ghosts.Add(new PGhost(new PRef(PK.Steering), 0, "", -L / 2 + 0.035f * L, -L / 2 + 0.085f * L, ib, Math.Min(DeckZ(4), wl), AddTip(Part.Steering)));
        }

        // the side profile: the deck line (raised over its stretches) over each station's keel
        var profile = new List<Vector2>();
        float zTop = 1.2f + (float)Math.Max(raised.Select(r => r.Top).DefaultIfEmpty(0).Max(),
            hb?.Components.Where(c => c.Kind is "main" or "conning_tower").Select(c => c.Top).DefaultIfEmpty(0).Max() ?? 0);
        float secKeel = keel;
        var secZ = new List<float>();
        var secY = new List<float>();
        if (hb != null)
        {
            foreach (var st in hb.HullForm.Stations)
                profile.Add(new Vector2((float)st.X, (float)st.Z[0]));
            var mid = hb.HullForm.Stations.MinBy(s => Math.Abs(s.X - secX))!;
            secX = (float)mid.X;
            secKeel = (float)mid.Z[0];
            for (int i = 0; i <= 24; i++)
            {
                float z = secKeel + (0 - secKeel) * i / 24f;
                secZ.Add(z);
                secY.Add((float)hb.HullForm.HalfWidth(secX, z + 1e-4));
            }
        }
        var lanes = new List<(string, float, float, float)>();
        foreach (var (n, name, z) in choices)
        {
            var rs = n < 0 ? raised[(int)(-n - 1)] : null;
            lanes.Add((DeckShort(n, rs?.Id ?? name), z, rs != null ? (float)rs.X0 : -L / 2, rs != null ? (float)Math.Min(rs.X1, L / 2) : L / 2));
        }

        // the rows, group by group: only the parts the ship has; the rest are + chips
        var rows = new List<ProtRow>();
        Vector4 Ramp(double mm) => T.Armour(mm);
        float Share(double t) => armourT > 0 ? (float)(t / armourT) : 0;
        string Tn(double t) => ship != null ? N0(t) : "–";
        string Pct(double t) => std > 0 ? $"{N0(t)} t · {100 * t / std:0.0} % of standard" : "–";
        ChoiceRow Choice(ChoiceKnob k)
        {
            string? v = k.Get(d) ?? k.Default;
            int i = Array.IndexOf(k.Values, v);
            var labels = i < 0 ? [.. k.Labels, v ?? "custom"] : k.Labels;
            return new ChoiceRow(k, labels, i < 0 ? labels.Length - 1 : i, true);
        }
        string Covers(float x0, float x1) => $"{N0(x0)} to {N0(x1)} m · {x1 - x0:0.0} m, {100 * (x1 - x0) / L:0} %";
        string WlSpan(float z0, float z1) => $"{z0 - wl:+0.0;−0.0} to {z1 - wl:+0.0;−0.0} m on the waterline";
        string Weights(string title, Func<string, bool> f)
        {
            var parts = w.Where(kv => f(kv.Key)).Select(kv => $"{kv.Key}  {N0(kv.Value)} t").ToList();
            return parts.Count == 0 ? title : title + "\n" + string.Join("\n", parts);
        }
        string Scheme(string part) => a.Materials?.GetValueOrDefault(part) ?? "the engine's default";

        if (PE.Has(d, Part.Belt))
        {
            double t = w.GetValueOrDefault("Belt armour");
            rows.Add(new ProtRow(new PRef(PK.Belt), 0, "Belt", Ramp(a.BeltMm ?? 0), Row(Knobs.Belt, d, ship) with { Delta = "" }, "",
                Row(Knobs.BeltBottom, d, ship) with { Delta = "" }, "lower", Row(Knobs.BeltDepth, d, ship) with { Delta = "" }, "↓",
                Row(Knobs.BeltHeight, d, ship) with { Delta = "" }, null, "", null, 0, "", "", Choice(Knobs.SchemeMaterial("belt", "Belt")), "",
                Tn(t), Share(t), Weights("Belt", n => n == "Belt armour"), true,
                [("Covers", Covers(X0, X1)), ("Height", WlSpan(beltBot, beltTop)), ("Weight", Pct(t)), ("Material", Scheme("belt")),
                 ("Taper", $"{Mm(a.BeltMm ?? 0)} at the waterline to {Mm(a.BeltBottomMm ?? a.BeltMm ?? 0)} at the lower edge")],
                "The citadel runs between the outermost barbettes; the layout places it, so its length moves when the turrets do."));
        }
        if (PE.Has(d, Part.Upper))
        {
            double t = w.GetValueOrDefault("Upper belt");
            var s = hb?.Armour.Strakes?.FirstOrDefault(x => x.Kind == "upper");
            long to = a.UpperBelt!.ToDeck ?? 0;
            rows.Add(new ProtRow(new PRef(PK.Upper), 0, "Upper belt", Ramp(a.UpperBelt.Mm ?? 0), Row(Knobs.UpperBelt, d, ship) with { Delta = "" }, "",
                null, "", null, "", null, Knobs.UpperBeltExtent, a.UpperBelt.Extent ?? "citadel", Knobs.UpperBeltToDeck, to, "to " + DeckShort(to, Decks.DeckName(to)),
                "", Choice(Knobs.UpperBeltMaterial), "", Tn(t), Share(t), Weights("Upper belt", n => n == "Upper belt"), true,
                [("Covers", s != null ? Covers((float)s.X0, (float)s.X1) : "–"), ("Height", s != null ? WlSpan((float)s.Bottom, (float)s.Top) : "–"), ("Weight", Pct(t)),
                 ("Material", a.UpperBelt.Material ?? Scheme("upper_belt"))],
                "From the belt's top edge up to the chosen deck."));
        }
        foreach (var (p, end) in new[] { (Part.EndFore, "fore"), (Part.EndAft, "aft") })
        {
            if (!PE.Has(d, p))
                continue;
            var e = a.EndBelts!.Of(end)!;
            string prefix = end == "fore" ? "Fore end belt" : "Aft end belt";
            double t = Sum(n => n.StartsWith(prefix, StringComparison.Ordinal));
            var s = hb?.Armour.Strakes?.FirstOrDefault(x => x.Kind == "end" && x.Extent == end);
            double reach = e.Reach ?? 1;
            rows.Add(new ProtRow(new PRef(end == "fore" ? PK.EndFore : PK.EndAft), 0, PE.Name(p), Ramp(e.Mm ?? 0), Row(Knobs.EndBeltMm(end), d, ship) with { Delta = "" }, "",
                Row(Knobs.EndBeltTip(end), d, ship) with { Delta = "" }, "tip", Row(Knobs.EndBeltReach(end), d, ship) with { Delta = "" }, "reach", null, null, "", null, 0, "",
                reach >= 0.995 ? $"to the {(end == "fore" ? "stem" : "stern")}" : $"{reach * 100:0} % of the way",
                Choice(Knobs.EndBeltMaterial(end)), "", Tn(t), Share(t), Weights(PE.Name(p), n => n.StartsWith(prefix, StringComparison.Ordinal)), true,
                [("Covers", s != null ? Covers((float)s.X0, (float)s.X1) : "–"), ("Height", WlSpan(beltBot, beltTop)), ("Weight", Pct(t)),
                 ("Material", e.Material ?? Scheme("end_belts"))],
                end == "fore" ? "Same height as the belt; tapers from the root to the tip. Reach is the share of the way to the stem."
                    : "Same height as the belt; tapers from the root to the tip. At reach 1.0 it turns across the stern face."));
        }
        if (PE.Has(d, Part.Bulkheads))
        {
            double t = w.GetValueOrDefault("Bulkheads");
            rows.Add(new ProtRow(new PRef(PK.Bulkheads), 0, "Citadel bulkheads", Ramp(a.BulkheadMm ?? 0), Row(Knobs.Bulkheads, d, ship) with { Delta = "" }, "",
                null, "", null, "", null, null, "", null, 0, "", $"at both citadel ends · {Mm((a.BulkheadMm ?? 0) / 2)} below the belt",
                Choice(Knobs.SchemeMaterial("bulkheads", "Bulkheads")), "", Tn(t), Share(t), Weights("Citadel bulkheads", n => n == "Bulkheads"), true,
                [("Where", $"at {N0(X0)} and {N0(X1)} m"), ("Weight", Pct(t)), ("Material", Scheme("bulkheads"))],
                "Full thickness down to the belt's lower edge, half below it to the inner bottom."));
        }
        if (PE.Has(d, Part.Tds))
        {
            double t = w.GetValueOrDefault("Torpedo protection");
            rows.Add(new ProtRow(new PRef(PK.Tds), 0, "Torpedo protection", T.Blue, Row(Knobs.Tds, d, ship) with { Delta = "" }, "",
                null, "", null, "", null, null, "", null, 0, "", "from the roof deck down to the inner bottom", null, "layered bulkheads",
                Tn(t), Share(t), Weights("Torpedo protection", n => n == "Torpedo protection"), true,
                [("Depth", $"{a.TdsM:0.0} m from the shell to the holding bulkhead"), ("Covers", Covers(X0, X1)), ("Weight", Pct(t))],
                "Its spaces count in the subdivision (liquid layers, holding bulkhead); the depth also takes beam."));
        }
        for (int i = 0; i < designDecks.Count; i++)
        {
            var x = designDecks[i];
            long n = x.Deck ?? 0;
            string ext = x.Extent ?? "citadel";
            string name = Decks.DeckName(n) + (ext == "citadel" ? "" : ", " + PE.ExtentText(ext));
            bool roof = n == PE.RoofDeck(d) && ext is "citadel" or "full";
            float z = DeckZ(n);
            rows.Add(new ProtRow(new PRef(PK.Deck, i), 1, name, Ramp(x.Mm ?? 0), Row(Knobs.DeckMm(i), d, ship) with { Delta = "" }, "",
                null, "", null, "", null, Knobs.DeckExtent(i), ext, Knobs.DeckLevel(i), n, DeckShort(n, Decks.DeckName(n)), "",
                Choice(Knobs.DeckMaterial(i)), "", Tn(deckT[i]), Share(deckT[i]), $"{name}\n{N0(deckT[i])} t", true,
                [("On", $"{Decks.DeckNameLower(n)} · {-z:0.0} m under the main deck, {z - wl:+0.0;−0.0} m on the WL"),
                 ("Over", PE.ExtentText(ext)), ("Weight", Pct(deckT[i])), ("Material", x.Material ?? Scheme("decks"))],
                roof ? "The citadel's roof: the lowest armour deck over it, the one the belt's top edge meets."
                    : "Over or beside the roof deck: a capping deck to start shells, or a splinter deck below."));
        }
        if (d.StyleName == "carrier")
        {
            double t = w.GetValueOrDefault("Flight deck armour");
            rows.Add(new ProtRow(new PRef(PK.FlightDeck), 1, "Flight deck", Ramp(a.FlightDeckMm ?? 0), Row(Knobs.FlightDeckArmour, d, ship) with { Delta = "" }, "",
                null, "", null, "", null, null, "", null, 0, "", "the flight deck's plating", null, Scheme("flight_deck"), Tn(t), Share(t),
                "Flight deck armour", false, [("Weight", Pct(t))], "The flight deck's own armour, over the hangar."));
        }
        if (PE.Has(d, Part.Steering))
        {
            double t = Sum(n => n.StartsWith("Steering gear box", StringComparison.Ordinal) || n.EndsWith(", steering)", StringComparison.Ordinal));
            rows.Add(new ProtRow(new PRef(PK.Steering), 2, "Steering gear box", Ramp(a.SteeringBox!.Mm ?? 0), Row(Knobs.SteeringSides, d, ship) with { Delta = "" }, "",
                Row(Knobs.SteeringRoof, d, ship) with { Delta = "" }, "roof", Row(Knobs.SteeringEnds, d, ship) with { Delta = "" }, "ends", null, null, "", null, 0, "",
                "over the rudders", Choice(Knobs.SteeringMaterial), "", Tn(t), Share(t),
                Weights("Steering gear box", n => n.StartsWith("Steering gear box", StringComparison.Ordinal) || n.EndsWith(", steering)", StringComparison.Ordinal)), true,
                [("Sides", Fmt(a.SteeringBox.Mm ?? 0, Quantity.Armour)), ("Roof", Fmt(a.SteeringBox.DeckMm ?? 0, Quantity.Armour)),
                 ("Ends", Fmt(a.SteeringBox.BulkheadMm ?? 0, Quantity.Armour)), ("Weight", Pct(t))],
                "A box over the steering gear: sides, a roof on its own deck and two ends."));
        }
        var mains = d.Main ?? [];
        for (int k = 0; k < mains.Count; k++)
        {
            double f = mains[k].ArmourMm ?? 0;
            rows.Add(new ProtRow(new PRef(PK.Main, k), 3, mains.Count == 1 ? "Turrets" : $"Turrets, battery {(char)('A' + k)}", Ramp(f),
                Row(Knobs.MainArmour(k), d, ship) with { Delta = "" }, "", null, "", null, "", null, null, "", null, 0, "",
                $"faces · side {Mm(f * 0.55)} · rear {Mm(f * 0.5)} · roof {Mm(f * 0.4)}", Choice(Knobs.MainMaterial(k)), "", "turret", 0,
                "The gunhouse armour is part of each turret's weight (armament), so it has no line of its own here.", false,
                [("Gunhouse", $"face {Mm(f)} · side {Mm(f * 0.55)} · rear {Mm(f * 0.5)} · roof {Mm(f * 0.4)}"), ("Material", mains[k].Material ?? Scheme("turrets"))],
                "Also on the Main battery tab. The side, rear and roof are fixed shares of the face."));
        }
        if (mains.Count > 0 && (mains[0].ArmourMm ?? 0) > 0)
        {
            double t = Sum(n => n.StartsWith("Barbette ", StringComparison.Ordinal) && mainIds.Contains(n[9..]));
            double mm = 0.8 * (mains[0].ArmourMm ?? 0);
            rows.Add(new ProtRow(new PRef(PK.Barbettes), 3, "Barbettes", Ramp(mm), null, Fmt(mm, Quantity.Armour), null, "", null, "", null, null, "", null, 0, "",
                "0.8 × the faces", null, "as the turrets", Tn(t), Share(t), Weights("Barbettes", n => n.StartsWith("Barbette ", StringComparison.Ordinal) && mainIds.Contains(n[9..])),
                false, [("Thickness", Fmt(mm, Quantity.Armour)), ("Weight", Pct(t))], "Barbettes follow the turret faces at 0.8 ×, from the armour deck up to the gunhouse."));
        }
        if (hasBelt && hb?.Components.Any(c => c.Kind == "conning_tower") == true)
        {
            double t = w.GetValueOrDefault("Conning tower");
            rows.Add(new ProtRow(new PRef(PK.Ct), 3, "Conning tower", Ramp(a.BeltMm ?? 0), null, Fmt(a.BeltMm ?? 0, Quantity.Armour), null, "", null, "", null, null, "",
                null, 0, "", "the belt's thickness", Choice(Knobs.SchemeMaterial("conning_tower", "Conning tower")), "", Tn(t), Share(t),
                Weights("Conning tower", n => n == "Conning tower"), false, [("Thickness", Fmt(a.BeltMm ?? 0, Quantity.Armour)), ("Weight", Pct(t))],
                "The conning tower takes the belt's thickness: set the belt."));
        }

        var chips = new PChip[4][];
        chips[0] = new[] { Part.Belt, Part.Upper, Part.EndFore, Part.EndAft, Part.Bulkheads, Part.Tds }.Where(p => !PE.Has(d, p))
            .Select(p => new PChip(p, 0, "+ " + PE.Name(p).ToLowerInvariant().Replace("end belt, ", "end belt "), AddTip(p))).ToArray();
        chips[1] = choices.Where(c => c.N >= 0 && !designDecks.Any(x => (x.Deck ?? 0) == c.N))
            .Select(c => new PChip(null, (int)c.N, "+ " + DeckShort(c.N, c.Name),
                $"+ armour deck on the {c.Name.ToLowerInvariant()}, over the citadel, {Fmt(25, Quantity.Armour)}\n" +
                $"about {N0((X1 - X0) * B * 0.92 * 25 / 1000 * Weight.Steel)} t\nThen: its extent in the row, or drag it to another deck.")).ToArray();
        chips[2] = PE.Has(d, Part.Steering) ? [] : [new PChip(Part.Steering, 0, "+ steering gear box", AddTip(Part.Steering))];
        chips[3] = [];

        var cov = PE.Reads(d);
        var coverTips = PE.Coverages.Select(c =>
        {
            var lines = PE.Describe(d, PE.Apply(d, c, deepest));
            return $"{PE.Name(c)}\n{PE.Purpose(c)}\n\n" + (lines.Count > 0 ? string.Join("\n", lines) : "No change: the design is already this.") +
                   "\n\nThicknesses stay; new plates start from the belt's.";
        }).ToArray();
        string reads = ship != null ? $"reads as {PE.Name(cov).ToLowerInvariant()} · armour {N0(armourT)} t, {100 * armourT / Math.Max(1, std):0.0} % of standard"
            : $"reads as {PE.Name(cov).ToLowerInvariant()}";

        var upper = hb?.Armour.Strakes?.FirstOrDefault(x => x.Kind == "upper");
        var tipF = hb?.Armour.Strakes?.FirstOrDefault(x => x.Kind == "end" && x.Extent == "fore");
        var tipA = hb?.Armour.Strakes?.FirstOrDefault(x => x.Kind == "end" && x.Extent == "aft");
        return new ProtPane(rows.Select(r => r with { Hover = PlateTip(r, r.Ref) }).ToArray(), chips, plates.ToArray(), ghosts.ToArray(), lanes.ToArray(), profile.ToArray(), L, B, wl, keel, ib, zTop, X0, X1,
            beltTop, beltBot, Math.Min(roofZ, wl), $"citadel {X1 - X0:0.0} m · {100 * (X1 - X0) / L:0} % of length", choices.ToArray(),
            secX, secZ.ToArray(), secY.ToArray(), sec.ToArray(), secDecks.ToArray(), (float)(a.TdsM ?? 0),
            reads, cov, coverTips, ArmourPick(d), mains.Count > 0 ? $"her own {Fmt(mains[0].CalibreMm ?? 0, Quantity.Calibre)}" : "a gun",
            Row(Knobs.BeltHeight, d, ship) with { Delta = "" }, Row(Knobs.BeltDepth, d, ship) with { Delta = "" }, Row(Knobs.Tds, d, ship) with { Delta = "" },
            ship != null)
        {
            BeltTip = "Drag the belt's top or bottom edge (0.1 m steps)\n" +
                      $"now {beltBot - wl:+0.0;−0.0} to {beltTop - wl:+0.0;−0.0} m on the waterline · the end belts share the heights",
            TdsTip = $"Drag the holding bulkhead: the torpedo protection's depth, now {a.TdsM ?? 0:0.0} m (0.1 m steps)",
            EndTipFore = tipF != null ? (float)tipF.X1 : float.NaN,
            EndTipAft = tipA != null ? (float)tipA.X0 : float.NaN,
            UpX0 = upper != null ? (float)upper.X0 : float.NaN,
            UpX1 = upper != null ? (float)upper.X1 : float.NaN,
            UpTop = upper != null ? (float)upper.Top : float.NaN,
        };
    }

    // ------------------------------------------------------------------ the pane

    void ProtectionPane(Vector2 size)
    {
        var pp = snap!.Prot!;
        ImGui.BeginChild("##protection", size, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var dl = ImGui.GetWindowDrawList();
        var o = ImGui.GetCursorScreenPos();
        float w = size.X, gap = P(6);
        protHot = protHotNext;
        protHotNext = null;
        if (ProtRowOf(pp, protSel) == null && pp.Rows.Length > 0)
            protSel = pp.Rows[0].Ref;

        ProtBar(dl, o, w, pp);
        float rowH = ProtRowH, groupH = P(15);
        float tableH = P(17) + pp.Rows.Length * rowH + 4 * groupH + P(2);
        float topY = o.Y + P(18) + gap;
        float topH = Math.Clamp(size.Y - P(18) - 3 * gap - tableH, P(170), MathF.Max(P(170), size.Y * 0.46f));   // the lower half keeps room
        float secW = MathF.Round(Math.Clamp(w * 0.33f, P(330), P(470)));
        float elevW = w - secW - 3 * gap;
        ProtElevation(dl, new Vector2(o.X + gap, topY), new Vector2(elevW, topH), pp);
        ProtSection(dl, new Vector2(o.X + 2 * gap + elevW, topY), new Vector2(secW, topH), pp);
        float lowY = topY + topH + gap, lowH = o.Y + size.Y - gap - lowY;
        ProtTable(dl, new Vector2(o.X + gap, lowY), new Vector2(elevW, lowH), pp);
        ProtInspector(dl, new Vector2(o.X + 2 * gap + elevW, lowY), new Vector2(secW, lowH), pp);
        ProtMenu(pp);
        ImGui.EndChild();
    }

    static float ProtRowH => MathF.Max(P(18), ImGui.GetFrameHeight() + P(2));

    /// <summary>The sub-bar: what the scheme reads as, the coverage presets, the armour scheme.</summary>
    void ProtBar(ImDrawListPtr dl, Vector2 o, float w, ProtPane pp)
    {
        float h = P(18);
        dl.AddRectFilled(o, o + new Vector2(w, h), T.U32(T.HeaderBg));
        dl.AddLine(o + new Vector2(0, h - 1), o + new Vector2(w, h - 1), T.U32(T.Edge));
        float ty = o.Y + (h - UiFonts.SansSmall.FontSize) / 2;
        Text(dl, UiFonts.SansSmall, new Vector2(o.X + P(6), ty), T.Muted, "PROTECTION");
        Text(dl, UiFonts.SansSmall, new Vector2(o.X + P(6) + Width(UiFonts.SansSmall, "PROTECTION") + P(10), ty), T.Text, pp.Reads);

        var d = doc.Current;
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(5), 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 0));
        float schemeW = P(260);
        float segsW = 0;
        foreach (var c in PE.Coverages)
            segsW += Width(UiFonts.SansSmall, PE.Name(c)) + P(10) + 1;
        float x = o.X + w - P(4) - schemeW - P(6) - Width(UiFonts.SansSmall, "Scheme") - P(14) - segsW - P(6) - Width(UiFonts.SansSmall, "Coverage");
        Text(dl, UiFonts.SansSmall, new Vector2(x, ty), T.Muted, "Coverage");
        ImGui.SetCursorScreenPos(new Vector2(x + Width(UiFonts.SansSmall, "Coverage") + P(6), o.Y + P(1)));
        for (int i = 0; i < PE.Coverages.Length; i++)
        {
            var c = PE.Coverages[i];
            if (i > 0)
                ImGui.SameLine();
            if (Seg(PE.Name(c), pp.Coverage == c, Width(UiFonts.SansSmall, PE.Name(c)) + P(10)))
                Edit(PE.Apply(d, c, DeepestDeck(pp)), $"Coverage: {PE.Name(c).ToLowerInvariant()}");
            if (ImGui.IsItemHovered())
                Tip(pp.CoverTips[i]);
            if (protForceTip != null && protForceTip == CoverForce[i])
                TipAt(pp.CoverTips[i], ImGui.GetItemRectMin() + new Vector2(-P(120), P(20)));
        }
        float sx = o.X + w - P(4) - schemeW - P(6) - Width(UiFonts.SansSmall, "Scheme");
        Text(dl, UiFonts.SansSmall, new Vector2(sx, ty), T.Muted, "Scheme");
        ImGui.SetCursorScreenPos(new Vector2(o.X + w - P(4) - schemeW, o.Y + P(1)));
        ImGui.SetNextItemWidth(schemeW);
        int si = pp.Scheme.Index;
        if (ImGui.Combo("##scheme", ref si, pp.Scheme.Names, pp.Scheme.Names.Length) && si != pp.Scheme.Index && si < pp.Scheme.Count)
            pp.Scheme.Apply(si);
        ImGui.PopStyleVar(2);
        ImGui.PopFont();
    }

    static int DeepestDeck(ProtPane pp)
    {
        long n = 1;
        foreach (var c in pp.DeckChoices)
            n = Math.Max(n, c.N);
        return (int)n;
    }

    /// <summary>A knob's row for a plate's thickness, for the wheel over a drawing.</summary>
    NumberKnob? ThickKnob(PRef r) => r.Kind switch
    {
        PK.Belt => Knobs.Belt,
        PK.Upper => Knobs.UpperBelt,
        PK.EndFore => Knobs.EndBeltMm("fore"),
        PK.EndAft => Knobs.EndBeltMm("aft"),
        PK.Bulkheads => Knobs.Bulkheads,
        PK.Tds => Knobs.Tds,
        PK.Deck when r.I >= 0 => Knobs.DeckMm(r.I),
        PK.Steering => Knobs.SteeringSides,
        PK.Main => Knobs.MainArmour(r.I),
        PK.FlightDeck => Knobs.FlightDeckArmour,
        _ => null,
    };

    bool ProtLit(PRef r) => r == protSel || protHot == r;

    // ------------------------------------------------------------------ the elevation

    /// <summary>Ship metres to the elevation's pixels: x to scale, heights stretched to fill.</summary>
    readonly record struct ElevMap(float Left, float Top, float Kx, float Kz, float L, float ZTop)
    {
        public float X(float x) => Left + (x + L / 2) * Kx;
        public float Z(float z) => Top + (ZTop - z) * Kz;
        public float InvX(float px) => (px - Left) / Kx - L / 2;
        public float InvZ(float py) => ZTop - (py - Top) / Kz;
    }

    void ProtElevation(ImDrawListPtr dl, Vector2 pos, Vector2 size, ProtPane pp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Plan));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "ELEVATION", "heights stretched, deck by deck · + on a bare deck or side adds armour there");
        var io = ImGui.GetIO();
        float left = P(58), right = P(8), top = P(26), bottom = P(16);
        float kx = (size.X - left - right) / pp.L;
        float kz = (size.Y - top - bottom) / Math.Max(1, pp.ZTop - (pp.Keel - 0.4f));
        var m = new ElevMap(pos.X + left, pos.Y + top, kx, kz, pp.L, pp.ZTop);
        var font = UiFonts.SansSmall;
        var mono = UiFonts.Mono;

        // the hull side: quads from the deck line down to each station's keel
        for (int i = 0; i + 1 < pp.Profile.Length; i++)
        {
            var (p0, p1) = (pp.Profile[i], pp.Profile[i + 1]);
            float t0 = HullTop(pp, p0.X), t1 = HullTop(pp, p1.X);
            dl.AddQuadFilled(new Vector2(m.X(p0.X), m.Z(t0)), new Vector2(m.X(p1.X), m.Z(t1)), new Vector2(m.X(p1.X), m.Z(p1.Y)),
                new Vector2(m.X(p0.X), m.Z(p0.Y)), T.U32(new Vector4(0.10f, 0.176f, 0.267f, 1)));
        }
        uint hullLine = T.U32(new Vector4(0.435f, 0.576f, 0.71f, 1));
        for (int i = 0; i + 1 < pp.Profile.Length; i++)
        {
            var (p0, p1) = (pp.Profile[i], pp.Profile[i + 1]);
            dl.AddLine(new Vector2(m.X(p0.X), m.Z(p0.Y)), new Vector2(m.X(p1.X), m.Z(p1.Y)), hullLine);
            dl.AddLine(new Vector2(m.X(p0.X), m.Z(HullTop(pp, p0.X))), new Vector2(m.X(p1.X), m.Z(HullTop(pp, p1.X))), hullLine);
        }
        // deck lanes, dashed, named in the margin
        uint lane = T.U32(new Vector4(0.61f, 0.76f, 0.886f, 0.28f));
        foreach (var (name, z, x0, x1) in pp.Lanes)
        {
            Dashed(dl, new Vector2(m.X(x0), m.Z(z)), new Vector2(m.X(x1), m.Z(z)), lane, P(3));
            Text(dl, font, new Vector2(pos.X + left - P(6) - Width(font, name), m.Z(z) - font.FontSize / 2), new Vector4(0.5f, 0.61f, 0.72f, 1), name);
        }
        Dashed(dl, new Vector2(m.X(-pp.L / 2 + 0.05f * pp.L), m.Z(pp.Ib)), new Vector2(m.X(pp.L / 2 - 0.06f * pp.L), m.Z(pp.Ib)), lane, P(3));
        Text(dl, font, new Vector2(pos.X + left - P(6) - Width(font, "Inner btm"), m.Z(pp.Ib) - font.FontSize / 2), new Vector4(0.5f, 0.61f, 0.72f, 1), "Inner btm");
        dl.AddLine(new Vector2(m.X(-pp.L / 2) - P(4), m.Z(pp.Wl)), new Vector2(m.X(pp.L / 2) + P(4), m.Z(pp.Wl)), T.U32(T.Blue));
        Text(dl, font, new Vector2(pos.X + P(4), m.Z(pp.Wl) - font.FontSize / 2), T.Blue, "WL");
        // the citadel's brackets
        uint brk = T.U32(T.Brass with { W = 0.22f });
        Dashed(dl, new Vector2(m.X(pp.X0), pos.Y + P(20)), new Vector2(m.X(pp.X0), pos.Y + size.Y - bottom), brk, P(3), vertical: true);
        Dashed(dl, new Vector2(m.X(pp.X1), pos.Y + P(20)), new Vector2(m.X(pp.X1), pos.Y + size.Y - bottom), brk, P(3), vertical: true);
        Text(dl, font, new Vector2((m.X(pp.X0) + m.X(pp.X1) - Width(font, pp.Citadel)) / 2, pos.Y + size.Y - bottom + P(1)), new Vector4(0.55f, 0.48f, 0.33f, 1), pp.Citadel);
        Text(dl, font, new Vector2(m.X(-pp.L / 2), pos.Y + size.Y - bottom + P(1)), new Vector4(0.37f, 0.48f, 0.59f, 1), "aft");
        Text(dl, font, new Vector2(m.X(pp.L / 2) - Width(font, "fore"), pos.Y + size.Y - bottom + P(1)), new Vector4(0.37f, 0.48f, 0.59f, 1), "fore");

        // the interaction area under everything drawn from here
        var area0 = new Vector2(pos.X + 1, pos.Y + P(18));
        ImGui.SetCursorScreenPos(area0);
        ImGui.SetNextItemAllowOverlap();
        ImGui.InvisibleButton("##elev", new Vector2(size.X - 2, size.Y - P(18) - 1), ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        bool hover = ImGui.IsItemHovered();
        bool clickL = ImGui.IsItemClicked(ImGuiMouseButton.Left), clickR = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        if (hover)
            ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY);
        var mouse = io.MousePos;

        // the plates
        protHits.Clear();
        dl.PushClipRect(pos + new Vector2(1, P(18)), pos + size - new Vector2(1, 1), true);
        foreach (var pl in pp.Plates)
            DrawPlate(dl, m, pl, pp);
        // the + ghosts: on hover a dashed outline and its +
        PGhost? ghostHot = null;
        foreach (var g in pp.Ghosts)
        {
            var (a, b) = GhostRect(m, g);
            bool on = protDrag == DragKind.None && hover && Inside(mouse, a, b) && !HitPlate(mouse).HasValue
                      || protForceTip != null && g.Ref.Kind == PK.Deck && protForceTip == g.Force;
            if (!on)
                continue;
            ghostHot = g;
            dl.AddRectFilled(a, b, T.U32(T.Brass with { W = 0.12f }));
            dl.AddRect(a, b, T.U32(T.Brass with { W = 0.75f }));
            Text(dl, UiFonts.SansBold, (a + b) / 2 - new Vector2(Width(UiFonts.SansBold, "+") / 2, UiFonts.SansBold.FontSize / 2), new Vector4(1, 0.906f, 0.659f, 1), "+");
            if (protForceTip != null)
                TipAt(g.Tip, b + new Vector2(P(6), P(-4)));
            else
                Tip(g.Tip);
        }
        // the selected plate's handles, and a drag in progress
        var plateHot = protDrag == DragKind.None && hover && ghostHot == null ? HitPlate(mouse) : null;
        var handleHot = ElevHandles(dl, m, pp, mouse, hover, clickL);
        dl.PopClipRect();
        if (plateHot is { } ph && handleHot == DragKind.None)
        {
            protHotNext = ph;
            if (ProtRowOf(pp, ph) is { } row)
                Tip(row.Hover);
            if (clickL)
            {
                protSel = ph;
                stale = true;
            }
            if (clickR)
            {
                protSel = ph;
                protMenuFor = ph;
                openProtMenu = true;
            }
            if (io.MouseWheel != 0 && ThickKnob(ph) is { } k)
                Step(Row(k, doc.Current, shown?.Ship), Math.Sign(io.MouseWheel) * (io.KeyShift ? 4 : 1));
        }
        else if (ghostHot is { } gh && clickL)
            AddFromGhost(gh);
    }

    static bool Inside(Vector2 p, Vector2 a, Vector2 b) => p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X) && p.Y >= Math.Min(a.Y, b.Y) && p.Y <= Math.Max(a.Y, b.Y);

    PRef? HitPlate(Vector2 p)
    {
        for (int i = protHits.Count - 1; i >= 0; i--)
            if (Inside(p, protHits[i].A, protHits[i].B))
                return protHits[i].R;
        return null;
    }

    static float HullTop(ProtPane pp, float x)
    {
        float t = 0;
        foreach (var l in pp.Lanes)
            if (l.Z > 0 && x >= l.X0 && x <= l.X1)
                t = MathF.Max(t, l.Z);
        return t;
    }

    static (Vector2, Vector2) GhostRect(ElevMap m, PGhost g) => g.Ref.Kind == PK.Deck
        ? (new Vector2(m.X(g.X0) + 1, m.Z(g.Z0) - P(5)), new Vector2(m.X(g.X1) - 1, m.Z(g.Z0) + P(5)))
        : (new Vector2(m.X(g.X0) + 1, m.Z(g.Z1)), new Vector2(m.X(g.X1) - 1, m.Z(g.Z0)));

    void DrawPlate(ImDrawListPtr dl, ElevMap m, PPlate pl, ProtPane pp)
    {
        var col = T.Armour(pl.Mm);
        bool lit = ProtLit(pl.Ref);
        bool dim = protHot is { } h && !lit;
        if (dim)
            col = col with { W = 0.55f };
        uint edge = T.U32(lit ? new Vector4(1, 0.906f, 0.659f, 0.95f) : new Vector4(0, 0, 0, 0.45f));
        var font = UiFonts.SansSmall;
        if (pl.Ref.Kind == PK.Tds)
        {
            var a = new Vector2(m.X(pl.X0), m.Z(pl.Z1));
            var b = new Vector2(m.X(pl.X1), m.Z(pl.Z0));
            dl.PushClipRect(a, b, true);
            uint hatch = T.U32(T.Blue with { W = lit ? 0.4f : 0.22f });
            for (float x = a.X - (b.Y - a.Y); x < b.X; x += P(6))
                dl.AddLine(new Vector2(x, b.Y), new Vector2(x + (b.Y - a.Y), a.Y), hatch, P(2));
            dl.PopClipRect();
            dl.AddRect(a, b, T.U32(T.Blue with { W = lit ? 0.8f : 0.35f }));
            Text(dl, font, new Vector2(a.X + P(6), b.Y - font.FontSize - P(3)), new Vector4(0.5f, 0.69f, 0.88f, 1), pl.Label);
            protHits.Add((a, b, pl.Ref));
            return;
        }
        if (pl.Flat)
        {
            float th = Math.Clamp((float)pl.Mm / 25 * S, P(2.5f), P(6));
            var a = new Vector2(m.X(pl.X0), m.Z(pl.Z0));
            var b = new Vector2(m.X(pl.X1), a.Y + th);
            dl.AddRectFilled(a, b, T.U32(col));
            dl.AddRect(a, b, edge);
            float lx = MathF.Min(a.X + P(24), (a.X + b.X) / 2), ly = b.Y + P(2);
            float lw = Width(font, pl.Label) + P(6);
            dl.AddRectFilled(new Vector2(lx - lw / 2, ly), new Vector2(lx + lw / 2, ly + font.FontSize + P(1)), T.U32(new Vector4(0.055f, 0.07f, 0.082f, 0.82f)), P(2));
            Text(dl, font, new Vector2(lx - lw / 2 + P(3), ly), T.Armour(pl.Mm), pl.Label);
            protHits.Add((a - new Vector2(0, P(4)), b + new Vector2(0, P(4)), pl.Ref));
            return;
        }
        if (pl.Bulkhead)
        {
            float x = m.X(pl.X0), hw = pl.Inside ? P(1.5f) : P(2.5f);
            var a = new Vector2(x - hw, m.Z(pl.Z1));
            var b = new Vector2(x + hw, m.Z(pl.Z0));
            dl.AddRectFilled(a, b, T.U32(col));
            dl.AddRect(a, b, edge);
            protHits.Add((new Vector2(x - P(6), a.Y), new Vector2(x + P(6), b.Y), pl.Ref));
            return;
        }
        {
            var a = new Vector2(m.X(pl.X0), m.Z(pl.Z1));
            var b = new Vector2(MathF.Max(m.X(pl.X1), a.X + 1.5f), MathF.Max(m.Z(pl.Z0), a.Y + 2));
            if (pl.Inside)
            {
                Dashed(dl, a, new Vector2(a.X, b.Y), T.U32(col with { W = 0.8f }), P(3), vertical: true);
                Dashed(dl, new Vector2(b.X, a.Y), b, T.U32(col with { W = 0.8f }), P(3), vertical: true);
                return;
            }
            if (pl.Ref.Kind == PK.Main)
            {
                // the gunhouse: its sides in the side's thickness, the face toward its end
                dl.AddRectFilled(a, b, T.U32(T.Armour(pl.Mm * 0.55) with { W = col.W }));
                dl.AddRect(a, b, edge);
                bool fore = pl.X0 + pl.X1 > 0;
                float fx = fore ? b.X - P(3) : a.X;
                dl.AddRectFilled(new Vector2(fx, a.Y), new Vector2(fx + P(3), b.Y), T.U32(col));
                if (pl.Label.Length > 0)
                    Text(dl, font, new Vector2((a.X + b.X - Width(font, pl.Label)) / 2, a.Y - font.FontSize - P(1)), T.Text, pl.Label);
            }
            else
            {
                dl.AddRectFilled(a, b, T.U32(col));
                dl.AddRect(a, b, edge);
                float lw = Width(font, pl.Label);
                if (pl.Label.Length > 0 && pl.Ref.Kind == PK.Ct)
                    Text(dl, font, new Vector2((a.X + b.X - lw) / 2, a.Y - font.FontSize - P(1)), T.Text, pl.Label);
                else if (pl.Label.Length > 0 && b.X - a.X > lw + P(8) && b.Y - a.Y >= font.FontSize)
                    Text(dl, font, (a + b) / 2 - new Vector2(lw / 2, font.FontSize / 2), pl.Mm >= 140 ? new Vector4(0.965f, 0.937f, 0.886f, 1) : new Vector4(0.149f, 0.133f, 0.11f, 1), pl.Label);
            }
            protHits.Add((a, b, pl.Ref));
        }
    }

    static void Dashed(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col, float dash, bool vertical = false)
    {
        float len = vertical ? b.Y - a.Y : b.X - a.X;
        for (float t = 0; t < len; t += 2 * dash)
        {
            float e = MathF.Min(t + dash, len);
            if (vertical)
                dl.AddLine(new Vector2(a.X, a.Y + t), new Vector2(a.X, a.Y + e), col);
            else
                dl.AddLine(new Vector2(a.X + t, a.Y), new Vector2(a.X + e, a.Y), col);
        }
    }

    /// <summary>The selected plate's brass handles on the elevation; a press starts a drag, which previews and edits on
    /// release. Returns the handle under the mouse.</summary>
    DragKind ElevHandles(ImDrawListPtr dl, ElevMap m, ProtPane pp, Vector2 mouse, bool hover, bool press)
    {
        var hot = DragKind.None;
        var d = doc.Current;
        var sel = protSel;
        int forced = protForceTip is { } ft && ft.StartsWith("handle:", StringComparison.Ordinal) ? int.Parse(ft[7..], CultureInfo.InvariantCulture) : -1;
        int n = 0;
        void H(DragKind kind, int i, Vector2 at, bool horizontal, string tip)
        {
            bool on = hover && Vector2.Distance(mouse, at) <= P(8) && protDrag == DragKind.None;
            Handle(dl, at, horizontal, on || protDrag == kind && protDragI == i);
            if (on)
            {
                hot = kind;
                Tip(tip);
                if (press)
                    StartDrag(kind, i);
            }
            if (n++ == forced)
                TipAt(tip, at + new Vector2(P(10), P(8)));
        }
        var row = ProtRowOf(pp, sel);
        if (row != null && sel.Kind is PK.Belt or PK.EndFore or PK.EndAft && PE.Has(d, Part.Belt))
        {
            float xa = sel.Kind == PK.Belt ? pp.X0 : sel.Kind == PK.EndFore ? pp.X1 : -pp.L / 2;
            float xb = sel.Kind == PK.Belt ? pp.X1 : sel.Kind == PK.EndFore ? pp.L / 2 : pp.X0;
            float hx = m.X(xa) + MathF.Min(P(70), (m.X(xb) - m.X(xa)) * 0.25f);
            H(DragKind.BeltTop, 0, new Vector2(hx, m.Z(pp.BeltTop)), false, pp.BeltTip);
            H(DragKind.BeltBottom, 0, new Vector2(hx, m.Z(pp.BeltBottom)), false, pp.BeltTip);
        }
        if (sel.Kind is PK.EndFore or PK.EndAft)
        {
            bool fore = sel.Kind == PK.EndFore;
            float tipX = fore ? pp.EndTipFore : pp.EndTipAft;
            if (!float.IsNaN(tipX))
                H(DragKind.Reach, fore ? 0 : 1, new Vector2(m.X(tipX), (m.Z(pp.BeltTop) + m.Z(pp.BeltBottom)) / 2), true,
                    fore ? "Drag the tip: the end belt's reach, in tenths of the way to the stem" : "Drag the tip: the end belt's reach, in tenths of the way to the stern");
        }
        if (sel.Kind == PK.Upper && !float.IsNaN(pp.UpTop))
            H(DragKind.UpperTop, 0, new Vector2(m.X(pp.UpX0) + MathF.Min(P(70), (m.X(pp.UpX1) - m.X(pp.UpX0)) * 0.25f), m.Z(pp.UpTop)), false,
                "Drag the top edge: the upper belt up to another deck");
        if (sel.Kind == PK.Deck && sel.I >= 0)
            foreach (var pl in pp.Plates)
                if (pl.Flat && pl.Ref == sel)
                {
                    H(DragKind.DeckMove, sel.I, new Vector2((m.X(pl.X0) + m.X(pl.X1)) / 2, m.Z(pl.Z0) + P(2)), false, "Drag the deck to another deck");
                    break;
                }
        // a drag in progress: the snapped edge under the mouse, applied once on release
        if (protDrag is DragKind.BeltTop or DragKind.BeltBottom or DragKind.Reach or DragKind.UpperTop or DragKind.DeckMove)
        {
            var io = ImGui.GetIO();
            double v = protDrag switch
            {
                DragKind.BeltTop => Math.Clamp(Math.Round((m.InvZ(io.MousePos.Y) - pp.Wl) * 10) / 10, 0.3, -pp.Wl),
                DragKind.BeltBottom => Math.Clamp(Math.Round((pp.Wl - m.InvZ(io.MousePos.Y)) * 10) / 10, 0, pp.Wl - pp.Ib - 0.2),
                DragKind.Reach => Math.Clamp(Math.Round(10 * (protDragI == 0 ? (m.InvX(io.MousePos.X) - pp.X1) / (pp.L / 2 - pp.X1)
                    : (pp.X0 - m.InvX(io.MousePos.X)) / (pp.X0 + pp.L / 2))) / 10, 0.1, 1),
                DragKind.UpperTop => NearestDeck(pp, m.InvZ(io.MousePos.Y), upper: true),
                _ => NearestDeck(pp, m.InvZ(io.MousePos.Y), upper: false),
            };
            protDragValue = v;
            uint brass = T.U32(new Vector4(1, 0.906f, 0.659f, 1));
            if (protDrag == DragKind.Reach)
            {
                float x = protDragI == 0 ? pp.X1 + (float)v * (pp.L / 2 - pp.X1) : pp.X0 - (float)v * (pp.X0 + pp.L / 2);
                dl.AddLine(new Vector2(m.X(x), m.Z(pp.BeltTop)), new Vector2(m.X(x), m.Z(pp.BeltBottom)), brass, P(2));
            }
            else
            {
                float z = protDrag switch
                {
                    DragKind.BeltTop => pp.Wl + (float)v,
                    DragKind.BeltBottom => pp.Wl - (float)v,
                    _ => DeckZOf(pp, (long)v),
                };
                float xa = protDrag == DragKind.DeckMove ? -pp.L / 2 : pp.X0, xb = protDrag == DragKind.DeckMove ? pp.L / 2 : pp.X1;
                dl.AddLine(new Vector2(m.X(xa), m.Z(z)), new Vector2(m.X(xb), m.Z(z)), brass, P(2));
            }
            DragTip(pp);
            EndDragIfReleased();
        }
        return hot;
    }

    static void Handle(ImDrawListPtr dl, Vector2 at, bool horizontal, bool on)
    {
        var half = horizontal ? new Vector2(P(3), P(7)) : new Vector2(P(7), P(3));
        dl.AddRectFilled(at - half, at + half, T.U32(on ? new Vector4(1, 0.906f, 0.659f, 1) : T.Brass), P(1.5f));
        dl.AddRect(at - half, at + half, T.U32(T.Ground), P(1.5f));
        if (horizontal)
            dl.AddLine(at - new Vector2(0, P(4)), at + new Vector2(0, P(4)), T.U32(T.BrassDark));
        else
            dl.AddLine(at - new Vector2(P(4), 0), at + new Vector2(P(4), 0), T.U32(T.BrassDark));
    }

    static float DeckZOf(ProtPane pp, long n)
    {
        foreach (var c in pp.DeckChoices)
            if (c.N == n)
                return c.Z;
        return 0;
    }

    /// <summary>The deck nearest height z: any of the stack for an armour deck, one over the belt and no lower than the
    /// main deck for the upper belt's top.</summary>
    static double NearestDeck(ProtPane pp, float z, bool upper)
    {
        long best = 0;
        float bd = float.MaxValue;
        foreach (var c in pp.DeckChoices)
            if ((upper ? c.N <= 0 && c.Z > pp.BeltTop + 0.3f : c.N >= 0) && Math.Abs(c.Z - z) < bd)
                (best, bd) = (c.N, Math.Abs(c.Z - z));
        return best;
    }

    void StartDrag(DragKind kind, int i)
    {
        protDrag = kind;
        protDragI = i;
        protDragValue = double.NaN;
        protDragTipFor = double.NaN;
    }

    void DragTip(ProtPane pp)
    {
        if (double.IsNaN(protDragValue))
            return;
        if (protDragValue != protDragTipFor)   // the text only when the snapped value changes
        {
            protDragTipFor = protDragValue;
            protDragTip = protDrag switch
            {
                DragKind.BeltTop => $"Belt top {protDragValue:+0.0} m over the waterline",
                DragKind.BeltBottom => $"Belt bottom {protDragValue:0.0} m under the waterline",
                DragKind.Reach => $"End belt reach {protDragValue:0.0}" + (protDragValue >= 0.995 ? ": to the end" : $": {protDragValue * 100:0} % of the way"),
                DragKind.UpperTop => $"Upper belt up to the {Decks.DeckNameLower((long)protDragValue)}",
                DragKind.DeckMove => $"Armour deck on the {Decks.DeckNameLower((long)protDragValue)}",
                _ => $"Torpedo protection {protDragValue:0.0} m deep",
            } + "\nlet go to apply";
        }
        Tip(protDragTip);
    }

    /// <summary>On release, the drag's one edit (none if the value didn't move).</summary>
    void EndDragIfReleased()
    {
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            return;
        var kind = protDrag;
        protDrag = DragKind.None;
        if (double.IsNaN(protDragValue))
            return;
        var d = doc.Current;
        NumberKnob k = kind switch
        {
            DragKind.BeltTop => Knobs.BeltHeight,
            DragKind.BeltBottom => Knobs.BeltDepth,
            DragKind.Reach => Knobs.EndBeltReach(protDragI == 0 ? "fore" : "aft"),
            DragKind.UpperTop => Knobs.UpperBeltToDeck,
            DragKind.DeckMove => Knobs.DeckLevel(protDragI),
            _ => Knobs.Tds,
        };
        if (k.Value(d, shown?.Ship) is { } now && Math.Abs(now - protDragValue) < 1e-6)
            return;
        long deck = (long)protDragValue;
        if (kind == DragKind.DeckMove)
            Edit(k.Apply(d, deck), $"Armour deck {protDragI + 1} moved to the {Decks.DeckNameLower(deck)}");
        else if (kind == DragKind.UpperTop)
            Edit(k.Apply(d, deck), $"Upper belt up to the {Decks.DeckNameLower(deck)}");
        else
            SetValue(k, protDragValue);
        doc.Seal();
    }

    void AddFromGhost(PGhost g)
    {
        var d = doc.Current;
        switch (g.Ref.Kind)
        {
            case PK.Deck:
                Edit(PE.AddDeck(d, g.Deck, g.Extent, 25, out int i), $"Armour deck added: {Decks.DeckNameLower(g.Deck)}, {PE.ExtentText(g.Extent)}");
                protSel = new PRef(PK.Deck, i);
                break;
            default:
                AddPart(PartOf(g.Ref.Kind));
                break;
        }
    }

    static Part PartOf(PK k) => k switch
    {
        PK.Belt => Part.Belt,
        PK.Upper => Part.Upper,
        PK.EndFore => Part.EndFore,
        PK.EndAft => Part.EndAft,
        PK.Bulkheads => Part.Bulkheads,
        PK.Tds => Part.Tds,
        _ => Part.Steering,
    };

    static PK KindOf(Part p) => p switch
    {
        Part.Belt => PK.Belt,
        Part.Upper => PK.Upper,
        Part.EndFore => PK.EndFore,
        Part.EndAft => PK.EndAft,
        Part.Bulkheads => PK.Bulkheads,
        Part.Tds => PK.Tds,
        _ => PK.Steering,
    };

    void AddPart(Part p)
    {
        Edit(PE.Add(doc.Current, p), $"{PE.Name(p)} added");
        protSel = new PRef(KindOf(p));
    }

    void RemovePart(PRef r)
    {
        var d = doc.Current;
        if (r.Kind == PK.Deck)
            Edit(PE.RemoveDeck(d, r.I), $"Armour deck {r.I + 1} removed");
        else
            Edit(PE.Remove(d, PartOf(r.Kind)), $"{PE.Name(PartOf(r.Kind))} removed");
        if (protSel == r || r.Kind == PK.Deck && protSel.Kind == PK.Deck)
            protSel = new PRef(PK.Belt);
    }

    static ProtRow? ProtRowOf(ProtPane pp, PRef r)
    {
        foreach (var row in pp.Rows)
            if (row.Ref == r)
                return row;
        return null;
    }

    static string PlateTip(ProtRow row, PRef r) =>
        $"{row.Name} {row.Thick?.Value ?? row.ThickText}" + (row.T is "turret" or "–" ? "" : $" · {row.T} t") +
        (r.Kind is PK.Barbettes or PK.Ct ? "\n" + row.WhereText : "\nclick: select · wheel: one step · right-click: more");

    // ------------------------------------------------------------------ the midship section

    void ProtSection(ImDrawListPtr dl, Vector2 pos, Vector2 size, ProtPane pp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Plan));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "MIDSHIP SECTION", $"to scale, plates ×3");
        if (pp.SecZ.Length < 2)
            return;
        var io = ImGui.GetIO();
        var font = UiFonts.SansSmall;
        const float th = 3;   // plate thickness stretch
        float chipsW = P(122), top = P(24), bottom = P(10);
        float zHi = 1.0f, zLo = pp.SecZ[0] - 0.3f;
        float k = MathF.Min((size.X - chipsW - P(56)) / pp.B, (size.Y - top - bottom) / (zHi - zLo));
        float cx = pos.X + P(46) + pp.B / 2 * k;
        float oy = pos.Y + top + ((size.Y - top - bottom) - (zHi - zLo) * k) / 2;
        float Y(float z) => oy + (zHi - z) * k;
        float Half(float z)
        {
            var zs = pp.SecZ;
            if (z <= zs[0])
                return pp.SecY[0];
            for (int i = 1; i < zs.Length; i++)
                if (z <= zs[i])
                    return pp.SecY[i - 1] + (pp.SecY[i] - pp.SecY[i - 1]) * (z - zs[i - 1]) / MathF.Max(1e-4f, zs[i] - zs[i - 1]);
            return pp.SecY[^1];
        }
        // the hull
        uint fill = T.U32(new Vector4(0.10f, 0.176f, 0.267f, 1)), line = T.U32(new Vector4(0.435f, 0.576f, 0.71f, 1));
        for (int i = 0; i + 1 < pp.SecZ.Length; i++)
        {
            float y0 = Y(pp.SecZ[i]), y1 = Y(pp.SecZ[i + 1]), h0 = pp.SecY[i] * k, h1 = pp.SecY[i + 1] * k;
            dl.AddQuadFilled(new Vector2(cx - h0, y0), new Vector2(cx + h0, y0), new Vector2(cx + h1, y1), new Vector2(cx - h1, y1), fill);
            dl.AddLine(new Vector2(cx - h0, y0), new Vector2(cx - h1, y1), line);
            dl.AddLine(new Vector2(cx + h0, y0), new Vector2(cx + h1, y1), line);
        }
        dl.AddLine(new Vector2(cx - pp.SecY[^1] * k, Y(0)), new Vector2(cx + pp.SecY[^1] * k, Y(0)), line);
        // decks, named
        uint lane = T.U32(new Vector4(0.61f, 0.76f, 0.886f, 0.25f));
        foreach (var (name, z, x0, x1) in pp.Lanes)
        {
            if (z > 0.01f || z < pp.SecZ[0])
                continue;
            float hw = Half(z) * k;
            Dashed(dl, new Vector2(cx - hw, Y(z)), new Vector2(cx + hw, Y(z)), lane, P(3));
            Text(dl, font, new Vector2(cx - pp.B / 2 * k - P(6) - Width(font, name), Y(z) - font.FontSize / 2), new Vector4(0.5f, 0.61f, 0.72f, 1), name);
        }
        dl.AddLine(new Vector2(cx - pp.B / 2 * k - P(8), Y(pp.Wl)), new Vector2(cx + pp.B / 2 * k + P(8), Y(pp.Wl)), T.U32(T.Blue));
        Text(dl, font, new Vector2(pos.X + P(4), Y(pp.Wl) - font.FontSize / 2), T.Blue, "WL");

        var mouse = io.MousePos;
        ImGui.SetCursorScreenPos(pos + new Vector2(1, P(18)));
        ImGui.SetNextItemAllowOverlap();
        ImGui.InvisibleButton("##sect", new Vector2(size.X - chipsW - P(4), size.Y - P(18) - 1), ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        bool hover = ImGui.IsItemHovered(), clickL = ImGui.IsItemClicked(ImGuiMouseButton.Left), clickR = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        if (hover)
            ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY);
        PRef? hot = null;

        // torpedo protection: the holding bulkhead tds in from the shell, inner bottom to the roof deck, hatched
        if (pp.Tds > 0)
        {
            bool lit = ProtLit(new PRef(PK.Tds));
            uint hatch = T.U32(T.Blue with { W = lit ? 0.45f : 0.25f });
            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = pp.Ib; z < pp.RoofZ; z += 0.5f)
                {
                    float z1 = MathF.Min(pp.RoofZ, z + 0.5f);
                    float o0 = Half(z) * k, i0 = MathF.Max(0.5f, Half(z) - pp.Tds) * k;
                    for (float t = 0; t < o0 - i0; t += P(5))
                        dl.AddLine(new Vector2(cx + side * (i0 + t), Y(z)), new Vector2(cx + side * MathF.Min(o0, i0 + t + (Y(z) - Y(z1))), Y(z1)), hatch, P(1.5f));
                }
                float zm = (pp.Ib + pp.RoofZ) / 2, ix = cx + side * MathF.Max(0.5f, Half(zm) - pp.Tds) * k;
                Dashed(dl, new Vector2(ix, Y(pp.RoofZ)), new Vector2(ix, Y(pp.Ib)), T.U32(new Vector4(0.5f, 0.69f, 0.88f, 1)), P(3), vertical: true);
                if (hover && Math.Abs(mouse.X - ix) < P(5) && mouse.Y > Y(pp.RoofZ) && mouse.Y < Y(pp.Ib))
                    hot = new PRef(PK.Tds);
            }
        }
        // armour decks across
        foreach (var (z, mm, r, label) in pp.SecDecks)
        {
            float hw = Half(z) * k, t = MathF.Max(P(2), (float)mm / 1000 * k * th);
            bool lit = ProtLit(r);
            var a = new Vector2(cx - hw, Y(z));
            var b = new Vector2(cx + hw, Y(z) + t);
            dl.AddRectFilled(a, b, T.U32(T.Armour(mm)));
            dl.AddRect(a, b, T.U32(lit ? new Vector4(1, 0.906f, 0.659f, 0.95f) : new Vector4(0, 0, 0, 0.5f)));
            float lw = Width(font, label) + P(6);
            dl.AddRectFilled(new Vector2(cx - lw / 2, b.Y + P(1)), new Vector2(cx + lw / 2, b.Y + P(2) + font.FontSize), T.U32(new Vector4(0.055f, 0.07f, 0.082f, 0.82f)), P(2));
            Text(dl, font, new Vector2(cx - lw / 2 + P(3), b.Y + P(1)), T.Armour(mm), label);
            if (hover && Inside(mouse, a - new Vector2(0, P(4)), b + new Vector2(0, P(4))))
                hot = r;
        }
        // belt and upper belt on both sides, the belt tapering below the waterline
        foreach (var (z0, z1, mm, lower, r) in pp.SecSide)
        {
            bool lit = ProtLit(r);
            for (int side = -1; side <= 1; side += 2)
            {
                const int n = 10;
                for (int i = 0; i < n; i++)
                {
                    float za = z0 + (z1 - z0) * i / n, zb = z0 + (z1 - z0) * (i + 1) / n;
                    double ma = za >= pp.Wl ? mm : lower + (mm - lower) * (za - z0) / MathF.Max(0.01f, pp.Wl - z0);
                    double mb = zb >= pp.Wl ? mm : lower + (mm - lower) * (zb - z0) / MathF.Max(0.01f, pp.Wl - z0);
                    float oa = Half(za) * k, ob = Half(zb) * k;
                    float ia = oa - MathF.Max(P(2), (float)ma / 1000 * k * th), ib = ob - MathF.Max(P(2), (float)mb / 1000 * k * th);
                    dl.AddQuadFilled(new Vector2(cx + side * oa, Y(za)), new Vector2(cx + side * ob, Y(zb)), new Vector2(cx + side * ib, Y(zb)),
                        new Vector2(cx + side * ia, Y(za)), T.U32(T.Armour((ma + mb) / 2)));
                }
                if (lit)
                    dl.AddLine(new Vector2(cx + side * Half(z0) * k, Y(z0)), new Vector2(cx + side * Half(z1) * k, Y(z1)), T.U32(new Vector4(1, 0.906f, 0.659f, 0.95f)), P(1.5f));
                float hx = cx + side * Half((z0 + z1) / 2) * k;
                if (hover && Math.Abs(mouse.X - hx) < P(8) && mouse.Y >= Y(z1) && mouse.Y <= Y(z0))
                    hot = r;
            }
        }

        // the belt's edges and the holding bulkhead as handles; the same values as spinners beside them
        bool beltOn = false;
        foreach (var s in pp.SecSide)
            beltOn |= s.R.Kind == PK.Belt;
        int forced = protForceTip is { } ft && ft.StartsWith("sechandle:", StringComparison.Ordinal) ? int.Parse(ft[10..], CultureInfo.InvariantCulture) : -1;
        var handleHot = DragKind.None;
        void H(DragKind kind, Vector2 at, bool horizontal, string tip, int n)
        {
            bool on = hover && Vector2.Distance(mouse, at) <= P(8) && protDrag == DragKind.None;
            Handle(dl, at, horizontal, on || protDrag == kind && protDragI == 9);
            if (on)
            {
                handleHot = kind;
                Tip(tip);
                if (clickL)
                {
                    StartDrag(kind, 9);
                    protDragI = 9;   // the section's drag
                }
            }
            if (n == forced)
                TipAt(tip, at + new Vector2(P(10), P(8)));
        }
        if (beltOn)
        {
            H(DragKind.BeltTop, new Vector2(cx + Half(pp.BeltTop) * k - P(4), Y(pp.BeltTop)), false, pp.BeltTip, 0);
            H(DragKind.BeltBottom, new Vector2(cx + Half(pp.BeltBottom) * k - P(4), Y(pp.BeltBottom)), false, pp.BeltTip, 1);
        }
        if (pp.Tds > 0)
        {
            float zm = (pp.Ib + pp.RoofZ) / 2;
            H(DragKind.TdsDepth, new Vector2(cx + MathF.Max(0.5f, Half(zm) - pp.Tds) * k, Y(zm)), true, pp.TdsTip, 2);
        }
        if (protDrag != DragKind.None && protDragI == 9)
        {
            uint brass = T.U32(new Vector4(1, 0.906f, 0.659f, 1));
            float zAt = zHi - (mouse.Y - oy) / k;
            if (protDrag == DragKind.TdsDepth)
            {
                float zm = (pp.Ib + pp.RoofZ) / 2;
                protDragValue = Math.Clamp(Math.Round((Half(zm) - (mouse.X - cx) / k) * 10) / 10, 0.5, pp.B / 2 - 2);
                float ix = cx + MathF.Max(0.5f, Half(zm) - (float)protDragValue) * k;
                dl.AddLine(new Vector2(ix, Y(pp.RoofZ)), new Vector2(ix, Y(pp.Ib)), brass, P(2));
            }
            else
            {
                protDragValue = protDrag == DragKind.BeltTop ? Math.Clamp(Math.Round((zAt - pp.Wl) * 10) / 10, 0.3, -pp.Wl)
                    : Math.Clamp(Math.Round((pp.Wl - zAt) * 10) / 10, 0, pp.Wl - pp.Ib - 0.2);
                float z = protDrag == DragKind.BeltTop ? pp.Wl + (float)protDragValue : pp.Wl - (float)protDragValue;
                dl.AddLine(new Vector2(cx - Half(z) * k, Y(z)), new Vector2(cx + Half(z) * k, Y(z)), brass, P(2));
            }
            DragTip(pp);
            EndDragIfReleased();
        }
        else if (hot is { } h && handleHot == DragKind.None && protDrag == DragKind.None)
        {
            protHotNext = h;
            if (ProtRowOf(pp, h) is { } row)
                Tip(row.Hover);
            if (clickL)
            {
                protSel = h;
                stale = true;
            }
            if (clickR)
                (protSel, protMenuFor, openProtMenu) = (h, h, true);
            if (io.MouseWheel != 0 && ThickKnob(h) is { } kk)
                Step(Row(kk, doc.Current, shown?.Ship), Math.Sign(io.MouseWheel) * (io.KeyShift ? 4 : 1));
        }

        // the spinners, at the right edge
        float sxp = pos.X + size.X - chipsW;
        void Chip(NumRow r, float y, string pre)
        {
            Text(dl, font, new Vector2(sxp, y + (ImGui.GetFrameHeight() - font.FontSize) / 2), T.Muted, pre);
            ImGui.SetCursorScreenPos(new Vector2(sxp + P(18), y));
            Spinner(r, P(54), extras: false);
        }
        if (beltOn)
        {
            float fh = ImGui.GetFrameHeight();
            Chip(pp.BeltHeight, Y(pp.BeltTop) - fh / 2, "↑");
            Chip(pp.BeltDepth, MathF.Max(Y(pp.BeltBottom) - fh / 2, Y(pp.BeltTop) + fh / 2 + P(2)), "↓");
            dl.AddLine(new Vector2(sxp - P(6), Y(pp.BeltTop)), new Vector2(sxp - P(6), Y(pp.BeltBottom)), T.U32(T.BrassDark));
        }
        if (pp.Tds > 0)
            Chip(pp.TdsRow, Y((pp.Ib + pp.RoofZ) / 2) + P(4), "TDS");
    }

    // ------------------------------------------------------------------ the plate table

    static readonly float[] ProtColW = [12, 124, 100, 124, 0, 132, 56, 96, 22];   // 0: "where" takes what's left
    readonly float[] protX = new float[ProtColW.Length + 1];
    static readonly string[] ProtHead = ["", "PLATE", "THICKNESS", "LOWER · TIP · ROOF", "WHERE", "MATERIAL", "T", "OF THE ARMOUR", ""];

    void ProtTable(ImDrawListPtr dl, Vector2 o, Vector2 size, ProtPane pp)
    {
        dl.AddRectFilled(o, o + size, T.U32(T.Panel2));
        dl.AddRect(o, o + size, T.U32(T.Edge));
        float w = size.X;
        var x = protX;
        float fixedW = 0;
        foreach (var c in ProtColW)
            fixedW += P(c);
        x[0] = o.X;
        for (int i = 0; i < ProtColW.Length; i++)
            x[i + 1] = x[i] + (ProtColW[i] > 0 ? P(ProtColW[i]) : Math.Max(P(200), w - fixedW));
        float hh = P(17), rowH = ProtRowH, gh = P(15);
        var font = UiFonts.SansSmall;
        dl.AddRectFilled(o, o + new Vector2(w, hh), T.U32(T.HeaderBg));
        dl.AddLine(o + new Vector2(0, hh - 1), o + new Vector2(w, hh - 1), T.U32(T.Edge));
        for (int i = 0; i < ProtHead.Length; i++)
        {
            if (ProtHead[i].Length == 0)
                continue;
            float tx = i is 2 or 3 or 6 ? x[i + 1] - P(6) - Width(font, ProtHead[i]) : x[i] + P(4);
            Text(dl, font, new Vector2(tx, o.Y + (hh - font.FontSize) / 2), T.Muted, ProtHead[i]);
        }
        var d = doc.Current;
        float y = o.Y + hh;
        float fh = ImGui.GetFrameHeight();
        int n = 0;
        for (int g = 0; g < 4; g++)
        {
            // the group's header, with its + chips
            dl.AddRectFilled(new Vector2(o.X, y), new Vector2(o.X + w, y + gh), T.U32(new Vector4(0.035f, 0.047f, 0.055f, 0.35f)));
            dl.AddLine(new Vector2(o.X, y + gh - 1), new Vector2(o.X + w, y + gh - 1), T.U32(T.Edge));
            Text(dl, font, new Vector2(o.X + P(6), y + (gh - font.FontSize) / 2), new Vector4(0.43f, 0.47f, 0.51f, 1), GroupTitle[g]);
            float cxp = o.X + P(12) + Width(font, GroupTitle[g]);
            ImGui.PushID(500 + g);
            for (int c = 0; c < pp.Chips[g].Length; c++)
            {
                var chip = pp.Chips[g][c];
                float cw = Width(font, chip.Label) + P(10);
                var a = new Vector2(cxp, y + P(1.5f));
                var b = a + new Vector2(cw, gh - P(3));
                ImGui.SetCursorScreenPos(a);
                ImGui.PushID(c);
                bool click = ImGui.InvisibleButton("##chip", b - a);
                bool hov = ImGui.IsItemHovered();
                ImGui.PopID();
                dl.AddRectFilled(a, b, T.U32(T.Brass with { W = hov ? 0.16f : 0.05f }), P(2));
                dl.AddRect(a, b, T.U32(hov ? T.Brass : T.BrassDark), P(2));
                Text(dl, font, new Vector2(a.X + P(5), a.Y + (b.Y - a.Y - font.FontSize) / 2), hov ? new Vector4(1, 0.906f, 0.659f, 1) : T.Brass, chip.Label);
                if (hov)
                    Tip(chip.Tip);
                if (click)
                {
                    if (chip.Part is { } part)
                        AddPart(part);
                    else
                    {
                        Edit(PE.AddDeck(d, chip.Deck, "citadel", 25, out int i), $"Armour deck added: {Decks.DeckNameLower(chip.Deck)}, citadel");
                        protSel = new PRef(PK.Deck, i);
                    }
                }
                cxp = b.X + P(5);
            }
            ImGui.PopID();
            if (g == 3)
                Text(dl, font, new Vector2(cxp + P(4), y + (gh - font.FontSize) / 2), T.Muted, "turret faces also on the Main battery tab; secondary shields on Secondary");
            y += gh;
            foreach (var r in pp.Rows)
            {
                if (r.Group != g)
                    continue;
                ProtTableRow(dl, new Vector2(o.X, y), w, rowH, fh, r, n++);
                y += rowH;
            }
        }
    }

    void ProtTableRow(ImDrawListPtr dl, Vector2 p, float w, float rowH, float fh, ProtRow r, int n)
    {
        var x = protX;
        var d = doc.Current;
        var font = UiFonts.SansSmall;
        bool on = r.Ref == protSel;
        ImGui.PushID(2000 + (int)r.Ref.Kind * 64 + r.Ref.I);
        ImGui.SetCursorScreenPos(p);
        ImGui.SetNextItemAllowOverlap();
        if (ImGui.InvisibleButton("##row", new Vector2(w, rowH), ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight))
        {
            protSel = r.Ref;
            stale = true;
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            (protSel, protMenuFor, openProtMenu) = (r.Ref, r.Ref, true);
        bool hover = ImGui.IsItemHovered() || protHot == r.Ref;
        if (ImGui.IsItemHovered())
            protHotNext = r.Ref;
        RowBg(dl, p, w, n);
        if (on)
        {
            dl.AddRectFilled(p, p + new Vector2(w, rowH), T.U32(T.Brass with { W = hover ? 0.13f : 0.10f }));
            dl.AddRectFilled(p, p + new Vector2(P(3), rowH), T.U32(T.Brass));
        }
        else if (hover)
            dl.AddRectFilled(p, p + new Vector2(w, rowH), T.U32(new Vector4(1, 1, 1, 0.045f)));
        float cy = p.Y + (rowH - fh) / 2, ty = p.Y + (rowH - UiFonts.Sans.FontSize) / 2, tys = p.Y + (rowH - font.FontSize) / 2;
        void At(int col, float dx = 4) => ImGui.SetCursorScreenPos(new Vector2(x[col] + P(dx), cy));

        var swp = new Vector2(x[0] + P(4), p.Y + (rowH - P(11)) / 2);
        dl.AddRectFilled(swp, swp + new Vector2(P(7), P(11)), T.U32(r.Swatch));
        Text(dl, UiFonts.Sans, new Vector2(x[1] + P(4), ty), on ? T.Brass : T.Bright, r.Name);
        if (r.Thick is { } th)
        {
            At(2);
            Spinner(th, P(52), extras: false);
        }
        else
            Text(dl, UiFonts.Mono, new Vector2(x[3] - P(22) - Width(UiFonts.Mono, r.ThickText), p.Y + (rowH - UiFonts.Mono.FontSize) / 2), T.Muted, r.ThickText);
        if (r.Second is { } s2)
        {
            Text(dl, font, new Vector2(x[3] + P(4), tys), T.Muted, r.SecondPre);
            ImGui.SetCursorScreenPos(new Vector2(x[4] - P(4) - P(48) - 2 * fh, cy));
            Spinner(s2, P(48), extras: false);
        }

        // where
        float wx = x[4] + P(4);
        if (r.WhereA is { } wa)
        {
            Text(dl, font, new Vector2(wx, tys), T.Muted, r.WhereAPre);
            wx += Width(font, r.WhereAPre) + P(4);
            ImGui.SetCursorScreenPos(new Vector2(wx, cy));
            Spinner(wa, P(46), extras: false);
            wx = ImGui.GetItemRectMax().X + P(8);
        }
        if (r.WhereB is { } wb)
        {
            Text(dl, font, new Vector2(wx, tys), T.Muted, "↑");
            wx += Width(font, "↑") + P(4);
            ImGui.SetCursorScreenPos(new Vector2(wx, cy));
            Spinner(wb, P(46), extras: false);
            wx = ImGui.GetItemRectMax().X + P(8);
        }
        if (r.DeckKnob is { } dk)
        {
            ImGui.SetCursorScreenPos(new Vector2(wx, cy));
            ImGui.SetNextItemWidth(P(64));
            ImGui.PushFont(font);
            if (ImGui.BeginCombo("##deck", r.DeckLabel))
            {
                var pp = snap!.Prot!;
                foreach (var c in pp.DeckChoices)
                {
                    if (r.Ref.Kind == PK.Upper && (c.N > 0 || c.Z <= pp.BeltTop + 0.3f))
                        continue;
                    if (ImGui.Selectable(c.Name, c.N == r.DeckNow) && c.N != r.DeckNow)
                    {
                        SetValue(dk, c.N);
                        doc.Seal();
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.PopFont();
            wx = ImGui.GetItemRectMax().X + P(6);
        }
        if (r.Extent is { } ek)
        {
            ImGui.SetCursorScreenPos(new Vector2(wx, cy));
            ImGui.PushFont(font);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(4), ImGui.GetStyle().FramePadding.Y));
            for (int i = 0; i < ExtentValues.Length; i++)
            {
                if (i > 0)
                    ImGui.SameLine();
                if (Seg(ExtentShort[i], r.ExtentNow == ExtentValues[i]) && r.ExtentNow != ExtentValues[i])
                    Edit(ek.Set(d, ExtentValues[i]), $"{r.Name}: over the {PE.ExtentText(ExtentValues[i])}");
                if (ImGui.IsItemHovered())
                    Tip($"{(r.Ref.Kind == PK.Upper ? "Upper belt" : "This deck")} over the {PE.ExtentText(ExtentValues[i])}");
            }
            ImGui.PopStyleVar(2);
            ImGui.PopFont();
            wx = ImGui.GetItemRectMax().X + P(8);
        }
        if (r.WhereText.Length > 0)
            Text(dl, font, new Vector2(wx, tys), T.Muted, r.WhereText);

        // material
        if (r.Material is { } mat)
        {
            At(5);
            ImGui.SetNextItemWidth(P(124));
            ImGui.PushFont(font);
            int mi = mat.Index;
            if (ImGui.Combo("##mat", ref mi, mat.Labels, mat.Labels.Length) && mi < mat.Knob.Values.Length && mi != mat.Index)
                Edit(mat.Knob.Set(d, mat.Knob.Values[mi]), $"{r.Name} material: {mat.Labels[mi]}");
            ImGui.PopFont();
        }
        else
            Text(dl, font, new Vector2(x[5] + P(6), tys), T.Muted, r.MaterialText);

        // weight and its share
        var tp = new Vector2(x[7] - P(6) - Width(UiFonts.Mono, r.T), p.Y + (rowH - UiFonts.Mono.FontSize) / 2);
        Text(dl, UiFonts.Mono, tp, r.T == "turret" ? T.Muted : T.Bright, r.T);
        if (ImGui.IsMouseHoveringRect(new Vector2(x[6], p.Y), new Vector2(x[7], p.Y + rowH)))
            Tip(r.WeightTip);
        if (r.Share > 0)
        {
            var bp = new Vector2(x[7] + P(4), p.Y + (rowH - P(6)) / 2);
            Bar(dl, bp, new Vector2(P(50), P(6)), Math.Min(1, r.Share * 2), T.Group("armour"));
            Text(dl, UiFonts.Mono, new Vector2(bp.X + P(56), p.Y + (rowH - UiFonts.Mono.FontSize) / 2), T.Muted, PctText(r.Share));
        }
        if (r.Removable)
        {
            At(8, 2);
            ImGui.PushFont(font);
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Text, T.Muted);
            if (ImGui.SmallButton("x"))
                RemovePart(r.Ref);
            ImGui.PopStyleColor(2);
            ImGui.PopFont();
            if (ImGui.IsItemHovered())
                Tip($"Remove the {r.Name.ToLowerInvariant()}: it comes back as a + in its group, and on the elevation");
        }
        ImGui.PopID();
    }

    static readonly string[] PctCache = Enumerable.Range(0, 101).Select(i => $"{i} %").ToArray();
    static string PctText(float share) => PctCache[Math.Clamp((int)MathF.Round(share * 100), 0, 100)];

    // ------------------------------------------------------------------ the selected plate

    void ProtInspector(ImDrawListPtr dl, Vector2 pos, Vector2 size, ProtPane pp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Panel2));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "SELECTED PLATE", "drag its brass handles on the drawings");
        var r = ProtRowOf(pp, protSel);
        float x = pos.X + P(8), y = pos.Y + P(22);
        float ih = P(64);
        ImGui.PushClipRect(pos, new Vector2(pos.X + size.X, pos.Y + size.Y - ih - P(8)), true);
        if (r != null)
        {
            dl.AddRectFilled(new Vector2(x, y + P(2)), new Vector2(x + P(8), y + P(13)), T.U32(r.Swatch));
            Text(dl, UiFonts.SansBold, new Vector2(x + P(14), y), T.Bright, r.Name);
            string th = r.Thick?.Value ?? r.ThickText;
            Text(dl, UiFonts.Sans, new Vector2(x + P(22) + Width(UiFonts.SansBold, r.Name), y), T.Muted, th);
            y += P(20);
            foreach (var (label, value) in r.Lines)
            {
                Text(dl, UiFonts.Sans, new Vector2(x, y), T.Muted, label);
                Text(dl, UiFonts.Mono, new Vector2(x + P(82), y + P(1)), T.Text, value);
                y += P(17);
            }
            y += P(4);
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            ImGui.PushTextWrapPos(pos.X + size.X - P(8) - ImGui.GetWindowPos().X);   // window-local
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TextColored(T.Muted, r.Note);
            ImGui.PopFont();
            ImGui.PopTextWrapPos();
        }
        ImGui.PopClipRect();
        // the immune zone: a greyed placeholder until the penetration model
        var a = new Vector2(pos.X + P(8), pos.Y + size.Y - ih - P(6));
        var b = new Vector2(pos.X + size.X - P(8), pos.Y + size.Y - P(6));
        dl.AddRect(a, b, T.U32(new Vector4(0.333f, 0.376f, 0.416f, 1)));
        var font = UiFonts.SansSmall;
        var grey = new Vector4(0.37f, 0.416f, 0.455f, 1);
        Text(dl, font, a + new Vector2(P(6), P(4)), grey, "Immune zone and shells fired in anger");
        string tag = "NEEDS THE PENETRATION MODEL";
        var tg = new Vector2(b.X - P(6) - Width(font, tag) - P(6), a.Y + P(4));
        dl.AddRect(tg - new Vector2(0, P(1)), tg + new Vector2(Width(font, tag) + P(6), font.FontSize + P(1)), T.U32(T.BrassDark));
        Text(dl, font, tg + new Vector2(P(3), 0), new Vector4(0.55f, 0.48f, 0.33f, 1), tag);
        ImGui.SetCursorScreenPos(a + new Vector2(P(6), P(22)));
        ImGui.BeginDisabled();
        ImGui.PushFont(font);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Against");
        ImGui.SameLine(0, P(6));
        ImGui.SetNextItemWidth(P(160));
        if (ImGui.BeginCombo("##immgun", pp.ImmuneGun))
            ImGui.EndCombo();
        ImGui.SetCursorScreenPos(new Vector2(b.X - P(6) - Width(font, "Fire 1,000 shells…") - P(12), a.Y + P(22)));
        ImGui.Button("Fire 1,000 shells…");
        ImGui.PopFont();
        ImGui.EndDisabled();
        Text(dl, font, a + new Vector2(P(6), P(44)), grey, "belt holds beyond — km · deck holds inside — km");
    }

    // ------------------------------------------------------------------ the context menu

    void ProtMenu(ProtPane pp)
    {
        if (openProtMenu)
        {
            ImGui.OpenPopup("##protmenu");
            openProtMenu = false;
        }
        if (!ImGui.BeginPopup("##protmenu"))
            return;
        var d = doc.Current;
        var r = ProtRowOf(pp, protMenuFor);
        if (r == null)
        {
            ImGui.EndPopup();
            return;
        }
        ImGui.TextColored(T.Muted, r.Name);
        ImGui.Separator();
        if (r.Extent is { } ek)
            for (int i = 0; i < ExtentValues.Length; i++)
                if (ImGui.MenuItem($"Over {(ExtentValues[i] == "ends" ? "" : "the ")}{PE.ExtentText(ExtentValues[i])}", r.ExtentNow == ExtentValues[i] ? "now" : null, false,
                        r.ExtentNow != ExtentValues[i]))
                    Edit(ek.Set(d, ExtentValues[i]), $"{r.Name}: over the {PE.ExtentText(ExtentValues[i])}");
        if (r.Ref.Kind == PK.Deck && r.Ref.I < (d.Armour?.Decks?.Count ?? 0))
        {
            ImGui.Separator();
            var stack = pp.DeckChoices.Where(c => c.N >= 0).Select(c => c.N).OrderBy(c => c).ToList();
            int k = stack.IndexOf(r.DeckNow);
            if (ImGui.MenuItem(k > 0 ? $"Up to the {Decks.DeckNameLower(stack[k - 1])}" : "Up a deck", null, false, k > 0))
                Edit(PE.SetDeck(d, r.Ref.I, x => x with { Deck = (int)stack[k - 1] }), $"{r.Name} up to the {Decks.DeckNameLower(stack[k - 1])}");
            if (ImGui.MenuItem(k >= 0 && k + 1 < stack.Count ? $"Down to the {Decks.DeckNameLower(stack[k + 1])}" : "Down a deck", null, false, k >= 0 && k + 1 < stack.Count))
                Edit(PE.SetDeck(d, r.Ref.I, x => x with { Deck = (int)stack[k + 1] }), $"{r.Name} down to the {Decks.DeckNameLower(stack[k + 1])}");
            if (ImGui.MenuItem("Duplicate onto the deck below", null, false, k >= 0 && k + 1 < stack.Count))
            {
                var x = d.Armour!.Decks![r.Ref.I];
                Edit(PE.AddDeck(d, (int)stack[k + 1], x.Extent ?? "citadel", Math.Max(5, Math.Round((x.Mm ?? 0) / 10) * 5), out int i),
                    $"Armour deck added: {Decks.DeckNameLower(stack[k + 1])}");
                protSel = new PRef(PK.Deck, i);
            }
        }
        if (r.Ref.Kind is PK.EndFore or PK.EndAft)
        {
            string end = r.Ref.Kind == PK.EndFore ? "fore" : "aft";
            double mm = d.Armour?.EndBelts?.Of(end)?.Mm ?? 0;
            if (ImGui.MenuItem("Taper to the tip: 2/3 of the root"))
                Edit(Knobs.EndBeltTip(end).Apply(d, Math.Round(mm * 2 / 30) * 10), $"End belt, {end}: tapered");
            if (ImGui.MenuItem("No taper"))
                Edit(Knobs.EndBeltTip(end).Apply(d, mm), $"End belt, {end}: no taper");
        }
        if (r.Removable)
        {
            ImGui.Separator();
            if (ImGui.MenuItem(r.Ref.Kind == PK.Deck ? "Remove this deck" : "Remove", "Del"))
                RemovePart(r.Ref);
        }
        ImGui.EndPopup();
    }

    /// <summary>Del on the Protection tab removes the selected plate.</summary>
    void ProtDelete()
    {
        if (snap?.Prot is { } pp && ProtRowOf(pp, protSel) is { Removable: true } r)
            RemovePart(r.Ref);
    }

    /// <summary>The dev hook's protection states: protsel:ID (belt, upper, end_fore, end_aft, bulk, tds, deckN, steer,
    /// mainN, barb, ct), protmenu, protcover:N, protghost:DECK:EXT, prothandle:N, protsechandle:N.</summary>
    bool ApplyProtDevUi(string[] p)
    {
        switch (p[0])
        {
            case "protsel" when p.Length > 1:
                string id = p[1];
                int num = int.TryParse(new string(id.Where(char.IsDigit).ToArray()), out int v) ? v : 0;
                protSel = id switch
                {
                    "upper" => new PRef(PK.Upper),
                    "end_fore" => new PRef(PK.EndFore),
                    "end_aft" => new PRef(PK.EndAft),
                    "bulk" => new PRef(PK.Bulkheads),
                    "tds" => new PRef(PK.Tds),
                    "steer" => new PRef(PK.Steering),
                    "barb" => new PRef(PK.Barbettes),
                    "ct" => new PRef(PK.Ct),
                    _ when id.StartsWith("deck", StringComparison.Ordinal) => new PRef(PK.Deck, num),
                    _ when id.StartsWith("main", StringComparison.Ordinal) => new PRef(PK.Main, num),
                    _ => new PRef(PK.Belt),
                };
                return true;
            case "protmenu":
                (protMenuFor, openProtMenu) = (protSel, true);
                return true;
            case "protcover" when p.Length > 1:
                protForceTip = $"cover:{p[1]}";
                return true;
            case "protghost" when p.Length > 2:
                protForceTip = $"ghost:{p[1]}:{p[2]}";
                return true;
            case "prothandle" when p.Length > 1:
                protForceTip = $"handle:{p[1]}";
                return true;
            case "protsechandle" when p.Length > 1:
                protForceTip = $"sechandle:{p[1]}";
                return true;
        }
        return false;
    }
}
