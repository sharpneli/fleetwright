using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Combat;

/// <summary>What a puff's smoke looks like, for the drawing and (later) the smoke contacts' contrast.</summary>
public enum SmokeTint : byte
{
    Coal,
    Oil,
    Powder,
    Fire,
    Chemical,
}

/// <summary>A Gaussian puff of smoke as the field keeps it (docs/battle/smoke.md Z0): its state at any age is closed form
/// (<see cref="SmokeModel.State"/>), so nothing steps it. Where it is comes from the air's frame: a puff never moves
/// relative to the air.</summary>
public struct SmokePuff
{
    /// <summary>Its place on the sea at birth (a merged puff's: at the merge), in the air's frame
    /// (<see cref="AirFrame.ToAir"/>), Earth-centred at sea level.</summary>
    public Vec3d Origin;
    /// <summary>Its birth, world time, s (a merged puff's: the age its spread carries on from).</summary>
    public double Born;
    /// <summary>Its height at birth over the sea, m, the rise still to come on its heat, m, and the rise's time, s
    /// (<see cref="SmokeModel.RiseShare"/>).</summary>
    public float Z0, Rise, RiseS;
    /// <summary>Its variances at birth (and what a merge folded in), across and up, m².</summary>
    public float S0h2, S0z2;
    /// <summary>Its extinction area at birth, m², and the time it decays over, s (0: it doesn't).</summary>
    public float A0, LifeS;
    /// <summary>The air's turbulence up at its settled height, σ_w, m/s, and its Lagrangian time, s, as the air was when
    /// it was born or merged (0: read the air's now). Kept so its state is a few multiplies and two exponentials.</summary>
    public float SigmaW, LagrangianW;
    public SmokeTint Tint;
}

/// <summary>A puff at an age: its centre on the sea (Earth-centred, sea level), its height over the sea, m, its
/// variances across and up, m², its extinction area, m², and its age, s.</summary>
public readonly record struct PuffState(Vec3d Centre, double Z, double SigmaH2, double SigmaZ2, double Area, double Age);

/// <summary>A puff readied for many sight lines (the index's, docs/battle/smoke.md Z2): its state with the square roots
/// and normalisations worked once, for <see cref="SmokeModel.TauGrid(in SmokeLine, in LinePuff, ReadOnlySpan{double},
/// ReadOnlySpan{double}, Span{double})"/>.</summary>
public readonly struct LinePuff
{
    public readonly Vec3d Centre;
    /// <summary>Its height, m; σ_h, m; how far off a line it can add anything (3.5 σ_h), m; A / (2π σ_h σ_z);
    /// 1/(2σ_h²); 1/(2σ_z²); (3.5 σ_z)²; 1/(√2 σ_h).</summary>
    public readonly double Z, SigmaH, Reach, Plan, HalfInvH2, HalfInvZ2, ReachZ2, InvRoot2H;

    public LinePuff(in PuffState p, Vec3d centre)
    {
        double sh = Math.Sqrt(p.SigmaH2), sz = Math.Sqrt(p.SigmaZ2);
        (Centre, Z, SigmaH, Reach) = (centre, p.Z, sh, SmokeModel.CullSigma * sh);
        Plan = p.Area > 0 ? p.Area / (2 * Math.PI * sh * sz) : 0;
        (HalfInvH2, HalfInvZ2, ReachZ2, InvRoot2H) = (1 / (2 * p.SigmaH2), 1 / (2 * p.SigmaZ2), SmokeModel.CullSigma * SmokeModel.CullSigma * p.SigmaZ2,
            1 / (Math.Sqrt(2) * sh));
    }
}

/// <summary>The air's frame (docs/battle/smoke.md Z0): the world's one wind as a rotation of the sphere, so a puff born in
/// it stays put and is carried with it exactly, however long it drifts and however the wind turns over time (each step
/// a rotation about the axis that takes the reference point along the wind). Near the reference point that's the
/// weather's wind; a theatre's breadth away its direction differs by the angle the theatre spans (~1° a 100 km), well
/// inside the weather's own accuracy.</summary>
public struct AirFrame
{
    // world = M · air (rows)
    double m00, m01, m02, m10, m11, m12, m20, m21, m22;

    public static AirFrame Identity => new() { m00 = 1, m11 = 1, m22 = 1 };

    public readonly Vec3d ToWorld(Vec3d p) =>
        new(m00 * p.X + m01 * p.Y + m02 * p.Z, m10 * p.X + m11 * p.Y + m12 * p.Z, m20 * p.X + m21 * p.Y + m22 * p.Z);

    public readonly Vec3d ToAir(Vec3d p) =>
        new(m00 * p.X + m10 * p.Y + m20 * p.Z, m01 * p.X + m11 * p.Y + m21 * p.Z, m02 * p.X + m12 * p.Y + m22 * p.Z);

    /// <summary>The air carried on for <paramref name="dtS"/> s by a wind blowing toward <paramref name="windToDeg"/> at
    /// <paramref name="windMs"/> at the reference point <paramref name="at"/>.</summary>
    public void Advance(Vec3d at, double windToDeg, double windMs, double dtS)
    {
        double angle = windMs * dtS / Earth.Radius;
        if (!(angle != 0))
            return;
        var up = at.Normalized();
        var k = Vec3d.Cross(up, Earth.Forward(at, windToDeg)).Normalized();
        double c = Math.Cos(angle), s = Math.Sin(angle), t = 1 - c;
        // Rodrigues' rotation about k, applied to each column of M
        (m00, m10, m20) = Rotate(m00, m10, m20, k, c, s, t);
        (m01, m11, m21) = Rotate(m01, m11, m21, k, c, s, t);
        (m02, m12, m22) = Rotate(m02, m12, m22, k, c, s, t);
        Orthonormalize();
    }

    static (double, double, double) Rotate(double x, double y, double z, Vec3d k, double c, double s, double t)
    {
        double d = k.X * x + k.Y * y + k.Z * z;
        return (x * c + (k.Y * z - k.Z * y) * s + k.X * d * t,
            y * c + (k.Z * x - k.X * z) * s + k.Y * d * t,
            z * c + (k.X * y - k.Y * x) * s + k.Z * d * t);
    }

    public readonly void Hash(ref StateHash h)
    {
        h.Add(m00); h.Add(m01); h.Add(m02);
        h.Add(m10); h.Add(m11); h.Add(m12);
        h.Add(m20); h.Add(m21); h.Add(m22);
    }

    /// <summary>Gram-Schmidt on the columns: the rounding of many small steps never shears the frame.</summary>
    void Orthonormalize()
    {
        var a = new Vec3d(m00, m10, m20).Normalized();
        var b = new Vec3d(m01, m11, m21);
        b = (b - a * Vec3d.Dot(a, b)).Normalized();
        var c = Vec3d.Cross(a, b);
        (m00, m10, m20, m01, m11, m21, m02, m12, m22) = (a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z);
    }
}

/// <summary>The air the smoke moves in, from the weather (docs/battle/smoke.md Z0): the friction velocity over the sea
/// u* = √C_D·U₁₀, the stability from the air's temperature over the sea's (a bulk Richardson number into Monin-Obukhov's
/// z/L), and from them the turbulence that spreads a puff (σ_v and T_L across, σ_w and K_z up) and the stability that
/// caps a plume's rise. Interpolated on those, no stability classes [EST: surface-layer textbook values, the research's
/// K_h ≈ 5 m²/s in neutral air at 8 m/s; tuned when the system is whole].</summary>
public readonly record struct SmokeAir
{
    /// <summary>The 10 m wind, toward, degrees, and m/s; the wind the smoke drifts on (at the steering height), m/s.</summary>
    public double WindToDeg { get; init; }
    public double WindMs { get; init; }
    public double DriftMs { get; init; }
    /// <summary>The air's temperature, K, and the friction velocity u*, m/s.</summary>
    public double AirK { get; init; }
    public double FrictionMs { get; init; }
    /// <summary>1/L, per m: positive stable, negative unstable, 0 neutral.</summary>
    public double InvObukhovM { get; init; }
    /// <summary>The turbulence across: σ_v, m/s, and its Lagrangian time T_L, s.</summary>
    public double SigmaV { get; init; }
    public double LagrangianH { get; init; }

    public const double DragCoefficient = 1.2e-3, SigmaVPerUstar = 1.9, SigmaWPerUstar = 1.25, LagrangianHS = 20;
    /// <summary>The height the smoke drifts at (the wind there by a sea profile, power 0.11), m; the surface layer's top
    /// (K_z stops growing above it), m; the air's temperature when the weather doesn't give one, K.</summary>
    public const double SteeringM = 50, ProfilePower = 0.11, SurfaceLayerM = 100, DefaultAirK = 288;
    /// <summary>The calmest wind the turbulence is worked from, m/s (smoke still spreads in a calm).</summary>
    public const double CalmMs = 0.5;

    public static SmokeAir Of(in WeatherHere w)
    {
        double u = Math.Max(w.WindMs, CalmMs), us = Math.Sqrt(DragCoefficient) * u, airK = DefaultAirK;
        double ri = SmokeModel.G * 10 * w.AirOverSeaK / (airK * u * u);
        double zeta10 = Math.Clamp(ri >= 0 ? ri / Math.Max(1 - 5 * ri, 0.1) : ri, -2, 2);
        double unstable = zeta10 < 0 ? Math.Cbrt(1 - 3 * zeta10) : 1;
        return new SmokeAir
        {
            WindToDeg = (w.WindFromDeg + 180) % 360, WindMs = Math.Max(w.WindMs, 0),
            DriftMs = Math.Max(w.WindMs, 0) * Math.Pow(SteeringM / 10, ProfilePower),
            AirK = airK, FrictionMs = us, InvObukhovM = zeta10 / 10,
            SigmaV = SigmaVPerUstar * us * unstable, LagrangianH = LagrangianHS,
        };
    }

    double Zeta(double z) => Math.Clamp(Height(z) * InvObukhovM, -2, 2);

    static double Height(double z) => Math.Clamp(z, 2, SurfaceLayerM);

    /// <summary>Monin-Obukhov's φ_h: the heat's (and smoke's) mixing slowed in stable air, quickened in unstable.</summary>
    static double PhiH(double zeta) => zeta >= 0 ? 1 + 5 * zeta : 1 / Math.Sqrt(1 - 16 * zeta);

    /// <summary>The turbulence up at a height: σ_w, m/s.</summary>
    public double SigmaW(double z)
    {
        double zeta = Zeta(z);
        return SigmaWPerUstar * FrictionMs * (zeta < 0 ? Math.Cbrt(1 - 3 * zeta) : 1);
    }

    /// <summary>The eddy diffusivity up at a height, K_z = κ u* z / φ_h, m²/s.</summary>
    public double Kz(double z) => SmokeModel.Karman * FrictionMs * Height(z) / PhiH(Zeta(z));

    /// <summary>The Lagrangian time up at a height, s: K_z / σ_w².</summary>
    public double LagrangianW(double z)
    {
        double sw = SigmaW(z);
        return sw > 0 ? Kz(z) / (sw * sw) : 0;
    }

    /// <summary>The stability at a height, s = (g/T)·dθ/dz, s⁻² (0 when neutral or unstable): Monin-Obukhov's
    /// dθ/dz = θ*·φ_h/(κz) with θ* = u*²T/(κgL), so s = u*²·φ_h/(κ²·L·z).</summary>
    public double Stability(double z) =>
        InvObukhovM > 0 ? FrictionMs * FrictionMs * PhiH(Zeta(z)) * InvObukhovM / (SmokeModel.Karman * SmokeModel.Karman * Height(z)) : 0;
}

/// <summary>A sight line for the smoke (docs/battle/smoke.md Z0): from a point over the sea to another, its height
/// along it over the sea with the Earth's curvature, h(s) = h_a + (h_b − h_a)·s/L − s(L − s)/(2kR) (the refraction's k,
/// as the sight model's horizon). Puffs are placed on it in the tangent plane at its start: across a sight line's 30 km
/// that's out by centimetres.</summary>
public readonly struct SmokeLine
{
    public readonly Vec3d From, Along, Side;
    public readonly double Length, FromM, ToM, Curve;

    public SmokeLine(Vec3d from, Vec3d to, double fromM, double toM, double refraction)
    {
        var up = from.Normalized();
        From = up * Earth.Radius;
        var d = to - from;
        Along = (d - up * Vec3d.Dot(d, up)).Normalized();
        Side = Vec3d.Cross(up, Along);
        Length = Earth.Distance(from, to);
        (FromM, ToM) = (fromM, toM);
        Curve = 1 / (2 * refraction * Earth.Radius);
    }

    /// <summary>The line's height over the sea at s m along it (ending <paramref name="toM"/> high), m.</summary>
    public double HeightAt(double s, double toM) => FromM + (toM - FromM) * s / Length - s * (Length - s) * Curve;

    /// <summary>Its slope there.</summary>
    public double SlopeAt(double s, double toM) => (toM - FromM) / Length - (Length - 2 * s) * Curve;
}

/// <summary>The smoke field's model (docs/battle/smoke.md Z0; research/vfx/unified-smoke-system.md): Gaussian puffs, each
/// a passive scalar in a prescribed wind, closed form in its age. Pure functions; no state, nothing allocated.
/// <list type="bullet">
/// <item><b>The rise</b> on its heat (Briggs): the buoyancy flux F from the gas, a final rise by the wind and the
/// stability, reached along one curve (<see cref="RiseShare"/>, Briggs' x^⅔ at first); funnel gas leaving slower than
/// 1.5 times the wind is pulled down into the funnel's lee first (<see cref="Downwash"/>).</item>
/// <item><b>The spread</b> by Taylor's statistical theory, σ² = s0² + 2σ²T_L²(t/T_L − 1 + e^(−t/T_L)): σ·t at first, 2K·t
/// later, across and up with their own turbulence (<see cref="SmokeAir"/>), and the rising plume's own entrainment, Δh/3.5
/// (Pasquill's buoyancy-induced spread).</item>
/// <item><b>The sea</b> under it: the vertical Gaussian is reflected at the surface (an image at −z).</item>
/// <item><b>A sight line</b> through it: the whitened 3D line integral (closed form with an erf), with the curvature,
/// cheap culls first.</item>
/// </list></summary>
public static class SmokeModel
{
    public const double G = 9.81, Karman = 0.4;
    /// <summary>The highest rise the model gives a plume, m (a calm's).</summary>
    public const double MaxRiseM = 500;
    /// <summary>The rising plume's spread for its rise: σ_b = Δh / 3.5 (Pasquill 1976).</summary>
    public const double BuoyantSpread = 1 / 3.5;
    /// <summary>A puff is gone when its peak column τ, A / (2π σ_h²), falls under this.</summary>
    public const double GoneTau = 0.005;
    /// <summary>A puff further off a line than this many σ adds nothing worth the work (e^(−6.1)).</summary>
    public const double CullSigma = 3.5;

    // ------------------------------------------------------------------ erf

    /// <summary>The error function, our own (.NET has none): Abramowitz and Stegun 7.1.26, within 1.5e-7; its series near
    /// 0, where 7.1.26 isn't quite odd. (An own exp too was tried, 2026-10-21: Math.Exp is faster, 2.8 ns against 3.7, and
    /// the rest of the sim uses it.)</summary>
    public static double Erf(double x)
    {
        double ax = Math.Abs(x);
        if (ax > 6)
            return Math.Sign(x);
        if (ax < 0.05)
        {
            double x2 = x * x;
            return 1.1283791670955126 * x * (1 - x2 * (1.0 / 3 - x2 * (0.1 - x2 / 42)));
        }
        double t = 1 / (1 + 0.3275911 * ax);
        double y = 1 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-ax * ax);
        return x < 0 ? -y : y;
    }

    // ------------------------------------------------------------------ the rise

    /// <summary>A funnel's or fire's buoyancy flux F = g·V̇·(T_g − T_a)/(π·T_g), m⁴/s³, from its gas flow, m³/s, at its
    /// temperature, K.</summary>
    public static double BuoyancyFlux(double gasM3s, double gasK, double airK) =>
        gasM3s > 0 && gasK > airK ? G * gasM3s * (gasK - airK) / (Math.PI * gasK) : 0;

    /// <summary>A fire's buoyancy flux from the heat it gives the air, MW: Briggs' 8.8 m⁴/s³ a MW.</summary>
    public static double HeatFlux(double mw) => 8.8 * Math.Max(mw, 0);

    /// <summary>Briggs' plume rise: the final rise, m, the lower of neutral air's (21.4 F^¾/U under F = 55, 38.7 F^⅗/U
    /// over) and stable air's 2.6 (F/(U s))^⅓, and the time it's reached in along Briggs' 1.6 F^⅓ (U t)^⅔ / U, s.
    /// <paramref name="windMs"/> is the wind the plume leaves in (relative to its source), at least 1 m/s.</summary>
    public static (double RiseM, double RiseS) Briggs(double flux, double windMs, double stability)
    {
        if (!(flux > 0))
            return (0, 1);
        double u = Math.Max(windMs, 1);
        double rise = flux < 55 ? 21.425 * Math.Pow(flux, 0.75) / u : 38.71 * Math.Pow(flux, 0.6) / u;
        if (stability > 0)
            rise = Math.Min(rise, 2.6 * Math.Cbrt(flux / (u * stability)));
        rise = Math.Min(rise, MaxRiseM);
        double s = Math.Pow(rise * Math.Cbrt(u) / (1.6 * Math.Cbrt(flux)), 1.5);
        return (rise, Math.Max(s, 0.1));
    }

    /// <summary>Briggs' stack-tip downwash: gas leaving at <paramref name="exitMs"/> into a wind over 2/3 of it is pulled
    /// down into the funnel's lee by 2r(1.5 − w/U), m.</summary>
    public static double Downwash(double exitMs, double windMs, double radiusM) =>
        windMs > 0 && exitMs < 1.5 * windMs ? 2 * radiusM * (1.5 - Math.Max(exitMs, 0) / windMs) : 0;

    /// <summary>The share of its rise a puff has made at u = age / rise time: (u² / (1 + u²))^⅓, Briggs' u^⅔ at first,
    /// all of it later.</summary>
    public static double RiseShare(double u) => u > 0 ? Math.Cbrt(u * u / (1 + u * u)) : 0;

    // ------------------------------------------------------------------ the spread

    /// <summary>Taylor's spread by turbulence of σ and Lagrangian time T_L after t s, m²: 2σ²T_L²(t/T_L − 1 + e^(−t/T_L)).</summary>
    public static double Taylor(double t, double sigma, double lagrangianS)
    {
        if (!(t > 0) || !(sigma > 0) || !(lagrangianS > 0))
            return 0;
        double u = t / lagrangianS;
        return u < 1e-4 ? sigma * sigma * t * t * (1 - u / 3) : 2 * sigma * sigma * lagrangianS * lagrangianS * (u - 1 + Math.Exp(-u));
    }

    /// <summary>A puff's state at <paramref name="now"/> (world time, s), carried by the air's frame.</summary>
    public static PuffState State(in SmokePuff p, double now, in AirFrame frame, in SmokeAir air)
    {
        double age = Math.Max(now - p.Born, 0);
        double risen = p.Rise * RiseShare(age / p.RiseS), z = p.Z0 + risen, settled = p.Z0 + p.Rise;
        double b = risen * BuoyantSpread;
        double h2 = p.S0h2 + Taylor(age, air.SigmaV, air.LagrangianH) + b * b;
        double z2 = p.S0z2 + (p.SigmaW > 0 ? Taylor(age, p.SigmaW, p.LagrangianW) : Taylor(age, air.SigmaW(settled), air.LagrangianW(settled))) + b * b;
        double a = p.LifeS > 0 ? p.A0 * Math.Exp(-age / p.LifeS) : p.A0;
        return new PuffState(frame.ToWorld(p.Origin), z, h2, z2, a, age);
    }

    /// <summary>A puff's plan view at <paramref name="now"/>: its centre, its variance across, its extinction area and its
    /// age; what retiring and merging decide by, without the vertical's work (<see cref="State"/> has it all).</summary>
    public static void Plan(in SmokePuff p, double now, in AirFrame frame, in SmokeAir air, out Vec3d centre, out double sigmaH2,
        out double area, out double age)
    {
        age = Math.Max(now - p.Born, 0);
        double b = p.Rise * RiseShare(age / p.RiseS) * BuoyantSpread;
        sigmaH2 = p.S0h2 + Taylor(age, air.SigmaV, air.LagrangianH) + b * b;
        area = p.LifeS > 0 ? p.A0 * Math.Exp(-age / p.LifeS) : p.A0;
        centre = frame.ToWorld(p.Origin);
    }

    /// <summary>A puff born now at a place on the sea (<paramref name="at"/>, Earth-centred), into the air's frame.</summary>
    public static SmokePuff Born(Vec3d at, double now, in AirFrame frame, double z0, double riseM, double riseS, double sigma0H,
        double sigma0Z, double areaM2, double lifeS, SmokeTint tint, in SmokeAir air = default) => new()
        {
            Origin = frame.ToAir(at.Normalized() * Earth.Radius), Born = now, Z0 = (float)z0, Rise = (float)riseM,
            RiseS = (float)Math.Max(riseS, 0.1), S0h2 = (float)(sigma0H * sigma0H), S0z2 = (float)(sigma0Z * sigma0Z),
            A0 = (float)areaM2, LifeS = (float)lifeS, Tint = tint,
            SigmaW = air.FrictionMs > 0 ? (float)air.SigmaW(z0 + riseM) : 0,
            LagrangianW = air.FrictionMs > 0 ? (float)air.LagrangianW(z0 + riseM) : 0,
        };

    // ------------------------------------------------------------------ the queries

    /// <summary>The optical depth straight down through a puff over a point on the sea: A/(2πσ_h²)·e^(−r²/2σ_h²) (the
    /// sea's image folds what's under the sea back up, so the column holds A).</summary>
    public static double TauColumn(in PuffState p, Vec3d at)
    {
        double r2 = (at.Normalized() * Earth.Radius - p.Centre).LengthSquared;
        return p.Area / (2 * Math.PI * p.SigmaH2) * Math.Exp(-r2 / (2 * p.SigmaH2));
    }

    /// <summary>The optical depth a puff puts on a sight line, the line's end at the line's own height: the whitened 3D
    /// line integral of the puff and its image under the sea, the line's height and slope taken where it passes the
    /// puff (a puff is a few σ across, the line's curve kilometres).</summary>
    public static double TauLine(in SmokeLine l, in PuffState p) => TauLine(l, p, l.ToM);

    /// <summary>The same, the line ending <paramref name="toM"/> over the sea.</summary>
    public static double TauLine(in SmokeLine l, in PuffState p, double toM)
    {
        var rel = p.Centre - l.From;
        double s0 = Vec3d.Dot(rel, l.Along), d = Vec3d.Dot(rel, l.Side);
        double sh = Math.Sqrt(p.SigmaH2), reach = CullSigma * sh;
        if (Math.Abs(d) > reach || s0 < -reach || s0 > l.Length + reach || !(p.Area > 0))
            return 0;
        double sc = Math.Clamp(s0, 0, l.Length), slope = l.SlopeAt(sc, toM);
        double h = l.HeightAt(sc, toM) + slope * (s0 - sc);   // the line's height at the puff's place along it
        return Segment(d, h - p.Z, slope, s0, l.Length, p) + Segment(d, h + p.Z, slope, s0, l.Length, p);
    }

    /// <summary>A straight line through a puff: passing <paramref name="d"/> across and <paramref name="dz"/> above its
    /// centre at the centre's place along it, sloping <paramref name="slope"/>, from −s0 to L − s0 along it. In whitened
    /// space (each axis over its σ) the line is m + t·v, and τ = A·e^(−b²/2) / (2π σ_h² σ_z |v|) · ½[erf − erf].</summary>
    static double Segment(double d, double dz, double slope, double s0, double length, in PuffState p)
    {
        double iv2 = 1 / p.SigmaH2 + slope * slope / p.SigmaZ2, v = Math.Sqrt(iv2);
        double mv = dz * slope / p.SigmaZ2;
        double b2 = d * d / p.SigmaH2 + dz * dz / p.SigmaZ2 - mv * mv / iv2;
        if (b2 > 2 * CullSigma * CullSigma)
            return 0;
        double tStar = -mv / iv2;
        double a0 = v * (-s0 - tStar) / Math.Sqrt(2), a1 = v * (length - s0 - tStar) / Math.Sqrt(2);
        double frac = a0 < -CullSigma && a1 > CullSigma ? 1 : 0.5 * (Erf(a1) - Erf(a0));
        return p.Area * Math.Exp(-b2 / 2) / (2 * Math.PI * p.SigmaH2 * Math.Sqrt(p.SigmaZ2) * v) * frac;
    }

    /// <summary>A puff on several lines from one eye at once, one to each height <paramref name="toM"/>[j] at the line's
    /// end (a target's bands: hull, upper works, tops), added into <paramref name="tau"/>[j]. Near-horizontal lines, so
    /// the puff's plan part is worked once and its vertical Gaussian per band: within a fraction of a percent of
    /// <see cref="TauLine(in SmokeLine, in PuffState, double)"/> for a sight line's slopes of a few metres a kilometre.</summary>
    public static void TauBands(in SmokeLine l, in PuffState p, ReadOnlySpan<double> toM, Span<double> tau)
    {
        var rel = p.Centre - l.From;
        double s0 = Vec3d.Dot(rel, l.Along), d = Vec3d.Dot(rel, l.Side);
        double sh = Math.Sqrt(p.SigmaH2), reach = CullSigma * sh;
        if (Math.Abs(d) > reach || s0 < -reach || s0 > l.Length + reach || !(p.Area > 0))
            return;
        double a0 = -s0 / (Math.Sqrt(2) * sh), a1 = (l.Length - s0) / (Math.Sqrt(2) * sh);
        double frac = a0 < -CullSigma && a1 > CullSigma ? 1 : 0.5 * (Erf(a1) - Erf(a0));
        double plan = p.Area * Math.Exp(-d * d / (2 * p.SigmaH2)) / (2 * Math.PI * sh * Math.Sqrt(p.SigmaZ2)) * frac;
        if (plan <= 0)
            return;
        double sc = Math.Clamp(s0, 0, l.Length);
        for (int j = 0; j < toM.Length; j++)
        {
            double h = l.HeightAt(sc, toM[j]) + l.SlopeAt(sc, toM[j]) * (s0 - sc), lo = h - p.Z, hi = h + p.Z;
            tau[j] += plan * (Math.Exp(-lo * lo / (2 * p.SigmaZ2)) + Math.Exp(-hi * hi / (2 * p.SigmaZ2)));
        }
    }

    /// <summary>A puff on the lines from one place to another at several heights at each end: from each of
    /// <paramref name="fromM"/> (an observer's eyes) to each of <paramref name="toM"/> (a target's bands), added into
    /// <paramref name="tau"/>[i·toM.Length + j]. The line <paramref name="l"/> gives the places (its own heights unused).
    /// Near-horizontal lines: the puff's plan part worked once, its vertical Gaussian and the sea's image per pair of
    /// heights (<see cref="TauBands"/>'s). False when the puff is too far off the line to add anything.</summary>
    public static bool TauGrid(in SmokeLine l, in PuffState p, ReadOnlySpan<double> fromM, ReadOnlySpan<double> toM, Span<double> tau)
    {
        var rel = p.Centre - l.From;
        double s0 = Vec3d.Dot(rel, l.Along), d = Vec3d.Dot(rel, l.Side);
        double sh = Math.Sqrt(p.SigmaH2), reach = CullSigma * sh;
        if (Math.Abs(d) > reach || s0 < -reach || s0 > l.Length + reach || !(p.Area > 0))
            return false;
        double a0 = -s0 / (Math.Sqrt(2) * sh), a1 = (l.Length - s0) / (Math.Sqrt(2) * sh);
        double frac = a0 < -CullSigma && a1 > CullSigma ? 1 : 0.5 * (Erf(a1) - Erf(a0));
        double plan = p.Area * Math.Exp(-d * d / (2 * p.SigmaH2)) / (2 * Math.PI * sh * Math.Sqrt(p.SigmaZ2)) * frac;
        if (!(plan > 0))
            return false;
        double sc = Math.Clamp(s0, 0, l.Length), u = sc / l.Length, bow = sc * (l.Length - sc) * l.Curve, slopeBow = (l.Length - 2 * sc) * l.Curve;
        double inv2z = 1 / (2 * p.SigmaZ2), reachZ2 = CullSigma * CullSigma * p.SigmaZ2;
        for (int i = 0; i < fromM.Length; i++)
            for (int j = 0; j < toM.Length; j++)
            {
                double rise = toM[j] - fromM[i];
                double h = fromM[i] + rise * u - bow + (rise / l.Length - slopeBow) * (s0 - sc), lo = h - p.Z, hi = h + p.Z;
                double v = (lo * lo < reachZ2 ? Math.Exp(-lo * lo * inv2z) : 0) + (hi * hi < reachZ2 ? Math.Exp(-hi * hi * inv2z) : 0);
                tau[i * toM.Length + j] += plan * v;
            }
        return true;
    }

    /// <summary><see cref="TauGrid(in SmokeLine, in PuffState, ReadOnlySpan{double}, ReadOnlySpan{double}, Span{double})"/>
    /// on a readied puff: the same sums, no square roots or divisions per line.</summary>
    public static bool TauGrid(in SmokeLine l, in LinePuff p, ReadOnlySpan<double> fromM, ReadOnlySpan<double> toM, Span<double> tau)
    {
        var rel = p.Centre - l.From;
        double s0 = Vec3d.Dot(rel, l.Along), d = Vec3d.Dot(rel, l.Side);
        if (Math.Abs(d) > p.Reach || s0 < -p.Reach || s0 > l.Length + p.Reach || !(p.Plan > 0))
            return false;
        double a0 = -s0 * p.InvRoot2H, a1 = (l.Length - s0) * p.InvRoot2H;
        double frac = a0 < -CullSigma && a1 > CullSigma ? 1 : 0.5 * (Erf(a1) - Erf(a0));
        double plan = p.Plan * Math.Exp(-d * d * p.HalfInvH2) * frac;
        if (!(plan > 0))
            return false;
        double sc = Math.Clamp(s0, 0, l.Length), u = sc / l.Length, bow = sc * (l.Length - sc) * l.Curve, slopeBow = (l.Length - 2 * sc) * l.Curve;
        double off = s0 - sc, invL = 1 / l.Length;
        for (int i = 0; i < fromM.Length; i++)
            for (int j = 0; j < toM.Length; j++)
            {
                double rise = toM[j] - fromM[i];
                double h = fromM[i] + rise * u - bow + (rise * invL - slopeBow) * off, lo = h - p.Z, hi = h + p.Z;
                double v = (lo * lo < p.ReachZ2 ? Math.Exp(-lo * lo * p.HalfInvZ2) : 0) + (hi * hi < p.ReachZ2 ? Math.Exp(-hi * hi * p.HalfInvZ2) : 0);
                tau[i * toM.Length + j] += plan * v;
            }
        return true;
    }

    // ------------------------------------------------------------------ merging

    /// <summary>Two puffs as one at <paramref name="now"/>, keeping their extinction, their centre and their second
    /// moments (across: half the spread of the centres per axis; up: the spread of the heights), and the rise still to
    /// come. The merged puff carries on from their A-weighted age: Taylor's growth is convex in age, so what the mean age
    /// leaves of the variance is never negative, and is folded into its s0².</summary>
    public static SmokePuff Merge(in SmokePuff p, in PuffState sp, in SmokePuff q, in PuffState sq, double now, in AirFrame frame,
        in SmokeAir air)
    {
        double a = sp.Area + sq.Area;
        if (!(a > 0))
            return p;
        double wp = sp.Area / a, wq = sq.Area / a;
        var c = (sp.Centre * wp + sq.Centre * wq).Normalized() * Earth.Radius;
        double h2 = wp * (sp.SigmaH2 + 0.5 * (sp.Centre - c).LengthSquared) + wq * (sq.SigmaH2 + 0.5 * (sq.Centre - c).LengthSquared);
        double z = wp * sp.Z + wq * sq.Z;
        double z2 = wp * (sp.SigmaZ2 + (sp.Z - z) * (sp.Z - z)) + wq * (sq.SigmaZ2 + (sq.Z - z) * (sq.Z - z));
        double age = wp * sp.Age + wq * sq.Age;
        double left = wp * p.Rise * (1 - RiseShare(sp.Age / p.RiseS)) + wq * q.Rise * (1 - RiseShare(sq.Age / q.RiseS));
        double riseS = wp * p.RiseS + wq * q.RiseS, share = RiseShare(age / riseS);
        double rise = left > 0.5 && share < 0.99 ? left / (1 - share) : 0, z0 = z - rise * share, settled = z0 + rise;
        double b = rise * share * BuoyantSpread;
        double life = p.LifeS > 0 && q.LifeS > 0 ? wp * p.LifeS + wq * q.LifeS : 0;
        return new SmokePuff
        {
            Origin = frame.ToAir(c), Born = now - age, Z0 = (float)z0, Rise = (float)rise, RiseS = (float)riseS,
            S0h2 = (float)Math.Max(h2 - Taylor(age, air.SigmaV, air.LagrangianH) - b * b, 0),
            S0z2 = (float)Math.Max(z2 - Taylor(age, air.SigmaW(settled), air.LagrangianW(settled)) - b * b, 0),
            A0 = (float)(life > 0 ? a * Math.Exp(age / life) : a), LifeS = (float)life,
            SigmaW = air.FrictionMs > 0 ? (float)air.SigmaW(settled) : 0, LagrangianW = air.FrictionMs > 0 ? (float)air.LagrangianW(settled) : 0,
            Tint = wp >= wq ? p.Tint : q.Tint,
        };
    }
}
