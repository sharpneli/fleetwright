using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a ship can still do (docs/battle/command.md "Fitness"), from its condition: the speed it can make as a
/// share of its designed speed, its main guns working as a share of them (by barrels), the water aboard as a share of its
/// reserve buoyancy, its list, and its steering. Its own captain reads it exactly (own ships are known); doctrine's
/// fall-out thresholds compare against it. Pure, and it allocates nothing.</summary>
public readonly record struct Fitness(double SpeedShare, double MainGunsShare, double WaterShare, double ListDeg, Steering Steering)
{
    public static readonly Fitness Whole = new(1, 1, 0, 0, Steering.Free);

    /// <param name="reserveT">The class's reserve buoyancy, tonnes (its flooding model's).</param>
    public static Fitness Of(ShipClass c, ShipCondition k, double reserveT)
    {
        if (!k.Damaged)
            return Whole;
        double top = c.Motion.MaxSpeedKn;
        double speed = top > 0 ? c.SpeedAt(c.PowerAt(top) * k.PowerCap) / top : 1;
        int barrels = 0, working = 0;
        foreach (ref readonly var b in c.Batteries)
        {
            if (b.Kind != Battery.Main)
                continue;
            foreach (int m in c.Links(b.Mounts))
            {
                int n = c.TurretTypes[c.Mounts[m].Type].Barrels;
                barrels += n;
                if (k.MountWorks[m])
                    working += n;
            }
        }
        return new Fitness(Math.Clamp(speed, 0, 1), barrels > 0 ? (double)working / barrels : 1, reserveT > 0 ? k.WaterT / reserveT : 0,
            Math.Abs(k.Attitude.HeelDeg), k.Steering);
    }
}
