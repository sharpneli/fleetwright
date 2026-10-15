using System.Numerics;
using Fleetwright.Sim.Combat;

namespace Fleetwright.Map.Magazines;

/// <summary>What a magazine throws (docs/visuals/magazine-explosions.md X4; research/vfx/magazine-explosion-vfx-research.md
/// §2.3, vidgen's <c>Blast._debris</c>, <c>_throw_gunhouse</c>): pieces of plating and fittings hurled at the main event,
/// half of them burning and trailing smoke (the "octopus"), sparks of burning grains, and the gunhouse over the
/// magazine thrown off its barbette. All drag-free ballistic arcs in closed form, so any moment is a function of age;
/// each event's launches drawn once from its seed. Positions in the ship's frame (+x bow, +y starboard) from the
/// magazine, heights over the waterline.</summary>
public sealed class MagazineDebris
{
    public const double Gravity = 9.81;
    /// <summary>The highest a piece is thrown at the reference's 20 t event, m ("hurled hundreds of feet", Hood; "200
    /// feet", Indefatigable): its launch speed's top, lower for smaller events (vidgen's).</summary>
    public const double ApexM = 140;
    /// <summary>Sparks: how many at the reference's event (vidgen's 200-2000; fewer, each a quad), their speed's spread,
    /// m/s, and life, s.</summary>
    public const int Sparks = 160;
    public const double SparkSpeed = 25, SparkLifeS = 2.5;

    public readonly struct Piece(Vector3 v, float radius, bool burning, float trailS)
    {
        /// <summary>Its launch velocity, m/s (x forward, y starboard, z up), its size, m, whether it burns (and trails
        /// smoke), and how long its trail's smoke lasts, s.</summary>
        public readonly Vector3 V = v;
        public readonly float Radius = radius, TrailS = trailS;
        public readonly bool Burning = burning;
    }

    /// <summary>The launch point: over the magazine, a little over the deck.</summary>
    public Vector3 From { get; }
    public Piece[] Pieces { get; }

    /// <summary>The gunhouse: thrown (a mount over the magazine with its barbette), its launch point (the barbette's
    /// top) and velocity, its spin, degrees a second, and tumble (its roof tilting over), and where it comes down: on the
    /// deck (it lands within the hull) or into the sea.</summary>
    public bool Gunhouse { get; }
    public Vector3 HouseFrom { get; }
    public Vector3 HouseV { get; }
    public double HouseSpin { get; }
    public double HouseTumble { get; }

    public MagazineDebris(MagazineBlast ev, uint seed, Fleetwright.Sim.Ships.ShipClass c)
    {
        uint h = seed;
        double U() => (h = Hash(h + 0x9e3779b9u)) / 4294967296.0;
        From = new Vector3((float)ev.X, (float)ev.Y, (float)(ev.DeckZ + 2.5));
        double vmax = Math.Sqrt(2 * Gravity * ApexM * Math.Min(1, ev.K));
        Pieces = new Piece[ev.Debris];
        for (int i = 0; i < Pieces.Length; i++)
        {
            double el = (35 + 53 * U()) * Math.PI / 180, az = 2 * Math.PI * U(), sp = vmax * Math.Pow(U(), 0.6);
            var v = new Vector3((float)(Math.Cos(el) * Math.Cos(az) * sp), (float)(Math.Cos(el) * Math.Sin(az) * sp), (float)(Math.Sin(el) * sp));
            Pieces[i] = new Piece(v, (float)(1.2 + 1.5 * U()), U() < 0.5, (float)(4 + 6 * U()));
        }
        Gunhouse = ev.Mount >= 0 && !double.IsNaN(ev.BarbetteR);
        if (Gunhouse)
        {
            HouseFrom = new Vector3((float)ev.BarbetteX, (float)ev.BarbetteY, (float)ev.BarbetteZ);
            double az = 2 * Math.PI * U(), vz = 20 + 10 * U(), air = 2 * vz / Gravity, vh = (25 + 25 * U()) / air;
            HouseV = new Vector3((float)(vh * Math.Cos(az)), (float)(vh * Math.Sin(az)), (float)vz);
            HouseSpin = (U() * 2 - 1) * 60;
            HouseTumble = (40 + 70 * U()) * (U() < 0.5 ? -1 : 1);
            var land = At(HouseFrom, HouseV, Landing(HouseFrom.Z, HouseV.Z));
            var hull = c.Hull;
            HouseOnDeck = land.X > hull.X0 && land.X < hull.X1 && Math.Abs(land.Y) < hull.HalfWidth(land.X, hull.DeckTop(land.X) - 0.5);
        }
    }

    public bool HouseOnDeck { get; }

    /// <summary>Where an arc from <paramref name="p0"/> at <paramref name="v"/> is <paramref name="t"/> s on.</summary>
    public static Vector3 At(Vector3 p0, Vector3 v, double t) =>
        p0 + v * (float)t - new Vector3(0, 0, (float)(0.5 * Gravity * t * t));

    /// <summary>When an arc from height <paramref name="z0"/> rising at <paramref name="vz"/> comes down to the sea, s.</summary>
    public static double Landing(double z0, double vz) => (vz + Math.Sqrt(vz * vz + 2 * Gravity * Math.Max(z0, 0))) / Gravity;

    /// <summary>A spark's launch from the event's seed: its velocity and life.</summary>
    public static (Vector3 V, double Life) Spark(uint seed, int i)
    {
        uint h = Hash(seed * 31u + (uint)i * 2654435761u);
        double U() => (h = Hash(h + 0x9e3779b9u)) / 4294967296.0;
        double az = 2 * Math.PI * U(), el = Math.Asin(0.2 + 0.8 * U()), sp = SparkSpeed * (0.5 + 1.5 * U());
        return (new Vector3((float)(Math.Cos(el) * Math.Cos(az) * sp), (float)(Math.Cos(el) * Math.Sin(az) * sp), (float)(Math.Sin(el) * sp + 5)),
            1.5 + 2 * U());
    }

    static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return x;
    }
}
