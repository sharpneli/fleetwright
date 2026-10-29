using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Map.Flashes;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map.Night;

/// <summary>One flare's part as flare.vert reads it (std430: 6 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct FlareGpu
{
    /// <summary>Lo, Hi: the part's ends as drawn (on the sea's plane, lifted by the effects camera; metres from the eye),
    /// w the kind (0 the trail, 1 the star) and the trail's half width at the top. LoAt, HiAt: the same points where they
    /// are (for the lights), w the trail's half width at the bottom and its opacity (the star's: the air's scattering).
    /// Look: the star's core (display units), its glow in the haze (display units × m), its core's radius (m; the trail's
    /// half width at a fresh star) and the glow's reach (m). Colour: the star's.</summary>
    public Vector4 Lo, Hi, LoAt, HiAt, Look, Colour;
    public const int SizeInBytes = 96;
}

/// <summary>Star shell in the world view (docs/visuals/night-lights.md Q10d): each flare the sim fires, drawn through the
/// effects camera (its height up the screen, the pool it lights on the sea exact):
/// <list type="bullet">
/// <item><b>The star</b>: a blinding point (its core a little wider than the candle, never under a couple of pixels) and
/// its <b>glow in the haze</b>: the air's light scattered toward the eye round a point source, closed form along the
/// eye's ray past it (L = σ p I (π/2 + atan(h/d)) / d: the ray from above through the air down to the sea h under the
/// star; p the haze's at right angles, σ the sim's scattering), falling off with the air's
/// extinction; through the night eye, rolled to white on luminance.</item>
/// <item><b>Its life</b> (<see cref="Envelope"/>, shared with its light in <see cref="NightLights"/>): a pop as the star
/// lights, a few percent of flicker, and a burn-out over its last <see cref="OutS"/> that dims and reddens.</item>
/// <item><b>The trail</b>: the smoke it leaves as it falls, a column from where it burst down to it, drifting with the
/// wind as it does (so it hangs straight over it), widening with its age, lit by every light in the block (the star's
/// own from just under it) and by the night; it outlives the star by <see cref="TrailS"/>, its top the burst's puff.</item>
/// </list>
/// The world view's alone. A book of the flares seen keeps their trails after the snapshot drops them. Nothing allocates
/// once grown.</summary>
public sealed unsafe class FlareRenderer : IDisposable
{
    const uint BufferBytes = 96 << 9;   // 512 parts

    public static bool On = true;

    /// <summary>A gain on the star and its glow (1: physical, through the night eye).</summary>
    public static double Gain = 1, HaloGain = 1;

    /// <summary>How long the trail outlives its star, s (an effect fades within a minute), and its spread: a half width of
    /// <see cref="TrailM"/> at the star, growing by <see cref="TrailGrowMs"/> a second of its age [EST].</summary>
    public static double TrailS = 40, TrailM = 1.2, TrailGrowMs = 0.25;

    /// <summary>The star's lighting and its burn-out, s [EST: the record's "opened up like a parachute"; a candle dims as
    /// it goes].</summary>
    public const double PopS = 0.6, OutS = 1.5;

    /// <summary>The star's own size, m: the candle and the glare round it [EST].</summary>
    public const double CoreM = 0.6;

    /// <summary>The haze's phase function, per steradian, for the star's glow: a marine aerosol's (Henyey-Greenstein,
    /// g 0.7) at right angles to the light, the eye's ray passing the star from above. No sim number stands behind the
    /// glow, so the physical one; all round (1 / 4π, the beams' from the sim) would be 3.6 times brighter.</summary>
    public const double HazePhase = 0.0223;

    /// <summary>The night sea's light over a lit white deck's: where the glow's quad may end unseen [EST].</summary>
    const double SeaShare = 0.02;

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fs;
    readonly DynamicGpuBuffer buf;
    SDL_GPUGraphicsPipeline* pipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    FlareGpu[] items = new FlareGpu[16];
    int count, uploaded;
    ShipLights lights = ShipLights.Flat;

    // the book: every flare seen, kept until its trail has gone
    struct Seen
    {
        public Vec3d Origin, Drift;
        public long BurstMs, OutMs;
        public double Cd, FallMs;
    }
    Seen[] book = new Seen[16];
    int booked;

    [StructLayout(LayoutKind.Sequential)]
    struct ViewParams
    {
        public Vector4 Right, Up, Fwd, Depth;
    }

    public FlareRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/flare.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fs = GpuShader.Load(device, "Content/Shaders/Compiled/flare.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 1);
        if (vs == null || fs == null)
            throw new InvalidOperationException("flare shaders failed to load");
        buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, BufferBytes);
    }

    /// <summary>A star's light over its life, a share of its candle's, and how far it's gone red (0-1): a pop as it
    /// lights, a flicker of a few percent (by the world's time, the same at any speed), its last <see cref="OutS"/>
    /// dimming. <paramref name="seed"/> keeps each star's flicker its own.</summary>
    public static (double Light, double Red) Envelope(double ageS, double burnS, long seed)
    {
        if (ageS < 0 || ageS >= burnS)
            return (0, 1);
        double pop = 1 + 0.8 * Math.Exp(-ageS / (PopS / 3)) * Math.Min(ageS / 0.05, 1);
        double left = burnS - ageS, dying = left < OutS ? left / OutS : 1;
        double t = ageS * 11, i = Math.Floor(t), f = t - i;
        double n = Hash(seed, (long)i) + (Hash(seed, (long)i + 1) - Hash(seed, (long)i)) * f * f * (3 - 2 * f);
        double flicker = 1 + 0.08 * (n - 0.5);
        return (pop * flicker * dying * dying, 1 - dying);
    }

    static double Hash(long seed, long i)
    {
        ulong x = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL ^ (ulong)i * 0xBF58476D1CE4E5B9UL);
        x ^= x >> 31;
        x *= 0x94D049BB133111EBUL;
        x ^= x >> 29;
        return (x & 0xFFFFFF) / (double)0xFFFFFF;
    }

    /// <summary>A frame: the book brought up to the snapshot's flares, then each one's trail and burning star as parts.</summary>
    public void Build(in MapView view, Snapshot s, long drawMs, double world, ShipLights frameLights, double visibilityM, SimSettings settings)
    {
        count = 0;
        lights = frameLights;
        var f = s.Flares;
        for (int i = 0; i < f.Count; i++)
            Book(f, i);
        int kept = 0;
        for (int i = 0; i < booked; i++)
            if (drawMs < book[i].OutMs + 1000 * TrailS && drawMs >= book[i].BurstMs - 60_000)
                book[kept++] = book[i];
        booked = kept;
        if (!On || world <= 0 || booked == 0)
            return;
        var up = (-view.Forward).Normalized();
        double perCd = FlashRenderer.PerCandela(frameLights, up), k = Effects.K;
        double scatter = SearchlightModel.ScatterPerM(visibilityM, settings);
        double mpp = view.MetresPerPixel, reach = (view.Width + view.Height) * mpp + 3000;
        var centre = up * Earth.Radius;
        for (int i = 0; i < booked; i++)
        {
            ref readonly var b = ref book[i];
            double age = (drawMs - b.BurstMs) / 1000.0, burn = (b.OutMs - b.BurstMs) / 1000.0;
            if (age < 0)
                continue;
            double fall = Math.Min(age, burn);
            var origin = b.Origin + b.Drift * age;   // the burst's puff, drifted with the wind as the star has
            var star = origin - b.Origin.Normalized() * (b.FallMs * fall);
            if (Earth.Distance(star, centre) > reach)
                continue;
            var (lo, hi) = (Lift(view, star, k), Lift(view, origin, k));
            // the trail: from the star (or where it went out) up to the burst, wider with its age, fading after burn-out
            double fade = world * (age <= burn ? 1 : Math.Clamp(1 - (age - burn) / TrailS, 0, 1));
            double wLo = TrailM + TrailGrowMs * Math.Max(age - fall, 0), wHi = TrailM + TrailGrowMs * age;
            Add(new FlareGpu
            {
                Lo = new Vector4(F(lo), 0), Hi = new Vector4(F(hi), (float)wHi),
                LoAt = new Vector4(F(star - view.Eye), (float)wLo), HiAt = new Vector4(F(origin - view.Eye), (float)(fade * fade)),
                Look = new Vector4(0, 0, (float)TrailM, 0),
            });
            // the star, while it burns
            var (light, red) = Envelope(age, burn, b.BurstMs ^ (long)(b.Origin.X * 1000));
            double cd = b.Cd * light * world * Gain;
            if (!(cd > 0))
                continue;
            double core = Math.Max(CoreM, 1.5 * mpp);
            double coreDisplay = perCd * cd / (Math.PI * core * core);   // its candle spread over its drawn core
            // its glow: sigma p I times the eye's ray's angle past it, display units x m; out to where it sinks under the
            // night sea's own light (its body a few percent of a lit deck)
            double halo = HaloGain * perCd * scatter * cd * HazePhase, h = star.Length - Earth.Radius;
            double under = 0.25 * SeaShare * FlashRenderer.DisplayWhite(frameLights, up), r = 50;
            while (r < 10_000 && halo * (Math.PI / 2 + Math.Atan2(h, r)) / r * Math.Exp(-scatter * r) > under)
                r *= 1.25;
            var colour = Vector3.Lerp(NightLights.StarColour, new Vector3(1f, 0.55f, 0.25f), (float)red);
            Add(new FlareGpu
            {
                Lo = new Vector4(F(lo), 1), Hi = new Vector4(F(lo), 0),
                LoAt = new Vector4(F(star - view.Eye), 0), HiAt = new Vector4(0, 0, 0, (float)scatter),
                Look = new Vector4((float)coreDisplay, (float)halo, (float)core, (float)r),
                Colour = new Vector4(FlashPhotometry.UnitLuminance(colour), (float)Math.Max(h, 0)),
            });
        }
    }

    void Book(FlaresView f, int i)
    {
        for (int j = 0; j < booked; j++)
            if (book[j].BurstMs == f.BurstMs[i] && book[j].Origin == f.Origin[i])
                return;
        if (booked == book.Length)
            Array.Resize(ref book, booked * 2);
        book[booked++] = new Seen
        {
            Origin = f.Origin[i], Drift = f.Drift[i], BurstMs = f.BurstMs[i], OutMs = f.OutMs[i], Cd = f.Candela[i], FallMs = f.FallMs[i],
        };
    }

    void Add(in FlareGpu g)
    {
        if (count == items.Length)
            Array.Resize(ref items, items.Length * 2);
        items[count++] = g;
    }

    /// <summary>A point as the effects camera draws it, metres from the eye: the sea under it, its height up the screen.</summary>
    static Vec3d Lift(in MapView view, Vec3d p, double k)
    {
        double h = p.Length - Earth.Radius;
        var ground = p.Normalized() * Earth.Radius;
        return ground + view.Up * (h * k) - view.Eye;
    }

    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        uploaded = Math.Min(count, (int)(BufferBytes / FlareGpu.SizeInBytes));
        if (uploaded > 0)
            buf.Upload(cmd, items.AsSpan(0, uploaded));
    }

    /// <summary>The flares, last over everything (they hang high over it all).</summary>
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
                ? b.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_ALWAYS)
                : b.NoDepthTarget().DisableDepthTest();
            pipe = b.SetShaders(vs, fs).Build(device);
            if (pipe == null)
                throw new InvalidOperationException($"flare pipeline: {SDL_GetError()}");
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

    /// <summary>Sets a knob, "on:1", "gain:1", "halo:1", "trail:40", "width:1.2", "grow:0.25" (the -flares= flag, for
    /// side-by-side captures); false for a bad one.</summary>
    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], System.Globalization.CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "on": On = v != 0; break;
            case "gain": Gain = Math.Max(v, 0); break;
            case "halo": HaloGain = Math.Max(v, 0); break;
            case "trail": TrailS = Math.Max(v, 0); break;
            case "width": TrailM = Math.Max(v, 0.1); break;
            case "grow": TrailGrowMs = Math.Max(v, 0); break;
            default: return false;
        }
        return true;
    }

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
