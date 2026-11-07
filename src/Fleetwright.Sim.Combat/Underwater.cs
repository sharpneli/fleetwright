using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>An underwater explosion against the hull (docs/battle/fighting.md 4.6: a torpedo's hit; a mine's later), on the one
/// path every explosion takes (docs/battle/explosions.md EX5):
/// <list type="bullet">
/// <item><b>a dud</b> by the torpedo's own chance: nothing but the blow;</item>
/// <item><b>the near field:</b> the charge, as tamped as its depth makes it, at her plate: its near field (<see cref="NearField"/>)
/// holes her side and walks in through what it opens (<see cref="GasNetwork.Strike"/>). Its gas is the bubble in the sea, not in
/// her. A torpedo protection is its own spaces and plates: a holding bulkhead past the reach isn't holed and holds
/// (<see cref="Outcome.Held"/>, read from what opened, not decided before). Her section and her flooding take what it opened, and
/// the girder whether she broke;</item>
/// <item><b>what's in reach:</b> the spaces it got into within half the reach are put out of action, the rest within it damaged, and
/// this share of their battle crew lost; parts within it it got to (shafts, propellers, rudders, tubes); the nearest magazine among
/// them may be lit, as by a burst among its charges; the gas's men and gear as a shell's;</item>
/// <item><b>the shock</b> by its shock factor, as a near miss's (<see cref="NearMiss"/>): seams, machinery, gear, the plant, the
/// stern;</item>
/// <item><b>the bubble's whip</b> (<see cref="HullGirder.Bubble"/>): none from a charge at her plate, its bubble venting into what it
/// opened (research 13 §4.3); a mine's under her keel whips her.</item>
/// </list>
/// Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class Underwater
{
    public static EffectsResult Apply(ShipClass c, ShipCondition k, in Hit hit, SimSettings s, ref RandomStream r, out Outcome outcome)
    {
        var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
        var p = hit.Point;
        var cells = c.Cells;
        int first = -1;
        double nearest = double.MaxValue;
        for (int i = 0; i < cells.Length; i++)
        {
            double d = Effects.BoxDistance(cells[i].Box, p);
            if (d < nearest)
                (first, nearest) = (i, d);
        }
        if (first >= 0 && cells[first].Room >= 0)
            res.Part = c.RoomNode(cells[first].Room);
        if (first < 0 || r.NextDouble() < hit.Torpedo.DudShare)   // the torpedo's own (it was the struck ship's era's until 2026-10-19)
        {
            outcome = Outcome.Dud;
            return res;
        }

        Casualties.Begin(k, hit.Ms);
        double w = Math.Max(hit.Torpedo.ChargeKg, 0), size = Math.Cbrt(w);
        double depth = HitQuery.WaterOf(c, k.Attitude).At(p.X, p.Y) - p.Z;
        var near = NearField.Charge(size, 1, NearField.Tamp(depth, size, s), s);
        var b0 = cells[first].Box;
        var at = new P3(Math.Clamp(p.X, b0.X0, b0.X1), Math.Clamp(p.Y, b0.Y0, b0.Y1), Math.Clamp(p.Z, b0.Base, b0.Top));
        var net = GasNetwork.For(c);
        res.Blast = net.Strike(k, first, at, near, s, ref r, w * s.UnderwaterGasShare, Products);
        res.BubbleWhip = HullGirder.Of(c).Bubble(w, p, Math.Sqrt((p.X - at.X) * (p.X - at.X) + (p.Y - at.Y) * (p.Y - at.Y) + (p.Z - at.Z) * (p.Z - at.Z)),
            HitQuery.WaterOf(c, k.Attitude), s);   // none at her plate: its bubble vents into what it opened
        res.BreachM2 += res.Blast.HoleM2;
        outcome = Outboard(c, first) && !Inside(c, net) ? Outcome.Held : Outcome.Exploded;

        Reach(c, k, net, p, near.ReachM, s, ref res, ref r);
        Effects.GasFelt(c, k, net, -1, s, ref res);
        NearMiss.Shock(c, k, hit, w, s, ref res, ref r);
        Casualties.Draw(c, k, hit.Ms, s, ref res, ref r);
        k.CrewLost += res.CrewLost;
        return res;
    }

    /// <summary>What's within the reach in the spaces the explosion got into: those within half of it put out of action, the rest
    /// damaged, a share of their men lost; the nearest magazine among them, lit as by a burst among its charges
    /// (<see cref="SimSettings.MagazineFlashChance"/>, whole within the half the explosion wrecks, fading to none at the reach;
    /// docs/battle/magazines.md V7); the parts within it standing in those spaces or in the sea (shafts, propellers, rudders).</summary>
    static void Reach(ShipClass c, ShipCondition k, GasNetwork net, P3 p, double reach, SimSettings s, ref EffectsResult res, ref RandomStream r)
    {
        var cells = c.Cells;
        int magazine = -1;
        double magazineD = double.MaxValue;
        for (int i = 0; i < cells.Length; i++)
        {
            double d = Effects.BoxDistance(cells[i].Box, p);
            if (d > reach || !net.InRun(i))
                continue;
            res.CrewLost += cells[i].BattleCrew * s.TorpedoCasualtyShare * (1 - d / reach);
            foreach (int room in c.RoomsIn(i))
            {
                int node = c.RoomNode(room);
                var state = d <= reach / 2 ? PartState.Disabled : PartState.Damaged;
                Effects.Count(ref res, k, node, (state == PartState.Damaged ? k.Harm(node) : k.Worsen(node, state)) ? k.State[node] : PartState.Ok);
                if (c.Rooms[room].Kind == RoomKind.Magazine && d < magazineD)
                    (magazine, magazineD) = (room, d);
            }
        }
        res.Magazine = magazine >= 0 && r.NextDouble() < s.MagazineFlashChance * Math.Clamp(2 * (1 - magazineD / reach), 0, 1);
        (res.MagazineRoom, res.MagazineVia) = res.Magazine ? (magazine, MagazineVia.Underwater) : (-1, MagazineVia.None);

        var comps = c.Components;
        for (int i = 0; i < comps.Length; i++)
        {
            if (Effects.Distance(c, i, p) > reach || !Reached(c, net, comps[i].Bounds))
                continue;
            var state = Effects.Struck(comps[i].Kind);
            Effects.Count(ref res, k, i, k.Worsen(i, state) ? state : PartState.Ok);
        }
    }

    /// <summary>Whether a part stands where the explosion got: in a space it reached, or in none of her spaces (the sea's).</summary>
    static bool Reached(ShipClass c, GasNetwork net, in Box box)
    {
        bool inAny = false;
        for (int i = 0; i < c.Cells.Length; i++)
            if (Effects.Overlaps(c.Cells[i].Box, box))
            {
                if (net.InRun(i))
                    return true;
                inAny = true;
            }
        return !inAny;
    }

    /// <summary>Whether the explosion opened a way from a space outside a holding bulkhead into one inside it.</summary>
    static bool Inside(ShipClass c, GasNetwork net)
    {
        foreach (int i in net.Reached())
        {
            if (!Outboard(c, i))
                continue;
            var nb = c.Cells[i].Neighbours;
            for (int j = nb.Start; j < nb.End; j++)
                if (!Outboard(c, c.Neighbour(j).Cell) && net.Opened(j))
                    return true;
        }
        return false;
    }

    /// <summary>A charge's products as they come into her from the water: TNT's detonation alone, no air to burn in [SRC: TNT's
    /// 4.5 MJ/kg, Content/Templates/explosives.json; its torpedo's charge is TNT's worth].</summary>
    static readonly ShellTypeRow Products = new() { FillerRe = 1, FillerQDet = 4.5, FillerQComb = 4.5, FillerEta = 1 };

    /// <summary>Whether a cell lies outside a holding bulkhead: a TDS cell, or the double bottom.</summary>
    static bool Outboard(ShipClass c, int cell)
    {
        if (c.Cells[cell].TdsM > 0)
            return true;
        foreach (int room in c.RoomsIn(cell))
            if (c.Rooms[room].Kind is RoomKind.Tds or RoomKind.DoubleBottom)
                return true;
        return false;
    }
}
