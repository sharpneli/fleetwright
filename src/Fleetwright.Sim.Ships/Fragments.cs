using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>What a shell's burst is made of (docs/battle/shells-armour-damage.md E1; docs/battle/damage/13), from the
/// shell's own numbers: its calibre, mass, filler and explosive (Gurney's constant, brisance), the steel of its wall and
/// how its casing breaks. One per shell type, made when the class compiles, a full detonation's and a low-order one's.
/// <para>A shell's equality is its inputs' (<see cref="FragmentInputs"/>), so the shell rows it rides in compare as
/// before.</para></summary>
public sealed class Fragments : IEquatable<Fragments>
{
    public FragmentInputs Inputs { get; }

    /// <summary>A full detonation's pieces, and a low-order one's (0.4× Gurney, 3× Mott's B: fewer, slower, larger, 13
    /// §2.5 [EST]).</summary>
    public FragmentBurst High { get; }
    public FragmentBurst Low { get; }

    /// <summary>The plate of homogeneous armour (ductile) the best 10% of a full burst's side spray get through 5 m off,
    /// static, mm: one number for the shell's splinters, the matchup's (13 §4.3: 25-30 lb STS against cruiser HE, 1-1.5"
    /// against capital-ship HE).</summary>
    public double SplinterMm { get; }

    Fragments(FragmentInputs inputs)
    {
        Inputs = inputs;
        High = new FragmentBurst(inputs, false);
        Low = new FragmentBurst(inputs, true);
        SplinterMm = High.DefeatMm(5, 0.1, FragmentClass.Ductile);
    }

    /// <summary>A shell's fragments, or null for a shell without filler.</summary>
    public static Fragments? Of(in FragmentInputs inputs) => inputs.FillerKg > 0 && inputs.MassKg > inputs.FillerKg ? new Fragments(inputs) : null;

    public static Fragments? Of(ShellReport s, double calibreMm) => Of(new FragmentInputs(calibreMm, s.MassKg, s.FillerKg, Kinds.ShellKindOf(s.Kind),
        s.GurneyMs, s.Brisance, s.SteelSize, s.SteelSpeed, s.Casing.Nose, s.Casing.Ogive, s.Casing.Body, s.Casing.Plug, s.Casing.Tail, s.FillerBody));

    public FragmentBurst Of(bool lowOrder) => lowOrder ? Low : High;

    public bool Equals(Fragments? other) => other is not null && other.Inputs == Inputs;
    public override bool Equals(object? obj) => Equals(obj as Fragments);
    public override int GetHashCode() => Inputs.GetHashCode();

    // ------------------------------------------------------------------ the laws (13 §2), SI outside, THOR in its own units

    public const double SteelKgM3 = 7850, FillerKgM3 = 1600;
    /// <summary>Mott's constant for TNT in mild or medium steel, lb^½ in^−7/6 (IATG 01.80 T23 [SRC]): brisance is B_TNT/B.</summary>
    public const double BTnt = 0.0779;
    /// <summary>Drag: V = V₀·e^(−R/(L₁·m^⅓)), L₁ m/kg^⅓ (C_D 1.28, forged-steel fragments' shape: DDESB TP-12 [SRC]).</summary>
    public const double DragL1 = 247;
    /// <summary>THOR's limit (TR 47, armor/01 §3.10 [SRC]): V₅₀ = 10^c₁·(e·A)^0.906·m^−0.963·sec^1.286 θ, fps, in, in²,
    /// grains, A = (m/660)^⅔ in²; mild homogeneous steel's c₁. Fitted to 0.3-53 g; the heavy AP and black-powder pieces
    /// are an extrapolation (open question 2).</summary>
    public const double ThorC1 = 6.523, ThorA = 0.906, ThorB = -0.963, ThorG = 1.286, ShapeGrIn3 = 660;
    /// <summary>The side spray's band: "nearly all within an arc of 15 degrees" (WDR 51 [SRC]), the rest (0.15) over the
    /// rest of the sphere [EST].</summary>
    public const double BandHalfDeg = 7.5, OutsideShare = 0.15;
    /// <summary>The fragments counted: 1 g and up (Mott undercounts the fines, which carry little punch; 13 §2.2).</summary>
    public const double MinKg = 0.001;
    const double Lb = 0.45359237, In = 0.0254, Grains = 15432.358, Fps = 0.3048;

    /// <summary>Gurney's cylinder: a body of metal M round charge C throws its pieces at √(2E)·√((C/M)/(1 + C/2M)).</summary>
    public static double Cylinder(double chargeKg, double metalKg, double gurneyMs) =>
        metalKg <= 0 || chargeKg <= 0 ? 0 : gurneyMs * Math.Sqrt(chargeKg / metalKg / (1 + 0.5 * chargeKg / metalKg));

    /// <summary>Gurney's open-faced sandwich: the kick on one heavy end (a nose slug, a base plug) pushed by charge C.</summary>
    public static double End(double chargeKg, double metalKg, double gurneyMs)
    {
        if (metalKg <= 0 || chargeKg <= 0)
            return 0;
        double mu = metalKg / chargeKg;
        return gurneyMs / Math.Sqrt((Math.Pow(1 + 2 * mu, 3) + 1) / (6 * (1 + mu)) + mu);
    }

    /// <summary>A fragment's speed after R m through the air.</summary>
    public static double Speed(double v0, double massKg, double rM) => v0 * Math.Exp(-rM / (DragL1 * Math.Cbrt(massKg)));

    /// <summary>THOR's c₁ for a material's fragments class: mild homogeneous steel's (TR 47); hard homogeneous steel's
    /// offset (+0.076, its residual law's) for homogeneous armour and, lacking a limit of its own, face-hardened plate
    /// [EST]; brittle plate as mild [EST].</summary>
    public static double C1(FragmentClass c) => c switch
    {
        FragmentClass.Ductile or FragmentClass.Fh => ThorC1 + 0.076,
        _ => ThorC1,
    };

    /// <summary>A plate as the mild steel that stops fragments as it does, mm: its mass's thickness in steel (wood and
    /// cement count by their mass), by its class's c₁, and at its obliquity (THOR's sec^1.286, in thickness).</summary>
    public static double MildMm(double steelMm, FragmentClass c, double obliquityDeg = 0)
    {
        double sec = 1 / Math.Max(Math.Cos(double.DegreesToRadians(Math.Min(obliquityDeg, 80))), 1e-3);
        return steelMm * Math.Pow(10, (C1(c) - ThorC1) / ThorA) * Math.Pow(sec, ThorG / ThorA);
    }

    /// <summary>THOR's limit, m/s, for a fragment of a mass against mild steel mm thick at normal (<see cref="MildMm"/>
    /// takes the plate's class and obliquity in).</summary>
    public static double LimitMs(double massKg, double mildMm) =>
        mildMm <= 0 ? 0 : LimitK(massKg) * Math.Pow(mildMm / 25.4, ThorA);

    /// <summary>THOR's limit over e^0.906 (e in inches), m/s: what depends on the fragment's mass.</summary>
    public static double LimitK(double massKg)
    {
        double gr = massKg * Grains, area = Math.Pow(gr / ShapeGrIn3, 2.0 / 3);
        return Math.Pow(10, ThorC1) * Math.Pow(area, ThorA) * Math.Pow(gr, ThorB) * Fps;
    }

    /// <summary>The thickest mild steel a fragment of a mass at v gets through, mm (THOR inverted).</summary>
    public static double MildMmAt(double massKg, double v) => v <= 0 ? 0 : 25.4 * Math.Pow(v / LimitK(massKg), 1 / ThorA);

    internal static double LbHalf => Math.Sqrt(Lb);
    internal static double Inch => In;
}

/// <summary>THOR's classes of plate for fragments (a material's `fragments`).</summary>
public enum FragmentClass : byte
{
    Mild,
    Ductile,
    Fh,
    Brittle,
}

/// <summary>What a shell's fragments are made of: its calibre, mass, filler kg and kind; the explosive's Gurney constant
/// (m/s) and brisance; the steel's size and speed multipliers; the casing's shares (nose, ogive, body, plug, tail) and
/// the filler's share alongside the body.</summary>
public readonly record struct FragmentInputs(double CalibreMm, double MassKg, double FillerKg, ShellKind Kind, double GurneyMs, double Brisance,
    double SteelSize, double SteelSpeed, double Nose, double Ogive, double Body, double Plug, double Tail, double FillerBody);

/// <summary>One burst's pieces (13 §2-3): the side spray from the cylindrical body (Gurney's speed, Mott's masses in
/// <see cref="Classes"/> classes of 1 g and up), the ogive's spray (HE: a forward cone, 30-80° off the nose) and the
/// tail's (a slow backward cone), and two single pieces, the nose slug and the base plug, with the kicks the burst gives
/// them. The wall's geometry follows from the body's charge-to-metal ratio; nothing more is needed.</summary>
public sealed class FragmentBurst
{

    /// <summary>The ogive's spray, degrees off the nose (13 §2.5 [EST]); the tail's [EST].</summary>
    public const double OgiveFromDeg = 30, OgiveToDeg = 80, TailFromDeg = 110, TailToDeg = 180;

    public double BodyKg { get; }
    public double BodyFillerKg { get; }
    public double WallMm { get; }
    public double BoreMm { get; }
    /// <summary>The side spray's speed off a static burst, m/s.</summary>
    public double SideMs { get; }
    /// <summary>Mott's M_k, kg^½, and his N₀: N(>m) = N₀·e^(−√m/M_k).</summary>
    public double MottK { get; }
    public double MottN0 { get; }
    public double OgiveKg { get; }
    public double OgiveMs { get; }
    public double TailKg { get; }
    public double TailMs { get; }
    public double NoseKg { get; }
    public double NoseKickMs { get; }
    public double PlugKg { get; }
    public double PlugKickMs { get; }


    internal FragmentBurst(in FragmentInputs s, bool low)
    {
        double casing = s.MassKg - s.FillerKg;
        BodyKg = casing * s.Body;
        BodyFillerKg = s.FillerKg * s.FillerBody;
        double g = s.GurneyMs * s.SteelSpeed * (low ? 0.4 : 1);
        SideMs = Fragments.Cylinder(BodyFillerKg, BodyKg, g);
        // the wall from the body's charge to metal: x = (d/D)² = (C/M·ρs)/(ρe + C/M·ρs)
        double r = BodyKg > 0 ? BodyFillerKg / BodyKg : 0;
        double x = r * Fragments.SteelKgM3 / (Fragments.FillerKgM3 + r * Fragments.SteelKgM3);
        double D = s.CalibreMm / 25.4, d = D * Math.Sqrt(x), t = (D - d) / 2;
        (BoreMm, WallMm) = (d * 25.4, t * 25.4);
        double B = Fragments.BTnt / Math.Max(s.Brisance, 1e-3) * s.SteelSize * (low ? 3 : 1);
        MottK = d > 0 ? B * Math.Pow(t, 5.0 / 6) * Math.Cbrt(d) * (1 + t / d) * Fragments.LbHalf : 0;
        MottN0 = MottK > 0 ? BodyKg / (2 * MottK * MottK) : 0;
        OgiveKg = casing * s.Ogive;
        OgiveMs = OgiveKg > 0 ? Fragments.Cylinder(s.FillerKg * (1 - s.FillerBody) * 0.8, OgiveKg, g) : 0;   // its cavity's share [EST]
        TailKg = casing * s.Tail;
        TailMs = Fragments.Cylinder(s.FillerKg * 0.05, TailKg, g);
        (NoseKg, PlugKg) = (casing * s.Nose, casing * s.Plug);
        double ends = s.FillerKg * 0.10;   // the cavity's ends [EST]
        (NoseKickMs, PlugKickMs) = (Fragments.End(ends, NoseKg, g), Fragments.End(ends, PlugKg, g));
    }

    /// <summary>Mott: the body's fragments heavier than m.</summary>
    public double CountAbove(double massKg) => MottN0 * Math.Exp(-Math.Sqrt(massKg) / Math.Max(MottK, 1e-12));

    /// <summary>Mott inverted: the mass the n heaviest fragments are heavier than.</summary>
    public double MassAt(double n) => n >= MottN0 ? 0 : Math.Pow(MottK * Math.Log(MottN0 / Math.Max(n, 1e-12)), 2);

    /// <summary>The side spray's fragments of 1 g and up.</summary>
    public double SideCount => CountAbove(Fragments.MinKg);

    /// <summary>The plate of a class the best share (by count, of all Mott's fragments, the fines too, as Okun counted
    /// them) of the side spray get through rM off a static burst, mm (the bench's quantiles, the matchup's splinters). A
    /// heavier fragment gets through more (THOR's limit falls as m^−0.36, and it slows later), so it's the plate of the
    /// share-th heaviest.</summary>
    public double DefeatMm(double rM, double share, FragmentClass c)
    {
        if (!(MottN0 > 0))
            return 0;
        double m = Math.Max(MassAt(MottN0 * share), 1e-6);
        return Fragments.MildMmAt(m, Fragments.Speed(SideMs, m, rM)) / Fragments.MildMm(1, c);
    }

    /// <summary>The side spray's fragments of 1 g and up that get through mild steel mm thick at normal, rM off a burst
    /// whose spray flies at v (static: <see cref="SideMs"/>), of all of them.</summary>
    public double ThroughCount(double rM, double v, double mildMm) =>
        Through(rM, v, mildMm > 0 ? Math.Pow(mildMm / 25.4, Fragments.ThorA) : 0);

    // THOR's limit as K₁·m^p (m in kg), p = 0.906·⅔ − 0.963
    static readonly double LogK1 = Math.Log(Fragments.LimitK(1));
    const double P = Fragments.ThorA * 2 / 3 + Fragments.ThorB;

    /// <summary><see cref="ThroughCount"/> with the plate as e^0.906 (e in inches of mild steel): Mott's count above the
    /// lightest fragment that gets through (<see cref="ThresholdKg"/>), since a heavier one always does.</summary>
    double Through(double rM, double v, double e) => v <= 0 ? 0 : CountAbove(Math.Max(ThresholdKg(rM, v, e), Fragments.MinKg));

    /// <summary>The lightest fragment thrown at v that still beats a plate (e^0.906, e in inches of mild steel; 0 for none)
    /// rM off: v·e^(−R/(L₁m^⅓)) = K₁·m^p·e^0.906, solved in ln m by Newton (the left side less the right is concave and
    /// rising in ln m, so the steps climb to the root from the drag-free one below it).</summary>
    public static double ThresholdKg(double rM, double v, double e)
    {
        if (e <= 0)
            return 0;
        double a = Math.Log(v) - LogK1 - Math.Log(e), b = rM / Fragments.DragL1;
        double x = Math.Max(a / P, Math.Log(1e-7));   // the drag-free root, a − p·x = 0: at or below the true one
        for (int i = 0; i < 12; i++)
        {
            // f = a − p·x − b·e^(−x/3): rising (p < 0) and concave, so Newton from the left climbs to the root
            double ex = b * Math.Exp(-x / 3), f = a - P * x - ex, df = -P + ex / 3;
            double dx = -f / df;
            x += dx;
            if (Math.Abs(dx) < 1e-4)
                break;
        }
        return Math.Exp(Math.Min(x, Math.Log(1e5)));
    }

    /// <summary>Line targets (13 §4.4, §5.3: a cable run, a pipe, a trunk) crossing the side band rM off: the hits on one w m
    /// wide by the fragments of minKg and up still faster than minMs there, n·w/(2πR), independent of the band's width
    /// (the band's share of the spray, 0.85). For the service networks' cut rule.</summary>
    public double LineHits(double rM, double widthM, double v, double minKg = 0.005, double minMs = 150)
    {
        if (v <= minMs)
            return 0;
        double slow = rM / (Fragments.DragL1 * Math.Log(v / minMs));   // lighter than slow³ kg, it's under minMs by rM
        double n = CountAbove(Math.Max(minKg, slow * slow * slow));
        return (1 - Fragments.OutsideShare) * n * widthM / (2 * Math.PI * Math.Max(rM, 0.1));
    }

    /// <summary>The fragments expected to hit a box and get through its plate (13 §5.1): the burst at a point, the shell
    /// flying along dir at v there. Each spray flies at its kick plus the shell's motion: the side ring leans forward by
    /// atan(v/V₀), the ogive's and tail's cones the same way. A box takes its presented area's share of the sprays it
    /// overlaps (in the polar angle off the shell's line), its fragments slowed by the air on the way to its nearest
    /// point; those over the plate's limit get in. The plate is mild steel mildMm thick at normal (<see
    /// cref="Fragments.MildMm"/>), at its obliquity on the box's face square to the fragments, or on the face whose
    /// normal is <paramref name="normal"/> when one is given (a deck's, a bulkhead's). Allocation-free.</summary>
    public double Hits(P3 at, P3 dir, double v, in Box box, double mildMm, P3 normal = default)
    {
        if (SideCount <= 0)
            return 0;
        double cx = (box.X0 + box.X1) / 2, cy = (box.Y0 + box.Y1) / 2, cz = (box.Base + box.Top) / 2;
        double dx = box.X1 - box.X0, dy = box.Y1 - box.Y0, dz = box.Top - box.Base;
        double rx = cx - at.X, ry = cy - at.Y, rz = cz - at.Z, rc = Math.Sqrt(rx * rx + ry * ry + rz * rz);
        P3 u = rc > 1e-6 ? new P3(rx / rc, ry / rc, rz / rc) : dir;
        double near = Math.Max(Distance(box, at), 0.3);
        double area = Math.Abs(u.X) * dy * dz + Math.Abs(u.Y) * dx * dz + Math.Abs(u.Z) * dx * dy;
        double omega = Math.Min(area / Math.Max(rc * rc, 0.25), 2 * Math.PI);
        // the plate at its obliquity: on the face given, else the face square to the fragments
        double cosOb = normal == default ? Math.Max(Math.Max(Math.Abs(u.X), Math.Abs(u.Y)), Math.Abs(u.Z))
            : Math.Abs(u.X * normal.X + u.Y * normal.Y + u.Z * normal.Z);
        double sec = 1 / Math.Max(cosOb, Math.Cos(double.DegreesToRadians(80)));
        double e = mildMm > 0 ? Math.Pow(mildMm / 25.4 * Math.Pow(sec, Fragments.ThorG / Fragments.ThorA), Fragments.ThorA) : 0;
        // the box's polar angle off the shell's line, and its half-extent in it
        double cosPsi = Math.Clamp(u.X * dir.X + u.Y * dir.Y + u.Z * dir.Z, -1, 1), psi = Math.Acos(cosPsi);
        double sinPsi = Math.Sqrt(Math.Max(1 - cosPsi * cosPsi, 0));
        double h;
        if (sinPsi > 1e-6)
        {
            double ex = (u.X * cosPsi - dir.X) / sinPsi, ey = (u.Y * cosPsi - dir.Y) / sinPsi, ez = (u.Z * cosPsi - dir.Z) / sinPsi;
            h = Math.Abs(ex) * dx + Math.Abs(ey) * dy + Math.Abs(ez) * dz;
        }
        else
            h = Math.Max(dx, Math.Max(dy, dz));
        double alpha = Math.Min(Math.Atan(h / 2 / Math.Max(rc, 1e-3)), Math.PI / 2);

        double hits = 0;
        // the side ring, leaning forward with the shell, and the share outside it over the rest of the sphere
        double side = Math.Atan2(SideMs, v), band = double.DegreesToRadians(Fragments.BandHalfDeg);
        double inBand = Overlap(psi, alpha, side - band, side + band);
        double bandOmega = Cone(side - band, side + band), sideV = Math.Sqrt(SideMs * SideMs + v * v);
        double thru = Through(near, sideV, e);
        hits += thru * ((1 - Fragments.OutsideShare) * inBand * omega / bandOmega
                        + Fragments.OutsideShare * (1 - inBand) * omega / Math.Max(4 * Math.PI - bandOmega, 1e-6));
        // the ogive's forward cone and the tail's backward one, each the side spray's classes scaled to its metal
        if (OgiveKg > 0 && BodyKg > 0)
            hits += Spray(psi, alpha, omega, near, e, v, OgiveMs, OgiveFromDeg, OgiveToDeg, OgiveKg / BodyKg);
        if (TailKg > 0 && BodyKg > 0)
            hits += Spray(psi, alpha, omega, near, e, v, TailMs, TailFromDeg, TailToDeg, TailKg / BodyKg);
        return hits;
    }

    /// <summary>A cone of spray's hits on a box: its share of the box's polar extent, the classes scaled to its metal.</summary>
    double Spray(double psi, double alpha, double omega, double near, double e, double v, double kick, double fromDeg, double toDeg, double scale)
    {
        double a0 = Lean(kick, v, fromDeg), a1 = Lean(kick, v, toDeg);
        if (a0 > a1)
            (a0, a1) = (a1, a0);
        double f = Overlap(psi, alpha, a0, a1);
        if (f <= 0)
            return 0;
        double mid = double.DegreesToRadians((fromDeg + toDeg) / 2);
        double speed = Math.Sqrt(Math.Max(kick * kick + v * v + 2 * kick * v * Math.Cos(mid), 0));
        return scale * Through(near, speed, e) * f * omega / Math.Max(Cone(a0, a1), 1e-6);
    }

    /// <summary>A piece thrown psi off the nose at kick, with the shell's v added: its angle off the shell's line.</summary>
    static double Lean(double kick, double v, double psiDeg)
    {
        double p = double.DegreesToRadians(psiDeg);
        return Math.Atan2(kick * Math.Sin(p), v + kick * Math.Cos(p));
    }


    /// <summary>The share of [psi − alpha, psi + alpha] inside [a, b] (all of it, for a box too small to tell, when psi is).</summary>
    static double Overlap(double psi, double alpha, double a, double b)
    {
        if (alpha < 1e-6)
            return psi >= a && psi <= b ? 1 : 0;
        double lo = Math.Max(psi - alpha, a), hi = Math.Min(psi + alpha, b);
        return hi > lo ? (hi - lo) / (2 * alpha) : 0;
    }

    /// <summary>The solid angle between two polar angles off an axis.</summary>
    static double Cone(double a, double b) => 2 * Math.PI * Math.Abs(Math.Cos(Math.Max(a, 0)) - Math.Cos(Math.Min(b, Math.PI)));

    static double Distance(in Box b, P3 p)
    {
        double dx = Math.Max(Math.Max(b.X0 - p.X, p.X - b.X1), 0), dy = Math.Max(Math.Max(b.Y0 - p.Y, p.Y - b.Y1), 0),
            dz = Math.Max(Math.Max(b.Base - p.Z, p.Z - b.Top), 0);
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
