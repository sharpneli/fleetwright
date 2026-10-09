# Fleetwright

A naval game in C# / .NET 10, rendered with the SDL3 GPU API.

## Requirements

- .NET 10 SDK
- Vulkan SDK (only to recompile shaders)

## Build and run

```bash
dotnet build
dotnet test
dotnet run --project src/Fleetwright
dotnet run --project src/Fleetwright -- path/to/model.glb   # load a glTF/GLB model
dotnet run --project src/Fleetwright -- -screenshot=5       # save frame 5 to screenshot.png and exit
```

Controls: WASD to move, Space / Ctrl to go up and down, left mouse drag to look, Escape to quit.

## Layout

- `src/Fleetwright/` - the game: entry point, renderer, model loading, debug UI, camera
- `src/Fleetwright.Gpu/` - GPU types, pipeline builder, math
- `src/Fleetwright.Shipgen*/`, `tests/` - the ship generator, being ported from Python (see `PORTING.md`)
- `Content/Shaders/` - GLSL sources and compiled SPIR-V (`compile_shaders.bat` rebuilds them)
- `research/` - design and research notes for the game's simulation and visuals
