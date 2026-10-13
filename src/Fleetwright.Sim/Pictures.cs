using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>How far a contact has been made out: a ship, its type, its class (perhaps wrongly), its name.</summary>
public enum ContactLevel : byte
{
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

/// <summary>A ship as a navy holds it (docs/world/architecture.md "Knowledge"; docs/battle/plan.md step 3.2d): keyed on
/// the true ship (agreed 2026-10-13: no false merges or splits in the battle module); first and last seen, and whether it
/// was in sight at the last look; the estimate (position and velocity at <see cref="EstMs"/>, with its covariance); how far
/// it's been made out and what it's taken for (perhaps wrongly); and the last report's source.</summary>
public struct Contact
{
    public int Entity;
    public long FirstMs, SeenMs, EstMs;

    /// <summary>When a look was last made for it (seen or not).</summary>
    public long LookedMs;
    public bool InSight;
    public Vec3d Position, Velocity;
    public Cov4 P;
    public ContactLevel Level;
    public ShipType Type;

    /// <summary>The class it's taken for (a registry id), -1 below <see cref="ContactLevel.Class"/>.</summary>
    public int TakenFor;
    public int Observer;
    public EyeKind Eye;

    /// <summary>The ship's side (a navy knows it once it's made out the type: its colours, its build).</summary>
    public byte Side;
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
            h.Add(c.Position.X);
            h.Add(c.Position.Y);
            h.Add(c.Position.Z);
            h.Add(c.Velocity.X);
            h.Add(c.Velocity.Y);
            h.Add(c.Velocity.Z);
            h.Add(c.P.Ee);
            h.Add(c.P.Nn);
            h.Add(c.P.VeVe);
            h.Add((int)c.Level);
            h.Add(c.TakenFor);
        }
    }
}
