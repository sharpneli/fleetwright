using System.Numerics;
using Fleetwright.Map.Flashes;
using Fleetwright.Map.Magazines;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Fires;

/// <summary>The fires that show from outside, in the world view (docs/visuals/hits-and-fires.md I4; decision 3: for every
/// ship, only what shows), each frame from the snapshot's fires (<see cref="ShipsView.ShowAt"/>) on her pose now, so they
/// ride with her:
/// <list type="bullet">
/// <item><b>the flames</b>: a few tongues across the fire's opening or deck, as the magazine's jets (one flame model),
/// standing Heskestad's height L = 0.235 Q^0.4 − 1.02 D (Q in kW over D m: 5 MW over 4 m, 3 m; 20 MW over 6 m, 7 m) up
/// the screen through the effects camera, puffing at a pool fire's 1.5/√D Hz, leaning downwind in the apparent wind (her
/// own way through the air in it), white-yellow at the root and orange-red at the tips;</item>
/// <item><b>charges burning</b>: a brighter, taller flare over the fire, by their own MW;</item>
/// <item><b>the light</b>: a warm point light at each, into the fires' own pool (decision 5), the eight lighting most of
/// what's seen kept.</item>
/// </list>
/// The fires burn on the sea's clock (decision 8): their flicker's clock is the sea's. Nothing allocates.</summary>
public static class FireEffects
{
    const int MaxTongues = 4;

    public static void Build(in MapView view, Snapshot s, long drawMs, double world, double clock, Func<int, ShipClass?> classOf,
        MagazineRenderer bodies, FlashLights lights)
    {
        if (!FireLook.On || world <= 0 || !bodies.Ready)
            return;
        var up0 = (-view.Forward).Normalized();
        lights.Centre(up0 * Earth.Radius);
        double span = view.MetresPerPixel * Math.Max(view.Width, view.Height);
        foreach (var v in (ReadOnlySpan<ShipsView>)[s.Ships, s.Sighted])
            for (int k = 0; k < v.Count; k++)
            {
                if (v.ShowCount[k] == 0 || classOf(v.Class[k]) is not { } c)
                    continue;
                var (p, f) = v.PoseAt(k, drawMs);
                if (!view.Project(p, out double px, out double py))
                    continue;
                double reach = (c.Length + 60) / view.MetresPerPixel;
                if (px < -reach || py < -reach || px > view.Width + reach || py > view.Height + reach)
                    continue;
                var up = p.Normalized();
                var stbd = Vec3d.Cross(f, up).Normalized();
                // the apparent wind: the true wind less her own way, east and north
                var wind = Earth.Forward(p, (s.Weather.WindFromDeg + 180) % 360) * s.Weather.WindMs - f * v.Speed[k];
                var windPlan = new Vector2((float)Vec3d.Dot(wind, view.Right), (float)Vec3d.Dot(wind, view.Up));
                for (int j = v.ShowStart[k]; j < v.ShowStart[k] + v.ShowCount[k]; j++)
                    Fire(view, v, j, p, f, stbd, c.Vertical.Waterline, windPlan, world, clock, bodies, lights, span, v.Entity[k]);
            }
        lights.Finish(view);
    }

    static void Fire(in MapView view, ShipsView v, int j, Vec3d p, Vec3d f, Vec3d stbd, double wl, Vector2 wind, double world, double clock,
        MagazineRenderer bodies, FlashLights lights, double span, int entity)
    {
        var at = v.ShowAt[j];
        double z = at.Z - wl, d = Math.Max(v.ShowWidthM[j], 0.5), q = Math.Max(v.ShowMW[j], 0), qc = Math.Max(v.ShowChargesMW[j], 0);
        var sea = p + f * at.X + stbd * at.Y;
        double l = MagazineBlast.Heskestad(q * 1000, d) * FireLook.Height;
        // a fire too small to stand a flame over its opening still shows its glow there
        l = Math.Max(l, 0.4 * d);
        uint seed = (uint)entity * 2654435761u ^ (uint)v.ShowSpace[j] * 40503u;
        double hz = 1.5 / Math.Sqrt(d), k = Effects.K;
        double ws = wind.Length(), lean = FireLook.Lean * ws / (ws + 4);
        var leanWay = ws > 1e-3 ? wind / (float)ws : Vector2.Zero;
        var (east, north) = (view.Right, view.Up);
        var fPlan = new Vector2((float)Vec3d.Dot(f, east), (float)Vec3d.Dot(f, north));
        var sPlan = new Vector2((float)Vec3d.Dot(stbd, east), (float)Vec3d.Dot(stbd, north));
        int n = Math.Clamp((int)Math.Round(d / 3), 1, MaxTongues);
        double minR = 0.6 * view.MetresPerPixel;
        if ((FireLook.Parts & 1) != 0)
            for (int t = 0; t < n; t++)
            {
                // each tongue's place across the opening, and its own puffing
                double ox = (U(seed, (uint)t * 3) - 0.5) * d * 0.8, oy = (U(seed, (uint)t * 3 + 1) - 0.5) * d * 0.8;
                double phase = 2 * Math.PI * U(seed, (uint)t * 3 + 2);
                double puff = 1 - FireLook.Puff * (0.5 + 0.5 * Math.Sin(2 * Math.PI * hz * clock + phase)) * (0.7 + 0.3 * Math.Sin(2 * Math.PI * 2.7 * hz * clock + 2 * phase));
                double len = l * (0.75 + 0.35 * U(seed, 40 + (uint)t)) * puff;
                var root = fPlan * (float)ox + sPlan * (float)oy;
                var tip = root + leanWay * (float)(lean * len) + new Vector2(0, (float)(k * len * Math.Sqrt(Math.Max(1 - lean * lean, 0))));
                double r0 = Math.Max(0.45 * d / Math.Sqrt(n), minR), r1 = Math.Max(0.35 * r0, minR);
                bodies.AddJet(view, sea + up(sea) * z, root, tip, r0, r1, 0.85, 0.45, len, world, clock + phase, (seed & 0xffff) / 65536f);
            }
        double lc = 0;
        if (qc > 0 && (FireLook.Parts & 1) != 0)
        {
            // the charges' flare: their own fire over the space's, hotter
            lc = MagazineBlast.Heskestad((q + qc) * 1000, d) * FireLook.Height * FireLook.Flare;
            double flick = 0.85 + 0.15 * Math.Sin(2 * Math.PI * 3 * hz * clock);
            var tip = leanWay * (float)(0.5 * lean * lc) + new Vector2(0, (float)(k * lc * flick));
            double r0 = Math.Max(0.6 * d, minR);
            bodies.AddJet(view, sea + up(sea) * z, Vector2.Zero, tip, r0, Math.Max(0.2 * r0, minR), 1, 0.7, lc, world, clock, (seed >> 7 & 0xffff) / 65536f);
        }
        if ((FireLook.Parts & 2) != 0)
        {
            double h = Math.Max(l, lc);
            double kel = qc > 0 ? FireLook.Kelvin + 400 : FireLook.Kelvin;
            lights.AddFire(sea + up(sea) * (z + 0.5 * h), bodies.Light(kel, d * h, FireLook.LightShare), FlashPhotometry.Chroma(kel), Math.Max(d, h) / 2,
                span);
        }

        static Vec3d up(Vec3d x) => x.Normalized();
    }

    /// <summary>A number in [0, 1) from a fire's seed, one per <paramref name="k"/>.</summary>
    static double U(uint seed, uint k)
    {
        uint x = seed ^ k * 0x9e3779b9u;
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return x / 4294967296.0;
    }
}
