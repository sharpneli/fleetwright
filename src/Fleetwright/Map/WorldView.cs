namespace Fleetwright.Map;

/// <summary>The one boundary between the planning table and the world (docs/visuals/sea.md W0). Zoomed out, the map is
/// a chart: icons, the grid, symbols. Zoomed in, it is the world seen from above: ships as themselves at their true
/// size, the sea as water. Everything that changes between the two reads <see cref="Factor(in MapView)"/>, so all of
/// it crosses at the same zoom and over the same cross-fade. The battle's stylised marks (shells, splashes, fires,
/// contacts, orders) stay in both.</summary>
public static class WorldView
{
    /// <summary>The boundary: metres per reference pixel (a window <see cref="ReferenceHeightPx"/> tall) at and under
    /// which the map is wholly the world. The one number to iterate on (<c>-worldview=M</c>, or live in the "where"
    /// panel with F2's stats shown).</summary>
    public static double BoundaryM = 4;

    /// <summary>The cross-fade's far end over the boundary, as a ratio: at and past it the map is wholly the table.</summary>
    public const double FadeRatio = 1.5;

    /// <summary>The window height the boundary is measured against, the UI scale's own (1080 px = 1.0): so the boundary
    /// is the same stretch of sea on any screen.</summary>
    public const double ReferenceHeightPx = 1080;

    /// <summary>How much of the world the map shows: 1 the world, 0 the table, between them the cross-fade.</summary>
    public static double Factor(in MapView v) => Factor(v.MetresPerPixel, v.Height);

    public static double Factor(double metresPerPixel, double heightPx)
    {
        double m = metresPerPixel * heightPx / ReferenceHeightPx, b = BoundaryM;
        double t = Math.Clamp((m - b) / (b * FadeRatio - b), 0, 1);
        return 1 - t * t * (3 - 2 * t);
    }

    /// <summary>The factor as the HUD names it.</summary>
    public static string Label(double factor) => factor >= 1 ? "world" : factor <= 0 ? "table" : $"table to world {factor:0.00}";
}
