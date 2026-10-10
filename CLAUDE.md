# Fleetwright

Fleetwright is a naval game written in C# on .NET 10, rendered with the SDL3 GPU API (via ppy.SDL3-CS). The exe currently opens the ship viewer (a test tool for the ship generator) with a Dear ImGui overlay; game code builds on top of it.

## Build and test

```bash
dotnet build          # whole solution (Fleetwright.slnx)
dotnet test
```

## Run: the ship viewer

```bash
dotnet run --project src/Fleetwright
dotnet run --project src/Fleetwright -- -ship=shipgen/designs/yamato.json [-navy=kure] [-era=wwii]
dotnet run --project src/Fleetwright -- -screenshot=30 [-output=ship.png]   # save frame 30 (default screenshot.png), exit
```

The exe opens the ship viewer. `-ship=` picks the design; without it, `shipgen/designs/bismarck.json` is looked up
from the working directory and the exe's folder upwards. It builds, bakes and shows a design from the baked textures
(mips, turrets through their arcs, height-map shadows); the "Ship" panel switches design, look, mip level, turrets
and sun. Wheel zooms, left drag pans, F2 shows the stats window, Escape quits.

## Shipgen CLI

The ship generator's command line, the port's test harness (`PORTING.md`):

```bash
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- golden-check          # every golden case, ~25 s
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- golden-check bismarck fuzz_lim_*
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- golden-update         # after a deliberate output change
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- design shipgen/designs/bismarck.json --out out --previews
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- validate shipgen/designs/*.json
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- bench bismarck yamato
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- draw shipgen/designs/bismarck.json --out out   # sprite.json + SVGs
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- bake shipgen/designs/bismarck.json --out out   # PNGs on the GPU
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- png-check             # bake all, IoU vs Python's PNGs
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- svg-check             # every case's SVGs, ~20 s
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- fuzz shipgen/designs/*.json --cases 1600   # robustness
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- verify out_designs/*  # sprites vs hitboxes
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- sprite-check          # every design's sprite.json
```

`golden-check` must stay at 371 of 371 for any change to `Fleetwright.Shipgen` that isn't a deliberate fix (fixes
update the goldens on purpose with `golden-update`, one per commit). `svg-check` and `sprite-check` do the same for
`Fleetwright.Shipgen.Render`, until the drawing is changed on purpose (then the SVG goldens retire).

## Profiling

`dotnet-trace` is installed; `docs/profiling.md` has the recipe (trace a Release exe, summarize with
`python -I tools/speedscope_top.py X.speedscope.json [--focus Method]`), allocation traces (`tools/alloctop`: the game
is soft real time, so garbage counts), how to read the numbers, the checks after an optimization, and a log of past
findings.

## Project Structure

```
Fleetwright.slnx
Directory.Build.props         # net10.0, nullable, InvariantGlobalization; warnings as errors in Fleetwright.Shipgen*
Directory.Packages.props      # central package versions (PackageReference has no Version)
src/
  Fleetwright/                # the game exe
    Program.cs                #   entry point, command-line parsing
    Engine.cs                 #   Sdl3GpuEngine: init, resources, render loop, PbrMaterial
    GltfLoader.cs             #   glTF/GLB loader (SharpGLTF), not wired to the command line
    ImGuiRenderer.cs          #   Dear ImGui backend on SDL3 GPU
    Camera.cs                 #   FPS camera (unused; to become the top-down Earth camera)
    ShipViewer.cs             #   the ship viewer (the default launch), a test tool for Shipgen
  Fleetwright.Gpu/            # GPU helpers for the game exe
    GpuTypes.cs               #   buffers, textures, samplers, DrawContext, SceneNode, MeshNode, materials
    GpuPipelineBuilder.cs     #   fluent pipeline builder
    GpuMath.cs                #   math helpers (perspective, lookAt)
    ShaderTypes.cs            #   vertex layout, uniforms, scene data
  Fleetwright.Shipgen/        # ship design library (ported, see below). No package references.
    Model/                    #   the typed data: Design (the input), Ship (the output), the JSON context and JsonFile
    Layout/                   #   layout.py: the Layout object, parts, superstructure levels, the warship layout
    Styles/                   #   the style hooks and the warship, carrier, merchant and planing styles
    Golden/                   #   the golden comparer and case runner
    Tools/                    #   the fuzz mutator
  Fleetwright.Shipgen.Render/ # the drawing side: reads only the built Ship (its render data)
    Data/looks.jsonc          #   every navy and era's colours and shapes (the documented table)
    Looks.cs                  #   resolving a design's look: from-chains, adjust, era muting
    Scene.cs                  #   the display list (paths, circles, rects, lines, text, clipped groups) and SvgWriter
    Painter.cs, HullArt.cs, TurretArt.cs, Clutter.cs   # shipgen.py's drawing and clutter.py
    Sprite.cs                 #   the height map and sprite.json (ShipSprites.Build)
    ShipRng.cs                #   the drawing's seeded RNG (per feature)
    RenderTypes.cs            #   Palette, Shapes and the sprite.json records
    Bake/                     #   the GPU bake: Lower (scene -> triangles), Stroker, Glyphs, GpuBaker (SDL_GPU), PNG, mips
    Golden/                   #   the SVG comparer and the drawing's golden checks
  Fleetwright.Shipgen.Cli/    # `shipgen` command, the port's test harness
tests/
  Fleetwright.Shipgen.Tests/  # xUnit: design input, every golden case, concurrent builds, the drawing, the
                              #   bake (trait Gpu: needs a GPU)
shipgen/                      # the port's test data
  designs/                    #   the 71 designs, and fuzz/ (300 mutants)
  golden/                     #   Python's output (README.md there)
Content/                      # copied into the game's output folder; the bake shaders are embedded in Shipgen.Render
  Shaders/Source/             #   GLSL sources
  Shaders/Compiled/           #   SPIR-V binaries (checked in)
docs/shipgen/                 # the ship designer: inputs, outputs, conventions, decisions, TODO, its research notes
research/                     # research notes, see below
```

Namespaces follow the project names: `Fleetwright`, `Fleetwright.Gpu`, `Fleetwright.Shipgen`, ...

Content paths in code (`"Content/Shaders/Compiled/..."`) resolve against `AppContext.BaseDirectory`, not the working directory.

## Renderer

- HDR R16G16B16A16 color target, 8x MSAA, resolved and blitted to the swapchain
- The ship viewer draws into it. Without a viewer the engine draws its scene graph instead (meshes with a base color
  texture and spherical-harmonics ambient light, `PbrMaterial` in Engine.cs), but nothing fills the scene today
- Dear ImGui overlay; Tracy zones in the frame loop

## Shaders

GLSL sources live in `Content/Shaders/Source/` and compile to SPIR-V in `Content/Shaders/Compiled/`. The compiled `.spv` files are checked in, so recompile after editing a shader. Requires `glslangValidator` from the Vulkan SDK:

```bash
compile_shaders.bat
```

## Dependencies

- ppy.SDL3-CS - SDL3 C# bindings
- ppy.SDL3_image-CS - texture decoding (glTF images) and PNG saving
- SharpGLTF.Toolkit - glTF/GLB loading
- ImGui.NET - Dear ImGui bindings
- TracyWrapper - Tracy profiler integration

## Research Notes

Research lives in `research/` and is produced by a separate research companion (Claude in the Claude app), which writes files directly into this folder.

- Before designing a non-trivial feature, check `research/` for an existing note on the topic.
- To ask for research, add an entry to `research/REQUESTS.md` (template inside) and tell the user. Don't block on it; continue with other work.
- Research notes are advisory: verify against the actual code before applying. Builds, tests, and git stay with Claude Code.
- Imported research from other projects lives in subfolders (e.g. `research/naval/`).

## Porting shipgen

The Python ship generator in `../shipgen` (frozen) has been ported here as `Fleetwright.Shipgen` (done 2026-10-09). `PORTING.md` holds the plan, the decisions and how each step went. `HANDOFF.md` has the latest session's notes and next steps: read it first. The shipgen docs live in `docs/shipgen/`.
