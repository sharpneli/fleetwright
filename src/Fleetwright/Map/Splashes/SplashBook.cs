using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Splashes;

/// <summary>The splashes the map has seen (docs/visuals/splashes.md P0, P5), kept past the snapshot's while so their
/// foam and stains last, and the two display clocks they're drawn on. Display only: nothing here reaches the sim.
/// <list type="bullet">
/// <item><b>The column's clock</b> runs at the world's speed up to <see cref="Effects.ClockKnee"/>, slower than it
/// above (<see cref="Effects.Rate"/>), so at 60x a column still rises and falls in most of a second. Accumulated
/// frame by frame, so a change of speed never makes a live column jump.</item>
/// <item><b>The foam's clock</b> is the sea's: the world's speed, capped (<see cref="Effects.SeaRate"/>).</item>
/// <item>Each splash is placed on both when first seen, back-dated by its age in the world at the rates of the moment,
/// and two things are decided then and kept: whether its salvo shows a column (<see cref="Effects.KeepShare"/>, by a
/// hash of the salvo, so a salvo shows whole or not at all; the selected ship's always do) and its foam's strength
/// (<see cref="Effects.SeaStrength"/>). Nothing pops when the speed changes.</item>
/// <item>Each animates from its own impact time, so nothing assumes a salvo lands together (decision 5: the ripple
/// comes later).</item>
/// </list>
/// Misses only; hits are the table's marks until the hit effects' pass. A fixed ring: past its size the oldest go first
/// (the faintest foam). Nothing allocates once made.</summary>
public sealed class SplashBook
{
    public struct Entry
    {
        public Vec3d Point;
        public long Ms;
        public int Salvo, Shooter;
        public float CalibreMm, FallDeg, BearingDeg, Seed, FoamStrength;
        public ShellKind Kind;
        public Dye Dye;
        public bool Column;
        /// <summary>The impact's moment on the column's and the foam's clocks, s.</summary>
        public double ColumnT0, FoamT0;
    }

    /// <summary>How long a splash is kept on the foam's clock, s: the foam's minute and a little.</summary>
    public const double KeepS = 65;

    readonly Entry[] ring;
    int head, count;
    long lastMs = long.MinValue;
    int atLast;   // the snapshot's entries (hits too) at lastMs already taken

    public SplashBook(int capacity = 16384) => ring = new Entry[capacity];

    /// <summary>The column's and the foam's clocks, s.</summary>
    public double ColumnClock { get; private set; }
    public double FoamClock { get; private set; }

    public int Count => count;

    /// <summary>The kept entries, oldest first, by index 0..Count-1.</summary>
    public ref readonly Entry this[int i] => ref ring[(head - count + i + ring.Length) % ring.Length];

    /// <summary>A new world: nothing seen.</summary>
    public void Clear()
    {
        (head, count, lastMs, atLast) = (0, 0, long.MinValue, 0);
    }

    /// <summary>A frame: the clocks advanced by <paramref name="dtS"/> real seconds at the clock's
    /// <paramref name="speed"/> (0 paused), the snapshot's new splashes taken (newest first in it), the gone let go.</summary>
    public void Update(SplashesView v, long drawMs, double speed, double dtS, int selected)
    {
        double colRate = Effects.Rate(speed), foamRate = Effects.SeaRate(speed);
        ColumnClock += dtS * colRate;
        FoamClock += dtS * foamRate;
        double colRatio = speed > 0 ? colRate / speed : 1, foamRatio = speed > 0 ? foamRate / speed : 1;
        double keep = speed > 0 ? Effects.KeepShare(speed) : 1, foam = speed > 0 ? Effects.SeaStrength(speed) : 1;

        // the oldest first, skipping what was taken: everything before lastMs, and the first atLast at it (a moment's
        // splashes all come in the same step, so a snapshot has all of them or none)
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
            if (v.Ship[i] >= 0)
                continue;   // a hit: no splash
            double ageS = (drawMs - ms) / 1000.0;
            uint h = Hash((uint)v.Salvo[i]);
            ref var e = ref ring[head];
            e = new Entry
            {
                Point = v.Point[i], Ms = ms, Salvo = v.Salvo[i], Shooter = v.Shooter[i], CalibreMm = (float)v.CalibreMm[i],
                FallDeg = v.FallDeg[i], BearingDeg = v.BearingDeg[i], Kind = v.Kind[i], Dye = v.Dye[i],
                Seed = (Hash((uint)ms ^ Hash((uint)i + 0x9e37u)) & 0xffff) / 65536f,
                Column = h / 4294967296.0 < keep || v.Shooter[i] == selected,
                FoamStrength = (float)foam,
                ColumnT0 = ColumnClock - ageS * colRatio, FoamT0 = FoamClock - ageS * foamRatio,
            };
            head = (head + 1) % ring.Length;
            count = Math.Min(count + 1, ring.Length);
        }

        // the gone, oldest first (roughly in order: they came in by time)
        while (count > 0 && FoamClock - this[0].FoamT0 > KeepS)
            count--;
    }

    /// <summary>A number in [-1, 1] drawn from a splash's seed, one per <paramref name="k"/>: its own size and timing.</summary>
    public static double Signed(float seed, int k) => Hash((uint)(seed * 65536) * 8u + (uint)k) / 2147483648.0 - 1;

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
