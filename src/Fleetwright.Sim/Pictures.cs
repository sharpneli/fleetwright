using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>How far a contact has been made out: a light in the dark (docs/battle/night.md Q2: a salvo's flash, a funnel's
/// glow; something's there), smoke on the horizon (docs/battle/smoke.md Z6: something's there, its ships not yet seen), a
/// ship, its type, its class (perhaps wrongly), its name.</summary>
public enum ContactLevel : byte
{
    Light,
    Smoke,
    Ship,
    Type,
    Class,
    Name,
}

/// <summary>What a ship is reported as at <see cref="ContactLevel.Type"/>.</summary>
public enum ShipType : byte
{
    Unknown,
    Capital,
    Cruiser,
    Destroyer,
    Small,
    Merchant,
}

/// <summary>What an observer makes out of a ship's condition (docs/battle/fighting.md 4.3b: the enemy sees only what can
/// be seen): low in the water, a list, trimmed by the bow or the stern, fires, stopped, its guns silent (a damaged ship not
/// seen firing for a while: docs/battle/crew.md N5b), going under; and seen to go. Whether it's done for is the
/// observer's judgement (<see cref="Contact.DoneFor"/>).</summary>
[Flags]
public enum SeenFlags : ushort
{
    None = 0,
    Low = 1,
    Listing = 2,
    ByBow = 4,
    ByStern = 8,
    Burning = 16,
    Stopped = 32,
    Silent = 64,
    GoingDown = 128,
    Gone = 256,
}

/// <summary>A ship as a navy holds it (docs/world/architecture.md "Knowledge"; docs/battle/plan.md step 3.2d): keyed on
/// the true ship (agreed 2026-10-13: no false merges or splits in the battle module), her smoke's too (docs/battle/smoke.md
/// Z6); first and last seen (she or her smoke), and whether she or her smoke was in sight at the last look; the estimate
/// (position and velocity at <see cref="EstMs"/>, with its covariance); how far it's been made out and what it's taken for (perhaps wrongly); the last report's source; and what was made out of its
/// condition at the last sighting (<see cref="Seen"/>).</summary>
public struct Contact
{
    public int Entity;
    public long FirstMs, SeenMs, EstMs;

    /// <summary>When a look was last made for it (seen or not).</summary>
    public long LookedMs;

    /// <summary>Whether the ship is in sight; whether her smoke was at the last look while she wasn't (docs/battle/smoke.md
    /// Z6: a fix on the smoke keeps her track, <see cref="SeenMs"/> its time); whether her lights were (docs/battle/night.md
    /// Q2: her salvo's flash, her funnels' glow).</summary>
    public bool InSight, SmokeInSight, LightInSight;
    public Vec3d Position, Velocity;
    public Cov4 P;
    public ContactLevel Level;
    public ShipType Type;

    /// <summary>The class it's taken for (a registry id), -1 below <see cref="ContactLevel.Class"/>.</summary>
    public int TakenFor;
    public int Observer;
    public EyeKind Eye;

    /// <summary>Where the observer was at the last fix (the map's bearing line for smoke).</summary>
    public Vec3d From;

    /// <summary>The ship's side (a navy knows it once it's made out the type: its colours, its build).</summary>
    public byte Side;

    /// <summary>What was made out of its condition when last seen, and whether it was seen to go.</summary>
    public SeenFlags Seen;

    /// <summary>Since when her guns have been quiet as her navy has watched her (world ms; docs/battle/crew.md N5b): a salvo
    /// of hers seen, or her coming into sight, starts it again. Silent is read from it.</summary>
    public long QuietMs;

    /// <summary>Seen going down or gone: no target, no threat (docs/battle/fighting.md 4.3b).</summary>
    public readonly bool Lost => (Seen & (SeenFlags.GoingDown | SeenFlags.Gone)) != 0;

    /// <summary>How done for she looks, 0-1 (docs/battle/crew.md N5b; fire-control.md F10 "ships seen done for"): burning
    /// and silent as seen, by how long her guns have been quiet past <see cref="SimSettings.SeenSilentS"/> (over as long
    /// again) and how near stopped her estimated speed is (none at <see cref="SimSettings.DoneForKn"/>); 0 if she isn't
    /// both burning and silent. Never her water or her men.</summary>
    public readonly double DoneFor(long now, SimSettings s)
    {
        if ((Seen & (SeenFlags.Burning | SeenFlags.Silent)) != (SeenFlags.Burning | SeenFlags.Silent))
            return 0;
        double quiet = Math.Clamp(((now - QuietMs) / 1000.0 - s.SeenSilentS) / s.SeenSilentS, 0, 1);
        double kn = Velocity.Length / Handling.MsPerKnot;
        return quiet * Math.Clamp(1 - kn / s.DoneForKn, 0, 1);
    }
}

/// <summary>One navy's picture: its contacts, in slots, by entity through an index. World state: deterministic, saved
/// and hashed. A navy knows at once what any of its ships sees (agreed 2026-10-13); each contact keeps when it was seen and
/// from where, so signals' delays (step 5.1) slot in later.</summary>
public sealed class Picture
{
    Contact[] contacts = new Contact[16];
    readonly Dictionary<int, int> index = new(16);

    public byte Side { get; }
    public int Count { get; private set; }

    internal Picture(byte side) => Side = side;

    public ref readonly Contact this[int i] => ref contacts[i];

    internal ref Contact At(int i) => ref contacts[i];

    /// <summary>The slot of the contact on a ship, or -1.</summary>
    public int IndexOf(int entity) => index.TryGetValue(entity, out int i) ? i : -1;

    /// <summary>Whether it holds a ship and sees it going down or gone (<see cref="Contact.Lost"/>).</summary>
    public bool Lost(int entity) => IndexOf(entity) is int i and >= 0 && contacts[i].Lost;

    /// <summary>Room for this many contacts (every ship at sea): a picture grows when the fleets do, not when a contact
    /// comes in (a light seen far, docs/battle/night.md Q2, makes many more), so a look never allocates.</summary>
    internal void Reserve(int n)
    {
        if (contacts.Length < n)
            Array.Resize(ref contacts, Math.Max(n, 2 * contacts.Length));
        index.EnsureCapacity(n);
    }

    internal ref Contact Add(int entity)
    {
        if (Count == contacts.Length)
            Array.Resize(ref contacts, Count * 2);
        index[entity] = Count;
        contacts[Count] = new Contact { Entity = entity, TakenFor = -1 };
        return ref contacts[Count++];
    }

    internal void RemoveAt(int i)
    {
        index.Remove(contacts[i].Entity);
        int last = --Count;
        if (i != last)
        {
            contacts[i] = contacts[last];
            index[contacts[i].Entity] = i;
        }
        contacts[last] = default;
    }

    /// <summary>A contact's estimate carried on to a time: dead reckoned along its velocity on the sphere, its
    /// covariance grown by the settings' manoeuvre noise.</summary>
    public (Vec3d Position, Vec3d Velocity, Cov4 P) EstimateAt(int i, long ms, SimSettings s)
    {
        ref readonly var c = ref contacts[i];
        var p = c.Position;
        var v = c.Velocity;
        var cov = c.P;
        double dt = (ms - c.EstMs) / 1000.0;
        if (dt > 0)
        {
            Track.Carry(ref p, ref v, dt);
            cov.Predict(dt, s.SightManoeuvreMs2 * s.SightManoeuvreMs2);
        }
        return (p, v, cov);
    }

    public void Hash(ref StateHash h)
    {
        h.Add(Side);
        h.Add(Count);
        for (int i = 0; i < Count; i++)
        {
            ref readonly var c = ref contacts[i];
            h.Add(c.Entity);
            h.Add(c.FirstMs);
            h.Add(c.SeenMs);
            h.Add(c.LookedMs);
            h.Add(c.EstMs);
            h.Add(c.InSight);
            h.Add(c.SmokeInSight);
            if (c.LightInSight)
                h.Add(0x119u);   // (only when set: the hashes of worlds with no lights seen as they were)
            h.Add(c.Position.X);
            h.Add(c.Position.Y);
            h.Add(c.Position.Z);
            h.Add(c.Velocity.X);
            h.Add(c.Velocity.Y);
            h.Add(c.Velocity.Z);
            h.Add(c.P.Ee);
            h.Add(c.P.Nn);
            h.Add(c.P.VeVe);
            h.Add((int)c.Level | (int)c.Seen << 8);
            h.Add(c.TakenFor);
            h.Add(c.QuietMs);
        }
    }
}
