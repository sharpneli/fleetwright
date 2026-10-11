using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright;

/// <summary>The profile tab: the session's ship as a poster to hang on a wall. Its name over the plan and the profile at
/// one scale with a scale bar, then the recognition silhouette small beside the headline figures in big type and the
/// particulars (ShipCard). The plan is the sprite (ShipSpriteRenderer, turrets at rest, no shadow) drawn into a target
/// of its own at the poster's scale; the profile and silhouette (ShipProfile) are made only while the tab is open, off
/// the render thread with a baker of its own, at the scale nearest above the poster's, so the designer's builds don't
/// wait for them. The design and look are the session's (picked in the designer or the ship viewer).</summary>
public sealed unsafe class ProfileScene : IScene
{
    static readonly float[] Scales = [5, 10, 20];
    static readonly Vector4 Paper = new(0.91f, 0.89f, 0.84f, 1);
    static readonly uint Ink = ImGui.ColorConvertFloat4ToU32(new(0.11f, 0.13f, 0.15f, 1));
    static readonly uint Grey = ImGui.ColorConvertFloat4ToU32(new(0.38f, 0.40f, 0.42f, 1));
    static readonly uint Faint = ImGui.ColorConvertFloat4ToU32(new(0.11f, 0.13f, 0.15f, 0.35f));
    static readonly string[] CharStrings = [.. Enumerable.Range(0, 0x2213).Select(c => ((char)c).ToString())];   // tracked text, no garbage

    readonly SDL_GPUDevice* device;
    readonly DesignSession session;
    GpuBaker? baker;   // used by one job at a time, never on the render thread

    // the profile and silhouette shown, and what they were made from
    SDL_GPUTexture* fullTex, silTex;
    Vector2 fullSize, silSize;
    float bakedScale = 10, silWaterline;
    (int Version, float Scale) shown = (-1, -1);
    string status = "";
    Task<Made>? job;
    (int Version, float Scale) jobKey;
    float wantScale = 10;

    // the plan: the sprite into a target of its own, at the poster's scale
    readonly ShipSpriteRenderer plan;
    RenderTarget? planTarget;
    int planVersion = -1;
    float planZoom;

    // the text, made once per ship
    ShipCard? card;
    int cardVersion = -1;

    sealed record Made(Image8? Full, Image8? Silhouette, float SilWaterline, string Status);

    public ProfileScene(SDL_GPUDevice* device, DesignSession session)
    {
        this.device = device;
        this.session = session;
        plan = new ShipSpriteRenderer(device);
        Start();
        Take(job!.Result);   // the first one at once, so the first frame (and a capture) has it
    }

    /// <summary>Starts a job for the session's ship as it is now, at the wanted scale.</summary>
    void Start()
    {
        jobKey = (session.Version, wantScale);
        var ship = session.Ship;
        var look = session.Look;
        string why = session.Status;
        double scale = wantScale;
        job = Task.Run(() =>
        {
            if (ship == null)
                return new Made(null, null, 0, why);
            try
            {
                var p = ShipProfile.Build(ship, scale, look);
                baker ??= new GpuBaker(device);
                var (full, sil) = ProfileBake.Bake(p, baker);
                return new Made(full, sil, (float)p.SilhouetteWaterlinePx, "");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"profile: {ship.Design.Id}: {e}");
                return new Made(null, null, 0, $"{e.GetType().Name}: {e.Message}");
            }
        });
    }

    /// <summary>Shows a finished job's images (the old textures go at once: SDL keeps them until the GPU is done).</summary>
    void Take(Made m)
    {
        shown = jobKey;
        job = null;
        status = m.Status;
        if (m.Full == null || m.Silhouette == null)
            return;   // no ship: the last poster stays, with the status saying why
        Release();
        fullTex = Upload(m.Full);
        silTex = Upload(m.Silhouette);
        fullSize = new(m.Full.Width, m.Full.Height);
        silSize = new(m.Silhouette.Width, m.Silhouette.Height);
        bakedScale = jobKey.Scale;
        silWaterline = m.SilWaterline;
    }

    SDL_GPUTexture* Upload(Image8 im)
    {
        var info = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
            width = (uint)im.Width,
            height = (uint)im.Height,
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
        };
        var tex = SDL_CreateGPUTexture(device, &info);
        if (tex == null)
            throw new InvalidOperationException($"profile texture: {SDL_GetError()}");
        var ti = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, size = (uint)im.Data.Length };
        var tb = SDL_CreateGPUTransferBuffer(device, &ti);
        var dst = (byte*)SDL_MapGPUTransferBuffer(device, tb, false);
        im.Data.CopyTo(new Span<byte>(dst, im.Data.Length));
        SDL_UnmapGPUTransferBuffer(device, tb);
        var cmd = SDL_AcquireGPUCommandBuffer(device);
        var cp = SDL_BeginGPUCopyPass(cmd);
        var src = new SDL_GPUTextureTransferInfo { transfer_buffer = tb, pixels_per_row = (uint)im.Width, rows_per_layer = (uint)im.Height };
        var region = new SDL_GPUTextureRegion { texture = tex, w = (uint)im.Width, h = (uint)im.Height, d = 1 };
        SDL_UploadToGPUTexture(cp, &src, &region, false);
        SDL_EndGPUCopyPass(cp);
        SDL_SubmitGPUCommandBuffer(cmd);
        SDL_ReleaseGPUTransferBuffer(device, tb);
        return tex;
    }

    void Release()
    {
        if (fullTex != null)
            SDL_ReleaseGPUTexture(device, fullTex);
        if (silTex != null)
            SDL_ReleaseGPUTexture(device, silTex);
        fullTex = silTex = null;
    }

    static SDL_FColor PaperClear => new() { r = Paper.X, g = Paper.Y, b = Paper.Z, a = 1 };

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt)
    {
        if (planTarget != null && plan.Meta != null)
        {
            var view = ShipSpriteView.Default;
            view.Zoom = planZoom;
            view.Centre = Vector2.Zero;
            view.Shadows = false;
            plan.Render(cmd, planTarget, view, ReadOnlySpan<double>.Empty, PaperClear);
        }
        var pass = target.BeginPass(cmd, PaperClear, depth: false);
        SDL_EndGPURenderPass(pass);
    }

    // ------------------------------------------------------------------ the poster

    /// <summary>Text spaced out letter by letter (tracking, in px), from x; returns its width.</summary>
    static float Tracked(ImDrawListPtr dl, ImFontPtr font, Vector2 at, uint col, string text, float track, bool draw = true)
    {
        float size = font.FontSize, x = at.X;
        foreach (char ch in text)
        {
            var s = ch < CharStrings.Length ? CharStrings[ch] : "?";
            if (draw)
                dl.AddText(font, size, new Vector2(x, at.Y), col, s);
            x += font.CalcTextSizeA(size, float.MaxValue, 0, s).X + track;
        }
        return x - at.X - (text.Length > 0 ? track : 0);
    }

    static float Width(ImFontPtr font, string text) => font.CalcTextSizeA(font.FontSize, float.MaxValue, 0, text).X;

    /// <summary>A round length for the scale bar: the largest of 5, 10, 20, 25, 50, 100 m up to max.</summary>
    static float BarLength(float max) => new[] { 100f, 50f, 25f, 20f, 10f, 5f }.FirstOrDefault(l => l <= max, 5f);

    public void BuildUi()
    {
        if (job is { IsCompleted: true })
            Take(job.Result);
        if (job == null && shown != (session.Version, wantScale))
            Start();
        if (planVersion != session.Version)
        {
            planVersion = session.Version;
            if (session.Result is { Sprites: not null } r)
                plan.Upload(r);
        }
        if (cardVersion != session.Version)
        {
            cardVersion = session.Version;
            if (session.Ship is { } ship)
                card = ShipCard.Of(ship, session.Look);
        }

        var vp = ImGui.GetMainViewport();
        Vector2 pos = vp.WorkPos, size = vp.WorkSize;
        float s = UiFonts.Scale, m = 34 * s;
        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(size);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoBringToFrontOnFocus;
        ImGui.Begin("##ship card", flags);
        var dl = ImGui.GetWindowDrawList();

        // the frame: a heavy rule and a hairline inside it
        dl.AddRect(pos + new Vector2(m / 2), pos + size - new Vector2(m / 2), Ink, 0, ImDrawFlags.None, 2.2f * s);
        dl.AddRect(pos + new Vector2(m / 2 + 5 * s), pos + size - new Vector2(m / 2 + 5 * s), Ink, 0, ImDrawFlags.None, 0.8f * s);
        float x0 = pos.X + 1.4f * m, x1 = pos.X + size.X - 1.4f * m, y = pos.Y + 1.2f * m;

        if (card == null || fullTex == null)
        {
            string why = status.Length > 0 ? status : session.Status.Length > 0 ? session.Status : "no ship";
            dl.AddText(UiFonts.PosterText, UiFonts.PosterText.FontSize, new Vector2(x0, y), Grey, why);
            ImGui.End();
            return;
        }

        // the name, tracked wide, and the line under it
        float track = 6 * s;
        float tw = Tracked(dl, UiFonts.PosterTitle, Vector2.Zero, Ink, card.Name, track, draw: false);
        Tracked(dl, UiFonts.PosterTitle, new Vector2((x0 + x1 - tw) / 2, y), Ink, card.Name, track);
        y += UiFonts.PosterTitle.FontSize + 2 * s;
        float sw = Tracked(dl, UiFonts.SansBold, Vector2.Zero, Grey, card.Subtitle, 2.5f * s, draw: false);
        Tracked(dl, UiFonts.SansBold, new Vector2((x0 + x1 - sw) / 2, y), Grey, card.Subtitle, 2.5f * s);
        y += UiFonts.SansBold.FontSize + 12 * s;
        dl.AddLine(new Vector2(x0, y), new Vector2(x1, y), Ink, 1.6f * s);
        dl.AddLine(new Vector2(x0, y + 3 * s), new Vector2(x1, y + 3 * s), Ink, 0.6f * s);
        y += 16 * s;

        // the lower band: silhouette, headline figures, particulars
        float bandBottom = pos.Y + size.Y - 1.25f * m;
        float bandH = Math.Clamp(size.Y * 0.3f, 190 * s, 330 * s), bandTop = bandBottom - bandH;

        // the plan over the profile, one scale, as large as the space between allows
        float barH = 26 * s;
        var planM = plan.CanvasM;
        Vector2 profM = fullSize / bakedScale;
        float drawW = x1 - x0, drawH = bandTop - 18 * s - barH - y;
        float overlap = (float)HullArt.PadM;   // the plan's margin under the hull and the profile's over the masts share one band
        float k = Math.Min(drawW / Math.Max(planM.X, profM.X), drawH / Math.Max(1, planM.Y + profM.Y - overlap));
        wantScale = Scales.FirstOrDefault(sc => sc >= k * 0.98f, Scales[^1]);
        float cx = (x0 + x1) / 2;
        float stackH = (planM.Y + profM.Y - overlap) * k, top = y + (drawH - stackH) / 2;
        if (planM.X > 0)
        {
            uint pw = (uint)Math.Max(16, MathF.Round(planM.X * k)), ph = (uint)Math.Max(16, MathF.Round(planM.Y * k));
            if (planTarget == null)
                planTarget = new RenderTarget(device, pw, ph, SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_4);
            else if (planTarget.Width != pw || planTarget.Height != ph)
                planTarget.Resize(pw, ph);
            planZoom = k;
            var p0 = new Vector2(MathF.Round(cx - pw / 2f), MathF.Round(top));
            dl.AddImage((nint)planTarget.Resolve, p0, p0 + new Vector2(pw, ph));
        }
        var f0 = new Vector2(cx - profM.X * k / 2, top + (planM.Y - overlap) * k);
        var f1 = f0 + profM * k;
        dl.AddImage((nint)fullTex, f0, f1);

        // the scale bar under the profile: five cells, alternately solid and open
        float barM = BarLength(0.2f * (float)session.Ship!.Hitboxes.Length), by = f1.Y + 6 * s, bx = x0;
        for (int i = 0; i < 5; i++)
        {
            var a = new Vector2(bx + barM * k * i / 5, by);
            var b = new Vector2(bx + barM * k * (i + 1) / 5, by + 5 * s);
            if (i % 2 == 0)
                dl.AddRectFilled(a, b, Ink);
            dl.AddRect(a, b, Ink, 0, ImDrawFlags.None, 0.8f * s);
        }
        dl.AddText(UiFonts.SansSmall, UiFonts.SansSmall.FontSize, new Vector2(bx, by + 7 * s), Grey, "0");
        dl.AddText(UiFonts.SansSmall, UiFonts.SansSmall.FontSize, new Vector2(bx + barM * k - 6 * s, by + 7 * s), Grey, BarLabel(barM));
        string toScale = "PLAN AND PROFILE TO SCALE";
        Tracked(dl, UiFonts.SansSmall, new Vector2(x1 - Tracked(dl, UiFonts.SansSmall, Vector2.Zero, Grey, toScale, 2 * s, false), by), Grey, toScale, 2 * s);

        dl.AddLine(new Vector2(x0, bandTop), new Vector2(x1, bandTop), Ink, 1.6f * s);
        dl.AddLine(new Vector2(x0, bandTop + 3 * s), new Vector2(x1, bandTop + 3 * s), Ink, 0.6f * s);

        // the silhouette, small, sitting on its waterline
        float boxW = 0.34f * (x1 - x0), pad = 14 * s, capY = bandTop + pad + 4 * s;
        Tracked(dl, UiFonts.SansSmall, new Vector2(x0, capY), Grey, "RECOGNITION SILHOUETTE", 2 * s);
        float availH = bandBottom - capY - UiFonts.SansSmall.FontSize - 2 * pad;
        float ks = Math.Min(boxW / silSize.X, availH / silSize.Y);
        float silTop = capY + UiFonts.SansSmall.FontSize + pad;   // centred in what is left under the caption
        var s1 = new Vector2(x0 + (boxW - silSize.X * ks) / 2, silTop + (bandBottom - pad - silTop - silSize.Y * ks) / 2);
        dl.AddImage((nint)silTex, s1, s1 + silSize * ks);
        float wly = s1.Y + silWaterline * ks;
        dl.AddLine(new Vector2(x0, wly), new Vector2(x0 + boxW, wly), Faint, 1f * s);
        float divX = x0 + boxW + 22 * s;
        dl.AddLine(new Vector2(divX, bandTop + pad), new Vector2(divX, bandBottom - pad), Faint, 1f * s);

        // the headline figures across the top of the right side, the particulars in two columns under them
        float rx0 = divX + 26 * s, rx1 = x1, ry = bandTop + pad + 2 * s;
        int nh = card.Heroes.Length;
        float cw = (rx1 - rx0) / Math.Max(1, nh);
        for (int i = 0; i < nh; i++)
        {
            var (value, label) = card.Heroes[i];
            float hx = rx0 + i * cw;
            dl.AddText(UiFonts.PosterFigure, UiFonts.PosterFigure.FontSize, new Vector2(hx, ry), Ink, value);
            Tracked(dl, UiFonts.SansSmall, new Vector2(hx, ry + UiFonts.PosterFigure.FontSize + 2 * s), Grey, label.ToUpperInvariant(), 1.5f * s);
        }
        ry += UiFonts.PosterFigure.FontSize + UiFonts.SansSmall.FontSize + 14 * s;
        dl.AddLine(new Vector2(rx0, ry), new Vector2(rx1, ry), Faint, 1f * s);
        ry += 10 * s;
        float rowH = UiFonts.PosterText.FontSize + 5 * s, colW = (rx1 - rx0) / 2, labelW = 104 * s;
        int fit = Math.Max(1, (int)((bandBottom - pad - ry) / rowH)), perCol = Math.Min(fit, Math.Max(1, (card.Rows.Length + 1) / 2));
        for (int i = 0; i < card.Rows.Length && i < 2 * perCol; i++)
        {
            var (label, value) = card.Rows[i];
            float lx = rx0 + (i / perCol) * colW, ly = ry + (i % perCol) * rowH;
            Tracked(dl, UiFonts.SansSmall, new Vector2(lx, ly + 3 * s), Grey, label.ToUpperInvariant(), 1.2f * s);
            dl.PushClipRect(new Vector2(lx + labelW, ly), new Vector2(lx + colW - 10 * s, ly + rowH), true);
            dl.AddText(UiFonts.PosterText, UiFonts.PosterText.FontSize, new Vector2(lx + labelW, ly), Ink, value);
            dl.PopClipRect();
        }

        // the imprint, small in the bottom margin
        string imprint = "FLEETWRIGHT  ·  SHIP CARD";
        float iw = Tracked(dl, UiFonts.SansSmall, Vector2.Zero, Grey, imprint, 2 * s, false);
        Tracked(dl, UiFonts.SansSmall, new Vector2((x0 + x1 - iw) / 2, bandBottom + 0.25f * m - UiFonts.SansSmall.FontSize / 2), Grey, imprint, 2 * s);
        ImGui.End();
    }

    static readonly Dictionary<float, string> BarLabels = [];

    static string BarLabel(float m)
    {
        if (!BarLabels.TryGetValue(m, out var s))
            BarLabels[m] = s = $"{m:0} m";
        return s;
    }

    public bool ProcessEvent(SDL_Event* e, uint width, uint height) => false;

    public void Dispose()
    {
        try
        {
            job?.Wait();
        }
        catch (AggregateException)
        {
        }
        baker?.Dispose();
        Release();
        plan.Dispose();
        planTarget?.Dispose();
    }
}
