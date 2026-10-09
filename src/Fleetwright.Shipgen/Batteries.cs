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
    public static double GunRounds(TurretType t) => t.RoundsPerGun ?? RoundsPerGun(t.CalibreMm);

    /// <summary>(turret incl. guns+armour, barbette armour, magazine/ammo) for one mount of type t.</summary>
    public static (double Turret, double Barbette, double Ammo) MountWeights(TurretType t, double armourMm, double depth, double level,
        double deck = 0.0)
    {
        double cal = t.CalibreMm, cl = t.CalibreLength!.Value, n = t.Barrels, r = t.R;
        double guns = n * GunTubeT(cal, cl);
        double mech = MOUNT_K * guns + MOUNT_FIXED_T * Math.Min(1.0, Math.Pow(cal / 76.0, 3));
        double th = 0.42 * r;
        double area = 2 * Math.PI * 0.9 * r * th + 0.85 * Math.PI * r * r;
        double tAvg = TURRET_T_AVG * armourMm / 1000.0;
        double turret = guns + mech + area * tAvg * Weight.STEEL;
        double bh = 0.45 * depth + deck + level * Geometry.SuperfireStep(th);
        double barbette = t.HasBarbette ? 2 * Math.PI * 0.95 * r * bh * (0.8 * armourMm / 1000.0) * Weight.STEEL : 0.0;
        double ammo = n * GunRounds(t) * SHELL_K * Math.Pow(cal, 3) / 1000.0 * AMMO_MULT;
        return (turret, barbette, ammo);
    }

    public static double TorpedoWeight(double tubes, bool fixed_ = false)
    {
        if (fixed_)
            return tubes * (TORP_FIXED_TUBE_T + TORP_T);
        return TORP_MOUNT_T + tubes * (TORP_TUBE_T + TORP_T);
    }

    public const double SECONDARY_ARMOUR_MM = 25;

    /// <summary>The main batteries, in the design's order.</summary>
    public static List<BatteryInput> MainBatteries(Design design) => [.. design.MainBatteries];

    /// <summary>The secondary batteries, in the design's order, with count, per_side and armour_mm filled in.</summary>
    public static List<BatteryInput> SecondaryBatteries(Design design) =>
        (design.Secondary ?? []).Select(b => b with { Count = b.MountCount, PerSide = b.MountsPerSide, ArmourMm = b.ArmourMm ?? SECONDARY_ARMOUR_MM })
            .ToList();

    /// <summary>The k-th secondary battery's name: S, SB, SC, ...</summary>
    public static string BatteryPrefix(int k) => k == 0 ? "S" : k < 26 ? $"S{(char)('A' + k)}" : $"S{k + 1}-";

    /// <summary>First-pass armament estimate before the layout exists (all at x=0).</summary>
    public static List<Weight> RoughArmament(Design design, double D)
    {
        var out_ = new List<Weight>();
        var mains = MainBatteries(design);
        for (int k = 0; k < mains.Count; k++)
        {
            var m = mains[k];
            var (_, t) = Geometry.BatteryType(m);
            double n = m.Turrets;
            var (tw, bw, aw) = MountWeights(t, m.ArmourMm ?? 0, D, 0);
            string sfx = k != 0 ? $" {k + 1}" : "";
            out_.Add(new Weight("Main battery" + sfx, "armament", n * tw, zRel: ZRel.Deck(2)));
            out_.Add(new Weight("Main barbettes" + sfx, "armour", n * bw, zRel: ZRel.Frac(0.75)));
            out_.Add(new Weight("Main magazines" + sfx, "armament", n * aw, zRel: ZRel.Frac(0.25)));
        }
        foreach (var s in SecondaryBatteries(design))
        {
            double n = s.MountCount;
            if (n == 0)
                continue;
            var (_, t) = Geometry.BatteryType(s);
            var (tw, _, aw) = MountWeights(t, s.ArmourMm!.Value, D, 0);
            out_.Add(new Weight("Secondary battery", "armament", n * (tw + aw), zRel: ZRel.Deck(2)));
        }
        if (design.Torpedoes is { Mounts: > 0 } tp)
            out_.Add(new Weight("Torpedoes", "armament", tp.Mounts.Value * TorpedoWeight(tp.Tubes ?? 4), zRel: ZRel.Deck(1)));
        double w = (design.Aa?.Heavy ?? 0) * AA_T["quad40"] + (design.Aa?.Light ?? 0) * AA_T["single20"];
        if (w != 0)
            out_.Add(new Weight("AA guns", "armament", w, zRel: ZRel.Deck(2)));
        return out_;
    }
}

/// <summary>The steady heel a beam gale gives in the worse of the full-load and light conditions, the heel that puts
/// the deck edge under and the wind that does it, and the area the wind sees.</summary>
public sealed record WindHeel(string Condition, double HeelDeg, double DeckEdgeDeg, double DeckEdgeWindKn, double AreaM2);

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
    public static WindHeel? WindHeelOf(double L, double B, double D, double cb, double full, double std, double gmFull,
        double gmLight, Windage? windage)
    {
        if (windage is null)
            return null;
        WindHeel? out_ = null;
        foreach (var (cond, disp, gm) in new[] { ("full load", full, gmFull), ("light", std, gmLight) })
        {
            double T = disp / (Weight.SEAWATER * L * B * cb);
            double fb = Math.Max(0.0, D - T);
            double aHull = 0.95 * L * fb;
            double area = aHull + windage.AreaM2;
            double z = area != 0 ? (aHull * (T + fb / 2) + windage.AreaM2 * (D + windage.ZM)) / area : T;
            double arm = area * (z - T / 2) / (9.81 * disp * 1000.0);
            double edge = double.RadiansToDegrees(Math.Atan2(fb, B / 2));
            double heel = gm > 0 ? double.RadiansToDegrees(Math.Atan(WIND_PA_K * Math.Pow(WIND_REF_MS, 2) * arm / gm)) : 90.0;
            double pEdge = gm > 0 && arm > 0 ? Math.Tan(double.DegreesToRadians(edge)) * gm / arm : 0.0;
            var r = new WindHeel(cond, heel, edge, Math.Sqrt(pEdge / WIND_PA_K) / 0.5144, area);
            if (out_ is null || heel > out_.HeelDeg)
                out_ = r;
        }
        return out_!;
    }

    /// <summary>The full-load hydrostatics a game needs to settle, trim and heel a flooded ship by added weight.</summary>
    public static Hydrostatics Hydrostatics(HullForm form, Navarch.Result res)
    {
        double L = form.Hull.L, D = res.Depth, T = res.Draught, disp = res.Full;
        var (area, lcf, iL, iT) = form.Waterplane();
        double vol = disp / Weight.SEAWATER;
        double kg = res.Weights.Select(w => w.W * w.Z!.Value).Sum() / res.Weights.Select(w => w.W).Sum();
        double kb = 0.53 * T;
        double gmL = kb + iL / vol - kg;
        return new Hydrostatics
        {
            DisplacementT = (long)Math.Round(disp), VolumeM3 = (long)Math.Round(vol), WaterplaneM2 = Math.Round(area, 1), Lcf = Math.Round(lcf, 3),
            Lcg = Math.Round(res.Lcg, 3), Lcb = Math.Round(res.Lcb, 3), Kg = Math.Round(kg - D, 2), Kb = Math.Round(kb - D, 2),
            GmT = Math.Round(res.GmFull, 3), GmL = Math.Round(gmL, 1), ITM4 = (long)Math.Round(iT), ILM4 = (long)Math.Round(iL),
            TpcT = Math.Round(Weight.SEAWATER * area / 100, 2), MctTm = Math.Round(disp * gmL / (100 * L), 1),
        };
    }

    /// <summary>GM (full load, and light), roll period, wind heel and trim of the solved ship, set on res.</summary>
    public static void Evaluate(Navarch.Result res, double L, double B, double cb, Tuning tun, Windage? windage)
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
        res.Wind = WindHeelOf(L, B, res.Depth, cb, res.Full, res.Std, res.GmFull, res.GmLight, windage);
        res.Lcg = res.Weights.Select(w => w.W * w.X).Sum() / res.Full;
        res.Lcb = tun.LcbFrac * L;
        double cw2 = Geometry.Cwp(cb);
        double bml = 0.0743 * Math.Pow(cw2, 2) * Math.Pow(L, 2) / (cb * res.Draught);
        res.TrimM = (res.Lcg - res.Lcb) * L / bml;
    }

    /// <summary>Stability and trim errors and warnings, appended to res.errors and res.warnings.</summary>
    public static void Checks(Navarch.Result res, double L, double B, Tuning tun)
    {
        double gmin = res.GmFull;
        if (gmin <= 0)
            res.Errors.Add($"Unstable: GM {gmin:F2} m. The ship would capsize. Lower the weight high up.");
        else if (gmin < 0.035 * B)
            res.Warnings.Add($"Top-heavy: GM {gmin:F2} m (want at least {0.035 * B:F2} m).");
        if (res.GmLight <= 0 && 0 < gmin)
            res.Warnings.Add($"Needs water ballast when low on fuel (light-condition GM {res.GmLight:F2} m).");
        if (res.GmFull > tun.GmStiffFrac * B)
            res.Warnings.Add($"Very stiff: GM {res.GmFull:F2} m. Snappy roll, poor gun platform.");
        double trimTol = tun.TrimTolFrac;
        string end = res.TrimM > 0 ? "bow" : "stern";
        if (Math.Abs(res.TrimM) > trimTol * L)
            res.Errors.Add($"Badly out of trim: {Math.Abs(res.TrimM):F1} m by the {end}.");
        else if (Math.Abs(res.TrimM) > tun.TrimWarnFrac * L)
            res.Warnings.Add($"Trimmed {Math.Abs(res.TrimM):F1} m by the {end}.");
        var w = res.Wind;
        if (w != null && w.HeelDeg > Math.Min(WIND_HEEL_WARN, 0.8 * w.DeckEdgeDeg))
            res.Warnings.Add($"Heels {w.HeelDeg:F0}° in a beam gale ({WIND_REF_MS:F0} m/s, {w.Condition}): too " +
                             $"much windage for its stability. The deck edge goes under at {w.DeckEdgeDeg:F0}°, " +
                             $"in a {w.DeckEdgeWindKn:F0} kn wind.");
    }
}
