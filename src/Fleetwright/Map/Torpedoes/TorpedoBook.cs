using Fleetwright.Sim;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Torpedoes;

/// <summary>The torpedoes the map has seen (docs/visuals/torpedoes.md R0), kept past their run so their tracks fade where
/// they lie, for the table's line and the world view's alike. Each is known by its serial, so two of one spread never
/// share an entry. A run takes its end (how it ended, its charge) when the snapshot's ends show it. Display only: nothing
/// here reaches the sim. Nothing allocates once grown.</summary>
public sealed class TorpedoBook
{
    public struct Run
    {
        /// <summary>Where it entered the water (on the surface) and its heading there.</summary>
        public Vec3d Origin, Forward;
        public long LaunchMs, Serial;
        /// <summary>Its speed, m/s; seconds after launch its run ends; the depth it runs at, m.</summary>
        public double SpeedMs, EndS, DepthM;
        /// <summary>Who fired it, the shooter's class (an index, -1 unknown), its launcher and its torpedo (the class's
        /// rows).</summary>
        public int Shooter, Class, Launcher, Row;
        /// <summary>Whether its end has been seen, how it ended and its charge, kg.</summary>
        public bool Ended;
        public TorpedoEnd How;
        public double ChargeKg;

        /// <summary>Where it is after running for <paramref name="runS"/> seconds (on the surface over it).</summary>
        public readonly Vec3d At(double runS)
        {
            var (p, f) = (Origin, Forward);
            Earth.Advance(ref p, ref f, SpeedMs * Math.Clamp(runS, 0, EndS));
            return p;
        }
    }

    Run[] runs = new Run[64];
    int count;
    readonly Dictionary<long, int> bySerial = new(64);

    public int Count => count;

    /// <summary>The runs kept, in no order.</summary>
    public ref readonly Run this[int i] => ref runs[i];

    /// <summary>A new world: nothing seen.</summary>
    public void Clear()
    {
        count = 0;
        bySerial.Clear();
    }

    /// <summary>A frame: the snapshot's torpedoes taken (their run as it now stands: a hit shortens it), their ends marked,
    /// and the runs whose tracks have faded <paramref name="keepS"/> after their end let go.</summary>
    public void Update(TorpedoesView v, TorpedoEndsView e, long drawMs, double keepS)
    {
        for (int i = 0; i < v.Count; i++)
        {
            if (!bySerial.TryGetValue(v.Serial[i], out int k))
            {
                if (count == runs.Length)
                    Array.Resize(ref runs, runs.Length * 2);
                k = count++;
                bySerial[v.Serial[i]] = k;
                runs[k] = new Run { Serial = v.Serial[i] };
            }
            ref var r = ref runs[k];
            (r.Origin, r.Forward, r.LaunchMs, r.SpeedMs, r.EndS, r.DepthM) = (v.Origin[i], v.Forward[i], v.LaunchMs[i], v.SpeedMs[i], v.EndS[i], v.DepthM[i]);
            (r.Shooter, r.Class, r.Launcher, r.Row) = (v.Shooter[i], v.Class[i], v.Launcher[i], v.Row[i]);
        }
        for (int i = 0; i < e.Count; i++)
            if (bySerial.TryGetValue(e.Serial[i], out int k) && !runs[k].Ended)
            {
                ref var r = ref runs[k];
                (r.Ended, r.How, r.ChargeKg) = (true, e.How[i], e.ChargeKg[i]);
                r.EndS = Math.Min(r.EndS, (e.Ms[i] - r.LaunchMs) / 1000.0);
            }
        for (int k = count - 1; k >= 0; k--)
        {
            if ((drawMs - runs[k].LaunchMs) / 1000.0 <= runs[k].EndS + keepS)
                continue;
            bySerial.Remove(runs[k].Serial);
            if (k != --count)
            {
                runs[k] = runs[count];
                bySerial[runs[k].Serial] = k;
            }
        }
    }
}
