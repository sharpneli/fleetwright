using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map.Splashes;

/// <summary>One splash as the splash shaders read it (splash.vert's Splash, std430: 5 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SplashGpu
{
    public Vector4 Base, Shape, Lean, Dye, Foam;
    public const int SizeInBytes = 80;
}

/// <summary>The world view's shell splashes (docs/visuals/splashes.md): the book's splashes in view as instances, built
/// on the CPU each frame (placed eye-relative in double, sized by calibre and angle of fall, sorted north to south), and
/// four instanced draws over them, all from the one model in <c>Include/splash.glsl</c>:
/// <list type="bullet">
/// <item>the foam, into the wake target in its pre-pass, flat on the water, lit by the sea's shading
/// (<see cref="DrawFoam"/>);</item>
/// <item>the dye's stains, in the main pass after the sea and before the ships (<see cref="DrawStains"/>);</item>
/// <item>the shadows, with the other effects' shadows, after the ships (<see cref="DrawShadows"/>);</item>
/// <item>the columns through the effects camera, after the ships and before the gun smoke and the magazine explosions
/// (<see cref="DrawColumns"/>): a column covers what's north of it, never what's south, so it stands over a ship it's
/// south of, but a cloud hides it.</item>
/// </list>
/// The ships' layer calls it (<see cref="ShipBatchRenderer"/>). It reads the snapshot's splashes and the frame's
/// lights, never the world; nothing allocates once its arrays have grown.</summary>
public sealed unsafe class SplashRenderer : IDisposable
{
    const uint BufferBytes = 2 << 20;   // ~26,000 splashes
    const double Gravity = 9.81;
    /// <summary>The column's peak height and the shaft's width per metre of calibre (research/vfx/shell-splashes.md:
    /// a 16" 73 m and 10 m), and the lean at the shallowest fall.</summary>
    const double HeightPerM = 180, WidthPerM = 25, LeanDeg = 25;
    /// <summary>Each splash's own share (the user, 2026-10-15: identical columns starting together read as clones):
    /// its height and width up or down by up to these, its rise and fall as much longer or shorter, and its start up to
    /// <see cref="DelayS"/> after the impact. All from its seed, so a splash is the same from frame to frame; scaled by
    /// <see cref="SplashLook.Vary"/>.</summary>
    const double VaryHeight = 0.15, VaryWidth = 0.10, VaryTime = 0.10, DelayS = 0.3;
    /// <summary>A torpedo's hit (docs/visuals/torpedoes.md R5) on the one column model: its shaft this much wider than a
    /// shell's of the same height (the plume ~0.4 of its height across), its water greyed by the explosion's smoke
    /// through the dye's tint.</summary>
    const double TorpedoWidth = 2.5;
    static readonly Vector4 TorpedoGrey = new(0.42f, 0.42f, 0.44f, 1);
    /// <summary>The longest shadow, metres per metre of height (a sun at ~7°).</summary>
    const float MaxDrift = 8;

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fsColumn, fsShadow, fsFoam, fsStain;
    readonly DynamicGpuBuffer buf;
    SDL_GPUGraphicsPipeline* columnPipe, shadowPipe, stainPipe, foamPipe;
    SDL_GPUTextureFormat pipeFormat, foamFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    int uploaded;

    SplashGpu[] items = new SplashGpu[256];
    float[] keys = new float[256];
    int count;
    ShipLights lights = ShipLights.Flat;
    Params prms;
    Vector4 shadowView;

    public SplashBook Book { get; } = new();

    /// <summary>The instances this frame (tests, stats).</summary>
    public ReadOnlySpan<SplashGpu> Items => items.AsSpan(0, count);

    struct ViewParams
    {
        public Vector4 Right, Up, Fwd, Depth, East, North, Shadow;
    }

    struct Params
    {
        public Vector4 SunL, MoonL, ShadowL, Misc;
    }

    public SplashRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/splash.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fsColumn = GpuShader.Load(device, "Content/Shaders/Compiled/splash_column.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 2);
        fsShadow = GpuShader.Load(device, "Content/Shaders/Compiled/splash_shadow.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 2);
        fsFoam = GpuShader.Load(device, "Content/Shaders/Compiled/splash_foam.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 0);
        fsStain = GpuShader.Load(device, "Content/Shaders/Compiled/splash_stain.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 2);
        if (vs == null || fsColumn == null || fsShadow == null || fsFoam == null || fsStain == null)
            throw new InvalidOperationException("splash shaders failed to load");
        buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, BufferBytes);
    }

    // ------------------------------------------------------------------ the CPU side

    /// <summary>A frame: the book updated from the snapshot, then the splashes in view as instances. <paramref name="speed"/>
    /// is the clock's (0 paused), <paramref name="dtS"/> the frame's real seconds, <paramref name="world"/> the view's
    /// <see cref="WorldView.Factor(in MapView)"/>.</summary>
    public void Build(in MapView view, Snapshot s, long drawMs, double speed, double dtS, int selected, double world, ShipLights frameLights)
    {
        Book.Update(s.Splashes, drawMs, speed, dtS, selected);
        lights = frameLights;
        count = 0;
        if (world <= 0 || !SplashLook.On)
            return;

        // the light in the view's own frame (east, north, up at its centre: a view of the world spans a few km)
        var up = (-view.Forward).Normalized();
        var (east, north) = (view.Right, view.Up);
        Vector3 Local(Vec3d d) => new((float)Vec3d.Dot(d, east), (float)Vec3d.Dot(d, north), (float)Vec3d.Dot(d, up));
        var sunL = Local(lights.SunDir);
        var moonL = Local(lights.MoonDir);
        var l = Local(lights.ShadowDir);
        // the shadows: away from the light, their drift per metre of height capped (a sun under ~7° throws them no
        // longer: the quads would grow long for little), fading out under 7° and gone at 3°
        double sinLo = Math.Sin(3 * Math.PI / 180), sinHi = Math.Sin(7 * Math.PI / 180);
        float on = (float)(lights.ShadowOpacity * SmoothStep(sinLo, sinHi, l.Z));
        var drift = l.Z > 1e-3 ? -new Vector2(l.X, l.Y) / l.Z : Vector2.Zero;
        if (drift.Length() > MaxDrift)
            drift *= MaxDrift / drift.Length();
        shadowView = new Vector4(drift, on > 0.002f ? 1 : 0, 0);
        double mpp = view.MetresPerPixel, k = Effects.K;
        prms = new Params
        {
            SunL = new Vector4(sunL, 0), MoonL = new Vector4(moonL, 0), ShadowL = new Vector4(drift, on, 0),
            Misc = new Vector4((float)k, (float)mpp, (float)SplashLook.Steps, (float)SplashLook.Vivid),
        };

        var book = Book;
        double colClock = book.ColumnClock, foamClock = book.FoamClock;
        for (int i = 0; i < book.Count; i++)
        {
            ref readonly var e = ref book[i];
            double cal = e.CalibreMm / 1000;
            double steep = SmoothStep(3, 15, e.FallDeg), vary = SplashLook.Vary;
            double h = HeightPerM * cal * (0.5 + 0.5 * steep) * (1 + vary * VaryHeight * SplashBook.Signed(e.Seed, 1));
            double w0 = WidthPerM * cal * (1 + vary * VaryWidth * SplashBook.Signed(e.Seed, 2)) * (e.TorpedoHit ? TorpedoWidth : 1);
            double ta = Math.Sqrt(2 * h / Gravity) * (1 + vary * VaryTime * SplashBook.Signed(e.Seed, 3));
            double delay = vary * DelayS * (0.5 + 0.5 * SplashBook.Signed(e.Seed, 4));
            double tau = (colClock - e.ColumnT0 - delay) / ta, foamAge = foamClock - e.FoamT0 - delay;
            bool column = e.Column && tau >= 0 && tau < 4.5;
            if (!column && (foamAge < 0 || foamAge > SplashBook.KeepS))
                continue;
            // in view: the foam round the base and the shadow off it, or the column up the screen (north) from it
            if (!view.Project(e.Point, out double x, out double y))
                continue;
            double aroundPx = (3.4 * w0 + (column ? drift.Length() * h : 0)) / mpp + 4, upPx = column ? h * (k + 0.5) / mpp : 0;
            if (x < -aroundPx || x > view.Width + aroundPx || y < -aroundPx || y > view.Height + aroundPx + upPx)
                continue;
            // the size floor: a column under FloorPx wide is drawn that wide, as much fainter
            double floor = Math.Max(1, SplashLook.FloorPx * mpp / Math.Max(w0, 1e-3));
            // the lean along the shell's path: the most at a shallow fall, eased away at point blank (the user, 2026-10-15:
            // right from ~6° of fall up, too much under it)
            double leanDeg = LeanDeg * (1 - steep) * SmoothStep(1.5, 6, e.FallDeg);
            double lean = Math.Tan(leanDeg * Math.PI / 180), b = e.BearingDeg * Math.PI / 180;
            var dye = e.Dye != Dye.None ? e.Dye : SplashLook.AutoDye ? (Dye)(1 + (uint)e.Shooter % 7) : Dye.None;
            var rel = e.Point - view.Eye;
            if (count == items.Length)
            {
                Array.Resize(ref items, count * 2);
                Array.Resize(ref keys, count * 2);
            }
            items[count] = new SplashGpu
            {
                Base = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, e.Seed),
                Shape = new Vector4((float)h, (float)(w0 * floor), (float)tau, (float)ta),
                Lean = new Vector4((float)(lean * Math.Sin(b)), (float)(lean * Math.Cos(b)), (float)(world / floor), column ? 1 : 0),
                Dye = e.TorpedoHit ? TorpedoGrey : new Vector4(SplashLook.Palette[(int)dye], dye == Dye.None ? 0 : 1),
                Foam = new Vector4((float)foamAge, e.FoamStrength, (float)w0, (float)world),
            };
            keys[count] = -(float)Vec3d.Dot(rel, north);   // north first: the effects camera looks north, so they're furthest
            count++;
        }
        Array.Sort(keys, items, 0, count);
    }

    static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    // ------------------------------------------------------------------ the GPU side

    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        uploaded = Math.Min(count, (int)(BufferBytes / SplashGpu.SizeInBytes));
        if (uploaded > 0)
            buf.Upload(cmd, items.AsSpan(0, uploaded));
    }

    void EnsurePipelines(RenderTarget target)
    {
        if (columnPipe != null && pipeFormat == target.ColorFormat && pipeSamples == target.SampleCount && pipeDepth == target.HasDepth)
            return;
        ReleaseMain();
        (pipeFormat, pipeSamples, pipeDepth) = (target.ColorFormat, target.SampleCount, target.HasDepth);
        GpuPipelineBuilder B()
        {
            var b = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
                .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingPremultiplied().ClearVertexLayout();
            return target.HasDepth
                ? b.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
                : b.NoDepthTarget().DisableDepthTest();
        }
        columnPipe = B().SetShaders(vs, fsColumn).Build(device);
        shadowPipe = B().SetShaders(vs, fsShadow).Build(device);
        stainPipe = B().SetShaders(vs, fsStain).Build(device);
        if (columnPipe == null || shadowPipe == null || stainPipe == null)
            throw new InvalidOperationException($"splash pipelines: {SDL_GetError()}");
    }

    void ReleaseMain()
    {
        foreach (var p in (Span<nint>)[(nint)columnPipe, (nint)shadowPipe, (nint)stainPipe])
            if (p != 0)
                SDL_ReleaseGPUGraphicsPipeline(device, (SDL_GPUGraphicsPipeline*)p);
        columnPipe = shadowPipe = stainPipe = null;
    }

    ViewParams View(in MapView view, int mode)
    {
        var (near, far) = GlobeRenderer.DepthRange(view);
        return new ViewParams
        {
            Right = new Vector4(F(view.Right), (float)view.TanHalfX),
            Up = new Vector4(F(view.Up), (float)view.TanHalfY),
            Fwd = new Vector4(F(view.Forward), 0),
            Depth = new Vector4((float)near, (float)far, 0, mode),
            East = new Vector4(F(view.Right), (float)Effects.K),
            North = new Vector4(F(view.Up), 0),
            Shadow = shadowView,
        };
    }

    void Pass(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, SDL_GPUGraphicsPipeline* pipe, in MapView view, int mode, bool uniforms)
    {
        SDL_BindGPUGraphicsPipeline(pass, pipe);
        var sb = buf.Buffer;
        SDL_BindGPUVertexStorageBuffers(pass, 0, &sb, 1);
        var vp = View(view, mode);
        SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&vp), (uint)sizeof(ViewParams));
        if (uniforms)
        {
            var lu = lights.Uniforms;
            var pr = prms;
            SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&lu), (uint)sizeof(LightUniforms));
            SDL_PushGPUFragmentUniformData(cmd, 1, (nint)(&pr), (uint)sizeof(Params));
        }
        SDL_DrawGPUPrimitives(pass, 6, (uint)uploaded, 0, 0);
    }

    /// <summary>The foam into the wake target (its own format, one sample, no depth): by the most, as the lanes'.</summary>
    public void DrawFoam(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0 || (SplashLook.Parts & 1) == 0)
            return;
        if (foamPipe == null || foamFormat != target.ColorFormat)
        {
            if (foamPipe != null)
                SDL_ReleaseGPUGraphicsPipeline(device, foamPipe);
            foamFormat = target.ColorFormat;
            foamPipe = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
                .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingAddMaxAlpha().NoDepthTarget().DisableDepthTest()
                .ClearVertexLayout().SetShaders(vs, fsFoam).Build(device);
            if (foamPipe == null)
                throw new InvalidOperationException($"splash foam pipeline: {SDL_GetError()}");
        }
        Pass(cmd, pass, foamPipe, view, 2, false);
    }

    /// <summary>The dye's stains on the water, after the sea and before the ships.</summary>
    public void DrawStains(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0)
            return;
        EnsurePipelines(target);
        if ((SplashLook.Parts & 2) != 0)
            Pass(cmd, pass, stainPipe, view, 3, true);
    }

    /// <summary>The shadows on the sea and the decks, after the ships and before any column or cloud stands over them.</summary>
    public void DrawShadows(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0 || shadowView.Z <= 0 || (SplashLook.Parts & 4) == 0)
            return;
        EnsurePipelines(target);
        Pass(cmd, pass, shadowPipe, view, 1, true);
    }

    /// <summary>The columns, after the ships and before the smoke and the magazine explosions, which hide them.</summary>
    public void DrawColumns(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0)
            return;
        EnsurePipelines(target);
        if ((SplashLook.Parts & 8) != 0)
            Pass(cmd, pass, columnPipe, view, 0, true);
    }

    static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);

    public void Dispose()
    {
        SDL_WaitForGPUIdle(device);
        ReleaseMain();
        if (foamPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, foamPipe);
        buf.Dispose();
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fsColumn);
        SDL_ReleaseGPUShader(device, fsShadow);
        SDL_ReleaseGPUShader(device, fsFoam);
        SDL_ReleaseGPUShader(device, fsStain);
    }
}
