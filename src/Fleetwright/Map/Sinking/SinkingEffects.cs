using Fleetwright.Map.Flashes;
using Fleetwright.Map.Splashes;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Sinking;

/// <summary>A ship going down in the world view (docs/visuals/hits-and-fires.md I7; research/vfx/sinking-vfx-research.md),
/// driven by the attitude she goes under by (<see cref="Attitude.At"/>, the one model the hit queries read), its layers
/// on the models already drawn (marks on the water, short-lived: a visual effect, not a simulated object, the user
/// 2026-10-19):
/// <list type="bullet">
/// <item><b>air boils</b> (water in, air out) over what's under, rising d / 2 m/s and sized S = 0.9 V^⅓ + 0.1 d;</item>
/// <item><b>the closing slam</b> when she's gone: one big boil (3 + 0.9 √area) and a ring on the water (the blasts'
/// quads), then trapped air burping up over the wreck as it sinks on (τ 14 s: later, wider, fainter); her tanks'
/// oil coming up (dark stains spreading) and her debris (small marks).</item>
/// </list>
/// No whirlpool (the research: a myth). Foam marks are the splash book's foam alone; the oil its stains; nothing here
/// reaches the sim. Allocates only for a ship's first frame going down.</summary>
public sealed class SinkingEffects
{
    sealed class Going
    {
        public ShipClass Cls = null!;
        public Vec3d At, Forward;
        public Attitude Settled;
        public LossKind Loss;
        public long LossMs, GoneMs, Seen;
        public double NextBoil, Slam = double.NaN;
        public uint Seed;
        public bool Done;
    }

    /// <summary>Stations along her the layers are sampled at.</summary>
    const int Stations = 24;

    /// <summary>How long the slam's ring and the burps go on, s [EST].</summary>
    public static double AfterS = 50;

    /// <summary>Off: no world view sinking (the table's ring stays).</summary>
    public static bool On = true;

    readonly Dictionary<int, Going> going = [];
    readonly List<int> gone = new(16);
    long frame;

    public int Count => going.Count;

    public void Clear() => going.Clear();

    public void Build(in MapView view, Snapshot s, long drawMs, double speed, double world, Func<int, ShipClass?> classOf, SplashBook splashes,
        BlastRenderer blasts, double blastClock)
    {
        frame++;
        if (!On)
            return;
        double seaRatio = speed > 0 ? Effects.SeaRate(speed) / speed : 1, now = splashes.FoamClock;
        foreach (var v in (ReadOnlySpan<ShipsView>)[s.Ships, s.Sighted])
            for (int k = 0; k < v.Count; k++)
            {
                if (v.Loss[k] == LossKind.None || classOf(v.Class[k]) is not { } c)
                    continue;
                int e = v.Entity[k];
                if (!going.TryGetValue(e, out var g))
                    going[e] = g = new Going { Cls = c, Seed = (uint)e * 2654435761u, NextBoil = now };
                var (p, f) = v.PoseAt(k, drawMs);
                (g.At, g.Forward, g.Settled, g.Loss, g.LossMs, g.GoneMs, g.Seen) = (p, f, v.Attitude[k], v.Loss[k], v.LossMs[k], v.GoneMs[k], frame);
                if (drawMs < g.GoneMs && world > 0)
                    Layers(g, drawMs, now, seaRatio, splashes);
            }

        gone.Clear();
        foreach (var (e, g) in going)
        {
            // gone under (out of the snapshot, or past her time): the slam, once; one lost from sight is only forgotten
            if (double.IsNaN(g.Slam) && (g.Seen != frame || drawMs >= g.GoneMs))
            {
                if (drawMs >= g.GoneMs - 2000)
                    Slam(g, now, seaRatio, blastClock, splashes);
                else
                    g.Done = true;
            }
            if (!double.IsNaN(g.Slam))
            {
                double age = blastClock - g.Slam;
                if (world > 0)
                    blasts.AddSpherical(view, g.At, 0, SlamLambda * g.Cls.Length, age, world, (g.Seed & 0xffff) / 65536f);
                if (age > AfterS)
                    g.Done = true;
            }
            if (g.Done)
                gone.Add(e);
        }
        foreach (int e in gone)
            going.Remove(e);
    }

    /// <summary>A foam mark of radius R m on the splash book is a shell's of this many mm per metre (its foam reaches ~85
    /// calibres).</summary>
    const double FoamMmPerM = 12;

    /// <summary>The slam's ring's length scale, λ per metre of her length [EST].</summary>
    const double SlamLambda = 0.25;

    /// <summary>A point of her frame (heights in the class's frame) under an attitude: where it is over the sea (her plan:
    /// along and across, m) and how high over the water, m. HitQuery.ToWorld's maths, kept local (no frame wanted).</summary>
    public static (double Along, double Across, double Height) Posed(ShipClass c, in Attitude a, double x, double y, double z)
    {
        double wl = c.Vertical.Waterline, lcf = c.Hydrostatics.Lcf;
        double t = double.DegreesToRadians(a.TrimDeg), ph = double.DegreesToRadians(a.HeelDeg);
        double ct = Math.Cos(t), st = Math.Sin(t), cp = Math.Cos(ph), sp = Math.Sin(ph);
        double dx = x - lcf, dz = z - wl;
        double tx = dx * ct + dz * st, tz = -dx * st + dz * ct;
        double hy = y * cp + tz * sp, hz = -y * sp + tz * cp;
        return (tx + lcf, hy, hz - a.SinkageM);
    }

    static Vec3d Sea(Going g, double along, double across)
    {
        var up = g.At.Normalized();
        var stbd = Vec3d.Cross(g.Forward, up).Normalized();
        return (g.At + g.Forward * along + stbd * across).Normalized() * Earth.Radius;
    }

    void Layers(Going g, long ms, double now, double seaRatio, SplashBook splashes)
    {
        var c = g.Cls;
        var a = Attitude.At(c, g.Settled, g.Loss, g.LossMs, g.GoneMs, ms);
        var a2 = Attitude.At(c, g.Settled, g.Loss, g.LossMs, g.GoneMs, ms + 500);
        var hull = c.Hull;
        // how much of her deck is under, and how fast it's going down: the boils' place and pace
        int under = 0;
        double sink = 0;
        for (int i = 0; i < Stations; i++)
        {
            double x = hull.X0 + (hull.X1 - hull.X0) * (i + 0.5) / Stations, deck = hull.DeckTop(x);
            double h = Posed(c, a, x, 0, deck).Height;
            if (h < 0)
            {
                under++;
                sink += Math.Max(-(Posed(c, a2, x, 0, deck).Height - h) / 0.5, 0);
            }
        }
        // air boils over what's under: as often as she's going down fast; each rises d / 2 m/s to the surface
        if (under > 0 && now >= g.NextBoil)
        {
            g.NextBoil = now + Math.Clamp(3 / (1 + 0.5 * sink), 0.4, 4);
            uint n = (uint)(now * 10);
            int pick = (int)(U(g.Seed, n) * Stations);
            double x = hull.X0 + (hull.X1 - hull.X0) * (pick + 0.5) / Stations, deck = hull.DeckTop(x);
            var (al, ac, h) = Posed(c, a, x, (U(g.Seed, n + 1) - 0.5) * 0.5 * c.Beam, deck);
            if (h < 0)
            {
                double d = -h, size = Math.Clamp(0.9 * Math.Cbrt(c.Length * c.Beam * 2 / Stations) + 0.1 * d, 2, 14);
                double rise = d / 2 * seaRatio;
                splashes.Add(Sea(g, al, ac), FoamMmPerM * size, splashes.ColumnClock + rise, now + rise, U(g.Seed, n + 2), column: false,
                    strength: Math.Clamp(1.15 - 0.008 * d, 0.35, 1));
            }
        }
    }

    /// <summary>She's gone: the slam's boil where the last of her went, its ring (its age on the blasts' clock, the sea's),
    /// the trapped air burping up over the wreck as it goes on down (later, wider, fainter, wandering), her oil coming up
    /// and her debris, all dated ahead on the splash book's clocks.</summary>
    void Slam(Going g, double now, double seaRatio, double blastClock, SplashBook splashes)
    {
        var c = g.Cls;
        g.Slam = blastClock;
        double area = 0.3 * c.Length * c.Beam, s = 3 + 0.9 * Math.Sqrt(area);
        splashes.Add(Sea(g, 0, 0), FoamMmPerM * Math.Min(s, 40), splashes.ColumnClock, now, U(g.Seed, 1), column: false);
        // trapped air: ~a dozen burps over a minute, τ 14 s, the wreck descending 7.5 m/s and drifting
        for (uint j = 0; j < 12; j++)
        {
            double t = Math.Min(-14 * Math.Log(Math.Max(U(g.Seed, 10 + j), 1e-3)), AfterS);
            double d = 7.5 * t, size = Math.Clamp(4 + 0.1 * d, 4, 30);
            double along = (U(g.Seed, 30 + j) - 0.5) * 0.3 * c.Length, across = (U(g.Seed, 50 + j) - 0.5) * 0.3 * c.Beam + 0.02 * t;
            double ahead = (t + d / 2) * seaRatio;
            splashes.Add(Sea(g, along, across), FoamMmPerM * size, splashes.ColumnClock + ahead, now + ahead, U(g.Seed, 70 + j), column: false,
                strength: Math.Clamp(1.15 - 0.008 * d, 0.35, 1));
        }
        // her oil: her tanks ruptured, coming up as dark stains spreading over the wreck [EST]
        for (uint j = 0; j < 3; j++)
        {
            double ahead = (2 + 15 * U(g.Seed, 90 + j)) * seaRatio;
            double along = (U(g.Seed, 100 + j) - 0.5) * 0.25 * c.Length, across = (U(g.Seed, 110 + j) - 0.5) * 0.25 * c.Beam;
            splashes.Add(Sea(g, along, across), 250 + 350 * U(g.Seed, 120 + j), splashes.ColumnClock + ahead, now + ahead, U(g.Seed, 130 + j),
                column: false, oil: true);
        }
        // debris: ~25 small pieces over 0.3 L × 0.15 L
        for (uint j = 0; j < 25; j++)
        {
            double along = (U(g.Seed, 150 + j) - 0.5) * 0.3 * c.Length, across = (U(g.Seed, 180 + j) - 0.5) * 0.15 * c.Length;
            double ahead = 3 * U(g.Seed, 210 + j) * seaRatio;
            splashes.Add(Sea(g, along, across), 35, splashes.ColumnClock + ahead, now + ahead, U(g.Seed, 240 + j), column: false, strength: 0.8);
        }
    }

    static float U(uint seed, uint k)
    {
        uint x = seed ^ k * 0x9e3779b9u;
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return (x & 0xffffff) / 16777216f;
    }

    static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
