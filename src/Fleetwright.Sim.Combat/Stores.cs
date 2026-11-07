using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>High-explosive stores aboard (docs/battle/explosions.md EX6c; research 15): a launcher's warheads, in its tubes and
/// its spares beside them (<see cref="LauncherRow.StoreBox"/>). What sets them off follows from where they are and what reaches
/// them, never from what the explosion was:
/// <list type="bullet">
/// <item><b>contact:</b> a detonation within <see cref="SimSettings.ContactReachK"/>·W^⅓ of them (15 §1.2: a blast's pressure is a
/// tenth to a quarter of what a filler needs even there, so only a donor's case at a few tens of cm carries it);</item>
/// <item><b>fragments:</b> a burst's pieces that keep Held's v²d over their filler's constant through what's between and their own
/// case (<see cref="FragmentBurst.Hits"/> with held_k; 15 §2.1): a shell's never set off a TNT warhead, a neighbour's do;</item>
/// <item><b>fire:</b> their cases heated to their filler's cook-off temperature (<see cref="Cook"/>; 15 §3.2).</item>
/// </list>
/// A filler that nothing from outside sets off (held_k 0: wet guncotton) takes none of them. What's set off is marked
/// (<see cref="ShipCondition.StoreLit"/>) for its own explosion, which the damage system runs after what set it off
/// (<see cref="Effects.Detonate"/>). Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class Stores
{
    /// <summary>Fragments carry no further than this, m: past it a store's chance is nothing a draw would see.</summary>
    const double FarM = 60;

    /// <summary>A burst inside her or on her (a shell's, a store's own): every store it touches or its pieces set off. at: the
    /// burst; dir and v its pieces' axis and the speed it flew at; share the pieces' multiple of frag's (a store's warheads, a
    /// broken shell's share); w its TNT kg; cell and part where it is (-1 none), around the part's plates its pieces leave by;
    /// self a store not to count (the one going off). How many it set off.</summary>
    public static int Burst(ShipClass c, ShipCondition k, P3 at, P3 dir, double v, FragmentBurst? frag, double share, double w, int cell, int part,
        double around, int self, SimSettings s, ref RandomStream r)
    {
        int lit = 0;
        var launchers = c.Launchers;
        for (int l = 0; l < launchers.Length; l++)
        {
            ref readonly var row = ref launchers[l];
            if (l == self || !Live(c, k, l))
                continue;
            double d = Effects.BoxDistance(row.StoreBox, at);
            if (d > FarM)
                continue;
            if (!Between(c, row, cell, part, at, out double mild))
                continue;
            lit += Try(c, k, l, at, dir, v, frag, share, w, d, around + mild, s, ref r);
        }
        return lit;
    }

    /// <summary>A burst against her from outside in the water (a torpedo's): the stores in the spaces its near field walked into
    /// (or on the deck over one), its own case's pieces through her plating and that deck, and contact.</summary>
    public static int Outside(ShipClass c, ShipCondition k, GasNetwork net, P3 at, P3 dir, FragmentBurst? frag, double w, SimSettings s, ref RandomStream r)
    {
        int lit = 0;
        var launchers = c.Launchers;
        for (int l = 0; l < launchers.Length; l++)
        {
            ref readonly var row = ref launchers[l];
            if (!Live(c, k, l))
                continue;
            double mild = c.Plating.ShellMm;
            if (row.StoreCell >= 0 ? !net.NearReached(row.StoreCell) : row.StoreDeck < 0 || !net.NearReached(row.StoreDeck))
                continue;
            if (row.StoreCell < 0)
                mild += c.Plating.StrengthDeckMm;
            double d = Effects.BoxDistance(row.StoreBox, at);
            if (d <= FarM)
                lit += Try(c, k, l, at, dir, 0, frag, 1, w, d, mild, s, ref r);
        }
        return lit;
    }

    /// <summary>Whether a launcher's warheads are aboard, not yet set off, and of a filler something from outside can set off.</summary>
    internal static bool Live(ShipClass c, ShipCondition k, int l) =>
        k.Warheads[l] > 0 && !k.StoreLit[l] && c.Torpedoes[c.Launchers[l].Torpedo].HeldK > 0;

    /// <summary>One store against one burst: contact, else its pieces through mildMm and its case by Held (and dragM of the air's
    /// worth of a medium on the way: the sea's, for a near miss). Marks it set off.</summary>
    internal static int Try(ShipClass c, ShipCondition k, int l, P3 at, P3 dir, double v, FragmentBurst? frag, double share, double w, double d, double mildMm,
        SimSettings s, ref RandomStream r, double dragM = 0)
    {
        ref readonly var row = ref c.Launchers[l];
        ref readonly var tp = ref c.Torpedoes[row.Torpedo];
        bool lit = d <= s.ContactReachK * Math.Cbrt(Math.Max(w, 0));
        if (!lit && frag is not null && share > 0)
        {
            double n = share * frag.Hits(at, dir, v, row.StoreBox, mildMm + tp.CaseMm, default, dragM, tp.HeldK);
            lit = n > 1e-6 && r.NextDouble() < 1 - Math.Exp(-n);
        }
        if (!lit)
            return 0;
        k.StoreLit[l] = true;
        return 1;
    }


    /// <summary>What stands between a burst and a store, as mild steel against its pieces; false when more than a wall or a deck does
    /// (they don't get there). In her spaces: nothing in its own, the bulkhead or deck to the next. On deck: the part it burst in
    /// (its plates are its own <c>around</c>), a burst outside in the open nothing, one in the space under the deck that deck.</summary>
    static bool Between(ShipClass c, in LauncherRow row, int cell, int part, P3 at, out double mild)
    {
        mild = 0;
        if (row.StoreCell >= 0)
        {
            if (cell == row.StoreCell)
                return true;
            if (cell < 0)
                return false;
            foreach (var n in c.Neighbours(c.Cells[cell].Neighbours))
                if (n.Cell == row.StoreCell)
                {
                    mild = Effects.JoinMild(c, n, c.Cells[cell].Box, c.Cells[row.StoreCell].Box, at);
                    return true;
                }
            return false;
        }
        if (cell < 0)
            return true;   // both above her hull: in the open, or the part it burst in
        if (cell == row.StoreDeck)
        {
            mild = c.Plating.StrengthDeckMm;
            return true;
        }
        return false;
    }

    /// <summary>The stores' heat in her fires (15 §3.2, ref H2): each in a burning space heats toward its filler's cook-off
    /// temperature by the fire's class: an engulfing fire's (charges burning there), a compartment's (developed, decaying), or hot
    /// smoke (growing, local, starved, in the open), <see cref="SimSettings.CookOffMin"/> for a 5 mm TNT case and by its own wall
    /// and filler as a lumped wall heats; water on that fire, or the space flooded, and it cools. At its drawn limit it goes: an
    /// oxygen flask's high order, an air flask's high order <see cref="SimSettings.CookOffHighShare"/> of the time, else it burns
    /// (gone, no explosion). How many it set off, and how many burned (their launchers' warheads are none).</summary>
    public static (int Lit, int Burned) Cook(ShipClass c, FloodModel f, ShipCondition k, double dt, SimSettings s, ref RandomStream r)
    {
        int lit = 0, burned = 0;
        var launchers = c.Launchers;
        for (int l = 0; l < launchers.Length; l++)
        {
            ref readonly var row = ref launchers[l];
            ref readonly var tp = ref c.Torpedoes[row.Torpedo];
            if (k.Warheads[l] <= 0 || k.StoreLit[l] || tp.CookK <= 0)
                continue;
            int fs = row.StoreFire;
            bool wet = row.StoreCell >= 0 && f.Capacity[row.StoreCell] > 0 && k.WaterM3[row.StoreCell] >= s.MagazineFullShare * f.Capacity[row.StoreCell];
            if (fs < 0 || wet || !k.Fire[fs].Burning || k.FireFlow[fs].Lpm > 0)
            {
                k.StoreHeat[l] = Math.Max(0, k.StoreHeat[l] - dt / s.CookOffCoolS);
                continue;
            }
            ref readonly var x = ref k.Fire[fs];
            int kind = x.Charges > 0 ? 0 : !c.FireSpaces[fs].OpenAir && x.Phase is BurnPhase.Developed or BurnPhase.Decay ? 1 : 2;
            double t = CookS(tp, kind, s);
            if (!double.IsFinite(t))
                continue;
            if (k.StoreLimit[l] <= 0)
                k.StoreLimit[l] = Math.Exp(s.CookOffSigma * r.NextGaussian());
            k.StoreHeat[l] += dt / t;
            if (k.StoreHeat[l] < k.StoreLimit[l])
                continue;
            if (tp.Oxygen || r.NextDouble() < s.CookOffHighShare)
            {
                k.StoreLit[l] = true;
                lit++;
            }
            else
            {
                k.Warheads[l] = 0;
                k.StoreHeat[l] = 0;
                burned++;
            }
        }
        return (lit, burned);
    }

    /// <summary>A store's cook-off in a fire of a class, s: <see cref="SimSettings.CookOffMin"/> (a 5 mm case of TNT at 561 K) by its
    /// case's wall and ln((T_f − 300)/(T_f − T_c)) against TNT's, as a lumped wall heats; never in a fire no hotter than its filler's
    /// temperature.</summary>
    public static double CookS(in TorpedoRow tp, int kind, SimSettings s)
    {
        double tf = s.CookOffFireK[kind];
        if (tf <= tp.CookK)
            return double.PositiveInfinity;
        double own = Math.Log((tf - 300) / (tf - tp.CookK)), tnt = Math.Log((tf - 300) / (tf - 561));
        return s.CookOffMin[kind] * 60 * tp.CaseMm / 5 * own / tnt;
    }
}
