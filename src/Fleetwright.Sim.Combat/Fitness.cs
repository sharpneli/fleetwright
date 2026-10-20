using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a ship can still do (docs/battle/command.md "Fitness"), from its condition: the speed it can make as a
/// share of its designed speed, its chief guns (<see cref="BatteryRow.Chief"/>) working as a share of them (by barrels),
/// the water aboard as a share of its reserve buoyancy, its list, and its steering. Its own captain reads it exactly (own ships are known); doctrine's
/// fall-out thresholds compare against it. The speed and guns are the lasting state's (<see cref="ShipCondition.MountLasts"/>,
/// <see cref="ShipCondition.PowerCapLasts"/>): a fault he expects back soon (a blackout, a tripped breaker, a stunned
/// crew) doesn't take her out of the line (the user, 2026-10-20). Pure, and it allocates nothing.</summary>
public readonly record struct Fitness(double SpeedShare, double MainGunsShare, double WaterShare, double ListDeg, Steering Steering)
{
    public static readonly Fitness Whole = new(1, 1, 0, 0, Steering.Free);

    /// <param name="reserveT">The class's reserve buoyancy, tonnes (its flooding model's).</param>
    public static Fitness Of(ShipClass c, ShipCondition k, double reserveT)
    {
        if (!k.Damaged)
            return Whole;
        double top = c.Motion.MaxSpeedKn;
        double speed = top > 0 ? c.SpeedAt(c.PowerAt(top) * k.PowerCapLasts) / top : 1;
        int barrels = 0, working = 0;
        foreach (ref readonly var b in c.Batteries)
        {
            if (!b.Chief)
                continue;
            foreach (int m in c.Links(b.Mounts))
            {
                int n = c.TurretTypes[c.Mounts[m].Type].Barrels;
                barrels += n;
                if (k.MountLasts[m])
                    working += n;
            }
        }
        return new Fitness(Math.Clamp(speed, 0, 1), barrels > 0 ? (double)working / barrels : 1, reserveT > 0 ? k.WaterT / reserveT : 0,
            Math.Abs(k.Attitude.HeelDeg), k.Steering);
    }
}

/// <summary>Why a ship falls out of the line (docs/battle/command.md 5.1a), or None.</summary>
public enum FallOutCause : byte
{
    None,
    Steering,
    Speed,
    Guns,
    List,
    Water,
}

/// <summary>Doctrine's fall-out (docs/battle/command.md 5.1a): under a share of her designed speed, of her main guns, past
/// a list, or with a share of her reserve flooded; and her steering gone, where she falls out at all (a speed share over
/// 0). Dead in the water she's out whatever the doctrine: a ship that can't steam can't keep a line, nor lead one. The one
/// test: the captain's, and the matchup's toughness (the hits that put a ship out of the fight, 5.1g).</summary>
public readonly record struct FallOutRule(double SpeedShare, double GunsShare, double ListDeg, double WaterShare)
{
    public FallOutCause Of(in Fitness f) =>
        SpeedShare > 0 && f.Steering is Steering.Jammed or Steering.Lost or Steering.Engines ? FallOutCause.Steering
        : f.SpeedShare <= 0 || f.SpeedShare < SpeedShare ? FallOutCause.Speed
        : f.MainGunsShare < GunsShare ? FallOutCause.Guns
        : f.ListDeg > ListDeg ? FallOutCause.List
        : f.WaterShare > WaterShare ? FallOutCause.Water
        : FallOutCause.None;
}
