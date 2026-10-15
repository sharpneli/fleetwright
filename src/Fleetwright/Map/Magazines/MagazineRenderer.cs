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

namespace Fleetwright.Map.Magazines;

/// <summary>One event as the magazine shaders read it (magazine.vert's Mag, std430: 8 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct MagazineGpu
{
    public Vector4 Base, Quad, Shade, Body, BodyB, Stem, Time, Misc;
    public const int SizeInBytes = 128;
}

/// <summary>The world view's magazine explosions (docs/visuals/magazine-explosions.md): the book's events in view as
/// instances, their state built on the CPU each frame (<see cref="MagazineBlast.BodyAt"/>, the wind's drift and shear,
/// the stem's fires, the fade), placed eye-relative in double, and two instanced draws over them, from the one model in
/// <c>Include/magazine.glsl</c>:
/// <list type="bullet">
/// <item>the shadows on the sea and the decks, then the bodies (fireball, cap, stem) through the effects camera, after
/// the ships and the gun smoke (<see cref="Draw"/>).</item>
/// </list>
/// It reads the snapshot's events and the frame's lights, never the world; nothing allocates once its arrays have grown.</summary>
public sealed unsafe class MagazineRenderer : IDisposable
{
    const uint BufferBytes = 64 * 1024;   // 512 events
    /// <summary>The longest shadow, metres per metre of height (a sun at ~18°: a 1 km cap's shadow is already 3 km).</summary>
    const float MaxDrift = 3;

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fsBody, fsShadow;
    readonly DynamicGpuBuffer buf;
    SDL_GPUGraphicsPipeline* bodyPipe, shadowPipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    int uploaded;

    MagazineGpu[] items = new MagazineGpu[8];
    int count;
    ShipLights lights = ShipLights.Flat;
    Params prms;
    bool shadows;

    public MagazineBook Book { get; } = new();

    /// <summary>The instances this frame (tests, stats).</summary>
    public ReadOnlySpan<MagazineGpu> Items => items.AsSpan(0, count);

    struct ViewParams
    {
        public Vector4 Right, Up, Fwd, Depth, East, North;
    }

    struct Params
    {
        public Vector4 SunL, MoonL, ShadowL, Misc, FireK, More;
    }

    public MagazineRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/magazine.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fsBody = GpuShader.Load(device, "Content/Shaders/Compiled/magazine_body.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 2);
        fsShadow = GpuShader.Load(device, "Content/Shaders/Compiled/magazine_shadow.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 2);
        if (vs == null || fsBody == null || fsShadow == null)
            throw new InvalidOperationException("magazine shaders failed to load");
        buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, BufferBytes);
    }

    // ------------------------------------------------------------------ the CPU side

    /// <summary>A frame: the book updated from the snapshot, then the events in view as instances. <paramref name="speed"/>
    /// is the clock's (0 paused), <paramref name="dtS"/> the frame's real seconds, <paramref name="world"/> the view's
    /// <see cref="WorldView.Factor(in MapView)"/>.</summary>
    public void Build(in MapView view, Snapshot s, long drawMs, double speed, double dtS, double world, ShipLights frameLights,
        Func<int, ShipClass?> classOf)
    {
        Book.Update(s.Blasts, drawMs, speed, dtS, classOf);
        lights = frameLights;
        count = 0;
        if (world <= 0 || !MagazineLook.On || Book.Count == 0)
            return;

        var up = (-view.Forward).Normalized();
        var (east, north) = (view.Right, view.Up);
        Vector3 Local(Vec3d d) => new((float)Vec3d.Dot(d, east), (float)Vec3d.Dot(d, north), (float)Vec3d.Dot(d, up));
        var sunL = Local(lights.SunDir);
        var moonL = Local(lights.MoonDir);
        var l = Local(lights.ShadowDir);
        double sinLo = Math.Sin(3 * Math.PI / 180), sinHi = Math.Sin(7 * Math.PI / 180);
        float on = (float)(lights.ShadowOpacity * SmoothStep(sinLo, sinHi, l.Z));
        var drift = l.Z > 1e-3 ? -new Vector2(l.X, l.Y) / l.Z : Vector2.Zero;
        if (drift.Length() > MaxDrift)
            drift *= MaxDrift / drift.Length();
        shadows = on > 0.002f && (MagazineLook.Parts & 2) != 0;
        double mpp = view.MetresPerPixel, k = Effects.K;
        double perCd = MagazineLook.Gain * FlashRenderer.PerCandela(lights, s.Sun, up, drawMs);
        prms = new Params
        {
            SunL = new Vector4(sunL, 0), MoonL = new Vector4(moonL, 0), ShadowL = new Vector4(drift, on, 0),
            Misc = new Vector4((float)k, (float)mpp, (float)MagazineLook.Steps, (float)perCd),
            FireK = new Vector4((float)MagazineLook.ColdK, (float)MagazineLook.HotK, (float)MagazineLook.SootAlbedo, (float)Effects.KneeM),
            More = new Vector4((float)Effects.KneeHcM, (float)MagazineLook.Mushroom, (float)MagazineLook.Whiten, 0),
        };
        // the wind, toward where it blows (east, north), and its speed at 10 m
        double toward = (s.Weather.WindFromDeg + 180) * Math.PI / 180;
        var windDir = new Vector2((float)Math.Sin(toward), (float)Math.Cos(toward));
        double u10 = s.Weather.WindMs;

        var book = Book;
        for (int i = 0; i < book.Count; i++)
        {
            ref readonly var e = ref book[i];
            if (MagazineLook.AtS >= 0 && i != book.Count - 1)
                continue;
            double age = MagazineLook.AtS >= 0 ? MagazineLook.AtS : book.Clock - e.T0;
            double t = age - MagazineBlast.MainAtS, life = e.FireballS;
            if (t < 0 || t > MagazineLook.VolumeS)
                continue;
            var ev = e.Event;
            double deck = ev.DeckZ;
            var b = MagazineBlast.BodyAt(e.FireballM, life, ev.MassKg, deck, t);
            // downwind: the cap at its height's wind (the shear's power law), once it lifts
            double u = u10 * Math.Pow(Math.Max(b.CentreZ, 1) / 10, 0.14);
            var capDrift = windDir * (float)(u * Math.Max(t - 0.3 * life, 0) * 0.8);
            double r = b.R, rv = b.Rv;
            double stemR = Math.Max(MagazineLook.StemShare * r, 0.12 * e.FireballM);
            double fires = Math.Exp(-t / MagazineLook.StemFadeS);
            double stemSigma = MagazineLook.StemTau / (2 * stemR) * fires * SmoothStep(0.2 * life, 0.6 * life, t);
            double sigma = MagazineLook.Opacity * b.Tau / (2 * r);
            double fade = (1 - SmoothStep(0.67 * MagazineLook.VolumeS, MagazineLook.VolumeS, t)) * world;
            double capShare = SmoothStep(0.6 * life, 1.5 * life, t);
            double spin = t > life ? 2 * Math.PI * MagazineLook.Spin * 2 * life * (Math.Sqrt(t / life) - 1) : 0;
            // the fire's light on its own smoke: the core's drawn brightness, compressed as the flashes' halo's source
            double kel = MagazineLook.ColdK + (MagazineLook.HotK - MagazineLook.ColdK) * Math.Max(b.Heat, 0);
            double drawn = perCd * FlashPhotometry.Luminance(kel) * Math.Max(b.Heat, 0);
            double glow = 0.3 * drawn / (1 + drawn / FlashLook.HaloKnee);

            // the quads: the body and the stem through the effects camera; their shadow down-sun
            double zb = Math.Max(Math.Min(deck - 3, b.CentreZ - 1.35 * rv), 0), zt = b.CentreZ + 1.35 * rv;
            double rr = 1.4 * r, rs = 1.4 * stemR;
            var c = new Vector2(capDrift.X, capDrift.Y);
            float Dn(double h) => (float)(k * (h >= 0 ? Effects.Drawn(h) : h));
            var lo = Vector2.Min(c - new Vector2((float)rr) + new Vector2(0, Dn(b.CentreZ - 1.35 * rv - deck)), new Vector2((float)-rs) + new Vector2(0, Dn(zb - deck)));
            var hi = Vector2.Max(c + new Vector2((float)rr) + new Vector2(0, Dn(zt - deck)), new Vector2((float)rs) + new Vector2(0, Dn(Math.Max(b.CentreZ - deck, 0))));
            lo = Vector2.Min(lo, c - new Vector2((float)rs));
            hi = Vector2.Max(hi, c + new Vector2((float)rs));
            Vector4 shade = default;
            if (shadows)
            {
                var sLo = Vector2.Min(c + drift * (float)zb, c + drift * (float)zt) - new Vector2((float)rr);
                var sHi = Vector2.Max(c + drift * (float)zb, c + drift * (float)zt) + new Vector2((float)rr);
                sLo = Vector2.Min(sLo, Vector2.Min(drift * (float)deck, c + drift * (float)b.CentreZ) - new Vector2((float)rs));
                sHi = Vector2.Max(sHi, Vector2.Max(drift * (float)deck, c + drift * (float)b.CentreZ) + new Vector2((float)rs));
                shade = new Vector4(sLo, sHi.X, sHi.Y);
            }

            if (!view.Project(e.At, out double x, out double y))
                continue;
            double reach = Math.Max(Math.Max(Math.Abs(lo.X), Math.Abs(lo.Y)), Math.Max(Math.Abs(hi.X), Math.Abs(hi.Y)));
            if (shadows)
                reach = Math.Max(reach, Math.Max(Math.Max(Math.Abs(shade.X), Math.Abs(shade.Y)), Math.Max(Math.Abs(shade.Z), Math.Abs(shade.W))));
            double reachPx = reach / mpp + 8;
            if (x < -reachPx || x > view.Width + reachPx || y < -reachPx || y > view.Height + reachPx)
                continue;

            var rel = e.At - view.Eye;
            if (count == items.Length)
                Array.Resize(ref items, count * 2);
            items[count++] = new MagazineGpu
            {
                Base = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, e.Seed),
                Quad = new Vector4(lo, hi.X, hi.Y),
                Shade = shade,
                Body = new Vector4(c, (float)b.CentreZ, (float)r),
                BodyB = new Vector4((float)rv, (float)sigma, (float)b.Heat, (float)(b.Skin * MagazineLook.Skin * 0.5)),
                Stem = new Vector4(c, (float)stemR, (float)stemSigma),
                Time = new Vector4((float)(0.5 * fires), (float)capShare, (float)spin, (float)fade),
                Misc = new Vector4((float)deck, (float)glow, 0, 0),
            };
        }
    }

    static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    // ------------------------------------------------------------------ the GPU side

    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        uploaded = Math.Min(count, (int)(BufferBytes / MagazineGpu.SizeInBytes));
        if (uploaded > 0)
            buf.Upload(cmd, items.AsSpan(0, uploaded));
    }

    void EnsurePipelines(RenderTarget target)
    {
        if (bodyPipe != null && pipeFormat == target.ColorFormat && pipeSamples == target.SampleCount && pipeDepth == target.HasDepth)
            return;
        Release();
        (pipeFormat, pipeSamples, pipeDepth) = (target.ColorFormat, target.SampleCount, target.HasDepth);
        GpuPipelineBuilder B()
        {
            var b = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
                .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingPremultiplied().ClearVertexLayout();
            return target.HasDepth
                ? b.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
                : b.NoDepthTarget().DisableDepthTest();
        }
        bodyPipe = B().SetShaders(vs, fsBody).Build(device);
        shadowPipe = B().SetShaders(vs, fsShadow).Build(device);
        if (bodyPipe == null || shadowPipe == null)
            throw new InvalidOperationException($"magazine pipelines: {SDL_GetError()}");
    }

    void Release()
    {
        if (bodyPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, bodyPipe);
        if (shadowPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, shadowPipe);
        bodyPipe = shadowPipe = null;
    }

    void Pass(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, SDL_GPUGraphicsPipeline* pipe, in MapView view, int mode)
    {
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
            Depth = new Vector4((float)near, (float)far, 0, mode),
            East = new Vector4(F(view.Right), 0),
            North = new Vector4(F(view.Up), 0),
        };
        SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&vp), (uint)sizeof(ViewParams));
        var lu = lights.Uniforms;
        var pr = prms;
        SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&lu), (uint)sizeof(LightUniforms));
        SDL_PushGPUFragmentUniformData(cmd, 1, (nint)(&pr), (uint)sizeof(Params));
        SDL_DrawGPUPrimitives(pass, 6, (uint)uploaded, 0, 0);
    }

    /// <summary>The shadows, then the bodies, after the ships and the gun smoke.</summary>
    public void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0)
            return;
        EnsurePipelines(target);
        if (shadows)
            Pass(cmd, pass, shadowPipe, view, 1);
        if ((MagazineLook.Parts & 1) != 0)
            Pass(cmd, pass, bodyPipe, view, 0);
    }

    static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);

    public void Dispose()
    {
        SDL_WaitForGPUIdle(device);
        Release();
        buf.Dispose();
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fsBody);
        SDL_ReleaseGPUShader(device, fsShadow);
    }
}
