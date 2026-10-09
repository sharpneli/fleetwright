namespace Fleetwright.Shipgen;

/// <summary>warship: displacement-hull armoured warships, destroyer to battleship. The layout is layout.py.</summary>
public sealed class WarshipStyle : Style
{
    public override string Name => "warship";
    public override bool MAIN_LIST => true;
    public override bool MIDSHIPS_TURRETS => true;
    public override bool WING_TURRETS => true;
    public override bool SECONDARY_LIST => true;
    public override bool CASEMATES => true;
    public override long MIN_TOWER => 2;
    public override bool DECKHOUSE_LEVELS => true;
    public override bool CONTROL_TOWERS => true;
    public override bool RAISED_MOUNTS => true;
    public override bool RAISED_HULL => true;

    public override Layout BuildLayout(PyDict design, Navarch.Result res, double shift = 0.0, double spread = 0.0) =>
        Layout.BuildLayout(design, res, shift, spread);
}

/// <summary>planing: planing-hull fast craft (motor torpedo boats, PT boats, motor gunboats).</summary>
public sealed class PlaningStyle : Style
{
    const double WOOD_T_PER_M2 = 0.10, PLANK_MM = 25.0;

    public override string Name => "planing";

    public override PyDict? DefaultTech => PyDict.Of(("name", "Petrol engines (1940)"), ("fuel", "petrol"), ("weight_kg_per_kw", 6.5),
        ("stress_floor", 0.7), ("sfc_g_per_kwh", 291L), ("density_t_per_m3", 0.4), ("unit_max_mw", 1.1),
        ("unit", PyDict.Of(("mw", 1L), ("height_m", 1.2), ("width_m", 1.1), ("length_m", 2.5))), ("boiler_fraction", 0.0),
        ("crew_k", 3L), ("part_load", "DSL"),
        ("draught", PyDict.Of(("system", "exhaust"), ("velocity_m_s", 35L), ("reach_m", 60L), ("gas_temp_k", 620L), ("air_fuel_ratio", 38L))));

    public override double DEFAULT_CB => 0.45;
    public override SizeRules SIZE => new((8.0, 60.0), 12.0, 0.06, 1.0, 4.5, false);

    protected override IEnumerable<Limit> StyleLimits =>
    [
        new(["hull", "block_coefficient"], 0.35, 0.6), new(["speed_kn"], 15L, 60L), new(["range_nm"], 100L, 3000L),
    ];

    public override PyDict Tuning(PyDict design) => PyDict.Merge(base.Tuning(design), PyDict.Of(("power_model", "planing"),
        ("hull_model", "box"), ("hull_k", 0.06), ("plate_own_mm", PLANK_MM), ("freeboard_a", 0.04), ("freeboard_b", 0.8),
        ("misc_frac", 0.07), ("cruise_kn", 25.0), ("lcb_frac", -0.11), ("gm_stiff_frac", 0.5), ("fn_warn", 99.0), ("lb_warn", 2.8),
        ("trim_tol_frac", 0.025), ("trim_warn_frac", 0.01)));

    public override Layout BuildLayout(PyDict design, Navarch.Result res, double shift = 0.0, double spread = 0.0) =>
        PlaningLayout(design, res, shift);

    public override List<string> Checks(PyDict design, Navarch.Result r, PyDict tun)
    {
        double fnv = Navarch.VolumetricFroude(r.Full, design.F("speed_kn"));
        if (fnv < 2.0)
            return [$"Not fully planing at {Py.Str(design["speed_kn"])} kn (Fn∇ {Py.F(fnv, 1)}, want 2+): the hull is too heavy " +
                    "or too slow to rise onto the plane, so the power is spent pushing water."];
        return [];
    }

    public override string CREW_STANDARD => "H0";

    public override PyDict Results(PyDict design, Layout lay, Navarch.Result r) =>
        PyDict.Of(("volumetric_froude", Py.Round(Navarch.VolumetricFroude(r.Full, design.F("speed_kn")), 2)),
            ("power_to_weight_hp_per_t", Py.Round(r.PowerShp / r.Full, 1)), ("power_model", "planing placeholder"));

    public override List<object?> Summary(PyDict design, Layout lay, Navarch.Result r) =>
        [$"planing: Fn∇ {Py.F(Navarch.VolumetricFroude(r.Full, design.F("speed_kn")), 2)}   " +
         $"{Py.F(r.PowerShp / r.Full, 0)} hp/t   (placeholder power model)"];

    static PyDict PlaningHullSpec(PyDict design)
    {
        var h = design.D("hull");
        return PyDict.Of(("length", h["length"]), ("beam", h["beam"]), ("deck_inset", 0.15), ("plank_spacing", 0.55),
            ("bow", PyDict.Of(("taper", 0.45), ("power", 1.25), ("shape", "pointed"))),
            ("stern", PyDict.Of(("taper", 0.04), ("transom", 0.92), ("power", 2.0), ("shape", "round"))));
    }

    static Layout PlaningLayout(PyDict design, Navarch.Result res, double shift)
    {
        double depth = res.Depth;
        var lay = new Layout(design, WOOD_T_PER_M2, PLANK_MM);
        var hs = PlaningHullSpec(design);
        var hull = new Hull(hs);
        lay.Hull = hull;
        double L = hull.L, B = hull.B;
        lay.ShiftRange = (-0.04 * L, 0.04 * L);
        shift = Layout.Clamp(shift, lay.ShiftRange.Lo, lay.ShiftRange.Hi);
        lay.Geo.Shift = shift;

        var blocks = new List<PyDict>();
        double cx0 = 0.0 + shift, cx1 = 0.22 * L + shift;
        double wc = 0.42 * B;
        Layout.AddBlock(lay, blocks, "Charthouse", cx0, cx1, wc, 1, 0.45 * wc, 0.2, role: "bridge");
        var masts = new List<PyDict> { PyDict.Of(("x", cx0 + 0.25 * (cx1 - cx0)), ("yard", Py.Min(0.5 * B, 2.4)), ("tripod", false),
            ("top", Layout.LEVEL_H + 3.5)) };

        double m0 = -0.42 * L, m1 = -0.08 * L;
        double lMach = Layout.PlanMachinery(lay, design, res, hull, (m0 + m1) / 2);
        if (lMach > m1 - m0)
            lay.Fail("length", $"The engines need {Py.F(lMach, 1)} m, but the engine room has {Py.F(m1 - m0, 1)} m.");
        lay.Geo.Machinery = (m0, m1);
        lay.Geo.MachineryX = (m0 + m1) / 2;
        Layout.SetCitadel(lay, m0, m1);

        var mounts = new List<PyDict>();
        var turretTypes = new PyDict();
        var tp = design.Or("torpedoes", null) as PyDict ?? new PyDict();
        if (Py.Truthy(tp.Get("mounts")))
        {
            var xs = Py.Range(Py.Int(0.6 * L / 0.5)).Select(k => cx0 + 0.3 * (cx1 - cx0) - k * 0.5);

            double YOfX(double x, PyDict t)
            {
                double w = ((t.F("barrels") - 1) * t.F("spacing") + t.F("barrel_w")) / 2;
                return Py.Min(hull.HalfWidth(x + t.F("barrel_len") / 2), hull.HalfWidth(x - t.F("barrel_len") / 2)) - w - 0.15;
            }

            Armament.FixedTubePairs(lay, mounts, turretTypes, tp, xs, YOfX, toeDeg: 5.0);
        }

        var (fore, aft) = Armament.GunGroups(design);
        if (fore != null)
        {
            Armament.GunLine(lay, mounts, turretTypes, fore, "main", ["A", "B", "C"], L / 2 - 0.16 * L, -1, 0.0, 0, x => 0.2,
                armourMm: fore.Get("armour_mm", 0L), depth: depth);
            Armament.GunLine(lay, mounts, turretTypes, aft!, "main", ["Y", "X", "W"], -L / 2 + 1.6, +1, 0.0, 180, x => 0.2,
                armourMm: fore.Get("armour_mm", 0L), depth: depth);
        }

        var xs2 = CarrierStyle.Vdc(48).Select(v => -L / 2 + 0.1 * L + v * 0.7 * L).ToList();
        var secs = Batteries.SecondaryBatteries(design);
        Armament.WarnUnpaired(lay, secs);
        var sec = secs.Count > 0 ? secs[0] : new PyDict();
        if (Py.Truthy(sec.Get("per_side")))
        {
            var (tsId, ts) = Armament.GunType(sec);
            double r = Armament.BodyReach(ts);
            Armament.SidePairs(lay, mounts, turretTypes, "secondary", tsId, ts, Py.ToLong(sec["per_side"]),
                xs2.Select(x => new object?[] { x, hull.HalfWidth(x) - r - 0.3, 0.2 }), "S", armourMm: sec["armour_mm"]);
        }
        FireControl.Place(lay, design, blocks);
        var aaOut = new List<PyDict>();
        var aaReq = design.Or("aa", null) as PyDict ?? new PyDict();
        foreach (var (kind, count) in new[] { ("quad40", aaReq.Get("heavy", 0L)), ("single20", aaReq.Get("light", 0L)) })
        {
            double rr = Geometry.AA_CFG[kind].R;
            var cands = new List<object?[]> { new object?[] { cx0 - rr - 0.3, rr + 0.15, 0.2 }, new object?[] { cx0 - rr - 0.3, 0.0, 0.2 } };
            cands.AddRange(xs2.Select(x => new object?[] { x, hull.HalfWidth(x) - rr - 0.3, 0.2 }));
            cands.AddRange(xs2.Select(x => new object?[] { x, 0.0, 0.2 }));
            Armament.PlaceAa(lay, aaOut, kind, Py.ToLong(count), cands, spacing: 0.6);
        }

        var fittings = new List<object?>();
        for (int k = 0; k < 3; k++)
        {
            double hx = m0 + (k + 0.5) * (m1 - m0) / 3, hl = 0.18 * (m1 - m0);
            var fp = Footprint.Rect(hx - hl / 2, -0.16 * B, hx + hl / 2, 0.16 * B);
            if (lay.Free(fp, 0.2))
            {
                fittings.Add(PyDict.Of(("x", hx), ("y", 0.0), ("l", hl), ("w", 0.32 * B), ("color", "hatch_coaming")));
                lay.Occupy(fp, 0, 0.5, $"Engine hatch {k + 1}");
            }
        }
        var fpS = Footprint.Rect(-L / 2 + 0.2, -0.18 * B, -L / 2 + 1.0, 0.18 * B);
        if (lay.Free(fpS, 0.1))
        {
            fittings.Add(PyDict.Of(("x", -L / 2 + 0.6), ("y", 0.0), ("l", 0.7), ("w", 0.35 * B), ("color", "fitting")));
            lay.Occupy(fpS, 0, 1.0, "Smoke generator");
        }
        var boats = new List<object?>();
        double rl = Layout.Clamp(0.1 * L, 1.8, 3.0), rw = Layout.Clamp(0.22 * B, 1.0, 1.6);
        foreach (var x in Py.Range(Py.Int((m1 - m0) / 0.5)).Select(k => m1 - rl / 2 - k * 0.5))
        {
            var fp = Footprint.Rect(x - rl / 2, -rw / 2, x + rl / 2, rw / 2);
            if (lay.Free(fp, 0.2))
            {
                boats.Add(PyDict.Of(("x", x), ("y", 0.0), ("l", rl), ("w", rw), ("top", 1.0)));
                lay.Occupy(fp, 0.5, 1.0, "Raft");
                break;
            }
        }

        double innerHw = 0.8 * B / 2;
        lay.Compartments.AddRange([
            PyDict.Of(("id", "Crew space"), ("kind", "accommodation"), ("x0", cx1), ("x1", L / 2 - 0.08 * L), ("half_width", innerHw)),
            PyDict.Of(("id", "Fuel tanks"), ("kind", "fuel_tank"), ("x0", m1), ("x1", cx0 + 0.05 * L), ("half_width", innerHw)),
            PyDict.Of(("id", "Engine room"), ("kind", "engine_room"), ("x0", m0), ("x1", m1), ("half_width", innerHw)),
        ]);
        Layout.AddSteering(lay, -L / 2, m0, 0.6 * B / 2, "Tiller flat");
        var guns = Ordnance.Guns(mounts);
        if (guns.Count > 0)
        {
            double lx1 = L / 2 - 0.08 * L, lw = 0.5 * innerHw;
            Ordnance.Stow(lay, mounts, [PyDict.Of(("x0", lx1 - Ordnance.ZoneLength(Ordnance.BookedM3(lay, guns), 2 * lw, lay.Geo.Plant!, 1, 0.5)),
                ("x1", lx1), ("half_width", lw), ("rooms", new List<object?> { PyDict.Of(("id", "Ammunition locker"),
                    ("mounts", guns.Cast<object?>().ToList())) }))]);
        }
        return Layout.FinishLayout(lay, design, hs, mounts, turretTypes, blocks, [], masts, aaOut, 0.0, null,
            ("deck_inset", hs["deck_inset"]), ("plank_spacing", hs["plank_spacing"]), ("boats", boats), ("fittings", fittings));
    }
}
