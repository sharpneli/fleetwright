using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Sim.Core;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The sea's waves on the GPU (docs/visuals/sea.md W2): three FFT cascades of <see cref="N"/>², evolved to the
/// sea's clock and transformed by two compute dispatches (sea_rows: spectrum and the rows' inverse FFT; sea_columns:
/// the columns' and the combine), then mipmapped, so the shader filters unresolved waves into roughness (LEAN). Per
/// cascade it leaves a moments texture (sx, sy, sx², sy²) and a displacement texture (Dx, Dy, h, J), RGBA16F with
/// mips, in the sea frame's axes (x east, y north at its anchor). The initial spectrum (<see cref="SeaSpectrum"/>) is
/// uploaded only when the sea state changes. Nothing here allocates per frame.</summary>
public sealed unsafe class SeaSim : IDisposable
{
    public const int N = 128;
    public const int Levels = 8;   // log2(N) + 1
    public const double PeriodS = 1024;   // the clock wraps; the dispersion is quantised to 2π over it

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUComputePipeline* rows, columns;
    readonly nint[] h0 = new nint[3], a = new nint[3], b = new nint[3], moments = new nint[3], disp = new nint[3];
    readonly SDL_GPUTransferBuffer* transfer;
    SeaState? uploaded;
    bool pending;

    /// <summary>The spectrum last set, and each cascade's statistics as uploaded.</summary>
    public SeaSpectrum? Spectrum { get; private set; }
    public CascadeStats[] Stats { get; } = new CascadeStats[3];

    /// <summary>Each cascade's moments and displacement, for the shader.</summary>
    public SDL_GPUTexture* Moments(int c) => (SDL_GPUTexture*)moments[c];
    public SDL_GPUTexture* Displacement(int c) => (SDL_GPUTexture*)disp[c];

    [StructLayout(LayoutKind.Sequential)]
    struct RowParams
    {
        public float L0, L1, L2, Clock, W0, P1, P2, P3;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ColumnParams
    {
        public float Chop, P1, P2, P3;
    }

    public SeaSim(SDL_GPUDevice* device)
    {
        this.device = device;
        rows = GpuShader.LoadCompute(device, "Content/Shaders/Compiled/sea_rows.comp.spv", 0, 3, 6, 1, N);
        columns = GpuShader.LoadCompute(device, "Content/Shaders/Compiled/sea_columns.comp.spv", 0, 6, 6, 1, N);
        if (rows == null || columns == null)
            throw new InvalidOperationException("sea compute shaders failed to load");
        const SDL_GPUTextureUsageFlags Read = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_COMPUTE_STORAGE_READ,
            Write = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_COMPUTE_STORAGE_WRITE,
            Sampler = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
            Target = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_COLOR_TARGET;
        const SDL_GPUTextureFormat F32 = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R32G32B32A32_FLOAT,
            F16 = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R16G16B16A16_FLOAT;
        for (int c = 0; c < 3; c++)
        {
            h0[c] = Texture(F32, Read, 1);
            a[c] = Texture(F32, Read | Write, 1);
            b[c] = Texture(F32, Read | Write, 1);
            // sampled by the globe; a colour target so SDL can blit the mips; written by sea_columns
            moments[c] = Texture(F16, Sampler | Target | Write, Levels);
            disp[c] = Texture(F16, Sampler | Target | Write, Levels);
        }
        var ti = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, size = 3 * N * N * 16 };
        transfer = SDL_CreateGPUTransferBuffer(device, &ti);
    }

    nint Texture(SDL_GPUTextureFormat format, SDL_GPUTextureUsageFlags usage, int levels)
    {
        var info = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D, format = format, usage = usage,
            width = N, height = N, layer_count_or_depth = 1, num_levels = (uint)levels,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
        };
        var t = SDL_CreateGPUTexture(device, &info);
        if (t == null)
            throw new InvalidOperationException($"sea texture: {SDL_GetError()}");
        return (nint)t;
    }

    /// <summary>Sets the sea state: rebuilds the initial spectrum (the same phases, new amplitudes) into the transfer
    /// buffer for the next <see cref="Simulate"/>, unless it's the state already up.</summary>
    public void SetState(in SeaState s)
    {
        if (uploaded == s)
            return;
        uploaded = s;
        Spectrum = new SeaSpectrum(s);   // a change of weather, not a frame
        var dst = (float*)SDL_MapGPUTransferBuffer(device, transfer, true);
        for (int c = 0; c < 3; c++)
            Stats[c] = Spectrum.BuildH0(c, N, new Span<float>(dst + c * N * N * 4, N * N * 4));
        SDL_UnmapGPUTransferBuffer(device, transfer);
        pending = true;
    }

    /// <summary>Evolves the waves to <paramref name="clockS"/> (seconds within <see cref="PeriodS"/>) with choppiness
    /// <paramref name="chop"/>: compute passes and the mips, recorded into <paramref name="cmd"/> before any render pass.</summary>
    public void Simulate(SDL_GPUCommandBuffer* cmd, double clockS, float chop)
    {
        if (pending)
        {
            var cp = SDL_BeginGPUCopyPass(cmd);
            for (int c = 0; c < 3; c++)
            {
                var src = new SDL_GPUTextureTransferInfo { transfer_buffer = transfer, offset = (uint)(c * N * N * 16), pixels_per_row = N, rows_per_layer = N };
                var region = new SDL_GPUTextureRegion { texture = (SDL_GPUTexture*)h0[c], w = N, h = N, d = 1 };
                SDL_UploadToGPUTexture(cp, &src, &region, false);
            }
            SDL_EndGPUCopyPass(cp);
            pending = false;
        }
        if (Spectrum == null)
            return;

        var rw = stackalloc SDL_GPUStorageTextureReadWriteBinding[6];
        var ro = stackalloc SDL_GPUTexture*[6];
        for (int c = 0; c < 3; c++)
        {
            rw[2 * c] = new SDL_GPUStorageTextureReadWriteBinding { texture = (SDL_GPUTexture*)a[c], cycle = true };
            rw[2 * c + 1] = new SDL_GPUStorageTextureReadWriteBinding { texture = (SDL_GPUTexture*)b[c], cycle = true };
            ro[c] = (SDL_GPUTexture*)h0[c];
        }
        var rp = new RowParams
        {
            L0 = (float)SeaSpectrum.TileM[0], L1 = (float)SeaSpectrum.TileM[1], L2 = (float)SeaSpectrum.TileM[2],
            Clock = (float)clockS, W0 = (float)(2 * Math.PI / PeriodS),
        };
        var pass = SDL_BeginGPUComputePass(cmd, rw, 6, null, 0);
        SDL_BindGPUComputePipeline(pass, rows);
        SDL_BindGPUComputeStorageTextures(pass, 0, ro, 3);
        SDL_PushGPUComputeUniformData(cmd, 0, (nint)(&rp), (uint)sizeof(RowParams));
        SDL_DispatchGPUCompute(pass, 1, N, 3);
        SDL_EndGPUComputePass(pass);

        for (int c = 0; c < 3; c++)
        {
            // not cycled: SDL's mip blits on a cycled texture stalled the frame (8.3 -> 15.6 ms at 120 Hz); written in
            // place, the queue orders it after the last frame's reads
            rw[2 * c] = new SDL_GPUStorageTextureReadWriteBinding { texture = (SDL_GPUTexture*)moments[c] };
            rw[2 * c + 1] = new SDL_GPUStorageTextureReadWriteBinding { texture = (SDL_GPUTexture*)disp[c] };
            ro[2 * c] = (SDL_GPUTexture*)a[c];
            ro[2 * c + 1] = (SDL_GPUTexture*)b[c];
        }
        var cpar = new ColumnParams { Chop = chop };
        pass = SDL_BeginGPUComputePass(cmd, rw, 6, null, 0);
        SDL_BindGPUComputePipeline(pass, columns);
        SDL_BindGPUComputeStorageTextures(pass, 0, ro, 6);
        SDL_PushGPUComputeUniformData(cmd, 0, (nint)(&cpar), (uint)sizeof(ColumnParams));
        SDL_DispatchGPUCompute(pass, N, 1, 3);
        SDL_EndGPUComputePass(pass);

        for (int c = 0; c < 3; c++)
        {
            SDL_GenerateMipmapsForGPUTexture(cmd, (SDL_GPUTexture*)moments[c]);
            SDL_GenerateMipmapsForGPUTexture(cmd, (SDL_GPUTexture*)disp[c]);
        }
    }

    public void Dispose()
    {
        foreach (var arr in new[] { h0, a, b, moments, disp })
            foreach (var t in arr)
                if (t != 0)
                    SDL_ReleaseGPUTexture(device, (SDL_GPUTexture*)t);
        SDL_ReleaseGPUTransferBuffer(device, transfer);
        SDL_ReleaseGPUComputePipeline(device, rows);
        SDL_ReleaseGPUComputePipeline(device, columns);
    }
}
