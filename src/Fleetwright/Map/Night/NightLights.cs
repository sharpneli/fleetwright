using System.Numerics;
using Fleetwright.Map.Flashes;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Night;

/// <summary>The night's lights in the world view (docs/visuals/night-lights.md Q10b): each star shell's flare a point
/// light where it burns now (its motion closed form, <see cref="FlaresView.Where"/>), each searchlight's beam a Gaussian
/// spot from its lamp along its bearing (carried on between looks by its sweep's rate), its iris opening over
/// <see cref="IrisS"/>.
/// <list type="bullet">
/// <item><see cref="Gather"/>, first in the frame, in physical units: what <see cref="Strongest"/> answers the ships'
/// shadows with (a ship under a flare throws her shadow away from it once it outshines the night).</item>
/// <item><see cref="Into"/>, once the frame's light is known: into the night's pool of <see cref="FlashLights"/>, which
/// every world shader reads (the ships, the sea, its body too, the splashes and the smoke take them with no code of
/// their own), through the frame's night eye: a light of I cd lights a white surface facing it d away as I / d² lux, so
/// its display intensity is I times the display's level a lux (the frame's lit white deck over the real light,
/// <see cref="ShipLights.Lux"/>), the same eye the flashes stand on.</item>
/// </list>
/// The world view's alone (the table's marks stay). Pooled: allocates only to grow.</summary>
public sealed class NightLights
{
    public static bool On = true;

    /// <summary>A gain on their light (1: physical, through the night eye).</summary>
    public static double Gain = 1;

    /// <summary>How long a beam's iris takes to open, s (the record: exposure "rapid" by iris).</summary>
    public static double IrisS = 1;

    /// <summary>The carbon arc's colour [EST: a crater near 3,800 K, white with a faint violet-blue beside the fires],
    /// and a star shell's magnesium flare [EST: brilliant white, faintly yellow-green].</summary>
    public static Vector3 ArcColour = new(0.90f, 0.94f, 1.0f), StarColour = new(1.0f, 0.98f, 0.84f);

    Vec3d[] at = new Vec3d[16], axis = new Vec3d[16];
    double[] cd = new double[16], invSigma2 = new double[16];
    bool[] beam = new bool[16];
    int[] target = new int[16];

    /// <summary>How many lights this frame.</summary>
    public int Count { get; private set; }

    /// <summary>Light i: whether it's a beam, where (Earth-centred: a beam's lamp), its light (cd, a beam's peak, times
    /// its iris and the world's share), a beam's axis (unit, Earth-fixed), its 1-sigma spread (radians) and whom it's
    /// held on (an entity, -1 sweeping).</summary>
    public bool IsBeam(int i) => beam[i];
    public Vec3d At(int i) => at[i];
    public double Candela(int i) => cd[i];
    public Vec3d Axis(int i) => axis[i];
    public double SigmaRad(int i) => invSigma2[i] > 0 ? 1 / Math.Sqrt(invSigma2[i]) : 0;
    public int Target(int i) => target[i];

    /// <summary>This frame's flares and beams from the snapshot at the frame's time <paramref name="drawMs"/>, times the
    /// world's share (none on the table).</summary>
    public void Gather(Snapshot s, long drawMs, double world)
    {
        Count = 0;
        if (!On || world <= 0)
            return;
        var f = s.Flares;
        for (int i = 0; i < f.Count; i++)
            if (drawMs >= f.BurstMs[i] && drawMs < f.OutMs[i])
            {
                // its light over its life: the pop as it lights, its flicker, its burn-out (the star's own, FlareRenderer)
                var (light, _) = FlareRenderer.Envelope((drawMs - f.BurstMs[i]) / 1000.0, (f.OutMs[i] - f.BurstMs[i]) / 1000.0,
                    f.BurstMs[i] ^ (long)(f.Origin[i].X * 1000));
                Add(f.Where(i, drawMs), f.Candela[i] * light * world, false, default, 0, -1);
            }
        var b = s.Beams;
        for (int i = 0; i < b.Count; i++)
        {
            double open = Math.Clamp((drawMs - b.OnMs[i]) / (1000 * IrisS), 0, 1);
            if (open <= 0)
                continue;
            var (lamp, brg) = Beam(s, i, drawMs);
            double sigma = SearchlightModel.BeamSigmaDeg(b.SpreadDeg[i]) * Math.PI / 180;
            Add(lamp, b.Candela[i] * world * open * open * (3 - 2 * open), true, Earth.Forward(lamp, brg), 1 / (sigma * sigma), b.Target[i]);
        }
    }

    /// <summary>Beam i's lamp and true bearing at the frame's time, fixed to her ship as she's drawn: the lamp in her frame
    /// from her pose, held on a ship the map shows at that ship as she's drawn, else the bearing off her bow at the look
    /// carried on by the sweep's rate from her heading now. A ship the map doesn't show (a contact's light seen alone):
    /// the look's lamp and bearing, the sweep carried on.</summary>
    static (Vec3d Lamp, double BearingDeg) Beam(Snapshot s, int i, long drawMs)
    {
        var b = s.Beams;
        double swept = b.RateDegS[i] * (drawMs - b.LookMs) / 1000.0;
        if (!PoseOf(s, b.Ship[i], drawMs, out var p, out var f))
            return (b.Lamp[i], b.BearingDeg[i] + swept);
        var up = p.Normalized();
        var stbd = Vec3d.Cross(f, up);
        var l = b.LampLocal[i];
        var lamp = p + f * l.X + stbd * l.Y + up * l.Z;
        if (b.Target[i] >= 0 && PoseOf(s, b.Target[i], drawMs, out var t, out _))
            return (lamp, Earth.Bearing(lamp, t));
        return (lamp, Earth.Heading(p, f) + b.RelativeDeg[i] + swept);
    }

    /// <summary>A ship's pose as the map draws her (hers or one in sight); false if it doesn't show her.</summary>
    static bool PoseOf(Snapshot s, int entity, long drawMs, out Vec3d p, out Vec3d f)
    {
        foreach (var v in (ReadOnlySpan<ShipsView>)[s.Ships, s.Sighted])
            if (v.IndexOf(entity) is int k and >= 0)
            {
                (p, f) = v.PoseAt(k, drawMs);
                return true;
            }
        (p, f) = (default, default);
        return false;
    }

    void Add(Vec3d where, double candela, bool isBeam, Vec3d dir, double k, int held)
    {
        if (Count == at.Length)
        {
            int n = 2 * Count;
            Array.Resize(ref at, n);
            Array.Resize(ref axis, n);
            Array.Resize(ref cd, n);
            Array.Resize(ref invSigma2, n);
            Array.Resize(ref beam, n);
            Array.Resize(ref target, n);
        }
        (at[Count], cd[Count], beam[Count], axis[Count], invSigma2[Count], target[Count]) = (where, candela, isBeam, dir, k, held);
        Count++;
    }

    /// <summary>The light that falls strongest on a point, Earth-centred: the way toward it (unit) and its light there
    /// facing it, lux (0 with none).</summary>
    public (Vec3d Toward, double Lux) Strongest(Vec3d p)
    {
        Vec3d best = default;
        double most = 0;
        for (int i = 0; i < Count; i++)
        {
            var d = at[i] - p;
            double d2 = Vec3d.Dot(d, d);
            if (d2 < 1)
                continue;
            var l = d / Math.Sqrt(d2);
            double e = cd[i] / d2 * (beam[i] ? Math.Exp(-(1 + Vec3d.Dot(l, axis[i])) * invSigma2[i]) : 1);
            if (e > most)
                (most, best) = (e, l);
        }
        return (best, most);
    }

    /// <summary>Into <paramref name="lights"/>' night pool and the block, by <paramref name="frame"/>'s night eye.</summary>
    public void Into(FlashLights lights, in MapView view, in ShipLights frame)
    {
        if (Count == 0)
            return;
        var up = (-view.Forward).Normalized();
        lights.Centre(up * Earth.Radius);
        double span = view.MetresPerPixel * Math.Max(view.Width, view.Height);
        double perLux = Gain * FlashRenderer.DisplayWhite(frame, up) / Math.Max(frame.Lux, 1e-8);
        for (int i = 0; i < Count; i++)
            lights.AddNight(at[i], cd[i] * perLux, beam[i] ? ArcColour : StarColour, beam[i] ? 0.5 : 2, axis[i], invSigma2[i], span);
        lights.Finish(view);
    }

    /// <summary>Sets a knob, "on:1", "gain:1", "iris:1", "beam:1" (the beams' glow in the air, <see cref="BeamRenderer.Gain"/>)
    /// (the -night= flag, for side-by-side captures); false for a bad one.</summary>
    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], System.Globalization.CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "on": On = v != 0; break;
            case "gain": Gain = Math.Max(v, 0); break;
            case "iris": IrisS = Math.Max(v, 0.01); break;
            case "beam": BeamRenderer.Gain = Math.Max(v, 0); break;
            default: return false;
        }
        return true;
    }
}
