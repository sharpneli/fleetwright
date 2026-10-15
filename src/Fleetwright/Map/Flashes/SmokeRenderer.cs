using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Sim.Core;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map.Flashes;

/// <summary>One puff as gunsmoke.vert/.frag read it (std430: 5 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SmokeGpu
{
    /// <summary>xyz: the puff's centre over the sea at its anchor height, metres from the eye; w: seed.</summary>
    public Vector4 Centre;
    /// <summary>x: σ_h, m; y: its extinction area, m² (the world's share in); z: how far up the screen it's drawn (the
    /// effects camera: k × its height over its anchor), m; w: the quad's half size, m.</summary>
    public Vector4 Shape;
    /// <summary>x: its height over its anchor, m; y: its age, s; z: how far its tint has gone to white, 0-1; w: the water
    /// fog's share of it.</summary>
    public Vector4 Light;
    /// <summary>rgb: its tint near the muzzle, linear; w: the bore's way east (0 for a merged puff: no stretch).</summary>
    public Vector4 Tint;
    /// <summary>xy: its shadow's offset on the sea (east, north), m; z: the shadow's opacity (0: none); w: the bore's way
    /// north.</summary>
    public Vector4 Shadow;
    /// <summary>The gas's stem while the jet is spent (an effect, not the research's): xy from the puff's drawn centre
    /// back to the muzzle, m; z: its strength (0: none).</summary>
    public Vector4 Stem;
    public const int SizeInBytes = 96;
}

/// <summary>The world view's gun smoke (docs/visuals/muzzle-flashes.md F6): the smoke book's puffs as instances, built on
/// the CPU each frame (placed eye-relative in double, sorted north to south), and two instanced draws after the ships:
/// their shadows on the sea and the decks, then the puffs through the effects camera (anchored at the height they were
/// born at, so they leave the guns without a jump and climb up the screen as they rise). Each puff's alpha is
/// 1 − e^(−τ) of its Gaussian's optical depth (the research's, so a later smoke system's sight lines would see what is
/// drawn), its outline billowed by noise, lit by the sun, the moon and the flashes' lights with a two-tap self-shadow.
/// Display only; nothing allocates once its arrays have grown.</summary>
public sealed unsafe class SmokeRenderer : IDisposable
{
    const uint BufferBytes = 1 << 20;
    /// <summary>A puff is drawn out to where its optical depth falls to this (5% opaque).</summary>
    const double EdgeTau = 0.05;
    /// <summary>The longest shadow, metres per metre of height (a sun at ~7°), as the splashes'.</summary>
    const double MaxDrift = 8;

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fs;
    readonly DynamicGpuBuffer buf;
    SDL_GPUGraphicsPipeline* pipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    int uploaded;
    bool shadows;

    SmokeGpu[] items = new SmokeGpu[256];
    float[] keys = new float[256];
    int count;
    ShipLights lights = ShipLights.Flat;
    Params prms;

    public SmokeBook Book { get; } = new();

    public ReadOnlySpan<SmokeGpu> Items => items.AsSpan(0, count);

    struct ViewParams
    {
        public Vector4 Right, Up, Fwd, Depth, East, North;
    }

    struct Params
    {
        /// <summary>xyz: toward the sun, in east, north, up; w: metres per pixel.</summary>
        public Vector4 SunL;
        /// <summary>xyz: toward the moon.</summary>
        public Vector4 MoonL;
        /// <summary>The look's knobs: x the opacity cap; y the youth's time, s; z the stretch; w the billows.</summary>
        public Vector4 Look;
        /// <summary>x: the churn; y: the warp.</summary>
        public Vector4 Motion;
    }

    public SmokeRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/gunsmoke.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fs = GpuShader.Load(device, "Content/Shaders/Compiled/gunsmoke.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 2);
        if (vs == null || fs == null)
            throw new InvalidOperationException("gun smoke shaders failed to load");
        buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, BufferBytes);
    }

    /// <summary>A frame: the book updated from the shot book's new shots, then the puffs in view as instances.</summary>
    public void Build(in MapView view, ShotBook shots, WeatherHere weather, double world, ShipLights frameLights)
    {
        Book.Update(shots, shots.SeaClock, weather.WindFromDeg, weather.WindMs);
        lights = frameLights;
        count = 0;
        if (world <= 0 || !FlashLook.On || (FlashLook.Parts & 8) == 0)
            return;
        var up = (-view.Forward).Normalized();
        var (east, north) = (view.Right, view.Up);
        Vector3 Local(Vec3d d) => new((float)Vec3d.Dot(d, east), (float)Vec3d.Dot(d, north), (float)Vec3d.Dot(d, up));
        var sunL = Local(lights.SunDir);
        var l = Local(lights.ShadowDir);
        double sinLo = Math.Sin(3 * Math.PI / 180), sinHi = Math.Sin(7 * Math.PI / 180);
        float shadowOn = (float)(lights.ShadowOpacity * SmoothStep(sinLo, sinHi, l.Z));
        var drift = l.Z > 1e-3 ? -new Vector2(l.X, l.Y) / l.Z : Vector2.Zero;
        if (drift.Length() > MaxDrift)
            drift *= (float)(MaxDrift / drift.Length());
        shadows = shadowOn > 0.002f;
        double mpp = view.MetresPerPixel, k = Effects.K, now = shots.SeaClock;
        prms = new Params
        {
            SunL = new Vector4(sunL, (float)mpp), MoonL = new Vector4(Local(lights.MoonDir), 0),
            Look = new Vector4((float)FlashLook.SmokeCap, (float)FlashLook.SmokeYouthS, (float)FlashLook.SmokeStretch, (float)FlashLook.SmokeBillow),
            Motion = new Vector4((float)FlashLook.SmokeChurn, (float)FlashLook.SmokeWarp, 0, 0),
        };

        for (int i = 0; i < Book.Count; i++)
        {
            Book.State(i, now, out var centre, out double z, out double sigma, out double area, out double age);
            ref readonly var p = ref Book[i];
            double peak = area * world / (2 * Math.PI * sigma * sigma);
            if (peak < EdgeTau)
                continue;
            // the bore's way on the screen, and the young puff's stretch along it (the shader's, for the quad)
            var bore = new Vector2((float)Vec3d.Dot(p.Bore, east), (float)Vec3d.Dot(p.Bore, north));
            bore = bore.LengthSquared() > 1e-6f ? Vector2.Normalize(bore) : Vector2.Zero;
            double stretch = 1 + FlashLook.SmokeStretch * Math.Exp(-age / FlashLook.SmokeYouthS) * bore.Length();
            double half = 1.15 * sigma * Math.Sqrt(2 * Math.Log(peak / EdgeTau)) * Math.Sqrt(stretch);
            half = Math.Max(half, sigma);
            var at = centre.Normalized() * (Earth.Radius + p.Anchor);
            if (!view.Project(at, out double x, out double y))
                continue;
            double dz = z - p.Anchor, lift = k * dz;
            var shadowOff = drift * (float)z;
            // the stem back to the muzzle (drawn: the muzzle at the anchor's height, the puff lifted), while the jet runs
            double jet = p.JetS > 0 ? 1 - Math.Exp(-age / p.JetS) : 1, along = p.Jet0 + p.Jet * jet;
            var stem = -bore * (float)along - new Vector2(0, (float)lift);
            float stemOn = bore.LengthSquared() > 0 ? (float)Math.Exp(-age / FlashLook.SmokeStemS) : 0;
            if (stemOn > 0.01f)
                half = Math.Max(half, stem.Length() + 1.5 * sigma);
            double reach = (half + Math.Max(lift, shadowOff.Length())) / mpp;
            if (x < -reach || x > view.Width + reach || y < -reach || y > view.Height + reach)
                continue;
            var rel = at - view.Eye;
            if (count == items.Length)
            {
                Array.Resize(ref items, count * 2);
                Array.Resize(ref keys, count * 2);
            }
            double fog = area > 0 ? Math.Min(p.Aw0 * Math.Exp(-age / p.FogS) / Math.Max(area, 1e-6), 1) : 0;
            items[count] = new SmokeGpu
            {
                Centre = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, p.Seed),
                Shape = new Vector4((float)sigma, (float)(area * world), (float)lift, (float)half),
                Light = new Vector4((float)dz, (float)age, p.Keep ? 0 : (float)(1 - Math.Exp(-age / 5)), (float)fog),
                Tint = new Vector4(p.Tint, bore.X),
                // its shadow only where it falls clear of the puff (under a high sun it lies beneath it: half the fragments)
                Shadow = new Vector4(shadowOff, shadowOff.Length() > 0.5 * sigma ? shadowOn : 0, bore.Y),
                Stem = new Vector4(stem, stemOn > 0.01f ? stemOn : 0, 0),
            };
            keys[count] = -(float)(Vec3d.Dot(rel, north) + lift);   // north first: the effects camera looks north
            count++;
        }
        Array.Sort(keys, items, 0, count);
    }

    static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        uploaded = Math.Min(count, (int)(BufferBytes / SmokeGpu.SizeInBytes));
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
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingPremultiplied().ClearVertexLayout().SetShaders(vs, fs);
        b = target.HasDepth
            ? b.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
            : b.NoDepthTarget().DisableDepthTest();
        pipe = b.Build(device);
        if (pipe == null)
            throw new InvalidOperationException($"gun smoke pipeline: {SDL_GetError()}");
    }

    /// <summary>The shadows, then the puffs, after the ships.</summary>
    public void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0)
            return;
        EnsurePipeline(target);
        SDL_BindGPUGraphicsPipeline(pass, pipe);
        var sb = buf.Buffer;
        SDL_BindGPUVertexStorageBuffers(pass, 0, &sb, 1);
        SDL_BindGPUFragmentStorageBuffers(pass, 0, &sb, 1);
        var lu = lights.Uniforms;
        var pr = prms;
        SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&lu), (uint)sizeof(LightUniforms));
        SDL_PushGPUFragmentUniformData(cmd, 1, (nint)(&pr), (uint)sizeof(Params));
        var (near, far) = GlobeRenderer.DepthRange(view);
        for (int mode = shadows ? 1 : 0; mode >= 0; mode--)
        {
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
            SDL_DrawGPUPrimitives(pass, 6, (uint)uploaded, 0, 0);
        }
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
