using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>The captains' searchlights (docs/battle/night.md Q4), their first use: to fire. In the dark (where it adds to
/// his lookouts' threshold, <see cref="SightModel.DarkOf(double, SimSettings)"/>), a captain in an encounter has his arcs
/// struck behind their shutters; he lights the target of his engaged batteries that their fire control can't make out
/// (the chief battery's first, then the nearest), within <see cref="SimSettings.SearchlightOpenShare"/> of his lights'
/// reach (<see cref="SearchlightModel.ReachM"/>: where his operator makes out the reference ship by them, in this light
/// and haze), and holds it on her until his navy loses her, sees her going down, or she's past
/// <see cref="SimSettings.SearchlightDouseShare"/> of it; then shutters again. Out of encounters, or by day, the arcs go out. A ship pressing a torpedo attack keeps dark. Doctrine's when to burn them,
/// searching for torpedo craft, keeping dark and dousing when lit are Q7's.</summary>
public sealed partial class CaptainLayer
{
    void Searchlights(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var cls = own.Handling.Class;
        var (big, offset) = SearchlightModel.Biggest(cls);
        if (big < 0)
            return;
        var s = ctx.Settings;
        var world = ctx.World;
        var (order, target, _) = world.Searchlights.OrderOf(own.Entity, ctx.NowMs);
        var weather = world.Weather.At(own.Position, world.Time);
        double sky = NightLight.At(own.Position, world.Time, weather.Cloud, s).SkyCdM2;
        double reach = SearchlightModel.ReachM(cls.Searchlights[big], offset, weather.VisibilityM, sky, s);
        bool burn = !(cap.Attacking && cap.Order == CaptainOrder.Fight) && encounters.Of(slot, own.Entity) != 0 &&
                    SightModel.DarkOf(sky * SightModel.LookoutGlass(SightModel.PupilMm(sky), s), s).Any;
        if (!burn)
        {
            if (order != SearchlightOrder.Off)
                ctx.Issue(SearchlightSystem.Order(own.Entity, SearchlightOrder.Off));
            return;
        }
        var pic = world.Sightings.Of(own.Side);
        int pick = order == SearchlightOrder.Illuminate && Held(ctx, pic, own, target, s.SearchlightDouseShare * reach) ? target : 0;
        if (pick == 0)
        {
            // his batteries' targets their fire control can't make out, the chief battery's first, then the nearest
            double best = double.MaxValue;
            int chief = ChiefBattery(cls);
            for (int b = 0; b < cls.Batteries.Length; b++)
            {
                int t = ctx.Ships.Combat.Target(slot, b);
                if (t == 0 || ctx.Ships.Combat.Sees(slot, b) || !Held(ctx, pic, own, t, s.SearchlightOpenShare * reach, out double d))
                    continue;
                if (b == chief)
                    d = -1;
                if (d < best)
                    (best, pick) = (d, t);
            }
        }
        if (pick != 0)
        {
            if (order != SearchlightOrder.Illuminate || target != pick)
                ctx.Issue(SearchlightSystem.Order(own.Entity, SearchlightOrder.Illuminate, pick));
        }
        else if (order != SearchlightOrder.Ready)
            ctx.Issue(SearchlightSystem.Order(own.Entity, SearchlightOrder.Ready));
    }

    static bool Held(in AiContext ctx, Picture? pic, ShipState own, int target, double withinM) => Held(ctx, pic, own, target, withinM, out _);

    /// <summary>Whether his navy holds a contact (not seen going down) within a distance of him by its picture, and how far.</summary>
    static bool Held(in AiContext ctx, Picture? pic, ShipState own, int target, double withinM, out double distanceM)
    {
        distanceM = double.MaxValue;
        int ci = pic?.IndexOf(target) ?? -1;
        if (ci < 0 || pic![ci].Lost)
            return false;
        ref readonly var c = ref pic[ci];
        distanceM = Earth.Distance(own.Position, c.Position + c.Velocity * ((ctx.NowMs - c.EstMs) / 1000.0));
        return distanceM <= withinM;
    }
}
