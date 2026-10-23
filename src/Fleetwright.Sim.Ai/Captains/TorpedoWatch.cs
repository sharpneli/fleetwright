using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>A ship's lookouts on torpedoes (docs/battle/command.md 5.1a, decision 10: captains try to evade them): the
/// era's torpedoes left a wake, seen from a ship within <see cref="SimSettings.TorpedoSeenM"/> by day and
/// <see cref="SimSettings.TorpedoSeenNightM"/> at night. One coming at her (its track passing within her half-length and
/// <see cref="SimSettings.TorpedoCombMarginM"/> of her, as she goes on, before its run ends) is a threat she combs: she
/// turns parallel to its track, bow or stern on, whichever turn is the smaller. What a ship's own lookouts see, not her
/// navy's picture (that holds ships; tracks go there when an admiral's turn-away needs them, 5.1b). The torpedoes go in a
/// grid once a think, so a ship's question is local. Not world state. Allocates only to grow.</summary>
public sealed class TorpedoWatch
{
    readonly CubeIndex grid = new();
    long builtMs = long.MinValue;
    int any;

    /// <summary>Sized for 256 torpedoes running, so the first spread of a fight doesn't grow it.</summary>
    public TorpedoWatch() => grid.Clear(256, 1);

    void Build(in AiContext ctx)
    {
        if (builtMs == ctx.NowMs)
            return;
        builtMs = ctx.NowMs;
        var torpedoes = ctx.World.Torpedoes;
        any = torpedoes.Running;
        if (any == 0)
            return;
        grid.Clear(torpedoes.Slots, ctx.Settings.TorpedoSeenM);
        for (int i = 0; i < torpedoes.Slots; i++)
            if (torpedoes[i].Alive)
                grid.Add(i, TorpedoSystem.Where(torpedoes[i], ctx.NowMs).Position);
    }

    /// <summary>The course that combs the first torpedo seen coming at her, how long until it's past her, s, and its
    /// bearing from her now (the side her lookouts call); false if none.</summary>
    public bool Threat(in AiContext ctx, ShipState own, out double combDeg, out double pastS, out double fromDeg)
    {
        (combDeg, pastS, fromDeg) = (0, 0, 0);
        Build(ctx);
        if (any == 0)
            return false;
        var s = ctx.Settings;
        var p = own.Position;
        double seen = Light.SunElevationDeg(p, ctx.World.Time) > s.TorpedoDayElevationDeg ? s.TorpedoSeenM : s.TorpedoSeenNightM;
        double reach = own.Handling.LengthM / 2 + s.TorpedoCombMarginM;
        var vs = own.Forward * own.Speed;
        var torpedoes = ctx.World.Torpedoes;
        double soonest = double.MaxValue;
        int n = grid.Near(p);
        foreach (int i in grid.Found(n))
        {
            ref readonly var t = ref torpedoes[i];
            if (!t.Alive)
                continue;
            var (q, f, left) = TorpedoSystem.Where(t, ctx.NowMs);
            var r = q - p;
            if (r.LengthSquared > seen * seen)
                continue;   // not seen
            var v = f * t.SpeedMs - vs;
            double vv = v.LengthSquared;
            if (vv < 1e-6)
                continue;
            double tc = -Vec3d.Dot(r, v) / vv;   // when it's nearest her, s
            if (tc <= 0 || tc * t.SpeedMs > left || (r + v * tc).Length > reach || tc >= soonest)
                continue;
            soonest = tc;
            double track = Earth.Bearing(q, q + f * 1000), turnAway = Turn(own.HeadingDeg, track), turnInto = Turn(own.HeadingDeg, track + 180);
            combDeg = Math.Abs(turnAway) <= Math.Abs(turnInto) ? track : (track + 180) % 360;
            pastS = tc;
            fromDeg = Earth.Bearing(p, q);
        }
        return soonest < double.MaxValue;
    }

    static double Turn(double from, double to)
    {
        double d = to - from;
        return d - 360 * Math.Round(d / 360);
    }
}
