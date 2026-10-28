using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A repair party's job besides fighting a fire (docs/battle/damage-control.md DC3; 09 §4): shut a watertight door
/// left open, re-dog an edge's loosened closures, caulk its sprung seams or shore it (from its dry side), plug a cell's
/// small holes, patch its medium ones, caulk its sprung plating. The settings' job tables are by these.</summary>
public enum RepairJob : byte
{
    None,
    Door,
    Redog,
    Caulk,
    Shore,
    Plug,
    Patch,
    Seam,
    /// <summary>Rig a portable pump in a flooded space (DC4).</summary>
    RigPump,
    /// <summary>A collision mat over a cell's large holes near the waterline, from outside, the ship slowed (DC6).</summary>
    Mat,
    /// <summary>Steering by hand or the steam gear aft rigged, the gear's failure (DC7).</summary>
    Steering,
    /// <summary>A disabled part brought back to damaged: a mount, a tube, a top, a machinery room (DC7).</summary>
    Repair,
    /// <summary>A room flooded out of action brought back once pumped down (DC7).</summary>
    Restore,
    /// <summary>A deck gone under the sea battened down: its hatches and scuttles closed from above (DC9).</summary>
    Batten,
}

/// <summary>What damage control did or met, for the log (docs/battle/damage-control.md DC8): boundaries gave way under the
/// head; her steering went to engines or to hand; a part was repaired; a room flooded out of action was brought back; the
/// counterflood tanks on a side are full (the doom clock run out); damage control asked for a lower speed.</summary>
public enum DcEvent : byte
{
    Gave,
    Steering,
    Repaired,
    Restored,
    CounterfloodOut,
    SafeSpeed,
}

/// <summary>A damage-control event waiting for the log: what, the part (or -1), a number with it.</summary>
public readonly record struct DcNote(DcEvent What, int Part, int Value);

/// <summary>What a repairs step changed that the ship must answer: her steering, or her parts (what she can do worked out
/// again).</summary>
public enum RepairChange : byte
{
    None,
    Steering,
    Parts,
}

/// <summary>REPAIRS (docs/battle/damage-control.md DC3; 09 §3-§4, 10 §5.1): her repair parties, one pool of men for fire
/// and water, made at her first damage (a share of her complement, two to eight parties spread along her). Every
/// <see cref="SimSettings.FirePlanS"/> the free ones are sent to the worst job by her side's doctrine: fires next to a
/// magazine, open doors on the flooding boundary, the water jobs ranked by the flow through them × what's behind
/// (a magazine ×3, machinery and steering ×2), other fires, then holes near the waterline that don't leak yet; a
/// flood-first doctrine puts the water before the magazine's fires. A job is man-minutes from the catalogue, worked at the
/// party's men (at most twice the job's crew), the doctrine's skill, slower in the dark and on a list; done, it marks the
/// edge or plugs, patches or caulks the cell's holes of its class. The water's jobs need their dry side (or the hole
/// above the water inside, the water under the reach). The men on the spot shut an open door at the flooding boundary
/// after the watch's delay. Pure and allocation-free; fires' water is <see cref="Fires"/>'.</summary>
public static class Repairs
{
    const double G = 9.81;

    /// <summary>The tiers of the doctrine's order, fire first (10 §5.1); flood first swaps the magazine's fires below the
    /// water's.</summary>
    enum Tier : byte { Parts = 1, Pump = 2, Near = 3, Fire = 4, Steer = 5, Water = 6, Door = 7, MagazineFire = 8 }

    public static RepairChange Step(ShipClass c, FloodModel f, ShipCondition k, double dt, long now, SimSettings s)
    {
        var r = RandomStream.FromSeed(0xF1C5);
        return Step(c, f, k, dt, now, 0, s, ref r);
    }

    public static RepairChange Step(ShipClass c, FloodModel f, ShipCondition k, double dt, long now, double speedMs, SimSettings s,
        ref RandomStream rng)
    {
        ref readonly var doc = ref k.DamageControl;
        if (k.Status != ShipStatus.Afloat)
            return RepairChange.None;
        var change = Steer(c, k, now, s);
        if (doc.Name is null || !k.Damaged && k.Fires == 0)
            return change;
        Make(c, f, k, now, s);
        Watch(c, f, k, now, s);
        Counterflood(c, f, k, dt, now, s);
        Loosen(k, dt, speedMs, s);
        if (now >= k.NextPlanMs)
        {
            Safe(c, f, k, now, speedMs, s);
            Draw(c, f, k, s, ref rng);
        }
        PlanIfDue(c, f, k, now, s);
        var work = Work(c, f, k, dt, now, speedMs, s);
        return work > change ? work : change;
    }

    // ------------------------------------------------------------------ steering and parts (DC7)

    /// <summary>Whether her rudder itself is out (beyond repair at sea).</summary>
    public static bool RudderHit(ShipCondition k)
    {
        var comps = k.Class.Components;
        for (int i = 0; i < comps.Length; i++)
            if (comps[i].Kind == ComponentKind.Rudder && k.State[i] >= PartState.Disabled)
                return true;
        return false;
    }

    /// <summary>Her shafts still turning.</summary>
    static int Shafts(ShipClass c, ShipCondition k)
    {
        int n = 0;
        var comps = c.Components;
        for (int i = 0; i < comps.Length; i++)
            n += comps[i].Kind == ComponentKind.Shaft && k.State[i] < PartState.Disabled && (k.Flags[i] & PartFlags.Down) == 0 ? 1 : 0;
        return n;
    }

    /// <summary>The bridge steers by her engines once the steering's gone a while (10 §3: two shafts or more, a share of a
    /// rudder's turn): the gear's failure waits there for a party to rig hand steering, the rudder's for good.</summary>
    static RepairChange Steer(ShipClass c, ShipCondition k, long now, SimSettings s)
    {
        if (k.Steering is not (Steering.Jammed or Steering.Lost) || now - k.SteerSinceMs < s.EnginesSteerS * 1000 || Shafts(c, k) < 2)
            return RepairChange.None;
        k.Steering = Steering.Engines;
        k.Note(DcEvent.Steering, -1, (int)Steering.Engines);
        return RepairChange.Steering;
    }

    /// <summary>The part kinds damage control can fix at sea (the settings' columns): mounts, torpedo tubes, tops (their
    /// directors and rangefinders), boiler and engine rooms; -1 none.</summary>
    static int PartKind(ShipClass c, int node)
    {
        if (node < c.Components.Length)
            return c.Components[node].Kind switch
            {
                ComponentKind.Main or ComponentKind.Secondary or ComponentKind.Aa or ComponentKind.Torpedo => 0,
                ComponentKind.TorpedoTube => 1,
                ComponentKind.Top => 2,
                _ => -1,
            };
        return c.Rooms[node - c.Components.Length].Kind is RoomKind.BoilerRoom or RoomKind.EngineRoom ? 3 : -1;
    }

    /// <summary>Each part newly out drawn once: can it be fixed at sea (by its kind's chance), and how long; a room flooded
    /// out of action its time to bring back once pumped down.</summary>
    static void Draw(ShipClass c, FloodModel f, ShipCondition k, SimSettings s, ref RandomStream r)
    {
        for (int node = 0; node < k.State.Length; node++)
        {
            if (k.State[node] != PartState.Disabled || k.Fix[node] != 0)
                continue;
            if (k.Drowned[node])
            {
                bool machinery = PartKind(c, node) == 3;
                double lo = s.RestoreMin[machinery ? 0 : 2], hi = s.RestoreMin[machinery ? 1 : 3];
                (k.Fix[node], k.FixManMin[node]) = (1, (lo + r.NextDouble() * (hi - lo)) * s.RepairPartMen);
                continue;
            }
            int kind = PartKind(c, node);
            if (kind < 0 || r.NextDouble() >= s.RepairPartShare[kind])
            {
                k.Fix[node] = 2;
                continue;
            }
            double a = s.RepairPartMin[2 * kind], b = s.RepairPartMin[2 * kind + 1];
            (k.Fix[node], k.FixManMin[node]) = (1, (a + r.NextDouble() * (b - a)) * s.RepairPartMen);
        }
    }

    /// <summary>Whether she has repairs still to come: steering to take over or rig, parts newly out or to fix, rooms to
    /// bring back (her damage system keeps stepping her while so).</summary>
    public static bool Busy(ShipClass c, ShipCondition k)
    {
        if (k.Steering is Steering.Jammed or Steering.Lost || k.Steering == Steering.Engines && k.SteerCause == SteerCause.Gear)
            return true;
        if (k.DamageControl.Name is null)
            return false;
        for (int node = 0; node < k.State.Length; node++)
            if (k.State[node] == PartState.Disabled && (k.Fix[node] == 1 || k.Fix[node] == 0 && (k.Drowned[node] || PartKind(c, node) >= 0)))
                return true;
        return false;
    }

    /// <summary>A room's water as a share of its cells' capacity, and whether a party can work in it (not burning).</summary>
    static (double Share, bool Burning) RoomWater(ShipClass c, FloodModel f, ShipCondition k, int room)
    {
        double w = 0, cap = 0;
        bool burning = false;
        foreach (int i in c.Links(c.Rooms[room].Cells))
            (w, cap, burning) = (w + k.WaterM3[i], cap + f.Capacity[i], burning | k.CellFire[i]);
        return (cap > 0 ? w / cap : 0, burning);
    }

    /// <summary>The section a part or room is in.</summary>
    static int SectionOf(ShipClass c, int node)
    {
        if (node < c.Components.Length)
        {
            var b = c.Components[node].Bounds;
            return c.SectionAt((b.X0 + b.X1) / 2);
        }
        foreach (int i in c.Links(c.Rooms[node - c.Components.Length].Cells))
            return c.Cells[i].Section;
        return 0;
    }

    /// <summary>The steering room (-1: none), its section (aft when there's none).</summary>
    static (int Room, int Section) SteeringRoom(ShipClass c)
    {
        for (int r = 0; r < c.Rooms.Length; r++)
            if (c.Rooms[r].Kind == RoomKind.Steering)
                return (r, SectionOf(c, c.Components.Length + r));
        return (-1, c.Sections.Length - 1);
    }

    // ------------------------------------------------------------------ speed (DC6)

    /// <summary>The speed damage control asks for (09 §2.2, 17 §5; Seydlitz, Lützow, Bismarck): the highest at which every
    /// boundary of a holed space forward stays under <see cref="SimSettings.SafeLoadShare"/> of its rating with the ram head
    /// on it (at least <see cref="SimSettings.SafeMinKn"/>, steerage); steerage while her deck forward is under the sea
    /// (<see cref="DeckUnder"/>); no more than <see cref="SimSettings.HullWorkKn"/>
    /// while the work on her hull's holes holds back more than her pumps could take if it worked loose; no more than
    /// <see cref="SimSettings.MatKn"/> while a mat goes over (only started on holes the pumps can't keep up with). When it
    /// first asks for less than she's making is when the doctrine's hold time starts.</summary>
    static void Safe(ShipClass c, FloodModel f, ShipCondition k, long now, double speedMs, SimSettings s)
    {
        double safe = double.PositiveInfinity;
        var edges = f.Edges;
        for (int ei = 0; ei < edges.Length; ei++)
        {
            ref readonly var e = ref edges[ei];
            if (k.BreachM2[e.A] <= 0 && k.BreachM2[e.B] <= 0 || (k.JoinMarks[e.Pool] & JoinMark.Collapsed) != 0)
                continue;
            var (load, ramK) = Flooding.EdgeLoad(c, f, k, ei, s);
            if (ramK <= 0)
                continue;
            double room = s.SafeLoadShare * Flooding.Rating(f, k, ei, e.Pool, s) - load;
            safe = Math.Min(safe, room <= 0 ? 0 : Math.Sqrt(room / ramK) / Kn);
        }
        if (DeckUnder(c, f, k, s))
            safe = 0;   // steerage way, below
        if (safe > s.HullWorkKn && Held(c, f, k, s) > Pumps(c, k))
            safe = s.HullWorkKn;
        for (int p = 0; p < k.PartyCount; p++)
            if (k.Parties[p].Job == RepairJob.Mat)
                safe = Math.Min(safe, s.MatKn);
        if (double.IsFinite(safe))
            safe = Math.Max(safe, s.SafeMinKn);
        bool asks = safe < Math.Abs(speedMs) / Kn - 0.5;
        if (asks && (!double.IsFinite(k.SafeSpeedKn) || k.SafeSinceMs == 0))
        {
            k.SafeSinceMs = Math.Max(now, 1);
            k.Note(DcEvent.SafeSpeed, -1, (int)Math.Round(safe));
        }
        else if (!double.IsFinite(safe))
            k.SafeSinceMs = 0;
        k.SafeSpeedKn = safe;
    }

    const double Kn = 0.514444;

    /// <summary>Her deck is under the sea where going ahead makes it worse (the Åland card, 2026-10-26): a weather deck in
    /// the bow's ram under the sea at rest (every knot drives more of it over the deck and in), or a deck whose press
    /// drives her down (<see cref="Ploughing.Deck"/>). Damage control asks for steerage way: both go as the speed squared.</summary>
    static bool DeckUnder(ShipClass c, FloodModel f, ShipCondition k, SimSettings s)
    {
        if (k.Attitude == Attitude.Upright)
            return false;
        var water = HitQuery.WaterOf(c, k.Attitude);
        for (int i = 0; i < c.Cells.Length; i++)
            if (f.Weather[i] && water.At(f.Cx[i], f.Cy[i]) > c.Cells[i].Box.Top && Flooding.Ram(c, f.Cx[i], 1, s) > 0)
                return true;
        return Ploughing.Deck(c, f, k.Attitude, true, s).DownM2 > 0;
    }

    /// <summary>What the work on her hull's holes holds back, m³/s: their area worked on × the share stopped, at each cell's
    /// full head (what would come in if it all worked loose).</summary>
    static double Held(ShipClass c, FloodModel f, ShipCondition k, SimSettings s)
    {
        var water = HitQuery.WaterOf(c, k.Attitude);
        double held = 0;
        int cells = k.HoleDoneM2.Length / ShipCondition.HoleClasses;
        for (int i = 0; i < cells; i++)
        {
            double sea = water.At(f.Cx[i], k.BreachY[i]);
            for (int h = i * ShipCondition.HoleClasses; h < (i + 1) * ShipCondition.HoleClasses; h++)
            {
                double stopped = k.HoleDoneM2[h] * (1 - k.HoleLeft[h]), head = sea - Math.Max(k.HoleZ[h], c.Cells[i].Box.Base);
                if (stopped > 0 && head > 0)
                    held += s.FloodCd * stopped * Math.Sqrt(2 * 9.81 * head);
            }
        }
        return held;
    }

    /// <summary>What her pumps can take now, m³/s: the drainage on her power and the portable pumps she carries.</summary>
    static double Pumps(ShipClass c, ShipCondition k) =>
        (c.Drainage.PumpsTph * k.PowerCap + c.Drainage.PortablePumps * c.Drainage.PortableTph) / FloodModel.SeaT / 3600;

    /// <summary>Caulking, plugs, patches and mats on the hull work loose at speed (17 §5.2, 13: 15% back toward open every
    /// 10 min above <see cref="SimSettings.HullWorkKn"/>; Bismarck's mat).</summary>
    static void Loosen(ShipCondition k, double dt, double speedMs, SimSettings s)
    {
        if (Math.Abs(speedMs) / Kn <= s.HullWorkKn)
            return;
        double creep = 1 - Math.Pow(1 - s.HullWorkLoosen, dt / 600);
        int cells = k.HoleDoneM2.Length / ShipCondition.HoleClasses;
        for (int i = 0; i < cells; i++)
        {
            bool any = false;
            for (int h = i * ShipCondition.HoleClasses; h < (i + 1) * ShipCondition.HoleClasses; h++)
                if (k.HoleDoneM2[h] > 0 && k.HoleLeft[h] < 1)
                {
                    k.HoleLeft[h] += (1 - k.HoleLeft[h]) * creep;
                    any = true;
                }
            if (any)
                k.Recount(i);
        }
    }

    // ------------------------------------------------------------------ DC Central (DC5)

    /// <summary>DC Central's counterflooding (09 §2.5, §3; WDR 61): past the doctrine's list it opens the narrow wing tank on
    /// the high side whose fill best matches the water the list needs (Δ·GM·tanφ over its lever), and fills it to full (a
    /// tank left part full is free surface: NC's one at a time), the next when the list still asks; past its trim, when
    /// there's no list to answer or nothing to answer it with, a centreline tank at the other end (with her GM gone too:
    /// the loll's rule is the high side's). It shuts a valve early only when the list has gone past the doctrine's the other
    /// way. At
    /// the design's valves' rate while she's conned (DC Central's stand-in until the designer has the station), by hand at
    /// a share of it after a delay when she isn't. The tanks it fills the pumps leave alone; when the high side's are
    /// full, the list grows (the doom clock).</summary>
    static void Counterflood(ShipClass c, FloodModel f, ShipCondition k, double dt, long now, SimSettings s)
    {
        ref readonly var doc = ref k.DamageControl;
        double rate = c.Drainage.CounterfloodTpm / 60 / FloodModel.SeaT;   // m³/s
        if (rate <= 0 || doc.CounterfloodDeg < 0)
            return;
        if (!k.Conned)
        {
            if (k.CounterHandMs == 0)
                k.CounterHandMs = Math.Max(now, 1);
            if (now - k.CounterHandMs < s.CounterfloodHandS * 1000)
                return;
            rate *= s.CounterfloodHandShare;
        }
        double heel = k.Attitude.HeelDeg, trim = k.Attitude.TrimDeg;
        int t = k.CounterTank;
        if (t >= 0)
        {
            // the valve shut once the list (or the trim it was opened for) is put right, or the tank is full or holed
            bool forList = Math.Abs(f.Cy[t]) >= 1;
            bool done = forList ? heel * f.Cy[t] > 0 && Math.Abs(heel) > doc.CounterfloodDeg
                : trim * (f.Cx[t] - c.Hydrostatics.Lcf) > 0 && Math.Abs(trim) > doc.CounterfloodTrimDeg;
            if (done || k.WaterM3[t] >= f.Capacity[t] * 0.999 || k.BreachM2[t] > 0)
                k.CounterTank = t = -1;
        }
        if (t < 0 && Math.Abs(heel) > doc.CounterfloodDeg && Deballast(c, f, k, heel > 0 ? 1 : -1, dt))
            return;   // the list is now toward water it let in itself: that goes out first
        if (t < 0)
        {
            // a list with her GM gone is a loll: flooding the high side would throw her over (Ralph Talbot's lesson)
            if (Math.Abs(heel) > doc.CounterfloodDeg && k.GmM >= s.CounterfloodMinGmM)
            {
                int side = heel > 0 ? -1 : 1;
                t = Tank(c, f, k, side, 0, Needed(c, k, heel));
                byte mark = (byte)(side < 0 ? 1 : 2);
                if (t < 0 && (k.CounterOut & mark) == 0 && CounterfloodLeftT(f, k, side) < 1)
                {
                    k.CounterOut |= mark;   // the high side's tanks full: the list can only grow now
                    k.Note(DcEvent.CounterfloodOut, -1, side);
                }
            }
            if (t < 0 && doc.CounterfloodTrimDeg >= 0 && Math.Abs(trim) > doc.CounterfloodTrimDeg)
                t = Tank(c, f, k, 0, trim > 0 ? -1 : 1, 0);
            if (t < 0)
                return;
            k.CounterTank = t;
        }
        double v = Math.Min(rate * dt, f.Capacity[t] - k.WaterM3[t]);
        if (v <= 0)
            return;
        k.WaterM3[t] += v;
        k.Counterflooded[t] = true;
        k.CounterfloodT += v * FloodModel.SeaT;
    }

    /// <summary>Counterflood water on the low side pumped back out by the drainage pumps (on her power), the outermost tank
    /// first: when the flooding that was answered drains away (a holed side lifted clear) the list swings toward it.
    /// Whether there was any to pump.</summary>
    static bool Deballast(ShipClass c, FloodModel f, ShipCondition k, int side, double dt)
    {
        int best = -1;
        foreach (int i in f.CounterTanks)
            if (k.Counterflooded[i] && k.WaterM3[i] > 0 && k.BreachM2[i] <= 0 && side * f.Cy[i] > 0.5 &&
                (best < 0 || side * f.Cy[i] > side * f.Cy[best]))
                best = i;
        if (best < 0)
            return false;
        double v = Math.Min(k.WaterM3[best], c.Drainage.PumpsTph / FloodModel.SeaT / 3600 * k.PowerCap * dt);
        if (v <= 0)
            return false;
        k.WaterM3[best] -= v;
        k.CounterfloodT -= v * FloodModel.SeaT;
        if (k.WaterM3[best] <= 1e-6)
            (k.WaterM3[best], k.Counterflooded[best]) = (0, false);
        return true;
    }

    /// <summary>The heeling moment a list stands for, t·m (Δ·GM·tanφ, with her water in): what counterflooding must answer.</summary>
    static double Needed(ShipClass c, ShipCondition k, double heelDeg) =>
        (c.Hydrostatics.DisplacementT + k.WaterT) * Math.Max(k.GmM, 0.1) * Math.Tan(double.DegreesToRadians(Math.Abs(heelDeg)));

    /// <summary>The tank to open: on a side (-1 port, +1 starboard) the one whose free room × lever comes nearest the moment
    /// needed (a longer lever among near equals), or at an end (-1 aft, +1 forward) the centreline one with the longest
    /// lever along; with room, not holed (the sea's in it already).</summary>
    static int Tank(ShipClass c, FloodModel f, ShipCondition k, int side, int end, double moment)
    {
        int best = -1;
        double top = double.NegativeInfinity, lcf = c.Hydrostatics.Lcf;
        foreach (int i in f.CounterTanks)
        {
            double room = (f.Capacity[i] - k.WaterM3[i]) * FloodModel.SeaT;
            if (k.BreachM2[i] > 0 || room < 1)
                continue;
            double score;
            if (side != 0)
            {
                double lever = side * f.Cy[i];
                var b = c.Cells[i].Box;
                if (lever <= 0.5 || b.Y1 - b.Y0 > 0.25 * c.Beam)
                    continue;   // narrow wing tanks only: a wide one part full is free surface (the loll it would make)
                score = -Math.Abs(Math.Log(room * lever / Math.Max(moment, 1))) + 0.01 * lever;
            }
            else
            {
                double lever = end * (f.Cx[i] - lcf);
                if (lever <= 0.1 * c.Length || Math.Abs(f.Cy[i]) >= 1)
                    continue;
                score = lever;
            }
            if (score > top)
                (best, top) = (i, score);
        }
        return best;
    }

    /// <summary>The tonnes DC Central could still let in on a side (-1 port, +1 starboard): the doom clock's (DC8).</summary>
    public static double CounterfloodLeftT(FloodModel f, ShipCondition k, int side)
    {
        double left = 0;
        foreach (int i in f.CounterTanks)
            if (side * f.Cy[i] > 0.5 && k.BreachM2[i] <= 0)
                left += Math.Max(f.Capacity[i] - k.WaterM3[i], 0) * FloodModel.SeaT;
        return left;
    }

    /// <summary>The parties planned, every <see cref="SimSettings.FirePlanS"/> (the fire's step asks too, for the tools
    /// that step fire alone).</summary>
    public static void PlanIfDue(ShipClass c, FloodModel f, ShipCondition k, long now, SimSettings s)
    {
        if (now < k.NextPlanMs)
            return;
        Plan(c, f, k, now, s);
        k.NextPlanMs = now + (long)(s.FirePlanS * 1000);
    }

    /// <summary>Her parties, once (fire_ref.py's Sim): the doctrine's share of her repair men there now (docs/battle/crew.md
    /// N1: one book of men, the battle station's in her quarters and stores), two to eight of them, spread along her, each
    /// to muster in the repair men's space nearest its section. Their men stay at their stations until a party is first
    /// sent (<see cref="Plan"/>: it takes those still there), so the men standing by are where they were.</summary>
    public static void Make(ShipClass c, FloodModel f, ShipCondition k, long now, SimSettings s)
    {
        if (k.PartiesMade)
            return;
        ref readonly var doc = ref k.DamageControl;
        int groups = c.Components.Length + c.Cells.Length, there = 0;
        for (int g = 0; g < groups; g++)
            if (BattleStations.Of(c, g) == BattleStation.Repair)
                there += Casualties.Men(c, k, g);
        int n = Math.Clamp((int)Math.Round(doc.PartyShare * there / Math.Max(doc.PartyMen, 1)), s.FirePartiesMin, Math.Min(s.FirePartiesMax, ShipCondition.MaxParties));
        for (int i = 0; i < n; i++)
        {
            int section = (int)((i + 0.5) * c.Sections.Length / n);
            k.Parties[i] = new RepairParty
            {
                Men = doc.PartyShare * there / n, Skill = 1, Section = section, Target = -1, JobAt = -1, Home = Muster(c, section), Place = -1,
            };
        }
        (k.PartyCount, k.PartiesMade, k.NextPlanMs) = (n, true, now);
    }

    /// <summary>Men taken from a station's groups into a party (docs/battle/crew.md N1), each group giving its share of the
    /// wanted (whole men, the remainder carried on), away from it from now: how many, and the group that gave the most.</summary>
    static int Take(ShipClass c, ShipCondition k, BattleStation station, double want, out int most)
    {
        int groups = c.Components.Length + c.Cells.Length, there = 0;
        for (int g = 0; g < groups; g++)
            if (BattleStations.Of(c, g) == station)
                there += Casualties.Men(c, k, g);
        (most, int taken, int best) = (-1, 0, 0);
        if (there <= 0)
            return 0;
        double share = Math.Min(want / there, 1), carry = 0;
        for (int g = 0; g < groups; g++)
        {
            int men = BattleStations.Of(c, g) == station ? Casualties.Men(c, k, g) : 0;
            if (men <= 0)
                continue;
            double x = share * men + carry;
            int t = Math.Min((int)Math.Floor(x + 1e-9), men);
            carry = x - t;
            k.Away[g] += t;
            taken += t;
            if (t > best)
                (best, most) = (t, g);
        }
        return taken;
    }

    /// <summary>Where a party musters: the repair men's space (a cell of the quarters or stores) nearest its section, the
    /// fullest of those; -1 if she has none.</summary>
    static int Muster(ShipClass c, int section)
    {
        int best = -1, dist = int.MaxValue, crew = 0;
        for (int i = 0; i < c.Cells.Length; i++)
        {
            int g = Casualties.Cell(c, i);
            if (c.Cells[i].BattleCrew <= 0 || BattleStations.Of(c, g) != BattleStation.Repair)
                continue;
            int d = Math.Abs(c.Cells[i].Section - section);
            if (d < dist || d == dist && c.Cells[i].BattleCrew > crew)
                (best, dist, crew) = (g, d, c.Cells[i].BattleCrew);
        }
        return best;
    }

    /// <summary>Where a party works at a job (docs/battle/crew.md N1): a fire's space (a cell or a part; a weather deck's
    /// cell under it), a boundary's dry side, a cell's holes or pump, the steering room, a part or room under repair.</summary>
    static int PlaceAt(ShipClass c, FloodModel f, ShipCondition k, RepairJob job, int at, int home)
    {
        int RoomCell(int room)
        {
            foreach (int cell in c.Links(c.Rooms[room].Cells))
                return Casualties.Cell(c, cell);
            return home;
        }
        switch (job)
        {
            case RepairJob.None:
            {
                ref readonly var row = ref c.FireSpaces[at];
                return row.Kind switch
                {
                    FireSpaceKind.Cell => Casualties.Cell(c, row.Index),
                    FireSpaceKind.Part => Casualties.Part(row.Index),
                    _ => row.Cell >= 0 ? Casualties.Cell(c, row.Cell) : home,
                };
            }
            case RepairJob.Door:
            case RepairJob.Redog:
            case RepairJob.Caulk:
            case RepairJob.Shore:
            case RepairJob.Batten:
                return Casualties.Cell(c, Sides(c, f, k, f.Edges[at]).Lo);
            case RepairJob.Steering:
            {
                int room = SteeringRoom(c).Room;
                return room >= 0 ? RoomCell(room) : home;
            }
            case RepairJob.Repair:
            case RepairJob.Restore:
                return at < c.Components.Length ? Casualties.Part(at) : RoomCell(at - c.Components.Length);
            default:
                return Casualties.Cell(c, at);
        }
    }

    // ------------------------------------------------------------------ the men on the spot

    /// <summary>A watertight door left open with water at it is shut by the men there after the watch's delay (unless its
    /// dogs are jammed, or there's nobody in either space); an unorganised crew leaves it to the parties.</summary>
    static void Watch(ShipClass c, FloodModel f, ShipCondition k, long now, SimSettings s)
    {
        double after = k.DamageControl.WatchS;
        var edges = f.Edges;
        for (int ei = 0; ei < edges.Length; ei++)
        {
            ref readonly var e = ref edges[ei];
            var m = k.JoinMarks[e.Pool];
            if ((m & JoinMark.DoorOpen) == 0)
                continue;
            if (k.WaterM3[e.A] <= 0 && k.WaterM3[e.B] <= 0)
                continue;
            if (k.JoinWetMs[e.Pool] == 0)
                k.JoinWetMs[e.Pool] = Math.Max(now, 1);
            else if (after >= 0 && now - k.JoinWetMs[e.Pool] >= after * 1000 && (m & JoinMark.DoorJammed) == 0 &&
                     c.Cells[e.A].BattleCrew + c.Cells[e.B].BattleCrew + c.Cells[e.A].Crew + c.Cells[e.B].Crew > 0)
                k.JoinMarks[e.Pool] = m & ~JoinMark.DoorOpen;
        }
    }

    // ------------------------------------------------------------------ planning

    /// <summary>The parties sent: the gun crews turned to once the action's over, a party off a job that's done or gone,
    /// then the free ones each to the best job left, the nearest first: its muster the first time, then the sections to go.</summary>
    static void Plan(ShipClass c, FloodModel f, ShipCondition k, long now, SimSettings s)
    {
        ref readonly var doc = ref k.DamageControl;
        if (!k.TurnedTo && k.LastHitMs > 0 && now - k.LastHitMs >= s.FireTurnToS * 1000)
        {
            k.TurnedTo = true;
            // from the mounts' men (docs/battle/crew.md N1), standing by at the mount that gave the most
            for (int i = 0; i < doc.TurnTo && k.PartyCount < ShipCondition.MaxParties; i++)
            {
                int men = Take(c, k, BattleStation.Guns, Math.Max(doc.PartyMen, 2), out int from);
                k.Parties[k.PartyCount++] = new RepairParty
                {
                    Men = men, Skill = s.FireTurnToSkill, Section = c.PumpSection, Target = -1, JobAt = -1, Home = from, Place = -1, Mustered = true,
                };
            }
        }
        for (int p = 0; p < k.PartyCount; p++)
        {
            ref var party = ref k.Parties[p];
            if (party.Target >= 0 && !k.Fire[party.Target].Burning)
                party.Target = -1;
            if (party.Job != RepairJob.None && !Valid(c, f, k, party.Job, party.JobAt, s))
                (party.Job, party.JobAt) = (RepairJob.None, -1);
        }
        while (true)
        {
            bool free = false;
            for (int p = 0; p < k.PartyCount && !free; p++)
                free = k.Parties[p].Target < 0 && k.Parties[p].Job == RepairJob.None && k.Parties[p].Men >= 2;
            if (!free)
                return;   // every party at work: no need to look over the jobs
            var best = Best(c, f, k, s);
            if (best.Score <= 0)
                return;
            int near = -1;
            for (int p = 0; p < k.PartyCount; p++)
            {
                ref readonly var q = ref k.Parties[p];
                if (q.Target < 0 && q.Job == RepairJob.None && q.Men >= 2 &&
                    (near < 0 || Math.Abs(q.Section - best.Section) < Math.Abs(k.Parties[near].Section - best.Section)))
                    near = p;
            }
            if (near < 0)
                return;
            ref var party = ref k.Parties[near];
            // sent the first time, its men leave their stations: those still there of its share
            if (!party.Mustered)
                party.Men = Take(c, k, BattleStation.Repair, party.Men, out _);
            // on the way it's where it was: its last place, else where it mustered
            if (party.Place >= 0 && now >= party.ArriveMs)
                party.Home = party.Place;
            party.Place = PlaceAt(c, f, k, best.Job, best.At, party.Home);
            party.ArriveMs = now + (long)(1000 * (Math.Abs(party.Section - best.Section) * s.FireTravelS + (party.Mustered ? 0 : doc.MusterS)));
            (party.Section, party.Mustered, party.Done) = (best.Section, true, 0);
            if (best.Job == RepairJob.None)
                party.Target = best.At;
            else
                (party.Job, party.JobAt) = (best.Job, best.At);
        }
    }

    /// <summary>A candidate: a fire (Job None, At its space) or a water job, its section and its score (0: none).</summary>
    readonly record struct Candidate(RepairJob Job, int At, int Section, double Score);

    static double Score(Tier t, bool floodFirst, double weight)
    {
        int rank = (t, floodFirst) switch
        {
            (Tier.MagazineFire, true) => 6,
            (Tier.Door, true) => 8,
            (Tier.Water, true) => 7,
            _ => (int)t,
        };
        return rank * 1e9 + Math.Min(weight, 1e9 - 1);
    }

    /// <summary>The best job not yet taken.</summary>
    static Candidate Best(ShipClass c, FloodModel f, ShipCondition k, SimSettings s)
    {
        bool floodFirst = k.DamageControl.FloodFirst;
        var best = new Candidate(RepairJob.None, -1, 0, 0);
        void Offer(in Candidate x, ref Candidate b)
        {
            if (x.Score > b.Score)
                b = x;
        }

        // fires: the worst short of its parties (B4's weight), next to a magazine its own tier
        var spaces = c.FireSpaces;
        for (int i = 0; i < spaces.Length; i++)
        {
            ref readonly var x = ref k.Fire[i];
            if (!x.Burning || x.Phase == BurnPhase.Control || Sent(k, i) >= s.FirePartiesAFire)
                continue;
            bool mag = Fires.NextToMagazine(c, i);
            double weight = 1 + 1000 * (x.Q * (mag ? s.FireMagazineK : 1) + (x.Phase == BurnPhase.Grow ? 0.5 : 0));
            Offer(new Candidate(RepairJob.None, i, spaces[i].Section, Score(mag ? Tier.MagazineFire : Tier.Fire, floodFirst, weight)), ref best);
        }

        // the edges: doors, closures, seams, shores, from the dry side
        var cells = c.Cells;
        var edges = f.Edges;
        for (int ei = 0; ei < edges.Length; ei++)
        {
            ref readonly var e = ref edges[ei];
            int p = e.Pool;
            var m = k.JoinMarks[p];
            if ((m & JoinMark.Collapsed) != 0 || k.WaterM3[e.A] <= 0 && k.WaterM3[e.B] <= 0)
                continue;
            bool doorOpen = (m & JoinMark.DoorOpen) != 0;
            bool sunk = e.Join == JoinKind.Deck && (m & JoinMark.Battened) == 0 && e.Z > c.Vertical.Waterline && k.JoinFlow[p] != 0;
            if (!doorOpen && !sunk && k.JoinSeamM2[p] <= 0 && k.JoinClosureM2[p] <= 0)
                continue;
            var (hi, lo) = Sides(c, f, k, e);
            if (!DrySide(c, f, k, lo, s))
                continue;
            double flowH = Math.Abs(k.JoinFlow[p]) * 3600, value = Value(c, lo);
            int sec = cells[lo].Section;
            if (sunk && !Taken(k, RepairJob.Batten, ei))
                Offer(new Candidate(RepairJob.Batten, ei, sec, Score(Tier.Door, floodFirst, 1 + flowH * value)), ref best);
            if (doorOpen && (m & JoinMark.DoorJammed) == 0 && !Taken(k, RepairJob.Door, ei))
                Offer(new Candidate(RepairJob.Door, ei, sec, Score(Tier.Door, floodFirst, 1 + flowH * value)), ref best);
            if (flowH <= 0)
                continue;
            if (k.JoinClosureM2[p] > 0 && (m & JoinMark.Redogged) == 0 && !Taken(k, RepairJob.Redog, ei))
                Offer(new Candidate(RepairJob.Redog, ei, sec, Score(Tier.Water, floodFirst, flowH * value * Share(k, p, closures: true))), ref best);
            if (k.JoinSeamM2[p] > 0 && (m & JoinMark.Caulked) == 0 && !Taken(k, RepairJob.Caulk, ei))
                Offer(new Candidate(RepairJob.Caulk, ei, sec, Score(Tier.Water, floodFirst, flowH * value * Share(k, p, closures: false))), ref best);
            double load = Load(c, f, k, ei, s);
            if (load > s.RepairShoreAt && (m & JoinMark.ShoredTwice) == 0 && !Taken(k, RepairJob.Shore, ei))
                Offer(new Candidate(RepairJob.Shore, ei, sec, Score(Tier.Water, floodFirst, s.RepairShoreWeightM3h * value * load)), ref best);
        }

        // the cells' holes: plugged, patched, their plating caulked
        for (int i = 0; i < cells.Length; i++)
        {
            if (k.BreachM2[i] <= 0)
                continue;
            for (int h = 0; h <= (int)HoleClass.Patch; h++)
            {
                int hi = i * ShipCondition.HoleClasses + h;
                if (k.HoleOpenM2[hi] <= 0)
                    continue;
                var job = h switch { (int)HoleClass.Seam => RepairJob.Seam, (int)HoleClass.Plug => RepairJob.Plug, _ => RepairJob.Patch };
                if (Taken(k, job, i) || !Reachable(c, f, k, i, k.HoleZ[hi], s))
                    continue;
                double flowH = k.CellInflow[i] * 3600 * k.HoleOpenM2[hi] / k.BreachM2[i];
                if (flowH > 0)
                    Offer(new Candidate(job, i, cells[i].Section, Score(Tier.Water, floodFirst, flowH * Value(c, i))), ref best);
                else if (Near(c, k, i, k.HoleZ[hi], s))
                    Offer(new Candidate(job, i, cells[i].Section, Score(Tier.Near, floodFirst, 1 + k.HoleOpenM2[hi] * 1e4)), ref best);
            }
        }
        // the collision mats: over a cell's large holes near the waterline, from the deck (09 §4: 1-4 h, 20-40 men, slowed)
        for (int i = 0; i < cells.Length; i++)
        {
            int hm = i * ShipCondition.HoleClasses + (int)HoleClass.Mat;
            if (k.HoleOpenM2[hm] <= 0 || Taken(k, RepairJob.Mat, i) || !MatReach(c, k, i, k.HoleZ[hm], s))
                continue;
            double flowH = k.CellInflow[i] * 3600 * k.HoleOpenM2[hm] / Math.Max(k.BreachM2[i], 1e-9);
            // a mat means stopping the ship: only for holes whose water the pumps can't keep up with
            if (flowH > 0 && Flooding.FullInflow(c, f, k, HitQuery.WaterOf(c, k.Attitude), i, s.FloodCd) > Pumps(c, k))
                Offer(new Candidate(RepairJob.Mat, i, cells[i].Section, Score(Tier.Water, floodFirst, flowH * Value(c, i))), ref best);
        }

        // the steering: hand or the steam gear aft rigged when the gear failed (its room not flooded nor burning)
        if (k.SteerCause == SteerCause.Gear && k.Steering is Steering.Jammed or Steering.Lost or Steering.Engines && !Taken(k, RepairJob.Steering, 0) &&
            Valid(c, f, k, RepairJob.Steering, 0, s))
            Offer(new Candidate(RepairJob.Steering, 0, SteeringRoom(c).Section, Score(Tier.Steer, floodFirst, 1)), ref best);

        // the parts: weapons and their eyes, then machinery (the room's power first)
        for (int node = 0; node < k.State.Length; node++)
        {
            if (k.State[node] != PartState.Disabled || k.Fix[node] != 1)
                continue;
            var job = k.Drowned[node] ? RepairJob.Restore : RepairJob.Repair;
            if (Taken(k, job, node) || !Valid(c, f, k, job, node, s))
                continue;
            double weight = 1 + (PartKind(c, node) == 3 ? 2 : 1) * 1000 / Math.Max(k.FixManMin[node], 1);
            Offer(new Candidate(job, node, SectionOf(c, node), Score(Tier.Parts, floodFirst, weight)), ref best);
        }

        // the portable pumps: rigged where water stands and the holes don't beat them, the most water (by what it is) first
        if (k.PumpsRigged < c.Drainage.PortablePumps && c.Drainage.PortableTph > 0)
        {
            var water = HitQuery.WaterOf(c, k.Attitude);
            double rate = c.Drainage.PortableTph / FloodModel.SeaT / 3600;
            for (int i = 0; i < cells.Length; i++)
            {
                if (k.WaterM3[i] < s.RepairPumpMinM3 || k.PumpsIn[i] > 0 || k.CellFire[i] || Taken(k, RepairJob.RigPump, i) ||
                    Flooding.FullInflow(c, f, k, water, i, s.FloodCd) >= rate)
                    continue;
                Offer(new Candidate(RepairJob.RigPump, i, cells[i].Section, Score(Tier.Pump, floodFirst, k.WaterM3[i] * Value(c, i))), ref best);
            }
        }
        return best;
    }

    static int Sent(ShipCondition k, int space)
    {
        int n = 0;
        for (int p = 0; p < k.PartyCount; p++)
            n += k.Parties[p].Target == space ? 1 : 0;
        return n;
    }

    static bool Taken(ShipCondition k, RepairJob job, int at)
    {
        for (int p = 0; p < k.PartyCount; p++)
            if (k.Parties[p].Job == job && k.Parties[p].JobAt == at)
                return true;
        return false;
    }

    /// <summary>An edge's two cells, the one with the higher water first.</summary>
    static (int Hi, int Lo) Sides(ShipClass c, FloodModel f, ShipCondition k, in FloodEdge e) =>
        f.Level(c, k, e.A) - c.Cells[e.A].Box.Base + (f.Full(k, e.A) ? 1e3 : 0) >= f.Level(c, k, e.B) - c.Cells[e.B].Box.Base + (f.Full(k, e.B) ? 1e3 : 0)
            ? (e.A, e.B) : (e.B, e.A);

    /// <summary>Whether a party can work on a boundary from this side: not full, its water under the reach, not burning.</summary>
    static bool DrySide(ShipClass c, FloodModel f, ShipCondition k, int cell, SimSettings s) =>
        !f.Full(k, cell) && f.Level(c, k, cell) - c.Cells[cell].Box.Base < s.RepairReachM && !k.CellFire[cell];

    /// <summary>Whether a party can reach a cell's holes at a height: not full, not burning, and the water inside under
    /// the reach or under the holes.</summary>
    static bool Reachable(ShipClass c, FloodModel f, ShipCondition k, int cell, double holeZ, SimSettings s)
    {
        if (f.Full(k, cell) || k.CellFire[cell])
            return false;
        double level = f.Level(c, k, cell);
        return level - c.Cells[cell].Box.Base < s.RepairReachM || level < holeZ;
    }

    /// <summary>Whether holes at a height are near the sea outside (the list, sinkage or the roll bring them under).</summary>
    static bool Near(ShipClass c, ShipCondition k, int cell, double holeZ, SimSettings s)
    {
        var b = c.Cells[cell].Box;
        return holeZ - HitQuery.WaterOf(c, k.Attitude).At((b.X0 + b.X1) / 2, k.BreachY[cell]) < s.RepairNearM;
    }

    /// <summary>What's behind a boundary, to damage control (09 §3: the boundary's choice): a magazine 3, machinery and
    /// steering 2, anything else 1.</summary>
    static double Value(ShipClass c, int cell)
    {
        double v = 1;
        foreach (int room in c.RoomsIn(cell))
            v = Math.Max(v, c.Rooms[room].Kind switch
            {
                RoomKind.Magazine or RoomKind.Handling => 3,
                RoomKind.BoilerRoom or RoomKind.EngineRoom or RoomKind.Steering => 2,
                _ => 1,
            });
        return v;
    }

    /// <summary>The share of an edge's leak in its closures (or its seams).</summary>
    static double Share(ShipCondition k, int p, bool closures)
    {
        double seam = k.JoinSeamM2[p] * Math.Max(k.JoinWork[p], 1), clo = k.JoinClosureM2[p], all = seam + clo + k.JoinOpenM2[p] + 1e-9;
        return (closures ? clo : seam) / all;
    }

    /// <summary>An edge's load against its rating now (the water's heights either side; the ram head aside).</summary>
    static double Load(ShipClass c, FloodModel f, ShipCondition k, int ei, SimSettings s)
    {
        ref readonly var e = ref f.Edges[ei];
        var (hi, lo) = Sides(c, f, k, e);
        double drive = f.Level(c, k, hi) - Math.Max(f.Level(c, k, lo), e.Z);
        return drive <= 0 ? 0 : drive / Flooding.Rating(f, k, ei, e.Pool, s);
    }

    // ------------------------------------------------------------------ the work

    /// <summary>Whether a job is still there to do.</summary>
    static bool Valid(ShipClass c, FloodModel f, ShipCondition k, RepairJob job, int at, SimSettings s)
    {
        switch (job)
        {
            case RepairJob.Door:
            case RepairJob.Redog:
            case RepairJob.Caulk:
            case RepairJob.Shore:
            case RepairJob.Batten:
            {
                ref readonly var e = ref f.Edges[at];
                var m = k.JoinMarks[e.Pool];
                if ((m & JoinMark.Collapsed) != 0 || !DrySide(c, f, k, Sides(c, f, k, e).Lo, s))
                    return false;
                return job switch
                {
                    RepairJob.Door => (m & JoinMark.DoorOpen) != 0,
                    RepairJob.Batten => (m & JoinMark.Battened) == 0,
                    RepairJob.Redog => (m & JoinMark.Redogged) == 0,
                    RepairJob.Caulk => (m & JoinMark.Caulked) == 0,
                    _ => (m & JoinMark.ShoredTwice) == 0,
                };
            }
            case RepairJob.RigPump:
                return k.PumpsIn[at] == 0 && k.WaterM3[at] > 0 && !k.CellFire[at] && k.PumpsRigged < c.Drainage.PortablePumps;
            case RepairJob.Mat:
            {
                int h = at * ShipCondition.HoleClasses + (int)HoleClass.Mat;
                return k.HoleOpenM2[h] > 0 && MatReach(c, k, at, k.HoleZ[h], s);
            }
            case RepairJob.Steering:
            {
                if (k.SteerCause != SteerCause.Gear || k.Steering is not (Steering.Jammed or Steering.Lost or Steering.Engines))
                    return false;
                int room = SteeringRoom(c).Room;
                if (room < 0)
                    return true;
                var (share, burning) = RoomWater(c, f, k, room);
                return !burning && share < 0.5;
            }
            case RepairJob.Repair:
            case RepairJob.Restore:
            {
                if (k.State[at] != PartState.Disabled || k.Fix[at] != 1)
                    return false;
                if (at < c.Components.Length)
                    return true;
                var (share, burning) = RoomWater(c, f, k, at - c.Components.Length);
                return !burning && share <= (job == RepairJob.Restore ? s.RestoreShare : s.RoomFloodedShare);
            }
            default:
            {
                int h = at * ShipCondition.HoleClasses + (int)Class(job);
                return k.HoleOpenM2[h] > 0 && Reachable(c, f, k, at, k.HoleZ[h], s);
            }
        }
    }

    static HoleClass Class(RepairJob job) => job switch
    {
        RepairJob.Seam => HoleClass.Seam, RepairJob.Plug => HoleClass.Plug, RepairJob.Mat => HoleClass.Mat, _ => HoleClass.Patch,
    };

    /// <summary>Whether a mat from outside reaches holes at a height: in the hull's own side (the cell behind the plating
    /// there, not a space a torpedo tore open inside), within <see cref="SimSettings.MatReachM"/> of the sea.</summary>
    static bool MatReach(ShipClass c, ShipCondition k, int cell, double holeZ, SimSettings s)
    {
        var b = c.Cells[cell].Box;
        double x = (b.X0 + b.X1) / 2;
        if (FloodModel.SideCell(c, x, holeZ, k.BreachY[cell] >= 0).Cell != cell)
            return false;
        return Math.Abs(holeZ - HitQuery.WaterOf(c, k.Attitude).At(x, k.BreachY[cell])) <= s.MatReachM;
    }

    /// <summary>A job's size, man-minutes (09 §4's times × its crew; the holes' by their count).</summary>
    static double ManMin(ShipCondition k, RepairJob job, int at, SimSettings s)
    {
        double each = s.RepairJobManMin[(int)job];
        if (job is RepairJob.Repair or RepairJob.Restore)
            return k.FixManMin[at];
        return job is RepairJob.Plug or RepairJob.Patch or RepairJob.Mat ? each * Math.Max(k.HoleOpenN[at * ShipCondition.HoleClasses + (int)Class(job)], 1) : each;
    }

    /// <summary>The parties at their water jobs: their men (at most twice the job's crew) at the doctrine's skill, slower in
    /// the dark and on a list; a job done is applied and the party freed (its next job at the next plan).</summary>
    static RepairChange Work(ShipClass c, FloodModel f, ShipCondition k, double dt, long now, double speedMs, SimSettings s)
    {
        var change = RepairChange.None;
        bool slow = Math.Abs(speedMs) / Kn <= s.MatKn;
        double env = k.DamageControl.Skill / ((k.PowerCap < s.RepairDarkPower ? s.RepairDarkK : 1) * (1 + s.RepairListPerDeg * Math.Abs(k.Attitude.HeelDeg)));
        for (int p = 0; p < k.PartyCount; p++)
        {
            ref var party = ref k.Parties[p];
            if (party.Job == RepairJob.None || now < party.ArriveMs || now < party.StopMs || party.Men < 2)
                continue;
            if (!Valid(c, f, k, party.Job, party.JobAt, s))
            {
                (party.Job, party.JobAt) = (RepairJob.None, -1);
                continue;
            }
            if (party.Job == RepairJob.Mat && !slow)
                continue;   // a mat goes over only with the way off her
            double men = Math.Min(party.Men, s.RepairJobMen[(int)party.Job] * s.RepairCrewsAJob);
            party.Done += men * party.Skill * env * dt / 60;
            if (party.Done < ManMin(k, party.Job, party.JobAt, s))
                continue;
            var done = Apply(c, f, k, party.Job, party.JobAt, s);
            if (done > change)
                change = done;
            (party.Job, party.JobAt, party.Done) = (RepairJob.None, -1, 0);
        }
        return change;
    }

    /// <summary>A job done: the edge marked, or the cell's holes of its class worked on (what's left of them leaking).</summary>
    static RepairChange Apply(ShipClass c, FloodModel f, ShipCondition k, RepairJob job, int at, SimSettings s)
    {
        switch (job)
        {
            case RepairJob.Steering:
                (k.Steering, k.JamRudderRad) = (Steering.Hand, 0);
                k.Note(DcEvent.Steering, -1, (int)Steering.Hand);
                return RepairChange.Steering;
            case RepairJob.Repair:
            case RepairJob.Restore:
                k.Note(job == RepairJob.Restore ? DcEvent.Restored : DcEvent.Repaired, at, 0);
                (k.State[at], k.Fix[at], k.Drowned[at]) = (PartState.Damaged, 0, false);
                k.Version++;
                return RepairChange.Parts;
        }
        if (job is RepairJob.Door or RepairJob.Redog or RepairJob.Caulk or RepairJob.Shore or RepairJob.Batten)
        {
            int p = f.Edges[at].Pool;
            var m = k.JoinMarks[p];
            k.JoinMarks[p] = job switch
            {
                RepairJob.Door => m & ~JoinMark.DoorOpen,
                RepairJob.Batten => m | JoinMark.Battened,
                RepairJob.Redog => m | JoinMark.Redogged,
                RepairJob.Caulk => m | JoinMark.Caulked,
                _ => (m & JoinMark.Shored) != 0 ? m | JoinMark.ShoredTwice : m | JoinMark.Shored,
            };
            return RepairChange.None;
        }
        if (job == RepairJob.RigPump)
        {
            (k.PumpsIn[at], k.PumpsRigged) = ((byte)(k.PumpsIn[at] + 1), k.PumpsRigged + 1);
            return RepairChange.None;
        }
        int h = at * ShipCondition.HoleClasses + (int)Class(job);
        double left = job switch
        {
            RepairJob.Plug => s.RepairPlugLeft,
            RepairJob.Mat => s.MatLeft,
            RepairJob.Seam => s.CaulkSeamShare,
            // a patch put on against the water still coming in holds worse (NRL's ex-Shadwell)
            _ => Flowing(c, f, k, at, k.HoleZ[h]) ? s.RepairPatchFlowLeft : s.RepairPatchLeft,
        };
        k.Worked(at, h, left);
        return RepairChange.None;
    }

    /// <summary>Whether the sea still comes in at holes at a height: under the sea outside, the water inside under them.</summary>
    static bool Flowing(ShipClass c, FloodModel f, ShipCondition k, int cell, double holeZ)
    {
        var b = c.Cells[cell].Box;
        return holeZ < HitQuery.WaterOf(c, k.Attitude).At((b.X0 + b.X1) / 2, k.BreachY[cell]) && f.Level(c, k, cell) < holeZ;
    }
}
