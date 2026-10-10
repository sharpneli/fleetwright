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
dotnet run --project src/Fleetwright -- -view=hitbox [-camera=bow|quarter|side|internal|plan] [-show=all|outside|internal|rooms|armour]
```

The exe opens the ship viewer. `-ship=` picks the design; without it, it shows `Content/Designs/bismarck.json` next to
the exe (a game asset, below), and the "Ship" panel lists the design's folder. It builds, bakes and shows a design from the baked textures
(mips, turrets through their arcs, height-map shadows); the "Ship" panel switches design, look, mip level, turrets
and sun. Wheel zooms, left drag pans, F2 shows the stats window, Escape quits.

The menu bar (or F3) switches to the hitbox viewer: the same design's hitbox model in 3D (`HitView/`, the live
`hitview.py`). Left drag orbits, right drag pans, wheel zooms; its panel picks the view, the projection and which kinds
are drawn. `-screenshot` captures the scene without the ImGui overlay.

## Release and game assets

Three configurations:

- **Debug** - the everyday build: console, Tracy.
- **Release** - what players get: optimized, no console window (`WinExe`), no Tracy (`TracyWrapper` isn't referenced
  and `ProfilerStubs.cs` stands in for its calls as no-ops). Console output goes nowhere; there's no log file yet.
- **DevRelease** - for us: optimized like Release, with the console and Tracy (the `TRACY` define).

```bash
release.bat              # release/Fleetwright/            (gitignored)
release.bat DevRelease   # release/Fleetwright-DevRelease/
```

Each holds Fleetwright.exe, the .NET runtime, the native DLLs and the game assets: a self-contained win-x64 publish
that runs on a stock Windows 11 with nothing installed, except the VC++ runtime (`vcruntime140`, `msvcp140`;
SDL3_image and TracyClient import them), which is assumed present.

Game assets are the data the game ships with, listed as `GameAsset` items in `src/Fleetwright/Fleetwright.csproj`:
each lands in the build and publish output under its `Link` path and is read through `AppContext.BaseDirectory`, so a
dev build and a release find it the same way. Today the assets are the designs, `shipgen/designs/*.json` ->
`Content/Designs/` (not `fuzz/`, which is test data). Code must not reach into the repo for data a release needs.

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
release.bat                   # builds release/Fleetwright/ (see "Release and game assets")
Directory.Build.props         # net10.0, nullable, InvariantGlobalization; warnings as errors in Fleetwright.Shipgen*
Directory.Packages.props      # central package versions (PackageReference has no Version)
src/
  Fleetwright/                # the game exe
    Program.cs                #   entry point, command-line parsing
    Engine.cs                 #   Sdl3GpuEngine: init, resources, render loop, PbrMaterial
    GltfLoader.cs             #   glTF/GLB loader (SharpGLTF), not wired to the command line
    ImGuiRenderer.cs          #   Dear ImGui backend on SDL3 GPU
    Camera.cs                 #   FPS camera (unused; to become the top-down Earth camera)
    DesignSession.cs          #   the picked design and its built Ship, shared by the scenes (they watch Version)
    IScene.cs                 #   a full-window view with its own UI; the engine draws the current one (see Views)
    ShipViewer.cs             #   the ship viewer (the default launch), a test tool for Shipgen
    SceneSwitcher.cs          #   several scenes behind one IScene: the menu bar and F3 switch
    HitView/                  #   the hitbox viewer: HitboxMesh (hitboxes as triangles, CPU), HitboxRenderer (into any
                              #   RenderTarget), HitboxCamera and HitboxViewState (what it shows), HitboxScene (UI)
    ProfilerStubs.cs          #   no-op Tracy stand-ins for Release
  Fleetwright.Gpu/            # GPU helpers for the game exe
    GpuTypes.cs               #   buffers, textures, samplers, DrawContext, SceneNode, MeshNode, materials
    GpuPipelineBuilder.cs     #   fluent pipeline builder
    GpuMath.cs                #   math helpers (perspective, lookAt)
    ShaderTypes.cs            #   vertex layout, uniforms, scene data
    RenderTarget.cs           #   colour (+MSAA resolve) and depth a view draws into: the main target or offscreen
    GpuShader.cs              #   SPIR-V loading from a device alone
    GpuUpload.cs              #   static buffer uploads, and DynamicGpuBuffer (refilled within a frame)
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
  Fleetwright.Tests/          # xUnit for the game exe: the hitbox mesh over every design
  Fleetwright.Shipgen.Tests/  # xUnit: design input, every golden case, concurrent builds, the drawing, the
                              #   bake (trait Gpu: needs a GPU)
shipgen/                      # the port's test data
  designs/                    #   the 71 designs, and fuzz/ (300 mutants)
  golden/                     #   Python's output (README.md there)
Content/                      # copied into the game's output folder; the bake shaders are embedded in Shipgen.Render
                              #   (the output's Content/Designs/ comes from shipgen/designs, see GameAsset)
  Shaders/Source/             #   GLSL sources
  Shaders/Compiled/           #   SPIR-V binaries (checked in)
docs/shipgen/                 # the ship designer: inputs, outputs, conventions, decisions, TODO, its research notes
research/                     # research notes, see below
```

Namespaces follow the project names: `Fleetwright`, `Fleetwright.Gpu`, `Fleetwright.Shipgen`, ...

Content paths in code (`"Content/Shaders/Compiled/..."`) resolve against `AppContext.BaseDirectory`, not the working directory.

## Renderer

- HDR R16G16B16A16 color target, 8x MSAA, resolved and blitted to the swapchain
- The engine's main `RenderTarget` holds it; the current `IScene` (the ship viewer) draws into it. Without a scene the
  engine draws its scene graph instead (meshes with a base color
  texture and spherical-harmonics ambient light, `PbrMaterial` in Engine.cs), but nothing fills the scene today
- Dear ImGui overlay; Tracy zones in the frame loop (not in Release, see above)

## Views

Every view is built in layers joined by interfaces, so each runs on its own and none needs engine changes:

- a CPU step from the ship's data (the hitbox model) to what is drawn (a mesh), with no GPU;
- a renderer that draws into any `RenderTarget` (the main one, or a small offscreen one shown with `ImGui.Image` or
  on a game UI quad), driven by plain camera/state structs. It takes a device, never the engine; it reads the
  target's format and sample count to build its pipelines, and knows nothing of input, ImGui or the window;
- an `IScene`: input and its own ImGui UI around a renderer. The engine owns the window, the frame loop and the main
  target and calls the current scene each frame.

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
- TracyWrapper - Tracy profiler integration (Debug and DevRelease only)

## Research Notes

Research lives in `research/` and is produced by a separate research companion (Claude in the Claude app), which writes files directly into this folder.

- Before designing a non-trivial feature, check `research/` for an existing note on the topic.
- To ask for research, add an entry to `research/REQUESTS.md` (template inside) and tell the user. Don't block on it; continue with other work.
- Research notes are advisory: verify against the actual code before applying. Builds, tests, and git stay with Claude Code.
- Imported research from other projects lives in subfolders (e.g. `research/naval/`).

## Porting shipgen

The Python ship generator in `../shipgen` (frozen) has been ported here as `Fleetwright.Shipgen` (done 2026-10-09). `PORTING.md` holds the plan, the decisions and how each step went. `HANDOFF.md` has the latest session's notes and next steps: read it first. The shipgen docs live in `docs/shipgen/`.
