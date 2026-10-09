namespace Fleetwright.Shipgen;

/// <summary>The design side: a player's design in, the designed ship out as plain data. No drawing. Validate lists
/// the input errors (none: Build can run); Build returns the ship (design, report, hitboxes, render).</summary>
public static class ShipDesign
{
    const double MAST_ABOVE_FUNNEL = 6.0;

    /// <summary>Input errors. limits false skips the numeric ranges (--no-limits); structural checks stay.</summary>
    public static List<string> Validate(Design design, bool limits = true)
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
        var errs = limits ? Style.LimitErrors(design, style.Limits()) : [];
        if (design.Id is null)
            errs.Add("design needs an 'id'");
        if (design.SpeedKn is null)
            errs.Add("design needs a 'speed_kn'");
        errs.AddRange(style.Validate(design));
        return errs;
    }

    /// <summary>The shortest hull a displacement ship of disp_t tonnes gets for v_kn knots.</summary>
    static double MinLength(double dispT, double vKn)
    {
        double root = Math.Pow(dispT / Weight.SEAWATER, 1.0 / 3);
        double fnv = Navarch.VolumetricFroude(dispT, vKn);
        return Math.Max(5.25, Math.Min(8.2, 5.25 + 5.0 * (fnv - 0.55))) * root;
    }

    /// <summary>The design with a hull of L x B metres (and the style's block coefficient if it gives none).</summary>
    static Design WithHull(Design design, double L, double B)
    {
        var hull = design.Hull ?? new HullInput();
        return design with
        {
            Hull = hull with { Length = L, Beam = B, BlockCoefficient = hull.BlockCoefficient ?? Styles.Get(design).DefaultBlockCoefficient },
        };
    }

    /// <summary>The narrowest beam (at least B) for a hull of length L carrying `weights` (placed, from a layout).</summary>
    static double BeamNeeded(Design design, double L, double B, List<Weight> weights, Geo geo)
    {
        var size = Styles.Get(design).Sizing;
        double bMax = size.BeamMax;
        double lo = Math.Max(B, L / size.LbMax);

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
            (lo, hi) = (hi, Math.Min(bMax, hi * 1.15));
            if (Ok(hi))
            {
                broke = true;
                break;
            }
        }
        if (!broke)
            return bMax;
        while (hi - lo > 0.05 * Math.Max(1.0, lo / 10))
        {
            double mid = (lo + hi) / 2;
            (lo, hi) = Ok(mid) ? (lo, mid) : (mid, hi);
        }
        return hi;
    }

    /// <summary>The style's layout for the solved weights r, then crewed.</summary>
    static Layout LayOut(Design design, Navarch.Result r, double shift = 0.0, double spread = 0.0)
    {
        var style = Styles.Get(design);
        var lay = style.BuildLayout(design, r, shift, spread);
        Crew.Apply(lay, design, r, style);
        return lay;
    }

    /// <summary>Does everything fit on a hull of length L? (fits, beam).</summary>
    static (bool Fits, double B) Fit(Design design, double L, double B = 0.0)
    {
        var size = Styles.Get(design).Sizing;
        double bMax = size.BeamMax;
        B = Math.Max(B, L / size.LbMax);
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
                B2 = Math.Max(B2, B * 1.05);
            if (B2 <= B * 1.005)
                break;
            B = Math.Min(B2, bMax);
        }
        bool longEnough = !slender || L >= MinLength(r.Full, design.SpeedKn!.Value);
        return (!lay.Short.Contains("length") && longEnough, B);
    }

    /// <summary>The hull for a design that gives none: the shortest that fits everything, and the narrowest beam that
    /// carries it. hint: the length of a similar design to start the search from.</summary>
    static (double L, double B) Size(Design design, double? hint = null)
    {
        var (lMin, lMax) = Styles.Get(design).Sizing.Length;
        double Snap(double v) => Math.Max(lMin, Math.Min(lMax, (long)Math.Round(v * 2) / 2.0));
        double L, step;
        if (hint is double h && h != 0)
            (L, step) = (Snap(h), 1.03);
        else
        {
            (L, step) = (100.0, 1.15);
            for (int i = 0; i < 3; i++)
            {
                var r = Navarch.Solve(WithHull(design, L, L / 7.5));
                L = Snap(5.0 * Math.Pow(r.Full, 1.0 / 3));
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
                L = Snap(Math.Min(hi.Value - 0.5, hi.Value / step));
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
                L = Snap(Math.Max(lo.Value + 0.5, lo.Value * step));
                if (Ok(L))
                {
                    hi = L;
                    break;
                }
                lo = L;
            }
        }
        hi ??= lMax;
        while (lo is not null && hi.Value - lo.Value > Math.Max(0.5, 0.004 * hi.Value))
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
        return (hi.Value, Math.Round(fitHi.B, 2));
    }

    /// <summary>Size the hull, then lay it out and balance it. Returns the internal (layout, result, sized design).</summary>
    static (Layout Lay, Navarch.Result R, Design Sized) Solve(Design design, int iterations = 6, double? hint = null)
    {
        var (L, B) = Size(design, hint);
        double lMax = Styles.Get(design).Sizing.Length.Max, bMax = Styles.Get(design).Sizing.BeamMax;
        Layout lay = null!;
        Navarch.Result r = null!;
        Design sized = null!;
        for (int i = 0; i < 6; i++)
        {
            (lay, r, sized) = Balance(WithHull(design, L, B), iterations);
            if (lay.Short.Count == 0 || (L >= lMax && B >= bMax))
                break;
            if (lay.Short.Contains("beam"))
                B = Math.Min(bMax, Math.Round(B * 1.03, 2));
            if (lay.Short.Contains("length"))
                L = Math.Min(lMax, Math.Max(L + 0.5, (long)Math.Round(L * 1.01 * 2) / 2.0));
        }
        if (lay.Short.Contains("length") && L >= lMax && lay.Errors.Count == 0)
            lay.Errors.Add($"Not everything fits even on the longest hull ({lMax:N0} m). Carry less.");
        if (lay.Short.Count == 0)
            (lay, r, sized) = SpreadEnds(sized, lay, r, iterations);
        return (lay, r, sized);
    }

    /// <summary>Give the ends as much of the spare length as they take without the layout faring worse.</summary>
    static (Layout, Navarch.Result, Design) SpreadEnds(Design design, Layout lay, Navarch.Result r, int iterations = 6)
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
    public static (Layout Lay, Navarch.Result R, Design Design) Balance(Design design, int iterations = 6, double spread = 0.0)
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
            double moment = r.Weights.Select(w => w.W * (w.X - r.Lcb)).Sum();
            double movable = r.Weights.Where(w => !(w.Group is "hull" or "misc")).Select(w => w.W).Sum();
            if (best is null || Math.Abs(moment) < best.Value.M)
                best = (Math.Abs(moment), shift);
            var keep = bracket.Where(b => (b.Moment > 0) != (moment > 0)).ToList();
            bracket = keep.Skip(Math.Max(0, keep.Count - 1)).ToList();
            bracket.Add((shift, moment));
            double newShift = bracket.Count == 2
                ? (bracket[0].Shift + bracket[1].Shift) / 2
                : Math.Max(lay.ShiftRange.Lo, Math.Min(lay.ShiftRange.Hi, shift - moment / movable));
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
        var plan = lay.Geo.Plant;
        var fp = lay.Geo.FunnelPlan;
        var extra = PyDict.Of(
            ("machinery_length_m", Math.Round(plan?.Segments.Sum(s => s.Len) ?? 0.0, 1)),
            ("boiler_rooms", (long)lay.Compartments.Count(c => c.Kind == "boiler_room")),
            ("engine_rooms", (long)lay.Compartments.Count(c => c.Kind == "engine_room")),
            ("rows", plan?.Space.Rows), ("protrusion_m", Math.Round(plan?.Space.Protrusion ?? 0.0, 2)),
            ("space_m", PyDict.Of(("width", Math.Round(plan?.Width ?? 0.0, 2)), ("height", Math.Round(plan?.Height ?? 0.0, 2)))),
            ("wing_bunkers_t", (long)Math.Round(plan?.WingT ?? 0.0)), ("end_bunkers_m", Math.Round(plan?.EndM ?? 0.0, 1)),
            ("funnels", (long)lay.Funnels.Count), ("funnel_gas_area_m2", Math.Round(fp?.Area ?? 0.0, 1)),
            ("funnel_gas_velocity_m_s", Math.Round(fp?.Velocity ?? 0.0, 1)),
            ("smoke_reach_m", lay.Funnels.Count > 0 ? Math.Round(Powerplant.SmokeReach(r.Plant, r.PowerShp), 1) : 0.0));
        return Powerplant.Published(r.Plant, r.PowerShp, extra);
    }

    /// <summary>The hull structure and its girder amidships, for the damage model.</summary>
    static PyDict HullReport(Design design, Navarch.Result r)
    {
        var h = r.Hull;
        if (!h.PlateModel)
            return PyDict.Of(("structure_t", Math.Round(h.T)));
        return PyDict.Of(("construction", HullWeight.ConstructionOf(design).Name), ("structure_t", Math.Round(h.T)),
            ("min_gauge_t", Math.Round(h.MinGaugeT!.Value)), ("strength_t", Math.Round(h.StrengthT!.Value)),
            ("plate_min_mm", Math.Round(h.TMinMm!.Value, 1)), ("plate_strength_mm", Math.Round(h.TStrMm!.Value, 1)),
            ("shell_plating_t", Math.Round(h.ShellT)),
            ("girder", PyDict.Of(("allowable_stress_mpa", Math.Round(h.StressMpa!.Value, 1)), ("required_m4", Math.Round(h.IReqM4!.Value, 2)),
                ("plating_m4", Math.Round(h.IPlatingM4!.Value, 2)), ("armour_decks_m4", Math.Round(h.IArmourM4!.Value, 2)))));
    }

    /// <summary>design: the player's input (echoed in "inputs"); sized: the same with the hull the designer chose.</summary>
    static PyDict ReportDict(Design design, Layout lay, Navarch.Result r, Design sized)
    {
        var results = PyDict.Of(("length_m", sized.HullLength), ("beam_m", sized.HullBeam), ("block_coefficient", sized.BlockCoefficient),
            ("standard_displacement_t", (long)Math.Round(r.Std)), ("full_displacement_t", (long)Math.Round(r.Full)), ("draught_m", Math.Round(r.Draught, 2)),
            ("depth_m", Math.Round(r.Depth, 2)), ("freeboard_m", Math.Round(r.Freeboard, 2)), ("power_shp", Math.Round(r.PowerShp / 100.0) * 100),
            ("fuel_t", (long)Math.Round(r.Fuel)), ("crew", lay.Crew!.Complement), ("gm_full_m", Math.Round(r.GmFull, 2)),
            ("gm_light_m", Math.Round(r.GmLight, 2)), ("roll_period_s", Math.Round(r.RollS, 1)));
        if (r.Wind is { } wind)
            results.Update(("windage_m2", (long)Math.Round(wind.AreaM2)), ("gale_heel_deg", Math.Round(wind.HeelDeg, 1)),
                ("gale_heel_condition", wind.Condition), ("deck_edge_deg", Math.Round(wind.DeckEdgeDeg, 1)),
                ("deck_edge_wind_kn", (long)Math.Round(wind.DeckEdgeWindKn)));
        results.Update(("trim_m", Math.Round(r.TrimM, 2)), ("lcg_m", Math.Round(r.Lcg, 2)), ("lcb_m", Math.Round(r.Lcb, 2)),
            ("layout_shift_m", Math.Round(lay.Geo.Shift, 2)));
        results.Update(Styles.Get(design).Results(sized, lay, r));
        var report = PyDict.Of(("id", design.Id), ("name", design.Name ?? design.Id), ("valid", lay.Errors.Count == 0 && r.Errors.Count == 0),
            ("errors", lay.Errors.Concat(r.Errors).Cast<object?>().ToList()), ("warnings", lay.Warnings.Concat(r.Warnings).Cast<object?>().ToList()),
            ("inputs", design), ("results", results), ("plant", PlantReport(lay, r)), ("hull", HullReport(design, r)), ("crew", lay.Crew),
            ("fire_control", FireControl.Report(lay, r.Freeboard)));
        if (lay.Geo.Bridge != null)
            report["bridge"] = BridgeReport(lay, r.Freeboard);
        var groups = r.Groups.OrderBy(kv => -kv.Value).ToList();
        report["weight_groups_t"] = PyDict.Of(groups.Select(kv => (kv.Key, (object?)(long)Math.Round(kv.Value))).ToArray());
        report["weights"] = r.Weights.Select(w => (object?)PyDict.Of(("name", w.Name), ("group", w.Group), ("t", Math.Round(w.W, 1)),
            ("x", Math.Round(w.X, 2)), ("z", Math.Round(w.Z!.Value, 2)))).ToList();
        return report;
    }

    /// <summary>The unarmoured plating: the hull's from its structure, the superstructure's from the design.</summary>
    static HullPlates HullPlating(Layout lay, Design design, Navarch.Result res)
    {
        var sup = design.Superstructure;
        var hp = HullWeight.PlatingOf(design);
        return HullWeight.Plates(res.Hull, Layout.OwnPlateMm(lay), lay.Hull.L, hp.ShellMm, hp.Material, sup?.PlatingMm ?? 0.0,
            sup?.ControlMm ?? 0.0, hp.DeckWoodMm);
    }

    /// <summary>What the solved, laid-out ship is inside, beyond the layout.</summary>
    public sealed record Interior(ArmourLayout Armour, HullForm Form, PyDict Subdivision, HullPlates Plating, List<string> Planked,
        PyDict Hydrostatics, PyDict Propulsion, Dictionary<(string Kind, string Id), long> BattleCrew);

    static Interior InteriorOf(Layout lay, Design design, Navarch.Result r)
    {
        double D = r.Depth, T = r.Draught;
        var ag = r.Armour;
        double cb = design.BlockCoefficient;
        var gear = Propulsion.Gear(lay, design, r);
        var form = new HullForm(lay.Hull, cb, Geometry.Cwp(cb), T, D, Navarch.Froude(design.SpeedKn!.Value, lay.Hull.L), gear, r.Lcb);
        var sub = Subdivision.Build(lay, design, r, ag, ag.Armoured, form);
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
        double eye = deckM + b.Floor + BRIDGE_EYE;
        return PyDict.Of(("level", b.Level), ("tower_levels", b.Tower), ("eye_height_m", Math.Round(eye, 2)),
            ("horizon_km", Math.Round(FireControl.HorizonKm(eye), 1)), ("sees_over_turrets", b.Level >= b.Need),
            ("level_to_see_over_turrets", b.Need));
    }

    /// <summary>The static height-map columns, lowest first.</summary>
    static List<PyDict> HeightColumns(Layout lay, double deckM)
    {
        var items = new List<PyDict> { PyDict.Of(("top", deckM), ("shape", "hull")) };
        foreach (var dk in lay.Decks.Concat(lay.Sponsons))
            items.Add(PyDict.Of(("top", deckM + dk.Top), ("shape", "polygon"), ("points", dk.Points)));
        foreach (var ht in lay.Spec.Hatches ?? [])
            items.Add(PyDict.Of(("top", deckM + 1.2), ("shape", "rect"), ("x", ht.X - ht.L / 2), ("y", ht.Y - ht.W / 2), ("w", ht.L), ("h", ht.W)));
        foreach (var cr in lay.Spec.Cranes ?? [])
            items.Add(PyDict.Of(("top", deckM + cr.Top), ("shape", "circle"), ("cx", cr.X), ("cy", cr.Y), ("r", cr.R)));
        foreach (var m in lay.Mounts)
            if (m.T.HasBarbette && m.Base > 0.5)
                items.Add(PyDict.Of(("top", deckM + m.Base), ("shape", "circle"), ("cx", m.X), ("cy", m.Y), ("r", m.T.R * 0.95)));
        foreach (var b in lay.Blocks)
            items.Add(PyDict.Of(("top", deckM + b.TopZ), ("shape", "polygon"), ("points", Geometry.BlockOutline(b))));
        foreach (var a in lay.Aa)
            items.Add(PyDict.Of(("top", deckM + a.Base + 2.0), ("shape", "circle"), ("cx", a.X), ("cy", a.Y), ("r", Geometry.AA_CFG[a.Type].R * 0.8)));
        foreach (var bt in lay.Spec.Boats ?? [])
            items.Add(PyDict.Of(("top", deckM + (bt.Top ?? Layout.LEVEL_H + 1.5)), ("shape", "ellipse"), ("cx", bt.X), ("cy", bt.Y),
                ("rx", bt.L / 2), ("ry", bt.W / 2)));
        foreach (var fn in lay.Funnels)
        {
            var pts = Geometry.RrectPolygon(fn.X - fn.L / 2, fn.Y - fn.W / 2, fn.X + fn.L / 2, fn.Y + fn.W / 2, fn.W / 2, fn.W / 2);
            items.Add(PyDict.Of(("top", deckM + lay.FunTop), ("shape", "polygon"), ("points", pts)));
        }
        foreach (var m in lay.Spec.Masts)
            items.Add(PyDict.Of(("top", deckM + (m.Top ?? lay.FunTop + MAST_ABOVE_FUNNEL)), ("shape", "circle"), ("cx", m.X), ("cy", m.Y ?? 0.0),
                ("r", 0.7)));
        return items.OrderBy(it => it.F("top")).ToList();
    }

    /// <summary>Design the ship: the published, plain-data result. hint: the hull length of a similar earlier build.</summary>
    public static PyDict Build(Design design, double? hint = null)
    {
        var (lay, r, sized) = Solve(design, hint: hint);
        double deckM = Math.Max(r.Freeboard, 0.1);
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
                    var o = PyDict.Of(("id", m.Id), ("kind", m.Kind), ("rest", m.Rest), ("arcs", m.Arcs), ("traverse", m.Traverse), ("top", m.Top));
                    if (m.Casemate)
                        o["mount"] = "casemate";
                    return (object?)o;
                }).ToList()),
                ("columns", HeightColumns(lay, deckM).Cast<object?>().ToList()),
                ("summary", Styles.Get(design).Summary(sized, lay, r).Cast<object?>().ToList()))));
    }
}

/// <summary>looks.validate: the design's look (navy, era). The rest of looks.py is the renderer's (Step 4).</summary>
public static class Looks
{
    static readonly string[] NAVIES = ["generic", "brooklyn", "kure", "portsmouth", "kiel", "la_spezia", "toulon"];
    static readonly string[] ERAS = ["victorian", "great_war", "treaty", "wwii", "cold_war"];

    public static List<string> Validate(Design design)
    {
        var lk = design.Look ?? new LookInput();
        var extra = lk.Extra.KeysOrEmpty().Order(StringComparer.Ordinal).ToList();
        var errs = extra.Count > 0 ? new List<string> { $"look has unknown keys: {string.Join(", ", extra)}" } : [];
        string navy = lk.Navy ?? "generic", era = lk.Era ?? "wwii";
        if (!NAVIES.Contains(navy))
            errs.Add($"look.navy = {Style.Quote(navy)}: use {string.Join(", ", NAVIES)}");
        if (!ERAS.Contains(era))
            errs.Add($"look.era = {Style.Quote(era)}: use {string.Join(", ", ERAS)}");
        return errs;
    }
}
