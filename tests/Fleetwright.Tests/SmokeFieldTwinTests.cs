using System.Runtime.InteropServices;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using SDL;
using Xunit.Abstractions;
using static SDL.SDL3;

namespace Fleetwright.Tests;

/// <summary>The smoke field's drawing holds to the sim (docs/battle/smoke.md Z5): the copy the drawing reads carries
/// every puff, and the GLSL twin of the puffs' state (smokefield.glsl, run here by smokestate.comp on the GPU) gives
/// what <see cref="SmokeModel.State"/> gives, so what's drawn is what blocks sight. Needs a GPU (SDL_GPU, Vulkan).</summary>
[Trait("Category", "Gpu")]
public unsafe class SmokeFieldTwinTests(ITestOutputHelper output)
{
    [Fact]
    public void The_shader_works_a_puff_out_as_the_sim_does()
    {
        // a column smoking and firing, a ship making smoke, a magazine going up: every source's puffs, young and old
        var settings = SimSettings.Load(Path.Combine(Paths.Root, "Content", "Sim", "settings.json")) with { Smoke = true, RailsEnabled = false };
        using var registry = new ShipClassRegistry(0);
        var w = new World(WorldTime.FromUtc(new DateTime(1916, 5, 31, 14, 0, 0, DateTimeKind.Utc)), 3, settings, registry);
        int c = w.Ships.AddClass(ShipClassCompiler.Compile(Shipgen.ShipDesign.Build(Shipgen.Design.Load(Paths.Shipgen("designs", "dreadnought.json")))));
        w.Apply(Command.SetWeather(25_000, 250, 7, 1.5));
        for (int i = 0; i < 3; i++)
            w.Apply(ShipSystem.Spawn(c, 56.5 - 0.005 * i, 4.0, 90, 16, 0));
        for (int i = 0; i < 2; i++)
            w.Apply(ShipSystem.Spawn(c, 56.6, 4.0 - 0.01 * i, 90, 16, 1));
        w.Apply(GunnerySystem.Engage(4, 0, 1));
        w.Apply(SmokeSystem.MakeSmoke(5, true));
        w.AdvanceTo(w.Time + 5 * 60_000);
        w.Apply(DamageSystem.Sink(3, LossKind.Magazine));
        w.AdvanceTo(w.Time + 2 * 60_000);
        var snap = new Snapshot();
        w.Fill(snap, truth: true);
        Assert.NotNull(snap.SmokeCopies);
        snap.SmokeCopies!.Acquire();
        var copy = snap.SmokeCopies.Front;
        output.WriteLine($"{copy.Count} puffs copied of {w.Smoke.Count}");
        Assert.Equal(w.Smoke.Count, copy.Count);

        // their states 37.5 s after the copy, on the GPU
        float since = 37.5f;
        Assert.NotNull(copy.Eddies);
        var gpu = Run(copy, since);
        var air = new SmokeAir { SigmaV = copy.SigmaV, LagrangianH = copy.LagrangianH };
        double now = copy.CopiedMs / 1000.0 + since, worst = 0, worstEddy = 0, mostEddy = 0;
        for (int i = 0; i < copy.Count; i++)
        {
            ref readonly var r = ref copy.Records[i];
            var puff = new SmokePuff
            {
                Born = copy.CopiedMs / 1000.0 + r.Born, Z0 = r.Z0, Rise = r.Rise, RiseS = r.RiseS, A0 = r.A0, S0h2 = r.S0h2, S0z2 = r.S0z2,
                LifeS = r.LifeS, SigmaW = r.SigmaW, LagrangianW = r.LagrangianW,
            };
            var s = SmokeModel.State(puff, now, AirFrame.Identity, air);
            var g = gpu[2 * i];
            double[] want = [s.Z, s.SigmaH2, s.SigmaZ2, s.Area], got = [g.X, g.Y, g.Z, g.W];
            for (int k = 0; k < 4; k++)
            {
                double err = Math.Abs(got[k] - want[k]) / Math.Max(Math.Abs(want[k]), 1);
                worst = Math.Max(worst, err);
                Assert.True(err < 2e-4, $"puff {i}, quantity {k}: GPU {got[k]} against the sim's {want[k]}");
            }
            // its offset by the large eddies (Z5c), against SmokeEddies.At where it samples them
            var e = gpu[2 * i + 1];
            var wantEddy = copy.Eddies!.At(copy.Anchor + new Vec3d(r.KeyX, r.KeyY, r.KeyZ), now) * r.Eddy;
            double miss = (new Vec3d(e.X, e.Y, e.Z) - wantEddy).Length;
            (worstEddy, mostEddy) = (Math.Max(worstEddy, miss), Math.Max(mostEddy, wantEddy.Length));
            Assert.True(miss < 0.05, $"puff {i}: the eddies' offset {miss:F3} m off the sim's");
        }
        output.WriteLine($"the worst relative difference {worst:G3}; the eddies' offset at most {worstEddy:F4} m off (offsets up to {mostEddy:F0} m)");
        Assert.True(mostEddy > 10, "the eddies move the smoke");
    }

    [StructLayout(LayoutKind.Sequential)]
    struct State
    {
        public float X, Y, Z, W;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Params
    {
        public System.Numerics.Vector4 Info;
        public Fleetwright.Map.Smoke.SmokeFieldRenderer.EddyModes Eddies;
    }

    /// <summary>smokestate.comp on a device of its own: each record's state at <paramref name="since"/> s after the copy, and
    /// its offset by the large eddies then (two each).</summary>
    static State[] Run(SmokeCopy copy, float since)
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            SDL_SetHint(SDL_HINT_VIDEO_DRIVER, "offscreen");
            Assert.True(SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO), $"SDL_Init: {SDL_GetError()}");
        }
        var dev = SDL_CreateGPUDevice(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, false, (byte*)null);
        Assert.True(dev != null, $"SDL_CreateGPUDevice: {SDL_GetError()}");
        try
        {
            byte[] code = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "Shaders", "Compiled", "smokestate.comp.spv"));
            byte* entry = (byte*)Marshal.StringToHGlobalAnsi("main");
            SDL_GPUComputePipeline* pipe;
            fixed (byte* p = code)
            {
                var info = new SDL_GPUComputePipelineCreateInfo
                {
                    code = p, code_size = (nuint)code.Length, entrypoint = entry, format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
                    num_readonly_storage_buffers = 1, num_readwrite_storage_buffers = 1, num_uniform_buffers = 1,
                    threadcount_x = 64, threadcount_y = 1, threadcount_z = 1,
                };
                pipe = SDL_CreateGPUComputePipeline(dev, &info);
            }
            Marshal.FreeHGlobal((nint)entry);
            Assert.True(pipe != null, $"smokestate pipeline: {SDL_GetError()}");
            int n = copy.Count;
            uint inBytes = (uint)(n * SmokeRecord.SizeInBytes), outBytes = (uint)(2 * n * sizeof(State));
            var bi = new SDL_GPUBufferCreateInfo { usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_COMPUTE_STORAGE_READ, size = inBytes };
            var input = SDL_CreateGPUBuffer(dev, &bi);
            var bo = new SDL_GPUBufferCreateInfo { usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_COMPUTE_STORAGE_WRITE, size = outBytes };
            var outBuf = SDL_CreateGPUBuffer(dev, &bo);
            var tu = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, size = inBytes };
            var up = SDL_CreateGPUTransferBuffer(dev, &tu);
            var td = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_DOWNLOAD, size = outBytes };
            var down = SDL_CreateGPUTransferBuffer(dev, &td);
            copy.Records.AsSpan(0, n).CopyTo(new Span<SmokeRecord>((void*)SDL_MapGPUTransferBuffer(dev, up, false), n));
            SDL_UnmapGPUTransferBuffer(dev, up);

            var cmd = SDL_AcquireGPUCommandBuffer(dev);
            var cp = SDL_BeginGPUCopyPass(cmd);
            var src = new SDL_GPUTransferBufferLocation { transfer_buffer = up };
            var region = new SDL_GPUBufferRegion { buffer = input, size = inBytes };
            SDL_UploadToGPUBuffer(cp, &src, &region, false);
            SDL_EndGPUCopyPass(cp);
            var rw = new SDL_GPUStorageBufferReadWriteBinding { buffer = outBuf, cycle = false };
            var pass = SDL_BeginGPUComputePass(cmd, null, 0, &rw, 1);
            SDL_BindGPUComputePipeline(pass, pipe);
            SDL_BindGPUComputeStorageBuffers(pass, 0, &input, 1);
            var prm = new Params
            {
                Info = new System.Numerics.Vector4(since, (float)copy.SigmaV, (float)copy.LagrangianH, n),
                Eddies = Fleetwright.Map.Smoke.SmokeFieldRenderer.Eddies(copy.Eddies, copy.Anchor, copy.CopiedMs / 1000.0 + since),
            };
            SDL_PushGPUComputeUniformData(cmd, 0, (nint)(&prm), (uint)sizeof(Params));
            SDL_DispatchGPUCompute(pass, (uint)((n + 63) / 64), 1, 1);
            SDL_EndGPUComputePass(pass);
            cp = SDL_BeginGPUCopyPass(cmd);
            var from = new SDL_GPUBufferRegion { buffer = outBuf, size = outBytes };
            var to = new SDL_GPUTransferBufferLocation { transfer_buffer = down };
            SDL_DownloadFromGPUBuffer(cp, &from, &to);
            SDL_EndGPUCopyPass(cp);
            var fence = SDL_SubmitGPUCommandBufferAndAcquireFence(cmd);
            SDL_WaitForGPUFences(dev, true, &fence, 1);
            SDL_ReleaseGPUFence(dev, fence);
            var result = new Span<State>((void*)SDL_MapGPUTransferBuffer(dev, down, false), 2 * n).ToArray();
            SDL_UnmapGPUTransferBuffer(dev, down);
            SDL_ReleaseGPUTransferBuffer(dev, up);
            SDL_ReleaseGPUTransferBuffer(dev, down);
            SDL_ReleaseGPUBuffer(dev, input);
            SDL_ReleaseGPUBuffer(dev, outBuf);
            SDL_ReleaseGPUComputePipeline(dev, pipe);
            return result;
        }
        finally
        {
            SDL_DestroyGPUDevice(dev);
        }
    }
}
