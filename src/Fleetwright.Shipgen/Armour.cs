namespace Fleetwright.Shipgen;

/// <summary>A design's armour deck placed on the deck stack: the deck it lies on (asked: the one the design named),
/// its thickness, extent and height above the keel.</summary>
public sealed record PlannedArmourDeck(long Deck, double Mm, string Extent, double Z, long Asked, string? Material);

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
}

/// <summary>A strake of side armour: Kind end, upper or box (the steering gear's); Mm at its root tapering to TipMm at
/// the hull's end. The main belt, as the subdivision sees it, also has BottomMm below the waterline Wl.</summary>
public sealed record Strake(string Id, string Kind, string Extent, double Mm, double TipMm, double X0, double X1, double Bottom,
    double Top, string? Material)
{
    public double? BottomMm { get; init; }
    public double? Wl { get; init; }
}

/// <summary>An armoured transverse bulkhead at x; W narrower than the beam (the steering box).</summary>
public sealed record ArmourBulkhead(string Id, double X, double Mm, double Bottom, double Top, double? W, string? Material);

/// <summary>Where the armour is: the citadel (X0..X1), the belt, the armour decks and strakes, the bulkheads. Heights
/// above the keel. MainZ: the main armour deck's height; RoofZ: the lowest armour deck over the citadel.</summary>
public sealed class ArmourLayout
{
    public double X0, X1, BeltMm, BeltBottomMm, Waterline, BeltBottom, BeltTop;
    public List<ArmourDeck> Decks = [];
    public List<Strake> Strakes = [];
    public double? MainZ, RoofZ;
    public double RoofMm;
    public string? RoofMaterial, BeltMaterial, BulkheadMaterial;
    public bool Armoured;
    public double BulkheadMm, BulkheadBottom, BulkheadTop;
    public List<ArmourBulkhead> EndBulkheads = [];
}

/// <summary>The design's armour: its validation, where the armour is (the one source for its weights, the subdivision
/// and the hitboxes), its weights, the materials, and the warnings. Heights above the keel.</summary>
public static class Armour
{
    public const double TdsMmPerM = 12.0;
    const double BeltHA = 0.30, BeltHB = 2.4;
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
                asked, ArmourMaterial(design, "decks", d.Material)));
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
                    p.Mm += d.Mm;
                    (p.X0, p.X1) = (Math.Min(p.X0, p0), Math.Max(p.X1, p1));
                    p.Extent = p.Extent == "full" || ext == "full" ? "full" : p.Extent;
                    p.Material = dm is null || dm == pm ? pm : pm is null ? dm : $"{pm} + {dm}";
                    continue;
                }
                decks.Add(new ArmourDeck { Deck = dn, Mm = d.Mm, Extent = ext, Z = d.Z, Asked = d.Asked, Material = d.Material, X0 = p0, X1 = p1 });
            }
        }
        var over = decks.Where(d => d.Extent is "citadel" or "full" && d.Deck >= 0).ToList();
        var main = over.MaxBy(d => (d.Mm, d.Z));
        var roof = over.MinBy(d => d.Z);
        double h0 = BeltHA * T + BeltHB;
        double below = a.BeltDepthM ?? h0 / 2, above = a.BeltHeightM ?? h0 / 2;
        double h = below + above;
        double bot = Math.Max(0.0, T - below);
        double band = Math.Min(D, Math.Max(bot, T + above));
        double top = Math.Min(D, Math.Max(band, main?.Z ?? 0.0));

        var strakes = new List<Strake>();
        var endBhs = new List<ArmourBulkhead>();
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
        // the bulkheads reach the belt's top or the citadel's upper belt (LINQ's Max: leading NaNs skipped)
        double bhTop = top;
        foreach (var s in strakes)
            if (s.Kind == "upper" && s.Extent == "citadel" && (double.IsNaN(bhTop) || s.Top > bhTop))
                bhTop = s.Top;
        return new ArmourLayout
        {
            X0 = x0, X1 = x1, BeltMm = belt, BeltBottomMm = a.BeltBottomMm ?? belt, Waterline = T, BeltBottom = bot, BeltTop = top,
            Decks = decks, Strakes = strakes, MainZ = main?.Z, RoofZ = roof?.Z, RoofMm = roof?.Mm ?? 0, RoofMaterial = roof?.Material,
            BeltMaterial = ArmourMaterial(design, "belt"), BulkheadMaterial = ArmourMaterial(design, "bulkheads"),
            Armoured = belt > 0 || over.Count > 0, BulkheadMm = a.BulkheadMm ?? 0.6 * belt, BulkheadBottom = bhBot, BulkheadTop = bhTop,
            EndBulkheads = endBhs,
        };
    }

    /// <summary>The armour's weights from its geometry.</summary>
    public static List<Weight> ArmourWeights(Design design, double L, double B, double D, ArmourLayout g)
    {
        double lc = g.X1 - g.X0;
        double xc = (g.X0 + g.X1) / 2;
        ZRel Zf(double lo, double hi) => ZRel.Frac(D != 0 ? (lo + hi) / 2 / D : 0.5);
        var result = new List<Weight>();
        if (g.BeltMm > 0)
        {
            double bot = g.BeltBottom, top = g.BeltTop, mm = g.BeltMm, mb = g.BeltBottomMm;
            double t0 = Math.Min(top, Math.Max(bot, g.Waterline));
            double aUp = (top - t0) * mm, aLo = (t0 - bot) * (mm + mb) / 2;
            double zLo = mb + mm > 0 ? bot + (t0 - bot) * (mb + 2 * mm) / (3 * (mb + mm)) : bot;
            double zc = aUp + aLo > 0 ? ((top + t0) / 2 * aUp + zLo * aLo) / (aUp + aLo) : (top + bot) / 2;
            result.Add(new Weight("Belt armour", "armour", 2 * lc * (aUp + aLo) / 1000 * Weight.Steel, xc, ZRel.Frac(D != 0 ? zc / D : 0.5)));
        }
        if (g.Armoured && g.BulkheadMm > 0)
        {
            double hb = g.BulkheadTop - g.BulkheadBottom;
            result.Add(new Weight("Bulkheads", "armour", 2 * B * hb * g.BulkheadMm / 1000 * Weight.Steel, xc, Zf(g.BulkheadTop, g.BulkheadBottom)));
        }
        foreach (var b in g.EndBulkheads)
            result.Add(new Weight(b.Id, "armour", (b.W is double w && w != 0 ? w : B) * (b.Top - b.Bottom) * b.Mm / 1000 * Weight.Steel, b.X,
                Zf(b.Top, b.Bottom)));
        foreach (var s in g.Strakes)
        {
            var (a, b) = s.Extent != "aft" ? (s.Mm, s.TipMm) : (s.TipMm, s.Mm);
            double f = a + b > 0 ? (a + 2 * b) / (3 * (a + b)) : 0.5;
            result.Add(new Weight(s.Id, "armour", 2 * (s.X1 - s.X0) * (s.Top - s.Bottom) * (a + b) / 2 / 1000 * Weight.Steel,
                s.X0 + f * (s.X1 - s.X0), Zf(s.Top, s.Bottom)));
        }
        double tds = design.Armour?.TdsM ?? 0.0;
        if (tds > 0 && lc > 0)
        {
            double floor = Powerplant.DoubleBottom(D);
            double top = g.RoofZ ?? g.Waterline;
            double mm = TdsMmPerM * tds;
            result.Add(new Weight("Torpedo protection", "armour", 2 * lc * Math.Max(0.0, top - floor) * mm / 1000 * Weight.Steel, xc, Zf(top, floor)));
        }
        double cb = design.BlockCoefficient;
        foreach (var d in g.Decks)
        {
            string ext = d.Extent;
            double area = (ext == "full" ? L * Geometry.Cwp(cb) : d.X1 - d.X0) * (d.W is double w && w != 0 ? w : B) * 0.9;
            string name = $"Deck armour ({Decks.DeckNameLower(d.Deck)}" +
                          (ext is "fore" or "aft" or "steering" ? $", {ext})" : ")");
            result.Add(new Weight(name, "armour", area * d.Mm / 1000 * Weight.Steel, ext == "full" ? 0.0 : (d.X0 + d.X1) / 2, ZRel.Deck(d.Z - D)));
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
