namespace Fleetwright.Shipgen;

/// <summary>A ship's size between a destroyer's and a large ship's, for the things that grow with her and come as two
/// rows of data (docs/battle/shell-contents.md C1b: what stands in her spaces; C4c: her shafts' turning speed). No switch
/// between them: ln(Δ / small) / ln(large / small), held to 0..1.</summary>
public static class ShipSize
{
    /// <summary>The displacements (t) of the small and the large: a 1915 destroyer's and a dreadnought's.</summary>
    public const double SmallT = 1_000, LargeT = 20_000;

    /// <summary>A ship's place between the small (0) and the large (1); 1 when the displacement isn't known.</summary>
    public static double Of(double displacementT) =>
        displacementT > 0 ? Math.Clamp(Math.Log(displacementT / SmallT) / Math.Log(LargeT / SmallT), 0, 1) : 1;
}
