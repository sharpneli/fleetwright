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
  Fleetwright.Gpu/            # GPU helpers shared by the game and the Shipgen renderer
    GpuTypes.cs               #   buffers, textures, samplers, DrawContext, SceneNode, MeshNode, materials
    GpuPipelineBuilder.cs     #   fluent pipeline builder
    GpuMath.cs                #   math helpers (perspective, lookAt)
    ShaderTypes.cs            #   vertex layout, uniforms, scene data
  Fleetwright.Shipgen/        # ship design library (being ported, see below). No package references.
  Fleetwright.Shipgen.Cli/    # `shipgen` command, the port's test harness
tests/
  Fleetwright.Shipgen.Tests/  # xUnit
Content/                      # shared by all exes, copied into each exe's output folder
  Shaders/Source/             #   GLSL sources
  Shaders/Compiled/           #   SPIR-V binaries (checked in)
  Models/                     #   glTF/GLB assets
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

The Python ship generator in `../shipgen` (frozen) is being ported here as `Fleetwright.Shipgen`. `PORTING.md` holds the plan, the decisions and the progress checklist: read it before working on the port, and tick off steps as they're done.
