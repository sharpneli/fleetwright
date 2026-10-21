using System.Numerics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Flashes;

/// <summary>The gun smoke the map draws (docs/visuals/muzzle-flashes.md F6: drawn by the map alone, a logged exception;
/// the puffs may stay as the look over the smoke system's coarser state). One Gaussian puff per mount fired, its state a
/// pure function of its age on the sea's clock (research/vfx/muzzle-flash-smoke-research.md §5.3), so nothing is
/// simulated. A continuous source's puffs (the funnels' and fires' trails, <see cref="Puff.World"/>) age on the world's
/// clock instead: a trail's shape follows its ship, so it must age with her, the same at any time compression (the user,
/// 2026-10-21: the sim, and what's drawn of it, the same whatever the rate):
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
        /// <summary>A stylised life, s (0: none, its particles only spread): the puff stays whole for 0.4 of it, then thins
        /// to nothing by its end (a funnel's smoke: dense, and ended quickly; docs/visuals/hits-and-fires.md I9, the user).
        /// Such a puff isn't merged: it's gone before merging would matter.</summary>
        public float FadeS;
        public Vector3 Tint;
        public float Seed;
        /// <summary>Who it belongs to: puffs merge only with their own's (a ship's gun smoke; a magazine's soot, its
        /// steam, each keyed apart).</summary>
        public int Shooter;
        /// <summary>It keeps its tint as it ages (a magazine's soot, docs/visuals/magazine-explosions.md X6), where gun
        /// smoke whitens from its muzzle's tint.</summary>
        public bool Keep;
        /// <summary>It ages on the world's clock, not the sea's: a continuous source's trail (<see cref="SmokeBook"/>).
        /// Its <see cref="Born"/> is world time, s.</summary>
        public bool World;
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

    double seaNow, worldNow;

    /// <summary>A puff's clock now: the world's or the sea's.</summary>
    double Now(in Puff p) => p.World ? worldNow : seaNow;

    /// <summary>The wind this frame: toward, degrees, and m/s.</summary>
    public double WindToDeg { get; private set; }
    public double WindMs { get; private set; }

    public void Clear() => count = 0;

    /// <summary>A frame: the shots the book just added become puffs; the merged, the gone and (over capacity) the
    /// oldest let go. <paramref name="now"/> is the sea's clock, <paramref name="worldNow"/> the world's, s.</summary>
    public void Update(ShotBook shots, double now, double worldNow, double windFromDeg, double windMs)
    {
        (WindToDeg, WindMs) = ((windFromDeg + 180) % 360, Math.Max(windMs, 0));
        (seaNow, this.worldNow) = (now, worldNow);
        for (int k = shots.Count - shots.Added; k < shots.Count; k++)
            Spawn(shots[k]);
        // merging and sweeping are slow processes (puffs take seconds to grow into each other and to thin away): every
        // third frame does (docs/visuals/hits-and-fires.md I8: the funnels' and fires' trails are thousands of puffs)
        if (++frames % 3 != 0 && count < Capacity * 3 / 4)
            return;
        Merge(1.5);
        if (count > Capacity / 2)
            Merge(3.0);
        Sweep();
    }

    long frames;

    /// <summary>A puff from elsewhere than a gun (a magazine's after-smoke): kept as given, merged and swept with the
    /// rest. Its <see cref="Puff.Born"/> is on this book's clock (the sea's), and may lie in the past.</summary>
    public void Add(in Puff p)
    {
        if (p.Ap + p.Aw0 <= 0)
            return;
        if (count == Capacity)
        {
            Array.Copy(puffs, 1, puffs, 0, count - 1);
            count--;
        }
        puffs[count++] = p;
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

    /// <summary>Puff i now (on its clock, as of the last update): its centre over the sea (at its source's height:
    /// Earth-centred), its height, m, its σ_h, m, its extinction area, m², and its age, s.</summary>
    public void State(int i, out Vec3d centre, out double z, out double sigma, out double area, out double age)
    {
        ref readonly var p = ref puffs[i];
        age = Math.Max(Now(p) - p.Born, 0);
        double jet = p.JetS > 0 ? 1 - Math.Exp(-age / p.JetS) : 1;
        var wind = Earth.Forward(p.Origin, WindToDeg) * (WindMs * age);
        centre = p.Origin + p.Bore * (p.Jet0 + p.Jet * jet) + wind;
        z = p.Z0 + p.JetZ * jet + 0.6 * p.Lambda * (1 - Math.Exp(-age / (3 + 0.05 * p.Lambda)));
        double g = Growth, core = 0.35 * p.Lambda;
        sigma = Math.Sqrt(core * core + g * g * age * age + 0.5 * age);
        double ramp = p.RampS > 0 ? 1 - Math.Exp(-age / p.RampS) : 1;
        area = ramp * (p.Ap * Fade(p, age) + p.Aw0 * Math.Exp(-age / p.FogS));
    }

    /// <summary>Each ship's settled puffs, oldest first, into the one before when they overlap within
    /// <paramref name="k"/> times the larger σ: A, the centre and the second moment kept.</summary>
    void Merge(double k)
    {
        lastOf.Clear();
        for (int i = 0; i < count; i++)
        {
            ref var p = ref puffs[i];
            double now = Now(p);
            if (now - p.Born < SettleS || p.Ap + p.Aw0 <= 0 || p.FadeS > 0)
                continue;
            if (!lastOf.TryGetValue(p.Shooter, out int j))
            {
                lastOf[p.Shooter] = i;
                continue;
            }
            ref var q = ref puffs[j];
            if (q.World != p.World)
            {
                lastOf[p.Shooter] = i;
                continue;
            }
            State(j, out var cj, out double zj, out double sj, out double aj, out _);
            State(i, out var ci, out double zi, out double si, out double ai, out _);
            if ((ci - cj).Length > k * Math.Max(si, sj) || aj + ai <= 0)
            {
                lastOf[p.Shooter] = i;
                continue;
            }
            double a = aj + ai, wj = aj / a, wi = ai / a;
            var c = cj * wj + ci * wi;
            // the second moment per axis: each σ² and half its centre's offset squared (two axes, isotropic)
            double s2 = wj * (sj * sj + 0.5 * (cj - c).LengthSquared) + wi * (si * si + 0.5 * (ci - c).LengthSquared);
            double z = zj * wj + zi * wi, lambda = Math.Max(q.Lambda, p.Lambda);
            double g = Growth, core = 0.35 * lambda, rest = Math.Max(s2 - core * core, 0);
            double ageEff = (-0.5 + Math.Sqrt(0.25 + 4 * g * g * rest)) / (2 * g * g);
            double fogNow = WaterNow(q, now) + WaterNow(p, now), fogS = Math.Max(q.FogS, p.FogS);
            double particles = ParticlesNow(q, now) + ParticlesNow(p, now);
            // the rise still to come (a fire's plume's buoyancy, docs/visuals/hits-and-fires.md I5), kept over the merged
            // puff's own age; a gun's jet is spent by now
            double riseLeft = wj * RiseLeft(q, now) + wi * RiseLeft(p, now), jetS = Math.Max(q.JetS, p.JetS);
            double jetNow = riseLeft > 0.5 && jetS > 0 ? 1 - Math.Exp(-ageEff / jetS) : 0, jetZ = riseLeft > 0.5 && jetNow < 0.999 ? riseLeft / (1 - jetNow) : 0;
            q = new Puff
            {
                Born = now - ageEff,
                Origin = c - Earth.Forward(c, WindToDeg) * (WindMs * ageEff),
                Bore = default, Jet0 = 0, Jet = 0, JetS = jetZ > 0 ? (float)jetS : 0,
                Z0 = (float)(z - jetZ * jetNow - 0.6 * lambda * (1 - Math.Exp(-ageEff / (3 + 0.05 * lambda)))), JetZ = (float)jetZ,
                Anchor = (float)(q.Anchor * wj + p.Anchor * wi), Lambda = (float)lambda,
                Ap = (float)particles, Aw0 = (float)(fogNow * Math.Exp(ageEff / fogS)), FogS = (float)fogS, RampS = 0,
                Tint = q.Tint * (float)wj + p.Tint * (float)wi, Seed = q.Seed, Shooter = q.Shooter, Keep = q.Keep, World = q.World,
            };
            p.Ap = p.Aw0 = 0;   // merged away: swept below
        }
    }

    /// <summary>The height a puff has still to rise by its jet, m.</summary>
    static double RiseLeft(in Puff p, double now) =>
        p.JetS > 0 && p.JetZ > 0 ? p.JetZ * Math.Exp(-Math.Max(now - p.Born, 0) / p.JetS) : 0;

    /// <summary>A puff's stylised fade at an age: whole for 0.4 of its life, nothing at its end (1: no life set).</summary>
    static double Fade(in Puff p, double age)
    {
        if (p.FadeS <= 0)
            return 1;
        double u = Math.Clamp((age - 0.4 * p.FadeS) / (0.6 * p.FadeS), 0, 1);
        return 1 - u * u * (3 - 2 * u);
    }

    double ParticlesNow(in Puff p, double now)
    {
        double age = Math.Max(now - p.Born, 0);
        return (p.RampS > 0 ? 1 - Math.Exp(-age / p.RampS) : 1) * p.Ap * Fade(p, age);
    }

    double WaterNow(in Puff p, double now)
    {
        double age = Math.Max(now - p.Born, 0);
        return (p.RampS > 0 ? 1 - Math.Exp(-age / p.RampS) : 1) * p.Aw0 * Math.Exp(-age / p.FogS);
    }

    /// <summary>The merged and the gone out, the rest kept in order.</summary>
    void Sweep()
    {
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            ref readonly var p = ref puffs[i];
            if (p.Ap + p.Aw0 <= 0)
                continue;
            if (Now(p) - p.Born > SettleS)
            {
                State(i, out _, out _, out double s, out double a, out _);
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
