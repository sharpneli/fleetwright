using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using Fleetwright.Sim.Core;
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

    /// <summary>The sun and the moon, ship-local: bearing from the bow toward starboard, and elevation; the moon's phase
    /// angle (0 full, 180 new).</summary>
    public float SunAzimuthDeg, SunElevationDeg, MoonAzimuthDeg, MoonElevationDeg, MoonPhaseDeg;
    public bool Shadows;

    /// <summary>Lit by the sun and moon, or flat: the albedo as drawn (the poster's choice).</summary>
    public bool Lit;

    /// <summary>What a lit view shows: the lit sprite, or one of the lighting's inputs (the bench for
    /// docs/visuals/ship-lighting.md).</summary>
    public ShadeView Show;

    public static ShipSpriteView Default => new()
    {
        Zoom = 1, Mip = -1, SunAzimuthDeg = 300, SunElevationDeg = 50, MoonAzimuthDeg = 120, MoonElevationDeg = 35,
        Shadows = true, Lit = true, Show = ShadeView.Lit,
    };

    static Vec3d Toward(float azimuthDeg, float elevationDeg)
    {
        double a = azimuthDeg * Math.PI / 180, e = elevationDeg * Math.PI / 180;
        return new Vec3d(Math.Cos(a) * Math.Cos(e), Math.Sin(a) * Math.Cos(e), Math.Sin(e));
    }

    /// <summary>The light this view is lit by (<see cref="LightTable.Current"/>), ship-local, z up.</summary>
    public readonly ShipLights Lights()
    {
        if (!Lit)
            return ShipLights.Flat;
        var moon = Toward(MoonAzimuthDeg, MoonElevationDeg);
        var l = ShipLights.Compute(LightTable.Current, new Vec3d(0, 0, 1), Toward(SunAzimuthDeg, SunElevationDeg), moon,
            Moon.Light(moon.Z, MoonPhaseDeg), Moon.PhaseLaw(MoonPhaseDeg));
        l.Uniforms.Look.X = (float)Show;
        return l;
    }
}

/// <summary>The lit sprite or one of its inputs; the values are the shader's look.x (Include/shiplight.glsl).</summary>
public enum ShadeView
{
    Lit = 1,
    Albedo = 0,
    Normal = 2,
    Ao = 3,
    Roughness = 4,
    Metallic = 5,
}

/// <summary>
/// Draws a baked ship sprite into any <see cref="RenderTarget"/>, as the game will: the hull, the turrets at the
/// bearings the caller gives (their shadows first) and the shadow from the height map, lit by the view's sun and moon
/// through the map's lighting (Include/shiplight.glsl), the shadows by the stronger. Textures come from a
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

    SDL_GPUTexture* hullTex, heightTex, hullNormal;
    readonly Dictionary<string, nint> turretTex = new(StringComparer.Ordinal), turretNormal = new(StringComparer.Ordinal);
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
        fsSprite = GpuShader.Load(device, "Content/Shaders/Compiled/shipview_sprite.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 3, 0, 0, 2);
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
        hullNormal = Upload(im.HullNormal, height: false);
        foreach (var (tid, levels) in im.Turrets)
            turretTex[tid] = (nint)Upload(levels, height: false);
        foreach (var (tid, levels) in im.TurretNormals)
            turretNormal[tid] = (nint)Upload(levels, height: false);
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
        if (hullNormal != null)
            SDL_ReleaseGPUTexture(device, hullNormal);
        foreach (var t in turretTex.Values.Concat(turretNormal.Values))
            SDL_ReleaseGPUTexture(device, (SDL_GPUTexture*)t);
        turretTex.Clear();
        turretNormal.Clear();
        hullTex = heightTex = hullNormal = null;
        Meta = null;
        drawOrder = [];
    }

    SDL_GPUTexture* Upload(Image8[] levels, bool height) => Map.SpriteTextures.Upload(device, levels, height);

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
        public Vector4 P, HMap, SunL, MoonL;
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
        var lights = view.Lights();
        var lu = lights.Uniforms;
        SDL_PushGPUFragmentUniformData(cmd, 1, (nint)(&lu), (uint)sizeof(LightUniforms));
        Vector3 sunW = V(lights.SunDir), moonW = V(lights.MoonDir);
        // the shadows' light: toward it on the deck, and the tangent of its elevation (no lower than the map's 8.5°)
        var sd = lights.ShadowDir;
        double horiz = Math.Sqrt(sd.X * sd.X + sd.Y * sd.Y);
        var toSun = horiz > 1e-9 ? new Vector2((float)(sd.X / horiz), (float)(sd.Y / horiz)) : Vector2.UnitX;
        float k = (float)Math.Max(sd.Z / Math.Max(horiz, 1e-9), 0.15);
        bool shadows = view.Shadows && lights.ShadowOpacity > 0 && sd.Z > 0.05 && (!view.Lit || view.Show == ShadeView.Lit);
        float shadowOpacity = (float)lights.ShadowOpacity;
        var height = heightTex;

        void Sprite(SDL_GPUTexture* tex, SDL_GPUTexture* normal, Quad q, SpriteParams p)
        {
            SDL_BindGPUGraphicsPipeline(pass, spritePipe);
            var bind = stackalloc SDL_GPUTextureSamplerBinding[3];
            bind[0] = new SDL_GPUTextureSamplerBinding { texture = tex, sampler = linear };
            bind[1] = new SDL_GPUTextureSamplerBinding { texture = height, sampler = nearest };
            bind[2] = new SDL_GPUTextureSamplerBinding { texture = normal, sampler = linear };
            SDL_BindGPUFragmentSamplers(pass, 0, bind, 3);
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&q), (uint)sizeof(Quad));
            SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&p), (uint)sizeof(SpriteParams));
            SDL_DrawGPUPrimitives(pass, 6, 1, 0, 0);
        }

        // the lights in a quad's frame: x along its u edge, y along its v, z up
        static Vector4 InFrame(Vector3 w, Vector2 u) => new(w.X * u.X + w.Y * u.Y, -w.X * u.Y + w.Y * u.X, w.Z, 0);

        // the hull
        Sprite(hullTex, hullNormal, Q(new(-cw / 2, -ch / 2), new(cw, 0), new(0, ch)),
            new SpriteParams { P = new(mip, 0, 1, 0), HMap = hmap, SunL = new(sunW, 0), MoonL = new(moonW, 0) });

        // the turrets, their shadows first (only where the hull is lower than the turret's roof)
        var types = meta.TurretTypes;
        for (int pass2 = 0; pass2 < 2; pass2++)
        {
            if (pass2 == 0 && !shadows)
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
                var normal = (SDL_GPUTexture*)turretNormal[m.Type];
                if (pass2 == 0)
                {
                    float top = (float)m.TopM;
                    var off = -toSun * ((top - deckM) / k);   // shadow.sun_offset_px, in metres
                    Sprite(tex, normal, Q(o + off, ax, ay), new SpriteParams { P = new(mip, 1, shadowOpacity, top), HMap = hmap });
                }
                else
                {
                    var along = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    Sprite(tex, normal, Q(o, ax, ay), new SpriteParams
                    {
                        P = new(mip, 0, 1, 0), HMap = hmap, SunL = InFrame(sunW, along), MoonL = InFrame(moonW, along),
                    });
                }
            }
        }

        // the shadow from the height map, over the canvas and as far beyond it as a shadow can reach
        if (shadows)
        {
            float reach = maxH / k;
            var q = Q(new(-cw / 2 - reach, -ch / 2 - reach), new(cw + 2 * reach, 0), new(0, ch + 2 * reach));
            float lod = Math.Max(0, mip);
            float step = MathF.Max(0.1f, (float)(1 / Scale) * MathF.Pow(2, lod));
            var p = new ShadowParams
            {
                Sun = new(toSun, k, shadowOpacity),
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

    static Vector3 V(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);

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
