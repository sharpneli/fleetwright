using System.Numerics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Flashes;

/// <summary>The gun smoke the map draws (docs/visuals/muzzle-flashes.md F6: drawn by the map alone, a logged exception;
/// the puffs may stay as the look over the smoke system's coarser state). One Gaussian puff per mount fired, its state a
/// pure function of its age on the sea's clock (research/vfx/muzzle-flash-smoke-research.md §5.3), so nothing is
/// simulated:
/// <list type="bullet">
/// <item>carried along the bore by the gas jet, 2.5 λ_f over τ_m = 0.04 λ_f + 0.05 s, past the Mach disk;</item>
/// <item>drifting with the wind;</item>
/// <item>rising 0.6 λ_f as it cools, over 3 + 0.05 λ_f s;</item>
/// <item>spreading as σ_h² = (0.35 λ_f)² + ((0.07 U + 0.2) t)² + 0.5 t, σ_z = 0.8 σ_h;</item>
/// <item>its extinction area A ramping in after the flash (full A at once haloes the young flash), the water fog
/// evaporating over τ_w.</item>
/// </list>
/// The count is held down by merging (research §5.5): a ship's settled puffs that overlap (within 1.5 times the larger σ) become
/// one, keeping their A, their centre and their second moment; the merged puff carries on from an age that gives its
/// σ. A puff goes when its peak optical depth falls under <see cref="GoneTau"/>. Over <see cref="Capacity"/>, it merges
/// harder, then the oldest go. O(n) a frame; nothing allocates once made.</summary>
public sealed class SmokeBook
{
    public struct Puff
    {
        /// <summary>Its birth on the sea's clock, s (a merged puff's: the age that gives its spread).</summary>
        public double Born;
        /// <summary>Where it started (a point at its source's height), and the bore's way over the sea there (zero once
        /// settled or merged).</summary>
        public Vec3d Origin, Bore;
        /// <summary>Along the bore: where it starts and the jet's carry, m, and the jet's time, s.</summary>
        public float Jet0, Jet, JetS;
        /// <summary>Its height at the start and the jet's share of it, m; the height its drawing is anchored at.</summary>
        public float Z0, JetZ, Anchor;
        /// <summary>The fireball's length λ_f (its core's size and its rise), m.</summary>
        public float Lambda;
        /// <summary>The particles' extinction area and the water fog's at birth, m² (the sea's strength in), the fog's
        /// evaporation and the ramp's time, s (0: full).</summary>
        public float Ap, Aw0, FogS, RampS;
        public Vector3 Tint;
        public float Seed;
        public int Shooter;
    }

    public const int Capacity = 4096;
    /// <summary>A puff is gone when its peak optical depth falls under this.</summary>
    public const double GoneTau = 0.01;
    /// <summary>A puff is settled (the jet spent, the ramp done) and may merge from this age, s.</summary>
    public const double SettleS = 3;

    readonly Puff[] puffs = new Puff[Capacity];
    int count;
    readonly Dictionary<int, int> lastOf = new(256);

    public int Count => count;
    public ref readonly Puff this[int i] => ref puffs[i];

    /// <summary>The wind this frame: toward, degrees, and m/s.</summary>
    public double WindToDeg { get; private set; }
    public double WindMs { get; private set; }

    public void Clear() => count = 0;

    /// <summary>A frame: the shots the book just added become puffs; the merged, the gone and (over capacity) the
    /// oldest let go. <paramref name="now"/> is the sea's clock.</summary>
    public void Update(ShotBook shots, double now, double windFromDeg, double windMs)
    {
        (WindToDeg, WindMs) = ((windFromDeg + 180) % 360, Math.Max(windMs, 0));
        for (int k = shots.Count - shots.Added; k < shots.Count; k++)
            Spawn(shots[k]);
        Merge(now, 1.5);
        if (count > Capacity / 2)
            Merge(now, 3.0);
        Sweep(now);
    }

    void Spawn(in ShotBook.Entry e)
    {
        if (e.SmokeM2 + e.FogM2 <= 0)
            return;
        if (count == Capacity)
        {
            Array.Copy(puffs, 1, puffs, 0, count - 1);   // the oldest goes (rare: merging keeps well under)
            count--;
        }
        double lf = e.FlashLengthM, el = e.ElevationDeg * Math.PI / 180, ce = Math.Cos(el), se = Math.Sin(el);
        double ramp = 0.5 * Math.Max(e.SecondaryS, e.EmptyS) + 0.004;
        puffs[count++] = new Puff
        {
            Born = e.SeaT0, Origin = e.Tip, Bore = e.Forward,
            Jet0 = (float)(e.MachDiskM * ce), Jet = (float)(2.5 * lf * ce), JetS = (float)(0.04 * lf + 0.05),
            Z0 = (float)(e.HeightM + e.MachDiskM * se), JetZ = (float)(2.5 * lf * se), Anchor = e.HeightM,
            Lambda = (float)lf, Ap = e.SmokeM2 * e.SeaStrength, Aw0 = e.FogM2 * e.SeaStrength, FogS = Math.Max(e.FogS, 0.1f),
            RampS = (float)ramp, Tint = e.Tint, Seed = e.Seed, Shooter = e.Shooter,
        };
    }

    /// <summary>The spread's growth rate with the wind, m/s.</summary>
    double Growth => 0.07 * WindMs + 0.2;

    /// <summary>Puff i at <paramref name="now"/>: its centre over the sea (at its source's height: Earth-centred), its
    /// height, m, its σ_h, m, its extinction area, m², and its age, s.</summary>
    public void State(int i, double now, out Vec3d centre, out double z, out double sigma, out double area, out double age)
    {
        ref readonly var p = ref puffs[i];
        age = Math.Max(now - p.Born, 0);
        double jet = p.JetS > 0 ? 1 - Math.Exp(-age / p.JetS) : 1;
        var wind = Earth.Forward(p.Origin, WindToDeg) * (WindMs * age);
        centre = p.Origin + p.Bore * (p.Jet0 + p.Jet * jet) + wind;
        z = p.Z0 + p.JetZ * jet + 0.6 * p.Lambda * (1 - Math.Exp(-age / (3 + 0.05 * p.Lambda)));
        double g = Growth, core = 0.35 * p.Lambda;
        sigma = Math.Sqrt(core * core + g * g * age * age + 0.5 * age);
        double ramp = p.RampS > 0 ? 1 - Math.Exp(-age / p.RampS) : 1;
        area = ramp * (p.Ap + p.Aw0 * Math.Exp(-age / p.FogS));
    }

    /// <summary>Each ship's settled puffs, oldest first, into the one before when they overlap within
    /// <paramref name="k"/> times the larger σ: A, the centre and the second moment kept.</summary>
    void Merge(double now, double k)
    {
        lastOf.Clear();
        for (int i = 0; i < count; i++)
        {
            ref var p = ref puffs[i];
            if (now - p.Born < SettleS || p.Ap + p.Aw0 <= 0)
                continue;
            if (!lastOf.TryGetValue(p.Shooter, out int j))
            {
                lastOf[p.Shooter] = i;
                continue;
            }
            State(j, now, out var cj, out double zj, out double sj, out double aj, out _);
            State(i, now, out var ci, out double zi, out double si, out double ai, out _);
            if ((ci - cj).Length > k * Math.Max(si, sj) || aj + ai <= 0)
            {
                lastOf[p.Shooter] = i;
                continue;
            }
            ref var q = ref puffs[j];
            double a = aj + ai, wj = aj / a, wi = ai / a;
            var c = cj * wj + ci * wi;
            // the second moment per axis: each σ² and half its centre's offset squared (two axes, isotropic)
            double s2 = wj * (sj * sj + 0.5 * (cj - c).LengthSquared) + wi * (si * si + 0.5 * (ci - c).LengthSquared);
            double z = zj * wj + zi * wi, lambda = Math.Max(q.Lambda, p.Lambda);
            double g = Growth, core = 0.35 * lambda, rest = Math.Max(s2 - core * core, 0);
            double ageEff = (-0.5 + Math.Sqrt(0.25 + 4 * g * g * rest)) / (2 * g * g);
            double fogNow = WaterNow(q, now) + WaterNow(p, now), fogS = Math.Max(q.FogS, p.FogS);
            double particles = ParticlesNow(q, now) + ParticlesNow(p, now);
            q = new Puff
            {
                Born = now - ageEff,
                Origin = c - Earth.Forward(c, WindToDeg) * (WindMs * ageEff),
                Bore = default, Jet0 = 0, Jet = 0, JetS = 0,
                Z0 = (float)(z - 0.6 * lambda * (1 - Math.Exp(-ageEff / (3 + 0.05 * lambda)))), JetZ = 0,
                Anchor = (float)(q.Anchor * wj + p.Anchor * wi), Lambda = (float)lambda,
                Ap = (float)particles, Aw0 = (float)(fogNow * Math.Exp(ageEff / fogS)), FogS = (float)fogS, RampS = 0,
                Tint = q.Tint * (float)wj + p.Tint * (float)wi, Seed = q.Seed, Shooter = q.Shooter,
            };
            p.Ap = p.Aw0 = 0;   // merged away: swept below
        }
    }

    double ParticlesNow(in Puff p, double now)
    {
        double age = Math.Max(now - p.Born, 0);
        return (p.RampS > 0 ? 1 - Math.Exp(-age / p.RampS) : 1) * p.Ap;
    }

    double WaterNow(in Puff p, double now)
    {
        double age = Math.Max(now - p.Born, 0);
        return (p.RampS > 0 ? 1 - Math.Exp(-age / p.RampS) : 1) * p.Aw0 * Math.Exp(-age / p.FogS);
    }

    /// <summary>The merged and the gone out, the rest kept in order.</summary>
    void Sweep(double now)
    {
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            ref readonly var p = ref puffs[i];
            if (p.Ap + p.Aw0 <= 0)
                continue;
            if (now - p.Born > SettleS)
            {
                State(i, now, out _, out _, out double s, out double a, out _);
                if (a / (2 * Math.PI * s * s) < GoneTau)
                    continue;
            }
            if (n != i)
                puffs[n] = puffs[i];
            n++;
        }
        count = n;
    }
}
