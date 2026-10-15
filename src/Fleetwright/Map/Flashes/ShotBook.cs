using Fleetwright.Map.Splashes;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Flashes;

/// <summary>The mounts fired the map has seen (docs/visuals/muzzle-flashes.md F0, F2), kept past the snapshot's while
/// for the smoke to come, each with what its flash needs decided once when first seen: its gun's numbers
/// (<see cref="MuzzleFlash"/>, cached per class and turret type), where its barrel tips were, and its own draw (the
/// secondary flash burning or not, its intensity's scatter, its start offset, a seed). Display only: nothing here
/// reaches the sim.
/// <para>The flash plays on the effects' clock (<see cref="Effects.Rate"/>): the world's speed up to the knee, slower
/// above, accumulated frame by frame so a change of speed never makes a live flash jump. Each shot is placed on it when
/// first seen, back-dated by its age in the world, and whether its flash shows is decided then and kept (the salvo's
/// share under compression).</para>
/// A fixed ring: past its size the oldest go first. Nothing allocates once made, but the first sight of a class's gun.</summary>
public sealed class ShotBook
{
    public struct Entry
    {
        public long Ms;
        public int Shooter, Salvo, Barrels;
        /// <summary>The middle of the mount's barrel tips (on the sprite), and the bore's way over the sea, unit.</summary>
        public Vec3d Tip, Forward;
        public float ElevationDeg, Seed;
        /// <summary>The gun: bore and Mach disk, m; emptying time, s; the secondary's peak temperature, K; the spacing of
        /// the barrels, m.</summary>
        public float BoreM, MachDiskM, EmptyS, SecondaryK, SpacingM;
        /// <summary>This shot's fireball length λ_f (with its secondary flash, or the blast's alone), m, and the secondary's
        /// life, s (0 when it doesn't burn), and its intensity's scatter.</summary>
        public float FlashLengthM, SecondaryS, Scatter;
        /// <summary>The shot's moment on the flash's clock, s (its start offset included).</summary>
        public double FlashT0;
        /// <summary>Whether its flash shows: under compression a salvo keeps it with <see cref="Effects.KeepShare"/>, by a
        /// hash of the salvo, so a salvo flashes whole or not at all; the selected ship's always do.</summary>
        public bool Shows;
    }

    /// <summary>How long a shot is kept on the flash's clock, s (its flash is over within ~2 s).</summary>
    public const double KeepS = 4;

    readonly Entry[] ring;
    int head, count;
    long lastMs = long.MinValue;
    int atLast;
    readonly Dictionary<long, MuzzleFlash> guns = [];

    public ShotBook(int capacity = 8192) => ring = new Entry[capacity];

    /// <summary>The flash's clock, s.</summary>
    public double FlashClock { get; private set; }

    public int Count => count;

    /// <summary>The kept entries, oldest first, by index 0..Count-1.</summary>
    public ref readonly Entry this[int i] => ref ring[(head - count + i + ring.Length) % ring.Length];

    public void Clear()
    {
        (head, count, lastMs, atLast) = (0, 0, long.MinValue, 0);
        guns.Clear();
    }

    /// <summary>A frame: the clock advanced by <paramref name="dtS"/> real seconds at the clock's
    /// <paramref name="speed"/> (0 paused), the snapshot's new shots taken (newest first in it), the gone let go.</summary>
    public void Update(ShotsView v, long drawMs, double speed, double dtS, Func<int, ShipClass?> classOf, int selected = -1)
    {
        double rate = Effects.Rate(speed);
        FlashClock += dtS * rate;
        double ratio = speed > 0 ? rate / speed : 1;
        double keep = speed > 0 ? Effects.KeepShare(speed) : 1;

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
            var cls = classOf(v.Class[i]);
            int mount = v.Mount[i];
            if (cls == null || mount < 0 || mount >= cls.Mounts.Length)
                continue;
            int type = cls.Mounts[mount].Type;
            ref readonly var tt = ref cls.TurretTypes[type];
            long key = (long)v.Class[i] << 20 | (uint)type;
            if (!guns.TryGetValue(key, out var gun))
                guns[key] = gun = MuzzleFlash.Of(cls, mount, FlashLook.Humidity, FlashLook.AirC);

            uint h = Hash((uint)ms ^ Hash((uint)v.Shooter[i] * 977u + (uint)mount));
            float seed = (h & 0xffff) / 65536f;
            bool burns = U(h, 1) < gun.SecondaryP;
            double scatter = Math.Exp(FlashLook.Scatter * Gaussian(h));
            double lf = gun.FlashLengthM(burns ? scatter : 0);
            var fwd = v.Forward[i];
            double ageS = (drawMs - ms) / 1000.0;
            ref var e = ref ring[head];
            e = new Entry
            {
                Ms = ms, Shooter = v.Shooter[i], Salvo = v.Salvo[i], Barrels = Math.Max(v.Barrels[i], 1),
                Tip = v.Muzzle[i] + fwd * tt.TipM, Forward = fwd, ElevationDeg = v.ElevationDeg[i], Seed = seed,
                BoreM = (float)gun.BoreM, MachDiskM = (float)gun.MachDiskM, EmptyS = (float)gun.EmptyS, SecondaryK = (float)gun.SecondaryK,
                SpacingM = (float)tt.Spacing, FlashLengthM = (float)lf, SecondaryS = burns ? (float)MuzzleFlash.SecondaryS(lf) : 0,
                Scatter = (float)scatter,
                FlashT0 = FlashClock - ageS * ratio + FlashLook.OffsetS * U(h, 2),
                Shows = Hash((uint)v.Salvo[i] ^ 0x5bd1e995u) / 4294967296.0 < keep || v.Shooter[i] == selected,
            };
            head = (head + 1) % ring.Length;
            count = Math.Min(count + 1, ring.Length);
        }

        while (count > 0 && FlashClock - this[0].FlashT0 > KeepS && FlashLook.AtS < 0)
            count--;
    }

    /// <summary>A number in [0, 1) from a hash, one per <paramref name="k"/>.</summary>
    static double U(uint h, uint k) => Hash(h * 8u + k) / 4294967296.0;

    /// <summary>A standard normal from a hash (Box-Muller).</summary>
    static double Gaussian(uint h)
    {
        double u1 = Math.Max(U(h, 3), 1e-9), u2 = U(h, 4);
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
