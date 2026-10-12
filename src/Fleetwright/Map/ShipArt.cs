using System.Collections.Concurrent;
using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using Fleetwright.Sim.Ships;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>One class's art on the GPU (docs/battle/plan.md "The shape": ShipClassArt): the hull, its height map and
/// each turret type's image, mipmapped, and the sprite's layout. The CPU images are gone once these exist.</summary>
public sealed unsafe class ShipArt
{
    public required SpriteMeta Meta { get; init; }
    public required nint Hull { get; init; }
    public required nint Height { get; init; }

    /// <summary>Each turret type's texture, in the order of <see cref="TurretTypes"/>.</summary>
    public required nint[] Turrets { get; init; }
    public required string[] TurretTypes { get; init; }

    /// <summary>Each mount's turret type (an index into <see cref="Turrets"/>), and the mounts by ascending z.</summary>
    public required int[] MountType { get; init; }
    public required int[] DrawOrder { get; init; }

    /// <summary>Sprite pixels per metre at mip 0, and the mip levels below it.</summary>
    public double Scale => Meta.ScalePxPerM;
    public int MipLevels { get; init; }

    /// <summary>The canvas in metres (the sprite's full extent).</summary>
    public double CanvasX => Meta.SizePx[0] / Scale;
    public double CanvasY => Meta.SizePx[1] / Scale;

    /// <summary>The fewest screen pixels per metre the mip chain draws cleanly (its last level, sampled at half).</summary>
    public double MinPxPerM => Scale / Math.Pow(2, MipLevels) / 2;

    internal void Release(SDL_GPUDevice* device)
    {
        SDL_ReleaseGPUTexture(device, (SDL_GPUTexture*)Hull);
        SDL_ReleaseGPUTexture(device, (SDL_GPUTexture*)Height);
        foreach (var t in Turrets)
            SDL_ReleaseGPUTexture(device, (SDL_GPUTexture*)t);
    }
}

/// <summary>The art of every class in play, by class registry id (the look is the design's own; navies' looks come
/// later). It hangs on the registry's <see cref="ShipClassRegistry.Built"/>: each design's sprites are drawn and baked
/// on the building thread, from the very <see cref="Ship"/> the class was compiled from, then wait for the render
/// thread, which uploads them (<see cref="UploadPending"/>) and lets the CPU images go. Until a class's art is up, its
/// ships draw as icons.</summary>
public sealed unsafe class ShipArtLibrary : IDisposable
{
    /// <summary>Sprite pixels per metre at mip 0: the map's closest zoom is 10 px/m.</summary>
    public const double SpriteScale = 10;

    /// <summary>Mip levels below 0, down to 10/128 px/m: a 250 m ship at ~25 px. Below that it's an icon.</summary>
    public const int MipLevels = 7;

    readonly SDL_GPUDevice* device;
    readonly ConcurrentQueue<(int Id, ShipSprites Sprites, SpriteImages Images)> pending = new();
    readonly Dictionary<int, ShipArt> art = [];
    readonly ThreadLocal<GpuBaker> bakers;

    public ShipArtLibrary(SDL_GPUDevice* device, ShipClassRegistry registry)
    {
        this.device = device;
        bakers = new ThreadLocal<GpuBaker>(() => new GpuBaker(device), trackAllValues: true);
        registry.Built += Draw;
    }

    /// <summary>A class's art, or null while it's being drawn (or if it couldn't be).</summary>
    public ShipArt? Get(int id) => art.GetValueOrDefault(id);

    /// <summary>Classes drawn and waiting for the render thread.</summary>
    public int Pending => pending.Count;

    /// <summary>On a building thread: draws and bakes a design's sprites, mipmapped.</summary>
    void Draw(int id, Ship ship, ShipClass? cls)
    {
        if (cls == null)
            return;
        var sprites = ShipSprites.Build(ship, SpriteScale, MipLevels);
        var baked = ShipBake.Bake(sprites, bakers.Value!);
        pending.Enqueue((id, sprites, new SpriteImages
        {
            Hull = SpriteImages.Chain(baked.Hull, false, MipLevels),
            Height = SpriteImages.Chain(baked.Height, true, MipLevels),
            Turrets = baked.Turrets.ToDictionary(t => t.Key, t => SpriteImages.Chain(t.Value, false, MipLevels), StringComparer.Ordinal),
        }));
    }

    /// <summary>On the render thread, between frames: uploads up to <paramref name="max"/> classes' art.</summary>
    public void UploadPending(int max = 2)
    {
        for (int k = 0; k < max && pending.TryDequeue(out var p); k++)
        {
            var meta = p.Sprites.Meta;
            var types = meta.TurretTypes.Keys.ToArray();
            var mounts = meta.Mounts;
            var a = new ShipArt
            {
                Meta = meta,
                Hull = (nint)SpriteTextures.Upload(device, p.Images.Hull, height: false),
                Height = (nint)SpriteTextures.Upload(device, p.Images.Height, height: true),
                TurretTypes = types,
                Turrets = [.. types.Select(t => (nint)SpriteTextures.Upload(device, p.Images.Turrets[t], height: false))],
                MountType = [.. mounts.Select(m => Array.IndexOf(types, m.Type))],
                DrawOrder = [.. Enumerable.Range(0, mounts.Count).OrderBy(i => mounts[i].Z)],
                MipLevels = p.Images.Hull.Length - 1,
            };
            if (art.Remove(p.Id, out var old))
                old.Release(device);
            art[p.Id] = a;
        }
    }

    public void Dispose()
    {
        SDL_WaitForGPUIdle(device);
        foreach (var a in art.Values)
            a.Release(device);
        art.Clear();
        foreach (var b in bakers.Values)
            b.Dispose();
        bakers.Dispose();
    }
}

/// <summary>A mip chain into a sampled GPU texture: RGBA (premultiplied) or a one-channel height map.</summary>
public static unsafe class SpriteTextures
{
    public static SDL_GPUTexture* Upload(SDL_GPUDevice* device, Image8[] levels, bool height)
    {
        var level0 = levels[0];
        var info = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = height ? SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8_UNORM : SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
            width = (uint)level0.Width,
            height = (uint)level0.Height,
            layer_count_or_depth = 1,
            num_levels = (uint)levels.Length,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
        };
        var tex = SDL_CreateGPUTexture(device, &info);
        if (tex == null)
            throw new InvalidOperationException($"sprite texture: {SDL_GetError()}");
        uint total = 0;
        foreach (var l in levels)
            total += (uint)l.Data.Length;
        var ti = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, size = total };
        var tb = SDL_CreateGPUTransferBuffer(device, &ti);
        var dst = (byte*)SDL_MapGPUTransferBuffer(device, tb, false);
        uint off = 0;
        foreach (var l in levels)
        {
            l.Data.CopyTo(new Span<byte>(dst + off, l.Data.Length));
            off += (uint)l.Data.Length;
        }
        SDL_UnmapGPUTransferBuffer(device, tb);
        var cmd = SDL_AcquireGPUCommandBuffer(device);
        var cp = SDL_BeginGPUCopyPass(cmd);
        off = 0;
        for (int k = 0; k < levels.Length; k++)
        {
            var l = levels[k];
            var src = new SDL_GPUTextureTransferInfo { transfer_buffer = tb, offset = off, pixels_per_row = (uint)l.Width, rows_per_layer = (uint)l.Height };
            var region = new SDL_GPUTextureRegion { texture = tex, mip_level = (uint)k, w = (uint)l.Width, h = (uint)l.Height, d = 1 };
            SDL_UploadToGPUTexture(cp, &src, &region, false);
            off += (uint)l.Data.Length;
        }
        SDL_EndGPUCopyPass(cp);
        SDL_SubmitGPUCommandBuffer(cmd);
        SDL_ReleaseGPUTransferBuffer(device, tb);
        return tex;
    }
}
