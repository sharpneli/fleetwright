using SDL;
using static SDL.SDL3;

namespace Fleetwright.Gpu;

/// <summary>
/// A place a view draws into: a colour texture (multisampled when <see cref="SampleCount"/> is above 1, resolving into
/// <see cref="Resolve"/>), an optional D32 depth texture of the same sample count, and their size. The engine's main
/// target is one; an offscreen one can be shown as an ImGui image or on a game UI quad by sampling <see cref="Resolve"/>.
/// A renderer reads the formats and sample count to build matching pipelines and never assumes the window's.
/// </summary>
public sealed unsafe class RenderTarget : IDisposable
{
    readonly SDL_GPUDevice* device;

    /// <summary>The pass's colour attachment: multisampled when SampleCount is above 1, else the same as Resolve.</summary>
    public SDL_GPUTexture* Color { get; private set; }

    /// <summary>The single-sample result, sampled afterwards (blit, ImGui.Image, a UI quad).</summary>
    public SDL_GPUTexture* Resolve { get; private set; }

    /// <summary>D32 depth at the colour's sample count, or null for a target without depth.</summary>
    public SDL_GPUTexture* Depth { get; private set; }

    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public SDL_GPUTextureFormat ColorFormat { get; }
    public SDL_GPUSampleCount SampleCount { get; }
    public bool HasDepth { get; }
    public const SDL_GPUTextureFormat DepthFormat = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D32_FLOAT;

    public bool Multisampled => SampleCount != SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1;

    public RenderTarget(SDL_GPUDevice* device, uint width, uint height, SDL_GPUTextureFormat colorFormat,
        SDL_GPUSampleCount sampleCount = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1, bool depth = true)
    {
        this.device = device;
        ColorFormat = colorFormat;
        SampleCount = sampleCount;
        HasDepth = depth;
        Create(width, height);
    }

    /// <summary>Recreates the textures at a new size (waits for the GPU). Formats and sample count stay.</summary>
    public void Resize(uint width, uint height)
    {
        if (width == 0 || height == 0 || (width == Width && height == Height))
            return;
        SDL_WaitForGPUIdle(device);
        Release();
        Create(width, height);
    }

    /// <summary>Begins a render pass on this target with the viewport over all of it. Clears colour to
    /// <paramref name="clear"/> and depth to 1 (far); the colour is resolved (or stored) at the pass's end.
    /// <paramref name="depth"/> false leaves the depth texture out, for pipelines built without one.</summary>
    public SDL_GPURenderPass* BeginPass(SDL_GPUCommandBuffer* cmd, SDL_FColor clear, bool depth = true)
    {
        var cti = new SDL_GPUColorTargetInfo
        {
            texture = Color,
            clear_color = clear,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = Multisampled ? SDL_GPUStoreOp.SDL_GPU_STOREOP_RESOLVE : SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
            resolve_texture = Multisampled ? Resolve : null,
            cycle = true,
            cycle_resolve_texture = Multisampled,
        };
        var dti = new SDL_GPUDepthStencilTargetInfo
        {
            texture = Depth,
            clear_depth = 1.0f,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,
            stencil_load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE,
            stencil_store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,
            cycle = true,
        };
        var pass = SDL_BeginGPURenderPass(cmd, &cti, 1, HasDepth && depth ? &dti : null);
        var viewport = new SDL_GPUViewport { w = Width, h = Height, min_depth = 0, max_depth = 1 };
        SDL_SetGPUViewport(pass, &viewport);
        return pass;
    }

    void Create(uint width, uint height)
    {
        Width = width;
        Height = height;
        var sampled = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_COLOR_TARGET | SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER;
        Resolve = Texture(ColorFormat, sampled, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1);
        Color = Multisampled ? Texture(ColorFormat, SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_COLOR_TARGET, SampleCount) : Resolve;
        Depth = HasDepth ? Texture(DepthFormat, SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET, SampleCount) : null;
    }

    SDL_GPUTexture* Texture(SDL_GPUTextureFormat format, SDL_GPUTextureUsageFlags usage, SDL_GPUSampleCount samples)
    {
        var info = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = format,
            width = Width,
            height = Height,
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = samples,
            usage = usage,
        };
        var tex = SDL_CreateGPUTexture(device, &info);
        if (tex == null)
            throw new InvalidOperationException($"render target texture: {SDL_GetError()}");
        return tex;
    }

    void Release()
    {
        if (Color != null && Color != Resolve)
            SDL_ReleaseGPUTexture(device, Color);
        if (Resolve != null)
            SDL_ReleaseGPUTexture(device, Resolve);
        if (Depth != null)
            SDL_ReleaseGPUTexture(device, Depth);
        Color = Resolve = Depth = null;
    }

    public void Dispose() => Release();
}
