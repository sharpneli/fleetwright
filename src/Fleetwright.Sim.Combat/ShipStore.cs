using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Combat;

/// <summary>How a ship advances (docs/world/architecture.md "Four clock tiers, one clock"): stepped on the fixed fine
/// tick; on anchored segments of kinematic motion, decided for on the coarse step; or on rails, a function of time.
/// What a ship is doing and the asked speed pick it (ShipSystem.Tiers.cs). Routine is a group's, above ships.</summary>
public enum ClockTier : byte
{
    Fine,
    Coarse,
    Rails,
}

/// <summary>What steers a ship: nothing (the rudder stays where it is), a rudder angle held, a course, or a route.</summary>
public enum Helm : byte
{
    None,
    Rudder,
    Course,
    Route,
}

/// <summary>The world's ships at sea: one slot per ship, its state in columns (docs/battle/plan.md "The shape": callers
/// reach a ship through <see cref="ShipState"/> and index accessors, never the columns' layout, so the storage can
/// become an ECS without touching them). Ships are made and dropped one at a time, at any moment: a dropped slot goes on
/// a free list and its generation goes up, so a stale handle or event is recognised. The columns grow only when a
/// spawn needs room; nothing else allocates.</summary>
public sealed class ShipStore
{
    readonly SimSettings settings;
    readonly int routeCap;
    readonly Stack<int> free = new();
    int high;   // slots in use or freed: [0, high)

    // identity
    internal int[] entity = [], generation = [], classIndex = [];
    internal byte[] side = [];
    internal bool[] alive = [];
    internal Handling[] handling = [];

    // the pose: Earth-centred, at the time it was last moved to
    internal Vec3d[] position = [], forward = [];
    internal double[] speed = [], yawRate = [], rudder = [], power = [];
    internal long[] poseMs = [];

    // what's ordered
    internal Helm[] helm = [];
    internal double[] rudderOrder = [], courseDeg = [], courseRudder = [], powerOrder = [], speedOrderKn = [], clearance = [];
    internal Vec3d[] route = [];   // routeCap per slot
    internal bool[] routeLoop = [];   // past the last waypoint, on to the first again (a patrol)

    // ships and the land (Slide): which way along the coast a ship slides (+1 land to port, -1 to starboard, 0 not
    // sliding), and how often the backstop has had to put it back off the land
    internal sbyte[] landSide = [];
    internal int[] backstops = [];

    // the clock tier, and the anchored tiers' columns (docs/battle/plan.md steps 2.5 and 2.8): a ship on rails or coarse
    // isn't stepped, it's a function of time. A ship on its own is on a great circle or a rhumb line from an anchor
    // (time, position, forward) at a speed; a group's other ships ride at a fixed offset (ahead, to starboard) in their
    // guide's frame. railGen goes up whenever a ship's anchor changes, so its events go stale.
    internal ClockTier[] tier = [];
    internal long[] railMs = [];
    internal Vec3d[] railPos = [], railFwd = [];
    internal double[] railSpeed = [], railX = [], railY = [];
    internal int[] railGuide = [], railGen = [];
    internal double[] railCourse = [];   // NaN: a great circle (a route's leg); else a rhumb line at this course

    // a coarse ship's own segment (docs/battle/plan.md step 2.8) beyond rails': a constant acceleration (m/s²) from the
    // anchor's speed, a curvature (rad/m, positive to starboard; 0 straight, on the great circle or rhumb line above),
    // and when the segment ends (its turn or its change of speed done, a waypoint's wheel-over), long.MaxValue for never
    internal double[] railAccel = [], railCurv = [];
    internal long[] segEndMs = [];

    // a coarse ship's track (step 2.8d): its past segments, newest last, in a ring of trackCap a slot (where each began,
    // its way, curvature and line, how far along the track it began and how long it was), and how far along the track
    // its own anchor is; a group's ships riding it coarse sit on it at their distance astern
    readonly int trackCap;
    internal Vec3d[] histPos = [], histFwd = [];
    internal double[] histCourse = [], histCurv = [], histStart = [], histLen = [], trackAt = [];
    internal int[] histFirst = [], histCount = [];

    /// <summary>The world's time: what a ship on rails is evaluated at. The world sets it before anything reads the
    /// store (events, commands, the snapshot).</summary>
    public long NowMs { get; set; }
    internal int[] routeCount = [], routeNext = [];

    public ShipStore(SimSettings settings)
    {
        this.settings = settings;
        routeCap = settings.RouteCapacity;
        trackCap = Math.Max(settings.CoarseTrackSegments, 1);
        Grow(Math.Max(settings.ShipCapacity, 1));
    }

    /// <summary>Slots in use or freed; iterate [0, Slots) and skip the dead.</summary>
    public int Slots => high;
    public int Count => high - free.Count;
    public int RouteCapacity => routeCap;

    public bool IsAlive(int slot) => slot >= 0 && slot < high && alive[slot];
    public ShipState this[int slot] => new(this, slot);

    /// <summary>Makes a ship of a class (its index in the world's table, and its handling) at a pose, on a side. Its
    /// helm holds the heading it's given, its engines the speed.</summary>
    public int Spawn(int entityId, int classIndex, Handling h, Vec3d position, Vec3d forward, double speedMs, long timeMs, byte side = 0)
    {
        int slot;
        if (free.Count > 0)
            slot = free.Pop();
        else
        {
            if (high == alive.Length)
                Grow(alive.Length * 2);
            slot = high++;
        }
        var up = position.Normalized();
        entity[slot] = entityId;
        if (entityId >= 0)
        {
            if (entityId >= slotOfEntity.Length)
                Array.Resize(ref slotOfEntity, Math.Max(entityId + 1, slotOfEntity.Length * 2));
            slotOfEntity[entityId] = slot + 1;
        }
        this.classIndex[slot] = classIndex;
        this.side[slot] = side;
        alive[slot] = true;
        handling[slot] = h;
        this.position[slot] = up * Earth.Radius;
        this.forward[slot] = (forward - up * Vec3d.Dot(forward, up)).Normalized();
        speed[slot] = speedMs;
        yawRate[slot] = rudder[slot] = 0;
        power[slot] = powerOrder[slot] = h.PowerFor(speedMs / Handling.MsPerKnot);
        speedOrderKn[slot] = speedMs / Handling.MsPerKnot;
        poseMs[slot] = timeMs;
        SetTier(slot, ClockTier.Fine);
        railGen[slot]++;
        helm[slot] = Helm.Course;
        courseDeg[slot] = Earth.Heading(this.position[slot], this.forward[slot]);
        courseRudder[slot] = settings.StandardRudderDeg * Math.PI / 180;
        rudderOrder[slot] = 0;
        clearance[slot] = 0;
        routeCount[slot] = routeNext[slot] = 0;
        routeLoop[slot] = false;
        landSide[slot] = 0;
        backstops[slot] = 0;
        return slot;
    }

    // ------------------------------------------------------------------ the rails tier

    public bool OnRails(int slot) => tier[slot] == ClockTier.Rails;

    /// <summary>A ship's clock tier.</summary>
    public ClockTier TierOf(int slot) => tier[slot];

    /// <summary>Not stepped: on rails or coarse, its pose a function of time from its anchor.</summary>
    public bool Anchored(int slot) => tier[slot] != ClockTier.Fine;

    // the live ships of the fine and the coarse tier, densely: what the fine tick steps and the coarse step decides for
    // (each ship's step reads only its own slot, so their order changes nothing)
    readonly SlotList fine = new(), coarse = new();

    /// <summary>How many live ships are on the fine tick, and the i-th of them (in no order that matters).</summary>
    public int FineCount => fine.Count;
    public int FineSlot(int i) => fine[i];

    /// <summary>How many live ships are coarse, and the i-th of them (in no order that matters).</summary>
    public int CoarseCount => coarse.Count;
    public int CoarseSlot(int i) => coarse[i];

    /// <summary>How many live ships are off rails (fine or coarse), and the i-th of them: the fine ones, then the coarse
    /// (what encounters and sighting look at, in an order that's the same on every run).</summary>
    public int OffRailsCount => fine.Count + coarse.Count;
    public int OffRailsSlot(int i) => i < fine.Count ? fine[i] : coarse[i - fine.Count];

    void SetTier(int slot, ClockTier t)
    {
        tier[slot] = t;
        if (t == ClockTier.Fine)
            fine.Add(slot);
        else
            fine.Remove(slot);
        if (t == ClockTier.Coarse)
            coarse.Add(slot);
        else
            coarse.Remove(slot);
    }

    /// <summary>A set of slots, dense, with each one's place in it: adding, removing and walking allocate nothing
    /// (removing moves the last into the gap).</summary>
    sealed class SlotList
    {
        int[] slots = [], index = [];

        public int Count { get; private set; }
        public int this[int i] => slots[i];

        public void Grow(int n)
        {
            int old = index.Length;
            Array.Resize(ref slots, n);
            Array.Resize(ref index, n);
            Array.Fill(index, -1, old, n - old);
        }

        public void Add(int slot)
        {
            if (index[slot] >= 0)
                return;
            index[slot] = Count;
            slots[Count++] = slot;
        }

        public void Remove(int slot)
        {
            int k = index[slot];
            if (k < 0)
                return;
            int last = slots[--Count];
            slots[k] = last;
            index[last] = k;
            index[slot] = -1;
        }
    }

    /// <summary>A ship's pose now: its stepped pose, or where its rails put it at <see cref="NowMs"/>.</summary>
    public void PoseNow(int slot, out Vec3d position, out Vec3d forward) => PoseAt(slot, NowMs, out position, out forward);

    /// <summary>Where a ship's rails put it at a time, as if they held till then (a waypoint or an order on the way
    /// re-anchors them); a ship off rails is where it was last stepped to.</summary>
    public void PoseAt(int slot, long ms, out Vec3d position, out Vec3d forward)
    {
        if (tier[slot] == ClockTier.Fine)
        {
            (position, forward) = (this.position[slot], this.forward[slot]);
            return;
        }
        int g = railGuide[slot];
        if (g < 0)
        {
            Along(railPos[slot], railFwd[slot], railCourse[slot], railCurv[slot], Run(slot, ms), out position, out forward);
            return;
        }
        if (OnTrack(slot))
        {
            OnGuidesTrack(slot, ms, out position, out forward, out _);
            return;
        }
        PoseAt(g, ms, out var gp, out var gf);
        var up = gp.Normalized();
        var p = gp + gf * railX[slot] + Vec3d.Cross(gf, up) * railY[slot];
        position = p.Normalized() * Earth.Radius;
        var u2 = position.Normalized();
        forward = (gf - u2 * Vec3d.Dot(gf, u2)).Normalized();
    }

    /// <summary>How far an anchored ship of its own has gone along its segment by a time, metres.</summary>
    internal double Run(int slot, long ms)
    {
        double t = (ms - railMs[slot]) / 1000.0;
        return railSpeed[slot] * t + 0.5 * railAccel[slot] * t * t;
    }

    /// <summary>A distance along a segment from where it began: an arc of a curvature, a rhumb line at a course, or the
    /// great circle along its way.</summary>
    static void Along(Vec3d from, Vec3d way, double course, double curvature, double s, out Vec3d position, out Vec3d forward)
    {
        (position, forward) = (from, way);
        if (curvature != 0)
            Earth.Circle(ref position, ref forward, s, curvature);
        else if (!double.IsNaN(course))
            Earth.Rhumb(from, course, s, out position, out forward);
        else
            Earth.Advance(ref position, ref forward, s);
    }

    /// <summary>A coarse ship riding a coarse guide of its own: it sits on the guide's track.</summary>
    bool OnTrack(int slot) => tier[slot] == ClockTier.Coarse && railGuide[slot] is int g and >= 0 && tier[g] == ClockTier.Coarse && railGuide[g] < 0;

    /// <summary>A coarse ship riding its guide on the guide's track (step 2.8d): at its distance astern along the way the
    /// guide steamed (its segment now, then its past ones; before the oldest, straight back), and its distance abeam
    /// across it. A column turns where its guide turned; abreast it's where the guide is now. The curvature of the
    /// track there too (its rate of turn at the guide's speed).</summary>
    void OnGuidesTrack(int slot, long ms, out Vec3d position, out Vec3d forward, out double curvature)
    {
        int g = railGuide[slot];
        double at = trackAt[g] + Run(g, ms) + railX[slot];   // railX: metres ahead (astern negative)
        Vec3d p, f;
        if (at >= trackAt[g] || histCount[g] == 0)
        {
            double s = at - trackAt[g];   // back past a fresh anchor: straight back along its way
            curvature = s >= 0 ? railCurv[g] : 0;
            Along(railPos[g], railFwd[g], s >= 0 ? railCourse[g] : double.NaN, curvature, s, out p, out f);
        }
        else
        {
            int k = histCount[g] - 1;
            int h = g * trackCap + (histFirst[g] + k) % trackCap;
            while (k > 0 && at < histStart[h])
            {
                k--;
                h = g * trackCap + (histFirst[g] + k) % trackCap;
            }
            double s = at - histStart[h];
            curvature = s >= 0 ? histCurv[h] : 0;
            Along(histPos[h], histFwd[h], s >= 0 ? histCourse[h] : double.NaN, curvature, s, out p, out f);
        }
        var up = p.Normalized();
        position = (p + Vec3d.Cross(f, up) * railY[slot]).Normalized() * Earth.Radius;
        var u2 = position.Normalized();
        forward = (f - u2 * Vec3d.Dot(f, u2)).Normalized();
    }

    /// <summary>A coarse ship of its own about to be anchored again: the segment it's on goes on its track, as long as
    /// it went (a segment of no length isn't kept); the oldest is dropped once there are as many as are kept.</summary>
    void KeepTrack(int slot)
    {
        if (tier[slot] != ClockTier.Coarse || railGuide[slot] >= 0)
            return;
        double len = Run(slot, NowMs);
        if (!(Math.Abs(len) > 1e-6))
            return;
        if (histCount[slot] == trackCap)
        {
            histFirst[slot] = (histFirst[slot] + 1) % trackCap;
            histCount[slot]--;
        }
        int h = slot * trackCap + (histFirst[slot] + histCount[slot]) % trackCap;
        (histPos[h], histFwd[h], histCourse[h], histCurv[h], histStart[h], histLen[h]) =
            (railPos[slot], railFwd[slot], railCourse[slot], railCurv[slot], trackAt[slot], len);
        histCount[slot]++;
        trackAt[slot] += len;
    }

    /// <summary>A ship's speed through the water at a time, m/s: stepped (its last tick's), or its anchor's speed and
    /// acceleration since; a ship riding another goes at its speed.</summary>
    public double SpeedAt(int slot, long ms)
    {
        if (tier[slot] == ClockTier.Fine)
            return speed[slot];
        if (railGuide[slot] >= 0)
            return SpeedAt(railGuide[slot], ms);
        return railSpeed[slot] + railAccel[slot] * (ms - railMs[slot]) / 1000.0;
    }

    /// <summary>A ship's rate of turn at a time, rad/s, positive to starboard: stepped, its segment's curvature at its
    /// speed, or (riding another) its guide's.</summary>
    public double YawRateAt(int slot, long ms)
    {
        if (tier[slot] == ClockTier.Fine)
            return yawRate[slot];
        if (OnTrack(slot))
        {
            OnGuidesTrack(slot, ms, out _, out _, out double k);
            return SpeedAt(slot, ms) * k;   // turning where its guide turned
        }
        if (railGuide[slot] >= 0)
            return YawRateAt(railGuide[slot], ms);
        return railCurv[slot] == 0 ? 0 : SpeedAt(slot, ms) * railCurv[slot];
    }

    /// <summary>The curvature a coarse ship is on now, rad/m: its segment's, or riding, its guide's track's where it is.</summary>
    public double CurvatureNow(int slot)
    {
        if (OnTrack(slot))
        {
            OnGuidesTrack(slot, NowMs, out _, out _, out double k);
            return k;
        }
        return tier[slot] == ClockTier.Coarse && railGuide[slot] < 0 ? railCurv[slot] : 0;
    }

    /// <summary>A coarse ship on a segment of its own that's straight at a steady speed: its manoeuvre done.</summary>
    public bool CoarseSteady(int slot) => tier[slot] == ClockTier.Coarse && railGuide[slot] < 0 && railAccel[slot] == 0 && railCurv[slot] == 0;

    /// <summary>When a coarse ship's segment ends (ms), long.MaxValue for never or not coarse on its own.</summary>
    public long SegmentEndMs(int slot) => tier[slot] == ClockTier.Coarse && railGuide[slot] < 0 ? segEndMs[slot] : long.MaxValue;

    /// <summary>Makes a ship coarse on a segment of its own, anchored at its pose and speed now (from the fine tick, off
    /// rails, or riding its guide), straight and steady until <see cref="CoarseMotion.Plan"/> lays it. The swing and
    /// the rudder's lag are dropped: a coarse ship turns at once on its circle.</summary>
    public void Coarsen(int slot)
    {
        PoseNow(slot, out var p, out var f);
        double v = SpeedAt(slot, NowMs);
        SetTier(slot, ClockTier.Coarse);
        (histFirst[slot], histCount[slot], trackAt[slot]) = (0, 0, 0);   // a fresh track: straight back from here
        Anchor(slot, p, f, v);
    }

    /// <summary>A ship riding its guide on rails rides it coarse (its guide come off rails into the coarse tier), at the
    /// same offset.</summary>
    public void RideCoarse(int slot)
    {
        if (tier[slot] == ClockTier.Rails && railGuide[slot] >= 0)
            SetTier(slot, ClockTier.Coarse);
    }

    /// <summary>A coarse ship of its own rides a coarse guide at its offset as it is now: a group's ship back in station
    /// (its errors are kept, as on rails; nothing jumps). Only while the guide's straight leg reaches back past it, so
    /// its place on the guide's track is where it is; false (and nothing) otherwise.</summary>
    public bool RideCoarseWith(int slot, int guide)
    {
        PoseNow(guide, out var gp, out var gf);
        PoseNow(slot, out var p, out _);
        var d = p - gp;
        var up = gp.Normalized();
        double ahead = Vec3d.Dot(d, gf);
        if (railCurv[guide] != 0 || -ahead > Run(guide, NowMs) && histCount[guide] > 0)
            return false;
        SetTier(slot, ClockTier.Coarse);
        railGen[slot]++;
        (railGuide[slot], railX[slot], railY[slot]) = (guide, ahead, Vec3d.Dot(d, Vec3d.Cross(gf, up)));
        (railAccel[slot], railCurv[slot], segEndMs[slot]) = (0, 0, long.MaxValue);
        return true;
    }

    /// <summary>A coarse ship anchored again where its segment has it now, for <see cref="CoarseMotion.Plan"/> to lay
    /// the next (an order, its segment's end).</summary>
    public void ReanchorCoarse(int slot)
    {
        PoseNow(slot, out var p, out var f);
        double v = SpeedAt(slot, NowMs);
        KeepTrack(slot);
        Anchor(slot, p, f, v);
    }

    void Anchor(int slot, Vec3d p, Vec3d f, double v)
    {
        railGen[slot]++;
        var up = p.Normalized();
        (railMs[slot], railPos[slot], railFwd[slot], railSpeed[slot], railGuide[slot], railCourse[slot]) =
            (NowMs, up * Earth.Radius, (f - up * Vec3d.Dot(f, up)).Normalized(), v, -1, double.NaN);
        (railAccel[slot], railCurv[slot], segEndMs[slot]) = (0, 0, long.MaxValue);
    }

    /// <summary>Puts a ship on rails on its own, from its pose now at a speed (the one its engines settle at: it's within
    /// a fraction of a knot of it): on a rhumb line at a course (a course held), or on the great circle along its
    /// heading (NaN).</summary>
    public void Rail(int slot, double courseDeg, double speedMs)
    {
        SetTier(slot, ClockTier.Rails);
        railGen[slot]++;
        (railMs[slot], railPos[slot], railFwd[slot], railSpeed[slot], railGuide[slot], railCourse[slot]) =
            (NowMs, position[slot], forward[slot], speedMs, -1, courseDeg);
        (railAccel[slot], railCurv[slot], segEndMs[slot]) = (0, 0, long.MaxValue);
        yawRate[slot] = rudder[slot] = 0;
    }

    /// <summary>Re-anchors a ship on rails at a point on the great circle along a direction (a route's leg; at a
    /// waypoint the turn is instantaneous).</summary>
    public void Reanchor(int slot, Vec3d at, Vec3d forward)
    {
        KeepTrack(slot);
        railGen[slot]++;
        var up = at.Normalized();
        (railMs[slot], railPos[slot], railFwd[slot], railCourse[slot]) =
            (NowMs, up * Earth.Radius, (forward - up * Vec3d.Dot(forward, up)).Normalized(), double.NaN);
    }

    /// <summary>Re-anchors a ship on rails at the start of a route's leg, a great circle from a point along a direction,
    /// and orders the leg's heading as its course: what a group placed on its stations takes from it (as passing a
    /// waypoint does).</summary>
    public void ReanchorLeg(int slot, Vec3d at, Vec3d forward)
    {
        Reanchor(slot, at, forward);
        courseDeg[slot] = Earth.Heading(railPos[slot], railFwd[slot]);
    }

    /// <summary>Re-anchors a ship on rails along a coast (docs/world/terrain-plan.md S3): on the great circle from a point
    /// along the slide's direction, sliding on a side (land to port +1, to starboard -1), or let go of it (0).</summary>
    public void ReanchorSliding(int slot, Vec3d at, Vec3d forward, sbyte side)
    {
        Reanchor(slot, at, forward);
        landSide[slot] = side;
    }

    /// <summary>A ship no longer sliding along a coast: the side forgotten, its rails as they are.</summary>
    public void LetGoOfCoast(int slot) => landSide[slot] = 0;

    /// <summary>Re-anchors a ship on rails at a point on a rhumb line at a course (past a route's last waypoint).</summary>
    public void ReanchorRhumb(int slot, Vec3d at, double courseDeg)
    {
        KeepTrack(slot);
        railGen[slot]++;
        var up = at.Normalized();
        (railMs[slot], railPos[slot], railFwd[slot], railCourse[slot]) = (NowMs, up * Earth.Radius, Earth.Forward(up, courseDeg), courseDeg);
    }

    /// <summary>Puts a ship on rails at its offset from a guide (already on rails), as it is now.</summary>
    public void RailWith(int slot, int guide)
    {
        PoseNow(guide, out var gp, out var gf);
        var d = position[slot] - gp;
        var up = gp.Normalized();
        SetTier(slot, ClockTier.Rails);
        railGen[slot]++;
        (railGuide[slot], railX[slot], railY[slot], railSpeed[slot]) = (guide, Vec3d.Dot(d, gf), Vec3d.Dot(d, Vec3d.Cross(gf, up)), railSpeed[guide]);
        (railAccel[slot], railCurv[slot], segEndMs[slot]) = (0, 0, long.MaxValue);
        yawRate[slot] = rudder[slot] = 0;
    }

    /// <summary>Puts a ship on rails at a given offset from a guide (already on rails), metres ahead and to starboard
    /// in its frame: a formation snapped onto its stations. It takes the guide's course and speed as its orders, and
    /// the power that holds that speed, so it comes off rails steaming as the rails had it.</summary>
    public void RailAt(int slot, int guide, double aheadM, double starboardM)
    {
        SetTier(slot, ClockTier.Rails);
        railGen[slot]++;
        (railGuide[slot], railX[slot], railY[slot], railSpeed[slot]) = (guide, aheadM, starboardM, railSpeed[guide]);
        (railAccel[slot], railCurv[slot], segEndMs[slot]) = (0, 0, long.MaxValue);
        yawRate[slot] = rudder[slot] = 0;
        helm[slot] = Helm.Course;   // its orders the guide's, whatever it steered for before it was gathered in
        courseDeg[slot] = courseDeg[guide];
        speedOrderKn[slot] = railSpeed[guide] / Handling.MsPerKnot;
        power[slot] = powerOrder[slot] = handling[slot].PowerFor(speedOrderKn[slot]);
    }

    /// <summary>A ship off rails made what its orders ask at once (an order snapped on rails): its engines at the
    /// power rung and the speed that power holds, its head on the ordered course or for its next waypoint.</summary>
    public void Settle(int slot)
    {
        power[slot] = powerOrder[slot];
        speed[slot] = handling[slot].SteadySpeedMs(power[slot]);
        yawRate[slot] = rudder[slot] = 0;
        var p = position[slot];
        if (helm[slot] == Helm.Course && !Earth.InPolarCap(p, settings.PolarCapDeg))
            forward[slot] = Earth.Forward(p, courseDeg[slot]);
        else if (helm[slot] == Helm.Route && routeNext[slot] < routeCount[slot])
        {
            var up = p.Normalized();
            var d = route[slot * routeCap + routeNext[slot]] - p;
            var dir = d - up * Vec3d.Dot(d, up);
            if (dir.Length > 1e-6)
                forward[slot] = dir.Normalized();
        }
    }

    /// <summary>On rails riding another ship (a group's ship on its guide's), not on rails of its own.</summary>
    public bool RidesAnother(int slot) => tier[slot] != ClockTier.Fine && railGuide[slot] >= 0;

    /// <summary>The slot of the ship a ship rides on rails (its group's guide), or -1.</summary>
    public int RidingOn(int slot) => tier[slot] != ClockTier.Fine ? railGuide[slot] : -1;

    /// <summary>How far abeam of its guide a ship rides on rails, metres, either side (0 on rails of its own, or off).</summary>
    public double RailAbeamM(int slot) => RidesAnother(slot) ? Math.Abs(railY[slot]) : 0;

    /// <summary>How far a ship rides from its guide on rails, metres (0 on rails of its own, or off rails).</summary>
    public double RailOffsetM(int slot) => RidesAnother(slot) ? Math.Sqrt(railX[slot] * railX[slot] + railY[slot] * railY[slot]) : 0;

    /// <summary>A ship on rails has reached its route's next waypoint: it turns there at once for the one after (true,
    /// with that one), or, past the last, holds the course it arrived on (false). Not <paramref name="anchor"/>ed (a ship
    /// sliding along a coast on rails, passing near it), its rails are left as they are, for the caller to lay.</summary>
    public bool PassWaypoint(int slot, out Vec3d next, bool anchor = true)
    {
        int cap = routeCap;
        var wp = route[slot * cap + routeNext[slot]];
        PoseNow(slot, out _, out var arrived);
        // on to the next waypoint, past any given again where this one is (a leg of no length would be an event a
        // millisecond, for ever round a loop); a loop all at one point ends as a route does
        int n = routeCount[slot], k = routeNext[slot] + 1;
        bool loop = routeLoop[slot] && n > 1;
        for (int skipped = 0; ; skipped++, k++)
        {
            if (loop)
                k %= n;
            if (k >= n || skipped >= n)
            {
                k = n;
                break;
            }
            if ((route[slot * cap + k] - wp).LengthSquared > 1)
                break;
        }
        routeNext[slot] = k;
        if (!anchor)
        {
            bool more = k < n;
            next = more ? route[slot * cap + k] : default;
            if (!more)
            {
                PoseNow(slot, out var here, out _);
                helm[slot] = Helm.Course;
                courseDeg[slot] = Earth.Heading(here, arrived);
                routeCount[slot] = routeNext[slot] = 0;
                routeLoop[slot] = false;
            }
            return more;
        }
        if (routeNext[slot] < routeCount[slot])
        {
            next = route[slot * cap + routeNext[slot]];
            var up = wp.Normalized();
            var d = next - wp;
            var dir = (d - up * Vec3d.Dot(d, up)).Normalized();
            Reanchor(slot, wp, dir);
            courseDeg[slot] = Earth.Heading(wp, dir);
            return true;
        }
        next = default;
        helm[slot] = Helm.Course;
        courseDeg[slot] = Earth.Heading(wp, arrived);
        ReanchorRhumb(slot, wp, courseDeg[slot]);   // the helm holds the course it arrived on: a rhumb line
        routeCount[slot] = routeNext[slot] = 0;
        routeLoop[slot] = false;
        return false;
    }

    /// <summary>A ship on rails crosses into a polar cap on its course: on from here along the great circle.</summary>
    public void EnterPolarCap(int slot)
    {
        PoseNow(slot, out var p, out var f);
        Reanchor(slot, p, f);
    }

    /// <summary>A ship on rails leaves a polar cap: it holds the heading it leaves with, as a course.</summary>
    public void LeavePolarCap(int slot)
    {
        PoseNow(slot, out var p, out var f);
        courseDeg[slot] = Earth.Heading(p, f);
        ReanchorRhumb(slot, p, courseDeg[slot]);
    }

    /// <summary>Whether a ship's rails are a rhumb line (a course held), not a great circle.</summary>
    public bool OnRhumb(int slot) => tier[slot] != ClockTier.Fine && railGuide[slot] < 0 && !double.IsNaN(railCourse[slot]);

    /// <summary>Counts the changes to a ship's rails: an event stamped with an older count is stale.</summary>
    public int RailGen(int slot) => railGen[slot];

    /// <summary>Takes a ship off rails or out of coarse, exactly where they have it now, back to the fine tick. Off
    /// rails it's steady, rudder amidships, its power as it was; out of coarse it carries on as its segment had it, its
    /// speed and rate of turn, the rudder that holds that turn, and while its speed was changing the power that holds
    /// the speed it's at (the engines then go on toward what's rung), so the handover is smooth.</summary>
    public void Derail(int slot)
    {
        if (tier[slot] == ClockTier.Fine)
            return;
        PoseNow(slot, out var p, out var f);
        double v = SpeedAt(slot, NowMs), r = YawRateAt(slot, NowMs);
        bool coarse = tier[slot] == ClockTier.Coarse;
        (position[slot], forward[slot], speed[slot], poseMs[slot]) = (p, f, v, NowMs);
        yawRate[slot] = rudder[slot] = 0;
        if (coarse)
        {
            yawRate[slot] = r;
            rudder[slot] = Math.Abs(v) > 1e-9 ? handling[slot].RudderFor(r / v) : 0;
            // the power coarse didn't step: what's rung once at the speed it holds, else what holds the speed it's at
            if (railGuide[slot] < 0 && railAccel[slot] == 0)
                power[slot] = powerOrder[slot];
            else
                power[slot] = Math.Sign(v) * Math.Min(handling[slot].HoldingPowerW(Math.Abs(v)), v >= 0 ? handling[slot].MaxPowerW : handling[slot].AsternPowerW);
        }
        if (railGuide[slot] >= 0 && helm[slot] == Helm.Course)
            courseDeg[slot] = Earth.Heading(p, f);   // a ship carried by its guide comes off steering as the rails had it, not for a course they left behind
        SetTier(slot, ClockTier.Fine);
        railGen[slot]++;
    }

    public void Drop(int slot)
    {
        if (!IsAlive(slot))
            return;
        for (int i = 0; i < high; i++)
            if (alive[i] && tier[i] != ClockTier.Fine && railGuide[i] == slot)
                Derail(i);   // riding on a ship that's going: off rails first
        Derail(slot);
        alive[slot] = false;
        if (entity[slot] >= 0 && entity[slot] < slotOfEntity.Length && slotOfEntity[entity[slot]] == slot + 1)
            slotOfEntity[entity[slot]] = 0;
        fine.Remove(slot);   // a dead slot is stepped and decided for by nothing
        coarse.Remove(slot);
        handling[slot] = null!;
        generation[slot]++;
        free.Push(slot);
    }

    /// <summary>The slot of a live entity, or -1: by its index (docs/battle/plan.md step 2.7c: the AI and the rails ask
    /// it for every ship every second, and the scan it was cost the captains O(n²)).</summary>
    public int SlotOf(int entityId)
    {
        if ((uint)entityId < (uint)slotOfEntity.Length)
            return slotOfEntity[entityId] - 1;
        for (int i = 0; i < high; i++)   // a negative id (tests): the scan
            if (alive[i] && entity[i] == entityId)
                return i;
        return -1;
    }

    int[] slotOfEntity = new int[64];   // slot + 1 by entity id, 0 for none

    // ------------------------------------------------------------------ orders

    /// <summary>Hold a course, degrees, with a rudder no larger than given (radians; 0 for the standard rudder).</summary>
    public void OrderCourse(int slot, double courseDeg, double rudderRad = 0)
    {
        double was = this.courseDeg[slot], now = ((courseDeg % 360) + 360) % 360, change = now - was;
        if (helm[slot] != Helm.Course || Math.Abs(change - 360 * Math.Round(change / 360)) > settings.SlideResetDeg)
            landSide[slot] = 0;   // a new course: which way to slide along a coast is chosen again
        helm[slot] = Helm.Course;
        this.courseDeg[slot] = now;
        courseRudder[slot] = rudderRad > 0 ? Math.Min(rudderRad, handling[slot].HardOverRad) : settings.StandardRudderDeg * Math.PI / 180;
    }

    /// <summary>How far from the coast the ship must keep beyond its own standoff, metres: its formation's half-width
    /// (the pilot's, docs/world/terrain-plan.md).</summary>
    public void SetClearance(int slot, double metres) => clearance[slot] = metres;

    /// <summary>Hold a rudder angle, radians, positive to starboard.</summary>
    public void OrderRudder(int slot, double rudderRad)
    {
        helm[slot] = Helm.Rudder;
        var h = handling[slot].HardOverRad;
        rudderOrder[slot] = Math.Clamp(rudderRad, -h, h);
    }

    /// <summary>Ring a speed, knots, negative astern: the engines go to the power that holds it.</summary>
    public void OrderSpeed(int slot, double kn)
    {
        speedOrderKn[slot] = kn;
        powerOrder[slot] = handling[slot].PowerFor(kn);
    }

    public void ClearRoute(int slot)
    {
        routeCount[slot] = routeNext[slot] = 0;
        routeLoop[slot] = false;
        if (helm[slot] == Helm.Route)
            OrderCourse(slot, Earth.Heading(position[slot], forward[slot]));
    }

    /// <summary>Whether the route goes round again from its first waypoint after its last (a patrol), until it's
    /// cleared. A route of one waypoint doesn't loop.</summary>
    public void LoopRoute(int slot, bool loop) => routeLoop[slot] = loop;

    /// <summary>Adds a waypoint to the route and steers by it. False when the route is full.</summary>
    public bool AddWaypoint(int slot, Vec3d point)
    {
        int n = routeCount[slot];
        if (n == routeCap)
            return false;
        route[slot * routeCap + n] = point.Normalized() * Earth.Radius;
        routeCount[slot] = n + 1;
        if (helm[slot] != Helm.Route)
        {
            helm[slot] = Helm.Route;
            routeNext[slot] = n;
            landSide[slot] = 0;
            courseRudder[slot] = settings.StandardRudderDeg * Math.PI / 180;
        }
        return true;
    }

    // ------------------------------------------------------------------ hashing

    public void Hash(ref StateHash h)
    {
        h.Add(high);
        h.Add(Count);
        for (int i = 0; i < high; i++)
        {
            h.Add(alive[i]);
            h.Add(generation[i]);
            if (!alive[i])
                continue;
            h.Add(entity[i]);
            h.Add(classIndex[i]);
            h.Add(side[i]);
            h.Add(position[i]);
            h.Add(forward[i]);
            h.Add(speed[i]);
            h.Add(yawRate[i]);
            h.Add(rudder[i]);
            h.Add(power[i]);
            h.Add(poseMs[i]);
            h.Add((int)helm[i]);
            h.Add(rudderOrder[i]);
            h.Add(courseDeg[i]);
            h.Add(courseRudder[i]);
            h.Add(clearance[i]);
            h.Add(powerOrder[i]);
            h.Add(speedOrderKn[i]);
            h.Add(routeCount[i]);
            h.Add(routeNext[i]);
            h.Add(routeLoop[i]);
            h.Add(landSide[i]);
            h.Add(backstops[i]);
            h.Add(tier[i] != ClockTier.Fine);
            if (tier[i] == ClockTier.Coarse)
                h.Add(1);   // only then, so a world without coarse ships hashes as it did before the tier
            h.Add(railGen[i]);
            if (tier[i] != ClockTier.Fine)
            {
                h.Add(railMs[i]);
                h.Add(railPos[i]);
                h.Add(railFwd[i]);
                h.Add(railSpeed[i]);
                h.Add(railGuide[i]);
                h.Add(railX[i]);
                h.Add(railY[i]);
                h.Add(railCourse[i]);
                if (tier[i] == ClockTier.Coarse)
                {
                    h.Add(railAccel[i]);
                    h.Add(railCurv[i]);
                    h.Add(segEndMs[i]);
                    h.Add(trackAt[i]);
                    h.Add(histCount[i]);
                    for (int k = 0; k < histCount[i]; k++)
                    {
                        int t = i * trackCap + (histFirst[i] + k) % trackCap;
                        h.Add(histPos[t]);
                        h.Add(histFwd[t]);
                        h.Add(histCourse[t]);
                        h.Add(histCurv[t]);
                        h.Add(histStart[t]);
                        h.Add(histLen[t]);
                    }
                }
            }
            for (int k = 0; k < routeCount[i]; k++)
                h.Add(route[i * routeCap + k]);
        }
    }

    void Grow(int n)
    {
        fine.Grow(n);
        coarse.Grow(n);
        Array.Resize(ref entity, n);
        Array.Resize(ref generation, n);
        Array.Resize(ref classIndex, n);
        Array.Resize(ref side, n);
        Array.Resize(ref alive, n);
        Array.Resize(ref handling, n);
        Array.Resize(ref position, n);
        Array.Resize(ref forward, n);
        Array.Resize(ref speed, n);
        Array.Resize(ref yawRate, n);
        Array.Resize(ref rudder, n);
        Array.Resize(ref power, n);
        Array.Resize(ref poseMs, n);
        Array.Resize(ref helm, n);
        Array.Resize(ref rudderOrder, n);
        Array.Resize(ref courseDeg, n);
        Array.Resize(ref courseRudder, n);
        Array.Resize(ref clearance, n);
        Array.Resize(ref powerOrder, n);
        Array.Resize(ref speedOrderKn, n);
        Array.Resize(ref route, n * routeCap);
        Array.Resize(ref routeCount, n);
        Array.Resize(ref routeNext, n);
        Array.Resize(ref routeLoop, n);
        Array.Resize(ref landSide, n);
        Array.Resize(ref backstops, n);
        Array.Resize(ref tier, n);
        Array.Resize(ref railMs, n);
        Array.Resize(ref railPos, n);
        Array.Resize(ref railFwd, n);
        Array.Resize(ref railSpeed, n);
        Array.Resize(ref railX, n);
        Array.Resize(ref railY, n);
        Array.Resize(ref railGuide, n);
        Array.Resize(ref railGen, n);
        Array.Resize(ref railCourse, n);
        Array.Resize(ref railAccel, n);
        Array.Resize(ref railCurv, n);
        Array.Resize(ref segEndMs, n);
        Array.Resize(ref histPos, n * trackCap);
        Array.Resize(ref histFwd, n * trackCap);
        Array.Resize(ref histCourse, n * trackCap);
        Array.Resize(ref histCurv, n * trackCap);
        Array.Resize(ref histStart, n * trackCap);
        Array.Resize(ref histLen, n * trackCap);
        Array.Resize(ref trackAt, n);
        Array.Resize(ref histFirst, n);
        Array.Resize(ref histCount, n);
    }
}

/// <summary>One ship at sea: a handle (store and slot) with accessors. Cheap to make, holds nothing of its own.</summary>
public readonly struct ShipState(ShipStore store, int slot)
{
    public int Slot => slot;
    public bool Alive => store.IsAlive(slot);
    public int Entity => store.entity[slot];
    public int Generation => store.generation[slot];
    public Handling Handling => store.handling[slot];

    /// <summary>The class's index in the world's table of classes.</summary>
    public int ClassIndex => store.classIndex[slot];

    /// <summary>The side it's on: ships of different sides are opposing (relations between sides come in step 3.1).</summary>
    public byte Side => store.side[slot];

    /// <summary>Where the ship is now (on rails, evaluated at the world's time).</summary>
    public Vec3d Position
    {
        get
        {
            store.PoseNow(slot, out var p, out _);
            return p;
        }
    }

    public Vec3d Forward
    {
        get
        {
            store.PoseNow(slot, out _, out var f);
            return f;
        }
    }

    public ShipFrame Frame
    {
        get
        {
            store.PoseNow(slot, out var p, out var f);
            return ShipFrame.At(p, f);
        }
    }

    /// <summary>On the rails clock tier: a function of time, not stepped.</summary>
    public bool OnRails => store.tier[slot] == ClockTier.Rails;

    /// <summary>Its clock tier: fine, coarse or rails.</summary>
    public ClockTier Tier => store.tier[slot];

    /// <summary>Speed through the water, m/s (negative astern); rate of turn, rad/s (positive to starboard); rudder
    /// angle, rad; shaft power, W (negative astern).</summary>
    public double Speed => store.tier[slot] != ClockTier.Fine ? store.SpeedAt(slot, store.NowMs) : store.speed[slot];
    public double YawRate => store.tier[slot] != ClockTier.Fine ? store.YawRateAt(slot, store.NowMs) : store.yawRate[slot];
    public double Rudder => store.tier[slot] == ClockTier.Coarse ? Handling.RudderFor(store.CurvatureNow(slot)) : store.rudder[slot];
    public double Power => store.power[slot];

    /// <summary>The world time the pose is at, ms.</summary>
    public long PoseMs => store.tier[slot] != ClockTier.Fine ? store.NowMs : store.poseMs[slot];

    public double HeadingDeg
    {
        get
        {
            store.PoseNow(slot, out var p, out var f);
            return Earth.Heading(p, f);
        }
    }

    public Helm Helm => store.helm[slot];
    public double CourseOrderDeg => store.courseDeg[slot];
    public double SpeedOrderKn => store.speedOrderKn[slot];

    /// <summary>How far from the coast it must keep beyond its own standoff, m (<see cref="ShipStore.SetClearance"/>).</summary>
    public double ClearanceM => store.clearance[slot];
    public double RudderOrder => store.rudderOrder[slot];

    /// <summary>The most rudder its course or route is steered with, radians.</summary>
    public double CourseRudder => store.courseRudder[slot];

    /// <summary>The power rung (what the engines are going to), W.</summary>
    public double PowerOrder => store.powerOrder[slot];
    public int RouteCount => store.routeCount[slot];
    public int RouteNext => store.routeNext[slot];

    /// <summary>The route goes round again after its last waypoint (a patrol).</summary>
    public bool RouteLoop => store.routeLoop[slot];
    public Vec3d Waypoint(int k) => store.route[slot * store.RouteCapacity + k];

    /// <summary>Sliding along a coast (Slide): +1 with the land to port, -1 to starboard, 0 not.</summary>
    public int LandSide => store.landSide[slot];

    /// <summary>How often the backstop has put the ship back off the land.</summary>
    public int Backstops => store.backstops[slot];
}
