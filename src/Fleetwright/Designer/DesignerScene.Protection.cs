using System.Globalization;
using System.Numerics;
using Fleetwright.Shipgen;
using ImGuiNET;
using Part = Fleetwright.Designer.ProtectionEdits.Part;
using PE = Fleetwright.Designer.ProtectionEdits;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Designer;

/// <summary>
/// The Protection tab, after docs/designer-ui/protection.md and protection-layers.md: the elevation (the ship's side deck
/// by deck, heights stretched, every plate where the build put it, + ghosts where a missing part would go, the rooms
/// and the compartments, two flags for the sections' cuts), two half-sections at their own stations to scale
/// (ProtectionSection), the plate table (only the parts the ship has, sub-rows for a part's zone and slopes; a + in each
/// group's header adds a missing one, × removes), the selected plate's facts and Shape, and the sub-bar's coverage
/// presets, layout templates and materials. Drags preview on screen and edit once, on release: a build is expensive.
/// </summary>
public sealed unsafe partial class DesignerScene
{
    /// <summary>What a plate, row or ghost is: a part, an armour deck (I: its index in the design) and its slopes and zone,
    /// a battery's turrets (I: the battery), the barbettes, the conning tower, the flight deck.</summary>
    enum PK { Belt, BeltZone, Upper, EndFore, EndAft, Bulkheads, Tds, Inner, Box, Wings, Deck, Slope, DeckZone, Steering, Main, Barbettes, Ct, FlightDeck }

    readonly record struct PRef(PK Kind, int I = 0);

    enum DragKind { None, BeltTop, BeltBottom, Reach, UpperTop, DeckMove, TdsDepth, Lean, Knee, Wing, InnerWall, Cut }

    PRef protSel = new(PK.Belt);
    PRef? protHot;   // hovered this frame (row or plate): both drawings and the table light it
    PRef? protHotNext;
    PRef protMenuFor;
    bool openProtMenu, openLayoutMenu;
    int openCutMenu = -1;
    DragKind protDrag;
    int protDragI;   // the deck, the end belt (0 fore, 1 aft), the cut, or 9 for the section's drags
    double protDragValue = double.NaN;   // the snapped value under the mouse
    string protDragTip = "";
    double protDragTipFor = double.NaN;
    string? protForceTip;   // dev hook: "cover:N", "ghost:N:EXT", "handle:N", "sechandle:N", "layout:N", "cell:K:N", "comp"
    readonly List<(Vector2 A, Vector2 B, PRef R)> protHits = [];   // this frame's plates on the elevation, topmost last
    bool protRooms = true, protComp;
    // the sections' cuts: a stop's id ("mach", "mag:A", "fore", "aft") or a free station; Whole section locks 2 to 1
    readonly string?[] protCutStop = ["mach", null];
    readonly float[] protCutX = [0, 0];
    bool protLinked;
    (string? Stop, float X) protCut2Saved;
    // the layout template last applied here, and the layout as it left it (an edit after it reads "· edited")
    (int Index, string Key)? protLayoutApplied;

    // the drawings' frame (ship metres) and the top area's height, kept while the ship changes inside them, so a wheel
    // step on a plate doesn't rescale the view: reset for another design or pane size, grown when the ship outgrows it,
    // shrunk only when it's far too big
    object? protFrameDoc;
    Vector2 protFrameSize;
    float protFrameL, protFrameTop, protFrameBot, protFrameB, protTopH;

    static readonly string[] ExtentValues = ["citadel", "full", "fore", "aft", "ends"];
    static readonly string[] ExtentShort = ["cit", "full", "fore", "aft", "ends"];
    static readonly string[] GroupTitle = ["SIDE", "ARMOUR DECKS", "INBOARD: BEHIND THE SIDE", "ENDS", "GUNS AND CONNING TOWER"];
    const int ProtGroups = 5;

    // ------------------------------------------------------------------ the snapshot's part

    /// <summary>A plate on the elevation, in ship metres (z above the main deck): its rectangle, thickness and label.
    /// Flat: an armour deck, drawn a few pixels thick at Z0; Inside: a barbette's part under the deck, dashed; Hatched:
    /// an internal belt, behind the shell; Faint: a band (an internal belt's lower belt).</summary>
    sealed record PPlate(PRef Ref, float X0, float X1, float Z0, float Z1, double Mm, string Label, bool Flat = false, bool Inside = false,
        bool Bulkhead = false, bool Hatched = false, bool Faint = false);

    /// <summary>A + where a missing part or a deck's bare stretch would go.</summary>
    sealed record PGhost(PRef Ref, int Deck, string Extent, float X0, float X1, float Z0, float Z1, string Tip)
    {
        /// <summary>The dev hook's name for it (protghost:DECK:EXT).</summary>
        public string Force { get; } = $"ghost:{Deck}:{Extent}";
    }

    /// <summary>A line of the selected plate's Shape: a choice, a number, a + that adds a zone or slopes, a read-only value
    /// (with its × when it can go), a note or a warning.</summary>
    abstract record ShapeItem(string Label);
    sealed record ShapeSeg(string Label, ChoiceRow Row, string[] Tips) : ShapeItem(Label);
    sealed record ShapeNum(string Label, NumRow Row, string Note) : ShapeItem(Label);
    sealed record ShapeAdd(string Label, string Chip, Action Add, string Tip) : ShapeItem(Label);
    sealed record ShapeRead(string Label, string Text, Action? Remove = null, string RemoveTip = "") : ShapeItem(Label);
    sealed record ShapeNote(string Text, bool Warn = false) : ShapeItem("");

    sealed class ProtRow
    {
        public required PRef Ref { get; init; }
        public int Group { get; init; }
        public required string Name { get; init; }
        public Vector4 Swatch { get; init; }
        public NumRow? Thick { get; init; }
        public string ThickText { get; init; } = "";
        public NumRow? Second { get; init; }
        public string SecondPre { get; init; } = "";
        /// <summary>A read-only value in the second column (a slope's angle, an end belt's tip).</summary>
        public string SecondText { get; init; } = "";
        public NumRow? WhereA { get; init; }
        public string WhereAPre { get; init; } = "";
        public NumRow? WhereB { get; init; }
        public string WhereBPre { get; init; } = "↑";
        public ChoiceKnob? Extent { get; init; }
        public string ExtentNow { get; init; } = "";
        public NumberKnob? DeckKnob { get; init; }
        public long DeckNow { get; init; }
        public string DeckLabel { get; init; } = "";
        public string WhereText { get; init; } = "";
        public ChoiceRow? Material { get; init; }
        public string MaterialText { get; init; } = "";
        public string T { get; init; } = "–";
        public float Share { get; init; }
        public string WeightTip { get; init; } = "";
        public bool Removable { get; init; }
        public (string Label, string Value)[] Lines { get; init; } = [];
        public string Note { get; init; } = "";
        /// <summary>A sub-row (↳) under its part: a zone or slopes.</summary>
        public bool Sub { get; init; }
        /// <summary>The belt's shape after its name: "inside · 18°".</summary>
        public string Tag { get; init; } = "";
        /// <summary>Shown but not in force (wings on a TDS ship: the torpedo protection stands there).</summary>
        public bool Greyed { get; init; }
        public ShapeItem[] Shape { get; init; } = [];
        /// <summary>The plate's tooltip on the drawings.</summary>
        public string Hover { get; set; } = "";
        public string Title => Name;
        /// <summary>Its name with its parent's, for the selected plate ("Belt, at the magazines").</summary>
        public string Full { get; init; } = "";
        public string ThickShown => Thick?.Value ?? ThickText;
    }

    sealed record PChip(Part? Part, int Deck, string Label, string Tip);

    /// <summary>A stop the sections' cuts snap to: the machinery, each magazine, the fore and aft ends.</summary>
    sealed record CutStop(string Id, string Label, float X);

    /// <summary>A room on the elevation (magazines, handling rooms, machinery), tinted; its tooltip.</summary>
    sealed record PRoom(RoomKind Kind, string Name, float X0, float X1, float Z0, float Z1, string Tip);

    sealed class ProtPane
    {
        public required ProtRow[] Rows;
        public required PChip[][] Chips;
        public required PPlate[] Plates;
        public required PGhost[] Ghosts;
        public required (string Short, float Z, float X0, float X1)[] Lanes;
        public required Vector2[] Profile;
        public float L, B, Wl, Keel, Ib, ZTop, X0, X1, BeltTop, BeltBottom, RoofZ, SecTop;
        public string Citadel = "";
        public bool HasCitadel;
        public required (long N, string Name, float Z)[] DeckChoices;
        public required CutStop[] Stops;
        public required PRoom[] Rooms;
        /// <summary>The main transverse bulkheads (x, kind) and the run that sinks her, when warned.</summary>
        public required (float X, string Kind)[] Compartments;
        public (float X0, float X1, string Tip)? Warned;
        public string CompChip = "", CompTip = "", CompLabel = "", ImmuneLine = "";
        public bool CompOk = true;
        public required (string Tag, float X, float Z, double Mm, PRef R)[] Tags;
        public string Reads = "";
        public PE.Coverage Coverage;
        public required string[] CoverTips;
        public required PickRow Materials;
        /// <summary>The layout templates: the one the design reads as, its name in the dropdown (amber when it keeps its own
        /// values or is custom), and each template's words on this ship.</summary>
        public string LayoutName = "";
        public bool LayoutOwn;
        public int LayoutIndex = -1;
        public required string[] LayoutTips;
        public string PlainTip = "";
        public string ImmuneGun = "";
        public required NumRow BeltHeight, BeltDepth, TdsRow;
        public bool Built;
        public string BeltTip = "", TdsTip = "";
        /// <summary>The end belts' tips and the upper belt's span and top edge, for the handles (NaN: none).</summary>
        public float EndTipFore = float.NaN, EndTipAft = float.NaN, UpX0 = float.NaN, UpX1 = float.NaN, UpTop = float.NaN;
        public Ship? Ship;
        /// <summary>The design's armour deck for a built one, by deck name and extent.</summary>
        public required Func<string, Extent, int> DeckRow;
        public required string[] CutLabels;
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

    static string Sx1(double v) => (v < 0 ? "−" : v > 0 ? "+" : "") + Math.Abs(v).ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>Stretches as their union, aft to forward.</summary>
    static List<(float X0, float X1)> Merged(IEnumerable<(float X0, float X1)> spans)
    {
        var o = new List<(float X0, float X1)>();
        foreach (var (a, b) in spans.OrderBy(s => s.X0))
            if (o.Count > 0 && a <= o[^1].X1 + 0.05f)
                o[^1] = (o[^1].X0, MathF.Max(o[^1].X1, b));
            else
                o.Add((a, b));
        return o;
    }

    ProtPane ProtPaneOf(Design d, Ship? ship)
    {
        var a = d.Armour ?? new ArmourInput();
        var hb = ship?.Hitboxes;
        float L = (float)(hb?.Length ?? 100), B = (float)(hb?.Beam ?? 12);
        float wl = (float)(hb?.Vertical.Waterline ?? -4), keel = (float)(hb?.Vertical.Keel ?? -10);
        var belt = hb?.Armour.Belt;
        var fwd = hb?.Armour.Bulkheads?.FirstOrDefault(b => b.Id == "Forward bulkhead");
        var aft = hb?.Armour.Bulkheads?.FirstOrDefault(b => b.Id == "Aft bulkhead");
        float X0 = (float)(aft?.X ?? belt?.X0 ?? -0.3 * L), X1 = (float)(fwd?.X ?? belt?.X1 ?? 0.3 * L);
        if (belt != null && a.BeltExtent != "machinery")
            (X0, X1) = ((float)Math.Min(X0, belt.X0), (float)Math.Max(X1, belt.X1));
        float beltTop = (float)(belt?.Top ?? wl + (a.BeltHeightM ?? 2)), beltBot = (float)(belt?.Bottom ?? wl - (a.BeltDepthM ?? 1.5));
        var decks = hb?.Decks ?? [];
        float ib = (float)(decks.FirstOrDefault(x => x.Kind == "inner_bottom")?.Z ?? keel + 1.1);
        float roofZ = (float)(hb?.Vertical.ArmourDeck ?? wl);
        var raised = hb?.Vertical.Raised ?? [];
        bool inside = a.BeltPosition == "internal" && PE.Has(d, Part.Belt);
        double lean = PE.Has(d, Part.Belt) ? a.BeltInclineDeg ?? 0 : 0;

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

        // the magazines and machinery as the build placed them
        var rooms = hb?.Rooms ?? [];
        var magSpans = Merged(rooms.Where(r => r.Kind == RoomKind.Magazine && r.Cells.Count > 0).Select(r => ((float)r.X0, (float)r.X1)));
        var zoneSpans = magSpans.Select(s => (Math.Max(s.X0, X0), Math.Min(s.X1, X1))).Where(s => s.Item2 > s.Item1 + 0.05f).ToList();
        float zoneLen = zoneSpans.Sum(s => s.Item2 - s.Item1);
        var mach = rooms.Where(r => r.Kind is RoomKind.EngineRoom or RoomKind.BoilerRoom && r.Cells.Count > 0).ToList();

        // the built plates, each traced to its row
        var mainIds = hb?.Components.Where(c => c.Kind == ComponentKind.Main).Select(c => c.Id).ToHashSet() ?? [];
        var designDecks = a.Decks ?? [];
        int DeckRow(string name, Extent ext)
        {
            // a design deck's extent (a string, "ends" for both) against a built piece's
            static bool Covers(string? e, Extent rep) => (e ?? "citadel") == rep.Name() || e == "ends" && rep is Extent.Fore or Extent.Aft;
            for (int i = 0; i < designDecks.Count; i++)
                if (((designDecks[i].Mm ?? 0) > 0 || (designDecks[i].SlopeMm ?? 0) > 0) && Decks.DeckName(designDecks[i].Deck ?? 0) == name && Covers(designDecks[i].Extent, ext))
                    return i;
            for (int i = 0; i < designDecks.Count; i++)
                if (((designDecks[i].Mm ?? 0) > 0 || (designDecks[i].SlopeMm ?? 0) > 0) && Covers(designDecks[i].Extent, ext))
                    return i;
            return -1;
        }
        string Mm(double mm) => Fmt(mm, Quantity.Armour, unit: false);
        var plates = new List<PPlate>();
        var tags = new List<(string, float, float, double, PRef)>();
        // each design deck's weight: its lines once, then shared between the flat and its zone by the built pieces' plate
        var deckLine = new double[designDecks.Count];
        var deckZoneShare = new double[designDecks.Count];
        var deckFlatArea = new double[designDecks.Count];
        var deckZoneArea = new double[designDecks.Count];
        var slopeT = new double[designDecks.Count];
        for (int i = 0; i < designDecks.Count; i++)
        {
            var x = designDecks[i];
            string ext = x.Extent ?? "citadel";
            string name = $"Deck armour ({Decks.DeckNameLower(x.Deck ?? 0)}" + (ext is "fore" or "aft" ? $", {ext})" : ")");
            if (designDecks.Take(i).Any(p => (p.Deck ?? 0) == (x.Deck ?? 0) && (p.Extent ?? "citadel") == ext))
                continue;   // a second entry on the same deck and stretch merges with the first: its weight is there
            deckLine[i] = ext == "ends" ? w.GetValueOrDefault(name[..^1] + ", fore)") + w.GetValueOrDefault(name[..^1] + ", aft)") : w.GetValueOrDefault(name);
            slopeT[i] = w.GetValueOrDefault(name[..^1] + ", slopes)");
        }
        if (hb != null)
        {
            if (a.TdsM is > 0)
                plates.Add(new PPlate(new PRef(PK.Tds), X0, X1, ib, Math.Min(roofZ, wl), 0,
                    $"TDS {a.TdsM:0.0} m" + ((a.TdsLayers ?? 1) > 1 ? $" · {a.TdsLayers} layers" : "")));
            foreach (var s in hb.Armour.Strakes ?? [])
            {
                var r = s.Kind switch
                {
                    "upper" => new PRef(PK.Upper), "box" => new PRef(PK.Steering), "zone" => new PRef(PK.BeltZone),
                    _ => new PRef(s.Extent == Extent.Fore ? PK.EndFore : PK.EndAft),
                };
                if (s.Kind == "end" && s.TipMm is { } tip && Math.Abs(tip - s.ThicknessMm) > 0.5)
                {
                    // root and tip halves, so the taper shows
                    float m = (float)(s.X0 + s.X1) / 2;
                    bool fore = s.Extent == Extent.Fore;
                    plates.Add(new PPlate(r, (float)s.X0, m, (float)s.Bottom, (float)s.Top, fore ? s.ThicknessMm : tip, Mm(fore ? s.ThicknessMm : tip)));
                    plates.Add(new PPlate(r, m, (float)s.X1, (float)s.Bottom, (float)s.Top, fore ? tip : s.ThicknessMm, Mm(fore ? tip : s.ThicknessMm)));
                }
                else
                    plates.Add(new PPlate(r, (float)s.X0, (float)s.X1, (float)s.Bottom, (float)s.Top, s.ThicknessMm, s.Kind == "box" ? "" : Mm(s.ThicknessMm),
                        Hatched: s.Kind == "zone" && inside));
            }
            if (belt != null)
            {
                // the belt where no zone takes its place
                float tf = (float)(belt.TaperFrom ?? belt.Bottom);
                double low = belt.BottomMm ?? belt.ThicknessMm;
                var pieces = new List<(float, float)> { ((float)belt.X0, (float)belt.X1) };
                foreach (var z in (hb.Armour.Strakes ?? []).Where(s => s.Kind == "zone"))
                    pieces = pieces.SelectMany(p => z.X1 <= p.Item1 || z.X0 >= p.Item2 ? [p]
                        : new[] { (p.Item1, (float)z.X0), ((float)z.X1, p.Item2) }.Where(q => q.Item2 - q.Item1 > 0.05f)).ToList();
                foreach (var (p0, p1) in pieces)
                {
                    plates.Add(new PPlate(new PRef(PK.Belt), p0, p1, tf, (float)belt.Top, belt.ThicknessMm, Mm(belt.ThicknessMm), Hatched: inside));
                    if (tf > belt.Bottom + 0.05)
                        plates.Add(new PPlate(new PRef(PK.Belt), p0, p1, (float)belt.Bottom, tf, (belt.ThicknessMm + low) / 2,
                            Math.Abs(low - belt.ThicknessMm) > 0.5 ? "to " + Mm(low) : "", Hatched: inside));
                }
                if (hb.Armour.Inclined?.FirstOrDefault(p => p.Id == "Lower belt") is { } lb)
                    plates.Add(new PPlate(new PRef(PK.Belt), (float)lb.X0, (float)lb.X1, (float)lb.Z0, (float)lb.Z1, lb.ThicknessMm,
                        $"lower belt {Mm(lb.ThicknessMm)} to {Mm(lb.BottomMm ?? lb.ThicknessMm)}", Faint: true));
                string shape = inside ? lean > 0 ? $"inside · {lean:0}°" : "inside" : lean > 0 ? $"{lean:0}°" : "";
                if (shape.Length > 0)
                    tags.Add((shape, (float)belt.X1, (float)belt.Top, belt.ThicknessMm, new PRef(PK.Belt)));
            }
            foreach (var bh in hb.Armour.Bulkheads ?? [])
            {
                var r = bh.Id.Contains("Steering", StringComparison.Ordinal) ? new PRef(PK.Steering)
                    : bh.Id.StartsWith("Magazine box", StringComparison.Ordinal) ? new PRef(PK.Box)
                    : bh.Id.Contains("end belt", StringComparison.Ordinal) ? new PRef(PK.EndAft) : new PRef(PK.Bulkheads);
                float lt = (float)(bh.LowerTop ?? bh.Bottom);
                plates.Add(new PPlate(r, (float)bh.X, (float)bh.X, Math.Max(lt, (float)bh.Bottom), (float)bh.Top, bh.ThicknessMm, "", Bulkhead: true));
                if (lt > bh.Bottom + 0.05)
                    plates.Add(new PPlate(r, (float)bh.X, (float)bh.X, (float)bh.Bottom, lt, bh.LowerMm ?? bh.ThicknessMm / 2, "", Bulkhead: true, Inside: true));
            }
            // the magazine boxes' sides: a fill between their crown and floor over each room
            if (a.MagazineBox?.SideMm is > 0 && PE.Has(d, Part.Box))
                foreach (var r in rooms.Where(r => r.Kind == RoomKind.Magazine && r.Cells.Count > 0))
                    plates.Add(new PPlate(new PRef(PK.Box), (float)r.X0, (float)r.X1, (float)r.Base, (float)r.Top, a.MagazineBox.SideMm.Value, "", Faint: true));
            var barbR = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var c in hb.Components)
            {
                if (c.X is not { } x)
                    continue;
                switch (c.Kind)
                {
                    case ComponentKind.Barbette when c.R is { } r && c.ArmourMm is { } mm and > 0 && c.Id.EndsWith(" barbette", StringComparison.Ordinal)
                                                      && mainIds.Contains(c.Id[..^9]):
                        barbR[c.Id[..^9]] = r;
                        float deckTop = raised.Any(q => x >= q.X0 && x <= q.X1) ? (float)raised.Where(q => x >= q.X0 && x <= q.X1).Max(q => q.Top) : 0;
                        if (c.Top > deckTop)
                            plates.Add(new PPlate(new PRef(PK.Barbettes), (float)(x - 0.8 * r), (float)(x + 0.8 * r), deckTop, (float)c.Top, mm, ""));
                        plates.Add(new PPlate(new PRef(PK.Barbettes), (float)(x - 0.8 * r), (float)(x + 0.8 * r), (float)c.Base, Math.Min(deckTop, (float)c.Top),
                            mm, "", Inside: true));
                        break;
                    case ComponentKind.ConningTower when c.R is { } r && c.ArmourMm is { } mm and > 0:
                        plates.Add(new PPlate(new PRef(PK.Ct), (float)(x - r), (float)(x + r), (float)c.Base, (float)c.Top, mm, "CT"));
                        break;
                }
            }
            foreach (var c in hb.Components)
                if (c.Kind == ComponentKind.Main && c.X is { } x && c.ArmourMm is { } face and > 0)
                {
                    float r = (float)(barbR.GetValueOrDefault(c.Id, 5) * 1.15);
                    plates.Add(new PPlate(new PRef(PK.Main, 0), (float)x - r, (float)x + r, (float)c.Base, (float)c.Top, face, c.Id.Length <= 3 ? c.Id : ""));
                }
            // the slopes: a band from the deck down to the belt's lower edge, tagged
            foreach (var p in hb.Armour.Inclined ?? [])
            {
                if (p.Kind != "slope")
                    continue;
                int a0 = p.Id.IndexOf('('), a1 = p.Id.IndexOf(')');
                var parts = a0 >= 0 && a1 > a0 ? p.Id[(a0 + 1)..a1].Split(", ") : [""];
                int row = DeckRow(parts[0].Length > 0 ? char.ToUpperInvariant(parts[0][0]) + parts[0][1..] : "", parts.Length > 1 ? SnakeEnum<Extent>.Parse(parts[1]) : Extent.Citadel);
                var r = new PRef(PK.Slope, row);
                plates.Add(new PPlate(r, (float)p.X0, (float)p.X1, (float)p.Z0, (float)p.Z1, p.ThicknessMm, "", Faint: true));
                double deg = Math.Atan2(p.Z1 - p.Z0, Math.Max(1e-3, p.U1 - p.U0)) * 180 / Math.PI;
                if (!tags.Any(t => t.Item5 == r))
                    tags.Add(($"slopes {Mm(p.ThicknessMm)} · {deg:0}°", (float)p.X0, (float)(p.Z0 + p.Z1) / 2, p.ThicknessMm, r));
            }
            foreach (var dk in hb.Armour.Decks ?? [])
            {
                PRef r;
                if (dk.Extent == Extent.Steering)
                    r = new PRef(PK.Steering);
                else if (dk.Extent == Extent.Box)
                    r = new PRef(PK.Box);
                else
                {
                    int row = DeckRow(dk.Deck, dk.Extent);
                    bool zone = row >= 0 && designDecks[row] is { MagazinesMm: { } zm } dd && Math.Abs(zm - dk.ThicknessMm) < 0.5 && Math.Abs((dd.Mm ?? 0) - zm) > 0.5;
                    r = new PRef(zone ? PK.DeckZone : PK.Deck, row);
                    if (row >= 0)
                    {
                        double area = (dk.X1 - dk.X0) * dk.ThicknessMm;
                        if (zone)
                            deckZoneArea[row] += area;
                        else
                            deckFlatArea[row] += area;
                    }
                }
                plates.Add(new PPlate(r, (float)dk.X0, (float)dk.X1, (float)dk.Z, (float)dk.Z, dk.ThicknessMm, Mm(dk.ThicknessMm), Flat: true));
            }
        }
        for (int i = 0; i < designDecks.Count; i++)
            deckZoneShare[i] = deckZoneArea[i] + deckFlatArea[i] > 0 ? deckZoneArea[i] / (deckZoneArea[i] + deckFlatArea[i]) : 0;

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
                if (bare.X1[s] - bare.X0[s] < 4 || plates.Any(p => p.Flat && p.Ref.Kind is PK.Deck or PK.DeckZone && Math.Abs(p.Z0 - z) < 0.05 && p.X0 <= m && m <= p.X1))
                    continue;
                string ext = bare.Ext[s];
                double t = (bare.X1[s] - bare.X0[s]) * B * (ext == "citadel" ? 0.92 : 0.62) * 25 / 1000 * Weight.Steel;
                ghosts.Add(new PGhost(new PRef(PK.Deck, -1), (int)n, ext, bare.X0[s], bare.X1[s], z, z,
                    $"+ armour deck on the {name.ToLowerInvariant()}, {PE.ExtentText(ext)}, {Fmt(25, Quantity.Armour)}\nabout {N0(t)} t\n" +
                    "Then: full length or both ends in its row."));
            }
        }
        double beltMm = a.BeltMm is > 0 ? a.BeltMm.Value : 100;
        string AddTip(Part p) => $"+ {PE.Name(p).ToLowerInvariant()}: " + p switch
        {
            Part.Belt => $"over the citadel, {Fmt(100, Quantity.Armour)}",
            Part.Upper => $"from the belt up to the main deck, over the citadel, {Fmt(Math.Round((a.BeltMm ?? 200) * 0.05, MidpointRounding.AwayFromZero) * 10, Quantity.Armour)}",
            Part.EndFore => $"from the citadel to the stem, {Fmt(Math.Round((a.BeltMm ?? 200) * 0.04, MidpointRounding.AwayFromZero) * 10, Quantity.Armour)}",
            Part.EndAft => $"from the citadel to the stern, {Fmt(Math.Round((a.BeltMm ?? 200) * 0.04, MidpointRounding.AwayFromZero) * 10, Quantity.Armour)}",
            Part.Bulkheads => $"across both citadel ends, {Fmt(Math.Round((a.BeltMm ?? 200) * 0.07, MidpointRounding.AwayFromZero) * 10, Quantity.Armour)}",
            Part.Tds => "4.0 m deep: spaces between the side and the holding bulkhead, inner bottom to the roof deck",
            Part.Inner => $"an armoured wall behind the belt over the citadel, {Fmt(Math.Round(Math.Max(20, beltMm * 0.10) / 5) * 5, Quantity.Armour)}, " +
                          $"{Fmt(Math.Round(Math.Max(25, beltMm * 0.14) / 5) * 5, Quantity.Armour)} at the magazines; on the wall a section already has " +
                          "(TDS, wing bunkers, a magazine's wing) or its own",
            Part.Box => $"armour round each run of magazines, {Fmt(Math.Round(Math.Max(25, (a.BeltMm is > 0 ? a.BeltMm.Value : 60) * 0.4) / 5) * 5, Quantity.Armour)} sides: " +
                        "on their wing walls, crown on their platform deck, ends, floor on the inner bottom",
            Part.Wings => "stores between each magazine and the side, 0.1 of the beam",
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
            hb?.Components.Where(c => c.Kind is ComponentKind.Main or ComponentKind.ConningTower).Select(c => c.Top).DefaultIfEmpty(0).Max() ?? 0);
        if (hb != null)
            foreach (var st in hb.HullForm.Stations)
                profile.Add(new Vector2((float)st.X, (float)st.Z[0]));
        var lanes = new List<(string, float, float, float)>();
        foreach (var (n, name, z) in choices)
        {
            var rs = n < 0 ? raised[(int)(-n - 1)] : null;
            lanes.Add((DeckShort(n, rs?.Id ?? name), z, rs != null ? (float)rs.X0 : -L / 2, rs != null ? (float)Math.Min(rs.X1, L / 2) : L / 2));
        }

        // the rooms on the elevation, the compartments and the run that sinks her
        var proom = new List<PRoom>();
        foreach (var r in rooms)
            if (r.Cells.Count > 0 && r.Kind is RoomKind.Magazine or RoomKind.Handling or RoomKind.EngineRoom or RoomKind.BoilerRoom)
                proom.Add(new PRoom(r.Kind, r.Id, (float)r.X0, (float)r.X1, (float)r.Base, (float)r.Top,
                    $"{r.Id} · {r.X1 - r.X0:0.0} m · {Sx1(r.Base - wl)} to {Sx1(r.Top - wl)} m on the waterline"));
        var comps = new List<(float, string)>();
        foreach (var b in hb?.Bulkheads ?? [])
            if (b.X is { } bx && b.Y is null)
                comps.Add(((float)bx, b.Kind));
        (float, float, string)? warned = null;
        string? warning = ship?.Report.Warnings.FirstOrDefault(x => x.Contains("flooded sink", StringComparison.Ordinal));
        if (warning != null && hb != null)
        {
            var m = System.Text.RegularExpressions.Regex.Match(warning, @"sections? (\d+)(?:-(\d+))?");
            var s0 = hb.Sections.FirstOrDefault(s => s.Id == m.Groups[1].Value);
            var s1 = m.Groups[2].Success ? hb.Sections.FirstOrDefault(s => s.Id == m.Groups[2].Value) : s0;
            if (s0 != null && s1 != null)
                warned = ((float)Math.Min(s0.X0, s1.X0), (float)Math.Max(s0.X1, s1.X1), warning);
        }
        long cstd = ship?.Report.Results.CompartmentStandard ?? 0;
        int need = Styles.Get(d).CompartmentStandard;
        string Count(long k) => k switch { 0 => "no", 1 => "one", 2 => "two", 3 => "three", _ => k.ToString(CultureInfo.InvariantCulture) };

        // the cuts' stops
        var stops = new List<CutStop>();
        if (mach.Count > 0)
        {
            float mm0 = (float)mach.Min(r => r.X0), mm1 = (float)mach.Max(r => r.X1), mid = (mm0 + mm1) / 2;
            var m = mach.MinBy(r => Math.Abs((r.X0 + r.X1) / 2 - mid))!;
            stops.Add(new CutStop("mach", $"Machinery · {m.Id.ToLowerInvariant()}", (float)(m.X0 + m.X1) / 2));
        }
        foreach (var r in rooms.Where(r => r.Kind == RoomKind.Magazine && r.Cells.Count > 0).OrderByDescending(r => r.X0))
        {
            float x = (float)(r.X0 + r.X1) / 2;
            if (stops.Any(q => Math.Abs(q.X - x) < 0.6f))
                continue;
            stops.Add(new CutStop("mag:" + r.Id.Replace("Magazine ", "", StringComparison.Ordinal), r.Id, x));
        }
        stops.Add(new CutStop("fore", "Fore end", X1 + 0.3f * (L / 2 - X1)));
        stops.Add(new CutStop("aft", "Aft end", X0 - 0.3f * (X0 + L / 2)));

        // ---------------------------------------------------------------- the rows, group by group: only the parts the
        // ship has; the rest are + chips
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
        NumRow R(NumberKnob k) => Row(k, d, ship) with { Delta = "" };
        string Covers(float x0, float x1) => $"{N0(x0)} to {N0(x1)} m · {x1 - x0:0.0} m, {100 * (x1 - x0) / L:0} %";
        string Stretches(List<(float, float)> sp) => sp.Count == 0 ? "–" : sp.Count == 1 ? Covers(sp[0].Item1, sp[0].Item2)
            : $"{sp.Count} stretches, {sp.Sum(s => s.Item2 - s.Item1):0.0} m";
        string WlSpan(float z0, float z1) => $"{z0 - wl:+0.0;−0.0} to {z1 - wl:+0.0;−0.0} m on WL";
        string Weights(string title, Func<string, bool> f)
        {
            var parts = w.Where(kv => f(kv.Key)).Select(kv => $"{kv.Key}  {N0(kv.Value)} t").ToList();
            return parts.Count == 0 ? title : title + "\n" + string.Join("\n", parts);
        }
        string Scheme(string part) => a.Materials?.GetValueOrDefault(part) ?? "the engine's default";
        string roofName = choices.Where(c => c.N >= 0).OrderBy(c => Math.Abs(c.Z - roofZ)).Select(c => c.Name).FirstOrDefault() ?? "armour deck";

        if (PE.Has(d, Part.Belt))
        {
            double t = w.GetValueOrDefault("Belt armour") + w.GetValueOrDefault("Lower belt");
            var tag = new List<string>();
            if (inside)
                tag.Add("inside");
            if (lean > 0)
                tag.Add($"{lean:0}°");
            if (a.BeltExtent == "machinery")
                tag.Add("machinery");
            var span = belt != null ? ((float)belt.X0, (float)belt.X1) : (X0, X1);
            var shape = new List<ShapeItem>
            {
                new ShapeSeg("Stands", Choice(Knobs.BeltPosition), ["On the side: the hull's plating outside it", "Inside the shell, 0.3 m in at its top, leaning in below; carried on down as a lower belt to the inner bottom"]),
                new ShapeNum("Leans", R(Knobs.BeltIncline), "top outboard"),
                new ShapeSeg("Runs over", Choice(Knobs.BeltExtent), ["Between the outermost barbettes", "Over the machinery only: the magazines boxed instead (a treaty cruiser's)"]),
                a.BeltMagazinesMm is { } bz
                    ? new ShapeRead("At the magazines", $"{Fmt(bz, Quantity.Armour)}, its row", () => RemoveSub(new PRef(PK.BeltZone)), "Remove the zone: the belt's own thickness there")
                    : new ShapeAdd("At the magazines", "+ zone", () => AddSub(new PRef(PK.BeltZone)), "A thickness of its own beside the magazines (KGV 374 against 348; Brooklyn thinner)"),
            };
            if (inside)
                shape.Add(new ShapeNote($"Inside the shell, 0.3 m in at its top; carried on down as a lower belt to the inner bottom, " +
                                        $"{Fmt(a.BeltBottomMm ?? a.BeltMm ?? 0, Quantity.Armour)} tapering to 25 mm. The space outboard of it is a wing."));
            if (inside && a.BeltBottomMm == null)
                shape.Add(new ShapeNote("Untapered: the lower belt starts at the belt's full thickness and weighs heavy. Set a lower edge.", true));
            if (a.BeltExtent == "machinery" && !(a.MagazineBox?.SideMm > 0))
                shape.Add(new ShapeNote("The magazines' sides are bare: give them boxes.", true));
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Belt), Group = 0, Name = "Belt", Full = "Belt", Swatch = Ramp(a.BeltMm ?? 0), Thick = R(Knobs.Belt),
                Second = R(Knobs.BeltBottom), SecondPre = "lower", WhereA = R(Knobs.BeltDepth), WhereAPre = "↓", WhereB = R(Knobs.BeltHeight),
                Material = Choice(Knobs.SchemeMaterial("belt", "Belt")), T = Tn(t), Share = Share(t),
                WeightTip = Weights("Belt", n => n is "Belt armour" or "Lower belt"), Removable = true, Tag = string.Join(" · ", tag),
                Lines = [("Covers", Covers(span.Item1, span.Item2)), ("Height", WlSpan(beltBot, beltTop)), ("Weight", Pct(t)), ("Material", Scheme("belt")),
                         ("Taper", $"{Mm(a.BeltMm ?? 0)} at WL to {Mm(a.BeltBottomMm ?? a.BeltMm ?? 0)} at its edge")],
                Note = "The citadel runs between the outermost barbettes; the layout places it, so its length moves when the turrets do.",
                Shape = shape.ToArray(),
            });
            if (a.BeltMagazinesMm is { } bm)
            {
                double tz = Sum(n => n.StartsWith("Belt (magazines)", StringComparison.Ordinal));
                rows.Add(new ProtRow
                {
                    Ref = new PRef(PK.BeltZone), Group = 0, Name = "at the magazines", Full = "Belt, at the magazines", Sub = true, Swatch = Ramp(bm),
                    Thick = R(Knobs.BeltMagazines), WhereText = $"{zoneSpans.Count} magazine stretch{(zoneSpans.Count == 1 ? "" : "es")} · {zoneLen:0.0} m",
                    MaterialText = "as above", T = Tn(tz), Share = Share(tz), WeightTip = Weights("Belt at the magazines", n => n.StartsWith("Belt (magazines)", StringComparison.Ordinal)),
                    Removable = true,
                    Lines = [("Covers", Stretches(zoneSpans)), ("Height", WlSpan(beltBot, beltTop)), ("Weight", Pct(tz)), ("Material", Scheme("belt"))],
                    Note = "The belt beside the magazines, in its place there: where the magazine rooms are, as the subdivision gave them their cells.",
                    Shape = rows[^1].Shape,
                });
            }
        }
        if (PE.Has(d, Part.Upper))
        {
            double t = w.GetValueOrDefault("Upper belt");
            var s = hb?.Armour.Strakes?.FirstOrDefault(x => x.Kind == "upper");
            long to = a.UpperBelt!.ToDeck ?? 0;
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Upper), Group = 0, Name = "Upper belt", Full = "Upper belt", Swatch = Ramp(a.UpperBelt.Mm ?? 0), Thick = R(Knobs.UpperBelt),
                Extent = Knobs.UpperBeltExtent, ExtentNow = a.UpperBelt.Extent ?? "citadel", DeckKnob = Knobs.UpperBeltToDeck, DeckNow = to,
                DeckLabel = "to " + DeckShort(to, Decks.DeckName(to)), Material = Choice(Knobs.UpperBeltMaterial), T = Tn(t), Share = Share(t),
                WeightTip = Weights("Upper belt", n => n == "Upper belt"), Removable = true,
                Lines = [("Covers", s != null ? Covers((float)s.X0, (float)s.X1) : "–"), ("Height", s != null ? WlSpan((float)s.Bottom, (float)s.Top) : "–"),
                         ("Weight", Pct(t)), ("Material", a.UpperBelt.Material ?? Scheme("upper_belt"))],
                Note = "From the belt's top edge up to the chosen deck.",
            });
        }
        foreach (var (p, end) in new[] { (Part.EndFore, "fore"), (Part.EndAft, "aft") })
        {
            if (!PE.Has(d, p))
                continue;
            var e = a.EndBelts!.Of(end)!;
            string prefix = end == "fore" ? "Fore end belt" : "Aft end belt";
            double t = Sum(n => n.StartsWith(prefix, StringComparison.Ordinal));
            var s = hb?.Armour.Strakes?.FirstOrDefault(x => x.Kind == "end" && x.Extent.Name() == end);
            double reach = e.Reach ?? 1;
            rows.Add(new ProtRow
            {
                Ref = new PRef(end == "fore" ? PK.EndFore : PK.EndAft), Group = 0, Name = PE.Name(p), Full = PE.Name(p), Swatch = Ramp(e.Mm ?? 0),
                Thick = R(Knobs.EndBeltMm(end)), Second = R(Knobs.EndBeltTip(end)), SecondPre = "tip", WhereA = R(Knobs.EndBeltReach(end)), WhereAPre = "reach",
                WhereText = reach >= 0.995 ? $"to the {(end == "fore" ? "stem" : "stern")}" : $"{reach * 100:0} % of the way",
                Material = Choice(Knobs.EndBeltMaterial(end)), T = Tn(t), Share = Share(t),
                WeightTip = Weights(PE.Name(p), n => n.StartsWith(prefix, StringComparison.Ordinal)), Removable = true,
                Lines = [("Covers", s != null ? Covers((float)s.X0, (float)s.X1) : "–"), ("Height", WlSpan(beltBot, beltTop)), ("Weight", Pct(t)),
                         ("Material", e.Material ?? Scheme("end_belts"))],
                Note = end == "fore" ? "Same height as the belt; tapers from the root to the tip. Reach is the share of the way to the stem."
                    : "Same height as the belt; tapers from the root to the tip. At reach 1.0 it turns across the stern face.",
            });
        }
        if (PE.Has(d, Part.Bulkheads))
        {
            double t = w.GetValueOrDefault("Bulkheads");
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Bulkheads), Group = 0, Name = "Citadel bulkheads", Full = "Citadel bulkheads", Swatch = Ramp(a.BulkheadMm ?? 0),
                Thick = R(Knobs.Bulkheads), WhereText = $"at both citadel ends · {Mm((a.BulkheadMm ?? 0) / 2)} below the belt",
                Material = Choice(Knobs.SchemeMaterial("bulkheads", "Bulkheads")), T = Tn(t), Share = Share(t),
                WeightTip = Weights("Citadel bulkheads", n => n == "Bulkheads"), Removable = true,
                Lines = [("Where", $"at {N0(X0)} and {N0(X1)} m"), ("Weight", Pct(t)), ("Material", Scheme("bulkheads"))],
                Note = "Full thickness down to the belt's lower edge, half below it to the inner bottom.",
            });
        }
        for (int i = 0; i < designDecks.Count; i++)
        {
            var x = designDecks[i];
            long n = x.Deck ?? 0;
            string ext = x.Extent ?? "citadel";
            string name = Decks.DeckName(n) + (ext == "citadel" ? "" : ", " + PE.ExtentText(ext));
            bool roof = n == PE.RoofDeck(d) && ext is "citadel" or "full";
            bool cit = PE.CitadelDeck(d, i);
            float z = DeckZ(n);
            var dk = hb?.Armour.Decks?.FirstOrDefault(k => DeckRow(k.Deck, k.Extent) == i && k.SlopeReach != null);
            double reach = dk?.SlopeReach ?? 0;
            double tFlat = deckLine[i] * (1 - deckZoneShare[i]), tZone = deckLine[i] * deckZoneShare[i];
            string mat = x.Material ?? Scheme("decks");
            int di = i;
            var shape = new List<ShapeItem>();
            if (!cit)
                shape.Add(new ShapeNote("An end deck: flat, side to side. Slopes and zones are for citadel decks."));
            else
            {
                if (x.SlopeMm is > 0)
                {
                    shape.Add(new ShapeNum("Slopes", R(Knobs.DeckSlopeDeg(i)), ""));
                    shape.Add(new ShapeRead("", $"{Fmt(x.SlopeMm.Value, Quantity.Armour)}, its row", () => RemoveSub(new PRef(PK.Slope, di)), "Remove the slopes: the deck flat to the side"));
                    shape.Add(new ShapeRead("Flat", $"{Fmt(x.Mm ?? 0, Quantity.Armour)}, stops {reach:0.0} m in from the side"));
                }
                else
                    shape.Add(new ShapeAdd("Slopes", "+ slopes", () => AddSub(new PRef(PK.Slope, di)), "The deck turns down each side to meet the side at the belt's lower edge"));
                if (ext == "citadel")
                    shape.Add(x.MagazinesMm is { } zm
                        ? new ShapeRead("At the magazines", $"{Fmt(zm, Quantity.Armour)}, its row", () => RemoveSub(new PRef(PK.DeckZone, di)), "Remove the zone: the deck's own thickness there")
                        : new ShapeAdd("At the magazines", "+ zone", () => AddSub(new PRef(PK.DeckZone, di)), "A thickness of its own over the magazines"));
                if (x.SlopeMm is > 0)
                    shape.Add(new ShapeNote("The slope's foot is on the belt's lower edge: move that edge and the slope follows. Drag the brass knee on the section to change the angle."));
            }
            var deckShape = shape.ToArray();
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Deck, i), Group = 1, Name = name, Full = name, Swatch = Ramp(x.Mm ?? 0), Thick = R(Knobs.DeckMm(i)),
                Extent = Knobs.DeckExtent(i), ExtentNow = ext, DeckKnob = Knobs.DeckLevel(i), DeckNow = n, DeckLabel = DeckShort(n, Decks.DeckName(n)),
                Material = Choice(Knobs.DeckMaterial(i)), T = Tn(tFlat), Share = Share(tFlat), WeightTip = $"{name}\n{N0(tFlat)} t", Removable = true,
                Lines = [("On", $"{Decks.DeckNameLower(n)} · {-z:0.0} m under the main deck, {z - wl:+0.0;−0.0} m on the WL"),
                         ("Over", PE.ExtentText(ext)), ("Weight", Pct(tFlat)), ("Material", mat)],
                Note = roof ? "The citadel's roof: the lowest armour deck over it, the one the belt's top edge meets."
                    : "Over or beside the roof deck: a capping deck to start shells, or a splinter deck below.",
                Shape = deckShape,
            });
            if (x.SlopeMm is > 0 && cit)
            {
                double deg = x.SlopeDeg ?? Armour.SlopeDegDefault;
                rows.Add(new ProtRow
                {
                    Ref = new PRef(PK.Slope, i), Group = 1, Name = "slopes", Full = name + ", slopes", Sub = true, Swatch = Ramp(x.SlopeMm.Value),
                    Thick = R(Knobs.DeckSlopeMm(i)), SecondText = $"at {deg:0}°", WhereText = $"to the belt's lower edge · flat {reach:0.0} m in",
                    MaterialText = "as above", T = Tn(slopeT[i]), Share = Share(slopeT[i]), WeightTip = $"{name}, slopes\n{N0(slopeT[i])} t", Removable = true,
                    Lines = [("Height", $"{z - wl:+0.0;−0.0} down to {beltBot - wl:+0.0;−0.0} m on the WL"), ("Angle", $"{deg:0}° from the horizontal"),
                             ("Weight", Pct(slopeT[i])), ("Material", mat)],
                    Note = "Each side the deck turns down to meet the side at the belt's lower edge: a shell through the belt meets it at a steep angle.",
                    Shape = deckShape,
                });
            }
            if (x.MagazinesMm is { } mz && ext == "citadel")
            {
                rows.Add(new ProtRow
                {
                    Ref = new PRef(PK.DeckZone, i), Group = 1, Name = "at the magazines", Full = name + ", at the magazines", Sub = true, Swatch = Ramp(mz),
                    Thick = R(Knobs.DeckMagazinesMm(i)), WhereText = $"over the magazines · {zoneLen:0.0} m", MaterialText = "as above",
                    T = Tn(tZone), Share = Share(tZone), WeightTip = $"{name}, at the magazines\n{N0(tZone)} t", Removable = true,
                    Lines = [("Covers", Stretches(zoneSpans)), ("On", Decks.DeckNameLower(n)), ("Weight", Pct(tZone)), ("Material", mat)],
                    Note = "The deck over the magazines, in its place there.",
                    Shape = deckShape,
                });
            }
        }
        if (d.StyleName == "carrier")
        {
            double t = w.GetValueOrDefault("Flight deck armour");
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.FlightDeck), Group = 1, Name = "Flight deck", Full = "Flight deck", Swatch = Ramp(a.FlightDeckMm ?? 0), Thick = R(Knobs.FlightDeckArmour),
                WhereText = "the flight deck's plating", MaterialText = Scheme("flight_deck"), T = Tn(t), Share = Share(t), WeightTip = "Flight deck armour",
                Lines = [("Weight", Pct(t))], Note = "The flight deck's own armour, over the hangar.",
            });
        }

        // inboard: behind the side
        bool tds = PE.Has(d, Part.Tds);
        // wing bunkers: coal beside the machinery (fuel at the ends doesn't stand behind the belt)
        bool bunkers = false;
        if (hb != null && mach.Count > 0)
        {
            var bunkerRooms = rooms.Where(r => r.Kind == RoomKind.Bunker).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
            double m0 = mach.Min(r => r.X0) - 0.5, m1 = mach.Max(r => r.X1) + 0.5;
            bunkers = bunkerRooms.Count > 0 && hb.Cells.Any(c => c.X0 >= m0 && c.X1 <= m1 && bunkerRooms.Contains(c.Room));
        }
        double wingAuto = Armour.MagazineWingShare * B;
        string innerOn = tds ? "the TDS's holding bulkhead" : !((a.InnerBulkhead?.Mm ?? 0) > 0) ? "the magazines' wing walls"
            : bunkers ? "the wing bunkers' inner wall" : $"its own wall, {a.InnerBulkhead?.InboardM ?? Armour.MagazineWingM(d, B):0.0} m in";
        if (tds)
        {
            double t = w.GetValueOrDefault("Torpedo protection");
            var spec = Armour.Tds(d);
            double steel = Armour.TdsMmPerM * spec.DepthM;
            var shape = new List<ShapeItem>
            {
                new ShapeNum("Depth", R(Knobs.Tds), "to the holding wall"),
                new ShapeNum("Layers", R(Knobs.TdsLayers), spec.Layers > 1 ? "a space each" : "one space"),
                new ShapeNum("Holding bulkhead", R(Knobs.TdsHolding), ""),
            };
            if (spec.Layers > 1)
                shape.Add(new ShapeNum("Layer walls", R(Knobs.TdsLayerWall), ""));
            shape.Add(new ShapeNote($"Steel: {Fmt(steel, Quantity.Armour)} a side by default ({Fmt(Armour.TdsMmPerM, Quantity.Armour)} a metre of depth), the same weight however it's split."));
            if (spec.Layers > 1)
                shape.Add(new ShapeNote("Torpedoes still hole every layer in reach at once (open: the flooding session).", true));
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Tds), Group = 2, Name = "Torpedo protection", Full = "Torpedo protection", Swatch = T.Blue, Thick = R(Knobs.Tds),
                Second = R(Knobs.TdsLayers), SecondPre = "layers",
                WhereText = spec.Layers > 1 ? $"{spec.Layers} spaces · holding {Fmt(spec.HoldingMm, Quantity.Armour)}" : $"one space · holding {Fmt(spec.HoldingMm, Quantity.Armour)}",
                MaterialText = "steel walls", T = Tn(t), Share = Share(t), WeightTip = Weights("Torpedo protection", n => n == "Torpedo protection"), Removable = true,
                Lines = [("Depth", $"{a.TdsM:0.0} m, shell to holding bulkhead"), ("Covers", Covers(X0, X1)),
                         ("Height", $"inner bottom to the {roofName.ToLowerInvariant()}"), ("Weight", Pct(t))],
                Note = "Its spaces count in the subdivision (liquid layers, holding bulkhead); the depth also takes beam.",
                Shape = shape.ToArray(),
            });
        }
        if (PE.Has(d, Part.Inner))
        {
            double t = w.GetValueOrDefault("Inner bulkhead");
            var ib0 = a.InnerBulkhead!;
            bool own = !tds && !bunkers && (ib0.Mm ?? 0) > 0;
            var shape = new List<ShapeItem>
            {
                new ShapeNum("Over the citadel", R(Knobs.InnerMm), (ib0.Mm ?? 0) > 0 ? "" : "none"),
                new ShapeNum("At the magazines", R(Knobs.InnerMagazines), ""),
                new ShapeRead("Stands on", innerOn),
            };
            if (own)
                shape.Add(new ShapeNum("In from the side", R(Knobs.InnerInboard), ""));
            shape.Add(new ShapeNote("From the inner bottom to the armour deck. It rides on the wall the section already has; only where there is none does it stand on its own, and that makes wing compartments (one side floods)."));
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Inner), Group = 2, Name = "Inner bulkhead", Full = "Inner bulkhead", Swatch = Ramp(ib0.Mm ?? ib0.MagazinesMm ?? 0),
                Thick = R(Knobs.InnerMm), Second = R(Knobs.InnerMagazines), SecondPre = "mags", WhereText = innerOn, MaterialText = "as the bulkheads",
                T = Tn(t), Share = Share(t), WeightTip = Weights("Inner bulkhead", n => n == "Inner bulkhead"), Removable = true,
                Lines = [("Covers", Covers(X0, X1)), ("Height", $"inner bottom to the {roofName.ToLowerInvariant()}"), ("Stands on", innerOn), ("Weight", Pct(t)),
                         ("Material", Scheme("bulkheads"))],
                Note = "An armoured wall behind the belt, each side over the citadel, thicker (or thinner) beside the magazines.",
                Shape = shape.ToArray(),
            });
        }
        if (PE.Has(d, Part.Box))
        {
            double t = w.GetValueOrDefault("Magazine boxes");
            var runs = Merged(rooms.Where(r => r.Kind == RoomKind.Magazine && r.Cells.Count > 0).Select(r => ((float)r.X0, (float)r.X1)));
            string runText = string.Join(", ", runs.Select(s => $"{Sx1(s.X0)} to {Sx1(s.X1)} m"));
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Box), Group = 2, Name = "Magazine boxes", Full = "Magazine boxes", Swatch = Ramp(a.MagazineBox!.SideMm ?? 0),
                Thick = R(Knobs.BoxSide), Second = R(Knobs.BoxCrown), SecondPre = "crown",
                WhereText = $"ends {Mm(a.MagazineBox.EndMm ?? 0)} · floor {Mm(a.MagazineBox.FloorMm ?? 0)} · {runs.Count} run{(runs.Count == 1 ? "" : "s")}",
                MaterialText = "as the bulkheads", T = Tn(t), Share = Share(t),
                WeightTip = Weights("Magazine boxes", n => n == "Magazine boxes"), Removable = true,
                Lines = [("Covers", Stretches(runs)), ("Weight", Pct(t)), ("Material", Scheme("bulkheads"))],
                Note = "Round each run of magazines, as wide as their cells.",
                Shape =
                [
                    new ShapeNum("Sides", R(Knobs.BoxSide), "wing walls"), new ShapeNum("Crown", R(Knobs.BoxCrown), "platform"), new ShapeNum("Ends", R(Knobs.BoxEnd), ""),
                    new ShapeNum("Floor", R(Knobs.BoxFloor), "inner bottom"),
                    new ShapeNote($"Round {runs.Count} run{(runs.Count == 1 ? "" : "s")} of magazines: {runText}. As wide as their cells; a wing of 0.1 B stands outside them even with wings off."),
                ],
            });
        }
        if (PE.Has(d, Part.Wings) || tds)
        {
            var shape = new List<ShapeItem>();
            if (tds)
                shape.Add(new ShapeNote("The torpedo protection stands between the magazines and the side, so it is their wing. The value counts where it doesn't reach (the ends)."));
            shape.Add(new ShapeNum("Width", R(Knobs.MagazineWing), a.MagazineWingM == null ? "0.1 of the beam" : "each side"));
            shape.Add(new ShapeNote("Stores (refrigerated rooms, the crew's) between each magazine and the side, never fuel: a tank there gets a void cofferdam. In the fine ends the magazine grows longer for its volume."));
            double ww = a.MagazineWingM ?? wingAuto;
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Wings), Group = 2, Name = "Magazine wings", Full = "Magazine wings", Swatch = ProtectionSection.Rgb(0xD7C79A, 1),
                Thick = tds ? null : R(Knobs.MagazineWing), ThickText = tds ? "—" : "", Greyed = tds,
                WhereText = tds ? "the torpedo protection stands there" : "stores between magazine and side", MaterialText = "stores", T = "—",
                Removable = !tds && PE.Has(d, Part.Wings),
                Lines = [("Width", $"{ww:0.0} m each side" + (a.MagazineWingM == null ? " (0.1 B)" : "")), ("Holds", "stores, never fuel")],
                Note = "The wing handle sets the input, not the cell: in a fine end the wing is measured halfway up the magazine, so its cells can look wider.",
                Shape = shape.ToArray(),
            });
        }

        if (PE.Has(d, Part.Steering))
        {
            double t = Sum(n => n.StartsWith("Steering gear box", StringComparison.Ordinal) || n.EndsWith(", steering)", StringComparison.Ordinal));
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Steering), Group = 3, Name = "Steering gear box", Full = "Steering gear box", Swatch = Ramp(a.SteeringBox!.Mm ?? 0),
                Thick = R(Knobs.SteeringSides), Second = R(Knobs.SteeringRoof), SecondPre = "roof", WhereA = R(Knobs.SteeringEnds), WhereAPre = "ends",
                WhereText = "over the rudders", Material = Choice(Knobs.SteeringMaterial), T = Tn(t), Share = Share(t),
                WeightTip = Weights("Steering gear box", n => n.StartsWith("Steering gear box", StringComparison.Ordinal) || n.EndsWith(", steering)", StringComparison.Ordinal)),
                Removable = true,
                Lines = [("Sides", Fmt(a.SteeringBox.Mm ?? 0, Quantity.Armour)), ("Roof", Fmt(a.SteeringBox.DeckMm ?? 0, Quantity.Armour)),
                         ("Ends", Fmt(a.SteeringBox.BulkheadMm ?? 0, Quantity.Armour)), ("Weight", Pct(t))],
                Note = "A box over the steering gear: sides, a roof on its own deck and two ends.",
            });
        }
        var mains = d.Main ?? [];
        for (int k = 0; k < mains.Count; k++)
        {
            double f = mains[k].ArmourMm ?? 0;
            string nm = mains.Count == 1 ? "Turrets" : $"Turrets, battery {(char)('A' + k)}";
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Main, k), Group = 4, Name = nm, Full = nm, Swatch = Ramp(f), Thick = R(Knobs.MainArmour(k)),
                WhereText = $"faces · side {Mm(f * 0.55)} · rear {Mm(f * 0.5)} · roof {Mm(f * 0.4)}", Material = Choice(Knobs.MainMaterial(k)), T = "turret",
                WeightTip = "The gunhouse armour is part of each turret's weight (armament), so it has no line of its own here.",
                Lines = [("Gunhouse", $"face {Mm(f)} · side {Mm(f * 0.55)} · rear {Mm(f * 0.5)} · roof {Mm(f * 0.4)}"), ("Material", mains[k].Material ?? Scheme("turrets"))],
                Note = "Also on the Main battery tab. The side, rear and roof are fixed shares of the face.",
            });
        }
        if (mains.Count > 0 && (mains[0].ArmourMm ?? 0) > 0)
        {
            double t = Sum(n => n.StartsWith("Barbette ", StringComparison.Ordinal) && mainIds.Contains(n[9..]));
            double mm = 0.8 * (mains[0].ArmourMm ?? 0);
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Barbettes), Group = 4, Name = "Barbettes", Full = "Barbettes", Swatch = Ramp(mm), ThickText = Fmt(mm, Quantity.Armour),
                WhereText = "0.8 × the faces", MaterialText = "as the turrets", T = Tn(t), Share = Share(t),
                WeightTip = Weights("Barbettes", n => n.StartsWith("Barbette ", StringComparison.Ordinal) && mainIds.Contains(n[9..])),
                Lines = [("Thickness", Fmt(mm, Quantity.Armour)), ("Weight", Pct(t))],
                Note = "Barbettes follow the turret faces at 0.8 ×, from the armour deck up to the gunhouse.",
            });
        }
        if (hasBelt && hb?.Components.Any(c => c.Kind == ComponentKind.ConningTower) == true)
        {
            double t = w.GetValueOrDefault("Conning tower");
            rows.Add(new ProtRow
            {
                Ref = new PRef(PK.Ct), Group = 4, Name = "Conning tower", Full = "Conning tower", Swatch = Ramp(a.BeltMm ?? 0), ThickText = Fmt(a.BeltMm ?? 0, Quantity.Armour),
                WhereText = "the belt's thickness", Material = Choice(Knobs.SchemeMaterial("conning_tower", "Conning tower")), T = Tn(t), Share = Share(t),
                WeightTip = Weights("Conning tower", n => n == "Conning tower"), Lines = [("Thickness", Fmt(a.BeltMm ?? 0, Quantity.Armour)), ("Weight", Pct(t))],
                Note = "The conning tower takes the belt's thickness: set the belt.",
            });
        }

        var chips = new PChip[ProtGroups][];
        chips[0] = new[] { Part.Belt, Part.Upper, Part.EndFore, Part.EndAft, Part.Bulkheads }.Where(p => !PE.Has(d, p))
            .Select(p => new PChip(p, 0, "+ " + PE.Name(p).ToLowerInvariant().Replace("end belt, ", "end belt "), AddTip(p))).ToArray();
        chips[1] = choices.Where(c => c.N >= 0 && !designDecks.Any(x => (x.Deck ?? 0) == c.N))
            .Select(c => new PChip(null, (int)c.N, "+ " + DeckShort(c.N, c.Name),
                $"+ armour deck on the {c.Name.ToLowerInvariant()}, over the citadel, {Fmt(25, Quantity.Armour)}\n" +
                $"about {N0((X1 - X0) * B * 0.92 * 25 / 1000 * Weight.Steel)} t\nThen: its extent in the row, or drag it to another deck.")).ToArray();
        chips[2] = new[] { Part.Tds, Part.Inner, Part.Box, Part.Wings }.Where(p => !PE.Has(d, p) && !(p == Part.Wings && tds))
            .Select(p => new PChip(p, 0, "+ " + PE.Name(p).ToLowerInvariant(), AddTip(p))).ToArray();
        chips[3] = PE.Has(d, Part.Steering) ? [] : [new PChip(Part.Steering, 0, "+ steering gear box", AddTip(Part.Steering))];
        chips[4] = [];

        var cov = PE.Reads(d);
        var coverTips = PE.Coverages.Select(c =>
        {
            var lines = PE.Describe(d, PE.Apply(d, c, deepest));
            return $"{PE.Name(c)}\n{PE.Purpose(c)}\n\n" + (lines.Count > 0 ? string.Join("\n", lines) : "No change: the design is already this.") +
                   "\n\nThicknesses stay; new plates start from the belt's.";
        }).ToArray();

        // the layout: which template the design reads as, and what each would make of it
        var schemes = Templates.Protection.Select(t => t.Value).ToList();
        var (li, exact) = PE.LayoutOf(d, schemes);
        // two templates can put the same inputs on a ship: the one applied here names it
        if (exact && protLayoutApplied is var (pa, _) && pa != li && pa < schemes.Count && PE.LayoutKey(PE.ApplyLayout(d, schemes[pa])) == PE.LayoutKey(d))
            li = pa;
        string layoutName;
        bool layoutOwn = false;
        if (protLayoutApplied is var (ai, akey) && ai < schemes.Count && PE.LayoutKey(d) != akey && !exact)
            (layoutName, layoutOwn, li) = ($"{Templates.Protection[ai].Name} · edited", true, ai);
        else if (li >= 0)
            (layoutName, layoutOwn) = exact ? (Templates.Protection[li].Name, false) : ($"{Templates.Protection[li].Name} · own values", true);
        else if (!PE.HasLayout(d))
            layoutName = "Plain";
        else
            (layoutName, layoutOwn) = ("Custom", true);
        var layoutTips = Templates.Protection.Select(t =>
        {
            var words = PE.DescribeLayout(PE.ApplyLayout(d, t.Value));
            return $"{t.Name} on this ship:\n" + (words.Count > 0 ? string.Join("\n", words.Select(x => "· " + x)) : "nothing to change") +
                   "\nThicknesses scale with the design's own; what it doesn't set stays.";
        }).ToArray();
        string reads = $"reads as {PE.Name(cov).ToLowerInvariant()}" + (li >= 0 && !layoutName.EndsWith("· edited", StringComparison.Ordinal)
            ? $" · {Templates.Protection[li].Name.ToLowerInvariant()}" : "");
        if (ship != null)
            reads += $" · armour {N0(armourT)} t, {100 * armourT / Math.Max(1, std):0.0} % of standard";

        var upper = hb?.Armour.Strakes?.FirstOrDefault(x => x.Kind == "upper");
        var tipF = hb?.Armour.Strakes?.FirstOrDefault(x => x.Kind == "end" && x.Extent == Extent.Fore);
        var tipA = hb?.Armour.Strakes?.FirstOrDefault(x => x.Kind == "end" && x.Extent == Extent.Aft);
        foreach (var r in rows)
            r.Hover = PlateTip(r);
        float secTop = (float)Math.Max(0, raised.Select(r => r.Top).DefaultIfEmpty(0).Max()) + 1.6f;
        return new ProtPane
        {
            Rows = rows.ToArray(), Chips = chips, Plates = plates.ToArray(), Ghosts = ghosts.ToArray(), Lanes = lanes.ToArray(), Profile = profile.ToArray(),
            L = L, B = B, Wl = wl, Keel = keel, Ib = ib, ZTop = zTop, X0 = X0, X1 = X1, BeltTop = beltTop, BeltBottom = beltBot, RoofZ = Math.Min(roofZ, wl),
            SecTop = secTop, Citadel = $"citadel {X1 - X0:0.0} m · {100 * (X1 - X0) / L:0} % of length",
            HasCitadel = hasBelt || PE.Has(d, Part.Bulkheads) || designDecks.Count > 0, DeckChoices = choices.ToArray(), Stops = stops.ToArray(),
            Rooms = proom.ToArray(), Compartments = comps.ToArray(), Warned = warned,
            CompChip = $"{cstd}", CompLabel = $"Compartments {cstd}     ", CompOk = cstd >= need,
            ImmuneLine = "Immune zone and shells fired in anger · against " + (mains.Count > 0 ? $"her own {Fmt(mains[0].CalibreMm ?? 0, Quantity.Calibre)}" : "a gun"),
            CompTip = $"The main watertight bulkheads. A {Count(cstd)}-compartment ship: any {Count(cstd)} adjacent compartments flooded, she floats " +
                      $"with the margin line clear (her type asks {need})." + (warning != null ? "\n\n" + warning : ""),
            Tags = tags.ToArray(), Reads = reads, Coverage = cov, CoverTips = coverTips, Materials = ArmourPick(d),
            LayoutName = layoutName, LayoutOwn = layoutOwn, LayoutIndex = exact ? li : -1, LayoutTips = layoutTips,
            PlainTip = "Plain: no slopes, an upright belt on the side, no inner bulkhead or boxes; zones and plates stay.",
            ImmuneGun = mains.Count > 0 ? $"her own {Fmt(mains[0].CalibreMm ?? 0, Quantity.Calibre)}" : "a gun",
            BeltHeight = R(Knobs.BeltHeight), BeltDepth = R(Knobs.BeltDepth), TdsRow = R(Knobs.Tds), Built = ship != null,
            BeltTip = "Drag the belt's top or bottom edge (0.1 m steps)\n" +
                      $"now {beltBot - wl:+0.0;−0.0} to {beltTop - wl:+0.0;−0.0} m on the waterline · the end belts share the heights",
            TdsTip = $"Drag the holding bulkhead: the torpedo protection's depth, now {a.TdsM ?? 0:0.0} m (0.1 m steps)",
            EndTipFore = tipF != null ? (float)tipF.X1 : float.NaN, EndTipAft = tipA != null ? (float)tipA.X0 : float.NaN,
            UpX0 = upper != null ? (float)upper.X0 : float.NaN, UpX1 = upper != null ? (float)upper.X1 : float.NaN, UpTop = upper != null ? (float)upper.Top : float.NaN,
            Ship = ship, DeckRow = DeckRow,
            CutLabels = stops.Select(s => s.Id == "mach" ? $"Machinery ({s.Label[12..]})" : s.Label).ToArray(),
        };
    }

    static string PlateTip(ProtRow row) =>
        $"{row.Full} {row.ThickShown}" + (row.T is "turret" or "–" or "—" ? "" : $" · {row.T} t") +
        (row.Ref.Kind is PK.Barbettes or PK.Ct ? "\n" + row.WhereText
            : row.Ref.Kind is PK.Belt or PK.BeltZone or PK.Deck or PK.Slope or PK.DeckZone ? "\nclick: select · wheel: one step · right-click: zones, slopes, position"
            : "\nclick: select · wheel: one step · right-click: more");

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
        int groups = 0;
        for (int g = 0; g < ProtGroups; g++)
            if (pp.Chips[g].Length > 0 || pp.Rows.Any(r => r.Group == g))
                groups++;
        float tableH = P(17) + pp.Rows.Length * rowH + groups * groupH + P(2);
        float topY = o.Y + P(18) + gap;
        float avail = size.Y - P(18) - 3 * gap;
        float wantTop = Math.Clamp(avail - tableH, P(170), MathF.Max(P(170), avail * 0.42f));   // the lower half keeps room
        if (!ReferenceEquals(protFrameDoc, doc) || protFrameSize != size)
        {
            (protFrameDoc, protFrameSize, protTopH, protFrameL) = (doc, size, wantTop, 0);
            protLayoutApplied = null;
        }
        protTopH = MathF.Min(protTopH, wantTop);   // more rows take room from the drawings; fewer don't give it back
        if (pp.Built && (protFrameL <= 0 || pp.L > protFrameL || pp.ZTop > protFrameTop || pp.Keel - 0.4f < protFrameBot || pp.B > protFrameB
                         || pp.L < 0.8f * protFrameL))
            (protFrameL, protFrameTop, protFrameBot, protFrameB) = (pp.L * 1.03f, pp.ZTop + 0.6f, pp.Keel - 0.9f, pp.B * 1.03f);
        float elevH = protTopH;
        float rightW = MathF.Round(Math.Clamp(w * 0.41f, P(400), P(640)));
        float leftW = w - rightW - 3 * gap;
        float secH = MathF.Round(Math.Clamp(avail * 0.57f, P(240), MathF.Max(P(240), avail - P(200))));
        ResolveCuts(pp);
        ProtElevation(dl, new Vector2(o.X + gap, topY), new Vector2(leftW, elevH), pp);
        ProtSections(dl, new Vector2(o.X + 2 * gap + leftW, topY), new Vector2(rightW, secH), pp);
        float lowY = topY + elevH + gap;
        ProtTable(dl, new Vector2(o.X + gap, lowY), new Vector2(leftW, o.Y + size.Y - gap - lowY), pp);
        float insY = topY + secH + gap;
        ProtInspector(dl, new Vector2(o.X + 2 * gap + leftW, insY), new Vector2(rightW, o.Y + size.Y - gap - insY), pp);
        ProtMenu(pp);
        LayoutMenu(pp);
        CutMenu(pp);
        ImGui.EndChild();
    }

    static float ProtRowH => MathF.Max(P(18), ImGui.GetFrameHeight() + P(2));

    /// <summary>The sub-bar: what the scheme reads as, the coverage presets, the layout template, the materials.</summary>
    void ProtBar(ImDrawListPtr dl, Vector2 o, float w, ProtPane pp)
    {
        float h = P(18);
        dl.AddRectFilled(o, o + new Vector2(w, h), T.U32(T.HeaderBg));
        dl.AddLine(o + new Vector2(0, h - 1), o + new Vector2(w, h - 1), T.U32(T.Edge));
        var font = UiFonts.SansSmall;
        float ty = o.Y + (h - font.FontSize) / 2;
        Text(dl, font, new Vector2(o.X + P(6), ty), T.Muted, "PROTECTION");

        var d = doc.Current;
        ImGui.PushFont(font);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(5), 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 0));
        float matW = P(220), layW = P(200);
        float segsW = 0;
        foreach (var c in PE.Coverages)
            segsW += Width(font, PE.Name(c)) + P(10) + 1;
        float right = o.X + w - P(4);
        float matX = right - matW, matL = matX - P(6) - Width(font, "Materials");
        float layX = matL - P(12) - layW, layL = layX - P(6) - Width(font, "Layout");
        float segX = layL - P(12) - segsW, covL = segX - P(6) - Width(font, "Coverage");
        float readsX = o.X + P(6) + Width(font, "PROTECTION") + P(10);
        dl.PushClipRect(new Vector2(readsX, o.Y), new Vector2(covL - P(8), o.Y + h), true);
        Text(dl, font, new Vector2(readsX, ty), T.Text, pp.Reads);
        dl.PopClipRect();
        Text(dl, font, new Vector2(covL, ty), T.Muted, "Coverage");
        ImGui.SetCursorScreenPos(new Vector2(segX, o.Y + P(1)));
        for (int i = 0; i < PE.Coverages.Length; i++)
        {
            var c = PE.Coverages[i];
            if (i > 0)
                ImGui.SameLine();
            if (Seg(PE.Name(c), pp.Coverage == c, Width(font, PE.Name(c)) + P(10)))
                Edit(PE.Apply(d, c, DeepestDeck(pp)), $"Coverage: {PE.Name(c).ToLowerInvariant()}");
            if (ImGui.IsItemHovered())
                Tip(pp.CoverTips[i]);
            if (protForceTip != null && protForceTip == CoverForce[i])
                TipAt(pp.CoverTips[i], ImGui.GetItemRectMin() + new Vector2(-P(120), P(20)));
        }
        // the layout: a dropdown of the templates, the one in force named (amber with its own values, edited or custom)
        Text(dl, font, new Vector2(layL, ty), T.Muted, "Layout");
        ImGui.SetCursorScreenPos(new Vector2(layX, o.Y + P(1)));
        ImGui.PushStyleColor(ImGuiCol.Text, pp.LayoutOwn ? T.Amber : T.Text);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));
        ImGui.PushID("layout");
        if (ImGui.Button(pp.LayoutName, new Vector2(layW, 0)))
            openLayoutMenu = true;
        ImGui.PopID();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        var lr = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        Twisty(dl, new Vector2(lr.Item2.X - P(7), (lr.Item1.Y + lr.Item2.Y) / 2), true, T.Muted);
        if (ImGui.IsItemHovered())
            Tip(pp.LayoutIndex >= 0 ? pp.LayoutTips[pp.LayoutIndex] : pp.LayoutName == "Plain" ? pp.PlainTip
                : $"{pp.LayoutName}: the design's own layout inputs. Pick a template to put its layout on.");
        layoutAnchor = new Vector2(lr.Item1.X, lr.Item2.Y + 1);
        Text(dl, font, new Vector2(matL, ty), T.Muted, "Materials");
        ImGui.SetCursorScreenPos(new Vector2(matX, o.Y + P(1)));
        ImGui.SetNextItemWidth(matW);
        int si = pp.Materials.Index;
        if (ImGui.Combo("##materials", ref si, pp.Materials.Names, pp.Materials.Names.Length) && si != pp.Materials.Index && si < pp.Materials.Count)
            pp.Materials.Apply(si);
        if (ImGui.IsItemHovered())
            Tip("The armour material scheme: the plate each part is made of (the Templates panel's Armour row).");
        ImGui.PopStyleVar(2);
        ImGui.PopFont();
    }

    Vector2 layoutAnchor;

    /// <summary>The layout templates as a plain list of names; hovering one says what it makes of this ship, a click puts
    /// it on. Plain at the foot clears the layout.</summary>
    void LayoutMenu(ProtPane pp)
    {
        if (openLayoutMenu)
        {
            ImGui.SetNextWindowPos(layoutAnchor);
            ImGui.OpenPopup("##layoutmenu");
            openLayoutMenu = false;
        }
        if (!ImGui.BeginPopup("##layoutmenu"))
            return;
        var d = doc.Current;
        ImGui.TextColored(T.Muted, "Layout");
        ImGui.Separator();
        var list = Templates.Protection;
        int forced = protForceTip is { } ft && ft.StartsWith("layout:", StringComparison.Ordinal) ? int.Parse(ft[7..], CultureInfo.InvariantCulture) : -1;
        for (int i = 0; i < list.Count; i++)
        {
            bool cur = pp.LayoutIndex == i;
            if (ImGui.Selectable(list[i].Name + "##lay" + i, cur))
            {
                var next = PE.ApplyLayout(d, list[i].Value);
                Edit(next, $"Layout: {list[i].Name}");
                protLayoutApplied = (i, PE.LayoutKey(next));
            }
            if (ImGui.IsItemHovered())
                Tip(pp.LayoutTips[i]);
            if (i == forced)
                TipAt(pp.LayoutTips[i], ImGui.GetItemRectMax() + new Vector2(P(8), -P(14)));
        }
        ImGui.Separator();
        if (ImGui.Selectable("Plain##layplain", pp.LayoutName == "Plain"))
        {
            Edit(PE.Plain(d), "Layout: plain");
            protLayoutApplied = null;
        }
        if (ImGui.IsItemHovered())
            Tip(pp.PlainTip);
        ImGui.EndPopup();
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
        PK.BeltZone => Knobs.BeltMagazines,
        PK.Upper => Knobs.UpperBelt,
        PK.EndFore => Knobs.EndBeltMm("fore"),
        PK.EndAft => Knobs.EndBeltMm("aft"),
        PK.Bulkheads => Knobs.Bulkheads,
        PK.Tds => Knobs.Tds,
        PK.Inner => Knobs.InnerMm,
        PK.Box => Knobs.BoxSide,
        PK.Wings => Knobs.MagazineWing,
        PK.Deck when r.I >= 0 => Knobs.DeckMm(r.I),
        PK.Slope when r.I >= 0 => Knobs.DeckSlopeMm(r.I),
        PK.DeckZone when r.I >= 0 => Knobs.DeckMagazinesMm(r.I),
        PK.Steering => Knobs.SteeringSides,
        PK.Main => Knobs.MainArmour(r.I),
        PK.FlightDeck => Knobs.FlightDeckArmour,
        _ => null,
    };

    bool ProtLit(PRef r) => r == protSel || protHot == r;

    // ------------------------------------------------------------------ parts: add and remove

    static Part PartOf(PK k) => k switch
    {
        PK.Belt => Part.Belt,
        PK.Upper => Part.Upper,
        PK.EndFore => Part.EndFore,
        PK.EndAft => Part.EndAft,
        PK.Bulkheads => Part.Bulkheads,
        PK.Tds => Part.Tds,
        PK.Inner => Part.Inner,
        PK.Box => Part.Box,
        PK.Wings => Part.Wings,
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
        Part.Inner => PK.Inner,
        Part.Box => PK.Box,
        Part.Wings => PK.Wings,
        _ => PK.Steering,
    };

    void AddPart(Part p)
    {
        Edit(PE.Add(doc.Current, p), $"{PE.Name(p)} added");
        protSel = new PRef(KindOf(p));
    }

    /// <summary>A part's zone or slopes added: the belt's zone, a deck's slopes or zone.</summary>
    void AddSub(PRef r)
    {
        var d = doc.Current;
        string deck = r.I >= 0 && d.Armour?.Decks?.ElementAtOrDefault(r.I) is { } x ? Decks.DeckNameLower(x.Deck ?? 0) : "";
        switch (r.Kind)
        {
            case PK.BeltZone:
                Edit(PE.AddBeltZone(d), "Belt: a zone at the magazines");
                break;
            case PK.Slope:
                Edit(PE.AddSlopes(d, r.I), $"Slopes on the {deck}");
                break;
            case PK.DeckZone:
                Edit(PE.AddDeckZone(d, r.I), $"The {deck}: a zone at the magazines");
                break;
            default:
                return;
        }
        protSel = r;
    }

    void RemoveSub(PRef r)
    {
        var d = doc.Current;
        string deck = r.I >= 0 && d.Armour?.Decks?.ElementAtOrDefault(r.I) is { } x ? Decks.DeckNameLower(x.Deck ?? 0) : "";
        switch (r.Kind)
        {
            case PK.BeltZone:
                Edit(PE.RemoveBeltZone(d), "Belt: no zone at the magazines");
                protSel = new PRef(PK.Belt);
                break;
            case PK.Slope:
                Edit(PE.RemoveSlopes(d, r.I), $"No slopes on the {deck}");
                protSel = new PRef(PK.Deck, r.I);
                break;
            case PK.DeckZone:
                Edit(PE.RemoveDeckZone(d, r.I), $"The {deck}: no zone at the magazines");
                protSel = new PRef(PK.Deck, r.I);
                break;
        }
    }

    void RemovePart(PRef r)
    {
        var d = doc.Current;
        if (r.Kind is PK.BeltZone or PK.Slope or PK.DeckZone)
        {
            RemoveSub(r);
            return;
        }
        if (r.Kind == PK.Deck)
            Edit(PE.RemoveDeck(d, r.I), $"Armour deck {r.I + 1} removed");
        else
            Edit(PE.Remove(d, PartOf(r.Kind)), $"{PE.Name(PartOf(r.Kind))} removed");
        if (protSel == r || r.Kind == PK.Deck && protSel.Kind is PK.Deck or PK.Slope or PK.DeckZone)
            protSel = new PRef(PK.Belt);
    }

    static ProtRow? ProtRowOf(ProtPane pp, PRef r)
    {
        foreach (var row in pp.Rows)
            if (row.Ref == r)
                return row;
        return null;
    }

    // ------------------------------------------------------------------ the plate table

    static readonly float[] ProtColW = [12, 126, 96, 118, 0, 90, 46, 62, 20];   // 0: "where" takes what's left
    readonly float[] protX = new float[ProtColW.Length + 1];
    static readonly string[] ProtHead = ["", "PLATE", "THICKNESS", "LOWER · TIP · …", "WHERE", "MATERIAL", "T", "OF ARM.", ""];

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
            x[i + 1] = x[i] + (ProtColW[i] > 0 ? P(ProtColW[i]) : Math.Max(P(150), w - fixedW));
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
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, P(1)));
        float fh = ImGui.GetFrameHeight();
        int n = 0;
        dl.PushClipRect(o, o + size, true);
        for (int g = 0; g < ProtGroups; g++)
        {
            bool any = pp.Chips[g].Length > 0;
            foreach (var r in pp.Rows)
                any |= r.Group == g;
            if (!any)
                continue;
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
            if (g == 4)
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
        dl.PopClipRect();
        ImGui.PopStyleVar();
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
        var dim = r.Greyed ? T.Muted with { W = 0.6f } : T.Muted;

        var swp = new Vector2(x[0] + P(4) + (r.Sub ? P(6) : 0), p.Y + (rowH - P(11)) / 2);
        dl.AddRectFilled(swp, swp + new Vector2(P(r.Sub ? 5 : 7), P(11)), T.U32(r.Swatch with { W = r.Greyed ? 0.4f : 1 }));
        float nx = x[1] + P(4) + (r.Sub ? P(14) : 0);
        if (r.Sub)
        {
            uint elbow = T.U32(T.Muted);
            float ex = x[1] + P(6), ey = p.Y + rowH / 2;
            dl.AddLine(new Vector2(ex, ey - P(6)), new Vector2(ex, ey), elbow);
            dl.AddLine(new Vector2(ex, ey), new Vector2(ex + P(6), ey), elbow);
        }
        Text(dl, r.Sub ? font : UiFonts.Sans, new Vector2(nx, r.Sub ? tys : ty), on ? T.Brass : r.Greyed ? dim : r.Sub ? T.Text : T.Bright, r.Title);
        if (r.Tag.Length > 0)
        {
            float tx = nx + Width(UiFonts.Sans, r.Title) + P(6);
            float tw = Width(font, r.Tag) + P(8);
            dl.AddRect(new Vector2(tx, tys - P(1)), new Vector2(tx + tw, tys + font.FontSize + P(1)), T.U32(T.BrassDark), P(2));
            Text(dl, font, new Vector2(tx + P(4), tys), T.Brass, r.Tag);
        }
        if (r.Thick is { } th)
        {
            At(2);
            Spinner(th, P(50), extras: false);
        }
        else
            Text(dl, UiFonts.Mono, new Vector2(x[3] - P(22) - Width(UiFonts.Mono, r.ThickText), p.Y + (rowH - UiFonts.Mono.FontSize) / 2), dim, r.ThickText);
        if (r.Second is { } s2)
        {
            Text(dl, font, new Vector2(x[3] + P(4), tys), T.Muted, r.SecondPre);
            ImGui.SetCursorScreenPos(new Vector2(x[4] - P(4) - P(46) - 2 * fh, cy));
            Spinner(s2, P(46), extras: false);
        }
        else if (r.SecondText.Length > 0)
            Text(dl, UiFonts.Mono, new Vector2(x[4] - P(6) - Width(UiFonts.Mono, r.SecondText), p.Y + (rowH - UiFonts.Mono.FontSize) / 2), T.Muted, r.SecondText);

        // where
        float wx = x[4] + P(4);
        if (r.WhereA is { } wa)
        {
            Text(dl, font, new Vector2(wx, tys), T.Muted, r.WhereAPre);
            wx += Width(font, r.WhereAPre) + P(4);
            ImGui.SetCursorScreenPos(new Vector2(wx, cy));
            Spinner(wa, P(40), extras: false);
            wx = ImGui.GetItemRectMax().X + P(6);
        }
        if (r.WhereB is { } wb)
        {
            Text(dl, font, new Vector2(wx, tys), T.Muted, r.WhereBPre);
            wx += Width(font, r.WhereBPre) + P(4);
            ImGui.SetCursorScreenPos(new Vector2(wx, cy));
            Spinner(wb, P(40), extras: false);
            wx = ImGui.GetItemRectMax().X + P(6);
        }
        if (r.DeckKnob is { } dk)
        {
            ImGui.SetCursorScreenPos(new Vector2(wx, cy));
            ImGui.SetNextItemWidth(P(52));
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
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(3), ImGui.GetStyle().FramePadding.Y));
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
        {
            dl.PushClipRect(new Vector2(wx, p.Y), new Vector2(x[5] - P(4), p.Y + rowH), true);
            Text(dl, font, new Vector2(wx, tys), dim, r.WhereText);
            dl.PopClipRect();
        }

        // material
        if (r.Material is { } mat)
        {
            At(5);
            ImGui.SetNextItemWidth(P(88));
            ImGui.PushFont(font);
            int mi = mat.Index;
            if (ImGui.Combo("##mat", ref mi, mat.Labels, mat.Labels.Length) && mi < mat.Knob.Values.Length && mi != mat.Index)
                Edit(mat.Knob.Set(d, mat.Knob.Values[mi]), $"{r.Name} material: {mat.Labels[mi]}");
            ImGui.PopFont();
        }
        else
            Text(dl, font, new Vector2(x[5] + P(6), tys), dim, r.MaterialText);

        // weight and its share
        var tp = new Vector2(x[7] - P(6) - Width(UiFonts.Mono, r.T), p.Y + (rowH - UiFonts.Mono.FontSize) / 2);
        Text(dl, UiFonts.Mono, tp, r.T is "turret" or "—" ? T.Muted : T.Bright, r.T);
        if (r.WeightTip.Length > 0 && ImGui.IsMouseHoveringRect(new Vector2(x[6], p.Y), new Vector2(x[7], p.Y + rowH)))
            Tip(r.WeightTip);
        if (r.Share > 0)
        {
            var bp = new Vector2(x[7] + P(4), p.Y + (rowH - P(6)) / 2);
            Bar(dl, bp, new Vector2(P(22), P(6)), Math.Min(1, r.Share * 2), T.Group("armour"));
            var pct = PctText(r.Share);
            Text(dl, UiFonts.Mono, new Vector2(x[8] - P(2) - Width(UiFonts.Mono, pct), p.Y + (rowH - UiFonts.Mono.FontSize) / 2), T.Muted, pct);
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
                Tip(r.Sub ? $"Remove the {r.Name}: it comes back as a + in its part's Shape, or from the right-click menu"
                    : $"Remove the {r.Name.ToLowerInvariant()}: it comes back as a + in its group, and on the elevation");
        }
        ImGui.PopID();
    }

    static readonly string[] PctCache = Enumerable.Range(0, 101).Select(i => $"{i} %").ToArray();
    static string PctText(float share) => PctCache[Math.Clamp((int)MathF.Round(share * 100), 0, 100)];

    // ------------------------------------------------------------------ the selected plate: facts and Shape

    void ProtInspector(ImDrawListPtr dl, Vector2 pos, Vector2 size, ProtPane pp)
    {
        dl.AddRectFilled(pos, pos + size, T.U32(T.Panel2));
        dl.AddRect(pos, pos + size, T.U32(T.Edge));
        ImGui.SetCursorScreenPos(pos);
        SubHeader(dl, size.X, "SELECTED PLATE", "drag its brass handles on the drawings");
        var r = ProtRowOf(pp, protSel);
        var font = UiFonts.SansSmall;
        float ih = P(22);
        float x = pos.X + P(8), y = pos.Y + P(22);
        float factsW = MathF.Round(size.X * 0.46f);
        float bottom = pos.Y + size.Y - ih - P(6);
        if (r != null)
        {
            // the facts, on the left
            ImGui.PushClipRect(pos, new Vector2(pos.X + factsW, bottom), true);
            dl.AddRectFilled(new Vector2(x, y + P(2)), new Vector2(x + P(8), y + P(13)), T.U32(r.Swatch));
            Text(dl, UiFonts.SansBold, new Vector2(x + P(14), y), T.Bright, r.Full);
            Text(dl, UiFonts.Sans, new Vector2(x + P(22) + Width(UiFonts.SansBold, r.Full), y), T.Muted, r.ThickShown);
            y += P(20);
            foreach (var (label, value) in r.Lines)
            {
                Text(dl, font, new Vector2(x, y + P(1)), T.Muted, label);
                Text(dl, UiFonts.Mono, new Vector2(x + P(66), y + P(1)), T.Text, value);
                y += P(16);
            }
            y += P(4);
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            ImGui.PushTextWrapPos(pos.X + factsW - P(10) - ImGui.GetWindowPos().X);   // window-local
            ImGui.PushFont(font);
            ImGui.TextColored(T.Muted, r.Note);
            ImGui.PopFont();
            ImGui.PopTextWrapPos();
            ImGui.PopClipRect();
            dl.AddLine(new Vector2(pos.X + factsW, pos.Y + P(24)), new Vector2(pos.X + factsW, bottom - P(2)), T.U32(T.Edge));
            ShapePanel(dl, new Vector2(pos.X + factsW + P(8), pos.Y + P(22)), new Vector2(size.X - factsW - P(16), bottom - pos.Y - P(22)), r);
        }
        // the immune zone: a one-line placeholder until the penetration model
        var a = new Vector2(pos.X + P(8), pos.Y + size.Y - ih - P(4));
        var b = new Vector2(pos.X + size.X - P(8), pos.Y + size.Y - P(4));
        dl.AddRect(a, b, T.U32(new Vector4(0.333f, 0.376f, 0.416f, 1)));
        var grey = new Vector4(0.37f, 0.416f, 0.455f, 1);
        float ly = a.Y + (ih - font.FontSize) / 2;
        Text(dl, font, new Vector2(a.X + P(6), ly), grey, pp.ImmuneLine);
        string tag = "NEEDS THE PENETRATION MODEL";
        var tg = new Vector2(b.X - P(6) - Width(font, tag) - P(6), ly);
        dl.AddRect(tg - new Vector2(0, P(1)), tg + new Vector2(Width(font, tag) + P(6), font.FontSize + P(1)), T.U32(T.BrassDark));
        Text(dl, font, tg + new Vector2(P(3), 0), new Vector4(0.55f, 0.48f, 0.33f, 1), tag);
    }

    /// <summary>Shape: what changes the part's form rather than its thickness (a zone, slopes, the belt's lean and place),
    /// with the material at its head.</summary>
    void ShapePanel(ImDrawListPtr dl, Vector2 pos, Vector2 size, ProtRow r)
    {
        var font = UiFonts.SansSmall;
        var d = doc.Current;
        float fh = ImGui.GetFrameHeight();
        ImGui.PushClipRect(pos, pos + size, true);
        Text(dl, font, pos + new Vector2(0, P(2)), T.Muted, "SHAPE");
        float y = pos.Y;
        if (r.Material is { } mat)
        {
            ImGui.SetCursorScreenPos(new Vector2(pos.X + size.X - P(150), y));
            ImGui.SetNextItemWidth(P(150));
            ImGui.PushFont(font);
            int mi = mat.Index;
            if (ImGui.Combo("##shapemat", ref mi, mat.Labels, mat.Labels.Length) && mi < mat.Knob.Values.Length && mi != mat.Index)
                Edit(mat.Knob.Set(d, mat.Knob.Values[mi]), $"{r.Name} material: {mat.Labels[mi]}");
            ImGui.PopFont();
        }
        y += P(22);
        float lx = pos.X, cx = pos.X + P(86);
        float rowH = MathF.Max(P(20), fh + P(3));
        ImGui.PushID("shape");
        int n = 0;
        foreach (var it in r.Shape)
        {
            ImGui.PushID(n++);
            float ty = y + (rowH - font.FontSize) / 2, cy = y + (rowH - fh) / 2;
            switch (it)
            {
                case ShapeSeg s:
                    Text(dl, font, new Vector2(lx, ty), T.Muted, s.Label);
                    ImGui.SetCursorScreenPos(new Vector2(cx, cy));
                    ImGui.PushFont(font);
                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, 0));
                    for (int i = 0; i < s.Row.Knob.Values.Length; i++)
                    {
                        if (i > 0)
                            ImGui.SameLine();
                        if (Seg(s.Row.Labels[i], s.Row.Index == i) && s.Row.Index != i)
                            Edit(s.Row.Knob.Set(d, s.Row.Knob.Values[i]), $"{s.Row.Knob.Label}: {s.Row.Labels[i]}");
                        if (ImGui.IsItemHovered() && i < s.Tips.Length)
                            Tip(s.Tips[i]);
                    }
                    ImGui.PopStyleVar();
                    ImGui.PopFont();
                    break;
                case ShapeNum s:
                    Text(dl, font, new Vector2(lx, ty), T.Muted, s.Label);
                    ImGui.SetCursorScreenPos(new Vector2(cx, cy));
                    Spinner(s.Row, P(50), extras: false);
                    float nx = ImGui.GetItemRectMax().X + P(8);
                    if (s.Row.Knob.CanAuto)
                    {
                        ImGui.SetCursorScreenPos(new Vector2(nx, cy));
                        ImGui.PushFont(font);
                        if (s.Row.IsAuto)
                        {
                            Text(dl, font, new Vector2(nx, ty), T.Muted, "auto");
                            nx += Width(font, "auto") + P(8);
                        }
                        else
                        {
                            if (ImGui.SmallButton("auto"))
                            {
                                SetValue(s.Row.Knob, null);
                                doc.Seal();
                            }
                            if (ImGui.IsItemHovered())
                                Tip("Back to the engine's own value");
                            nx = ImGui.GetItemRectMax().X + P(6);
                        }
                        ImGui.PopFont();
                    }
                    if (s.Note.Length > 0)
                        Text(dl, font, new Vector2(nx, ty), T.Muted, s.Note);
                    break;
                case ShapeAdd s:
                    Text(dl, font, new Vector2(lx, ty), T.Muted, s.Label);
                    {
                        float cw = Width(font, s.Chip) + P(10);
                        var a = new Vector2(cx, y + (rowH - P(14)) / 2);
                        var b = a + new Vector2(cw, P(14));
                        ImGui.SetCursorScreenPos(a);
                        bool click = ImGui.InvisibleButton("##add", b - a);
                        bool hov = ImGui.IsItemHovered();
                        dl.AddRectFilled(a, b, T.U32(T.Brass with { W = hov ? 0.16f : 0.05f }), P(2));
                        dl.AddRect(a, b, T.U32(hov ? T.Brass : T.BrassDark), P(2));
                        Text(dl, font, new Vector2(a.X + P(5), a.Y + (P(14) - font.FontSize) / 2), hov ? new Vector4(1, 0.906f, 0.659f, 1) : T.Brass, s.Chip);
                        if (hov)
                            Tip(s.Tip);
                        if (click)
                            s.Add();
                    }
                    break;
                case ShapeRead s:
                    Text(dl, font, new Vector2(lx, ty), T.Muted, s.Label);
                    Text(dl, UiFonts.Mono, new Vector2(cx, y + (rowH - UiFonts.Mono.FontSize) / 2), s.Remove != null ? T.Muted : T.Text, s.Text);
                    if (s.Remove != null)
                    {
                        ImGui.SetCursorScreenPos(new Vector2(cx + Width(UiFonts.Mono, s.Text) + P(6), cy));
                        ImGui.PushFont(font);
                        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                        ImGui.PushStyleColor(ImGuiCol.Text, T.Muted);
                        if (ImGui.SmallButton("x"))
                            s.Remove();
                        ImGui.PopStyleColor(2);
                        ImGui.PopFont();
                        if (ImGui.IsItemHovered())
                            Tip(s.RemoveTip);
                    }
                    break;
                case ShapeNote s:
                {
                    ImGui.SetCursorScreenPos(new Vector2(lx, y + P(3)));
                    ImGui.PushTextWrapPos(pos.X + size.X - ImGui.GetWindowPos().X);
                    ImGui.PushFont(font);
                    ImGui.TextColored(s.Warn ? T.Amber : T.Muted, s.Text);
                    ImGui.PopFont();
                    ImGui.PopTextWrapPos();
                    y = ImGui.GetItemRectMax().Y + P(2) - rowH;
                    break;
                }
            }
            ImGui.PopID();
            y += rowH;
        }
        ImGui.PopID();
        if (r.Shape.Length == 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(lx, y));
            ImGui.PushFont(font);
            ImGui.TextColored(T.Muted, "Its values are in the table.");
            ImGui.PopFont();
        }
        ImGui.PopClipRect();
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
        ImGui.TextColored(T.Muted, r.Full);
        ImGui.Separator();
        var a = d.Armour;
        if (r.Ref.Kind is PK.Belt or PK.BeltZone)
        {
            if (ImGui.MenuItem(a?.BeltMagazinesMm != null ? "Remove the zone at the magazines" : "Zone at the magazines"))
            {
                if (a?.BeltMagazinesMm != null)
                    RemoveSub(new PRef(PK.BeltZone));
                else
                    AddSub(new PRef(PK.BeltZone));
            }
            bool inside = a?.BeltPosition == "internal";
            if (ImGui.MenuItem(inside ? "On the side" : "Inside the shell"))
                Edit(Knobs.BeltPosition.Set(d, inside ? null : "internal"), inside ? "Belt on the side" : "Belt inside the shell");
            bool mach = a?.BeltExtent == "machinery";
            if (ImGui.MenuItem(mach ? "Over the citadel" : "Over the machinery only"))
                Edit(Knobs.BeltExtent.Set(d, mach ? null : "machinery"), mach ? "Belt over the citadel" : "Belt over the machinery only");
        }
        if (r.Ref.Kind is PK.Deck or PK.Slope or PK.DeckZone && r.Ref.I >= 0 && PE.CitadelDeck(d, r.Ref.I))
        {
            var x = a!.Decks![r.Ref.I];
            if (ImGui.MenuItem(x.SlopeMm is > 0 ? "Remove the slopes" : "Slopes to the belt's lower edge"))
            {
                if (x.SlopeMm is > 0)
                    RemoveSub(new PRef(PK.Slope, r.Ref.I));
                else
                    AddSub(new PRef(PK.Slope, r.Ref.I));
            }
            if ((x.Extent ?? "citadel") == "citadel" && ImGui.MenuItem(x.MagazinesMm != null ? "Remove the zone at the magazines" : "Zone at the magazines"))
            {
                if (x.MagazinesMm != null)
                    RemoveSub(new PRef(PK.DeckZone, r.Ref.I));
                else
                    AddSub(new PRef(PK.DeckZone, r.Ref.I));
            }
            ImGui.Separator();
        }
        if (r.Extent is { } ek && !r.Sub)
            for (int i = 0; i < ExtentValues.Length; i++)
                if (ImGui.MenuItem($"Over {(ExtentValues[i] == "ends" ? "" : "the ")}{PE.ExtentText(ExtentValues[i])}", r.ExtentNow == ExtentValues[i] ? "now" : null, false,
                        r.ExtentNow != ExtentValues[i]))
                    Edit(ek.Set(d, ExtentValues[i]), $"{r.Name}: over the {PE.ExtentText(ExtentValues[i])}");
        if (r.Ref.Kind == PK.Deck && r.Ref.I < (a?.Decks?.Count ?? 0))
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
                var x = a!.Decks![r.Ref.I];
                Edit(PE.AddDeck(d, (int)stack[k + 1], x.Extent ?? "citadel", Math.Max(5, Math.Round((x.Mm ?? 0) / 10) * 5), out int i),
                    $"Armour deck added: {Decks.DeckNameLower(stack[k + 1])}");
                protSel = new PRef(PK.Deck, i);
            }
        }
        if (r.Ref.Kind is PK.EndFore or PK.EndAft)
        {
            string end = r.Ref.Kind == PK.EndFore ? "fore" : "aft";
            double mm = a?.EndBelts?.Of(end)?.Mm ?? 0;
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

    /// <summary>The dev hook's protection states: protsel:ID (belt, beltzone, upper, end_fore, end_aft, bulk, tds, inner,
    /// box, wings, deckN, slopeN, dzoneN, steer, mainN, barb, ct), protmenu, protcover:N, protghost:DECK:EXT,
    /// prothandle:N, protsechandle:N, protcut:K:STOP (K 1 or 2; STOP mach, mag:A, fore, aft or x in m), protlink,
    /// protlayout (the menu open), prottip:layout:N | cell:K:N | comp, protcomp, protrooms:0.</summary>
    bool ApplyProtDevUi(string[] p)
    {
        switch (p[0])
        {
            case "protsel" when p.Length > 1:
                string id = p[1];
                int num = int.TryParse(new string(id.Where(char.IsDigit).ToArray()), out int v) ? v : 0;
                protSel = id switch
                {
                    "beltzone" => new PRef(PK.BeltZone),
                    "upper" => new PRef(PK.Upper),
                    "end_fore" => new PRef(PK.EndFore),
                    "end_aft" => new PRef(PK.EndAft),
                    "bulk" => new PRef(PK.Bulkheads),
                    "tds" => new PRef(PK.Tds),
                    "inner" => new PRef(PK.Inner),
                    "box" => new PRef(PK.Box),
                    "wings" => new PRef(PK.Wings),
                    "steer" => new PRef(PK.Steering),
                    "barb" => new PRef(PK.Barbettes),
                    "ct" => new PRef(PK.Ct),
                    _ when id.StartsWith("deck", StringComparison.Ordinal) => new PRef(PK.Deck, num),
                    _ when id.StartsWith("slope", StringComparison.Ordinal) => new PRef(PK.Slope, num),
                    _ when id.StartsWith("dzone", StringComparison.Ordinal) => new PRef(PK.DeckZone, num),
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
            case "protcut" when p.Length > 2:
            {
                int k = p[1] == "2" ? 1 : 0;
                string stop = string.Join(':', p[2..]);
                if (float.TryParse(stop, NumberStyles.Float, CultureInfo.InvariantCulture, out float xs))
                    (protCutStop[k], protCutX[k]) = (null, xs);
                else
                    protCutStop[k] = stop;
                if (protLinked)
                    (protCutStop[1 - k], protCutX[1 - k]) = (protCutStop[k], protCutX[k]);
                protCutsDoc = doc;   // the defaults for a new design leave these cuts alone
                return true;
            }
            case "protlink":
                protCutsDoc = doc;
                ToggleLinked();
                return true;
            case "protlayout":
                openLayoutMenu = true;
                return true;
            case "prottip" when p.Length > 1:
                protForceTip = string.Join(':', p[1..]);
                return true;
            case "protcomp":
                protComp = true;
                return true;
            case "protrooms":
                protRooms = p.Length < 2 || p[1] != "0";
                return true;
        }
        return false;
    }
}
