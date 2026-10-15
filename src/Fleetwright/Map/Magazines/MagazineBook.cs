using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Magazines;

/// <summary>The magazines set off that the map has seen (docs/visuals/magazine-explosions.md X0, X2), each with what its
/// drawing needs decided once when first seen: the event (<see cref="MagazineBlast"/>, cached per class and room), where
/// the ship was, its scatter and seed. Display only: nothing here reaches the sim.
/// <para>The event plays on the effects' clock (<see cref="Effects.Rate"/>), never thinned (a magazine is the event the
/// eye must catch); what lies on the water and the after-smoke on the sea's. Each is placed on both when first seen,
/// back-dated by its age in the world, so one seen late (zoomed in on, or off screen when it went) is drawn at its own
/// age.</para>
/// Nothing allocates once made, but the first sight of a class's magazine.</summary>
public sealed class MagazineBook
{
    public struct Entry
    {
        public long Ms;
        public int Ship;
        public MagazineBlast Event;
        /// <summary>The sea's point under the magazine (Earth-centred), the ship's heading over the sea (unit) and her
        /// beam's way (to starboard).</summary>
        public Vec3d At, Forward, Starboard;
        /// <summary>This event's fireball (the research's scatter in), m and s.</summary>
        public double FireballM, FireballS;
        public float Seed;
        /// <summary>The hit on the effects' clock and the sea's, s.</summary>
        public double T0, SeaT0;
        /// <summary>The hit's point in the ship's frame, its height over the waterline (the hit's flash).</summary>
        public P3 Hit;
        /// <summary>Her mounts' bearings when first seen, degrees clockwise from ahead (the gun ports' jets go along the
        /// trained guns); the class's rests when she wasn't in view.</summary>
        public double[] Bearings;
    }

    /// <summary>The most kept at once.</summary>
    public const int Capacity = 32;

    readonly Entry[] ring = new Entry[Capacity];
    int head, count;
    long lastMs = long.MinValue;
    readonly Dictionary<(int Class, int Room), MagazineBlast> events = [];

    public double Clock { get; private set; }
    public double SeaClock { get; private set; }
    public int Count => count;

    /// <summary>The kept entries, oldest first.</summary>
    public ref readonly Entry this[int i] => ref ring[(head - count + i + Capacity) % Capacity];

    /// <summary>A frame: the clocks advanced by <paramref name="dtS"/> real seconds at the clock's
    /// <paramref name="speed"/> (0 paused), the snapshot's new events taken (newest first in it), the spent let go.</summary>
    public void Update(BlastsView v, long drawMs, double speed, double dtS, Func<int, ShipClass?> classOf, ShipsView? ships = null)
    {
        double rate = Effects.Rate(speed), seaRate = Effects.SeaRate(speed);
        Clock += dtS * rate;
        SeaClock += dtS * seaRate;
        double ratio = speed > 0 ? rate / speed : 1, seaRatio = speed > 0 ? seaRate / speed : 1;

        for (int i = v.Count - 1; i >= 0; i--)
        {
            long ms = v.Ms[i];
            if (ms < lastMs || Seen(ms, v.Ship[i]))
                continue;
            lastMs = ms;
            if (classOf(v.Class[i]) is not { } cls || Room(cls, v.Room[i]) is not int room)
                continue;
            if (!events.TryGetValue((v.Class[i], room), out var ev))
                events[(v.Class[i], room)] = ev = MagazineBlast.Of(cls, room);
            uint h = Hash((uint)ms ^ Hash((uint)v.Ship[i] * 2654435761u));
            var fwd = v.Forward[i];
            var up = v.At[i].Normalized();
            var stbd = Vec3d.Cross(fwd, up).Normalized();
            double scale = Math.Exp(MagazineLook.ScatterD * Gaussian(h, 1)), life = Math.Exp(MagazineLook.ScatterS * Gaussian(h, 3));
            double ageS = (drawMs - ms) / 1000.0;
            // her mounts' bearings, if she's in view (a new event's array: the start may allocate, the frames don't)
            var bearings = new double[cls.Mounts.Length];
            int k = ships?.IndexOf(v.Ship[i]) ?? -1, start = k >= 0 ? ships!.MountStart[k] : -1;
            for (int m = 0; m < bearings.Length; m++)
                bearings[m] = start >= 0 ? ships!.MountBearingDeg[start + m] : cls.Mounts[m].RestDeg;
            // a shell's or torpedo's hit where it landed; a fire's or the dev's over the magazine, on its barbette's top
            var hit = v.Cause[i] is BlastCause.Shell or BlastCause.Torpedo ? v.Point[i]
                : new P3(ev.X, ev.Y, (double.IsNaN(ev.BarbetteR) ? ev.DeckZ : ev.BarbetteZ) + cls.Vertical.Waterline);
            ring[head] = new Entry
            {
                Ms = ms, Ship = v.Ship[i], Event = ev,
                At = v.At[i] + fwd * ev.X + stbd * ev.Y, Forward = fwd, Starboard = stbd,
                FireballM = ev.FireballM * scale, FireballS = ev.FireballS * life, Seed = (h & 0xffff) / 65536f,
                T0 = Clock - ageS * ratio, SeaT0 = SeaClock - ageS * seaRatio,
                Hit = new P3(hit.X, hit.Y, hit.Z - cls.Vertical.Waterline), Bearings = bearings,
            };
            head = (head + 1) % Capacity;
            count = Math.Min(count + 1, Capacity);
        }

        while (count > 0 && Clock - this[0].T0 > MagazineBlast.MainAtS + MagazineLook.VolumeS && MagazineLook.AtS < 0)
            count--;
    }

    bool Seen(long ms, int ship)
    {
        for (int k = 0; k < count; k++)
            if (this[k].Ms == ms && this[k].Ship == ship)
                return true;
        return false;
    }

    /// <summary>The magazine that went: the record's, else the class's fullest; null when it has none.</summary>
    static int? Room(ShipClass c, int room)
    {
        if (room >= 0 && room < c.Rooms.Length && c.Rooms[room].Kind == RoomKind.Magazine)
            return room;
        int best = -1;
        for (int r = 0; r < c.Rooms.Length; r++)
            if (c.Rooms[r].Kind == RoomKind.Magazine && (best < 0 || c.Rooms[r].Tonnes > c.Rooms[best].Tonnes))
                best = r;
        return best >= 0 ? best : null;
    }

    static double U(uint h, uint k) => Hash(h * 8u + k) / 4294967296.0;

    static double Gaussian(uint h, uint k)
    {
        double u1 = Math.Max(U(h, k), 1e-9), u2 = U(h, k + 1);
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
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
