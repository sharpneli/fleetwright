using System.Numerics;
using Fleetwright.Map.Flashes;
using Fleetwright.Map.Magazines;
using Fleetwright.Map.Splashes;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Hits;

/// <summary>What a shell's hit shows in the world view (docs/visuals/hits-and-fires.md I1), each frame from the book,
/// through the models already drawn, so each is learned once:
/// <list type="bullet">
/// <item><b>a burst in the open</b> (outside a plate, in it, beyond the ship, or in a space open enough to vent as fast
/// as it formed): the magazine's fireball body at the filler's W (<see cref="MagazineBlast.Fireball"/>: 14 m and 1.1 s
/// for a 12" AP's 15 kg, 21 m and 1.7 s for a 12" HE's 50), after the fuze's delay, its light into the flashes'
/// lights by its heat;</item>
/// <item><b>a burst inside</b>: little at once: a short warm light at the burst, and its gas jetting back out of the hole
/// it came in by (the magazine's port jets at small scale: 45 d √2 at most, d the hole's and what the burst tore open);</item>
/// <item><b>a plate that held</b> (stopped, broke it up, holed it in pieces, glanced it off): a hard flash at the plate,
/// about 0.1 s; a shell through thin plating flashes smaller at each hole;</item>
/// <item><b>smoke</b>, on the gun smoke's book: the burst's (its A from the filler, its tint from the filler's RE: black
/// powder white-grey to the high explosives' dark grey-brown, placeholders), the vent's out of the hole, a hole's dust.</item>
/// </list>
/// A hit under the water shows nothing here (its boil is I3's). Each point anchors at its own height (drawn at its place
/// on the sprite), heights over it going up the screen through the effects camera. Nothing allocates.</summary>
public static class HitEffects
{
    /// <summary>The reference calibre the plate flash and the dust are sized against, mm.</summary>
    const double RefCalibreMm = 305;

    /// <summary>Smoke's tints, linear: black powder's white-grey, the high explosives' grey-brown [EST, gap 2]; a hole's
    /// dust and paint, a light grey.</summary>
    static readonly Vector3 PowderSmoke = new(0.62f, 0.61f, 0.6f), HeSmoke = new(0.22f, 0.2f, 0.18f), DustTint = new(0.5f, 0.48f, 0.46f);

    /// <summary>A frame's hits: the bursts, flashes and vents into the magazine renderer's instances, their light into
    /// the flashes' (finished again), their smoke handed once to the smoke's book (<paramref name="smokeNow"/> its clock).</summary>
    public static void Build(in MapView view, HitBook book, double world, MagazineRenderer bodies, FlashLights lights, SmokeBook smoke,
        double smokeNow)
    {
        if (!HitLook.On || book.Count == 0)
            return;
        for (int i = 0; i < book.Count; i++)
        {
            ref var e = ref book.Edit(i);
            if (!e.Smoked)
            {
                e.Smoked = true;
                if ((HitLook.Parts & 16) != 0)
                    Smoke(e, book, smoke, smokeNow);
            }
            if (world <= 0 || !bodies.Ready || HitLook.AtS >= 0 && !book.InStill(e))
                continue;
            double age = HitLook.AtS >= 0 ? HitLook.AtS : book.Clock - e.T0;
            if (age < 0)
                continue;
            Draw(view, e, age, world, bodies, lights);
        }
        lights.Finish(view);
    }

    /// <summary>Whether a hit's burst shows whole: in the open, on or in a plate, or beyond her; a space open enough
    /// vents it as fast as it forms.</summary>
    static bool Open(in HitBook.Entry e) =>
        e.Burst is BurstKind.Outside or BurstKind.InPlate || e.Outcome == Outcome.BurstBeyond || (e.Shows & HitShows.Open) != 0;

    /// <summary>Whether it burst: on, in or inside her, or beyond her far side.</summary>
    static bool Bursts(in HitBook.Entry e) => (e.Burst != BurstKind.None || e.Outcome == Outcome.BurstBeyond) && e.FillerKg > 0;

    static bool Under(in HitBook.Entry e, P3 p) => (e.Shows & HitShows.Underwater) != 0 && p.Z < 0;

    static void Draw(in MapView view, in HitBook.Entry e, double age, double world, MagazineRenderer bodies, FlashLights lights)
    {
        double cal = e.CalibreMm / RefCalibreMm, k = Effects.K;
        double tb = age - e.BurstS;
        var (d, life) = MagazineBlast.Fireball(Math.Max(e.FillerKg, 0));
        d *= HitLook.Scale;

        if (Bursts(e) && tb >= 0 && !Under(e, e.BurstAt))
        {
            var sea = e.Sea(e.BurstAt);
            var up = sea.Normalized();
            double h = e.BurstAt.Z;
            if (Open(e))
            {
                double dd = (e.Burst == BurstKind.Inside && e.Outcome != Outcome.BurstBeyond ? HitLook.OpenShare : 1) * d;
                // its centre at the burst at first (BodyAt's: 0.7 of 0.35 of its radius over its foot)
                if ((HitLook.Parts & 1) != 0)
                    bodies.AddBody(view, sea, h - 0.1225 * dd, dd, life, tb, e.Seed, world);
                if ((HitLook.Parts & 8) != 0 && tb < 2 * life)
                {
                    var b = MagazineBlast.BodyAt(dd, life, h - 0.1225 * dd, tb);
                    if (b.Heat > 0.05)
                    {
                        double kel = MagazineLook.ColdK + (MagazineLook.HotK - MagazineLook.ColdK) * b.Heat;
                        lights.Add(sea + up * b.CentreZ, bodies.Light(kel, Math.PI * b.R * b.R, HitLook.LightShare) * (1 - 0.7 * b.Skin),
                            FlashPhotometry.Chroma(kel), b.R);
                    }
                }
            }
            else
            {
                // inside: a warm light through what's open, and its gas back out of the hole it came in by
                double lit = HitLook.InsideLightShare * life;
                if ((HitLook.Parts & 8) != 0 && tb < lit)
                {
                    double u = tb / lit, kel = 1300 + 500 * (1 - u);
                    lights.Add(sea + up * h, bodies.Light(kel, Math.PI * 0.09 * d * d, HitLook.LightShare) * (1 - u) * (1 - u), FlashPhotometry.Chroma(kel), 0.3 * d);
                }
                double vent = HitLook.VentShare * life;
                if ((HitLook.Parts & 4) != 0 && tb < vent)
                {
                    double hole = Math.PI * Math.Pow(1.2 * e.CalibreMm / 1000, 2) / 4 + Math.Max(e.VentM2, 0);
                    double dEq = Math.Sqrt(4 * hole / Math.PI);
                    double len = Math.Min(45 * dEq * Math.Sqrt(2), HitLook.VentReach * d) * Math.Min(1, tb / 0.08) * (1 - SmoothStep(0.5 * vent, vent, tb));
                    if (len > 0.5)
                    {
                        var entry = e.Sea(e.Point);
                        var way = new P3(-e.Direction.X, -e.Direction.Y, -e.Direction.Z);   // back out along its line in
                        var (east, north) = (view.Right, view.Up);
                        var plan = e.Forward * way.X + e.Starboard * way.Y;
                        var tip = new Vector2((float)(Vec3d.Dot(plan, east) * len), (float)(Vec3d.Dot(plan, north) * len + k * way.Z * len));
                        double r0 = Math.Max(0.45 * dEq, 0.6 * view.MetresPerPixel), r1 = Math.Max(0.45 * dEq + 0.06 * len, 0.9 * view.MetresPerPixel);
                        bodies.AddJet(view, entry, Vector2.Zero, tip, r0, r1, 1, 0.7, len, world * (1 - tb / vent), tb, e.Seed);
                    }
                }
            }
        }

        if ((HitLook.Parts & 2) == 0)
            return;
        // a plate that held: a hard flash where it struck; through thin plating, a smaller one at each hole
        bool plate = e.Outcome is Outcome.Defeated or Outcome.BrokeUp or Outcome.Holed or Outcome.Ricochet or Outcome.Dud;
        bool through = e.Outcome is Outcome.PassedThrough;
        double fs = HitLook.PlateFlashS;
        if ((plate || through) && age < fs && !Under(e, e.Point))
            Flash(view, e, e.Point, HitLook.PlateFlashM * cal * (through ? HitLook.ThroughShare : 1), age / fs, world, bodies, lights, age);
        if (e.Outcome == Outcome.Ricochet && (e.Shows & HitShows.Exit) != 0)
            Sparks(view, e, age, world, bodies);
        if (through && (e.Shows & HitShows.Exit) != 0 && age < fs)
            Flash(view, e, e.ExitAt, HitLook.PlateFlashM * cal * HitLook.ThroughShare, age / fs, world, bodies, lights, age);
    }

    /// <summary>What flies off each hit, once, into the splash book (I2), dated by its moment in the world on the book's
    /// clocks at the clock's rates (seen late, back-dated; still to come, it waits):
    /// <list type="bullet">
    /// <item>a shell that leaves her whole (through her, or glanced off) flies on from where it left her, falling under
    /// gravity alone (drag left out: display only), and lands as a full splash of its calibre. Decision 4: display only,
    /// the spotter doesn't see it;</item>
    /// <item>a burst in the open throws its pieces of 50 g and more (E1's count) out round it: a share land in the sea
    /// as foam marks within a few of their drag lengths, none on her;</item>
    /// <item>a plate that held (stopped, shattered) drops a few large pieces (the cap, the broken body) into the sea
    /// close by on the side it came from.</item>
    /// </list></summary>
    public static void Hand(HitBook book, long nowMs, double speed, SplashBook splashes, Func<int, ShipClass?> classOf)
    {
        if (!HitLook.On)
            return;
        double colRatio = speed > 0 ? Effects.Rate(speed) / speed : 1, seaRatio = speed > 0 ? Effects.SeaRate(speed) / speed : 1;
        for (int i = 0; i < book.Count; i++)
        {
            ref var e = ref book.Edit(i);
            if (e.Handed)
                continue;
            e.Handed = true;
            if ((HitLook.Parts & 32) == 0 || classOf(e.Class) is not { } cls)
                continue;
            double ago = (nowMs - e.Ms) / 1000.0;

            // flying on
            if ((e.Shows & HitShows.Exit) != 0 && e.ExitMs > 0)
            {
                var d = e.ExitDirection;
                double v = e.ExitMs, vz = v * d.Z, h = Math.Max(e.ExitAt.Z, 0);
                double t = Math.Min((vz + Math.Sqrt(vz * vz + 2 * G * h)) / G, MaxFlightS);
                var horiz = e.Forward * d.X + e.Starboard * d.Y;
                Land(splashes, e, e.Sea(e.ExitAt) + horiz * (v * t), t - ago, colRatio, seaRatio, e.CalibreMm, 1, true);
            }

            // a burst's pieces in the sea round it
            if (Bursts(e) && Open(e) && e.Pieces > 0 && !Under(e, e.BurstAt))
            {
                int n = (int)Math.Min(Math.Round(e.Pieces * HitLook.PieceShare), HitLook.PieceMarks);
                double reach = Fragments.DragL1 * Math.Cbrt(0.05);
                for (int j = 0; j < n; j++)
                {
                    double a = 2 * Math.PI * U(e.Seed, 10 + (uint)j), r = reach * (0.4 + 1.6 * U(e.Seed, 200 + (uint)j));
                    var p = new P3(e.BurstAt.X + r * Math.Cos(a), e.BurstAt.Y + r * Math.Sin(a), 0);
                    if (p.X > cls.Hull.X0 && p.X < cls.Hull.X1 && Math.Abs(p.Y) < cls.Beam / 2)
                        continue;   // on her: no mark on the water
                    Land(splashes, e, e.Sea(p), e.BurstS + r / PieceMs - ago, colRatio, seaRatio, HitLook.PieceMm, 400 + (uint)j, false);
                }
            }

            // a plate that held: a few large pieces off the side it came from
            if (e.Outcome is Outcome.Defeated or Outcome.BrokeUp && !Under(e, e.Point))
            {
                int n = 2 + (int)(3 * U(e.Seed, 7));
                double bx = -e.Direction.X, by = -e.Direction.Y, bl = Math.Max(Math.Sqrt(bx * bx + by * by), 1e-6);
                for (int j = 0; j < n; j++)
                {
                    double off = 4 + 20 * U(e.Seed, 30 + (uint)j), along = 8 * (U(e.Seed, 40 + (uint)j) - 0.5);
                    var p = new P3(e.Point.X + bx / bl * off - by / bl * along, e.Point.Y + by / bl * off + bx / bl * along, 0);
                    double fall = Math.Sqrt(2 * Math.Max(e.Point.Z, 0.5) / G) + 0.3 * U(e.Seed, 50 + (uint)j);
                    Land(splashes, e, e.Sea(p), fall - ago, colRatio, seaRatio, HitLook.FallenShare * e.CalibreMm, 60 + (uint)j, true);
                }
            }
        }
    }

    /// <summary>A splash of what flew off, <paramref name="ahead"/> world seconds from now (past: back-dated).</summary>
    static void Land(SplashBook splashes, in HitBook.Entry e, Vec3d point, double ahead, double colRatio, double seaRatio, double calibreMm, uint k,
        bool column) =>
        splashes.Add(point.Normalized() * Earth.Radius, calibreMm, splashes.ColumnClock + ahead * colRatio, splashes.FoamClock + ahead * seaRatio,
            (float)U(e.Seed, k), column);

    const double G = 9.81, MaxFlightS = 40;
    /// <summary>The pieces' mean speed out to where they fall, m/s (they slow from ~1,000 by drag) [EST].</summary>
    const double PieceMs = 400;

    /// <summary>A number in [0, 1) from a hit's seed, one per <paramref name="k"/>.</summary>
    static double U(float seed, uint k)
    {
        uint x = (uint)(seed * 65536) * 2654435761u ^ k * 40503u;
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return x / 4294967296.0;
    }

    /// <summary>A glance's sparks: steel scraped off the plate, flying on along the shell's way out and fanning out, under
    /// gravity, for their part of a second [EST].</summary>
    static void Sparks(in MapView view, in HitBook.Entry e, double age, double world, MagazineRenderer bodies)
    {
        var sea = e.Sea(e.Point);
        var (east, north) = (view.Right, view.Up);
        double k = Effects.K, minR = 0.6 * view.MetresPerPixel;
        var d = e.ExitDirection;
        for (uint j = 0; j < SparkCount; j++)
        {
            double life = 0.25 + 0.3 * U(e.Seed, 70 + j);
            if (age >= life)
                continue;
            double speed = 40 + 50 * U(e.Seed, 80 + j), spread = 0.6 * (U(e.Seed, 90 + j) - 0.5), lift = 0.4 * U(e.Seed, 100 + j);
            // its way: the shell's out, turned a little about the vertical and lifted
            double cx = Math.Cos(spread), sx = Math.Sin(spread);
            var w = new P3(d.X * cx - d.Y * sx, d.X * sx + d.Y * cx, d.Z + lift);
            var a = SparkAt(e, w, speed, Math.Max(age - 0.03, 0), east, north, k);
            var b = SparkAt(e, w, speed, age, east, north, k);
            double u = age / life;
            bodies.AddJet(view, sea, a, b, Math.Max(0.25, minR), Math.Max(0.2, minR), 1 - 0.3 * u, 0.8 - 0.3 * u, 0, world * (1 - u), age, e.Seed);
        }
    }

    /// <summary>A spark t s out along way w at speed, drawn through the effects camera from its plate.</summary>
    static Vector2 SparkAt(in HitBook.Entry e, P3 w, double speed, double t, Vec3d east, Vec3d north, double k)
    {
        var plan = e.Forward * (w.X * speed * t) + e.Starboard * (w.Y * speed * t);
        double z = w.Z * speed * t - 0.5 * G * t * t;
        return new((float)Vec3d.Dot(plan, east), (float)(Vec3d.Dot(plan, north) + k * z));
    }

    const uint SparkCount = 7;

    /// <summary>A hard flash at a point of her frame, u of the way through its life.</summary>
    static void Flash(in MapView view, in HitBook.Entry e, P3 p, double r, double u, double world, MagazineRenderer bodies, FlashLights lights, double age)
    {
        var sea = e.Sea(p);
        r = Math.Max(r * (0.6 + 0.6 * u), 2 * view.MetresPerPixel);
        bodies.AddJet(view, sea, Vector2.Zero, Vector2.Zero, r, r, 1 - 0.4 * u, 1 - 0.4 * u, 0, (1 - u * u) * world, age, e.Seed);
        if ((HitLook.Parts & 8) != 0)
            lights.Add(sea + sea.Normalized() * p.Z, bodies.Light(2200, Math.PI * r * r, HitLook.LightShare) * (1 - u), FlashPhotometry.Chroma(2200), r);
    }

    /// <summary>A hit's smoke, once, dated on the smoke's clock to when it bursts (ahead of it: it waits, ramped from
    /// nothing): the burst's, or the vent's out of the hole; a hole's dust.</summary>
    static void Smoke(in HitBook.Entry e, HitBook book, SmokeBook smoke, double smokeNow)
    {
        // the hit's moment on the smoke's clock (the book's sea clock runs at the same rate); a fuze's delay is
        // milliseconds, left out
        double hitSea = smokeNow - (book.SeaClock - e.SeaT0);
        int key = (e.Ship & 0xffffff) | 1 << 26;
        double cal = e.CalibreMm / RefCalibreMm;
        if (Bursts(e) && !Under(e, e.BurstAt))
        {
            var (d, life) = MagazineBlast.Fireball(e.FillerKg);
            d *= HitLook.Scale;
            bool open = Open(e);
            var at = open ? e.BurstAt : e.Point;
            var sea = e.Sea(at);
            double a = e.FillerKg * 1000 * HitLook.SmokeM2g * (open ? 1 : HitLook.VentSmokeShare);
            float soot = (float)Math.Clamp((e.FillerRe - 0.55) / 0.45, 0, 1);
            smoke.Add(new SmokeBook.Puff
            {
                Born = hitSea, Origin = sea + sea.Normalized() * at.Z, Z0 = (float)(at.Z + 0.3 * d), Anchor = (float)at.Z,
                Lambda = (float)(0.5 * d), Ap = (float)a, Aw0 = 0, FogS = 1, RampS = (float)Math.Max(0.6 * life, 0.2),
                Tint = Vector3.Lerp(PowderSmoke, HeSmoke, soot), Seed = e.Seed, Shooter = key, Keep = true,
            });
        }
        if (e.Outcome is Outcome.PassedThrough && !Under(e, e.Point))
        {
            Dust(smoke, e, e.Point, hitSea, cal, key);
            if ((e.Shows & HitShows.Exit) != 0)
                Dust(smoke, e, e.ExitAt, hitSea, cal, key);
        }
    }

    /// <summary>A hole's dust and paint, a small light puff.</summary>
    static void Dust(SmokeBook smoke, in HitBook.Entry e, P3 p, double born, double cal, int key)
    {
        var sea = e.Sea(p);
        smoke.Add(new SmokeBook.Puff
        {
            Born = born, Origin = sea + sea.Normalized() * p.Z, Z0 = (float)p.Z, Anchor = (float)p.Z, Lambda = (float)(1.5 * Math.Sqrt(cal) + 0.5),
            Ap = (float)(HitLook.DustM2 * cal * cal), Aw0 = 0, FogS = 1, RampS = 0.15f, Tint = DustTint, Seed = e.Seed, Shooter = key, Keep = true,
        });
    }

    static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
