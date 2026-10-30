using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>The men's nerve at their stations (docs/battle/crew.md N3b, decisions 5-7; CUF R1-R4): each station keeps its
/// <b>duress</b>, what has landed at and around it, fading (its own casualties, its officer hit, the casualties at an open
/// station next to an open one, a shell through it, flash or fire in it, a burst near it; never hits elsewhere on the
/// ship, no contagion), against its men's <b>nerve</b> by their experience (<see cref="SimSettings.Nerves"/>; work-up doesn't
/// enter). Over it, a share of the men go on their own (shaken), rising smoothly with the duress up to the row's most, a
/// core never going; they come back once the duress is well under the nerve and a while has passed. Its officer there
/// steadies them (a higher nerve, half the share going); hit, they waver until a relief officer walks in from the ship's
/// few spare ones, and a green station with no officer after a shell through it or a fire has some of its men frozen,
/// present and not working, until one comes. A station that has gone over its nerve stands less the next time.
/// <para>All through where the men stand (the plan's principle): the shaken and the frozen aren't at work
/// (<see cref="BattleStations.AtWork"/>); rough, they still stand where the hits find them, as the men of an untenable
/// space do (N2). The shaken aren't the captain's reserve. Worked out with her functions (on a hit, on the outage clock
/// while men are shaken, frozen or an officer is coming), the same at any compression; no garbage.</para></summary>
public static class Steadiness
{
    /// <summary>A station's channels of duress (their half-lives <see cref="SimSettings.DuressHalfLifeS"/>): a burst near
    /// it, a shell through it, flash or fire, its casualties, its casualties held; and the nerve its sensitising has taken
    /// that comes back (<see cref="SimSettings.SensitiseBackS"/>).</summary>
    const int Near = 0, Inside = 1, Burn = 2, Fast = 3, Held = 4, Sens = 5;
    public const int Channels = 6;

    /// <summary>A class's stations with men at work (every group with a battle crew but the repair men's quarters), each
    /// open or enclosed and how its men are covered from a burst near it; the open ones by x. At spawn.</summary>
    internal static void Setup(ShipClass c, ShipCondition k)
    {
        int groups = c.Components.Length + c.Cells.Length, n = 0, open = 0;
        k.StationOf = new int[groups];
        for (int g = 0; g < groups; g++)
        {
            int crew = g < c.Components.Length ? c.Components[g].BattleCrew : c.Cells[g - c.Components.Length].BattleCrew;
            k.StationOf[g] = crew > 0 && BattleStations.Of(c, g) != BattleStation.Repair ? n++ : -1;
        }
        k.StationGroup = new int[n];
        (k.StationOpen, k.StationCover) = (new bool[n], new byte[n]);
        k.Duress = new double[n * Channels];
        (k.DuressMs, k.CalmMs, k.OfficerAt, k.BurnMs) = (new long[n], new long[n], new long[n], new long[n]);
        (k.Shaken, k.Frozen, k.NerveKept, k.Over) = (new int[n], new int[n], new double[n], new bool[n]);
        (k.InsideStamp, k.NearStamp, k.Active, k.IsActive) = (new int[n], new int[n], new int[n], new bool[n]);
        for (int g = 0; g < groups; g++)
        {
            int s = k.StationOf[g];
            if (s < 0)
                continue;
            k.StationGroup[s] = g;
            (k.StationOpen[s], int cover) = Kind(c, g);
            k.StationCover[s] = (byte)cover;
            open += k.StationOpen[s] ? 1 : 0;
        }
        k.OpenByX = new int[open];
        var x = new double[open];
        open = 0;
        for (int s = 0; s < n; s++)
            if (k.StationOpen[s])
            {
                k.OpenByX[open] = s;
                x[open++] = CentreX(c, k.StationGroup[s]);
            }
        Array.Sort(x, k.OpenByX);
    }

    /// <summary>Whether a group's station is open (its men see what lands about them) and how they're covered from a burst
    /// near it (<see cref="SimSettings.DuressNearCover"/>: open, shielded, a casemate, a turret or below the armour).</summary>
    static (bool Open, int Cover) Kind(ShipClass c, int g)
    {
        if (g >= c.Components.Length)
            return (false, 3);
        int m = c.MountOf(g);
        if (m >= 0 && c.Mounts[m].Component == g)
            return c.Enclosed(m) ? (false, 3) : c.Mounts[m].Casemate ? (false, 2) : (true, c.Mounts[m].FaceMm > 0 ? 1 : 0);
        return c.Components[g].Kind is ComponentKind.Barbette or ComponentKind.ConningTower ? (false, 3) : (true, 0);
    }

    static double CentreX(ShipClass c, int g)
    {
        var b = Casualties.BoxOf(c, g);
        return (b.X0 + b.X1) / 2;
    }

    /// <summary>A ship's men steady again (at spawn): no duress, every officer there, the nerve whole.</summary>
    internal static void Reset(ShipCondition k)
    {
        Array.Clear(k.Duress);
        Array.Clear(k.DuressMs);
        Array.Clear(k.CalmMs);
        Array.Clear(k.OfficerAt);
        Array.Clear(k.BurnMs);
        Array.Clear(k.Shaken);
        Array.Clear(k.Frozen);
        Array.Clear(k.Over);
        Array.Clear(k.InsideStamp);
        Array.Clear(k.NearStamp);
        Array.Clear(k.IsActive);
        Array.Fill(k.NerveKept, 1.0);
        (k.ActiveN, k.SpareOfficers, k.NerveDueMs, k.StirredId) = (0, -1, 0, -1);
    }

    /// <summary>The men at a station who aren't at work for their nerve: the shaken and the frozen.</summary>
    public static int Idle(ShipCondition k, int g)
    {
        int s = g < k.StationOf.Length ? k.StationOf[g] : -1;
        return s < 0 ? 0 : k.Shaken[s] + k.Frozen[s];
    }

    /// <summary>A station's duress now (0 for a group that isn't one).</summary>
    public static double Duress(ShipClass c, ShipCondition k, int g, long now, SimSettings s)
    {
        int n = g < k.StationOf.Length ? k.StationOf[g] : -1;
        return n < 0 ? 0 : Level(c, k, n, now, s, s.NerveAt(k.Experience).Fade);
    }

    /// <summary>The stress on a station's men at their work (docs/battle/balance.md BL1d): the multiplier on their errors
    /// (laying, timing the roll, reading a range), 1 + <see cref="SimSettings.StressGain"/> x their duress against their
    /// nerve (to 1), by a component's station (one that isn't a station, or none: 1).</summary>
    public static double Stress(ShipClass c, ShipCondition k, int component, long now, SimSettings s)
    {
        if (component < 0 || s.StressGain == 0)
            return 1;
        double d = Duress(c, k, Casualties.Part(component), now, s);
        return d <= 0 ? 1 : 1 + s.StressGain * Math.Min(d / Math.Max(s.NerveAt(k.Experience).Nerve, 1e-6), 1);
    }

    /// <summary>The nerve a party of her men walking in carries (her men's, no officer's or station's own): reliefs wait
    /// while a station's duress is over it (decision 1's lull, CUF R9).</summary>
    public static double ReliefNerve(ShipCondition k, SimSettings s) => s.NerveAt(k.Experience).Nerve;

    static double Level(ShipClass c, ShipCondition k, int n, long now, SimSettings s, double fade)
    {
        double dt = Math.Max(now - k.DuressMs[n], 0) / 1000.0, d = 0;
        var half = s.DuressHalfLifeS;
        for (int ch = 0; ch < Sens; ch++)
            if (k.Duress[n * Channels + ch] != 0)
                d += k.Duress[n * Channels + ch] * Math.Pow(2, -dt * fade / half[ch]);
        // flash or a fire in it: the level while it burns, fading after
        if (BattleStations.Burning(c, k, k.StationGroup[n]))
            d += s.DuressFire;
        else if (k.BurnMs[n] > 0)
            d += s.DuressFire * Math.Pow(2, -Math.Max(now - k.BurnMs[n], 0) / 1000.0 * fade / half[Burn]);
        return Math.Max(d, 0);
    }

    /// <summary>A station's channels faded to now.</summary>
    static void Fade(ShipCondition k, int n, long now, SimSettings s, double fade)
    {
        double dt = Math.Max(now - k.DuressMs[n], 0) / 1000.0;
        k.DuressMs[n] = now;
        if (!(dt > 0))
            return;
        var half = s.DuressHalfLifeS;
        for (int ch = 0; ch < Sens; ch++)
            if (k.Duress[n * Channels + ch] != 0)
                k.Duress[n * Channels + ch] *= Math.Pow(2, -dt * fade / half[ch]);
        k.Duress[n * Channels + Sens] *= Math.Pow(2, -dt / s.SensitiseBackS);
    }

    static void Add(ShipCondition k, int n, int ch, double amount, long now, SimSettings s)
    {
        Fade(k, n, now, s, s.NerveAt(k.Experience).Fade);
        k.Duress[n * Channels + ch] += amount;
        k.StirredId = k.RiskId;
        if (!k.IsActive[n])
        {
            k.IsActive[n] = true;
            k.Active[k.ActiveN++] = n;
        }
    }

    /// <summary>Men at a station hit (E6's roll, <see cref="Casualties.Draw"/>): hurt of the men there, its officer among
    /// them or not. Their share of its crew (1 = the whole crew hit at once, however many are left) is its duress (some of
    /// it held longer), its officer's too, and the open stations next to an open one see it; the shaken and the frozen are
    /// hit as the rest.</summary>
    internal static void Hurt(ShipClass c, ShipCondition k, int g, int hurt, int men, bool officer, long now, SimSettings s)
    {
        int n = g < k.StationOf.Length ? k.StationOf[g] : -1;
        if (n < 0 || hurt <= 0 || men <= 0)
            return;
        int crew = Math.Max(g < c.Components.Length ? c.Components[g].BattleCrew : c.Cells[g - c.Components.Length].BattleCrew, men);
        double share = (double)hurt / crew;
        Add(k, n, Fast, (1 - s.DuressHeldShare) * share, now, s);
        Add(k, n, Held, s.DuressHeldShare * share, now, s);
        k.Shaken[n] -= Math.Min(k.Shaken[n], (int)Math.Round((double)hurt / men * k.Shaken[n]));
        k.Frozen[n] -= Math.Min(k.Frozen[n], (int)Math.Round((double)hurt / men * k.Frozen[n]));
        if (officer && k.OfficerAt[n] == 0)
        {
            Add(k, n, Fast, s.DuressOfficerHit, now, s);
            if (k.SpareOfficers < 0)
                k.SpareOfficers = Math.Max(1, (int)Math.Round(s.OfficersSpare * Crew(c, k)));
            // a relief officer walks in from the ship's few (from her middle), if one's left
            if (k.SpareOfficers > 0)
            {
                k.SpareOfficers--;
                k.OfficerAt[n] = now + (long)(1000 * (s.ReliefWalkS + s.ReliefWalkSectionS * Math.Abs(Reliefs.SectionOf(c, g) - c.Sections.Length / 2)));
            }
            else
                k.OfficerAt[n] = -1;
        }
        if (!k.StationOpen[n])
            return;
        // the open stations about it see it
        double x = CentreX(c, g), reach = s.DuressNeighbourM;
        var box = Casualties.BoxOf(c, g);
        int lo = 0, hi = k.OpenByX.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (CentreX(c, k.StationGroup[k.OpenByX[mid]]) < x - reach)
                lo = mid + 1;
            else
                hi = mid;
        }
        for (int i = lo; i < k.OpenByX.Length; i++)
        {
            int o = k.OpenByX[i], og = k.StationGroup[o];
            if (CentreX(c, og) > x + reach)
                break;
            if (o == n || Casualties.Men(c, k, og) <= 0)
                continue;
            var b = Casualties.BoxOf(c, og);
            if (Effects.BoxDistance(b, new P3((box.X0 + box.X1) / 2, (box.Y0 + box.Y1) / 2, (box.Base + box.Top) / 2)) <= reach)
                Add(k, o, Fast, s.DuressNeighbourShare * share, now, s);
        }
    }

    static int Crew(ShipClass c, ShipCondition k)
    {
        int crew = 0;
        foreach (int g in k.StationGroup)
            crew += g < c.Components.Length ? c.Components[g].BattleCrew : c.Cells[g - c.Components.Length].BattleCrew;
        return crew;
    }

    /// <summary>A shell through a station's space (its lane, E6): once a hit, at the casualty pass's time.</summary>
    internal static void Through(ShipCondition k, int g, SimSettings s)
    {
        int n = g < k.StationOf.Length ? k.StationOf[g] : -1;
        if (n < 0 || k.InsideStamp[n] == k.RiskId)
            return;
        k.InsideStamp[n] = k.RiskId;
        Add(k, n, Inside, s.DuressInside, k.RiskMs, s);
    }

    /// <summary>A burst at a point (ship frame) near a station whose men it may reach: within
    /// <see cref="SimSettings.DuressNearM"/>, by how they're covered; once a hit.</summary>
    internal static void NearBurst(ShipClass c, ShipCondition k, int g, P3 at, SimSettings s)
    {
        int n = g < k.StationOf.Length ? k.StationOf[g] : -1;
        if (n < 0 || k.NearStamp[n] == k.RiskId || Effects.BoxDistance(Casualties.BoxOf(c, g), at) > s.DuressNearM)
            return;
        k.NearStamp[n] = k.RiskId;
        Add(k, n, Near, s.DuressNearBurst * s.DuressNearCover[Math.Min(k.StationCover[n], s.DuressNearCover.Length - 1)], k.RiskMs, s);
    }

    /// <summary>A salvo down about her that straddled her, or fell while she was lit (docs/battle/night.md Q8; crew.md N3b's
    /// last row, CUF R2-R3): the strain of being found and fired on, at every station, on the burst's 30 s channel; the
    /// amount already weighed by her men's greenness (none for trained men: no sourced case of them breaking before being
    /// hit).</summary>
    public static void Pressed(ShipCondition k, double amount, long now, SimSettings s)
    {
        if (amount <= 0)
            return;
        for (int n = 0; n < k.StationGroup.Length; n++)
            Add(k, n, Near, amount, now, s);
    }

    /// <summary>Propellant flash through a station (16 §4.6).</summary>
    internal static void Flash(ShipCondition k, int g, SimSettings s)
    {
        int n = g < k.StationOf.Length ? k.StationOf[g] : -1;
        if (n >= 0)
            Add(k, n, Burn, s.DuressFire, k.RiskMs, s);
    }

    /// <summary>Who stays at each station with duress now (with her functions, <see cref="Functions.Evaluate"/>): a relief
    /// officer come; over its nerve, more of its men go; well under it a while, they come back; frozen men at a green
    /// station with no officer. Then when to look again.</summary>
    internal static void Update(ShipCondition k, SimSettings s, long now)
    {
        if (k.ActiveN == 0)
        {
            k.NerveDueMs = 0;
            return;
        }
        var c = k.Class;
        var row = s.NerveAt(k.Experience);
        double green = Math.Clamp(1 - k.Experience, 0, 1);
        long due = long.MaxValue, check = now + (long)(s.NerveCheckS * 1000);
        for (int i = k.ActiveN - 1; i >= 0; i--)
        {
            int n = k.Active[i], g = k.StationGroup[n];
            Fade(k, n, now, s, row.Fade);
            ref double fast = ref k.Duress[n * Channels + Fast];
            if (k.OfficerAt[n] > 0 && k.OfficerAt[n] <= now)
            {
                // a relief officer come: his men steadier, the frozen ones at work again
                k.OfficerAt[n] = 0;
                double off = Math.Min(s.DuressOfficerCome, fast);
                fast -= off;
                k.Duress[n * Channels + Held] = Math.Max(k.Duress[n * Channels + Held] - (s.DuressOfficerCome - off), 0);
                k.Frozen[n] = 0;
            }
            bool burning = BattleStations.Burning(c, k, g);
            if (burning)
                k.BurnMs[n] = now;
            double d = Level(c, k, n, now, s, row.Fade);
            bool officer = k.OfficerAt[n] == 0;
            double nerve = row.Nerve * (officer ? s.OfficerNerve : s.OfficerHitNerve) * k.NerveKept[n] * (1 - Math.Min(k.Duress[n * Channels + Sens], 0.9));
            if (d > nerve && !k.Over[n])
            {
                // over its nerve: it stands less the next time, some of that back in a while
                k.Over[n] = true;
                k.NerveKept[n] *= 1 - s.SensitiseLoss * (1 - s.SensitiseBackShare);
                k.Duress[n * Channels + Sens] += s.SensitiseLoss * s.SensitiseBackShare;
            }
            int men = Casualties.Men(c, k, g);
            double most = (k.StationOpen[n] ? row.GoingOpen : row.GoingEnclosed) * (officer ? s.OfficerGoing : 1);
            int going = nerve > 0 ? (int)Math.Round(men * most / (1 + Math.Exp(-(d - nerve) / (s.NerveWidth * nerve)))) : 0;
            bool calm = d < s.NerveBackShare * nerve;
            if (!calm)
                k.CalmMs[n] = 0;
            else
            {
                k.Over[n] = false;
                if (k.CalmMs[n] == 0)
                    k.CalmMs[n] = now;
            }
            long backAt = k.CalmMs[n] > 0 ? k.CalmMs[n] + (long)(1000 * row.BackS * (officer ? 1 : s.OfficerHitBack)) : long.MaxValue;
            bool back = now >= backAt;
            int shaken = k.Shaken[n];
            if (going > shaken || back)
                shaken = going;
            int frozen = officer || back ? 0 : k.Frozen[n];
            if (!officer && green > 0 && (burning || k.Duress[n * Channels + Inside] > 0.1 * s.DuressInside))
                frozen = Math.Max(frozen, (int)Math.Round(s.FrozenShare * green * (men - shaken)));
            shaken = Math.Min(shaken, men);
            (k.Shaken[n], k.Frozen[n]) = (shaken, Math.Min(frozen, men - shaken));
            // when to look again: the shaken or frozen back, an officer coming
            if (k.Shaken[n] + k.Frozen[n] > 0)
                due = Math.Min(due, Math.Max(Math.Min(backAt, check), now + 1));
            if (k.OfficerAt[n] > 0)
                due = Math.Min(due, k.OfficerAt[n]);
            if (d < 0.01 && k.Shaken[n] + k.Frozen[n] == 0 && k.OfficerAt[n] <= 0 && !burning)
            {
                // steady again: off the list (its officer's and its nerve's state kept)
                k.IsActive[n] = false;
                k.Active[i] = k.Active[--k.ActiveN];
            }
        }
        if (due == long.MaxValue)
            k.NerveDueMs = 0;
        else
            k.NerveDue(due);
    }
}
