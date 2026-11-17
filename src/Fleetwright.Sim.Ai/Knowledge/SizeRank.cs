using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Knowledge;

/// <summary>A contact's size as the matchup takes her, for the choices that were her type's (docs/battle/allocation.md
/// AL7c): her tonnage (<see cref="Matchups.TonnageOf"/>; before she's made out to her type, the mean of what her side has
/// that the navy hasn't accounted for), and that as a rank, 0 a small craft to 4 a capital ship: the old types' ranks made
/// smooth, each at the middle of its type's band of the world's yardstick (<see cref="ShipTypes"/>: a small craft's band
/// taken as wide as a destroyer's, a capital ship's middle the yardstick itself) and between them by the log of her tonnage.
/// No line: a ship a little over a cruiser's band ranks a little over a cruiser.</summary>
public static class SizeRank
{
    /// <summary>The rank an unarmed ship (a merchant, made out so) takes where the smallest first are wanted: last.</summary>
    public const double Unarmed = 5;

    /// <summary>The largest rank a warship has: a capital ship's.</summary>
    public const double Capital = 4;

    /// <summary>A tonnage's rank against the yardstick (t; none yet: a capital ship's).</summary>
    public static double Of(double t, double referenceT, SimSettings s)
    {
        if (referenceT <= 0 || t <= 0)
            return t > 0 ? Capital : 0;
        double d = Math.Log(s.TypeDestroyerShare), c = Math.Log(s.TypeCruiserShare), k = Math.Log(s.TypeCapitalShare);
        double small = d - 0.5 * (c - d), destroyer = 0.5 * (d + c), cruiser = 0.5 * (c + k), capital = Math.Max(0, cruiser + 1e-9);
        double x = Math.Log(t / referenceT);
        return x <= small ? 0
            : x <= destroyer ? (x - small) / (destroyer - small)
            : x <= cruiser ? 1 + 2 * (x - destroyer) / (cruiser - destroyer)
            : x <= capital ? 3 + (x - cruiser) / (capital - cruiser)
            : Capital;
    }

    /// <summary>A contact's rank as a navy (<paramref name="viewer"/>) takes her; <see cref="Unarmed"/> if she's made out
    /// to carry no guns.</summary>
    public static double Of(World world, Matchups m, byte viewer, in Contact c)
    {
        if (c.Level >= ContactLevel.Type && !c.Armed)
            return Unarmed;
        return Of(TonnageOf(world, m, viewer, c), world.Types.ReferenceT, world.Settings);
    }

    /// <summary>A contact's tonnage as a navy takes her (<see cref="Matchups.TonnageOf"/>), and before she's made out to
    /// her type, the mean of the ships of the others' it hasn't accounted for (as <see cref="MatchupSide.AddContact"/>
    /// takes her).</summary>
    public static double TonnageOf(World world, Matchups m, byte viewer, in Contact c)
    {
        if (c.Level >= ContactLevel.Type)
            return m.TonnageOf(world, viewer, c);
        bool exactly = false;
        for (int side = 0; side < 256 && !exactly; side++)
            exactly = side != viewer && m.Census(world, (byte)side).Total > 0 && m.Unmade(world, viewer, (byte)side, true).Total > 1e-9;
        double t = 0, n = 0;
        for (int side = 0; side < 256; side++)
            if (side != viewer && m.Census(world, (byte)side).Total > 0 && m.Unmade(world, viewer, (byte)side, exactly) is ({ } some, > 1e-9))
                foreach (var (p, k) in some)
                    (t, n) = (t + k * p.DisplacementT, n + k);
        return n > 0 ? t / n : 0;
    }
}
