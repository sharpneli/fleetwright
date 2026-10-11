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

/// <summary>The profile tab: the session's ship from the starboard side as the ship card will show it (ShipProfile,
/// docs/shipgen/profile-plan.md), the full hull above the recognition silhouette, in the session's look. A peek while
/// designing: the profile is made only while the tab is open, off the render thread with a baker of its own, so the
/// designer's builds don't wait for it. The panel picks the scale it is drawn at and the zoom it is shown at.</summary>
public sealed unsafe class ProfileScene : IScene
{
    static readonly float[] Scales = [5, 10, 20];
    static readonly string[] ScaleNames = ["5 px/m", "10 px/m", "20 px/m"];
    static readonly Vector4 Paper = new(0.91f, 0.89f, 0.84f, 1), Night = new(0.13f, 0.15f, 0.17f, 1);

    readonly SDL_GPUDevice* device;
    readonly DesignSession session;
    GpuBaker? baker;   // used by one job at a time, never on the render thread

    // what is shown, and what it was made from
    SDL_GPUTexture* fullTex, silTex;
    Vector2 fullSize, silSize;
    (int Version, int Scale) shown = (-1, -1);
    string status = "";

    // the job making the next one
    Task<Made>? job;
    (int Version, int Scale) jobKey;

    // controls
    int scaleIndex = 1;
    bool fit = true, dark;
    readonly string[] navies = ["(design)", .. Shipgen.Render.Looks.Navies], eras = ["(design)", .. Shipgen.Render.Looks.Eras];

    sealed record Made(ShipProfile? Profile, Image8? Full, Image8? Silhouette, string Status);

    public ProfileScene(SDL_GPUDevice* device, DesignSession session)
    {
        this.device = device;
        this.session = session;
        Start();
        Take(job!.Result);   // the first one at once, so the first frame (and a capture) has it
    }

    /// <summary>Starts a job for the session's ship as it is now, at the picked scale.</summary>
    void Start()
    {
        jobKey = (session.Version, scaleIndex);
        var ship = session.Ship;
        var look = session.Look;
        string why = session.Status;
        double scale = Scales[scaleIndex];
        job = Task.Run(() =>
        {
            if (ship == null)
                return new Made(null, null, null, why);
            try
            {
                var sw = Stopwatch.StartNew();
                var p = ShipProfile.Build(ship, scale, look);
                double tDraw = sw.Elapsed.TotalSeconds;
                baker ??= new GpuBaker(device);
                var (full, sil) = ProfileBake.Bake(p, baker);
                return new Made(p, full, sil, $"{ship.Design.Name ?? ship.Design.Id}\n{full.Width} x {full.Height} px at {scale:0} px/m\n" +
                                              $"draw {tDraw * 1000:F0} ms, bake {(sw.Elapsed.TotalSeconds - tDraw) * 1000:F0} ms");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"profile: {ship.Design.Id}: {e}");
                return new Made(null, null, null, $"{e.GetType().Name}: {e.Message}");
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
            return;   // no ship: the last profile stays, under the status saying why
        Release();
        fullTex = Upload(m.Full);
        silTex = Upload(m.Silhouette);
        fullSize = new(m.Full.Width, m.Full.Height);
        silSize = new(m.Silhouette.Width, m.Silhouette.Height);
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

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt)
    {
        var bg = dark ? Night : Paper;
        var pass = target.BeginPass(cmd, new SDL_FColor { r = bg.X, g = bg.Y, b = bg.Z, a = 1 }, depth: false);
        SDL_EndGPURenderPass(pass);
    }

    // ------------------------------------------------------------------ UI

    public void BuildUi()
    {
        // a finished job is shown here, before the images are referenced; a newer ship or scale starts the next
        if (job is { IsCompleted: true })
            Take(job.Result);
        if (job == null && shown != (session.Version, scaleIndex))
            Start();

        SidePanel.Begin("Profile");
        // the design and look are the session's, shared with the other tabs (as the ship viewer picks them)
        int designIndex = session.Index;
        if (ImGui.Combo("design", ref designIndex, session.Names, session.Names.Length))
            session.Select(designIndex);
        if (session.Edited)
            ImGui.TextColored(new Vector4(0.88f, 0.68f, 0.31f, 1), "edited in the designer");
        int navyIndex = Math.Max(0, Array.IndexOf(navies, session.Look?.Navy)), eraIndex = Math.Max(0, Array.IndexOf(eras, session.Look?.Era));
        if (ImGui.Combo("navy", ref navyIndex, navies, navies.Length) | ImGui.Combo("era", ref eraIndex, eras, eras.Length))
            session.SetLook(navyIndex > 0 || eraIndex > 0
                ? new LookInput { Navy = navyIndex > 0 ? navies[navyIndex] : null, Era = eraIndex > 0 ? eras[eraIndex] : null }
                : null);
        ImGui.Separator();
        ImGui.Combo("drawn at", ref scaleIndex, ScaleNames, ScaleNames.Length);
        ImGui.Checkbox("fit to the window", ref fit);
        ImGui.Checkbox("dark background", ref dark);
        ImGui.Separator();
        ImGui.TextWrapped(job != null || session.Busy ? "drawing...\n" + status : status);
        float panelRight = ImGui.GetWindowPos().X + ImGui.GetWindowWidth();
        ImGui.End();

        // the pictures: the full profile above the silhouette, on the background Draw clears to
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new Vector2(panelRight, vp.WorkPos.Y));
        ImGui.SetNextWindowSize(new Vector2(vp.WorkPos.X + vp.WorkSize.X - panelRight, vp.WorkSize.Y));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoSavedSettings;
        flags |= fit ? ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse : ImGuiWindowFlags.HorizontalScrollbar;
        if (ImGui.Begin("##profile pictures", flags) && fullTex != null)
        {
            float pad = 24 * UiFonts.Scale;
            var avail = ImGui.GetContentRegionAvail();
            float k = fit ? Math.Min((avail.X - 2 * pad) / fullSize.X, (avail.Y - 3 * pad) / (fullSize.Y + silSize.Y)) : 1;
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(pad, pad));
            ImGui.Image((nint)fullTex, fullSize * k);
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(pad, pad));
            ImGui.Image((nint)silTex, silSize * k);
        }
        ImGui.End();
        ImGui.PopStyleColor();
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
    }
}
