# Fleetwright

A naval game in C# / .NET 10, rendered with the SDL3 GPU API.

## Requirements

- .NET 10 SDK
- Vulkan SDK (only to recompile shaders)

## Build and run

```bash
dotnet build
dotnet test
dotnet run --project src/Fleetwright                                        # the ship viewer, on Bismarck
dotnet run --project src/Fleetwright -- -ship=shipgen/designs/yamato.json   # another design
dotnet run --project src/Fleetwright -- -screenshot=30                      # save frame 30 to screenshot.png and exit
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- design shipgen/designs/bismarck.json --out out_designs --previews
release.bat                                                                  # release/Fleetwright/: for players, no console or Tracy
release.bat DevRelease                                                       # release/Fleetwright-DevRelease/: with both
```

Controls: wheel zooms, left drag pans, the "Ship" panel picks the design and look, F2 shows stats, Escape quits.

## Layout

- `src/Fleetwright/` - the game: entry point, renderer, debug UI, the ship viewer
- `src/Fleetwright.Gpu/` - GPU types, pipeline builder, math
- `src/Fleetwright.Shipgen*/`, `tests/` - the ship generator: design side, drawing and GPU bake, the `shipgen` CLI
  (ported from Python, see `PORTING.md`; docs in `docs/shipgen/`)
- `Content/Shaders/` - GLSL sources and compiled SPIR-V (`compile_shaders.bat` rebuilds them)
- `research/` - design and research notes for the game's simulation and visuals
