using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

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
/// (<see cref="SmokeModel.State"/>), so nothing steps it. Where it is comes from the air's frame, which moves with the
/// wind at the steering height, and its own drift along the wind against it: the wind at its height less the frame's
/// (the sea's wind profile: low smoke lags, high smoke runs ahead).</summary>
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
    /// <summary>Its variances at birth (and what a merge folded in), across and up, m²: the plan view's round part (what
    /// the turbulence adds to).</summary>
    public float S0h2, S0z2;
    /// <summary>Its stretch (docs/battle/smoke.md Z5b): the plan view's variance more along <see cref="Axis"/>, m² (0:
    /// round), and that axis, a unit vector on the sea in the air's frame. Any 2×2 covariance is a round part and one
    /// stretch, so a merge keeps the plan's second moments exactly.</summary>
    public float Along2;
    public Vec3d Axis;
    /// <summary>The large eddies' hold on it (docs/battle/smoke.md Z5c): where it samples their field (its birthplace in
    /// the air's frame, a merge's A-weighted) and its share of their strength, the air's σ_v at its birth against
    /// <see cref="SmokeEddies.ReferenceSigmaV"/> (0: none). It's offset by Eddy · f(Key, t) from its origin, the origin
    /// set so it's where it was laid (or merged).</summary>
    public Vec3d Key;
    public float Eddy;
    /// <summary>Its modes' phases where it samples the eddies, cos and sin of k_i·Key + φ_i (set with the key: its offset
    /// then takes no trigonometry, <see cref="SmokeEddies.At(in EddyPhases, double)"/>).</summary>
    public EddyPhases Phases;
    /// <summary>Its extinction area at birth, m², and the time it decays over, s (0: it doesn't).</summary>
    public float A0, LifeS;
    /// <summary>The air's turbulence up at its settled height, σ_w, m/s, and its Lagrangian time, s, as the air was when
    /// it was born or merged (0: read the air's now). Kept so its state is a few multiplies and two exponentials.</summary>
    public float SigmaW, LagrangianW;
    /// <summary>Its drift along the wind against the air's frame, m/s: the wind at its settled height less the steering
    /// height's (<see cref="SmokeAir.ShearAt"/>), as the air was when it was born or merged; and the world time, s, it's
    /// counted from (its birth, or when its drift so far was last folded into its origin: the store does it as its walk
    /// passes, so the drift follows a wind that turns).</summary>
    public float ShearMs;
    public double ShearFromS;
    public SmokeTint Tint;
}

/// <summary>A puff at an age: its centre on the sea (Earth-centred, sea level), its height over the sea, m, its
/// variances across (the plan's round part) and up, m², its extinction area, m², its age, s, and its stretch: the plan's
/// variance more along <paramref name="Axis"/> (a unit vector on the sea, Earth-centred), m².</summary>
public readonly record struct PuffState(Vec3d Centre, double Z, double SigmaH2, double SigmaZ2, double Area, double Age, double Along2 = 0,
    Vec3d Axis = default)
{
    /// <summary>The plan's variance along its long axis, m².</summary>
    public double Major2 => SigmaH2 + Along2;
}

/// <summary>A puff readied for many sight lines (the index's, docs/battle/smoke.md Z2): its state with the square roots
/// and normalisations worked once, for <see cref="SmokeModel.TauGrid(in SmokeLine, in LinePuff, ReadOnlySpan{double},
/// ReadOnlySpan{double}, Span{double})"/>.</summary>
public readonly struct LinePuff
{
    public readonly Vec3d Centre, Axis;
    /// <summary>Its height, m; σ_h (the round part), m; how far off a line it can add anything (3.5 σ along its long
    /// axis), m; A / (2π √det Σ σ_z); 1/σ_h²; its stretch's share σ_a² / (σ_h² + σ_a²); 1/(2σ_z²); (3.5 σ_z)²;
    /// 1/(√2 σ_h).</summary>
    public readonly double Z, SigmaH, Reach, Norm, InvH2, Kappa, HalfInvZ2, ReachZ2, InvRoot2H;

    public LinePuff(in PuffState p, Vec3d centre, Vec3d axis)
    {
        double sh = Math.Sqrt(p.SigmaH2), sz = Math.Sqrt(p.SigmaZ2), al2 = Math.Max(p.Along2, 0);
        (Centre, Axis, Z, SigmaH, Reach) = (centre, axis, p.Z, sh, SmokeModel.CullSigma * Math.Sqrt(p.SigmaH2 + al2));
        Norm = p.Area > 0 ? p.Area / (2 * Math.PI * Math.Sqrt(p.SigmaH2 * (p.SigmaH2 + al2)) * sz) : 0;
        (InvH2, Kappa) = (1 / p.SigmaH2, al2 > 0 ? al2 / (p.SigmaH2 + al2) : 0);
        (HalfInvZ2, ReachZ2, InvRoot2H) = (1 / (2 * p.SigmaZ2), SmokeModel.CullSigma * SmokeModel.CullSigma * p.SigmaZ2, 1 / (Math.Sqrt(2) * sh));
    }

    /// <summary>A puff on lines in the Earth-centred frame its state is in.</summary>
    public LinePuff(in PuffState p) : this(p, p.Centre, p.Axis) { }
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

    /// <summary>The rotation's rows (world = rows · air), for the drawing.</summary>
    public readonly (Vec3d R0, Vec3d R1, Vec3d R2) Rows => (new(m00, m01, m02), new(m10, m11, m12), new(m20, m21, m22));

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
    /// <summary>The large eddies (docs/battle/smoke.md Z5c), the field's own; none in plain air (tests, the drawing's copy
    /// of a puff's place without them).</summary>
    public SmokeEddies? Eddies { get; init; }
    /// <summary>The way the wind blows at the air frame's reference point, a unit vector (Earth-centred), when the field
    /// has set it (zero: worked out at each puff): a puff's drift takes it onto its own tangent, a few multiplies.</summary>
    public Vec3d WindDir { get; init; }

    /// <summary>The way the wind blows at a point on the sea (|c| the Earth's radius), a tangent vector, near unit.</summary>
    public Vec3d WindAt(Vec3d c)
    {
        if (WindDir.LengthSquared == 0)
            return Earth.Forward(c, WindToDeg);
        var up = c * (1 / Earth.Radius);
        return WindDir - up * Vec3d.Dot(up, WindDir);
    }

    public const double DragCoefficient = 1.2e-3, SigmaVPerUstar = 1.9, SigmaWPerUstar = 1.25, LagrangianHS = 20;
    /// <summary>The height the smoke drifts at (the wind there by a sea profile, power 0.11), m; the surface layer's top
    /// (K_z stops growing above it), m; the air's temperature when the weather doesn't give one, K.</summary>
    public const double SteeringM = 50, ProfilePower = 0.11, SurfaceLayerM = 100, DefaultAirK = 288;
    /// <summary>The calmest wind the turbulence is worked from, m/s (smoke still spreads in a calm).</summary>
    public const double CalmMs = 0.5;

    /// <summary>The highest the sea's wind profile is taken up to, m (smoke rises to a few hundred at most).</summary>
    public const double ProfileTopM = 300;

    /// <summary>The wind at a height, m/s: the 10 m wind on the sea's profile (power <see cref="ProfilePower"/>), from 2 m
    /// to <see cref="ProfileTopM"/>.</summary>
    public double DriftAt(double z) => Math.Max(WindMs, 0) * Math.Pow(Math.Clamp(z, 2, ProfileTopM) / 10, ProfilePower);

    /// <summary>Smoke at a height drifts this much faster along the wind than the air's frame (which moves at the steering
    /// height's), m/s: negative below it, positive above (8 m/s at 10 m: -0.9 at 20 m, +0.8 at 100 m).</summary>
    public double ShearAt(double z) => DriftAt(z) - DriftMs;

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
        return new PuffState(Centre(p, now, frame, air), z, h2, z2, a, age, p.Along2, p.Along2 > 0 ? frame.ToWorld(p.Axis) : default);
    }

    /// <summary>A puff's centre on the sea at <paramref name="now"/> (world s): its origin carried by the air's frame, and
    /// on along the wind by its own drift against the frame since <see cref="SmokePuff.ShearFromS"/> (the wind's way now:
    /// the store folds the drift into the origin every walk, so a turning wind bends it no more than a walk's worth).</summary>
    public static Vec3d Centre(in SmokePuff p, double now, in AirFrame frame, in SmokeAir air)
    {
        var c = frame.ToWorld(p.Eddy > 0 && air.Eddies is { } e ? p.Origin + e.At(p.Phases, now) * p.Eddy : p.Origin);
        return p.ShearMs != 0 ? c + air.WindAt(c) * (p.ShearMs * Math.Max(now - p.ShearFromS, 0)) : c;
    }

    /// <summary>The origin that puts a puff at <paramref name="centre"/> (Earth-centred) at <paramref name="now"/>: the
    /// place in the air's frame less its eddies' offset then.</summary>
    static Vec3d OriginFor(Vec3d centre, in EddyPhases phases, double eddy, double now, in AirFrame frame, in SmokeAir air)
    {
        var at = frame.ToAir(centre.Normalized() * Earth.Radius);
        return eddy > 0 && air.Eddies is { } e ? at - e.At(phases, now) * eddy : at;
    }

    /// <summary>A puff's drift so far folded into its origin, counted afresh from <paramref name="now"/>: it stays where
    /// it is.</summary>
    public static void Reanchor(ref SmokePuff p, double now, in AirFrame frame, in SmokeAir air) =>
        Reanchor(ref p, now, frame, air, Centre(p, now, frame, air));

    /// <summary>The same, its centre now already worked out (<see cref="Plan"/>'s).</summary>
    public static void Reanchor(ref SmokePuff p, double now, in AirFrame frame, in SmokeAir air, Vec3d centre)
    {
        if (p.ShearMs == 0 || !(now > p.ShearFromS))
            return;
        p.Origin = OriginFor(centre, p.Phases, p.Eddy, now, frame, air);
        p.ShearFromS = now;
    }

    /// <summary>A puff's plan view at <paramref name="now"/>: its centre, its variance across (the round part), its stretch
    /// and its axis (Earth-centred), its extinction area and its age; what retiring and merging decide by, without the
    /// vertical's work (<see cref="State"/> has it all).</summary>
    public static void Plan(in SmokePuff p, double now, in AirFrame frame, in SmokeAir air, out Vec3d centre, out double sigmaH2,
        out double along2, out Vec3d axis, out double area, out double age)
    {
        age = Math.Max(now - p.Born, 0);
        double b = p.Rise * RiseShare(age / p.RiseS) * BuoyantSpread;
        sigmaH2 = p.S0h2 + Taylor(age, air.SigmaV, air.LagrangianH) + b * b;
        area = p.LifeS > 0 ? p.A0 * Math.Exp(-age / p.LifeS) : p.A0;
        centre = Centre(p, now, frame, air);
        along2 = p.Along2;
        axis = p.Along2 > 0 ? frame.ToWorld(p.Axis) : default;
    }

    /// <summary>A plan view's peak column τ: A / (2π √det Σ).</summary>
    public static double Peak(double area, double sigmaH2, double along2) => area / (2 * Math.PI * Math.Sqrt(sigmaH2 * (sigmaH2 + along2)));

    /// <summary>A plan view's variance along a way on the sea (<paramref name="way"/>, any length but not zero), m².</summary>
    public static double VarianceAlong(double sigmaH2, double along2, Vec3d axis, Vec3d way)
    {
        if (!(along2 > 0))
            return sigmaH2;
        double c = Vec3d.Dot(axis, way);
        return sigmaH2 + along2 * c * c / way.LengthSquared;
    }

    /// <summary>A puff born now at a place on the sea (<paramref name="at"/>, Earth-centred), into the air's frame; its
    /// plan view stretched by <paramref name="along2"/>, m², along <paramref name="axis"/> (a way on the sea, Earth-centred:
    /// a piece of a trail laid along her run through the air).</summary>
    public static SmokePuff Born(Vec3d at, double now, in AirFrame frame, double z0, double riseM, double riseS, double sigma0H,
        double sigma0Z, double areaM2, double lifeS, SmokeTint tint, in SmokeAir air = default, double along2 = 0, Vec3d axis = default)
    {
        var up = at.Normalized();
        var way = axis - up * Vec3d.Dot(axis, up);
        bool stretched = along2 > 0 && way.LengthSquared > 1e-12;
        var place = frame.ToAir(up * Earth.Radius);
        double eddy = air.Eddies != null ? SmokeEddies.Share(air) : 0;
        var phases = new EddyPhases();
        air.Eddies?.PhasesAt(place, ref phases);
        return new()
        {
            Origin = eddy > 0 ? place - air.Eddies!.At(phases, now) * eddy : place, Key = place, Eddy = (float)eddy, Phases = phases,
            Born = now, Z0 = (float)z0, Rise = (float)riseM,
            RiseS = (float)Math.Max(riseS, 0.1), S0h2 = (float)(sigma0H * sigma0H), S0z2 = (float)(sigma0Z * sigma0Z),
            A0 = (float)areaM2, LifeS = (float)lifeS, Tint = tint,
            SigmaW = air.FrictionMs > 0 ? (float)air.SigmaW(z0 + riseM) : 0,
            LagrangianW = air.FrictionMs > 0 ? (float)air.LagrangianW(z0 + riseM) : 0,
            ShearMs = (float)air.ShearAt(z0 + riseM), ShearFromS = now,
            Along2 = stretched ? (float)along2 : 0, Axis = stretched ? frame.ToAir(way.Normalized()) : default,
        };
    }

    // ------------------------------------------------------------------ the queries

    /// <summary>The plan view's inverse covariance on a line's axes (along it, across it): the round part's 1/σ_h², less
    /// the stretch's share κ = σ_a²/(σ_h² + σ_a²) along its axis.</summary>
    static void PlanInverse(double invH2, double kappa, Vec3d axis, Vec3d along, Vec3d side, out double paa, out double pas, out double pss)
    {
        if (!(kappa > 0))
        {
            (paa, pas, pss) = (invH2, 0, invH2);
            return;
        }
        double ea = Vec3d.Dot(axis, along), es = Vec3d.Dot(axis, side);
        (paa, pas, pss) = ((1 - kappa * ea * ea) * invH2, -kappa * ea * es * invH2, (1 - kappa * es * es) * invH2);
    }

    static double Kappa(in PuffState p) => p.Along2 > 0 ? p.Along2 / (p.SigmaH2 + p.Along2) : 0;

    /// <summary>The optical depth straight down through a puff over a point on the sea: A/(2π√det Σ)·e^(−rᵀΣ⁻¹r/2) (the
    /// sea's image folds what's under the sea back up, so the column holds A).</summary>
    public static double TauColumn(in PuffState p, Vec3d at)
    {
        var r = at.Normalized() * Earth.Radius - p.Centre;
        double r2 = r.LengthSquared, ra = p.Along2 > 0 ? Vec3d.Dot(r, p.Axis) : 0;
        return Peak(p.Area, p.SigmaH2, p.Along2) * Math.Exp(-(r2 - Kappa(p) * ra * ra) / (2 * p.SigmaH2));
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
        double reach = CullSigma * Math.Sqrt(p.Major2);
        if (Math.Abs(d) > reach || s0 < -reach || s0 > l.Length + reach || !(p.Area > 0))
            return 0;
        PlanInverse(1 / p.SigmaH2, Kappa(p), p.Axis, l.Along, l.Side, out double paa, out double pas, out double pss);
        double sc = Math.Clamp(s0, 0, l.Length), slope = l.SlopeAt(sc, toM);
        double h = l.HeightAt(sc, toM) + slope * (s0 - sc);   // the line's height at the puff's place along it
        double norm = p.Area / (2 * Math.PI * Math.Sqrt(p.SigmaH2 * p.Major2 * p.SigmaZ2));
        return norm * (Segment(d, h - p.Z, slope, s0, l.Length, paa, pas, pss, p.SigmaZ2)
            + Segment(d, h + p.Z, slope, s0, l.Length, paa, pas, pss, p.SigmaZ2));
    }

    /// <summary>A straight line through a puff, over its normalisation: passing <paramref name="d"/> across and
    /// <paramref name="dz"/> above its centre at the centre's place along it, sloping <paramref name="slope"/>, from −s0
    /// to L − s0 along it; the plan's inverse covariance on the line's axes. The quadratic form along the line is
    /// a·x² + 2b·x + c, and its integral e^(−(c − b²/a)/2) / √a · √(π/2) [erf − erf] (whitened, closed form).</summary>
    static double Segment(double d, double dz, double slope, double s0, double length, double paa, double pas, double pss, double z2)
    {
        // the line passes −d across: x along it from the puff's place, the plan offset (x, −d), the height dz + slope·x
        double a = paa + slope * slope / z2, b = -pas * d + dz * slope / z2, c = pss * d * d + dz * dz / z2;
        double b2 = c - b * b / a;
        if (b2 > 2 * CullSigma * CullSigma)
            return 0;
        double tStar = -b / a, v = Math.Sqrt(a);
        double a0 = v * (-s0 - tStar) / Math.Sqrt(2), a1 = v * (length - s0 - tStar) / Math.Sqrt(2);
        double frac = a0 < -CullSigma && a1 > CullSigma ? 1 : 0.5 * (Erf(a1) - Erf(a0));
        return Math.Exp(-b2 / 2) / v * frac;
    }

    /// <summary>A puff on several lines from one eye at once, one to each height <paramref name="toM"/>[j] at the line's
    /// end (a target's bands: hull, upper works, tops), added into <paramref name="tau"/>[j]. Near-horizontal lines, so
    /// the puff's plan part is worked once and its vertical Gaussian per band: within a fraction of a percent of
    /// <see cref="TauLine(in SmokeLine, in PuffState, double)"/> for a sight line's slopes of a few metres a kilometre.</summary>
    public static void TauBands(in SmokeLine l, in PuffState p, ReadOnlySpan<double> toM, Span<double> tau)
    {
        Span<double> from = stackalloc double[1];
        from[0] = l.FromM;
        TauGrid(l, new LinePuff(p), from, toM, tau);
    }

    /// <summary>A puff on the lines from one place to another at several heights at each end: from each of
    /// <paramref name="fromM"/> (an observer's eyes) to each of <paramref name="toM"/> (a target's bands), added into
    /// <paramref name="tau"/>[i·toM.Length + j]. The line <paramref name="l"/> gives the places (its own heights unused).
    /// Near-horizontal lines: the puff's plan part worked once, its vertical Gaussian and the sea's image per pair of
    /// heights (<see cref="TauBands"/>'s). False when the puff is too far off the line to add anything.</summary>
    public static bool TauGrid(in SmokeLine l, in PuffState p, ReadOnlySpan<double> fromM, ReadOnlySpan<double> toM, Span<double> tau) =>
        TauGrid(l, new LinePuff(p), fromM, toM, tau);

    /// <summary><see cref="TauGrid(in SmokeLine, in PuffState, ReadOnlySpan{double}, ReadOnlySpan{double}, Span{double})"/>
    /// on a readied puff: the same sums, no square roots or divisions per line for a round one (a stretched one takes its
    /// axis onto the line's and a square root). The plan's integral along the line (its closest pass x* along from the
    /// centre's place, where the vertical is taken: a stretched puff crossed aslant is nearest off its centre).</summary>
    public static bool TauGrid(in SmokeLine l, in LinePuff p, ReadOnlySpan<double> fromM, ReadOnlySpan<double> toM, Span<double> tau)
    {
        var rel = p.Centre - l.From;
        double s0 = Vec3d.Dot(rel, l.Along), d = Vec3d.Dot(rel, l.Side);
        if (Math.Abs(d) > p.Reach || s0 < -p.Reach || s0 > l.Length + p.Reach || !(p.Norm > 0))
            return false;
        double root, norm, xs, q;
        if (p.Kappa > 0)
        {
            PlanInverse(p.InvH2, p.Kappa, p.Axis, l.Along, l.Side, out double paa, out double pas, out double pss);
            double inv = 1 / paa;
            (root, norm, xs) = (Math.Sqrt(0.5 * paa), p.Norm * Math.Sqrt(inv), pas * d * inv);
            q = (pss - pas * pas * inv) * d * d;
        }
        else
            (root, norm, xs, q) = (p.InvRoot2H, p.Norm * p.SigmaH, 0, d * d * p.InvH2);
        double a0 = (-s0 - xs) * root, a1 = (l.Length - s0 - xs) * root;
        double frac = a0 < -CullSigma && a1 > CullSigma ? 1 : 0.5 * (Erf(a1) - Erf(a0));
        double plan = norm * Math.Exp(-0.5 * q) * frac;
        if (!(plan > 0))
            return false;
        double sm = s0 + xs, sc = Math.Clamp(sm, 0, l.Length), u = sc / l.Length, bow = sc * (l.Length - sc) * l.Curve, slopeBow = (l.Length - 2 * sc) * l.Curve;
        double off = sm - sc, invL = 1 / l.Length;
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

    // ------------------------------------------------------------------ a pair's lines (docs/battle/smoke.md Z3)

    /// <summary>The most eye rows and bands a pair's answer holds.</summary>
    public const int PairEyes = 4, PairBands = 3;

    /// <summary>The eye a pair's row is worked out from, of an observer's eyes (her class's, highest first): all of them up
    /// to <see cref="PairEyes"/>, else her highest, her lowest and evenly between, so every eye's depths are a row's or
    /// between two.</summary>
    public static int PairEye(int row, int eyes) => eyes <= PairEyes ? row : (int)Math.Round(row * (eyes - 1) / (double)(PairEyes - 1));

    /// <summary>An eye's depths to the target's bands from a pair's answer (eye rows by <see cref="PairEye"/>, eye-major):
    /// a row's when it's one, else between the two rows its height lies between (held past the highest and the lowest).</summary>
    public static SmokeTau EyeTau(ReadOnlySpan<double> pair, ReadOnlySpan<EyeRow> eyes, double eyeM)
    {
        int rows = Math.Min(eyes.Length, PairEyes);
        if (rows == 0)
            return default;
        int i = 0;
        double w = 0;
        if (eyeM <= eyes[PairEye(rows - 1, eyes.Length)].HeightM)
            i = rows - 1;
        else if (eyeM < eyes[PairEye(0, eyes.Length)].HeightM)
            for (; i < rows - 1; i++)
            {
                double hi = eyes[PairEye(i, eyes.Length)].HeightM, lo = eyes[PairEye(i + 1, eyes.Length)].HeightM;
                if (eyeM >= lo)
                {
                    w = hi > lo ? (hi - eyeM) / (hi - lo) : 0;
                    break;
                }
            }
        var a = pair.Slice(i * PairBands, PairBands);
        if (w == 0)
            return new SmokeTau(a[0], a[1], a[2]);
        var b = pair.Slice((i + 1) * PairBands, PairBands);
        return new SmokeTau(a[0] + w * (b[0] - a[0]), a[1] + w * (b[1] - a[1]), a[2] + w * (b[2] - a[2]));
    }

    // ------------------------------------------------------------------ merging

    /// <summary>Two puffs as one at <paramref name="now"/>, keeping their extinction, their centre and their second
    /// moments (across: the plan's whole 2×2 covariance, the spread of the centres in it, as a round part and a stretch;
    /// up: the spread of the heights), and the rise still to come. The merged puff carries on from their A-weighted age:
    /// Taylor's growth is convex in age, so what the mean age leaves of the round part is never negative, and is folded
    /// into its s0².</summary>
    public static SmokePuff Merge(in SmokePuff p, in PuffState sp, in SmokePuff q, in PuffState sq, double now, in AirFrame frame,
        in SmokeAir air)
    {
        double a = sp.Area + sq.Area;
        if (!(a > 0))
            return p;
        double wp = sp.Area / a, wq = sq.Area / a;
        var c = MergedPlan(sp.Centre, sp.SigmaH2, sp.Along2, sp.Axis, sp.Area, sq.Centre, sq.SigmaH2, sq.Along2, sq.Axis, sq.Area,
            out double round, out double along, out var axis);
        double z = wp * sp.Z + wq * sq.Z;
        double z2 = wp * (sp.SigmaZ2 + (sp.Z - z) * (sp.Z - z)) + wq * (sq.SigmaZ2 + (sq.Z - z) * (sq.Z - z));
        double age = wp * sp.Age + wq * sq.Age;
        double left = wp * p.Rise * (1 - RiseShare(sp.Age / p.RiseS)) + wq * q.Rise * (1 - RiseShare(sq.Age / q.RiseS));
        double riseS = wp * p.RiseS + wq * q.RiseS, share = RiseShare(age / riseS);
        double rise = left > 0.5 && share < 0.99 ? left / (1 - share) : 0, z0 = z - rise * share, settled = z0 + rise;
        double b = rise * share * BuoyantSpread;
        double life = p.LifeS > 0 && q.LifeS > 0 ? wp * p.LifeS + wq * q.LifeS : 0;
        // its drift A-weighted (the centroid's), counted from now; its hold on the eddies A-weighted, its origin then
        // putting it at the centroid
        double shear = wp * p.ShearMs + wq * q.ShearMs, eddy = wp * p.Eddy + wq * q.Eddy;
        var key = (p.Key * wp + q.Key * wq).Normalized() * Earth.Radius;
        var phases = new EddyPhases();
        air.Eddies?.PhasesAt(key, ref phases);
        return new SmokePuff
        {
            Origin = OriginFor(c, phases, eddy, now, frame, air), Key = key, Eddy = (float)eddy, Phases = phases, Born = now - age, ShearMs = (float)shear, ShearFromS = now, Z0 = (float)z0, Rise = (float)rise, RiseS = (float)riseS,
            S0h2 = (float)Math.Max(round - Taylor(age, air.SigmaV, air.LagrangianH) - b * b, 0),
            S0z2 = (float)Math.Max(z2 - Taylor(age, air.SigmaW(settled), air.LagrangianW(settled)) - b * b, 0),
            Along2 = (float)along, Axis = along > 0 ? frame.ToAir(axis) : default,
            A0 = (float)(life > 0 ? a * Math.Exp(age / life) : a), LifeS = (float)life,
            SigmaW = air.FrictionMs > 0 ? (float)air.SigmaW(settled) : 0, LagrangianW = air.FrictionMs > 0 ? (float)air.LagrangianW(settled) : 0,
            Tint = wp >= wq ? p.Tint : q.Tint,
        };
    }

    /// <summary>Two plan views as one (the merge's): the A-weighted centre (returned, on the sea), and the second moments
    /// about it (each one's covariance and its centre's offset) as a round part, a stretch and its axis (Earth-centred).
    /// Cheap enough to try before a merge.</summary>
    public static Vec3d MergedPlan(Vec3d cp, double hp, double lp, Vec3d xp, double ap, Vec3d cq, double hq, double lq, Vec3d xq, double aq,
        out double round, out double along, out Vec3d axis)
    {
        double a = ap + aq, wp = a > 0 ? ap / a : 0.5, wq = 1 - wp;
        var c = (cp * wp + cq * wq).Normalized() * Earth.Radius;
        // the plan's second moments about c on a basis of the sea there
        var up = c * (1 / Earth.Radius);
        var e1 = Vec3d.Cross(Math.Abs(up.Z) < 0.9 ? new Vec3d(0, 0, 1) : new Vec3d(1, 0, 0), up).Normalized();
        var e2 = Vec3d.Cross(up, e1);
        double sxx = 0, sxy = 0, syy = 0;
        Moments(cp, hp, lp, xp, wp, c, e1, e2, ref sxx, ref sxy, ref syy);
        Moments(cq, hq, lq, xq, wq, c, e1, e2, ref sxx, ref sxy, ref syy);
        double mid = (sxx + syy) / 2, half = Math.Sqrt((sxx - syy) * (sxx - syy) / 4 + sxy * sxy);
        (round, along, axis) = (mid - half, 2 * half, default);
        if (along > 1e-6 * mid)
        {
            // the larger eigenvalue's vector, from the better-conditioned of its two forms
            double hi = mid + half;
            var (ax, ay) = Math.Abs(hi - sxx) > Math.Abs(hi - syy) ? (sxy, hi - sxx) : (hi - syy, sxy);
            axis = (e1 * ax + e2 * ay).Normalized();
        }
        else
            along = 0;
        return c;
    }

    /// <summary>A plan view's share of a merge's moments about c (weight w): its own covariance and its centre's offset.</summary>
    static void Moments(Vec3d centre, double h2, double l2, Vec3d axis, double w, Vec3d c, Vec3d e1, Vec3d e2, ref double sxx, ref double sxy,
        ref double syy)
    {
        var d = centre - c;
        double dx = Vec3d.Dot(d, e1), dy = Vec3d.Dot(d, e2);
        double ax = l2 > 0 ? Vec3d.Dot(axis, e1) : 0, ay = l2 > 0 ? Vec3d.Dot(axis, e2) : 0;
        sxx += w * (h2 + l2 * ax * ax + dx * dx);
        sxy += w * (l2 * ax * ay + dx * dy);
        syy += w * (h2 + l2 * ay * ay + dy * dy);
    }
}

/// <summary>The large eddies (docs/battle/smoke.md Z5c): the boundary layer's and the mesoscale's, kilometres across,
/// which a puff's own spread (Taylor's, the surface layer's small eddies) doesn't hold; they make a plume snake and break
/// into clumps. A smooth displacement field of the air's frame and world time, f(x, t) = Σ A_i d_i cos(k_i·x − ω_i t + φ_i):
/// <see cref="Modes"/> transverse waves on the sea's plane at the field's reference point (each d_i across its k_i, so
/// the field is divergence-free: it stretches and folds a trail but never piles smoke up), wavelengths log-even over a
/// range, ways and phases drawn from the world's seed, each turning over in its wavelength at a speed, its amplitude its
/// strain times its wavelength over 2π. Closed form in time: the same at any speed and on replay. A puff takes
/// Eddy · [f(Key, t) − f(Key, born)], so young smoke is still at the ship and neighbours laid close together move
/// together. The drawing's twin is smokefield.glsl's fieldEddy. [EST: ~300 m of meander at saturation in 8 m/s; research
/// requested]</summary>
public sealed class SmokeEddies
{
    public const int Modes = 8;

    /// <summary>The air the eddies' strength is given for: neutral 8 m/s's σ_v, m/s.</summary>
    public static readonly double ReferenceSigmaV = SmokeAir.SigmaVPerUstar * Math.Sqrt(SmokeAir.DragCoefficient) * 8;

    /// <summary>Each mode's wave vector in the air's frame, rad/m, and the way it displaces (a unit vector across it on the
    /// sea's plane); its amplitude, m, its angular speed, rad/s, and its phase, rad.</summary>
    public readonly Vec3d[] K = new Vec3d[Modes], Dir = new Vec3d[Modes];
    public readonly double[] Amp = new double[Modes], Omega = new double[Modes], Phase = new double[Modes];

    /// <summary>The sea's up at the reference point, in the air's frame.</summary>
    public Vec3d Up { get; }

    public SmokeEddies(Vec3d up, double shortM, double longM, double strain, double speedMs, ulong seed)
    {
        Up = up.Normalized();
        var east = Vec3d.Cross(Math.Abs(Up.Z) < 0.9 ? new Vec3d(0, 0, 1) : new Vec3d(1, 0, 0), Up).Normalized();
        var north = Vec3d.Cross(Up, east);
        var r = RandomStream.Named(seed, "SmokeEddies");
        for (int i = 0; i < Modes; i++)
        {
            // log-even over the range, each jittered by up to a third of a step
            double u = (i + r.NextDouble(-0.33, 0.33)) / (Modes - 1);
            double lambda = shortM * Math.Pow(longM / shortM, Math.Clamp(u, 0, 1)), k = 2 * Math.PI / lambda, way = 2 * Math.PI * r.NextDouble();
            var kd = east * Math.Cos(way) + north * Math.Sin(way);
            K[i] = kd * k;
            Dir[i] = Vec3d.Cross(Up, kd);
            Amp[i] = strain / k;
            Omega[i] = speedMs * k;
            Phase[i] = 2 * Math.PI * r.NextDouble();
        }
    }

    /// <summary>The field at a place in the air's frame at a world time, s: a displacement on the sea, m (air's frame).</summary>
    public Vec3d At(Vec3d key, double t)
    {
        Vec3d sum = default;
        for (int i = 0; i < Modes; i++)
            sum += Dir[i] * (Amp[i] * Math.Cos(Vec3d.Dot(K[i], key) - Omega[i] * t + Phase[i]));
        return sum;
    }

    /// <summary>A place's modes' phases (cos and sin of k_i·key + φ_i), for <see cref="At(in EddyPhases, double)"/>.</summary>
    public void PhasesAt(Vec3d key, ref EddyPhases phases)
    {
        for (int i = 0; i < Modes; i++)
        {
            var (sn, cs) = Math.SinCos(Vec3d.Dot(K[i], key) + Phase[i]);
            (phases[2 * i], phases[2 * i + 1]) = ((float)cs, (float)sn);
        }
    }

    // the clock's turn, cos and sin of ω_i t, for the last time asked (the sim's thread's)
    readonly double[] turnCos = new double[Modes], turnSin = new double[Modes];
    double turnT = double.NaN;

    /// <summary>The field at a place by its phases (<see cref="PhasesAt"/>): cos(a − ωt) = cos a cos ωt + sin a sin ωt,
    /// the clock's turn worked once a time. The same as <see cref="At(Vec3d, double)"/> within the phases' float.</summary>
    public Vec3d At(in EddyPhases phases, double t)
    {
        if (t != turnT)
        {
            for (int i = 0; i < Modes; i++)
                (turnSin[i], turnCos[i]) = Math.SinCos(Omega[i] * t);
            turnT = t;
        }
        Vec3d sum = default;
        for (int i = 0; i < Modes; i++)
            sum += Dir[i] * (Amp[i] * (phases[2 * i] * turnCos[i] + phases[2 * i + 1] * turnSin[i]));
        return sum;
    }

    /// <summary>A puff's share of the eddies' strength in this air: its σ_v against the reference's.</summary>
    public static double Share(in SmokeAir air) => air.SigmaV / ReferenceSigmaV;

    public void Hash(ref StateHash h)
    {
        h.Add(Up);
        for (int i = 0; i < Modes; i++)
        {
            h.Add(K[i]);
            h.Add(Amp[i]);
            h.Add(Omega[i]);
            h.Add(Phase[i]);
        }
    }
}

/// <summary>A puff's large-eddy modes' phases, cos and sin of each (<see cref="SmokeEddies.PhasesAt"/>).</summary>
[System.Runtime.CompilerServices.InlineArray(2 * SmokeEddies.Modes)]
public struct EddyPhases
{
    float first;
}
