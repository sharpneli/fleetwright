using System.Diagnostics;
using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright;

/// <summary>The ship viewer (PORTING.md Step 6), a test tool: builds a design, bakes it on the GPU device
/// and draws it from the baked textures as the game will. Mips, turrets turning through their arcs, turret shadows
/// and the sun's shadow from the height map (a first version of the game's shadow shader). The ImGui panel picks
/// the design, the look, the mip level, the turrets and the sun, and rebuilds on change: the designer's loop in
/// miniature. Mouse: wheel zooms, left drag pans.</summary>
public sealed unsafe class ShipViewer : IScene
{
    const double Scale = 10.0;   // px per metre at mip level 0
    const int MipLevels = 5;
    static readonly Vector4 Sea = new(0x2d / 255f, 0x5a / 255f, 0x73 / 255f, 1f);

    readonly SDL_GPUDevice* device;
    readonly GpuBaker baker;
    readonly SDL_GPUShader* vs, fsSprite, fsShadow;
    readonly SDL_GPUSampler* linear, nearest;

    // built for the target drawn into (its format and sample count), rebuilt if a different one comes
    SDL_GPUGraphicsPipeline* spritePipe, shadowPipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;

    // what is shown
    readonly List<string> designs;
    int designIndex;
    int navyIndex, eraIndex;   // 0 = the design's own
    readonly string[] navies, eras;
    ShipSprites? sprites;
    SDL_GPUTexture* hullTex, heightTex;
    readonly Dictionary<string, nint> turretTex = new(StringComparer.Ordinal);
    string status = "";
    bool dirty = true;

    // view and controls
    Vector2 centre;
    float zoom = 1;
    bool fit = true;
    int mip = -1;
    int turretMode = 1;   // rest, sweep, starboard
    float sunAz = 300, sunEl = 50;
    bool shadows = true;
    double clock;
    double[]? bearings;   // each mount's bearing now, slewing towards its target
    const double TraverseDegPerS = 45;   // faster than the sweep, so a turret catches up after a blind arc
    bool dragging;

    public ShipViewer(SDL_GPUDevice* device, string designPath, string? navy = null, string? era = null)
    {
        this.device = device;
        var dir = Path.GetDirectoryName(Path.GetFullPath(designPath))!;
        designs = Directory.GetFiles(dir, "*.json").Order(StringComparer.Ordinal).ToList();
        designIndex = Math.Max(0, designs.FindIndex(p => Path.GetFullPath(p) == Path.GetFullPath(designPath)));
        if (designs.Count == 0)
            designs.Add(designPath);
        navies = ["(design)", .. Shipgen.Render.Looks.Navies];
        eras = ["(design)", .. Shipgen.Render.Looks.Eras];
        navyIndex = navy != null ? Math.Max(0, Array.IndexOf(navies, navy)) : 0;
        eraIndex = era != null ? Math.Max(0, Array.IndexOf(eras, era)) : 0;

        baker = new GpuBaker(device);
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/shipview.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 1);
        fsSprite = GpuShader.Load(device, "Content/Shaders/Compiled/shipview_sprite.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 2, 0, 0, 1);
        fsShadow = GpuShader.Load(device, "Content/Shaders/Compiled/shipview_shadow.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 1, 0, 0, 1);
        if (vs == null || fsSprite == null || fsShadow == null)
            throw new InvalidOperationException("ship viewer shaders failed to load");
        linear = Sampler(SDL_GPUFilter.SDL_GPU_FILTER_LINEAR, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR);
        nearest = Sampler(SDL_GPUFilter.SDL_GPU_FILTER_NEAREST, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST);
        Rebuild();
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

    /// <summary>The pipelines for the target's colour format and sample count (no depth: the sprites are drawn in
    /// order), kept until a target with another format or sample count comes.</summary>
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
            throw new InvalidOperationException($"ship viewer pipeline: {SDL_GetError()}");
        return p;
    }

    // ------------------------------------------------------------------ building and baking

    void Rebuild()
    {
        dirty = false;
        var sw = Stopwatch.StartNew();
        try
        {
            var design = Design.Load(designs[designIndex]);
            var errs = ShipDesign.Validate(design, limits: false).Concat(Shipgen.Looks.Validate(design)).ToList();
            if (errs.Count > 0)
            {
                status = "invalid design:\n" + string.Join("\n", errs.Take(6));
                return;
            }
            var ship = ShipDesign.Build(design);
            double tBuild = sw.Elapsed.TotalSeconds;
            LookInput? look = null;
            if (navyIndex > 0 || eraIndex > 0)
                look = new LookInput { Navy = navyIndex > 0 ? navies[navyIndex] : null, Era = eraIndex > 0 ? eras[eraIndex] : null };
            var sp = ShipSprites.Build(ship, Scale, MipLevels, look);
            double tDraw = sw.Elapsed.TotalSeconds - tBuild;
            var baked = ShipBake.Bake(sp, baker);
            double tBake = sw.Elapsed.TotalSeconds - tBuild - tDraw;
            ReleaseTextures();
            bearings = null;
            hullTex = Upload(baked.Hull, height: false);
            heightTex = Upload(baked.Height, height: true);
            foreach (var (tid, im) in baked.Turrets)
                turretTex[tid] = (nint)Upload(im, height: false);
            sprites = sp;
            var size = sp.Meta.SizePx;
            status = $"{sp.Meta.Name}\n{size[0]} x {size[1]} px, {sp.Turrets.Count} turret types, {sp.Clutter.Count} clutter items\n" +
                     $"build {tBuild:F2} s, draw {tDraw:F2} s, bake {tBake:F2} s";
            Console.WriteLine($"viewer: {design.Id}: {status.Replace('\n', ';')}");
        }
        catch (Exception e)
        {
            status = $"{e.GetType().Name}: {e.Message}";
            Console.Error.WriteLine($"viewer: {status}\n{e.StackTrace}");
        }
    }

    /// <summary>A layer with its mip chain (premultiplied colour, or the height map's max), sampled by the shaders.</summary>
    SDL_GPUTexture* Upload(Image8 level0, bool height)
    {
        var levels = new List<Image8> { level0 };
        while (levels.Count <= MipLevels && levels[^1].Width % 2 == 0 && levels[^1].Height % 2 == 0)
            levels.Add(Mips.Half(levels[^1], max: height));
        var info = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = height ? SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8_UNORM : SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
            width = (uint)level0.Width,
            height = (uint)level0.Height,
            layer_count_or_depth = 1,
            num_levels = (uint)levels.Count,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
        };
        var tex = SDL_CreateGPUTexture(device, &info);
        if (tex == null)
            throw new InvalidOperationException($"viewer texture: {SDL_GetError()}");
        uint total = (uint)levels.Sum(l => l.Data.Length);
        var ti = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, size = total };
        var tb = SDL_CreateGPUTransferBuffer(device, &ti);
        var dst = (byte*)SDL_MapGPUTransferBuffer(device, tb, false);
        uint off = 0;
        foreach (var l in levels)
        {
            var span = new Span<byte>(dst + off, l.Data.Length);
            l.Data.CopyTo(span);
            if (!height)   // premultiply, so the filtering doesn't bleed the transparent border's black in
                for (int i = 0; i < span.Length; i += 4)
                {
                    int a = span[i + 3];
                    for (int k = 0; k < 3; k++)
                        span[i + k] = (byte)((span[i + k] * a + 127) / 255);
                }
            off += (uint)l.Data.Length;
        }
        SDL_UnmapGPUTransferBuffer(device, tb);
        var cmd = SDL_AcquireGPUCommandBuffer(device);
        var cp = SDL_BeginGPUCopyPass(cmd);
        off = 0;
        for (int k = 0; k < levels.Count; k++)
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

    void ReleaseTextures()
    {
        SDL_WaitForGPUIdle(device);
        if (hullTex != null)
            SDL_ReleaseGPUTexture(device, hullTex);
        if (heightTex != null)
            SDL_ReleaseGPUTexture(device, heightTex);
        foreach (var t in turretTex.Values)
            SDL_ReleaseGPUTexture(device, (SDL_GPUTexture*)t);
        turretTex.Clear();
        hullTex = heightTex = null;
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

    /// <summary>Turns each turret towards its target at the traverse rate, the way round that stays inside its arcs
    /// (a turret never swings through the superstructure). A new ship starts with every turret on its target.</summary>
    void Traverse(float dt)
    {
        var mounts = sprites!.Meta.Mounts;
        bool first = bearings == null;
        bearings ??= new double[mounts.Count];
        for (int i = 0; i < mounts.Count; i++)
        {
            var m = mounts[i];
            double target = Target(m);
            if (first)
            {
                bearings[i] = target;
                continue;
            }
            var arcs = m.ArcsDeg.Select(a => (Lo: a[0], Hi: a[1])).ToList();
            double now = bearings[i];
            double ccw = Geometry.Normalize360(target - now), cw = ccw - 360;
            if (ccw == 0)
                continue;
            // the shorter way, unless it crosses a blind arc (only the path's inside is tested: both ends are allowed)
            bool Clear(double d)
            {
                if (arcs.Count == 0)
                    return true;
                int n = (int)Math.Ceiling(Math.Abs(d));
                for (int s = 1; s < n; s++)
                    if (!Geometry.AngleAllowed(arcs, now + d * s / n))
                        return false;
                return true;
            }
            var (a, b) = ccw <= -cw ? (ccw, cw) : (cw, ccw);
            double delta = Clear(a) || !Clear(b) ? a : b;
            double step = TraverseDegPerS * dt;
            bearings[i] = Math.Abs(delta) <= step ? target : Geometry.Normalize360(now + Math.Sign(delta) * step);
        }
    }

    /// <summary>Where the turret is heading: its rest angle, a bearing circling the ship (stopping at the arcs' ends),
    /// or the nearest allowed to starboard.</summary>
    double Target(SpriteMount m)
    {
        var arcs = m.ArcsDeg.Select(a => (Lo: a[0], Hi: a[1])).ToList();
        double target = turretMode switch
        {
            0 => m.RestDeg,
            1 => Geometry.Wrap180(clock * 24.0),
            _ => 90.0,
        };
        if (turretMode == 0 || arcs.Count == 0 || Geometry.AngleAllowed(arcs, target))
            return target;
        double best = target, bd = 1e9;   // geometry.nearest_allowed
        foreach (var (lo, hi) in arcs)
            foreach (var e in new[] { lo, hi })
            {
                double d = Math.Abs(Geometry.Wrap180(e - target));
                if (d < bd)
                    (best, bd) = (e, d);
            }
        return best;
    }

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt)
    {
        if (dirty)
            Rebuild();
        clock += dt;
        if (sprites != null)
            Traverse(dt);
        EnsurePipelines(target);
        var pass = target.BeginPass(cmd, new SDL_FColor { r = Sea.X, g = Sea.Y, b = Sea.Z, a = 1 }, depth: false);
        if (sprites != null && hullTex != null)
            DrawShip(cmd, pass, target.Width, target.Height);
        SDL_EndGPURenderPass(pass);
    }

    void DrawShip(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, uint w, uint h)
    {
        var meta = sprites!.Meta;
        var size = meta.SizePx;
        float cw = (float)(size[0] / Scale), ch = (float)(size[1] / Scale);   // canvas, metres
        if (fit)
        {
            zoom = Math.Min(w * 0.92f / cw, h * 0.8f / ch);
            centre = Vector2.Zero;
            fit = false;
        }
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
        float k = MathF.Tan(sunEl * MathF.PI / 180);
        var toSun = new Vector2(MathF.Cos(sunAz * MathF.PI / 180), MathF.Sin(sunAz * MathF.PI / 180));

        void Sprite(SDL_GPUTexture* tex, Quad q, SpriteParams p)
        {
            SDL_BindGPUGraphicsPipeline(pass, spritePipe);
            var bind = stackalloc SDL_GPUTextureSamplerBinding[2];
            bind[0] = new SDL_GPUTextureSamplerBinding { texture = tex, sampler = linear };
            bind[1] = new SDL_GPUTextureSamplerBinding { texture = heightTex, sampler = nearest };
            SDL_BindGPUFragmentSamplers(pass, 0, bind, 2);
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&q), (uint)sizeof(Quad));
            SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&p), (uint)sizeof(SpriteParams));
            SDL_DrawGPUPrimitives(pass, 6, 1, 0, 0);
        }

        // the hull
        Sprite(hullTex, Q(new(-cw / 2, -ch / 2), new(cw, 0), new(0, ch)), new SpriteParams { P = new(mip, 0, 1, 0), HMap = hmap });

        // the turrets, their shadows first (only where the hull is lower than the turret's roof)
        var types = meta.TurretTypes;
        var order = Enumerable.Range(0, meta.Mounts.Count).OrderBy(i => meta.Mounts[i].Z).ToList();
        foreach (var pass2 in new[] { 0, 1 })
        {
            if (pass2 == 0 && !shadows)
                continue;
            foreach (int i in order)
            {
                var m = meta.Mounts[i];
                var tm = types[m.Type];
                float e = (float)(tm.SizePx[0] / Scale / 2);
                var c = new Vector2((float)m.PosM[0], (float)m.PosM[1]);
                float a = (float)(bearings![i] * Math.PI / 180);
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
        if (shadows)
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

    // ------------------------------------------------------------------ UI and input

    public void BuildUi()
    {
        ImGui.SetNextWindowPos(new Vector2(10, 170), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(330, 420), ImGuiCond.FirstUseEver);
        ImGui.Begin("Ship");
        var names = designs.Select(Path.GetFileNameWithoutExtension).ToArray();
        if (ImGui.Combo("design", ref designIndex, names!, names.Length))
            dirty = fit = true;
        if (ImGui.Combo("navy", ref navyIndex, navies, navies.Length))
            dirty = true;
        if (ImGui.Combo("era", ref eraIndex, eras, eras.Length))
            dirty = true;
        ImGui.Separator();
        ImGui.SliderInt("mip", ref mip, -1, MipLevels, mip < 0 ? "auto" : "%d");
        ImGui.Text("turrets");
        ImGui.SameLine();
        ImGui.RadioButton("rest", ref turretMode, 0);
        ImGui.SameLine();
        ImGui.RadioButton("sweep", ref turretMode, 1);
        ImGui.SameLine();
        ImGui.RadioButton("starboard", ref turretMode, 2);
        ImGui.Checkbox("shadows", ref shadows);
        ImGui.SliderFloat("sun bearing", ref sunAz, 0, 360, "%.0f");
        ImGui.SliderFloat("sun elevation", ref sunEl, 10, 85, "%.0f");
        if (ImGui.Button("fit"))
            fit = true;
        ImGui.SameLine();
        if (ImGui.Button("rebuild"))
            dirty = true;
        ImGui.Separator();
        ImGui.TextWrapped(status);
        ImGui.End();
    }

    /// <summary>Wheel zooms about the cursor, left drag pans. False when the event isn't the viewer's.</summary>
    public bool ProcessEvent(SDL_Event* e, uint w, uint h)
    {
        switch ((SDL_EventType)e->type)
        {
            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                {
                    var at = new Vector2(e->wheel.mouse_x - w / 2f, e->wheel.mouse_y - h / 2f);
                    var before = centre + at / zoom;
                    zoom *= MathF.Pow(1.15f, e->wheel.y);
                    centre = before - at / zoom;
                    return true;
                }
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                dragging = e->button.button == 1;
                return dragging;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                dragging = false;
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                if (!dragging)
                    return false;
                centre -= new Vector2(e->motion.xrel, e->motion.yrel) / zoom;
                return true;
        }
        return false;
    }

    public void Dispose()
    {
        ReleaseTextures();
        ReleasePipelines();
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fsSprite);
        SDL_ReleaseGPUShader(device, fsShadow);
        SDL_ReleaseGPUSampler(device, linear);
        SDL_ReleaseGPUSampler(device, nearest);
        baker.Dispose();
    }
}
