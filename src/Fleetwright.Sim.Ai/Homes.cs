using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>Where ships go out of the fight (docs/battle/command.md "home"): a side's home, and a ship's own while an aim
/// of her side's wants her somewhere (docs/battle/aims.md A2): the zone of its first open Reach naming her that she hasn't
/// arrived for, else the last she arrived at (<see cref="AimBook.ZoneOf"/>), so going home, a cripple's escape and a withdrawal all make for the beach
/// or the strait. How near counts as there: the side's <see cref="SimSettings.HomeRadiusM"/>, a zone's half its radius
/// (no more than that), so a column stopped about its guide lies inside. The one place every layer and the judge ask.</summary>
public sealed class Homes(AimBook aims)
{
    readonly Vec3d?[] sides = new Vec3d?[256];

    /// <summary>A side's home, if it has one.</summary>
    public Vec3d? OfSide(int side) => sides[side & 255];

    internal void Set(int side, Vec3d? at) => sides[side & 255] = at;

    /// <summary>A ship's home and how near counts as there, if she has one.</summary>
    public (Vec3d At, double RadiusM)? Of(int side, int ship, double homeRadiusM) =>
        aims.ZoneOf(side, ship) is { } z ? (z.Centre, Math.Min(homeRadiusM, z.RadiusM / 2))
        : sides[side & 255] is { } p ? (p, homeRadiusM) : null;

    internal void Hash(ref StateHash h)
    {
        for (int side = 0; side < sides.Length; side++)
            if (sides[side] is { } p)
            {
                h.Add(side);
                h.Add(p.X);
                h.Add(p.Y);
                h.Add(p.Z);
            }
    }
}
