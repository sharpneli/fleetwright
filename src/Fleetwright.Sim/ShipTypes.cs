using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>What a class is reported as by type (<see cref="ShipType"/>), measured against the ships in this world rather
/// than any era's tonnage: what made a capital ship moved from 7,000 t ironclads to 40,000 t battleships, and a ship is
/// one by her place among the ships she fights beside and against. The yardstick is the warships' tonnage-weighted mean
/// displacement, Σt² / Σt over every ship placed (the size of ship the average ton at sea sails in: a crowd of boats moves
/// it little, a lone outsize ship no further than her share of the tonnage); a class is a capital ship from
/// <see cref="SimSettings.TypeCapitalShare"/> of it, a cruiser from <see cref="SimSettings.TypeCruiserShare"/>, a
/// destroyer from <see cref="SimSettings.TypeDestroyerShare"/>, else a small craft. A class with no main mounts is a
/// merchant whatever her size.
///
/// <para>Every ship placed counts, sunk or not (the scenario's ships, and a dev spawn's), so a battle's losses never
/// make a cruiser of a battleship or a battleship of a cruiser. Updated at a spawn, on the sim thread; read anywhere
/// after (the AI's workers too).</para></summary>
public sealed class ShipTypes(SimSettings settings)
{
    const int Types = (int)ShipType.Merchant + 1;

    double sumT, sumT2;   // the warships placed: Σ t, Σ t² (full displacement)
    readonly List<(ShipClass Class, int Ships)> placed = [];
    readonly double[] lengthM = new double[Types], draughtM = new double[Types];

    /// <summary>The yardstick, t: the warships' tonnage-weighted mean full displacement; 0 before any is placed.</summary>
    public double ReferenceT => sumT > 0 ? sumT2 / sumT : 0;

    /// <summary>A class by type, against the ships placed (before any warship is, she is her own yardstick: a capital
    /// ship).</summary>
    public ShipType Of(ShipClass c)
    {
        if (IsMerchant(c))
            return ShipType.Merchant;
        double t = c.Motion.FullDisplacementT, r = sumT > 0 ? sumT2 / sumT : t;
        return t >= settings.TypeCapitalShare * r ? ShipType.Capital
            : t >= settings.TypeCruiserShare * r ? ShipType.Cruiser
            : t >= settings.TypeDestroyerShare * r ? ShipType.Destroyer
            : ShipType.Small;
    }

    /// <summary>Whether a class is a merchant: no main mounts. The one type that doesn't depend on the others.</summary>
    public static bool IsMerchant(ShipClass c) => c.Silhouette.MainMounts == 0;

    /// <summary>The typical length and draught of a type, m: the mean of the ships placed reported as it (a contact made
    /// out as a type and no more, sized for a torpedo's aim and depth); the warships' mean if none is, and 120 m by 4 m in
    /// a world with no ships.</summary>
    public (double LengthM, double DraughtM) Typical(ShipType type) =>
        lengthM[(int)type] > 0 ? (lengthM[(int)type], draughtM[(int)type])
        : lengthM[(int)ShipType.Unknown] > 0 ? (lengthM[(int)ShipType.Unknown], draughtM[(int)ShipType.Unknown])
        : (120, 4);

    /// <summary>A ship placed (<see cref="ShipSystem"/>'s spawn): the yardstick and the types' sizes worked out again.</summary>
    internal void Placed(ShipClass c)
    {
        int k = 0;
        while (k < placed.Count && !ReferenceEquals(placed[k].Class, c))
            k++;
        if (k == placed.Count)
            placed.Add((c, 0));
        placed[k] = (c, placed[k].Ships + 1);
        if (!IsMerchant(c))
        {
            double t = c.Motion.FullDisplacementT;
            sumT += t;
            sumT2 += t * t;
        }
        Span<int> n = stackalloc int[Types];
        Array.Clear(lengthM);
        Array.Clear(draughtM);
        foreach (var (cls, ships) in placed)
        {
            int type = (int)Of(cls);
            lengthM[type] += cls.Length * ships;
            draughtM[type] += cls.Vertical.Draught * ships;
            n[type] += ships;
            if (type != (int)ShipType.Merchant)   // Unknown's slot: every warship's, the fallback
            {
                lengthM[0] += cls.Length * ships;
                draughtM[0] += cls.Vertical.Draught * ships;
                n[0] += ships;
            }
        }
        for (int i = 0; i < Types; i++)
            if (n[i] > 0)
                (lengthM[i], draughtM[i]) = (lengthM[i] / n[i], draughtM[i] / n[i]);
    }
}
