namespace Fleetwright.Shipgen;

/// <summary>The design side: a player's design in, the designed ship out as plain data. No drawing. Validate lists
/// the input errors (none: Build can run); Build returns the ship (design, report, hitboxes, render).</summary>
public static class ShipDesign
{

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
        double root = Math.Pow(dispT / Weight.Seawater, 1.0 / 3);
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
    static PlantReport PlantReport(Layout lay, Navarch.Result r)
    {
        var plan = lay.Geo.Plant;
        var fp = lay.Geo.FunnelPlan;
        var o = Powerplant.Published(r.Plant, r.PowerShp);
        o.MachineryLengthM = Math.Round(plan?.Segments.Sum(s => s.Len) ?? 0.0, 1);
        o.BoilerRooms = lay.Compartments.Count(c => c.Kind == "boiler_room");
        o.EngineRooms = lay.Compartments.Count(c => c.Kind == "engine_room");
        o.Rows = plan?.Space.Rows;
        o.ProtrusionM = Math.Round(plan?.Space.Protrusion ?? 0.0, 2);
        o.SpaceM = new SpaceM(Math.Round(plan?.Width ?? 0.0, 2), Math.Round(plan?.Height ?? 0.0, 2));
        o.WingBunkersT = (long)Math.Round(plan?.WingT ?? 0.0);
        o.EndBunkersM = Math.Round(plan?.EndM ?? 0.0, 1);
        o.Funnels = lay.Funnels.Count;
        o.FunnelGasAreaM2 = Math.Round(fp?.Area ?? 0.0, 1);
        o.FunnelGasVelocityMS = Math.Round(fp?.Velocity ?? 0.0, 1);
        o.SmokeReachM = lay.Funnels.Count > 0 ? Math.Round(Powerplant.SmokeReach(r.Plant, r.PowerShp), 1) : 0.0;
        return o;
    }

    /// <summary>The hull structure and its girder amidships, for the damage model.</summary>
    static HullReport HullReport(Design design, Navarch.Result r)
    {
        var h = r.Hull;
        if (!h.PlateModel)
            return new HullReport { StructureT = Math.Round(h.T) };
        return new HullReport
        {
            Construction = HullWeight.ConstructionOf(design).Name, StructureT = Math.Round(h.T), MinGaugeT = Math.Round(h.MinGaugeT!.Value),
            StrengthT = Math.Round(h.StrengthT!.Value), PlateMinMm = Math.Round(h.TMinMm!.Value, 1), PlateStrengthMm = Math.Round(h.TStrMm!.Value, 1),
            ShellPlatingT = Math.Round(h.ShellT),
            Girder = new Girder(Math.Round(h.StressMpa!.Value, 1), Math.Round(h.IReqM4!.Value, 2), Math.Round(h.IPlatingM4!.Value, 2),
                Math.Round(h.IArmourM4!.Value, 2)),
        };
    }

    /// <summary>design: the player's input (echoed in Inputs); sized: the same with the hull the designer chose.</summary>
    static Report ReportOf(Design design, Layout lay, Navarch.Result r, Design sized)
    {
        var results = new Results
        {
            LengthM = sized.HullLength, BeamM = sized.HullBeam, BlockCoefficient = sized.BlockCoefficient,
            StandardDisplacementT = (long)Math.Round(r.Std), FullDisplacementT = (long)Math.Round(r.Full), DraughtM = Math.Round(r.Draught, 2),
            DepthM = Math.Round(r.Depth, 2), FreeboardM = Math.Round(r.Freeboard, 2), PowerShp = Math.Round(r.PowerShp / 100.0) * 100,
            FuelT = (long)Math.Round(r.Fuel), Crew = lay.Crew!.Complement, GmFullM = Math.Round(r.GmFull, 2), GmLightM = Math.Round(r.GmLight, 2),
            RollPeriodS = Math.Round(r.RollS, 1), TrimM = Math.Round(r.TrimM, 2), LcgM = Math.Round(r.Lcg, 2), LcbM = Math.Round(r.Lcb, 2),
            LayoutShiftM = Math.Round(lay.Geo.Shift, 2),
        };
        if (r.Wind is { } wind)
        {
            results.WindageM2 = (long)Math.Round(wind.AreaM2);
            results.GaleHeelDeg = Math.Round(wind.HeelDeg, 1);
            results.GaleHeelCondition = wind.Condition;
            results.DeckEdgeDeg = Math.Round(wind.DeckEdgeDeg, 1);
            results.DeckEdgeWindKn = (long)Math.Round(wind.DeckEdgeWindKn);
        }
        Styles.Get(design).AddResults(results, sized, lay, r);
        var groups = new OrderedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var (k, v) in r.Groups.OrderBy(kv => -kv.Value))
            groups[k] = (long)Math.Round(v);
        return new Report
        {
            Id = design.Id, Name = design.Name ?? design.Id, Valid = lay.Errors.Count == 0 && r.Errors.Count == 0,
            Errors = [.. lay.Errors, .. r.Errors], Warnings = [.. lay.Warnings, .. r.Warnings], Inputs = design, Results = results,
            Summary = Styles.Get(design).Summary(sized, lay, r),
            Plant = PlantReport(lay, r), Hull = HullReport(design, r), Crew = lay.Crew, FireControl = FireControl.Report(lay, r.Freeboard),
            Bridge = lay.Geo.Bridge != null ? BridgeReport(lay, r.Freeboard) : null, WeightGroupsT = groups,
            Weights = r.Weights.Select(w => new WeightReport(w.Name, w.Group, Math.Round(w.W, 1), Math.Round(w.X, 2), Math.Round(w.Z!.Value, 2))).ToList(),
        };
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
    public sealed record Interior(ArmourLayout Armour, HullForm Form, SubdivisionData Subdivision, HullPlates Plating, List<string> Planked,
        Hydrostatics Hydrostatics, Train Propulsion, Dictionary<(string Kind, string Id), long> BattleCrew);

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

    const double BridgeEye = 1.7;

    /// <summary>The navigating bridge's view (warships).</summary>
    static BridgeReport BridgeReport(Layout lay, double deckM)
    {
        var b = lay.Geo.Bridge!;
        double eye = deckM + b.Floor + BridgeEye;
        return new BridgeReport(b.Level, b.Tower, Math.Round(eye, 2), Math.Round(FireControl.HorizonKm(eye), 1), b.Level >= b.Need, b.Need);
    }

    /// <summary>Design the ship. hint: the hull length of a similar earlier build.</summary>
    public static Ship Build(Design design, double? hint = null)
    {
        var (lay, r, sized) = Solve(design, hint: hint);
        var inner = InteriorOf(lay, sized, r);
        return new Ship(design, ReportOf(design, lay, r, sized), Hitbox.ExportHitboxes(lay, sized, r, inner), lay.Dressing);
    }
}

/// <summary>looks.validate: the design's look (navy, era). The rest of looks.py is the renderer's (Step 4).</summary>
public static class Looks
{
    static readonly string[] Navies = ["generic", "brooklyn", "kure", "portsmouth", "kiel", "la_spezia", "toulon"];
    static readonly string[] Eras = ["victorian", "great_war", "treaty", "wwii", "cold_war"];

    public static List<string> Validate(Design design)
    {
        var lk = design.Look ?? new LookInput();
        var extra = lk.Extra.KeysOrEmpty().Order(StringComparer.Ordinal).ToList();
        var errs = extra.Count > 0 ? new List<string> { $"look has unknown keys: {string.Join(", ", extra)}" } : [];
        string navy = lk.Navy ?? "generic", era = lk.Era ?? "wwii";
        if (!Navies.Contains(navy))
            errs.Add($"look.navy = {Style.Quote(navy)}: use {string.Join(", ", Navies)}");
        if (!Eras.Contains(era))
            errs.Add($"look.era = {Style.Quote(era)}: use {string.Join(", ", Eras)}");
        return errs;
    }
}
