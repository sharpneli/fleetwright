using System.Numerics;
using Fleetwright.Map.Flashes;
using Fleetwright.Map.Splashes;
using Fleetwright.Sim;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Torpedoes;

/// <summary>What a torpedo's launch and hit leave in the world view (docs/visuals/torpedoes.md R4, R5), handed once a run
/// to the books already drawn, so each is one model:
/// <list type="bullet">
/// <item>a deck mount's launch: the impulse charge's puff at the tube's mouth (the gun smoke's book) and, where the
/// torpedo falls in clear of the side, a small splash (a few metres: as a 25 mm shell's) and its foam;</item>
/// <item>a submerged tube's: the air that ejects it, a boil of foam beside the ship over the tube (no column);</item>
/// <item>a hit: the column through the splash model's torpedo kind, its height H ≈ 12 W^⅓ m from the charge W, kg (68 m at
/// 180 kg: an estimate), wider than a shell's and greyed by the smoke; the explosion's smoke and spray on the smoke's
/// book.</item>
/// </list>
/// Each is dated by its moment in the world, converted to each book's clock at the clock's rate, so one seen late is
/// back-dated and one still to come waits. A run first seen long after its launch (a world loaded mid-run) shows no
/// launch. Never thinned. Nothing allocates.</summary>
public static class TorpedoEffects
{
    /// <summary>The hit's column per kg^⅓ of charge, m, and the launch's splash and the tube's boil as shells' calibres, mm.</summary>
    const double ColumnPerKgThird = 12, EntryCalibreMm = 25, BoilCalibreMm = 100;
    /// <summary>A mount's torpedo falls in this far out from the side, m, this long after it's fired, s.</summary>
    const double EntryOutM = 6, EntryS = 0.8;
    /// <summary>A launch seen more than this after it happened shows nothing, s.</summary>
    const double LateS = 30;

    /// <summary>Cordite's smoke, a light grey; the hit's, a grey-brown (linear).</summary>
    static readonly Vector3 Impulse = new(0.55f, 0.55f, 0.56f), HitSmoke = new(0.3f, 0.28f, 0.26f);

    public static void Hand(TorpedoBook book, long nowMs, double speed, SplashBook splashes, SmokeBook smoke, double smokeNow,
        Func<int, ShipClass?> classOf)
    {
        double colRatio = speed > 0 ? Effects.Rate(speed) / speed : 1, seaRatio = speed > 0 ? Effects.SeaRate(speed) / speed : 1;
        for (int i = 0; i < book.Count; i++)
        {
            ref var t = ref book.Edit(i);
            if (!t.LaunchShown)
            {
                t.LaunchShown = true;
                if ((nowMs - t.LaunchMs) / 1000.0 <= LateS)
                    Launch(t, nowMs, colRatio, seaRatio, splashes, smoke, smokeNow, classOf);
            }
            if (t.Ended && t.How == TorpedoEnd.Hit && !t.HitShown)
            {
                t.HitShown = true;
                Hit(t, nowMs, colRatio, seaRatio, splashes, smoke, smokeNow);
            }
        }
    }

    static void Launch(in TorpedoBook.Run t, long nowMs, double colRatio, double seaRatio, SplashBook splashes, SmokeBook smoke, double smokeNow,
        Func<int, ShipClass?> classOf)
    {
        var c = t.Class >= 0 ? classOf(t.Class) : null;
        if (c == null || t.Launcher < 0 || t.Launcher >= c.Launchers.Length)
            return;
        ref readonly var row = ref c.Launchers[t.Launcher];
        float seed = Seed(t.Serial, 1);
        double ago = (nowMs - t.LaunchMs) / 1000.0;   // world seconds since it was fired
        if (row.Kind == LauncherKind.Tube)
        {
            splashes.Add(t.Origin, BoilCalibreMm, splashes.ColumnClock - ago * colRatio, splashes.FoamClock - ago * seaRatio, seed, column: false);
            return;
        }
        // the tube's mouth on deck, and where it falls in, clear of the side (a centreline mount's torpedo crosses half the
        // beam first)
        double outM = Math.Max(c.Beam / 2 - Math.Abs(row.Y), 0) + EntryOutM;
        double runS = outM / Math.Max(t.SpeedMs, 1);
        var entry = t.At(runS);
        double entryAgo = ago - EntryS;
        splashes.Add(entry, EntryCalibreMm, splashes.ColumnClock - entryAgo * colRatio, splashes.FoamClock - entryAgo * seaRatio, seed);
        var up = t.Origin.Normalized();
        smoke.Add(new SmokeBook.Puff
        {
            Born = smokeNow - ago * seaRatio, Origin = t.Origin + up * 3, Z0 = 3, Anchor = 3, Lambda = 2.5f,
            Ap = 60, Aw0 = 0, FogS = 1, RampS = 0.3f, Tint = Impulse, Seed = Seed(t.Serial, 2), Shooter = t.Shooter | 1 << 28,
        });
    }

    static void Hit(in TorpedoBook.Run t, long nowMs, double colRatio, double seaRatio, SplashBook splashes, SmokeBook smoke, double smokeNow)
    {
        double ago = (nowMs - t.LaunchMs) / 1000.0 - t.EndS;
        var at = t.At(t.EndS);
        double h = ColumnPerKgThird * Math.Cbrt(Math.Max(t.ChargeKg, 1));
        splashes.Add(at, h / 180 * 1000, splashes.ColumnClock - ago * colRatio, splashes.FoamClock - ago * seaRatio, Seed(t.Serial, 3),
            torpedoHit: true);
        var up = at.Normalized();
        smoke.Add(new SmokeBook.Puff
        {
            Born = smokeNow - ago * seaRatio, Origin = at + up * 0.3 * h, Z0 = (float)(0.3 * h), Anchor = 0, Lambda = (float)(0.25 * h),
            Ap = (float)(40 * h), Aw0 = (float)(60 * h), FogS = 6, RampS = 1, Tint = HitSmoke, Seed = Seed(t.Serial, 4),
            Shooter = (int)(t.Serial & 0xffffff) | 1 << 27, Keep = true,
        });
    }

    static float Seed(long serial, uint k)
    {
        uint n = (uint)serial * 2654435761u ^ k * 40503u;
        n = (n ^ (n >> 13)) * 1274126177u;
        return (n & 0xffff) / 65536f;
    }
}
