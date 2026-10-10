using SDL;
using static SDL.SDL3;

namespace Fleetwright.Gpu;

/// <summary>GPU buffers from a device alone: one-off static uploads, and a dynamic buffer refilled during a frame.</summary>
public static unsafe class GpuUpload
{
    /// <summary>A buffer holding <paramref name="data"/>, uploaded on its own command buffer (load time, not per frame).</summary>
    public static SDL_GPUBuffer* Buffer<T>(SDL_GPUDevice* device, SDL_GPUBufferUsageFlags usage, ReadOnlySpan<T> data) where T : unmanaged
    {
        uint size = (uint)(data.Length * sizeof(T));
        var info = new SDL_GPUBufferCreateInfo { usage = usage, size = Math.Max(size, 4) };
        var buf = SDL_CreateGPUBuffer(device, &info);
        if (buf == null)
            throw new InvalidOperationException($"GPU buffer: {SDL_GetError()}");
        if (size == 0)
            return buf;
        var ti = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, size = size };
        var tb = SDL_CreateGPUTransferBuffer(device, &ti);
        var dst = (T*)SDL_MapGPUTransferBuffer(device, tb, false);
        data.CopyTo(new Span<T>(dst, data.Length));
        SDL_UnmapGPUTransferBuffer(device, tb);
        var cmd = SDL_AcquireGPUCommandBuffer(device);
        var cp = SDL_BeginGPUCopyPass(cmd);
        var src = new SDL_GPUTransferBufferLocation { transfer_buffer = tb };
        var region = new SDL_GPUBufferRegion { buffer = buf, size = size };
        SDL_UploadToGPUBuffer(cp, &src, &region, false);
        SDL_EndGPUCopyPass(cp);
        SDL_SubmitGPUCommandBuffer(cmd);
        SDL_ReleaseGPUTransferBuffer(device, tb);
        return buf;
    }
}

/// <summary>A GPU buffer with its own transfer buffer, refilled inside a frame's command buffer (before the render
/// pass that reads it). Both cycle, so a refill never waits for the GPU to finish with the last contents.</summary>
public sealed unsafe class DynamicGpuBuffer : IDisposable
{
    readonly SDL_GPUDevice* device;
    readonly SDL_GPUTransferBuffer* transfer;
    public SDL_GPUBuffer* Buffer { get; }
    public uint Capacity { get; }

    public DynamicGpuBuffer(SDL_GPUDevice* device, SDL_GPUBufferUsageFlags usage, uint capacityBytes)
    {
        this.device = device;
        Capacity = Math.Max(capacityBytes, 4);
        var info = new SDL_GPUBufferCreateInfo { usage = usage, size = Capacity };
        Buffer = SDL_CreateGPUBuffer(device, &info);
        var ti = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, size = Capacity };
        transfer = SDL_CreateGPUTransferBuffer(device, &ti);
        if (Buffer == null || transfer == null)
            throw new InvalidOperationException($"dynamic GPU buffer: {SDL_GetError()}");
    }

    /// <summary>Copies <paramref name="data"/> in with a copy pass on <paramref name="cmd"/> (outside any render pass).</summary>
    public void Upload<T>(SDL_GPUCommandBuffer* cmd, ReadOnlySpan<T> data) where T : unmanaged
    {
        uint size = (uint)(data.Length * sizeof(T));
        if (size == 0)
            return;
        if (size > Capacity)
            throw new ArgumentException($"{size} bytes into a {Capacity}-byte buffer");
        var dst = (T*)SDL_MapGPUTransferBuffer(device, transfer, true);
        data.CopyTo(new Span<T>(dst, data.Length));
        SDL_UnmapGPUTransferBuffer(device, transfer);
        var cp = SDL_BeginGPUCopyPass(cmd);
        var src = new SDL_GPUTransferBufferLocation { transfer_buffer = transfer };
        var region = new SDL_GPUBufferRegion { buffer = Buffer, size = size };
        SDL_UploadToGPUBuffer(cp, &src, &region, true);
        SDL_EndGPUCopyPass(cp);
    }

    public void Dispose()
    {
        SDL_ReleaseGPUTransferBuffer(device, transfer);
        SDL_ReleaseGPUBuffer(device, Buffer);
    }
}
