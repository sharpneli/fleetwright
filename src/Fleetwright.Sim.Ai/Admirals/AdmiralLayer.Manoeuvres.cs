using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>The admirals' manoeuvres (docs/battle/command.md 5.1b): a group's line turned together, every ship at once, or
/// the rear ship first (the battle turn-away). The formation is changed at the start to what the ships will be in once
/// they've turned (<see cref="GroupSystem.TurnTogether"/>: the line stays where it lies, now on a bearing from the new
/// heading); then each ship is given the new course (through 16 points, a quarter at a time, so she turns the way the
/// signal said), at the speed the guide had, and nobody in the tree is given a station until every ship is round
/// (<see cref="SimSettings.ManoeuvreDoneDeg"/>, or <see cref="SimSettings.ManoeuvreMaxMs"/>). Then the group holds the new
/// course, and the stations are given on the new line. A turn the admiral makes on his own (the turn-away from
/// torpedoes) keeps his order; turned back, it's handed on again. A turn in succession is a course to a line ahead: its
/// ships turn where the guide turned.</summary>
public sealed partial class AdmiralLayer
{
    /// <summary>Starts a turn together of a group's line through an angle (+ to starboard). <paramref name="keep"/>: his
    /// own turn, his order stays; else the group's order becomes the new course.</summary>
    void StartTurn(in AiContext ctx, GroupSystem.Group g, Admiral a, double deg, bool rearFirst, bool keep)
    {
        int lead = ctx.Ships.SlotOf(groups.GuideShip(g));
        if (lead < 0)
            return;
        var guide = ctx.Ships[lead];

        // who turns when: rear first, by their stations astern of the guide as they lie now
        groups.LineOffsets(g, line);
        int n = line.Count;
        if (a.TurnShips.Length < n)
            a.TurnShips = new int[Math.Max(n, 2 * a.TurnShips.Length)];
        Ensure(n);
        for (int i = 0; i < n; i++)
            (keys[i], items[i]) = (rearFirst ? -line[i].AsternM : i, line[i].Ship);
        Array.Sort(keys, items, 0, n);
        Array.Copy(items, a.TurnShips, n);

        (a.Turning, a.TurnCount, a.TurnDeg, a.TurnRearFirst, a.TurnStartMs) = (true, n, deg, rearFirst, ctx.NowMs);
        a.TurnCourse = Wrap360(guide.HeadingDeg + deg);
        a.TurnKn = guide.SpeedOrderKn;
        ctx.Issue(GroupSystem.TurnTogether(g.GroupId, deg));
        if (!keep)
            (a.Order, a.CourseDeg, a.RudderDeg, a.RouteCount, a.Loop, a.Watch) = (CaptainOrder.Course, a.TurnCourse, 0, 0, false, false);
        TurnStep(ctx, g, a);
    }

    /// <summary>A turn together, each think: the ships whose time has come given the new course (a quarter of the way
    /// at a time past 150°, so each turns the way the signal said), and the speed; done when every one is round, or the
    /// time's up.</summary>
    void TurnStep(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        var s = ctx.Settings;
        var ships = ctx.Ships;
        long since = ctx.NowMs - a.TurnStartMs;
        int due = a.TurnRearFirst ? 1 + (int)(since / Math.Max(1, (long)(s.BattleTurnStepS * 1000))) : a.TurnCount;
        bool round = true;
        for (int k = 0; k < a.TurnCount; k++)
        {
            int e = a.TurnShips[k];
            if (ships.SlotOf(e) is not (int slot and >= 0) || groups.UnitOf(e) != groups.TopOf(g))
                continue;   // gone, or out of the line
            if (k >= due)
            {
                round = false;
                continue;
            }
            var own = ships[slot];
            double off = Wrap180(a.TurnCourse - own.HeadingDeg);
            if (Math.Abs(off) > s.ManoeuvreDoneDeg)
                round = false;
            double want = Math.Abs(off) > 150 ? Wrap360(own.HeadingDeg + Math.Sign(a.TurnDeg) * 90) : a.TurnCourse;
            var (order, _, source) = captains.Of(e);
            if (order != CaptainOrder.Course || source != OrderSource.Superior || own.Helm != Helm.Course ||
                Math.Abs(Wrap180(want - own.CourseOrderDeg)) > s.CaptainCourseDeadbandDeg)
                ctx.Issue(ctx.Orders.Course(Unit.Ship(e), want, 0, OrderSource.Superior));
            if (!(Math.Abs(captains.SpeedOf(e).AskedKn - a.TurnKn) <= s.CaptainSpeedDeadbandKn))   // NaN too: none asked
                ctx.Issue(ctx.Orders.Speed(Unit.Ship(e), a.TurnKn, OrderSource.Superior));
        }
        if (round || since > s.ManoeuvreMaxMs)
            EndTurn(ctx, g, a);
    }

    /// <summary>The turn's done: his order handed to the guide again (the new course, or the one he kept), and the
    /// stations given on the new line by the think that follows.</summary>
    void EndTurn(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        a.Turning = false;
        if (a.Order == CaptainOrder.Course && double.IsNaN(a.AskedKn))
            a.AskedKn = a.TurnKn;   // the speed the line turned at, kept
        var to = GuideUnit(g);
        if (to.Id != 0)
        {
            a.HandedTo = 0;   // handed afresh: every ship had his own course for the turn
            HandOver(ctx, a, to);
        }
    }

    /// <summary>Whether a group is turning together, or the line it's in is.</summary>
    bool Manoeuvring(GroupSystem.Group g)
    {
        for (var at = g; ;)
        {
            if (Find(at.GroupId) is int i and >= 0 && admirals[i].Turning)
                return true;
            if (!groups.InForceLine(at) || groups.Get(at.Parent) is not { } up)
                return false;
            at = up;
        }
    }

    static double Wrap180(double d) => d - 360 * Math.Round(d / 360);

    static double Wrap360(double d) => ((d % 360) + 360) % 360;
}
