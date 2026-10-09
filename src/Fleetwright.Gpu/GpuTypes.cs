using System.Numerics;
using SDL;

namespace Fleetwright.Gpu;

/// <summary>
/// Wrapper for SDL_GPUBuffer with metadata.
/// </summary>
public unsafe struct GpuBuffer
{
    public SDL_GPUBuffer* Buffer;
    public nuint Size;

    public readonly bool IsValid => Buffer != null;
}

/// <summary>
/// Wrapper for SDL_GPUTexture with dimensions.
/// </summary>
public unsafe struct GpuTexture
{
    public SDL_GPUTexture* Texture;
    public uint Width;
    public uint Height;
    public uint Depth;
    public SDL_GPUTextureFormat Format;

    public readonly bool IsValid => Texture != null;
}

/// <summary>
/// Wrapper for SDL_GPUSampler.
/// </summary>
public unsafe struct GpuSampler
{
    public SDL_GPUSampler* Sampler;

    public readonly bool IsValid => Sampler != null;
}

/// <summary>
/// Combined index and vertex buffers for a mesh.
/// </summary>
public struct GpuMeshBuffers
{
    public GpuBuffer IndexBuffer;
    public GpuBuffer VertexBuffer;
    public uint IndexCount;
    public uint VertexCount;
    public SDL_GPUIndexElementSize IndexElementSize;

    public readonly bool IsValid => IndexBuffer.IsValid && VertexBuffer.IsValid;
}

/// <summary>
/// Wraps a graphics pipeline for material rendering.
/// </summary>
public unsafe struct MaterialPipeline
{
    public SDL_GPUGraphicsPipeline* Pipeline;

    public readonly bool IsValid => Pipeline != null;
}

/// <summary>
/// Instance of a material with textures and parameters.
/// </summary>
public struct MaterialInstance
{
    /// <summary>The pipeline to use for rendering.</summary>
    public MaterialPipeline Pipeline;

    /// <summary>Base color/albedo texture.</summary>
    public GpuTexture ColorTexture;

    /// <summary>Metallic-roughness texture (G=roughness, B=metallic).</summary>
    public GpuTexture MetalRoughTexture;

    /// <summary>Sampler for texture sampling.</summary>
    public GpuSampler Sampler;

    /// <summary>Which render pass this material belongs to.</summary>
    public MaterialPass PassType;

    /// <summary>Color multiplier factors.</summary>
    public Vector4 ColorFactors;

    /// <summary>
    /// Creates a default material instance.
    /// </summary>
    public static MaterialInstance CreateDefault()
    {
        return new MaterialInstance
        {
            PassType = MaterialPass.MainColor,
            ColorFactors = Vector4.One
        };
    }
}

/// <summary>
/// Identifies which render pass a material should be drawn in.
/// </summary>
public enum MaterialPass
{
    /// <summary>Opaque geometry pass.</summary>
    MainColor,

    /// <summary>Transparent geometry pass.</summary>
    Transparent,

    /// <summary>Other specialized pass.</summary>
    Other
}

/// <summary>
/// Collects render objects for batched drawing.
/// </summary>
public class DrawContext
{
    /// <summary>
    /// Opaque surfaces to render (front-to-back for best performance).
    /// </summary>
    public List<RenderObject> OpaqueSurfaces { get; } = new();

    /// <summary>
    /// Transparent surfaces to render (back-to-front for correct blending).
    /// </summary>
    public List<RenderObject> TransparentSurfaces { get; } = new();

    /// <summary>
    /// Clears all collected render objects.
    /// </summary>
    public void Clear()
    {
        OpaqueSurfaces.Clear();
        TransparentSurfaces.Clear();
    }
}

/// <summary>
/// Represents a single renderable surface.
/// </summary>
public struct RenderObject
{
    /// <summary>Number of indices to draw.</summary>
    public uint IndexCount;

    /// <summary>First index in the index buffer.</summary>
    public uint FirstIndex;

    /// <summary>Index buffer containing triangle indices.</summary>
    public GpuBuffer IndexBuffer;

    /// <summary>Vertex buffer containing vertex data.</summary>
    public GpuBuffer VertexBuffer;

    /// <summary>Material instance for this surface.</summary>
    public MaterialInstance Material;

    /// <summary>Bounding volume for culling.</summary>
    public Bounds Bounds;

    /// <summary>World transform matrix.</summary>
    public Matrix4x4 Transform;
}

/// <summary>
/// Axis-aligned bounding sphere for culling.
/// </summary>
public struct Bounds
{
    /// <summary>Center of the bounding sphere in local space.</summary>
    public Vector3 Origin;

    /// <summary>Radius of the bounding sphere.</summary>
    public float SphereRadius;

    /// <summary>
    /// Creates bounds from a collection of vertices.
    /// </summary>
    public static Bounds FromVertices(ReadOnlySpan<Vertex> vertices)
    {
        if (vertices.Length == 0)
            return new Bounds { Origin = Vector3.Zero, SphereRadius = 0 };

        // Calculate center (average of all positions)
        Vector3 min = new Vector3(float.MaxValue);
        Vector3 max = new Vector3(float.MinValue);

        foreach (ref readonly Vertex v in vertices)
        {
            min = Vector3.Min(min, v.Position);
            max = Vector3.Max(max, v.Position);
        }

        Vector3 origin = (min + max) * 0.5f;

        // Calculate radius (max distance from center)
        float maxDistSq = 0;
        foreach (ref readonly Vertex v in vertices)
        {
            float distSq = Vector3.DistanceSquared(origin, v.Position);
            maxDistSq = MathF.Max(maxDistSq, distSq);
        }

        return new Bounds
        {
            Origin = origin,
            SphereRadius = MathF.Sqrt(maxDistSq)
        };
    }
}

/// <summary>
/// Interface for objects that can be rendered.
/// </summary>
public interface IRenderable
{
    /// <summary>
    /// Collects render commands into the draw context.
    /// </summary>
    /// <param name="parentTransform">Parent's world transform matrix.</param>
    /// <param name="context">Draw context to add render objects to.</param>
    void Draw(Matrix4x4 parentTransform, DrawContext context);
}

/// <summary>
/// Base class for scene graph nodes with hierarchical transforms.
/// </summary>
public class SceneNode : IRenderable
{
    /// <summary>Weak reference to parent node to avoid circular references.</summary>
    public WeakReference<SceneNode>? Parent;

    /// <summary>Child nodes.</summary>
    public List<SceneNode> Children { get; } = new();

    /// <summary>Local transform relative to parent.</summary>
    public Matrix4x4 LocalTransform = Matrix4x4.Identity;

    /// <summary>Cached world transform (computed during refresh).</summary>
    public Matrix4x4 WorldTransform = Matrix4x4.Identity;

    /// <summary>Optional name for debugging.</summary>
    public string? Name;

    /// <summary>
    /// Refreshes world transforms for this node and all children.
    /// </summary>
    /// <param name="parentMatrix">Parent's world transform.</param>
    public void RefreshTransform(Matrix4x4 parentMatrix)
    {
        // Match SharpGLTF convention: parent * local
        WorldTransform = parentMatrix * LocalTransform;

        foreach (var child in Children)
        {
            child.RefreshTransform(WorldTransform);
        }
    }

    /// <summary>
    /// Adds a child node.
    /// </summary>
    public void AddChild(SceneNode child)
    {
        child.Parent = new WeakReference<SceneNode>(this);
        Children.Add(child);
    }

    /// <summary>
    /// Removes a child node.
    /// </summary>
    public bool RemoveChild(SceneNode child)
    {
        if (Children.Remove(child))
        {
            child.Parent = null;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Collects render commands for this node and children.
    /// Override in derived classes to add actual geometry.
    /// </summary>
    public virtual void Draw(Matrix4x4 parentTransform, DrawContext context)
    {
        // Match SharpGLTF convention: parent * local
        Matrix4x4 worldTransform = parentTransform * LocalTransform;

        // Draw children
        foreach (var child in Children)
        {
            child.Draw(worldTransform, context);
        }
    }

    /// <summary>
    /// Sets local transform from position, rotation (quaternion), and scale.
    /// </summary>
    public void SetTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        // Match GLM/GLTF convention: Translation * Rotation * Scale
        LocalTransform = Matrix4x4.CreateTranslation(position) *
                         Matrix4x4.CreateFromQuaternion(rotation) *
                         Matrix4x4.CreateScale(scale);
    }

    /// <summary>
    /// Sets local transform from position only.
    /// </summary>
    public void SetPosition(Vector3 position)
    {
        LocalTransform = Matrix4x4.CreateTranslation(position);
    }
}

/// <summary>
/// Scene node that renders a mesh asset.
/// </summary>
public class MeshNode : SceneNode
{
    /// <summary>The mesh asset to render.</summary>
    public MeshAsset? Mesh;

    /// <summary>
    /// Collects render commands for this mesh and children.
    /// </summary>
    public override void Draw(Matrix4x4 parentTransform, DrawContext context)
    {
        // Match SharpGLTF convention: parent * local
        Matrix4x4 worldTransform = parentTransform * LocalTransform;

        // Draw mesh surfaces
        if (Mesh != null && Mesh.MeshBuffers.IsValid)
        {
            foreach (var surface in Mesh.Surfaces)
            {
                RenderObject renderObj = new RenderObject
                {
                    IndexCount = surface.Count,
                    FirstIndex = surface.StartIndex,
                    IndexBuffer = Mesh.MeshBuffers.IndexBuffer,
                    VertexBuffer = Mesh.MeshBuffers.VertexBuffer,
                    Material = surface.Material,
                    Bounds = surface.Bounds,
                    Transform = worldTransform
                };

                if (surface.Material.PassType == MaterialPass.Transparent)
                {
                    context.TransparentSurfaces.Add(renderObj);
                }
                else
                {
                    context.OpaqueSurfaces.Add(renderObj);
                }
            }
        }

        // Draw children
        foreach (var child in Children)
        {
            child.Draw(worldTransform, context);
        }
    }
}

/// <summary>
/// GPU mesh data with multiple surfaces.
/// </summary>
public class MeshAsset
{
    /// <summary>Name of the mesh (for debugging).</summary>
    public string Name = "";

    /// <summary>Combined vertex and index buffers.</summary>
    public GpuMeshBuffers MeshBuffers;

    /// <summary>Surfaces (sub-meshes) with different materials.</summary>
    public List<GeoSurface> Surfaces { get; } = new();
}

/// <summary>
/// A surface (sub-mesh) within a mesh asset.
/// </summary>
public struct GeoSurface
{
    /// <summary>Starting index in the index buffer.</summary>
    public uint StartIndex;

    /// <summary>Number of indices to draw.</summary>
    public uint Count;

    /// <summary>Bounding volume for this surface.</summary>
    public Bounds Bounds;

    /// <summary>Material instance for rendering.</summary>
    public MaterialInstance Material;
}
