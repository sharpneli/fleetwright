using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>The night's challenge (docs/battle/night.md Q6): a ship flashes the challenge at a contact she can't make out,
/// and waits for the reply.
/// <list type="bullet">
/// <item><b>The signal:</b> her bridge's signal lamp, a narrow beam held on the contact (<see cref="SimSettings.ChallengeLampCd"/>,
/// <see cref="SimSettings.ChallengeLampDeg"/>) for <see cref="SimSettings.ChallengeSignalS"/>: a light in the world's
/// <see cref="Lights"/> like any other, so it shows her to whoever it reaches (the contact first), and a challenge seen
/// that isn't one's own gives her away (<c>Sightings</c>: she's a foe).</item>
/// <item><b>The reply:</b> awaited <see cref="SimSettings.ChallengeReplyS"/> after the signal. Only a ship that knows it
/// can answer: a friend (none yet: decision 1, no friendly fire), or a foe who has her enemy's reply (<see cref="Answer"/>,
/// her captain's choice), her own lamp held back on the challenger as long. Answered, the contact is taken for a friend
/// (<see cref="Contact.FriendMs"/>); not, she's known for a foe (<see cref="Contact.Foe"/>), whether she saw it or not.</item>
/// <item>One challenge a ship at a time; a contact already known for a foe isn't asked about.</item>
/// </list>
/// The challenges come from the captains (or the player): doctrine's whether to challenge, hold or fire is theirs. World
/// state: replays and hashes. Always registered tenth (<see cref="Id"/>).</summary>
public sealed class ChallengeSystem : IWorldSystem
{
    public const byte Id = 10;
    const ushort ChallengeCode = 1, AnswerCode = 2, ResolveEvent = 1;

    /// <summary>A challenge: who made it (an entity), at whom (an entity), when it began, and when the answer was given
    /// (0: none).</summary>
    struct Asked
    {
        public int Ship, Target;
        public long StartMs, AnswerMs;
    }

    readonly SimSettings settings;
    readonly ShipSystem ships;
    // the challenges open, by challenger
    Asked[] asked = new Asked[8];
    int count;

    internal ChallengeSystem(SimSettings settings, ShipSystem ships) => (this.settings, this.ships) = (settings, ships);

    /// <summary>A ship challenges a contact (an entity), if she has no challenge open.</summary>
    public static Command Challenge(int ship, int target) => new(Id, ChallengeCode, ship, A: target);

    /// <summary>A ship answers a challenge made at her (by its challenger, an entity) with its reply, while it's open.</summary>
    public static Command Answer(int ship, int challenger) => new(Id, AnswerCode, ship, A: challenger);

    long SignalMs => (long)(settings.ChallengeSignalS * 1000);
    long EndMs(in Asked a) => a.StartMs + SignalMs + (long)(settings.ChallengeReplyS * 1000);

    public void Apply(World world, in Command c)
    {
        long now = world.Time.Ms;
        int other = (int)c.A;
        switch (c.Code)
        {
            case ChallengeCode:
                if (other == 0 || other == c.Entity || Find(c.Entity) >= 0)
                    return;   // one at a time
                int i = ~Find(c.Entity);
                if (count == asked.Length)
                    Array.Resize(ref asked, 2 * count);
                Array.Copy(asked, i, asked, i + 1, count - i);
                count++;
                asked[i] = new Asked { Ship = c.Entity, Target = other, StartMs = now };
                world.Schedule(new WorldTime(EndMs(asked[i])), EventTier.Fine, c.Entity, Id, ResolveEvent);
                return;
            case AnswerCode:
                int k = Find(other);
                if (k >= 0 && asked[k].Target == c.Entity && asked[k].AnswerMs == 0 && now < EndMs(asked[k]))
                    asked[k].AnswerMs = now;
                return;
            default:
                throw new ArgumentException($"the challenge has no command {c.Code}");
        }
    }

    /// <summary>The challenges whose reply time is up resolved: the challenger's navy takes the contact for a friend if she
    /// answered, else knows her for a foe.</summary>
    public void Handle(World world, in SimEvent e)
    {
        if (e.Code != ResolveEvent)
            throw new ArgumentException($"the challenge has no event {e.Code}");
        long now = world.Time.Ms;
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            ref readonly var a = ref asked[i];
            if (now < EndMs(a))
            {
                asked[n++] = a;
                continue;
            }
            if (ships.Store.SlotOf(a.Ship) is not (>= 0 and var slot) || world.Sightings.Of(ships.Store[slot].Side) is not { } pic ||
                pic.IndexOf(a.Target) is not (>= 0 and var ci))
                continue;
            ref var con = ref pic.At(ci);
            if (con.Foe)
                continue;
            if (a.AnswerMs > 0)
                con.FriendMs = now;
            else
                con.Foe = true;
        }
        count = n;
    }

    int Find(int ship)
    {
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (asked[mid].Ship == ship)
                return mid;
            if (asked[mid].Ship < ship)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return ~lo;
    }

    /// <summary>The contact a ship is challenging (0: none open), when she began and whether she was answered.</summary>
    public (int Target, long StartMs, bool Answered) ChallengeOf(int ship)
    {
        int i = Find(ship);
        return i < 0 ? (0, 0, false) : (asked[i].Target, asked[i].StartMs, asked[i].AnswerMs > 0);
    }

    /// <summary>The challenges open, for the world view's lamps (docs/visuals/night-lights.md Q10e): the map draws them only
    /// on ships it shows.</summary>
    public void Fill(Snapshot s)
    {
        var v = s.Challenges;
        v.EnsureCapacity(count);
        for (int i = 0; i < count; i++)
            (v.Ship[i], v.Target[i], v.StartMs[i], v.AnswerMs[i]) = (asked[i].Ship, asked[i].Target, asked[i].StartMs, asked[i].AnswerMs);
        v.Count = count;
    }

    /// <summary>Whether any ship of a side has a challenge open at a contact (an entity).</summary>
    public bool Asking(byte side, int target)
    {
        var store = ships.Store;
        for (int i = 0; i < count; i++)
            if (asked[i].Target == target && store.SlotOf(asked[i].Ship) is >= 0 and var s && store[s].Side == side)
                return true;
        return false;
    }

    /// <summary>A challenge made at a ship (an entity), the k-th from 0: its challenger and when it began; false past the
    /// last. Few are ever open, so a captain asks through them all.</summary>
    public bool At(int ship, int k, out int challenger, out long startMs, out bool answered)
    {
        for (int i = 0; i < count; i++)
            if (asked[i].Target == ship && k-- == 0)
            {
                (challenger, startMs, answered) = (asked[i].Ship, asked[i].StartMs, asked[i].AnswerMs > 0);
                return true;
            }
        (challenger, startMs, answered) = (0, 0, false);
        return false;
    }

    /// <summary>Whether a ship's challenge lamp was flashing at any moment from one time to another (world ms).</summary>
    public bool Signalling(int ship, long fromMs, long toMs)
    {
        int i = Find(ship);
        return i >= 0 && asked[i].StartMs <= toMs && asked[i].StartMs + SignalMs >= fromMs;
    }

    /// <summary>The lamps flashing now into the lights (at a look, after the flares): each challenge's signal held on its
    /// contact, and each answer's held back on its challenger, from the bridge (where her navy's picture has the other, or
    /// where she is while it holds her in sight).</summary>
    internal void Gather(World world, Lights lights)
    {
        long now = world.Time.Ms;
        for (int i = 0; i < count; i++)
        {
            ref readonly var a = ref asked[i];
            if (now >= a.StartMs && now < a.StartMs + SignalMs)
                Lamp(world, lights, a.Ship, a.Target);
            if (a.AnswerMs > 0 && now >= a.AnswerMs && now < a.AnswerMs + SignalMs)
                Lamp(world, lights, a.Target, a.Ship);
        }
    }

    void Lamp(World world, Lights lights, int from, int to)
    {
        var store = ships.Store;
        if (store.SlotOf(from) is not (>= 0 and var slot) || !SearchlightSystem.AimAt(world, store[slot], to, out var aim))
            return;
        var ship = store[slot];
        var cls = ship.Handling.Class;
        var lamp = ship.Frame.ToWorld(0, 0, BridgeM(cls));
        foreach (var e in cls.Eyes)
            if (e.Kind == EyeKind.Bridge)
            {
                lamp = ship.Frame.ToWorld(e.X, e.Y, e.HeightM);
                break;
            }
        lights.AddBeam(lamp, settings.ChallengeLampCd, slot, Earth.Bearing(lamp, aim), settings.ChallengeLampDeg, 0, settings.ChallengeLampMirrorM);
    }

    /// <summary>A lamp's height with no bridge post: halfway up her.</summary>
    static double BridgeM(ShipClass c) => 0.5 * c.Silhouette.TopM;

    public void Hash(ref StateHash h)
    {
        if (count == 0)
            return;
        h.Add(count);
        for (int i = 0; i < count; i++)
        {
            ref readonly var a = ref asked[i];
            h.Add(a.Ship);
            h.Add(a.Target);
            h.Add(a.StartMs);
            h.Add(a.AnswerMs);
        }
    }
}
