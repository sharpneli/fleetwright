using System.Numerics;

namespace Fleetwright.HitView;

/// <summary>
/// An orbit camera around a point of the ship, in ship-local metres (x toward the bow, y to starboard, z up). The
/// basis is built by hand, as hitview.py did: that frame is left-handed, and Matrix4x4.CreateLookAt would mirror it.
/// Orthographic by default (the ship's proportions stay true); perspective at the same framing.
/// </summary>
public struct HitboxCamera
{
    /// <summary>What the camera looks at.</summary>
    public Vector3 Target;

    /// <summary>Where the camera is, clockwise from dead ahead, degrees (90: off the starboard side).</summary>
    public float Bearing;

    /// <summary>The camera's height above the horizontal, degrees.</summary>
    public float Elevation;

    /// <summary>Half the view's height in metres at the target (zoom).</summary>
    public float HalfHeight;

    public bool Perspective;
    public const float FovY = 40f;

    /// <summary>Aims at the points and zooms so they fill the view from the current direction, with a margin.</summary>
    public void Fit(ReadOnlySpan<HitVertex> points, float aspect)
    {
        if (points.IsEmpty)
            return;
        var (r, s, f) = Basis();
        float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue, d = 0;
        foreach (ref readonly var p in points)
        {
            float u = Vector3.Dot(p.Position, r), v = Vector3.Dot(p.Position, s);
            (u0, u1, v0, v1) = (Math.Min(u0, u), Math.Max(u1, u), Math.Min(v0, v), Math.Max(v1, v));
            d += Vector3.Dot(p.Position, f);
        }
        Target = r * ((u0 + u1) / 2) + s * ((v0 + v1) / 2) + f * (d / points.Length);
        HalfHeight = 0.53f * Math.Max(v1 - v0, (u1 - u0) / Math.Max(aspect, 0.1f)) + 1;
    }

    /// <summary>Toward the camera from the target (unit).</summary>
    public readonly Vector3 Back
    {
        get
        {
            float a = Bearing * MathF.PI / 180, e = Elevation * MathF.PI / 180;
            return new(MathF.Cos(e) * MathF.Cos(a), MathF.Cos(e) * MathF.Sin(a), MathF.Sin(e));
        }
    }

    /// <summary>The screen's right, up and forward (into the screen) in ship space.</summary>
    public readonly (Vector3 Right, Vector3 Up, Vector3 Forward) Basis()
    {
        var f = -Back;
        float a = Bearing * MathF.PI / 180;
        var r = new Vector3(MathF.Sin(a), -MathF.Cos(a), 0);
        return (r, Vector3.Cross(f, r), f);
    }

    /// <summary>How far the perspective eye stands back from the target, for HalfHeight at the target.</summary>
    public readonly float EyeDistance => HalfHeight / MathF.Tan(FovY * MathF.PI / 360);

    /// <summary>The view-projection matrix (row vectors, as System.Numerics; GLSL reads it as column-major, so
    /// <c>viewProj * p</c> there). <paramref name="centre"/> and <paramref name="radius"/> bound the scene, for the
    /// depth range.</summary>
    public readonly Matrix4x4 ViewProj(float aspect, Vector3 centre, float radius)
    {
        var (r, s, f) = Basis();
        if (!Perspective)
        {
            float sx = 1 / (HalfHeight * aspect), sy = 1 / HalfHeight;
            float dn = Vector3.Dot(centre - Target, f) - radius, sz = 1 / (2 * radius);
            return Columns(r * sx, -Vector3.Dot(Target, r) * sx, s * sy, -Vector3.Dot(Target, s) * sy,
                f * sz, (-Vector3.Dot(Target, f) - dn) * sz, Vector3.Zero, 1);
        }
        var eye = Target - f * EyeDistance;
        float t = MathF.Tan(FovY * MathF.PI / 360);
        float far = Vector3.Dot(centre - eye, f) + radius, near = Math.Max(0.5f, far - 2 * radius - 1);
        float a = far / (far - near), b = -near * far / (far - near);
        // clip = (u / (t aspect), v / t, a d + b, d) with u, v, d the eye-space coordinates
        return Columns(r / (t * aspect), -Vector3.Dot(eye, r) / (t * aspect), s / t, -Vector3.Dot(eye, s) / t,
            f * a, -Vector3.Dot(eye, f) * a + b, f, -Vector3.Dot(eye, f));
    }

    static Matrix4x4 Columns(Vector3 c0, float w0, Vector3 c1, float w1, Vector3 c2, float w2, Vector3 c3, float w3) => new(
        c0.X, c1.X, c2.X, c3.X,
        c0.Y, c1.Y, c2.Y, c3.Y,
        c0.Z, c1.Z, c2.Z, c3.Z,
        w0, w1, w2, w3);

    /// <summary>The ray through a point of the view, <paramref name="ndc"/> in -1..1 with +y up.</summary>
    public readonly (Vector3 Origin, Vector3 Dir) Ray(Vector2 ndc, float aspect, float radius)
    {
        var (r, s, f) = Basis();
        if (!Perspective)
            return (Target + r * (ndc.X * HalfHeight * aspect) + s * (ndc.Y * HalfHeight) - f * (4 * radius), f);
        float t = MathF.Tan(FovY * MathF.PI / 360);
        return (Target - f * EyeDistance, Vector3.Normalize(f + r * (ndc.X * t * aspect) + s * (ndc.Y * t)));
    }
}

/// <summary>What a hitbox view shows, besides where from: which kinds, the clip box, the hovered and selected prism.</summary>
public struct HitboxViewState
{
    /// <summary>Bit k: kind k of HitKinds.All is drawn.</summary>
    public ulong KindMask;

    public bool Clip;
    public Vector3 ClipMin, ClipMax;

    /// <summary>Clip per pixel (a cross-section; cut prisms are open). Otherwise a prism is drawn whole when it
    /// overlaps the clip box at all (a tier's rooms with their floors and ceilings).</summary>
    public bool ClipCut;

    /// <summary>Whether the clip box leaves a prism with these bounds (all of it, or the part inside when cutting).</summary>
    public readonly bool Keeps(Vector3 min, Vector3 max) =>
        !Clip || (min.X <= ClipMax.X && min.Y <= ClipMax.Y && min.Z <= ClipMax.Z && max.X >= ClipMin.X && max.Y >= ClipMin.Y && max.Z >= ClipMin.Z);

    /// <summary>Prism indices into the mesh, or HitboxMesh.NoPrism.</summary>
    public uint Hover, Selected;

    public static HitboxViewState All => new() { KindMask = ulong.MaxValue, Hover = HitboxMesh.NoPrism, Selected = HitboxMesh.NoPrism };

    public readonly bool Shows(int kind) => (KindMask & (1UL << kind)) != 0;
}
