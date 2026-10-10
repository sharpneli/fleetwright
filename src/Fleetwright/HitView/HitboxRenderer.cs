using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.HitView;

/// <summary>
/// Draws a <see cref="HitboxMesh"/> into any <see cref="RenderTarget"/> with depth: the main target, or a small
/// offscreen one shown inside other UI. Solid faces first (they write depth), then the translucent ones far to near
/// (re-sorted when the view direction changes), then the outlines. What is shown comes from a camera and a view
/// state, plain structs the caller owns; the renderer knows nothing of input, ImGui or the window. Pipelines are
/// built for the target's colour format and sample count, and rebuilt if a different target comes.
/// </summary>
public sealed unsafe class HitboxRenderer : IDisposable
{
    public static readonly Vector4 Background = new(34 / 255f, 40 / 255f, 48 / 255f, 1);
    static readonly Vector3 Light = Vector3.Normalize(new(-0.35f, -0.45f, 0.82f));   // hitview.LIGHT
    const int MaxKinds = 64;

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fs;
    SDL_GPUGraphicsPipeline* solidPipe, clearPipe, linePipe;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;

    // the mesh on the GPU
    HitboxMesh? mesh;
    SDL_GPUBuffer* vertexBuf, solidIdx, lineIdx;
    DynamicGpuBuffer? clearIdx;
    uint solidCount, lineCount;
    Vector3 centre;
    float radius = 1;

    // the translucent prisms and their order, far to near (allocated per mesh, not per frame)
    int[] clearPrisms = [];
    Vector3[] centroids = [];
    float[] keys = [];
    uint[] clearIndices = [];
    Vector3 sortedFor = new(float.NaN);

    FsParams fsParams;

    public HitboxMesh? Mesh => mesh;

    public HitboxRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/hitbox.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 1);
        fs = GpuShader.Load(device, "Content/Shaders/Compiled/hitbox.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 1);
        if (vs == null || fs == null)
            throw new InvalidOperationException("hitbox shaders failed to load");
        for (int k = 0; k < HitKinds.All.Length && k < MaxKinds; k++)
        {
            var hk = HitKinds.All[k];
            var edge = hk.Colour * 0.45f;   // hitview: the fill (shaded) times 0.6
            float ea = Math.Min(1f, hk.Alpha + (hk.Filler ? 20 : 60) / 255f);
            fsParams.Colours[4 * k] = hk.Colour.X;
            fsParams.Colours[4 * k + 1] = hk.Colour.Y;
            fsParams.Colours[4 * k + 2] = hk.Colour.Z;
            fsParams.Colours[4 * k + 3] = hk.Alpha;
            fsParams.Edges[4 * k] = edge.X;
            fsParams.Edges[4 * k + 1] = edge.Y;
            fsParams.Edges[4 * k + 2] = edge.Z;
            fsParams.Edges[4 * k + 3] = hk.Edges ? ea : 0;
        }
    }

    /// <summary>Puts a mesh on the GPU, replacing the last one (waits for the GPU; load time, not per frame).</summary>
    public void Upload(HitboxMesh m)
    {
        ReleaseMesh();
        mesh = m;
        var solid = new List<uint>();
        var clear = new List<int>();
        for (int i = 0; i < m.Prisms.Length; i++)
        {
            var p = m.Prisms[i];
            if (HitKinds.All[p.Kind].Solid)
                solid.AddRange(m.Indices.AsSpan(p.FirstIndex, p.IndexCount));
            else if (p.IndexCount > 0)
                clear.Add(i);
        }
        vertexBuf = GpuUpload.Buffer<HitVertex>(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, m.Vertices);
        solidIdx = GpuUpload.Buffer<uint>(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX, solid.ToArray());
        lineIdx = GpuUpload.Buffer<uint>(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX, m.EdgeIndices);
        (solidCount, lineCount) = ((uint)solid.Count, (uint)m.EdgeIndices.Length);

        clearPrisms = clear.ToArray();
        centroids = clear.Select(i => (m.Prisms[i].Min + m.Prisms[i].Max) / 2).ToArray();
        keys = new float[clearPrisms.Length];
        clearIndices = new uint[clear.Sum(i => m.Prisms[i].IndexCount)];
        clearIdx = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX, (uint)(clearIndices.Length * sizeof(uint)));
        sortedFor = new(float.NaN);
        centre = (m.Min + m.Max) / 2;
        radius = Math.Max(1, (m.Max - m.Min).Length() / 2 + 5);
    }

    /// <summary>Draws the mesh into <paramref name="target"/> (which must have depth), clearing it first. Records a
    /// copy pass on <paramref name="cmd"/> when the translucent order changes, so call it outside any pass.</summary>
    public void Render(SDL_GPUCommandBuffer* cmd, RenderTarget target, in HitboxCamera cam, in HitboxViewState state)
    {
        if (!target.HasDepth)
            throw new ArgumentException("the hitbox view needs a target with depth");
        EnsurePipelines(target);
        var bg = new SDL_FColor { r = Background.X, g = Background.Y, b = Background.Z, a = 1 };
        if (mesh == null || mesh.Prisms.Length == 0)
        {
            SDL_EndGPURenderPass(target.BeginPass(cmd, bg));
            return;
        }
        var (_, _, forward) = cam.Basis();
        if (Vector3.DistanceSquared(forward, sortedFor) > 1e-6f || float.IsNaN(sortedFor.X))
            SortTranslucent(cmd, forward);

        float aspect = (float)target.Width / Math.Max(1, target.Height);
        var vsp = new VsParams
        {
            ViewProj = cam.ViewProj(aspect, centre, radius),
            MaskLo = (uint)state.KindMask,
            MaskHi = (uint)(state.KindMask >> 32),
        };
        // lines are pulled toward the camera by about two pixels (orthographic) or a fixed small step (perspective)
        float lineBias = cam.Perspective ? 2e-4f : 4 * cam.HalfHeight / Math.Max(1, target.Height) / (2 * radius);
        fsParams.Light = new(Light, 0);
        fsParams.ClipMin = new(state.ClipMin, state.Clip ? 1 : 0);
        fsParams.ClipMax = new(state.ClipMax, 0);
        fsParams.Hover = state.Hover;
        fsParams.Selected = state.Selected;

        var pass = target.BeginPass(cmd, bg);
        var vb = new SDL_GPUBufferBinding { buffer = vertexBuf };
        SDL_BindGPUVertexBuffers(pass, 0, &vb, 1);
        Draw(cmd, pass, solidPipe, solidIdx, solidCount, ref vsp, 0, 0);
        Draw(cmd, pass, clearPipe, clearIdx!.Buffer, (uint)clearIndices.Length, ref vsp, 1, 0);
        Draw(cmd, pass, linePipe, lineIdx, lineCount, ref vsp, 2, lineBias);
        SDL_EndGPURenderPass(pass);
    }

    void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, SDL_GPUGraphicsPipeline* pipe, SDL_GPUBuffer* idx, uint count,
        ref VsParams vsp, int mode, float bias)
    {
        if (count == 0)
            return;
        vsp.Misc = new(bias, 0, 0, 0);
        fsParams.Light.W = mode;
        SDL_BindGPUGraphicsPipeline(pass, pipe);
        var ib = new SDL_GPUBufferBinding { buffer = idx };
        SDL_BindGPUIndexBuffer(pass, &ib, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_32BIT);
        fixed (VsParams* v = &vsp)
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)v, (uint)sizeof(VsParams));
        fixed (FsParams* f = &fsParams)
            SDL_PushGPUFragmentUniformData(cmd, 0, (nint)f, (uint)sizeof(FsParams));
        SDL_DrawGPUIndexedPrimitives(pass, count, 1, 0, 0, 0);
    }

    /// <summary>Orders the translucent prisms far to near along <paramref name="forward"/> and uploads their indices.</summary>
    void SortTranslucent(SDL_GPUCommandBuffer* cmd, Vector3 forward)
    {
        sortedFor = forward;
        if (clearPrisms.Length == 0)
            return;
        for (int i = 0; i < clearPrisms.Length; i++)   // the order kept from the last sort: nearly sorted already
            keys[i] = -Vector3.Dot(centroids[i], forward);
        Array.Sort(keys, clearPrisms);
        // keep the centroids with their prisms: recompute them from the sorted prisms
        int n = 0;
        for (int i = 0; i < clearPrisms.Length; i++)
        {
            var p = mesh!.Prisms[clearPrisms[i]];
            centroids[i] = (p.Min + p.Max) / 2;
            mesh.Indices.AsSpan(p.FirstIndex, p.IndexCount).CopyTo(clearIndices.AsSpan(n));
            n += p.IndexCount;
        }
        clearIdx!.Upload<uint>(cmd, clearIndices);
    }

    void EnsurePipelines(RenderTarget target)
    {
        if (solidPipe != null && pipeFormat == target.ColorFormat && pipeSamples == target.SampleCount)
            return;
        ReleasePipelines();
        (pipeFormat, pipeSamples) = (target.ColorFormat, target.SampleCount);
        GpuPipelineBuilder Base() => new GpuPipelineBuilder()
            .SetShaders(vs, fs)
            .SetColorFormat(target.ColorFormat)
            .SetDepthFormat(RenderTarget.DepthFormat)
            .SetMultisampling(target.SampleCount)
            .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE)
            .AddVertexBuffer(0, HitVertex.SizeInBytes)
            .AddVertexAttribute(0, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 0)
            .AddVertexAttribute(1, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 12)
            .AddVertexAttribute(2, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_UINT, 24)
            .AddVertexAttribute(3, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_UINT, 28);
        solidPipe = Base().EnableDepthTest(true, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL).DisableBlending().Build(device);
        clearPipe = Base().EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL).EnableBlendingAlpha().Build(device);
        linePipe = Base().SetPrimitiveType(SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_LINELIST)
            .EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL).EnableBlendingAlpha().Build(device);
        if (solidPipe == null || clearPipe == null || linePipe == null)
            throw new InvalidOperationException($"hitbox pipelines: {SDL_GetError()}");
    }

    void ReleasePipelines()
    {
        foreach (var p in new[] { (nint)solidPipe, (nint)clearPipe, (nint)linePipe })
            if (p != 0)
                SDL_ReleaseGPUGraphicsPipeline(device, (SDL_GPUGraphicsPipeline*)p);
        solidPipe = clearPipe = linePipe = null;
    }

    /// <summary>Drops the mesh: the next renders show only the background.</summary>
    public void Clear() => ReleaseMesh();

    void ReleaseMesh()
    {
        if (vertexBuf == null)
            return;
        SDL_WaitForGPUIdle(device);
        SDL_ReleaseGPUBuffer(device, vertexBuf);
        SDL_ReleaseGPUBuffer(device, solidIdx);
        SDL_ReleaseGPUBuffer(device, lineIdx);
        clearIdx?.Dispose();
        vertexBuf = solidIdx = lineIdx = null;
        (clearIdx, mesh) = (null, null);
    }

    public void Dispose()
    {
        ReleaseMesh();
        ReleasePipelines();
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fs);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct VsParams
    {
        public Matrix4x4 ViewProj;
        public Vector4 Misc;
        public uint MaskLo, MaskHi, Pad0, Pad1;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct FsParams
    {
        public Vector4 Light, ClipMin, ClipMax;
        public uint Hover, Selected, Pad0, Pad1;
        public fixed float Colours[4 * MaxKinds];
        public fixed float Edges[4 * MaxKinds];
    }
}
