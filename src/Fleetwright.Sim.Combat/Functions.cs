using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a ship can still do (docs/battle/fighting.md 4.3a, FUNCTIONS; research/simulation/damage-research/
/// 06-service-networks.md §4, Model A): one generic pass over the class's function table (<see cref="ShipClass.Functions"/>),
/// each function worth the least of its needs' health times the best of its alternatives'. From them: which mounts
/// fire (and aren't jammed, nor all their guns out), which submerged tubes, directors and eyes work, the power the
/// plant can make (its boiler rooms' steam and its shaft lines, the less of the two), whether the rudders answer,
/// whether the ship is conned. A node down for a while (E4) gives nothing until it's back. Beside them, the mounts and
/// the power as they'll be once the outages ending by a time are over: the lasting state a captain falls out by
/// (<see cref="Fitness"/>). Run when the condition changes, not per tick.</summary>
public static class Functions
{
    /// <summary>A function's worth, 0-1: the least health of All, times the best of Any (if it has any), that factor
    /// no less than <paramref name="anyFloor"/> (what's left without any of them: a boiler room's draught with its
    /// funnels wrecked); with the outages ending by <paramref name="byMs"/> (world ms) over.</summary>
    public static double Value(ShipClass c, ShipCondition k, in FunctionRow f, double anyFloor = 0, long byMs = long.MinValue)
    {
        double v = 1;
        foreach (int n in c.FunctionNodes(f.All))
            v = Math.Min(v, k.HealthBy(n, byMs));
        if (f.Any.Count > 0)
        {
            double best = 0;
            foreach (int n in c.FunctionNodes(f.Any))
                best = Math.Max(best, k.HealthBy(n, byMs));
            v *= anyFloor + (1 - anyFloor) * best;
        }
        return v;
    }

    /// <summary>Works out the condition's functions; whether its rudders still answer (none fitted: they do).
    /// draughtFloor: the share of its steam a boiler room keeps with all its funnels wrecked; lastingMs: the lasting
    /// state's time (world ms), the outages ending by then over.</summary>
    public static bool Evaluate(ShipCondition k, double draughtFloor, long lastingMs)
    {
        var c = k.Class;
        double steam = 0, shafts = 0, steamBy = 0, shaftsBy = 0;
        int boilers = 0, lines = 0, rudders = 0;
        bool steer = false, conned = true;
        foreach (ref readonly var f in c.Functions)
        {
            double floor = f.Kind == FunctionKind.Boilers ? draughtFloor : 0, v = Value(c, k, f, floor);
            switch (f.Kind)
            {
                case FunctionKind.Mount:
                    k.MountWorks[f.Index] = v > 0 && !k.Has(c.Mounts[f.Index].Component, PartFlags.Jammed) && k.Guns(f.Index) > 0;
                    k.MountLasts[f.Index] = k.WorksBy(f.Index, Value(c, k, f, floor, lastingMs), lastingMs);
                    break;
                case FunctionKind.Director:
                    k.DirectorWorks[f.Index] = v > 0 && k.DirectorDown[f.Index] == 0;
                    break;
                case FunctionKind.Eye:
                    k.EyeWorks[f.Index] = v > 0 && k.EyeDown[f.Index] == 0;
                    break;
                case FunctionKind.ShaftLine:
                    shafts += v;
                    shaftsBy += Value(c, k, f, floor, lastingMs);
                    lines++;
                    break;
                case FunctionKind.Boilers:
                    steam += v;
                    steamBy += Value(c, k, f, floor, lastingMs);
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
        k.PowerCapLasts = Math.Min(boilers > 0 ? steamBy / boilers : 1, lines > 0 ? shaftsBy / lines : 1);
        k.LastingMs = lastingMs;
        k.Conned = conned;
        return steer || rudders == 0;
    }
}
