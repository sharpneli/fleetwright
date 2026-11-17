using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>Where a fast wing goes in battle (docs/battle/allocation.md AL5, decision 4).</summary>
public enum WingChoice : byte
{
    /// <summary>Nothing repays it: it keeps its station (scouting off its main body's bow), clear of the fight.</summary>
    None,

    /// <summary>His main body, its van: the line is outmatched without it.</summary>
    Line,

    /// <summary>His main body's van, flanking it rather than joining the line: the most it does for the least.</summary>
    Flank,

    /// <summary>Another of his groups (his fast wing, his scouts, his flotillas): the most it does there for the least.</summary>
    Group,
}

/// <summary>The fast wing's choice (docs/battle/allocation.md "At contact", decision 4): a unit strong, with speed to spare
/// over its line (<see cref="Roles.IsFastWing"/>), once the enemy's held, goes where its strength does the most for the
/// least. First the line: if the rest of its side (its force, inside one) is outmatched by his main body without it
/// (under doctrine's bar), it goes for his main body's van. Else each group he holds is a choice, if the wing alone
/// isn't hopeless against it (doctrine's bar), worth what the wing puts out of it (the matchup's share of it lost) times
/// what that group is worth: its strength against the side's fleet (<see cref="ForceMeasures.Enemy"/>), or for a group
/// whose torpedoes are its weapon against the line, what its attack would take of the line if it isn't broken up, if
/// that's more. His main body taken is its van, flanked. The choice kept unless another is worth
/// <see cref="SimSettings.CommanderRetargetShare"/> times as much: he doesn't turn the wing at each shift of the picture.
/// No reserve held back (a wing no choice repays stays clear), and no choice across his way home (an aim's Stop). Pure
/// of the world at the moment; nothing hashed; nothing allocated once grown.</summary>
public sealed class FastWing(MatchupJudge judge, HeldGroups held, ForceMeasures measures)
{
    MatchupSide[] sides = [];
    double[] strength = [];
    bool[] line = [];

    /// <summary>The wing's choice against the groups a side holds now: what, which held group (-1: none) and the ship to
    /// close (his van, or the group's biggest). <paramref name="wing"/> is the wing's ships, <paramref name="rest"/> the
    /// line it fights beside (its side's, or its force's, without it), <paramref name="current"/> the group it's on now
    /// (-1: none).</summary>
    public (WingChoice Choice, int Group, int Target) Choose(in AiContext ctx, byte side, MatchupSide wing, MatchupSide rest, int current)
    {
        var hs = held.Of(ctx, side);
        if (hs.Length == 0 || wing.Count == 0)
            return (WingChoice.None, -1, 0);
        Grow(hs.Length);
        var pic = ctx.World.Sightings.Of(side)!;
        int main = -1;
        for (int k = 0; k < hs.Length; k++)
        {
            var e = sides[k];
            e.Clear();
            foreach (int i in held.Members(side, hs[k]))
                e.AddContact(ctx.World, judge.Matchups, side, pic[i]);
            (strength[k], _, line[k]) = e.Count > 0 ? measures.Enemy(side, e) : default;
            if (line[k] && (main < 0 || strength[k] > strength[main]))
                main = k;
        }
        double accept = judge.Bars(0, side).Accept;
        if (main >= 0 && rest.Count > 0 && judge.Judge(ctx, 0, side, hs[main].Centre, rest, sides[main]).Confidence < accept)
            return (WingChoice.Line, main, Van(ctx, side, hs[main]));
        int best = -1;
        double bestV = 0, currentV = 0;
        for (int k = 0; k < hs.Length; k++)
        {
            var e = sides[k];
            if (e.Count == 0)
                continue;
            var r = judge.Judge(ctx, 0, side, hs[k].Centre, wing, e);
            if (r.Confidence < accept)
                continue;
            double worth = strength[k];
            if (rest.Count > 0 && !measures.Guns(e, rest))
                worth = Math.Max(worth, judge.Judge(ctx, 0, side, hs[k].Centre, e, rest).Terms.LostB * Tonnage(rest));
            double v = r.Terms.LostB * worth;
            if (k == current)
                currentV = v;
            if (v > bestV)
                (best, bestV) = (k, v);
        }
        if (current >= 0 && current < hs.Length && currentV > 0 && currentV * ctx.Settings.CommanderRetargetShare >= bestV)
            best = current;
        if (best < 0)
            return (WingChoice.None, -1, 0);
        return best == main ? (WingChoice.Flank, best, Van(ctx, side, hs[best])) : (WingChoice.Group, best, hs[best].Biggest);
    }

    /// <summary>The held group a contact is in, -1 for none.</summary>
    public int GroupOf(in AiContext ctx, byte side, int entity)
    {
        if (entity == 0)
            return -1;
        var pic = ctx.World.Sightings.Of(side);
        if (pic == null)
            return -1;
        var hs = held.Of(ctx, side);
        for (int k = 0; k < hs.Length; k++)
            foreach (int i in held.Members(side, hs[k]))
                if (pic[i].Entity == entity)
                    return k;
        return -1;
    }

    /// <summary>A group's van: its ship furthest along its course (the picture's estimates), the nearest its centre
    /// stopped.</summary>
    int Van(in AiContext ctx, byte side, in HeldGroup g)
    {
        var pic = ctx.World.Sightings.Of(side)!;
        int van = g.Biggest;
        double best = double.MaxValue;
        foreach (int i in held.Members(side, g))
        {
            var d = pic.EstimateAt(i, ctx.NowMs, ctx.Settings).Position - g.Centre;
            double key = g.Velocity.Length > 1 ? -Vec3d.Dot(d, g.Velocity) : d.Length;
            if (key < best)
                (best, van) = (key, pic[i].Entity);
        }
        return van;
    }

    static double Tonnage(MatchupSide s)
    {
        double t = 0;
        foreach (var e in s.Entries)
            t += e.Profile.DisplacementT * e.Count;
        return t;
    }

    void Grow(int n)
    {
        if (sides.Length >= n)
            return;
        int had = sides.Length;
        Array.Resize(ref sides, Math.Max(n, 2 * had));
        for (int k = had; k < sides.Length; k++)
            sides[k] = new MatchupSide();
        Array.Resize(ref strength, sides.Length);
        Array.Resize(ref line, sides.Length);
    }
}
