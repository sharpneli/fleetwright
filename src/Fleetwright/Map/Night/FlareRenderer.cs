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

/// <summary>One flare's part as flare.vert reads it (std430: 6 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct FlareGpu
{
    /// <summary>Lo, Hi: the part's ends as drawn (on the sea's plane, lifted by the effects camera; metres from the eye),
    /// w the kind (0 the trail, 1 the star, 2 a small light) and the trail's half width at the top. LoAt, HiAt: the same points where they
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
/// And the night's <b>small lights</b> on the ships the map shows (Q10e), points as the star is, on her as she's drawn
/// (not lifted: they're on her): her funnels' glow while she forces coal fires (<see cref="SmokeSources.FunnelGlowCd"/>,
/// the sim's own light, a dull red at each funnel's mouth, flickering) and a challenge's lamps (docs/battle/night.md Q6;
/// the record's coloured lamp groups, a row on her highest lookout's yard switched on three times over the signal, the
/// reply's on the ship challenged).
/// The world view's alone. A book of the flares seen keeps their trails after the snapshot drops them. Nothing allocates
/// once grown.</summary>
public sealed unsafe class FlareRenderer : IDisposable
{
    const uint BufferBytes = 96 << 9;   // 512 parts

    public static bool On = true;

    /// <summary>A gain on the star and on its glow in the haze (1: physical, through the night eye). The glow is turned down
    /// (the user, 2026-10-29, docs/visuals/night-lights.md Q10f): physical, a flare in a 10 km haze lit the air kilometres
    /// round to ten times a starlit white deck, a moonless night gone grey; a third keeps the glow and the night.</summary>
    public static double Gain = 1, HaloGain = 0.3;

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

    /// <summary>A coal fire's glow's temperature, K, and a challenge lamp's light through its colour's glass, a share of
    /// <see cref="SimSettings.ChallengeLampCd"/>: red, green, white [EST].</summary>
    public const double FunnelKelvin = 1150;
    static readonly (Vector3 Colour, double Share)[] LampGlass = [(new(1f, 0.12f, 0.08f), 0.2), (new(0.15f, 1f, 0.45f), 0.15), (new(1f, 0.95f, 0.85f), 1)];

    /// <summary>A frame: the book brought up to the snapshot's flares, then each one's trail and burning star as parts,
    /// and the small lights on the ships shown.</summary>
    public void Build(in MapView view, Snapshot s, long drawMs, double world, ShipLights frameLights, double visibilityM, SimSettings settings,
        Func<int, ShipClass?> classOf)
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
        if (!On || world <= 0)
            return;
        var up = (-view.Forward).Normalized();
        double perCd = FlashRenderer.PerCandela(frameLights, up), k = Effects.K;
        double scatter = SearchlightModel.ScatterPerM(visibilityM, settings);
        SmallLights(view, s, drawMs, world, perCd, scatter, settings, classOf);
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

    /// <summary>The small lights on the ships shown: her funnels' glow, a challenge's lamps.</summary>
    void SmallLights(in MapView view, Snapshot s, long drawMs, double world, double perCd, double scatter, SimSettings settings,
        Func<int, ShipClass?> classOf)
    {
        double mpp = view.MetresPerPixel;
        foreach (var v in (ReadOnlySpan<ShipsView>)[s.Ships, s.Sighted])
            for (int k = 0; k < v.Count; k++)
            {
                if (classOf(v.Class[k]) is not { } c)
                    continue;
                double cd = SmokeSources.FunnelGlowCd(c, v.Speed[k], settings);
                if (!(cd > 0))
                    continue;
                var (p, f) = v.PoseAt(k, drawMs);
                var (stbd, upv) = (Vec3d.Cross(f, p.Normalized()), p.Normalized());
                var colour = FlashPhotometry.Chroma(FunnelKelvin);
                foreach (ref readonly var fu in c.Funnels)
                {
                    var b = c.Components[fu.Component].Bounds;
                    var at = p + f * ((b.X0 + b.X1) / 2) + stbd * ((b.Y0 + b.Y1) / 2) + upv * (b.Top - c.Vertical.Waterline);
                    // each funnel its share, its mouth's glow flickering as the fire's draught does
                    var (light, _) = Envelope(10 + drawMs / 1000.0 % 1000, 1e9, v.Entity[k] * 31 + fu.Component);
                    double r = Math.Max(0.3 * Math.Min(b.X1 - b.X0, b.Y1 - b.Y0), 1.2 * mpp);
                    Point(view, at, cd / c.Funnels.Length * light * world, colour, r, perCd, scatter);
                }
            }
        var ch = s.Challenges;
        long signal = (long)(settings.ChallengeSignalS * 1000);
        for (int i = 0; i < ch.Count; i++)
        {
            Lamps(view, s, ch.Ship[i], drawMs, drawMs - ch.StartMs[i], signal, ch.StartMs[i] ^ ch.Ship[i], world, perCd, scatter, settings, classOf);
            if (ch.AnswerMs[i] > 0)
                Lamps(view, s, ch.Target[i], drawMs, drawMs - ch.AnswerMs[i], signal, ch.AnswerMs[i] ^ ch.Target[i], world, perCd, scatter, settings, classOf);
        }
    }

    /// <summary>A challenge's (or its reply's) lamps on a ship, <paramref name="ms"/> into its signal: a row of three
    /// across her highest lookout's yard, each red, green or white by the challenge (<paramref name="seed"/>), on for two
    /// seconds of every three while the signal lasts.</summary>
    void Lamps(in MapView view, Snapshot s, int ship, long drawMs, long sinceMs, long signal, long seed, double world, double perCd,
        double scatter, SimSettings settings, Func<int, ShipClass?> classOf)
    {
        if (sinceMs < 0 || sinceMs >= signal || sinceMs % 3000 >= 2000)
            return;
        foreach (var v in (ReadOnlySpan<ShipsView>)[s.Ships, s.Sighted])
            if (v.IndexOf(ship) is int k and >= 0 && classOf(v.Class[k]) is { } c && c.Eyes.Length > 0)
            {
                var eye = c.Eyes[0];
                foreach (ref readonly var e in c.Eyes)
                    if (e.HeightM > eye.HeightM)
                        eye = e;
                var (p, f) = v.PoseAt(k, drawMs);
                var (upv, r) = (p.Normalized(), Math.Max(LampM, 1.5 * view.MetresPerPixel));
                var stbd = Vec3d.Cross(f, upv);
                for (int j = 0; j < 3; j++)
                {
                    var (colour, share) = LampGlass[Math.Min((int)(Hash(seed, j) * LampGlass.Length), LampGlass.Length - 1)];
                    var at = p + f * eye.X + stbd * (eye.Y + (j - 1) * LampSpacingM) + upv * eye.HeightM;
                    Point(view, at, settings.ChallengeLampCd * share * world, colour, r, perCd, scatter);
                }
                return;
            }
    }

    /// <summary>A challenge lamp's drawn size and the lamps' spacing along the yard, m [EST].</summary>
    const double LampM = 0.3, LampSpacingM = 2;

    /// <summary>A small light as a star part (kind 2: its colour kept far past white, a red lamp still red: a coloured
    /// lamp's colour is its message): its candle over a core of radius <paramref name="r"/>, its glow in the haze.</summary>
    void Point(in MapView view, Vec3d at, double cd, Vector3 colour, double r, double perCd, double scatter)
    {
        if (!(cd > 0))
            return;
        var rel = at - view.Eye;
        double halo = HaloGain * perCd * scatter * cd * HazePhase;
        Add(new FlareGpu
        {
            Lo = new Vector4(F(rel), 2), Hi = new Vector4(F(rel), 0),
            LoAt = new Vector4(F(rel), 0), HiAt = new Vector4(0, 0, 0, (float)scatter),
            Look = new Vector4((float)(Gain * perCd * cd / (Math.PI * r * r)), (float)halo, (float)r, (float)Math.Max(8 * r, 20)),
            Colour = new Vector4(FlashPhotometry.UnitLuminance(colour), (float)Math.Max(at.Length - Earth.Radius, 0)),
        });
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
