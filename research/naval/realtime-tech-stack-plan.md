# Realtime implementation: tech stack & initial plan
Status: draft    Updated: 2026-10-08    Request: -

Status: draft v2 (2026-10-08). **Switched from C++/Vulkan to C#/.NET + SDL_GPU.** Still to iterate.

## Goals

A hobby project and a test of the AI-assisted workflow. **The learning and the finished game both matter.** That pushes the stack toward:

1. A dev loop the agent can run and verify end to end, without a human in the loop for routine work.
2. Low build and tooling friction on Windows.
3. Enough performance headroom for the full WW1 Royal Navy at smooth frame rates.

## What we're building (renderer's point of view)

- **The world is a real-scale Earth.** The camera is fixed, looks straight down at the planet's core, and only changes altitude, from orbit down to deck level. **The planet rotates under the camera.**
- **Ships are mipmapped sprites from shipgen:** hull layers (hull_base and hull_upper), a heightmap, and rotating turret sprites. The finest level is 10 px/m. Far-away ships collapse to icons. Hitboxes come from geometry.py, the same source as the sprites.
- **Effects:** ocean shading, wakes, funnel and gun smoke, muzzle flash, splashes, and later fires, sinking and explosions (see the VFX research docs). Anything with height gets a 3/4 RTS-style screen shear for looks. The camera itself stays top-down.
- **A real sun** placed by date and time, giving correct shadows and day/night, and night actions.
- **UI** on top. Aircraft come later.

The renderer needs instanced sprites, compute particles, planet tiles, a few full-screen passes and a UI. That's well within SDL_GPU.

## Why C# + SDL_GPU (and not C++/Vulkan)

- **The build friction goes away.** `dotnet build` works from any terminal, with no MSVC dev shell, vcvars or vcpkg. That was the expected main source of hassle.
- **Mistakes are easier to find.** In C#, my errors surface as exceptions with stack traces instead of silent undefined behaviour. Iteration is faster, and I'm more consistently correct in C#. Both help the agent loop.
- **SDL_GPU handles barriers and synchronisation for us.** That's a bug class I'd otherwise be likely to introduce. It runs on Vulkan or D3D12 underneath.
- **Costs we accept:**
  - **No bindless.** Texture arrays grouped by size replace it.
  - **Possibly no GPU timestamp queries in SDL_GPU (to verify in Phase 1).** The fallbacks are below.
  - **The garbage collector needs discipline in hot paths** (see the rules below).
  - **Native tools are called from C# through their C APIs:** Tracy, and Aftermath if needed.
- **Alternatives considered:**
  - **C++ with raw Vulkan:** maximum control, but most of the expected friction lives in the toolchain and in synchronisation.
  - **Rust with wgpu:** good validation and timestamps, but it would mean learning a new language alongside the workflow experiment, which muddies both. Borrow-checker friction in simulation code is also real.
  - **A C++ renderer DLL with a C# game on top:** brings back the build pain and adds an interop boundary. Rejected.

## Decisions (proposed)

| Area | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 (LTS), C# latest, NativeAOT for shipping builds | AOT gives one native exe, no JIT warm-up, and catches trimming and reflection problems early. Debug builds use the JIT for fast iteration and hot reload. |
| Build | `dotnet` CLI, one solution, `Directory.Build.props`, central package management | No scripts needed to enter a toolchain environment. |
| Windowing, input, GPU | SDL3 + SDL_GPU through the bindings already in the skeleton | Force the Vulkan backend in debug so validation layers apply. |
| Shaders | HLSL → SPIR-V/DXIL via SDL_shadercross (or DXC directly) in an MSBuild step, plus runtime compilation for hot reload | Reflection data is used to check resource bindings at load time. |
| Math | `System.Numerics` (float, SIMD) for rendering and per-frame work. **double** for world and simulation positions, with small custom double vector/quaternion types | Rendering is camera-relative by construction (see Precision). |
| ECS / data | Hand-written struct-of-arrays stores per entity kind (ships, shells, aircraft), or Arch if we want archetype queries | At ≤2,000 ships + tens of thousands of shells, simple SoA is enough. Decide in Phase 4. |
| Jobs | Our own small job system (see Simulation) | |
| Debug UI | Dear ImGui via ImGui.NET (or Hexa.NET.ImGui) with our own SDL_GPU backend | Dev overlays only. |
| Game UI | Our own retained/immediate layer on the sprite + MSDF text renderer, decided in Phase 5 | |
| Text | MSDF atlas generated at build time (msdf-atlas-gen) | |
| Images | KTX2 with BC7 (albedo) and BC4 (height), pre-baked from shipgen output | Mips are already generated upstream. |
| Audio | SDL3 audio, or miniaudio bindings later | |
| Tests | xUnit (or TUnit), BenchmarkDotNet, our own golden-image and simulation harness | |
| Profiling | Tracy via C# bindings, `dotnet-trace` / `dotnet-counters`, PresentMon | All have CLI or text output. |

### Hot-path rules (enforced by tests)

- No allocations per frame or per tick in simulation and rendering. A test runs N frames and asserts `GC.GetAllocatedBytesForCurrentThread()` didn't change (per worker thread).
- Structs, `Span<T>`, `stackalloc`, preallocated pools or `NativeMemory` for large arrays. No LINQ, closures, boxing, `params object[]` or string formatting in hot loops.
- GC settings: concurrent workstation GC, `GCSettings.LatencyMode = SustainedLowLatency` during play.
- SIMD with `Vector128/256<T>` where profiling says it matters.

## Rendering approach

### Camera, planet rotation and precision

- **The camera is fixed on an axis looking at the core.** The only parameter is altitude, with a fixed field of view and a perspective projection (near-orthographic close to the surface, correct globe from orbit).
- **Panning and rotating are a double-precision quaternion that rotates the Earth** so the point of interest sits under the camera. World→view is "apply the planet rotation, subtract the camera radius". That makes everything camera-relative. Positions are converted to float only after that subtraction.
- **Draw order is explicit layering:** water → wakes → ship shadows → hull_base → turrets → hull_upper → smoke and effects → UI. A reversed-Z depth buffer is used for the globe and terrain at altitude.
- **The 3/4 cheat:** anything with height (smoke, splash columns, plumes, aircraft altitude) gets a "height → screen offset" shear in one fixed screen direction. One tunable constant controls it, and it can vary with zoom.

### Sun and time of day

- **The sun position comes from the simulation clock** (UTC date and time) using a standard solar-position algorithm (NOAA or the Astronomical Almanac low-precision formulas). That gives one sun vector in Earth-fixed coordinates. Local sun elevation varies across the globe on its own, so the terminator and the dawn sweep are correct.
- **Lighting:** ocean glint, warm low-sun colour, ambient sky light, and shadows (below).
- **Night is a real game state** (Jutland's night phase). It needs a night palette, the moon (nice to have, from the same ephemeris), and local lights for searchlights, star shells, gun flashes and fires via a 2D light accumulation buffer.
- **The 3/4 shear is a stylistic constant; shadows follow the real sun.** If the mismatch looks odd at low sun angles, tune it visually.

### Planet

- **Cube-sphere with quadtree chunk LOD** selected by screen-space error, and a whole-globe mesh at orbit.
- **Land and coastlines** from Natural Earth plus GEBCO/ETOPO, baked offline into tiles. Shallow-water colour comes from bathymetry.
- **Ocean shading** uses normal-map cascades from [`ocean-surface-research.md`](../vfx/ocean-surface-research.md), fading to albedo plus glint at altitude.

### Ships: sprites + heightmap

- **Assets per design:** hull layer sprites, a hull heightmap, and turret sprites each with their own heightmap. Mipmapped, with a 10 px/m maximum. Memory is per design, not per ship: a battlecruiser is about 2,140 × 270 px at the finest mip.
- **On the GPU:** BC7 for albedo and BC4 for height in **texture arrays grouped by size class.** The per-instance array index replaces bindless.
- **Drawing:** instanced quads, one draw per layer pass (all hull_base, all turrets, all hull_upper). Per-instance transform, design index and turret angles come from a storage buffer filled once per frame. Premultiplied alpha.
- **Heightmap lighting:**
  - **Water shadows and deck self-shadowing** come from a short raymarch along the sun vector through the height fields. Length is capped at low sun angles.
  - **Normals derived from height** give sun shading and cheap ambient occlusion.
  - **Heights also place effect emitters:** funnel tops, gun muzzles.
- **Icons** replace ships below the coarsest mip.
- **Contract with shipgen:** the Python shipgen stays an offline tool for now. It exports sprites, heightmaps, hitboxes, firing arcs, and a JSON manifest (mount positions, pivots, layer heights). An asset build step turns those into KTX2 + a binary manifest. Porting shipgen to C# can come later, keeping geometry.py's semantics.

### Effects

- **GPU particles:** a compute update and an indirect draw per type (smoke, spray, splashes, muzzle flash).
- **Wakes:** world-space trail buffers per ship, following the wake bake research.
- **Soft smoke:** sorted particles at low resolution, then upsampled, with the 3/4 shear applied.

## Simulation

- **Fixed tick** (start at 20–30 Hz), decoupled from rendering, with interpolation.
- **Deterministic:** seeded RNG, no wall-clock reads, stable iteration order, and no `Parallel.ForEach` reductions whose order depends on scheduling. Same seed means bit-identical results on 1 thread or 16, which a test verifies. That determinism gives us replays and headless simulation tests the agent can run.
- **Scale:** ≤2,000 ships, tens of thousands of shells, with particles on the GPU. The cost is per-ship models (fire control, damage, flooding, crew), not entity count.
- **Spatial index:** a grid per cube face for the broadphase, then hitbox narrowphase.
- **Simulation LOD:** fleets far from any contact run coarse movement only.

### Job system (our own)

- **Fixed worker threads,** one per physical core minus the main thread, each with high-priority affinity hints.
- **Jobs are structs in preallocated ring buffers,** with no allocations per job. Work-stealing deques per worker.
- **`ParallelFor(range, batchSize, in TJob)`** with generic struct jobs, so the JIT/AOT specialises them and nothing is boxed.
- **Dependencies are counters,** and waiting threads help run jobs instead of blocking.
- **Tracy zones per job** in profiling builds.
- **Tests:** random dependency graphs (no lost or duplicated work), determinism across thread counts, BenchmarkDotNet throughput against `Parallel.For` as a baseline, and the zero-allocation check.

**Budget targets (to confirm with the GPU model):** sim ≤ 4 ms per tick with a full-RN scenario, and render ≤ 6 ms GPU at 1440p.

## Dev loop: the agent verifies its own work

### Where things run

- **Claude Code runs natively on your Windows machine** (it needs Git for Windows), so it can build and run against the real GPU.
- **This cloud project holds research, design docs and reference scripts.**
- **The editor is your choice:** Visual Studio, Rider or VS Code with C# Dev Kit. The build is `dotnet` either way, so editor state never affects it.

### Feedback ladder (text first)

1. **Validation:** debug builds force SDL_GPU's Vulkan backend with debug mode on, and route validation layer output to a log. **Any error fails the run.**
2. **Headless golden images:** `Naval.exe --headless --scenario X --frames N --capture out/` writes PNGs. The test harness diffs them against references with a tolerance and writes diff images. The agent can look at both.
3. **Frame timing:** CPU-side timing per phase always. GPU timing via SDL_GPU timestamps if available. If not: GPU frame time from fences or present timing, PresentMon CSV output, and Nsight GPU Trace for deep dives (run by you).
4. **Tracy:** `tracy-capture` from the CLI, then `tracy-csvexport`, gives CSV the agent can read.
5. **.NET diagnostics:** `dotnet-counters` (GC counts, allocation rate) and `dotnet-trace` give text and CSV, plus the zero-allocation test.
6. **Frame inspection:** RenderDoc captures, with scripted queries through RenderDoc's Python API. Debug labels on every pass via SDL_GPU's debug group and label calls.
7. **GPU crashes:** device-lost errors are logged with the last debug labels. Nsight Aftermath via P/Invoke only if crashes become a real problem.
8. **Nsight Graphics:** a tool for you. You run GPU Trace on the bench scenario and paste or export the findings.

### Repo hygiene

- **`CLAUDE.md`:** build and test commands, hot-path rules, directory map, how to update golden images deliberately, "tests pass before commit".
- **Analyzers:** nullable enabled, warnings as errors, and an analyzer or banned-API list for hot-path assemblies (no LINQ etc.).
- **CI (GitHub Actions, Windows runner):** build, unit tests, headless simulation tests and the AOT publish. GPU golden tests run locally.

## Phases

**Phase 0: Done.** Your existing skeleton (SDL3 + SDL_GPU bindings, builds) is the starting point for Claude Code on your machine.

**Phase 1: Rendering skeleton**
- Check what the skeleton already covers. Then add `CLAUDE.md`, the test project, and CI.
- Route validation output to a log and fail the run on errors. Check whether SDL_GPU has timestamp queries. Confirm the shader build step and the NativeAOT publish.
- Frame loop, headless capture, sprite batch with texture arrays, shader hot reload, ImGui overlay, Tracy.
- Done when the agent builds, runs, captures and diffs a frame with no human input.

**Phase 2: The planet**
- Fixed camera and planet rotation, double-precision path, cube-sphere LOD, coastline and bathymetry bake, ocean shading, sun ephemeris and terminator.
- Golden tests: sun vectors against reference values, and the same spot at 10 altitudes with no jitter or cracks.

**Phase 3: Ships on the water**
- shipgen manifest and KTX2 asset pipeline, instanced hull and turret layers, heightmap shadows and shading, icon collapse.
- Wakes, smoke, muzzle flash with the 3/4 shear.
- Done when the Lion/Tiger video scene is reproduced in real time.

**Phase 4: Simulation core**
- Job system, entity stores, deterministic fixed tick, movement and formations, fire control, ballistics, hitbox hits, damage-model skeleton from the damage research.
- Headless simulation tests. Stress scenario: the full 1914 RN order of battle.

**Phase 5+: The game**
- Game UI, orders, audio, save and replay, night lighting, aircraft.

## Open questions

1. GPU model (perf budget, reference machine for golden images).
2. Repo hosting (GitHub?) for CI.
3. Editor preference (doesn't block anything).

## Decision log

- 2026-10-08: Camera strictly nadir; the planet rotates under it; height effects use a 3/4 shear.
- 2026-10-08: Sun from a real ephemeris; shadows follow it.
- 2026-10-08: Ships are sprites + heightmap + turret sprites, mipmapped, max 10 px/m; icons when far away.
- 2026-10-08: Phase 0 dropped. The existing skeleton with SDL_GPU bindings is the starting point.
- 2026-10-08: Language and stack switched from C++20/Vulkan 1.3 to C#/.NET 10 + SDL_GPU, because of toolchain friction and agent-loop reliability.
