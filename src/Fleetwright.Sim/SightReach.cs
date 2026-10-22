using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>How far the ships at sea can see each other (docs/battle/fire-control.md F4d): the tallest eye's horizon and
/// the tallest silhouette's together, among the classes that have come into the world, so nothing beyond it shows over
/// the horizon to anyone; with the smoke field on, the tallest funnel smoke's instead where it's higher
/// (<see cref="SmokeSources.TopM"/>, docs/battle/smoke.md Z6: smoke on the horizon). It only grows (a class that has been
/// at sea stays counted). The looks reach this far, and the encounters hold opposing ships within it on the fine tick
/// (never under <see cref="SimSettings.EncounterRangeMinM"/>).</summary>
public sealed class SightReach(SimSettings settings)
{
    double eyeM, topM;

    /// <summary>The tallest funnel smoke among the classes, m (0 with the field off).</summary>
    public double SmokeTopM { get; private set; }

    /// <summary>The farthest any eye at sea can see anything of any ship at sea, m (0 before any class).</summary>
    public double SightM { get; private set; }

    /// <summary>Opposing ships within this of each other are in an encounter, on the fine tick, m.</summary>
    public double EncounterM => Math.Max(SightM, settings.EncounterRangeMinM);

    /// <summary>A class has come into the world: whether the reach grew.</summary>
    internal bool Add(ShipClass c)
    {
        double eye = c.Eyes.Length > 0 ? c.Eyes[0].HeightM : 0, top = c.Silhouette.TopM;
        double smoke = settings.Smoke ? SmokeSources.TopM(c, settings.DefaultWindMs) : 0;
        if (eye <= eyeM && top <= topM && smoke <= SmokeTopM)
            return false;
        (eyeM, topM, SmokeTopM) = (Math.Max(eyeM, eye), Math.Max(topM, top), Math.Max(SmokeTopM, smoke));
        double before = EncounterM;
        SightM = SightModel.ReachM(eyeM, Math.Max(topM, SmokeTopM), settings);
        return EncounterM != before;
    }
}
