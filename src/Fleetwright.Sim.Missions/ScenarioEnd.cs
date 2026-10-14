using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Tasks;
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

    /// <summary>Every side done: each side still afloat has its aims, or its commander has given them up (gone home, or
    /// broken off) and every ship of it afloat is home (docs/battle/command.md "The end", 2).</summary>
    Done,
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

    /// <summary>A ship fallen out of the fight (Entity the ship, Count its captain's <see cref="CaptainReason"/>).</summary>
    FellOut,

    /// <summary>A force deploying into its line of battle (Entity its group id).</summary>
    Deployed,

    /// <summary>A task's report (Entity the unit's id, a ship or a negated group id; Other its kind × 256 + its reason;
    /// Count its state): in contact, done, can't.</summary>
    Task,

    /// <summary>A side's commander sees the battle anew (Count his <see cref="CommanderPhase"/>, Other his
    /// <see cref="CommanderReason"/>).</summary>
    Phase,

    /// <summary>A side's commander gives a unit a task (Entity the unit's id; Other its kind × 256 + his
    /// <see cref="CommanderWhy"/>; Count what it's about: a unit's id, a contact's entity, or 0 for a point).</summary>
    Ordered,

    /// <summary>The end (Count its <see cref="EndReason"/>).</summary>
    End,
}

/// <summary>A line of the timeline: when, what, whose side, and the ships it's about.</summary>
public readonly record struct PlayLine(long Ms, PlayEvent Kind, int Side, int Entity = 0, int Other = 0, int Count = 0);

/// <summary>How a side came out: the facts, not a grade (grading is the missions', docs/battle/plan.md 6.1).</summary>
public readonly record struct SideResult(string Name, int Ships, double Tonnes, int Lost, double LostTonnes, int Damaged, int Afloat, bool AimsMet, int Hits = 0, int Torpedoes = 0);

/// <summary>The judge of a scenario's end (docs/battle/command.md "The end", step 5.0): whether it's over and why, its
/// timeline and its result, read from the world and its combat log. Not world state: the world never ends, only a
/// scenario does. It's asked at whole game minutes (<see cref="Check"/>), by the headless runner now and the app and the
/// missions later, so it says the same at the same times on every run and replay. Its lines and its first look at each
/// ship allocate (once each); a check otherwise doesn't.</summary>
public sealed class ScenarioEnd
{
    readonly Scenario scenario;
    readonly World world;
    readonly AiSystem? ai;
    readonly HashSet<int> fellOut = [];   // ships whose falling out is told
    readonly HashSet<int> deployed = [];  // groups whose deployment is told
    readonly Dictionary<int, byte> taskStates = [];   // the state each unit's task was last told in
    readonly Dictionary<int, long> ordersTold = [];   // when each unit's last commander's order was given, as told
    readonly int[] phases = new int[256];             // each side's commander's phase and reason as last told
    readonly Dictionary<int, string> groupNames = [];

    /// <summary>A group's name as it was when the timeline told of it.</summary>
    public string GroupName(int groupId) => groupNames.TryGetValue(groupId, out var n) ? n : $"group {groupId}";
    readonly long untilMs;
    readonly Aim[][] aims;   // by side
    readonly Dictionary<int, (byte Side, double Tonnes)> ships = [];   // every ship seen, kept after it's gone
    readonly List<PlayLine> lines = [];
    readonly bool[] sighted, fired, hit, torpedoes;   // by side: its first already told
    readonly int[] afloat;   // by side, at the last check
    readonly int[] hits, torpedoesFired;   // by side: the hits it has made, the torpedoes it has fired
    long logRead, lastBusyMs = long.MinValue;
    bool met;

    /// <param name="ai">The AI, whose captains' falling out goes on the timeline; none for a world without one.</param>
    public ScenarioEnd(Scenario s, World w, AiSystem? ai = null)
    {
        scenario = s;
        world = w;
        this.ai = ai;
        untilMs = s.Until?.Ms ?? s.Start.Ms + w.Settings.PlayLimitMs;
        int n = s.Sides.Length;
        aims = [.. Enumerable.Range(0, n).Select(side => s.AimsOf(side).ToArray())];
        (sighted, fired, hit, torpedoes, afloat, hits, torpedoesFired) = (new bool[n], new bool[n], new bool[n], new bool[n], new int[n], new int[n], new int[n]);
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
            {
                afloat[x.Side]++;
                if (ai?.Captains.ReasonOf(x.Entity) is { } why && why is >= CaptainReason.OutSpeed and <= CaptainReason.OutAmmunition && fellOut.Add(x.Entity))
                    lines.Add(new PlayLine(now, PlayEvent.FellOut, x.Side, x.Entity, Count: (int)why));
            }
        }
        ReadLog();
        Sightings();
        if (ai != null)
            CommandEvents(now);
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
        else if (ai != null && AllDone())
            End(EndReason.Done, now);
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

    /// <summary>Every side still afloat is done: its aims met, or given up with every ship of it afloat home.</summary>
    bool AllDone()
    {
        var store = world.Ships.Store;
        double home = world.Settings.HomeRadiusM;
        for (int side = 0; side < afloat.Length; side++)
        {
            if (afloat[side] == 0 || aims[side].All(a => Aims.Met(world, side, a)))
                continue;
            var (phase, reason) = ai!.Commanders.PhaseOf(side);
            if (!(phase == CommanderPhase.Home || reason == CommanderReason.BrokenOff) || ai.HomeOf(side) is not { } at)
                return false;
            for (int slot = 0; slot < store.Slots; slot++)
                if (store.IsAlive(slot) && store[slot].Side == side && Aims.Afloat(store, slot) && Earth.Distance(store[slot].Position, at) > home)
                    return false;
        }
        return true;
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
                    if (side >= 0 && side < hits.Length)
                        hits[side]++;
                    break;
                case CombatEvent.Torpedoes:
                    First(torpedoes, side, new PlayLine(e.Ms, PlayEvent.Torpedoes, side, e.Shooter, e.Target, e.Count));
                    if (side >= 0 && side < torpedoesFired.Length)
                        torpedoesFired[side] += e.Count;
                    break;
                case CombatEvent.Lost:
                    lines.Add(new PlayLine(e.Ms, PlayEvent.Lost, side, e.Target, e.Shooter, e.Count));
                    break;
            }
        }
        logRead = log.Written;
    }

    /// <summary>What the command did since the last check (docs/battle/command.md 5.1c): forces deploying, and tasks
    /// reporting in contact, done or can't.</summary>
    void CommandEvents(long now)
    {
        var groups = world.Groups;
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            if (ai!.Admirals.Deploying(g.GroupId) && deployed.Add(g.GroupId))
            {
                groupNames[g.GroupId] = g.Name;
                lines.Add(new PlayLine(now, PlayEvent.Deployed, groups.SideOf(g), g.GroupId));
            }
        }
        for (int i = 0; i < ai!.Tasks.Count; i++)
        {
            var (unit, kind, state, why) = ai.Tasks.At(i);
            if (state is not (TaskState.InContact or TaskState.Done or TaskState.Cant) || taskStates.TryGetValue(unit.Id, out byte told) && told == (byte)state)
                continue;
            taskStates[unit.Id] = (byte)state;
            int side = -1;
            if (unit.IsGroup && groups.Get(unit.GroupId) is { } g)
                (side, groupNames[g.GroupId]) = (groups.SideOf(g), g.Name);
            else if (world.Ships.Store.SlotOf(unit.Entity) is int slot and >= 0)
                side = world.Ships.Store[slot].Side;
            lines.Add(new PlayLine(now, PlayEvent.Task, side, unit.Id, (int)kind * 256 + (int)why, (int)state));
        }
        var cmd = ai.Commanders;
        for (int side = 0; side < scenario.Sides.Length; side++)
        {
            var (phase, reason) = cmd.PhaseOf(side);
            int key = (int)phase * 256 + (int)reason;
            if (phase != CommanderPhase.None && phases[side] != key)
            {
                phases[side] = key;
                lines.Add(new PlayLine(now, PlayEvent.Phase, side, Other: (int)reason, Count: (int)phase));
            }
        }
        for (int i = 0; i < cmd.Count; i++)
        {
            var (unit, given, target, why, ms) = cmd.At(i);
            if (given == TaskKind.None || ordersTold.TryGetValue(unit.Id, out long told) && told == ms)
                continue;
            ordersTold[unit.Id] = ms;
            int side = -1;
            if (unit.IsGroup && groups.Get(unit.GroupId) is { } g)
                (side, groupNames[g.GroupId]) = (groups.SideOf(g), g.Name);
            else if (world.Ships.Store.SlotOf(unit.Entity) is int slot and >= 0)
                side = world.Ships.Store[slot].Side;
            if (target < 0 && groups.Get(-target) is { } tg)
                groupNames[tg.GroupId] = tg.Name;
            lines.Add(new PlayLine(ms, PlayEvent.Ordered, side, unit.Id, (int)given * 256 + (int)why, target));
        }
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
            r[side] = new SideResult(scenario.Sides[side], count, t, lost, lostT, damaged, up, met, hits[side], torpedoesFired[side]);
        }
        return r;
    }
}
