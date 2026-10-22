using Fleetwright.Sim;

namespace Fleetwright.Map;

/// <summary>The combat log as the screens keep it (docs/map-ui/battle-hud.md U3b): every line the snapshots brought,
/// read once a frame by the log's count of lines ever written, oldest first; and each ship's hits, near misses and loss
/// by her entity (the ship card's "hits taken"). The snapshot carries the newest lines only
/// (<see cref="GunnerySystem.CombatLogShown"/>): a frame whose sim time wrote more loses the oldest of them, which
/// <see cref="Lost"/> counts. A new world starts it again. Grows with the fight; nothing per frame.</summary>
public sealed class CombatFeed
{
    readonly List<CombatEntry> lines = new(4096);
    readonly Dictionary<int, List<int>> hitsOn = [];
    long seen;
    int generation = -1;

    /// <summary>Lines the snapshots dropped before this feed read them.</summary>
    public long Lost { get; private set; }

    public IReadOnlyList<CombatEntry> Lines => lines;

    /// <summary>Takes the snapshot's new lines (a new world first clears what's kept).</summary>
    public void Read(Snapshot s, int worldGeneration)
    {
        var log = s.CombatLog;
        if (worldGeneration != generation || log.Written < seen)
        {
            generation = worldGeneration;
            lines.Clear();
            hitsOn.Clear();
            (seen, Lost) = (0, 0);
        }
        long fresh = log.Written - seen;
        if (fresh <= 0)
            return;
        int take = (int)Math.Min(fresh, log.Count);
        Lost += fresh - take;
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
        seen = log.Written;
    }

    /// <summary>A ship's hits, near misses and loss, as indices into <see cref="Lines"/>, oldest first (empty: none).</summary>
    public IReadOnlyList<int> HitsOn(int entity) => hitsOn.TryGetValue(entity, out var l) ? l : [];
}
