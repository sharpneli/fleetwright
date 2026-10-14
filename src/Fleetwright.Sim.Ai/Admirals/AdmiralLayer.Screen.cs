using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>A group's screen (docs/battle/command.md 5.1b; the task proper, with its ends and hand-back, is 5.1c's): it
/// keeps <see cref="SimSettings.ScreenM"/> (or the distance ordered) off the screened unit's guide, on the bearing of the
/// nearest enemy in sight within <see cref="SimSettings.FightCompareM"/> (ahead of it while there's none), given again
/// only when that moves it more than <see cref="SimSettings.ScreenMoveM"/>. The counter-attack: while enemy torpedo craft
/// close on the screened unit (<see cref="TorpedoThreat"/>, as its own turn-away sees them) the screen fights the nearest,
/// and goes back to its station <see cref="SimSettings.TurnBackS"/> after the last. It doesn't take up doctrine's Engage
/// (screening is its fight); its guns fire at will as always.</summary>
public sealed partial class AdmiralLayer
{
    void ScreenStep(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        if (a.Order != CaptainOrder.Screen)
            return;
        var s = ctx.Settings;
        long now = ctx.NowMs;
        var to = GuideUnit(g);
        var o = ctx.Orders;
        const OrderSource Up = OrderSource.Superior;
        var screened = new Unit(a.Target);
        var sg = screened.IsGroup ? groups.Get(screened.GroupId) : null;
        int ship = sg != null ? groups.GuideShip(sg) : screened.Entity;
        if (ctx.Ships.SlotOf(ship) is not (int slot and >= 0))
        {
            // nobody left to screen: it holds what it has
            (a.Order, a.CourseDeg, a.RudderDeg, a.ScreenFight, a.ScreenOn) = (CaptainOrder.Course, double.NaN, 0, 0, 0);
            ctx.Issue(o.Hold(to, Up));
            return;
        }
        var guide = ctx.Ships[slot];

        TorpedoThreat(ctx, sg, guide, out _, out int craft);
        if (craft != 0)
        {
            a.ScreenThreatMs = now;
            if (a.ScreenFight != craft)
            {
                a.ScreenFight = craft;
                ctx.Issue(o.Fight(to, Unit.Ship(craft), 0, Up));   // the counter-attack
            }
            return;
        }
        if (a.ScreenFight != 0)
        {
            if (now - a.ScreenThreatMs < (long)(s.TurnBackS * 1000))
                return;
            (a.ScreenFight, a.ScreenOn) = (0, 0);   // beaten off: back to the station
        }

        double rel = NearestEnemyBearing(ctx, guide) is double b ? (b - guide.HeadingDeg) * Math.PI / 180 : 0;
        double astern = -a.DistanceM * Math.Cos(rel), abeam = a.DistanceM * Math.Sin(rel);
        double moved = Math.Sqrt((astern - a.ScreenAstern) * (astern - a.ScreenAstern) + (abeam - a.ScreenAbeam) * (abeam - a.ScreenAbeam));
        if (a.ScreenOn == ship && moved <= s.ScreenMoveM)
            return;
        (a.ScreenOn, a.ScreenAstern, a.ScreenAbeam) = (ship, astern, abeam);
        ctx.Issue(o.Station(to, Unit.Ship(ship), astern, abeam, Up));
    }

    /// <summary>The bearing from a ship to the nearest enemy her navy holds in sight within
    /// <see cref="SimSettings.FightCompareM"/>, or null.</summary>
    double? NearestEnemyBearing(in AiContext ctx, ShipState from)
    {
        double best = double.MaxValue;
        double? bearing = null;
        foreach (int i in seen.Near(ctx.World, from.Side, from.Position, ctx.Settings.FightCompareM))
        {
            var q = seen.Position(from.Side, i);
            double d = (q - from.Position).LengthSquared;
            if (d < best)
                (best, bearing) = (d, Earth.Bearing(from.Position, q));
        }
        return bearing;
    }
}
