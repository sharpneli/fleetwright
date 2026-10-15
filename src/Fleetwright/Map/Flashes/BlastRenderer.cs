using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Sim.Core;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map.Flashes;

/// <summary>One mount's blast on the water as muzzleblast.vert/.frag read it (std430: 4 vec4s).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct BlastGpu
{
    /// <summary>xyz: the barrel tips' middle, metres from the eye; w: their height over the sea, m.</summary>
    public Vector4 Base;
    /// <summary>xy: the bore's way over the sea (east, north), unit; z: sin(elevation); w: cos(elevation).</summary>
    public Vector4 Bore;
    /// <summary>x: the blast's length λ, m (the Mach stem's share in); y: its age on the sea's clock, s; z: its strength
    /// (the sea's, the world's share); w: seed.</summary>
    public Vector4 Blast;
    /// <summary>x: the quad's half size, m; y: its centre's shift ahead along the bore, m.</summary>
    public Vector4 Quad;
    public const int SizeInBytes = 64;
}

/// <summary>The muzzle blast on the water (docs/visuals/muzzle-flashes.md F5; research/vfx/muzzle-blast-water-vfx.md),
/// one quad per mount fired into the wake target in its pre-pass, flat on the sea: the overpressure from Fansler's fit
/// with its directivity, the front arriving at its Rankine-Hugoniot time, behind it a thin dark edge (the ripples laid
/// down: variance taken off) and the frost (roughened: variance added), and near the muzzle the scour foam. The sea's
/// shading turns them into glitter and foam as it does the wakes'. Two mounts' fronts meeting make Iowa's bright seam by
/// themselves (the variance adds). On the sea's clock; display only.</summary>
public sealed unsafe class BlastRenderer : IDisposable
{
    const uint BufferBytes = 256 << 10;
    public const double C0 = 343, Gamma = 1.4;
    /// <summary>Fansler's directivity's μ.</summary>
    public const double Mu = 0.78;
    /// <summary>The arrival table: scaled distance r/λ' from <see cref="RhoMin"/> by factors of <see cref="RhoStep"/>.</summary>
    public const int ArrivalN = 16;
    public const double RhoMin = 0.05, RhoMax = 50;
    public static readonly double RhoStep = Math.Pow(RhoMax / RhoMin, 1.0 / (ArrivalN - 1));

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fs;
    readonly DynamicGpuBuffer buf;
    SDL_GPUGraphicsPipeline* pipe;
    SDL_GPUTextureFormat pipeFormat;
    int uploaded;
    BlastGpu[] items = new BlastGpu[64];
    int count;
    Params prms;

    /// <summary>The instances this frame (tests, stats).</summary>
    public ReadOnlySpan<BlastGpu> Items => items.AsSpan(0, count);

    struct ViewParams
    {
        public Vector4 Right, Up, Fwd, Depth, East, North;
    }

    struct Params
    {
        /// <summary>The front's arrival, in units of λ'/c₀, at the table's scaled distances.</summary>
        public Vector4 Arr0, Arr1, Arr2, Arr3;
        /// <summary>x: p_vis/p₀; y: p_sat/p₀; z: τ_frost, s; w: τ_foam, s.</summary>
        public Vector4 Misc0;
        /// <summary>x: the wind's mean-square slope (Cox-Munk); y: k_foam; z: ln RhoMin; w: ln RhoStep.</summary>
        public Vector4 Misc1;
    }

    static readonly float[] Arrival = ArrivalTable();

    /// <summary>The front's arrival at scaled distance ρ = r/λ', in units of λ'/c₀: ∫ dρ / (U_s/c₀) from the muzzle,
    /// U_s by Rankine-Hugoniot on the overpressure Δp/p₀ = 0.11/ρ + 0.0061/ρ² (the reference's <c>arrival_lut</c>), at
    /// the table's distances.</summary>
    static float[] ArrivalTable()
    {
        var t = new float[ArrivalN];
        double acc = 0, rho = 0;
        for (int i = 0; i < ArrivalN; i++)
        {
            double to = RhoMin * Math.Pow(RhoStep, i);
            const int Steps = 400;
            double h = (to - rho) / Steps;
            for (int k = 0; k < Steps; k++)
            {
                double a = rho + h * k, b = a + h;
                acc += 0.5 * h * (InvSpeed(a) + InvSpeed(b));
            }
            rho = to;
            t[i] = (float)acc;
        }
        return t;
    }

    /// <summary>c₀ over the front's speed at scaled distance ρ.</summary>
    public static double InvSpeed(double rho)
    {
        if (rho <= 1e-9)
            return 0;
        double x = 1 / rho, dp = 0.11 * x + 0.0061 * x * x;
        return 1 / Math.Sqrt(1 + (Gamma + 1) / (2 * Gamma) * dp);
    }

    /// <summary>The front's arrival at scaled distance ρ, units of λ'/c₀ (the table, as the shader reads it).</summary>
    public static double ArrivalAt(double rho)
    {
        if (rho <= RhoMin)
            return Arrival[0] * rho / RhoMin;
        if (rho >= RhoMax)
            return Arrival[^1] + (rho - RhoMax) * InvSpeed(RhoMax);
        // the node below by its log, then linear in rho between the two (the arrival is nearly linear in rho)
        int i = Math.Min((int)(Math.Log(rho / RhoMin) / Math.Log(RhoStep)), ArrivalN - 2);
        double r0 = RhoMin * Math.Pow(RhoStep, i);
        return Arrival[i] + (Arrival[i + 1] - Arrival[i]) * (rho - r0) / (r0 * (RhoStep - 1));
    }

    public BlastRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/muzzleblast.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fs = GpuShader.Load(device, "Content/Shaders/Compiled/muzzleblast.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 1, 0, 1);
        if (vs == null || fs == null)
            throw new InvalidOperationException("muzzle blast shaders failed to load");
        buf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, BufferBytes);
    }

    /// <summary>A frame: the book's blasts still marking the water, in view, as instances. <paramref name="windMs"/> is
    /// the weather's wind (the sea's own roughness, which the frost multiplies).</summary>
    public void Build(in MapView view, ShotBook book, double windMs, double world)
    {
        count = 0;
        double pvis = FlashLook.BlastVisPa / Sim.Combat.MuzzleFlash.P0, psat = FlashLook.BlastSatPa / Sim.Combat.MuzzleFlash.P0;
        double frostS = FlashLook.FrostS, foamS = FlashLook.ScourS;
        xVis = (-0.11 + Math.Sqrt(0.11 * 0.11 + 4 * 0.0061 * pvis)) / (2 * 0.0061);
        prms = new Params
        {
            Arr0 = new Vector4(Arrival[0], Arrival[1], Arrival[2], Arrival[3]),
            Arr1 = new Vector4(Arrival[4], Arrival[5], Arrival[6], Arrival[7]),
            Arr2 = new Vector4(Arrival[8], Arrival[9], Arrival[10], Arrival[11]),
            Arr3 = new Vector4(Arrival[12], Arrival[13], Arrival[14], Arrival[15]),
            Misc0 = new Vector4((float)pvis, (float)psat, (float)frostS, (float)foamS),
            Misc1 = new Vector4((float)(0.003 + 0.00512 * Math.Max(windMs, 0)), (float)FlashLook.ScourK, (float)Math.Log(RhoMin), (float)Math.Log(RhoStep)),
        };
        if (world <= 0 || !FlashLook.On || (FlashLook.Parts & 4) == 0)
            return;
        var (east, north) = (view.Right, view.Up);
        // the frost's reach forward (the overpressure at p_vis on the bore, λ' = 1.78 λ) and the scour's
        double clock = book.SeaClock, mpp = view.MetresPerPixel;
        long newest = book.Count > 0 ? book[book.Count - 1].Ms : 0;
        for (int i = 0; i < book.Count; i++)
        {
            ref readonly var e = ref book[i];
            if (FlashLook.BlastAtS >= 0 ? e.Ms < newest - 2000 : !e.Shows)
                continue;   // a still: the newest salvos only; else thinned by the salvo's share, as the flashes are
            double age = FlashLook.BlastAtS >= 0 ? FlashLook.BlastAtS : clock - e.SeaT0;
            if (age < 0 || age > ShotBook.SeaKeepS)
                continue;
            double el = e.ElevationDeg * Math.PI / 180;
            // over the water the blast acts as if doubled inside the Mach stem: low-elevation fire (blast-wave research §0.7)
            double lam = e.BlastLengthM * (1 + 0.26 * SmoothStep(20, 5, e.ElevationDeg));
            bool frost = age < 5 * frostS;
            // the quad over the mark: the frost reaches 1.78 λ/x ahead (Fansler's directivity), 0.22 λ/x behind, 1 λ/x
            // aside; the scour 1.3 k λ' and the jet's lobe 4 λ along the bore
            double scour = 1.3 * FlashLook.ScourK * 1.78 * lam + 4 * lam * Math.Cos(el);
            double ahead = frost ? Math.Max(1.78 * lam / xVis + e.HeightM, scour) : scour;
            double behind = frost ? Math.Max(0.22 * lam / xVis + e.HeightM, 0.4 * scour) : 0.4 * scour;
            double side = frost ? Math.Max(1.0 * lam / xVis + e.HeightM, 0.6 * scour) : 0.6 * scour;
            double half = Math.Max((ahead + behind) / 2, side), shift = (ahead - behind) / 2;
            if (!view.Project(e.Tip, out double x, out double y))
                continue;
            double px = half / mpp;
            if (x < -px || x > view.Width + px || y < -px || y > view.Height + px)
                continue;
            var dir = new Vector2((float)Vec3d.Dot(e.Forward, east), (float)Vec3d.Dot(e.Forward, north));
            dir = dir.LengthSquared() > 1e-12f ? Vector2.Normalize(dir) : Vector2.UnitY;
            var rel = e.Tip - view.Eye;
            if (count == items.Length)
                Array.Resize(ref items, count * 2);
            items[count++] = new BlastGpu
            {
                Quad = new Vector4((float)half, (float)shift, 0, 0),
                Base = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, e.HeightM),
                Bore = new Vector4(dir, (float)Math.Sin(el), (float)Math.Cos(el)),
                Blast = new Vector4((float)lam, (float)age, (float)(e.SeaStrength * world), e.Seed),
            };
        }
    }

    double xVis = 1;

    /// <summary>The spherical blast of a magazine going up (docs/visuals/magazine-explosions.md X5), one more mark on the
    /// water after the frame's build: <paramref name="lambda"/> its λ, <paramref name="heightM"/> its centre over the
    /// sea, <paramref name="age"/> on the sea's clock. Its "bore" points straight up, so the gun's directivity gives a
    /// ring (its 0.62 on the side undone in λ) and no jet's lobe.</summary>
    public void AddSpherical(in MapView view, Vec3d at, double heightM, double lambda, double age, double strength, float seed)
    {
        if (strength <= 0 || age < 0 || age > ShotBook.SeaKeepS || (FlashLook.Parts & 4) == 0)
            return;
        double lam = lambda / 0.62, scour = 1.3 * FlashLook.ScourK * 1.78 * lam;
        double half = Math.Max(age < 5 * FlashLook.FrostS ? 1.0 * lam / xVis + heightM : 0, scour);
        if (!view.Project(at, out double x, out double y))
            return;
        double px = half / view.MetresPerPixel;
        if (x < -px || x > view.Width + px || y < -px || y > view.Height + px)
            return;
        var rel = at - view.Eye;
        if (count == items.Length)
            Array.Resize(ref items, count * 2);
        items[count++] = new BlastGpu
        {
            Quad = new Vector4((float)half, 0, 0, 0),
            Base = new Vector4((float)rel.X, (float)rel.Y, (float)rel.Z, (float)heightM),
            Bore = new Vector4(0, 1, 1, 0),
            Blast = new Vector4((float)lam, (float)age, (float)strength, seed),
        };
    }

    static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        uploaded = Math.Min(count, (int)(BufferBytes / BlastGpu.SizeInBytes));
        if (uploaded > 0)
            buf.Upload(cmd, items.AsSpan(0, uploaded));
    }

    /// <summary>Into the wake target (its own format, one sample, no depth): the variance added, the foam by the most.</summary>
    public void DrawWake(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0)
            return;
        if (pipe == null || pipeFormat != target.ColorFormat)
        {
            if (pipe != null)
                SDL_ReleaseGPUGraphicsPipeline(device, pipe);
            pipeFormat = target.ColorFormat;
            pipe = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
                .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingAddMaxAlpha().NoDepthTarget().DisableDepthTest()
                .ClearVertexLayout().SetShaders(vs, fs).Build(device);
            if (pipe == null)
                throw new InvalidOperationException($"muzzle blast pipeline: {SDL_GetError()}");
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
            East = new Vector4(F(view.Right), 0),
            North = new Vector4(F(view.Up), 0),
        };
        SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&vp), (uint)sizeof(ViewParams));
        var pr = prms;
        SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&pr), (uint)sizeof(Params));
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
