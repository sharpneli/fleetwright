using System.Numerics;
using Fleetwright.Map.Fires;
using Fleetwright.Map.Flashes;
using Fleetwright.Map.Magazines;
using Fleetwright.Map.Splashes;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Hits;

/// <summary>What her explosions tear out of her, in the world view (docs/visuals/destruction.md D2-D3), from the snapshot's
/// own records (<see cref="ShipsView"/>'s torn faces, her gas's ways up and burning), every ship in sight alike:
/// <list type="bullet">
/// <item><b>a tear</b>, when first seen young: the gas bursting out of it (a short flash out of the opening, as wide as what
/// tore), and its plates thrown, a few pieces by its area, dark flecks on their arcs that come down in the sea round her;</item>
/// <item><b>the venting</b> while her gas runs: out of each torn opening the gas still pushes through, a jet by the pressure
/// behind it (the magazine's port jets' law), rooted in the hole and leaning back by her way through the air;</item>
/// <item><b>a magazine burning</b> up the ways it opened (docs/battle/workers.md ET: 20-170 s): a column of flame out of each
/// barbette or gunhouse ring, Heskestad's height by the propellant's burning, its light into the fires' pool and its smoke a
/// fire's trail, brown-yellow.</item>
/// </list>
/// What's rooted in her rides with her pose now (decision 3: the hole is hers); the pieces carry her way when thrown, so they
/// ride with her too (no drag on a plate in its seconds), their splashes landing where she has gone on to. Pieces are spent
/// only on ships in view, at most <see cref="PiecesPerTear"/> a tear and <see cref="MostPieces"/> in all (decision 4), so a fight
/// off the screen never starves the one watched. Nothing allocates once grown.</summary>
public sealed class WreckEffects : IThrownParts
{
    /// <summary>The most pieces in flight at once, and from one tear (decision 4).</summary>
    public const int MostPieces = 400, PiecesPerTear = 24;

    /// <summary>A tear older than this when first seen throws nothing (it was over before the eye got there), world s.</summary>
    public const double FreshS = 8;

    /// <summary>The propellant's heat of burning, MJ/kg (cordite's ~4), for the columns' flame [EST].</summary>
    const double PropellantMJkg = 4;

    struct Piece
    {
        public int Ship;
        public Vector3 From, V;
        public float R;
        public double T0;
    }

    /// <summary>A part thrown off her (D4): when on the effects clock, from where (her frame, its foot over the waterline), its launch
    /// (her frame, m/s) and tumble, degrees a second. It carries her way, so it rides with her pose.</summary>
    sealed class Flight
    {
        public double T0, Spin, Tumble;
        public Vector3 From, V;
    }

    readonly Dictionary<long, Flight> flights = [];
    readonly Piece[] pieces = new Piece[MostPieces];
    int pieceHead, pieceCount;
    /// <summary>The tears seen, by ship and face (each throws once).</summary>
    readonly HashSet<long> seen = [];
    readonly Dictionary<int, (Vec3d At, Vec3d F, double Speed)> poses = [];

    public double Clock { get; private set; }
    public int Pieces => pieceCount;

    public void Clear()
    {
        (pieceHead, pieceCount) = (0, 0);
        seen.Clear();
        flights.Clear();
        poses.Clear();
    }

    /// <summary>A frame: the clock on (the effects', <see cref="Effects.Rate"/>), each ship in sight's tears, venting and burning
    /// drawn into <paramref name="bodies"/> and <paramref name="lights"/>, new tears' pieces thrown and their splashes handed to
    /// <paramref name="splashes"/> once, the columns' smoke laid as trails.</summary>
    public void Build(in MapView view, Snapshot s, long drawMs, double speed, double dtS, double world, double seaClock,
        Func<int, ShipClass?> classOf, MagazineRenderer bodies, FlashLights lights, SplashBook splashes, SmokeBook smoke, SmokeTrails trails)
    {
        double rate = Effects.Rate(speed);
        Clock += dtS * rate;
        poses.Clear();
        double colRatio = speed > 0 ? rate / speed : 1, seaRatio = speed > 0 ? Effects.SeaRate(speed) / speed : 1;
        foreach (var v in (ReadOnlySpan<ShipsView>)[s.Ships, s.Sighted])
            for (int k = 0; k < v.Count; k++)
            {
                if (v.TornCount[k] == 0 && v.GasMs[k] == 0 && v.ThrownCount[k] == 0 || classOf(v.Class[k]) is not { } c)
                    continue;
                var (p, f) = v.PoseAt(k, drawMs);
                poses[v.Entity[k]] = (p, f, v.Speed[k]);
                bool inView = InView(view, p, c.Length + 200);
                var up = p.Normalized();
                var stbd = Vec3d.Cross(f, up).Normalized();
                var wind = Earth.Forward(p, (s.Weather.WindFromDeg + 180) % 360) * s.Weather.WindMs - f * v.Speed[k];
                var windPlan = new Vector2((float)Vec3d.Dot(wind, view.Right), (float)Vec3d.Dot(wind, view.Up));
                Tears(view, v, k, c, p, f, stbd, windPlan, drawMs, world, inView, colRatio, seaRatio, bodies, splashes);
                Launch(v, k, c, p, f, stbd, drawMs, colRatio, seaRatio, splashes);
                if (v.GasMs[k] > 0 && v.GasKgS[k] > 0 && world > 0)
                    Columns(view, v, k, c, p, f, stbd, windPlan, wind.Length, s, drawMs, world, bodies, lights, smoke, trails);
            }
        if (world > 0)
            DrawPieces(view, world, bodies);
    }

    static bool InView(in MapView view, Vec3d p, double marginM)
    {
        if (!view.Project(p, out double x, out double y))
            return false;
        double m = marginM / view.MetresPerPixel;
        return x > -m && y > -m && x < view.Width + m && y < view.Height + m;
    }

    void Tears(in MapView view, ShipsView v, int k, ShipClass c, Vec3d p, Vec3d f, Vec3d stbd, Vector2 wind, long drawMs, double world,
        bool inView, double colRatio, double seaRatio, MagazineRenderer bodies, SplashBook splashes)
    {
        var faces = HullFaces.Of(c);
        double wl = c.Vertical.Waterline, ke = Effects.K;
        var (east, north) = (view.Right, view.Up);
        double ws = wind.Length(), lean = 0.7 * ws / (ws + 4);
        var leanWay = ws > 1e-3 ? wind / (float)ws : Vector2.Zero;
        for (int j = v.TornStart[k], end = j + v.TornCount[k]; j < end; j++)
        {
            int x = v.TornFace[j], face = x & 3;
            if (face == 3)
                continue;
            var b = c.Cells[x >> 2].Box;
            var at = faces.Point[x];
            if (face == 2)
                at = new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, b.Top);
            if (!Wreck.Shows(c, v, k, at, drawMs))
                continue;
            double torn = v.TornM2[j], dEq = Math.Sqrt(4 * torn / Math.PI);
            // which way the gas leaves: a side's outboard and up, a deck's up
            var way = face == 2 ? new P3(0, 0, 1) : new P3(0, face == 0 ? -0.8 : 0.8, 0.6);
            var sea = p + f * at.X + stbd * at.Y;
            var root = sea + sea.Normalized() * (at.Z - wl);
            var plan = f * way.X + stbd * way.Y;
            var dir = new Vector2((float)Vec3d.Dot(plan, east), (float)Vec3d.Dot(plan, north) + (float)(ke * way.Z));
            uint seed = (uint)v.Entity[k] * 2654435761u ^ (uint)x * 40503u;

            // first seen while it's young: its burst and its plates
            double ageS = (drawMs - v.TornMs[j]) / 1000.0;
            long key = (long)v.Entity[k] << 24 | (uint)x;
            if (ageS >= 0 && seen.Add(key) && ageS < FreshS && inView)
                Throw(v, k, c, at, way, torn, ageS, seed, p, f, stbd, colRatio, seaRatio, splashes);

            if (world <= 0)
                continue;
            // its burst out: a flash of the gas leaving, the first half second
            double tb = ageS * colRatio;
            if (tb >= 0 && tb < 0.6)
            {
                double u = tb / 0.6, len = Math.Min(30 * dEq, 60) * Math.Min(1, tb / 0.08);
                bodies.AddJet(view, root, Vector2.Zero, dir * (float)len, Math.Max(0.5 * dEq, 0.6 * view.MetresPerPixel), Math.Max(0.7 * dEq, 0.9 * view.MetresPerPixel),
                    1, 0.75, len, world * (1 - u * u), tb, (seed & 0xffff) / 65536f);
            }
            // venting while her gas pushes through it
            double mpa = v.TornMPa[j];
            if (v.GasMs[k] > 0 && mpa > 0.005)
            {
                double len = Math.Min(45 * dEq * Math.Sqrt(mpa / 0.1), 4 * Math.Sqrt(torn) + 10);
                var tip = dir * (float)len + leanWay * (float)(lean * len);
                bodies.AddJet(view, root, Vector2.Zero, tip, Math.Max(0.45 * dEq, 0.6 * view.MetresPerPixel), Math.Max(0.45 * dEq + 0.06 * len, 0.9 * view.MetresPerPixel),
                    0.95, 0.6, len, world * Math.Min(1, mpa / 0.02), Clock, (seed >> 5 & 0xffff) / 65536f);
            }
        }
    }

    /// <summary>A tear's plates thrown: a few pieces by its area, out along the gas's way and up, at a few tens of m/s by how much
    /// tore [EST]; each comes down in the sea round her (its splash handed now, dated ahead to its landing) unless it falls back
    /// on her.</summary>
    void Throw(ShipsView v, int k, ShipClass c, P3 at, P3 way, double torn, double ageS, uint seed, Vec3d p, Vec3d f, Vec3d stbd,
        double colRatio, double seaRatio, SplashBook splashes)
    {
        int n = Math.Clamp((int)Math.Round(2 + 1.5 * Math.Sqrt(torn)), 2, PiecesPerTear);
        double vmax = Math.Clamp(12 + 4 * Math.Sqrt(torn), 15, 60), wl = c.Vertical.Waterline;
        var from = new Vector3((float)at.X, (float)at.Y, (float)(at.Z - wl));
        uint h = seed;
        double U() => (h = Hash(h + 0x9e3779b9u)) / 4294967296.0;
        for (int i = 0; i < n; i++)
        {
            double sp = vmax * (0.35 + 0.65 * U()), spread = 0.9 * (U() - 0.5), up = 0.4 + 0.5 * U();
            double cx = Math.Cos(spread), sx = Math.Sin(spread);
            var w = new Vector3((float)(way.X * cx - way.Y * sx + 0.3 * (U() - 0.5)), (float)(way.X * sx + way.Y * cx), (float)(way.Z + up));
            var vel = Vector3.Normalize(w) * (float)sp;
            var piece = new Piece { Ship = v.Entity[k], From = from, V = vel, R = (float)(0.4 + 1.2 * U()), T0 = Clock - ageS * colRatio };
            pieces[pieceHead] = piece;
            pieceHead = (pieceHead + 1) % MostPieces;
            pieceCount = Math.Min(pieceCount + 1, MostPieces);
            // its splash where it comes down, she gone on under it (it carries her way)
            double land = MagazineDebris.Landing(from.Z, vel.Z);
            var drop = MagazineDebris.At(from, vel, land);
            if (drop.X > c.Hull.X0 && drop.X < c.Hull.X1 && Math.Abs(drop.Y) < c.Beam / 2)
                continue;   // back on her deck
            var on = p + f * (drop.X + v.Speed[k] * (land - ageS)) + stbd * drop.Y;
            double ahead = land - ageS;
            splashes.Add(on.Normalized() * Earth.Radius, MagazineBook.PieceSplashMm * piece.R, splashes.ColumnClock + ahead * colRatio,
                splashes.FoamClock + ahead * seaRatio, (float)U(), column: true);
        }
    }

    /// <summary>Each part her gas threw (the sim's record: <see cref="ShipsView.ThrownPart"/>), once when first seen: its flight from
    /// its foot, mostly up at the speed it left at (70-88° [EST]: the gas under it pushes up), turning and tumbling, and its splash
    /// where it comes down (handed now, dated ahead). One seen long after it went is simply gone.</summary>
    void Launch(ShipsView v, int k, ShipClass c, Vec3d p, Vec3d f, Vec3d stbd, long drawMs, double colRatio, double seaRatio, SplashBook splashes)
    {
        for (int j = v.ThrownStart[k], end = j + v.ThrownCount[k]; j < end; j++)
        {
            long key = (long)v.Entity[k] << 24 | (uint)v.ThrownPart[j] | 1L << 62;
            if (flights.ContainsKey(key))
                continue;
            var b = c.Components[v.ThrownPart[j]].Bounds;
            double ageS = (drawMs - v.ThrownMs[j]) / 1000.0, speed = v.ThrownV[j];
            uint h = (uint)key * 2654435761u ^ (uint)(key >> 24);
            double U() => (h = Hash(h + 0x9e3779b9u)) / 4294967296.0;
            double el = (70 + 18 * U()) * Math.PI / 180, az = 2 * Math.PI * U();
            var from = new Vector3((float)((b.X0 + b.X1) / 2), (float)((b.Y0 + b.Y1) / 2), (float)(b.Base - c.Vertical.Waterline));
            var vel = new Vector3((float)(speed * Math.Cos(el) * Math.Cos(az)), (float)(speed * Math.Cos(el) * Math.Sin(az)), (float)(speed * Math.Sin(el)));
            var fl = new Flight { T0 = Clock - Math.Max(ageS, 0) * colRatio, From = from, V = vel, Spin = (U() * 2 - 1) * 90, Tumble = (60 + 120 * U()) * (U() < 0.5 ? -1 : 1) };
            if (ageS > FreshS)
                fl.T0 = double.NegativeInfinity;   // over before it was seen: gone
            flights[key] = fl;
            if (double.IsNegativeInfinity(fl.T0))
                continue;
            double land = MagazineDebris.Landing(from.Z, vel.Z), ahead = land - ageS;
            var drop = MagazineDebris.At(from, vel, land);
            var on = p + f * (drop.X + v.Speed[k] * ahead) + stbd * drop.Y;
            double size = Math.Max(b.X1 - b.X0, b.Y1 - b.Y0);
            splashes.Add(on.Normalized() * Earth.Radius, Math.Min(MagazineBook.PieceSplashMm * size, MagazineBook.HouseSplashMm), splashes.ColumnClock + ahead * colRatio,
                splashes.FoamClock + ahead * seaRatio, (float)U(), column: true);
        }
    }

    /// <summary>Where a part thrown off a ship is now (<see cref="IThrownParts"/>): on its arc from its foot, turned and tumbling
    /// (its roof's tilt), its size by its height (the effects camera's nearness); gone once it's down in the sea.</summary>
    public bool Thrown(int entity, int component, out ThrownTurret pose)
    {
        pose = default;
        if (!flights.TryGetValue((long)entity << 24 | (uint)component | 1L << 62, out var fl))
            return false;
        double t = Clock - fl.T0, land = MagazineDebris.Landing(fl.From.Z, fl.V.Z);
        if (t < 0)
            return false;   // still on her
        if (t >= land)
        {
            pose = new ThrownTurret(true, 0, 0, 0, 0, 1, 1);
            return true;
        }
        var at = MagazineDebris.At(fl.From, fl.V, t);
        double dz = Math.Max(at.Z - fl.From.Z, 0), tumble = fl.Tumble * t;
        pose = new ThrownTurret(false, at.X - fl.From.X, at.Y - fl.From.Y, dz, fl.Spin * t, Math.Cos(tumble * Math.PI / 180), 1 + dz / 150);
        return true;
    }

    /// <summary>The pieces in flight, dark flecks on their arcs in her frame (posed on her pose now: they carry her way).</summary>
    void DrawPieces(in MapView view, double world, MagazineRenderer bodies)
    {
        double ke = Effects.K, minR = 0.7 * view.MetresPerPixel;
        var (east, north) = (view.Right, view.Up);
        for (int i = 0; i < pieceCount; i++)
        {
            ref readonly var q = ref pieces[(pieceHead - pieceCount + i + MostPieces) % MostPieces];
            double t = Clock - q.T0;
            if (t < 0 || t > MagazineDebris.Landing(q.From.Z, q.V.Z) || !poses.TryGetValue(q.Ship, out var pose))
                continue;
            var at = MagazineDebris.At(q.From, q.V, t);
            var up = pose.At.Normalized();
            var stbd = Vec3d.Cross(pose.F, up).Normalized();
            var sea = pose.At + pose.F * at.X + stbd * at.Y;
            var lift = new Vector2(0, (float)(ke * at.Z));
            bodies.AddPiece(view, sea, lift, Math.Max(q.R, minR), world);
        }
    }

    /// <summary>A magazine burning up the ways it opened: a column of flame out of each, its light and its smoke.</summary>
    void Columns(in MapView view, ShipsView v, int k, ShipClass c, Vec3d p, Vec3d f, Vec3d stbd, Vector2 wind, double windMs, Snapshot s, long drawMs,
        double world, MagazineRenderer bodies, FlashLights lights, SmokeBook smoke, SmokeTrails trails)
    {
        int ways = v.WayCount[k];
        if (ways == 0)
            return;
        double q = v.GasKgS[k] * PropellantMJkg * 1000 / ways;   // kW a way
        double ke = Effects.K, wl = c.Vertical.Waterline, ws = wind.Length(), lean = 0.6 * ws / (ws + 6);
        var leanWay = ws > 1e-3 ? wind / (float)ws : Vector2.Zero;
        for (int j = v.WayStart[k], end = j + ways; j < end; j++)
        {
            int mt = v.WayMount[j];
            ref readonly var m = ref c.Mounts[mt];
            var top = c.Components[m.Component].Bounds;
            var at = new P3(m.X, m.Y, top.Top);
            if (!Wreck.Shows(c, v, k, at, drawMs))
                continue;
            double d = Math.Max(Math.Min(top.X1 - top.X0, top.Y1 - top.Y0), 2);
            double l = Math.Max(MagazineBlast.Heskestad(q, d), 0.5 * d);
            var sea = p + f * at.X + stbd * at.Y;
            var root = sea + sea.Normalized() * (at.Z - wl);
            uint seed = (uint)v.Entity[k] * 2654435761u ^ (uint)mt * 7919u;
            for (int t = 0; t < 3; t++)
            {
                double phase = 2 * Math.PI * ((seed >> (t * 5) & 31) / 32.0);
                double puff = 1 - 0.2 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * 1.5 / Math.Sqrt(d) * Clock + phase));
                double len = l * (0.7 + 0.15 * t) * puff;
                var tip = leanWay * (float)(lean * len) + new Vector2(0, (float)(ke * len * Math.Sqrt(Math.Max(1 - lean * lean, 0))));
                var off = new Vector2((float)((t - 1) * 0.25 * d), 0);
                bodies.AddJet(view, root, off, off + tip, Math.Max(0.4 * d, 0.6 * view.MetresPerPixel), Math.Max(0.25 * d, 0.6 * view.MetresPerPixel),
                    1, 0.6, len, world, Clock + phase, ((seed >> 3) + (uint)t & 0xffff) / 65536f);
            }
            lights.AddFire(root + root.Normalized() * (0.5 * l), bodies.Light(1700, d * l, FireLook.LightShare), FlashPhotometry.Chroma(1700),
                Math.Max(d, l) / 2, view.MetresPerPixel * Math.Max(view.Width, view.Height));
            // its smoke: a fire's trail, the propellant's brown-yellow, from its tip
            double rateM2 = q / 1000 / 18 * 0.03 * 8700 * FireLook.SmokeGain;   // Q / ΔH_c × a yield × σ_s, as a fire's (SmokeSources.Fire's law) [EST]
            long key = (long)v.Entity[k] << 20 | 0x80000 | (uint)mt;
            int owner = (int)((uint)(v.Entity[k] * 64 + 63 - mt % 32) & 0xffffff) | 1 << 29;
            trails.Emit(smoke, key, owner, drawMs / 1000.0, SmokeTrails.Spacing(v.Speed[k], FireLook.EveryS), root + root.Normalized() * l,
                f * v.Speed[k], at.Z - wl + l, rateM2, 0, FireLook.SteamS, PropellantSmoke, Math.Max(d, 2), 40, 20, (float)((seed & 0xff) / 256.0), FireLook.Spare);
        }
    }

    static readonly Vector3 PropellantSmoke = new(0.45f, 0.37f, 0.22f);

    static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return x;
    }
}
