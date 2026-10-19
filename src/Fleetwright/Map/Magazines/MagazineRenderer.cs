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
    public Vector4 Base, Quad, Shade, Body, BodyB, Drift, Time, Misc;
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
/// the fade), placed eye-relative in double, and two instanced draws over them, from the one model in
/// <c>Include/magazine.glsl</c>:
/// <list type="bullet">
/// <item>the shadows on the sea and the decks, with the other effects' shadows, after the ships (<see cref="DrawShadows"/>);</item>
/// <item>the bodies (the fireball and its cloud) through the effects camera, after the splash columns and the gun smoke, so
/// the boom hides the splashes round its ship, then the flame jets from the failed openings before the fireball and the
/// hit's flash, screen-blended (<see cref="Draw"/>).</item>
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
    readonly SDL_GPUShader* vs, fsBody, fsShadow, vsJet, fsJet, fsTrail;
    readonly DynamicGpuBuffer buf, jetBuf, trailBuf;
    SDL_GPUGraphicsPipeline* bodyPipe, shadowPipe, jetPipe, trailPipe;
    readonly Splashes.SplashBook? splashes;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    int uploaded, jetsUploaded, trailsUploaded;

    MagazineGpu[] items = new MagazineGpu[8];
    MagazineJetGpu[] jets = new MagazineJetGpu[32], trails = new MagazineJetGpu[64];
    int count, jetCount, trailCount;
    Vector2 windDir, shadowDrift;
    double windMs;
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

    /// <summary>The trails of the burning debris: how many moments of each arc its ribbon joins.</summary>
    const int TrailSamples = 8;

    public MagazineRenderer(SDL_GPUDevice* device, Splashes.SplashBook? splashes = null)
    {
        this.device = device;
        this.splashes = splashes;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/magazine.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fsBody = GpuShader.Load(device, "Content/Shaders/Compiled/magazine_body.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 2);
        fsShadow = GpuShader.Load(device, "Content/Shaders/Compiled/magazine_shadow.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 2);
        vsJet = GpuShader.Load(device, "Content/Shaders/Compiled/magazine_jet.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fsJet = GpuShader.Load(device, "Content/Shaders/Compiled/magazine_jet.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 2);
        fsTrail = GpuShader.Load(device, "Content/Shaders/Compiled/magazine_trail.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 2);
        if (vs == null || fsBody == null || fsShadow == null || vsJet == null || fsJet == null || fsTrail == null)
            throw new InvalidOperationException("magazine shaders failed to load");
        buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, BufferBytes);
        jetBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, JetBufferBytes);
        trailBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, JetBufferBytes);
    }

    // ------------------------------------------------------------------ the CPU side

    /// <summary>A frame: the book updated from the snapshot, then the events in view as instances. <paramref name="speed"/>
    /// is the clock's (0 paused), <paramref name="dtS"/> the frame's real seconds, <paramref name="world"/> the view's
    /// <see cref="WorldView.Factor(in MapView)"/>.</summary>
    public void Build(in MapView view, Snapshot s, long drawMs, double speed, double dtS, double world, ShipLights frameLights,
        Func<int, ShipClass?> classOf, FlashLights? flashLights = null, BlastRenderer? blasts = null, SmokeBook? smoke = null, double smokeNow = 0)
    {
        Book.Update(s.Blasts, drawMs, speed, dtS, classOf, s.Ships, splashes);
        if (smoke != null && MagazineLook.On && (MagazineLook.Parts & 32) != 0)
            Book.SpawnSmoke(smoke, smokeNow);
        lights = frameLights;
        count = jetCount = trailCount = 0;
        Ready = false;
        if (world <= 0)
            return;
        Frame(view, s, drawMs);
        if (!MagazineLook.On || Book.Count == 0)
            return;
        try
        {
            BuildEvents(view, world, flashLights, blasts);
        }
        finally
        {
            flashLights?.Finish(view);
        }
    }

    /// <summary>Whether this frame's light, wind and shadows are set: the bodies, flashes and jets of others (a shell's
    /// burst, docs/visuals/hits-and-fires.md I1) may be added after <see cref="Build"/>.</summary>
    public bool Ready { get; private set; }

    /// <summary>Display units per cd/m² this frame (the fire's brightness as drawn), once <see cref="Ready"/>.</summary>
    public double PerCandela { get; private set; }

    /// <summary>This frame's light, wind and shadows, which every body drawn shares.</summary>
    void Frame(in MapView view, Snapshot s, long drawMs)
    {
        var up = (-view.Forward).Normalized();
        var (east, north) = (view.Right, view.Up);
        Vector3 Local(Vec3d d) => new((float)Vec3d.Dot(d, east), (float)Vec3d.Dot(d, north), (float)Vec3d.Dot(d, up));
        var sunL = Local(lights.SunDir);
        var moonL = Local(lights.MoonDir);
        var l = Local(lights.ShadowDir);
        double sinLo = Math.Sin(3 * Math.PI / 180), sinHi = Math.Sin(7 * Math.PI / 180);
        float on = (float)(lights.ShadowOpacity * SmoothStep(sinLo, sinHi, l.Z));
        shadowDrift = l.Z > 1e-3 ? -new Vector2(l.X, l.Y) / l.Z : Vector2.Zero;
        if (shadowDrift.Length() > MaxDrift)
            shadowDrift *= MaxDrift / shadowDrift.Length();
        shadows = on > 0.002f && (MagazineLook.Parts & 2) != 0;
        double mpp = view.MetresPerPixel, k = Effects.K;
        PerCandela = MagazineLook.Gain * FlashRenderer.PerCandela(lights, s.Sun, up, drawMs);
        prms = new Params
        {
            SunL = new Vector4(sunL, 0), MoonL = new Vector4(moonL, 0), ShadowL = new Vector4(shadowDrift, on, 0),
            Misc = new Vector4((float)k, (float)mpp, (float)MagazineLook.Steps, (float)PerCandela),
            FireK = new Vector4((float)MagazineLook.ColdK, (float)MagazineLook.HotK, (float)MagazineLook.SootAlbedo, 0),
            More = new Vector4(0, 0, (float)MagazineLook.Whiten, 0),
        };
        // the wind, toward where it blows (east, north), and its speed at 10 m
        double toward = (s.Weather.WindFromDeg + 180) * Math.PI / 180;
        windDir = new Vector2((float)Math.Sin(toward), (float)Math.Cos(toward));
        windMs = s.Weather.WindMs;
        Ready = true;
    }

    void BuildEvents(in MapView view, double world, FlashLights? flashLights, BlastRenderer? blasts)
    {
        double mpp = view.MetresPerPixel, k = Effects.K, perCd = PerCandela;
        var book = Book;
        for (int i = 0; i < book.Count; i++)
        {
            ref readonly var e = ref book[i];
            if (MagazineLook.AtS >= 0 && !book.InStill(e))
                continue;
            // (perCd and the rest above are this frame's; the lights and the blast are added below for each)
            double age = MagazineLook.AtS >= 0 ? MagazineLook.AtS : book.Clock - e.T0;
            double t = age - MagazineBlast.MainAtS, life = e.FireballS;
            var ev = e.Event;
            double deck = ev.DeckZ;
            if (age >= 0 && t < 2.5 && (MagazineLook.Parts & 4) != 0)
                AddJets(view, e, age, world, mpp, k);
            if (t >= 0 && t < 40 && (MagazineLook.Parts & 8) != 0)
                AddDebris(view, e, t, world, mpp, k);
            // the blast on the water (the muzzle blasts' model, spherical), on the sea's clock from the main event
            double seaAge = MagazineLook.AtS >= 0 ? t : book.SeaClock - e.SeaT0 - MagazineBlast.MainAtS;
            blasts?.AddSpherical(view, e.At + e.At.Normalized() * deck, deck, ev.BlastLengthM, seaAge, world, e.Seed);
            if (flashLights != null && (MagazineLook.Parts & 16) != 0)
                Light(view, e, age, perCd, flashLights);
            if (t < 0 || t > MagazineLook.FadeTo * life)
                continue;
            AddBody(view, e.At, deck, e.FireballM, life, t, e.Seed, world);
        }
    }

    /// <summary>A fireball and its cloud (<see cref="MagazineBlast.BodyAt"/>) of diameter <paramref name="d"/> m and life
    /// <paramref name="life"/> s, <paramref name="t"/> s after it burst, over the sea point <paramref name="seaAt"/> from a
    /// deck <paramref name="deck"/> m over the waterline: drifting downwind with the shear once grown, fading out from
    /// <see cref="MagazineLook.FadeFrom"/> to <see cref="MagazineLook.FadeTo"/> lives. A magazine's, or a shell's burst
    /// (docs/visuals/hits-and-fires.md I1): one body model. Needs <see cref="Ready"/>.</summary>
    public void AddBody(in MapView view, Vec3d seaAt, double deck, double d, double life, double t, float seed, double world)
    {
        if (!Ready || t < 0 || t > MagazineLook.FadeTo * life)
            return;
        double mpp = view.MetresPerPixel, k = Effects.K, perCd = PerCandela;
        var drift = shadowDrift;
        double u10 = windMs;
        var b = MagazineBlast.BodyAt(d, life, deck, t);
        // downwind: the cloud at its height's wind (the shear's power law), once it's grown
        double u = u10 * Math.Pow(Math.Max(b.CentreZ, 1) / 10, 0.14);
        var capDrift = windDir * (float)(u * Math.Max(t - 0.3 * life, 0) * 0.8);
        double r = b.R, rv = b.Rv;
        double sigma = MagazineLook.Opacity * b.Tau / (2 * r);
        double fade = (1 - SmoothStep(MagazineLook.FadeFrom * life, MagazineLook.FadeTo * life, t)) * world;
        double roll = SmoothStep(0.6 * life, 1.5 * life, t);
        double churn = MagazineLook.Churn * Math.Log(1 + t / (0.3 * life));
        double spin = t > life ? 2 * Math.PI * MagazineLook.Spin * 2 * life * (Math.Sqrt(t / life) - 1) : 0;
        // the fire's light on its own smoke: the core's drawn brightness, compressed as the flashes' halo's source
        double kel = MagazineLook.ColdK + (MagazineLook.HotK - MagazineLook.ColdK) * Math.Max(b.Heat, 0);
        double drawn = perCd * FlashPhotometry.Luminance(kel) * Math.Max(b.Heat, 0);
        double glow = 0.3 * drawn / (1 + drawn / FlashLook.HaloKnee);

        // the quads: the body through the effects camera, its shadow down-sun
        double zb = Math.Max(b.CentreZ - 1.35 * rv, 0), zt = b.CentreZ + 1.35 * rv;
        double rr = 1.4 * r;
        var c = new Vector2(capDrift.X, capDrift.Y);
        var lo = c - new Vector2((float)rr) + new Vector2(0, (float)(k * (zb - deck)));
        var hi = c + new Vector2((float)rr) + new Vector2(0, (float)(k * (zt - deck)));
        Vector4 shade = default;
        if (shadows)
        {
            var sLo = Vector2.Min(c + drift * (float)zb, c + drift * (float)zt) - new Vector2((float)rr);
            var sHi = Vector2.Max(c + drift * (float)zb, c + drift * (float)zt) + new Vector2((float)rr);
            shade = new Vector4(sLo, sHi.X, sHi.Y);
        }

        if (!view.Project(seaAt, out double x, out double y))
            return;
        double reach = Math.Max(Math.Max(Math.Abs(lo.X), Math.Abs(lo.Y)), Math.Max(Math.Abs(hi.X), Math.Abs(hi.Y)));
        if (shadows)
            reach = Math.Max(reach, Math.Max(Math.Max(Math.Abs(shade.X), Math.Abs(shade.Y)), Math.Max(Math.Abs(shade.Z), Math.Abs(shade.W))));
        double reachPx = reach / mpp + 8;
        if (x < -reachPx || x > view.Width + reachPx || y < -reachPx || y > view.Height + reachPx)
            return;

        var rel = seaAt - view.Eye;
        if (count == items.Length)
            Array.Resize(ref items, count * 2);
        items[count++] = new MagazineGpu
        {
            Base = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, seed),
            Quad = new Vector4(lo, hi.X, hi.Y),
            Shade = shade,
            Body = new Vector4(c, (float)b.CentreZ, (float)r),
            BodyB = new Vector4((float)rv, (float)sigma, (float)b.Heat, (float)(b.Skin * MagazineLook.Skin * 0.5)),
            Time = new Vector4((float)churn, (float)roll, (float)spin, (float)fade),
            Misc = new Vector4((float)deck, (float)glow, 0, 0),
        };
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
        float Dn(double z) => (float)(k * (z - deck));
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

    /// <summary>An event's debris now, <paramref name="t"/> s after the main event: each piece on its arc (a hot fleck if
    /// burning, a dark one if not) until it comes down (its splash is the splash book's), the burning ones' trails as
    /// ribbons through the last moments of their arcs, spreading, rising a little and drifting downwind as they age;
    /// and the sparks, burning grains, for their seconds.</summary>
    void AddDebris(in MapView view, in MagazineBook.Entry e, double t, double world, double mpp, double k)
    {
        var ev = e.Event;
        var d = e.Debris;
        var (east, north) = (view.Right, view.Up);
        var f = new Vector2((float)Vec3d.Dot(e.Forward, east), (float)Vec3d.Dot(e.Forward, north));
        var sb = new Vector2((float)Vec3d.Dot(e.Starboard, east), (float)Vec3d.Dot(e.Starboard, north));
        double deck = ev.DeckZ;
        if (!view.Project(e.At, out double px, out double py) || px < -4000 / mpp || py < -4000 / mpp || px > view.Width + 4000 / mpp ||
            py > view.Height + 4000 / mpp)
            return;
        var rel = e.At - view.Eye;
        var baseV = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, e.Seed);
        // a point of the ship's frame (from the magazine), height over the waterline, drawn through the effects camera,
        // with a drift over the sea (east, north)
        Vector2 Drawn(Vector3 p, Vector2 drift) =>
            f * (float)(p.X - ev.X) + sb * (float)(p.Y - ev.Y) + drift + new Vector2(0, (float)(k * (p.Z - deck)));
        float minR = (float)(0.8 * mpp);

        foreach (var piece in d.Pieces)
        {
            double land = MagazineDebris.Landing(d.From.Z, piece.V.Z);
            if (t < land)
            {
                var at = Drawn(MagazineDebris.At(d.From, piece.V, t), Vector2.Zero);
                float r = Math.Max(piece.Radius, minR);
                if (piece.Burning)
                    AddJet(baseV, at, at, 1.4f * r, (float)(0.45 + 0.4 * Math.Exp(-t / 6)), (float)world, t);
                else
                    AddTrail(baseV, at, at, r, r, 0.9f, 0.9f, (float)world);
            }
            if (!piece.Burning)
                continue;
            // the trail: from the piece back through its last TrailS seconds, each moment's smoke aged since
            double span = Math.Min(Math.Min(t, land), piece.TrailS), head = Math.Min(t, land);
            if (span <= 0)
                continue;
            Vector2 prev = default;
            float prevR = 0, prevA = 0;
            for (int j = 0; j <= TrailSamples; j++)
            {
                double tj = head - span * j / TrailSamples, aj = t - tj;
                var p = MagazineDebris.At(d.From, piece.V, tj) + new Vector3(0, 0, (float)(1.5 * aj));
                double u = windMs * Math.Pow(Math.Max(p.Z, 1) / 10, 0.14);
                var at = Drawn(p, windDir * (float)(u * aj * 0.7));
                float r = Math.Max((float)(0.8 + 1.0 * aj), minR);
                float a = (float)(0.55 * Math.Exp(-aj / (0.5 * piece.TrailS)) * (1 - SmoothStep(0.7 * piece.TrailS, piece.TrailS, aj)));
                if (j > 0 && (a > 0.01f || prevA > 0.01f))
                    AddTrail(baseV, prev, at, prevR, r, prevA, a, (float)world);
                (prev, prevR, prevA) = (at, r, a);
            }
        }

        // the sparks
        int sparks = (int)Math.Round(MagazineDebris.Sparks * Math.Min(1, ev.K));
        for (int i = 0; i < sparks; i++)
        {
            var (v, life) = MagazineDebris.Spark(e.Hash, i);
            if (t >= life)
                continue;
            var p = MagazineDebris.At(d.From, v, t);
            if (p.Z < 0)
                continue;
            var at = Drawn(p, Vector2.Zero);
            AddJet(baseV, at, at, Math.Max(0.6f, minR), (float)(0.95 - 0.45 * t / life), (float)(world * (1 - t / life)), t);
        }
    }

    /// <summary>The fire's light (X5), into the frame's point lights with the flashes': while the jets burn, at the
    /// magazine a few tens of metres up; then the fireball's at its centre, by its heat. Each throws its brightness as
    /// drawn, compressed as the flashes' halo's source, times its area over pi (FlashRenderer's rule), times
    /// <see cref="MagazineLook.LightShare"/> (the research: keep it to a local pool, or the night sea goes beige).</summary>
    void Light(in MapView view, in MagazineBook.Entry e, double age, double perCd, FlashLights into)
    {
        var ev = e.Event;
        var up = e.At.Normalized();
        double knee = FlashLook.HaloKnee, main = MagazineBlast.MainAtS, t = age - main;
        double Thrown(double kelvin, double areaM2)
        {
            double drawn = perCd * FlashPhotometry.Luminance(kelvin);
            return drawn / (1 + drawn / knee) * areaM2 / Math.PI * MagazineLook.LightShare;
        }
        if (age >= MagazineBlast.HitLeadS && t < 0.5)
        {
            double area = 0;
            foreach (ref readonly var o in ev.Openings)
            {
                double len = MagazineBlast.JetLength(o, age);
                if (len >= 2)
                    area += len * (0.45 * o.DiameterM + 0.03 * len) * 2;
            }
            if (area > 0)
                into.Add(e.At + up * (ev.DeckZ + 20), Thrown(MagazineLook.ColdK + 0.9 * (MagazineLook.HotK - MagazineLook.ColdK), area) * (1 - SmoothStep(0, 0.5, t)),
                    FlashPhotometry.Chroma(1900), 30);
        }
        if (t < 0 || t > 3 * e.FireballS)
            return;
        var b = MagazineBlast.BodyAt(e.FireballM, e.FireballS, ev.DeckZ, t);
        if (b.Heat < 0.05)
            return;
        double kel = MagazineLook.ColdK + (MagazineLook.HotK - MagazineLook.ColdK) * b.Heat;
        // the soot skin hides the fire as it grows over it
        double rad = Thrown(kel, Math.PI * b.R * b.R) * (1 - 0.7 * b.Skin);
        into.Add(e.At + up * b.CentreZ, rad, FlashPhotometry.Chroma(kel), b.R);
    }

    /// <summary>A flame from <paramref name="a"/> to <paramref name="tip"/> (metres east and north of the sea point
    /// <paramref name="seaAt"/>, heights over its deck already drawn up the screen), r0 to r1 m wide, hot at its root and
    /// cooler at its tip, <paramref name="lenM"/> long: a magazine's opening's, or a shell's burst venting out of its hole
    /// (docs/visuals/hits-and-fires.md I1). A point (a == tip) is a round flash (a hit on a plate, a spark). One jet
    /// model. Off the screen, nothing.</summary>
    public void AddJet(in MapView view, Vec3d seaAt, Vector2 a, Vector2 tip, double r0, double r1, double heat0, double heat1, double lenM,
        double strength, double clock, float seed)
    {
        if (strength <= 0.002 || !view.Project(seaAt, out double px, out double py))
            return;
        double mpp = view.MetresPerPixel, reach = (Math.Max(a.Length(), tip.Length()) + 2 * Math.Max(r0, r1)) / mpp + 4;
        if (px < -reach || py < -reach || px > view.Width + reach || py > view.Height + reach)
            return;
        var rel = seaAt - view.Eye;
        float pad = 2 * (float)Math.Max(r0, r1);
        Add(new MagazineJetGpu
        {
            Base = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, seed), Seg = new Vector4(a, tip.X, tip.Y),
            Shape = new Vector4((float)r0, (float)r1, (float)heat0, (float)heat1), Look = new Vector4((float)strength, (float)clock, (float)lenM, 0),
            Quad = new Vector4(Vector2.Min(a, tip) - new Vector2(pad), Vector2.Max(a, tip).X + pad, Vector2.Max(a, tip).Y + pad),
        });
    }

    /// <summary>The light a fire of <paramref name="kelvin"/> over <paramref name="areaM2"/> throws on the sea and the ships
    /// (display units × m²): its brightness as drawn this frame, compressed as the flashes' halo's source, times its area
    /// over pi (FlashRenderer's rule), times a share. Needs <see cref="Ready"/>.</summary>
    public double Light(double kelvin, double areaM2, double share)
    {
        double drawn = PerCandela * FlashPhotometry.Luminance(kelvin);
        return drawn / (1 + drawn / FlashLook.HaloKnee) * areaM2 / Math.PI * share;
    }

    void AddJet(Vector4 baseV, Vector2 a, Vector2 b, float r, float heat, float strength, double clock)
    {
        float pad = 2 * r;
        Add(new MagazineJetGpu
        {
            Base = baseV, Seg = new Vector4(a, b.X, b.Y), Shape = new Vector4(r, r, heat, heat),
            Look = new Vector4(strength, (float)clock, 0, 0), Quad = new Vector4(Vector2.Min(a, b) - new Vector2(pad), Vector2.Max(a, b).X + pad, Vector2.Max(a, b).Y + pad),
        });
    }

    void AddTrail(Vector4 baseV, Vector2 a, Vector2 b, float ra, float rb, float alphaA, float alphaB, float opacity)
    {
        float pad = 1.2f * Math.Max(ra, rb);
        if (trailCount == trails.Length)
            Array.Resize(ref trails, trailCount * 2);
        trails[trailCount++] = new MagazineJetGpu
        {
            Base = baseV, Seg = new Vector4(a, b.X, b.Y), Shape = new Vector4(ra, rb, alphaA, alphaB),
            Look = new Vector4(opacity, 0, 0, 0), Quad = new Vector4(Vector2.Min(a, b) - new Vector2(pad), Vector2.Max(a, b).X + pad, Vector2.Max(a, b).Y + pad),
        };
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
        trailsUploaded = Math.Min(trailCount, (int)(JetBufferBytes / MagazineJetGpu.SizeInBytes));
        if (trailsUploaded > 0)
            trailBuf.Upload(cmd, trails.AsSpan(0, trailsUploaded));
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
        trailPipe = B().SetShaders(vsJet, fsTrail).Build(device);
        var jb = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingScreen().ClearVertexLayout().SetShaders(vsJet, fsJet);
        jetPipe = (target.HasDepth
            ? jb.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
            : jb.NoDepthTarget().DisableDepthTest()).Build(device);
        if (bodyPipe == null || shadowPipe == null || jetPipe == null || trailPipe == null)
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
        if (trailPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, trailPipe);
        bodyPipe = shadowPipe = jetPipe = trailPipe = null;
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

    /// <summary>The shadows on the sea and the decks, after the ships and before any column or cloud stands over them.</summary>
    public void DrawShadows(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0 || !shadows)
            return;
        EnsurePipelines(target);
        Pass(cmd, pass, shadowPipe, view, 1);
    }

    /// <summary>The debris trails, then the bodies, then the jets, after the splash columns and the gun smoke: the
    /// boom hides the splashes falling round the ship it is destroying.</summary>
    public void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0 && jetsUploaded == 0 && trailsUploaded == 0)
            return;
        EnsurePipelines(target);
        if (trailsUploaded > 0)
            Pass(cmd, pass, trailPipe, view, 0, trailBuf, trailsUploaded);
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
        trailBuf.Dispose();
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fsBody);
        SDL_ReleaseGPUShader(device, fsShadow);
        SDL_ReleaseGPUShader(device, vsJet);
        SDL_ReleaseGPUShader(device, fsJet);
        SDL_ReleaseGPUShader(device, fsTrail);
    }
}
