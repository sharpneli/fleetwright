using System.Numerics;
using Fleetwright.Map.Wakes;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Torpedoes;

/// <summary>The torpedoes' bubble tracks in the world view (docs/visuals/torpedoes.md R2): each run in the book a ribbon
/// along its great circle, from where its bubbles have begun to surface back to where they've faded
/// (<see cref="TorpedoTrack"/>), in the foam lanes' draw with the bubble kind (wakeribbon.frag's own branch). Its
/// half-width spans two of the Gaussian's widths, as the lanes'; under <see cref="TorpedoLook.FloorPx"/> it's drawn that
/// wide and thinner, by the square root of how much it was widened (not by all of it, which would keep its coverage: a
/// departure, so the track still reads zoomed out, where it's the player's cue). The track is straight (a great circle a
/// few km long), so its points go only where its strength and width change: <see cref="RampPoints"/> over the head's
/// surfacing, <see cref="DivePoints"/> over a mount's dive, and one every <see cref="FadeStepS"/> over the slow fade
/// (R6: points every few pixels cost 0.8 ms of CPU for 60 tracks). Nothing allocates.</summary>
public static class TorpedoRibbons
{
    const int RampPoints = 8, DivePoints = 6;
    const double Reach = 1.6, FadeStepS = 8;

    public static void Add(in MapView view, TorpedoBook book, FoamTrails foam, long nowMs, Func<int, ShipClass?> classOf)
    {
        double mpp = view.MetresPerPixel, life = TorpedoTrack.LifeS;
        double floorW = TorpedoLook.FloorPx * mpp / 2;   // the Gaussian's width that spans the floor
        var centre = -view.Forward;
        double reachM = (view.Width + view.Height) * mpp;
        for (int i = 0; i < book.Count; i++)
        {
            ref readonly var t = ref book[i];
            double age = (nowMs - t.LaunchMs) / 1000.0;
            if (age < 0 || age > t.EndS + life)
                continue;
            double hi = Math.Min(age - TorpedoTrack.HeadLagS(t.DepthM), t.EndS), lo = Math.Max(0, age - life);
            if (hi <= lo)
                continue;
            // off the screen: the run's stretch on the water against the view's reach
            var mid = t.At((lo + hi) / 2);
            if (Earth.Distance(mid, centre * Earth.Radius) > reachM + t.SpeedMs * (hi - lo) / 2)
                continue;
            bool dives = t.Class >= 0 && t.Launcher >= 0 && classOf(t.Class) is { } c && t.Launcher < c.Launchers.Length
                && c.Launchers[t.Launcher].Kind == LauncherKind.Mount;
            // from the head back: the surfacing ramp in fine steps, then the fade in coarse ones, finer again over the dive
            double rampS = (TorpedoTrack.FullS(t.DepthM) - TorpedoTrack.HeadLagS(t.DepthM)) / RampPoints;
            double diveTs = dives ? TorpedoTrack.DiveRunM / Math.Max(t.SpeedMs, 1) : 0, diveS = diveTs / DivePoints;
            FoamVertex a0 = default, a1 = default;
            double ts = hi;
            for (int j = 0; ; j++)
            {
                double since = age - ts, run = t.SpeedMs * ts;
                double depth = TorpedoTrack.Depth(run, t.DepthM, dives);
                double strength = TorpedoTrack.Strength(since, depth) * TorpedoLook.Gain;
                double w = TorpedoTrack.WidthM(since, depth) / 2;
                if (w < floorW)
                    (strength, w) = (strength * Math.Sqrt(w / floorW), floorW);
                var (p, f) = (t.Origin, t.Forward);
                Earth.Advance(ref p, ref f, run);
                var side = Vec3d.Cross(p.Normalized(), f);
                var b0 = Corner(view, p, side, -Reach * w, strength, run, w, since);
                var b1 = Corner(view, p, side, Reach * w, strength, run, w, since);
                if (j > 0)
                    foam.AddQuad(a0, a1, b0, b1);
                (a0, a1) = (b0, b1);
                if (ts <= lo)
                    break;
                double step = since < TorpedoTrack.FullS(t.DepthM) ? rampS : FadeStepS;
                if (ts <= diveTs + 1e-6)
                    step = Math.Min(step, diveS);
                ts = Math.Max(ts - Math.Max(step, 0.05), lo);
            }
        }
    }

    static FoamVertex Corner(in MapView view, Vec3d p, Vec3d side, double across, double strength, double run, double width, double since)
    {
        var r = p + side * across - view.Eye;
        return new FoamVertex
        {
            Rel = new Vector3((float)r.X, (float)r.Y, (float)r.Z), Intensity = (float)strength,
            Lane = new Vector2((float)run, (float)across), Width = (float)width, Age = (float)since, Kind = 1,
        };
    }
}
