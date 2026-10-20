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
    enum Tier : byte { Parts = 1, Pump = 2, Near = 3, Fire = 4, Water = 5, Door = 6, MagazineFire = 7 }

    public static void Step(ShipClass c, FloodModel f, ShipCondition k, double dt, long now, SimSettings s)
    {
        ref readonly var doc = ref k.DamageControl;
        if (doc.Name is null || k.Status != ShipStatus.Afloat || !k.Damaged && k.Fires == 0)
            return;
        Make(c, f, k, now, s);
        Watch(c, f, k, now, s);
        Counterflood(c, f, k, dt, now, s);
        PlanIfDue(c, f, k, now, s);
        Work(c, f, k, dt, now, s);
    }

    // ------------------------------------------------------------------ DC Central (DC5)

    /// <summary>DC Central's counterflooding (09 §2.5, §3; WDR 61): past the doctrine's list it opens the tank on the high
    /// side whose fill best matches the water the list needs (Δ·GM·tanφ over its lever), and fills it, the next when it's
    /// full; past its trim, a centreline tank at the other end. It shuts the valve once the list is inside half the
    /// doctrine's (or has gone the other way): a tank left part full has free surface, the price of not overshooting. At
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
            bool done = forList ? Math.Abs(heel) < 0.5 * doc.CounterfloodDeg || heel * f.Cy[t] > 0
                : Math.Abs(trim) < 0.5 * doc.CounterfloodTrimDeg || trim * (f.Cx[t] - c.Hydrostatics.Lcf) > 0;
            if (done || k.WaterM3[t] >= f.Capacity[t] * 0.999 || k.BreachM2[t] > 0)
                k.CounterTank = t = -1;
        }
        if (t < 0 && Math.Abs(heel) > doc.CounterfloodDeg && Deballast(c, f, k, heel > 0 ? 1 : -1, dt))
            return;   // the list is now toward water it let in itself: that goes out first
        if (t < 0 && k.GmM < s.CounterfloodMinGmM)
            return;   // a list with her GM gone is a loll: flooding the high side would throw her over (Ralph Talbot's lesson)
        if (t < 0)
        {
            if (Math.Abs(heel) > doc.CounterfloodDeg)
                t = Tank(c, f, k, heel > 0 ? -1 : 1, 0, Needed(c, k, heel));
            else if (doc.CounterfloodTrimDeg >= 0 && Math.Abs(trim) > doc.CounterfloodTrimDeg)
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
                if (lever <= 0.5)
                    continue;
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

    /// <summary>Her parties, once (fire_ref.py's Sim): a share of her complement, two to eight of them, spread along her.</summary>
    public static void Make(ShipClass c, FloodModel f, ShipCondition k, long now, SimSettings s)
    {
        if (k.PartiesMade)
            return;
        ref readonly var doc = ref k.DamageControl;
        double men = f.Complement * doc.PartyShare;
        int n = Math.Clamp((int)Math.Round(men / Math.Max(doc.PartyMen, 1)), s.FirePartiesMin, Math.Min(s.FirePartiesMax, ShipCondition.MaxParties));
        for (int i = 0; i < n; i++)
            k.Parties[i] = new RepairParty
            {
                Men = men / n, Skill = 1, Section = (int)((i + 0.5) * c.Sections.Length / n), Target = -1, JobAt = -1,
            };
        (k.PartyCount, k.PartiesMade, k.NextPlanMs) = (n, true, now);
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
            for (int i = 0; i < doc.TurnTo && k.PartyCount < ShipCondition.MaxParties; i++)
                k.Parties[k.PartyCount++] = new RepairParty
                {
                    Men = Math.Max(doc.PartyMen, 2), Skill = s.FireTurnToSkill, Section = c.PumpSection, Target = -1, JobAt = -1,
                };
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
            (Tier.MagazineFire, true) => 5,
            (Tier.Door, true) => 7,
            (Tier.Water, true) => 6,
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
            if (!doorOpen && k.JoinSeamM2[p] <= 0 && k.JoinClosureM2[p] <= 0)
                continue;
            var (hi, lo) = Sides(c, f, k, e);
            if (!DrySide(c, f, k, lo, s))
                continue;
            double flowH = Math.Abs(k.JoinFlow[p]) * 3600, value = Value(c, lo);
            int sec = cells[lo].Section;
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
            {
                ref readonly var e = ref f.Edges[at];
                var m = k.JoinMarks[e.Pool];
                if ((m & JoinMark.Collapsed) != 0 || !DrySide(c, f, k, Sides(c, f, k, e).Lo, s))
                    return false;
                return job switch
                {
                    RepairJob.Door => (m & JoinMark.DoorOpen) != 0,
                    RepairJob.Redog => (m & JoinMark.Redogged) == 0,
                    RepairJob.Caulk => (m & JoinMark.Caulked) == 0,
                    _ => (m & JoinMark.ShoredTwice) == 0,
                };
            }
            case RepairJob.RigPump:
                return k.PumpsIn[at] == 0 && k.WaterM3[at] > 0 && !k.CellFire[at] && k.PumpsRigged < c.Drainage.PortablePumps;
            default:
            {
                int h = at * ShipCondition.HoleClasses + (int)Class(job);
                return k.HoleOpenM2[h] > 0 && Reachable(c, f, k, at, k.HoleZ[h], s);
            }
        }
    }

    static HoleClass Class(RepairJob job) => job switch { RepairJob.Seam => HoleClass.Seam, RepairJob.Plug => HoleClass.Plug, _ => HoleClass.Patch };

    /// <summary>A job's size, man-minutes (09 §4's times × its crew; the holes' by their count).</summary>
    static double ManMin(ShipCondition k, RepairJob job, int at, SimSettings s)
    {
        double each = s.RepairJobManMin[(int)job];
        return job is RepairJob.Plug or RepairJob.Patch ? each * Math.Max(k.HoleOpenN[at * ShipCondition.HoleClasses + (int)Class(job)], 1) : each;
    }

    /// <summary>The parties at their water jobs: their men (at most twice the job's crew) at the doctrine's skill, slower in
    /// the dark and on a list; a job done is applied and the party freed (its next job at the next plan).</summary>
    static void Work(ShipClass c, FloodModel f, ShipCondition k, double dt, long now, SimSettings s)
    {
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
            double men = Math.Min(party.Men, s.RepairJobMen[(int)party.Job] * s.RepairCrewsAJob);
            party.Done += men * party.Skill * env * dt / 60;
            if (party.Done < ManMin(k, party.Job, party.JobAt, s))
                continue;
            Apply(c, f, k, party.Job, party.JobAt, s);
            (party.Job, party.JobAt, party.Done) = (RepairJob.None, -1, 0);
        }
    }

    /// <summary>A job done: the edge marked, or the cell's holes of its class worked on (what's left of them leaking).</summary>
    static void Apply(ShipClass c, FloodModel f, ShipCondition k, RepairJob job, int at, SimSettings s)
    {
        if (job is RepairJob.Door or RepairJob.Redog or RepairJob.Caulk or RepairJob.Shore)
        {
            int p = f.Edges[at].Pool;
            var m = k.JoinMarks[p];
            k.JoinMarks[p] = job switch
            {
                RepairJob.Door => m & ~JoinMark.DoorOpen,
                RepairJob.Redog => m | JoinMark.Redogged,
                RepairJob.Caulk => m | JoinMark.Caulked,
                _ => (m & JoinMark.Shored) != 0 ? m | JoinMark.ShoredTwice : m | JoinMark.Shored,
            };
            return;
        }
        if (job == RepairJob.RigPump)
        {
            (k.PumpsIn[at], k.PumpsRigged) = ((byte)(k.PumpsIn[at] + 1), k.PumpsRigged + 1);
            return;
        }
        int h = at * ShipCondition.HoleClasses + (int)Class(job);
        double left = job switch
        {
            RepairJob.Plug => s.RepairPlugLeft,
            RepairJob.Seam => s.CaulkSeamShare,
            // a patch put on against the water still coming in holds worse (NRL's ex-Shadwell)
            _ => Flowing(c, f, k, at, k.HoleZ[h]) ? s.RepairPatchFlowLeft : s.RepairPatchLeft,
        };
        k.Worked(at, h, left);
    }

    /// <summary>Whether the sea still comes in at holes at a height: under the sea outside, the water inside under them.</summary>
    static bool Flowing(ShipClass c, FloodModel f, ShipCondition k, int cell, double holeZ)
    {
        var b = c.Cells[cell].Box;
        return holeZ < HitQuery.WaterOf(c, k.Attitude).At((b.X0 + b.X1) / 2, k.BreachY[cell]) && f.Level(c, k, cell) < holeZ;
    }
}
