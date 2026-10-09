namespace Fleetwright.Shipgen;

/// <summary>armour: the design's armour inputs and their validation, where the armour is (the one source for its
/// weights, the subdivision and the hitboxes), its weights, the materials, and the warnings. Heights above the keel.</summary>
public static class Armour
{
    public const double TDS_MM_PER_M = 12.0;
    const double BELT_H_A = 0.30, BELT_H_B = 2.4;
    static readonly string[] ARMOUR_EXTENTS = ["citadel", "full", "fore", "aft", "ends"];
    static readonly string[] BELT_ENDS = ["fore", "aft"];
    static readonly string[] ARMOUR_PARTS = ["belt", "upper_belt", "end_belts", "bulkheads", "decks", "turrets", "barbettes",
        "conning_tower", "secondary", "flight_deck"];

    /// <summary>Extents that may share one deck (different stretches of it).</summary>
    static bool SharedDeck(object? a, object? b)
    {
        (string, string)[] pairs = [("citadel", "fore"), ("citadel", "aft"), ("citadel", "ends"), ("fore", "aft")];
        return pairs.Any(p => Py.Eq(a, p.Item1) && Py.Eq(b, p.Item2) || Py.Eq(a, p.Item2) && Py.Eq(b, p.Item1));
    }

    public static List<string> ArmourErrors(PyDict design)
    {
        var a = design.DOr("armour");
        var errs = new List<string>();
        if (a.Has("deck_mm"))
            errs.Add("armour.deck_mm is gone: list the armour decks top down in armour.decks, e.g. " +
                     "[{\"deck\": 1, \"mm\": 152, \"extent\": \"citadel\"}]");
        if (a.Has("turret_mm"))
            errs.Add("armour.turret_mm is gone: give each main battery its turrets' armour_mm (main[k].armour_mm)");
        var decksV = a.Get("decks", new List<object?>());
        if (decksV is not List<object?> decks)
            return [.. errs, "armour.decks: use a list of armour decks, top down"];
        object? last = null, prev = null;
        for (int k = 0; k < decks.Count; k++)
        {
            var d = decks[k] as PyDict;
            if (d is null || !Py.IsInt(d.Get("deck")))
            {
                errs.Add($"armour.decks[{k}].deck: use a deck number (0 the main deck, 1 the second deck, ..., -1 " +
                         "the first raised deck)");
                continue;
            }
            if (!Py.IsNumber(d.Get("mm")) || Py.ToDouble(d["mm"]) < 0)
                errs.Add($"armour.decks[{k}].mm: use a thickness of 0 or more");
            if (!ARMOUR_EXTENTS.Any(e => Py.Eq(d.Get("extent"), e)))
                errs.Add($"armour.decks[{k}].extent = {Py.Repr(d.Get("extent"))}: use {string.Join(" or ", ARMOUR_EXTENTS)}");
            long deck = Py.ToLong(d["deck"]);
            if (last is not null && deck < Py.ToLong(last))
                errs.Add($"armour.decks[{k}]: list the armour decks top down");
            else if (last is not null && deck == Py.ToLong(last) && !SharedDeck(d.Get("extent"), prev))
                errs.Add($"armour.decks[{k}]: a deck may appear twice only over different stretches (the citadel " +
                         "and its ends)");
            last = last is null ? d["deck"] : (object)Math.Max(Py.ToLong(last), deck);
            prev = d.Get("extent");
        }
        var ub = a.Get("upper_belt");
        if (ub is not null)
        {
            if (ub is not PyDict ubd)
                errs.Add("armour.upper_belt: use {\"mm\", \"to_deck\", \"extent\"}");
            else
            {
                if (!Py.IsInt(ubd.Get("to_deck", 0L)))
                    errs.Add("armour.upper_belt.to_deck: use a deck number (0 the main deck, 1 the second deck, ..., " +
                             "-1 the first raised deck)");
                if (!ARMOUR_EXTENTS.Any(e => Py.Eq(ubd.Get("extent", "citadel"), e)))
                    errs.Add($"armour.upper_belt.extent = {Py.Repr(ubd.Get("extent"))}: use {string.Join(" or ", ARMOUR_EXTENTS)}");
            }
        }
        var mats = a.Get("materials");
        if (mats is not null)
        {
            if (mats is not PyDict md)
                errs.Add("armour.materials: name a material per part, e.g. {\"belt\": \"Krupp cemented\", ...}");
            else
            {
                errs.AddRange(md.Keys.Where(k => !ARMOUR_PARTS.Contains(k))
                    .Select(k => $"armour.materials.{k}: not an armour part ({string.Join(", ", ARMOUR_PARTS)})"));
                errs.AddRange(md.Where(kv => !(kv.Value is string s && s.Length > 0))
                    .Select(kv => $"armour.materials.{kv.Key}: name the material as a string"));
            }
        }
        var owns = new List<(string Where, PyDict D)>();
        for (int k = 0; k < decks.Count; k++)
            if (decks[k] is PyDict dk)
                owns.Add(($"armour.decks[{k}]", dk));
        if (a.Get("upper_belt") is PyDict ub2)
            owns.Add(("armour.upper_belt", ub2));
        if (a.Get("end_belts") is PyDict eb2)
            foreach (var kv in eb2)
                if (kv.Value is PyDict ev)
                    owns.Add(($"armour.end_belts.{kv.Key}", ev));
        if (a.Get("steering_box") is PyDict sb2)
        {
            owns.Add(("armour.steering_box", sb2));
            if (sb2.Has("deck_material"))
                owns.Add(("armour.steering_box.deck", PyDict.Of(("material", sb2["deck_material"]))));
        }
        foreach (var g in new[] { "secondary", "main" })
        {
            var bl = Batteries.AsList(design.Get(g));
            for (int k = 0; k < bl.Count; k++)
                if (bl[k] is PyDict b)
                    owns.Add(($"{g}[{k}]", b));
        }
        errs.AddRange(owns.Where(o => o.D.Has("material") && !(o.D["material"] is string s && s.Length > 0))
            .Select(o => $"{o.Where}.material: name the material as a string"));
        var eb = a.Get("end_belts");
        if (eb is not null)
        {
            if (eb is not PyDict ebd || ebd.Keys.Any(k => !(k is "fore" or "aft")))
                errs.Add("armour.end_belts: use {\"fore\": {\"mm\", \"tip_mm\", \"reach\", \"bulkhead_mm\"}, " +
                         "\"aft\": {...}}");
            else
                errs.AddRange(ebd.Where(kv => kv.Value is not PyDict)
                    .Select(kv => $"armour.end_belts.{kv.Key}: use {{\"mm\", \"tip_mm\", \"reach\", \"bulkhead_mm\"}}"));
        }
        var sb = a.Get("steering_box");
        if (sb is not null && (sb is not PyDict sbd ||
                               sbd.Keys.Any(k => !(k is "mm" or "deck_mm" or "bulkhead_mm" or "material" or "deck_material"))))
            errs.Add("armour.steering_box: use {\"mm\", \"deck_mm\", \"bulkhead_mm\"} (0 for none)");
        return errs;
    }

    /// <summary>The armour material named for a part (a deck or battery may give its own): a string, or null.</summary>
    public static object? ArmourMaterial(PyDict design, string part, PyDict? own = null)
    {
        if (own != null && own.B("material"))
            return own["material"];
        return design.DOr("armour").DOr("materials").Get(part);
    }

    /// <summary>The design's armour decks (top down), placed on the deck stack: [dict(deck, mm, extent, z, asked,
    /// material)].</summary>
    public static List<PyDict> ArmourDecks(PyDict design, double D, IReadOnlyList<PyDict> raised)
    {
        var stack = Decks.DeckStack(design, D);
        long top = -raised.Select(s => s.I("levels")).DefaultIfEmpty(0L).Max();
        var out_ = new List<PyDict>();
        var decks = design.DOr("armour").Or("decks", null) as List<object?> ?? [];
        foreach (PyDict d in decks.Cast<PyDict>())
        {
            long asked = (long)(Py.ToDouble(d.Get("deck", 0L)));
            long n = Math.Max(top, Math.Min(asked, stack[^1].N));
            out_.Add(PyDict.Of(("deck", n), ("mm", d.Get("mm", 0L)), ("extent", d.Get("extent", "citadel")),
                ("z", n >= 0 ? stack[(int)n].Z : D - n * Geometry.DECK_PITCH), ("asked", asked),
                ("material", ArmourMaterial(design, "decks", d))));
        }
        return out_;
    }

    /// <summary>An armour extent as [(extent, x0, x1)] pieces.</summary>
    public static List<(string Ext, double X0, double X1)> ExtentSpans(string extent, double L, double x0, double x1) => extent switch
    {
        "citadel" => [("citadel", x0, x1)],
        "full" => [("full", -L / 2, L / 2)],
        "fore" => [("fore", x1, L / 2)],
        "aft" => [("aft", -L / 2, x0)],
        "ends" => [("fore", x1, L / 2), ("aft", -L / 2, x0)],
        _ => throw new PyKeyError(extent),
    };

    /// <summary>A belt's thickness at x (and height z): mm at its root tapering to tip_mm at the hull's end; a belt with
    /// bottom_mm keeps its thickness down to the waterline, then tapers to bottom_mm at its lower edge.</summary>
    public static double BeltMmAt(PyDict s, double x, double? z = null)
    {
        double mm = s.F("mm");
        if (s.F("tip_mm") != mm && s.F("x1") - s.F("x0") > 0)
        {
            double f = (x - s.F("x0")) / (s.F("x1") - s.F("x0"));
            if (Py.Eq(s["extent"], "aft"))
                f = 1.0 - f;
            mm += (s.F("tip_mm") - mm) * Math.Min(1.0, Math.Max(0.0, f));
        }
        if (z is double zz && s.F("bottom_mm", mm) != mm && zz < s.F("wl") && s.F("wl") > s.F("bottom"))
        {
            double f = Math.Max(0.0, (zz - s.F("bottom")) / (s.F("wl") - s.F("bottom")));
            mm = s.F("bottom_mm") + (mm - s.F("bottom_mm")) * f;
        }
        return mm;
    }

    static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    /// <summary>Where the armour is: the one source for its weights and its hitboxes. Heights above the keel.</summary>
    public static PyDict ArmourGeometry(PyDict design, double L, double T, double D, Geo geo)
    {
        var a = design.DOr("armour");
        object belt = a.Get("belt_mm", 0L)!;
        double beltD = Py.ToDouble(belt);
        var (x0, x1) = geo.CitadelSpan(L);
        var raised = geo.Raised;
        var decks = new List<PyDict>();
        foreach (var d in ArmourDecks(design, D, raised))
        {
            if (d.F("mm") <= 0)
                continue;
            var spans = ExtentSpans(d.S("extent"), L, x0, x1);
            long dn = d.I("deck");
            if (dn < 0)
                spans = spans.SelectMany(s => Decks.RaisedPieces(raised, s.X0, s.X1, -dn)
                    .Where(p => p.Lv >= -dn).Select(p => (s.Ext, p.X0, p.X1))).ToList();
            foreach (var (ext, p0, p1) in spans)
            {
                var p = decks.FirstOrDefault(q => q.I("deck") == dn && Math.Min(q.F("x1"), p1) - Math.Max(q.F("x0"), p0) > 1e-6);
                if (p != null)
                {
                    object? pm = p["material"], dm = d["material"];
                    p.Update(("mm", Py.Add(p["mm"], d["mm"])), ("x0", Math.Min(p.F("x0"), p0)), ("x1", Math.Max(p.F("x1"), p1)),
                        ("extent", Py.Eq(p["extent"], "full") || ext == "full" ? "full" : p["extent"]),
                        ("material", dm is null || Py.Eq(dm, pm) ? pm : pm is null ? dm : $"{Py.Str(pm)} + {Py.Str(dm)}"));
                    continue;
                }
                var nd = d.Copy();
                nd.Update(("extent", ext), ("x0", p0), ("x1", p1));
                decks.Add(nd);
            }
        }
        var over = decks.Where(d => Py.In(d["extent"], "citadel", "full") && d.I("deck") >= 0).ToList();
        var main = over.MaxBy(d => (d.F("mm"), d.F("z")));
        var roof = over.MinBy(d => d.F("z"));
        double h0 = BELT_H_A * T + BELT_H_B;
        double below = a.F("belt_depth_m", h0 / 2), above = a.F("belt_height_m", h0 / 2);
        double h = below + above;
        double bot = Math.Max(0.0, T - below);
        double band = Math.Min(D, Math.Max(bot, T + above));
        double top = Math.Min(D, Math.Max(band, main != null ? main.F("z") : 0.0));

        var strakes = new List<PyDict>();
        var endBhs = new List<PyDict>();
        double bhBot = Math.Max(0.0, bot - 0.4 * h);
        var tops = new Dictionary<string, double>(StringComparer.Ordinal) { ["citadel"] = beltD > 0 ? top : band };
        foreach (var end in BELT_ENDS)
        {
            var e = a.DOr("end_belts").DOr(end);
            tops[end] = band;
            if (e.F("mm", 0) <= 0)
                continue;
            var (_, s0, s1) = ExtentSpans(end, L, x0, x1)[0];
            double reach = Math.Min(1.0, Math.Max(0.0, e.F("reach", 1.0)));
            if (end == "fore")
                s1 = s0 + (s1 - s0) * reach;
            else
                s0 = s1 - (s1 - s0) * reach;
            double mid = (s0 + s1) / 2;
            var cover = decks.Where(d => d.F("x0") <= mid && mid <= d.F("x1") && Py.In(d["extent"], end, "full")).ToList();
            var dk = cover.MaxBy(d => (d.F("mm"), d.F("z")));
            double et = Math.Min(D, Math.Max(band, dk != null ? dk.F("z") : 0.0));
            tops[end] = et;
            if (s1 - s0 > 1e-6)
            {
                strakes.Add(PyDict.Of(("id", $"{Capitalize(end)} end belt"), ("kind", "end"), ("extent", end), ("mm", e["mm"]),
                    ("tip_mm", e.Get("tip_mm", e["mm"])), ("x0", s0), ("x1", s1), ("bottom", bot), ("top", et),
                    ("material", ArmourMaterial(design, "end_belts", e))));
                if (reach < 1.0 && e.F("bulkhead_mm", 0) > 0)
                    endBhs.Add(PyDict.Of(("id", $"{Capitalize(end)} end belt bulkhead"), ("x", end == "fore" ? s1 : s0),
                        ("mm", e["bulkhead_mm"]), ("bottom", bhBot), ("top", et), ("material", ArmourMaterial(design, "bulkheads"))));
            }
        }
        var sb = a.DOr("steering_box");
        if (Math.Max(sb.F("mm", 0), Math.Max(sb.F("deck_mm", 0), sb.F("bulkhead_mm", 0))) > 0)
        {
            var (b0, b1) = geo.SteeringSpan(L);
            var stack = Decks.DeckStack(design, D);
            object? wbox = geo.SteeringBeam;
            double rz = Ordnance.Span(PyDict.Of(("decks", stack.Select(s => (object?)s.Z).ToList()),
                ("inner_bottom", Powerplant.DoubleBottom(D)), ("top", roof != null ? roof.F("z") : D))).Top + D;
            long n = stack.MinBy(v => Math.Abs(v.Z - rz)).N;
            if (sb.F("deck_mm", 0) > 0)
            {
                object? own = sb.Get("deck_material");
                decks.Add(PyDict.Of(("deck", n), ("mm", sb["deck_mm"]), ("extent", "steering"), ("z", rz), ("asked", n),
                    ("x0", b0), ("x1", b1), ("w", wbox),
                    ("material", ArmourMaterial(design, "decks", Py.Truthy(own) ? PyDict.Of(("material", own)) : null))));
            }
            double floor = Math.Min(Powerplant.DoubleBottom(D), rz);
            if (sb.F("mm", 0) > 0)
                strakes.Add(PyDict.Of(("id", "Steering gear box"), ("kind", "box"), ("extent", "aft"), ("mm", sb["mm"]),
                    ("tip_mm", sb["mm"]), ("x0", b0), ("x1", b1), ("bottom", floor), ("top", rz),
                    ("material", ArmourMaterial(design, "end_belts", sb))));
            if (sb.F("bulkhead_mm", 0) > 0)
                foreach (var (w, x) in new[] { ("forward", b1), ("aft", b0) })
                    endBhs.Add(PyDict.Of(("id", $"Steering gear box {w} bulkhead"), ("x", x), ("mm", sb["bulkhead_mm"]),
                        ("bottom", floor), ("top", rz), ("w", wbox), ("material", ArmourMaterial(design, "bulkheads"))));
        }
        var ub = a.DOr("upper_belt");
        if (ub.F("mm", 0) > 0)
        {
            var stack = Decks.DeckStack(design, D);
            long to = (long)(Py.ToDouble(ub.Get("to_deck", 0L)));
            double ut = stack[(int)Math.Min(Math.Max(to, 0), stack[^1].N)].Z;
            var pieces = ExtentSpans(ub.S("extent", "citadel")!, L, x0, x1);
            if (Py.Eq(ub.Get("extent"), "full"))
                pieces = [("citadel", x0, x1), ("fore", x1, L / 2), ("aft", -L / 2, x0)];
            foreach (var (ext, s0, s1) in pieces)
            {
                var parts = to < 0 ? Decks.RaisedPieces(raised, s0, s1, -to) : [(s0, s1, 0L)];
                for (int k = 0; k < parts.Count; k++)
                {
                    var (p0, p1, lv) = parts[k];
                    double top_ = ut + lv * Geometry.DECK_PITCH;
                    if (top_ > tops[ext] + 0.05 && p1 - p0 > 1e-6)
                    {
                        string sid = ext == "citadel" ? "Upper belt" : $"Upper belt ({ext})";
                        strakes.Add(PyDict.Of(("id", sid + (parts.Count > 1 ? $" {k + 1}" : "")), ("kind", "upper"),
                            ("extent", ext), ("mm", ub["mm"]), ("tip_mm", ub["mm"]), ("x0", p0), ("x1", p1),
                            ("bottom", tops[ext]), ("top", top_), ("material", ArmourMaterial(design, "upper_belt", ub))));
                    }
                }
            }
        }
        double bhTop = (new[] { top }.Concat(strakes.Where(s => Py.Eq(s["kind"], "upper") && Py.Eq(s["extent"], "citadel"))
            .Select(s => s.F("top")))).Max();
        return PyDict.Of(("x0", x0), ("x1", x1), ("belt_mm", belt), ("belt_bottom_mm", a.Get("belt_bottom_mm", belt)),
            ("waterline", T), ("belt_bottom", bot), ("belt_top", top), ("decks", decks.Cast<object?>().ToList()),
            ("strakes", strakes.Cast<object?>().ToList()), ("main_z", main?["z"]), ("roof_z", roof?["z"]),
            ("roof_mm", roof != null ? roof["mm"] : 0L), ("roof_material", roof?["material"]),
            ("belt_material", ArmourMaterial(design, "belt")), ("bulkhead_material", ArmourMaterial(design, "bulkheads")),
            ("armoured", beltD > 0 || over.Count > 0), ("bulkhead_mm", a.Has("bulkhead_mm") ? a["bulkhead_mm"] : 0.6 * beltD),
            ("bulkhead_bottom", bhBot), ("bulkhead_top", bhTop), ("end_bulkheads", endBhs.Cast<object?>().ToList()));
    }

    /// <summary>The armour's weights from its geometry.</summary>
    public static List<Weight> ArmourWeights(PyDict design, double L, double B, double D, PyDict g)
    {
        double lc = g.F("x1") - g.F("x0");
        double xc = (g.F("x0") + g.F("x1")) / 2;
        ZRel Zf(double lo, double hi) => ZRel.Frac(D != 0 ? (lo + hi) / 2 / D : 0.5);
        var out_ = new List<Weight>();
        if (g.F("belt_mm") > 0)
        {
            double bot = g.F("belt_bottom"), top = g.F("belt_top"), mm = g.F("belt_mm"), mb = g.F("belt_bottom_mm");
            double t0 = Math.Min(top, Math.Max(bot, g.F("waterline")));
            double aUp = (top - t0) * mm, aLo = (t0 - bot) * (mm + mb) / 2;
            double zLo = mb + mm > 0 ? bot + (t0 - bot) * (mb + 2 * mm) / (3 * (mb + mm)) : bot;
            double zc = aUp + aLo > 0 ? ((top + t0) / 2 * aUp + zLo * aLo) / (aUp + aLo) : (top + bot) / 2;
            out_.Add(new Weight("Belt armour", "armour", 2 * lc * (aUp + aLo) / 1000 * Weight.STEEL, xc,
                ZRel.Frac(D != 0 ? zc / D : 0.5)));
        }
        if (Py.Truthy(g["armoured"]) && g.F("bulkhead_mm") > 0)
        {
            double hb = g.F("bulkhead_top") - g.F("bulkhead_bottom");
            out_.Add(new Weight("Bulkheads", "armour", 2 * B * hb * g.F("bulkhead_mm") / 1000 * Weight.STEEL, xc,
                Zf(g.F("bulkhead_top"), g.F("bulkhead_bottom"))));
        }
        foreach (PyDict b in g.L("end_bulkheads").Cast<PyDict>())
            out_.Add(new Weight(b.S("id"), "armour", (Py.Truthy(b.Get("w")) ? b.F("w") : B) * (b.F("top") - b.F("bottom")) * b.F("mm")
                / 1000 * Weight.STEEL, b.F("x"), Zf(b.F("top"), b.F("bottom"))));
        foreach (PyDict s in g.L("strakes").Cast<PyDict>())
        {
            var (a, b) = !Py.Eq(s["extent"], "aft") ? (s.F("mm"), s.F("tip_mm")) : (s.F("tip_mm"), s.F("mm"));
            double f = a + b > 0 ? (a + 2 * b) / (3 * (a + b)) : 0.5;
            out_.Add(new Weight(s.S("id"), "armour", 2 * (s.F("x1") - s.F("x0")) * (s.F("top") - s.F("bottom")) * (a + b) / 2
                / 1000 * Weight.STEEL, s.F("x0") + f * (s.F("x1") - s.F("x0")), Zf(s.F("top"), s.F("bottom"))));
        }
        double tds = design.DOr("armour").F("tds_m", 0.0);
        if (tds > 0 && lc > 0)
        {
            double floor = Powerplant.DoubleBottom(D);
            double top = g["roof_z"] is not null ? g.F("roof_z") : g.F("waterline");
            double mm = TDS_MM_PER_M * tds;
            out_.Add(new Weight("Torpedo protection", "armour", 2 * lc * Math.Max(0.0, top - floor) * mm / 1000 * Weight.STEEL, xc,
                Zf(top, floor)));
        }
        double cb = design.D("hull").F("block_coefficient");
        foreach (PyDict d in g.L("decks").Cast<PyDict>())
        {
            string ext = d.S("extent");
            double area = (ext == "full" ? L * Geometry.Cwp(cb) : d.F("x1") - d.F("x0")) * (Py.Truthy(d.Get("w")) ? d.F("w") : B) * 0.9;
            string name = $"Deck armour ({Decks.DeckName(d.I("deck")).ToLowerInvariant()}" +
                          (ext is "fore" or "aft" or "steering" ? $", {ext})" : ")");
            out_.Add(new Weight(name, "armour", area * d.F("mm") / 1000 * Weight.STEEL,
                ext == "full" ? 0.0 : (d.F("x0") + d.F("x1")) / 2, ZRel.Deck(d.F("z") - D)));
        }
        return out_;
    }

    /// <summary>Warnings on the solved ship's armour.</summary>
    public static List<string> ArmourChecks(PyDict design, Navarch.Result res, Geo geo)
    {
        var out_ = new List<string>();
        foreach (var d in ArmourDecks(design, res.Depth, geo.Raised))
            if (d.I("asked") != d.I("deck"))
                out_.Add($"The hull has no {Decks.DeckName(d.I("asked")).ToLowerInvariant()} ({res.Depth:F1} m deep): its " +
                         $"{Py.Str(d["mm"])} mm deck armour lies on the {Decks.DeckName(d.I("deck")).ToLowerInvariant()}.");
        var arm = design.DOr("armour");
        if (arm.F("belt_mm", 0) > 0 && arm.F("belt_depth_m", 1.0) < 1.0)
            out_.Add($"The belt reaches only {arm.F("belt_depth_m"):F1} m below the waterline: rolling or " +
                     "flooding uncovers the side under it.");
        var ub = arm.DOr("upper_belt");
        if (ub.F("mm", 0) > 0 && !res.Armour.L("strakes").Cast<PyDict>().Any(s => Py.Eq(s["kind"], "upper")))
            out_.Add($"The {Py.Str(ub["mm"])} mm upper belt has no height: the belt below it already reaches the " +
                     $"{Decks.DeckName(Py.ToLong(ub.Get("to_deck", 0L))).ToLowerInvariant()}.");
        return out_;
    }
}
