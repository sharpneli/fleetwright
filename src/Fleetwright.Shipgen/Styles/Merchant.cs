namespace Fleetwright.Shipgen;

/// <summary>merchant: cargo ships and tankers (a "three-island" ship).</summary>
public sealed class MerchantStyle : Style
{
    const double RaisedH = Layout.LevelH;
    static readonly Dictionary<string, double> Stowage = new() { ["dry"] = 1.4, ["tanker"] = 1.25 };
    const double DoubleBottom = 1.2;

    /// <summary>Volume of the holds below the main deck.</summary>
    static double HoldVolume(Hull hull, List<(double H0, double H1)> holds, double depth)
    {
        double area = holds.Select(h => (h.H1 - h.H0) / 8 *
            Enumerable.Range(0, 8).Select(k => 2 * hull.HalfWidth(h.H0 + (h.H1 - h.H0) * (k + 0.5) / 8)).Sum()).Sum();
        return 0.9 * area * Math.Max(0.0, depth - DoubleBottom);
    }

    /// <summary>The cargo: "dry" or "tanker", and its tonnes.</summary>
    static (string Kind, double DeadweightT) Cargo(Design design) => (design.Cargo?.Kind ?? "dry", design.Cargo?.DeadweightT ?? 0.0);

    /// <summary>Where the machinery stands: "amidships" or "aft".</summary>
    static string MachineryPosition(Design design) => design.Machinery?.Position ?? "amidships";

    /// <summary>Spread C tonnes of cargo over the holds so the loaded ship floats level, as far as the holds allow.</summary>
    static List<double> Stow(double C, List<(double X0, double X1)> holds, Navarch.PayloadContext ctx, double fill = 1.5)
    {
        var lens = holds.Select(h => h.X1 - h.X0).ToList();
        var xs = holds.Select(h => (h.X0 + h.X1) / 2).ToList();
        double total = lens.Sum();
        var prop = lens.Select(l => C * l / total).ToList();
        var caps = prop.Select(p => fill * p).ToList();
        double wLight = ctx.Items.Select(w => w.W).Sum();
        double mLight = ctx.Items.Select(w => w.W * w.X).Sum();
        double full = wLight + ctx.Fuel + C;
        double xc = (full * ctx.Lcb - mLight - ctx.Fuel * ctx.FuelX) / C;

        List<double> Packed(IEnumerable<int> order)
        {
            var result = Enumerable.Repeat(0.0, holds.Count).ToList();
            double left = C;
            foreach (int i in order)
            {
                result[i] = Math.Min(caps[i], left);
                left -= result[i];
            }
            return result;
        }

        double Centre(List<double> ws) => ws.Zip(xs).Select(t => t.First * t.Second).Sum() / C;
        double xP = Centre(prop);
        var end = Packed(Enumerable.Range(0, holds.Count).OrderBy(i => xc > xP ? -xs[i] : xs[i]).ToList());
        double xE = Centre(end);
        double a = Math.Abs(xE - xP) < 1e-9 ? 0.0 : Math.Max(0.0, Math.Min(1.0, (xc - xP) / (xE - xP)));
        return prop.Zip(end).Select(t => (1 - a) * t.First + a * t.Second).ToList();
    }

    static HullSpec MerchantHullSpec(Design design)
    {
        double cb = design.BlockCoefficient;
        return new HullSpec(design.HullLength, design.HullBeam, new HullEnd { Taper = Layout.Clamp(0.42 - 0.3 * cb, 0.15, 0.3), Power = 2.0 },
            new HullEnd { Taper = 0.15, Transom = 0.3 });
    }

    public override string Name => "merchant";
    public override bool TakesSecondaryList => true;

    protected override IEnumerable<Limit> StyleLimits =>
    [
        new(["hull", "block_coefficient"], 0.55, 0.85), new(["speed_kn"], 6, 30), new(["cargo", "deadweight_t"], 0, 80000),
    ];

    public override TechInput DefaultTech { get; } = new()
    {
        Name = "Triple expansion, large-tube water-tube boilers, oil-fired (1940)", Fuel = "oil", WeightKgPerKw = 103.5,
        StressFloor = 0.45, SfcGPerKwh = 660, DensityTPerM3 = 0.28, UnitMaxMw = 12.0,
        Unit = new() { Mw = 5, HeightM = 7.5, WidthM = 4.5, LengthM = 9 }, BoilerFraction = 0.55, CrewK = 15.3, PartLoad = "REC",
        Draught = new() { System = "forced", VelocityMS = 14.0, ReachM = 20.0, GasTempK = 570, AirFuelRatio = 15 },
    };

    public override double DefaultBlockCoefficient => 0.72;
    public override SizeRules Sizing => base.Sizing with { GmFrac = 0.04, Tb = 0.46, LbMax = 8.0 };

    public override List<string> Validate(Design design)
    {
        var errs = base.Validate(design);
        errs.AddRange(CarrierStyle.GunsAreSecondaries(this, design));
        if (Cargo(design).Kind is not ("dry" or "tanker"))
            errs.Add($"cargo.kind = {Quote(Cargo(design).Kind)}: use dry or tanker");
        if (MachineryPosition(design) is not ("amidships" or "aft"))
            errs.Add($"machinery.position = {Quote(MachineryPosition(design))}: use amidships or aft");
        return errs;
    }

    public override Tuning Tuning(Design design) => new()
    {
        FreeboardA = 0.011, FreeboardB = 1.0, MiscFrac = 0.03, CruiseAtService = true, TbMax = 0.62, LcbFrac = 0.012, GmStiffFrac = 0.2,
    };

    public override Layout BuildLayout(Design design, Navarch.Result res, double shift = 0.0, double spread = 0.0) =>
        MerchantLayout(design, res, shift);

    public override List<Weight> RoughPayload(Design design, double D)
    {
        double L = design.HullLength;
        return [new Weight("Cargo gear", "superstructure", 0.01 * Math.Pow(L, 2), zRel: ZRel.Deck(3))];
    }

    public override (List<Weight> Std, List<Weight> Full) PayloadWeights(Design design, double L, double D, Geo geo, Tuning tun,
        Navarch.PayloadContext ctx)
    {
        var cg = Cargo(design);
        var holds = geo.Holds is { Count: > 0 } h ? h : [(-0.3 * L, 0.3 * L)];
        if (cg.DeadweightT == 0)
            return ([], []);
        var loads = Stow(cg.DeadweightT, holds, ctx);
        var full = new List<Weight>();
        for (int i = 0; i < holds.Count; i++)
            if (loads[i] > 0)
                full.Add(new Weight($"Cargo, {(cg.Kind == "tanker" ? "tank" : "hold")} {i + 1}", "cargo", loads[i],
                    (holds[i].X0 + holds[i].X1) / 2, ZRel.Frac(0.45)));
        return ([], full);
    }

    public override string CrewStandard => "H3";
    public override double CrewDeckK => 0.3;

    public override void AddResults(Results results, Design design, Layout lay, Navarch.Result r)
    {
        var cg = Cargo(design);
        results.CargoT = Math.Round(cg.DeadweightT);
        results.DeadweightT = (long)Math.Round(r.Full - r.Std);
        results.CargoKind = cg.Kind;
        results.Holds = lay.Geo.Holds?.Count ?? 0;
        results.MachineryPosition = MachineryPosition(design);
    }

    public override List<string> Summary(Design design, Layout lay, Navarch.Result r)
    {
        var cg = Cargo(design);
        return [$"cargo: {cg.DeadweightT:N0} t {cg.Kind} in {lay.Geo.Holds?.Count ?? 0} " +
                $"{(cg.Kind == "tanker" ? "tanks" : "holds")}   deadweight {r.Full - r.Std:N0} t   " +
                $"machinery {MachineryPosition(design)}"];
    }

    static Layout MerchantLayout(Design design, Navarch.Result res, double shift)
    {
        double depth = res.Depth;
        var lay = new Layout(design);
        var hs = MerchantHullSpec(design);
        var hull = new Hull(hs);
        lay.Hull = hull;
        lay.Topside = Topside.Of(design, res.Freeboard);
        double L = hull.L, B = hull.B;
        var cg = Cargo(design);
        bool tanker = cg.Kind == "tanker";
        bool aftEngines = MachineryPosition(design) == "aft";
        lay.ShiftRange = (-0.05 * L, 0.05 * L);
        shift = Layout.Clamp(shift, lay.ShiftRange.Lo, lay.ShiftRange.Hi);
        lay.Geo.Shift = shift;
        double lMach = Layout.PlanMachinery(lay, design, res, hull, aftEngines ? -0.38 * L : 0.0);

        double fcLen = Layout.Clamp(0.09 * L, 6, 22);
        double poopLen = aftEngines ? Math.Max(0.08 * L, lMach + 0.03 * L) : 0.08 * L;
        double bdLen, bxc;
        if (aftEngines)
        {
            bdLen = Layout.Clamp(0.10 * L, 10, 24);
            bxc = 0.08 * L + shift;
        }
        else
        {
            bdLen = Math.Max(Layout.Clamp(0.16 * L, 12, 40), lMach + 6);
            bxc = -0.02 * L + shift;
        }
        var islands = new[] { ("Forecastle", L / 2 - fcLen, L / 2), ("Bridge deck", bxc - bdLen / 2, bxc + bdLen / 2), ("Poop", -L / 2, -L / 2 + poopLen) };
        foreach (var (name, x0, x1) in islands)
            Layout.AddRaised(lay, design, name, x0, x1, (long)Math.Round(RaisedH / Layout.LevelH));
        double DeckH(double x) => lay.DeckZ(x);

        var blocks = new List<Block>();
        var funnels = new List<Funnel>();
        var boats = new List<Boat>();
        double bx0 = bxc - bdLen / 2, bx1 = bxc + bdLen / 2;
        double hwMid = lay.DeckHalfWidth(bxc);
        double wh = Math.Min(0.62 * B, 2 * (hwMid - 1.5));
        var houseIds = new List<string>();

        Block House(string bid, double x0, double x1, double w, long level, double rf, double rb, double z0 = RaisedH, string role = "deckhouse")
        {
            houseIds.Add(bid);
            return Layout.AddBlock(lay, blocks, bid, x0, x1, w, level, rf, rb, z0: z0, role: role);
        }

        House("House", bx0 + 0.08 * bdLen, bx1 - 0.06 * bdLen, wh, 1, 1.2, 1.0);
        if (!aftEngines)
            House("Boat deck house", bx0 + 0.25 * bdLen, bx1 - 0.08 * bdLen, 0.5 * B, 2, 1.0, 0.8);
        House("Bridge", bx1 - 0.3 * bdLen, bx1 - 0.08 * bdLen, Math.Min(0.92 * B, 2 * (hwMid - 0.4)), !aftEngines ? 3 : 2, 0.6, 0.6, role: "bridge");
        double funTop = RaisedH + Layout.LevelH * 3 + 2.0;
        double fx, mx, boatX, boatY;
        if (aftEngines)
        {
            double ex0 = -L / 2 + 0.03 * L, ex1 = -L / 2 + poopLen - 1.0;
            House("Engine house", ex0, ex1, Math.Min(0.7 * B, 2 * (lay.DeckHalfWidth((ex0 + ex1) / 2) - 1.5)), 1, 1.0, 1.0);
            House("Engine house upper", ex0 + 0.15 * (ex1 - ex0), ex1 - 0.1 * (ex1 - ex0), 0.45 * B, 2, 0.8, 0.8);
            fx = (ex0 + ex1) / 2 - 0.1 * (ex1 - ex0);
            mx = (-L / 2 + 0.02 * L + -L / 2 + poopLen) / 2;
            (boatX, boatY) = (ex0 + 0.35 * (ex1 - ex0), 0.35 * B);
            funTop = RaisedH + Layout.LevelH * 2 + 3.0;
        }
        else
        {
            fx = bx0 + 0.3 * bdLen;
            mx = bx0 + lMach / 2 + 1.0;
            (boatX, boatY) = (fx, 0.31 * B);
        }
        var (nfun, fw, fl) = Layout.PlanFunnels(lay, design, res, B, funTop);
        fw = Math.Min(fw, 0.4 * B);
        for (int i = 0; i < nfun; i++)
        {
            double x = aftEngines ? fx - i * (fl + 1.5) : fx + (i - (nfun - 1) / 2.0) * (fl + 1.5);
            funnels.Add(new Funnel { Id = $"Funnel {i + 1}", X = x, Y = 0.0, L = fl, W = fw, Pipes = 1, Z0 = RaisedH, Seg = Layout.FunnelSeg(lay, i) });
            lay.Occupy(Footprint.Rect(x - fl / 2, -fw / 2, x + fl / 2, fw / 2), RaisedH, funTop, $"Funnel {i + 1}");
            Layout.AddFunnelWeights(lay, funnels[^1], funTop, mx, depth);
        }
        double bl_ = Layout.Clamp(0.045 * L, 5, 9);
        foreach (var dx in L >= 110 ? new[] { -0.6 * bl_, 0.6 * bl_ } : [0.0])
            foreach (int s in new[] { 1, -1 })
            {
                double x = dx < 0 ? boatX + dx - 0.5 * fl - 1.0 : boatX + dx + 0.5 * fl + 1.0;
                boats.Add(new Boat(x, s * boatY, bl_, 0.3 * bl_, RaisedH + Layout.LevelH + 1.5));
                lay.Occupy(Footprint.Rect(x - bl_ / 2, s * boatY - 0.15 * bl_, x + bl_ / 2, s * boatY + 0.15 * bl_), RaisedH + Layout.LevelH,
                    RaisedH + Layout.LevelH + 1.5, $"Boat{boats.Count}");
            }
        lay.Geo.Machinery = (mx - lMach / 2, mx + lMach / 2);
        lay.Geo.MachineryX = mx;

        var mounts = new List<Mount>();
        var turretTypes = new OrderedDictionary<string, TurretType>(StringComparer.Ordinal);
        var xs = CarrierStyle.Vdc(64).Select(v => -L / 2 + 0.06 * L + v * 0.88 * L).ToList();
        Armament.PlaceBatteries(lay, mounts, turretTypes, design,
            [new Armament.EndLine(-L / 2 + 0.025 * L, +1, 0.0, 180, x => DeckH(x) + 0.3, houseIds.ToList()),
             new Armament.EndLine(L / 2 - 0.035 * L, -1, 0.0, 0, x => DeckH(x) + 0.3, [])],
            t => xs.Select(x => new Slot(x, lay.DeckHalfWidth(x) - Armament.BodyReach(t) - 0.6, DeckH(x) + 0.3)), depth);

        var zones = new[] { (bx1 + 1.0, L / 2 - fcLen - 1.0, +1), (-L / 2 + poopLen + 1.0, bx0 - 1.0, -1) };
        double pitch = !tanker ? Layout.Clamp(0.12 * L, 10, 22) : Layout.Clamp(0.09 * L, 8, 16);
        var holds = new List<(double X0, double X1)>();
        var hatches = new List<Hatch>();
        var masts = new List<Mast>();
        var fittings = new List<Fitting>();
        double mastTop = Layout.Clamp(0.12 * L + 4, 10, 24);
        foreach (var (z0, z1, outward) in zones)
        {
            double zl = z1 - z0;
            if (zl < 6)
                continue;
            long n = Math.Max(1L, (long)Math.Round(zl / pitch));
            double hl = zl / n;
            var zone = Enumerable.Range(0, (int)n).Select(i => (X0: z0 + i * hl, X1: z0 + (i + 1) * hl)).ToList();
            if (outward < 0)
                zone.Reverse();
            holds.AddRange(zone);
            if (tanker)
            {
                foreach (var (h0, h1) in zone)
                    foreach (int s in new[] { 1, -1 })
                        fittings.Add(new Fitting((h0 + h1) / 2, s * 0.22 * B, 1.4, 1.4, "hatch_coaming"));
                continue;
            }
            double hatchW = 0.42 * B;
            var zoneHatches = new List<Hatch?>();
            foreach (var (h0, h1) in zone)
            {
                double hx = (h0 + h1) / 2, hl_ = 0.55 * (h1 - h0);
                while (hl_ > 0.25 * (h1 - h0) && !lay.Free(Footprint.Rect(hx - hl_ / 2, -hatchW / 2, hx + hl_ / 2, hatchW / 2), 0.4))
                    hl_ -= 0.5;
                if (hl_ <= 0.25 * (h1 - h0))
                {
                    zoneHatches.Add(null);
                    continue;
                }
                var ht = new Hatch(hx, 0.0, hl_, hatchW);
                hatches.Add(ht);
                zoneHatches.Add(ht);
                lay.Occupy(Footprint.Rect(hx - hl_ / 2, -hatchW / 2, hx + hl_ / 2, hatchW / 2), 0, 1.2, $"Hatch {hatches.Count}");
            }
            int k = 0;
            while (k < n)
            {
                double mxx;
                List<Hatch?> served;
                if (k + 1 < n)
                {
                    mxx = outward > 0 ? zone[k].X1 : zone[k].X0;
                    served = zoneHatches.Skip(k).Take(2).ToList();
                }
                else
                {
                    mxx = outward > 0 ? zone[k].X1 - 0.5 : zone[k].X0 + 0.5;
                    served = zoneHatches.Skip(k).Take(1).ToList();
                }
                double? spot = null;
                foreach (var d in new[] { 0, 1, -1, 2, -2, 3, -3 })
                    if (lay.Free(Footprint.Circle(mxx + d, 0.0, 0.9), 0.3))
                    {
                        spot = mxx + d;
                        break;
                    }
                if (spot is null)
                {
                    k += 2;
                    continue;
                }
                mxx = spot.Value;
                var booms = served.OfType<Hatch>().SelectMany(h => new[] { 1, -1 }.Select(s => new[] { h.X, s * 0.18 * B })).ToList();
                masts.Add(new Mast { Id = $"Mast {masts.Count + 1}", X = mxx, Y = 0.0, Yard = Math.Min(0.25 * B, 6), Tripod = false, Booms = booms, Top = mastTop });
                lay.Occupy(Footprint.Circle(mxx, 0.0, 0.9), 0, mastTop, $"Mast {masts.Count}");
                lay.Weights.Add(new Weight($"Mast {masts.Count}", "superstructure", 8 + 0.2 * L, mxx, ZRel.Deck(mastTop / 3)));
                k += 2;
            }
        }
        if (tanker)
        {
            masts.Add(new Mast { Id = "Foremast", X = L / 2 - fcLen - 0.5, Y = 0.0, Yard = Math.Min(0.3 * B, 7), Tripod = false, Top = mastTop });
            foreach (var (x0, x1) in new[] { (-L / 2 + poopLen, bx0), (bx1, L / 2 - fcLen) })
                fittings.Add(new Fitting((x0 + x1) / 2, 0.0, x1 - x0, 1.2, "fitting", 0.2));
        }
        if (holds.Count == 0)
            lay.Fail("length", "No room for cargo: the forecastle, midships house and poop fill the hull.");
        else if (cg.DeadweightT != 0)
        {
            double vol = HoldVolume(hull, holds, depth);
            double need = cg.DeadweightT * Stowage[cg.Kind];
            if (vol < need)
                lay.Fail("length", $"The {(tanker ? "tanks" : "holds")} take about {vol:N0} m3, but " +
                                   $"{cg.DeadweightT:N0} t of cargo needs about {need:N0} m3. Carry less cargo.");
        }
        double hatchT = hatches.Sum(h => h.L * h.W) * 0.12;
        if (hatchT != 0)
            lay.Weights.Add(new Weight("Hatch covers", "superstructure", hatchT, 0.0, ZRel.Deck(1.0)));
        lay.Geo.Holds = holds;

        if (design.Torpedoes is { Mounts: > 0 } tp)
        {
            var (ttId, tt) = Armament.TorpedoType(tp);
            double r = tt.BarrelLen / 2 + 0.3;
            Armament.SidePairs(lay, mounts, turretTypes, "torpedo", ttId, tt, (tp.Mounts.Value + 1) / 2,
                xs.Select(x => new Slot(x, lay.DeckHalfWidth(x) - r - 0.4, DeckH(x) + 0.3)), "T", label: "Torpedo");
        }
        FireControl.Place(lay, design, blocks);
        var aaOut = new List<AaMount>();
        foreach (var (kind, count) in new[] { ("quad40", design.Aa?.Heavy ?? 0), ("single20", design.Aa?.Light ?? 0) })
        {
            double rr = Geometry.AaCfg[kind].R;
            double roof = RaisedH + Layout.LevelH;
            var cands = new List<Slot>
            {
                new(bx1 - 0.12 * bdLen, Math.Min(0.46 * B, hwMid - 0.4) - rr - 0.2, RaisedH + Layout.LevelH * 2),
                new(bx0 + 0.1 * bdLen, wh / 2 - rr - 0.3, roof),
            };
            cands.AddRange(xs.Select(x => new Slot(x, lay.DeckHalfWidth(x) - rr - 0.6, DeckH(x))));
            var ign = houseIds.ToList();
            Armament.PlaceAa(lay, aaOut, kind, count, cands, ignore: _ => ign);
        }

        double innerHw = 0.85 * B / 2;
        var (m0, m1) = lay.Geo.Machinery!.Value;
        for (int i = 0; i < holds.Count; i++)
            lay.Compartments.Add(new Compartment
            {
                Id = $"{(tanker ? "Tank" : "Hold")} {i + 1}", Kind = tanker ? "cargo_tank" : "hold", X0 = holds[i].X0, X1 = holds[i].X1,
                HalfWidth = innerHw,
            });
        Layout.AddMachineryRooms(lay, Layout.StackMachinery(Layout.PlanSegments(lay.Geo.Plant!), m1), innerHw, depth);
        var steer = Layout.AddSteering(lay);
        var guns = Ordnance.Guns(mounts);
        if (guns.Count > 0)
        {
            double mx0 = steer.X1;
            double mw = Math.Min(innerHw, 0.5 * B / 2);
            Ordnance.Stow(lay, mounts, [new Zone(mx0, mx0 + Ordnance.ZoneLength(Ordnance.BookedM3(lay, guns), 2 * mw, lay.Geo.Plant!), mw,
                [new ZoneRoom("Gun magazine", guns)])]);
        }
        Layout.SetCitadel(lay, m0, m1);
        Layout.FinishLayout(lay, design, hs, mounts, turretTypes, blocks, funnels, masts, aaOut, funTop);
        var spec = lay.Dressing;
        (spec.Boats, spec.Hatches, spec.Fittings) = (boats, hatches, fittings);
        (spec.Bollards, spec.ChainX, spec.HawseBack) = ([L / 2 - 0.04 * L, -L / 2 + 0.04 * L], L / 2 - 0.05 * L, 0.025 * L + 1.0);
        return lay;
    }
}
