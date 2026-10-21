using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map.Smoke;

/// <summary>The smoke field drawn (docs/battle/smoke.md Z5, the plain look): the sim's own puffs, what blocks sight, as
/// they are at the frame's time.
/// <list type="bullet">
/// <item><b>The copy</b> comes from the sim's thread every <c>SmokeCopyMs</c> of real time (<see cref="SmokeCopy"/>,
/// its own triple buffer) and goes up to a storage buffer when it changes; between copies nothing is uploaded: each
/// puff's state is closed form in its age (smokefield.glsl, the sim's formulas) and the air's frame is carried on to the
/// frame's time on the CPU.</item>
/// <item><b>The accumulation</b> (an approved pipeline exception, 2026-10-21): a half-resolution RGBA16F target with
/// mips, cleared and drawn level by level before the main pass; each puff adds its column optical depth (and its tint
/// times it) into the level where its σ is 1.5-3 texels, so a puff writes a few dozen texels at any zoom and nothing
/// needs sorting (summing τ is Beer-Lambert's).</item>
/// <item><b>The collapse</b>, at half resolution: each level in use, from the top down, its own texels plus the B-spline
/// bicubic of the collapsed level above, into two chains taken in turn (a pass never reads the texture it draws).</item>
/// <item><b>The resolve</b>, in the main pass after the ships and the effects: the lowest collapsed level (B-spline
/// bicubic), α = 1 − e^(−τ), lit as the gun smoke is.</item>
/// </list>
/// In the world view a puff's height lifts it up the screen (the effects camera); on the table it lies flat. Nothing
/// allocates once grown.</summary>
public sealed unsafe class SmokeFieldRenderer : IDisposable
{
    const int MaxLevels = 10;
    const SDL_GPUTextureFormat FieldFormat = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R16G16B16A16_FLOAT;

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fs, vsBlit, fsResolve, fsCollapse;
    readonly SDL_GPUSampler* sampler;
    SDL_GPUGraphicsPipeline* accumPipe, collapsePipe, resolvePipe;
    SDL_GPUTextureFormat resolveFormat;
    SDL_GPUSampleCount resolveSamples;
    bool resolveDepth;
    DynamicGpuBuffer? buf;
    SDL_GPUTexture* field;
    readonly SDL_GPUTexture*[] collapsed = new SDL_GPUTexture*[2];
    uint fieldW, fieldH;
    int levels;

    SmokeCopy? copy;
    int uploadedVersion = -1, count;
    // the levels the puffs fall in this frame (only they're cleared, drawn into and summed)
    int lowLevel, highLevel;
    double minSigma, maxSigma;
    bool drawing;
    View view;
    Params prms;
    ShipLights lights = ShipLights.Flat;

    [StructLayout(LayoutKind.Sequential)]
    struct View
    {
        public Vector4 Right, Up, Fwd, Depth, East, North, M0, M1, M2, Anchor, Wind;
        public EddyModes Eddies;
        public Vector4 GrainE, GrainN, GrainO;
    }

    /// <summary>The grain's lattice period, m (smokefield.frag's GRAIN_PERIOD).</summary>
    const double GrainPeriodM = 65536;

    /// <summary>The large eddies' modes as smokefield.glsl's fieldEddy reads them (Z5c): k and A, then d and the phase.</summary>
    [System.Runtime.CompilerServices.InlineArray(2 * SmokeEddies.Modes)]
    public struct EddyModes
    {
        Vector4 first;
    }

    /// <summary>The large eddies at a world time, s, for a copy anchored at <paramref name="anchor"/> (air's frame): each
    /// mode's k and amplitude, its way and its phase there (k · anchor − ω t + φ, in double, wrapped).</summary>
    public static EddyModes Eddies(SmokeEddies? e, Vec3d anchor, double t)
    {
        var m = new EddyModes();
        if (e == null)
            return m;
        for (int i = 0; i < SmokeEddies.Modes; i++)
        {
            double phase = Math.IEEERemainder(Vec3d.Dot(e.K[i], anchor) - e.Omega[i] * t + e.Phase[i], 2 * Math.PI);
            m[i] = new Vector4(F(e.K[i]), (float)e.Amp[i]);
            m[SmokeEddies.Modes + i] = new Vector4(F(e.Dir[i]), (float)phase);
        }
        return m;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Params
    {
        public Vector4 SunL, MoonL, Info;
    }

    public SmokeFieldRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/smokefield.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fs = GpuShader.Load(device, "Content/Shaders/Compiled/smokefield.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 0);
        vsBlit = GpuShader.Load(device, "Content/Shaders/Compiled/blit.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 0);
        fsResolve = GpuShader.Load(device, "Content/Shaders/Compiled/smokeresolve.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 1, 0, 0, 2);
        fsCollapse = GpuShader.Load(device, "Content/Shaders/Compiled/smokecollapse.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 2, 0, 0, 1);
        if (vs == null || fs == null || vsBlit == null || fsResolve == null || fsCollapse == null)
            throw new InvalidOperationException("smoke field shaders failed to load");
        var si = new SDL_GPUSamplerCreateInfo
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR, mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            min_lod = 0, max_lod = MaxLevels,   // its default range is level 0 alone
        };
        sampler = SDL_CreateGPUSampler(device, &si);
        var b = new GpuPipelineBuilder().SetColorFormat(FieldFormat).SetMultisampling(SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1)
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingSum().ClearVertexLayout().NoDepthTarget().DisableDepthTest()
            .SetShaders(vs, fs);
        accumPipe = b.Build(device);
        collapsePipe = new GpuPipelineBuilder().SetColorFormat(FieldFormat).SetMultisampling(SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1)
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).DisableBlending().ClearVertexLayout().NoDepthTarget().DisableDepthTest()
            .SetShaders(vsBlit, fsCollapse).Build(device);
        if (accumPipe == null || collapsePipe == null)
            throw new InvalidOperationException($"smoke field pipeline: {SDL_GetError()}");
    }

    /// <summary>The puffs drawn this frame (0 when none).</summary>
    public int Count => drawing ? count : 0;

    /// <summary>The frame's view of the field: the newest copy taken, the air's frame carried on to the frame's time
    /// (<paramref name="drawMs"/>, world ms), the camera, the lift by the world view's share
    /// (<paramref name="world"/>), and the light.</summary>
    public void Build(in MapView v, Snapshot s, long drawMs, double world, ShipLights frameLights)
    {
        drawing = false;
        lights = frameLights;
        if (s.SmokeCopies is not { } copies || !SmokeFieldLook.On)
            return;
        if (copies.Acquire() || copy == null)
            copy = copies.Front;
        count = copy.Count;
        if (count == 0)
            return;
        drawing = true;
        // the puffs' sigmas now: the least as copied (a puff only grows; a new one starts at least as wide), the greatest
        // carried on by the turbulence's growth since the copy, with room for the rise's spread
        double since = Math.Max((drawMs - copy.CopiedMs) / 1000.0, 0);
        minSigma = Math.Sqrt(copy.MinH2);
        maxSigma = 1.1 * Math.Sqrt(copy.MaxH2 + 2 * copy.SigmaV * copy.SigmaV * copy.LagrangianH * since) + 1;
        var frame = copy.FrameAtMs(drawMs);
        var (r0, r1, r2) = frame.Rows;
        var anchor = frame.ToWorld(copy.Anchor) - v.Eye;
        var (near, far) = GlobeRenderer.DepthRange(v);
        double lift = SmokeFieldLook.Lift ? Effects.K * Math.Clamp(world, 0, 1) : 0;
        view = new View
        {
            Right = new Vector4(F(v.Right), (float)v.TanHalfX),
            Up = new Vector4(F(v.Up), (float)v.TanHalfY),
            Fwd = new Vector4(F(v.Forward), (float)lift),
            Depth = new Vector4((float)near, (float)far, 0, 0),
            East = new Vector4(F(v.Right), (float)(2 * v.MetresPerPixel)),
            North = new Vector4(F(v.Up), (float)((drawMs - copy.CopiedMs) / 1000.0)),
            M0 = new Vector4(F(r0), (float)copy.SigmaV),
            M1 = new Vector4(F(r1), (float)copy.LagrangianH),
            M2 = new Vector4(F(r2), (float)SmokeFieldLook.EdgeTau),
            Anchor = new Vector4(F(anchor), 1),
            Wind = new Vector4(F(Earth.Forward(frame.ToWorld(copy.Anchor), copy.WindToDeg)), 0),
            Eddies = Eddies(copy.Eddies, copy.Anchor, drawMs / 1000.0),
        };
        // the grain's plane (Z5d): fixed in the air's frame, the large eddies' (the sea's plane where the field began),
        // the anchor's place on it wrapped in double so the grain stays put from copy to copy
        var gu = copy.Eddies?.Up ?? copy.Frame.ToAir(copy.FrameAt).Normalized();
        var ge = Vec3d.Cross(Math.Abs(gu.Z) < 0.9 ? new Vec3d(0, 0, 1) : new Vec3d(1, 0, 0), gu).Normalized();
        var gn = Vec3d.Cross(gu, ge);
        view.GrainE = new Vector4(F(ge), (float)SmokeFieldLook.Grain);
        view.GrainN = new Vector4(F(gn), 0);
        view.GrainO = new Vector4((float)Wrap(Vec3d.Dot(copy.Anchor, ge)), (float)Wrap(Vec3d.Dot(copy.Anchor, gn)), 0, 0);
        var (east, north, up) = (v.Right, v.Up, (-v.Forward).Normalized());
        Vector3 Local(Vec3d d) => new((float)Vec3d.Dot(d, east), (float)Vec3d.Dot(d, north), (float)Vec3d.Dot(d, up));
        prms = new Params { SunL = new Vector4(Local(lights.SunDir), 0), MoonL = new Vector4(Local(lights.MoonDir), 0) };
    }

    /// <summary>The copy into the storage buffer when it's changed (a copy pass, before any render pass).</summary>
    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        if (!drawing || copy == null || copy.Version == uploadedVersion)
            return;
        uint bytes = (uint)(count * SmokeRecord.SizeInBytes);
        if (buf == null || buf.Capacity < bytes)
        {
            buf?.Dispose();
            buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ,
                Math.Max(bytes * 3 / 2, 1u << 20));
        }
        buf.Upload(cmd, copy.Records.AsSpan(0, count));
        uploadedVersion = copy.Version;
    }

    /// <summary>The accumulation, level by level, before the main pass.</summary>
    public void Prepass(SDL_GPUCommandBuffer* cmd, RenderTarget target)
    {
        if (!drawing || buf == null || (SmokeFieldLook.Parts & 1) == 0)
            return;
        EnsureField(Math.Max(target.Width / 2, 1), Math.Max(target.Height / 2, 1));
        double texel = view.East.W;
        (lowLevel, highLevel) = (LevelOf(minSigma, texel), LevelOf(maxSigma, texel));
        var sb = buf.Buffer;
        for (int l = lowLevel; l <= highLevel; l++)
        {
            var ct = new SDL_GPUColorTargetInfo
            {
                texture = field, mip_level = (uint)l, clear_color = new SDL_FColor(), load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
                store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE, cycle = l == lowLevel,
            };
            var pass = SDL_BeginGPURenderPass(cmd, &ct, 1, null);
            var vp = new SDL_GPUViewport { x = 0, y = 0, w = Math.Max(fieldW >> l, 1), h = Math.Max(fieldH >> l, 1), min_depth = 0, max_depth = 1 };
            SDL_SetGPUViewport(pass, &vp);
            SDL_BindGPUGraphicsPipeline(pass, accumPipe);
            SDL_BindGPUVertexStorageBuffers(pass, 0, &sb, 1);
            var u = view;
            u.Depth.Z = l;
            u.Depth.W = highLevel;   // a puff wider than foreseen is drawn here, finer than its own (never lost)
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&u), (uint)sizeof(View));
            SDL_DrawGPUPrimitives(pass, 6, (uint)count, 0, 0);
            SDL_EndGPURenderPass(pass);
        }
        // the collapse, top down: level l into chain l & 1, reading level l + 1 from the other
        var bind = stackalloc SDL_GPUTextureSamplerBinding[2];
        for (int l = highLevel; l >= lowLevel; l--)
        {
            var ct = new SDL_GPUColorTargetInfo
            {
                texture = collapsed[l & 1], mip_level = (uint)l, load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE,
                store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE, cycle = l >= highLevel - 1,
            };
            var pass = SDL_BeginGPURenderPass(cmd, &ct, 1, null);
            var vp = new SDL_GPUViewport { x = 0, y = 0, w = Math.Max(fieldW >> l, 1), h = Math.Max(fieldH >> l, 1), min_depth = 0, max_depth = 1 };
            SDL_SetGPUViewport(pass, &vp);
            SDL_BindGPUGraphicsPipeline(pass, collapsePipe);
            bind[0] = new SDL_GPUTextureSamplerBinding { texture = field, sampler = sampler };
            bind[1] = new SDL_GPUTextureSamplerBinding { texture = collapsed[(l + 1) & 1], sampler = sampler };
            SDL_BindGPUFragmentSamplers(pass, 0, bind, 2);
            var info = new Vector4(l, l == highLevel ? 1 : 0, fieldW, fieldH);
            SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&info), (uint)sizeof(Vector4));
            SDL_DrawGPUPrimitives(pass, 3, 1, 0, 0);
            SDL_EndGPURenderPass(pass);
        }
    }

    /// <summary>The field over the scene, in the main pass.</summary>
    public void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target)
    {
        if (!drawing || field == null || (SmokeFieldLook.Parts & 2) == 0)
            return;
        EnsureResolve(target);
        SDL_BindGPUGraphicsPipeline(pass, resolvePipe);
        var tb = new SDL_GPUTextureSamplerBinding { texture = collapsed[lowLevel & 1], sampler = sampler };
        SDL_BindGPUFragmentSamplers(pass, 0, &tb, 1);
        var lu = lights.Uniforms;
        var pr = prms;
        pr.Info = new Vector4(lowLevel + 1, (float)SmokeFieldLook.Gain, fieldW, fieldH);   // the collapsed lowest level alone
        pr.MoonL.W = lowLevel;
        SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&lu), (uint)sizeof(LightUniforms));
        SDL_PushGPUFragmentUniformData(cmd, 1, (nint)(&pr), (uint)sizeof(Params));
        SDL_DrawGPUPrimitives(pass, 3, 1, 0, 0);
    }

    /// <summary>The level a puff of this sigma is drawn into (the vertex shader's rule), among the target's.</summary>
    int LevelOf(double sigma, double texel) =>
        Math.Clamp((int)Math.Floor(Math.Log2(Math.Max(sigma, texel) / (1.5 * texel))), 0, levels - 1);

    void EnsureField(uint w, uint h)
    {
        if (field != null && w == fieldW && h == fieldH)
            return;
        if (field != null)
            SDL_ReleaseGPUTexture(device, field);
        for (int k = 0; k < 2; k++)
            if (collapsed[k] != null)
                SDL_ReleaseGPUTexture(device, collapsed[k]);
        (fieldW, fieldH) = (w, h);
        levels = Math.Min(Math.Min(MaxLevels, SmokeFieldLook.Levels), (int)Math.Floor(Math.Log2(Math.Max(w, h))) + 1);
        var info = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D, format = FieldFormat,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_COLOR_TARGET | SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
            width = w, height = h, layer_count_or_depth = 1, num_levels = (uint)levels, sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
        };
        field = SDL_CreateGPUTexture(device, &info);
        collapsed[0] = SDL_CreateGPUTexture(device, &info);
        collapsed[1] = SDL_CreateGPUTexture(device, &info);
        if (field == null || collapsed[0] == null || collapsed[1] == null)
            throw new InvalidOperationException($"smoke field target: {SDL_GetError()}");
    }

    void EnsureResolve(RenderTarget target)
    {
        if (resolvePipe != null && resolveFormat == target.ColorFormat && resolveSamples == target.SampleCount && resolveDepth == target.HasDepth)
            return;
        if (resolvePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, resolvePipe);
        (resolveFormat, resolveSamples, resolveDepth) = (target.ColorFormat, target.SampleCount, target.HasDepth);
        var b = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingPremultiplied().ClearVertexLayout().SetShaders(vsBlit, fsResolve);
        b = target.HasDepth ? b.SetDepthFormat(RenderTarget.DepthFormat).DisableDepthTest() : b.NoDepthTarget().DisableDepthTest();
        resolvePipe = b.Build(device);
        if (resolvePipe == null)
            throw new InvalidOperationException($"smoke field resolve pipeline: {SDL_GetError()}");
    }

    static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);

    static double Wrap(double m) => m - Math.Floor(m / GrainPeriodM) * GrainPeriodM;

    public void Dispose()
    {
        SDL_WaitForGPUIdle(device);
        if (accumPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, accumPipe);
        if (resolvePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, resolvePipe);
        if (collapsePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, collapsePipe);
        if (field != null)
            SDL_ReleaseGPUTexture(device, field);
        for (int k = 0; k < 2; k++)
            if (collapsed[k] != null)
                SDL_ReleaseGPUTexture(device, collapsed[k]);
        buf?.Dispose();
        SDL_ReleaseGPUSampler(device, sampler);
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fs);
        SDL_ReleaseGPUShader(device, vsBlit);
        SDL_ReleaseGPUShader(device, fsResolve);
        SDL_ReleaseGPUShader(device, fsCollapse);
    }
}
