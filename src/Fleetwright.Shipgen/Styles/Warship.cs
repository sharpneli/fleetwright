namespace Fleetwright.Shipgen;

/// <summary>Displacement-hull armoured warships, destroyer to battleship. The layout is Layout.BuildLayout.</summary>
public sealed class WarshipStyle : Style
{
    public override string Name => "warship";
    public override bool TakesMainList => true;
    public override bool HasMidshipsTurrets => true;
    public override bool HasWingTurrets => true;
    public override bool TakesSecondaryList => true;
    public override bool HasCasemates => true;
    public override int MinTowerLevels => 2;
    public override bool HasDeckhouseLevels => true;
    public override bool HasControlTowers => true;
    public override bool HasRaisedMounts => true;
    public override bool RaisedHull => true;

    public override Layout BuildLayout(Design design, Navarch.Result res, double shift = 0.0, double spread = 0.0) =>
        Layout.BuildLayout(design, res, shift, spread);
}

/// <summary>Planing-hull fast craft (motor torpedo boats, PT boats, motor gunboats).</summary>
public sealed class PlaningStyle : Style
{
    const double WOOD_T_PER_M2 = 0.10, PLANK_MM = 25.0;

    public override string Name => "planing";

    public override TechInput DefaultTech { get; } = new()
    {
        Name = "Petrol engines (1940)", Fuel = "petrol", WeightKgPerKw = 6.5, StressFloor = 0.7, SfcGPerKwh = 291, DensityTPerM3 = 0.4,
        UnitMaxMw = 1.1, Unit = new() { Mw = 1, HeightM = 1.2, WidthM = 1.1, LengthM = 2.5 }, BoilerFraction = 0.0, CrewK = 3,
        PartLoad = "DSL", Draught = new() { System = "exhaust", VelocityMS = 35, ReachM = 60, GasTempK = 620, AirFuelRatio = 38 },
    };

    public override double DefaultBlockCoefficient => 0.45;
    public override SizeRules Sizing => new((8.0, 60.0), 12.0, 0.06, 1.0, 4.5, false);

    protected override IEnumerable<Limit> StyleLimits =>
    [
        new(["hull", "block_coefficient"], 0.35, 0.6), new(["speed_kn"], 15, 60), new(["range_nm"], 100, 3000),
    ];

    public override Tuning Tuning(Design design) => new()
    {
        PlaningPower = true, BoxHull = true, HullK = 0.06, PlateOwnMm = PLANK_MM, FreeboardA = 0.04, FreeboardB = 0.8,
        MiscFrac = 0.07, CruiseKn = 25.0, LcbFrac = -0.11, GmStiffFrac = 0.5, FnWarn = 99.0, LbWarn = 2.8, TrimTolFrac = 0.025,
        TrimWarnFrac = 0.01,
    };

    public override Layout BuildLayout(Design design, Navarch.Result res, double shift = 0.0, double spread = 0.0) =>
        PlaningLayout(design, res, shift);

    public override List<string> Checks(Design design, Navarch.Result r, Tuning tun)
    {
        double fnv = Navarch.VolumetricFroude(r.Full, design.SpeedKn!.Value);
        if (fnv < 2.0)
            return [$"Not fully planing at {design.SpeedKn} kn (Fn∇ {fnv:F1}, want 2+): the hull is too heavy " +
                    "or too slow to rise onto the plane, so the power is spent pushing water."];
        return [];
    }

    public override string CrewStandard => "H0";

    public override void AddResults(Results results, Design design, Layout lay, Navarch.Result r)
    {
        results.VolumetricFroude = Math.Round(Navarch.VolumetricFroude(r.Full, design.SpeedKn!.Value), 2);
        results.PowerToWeightHpPerT = Math.Round(r.PowerShp / r.Full, 1);
        results.PowerModel = "planing placeholder";
    }

    public override List<string> Summary(Design design, Layout lay, Navarch.Result r) =>
        [$"planing: Fn∇ {Navarch.VolumetricFroude(r.Full, design.SpeedKn!.Value):F2}   " +
         $"{r.PowerShp / r.Full:F0} hp/t   (placeholder power model)"];

    static HullSpec PlaningHullSpec(Design design) =>
        new(design.HullLength, design.HullBeam, new HullEnd { Taper = 0.45, Power = 1.25, Shape = "pointed" },
            new HullEnd { Taper = 0.04, Transom = 0.92, Power = 2.0, Shape = "round" }, DeckInset: 0.15, PlankSpacing: 0.55);

    static Layout PlaningLayout(Design design, Navarch.Result res, double shift)
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

        var blocks = new List<Block>();
        double cx0 = 0.0 + shift, cx1 = 0.22 * L + shift;
        double wc = 0.42 * B;
        Layout.AddBlock(lay, blocks, "Charthouse", cx0, cx1, wc, 1, 0.45 * wc, 0.2, role: "bridge");
        var masts = new List<Mast> { new() { X = cx0 + 0.25 * (cx1 - cx0), Yard = Math.Min(0.5 * B, 2.4), Tripod = false, Top = Layout.LEVEL_H + 3.5 } };

        double m0 = -0.42 * L, m1 = -0.08 * L;
        double lMach = Layout.PlanMachinery(lay, design, res, hull, (m0 + m1) / 2);
        if (lMach > m1 - m0)
            lay.Fail("length", $"The engines need {lMach:F1} m, but the engine room has {m1 - m0:F1} m.");
        lay.Geo.Machinery = (m0, m1);
        lay.Geo.MachineryX = (m0 + m1) / 2;
        Layout.SetCitadel(lay, m0, m1);

        var mounts = new List<Mount>();
        var turretTypes = new OrderedDictionary<string, TurretType>(StringComparer.Ordinal);
        if (design.Torpedoes is { Mounts: > 0 } tp)
        {
            var xs = Enumerable.Range(0, (int)(0.6 * L / 0.5)).Select(k => cx0 + 0.3 * (cx1 - cx0) - k * 0.5);

            double YOfX(double x, TurretType t)
            {
                double w = ((t.Barrels - 1) * t.Spacing + t.BarrelW) / 2;
                return Math.Min(hull.HalfWidth(x + t.BarrelLen / 2), hull.HalfWidth(x - t.BarrelLen / 2)) - w - 0.15;
            }

            Armament.FixedTubePairs(lay, mounts, turretTypes, tp, xs, YOfX, toeDeg: 5.0);
        }

        if (Armament.GunGroups(design) is var (gun, fore, aft))
        {
            Armament.GunLine(lay, mounts, turretTypes, gun, fore.Count, fore.Stepped, false, "main", ["A", "B", "C"], L / 2 - 0.16 * L, -1,
                0.0, 0, x => 0.2, armourMm: gun.ArmourMm ?? 0, depth: depth);
            Armament.GunLine(lay, mounts, turretTypes, gun, aft.Count, aft.Stepped, false, "main", ["Y", "X", "W"], -L / 2 + 1.6, +1,
                0.0, 180, x => 0.2, armourMm: gun.ArmourMm ?? 0, depth: depth);
        }

        var xs2 = CarrierStyle.Vdc(48).Select(v => -L / 2 + 0.1 * L + v * 0.7 * L).ToList();
        var secs = Batteries.SecondaryBatteries(design);
        Armament.WarnUnpaired(lay, secs);
        if (secs.Count > 0 && secs[0].MountsPerSide != 0)
        {
            var sec = secs[0];
            var (tsId, ts) = Armament.GunType(sec);
            double r = Armament.BodyReach(ts);
            Armament.SidePairs(lay, mounts, turretTypes, "secondary", tsId, ts, sec.MountsPerSide,
                xs2.Select(x => new Slot(x, hull.HalfWidth(x) - r - 0.3, 0.2)), "S", armourMm: sec.ArmourMm!.Value);
        }
        FireControl.Place(lay, design, blocks);
        var aaOut = new List<AaMount>();
        foreach (var (kind, count) in new[] { ("quad40", design.Aa?.Heavy ?? 0), ("single20", design.Aa?.Light ?? 0) })
        {
            double rr = Geometry.AA_CFG[kind].R;
            var cands = new List<Slot> { new(cx0 - rr - 0.3, rr + 0.15, 0.2), new(cx0 - rr - 0.3, 0.0, 0.2) };
            cands.AddRange(xs2.Select(x => new Slot(x, hull.HalfWidth(x) - rr - 0.3, 0.2)));
            cands.AddRange(xs2.Select(x => new Slot(x, 0.0, 0.2)));
            Armament.PlaceAa(lay, aaOut, kind, count, cands, spacing: 0.6);
        }

        var fittings = new List<Fitting>();
        for (int k = 0; k < 3; k++)
        {
            double hx = m0 + (k + 0.5) * (m1 - m0) / 3, hl = 0.18 * (m1 - m0);
            var fp = Footprint.Rect(hx - hl / 2, -0.16 * B, hx + hl / 2, 0.16 * B);
            if (lay.Free(fp, 0.2))
            {
                fittings.Add(new Fitting(hx, 0.0, hl, 0.32 * B, "hatch_coaming"));
                lay.Occupy(fp, 0, 0.5, $"Engine hatch {k + 1}");
            }
        }
        var fpS = Footprint.Rect(-L / 2 + 0.2, -0.18 * B, -L / 2 + 1.0, 0.18 * B);
        if (lay.Free(fpS, 0.1))
        {
            fittings.Add(new Fitting(-L / 2 + 0.6, 0.0, 0.7, 0.35 * B, "fitting"));
            lay.Occupy(fpS, 0, 1.0, "Smoke generator");
        }
        var boats = new List<Boat>();
        double rl = Layout.Clamp(0.1 * L, 1.8, 3.0), rw = Layout.Clamp(0.22 * B, 1.0, 1.6);
        foreach (var x in Enumerable.Range(0, Math.Max(0, (int)((m1 - m0) / 0.5))).Select(k => m1 - rl / 2 - k * 0.5))
        {
            var fp = Footprint.Rect(x - rl / 2, -rw / 2, x + rl / 2, rw / 2);
            if (lay.Free(fp, 0.2))
            {
                boats.Add(new Boat(x, 0.0, rl, rw, 1.0));
                lay.Occupy(fp, 0.5, 1.0, "Raft");
                break;
            }
        }

        double innerHw = 0.8 * B / 2;
        lay.Compartments.AddRange([
            new Compartment { Id = "Crew space", Kind = "accommodation", X0 = cx1, X1 = L / 2 - 0.08 * L, HalfWidth = innerHw },
            new Compartment { Id = "Fuel tanks", Kind = "fuel_tank", X0 = m1, X1 = cx0 + 0.05 * L, HalfWidth = innerHw },
            new Compartment { Id = "Engine room", Kind = "engine_room", X0 = m0, X1 = m1, HalfWidth = innerHw },
        ]);
        Layout.AddSteering(lay, -L / 2, m0, 0.6 * B / 2, "Tiller flat");
        var guns = Ordnance.Guns(mounts);
        if (guns.Count > 0)
        {
            double lx1 = L / 2 - 0.08 * L, lw = 0.5 * innerHw;
            Ordnance.Stow(lay, mounts, [new Zone(lx1 - Ordnance.ZoneLength(Ordnance.BookedM3(lay, guns), 2 * lw, lay.Geo.Plant!, 1, 0.5),
                lx1, lw, [new ZoneRoom("Ammunition locker", guns)])]);
        }
        Layout.FinishLayout(lay, design, hs, mounts, turretTypes, blocks, [], masts, aaOut, 0.0);
        lay.Spec.Boats = boats;
        lay.Spec.Fittings = fittings;
        return lay;
    }
}
