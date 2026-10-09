namespace Fleetwright.Shipgen;

/// <summary>batteries: the ship's guns as data: reading the design's batteries, what a gun, mount, its magazine load
/// and a torpedo mount weigh, rounds per gun, and the first-pass armament estimate.</summary>
public static class Batteries
{
    public const double GUN_K = 1.9e-6, MOUNT_K = 2.2, TURRET_T_AVG = 0.65, SHELL_K = 1.83e-5, AMMO_MULT = 1.6;
    static readonly (double Cal, double Rounds)[] ROUNDS = [(127, 350), (152, 200), (203, 100)];
    const double MOUNT_FIXED_T = 4.0, TORP_MOUNT_T = 5.0, TORP_TUBE_T = 3.0, TORP_T = 1.6, TORP_FIXED_TUBE_T = 1.0;
    public static readonly IReadOnlyDictionary<string, double> AA_T =
        new Dictionary<string, double>(StringComparer.Ordinal) { ["quad40"] = 15.0, ["twin40"] = 7.0, ["single20"] = 1.0 };

    public static double GunTubeT(double calMm, double calLen) => GUN_K * Math.Pow(calMm, 3) * (calLen / 50.0);

    /// <summary>Rounds carried per gun: one curve, log-log between the points, flat beyond the ends.</summary>
    public static double RoundsPerGun(double calMm)
    {
        if (calMm <= ROUNDS[0].Cal)
            return ROUNDS[0].Rounds;
        for (int i = 0; i < ROUNDS.Length - 1; i++)
        {
            var (c0, r0) = ROUNDS[i];
            var (c1, r1) = ROUNDS[i + 1];
            if (calMm <= c1)
                return r0 * Math.Pow(r1 / r0, Math.Log(calMm / c0) / Math.Log(c1 / c0));
        }
        return ROUNDS[^1].Rounds;
    }

    /// <summary>Rounds per gun in the magazines for turret type t: the battery's rounds_per_gun, else the curve.</summary>
    public static double GunRounds(PyDict t) => t.Has("rounds_per_gun") ? t.F("rounds_per_gun") : RoundsPerGun(t.F("calibre_mm"));

    /// <summary>(turret incl. guns+armour, barbette armour, magazine/ammo) for one mount of type t.</summary>
    public static (double Turret, double Barbette, double Ammo) MountWeights(PyDict t, double armourMm, double depth, double level,
        double deck = 0.0)
    {
        double cal = t.F("calibre_mm"), cl = t.F("calibre_length"), n = t.F("barrels"), r = t.F("r");
        double guns = n * GunTubeT(cal, cl);
        double mech = MOUNT_K * guns + MOUNT_FIXED_T * Math.Min(1.0, Math.Pow(cal / 76.0, 3));
        double th = 0.42 * r;
        double area = 2 * Math.PI * 0.9 * r * th + 0.85 * Math.PI * r * r;
        double tAvg = TURRET_T_AVG * armourMm / 1000.0;
        double turret = guns + mech + area * tAvg * Weight.STEEL;
        double bh = 0.45 * depth + deck + level * Geometry.SuperfireStep(th);
        double barbette = Geometry.HasBarbette(t) ? 2 * Math.PI * 0.95 * r * bh * (0.8 * armourMm / 1000.0) * Weight.STEEL : 0.0;
        double ammo = n * GunRounds(t) * SHELL_K * Math.Pow(cal, 3) / 1000.0 * AMMO_MULT;
        return (turret, barbette, ammo);
    }

    public static double TorpedoWeight(double tubes, bool fixed_ = false)
    {
        if (fixed_)
            return tubes * (TORP_FIXED_TUBE_T + TORP_T);
        return TORP_MOUNT_T + tubes * (TORP_TUBE_T + TORP_T);
    }

    public const long SECONDARY_ARMOUR_MM = 25;

    /// <summary>A battery input as a list: one object or a list (anything tolerated, for the validators).</summary>
    public static List<object?> AsList(object? v) =>
        v is List<object?> l ? l : Py.Truthy(v) ? [v] : [];

    /// <summary>The main batteries, in the design's order.</summary>
    public static List<PyDict> MainBatteries(PyDict design) =>
        AsList(design.Get("main")).OfType<PyDict>().Where(b => b.Count > 0).ToList();

    /// <summary>The secondary batteries, in the design's order, with count, per_side and armour_mm filled in.</summary>
    public static List<PyDict> SecondaryBatteries(PyDict design)
    {
        var out_ = new List<PyDict>();
        foreach (var o in AsList(design.Get("secondary")))
        {
            var b = (PyDict)o!;
            object n = b.Has("count") ? b["count"]! : Mul2(b.Get("per_side", 0L));
            var nb = b.Copy();
            nb["count"] = n;
            nb["per_side"] = b.Has("per_side") ? b["per_side"] : FloorDiv2(n);
            nb["armour_mm"] = b.Has("armour_mm") ? b["armour_mm"] : SECONDARY_ARMOUR_MM;
            out_.Add(nb);
        }
        return out_;
    }

    static object Mul2(object? v) => v is double d ? 2 * d : (object)(2 * Py.ToLong(v));
    static object FloorDiv2(object? v) => v is double d ? Math.Floor(d / 2) : (object)(Py.ToLong(v) / 2);

    /// <summary>The k-th secondary battery's name: S, SB, SC, ...</summary>
    public static string BatteryPrefix(int k) => k == 0 ? "S" : k < 26 ? $"S{(char)('A' + k)}" : $"S{k + 1}-";

    /// <summary>How many turrets a main battery has: its end, midships and wing turrets (two to a pair).</summary>
    public static double BatteryTurrets(PyDict b) =>
        b.F("fore", 0) + b.F("aft", 0) + b.F("mid", 0) + 2 * b.F("wing", 0);

    /// <summary>First-pass armament estimate before the layout exists (all at x=0).</summary>
    public static List<Weight> RoughArmament(PyDict design, double D)
    {
        var out_ = new List<Weight>();
        var mains = MainBatteries(design);
        for (int k = 0; k < mains.Count; k++)
        {
            var m = mains[k];
            var (_, t) = Geometry.BatteryType(m);
            double n = BatteryTurrets(m);
            var (tw, bw, aw) = MountWeights(t, m.F("armour_mm", 0), D, 0);
            string sfx = k != 0 ? $" {k + 1}" : "";
            out_.Add(new Weight("Main battery" + sfx, "armament", n * tw, zRel: ZRel.Deck(2)));
            out_.Add(new Weight("Main barbettes" + sfx, "armour", n * bw, zRel: ZRel.Frac(0.75)));
            out_.Add(new Weight("Main magazines" + sfx, "armament", n * aw, zRel: ZRel.Frac(0.25)));
        }
        foreach (var s in SecondaryBatteries(design))
        {
            double n = s.F("count");
            if (n == 0)
                continue;
            var (_, t) = Geometry.BatteryType(s);
            var (tw, _, aw) = MountWeights(t, s.F("armour_mm"), D, 0);
            out_.Add(new Weight("Secondary battery", "armament", n * (tw + aw), zRel: ZRel.Deck(2)));
        }
        var tp = design.Get("torpedoes") as PyDict;
        if (tp != null && tp.Count > 0 && Py.Truthy(tp.Get("mounts", 0L)))
            out_.Add(new Weight("Torpedoes", "armament", tp.F("mounts") * TorpedoWeight(tp.F("tubes")), zRel: ZRel.Deck(1)));
        var aa = design.Get("aa", new PyDict()) as PyDict ?? new PyDict();
        double w = aa.F("heavy", 0) * AA_T["quad40"] + aa.F("light", 0) * AA_T["single20"];
        if (w != 0)
            out_.Add(new Weight("AA guns", "armament", w, zRel: ZRel.Deck(2)));
        return out_;
    }
}

/// <summary>stability: GM at full load and light, roll period, heel in a beam gale, trim, the full-load hydrostatics,
/// and the warnings on all of them.</summary>
public static class Stability
{
    public const double WIND_REF_MS = 26.0, WIND_PA_K = 0.746, WIND_HEEL_WARN = 16.0;

    public static double RollPeriod(double L, double B, double T, double gm)
    {
        if (gm <= 0 || T <= 0)
            return 0.0;
        double c = 0.373 + 0.023 * B / T - 0.043 * L / 100.0;
        return 2.0 * c * B / Math.Sqrt(gm);
    }

    /// <summary>The steady heel a beam gale gives, at full load and light, the worse of the two (empty without windage).</summary>
    public static PyDict WindHeel(double L, double B, double D, double cb, double full, double std, double gmFull,
        double gmLight, PyDict? windage)
    {
        if (windage is null)
            return new PyDict();
        PyDict? out_ = null;
        foreach (var (cond, disp, gm) in new[] { ("full load", full, gmFull), ("light", std, gmLight) })
        {
            double T = disp / (Weight.SEAWATER * L * B * cb);
            double fb = Math.Max(0.0, D - T);
            double aHull = 0.95 * L * fb;
            double area = aHull + windage.F("area_m2");
            double z = area != 0 ? (aHull * (T + fb / 2) + windage.F("area_m2") * (D + windage.F("z_m"))) / area : T;
            double arm = area * (z - T / 2) / (9.81 * disp * 1000.0);
            double edge = double.RadiansToDegrees(Math.Atan2(fb, B / 2));
            double heel = gm > 0 ? double.RadiansToDegrees(Math.Atan(WIND_PA_K * Math.Pow(WIND_REF_MS, 2) * arm / gm)) : 90.0;
            double pEdge = gm > 0 && arm > 0 ? Math.Tan(double.DegreesToRadians(edge)) * gm / arm : 0.0;
            var r = PyDict.Of(("condition", cond), ("heel_deg", heel), ("deck_edge_deg", edge),
                ("deck_edge_wind_kn", Math.Sqrt(pEdge / WIND_PA_K) / 0.5144), ("area_m2", area));
            if (out_ is null || heel > out_.F("heel_deg"))
                out_ = r;
        }
        return out_!;
    }

    /// <summary>The full-load hydrostatics a game needs to settle, trim and heel a flooded ship by added weight.</summary>
    public static PyDict Hydrostatics(HullForm form, Navarch.Result res)
    {
        double L = form.Hull.L, D = res.Depth, T = res.Draught, disp = res.Full;
        var (area, lcf, iL, iT) = form.Waterplane();
        double vol = disp / Weight.SEAWATER;
        double kg = res.Weights.Select(w => w.W * w.Z!.Value).Sum() / res.Weights.Select(w => w.W).Sum();
        double kb = 0.53 * T;
        double gmL = kb + iL / vol - kg;
        return PyDict.Of(("displacement_t", (long)Math.Round(disp)), ("volume_m3", (long)Math.Round(vol)), ("waterplane_m2", Math.Round(area, 1)),
            ("lcf", Math.Round(lcf, 3)), ("lcg", Math.Round(res.Lcg, 3)), ("lcb", Math.Round(res.Lcb, 3)), ("kg", Math.Round(kg - D, 2)),
            ("kb", Math.Round(kb - D, 2)), ("gm_t", Math.Round(res.GmFull, 3)), ("gm_l", Math.Round(gmL, 1)),
            ("i_t_m4", (long)Math.Round(iT)), ("i_l_m4", (long)Math.Round(iL)), ("tpc_t", Math.Round(Weight.SEAWATER * area / 100, 2)),
            ("mct_tm", Math.Round(disp * gmL / (100 * L), 1)));
    }

    /// <summary>GM (full load, and light), roll period, wind heel and trim of the solved ship, set on res.</summary>
    public static void Evaluate(Navarch.Result res, double L, double B, double cb, PyDict tun, PyDict? windage)
    {
        double Gm(double dispCase, bool includeFuel)
        {
            double T_ = dispCase / (Weight.SEAWATER * L * B * cb);
            var ws = res.Weights.Where(w => includeFuel || !(w.Group is "fuel" or "cargo")).ToList();
            double kg = ws.Select(w => w.W * w.Z!.Value).Sum() / ws.Select(w => w.W).Sum();
            double kb = 0.53 * T_;
            double cw = Geometry.Cwp(cb);
            double it = 0.0372 * Math.Pow(2 * cw + 1, 3) * L * Math.Pow(B, 3) / 12;
            double bm = it / (dispCase / Weight.SEAWATER);
            return kb + bm - kg;
        }

        res.GmFull = Gm(res.Full, true);
        res.GmLight = Gm(res.Std, false);
        res.RollS = RollPeriod(L, B, res.Draught, res.GmFull);
        res.Wind = WindHeel(L, B, res.Depth, cb, res.Full, res.Std, res.GmFull, res.GmLight, windage);
        res.Lcg = res.Weights.Select(w => w.W * w.X).Sum() / res.Full;
        res.Lcb = tun.F("lcb_frac") * L;
        double cw2 = Geometry.Cwp(cb);
        double bml = 0.0743 * Math.Pow(cw2, 2) * Math.Pow(L, 2) / (cb * res.Draught);
        res.TrimM = (res.Lcg - res.Lcb) * L / bml;
    }

    /// <summary>Stability and trim errors and warnings, appended to res.errors and res.warnings.</summary>
    public static void Checks(Navarch.Result res, double L, double B, PyDict tun)
    {
        double gmin = res.GmFull;
        if (gmin <= 0)
            res.Errors.Add($"Unstable: GM {gmin:F2} m. The ship would capsize. Lower the weight high up.");
        else if (gmin < 0.035 * B)
            res.Warnings.Add($"Top-heavy: GM {gmin:F2} m (want at least {0.035 * B:F2} m).");
        if (res.GmLight <= 0 && 0 < gmin)
            res.Warnings.Add($"Needs water ballast when low on fuel (light-condition GM {res.GmLight:F2} m).");
        if (res.GmFull > tun.F("gm_stiff_frac", 0.15) * B)
            res.Warnings.Add($"Very stiff: GM {res.GmFull:F2} m. Snappy roll, poor gun platform.");
        double trimTol = tun.F("trim_tol_frac", 0.01);
        string end = res.TrimM > 0 ? "bow" : "stern";
        if (Math.Abs(res.TrimM) > trimTol * L)
            res.Errors.Add($"Badly out of trim: {Math.Abs(res.TrimM):F1} m by the {end}.");
        else if (Math.Abs(res.TrimM) > tun.F("trim_warn_frac", 0.004) * L)
            res.Warnings.Add($"Trimmed {Math.Abs(res.TrimM):F1} m by the {end}.");
        var w = res.Wind;
        if (w.Count > 0 && w.F("heel_deg") > Math.Min(WIND_HEEL_WARN, 0.8 * w.F("deck_edge_deg")))
            res.Warnings.Add($"Heels {w.F("heel_deg"):F0}° in a beam gale ({WIND_REF_MS:F0} m/s, {Py.Str(w["condition"])}): too " +
                             $"much windage for its stability. The deck edge goes under at {w.F("deck_edge_deg"):F0}°, " +
                             $"in a {w.F("deck_edge_wind_kn"):F0} kn wind.");
    }
}
