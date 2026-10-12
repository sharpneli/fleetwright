using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.Sim.Core;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>Draws the globe and the map's lines into any <see cref="RenderTarget"/>.
/// <list type="number">
/// <item>The land's winding count, into a coverage target of its own: a depth pre-pass of a sphere 2 km inside the
/// Earth, then the baked fans of a coarse level (<see cref="LandMesh"/>, static, depth-tested so the far side
/// doesn't count) or the finest level's fans from <see cref="LandView"/> (in NDC, already clipped to the view).</item>
/// <item>The sphere, ray-cast per pixel: sea or land by that count, day and night from the sun, and the masking
/// sphere's depth.</item>
/// <item>The baked coast (depth-tested the same way), then the lines built on the CPU (<see cref="MapLines"/>: the grid,
/// and the finest level's coast).</item>
/// </list>
/// Takes a <see cref="MapView"/>, the sun and those buffers, plain values; knows nothing of input, ImGui or the world.
/// Pipelines are built for the target's format, sample count and depth.</summary>
public sealed unsafe class GlobeRenderer : IDisposable
{
    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* globeVs, globeFs, lineVs, lineFs, fillVs, fillFs, land3dVs, coast3dVs, depthFs;
    readonly DynamicGpuBuffer lineBuf, fillBuf, overlayBuf;
    readonly SDL_GPUSampler* nearest;
    SDL_GPUGraphicsPipeline* globePipe, linePipe, fillPipe, depthPipe, fill3dPipe, coast3dPipe;
    RenderTarget? coverage;   // the land's winding number per pixel, at the target's size, with depth
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;

    // the baked levels: fill vertices, coast vertices and indices, and their counts
    readonly SDL_GPUBuffer*[] meshFill = new SDL_GPUBuffer*[2], meshCoast = new SDL_GPUBuffer*[2], meshIdx = new SDL_GPUBuffer*[2];
    readonly uint[] meshFillCount = new uint[2], meshIdxCount = new uint[2];

    const uint LineBufferBytes = 16 << 20, FillBufferBytes = 8 << 20, OverlayBufferBytes = 2 << 20;
    const SDL_GPUTextureFormat CoverageFormat = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R16_FLOAT;

    /// <summary>How far inside the Earth the masking sphere is: over the baked fans' sag (under 0.4 km), so they pass,
    /// and small, so the far side's land shows at most a pixel or two at the limb.</summary>
    const double MaskDepthMetres = 2000;

    struct GlobeParams
    {
        public Vector4 Right, Up, Fwd, EyeDir, Sun, Depth;
    }

    struct ViewParams
    {
        public Vector4 Eye, Right, Up, Fwd, Depth, Viewport, Color;
    }

    public GlobeRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        const SDL_GPUShaderStage V = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, F = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT;
        SDL_GPUShader* Load(string name, SDL_GPUShaderStage stage, uint samplers, uint uniforms) =>
            GpuShader.Load(device, $"Content/Shaders/Compiled/{name}.spv", stage, samplers, 0, 0, uniforms);
        globeVs = Load("globe.vert", V, 0, 0);
        globeFs = Load("globe.frag", F, 1, 1);
        lineVs = Load("mapline.vert", V, 0, 0);
        lineFs = Load("mapline.frag", F, 0, 0);
        fillVs = Load("landfill.vert", V, 0, 0);
        fillFs = Load("landfill.frag", F, 0, 0);
        land3dVs = Load("land3d.vert", V, 0, 1);
        coast3dVs = Load("coast3d.vert", V, 0, 1);
        depthFs = Load("spheredepth.frag", F, 0, 1);
        if (globeVs == null || globeFs == null || lineVs == null || lineFs == null || fillVs == null || fillFs == null
            || land3dVs == null || coast3dVs == null || depthFs == null)
            throw new InvalidOperationException("globe shaders failed to load");
        lineBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, LineBufferBytes);
        fillBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, FillBufferBytes);
        overlayBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, OverlayBufferBytes);
        var si = new SDL_GPUSamplerCreateInfo
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
        };
        nearest = SDL_CreateGPUSampler(device, &si);
    }

    /// <summary>Uploads the baked levels once: 0 the coarse (1:110M), 1 the medium (1:50M).</summary>
    public void UploadLand(LandMesh coarse, LandMesh medium)
    {
        ReleaseMeshes();
        LandMesh[] meshes = [coarse, medium];
        for (int k = 0; k < 2; k++)
        {
            var m = meshes[k];
            meshFill[k] = GpuUpload.Buffer<Vector3>(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, m.Fill);
            meshCoast[k] = GpuUpload.Buffer<CoastVertex>(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, m.Coast);
            meshIdx[k] = GpuUpload.Buffer<uint>(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX, m.CoastIndices);
            meshFillCount[k] = (uint)m.Fill.Length;
            meshIdxCount[k] = (uint)m.CoastIndices.Length;
        }
    }

    void EnsurePipelines(RenderTarget target)
    {
        if (globePipe != null && pipeFormat == target.ColorFormat && pipeSamples == target.SampleCount && pipeDepth == target.HasDepth)
            return;
        ReleasePipelines();
        (pipeFormat, pipeSamples, pipeDepth) = (target.ColorFormat, target.SampleCount, target.HasDepth);
        // the main pass: depth when the target has it (the globe writes the masking sphere's, the baked coast tests it)
        GpuPipelineBuilder Main()
        {
            var b = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
                .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).DisableDepthTest();
            return target.HasDepth ? b.SetDepthFormat(RenderTarget.DepthFormat) : b.NoDepthTarget();
        }
        globePipe = (target.HasDepth ? Main().EnableDepthTest(true, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_ALWAYS) : Main())
            .SetShaders(globeVs, globeFs).DisableBlending().Build(device);
        linePipe = Main().SetShaders(lineVs, lineFs).EnableBlendingAlpha()
            .AddVertexBuffer(0, LineVertex.SizeInBytes)
            .AddVertexAttribute(0, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 0)
            .AddVertexAttribute(1, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 8)
            .AddVertexAttribute(2, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4, 16)
            .Build(device);
        if (target.HasDepth)
            coast3dPipe = Main().EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
                .SetShaders(coast3dVs, lineFs).EnableBlendingAlpha()
                .AddVertexBuffer(0, CoastVertex.SizeInBytes)
                .AddVertexAttribute(0, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 0)
                .AddVertexAttribute(1, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 12)
                .AddVertexAttribute(2, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 24)
                .AddVertexAttribute(3, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT, 36)
                .Build(device);
        // the winding count: one sample, additive, both faces, with the masking sphere's depth
        GpuPipelineBuilder Count() => new GpuPipelineBuilder().SetColorFormat(CoverageFormat)
            .SetDepthFormat(RenderTarget.DepthFormat).SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingAdditive();
        depthPipe = Count().EnableDepthTest(true, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_ALWAYS).SetShaders(globeVs, depthFs).Build(device);
        fill3dPipe = Count().EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL).SetShaders(land3dVs, fillFs)
            .AddVertexBuffer(0, 12)
            .AddVertexAttribute(0, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 0)
            .Build(device);
        fillPipe = Count().DisableDepthTest().SetShaders(fillVs, fillFs)
            .AddVertexBuffer(0, 8)
            .AddVertexAttribute(0, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 0)
            .Build(device);
        if (globePipe == null || linePipe == null || fillPipe == null || depthPipe == null || fill3dPipe == null
            || (target.HasDepth && coast3dPipe == null))
            throw new InvalidOperationException($"globe pipelines: {SDL_GetError()}");
    }

    void ReleasePipelines()
    {
        foreach (var p in new[] { (nint)globePipe, (nint)linePipe, (nint)fillPipe, (nint)depthPipe, (nint)fill3dPipe, (nint)coast3dPipe })
            if (p != 0)
                SDL_ReleaseGPUGraphicsPipeline(device, (SDL_GPUGraphicsPipeline*)p);
        globePipe = linePipe = fillPipe = depthPipe = fill3dPipe = coast3dPipe = null;
    }

    void ReleaseMeshes()
    {
        for (int k = 0; k < 2; k++)
        {
            foreach (var b in new[] { (nint)meshFill[k], (nint)meshCoast[k], (nint)meshIdx[k] })
                if (b != 0)
                    SDL_ReleaseGPUBuffer(device, (SDL_GPUBuffer*)b);
            meshFill[k] = meshCoast[k] = meshIdx[k] = null;
        }
    }

    /// <summary>Clears <paramref name="target"/> and draws the globe seen through <paramref name="view"/>, lit from
    /// <paramref name="sun"/> (a unit vector, Earth-fixed). The land is the baked level <paramref name="bakedLevel"/>
    /// (0 coarse, 1 medium, uploaded by <see cref="UploadLand"/>), or with -1 the fans in <paramref name="landFill"/>
    /// (triangles in NDC). <paramref name="lines"/> go on top, then <paramref name="layer"/> (the ships), then
    /// <paramref name="overlay"/>: lines rebuilt every frame (routes, selection), apart from the ones rebuilt only when
    /// the view changes. <paramref name="sunMotion"/> is <see cref="SunLapse.Motion"/>: 1 the live sun, 0 the
    /// time-lapse (each latitude at its mean over the day, the sun's track drawn).</summary>
    public void Render(SDL_GPUCommandBuffer* cmd, RenderTarget target, in MapView view, Vec3d sun,
        ReadOnlySpan<Vector2> landFill, int bakedLevel, MapLines lines, MapLines? overlay = null, IMapLayer? layer = null,
        double sunMotion = 1)
    {
        EnsurePipelines(target);
        if (coverage == null)
            coverage = new RenderTarget(device, target.Width, target.Height, CoverageFormat, depth: true);
        else
            coverage.Resize(target.Width, target.Height);
        var verts = lines.Vertices;
        int maxVerts = (int)(LineBufferBytes / LineVertex.SizeInBytes);
        if (verts.Length > maxVerts)
            verts = verts[..(maxVerts - maxVerts % 6)];
        int maxFill = (int)(FillBufferBytes / 8);
        if (landFill.Length > maxFill)
            landFill = landFill[..(maxFill - maxFill % 3)];
        var over = overlay != null ? overlay.Vertices : default;
        int maxOver = (int)(OverlayBufferBytes / LineVertex.SizeInBytes);
        if (over.Length > maxOver)
            over = over[..(maxOver - maxOver % 6)];
        lineBuf.Upload(cmd, verts);   // copy passes: before the render passes
        overlayBuf.Upload(cmd, over);
        layer?.Copy(cmd);
        fillBuf.Upload(cmd, landFill);
        bool baked = bakedLevel is 0 or 1 && meshFill[bakedLevel] != null;

        var g = GlobeUniforms(view, sun, sunMotion);
        var vp = ViewUniforms(view, target);

        // the land's winding count: the masking sphere's depth, then the fans
        var cpass = coverage.BeginPass(cmd, new SDL_FColor { r = 0, g = 0, b = 0, a = 0 });
        if (baked)
        {
            SDL_BindGPUGraphicsPipeline(cpass, depthPipe);
            SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&g), (uint)sizeof(GlobeParams));
            SDL_DrawGPUPrimitives(cpass, 3, 1, 0, 0);
            SDL_BindGPUGraphicsPipeline(cpass, fill3dPipe);
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&vp), (uint)sizeof(ViewParams));
            var mb = new SDL_GPUBufferBinding { buffer = meshFill[bakedLevel], offset = 0 };
            SDL_BindGPUVertexBuffers(cpass, 0, &mb, 1);
            SDL_DrawGPUPrimitives(cpass, meshFillCount[bakedLevel], 1, 0, 0);
        }
        else if (landFill.Length > 0)
        {
            SDL_BindGPUGraphicsPipeline(cpass, fillPipe);
            var fb = new SDL_GPUBufferBinding { buffer = fillBuf.Buffer, offset = 0 };
            SDL_BindGPUVertexBuffers(cpass, 0, &fb, 1);
            SDL_DrawGPUPrimitives(cpass, (uint)landFill.Length, 1, 0, 0);
        }
        SDL_EndGPURenderPass(cpass);

        // the globe, the baked coast, the lines
        var pass = target.BeginPass(cmd, new SDL_FColor { r = 0, g = 0, b = 0, a = 1 }, depth: target.HasDepth);
        SDL_BindGPUGraphicsPipeline(pass, globePipe);
        var cov = new SDL_GPUTextureSamplerBinding { texture = coverage.Resolve, sampler = nearest };
        SDL_BindGPUFragmentSamplers(pass, 0, &cov, 1);
        SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&g), (uint)sizeof(GlobeParams));
        SDL_DrawGPUPrimitives(pass, 3, 1, 0, 0);
        if (baked && coast3dPipe != null)
        {
            SDL_BindGPUGraphicsPipeline(pass, coast3dPipe);
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&vp), (uint)sizeof(ViewParams));
            var cb = new SDL_GPUBufferBinding { buffer = meshCoast[bakedLevel], offset = 0 };
            SDL_BindGPUVertexBuffers(pass, 0, &cb, 1);
            var ib = new SDL_GPUBufferBinding { buffer = meshIdx[bakedLevel], offset = 0 };
            SDL_BindGPUIndexBuffer(pass, &ib, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_32BIT);
            SDL_DrawGPUIndexedPrimitives(pass, meshIdxCount[bakedLevel], 1, 0, 0, 0);
        }
        if (verts.Length > 0)
        {
            SDL_BindGPUGraphicsPipeline(pass, linePipe);
            var binding = new SDL_GPUBufferBinding { buffer = lineBuf.Buffer, offset = 0 };
            SDL_BindGPUVertexBuffers(pass, 0, &binding, 1);
            SDL_DrawGPUPrimitives(pass, (uint)verts.Length, 1, 0, 0);
        }
        layer?.Draw(cmd, pass, target, view);
        if (over.Length > 0)
        {
            SDL_BindGPUGraphicsPipeline(pass, linePipe);
            var binding = new SDL_GPUBufferBinding { buffer = overlayBuf.Buffer, offset = 0 };
            SDL_BindGPUVertexBuffers(pass, 0, &binding, 1);
            SDL_DrawGPUPrimitives(pass, (uint)over.Length, 1, 0, 0);
        }
        SDL_EndGPURenderPass(pass);
    }

    // the depth range for the baked land and the masking sphere: from half the altitude to past the far side
    internal static (double Near, double Far) DepthRange(in MapView v) => (v.Altitude * 0.5, v.Altitude + 2 * Earth.Radius);

    /// <summary>The shader's camera, in Earth radii with the eye at the origin. |eye|² - 1 is formed from the altitude
    /// in double: the one large quantity, exact, so the ray-cast holds close to the sea. So is |eye|² - r² for the
    /// masking sphere.</summary>
    static GlobeParams GlobeUniforms(in MapView v, Vec3d sun, double sunMotion)
    {
        double h = v.Altitude / Earth.Radius, r = 1 - MaskDepthMetres / Earth.Radius;
        var (near, far) = DepthRange(v);
        return new GlobeParams
        {
            Right = new Vector4(F(v.Right), (float)v.TanHalfX),
            Up = new Vector4(F(v.Up), (float)v.TanHalfY),
            Fwd = new Vector4(F(v.Forward), (float)(1 + h)),
            EyeDir = new Vector4(F(-v.Forward), (float)(h * (2 + h))),
            Sun = new Vector4(F(sun), (float)Math.Clamp(sunMotion, 0, 1)),
            Depth = new Vector4((float)(h * (2 + h) + (1 - r * r)), (float)(near / Earth.Radius), (float)(far / Earth.Radius),
                (float)(v.MetresPerPixel / Earth.Radius)),
        };
    }

    /// <summary>The baked land's camera, in metres: floats are enough at the coarse levels it's used at.</summary>
    static ViewParams ViewUniforms(in MapView v, RenderTarget target)
    {
        var (near, far) = DepthRange(v);
        return new ViewParams
        {
            Eye = new Vector4(F(v.Eye), 0),
            Right = new Vector4(F(v.Right), (float)v.TanHalfX),
            Up = new Vector4(F(v.Up), (float)v.TanHalfY),
            Fwd = new Vector4(F(v.Forward), 0),
            Depth = new Vector4((float)near, (float)far, 0, 0),
            Viewport = new Vector4(target.Width, target.Height, LandView.CoastWidthPx / 2, 0),
            Color = LandView.CoastColor,
        };
    }

    static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);

    public void Dispose()
    {
        ReleasePipelines();
        ReleaseMeshes();
        lineBuf.Dispose();
        overlayBuf.Dispose();
        fillBuf.Dispose();
        coverage?.Dispose();
        SDL_ReleaseGPUSampler(device, nearest);
        foreach (var s in new[] { (nint)globeVs, (nint)globeFs, (nint)lineVs, (nint)lineFs, (nint)fillVs, (nint)fillFs,
                     (nint)land3dVs, (nint)coast3dVs, (nint)depthFs })
            SDL_ReleaseGPUShader(device, (SDL_GPUShader*)s);
    }
}

/// <summary>Something drawn on the globe inside its main pass, depth-tested against the masking sphere (the ships).
/// <see cref="Copy"/> runs before the passes (uploads), <see cref="Draw"/> in the main pass after the map's lines.</summary>
public unsafe interface IMapLayer
{
    void Copy(SDL_GPUCommandBuffer* cmd);
    void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view);
}
