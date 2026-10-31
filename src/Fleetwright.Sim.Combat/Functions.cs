using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a ship can still do (docs/battle/fighting.md 4.3a, FUNCTIONS; research/simulation/damage-research/
/// 06-service-networks.md §4, Model A): one generic pass over the class's function table (<see cref="ShipClass.Functions"/>),
/// each function worth the least of its needs' health times the best of its alternatives'. From them: which mounts
/// fire (and aren't jammed, nor all their guns out), which submerged tubes, directors and eyes work, the power the
/// plant can make (its boiler rooms' steam and its shaft lines, the less of the two), whether the rudders answer,
/// whether the ship is conned; each station by the men at work there (docs/battle/crew.md N2: <see cref="BattleStations"/>:
/// shares: a mount's crew stretches its reload and its handling men its supply, silent below the few it's fought with;
/// a director, an eye or a searchlight with nobody is out, short-handed it reads slower). A node down for a while (E4) gives nothing
/// until it's back. Beside them, the mounts and
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

    /// <summary>Where she's conned from (docs/battle/crew.md N2): forward while a bridge or the conning tower stands with
    /// her command party there; when that goes, aft control if it's manned, once the control has shifted there
    /// (<see cref="SimSettings.ConnShiftS"/>; not conned meanwhile); else nowhere.</summary>
    static void Conn(ShipCondition k, bool command, long now, SimSettings s)
    {
        var (fore, aft) = BattleStations.Conn(k.Class, k);
        if (command && fore)
            (k.ConnAt, k.ConnShiftMs) = (0, 0);
        else if (!aft)
            (k.ConnAt, k.ConnShiftMs) = (-1, 0);
        else if (k.ConnAt != 1)
        {
            if (k.ConnShiftMs == 0)
            {
                k.ConnAt = -1;
                k.ShiftConn(now + (long)(s.ConnShiftS * 1000));
            }
            else if (now >= k.ConnShiftMs)
                (k.ConnAt, k.ConnShiftMs) = (1, 0);
        }
        k.Conned = k.ConnAt >= 0;
    }

    /// <summary>Works out the condition's functions now (world ms); whether its rudders still answer (none fitted: they
    /// do). The settings' DraughtFloor: the share of its steam a boiler room keeps with all its funnels wrecked;
    /// lastingMs: the lasting state's time (world ms), the outages ending by then over (the men as they are now).</summary>
    public static bool Evaluate(ShipCondition k, SimSettings s, long now, long lastingMs)
    {
        var c = k.Class;
        Steadiness.Update(k, s, now);   // who stays at the stations (docs/battle/crew.md N3b), before what they can do
        double draughtFloor = s.DraughtFloor;
        double steam = 0, shafts = 0, steamBy = 0, shaftsBy = 0;
        int boilers = 0, lines = 0, rudders = 0;
        bool steer = false, command = true;
        foreach (ref readonly var f in c.Functions)
        {
            double floor = f.Kind == FunctionKind.Boilers ? draughtFloor : 0, v = Value(c, k, f, floor);
            switch (f.Kind)
            {
                case FunctionKind.Mount:
                {
                    var (crew, supply) = BattleStations.Mount(c, k, f.Index, s);
                    (k.MountCrew[f.Index], k.MountSupply[f.Index]) = (crew, supply);
                    bool manned = crew > 0 && supply > 0;
                    k.MountWorks[f.Index] = manned && v > 0 && !k.Has(c.Mounts[f.Index].Component, PartFlags.Jammed) && k.Guns(f.Index) > 0;
                    k.MountSound[f.Index] = k.WorksBy(f.Index, Value(c, k, f, floor, lastingMs), lastingMs);
                    k.MountLasts[f.Index] = manned && k.MountSound[f.Index];
                    break;
                }
                case FunctionKind.Director:
                    k.DirectorWorks[f.Index] = v > 0 && k.DirectorDown[f.Index] == 0 && BattleStations.Share(c, k, Casualties.Part(c.Directors[f.Index].Component)) > 0;
                    break;
                case FunctionKind.Eye:
                    k.EyeCrew[f.Index] = BattleStations.Share(c, k, Casualties.Part(c.Eyes[f.Index].Component));
                    k.EyeWorks[f.Index] = v > 0 && k.EyeDown[f.Index] == 0 && k.EyeCrew[f.Index] > 0;
                    break;
                case FunctionKind.ShaftLine:
                    shafts += v;
                    shaftsBy += Value(c, k, f, floor, lastingMs);
                    lines++;
                    break;
                case FunctionKind.Boilers:
                {
                    // by its stokers (crew.md N2), a drop held while what's in the fires burns; the lasting state at the
                    // stokers' own
                    double men = BattleStations.Stokehold(c, k, f.Index, s.StokeholdFloor);
                    ref double at = ref k.Stoke[f.Index];
                    if (men >= at)
                        (at, k.StokeUntilMs[f.Index]) = (men, 0);
                    else if (k.StokeUntilMs[f.Index] == 0)
                        k.HoldStoke(f.Index, now + (long)(s.StokeholdHoldS * 1000));
                    else if (now >= k.StokeUntilMs[f.Index])
                        (at, k.StokeUntilMs[f.Index]) = (men, 0);
                    steam += v * at;
                    steamBy += Value(c, k, f, floor, lastingMs) * men;
                    boilers++;
                    break;
                }
                case FunctionKind.Rudder:
                    steer |= v > 0;
                    rudders++;
                    break;
                case FunctionKind.Command:
                    command = v > 0;
                    break;
                case FunctionKind.Tube:
                    k.TubeWorks[f.Index] = v > 0;
                    break;
                case FunctionKind.Searchlight:
                    k.SearchlightWorks[f.Index] = v > 0 && k.SearchlightDown[f.Index] == 0 &&
                                                  BattleStations.Share(c, k, Casualties.Part(c.Searchlights[f.Index].Component)) > 0;
                    break;
            }
        }
        k.PowerCap = Math.Min(boilers > 0 ? steam / boilers : 1, lines > 0 ? shafts / lines : 1);
        k.PowerCapLasts = Math.Min(boilers > 0 ? steamBy / boilers : 1, lines > 0 ? shaftsBy / lines : 1);
        k.LastingMs = lastingMs;
        Conn(k, command, now, s);
        if (k.IsGivenUp)
            return StoodDown(k, steer || rudders == 0);
        return steer || rudders == 0;
    }

    /// <summary>A ship given up (docs/battle/striking.md SK1): her colours down, her guns, tubes and searchlights stood
    /// down (her men stay at the pumps and the conn: Sissoi, Spartak); her men ordered off, nothing worked at all (no
    /// guns, no plant, no conn, no lookouts), so she floods as her holes let her.</summary>
    static bool StoodDown(ShipCondition k, bool steers)
    {
        Array.Fill(k.MountWorks, false);
        Array.Fill(k.MountLasts, false);
        Array.Fill(k.TubeWorks, false);
        Array.Fill(k.SearchlightWorks, false);
        if (!k.Abandoned)
            return steers;
        Array.Fill(k.DirectorWorks, false);
        Array.Fill(k.EyeWorks, false);
        (k.PowerCap, k.PowerCapLasts, k.Conned, k.ConnAt) = (0, 0, false, -1);
        return false;
    }
}
