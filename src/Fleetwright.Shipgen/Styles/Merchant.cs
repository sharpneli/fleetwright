namespace Fleetwright.Shipgen;

/// <summary>merchant: cargo ships and tankers (a "three-island" ship).</summary>
public sealed class MerchantStyle : Style
{
    const double RAISED_H = Layout.LEVEL_H;
    static readonly Dictionary<string, double> STOWAGE = new() { ["dry"] = 1.4, ["tanker"] = 1.25 };
    const double DOUBLE_BOTTOM = 1.2;

    /// <summary>Volume of the holds below the main deck.</summary>
    static double HoldVolume(Hull hull, List<(double H0, double H1)> holds, double depth)
    {
        double area = holds.Select(h => (h.H1 - h.H0) / 8 *
            Enumerable.Range(0, 8).Select(k => 2 * hull.HalfWidth(h.H0 + (h.H1 - h.H0) * (k + 0.5) / 8)).Sum()).Sum();
        return 0.9 * area * Math.Max(0.0, depth - DOUBLE_BOTTOM);
    }

    static PyDict Cargo(PyDict design) => PyDict.Merge(PyDict.Of(("kind", "dry"), ("deadweight_t", 0.0)), design.Or("cargo", null) as PyDict);

    static PyDict Machinery(PyDict design) => PyDict.Merge(PyDict.Of(("position", "amidships")), design.Or("machinery", null) as PyDict);

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
            var out_ = Enumerable.Repeat(0.0, holds.Count).ToList();
            double left = C;
            foreach (int i in order)
            {
                out_[i] = Math.Min(caps[i], left);
                left -= out_[i];
            }
            return out_;
        }

        double Centre(List<double> ws) => ws.Zip(xs).Select(t => t.First * t.Second).Sum() / C;
        double xP = Centre(prop);
        var end = Packed(Enumerable.Range(0, holds.Count).OrderBy(i => xc > xP ? -xs[i] : xs[i]).ToList());
        double xE = Centre(end);
        double a = Math.Abs(xE - xP) < 1e-9 ? 0.0 : Math.Max(0.0, Math.Min(1.0, (xc - xP) / (xE - xP)));
        return prop.Zip(end).Select(t => (1 - a) * t.First + a * t.Second).ToList();
    }

    static PyDict MerchantHullSpec(PyDict design)
    {
        var h = design.D("hull");
        double cb = h.F("block_coefficient");
        return PyDict.Of(("length", h["length"]), ("beam", h["beam"]),
            ("bow", PyDict.Of(("taper", Layout.Clamp(0.42 - 0.3 * cb, 0.15, 0.3)), ("power", 2.0))),
            ("stern", PyDict.Of(("taper", 0.15), ("transom", 0.3))));
    }

    public override string Name => "merchant";
    public override bool SECONDARY_LIST => true;

    protected override IEnumerable<Limit> StyleLimits =>
    [
        new(["hull", "block_coefficient"], 0.55, 0.85), new(["speed_kn"], 6L, 30L), new(["cargo", "deadweight_t"], 0L, 80000L),
    ];

    public override PyDict? DefaultTech => PyDict.Of(("name", "Triple expansion, large-tube water-tube boilers, oil-fired (1940)"),
        ("fuel", "oil"), ("weight_kg_per_kw", 103.5), ("stress_floor", 0.45), ("sfc_g_per_kwh", 660L), ("density_t_per_m3", 0.28),
        ("unit_max_mw", 12.0), ("unit", PyDict.Of(("mw", 5L), ("height_m", 7.5), ("width_m", 4.5), ("length_m", 9L))),
        ("boiler_fraction", 0.55), ("crew_k", 15.3), ("part_load", "REC"),
        ("draught", PyDict.Of(("system", "forced"), ("velocity_m_s", 14.0), ("reach_m", 20.0), ("gas_temp_k", 570L), ("air_fuel_ratio", 15L))));

    public override double DEFAULT_CB => 0.72;
    public override SizeRules SIZE => base.SIZE with { GmFrac = 0.04, Tb = 0.46, LbMax = 8.0 };

    public override List<string> Validate(PyDict design)
    {
        var errs = base.Validate(design);
        errs.AddRange(CarrierStyle.GunsAreSecondaries(this, design));
        if (!Py.In(Cargo(design)["kind"], "dry", "tanker"))
            errs.Add($"cargo.kind = {Py.Repr(Cargo(design)["kind"])}: use dry or tanker");
        var m = Machinery(design);
        if (!Py.In(m["position"], "amidships", "aft"))
            errs.Add($"machinery.position = {Py.Repr(m["position"])}: use amidships or aft");
        return errs;
    }

    public override PyDict Tuning(PyDict design) => PyDict.Merge(base.Tuning(design), PyDict.Of(("freeboard_a", 0.011), ("freeboard_b", 1.0),
        ("misc_frac", 0.03), ("cruise_at_service", true), ("tb_max", 0.62), ("lcb_frac", 0.012), ("gm_stiff_frac", 0.2)));

    public override Layout BuildLayout(PyDict design, Navarch.Result res, double shift = 0.0, double spread = 0.0) =>
        MerchantLayout(design, res, shift);

    public override List<Weight> RoughPayload(PyDict design, double D)
    {
        double L = design.D("hull").F("length");
        return [new Weight("Cargo gear", "superstructure", 0.01 * Math.Pow(L, 2), zRel: ZRel.Deck(3))];
    }

    public override (List<Weight> Std, List<Weight> Full) PayloadWeights(PyDict design, double L, double D, Geo geo, PyDict tun,
        Navarch.PayloadContext ctx)
    {
        var cg = Cargo(design);
        var holds = geo.Holds is { Count: > 0 } h ? h : [(-0.3 * L, 0.3 * L)];
        if (!Py.Truthy(cg["deadweight_t"]))
            return ([], []);
        var loads = Stow(cg.F("deadweight_t"), holds, ctx);
        var full = new List<Weight>();
        for (int i = 0; i < holds.Count; i++)
            if (loads[i] > 0)
                full.Add(new Weight($"Cargo, {(Py.Eq(cg["kind"], "tanker") ? "tank" : "hold")} {i + 1}", "cargo", loads[i],
                    (holds[i].X0 + holds[i].X1) / 2, ZRel.Frac(0.45)));
        return ([], full);
    }

    public override string CREW_STANDARD => "H3";
    public override double CREW_DECK_K => 0.3;

    public override PyDict Results(PyDict design, Layout lay, Navarch.Result r)
    {
        var cg = Cargo(design);
        var m = Machinery(design);
        return PyDict.Of(("cargo_t", Py.RoundObj(cg["deadweight_t"])), ("deadweight_t", (long)Math.Round(r.Full - r.Std)), ("cargo_kind", cg["kind"]),
            ("holds", (long)(lay.Geo.Holds?.Count ?? 0)), ("machinery_position", m["position"]));
    }

    public override List<object?> Summary(PyDict design, Layout lay, Navarch.Result r)
    {
        var cg = Cargo(design);
        var m = Machinery(design);
        return [$"cargo: {cg["deadweight_t"]:N0} t {Py.Str(cg["kind"])} in {lay.Geo.Holds?.Count ?? 0} " +
                $"{(Py.Eq(cg["kind"], "tanker") ? "tanks" : "holds")}   deadweight {r.Full - r.Std:N0} t   " +
                $"machinery {Py.Str(m["position"])}"];
    }

    static Layout MerchantLayout(PyDict design, Navarch.Result res, double shift)
    {
        double depth = res.Depth;
        var lay = new Layout(design);
        var hs = MerchantHullSpec(design);
        var hull = new Hull(hs);
        lay.Hull = hull;
        double L = hull.L, B = hull.B;
        var cg = Cargo(design);
        var mach = Machinery(design);
        bool tanker = Py.Eq(cg["kind"], "tanker");
        bool aftEngines = Py.Eq(mach["position"], "aft");
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
            Layout.AddRaised(lay, design, name, x0, x1, (long)Math.Round(RAISED_H / Layout.LEVEL_H));
        double DeckH(double x) => lay.DeckZ(x);

        var blocks = new List<PyDict>();
        var funnels = new List<PyDict>();
        var boats = new List<object?>();
        double bx0 = bxc - bdLen / 2, bx1 = bxc + bdLen / 2;
        double hwMid = hull.HalfWidth(bxc);
        double wh = Math.Min(0.62 * B, 2 * (hwMid - 1.5));
        var houseIds = new List<string>();

        PyDict House(string bid, double x0, double x1, double w, long level, double rf, double rb, double z0 = RAISED_H, string role = "deckhouse")
        {
            houseIds.Add(bid);
            return Layout.AddBlock(lay, blocks, bid, x0, x1, w, level, rf, rb, z0: z0, role: role);
        }

        House("House", bx0 + 0.08 * bdLen, bx1 - 0.06 * bdLen, wh, 1, 1.2, 1.0);
        if (!aftEngines)
            House("Boat deck house", bx0 + 0.25 * bdLen, bx1 - 0.08 * bdLen, 0.5 * B, 2, 1.0, 0.8);
        House("Bridge", bx1 - 0.3 * bdLen, bx1 - 0.08 * bdLen, Math.Min(0.92 * B, 2 * (hwMid - 0.4)), !aftEngines ? 3 : 2, 0.6, 0.6, role: "bridge");
        double funTop = RAISED_H + Layout.LEVEL_H * 3 + 2.0;
        double fx, mx, boatX, boatY;
        if (aftEngines)
        {
            double ex0 = -L / 2 + 0.03 * L, ex1 = -L / 2 + poopLen - 1.0;
            House("Engine house", ex0, ex1, Math.Min(0.7 * B, 2 * (hull.HalfWidth((ex0 + ex1) / 2) - 1.5)), 1, 1.0, 1.0);
            House("Engine house upper", ex0 + 0.15 * (ex1 - ex0), ex1 - 0.1 * (ex1 - ex0), 0.45 * B, 2, 0.8, 0.8);
            fx = (ex0 + ex1) / 2 - 0.1 * (ex1 - ex0);
            mx = (-L / 2 + 0.02 * L + -L / 2 + poopLen) / 2;
            (boatX, boatY) = (ex0 + 0.35 * (ex1 - ex0), 0.35 * B);
            funTop = RAISED_H + Layout.LEVEL_H * 2 + 3.0;
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
            funnels.Add(PyDict.Of(("id", $"Funnel {i + 1}"), ("x", x), ("y", 0.0), ("l", fl), ("w", fw), ("pipes", 1L), ("z0", RAISED_H),
                ("seg", Layout.FunnelSeg(lay, i))));
            lay.Occupy(Footprint.Rect(x - fl / 2, -fw / 2, x + fl / 2, fw / 2), RAISED_H, funTop, $"Funnel {i + 1}");
            Layout.AddFunnelWeights(lay, funnels[^1], funTop, mx, depth);
        }
        double bl_ = Layout.Clamp(0.045 * L, 5, 9);
        foreach (var dx in L >= 110 ? new[] { -0.6 * bl_, 0.6 * bl_ } : [0.0])
            foreach (int s in new[] { 1, -1 })
            {
                double x = dx < 0 ? boatX + dx - 0.5 * fl - 1.0 : boatX + dx + 0.5 * fl + 1.0;
                boats.Add(PyDict.Of(("x", x), ("y", s * boatY), ("l", bl_), ("w", 0.3 * bl_), ("top", RAISED_H + Layout.LEVEL_H + 1.5)));
                lay.Occupy(Footprint.Rect(x - bl_ / 2, s * boatY - 0.15 * bl_, x + bl_ / 2, s * boatY + 0.15 * bl_), RAISED_H + Layout.LEVEL_H,
                    RAISED_H + Layout.LEVEL_H + 1.5, $"Boat{boats.Count}");
            }
        lay.Geo.Machinery = (mx - lMach / 2, mx + lMach / 2);
        lay.Geo.MachineryX = mx;

        var mounts = new List<PyDict>();
        var turretTypes = new PyDict();
        var xs = CarrierStyle.Vdc(64).Select(v => -L / 2 + 0.06 * L + v * 0.88 * L).ToList();
        Armament.PlaceBatteries(lay, mounts, turretTypes, design,
            [new Armament.EndLine(-L / 2 + 0.025 * L, +1, 0.0, 180, x => DeckH(x) + 0.3, houseIds.ToList()),
             new Armament.EndLine(L / 2 - 0.035 * L, -1, 0.0, 0, x => DeckH(x) + 0.3, [])],
            t => xs.Select(x => new object?[] { x, hull.HalfWidth(x) - Armament.BodyReach(t) - 0.6, DeckH(x) + 0.3 }), depth);

        var zones = new[] { (bx1 + 1.0, L / 2 - fcLen - 1.0, +1), (-L / 2 + poopLen + 1.0, bx0 - 1.0, -1) };
        double pitch = !tanker ? Layout.Clamp(0.12 * L, 10, 22) : Layout.Clamp(0.09 * L, 8, 16);
        var holds = new List<(double X0, double X1)>();
        var hatches = new List<PyDict>();
        var masts = new List<PyDict>();
        var fittings = new List<object?>();
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
                        fittings.Add(PyDict.Of(("x", (h0 + h1) / 2), ("y", s * 0.22 * B), ("l", 1.4), ("w", 1.4), ("color", "hatch_coaming")));
                continue;
            }
            double hatchW = 0.42 * B;
            var zoneHatches = new List<PyDict?>();
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
                var ht = PyDict.Of(("x", hx), ("y", 0.0), ("l", hl_), ("w", hatchW));
                hatches.Add(ht);
                zoneHatches.Add(ht);
                lay.Occupy(Footprint.Rect(hx - hl_ / 2, -hatchW / 2, hx + hl_ / 2, hatchW / 2), 0, 1.2, $"Hatch {hatches.Count}");
            }
            int k = 0;
            while (k < n)
            {
                double mxx;
                List<PyDict?> served;
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
                var booms = served.Where(h => h != null).SelectMany(h => new[] { 1, -1 }.Select(s => (object?)new object?[] { h!["x"], s * 0.18 * B }))
                    .ToList();
                masts.Add(PyDict.Of(("x", mxx), ("y", 0.0), ("yard", Math.Min(0.25 * B, 6)), ("tripod", false), ("booms", booms), ("top", mastTop)));
                lay.Occupy(Footprint.Circle(mxx, 0.0, 0.9), 0, mastTop, $"Mast {masts.Count}");
                lay.Weights.Add(new Weight($"Mast {masts.Count}", "superstructure", 8 + 0.2 * L, mxx, ZRel.Deck(mastTop / 3)));
                k += 2;
            }
        }
        if (tanker)
        {
            masts.Add(PyDict.Of(("x", L / 2 - fcLen - 0.5), ("y", 0.0), ("yard", Math.Min(0.3 * B, 7)), ("tripod", false), ("top", mastTop)));
            foreach (var (x0, x1) in new[] { (-L / 2 + poopLen, bx0), (bx1, L / 2 - fcLen) })
                fittings.Add(PyDict.Of(("x", (x0 + x1) / 2), ("y", 0.0), ("l", x1 - x0), ("w", 1.2), ("color", "fitting"), ("r", 0.2)));
        }
        if (holds.Count == 0)
            lay.Fail("length", "No room for cargo: the forecastle, midships house and poop fill the hull.");
        else if (Py.Truthy(cg["deadweight_t"]))
        {
            double vol = HoldVolume(hull, holds, depth);
            double need = cg.F("deadweight_t") * STOWAGE[cg.S("kind")];
            if (vol < need)
                lay.Fail("length", $"The {(tanker ? "tanks" : "holds")} take about {vol:N0} m3, but " +
                                   $"{cg["deadweight_t"]:N0} t of cargo needs about {need:N0} m3. Carry less cargo.");
        }
        double hatchT = hatches.Select(h => h.F("l") * h.F("w")).Sum() * 0.12;
        if (hatchT != 0)
            lay.Weights.Add(new Weight("Hatch covers", "superstructure", hatchT, 0.0, ZRel.Deck(1.0)));
        lay.Geo.Holds = holds;

        var tp = design.Or("torpedoes", null) as PyDict ?? new PyDict();
        if (Py.Truthy(tp.Get("mounts")))
        {
            var (ttId, tt) = Armament.TorpedoType(tp);
            double r = tt.F("barrel_len") / 2 + 0.3;
            Armament.SidePairs(lay, mounts, turretTypes, "torpedo", ttId, tt, (Py.ToLong(tp["mounts"]) + 1) / 2,
                xs.Select(x => new object?[] { x, hull.HalfWidth(x) - r - 0.4, DeckH(x) + 0.3 }), "T", label: "Torpedo");
        }
        FireControl.Place(lay, design, blocks);
        var aaOut = new List<PyDict>();
        var aaReq = design.Or("aa", null) as PyDict ?? new PyDict();
        foreach (var (kind, count) in new[] { ("quad40", aaReq.Get("heavy", 0L)), ("single20", aaReq.Get("light", 0L)) })
        {
            double rr = Geometry.AA_CFG[kind].R;
            double roof = RAISED_H + Layout.LEVEL_H;
            var cands = new List<object?[]>
            {
                new object?[] { bx1 - 0.12 * bdLen, Math.Min(0.46 * B, hwMid - 0.4) - rr - 0.2, RAISED_H + Layout.LEVEL_H * 2 },
                new object?[] { bx0 + 0.1 * bdLen, wh / 2 - rr - 0.3, roof },
            };
            cands.AddRange(xs.Select(x => new object?[] { x, hull.HalfWidth(x) - rr - 0.6, DeckH(x) }));
            var ign = houseIds.ToList();
            Armament.PlaceAa(lay, aaOut, kind, Py.ToLong(count), cands, ignore: _ => ign);
        }

        double innerHw = 0.85 * B / 2;
        var (m0, m1) = lay.Geo.Machinery!.Value;
        for (int i = 0; i < holds.Count; i++)
            lay.Compartments.Add(PyDict.Of(("id", $"{(tanker ? "Tank" : "Hold")} {i + 1}"), ("kind", tanker ? "cargo_tank" : "hold"),
                ("x0", holds[i].X0), ("x1", holds[i].X1), ("half_width", innerHw)));
        Layout.AddMachineryRooms(lay, Layout.StackMachinery(Layout.PlanSegments(lay.Geo.Plant!), m1), innerHw, depth);
        var steer = Layout.AddSteering(lay);
        var guns = Ordnance.Guns(mounts);
        if (guns.Count > 0)
        {
            double mx0 = steer.F("x1");
            double mw = Math.Min(innerHw, 0.5 * B / 2);
            Ordnance.Stow(lay, mounts, [PyDict.Of(("x0", mx0), ("x1", mx0 + Ordnance.ZoneLength(Ordnance.BookedM3(lay, guns), 2 * mw, lay.Geo.Plant!)),
                ("half_width", mw), ("rooms", new List<object?> { PyDict.Of(("id", "Gun magazine"), ("mounts", guns.Cast<object?>().ToList())) }))]);
        }
        Layout.SetCitadel(lay, m0, m1);
        return Layout.FinishLayout(lay, design, hs, mounts, turretTypes, blocks, funnels, masts, aaOut, funTop, null,
            ("boats", boats), ("hatches", hatches.Cast<object?>().ToList()), ("fittings", fittings),
            ("bollards", new List<object?> { L / 2 - 0.04 * L, -L / 2 + 0.04 * L }), ("chain_x", L / 2 - 0.05 * L), ("hawse_back", 0.025 * L + 1.0));
    }
}
