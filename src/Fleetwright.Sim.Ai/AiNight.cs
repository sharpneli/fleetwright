using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>Whether it's night to the AI at a place and a time (doctrine's NightAction and NightAttack, night's close order;
/// docs/battle/night.md Q12d): by the light, the sky there darker than <see cref="SimSettings.NightSkyCdM2"/> under the
/// weather's cloud, never by the clock. An overcast evening's night falls sooner; a misty day stays day (the mist reaches
/// the AI through what it sees and its matchups).</summary>
static class AiNight
{
    public static bool At(in AiContext ctx, Vec3d at, WorldTime time) =>
        NightLight.At(at, time, ctx.World.Weather.At(at, time).Cloud, ctx.Settings).SkyCdM2 < ctx.Settings.NightSkyCdM2;
}
