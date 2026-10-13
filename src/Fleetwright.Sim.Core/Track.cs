namespace Fleetwright.Sim.Core;

/// <summary>A position and velocity's covariance in the estimate's own east/north frame (e, n, ve, vn): the ten numbers of a
/// symmetric 4×4. A contact's estimate is a constant-velocity Kalman filter on it.</summary>
public struct Cov4
{
    public double Ee, En, Nn, EVe, EVn, NVe, NVn, VeVe, VeVn, VnVn;

    /// <summary>On by dt seconds at a constant velocity, with white acceleration of the given variance (m²/s⁴).</summary>
    public void Predict(double dt, double q)
    {
        // P = F P Fᵀ + Q, F = [[I, dt I], [0, I]]
        Ee += 2 * dt * EVe + dt * dt * VeVe;
        Nn += 2 * dt * NVn + dt * dt * VnVn;
        En += dt * (EVn + NVe) + dt * dt * VeVn;
        EVe += dt * VeVe;
        EVn += dt * VeVn;
        NVe += dt * VeVn;
        NVn += dt * VnVn;
        double q3 = q * dt * dt * dt / 3, q2 = q * dt * dt / 2, q1 = q * dt;
        Ee += q3;
        Nn += q3;
        EVe += q2;
        NVn += q2;
        VeVe += q1;
        VnVn += q1;
    }

    /// <summary>A fix of the position, (ze, zn) off the estimate, with its error's covariance (r11, r12, r22): the
    /// estimate's correction (de, dn, dve, dvn), and this updated.</summary>
    public (double De, double Dn, double DVe, double DVn) Update(double ze, double zn, double r11, double r12, double r22)
    {
        // S = H P Hᵀ + R (H takes the position); K = P Hᵀ S⁻¹
        double s11 = Ee + r11, s12 = En + r12, s22 = Nn + r22;
        double det = s11 * s22 - s12 * s12;
        double i11 = s22 / det, i12 = -s12 / det, i22 = s11 / det;
        // the rows of P Hᵀ: (Ee, En), (En, Nn), (EVe, NVe), (EVn, NVn)
        (double, double) K(double a, double b) => (a * i11 + b * i12, a * i12 + b * i22);
        var (k1a, k1b) = K(Ee, En);
        var (k2a, k2b) = K(En, Nn);
        var (k3a, k3b) = K(EVe, NVe);
        var (k4a, k4b) = K(EVn, NVn);
        var d = (k1a * ze + k1b * zn, k2a * ze + k2b * zn, k3a * ze + k3b * zn, k4a * ze + k4b * zn);
        // P -= K H P: each entry less K's row times the position columns of P
        double ee = Ee - (k1a * Ee + k1b * En), en = En - (k1a * En + k1b * Nn), nn = Nn - (k2a * En + k2b * Nn);
        double eve = EVe - (k1a * EVe + k1b * NVe), evn = EVn - (k1a * EVn + k1b * NVn);
        double nve = NVe - (k2a * EVe + k2b * NVe), nvn = NVn - (k2a * EVn + k2b * NVn);
        double veve = VeVe - (k3a * EVe + k3b * NVe), vevn = VeVn - (k3a * EVn + k3b * NVn), vnvn = VnVn - (k4a * EVn + k4b * NVn);
        (Ee, En, Nn, EVe, EVn, NVe, NVn, VeVe, VeVn, VnVn) = (ee, en, nn, eve, evn, nve, nvn, veve, vevn, vnvn);
        return d;
    }

    /// <summary>The position's error ellipse: its semi-axes (1 sigma, m), the major's bearing (degrees from north).</summary>
    public readonly (double Major, double Minor, double BearingDeg) Ellipse()
    {
        double a = Ee, b = En, c = Nn;
        double mid = (a + c) / 2, r = Math.Sqrt(Math.Max((a - c) * (a - c) / 4 + b * b, 0));
        double l1 = mid + r, l2 = Math.Max(mid - r, 0);
        double angle = 0.5 * Math.Atan2(2 * b, a - c);   // from east, toward north
        return (Math.Sqrt(l1), Math.Sqrt(l2), (90 - angle * 180 / Math.PI + 360) % 360);
    }
}

/// <summary>A ship tracked on the sphere: a constant-velocity Kalman filter of its position and velocity (with
/// <see cref="Cov4"/>), as of a moment. The one model of a track from readings of range and bearing: a navy's pictures
/// (docs/battle/plan.md 3.2d) and fire control's trackers (docs/battle/fighting.md 4.2b) both keep theirs with it.</summary>
public struct Track
{
    public Vec3d Position, Velocity;
    public Cov4 P;
    public long Ms;

    /// <summary>Carried on to a time (never back): dead reckoned along its velocity on the sphere, its covariance grown
    /// by white acceleration of variance q (m²/s⁴).</summary>
    public readonly Track At(long ms, double q)
    {
        var t = this;
        double dt = (ms - Ms) / 1000.0;
        if (dt > 0)
        {
            Carry(ref t.Position, ref t.Velocity, dt);
            t.P.Predict(dt, q);
            t.Ms = ms;
        }
        return t;
    }

    /// <summary>A reading at its time: the track carried there, then fixed (<see cref="Update"/>).</summary>
    public void Fix(long ms, double q, Vec3d z, double r11, double r12, double r22)
    {
        this = At(ms, q);
        Update(ref Position, ref Velocity, ref P, z, r11, r12, r22);
    }

    /// <summary>A position and its velocity on along the great circle for dt seconds.</summary>
    public static void Carry(ref Vec3d p, ref Vec3d v, double dt)
    {
        double speed = v.Length;
        if (speed <= 1e-9)
            return;
        var f = v * (1 / speed);
        Earth.Advance(ref p, ref f, speed * dt);
        v = f * speed;
    }

    /// <summary>A reading from an observer, a range and bearing with their errors (1 sigma: metres, radians), as a
    /// point on the sphere and its error's covariance in east/north: the range's along the line of sight, the bearing's
    /// times the range across it.</summary>
    public static (Vec3d Z, double R11, double R12, double R22) Reading(Vec3d observer, double rangeM, double bearingDeg, double sigmaRangeM,
        double sigmaBearingRad)
    {
        var z = observer;
        var f = Earth.Forward(z, bearingDeg);
        Earth.Advance(ref z, ref f, rangeM);
        double b = bearingDeg * Math.PI / 180, ue = Math.Sin(b), un = Math.Cos(b);
        double along = sigmaRangeM * sigmaRangeM, across = (sigmaBearingRad * rangeM) * (sigmaBearingRad * rangeM);
        return (z, along * ue * ue + across * un * un, (along - across) * ue * un, along * un * un + across * ue * ue);
    }

    /// <summary>An estimate (already at the reading's time) fixed by a reading z with its covariance: the Kalman update in
    /// its east/north frame, the position kept on the sphere and the velocity in its tangent plane.</summary>
    public static void Update(ref Vec3d position, ref Vec3d velocity, ref Cov4 p, Vec3d z, double r11, double r12, double r22)
    {
        var e = Earth.East(position);
        var nn = Earth.North(position);
        var dz = z - position;
        var (de, dn, dve, dvn) = p.Update(Vec3d.Dot(dz, e), Vec3d.Dot(dz, nn), r11, r12, r22);
        var moved = position + e * de + nn * dn;
        position = moved.Normalized() * Earth.Radius;
        var vel = velocity + e * dve + nn * dvn;
        var up = position.Normalized();
        velocity = vel - up * Vec3d.Dot(vel, up);   // kept in the tangent plane
    }
}
