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

/// <summary>One flame jet (or the hit's flash) as magazine_jet.vert/.frag read it (std430: 5 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct MagazineJetGpu
{
    public Vector4 Base, Seg, Shape, Look, Quad;
    public const int SizeInBytes = 80;
}

/// <summary>The world view's magazine explosions (docs/visuals/magazine-explosions.md): the book's events in view as
/// instances, their state built on the CPU each frame (<see cref="MagazineBlast.BodyAt"/>, the wind's drift and shear,
/// the stem's fires, the fade), placed eye-relative in double, and two instanced draws over them, from the one model in
/// <c>Include/magazine.glsl</c>:
/// <list type="bullet">
/// <item>the shadows on the sea and the decks, then the bodies (fireball, cap, stem) through the effects camera, after
/// the ships and the gun smoke, then the flame jets from the failed openings before the fireball and the hit's flash,
/// screen-blended (<see cref="Draw"/>).</item>
/// </list>
/// It reads the snapshot's events and the frame's lights, never the world; nothing allocates once its arrays have grown.</summary>
public sealed unsafe class MagazineRenderer : IDisposable
{
    const uint BufferBytes = 64 * 1024;   // 512 events
    const uint JetBufferBytes = 256 * 1024;   // ~3,000 jets
    /// <summary>The hit's flash: its radius, m, and life, s (the shell bursting on the gunhouse or the deck over the
    /// magazine: a flash and a dark puff before the flash reaches the charges).</summary>
    const double HitFlashM = 8, HitFlashS = 0.35;
    /// <summary>The longest shadow, metres per metre of height (a sun at ~18°: a 1 km cap's shadow is already 3 km).</summary>
    const float MaxDrift = 3;

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fsBody, fsShadow, vsJet, fsJet;
    readonly DynamicGpuBuffer buf, jetBuf;
    SDL_GPUGraphicsPipeline* bodyPipe, shadowPipe, jetPipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    int uploaded, jetsUploaded;

    MagazineGpu[] items = new MagazineGpu[8];
    MagazineJetGpu[] jets = new MagazineJetGpu[32];
    int count, jetCount;
    ShipLights lights = ShipLights.Flat;
    Params prms;
    bool shadows;

    public MagazineBook Book { get; } = new();

    /// <summary>The instances this frame (tests, stats).</summary>
    public ReadOnlySpan<MagazineGpu> Items => items.AsSpan(0, count);
    public ReadOnlySpan<MagazineJetGpu> Jets => jets.AsSpan(0, jetCount);

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
        vsJet = GpuShader.Load(device, "Content/Shaders/Compiled/magazine_jet.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fsJet = GpuShader.Load(device, "Content/Shaders/Compiled/magazine_jet.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 2);
        if (vs == null || fsBody == null || fsShadow == null || vsJet == null || fsJet == null)
            throw new InvalidOperationException("magazine shaders failed to load");
        buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, BufferBytes);
        jetBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, JetBufferBytes);
    }

    // ------------------------------------------------------------------ the CPU side

    /// <summary>A frame: the book updated from the snapshot, then the events in view as instances. <paramref name="speed"/>
    /// is the clock's (0 paused), <paramref name="dtS"/> the frame's real seconds, <paramref name="world"/> the view's
    /// <see cref="WorldView.Factor(in MapView)"/>.</summary>
    public void Build(in MapView view, Snapshot s, long drawMs, double speed, double dtS, double world, ShipLights frameLights,
        Func<int, ShipClass?> classOf)
    {
        Book.Update(s.Blasts, drawMs, speed, dtS, classOf, s.Ships);
        lights = frameLights;
        count = jetCount = 0;
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
            var ev = e.Event;
            double deck = ev.DeckZ;
            if (age >= 0 && t < 2.5 && (MagazineLook.Parts & 4) != 0)
                AddJets(view, e, age, world, mpp, k);
            if (t < 0 || t > MagazineLook.VolumeS)
                continue;
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

    /// <summary>An event's jets now, <paramref name="age"/> s after the hit (research §2.5): each opening's flame from its
    /// failing on, its length by the magazine's pressure, the gun ports' along the guns as they were trained, fading as
    /// the fireball swallows them; and the hit's flash at its first moment. Placed in the event's draw plane through the
    /// effects camera (heights over the deck up the screen).</summary>
    void AddJets(in MapView view, in MagazineBook.Entry e, double age, double world, double mpp, double k)
    {
        var ev = e.Event;
        var (east, north) = (view.Right, view.Up);
        var f = new Vector2((float)Vec3d.Dot(e.Forward, east), (float)Vec3d.Dot(e.Forward, north));
        var sb = new Vector2((float)Vec3d.Dot(e.Starboard, east), (float)Vec3d.Dot(e.Starboard, north));
        Vector2 Plan(double x, double y) => f * (float)(x - ev.X) + sb * (float)(y - ev.Y);
        Vector2 Way(double x, double y) => f * (float)x + sb * (float)y;
        double deck = ev.DeckZ;
        float Dn(double z) => (float)(k * (z - deck >= 0 ? Effects.Drawn(z - deck) : z - deck));
        if (!view.Project(e.At, out double px, out double py) || px < -4000 / mpp || py < -4000 / mpp || px > view.Width + 4000 / mpp ||
            py > view.Height + 4000 / mpp)
            return;
        var rel = e.At - view.Eye;
        var baseV = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, e.Seed);
        double main = MagazineBlast.MainAtS, fadeOut = 1 - SmoothStep(main, main + 0.5, age);

        // the hit's flash
        if (age < HitFlashS)
        {
            double u = age / HitFlashS;
            var at = Plan(e.Hit.X, e.Hit.Y) + new Vector2(0, Dn(e.Hit.Z));
            float r = (float)(Math.Max(HitFlashM * (0.6 + 0.6 * u), 2 * mpp));
            Add(new MagazineJetGpu
            {
                Base = baseV, Seg = new Vector4(at, at.X, at.Y), Shape = new Vector4(r, r, (float)(1 - 0.4 * u), (float)(1 - 0.4 * u)),
                Look = new Vector4((float)((1 - u * u) * world), (float)age, 0, 0), Quad = new Vector4(at - new Vector2(2 * r), at.X + 2 * r, at.Y + 2 * r),
            });
        }
        if (fadeOut <= 0)
            return;
        foreach (ref readonly var o in ev.Openings)
        {
            double len = MagazineBlast.JetLength(o, age);
            if (len < 2)
                continue;
            Vector2 root, way;
            if (o.Kind == OpeningKind.Port && o.Mount >= 0)
            {
                // along the guns as they were trained: the port's place and way turned by the mount's bearing
                double b = e.Bearings[o.Mount] * Math.PI / 180, cb = Math.Cos(b), sbn = Math.Sin(b);
                var (mx, my) = (ev.MountX(o.Mount), ev.MountY(o.Mount));
                root = Plan(mx + o.At.X * cb - o.At.Y * sbn, my + o.At.X * sbn + o.At.Y * cb);
                way = Way(o.Dir.X * cb - o.Dir.Y * sbn, o.Dir.X * sbn + o.Dir.Y * cb);
            }
            else if (o.Mount >= 0)   // the hood: aft of the pivot on the trained roof
            {
                double b = e.Bearings[o.Mount] * Math.PI / 180;
                var (mx, my) = (ev.MountX(o.Mount), ev.MountY(o.Mount));
                root = Plan(mx + o.At.X * Math.Cos(b), my + o.At.X * Math.Sin(b));
                way = Way(o.Dir.X, o.Dir.Y);
            }
            else
            {
                root = Plan(o.At.X, o.At.Y);
                way = Way(o.Dir.X, o.Dir.Y);
            }
            double z0 = o.At.Z, z1 = o.At.Z + o.Dir.Z * len;
            var a = root + new Vector2(0, Dn(z0));
            var tip = root + way * (float)len + new Vector2(0, Dn(z1));
            float r0 = (float)Math.Max(0.45 * o.DiameterM, 0.8 * mpp), r1 = (float)Math.Max(0.45 * o.DiameterM + 0.06 * len, 1.2 * mpp);
            float pad = 1.6f * Math.Max(r0, r1);
            var lo = Vector2.Min(a, tip) - new Vector2(pad);
            var hi = Vector2.Max(a, tip) + new Vector2(pad);
            Add(new MagazineJetGpu
            {
                Base = baseV, Seg = new Vector4(a, tip.X, tip.Y), Shape = new Vector4(r0, r1, 1f, 0.7f),
                Look = new Vector4((float)(fadeOut * world), (float)age, (float)len, 0), Quad = new Vector4(lo, hi.X, hi.Y),
            });
        }
    }

    void Add(in MagazineJetGpu j)
    {
        if (jetCount == jets.Length)
            Array.Resize(ref jets, jetCount * 2);
        jets[jetCount++] = j;
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
        jetsUploaded = Math.Min(jetCount, (int)(JetBufferBytes / MagazineJetGpu.SizeInBytes));
        if (jetsUploaded > 0)
            jetBuf.Upload(cmd, jets.AsSpan(0, jetsUploaded));
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
        var jb = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingScreen().ClearVertexLayout().SetShaders(vsJet, fsJet);
        jetPipe = (target.HasDepth
            ? jb.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
            : jb.NoDepthTarget().DisableDepthTest()).Build(device);
        if (bodyPipe == null || shadowPipe == null || jetPipe == null)
            throw new InvalidOperationException($"magazine pipelines: {SDL_GetError()}");
    }

    void Release()
    {
        if (bodyPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, bodyPipe);
        if (shadowPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, shadowPipe);
        if (jetPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, jetPipe);
        bodyPipe = shadowPipe = jetPipe = null;
    }

    void Pass(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, SDL_GPUGraphicsPipeline* pipe, in MapView view, int mode,
        DynamicGpuBuffer? from = null, int n = -1)
    {
        SDL_BindGPUGraphicsPipeline(pass, pipe);
        var sb = (from ?? buf).Buffer;
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
        SDL_DrawGPUPrimitives(pass, 6, (uint)(n >= 0 ? n : uploaded), 0, 0);
    }

    /// <summary>The shadows, then the bodies, then the jets, after the ships and the gun smoke.</summary>
    public void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0 && jetsUploaded == 0)
            return;
        EnsurePipelines(target);
        if (uploaded > 0 && shadows)
            Pass(cmd, pass, shadowPipe, view, 1);
        if (uploaded > 0 && (MagazineLook.Parts & 1) != 0)
            Pass(cmd, pass, bodyPipe, view, 0);
        if (jetsUploaded > 0)
            Pass(cmd, pass, jetPipe, view, 0, jetBuf, jetsUploaded);
    }

    static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);

    public void Dispose()
    {
        SDL_WaitForGPUIdle(device);
        Release();
        buf.Dispose();
        jetBuf.Dispose();
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fsBody);
        SDL_ReleaseGPUShader(device, fsShadow);
        SDL_ReleaseGPUShader(device, vsJet);
        SDL_ReleaseGPUShader(device, fsJet);
    }
}
