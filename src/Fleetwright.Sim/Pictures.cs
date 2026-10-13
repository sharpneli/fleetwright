using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>How far a contact has been made out: a ship, its type, its class (perhaps wrongly), its name.</summary>
public enum ContactLevel : byte
{
    Ship,
    Type,
    Class,
    Name,
}

/// <summary>What a ship is reported as at <see cref="ContactLevel.Type"/>.</summary>
public enum ShipType : byte
{
    Unknown,
    Capital,
    Cruiser,
    Destroyer,
    Small,
    Merchant,
}

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

/// <summary>A ship as a navy holds it (docs/world/architecture.md "Knowledge"; docs/battle/plan.md step 3.2d): keyed on
/// the true ship (agreed 2026-10-13: no false merges or splits in the battle module); first and last seen, and whether it
/// was in sight at the last look; the estimate (position and velocity at <see cref="EstMs"/>, with its covariance); how far
/// it's been made out and what it's taken for (perhaps wrongly); and the last report's source.</summary>
public struct Contact
{
    public int Entity;
    public long FirstMs, SeenMs, EstMs;

    /// <summary>When a look was last made for it (seen or not).</summary>
    public long LookedMs;
    public bool InSight;
    public Vec3d Position, Velocity;
    public Cov4 P;
    public ContactLevel Level;
    public ShipType Type;

    /// <summary>The class it's taken for (a registry id), -1 below <see cref="ContactLevel.Class"/>.</summary>
    public int TakenFor;
    public int Observer;
    public EyeKind Eye;

    /// <summary>The ship's side (a navy knows it once it's made out the type: its colours, its build).</summary>
    public byte Side;
}

/// <summary>One navy's picture: its contacts, in slots, by entity through an index. World state: deterministic, saved
/// and hashed. A navy knows at once what any of its ships sees (agreed 2026-10-13); each contact keeps when it was seen and
/// from where, so signals' delays (step 5.1) slot in later.</summary>
public sealed class Picture
{
    Contact[] contacts = new Contact[16];
    readonly Dictionary<int, int> index = new(16);

    public byte Side { get; }
    public int Count { get; private set; }

    internal Picture(byte side) => Side = side;

    public ref readonly Contact this[int i] => ref contacts[i];

    internal ref Contact At(int i) => ref contacts[i];

    /// <summary>The slot of the contact on a ship, or -1.</summary>
    public int IndexOf(int entity) => index.TryGetValue(entity, out int i) ? i : -1;

    internal ref Contact Add(int entity)
    {
        if (Count == contacts.Length)
            Array.Resize(ref contacts, Count * 2);
        index[entity] = Count;
        contacts[Count] = new Contact { Entity = entity, TakenFor = -1 };
        return ref contacts[Count++];
    }

    internal void RemoveAt(int i)
    {
        index.Remove(contacts[i].Entity);
        int last = --Count;
        if (i != last)
        {
            contacts[i] = contacts[last];
            index[contacts[i].Entity] = i;
        }
        contacts[last] = default;
    }

    /// <summary>A contact's estimate carried on to a time: dead reckoned along its velocity on the sphere, its
    /// covariance grown by the settings' manoeuvre noise.</summary>
    public (Vec3d Position, Vec3d Velocity, Cov4 P) EstimateAt(int i, long ms, SimSettings s)
    {
        ref readonly var c = ref contacts[i];
        var p = c.Position;
        var v = c.Velocity;
        var cov = c.P;
        double dt = (ms - c.EstMs) / 1000.0;
        if (dt > 0)
        {
            Carry(ref p, ref v, dt);
            cov.Predict(dt, s.SightManoeuvreMs2 * s.SightManoeuvreMs2);
        }
        return (p, v, cov);
    }

    /// <summary>A position and its velocity on along the great circle for dt seconds.</summary>
    internal static void Carry(ref Vec3d p, ref Vec3d v, double dt)
    {
        double speed = v.Length;
        if (speed <= 1e-9)
            return;
        var f = v * (1 / speed);
        Earth.Advance(ref p, ref f, speed * dt);
        v = f * speed;
    }

    public void Hash(ref StateHash h)
    {
        h.Add(Side);
        h.Add(Count);
        for (int i = 0; i < Count; i++)
        {
            ref readonly var c = ref contacts[i];
            h.Add(c.Entity);
            h.Add(c.FirstMs);
            h.Add(c.SeenMs);
            h.Add(c.LookedMs);
            h.Add(c.EstMs);
            h.Add(c.InSight);
            h.Add(c.Position.X);
            h.Add(c.Position.Y);
            h.Add(c.Position.Z);
            h.Add(c.Velocity.X);
            h.Add(c.Velocity.Y);
            h.Add(c.Velocity.Z);
            h.Add(c.P.Ee);
            h.Add(c.P.Nn);
            h.Add(c.P.VeVe);
            h.Add((int)c.Level);
            h.Add(c.TakenFor);
        }
    }
}
