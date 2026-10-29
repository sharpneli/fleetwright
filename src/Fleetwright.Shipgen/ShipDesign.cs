namespace Fleetwright.Shipgen;

/// <summary>The design side: a player's design in, the designed ship out as plain data. No drawing. Validate lists
/// the input errors (none: Build can run); Build returns the ship (design, report, hitboxes, render).</summary>
public static class ShipDesign
{
    /// <summary>The generator's version: bumped whenever its output changes on purpose (each `golden-update` commit), so
    /// anything compiled from a build (a ShipClass, a cache entry) knows it's stale.</summary>
    public const int Version = 50;

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

    /// <summary>The narrowest beam, at least bLo, on which a hull of length L carrying `weights` (placed, from a layout;
    /// null: the rough estimate) keeps the style's GM and draught: the root of the margin, found to half a centimetre, so
    /// the beam moves smoothly with what the ship carries. guess: where to start looking (the beam last time).</summary>
    static double BeamFor(Design design, double L, double bLo, double guess, List<Weight>? weights, Geo? geo)
    {
        var size = Styles.Get(design).Sizing;
        double bMax = size.BeamMax;
        bLo = Math.Min(bLo, bMax);

        double Margin(double b)
        {
            var r = Navarch.Solve(WithHull(design, L, b), weights, geo);
            return Math.Min(r.GmFull - size.GmFrac * b, size.Tb * b - r.Draught);
        }

        double lo = bLo, fLo = Margin(lo);
        if (fLo >= 0)
            return lo;
        // a bracket from the guess up, then Illinois regula falsi between the last beam short and the first enough
        double hi = Math.Max(lo, Math.Min(bMax, guess * 1.005)), fHi = hi > lo ? Margin(hi) : fLo;
        while (fHi < 0)
        {
            if (hi >= bMax)
                return bMax;
            (lo, fLo) = (hi, fHi);
            hi = Math.Min(bMax, hi * 1.06);
            fHi = Margin(hi);
        }
        int side = 0;
        for (int it = 0; it < 40 && hi - lo > 0.005; it++)
        {
            double b = (lo * fHi - hi * fLo) / (fHi - fLo);
            if (!(lo < b && b < hi))
                b = (lo + hi) / 2;
            double f = Margin(b);
            if (f >= 0)
            {
                (hi, fHi) = (b, f);
                if (side > 0)
                    fLo /= 2;
                side = 1;
            }
            else
            {
                (lo, fLo) = (b, f);
                if (side < 0)
                    fHi /= 2;
                side = -1;
            }
        }
        return hi;
    }

    /// <summary>The style's layout for the solved weights r, then crewed (a plan isn't: it's only for sizing).</summary>
    static Layout LayOut(Design design, Navarch.Result r, double shift, double midExtra, bool plan)
    {
        var style = Styles.Get(design);
        var lay = style.BuildLayout(design, r, shift, midExtra, plan);
        if (!plan)
        {
            SubmergedTubes.Plan(lay, design, r);
            Crew.Apply(lay, design, r, style);
        }
        return lay;
    }

    /// <summary>Size the hull, lay it out and balance it: (layout, result, sized design).
    /// <para>The hull is as long as the longer of what its speed asks (MinLength, from the displacement) and what its
    /// contents need, and as wide as its stability and draught ask (BeamFor) and its contents need. Each pass lays the
    /// ship out on the hull so far and reads those needs off it. What the contents need only ever grows: the length
    /// steps up by the middle's measured shortfall (which can't overshoot: the middle gains at most a metre per metre of
    /// hull), the middle's own extra length by what failed to fit in it. The length for speed and the beam are
    /// recomputed each pass. So a design that carries more doesn't come out smaller for the sizer's sake, and nothing
    /// depends on where a search happened to start.</para>
    /// <para>Plan passes (the style's layout as far as the turret groups and the middle) size the hull on rough
    /// weights; full passes then take the real weights, read what failed to fit, and move the arrangement fore and aft
    /// until the ship floats level.</para></summary>
    /// <summary>The hull (L, B) the weights ask for, the layout held as it is: at least lSpace long and bNeed wide, as long
    /// as its speed asks of its displacement (MinLength) and as wide as BeamFor. The length for speed is the root of
    /// MinLength(full(L)) - L, by the secant from the length so far (it rises slower than L, so there's one).</summary>
    static (double L, double B) Settle(Design design, double lSpace, double bNeed, double L, double B, List<Weight>? weights, Geo? geo)
    {
        var size = Styles.Get(design).Sizing;
        var (lMin, lMax) = size.Length;
        double speed = design.SpeedKn!.Value;

        (double F, double B) At(double l)
        {
            double b = BeamFor(design, l, Math.Max(l / size.LbMax, bNeed), B, weights, geo);
            B = b;
            if (Trace)
                Console.Error.WriteLine($"   settle l {l:F2} bNeed {bNeed:F2} -> b {b:F2}");
            return (size.Slender ? MinLength(Navarch.Solve(WithHull(design, l, b), weights, geo).Full, speed) - l : -1.0, b);
        }

        double l0 = Math.Clamp(Math.Max(lSpace, L), lMin, lMax);
        var (f0, b0) = At(l0);
        if (f0 <= 0 && l0 <= Math.Max(lSpace, lMin) + 1e-9)
            return (l0, b0);   // as long as its contents need, and that's long enough for its speed
        if (f0 <= 0)
        {
            // longer than it needs to be (an earlier pass's weights): from the contents' length up
            l0 = Math.Clamp(Math.Max(lSpace, lMin), lMin, lMax);
            (f0, b0) = At(l0);
            if (f0 <= 0)
                return (l0, b0);
        }
        double l1 = Math.Clamp(l0 + f0, lMin, lMax);
        var (f1, b1) = At(l1);
        for (int it = 0; it < 30 && Math.Abs(f1) > 0.005 && l1 < lMax; it++)
        {
            double l2 = f1 != f0 ? l1 - f1 * (l1 - l0) / (f1 - f0) : l1 + f1;
            l2 = Math.Clamp(Math.Max(l2, lSpace), lMin, lMax);
            (l0, f0) = (l1, f1);
            l1 = l2;
            (f1, b1) = At(l1);
        }
        return (l1, b1);
    }

    /// <summary>Print each sizing pass to stderr (SHIPGEN_TRACE=1).</summary>
    static readonly bool Trace = Environment.GetEnvironmentVariable("SHIPGEN_TRACE") == "1";

    static (Layout Lay, Navarch.Result R, Design Sized) Solve(Design design)
    {
        var style = Styles.Get(design);
        var size = style.Sizing;
        var (lMin, lMax) = size.Length;
        double bMax = size.BeamMax, speed = design.SpeedKn!.Value;

        // a start below the answer: the rough ship's displacement, and a length a little under any MinLength for it
        double L = 100.0;
        for (int i = 0; i < 3; i++)
            L = Math.Clamp(5.0 * Math.Pow(Navarch.Solve(WithHull(design, L, L / 7.5)).Full, 1.0 / 3), lMin, lMax);
        L = Math.Clamp(0.9 * L, lMin, lMax);
        double B = Math.Min(bMax, L / size.LbMax);
        double lSpace = lMin, midExtra = 0.0, bNeed = 0.0, shift = 0.0;
        (double B, double Need, bool Plan)? lastNeed = null;
        (double L, double B)? prev = null;
        (double Shift, double Moment)? lastTrim = null;
        List<Weight>? weights = null;
        Geo? geo = null;
        bool plan = true;
        int full = 0, vagueSteps = 0, midSteps = 0;   // vague: short for want of something that doesn't say how much; at most a few 1% steps
        Layout lay = null!;
        Navarch.Result r = null!;
        Design d = null!;
        for (int pass = 0; pass < 80; pass++)
        {
            d = WithHull(design, L, B);
            r = Navarch.Solve(d, weights, geo);
            lay = LayOut(d, r, shift, midExtra, plan);
            if (!plan)
            {
                r = Navarch.Solve(d, lay.Weights, lay.Geo);
                (weights, geo) = (lay.Weights, lay.Geo);
                full++;
            }
            else
                geo = lay.Geo;

            // what the contents need of the hull: these only ever grow
            bNeed = Math.Max(bNeed, lay.NeedB);
            if (lay.NeedB > B + 0.01)
            {
                // a part's beam often grows with the beam (a deckhouse a share of it wide): aim for where the two meet,
                // by the secant through this pass's need and the last one's
                double g = lay.NeedB - B;
                if (lastNeed is var (pb, pn, pp) && pp == plan && pb < B && pn - pb > g)
                    bNeed = Math.Max(bNeed, B + Math.Min(3 * g, g * (B - pb) / (pn - pb - g)) + 0.005);
                lastNeed = (B, lay.NeedB, plan);
            }
            bool wideEnough = lay.NeedB <= B + 0.01;
            // short of beam for a part that doesn't say by how much (a director without a roof): as likely the length,
            // so a little more of both, once the hull has settled (below)
            bool vagueBeam = lay.Short.Contains("beam") && wideEnough;
            var (L2, B2) = Settle(design, lSpace, bNeed, L, B, weights, geo);
            // what the layout asks of the length, read off a hull as wide as this length gets (on one much narrower the
            // turrets stand further in on the taper, a hangar holds fewer aircraft, and what's short overstates what a
            // longer, and so wider, hull needs) ...
            // ... and as long as its weights make it: a need read off a hull still growing for its speed overstates what
            // the grown hull needs, and stays (needs only grow)
            bool vague = false, settledBeam = wideEnough && Math.Abs(B2 - B) < 0.01 * B && L2 - L < 0.05;
            if (settledBeam)
            {
                double l0 = lSpace;
                if (lay.Deficit is double def && def > 0)
                    lSpace = Math.Max(lSpace, L + def + 0.05);
                if (lay.NeedL > L)   // a need met from below gets there only in the limit: a few centimetres over
                    lSpace = Math.Max(lSpace, lay.NeedL + 0.05);
                vague = (lay.Short.Contains("length") && lay.Deficit is not > 0 && lay.NeedL <= L && lay.NeedMid <= 0 || vagueBeam)
                        && vagueSteps < 5;
                if (vague)
                    vagueSteps++;
                if (vague)
                    lSpace = Math.Max(lSpace, L * 1.01);
                if (vague && vagueBeam)
                    bNeed = Math.Max(bNeed, B * 1.01);
                if (lay.NeedMid > 0 && midSteps < 4)   // a few steps: if those don't fit it, more length in the middle won't
                {
                    midExtra += lay.NeedMid;
                    midSteps++;
                }
                if (lSpace > l0 || vague)
                    (L2, B2) = Settle(design, lSpace, bNeed, L, B, weights, geo);
            }

            // the arrangement fore and aft for trim: the moment moves with the shift by the weight that moves with it
            double shift2 = shift;
            if (!plan)
            {
                double moment = 0.0, movable = 0.0;
                foreach (var w in r.Weights)
                {
                    moment += w.W * (w.X - r.Lcb);
                    if (!(w.Group is "hull" or "misc"))
                        movable += w.W;
                }
                shift2 = Math.Clamp(shift - moment / movable, lay.ShiftRange.Lo, lay.ShiftRange.Hi);
                // once the moment has changed sign, halve the bracket: the layout's own steps can make the plain update
                // jump back and forth
                if (lastTrim is var (ps, pm) && pm * moment < 0)
                    shift2 = (shift + ps) / 2;
                lastTrim = (shift, moment);
            }
            bool atMax = L >= lMax && L2 >= lMax;
            // to a few centimetres: closer, a layout's own steps (a mount a quarter metre along) can flip it back and forth
            // short of something more hull could still give (what has had its few steps stays an error)
            bool vagueLeft = (lay.Short.Contains("length") && lay.Deficit is not > 0 && lay.NeedL <= L && lay.NeedMid <= 0 || vagueBeam)
                             && vagueSteps < 5;
            bool shortOf = lay.Short.Count > 0 && !atMax && (lay.NeedL > L + 0.01 || lay.Deficit > 0.01 || lay.NeedB > B + 0.01
                                                              || lay.NeedMid > 0 && midSteps < 4 || vagueLeft);
            bool settled = Math.Abs(L2 - L) < 0.05 && Math.Abs(B2 - B) < 0.02 && settledBeam && !shortOf;
            if (Trace)
                Console.Error.WriteLine($"{(plan ? "plan" : "full")} L {L:F2}->{L2:F2} (space {lSpace:F2}) B {B:F2}->{B2:F2} " +
                                        $"mid+ {midExtra:F1} shift {shift:F2}->{shift2:F2} std {r.Std:F0} short [{string.Join(",", lay.Short)}]" +
                                        (lay.Short.Count > 0 ? " " + string.Join(" | ", lay.Errors) : ""));
            if ((settled || pass >= 40) && plan)
            {
                plan = false;
                continue;
            }
            // a layout can flip between two states a step apart (a mount on one side of a line or the other): stop on
            // the bigger hull, whichever pass the flip was seen in
            bool flip = !plan && prev is var (pL, pB) && Math.Abs(L2 - pL) < 0.01 && Math.Abs(B2 - pB) < 0.01 && !shortOf
                        && Math.Abs(L2 - L) < 0.5 && L >= L2;
            if (settled && Math.Abs(shift2 - shift) < 0.05 || flip || !plan && full >= 16)
                break;
            prev = plan ? null : (L, B);
            (L, B, shift) = (L2, B2, shift2);
        }
        if (lay.Short.Contains("length") && L >= lMax && lay.Errors.Count == 0)
            lay.Errors.Add($"Not everything fits even on the longest hull ({lMax:N0} m). Carry less.");
        Arcs.AssignArcs(lay);
        FireControl.AssignSmoke(lay, r);
        return (lay, r, d);
    }

    /// <summary>The plant's static numbers for the game and how it sits in the hull.</summary>
    static PlantReport PlantReport(Layout lay, Navarch.Result r)
    {
        var plan = lay.Geo.Plant;
        var fp = lay.Geo.FunnelPlan;
        var o = Powerplant.Published(r.Plant, r.PowerShp);
        o.MachineryLengthM = Math.Round(plan?.Segments.Sum(s => s.Len) ?? 0.0, 1);
        o.BoilerRooms = lay.Compartments.Count(c => c.Kind == RoomKind.BoilerRoom);
        o.EngineRooms = lay.Compartments.Count(c => c.Kind == RoomKind.EngineRoom);
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
        var c = HullWeight.ConstructionOf(design);
        if (!h.PlateModel)
            return new HullReport { Shell = c.Shell, Internals = c.Internals, StructureT = Math.Round(h.T) };
        return new HullReport
        {
            Construction = c.Name, Shell = c.Shell, Internals = c.Internals, StructureT = Math.Round(h.T), MinGaugeT = Math.Round(h.MinGaugeT!.Value),
            StrengthT = Math.Round(h.StrengthT!.Value), PlateMinMm = Math.Round(h.TMinMm!.Value, 1), PlateStrengthMm = Math.Round(h.TStrMm!.Value, 1),
            ShellPlatingT = Math.Round(h.ShellT),
            Girder = new Girder(Math.Round(h.StressMpa!.Value, 1), Math.Round(h.IReqM4!.Value, 2), Math.Round(h.IPlatingM4!.Value, 2),
                Math.Round(h.IArmourM4!.Value, 2)),
        };
    }

    /// <summary>design: the player's input (echoed in Inputs); sized: the same with the hull the designer chose.</summary>
    static Report ReportOf(Design design, Layout lay, Navarch.Result r, Design sized, Interior inner)
    {
        // the subdivision check (protection-plan.md H7)
        var sub = inner.Subdivision;
        var (standard, worst) = Floodable.Check(sub.Sections, sub.Cells, inner.Hydrostatics, lay.Hull.L, -(r.Depth - r.Draught));
        string? flooded = Floodable.Warning(sub, standard, worst, Styles.Get(design).CompartmentStandard);
        var results = new Results
        {
            LengthM = sized.HullLength, BeamM = sized.HullBeam, BlockCoefficient = sized.BlockCoefficient,
            StandardDisplacementT = (long)Math.Round(r.Std), FullDisplacementT = (long)Math.Round(r.Full), DraughtM = Math.Round(r.Draught, 2),
            DepthM = Math.Round(r.Depth, 2), FreeboardM = Math.Round(r.Freeboard, 2), PowerShp = Math.Round(r.PowerShp / 100.0) * 100,
            FuelT = (long)Math.Round(r.Fuel), Crew = lay.Crew!.Complement, GmFullM = Math.Round(r.GmFull, 2), GmLightM = Math.Round(r.GmLight, 2),
            RollPeriodS = Math.Round(r.RollS, 1), TrimM = Math.Round(r.TrimM, 2), LcgM = Math.Round(r.Lcg, 2), LcbM = Math.Round(r.Lcb, 2),
            LayoutShiftM = Math.Round(lay.Geo.Shift, 2), CompartmentStandard = standard,
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
        var dataWarnings = new List<string>();
        var shells = Ammunition.Resolve(design, dataWarnings);
        var materials = MaterialTemplates.Report(design, dataWarnings);
        return new Report
        {
            Id = design.Id, Name = design.Name ?? design.Id, Valid = lay.Errors.Count == 0 && r.Errors.Count == 0,
            Errors = [.. lay.Errors, .. r.Errors],
            Warnings = [.. DesignFormat.Warnings(design), .. lay.Warnings, .. r.Warnings, .. dataWarnings, .. flooded is null ? [] : new[] { flooded }],
            Inputs = design, Results = results, Shells = shells.Count > 0 ? shells : null,
            Materials = materials.Count > 0 ? materials : null,
            Summary = Styles.Get(design).Summary(sized, lay, r),
            Plant = PlantReport(lay, r), Hull = HullReport(design, r), Fittings = new FittingsReport(Fittings.GradeOf(design)), Crew = lay.Crew, FireControl = FireControl.Report(lay, r.Freeboard),
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
        Hydrostatics Hydrostatics, Train Propulsion, List<TubeRun> Tubes, Dictionary<(string Kind, string Id), long> BattleCrew);

    /// <summary>The laid-out ship's hull form, for the interior (and the layout steps that need its sections).</summary>
    public static HullForm FormOf(Layout lay, Design design, Navarch.Result r, Gear gear)
    {
        double cb = design.BlockCoefficient;
        return new HullForm(lay.Hull, cb, Geometry.Cwp(cb), r.Draught, r.Depth, Navarch.Froude(design.SpeedKn!.Value, lay.Hull.L), gear,
            r.Lcb, lay.Topside);
    }

    static Interior InteriorOf(Layout lay, Design design, Navarch.Result r)
    {
        double D = r.Depth, T = r.Draught;
        var ag = r.Armour;
        double cb = design.BlockCoefficient;
        var gear = Propulsion.Gear(lay, design, r);
        var form = FormOf(lay, design, r, gear);
        var sub = Subdivision.Build(lay, design, r, ag, ag.Armoured, form);
        var plating = HullPlating(lay, design, r);
        var planked = Subdivision.DeckPlates(sub, plating, design, lay);
        var hydro = Stability.Hydrostatics(form, r);
        var train = Propulsion.Build(lay, design, r, form, gear);
        Propulsion.Link(train, sub, D);
        var tubes = SubmergedTubes.Build(lay, form, D, sub);
        return new Interior(ag, form, sub, plating, planked, hydro, train, tubes, Crew.AssignBattleCrew(lay, sub));
    }

    const double BridgeEye = 1.7;

    /// <summary>The navigating bridge's view (warships).</summary>
    static BridgeReport BridgeReport(Layout lay, double deckM)
    {
        var b = lay.Geo.Bridge!;
        double eye = deckM + b.Floor + BridgeEye;
        return new BridgeReport(b.Level, b.Tower, Math.Round(eye, 2), Math.Round(FireControl.HorizonKm(eye), 1), b.Level >= b.Need, b.Need);
    }

    static bool SameSpans(List<(double X0, double X1, double HalfWidth, double Base, double Top)> a,
        List<(double X0, double X1, double HalfWidth, double Base, double Top)> b)
    {
        var (sa, sb) = (Armour.MagazineRuns(a), Armour.MagazineRuns(b));
        if (sa.Count != sb.Count)
            return false;
        for (int i = 0; i < sa.Count; i++)
            if (Math.Abs(sa[i].X0 - sb[i].X0) > 0.05 || Math.Abs(sa[i].X1 - sb[i].X1) > 0.05)
                return false;
        return true;
    }

    /// <summary>Design the ship.</summary>
    public static Ship Build(Design design)
    {
        var (lay, r, sized) = Solve(Ammunition.WithShellMasses(design));
        var inner = InteriorOf(lay, sized, r);
        // the armour that goes by the magazines (zones, the inner bulkhead's and the boxes' weights) was solved on Stow's
        // magazines; where the subdivision gave a room more of the hull (a short magazine takes its whole section), solve once
        // more on the rooms. The sections don't move with it: they stand where the rooms and the citadel put them
        if (Armour.GoesByMagazines(sized) && Subdivision.MagazineRooms(inner.Subdivision) is { Count: > 0 } rooms
            && !SameSpans(rooms, lay.Geo.MagazineBoxes ?? []))
        {
            lay.Geo.MagazineRooms = rooms;
            r = Navarch.Solve(sized, lay.Weights, lay.Geo);
            inner = InteriorOf(lay, sized, r);
        }
        return new Ship(design, ReportOf(design, lay, r, sized, inner), Hitbox.ExportHitboxes(lay, sized, r, inner), lay.Dressing);
    }
}
