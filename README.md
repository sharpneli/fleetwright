# Fleetwright

A naval game in C# / .NET 10, rendered with the SDL3 GPU API.

## Requirements

- .NET 10 SDK
- Vulkan SDK (only to recompile shaders)

## Build and run

```bash
dotnet build
dotnet run
dotnet run -- path/to/model.glb      # load a glTF/GLB model
dotnet run -- -screenshot=5          # save frame 5 to screenshot.png and exit
```

Controls: WASD to move, Space / Ctrl to go up and down, left mouse drag to look, Escape to quit.

## Layout

- `Program.cs`, `Engine.cs`, `GltfLoader.cs`, `ImGuiRenderer.cs` - entry point, renderer, model loading, debug UI
- `Shared/` - GPU types, pipeline builder, math, camera
- `Content/Shaders/` - GLSL sources and compiled SPIR-V (`compile_shaders.bat` rebuilds them)
- `research/` - design and research notes for the game's simulation and visuals
