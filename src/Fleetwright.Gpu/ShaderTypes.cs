using System.Numerics;
using System.Runtime.InteropServices;

namespace Fleetwright.Gpu;

/// <summary>
/// Standard vertex format for mesh rendering.
/// Matches the layout expected by mesh shaders.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vector3 Position;
    public float UvX;
    public Vector3 Normal;
    public float UvY;
    public Vector4 Color;

    public Vertex(Vector3 position, Vector3 normal, Vector2 uv, Vector4 color)
    {
        Position = position;
        UvX = uv.X;
        Normal = normal;
        UvY = uv.Y;
        Color = color;
    }

    public Vertex(Vector3 position, Vector3 normal, Vector2 uv)
        : this(position, normal, uv, Vector4.One)
    {
    }

    public Vertex(Vector3 position)
        : this(position, Vector3.UnitY, Vector2.Zero, Vector4.One)
    {
    }

    public static int SizeInBytes => Marshal.SizeOf<Vertex>();

    public static void DebugPrintLayout()
    {
        Console.WriteLine($"Vertex struct layout:");
        Console.WriteLine($"  Total size: {SizeInBytes} bytes");
        Console.WriteLine($"  Position offset: {Marshal.OffsetOf<Vertex>(nameof(Position))}");
        Console.WriteLine($"  UvX offset: {Marshal.OffsetOf<Vertex>(nameof(UvX))}");
        Console.WriteLine($"  Normal offset: {Marshal.OffsetOf<Vertex>(nameof(Normal))}");
        Console.WriteLine($"  UvY offset: {Marshal.OffsetOf<Vertex>(nameof(UvY))}");
        Console.WriteLine($"  Color offset: {Marshal.OffsetOf<Vertex>(nameof(Color))}");
    }
}

/// <summary>
/// Push constant data for vertex shader.
/// Contains transform matrices and material parameters.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GpuVertexPushData
{
    public Matrix4x4 ViewProj;
    public Matrix4x4 ModelMatrix;
    public Vector4 ColorFactors;

    public static int SizeInBytes => Marshal.SizeOf<GpuVertexPushData>();
}

/// <summary>
/// Scene-wide data uploaded as a uniform buffer.
/// Contains view/projection matrices and lighting information.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GpuSceneData
{
    public Matrix4x4 View;
    public Matrix4x4 Proj;
    public Matrix4x4 ViewProj;
    public Vector4 AmbientColor;
    public Vector4 SunlightDirection;
    public Vector4 SunlightColor;

    public static int SizeInBytes => Marshal.SizeOf<GpuSceneData>();

    public static GpuSceneData CreateDefault()
    {
        return new GpuSceneData
        {
            View = Matrix4x4.Identity,
            Proj = Matrix4x4.Identity,
            ViewProj = Matrix4x4.Identity,
            AmbientColor = new Vector4(0.1f, 0.1f, 0.1f, 1.0f),
            SunlightDirection = Vector4.Normalize(new Vector4(1.0f, 1.0f, 1.0f, 0.0f)),
            SunlightColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
        };
    }
}

/// <summary>
/// Material constants for PBR rendering.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GpuMaterialData
{
    public Vector4 ColorFactors;
    public float MetallicFactor;
    public float RoughnessFactor;
    public float Padding1;
    public float Padding2;

    public static int SizeInBytes => Marshal.SizeOf<GpuMaterialData>();

    public static GpuMaterialData CreateDefault()
    {
        return new GpuMaterialData
        {
            ColorFactors = Vector4.One,
            MetallicFactor = 0.0f,
            RoughnessFactor = 0.5f
        };
    }
}

/// <summary>
/// Compute shader push data for gradient/sky rendering.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GpuComputePushData
{
    public Vector4 Data1;
    public Vector4 Data2;
    public Vector4 Data3;
    public Vector4 Data4;

    public static int SizeInBytes => Marshal.SizeOf<GpuComputePushData>();
}
