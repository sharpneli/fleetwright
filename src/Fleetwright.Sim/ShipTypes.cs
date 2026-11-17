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
    double sumT, sumT2;   // the warships placed: Σ t, Σ t² (full displacement)
    readonly List<(ShipClass Class, int Ships)> placed = [];

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

    /// <summary>What a class shows side on above the water, m² (its silhouette's: what a lookout judges her size by).</summary>
    public static double SideAreaM2(ShipClass c) => Sight.AreaAbove(c.Numbers(c.Silhouette.SideAreaM2), 0);

    /// <summary>The length and draught of a ship estimated at a size (docs/battle/allocation.md AL7b: a contact made out as a
    /// type and no more, sized for a torpedo's aim and depth): the ships placed, armed or not as she is, each by how near
    /// what it shows side on is to the estimate (a normal in its log, of <paramref name="sigma"/>); every ship placed if
    /// none of her kind is, and 120 m by 4 m in a world with no ships.</summary>
    public (double LengthM, double DraughtM) At(double sizeM2, double sigma, bool armed)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            double w = 0, l = 0, d = 0;
            foreach (var (cls, ships) in placed)
            {
                if (pass == 0 && IsMerchant(cls) == armed)
                    continue;
                double area = SideAreaM2(cls), x = sizeM2 > 0 && area > 0 ? Math.Log(area / sizeM2) / sigma : 0, k = ships * Math.Exp(-0.5 * x * x);
                (w, l, d) = (w + k, l + k * cls.Length, d + k * cls.Vertical.Draught);
            }
            if (w > 1e-300)
                return (l / w, d / w);
        }
        return (120, 4);
    }

    /// <summary>The warships placed's mean length and draught, m (a contact not yet made out to her type, sized for a
    /// torpedo's aim and depth); 120 m by 4 m in a world with none.</summary>
    public (double LengthM, double DraughtM) Mean()
    {
        double n = 0, l = 0, d = 0;
        foreach (var (cls, ships) in placed)
            if (!IsMerchant(cls))
                (n, l, d) = (n + ships, l + cls.Length * ships, d + cls.Vertical.Draught * ships);
        return n > 0 ? (l / n, d / n) : (120, 4);
    }

    /// <summary>A ship placed (<see cref="ShipSystem"/>'s spawn): the yardstick worked out again.</summary>
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
    }
}
