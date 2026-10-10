namespace Fleetwright.Shipgen;

/// <summary>A carrier's aviation, defaults filled in: the flight deck ("axial", "angled" or "none" for a seaplane
/// carrier), the air group and its aircraft's weight, the hangar decks and whether the hangar is closed, the elevators,
/// catapults and cranes, and a number painted on the deck.</summary>
public sealed record AviationSpec(string FlightDeck, double Aircraft, double AircraftT, int HangarDecks, string Hangar,
    int Elevators, int DeckEdgeElevators, int Catapults, int Cranes, string? Number);

/// <summary>Aircraft carriers, from seaplane carriers to angled-deck fleet carriers.</summary>
public sealed class CarrierStyle : Style
{
    const double HangarH = 5.6, GalleryH = 2.0, SpotK = 20.0, AngleDeg = 9.0;
    const double OrdnanceK = 0.6, AvgasK = 1.2, AvgasTPerM3 = 0.5;
    static readonly string[] FlightDecks = ["axial", "angled", "none"];

    /// <summary>The elevators, deck-edge elevators, catapults and cranes a flight deck kind comes with.</summary>
    static (int Elevators, int DeckEdge, int Catapults, int Cranes) Defaults(string kind) => kind switch
    {
        "axial" => (2, 1, 1, 0),
        "angled" => (0, 4, 4, 0),
        "none" => (0, 0, 1, 2),
        _ => (0, 0, 0, 0),
    };

    public static AviationSpec Aviation(Design design)
    {
        var a = design.Aviation;
        string kind = a?.FlightDeck ?? "axial";
        var d = Defaults(kind);
        return new AviationSpec(kind, a?.Aircraft ?? 0, a?.AircraftT ?? 5.0, a?.HangarDecks ?? 1, a?.Hangar ?? "open",
            a?.Elevators ?? d.Elevators, a?.DeckEdgeElevators ?? d.DeckEdge, a?.Catapults ?? d.Catapults, a?.Cranes ?? d.Cranes, a?.Number);
    }

    /// <summary>A closed hangar: the flight deck is the hull's strength deck.</summary>
    static bool Closed(Design design)
    {
        var av = Aviation(design);
        return av.Hangar == "closed" && av.FlightDeck != "none";
    }

    static double SpotM2(AviationSpec av) => SpotK * Math.Pow(av.AircraftT, 2.0 / 3);

    /// <summary>0, 1/2, 1/4, 3/4, 1/8, ...: positions that spread evenly however many get used.</summary>
    public static List<double> Vdc(int n)
    {
        var result = new List<double>();
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
            result.Add(v);
        }
        return result;
    }

    /// <summary>Flight deck and hangar geometry, from the design alone (so navarch and the layout agree).</summary>
    public sealed record DeckPlanData(string Kind, double FdH, (double X0, double X1, double Hhw) Hangar, double HangarArea, double FdArea,
        long Capacity, double X0 = 0, double X1 = 0, double Tap = 0, double Hw = 0, List<Pt>? Points = null, Land? Land = null,
        (double A, double B)? Park = null);

    public sealed record Land(Pt P0, (double X, double Y) D, (double X, double Y) N, double Length, double Width, Pt End, Pt Corner);

    public static DeckPlanData DeckPlan(Design design)
    {
        var av = Aviation(design);
        double L = design.HullLength, B = design.HullBeam;
        string kind = av.FlightDeck;
        if (kind == "none")
        {
            double hx0n = -0.30 * L, hx1n = -0.02 * L, hhwn = 0.36 * B;
            double deckX0 = -L / 2 + 0.03 * L;
            double hangarAreaN = (hx1n - hx0n) * 2 * hhwn;
            double parkArea = (hx0n - deckX0) * 0.7 * B;
            return new DeckPlanData(kind, 0.0, (hx0n, hx1n, hhwn), hangarAreaN, 0.0,
                (long)((hangarAreaN * 0.85 + parkArea * 0.5) / SpotM2(av)), Park: (deckX0, hx0n));
        }
        double fdH = HangarH * av.HangarDecks + GalleryH;
        double x0 = -L / 2 - 0.02 * L, x1 = L / 2 - 0.03 * L;
        double tap = 0.06 * L, hw = 0.6 * B;
        var port = new List<Pt> { new(x0, -0.9 * hw), new(x0 + 3, -hw) };
        Land? land = null;
        if (kind == "angled")
        {
            double th = double.DegreesToRadians(AngleDeg);
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
        long cap = (long)((hangarArea * 0.85 * av.HangarDecks + fdArea * 0.30) / SpotM2(av));
        return new DeckPlanData(kind, fdH, (hx0, hx1, hhw), hangarArea, fdArea, cap, x0, x1, tap, hw, pts, land);
    }

    static (double Lo, double Hi) Edges(DeckPlanData dp, double x) =>
        Geometry.PolygonYSpan(dp.Points!, x) ?? (-dp.Hw, dp.Hw);

    /// <summary>Renderer spec for the flight deck.</summary>
    static FlightDeckDrawing FlightDeckDrawingOf(DeckPlanData dp, AviationSpec av, double L)
    {
        double x0 = dp.X0, x1 = dp.X1, tap = dp.Tap, hw = dp.Hw;
        var marks = new List<DeckMark>();
        var wires = new List<double[]>();
        var ys = dp.Points!.Select(p => p.Y).ToList();
        double lc = Layout.Clamp(12 + 2.8 * av.AircraftT, 20, 85);
        var land = dp.Land;
        (double, double) At(double t, double off = 0.0) =>
            (land!.P0.X + off * land.N.X + t * land.D.X, land.P0.Y + off * land.N.Y + t * land.D.Y);
        DeckMark Line((double X, double Y) a, (double X, double Y) b, string colour, double width) => new(a.X, a.Y, b.X, b.Y, colour, width);
        if (land != null)
        {
            double ll = land.Length, wl = land.Width;
            marks.Add(Line(At(8), At(ll - 5), "marking", 0.5) with { Dash = "5 5" });
            foreach (var off in new[] { wl / 2 - 1.0, -wl / 2 + 1.0 })
                marks.Add(Line(At(2, off), At(ll - 2, off), "marking", 0.35) with { Opacity = 0.85 });
            for (int k = 0; k < 4; k++)
            {
                double t = ll * (0.10 + 0.04 * k);
                var a3 = At(t, wl / 2 - 2);
                var b3 = At(t, -wl / 2 + 2);
                wires.Add([a3.Item1, a3.Item2, b3.Item1, b3.Item2]);
            }
            marks.Add(new DeckMark(land.Corner.X, 0, x1 - 4, 0, "marking", 0.5) { Dash = "5 5" });
            marks.Add(new DeckMark(x0 + 1.5, hw - 1.0, x1 - tap, hw - 1.0, "marking", 0.35) { Opacity = 0.85 });
        }
        else
        {
            marks.Add(new DeckMark(x0 + 4, 0, x1 - 4, 0, "marking", 0.5) { Dash = "5 5" });
            foreach (int side in new[] { -1, 1 })
                marks.Add(new DeckMark(x0 + 1.5, side * (hw - 1.0), x1 - tap, side * (hw - 1.0), "marking", 0.35) { Opacity = 0.85 });
            for (int k = 0; k < 8; k++)
            {
                double wx = x0 + (x1 - x0) * (0.06 + 0.02 * k);
                wires.Add([wx, -hw + 2, wx, hw - 2]);
            }
        }
        for (int i = 0; i < 6; i++)
        {
            double sx = x0 + 0.8 + i * 1.6;
            marks.Add(new DeckMark(sx, -0.9 * hw + 1.5, sx, 0.9 * hw - 1.5, "stripe", 0.7) { Opacity = i % 2 == 0 ? 0.9 : 0.0 });
        }
        for (int k = 0; k < Math.Min(av.Catapults, land != null ? 4 : 2); k++)
        {
            if (k < 2)
            {
                double y = (k == 0 ? -0.22 : 0.22) * hw;
                marks.Add(new DeckMark(x1 - 0.6 * tap, y, x1 - 0.6 * tap - lc, y, "track", 0.6));
            }
            else
            {
                double off = land!.Width / 2 - 6 - 7 * (k - 2);
                marks.Add(Line(At(land.Length - 10, off), At(land.Length - 10 - lc, off), "track", 0.6));
            }
        }
        return new FlightDeckDrawing
        {
            Planks = new FlightDeckPlanks(x0, x1, ys.Min(), ys.Max(), 1.4), Wires = wires, Marks = marks,
            Number = !string.IsNullOrEmpty(av.Number) ? new DeckNumber(x1 - 0.6 * tap - lc - 0.04 * L, 0, av.Number, 0.035 * L) : null,
        };
    }

    /// <summary>Carriers and merchants are built around something else; guns are fitted where they suit.</summary>
    public static List<string> GunsAreSecondaries(Style style, Design design)
    {
        var errs = new List<string>();
        if (design.Main is { Count: > 0 })
            errs.Add($"The {style.Name} style has no main battery: its guns are secondaries fitted where they suit. " +
                     "Give them as \"secondary\": a battery or a list of batteries, each with \"count\" (or " +
                     "\"per_side\") and \"where\": \"ends\" or \"sides\".");
        errs.AddRange(Armament.BatteriesOf(design).Where(b => b.Where is not ("ends" or "sides"))
            .Select(b => $"secondary.where = {Quote(b.Where)}: use ends or sides"));
        return errs;
    }

    public override string Name => "carrier";
    public override bool TakesSecondaryList => true;
    public override int MinTowerLevels => 3;

    protected override IEnumerable<Limit> StyleLimits =>
    [
        new(["hull", "block_coefficient"], 0.45, 0.76), new(["aviation", "aircraft"], 0, 160), new(["aviation", "aircraft_t"], 0.5, 35),
        new(["aviation", "hangar_decks"], 1, 2), new(["aviation", "elevators"], 0, 4), new(["aviation", "deck_edge_elevators"], 0, 4),
        new(["aviation", "catapults"], 0, 4), new(["aviation", "cranes"], 0, 4), new(["armour", "flight_deck_mm"], 0, 100),
    ];

    public override SizeRules Sizing => base.Sizing with { LbMax = 9.5 };

    public override List<string> Validate(Design design)
    {
        var av = Aviation(design);
        var errs = base.Validate(design);
        errs.AddRange(GunsAreSecondaries(this, design));
        if (!FlightDecks.Contains(av.FlightDeck))
            errs.Add($"aviation.flight_deck = {Quote(av.FlightDeck)}: use axial, angled or none");
        if (av.Hangar is not ("open" or "closed"))
            errs.Add($"aviation.hangar = {Quote(av.Hangar)}: use open or closed");
        return errs;
    }

    public override StrengthDeck? StrengthDeckOf(Design design, double D)
    {
        if (!Closed(design))
            return null;
        double fdH = DeckPlan(design).FdH;
        double mm = design.Armour?.FlightDeckMm ?? 0;
        return new StrengthDeck(fdH, Aviation(design).HangarDecks, mm != 0 ? [(mm, D + fdH)] : []);
    }

    public override Tuning Tuning(Design design) => new()
    {
        FreeboardA = 0.024, FreeboardB = 2.5, MiscFrac = 0.075, HullZFrac = 0.5, FlightDeckTPerM2 = 0.34, HangarTPerM2 = 0.32,
    };

    public override Layout BuildLayout(Design design, Navarch.Result res, double shift = 0.0, double spread = 0.0) =>
        Aviation(design).FlightDeck == "none" ? SeaplaneLayout(design, res, shift) : FlightDeckLayout(design, res, shift);

    public override List<Weight> RoughPayload(Design design, double D)
    {
        double L = design.HullLength;
        return [new Weight("Island", "superstructure", 0.006 * Math.Pow(L, 2), zRel: ZRel.Deck(DeckPlan(design).FdH + 4))];
    }

    public override (double Area, double X, double Z) WeatherDeck(Design design, double L, double B)
    {
        var dp = DeckPlan(design);
        if (dp.Kind == "none")
            return base.WeatherDeck(design, L, B);
        return (dp.FdArea, (dp.X0 + dp.X1) / 2, dp.FdH);
    }

    public override List<Weight> StructureWeights(Design design, double L, double B, double T, double D, Geo geo, Tuning tun)
    {
        var dp = DeckPlan(design);
        var av = Aviation(design);
        double m = av.AircraftT;
        var (hx0, hx1, _) = dp.Hangar;
        var result = new List<Weight>();
        if (dp.Kind != "none")
        {
            double fdx = (dp.X0 + dp.X1) / 2;
            double fdT = dp.FdArea * tun.FlightDeckTPerM2;
            if (Closed(design))
                fdT = Math.Max(0.0, fdT - HullWeight.DeckArea(L, B, design.BlockCoefficient)
                    * HullWeight.DeckTPerM2(L, HullWeight.ConstructionOf(design)));
            result.Add(new Weight("Flight deck", "hull", fdT, fdx, ZRel.Deck(dp.FdH)));
            double mm = design.Armour?.FlightDeckMm ?? 0;
            if (mm != 0)
                result.Add(new Weight("Flight deck armour", "armour", dp.FdArea * 0.85 * mm / 1000 * Weight.Steel, fdx, ZRel.Deck(dp.FdH)));
            result.Add(new Weight("Hangar structure", "hull", dp.HangarArea * av.HangarDecks * tun.HangarTPerM2, (hx0 + hx1) / 2,
                ZRel.Deck(dp.FdH / 2)));
            double nEl = av.Elevators + av.DeckEdgeElevators;
            if (nEl != 0)
                result.Add(new Weight("Elevators", "aviation", nEl * (8 + 3 * m), (hx0 + hx1) / 2, ZRel.Deck(dp.FdH - 1)));
            result.Add(new Weight("Arresting gear", "aviation", 30 + 2 * m, dp.X0 + 0.15 * (dp.X1 - dp.X0), ZRel.Deck(dp.FdH - 1)));
            if (av.Catapults != 0)
                result.Add(new Weight("Catapults", "aviation", av.Catapults * (15 + 3 * m), 0.3 * L, ZRel.Deck(dp.FdH - 1)));
        }
        else
        {
            if (av.Cranes != 0)
                result.Add(new Weight("Aircraft cranes", "aviation", av.Cranes * (15 + 2 * m), hx0, ZRel.Deck(4)));
            if (av.Catapults != 0)
                result.Add(new Weight("Catapults", "aviation", av.Catapults * (10 + 2 * m), (dp.Park!.Value.A + hx0) / 2, ZRel.Deck(1)));
        }
        return result;
    }

    public override (List<Weight> Std, List<Weight> Full) PayloadWeights(Design design, double L, double D, Geo geo, Tuning tun,
        Navarch.PayloadContext ctx)
    {
        var av = Aviation(design);
        var dp = DeckPlan(design);
        double n = av.Aircraft, m = av.AircraftT;
        if (n == 0)
            return ([], []);
        var (hx0, hx1, _) = dp.Hangar;
        return ([
            new Weight("Air group", "aviation", n * m, (hx0 + hx1) / 2, ZRel.Deck(Math.Max(dp.FdH - 3, 2))),
            new Weight("Aviation ordnance", "aviation", OrdnanceK * n * m, geo.Magazine is { } mg ? mg.X : 0.2 * L,
                geo.Magazine is { } mg2 ? ZRel.Deck(mg2.Z) : ZRel.Frac(0.25)),
        ], [
            new Weight("Aviation fuel", "fuel", AvgasK * n * m, geo.Avgas is { } ag ? ag.X : -0.25 * L,
                geo.Avgas is { } ag2 ? ZRel.Deck(ag2.Z) : ZRel.Frac(0.15)),
        ]);
    }

    public override List<(string Name, long Men)> CrewExtra(Design design)
    {
        var av = Aviation(design);
        return [("air_group", (long)Math.Round(av.Aircraft * (6 + 0.75 * av.AircraftT)))];
    }

    public override void AddResults(Results results, Design design, Layout lay, Navarch.Result r)
    {
        var av = Aviation(design);
        var dp = DeckPlan(design);
        results.Aircraft = av.Aircraft;
        results.AircraftCapacity = dp.Capacity;
        results.HangarAreaM2 = (long)Math.Round(dp.HangarArea);
        results.FlightDeck = dp.Kind;
        if (dp.Kind == "none")
            return;
        var ys = dp.Points!.Select(p => p.Y).ToList();
        results.FlightDeckM = [Math.Round(dp.X1 - dp.X0, 1), Math.Round(ys.Max() - ys.Min(), 1)];
        results.FlightDeckHeightM = Math.Round(r.Freeboard + dp.FdH, 2);
    }

    public override List<string> Summary(Design design, Layout lay, Navarch.Result r)
    {
        var av = Aviation(design);
        var dp = DeckPlan(design);
        string line = $"aviation: {av.Aircraft} aircraft of {av.AircraftT} t (capacity {dp.Capacity})   ";
        if (dp.Kind == "none")
            return [line + $"seaplane carrier, {av.Cranes} cranes, {av.Catapults} catapults"];
        var ys = dp.Points!.Select(p => p.Y).ToList();
        return [line + $"{dp.Kind} flight deck {dp.X1 - dp.X0:F0} x {ys.Max() - ys.Min():F0} m, " +
                $"{av.Elevators} + {av.DeckEdgeElevators} deck-edge elevators, {av.Catapults} catapults"];
    }

    // ------------------------------------------------------------------ layouts

    static (Layout Lay, HullSpec Hs, Hull Hull, double Shift) Common(Design design, double shift)
    {
        var lay = new Layout(design);
        var hs = Layout.HullSpecOf(design);
        var hull = new Hull(hs);
        lay.Hull = hull;
        lay.ShiftRange = (-0.04 * hull.L, 0.04 * hull.L);
        shift = Layout.Clamp(shift, lay.ShiftRange.Lo, lay.ShiftRange.Hi);
        lay.Geo.Shift = shift;
        return (lay, hs, hull, shift);
    }

    static void CheckCapacity(Layout lay, AviationSpec av, DeckPlanData dp)
    {
        if (av.Aircraft > dp.Capacity)
            lay.Fail("length", $"Air group of {av.Aircraft} does not fit: the hangar and deck park hold about " +
                               $"{dp.Capacity} aircraft of {av.AircraftT} t. Add a hangar deck, or carry fewer or " +
                               "smaller aircraft.");
    }

    static double Machinery(Layout lay, Design design, Navarch.Result res, Hull hull, double mc)
    {
        double lMach = Layout.PlanMachinery(lay, design, res, hull, mc);
        if (lMach > 0.5 * hull.L)
            lay.Fail("length", $"The machinery needs {lMach:F0} m, more than half the hull. Use less power or a more " +
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

    static void Compartments(Layout lay, Design design, Hull hull, (double X0, double X1) mach, (double X0, double X1, double Hhw) hangar,
        List<Mount> mounts)
    {
        var av = Aviation(design);
        double L = hull.L, B = hull.B;
        var (m0, m1) = mach;
        var cit = (m0 - 0.06 * L, m1 + 0.08 * L);
        Layout.SetCitadel(lay, cit.Item1, cit.Item2);
        double innerHw = 0.8 * B / 2;
        var (hx0, hx1, hhw) = hangar;
        double airT = av.Aircraft * av.AircraftT;
        var st = Ordnance.Stow(lay, mounts, [
            new Zone(m1, cit.Item2, innerHw, [new ZoneRoom("Aviation magazines", Tonnes: OrdnanceK * airT),
                new ZoneRoom("Gun magazines", Ordnance.Guns(mounts))]),
            new Zone(cit.Item1, m0, innerHw, [new ZoneRoom("Aviation fuel", Tonnes: AvgasK * airT, Kind: "fuel_tank", TPerM3: AvgasTPerM3)]),
        ]);
        if (st.TryGetValue("Aviation magazines", out var am))
            lay.Geo.Magazine = ((am.X0 + am.X1) / 2, (am.Base + am.Top) / 2);
        if (st.TryGetValue("Aviation fuel", out var af))
            lay.Geo.Avgas = ((af.X0 + af.X1) / 2, (af.Base + af.Top) / 2);
        lay.Compartments.Add(new Compartment
        {
            Id = "Hangar", Kind = "hangar", X0 = hx0, X1 = hx1, HalfWidth = hhw, Base = 0.0,
            Top = av.FlightDeck == "none" ? 2 * Layout.LevelH : HangarH * av.HangarDecks,
        });
        Layout.AddSteering(lay);
    }

    static Layout FlightDeckLayout(Design design, Navarch.Result res, double shift0)
    {
        double depth = res.Depth;
        var (lay, hs, hull, shift) = Common(design, shift0);
        lay.Topside = Topside.Of(design, res.Freeboard);
        double L = hull.L, B = hull.B;
        var av = Aviation(design);
        var dp = DeckPlan(design);
        double fdH = dp.FdH, hw = dp.Hw;
        CheckCapacity(lay, av, dp);
        lay.Decks.Add(new DeckPlate("Flight deck", "flight_deck", dp.Points!, fdH - 1.0, fdH));
        var fd = FlightDeckDrawingOf(dp, av, L);

        double mc = -0.04 * L + shift;
        Machinery(lay, design, res, hull, mc);
        double li = Layout.Clamp(0.11 * L, 8, 36);
        double wi = Layout.Clamp(0.3 * B, 4, 10);
        var (nfun, fw, fl) = Layout.PlanFunnels(lay, design, res, B, fdH + Layout.LevelH * 4 + 3.0);
        fw = Math.Min(fw, wi - 1.0);
        fl = Math.Min(fl, 0.45 * li / nfun);
        wi = Math.Max(wi, fw + 1.2);
        double xi = 0.05 * L + shift;
        double yi = hw - wi / 2 - 0.3;
        var blocks = new List<Block>();
        var funnels = new List<Funnel>();
        double ix0 = xi - li / 2, ix1 = xi + li / 2;
        Layout.AddBlock(lay, blocks, "Island", ix0, ix1, wi, 1, 1.5, 1.0, y: yi, z0: fdH, role: "island");
        double fwd0 = ix0 + nfun * (fl + 1.0) + 1.0;
        Layout.AddBlock(lay, blocks, "Island upper", fwd0, ix1 - 0.5, 0.85 * wi, 2, 1.2, 0.8, y: yi, z0: fdH, role: "island");
        Layout.AddBlock(lay, blocks, "Bridge", fwd0 + 0.2 * (ix1 - fwd0), ix1 - 0.3, 0.9 * wi, 3, 0.4 * wi, 0.6, y: yi, z0: fdH, role: "bridge");
        long topLevel = Layout.TowerLevels(design, L >= 200 ? 4 : 3);
        for (long k = 4; k <= topLevel; k++)
        {
            double f = Math.Min(0.12, 0.03 * (k - 4));
            Layout.AddBlock(lay, blocks, $"Island tower {k}", fwd0 + (0.4 + f) * (ix1 - fwd0),
                Math.Max(fwd0 + (0.4 + f) * (ix1 - fwd0) + 3.0, ix1 - (0.25 + f) * (ix1 - fwd0)),
                Math.Max(3.0, 0.45 * wi * Math.Pow(0.9, k - 4)), k, 0.2 * wi, 0.2 * wi, y: yi, z0: fdH, role: "island");
        }
        double funTop = fdH + Layout.LevelH * Math.Min(topLevel, 4) + 3.0;
        for (int i = 0; i < nfun; i++)
        {
            double fx = ix0 + 1.0 + (i + 0.5) * (fl + 1.0);
            funnels.Add(new Funnel { Id = $"Funnel {i + 1}", X = fx, Y = yi, L = fl, W = fw, Pipes = fw > 4 ? 2 : 1, Z0 = fdH, Seg = Layout.FunnelSeg(lay, i) });
            lay.Occupy(Footprint.Rect(fx - fl / 2, yi - fw / 2, fx + fl / 2, yi + fw / 2), fdH, funTop, $"Funnel {i + 1}");
            Layout.AddFunnelWeights(lay, funnels[^1], funTop, mc, res.Depth);
        }
        var masts = new List<Mast>
        {
            new() { X = fwd0 - 0.5, Y = yi, Yard = Math.Min(0.6 * wi, 6), Tripod = false,
                Top = Math.Max(funTop + 5.0, fdH + Layout.LevelH * topLevel + FireControl.HoodH + 2.0) },
        };
        Layout.MastWeight(lay, masts[0], masts[0].Top!.Value, "Mast");

        var mounts = new List<Mount>();
        var turretTypes = new OrderedDictionary<string, TurretType>(StringComparer.Ordinal);
        bool islandGuns = Armament.BatteriesOf(design).Any(b => b.Where == "ends" || b.MountCount % 2 != 0);

        var (hx0, hx1, _) = dp.Hangar;
        double le = Layout.Clamp(0.055 * L, 10, 18), ew = Math.Min(Layout.Clamp(0.45 * 2 * hw, 10, 18), 0.9 * hw);
        var fdElev = fd.Elevators;
        for (long k = 0; k < av.Elevators; k++)
        {
            double ex = hx1 - le / 2 - 2 - k * (hx1 - hx0 - le - 4) / Math.Max(av.Elevators - 1, 1);
            fdElev.Add(new ElevatorDrawing(ex, -0.1 * hw, le, ew));
        }
        double lee = Layout.Clamp(0.06 * L, 10, 20), wee = Layout.Clamp(0.045 * L, 6, 16);
        var land = dp.Land;
        var slots = new List<(double X, int Side)> { (ix1 + lee / 2 + 3, 1), (ix0 - lee / 2 - 3, 1),
            (land != null ? land.End.X - lee / 2 - 3 : xi, -1), (ix0 - 1.5 * lee - 8, 1) };
        if (islandGuns)
            slots = [slots[2], slots[1], slots[3], slots[0]];
        long placed = 0;
        foreach (var (ex, side) in slots)
        {
            if (placed >= av.DeckEdgeElevators)
                break;
            var ed = Edges(dp, ex);
            double edge = side < 0 ? ed.Lo : ed.Hi;
            double yIn = edge - side * 0.5, yOut = edge + side * (wee - 0.5);
            var fp = Footprint.Rect(ex - lee / 2, Math.Min(yIn, yOut), ex + lee / 2, Math.Max(yIn, yOut));
            if (!lay.Free(fp, 0.5))
                continue;
            string eid = $"Deck-edge elevator {placed + 1}";
            lay.Occupy(fp, fdH - 1.0, fdH, eid);
            double yc = (yIn + yOut) / 2;
            lay.Sponsons.Add(new DeckPlate(eid, "sponson", [new(ex - lee / 2, yIn), new(ex + lee / 2, yIn), new(ex + lee / 2, yOut),
                new(ex - lee / 2, yOut)], fdH - 1.0, fdH, "elevator"));
            placed++;
        }
        if (placed < av.DeckEdgeElevators)
            lay.Fail("length", $"Only {placed} of {av.DeckEdgeElevators} deck-edge elevators fit.");

        var xs = Vdc(64).Select(v => dp.X0 + 6 + v * (dp.X1 - dp.Tap - 10 - dp.X0)).ToList();

        List<Slot> SponsonSlots(double reach, double bse)
        {
            var result = new List<Slot>();
            foreach (var x in xs)
            {
                var (pe, se) = Edges(dp, x);
                result.Add(new Slot(x, se + reach + 0.3, bse, pe - reach - 0.3));
            }
            return result;
        }

        Armament.PlaceBatteries(lay, mounts, turretTypes, design,
            [new Armament.EndLine(ix1 + 1.0, +1, yi, 0, x => fdH, []), new Armament.EndLine(ix0 - 1.0, -1, yi, 180, x => fdH, [])],
            t => SponsonSlots(Armament.BodyReach(t), fdH - Geometry.TurretHeight(t) - 0.3), depth);
        if (design.Torpedoes is { Mounts: > 0 } tp)
        {
            var (ttId, tt) = Armament.TorpedoType(tp);
            long n = (tp.Mounts.Value + 1) / 2;
            Armament.SidePairs(lay, mounts, turretTypes, "torpedo", ttId, tt, n, SponsonSlots(tt.BarrelLen / 2 + 0.3, fdH - 2.5), "T",
                label: "Torpedo");
        }
        FireControl.Place(lay, design, blocks);
        var aaOut = new List<AaMount>();
        foreach (var (kind, count) in new[] { ("quad40", design.Aa?.Heavy ?? 0), ("single20", design.Aa?.Light ?? 0) })
        {
            double rr = Geometry.AaCfg[kind].R;
            var island = Layout.RoofSpots(blocks, 2 * rr, 2 * rr).OrderBy(s => (s.Z0, Math.Abs(s.X - xi))).ToList()
                .Where(s => !s.Pair).Select(s => new Slot(s.X, s.Y, s.Z0, Lone: true)).ToList();
            Armament.PlaceAa(lay, aaOut, kind, count, island.Concat(SponsonSlots(rr, fdH - 2.4)).ToList());
        }
        var below = mounts.Where(m => m.Base < fdH - 0.5)
            .Select(m => (m.Id, m.X, m.Y, m.Base, Reach: m.Kind != "torpedo" ? Armament.BodyReach(m.T) : m.T.BarrelLen / 2 + 0.3))
            .Concat(aaOut.Select(a => (a.Id, a.X, a.Y, a.Base, Reach: Geometry.AaCfg[a.Type].R)));
        foreach (var (id, x, y, bse, reach) in below)
        {
            var (pe, se) = Edges(dp, x);
            int side = y > 0 ? 1 : -1;
            double edge = side > 0 ? se : pe;
            double yIn = edge - side * 0.8, yOut = y + side * (reach + 0.5);
            double l = 2 * reach + 1.0;
            lay.Sponsons.Add(new DeckPlate($"Sponson {id}", "sponson", [new(x - l / 2, yIn), new(x + l / 2, yIn), new(x + l / 2, yOut),
                new(x - l / 2, yOut)], bse - 0.5, bse));
        }

        MachineryRooms(lay, hull, res);
        Compartments(lay, design, hull, lay.Geo.Machinery!.Value, dp.Hangar, mounts);
        Layout.FinishLayout(lay, design, hs, mounts, turretTypes, blocks, funnels, masts, aaOut, funTop);
        (lay.Dressing.FlightDeck, lay.Dressing.Boats) = (fd, []);
        return lay;
    }

    static Layout SeaplaneLayout(Design design, Navarch.Result res, double shift0)
    {
        double depth = res.Depth;
        var (lay, hs, hull, shift) = Common(design, shift0);
        lay.Topside = Topside.Of(design, res.Freeboard);
        double L = hull.L, B = hull.B;
        var av = Aviation(design);
        var dp = DeckPlan(design);
        CheckCapacity(lay, av, dp);
        var blocks = new List<Block>();
        var funnels = new List<Funnel>();
        var mounts = new List<Mount>();
        var turretTypes = new OrderedDictionary<string, TurretType>(StringComparer.Ordinal);
        var (hx0, hx1, hhw) = dp.Hangar;
        (hx0, hx1) = (hx0 + shift, hx1 + shift);
        Layout.AddBlock(lay, blocks, "Hangar", hx0, hx1, 2 * hhw, 1, 1.0, 0.5, role: "hangar");
        Layout.AddBlock(lay, blocks, "Hangar roof", hx0, hx1, 2 * hhw, 2, 1.0, 0.5, role: "hangar");
        string[] hangarIds = ["Hangar", "Hangar roof"];
        double roof = 2 * Layout.LevelH;

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
            double f = Math.Min(0.12, 0.03 * (k - 4)), tw = Math.Max(3.0, 0.5 * wb * Math.Pow(0.9, k - 4));
            Layout.AddBlock(lay, blocks, $"Tower {k}", bx0 + (0.45 + f) * lb, Math.Max(bx0 + (0.45 + f) * lb + 3.0, bx1 - (0.15 + f) * lb), tw, k,
                0.5 * tw, 0.5 * tw, role: "bridge");
        }
        double funTop = Layout.LevelH * Math.Min(nTower, 4) + 3.0;
        double mc = (hx1 + bx0) / 2;
        Machinery(lay, design, res, hull, mc);
        var (nfun, fw, fl) = Layout.PlanFunnels(lay, design, res, B, funTop);
        fw = Math.Min(fw, 0.3 * B);
        double room = bx0 - hx1 - 2.0;
        if (room < nfun * (fl + 1.0))
            lay.Fail("length", $"No room for {nfun} funnel(s) between the bridge and the hangar.");
        for (int i = 0; i < nfun; i++)
        {
            double fx = hx1 + 1.0 + (i + 0.5) * room / nfun;
            funnels.Add(new Funnel { Id = $"Funnel {i + 1}", X = fx, Y = 0.0, L = fl, W = fw, Pipes = fw > 4 ? 2 : 1, Seg = Layout.FunnelSeg(lay, i) });
            lay.Occupy(Footprint.Rect(fx - fl / 2, -fw / 2, fx + fl / 2, fw / 2), 0, funTop, $"Funnel {i + 1}");
            Layout.AddFunnelWeights(lay, funnels[^1], funTop, mc, res.Depth);
        }
        var masts = new List<Mast> { new() { X = bx0 - 1.0, Yard = Math.Min(0.3 * B, 8), Tripod = false } };
        if (Layout.LevelH * nTower + FireControl.HoodH + 2.0 > funTop + 6.0)
            masts[0].Top = Layout.LevelH * nTower + FireControl.HoodH + 2.0;
        Layout.MastWeight(lay, masts[0], masts[0].Top ?? funTop + 6.0, "Mast");

        double park0 = dp.Park!.Value.A;
        var fittings = new List<Fitting>();
        var cranes = new List<Crane>();
        double lc = Layout.Clamp(0.5 * (hx0 - park0), 8, 25);
        long ncat = av.Catapults;
        for (long k = 0; k < ncat; k++)
        {
            double y = ncat == 1 ? 0.0 : ((double)k / (ncat - 1) - 0.5) * 0.5 * B;
            double cx = (park0 + hx0) / 2;
            fittings.Add(new Fitting(cx, y, lc, 1.0, "track"));
            lay.Occupy(Footprint.Rect(cx - lc / 2, y - 0.8, cx + lc / 2, y + 0.8), 0, 1.5, $"Catapult {k + 1}");
        }
        double jib = Layout.Clamp(0.09 * L, 8, 16);
        var craneSlots = new[] { (hx0 - 2.5, hhw - 1.0, 135L), (hx0 - 2.5, -(hhw - 1.0), -135L), (park0 + 3, 0.0, 180L), (hx1 + 2.0, hhw, 45L) };
        for (int k = 0; k < Math.Min(av.Cranes, 4); k++)
        {
            var (cx, cy, d) = craneSlots[k];
            cranes.Add(new Crane(cx, cy, 1.4, d, jib, roof + 6.0));
            lay.Occupy(Footprint.Circle(cx, cy, 1.6), 0, roof + 6.0, $"Crane {k + 1}");
        }

        var xs = Vdc(48).Select(v => hx0 + v * (bx1 - hx0)).ToList();
        Armament.PlaceBatteries(lay, mounts, turretTypes, design,
            [new Armament.EndLine(L / 2 - 0.08 * L, -1, 0.0, 0, x => 0.3, []), new Armament.EndLine(hx0 + 0.5, +1, 0.0, 180, x => roof, hangarIds)],
            t => xs.Select(x => new Slot(x, lay.DeckHalfWidth(x) - Armament.BodyReach(t) - 0.6, 0.0)), depth);
        if (design.Torpedoes is { Mounts: > 0 } tp)
        {
            var (ttId, tt) = Armament.TorpedoType(tp);
            double r = tt.BarrelLen / 2 + 0.3;
            Armament.SidePairs(lay, mounts, turretTypes, "torpedo", ttId, tt, (tp.Mounts.Value + 1) / 2,
                xs.Select(x => new Slot(x, lay.DeckHalfWidth(x) - r - 0.4, 0.3)), "T", label: "Torpedo");
        }
        FireControl.Place(lay, design, blocks);
        var aaOut = new List<AaMount>();
        foreach (var (kind, count) in new[] { ("quad40", design.Aa?.Heavy ?? 0), ("single20", design.Aa?.Light ?? 0) })
        {
            double rr = Geometry.AaCfg[kind].R;
            var cands = Vdc(24).Select(v => new Slot(hx0 + v * (hx1 - hx0), hhw - rr - 0.3, roof)).ToList();
            cands.AddRange(xs.Select(x => new Slot(x, lay.DeckHalfWidth(x) - rr - 0.5, 0.0)));
            Armament.PlaceAa(lay, aaOut, kind, count, cands, ignore: _ => hangarIds);
        }

        var boats = new List<Boat>();
        double bl_ = Layout.Clamp(0.03 * L, 4, 8);
        foreach (var x in Enumerable.Range(-4, 9).Select(k => (hx1 + bx0) / 2 + k * 2.0))
        {
            double y = 0.5 * B - 0.35 * bl_ - 1.0;
            var fps = new[] { 1, -1 }.Select(s => Footprint.Rect(x - bl_ / 2, s * y - 0.15 * bl_, x + bl_ / 2, s * y + 0.15 * bl_)).ToList();
            if (y > fw / 2 + 0.3 * bl_ && fps.All(fp => lay.Free(fp, 0.3)))
            {
                foreach (var (s, fp) in new[] { (1, fps[0]), (-1, fps[1]) })
                {
                    boats.Add(new Boat(x, s * y, bl_, 0.3 * bl_));
                    lay.Occupy(fp, Layout.LevelH, Layout.LevelH + 1.5, $"Boat{boats.Count}");
                }
                break;
            }
        }

        MachineryRooms(lay, hull, res);
        Compartments(lay, design, hull, lay.Geo.Machinery!.Value, (hx0, hx1, hhw), mounts);
        Layout.FinishLayout(lay, design, hs, mounts, turretTypes, blocks, funnels, masts, aaOut, funTop);
        var spec = lay.Dressing;
        (spec.Fittings, spec.Cranes, spec.Boats) = (fittings, cranes, boats);
        (spec.Bollards, spec.ChainX, spec.HawseBack) = ([L / 2 - 0.05 * L, -L / 2 + 0.06 * L], L / 2 - 0.06 * L, 0.03 * L + 1.0);
        return lay;
    }
}
