using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using SDL;
using static SDL.SDL3;

namespace Fleetwright;

/// <summary>Where the ship sprite is seen from and how it's lit: a plain struct the caller owns.</summary>
public struct ShipSpriteView
{
    /// <summary>The ship point at the target's centre, metres (x toward the bow, y to starboard).</summary>
    public Vector2 Centre;

    /// <summary>Target pixels per metre.</summary>
    public float Zoom;

    /// <summary>The mip level to sample, or -1 for the hardware's choice.</summary>
    public int Mip;

    public float SunAzimuthDeg, SunElevationDeg;
    public bool Shadows;

    public static ShipSpriteView Default => new() { Zoom = 1, Mip = -1, SunAzimuthDeg = 300, SunElevationDeg = 50, Shadows = true };
}

/// <summary>
/// Draws a baked ship sprite into any <see cref="RenderTarget"/>, as the game will: the hull, the turrets at the
/// bearings the caller gives (their shadows first) and the sun's shadow from the height map. Textures come from a
/// <see cref="DesignResult"/> (baked and mipped on the worker; only the upload happens here). Pipelines are built for
/// the target's format and sample count, and rebuilt if a different target comes. Knows nothing of input or ImGui.
/// </summary>
public sealed unsafe class ShipSpriteRenderer : IDisposable
{
    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fsSprite, fsShadow;
    readonly SDL_GPUSampler* linear, nearest;
    SDL_GPUGraphicsPipeline* spritePipe, shadowPipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;

    SDL_GPUTexture* hullTex, heightTex;
    readonly Dictionary<string, nint> turretTex = new(StringComparer.Ordinal);
    int[] drawOrder = [];   // the mounts by ascending z, made once per upload so a frame allocates nothing

    /// <summary>The shown sprite's layout, or null when nothing is uploaded.</summary>
    public SpriteMeta? Meta { get; private set; }

    /// <summary>Sprite pixels per metre at mip level 0.</summary>
    public double Scale { get; private set; } = 1;

    /// <summary>The canvas in metres (the sprite's full extent), or zero when nothing is uploaded.</summary>
    public Vector2 CanvasM => Meta is { } m ? new((float)(m.SizePx[0] / Scale), (float)(m.SizePx[1] / Scale)) : Vector2.Zero;

    public ShipSpriteRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/shipview.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 1);
        fsSprite = GpuShader.Load(device, "Content/Shaders/Compiled/shipview_sprite.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 2, 0, 0, 1);
        fsShadow = GpuShader.Load(device, "Content/Shaders/Compiled/shipview_shadow.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 1, 0, 0, 1);
        if (vs == null || fsSprite == null || fsShadow == null)
            throw new InvalidOperationException("ship sprite shaders failed to load");
        linear = Sampler(SDL_GPUFilter.SDL_GPU_FILTER_LINEAR, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR);
        nearest = Sampler(SDL_GPUFilter.SDL_GPU_FILTER_NEAREST, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST);
    }

    SDL_GPUSampler* Sampler(SDL_GPUFilter f, SDL_GPUSamplerMipmapMode m)
    {
        var info = new SDL_GPUSamplerCreateInfo
        {
            min_filter = f,
            mag_filter = f,
            mipmap_mode = m,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            max_lod = 1000,
        };
        return SDL_CreateGPUSampler(device, &info);
    }

    // ------------------------------------------------------------------ textures

    /// <summary>Shows a result's sprite (or nothing, if it has none). The old textures are released at once: SDL keeps
    /// them alive until the GPU work in flight that reads them is done.</summary>
    public void Upload(DesignResult r)
    {
        Clear();
        if (r.Sprites is not { } sp || r.Images is not { } im)
            return;
        hullTex = Upload(im.Hull, height: false);
        heightTex = Upload(im.Height, height: true);
        foreach (var (tid, levels) in im.Turrets)
            turretTex[tid] = (nint)Upload(levels, height: false);
        Meta = sp.Meta;
        Scale = sp.Meta.ScalePxPerM;
        var mounts = sp.Meta.Mounts;
        drawOrder = Enumerable.Range(0, mounts.Count).OrderBy(i => mounts[i].Z).ToArray();
    }

    /// <summary>Drops the sprite: the next renders show only the background.</summary>
    public void Clear()
    {
        if (hullTex != null)
            SDL_ReleaseGPUTexture(device, hullTex);
        if (heightTex != null)
            SDL_ReleaseGPUTexture(device, heightTex);
        foreach (var t in turretTex.Values)
            SDL_ReleaseGPUTexture(device, (SDL_GPUTexture*)t);
        turretTex.Clear();
        hullTex = heightTex = null;
        Meta = null;
        drawOrder = [];
    }

    SDL_GPUTexture* Upload(Image8[] levels, bool height)
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
            throw new InvalidOperationException($"ship sprite texture: {SDL_GetError()}");
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

    // ------------------------------------------------------------------ pipelines

    void EnsurePipelines(RenderTarget target)
    {
        if (spritePipe != null && pipeFormat == target.ColorFormat && pipeSamples == target.SampleCount)
            return;
        ReleasePipelines();
        (pipeFormat, pipeSamples) = (target.ColorFormat, target.SampleCount);
        spritePipe = Pipeline(fsSprite);
        shadowPipe = Pipeline(fsShadow);
    }

    void ReleasePipelines()
    {
        if (spritePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, spritePipe);
        if (shadowPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, shadowPipe);
        spritePipe = shadowPipe = null;
    }

    /// <summary>Premultiplied blending, no depth: the sprites are drawn in order.</summary>
    SDL_GPUGraphicsPipeline* Pipeline(SDL_GPUShader* fs)
    {
        var ctd = new SDL_GPUColorTargetDescription
        {
            format = pipeFormat,
            blend_state = new SDL_GPUColorTargetBlendState
            {
                enable_blend = true,
                src_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE,
                dst_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                color_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
                src_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE,
                dst_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                alpha_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
            },
        };
        var info = new SDL_GPUGraphicsPipelineCreateInfo
        {
            vertex_shader = vs,
            fragment_shader = fs,
            primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST,
            rasterizer_state = new SDL_GPURasterizerState
            {
                fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL,
                cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE,
            },
            multisample_state = new SDL_GPUMultisampleState { sample_count = pipeSamples },
            target_info = new SDL_GPUGraphicsPipelineTargetInfo
            {
                color_target_descriptions = &ctd,
                num_color_targets = 1,
            },
        };
        var p = SDL_CreateGPUGraphicsPipeline(device, &info);
        if (p == null)
            throw new InvalidOperationException($"ship sprite pipeline: {SDL_GetError()}");
        return p;
    }

    // ------------------------------------------------------------------ drawing

    struct Quad
    {
        public Vector4 O, U, V;
    }

    struct SpriteParams
    {
        public Vector4 P, HMap;
    }

    struct ShadowParams
    {
        public Vector4 Sun, HMap, March;
    }

    /// <summary>The zoom that fits the canvas into a w x h target, filling the given fractions of it.</summary>
    public float FitZoom(float w, float h, float fracW = 0.92f, float fracH = 0.8f)
    {
        var c = CanvasM;
        return c.X <= 0 ? 1 : Math.Min(w * fracW / c.X, h * fracH / c.Y);
    }

    /// <summary>Clears <paramref name="target"/> to <paramref name="clear"/> and draws the sprite in its own pass.
    /// <paramref name="bearings"/> holds each mount's bearing in degrees (empty: the rest angles).</summary>
    public void Render(SDL_GPUCommandBuffer* cmd, RenderTarget target, in ShipSpriteView view, ReadOnlySpan<double> bearings,
        SDL_FColor clear)
    {
        EnsurePipelines(target);
        var pass = target.BeginPass(cmd, clear, depth: false);
        if (Meta != null && hullTex != null)
            Draw(cmd, pass, target.Width, target.Height, view, bearings);
        SDL_EndGPURenderPass(pass);
    }

    void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, uint w, uint h, in ShipSpriteView view, ReadOnlySpan<double> bearings)
    {
        var meta = Meta!;
        var size = meta.SizePx;
        var canvas = CanvasM;
        float cw = canvas.X, ch = canvas.Y;
        var centre = view.Centre;
        float zoom = view.Zoom;
        int mip = view.Mip;
        Vector2 Ndc(Vector2 ship) => new((ship.X - centre.X) * zoom * 2 / w, -(ship.Y - centre.Y) * zoom * 2 / h);
        Quad Q(Vector2 o, Vector2 u, Vector2 v)
        {
            var no = Ndc(o);
            var nu = Ndc(o + u) - no;
            var nv = Ndc(o + v) - no;
            return new Quad { O = new(no, o.X, o.Y), U = new(nu, u.X, u.Y), V = new(nv, v.X, v.Y) };
        }
        var hmap = new Vector4((float)(Scale / size[0]), (float)(Scale / size[1]), 0.5f, 0.5f);
        var sh = meta.Shadow;
        float deckM = (float)sh.DeckM, maxH = (float)sh.MaxHeightM;
        float k = MathF.Tan(view.SunElevationDeg * MathF.PI / 180);
        var toSun = new Vector2(MathF.Cos(view.SunAzimuthDeg * MathF.PI / 180), MathF.Sin(view.SunAzimuthDeg * MathF.PI / 180));
        var height = heightTex;

        void Sprite(SDL_GPUTexture* tex, Quad q, SpriteParams p)
        {
            SDL_BindGPUGraphicsPipeline(pass, spritePipe);
            var bind = stackalloc SDL_GPUTextureSamplerBinding[2];
            bind[0] = new SDL_GPUTextureSamplerBinding { texture = tex, sampler = linear };
            bind[1] = new SDL_GPUTextureSamplerBinding { texture = height, sampler = nearest };
            SDL_BindGPUFragmentSamplers(pass, 0, bind, 2);
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&q), (uint)sizeof(Quad));
            SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&p), (uint)sizeof(SpriteParams));
            SDL_DrawGPUPrimitives(pass, 6, 1, 0, 0);
        }

        // the hull
        Sprite(hullTex, Q(new(-cw / 2, -ch / 2), new(cw, 0), new(0, ch)), new SpriteParams { P = new(mip, 0, 1, 0), HMap = hmap });

        // the turrets, their shadows first (only where the hull is lower than the turret's roof)
        var types = meta.TurretTypes;
        for (int pass2 = 0; pass2 < 2; pass2++)
        {
            if (pass2 == 0 && !view.Shadows)
                continue;
            foreach (int i in drawOrder)
            {
                var m = meta.Mounts[i];
                var tm = types[m.Type];
                float e = (float)(tm.SizePx[0] / Scale / 2);
                var c = new Vector2((float)m.PosM[0], (float)m.PosM[1]);
                float a = (float)((i < bearings.Length ? bearings[i] : m.RestDeg) * Math.PI / 180);
                var ax = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 2 * e;
                var ay = new Vector2(-MathF.Sin(a), MathF.Cos(a)) * 2 * e;
                var o = c - ax / 2 - ay / 2;
                var tex = (SDL_GPUTexture*)turretTex[m.Type];
                if (pass2 == 0)
                {
                    float top = (float)m.TopM;
                    var off = -toSun * ((top - deckM) / k);   // shadow.sun_offset_px, in metres
                    Sprite(tex, Q(o + off, ax, ay), new SpriteParams { P = new(mip, 1, 0.4f, top), HMap = hmap });
                }
                else
                    Sprite(tex, Q(o, ax, ay), new SpriteParams { P = new(mip, 0, 1, 0), HMap = hmap });
            }
        }

        // the sun's shadow from the height map, over the canvas and as far beyond it as a shadow can reach
        if (view.Shadows)
        {
            float reach = maxH / k;
            var q = Q(new(-cw / 2 - reach, -ch / 2 - reach), new(cw + 2 * reach, 0), new(0, ch + 2 * reach));
            float lod = Math.Max(0, mip);
            float step = MathF.Max(0.1f, (float)(1 / Scale) * MathF.Pow(2, lod));
            var p = new ShadowParams
            {
                Sun = new(toSun, k, 0.4f),
                HMap = hmap,
                March = new(step, MathF.Ceiling(reach / step), 0.3f, lod),
            };
            SDL_BindGPUGraphicsPipeline(pass, shadowPipe);
            var bind = new SDL_GPUTextureSamplerBinding { texture = heightTex, sampler = nearest };
            SDL_BindGPUFragmentSamplers(pass, 0, &bind, 1);
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&q), (uint)sizeof(Quad));
            SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&p), (uint)sizeof(ShadowParams));
            SDL_DrawGPUPrimitives(pass, 6, 1, 0, 0);
        }
    }

    public void Dispose()
    {
        SDL_WaitForGPUIdle(device);
        Clear();
        ReleasePipelines();
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fsSprite);
        SDL_ReleaseGPUShader(device, fsShadow);
        SDL_ReleaseGPUSampler(device, linear);
        SDL_ReleaseGPUSampler(device, nearest);
    }
}
