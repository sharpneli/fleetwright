using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a ship can still do (docs/battle/fighting.md 4.3a, FUNCTIONS; research/simulation/damage-research/
/// 06-service-networks.md §4, Model A): one generic pass over the class's function table (<see cref="ShipClass.Functions"/>),
/// each function worth the least of its needs' health times the best of its alternatives'. From them: which mounts
/// fire (and aren't jammed, nor all their guns out), which submerged tubes, directors and eyes work, the power the
/// plant can make (its boiler rooms' steam and its shaft lines, the less of the two), whether the rudders answer,
/// whether the ship is conned. A node down for a while (E4) gives nothing until it's back. Run when the condition
/// changes, not per tick.</summary>
public static class Functions
{
    /// <summary>A function's worth, 0-1: the least health of All, times the best of Any (if it has any), that factor
    /// no less than <paramref name="anyFloor"/> (what's left without any of them: a boiler room's draught with its
    /// funnels wrecked).</summary>
    public static double Value(ShipClass c, ShipCondition k, in FunctionRow f, double anyFloor = 0)
    {
        double v = 1;
        foreach (int n in c.FunctionNodes(f.All))
            v = Math.Min(v, k.Health(n));
        if (f.Any.Count > 0)
        {
            double best = 0;
            foreach (int n in c.FunctionNodes(f.Any))
                best = Math.Max(best, k.Health(n));
            v *= anyFloor + (1 - anyFloor) * best;
        }
        return v;
    }

    /// <summary>Works out the condition's functions; whether its rudders still answer (none fitted: they do).
    /// draughtFloor: the share of its steam a boiler room keeps with all its funnels wrecked.</summary>
    public static bool Evaluate(ShipCondition k, double draughtFloor)
    {
        var c = k.Class;
        double steam = 0, shafts = 0;
        int boilers = 0, lines = 0, rudders = 0;
        bool steer = false, conned = true;
        foreach (ref readonly var f in c.Functions)
        {
            double v = Value(c, k, f, f.Kind == FunctionKind.Boilers ? draughtFloor : 0);
            switch (f.Kind)
            {
                case FunctionKind.Mount:
                    k.MountWorks[f.Index] = v > 0 && !k.Has(c.Mounts[f.Index].Component, PartFlags.Jammed) && k.Guns(f.Index) > 0;
                    break;
                case FunctionKind.Director:
                    k.DirectorWorks[f.Index] = v > 0 && k.DirectorDown[f.Index] == 0;
                    break;
                case FunctionKind.Eye:
                    k.EyeWorks[f.Index] = v > 0 && k.EyeDown[f.Index] == 0;
                    break;
                case FunctionKind.ShaftLine:
                    shafts += v;
                    lines++;
                    break;
                case FunctionKind.Boilers:
                    steam += v;
                    boilers++;
                    break;
                case FunctionKind.Rudder:
                    steer |= v > 0;
                    rudders++;
                    break;
                case FunctionKind.Command:
                    conned = v > 0;
                    break;
                case FunctionKind.Tube:
                    k.TubeWorks[f.Index] = v > 0;
                    break;
            }
        }
        k.PowerCap = Math.Min(boilers > 0 ? steam / boilers : 1, lines > 0 ? shafts / lines : 1);
        k.Conned = conned;
        return steer || rudders == 0;
    }
}
