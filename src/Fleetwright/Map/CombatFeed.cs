using Fleetwright.Sim;

namespace Fleetwright.Map;

/// <summary>The combat log as the screens keep it (docs/map-ui/battle-hud.md U3b): every line the snapshots brought (the
/// lines the player may know, <see cref="CombatLogView"/>), read once a frame by their place in the whole log, oldest
/// first; and each ship's hits, near misses and loss by her entity (the ship card's "hits taken"). The snapshot carries
/// the newest lines only (<see cref="GunnerySystem.CombatLogShown"/>): a frame whose sim time wrote more may lose some,
/// which <see cref="Lost"/> counts when the oldest it brought is past the last read. A new world starts it again. Grows
/// with the fight; nothing per frame.</summary>
public sealed class CombatFeed
{
    readonly List<CombatEntry> lines = new(4096);
    readonly Dictionary<int, List<int>> hitsOn = [];
    long seen = -1;   // the place in the whole log of the newest line read
    int generation = -1;

    /// <summary>Lines the snapshots dropped before this feed read them.</summary>
    public long Lost { get; private set; }

    public IReadOnlyList<CombatEntry> Lines => lines;

    /// <summary>Takes the snapshot's new lines (a new world first clears what's kept).</summary>
    public void Read(Snapshot s, int worldGeneration)
    {
        var log = s.CombatLog;
        if (worldGeneration != generation || log.Written - 1 < seen)
        {
            generation = worldGeneration;
            lines.Clear();
            hitsOn.Clear();
            (seen, Lost) = (-1, 0);
        }
        int take = 0;
        while (take < log.Count && log.Seq[take] > seen)
            take++;
        if (take == 0)
            return;
        if (take == log.Count && log.Written - 1 - seen > take && log.Seq[take - 1] > seen + 1)
            Lost++;   // lines beyond what the snapshot held: some may have been ours
        for (int i = take - 1; i >= 0; i--)   // the log's newest first: oldest new line first
        {
            ref readonly var e = ref log.Entries[i];
            lines.Add(e);
            if (e.Kind is CombatEvent.Hit or CombatEvent.NearMiss or CombatEvent.Lost)
            {
                if (!hitsOn.TryGetValue(e.Target, out var list))
                    hitsOn[e.Target] = list = [];
                list.Add(lines.Count - 1);
            }
        }
        seen = log.Seq[0];
    }

    /// <summary>A ship's hits, near misses and loss, as indices into <see cref="Lines"/>, oldest first (empty: none).</summary>
    public IReadOnlyList<int> HitsOn(int entity) => hitsOn.TryGetValue(entity, out var l) ? l : [];
}
