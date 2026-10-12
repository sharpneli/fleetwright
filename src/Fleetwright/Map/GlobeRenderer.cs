using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.Sim.Core;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>Draws the globe of water and the map's lines into any <see cref="RenderTarget"/>: the sphere ray-cast per
/// pixel (day and night from the sun's direction), then the lines (<see cref="MapLines"/>), already projected in
/// double. Takes a <see cref="MapView"/> and the sun, plain values; knows nothing of input, ImGui or the world.
/// Pipelines are built for the target's format and sample count.</summary>
public sealed unsafe class GlobeRenderer : IDisposable
{
    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* globeVs, globeFs, lineVs, lineFs;
    readonly DynamicGpuBuffer lineBuf;
    SDL_GPUGraphicsPipeline* globePipe, linePipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;

    const uint LineBufferBytes = 4 << 20;

    struct GlobeParams
    {
        public Vector4 Right, Up, Fwd, EyeDir, Sun;
    }

    public GlobeRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        globeVs = GpuShader.Load(device, "Content/Shaders/Compiled/globe.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 0);
        globeFs = GpuShader.Load(device, "Content/Shaders/Compiled/globe.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 1);
        lineVs = GpuShader.Load(device, "Content/Shaders/Compiled/mapline.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 0);
        lineFs = GpuShader.Load(device, "Content/Shaders/Compiled/mapline.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 0);
        if (globeVs == null || globeFs == null || lineVs == null || lineFs == null)
            throw new InvalidOperationException("globe shaders failed to load");
        lineBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, LineBufferBytes);
    }

    void EnsurePipelines(RenderTarget target)
    {
        if (globePipe != null && pipeFormat == target.ColorFormat && pipeSamples == target.SampleCount)
            return;
        ReleasePipelines();
        (pipeFormat, pipeSamples) = (target.ColorFormat, target.SampleCount);
        GpuPipelineBuilder Base() => new GpuPipelineBuilder()
            .SetColorFormat(target.ColorFormat)
            .NoDepthTarget()
            .DisableDepthTest()
            .SetMultisampling(target.SampleCount)
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE);
        globePipe = Base().SetShaders(globeVs, globeFs).DisableBlending().Build(device);
        linePipe = Base().SetShaders(lineVs, lineFs).EnableBlendingAlpha()
            .AddVertexBuffer(0, LineVertex.SizeInBytes)
            .AddVertexAttribute(0, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 0)
            .AddVertexAttribute(1, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 8)
            .AddVertexAttribute(2, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4, 16)
            .Build(device);
        if (globePipe == null || linePipe == null)
            throw new InvalidOperationException($"globe pipelines: {SDL_GetError()}");
    }

    void ReleasePipelines()
    {
        if (globePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, globePipe);
        if (linePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, linePipe);
        globePipe = linePipe = null;
    }

    /// <summary>Clears <paramref name="target"/> and draws the globe seen through <paramref name="view"/>, lit from
    /// <paramref name="sun"/> (a unit vector, Earth-fixed), with <paramref name="lines"/> on top, in its own pass.</summary>
    public void Render(SDL_GPUCommandBuffer* cmd, RenderTarget target, in MapView view, Vec3d sun, MapLines lines)
    {
        EnsurePipelines(target);
        var verts = lines.Vertices;
        int maxVerts = (int)(LineBufferBytes / LineVertex.SizeInBytes);
        if (verts.Length > maxVerts)
            verts = verts[..(maxVerts - maxVerts % 6)];
        lineBuf.Upload(cmd, verts);   // a copy pass: before the render pass

        var p = Params(view, sun);
        var pass = target.BeginPass(cmd, new SDL_FColor { r = 0, g = 0, b = 0, a = 1 }, depth: false);
        SDL_BindGPUGraphicsPipeline(pass, globePipe);
        SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&p), (uint)sizeof(GlobeParams));
        SDL_DrawGPUPrimitives(pass, 3, 1, 0, 0);
        if (verts.Length > 0)
        {
            SDL_BindGPUGraphicsPipeline(pass, linePipe);
            var binding = new SDL_GPUBufferBinding { buffer = lineBuf.Buffer, offset = 0 };
            SDL_BindGPUVertexBuffers(pass, 0, &binding, 1);
            SDL_DrawGPUPrimitives(pass, (uint)verts.Length, 1, 0, 0);
        }
        SDL_EndGPURenderPass(pass);
    }

    /// <summary>The shader's camera, in Earth radii with the eye at the origin. |eye|² - 1 is formed from the altitude
    /// in double: the one large quantity, exact, so the ray-cast holds close to the sea.</summary>
    static GlobeParams Params(in MapView v, Vec3d sun)
    {
        double h = v.Altitude / Earth.Radius;
        static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);
        return new GlobeParams
        {
            Right = new Vector4(F(v.Right), (float)v.TanHalfX),
            Up = new Vector4(F(v.Up), (float)v.TanHalfY),
            Fwd = new Vector4(F(v.Forward), (float)(1 + h)),
            EyeDir = new Vector4(F(-v.Forward), (float)(h * (2 + h))),
            Sun = new Vector4(F(sun), 0),
        };
    }

    public void Dispose()
    {
        ReleasePipelines();
        lineBuf.Dispose();
        SDL_ReleaseGPUShader(device, globeVs);
        SDL_ReleaseGPUShader(device, globeFs);
        SDL_ReleaseGPUShader(device, lineVs);
        SDL_ReleaseGPUShader(device, lineFs);
    }
}
