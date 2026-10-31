using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>What a combat log line records.</summary>
public enum CombatEvent : byte
{
    /// <summary>A battery fired a salvo: Count guns.</summary>
    Fired,
    /// <summary>A shell hit a ship (Target): Count its shell type (the shooter's class's), and what it did (the damage
    /// system's line: the part, the outcome, the effects, the shell's kind and calibre).</summary>
    Hit,
    /// <summary>A salvo's fall straddled its target (Count shells), as it truly fell.</summary>
    Straddle,
    /// <summary>A ship (Target) going down: Count says how (a <see cref="LossKind"/>: its magazine, foundered, capsized,
    /// broken in two);
    /// Shooter and Battery hit it last.</summary>
    Lost,
    /// <summary>A launcher (Battery) fired Count torpedoes at Target (4.6).</summary>
    Torpedoes,
    /// <summary>A shell that missed burst close to a ship (Target) and did something to it (docs/battle/
    /// shells-armour-damage.md E7): as a hit's line, its source <see cref="HitSource.NearMiss"/>.</summary>
    NearMiss,
    /// <summary>A ship's (Target) damage control (docs/battle/damage-control.md DC8): Count says what (a
    /// <see cref="Fleetwright.Sim.Combat.DcEvent"/>), Part the part or room it was about (-1: none), Battery a number with
    /// it (boundaries that gave way, the steering it went to, the side whose counterflood tanks are full).</summary>
    DamageControl,
    /// <summary>A ship's (Target) people gave her up (docs/battle/striking.md SK1): Count her <see cref="GivenUp"/> now
    /// (her colours down, her men ordered off, her charges set); Shooter and Battery hit her last.</summary>
    GaveUp,
}

/// <summary>What a hit did, as the log tells it.</summary>
[Flags]
public enum HitEffect : uint
{
    None = 0,
    Destroyed = 1,
    Disabled = 2,
    Damaged = 4,
    /// <summary>Holed near or under the waterline.</summary>
    Holed = 8,
    Fire = 16,
    Jammed = 32,
    Magazine = 64,
    /// <summary>Its steering lost or jammed.</summary>
    Steering = 128,
    /// <summary>Its power cut.</summary>
    Power = 256,
    /// <summary>Its back broken (an underwater explosion's): it breaks in two.</summary>
    Broken = 512,
    /// <summary>A turret or barbette that held (docs/battle/shells-armour-damage.md E4): its mount stiff in train, a gun
    /// out, its sights out; the struck structure's crew stunned (a turret's, a conning tower's).</summary>
    Stiff = 1024,
    GunOut = 2048,
    Sights = 4096,
    Stunned = 8192,
    /// <summary>A belt that held leaked into the space behind it (E4), and fouled a boiler or engine room's feed.</summary>
    Leak = 16384,
    Feed = 32768,
    /// <summary>A plate that held shook what was near it (E4): gear out for a while (power, a director's gyro, a
    /// rangefinder); the whole ship's power cascading out.</summary>
    Shock = 65536,
    Blackout = 131072,
}

/// <summary>A line of the combat log: when, what, who fired (an entity) with which battery, at whom, and a count or a
/// shell type; a hit's line also what it did: the part it's told by (a node of the target's class: a component, then
/// the rooms; -1 for none), the outcome, its effects, the shell's kind and calibre (so the line reads after the
/// shooter is gone), and what struck it (a torpedo's hit: Battery is its launcher, the calibre the torpedo's).</summary>
public readonly record struct CombatEntry(long Ms, CombatEvent Kind, int Shooter, int Battery, int Target, int Count, int Part = -1,
    Outcome Outcome = default, HitEffect Effect = default, ShellKind Shell = default, float CalibreMm = 0, HitSource Source = HitSource.Shell);

/// <summary>The world's record of the fighting (docs/world/architecture.md: "who fired first and what each hit did";
/// docs/battle/fighting.md 4.2b): a ring of plain lines, the newest kept. Encounters, missions and the damage report
/// read it later. World state: hashed once anything is written.</summary>
public sealed class CombatLog(int capacity)
{
    readonly CombatEntry[] ring = new CombatEntry[Math.Max(capacity, 16)];
    int next;

    /// <summary>Lines written since the world began (the ring keeps the newest).</summary>
    public long Written { get; private set; }

    public int Count => (int)Math.Min(Written, ring.Length);

    /// <summary>The i-th newest line (0 the newest).</summary>
    public ref readonly CombatEntry Newest(int i) => ref ring[(next - 1 - i + 2 * ring.Length) % ring.Length];

    public void Add(in CombatEntry e)
    {
        ring[next] = e;
        next = (next + 1) % ring.Length;
        Written++;
    }

    public void Hash(ref StateHash h)
    {
        if (Written == 0)
            return;
        h.Add(Written);
        for (int i = 0; i < Count; i++)
        {
            ref readonly var e = ref Newest(i);
            h.Add(e.Ms);
            h.Add((int)e.Kind);
            h.Add(e.Shooter);
            h.Add(e.Target);
            h.Add(e.Count);
            if (e.Kind == CombatEvent.Hit)
            {
                h.Add(e.Part);
                h.Add((int)e.Outcome | (int)((uint)e.Effect & 0xFFFF) << 8 | (int)e.Source << 24);
                if ((uint)e.Effect > 0xFFFF)
                    h.Add((int)((uint)e.Effect >> 16));   // E4's shock and on, beside the first sixteen (so logs before hash as they did)
            }
        }
    }

    /// <summary>The newest lines into a snapshot's part (up to its room); allocates only to grow it.</summary>
    public void Fill(CombatLogView v, int most)
    {
        int n = Math.Min(Count, most);
        v.EnsureCapacity(n);
        for (int i = 0; i < n; i++)
            v.Entries[i] = Newest(i);
        v.Count = n;
        v.Written = Written;
    }
}
