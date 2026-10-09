using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Shipgen.Render.Bake;

/// <summary>Draws a DrawList on the GPU (SDL_GPU) and reads it back: stencil-then-cover fills, the clip stack and
/// opacity groups in the stencil, 8x MSAA (4x if 8x isn't there) with the hardware resolve on the colour layers,
/// 1 sample on the height map, and tiles of Tile px so a long ship or a fat MSAA target never outgrows the device.
///
/// Stencil bits: 0-3 the winding count of the fill being drawn (or 1 for a union), 4 an opacity group's "already
/// drawn" flag, 5-7 the clip depth. A fill counts its winding where the clip depth is the current one (and the group
/// flag is clear), then covers its triangles where the count is non-zero, clearing it (setting the flag in a group).
/// A clip counts its winding the same way, then raises the depth there; popping lowers it again over the same
/// triangles. Ops arrive in order from Lower.</summary>
public sealed unsafe class GpuBaker : IDisposable
{
    public const int Tile = 2048;

    readonly SDL_GPUDevice* dev;
    readonly bool ownsSdl;   // the device and SDL are ours to close
    public readonly SDL_GPUSampleCount Samples;
    readonly SDL_GPUTextureFormat dsFormat;
    readonly SDL_GPUShader* vs, fs;
    readonly Pipes colour, height;
    readonly SDL_GPUTexture* colMs, colRes, colDs, hCol, hDs;
    SDL_GPUBuffer* vbuf;
    SDL_GPUTransferBuffer* up;
    uint vcap;
    readonly SDL_GPUTransferBuffer* down;

    sealed class Pipes
    {
        public SDL_GPUGraphicsPipeline* Wind, Union, Cover, CoverGroup, ClipPush, ClipPop, GroupClear;
    }

    public string Driver => SDL_GetGPUDeviceDriver(dev) ?? "?";

    /// <summary>A device of its own (SDL video initialised here, the offscreen driver if there is no display).</summary>
    public GpuBaker() : this(OwnDevice(), owns: true)
    {
    }

    /// <summary>Bake on a device someone else owns (the game's or the viewer's); Dispose leaves it alone.</summary>
    public GpuBaker(SDL_GPUDevice* device) : this(device, owns: false)
    {
    }

    static SDL_GPUDevice* OwnDevice()
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            SDL_SetHint(SDL_HINT_VIDEO_DRIVER, "offscreen");
            if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
                throw new InvalidOperationException($"SDL_Init: {SDL_GetError()}");
        }
        var d = SDL_CreateGPUDevice(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, false, (byte*)null);
        if (d == null)
            throw new InvalidOperationException($"SDL_CreateGPUDevice: {SDL_GetError()}");
        return d;
    }

    GpuBaker(SDL_GPUDevice* device, bool owns)
    {
        dev = device;
        ownsSdl = owns;
        Samples = SDL_GPUTextureSupportsSampleCount(dev, SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_8)
            ? SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_8 : SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_4;
        dsFormat = SDL_GPUTextureSupportsFormat(dev, SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D32_FLOAT_S8_UINT,
            SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D, SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET)
            ? SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D32_FLOAT_S8_UINT : SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D24_UNORM_S8_UINT;

        vs = Shader("shipbake.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX);
        fs = Shader("shipbake.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT);
        colour = MakePipes(SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM, Samples, blend: true);
        height = MakePipes(SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8_UNORM, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1, blend: false);

        var ct = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_COLOR_TARGET;
        colMs = Texture(SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM, ct, Samples);
        colRes = Texture(SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM, ct | SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
            SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1);
        colDs = Texture(dsFormat, SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET, Samples);
        hCol = Texture(SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8_UNORM, ct, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1);
        hDs = Texture(dsFormat, SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1);
        var di = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_DOWNLOAD,
            size = Tile * Tile * 4,
        };
        down = SDL_CreateGPUTransferBuffer(dev, &di);
    }

    SDL_GPUShader* Shader(string name, SDL_GPUShaderStage stage)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                      ?? throw new InvalidOperationException($"{name} is not embedded");
        var code = new byte[s.Length];
        s.ReadExactly(code);
        var entry = "main"u8.ToArray().Append((byte)0).ToArray();
        fixed (byte* c = code, e = entry)
        {
            var info = new SDL_GPUShaderCreateInfo
            {
                code = c,
                code_size = (nuint)code.Length,
                entrypoint = e,
                format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
                stage = stage,
                num_uniform_buffers = 1,
            };
            var sh = SDL_CreateGPUShader(dev, &info);
            if (sh == null)
                throw new InvalidOperationException($"shader {name}: {SDL_GetError()}");
            return sh;
        }
    }

    SDL_GPUTexture* Texture(SDL_GPUTextureFormat format, SDL_GPUTextureUsageFlags usage, SDL_GPUSampleCount samples)
    {
        var info = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = format,
            usage = usage,
            width = Tile,
            height = Tile,
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = samples,
        };
        var t = SDL_CreateGPUTexture(dev, &info);
        if (t == null)
            throw new InvalidOperationException($"texture {format}: {SDL_GetError()}");
        return t;
    }

    static SDL_GPUStencilOpState Op(SDL_GPUStencilOp pass, SDL_GPUCompareOp cmp) => new()
    {
        fail_op = SDL_GPUStencilOp.SDL_GPU_STENCILOP_KEEP,
        depth_fail_op = SDL_GPUStencilOp.SDL_GPU_STENCILOP_KEEP,
        pass_op = pass,
        compare_op = cmp,
    };

    SDL_GPUGraphicsPipeline* Pipe(SDL_GPUTextureFormat format, SDL_GPUSampleCount samples, bool blend, bool write,
        SDL_GPUStencilOp front, SDL_GPUStencilOp back, SDL_GPUCompareOp cmp, byte cmpMask, byte writeMask)
    {
        var attr = new SDL_GPUVertexAttribute { location = 0, buffer_slot = 0, format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2 };
        var buf = new SDL_GPUVertexBufferDescription { slot = 0, pitch = 8, input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX };
        var ctd = new SDL_GPUColorTargetDescription
        {
            format = format,
            blend_state = new SDL_GPUColorTargetBlendState
            {
                enable_blend = blend && write,
                src_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE,
                dst_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                color_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
                src_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE,
                dst_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                alpha_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
                enable_color_write_mask = true,
                color_write_mask = write
                    ? SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_R | SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_G |
                      SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_B | SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_A
                    : 0,
            },
        };
        var info = new SDL_GPUGraphicsPipelineCreateInfo
        {
            vertex_shader = vs,
            fragment_shader = fs,
            vertex_input_state = new SDL_GPUVertexInputState
            {
                vertex_buffer_descriptions = &buf,
                num_vertex_buffers = 1,
                vertex_attributes = &attr,
                num_vertex_attributes = 1,
            },
            primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST,
            rasterizer_state = new SDL_GPURasterizerState
            {
                fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL,
                cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE,
                front_face = SDL_GPUFrontFace.SDL_GPU_FRONTFACE_COUNTER_CLOCKWISE,
            },
            multisample_state = new SDL_GPUMultisampleState { sample_count = samples },
            depth_stencil_state = new SDL_GPUDepthStencilState
            {
                enable_stencil_test = true,
                front_stencil_state = Op(front, cmp),
                back_stencil_state = Op(back, cmp),
                compare_mask = cmpMask,
                write_mask = writeMask,
            },
            target_info = new SDL_GPUGraphicsPipelineTargetInfo
            {
                color_target_descriptions = &ctd,
                num_color_targets = 1,
                depth_stencil_format = dsFormat,
                has_depth_stencil_target = true,
            },
        };
        var p = SDL_CreateGPUGraphicsPipeline(dev, &info);
        if (p == null)
            throw new InvalidOperationException($"pipeline: {SDL_GetError()}");
        return p;
    }

    Pipes MakePipes(SDL_GPUTextureFormat f, SDL_GPUSampleCount s, bool blend)
    {
        const SDL_GPUStencilOp keep = SDL_GPUStencilOp.SDL_GPU_STENCILOP_KEEP;
        const SDL_GPUStencilOp inc = SDL_GPUStencilOp.SDL_GPU_STENCILOP_INCREMENT_AND_WRAP;
        const SDL_GPUStencilOp dec = SDL_GPUStencilOp.SDL_GPU_STENCILOP_DECREMENT_AND_WRAP;
        const SDL_GPUStencilOp rep = SDL_GPUStencilOp.SDL_GPU_STENCILOP_REPLACE;
        const SDL_GPUStencilOp zero = SDL_GPUStencilOp.SDL_GPU_STENCILOP_ZERO;
        const SDL_GPUCompareOp eq = SDL_GPUCompareOp.SDL_GPU_COMPAREOP_EQUAL;
        const SDL_GPUCompareOp ne = SDL_GPUCompareOp.SDL_GPU_COMPAREOP_NOT_EQUAL;
        _ = keep;
        return new Pipes
        {
            // count the winding where the clip depth is current and the group flag clear (ref = depth << 5)
            Wind = Pipe(f, s, blend, false, inc, dec, eq, 0xF0, 0x0F),
            // set coverage 1 (ref = depth << 5 | 1)
            Union = Pipe(f, s, blend, false, rep, rep, eq, 0xF0, 0x0F),
            // paint where the count is non-zero and clear it (ref = 0)
            Cover = Pipe(f, s, blend, true, zero, zero, ne, 0x0F, 0x0F),
            // the same in an opacity group, setting its flag (ref = 0x10)
            CoverGroup = Pipe(f, s, blend, true, rep, rep, ne, 0x0F, 0x1F),
            // raise the clip depth where the count is non-zero, clearing it (ref = (depth + 1) << 5)
            ClipPush = Pipe(f, s, blend, false, rep, rep, ne, 0x0F, 0xFF),
            // lower the clip depth where it is the current one (ref = depth << 5)
            ClipPop = Pipe(f, s, blend, false, dec, dec, eq, 0xE0, 0xE0),
            // clear the group flag (ref = 0x10)
            GroupClear = Pipe(f, s, blend, false, zero, zero, eq, 0x10, 0x10),
        };
    }

    void Upload(List<Vector2> verts)
    {
        uint size = (uint)(verts.Count * 8);
        if (size == 0)
            return;
        if (size > vcap)
        {
            if (vbuf != null)
                SDL_ReleaseGPUBuffer(dev, vbuf);
            if (up != null)
                SDL_ReleaseGPUTransferBuffer(dev, up);
            vcap = Math.Max(size, vcap * 2);
            var bi = new SDL_GPUBufferCreateInfo { usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, size = vcap };
            vbuf = SDL_CreateGPUBuffer(dev, &bi);
            var ti = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, size = vcap };
            up = SDL_CreateGPUTransferBuffer(dev, &ti);
        }
        var p = (Vector2*)SDL_MapGPUTransferBuffer(dev, up, false);
        CollectionsMarshal.AsSpan(verts).CopyTo(new Span<Vector2>(p, verts.Count));
        SDL_UnmapGPUTransferBuffer(dev, up);
        var cmd = SDL_AcquireGPUCommandBuffer(dev);
        var cp = SDL_BeginGPUCopyPass(cmd);
        var src = new SDL_GPUTransferBufferLocation { transfer_buffer = up, offset = 0 };
        var dst = new SDL_GPUBufferRegion { buffer = vbuf, offset = 0, size = size };
        SDL_UploadToGPUBuffer(cp, &src, &dst, false);
        SDL_EndGPUCopyPass(cp);
        SDL_SubmitGPUCommandBuffer(cmd);
    }

    /// <summary>Draws dl and reads it back: RGBA with straight alpha, or for the height map one grey channel.</summary>
    public Image8 Render(DrawList dl, bool heightMap = false)
    {
        var img = new Image8(dl.Width, dl.Height, heightMap ? 1 : 4);
        Upload(dl.Verts);
        var P = heightMap ? height : colour;
        for (int ty = 0; ty < dl.Height; ty += Tile)
            for (int tx = 0; tx < dl.Width; tx += Tile)
            {
                int tw = Math.Min(Tile, dl.Width - tx), th = Math.Min(Tile, dl.Height - ty);
                var cmd = SDL_AcquireGPUCommandBuffer(dev);
                var cti = new SDL_GPUColorTargetInfo
                {
                    texture = heightMap ? hCol : colMs,
                    clear_color = new SDL_FColor { r = 0, g = 0, b = 0, a = 0 },
                    load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
                    store_op = heightMap ? SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE : SDL_GPUStoreOp.SDL_GPU_STOREOP_RESOLVE,
                    resolve_texture = heightMap ? null : colRes,
                };
                var dti = new SDL_GPUDepthStencilTargetInfo
                {
                    texture = heightMap ? hDs : colDs,
                    clear_depth = 0,
                    load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
                    store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,
                    stencil_load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
                    stencil_store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,
                    clear_stencil = 0,
                };
                var pass = SDL_BeginGPURenderPass(cmd, &cti, 1, &dti);
                if (dl.Verts.Count > 0)
                {
                    var bind = new SDL_GPUBufferBinding { buffer = vbuf, offset = 0 };
                    SDL_BindGPUVertexBuffers(pass, 0, &bind, 1);
                    var xf = new Vector4(2f / Tile, -2f / Tile, -1f - 2f * tx / Tile, 1f + 2f * ty / Tile);
                    SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&xf), 16);
                    Draw(cmd, pass, dl, P, heightMap);
                }
                SDL_EndGPURenderPass(pass);
                var cp = SDL_BeginGPUCopyPass(cmd);
                var region = new SDL_GPUTextureRegion { texture = heightMap ? hCol : colRes, w = (uint)tw, h = (uint)th, d = 1 };
                var tinfo = new SDL_GPUTextureTransferInfo { transfer_buffer = down, offset = 0, pixels_per_row = (uint)tw, rows_per_layer = (uint)th };
                SDL_DownloadFromGPUTexture(cp, &region, &tinfo);
                SDL_EndGPUCopyPass(cp);
                var fence = SDL_SubmitGPUCommandBufferAndAcquireFence(cmd);
                SDL_WaitForGPUFences(dev, true, &fence, 1);
                SDL_ReleaseGPUFence(dev, fence);
                var px = (byte*)SDL_MapGPUTransferBuffer(dev, down, false);
                int c = img.Channels;
                for (int y = 0; y < th; y++)
                    new Span<byte>(px + y * tw * c, tw * c).CopyTo(img.Data.AsSpan(((ty + y) * img.Width + tx) * c, tw * c));
                SDL_UnmapGPUTransferBuffer(dev, down);
            }
        if (!heightMap)
            Unpremultiply(img);
        return img;
    }

    static void Unpremultiply(Image8 im)
    {
        var d = im.Data;
        for (int i = 0; i < d.Length; i += 4)
        {
            int a = d[i + 3];
            if (a == 0 || a == 255)
                continue;
            for (int k = 0; k < 3; k++)
                d[i + k] = (byte)Math.Min(255, (d[i + k] * 255 + a / 2) / a);
        }
    }

    static void Use(SDL_GPURenderPass* pass, ref SDL_GPUGraphicsPipeline* bound, SDL_GPUGraphicsPipeline* p, int stencilRef)
    {
        if (p != bound)
        {
            SDL_BindGPUGraphicsPipeline(pass, p);
            bound = p;
        }
        SDL_SetGPUStencilReference(pass, (byte)stencilRef);
    }

    void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, DrawList dl, Pipes P, bool heightMap)
    {
        int depth = 0;
        SDL_GPUGraphicsPipeline* bound = null;
        foreach (var op in dl.Ops)
        {
            if (op.Count == 0)
                continue;
            uint n = (uint)op.Count, first = (uint)op.First;
            switch (op.Kind)
            {
                case OpKind.Fill:
                    {
                        if (op.Union)
                            Use(pass, ref bound, P.Union, depth << 5 | 1);
                        else
                            Use(pass, ref bound, P.Wind, depth << 5);
                        SDL_DrawGPUPrimitives(pass, n, 1, first, 0);
                        var col = heightMap ? new Vector4(op.Colour.X, 0, 0, 1) : op.Colour;
                        SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&col), 16);
                        if (op.InGroup)
                            Use(pass, ref bound, P.CoverGroup, 0x10);
                        else
                            Use(pass, ref bound, P.Cover, 0);
                        SDL_DrawGPUPrimitives(pass, n, 1, first, 0);
                        break;
                    }
                case OpKind.PushClip:
                    Use(pass, ref bound, P.Wind, depth << 5);
                    SDL_DrawGPUPrimitives(pass, n, 1, first, 0);
                    depth++;
                    Use(pass, ref bound, P.ClipPush, depth << 5);
                    SDL_DrawGPUPrimitives(pass, n, 1, first, 0);
                    break;
                case OpKind.PopClip:
                    Use(pass, ref bound, P.ClipPop, depth << 5);
                    SDL_DrawGPUPrimitives(pass, n, 1, first, 0);
                    depth--;
                    break;
                case OpKind.GroupEnd:
                    Use(pass, ref bound, P.GroupClear, 0x10);
                    SDL_DrawGPUPrimitives(pass, n, 1, first, 0);
                    break;
            }
        }
    }

    public void Dispose()
    {
        if (dev == null)
            return;
        SDL_WaitForGPUIdle(dev);
        foreach (var P in new[] { colour, height })
            foreach (var p in new[] { P.Wind, P.Union, P.Cover, P.CoverGroup, P.ClipPush, P.ClipPop, P.GroupClear })
                SDL_ReleaseGPUGraphicsPipeline(dev, p);
        foreach (var t in new[] { (nint)colMs, (nint)colRes, (nint)colDs, (nint)hCol, (nint)hDs })
            SDL_ReleaseGPUTexture(dev, (SDL_GPUTexture*)t);
        if (vbuf != null)
            SDL_ReleaseGPUBuffer(dev, vbuf);
        if (up != null)
            SDL_ReleaseGPUTransferBuffer(dev, up);
        SDL_ReleaseGPUTransferBuffer(dev, down);
        SDL_ReleaseGPUShader(dev, vs);
        SDL_ReleaseGPUShader(dev, fs);
        if (ownsSdl)
        {
            SDL_DestroyGPUDevice(dev);
            SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);   // reference-counted: another baker may still use it
        }
    }
}
