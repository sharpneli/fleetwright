using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>Where the men stand by the captain's order (docs/battle/crew.md N3a): a mount's crew closed up or under cover
/// (its men waiting in its shelter, the cell under it, where a hit there still finds them: the reserve), and reliefs
/// walking from where men are spare to a station short of its need (a relief counts <see cref="SimSettings.ReliefWorth"/>
/// of one of the station's own trade, N2). Men on their way are nowhere a hit finds; they arrive on the outage clock, a
/// walk of <see cref="SimSettings.ReliefWalkS"/> and <see cref="SimSettings.ReliefWalkSectionS"/> a section between. The
/// orders are the captain's (Sim.Ai's crew pass), as commands; this is what they do.</summary>
public static class Reliefs
{
    /// <summary>Where a relief comes from: a station's group, a mount's men under cover, a fire party standing by.</summary>
    public enum From : byte
    {
        Station,
        Covered,
        Party,
    }

    /// <summary>A relief's source as a command carries it.</summary>
    public static long Source(From kind, int index) => (long)kind * 1_000_000 + index;

    /// <summary>A mount's shelter: the cell under it in the hull, the topmost whose plan holds the mount's centre; -1 if none
    /// (its men under cover are then out of the hits' way).</summary>
    internal static int ShelterOf(ShipClass c, int mount)
    {
        ref readonly var row = ref c.Mounts[mount];
        double below = c.Components[row.Component].Bounds.Base + 0.5;
        int best = -1;
        double top = double.NegativeInfinity;
        for (int i = 0; i < c.Cells.Length; i++)
        {
            var b = c.Cells[i].Box;
            if (row.X < b.X0 || row.X > b.X1 || row.Y < b.Y0 || row.Y > b.Y1 || b.Top > below || b.Top <= top)
                continue;
            (best, top) = (i, b.Top);
        }
        return best < 0 ? -1 : Casualties.Cell(c, best);
    }

    /// <summary>A group's section (a part's by its middle).</summary>
    internal static int SectionOf(ShipClass c, int g)
    {
        if (g < c.Components.Length)
        {
            var b = c.Components[g].Bounds;
            return c.SectionAt((b.X0 + b.X1) / 2);
        }
        return c.Cells[g - c.Components.Length].Section;
    }

    /// <summary>The walk between two groups' places, ms.</summary>
    static long Walk(ShipClass c, int from, int to, SimSettings s) =>
        (long)(1000 * (s.ReliefWalkS + s.ReliefWalkSectionS * Math.Abs(SectionOf(c, from) - SectionOf(c, to))));

    /// <summary>A mount's crew closed up, or under cover: under cover its men go to its shelter at once; closed up those
    /// under cover walk back. Whether anything changed.</summary>
    public static bool Posture(ShipClass c, ShipCondition k, int mount, bool closedUp, long now, SimSettings s)
    {
        if (mount < 0 || mount >= c.Mounts.Length || k.ClosedUp[mount] == closedUp)
            return false;
        int g = Casualties.Part(c.Mounts[mount].Component);
        k.ClosedUp[mount] = closedUp;
        if (!closedUp)
        {
            int men = Casualties.Men(c, k, g);
            k.Away[g] += men;
            k.Covered[mount] += men;
        }
        else if (k.Covered[mount] > 0)
        {
            int from = k.MountShelter[mount] >= 0 ? k.MountShelter[mount] : g;
            k.Move(-1 - g, k.Covered[mount], now + Walk(c, from, g, s));
            k.Covered[mount] = 0;
        }
        return true;
    }

    /// <summary>Men sent from a source to a station (a group), there after their walk: from a station's men there, a
    /// mount's men under cover, or a fire party standing by (lent, back to it when it's sent to a job: decision 12). As
    /// many as there are, up to the asked. How many went.</summary>
    public static int Relieve(ShipClass c, ShipCondition k, long source, int to, int men, long now, SimSettings s)
    {
        int groups = c.Components.Length + c.Cells.Length;
        if (to < 0 || to >= groups || men <= 0)
            return 0;
        var kind = (From)(source / 1_000_000);
        int i = (int)(source % 1_000_000), from;
        switch (kind)
        {
            case From.Station:
                if (i < 0 || i >= groups || i == to)
                    return 0;
                men = Math.Min(men, Casualties.Men(c, k, i));
                k.Away[i] += men;
                from = i;
                break;
            case From.Covered:
                if (i < 0 || i >= c.Mounts.Length)
                    return 0;
                men = Math.Min(men, k.Covered[i]);
                k.Covered[i] -= men;
                from = k.MountShelter[i] >= 0 ? k.MountShelter[i] : Casualties.Part(c.Mounts[i].Component);
                break;
            case From.Party:
                if (i < 0 || i >= k.PartyCount)
                    return 0;
                ref var p = ref k.Parties[i];
                if (!p.Mustered || p.Target >= 0 || p.Job != RepairJob.None || k.PartyLent[i] > 0 && k.PartyLentTo[i] != to)
                    return 0;   // never sent (its men still at their stations), at work, or lent elsewhere: one station a party
                men = Math.Min(men, (int)p.Men);
                p.Men -= men;
                (k.PartyLent[i], k.PartyLentTo[i]) = (k.PartyLent[i] + men, to);
                from = Casualties.PlaceOf(p, now) is int at and >= 0 ? at : to;
                break;
            default:
                return 0;
        }
        if (men > 0)
            k.Move(to, men, now + Walk(c, from, to, s));
        return Math.Max(men, 0);
    }

    /// <summary>A party sent to a job takes back the men it lent (decision 12: "toss them back"), as many as are left at
    /// the station they went to.</summary>
    public static void Recall(ShipClass c, ShipCondition k, int party)
    {
        int lent = k.PartyLent[party], at = k.PartyLentTo[party];
        if (lent <= 0)
            return;
        int back = Math.Min(Math.Min(lent, k.In[at]), Casualties.Men(c, k, at));
        k.In[at] -= back;
        k.Parties[party].Men += back;
        (k.PartyLent[party], k.PartyLentTo[party]) = (0, 0);
    }

    /// <summary>The men on their way to a station.</summary>
    public static int Inbound(ShipCondition k, int g)
    {
        int n = 0;
        for (int i = 0; i < k.Moves; i++)
            if (k.MoveTo[i] == g)
                n += k.MoveMen[i];
        return n;
    }

    /// <summary>The reliefs at a station now (its arrivals less those hit there since: the casualty roll takes their
    /// share), no more than the men there.</summary>
    public static double At(ShipClass c, ShipCondition k, int g) => k.In[g] <= 0 ? 0 : Math.Min(k.In[g], Casualties.Men(c, k, g));
}
