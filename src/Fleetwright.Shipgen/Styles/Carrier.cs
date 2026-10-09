namespace Fleetwright.Shipgen;

/// <summary>carrier: aircraft carriers, from seaplane carriers to angled-deck fleet carriers.</summary>
public sealed class CarrierStyle : Style
{
    const double HANGAR_H = 5.6, GALLERY_H = 2.0, SPOT_K = 20.0, ANGLE_DEG = 9.0;
    const double ORDNANCE_K = 0.6, AVGAS_K = 1.2, AVGAS_T_PER_M3 = 0.5;

    static PyDict? Defaults(object? kind) => kind switch
    {
        "axial" => PyDict.Of(("elevators", 2L), ("deck_edge_elevators", 1L), ("catapults", 1L), ("cranes", 0L)),
        "angled" => PyDict.Of(("elevators", 0L), ("deck_edge_elevators", 4L), ("catapults", 4L), ("cranes", 0L)),
        "none" => PyDict.Of(("elevators", 0L), ("deck_edge_elevators", 0L), ("catapults", 1L), ("cranes", 2L)),
        _ => null,
    };

    public static PyDict Aviation(PyDict design)
    {
        var a = design.DOr("aviation");
        var kind = a.Get("flight_deck", "axial");
        return PyDict.Merge(PyDict.Of(("flight_deck", kind), ("aircraft", 0L), ("aircraft_t", 5.0), ("hangar_decks", 1L), ("hangar", "open")),
            Defaults(kind), a);
    }

    /// <summary>A closed hangar: the flight deck is the hull's strength deck.</summary>
    static bool Closed(PyDict design)
    {
        var av = Aviation(design);
        return Py.Eq(av["hangar"], "closed") && !Py.Eq(av["flight_deck"], "none");
    }

    static double SpotM2(PyDict av) => SPOT_K * Py.Pow(av.F("aircraft_t"), 2.0 / 3);

    /// <summary>0, 1/2, 1/4, 3/4, 1/8, ...: positions that spread evenly however many get used.</summary>
    public static List<double> Vdc(int n)
    {
        var out_ = new List<double>();
        for (int i = 0; i < n; i++)
        {
            int k = i;
            long d = 2;
            double v = 0.0;
            while (k != 0)
            {
                v += (double)(k & 1) / d;
                k >>= 1;
                d *= 2;
            }
            out_.Add(v);
        }
        return out_;
    }

    /// <summary>Flight deck and hangar geometry, from the design alone (so navarch and the layout agree).</summary>
    public sealed record DeckPlanData(string Kind, double FdH, (double X0, double X1, double Hhw) Hangar, double HangarArea, double FdArea,
        long Capacity, double X0 = 0, double X1 = 0, double Tap = 0, double Hw = 0, List<Pt>? Points = null, Land? Land = null,
        (double A, double B)? Park = null);

    public sealed record Land(Pt P0, (double X, double Y) D, (double X, double Y) N, double Length, double Width, Pt End, Pt Corner);

    public static DeckPlanData DeckPlan(PyDict design)
    {
        var av = Aviation(design);
        double L = design.D("hull").F("length"), B = design.D("hull").F("beam");
        string kind = av.S("flight_deck");
        if (kind == "none")
        {
            double hx0n = -0.30 * L, hx1n = -0.02 * L, hhwn = 0.36 * B;
            double deckX0 = -L / 2 + 0.03 * L;
            double hangarAreaN = (hx1n - hx0n) * 2 * hhwn;
            double parkArea = (hx0n - deckX0) * 0.7 * B;
            return new DeckPlanData(kind, 0.0, (hx0n, hx1n, hhwn), hangarAreaN, 0.0,
                Py.Int((hangarAreaN * 0.85 + parkArea * 0.5) / SpotM2(av)), Park: (deckX0, hx0n));
        }
        double fdH = HANGAR_H * av.F("hangar_decks") + GALLERY_H;
        double x0 = -L / 2 - 0.02 * L, x1 = L / 2 - 0.03 * L;
        double tap = 0.06 * L, hw = 0.6 * B;
        var port = new List<Pt> { new(x0, -0.9 * hw), new(x0 + 3, -hw) };
        Land? land = null;
        if (kind == "angled")
        {
            double th = Py.Radians(ANGLE_DEG);
            double c = Math.Cos(th), s = Math.Sin(th);
            double wl = 1.4 * hw;
            var p0 = new Pt(x0, 0.1 * hw);
            double ll = 0.62 * (x1 - x0);
            var d = (c, -s);
            var n = (-s, -c);
            Pt Q(double t, double off) => new(p0.X + off * n.Item1 + t * d.Item1, p0.Y + off * n.Item2 + t * d.Item2);
            double t1 = (p0.Y - c * wl / 2 + hw) / s;
            var e = Q(ll, wl / 2);
            var f = new Pt(e.X + (Math.Abs(e.Y) - hw) * 0.8, -hw);
            port.AddRange([Q(t1, wl / 2), e, f]);
            land = new Land(p0, d, n, ll, wl, e, f);
        }
        port.AddRange([new(x1 - tap, -hw), new(x1, -0.45 * hw)]);
        var stbd = new List<Pt> { new(x1, 0.45 * hw), new(x1 - tap, hw), new(x0 + 3, hw), new(x0, 0.9 * hw) };
        var pts = port.Concat(stbd).ToList();
        double hx0 = -0.40 * L, hx1 = 0.32 * L, hhw = 0.4 * B;
        double hangarArea = (hx1 - hx0) * 2 * hhw;
        double fdArea = Geometry.PolygonArea(pts);
        long cap = Py.Int((hangarArea * 0.85 * av.F("hangar_decks") + fdArea * 0.30) / SpotM2(av));
        return new DeckPlanData(kind, fdH, (hx0, hx1, hhw), hangarArea, fdArea, cap, x0, x1, tap, hw, pts, land);
    }

    static (double Lo, double Hi) Edges(DeckPlanData dp, double x) =>
        Geometry.PolygonYSpan(dp.Points!, x) ?? (-dp.Hw, dp.Hw);

    /// <summary>Renderer spec for the flight deck.</summary>
    static PyDict FlightDeckDrawing(DeckPlanData dp, PyDict av, double L)
    {
        double x0 = dp.X0, x1 = dp.X1, tap = dp.Tap, hw = dp.Hw;
        var marks = new List<object?>();
        var wires = new List<object?>();
        var ys = dp.Points!.Select(p => p.Y).ToList();
        double lc = Layout.Clamp(12 + 2.8 * av.F("aircraft_t"), 20, 85);
        var land = dp.Land;
        (double, double) At(double t, double off = 0.0) =>
            (land!.P0.X + off * land.N.X + t * land.D.X, land.P0.Y + off * land.N.Y + t * land.D.Y);
        if (land != null)
        {
            double ll = land.Length, wl = land.Width;
            var a = At(8);
            var b = At(ll - 5);
            marks.Add(PyDict.Of(("x1", a.Item1), ("y1", a.Item2), ("x2", b.Item1), ("y2", b.Item2), ("color", "marking"), ("width", 0.5), ("dash", "5 5")));
            foreach (var off in new[] { wl / 2 - 1.0, -wl / 2 + 1.0 })
            {
                var a2 = At(2, off);
                var b2 = At(ll - 2, off);
                marks.Add(PyDict.Of(("x1", a2.Item1), ("y1", a2.Item2), ("x2", b2.Item1), ("y2", b2.Item2), ("color", "marking"), ("width", 0.35),
                    ("opacity", 0.85)));
            }
            for (int k = 0; k < 4; k++)
            {
                double t = ll * (0.10 + 0.04 * k);
                var a3 = At(t, wl / 2 - 2);
                var b3 = At(t, -wl / 2 + 2);
                wires.Add(new object?[] { a3.Item1, a3.Item2, b3.Item1, b3.Item2 });
            }
            marks.Add(PyDict.Of(("x1", land.Corner.X), ("y1", 0L), ("x2", x1 - 4), ("y2", 0L), ("color", "marking"), ("width", 0.5), ("dash", "5 5")));
            marks.Add(PyDict.Of(("x1", x0 + 1.5), ("y1", hw - 1.0), ("x2", x1 - tap), ("y2", hw - 1.0), ("color", "marking"), ("width", 0.35),
                ("opacity", 0.85)));
        }
        else
        {
            marks.Add(PyDict.Of(("x1", x0 + 4), ("y1", 0L), ("x2", x1 - 4), ("y2", 0L), ("color", "marking"), ("width", 0.5), ("dash", "5 5")));
            foreach (int side in new[] { -1, 1 })
                marks.Add(PyDict.Of(("x1", x0 + 1.5), ("y1", side * (hw - 1.0)), ("x2", x1 - tap), ("y2", side * (hw - 1.0)), ("color", "marking"),
                    ("width", 0.35), ("opacity", 0.85)));
            for (int k = 0; k < 8; k++)
            {
                double wx = x0 + (x1 - x0) * (0.06 + 0.02 * k);
                wires.Add(new object?[] { wx, -hw + 2, wx, hw - 2 });
            }
        }
        for (int i = 0; i < 6; i++)
        {
            double sx = x0 + 0.8 + i * 1.6;
            marks.Add(PyDict.Of(("x1", sx), ("y1", -0.9 * hw + 1.5), ("x2", sx), ("y2", 0.9 * hw - 1.5), ("color", "stripe"), ("width", 0.7),
                ("opacity", i % 2 == 0 ? 0.9 : (object)0L)));
        }
        for (int k = 0; k < Math.Min(av.I("catapults"), land != null ? 4 : 2); k++)
        {
            if (k < 2)
            {
                double y = (k == 0 ? -0.22 : 0.22) * hw;
                marks.Add(PyDict.Of(("x1", x1 - 0.6 * tap), ("y1", y), ("x2", x1 - 0.6 * tap - lc), ("y2", y), ("color", "track"), ("width", 0.6)));
            }
            else
            {
                double off = land!.Width / 2 - 6 - 7 * (k - 2);
                var a = At(land.Length - 10, off);
                var b = At(land.Length - 10 - lc, off);
                marks.Add(PyDict.Of(("x1", a.Item1), ("y1", a.Item2), ("x2", b.Item1), ("y2", b.Item2), ("color", "track"), ("width", 0.6)));
            }
        }
        return PyDict.Of(("points", dp.Points), ("planks", PyDict.Of(("x0", x0), ("x1", x1), ("y0", Py.Min(ys)), ("y1", Py.Max(ys)), ("step", 1.4))),
            ("elevators", new List<object?>()), ("edge_elevators", new List<object?>()), ("wires", wires), ("marks", marks),
            ("number", av.B("number") ? PyDict.Of(("x", x1 - 0.6 * tap - lc - 0.04 * L), ("y", 0L), ("text", Py.Str(av.Get("number", ""))),
                ("size", 0.035 * L)) : null));
    }

    /// <summary>Carriers and merchants are built around something else; guns are fitted where they suit.</summary>
    public static List<string> GunsAreSecondaries(Style style, PyDict design)
    {
        var errs = new List<string>();
        if (Py.Truthy(design.Get("main")))
            errs.Add($"The {style.Name} style has no main battery: its guns are secondaries fitted where they suit. " +
                     "Give them as \"secondary\": a battery or a list of batteries, each with \"count\" (or " +
                     "\"per_side\") and \"where\": \"ends\" or \"sides\".");
        errs.AddRange(Armament.BatteriesOf(design).Where(b => !Py.In(b["where"], "ends", "sides"))
            .Select(b => $"secondary.where = {Py.Repr(b["where"])}: use ends or sides"));
        return errs;
    }

    public override string Name => "carrier";
    public override bool SECONDARY_LIST => true;
    public override long MIN_TOWER => 3;

    protected override IEnumerable<Limit> StyleLimits =>
    [
        new(["hull", "block_coefficient"], 0.45, 0.76), new(["aviation", "aircraft"], 0L, 160L), new(["aviation", "aircraft_t"], 0.5, 35L),
        new(["aviation", "hangar_decks"], 1L, 2L), new(["aviation", "elevators"], 0L, 4L), new(["aviation", "deck_edge_elevators"], 0L, 4L),
        new(["aviation", "catapults"], 0L, 4L), new(["aviation", "cranes"], 0L, 4L), new(["armour", "flight_deck_mm"], 0L, 100L),
    ];

    public override SizeRules SIZE => base.SIZE with { LbMax = 9.5 };

    public override List<string> Validate(PyDict design)
    {
        var av = Aviation(design);
        var kind = av["flight_deck"];
        var hangar = av["hangar"];
        var errs = base.Validate(design);
        errs.AddRange(GunsAreSecondaries(this, design));
        if (Defaults(kind) is null)
            errs.Add($"aviation.flight_deck = {Py.Repr(kind)}: use axial, angled or none");
        if (!Py.In(hangar, "open", "closed"))
            errs.Add($"aviation.hangar = {Py.Repr(hangar)}: use open or closed");
        return errs;
    }

    public override PyDict? StrengthDeck(PyDict design, double D)
    {
        if (!Closed(design))
            return null;
        double fdH = DeckPlan(design).FdH;
        var mm = design.DOr("armour").Get("flight_deck_mm", 0L);
        return PyDict.Of(("h", fdH), ("decks", Aviation(design)["hangar_decks"]),
            ("plates", Py.Truthy(mm) ? new List<(double, double)> { (Py.ToDouble(mm), D + fdH) } : new List<(double, double)>()));
    }

    public override PyDict Tuning(PyDict design) => PyDict.Merge(base.Tuning(design), PyDict.Of(("freeboard_a", 0.024), ("freeboard_b", 2.5),
        ("misc_frac", 0.075), ("hull_z_frac", 0.5), ("flight_deck_t_per_m2", 0.34), ("hangar_t_per_m2", 0.32)));

    public override Layout BuildLayout(PyDict design, Navarch.Result res, double shift = 0.0, double spread = 0.0) =>
        Py.Eq(Aviation(design)["flight_deck"], "none") ? SeaplaneLayout(design, res, shift) : FlightDeckLayout(design, res, shift);

    public override List<Weight> RoughPayload(PyDict design, double D)
    {
        double L = design.D("hull").F("length");
        return [new Weight("Island", "superstructure", 0.006 * Py.Pow(L, 2), zRel: ZRel.Deck(DeckPlan(design).FdH + 4))];
    }

    public override (double Area, double X, double Z) WeatherDeck(PyDict design, double L, double B)
    {
        var dp = DeckPlan(design);
        if (dp.Kind == "none")
            return base.WeatherDeck(design, L, B);
        return (dp.FdArea, (dp.X0 + dp.X1) / 2, dp.FdH);
    }

    public override List<Weight> StructureWeights(PyDict design, double L, double B, double T, double D, Geo geo, PyDict tun)
    {
        var dp = DeckPlan(design);
        var av = Aviation(design);
        double m = av.F("aircraft_t");
        var (hx0, hx1, _) = dp.Hangar;
        var out_ = new List<Weight>();
        if (dp.Kind != "none")
        {
            double fdx = (dp.X0 + dp.X1) / 2;
            double fdT = dp.FdArea * tun.F("flight_deck_t_per_m2");
            if (Closed(design))
                fdT = Py.Max(0.0, fdT - HullWeight.DeckArea(L, B, design.D("hull").F("block_coefficient"))
                    * HullWeight.DeckTPerM2(L, HullWeight.Construction(design)));
            out_.Add(new Weight("Flight deck", "hull", fdT, fdx, ZRel.Deck(dp.FdH)));
            var mm = design.DOr("armour").Get("flight_deck_mm", 0L);
            if (Py.Truthy(mm))
                out_.Add(new Weight("Flight deck armour", "armour", dp.FdArea * 0.85 * Py.ToDouble(mm) / 1000 * Weight.STEEL, fdx, ZRel.Deck(dp.FdH)));
            out_.Add(new Weight("Hangar structure", "hull", dp.HangarArea * av.F("hangar_decks") * tun.F("hangar_t_per_m2"), (hx0 + hx1) / 2,
                ZRel.Deck(dp.FdH / 2)));
            double nEl = av.F("elevators") + av.F("deck_edge_elevators");
            if (nEl != 0)
                out_.Add(new Weight("Elevators", "aviation", nEl * (8 + 3 * m), (hx0 + hx1) / 2, ZRel.Deck(dp.FdH - 1)));
            out_.Add(new Weight("Arresting gear", "aviation", 30 + 2 * m, dp.X0 + 0.15 * (dp.X1 - dp.X0), ZRel.Deck(dp.FdH - 1)));
            if (av.F("catapults") != 0)
                out_.Add(new Weight("Catapults", "aviation", av.F("catapults") * (15 + 3 * m), 0.3 * L, ZRel.Deck(dp.FdH - 1)));
        }
        else
        {
            if (av.F("cranes") != 0)
                out_.Add(new Weight("Aircraft cranes", "aviation", av.F("cranes") * (15 + 2 * m), hx0, ZRel.Deck(4)));
            if (av.F("catapults") != 0)
                out_.Add(new Weight("Catapults", "aviation", av.F("catapults") * (10 + 2 * m), (dp.Park!.Value.A + hx0) / 2, ZRel.Deck(1)));
        }
        return out_;
    }

    public override (List<Weight> Std, List<Weight> Full) PayloadWeights(PyDict design, double L, double D, Geo geo, PyDict tun,
        Navarch.PayloadContext ctx)
    {
        var av = Aviation(design);
        var dp = DeckPlan(design);
        double n = av.F("aircraft"), m = av.F("aircraft_t");
        if (n == 0)
            return ([], []);
        var (hx0, hx1, _) = dp.Hangar;
        return ([
            new Weight("Air group", "aviation", n * m, (hx0 + hx1) / 2, ZRel.Deck(Py.Max(dp.FdH - 3, 2))),
            new Weight("Aviation ordnance", "aviation", ORDNANCE_K * n * m, geo.Magazine is { } mg ? mg.X : 0.2 * L,
                geo.Magazine is { } mg2 ? ZRel.Deck(mg2.Z) : ZRel.Frac(0.25)),
        ], [
            new Weight("Aviation fuel", "fuel", AVGAS_K * n * m, geo.Avgas is { } ag ? ag.X : -0.25 * L,
                geo.Avgas is { } ag2 ? ZRel.Deck(ag2.Z) : ZRel.Frac(0.15)),
        ]);
    }

    public override PyDict CrewExtra(PyDict design)
    {
        var av = Aviation(design);
        return PyDict.Of(("air_group", Py.Round(av.F("aircraft") * (6 + 0.75 * av.F("aircraft_t")))));
    }

    public override PyDict Results(PyDict design, Layout lay, Navarch.Result r)
    {
        var av = Aviation(design);
        var dp = DeckPlan(design);
        var out_ = PyDict.Of(("aircraft", av["aircraft"]), ("aircraft_capacity", dp.Capacity), ("hangar_area_m2", Py.Round(dp.HangarArea)),
            ("flight_deck", dp.Kind));
        if (dp.Kind != "none")
        {
            var ys = dp.Points!.Select(p => p.Y).ToList();
            out_.Update(("flight_deck_m", new List<object?> { Py.Round(dp.X1 - dp.X0, 1), Py.Round(Py.Max(ys) - Py.Min(ys), 1) }),
                ("flight_deck_height_m", Py.Round(r.Freeboard + dp.FdH, 2)));
        }
        return out_;
    }

    public override List<object?> Summary(PyDict design, Layout lay, Navarch.Result r)
    {
        var av = Aviation(design);
        var dp = DeckPlan(design);
        string line = $"aviation: {Py.Str(av["aircraft"])} aircraft of {Py.G(av.F("aircraft_t"))} t (capacity {dp.Capacity})   ";
        if (dp.Kind == "none")
            return [line + $"seaplane carrier, {Py.Str(av["cranes"])} cranes, {Py.Str(av["catapults"])} catapults"];
        var ys = dp.Points!.Select(p => p.Y).ToList();
        return [line + $"{dp.Kind} flight deck {Py.F(dp.X1 - dp.X0, 0)} x {Py.F(Py.Max(ys) - Py.Min(ys), 0)} m, " +
                $"{Py.Str(av["elevators"])} + {Py.Str(av["deck_edge_elevators"])} deck-edge elevators, {Py.Str(av["catapults"])} catapults"];
    }

    // ------------------------------------------------------------------ layouts

    static (Layout Lay, PyDict Hs, Hull Hull, double Shift) Common(PyDict design, double shift)
    {
        var lay = new Layout(design);
        var hs = Layout.HullSpec(design);
        var hull = new Hull(hs);
        lay.Hull = hull;
        lay.ShiftRange = (-0.04 * hull.L, 0.04 * hull.L);
        shift = Layout.Clamp(shift, lay.ShiftRange.Lo, lay.ShiftRange.Hi);
        lay.Geo.Shift = shift;
        return (lay, hs, hull, shift);
    }

    static void CheckCapacity(Layout lay, PyDict av, DeckPlanData dp)
    {
        if (av.F("aircraft") > dp.Capacity)
            lay.Fail("length", $"Air group of {Py.Str(av["aircraft"])} does not fit: the hangar and deck park hold about " +
                               $"{dp.Capacity} aircraft of {Py.G(av.F("aircraft_t"))} t. Add a hangar deck, or carry fewer or " +
                               "smaller aircraft.");
    }

    static double Machinery(Layout lay, PyDict design, Navarch.Result res, Hull hull, double mc)
    {
        double lMach = Layout.PlanMachinery(lay, design, res, hull, mc);
        if (lMach > 0.5 * hull.L)
            lay.Fail("length", $"The machinery needs {Py.F(lMach, 0)} m, more than half the hull. Use less power or a more " +
                               "compact plant.");
        lay.Geo.Machinery = (mc - lMach / 2, mc + lMach / 2);
        lay.Geo.MachineryX = mc;
        return lMach;
    }

    static void MachineryRooms(Layout lay, Hull hull, Navarch.Result res)
    {
        var (_, m1) = lay.Geo.Machinery!.Value;
        Layout.AddMachineryRooms(lay, Layout.StackMachinery(Layout.PlanSegments(lay.Geo.Plant!), m1), 0.8 * hull.B / 2, res.Depth);
    }

    static void Compartments(Layout lay, PyDict design, Hull hull, (double X0, double X1) mach, (double X0, double X1, double Hhw) hangar,
        List<PyDict> mounts)
    {
        var av = Aviation(design);
        double L = hull.L, B = hull.B;
        var (m0, m1) = mach;
        var cit = (m0 - 0.06 * L, m1 + 0.08 * L);
        Layout.SetCitadel(lay, cit.Item1, cit.Item2);
        double innerHw = 0.8 * B / 2;
        var (hx0, hx1, hhw) = hangar;
        double airT = av.F("aircraft") * av.F("aircraft_t");
        var st = Ordnance.Stow(lay, mounts, [
            PyDict.Of(("x0", m1), ("x1", cit.Item2), ("half_width", innerHw), ("rooms", new List<object?> {
                PyDict.Of(("id", "Aviation magazines"), ("tonnes", ORDNANCE_K * airT)),
                PyDict.Of(("id", "Gun magazines"), ("mounts", Ordnance.Guns(mounts).Cast<object?>().ToList())) })),
            PyDict.Of(("x0", cit.Item1), ("x1", m0), ("half_width", innerHw), ("rooms", new List<object?> {
                PyDict.Of(("id", "Aviation fuel"), ("kind", "fuel_tank"), ("tonnes", AVGAS_K * airT), ("t_per_m3", AVGAS_T_PER_M3)) })),
        ]);
        if (st.TryGetValue("Aviation magazines", out var am))
            lay.Geo.Magazine = ((am.X0 + am.X1) / 2, (am.Base + am.Top) / 2);
        if (st.TryGetValue("Aviation fuel", out var af))
            lay.Geo.Avgas = ((af.X0 + af.X1) / 2, (af.Base + af.Top) / 2);
        lay.Compartments.Add(PyDict.Of(("id", "Hangar"), ("kind", "hangar"), ("x0", hx0), ("x1", hx1), ("half_width", hhw), ("base", 0.0),
            ("top", Py.Eq(av["flight_deck"], "none") ? 2 * Layout.LEVEL_H : HANGAR_H * av.F("hangar_decks"))));
        Layout.AddSteering(lay);
    }

    static Layout FlightDeckLayout(PyDict design, Navarch.Result res, double shift0)
    {
        double depth = res.Depth;
        var (lay, hs, hull, shift) = Common(design, shift0);
        double L = hull.L, B = hull.B;
        var av = Aviation(design);
        var dp = DeckPlan(design);
        double fdH = dp.FdH, hw = dp.Hw;
        CheckCapacity(lay, av, dp);
        lay.Decks.Add(PyDict.Of(("id", "Flight deck"), ("kind", "flight_deck"), ("points", dp.Points), ("base", fdH - 1.0), ("top", fdH)));
        var fd = FlightDeckDrawing(dp, av, L);

        double mc = -0.04 * L + shift;
        Machinery(lay, design, res, hull, mc);
        double li = Layout.Clamp(0.11 * L, 8, 36);
        double wi = Layout.Clamp(0.3 * B, 4, 10);
        var (nfun, fw, fl) = Layout.PlanFunnels(lay, design, res, B, fdH + Layout.LEVEL_H * 4 + 3.0);
        fw = Py.Min(fw, wi - 1.0);
        fl = Py.Min(fl, 0.45 * li / nfun);
        wi = Py.Max(wi, fw + 1.2);
        double xi = 0.05 * L + shift;
        double yi = hw - wi / 2 - 0.3;
        var blocks = new List<PyDict>();
        var funnels = new List<PyDict>();
        double ix0 = xi - li / 2, ix1 = xi + li / 2;
        Layout.AddBlock(lay, blocks, "Island", ix0, ix1, wi, 1, 1.5, 1.0, y: yi, z0: fdH, role: "island");
        double fwd0 = ix0 + nfun * (fl + 1.0) + 1.0;
        Layout.AddBlock(lay, blocks, "Island upper", fwd0, ix1 - 0.5, 0.85 * wi, 2, 1.2, 0.8, y: yi, z0: fdH, role: "island");
        Layout.AddBlock(lay, blocks, "Bridge", fwd0 + 0.2 * (ix1 - fwd0), ix1 - 0.3, 0.9 * wi, 3, 0.4 * wi, 0.6, y: yi, z0: fdH, role: "bridge");
        long topLevel = Layout.TowerLevels(design, L >= 200 ? 4 : 3);
        for (long k = 4; k <= topLevel; k++)
        {
            double f = Py.Min(0.12, 0.03 * (k - 4));
            Layout.AddBlock(lay, blocks, $"Island tower {k}", fwd0 + (0.4 + f) * (ix1 - fwd0),
                Py.Max(fwd0 + (0.4 + f) * (ix1 - fwd0) + 3.0, ix1 - (0.25 + f) * (ix1 - fwd0)),
                Py.Max(3.0, 0.45 * wi * Py.Pow(0.9, k - 4)), k, 0.2 * wi, 0.2 * wi, y: yi, z0: fdH, role: "island");
        }
        double funTop = fdH + Layout.LEVEL_H * Math.Min(topLevel, 4) + 3.0;
        for (int i = 0; i < nfun; i++)
        {
            double fx = ix0 + 1.0 + (i + 0.5) * (fl + 1.0);
            funnels.Add(PyDict.Of(("id", $"Funnel {i + 1}"), ("x", fx), ("y", yi), ("l", fl), ("w", fw), ("pipes", fw > 4 ? 2L : 1L), ("z0", fdH),
                ("seg", Layout.FunnelSeg(lay, i))));
            lay.Occupy(Footprint.Rect(fx - fl / 2, yi - fw / 2, fx + fl / 2, yi + fw / 2), fdH, funTop, $"Funnel {i + 1}");
            Layout.AddFunnelWeights(lay, funnels[^1], funTop, mc, res.Depth);
        }
        var masts = new List<PyDict> { PyDict.Of(("x", fwd0 - 0.5), ("y", yi), ("yard", Py.Min(0.6 * wi, 6)), ("tripod", false),
            ("top", Py.Max(funTop + 5.0, fdH + Layout.LEVEL_H * topLevel + FireControl.HOOD_H + 2.0))) };
        Layout.MastWeight(lay, masts[0], masts[0].F("top"), "Mast");

        var mounts = new List<PyDict>();
        var turretTypes = new PyDict();
        bool islandGuns = Armament.BatteriesOf(design).Any(b => Py.Eq(b["where"], "ends") || Py.ToLong(b["count"]) % 2 != 0);

        var (hx0, hx1, _) = dp.Hangar;
        double le = Layout.Clamp(0.055 * L, 10, 18), ew = Py.Min(Layout.Clamp(0.45 * 2 * hw, 10, 18), 0.9 * hw);
        var fdElev = fd.L("elevators");
        for (long k = 0; k < av.I("elevators"); k++)
        {
            double ex = hx1 - le / 2 - 2 - k * (hx1 - hx0 - le - 4) / Math.Max(av.I("elevators") - 1, 1);
            fdElev.Add(PyDict.Of(("x", ex), ("y", -0.1 * hw), ("l", le), ("w", ew)));
        }
        double lee = Layout.Clamp(0.06 * L, 10, 20), wee = Layout.Clamp(0.045 * L, 6, 16);
        var land = dp.Land;
        var slots = new List<(double X, int Side)> { (ix1 + lee / 2 + 3, 1), (ix0 - lee / 2 - 3, 1),
            (land != null ? land.End.X - lee / 2 - 3 : xi, -1), (ix0 - 1.5 * lee - 8, 1) };
        if (islandGuns)
            slots = [slots[2], slots[1], slots[3], slots[0]];
        long placed = 0;
        var fdEdge = fd.L("edge_elevators");
        foreach (var (ex, side) in slots)
        {
            if (placed >= av.I("deck_edge_elevators"))
                break;
            var ed = Edges(dp, ex);
            double edge = side < 0 ? ed.Lo : ed.Hi;
            double yIn = edge - side * 0.5, yOut = edge + side * (wee - 0.5);
            var fp = Footprint.Rect(ex - lee / 2, Py.Min(yIn, yOut), ex + lee / 2, Py.Max(yIn, yOut));
            if (!lay.Free(fp, 0.5))
                continue;
            string eid = $"Deck-edge elevator {placed + 1}";
            lay.Occupy(fp, fdH - 1.0, fdH, eid);
            double yc = (yIn + yOut) / 2;
            fdEdge.Add(PyDict.Of(("x", ex), ("y", yc), ("l", lee), ("w", wee)));
            lay.Sponsons.Add(PyDict.Of(("id", eid), ("points", new List<Pt> { new(ex - lee / 2, yIn), new(ex + lee / 2, yIn), new(ex + lee / 2, yOut),
                new(ex - lee / 2, yOut) }), ("base", fdH - 1.0), ("top", fdH)));
            placed++;
        }
        if (placed < av.I("deck_edge_elevators"))
            lay.Fail("length", $"Only {placed} of {Py.Str(av["deck_edge_elevators"])} deck-edge elevators fit.");

        var xs = Vdc(64).Select(v => dp.X0 + 6 + v * (dp.X1 - dp.Tap - 10 - dp.X0)).ToList();

        List<object?[]> SponsonSlots(double reach, double bse)
        {
            var out_ = new List<object?[]>();
            foreach (var x in xs)
            {
                var (pe, se) = Edges(dp, x);
                out_.Add([x, se + reach + 0.3, bse, pe - reach - 0.3]);
            }
            return out_;
        }

        Armament.PlaceBatteries(lay, mounts, turretTypes, design,
            [new Armament.EndLine(ix1 + 1.0, +1, yi, 0, x => fdH, []), new Armament.EndLine(ix0 - 1.0, -1, yi, 180, x => fdH, [])],
            t => SponsonSlots(Armament.BodyReach(t), fdH - Geometry.TurretHeight(t) - 0.3), depth);
        var tp = design.Or("torpedoes", null) as PyDict ?? new PyDict();
        if (Py.Truthy(tp.Get("mounts")))
        {
            var (ttId, tt) = Armament.TorpedoType(tp);
            long n = Py.FloorDiv(Py.ToLong(tp["mounts"]) + 1, 2);
            Armament.SidePairs(lay, mounts, turretTypes, "torpedo", ttId, tt, n, SponsonSlots(tt.F("barrel_len") / 2 + 0.3, fdH - 2.5), "T",
                label: "Torpedo");
        }
        FireControl.Place(lay, design, blocks);
        var aaOut = new List<PyDict>();
        var aaReq = design.Or("aa", null) as PyDict ?? new PyDict();
        foreach (var (kind, count) in new[] { ("quad40", aaReq.Get("heavy", 0L)), ("single20", aaReq.Get("light", 0L)) })
        {
            double rr = Geometry.AA_CFG[kind].R;
            var island = Py.Sorted(Layout.RoofSpots(blocks, 2 * rr, 2 * rr), s => (s.Z0, Math.Abs(s.X - xi)))
                .Where(s => !s.Pair).Select(s => new object?[] { s.X, s.Y, s.Z0, null }).ToList();
            Armament.PlaceAa(lay, aaOut, kind, Py.ToLong(count), island.Concat(SponsonSlots(rr, fdH - 2.4)).ToList(),
                layerOf: bse => bse > fdH + 0.01 ? "upper" : "base");
        }
        var sponsons = new List<object?>();
        foreach (var it in mounts.Where(m => m.F("base") < fdH - 0.5).Concat(aaOut))
        {
            double reach = it.Has("dir") ? Geometry.AA_CFG[it.S("type")].R
                : !Py.Eq(it["kind"], "torpedo") ? Armament.BodyReach(it.D("t")) : it.D("t").F("barrel_len") / 2 + 0.3;
            var (pe, se) = Edges(dp, it.F("x"));
            int side = it.F("y") > 0 ? 1 : -1;
            double edge = side > 0 ? se : pe;
            double yIn = edge - side * 0.8, yOut = it.F("y") + side * (reach + 0.5);
            double l = 2 * reach + 1.0;
            sponsons.Add(PyDict.Of(("x", it["x"]), ("y", (yIn + yOut) / 2), ("l", l), ("w", Math.Abs(yOut - yIn))));
            lay.Sponsons.Add(PyDict.Of(("id", $"Sponson {it.S("id")}"), ("points", new List<Pt> { new(it.F("x") - l / 2, yIn),
                new(it.F("x") + l / 2, yIn), new(it.F("x") + l / 2, yOut), new(it.F("x") - l / 2, yOut) }), ("base", it.F("base") - 0.5),
                ("top", it["base"])));
        }

        MachineryRooms(lay, hull, res);
        Compartments(lay, design, hull, lay.Geo.Machinery!.Value, dp.Hangar, mounts);
        return Layout.FinishLayout(lay, design, hs, mounts, turretTypes, blocks, funnels, masts, aaOut, funTop, null,
            ("flight_deck", fd), ("sponsons", sponsons), ("boats", new List<object?>()));
    }

    static Layout SeaplaneLayout(PyDict design, Navarch.Result res, double shift0)
    {
        double depth = res.Depth;
        var (lay, hs, hull, shift) = Common(design, shift0);
        double L = hull.L, B = hull.B;
        var av = Aviation(design);
        var dp = DeckPlan(design);
        CheckCapacity(lay, av, dp);
        var blocks = new List<PyDict>();
        var funnels = new List<PyDict>();
        var mounts = new List<PyDict>();
        var turretTypes = new PyDict();
        var (hx0, hx1, hhw) = dp.Hangar;
        (hx0, hx1) = (hx0 + shift, hx1 + shift);
        Layout.AddBlock(lay, blocks, "Hangar", hx0, hx1, 2 * hhw, 1, 1.0, 0.5, role: "hangar");
        Layout.AddBlock(lay, blocks, "Hangar roof", hx0, hx1, 2 * hhw, 2, 1.0, 0.5, role: "hangar");
        string[] hangarIds = ["Hangar", "Hangar roof"];
        double roof = 2 * Layout.LEVEL_H;

        double bx1 = 0.24 * L + shift;
        double lb = Layout.Clamp(0.07 * L, 7, 16);
        double bx0 = bx1 - lb;
        double wb = Layout.Clamp(0.5 * B, 4.5, 12);
        Layout.AddBlock(lay, blocks, "Bridge base", bx0, bx1, wb, 1, 0.3 * wb, 1.0, role: "bridge", office: true);
        Layout.AddBlock(lay, blocks, "Bridge", bx0 + 0.15 * lb, bx1, 0.85 * wb, 2, 0.4 * wb, 1.0, role: "bridge");
        Layout.AddBlock(lay, blocks, "Bridge upper", bx0 + 0.35 * lb, bx1 - 0.05 * lb, 0.7 * wb, 3, 0.3 * wb, 0.8, role: "bridge");
        long nTower = Layout.TowerLevels(design, 3);
        for (long k = 4; k <= nTower; k++)
        {
            double f = Py.Min(0.12, 0.03 * (k - 4)), tw = Py.Max(3.0, 0.5 * wb * Py.Pow(0.9, k - 4));
            Layout.AddBlock(lay, blocks, $"Tower {k}", bx0 + (0.45 + f) * lb, Py.Max(bx0 + (0.45 + f) * lb + 3.0, bx1 - (0.15 + f) * lb), tw, k,
                0.5 * tw, 0.5 * tw, role: "bridge");
        }
        double funTop = Layout.LEVEL_H * Math.Min(nTower, 4) + 3.0;
        double mc = (hx1 + bx0) / 2;
        Machinery(lay, design, res, hull, mc);
        var (nfun, fw, fl) = Layout.PlanFunnels(lay, design, res, B, funTop);
        fw = Py.Min(fw, 0.3 * B);
        double room = bx0 - hx1 - 2.0;
        if (room < nfun * (fl + 1.0))
            lay.Fail("length", $"No room for {nfun} funnel(s) between the bridge and the hangar.");
        for (int i = 0; i < nfun; i++)
        {
            double fx = hx1 + 1.0 + (i + 0.5) * room / nfun;
            funnels.Add(PyDict.Of(("id", $"Funnel {i + 1}"), ("x", fx), ("y", 0.0), ("l", fl), ("w", fw), ("pipes", fw > 4 ? 2L : 1L),
                ("seg", Layout.FunnelSeg(lay, i))));
            lay.Occupy(Footprint.Rect(fx - fl / 2, -fw / 2, fx + fl / 2, fw / 2), 0, funTop, $"Funnel {i + 1}");
            Layout.AddFunnelWeights(lay, funnels[^1], funTop, mc, res.Depth);
        }
        var masts = new List<PyDict> { PyDict.Of(("x", bx0 - 1.0), ("yard", Py.Min(0.3 * B, 8)), ("tripod", false)) };
        if (Layout.LEVEL_H * nTower + FireControl.HOOD_H + 2.0 > funTop + 6.0)
            masts[0]["top"] = Layout.LEVEL_H * nTower + FireControl.HOOD_H + 2.0;
        Layout.MastWeight(lay, masts[0], masts[0].F("top", funTop + 6.0), "Mast");

        double park0 = dp.Park!.Value.A;
        var fittings = new List<object?>();
        var cranes = new List<object?>();
        double lc = Layout.Clamp(0.5 * (hx0 - park0), 8, 25);
        long ncat = av.I("catapults");
        for (long k = 0; k < ncat; k++)
        {
            double y = ncat == 1 ? 0.0 : ((double)k / (ncat - 1) - 0.5) * 0.5 * B;
            double cx = (park0 + hx0) / 2;
            fittings.Add(PyDict.Of(("x", cx), ("y", y), ("l", lc), ("w", 1.0), ("color", "track")));
            lay.Occupy(Footprint.Rect(cx - lc / 2, y - 0.8, cx + lc / 2, y + 0.8), 0, 1.5, $"Catapult {k + 1}");
        }
        double jib = Layout.Clamp(0.09 * L, 8, 16);
        var craneSlots = new[] { (hx0 - 2.5, hhw - 1.0, 135L), (hx0 - 2.5, -(hhw - 1.0), -135L), (park0 + 3, 0.0, 180L), (hx1 + 2.0, hhw, 45L) };
        for (int k = 0; k < Math.Min(av.I("cranes"), 4); k++)
        {
            var (cx, cy, d) = craneSlots[k];
            cranes.Add(PyDict.Of(("x", cx), ("y", cy), ("r", 1.4), ("dir", d), ("jib", jib), ("top", roof + 6.0)));
            lay.Occupy(Footprint.Circle(cx, cy, 1.6), 0, roof + 6.0, $"Crane {k + 1}");
        }

        var xs = Vdc(48).Select(v => hx0 + v * (bx1 - hx0)).ToList();
        Armament.PlaceBatteries(lay, mounts, turretTypes, design,
            [new Armament.EndLine(L / 2 - 0.08 * L, -1, 0.0, 0, x => 0.3, []), new Armament.EndLine(hx0 + 0.5, +1, 0.0, 180, x => roof, hangarIds)],
            t => xs.Select(x => new object?[] { x, hull.HalfWidth(x) - Armament.BodyReach(t) - 0.6, 0.0 }), depth);
        var tp = design.Or("torpedoes", null) as PyDict ?? new PyDict();
        if (Py.Truthy(tp.Get("mounts")))
        {
            var (ttId, tt) = Armament.TorpedoType(tp);
            double r = tt.F("barrel_len") / 2 + 0.3;
            Armament.SidePairs(lay, mounts, turretTypes, "torpedo", ttId, tt, Py.FloorDiv(Py.ToLong(tp["mounts"]) + 1, 2),
                xs.Select(x => new object?[] { x, hull.HalfWidth(x) - r - 0.4, 0.3 }), "T", label: "Torpedo");
        }
        FireControl.Place(lay, design, blocks);
        var aaOut = new List<PyDict>();
        var aaReq = design.Or("aa", null) as PyDict ?? new PyDict();
        foreach (var (kind, count) in new[] { ("quad40", aaReq.Get("heavy", 0L)), ("single20", aaReq.Get("light", 0L)) })
        {
            double rr = Geometry.AA_CFG[kind].R;
            var cands = Vdc(24).Select(v => new object?[] { hx0 + v * (hx1 - hx0), hhw - rr - 0.3, roof }).ToList();
            cands.AddRange(xs.Select(x => new object?[] { x, hull.HalfWidth(x) - rr - 0.5, 0.0 }));
            Armament.PlaceAa(lay, aaOut, kind, Py.ToLong(count), cands, ignore: _ => hangarIds);
        }

        var boats = new List<object?>();
        double bl_ = Layout.Clamp(0.03 * L, 4, 8);
        foreach (var x in Enumerable.Range(-4, 9).Select(k => (hx1 + bx0) / 2 + k * 2.0))
        {
            double y = 0.5 * B - 0.35 * bl_ - 1.0;
            var fps = new[] { 1, -1 }.Select(s => Footprint.Rect(x - bl_ / 2, s * y - 0.15 * bl_, x + bl_ / 2, s * y + 0.15 * bl_)).ToList();
            if (y > fw / 2 + 0.3 * bl_ && fps.All(fp => lay.Free(fp, 0.3)))
            {
                foreach (var (s, fp) in new[] { (1, fps[0]), (-1, fps[1]) })
                {
                    boats.Add(PyDict.Of(("x", x), ("y", s * y), ("l", bl_), ("w", 0.3 * bl_)));
                    lay.Occupy(fp, Layout.LEVEL_H, Layout.LEVEL_H + 1.5, $"Boat{boats.Count}");
                }
                break;
            }
        }

        MachineryRooms(lay, hull, res);
        Compartments(lay, design, hull, lay.Geo.Machinery!.Value, (hx0, hx1, hhw), mounts);
        return Layout.FinishLayout(lay, design, hs, mounts, turretTypes, blocks, funnels, masts, aaOut, funTop, null,
            ("fittings", fittings), ("cranes", cranes), ("boats", boats), ("bollards", new List<object?> { L / 2 - 0.05 * L, -L / 2 + 0.06 * L }),
            ("chain_x", L / 2 - 0.06 * L), ("hawse_back", 0.03 * L + 1.0));
    }
}
