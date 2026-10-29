using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>A ship's lookouts on torpedoes (docs/battle/command.md 5.1a, decision 10: captains try to evade them): the
/// era's torpedoes left a wake, seen from a ship within <see cref="SimSettings.TorpedoSeenM"/> by day and less in the dark
/// (docs/battle/night.md Q1: the eye's law in the night's light). One coming at her (its track passing within her half-length and
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
        double seen = SeenM(ctx, p);
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

    readonly CubeIndex watchedAt = new();

    /// <summary>The coarse tier's lookouts (docs/battle/coarse.md J3): a coarse captain thinks once a minute, so the first
    /// moment before <paramref name="untilMs"/> that a running torpedo comes within sight of one of
    /// <paramref name="watched"/> (slots) is worked out along both tracks, the torpedo's run and the ship's segment (straight
    /// on from its end), to wake her captain then for the same test (<see cref="Threat"/>); long.MaxValue if none. A track
    /// in sight already is the think's. Sampled every <see cref="SampleMs"/>, then halved to <see cref="EdgeMs"/>.</summary>
    public long NextSeen(in AiContext ctx, List<int> watched, long untilMs)
    {
        var torpedoes = ctx.World.Torpedoes;
        long now = ctx.NowMs;
        if (torpedoes.Running == 0 || watched.Count == 0 || untilMs <= now)
            return long.MaxValue;
        var s = ctx.Settings;
        var store = ctx.Ships;
        double span = (untilMs - now) / 1000.0, fastest = 0, torpedo = 0;
        foreach (int slot in watched)
            fastest = Math.Max(fastest, Math.Abs(store[slot].Speed));
        for (int i = 0; i < torpedoes.Slots; i++)
            if (torpedoes[i].Alive)
                torpedo = Math.Max(torpedo, torpedoes[i].SpeedMs);
        watchedAt.Clear(store.Slots, s.TorpedoSeenM + (fastest + torpedo) * span);
        foreach (int slot in watched)
            watchedAt.Add(slot, store[slot].Position);
        long first = long.MaxValue;
        for (int i = 0; i < torpedoes.Slots; i++)
        {
            ref readonly var t = ref torpedoes[i];
            if (!t.Alive)
                continue;
            long end = Math.Min(untilMs, Math.Min(first, t.LaunchMs + (long)(t.EndS * 1000)));
            if (end <= now)
                continue;
            int n = watchedAt.Near(TorpedoSystem.Where(t, now).Position);
            foreach (int slot in watchedAt.Found(n))
            {
                var p = store[slot].Position;
                double seen = SeenM(ctx, p);
                long at = Entry(store, slot, t, now, end, seen * seen);
                if (at < first)
                    (first, end) = (at, at);
            }
        }
        return first;
    }

    /// <summary>How far a track is seen from a place now, m: <see cref="SimSettings.TorpedoSeenM"/> by day, less in the
    /// dark (docs/battle/night.md Q1: <see cref="SightModel.TrackSeenM"/> in the night's light there).</summary>
    static double SeenM(in AiContext ctx, Vec3d p)
    {
        var s = ctx.Settings;
        double cloud = ctx.World.Weather.At(p, ctx.World.Time).Cloud;
        return SightModel.TrackSeenM(s.TorpedoSeenM, NightLight.At(p, ctx.World.Time, cloud, s).SkyCdM2, s);
    }

    const long SampleMs = 2_000, EdgeMs = 50;

    /// <summary>When a torpedo first comes within sight (squared, m²) of a ship between two moments, ms; long.MaxValue if it
    /// doesn't, or is in sight from the start.</summary>
    static long Entry(ShipStore store, int slot, in TorpedoSystem.Torpedo t, long from, long to, double seen2)
    {
        if (Apart2(store, slot, t, from) <= seen2)
            return long.MaxValue;
        for (long lo = from; lo < to;)
        {
            long hi = Math.Min(lo + SampleMs, to);
            if (Apart2(store, slot, t, hi) <= seen2)
            {
                while (hi - lo > EdgeMs)
                {
                    long mid = (lo + hi) / 2;
                    if (Apart2(store, slot, t, mid) <= seen2)
                        hi = mid;
                    else
                        lo = mid;
                }
                return hi;
            }
            lo = hi;
        }
        return long.MaxValue;
    }

    static double Apart2(ShipStore store, int slot, in TorpedoSystem.Torpedo t, long ms) => (TorpedoSystem.Where(t, ms).Position - ShipAt(store, slot, ms)).LengthSquared;

    /// <summary>Where a coarse ship will be: on her segment while it lasts, straight on at its end's speed after (her next
    /// segment isn't laid yet; a wrong guess only moves the wake, which tests her as she is).</summary>
    static Vec3d ShipAt(ShipStore store, int slot, long ms)
    {
        long end = store.SegmentEndMs(slot);
        if (ms <= end)
        {
            store.PoseAt(slot, ms, out var p, out _);
            return p;
        }
        store.PoseAt(slot, end, out var q, out var f);
        Earth.Advance(ref q, ref f, store.SpeedAt(slot, end) * (ms - end) / 1000.0);
        return q;
    }

    static double Turn(double from, double to)
    {
        double d = to - from;
        return d - 360 * Math.Round(d / 360);
    }
}
