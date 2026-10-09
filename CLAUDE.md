# Fleetwright

Fleetwright is a naval game written in C# on .NET 10, rendered with the SDL3 GPU API (via ppy.SDL3-CS). The renderer currently does PBR materials, glTF loading, a scene graph and a Dear ImGui overlay; game code builds on top of it.

## Build and test

```bash
dotnet build          # whole solution (Fleetwright.slnx)
dotnet test
```

## Run

```bash
dotnet run --project src/Fleetwright

# Load a glTF/GLB model
dotnet run --project src/Fleetwright -- -model=path/to/model.glb
dotnet run --project src/Fleetwright -- path/to/model.gltf
```

With no model loaded, the engine draws a placeholder cube at the origin.

## Screenshots

Capture a specific frame as a PNG image:

```bash
# Capture frame 5 to screenshot.png
dotnet run --project src/Fleetwright -- -screenshot=5

# With a model and custom output path
dotnet run --project src/Fleetwright -- path/to/model.glb -screenshot=10 -output=my_screenshot.png
```

The application exits after capturing the screenshot.

## Controls

- **WASD** - Move camera
- **Space** - Move up
- **Left Ctrl/Shift** - Move down
- **Left Mouse + Drag** - Look around
- **Escape** - Exit

## Ship viewer

```bash
dotnet run --project src/Fleetwright -- -ship=shipgen/designs/bismarck.json [-navy=kure] [-era=wwii]
dotnet run --project src/Fleetwright -- -ship=shipgen/designs/bismarck.json -screenshot=30 -output=ship.png
```

Builds, bakes and shows a design from the baked textures (mips, turrets through their arcs, height-map shadows);
the "Ship" panel switches design, look, mip level, turrets and sun. Wheel zooms, left drag pans.

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

## Project Structure

```
Fleetwright.slnx
Directory.Build.props         # net10.0, nullable, InvariantGlobalization; warnings as errors in Fleetwright.Shipgen*
Directory.Packages.props      # central package versions (PackageReference has no Version)
src/
  Fleetwright/                # the game exe
    Program.cs                #   entry point, command-line parsing
    Engine.cs                 #   Sdl3GpuEngine: init, resources, render loop, PbrMaterial
    GltfLoader.cs             #   glTF/GLB loader (SharpGLTF)
    ImGuiRenderer.cs          #   Dear ImGui backend on SDL3 GPU
    Camera.cs                 #   FPS camera
    ShipViewer.cs             #   the ship viewer (-ship=), a test tool for Shipgen
  Fleetwright.Gpu/            # GPU helpers shared by the game and the Shipgen renderer
    GpuTypes.cs               #   buffers, textures, samplers, DrawContext, SceneNode, MeshNode, materials
    GpuPipelineBuilder.cs     #   fluent pipeline builder
    GpuMath.cs                #   math helpers (perspective, lookAt)
    ShaderTypes.cs            #   vertex layout, uniforms, scene data
  Fleetwright.Shipgen/        # ship design library (ported, see below). No package references.
    Py/                       #   the Python runtime the port stands on: PyDict, Py (numerics, repr, formats), PyJson
    Layout/                   #   layout.py: the Layout object, parts, superstructure levels, the warship layout
    Styles/                   #   the style hooks and the warship, carrier, merchant and planing styles
    Golden/                   #   the golden comparer and case runner
    Tools/                    #   the fuzz mutator
  Fleetwright.Shipgen.Render/ # the drawing side: reads only the built ship dict
    Data/looks.jsonc          #   every navy and era's colours and shapes (the documented table)
    Looks.cs                  #   resolving a design's look: from-chains, adjust, era muting
    Scene.cs                  #   the display list (paths, circles, rects, lines, text, clipped groups) and SvgWriter
    Painter.cs, HullArt.cs, TurretArt.cs, Clutter.cs   # shipgen.py's drawing and clutter.py
    Sprite.cs                 #   the height map and sprite.json (ShipSprites.Build)
    ShipRng.cs                #   the drawing's seeded RNG (per feature)
    Bake/                     #   the GPU bake: Lower (scene -> triangles), Stroker, Glyphs, GpuBaker (SDL_GPU), PNG, mips
    Golden/                   #   the SVG comparer and the drawing's golden checks
  Fleetwright.Shipgen.Cli/    # `shipgen` command, the port's test harness
tests/
  Fleetwright.Shipgen.Tests/  # xUnit: Py helpers vs CPython, every golden case, concurrent builds, the drawing, the
                              #   bake (trait Gpu: needs a GPU)
shipgen/                      # the port's test data
  designs/                    #   the 71 designs, and fuzz/ (300 mutants)
  golden/                     #   Python's output (README.md there)
Content/                      # shared by all exes, copied into each exe's output folder
  Shaders/Source/             #   GLSL sources
  Shaders/Compiled/           #   SPIR-V binaries (checked in)
  Models/                     #   glTF/GLB assets
docs/shipgen/                 # the ship designer: inputs, outputs, conventions, decisions, TODO, its research notes
research/                     # research notes, see below
```

Namespaces follow the project names: `Fleetwright`, `Fleetwright.Gpu`, `Fleetwright.Shipgen`, ...

Content paths in code (`"Content/Shaders/Compiled/..."`) resolve against `AppContext.BaseDirectory`, not the working directory.

## Renderer features

- HDR rendering with R16G16B16A16 color target
- 8x MSAA
- Reverse-Z depth buffer
- PBR materials (metallic-roughness)
- glTF/GLB loading via SharpGLTF
- Hierarchical scene graph
- FPS-style camera with mouse look
- Dear ImGui overlay (stats window)

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
