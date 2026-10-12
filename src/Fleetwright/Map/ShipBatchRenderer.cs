using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>One quad the ship shaders draw (shipmap.vert's Quad, std430: 9 vec4s). Corners are metres from the eye, so
/// the floats are small numbers and nothing jitters at the ship band.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct ShipQuad
{
    public Vector4 O, U, V, Ship, ShipV, Sun, March, Hmap, Tint;
    public const int SizeInBytes = 144;
}

public enum ShipDrawKind : byte
{
    Sprite,
    Shadow,
}

/// <summary>One instanced draw: <see cref="Count"/> quads from <see cref="First"/>, with these textures.</summary>
public struct ShipDraw
{
    public ShipDrawKind Kind;
    public nint Tex0, Tex1;
    public int First, Count;
}

/// <summary>The ships of a snapshot as quads and instanced draws, on the CPU, per frame (docs/battle/plan.md step 2.3):
/// a ship long enough on screen, and whose art is up, is its sprite (the hull, the turrets at rest, their shadows and
/// the sun's shadow from the height map, sampled through the mip chain); a ship too small for its mips, or whose art
/// isn't drawn yet, is a stand-in icon a fixed number of pixels long, along its heading. The two cross-fade across a
/// few pixels. Draws are one per class and layer (per turret type for the turrets), so a fleet of one class is a
/// handful of draws. Nothing here allocates once the arrays have grown to the fleet.</summary>
public sealed class ShipDrawList
{
    /// <summary>The icon's length and width on screen, pixels; and the sprite's length in pixels across which the two
    /// cross-fade (below the first: icon only; above the second: sprite only).</summary>
    public const double IconLengthPx = 22, IconWidthPx = 8, FadeFromPx = 16, FadeToPx = 28;

    /// <summary>Sprites shorter than this on screen get no shadows: too small to see, and the march costs.</summary>
    public const double ShadowFromPx = 90;

    public static readonly Vector4 IconTint = new(0.95f, 0.92f, 0.82f, 1f), SelectedTint = new(1f, 0.78f, 0.25f, 1f);

    ShipQuad[] quads = new ShipQuad[1024];
    ShipDraw[] draws = new ShipDraw[64];
    int quadCount, drawCount;

    // per ship this frame
    struct Placed
    {
        public Vec3d Rel, F, S;
        public double PxPerM, LengthPx, Light, SpriteAlpha, IconAlpha;
        public Vector2 SunDir;
        public double SunTan;
        public bool Shadows;
        public int Class, Entity;
    }

    Placed[] placed = new Placed[64];
    int placedCount;
    readonly Dictionary<int, List<int>> byClass = [];
    readonly List<int> classes = [];

    public ReadOnlySpan<ShipQuad> Quads => quads.AsSpan(0, quadCount);
    public ReadOnlySpan<ShipDraw> Draws => draws.AsSpan(0, drawCount);

    /// <summary>How each ship was drawn this frame, by its index in the snapshot: 0 not at all (off screen or the far
    /// side), 1 icon, 2 sprite, 3 both (cross-fading).</summary>
    public byte[] DrawnAs { get; private set; } = new byte[64];

    /// <param name="artOf">A class's art, or null while it isn't up.</param>
    /// <param name="lengthOf">A class's length, metres (for the icon's fade before its art is up).</param>
    /// <param name="icon">The icon's texture.</param>
    /// <param name="hidden">Per snapshot ship, true to leave it out (drawn as part of its group's marker); may be
    /// shorter than the ships.</param>
    public void Build(in MapView view, ShipsView ships, long timeMs, Vec3d sun, Func<int, ShipArt?> artOf, Func<int, double> lengthOf,
        nint icon, int selectedEntity, ReadOnlySpan<bool> hidden = default)
    {
        quadCount = drawCount = placedCount = 0;
        foreach (var l in byClass.Values)
            l.Clear();
        classes.Clear();
        if (DrawnAs.Length < ships.Count)
            DrawnAs = new byte[Math.Max(ships.Count, DrawnAs.Length * 2)];
        if (placed.Length < ships.Count)
            placed = new Placed[Math.Max(ships.Count, placed.Length * 2)];

        // place every ship: where it is, how big on screen, sprite or icon, the sun in its frame
        for (int k = 0; k < ships.Count; k++)
        {
            DrawnAs[k] = 0;
            if (k < hidden.Length && hidden[k])
                continue;
            var (p, f) = ships.PoseAt(k, timeMs);
            if (!view.Project(p, out double x, out double y))
                continue;
            int cls = ships.Class[k];
            var art = artOf(cls);
            double length = lengthOf(cls);
            var rel = p - view.Eye;
            double pxPerM = 1 / view.MetresPerPixel * view.Altitude / Math.Max(Vec3d.Dot(rel, view.Forward), 1e-3);
            double lengthPx = length * pxPerM, margin = Math.Max(lengthPx, IconLengthPx);
            if (x < -margin || y < -margin || x > view.Width + margin || y > view.Height + margin)
                continue;
            var up = p.Normalized();
            var s = Vec3d.Cross(f, up);
            double sx = Vec3d.Dot(sun, f), sy = Vec3d.Dot(sun, s), sz = Vec3d.Dot(sun, up);
            double horiz = Math.Sqrt(sx * sx + sy * sy);
            double sprite = art != null && pxPerM >= art.MinPxPerM ? SmoothStep(FadeFromPx, FadeToPx, lengthPx) : 0;
            placed[placedCount] = new Placed
            {
                Rel = rel, F = f, S = s, PxPerM = pxPerM, LengthPx = lengthPx,
                Light = 0.3 + 0.7 * SmoothStep(-0.10, 0.12, sz),   // the globe's terminator (globe.frag)
                SpriteAlpha = sprite, IconAlpha = 1 - sprite,
                SunDir = horiz > 1e-9 ? new Vector2((float)(sx / horiz), (float)(sy / horiz)) : Vector2.UnitX,
                SunTan = Math.Max(sz / Math.Max(horiz, 1e-9), 0.15),
                Shadows = sz > 0.05 && lengthPx >= ShadowFromPx,
                Class = cls, Entity = ships.Entity[k],
            };
            DrawnAs[k] = (byte)((sprite > 0 ? 2 : 0) | (sprite < 1 ? 1 : 0));
            if (sprite > 0)
            {
                if (!byClass.TryGetValue(cls, out var list))
                    byClass[cls] = list = [];
                if (list.Count == 0)
                    classes.Add(cls);
                list.Add(placedCount);
            }
            placedCount++;
        }

        // the sprites, layer by layer, a draw per class (and turret type)
        foreach (int cls in classes)
        {
            var art = artOf(cls)!;
            int first = quadCount;
            foreach (int i in byClass[cls])
                Hull(ref placed[i], art);
            AddDraw(ShipDrawKind.Sprite, art.Hull, art.Height, first);
        }
        for (int layer = 0; layer < 2; layer++)   // turret shadows, then turrets
            foreach (int cls in classes)
            {
                var art = artOf(cls)!;
                for (int t = 0; t < art.Turrets.Length; t++)
                {
                    int first = quadCount;
                    foreach (int i in byClass[cls])
                        if (layer == 1 || placed[i].Shadows)
                            Turrets(ref placed[i], art, t, shadow: layer == 0);
                    AddDraw(ShipDrawKind.Sprite, art.Turrets[t], art.Height, first);
                }
            }
        foreach (int cls in classes)
        {
            var art = artOf(cls)!;
            int first = quadCount;
            foreach (int i in byClass[cls])
                if (placed[i].Shadows)
                    SunShadow(ref placed[i], art);
            AddDraw(ShipDrawKind.Shadow, art.Height, 0, first);
        }

        // the icons, all in one draw
        int firstIcon = quadCount;
        for (int i = 0; i < placedCount; i++)
            if (placed[i].IconAlpha > 0)
                Icon(ref placed[i], placed[i].Entity == selectedEntity);
        AddDraw(ShipDrawKind.Sprite, icon, icon, firstIcon);
    }

    void Hull(ref Placed p, ShipArt art)
    {
        double cw = art.CanvasX, ch = art.CanvasY;
        Emit(ref p, art, -cw / 2, -ch / 2, cw, 0, 0, ch, mode: 0, opacity: p.SpriteAlpha, roof: 0, Vector4.Zero);
    }

    void Turrets(ref Placed p, ShipArt art, int type, bool shadow)
    {
        var meta = art.Meta;
        var tm = meta.TurretTypes[art.TurretTypes[type]];
        double e = tm.SizePx[0] / art.Scale / 2, deck = meta.Shadow.DeckM;
        foreach (int i in art.DrawOrder)
        {
            if (art.MountType[i] != type)
                continue;
            var m = meta.Mounts[i];
            double a = m.RestDeg * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
            double axx = ca * 2 * e, axy = sa * 2 * e, ayx = -sa * 2 * e, ayy = ca * 2 * e;
            double ox = m.PosM[0] - axx / 2 - ayx / 2, oy = m.PosM[1] - axy / 2 - ayy / 2;
            if (shadow)
            {
                double off = (m.TopM - deck) / p.SunTan;   // shadow.sun_offset_px, in metres
                Emit(ref p, art, ox - p.SunDir.X * off, oy - p.SunDir.Y * off, axx, axy, ayx, ayy, mode: 1,
                    opacity: 0.4 * p.SpriteAlpha, roof: m.TopM, Vector4.Zero);
            }
            else
                Emit(ref p, art, ox, oy, axx, axy, ayx, ayy, mode: 0, opacity: p.SpriteAlpha, roof: 0, Vector4.Zero);
        }
    }

    void SunShadow(ref Placed p, ShipArt art)
    {
        double cw = art.CanvasX, ch = art.CanvasY;
        double reach = art.Meta.Shadow.MaxHeightM / p.SunTan;
        double lod = Math.Clamp(Math.Log2(art.Scale / p.PxPerM), 0, art.MipLevels);
        double step = Math.Max(0.1, Math.Pow(2, lod) / art.Scale);
        double steps = Math.Min(Math.Ceiling(reach / step), 96);
        Emit(ref p, art, -cw / 2 - reach, -ch / 2 - reach, cw + 2 * reach, 0, 0, ch + 2 * reach, mode: 0, opacity: p.SpriteAlpha, roof: 0,
            Vector4.Zero, new Vector4((float)step, (float)steps, 0.3f, (float)lod));
    }

    void Icon(ref Placed p, bool selected)
    {
        // a fixed size on screen, along the heading: metres per pixel at the ship
        double l = IconLengthPx / p.PxPerM, w = IconWidthPx / p.PxPerM;
        Emit(ref p, null, -l / 2, -w / 2, l, 0, 0, w, mode: 2, opacity: p.IconAlpha, roof: 0, selected ? SelectedTint : IconTint);
    }

    /// <summary>A quad from ship-local metres: corner (ox, oy), edges (ux, uy) and (vx, vy).</summary>
    void Emit(ref Placed p, ShipArt? art, double ox, double oy, double ux, double uy, double vx, double vy, int mode, double opacity,
        double roof, Vector4 tint, Vector4 march = default)
    {
        if (quadCount == quads.Length)
            Array.Resize(ref quads, quads.Length * 2);
        var o = p.Rel + p.F * ox + p.S * oy;
        var u = p.F * ux + p.S * uy;
        var v = p.F * vx + p.S * vy;
        var hmap = art != null
            ? new Vector4((float)(art.Scale / art.Meta.SizePx[0]), (float)(art.Scale / art.Meta.SizePx[1]), 0.5f, 0.5f)
            : Vector4.Zero;
        quads[quadCount++] = new ShipQuad
        {
            O = new Vector4((float)o.X, (float)o.Y, (float)o.Z, mode),
            U = new Vector4((float)u.X, (float)u.Y, (float)u.Z, (float)opacity),
            V = new Vector4((float)v.X, (float)v.Y, (float)v.Z, (float)roof),
            Ship = new Vector4((float)ox, (float)oy, (float)ux, (float)uy),
            ShipV = new Vector4((float)vx, (float)vy, (float)p.Light, 0),
            Sun = new Vector4(p.SunDir, (float)p.SunTan, 0.4f),
            March = march,
            Hmap = hmap,
            Tint = tint,
        };
    }

    void AddDraw(ShipDrawKind kind, nint tex0, nint tex1, int first)
    {
        if (quadCount == first)
            return;
        if (drawCount == draws.Length)
            Array.Resize(ref draws, draws.Length * 2);
        draws[drawCount++] = new ShipDraw { Kind = kind, Tex0 = tex0, Tex1 = tex1, First = first, Count = quadCount - first };
    }

    static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }
}

/// <summary>Draws a <see cref="ShipDrawList"/> inside the globe's main pass (an <see cref="IMapLayer"/>): the quads go
/// up in one storage buffer, then one instanced draw per entry. Pipelines are built for the target's format, samples
/// and depth; with depth, ships are tested against the masking sphere as the coast is, so the far side hides them.
/// Knows nothing of input, ImGui or the world.</summary>
public sealed unsafe class ShipBatchRenderer : IMapLayer, IDisposable
{
    const uint QuadBufferBytes = 8 << 20;   // ~58,000 quads

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fsSprite, fsShadow;
    readonly SDL_GPUSampler* linear, nearest;
    readonly DynamicGpuBuffer quadBuf;
    SDL_GPUGraphicsPipeline* spritePipe, shadowPipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    int uploaded;

    public ShipDrawList List { get; } = new();

    /// <summary>The stand-in icon: a hull's plan, white, mipmapped.</summary>
    public nint Icon { get; }

    struct ViewParams
    {
        public Vector4 Right, Up, Fwd, Depth;
    }

    public ShipBatchRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/shipmap.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fsSprite = GpuShader.Load(device, "Content/Shaders/Compiled/shipmap.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 2, 0, 0, 0);
        fsShadow = GpuShader.Load(device, "Content/Shaders/Compiled/shipmap_shadow.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 1, 0, 0, 0);
        if (vs == null || fsSprite == null || fsShadow == null)
            throw new InvalidOperationException("ship map shaders failed to load");
        linear = Sampler(SDL_GPUFilter.SDL_GPU_FILTER_LINEAR, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR);
        nearest = Sampler(SDL_GPUFilter.SDL_GPU_FILTER_NEAREST, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST);
        quadBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, QuadBufferBytes);
        Icon = (nint)SpriteTextures.Upload(device, SpriteImages.Chain(IconImage(), false, 5), height: false);
    }

    /// <summary>The icon: a hull's plan 128 x 32, bow to the right (+x), a fine bow and a rounded stern.</summary>
    static Image8 IconImage()
    {
        const int W = 128, H = 32;
        var img = new Image8(W, H, 4);
        for (int py = 0; py < H; py++)
            for (int px = 0; px < W; px++)
            {
                // coverage by 4x4 samples of the half-breadth: full amidships, a pointed bow, a round stern
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        double x = (px + (sx + 0.5) / 4) / W, y = Math.Abs((py + (sy + 0.5) / 4) / H - 0.5) * 2;
                        double half = x > 0.6 ? Math.Sqrt(Math.Max(0, 1 - Math.Pow((x - 0.6) / 0.4, 2))) * (1 - (x - 0.6) / 0.4 * 0.35)
                            : x < 0.12 ? Math.Sqrt(Math.Max(0, 1 - Math.Pow((0.12 - x) / 0.12, 2)))
                            : 1;
                        if (y <= half * 0.92)
                            hits++;
                    }
                int i = (py * W + px) * 4;
                byte a = (byte)(hits * 255 / 16);
                img.Data[i] = img.Data[i + 1] = img.Data[i + 2] = 255;
                img.Data[i + 3] = a;
            }
        return img;
    }

    SDL_GPUSampler* Sampler(SDL_GPUFilter f, SDL_GPUSamplerMipmapMode m)
    {
        var info = new SDL_GPUSamplerCreateInfo
        {
            min_filter = f, mag_filter = f, mipmap_mode = m,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            max_lod = 1000,
        };
        return SDL_CreateGPUSampler(device, &info);
    }

    void EnsurePipelines(RenderTarget target)
    {
        if (spritePipe != null && pipeFormat == target.ColorFormat && pipeSamples == target.SampleCount && pipeDepth == target.HasDepth)
            return;
        ReleasePipelines();
        (pipeFormat, pipeSamples, pipeDepth) = (target.ColorFormat, target.SampleCount, target.HasDepth);
        GpuPipelineBuilder B()
        {
            var b = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
                .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingPremultiplied().ClearVertexLayout();
            return target.HasDepth
                ? b.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
                : b.NoDepthTarget().DisableDepthTest();
        }
        spritePipe = B().SetShaders(vs, fsSprite).Build(device);
        shadowPipe = B().SetShaders(vs, fsShadow).Build(device);
        if (spritePipe == null || shadowPipe == null)
            throw new InvalidOperationException($"ship map pipelines: {SDL_GetError()}");
    }

    void ReleasePipelines()
    {
        if (spritePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, spritePipe);
        if (shadowPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, shadowPipe);
        spritePipe = shadowPipe = null;
    }

    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        var q = List.Quads;
        uploaded = Math.Min(q.Length, (int)(QuadBufferBytes / ShipQuad.SizeInBytes));
        quadBuf.Upload(cmd, q[..uploaded]);
    }

    public void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0)
            return;
        EnsurePipelines(target);
        var (near, far) = GlobeRenderer.DepthRange(view);
        var sb = quadBuf.Buffer;
        SDL_GPUGraphicsPipeline* bound = null;
        foreach (var d in List.Draws)
        {
            if (d.First + d.Count > uploaded)
                break;
            var pipe = d.Kind == ShipDrawKind.Shadow ? shadowPipe : spritePipe;
            if (pipe != bound)
            {
                SDL_BindGPUGraphicsPipeline(pass, pipe);
                SDL_BindGPUVertexStorageBuffers(pass, 0, &sb, 1);
                bound = pipe;
            }
            var vp = new ViewParams
            {
                Right = new Vector4(F(view.Right), (float)view.TanHalfX),
                Up = new Vector4(F(view.Up), (float)view.TanHalfY),
                Fwd = new Vector4(F(view.Forward), 0),
                Depth = new Vector4((float)near, (float)far, d.First, 0),
            };
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&vp), (uint)sizeof(ViewParams));
            if (d.Kind == ShipDrawKind.Shadow)
            {
                var b = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)d.Tex0, sampler = nearest };
                SDL_BindGPUFragmentSamplers(pass, 0, &b, 1);
            }
            else
            {
                var b = stackalloc SDL_GPUTextureSamplerBinding[2];
                b[0] = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)d.Tex0, sampler = linear };
                b[1] = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)d.Tex1, sampler = nearest };
                SDL_BindGPUFragmentSamplers(pass, 0, b, 2);
            }
            SDL_DrawGPUPrimitives(pass, 6, (uint)d.Count, 0, 0);
        }
    }

    static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);

    public void Dispose()
    {
        SDL_WaitForGPUIdle(device);
        ReleasePipelines();
        quadBuf.Dispose();
        SDL_ReleaseGPUTexture(device, (SDL_GPUTexture*)Icon);
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fsSprite);
        SDL_ReleaseGPUShader(device, fsShadow);
        SDL_ReleaseGPUSampler(device, linear);
        SDL_ReleaseGPUSampler(device, nearest);
    }
}
