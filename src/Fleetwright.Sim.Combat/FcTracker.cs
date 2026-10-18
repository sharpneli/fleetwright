using System.Runtime.CompilerServices;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

[InlineArray(6)]
public struct FcState
{
    double e;
}

[InlineArray(36)]
public struct FcCov
{
    double e;
}

/// <summary>A reading as a fire-control computer takes it: when it was taken (and when it reaches the computer), from
/// where (own ship's reference point: every instrument's cut is reduced to it) and own ship's velocity then, its range
/// and bearing from there with their errors (1 sigma: m, rad), and
/// the same as a point on the sphere with its east/north covariance (<see cref="Track.Reading"/>).</summary>
public struct FcReading
{
    public long Ms, ArriveMs;
    public Vec3d From, OwnVelocity, Z;
    public double RangeM, BearingDeg, SigmaRangeM, SigmaBearingRad, R11, R12, R22;
}

/// <summary>A fire-control computer's hold on its target (docs/battle/fire-control.md F2): one fixed-size struct for
/// every <see cref="TrackerKind"/>, stepped in place, a switch on the kind (fire_control_ref.py's PolarTracker,
/// CartTracker and ManoeuvreTracker, and a sailor's judgement below them). Each takes the same readings and answers the
/// same question, where the target will be at a time, and fails where its machine failed:
/// <list type="bullet">
/// <item><b>Judgement:</b> a position from the sightings (weighed as a man would: the bearing he lays on, the range he
/// guesses) carried on along a course and speed guessed from the target's look, which it never learns; the guess is
/// made again now and then (<see cref="Rejudge"/>).</item>
/// <item><b>RateClock:</b> range and range rate, true bearing and bearing rate from own ship, each a two-state filter;
/// constant rates, clamped at the clock's limits, so it drifts on a crossing target. A plain clock's rates are from the
/// ship as she goes, so her turns and changes of speed show as the target's and must be re-learned from the readings. A
/// helm-free clock's are from a reference ship (<see cref="OwnP"/>, <see cref="OwnV"/>: own ship at its last reading,
/// carried on straight), so own ship's motion is an exact input: at each reading its prediction is taken from where she
/// really is and its rates re-referenced to her velocity then.</item>
/// <item><b>TrueCourse:</b> the target's position and velocity on the sphere (<see cref="Track"/>, as the pictures).</item>
/// <item><b>Acceleration:</b> position, velocity and a decaying (Singer) acceleration in a plane tangent at the first
/// reading.</item>
/// <item><b>TurnRate:</b> position, speed, heading and rate of turn in that plane (a coordinated turn).</item>
/// </list>
/// The plane trackers (and the judgement) open up on a manoeuvre when asked (<see cref="ComputerRow.Detect"/>): two
/// readings in a row past the 99.9% surprise.</summary>
public struct FcTracker
{
    public TrackerKind Kind;
    /// <summary>The moment the estimate stands at (its last reading's).</summary>
    public long Ms;
    /// <summary>TrueCourse's track.</summary>
    public Track Line;
    /// <summary>The tangent plane's origin, for the plane trackers and the judgement.</summary>
    public Vec3d Origin;
    public FcState X;
    public FcCov P;
    public int Strikes;
    /// <summary>A helm-free rate clock's reference ship: own ship's position and velocity at its last reading.</summary>
    public Vec3d OwnP, OwnV;

    const double Knot = 1852.0 / 3600;
    const double Chi2Pos999 = 13.8;   // two degrees of freedom, 99.9%

    readonly int N => Kind switch { TrackerKind.Judgement => 2, TrackerKind.Acceleration => 6, TrackerKind.TurnRate => 5, _ => 4 };

    // ------------------------------------------------------------------ starting

    /// <summary>The track begun at its first reading, from the eye's guess at the target's velocity (a tangent vector at
    /// the target, with its error sv, m/s 1 sigma) and own ship's velocity then.</summary>
    public static FcTracker Start(in ComputerRow c, in FcReading r, Vec3d guess, double sv)
    {
        var t = new FcTracker { Kind = c.Tracker, Ms = r.Ms, Origin = r.Z, OwnP = r.From, OwnV = r.OwnVelocity };
        var ownVelocity = r.OwnVelocity;
        var e0 = Earth.East(r.Z);
        var n0 = Earth.North(r.Z);
        double ge = Vec3d.Dot(guess, e0), gn = Vec3d.Dot(guess, n0);
        switch (c.Tracker)
        {
            case TrackerKind.TrueCourse:
                t.Line = new Track
                {
                    Position = r.Z, Velocity = guess, Ms = r.Ms,
                    P = new Cov4 { Ee = r.R11, En = r.R12, Nn = r.R22, VeVe = sv * sv, VnVn = sv * sv },
                };
                break;
            case TrackerKind.RateClock:
            {
                var e = Earth.East(r.From);
                var n = Earth.North(r.From);
                var rel = guess - ownVelocity;
                double re = Vec3d.Dot(rel, e), rn = Vec3d.Dot(rel, n), b = r.BearingDeg * Math.PI / 180, sb = Math.Sin(b), cb = Math.Cos(b);
                (t.X[0], t.X[1], t.X[2], t.X[3]) = (r.RangeM, re * sb + rn * cb, b, (re * cb - rn * sb) / r.RangeM);
                (t.P[0], t.P[1], t.P[2]) = (r.SigmaRangeM * r.SigmaRangeM, 0, sv * sv);
                (t.P[3], t.P[4], t.P[5]) = (r.SigmaBearingRad * r.SigmaBearingRad, 0, sv * sv / (r.RangeM * r.RangeM));
                break;
            }
            case TrackerKind.Judgement:
                (t.X[2], t.X[3]) = (ge, gn);
                (t.P[0], t.P[1], t.P[2], t.P[3]) = (r.R11, r.R12, r.R12, r.R22);
                break;
            case TrackerKind.Acceleration:
                (t.X[2], t.X[3]) = (ge, gn);
                t.SetPosition(6, r);
                t.P[2 * 6 + 2] = t.P[3 * 6 + 3] = sv * sv;
                t.P[4 * 6 + 4] = t.P[5 * 6 + 5] = c.AccelSigmaMs2 * c.AccelSigmaMs2;
                break;
            case TrackerKind.TurnRate:
            {
                (t.X[2], t.X[3]) = (Math.Sqrt(ge * ge + gn * gn), Math.Atan2(ge, gn));
                t.SetPosition(5, r);
                double sd = c.PriorDeg * Math.PI / 180, sk = c.PriorKn * Knot, w = Math.PI / 180;
                (t.P[2 * 5 + 2], t.P[3 * 5 + 3], t.P[4 * 5 + 4]) = (sk * sk, sd * sd, w * w);
                break;
            }
        }
        return t;
    }

    void SetPosition(int n, in FcReading r) => (P[0], P[1], P[n], P[n + 1]) = (r.R11, r.R12, r.R12, r.R22);

    // ------------------------------------------------------------------ readings

    /// <summary>A reading taken in: the estimate carried to its time, then corrected.</summary>
    public void Fix(in ComputerRow c, in FcReading r)
    {
        double dt = Math.Max((r.Ms - Ms) / 1000.0, 0);
        switch (Kind)
        {
            case TrackerKind.TrueCourse:
                Line.Fix(r.Ms, c.Q, r.Z, r.R11, r.R12, r.R22);
                break;
            case TrackerKind.RateClock:
            {
                if (c.HelmFree && dt > 0)
                {
                    // the clock's prediction at the reading, from the reference ship, taken from where she really is;
                    // its rates then re-referenced to her velocity
                    var (rp, rv) = (OwnP, OwnV);
                    Track.Carry(ref rp, ref rv, dt);
                    ClockPoint(c, X, rp, rv, dt, out var at, out _);
                    (X[0], X[2]) = (Earth.Distance(r.From, at), Earth.Bearing(r.From, at) * Math.PI / 180);
                    OwnChange(ref X, r.OwnVelocity - OwnV, r.From);
                    PredictClockCovariance(dt, c.Q);
                }
                else
                    PredictClock(dt, c.Q);
                (OwnP, OwnV) = (r.From, r.OwnVelocity);
                double s = P[0] + r.SigmaRangeM * r.SigmaRangeM;
                Kalman2(ref X[0], ref X[1], ref P[0], ref P[1], ref P[2], r.RangeM - X[0], s);
                double zb = WrapRad(r.BearingDeg * Math.PI / 180 - X[2]);
                Kalman2(ref X[2], ref X[3], ref P[3], ref P[4], ref P[5], zb, P[3] + r.SigmaBearingRad * r.SigmaBearingRad);
                break;
            }
            case TrackerKind.Judgement:
            {
                // carried on along the guess; the guess's own error grows the position's doubt
                (X[0], X[1]) = (X[0] + X[2] * dt, X[1] + X[3] * dt);
                double sv = GuessSigma(c, Math.Sqrt(X[2] * X[2] + X[3] * X[3])) * dt;
                (P[0], P[3]) = (P[0] + sv * sv, P[3] + sv * sv);
                UpdatePosition(2, r, false, c);
                break;
            }
            case TrackerKind.Acceleration:
            case TrackerKind.TurnRate:
                Propagate(c, dt);
                UpdatePosition(N, r, c.Detect, c);
                if (Kind == TrackerKind.TurnRate)
                    X[4] = Math.Clamp(X[4], -0.05, 0.05);   // no ship turns faster than ~3°/s
                break;
        }
        Ms = Math.Max(Ms, r.Ms);
    }

    /// <summary>Own ship's change of velocity taken off a rate clock's rates (a helm-free clock's Dumaresq, set from the
    /// gyro and the log): the relative velocity changes by -dv.</summary>
    static void OwnChange(ref FcState x, Vec3d dv, Vec3d ownPosition)
    {
        if (x[0] <= 0)
            return;
        double de = Vec3d.Dot(dv, Earth.East(ownPosition)), dn = Vec3d.Dot(dv, Earth.North(ownPosition)), sb = Math.Sin(x[2]), cb = Math.Cos(x[2]);
        x[1] -= de * sb + dn * cb;
        x[3] -= (de * cb - dn * sb) / x[0];
    }

    /// <summary>A judgement looks at the target again: a new guess at its velocity (a tangent vector).</summary>
    public void Rejudge(Vec3d guess)
    {
        if (Kind != TrackerKind.Judgement)
            return;
        (X[2], X[3]) = (Vec3d.Dot(guess, Earth.East(Origin)), Vec3d.Dot(guess, Earth.North(Origin)));
    }

    /// <summary>The eye's guess at a velocity's error, m/s 1 sigma, at a speed: its speed's and its course's.</summary>
    public static double GuessSigma(in ComputerRow c, double speedMs)
    {
        double a = c.PriorKn * Knot, b = speedMs * c.PriorDeg * Math.PI / 180;
        return Math.Sqrt(a * a + b * b);
    }

    // ------------------------------------------------------------------ prediction

    /// <summary>Where the computer has the target at a time, and its velocity then (a tangent vector), given own ship's
    /// position and velocity now (a rate clock's answer is relative to own ship, which it carries on as she goes).</summary>
    public readonly void At(in ComputerRow c, long ms, long nowMs, Vec3d ownPosition, Vec3d ownVelocity, out Vec3d position, out Vec3d velocity)
    {
        double dt = Math.Max((ms - Ms) / 1000.0, 0);
        switch (Kind)
        {
            case TrackerKind.TrueCourse:
            {
                var t = Line.At(ms, c.Q);
                (position, velocity) = (t.Position, t.Velocity);
                return;
            }
            case TrackerKind.RateClock:
                // a plain clock's relative answer laid off from the ship as she'll be; a helm-free one's from its
                // reference ship
                if (c.HelmFree)
                {
                    var (rp, rv) = (OwnP, OwnV);
                    Track.Carry(ref rp, ref rv, dt);
                    ClockPoint(c, X, rp, rv, dt, out position, out velocity);
                }
                else
                {
                    var own = ownPosition;
                    var ov = ownVelocity;
                    Track.Carry(ref own, ref ov, (ms - nowMs) / 1000.0);
                    ClockPoint(c, X, own, ov, dt, out position, out velocity);
                }
                return;
            default:
            {
                Span<double> x = stackalloc double[6];
                for (int i = 0; i < 6; i++)
                    x[i] = X[i];
                double ve, vn;
                switch (Kind)
                {
                    case TrackerKind.Judgement:
                        (x[0], x[1], ve, vn) = (x[0] + x[2] * dt, x[1] + x[3] * dt, x[2], x[3]);
                        break;
                    case TrackerKind.Acceleration:
                        Singer(x, dt, c.AccelTauS);
                        (ve, vn) = (x[2], x[3]);
                        break;
                    default:
                        CoordinatedTurn(x, dt);
                        (ve, vn) = (x[2] * Math.Sin(x[3]), x[2] * Math.Cos(x[3]));
                        break;
                }
                var e0 = Earth.East(Origin);
                var n0 = Earth.North(Origin);
                position = (Origin + e0 * x[0] + n0 * x[1]).Normalized() * Earth.Radius;
                velocity = e0 * ve + n0 * vn;
                return;
            }
        }
    }

    public readonly void Hash(ref StateHash h)
    {
        h.Add((int)Kind);
        h.Add(Ms);
        if (Kind == TrackerKind.TrueCourse)
        {
            h.Add(Line.Position.X);
            h.Add(Line.Position.Y);
            h.Add(Line.Velocity.X);
        }
        else
            for (int i = 0; i < 4; i++)
                h.Add(X[i]);
    }

    // ------------------------------------------------------------------ the filters

    void PredictClock(double dt, double q)
    {
        if (dt <= 0)
            return;
        Predict2(ref X[0], ref X[1], ref P[0], ref P[1], ref P[2], dt, q);
        Predict2(ref X[2], ref X[3], ref P[3], ref P[4], ref P[5], dt, q / Math.Max(X[0] * X[0], 1));
    }

    /// <summary>A rate clock's range and bearing carried on dt at its rates (clamped at its limits) and laid off from an
    /// observer (where the ship it's reckoned from will be then, and her velocity): the point, and its velocity.</summary>
    static void ClockPoint(in ComputerRow c, in FcState x, Vec3d own, Vec3d ov, double dt, out Vec3d position, out Vec3d velocity)
    {
        double rd = c.MaxRangeRateMs > 0 ? Math.Clamp(x[1], -c.MaxRangeRateMs, c.MaxRangeRateMs) : x[1];
        double wmax = c.MaxBearingRateDegS * Math.PI / 180, bd = wmax > 0 ? Math.Clamp(x[3], -wmax, wmax) : x[3];
        double range = Math.Max(x[0] + rd * dt, 1), b = x[2] + bd * dt;
        var p = own;
        var f = Earth.Forward(p, b * 180 / Math.PI);
        Earth.Advance(ref p, ref f, range);
        var e = Earth.East(own);
        var n = Earth.North(own);
        double sb = Math.Sin(b), cb = Math.Cos(b);
        position = p;
        velocity = ov + e * (rd * sb + range * bd * cb) + n * (rd * cb - range * bd * sb);
    }

    void PredictClockCovariance(double dt, double q)
    {
        double r0 = X[0], b0 = X[2], rv = X[1], bv = X[3];
        PredictClock(dt, q);
        (X[0], X[1], X[2], X[3]) = (r0, rv, b0, bv);
    }

    static void Predict2(ref double x0, ref double x1, ref double p00, ref double p01, ref double p11, double dt, double q)
    {
        x0 += x1 * dt;
        p00 += 2 * dt * p01 + dt * dt * p11 + q * dt * dt * dt / 3;
        p01 += dt * p11 + q * dt * dt / 2;
        p11 += q * dt;
    }

    /// <summary>A two-state filter's update by an innovation y with its variance s.</summary>
    static void Kalman2(ref double x0, ref double x1, ref double p00, ref double p01, ref double p11, double y, double s)
    {
        double k0 = p00 / s, k1 = p01 / s;
        x0 += k0 * y;
        x1 += k1 * y;
        (p00, p01, p11) = ((1 - k0) * p00, (1 - k0) * p01, p11 - k1 * p01);
    }

    /// <summary>The plane trackers carried on dt: x and P (P = F P Fᵀ + Q, F the motion's Jacobian).</summary>
    void Propagate(in ComputerRow c, double dt)
    {
        if (dt <= 0)
            return;
        int n = N;
        Span<double> f = stackalloc double[36], q = stackalloc double[36], x = stackalloc double[6];
        f.Clear();
        q.Clear();
        for (int i = 0; i < n; i++)
            x[i] = X[i];
        if (Kind == TrackerKind.Acceleration)
        {
            double tau = c.AccelTauS, e = Math.Exp(-dt / tau), k1 = tau * tau * (e - 1 + dt / tau), k2 = tau * (1 - e);
            for (int i = 0; i < 6; i++)
                f[i * 6 + i] = 1;
            (f[0 * 6 + 2], f[0 * 6 + 4], f[1 * 6 + 3], f[1 * 6 + 5]) = (dt, k1, dt, k1);
            (f[2 * 6 + 4], f[3 * 6 + 5], f[4 * 6 + 4], f[5 * 6 + 5]) = (k2, k2, e, e);
            double qa = c.AccelSigmaMs2 * c.AccelSigmaMs2 * (1 - Math.Exp(-2 * dt / tau)), qp = qa * dt * dt * dt * dt / 20 + c.Q * dt * dt * dt / 3;
            (q[0], q[7], q[14], q[21], q[28], q[35]) = (qp, qp, qa * dt * dt / 3, qa * dt * dt / 3, qa, qa);
            Singer(x, dt, tau);
        }
        else
        {
            // the coordinated turn's Jacobian, by differences (as the reference takes it)
            Span<double> fx = stackalloc double[6], xd = stackalloc double[6];
            x.CopyTo(fx);
            CoordinatedTurn(fx, dt);
            for (int j = 0; j < n; j++)
            {
                x.CopyTo(xd);
                double d = 1e-4 * Math.Max(1, Math.Abs(x[j]));
                xd[j] += d;
                CoordinatedTurn(xd, dt);
                for (int i = 0; i < n; i++)
                    f[i * n + j] = (xd[i] - fx[i]) / d;
            }
            fx.CopyTo(x);
            double qp = c.Q * dt * dt * dt / 3;
            (q[0], q[n + 1], q[2 * n + 2], q[3 * n + 3], q[4 * n + 4]) = (qp, qp, c.SpeedQ * dt, 1e-6 * dt, c.TurnQ * dt);
        }
        for (int i = 0; i < n; i++)
            X[i] = x[i];
        // P = F P Fᵀ + Q
        Span<double> fp = stackalloc double[36];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                double s = 0;
                for (int k = 0; k < n; k++)
                    s += f[i * n + k] * P[k * n + j];
                fp[i * n + j] = s;
            }
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                double s = q[i * n + j];
                for (int k = 0; k < n; k++)
                    s += fp[i * n + k] * f[j * n + k];
                P[i * n + j] = s;
            }
    }

    /// <summary>A fix of the position (states 0 and 1) in the plane, with the manoeuvre test when asked.</summary>
    void UpdatePosition(int n, in FcReading r, bool detect, in ComputerRow c)
    {
        var e0 = Earth.East(Origin);
        var n0 = Earth.North(Origin);
        var d = r.Z - Origin;
        double ye = Vec3d.Dot(d, e0) - X[0], yn = Vec3d.Dot(d, n0) - X[1];
        double s11 = P[0] + r.R11, s12 = P[1] + r.R12, s22 = P[n + 1] + r.R22, det = s11 * s22 - s12 * s12;
        if (detect && det > 0)
        {
            double nis = (s22 * ye * ye - 2 * s12 * ye * yn + s11 * yn * yn) / det;
            Strikes = nis > Chi2Pos999 ? Strikes + 1 : 0;
            if (Strikes >= 2)
            {
                // "target just turned": its motion's doubt opened up
                if (Kind == TrackerKind.Acceleration)
                {
                    P[4 * n + 4] += 0.3 * 0.3;
                    P[5 * n + 5] += 0.3 * 0.3;
                    P[2 * n + 2] += 2.0 * 2.0;
                    P[3 * n + 3] += 2.0 * 2.0;
                }
                else
                {
                    double w = Math.PI / 180;
                    P[4 * n + 4] += w * w;
                    P[3 * n + 3] += 100 * w * w;
                }
                Strikes = 0;
                (s11, s12, s22) = (P[0] + r.R11, P[1] + r.R12, P[n + 1] + r.R22);
                det = s11 * s22 - s12 * s12;
            }
        }
        if (!(det > 0))
            return;
        double i11 = s22 / det, i12 = -s12 / det, i22 = s11 / det;
        // K = P Hᵀ S⁻¹ (n×2): P's first two columns times S⁻¹
        Span<double> k = stackalloc double[12], ph = stackalloc double[12];
        for (int i = 0; i < n; i++)
        {
            double a = P[i * n], b = P[i * n + 1];
            (k[2 * i], k[2 * i + 1]) = (a * i11 + b * i12, a * i12 + b * i22);
        }
        for (int i = 0; i < n; i++)
            X[i] += k[2 * i] * ye + k[2 * i + 1] * yn;
        // P -= K H P: H P is P's first two rows
        for (int j = 0; j < n; j++)
            (ph[2 * j], ph[2 * j + 1]) = (P[j], P[n + j]);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                P[i * n + j] -= k[2 * i] * ph[2 * j] + k[2 * i + 1] * ph[2 * j + 1];
    }

    static void Singer(Span<double> x, double dt, double tau)
    {
        double e = Math.Exp(-dt / tau), k1 = tau * tau * (e - 1 + dt / tau), k2 = tau * (1 - e);
        x[0] += x[2] * dt + x[4] * k1;
        x[1] += x[3] * dt + x[5] * k1;
        x[2] += x[4] * k2;
        x[3] += x[5] * k2;
        x[4] *= e;
        x[5] *= e;
    }

    /// <summary>A coordinated turn: x east, y north, speed, heading (rad clockwise from north), rate of turn (rad/s).</summary>
    static void CoordinatedTurn(Span<double> x, double dt)
    {
        double v = x[2], psi = x[3], w = x[4];
        if (Math.Abs(w) < 1e-6)
        {
            x[0] += v * Math.Sin(psi) * dt;
            x[1] += v * Math.Cos(psi) * dt;
        }
        else
        {
            x[0] += v / w * (Math.Cos(psi) - Math.Cos(psi + w * dt));
            x[1] += v / w * (Math.Sin(psi + w * dt) - Math.Sin(psi));
        }
        x[3] = psi + w * dt;
    }

    static double WrapRad(double a) => a - 2 * Math.PI * Math.Round(a / (2 * Math.PI));
}
