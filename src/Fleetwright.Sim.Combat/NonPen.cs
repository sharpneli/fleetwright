using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>Where on a turret a hit that held struck (damage/13 §1.4): the barbette exposed above the deck, near the
/// ring; the barbette at or under the deck (or the glacis); the gunhouse's face, side (its rear too) or roof.</summary>
public enum TurretSpot : byte
{
    BarbetteExposed,
    BarbetteDeck,
    Face,
    Side,
    Roof,
}

/// <summary>What a hit that held did to a turret (damage/13 §1.2): jammed in train, stiff (trains slowly), one gun out,
/// its sights out, its crew stunned with nothing lasting, or nothing.</summary>
public enum TurretBlow : byte
{
    None,
    Jam,
    Stiff,
    GunOut,
    Sights,
    Stun,
}

/// <summary>What jammed a turret, which says how long (damage/13 §1.3): debris in the gap, roller path or rack (the crew
/// clears it), the deck or glacis heaved into it (cut free), the ring or training gear distorted (a dockyard's).</summary>
public enum JamCause : byte
{
    None,
    Debris,
    Deck,
    Ring,
}

/// <summary>A turret's outcome: the blow and a jam's cause, how long it lasts (ms; <see cref="NonPen.Battle"/> for the
/// rest of the battle), and the crew's stun with it (ms, 0 for none).</summary>
public readonly record struct TurretHarm(TurretBlow Blow, JamCause Cause, long Ms, long StunMs);

/// <summary>What a plate that held shakes (docs/battle/shells-armour-damage.md E4; research docs/battle/damage/
/// 13_nonpenetrating_hits.md, <c>nonpen_ref.py</c>): a shell that didn't get through still gives the plate its whole
/// impulse I = m·v, and how near it came is the margin r, the plate over what the shell beats there. Pure; the draws
/// come from the stream it's given.</summary>
public static class NonPen
{
    /// <summary>An outcome lasting the rest of the battle: until the dockyard.</summary>
    public const long Battle = long.MaxValue;

    /// <summary>How much of the heavy hit's rate a shell gets, by its impulse (N·s): an 8" about 0.4, a 15" 1, a 16" Mk 8
    /// the most (<see cref="SimSettings.NonPenSizeMax"/>).</summary>
    public static double SizeK(double impulse, SimSettings s) => Math.Min(s.NonPenSizeMax, Math.Sqrt(Math.Max(impulse, 0) / s.NonPenImpulseRef));

    /// <summary>A near thing (r about 1) throws plate and spall, a shell far short only dents: 1 to r 1, 0.7 at 2, 0.4
    /// from 3.</summary>
    public static double MarginK(double r) => Math.Clamp(1.3 - 0.3 * r, 0.4, 1);

    /// <summary>The margin r from u = v/V_L at the plate: the plate a shell beats goes as v^1.4 (Thompson's, as the
    /// matchup's spall band has it), so r = u^−1.4; a shell with no u there (NaN) counts as a near thing.</summary>
    public static double Margin(double u) => double.IsNaN(u) ? 1 : Math.Pow(Math.Max(u, 1e-3), -1.4);

    /// <summary>Where a turret or barbette plate was struck: a barbette above the main deck (z over 0, the ship's frame) is
    /// exposed near the ring, at or under it it's the deck's; a turret's rear counts as its side.</summary>
    public static TurretSpot Spot(PlateKind plate, double z) => plate switch
    {
        PlateKind.Barbette => z > 0 ? TurretSpot.BarbetteExposed : TurretSpot.BarbetteDeck,
        PlateKind.TurretFace => TurretSpot.Face,
        PlateKind.TurretRoof => TurretSpot.Roof,
        _ => TurretSpot.Side,
    };

    /// <summary>A turret or barbette that held a shell of an impulse at a margin (damage/13 §1.4, <c>turret_nonpen</c>): by
    /// where it struck, the table's chances times size and margin (the stun with nothing lasting by half of them), a shell
    /// that burst on it by <see cref="SimSettings.TurretBurstK"/>, a thin barbette's by
    /// <see cref="SimSettings.ThinBarbetteK"/>; one outcome drawn, then its length (a jam by its cause), and with a lasting
    /// one the crew stunned too (<see cref="Stun"/>).</summary>
    public static TurretHarm Turret(TurretSpot spot, double impulse, double r, bool burst, bool thin, SimSettings s, ref RandomStream rng)
    {
        double k = SizeK(impulse, s) * MarginK(r);
        double x = rng.NextDouble();
        int row = (int)spot * 5;
        var blow = TurretBlow.None;
        for (int o = 0; o < 5; o++)
        {
            double p = s.TurretNonPen[row + o] * (o == 4 ? 0.5 + 0.5 * k : k);
            if (burst)
                p *= s.TurretBurstK[o];
            if (thin && spot <= TurretSpot.BarbetteDeck)
                p *= s.ThinBarbetteK;
            if (x < p)
            {
                blow = (TurretBlow)(o + 1);
                break;
            }
            x -= p;
        }
        switch (blow)
        {
            case TurretBlow.Jam:
                double c = rng.NextDouble(), debris = s.JamCause[(int)spot * 3], deck = s.JamCause[(int)spot * 3 + 1];
                var cause = c < debris ? JamCause.Debris : c < debris + deck ? JamCause.Deck : JamCause.Ring;
                long jam = cause switch
                {
                    JamCause.Debris => LogMinutes(s.JamDebrisMin, ref rng),
                    JamCause.Deck => LogMinutes(s.JamDeckMin, ref rng),
                    _ => Battle,
                };
                return new TurretHarm(blow, cause, jam, Stun(impulse, s.TurretStunChance, s, ref rng));
            case TurretBlow.Stiff:
                return new TurretHarm(blow, JamCause.None, rng.NextDouble() < s.StiffBattleShare ? Battle : LogMinutes(s.StiffMin, ref rng),
                    Stun(impulse, s.TurretStunChance, s, ref rng));
            case TurretBlow.GunOut:
                return new TurretHarm(blow, JamCause.None, rng.NextDouble() < s.GunOutBattleShare ? Battle : LogMinutes(s.GunOutMin, ref rng),
                    Stun(impulse, s.TurretStunChance, s, ref rng));
            case TurretBlow.Sights:
                return new TurretHarm(blow, JamCause.None, Battle, Stun(impulse, s.TurretStunChance, s, ref rng));
            case TurretBlow.Stun:
                return new TurretHarm(blow, JamCause.None, 0, StunMs(s, ref rng));
            default:
                return default;
        }
    }

    /// <summary>The struck structure's crew stunned (damage/13 §3.4, a turret's or a conning tower's): with a chance
    /// scaled by the shock's (I / <see cref="SimSettings.NonPenImpulseRef"/>)^0.7, at most
    /// <see cref="SimSettings.ShockSizeMax"/>; how long, ms (0: not stunned).</summary>
    public static long Stun(double impulse, double chance, SimSettings s, ref RandomStream rng) =>
        rng.NextDouble() < chance * ShockK(impulse, s) ? StunMs(s, ref rng) : 0;

    /// <summary>The shock's scale by impulse (damage/13 §3.4): (I / I_ref)^0.7, at most <see cref="SimSettings.ShockSizeMax"/>.</summary>
    public static double ShockK(double impulse, SimSettings s) => Math.Min(s.ShockSizeMax, Math.Pow(Math.Max(impulse, 0) / s.NonPenImpulseRef, 0.7));

    /// <summary>A stun's length, ms: <see cref="SimSettings.StunS"/> seconds, or with <see cref="SimSettings.KeyManShare"/>
    /// a key man out <see cref="SimSettings.KeyManMin"/> minutes.</summary>
    static long StunMs(SimSettings s, ref RandomStream rng) => rng.NextDouble() < s.KeyManShare
        ? LogMinutes(s.KeyManMin, ref rng)
        : (long)(1000 * (s.StunS[0] + (s.StunS[1] - s.StunS[0]) * rng.NextDouble()));

    /// <summary>Minutes drawn uniform in log space between two, as ms.</summary>
    static long LogMinutes(double[] range, ref RandomStream rng) =>
        (long)(60_000 * Math.Exp(Math.Log(range[0]) + (Math.Log(range[1]) - Math.Log(range[0])) * rng.NextDouble()));
}
