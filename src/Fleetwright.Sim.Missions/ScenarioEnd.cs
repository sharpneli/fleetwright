using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Missions;

/// <summary>Why a scenario ended (docs/battle/command.md "The end").</summary>
public enum EndReason : byte
{
    /// <summary>Not over.</summary>
    None,

    /// <summary>Its time ran out (<c>"until"</c>, or <see cref="SimSettings.PlayLimitMs"/> after the start).</summary>
    Time,

    /// <summary>Decided: every side still afloat has its aims (v0's destroy everyone: one side, or sides at peace, left).</summary>
    Decided,

    /// <summary>Quiet: the sides met, and no encounter has been open for <see cref="SimSettings.QuietEndMs"/>.</summary>
    Quiet,
}

/// <summary>What a line of a scenario's timeline tells.</summary>
public enum PlayEvent : byte
{
    /// <summary>The first encounter opened.</summary>
    Met,

    /// <summary>A side's first sight of an enemy (Entity the ship seen).</summary>
    Sighted,

    /// <summary>A side's first salvo (Entity the shooter, Other the target).</summary>
    Fired,

    /// <summary>A side's first hit (Entity the shooter, Other the ship hit).</summary>
    Hit,

    /// <summary>A side's first torpedoes (Entity the shooter, Other the target, Count how many).</summary>
    Torpedoes,

    /// <summary>A ship going down (Entity the ship, Other the ship that hit it last, Count its <see cref="LossKind"/>).</summary>
    Lost,

    /// <summary>The end (Count its <see cref="EndReason"/>).</summary>
    End,
}

/// <summary>A line of the timeline: when, what, whose side, and the ships it's about.</summary>
public readonly record struct PlayLine(long Ms, PlayEvent Kind, int Side, int Entity = 0, int Other = 0, int Count = 0);

/// <summary>How a side came out: the facts, not a grade (grading is the missions', docs/battle/plan.md 6.1).</summary>
public readonly record struct SideResult(string Name, int Ships, double Tonnes, int Lost, double LostTonnes, int Damaged, int Afloat, bool AimsMet);

/// <summary>The judge of a scenario's end (docs/battle/command.md "The end", step 5.0): whether it's over and why, its
/// timeline and its result, read from the world and its combat log. Not world state: the world never ends, only a
/// scenario does. It's asked at whole game minutes (<see cref="Check"/>), by the headless runner now and the app and the
/// missions later, so it says the same at the same times on every run and replay. Its lines and its first look at each
/// ship allocate (once each); a check otherwise doesn't.</summary>
public sealed class ScenarioEnd
{
    readonly Scenario scenario;
    readonly World world;
    readonly long untilMs;
    readonly Aim[][] aims;   // by side
    readonly Dictionary<int, (byte Side, double Tonnes)> ships = [];   // every ship seen, kept after it's gone
    readonly List<PlayLine> lines = [];
    readonly bool[] sighted, fired, hit, torpedoes;   // by side: its first already told
    readonly int[] afloat;   // by side, at the last check
    long logRead, lastBusyMs = long.MinValue;
    bool met;

    public ScenarioEnd(Scenario s, World w)
    {
        scenario = s;
        world = w;
        untilMs = s.Until?.Ms ?? s.Start.Ms + w.Settings.PlayLimitMs;
        int n = s.Sides.Length;
        aims = [.. Enumerable.Range(0, n).Select(side => s.AimsOf(side).ToArray())];
        (sighted, fired, hit, torpedoes, afloat) = (new bool[n], new bool[n], new bool[n], new bool[n], new int[n]);
        logRead = w.CombatLog.Written;
    }

    public bool Over => Reason != EndReason.None;
    public EndReason Reason { get; private set; }
    public long EndMs { get; private set; }
    public IReadOnlyList<PlayLine> Timeline => lines;

    /// <summary>The combat log's lines that went by between two checks unread (its ring is that much too small).</summary>
    public long Missed { get; private set; }

    /// <summary>The side a ship is on, as it was when it was seen (it may be gone), or -1.</summary>
    public int SideOf(int entity) => ships.TryGetValue(entity, out var x) ? x.Side : -1;

    /// <summary>Looks at the world now: what happened since the last check, and whether it's over. Call it at whole game
    /// minutes (or any fixed grid of world time), never at a host's whim, so every run says the same.</summary>
    public bool Check()
    {
        if (Over)
            return true;
        long now = world.Time.Ms;
        var store = world.Ships.Store;
        Array.Clear(afloat);
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot))
                continue;
            var x = store[slot];
            if (!ships.ContainsKey(x.Entity))
                ships[x.Entity] = (x.Side, x.Handling.Class.Hydrostatics.DisplacementT);
            if (Aims.Afloat(store, slot) && x.Side < afloat.Length)
                afloat[x.Side]++;
        }
        ReadLog();
        Sightings();
        if (world.Encounters.Count > 0)
        {
            if (!met)
            {
                met = true;
                long first = world.Encounters.Open.Min(e => e.OpenedMs);
                lines.Add(new PlayLine(first, PlayEvent.Met, -1));
            }
            lastBusyMs = now;
        }

        if (now >= untilMs)
            End(EndReason.Time, now);
        else if (Decided())
            End(EndReason.Decided, now);
        else if (met && world.Encounters.Count == 0 && now - lastBusyMs >= world.Settings.QuietEndMs)
            End(EndReason.Quiet, now);
        return Over;
    }

    /// <summary>Every side still afloat has met its aims: nothing is left to be fought for.</summary>
    bool Decided()
    {
        bool any = false;
        for (int side = 0; side < afloat.Length; side++)
        {
            if (afloat[side] == 0)
                continue;
            any = true;
            foreach (var a in aims[side])
                if (!Aims.Met(world, side, a))
                    return false;
        }
        return any;
    }

    void End(EndReason why, long now)
    {
        (Reason, EndMs) = (why, now);
        lines.Add(new PlayLine(now, PlayEvent.End, -1, Count: (int)why));
    }

    void ReadLog()
    {
        var log = world.CombatLog;
        long fresh = log.Written - logRead;
        Missed += Math.Max(0, fresh - log.Count);
        for (long i = Math.Min(fresh, log.Count) - 1; i >= 0; i--)
        {
            ref readonly var e = ref log.Newest((int)i);
            int side = SideOf(e.Kind == CombatEvent.Lost ? e.Target : e.Shooter);
            switch (e.Kind)
            {
                case CombatEvent.Fired:
                    First(fired, side, new PlayLine(e.Ms, PlayEvent.Fired, side, e.Shooter, e.Target, e.Count));
                    break;
                case CombatEvent.Hit:
                    First(hit, side, new PlayLine(e.Ms, PlayEvent.Hit, side, e.Shooter, e.Target));
                    break;
                case CombatEvent.Torpedoes:
                    First(torpedoes, side, new PlayLine(e.Ms, PlayEvent.Torpedoes, side, e.Shooter, e.Target, e.Count));
                    break;
                case CombatEvent.Lost:
                    lines.Add(new PlayLine(e.Ms, PlayEvent.Lost, side, e.Target, e.Shooter, e.Count));
                    break;
            }
        }
        logRead = log.Written;
    }

    void First(bool[] told, int side, PlayLine line)
    {
        if (side < 0 || side >= told.Length || told[side])
            return;
        told[side] = true;
        lines.Add(line);
    }

    /// <summary>Each side's first sight of an enemy, at the moment its picture first held one.</summary>
    void Sightings()
    {
        for (int side = 0; side < sighted.Length; side++)
        {
            if (sighted[side] || world.Sightings.Of(side) is not { Count: > 0 } pic)
                continue;
            int best = 0;
            for (int i = 1; i < pic.Count; i++)
                if (pic[i].FirstMs < pic[best].FirstMs)
                    best = i;
            First(sighted, side, new PlayLine(pic[best].FirstMs, PlayEvent.Sighted, side, pic[best].Entity));
        }
    }

    /// <summary>Each side as it stands now: its ships and tonnage at sea since the start, those lost (going down counts),
    /// those damaged and still afloat, and whether its aims are met.</summary>
    public SideResult[] Result()
    {
        var store = world.Ships.Store;
        var r = new SideResult[scenario.Sides.Length];
        for (int side = 0; side < r.Length; side++)
        {
            int count = 0, lost = 0, damaged = 0, up = 0;
            double t = 0, lostT = 0;
            foreach (var (entity, (s, tonnes)) in ships)
            {
                if (s != side)
                    continue;
                count++;
                t += tonnes;
                int slot = store.SlotOf(entity);
                if (slot >= 0 && Aims.Afloat(store, slot))
                {
                    up++;
                    if (store.Combat.Condition(slot) is { Damaged: true })
                        damaged++;
                }
                else
                {
                    lost++;
                    lostT += tonnes;
                }
            }
            bool met = aims[side].All(a => Aims.Met(world, side, a)) && up > 0;
            r[side] = new SideResult(scenario.Sides[side], count, t, lost, lostT, damaged, up, met);
        }
        return r;
    }
}
