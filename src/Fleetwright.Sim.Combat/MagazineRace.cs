using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>How a magazine fire ended (docs/battle/magazines.md V3; 07 §6.3): contained (flooded, or the vents kept up),
/// a gunhouse burned out (the gas left through its trunk, the ship fights on), a local breach (the deck over it or its
/// sides or bottom gave, the girder held), the ship severed at the magazine, or two magazine groups together.</summary>
public enum MagazineTier : byte
{
    Contained,
    BurnedOut,
    Breached,
    Severed,
    TwoGroups,
}

/// <summary>A magazine's race run out (<see cref="Magazines.SetOff"/>): its tier, the peak gauge pressure (kPa), the
/// propellant burned (kg; and by the girder's first failure and a second after, the fireball's), the charges that were
/// there, the share of the girder lost at the magazine, the vents at the end (m²), the magazines alight, the gunhouses
/// vented (and lifted), when the first vented and when the girder first gave (s from the flash; NaN none), and the men
/// it killed and wounded.</summary>
public struct RaceResult
{
    public MagazineTier Tier;
    public double PeakKPa, BurnedKg, FastKg, Charges, D, VentM2, VentS, StructureS;
    public int Magazines, Vented, Lifted, Killed, Wounded;
    /// <summary>What gave, as flags: 1 the bulkhead to the next space, 2 the deck over it, 4 the sides, 8 the bottom.</summary>
    public int Gave;
    /// <summary>The magazine's place along her, 0 the stern to 1 the bow; the moment on the girder there over what's left
    /// of its capacity (1 or more: broken); an end magazine that took her end off (forward or aft of it flooding).</summary>
    public double Station, GirderLoad;
    public bool EndLost;
    public readonly bool Lost => Tier >= MagazineTier.Severed;
}

public static partial class Magazines
{
    const double Atm = 101_325;

    /// <summary>A bench's view of a race (<c>shipgen magazine --race --trace</c>): what gave and when. Null in the
    /// game.</summary>
    public static Action<string>? Trace;

    /// <summary>A magazine set off (V3; research/simulation/damage-research/10 §6, magazine_race_ref.py): the venting race,
    /// then what it left. One gas volume, the magazine's spaces' gas (and its handling room's, once the scuttle gives),
    /// P = P_atm + m·k·f/V:
    /// <list type="bullet">
    /// <item><b>made</b> by the lit charges' surface, ρ·S·r(P), r = β·P^α on two branches meeting at the knee
    /// (<see cref="PropellantRow"/>); a cord's surface shrinks as it burns, a tube's doesn't;</item>
    /// <item><b>the flame through the stow:</b> the charges lying open (in the scuttles, or bags alone: the packaging's)
    /// light within <see cref="SimSettings.MagazineBagSpreadS"/>; those in their packages light one from the next, R
    /// neighbours a burning charge over its burn-out (<see cref="SimSettings.MagazinePackageR"/>); the flooded share never
    /// lights;</item>
    /// <item><b>let out</b> through the open vents, choked or not: the spaces' own, each enclosed mount's trunk once its
    /// roof or gunhouse gives, a shield's or casemate's hoist at the scuttle's pressure, and what gives of the magazine's
    /// boundaries by the research's table (<see cref="SimSettings.MagazineGiveKPa"/>: the bulkhead to the next space, the
    /// armoured deck over it, its sides, its bottom). An approved exception to the blast's own rules (the user,
    /// 2026-10-20): run on a ship's own plating, they let the gas spread through her thin bulkheads and never load the
    /// girder;</item>
    /// <item><b>another magazine</b> across the bulkhead lights too when it gives (Hood's 4″ to her 15″);</item>
    /// <item><b>the tier</b> from the girder's share lost at the magazine (<see cref="SimSettings.MagazineD"/>; V4's hull
    /// girder takes its place) and the gunhouses vented.</item>
    /// </list>
    /// What it leaves when she lives: the magazines alight spent (their mounts out of ammunition), the vented gunhouses and
    /// barbettes destroyed and burning, the trains' men flashed (a contained one, the magazine's), the holes to the sea
    /// where its sides or bottom gave. Allocation-free.</summary>
    public static RaceResult SetOff(ShipClass c, ShipCondition k, int room, ReadOnlySpan<MountState> mounts, SimSettings s, long now,
        ref RandomStream r, double sea = 0)
    {
        var res = new RaceResult { VentS = double.NaN, StructureS = double.NaN };
        if (room < 0)
            return res with { Tier = MagazineTier.Severed };
        int nm = c.Mounts.Length;
        Span<bool> vented = nm <= 256 ? stackalloc bool[nm] : new bool[nm];
        Span<bool> fed = nm <= 256 ? stackalloc bool[nm] : new bool[nm];
        Span<int> lit = stackalloc int[8];
        int lits = 0;

        // the propellant and its packing: the first mount it feeds says what they are
        ChargeRow charge = default;
        foreach (ref readonly var mr in c.Mounts)
            if (mr.Magazine == room && mr.Battery >= 0)
            {
                charge = c.Charges[mr.Type];
                break;
            }
        var pr = charge.Propellant;
        if (!(pr.BetaMmS > 0) || !(charge.MassKg > 0))
            return res with { Tier = MagazineTier.Severed };   // nothing known to burn: as before
        double nOpen = 0, nCased = 0, massSum = 0, v = 0, a = 0;
        int id = ++k.BlastId;
        var flash = Discipline(k, s);
        Light(c, k, room, mounts, fed, lit, ref lits, ref nOpen, ref nCased, ref massSum, flash, s, id, ref v, ref a);
        double flooded = Math.Max(Flooded(c, k, room), k.MagazineWet.Length > room ? k.MagazineWet[room] : 0);   // flooded or sprinkled
        double ck = massSum / Math.Max(nOpen + nCased, 1);   // a charge's kg, mass-weighted
        res.Charges = nOpen + nCased;

        double tBurn = pr.WebM / Rate(Atm, pr);
        double rt = s.MagazineHeatKeep * pr.ImpetusJkg, choke = Choke(s.MagazineGasGamma);
        double pkg = s.MagazinePackageR[Math.Min((int)charge.Packaging, s.MagazinePackageR.Length - 1)] * pr.Sensitivity;
        Span<double> cnt = stackalloc double[32], z = stackalloc double[32], give = stackalloc double[s.MagazineGiveKPa.Length],
            opens = stackalloc double[s.MagazineGiveM2.Length];
        GiveKPa(c, k, room, s, give);
        GiveM2(c, room, s, opens);
        int cohorts = 0, gave = 0;
        double litOpen = 0, litCased = 0, cohortT0 = -1, gas = 0, burned = 0, peak = 0, t = 0, more = 0, held = 0;
        if (nOpen * (1 - flooded) < 1 && nCased * (1 - flooded) >= 1)
        {
            litCased = 1;   // the flame reached it: one charge in its package lights
            (cnt[0], z[0], cohorts, cohortT0) = (1, 0, 1, 0);
        }
        double dt = s.MagazineRaceMs[0] / 1000;
        while (t < s.MagazineRaceS)
        {
            double p = Atm + gas * rt / Math.Max(v, 1), rate = Rate(p, pr), gen = 0, burning = 0;
            for (int i = 0; i < cohorts; i++)
            {
                if (z[i] >= 1)
                    continue;
                double shape = pr.Form switch
                {
                    PropellantForm.Cord => Math.Sqrt(1 - z[i]),
                    PropellantForm.Grain => Math.Pow(1 - z[i], 2.0 / 3),
                    _ => 1,
                };
                double g = Math.Min(pr.DensityKgM3 * cnt[i] * ck * pr.SurfaceM2Kg * shape * rate, cnt[i] * ck * (1 - z[i]) / dt);
                gen += g;
                z[i] = Math.Min(1, z[i] + g * dt / (cnt[i] * ck));
                burning += cnt[i];
            }
            gas = Math.Max(0, gas + (gen - Flow(a, p, rt, choke, s)) * dt);
            burned += gen * dt;

            // the flame through the stow
            double openDry = nOpen * (1 - flooded), casedDry = nCased * (1 - flooded), d = 0;
            if (openDry - litOpen > 0)
            {
                double dOpen = (openDry - litOpen) * (1 - Math.Exp(-dt / s.MagazineBagSpreadS));
                (litOpen, d) = (litOpen + dOpen, d + dOpen);
            }
            if (casedDry - litCased > 0 && (burning > 0 || more > 0))
            {
                double dCased = Math.Min(casedDry - litCased, burning * pkg / tBurn * dt * (casedDry - litCased) / Math.Max(casedDry, 1) + more);
                (litCased, d, more) = (litCased + dCased, d + dCased, 0);
            }
            if (d > 0)
            {
                if (cohorts > 0 && (t - cohortT0 < 0.1 || cohorts == cnt.Length))
                {
                    int last = cohorts - 1;
                    z[last] = cnt[last] * z[last] / (cnt[last] + d);
                    cnt[last] += d;
                }
                else
                    (cnt[cohorts], z[cohorts], cohorts, cohortT0) = (d, 0, cohorts + 1, t);
            }

            // what gives as the pressure stands: the scuttles and the ways up, then the magazine's own boundaries
            double kpa = (p - Atm) / 1000;
            peak = Math.Max(peak, kpa);
            if (kpa >= s.MagazineScuttleKPa)
                Scuttles(c, k, fed, id, ref v, ref a, s);
            a += Trunks(c, k, fed, vented, kpa, t, ref res, s);
            for (int b = 0; b < s.MagazineGiveKPa.Length; b++)
            {
                if ((gave & (1 << b)) != 0 || kpa < give[b])
                    continue;
                gave |= 1 << b;
                // a side torn under the waterline has the sea behind it (07 §2.4, 10 §6: "backed by water"): no way out for
                // the gas until the deck has opened one up; else a thinner side would save her by venting first [EST]
                if (b == 2 && (gave & 2) == 0)
                    held = opens[b];
                else
                    a += opens[b] + (b == 1 ? held : 0);
                Trace?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture, "      {0:0.000} s: {1} gave at {2:0} kPa",
                    t, b switch { 0 => "the bulkhead to the next space", 1 => "the deck over it", 2 => "its sides", _ => "its bottom" }, kpa));
                if (b == 0)
                {
                    // another magazine across it lights too: its charges join the stow, those lying open with the rest
                    double o0 = nOpen, c0 = nCased;
                    Neighbours(c, k, mounts, fed, lit, ref lits, ref nOpen, ref nCased, ref massSum, flash, s, id, ref v, ref a);
                    ck = massSum / Math.Max(nOpen + nCased, 1);
                    res.Charges = nOpen + nCased;
                    if (nOpen - o0 < 1 && nCased - c0 >= 1)
                        more += 1;
                }
                else if (double.IsNaN(res.StructureS))
                    (res.StructureS, res.FastKg) = (t, burned);
            }
            if (!double.IsNaN(res.StructureS) && t <= res.StructureS + 1)
                res.FastKg = burned;
            if (burning < 1e-6 && litOpen >= openDry - 1e-6 && litCased >= casedDry - 1e-6 && more <= 0 && t > 1 && kpa < 1)
                break;
            dt = (t < 5 ? s.MagazineRaceMs[0] : s.MagazineRaceMs[1]) / 1000;
            t += dt;
        }

        res.PeakKPa = peak;
        res.BurnedKg = burned;
        res.Magazines = lits;
        res.Gave = gave;
        res.VentM2 = a;
        res.D = ((gave & 2) != 0 ? s.MagazineD[0] : 0) + ((gave & 4) != 0 ? s.MagazineD[1] : 0) + ((gave & 8) != 0 ? s.MagazineD[2] : 0);
        Girder(c, lit[..lits], res.D, sea, s, ref res);
        res.Tier = res.GirderLoad >= 1 && !res.EndLost ? (lits > 1 ? MagazineTier.TwoGroups : MagazineTier.Severed)
            : res.D > 0 ? MagazineTier.Breached : res.Vented > 0 ? MagazineTier.BurnedOut : MagazineTier.Contained;
        if (double.IsNaN(res.StructureS))
            res.FastKg = burned;
        Aftermath(c, k, room, lit[..lits], vented, gave, opens[2] / 2, opens[3], ref res, s, now, ref r);
        return res;
    }

    /// <summary>What a magazine's boundaries give at, kPa: the research's table for the steel at Lion's Q, by the steel at
    /// this one (docs/battle/magazines.md "Too safe": decision 6's table scaled, a plate's strength going as its thickness,
    /// as E5's <see cref="SimSettings.RuptureMPaMm"/>): a thin hull gives at what a thin hull gives at.</summary>
    public static void GiveKPa(ShipClass c, ShipCondition k, int room, SimSettings s, Span<double> kpa)
    {
        Span<double> mm = stackalloc double[kpa.Length];
        GiveMm(c, k, room, mm);
        for (int b = 0; b < kpa.Length; b++)
            kpa[b] = s.MagazineGiveKPa[b] * mm[b] / s.MagazineGiveMm[b];
    }

    /// <summary>What a magazine's boundaries open when they give, m²: the research's table for the faces of Lion's Q, by this
    /// one's (docs/battle/magazines.md "Too safe"): a tear goes with the plate it's in, so a small ship's isn't a battleship's
    /// and doesn't let her gas out as one does.</summary>
    public static void GiveM2(ShipClass c, int room, SimSettings s, Span<double> m2)
    {
        double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y0 = x0, y1 = x1, z0 = x0, z1 = x1;
        foreach (int cell in c.Links(c.Rooms[room].Cells))
        {
            var b = c.Cells[cell].Box;
            (x0, x1, y0, y1, z0, z1) = (Math.Min(x0, b.X0), Math.Max(x1, b.X1), Math.Min(y0, b.Y0), Math.Max(y1, b.Y1), Math.Min(z0, b.Base), Math.Max(z1, b.Top));
        }
        double dx = Math.Max(x1 - x0, 0), dy = Math.Max(y1 - y0, 0), dz = Math.Max(z1 - z0, 0);
        m2[0] = s.MagazineGiveM2[0] * dy * dz / s.MagazineGiveFaceM2[0];
        m2[1] = s.MagazineGiveM2[1] * dx * dy / s.MagazineGiveFaceM2[1];
        m2[2] = s.MagazineGiveM2[2] * dx * dz / s.MagazineGiveFaceM2[2];
        m2[3] = s.MagazineGiveM2[3] * dx * dy / s.MagazineGiveFaceM2[3];
    }

    /// <summary>The steel at a magazine's boundaries, mm: its thinnest bulkhead to another space (the gas finds the weakest
    /// way on), the thickest deck on the way up from it and the plates between it and the sea at its side (a belt's in the
    /// shell's), each the mean over its spaces by their faces, and its bottom (the shell and inner bottom).</summary>
    public static void GiveMm(ShipClass c, ShipCondition k, int room, Span<double> mm)
    {
        double bulkhead = double.PositiveInfinity, deck = 0, deckArea = 0, side = 0, sideArea = 0;
        var cells = c.Links(c.Rooms[room].Cells);
        foreach (int cell in cells)
        {
            var box = c.Cells[cell].Box;
            foreach (ref readonly var e in c.Neighbours(c.Cells[cell].Neighbours))
                if (e.Join == JoinKind.Bulkhead && c.Cells[e.Cell].Room != room)
                    bulkhead = Math.Min(bulkhead, Blast.JoinMm(c, e, box, c.Cells[e.Cell].Box));
            double plan = (box.X1 - box.X0) * (box.Y1 - box.Y0), elevation = (box.X1 - box.X0) * (box.Top - box.Base);
            (deck, deckArea) = (deck + plan * DeckOver(c, k, cell), deckArea + plan);
            (side, sideArea) = (side + elevation * SideOf(c, k, cell), sideArea + elevation);
        }
        var pl = c.Plating;
        double xm = cells.Length > 0 ? (c.Cells[cells[0]].Box.X0 + c.Cells[cells[0]].Box.X1) / 2 : 0;
        bool mid = xm >= pl.MidX0 && xm <= pl.MidX1;
        mm[0] = double.IsFinite(bulkhead) ? bulkhead : pl.BulkheadMm;
        mm[1] = deckArea > 0 ? deck / deckArea : pl.DeckMm;
        mm[2] = sideArea > 0 ? side / sideArea : mid ? pl.ShellMm : pl.ShellEndMm;
        mm[3] = (mid ? pl.ShellMm : pl.ShellEndMm) + pl.InnerBottomMm;
    }

    /// <summary>The thickest deck over a space, up to the weather.</summary>
    static double DeckOver(ShipClass c, ShipCondition k, int cell)
    {
        double most = 0;
        for (int guard = 0; guard < 16; guard++)
        {
            var box = c.Cells[cell].Box;
            if (Blast.Outside(c, k, cell, 2, out double top, out _) > 0)
                return Math.Max(most, top);
            int next = -1;
            double x = (box.X0 + box.X1) / 2, y = (box.Y0 + box.Y1) / 2;
            foreach (ref readonly var e in c.Neighbours(c.Cells[cell].Neighbours))
            {
                var o = c.Cells[e.Cell].Box;
                if (e.Join == JoinKind.Bulkhead || o.Base < box.Top - 0.05 || x < o.X0 || x > o.X1 || y < o.Y0 || y > o.Y1)
                    continue;
                if (e.Join == JoinKind.Deck)
                    most = Math.Max(most, Blast.JoinMm(c, e, box, o));
                next = e.Cell;
                break;
            }
            if (next < 0)
                return most;
            cell = next;
        }
        return most;
    }

    /// <summary>The plates between a space and the sea, out to starboard (she's built alike to port): the bulkheads on the
    /// way and the shell with her belt there.</summary>
    static double SideOf(ShipClass c, ShipCondition k, int cell)
    {
        double sum = 0;
        for (int guard = 0; guard < 16; guard++)
        {
            var box = c.Cells[cell].Box;
            if (Blast.Outside(c, k, cell, 1, out double shell, out _) > 0)
                return sum + shell;
            int next = -1;
            double x = (box.X0 + box.X1) / 2, z = (box.Base + box.Top) / 2;
            foreach (ref readonly var e in c.Neighbours(c.Cells[cell].Neighbours))
            {
                var o = c.Cells[e.Cell].Box;
                if (e.Join == JoinKind.Deck || o.Y0 < box.Y1 - 0.05 || x < o.X0 || x > o.X1 || z < o.Base || z > o.Top)
                    continue;
                if (e.Join == JoinKind.Bulkhead)
                    sum += Blast.JoinMm(c, e, box, o);
                next = e.Cell;
                break;
            }
            if (next < 0)
                return sum + c.Plating.ShellMm;
            cell = next;
        }
        return sum + c.Plating.ShellMm;
    }

    /// <summary>The hull girder at the magazines alight (V4; 07 §3.1): their place along her (the hull's cells' length,
    /// the stern 0), the moment there over what's left of the section's capacity; an end magazine that lost
    /// <see cref="SimSettings.MagazineEndD"/> of it without breaking her takes her end off.</summary>
    static void Girder(ShipClass c, ReadOnlySpan<int> lit, double d, double sea, SimSettings s, ref RaceResult res)
    {
        double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity, mx = 0, mm = 0;
        foreach (ref readonly var cell in c.Cells)
            (x0, x1) = (Math.Min(x0, cell.Box.X0), Math.Max(x1, cell.Box.X1));
        foreach (int rm in lit)
        {
            double t = Math.Max(c.Rooms[rm].Tonnes, 1);
            (mx, mm) = (mx + t * (c.Rooms[rm].X0 + c.Rooms[rm].X1) / 2, mm + t);
        }
        double station = x1 > x0 ? Math.Clamp((mx / mm - x0) / (x1 - x0), 0, 1) : 0.5;
        var g = s.MagazineGirder;
        double moment = Math.Sin(Math.PI * station) * (g[1] + g[2] * Math.Clamp(sea, 0, 1.5) + g[3]);
        res.Station = station;
        res.GirderLoad = d >= 1 ? double.PositiveInfinity : moment / (g[0] * (1 - d));
        // near an end hardly any moment comes through: all her section gone there, that end falls off, she isn't broken
        res.EndLost = d >= s.MagazineEndD && Math.Min(station, 1 - station) <= s.MagazineEndShare;
    }

    /// <summary>A magazine alight: its spaces' gas and vents join the race's, and the charges of the mounts it feeds join
    /// the stow (their rounds left; those lying open: in the scuttles, the discipline's charges a station a gun, and the
    /// share with their lids off; all of them in bags alone).</summary>
    static void Light(ShipClass c, ShipCondition k, int room, ReadOnlySpan<MountState> mounts, Span<bool> fed, Span<int> lit, ref int lits,
        ref double nOpen, ref double nCased, ref double massSum, in FlashDiscipline flash, SimSettings s, int id, ref double v, ref double a)
    {
        lit[lits++] = room;
        foreach (int cell in c.Links(c.Rooms[room].Cells))
            Add(c, k, cell, id, ref v, ref a, s);
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            ref readonly var mr = ref c.Mounts[m];
            if (mr.Magazine != room || mr.Battery < 0)
                continue;
            fed[m] = true;
            var ch = c.Charges[mr.Type];
            int left = m < mounts.Length ? mounts[m].Rounds : mr.Rounds;
            double rounds = left == int.MaxValue ? Math.Max(mr.Rounds, 0) : Math.Max(left, 0);
            double open = ch.Packaging == Packaging.Bag ? rounds
                : Math.Min(rounds, flash.StationCharges * c.TurretTypes[mr.Type].Barrels + flash.LidsOff * rounds);
            (nOpen, nCased, massSum) = (nOpen + open, nCased + rounds - open, massSum + rounds * ch.MassKg);
        }
    }

    /// <summary>A space's gas and its vents into the race's (once: it's marked).</summary>
    static void Add(ShipClass c, ShipCondition k, int cell, int id, ref double v, ref double a, SimSettings s)
    {
        if (k.BlastStamp[cell] == id)
            return;
        k.BlastStamp[cell] = id;
        v += Blast.GasM3(c, k, cell);
        a += Blast.VentM2(c, k, cell, s);
    }

    /// <summary>The magazines across the bulkheads of those alight light when the bulkhead gives.</summary>
    static void Neighbours(ShipClass c, ShipCondition k, ReadOnlySpan<MountState> mounts, Span<bool> fed, Span<int> lit, ref int lits,
        ref double nOpen, ref double nCased, ref double massSum, in FlashDiscipline flash, SimSettings s, int id, ref double v, ref double a)
    {
        for (int l = 0, l0 = lits; l < l0; l++)
            foreach (int cell in c.Links(c.Rooms[lit[l]].Cells))
                foreach (ref readonly var e in c.Neighbours(c.Cells[cell].Neighbours))
                {
                    if (e.Join != JoinKind.Bulkhead)
                        continue;
                    foreach (int rm in c.RoomsIn(e.Cell))
                        if (c.Rooms[rm].Kind == RoomKind.Magazine && lits < lit.Length && lit[..lits].IndexOf(rm) < 0)
                            Light(c, k, rm, mounts, fed, lit, ref lits, ref nOpen, ref nCased, ref massSum, flash, s, id, ref v, ref a);
                }
    }

    /// <summary>The share of a magazine's stow under water: its water over its spaces' room for it.</summary>
    static double Flooded(ShipClass c, ShipCondition k, int room)
    {
        double water = 0, space = 0;
        foreach (int cell in c.Links(c.Rooms[room].Cells))
        {
            ref readonly var x = ref c.Cells[cell];
            water += k.WaterM3[cell];
            space += x.VolumeM3 * Math.Clamp(x.Permeability, 0.05, 1);
        }
        return space > 0 ? Math.Clamp(water / space, 0, 1) : 0;
    }

    /// <summary>The linear burning rate, m/s, at absolute pressure p (Pa): two branches meeting at the knee.</summary>
    static double Rate(double p, in PropellantRow pr)
    {
        double pm = Math.Max(p, 0.02e6) / 1e6;
        if (pm >= pr.KneeMPa)
            return pr.BetaMmS * Math.Pow(pm, pr.AlphaHi) / 1000;
        return pr.BetaMmS * Math.Pow(pr.KneeMPa, pr.AlphaHi) * Math.Pow(pm / pr.KneeMPa, pr.AlphaLo) / 1000;
    }

    static double Choke(double gamma) => Math.Sqrt(gamma) * Math.Pow(2 / (gamma + 1), (gamma + 1) / (2 * (gamma - 1)));

    /// <summary>The gas out through a m² of openings from absolute p into the air, kg/s: choked, or below that the smaller
    /// flow.</summary>
    static double Flow(double a, double p, double rt, double choke, SimSettings s)
    {
        if (!(a > 0) || p <= Atm)
            return 0;
        double rho = p / rt;
        return Math.Min(s.MagazineVentCd * choke * a * p / Math.Sqrt(rt), s.MagazineVentCd * a * Math.Sqrt(2 * rho * (p - Atm)));
    }

    /// <summary>The scuttles given: the handling rooms of the mounts the magazines alight feed join the gas.</summary>
    static void Scuttles(ShipClass c, ShipCondition k, ReadOnlySpan<bool> fed, int id, ref double v, ref double a, SimSettings s)
    {
        for (int m = 0; m < fed.Length; m++)
        {
            if (!fed[m])
                continue;
            foreach (var st in c.Train(m))
                if (st.Kind == StationKind.Handling)
                    foreach (int cell in c.Links(c.Rooms[st.Room].Cells))
                        Add(c, k, cell, id, ref v, ref a, s);
        }
    }

    /// <summary>The ways up the mounts open as the pressure stands (kPa gauge): an enclosed mount's trunk once its roof
    /// gives (a share of its lift) or its gunhouse lifts, a shield's or casemate's hoist at the scuttle's pressure. The
    /// vent area they add.</summary>
    static double Trunks(ShipClass c, ShipCondition k, ReadOnlySpan<bool> fed, Span<bool> vented, double kpa, double t, ref RaceResult res,
        SimSettings s)
    {
        double add = 0;
        for (int m = 0; m < fed.Length; m++)
        {
            if (!fed[m])
                continue;
            double lift = c.LiftKPa(m);
            if (lift > 0)
            {
                if (!vented[m] && kpa >= s.MagazineRoofShare * lift)
                {
                    vented[m] = true;
                    add += s.MagazineTrunkShare * c.BoreM2(m);
                    res.Vented++;
                    if (double.IsNaN(res.VentS))
                        res.VentS = t;
                }
                if (vented[m] && kpa >= lift && k.State[c.Mounts[m].Component] != PartState.Destroyed)
                {
                    res.Lifted++;
                    k.Worsen(c.Mounts[m].Component, PartState.Destroyed);   // thrown: it can't lift twice
                }
            }
            else if (!vented[m] && kpa >= s.MagazineScuttleKPa)
            {
                vented[m] = true;
                add += s.MagazineHoistM2;
            }
        }
        return add;
    }

    /// <summary>What the race left (a lost ship's too: her men in the trains): the magazines alight spent, the vented
    /// gunhouses and their barbettes destroyed and burning, the trains' men flashed (a contained fire, the magazine's own),
    /// holes to the sea where its sides or bottom gave.</summary>
    static void Aftermath(ShipClass c, ShipCondition k, int room, ReadOnlySpan<int> lit, ReadOnlySpan<bool> vented, int gave, double sideM2, double bottomM2,
        ref RaceResult res, SimSettings s, long now, ref RandomStream r)
    {
        Casualties.Begin(k, now);
        foreach (int rm in lit)
        {
            k.Worsen(c.RoomNode(rm), PartState.Destroyed);
            if (res.Tier == MagazineTier.Contained)
                foreach (int cell in c.Links(c.Rooms[rm].Cells))
                    Casualties.Flash(k, Casualties.Cell(c, cell), true, s);
            else
                Casualties.Train(c, k, rm, s);
        }
        for (int m = 0; m < vented.Length; m++)
        {
            if (!vented[m] || !c.Enclosed(m))
                continue;
            ref readonly var mr = ref c.Mounts[m];
            k.Worsen(mr.Component, PartState.Destroyed);
            if (mr.Barbette >= 0)
                k.Worsen(mr.Barbette, PartState.Destroyed);
            Fires.Ignite(c, k, c.FireOfPart(mr.Component), s.ReadyAmmoSeatMW, s);
        }
        // an end magazine took her end off: everything beyond it toward that end open to the sea at its cut (New Orleans)
        if (res.EndLost)
        {
            ref readonly var mag = ref c.Rooms[room];
            bool bow = res.Station > 0.5;
            for (int cell = 0; cell < c.Cells.Length; cell++)
            {
                var b = c.Cells[cell].Box;
                if (bow ? b.X0 < mag.X0 : b.X1 > mag.X1)
                    continue;
                double xm = (b.X0 + b.X1) / 2;
                k.Hole(cell, s.RupturedShare * (b.Y1 - b.Y0) * (b.Top - b.Base), new P3(xm, (b.Y0 + b.Y1) / 2, b.Base));
            }
        }
        // the sea in where its sides or bottom gave, at the magazine's middle
        if (!res.Lost && (gave & 12) != 0 && c.Links(c.Rooms[room].Cells) is { Length: > 0 } cells)
        {
            int cell = cells[0];
            var b = c.Cells[cell].Box;
            double xm = (b.X0 + b.X1) / 2, zm = (b.Base + b.Top) / 2;
            if ((gave & 4) != 0)
                k.Hole(cell, sideM2, new P3(xm, c.Hull.HalfWidth(xm, zm), zm));
            if ((gave & 8) != 0)
                k.Hole(cell, bottomM2, new P3(xm, 0, b.Base));
        }
        var fx = new EffectsResult { Part = -1, MagazineRoom = -1 };
        Casualties.Draw(c, k, now, s, ref fx, ref r);
        (res.Killed, res.Wounded) = (fx.Killed, fx.Wounded);
        k.Version++;
    }
}
