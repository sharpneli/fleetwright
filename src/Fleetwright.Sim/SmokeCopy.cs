using System.Runtime.InteropServices;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>A puff as the drawing reads it (docs/battle/smoke.md Z5; smokefield.glsl, std430: five vec4s): its place on
/// the sea in the air's frame at the copy (its drift so far folded in) as an offset from the copy's anchor, m, and its
/// birth, s before the copy (negative); its
/// height at birth, the rise and its time, its extinction area at birth; its spreads at birth, m², and the turbulence
/// up that spreads it (σ_w, m/s, and its Lagrangian time, s); its decay time (0 none), its tint, whether it's drawn
/// (the drawing rule, decision 3: 1 or 0), and its drift along the wind against the air's frame, m/s; its stretch's axis
/// in the air's frame and the stretch, m² (Z5b).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SmokeRecord
{
    public float X, Y, Z, Born;
    public float Z0, Rise, RiseS, A0;
    public float S0h2, S0z2, SigmaW, LagrangianW;
    public float LifeS, Tint, Shown, ShearMs;
    public float AxisX, AxisY, AxisZ, Along2;
    public const int SizeInBytes = 80;
}

/// <summary>The smoke field copied for the drawing (docs/battle/smoke.md Z5), handed from the sim's thread to the
/// render thread through a triple buffer every <see cref="SimSettings.SmokeCopyMs"/> of real time (the drawing's, never
/// world state). A puff's state is closed form in its age and the air's frame is a rotation carried on at a steady rate,
/// so the drawing works each one out at its own frame's time: a copy a quarter of a second old draws exactly, only a
/// puff laid since showing late.</summary>
public sealed class SmokeCopy
{
    /// <summary>Counts up with each copy made (the renderer uploads when it changes).</summary>
    public int Version;
    /// <summary>The puffs copied (<see cref="Records"/>' first so many).</summary>
    public int Count;
    /// <summary>The world time the records' births are counted from, ms.</summary>
    public long CopiedMs;
    /// <summary>The air's frame as of <see cref="FrameMs"/>, carried on by <see cref="DriftMs"/> toward
    /// <see cref="WindToDeg"/> at <see cref="FrameAt"/> (<see cref="AirFrame.Advance"/>).</summary>
    public AirFrame Frame;
    public long FrameMs;
    public Vec3d FrameAt;
    public double WindToDeg, DriftMs;
    /// <summary>The records' origin in the air's frame (Earth-centred, at the sea), m.</summary>
    public Vec3d Anchor;
    /// <summary>The turbulence across: σ_v, m/s, and its Lagrangian time, s.</summary>
    public double SigmaV, LagrangianH;
    /// <summary>The least and the greatest σ_h² of the puffs at the copy's time, m² (the drawing's levels in use; a puff
    /// only grows, the greatest by at most 2 σ_v² T_L a second after).</summary>
    public double MinH2, MaxH2;
    public SmokeRecord[] Records = new SmokeRecord[1024];

    /// <summary>The air's frame at a world time, ms: the copy's carried on (or back) to it.</summary>
    public AirFrame FrameAtMs(long ms)
    {
        var f = Frame;
        f.Advance(FrameAt, WindToDeg, DriftMs, (ms - FrameMs) / 1000.0);
        return f;
    }

    public void Ensure(int n)
    {
        if (Records.Length < n)
            Array.Resize(ref Records, Math.Max(n, Records.Length * 2));
    }
}
