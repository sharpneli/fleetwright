using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Map.Splashes;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map.Flashes;

/// <summary>One mount's flash as muzzleflash.vert/.frag read it (std430: 11 vec4s). Offsets are metres east and north
/// of the barrel tips' middle, through the effects camera (height above the tips drawn k up the screen).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct FlashGpu
{
    /// <summary>xyz: the barrel tips' middle, metres from the eye; w: seed.</summary>
    public Vector4 Base;
    /// <summary>xy: the bore's way (east, north), unit; z: barrels; w: their spacing, m.</summary>
    public Vector4 Bore;
    /// <summary>xy: the quad's centre (offset); z: its half size, m; w: unused.</summary>
    public Vector4 Quad;
    /// <summary>Per part, A: xy its centre (offset), z σ along the bore, w σ across, m; B: rgb its colour (linear, unit
    /// luminance), w its brightness, display units × m² (a barrel's for the core and the disc).</summary>
    public Vector4 CoreA, CoreB, DiscA, DiscB, FireA, FireB;
    /// <summary>xy: the halo's centre (offset); z: its source's σ, m; w: its compressed brightness, display units × m².</summary>
    public Vector4 Halo;
    /// <summary>rgb: the halo's colour; w: the fireball's life gone, 0-1.</summary>
    public Vector4 HaloColour;
    public const int SizeInBytes = 176;
}

/// <summary>The world view's muzzle flashes (docs/visuals/muzzle-flashes.md F2): the book's shots glowing now, built
/// on the CPU each frame and drawn in one instanced, screen-blended draw after the splash columns.
/// <list type="bullet">
/// <item>Each part (<see cref="FlashEmitters"/>) is averaged over the frame and splatted as a Gaussian with its
/// integral fixed (σ at least <see cref="MinSigmaPx"/>), so a flash keeps its energy from a sprite down to a point.</item>
/// <item>Its brightness is its luminous intensity over a lit white deck's luminance (<see cref="FlashPhotometry"/>), laid
/// onto the display's level for that deck (the frame's lights): no HDR target.</item>
/// <item>The halo stands in for a bloom pass: the parts again, widened by 1.5, 6 and 18 px, on a source compressed
/// as x/(1 + x/knee), whose integral over a Gaussian is closed-form.</item>
/// </list>
/// It reads the snapshot's shots and the frame's lights, never the world; nothing allocates once its arrays have grown.</summary>
public sealed unsafe class FlashRenderer : IDisposable
{
    const uint BufferBytes = 1 << 20;   // ~6,000 flashes
    /// <summary>The narrowest a part is splatted, px: under it the energy is kept, not the size.</summary>
    public const double MinSigmaPx = 0.6;
    /// <summary>The halo's radii, px, and their shares (the research's bloom).</summary>
    static readonly (double Px, double Share)[] HaloRadii = [(1.5, 0.2), (6, 0.1), (18, 0.05)];

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fs;
    readonly DynamicGpuBuffer buf;
    SDL_GPUGraphicsPipeline* pipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    int uploaded;

    FlashGpu[] items = new FlashGpu[64];
    int count;
    ShipLights lights = ShipLights.Flat;
    Params prms;

    public ShotBook Book { get; } = new();

    /// <summary>The instances this frame (tests, stats).</summary>
    public ReadOnlySpan<FlashGpu> Items => items.AsSpan(0, count);

    /// <summary>Display units per cd/m² this frame: the display's white deck over the real one's, times the gain.</summary>
    public double Scale { get; private set; }

    struct ViewParams
    {
        public Vector4 Right, Up, Fwd, Depth, East, North;
    }

    struct Params
    {
        /// <summary>x: metres per pixel; y: the halo's scale; z: its gain; w: the compression's knee.</summary>
        public Vector4 Misc;
    }

    public FlashRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/muzzleflash.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fs = GpuShader.Load(device, "Content/Shaders/Compiled/muzzleflash.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 2);
        if (vs == null || fs == null)
            throw new InvalidOperationException("muzzle flash shaders failed to load");
        buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, BufferBytes);
    }

    // ------------------------------------------------------------------ the CPU side

    /// <summary>A frame: the book updated from the snapshot, then the flashes glowing in view as instances.
    /// <paramref name="speed"/> is the clock's (0 paused), <paramref name="dtS"/> the frame's real seconds,
    /// <paramref name="world"/> the view's <see cref="WorldView.Factor(in MapView)"/>.</summary>
    public void Build(in MapView view, Snapshot s, long drawMs, double speed, double dtS, double world, ShipLights frameLights,
        Func<int, ShipClass?> classOf)
    {
        double before = Book.FlashClock;
        Book.Update(s.Shots, drawMs, speed, dtS, classOf);
        double frame = Book.FlashClock - before;
        lights = frameLights;
        count = 0;
        if (world <= 0 || !FlashLook.On)
            return;

        var up = (-view.Forward).Normalized();
        var (east, north) = (view.Right, view.Up);
        Scale = FlashLook.Gain * DisplayWhite(lights, up) / FlashPhotometry.WhiteDeck(Illuminance(s.Sun, up, drawMs, lights));
        double mpp = view.MetresPerPixel, k = SplashLook.EffectK, floor = MinSigmaPx * mpp;
        prms = new Params { Misc = new Vector4((float)mpp, (float)FlashLook.HaloScale, (float)FlashLook.HaloGain, (float)FlashLook.HaloKnee) };
        double haloOut = HaloRadii[^1].Px * FlashLook.HaloScale * mpp;

        var book = Book;
        double clock = book.FlashClock;
        long newest = book.Count > 0 ? book[book.Count - 1].Ms : 0;
        for (int i = 0; i < book.Count; i++)
        {
            ref readonly var e = ref book[i];
            if (FlashLook.AtS >= 0 && e.Ms < newest - 2000)
                continue;   // a still: the newest salvos only, not every one kept
            double t = FlashLook.AtS >= 0 ? FlashLook.AtS : clock - e.FlashT0;
            if (FlashLook.AtS >= 0)
                frame = 0;
            if (t < 0 || t - frame > FlashEmitters.Life(e))
                continue;
            if (!view.Project(e.Tip, out double x, out double y))
                continue;
            double reachPx = (e.MachDiskM + 2 * e.FlashLengthM + 3 * haloOut) / mpp + 8;
            if (x < -reachPx || x > view.Width + reachPx || y < -reachPx || y > view.Height + reachPx)
                continue;
            FlashEmitters.Averaged(e, t, frame, out var core, out var disc, out var fire);
            if (core.Intensity <= 0 && disc.Intensity <= 0 && fire.Intensity <= 0)
                continue;

            // the bore over the sea in the view's axes, and how each metre along it is drawn: cos(el) along, sin(el) up
            var dir = new Vector2((float)Vec3d.Dot(e.Forward, east), (float)Vec3d.Dot(e.Forward, north));
            dir = dir.LengthSquared() > 1e-12f ? Vector2.Normalize(dir) : Vector2.UnitY;
            double el = e.ElevationDeg * Math.PI / 180, ce = Math.Cos(el), se = Math.Sin(el);
            Vector2 At(in FlashEmitters.Part p) => dir * (float)(p.Along * ce) + new Vector2(0, (float)(k * (p.Along * se + p.Lift)));
            float scale = (float)(Scale * world);
            int n = e.Barrels;

            var cA = Splat(At(core), core, ce, floor, out double cs);
            var dA = Splat(At(disc), disc, ce, floor, out double ds);
            var fA = Splat(At(fire), fire, ce, floor, out double fs2);
            float qc = scale * core.Intensity, qd = scale * disc.Intensity, qf = scale * fire.Intensity;
            // the halo's source: every part's brightness compressed (closed-form over a Gaussian), at their centroid
            double knee = FlashLook.HaloKnee;
            double hc = n * Compressed(qc, cs, knee), hd = n * Compressed(qd, ds, knee), hf = Compressed(qf, fs2, knee), h = hc + hd + hf;
            if (h <= 0)
                continue;
            var cc = FlashEmitters.Colour(core, false);
            var dc = FlashEmitters.Colour(disc, false);
            var fc = FlashEmitters.Colour(fire, true);
            var centre = (new Vector2(cA.X, cA.Y) * (float)hc + new Vector2(dA.X, dA.Y) * (float)hd + new Vector2(fA.X, fA.Y) * (float)hf) / (float)h;
            double sigma = Math.Sqrt((hc * cs + hd * ds + hf * fs2) / h);
            var hcol = FlashPhotometry.UnitLuminance((cc * (float)hc + dc * (float)hd + fc * (float)hf) / (float)h);

            // the quad: the parts (and the outer barrels) and the halo's 3 sigma round its centre
            double half = 0, across = (n - 1) * e.SpacingM / 2;
            // (the fireball's outline is pushed out by its lobes and noise, up to ~1.6 times)
            foreach (var (a, reach) in (ReadOnlySpan<(Vector4, double)>)[(cA, 3.2), (dA, 3.2), (fA, 3.2 * 1.6)])
                if (a.Z > 0)
                    half = Math.Max(half, (new Vector2(a.X, a.Y) - centre).Length() + reach * Math.Max(a.Z, a.W) + across);
            half = Math.Max(half, 3.2 * Math.Sqrt(sigma * sigma + haloOut * haloOut));

            var rel = e.Tip - view.Eye;
            if (count == items.Length)
                Array.Resize(ref items, count * 2);
            items[count++] = new FlashGpu
            {
                Base = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, e.Seed),
                Bore = new Vector4(dir, n, e.SpacingM),
                Quad = new Vector4(centre, (float)half, 0),
                CoreA = cA, CoreB = new Vector4(cc, qc),
                DiscA = dA, DiscB = new Vector4(dc, qd),
                FireA = fA, FireB = new Vector4(fc, qf),
                Halo = new Vector4(centre, (float)sigma, (float)h),
                HaloColour = new Vector4(hcol, fire.Life),
            };
        }
    }

    /// <summary>A part as a Gaussian at its drawn centre: σ along the bore its semi-axis × cos(el) / 1.5, across its
    /// semi-axis / 1.5 (the reference renderer's), neither under the floor; <paramref name="area"/> σ_a·σ_b.</summary>
    static Vector4 Splat(Vector2 at, in FlashEmitters.Part p, double cosEl, double floor, out double area)
    {
        if (p.Intensity <= 0)
        {
            area = 0;
            return Vector4.Zero;
        }
        double sa = Math.Max(p.A * cosEl / 1.5, floor), sb = Math.Max(p.B / 1.5, floor);
        area = sa * sb;
        return new Vector4(at, (float)sa, (float)sb);
    }

    /// <summary>The integral of x/(1 + x/knee) over a Gaussian of brightness q (display units × m²) and σ_a·σ_b
    /// <paramref name="area"/>: 2π σ_a σ_b · knee · ln(1 + peak/knee), q itself while the peak is under the knee.</summary>
    public static double Compressed(double q, double area, double knee)
    {
        if (q <= 0 || area <= 0)
            return 0;
        double a = 2 * Math.PI * area, peak = q / a;
        return a * knee * Math.Log(1 + peak / knee);
    }

    /// <summary>The display's level for a lit level white deck, linear: what the frame's lights put on it (the ships'
    /// shading's sun and ambient).</summary>
    public static double DisplayWhite(in ShipLights l, Vec3d up)
    {
        var u = l.Uniforms;
        double sun = FlashPhotometry.Lum(new Vector3(u.SunRad.X, u.SunRad.Y, u.SunRad.Z)) * Math.Max(Vec3d.Dot(l.SunDir, up), 0);
        double moon = FlashPhotometry.Lum(new Vector3(u.MoonRad.X, u.MoonRad.Y, u.MoonRad.Z)) * Math.Max(Vec3d.Dot(l.MoonDir, up), 0);
        return Math.Max(sun + moon + FlashPhotometry.Lum(new Vector3(u.Sky.X, u.Sky.Y, u.Sky.Z)), 1e-4);
    }

    /// <summary>The real illuminance on a level surface at the view's centre, lux.</summary>
    static double Illuminance(Vec3d sun, Vec3d up, long ms, in ShipLights l) =>
        FlashPhotometry.Illuminance(Vec3d.Dot(sun, up), Moon.Light(up * Earth.Radius, new WorldTime(ms)), Vec3d.Dot(l.MoonDir, up));

    // ------------------------------------------------------------------ the GPU side

    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        uploaded = Math.Min(count, (int)(BufferBytes / FlashGpu.SizeInBytes));
        if (uploaded > 0)
            buf.Upload(cmd, items.AsSpan(0, uploaded));
    }

    void EnsurePipeline(RenderTarget target)
    {
        if (pipe != null && pipeFormat == target.ColorFormat && pipeSamples == target.SampleCount && pipeDepth == target.HasDepth)
            return;
        if (pipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, pipe);
        (pipeFormat, pipeSamples, pipeDepth) = (target.ColorFormat, target.SampleCount, target.HasDepth);
        var b = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingScreen().ClearVertexLayout().SetShaders(vs, fs);
        b = target.HasDepth
            ? b.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
            : b.NoDepthTarget().DisableDepthTest();
        pipe = b.Build(device);
        if (pipe == null)
            throw new InvalidOperationException($"muzzle flash pipeline: {SDL_GetError()}");
    }

    /// <summary>The flashes, last in the ships' layer.</summary>
    public void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0 || (FlashLook.Parts & 1) == 0)
            return;
        EnsurePipeline(target);
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
