using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.Sim.Core;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>Draws the globe and the map's lines into any <see cref="RenderTarget"/>. First the land's fill
/// (<see cref="LandView"/>'s fans, in NDC) is counted by winding into a coverage target of its own; then the sphere
/// is ray-cast per pixel, sea or land by that count, day and night from the sun's direction; then the lines
/// (<see cref="MapLines"/>), already projected in double. Takes a <see cref="MapView"/>, the sun and those buffers,
/// plain values; knows nothing of input, ImGui or the world. Pipelines are built for the target's format and sample
/// count.</summary>
public sealed unsafe class GlobeRenderer : IDisposable
{
    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* globeVs, globeFs, lineVs, lineFs, fillVs, fillFs;
    readonly DynamicGpuBuffer lineBuf, fillBuf;
    readonly SDL_GPUSampler* nearest;
    SDL_GPUGraphicsPipeline* globePipe, linePipe, fillPipe;
    RenderTarget? coverage;   // the land's winding number per pixel, at the target's size
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;

    const uint LineBufferBytes = 4 << 20, FillBufferBytes = 8 << 20;
    const SDL_GPUTextureFormat CoverageFormat = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R16_FLOAT;

    struct GlobeParams
    {
        public Vector4 Right, Up, Fwd, EyeDir, Sun;
    }

    public GlobeRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        globeVs = GpuShader.Load(device, "Content/Shaders/Compiled/globe.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 0);
        globeFs = GpuShader.Load(device, "Content/Shaders/Compiled/globe.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 1, 0, 0, 1);
        lineVs = GpuShader.Load(device, "Content/Shaders/Compiled/mapline.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 0);
        lineFs = GpuShader.Load(device, "Content/Shaders/Compiled/mapline.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 0);
        fillVs = GpuShader.Load(device, "Content/Shaders/Compiled/landfill.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 0);
        fillFs = GpuShader.Load(device, "Content/Shaders/Compiled/landfill.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 0);
        if (globeVs == null || globeFs == null || lineVs == null || lineFs == null || fillVs == null || fillFs == null)
            throw new InvalidOperationException("globe shaders failed to load");
        lineBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, LineBufferBytes);
        fillBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, FillBufferBytes);
        var si = new SDL_GPUSamplerCreateInfo
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
        };
        nearest = SDL_CreateGPUSampler(device, &si);
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
        // the winding count: one sample, additive, both faces
        fillPipe = new GpuPipelineBuilder()
            .SetColorFormat(CoverageFormat)
            .NoDepthTarget()
            .DisableDepthTest()
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE)
            .SetShaders(fillVs, fillFs)
            .EnableBlendingAdditive()
            .AddVertexBuffer(0, 8)
            .AddVertexAttribute(0, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 0)
            .Build(device);
        if (globePipe == null || linePipe == null || fillPipe == null)
            throw new InvalidOperationException($"globe pipelines: {SDL_GetError()}");
    }

    void ReleasePipelines()
    {
        if (globePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, globePipe);
        if (linePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, linePipe);
        if (fillPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, fillPipe);
        globePipe = linePipe = fillPipe = null;
    }

    /// <summary>Clears <paramref name="target"/> and draws the globe seen through <paramref name="view"/>, lit from
    /// <paramref name="sun"/> (a unit vector, Earth-fixed), its land from <paramref name="landFill"/> (triangles in NDC,
    /// counted by winding), with <paramref name="lines"/> on top.</summary>
    public void Render(SDL_GPUCommandBuffer* cmd, RenderTarget target, in MapView view, Vec3d sun,
        ReadOnlySpan<Vector2> landFill, MapLines lines)
    {
        EnsurePipelines(target);
        if (coverage == null)
            coverage = new RenderTarget(device, target.Width, target.Height, CoverageFormat, depth: false);
        else
            coverage.Resize(target.Width, target.Height);
        var verts = lines.Vertices;
        int maxVerts = (int)(LineBufferBytes / LineVertex.SizeInBytes);
        if (verts.Length > maxVerts)
            verts = verts[..(maxVerts - maxVerts % 6)];
        int maxFill = (int)(FillBufferBytes / 8);
        if (landFill.Length > maxFill)
            landFill = landFill[..(maxFill - maxFill % 3)];
        lineBuf.Upload(cmd, verts);   // copy passes: before the render passes
        fillBuf.Upload(cmd, landFill);

        // the land's winding count
        var cpass = coverage.BeginPass(cmd, new SDL_FColor { r = 0, g = 0, b = 0, a = 0 }, depth: false);
        if (landFill.Length > 0)
        {
            SDL_BindGPUGraphicsPipeline(cpass, fillPipe);
            var fb = new SDL_GPUBufferBinding { buffer = fillBuf.Buffer, offset = 0 };
            SDL_BindGPUVertexBuffers(cpass, 0, &fb, 1);
            SDL_DrawGPUPrimitives(cpass, (uint)landFill.Length, 1, 0, 0);
        }
        SDL_EndGPURenderPass(cpass);

        var p = Params(view, sun);
        var pass = target.BeginPass(cmd, new SDL_FColor { r = 0, g = 0, b = 0, a = 1 }, depth: false);
        SDL_BindGPUGraphicsPipeline(pass, globePipe);
        var cov = new SDL_GPUTextureSamplerBinding { texture = coverage.Resolve, sampler = nearest };
        SDL_BindGPUFragmentSamplers(pass, 0, &cov, 1);
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
        fillBuf.Dispose();
        coverage?.Dispose();
        SDL_ReleaseGPUSampler(device, nearest);
        SDL_ReleaseGPUShader(device, fillVs);
        SDL_ReleaseGPUShader(device, fillFs);
        SDL_ReleaseGPUShader(device, globeVs);
        SDL_ReleaseGPUShader(device, globeFs);
        SDL_ReleaseGPUShader(device, lineVs);
        SDL_ReleaseGPUShader(device, lineFs);
    }
}
