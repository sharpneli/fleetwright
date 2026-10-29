using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Map.Flashes;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map.Night;

/// <summary>One beam as beam.vert reads it (std430: 7 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct BeamGpu
{
    public Vector4 Base, Axis, Side, Light, Shadow, Colour, Hull;
    public const int SizeInBytes = 112;
}

/// <summary>The searchlights' beams in the air (docs/visuals/night-lights.md Q10c): each beam the night's lights hold
/// (<see cref="NightLights"/>) a quad on the sea under it, the air's glow out of it closed form in beam.frag. Its numbers
/// are the sim's: the beam's peak and spread, the air's scattering (<see cref="SearchlightModel.ScatterPerM"/>, Allard's,
/// at least clear air's) and its share seen from beside (all round, as <see cref="SearchlightModel.ColumnCd"/> has it),
/// so the beam the player sees is the one the lookouts' model says a beam shows. In the night eye's units (display units
/// a cd/m², <see cref="FlashRenderer.PerCandela"/>) times <see cref="Gain"/>. Past the ship it's held on, her shadow in
/// the air. One instanced draw in the main pass after the gun smoke and the magazines, before the smoke field (which
/// takes the beams' light itself); the ships' layer calls it. Nothing allocates once grown.</summary>
public sealed unsafe class BeamRenderer : IDisposable
{
    const uint BufferBytes = 112 << 8;   // 256 beams

    /// <summary>The glow's share left over the ship the beam's held on (a look's departure, the user, 2026-10-29): the
    /// air over her is lit as much as anywhere, but her lit hull is what the beam is for; softened at her edges.</summary>
    public static double OverHull = 0.3;

    /// <summary>A gain on the glow as shown, after the eye's response (1: physical, through the night eye). Halved, a
    /// look's departure (the user, 2026-10-29, docs/visuals/night-lights.md Q10f): over a hull the glow saturated like a
    /// lightsaber's and hid the lit ship under it; the light the beam throws on her is untouched. After the response, so
    /// the half shows (before it, the log took most of it back).</summary>
    public static double Gain = 0.5;

    /// <summary>Where the glow stops being drawn: a share of a lit white deck (display units).</summary>
    public const double FadeTo = 0.003;

    /// <summary>The lamp's softening, m: the glow's 1/r held to the mirror's glare near it.</summary>
    public const double LampM = 6;

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fs;
    readonly DynamicGpuBuffer buf;
    SDL_GPUGraphicsPipeline* pipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    BeamGpu[] items = new BeamGpu[16];
    int count, uploaded;
    ShipLights lights = ShipLights.Flat;

    [StructLayout(LayoutKind.Sequential)]
    struct ViewParams
    {
        public Vector4 Right, Up, Fwd, Depth;
    }

    public BeamRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/beam.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fs = GpuShader.Load(device, "Content/Shaders/Compiled/beam.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 1);
        if (vs == null || fs == null)
            throw new InvalidOperationException("beam shaders failed to load");
        buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, BufferBytes);
    }

    /// <summary>A frame: the night's beams as instances (their light already times the world's share).</summary>
    public void Build(in MapView view, NightLights night, Snapshot s, long drawMs, ShipLights frameLights, double visibilityM,
        SimSettings settings, Func<int, ShipClass?> classOf)
    {
        count = 0;
        lights = frameLights;
        if (Gain <= 0 || night.Count == 0)
            return;
        var up = (-view.Forward).Normalized();
        double perCd = FlashRenderer.PerCandela(frameLights, up);
        double scatter = SearchlightModel.ScatterPerM(visibilityM, settings), extinction = scatter;
        for (int i = 0; i < night.Count; i++)
        {
            if (!night.IsBeam(i) || !(night.Candela(i) > 0))
                continue;
            var lamp = night.At(i);
            var ground = lamp.Normalized() * Earth.Radius;
            double h = lamp.Length - Earth.Radius, sigma = night.SigmaRad(i);
            var axis = night.Axis(i);
            var side = Vec3d.Cross(ground.Normalized(), axis).Normalized();
            // the glow's scale: I sigma_s p sqrt(2 pi) s, p all round (the sim's beam from beside), in display units
            double k = night.Candela(i) * scatter / (4 * Math.PI) * Math.Sqrt(2 * Math.PI) * sigma * perCd;
            // out to where the glow on its axis (k / r e^(-sigma r), half the beam above the sea) falls to FadeTo
            double len = 200;
            while (len < 20_000 && 0.5 * k / len * Math.Exp(-extinction * len) > FadeTo)
                len *= 1.25;
            var rel = ground - view.Eye;
            if (count == items.Length)
                Array.Resize(ref items, items.Length * 2);
            items[count++] = new BeamGpu
            {
                Base = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, (float)h),
                Axis = new Vector4(F(axis), (float)len),
                Side = new Vector4(F(side), (float)sigma),
                Light = new Vector4((float)k, (float)extinction, (float)LampM, (float)Gain),
                Hull = new Vector4((float)OverHull, 0, 0, 0),
                Shadow = ShadowOf(night.Target(i), s, drawMs, ground, axis, side, classOf, out double alongHalf),
                Colour = new Vector4(FlashPhotometry.UnitLuminance(NightLights.ArcColour), (float)alongHalf),
            };
        }
    }

    /// <summary>The ship a beam's held on, as beam.frag reads her shadow: where she stands along the beam and across it,
    /// the half width of her silhouette across it, her top (m); x &lt; 0 for none (sweeping, or not seen). And the half of
    /// her length along the beam (<paramref name="alongHalf"/>, m: the glow thinned over her).</summary>
    static Vector4 ShadowOf(int target, Snapshot s, long drawMs, Vec3d ground, Vec3d axis, Vec3d side, Func<int, ShipClass?> classOf,
        out double alongHalf)
    {
        alongHalf = 0;
        if (target < 0)
            return new Vector4(-1, 0, 0, 0);
        foreach (var v in (ReadOnlySpan<ShipsView>)[s.Ships, s.Sighted])
            if (v.IndexOf(target) is int k and >= 0 && classOf(v.Class[k]) is { } c)
            {
                var (p, f) = v.PoseAt(k, drawMs);
                var d = p - ground;
                double along = Vec3d.Dot(d, axis), across = Vec3d.Dot(d, side);
                if (along <= 0)
                    break;
                double half = 0.5 * (c.Length * Math.Abs(Vec3d.Dot(f, side)) + c.Beam * Math.Abs(Vec3d.Dot(f, axis)));
                alongHalf = 0.5 * (c.Length * Math.Abs(Vec3d.Dot(f, axis)) + c.Beam * Math.Abs(Vec3d.Dot(f, side)));
                return new Vector4((float)along, (float)across, (float)Math.Max(half, 3), (float)Math.Max(c.Silhouette.TopM, 5));
            }
        return new Vector4(-1, 0, 0, 0);
    }

    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        uploaded = Math.Min(count, (int)(BufferBytes / BeamGpu.SizeInBytes));
        if (uploaded > 0)
            buf.Upload(cmd, items.AsSpan(0, uploaded));
    }

    /// <summary>The beams' glow, screen-blended over what's under them.</summary>
    public void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0)
            return;
        if (pipe == null || pipeFormat != target.ColorFormat || pipeSamples != target.SampleCount || pipeDepth != target.HasDepth)
        {
            if (pipe != null)
                SDL_ReleaseGPUGraphicsPipeline(device, pipe);
            (pipeFormat, pipeSamples, pipeDepth) = (target.ColorFormat, target.SampleCount, target.HasDepth);
            var b = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
                .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingScreen().ClearVertexLayout();
            b = target.HasDepth
                ? b.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
                : b.NoDepthTarget().DisableDepthTest();
            pipe = b.SetShaders(vs, fs).Build(device);
            if (pipe == null)
                throw new InvalidOperationException($"beam pipeline: {SDL_GetError()}");
        }
        SDL_BindGPUGraphicsPipeline(pass, pipe);
        var sb = buf.Buffer;
        SDL_BindGPUVertexStorageBuffers(pass, 0, &sb, 1);
        SDL_BindGPUFragmentStorageBuffers(pass, 0, &sb, 1);
        var (near, far) = GlobeRenderer.DepthRange(view);
        var vp = new ViewParams
        {
            Right = new Vector4(F(view.Right), (float)view.TanHalfX),
            Up = new Vector4(F(view.Up), (float)view.TanHalfY),
            Fwd = new Vector4(F(view.Forward), 0),
            Depth = new Vector4((float)near, (float)far, 0, 0),
        };
        SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&vp), (uint)sizeof(ViewParams));
        var lu = lights.Uniforms;
        SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&lu), (uint)sizeof(LightUniforms));
        SDL_DrawGPUPrimitives(pass, 6, (uint)uploaded, 0, 0);
    }

    static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);

    public void Dispose()
    {
        SDL_WaitForGPUIdle(device);
        if (pipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, pipe);
        buf.Dispose();
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fs);
    }
}
