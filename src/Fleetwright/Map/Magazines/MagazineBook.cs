using Fleetwright.Map.Splashes;
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
public sealed class MagazineBook : IThrownTurrets
{
    /// <summary>A debris piece's splash, as a shell's of this calibre per metre of the piece would be, mm (plating at
    /// 30-50 m/s: 36-81 mm, columns of 6-15 m; at 90 they stood round the wreck like candles), and the gunhouse's
    /// (hundreds of tonnes at ~25 m/s, a heavy shell's energy).</summary>
    public const double PieceSplashMm = 60, HouseSplashMm = 300;

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
        /// <summary>What it throws (X4).</summary>
        public MagazineDebris Debris;
        public uint Hash;
        /// <summary>The after-smoke's puffs given to the smoke so far (X6): the stem's fires', the boilers' steam.</summary>
        public int Stem, Steam;
    }

    /// <summary>The most kept at once.</summary>
    public const int Capacity = 32;

    /// <summary>How long an event is kept after its main event at least, s on the effects' clock: its debris' trails and
    /// the after-smoke's spawning outlast the body.</summary>
    public const double KeepS = 3 * 40 + 10;

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
    public void Update(BlastsView v, long drawMs, double speed, double dtS, Func<int, ShipClass?> classOf, ShipsView? ships = null,
        SplashBook? splashes = null)
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
                Debris = new MagazineDebris(ev, h, cls), Hash = h,
            };
            if (splashes != null)
                Splashes(ring[head], splashes, rate > 0 ? seaRate / rate : 1);
            head = (head + 1) % Capacity;
            count = Math.Min(count + 1, Capacity);
        }

        while (count > 0 && Clock - this[0].T0 > MagazineBlast.MainAtS + Math.Max(MagazineLook.FadeTo * this[0].FireballS, KeepS) && MagazineLook.AtS < 0)
            count--;
    }

    /// <summary>Each debris piece's splash, and the gunhouse's if it comes down in the sea, into the splash book once,
    /// dated ahead on its clocks to when it lands (its column and foam wait for it).</summary>
    void Splashes(Entry e, SplashBook book, double seaPerEffects)
    {
        var d = e.Debris;
        double main = MagazineBlast.MainAtS;
        void Add(System.Numerics.Vector3 land, double atS, double calibreMm, int k)
        {
            double ahead = e.T0 + atS - Clock;   // on the effects' clock, from now
            var point = e.At + e.Forward * (land.X - e.Event.X) + e.Starboard * (land.Y - e.Event.Y);
            book.Add(point, calibreMm, book.ColumnClock + ahead, book.FoamClock + ahead * seaPerEffects, (Hash(e.Hash + (uint)k * 7919u) & 0xffff) / 65536f);
        }
        for (int i = 0; i < d.Pieces.Length; i++)
        {
            var p = d.Pieces[i];
            double t = MagazineDebris.Landing(d.From.Z, p.V.Z);
            Add(MagazineDebris.At(d.From, p.V, t), main + t, PieceSplashMm * p.Radius / 2, i);
        }
        if (d.Gunhouse && !d.HouseOnDeck)
        {
            double t = MagazineDebris.Landing(d.HouseFrom.Z, d.HouseV.Z), lift = MagazineBlast.HitLeadS + MagazineBlast.LiftS;
            Add(MagazineDebris.At(d.HouseFrom, d.HouseV, t), lift + t, HouseSplashMm, 9999);
        }
    }

    /// <summary>A gunhouse thrown off its barbette by its magazine, now (<see cref="IThrownTurrets"/>): from the roof's
    /// lifting on its arc, turning and tumbling, until it lands on the deck (it stays there, upright or upside down) or in
    /// the sea (gone).</summary>
    public bool Thrown(int entity, int classMount, out ThrownTurret pose)
    {
        pose = default;
        for (int k = count - 1; k >= 0; k--)
        {
            ref readonly var e = ref this[k];
            if (e.Ship != entity || e.Event.Mount != classMount || !e.Debris.Gunhouse)
                continue;
            double age = MagazineLook.AtS >= 0 && InStill(e) ? MagazineLook.AtS : Clock - e.T0;
            double t = age - MagazineBlast.HitLeadS - MagazineBlast.LiftS;
            if (t < 0)
                return false;
            var d = e.Debris;
            double land = MagazineDebris.Landing(d.HouseFrom.Z, d.HouseV.Z);
            if (t >= land && !d.HouseOnDeck)
            {
                pose = new ThrownTurret(true, 0, 0, 0, 0, 1, 1);
                return true;
            }
            double tt = Math.Min(t, land);
            var p = MagazineDebris.At(d.HouseFrom, d.HouseV, tt);
            double tumble = d.HouseTumble * tt;
            if (t >= land)   // come to rest on the deck: flat, the right way up or upside down
                tumble = Math.Round(tumble / 180) * 180;
            double dz = Math.Max(p.Z - d.HouseFrom.Z, 0);
            pose = new ThrownTurret(false, p.X - d.HouseFrom.X, p.Y - d.HouseFrom.Y, dz, d.HouseSpin * tt, Math.Cos(tumble * Math.PI / 180),
                1 + dz / 150);
            return true;
        }
        return false;
    }

    /// <summary>The after-smoke (X6) into the smoke's book, each puff once, back-dated to its moment on the smoke's
    /// clock (<paramref name="smokeNow"/>) so an event seen late has its whole plume: the stem's fires every
    /// <see cref="MagazineLook.StemEveryS"/> while they burn, soot that keeps its colour, fading with them; the nearest
    /// boiler room's steam over its first seconds, white water fog that evaporates.</summary>
    public void SpawnSmoke(Flashes.SmokeBook smoke, double smokeNow)
    {
        for (int i = 0; i < count; i++)
        {
            ref var e = ref ring[(head - count + i + Capacity) % Capacity];
            var ev = e.Event;
            double since = SeaClock - e.SeaT0 - MagazineBlast.MainAtS;   // on the sea's clock, from the main event
            if (since < 0)
                continue;
            var up = e.At.Normalized();
            double k = ev.K;
            int due = (int)Math.Min(since / MagazineLook.StemEveryS, 3 * MagazineLook.StemFadeS / MagazineLook.StemEveryS) + 1;
            for (; e.Stem < due; e.Stem++)
            {
                double at = e.Stem * MagazineLook.StemEveryS;
                smoke.Add(new Flashes.SmokeBook.Puff
                {
                    Born = smokeNow - (since - at), Origin = e.At + up * ev.DeckZ, Z0 = (float)(ev.DeckZ + 10), Anchor = (float)ev.DeckZ,
                    Lambda = (float)(30 * Math.Sqrt(k)),
                    Ap = (float)(MagazineLook.StemSmokeM2 * MagazineLook.StemEveryS * k * k * Math.Exp(-at / MagazineLook.StemFadeS)),
                    Aw0 = 0, FogS = 1, RampS = 1, Tint = Soot, Seed = (Hash(e.Hash + (uint)e.Stem * 977u) & 0xffff) / 65536f,
                    Shooter = e.Ship | 1 << 30, Keep = true,
                });
            }
            if (double.IsNaN(ev.SteamX))
                continue;
            var boiler = e.At + e.Forward * (ev.SteamX - ev.X) - e.Starboard * ev.Y;
            int steamDue = (int)Math.Min(since / 2, 3) + 1;
            for (; e.Steam < steamDue; e.Steam++)
            {
                double at = e.Steam * 2.0;
                smoke.Add(new Flashes.SmokeBook.Puff
                {
                    Born = smokeNow - (since - at), Origin = boiler + up * ev.DeckZ, Z0 = (float)(ev.DeckZ + 5), Anchor = (float)ev.DeckZ,
                    Lambda = (float)(30 * Math.Sqrt(k)), Ap = 0, Aw0 = (float)(MagazineLook.SteamM2 * k), FogS = 8, RampS = 1,
                    Tint = System.Numerics.Vector3.One, Seed = (Hash(e.Hash + (uint)e.Steam * 7907u) & 0xffff) / 65536f,
                    Shooter = e.Ship | 1 << 29,
                });
            }
        }
    }

    /// <summary>Soot, linear: very dark, a little warm (research §1.2).</summary>
    static readonly System.Numerics.Vector3 Soot = new(0.075f, 0.068f, 0.062f);

    /// <summary>Whether a still (<see cref="MagazineLook.AtS"/>) holds an event: those of the newest 2 s, as the
    /// flashes' stills do.</summary>
    public bool InStill(in Entry e) => count > 0 && e.Ms >= this[count - 1].Ms - 2000;

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
