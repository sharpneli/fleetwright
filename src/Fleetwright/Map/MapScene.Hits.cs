using System.Globalization;
using Fleetwright.Map.Hits;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map;

/// <summary>Shell hits in the world view (docs/visuals/hits-and-fires.md I1): the hits seen and what they show each
/// frame, and the dev's hits bench (<c>-hitbench=TARGET:SHOOTER[:every_s]</c>): the shooter's shells struck at a still
/// target one after another through the damage system (<see cref="DamageSystem.Shoot"/>), at the belt, the upper works,
/// the thin ends, the deck, a turret, so each outcome's look comes round.</summary>
public sealed partial class MapScene
{
    readonly HitBook hitBook = new();

    /// <summary>The dev's follow: the camera kept on this ship (an entity; 0 none) until a pan, as a middle click on her.</summary>
    public int Follow { get => follow; init => follow = value; }
    readonly HitMarks hitMarks = new();

    /// <summary>The continuous smoke sources: fires (I5), funnels (I6).</summary>
    readonly Fires.SmokeTrails smokeTrails = new();

    /// <summary>Ships going down in the world view (I7).</summary>
    readonly Sinking.SinkingEffects sinking = new();

    /// <summary>The dev's hits bench: "target:shooter[:every_s[:shot]]" (entities; one of the bench's shots over and over),
    /// or null.</summary>
    public string? HitBench { get; init; }

    int benchShot;
    long benchNextMs = long.MinValue, benchSeenMs = long.MinValue;

    /// <summary>The bench's shots: battery, kind, where along her (a share of her half length, + forward), how high over
    /// her waterline (m; NaN: the first main mount's roof), from how far (m) and from what aspect (degrees off her bow).</summary>
    static readonly (int Battery, ShellKind Kind, double X, double Z, double RangeM, double AspectDeg)[] BenchShots =
    [
        (0, ShellKind.Ap, 0.0, 1.5, 8_000, 90),     // the belt
        (0, ShellKind.He, 0.25, 10, 8_000, 90),     // the upper works
        (0, ShellKind.Ap, 0.85, 3, 8_000, 90),      // the thin bow
        (0, ShellKind.Ap, -0.1, 6, 18_000, 60),     // the deck, falling steeply
        (0, ShellKind.Ap, double.NaN, double.NaN, 8_000, 90),   // a turret
        (1, ShellKind.He, -0.35, 4, 5_000, 100),    // the side, a secondary's
        (0, ShellKind.Common, -0.25, 8, 8_000, 120),   // the after upper works
        (0, ShellKind.He, -0.85, 2, 8_000, 90),     // the thin stern
        (0, ShellKind.Ap, 0.3, -4.4, 16_000, 90),   // just under the water
    ];

    /// <summary>The frame's hits: the book from the snapshot, what they show through the models already drawn (after the
    /// magazines', before the lights are set into the frame's).</summary>
    void BuildHits(in MapView view, Snapshot s, double dt)
    {
        shipRenderer.List.Marks ??= hitMarks;
        hitBook.Update(s.Hits, world.DrawMs, s.Paused ? 0 : s.AchievedSpeed, dt, classOf);
        HitEffects.Hand(hitBook, world.DrawMs, s.Paused ? 0 : s.AchievedSpeed, shipRenderer.Splashes.Book, classOf, hitMarks);
        HitEffects.Build(view, hitBook, WorldView.Factor(view), shipRenderer.Magazines, shipRenderer.Flashes.Lights, shipRenderer.Smoke.Book,
            shipRenderer.Flashes.Book.SeaClock);
        RunHitBench(s);
    }

    void RunHitBench(Snapshot s)
    {
        if (HitBench is null || s.Paused)
            return;
        var p = HitBench.Split(':');
        if (p.Length < 2 || !int.TryParse(p[0], out int target) || !int.TryParse(p[1], out int shooter))
            return;
        double every = p.Length > 2 && double.TryParse(p[2], CultureInfo.InvariantCulture, out double e) ? e : 4;
        long now = s.Time.Ms;
        // each new hit's line, to tell the captures apart
        var h = s.Hits;
        for (int i = h.Count - 1; i >= 0; i--)
            if (h.Ms[i] > benchSeenMs)
            {
                benchSeenMs = h.Ms[i];
                Console.WriteLine($"hits bench {new WorldTime(h.Ms[i]).ToUtc():HH:mm:ss.f}: {h.Kind[i]} {h.CalibreMm[i]:0} mm {h.Outcome[i]}, burst {h.Burst[i]} " +
                                  $"after {h.BurstS[i] * 1000:0} ms, W {h.FillerKg[i]:0.0} kg, {h.Shows[i]}");
            }
        if (benchNextMs == long.MinValue)
            benchNextMs = now + 1000;
        if (now < benchNextMs)
            return;
        benchNextMs = now + (long)(every * 1000);
        int k = s.Ships.IndexOf(target);
        if (k < 0 || classOf(s.Ships.Class[k]) is not { } c)
            return;
        int only = p.Length > 3 && int.TryParse(p[3], out int o) ? o : -1;
        var shot = BenchShots[only >= 0 ? only % BenchShots.Length : benchShot++ % BenchShots.Length];
        double x = shot.X * c.Length / 2, z = shot.Z;
        if (double.IsNaN(x) && c.Mounts.Length > 0)
        {
            var m = c.Mounts[0];
            (x, z) = (m.X, c.Components[m.Component].Bounds.Top - c.Vertical.Waterline - 1);
        }
        world.Send(DamageSystem.Shoot(target, shooter, shot.Battery, shot.Kind, x, z, shot.RangeM, shot.AspectDeg));
    }
}
