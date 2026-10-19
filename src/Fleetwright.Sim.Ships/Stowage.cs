namespace Fleetwright.Sim.Ships;

/// <summary>What a room stows (docs/battle/shell-contents.md C2-C3): a medium (oil, water, coal) filling it from its floor
/// to a level; at full load the share of it filled (Shipgen's stowage: Powerplant.Bunkers); whether that's fuel (its
/// level falls with the ship's fuel), and whether it's a protective side space (a wing bunker, a TDS layer: kept full
/// longest). Floor and Top are the room's heights over its cells.</summary>
public readonly record struct RoomStowage(MediumKind Kind, double Rho, double FullShare, bool Fuel, bool Protective, double Floor, double Top,
    double VolumeM3, string Name)
{
    public bool Some => Kind != MediumKind.None && FullShare > 0;
}

/// <summary>The rooms' stowage to levels (docs/battle/shell-contents.md C2-C3).</summary>
public static class Stowage
{
    /// <summary>Densities, kg/m³ (damage/09 §3-4): fuel oil 950, coal as bunkered 800, fresh or feed water 1,000,
    /// petrol 720, diesel 850.</summary>
    public const double OilRho = 950, CoalRho = 800, WaterRho = 1000, PetrolRho = 720, DieselRho = 850;

    /// <summary>Each room's level, a height in the ship's frame (NaN: it stows nothing), with the ship's fuel at a share
    /// of a full load: what isn't fuel (feed water) at its full share; the fuel, the share of all of it, in the protective
    /// side spaces first (full before the rest get any) when <paramref name="protectiveLast"/> (the period's practice:
    /// burn the others first, damage/09 §4), else in every fuel space alike.</summary>
    public static void Levels(ShipClass c, double fuelShare, bool protectiveLast, Span<double> level)
    {
        var st = c.Stowages;
        double all = 0, guard = 0, f = Math.Clamp(fuelShare, 0, 1);
        for (int i = 0; i < st.Length; i++)
            if (st[i].Some && st[i].Fuel)
            {
                all += st[i].FullShare * st[i].VolumeM3;
                if (st[i].Protective)
                    guard += st[i].FullShare * st[i].VolumeM3;
            }
        double have = f * all;
        // protective first: the share of its full load each protective space keeps, then the others'
        double guardShare = !protectiveLast ? f : guard > 0 ? Math.Min(have / guard, 1) : 0;
        double rest = !protectiveLast ? f : all - guard > 0 ? Math.Max(have - guard, 0) / (all - guard) : 0;
        for (int i = 0; i < st.Length; i++)
        {
            ref readonly var r = ref st[i];
            double share = !r.Some ? 0 : r.FullShare * (!r.Fuel ? 1 : r.Protective ? guardShare : rest);
            level[i] = share > 0 ? r.Floor + share * (r.Top - r.Floor) : double.NaN;
        }
    }
}
