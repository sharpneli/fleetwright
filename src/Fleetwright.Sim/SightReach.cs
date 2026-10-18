using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>How far the ships at sea can see each other (docs/battle/fire-control.md F4d): the tallest eye's horizon and
/// the tallest silhouette's together, among the classes that have come into the world, so nothing beyond it shows over
/// the horizon to anyone. It only grows (a class that has been at sea stays counted). The looks reach this far, and the
/// encounters hold opposing ships within it on the fine tick (never under
/// <see cref="SimSettings.EncounterRangeMinM"/>).</summary>
public sealed class SightReach(SimSettings settings)
{
    double eyeM, topM;

    /// <summary>The farthest any eye at sea can see anything of any ship at sea, m (0 before any class).</summary>
    public double SightM { get; private set; }

    /// <summary>Opposing ships within this of each other are in an encounter, on the fine tick, m.</summary>
    public double EncounterM => Math.Max(SightM, settings.EncounterRangeMinM);

    /// <summary>A class has come into the world: whether the reach grew.</summary>
    internal bool Add(ShipClass c)
    {
        double eye = c.Eyes.Length > 0 ? c.Eyes[0].HeightM : 0, top = c.Silhouette.TopM;
        if (eye <= eyeM && top <= topM)
            return false;
        (eyeM, topM) = (Math.Max(eyeM, eye), Math.Max(topM, top));
        double before = EncounterM;
        SightM = SightModel.ReachM(eyeM, topM, settings);
        return EncounterM != before;
    }
}
