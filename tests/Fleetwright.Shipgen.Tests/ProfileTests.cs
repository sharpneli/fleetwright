using Fleetwright.Shipgen.Golden;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;

namespace Fleetwright.Shipgen.Tests;

/// <summary>The profile (docs/shipgen/profile-plan.md) over every golden case that builds: the side view's parts stay
/// on the hull's length and within the hitboxes' heights, and both scenes lower to triangles.</summary>
public class ProfileTests
{
    static readonly string Root = Paths.Shipgen();

    [Fact]
    public void EveryShipProfiles()
    {
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        int profiled = 0;
        Parallel.ForEach(GoldenCases.Load(Root), new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, c =>
        {
            Ship ship;
            try
            {
                ship = ShipDesign.Build(Design.Load(c.DesignPath));
            }
            catch (Exception)
            {
                return;   // a case that doesn't build has nothing to draw
            }
            try
            {
                var v = SideView.Of(ship);
                var hb = ship.Hitboxes;
                double top = hb.Components.Select(x => x.Top).DefaultIfEmpty(0).Max();
                var (x0, x1, z0, z1) = v.Bounds();
                if (v.Hull.Count < 4)
                    failures.Add($"{c.Name}: hull outline of {v.Hull.Count} points");
                if (x1 - x0 < hb.Length - 0.5)
                    failures.Add($"{c.Name}: drawn {x1 - x0:F1} m long, the hull {hb.Length:F1} m");
                if (z0 < hb.Vertical.Keel - 3 || z1 > top + 3)
                    failures.Add($"{c.Name}: drawn {z0:F1}..{z1:F1} m, the hitboxes {hb.Vertical.Keel:F1}..{top:F1} m");
                var p = ShipProfile.Build(ship, 4.0);
                if (p.Full.WidthPx < hb.Length * 4 || p.Silhouette.HeightPx >= p.Full.HeightPx)
                    failures.Add($"{c.Name}: canvas {p.Full.WidthPx}x{p.Full.HeightPx}, silhouette {p.Silhouette.HeightPx} px high");
                Lower.Run(p.Full);
                Lower.Run(p.Silhouette);
                Interlocked.Increment(ref profiled);
            }
            catch (Exception e)
            {
                failures.Add($"{c.Name}: {e.GetType().Name}: {e.Message}");
            }
        });
        Assert.True(profiled > 300, $"only {profiled} cases profiled");
        Assert.True(failures.IsEmpty, $"{failures.Count} cases:\n" + string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal).Take(10)));
    }
}
