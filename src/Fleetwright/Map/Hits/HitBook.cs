using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Hits;

/// <summary>The shell hits the map has seen (docs/visuals/hits-and-fires.md I1), kept past the snapshot's while for what
/// they leave, each placed once when first seen: where its points are over the sea (the ship's pose at the hit: a burst's
/// gas and smoke stay in the air where she was, she steams on out from under them) and their heights over the waterline,
/// and its moments on the effects' clock and the sea's. Display only: nothing here reaches the sim.
/// <para>Hits play on the effects' clock (<see cref="Effects.Rate"/>) and are never thinned (decision 8: they're few
/// and decisive); what they leave in the air and on the water on the sea's. Each is back-dated by its age in the world
/// when first seen.</para>
/// A fixed ring: past its size the oldest go first. Nothing allocates once made.</summary>
public sealed class HitBook
{
    public struct Entry
    {
        public long Ms;
        public int Ship, Class, Shooter;
        /// <summary>Her heading over the sea at the hit and her beam's way (to starboard), unit, and the sea point under
        /// her origin.</summary>
        public Vec3d At, Forward, Starboard;
        /// <summary>Where it struck, burst and left her, in her frame (heights over the waterline), and the ways in and
        /// out (unit, her frame).</summary>
        public P3 Point, BurstAt, ExitAt, Direction, ExitDirection;
        public float BurstS, ExitMs, CalibreMm, FillerKg, FillerRe, VentM2, Pieces, Seed;
        public Outcome Outcome;
        public BurstKind Burst;
        public ShellKind Kind;
        public HitShows Shows;
        /// <summary>The hit on the effects' clock and the sea's, s.</summary>
        public double T0, SeaT0;
        /// <summary>Its smoke handed to the smoke's book, and what flew off it to the splashes' (I2).</summary>
        public bool Smoked, Handed;

        /// <summary>The sea point under a point of her frame.</summary>
        public readonly Vec3d Sea(P3 p) => At + Forward * p.X + Starboard * p.Y;
    }

    /// <summary>How long a hit is kept on the effects' clock, s: its fireball and vents are over in a few seconds.</summary>
    public const double KeepS = 15;

    readonly Entry[] ring;
    int head, count;
    long lastMs = long.MinValue;
    int atLast;

    public HitBook(int capacity = 1024) => ring = new Entry[capacity];

    public double Clock { get; private set; }
    public double SeaClock { get; private set; }
    public int Count => count;

    /// <summary>The kept entries, oldest first.</summary>
    public ref readonly Entry this[int i] => ref ring[(head - count + i + ring.Length) % ring.Length];
    public ref Entry Edit(int i) => ref ring[(head - count + i + ring.Length) % ring.Length];

    public void Clear() => (head, count, lastMs, atLast) = (0, 0, long.MinValue, 0);

    /// <summary>A frame: the clocks advanced by <paramref name="dtS"/> real seconds at the clock's
    /// <paramref name="speed"/> (0 paused), the snapshot's new hits taken (newest first in it), the spent let go.</summary>
    public void Update(HitsView v, long drawMs, double speed, double dtS, Func<int, ShipClass?> classOf)
    {
        double rate = Effects.Rate(speed), seaRate = Effects.SeaRate(speed);
        Clock += dtS * rate;
        SeaClock += dtS * seaRate;
        double ratio = speed > 0 ? rate / speed : 1, seaRatio = speed > 0 ? seaRate / speed : 1;

        // the oldest first, skipping what was taken (a moment's hits all come in the same step)
        int skipAt = atLast;
        for (int i = v.Count - 1; i >= 0; i--)
        {
            long ms = v.Ms[i];
            if (ms < lastMs)
                continue;
            if (ms == lastMs && skipAt > 0)
            {
                skipAt--;
                continue;
            }
            if (ms == lastMs)
                atLast++;
            else
                (lastMs, atLast) = (ms, 1);
            if (classOf(v.Class[i]) is not { } cls)
                continue;
            double wl = cls.Vertical.Waterline, ageS = (drawMs - ms) / 1000.0;
            P3 Over(P3 p) => new(p.X, p.Y, p.Z - wl);
            var fwd = v.Forward[i];
            var up = v.At[i].Normalized();
            ring[head] = new Entry
            {
                Ms = ms, Ship = v.Ship[i], Class = v.Class[i], Shooter = v.Shooter[i],
                At = v.At[i], Forward = fwd, Starboard = Vec3d.Cross(fwd, up).Normalized(),
                Point = Over(v.Point[i]), BurstAt = Over(v.BurstAt[i]), ExitAt = Over(v.ExitAt[i]), Direction = v.Direction[i],
                ExitDirection = v.ExitDirection[i],
                BurstS = v.BurstS[i], ExitMs = v.ExitMs[i], CalibreMm = v.CalibreMm[i], FillerKg = v.FillerKg[i], FillerRe = v.FillerRe[i],
                VentM2 = v.VentM2[i], Pieces = v.Pieces[i], Outcome = v.Outcome[i], Burst = v.Burst[i], Kind = v.Kind[i], Shows = v.Shows[i],
                Seed = (Hash((uint)ms ^ Hash((uint)i * 977u + (uint)v.Ship[i])) & 0xffff) / 65536f,
                T0 = Clock - ageS * ratio, SeaT0 = SeaClock - ageS * seaRatio,
            };
            head = (head + 1) % ring.Length;
            count = Math.Min(count + 1, ring.Length);
        }

        while (count > 0 && Clock - this[0].T0 > KeepS && HitLook.AtS < 0)
            count--;
    }

    /// <summary>Whether an entry is drawn in a still (<see cref="HitLook.AtS"/>): the newest 2 s of hits.</summary>
    public bool InStill(in Entry e) => count > 0 && e.Ms >= this[count - 1].Ms - 2000;

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
