using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>The captains' searchlights and star shell (docs/battle/night.md Q4, Q5), their first use: to fire. In the dark
/// (where it adds to his lookouts' threshold, <see cref="SightModel.DarkOf(double, SimSettings)"/>), a captain in an
/// encounter has his arcs struck behind their shutters; he lights the target of his engaged batteries that their fire
/// control can't make out (the chief battery's first, then the nearest): with star shell where his ship carries it and she's
/// within its reach (<see cref="StarShellSystem.ReachM"/>: it shows nothing of the firer but his gun's flash), else with his
/// searchlights within <see cref="SimSettings.SearchlightOpenShare"/> of their reach (<see cref="SearchlightModel.ReachM"/>:
/// where his operator makes out the reference ship by them, in this light and haze). He keeps her lit until his navy loses
/// her, sees her going down, or she's past <see cref="SimSettings.SearchlightDouseShare"/> of the reach; then shutters again.
/// Out of encounters, or by day, the arcs go out and the star shell stops. A ship pressing a torpedo attack keeps dark.
/// Doctrine's when to burn them, searching, keeping dark and dousing when lit are Q7's.</summary>
public sealed partial class CaptainLayer
{
    void NightLights(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var cls = own.Handling.Class;
        var world = ctx.World;
        var (big, offset) = SearchlightModel.Biggest(cls);
        var (starOn, _) = world.StarShells.TaskOf(own.Entity);
        double starReach = world.StarShells.ReachM(own.Entity);
        if (big < 0 && starReach <= 0 && starOn == 0)
            return;
        var s = ctx.Settings;
        var (order, target, _) = world.Searchlights.OrderOf(own.Entity, ctx.NowMs);
        var weather = world.Weather.At(own.Position, world.Time);
        double sky = NightLight.At(own.Position, world.Time, weather.Cloud, s).SkyCdM2;
        double reach = big >= 0 ? SearchlightModel.ReachM(cls.Searchlights[big], offset, weather.VisibilityM, sky, s) : 0;
        bool burn = !(cap.Attacking && cap.Order == CaptainOrder.Fight) && encounters.Of(slot, own.Entity) != 0 &&
                    SightModel.DarkOf(sky * SightModel.LookoutGlass(SightModel.PupilMm(sky), s), s).Any;
        if (!burn)
        {
            if (order != SearchlightOrder.Off)
                ctx.Issue(SearchlightSystem.Order(own.Entity, SearchlightOrder.Off));
            if (starOn != 0)
                ctx.Issue(StarShellSystem.Illuminate(own.Entity, 0));
            return;
        }
        var pic = world.Sightings.Of(own.Side);
        // what's lit stays lit while she's held and within reach
        int pick = 0;
        bool byStar = false;
        if (starOn != 0 && Held(ctx, pic, own, starOn, s.SearchlightDouseShare * starReach))
            (pick, byStar) = (starOn, true);
        else if (order == SearchlightOrder.Illuminate && Held(ctx, pic, own, target, s.SearchlightDouseShare * reach))
            pick = target;
        else
        {
            // his batteries' targets their fire control can't make out, the chief battery's first, then the nearest
            double best = double.MaxValue, at = 0, within = Math.Max(s.SearchlightOpenShare * reach, starReach);
            int chief = ChiefBattery(cls);
            for (int b = 0; b < cls.Batteries.Length; b++)
            {
                int t = ctx.Ships.Combat.Target(slot, b);
                if (t == 0 || ctx.Ships.Combat.Sees(slot, b) || !Held(ctx, pic, own, t, within, out double d))
                    continue;
                if ((b == chief ? -1 : d) < best)
                    (best, pick, at) = (b == chief ? -1 : d, t, d);
            }
            byStar = pick != 0 && at <= starReach;
            if (pick != 0 && !byStar && at > s.SearchlightOpenShare * reach)
                pick = 0;
        }
        if (byStar)
        {
            if (starOn != pick)
                ctx.Issue(StarShellSystem.Illuminate(own.Entity, pick));
        }
        else if (starOn != 0)
            ctx.Issue(StarShellSystem.Illuminate(own.Entity, 0));
        if (big < 0)
            return;
        if (pick != 0 && !byStar)
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
