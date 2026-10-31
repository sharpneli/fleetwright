using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>A search's line (docs/battle/balance.md BL5, the user's 2026-10-31: "these are decisions for the admiral leading
/// the force, if they estimate the risk from spreading out is worth it"): spread abreast, a squadron's ships or a force's,
/// a line sweeps as wide as its ships, but each piece of it may meet the enemy alone. The admiral spreads it if every
/// piece (a force's groups, a squadron's ships) could take on alone the enemy his navy knows is out there (the picture's
/// every contact not seen going down) at his doctrine's bar, or get away from it (<see cref="MatchupJudge.Outruns"/>);
/// else it searches closed up, as one.</summary>
public sealed partial class AdmiralLayer
{
    readonly MatchupSide spreadPiece = new(), spreadEnemy = new();

    /// <summary>How many ships abreast a unit searches with: its line's, spread (<see cref="SimSettings.SearchSpacingShare"/>
    /// of the visibility apart), if its admiral judges the risk worth it; 1 if it keeps closed up (or is a ship alone).</summary>
    public int SearchLine(in AiContext ctx, Unit u)
    {
        if (!u.IsGroup || groups.Get(u.GroupId) is not { } g || ctx.Ships.SlotOf(groups.GuideShip(g)) is not (int gs and >= 0))
            return 1;
        var guide = ctx.Ships[gs];
        int ships = 0;
        groups.LineOffsets(g, line);
        foreach (var (ship, _, _) in line)
            if (ctx.Ships.SlotOf(ship) >= 0)
                ships++;
        if (ships <= 1)
            return 1;
        spreadEnemy.Clear();
        if (ctx.World.Sightings.Of(guide.Side) is { } pic)
            for (int i = 0; i < pic.Count; i++)
                spreadEnemy.AddContact(ctx.World, Judge.Matchups, guide.Side, pic[i]);
        if (spreadEnemy.Count == 0)
            return ships;   // nothing known out there: no risk to weigh
        double accept = Judge.Bars(guide.Entity, guide.Side).Accept;
        for (int k = 0; k < g.Line.Count; k++)
        {
            spreadPiece.Clear();
            if (g.IsForce)
            {
                if (groups.Get(g.Line[k]) is { } sub)
                    Judge.AddGroup(ctx.World, sub, spreadPiece);
            }
            else if (ctx.Ships.SlotOf(g.Line[k]) is int slot and >= 0)
                spreadPiece.AddOwn(ctx.World, Judge.Matchups, slot);
            if (spreadPiece.Count == 0 || MatchupJudge.Outruns(spreadPiece, spreadEnemy, ctx.Settings))
                continue;
            if (Judge.Judge(ctx, guide.Entity, guide.Side, guide.Position, spreadPiece, spreadEnemy).Confidence < accept)
                return 1;   // this piece alone would be caught by what he can't beat: closed up
        }
        return ships;
    }
}
