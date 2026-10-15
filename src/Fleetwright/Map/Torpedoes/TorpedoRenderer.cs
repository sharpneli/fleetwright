using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map.Torpedoes;

/// <summary>One torpedo as torpedo.vert reads it (std430: 4 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TorpedoGpu
{
    public Vector4 Base, Axis, Side, Look;
    public const int SizeInBytes = 64;
}

/// <summary>The torpedoes themselves in the world view (docs/visuals/torpedoes.md R3): each running torpedo in the book
/// a dark, soft shape under the water at its true place and heading, its length by its calibre (13.5 calibres: a 21-inch
/// 7.2 m), its contrast through the water over it (e^(−2Kd), K ≈ 0.25 /m in the North Sea) times
/// <see cref="TorpedoLook.Body"/> (decision 2: emphasised, a named departure), never shorter on the screen than
/// <see cref="TorpedoLook.BodyFloorPx"/> (a third as wide). One instanced draw in the main pass, after the sea and before
/// the ships; the ships' layer calls it. Nothing allocates once grown.</summary>
public sealed unsafe class TorpedoRenderer : IDisposable
{
    const uint BufferBytes = 64 << 10;   // 1,024 torpedoes
    const double LengthCalibres = 13.5, Attenuation = 0.25, MaxOpacity = 0.75;

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fs;
    readonly DynamicGpuBuffer buf;
    SDL_GPUGraphicsPipeline* pipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    TorpedoGpu[] items = new TorpedoGpu[64];
    int count, uploaded;
    ShipLights lights = ShipLights.Flat;
    Params prms;

    [StructLayout(LayoutKind.Sequential)]
    struct ViewParams
    {
        public Vector4 Right, Up, Fwd, Depth;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Params
    {
        public Vector4 SunL, MoonL;
    }

    public TorpedoRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/torpedo.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fs = GpuShader.Load(device, "Content/Shaders/Compiled/torpedo.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 2);
        if (vs == null || fs == null)
            throw new InvalidOperationException("torpedo shaders failed to load");
        buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, BufferBytes);
    }

    /// <summary>A frame: the book's torpedoes running at <paramref name="nowMs"/> as instances; <paramref name="world"/> the
    /// view's <see cref="WorldView.Factor(in MapView)"/>.</summary>
    public void Build(in MapView view, TorpedoBook book, long nowMs, double world, ShipLights frameLights, Func<int, ShipClass?> classOf)
    {
        count = 0;
        lights = frameLights;
        if (world <= 0 || !TorpedoLook.On || TorpedoLook.Body <= 0)
            return;
        var up = (-view.Forward).Normalized();
        prms = new Params
        {
            SunL = new Vector4(0, 0, (float)Vec3d.Dot(lights.SunDir, up), 0),
            MoonL = new Vector4(0, 0, (float)Vec3d.Dot(lights.MoonDir, up), 0),
        };
        double mpp = view.MetresPerPixel, reach = (view.Width + view.Height) * mpp;
        var centre = up * Earth.Radius;
        for (int i = 0; i < book.Count; i++)
        {
            ref readonly var t = ref book[i];
            double age = (nowMs - t.LaunchMs) / 1000.0;
            if (age < 0 || age >= t.EndS)
                continue;
            var (p, f) = (t.Origin, t.Forward);
            Earth.Advance(ref p, ref f, t.SpeedMs * age);
            if (Earth.Distance(p, centre) > reach)
                continue;
            var c = t.Class >= 0 ? classOf(t.Class) : null;
            bool dives = c != null && t.Launcher >= 0 && t.Launcher < c.Launchers.Length && c.Launchers[t.Launcher].Kind == LauncherKind.Mount;
            double calibre = c != null && t.Row >= 0 && t.Row < c.Torpedoes.Length ? c.Torpedoes[t.Row].CalibreMm : 533;
            double depth = TorpedoTrack.Depth(t.SpeedMs * age, t.DepthM, dives);
            double hl = LengthCalibres * calibre / 1000 / 2, r = calibre / 1000 / 2;
            // the floor: never shorter than BodyFloorPx on the screen, a third as wide
            double minHl = TorpedoLook.BodyFloorPx * mpp / 2;
            if (hl < minHl)
                (hl, r) = (minHl, Math.Max(r, minHl / 3));
            double opacity = Math.Min(TorpedoLook.Body * Math.Exp(-2 * Attenuation * depth), MaxOpacity);
            double blur = Math.Max(0.15 * depth, 0.5 * mpp);
            if (count == items.Length)
                Array.Resize(ref items, items.Length * 2);
            var side = Vec3d.Cross(p.Normalized(), f);
            var rel = p - view.Eye;
            items[count++] = new TorpedoGpu
            {
                Base = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, 0),
                Axis = new Vector4(F(f), (float)hl),
                Side = new Vector4(F(side), (float)r),
                Look = new Vector4((float)opacity, (float)blur, (float)world, 0),
            };
        }
    }

    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        uploaded = Math.Min(count, (int)(BufferBytes / TorpedoGpu.SizeInBytes));
        if (uploaded > 0)
            buf.Upload(cmd, items.AsSpan(0, uploaded));
    }

    /// <summary>The torpedoes under the water, after the sea and before the ships.</summary>
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
                .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingPremultiplied().ClearVertexLayout();
            b = target.HasDepth
                ? b.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
                : b.NoDepthTarget().DisableDepthTest();
            pipe = b.SetShaders(vs, fs).Build(device);
            if (pipe == null)
                throw new InvalidOperationException($"torpedo pipeline: {SDL_GetError()}");
        }
        SDL_BindGPUGraphicsPipeline(pass, pipe);
        var sb = buf.Buffer;
        SDL_BindGPUVertexStorageBuffers(pass, 0, &sb, 1);
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
        var pr = prms;
        SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&lu), (uint)sizeof(LightUniforms));
        SDL_PushGPUFragmentUniformData(cmd, 1, (nint)(&pr), (uint)sizeof(Params));
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
