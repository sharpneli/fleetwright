namespace Fleetwright.Shipgen;

/// <summary>A design's armour deck placed on the deck stack: the deck it lies on (asked: the one the design named),
/// its thickness, extent and height above the keel.</summary>
public sealed record PlannedArmourDeck(long Deck, double Mm, string Extent, double Z, long Asked, string? Material)
{
    /// <summary>Its plate over the magazines (a citadel deck's, protection-plan.md H5), or null.</summary>
    public double? MagazinesMm { get; init; }
    /// <summary>Its slopes' plate (0: flat to the side) and angle from the horizontal (protection-plan.md H6).</summary>
    public double SlopeMm { get; init; }
    public double SlopeDeg { get; init; } = Armour.SlopeDegDefault;
}

/// <summary>An armour deck over one stretch: Extent citadel, full, fore, aft or steering; W a narrower width than the
/// beam (the steering box).</summary>
public sealed class ArmourDeck
{
    public long Deck;
    public double Mm;
    public string Extent = "";
    public double Z;
    public long Asked;
    public string? Material;
    public double X0, X1;
    public double? W;
    /// <summary>Its plate over the magazines, while the pieces are gathered (protection-plan.md H5); null: Mm.</summary>
    public double? MagazinesMm;
    /// <summary>Its slopes (protection-plan.md H6): SlopeMm thick at SlopeDeg, down to the side at FootZ (above the keel);
    /// the flat stands Reach in from the side as measured there. Reach 0: flat to the side.</summary>
    public double SlopeMm, SlopeDeg = Armour.SlopeDegDefault, FootZ, Reach;

    public ArmourDeck Clone() => (ArmourDeck)MemberwiseClone();
}

/// <summary>An inclined longitudinal armour plate each side (protection-plan.md H6): a deck's slope, or a belt inside the
/// hull. It runs over X0..X1 from Z0 up to Z1 (above the keel), U0 in from the side at Z0 and U1 at Z1, linear between,
/// the side measured at SideZ (so it follows the hull along its length but not its flare). Mm thick, BottomMm at Z0
/// tapering from TaperFrom down (null: one thickness). Kind slope, belt or zone (a belt's over the magazines).</summary>
public sealed record InclinedPlate(string Id, string Kind, double X0, double X1, double Z0, double Z1, double U0, double U1, double SideZ,
    double Mm, string? Material)
{
    public double? BottomMm { get; init; }
    public double? TaperFrom { get; init; }
}

/// <summary>A strake of side armour: Kind end, upper or box (the steering gear's); Mm at its root tapering to TipMm at
/// the hull's end. The main belt, as the subdivision sees it, also has BottomMm below the waterline Wl.</summary>
public sealed record Strake(string Id, string Kind, string Extent, double Mm, double TipMm, double X0, double X1, double Bottom,
    double Top, string? Material)
{
    public double? BottomMm { get; init; }
    public double? Wl { get; init; }
}

/// <summary>An armoured transverse bulkhead at x; W narrower than the beam (the steering box). Mm down to LowerTop,
/// LowerMm below it (the citadel's under its belt); LowerTop is Bottom when it's one thickness.</summary>
public sealed record ArmourBulkhead(string Id, double X, double Mm, double Bottom, double Top, double? W, string? Material)
{
    public double LowerMm { get; init; } = Mm;
    public double LowerTop { get; init; } = Bottom;
}

/// <summary>Where the armour is: the citadel (X0..X1), the belt, the armour decks and strakes, the bulkheads. Heights
/// above the keel. MainZ: the main armour deck's height; RoofZ: the lowest armour deck over the citadel.</summary>
public sealed class ArmourLayout
{
    public double X0, X1, BeltMm, BeltBottomMm, Waterline, BeltBottom, BeltTop;
    /// <summary>Where the main belt runs: the citadel, or the machinery only (armour.belt_extent).</summary>
    public double BeltX0, BeltX1;
    /// <summary>The belt's lean off the upright, its top outboard, and whether it stands inside the shell (then it and its
    /// zones are inclined plates, in Inclined; protection-plan.md H6).</summary>
    public double BeltInclineDeg;
    public bool BeltInternal;
    public List<ArmourDeck> Decks = [];
    public List<Strake> Strakes = [];
    public double? MainZ, RoofZ;
    public double RoofMm;
    public string? RoofMaterial, BeltMaterial, BulkheadMaterial;
    public bool Armoured;
    /// <summary>The citadel's bulkheads: BulkheadMm from BulkheadTop down to BulkheadStep (the belt's lower edge),
    /// BulkheadLowerMm below it down to BulkheadBottom (the inner bottom).</summary>
    public double BulkheadMm, BulkheadLowerMm, BulkheadBottom, BulkheadStep, BulkheadTop;
    public List<ArmourBulkhead> EndBulkheads = [];
    /// <summary>The end belts' plates across the hull's end face (a transom), where a belt runs all the way to it: the
    /// belt carried round the stern, as the complete belts were. Not bulkheads: the subdivision doesn't stop at them.</summary>
    public List<ArmourBulkhead> EndPlates = [];
    /// <summary>The inclined plates: the decks' slopes, an internal belt (protection-plan.md H6).</summary>
    public List<InclinedPlate> Inclined = [];
}

/// <summary>The design's armour: its validation, where the armour is (the one source for its weights, the subdivision
/// and the hitboxes), its weights, the materials, and the warnings. Heights above the keel.</summary>
public static class Armour
{
    public const double TdsMmPerM = 12.0;
    /// <summary>The magazine wing's default width, a share of the beam (protection-plan.md H2) [INFERRED].</summary>
    public const double MagazineWingShare = 0.1;
    /// <summary>A deck's slopes' angle from the horizontal when the design gives none (protection-plan.md H6): the
    /// research's 25-45° and Bismarck's 22°, about the middle [INFERRED].</summary>
    public const double SlopeDegDefault = 30.0;
    /// <summary>A slope needs at least this much height between the flat and the belt's lower edge, m; lower, the deck
    /// stays flat to the side (with a warning).</summary>
    public const double MinSlopeRiseM = 0.3;
    /// <summary>How far inside the shell an internal belt's top stands, m: the side's framing [INFERRED].</summary>
    public const double InternalBeltInsetM = 0.3;
    /// <summary>An internal belt's lower belt, its plane carried on down to the inner bottom (South Dakota's, Iowa's): the
    /// belt's bottom thickness at its top, tapering to this at its foot, mm (South Dakota's 1 in) [INFERRED].</summary>
    public const double LowerBeltFootMm = 25.0;

    /// <summary>The torpedo protection's layers (protection-plan.md H3): its depth each side, how many spaces, the
    /// holding bulkhead's plate and each layer wall's, mm. One layer by default: one wall carrying all its steel.</summary>
    public static (double DepthM, int Layers, double HoldingMm, double LayerMm) Tds(Design design)
    {
        var a = design.Armour;
        double depth = a?.TdsM ?? 0.0, steel = TdsMmPerM * depth;
        int n = Math.Max(1, a?.TdsLayers ?? 1);
        double holding = a?.TdsHoldingMm ?? (n == 1 ? steel : steel / 2);
        double layer = n == 1 ? 0.0 : a?.TdsLayerMm ?? Math.Max(0.0, steel - holding) / (n - 1);
        return (depth, n, holding, layer);
    }

    /// <summary>The inner bulkhead behind the belt (protection-plan.md H4): its armour over the citadel and beside the
    /// magazines, mm, and how far in from the side it stands where nothing else puts a wall (null: the magazine wing's).</summary>
    public static (double Mm, double MagazinesMm, double? InboardM) InnerBulkhead(Design design)
    {
        var ib = design.Armour?.InnerBulkhead;
        double mm = Math.Max(0.0, ib?.Mm ?? 0.0);
        return (mm, Math.Max(0.0, ib?.MagazinesMm ?? mm), ib?.InboardM);
    }

    /// <summary>Does any armour go by where the magazines are: a belt or citadel deck zone, an inner bulkhead thicker or
    /// thinner beside them, a magazine box.</summary>
    public static bool GoesByMagazines(Design design)
    {
        var a = design.Armour;
        if (a is null)
            return false;
        var (mm, mags, _) = InnerBulkhead(design);
        var box = MagazineBox(design);
        return a.BeltMagazinesMm is double bm && bm != (a.BeltMm ?? 0) || (a.Decks ?? []).Any(d => d.MagazinesMm is double m && m != (d.Mm ?? 0))
               || mags != mm || box.SideMm + box.CrownMm + box.EndMm + box.FloorMm > 0;
    }

    /// <summary>The wing between a magazine and the side, each side, m.</summary>
    public static double MagazineWingM(Design design, double B) => Math.Max(0.0, design.Armour?.MagazineWingM ?? MagazineWingShare * B);
    const double BeltHA = 0.30, BeltHB = 2.4;
    /// <summary>The citadel bulkheads' thickness under the belt, as a share of theirs above. A shell gets that low at
    /// the citadel's end only by falling through the unarmoured end (lower, it meets water first), so it strikes the
    /// upright plate at least as obliquely as it falls: a placeholder until the practice is researched.</summary>
    public const double BulkheadLowerShare = 0.5;
    static readonly string[] ArmourExtents = ["citadel", "full", "fore", "aft", "ends"];
    static readonly string[] BeltEnds = ["fore", "aft"];
    static readonly string[] ArmourParts = ["belt", "upper_belt", "end_belts", "bulkheads", "decks", "turrets", "barbettes",
        "conning_tower", "secondary", "flight_deck"];

    /// <summary>Extents that may share one deck (different stretches of it).</summary>
    static bool SharedDeck(string? a, string? b)
    {
        (string, string)[] pairs = [("citadel", "fore"), ("citadel", "aft"), ("citadel", "ends"), ("fore", "aft")];
        return pairs.Any(p => a == p.Item1 && b == p.Item2 || a == p.Item2 && b == p.Item1);
    }

    public static List<string> ArmourErrors(Design design)
    {
        var a = design.Armour ?? new ArmourInput();
        var errs = new List<string>();
        var extra = a.Extra.KeysOrEmpty().ToList();
        if (extra.Contains("deck_mm"))
            errs.Add("armour.deck_mm is gone: list the armour decks top down in armour.decks, e.g. " +
                     "[{\"deck\": 1, \"mm\": 152, \"extent\": \"citadel\"}]");
        if (extra.Contains("turret_mm"))
            errs.Add("armour.turret_mm is gone: give each main battery its turrets' armour_mm (main[k].armour_mm)");
        var decks = a.Decks ?? [];
        long? last = null;
        string? prev = null;
        for (int k = 0; k < decks.Count; k++)
        {
            var d = decks[k];
            if (d.Deck is not int deck)
            {
                errs.Add($"armour.decks[{k}].deck: use a deck number (0 the main deck, 1 the second deck, ..., -1 " +
                         "the first raised deck)");
                continue;
            }
            if (!(d.Mm >= 0))
                errs.Add($"armour.decks[{k}].mm: use a thickness of 0 or more");
            if (d.MagazinesMm is double mg && !(mg >= 0))
                errs.Add($"armour.decks[{k}].magazines_mm: use a thickness of 0 or more");
            if (d.SlopeMm is double sm && !(sm >= 0))
                errs.Add($"armour.decks[{k}].slope_mm: use a thickness of 0 or more");
            if (d.SlopeDeg is double sd && !(sd > 0 && sd < 90))
                errs.Add($"armour.decks[{k}].slope_deg: use an angle from the horizontal over 0 and under 90");
            if (!ArmourExtents.Contains(d.Extent))
                errs.Add($"armour.decks[{k}].extent = {Style.Quote(d.Extent)}: use {string.Join(" or ", ArmourExtents)}");
            if (last is long l && deck < l)
                errs.Add($"armour.decks[{k}]: list the armour decks top down");
            else if (last is long l2 && deck == l2 && !SharedDeck(d.Extent, prev))
                errs.Add($"armour.decks[{k}]: a deck may appear twice only over different stretches (the citadel " +
                         "and its ends)");
            last = last is long l3 ? Math.Max(l3, deck) : deck;
            prev = d.Extent;
        }
        if (a.BeltExtent is { } be && be is not ("citadel" or "machinery"))
            errs.Add($"armour.belt_extent = {Style.Quote(be)}: use citadel or machinery");
        if (a.BeltPosition is { } bp && bp is not ("external" or "internal"))
            errs.Add($"armour.belt_position = {Style.Quote(bp)}: use external or internal");
        if (a.BeltInclineDeg is double bi && !(bi >= 0 && bi < 60))
            errs.Add("armour.belt_incline_deg: use a lean off the upright of 0 or more and under 60");
        if (a.UpperBelt is { } ub && !ArmourExtents.Contains(ub.Extent ?? "citadel"))
            errs.Add($"armour.upper_belt.extent = {Style.Quote(ub.Extent)}: use {string.Join(" or ", ArmourExtents)}");
        if (a.Materials is { } md)
        {
            errs.AddRange(md.Keys.Where(k => !ArmourParts.Contains(k))
                .Select(k => $"armour.materials.{k}: not an armour part ({string.Join(", ", ArmourParts)})"));
            errs.AddRange(md.Where(kv => kv.Value.Length == 0).Select(kv => $"armour.materials.{kv.Key}: name the material as a string"));
        }
        var owns = new List<(string Where, string? Material)>();
        for (int k = 0; k < decks.Count; k++)
            owns.Add(($"armour.decks[{k}]", decks[k].Material));
        if (a.UpperBelt is { } ub2)
            owns.Add(("armour.upper_belt", ub2.Material));
        foreach (var end in BeltEnds)
            if (a.EndBelts?.Of(end) is { } ev)
                owns.Add(($"armour.end_belts.{end}", ev.Material));
        if (a.SteeringBox is { } sb)
        {
            owns.Add(("armour.steering_box", sb.Material));
            owns.Add(("armour.steering_box.deck", sb.DeckMaterial));
        }
        foreach (var (g, bl) in new[] { ("secondary", design.Secondary ?? []), ("main", design.Main ?? []) })
            for (int k = 0; k < bl.Count; k++)
                owns.Add(($"{g}[{k}]", bl[k].Material));
        errs.AddRange(owns.Where(o => o.Material is "").Select(o => $"{o.Where}.material: name the material as a string"));
        if (a.EndBelts?.Extra is { Count: > 0 })
            errs.Add("armour.end_belts: use {\"fore\": {\"mm\", \"tip_mm\", \"reach\", \"bulkhead_mm\"}, \"aft\": {...}}");
        if (a.SteeringBox?.Extra is { Count: > 0 })
            errs.Add("armour.steering_box: use {\"mm\", \"deck_mm\", \"bulkhead_mm\"} (0 for none)");
        if (a.MagazineBox?.Extra is { Count: > 0 })
            errs.Add("armour.magazine_box: use {\"side_mm\", \"crown_mm\", \"end_mm\", \"floor_mm\"} (0 for none)");
        if (a.InnerBulkhead?.Extra is { Count: > 0 })
            errs.Add("armour.inner_bulkhead: use {\"mm\", \"magazines_mm\", \"inboard_m\"} (0 for none)");
        return errs;
    }

    /// <summary>The armour material named for a part (a deck or battery may give its own), or null.</summary>
    public static string? ArmourMaterial(Design design, string part, string? own = null) =>
        !string.IsNullOrEmpty(own) ? own : design.Armour?.Materials?.GetValueOrDefault(part);

    /// <summary>The design's armour decks (top down), placed on the deck stack.</summary>
    public static List<PlannedArmourDeck> ArmourDecks(Design design, double D, IReadOnlyList<RaisedStretch> raised,
        List<(long N, double Z)>? stack = null)
    {
        stack ??= Decks.DeckStack(design, D);
        long top = -raised.Select(s => s.Levels).DefaultIfEmpty(0L).Max();
        var result = new List<PlannedArmourDeck>();
        foreach (var d in design.Armour?.Decks ?? [])
        {
            long asked = d.Deck ?? 0;
            long n = Math.Max(top, Math.Min(asked, stack[^1].N));
            result.Add(new PlannedArmourDeck(n, d.Mm ?? 0, d.Extent ?? "citadel", n >= 0 ? stack[(int)n].Z : D - n * Geometry.DeckPitch,
                asked, ArmourMaterial(design, "decks", d.Material))
                { MagazinesMm = d.MagazinesMm, SlopeMm = Math.Max(0.0, d.SlopeMm ?? 0.0), SlopeDeg = d.SlopeDeg ?? SlopeDegDefault });
        }
        return result;
    }

    /// <summary>An armour extent as [(extent, x0, x1)] pieces.</summary>
    public static List<(string Ext, double X0, double X1)> ExtentSpans(string extent, double L, double x0, double x1) => extent switch
    {
        "citadel" => [("citadel", x0, x1)],
        "full" => [("full", -L / 2, L / 2)],
        "fore" => [("fore", x1, L / 2)],
        "aft" => [("aft", -L / 2, x0)],
        "ends" => [("fore", x1, L / 2), ("aft", -L / 2, x0)],
        _ => throw new ArgumentOutOfRangeException(nameof(extent), extent, "unknown armour extent"),
    };

    /// <summary>A belt's thickness at x (and height z): mm at its root tapering to tip_mm at the hull's end; a belt with
    /// bottom_mm keeps its thickness down to the waterline, then tapers to bottom_mm at its lower edge.</summary>
    public static double BeltMmAt(Strake s, double x, double? z = null)
    {
        double mm = s.Mm;
        if (s.TipMm != mm && s.X1 - s.X0 > 0)
        {
            double f = (x - s.X0) / (s.X1 - s.X0);
            if (s.Extent == "aft")
                f = 1.0 - f;
            mm += (s.TipMm - mm) * Math.Min(1.0, Math.Max(0.0, f));
        }
        if (z is double zz && s.BottomMm is double bm && bm != mm && s.Wl is double wl && zz < wl && wl > s.Bottom)
        {
            double f = Math.Max(0.0, (zz - s.Bottom) / (wl - s.Bottom));
            mm = bm + (mm - bm) * f;
        }
        return mm;
    }

    static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    /// <summary>Where the armour is: the one source for its weights and its hitboxes. Heights above the keel.</summary>
    public static ArmourLayout ArmourGeometry(Design design, double L, double T, double D, Geo geo)
    {
        var a = design.Armour ?? new ArmourInput();
        double belt = a.BeltMm ?? 0;
        var (x0, x1) = geo.CitadelSpan(L);
        var raised = geo.Raised;
        var stack = Decks.DeckStack(design, D);
        var decks = new List<ArmourDeck>();
        foreach (var d in ArmourDecks(design, D, raised, stack))
        {
            if (d.Mm <= 0)
                continue;
            var spans = ExtentSpans(d.Extent, L, x0, x1);
            long dn = d.Deck;
            if (dn < 0)
                spans = spans.SelectMany(s => Decks.RaisedPieces(raised, s.X0, s.X1, -dn)
                    .Where(p => p.Lv >= -dn).Select(p => (s.Ext, p.X0, p.X1))).ToList();
            foreach (var (ext, p0, p1) in spans)
            {
                ArmourDeck? p = null;
                foreach (var q in decks)
                    if (q.Deck == dn && Math.Min(q.X1, p1) - Math.Max(q.X0, p0) > 1e-6)
                    {
                        p = q;
                        break;
                    }
                if (p != null)
                {
                    string? pm = p.Material, dm = d.Material;
                    if (p.MagazinesMm is not null || d.MagazinesMm is not null)
                        p.MagazinesMm = (p.MagazinesMm ?? p.Mm) + (d.MagazinesMm ?? d.Mm);
                    p.Mm += d.Mm;
                    (p.X0, p.X1) = (Math.Min(p.X0, p0), Math.Max(p.X1, p1));
                    p.Extent = p.Extent == "full" || ext == "full" ? "full" : p.Extent;
                    p.Material = dm is null || dm == pm ? pm : pm is null ? dm : $"{pm} + {dm}";
                    if (d.SlopeMm > 0 && dn >= 0)
                        (p.SlopeMm, p.SlopeDeg) = (p.SlopeMm + d.SlopeMm, p.SlopeMm > 0 ? p.SlopeDeg : d.SlopeDeg);
                    continue;
                }
                // (a raised deck has no slopes: its side is above the belt)
                decks.Add(new ArmourDeck { Deck = dn, Mm = d.Mm, Extent = ext, Z = d.Z, Asked = d.Asked, Material = d.Material, X0 = p0, X1 = p1,
                    MagazinesMm = ext == "citadel" ? d.MagazinesMm : null, SlopeMm = dn >= 0 ? d.SlopeMm : 0.0, SlopeDeg = d.SlopeDeg });
            }
        }
        var over = decks.Where(d => d.Extent is "citadel" or "full" && d.Deck >= 0).ToList();
        var main = over.MaxBy(d => (d.Mm, d.Z));
        var roof = over.MinBy(d => d.Z);
        // zones (protection-plan.md H5): a citadel deck and the belt thicker (or thinner) beside the magazines, where the
        // last layout pass stowed them
        var mags = MergedSpans(geo.MagazineSpans);
        decks = decks.SelectMany(d => Zoned(d, mags)).ToList();
        double h0 = BeltHA * T + BeltHB;
        double below = a.BeltDepthM ?? h0 / 2, above = a.BeltHeightM ?? h0 / 2;
        double h = below + above;
        double bot = Math.Max(0.0, T - below);
        double band = Math.Min(D, Math.Max(bot, T + above));
        // the slopes (protection-plan.md H6): from the flat's edge down to the side at the belt's lower edge (on a beltless
        // ship as deep as a belt would reach); each piece of a zoned deck has its own
        var inclined = new List<InclinedPlate>();
        foreach (var d in decks)
        {
            if (d.SlopeMm <= 0)
                continue;
            if (d.Z - bot < MinSlopeRiseM)
            {
                d.SlopeMm = 0;
                continue;
            }
            (d.FootZ, d.Reach) = (bot, (d.Z - bot) / Math.Tan(double.DegreesToRadians(d.SlopeDeg)));
            string name = $"Slopes ({Decks.DeckNameLower(d.Deck)}{(d.Extent is "fore" or "aft" ? $", {d.Extent}" : "")})";
            int same = inclined.Count(p => p.Id == name || p.Id.StartsWith(name + " ", StringComparison.Ordinal));
            inclined.Add(new InclinedPlate(same > 0 ? $"{name} {same + 1}" : name, "slope", d.X0, d.X1, bot, d.Z, 0.0, d.Reach, bot, d.SlopeMm,
                d.Material));
        }
        double top = Math.Min(D, Math.Max(band, main?.Z ?? 0.0));

        var strakes = new List<Strake>();
        var (bx0, bx1) = a.BeltExtent == "machinery" && geo.Machinery is var (mx0, mx1) ? (Math.Max(x0, mx0), Math.Min(x1, mx1)) : (x0, x1);
        if (belt > 0 && a.BeltMagazinesMm is double bm && bm != belt)
        {
            double bb = a.BeltBottomMm ?? belt;
            int k = 0;
            foreach (var (m0, m1) in mags)
            {
                double s0 = Math.Max(bx0, m0), s1 = Math.Min(bx1, m1);
                if (s1 - s0 > 0.05)
                    strakes.Add(new Strake($"Belt (magazines){(mags.Count > 1 ? $" {++k}" : "")}", "zone", "citadel", bm, bm, s0, s1, bot, top,
                        ArmourMaterial(design, "belt")) { BottomMm = bm * bb / belt, Wl = T });
            }
        }
        // an internal belt (protection-plan.md H6): the belt and its zones inside the shell, leaning in toward their foot
        double incline = belt > 0 ? Math.Max(0.0, a.BeltInclineDeg ?? 0.0) : 0.0;
        bool inside = belt > 0 && a.BeltPosition == "internal";
        if (inside)
        {
            double u1 = InternalBeltInsetM, u0 = u1 + (top - bot) * Math.Tan(double.DegreesToRadians(incline)), bb = a.BeltBottomMm ?? belt;
            var zones = strakes.Where(s => s.Kind == "zone").OrderBy(s => s.X0).ToList();
            var pieces = new List<(double X0, double X1)>();
            double x = bx0;
            foreach (var zn in zones)
            {
                pieces.Add((x, zn.X0));
                x = zn.X1;
            }
            pieces.Add((x, bx1));
            pieces.RemoveAll(p => p.X1 - p.X0 <= 0.05);
            for (int k = 0; k < pieces.Count; k++)
                inclined.Add(new InclinedPlate(pieces.Count > 1 ? $"Belt {k + 1}" : "Belt", "belt", pieces[k].X0, pieces[k].X1, bot, top, u0, u1, top, belt,
                    ArmourMaterial(design, "belt")) { BottomMm = bb, TaperFrom = T });
            foreach (var zn in zones)
                inclined.Add(new InclinedPlate(zn.Id, "zone", zn.X0, zn.X1, bot, top, u0, u1, top, zn.Mm, zn.Material) { BottomMm = zn.BottomMm, TaperFrom = T });
            // the lower belt: the same plane on down to the inner bottom, so a shell falling into the space outboard of the
            // belt can't pass under its edge
            double ibz = Powerplant.DoubleBottom(D);
            if (bot - ibz > 0.1)
                inclined.Add(new InclinedPlate("Lower belt", "belt", bx0, bx1, ibz, bot, u0 + (bot - ibz) * Math.Tan(double.DegreesToRadians(incline)), u0, top,
                    bb, ArmourMaterial(design, "belt")) { BottomMm = Math.Min(bb, LowerBeltFootMm), TaperFrom = bot });
        }
        var endBhs = new List<ArmourBulkhead>();
        var endPlates = new List<ArmourBulkhead>();
        double bhBot = Math.Max(0.0, bot - 0.4 * h);
        var tops = new Dictionary<string, double>(StringComparer.Ordinal) { ["citadel"] = belt > 0 ? top : band };
        foreach (var end in BeltEnds)
        {
            var e = a.EndBelts?.Of(end) ?? new EndBeltInput();
            tops[end] = band;
            double emm = e.Mm ?? 0;
            if (emm <= 0)
                continue;
            var (_, s0, s1) = ExtentSpans(end, L, x0, x1)[0];
            double reach = Math.Min(1.0, Math.Max(0.0, e.Reach ?? 1.0));
            if (end == "fore")
                s1 = s0 + (s1 - s0) * reach;
            else
                s0 = s1 - (s1 - s0) * reach;
            double mid = (s0 + s1) / 2;
            var cover = decks.Where(d => d.X0 <= mid && mid <= d.X1 && (d.Extent == end || d.Extent == "full")).ToList();
            var dk = cover.MaxBy(d => (d.Mm, d.Z));
            double et = Math.Min(D, Math.Max(band, dk?.Z ?? 0.0));
            tops[end] = et;
            if (s1 - s0 > 1e-6)
            {
                strakes.Add(new Strake($"{Capitalize(end)} end belt", "end", end, emm, e.TipMm ?? emm, s0, s1, bot, et,
                    ArmourMaterial(design, "end_belts", e.Material)));
                if (reach < 1.0 && (e.BulkheadMm ?? 0) > 0)
                    endBhs.Add(new ArmourBulkhead($"{Capitalize(end)} end belt bulkhead", end == "fore" ? s1 : s0, e.BulkheadMm!.Value,
                        bhBot, et, null, ArmourMaterial(design, "bulkheads")));
                double hwEnd = geo.EndHalfWidths is var (ha, hf) ? end == "fore" ? hf : ha : 0.0;
                if (reach >= 1.0 && hwEnd > 0.01)
                    endPlates.Add(new ArmourBulkhead($"{Capitalize(end)} end belt, {(end == "fore" ? "stem" : "stern")} face",
                        end == "fore" ? L / 2 : -L / 2, e.TipMm ?? emm, bot, et, 2 * hwEnd, ArmourMaterial(design, "end_belts", e.Material)));
            }
        }
        var sb = a.SteeringBox ?? new SteeringBoxInput();
        double sbMm = sb.Mm ?? 0, sbDeck = sb.DeckMm ?? 0, sbBhd = sb.BulkheadMm ?? 0;
        if (Math.Max(sbMm, Math.Max(sbDeck, sbBhd)) > 0)
        {
            var (b0, b1) = geo.SteeringSpan(L);
            double? wbox = geo.SteeringBeam;
            double rz;
            using (Scratch<double>.Rent(out var zs))
            {
                foreach (var s in stack)
                    zs.Add(s.Z);
                rz = Ordnance.Span(zs, Powerplant.DoubleBottom(D), roof?.Z ?? D).Top + D;
            }
            // the deck nearest rz (MinBy: the first of equals)
            long n = stack[0].N;
            double best = Math.Abs(stack[0].Z - rz);
            foreach (var v in stack)
                if (Math.Abs(v.Z - rz).CompareTo(best) < 0)
                    (n, best) = (v.N, Math.Abs(v.Z - rz));
            if (sbDeck > 0)
                decks.Add(new ArmourDeck { Deck = n, Mm = sbDeck, Extent = "steering", Z = rz, Asked = n, X0 = b0, X1 = b1, W = wbox,
                    Material = ArmourMaterial(design, "decks", sb.DeckMaterial) });
            double floor = Math.Min(Powerplant.DoubleBottom(D), rz);
            if (sbMm > 0)
                strakes.Add(new Strake("Steering gear box", "box", "aft", sbMm, sbMm, b0, b1, floor, rz,
                    ArmourMaterial(design, "end_belts", sb.Material)));
            if (sbBhd > 0)
                foreach (var (w, x) in new[] { ("forward", b1), ("aft", b0) })
                    endBhs.Add(new ArmourBulkhead($"Steering gear box {w} bulkhead", x, sbBhd, floor, rz, wbox,
                        ArmourMaterial(design, "bulkheads")));
        }
        var ub = a.UpperBelt ?? new UpperBeltInput();
        if ((ub.Mm ?? 0) > 0)
        {
            long to = ub.ToDeck ?? 0;
            double ut = stack[(int)Math.Min(Math.Max(to, 0), stack[^1].N)].Z;
            var pieces = ExtentSpans(ub.Extent ?? "citadel", L, x0, x1);
            if (ub.Extent == "full")
                pieces = [("citadel", x0, x1), ("fore", x1, L / 2), ("aft", -L / 2, x0)];
            foreach (var (ext, s0, s1) in pieces)
            {
                var parts = to < 0 ? Decks.RaisedPieces(raised, s0, s1, -to) : [(s0, s1, 0L)];
                for (int k = 0; k < parts.Count; k++)
                {
                    var (p0, p1, lv) = parts[k];
                    double top_ = ut + lv * Geometry.DeckPitch;
                    if (top_ > tops[ext] + 0.05 && p1 - p0 > 1e-6)
                    {
                        string sid = ext == "citadel" ? "Upper belt" : $"Upper belt ({ext})";
                        strakes.Add(new Strake(sid + (parts.Count > 1 ? $" {k + 1}" : ""), "upper", ext, ub.Mm!.Value, ub.Mm.Value, p0, p1,
                            tops[ext], top_, ArmourMaterial(design, "upper_belt", ub.Material)));
                    }
                }
            }
        }
        // the bulkheads reach the belt's top or the citadel's upper belt (LINQ's Max: leading NaNs skipped), and down
        // to the inner bottom: a shell falling through an unarmoured end stays inside the dry hull, so stopping them
        // short of the vital spaces' floor would leave a way in under them. Under the belt they're thinner
        // (BulkheadLowerShare)
        double bhTop = top, citBot = Math.Min(bhBot, Powerplant.DoubleBottom(D)), bhMm = a.BulkheadMm ?? 0.6 * belt;
        foreach (var s in strakes)
            if (s.Kind == "upper" && s.Extent == "citadel" && (double.IsNaN(bhTop) || s.Top > bhTop))
                bhTop = s.Top;
        return new ArmourLayout
        {
            X0 = x0, X1 = x1, BeltX0 = bx0, BeltX1 = bx1, BeltInclineDeg = incline, BeltInternal = inside, BeltMm = belt, BeltBottomMm = a.BeltBottomMm ?? belt, Waterline = T, BeltBottom = bot, BeltTop = top,
            Decks = decks, Strakes = strakes, MainZ = main?.Z, RoofZ = roof?.Z, RoofMm = roof?.Mm ?? 0, RoofMaterial = roof?.Material,
            BeltMaterial = ArmourMaterial(design, "belt"), BulkheadMaterial = ArmourMaterial(design, "bulkheads"),
            Armoured = belt > 0 || over.Count > 0, BulkheadMm = bhMm, BulkheadLowerMm = BulkheadLowerShare * bhMm,
            BulkheadBottom = citBot, BulkheadStep = Math.Max(citBot, Math.Min(bot, bhTop)), BulkheadTop = bhTop,
            EndBulkheads = endBhs, EndPlates = endPlates, Inclined = inclined,
        };
    }

    /// <summary>The magazine box's plates, mm (protection-plan.md H5).</summary>
    public static (double SideMm, double CrownMm, double EndMm, double FloorMm) MagazineBox(Design design)
    {
        var b = design.Armour?.MagazineBox;
        return (Math.Max(0.0, b?.SideMm ?? 0.0), Math.Max(0.0, b?.CrownMm ?? 0.0), Math.Max(0.0, b?.EndMm ?? 0.0), Math.Max(0.0, b?.FloorMm ?? 0.0));
    }

    /// <summary>The magazine boxes' plates round the runs of magazines (heights above the main deck; protection-plan.md H5):
    /// their crowns and floors (Z above the keel, W their width) and their ends, one where two boxes meet. Their sides are
    /// the subdivision's wing walls.</summary>
    public static (List<ArmourDeck> Decks, List<ArmourBulkhead> Ends) BoxPlates(Design design,
        IEnumerable<(double X0, double X1, double HalfWidth, double Base, double Top)> runs, double D)
    {
        var decks = new List<ArmourDeck>();
        var ends = new List<ArmourBulkhead>();
        var mb = MagazineBox(design);
        if (mb.CrownMm + mb.EndMm + mb.FloorMm <= 0)
            return (decks, ends);
        int k = 0;
        foreach (var r in runs)
        {
            k++;
            double w = 2 * r.HalfWidth;
            if (mb.CrownMm > 0)
                decks.Add(new ArmourDeck { Deck = -1, Mm = mb.CrownMm, Extent = "box", Z = r.Top + D, X0 = r.X0, X1 = r.X1, W = w,
                    Material = ArmourMaterial(design, "decks") });
            if (mb.FloorMm > 0)
                decks.Add(new ArmourDeck { Deck = -1, Mm = mb.FloorMm, Extent = "box", Z = r.Base + D, X0 = r.X0, X1 = r.X1, W = w,
                    Material = ArmourMaterial(design, "decks") });
            if (mb.EndMm <= 0)
                continue;
            foreach (var (end, x) in new[] { ("forward", r.X1), ("aft", r.X0) })
            {
                // where two boxes meet, one plate between them, the taller's height and the wider's width
                int j = ends.FindIndex(e => Math.Abs(e.X - x) < 0.05);
                var plate = new ArmourBulkhead($"Magazine box {k} {end} end", x, mb.EndMm, r.Base + D, r.Top + D, w, ArmourMaterial(design, "bulkheads"));
                if (j < 0)
                    ends.Add(plate);
                else
                    ends[j] = ends[j] with { Bottom = Math.Min(ends[j].Bottom, plate.Bottom), Top = Math.Max(ends[j].Top, plate.Top), W = Math.Max(ends[j].W ?? 0, w) };
            }
        }
        return (decks, ends);
    }

    /// <summary>The magazines as runs of those that touch end to end on the same floor and under the same crown: each
    /// run's stretch, its widest half-width, floor and crown (above the main deck). From geo (the rooms' cells once a build
    /// has them, else Stow's magazines; for the weights) or the subdivision's (for the plates).</summary>
    public static List<(double X0, double X1, double HalfWidth, double Base, double Top)> MagazineRuns(Geo? geo) => MagazineRuns(geo?.Magazines ?? []);

    public static List<(double X0, double X1, double HalfWidth, double Base, double Top)> MagazineRuns(
        IEnumerable<(double X0, double X1, double HalfWidth, double Base, double Top)> mags)
    {
        var runs = new List<(double X0, double X1, double HalfWidth, double Base, double Top)>();
        foreach (var m in mags.OrderBy(m => m.X0))
        {
            if (runs.Count > 0 && m.X0 <= runs[^1].X1 + 0.05 && Math.Abs(m.Top - runs[^1].Top) < 0.05 && Math.Abs(m.Base - runs[^1].Base) < 0.05)
            {
                var r = runs[^1];
                runs[^1] = (r.X0, Math.Max(r.X1, m.X1), Math.Max(r.HalfWidth, m.HalfWidth), Math.Min(r.Base, m.Base), Math.Max(r.Top, m.Top));
            }
            else
                runs.Add(m);
        }
        return runs;
    }

    /// <summary>Stretches along the hull as their union, aft to forward.</summary>
    static List<(double X0, double X1)> MergedSpans(IEnumerable<(double X0, double X1)> spans)
    {
        var merged = new List<(double X0, double X1)>();
        foreach (var (m0, m1) in spans.OrderBy(m => m.X0))
        {
            if (merged.Count > 0 && m0 <= merged[^1].X1 + 1e-6)
                merged[^1] = (merged[^1].X0, Math.Max(merged[^1].X1, m1));
            else
                merged.Add((m0, m1));
        }
        return merged;
    }

    /// <summary>A deck piece cut where the magazines are when it's thicker or thinner over them: the pieces over the
    /// magazines take MagazinesMm (none at 0).</summary>
    static List<ArmourDeck> Zoned(ArmourDeck d, List<(double X0, double X1)> mags)
    {
        if (d.MagazinesMm is not double mm || mm == d.Mm)
            return [d];
        var result = new List<ArmourDeck>();
        void Piece(double a, double b, double t)
        {
            if (b - a > 0.05 && t > 0)
            {
                var piece = d.Clone();
                (piece.Mm, piece.X0, piece.X1, piece.MagazinesMm) = (t, a, b, null);
                result.Add(piece);
            }
        }
        double x = d.X0;
        foreach (var (m0, m1) in mags)
        {
            double a = Math.Max(d.X0, m0), b = Math.Min(d.X1, m1);
            if (b <= a)
                continue;
            Piece(x, a, d.Mm);
            Piece(a, b, mm);
            x = b;
        }
        Piece(x, d.X1, d.Mm);
        return result;
    }

    /// <summary>The citadel's two armoured bulkheads, forward then aft.</summary>
    public static ArmourBulkhead[] CitadelBulkheads(ArmourLayout g) =>
        [.. new[] { ("Forward", g.X1), ("Aft", g.X0) }.Select(t => new ArmourBulkhead($"{t.Item1} bulkhead", t.Item2, g.BulkheadMm, g.BulkheadBottom,
            g.BulkheadTop, null, g.BulkheadMaterial) { LowerMm = g.BulkheadLowerMm, LowerTop = g.BulkheadStep })];

    /// <summary>The armour's weights from its geometry.</summary>
    /// <param name="topside">the side above the widest point: a full-width armour deck is its breadth at its height,
    /// side armour as long as the side it lies on, an armoured bulkhead as wide as the hull up its height (null:
    /// wall-sided).</param>
    public static List<Weight> ArmourWeights(Design design, double L, double B, double D, ArmourLayout g, Topside? topside = null, Geo? geo = null)
    {
        double lc = g.X1 - g.X0;
        double xc = (g.X0 + g.X1) / 2;
        ZRel Zf(double lo, double hi) => ZRel.Frac(D != 0 ? (lo + hi) / 2 / D : 0.5);
        // heights here are over the keel; the topside's over the main deck
        double Slant(double lo, double hi) => topside?.SlantFactor(B, lo - D, hi - D) ?? 1.0;
        double Across(double x, double lo, double hi) => topside?.BandRatio(x, lo - D, hi - D) ?? 1.0;
        var result = new List<Weight>();
        // a belt mm thick (mb at its lower edge, tapering from the waterline down), len long: its weight and height. A
        // leaning belt is longer up its face (protection-plan.md H6): inside the shell by its lean alone, on the side by
        // that or the side's own slant, whichever is more
        double lean = 1 / Math.Cos(double.DegreesToRadians(g.BeltInclineDeg));
        double Lean(double lo, double hi) => g.BeltInternal ? lean : Math.Max(lean, Slant(lo, hi));
        Weight BeltOf(string name, double len, double x, double mm, double mb)
        {
            double bot = g.BeltBottom, top = g.BeltTop;
            double t0 = Math.Min(top, Math.Max(bot, g.Waterline));
            double aUp = (top - t0) * mm * Lean(t0, top), aLo = (t0 - bot) * (mm + mb) / 2 * Lean(bot, t0);
            double zLo = mb + mm > 0 ? bot + (t0 - bot) * (mb + 2 * mm) / (3 * (mb + mm)) : bot;
            double zc = aUp + aLo > 0 ? ((top + t0) / 2 * aUp + zLo * aLo) / (aUp + aLo) : (top + bot) / 2;
            return new Weight(name, "armour", 2 * len * (aUp + aLo) / 1000 * Weight.Steel, x, ZRel.Frac(D != 0 ? zc / D : 0.5));
        }
        if (g.BeltMm > 0)
        {
            double zones = g.Strakes.Where(s => s.Kind == "zone").Sum(s => s.X1 - s.X0);
            result.Add(BeltOf("Belt armour", Math.Max(0.0, g.BeltX1 - g.BeltX0 - zones), (g.BeltX0 + g.BeltX1) / 2, g.BeltMm, g.BeltBottomMm));
        }
        foreach (var s in g.Strakes.Where(s => s.Kind == "zone"))
            result.Add(BeltOf(s.Id, s.X1 - s.X0, (s.X0 + s.X1) / 2, s.Mm, s.BottomMm ?? s.Mm));
        foreach (var p in g.Inclined.Where(p => p.Id == "Lower belt"))
            result.Add(new Weight(p.Id, "armour", 2 * (p.X1 - p.X0) * (p.Z1 - p.Z0) * lean * (p.Mm + (p.BottomMm ?? p.Mm)) / 2 / 1000 * Weight.Steel, (p.X0 + p.X1) / 2,
                Zf(p.Z0, p.Z1)));
        if (g.Armoured && g.BulkheadMm > 0)
        {
            // full thickness down to the step, thinner under it
            double Part(double lo, double hi, double mm) => (Across(g.X0, lo, hi) + Across(g.X1, lo, hi)) / 2 * (hi - lo) * mm;
            double up = Part(g.BulkheadStep, g.BulkheadTop, g.BulkheadMm), low = Part(g.BulkheadBottom, g.BulkheadStep, g.BulkheadLowerMm);
            double zc = up + low > 0 ? ((g.BulkheadStep + g.BulkheadTop) / 2 * up + (g.BulkheadBottom + g.BulkheadStep) / 2 * low) / (up + low)
                : (g.BulkheadTop + g.BulkheadBottom) / 2;
            result.Add(new Weight("Bulkheads", "armour", 2 * B * (up + low) / 1000 * Weight.Steel, xc, ZRel.Frac(D != 0 ? zc / D : 0.5)));
        }
        foreach (var b in g.EndBulkheads.Concat(g.EndPlates))
            result.Add(new Weight(b.Id, "armour", (b.W is double w && w != 0 ? w : B * Across(b.X, b.Bottom, b.Top)) * (b.Top - b.Bottom) * b.Mm / 1000 * Weight.Steel, b.X,
                Zf(b.Top, b.Bottom)));
        foreach (var s in g.Strakes.Where(s => s.Kind != "zone"))
        {
            var (a, b) = s.Extent != "aft" ? (s.Mm, s.TipMm) : (s.TipMm, s.Mm);
            double f = a + b > 0 ? (a + 2 * b) / (3 * (a + b)) : 0.5;
            result.Add(new Weight(s.Id, "armour", 2 * (s.X1 - s.X0) * (s.Top - s.Bottom) * Slant(s.Bottom, s.Top) * (a + b) / 2 / 1000 * Weight.Steel,
                s.X0 + f * (s.X1 - s.X0), Zf(s.Top, s.Bottom)));
        }
        var box = MagazineBox(design);
        if (box.SideMm + box.CrownMm + box.EndMm + box.FloorMm > 0)
        {
            // each run's sides, crown, floor and ends, as Stow made the magazines: the plates stand on the subdivision's
            // cells, which may be longer (a short magazine takes its whole section) and a little wider, so this is a little under
            double t = 0, mx = 0, mz = 0;
            foreach (var r in MagazineRuns(geo))
            {
                // (an end where two boxes meet is weighed with each: a little over)
                double len = r.X1 - r.X0, h = r.Top - r.Base, w = 2 * r.HalfWidth;
                double one = (2 * len * h * box.SideMm + len * w * (box.CrownMm + box.FloorMm) + 2 * w * h * box.EndMm) / 1000 * Weight.Steel;
                (t, mx, mz) = (t + one, mx + one * (r.X0 + r.X1) / 2, mz + one * ((r.Base + r.Top) / 2 + D));
            }
            if (t > 0)
                result.Add(new Weight("Magazine boxes", "armour", t, mx / t, ZRel.Frac(D != 0 ? mz / t / D : 0.5)));
        }
        var (inMm, inMag, _) = InnerBulkhead(design);
        if (g.Armoured && lc > 0 && Math.Max(inMm, inMag) > 0)
        {
            // each side over the citadel, from the inner bottom to the armour deck; thicker beside the magazines
            double floor = Powerplant.DoubleBottom(D), top = g.RoofZ ?? g.Waterline;
            // their union: magazines side by side or one over another share a stretch of wall
            double mag = 0;
            foreach (var (m0, m1) in MergedSpans(geo?.MagazineSpans ?? []))
                mag += Math.Max(0.0, Math.Min(g.X1, m1) - Math.Max(g.X0, m0));
            mag = Math.Min(mag, lc);
            double area = 2 * Math.Max(0.0, top - floor);
            result.Add(new Weight("Inner bulkhead", "armour", area * ((lc - mag) * inMm + mag * inMag) / 1000 * Weight.Steel, xc, Zf(top, floor)));
        }
        var (tds, layers, holding, layerMm) = Tds(design);
        if (tds > 0 && lc > 0)
        {
            double floor = Powerplant.DoubleBottom(D);
            double top = g.RoofZ ?? g.Waterline;
            double mm = holding + (layers - 1) * layerMm;
            result.Add(new Weight("Torpedo protection", "armour", 2 * lc * Math.Max(0.0, top - floor) * mm / 1000 * Weight.Steel, xc, Zf(top, floor)));
        }
        double cb = design.BlockCoefficient;
        foreach (var d in g.Decks)
        {
            string ext = d.Extent;
            double wide = topside is null ? B : B * topside.MeanRatio(L, d.Z - D);
            double area = (ext == "full" ? L * Geometry.Cwp(cb) : d.X1 - d.X0) * (d.W is double w && w != 0 ? w : wide) * 0.9;
            string name = $"Deck armour ({Decks.DeckNameLower(d.Deck)}" +
                          (ext is "fore" or "aft" or "steering" ? $", {ext})" : ")");
            double xw = ext == "full" ? 0.0 : (d.X0 + d.X1) / 2;
            // the slopes take the outer Reach each side of the breadth, longer by their angle (protection-plan.md H6)
            double sloped = d.Reach > 0 && wide > 0 ? Math.Min(1.0, 2 * d.Reach / wide) : 0.0;
            result.Add(new Weight(name, "armour", area * (1 - sloped) * d.Mm / 1000 * Weight.Steel, xw, ZRel.Deck(d.Z - D)));
            if (sloped > 0)
                result.Add(new Weight(name[..^1] + ", slopes)", "armour",
                    area * sloped / Math.Cos(double.DegreesToRadians(d.SlopeDeg)) * d.SlopeMm / 1000 * Weight.Steel, xw, Zf(d.Z, d.FootZ)));
        }
        return result;
    }

    /// <summary>Warnings on the solved ship's armour.</summary>
    public static List<string> ArmourChecks(Design design, Navarch.Result res, Geo geo)
    {
        var result = new List<string>();
        foreach (var d in ArmourDecks(design, res.Depth, geo.Raised))
            if (d.Asked != d.Deck)
                result.Add($"The hull has no {Decks.DeckNameLower(d.Asked)} ({res.Depth:F1} m deep): its " +
                         $"{d.Mm} mm deck armour lies on the {Decks.DeckNameLower(d.Deck)}.");
        foreach (var d in ArmourDecks(design, res.Depth, geo.Raised))
            if (d.SlopeMm > 0 && d.Deck >= 0 && !res.Armour.Decks.Any(p => p.Deck == d.Deck && p.Reach > 0))
                result.Add($"The {Decks.DeckNameLower(d.Deck)}'s {d.SlopeMm} mm slopes have no height: the deck lies at the belt's lower " +
                           "edge, so it runs flat to the side.");
        var arm = design.Armour ?? new ArmourInput();
        if ((arm.BeltMm ?? 0) > 0 && (arm.BeltDepthM ?? 1.0) < 1.0)
            result.Add($"The belt reaches only {arm.BeltDepthM:F1} m below the waterline: rolling or " +
                     "flooding uncovers the side under it.");
        var ub = arm.UpperBelt ?? new UpperBeltInput();
        if ((ub.Mm ?? 0) > 0 && !res.Armour.Strakes.Any(s => s.Kind == "upper"))
            result.Add($"The {ub.Mm} mm upper belt has no height: the belt below it already reaches the " +
                     $"{Decks.DeckNameLower(ub.ToDeck ?? 0)}.");
        return result;
    }
}
