using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>The captains' searchlights and star shell (docs/battle/night.md Q4, Q5, Q7). In the dark (where it adds to his
/// lookouts' threshold, <see cref="SightModel.DarkOf(double, SimSettings)"/>), a captain in an encounter has his arcs struck
/// behind their shutters, as doctrine's <see cref="DoctrineField.Searchlights"/> lets him: never; once he's under fire; to
/// fire; or to search as well. To fire, he lights the target of his engaged batteries that their fire control can't make
/// out (the chief battery's first, then the nearest): with star shell where his ship carries it, doctrine's
/// <see cref="DoctrineField.StarShell"/> lets him and she's within its reach (<see cref="StarShellSystem.ReachM"/>: it shows
/// nothing of the firer but his gun's flash), else with his searchlights within <see cref="SimSettings.SearchlightOpenShare"/>
/// of their reach (<see cref="SearchlightModel.ReachM"/>: where his operator makes out the reference ship by them, in this
/// light and haze). He keeps her lit until his navy loses her, sees her going down, or she's past
/// <see cref="SimSettings.SearchlightDouseShare"/> of the reach; then shutters again. Star shell "to find" lights, with
/// nothing to fire at, the nearest contact in its reach his navy holds but doesn't see or hasn't made out; searchlights "to
/// search" sweep <see cref="SimSettings.SearchHalfDeg"/> either side of the nearest contact held. Out of encounters, or by
/// day, the arcs go out and the star shell stops. A ship pressing a torpedo attack keeps dark, and so does one turned away
/// from a light on her (<see cref="TurnFromLight"/>). A darkened unit's (doctrine's <see cref="DoctrineField.Darkened"/>,
/// docs/battle/night.md Q9) shows nothing of its own. The player's hand beats it all: his beams kept on (held on the guns'
/// target, chief battery's first, seen or not; else the one lit while she's held; else sweeping about the nearest contact,
/// or ahead), his star shell task left alone until it ends.</summary>
public sealed partial class CaptainLayer
{
    /// <summary>Whether it's dark where she is for her lookouts (the night sky adds to their threshold), and the sky's
    /// luminance, cd/m².</summary>
    static bool InDark(in AiContext ctx, ShipState own, out double skyCdM2)
    {
        var world = ctx.World;
        var weather = world.Weather.At(own.Position, world.Time);
        skyCdM2 = NightLight.At(own.Position, world.Time, weather.Cloud, ctx.Settings).SkyCdM2;
        return SightModel.DarkOf(skyCdM2 * SightModel.LookoutGlass(SightModel.PupilMm(skyCdM2), ctx.Settings), ctx.Settings).Any;
    }

    void NightLights(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var cls = own.Handling.Class;
        var world = ctx.World;
        var (big, offset) = SearchlightModel.Biggest(cls);
        if (cap.StarHeld && !world.StarShells.Busy(own.Entity, out _))
            cap.StarHeld = false;   // the player's task ended: the contact lost or gone, no mount left to fire it
        // the player's star shell task is his to end: his doctrine's left out of it
        var (starOn, _) = cap.StarHeld ? (0, -1) : world.StarShells.TaskOf(own.Entity);
        double starReach = cap.StarHeld ? 0 : world.StarShells.ReachM(own.Entity);
        if (big < 0 && starReach <= 0 && starOn == 0)
            return;
        var s = ctx.Settings;
        var use = (SearchlightUse)doctrine.Of(own.Entity, own.Side, DoctrineField.Searchlights);
        var starUse = (StarShellUse)doctrine.Of(own.Entity, own.Side, DoctrineField.StarShell);
        if (candDark)
            (use, starUse) = (SearchlightUse.Never, StarShellUse.Never);   // darkened: nothing of his own shows
        if (starUse == StarShellUse.Never)
            starReach = 0;
        var (order, target, _) = world.Searchlights.OrderOf(own.Entity, ctx.NowMs);
        var weather = world.Weather.At(own.Position, world.Time);
        bool dark = InDark(ctx, own, out double sky);
        bool doused = cap.HeldUntilMs > ctx.NowMs && cap.HeldFor == CaptainReason.Lit;
        bool held = cap.BeamsHeld && big >= 0;
        bool burn = dark && (held || !(cap.Attacking && cap.Order == CaptainOrder.Fight) && !doused && encounters.Of(slot, own.Entity) != 0);
        bool beams = big >= 0 && (held || use switch
        {
            SearchlightUse.Never => false,
            SearchlightUse.WhenFiredOn => slot < underFire.Length && underFire[slot] > 0,
            _ => true,
        });
        if (!burn || !beams && starReach <= 0)
        {
            if (order != SearchlightOrder.Off)
                ctx.Issue(SearchlightSystem.Order(own.Entity, SearchlightOrder.Off));
            if (starOn != 0)
                ctx.Issue(StarShellSystem.Illuminate(own.Entity, 0));
            return;
        }
        double reach = beams ? SearchlightModel.ReachM(cls.Searchlights[big], offset, weather.VisibilityM, sky, s) : 0;
        var pic = world.Sightings.Of(own.Side);
        // what's lit stays lit while she's held and within reach
        int pick = 0;
        bool byStar = false;
        if (starOn != 0 && starReach > 0 && Held(ctx, pic, own, starOn, s.SearchlightDouseShare * starReach))
            (pick, byStar) = (starOn, true);
        else if (!held && order == SearchlightOrder.Illuminate && beams && Held(ctx, pic, own, target, s.SearchlightDouseShare * reach))
            pick = target;
        else
        {
            // his batteries' targets their fire control can't make out, the chief battery's first, then the nearest; the
            // player's beams on any of them, seen or not, at any range
            double best = double.MaxValue, at = 0, within = held ? double.MaxValue : Math.Max(s.SearchlightOpenShare * reach, starReach);
            int chief = ChiefBattery(cls);
            for (int b = 0; b < cls.Batteries.Length; b++)
            {
                int t = ctx.Ships.Combat.Target(slot, b);
                if (t == 0 || !held && ctx.Ships.Combat.Sees(slot, b) || !Held(ctx, pic, own, t, within, out double d))
                    continue;
                if ((b == chief ? -1 : d) < best)
                    (best, pick, at) = (b == chief ? -1 : d, t, d);
            }
            byStar = !held && pick != 0 && at <= starReach;
            if (pick != 0 && !byStar && !held && at > s.SearchlightOpenShare * reach)
                pick = 0;
            if (pick == 0 && held && order == SearchlightOrder.Illuminate && Held(ctx, pic, own, target, double.MaxValue))
                pick = target;   // nothing new to light: the one lit kept while she's held
            if (pick == 0 && starUse == StarShellUse.ToFind && starReach > 0 && pic != null)
                (pick, byStar) = (ToFind(ctx, pic, own, starReach), true);
        }
        if (byStar && pick != 0)
        {
            if (starOn != pick)
                ctx.Issue(StarShellSystem.Illuminate(own.Entity, pick));
        }
        else if (starOn != 0)
            ctx.Issue(StarShellSystem.Illuminate(own.Entity, 0));
        if (!beams)
        {
            if (order != SearchlightOrder.Off)
                ctx.Issue(SearchlightSystem.Order(own.Entity, SearchlightOrder.Off));
            return;
        }
        if (pick != 0 && !byStar)
        {
            if (order != SearchlightOrder.Illuminate || target != pick)
                ctx.Issue(SearchlightSystem.Order(own.Entity, SearchlightOrder.Illuminate, pick));
        }
        else if ((use == SearchlightUse.Search || held) && (Nearest(ctx, pic, own, out double rel) || held))
        {
            // swept either side of the nearest contact (the player's beams with none held: about her bow, rel 0); trained
            // round again only once she's drawn half the arc off its middle
            rel -= 360 * Math.Round(rel / 360);
            var (centre, half) = world.Searchlights.ArcOf(own.Entity);
            double off = rel - centre;
            if (order != SearchlightOrder.Search || half != s.SearchHalfDeg || Math.Abs(off - 360 * Math.Round(off / 360)) > s.SearchHalfDeg / 2)
                ctx.Issue(SearchlightSystem.Order(own.Entity, SearchlightOrder.Search, centreDeg: rel, halfDeg: s.SearchHalfDeg));
        }
        else if (order != SearchlightOrder.Ready)
            ctx.Issue(SearchlightSystem.Order(own.Entity, SearchlightOrder.Ready));
    }

    /// <summary>Star shell to find (docs/battle/night.md Q7): the nearest contact within its reach that his navy holds but
    /// doesn't see (her lights, her smoke, or lost from sight) or hasn't made out for friend or foe; 0 if none.</summary>
    static int ToFind(in AiContext ctx, Picture pic, ShipState own, double reachM)
    {
        int best = 0;
        double bestD = reachM;
        for (int i = 0; i < pic.Count; i++)
        {
            ref readonly var c = ref pic[i];
            if (c.Lost || c.InSight && c.Foe || c.TakenForFriend(ctx.NowMs, ctx.Settings))
                continue;
            double d = Earth.Distance(own.Position, c.Position + c.Velocity * ((ctx.NowMs - c.EstMs) / 1000.0));
            if (d < bestD)
                (best, bestD) = (c.Entity, d);
        }
        return best;
    }

    /// <summary>The nearest contact his navy holds (not seen going down), by its bearing off his bow, degrees.</summary>
    static bool Nearest(in AiContext ctx, Picture? pic, ShipState own, out double relativeDeg)
    {
        relativeDeg = 0;
        double best = double.MaxValue;
        for (int i = 0; pic != null && i < pic.Count; i++)
        {
            ref readonly var c = ref pic[i];
            if (c.Lost)
                continue;
            var q = c.Position + c.Velocity * ((ctx.NowMs - c.EstMs) / 1000.0);
            double d = (q - own.Position).LengthSquared;
            if (d < best)
                (best, relativeDeg) = (d, Earth.Bearing(own.Position, q) - own.HeadingDeg);
        }
        return best < double.MaxValue;
    }

    /// <summary>A light on her in the dark (docs/battle/night.md Q7), doctrine's WhenLit turning her away: once the others'
    /// beams and flares on her (<see cref="Lights.LitBy"/>) are <see cref="SimSettings.LitShare"/> times the night sky's
    /// light or more, she turns straight away from them and holds that course until <see cref="SimSettings.LitTurnAwayS"/>
    /// after she was last lit, her own lights out meanwhile (<see cref="NightLights"/>), her guns still firing; then her
    /// order's course again, as after a comb. Not while combing a torpedo's track, pressing a torpedo attack, or under the
    /// conn.</summary>
    void TurnFromLight(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        long now = ctx.NowMs;
        if (cap.Attacking || Conned(cap, own.Entity) || cap.HeldUntilMs > now && cap.HeldFor == CaptainReason.Combing ||
            (WhenLit)doctrine.Of(own.Entity, own.Side, DoctrineField.WhenLit) != WhenLit.TurnAway)
            return;
        var world = ctx.World;
        var s = ctx.Settings;
        var weather = world.Weather.At(own.Position, world.Time);
        var (lux, fromDeg) = world.Lights.LitBy(slot, own.Position, weather.VisibilityM);
        if (lux <= 0 || lux < s.LitShare * NightLight.At(own.Position, world.Time, weather.Cloud, s).Lux)
            return;
        double away = ((fromDeg + 180) % 360 + 360) % 360;
        if (cap.HeldUntilMs == 0)
            cap.HeldBackDeg = own.CourseOrderDeg;
        (cap.HeldDeg, cap.HeldUntilMs, cap.HeldFor) = (away, now + (long)(s.LitTurnAwayS * 1000), CaptainReason.Lit);
        if (cap.Reason == CaptainReason.None)
            cap.Reason = CaptainReason.Lit;
        Give(ctx, own, away, own.SpeedOrderKn);
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
