using System.Numerics;
using SDL;
using Fleetwright.Shared;
using SharpGLTF.Schema2;
using TracyWrapper;
using static SDL.SDL3;
using static SDL.SDL3_image;

namespace Fleetwright;

/// <summary>
/// Loaded GLTF scene with all resources.
/// </summary>
public class LoadedGltf : IRenderable, IDisposable
{
    /// <summary>Loaded mesh assets by name.</summary>
    public Dictionary<string, MeshAsset> Meshes { get; } = new();

    /// <summary>Scene nodes by name.</summary>
    public Dictionary<string, SceneNode> Nodes { get; } = new();

    /// <summary>Loaded textures by index.</summary>
    public Dictionary<int, GpuTexture> Images { get; } = new();

    /// <summary>Material instances by index.</summary>
    public Dictionary<int, MaterialInstance> Materials { get; } = new();

    /// <summary>Top-level nodes in the scene.</summary>
    public List<SceneNode> TopNodes { get; } = new();

    /// <summary>Samplers created for this scene.</summary>
    public List<GpuSampler> Samplers { get; } = new();

    private Sdl3GpuEngine? _engine;

    internal void SetEngine(Sdl3GpuEngine engine) => _engine = engine;

    /// <summary>
    /// Draws all top-level nodes.
    /// </summary>
    public void Draw(Matrix4x4 parentTransform, DrawContext context)
    {
        foreach (var node in TopNodes)
        {
            node.Draw(parentTransform, context);
        }
    }

    /// <summary>
    /// Disposes of all GPU resources.
    /// </summary>
    public void Dispose()
    {
        if (_engine == null) return;

        // Dispose meshes
        foreach (var mesh in Meshes.Values)
        {
            if (mesh.MeshBuffers.IsValid)
            {
                _engine.DestroyBuffer(mesh.MeshBuffers.VertexBuffer);
                _engine.DestroyBuffer(mesh.MeshBuffers.IndexBuffer);
            }
        }

        // Dispose textures
        foreach (var texture in Images.Values)
        {
            if (texture.IsValid)
            {
                _engine.DestroyTexture(texture);
            }
        }

        // Dispose samplers
        foreach (var sampler in Samplers)
        {
            if (sampler.IsValid)
            {
                _engine.DestroySampler(sampler);
            }
        }

        Meshes.Clear();
        Nodes.Clear();
        Images.Clear();
        Materials.Clear();
        TopNodes.Clear();
        Samplers.Clear();
    }
}

/// <summary>
/// GLTF/GLB file loader using SharpGLTF.
/// </summary>
public static unsafe class GltfLoader
{
    /// <summary>
    /// Loads a GLTF or GLB file.
    /// </summary>
    public static LoadedGltf? Load(Sdl3GpuEngine engine, string path)
    {
        using var _ = new ProfileScope("GLTF.Load", ZoneC.YELLOW);

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"GLTF file not found: {path}");
            return null;
        }

        try
        {
            var model = ModelRoot.Load(path);
            var result = new LoadedGltf();
            result.SetEngine(engine);

            string directory = Path.GetDirectoryName(path) ?? "";

            // Load images/textures
            using (new ProfileScope("GLTF.LoadImages", ZoneC.YELLOW))
                LoadImages(engine, model, result, directory);

            // Load samplers
            using (new ProfileScope("GLTF.LoadSamplers", ZoneC.YELLOW))
                LoadSamplers(engine, model, result);

            // Load materials
            using (new ProfileScope("GLTF.LoadMaterials", ZoneC.YELLOW))
                LoadMaterials(engine, model, result);

            // Load meshes
            using (new ProfileScope("GLTF.LoadMeshes", ZoneC.YELLOW))
                LoadMeshes(engine, model, result);

            // Load scene nodes
            using (new ProfileScope("GLTF.LoadNodes", ZoneC.YELLOW))
                LoadNodes(engine, model, result);

            Console.WriteLine($"Loaded GLTF: {path}");
            Console.WriteLine($"  Meshes: {result.Meshes.Count}");
            Console.WriteLine($"  Textures: {result.Images.Count}");
            Console.WriteLine($"  Materials: {result.Materials.Count}");
            Console.WriteLine($"  Nodes: {result.Nodes.Count}");

            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load GLTF {path}: {ex.Message}");
            return null;
        }
    }

    private static void LoadImages(Sdl3GpuEngine engine, ModelRoot model, LoadedGltf result, string directory)
    {
        for (int i = 0; i < model.LogicalImages.Count; i++)
        {
            var image = model.LogicalImages[i];
            try
            {
                ReadOnlySpan<byte> encoded = image.Content.Content.Span;
                SDL_Surface* loaded;
                fixed (byte* encodedPtr = encoded)
                {
                    SDL_IOStream* io = SDL_IOFromConstMem((IntPtr)encodedPtr, (nuint)encoded.Length);
                    loaded = IMG_Load_IO(io, true);
                }
                if (loaded == null)
                    throw new InvalidOperationException(SDL_GetError());

                // Normalize to RGBA8 byte order to match R8G8B8A8_UNORM (ABGR8888 == RGBA32 on little-endian)
                SDL_Surface* rgba = SDL_ConvertSurface(loaded, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888);
                SDL_DestroySurface(loaded);
                if (rgba == null)
                    throw new InvalidOperationException(SDL_GetError());

                uint width = (uint)rgba->w;
                uint height = (uint)rgba->h;

                // Create texture
                var texture = engine.CreateTexture(
                    SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
                    width, height,
                    SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER
                );

                // Upload pixel data (copy rows in case the surface pitch has padding)
                byte[] pixels = new byte[width * height * 4];
                int rowBytes = (int)width * 4;
                for (int y = 0; y < height; y++)
                {
                    new ReadOnlySpan<byte>((byte*)rgba->pixels + y * rgba->pitch, rowBytes)
                        .CopyTo(pixels.AsSpan(y * rowBytes, rowBytes));
                }
                SDL_DestroySurface(rgba);

                fixed (byte* pixelPtr = pixels)
                {
                    engine.UploadToTexture(texture, pixelPtr, width, height);
                }

                result.Images[i] = texture;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load image {i}: {ex.Message}");
                result.Images[i] = engine.ErrorCheckerboardTexture;
            }
        }
    }

    private static void LoadSamplers(Sdl3GpuEngine engine, ModelRoot model, LoadedGltf result)
    {
        // Samplers in GLTF are accessed through textures
        // Collect unique samplers from all textures
        HashSet<int> processedSamplers = new();

        foreach (var texture in model.LogicalTextures)
        {
            var sampler = texture.Sampler;
            if (sampler != null && !processedSamplers.Contains(sampler.LogicalIndex))
            {
                processedSamplers.Add(sampler.LogicalIndex);

                SDL_GPUSamplerCreateInfo createInfo = new SDL_GPUSamplerCreateInfo
                {
                    min_filter = ConvertMipMapFilter(sampler.MinFilter),
                    mag_filter = ConvertFilter(sampler.MagFilter),
                    mipmap_mode = ConvertMipmapMode(sampler.MinFilter),
                    address_mode_u = ConvertWrap(sampler.WrapS),
                    address_mode_v = ConvertWrap(sampler.WrapT),
                    address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT,
                    max_anisotropy = 16.0f,
                    enable_anisotropy = true
                };

                // Ensure we have enough slots for the sampler index
                while (result.Samplers.Count <= sampler.LogicalIndex)
                {
                    result.Samplers.Add(engine.DefaultSamplerLinear);
                }
                result.Samplers[sampler.LogicalIndex] = engine.CreateSampler(createInfo);
            }
        }

        // Add a default sampler if none exist
        if (result.Samplers.Count == 0)
        {
            result.Samplers.Add(engine.DefaultSamplerLinear);
        }
    }

    private static void LoadMaterials(Sdl3GpuEngine engine, ModelRoot model, LoadedGltf result)
    {
        for (int i = 0; i < model.LogicalMaterials.Count; i++)
        {
            var material = model.LogicalMaterials[i];
            var pbr = material.FindChannel("BaseColor");
            var metalRough = material.FindChannel("MetallicRoughness");

            GpuTexture colorTexture = engine.WhiteTexture;
            GpuTexture metalRoughTexture = engine.WhiteTexture;
            GpuSampler sampler = engine.DefaultSamplerLinear;
            Vector4 colorFactors = Vector4.One;

            // Base color texture
            if (pbr != null)
            {
                // Try to get the RGBA color factor
                try
                {
                    var color = pbr.Value.Color;
                    colorFactors = new Vector4(color.X, color.Y, color.Z, color.W);
                }
                catch
                {
                    colorFactors = Vector4.One;
                }

                var tex = pbr.Value.Texture;
                if (tex != null)
                {
                    int imageIndex = tex.PrimaryImage.LogicalIndex;
                    if (result.Images.TryGetValue(imageIndex, out var loadedTex))
                    {
                        colorTexture = loadedTex;
                    }

                    // Get sampler
                    if (tex.Sampler != null && tex.Sampler.LogicalIndex < result.Samplers.Count)
                    {
                        sampler = result.Samplers[tex.Sampler.LogicalIndex];
                    }
                }
            }

            // Metallic-roughness texture
            if (metalRough != null)
            {
                var tex = metalRough.Value.Texture;
                if (tex != null)
                {
                    int imageIndex = tex.PrimaryImage.LogicalIndex;
                    if (result.Images.TryGetValue(imageIndex, out var loadedTex))
                    {
                        metalRoughTexture = loadedTex;
                    }
                }
            }

            // Determine pass type
            MaterialPass pass = MaterialPass.MainColor;
            if (material.Alpha == AlphaMode.BLEND)
            {
                pass = MaterialPass.Transparent;
            }

            result.Materials[i] = engine.PbrMaterial.CreateInstance(
                pass,
                colorTexture,
                metalRoughTexture,
                sampler,
                colorFactors
            );
        }
    }

    private static void LoadMeshes(Sdl3GpuEngine engine, ModelRoot model, LoadedGltf result)
    {
        foreach (var mesh in model.LogicalMeshes)
        {
            var meshAsset = new MeshAsset { Name = mesh.Name ?? $"Mesh_{mesh.LogicalIndex}" };

            List<Vertex> allVertices = new();
            List<uint> allIndices = new();

            foreach (var primitive in mesh.Primitives)
            {
                uint startIndex = (uint)allIndices.Count;
                uint baseVertex = (uint)allVertices.Count;

                // Get accessors
                var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
                var normals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
                var uvs = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                var colors = primitive.GetVertexAccessor("COLOR_0")?.AsVector4Array();

                if (positions == null)
                {
                    Console.Error.WriteLine($"Mesh {mesh.Name} primitive has no positions");
                    continue;
                }

                // Build vertices
                List<Vertex> vertices = new();
                for (int i = 0; i < positions.Count; i++)
                {
                    Vector3 pos = positions[i];
                    Vector3 normal = normals != null && i < normals.Count ? normals[i] : Vector3.UnitY;
                    Vector2 uv = uvs != null && i < uvs.Count ? uvs[i] : Vector2.Zero;
                    Vector4 color = colors != null && i < colors.Count ? colors[i] : Vector4.One;

                    vertices.Add(new Vertex(pos, normal, uv, color));
                }

                // Get indices
                var indexAccessor = primitive.GetIndices();
                List<uint> indices = new();
                if (indexAccessor != null)
                {
                    foreach (var idx in indexAccessor)
                    {
                        indices.Add((uint)idx + baseVertex);
                    }
                }
                else
                {
                    // Non-indexed primitive - generate sequential indices
                    for (int i = 0; i < positions.Count; i++)
                    {
                        indices.Add((uint)(baseVertex + i));
                    }
                }

                // Create surface
                GeoSurface surface = new GeoSurface
                {
                    StartIndex = startIndex,
                    Count = (uint)indices.Count,
                    Bounds = Bounds.FromVertices(vertices.ToArray())
                };

                // Assign material
                if (primitive.Material != null && result.Materials.TryGetValue(primitive.Material.LogicalIndex, out var mat))
                {
                    surface.Material = mat;
                }
                else
                {
                    surface.Material = engine.PbrMaterial.CreateDefaultOpaque(engine);
                }

                meshAsset.Surfaces.Add(surface);
                allVertices.AddRange(vertices);
                allIndices.AddRange(indices);
            }

            // Upload to GPU
            if (allVertices.Count > 0 && allIndices.Count > 0)
            {
                meshAsset.MeshBuffers = engine.UploadMesh(allVertices.ToArray(), allIndices.ToArray());
                result.Meshes[meshAsset.Name] = meshAsset;
            }
        }
    }

    private static void LoadNodes(Sdl3GpuEngine engine, ModelRoot model, LoadedGltf result)
    {
        // First pass: create all nodes
        foreach (var node in model.LogicalNodes)
        {
            SceneNode sceneNode;

            if (node.Mesh != null && result.Meshes.TryGetValue(node.Mesh.Name ?? $"Mesh_{node.Mesh.LogicalIndex}", out var meshAsset))
            {
                sceneNode = new MeshNode
                {
                    Mesh = meshAsset,
                    Name = node.Name ?? $"Node_{node.LogicalIndex}"
                };
            }
            else
            {
                sceneNode = new SceneNode
                {
                    Name = node.Name ?? $"Node_{node.LogicalIndex}"
                };
            }

            // Use SharpGLTF's LocalMatrix directly - it's already a System.Numerics.Matrix4x4
            // that represents the node's local transform
            sceneNode.LocalTransform = node.LocalMatrix;

            result.Nodes[sceneNode.Name] = sceneNode;
        }

        // Second pass: establish hierarchy
        foreach (var node in model.LogicalNodes)
        {
            string nodeName = node.Name ?? $"Node_{node.LogicalIndex}";
            if (!result.Nodes.TryGetValue(nodeName, out var sceneNode))
                continue;

            foreach (var child in node.VisualChildren)
            {
                string childName = child.Name ?? $"Node_{child.LogicalIndex}";
                if (result.Nodes.TryGetValue(childName, out var childNode))
                {
                    sceneNode.AddChild(childNode);
                }
            }
        }

        // Find top-level nodes (nodes with no parent in the GLTF hierarchy)
        foreach (var node in model.LogicalNodes)
        {
            if (node.VisualParent == null)
            {
                string nodeName = node.Name ?? $"Node_{node.LogicalIndex}";
                if (result.Nodes.TryGetValue(nodeName, out var sceneNode))
                {
                    result.TopNodes.Add(sceneNode);
                }
            }
        }
    }

    private static SDL_GPUFilter ConvertFilter(SharpGLTF.Schema2.TextureInterpolationFilter? filter)
    {
        return filter switch
        {
            TextureInterpolationFilter.NEAREST => SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            TextureInterpolationFilter.LINEAR => SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            _ => SDL_GPUFilter.SDL_GPU_FILTER_LINEAR
        };
    }

    private static SDL_GPUFilter ConvertMipMapFilter(SharpGLTF.Schema2.TextureMipMapFilter? filter)
    {
        return filter switch
        {
            TextureMipMapFilter.NEAREST => SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            TextureMipMapFilter.LINEAR => SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            TextureMipMapFilter.NEAREST_MIPMAP_NEAREST => SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            TextureMipMapFilter.LINEAR_MIPMAP_NEAREST => SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            TextureMipMapFilter.NEAREST_MIPMAP_LINEAR => SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            TextureMipMapFilter.LINEAR_MIPMAP_LINEAR => SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            _ => SDL_GPUFilter.SDL_GPU_FILTER_LINEAR
        };
    }

    private static SDL_GPUSamplerMipmapMode ConvertMipmapMode(SharpGLTF.Schema2.TextureMipMapFilter? filter)
    {
        return filter switch
        {
            TextureMipMapFilter.NEAREST => SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            TextureMipMapFilter.LINEAR => SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR,
            TextureMipMapFilter.NEAREST_MIPMAP_NEAREST => SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            TextureMipMapFilter.LINEAR_MIPMAP_NEAREST => SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            TextureMipMapFilter.NEAREST_MIPMAP_LINEAR => SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR,
            TextureMipMapFilter.LINEAR_MIPMAP_LINEAR => SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR,
            _ => SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR
        };
    }

    private static SDL_GPUSamplerAddressMode ConvertWrap(SharpGLTF.Schema2.TextureWrapMode wrap)
    {
        return wrap switch
        {
            TextureWrapMode.CLAMP_TO_EDGE => SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            TextureWrapMode.MIRRORED_REPEAT => SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_MIRRORED_REPEAT,
            TextureWrapMode.REPEAT => SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT,
            _ => SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_REPEAT
        };
    }
}
