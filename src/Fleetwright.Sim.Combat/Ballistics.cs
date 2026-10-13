using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a shell's flight depends on: its calibre, mass, muzzle velocity and drag form factor, and how high its
/// gun elevates (the table's span). Shell types with equal keys fly alike and share a table.</summary>
public readonly record struct BallisticKey(double CalibreMm, double MassKg, double MuzzleVelocityMs, double FormFactor, double MaxElevationDeg)
{
    public static BallisticKey Of(in ShellTypeRow s, double maxElevationDeg) =>
        new(s.CalibreMm, s.MassKg, s.MuzzleVelocityMs, s.FormFactor, maxElevationDeg);
}

/// <summary>The exterior ballistics (docs/battle/plan.md "Shells", docs/battle/fighting.md 4.2a): a point mass on the
/// sphere, gravity toward the centre falling off with the square of the distance, drag along the velocity from the
/// standard drag curve Cd(Mach) times the shell's form factor, in an exponential atmosphere with the speed of sound by
/// height. It's research/simulation/fire_control_ref.py's <c>_fly</c>, on the sphere. A flight stays in the plane
/// through the Earth's centre, the muzzle and its first velocity, so it's integrated in that plane (x along the
/// muzzle's horizontal, y up, from the muzzle at sea level; the centre at (0, -R)).
/// <para>The Earth's turning adds -2Ω × v. To first order it deflects the shell by δ with δ'' = -2Ω × v - c δ', c the
/// drag's damping across the track (kρCd·v): a deflection's own speed is braked as the shell's is. That has a closed
/// form, δ = -2Ω × K, K = ∫ e^-C W, W = ∫ e^C v, C = ∫ c, so the flight carries K (in its plane) and the lookup's
/// deflection is one cross product. Without the damping (plan.md's first idea, the plain integral of the displacement) a
/// shell flying 37 s misses the turning sphere's fall by 6 m of its 38.</para></summary>
public static class Ballistics
{
    public const double G = 9.80665;
    /// <summary>The Earth's turning, rad/s, about +Z (the north pole).</summary>
    public const double Omega = 7.2921159e-5;
    public static readonly Vec3d OmegaVector = new(0, 0, Omega);

    // Cd(Mach) for a pointed shell, the 1943 / KD8 family (fire_control_ref.py _CD_M, _CD_V)
    static readonly double[] CdM = [0.0, 0.70, 0.80, 0.90, 0.95, 1.00, 1.05, 1.10, 1.20, 1.50, 2.00, 2.50, 3.00, 4.00];
    static readonly double[] CdV = [0.120, 0.120, 0.125, 0.160, 0.220, 0.330, 0.380, 0.385, 0.375, 0.340, 0.295, 0.265, 0.245, 0.225];

    public static double Cd(double mach)
    {
        if (mach <= CdM[0])
            return CdV[0];
        for (int i = 1; i < CdM.Length; i++)
            if (mach <= CdM[i])
                return CdV[i - 1] + (CdV[i] - CdV[i - 1]) * (mach - CdM[i - 1]) / (CdM[i] - CdM[i - 1]);
        return CdV[^1];
    }

    /// <summary>The air at a height: density, kg/m³, and the speed of sound, m/s (fire_control_ref.py _atm).</summary>
    public static (double Rho, double A) Air(double z)
    {
        z = Math.Max(z, 0.0);
        return (1.225 * Math.Exp(-z / 9000.0), 340.3 - 0.0041 * Math.Min(z, 11000.0));
    }

    /// <summary>A flight's state in its plane: position and velocity, and the Coriolis term's integrals (C, W, K).</summary>
    public struct State
    {
        public double X, Y, Vx, Vy, C, Wx, Wy, Kx, Ky;
    }

    /// <summary>The drag constant k (a = -k ρ Cd v v⃗): ½ A i / m.</summary>
    public static double DragK(in BallisticKey key) => 0.5 * Math.PI * Math.Pow(key.CalibreMm / 1000, 2) / 4 * key.FormFactor / key.MassKg;

    /// <summary>The state's rate of change.</summary>
    static State Rate(in State s, double k)
    {
        double R = Earth.Radius;
        double cx = s.X, cy = s.Y + R, r = Math.Sqrt(cx * cx + cy * cy);
        double v = Math.Sqrt(s.Vx * s.Vx + s.Vy * s.Vy);
        var (rho, a) = Air(r - R);
        double d = k * rho * Cd(v / a) * v, g = G * R * R / (r * r * r), e = Math.Exp(s.C), ie = 1 / e;
        return new State
        {
            X = s.Vx, Y = s.Vy, Vx = -d * s.Vx - g * cx, Vy = -d * s.Vy - g * cy,
            C = d, Wx = e * s.Vx, Wy = e * s.Vy, Kx = ie * s.Wx, Ky = ie * s.Wy,
        };
    }

    /// <summary>One RK4 step.</summary>
    public static State Step(in State s, double k, double dt)
    {
        var k1 = Rate(s, k);
        var k2 = Rate(Add(s, k1, dt / 2), k);
        var k3 = Rate(Add(s, k2, dt / 2), k);
        var k4 = Rate(Add(s, k3, dt), k);
        var sum = Add(Add(k1, k4, 1), Add(k2, k3, 1), 2);
        return Add(s, sum, dt / 6);
    }

    static State Add(in State s, in State d, double h) => new()
    {
        X = s.X + d.X * h, Y = s.Y + d.Y * h, Vx = s.Vx + d.Vx * h, Vy = s.Vy + d.Vy * h,
        C = s.C + d.C * h, Wx = s.Wx + d.Wx * h, Wy = s.Wy + d.Wy * h, Kx = s.Kx + d.Kx * h, Ky = s.Ky + d.Ky * h,
    };

    /// <summary>The first state of a flight from sea level at an elevation and speed.</summary>
    public static State Start(double elevationDeg, double speedMs)
    {
        double e = double.DegreesToRadians(elevationDeg);
        return new State { Vx = speedMs * Math.Cos(e), Vy = speedMs * Math.Sin(e) };
    }

    /// <summary>A state's arc along the sea from the muzzle (m), its height over the sea, and their rates.</summary>
    public static (double S, double H, double SDot, double HDot) Polar(in State s)
    {
        double R = Earth.Radius, cx = s.X, cy = s.Y + R, r2 = cx * cx + cy * cy, r = Math.Sqrt(r2);
        return (R * Math.Atan2(cx, cy), r - R, R * (cy * s.Vx - cx * s.Vy) / r2, (cx * s.Vx + cy * s.Vy) / r);
    }
}

/// <summary>A flight at a moment, from a table: the arc along the sea from the muzzle (m) and the height over the
/// muzzle, their rates, and the Coriolis term's K (in the flight's plane: along the muzzle's horizontal and up) with its
/// rate.</summary>
public struct FlightPoint
{
    public double S, H, SDot, HDot, Kx, Ky, KxDot, KyDot;
}

/// <summary>A shell's flights, integrated once (<see cref="Ballistics"/>): for elevations every <see cref="ElevationStep"/>
/// from <see cref="MinElevation"/> to past the gun's highest, muzzle velocities of 0.96, 1 and 1.04 of its own (the
/// ship's motion adds or takes a few per cent) and air 0.95, 1 and 1.05 as dense as the standard (a muzzle h0 over the
/// sea meets air thinner by e^(-h0/9000) all the way, which is nearly its whole difference from a flight from the sea;
/// later, the day's weather), each flight's arc along the sea, height and Coriolis K every <see cref="TimeStep"/>, with
/// their rates. A lookup is Hermite in time, a cubic in elevation and quadratics in velocity and density: a couple of
/// microseconds, so the sim and the renderer look shells up rather than fly them. Read-only once built: every thread
/// shares it.</summary>
public sealed class BallisticTable
{
    public const double MinElevation = -3, ElevationStep = 0.5, TimeStep = 1.0, IntegrationStep = 0.025;
    /// <summary>How far below the sea each flight runs on, m: room for muzzles up there and neighbours that land later.</summary>
    public const double Below = 800;
    public static readonly double[] Speeds = [0.96, 1.0, 1.04];
    public static readonly double[] Densities = [0.95, 1.0, 1.05];
    const int Flights = 9;   // speeds x densities a row
    const int Fields = 8;    // a FlightPoint

    public BallisticKey Key { get; }
    readonly int rows;
    readonly double[][] data;   // [(row * 3 + speed) * 3 + density]: Fields doubles a sample
    readonly int[] length;      // samples in each flight

    public int Elevations => rows;
    public double MaxElevationDeg => MinElevation + (rows - 1) * ElevationStep;

    /// <summary>The bytes it holds.</summary>
    public long Bytes
    {
        get
        {
            long b = 0;
            foreach (var d in data)
                b += d.Length * 8L;
            return b;
        }
    }

    public BallisticTable(BallisticKey key)
    {
        Key = key;
        rows = (int)Math.Ceiling((key.MaxElevationDeg + 2 - MinElevation) / ElevationStep) + 1;
        data = new double[rows * Flights][];
        length = new int[rows * Flights];
        double k0 = Ballistics.DragK(key);
        Parallel.For(0, rows * Flights, f =>
        {
            int row = f / Flights, sp = f % Flights / 3;
            double k = k0 * Densities[f % 3];
            var samples = new List<double>(4096);
            var st = Ballistics.Start(MinElevation + row * ElevationStep, key.MuzzleVelocityMs * Speeds[sp]);
            int per = (int)Math.Round(TimeStep / IntegrationStep);
            for (int n = 0; ; n++)
            {
                if (n % per == 0)
                {
                    var (s, h, sd, hd) = Ballistics.Polar(st);
                    double ie = Math.Exp(-st.C);
                    samples.AddRange([s, h, sd, hd, st.Kx, st.Ky, ie * st.Wx, ie * st.Wy]);
                    if (h < -Below || samples.Count / Fields > 4000)
                        break;
                }
                st = Ballistics.Step(st, k, IntegrationStep);
            }
            data[f] = samples.ToArray();
            length[f] = samples.Count / Fields;
        });
        falls = BuildFalls();
    }

    /// <summary>The air's density, as a fraction of the standard, for a muzzle h0 over the sea.</summary>
    public static double DensityAt(double h0) => Math.Exp(-h0 / 9000);

    /// <summary>Where a flight is at t seconds. speed: its muzzle velocity as a fraction of the shell's own; density: the
    /// air's as a fraction of the standard (<see cref="DensityAt"/>).</summary>
    public FlightPoint At(double elevationDeg, double speed, double density, double t)
    {
        // a cubic across elevations (Lagrange on four rows), quadratics across the three speeds and densities
        double fe = (elevationDeg - MinElevation) / ElevationStep;
        int e0 = Math.Clamp((int)Math.Floor(fe) - 1, 0, rows - 4);
        double u = fe - e0;
        Span<double> we = [Lagrange4(u, 0), Lagrange4(u, 1), Lagrange4(u, 2), Lagrange4(u, 3)];
        double fv = (speed - Speeds[0]) / (Speeds[1] - Speeds[0]);
        Span<double> wv = [(fv - 1) * (fv - 2) / 2, -fv * (fv - 2), fv * (fv - 1) / 2];
        double fd = (density - Densities[0]) / (Densities[1] - Densities[0]);
        Span<double> wd = [(fd - 1) * (fd - 2) / 2, -fd * (fd - 2), fd * (fd - 1) / 2];
        var p = new FlightPoint();
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3; j++)
                for (int l = 0; l < 3; l++)
                    Accumulate(ref p, ((e0 + i) * 3 + j) * 3 + l, t, we[i] * wv[j] * wd[l]);
        return p;
    }

    /// <summary>The weight of node i (0..3, at 0, 1, 2, 3) at u.</summary>
    static double Lagrange4(double u, int i) => i switch
    {
        0 => -(u - 1) * (u - 2) * (u - 3) / 6,
        1 => u * (u - 2) * (u - 3) / 2,
        2 => -u * (u - 1) * (u - 3) / 2,
        _ => u * (u - 1) * (u - 2) / 6,
    };

    /// <summary>One flight at t, weighted into p: Hermite between its samples, positions with their rates.</summary>
    void Accumulate(ref FlightPoint p, int f, double t, double w)
    {
        var d = data[f];
        int n = length[f];
        double ft = Math.Max(t, 0) / TimeStep;
        int k = Math.Min((int)ft, n - 2);
        double u = ft - k, dt = TimeStep;
        int a = k * Fields, b = a + Fields;
        double h00 = (1 + 2 * u) * (1 - u) * (1 - u) * w, h10 = u * (1 - u) * (1 - u) * dt * w, h01 = u * u * (3 - 2 * u) * w, h11 = u * u * (u - 1) * dt * w;
        double d00 = 6 * u * (u - 1) / dt * w, d10 = (1 - u) * (1 - 3 * u) * w, d01 = -d00, d11 = u * (3 * u - 2) * w;
        p.S += h00 * d[a] + h10 * d[a + 2] + h01 * d[b] + h11 * d[b + 2];
        p.H += h00 * d[a + 1] + h10 * d[a + 3] + h01 * d[b + 1] + h11 * d[b + 3];
        p.SDot += d00 * d[a] + d10 * d[a + 2] + d01 * d[b] + d11 * d[b + 2];
        p.HDot += d00 * d[a + 1] + d10 * d[a + 3] + d01 * d[b + 1] + d11 * d[b + 3];
        p.Kx += h00 * d[a + 4] + h10 * d[a + 6] + h01 * d[b + 4] + h11 * d[b + 6];
        p.Ky += h00 * d[a + 5] + h10 * d[a + 7] + h01 * d[b + 5] + h11 * d[b + 7];
        // K's rate between samples: linear (it's e^-C W, smooth; only the renderer's velocity reads it)
        p.KxDot += w * (d[a + 6] + (d[b + 6] - d[a + 6]) * u);
        p.KyDot += w * (d[a + 7] + (d[b + 7] - d[a + 7]) * u);
    }

    // ------------------------------------------------------------------ a shell in the world

    /// <summary>A shell's flight in the world: from a muzzle position at an elevation and a muzzle velocity (a fraction
    /// of the shell's own) along a heading (a unit vector, horizontal at the muzzle). Its position t seconds after firing,
    /// Earth-centred, and its velocity, the Earth's turning in.</summary>
    public Vec3d Position(Vec3d muzzle, Vec3d forward, double elevationDeg, double speed, double t, out Vec3d velocity)
    {
        var up = muzzle.Normalized();
        double h0 = muzzle.Length - Earth.Radius;
        var p = At(elevationDeg, speed, DensityAt(h0), t);
        double th = p.S / Earth.Radius, c = Math.Cos(th), sn = Math.Sin(th), r = Earth.Radius + h0 + p.H;
        var radial = up * c + forward * sn;
        var along = forward * c - up * sn;
        velocity = radial * p.HDot + along * (p.SDot * r / Earth.Radius) + Coriolis(forward, up, p.KxDot, p.KyDot);
        return radial * r + Coriolis(forward, up, p.Kx, p.Ky);
    }

    /// <summary>The Earth's turning's deflection, -2Ω × K (K in the flight's plane: kx along the muzzle's horizontal, ky
    /// up); given K's rate, the deflection's velocity.</summary>
    public static Vec3d Coriolis(Vec3d forward, Vec3d up, double kx, double ky) =>
        Vec3d.Cross(Ballistics.OmegaVector, forward * kx + up * ky) * -2;

    /// <summary>When a flight from a muzzle h0 over the sea comes down through a height (over the sea) on its way down,
    /// in its plane, seconds; NaN if it never does. From the range table's time of flight, up the descent by the height
    /// at its rate of fall, then Newton's steps on the flight: its height is concave in time (gravity), so from the late
    /// side they close on the descent's crossing and can't jump to the climb's.</summary>
    public double Descends(double elevationDeg, double speed, double h0, double height)
    {
        double density = DensityAt(h0), t = Fall(elevationDeg, h0, speed).Tof;
        if (double.IsNaN(t))
            return double.NaN;
        if (height > 0)
            t -= height / Math.Max(-At(elevationDeg, speed, density, t).HDot, 1);
        for (int i = 0; i < 30; i++)
        {
            if (!(t >= 0))
                return double.NaN;
            var p = At(elevationDeg, speed, density, t);
            double f = p.H + h0 - height;
            if (p.HDot >= 0)
                return double.NaN;   // past the top going back: it never comes down through it
            double dt = f / p.HDot;
            t -= dt;
            if (Math.Abs(dt) < 1e-9)
                return t;
        }
        return t;
    }

    /// <summary>When a shell in the world comes down through a height over the sea, seconds after firing (NaN if it never
    /// does): <see cref="Descends"/> in its plane, then Newton's steps on its world height, the Earth's turning in (its
    /// vertical part, the Eötvös effect, moves the fall by metres).</summary>
    public double Reaches(Vec3d muzzle, Vec3d forward, double elevationDeg, double speed, double height)
    {
        double h0 = muzzle.Length - Earth.Radius, t = Descends(elevationDeg, speed, h0, height);
        if (double.IsNaN(t))
            return t;
        for (int i = 0; i < 4; i++)
        {
            var p = Position(muzzle, forward, elevationDeg, speed, t, out var v);
            double up = Vec3d.Dot(v, p) / p.Length;
            if (up >= 0)
                break;
            double dt = (p.Length - Earth.Radius - height) / up;
            t -= dt;
            if (Math.Abs(dt) < 1e-6)
                break;
        }
        return t;
    }

    // ------------------------------------------------------------------ the range table (fire control's)

    /// <summary>The range table's muzzle heights: every <see cref="FallHeightStep"/> m from the sea to
    /// <see cref="FallHeightMax"/> (a muzzle over it reads the top row).</summary>
    public const double FallHeightStep = 5, FallHeightMax = 60;
    const int FallHeights = 13, FallFields = 4;

    /// <summary>Each row's falls at sea level, by speed and muzzle height: range, time of flight, angle of fall and
    /// striking velocity (<see cref="BuildFalls"/>).</summary>
    readonly double[] falls;

    /// <summary>The range table, from the flights: where each row's flight at each speed comes down to the sea from each
    /// muzzle height, in the air that height gives (the density's quadratic across its three flights), found on the
    /// flights' samples (the first sample past the top below the sea, then halving between Hermite points).</summary>
    double[] BuildFalls()
    {
        var f = new double[rows * Speeds.Length * FallHeights * FallFields];
        Parallel.For(0, rows, row =>
        {
            var wd = new double[3];
            for (int sp = 0; sp < Speeds.Length; sp++)
                for (int k = 0; k < FallHeights; k++)
                {
                    double h0 = k * FallHeightStep, fd = (DensityAt(h0) - Densities[0]) / (Densities[1] - Densities[0]);
                    (wd[0], wd[1], wd[2]) = ((fd - 1) * (fd - 2) / 2, -fd * (fd - 2), fd * (fd - 1) / 2);
                    int f0 = (row * 3 + sp) * 3, n = Math.Min(length[f0], Math.Min(length[f0 + 1], length[f0 + 2]));
                    double H(int sample) =>
                        wd[0] * data[f0][sample * Fields + 1] + wd[1] * data[f0 + 1][sample * Fields + 1] + wd[2] * data[f0 + 2][sample * Fields + 1];
                    FlightPoint Point(double t)
                    {
                        var p = new FlightPoint();
                        for (int l = 0; l < 3; l++)
                            Accumulate(ref p, f0 + l, t, wd[l]);
                        return p;
                    }
                    int at = ((row * 3 + sp) * FallHeights + k) * FallFields;
                    int s = 1;
                    while (s < n && H(s) >= H(s - 1))
                        s++;   // up to the top
                    while (s < n && H(s) > -h0)
                        s++;   // down to the sea
                    if (s >= n)
                    {
                        f.AsSpan(at, FallFields).Fill(double.NaN);
                        continue;
                    }
                    double lo = (s - 1) * TimeStep, hi = s * TimeStep;
                    for (int i = 0; i < 50; i++)
                    {
                        double m = (lo + hi) / 2;
                        if (Point(m).H > -h0)
                            lo = m;
                        else
                            hi = m;
                    }
                    double tf = (lo + hi) / 2;
                    var pt = Point(tf);
                    double along = pt.SDot * (Earth.Radius + h0 + pt.H) / Earth.Radius;
                    (f[at], f[at + 1], f[at + 2], f[at + 3]) =
                        (pt.S, tf, double.RadiansToDegrees(Math.Atan2(-pt.HDot, along)), Math.Sqrt(along * along + pt.HDot * pt.HDot));
                }
        });
        return f;
    }

    /// <summary>The fall at sea level of a flight from a muzzle h0 high, in its plane: range along the sea, time of
    /// flight, angle of fall (degrees under the horizontal) and striking velocity. From the range table: a cubic in
    /// elevation, quadratics in speed and the muzzle's height; a fraction of a microsecond.</summary>
    public (double Range, double Tof, double FallDeg, double StrikeMs) Fall(double elevationDeg, double h0, double speed = 1)
    {
        double fe = (elevationDeg - MinElevation) / ElevationStep;
        int e0 = Math.Clamp((int)Math.Floor(fe) - 1, 0, rows - 4);
        double u = fe - e0;
        Span<double> we = [Lagrange4(u, 0), Lagrange4(u, 1), Lagrange4(u, 2), Lagrange4(u, 3)];
        double fv = (speed - Speeds[0]) / (Speeds[1] - Speeds[0]);
        Span<double> wv = [(fv - 1) * (fv - 2) / 2, -fv * (fv - 2), fv * (fv - 1) / 2];
        double fh = Math.Clamp(h0, 0, FallHeightMax) / FallHeightStep;
        int k0 = Math.Clamp((int)Math.Round(fh) - 1, 0, FallHeights - 3);
        double g = fh - k0;
        Span<double> wh = [(g - 1) * (g - 2) / 2, -g * (g - 2), g * (g - 1) / 2];
        double range = 0, tof = 0, fall = 0, strike = 0;
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3; j++)
            {
                int at = (((e0 + i) * 3 + j) * FallHeights + k0) * FallFields;
                for (int l = 0; l < 3; l++, at += FallFields)
                {
                    double w = we[i] * wv[j] * wh[l];
                    range += w * falls[at];
                    tof += w * falls[at + 1];
                    fall += w * falls[at + 2];
                    strike += w * falls[at + 3];
                }
            }
        return (range, tof, fall, strike);
    }

    /// <summary>The elevation that throws to a range along the sea from a muzzle h0 high (the lower of the two), or NaN
    /// beyond the gun's reach at its highest elevation: the range table's falls, by regula falsi (a few microseconds).</summary>
    public double ElevationFor(double range, double h0, double speed = 1)
    {
        double lo = MinElevation, hi = Math.Min(Key.MaxElevationDeg, MaxElevationDeg - 2 * ElevationStep);
        double rHi = Fall(hi, h0, speed).Range - range;
        if (!(rHi >= 0))
            return double.NaN;
        double rLo = Fall(lo, h0, speed).Range - range;
        if (rLo > 0)
            return lo;
        // Illinois: the false position, halving the stale end's value when the same end moves twice
        int side = 0;
        for (int i = 0; i < 60 && hi - lo > 1e-9; i++)
        {
            double m = (lo * rHi - hi * rLo) / (rHi - rLo), rm = Fall(m, h0, speed).Range - range;
            if (Math.Abs(rm) < 1e-6)
                return m;
            if (rm < 0)
            {
                (lo, rLo) = (m, rm);
                if (side == -1)
                    rHi /= 2;
                side = -1;
            }
            else
            {
                (hi, rHi) = (m, rm);
                if (side == 1)
                    rLo /= 2;
                side = 1;
            }
        }
        return (lo + hi) / 2;
    }

    /// <summary><see cref="ElevationFor(double, double, double)"/> from a guess near it (last second's, for a battery
    /// following its target): the secant from there, a microsecond or so; the bracketed search if it strays.</summary>
    public double ElevationFor(double range, double h0, double speed, double guessDeg)
    {
        if (double.IsNaN(guessDeg))
            return ElevationFor(range, h0, speed);
        double hi = Math.Min(Key.MaxElevationDeg, MaxElevationDeg - 2 * ElevationStep);
        double a = Math.Clamp(guessDeg, MinElevation, hi), fa = Fall(a, h0, speed).Range - range;
        double b = Math.Clamp(a + (fa > 0 ? -0.02 : 0.02), MinElevation, hi), fb = Fall(b, h0, speed).Range - range;
        for (int i = 0; i < 6 && Math.Abs(fb) > 1e-6 && fb != fa; i++)
        {
            double c = b - fb * (b - a) / (fb - fa);
            if (!(c >= MinElevation && c <= hi))
                break;
            (a, fa) = (b, fb);
            b = c;
            fb = Fall(b, h0, speed).Range - range;
        }
        return Math.Abs(fb) <= 1e-4 ? b : ElevationFor(range, h0, speed);
    }

    /// <summary>Metres of range a degree of elevation is worth there (the range table's slope): an error in elevation as
    /// one in range.</summary>
    public double RangePerDeg(double elevationDeg, double h0, double speed = 1) =>
        (Fall(elevationDeg + 0.01, h0, speed).Range - Fall(elevationDeg - 0.01, h0, speed).Range) / 0.02;
}

/// <summary>The world's ballistic tables, one per key, built when a class first needs them and read-only after; the
/// renderer looks shells up in the same tables (by index, from the snapshot). Adding is the sim thread's; reading by
/// index is safe from any thread.</summary>
public sealed class BallisticsRegistry
{
    readonly Dictionary<BallisticKey, int> index = [];
    volatile BallisticTable[] tables = [];

    public int Count => tables.Length;

    /// <summary>The table at an index (any thread).</summary>
    public BallisticTable this[int i] => tables[i];

    /// <summary>The table for a key, built now if it's new; its index.</summary>
    public int Ensure(BallisticKey key)
    {
        if (index.TryGetValue(key, out int i))
            return i;
        var t = new BallisticTable(key);
        var grown = new BallisticTable[tables.Length + 1];
        tables.CopyTo(grown, 0);
        grown[^1] = t;
        tables = grown;
        index[key] = grown.Length - 1;
        return grown.Length - 1;
    }

    public bool TryIndex(BallisticKey key, out int i) => index.TryGetValue(key, out i);
}
