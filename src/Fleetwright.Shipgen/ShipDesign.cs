namespace Fleetwright.Shipgen;

/// <summary>shipdesign: the design side. A player's design (JSON) in, the designed ship out as plain data. No drawing.
/// validate() lists the input errors ([] means build() can run); build() returns the ship dict (design, report,
/// hitboxes, render).</summary>
public static class ShipDesign
{
    const double MAST_ABOVE_FUNNEL = 6.0;

    /// <summary>Input errors. limits=false skips the numeric ranges (--no-limits); structural checks stay.</summary>
    public static List<string> Validate(PyDict design, bool limits = true)
    {
        Style style;
        try
        {
            style = Styles.Get(design);
        }
        catch (StyleError e)
        {
            return [e.Message];
        }
        var errs = new List<string>();
        foreach (var lim in limits ? style.Limits() : [])
        {
            foreach (var d in Style.Walk(design, lim.Path))
            {
                string key = lim.Path[^1];
                if (!d.Has(key))
                    continue;
                var v = d[key];
                string path = string.Join('.', lim.Path);
                if (!Py.IsNumber(v) || v is bool)
                    errs.Add($"{path} = {Py.Repr(v)}: give a number, {Py.Str(lim.Lo)}..{Py.Str(lim.Hi)}");
                else if (!(Py.ToDouble(lim.Lo) <= Py.ToDouble(v) && Py.ToDouble(v) <= Py.ToDouble(lim.Hi)))
                    errs.Add($"{path} = {Py.Str(v)} is outside {Py.Str(lim.Lo)}..{Py.Str(lim.Hi)}");
            }
        }
        if (!design.Has("id"))
            errs.Add("design needs an 'id'");
        errs.AddRange(style.Validate(design));
        return errs;
    }

    /// <summary>The shortest hull a displacement ship of disp_t tonnes gets for v_kn knots.</summary>
    static double MinLength(double dispT, double vKn)
    {
        double root = Py.Pow(dispT / Weight.SEAWATER, 1.0 / 3);
        double fnv = Navarch.VolumetricFroude(dispT, vKn);
        return Py.Max(5.25, Py.Min(8.2, 5.25 + 5.0 * (fnv - 0.55))) * root;
    }

    /// <summary>The design with a hull of L x B metres (and the style's block coefficient if it gives none).</summary>
    static PyDict WithHull(PyDict design, double L, double B)
    {
        var hull = design.DOr("hull");
        var cb = hull.Has("block_coefficient") ? hull["block_coefficient"] : Styles.Get(design).DEFAULT_CB;
        var nh = hull.Copy();
        nh.Update(("length", L), ("beam", B), ("block_coefficient", cb));
        var d = design.Copy();
        d["hull"] = nh;
        return d;
    }

    /// <summary>The narrowest beam (at least B) for a hull of length L carrying `weights` (placed, from a layout).</summary>
    static double BeamNeeded(PyDict design, double L, double B, List<Weight> weights, Geo geo)
    {
        var size = Styles.Get(design).SIZE;
        double bMax = size.BeamMax;
        double lo = Py.Max(B, L / size.LbMax);

        bool Ok(double b)
        {
            var r = Navarch.Solve(WithHull(design, L, b), weights, geo);
            return r.GmFull >= size.GmFrac * b && r.Draught <= size.Tb * b;
        }

        if (Ok(lo))
            return lo;
        double hi = lo;
        bool broke = false;
        while (hi < bMax)
        {
            (lo, hi) = (hi, Py.Min(bMax, hi * 1.15));
            if (Ok(hi))
            {
                broke = true;
                break;
            }
        }
        if (!broke)
            return bMax;
        while (hi - lo > 0.05 * Py.Max(1.0, lo / 10))
        {
            double mid = (lo + hi) / 2;
            (lo, hi) = Ok(mid) ? (lo, mid) : (mid, hi);
        }
        return hi;
    }

    /// <summary>The style's layout for the solved weights r, then crewed.</summary>
    static Layout LayOut(PyDict design, Navarch.Result r, double shift = 0.0, double spread = 0.0)
    {
        var style = Styles.Get(design);
        var lay = style.BuildLayout(design, r, shift, spread);
        Crew.Apply(lay, design, r, style);
        return lay;
    }

    /// <summary>Does everything fit on a hull of length L? (fits, beam).</summary>
    static (bool Fits, double B) Fit(PyDict design, double L, double B = 0.0)
    {
        var size = Styles.Get(design).SIZE;
        double bMax = size.BeamMax;
        B = Py.Max(B, L / size.LbMax);
        bool slender = size.Slender;
        Navarch.Result r = null!;
        Layout lay = null!;
        for (int i = 0; i < 12; i++)
        {
            var d = WithHull(design, L, B);
            r = Navarch.Solve(d);
            lay = LayOut(d, r, 0.0);
            double B2 = BeamNeeded(design, L, B, lay.Weights, lay.Geo);
            if (lay.Short.Contains("beam") && B < bMax)
                B2 = Py.Max(B2, B * 1.05);
            if (B2 <= B * 1.005)
                break;
            B = Py.Min(B2, bMax);
        }
        bool longEnough = !slender || L >= MinLength(r.Full, design.F("speed_kn"));
        return (!lay.Short.Contains("length") && longEnough, B);
    }

    /// <summary>The hull for a design that gives none: the shortest that fits everything, and the narrowest beam that
    /// carries it. hint: the length of a similar design to start the search from.</summary>
    static (double L, double B) Size(PyDict design, double? hint = null)
    {
        var (lMin, lMax) = Styles.Get(design).SIZE.Length;
        double Snap(double v) => Py.Max(lMin, Py.Min(lMax, Py.Round(v * 2) / 2.0));
        double L, step;
        if (hint is double h && h != 0)
            (L, step) = (Snap(h), 1.03);
        else
        {
            (L, step) = (100.0, 1.15);
            for (int i = 0; i < 3; i++)
            {
                var r = Navarch.Solve(WithHull(design, L, L / 7.5));
                L = Snap(5.0 * Py.Pow(r.Full, 1.0 / 3));
            }
        }
        var cache = new Dictionary<double, (bool Fits, double B)>();

        bool Ok(double l)
        {
            if (!cache.TryGetValue(l, out var v))
                cache[l] = v = Fit(design, l);
            return v.Fits;
        }

        double? lo = null, hi = null;
        if (Ok(L))
        {
            hi = L;
            while (hi > lMin)
            {
                L = Snap(Py.Min(hi.Value - 0.5, hi.Value / step));
                if (!Ok(L))
                {
                    lo = L;
                    break;
                }
                hi = L;
            }
        }
        else
        {
            lo = L;
            while (lo < lMax)
            {
                L = Snap(Py.Max(lo.Value + 0.5, lo.Value * step));
                if (Ok(L))
                {
                    hi = L;
                    break;
                }
                lo = L;
            }
        }
        hi ??= lMax;
        while (lo is not null && hi.Value - lo.Value > Py.Max(0.5, 0.004 * hi.Value))
        {
            double mid = Snap((lo.Value + hi.Value) / 2);
            if (mid == lo.Value || mid == hi.Value)
                break;
            if (Ok(mid))
                hi = mid;
            else
                lo = mid;
        }
        var fitHi = cache.TryGetValue(hi.Value, out var fv) ? fv : Fit(design, hi.Value);
        return (hi.Value, Py.Round(fitHi.B, 2));
    }

    /// <summary>Size the hull, then lay it out and balance it. Returns the internal (layout, result, sized design).</summary>
    static (Layout Lay, Navarch.Result R, PyDict Sized) Solve(PyDict design, int iterations = 6, double? hint = null)
    {
        var (L, B) = Size(design, hint);
        double lMax = Styles.Get(design).SIZE.Length.Max, bMax = Styles.Get(design).SIZE.BeamMax;
        Layout lay = null!;
        Navarch.Result r = null!;
        PyDict sized = null!;
        for (int i = 0; i < 6; i++)
        {
            (lay, r, sized) = Balance(WithHull(design, L, B), iterations);
            if (lay.Short.Count == 0 || (L >= lMax && B >= bMax))
                break;
            if (lay.Short.Contains("beam"))
                B = Py.Min(bMax, Py.Round(B * 1.03, 2));
            if (lay.Short.Contains("length"))
                L = Py.Min(lMax, Py.Max(L + 0.5, Py.Round(L * 1.01 * 2) / 2.0));
        }
        if (lay.Short.Contains("length") && L >= lMax && lay.Errors.Count == 0)
            lay.Errors.Add($"Not everything fits even on the longest hull ({Py.F(lMax, 0, comma: true)} m). Carry less.");
        if (lay.Short.Count == 0)
            (lay, r, sized) = SpreadEnds(sized, lay, r, iterations);
        return (lay, r, sized);
    }

    /// <summary>Give the ends as much of the spare length as they take without the layout faring worse.</summary>
    static (Layout, Navarch.Result, PyDict) SpreadEnds(PyDict design, Layout lay, Navarch.Result r, int iterations = 6)
    {
        double shift = lay.Geo.Shift;

        (bool Short, HashSet<string> Errs, HashSet<string> Warns) Outcome(double s)
        {
            var l_ = LayOut(design, r, shift, s);
            var r_ = Navarch.Solve(design, l_.Weights, l_.Geo);
            FireControl.AssignSmoke(l_, r_);
            var errs = new HashSet<string>(l_.Errors, StringComparer.Ordinal);
            errs.UnionWith(r_.Errors);
            return (l_.Short.Count > 0, errs, new HashSet<string>(l_.Warnings, StringComparer.Ordinal));
        }

        var (_, errs0, warns0) = Outcome(0.0);

        bool Ok((bool Short, HashSet<string> Errs, HashSet<string> Warns) o) => !o.Short && o.Errs.IsSubsetOf(errs0) && o.Warns.IsSubsetOf(warns0);

        double s;
        if (Ok(Outcome(1.0)))
            s = 1.0;
        else
        {
            double lo = 0.0, hi = 1.0;
            for (int i = 0; i < 5; i++)
            {
                double mid = (lo + hi) / 2;
                (lo, hi) = Ok(Outcome(mid)) ? (mid, hi) : (lo, mid);
            }
            s = lo;
        }
        if (s <= 0.0)
            return (lay, r, design);
        var (lay2, r2, _) = Balance(design, iterations, s);
        var e2 = new HashSet<string>(lay2.Errors, StringComparer.Ordinal);
        e2.UnionWith(r2.Errors);
        if (lay2.Short.Count > 0 || !e2.IsSubsetOf(errs0) || !new HashSet<string>(lay2.Warnings, StringComparer.Ordinal).IsSubsetOf(lay.Warnings))
            return (lay, r, design);
        return (lay2, r2, design);
    }

    /// <summary>Rough solve -&gt; layout -&gt; solve -&gt; shift to balance; repeat until stable.</summary>
    public static (Layout Lay, Navarch.Result R, PyDict Design) Balance(PyDict design, int iterations = 6, double spread = 0.0)
    {
        var r = Navarch.Solve(design);
        double shift = 0.0;
        Layout lay = null!;
        (double M, double Shift)? best = null;
        var bracket = new List<(double Shift, double Moment)>();
        for (int i = 0; i < iterations + 4; i++)
        {
            lay = LayOut(design, r, shift, spread);
            r = Navarch.Solve(design, lay.Weights, lay.Geo);
            shift = lay.Geo.Shift;
            double moment = Py.Sum(r.Weights.Select(w => w.W * (w.X - r.Lcb)));
            double movable = Py.Sum(r.Weights.Where(w => !(w.Group is "hull" or "misc")).Select(w => w.W));
            if (best is null || Math.Abs(moment) < best.Value.M)
                best = (Math.Abs(moment), shift);
            var keep = bracket.Where(b => (b.Moment > 0) != (moment > 0)).ToList();
            bracket = keep.Skip(Math.Max(0, keep.Count - 1)).ToList();
            bracket.Add((shift, moment));
            double newShift = bracket.Count == 2
                ? (bracket[0].Shift + bracket[1].Shift) / 2
                : Py.Max(lay.ShiftRange.Lo, Py.Min(lay.ShiftRange.Hi, shift - moment / movable));
            if (Math.Abs(newShift - shift) < 0.05)
                break;
            shift = newShift;
        }
        shift = best!.Value.Shift;
        lay = LayOut(design, r, shift, spread);
        r = Navarch.Solve(design, lay.Weights, lay.Geo);
        Arcs.AssignArcs(lay);
        FireControl.AssignSmoke(lay, r);
        return (lay, r, design);
    }

    /// <summary>The plant's static numbers for the game and how it sits in the hull.</summary>
    static PyDict PlantReport(Layout lay, Navarch.Result r)
    {
        var plan = lay.Geo.Plant ?? new PyDict();
        var sp = plan.Get("space", new PyDict()) as PyDict ?? new PyDict();
        var fp = lay.Geo.FunnelPlan ?? new PyDict();
        var segs = plan.Has("segments") ? Layout.PlanSegments(plan) : [];
        var extra = PyDict.Of(
            ("machinery_length_m", Py.Round(Py.Sum(segs.Select(s => s.Len)), 1)),
            ("boiler_rooms", (long)lay.Compartments.Count(c => Py.Eq(c["kind"], "boiler_room"))),
            ("engine_rooms", (long)lay.Compartments.Count(c => Py.Eq(c["kind"], "engine_room"))),
            ("rows", sp.Get("rows")), ("protrusion_m", Py.Round(sp.F("protrusion", 0.0), 2)),
            ("space_m", PyDict.Of(("width", Py.Round(plan.F("width", 0.0), 2)), ("height", Py.Round(plan.F("height", 0.0), 2)))),
            ("wing_bunkers_t", Py.Round(plan.F("wing_t", 0.0))), ("end_bunkers_m", Py.Round(plan.F("end_m", 0.0), 1)),
            ("funnels", (long)lay.Funnels.Count), ("funnel_gas_area_m2", Py.Round(fp.F("area", 0.0), 1)),
            ("funnel_gas_velocity_m_s", Py.Round(fp.F("velocity", 0.0), 1)),
            ("smoke_reach_m", lay.Funnels.Count > 0 ? Py.Round(Powerplant.SmokeReach(r.Plant, r.PowerShp), 1) : 0.0));
        return Powerplant.Published(r.Plant, r.PowerShp, extra);
    }

    /// <summary>The hull structure and its girder amidships, for the damage model.</summary>
    static PyDict HullReport(PyDict design, Navarch.Result r)
    {
        var h = r.Hull;
        if (!h.Has("t_min_mm"))
            return PyDict.Of(("structure_t", Py.Round(h.F("t"))));
        return PyDict.Of(("construction", HullWeight.Construction(design).Get("name")), ("structure_t", Py.Round(h.F("t"))),
            ("min_gauge_t", Py.Round(h.F("min_gauge_t"))), ("strength_t", Py.Round(h.F("strength_t"))),
            ("plate_min_mm", Py.Round(h.F("t_min_mm"), 1)), ("plate_strength_mm", Py.Round(h.F("t_str_mm"), 1)),
            ("shell_plating_t", Py.Round(h.F("shell_t"))),
            ("girder", PyDict.Of(("allowable_stress_mpa", Py.Round(h.F("stress_mpa"), 1)), ("required_m4", Py.Round(h.F("i_req_m4"), 2)),
                ("plating_m4", Py.Round(h.F("i_plating_m4"), 2)), ("armour_decks_m4", Py.RoundObj(h["i_armour_m4"], 2)))));
    }

    /// <summary>design: the player's input (echoed in "inputs"); sized: the same with the hull the designer chose.</summary>
    static PyDict ReportDict(PyDict design, Layout lay, Navarch.Result r, PyDict sized)
    {
        var h = sized.D("hull");
        var results = PyDict.Of(("length_m", h["length"]), ("beam_m", h["beam"]), ("block_coefficient", h["block_coefficient"]),
            ("standard_displacement_t", Py.Round(r.Std)), ("full_displacement_t", Py.Round(r.Full)), ("draught_m", Py.Round(r.Draught, 2)),
            ("depth_m", Py.Round(r.Depth, 2)), ("freeboard_m", Py.Round(r.Freeboard, 2)), ("power_shp", Py.Round(r.PowerShp, -2)),
            ("fuel_t", Py.Round(r.Fuel)), ("crew", lay.Crew!["complement"]), ("gm_full_m", Py.Round(r.GmFull, 2)),
            ("gm_light_m", Py.Round(r.GmLight, 2)), ("roll_period_s", Py.Round(r.RollS, 1)));
        if (r.Wind.Count > 0)
            results.Update(("windage_m2", Py.Round(r.Wind.F("area_m2"))), ("gale_heel_deg", Py.Round(r.Wind.F("heel_deg"), 1)),
                ("gale_heel_condition", r.Wind["condition"]), ("deck_edge_deg", Py.Round(r.Wind.F("deck_edge_deg"), 1)),
                ("deck_edge_wind_kn", Py.Round(r.Wind.F("deck_edge_wind_kn"))));
        results.Update(("trim_m", Py.Round(r.TrimM, 2)), ("lcg_m", Py.Round(r.Lcg, 2)), ("lcb_m", Py.Round(r.Lcb, 2)),
            ("layout_shift_m", Py.Round(lay.Geo.Shift, 2)));
        results.Update(Styles.Get(design).Results(sized, lay, r));
        var report = PyDict.Of(("id", design["id"]), ("name", design.Get("name", design["id"])), ("valid", lay.Errors.Count == 0 && r.Errors.Count == 0),
            ("errors", lay.Errors.Concat(r.Errors).Cast<object?>().ToList()), ("warnings", lay.Warnings.Concat(r.Warnings).Cast<object?>().ToList()),
            ("inputs", design), ("results", results), ("plant", PlantReport(lay, r)), ("hull", HullReport(design, r)), ("crew", lay.Crew),
            ("fire_control", FireControl.Report(lay, r.Freeboard)));
        if (lay.Geo.Bridge != null)
            report["bridge"] = BridgeReport(lay, r.Freeboard);
        var groups = Py.Sorted(r.Groups, kv => -kv.Value);
        report["weight_groups_t"] = PyDict.Of(groups.Select(kv => (kv.Key, (object?)Py.Round(kv.Value))).ToArray());
        report["weights"] = r.Weights.Select(w => (object?)PyDict.Of(("name", w.Name), ("group", w.Group), ("t", Py.Round(w.W, 1)),
            ("x", Py.Round(w.X, 2)), ("z", Py.Round(w.Z!.Value, 2)))).ToList();
        return report;
    }

    /// <summary>The unarmoured plating: the hull's from its structure, the superstructure's from the design.</summary>
    static PyDict HullPlating(Layout lay, PyDict design, Navarch.Result res)
    {
        var sup = design.DOr("superstructure");
        var hp = HullWeight.Plating(design);
        var h = res.Hull.Has("t_min_mm") ? res.Hull : PyDict.Merge(res.Hull, PyDict.Of(("plate_own_mm", Layout.OwnPlateMm(lay))));
        return HullWeight.Plates(h, lay.Hull.L, hp.F("shell_mm"), hp["material"], sup.F("plating_mm", 0.0), sup.F("control_mm", 0.0),
            hp.F("deck_wood_mm"));
    }

    /// <summary>What the solved, laid-out ship is inside, beyond the layout.</summary>
    public sealed record Interior(PyDict Armour, HullForm Form, PyDict Subdivision, PyDict Plating, List<string> Planked,
        PyDict Hydrostatics, PyDict Propulsion, Dictionary<(string Kind, string Id), long> BattleCrew);

    static Interior InteriorOf(Layout lay, PyDict design, Navarch.Result r)
    {
        double D = r.Depth, T = r.Draught;
        var ag = r.Armour;
        double cb = design.D("hull").F("block_coefficient");
        var gear = Propulsion.Gear(lay, design, r);
        var form = new HullForm(lay.Hull, cb, Geometry.Cwp(cb), T, D, Navarch.Froude(design.F("speed_kn"), lay.Hull.L), gear, r.Lcb);
        var sub = Subdivision.Build(lay, design, r, ag, Py.Truthy(ag["armoured"]), form);
        var plating = HullPlating(lay, design, r);
        var planked = Subdivision.DeckPlates(sub, plating, design, lay);
        var hydro = Stability.Hydrostatics(form, r);
        var train = Propulsion.Build(lay, design, r, form, gear);
        Propulsion.Link(train, sub, D);
        return new Interior(ag, form, sub, plating, planked, hydro, train, Crew.AssignBattleCrew(lay, sub));
    }

    const double BRIDGE_EYE = 1.7;

    /// <summary>The navigating bridge's view (warships).</summary>
    static PyDict BridgeReport(Layout lay, double deckM)
    {
        var b = lay.Geo.Bridge!;
        double eye = deckM + b.F("floor") + BRIDGE_EYE;
        return PyDict.Of(("level", b["level"]), ("tower_levels", b["tower"]), ("eye_height_m", Py.Round(eye, 2)),
            ("horizon_km", Py.Round(FireControl.HorizonKm(eye), 1)), ("sees_over_turrets", b.F("level") >= b.F("need")),
            ("level_to_see_over_turrets", b["need"]));
    }

    /// <summary>The static height-map columns, lowest first.</summary>
    static List<PyDict> HeightColumns(Layout lay, double deckM)
    {
        var items = new List<PyDict> { PyDict.Of(("top", deckM), ("shape", "hull")) };
        foreach (var dk in lay.Decks.Concat(lay.Sponsons))
            items.Add(PyDict.Of(("top", deckM + dk.F("top")), ("shape", "polygon"), ("points", dk["points"])));
        foreach (PyDict ht in (lay.Spec.Get("hatches") as List<object?> ?? []).Cast<PyDict>())
            items.Add(PyDict.Of(("top", deckM + ht.F("top", 1.2)), ("shape", "rect"), ("x", ht.F("x") - ht.F("l") / 2),
                ("y", ht.F("y") - ht.F("w") / 2), ("w", ht["l"]), ("h", ht["w"])));
        foreach (PyDict cr in (lay.Spec.Get("cranes") as List<object?> ?? []).Cast<PyDict>())
            items.Add(PyDict.Of(("top", deckM + cr.F("top")), ("shape", "circle"), ("cx", cr["x"]), ("cy", cr["y"]), ("r", cr.Get("r", 1.2))));
        foreach (var m in lay.Mounts)
        {
            var t = m.D("t");
            if (Geometry.HasBarbette(t) && m.F("base") > 0.5)
                items.Add(PyDict.Of(("top", deckM + m.F("base")), ("shape", "circle"), ("cx", m["x"]), ("cy", m["y"]), ("r", t.F("r") * 0.95)));
        }
        foreach (var b in lay.Blocks)
            items.Add(PyDict.Of(("top", deckM + Layout.BlockTop(b)), ("shape", "polygon"), ("points", Geometry.BlockOutline(b))));
        foreach (var a in lay.Aa)
            items.Add(PyDict.Of(("top", deckM + a.F("base") + 2.0), ("shape", "circle"), ("cx", a["x"]), ("cy", a["y"]),
                ("r", Geometry.AA_CFG[a.S("type")].R * 0.8)));
        foreach (PyDict bt in (lay.Spec.Get("boats") as List<object?> ?? []).Cast<PyDict>())
            items.Add(PyDict.Of(("top", deckM + bt.F("top", Layout.LEVEL_H + 1.5)), ("shape", "ellipse"), ("cx", bt["x"]), ("cy", bt["y"]),
                ("rx", bt.F("l") / 2), ("ry", bt.F("w") / 2)));
        foreach (var fn in lay.Funnels)
        {
            var pts = Geometry.RrectPolygon(fn.F("x") - fn.F("l") / 2, fn.F("y") - fn.F("w") / 2, fn.F("x") + fn.F("l") / 2,
                fn.F("y") + fn.F("w") / 2, fn.F("w") / 2, fn.F("w") / 2);
            items.Add(PyDict.Of(("top", deckM + lay.FunTop), ("shape", "polygon"), ("points", pts)));
        }
        foreach (PyDict m in (lay.Spec.Get("masts") as List<object?> ?? []).Cast<PyDict>())
        {
            double top = m.F("top", lay.FunTop + MAST_ABOVE_FUNNEL);
            items.Add(PyDict.Of(("top", deckM + top), ("shape", "circle"), ("cx", m["x"]), ("cy", m.Get("y", 0L)), ("r", 0.7)));
        }
        return Py.Sorted(items, it => it.F("top"));
    }

    /// <summary>Design the ship: the published, plain-data result. hint: the hull length of a similar earlier build.</summary>
    public static PyDict Build(PyDict design, double? hint = null)
    {
        var (lay, r, sized) = Solve(design, hint: hint);
        double deckM = Py.Max(r.Freeboard, 0.1);
        var inner = InteriorOf(lay, sized, r);
        var hitboxes = Hitbox.ExportHitboxes(lay, sized, r, inner);
        return PyDict.Of(
            ("design", design),
            ("report", ReportDict(design, lay, r, sized)),
            ("hitboxes", hitboxes),
            ("render", PyDict.Of(
                ("spec", lay.Spec),
                ("deck_m", deckM),
                ("mounts", lay.Mounts.Select(m =>
                {
                    var o = PyDict.Of(("id", m["id"]), ("kind", m["kind"]), ("rest", m["rest"]), ("arcs", m["arcs"]), ("traverse", m["traverse"]),
                        ("top", m["top"]));
                    if (m.B("casemate"))
                        o["mount"] = "casemate";
                    return (object?)o;
                }).ToList()),
                ("columns", HeightColumns(lay, deckM).Cast<object?>().ToList()),
                ("summary", Styles.Get(design).Summary(sized, lay, r)))));
    }
}

/// <summary>looks.validate: the design's look (navy, era). The rest of looks.py is the renderer's (Step 4).</summary>
public static class Looks
{
    static readonly string[] NAVIES = ["generic", "brooklyn", "kure", "portsmouth", "kiel", "la_spezia", "toulon"];
    static readonly string[] ERAS = ["victorian", "great_war", "treaty", "wwii", "cold_war"];

    public static List<string> Validate(PyDict design)
    {
        var lkV = design.Get("look", new PyDict());
        if (lkV is not PyDict lk)
            return [$"look = {Py.Repr(lkV)}: give {{\"navy\": ..., \"era\": ...}}"];
        var extra = Py.Sorted(lk.Keys.Where(k => !(k is "navy" or "era" or "number")).Distinct());
        var errs = extra.Count > 0 ? new List<string> { $"look has unknown keys: {string.Join(", ", extra)}" } : [];
        var look = PyDict.Merge(PyDict.Of(("navy", "generic"), ("era", "wwii")), lk);
        if (!(look["navy"] is string n && NAVIES.Contains(n)))
            errs.Add($"look.navy = {Py.Repr(look["navy"])}: use {string.Join(", ", NAVIES)}");
        if (!(look["era"] is string e && ERAS.Contains(e)))
            errs.Add($"look.era = {Py.Repr(look["era"])}: use {string.Join(", ", ERAS)}");
        return errs;
    }
}
